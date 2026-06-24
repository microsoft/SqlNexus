using System;

namespace TraceEventImporter.Models
{
    public enum TraceEventType
    {
        Unknown = 0,
        RpcCompleted = 10,
        RpcStarting = 11,
        SqlBatchCompleted = 12,
        SqlBatchStarting = 13,
        AuditLogin = 14,
        AuditLogout = 15,
        Attention = 16,
        ExistingConnection = 17,
        DtcTransaction = 19,
        StmtStarting = 40,
        StmtCompleted = 41,
        SpStarting = 42,
        SpCompleted = 43,
        SpStmtStarting = 44,
        SpStmtCompleted = 45,
        ShowplanAll = 97,
        ShowplanStatisticsProfile = 146,
        StmtRecompile = 166,
    }

    public class TraceEvent
    {
        public long Seq { get; set; }
        public TraceEventType EventType { get; set; }
        public int EventId { get; set; }
        public int SessionId { get; set; }
        public int RequestId { get; set; }
        public long ConnId { get; set; }
        public int DatabaseId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long? Duration { get; set; }
        public long? CPU { get; set; }
        public long? Reads { get; set; }
        public long? Writes { get; set; }
        public long? RowCount { get; set; }
        public string TextData { get; set; }
        public int? ObjectId { get; set; }
        public string ObjectName { get; set; }
        public int? NestLevel { get; set; }
        public string ApplicationName { get; set; }
        public string LoginName { get; set; }
        public string HostName { get; set; }
        public string NTDomainName { get; set; }
        public string NTUserName { get; set; }
        public int? Error { get; set; }
        public int? EventSubclass { get; set; }
        public int? IntegerData { get; set; }
        public int? Severity { get; set; }
        public int? State { get; set; }
        public int? Offset { get; set; }
        public int? LineNumber { get; set; }

        public bool IsStartingEvent
        {
            get
            {
                switch (EventType)
                {
                    case TraceEventType.SqlBatchStarting:
                    case TraceEventType.RpcStarting:
                    case TraceEventType.SpStmtStarting:
                    case TraceEventType.StmtStarting:
                    case TraceEventType.SpStarting:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsCompletedEvent
        {
            get
            {
                switch (EventType)
                {
                    case TraceEventType.SqlBatchCompleted:
                    case TraceEventType.RpcCompleted:
                    case TraceEventType.SpStmtCompleted:
                    case TraceEventType.StmtCompleted:
                    case TraceEventType.SpCompleted:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsBatchEvent
        {
            get
            {
                switch (EventType)
                {
                    case TraceEventType.SqlBatchStarting:
                    case TraceEventType.SqlBatchCompleted:
                    case TraceEventType.RpcStarting:
                    case TraceEventType.RpcCompleted:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsStatementEvent
        {
            get
            {
                switch (EventType)
                {
                    case TraceEventType.SpStmtStarting:
                    case TraceEventType.SpStmtCompleted:
                    case TraceEventType.StmtStarting:
                    case TraceEventType.StmtCompleted:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool IsRpcEvent
        {
            get
            {
                return EventType == TraceEventType.RpcStarting || EventType == TraceEventType.RpcCompleted;
            }
        }
    }
}
