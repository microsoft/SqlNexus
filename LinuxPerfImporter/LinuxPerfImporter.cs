// TODO: update debug bin path

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using LinuxPerfImporter.Logging;
using LinuxPerfImporter.Model;
using LinuxPerfImporter.Utility;
using NexusInterfaces;
using PerfmonImporter;

namespace LinuxPerfImporter
{
    public static class LinuxPerfImortGlobals
    {
        public static Log log = new Log();
    }

    public class LinuxPerfImporter : INexusImporter, INexusProgressReporter
    {


        private const string POST_LOAD_SQL_SCRIPT = "";
        private bool HasPostScript = false;
        // Options to be exposed to Nexus host
        private const string OPTION_IMPORT_TO_SQL = "Import to SQL (Linux Perf)";
        private const string OPTION_DROP_EXISTING = "Drop existing tables (Linux Perf)";
        private const string OPTION_ENABLED = "Enabled";


        // Private members
        private ArrayList knownRowsets = new ArrayList(); // List of the rowsets we know how to interpret
        private Dictionary<string, object> options = new Dictionary<string, object>();
        private ImportState state = ImportState.Idle; // Host can check this to see current state
        public ILogger logger = null;
        public string connStr = "";
        public string sqlServer = "";
        public string sqlLogin = "";
        public string sqlPassword = "";
        public string database = "";
        public bool useWindowsAuth = true;
        public string traceFileSpec = "";
        private Process linuxPerfImporter;
        private long totalLinesProcessed = 0;
        private long totalRowsInserted = 0;
        private bool is_cancelled = false; // Will be set to true if the current import has been canceled

        /// <summary>Default ctor</summary>
        /// <remarks>Define the options that we expose to host framework, and try to find ReadTrace.exe.</remarks>
        public LinuxPerfImporter()
        {

            // Define the options we support and their default values

            options.Add(OPTION_DROP_EXISTING, true);
            options.Add(OPTION_IMPORT_TO_SQL, true);
            options.Add(OPTION_ENABLED, false);
        }

        private void LogMessage(string msg)
        {
            if (null == logger)
                System.Diagnostics.Trace.WriteLine(msg);
            else
                logger.LogMessage(msg);
        }

        #region INexusImporter Members

        /// <summary>Cancel an in-progress load</summary>
        /// <remarks>Called by host to ask in importer abort an in-progress load.  Can return before abort is complete; 
        /// the host will wait until <c>DoImport()</c> returns.</remarks>
        public void Cancel()
        {
            Cancelled = true;
            State = ImportState.Canceling;
            Util.Logger.LogMessage("LinuxPerfImporter - Received cancel request");
            try
            {
                if (null != linuxPerfImporter)
                {
                    linuxPerfImporter.CloseMainWindow();
                    linuxPerfImporter.Close();
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
            get { return is_cancelled; }
            set { is_cancelled = value; }
        }

        /// <summary>Start import</summary>
        /// <remarks><c>Initialize()</c> will be called prior to <c>DoImport()</c></remarks>
        /// <returns>true if import succeeds, false otherwise</returns>
        public bool DoImport()
        {
            Directory.SetCurrentDirectory(ConfigValues.WorkingDirectory);

            // This is where we log information about the linux performance importer. It will be placed in the directory with the pssdiag files.
            LoggingConfig.LogFilePath = "pssdiaglinuximport.log";

            var pssdiagConfFile = FileUtility.GetFullFilePath(".\\pssdiag_importer.conf");

            // Check to see if pssdiag.conf exists, if not, we exit.
            if (pssdiagConfFile == "false")
            {
                LinuxPerfImortGlobals.log.WriteLog("pssdiag_importer.conf", "DOES NOT EXIST", "[Error]");
                return false;
            }
            else
            {
                LinuxPerfImortGlobals.log.WriteLog(pssdiagConfFile, "pssdiag configuration file found", "[Info]");
            }

            string TempDir = Path.GetTempPath();
            State = ImportState.Importing;

            Config config = new Config();

            List<Thread> threads = new List<Thread>();

            if (ConfigValues.ImportIoStat)
            {
                Thread thread = new Thread(new ThreadStart(ImportIo));
                thread.Name = "Import IO Stat";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }

            if (ConfigValues.ImportMemFree)
            {
                Thread thread = new Thread(new ThreadStart(ImportMemFree));
                thread.Name = "Import Memory Free";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }
            if (ConfigValues.ImportMemSwap)
            {
                Thread thread = new Thread(new ThreadStart(ImportMemSwap));
                thread.Name = "Import Memory Swap";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }
            if (ConfigValues.ImportMpStat)
            {
                Thread thread = new Thread(new ThreadStart(ImportMp));
                thread.Name = "Import MP Stat CPU";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }
            if (ConfigValues.ImportNetStats)
            {
                Thread thread = new Thread(new ThreadStart(ImportNet));
                thread.Name = "Import Network";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }
            if (ConfigValues.ImportPidStat)
            {
                Thread thread = new Thread(new ThreadStart(ImportPid));
                thread.Name = "Import PID Stat";
                thread.Start();
                Console.WriteLine("Started " + DateTime.Now + ": " + thread.Name);

                threads.Add(thread);
            }

            foreach (Thread thread in threads)
            {
                switch (thread.Name)
                {
                    case "Import IO Stat":
                        State = ImportState.ImportingIoStat;
                        break;
                    case "Import Memory Free":
                        State = ImportState.ImportingMemFree;
                        break;
                    case "Import Memory Swap":
                        State = ImportState.ImportingMemSwap;
                        break;
                    case "Import MP Stat CPU":
                        State = ImportState.ImportingMpStatCpu;
                        break;
                    case "Import Network":
                        State = ImportState.ImportingNetowrking;
                        break;
                    case "Import PID Stat":
                        State = ImportState.ImportingPidStat;
                        break;
                }

                thread.Join();
            }

            if (ConfigValues.ImportCombine)
            {
                State = ImportState.CreatingBlg;
                ImportCombine();
            }

            if ((bool)Options[OPTION_IMPORT_TO_SQL])
            {
                State = ImportState.Importing;
                ImportToSql();
            }

            State = ImportState.Idle;

            return true;
        }

        public void ImportIo()
        {
            string ioStatFileName = "*_iostat.perf";
            LinuxOutFileIoStat ioStat = new LinuxOutFileIoStat(ioStatFileName);
            new FileUtility().WriteTsvFileByLine(ioStatFileName, ioStat.Header, ioStat.Metrics);
        }

        private static void ImportMemFree()
        {
            string memFreeFileName = "*_memory_free.perf";
            LinuxOutFileMemFree memFree = new LinuxOutFileMemFree(memFreeFileName);
            new FileUtility().WriteTsvFileByLine(memFreeFileName, memFree.Header, memFree.Metrics);
        }

        private static void ImportMemSwap()
        {
            string memSwapFileName = "*_memory_swap.perf";
            LinuxOutFileMemSwap memSwap = new LinuxOutFileMemSwap(memSwapFileName);
            new FileUtility().WriteTsvFileByLine(memSwapFileName, memSwap.Header, memSwap.Metrics);
        }

        private static void ImportMp()
        {
            string mpStatFileName = "*_mpstats_cpu.perf";
            LinuxOutFileMpStat mpStat = new LinuxOutFileMpStat(mpStatFileName);
            new FileUtility().WriteTsvFileByLine(mpStatFileName, mpStat.Header, mpStat.Metrics);
        }

        private static void ImportNet()
        {
            string networkFileName = "*_network_stats.perf";
            LinuxOutFileNetwork network = new LinuxOutFileNetwork(networkFileName);
            new FileUtility().WriteTsvFileByLine(networkFileName, network.Header, network.Metrics);
        }

        private static void ImportPid()
        {
            string pidStatFileName = "*_process_pidstat.perf";
            LinuxOutFilePidStat pidStat = new LinuxOutFilePidStat(pidStatFileName);
            new FileUtility().WriteTsvFileByLine(pidStatFileName, pidStat.Header, pidStat.Metrics);
        }

        private static void ImportCombine()
        {
            ImportCombine ic = new ImportCombine();
            ic.CreateOutputDirectory();
            ic.RelogConvertToBlg();
        }

        private void ImportToSql()
        {
            BLGImporter blgImporter = new BLGImporter();
            blgImporter.Initialize(ConfigValues.WorkingDirectory + ".\\*.BLG", this.connStr, this.sqlServer, this.useWindowsAuth, this.sqlLogin, this.sqlPassword, this.database, this.logger);

            if ((bool)Options[OPTION_DROP_EXISTING])
            {
                DropExistingTables();
            }

            blgImporter.DoImport();
        }

        private bool DropExistingTables()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                cn.Open();
                SqlCommand sqlcmd = new SqlCommand();
                sqlcmd.Connection = cn;
                sqlcmd.CommandTimeout = 0;
                sqlcmd.CommandText = "IF OBJECT_ID ('CounterData') IS NOT NULL DROP TABLE CounterData "
                    + "IF OBJECT_ID ('CounterDetails') IS NOT NULL DROP TABLE CounterDetails "
                    + "IF OBJECT_ID ('DisplayToID') IS NOT NULL DROP TABLE DisplayToID";
                sqlcmd.ExecuteNonQuery();
                return true;
            }
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
            is_cancelled = false;
            knownRowsets = new ArrayList();
            this.totalLinesProcessed = 0;
            this.totalRowsInserted = 0;
            if (null == this.linuxPerfImporter)

                return;
        }

        public ArrayList KnownRowsets
        {
            get { return knownRowsets; }
            set { knownRowsets = value; }
        }

        public string Name
        {
            get { return "Import Linux Performance Files (.perf)"; }
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
            get { return new string[] { "*.perf" }; }
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
