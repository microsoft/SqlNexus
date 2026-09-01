using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NexusInterfaces;
using System.IO;
using System.Diagnostics;
using System.Data;
using Microsoft.Data.SqlClient;
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

        // Drivers are tried in preference order. The modern "ODBC Driver 18/17 for SQL Server"
        // drivers honor the Encrypt / TrustServerCertificate keywords; the legacy "SQL Server"
        // driver (sqlsrv32.dll) is kept only as a last-resort fallback and largely ignores them.
        public static readonly string[] PreferredDrivers = new string[]
        {
            "ODBC Driver 18 for SQL Server",
            "ODBC Driver 17 for SQL Server",
            "SQL Server"
        };

        // Builds the null-delimited attribute string passed to SQLConfigDataSource.
        // Extracted for unit testing (the P/Invoke itself cannot run in a unit test).
        public static string BuildDsnSettings(
            string DSNName, string Server, string Database, bool AuthMode, string User, string Password, bool Encrypt, bool TrustServerCertificate)
        {
            string DSNSettings;
            DSNSettings = "DSN=" + DSNName + "\0"
                + "Database=" + Database + "\0"
                + "Server=" + Server + "\0";
            if (AuthMode)
                DSNSettings += "Trusted_Connection=yes\0";
            else	// NOTE: I don't think SQL allows you to persist a SQL login/pwd in a DSN...
                DSNSettings += "Trusted_Connection=no\0;UID=" + User + "\0" + "PWD=" + Password + "\0";

            // Honor the encryption choices the user made when connecting SqlNexus to SQL Server,
            // so relog.exe negotiates the same transport security as the rest of the app.
            DSNSettings += "Encrypt=" + (Encrypt ? "yes" : "no") + "\0";
            DSNSettings += "TrustServerCertificate=" + (TrustServerCertificate ? "yes" : "no") + "\0";

            return DSNSettings;
        }

        public static bool CreateDSN(string DSNName, string Server, string Database, bool AuthMode, string User, string Password)
        {
            // Backward-compatible overload: default to no encryption (legacy behavior).
            return CreateDSN(DSNName, Server, Database, AuthMode, User, Password, false, false, null);
        }

        public static bool CreateDSN(string DSNName, string Server, string Database, bool AuthMode, string User, string Password, bool Encrypt, bool TrustServerCertificate, ILogger Logger)
        {
            string DSNSettings = BuildDsnSettings(DSNName, Server, Database, AuthMode, User, Password, Encrypt, TrustServerCertificate);

            // UAC makes creating a system DSN a problem, so we register a USER DSN.
            // Try modern drivers first (which honor Encrypt/TrustServerCertificate) and fall
            // back to older ones so the import still works on machines without them installed.
            foreach (string driver in PreferredDrivers)
            {
                // The driver is supplied as the separate lpszDriver parameter below; it must NOT
                // also be embedded in the attribute string or SQLConfigDataSource will fail.
                bool created = SQLConfigDataSource((IntPtr)0, (int)DSNRequestTypes.ODBC_ADD_DSN, driver, DSNSettings);
                if (created)
                {
                    if (null != Logger)
                    {
                        Logger.LogMessage("Perfmon import DSN '" + DSNName + "' created using ODBC driver '" + driver + "'.");
                        if (Encrypt && string.Equals(driver, "SQL Server", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogMessage("Warning: the legacy 'SQL Server' ODBC driver was used; it may not enforce the requested connection encryption.");
                        }
                    }
                    return true;
                }
            }

            if (null != Logger)
            {
                Logger.LogMessage("Failed to create Perfmon import DSN '" + DSNName + "' with any available SQL Server ODBC driver.");
            }
            return false;
        }
    }
    //[NexusInterfaces.OffByDefault]
    public class BLGImporter : INexusImporter
    {
        const string OPTION_DROP_EXISTING = "Drop existing tables (Perfmon)";
        const string OPTION_ENABLED = "Enabled";
        const string OPTION_MINIMIZE_RELOG_CMD = "Minimize Cmd window (Relog.exe) during import";

        // Name of the ODBC DSN created for relog.exe. Used both when registering the DSN and in
        // the relog "-o SQL:<DSN>!<db>" argument, so keep them in sync via this constant.
        const string DSN_NAME = "SQLNexusDSN";

        private const string POST_LOAD_SQL_SCRIPT = null; //"PerfStatsAnalysis_doNOTRun.sql";

        public BLGImporter()
        {
            options.Add(OPTION_DROP_EXISTING, true);
            options.Add(OPTION_ENABLED, true);
            options.Add(OPTION_MINIMIZE_RELOG_CMD, false);
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
            cancelled = false;
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

        bool cancelled = false;
        public bool Cancelled
        {
            get 
            { 
                return cancelled; 
            }
            set
            {
                cancelled = value;
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


                // Create a system DSN pointing at the SQL Server. (Relog.exe requires a DSN.)
                // Honor the same Encrypt / TrustServerCertificate options the user selected when
                // connecting SqlNexus to SQL Server, so the relog import is consistent with the app.
                bool encrypt = false;
                bool trustServerCertificate = false;
                try
                {
                    SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder(connStr);
                    // In Microsoft.Data.SqlClient 5.x, Encrypt is a SqlConnectionEncryptOption.
                    // Treat Mandatory or Strict as "encrypt"; Optional means no encryption.
                    encrypt = csb.Encrypt != SqlConnectionEncryptOption.Optional;
                    trustServerCertificate = csb.TrustServerCertificate;
                }
                catch (Exception ex)
                {
                    // Fall back to no encryption if the connection string cannot be parsed, but log it.
                    Util.Logger.LogMessage("Could not read encryption settings from connection string; defaulting to unencrypted DSN. " + ex.Message);
                }

                bool DSNCreate = DSNCreator.CreateDSN(DSN_NAME, server, databasename, usewindowsauth, sqllogin, sqlpassword, encrypt, trustServerCertificate, logger);
                if (!DSNCreate)
                {
                    // Fail closed: without the DSN, relog cannot write to SQL Server. Surface the
                    // failure instead of letting relog run and silently import zero rows.
                    logger.LogMessage("Failed to create ODBC DSN '" + DSN_NAME + "' for " + Path.GetFileName(f) + "; skipping relog import for this file.", MessageOptions.All);
                    State = ImportState.Idle;
                    return false;
                }

                // Finally, kick off relog to load the BLG into the database. To improve 
                // loading perf we have excluded Thread and Process counters (except for 
                // Process(sqlservr)) in the above steps to get a >90% reduction in the 
                // number of counters.  This reduced counter list is passed to relog as 
                // %TEMP%\counterlist_small.txt (UPDATE: pssdiag no longer collects these by default
                // so the -cf param is commented out).  The "-t 2" command line parameter tells 
                // relog to skip every other sample point.  With the default 5 sec sampling 
                // this will load a data point for every 10 second interval. The load will 
                // usually finish in about 1.5 minutes per 256MB .BLG (~200K rows loaded). 

                args = "\"" + f + "\" -o SQL:" + DSN_NAME + "!" + databasename + " -f SQL -t 2 "; // + " -cf \"" + TempDir + "\\counterlist_small.txt\"";

                ProcessStartInfo pi = new ProcessStartInfo("relog.exe", args);

                if ((bool)Options[OPTION_MINIMIZE_RELOG_CMD])
                {
                    pi.WindowStyle = ProcessWindowStyle.Minimized;
                }
                    
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

                if (cancelled)
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
            cancelled = true; 
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
