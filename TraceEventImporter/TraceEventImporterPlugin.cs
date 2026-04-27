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
    /// SqlNexus importer plugin that reads .trc and .xel trace files, normalizes SQL text,
    /// computes HashIDs, and bulk-loads data into the ReadTrace schema.
    /// Coexists with the existing ReadTraceNexusImporter (which shells out to ReadTrace.exe).
    /// Discovered automatically by SqlNexus via DLL reflection.
    /// </summary>
    public class TraceEventImporterPlugin : INexusImporter, INexusProgressReporter
    {
        // Options
        private const string OPTION_ENABLED = "Enabled";
        private const string OPTION_DROP_EXISTING = "Drop existing ReadTrace tables";
        private const string OPTION_INTERVAL_SECONDS = "Aggregation interval (seconds)";

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
        private bool _cancelled;
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
        }

        #region INexusImporter

        public Guid ID => new Guid("B7A3C2D1-E4F5-4A6B-8C9D-0E1F2A3B4C5D");

        public string Name => "Trace Event Importer (Managed)";

        public string[] SupportedMasks => new[] { "*.trc", "*pssdiag*.xel", "*LogScout*.xel" };

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

                string[] allFiles = Directory.GetFiles(dir, mask);
                string[] files = allFiles.Where(f => !SkipFile(f)).OrderBy(f => f).ToArray();

                if (files.Length == 0)
                {
                    LogMessage("TraceEventImporter: No eligible trace files found.");
                    State = ImportState.NoFiles;
                    return false;
                }

                int excluded = allFiles.Length - files.Length;
                if (excluded > 0)
                    LogMessage($"TraceEventImporter: Excluded {excluded} file(s) (log_NNN.trc / *_blk.trc).");

                LogMessage($"TraceEventImporter: Processing {files.Length} file(s)...");

                // 2. Deploy schema (always ensure tables exist; option controls whether to drop first)
                LogMessage("TraceEventImporter: Creating ReadTrace schema and tables...");
                DeploySchema();

                // 3. Process all files
                var store = new UniqueStore();
                var processor = new EventProcessor(store);
                int intervalSeconds = Convert.ToInt32(_options[OPTION_INTERVAL_SECONDS]);
                long globalSeq = 0;

                using (var writer = new BulkWriter(_connStr))
                {
                    foreach (string file in files)
                    {
                        if (_cancelled) break;

                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        LogMessage($"TraceEventImporter: Reading {Path.GetFileName(file)}...");

                        ITraceEventReader reader;
                        if (ext == ".xel")
                            reader = new XelFileReader(globalSeq);
                        else
                            reader = new TrcFileReader(globalSeq);

                        long fileFirstSeq = long.MaxValue;
                        long fileLastSeq = 0;
                        DateTime? fileFirstTime = null;
                        DateTime? fileLastTime = null;
                        long fileEventsRead = 0;

                        foreach (TraceEvent evt in reader.ReadEvents(file))
                        {
                            if (_cancelled) break;

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

                            // Report progress periodically
                            if (fileEventsRead % 10000 == 0)
                            {
                                _currentPosition = fileEventsRead;
                                OnProgressChanged(EventArgs.Empty);
                            }
                        }

                        // Record trace file info
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

                    // Metadata
                    writer.WriteMiscInfo("SchemaVersion", "3.0");
                    writer.WriteMiscInfo("LoadDateTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteMiscInfo("ImporterVersion", "TraceEventImporter 1.0");

                    // Reference data
                    var tracedEvents = store.GetTracedEventIds();
                    writer.WriteTracedEvents(tracedEvents);
                    var uniqueAppNames = store.GetUniqueAppNames();
                    writer.WriteUniqueAppNames(uniqueAppNames);
                    var uniqueLoginNames = store.GetUniqueLoginNames();
                    writer.WriteUniqueLoginNames(uniqueLoginNames);
                    var procedureNames = store.GetProcedureNames();
                    writer.WriteProcedureNames(procedureNames);

                    // Unique text
                    var uniqueBatches = store.GetUniqueBatches();
                    writer.WriteUniqueBatches(uniqueBatches);
                    var uniqueStatements = store.GetUniqueStatements();
                    writer.WriteUniqueStatements(uniqueStatements);

                    // Fact data
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

                    // Log per-table row counts
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

                // 7. Post-load fixups (indexes, ConnSeq linking, ParentStmtSeq)
                LogMessage("TraceEventImporter: Running post-load fixups...");
                RunPostLoadFixups();

                LogMessage($"TraceEventImporter: Import complete. {_totalRowsInserted} total rows inserted.");
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

        private void ExecuteSqlScript(string script)
        {
            // Split on GO statements (same approach as CSql in NexusInterfaces)
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

        private bool SkipFile(string fullFileName)
        {
            string name = Path.GetFileName(fullFileName);
            if (string.IsNullOrEmpty(name)) return false;

            // Skip blocked trace files
            if (name.EndsWith("_blk.trc", StringComparison.OrdinalIgnoreCase))
                return true;

            // Skip internal log trace files
            if (Regex.IsMatch(name, @"^log_\d+\.trc$", RegexOptions.IgnoreCase))
                return true;

            return false;
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
