using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NexusInterfaces;
using System.IO;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
//using System.Windows.Forms;

namespace PerfmonImporter
{
    public class DSNCreator
    {
        private enum DSNRequestTypes
        {
            ODBC_ADD_DSN = 1,			// add a user DSN
            ODBC_CONFIG_DSN = 2,		// configure a user DSN
            ODBC_REMOVE_DSN = 3,		// remove a user DSN
            ODBC_ADD_SYS_DSN = 4,		// add a system DSN
            ODBC_CONFIG_SYS_DSN = 5,	// configure a system DSN
            ODBC_REMOVE_SYS_DSN = 6		// remove a system DSN
        }
        [DllImport("BulkLoad.dll", EntryPoint = "AllocateConnectionHandle", CharSet = CharSet.Unicode)]
        public static extern uint AllocateConnectionHandle();
        [DllImport("ODBCCP32.dll", EntryPoint = "SQLConfigDataSource", CharSet = CharSet.Unicode)]
        private static extern bool SQLConfigDataSource(IntPtr parent, int request, string driver, string attributes);
        public static bool CreateDSN(string DSNName, string Server, string Database, bool AuthMode, string User, string Password)
        {
            string DSNSettings;
            DSNSettings = "DSN=" + DSNName + "\0"
                + "Database=" + Database + "\0"
                + "Server=" + Server + "\0";
            if (AuthMode)
                DSNSettings += "Trusted_Connection=yes\0";
            else	// NOTE: I don't think SQL allows you to persist a SQL login/pwd in a DSN...
                DSNSettings += "Trusted_Connection=no\0;UID=" + User + "\0" + "PWD=" + Password + "\0";

            //return SQLConfigDataSource((IntPtr)0, (int)DSNRequestTypes.ODBC_ADD_SYS_DSN,                 "SQL Server",                 DSNSettings);
            //UAC makes creating system DSN a problem. changing to use USER DSN
            return SQLConfigDataSource((IntPtr)0, (int)DSNRequestTypes.ODBC_ADD_DSN, "SQL Server", DSNSettings);
        }
    }
    //[NexusInterfaces.OffByDefault]
    public class BLGImporter : INexusImporter
    {
        const string OPTION_DROP_EXISTING = "Drop existing tables";
        const string OPTION_ENABLED = "Enabled";
        private const string POST_LOAD_SQL_SCRIPT = null; //"PerfStatsAnalysis_doNOTRun.sql";

        public BLGImporter()
        {
            options.Add(OPTION_DROP_EXISTING, true);
            options.Add(OPTION_ENABLED, true);
        }

        #region INexusImporter Members

        ILogger logger;
        string filemask;
        string connStr;
        string server;
        bool usewindowsauth;
        string sqllogin;
        string sqlpassword;
        string databasename;

        public void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            logger = Logger;
            filemask = Filemask;
            connStr = connString;
            server = Server;
            usewindowsauth = UseWindowsAuth;
            sqllogin = SQLLogin;
            sqlpassword = SQLPassword;
            databasename = DatabaseName;

            // Init status members
            state = ImportState.Idle;
            canceled = false;
            knownRowsets = new ArrayList();
            totalRowsInserted = 0;
            totalLinesProcessed = 0;				

        }

        public string Name
        {
            get { return "BLG Blaster (Perfmon/Sysmon BLG files)"; }
        }

        public Guid ID
        {
            get
            {
                return new Guid("F093D945-B6D0-4945-ABA9-FB170A799165");
            }
        }

        public string[] SupportedMasks
        {
            get
            {
                return new String[] { "*.BLG" };
            }
        }

        public string[] PreScripts
        {
            get
            {
                return new string[0];
            }
        }

        public string[] PostScripts
        {
            get
            {
                return new string[] { POST_LOAD_SQL_SCRIPT };
            }
        }

        private ImportState state = ImportState.Idle;	// Host can check this to see current state
        public ImportState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                OnStatusChanged(new EventArgs());
            }
        }

        bool canceled = false;
        public bool Canceled
        {
            get 
            { 
                return canceled; 
            }
            set
            {
                canceled = value;
            }
        }

        private ArrayList knownRowsets = new ArrayList();	// List of the rowsets we know how to interpret
        public System.Collections.ArrayList KnownRowsets
        {
            get 
            { 
                return knownRowsets;
            }
        }

        long totalRowsInserted = 0;
        public long TotalRowsInserted
        {
            get 
            { 
                return totalRowsInserted; 
            }
        }

        long totalLinesProcessed = 0;
        public long TotalLinesProcessed
        {
            get 
            { 
                return totalLinesProcessed;
            }
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

        private long TotalRows()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                cn.Open();
                SqlCommand sqlcmd = new SqlCommand();
                sqlcmd.Connection = cn;
                sqlcmd.CommandTimeout = 0;
                sqlcmd.CommandText = "select isnull (sum(rowcnt), 0) as TotalRows from sysindexes where id in (object_id('CounterData'), object_id('CounterDetails'), object_id('DisplayToID'))";
                return (long)sqlcmd.ExecuteScalar();
            }
        }

        public bool DoImport()
        {
            string[] Files = Directory.GetFiles(Path.GetDirectoryName(filemask), Path.GetFileName(filemask));
            if (0 == Files.Length)
            {
                State = ImportState.NoFiles;
                return false;
            }

            string TempDir = Path.GetTempPath();
            State = ImportState.Importing;

            if ((bool)Options[OPTION_DROP_EXISTING])
            {
                DropExistingTables();
            }

            int filenum = 1;
            foreach (string f in Files)
            {
                string args;

                filenum++;
                logger.LogMessage("Loading " + Path.GetFileName(f));

               // string _Output;
                /*
                 * Commented this block out now that PSSDIAG no longer collects all Thread 
                 * counters by default. 
                 * 
                //TODO: Expose the ability in the UI to select which ctr objects get loaded (to speed up the load). Could use a technique similar to the one below. 

                // Export counter list from the BLG to a temp file
                //		relog.exe "C:\DATA\PSSDIAG.BLG" -q > "%TEMP%\counterlist_full.txt"
                args = "\"" + f + "\" -q > \"" + TempDir + "\"\\counterlist_full.txt\"";
                m_Output.Add ("relog.exe " + args);
                Shell.ShellExecute("relog.exe", args, out _Output);
                m_Output.Add ("Output: " + _Output);

                // Save off the sqlservr Process counters
                //		findstr -C:"Process(sqlservr" -C:"Pages Input/sec" "%TEMP%\counterlist_full.txt" > "%TEMP%\counterlist_SQL.txt"
                args = "-C:\"Process(sqlservr\" -C:\"Pages Input/sec\" \"" + TempDir + "\\counterlist_full.txt\" > \"" + TempDir + "\\counterlist_SQL.txt\"";
                m_Output.Add ("findstr " + args);
                Shell.ShellExecute("findstr", args, out _Output);
                m_Output.Add ("Output: " + _Output);
                // Get rid of all Process and Thread counters (and the extra crap that relog puts in the file)
                //		findstr -V -C:"File(s):" -C:"--------------" -C:"C:\DATA\PSSDIAG.BLG" -C:Input -C:"Begin:  " -C:"End:   " -C:"Samples: " -C:"The command completed" -C:"Process(" -C:"Thread(" "%TEMP%\counterlist_full.txt" > "%TEMP%\counterlist_small.txt"
                args = "-V -C:\"File(s):\" -C:\"--------------\" -C:\"" + f + "\" -C:Input -C:\"Begin:  \" -C:\"End:   \" -C:\"Samples: \" -C:\"The command completed\" -C:\"Process(\" -C:\"Thread(\" \"" + TempDir + "\\counterlist_full.txt\" > \"" + TempDir + "\\counterlist_small.txt\"";
                m_Output.Add ("findstr " + args);
                Shell.ShellExecute("findstr", args, out _Output);
                m_Output.Add ("Output: " + _Output);
                // Add the sqlservr process counters back in
                //		cmd.exe /C type "%TEMP%\counterlist_SQL.txt" >> "%TEMP%\counterlist_small.txt"
                args = "/C type \"" + TempDir + "\\counterlist_SQL.txt\" >> \"" + TempDir + "\\counterlist_small.txt\"";
                m_Output.Add ("cmd.exe " + args);
                Shell.ShellExecute("cmd.exe", args, out _Output);
                m_Output.Add ("Output: " + _Output);
                */

                // Create a system DSN pointing at the SQL Server. (Relog.exe requires a DSN.)
                bool DSNCreate = DSNCreator.CreateDSN("Nexus", server, databasename, usewindowsauth, sqllogin, sqlpassword);

                // Finally, kick off relog to load the BLG into the database. To improve 
                // loading perf we have excluded Thread and Process counters (except for 
                // Process(sqlservr)) in the above steps to get a >90% reduction in the 
                // number of counters.  This reduced counter list is passed to relog as 
                // %TEMP%\counterlist_small.txt (UPDATE: pssdiag no longer collects these by default
                // so the -cf param is commented out).  The "-t 2" command line parameter tells 
                // relog to skip every other sample point.  With the default 5 sec sampling 
                // this will load a data point for every 10 second interval. The load will 
                // usually finish in about 1.5 minutes per 256MB .BLG (~200K rows loaded). 

                //if (File.Exists(TempDir + "\\counterlist_small.txt"))
                //{
                args = "\"" + f + "\" -o SQL:Nexus!" + databasename + " -f SQL -t 2 "; // + " -cf \"" + TempDir + "\\counterlist_small.txt\"";

                ProcessStartInfo pi = new ProcessStartInfo("relog.exe", args);
//                pi.CreateNoWindow = true;
//                pi.WindowStyle = ProcessWindowStyle.Hidden;
                Util.Logger.LogMessage("relog.exe args " + args);
                Process p = Process.Start(pi);
                p.WaitForExit();

//                m_Output.Add("Errorlevel: " + ErrorLevel.ToString() + ", Output: " + _Output);

                //if (0 != ErrorLevel)
                //    throw new Exception("Error: Relog.exe failed to import BLG.");

                //}
                //else
                //{
                //	throw new Exception ("Failed to generate reduced counter list.");
                //}
                if (canceled)
                {
                    break;
                }
                totalLinesProcessed = TotalRows();
                totalRowsInserted = TotalLinesProcessed;
            }
            State = ImportState.Idle;
            return true;
        }

        public void Cancel()
        {
            canceled = true; 
        }

        public event EventHandler StatusChanged;

        public virtual void OnStatusChanged(EventArgs e)
        {
            if (null != StatusChanged)
            {
                StatusChanged(this, e);
            }
        }

        Dictionary<string, object> options = new Dictionary<string, object>();

        public Dictionary<string, object> Options
        {
            get 
            {
                return options;
            }
        }

        public System.Windows.Forms.Form OptionsDialog
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion

    }
}
