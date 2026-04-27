using System;
using System.Collections.Generic;

namespace TraceEventImporter.Processing
{
    /// <summary>
    /// In-memory deduplication store for unique batches, statements, app names, and login names.
    /// First occurrence stores original + normalized text; subsequent occurrences reuse the HashID.
    /// </summary>
    public class UniqueStore
    {
        private readonly Dictionary<long, UniqueBatch> _uniqueBatches = new Dictionary<long, UniqueBatch>();
        private readonly Dictionary<long, UniqueStatement> _uniqueStatements = new Dictionary<long, UniqueStatement>();
        private readonly Dictionary<string, int> _uniqueAppNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _uniqueLoginNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ProcedureInfo> _procedureNames = new Dictionary<string, ProcedureInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _tracedEventIds = new HashSet<int>();

        private long _uniqueBatchSeq;
        private long _uniqueStmtSeq;
        private int _appNameIdSeq;
        private int _loginNameIdSeq;

        #region Unique Batches

        public bool TryAddBatch(long hashId, string origText, string normText, byte specialProcId)
        {
            if (_uniqueBatches.ContainsKey(hashId))
                return false;

            _uniqueBatches[hashId] = new UniqueBatch
            {
                Seq = ++_uniqueBatchSeq,
                HashID = hashId,
                OrigText = origText ?? "",
                NormText = normText ?? "",
                SpecialProcID = specialProcId
            };
            return true;
        }

        public IEnumerable<UniqueBatch> GetUniqueBatches() => _uniqueBatches.Values;

        #endregion

        #region Unique Statements

        public bool TryAddStatement(long hashId, string origText, string normText)
        {
            if (_uniqueStatements.ContainsKey(hashId))
                return false;

            _uniqueStatements[hashId] = new UniqueStatement
            {
                Seq = ++_uniqueStmtSeq,
                HashID = hashId,
                OrigText = origText,
                NormText = normText
            };
            return true;
        }

        public IEnumerable<UniqueStatement> GetUniqueStatements() => _uniqueStatements.Values;

        #endregion

        #region App Names

        public int GetOrAddAppName(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                appName = "";

            if (_uniqueAppNames.TryGetValue(appName, out int id))
                return id;

            id = ++_appNameIdSeq;
            _uniqueAppNames[appName] = id;
            return id;
        }

        public IEnumerable<KeyValuePair<string, int>> GetUniqueAppNames() => _uniqueAppNames;

        #endregion

        #region Login Names

        public int GetOrAddLoginName(string loginName)
        {
            if (string.IsNullOrEmpty(loginName))
                loginName = "";

            if (_uniqueLoginNames.TryGetValue(loginName, out int id))
                return id;

            id = ++_loginNameIdSeq;
            _uniqueLoginNames[loginName] = id;
            return id;
        }

        public IEnumerable<KeyValuePair<string, int>> GetUniqueLoginNames() => _uniqueLoginNames;

        #endregion

        #region Procedure Names

        public void AddProcedureName(int dbid, int objectId, byte specialProcId, string name)
        {
            string key = $"{dbid}_{objectId}_{specialProcId}";
            if (!_procedureNames.ContainsKey(key))
            {
                _procedureNames[key] = new ProcedureInfo
                {
                    DBID = dbid,
                    ObjectID = objectId,
                    SpecialProcID = specialProcId,
                    Name = name
                };
            }
        }

        public IEnumerable<ProcedureInfo> GetProcedureNames() => _procedureNames.Values;

        #endregion

        #region Traced Events

        public void AddTracedEvent(int eventId)
        {
            _tracedEventIds.Add(eventId);
        }

        public IEnumerable<int> GetTracedEventIds() => _tracedEventIds;

        #endregion
    }

    public class UniqueBatch
    {
        public long Seq { get; set; }
        public long HashID { get; set; }
        public string OrigText { get; set; }
        public string NormText { get; set; }
        public byte SpecialProcID { get; set; }
    }

    public class UniqueStatement
    {
        public long Seq { get; set; }
        public long HashID { get; set; }
        public string OrigText { get; set; }
        public string NormText { get; set; }
    }

    public class ProcedureInfo
    {
        public int DBID { get; set; }
        public int ObjectID { get; set; }
        public byte SpecialProcID { get; set; }
        public string Name { get; set; }
    }
}
