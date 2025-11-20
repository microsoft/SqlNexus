// TODO: update debug bin path

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Microsoft.Win32;
using NexusInterfaces;
using System.Xml;
using System.Reflection;
using System.Windows.Forms;
using System.Linq;


namespace ReadTrace
{
    public class ReadTraceNexusImporter : INexusImporter, INexusProgressReporter
    {
        private const string POST_LOAD_SQL_SCRIPT = "ReadTracePostProcessing.sql";
        private bool SkipExtractReports = false;
        private bool HasPostScript = false;
        // Options to be exposed to Nexus host
        const string OPTION_ENABLED = "Enabled";
        const string OPTION_OUTPUT_SPID_TRC = "Output trace files (.trc) by SPID to %TEMP%\\RML";
        const string OPTION_OUTPUT_RML = "Output RML files (.rml) to %TEMP%\\RML";
        const string OPTION_QUOTED_IDENTIFIERS = "Assume QUOTED_IDENTIFIER ON";
        const string OPTION_IGNORE_PSSDIAG_HOST = "Ignore events associated with PSSDIAG activity";
        const string OPTION_DISABLE_EVENT_REQUIREMENTS = "Disable event requirement checks";
        const string OPTION_ENABLE_MARS = "Enable -T35 to support MARs";
        const string OPTION_USE_LOCAL_SERVER_TIME = "Import events using local server time (not UTC)";


        //      Due to the batch flush levels for the BCP from ReadTrace these are often 
        //      going to be 0 until you exceed 1 million loaded.  The progress for ReadTrace 
        //      has been updated some to help this display 
        const string PROGRESS_QUERY = "select isnull (sum(rowcnt),0) as TotalRows from sysindexes" +
                                         " where id in (object_id('ReadTrace.tblBatches')," +
                                         "              object_id('ReadTrace.tblConnections')," +
                                         "              object_id('ReadTrace.tblStatements')," +
                                         "              object_id('ReadTrace.tblUniqueBatches')," +
                                         "              object_id('ReadTrace.tblUniqueStatements') )";

        const string LOCAL_SRV_TIME_QUERY = "SELECT CONVERT(decimal, PropertyValue) UtcToLocalOffset FROM " +
                                            "tbl_ServerProperties " +
                                            "WHERE PropertyName = 'UTCOffset_in_Hours'";


        // Private members
        private ArrayList knownRowsets = new ArrayList();	// List of the rowsets we know how to interpret
        private Dictionary<string, object> options = new Dictionary<string, object>();
        private ImportState state = ImportState.Idle;	    // Host can check this to see current state
        public ILogger logger = null;
        public string connStr = "";
        public string sqlServer = "";
        public string sqlLogin = "";
        public string sqlPassword = "";
        public string database = "";
        public bool useWindowsAuth = true;
        public string traceFileSpec = "";
        private Process processReadTrace;
        private long totalLinesProcessed = 0;
        private long totalRowsInserted = 0;
        private bool canceled = false;						// Will be set to true if the current import has been canceled
        private string readTracePath;

        /// <summary>Default ctor</summary>
        /// <remarks>Define the options that we expose to host framework, and try to find ReadTrace.exe.</remarks>
        public ReadTraceNexusImporter()
        {
            // Try to find readtrace.exe
            FindReadTraceExe();

            // Define the options we support and their default values
            options.Add(OPTION_OUTPUT_SPID_TRC, false);
            options.Add(OPTION_OUTPUT_RML, false);
            options.Add(OPTION_QUOTED_IDENTIFIERS, true);
            options.Add(OPTION_IGNORE_PSSDIAG_HOST, true);
            options.Add(OPTION_DISABLE_EVENT_REQUIREMENTS, false);
            options.Add(OPTION_ENABLE_MARS, false);
            options.Add(OPTION_USE_LOCAL_SERVER_TIME, false);


            // TODO: update enabled-by-default based on whether we found ReadTrace
            if (null == this.readTracePath)
                options.Add(OPTION_ENABLED, false);
            else
                options.Add(OPTION_ENABLED, true);
        }

        /// <summary>
        /// can we find readtrace?
        /// </summary>
        /// <returns></returns>
        private bool FindReadTraceExe()
        {

            try
            {
                readTracePath = Util.GetReadTraceExe();

                Util.Logger.LogMessage(String.Format(@"ReadTraceNexusImporter: Discovered readtrace at {0} ", readTracePath));




                if (readTracePath != null)
                {
                    //Util.Logger.LogMessage("readtrace path " + FileVersionInfo.GetVersionInfo(readTracePath).ToString(), MessageOptions.Dialog);
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(readTracePath);

                    int MajorFactor = 1000000;
                    int MinorFactor = 1000;
                    int BuildFactor = 10;
                    int RequiredVersion = 9 * MajorFactor + 3 * MinorFactor + 78 * BuildFactor;
                    int CurrentVersion = fvi.FileMajorPart * MajorFactor + fvi.FileMinorPart * MinorFactor + fvi.FileBuildPart * BuildFactor;


                    //if (!(fvi.FileMajorPart >= 9 && fvi.FileMinorPart >= 3 && fvi.FileBuildPart >= 78))
                    if (CurrentVersion < RequiredVersion)
                    {
                        Util.Logger.LogMessage("ReadTrace needs to be at least 9.3.78.  Readtrace reports may fail.  Please install latest RML utilities", MessageOptions.All);
                        Util.Logger.LogMessage("Readtrace is has a display issue, skipping extracting");


                    }

                }


            }

            catch (Exception e)
            {
                Util.Logger.LogMessage("FindReadTraceExe() exception:" + e.Message);
                //string exception_message = e.Message;
                //MessageBox.Show("There was a problem", "Title: Missing ReadTrace", MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
            }

            return (readTracePath != null);

        }



        void LogMessage(string msg)
        {
            if (null == logger)
                System.Diagnostics.Trace.WriteLine(msg);
            else
                logger.LogMessage(msg);
        }

        /// <summary>Try to determine the number of rows that have been inserted so far</summary>
        /// <remarks>Query <c>sysindexes</c> to try to determine current rowcount for core ReadTrace tables. 
        /// Note that row counts will not be available until ReadTrace flushes its bulk load rowsets, 
        /// which may be infrequently.</remarks>
        /// <returns>Approximate total number of rows inserted</returns>
        private long GetApproximateTotalRowsInserted()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                try
                {
                    cn.Open();

                    SqlCommand sqlcmd = new SqlCommand();
                    sqlcmd.Connection = cn;
                    sqlcmd.CommandTimeout = 0;
                    sqlcmd.CommandText = PROGRESS_QUERY;
                    return (long)sqlcmd.ExecuteScalar();
                }
                catch (Exception e)
                {
                    logger.LogMessage("Failed with exception" + e.Message);
                    return 0;
                }
            }
        }

        private decimal GetLocalServerTimeOffset()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                try
                {
                    decimal utc_offset;

                    cn.Open();

                    SqlCommand sqlcmd = new SqlCommand();
                    sqlcmd.Connection = cn;
                    sqlcmd.CommandTimeout = 0;
                    sqlcmd.CommandText = LOCAL_SRV_TIME_QUERY;
                    utc_offset = (decimal)sqlcmd.ExecuteScalar();
                    logger.LogMessage("UTC_Offset: " + utc_offset);
                    return utc_offset;
                }
                catch (Exception e)
                {
                    logger.LogMessage("Failed with exception" + e.Message);
                    return -99;
                }
            }
        }




        private bool SkipFile(string fullFileName)
        {
            string name = Path.GetFileName(fullFileName);
            if (name == null) 
                return false;

            if (name.EndsWith("_blk.trc", StringComparison.OrdinalIgnoreCase))
                return true;

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^log_\d+\.trc$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            return false;
        }
        /// <summary>Find the first trace file</summary>
        /// <remarks>If a trace file without a rollover number exists (e.g. "ABC_sp_trace.trc"), prefer it. 
        /// Otherwise, start with the rollover file with the lowest number (e.g. "ABC_sp_trace_14.trc" would be 
        /// used instead of "ABC_sp_trace_15.trc").</remarks>
        private string FindFirstTraceFile(string[] files)
        {
            if (this.traceFileSpec.ToUpper().Contains("XEL"))
            {
                return FileFirstXelFile(files);
            }
            string firstTrcFile = "";
            int trcFileNumber = -1;
            int firstTrcFileNumber = -1;

            Array.Sort(files);
            foreach (string f in files)
            {

                string trcFileNoExt = Path.GetFileNameWithoutExtension(f);
                if ((trcFileNoExt.LastIndexOf('_') > -1)     // find the last underscore in the filename
                    && (Int32.TryParse(trcFileNoExt.Substring((trcFileNoExt + " ").LastIndexOf('_') + 1), out trcFileNumber))) // Extract trc file #
                {
                    if ((trcFileNumber < firstTrcFileNumber) || (-1 == firstTrcFileNumber))
                    {
                        firstTrcFile = f;   // This is the earliest rollover file # we've found so far
                        firstTrcFileNumber = trcFileNumber;
                    }
                }
                else
                {
                    firstTrcFile = f;   // No rollover number -- assume this is the first trc file
                    firstTrcFileNumber = 0;
                }
            }
            return firstTrcFile;
        }
        private string FileFirstXelFile(string[] files)
        {

            string FirstFile = "";

            DateTime LastFileCreateTime = new DateTime(9999, 1, 1, 0, 0, 0);

            foreach (string file in files)
            {
                logger.LogMessage("Looking at file " + file);
                FileInfo fs = new FileInfo(file);
                DateTime CurrentFileCreateTime = fs.CreationTime;
                if (CurrentFileCreateTime < LastFileCreateTime)
                {
                    FirstFile = file;
                }
                LastFileCreateTime = CurrentFileCreateTime;

            }

            return FirstFile;
        }

        #region INexusImporter Members

        /// <summary>Cancel an in-progress load</summary>
        /// <remarks>Called by host to ask in importer abort an in-progress load.  Can return before abort is complete; 
        /// the host will wait until <c>DoImport()</c> returns.</remarks>
        public void Cancel()
        {
            Cancelled = true;
            State = ImportState.Canceling;
            Util.Logger.LogMessage("ReadTraceNexusImporter - Received cancel request");
            try
            {
                if (null != processReadTrace)
                {
                    processReadTrace.CloseMainWindow();
                    processReadTrace.Close();
                }
            }
            catch
            {
            }
            State = ImportState.Idle;
        }

        /// <summary>True if the import has been asked to cancel an in-progress load. Set by the <c>Cancel</c> method.</summary>
        public bool Cancelled
        {
            get { return canceled; }
            set { canceled = value; }
        }

        /// <summary>Start import</summary>
        /// <remarks><c>Initialize()</c> will be called prior to <c>DoImport()</c></remarks>
        /// <returns>true if import succeeds, false otherwise</returns>
        public bool DoImport()
        {
            string firstTrcFile = "";
            string timeAdjForLocalTimeMinutes = "";
            decimal UtcToLocalOffsetHours = 99;
            Util.Logger.LogMessage("ReadTraceNexusImporter - Starting import...");

            string[] allFiles = Directory.GetFiles(Path.GetDirectoryName(this.traceFileSpec), Path.GetFileName(this.traceFileSpec));
            string[] filteredFiles = allFiles.Where(f => !SkipFile(f)).ToArray();
            int excludedCount = allFiles.Length - filteredFiles.Length;

            if (excludedCount > 0)
                Util.Logger.LogMessage($"ReadTraceNexusImporter: Excluded {excludedCount} trace file(s) (log_NNN.trc / *_blk.trc).");

            if (null == this.readTracePath)
                throw new Exception("Cannot locate ReadTrace.exe. This import requires ReadTrace version 9.0.9.0 or later.");
            
            if (filteredFiles.Length == 0)
            {
                Util.Logger.LogMessage("ReadTraceNexusImporter: No eligible trace files after exclusions.");
                State = ImportState.NoFiles;
                return false;
            }

            // Use filtered array
            string[] files = filteredFiles;

            State = ImportState.Importing;

            // Find the first trace file. 
            firstTrcFile = FindFirstTraceFile(files);

            //Get local server time offset
            UtcToLocalOffsetHours = GetLocalServerTimeOffset();
            timeAdjForLocalTimeMinutes = (UtcToLocalOffsetHours * 60).ToString();

            // -T18 means to disable pop up of reporter.exe at end of load 
            // -T35 to enable mars
            string args = String.Format("-S\"{0}\" \"-d{1}\" {2} -T18 -T28 -T29 \"-I{3}\" {4} {5} {6} \"-o{7}\" {8} {9} {10} {11}",
                this.sqlServer,                                                 // -S{0}                            SQL Server name 
                this.database,                                                  // -d{1}                            Database name
                (this.useWindowsAuth ? "-E" : String.Format("-U\"{0}\" -P\"{1}\"", this.sqlLogin, this.sqlPassword)), // {2} = -E (or) -Uuser -PPassword  Credentials
                firstTrcFile,                                                   // -I{3}                            Profiler trace file
                ((bool)this.options[OPTION_OUTPUT_RML] ? "" : "-f"),            // {4} = -f                         Optional: enable RML file generation
                ((bool)this.options[OPTION_OUTPUT_SPID_TRC] ? "-M" : ""),       // {5} = -M                         Optional: output spid-specific .trc files
                ((bool)this.options[OPTION_QUOTED_IDENTIFIERS] ? "" : "-Q"),    // {6} = -Q                         Optional: assume quoted identifiers OFF
                Path.GetTempPath() + "RML",     // -o{7}  Temp output path (%TEMP%\RML)
                ((bool)this.options[OPTION_IGNORE_PSSDIAG_HOST] ? "-H\"!PSSDIAG\"" : ""),   //  {8}   Using 9.00.009 ReadTrace ignore events with HOST=PSSDIAG 
                ((bool)this.options[OPTION_DISABLE_EVENT_REQUIREMENTS] ? "-T28 -T29 " : ""),       //  {9} tell ReadTrace to override event requirement checks 
                ((bool)this.options[OPTION_ENABLE_MARS] ? "-T35" : ""),  //  {10} tell ReadTrace that there's MARS sessions 
                ((bool)this.options[OPTION_USE_LOCAL_SERVER_TIME] ? "-B" + timeAdjForLocalTimeMinutes : "") //{11} Optional: -B### Time bias: Adjusts the start and end times, as read by (+-)### minutes. 
            );

            Util.Env["RMLLogDir"] = Path.GetTempPath() + "RML";

            Util.Env.ReadTraceLogFile = Path.GetTempPath() + @"RML\readtrace.log";
            Util.Logger.LogMessage("ReadTraceNexusImporter: Loading " + firstTrcFile);
            Util.Logger.LogMessage("ReadTraceNexusImporter: Temp Path: " + Path.GetTempPath());


            //Don't log clear text password and obscure login name
            string argsOut = args;
            if (false == this.useWindowsAuth)
            {
                // Obscure sqlLogin: show first and last character, rest as '*'
                string obscuredLogin = (string.IsNullOrEmpty(this.sqlLogin) || this.sqlLogin.Length < 3)
                   ? this.sqlLogin // Show as-is if too short
                   : this.sqlLogin[0] + new string('*', this.sqlLogin.Length - 2) + this.sqlLogin[this.sqlLogin.Length - 1];


                argsOut = System.Text.RegularExpressions.Regex.Replace(
                    args,
                    @"-U""[^""]*""",
                    $@"-U""{obscuredLogin}""");

                if (!string.IsNullOrEmpty(this.sqlPassword))
                {
                    // Obscure password completely
                    string obscuredPwd = "******************";

                    argsOut = System.Text.RegularExpressions.Regex.Replace(
                    argsOut,
                    @"-P""[^""]*""",
                    $@"-P""{obscuredPwd}""");
                }
            }


            Util.Logger.LogMessage("ReadTraceNexusImporter: Cmd Line: " + this.readTracePath + " " + argsOut);

            // configure the arguments and window properties
            ProcessStartInfo pi = new ProcessStartInfo(this.readTracePath, args);
            pi.CreateNoWindow = false;
            pi.UseShellExecute = false;

            // NOTE: If we decide to redirect in the future we have to read from the pipe or when
            //       the pipe is full ReadTrace will hang attempting to write to stdout

            pi.RedirectStandardOutput = false;  // don't redirect -- we can always use the log file at %TEMP%\RML for tshooting
            pi.RedirectStandardError = false;

            //launch Readtrace.exe
            processReadTrace = Process.Start(pi);

            int i = 0;
            long rowsInserted = 0;
            while (!processReadTrace.HasExited)
            {
                i++;
                System.Threading.Thread.Sleep(100);
                // We don't have any good way to communicate with readtrace.exe and determine actual number of events processed 
                // or other progress indicators.  As a partial solution, try to retrieve approximate inserted row counts every 2 
                // seconds while we're waiting for readtrace to complete its trace processing. 
                if (0 == i % 20)
                    rowsInserted = GetApproximateTotalRowsInserted();
                this.TotalLinesProcessed = rowsInserted;
                this.TotalRowsInserted = rowsInserted;
                if (canceled)
                    break;
            }

            //LogMessage("ReadTrace stdout: " + processReadTrace.StandardOutput);
            //LogMessage("ReadTrace stderr: " + processReadTrace.StandardError);

            Util.Logger.LogMessage("ReadTrace exitcode: " + processReadTrace.ExitCode.ToString());

            State = ImportState.Idle;

            if (0 == processReadTrace.ExitCode)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Unique ID associated with this importer. 
        /// </summary>
        public Guid ID
        {
            get { return new Guid("9CE3F4E1-B8EE-4f37-91C4-BDAB1EA71F2F"); }
        }

        /// <summary>Initialize the importer</summary>
        /// <remarks>Called by host to have an importer prepare for a load and to pass in importer initialization parameters 
        /// (SQL Server name, target database, etc).</remarks>
        /// <param name="Filemask">Path to the file(s) to import. May be a filemask</param>
        /// <param name="connString">Database connection string</param>
        /// <param name="Server">SQL Server name</param>
        /// <param name="UseWindowsAuth">Whether to use an integrated/trusted SQL connection</param>
        /// <param name="SQLLogin">SQL login name (implies <c>UseWindowsAuth</c>==false)</param>
        /// <param name="SQLPassword">SQL password (implies <c>UseWindowsAuth</c>==false)</param>
        /// <param name="DatabaseName">Target database name</param>
        /// <param name="Logger"><c>ILogger</c> instance - use to communicate status/debug messages back to host</param>
        /// <returns>void</returns>
        public void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            // Persist inputs from host
            this.traceFileSpec = Filemask;
            this.connStr = connString;
            this.sqlServer = Server;
            this.sqlLogin = SQLLogin;
            this.sqlPassword = SQLPassword;
            this.database = DatabaseName;
            this.useWindowsAuth = UseWindowsAuth;
            this.logger = Logger;
            // Init status members
            state = ImportState.Idle;
            canceled = false;
            knownRowsets = new ArrayList();
            this.totalLinesProcessed = 0;
            this.totalRowsInserted = 0;
            if (null == this.readTracePath)
                FindReadTraceExe();
            Util.Logger.LogMessage(@"ReadTrace.exe Path: " + (null == this.readTracePath ? "(NOT FOUND)" : this.readTracePath));
            return;
        }

        public ArrayList KnownRowsets
        {
            get { return knownRowsets; }
            set { knownRowsets = value; }
        }

        public string Name
        {
            get { return "ReadTrace (SQL XEL/TRC Files)"; }
        }

        /// <summary>Set of true/false importer options (initialized in ctor)</summary>
        public Dictionary<string, object> Options
        {
            get { return options; }
        }

        /// <summary>Called by host to instantiate an importer-specific options dialog</summary>
        public System.Windows.Forms.Form OptionsDialog
        {   // We don't currently need a custom options dialog for this importer
            get { throw new Exception("The method or operation is not implemented."); }
        }

        /// <summary>Post-import .SQL scripts</summary>
        /// <remarks>Scripts must be present in the host .exe's directory</remarks>
        public string[] PostScripts
        {   // No scripts needed by this importer
            get
            {
                if (HasPostScript == true)
                    return new string[] { POST_LOAD_SQL_SCRIPT };
                else
                    return new string[0];

            }
        }

        /// <summary>Pre-import .SQL scripts</summary>
        /// <remarks>Scripts must be present in the host .exe's directory</remarks>
        public string[] PreScripts
        {   // No scripts needed by this importer
            get { return new string[0]; }
        }

        /// <summary>Public property used to communicate importer status back to the host</summary>
        public ImportState State
        {
            get { return state; }
            set
            {
                state = value;
                OnStatusChanged(new EventArgs());
            }
        }

        public void OnStatusChanged(EventArgs e)
        {
            if (null != StatusChanged)
                StatusChanged(this, e);
        }

        public event EventHandler StatusChanged;

        /// <summary>Filemask (e.g. "*.trc") used to advertise the set of files that a given importer knows how to process</summary>
        public string[] SupportedMasks
        {
            get { return new string[] { "*.trc", "*pssdiag*.xel", "*LogScout*.xel"}; }
        }

        /// <summary>Number of rows/lines/events processed from source file.  Used to communicate progress back to host.</summary>
        public long TotalLinesProcessed
        {
            get { return this.totalLinesProcessed; }
            set { this.totalLinesProcessed = value; }
        }

        /// <summary>Number of rows inserted into the database. Used to communicate progress back to host.</summary>
        public long TotalRowsInserted
        {
            get { return this.totalRowsInserted; }
            set
            {
                this.totalRowsInserted = value;
                OnProgressChanged(new EventArgs());
            }
        }

        #endregion

        #region INexusProgressReporter Members

        public long CurrentPosition
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public void OnProgressChanged(EventArgs e)
        {
            if (null != ProgressChanged)
                ProgressChanged(this, e);
        }

        public event EventHandler ProgressChanged;

        #endregion
    }
}
