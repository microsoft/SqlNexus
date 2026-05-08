using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using NexusInterfaces;
using TraceEventImporter.Database;
using TraceEventImporter.Models;
using TraceEventImporter.Processing;
using TraceEventImporter.Readers;

namespace TraceEventImporter
{
    /// <summary>
    /// SqlNexus importer plugin that reads .xel trace files, normalizes SQL text,
    /// computes HashIDs, and bulk-loads data into the ReadTrace schema.
    /// Coexists with the existing ReadTraceNexusImporter (which handles .trc files
    /// by shelling out to ReadTrace.exe).
    /// Discovered automatically by SqlNexus via DLL reflection.
    /// </summary>
    public class TraceEventImporterPlugin : INexusImporter, INexusProgressReporter
    {
        // Options
        private const string OPTION_ENABLED = "Enabled";
        private const string OPTION_DROP_EXISTING = "Drop existing ReadTrace tables";
        private const string OPTION_INTERVAL_SECONDS = "Aggregation interval (seconds)";
        private const string OPTION_USE_LOCAL_SERVER_TIME = "Import events using local server time (not UTC)";

        // Private state
        private ILogger _logger;
        private string _connStr;
        private string _server;
        private bool _useWindowsAuth;
        private string _sqlLogin;
        private string _sqlPassword;
        private string _database;
        private string _fileMask;
        private ImportState _state = ImportState.Idle;
        private volatile bool _cancelled;
        private long _totalRowsInserted;
        private long _totalLinesProcessed;
        private long _currentPosition;
        private readonly ArrayList _knownRowsets = new ArrayList();
        private readonly Dictionary<string, object> _options = new Dictionary<string, object>();

        public TraceEventImporterPlugin()
        {
            _options.Add(OPTION_DROP_EXISTING, true);
            _options.Add(OPTION_INTERVAL_SECONDS, 60);
            _options.Add(OPTION_ENABLED, true);
            _options.Add(OPTION_USE_LOCAL_SERVER_TIME, false);
        }

        #region INexusImporter

        public Guid ID => new Guid("B7A3C2D1-E4F5-4A6B-8C9D-0E1F2A3B4C5D");

        public string Name => "Trace Event Importer (Managed)";

        // .trc files are handled by ReadTraceNexusImporter (ReadTrace.exe).
        // This importer handles XEL files only.
        public string[] SupportedMasks => new[] { "*pssdiag*.xel", "*LogScout*.xel" };

        public Dictionary<string, object> Options => _options;

        public Form OptionsDialog => null;

        public string[] PreScripts => new string[0];

        public string[] PostScripts => new string[] { "ReadTracePostProcessing.sql" };

        public ImportState State
        {
            get => _state;
            private set
            {
                _state = value;
                OnStatusChanged(EventArgs.Empty);
            }
        }

        public bool Cancelled
        {
            get => _cancelled;
            set => _cancelled = value;
        }

        public ArrayList KnownRowsets => _knownRowsets;

        public long TotalRowsInserted => _totalRowsInserted;

        public long TotalLinesProcessed => _totalLinesProcessed;

        public void Initialize(string Filemask, string connString, string Server,
            bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            _fileMask = Filemask;
            _connStr = connString;
            _server = Server;
            _useWindowsAuth = UseWindowsAuth;
            _sqlLogin = SQLLogin;
            _sqlPassword = SQLPassword;
            _database = DatabaseName;
            _logger = Logger;

            _state = ImportState.Idle;
            _cancelled = false;
            _totalRowsInserted = 0;
            _totalLinesProcessed = 0;
        }

        public bool DoImport()
        {
            try
            {
                LogMessage("TraceEventImporter: Starting import...");
                State = ImportState.Importing;

                // 1. Find trace files
                string dir = Path.GetDirectoryName(_fileMask);
                string mask = Path.GetFileName(_fileMask);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    LogMessage("TraceEventImporter: Directory not found: " + dir);
                    State = ImportState.NoFiles;
                    return false;
                }

                string[] files = Directory.GetFiles(dir, mask).OrderBy(f => f).ToArray();

                if (files.Length == 0)
                {
                    LogMessage("TraceEventImporter: No eligible trace files found.");
                    State = ImportState.NoFiles;
                    return false;
                }

                LogMessage($"TraceEventImporter: Processing {files.Length} file(s)...");

                // 2. Deploy schema
                LogMessage("TraceEventImporter: Creating ReadTrace schema and tables...");
                DeploySchema();

                // 3. Process all files
                var store = new UniqueStore();
                var processor = new EventProcessor(store);
                int intervalSeconds = Convert.ToInt32(_options[OPTION_INTERVAL_SECONDS]);
                long globalSeq = 0;

                // When importing with local server time, shift every event timestamp by the
                // UTC-to-local offset so StartTime/EndTime are stored in local time, matching
                // the behaviour of ReadTraceNexusImporter's -B flag passed to ReadTrace.exe.
                TimeSpan localTimeOffset = TimeSpan.Zero;
                if ((bool)_options[OPTION_USE_LOCAL_SERVER_TIME])
                {
                    decimal offsetHours = GetLocalServerTimeOffset();
                    localTimeOffset = TimeSpan.FromHours((double)offsetHours);
                    LogMessage($"TraceEventImporter: Local server time offset = {offsetHours} hours; timestamps will be shifted accordingly.");
                }

                using (var writer = new BulkWriter(_connStr))
                {
                    foreach (string file in files)
                    {
                        if (_cancelled) break;

                        LogMessage($"TraceEventImporter: Reading {Path.GetFileName(file)}...");

                        ITraceEventReader reader = new XelFileReader(globalSeq);

                        long fileFirstSeq = long.MaxValue;
                        long fileLastSeq = 0;
                        DateTime? fileFirstTime = null;
                        DateTime? fileLastTime = null;
                        long fileEventsRead = 0;

                        foreach (TraceEvent evt in reader.ReadEvents(file))
                        {
                            if (_cancelled) break;

                            if (localTimeOffset != TimeSpan.Zero)
                            {
                                if (evt.StartTime.HasValue)
                                    evt.StartTime = evt.StartTime.Value.Add(localTimeOffset);
                                if (evt.EndTime.HasValue)
                                    evt.EndTime = evt.EndTime.Value.Add(localTimeOffset);
                            }

                            processor.ProcessEvent(evt);
                            _totalLinesProcessed++;
                            fileEventsRead++;

                            if (evt.Seq < fileFirstSeq) fileFirstSeq = evt.Seq;
                            if (evt.Seq > fileLastSeq) fileLastSeq = evt.Seq;
                            if (evt.Seq > globalSeq) globalSeq = evt.Seq;

                            if (evt.StartTime.HasValue)
                            {
                                if (!fileFirstTime.HasValue || evt.StartTime.Value < fileFirstTime.Value)
                                    fileFirstTime = evt.StartTime;
                                if (!fileLastTime.HasValue || evt.StartTime.Value > fileLastTime.Value)
                                    fileLastTime = evt.StartTime;
                            }

                            if (fileEventsRead % 10000 == 0)
                            {
                                _currentPosition = fileEventsRead;
                                OnProgressChanged(EventArgs.Empty);
                            }
                        }

                        if (fileEventsRead > 0)
                        {
                            writer.WriteTraceFile(fileFirstSeq, fileLastSeq, fileFirstTime, fileLastTime, fileEventsRead, Path.GetFileName(file));
                        }

                        LogMessage($"TraceEventImporter: {Path.GetFileName(file)} - {fileEventsRead} events read.");
                    }

                    if (_cancelled)
                    {
                        LogMessage("TraceEventImporter: Import cancelled.");
                        State = ImportState.Idle;
                        return false;
                    }

                    // 4. Finalize processor (flush pending connections)
                    processor.Finalize();

                    // 5. Write all data
                    LogMessage("TraceEventImporter: Writing data to database...");

                    writer.WriteMiscInfo("SchemaVersion", "3.0");
                    writer.WriteMiscInfo("LoadDateTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteMiscInfo("ImporterVersion", "TraceEventImporter 1.0");

                    var tracedEvents = store.GetTracedEventIds();
                    writer.WriteTracedEvents(tracedEvents);
                    var uniqueAppNames = store.GetUniqueAppNames();
                    writer.WriteUniqueAppNames(uniqueAppNames);
                    var uniqueLoginNames = store.GetUniqueLoginNames();
                    writer.WriteUniqueLoginNames(uniqueLoginNames);
                    var procedureNames = store.GetProcedureNames();
                    writer.WriteProcedureNames(procedureNames);

                    var uniqueBatches = store.GetUniqueBatches();
                    writer.WriteUniqueBatches(uniqueBatches);
                    var uniqueStatements = store.GetUniqueStatements();
                    writer.WriteUniqueStatements(uniqueStatements);

                    writer.WriteConnections(processor.Connections);
                    writer.WriteBatches(processor.Batches);
                    writer.WriteStatements(processor.Statements);
                    writer.WriteInterestingEvents(processor.InterestingEvents);

                    // 6. Aggregation
                    LogMessage("TraceEventImporter: Computing aggregations...");
                    var aggregator = new Aggregator(intervalSeconds);
                    aggregator.Compute(processor.Batches, processor.Statements);

                    writer.WriteTimeIntervals(aggregator.TimeIntervals);
                    writer.WriteBatchPartialAggs(aggregator.BatchAggs);
                    writer.WriteStmtPartialAggs(aggregator.StmtAggs);

                    _totalRowsInserted = writer.TotalRowsInserted;

                    LogMessage("TraceEventImporter: --- Row counts per table ---");
                    LogMessage($"TraceEventImporter:   tblTracedEvents:      {tracedEvents.Count()}");
                    LogMessage($"TraceEventImporter:   tblUniqueAppNames:    {uniqueAppNames.Count()}");
                    LogMessage($"TraceEventImporter:   tblUniqueLoginNames:  {uniqueLoginNames.Count()}");
                    LogMessage($"TraceEventImporter:   tblProcedureNames:    {procedureNames.Count()}");
                    LogMessage($"TraceEventImporter:   tblUniqueBatches:     {uniqueBatches.Count()}");
                    LogMessage($"TraceEventImporter:   tblUniqueStatements:  {uniqueStatements.Count()}");
                    LogMessage($"TraceEventImporter:   tblConnections:       {processor.Connections.Count}");
                    LogMessage($"TraceEventImporter:   tblBatches:           {processor.Batches.Count}");
                    LogMessage($"TraceEventImporter:   tblStatements:        {processor.Statements.Count}");
                    LogMessage($"TraceEventImporter:   tblInterestingEvents: {processor.InterestingEvents.Count}");
                    LogMessage($"TraceEventImporter:   tblTimeIntervals:     {aggregator.TimeIntervals.Count}");
                    LogMessage($"TraceEventImporter:   tblBatchPartialAggs:  {aggregator.BatchAggs.Count}");
                    LogMessage($"TraceEventImporter:   tblStmtPartialAggs:   {aggregator.StmtAggs.Count}");
                    LogMessage($"TraceEventImporter:   Total rows inserted:  {_totalRowsInserted}");
                }

                // 7. Post-load fixups
                LogMessage("TraceEventImporter: Running post-load fixups...");
                RunPostLoadFixups();

                LogMessage($"TraceEventImporter: Import complete. {_totalRowsInserted} total rows inserted.");

                // Always write the flag ('1' = timestamps already in local time, '0' = timestamps
                // are UTC). SQLNexus_PostProcessing.sql reads this value to choose between a direct
                // copy and a DATEADD offset conversion when populating StartTime_local/EndTime_local.
                WriteLocalTimeFlag((bool)_options[OPTION_USE_LOCAL_SERVER_TIME]);

                State = ImportState.Idle;
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"TraceEventImporter: Error - {ex.Message}");
                LogMessage(ex.ToString());
                State = ImportState.Idle;
                return false;
            }
        }

        public void Cancel()
        {
            _cancelled = true;
            State = ImportState.Canceling;
            LogMessage("TraceEventImporter: Cancel requested.");
        }

        public event EventHandler StatusChanged;

        public void OnStatusChanged(EventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        #endregion

        #region INexusProgressReporter

        public long CurrentPosition => _currentPosition;

        public event EventHandler ProgressChanged;

        public void OnProgressChanged(EventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        #endregion

        #region Private Helpers

        private void DeploySchema()
        {
            string sql = LoadEmbeddedResource("TraceEventImporter.Schema.CreateSchema.sql");
            LogMessage($"TraceEventImporter: Schema script loaded ({sql.Length} chars).");
            ExecuteSqlScript(sql);
            LogMessage("TraceEventImporter: Schema deployment complete.");
        }

        private void RunPostLoadFixups()
        {
            string sql = LoadEmbeddedResource("TraceEventImporter.Schema.PostLoadFixups.sql");
            ExecuteSqlScript(sql);
        }

        private const string LOCAL_SRV_TIME_QUERY =
            "SELECT ISNULL(CONVERT(decimal, PropertyValue), 0) UtcToLocalOffset " +
            "FROM tbl_ServerProperties " +
            "WHERE PropertyName = 'UTCOffset_in_Hours'";

        /// <summary>
        /// Reads the UTC-to-local offset (in hours) from the database.
        /// Returns 0 if the value cannot be determined.
        /// </summary>
        private decimal GetLocalServerTimeOffset()
        {
            // Primary: tbl_ServerProperties
            try
            {
                using (var cn = new SqlConnection(_connStr))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(LOCAL_SRV_TIME_QUERY, cn))
                    {
                        cmd.CommandTimeout = 0;
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            decimal offset = Convert.ToDecimal(result);
                            LogMessage("TraceEventImporter: UTC_Offset from tbl_ServerProperties: " + offset);
                            return offset;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage("TraceEventImporter: Could not read UTC offset from tbl_ServerProperties: " + e.Message);
            }

            // Fallback: tbl_server_times (Log Scout captures without PSSDIAG)
            try
            {
                using (var cn = new SqlConnection(_connStr))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 ISNULL(time_delta_hours * -1, 0) FROM dbo.tbl_server_times", cn))
                    {
                        cmd.CommandTimeout = 0;
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            decimal offset = Convert.ToDecimal(result);
                            LogMessage("TraceEventImporter: UTC_Offset from tbl_server_times: " + offset);
                            return offset;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage("TraceEventImporter: Could not read UTC offset from tbl_server_times: " + e.Message);
            }

            LogMessage("TraceEventImporter: UTC offset not found; defaulting to 0.");
            return 0;
        }

        /// <summary>
        /// Writes a flag to <c>ReadTrace.tblMiscInfo</c> (and to <c>tbl_ServerProperties</c> /
        /// <c>tbl_server_times</c> when present) that records whether timestamps in the ReadTrace
        /// tables are already in local server time (<paramref name="isLocalTime"/> = true, value
        /// '1') or in UTC (false, value '0').
        /// <c>SQLNexus_PostProcessing.sql</c> reads this flag to choose between a direct copy
        /// and a DATEADD offset conversion when populating
        /// <c>StartTime_local</c> / <c>EndTime_local</c>.
        /// </summary>
        private void WriteLocalTimeFlag(bool isLocalTime)
        {
            // @flagStr = '1' or '0' for VARCHAR columns (tbl_ServerProperties, tblMiscInfo).
            // @flagBit = 1 or 0 for the BIT column on tbl_server_times.
            // sp_executesql's own @val parameter is fed from the outer @flagBit so the BIT
            // update is also fully parameterised with no runtime concatenation.
            const string sql =
                // --- tbl_ServerProperties (primary) ---
                "IF OBJECT_ID('dbo.tbl_ServerProperties') IS NOT NULL " +
                "BEGIN " +
                "    IF EXISTS (SELECT 1 FROM dbo.tbl_ServerProperties WHERE PropertyName = 'ImportedTraceTimestampsInLocalTime') " +
                "        UPDATE dbo.tbl_ServerProperties SET PropertyValue = @flagStr WHERE PropertyName = 'ImportedTraceTimestampsInLocalTime'; " +
                "    ELSE " +
                "        INSERT INTO dbo.tbl_ServerProperties (PropertyName, PropertyValue) VALUES ('ImportedTraceTimestampsInLocalTime', @flagStr); " +
                "END " +
                // --- tbl_server_times (fallback) ---
                // NOTE: ALTER TABLE and UPDATE must be in different batches; sp_executesql is
                // used here so the UPDATE is compiled only after the column is already visible.
                // The outer @flagBit parameter is forwarded into sp_executesql's own @val.
                "IF OBJECT_ID('dbo.tbl_server_times') IS NOT NULL " +
                "BEGIN " +
                "    IF COL_LENGTH('dbo.tbl_server_times', 'ImportedTraceTimestampsInLocalTime') IS NULL " +
                "        ALTER TABLE dbo.tbl_server_times ADD [ImportedTraceTimestampsInLocalTime] BIT NULL; " +
                "    IF COL_LENGTH('dbo.tbl_server_times', 'ImportedTraceTimestampsInLocalTime') IS NOT NULL " +
                "        EXEC sp_executesql N'UPDATE dbo.tbl_server_times SET [ImportedTraceTimestampsInLocalTime] = @val', N'@val BIT', @val = @flagBit; " +
                "END " +
                // --- ReadTrace.tblMiscInfo (guaranteed fallback) ---
                "IF OBJECT_ID('ReadTrace.tblMiscInfo') IS NOT NULL " +
                "BEGIN " +
                "    IF EXISTS (SELECT 1 FROM ReadTrace.tblMiscInfo WHERE Attribute = 'ImportedTraceTimestampsInLocalTime') " +
                "        UPDATE ReadTrace.tblMiscInfo SET Value = @flagStr WHERE Attribute = 'ImportedTraceTimestampsInLocalTime'; " +
                "    ELSE " +
                "        INSERT INTO ReadTrace.tblMiscInfo (Attribute, Value) VALUES ('ImportedTraceTimestampsInLocalTime', @flagStr); " +
                "END";

            using (var cn = new SqlConnection(_connStr))
            {
                try
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(sql, cn))
                    {
                        cmd.CommandTimeout = 0;
                        cmd.Parameters.AddWithValue("@flagStr", isLocalTime ? "1" : "0");
                        cmd.Parameters.AddWithValue("@flagBit", isLocalTime);
                        cmd.ExecuteNonQuery();
                    }
                    LogMessage("TraceEventImporter: Wrote 'ImportedTraceTimestampsInLocalTime'=" + (isLocalTime ? "1" : "0") + " to tbl_ServerProperties, tbl_server_times, and/or ReadTrace.tblMiscInfo.");
                }
                catch (Exception e)
                {
                    LogMessage("TraceEventImporter: Could not write local time flag: " + e.Message);
                }
            }
        }

        private void ExecuteSqlScript(string script)
        {
            string[] batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            int executedCount = 0;
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();
                LogMessage($"TraceEventImporter: Executing SQL script ({batches.Length} batches) on database '{conn.Database}'...");
                foreach (string batch in batches)
                {
                    string trimmed = batch.Trim();
                    if (trimmed.Length == 0) continue;

                    using (var cmd = new SqlCommand(trimmed, conn))
                    {
                        cmd.CommandTimeout = 0;
                        cmd.ExecuteNonQuery();
                        executedCount++;
                    }
                }
                LogMessage($"TraceEventImporter: Executed {executedCount} SQL batch(es) successfully.");
            }
        }

        private static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private void LogMessage(string msg)
        {
            if (_logger != null)
                _logger.LogMessage(msg);
            else
                System.Diagnostics.Trace.WriteLine(msg);
        }

        #endregion
    }
}
