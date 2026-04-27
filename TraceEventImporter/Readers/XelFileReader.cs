using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TraceEventImporter.Models;
using Microsoft.SqlServer.XEvent.XELite;

namespace TraceEventImporter.Readers
{
    /// <summary>
    /// Reads SQL Server Extended Events (.xel) files using Microsoft.SqlServer.XEvent.XELite.
    /// Maps XE event fields and actions to the unified TraceEvent model based on
    /// the field mappings defined in xereaderlib/XEtoTrcConversions.h.
    /// </summary>
    public class XelFileReader : ITraceEventReader
    {
        private long _globalSeq;

        public XelFileReader(long startingSeq = 0)
        {
            _globalSeq = startingSeq;
        }

        public string[] SupportedExtensions => new[] { ".xel" };

        public IEnumerable<TraceEvent> ReadEvents(string filePath)
        {
            // XELite uses async callbacks; bridge to synchronous IEnumerable via BlockingCollection
            var collection = new BlockingCollection<TraceEvent>(boundedCapacity: 1000);

            Task readerTask = Task.Run(() =>
            {
                try
                {
                    var streamer = new XEFileEventStreamer(filePath);
                    streamer.ReadEventStream(
                        () => Task.CompletedTask,
                        xevent =>
                        {
                            TraceEvent evt = MapEvent(xevent);
                            if (evt != null)
                            {
                                if (evt.Seq == 0)
                                    evt.Seq = Interlocked.Increment(ref _globalSeq);
                                collection.Add(evt);
                            }
                            return Task.CompletedTask;
                        },
                        CancellationToken.None).Wait();
                }
                finally
                {
                    collection.CompleteAdding();
                }
            });

            foreach (TraceEvent evt in collection.GetConsumingEnumerable())
            {
                yield return evt;
            }

            // Propagate any exception from the reader task
            readerTask.GetAwaiter().GetResult();
        }

        private TraceEvent MapEvent(IXEvent xe)
        {
            var evt = new TraceEvent();

            // Map event name to TraceEventType
            switch (xe.Name.ToLowerInvariant())
            {
                case "sql_batch_completed":
                    evt.EventType = TraceEventType.SqlBatchCompleted;
                    evt.EventId = 12;
                    MapBatchCompleted(xe, evt);
                    break;
                case "sql_batch_starting":
                    evt.EventType = TraceEventType.SqlBatchStarting;
                    evt.EventId = 13;
                    MapBatchStarting(xe, evt);
                    break;
                case "rpc_completed":
                    evt.EventType = TraceEventType.RpcCompleted;
                    evt.EventId = 10;
                    MapRpcCompleted(xe, evt);
                    break;
                case "rpc_starting":
                    evt.EventType = TraceEventType.RpcStarting;
                    evt.EventId = 11;
                    MapRpcStarting(xe, evt);
                    break;
                case "sp_statement_completed":
                    evt.EventType = TraceEventType.SpStmtCompleted;
                    evt.EventId = 45;
                    MapSpStmtCompleted(xe, evt);
                    break;
                case "sp_statement_starting":
                    evt.EventType = TraceEventType.SpStmtStarting;
                    evt.EventId = 44;
                    MapSpStmtStarting(xe, evt);
                    break;
                case "login":
                    evt.EventType = TraceEventType.AuditLogin;
                    evt.EventId = 14;
                    break;
                case "logout":
                    evt.EventType = TraceEventType.AuditLogout;
                    evt.EventId = 15;
                    break;
                case "attention":
                    evt.EventType = TraceEventType.Attention;
                    evt.EventId = 16;
                    break;
                case "sql_statement_recompile":
                    evt.EventType = TraceEventType.StmtRecompile;
                    evt.EventId = 166;
                    break;
                default:
                    evt.EventType = TraceEventType.Unknown;
                    break;
            }

            // Common timestamp
            evt.StartTime = xe.Timestamp.UtcDateTime;

            // Common actions (global fields attached to all events)
            evt.Seq = GetActionInt64(xe, "event_sequence");
            evt.DatabaseId = (int)GetActionInt64(xe, "database_id");
            evt.SessionId = (int)GetActionInt64(xe, "session_id");
            evt.RequestId = (int)GetActionInt64(xe, "request_id");
            evt.ApplicationName = GetActionString(xe, "client_app_name");
            evt.LoginName = GetActionString(xe, "server_principal_name");
            evt.HostName = GetActionString(xe, "client_hostname");
            evt.NTUserName = GetActionString(xe, "nt_username");

            // If no text from event-specific mapping, try the sql_text action
            if (string.IsNullOrEmpty(evt.TextData))
                evt.TextData = GetActionString(xe, "sql_text");

            return evt;
        }

        private void MapBatchCompleted(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "batch_text");
            MapPerformanceFields(xe, evt);
            evt.Error = (int?)GetFieldInt64Nullable(xe, "result");
        }

        private void MapBatchStarting(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "batch_text");
        }

        private void MapRpcCompleted(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "statement");
            evt.ObjectName = GetFieldString(xe, "object_name");
            MapPerformanceFields(xe, evt);
            evt.Error = (int?)GetFieldInt64Nullable(xe, "result");
        }

        private void MapRpcStarting(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "statement");
            evt.ObjectName = GetFieldString(xe, "object_name");
        }

        private void MapSpStmtCompleted(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "statement");
            evt.ObjectId = (int?)GetFieldInt64Nullable(xe, "object_id");
            evt.ObjectName = GetFieldString(xe, "object_name");
            evt.NestLevel = (int?)GetFieldInt64Nullable(xe, "nest_level");
            evt.LineNumber = (int?)GetFieldInt64Nullable(xe, "line_number");
            evt.Offset = (int?)GetFieldInt64Nullable(xe, "offset");
            MapPerformanceFields(xe, evt);

            // source_database_id overrides the action-level database_id for statements
            long? srcDbId = GetFieldInt64Nullable(xe, "source_database_id");
            if (srcDbId.HasValue && srcDbId.Value > 0)
                evt.DatabaseId = (int)srcDbId.Value;
        }

        private void MapSpStmtStarting(IXEvent xe, TraceEvent evt)
        {
            evt.TextData = GetFieldString(xe, "statement");
            evt.ObjectId = (int?)GetFieldInt64Nullable(xe, "object_id");
            evt.ObjectName = GetFieldString(xe, "object_name");
            evt.NestLevel = (int?)GetFieldInt64Nullable(xe, "nest_level");
        }

        private void MapPerformanceFields(IXEvent xe, TraceEvent evt)
        {
            evt.CPU = GetFieldInt64Nullable(xe, "cpu_time");
            evt.Duration = GetFieldInt64Nullable(xe, "duration");
            evt.Writes = GetFieldInt64Nullable(xe, "writes");
            evt.RowCount = GetFieldInt64Nullable(xe, "row_count");

            // Reads = logical_reads + physical_reads (matching XEtoTrcConversions.h Add behavior)
            long? logicalReads = GetFieldInt64Nullable(xe, "logical_reads");
            long? physicalReads = GetFieldInt64Nullable(xe, "physical_reads");
            if (logicalReads.HasValue || physicalReads.HasValue)
                evt.Reads = (logicalReads ?? 0) + (physicalReads ?? 0);

            // EndTime = StartTime + Duration for completed events
            if (evt.StartTime.HasValue && evt.Duration.HasValue)
                evt.EndTime = evt.StartTime.Value.AddMicroseconds(evt.Duration.Value);
        }

        #region Field/Action Helpers

        private static string GetFieldString(IXEvent xe, string fieldName)
        {
            try
            {
                if (xe.Fields != null && xe.Fields.TryGetValue(fieldName, out object val) && val != null)
                    return val.ToString();
            }
            catch { }
            return null;
        }

        private static long? GetFieldInt64Nullable(IXEvent xe, string fieldName)
        {
            try
            {
                if (xe.Fields != null && xe.Fields.TryGetValue(fieldName, out object val) && val != null)
                    return Convert.ToInt64(val);
            }
            catch { }
            return null;
        }

        private static string GetActionString(IXEvent xe, string actionName)
        {
            try
            {
                if (xe.Actions != null && xe.Actions.TryGetValue(actionName, out object val) && val != null)
                    return val.ToString();
            }
            catch { }
            return null;
        }

        private static long GetActionInt64(IXEvent xe, string actionName)
        {
            try
            {
                if (xe.Actions != null && xe.Actions.TryGetValue(actionName, out object val) && val != null)
                    return Convert.ToInt64(val);
            }
            catch { }
            return 0;
        }

        #endregion
    }

    // Extension method for DateTime.AddMicroseconds (not available in .NET Framework 4.8)
    internal static class DateTimeExtensions
    {
        public static DateTime AddMicroseconds(this DateTime dt, long microseconds)
        {
            return dt.AddTicks(microseconds * 10);
        }
    }
}
