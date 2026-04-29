using System;
using System.Collections.Generic;
using TraceEventImporter.Models;
using TraceEventImporter.Normalization;

namespace TraceEventImporter.Processing
{
    /// <summary>
    /// Correlates Starting↔Completed events per session+request, normalizes SQL text,
    /// computes HashIDs, and produces rows for tblBatches, tblStatements, tblConnections,
    /// and tblInterestingEvents.
    /// </summary>
    public class EventProcessor
    {
        private readonly UniqueStore _store;

        // Pending batch events awaiting their Completed counterpart
        private readonly Dictionary<SessionRequestKey, PendingBatch> _pendingBatches =
            new Dictionary<SessionRequestKey, PendingBatch>();

        // Pending statement events (nested via stack per session+request)
        private readonly Dictionary<SessionRequestKey, Stack<PendingStatement>> _pendingStatements =
            new Dictionary<SessionRequestKey, Stack<PendingStatement>>();

        // Connection tracking (login→logout)
        private readonly Dictionary<int, ConnectionInfo> _connections =
            new Dictionary<int, ConnectionInfo>();

        // Per session+request: the StartSeq of the current in-flight batch.
        // This is how ReadTrace links statements to their parent batch.
        private readonly Dictionary<SessionRequestKey, long> _curBatchStartSeq =
            new Dictionary<SessionRequestKey, long>();

        // Sessions that have had a connection event (login/existing connection).
        // Used to detect sessions that were "connected before trace" started.
        // Value is the ConnSeq assigned to that session's connection row.
        private readonly Dictionary<int, long> _sessionConnSeq =
            new Dictionary<int, long>();

        // All sequence values (ConnSeq, BatchSeq, StmtSeq) are derived from the
        // global event sequence (evt.Seq), aligned with ReadTrace.exe which uses
        // pEvent->GetGlobalSeq() for all of them. No separate counters needed.

        // Collected rows for bulk insert
        public List<BatchRow> Batches { get; } = new List<BatchRow>();
        public List<StatementRow> Statements { get; } = new List<StatementRow>();
        public List<ConnectionRow> Connections { get; } = new List<ConnectionRow>();
        public List<InterestingEventRow> InterestingEvents { get; } = new List<InterestingEventRow>();

        public EventProcessor(UniqueStore store)
        {
            _store = store;
        }

        public void ProcessEvent(TraceEvent evt)
        {
            _store.AddTracedEvent(evt.EventId);

            switch (evt.EventType)
            {
                case TraceEventType.SqlBatchStarting:
                case TraceEventType.RpcStarting:
                    HandleBatchStarting(evt);
                    break;

                case TraceEventType.SqlBatchCompleted:
                case TraceEventType.RpcCompleted:
                    HandleBatchCompleted(evt);
                    break;

                case TraceEventType.SpStmtStarting:
                case TraceEventType.StmtStarting:
                    HandleStatementStarting(evt);
                    break;

                case TraceEventType.SpStmtCompleted:
                case TraceEventType.StmtCompleted:
                    HandleStatementCompleted(evt);
                    break;

                case TraceEventType.AuditLogin:
                case TraceEventType.ExistingConnection:
                    HandleLogin(evt);
                    break;

                case TraceEventType.AuditLogout:
                    HandleLogout(evt);
                    break;

                case TraceEventType.Attention:
                    HandleAttention(evt);
                    break;

                default:
                    // Interesting events (recompile, autogrow, etc.)
                    if (evt.EventType != TraceEventType.Unknown)
                        HandleInterestingEvent(evt);
                    break;
            }
        }

        /// <summary>
        /// Finalize: flush any pending connections that never got a logout event.
        /// </summary>
        public void Finalize()
        {
            foreach (var conn in _connections.Values)
            {
                Connections.Add(new ConnectionRow
                {
                    ConnSeq = conn.ConnSeq,
                    Session = conn.SessionId,
                    StartTime = conn.StartTime,
                    EndTime = null,
                    Duration = null,
                    Reads = null,
                    Writes = null,
                    CPU = null,
                    ApplicationName = conn.AppName,
                    LoginName = conn.LoginName,
                    HostName = conn.HostName,
                    NTDomainName = conn.NTDomainName,
                    NTUserName = conn.NTUserName,
                    StartSeq = conn.StartSeq,
                    EndSeq = null,
                    TextData = conn.TextData
                });
            }
            _connections.Clear();
        }

        #region Batch Handling

        private void HandleBatchStarting(TraceEvent evt)
        {
            var key = new SessionRequestKey(evt.SessionId, evt.RequestId);

            // Track the starting sequence for this session+request so statements
            // processed during batch execution can reference it (ReadTrace pattern).
            _curBatchStartSeq[key] = evt.Seq;

            _pendingBatches[key] = new PendingBatch
            {
                StartSeq = evt.Seq,
                StartTime = evt.StartTime,
                TextData = evt.TextData,
                ObjectName = evt.ObjectName,
                IsRpc = evt.IsRpcEvent,
                DatabaseId = evt.DatabaseId,
                ApplicationName = evt.ApplicationName,
                LoginName = evt.LoginName
            };
        }

        private void HandleBatchCompleted(TraceEvent evt)
        {
            var key = new SessionRequestKey(evt.SessionId, evt.RequestId);

            PendingBatch pending = null;
            _pendingBatches.TryGetValue(key, out pending);
            if (pending != null)
                _pendingBatches.Remove(key);

            // BatchSeq = StartSeq if available, otherwise EndSeq (matches ReadTrace.exe:
            // Row.BatchSeq_Value = (StartSeq_Status == OK) ? StartSeq : EndSeq)
            long batchSeq = pending?.StartSeq ?? evt.Seq;

            // Use completed event's text, fall back to starting event's text
            string textData = evt.TextData ?? pending?.TextData;
            string objectName = evt.ObjectName ?? pending?.ObjectName;
            bool isRpc = evt.IsRpcEvent || (pending?.IsRpc ?? false);

            // Determine special proc and normalize
            byte specialProcId = isRpc ? SpecialProcDetector.GetSpecialProcId(objectName) : (byte)0;
            string normText = SqlTextNormalizer.Normalize(textData);
            long hashId = HashComputer.ComputeHash(normText, specialProcId);

            // Add to unique store
            _store.TryAddBatch(hashId, textData, normText, specialProcId);

            // Track procedure name
            if (isRpc && !string.IsNullOrEmpty(objectName))
            {
                _store.AddProcedureName(
                    evt.DatabaseId,
                    evt.ObjectId ?? 0,
                    specialProcId,
                    objectName);
            }

            int appNameId = _store.GetOrAddAppName(evt.ApplicationName ?? pending?.ApplicationName);
            int loginNameId = _store.GetOrAddLoginName(evt.LoginName ?? pending?.LoginName);

            // Determine ConnSeq for this batch. If the session has no connection event,
            // create a "connected before trace" placeholder (matches ReadTrace.exe behavior).
            long connSeq = EnsureConnectionForSession(evt);

            var row = new BatchRow
            {
                BatchSeq = batchSeq,
                HashID = hashId,
                Session = evt.SessionId,
                Request = evt.RequestId,
                ConnId = evt.ConnId,
                StartTime = pending?.StartTime ?? evt.StartTime,
                EndTime = evt.EndTime ?? evt.StartTime,
                Duration = evt.Duration,
                Reads = evt.Reads,
                Writes = evt.Writes,
                CPU = evt.CPU,
                fRPCEvent = (byte)(isRpc ? 1 : 0),
                DBID = evt.DatabaseId,
                StartSeq = pending?.StartSeq,
                EndSeq = evt.Seq,
                AttnSeq = null,
                ConnSeq = connSeq,
                TextData = textData,
                OrigRowCount = evt.RowCount,
                AppNameID = appNameId,
                LoginNameID = loginNameId
            };

            Batches.Add(row);

            // Clear the in-flight batch sequence now that the batch has completed
            // (matches ReadTrace: pStateInfo->CurBatchStartSeq = 0)
            _curBatchStartSeq.Remove(key);
        }

        #endregion

        #region Statement Handling

        private void HandleStatementStarting(TraceEvent evt)
        {
            var key = new SessionRequestKey(evt.SessionId, evt.RequestId);
            if (!_pendingStatements.ContainsKey(key))
                _pendingStatements[key] = new Stack<PendingStatement>();

            _pendingStatements[key].Push(new PendingStatement
            {
                StartSeq = evt.Seq,
                StartTime = evt.StartTime,
                TextData = evt.TextData,
                ObjectId = evt.ObjectId,
                NestLevel = evt.NestLevel
            });
        }

        private void HandleStatementCompleted(TraceEvent evt)
        {
            var key = new SessionRequestKey(evt.SessionId, evt.RequestId);

            PendingStatement pending = null;
            if (_pendingStatements.TryGetValue(key, out var stack) && stack.Count > 0)
                pending = stack.Pop();

            string textData = evt.TextData ?? pending?.TextData;
            string normText = SqlTextNormalizer.Normalize(textData);
            long hashId = HashComputer.ComputeHash(normText);

            _store.TryAddStatement(hashId, textData, normText);

            // StmtSeq = StartSeq if available, otherwise EndSeq (matches ReadTrace.exe:
            // Row.StmtSeq_Value = (StartSeq_Status == OK) ? StartSeq : EndSeq)
            long stmtSeq = pending?.StartSeq ?? evt.Seq;

            // Find the parent batch using the in-flight batch's starting sequence.
            // This is the key difference from the old approach: ReadTrace maintains
            // CurBatchStartSeq as live session state set when BatchStarting arrives,
            // so statements that complete *during* batch execution get linked.
            long? batchSeq = null;
            _curBatchStartSeq.TryGetValue(key, out long curBatchStart);
            if (curBatchStart > 0)
            {
                batchSeq = curBatchStart;
            }

            // Get ConnSeq for this statement's session. Unlike batches, statements
            // do NOT create fake "CONNECTED BEFORE TRACE" rows — they just reference
            // the existing ConnSeq if available (matches ReadTrace.exe: InsertStmt
            // uses CurConnectSeq directly, only InsertBatch calls InsertConnectEvent).
            long? connSeq = null;
            if (_sessionConnSeq.TryGetValue(evt.SessionId, out long existingConnSeq))
            {
                connSeq = existingConnSeq;
            }

            int appNameId = _store.GetOrAddAppName(evt.ApplicationName);
            int loginNameId = _store.GetOrAddLoginName(evt.LoginName);

            var row = new StatementRow
            {
                StmtSeq = stmtSeq,
                HashID = hashId,
                Session = evt.SessionId,
                Request = evt.RequestId,
                ConnId = evt.ConnId,
                StartTime = pending?.StartTime ?? evt.StartTime,
                EndTime = evt.EndTime ?? evt.StartTime,
                Duration = evt.Duration,
                Reads = evt.Reads,
                Writes = evt.Writes,
                CPU = evt.CPU,
                Rows = evt.RowCount,
                DBID = evt.DatabaseId,
                ObjectID = evt.ObjectId ?? pending?.ObjectId,
                NestLevel = evt.NestLevel ?? pending?.NestLevel,
                fDynamicSQL = false,
                StartSeq = pending?.StartSeq,
                EndSeq = evt.Seq,
                ConnSeq = connSeq,
                BatchSeq = batchSeq,
                ParentStmtSeq = null, // Filled in post-load fixups
                AttnSeq = null,
                TextData = textData,
                AppNameID = appNameId,
                LoginNameID = loginNameId
            };

            Statements.Add(row);
        }

        #endregion

        #region Connection Handling

        /// <summary>
        /// Ensures a connection row exists for the given event's session.
        /// If no login/existing-connection event was seen for this session,
        /// creates a "CONNECTED BEFORE TRACE" placeholder row using the event's
        /// global sequence as ConnSeq (matches ReadTrace.exe: CurConnectSeq = BatchSeq_Value).
        /// All ConnSeq values come from the global sequence space, ensuring uniqueness.
        /// Returns the ConnSeq for the session.
        /// </summary>
        private long EnsureConnectionForSession(TraceEvent evt)
        {
            if (_sessionConnSeq.TryGetValue(evt.SessionId, out long existingConnSeq))
            {
                return existingConnSeq;
            }

            // No login event seen for this session — create a placeholder.
            // ReadTrace.exe uses the batch's BatchSeq (which equals its StartSeq global
            // sequence) as ConnSeq for fake connection rows. Since we call this from
            // HandleBatchCompleted/HandleStatementCompleted, evt.Seq is the triggering
            // event's unique global sequence — guaranteed unique per event.
            long fakeConnSeq = evt.Seq;

            Connections.Add(new ConnectionRow
            {
                ConnSeq = fakeConnSeq,
                Session = evt.SessionId,
                StartTime = null,
                EndTime = null,
                Duration = null,
                Reads = null,
                Writes = null,
                CPU = null,
                ApplicationName = "CONNECTED BEFORE TRACE",
                LoginName = "CONNECTED BEFORE TRACE",
                HostName = null,
                NTDomainName = null,
                NTUserName = null,
                StartSeq = null,
                EndSeq = null,
                TextData = null
            });

            _sessionConnSeq[evt.SessionId] = fakeConnSeq;
            return fakeConnSeq;
        }

        private void HandleLogin(TraceEvent evt)
        {
            // ConnSeq = the login event's global sequence (matches ReadTrace.exe:
            // pStateInfo->CurConnectSeq = pEvent->GetGlobalSeq())
            long connSeq = evt.Seq;

            _connections[evt.SessionId] = new ConnectionInfo
            {
                ConnSeq = connSeq,
                SessionId = evt.SessionId,
                StartTime = evt.StartTime,
                StartSeq = evt.Seq,
                AppName = evt.ApplicationName,
                LoginName = evt.LoginName,
                HostName = evt.HostName,
                NTDomainName = evt.NTDomainName,
                NTUserName = evt.NTUserName,
                TextData = evt.TextData
            };

            // Track that this session now has a real connection event
            _sessionConnSeq[evt.SessionId] = connSeq;
        }

        private void HandleLogout(TraceEvent evt)
        {
            if (_connections.TryGetValue(evt.SessionId, out var conn))
            {
                Connections.Add(new ConnectionRow
                {
                    ConnSeq = conn.ConnSeq,
                    Session = conn.SessionId,
                    StartTime = conn.StartTime,
                    EndTime = evt.StartTime,
                    Duration = evt.Duration,
                    Reads = evt.Reads,
                    Writes = evt.Writes,
                    CPU = evt.CPU,
                    ApplicationName = conn.AppName,
                    LoginName = conn.LoginName,
                    HostName = conn.HostName,
                    NTDomainName = conn.NTDomainName,
                    NTUserName = conn.NTUserName,
                    StartSeq = conn.StartSeq,
                    EndSeq = evt.Seq,
                    TextData = conn.TextData
                });
                _connections.Remove(evt.SessionId);

                // Do NOT clear _sessionConnSeq here. ReadTrace.exe clears
                // CurConnectSeq = 0 on logout, but then creates only ONE fake
                // connection per session gap. Our EnsureConnectionForSession
                // already caches per-session, so keeping the last known ConnSeq
                // prevents creating duplicate fake connection rows for every
                // batch that arrives between logout and the next login.
                // The next HandleLogin will overwrite _sessionConnSeq with the
                // new login's global sequence anyway.
            }
        }

        #endregion

        #region Attention & Interesting Events

        private void HandleAttention(TraceEvent evt)
        {
            // Find the most recent batch for this session and mark it with AttnSeq
            for (int i = Batches.Count - 1; i >= 0; i--)
            {
                if (Batches[i].Session == evt.SessionId && Batches[i].AttnSeq == null)
                {
                    Batches[i].AttnSeq = evt.Seq;
                    break;
                }
            }
        }

        private void HandleInterestingEvent(TraceEvent evt)
        {
            // Find parent batch using in-flight batch sequence (matches ReadTrace)
            var key = new SessionRequestKey(evt.SessionId, evt.RequestId);
            long? batchSeq = null;
            _curBatchStartSeq.TryGetValue(key, out long curBatchStart);
            if (curBatchStart > 0)
            {
                batchSeq = curBatchStart;
            }

            InterestingEvents.Add(new InterestingEventRow
            {
                Seq = evt.Seq,
                EventID = evt.EventId,
                Session = evt.SessionId,
                Request = evt.RequestId,
                ConnId = evt.ConnId,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                Duration = evt.Duration,
                DBID = evt.DatabaseId,
                IntegerData = evt.IntegerData,
                EventSubclass = evt.EventSubclass,
                TextData = evt.TextData?.Length > 1000
                    ? evt.TextData.Substring(0, 1000)
                    : evt.TextData,
                ObjectID = evt.ObjectId,
                Error = evt.Error,
                BatchSeq = batchSeq,
                Severity = evt.Severity,
                State = evt.State
            });
        }

        #endregion
    }

    #region Row Types

    public class BatchRow
    {
        public long BatchSeq;
        public long HashID;
        public int Session;
        public int Request;
        public long ConnId;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public long? Duration;
        public long? Reads;
        public long? Writes;
        public long? CPU;
        public byte fRPCEvent;
        public int DBID;
        public long? StartSeq;
        public long? EndSeq;
        public long? AttnSeq;
        public long? ConnSeq;
        public string TextData;
        public long? OrigRowCount;
        // For aggregation — not stored in table
        public int AppNameID;
        public int LoginNameID;
    }

    public class StatementRow
    {
        public long StmtSeq;
        public long HashID;
        public int Session;
        public int Request;
        public long ConnId;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public long? Duration;
        public long? Reads;
        public long? Writes;
        public long? CPU;
        public long? Rows;
        public int DBID;
        public int? ObjectID;
        public int? NestLevel;
        public bool fDynamicSQL;
        public long? StartSeq;
        public long? EndSeq;
        public long? ConnSeq;
        public long? BatchSeq;
        public long? ParentStmtSeq;
        public long? AttnSeq;
        public string TextData;
        // For aggregation — not stored in table
        public int AppNameID;
        public int LoginNameID;
    }

    public class ConnectionRow
    {
        public long ConnSeq;
        public int Session;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public long? Duration;
        public long? Reads;
        public long? Writes;
        public long? CPU;
        public string ApplicationName;
        public string LoginName;
        public string HostName;
        public string NTDomainName;
        public string NTUserName;
        public long? StartSeq;
        public long? EndSeq;
        public string TextData;
    }

    public class InterestingEventRow
    {
        public long Seq;
        public int EventID;
        public int Session;
        public int Request;
        public long ConnId;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public long? Duration;
        public int DBID;
        public int? IntegerData;
        public int? EventSubclass;
        public string TextData;
        public int? ObjectID;
        public int? Error;
        public long? BatchSeq;
        public int? Severity;
        public int? State;
    }

    internal struct SessionRequestKey : IEquatable<SessionRequestKey>
    {
        public readonly int Session;
        public readonly int Request;

        public SessionRequestKey(int session, int request)
        {
            Session = session;
            Request = request;
        }

        public bool Equals(SessionRequestKey other) => Session == other.Session && Request == other.Request;
        public override bool Equals(object obj) => obj is SessionRequestKey k && Equals(k);
        public override int GetHashCode() => (Session * 397) ^ Request;
    }

    internal class PendingBatch
    {
        public long StartSeq;
        public DateTime? StartTime;
        public string TextData;
        public string ObjectName;
        public bool IsRpc;
        public int DatabaseId;
        public string ApplicationName;
        public string LoginName;
    }

    internal class PendingStatement
    {
        public long StartSeq;
        public DateTime? StartTime;
        public string TextData;
        public int? ObjectId;
        public int? NestLevel;
    }

    internal class ConnectionInfo
    {
        public long ConnSeq;
        public int SessionId;
        public DateTime? StartTime;
        public long StartSeq;
        public string AppName;
        public string LoginName;
        public string HostName;
        public string NTDomainName;
        public string NTUserName;
        public string TextData;
    }

    #endregion
}
