using System;
using System.Collections.Generic;
using System.Data;
using BulkLoadEx;
using TraceEventImporter.Processing;

namespace TraceEventImporter.Database
{
    /// <summary>
    /// Writes processed trace data to SQL Server using BulkLoadRowset from BulkLoadEx.
    /// Tables must already exist (created via CreateSchema.sql) before calling these methods.
    /// </summary>
    public class BulkWriter : IDisposable
    {
        private readonly string _connStr;
        private bool _disposed;

        public long TotalRowsInserted { get; private set; }

        public BulkWriter(string connectionString)
        {
            _connStr = connectionString;
        }

        public void WriteMiscInfo(string attribute, string value)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblMiscInfo", _connStr);
            try
            {
                DataRow row = bl.GetNewRow();
                row["Attribute"] = attribute;
                row["Value"] = (object)value ?? DBNull.Value;
                bl.InsertRow(row);
                TotalRowsInserted++;
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteTraceFile(long firstSeq, long lastSeq, DateTime? firstTime, DateTime? lastTime, long eventsRead, string fileName)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblTraceFiles", _connStr);
            try
            {
                DataRow row = bl.GetNewRow();
                row["FirstSeqNumber"] = firstSeq;
                row["LastSeqNumber"] = lastSeq;
                row["FirstEventTime"] = (object)firstTime ?? DBNull.Value;
                row["LastEventTime"] = (object)lastTime ?? DBNull.Value;
                row["EventsRead"] = eventsRead;
                row["TraceFileName"] = fileName;
                bl.InsertRow(row);
                TotalRowsInserted++;
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteTracedEvents(IEnumerable<int> eventIds)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblTracedEvents", _connStr);
            try
            {
                foreach (int id in eventIds)
                {
                    DataRow row = bl.GetNewRow();
                    row["EventID"] = (short)id;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteUniqueAppNames(IEnumerable<KeyValuePair<string, int>> appNames)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblUniqueAppNames", _connStr);
            try
            {
                foreach (var kvp in appNames)
                {
                    DataRow row = bl.GetNewRow();
                    // iID is identity — don't set it; SQL Server generates it
                    row["AppName"] = kvp.Key ?? "";
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteUniqueLoginNames(IEnumerable<KeyValuePair<string, int>> loginNames)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblUniqueLoginNames", _connStr);
            try
            {
                foreach (var kvp in loginNames)
                {
                    DataRow row = bl.GetNewRow();
                    row["LoginName"] = kvp.Key ?? "";
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteProcedureNames(IEnumerable<ProcedureInfo> procs)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblProcedureNames", _connStr);
            try
            {
                foreach (var proc in procs)
                {
                    DataRow row = bl.GetNewRow();
                    row["DBID"] = proc.DBID;
                    row["ObjectID"] = proc.ObjectID;
                    row["SpecialProcID"] = proc.SpecialProcID;
                    row["Name"] = proc.Name ?? "";
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteUniqueBatches(IEnumerable<UniqueBatch> batches)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblUniqueBatches", _connStr);
            try
            {
                foreach (var b in batches)
                {
                    DataRow row = bl.GetNewRow();
                    row["Seq"] = b.Seq;
                    row["HashID"] = b.HashID;
                    row["OrigText"] = b.OrigText ?? "";
                    row["NormText"] = b.NormText ?? "";
                    row["SpecialProcID"] = b.SpecialProcID;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteUniqueStatements(IEnumerable<UniqueStatement> stmts)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblUniqueStatements", _connStr);
            try
            {
                foreach (var s in stmts)
                {
                    DataRow row = bl.GetNewRow();
                    row["Seq"] = s.Seq;
                    row["HashID"] = s.HashID;
                    row["OrigText"] = (object)s.OrigText ?? DBNull.Value;
                    row["NormText"] = (object)s.NormText ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteBatches(List<BatchRow> batches)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblBatches", _connStr);
            try
            {
                foreach (var b in batches)
                {
                    DataRow row = bl.GetNewRow();
                    row["BatchSeq"] = b.BatchSeq;
                    row["HashID"] = b.HashID;
                    row["Session"] = b.Session;
                    row["Request"] = b.Request;
                    row["ConnId"] = b.ConnId;
                    row["StartTime"] = (object)b.StartTime ?? DBNull.Value;
                    row["EndTime"] = (object)b.EndTime ?? DBNull.Value;
                    row["Duration"] = (object)b.Duration ?? DBNull.Value;
                    row["Reads"] = (object)b.Reads ?? DBNull.Value;
                    row["Writes"] = (object)b.Writes ?? DBNull.Value;
                    row["CPU"] = (object)b.CPU ?? DBNull.Value;
                    row["fRPCEvent"] = b.fRPCEvent;
                    row["DBID"] = b.DBID;
                    row["StartSeq"] = (object)b.StartSeq ?? DBNull.Value;
                    row["EndSeq"] = (object)b.EndSeq ?? DBNull.Value;
                    row["AttnSeq"] = (object)b.AttnSeq ?? DBNull.Value;
                    row["ConnSeq"] = (object)b.ConnSeq ?? DBNull.Value;
                    row["TextData"] = (object)b.TextData ?? DBNull.Value;
                    row["OrigRowCount"] = (object)b.OrigRowCount ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteStatements(List<StatementRow> stmts)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblStatements", _connStr);
            try
            {
                foreach (var s in stmts)
                {
                    DataRow row = bl.GetNewRow();
                    row["StmtSeq"] = s.StmtSeq;
                    row["HashID"] = s.HashID;
                    row["Session"] = s.Session;
                    row["Request"] = s.Request;
                    row["ConnId"] = s.ConnId;
                    row["StartTime"] = (object)s.StartTime ?? DBNull.Value;
                    row["EndTime"] = (object)s.EndTime ?? DBNull.Value;
                    row["Duration"] = (object)s.Duration ?? DBNull.Value;
                    row["Reads"] = (object)s.Reads ?? DBNull.Value;
                    row["Writes"] = (object)s.Writes ?? DBNull.Value;
                    row["CPU"] = (object)s.CPU ?? DBNull.Value;
                    row["Rows"] = (object)s.Rows ?? DBNull.Value;
                    row["DBID"] = s.DBID;
                    row["ObjectID"] = (object)s.ObjectID ?? DBNull.Value;
                    row["NestLevel"] = s.NestLevel.HasValue ? (object)(byte)s.NestLevel.Value : DBNull.Value;
                    row["fDynamicSQL"] = s.fDynamicSQL;
                    row["StartSeq"] = (object)s.StartSeq ?? DBNull.Value;
                    row["EndSeq"] = (object)s.EndSeq ?? DBNull.Value;
                    row["ConnSeq"] = (object)s.ConnSeq ?? DBNull.Value;
                    row["BatchSeq"] = (object)s.BatchSeq ?? DBNull.Value;
                    row["ParentStmtSeq"] = (object)s.ParentStmtSeq ?? DBNull.Value;
                    row["AttnSeq"] = (object)s.AttnSeq ?? DBNull.Value;
                    row["TextData"] = (object)s.TextData ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteConnections(List<ConnectionRow> conns)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblConnections", _connStr);
            try
            {
                foreach (var c in conns)
                {
                    DataRow row = bl.GetNewRow();
                    row["ConnSeq"] = c.ConnSeq;
                    row["Session"] = c.Session;
                    row["StartTime"] = (object)c.StartTime ?? DBNull.Value;
                    row["EndTime"] = (object)c.EndTime ?? DBNull.Value;
                    row["Duration"] = (object)c.Duration ?? DBNull.Value;
                    row["Reads"] = (object)c.Reads ?? DBNull.Value;
                    row["Writes"] = (object)c.Writes ?? DBNull.Value;
                    row["CPU"] = (object)c.CPU ?? DBNull.Value;
                    row["ApplicationName"] = (object)c.ApplicationName ?? DBNull.Value;
                    row["LoginName"] = (object)c.LoginName ?? DBNull.Value;
                    row["HostName"] = (object)c.HostName ?? DBNull.Value;
                    row["NTDomainName"] = (object)c.NTDomainName ?? DBNull.Value;
                    row["NTUserName"] = (object)c.NTUserName ?? DBNull.Value;
                    row["StartSeq"] = (object)c.StartSeq ?? DBNull.Value;
                    row["EndSeq"] = (object)c.EndSeq ?? DBNull.Value;
                    row["TextData"] = (object)c.TextData ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteInterestingEvents(List<InterestingEventRow> events)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblInterestingEvents", _connStr);
            try
            {
                foreach (var e in events)
                {
                    DataRow row = bl.GetNewRow();
                    row["Seq"] = e.Seq;
                    row["EventID"] = e.EventID;
                    row["Session"] = e.Session;
                    row["Request"] = e.Request;
                    row["ConnId"] = e.ConnId;
                    row["StartTime"] = (object)e.StartTime ?? DBNull.Value;
                    row["EndTime"] = (object)e.EndTime ?? DBNull.Value;
                    row["Duration"] = (object)e.Duration ?? DBNull.Value;
                    row["DBID"] = e.DBID;
                    row["IntegerData"] = (object)e.IntegerData ?? DBNull.Value;
                    row["EventSubclass"] = (object)e.EventSubclass ?? DBNull.Value;
                    row["TextData"] = (object)e.TextData ?? DBNull.Value;
                    row["ObjectID"] = (object)e.ObjectID ?? DBNull.Value;
                    row["Error"] = (object)e.Error ?? DBNull.Value;
                    row["BatchSeq"] = (object)e.BatchSeq ?? DBNull.Value;
                    row["Severity"] = (object)e.Severity ?? DBNull.Value;
                    row["State"] = (object)e.State ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteTimeIntervals(List<TimeIntervalRow> intervals)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblTimeIntervals", _connStr);
            try
            {
                foreach (var ti in intervals)
                {
                    DataRow row = bl.GetNewRow();
                    row["StartTime"] = ti.StartTime;
                    row["EndTime"] = ti.EndTime;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteBatchPartialAggs(List<BatchPartialAggRow> aggs)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblBatchPartialAggs", _connStr);
            try
            {
                foreach (var a in aggs)
                {
                    DataRow row = bl.GetNewRow();
                    row["HashID"] = a.HashID;
                    row["TimeInterval"] = a.TimeInterval;
                    row["StartingEvents"] = a.StartingEvents;
                    row["CompletedEvents"] = a.CompletedEvents;
                    row["AttentionEvents"] = a.AttentionEvents;
                    row["MinDuration"] = (object)a.MinDuration ?? DBNull.Value;
                    row["MaxDuration"] = (object)a.MaxDuration ?? DBNull.Value;
                    row["TotalDuration"] = (object)a.TotalDuration ?? DBNull.Value;
                    row["MinReads"] = (object)a.MinReads ?? DBNull.Value;
                    row["MaxReads"] = (object)a.MaxReads ?? DBNull.Value;
                    row["TotalReads"] = (object)a.TotalReads ?? DBNull.Value;
                    row["MinWrites"] = (object)a.MinWrites ?? DBNull.Value;
                    row["MaxWrites"] = (object)a.MaxWrites ?? DBNull.Value;
                    row["TotalWrites"] = (object)a.TotalWrites ?? DBNull.Value;
                    row["MinCPU"] = (object)a.MinCPU ?? DBNull.Value;
                    row["MaxCPU"] = (object)a.MaxCPU ?? DBNull.Value;
                    row["TotalCPU"] = (object)a.TotalCPU ?? DBNull.Value;
                    row["AppNameID"] = a.AppNameID;
                    row["LoginNameID"] = a.LoginNameID;
                    row["DBID"] = a.DBID;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void WriteStmtPartialAggs(List<StmtPartialAggRow> aggs)
        {
            var bl = new BulkLoadRowset("ReadTrace.tblStmtPartialAggs", _connStr);
            try
            {
                foreach (var a in aggs)
                {
                    DataRow row = bl.GetNewRow();
                    row["HashID"] = a.HashID;
                    row["TimeInterval"] = a.TimeInterval;
                    row["ObjectID"] = (object)a.ObjectID ?? DBNull.Value;
                    row["DBID"] = (object)a.DBID ?? DBNull.Value;
                    row["AppNameID"] = a.AppNameID;
                    row["LoginNameID"] = a.LoginNameID;
                    row["StartingEvents"] = a.StartingEvents;
                    row["CompletedEvents"] = a.CompletedEvents;
                    row["AttentionEvents"] = a.AttentionEvents;
                    row["MinDuration"] = (object)a.MinDuration ?? DBNull.Value;
                    row["MaxDuration"] = (object)a.MaxDuration ?? DBNull.Value;
                    row["TotalDuration"] = (object)a.TotalDuration ?? DBNull.Value;
                    row["MinReads"] = (object)a.MinReads ?? DBNull.Value;
                    row["MaxReads"] = (object)a.MaxReads ?? DBNull.Value;
                    row["TotalReads"] = (object)a.TotalReads ?? DBNull.Value;
                    row["MinWrites"] = (object)a.MinWrites ?? DBNull.Value;
                    row["MaxWrites"] = (object)a.MaxWrites ?? DBNull.Value;
                    row["TotalWrites"] = (object)a.TotalWrites ?? DBNull.Value;
                    row["MinCPU"] = (object)a.MinCPU ?? DBNull.Value;
                    row["MaxCPU"] = (object)a.MaxCPU ?? DBNull.Value;
                    row["TotalCPU"] = (object)a.TotalCPU ?? DBNull.Value;
                    bl.InsertRow(row);
                    TotalRowsInserted++;
                }
            }
            finally
            {
                bl.Close();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
