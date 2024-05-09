#define TRACE

using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Microsoft.ReportingServices;
using System.IO;
using Microsoft.Data.SqlClient;
using System.Xml;
using System.Xml.XPath;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Win32;
using System.ServiceProcess;
//using Microsoft.Office.Interop;
using System.Globalization;
using NexusInterfaces;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading;
using System.Security;
using System.Security.Permissions;
using System.Linq;

//using Microsoft.SqlServer.Management.Smo.RegSvrEnum;

//[assembly: System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.RequestMinimum, Name = "FullTrust")]
namespace sqlnexus
{
    
    [System.Reflection.ObfuscationAttribute(Exclude = true, ApplyToMembers = false)]
    public partial class fmNexus : Form, ILogger
    {
        #region Imported APIs

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        [DllImport("hhctrl.ocx", CharSet = CharSet.Unicode, EntryPoint = "HtmlHelpW")]
        protected static extern int HtmlHelp(
           int caller,
           String file,
           uint command,
           String str
           );
        protected const int HH_DISPLAY_TOPIC    = 0x0000;
        protected const int HH_DISPLAY_TOC      = 0x0001;
        protected const int HH_DISPLAY_INDEX    = 0x0002;
        public static fmNexus singleton;

        #endregion

        #region Member vars, constants, and enums

        const string RDL_EXT = ".rdl";
        const string RDLC_EXT = ".rdlc";
        const string REG_HKCU_APP_KEY = @"HKEY_CURRENT_USER\Software\SQLNexus\SQLNexus";
        const string REG_HKCU_APP_KEY2 = @"HKEY_CURRENT_USER\Software\Wow6432Node\SQLNexus\SQLNexus";

        //DataTable - Report name pairs (used for refreshing reports)
        Dictionary<DataTable, string> reportDataTables = new Dictionary<DataTable, string>();

        public enum ReportExportType { Excel = 1, PDF, JPEG, BMP, EMF, GIF, PNG, TIFF, Clipboard };

        bool enableDataCollector = false;

        fmImport fmImportForm=null;

        #endregion Member vars, constants, and enums

        #region ILogger Members

        public TraceSource tracelogger = new TraceSource("SQLNexus", SourceLevels.All);

        public TraceSource TraceLogger
        {
            get
            {
                return tracelogger;
            }
        }

        /// <summary>
        /// Open a log file and associate it with our logger. 
        /// </summary>
        /// <remarks>Logs should be written to %TEMP% for Vista UAC compatibility.  Only one instance of sqlnexus.exe can write to 
        /// a given log file at a time using TextWriterTraceListener.  In order to support multiple instances, the first instance 
        /// will write to "sqlnexus.000.log", the second instance to "sqlnexus.001.log", etc.  Old log files will be overwritten 
        /// unless they are in use. 
        /// </remarks>
        /// <param name="logfilename">Full path to the log file. This file name will be modified with a log file number (generally 
        /// 000).</param>
        
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted=true)]
        public void InitializeLog(string logfilename)
        {
            Trace.AutoFlush = true;

            TraceListener conlistener = new ConsoleTraceListener();
            conlistener.Filter = new EventTypeFilter(SourceLevels.Information);
            TraceLogger.Listeners.Add(conlistener);

            // Allow use of env vars in log file path
            logfilename = Environment.ExpandEnvironmentVariables(logfilename);

            // First instance gets log file sqlnexus_0.log, second instance sqlnexus_1.log. 
            int maxLogFileNum = -1;
            string newLogFileName = "";
            string logFileMask; // e.g. "sqlnexus.*.log"
            logFileMask = Path.GetFileNameWithoutExtension (logfilename) + ".*" + Path.GetExtension (logfilename);

            // Get all the %TEMP%\sqlnexus.N.log files
            DirectoryInfo di = new DirectoryInfo (Path.GetDirectoryName(logfilename));
            FileInfo[] logFiles = di.GetFiles (logFileMask, SearchOption.TopDirectoryOnly);
            // Sort files according to name 
            Array.Sort(logFiles, new CompareFileInfoNames());
            foreach (FileInfo f in logFiles)
            {
                // Find the log file number (e.g. 1 for "sqlnexus.001.log")
                int logFileNum;
                Int32.TryParse (f.FullName.Split('.')[1], out logFileNum);
                // Keep track of the largest log file number we've encountered so far
                if (logFileNum > maxLogFileNum) maxLogFileNum = logFileNum;
                try
                {
                    // See if we can open the file exclusively (if so, it's safe to reuse)
                    FileStream fs = f.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                    fs.Close();
                    newLogFileName = f.FullName; 
                    break;
                }
                catch
                {   // This log file is in use -- try the next one
                    continue;
                }
            }

            // If we didn't find a reusable log file, we'll create a new one using the next available log file number
            if ("" == newLogFileName)
            {
                maxLogFileNum++;
                newLogFileName = Path.GetDirectoryName(logfilename) + @"\" + Path.GetFileNameWithoutExtension (logfilename) 
                    + "." + maxLogFileNum.ToString("000") + Path.GetExtension (logfilename);  // e.g. "sqlnexus.001.log"
            }
            if (File.Exists(newLogFileName))
                File.Delete(newLogFileName);

            // Hook up the log file as a trace listener
            FileStream fstream = new FileStream(newLogFileName,
                               FileMode.Create,
                               System.Security.AccessControl.FileSystemRights.WriteData,
                               FileShare.Read,
                               4096,
                               FileOptions.None, Util.GetFileSecurity());

            Util.Env.NexusLogFile = newLogFileName;
            TraceListener log = new System.Diagnostics.TextWriterTraceListener(fstream);
            log.TraceOutputOptions = TraceOptions.DateTime;
            log.Filter = new EventTypeFilter(SourceLevels.All);
            TraceLogger.Listeners.Add(log);
        }


        private  String GetCallingFunction()
        {
            StackTrace stack = new StackTrace();
            StackFrame frame = stack.GetFrame(3);
            String CallingFunction = String.Format("{0}.{1}", frame.GetMethod().DeclaringType.FullName, frame.GetMethod().Name);
            return CallingFunction;


        }   
 
        public void LogMessage(string msg, MessageOptions options, TraceEventType eventtype, string title)
        {
#if DEBUG
            msg = String.Format("[{0}] {1}", GetCallingFunction() , msg);

#endif
            //msg = String.Format("{0} {1}", "ver" + Globals.Runtime., msg);
            if ( (options & MessageOptions.Silent) == MessageOptions.Silent)
                TraceLogger.TraceEvent(eventtype, 0, msg);
            if ((options & MessageOptions.StatusBar) == MessageOptions.StatusBar)
            {
                ssText.Text = msg;
                Application.DoEvents();
            }
            Trace.Flush();

            if ((options & MessageOptions.Dialog) == MessageOptions.Dialog && Globals.ConsoleMode == false && String.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(msg, sqlnexus.Properties.Resources.Msg_Nexus);
            }
            else if ((options & MessageOptions.Dialog) == MessageOptions.Dialog && Globals.ConsoleMode == false)
            {
                MessageBox.Show(msg, title,MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public DialogResult LogMessage(string msg, string title, MessageBoxButtons buttons)
        {
            LogMessage(title + " " + msg, MessageOptions.Silent | MessageOptions.StatusBar, TraceEventType.Warning, String.Empty);
            return MessageBox.Show(msg, title, buttons);

        }
      
        public void LogMessage(string msg, string[] args, MessageOptions options, TraceEventType eventtype)
        {

            // We don't want to expand things that look escape sequences (callers can do this themselves 
            // with string.Format if they want to), so double-up backslashes. 
            string m = msg.Replace(@"\", @"\\");
            m = string.Format(m, args);
            LogMessage(m, options, eventtype, String.Empty);
        }

        public void LogMessage(string msg, MessageOptions options)
        {
            LogMessage(msg, options, TraceEventType.Information, String.Empty);
        }

        public void LogMessage(string msg, string[] args, MessageOptions options)
        {
            LogMessage(msg, args, options, TraceEventType.Information);
        }

        public void LogMessage(string msg, string[] args)
        {
            LogMessage(msg, args, MessageOptions.Both, TraceEventType.Information);
        }

        public void LogMessage(string msg)
        {
            LogMessage(msg, MessageOptions.Both, TraceEventType.Information, String.Empty);
        }

        public void ClearMessage()
        {
            ssText.Text = "";
            Application.DoEvents();
        }

        #endregion

        #region AutoUpdate methods
        private ClickOnce clickOnce = new ClickOnce();
        #endregion

        #region Form methods

        public fmNexus()
        {
            InitializeComponent();
            g_theme.setThemeColors(Properties.Settings.Default.Theme);
            g_theme.fRec_setControlColors(this);
            singleton = this;
        }

        // treeview hottracking is forcing color as blue , overriding its drawing to stick our own color
        private void tvReports_DrawMode(object sender, DrawTreeNodeEventArgs e)
        {

            if (e.State == TreeNodeStates.Hot)
            {
                Font font = new Font(e.Node.NodeFont ?? e.Node.TreeView.Font, FontStyle.Underline);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, e.Bounds, g_theme.ForeColor, g_theme.BackColor, TextFormatFlags.GlyphOverhangPadding);
            }
            else
            {
                Font font = e.Node.NodeFont ?? e.Node.TreeView.Font;
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, e.Bounds, g_theme.ForeColor, g_theme.BackColor, TextFormatFlags.GlyphOverhangPadding);
            }

        }

        public Cursor StartWaiting()
        {
            Cursor save = Cursor;
            Cursor = Cursors.WaitCursor;
            ssProgress.Visible = true;
            ssProgress.Style = ProgressBarStyle.Marquee;
            Application.DoEvents();
            return save;
        }

        public void StopWaiting(Cursor savecursor)
        {
            try
            {
                Cursor = savecursor;
                ssProgress.Style = ProgressBarStyle.Blocks;
                ssProgress.Visible = false;
                Application.DoEvents();
            }
            catch (Exception ex)  //Eat any exception here since we can get bogus exceptions due to form not being initialized yet
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);  
            }
        }

        private void UpdateTitle()
        {
            this.Text = sqlnexus.Properties.Resources.Msg_Nexus + " " + Application.ProductVersion.ToString() + " - " + CurrentReport.DisplayName;
        }

        public void StartStopSpinner(bool start, Spinner spinner)
        {
            spinner.Active = start;
        }

        private void tcReports_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTitle();
            UpdateReportButtons();
        }

        public DialogResult ShowConnectionDlg()
        {
            //fmConnect connect = new fmConnect(this);
            fmLoginEx connect = new fmLoginEx();
            DialogResult dr = connect.ShowDialog();
            if (dr == DialogResult.OK)
            {
                if (!CreateDB("sqlnexus"))
                {
                    dr = DialogResult.Abort;
                }
                
            }
            return dr;
        }

        
        void PopulateDatabaseList(String CurDB)
        {

            String SaveDB = Globals.credentialMgr.Database;
            Globals.credentialMgr.Database = "master";


            SqlDataAdapter da = new SqlDataAdapter("select name  from sys.databases where database_id not in (1,3, 4) and source_database_id is null and (name not like 'ReportServer%') and  databaseproperty (name, 'IsOffline') = 0 and databaseproperty (name, 'IsSingleUser') = 0 order by name", Globals.credentialMgr.ConnectionString);

            DataTable dt = new DataTable();
            da.Fill(dt);
            Globals.credentialMgr.Database = SaveDB;

            tscCurrentDatabase.Items.Clear();
            tscCurrentDatabase.Items.Add("<New Database>");
            
            foreach (DataRow row in dt.Rows)
            {
            
                tscCurrentDatabase.Items.Add(row["name"].ToString());
            }

            if (tscCurrentDatabase.Items.IndexOf(CurDB) < 0)
            {
                LogMessage("The database [" + CurDB + "] you entered doesn't exist or is not in a usable state (such as in single user mode). Switching database to tempdb. You can switch to available database from the dropdown list.", MessageOptions.All);
                CurDB = "tempdb";
            }
            
                tscCurrentDatabase.SelectedItem = CurDB;
                Globals.credentialMgr.Database = CurDB;

        }
        private void MakeImagesTransparent(Panel panel)
        {
            foreach (Control c in panel.Controls)
            {
                if (c is PictureBox)
                {
                    PictureBox pb = (c as PictureBox);
                    Bitmap Logo = new Bitmap(pb.Image);
                    Logo.MakeTransparent(Logo.GetPixel(0, 0));
                    pb.Image = (Image)Logo;
                }
            }
        }

        public Spinner spnImporter = new Spinner();
        public Spinner spnRealtime = new Spinner();
        public Spinner spnBackupRestore = new Spinner();

        private void InitAppEnvironment()
        {
            if (!Directory.Exists(Globals.AppDataPath))
                Directory.CreateDirectory(Globals.AppDataPath);
            if (!Directory.Exists(Globals.AppDataPath + @"\Reports"))
                Directory.CreateDirectory (Globals.AppDataPath + @"\Reports");
            if (!Directory.Exists(Globals.AppDataPath + @"\temp"))
                Directory.CreateDirectory(Globals.AppDataPath + @"\temp");
            Util.Logger = this;
        }
        private void fmNexus_Load(object sender, EventArgs e)
        {
            

            // Write our location to the registry so ReadTrace.exe can find us. 
            Registry.SetValue(REG_HKCU_APP_KEY, "InstallPath", 
                Path.GetDirectoryName(Application.ExecutablePath));
            Registry.SetValue(REG_HKCU_APP_KEY2, "InstallPath",
                Path.GetDirectoryName(Application.ExecutablePath)); 

            //Undo weird effect where Windows gets confused because
            //we're not flagged a GUI app
            // TODO: have console detect whether it's minimized and pass this info to fmNexus so that 
            // fmNexus can preseve a minimized state (e.g. START /MIN sqlnexus.exe ...). Low priority.
            this.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            if (!Globals.NoWindow && !Globals.ConsoleMode)
                this.WindowState = FormWindowState.Maximized;

            MakeTaskPaneImagesTransparent();

            SetupSpinners();

            AutoHide (false);

            Application.DoEvents();

            //Nuke image cache
            if (0 != Directory.GetDirectories(Path.GetTempPath(), "sqlnexus").Length)
                Directory.Delete(Path.GetTempPath() + "sqlnexus", true);

            // InitializeLog(Application.StartupPath + @"\sqlnexus.log");
            String logFileFullPath = String.Empty;
            if (Globals.ReportExportPath != null)
            { 
                logFileFullPath = Globals.ReportExportPath + @"\sqlnexus.log";
                if (!Directory.Exists (Globals.ReportExportPath ))
                {
                    Directory.CreateDirectory (Globals.ReportExportPath);
                }
            }
            else
            {
                logFileFullPath = @"%TEMP%\sqlnexus.log";
            }
            //FileMgr mgr = new FileMgr();
            //MessageBox.Show (mgr.ToString());
            RuntimeEnv.Env.NexusLogFile =logFileFullPath;
            InitializeLog(logFileFullPath);
            LogMessage("sqlnexus.exe running at: " + Application.ExecutablePath + " version " + Application.ProductVersion);
//#if BETA
//            LogMessage("This is a beta version");
//#endif 
            InitAppEnvironment();
           
            // Kick off async (background) ClickOnce autoupdate check.  Results of check will be 
            // written to the log file
            clickOnce.TraceLogger = this.TraceLogger;
            clickOnce.UpdateApplicationAsync();
            
          
            if (!Globals.ConsoleMode)
            {

                if (DialogResult.OK != ShowConnectionDlg())
                {   // User clicked Cancel on connection dialog
                    Application.Exit();
                    return;
                }
                if (Util.GetReadTracePath() == null)
                {
                    string dlgTitle = "Cannot Locate ReadTrace Path";
                    LogMessage("Unable to locate readtrace.  Nexus won't be able to load or analyze profiler trace data.", MessageOptions.All, TraceEventType.Error, dlgTitle);
                }
                else if (File.Exists(Application.StartupPath + @"\readtracenexusimporter.dll") == true)
                {
                    LogMessage("Extracing ReadTrace reports", MessageOptions.Silent);
                    Assembly assem = Assembly.LoadFile(Application.StartupPath + @"\readtracenexusimporter.dll");
                    INexusImporter ri = (INexusImporter)assem.CreateInstance("ReadTrace.ReadTraceNexusImporter", true);
                    ri.Initialize("*.out", Globals.credentialMgr.ConnectionString, Globals.credentialMgr.Server, true, "", "", Globals.credentialMgr.Database, this);
                }
                else
                {
                    LogMessage("Found readtrace path but ReadtraceNexusimporter.dll is not Available");
                }
                if (Util.GetReadTracePath() != null && !File.Exists(Util.GetReadTraceExe()))
                {
                    string dlgTitle = "Cannot Find ReadTrace.exe";
                    LogMessage("SQL Nexus located readtrace path as " + Util.GetReadTracePath() + " but it can't locate Readtrace.exe in this directory.\n\rYou have incorrect installation.  Trace import won't work", MessageOptions.All, TraceEventType.Error, dlgTitle);
                }

                
                
                
                string inst=Globals.credentialMgr.Server;

                int i=inst.IndexOf('\\');
                if (-1 != i)
                {
                    inst = inst.Substring(i + 1);
                }
                else
                {
                    inst = "(Default)";
                }

                UpdateSQLDiagInstance(inst);

                if (false)
                {
                 //   PromptStartCollector();
                }
                    
            }
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    EnumReports();
////                    InitCollectorService();

                    //if running in console mode (command line) , call ProcessReportQueue()
                    if (Globals.ConsoleMode)
                    {
                        //if it didn't process reports (due to db already present and contains nexus data) quit
                        if (false == ProcessReportQueue())
                        {
                            Thread.Sleep(500);
                            Application.Exit();
                        }

                        // If we were passed /X, exit after importing data and exporting reports
                        if (Globals.ExitAfterProcessingReports)
                            Application.Exit();
                    }
                    else
                    {
                        //Select the first report if there is one
                        if (0 != tvReports.Nodes.Count)
                            tvReports.SelectedNode = tvReports.Nodes[0];

                        ShowHideUIElements();

                        Application.DoEvents();

////                        UpdateServiceButtons();
                    }
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        public void EnumReports()
        {
            tvReports.Nodes.Clear();

            //Load special instructions report first
            LogMessage("sqlnexus.exe report directory: " + Application.StartupPath + @"\Reports");
            string instrep = Application.StartupPath + @"\Reports\SQL Perf Main" + RDL_EXT;
            if (File.Exists(instrep))
            {
                LogMessage("Instructions report found: " + instrep);
                instrep = ExtractReportName(instrep);
                AddFindReportNode(instrep, null);
            }
            else
            {
                LogMessage("Instructions report not found: " + instrep);
            }
            // Enum all reports in <exedir>\Reports
            EnumReportDirectory(Application.StartupPath + @"\Reports");

            // Enum all reports in %APPDATA%\SqlNexus\Reports
            // we don't need to enum reports from this directory anymore: jackli
            //everthing should be in startup directory
            //EnumReportDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SqlNexus\Reports");
            return;
        }

        string[] MainReportsToSkip = new string [] {
           "Blocking and Wait Statistics",
          "Blocking Chain Detail",
          "Blocking Runtime Detail",
          "Bottleneck Analysis",
          "Query Hash",
          "SQL 2000 Blocking Detail",
          "SQL 2000 Blocking",
           "Sysprocesses",
            "WaitDetails",
            "ReadTrace",
            "SQL Server 2000 Perf Stats",
            "SQL Server 2005 Perf Stats",
            "SQL Server 2008 Perf Stats"};

        private bool IsMainReport(string reportfilename)
        {
            if (null == reportfilename)
                return false;
            foreach (string str in MainReportsToSkip)
            {
                if (reportfilename.ToUpper().Contains(str.ToUpper()))
                    return false;
            }
            return true;
        }
        public void EnumReportDirectory(string reportDirectory)
        {
            if (!Directory.Exists(reportDirectory))
                return;
            string[] files = Directory.GetFiles(reportDirectory + @"\", "*" + RDL_EXT);
            foreach (string f in files)
            {
                if (!IsMainReport(f))
                    continue;
                if (0 == string.Compare(Path.GetExtension(f), RDLC_EXT, true, CultureInfo.InvariantCulture))
                    continue;
                string filename = ExtractReportName(f);
                if (0 == string.Compare(Path.GetFileName(filename), "Instructions.rdl", true, CultureInfo.InvariantCulture))
                    continue;
                LogMessage(sqlnexus.Properties.Resources.Msg_FoundReport + f, MessageOptions.Silent);
                AddFindReportNode(filename, null);
            }
        }

        private void ShowHideUIElements()
        {
            this.tspUnpin.Visible = !sqlnexus.Properties.Settings.Default.ShowReportNavigator;
            this.tspPin.Visible = sqlnexus.Properties.Settings.Default.ShowReportNavigator;
            this.mainMenuToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.ShowMainMenu;
            this.mainToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.ShowStandardToolbar;
            this.reportToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.ShowReportToolbar;
            this.serviceToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.ShowDataCollectionToolbar;
            this.askWhetherToStartTheSQLDiagCollectionServiceToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.SQLDiagStartDontAsk;
            this.askWhetherToStopTheSQLDiagCollectionServiceWhenExitingToolStripMenuItem.Checked = sqlnexus.Properties.Settings.Default.SQLDiagStopDontAsk;
            this.tsiShowReportTabs.Checked = sqlnexus.Properties.Settings.Default.ShowReportTabs;
            ToggleTabs(this.tsiShowReportTabs.Checked);
        }


        public  bool ProcessReportQueue()
        {
            fmImport fmi_local = new fmImport(this);

            //if Db exists and has nexus data imported in it, quit processing (return false)
            if (true == fmi_local.KeepPriorNonEmptyDb())
                return false;
            try
            {
                foreach (string path in Globals.PathsToImport)
                {
                    fmImport.ImportFiles(this, path);
                }
                foreach (string rpt in Globals.ReportsToRun)
                {
                    // If we were passed a /O command line parameter, export the reports; otherwise just display them. 
                    if ((null != Globals.ReportExportPath) && ("" != Globals.ReportExportPath))
                        this.RunAndExportReport(rpt, ReportExportType.Excel);
                    else
                        SelectLoadReport(rpt, true, null);
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }

            return true;
        }

        private void InitCollectorService()
        {
            if (enableDataCollector)
            {
                toolbarService.Visible = true;
                this.serviceToolStripMenuItem.Checked = true;
                if (InitializeService())
                    StartService();
            }
            else
            {
                if (!tsbStop.Enabled)
                {
                    toolbarService.Visible = false;
                    this.serviceToolStripMenuItem.Checked = false;
                }
            }
        }
        /*
        private void PromptStartCollector()
        {
            if ((!UpdateServiceButtons() || (!tsbStop.Enabled)))
            {
                if (sqlnexus.Properties.Settings.Default.SQLDiagStartDontAsk)
                {
                    enableDataCollector = Convert.ToBoolean(sqlnexus.Properties.Settings.Default.SQLDiagAutoStart);
                }
                else
                {
                    bool dontAsk;
                    enableDataCollector = (DialogResult.Yes == fmPrompt.ShowPrompt(sqlnexus.Properties.Resources.Msg_StartColl, sqlnexus.Properties.Resources.Msg_CollText, out dontAsk));
                    sqlnexus.Properties.Settings.Default.SQLDiagStartDontAsk = dontAsk;
                    sqlnexus.Properties.Settings.Default.SQLDiagAutoStart = enableDataCollector;
                }
            }
            else
            {
                serviceToolStripMenuItem.Checked = true;
                toolbarService.Visible = true;
            }
        }
        */
        private void MakeTaskPaneImagesTransparent()
        {
            MakeImagesTransparent(paReportsHeader);
            MakeImagesTransparent(paDataHeader);
            MakeImagesTransparent(paTasksHeader);
            MakeImagesTransparent(paTasksBody);
        }

        private void SetupSpinners()
        {
            this.spnImporter.Active = false;
            this.spnImporter.Color = System.Drawing.Color.DeepSkyBlue;
            this.spnImporter.InnerCircleRadius = 5;
            this.spnImporter.Location = new System.Drawing.Point(7, 10);
            this.spnImporter.Name = "Spinner1";
            this.spnImporter.NumberSpoke = 10;
            this.spnImporter.OuterCircleRadius = 8;
            this.spnImporter.RotationSpeed = 80;
            this.spnImporter.Size = new System.Drawing.Size(20, 20);
            this.spnImporter.SpokeThickness = 4;
            this.paTasksBody.Controls.Add(spnImporter);

            this.spnRealtime.Active = false;
            this.spnRealtime.Color = System.Drawing.Color.CornflowerBlue;
            this.spnRealtime.InnerCircleRadius = 4;
            this.spnRealtime.Location = new System.Drawing.Point(7, 33);
            this.spnRealtime.Name = "Spinner2";
            this.spnRealtime.NumberSpoke = 12;
            this.spnRealtime.OuterCircleRadius = 9;
            this.spnRealtime.RotationSpeed = 80;
            this.spnRealtime.Size = new System.Drawing.Size(20, 20);
            this.spnRealtime.SpokeThickness = 2;
            //this.paDataBody.Controls.Add(spnRealtime);

            this.spnBackupRestore.Active = false;
            this.spnBackupRestore.Color = System.Drawing.Color.CornflowerBlue;
            this.spnBackupRestore.InnerCircleRadius = 7;
            this.spnBackupRestore.Location = new System.Drawing.Point(7, 56);
            this.spnBackupRestore.Name = "Spinner3";
            this.spnBackupRestore.NumberSpoke = 36;
            this.spnBackupRestore.OuterCircleRadius = 8;
            this.spnBackupRestore.RotationSpeed = 20;
            this.spnBackupRestore.Size = new System.Drawing.Size(21, 21);
            this.spnBackupRestore.SpokeThickness = 4;
            //this.paDataBody.Controls.Add(spnBackupRestore);
        }

        private static void UpdateSQLDiagInstance(string inst)
        {
            // TODO: This is for real-time collection, which is not yet fully implemented...
            //XmlDocument doc = new XmlDocument();
            //doc.Load(Application.StartupPath + @"\Collection\SQLNexusRealTime.xml");
            //doc["dsConfig"]["Collection"]["Machines"]["Machine"]["Instances"]["Instance"].Attributes["name"].Value = inst;
            //doc.Save(Application.StartupPath + @"\Collection\SQLNexusRealTime.xml");
        }

        private string ExtractReportName(string f)
        {
            string filename = Path.GetFileNameWithoutExtension(f);
            //if ("_M"==filename.Substring(filename.Length-2,2))
            //    filename = filename.Substring(0, filename.Length - 2); //Remove "_M" from the end
            return filename;
        }

        private void fmNexus_FormClosing(object sender, FormClosingEventArgs e)
        {
            /*
            if (false)
            {
                //PromptStopCollector();
            }*/

            //Save settings to app.config
            sqlnexus.Properties.Settings.Default.ShowReportTabs = tsiShowReportTabs.Checked;
            sqlnexus.Properties.Settings.Default.Save();
            //workaround http://connect.microsoft.com/VisualStudio/feedback/details/522208/wpf-app-with-reportviewer-gets-error-while-unloading-appdomain-exception-on-termination
            CloseAllReports();
            //Thread.Sleep(1000);
        }

        private void PromptStopCollector()
        {
            bool stopcollector = true;
            if (tsbStop.Enabled)
            {
                if (sqlnexus.Properties.Settings.Default.SQLDiagStopDontAsk)
                {
                    stopcollector = Convert.ToBoolean(sqlnexus.Properties.Settings.Default.SQLDiagAutoStop);
                }
                else
                {
                    //bool dontAsk;
                   // stopcollector = (DialogResult.Yes != fmPrompt.ShowPrompt(sqlnexus.Properties.Resources.Msg_StopColl, sqlnexus.Properties.Resources.Msg_CollText, out dontAsk));
                 //   sqlnexus.Properties.Settings.Default.SQLDiagStopDontAsk = dontAsk;
                    sqlnexus.Properties.Settings.Default.SQLDiagAutoStop = stopcollector;
                }
            }

            if ((stopcollector) && (tsbStop.Enabled))
            {
                StopService();
            }
        }

        private void tvReports_AfterSelect(object sender, TreeViewEventArgs e)
        {
            
            if (!CanRunReport(e.Node.Text + ".rdl"))
            {
            //    this.LogMessage("The database doesn't have necessary data to run this report", MessageOptions.All);
                return;
            }
            SelectLoadReport(e.Node.Text, true, null);
        }
        private bool CanRunReport(string ReportName)
        { 
            String FullReportName;
            String FullReportPath;
            String FullXmlDoc;
            ReportName = Path.GetFileName(ReportName);
            if (File.Exists(Application.StartupPath + @"\Reports\" + ReportName))
            {
                FullReportName = Application.StartupPath + @"\Reports\" + ReportName;
                FullReportPath = Application.StartupPath + @"\Reports\";

            }
            else if (File.Exists(Globals.AppDataPath + @"\Reports\" + ReportName))
            {
                FullReportPath = Globals.AppDataPath + @"\Reports\";
                FullReportName = Globals.AppDataPath + @"\Reports\" + ReportName;
            }
            else
                return true; //assuming you don't want to validate
            FullXmlDoc = FullReportName + ".xml";
            if (!File.Exists(FullXmlDoc))
                return true; //assuming you don't want to validate
            XmlDocument doc = new XmlDocument();
            doc.Load(FullXmlDoc);
            XmlNode node = doc.SelectSingleNode("report/validate");
            String scriptName = node.Attributes["script"].Value.ToString();
            StreamReader sr = File.OpenText(FullReportPath + scriptName);
            CSql mySql = new CSql(Globals.credentialMgr.ConnectionString);
            mySql.ExecuteSqlScript (            sr.ReadToEnd());
            
            return mySql.IsSuccess;
            

        }
        private void tvReports_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (!CanRunReport(e.Node.Text + ".rdl"))
            {
                this.LogMessage("The database doesn't have necessary data to run this report", MessageOptions.All);
                return;
            }


            if (e.Node == tvReports.SelectedNode)
            {
                SelectLoadReport(e.Node.Text, true, null);
            }
        }

        private void tspUnpin_Click(object sender, EventArgs e)
        {
            AutoHide(true);
        }

        private void AutoHide(bool hide)
        {
            tspUnpin.Visible = !hide;
            tspPin.Visible = hide;
            splClient.Panel1Collapsed = hide;
        }

        private void tspPin_Click(object sender, EventArgs e)
        {
            AutoHide(false);
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public void ShowHelp(uint command)
        {
            ShowHelp("overview.htm", command);
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public static void CopyToClipboard(string text)
        {
            // We have to decode the text passed in from the form b/c our method invocation mechanism passes params as part of a URL
            Clipboard.SetDataObject(HttpUtility.UrlDecode(text), true, 5, 100);
        }


        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public void ShowHelp(string path, uint command)
        {
            HtmlHelp(0, "sqlnexus.chm::/"+path, command, null);
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public void ShowHelp(string path)
        {
            ShowHelp(path, HH_DISPLAY_TOPIC);
        }


        #endregion Form methods

        #region Tree node mgmt

        private TreeNode AddFindReportNode(string reportname, TreeNodeCollection parentnodes)
        {
            TreeNode node;
            string nodename = "tvn_" + reportname;
            
            //create a a tree node collection
            TreeNodeCollection pnodes = (null == parentnodes ? tvReports.Nodes : parentnodes);

            //create a tree node
            TreeNode[] nodes = pnodes.Find(nodename, true);


            if (-1==nodes.GetUpperBound(0))
            {
                node = new TreeNode(reportname);
                node.ImageIndex = 0;
                node.SelectedImageIndex = 0;
                node.Name = "tvn_" + reportname;
                pnodes.Add(node);
                // LogMessage(sqlnexus.Properties.Resources.Msg_AddedReportNode + node.Name, MessageOptions.Silent);
            }
            else
            {
                node = nodes[0];
            }
            return node;
        }

        private TreeNode RemoveFindReportNode(string reportname)
        {
            TreeNode[] nodes = tvReports.Nodes.Find("tvn_"+reportname, true);
            System.Diagnostics.Debug.Assert(null != nodes);
            if ((null != nodes[0].Parent) || (null!=nodes[0].Tag))
            {
                nodes[0].Remove();
                LogMessage(sqlnexus.Properties.Resources.Msg_RemovedNode + nodes[0].Name, MessageOptions.Silent);
                return null;
            }
            else
            {
                return nodes[0];
            }
        }

        #endregion Tree node mgmt

        #region Tab mgmt

        private void CloseTab(TabPage tab)
        {
            bool found;
            do
            {
                found = false;
                foreach (KeyValuePair<DataTable, string> kvp in reportDataTables)
                {
                    if (0 == string.Compare(kvp.Value, tab.Text, true, CultureInfo.InvariantCulture))
                    {
                        found = true;
                        reportDataTables.Remove(kvp.Key);
                        break;
                    }
                }
            } while (found);

            TreeNode n=RemoveFindReportNode(tab.Text);
            if (null != n)
            {
                n.ImageIndex = 0;
                n.SelectedImageIndex = 0;
            }
            tcReports.TabPages.Remove(tab);
            tvReports.SelectedNode = null;
            if (0 == tcReports.TabCount)
            {
                toolbarReport.Visible = false;
                this.editToolStripMenuItem.Enabled = false;
                //disable the panel with all the logs - Nexus, RML, copy to clipboard. For now we don't want this functionality
                //paTasksBody.Enabled = false;
            }
        }

        private void CloseAllButThis(TabPage tab)
        {
            foreach (TabPage p in tcReports.TabPages)
            {
                if (p != tab)
                {
                    CloseTab(p);
                }
            }
        }

        public void CloseAll()
        {
            foreach (TabPage p in tcReports.TabPages)
            {
                CloseTab(p);
            }
        }

        #endregion Tab mgmt

        #region Report mgmt

        public ReportViewer CurrentReportViewer
        {
            get
            {
                if ((0==tcReports.TabCount) || (null == tcReports.SelectedTab))
                {
                    return null;
                }
                else
                {
                    ReportViewer rv = ((tcReports.SelectedTab).Controls[0] as ReportViewer);
                    return rv;
                }
            }
        }

        public LocalReport CurrentReport
        {
            get
            {
                if (null == CurrentReportViewer)
                {
                    return null;
                }
                else
                {
                    return CurrentReportViewer.LocalReport;
                }
            }
        }

        private void RefreshReport(LocalReport report, ReportViewer rv)
        {
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    
                    string reportname = (0 == report.DisplayName.Length) ? Path.GetFileNameWithoutExtension(report.ReportPath) : report.DisplayName;
                    LogMessage(sqlnexus.Properties.Resources.Msg_RefreshingReport+reportname, MessageOptions.Silent);
                    LogMessage(sqlnexus.Properties.Resources.Msg_Refreshing);
                    foreach (KeyValuePair<DataTable, string> kvp in reportDataTables)
                    {
                        if (0 == string.Compare(kvp.Value, reportname, true, CultureInfo.InvariantCulture))
                        {
                            string DataSetName = kvp.Key.TableName;

                            string qrytext = fmNexus.GetQueryText(report.ReportPath, report.GetParameters(), DataSetName);

                            if (0 == qrytext.Length)
                                qrytext = DataSetName + " " + GetProcParams(report.ReportPath, report.GetParameters(), DataSetName);

                            SqlDataAdapter da = new SqlDataAdapter(qrytext, Globals.credentialMgr.ConnectionString);

                            kvp.Key.Clear();
                            da.Fill(kvp.Key);
                        }
                    }
                    rv.RefreshReport();
                    LogMessage(sqlnexus.Properties.Resources.Msg_RefreshDone);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }

        }

        private void CloseAllReports()
        {
          /*  foreach (TabPage p in tcReports.TabPages)
            {
                LocalReport r = (p.Controls[0] as ReportViewer).LocalReport;
                r.ReleaseSandboxAppDomain();
            }*/
           
            foreach (ReportViewer rv in Globals.ListOfReports)
            { 
                if (rv !=null && rv.LocalReport !=null)
                {
                    rv.LocalReport.ReleaseSandboxAppDomain();
                }
            }

        }
        
        private void RefreshAllReports()
        {
            foreach (TabPage p in tcReports.TabPages)
            {
                LocalReport r = (p.Controls[0] as ReportViewer).LocalReport;
                RefreshReport(r, (p.Controls[0] as ReportViewer));
            }
        }

        private bool IsQueryParam(string filename, string datasetname, string pname)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
//            nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");

            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }


            return (null != doc.DocumentElement.SelectSingleNode("//rds:Report/rds:DataSets/rds:DataSet[@Name = '" + datasetname + "']//rds:QueryParameter[@Name = '" + pname + "']", nsmgr));
        }

        private string GetProcParams(string filename, ReportParameterInfoCollection paramc, string datasetname)
        {
            string paramstr = "";
            foreach (ReportParameterInfo p in paramc)
            {
                string pname = "@" + p.Name;

                if (!IsQueryParam(filename, datasetname, pname))
                  continue;

                switch (p.DataType)
                {
                    case ParameterDataType.DateTime:
                        {
                            //paramstr += pname + "='" + Convert.ToDateTime(p.Values[0]).ToString("yyyy-MM-ddTHH:mm:ss.fff") + "'";
                            //some locale such as Italian converts date to format like 2008-05-27T11.34.56.000 (note the . between 11.34 and 34.56)
                            //so need to do this cultureinfo
                            paramstr += pname + "='" + DateTimeUtil.USString(Convert.ToDateTime(p.Values[0]),("yyyy-MM-ddTHH:mm:ss.fff")) + "'";
                            break;
                        }
                    case ParameterDataType.String:
                        {
                            paramstr += pname+"='" + p.Values[0] + "'";
                            break;
                        }
                    default:
                        {
                            paramstr += pname+"="+p.Values[0];
                            break;
                        }
                }
                paramstr += ", ";
            }
            if (0 != paramstr.Length)
                return paramstr.Substring(0, paramstr.Length - 2);  //Chop off trailing comma, space
            else
                return "";
        }

        const string PARAM_TOKEN = "=Parameters!";
        /// <summary>
        /// Substitutes report parameter values for any parameter names referenced in a DataSet's query.
        /// </summary>
        /// <remarks>Takes a query string like "EXEC myproc @Param1" and returns "EXEC myproc 'param_value'".</remarks>
        /// <param name="filename">Report .rdl or .rdlc filename</param>
        /// <param name="paramc">Collection of report parameters (including current values)</param>
        /// <param name="datasetname">Name of the DataSet that needs to have parameters replaced</param>
        /// <returns>Modified query string</returns>
        public static string GetQueryText(string filename, ReportParameterInfoCollection paramc, string datasetname)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }


            XmlNode qrynode = doc.DocumentElement.SelectSingleNode("//rds:Report/rds:DataSets/rds:DataSet[@Name = '" + datasetname + "']/rds:Query", nsmgr);
            if (null == qrynode)
                return "";

            XmlNode cmdTextNode = qrynode.SelectSingleNode("rds:CommandText", nsmgr);
            if (null == cmdTextNode)
                return "";
            string qrytext = cmdTextNode.InnerText;
            XmlNodeList qparams = qrynode.SelectNodes("rds:QueryParameters/rds:QueryParameter", nsmgr);
            // e.g. <QueryParameter Name="@Filter3"> <Value>=Parameters!Filter3.Value</Value> </QueryParameter>

            // A report may contain a query like this: "EXEC proc @Filter1, @Filter1Name".  The result of this could 
            // be "EXEC proc 'filter1val', 'filter1val'Name", which will cause a syntax error.  To avoid this problem, 
            // sort the parameters in descending order of parameter name length. In the above example, we avoid the 
            // problem by searching and replacing "@Filter1Name" first. 
            ArrayList arrayXmlNodes = new ArrayList();
            foreach (XmlNode node in qparams)
            {
                arrayXmlNodes.Add(node);
            }
            arrayXmlNodes.Sort(new CompareXmlNodeNameAttrLength());

            // For each query parameter 
            foreach (XmlNode n in arrayXmlNodes)
            {
                XmlNode qparamval = n.SelectSingleNode("rds:Value", nsmgr);
                string paramname, qpname;
                if ((null != qparamval) && (0 == qparamval.InnerText.IndexOf(PARAM_TOKEN, StringComparison.InvariantCultureIgnoreCase)))
                {
                    string[] parts = qparamval.InnerText.Substring(PARAM_TOKEN.Length).Split('.');
                    paramname = parts[0];   // "Filter3"
                }
                else  //Default to name of var
                {
                    paramname = n.Attributes["Name"].Value;
                    if ('@' == paramname[0])
                        paramname = paramname.Substring(1); // "Filter3"
                }

                qpname = n.Attributes["Name"].Value;    // e.g. "@Filter3"

                // If the query parameter should take on the value of a report parameter, we need to find the 
                // report parameter's current value.  
                string paramstr = "";
                
                foreach (ReportParameterInfo p in paramc)
                {
                    if (0!=string.Compare(paramname,p.Name,true,CultureInfo.InvariantCulture))
                        continue;   // keep going until we find the right param name

                    if ((p.Values.Count>0) && (null != p.Values[0]))
                    {   // param has an existing value
                        switch (p.DataType)
                        {
                            case ParameterDataType.DateTime:
                                {
                                    paramstr = "'" + DateTimeUtil.USString(Convert.ToDateTime(p.Values[0]),"yyyy-MM-ddTHH:mm:ss.fff") + "'";
                                    break;
                                }
                            case ParameterDataType.String:
                                {
                                    //if ((p.Values[0].Length < 2) || ("0x" != p.Values[0].Substring(0, 2)))
                                    //{
                                        paramstr = "'" + p.Values[0].Replace("'", "''") + "'";
                                    //}
                                    //else //special handling for binary
                                    //{
                                    //    paramstr = p.Values[0];
                                    //}
                                    break;
                                }
                            case ParameterDataType.Boolean:
                                {
                                    paramstr = Convert.ToInt32(0 == string.Compare("true", p.Values[0], true, CultureInfo.InvariantCulture)).ToString();
                                    break;
                                }
                            default:
                                {
                                    paramstr = p.Values[0].ToString();
                                    break;
                                }
                        }
                    }
                    else if (Globals.UserSuppliedReportParameters.ContainsKey(paramname))
                    {   // user supplied a param value on the command line
                        paramstr = Globals.UserSuppliedReportParameters[paramname];
                    }
                    else
                    {   // param has no value
                        paramstr = "NULL";
                    }
                    break;
                }

                qrytext = qrytext.Replace("@" + paramname, paramstr);

                //if (-1 != qrytext.IndexOf(qpname + ',', StringComparison.InvariantCultureIgnoreCase))
                //{   
                //    qrytext = qrytext.Replace(qpname + ',', paramstr);
                //}
                //else if (-1 != qrytext.IndexOf("@" + paramname, StringComparison.InvariantCultureIgnoreCase))
                //{
                //    qrytext = qrytext.Replace("@" + paramname, paramstr);
                //}
                //else if (-1 != qrytext.IndexOf(paramname, StringComparison.InvariantCultureIgnoreCase))
                //{
                //    qrytext = qrytext.Replace(paramname, paramstr);
                //}
                //else  //No param token in query text
                //{
                //    qrytext += " " + paramstr + ",";
                //}
            }
            ////Trim off trailing comma if there is one
            //if (',' == qrytext[qrytext.Length - 1])
            //    qrytext = qrytext.Substring(0, qrytext.Length - 1);
            return qrytext;
        }

        private void FixupDataSources(string filename, string reportname, ReportDataSourceCollection datasources, ReportParameterInfoCollection paramc)
        {
            try
            {
                System.Diagnostics.Trace.WriteLine(sqlnexus.Properties.Resources.Msg_DSFixup);
                XmlDocument doc = new XmlDocument();
                doc.Load(filename);

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition");
                //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
                String strNameSpace = ReportUtil.GetReportNameSpace(doc);
                if (strNameSpace != null)
                {
                    nsmgr.AddNamespace("rds", strNameSpace);
                }
                else
                {
                    nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
                }

            

                SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString);
                foreach (XmlNode n in doc.DocumentElement.SelectNodes("//rds:Report//rds:DataSets/rds:DataSet", nsmgr))
                {
                    string DataSetName = n.Attributes["Name"].Value;

                    string qrytext = fmNexus.GetQueryText(filename, paramc, DataSetName);

                    if (0 == qrytext.Length)
                        qrytext = DataSetName + " " + GetProcParams(filename, paramc, DataSetName);

                    //SqlDataAdapter da = new SqlDataAdapter(qrytext, Globals.ConnectionString);
                    SqlCommand cmd = new SqlCommand(qrytext, conn);
                    cmd.CommandTimeout = sqlnexus.Properties.Settings.Default.QueryTimeout;
                    SqlDataAdapter da = new SqlDataAdapter(cmd);

                    DataTable dt = new DataTable();
                    dt.TableName = DataSetName;

                    reportDataTables.Add(dt, reportname);

                    da.Fill(dt);

                    ReportDataSource rds = new ReportDataSource();
                    rds.Name = DataSetName;
                    rds.Value = dt;

                    datasources.Add(rds);
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        /// <summary>
        /// Open a Reporting Services report, hook its DataSets up to our SQL data source, and render the report. 
        /// </summary>
        /// <remarks>This is called to open any report that has an entry in the report treeview.  It is not called 
        /// for "native" drillthrough (drillthrough within the same ReportViewer control), though it is called for 
        /// CTRL+click drillthrough. </remarks>
        /// <param name="report">File name (.RDL)</param>
        /// <param name="master">true for top-level reports (.RDL), false for child reports (.RDLC)</param>
        /// <param name="parameters">Report parameter collection (can be null)</param>
        public void SelectLoadReport(string report, bool master, ReportParameter[] _parameters)
        {
            //Theme is a standard parameter that has to exist in all reports to make sure we comply with accessiblity review 
            //if the report does not contain the parameter "ContrastTheme" it will fail to load.
            ReportParameter paramTheme = new ReportParameter("ContrastTheme", Properties.Settings.Default.Theme);
            ReportParameter[] parameters = new ReportParameter[1];
            parameters[0] = paramTheme;

            if (_parameters != null)
            {
                ((ReportParameter[])_parameters.Where(x => x.Name != "ContrastTheme")).CopyTo(parameters,1);
            }

            NexusInfo nInfo = new NexusInfo(Globals.credentialMgr.ConnectionString, this);
            nInfo.SetAttribute("Nexus Report Version", Application.ProductVersion);

            Cursor save = StartWaiting();
            try
            {
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_LoadingReport+report, MessageOptions.Silent);

                    bool fullpath = false;
                    string filename;
                    if ("" == Path.GetExtension(report))
                    {
                        filename = GetFullReportPath(report, (master ? RDL_EXT : RDLC_EXT));
                        Util.Logger.LogMessage("Loading report from: " + filename);
                    }
                    else  //Assume full path to file
                    {
                        fullpath = true;
                        filename = report;
                        report = ExtractReportName(report);
                    }

                    int i = tcReports.TabPages.IndexOfKey(report);
                    // This report isn't currently open; create a new tab and associated ReporterViewer instance. 
                    if (-1 == i) 
                    {

                        ReportViewer rv = new ReportViewer();
                        rv.Name = "rv" + report;

                        //rv.LocalReport.ExecuteReportInCurrentAppDomain(AppDomain.CurrentDomain.Evidence);
                        //fixing error related to System.InvalidOperationException: Report execution in the current AppDomain requires Code Access Security policy, which is off by default in .NET 4.0 and later.  Enable legacy CAS policy or execute the report in the sandbox AppDomain.

                        //rv.LocalReport.ExecuteReportInSandboxAppDomain();
                        //rv.LocalReport.AddTrustedCodeModuleInCurrentAppDomain("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                        Globals.ListOfReports.Add(rv);

                        AddCustomAssemblies(filename, rv);

                        //rv.LocalReport.AddTrustedCodeModuleInCurrentAppDomain("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                        //rv.LocalReport.AddTrustedCodeModuleInCurrentAppDomain("RenderBitmap, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"); 

                        rv.LocalReport.ReportPath = filename;


                        rv.LocalReport.EnableHyperlinks = true;
                        rv.LocalReport.EnableExternalImages = true;
                        rv.LocalReport.DisplayName = report;
                        if (null != parameters)
                        {
                            rv.LocalReport.SetParameters(parameters);
                        }
                        SetReportQueryParameters(rv.LocalReport);
                        SetReportUserProvidedParameters(rv.LocalReport);

                        FixupDataSources(filename, report, rv.LocalReport.DataSources, rv.LocalReport.GetParameters());

                        TabPage p = new TabPage(report);
                        p.Name = report;
                        p.Controls.Add(rv);

                        rv.Dock = DockStyle.Fill;

                        rv.ShowContextMenu = false;

                        rv.ContextMenuStrip = cmReport;
                        rv.ShowToolBar = false;

                        tcReports.TabPages.Add(p);
                        tcReports.SelectTab(tcReports.TabPages.Count - 1);

                        //Hide the damned tabs!
                        if (!this.tsiShowReportTabs.Checked)
                        {
                            tcReports.ItemSize = new Size(0, 1);
                        }

                        //Hook up event syncs
                        rv.LocalReport.SubreportProcessing += new SubreportProcessingEventHandler(ProcessSubreport);

                        rv.ReportRefresh += new System.ComponentModel.CancelEventHandler(this.rvTemplate_ReportRefresh);
                        rv.Hyperlink += new Microsoft.Reporting.WinForms.HyperlinkEventHandler(this.rvTemplate_Hyperlink);
                        rv.Back += new Microsoft.Reporting.WinForms.BackEventHandler(this.rvTemplate_Back);
                        rv.Drillthrough += new Microsoft.Reporting.WinForms.DrillthroughEventHandler(this.rvTemplate_Drillthrough);
                        rv.Toggle += new System.ComponentModel.CancelEventHandler(this.rvTemplate_Toggle);
                        rv.RenderingBegin += new System.ComponentModel.CancelEventHandler(this.rvTemplate_RenderingBegin);
                        rv.RenderingComplete += new Microsoft.Reporting.WinForms.RenderingCompleteEventHandler(this.rvTemplate_RenderingComplete);
                        rv.ReportError += new Microsoft.Reporting.WinForms.ReportErrorEventHandler(this.rvTemplate_ReportError);
                        rv.Click += new System.EventHandler(this.rvTemplate_Click);

                        if ((master) && (fmReportParameters.HasReportParameters(rv.LocalReport, true)))
                            fmNexus.GetReportParameters(false,"");

                        rv.RefreshReport();
                        toolbarReport.Visible = reportToolStripMenuItem.Checked;
                        TreeNode n = AddFindReportNode(report, null);
                        n.ImageIndex = 1;
                        n.SelectedImageIndex = 1;
                        
                        this.tvReports.AfterSelect -= new System.Windows.Forms.TreeViewEventHandler(this.tvReports_AfterSelect);
                        tvReports.SelectedNode = n;
                        this.tvReports.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvReports_AfterSelect);

                        if (fullpath)
                            n.Tag = 1;

                        paTasksBody.Enabled = true;

                    }
                    else
                    {   // This report is already open -- select the tab and refresh the report. 
                        tcReports.SelectTab(i);
                        if (!master)
                        {
                            if (null != parameters)
                                CurrentReport.SetParameters(parameters);
                            SetReportQueryParameters(CurrentReport);
                            RefreshReport(CurrentReport, CurrentReportViewer);
                        }
                    }
                    LogMessage(sqlnexus.Properties.Resources.Msg_Loaded);
                    UpdateTitle();
                    UpdateReportButtons();
                }
                catch (LocalProcessingException ex)
                {
                    MessageBox.Show(report + " : Failed to load \r\n" + ex.InnerException.Message);
                }
                catch (Exception ex)
                {
                    
                    
                    

                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private static void AddCustomAssemblies(string filename, ReportViewer rv)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            String strNameSpace = ReportUtil.GetReportNameSpace (doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }

            //foreach (XmlNode n in doc.DocumentElement.SelectNodes("//rds:CodeModule", nsmgr))
            //{
            //    rv.LocalReport.AddTrustedCodeModuleInCurrentAppDomain(n.InnerText);
            //}
        }

        /// <summary>
        /// Retrieves default parameter values from the database for any report parameters that are bound to a SQL query. 
        /// </summary>
        /// <remarks>Called by <code>SelectLoadReport()</code>.</remarks>
        /// <param name="report">RS report</param>
        private static void SetReportQueryParameters(LocalReport report)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(report.ReportPath);
            //MessageBox.Show("name space" + ReportUtil.GetReportNameSpace(doc));
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");

            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }


            // Retrieve all report parameters that have a default value bound to a DataSet
            XmlNodeList nodes = doc.DocumentElement.SelectNodes("//rds:Report//rds:ReportParameters/rds:ReportParameter[rds:DefaultValue/rds:DataSetReference]", nsmgr);

            //If no params, bail
            if ((null == nodes) || (0 == nodes.Count))
            {
                return;
            }

            //Manually adding ContrastTheme since this is needed for Accessiblity and TrIP reviews
            ReportParameter[] rparameters = new ReportParameter[nodes.Count+1];
            rparameters[0] = new ReportParameter("ContrastTheme", Properties.Settings.Default.Theme);
            int i = 1;
            foreach (XmlNode node in nodes)
            {
                
                // Get the name of the DataSet associated with this param default
                XmlNode dsetnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:DataSetName", nsmgr);
                
                if (null != dsetnode)  //value from dataset
                {
                    // Get the name of the DataSet field/column to use for the default value
                    XmlNode vfnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:ValueField", nsmgr);
                    System.Diagnostics.Debug.Assert(null != vfnode);

                    DataTable dt = new DataTable();
                    // Should we attempt to do param substitution here (fmNexus.GetQueryText), or just pass InnerText unmodified?
                    SqlDataAdapter da = new SqlDataAdapter(fmNexus.GetQueryText(report.ReportPath, report.GetParameters(), dsetnode.InnerText), Globals.credentialMgr.ConnectionString);
                    da.Fill(dt);
                    // Add a new param to our param array
                    String paramName = node.Attributes["Name"].Value;
                    if ((dt.Rows.Count > 0) && (!paramName.Equals("ContrastTheme")))
                        rparameters[i++] = new ReportParameter(paramName, dt.Rows[0][vfnode.InnerText].ToString());
                }
            }
            if (0!=i)
                report.SetParameters(rparameters);
        }

        /// <summary>
        /// Retrieves report parameter default values that were provided on the command line via /V. 
        /// </summary>
        /// <remarks>Called by <code>SelectLoadReport().</code>.</remarks>
        /// <param name="report">RS report</param>
        private static void SetReportUserProvidedParameters(LocalReport report)
        {
            int i = 0;
            ReportParameterInfoCollection reportParams = report.GetParameters();
            // Walk through all of this report's parameters, checking to see if a value for each param was 
            // provided on the command line. 
            // Use an ArrayList for this pass since we don't yet know how many parameters we'll be defining.
            ArrayList newReportParamArrayList = new ArrayList();
            foreach (ReportParameterInfo p in reportParams)
            {
                if (Globals.UserSuppliedReportParameters.ContainsKey(p.Name))
                {
                    newReportParamArrayList.Add (new ReportParameter(p.Name, Globals.UserSuppliedReportParameters[p.Name]));
                }
            }
            // Move the new parameters to the ReportParameter array that SetParameters() requires.
            ReportParameter[] newParameters = new ReportParameter[newReportParamArrayList.Count];
            foreach (ReportParameter p in newReportParamArrayList)
            {
                newParameters[i++] = p;
            }
            report.SetParameters(newParameters);
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public static void GetReportParameters(bool autorefresh, string reportparamname)
        {
            try
            {
                if (fmReportParameters.GetReportParameters(singleton.CurrentReport, reportparamname, singleton, singleton))
                {
                    if (autorefresh)
                        singleton.RefreshReport(singleton.CurrentReport, singleton.CurrentReportViewer);
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, singleton, singleton);
            }
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public static void GetReportParameters(string reportparamname)
        {
            GetReportParameters(true, reportparamname);
        }

        private bool DrillThrough(LocalReport report, string reportpath)
        {
            
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_Drillthrough+reportpath, MessageOptions.Silent);
                    if (0 != (0x80 & GetKeyState(0x11)))  //If CTRL is pressed, create new tab
                    {

                        TreeNode[] pn = tvReports.Nodes.Find("tvn_" + CurrentReport.DisplayName, false);

                        System.Diagnostics.Debug.Assert(null != pn);

                        TreeNode n = AddFindReportNode(Path.GetFileNameWithoutExtension(reportpath), pn[0].Nodes);
                        n.EnsureVisible();
                        n.ImageIndex = 1;
                        n.SelectedImageIndex = 1;

                        ReportParameterInfoCollection paramc = report.GetParameters();
                        ReportParameter[] parameters = new ReportParameter[paramc.Count];
                        int i = 0;
                        foreach (ReportParameterInfo p in paramc)
                        {
                            parameters[i++] = new ReportParameter(p.Name, p.Values[0]);
                        }
                        // First check for a child report (.RDLC) with the specified name in the same dir as the parent report
                        string filename = Path.GetDirectoryName(report.ReportPath) + @"\" + reportpath + RDLC_EXT;
                        if (!File.Exists(filename))
                        {   // If not found, use our own reports dirs
                            filename = GetFullReportPath(reportpath, RDLC_EXT);
                        }
                        SelectLoadReport(filename, false, parameters);
                        return true;
                    }
                    else
                    {
                        // First check for a child report (.RDLC) with the specified name in the same dir as the parent report
                        string filename = Path.GetDirectoryName(report.ReportPath) + @"\" + reportpath + RDLC_EXT;
                        if (!File.Exists(filename)) 
                        {   // If not found, use our own reports dirs
                            filename = GetFullReportPath(reportpath, RDLC_EXT);
                        }

                        LogMessage(sqlnexus.Properties.Resources.Msg_Drillthrough + filename, MessageOptions.Silent);

                        //ReportFileManager.NeedToSupplyParamete("ts");
                        //jackli, reordering reports 
                        /*if (true == ReportFileManager.NeedToSupplyParameter(report.ReportPath))
                        {*/

                        //don't know whey we needed to call it twice
                            SetReportQueryParameters(report);
                            //SetReportQueryParameters(report);
                        /*}*/

                        FixupDataSources(filename, reportpath, report.DataSources, report.GetParameters());
                        return false;
                    }
                }
                catch (LocalProcessingException lex)
                {
                    MessageBox.Show(report.DisplayName + " Failed to load report : \r\n" + lex.InnerException.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                    return true;
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void ExportReport(ReportExportType exporttype)
        {
            ExportReport(exporttype, "");
        }

        public void RunAndExportReport(string filename, ReportExportType exporttype)
        {
            SelectLoadReport(filename, true, null);
            //filename = Path.GetFileNameWithoutExtension(filename)+".XLS";
            filename = Path.GetDirectoryName(Globals.ReportExportPath) + @"\" + Path.GetFileNameWithoutExtension(filename) + ".XLS";
            ExportReport(ReportExportType.Excel, filename);
        }
        
        private void ExportReport(ReportExportType exporttype, string filename)
        {
            string IMAGE_DEVICE_INFO = @"<DeviceInfo><ColorDepth>24</ColorDepth>
                                <DpiX>96</DpiX><DpiY>96</DpiY><MarginBottom>.25in</MarginBottom>
                                <MarginLeft>.25in</MarginLeft><MarginRight>.25in</MarginRight>
                                <MarginTop>.25in</MarginTop><OutputFormat>{0}</OutputFormat>
                                <PageHeight>11in</PageHeight><PageWidth>10in</PageWidth>
                                <StartPage>1</StartPage></DeviceInfo>";


            Cursor save = StartWaiting();
            try
            {
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_Exporting + filename);
                    string nullStr = null;
                    string[] streamids;
                    Warning[] warnings = null;

                    string format;
                    string exporttypestr;

                    switch (exporttype)
                    {
                        case ReportExportType.Excel:
                            {
                                format = "Excel";
                                exporttypestr = "Excel";
                                break;
                            }
                        case ReportExportType.PDF:
                            {
                                format = "PDF";
                                exporttypestr = "PDF";
                                break;
                            }
                        case ReportExportType.BMP:
                            {
                                format = "Image";
                                exporttypestr = "BMP";
                                break;
                            }
                        case ReportExportType.EMF:
                            {
                                format = "Image";
                                exporttypestr = "EMF";
                                break;
                            }
                        case ReportExportType.GIF:
                            {
                                format = "Image";
                                exporttypestr = "GIF";
                                break;
                            }
                        case ReportExportType.JPEG:
                            {
                                format = "Image";
                                exporttypestr = "JPEG";
                                break;
                            }
                        case ReportExportType.PNG:
                            {
                                format = "Image";
                                exporttypestr = "PNG";
                                break;
                            }
                        case ReportExportType.TIFF:
                            {
                                format = "Image";
                                exporttypestr = "TIFF";
                                break;
                            }
                        default:
                            {
                                format = "Image";
                                exporttypestr = "JPEG";
                                break;
                            }
                    }

                    string device_info = string.Format(IMAGE_DEVICE_INFO, exporttypestr);
                    byte[] result = CurrentReport.Render(format, device_info, out nullStr, out nullStr, out nullStr, out streamids, out warnings);

                    foreach (Warning w in warnings)
                        LogMessage(sqlnexus.Properties.Resources.Msg_RenderWarning + w.Message, MessageOptions.Silent);

                    if (ReportExportType.Clipboard == exporttype)
                    {
                        MemoryStream stream = new MemoryStream(result);

                        DataObject d = new DataObject();
                        d.SetData(DataFormats.Bitmap, new Bitmap(stream));

                        Clipboard.SetDataObject(d, true);
                    }
                    else
                    {
                        //Write out the stream in a new file.

                        FileStream fstream = new FileStream(filename,
                                FileMode.Create,
                                System.Security.AccessControl.FileSystemRights.WriteData,
                                FileShare.None, 
                                4096,
                                FileOptions.None, Util.GetFileSecurity());
                        

                        //FileStream fstream = File.Create(filename, result.Length);
                        fstream.Write(result, 0, result.Length);
                        fstream.Close();
                    }

                    LogMessage(sqlnexus.Properties.Resources.Msg_ReportCopied);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void RunAllReports()
        {
            foreach (TreeNode n in tvReports.Nodes)
                tvReports.SelectedNode = n;
        }

        private string GetFullReportPath(string reportName, string reportExt)
        {
            string filename;
            if ("" == Path.GetExtension(reportName))
            {
                //jackli: no need to go thru app path anymore
                //filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SqlNexus\Reports\" + reportName + reportExt;
                //if (!File.Exists(filename))
                    filename = Application.StartupPath + @"\Reports\" + reportName + reportExt;
            }
            else
                filename = reportName;

            if (!File.Exists(filename))
                throw (new ArgumentException("Report not found: " + reportName + reportExt));

            return filename;
        }

#endregion Report mgmt

        #region Menu syncs
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About();
        }

        [System.Reflection.ObfuscationAttribute (Exclude=true)]
        public static void About()
        {
            fmAbout fm = new fmAbout();
            fm.ShowDialog();
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public void Help()
        {
            LogMessage("No help yet", MessageOptions.Dialog);
        }

        private void tspCopy_Click(object sender, EventArgs e)
        {
            if (null == CurrentReport)  //No open reports
                return;
            ExportReport(ReportExportType.Clipboard);
        }

        private void closeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (null == CurrentReport)  //No open reports
                return;
            CloseTab(tcReports.SelectedTab);
        }

        private void tspParams_Click(object sender, EventArgs e)
        {
            GetReportParameters(true,"");
        }

        private void closeAllToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (null == CurrentReport)  //No open reports
                return;
            CloseAll();
        }

        private void closeToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (null == CurrentReport)  //No open reports
                return;
            CloseTab(tcReports.SelectedTab);
        }

        private void closeAllButCurrentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == CurrentReport)  //No open reports
                return;
            CloseAllButThis(tcReports.SelectedTab);
        }

        private void mainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolbarMain.Visible = !toolbarMain.Visible;
        }

        private void serviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolbarService.Visible = !toolbarService.Visible;
        }

        private void reportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (0 != tcReports.TabCount)
            {
                toolbarReport.Visible = !toolbarReport.Visible;
            }
        }

        private void tstbRunAll_Click(object sender, EventArgs e)
        {
            RunAllReports();
        }

        private void tstbOpen_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == od_Report.ShowDialog(this))
            {
                foreach (string f in od_Report.FileNames)
                    SelectLoadReport(f, true, null);
            }
        }

        private void contentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHelp(HH_DISPLAY_TOC);
        }

        private void indexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHelp(HH_DISPLAY_INDEX);
        }

        private void supportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Properties.Resources.Msg_Nexus_SupportUrl);
        }

        private void contactUsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ContactUs();
        }

        [System.Reflection.ObfuscationAttribute(Exclude = true)]
        public static void ContactUs()
        {
            Process.Start(Properties.Resources.Msg_Nexus_SupportEmail);
        }

        private void mainMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.menuBarMain.Visible = (sender as ToolStripMenuItem).Checked;
        }

        bool menukeypressed = false;

        private void fmNexus_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Alt)
            {
                foreach (ToolStripMenuItem mnu in menuBarMain.Items)
                {
                    if (-1 != mnu.Text.IndexOf("&" + e.KeyCode.ToString().ToUpper(CultureInfo.InvariantCulture)))
                    {
                        this.menuBarMain.Visible = true;
                        mnu.Select();
                        mnu.ShowDropDown();
                        e.Handled = true;
                        menukeypressed = true;
                        return;
                    }
                }
            }
            menukeypressed = false;
        }

        private void fmNexus_KeyUp(object sender, KeyEventArgs e)
        {
            if ((!menukeypressed)
                && (e.KeyCode == (Keys.RButton | Keys.ShiftKey))  //Alt
                && (e.Modifiers == Keys.None))
            {
                menuBarMain.Visible = true; // !menuBarMain.Visible;
                if (menuBarMain.Visible)
                    this.fileToolStripMenuItem.Select();
                e.Handled = true;
                return;
            }
            if ((e.KeyCode == Keys.Escape)
                && (e.Modifiers == Keys.None))
            {
                menuBarMain.Visible = false;
                e.Handled = true;
                return;
            }

        }

        private void menuBarMain_MenuDeactivate(object sender, EventArgs e)
        {
            menuBarMain.Visible = mainMenuToolStripMenuItem.Checked;
        }

        private void tvReports_Click(object sender, EventArgs e)
        {
            menuBarMain.Visible = mainMenuToolStripMenuItem.Checked;
        }


        private void askWhetherToStartTheSQLDiagCollectionServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sqlnexus.Properties.Settings.Default.SQLDiagStartDontAsk = (sender as ToolStripMenuItem).Checked;
        }

        private void askWhetherToStopTheSQLDiagCollectionServiceWhenExitingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sqlnexus.Properties.Settings.Default.SQLDiagStopDontAsk = (sender as ToolStripMenuItem).Checked;
        }

        private void tstbConnect_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == ShowConnectionDlg())
            {
                RefreshAllReports();

            }
        }


        #endregion Menu syncs

        #region Report toolbar syncs

        private void UpdatePageNum(int pagenum)
        {
            tstbPage.Text = pagenum.ToString();
        }

        private void UpdatePageNum()
        {
            UpdatePageNum(CurrentReportViewer.CurrentPage);
        }

        private void UpdateReportButtons()
        {
            UpdatePageNum();
            UpdatePageCount();
            tsbFirst.Enabled = (CurrentReportViewer.CurrentPage != 1);
            tsbPrev.Enabled = (CurrentReportViewer.CurrentPage != 1);
            tsbNext.Enabled = (CurrentReportViewer.CurrentPage != CurrentReport.GetTotalPages());
            tsbLast.Enabled = (CurrentReportViewer.CurrentPage != CurrentReport.GetTotalPages());
            tsbBack.Enabled = CurrentReport.IsDrillthroughReport;
            tsbStopAct.Enabled = false;
            try
            {
                tsbDocMap.Enabled = (null != CurrentReport.GetDocumentMap());
            }
            catch (Exception ex)  //An exception means there's no doc map
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                tsbDocMap.Enabled = false;
            }
            tstbParams.Enabled = fmReportParameters.HasReportParameters(CurrentReport, false);
            getReportParametersToolStripMenuItem.Enabled = tstbParams.Enabled;
            copyToolStripMenuItem.Enabled = (0 != tcReports.TabCount);
            tstbFind.Text = "";
            tsbFind.Enabled = false;
            tsbFindNext.Enabled = false;

            //Unhook the event handler (no need to trigger the report's zoom code; it's already zoomed)
            this.tscZoom.SelectedIndexChanged -= new System.EventHandler(this.tscZoom_SelectedIndexChanged);


            switch (CurrentReportViewer.ZoomMode)
            {
                case ZoomMode.PageWidth:
                    {
                        tscZoom.SelectedIndex = 0;
                        break;
                    }
                case ZoomMode.FullPage:
                    {
                        tscZoom.SelectedIndex = 1;
                        break;
                    }
                default:
                    {
                        int i = tscZoom.Items.IndexOf(CurrentReportViewer.ZoomPercent.ToString() + "%");
                        if (-1 != i)
                        {
                            tscZoom.SelectedIndex = i;
                        }
                        break;
                    }
            }

            //Hook it back up
            this.tscZoom.SelectedIndexChanged += new System.EventHandler(this.tscZoom_SelectedIndexChanged);
        }


        private void UpdatePageCount()
        {
            tslaPages.Text = string.Format(sqlnexus.Properties.Resources.Msg_PageCount, CurrentReport.GetTotalPages());
        }

        private void tsbFirst_Click(object sender, EventArgs e)
        {
            try
            {
                CurrentReportViewer.CurrentPage = 1;
                UpdatePageNum();
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbPrev_Click(object sender, EventArgs e)
        {
            try
            {
                if (CurrentReportViewer.CurrentPage != 1)
                    CurrentReportViewer.CurrentPage -= 1;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbNext_Click(object sender, EventArgs e)
        {
            try
            {
                if (CurrentReport.GetTotalPages() != CurrentReportViewer.CurrentPage)
                    CurrentReportViewer.CurrentPage += 1;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbLast_Click(object sender, EventArgs e)
        {
            try
            {
                if (CurrentReport.GetTotalPages() != CurrentReportViewer.CurrentPage)
                    CurrentReportViewer.CurrentPage = Int32.MaxValue;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }

        }

        private void tstbPage_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (e.KeyChar == '\r')
                {
                    if ("0" == tstbPage.Text)  //No zero page -- default to 1
                    {
                        tstbPage.Text = "1";
                    }
                    CurrentReportViewer.CurrentPage = Convert.ToInt32(tstbPage.Text);
                    tstbPage.Text = CurrentReportViewer.CurrentPage.ToString();
                    e.Handled = true;
                }
                else if (
                    (Char.IsWhiteSpace(e.KeyChar))
                    || (Char.IsLetter(e.KeyChar))
                    || (Char.IsSymbol(e.KeyChar))
                    || (Char.IsPunctuation(e.KeyChar))
                    )
                {
                    //eat non-numerics - can't blindly eat everything but IsDigit because of nav keys, etc.
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }

        }

        private void tsbBack_Click(object sender, EventArgs e)
        {
            try
            {
                CurrentReportViewer.PerformBack();
                tsbBack.Enabled = CurrentReport.IsDrillthroughReport;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbStopAct_Click(object sender, EventArgs e)
        {
            try
            {
                CurrentReportViewer.CancelRendering(-1);
                tsbStopAct.Enabled = false;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshReport(CurrentReport, CurrentReportViewer);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbPrint_Click(object sender, EventArgs e)
        {
            PrintCurrentReport();
        }

        private void PrintCurrentReport()
        {
            try
            {
                CurrentReportViewer.PrintDialog();
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbLayout_Click(object sender, EventArgs e)
        {
            try
            {
                if (!tsbLayout.Checked)
                {
                    CurrentReportViewer.SetDisplayMode(DisplayMode.PrintLayout);
                    tscZoom.SelectedIndex = 1;  //Whole page
                    tstbFind.Enabled = false;
                    tsbFind.Enabled = false;
                    tsbFindNext.Enabled = false;
                }
                else
                {
                    CurrentReportViewer.SetDisplayMode(DisplayMode.Normal);
                    tscZoom.SelectedIndex = 5;  //100%
                    tstbFind.Enabled = true;
                    tsbFind.Enabled = (0 != tstbFind.Text.Length);
                }
                tsbLayout.Checked = !tsbLayout.Checked;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }

        }

        private void SearchReport()
        {
            try
            {
                tsbFindNext.Enabled = (0 != CurrentReportViewer.Find(tstbFind.Text, CurrentReportViewer.CurrentPage));
                if (!tsbFindNext.Enabled)
                    MessageBox.Show(this, sqlnexus.Properties.Resources.Msg_NotFound, sqlnexus.Properties.Resources.Msg_Nexus);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void SearchReportAgain()
        {
            try
            {
                tsbFindNext.Enabled = (0 != CurrentReportViewer.FindNext());
                if (!tsbFindNext.Enabled)
                    MessageBox.Show(this, sqlnexus.Properties.Resources.Msg_NoMoreFound, sqlnexus.Properties.Resources.Msg_Nexus);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbFind_Click(object sender, EventArgs e)
        {
            try
            {
                SearchReport();
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tstbFind_TextChanged(object sender, EventArgs e)
        {
            try
            {
                tsbFindNext.Enabled = false;
                tsbFind.Enabled = (0 != tstbFind.Text.Length);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbFindNext_Click(object sender, EventArgs e)
        {
            try
            {
                SearchReportAgain();
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tstbFind_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (e.KeyChar == '\r')
                {
                    if (tsbFindNext.Enabled)
                        SearchReportAgain();
                    else if (tsbFind.Enabled)
                        SearchReport();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tscZoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch (tscZoom.SelectedIndex)
                {
                    case 0:
                        {
                            CurrentReportViewer.ZoomMode = ZoomMode.PageWidth;
                            break;
                        }
                    case 1:
                        {
                            CurrentReportViewer.ZoomMode = ZoomMode.FullPage;
                            break;
                        }
                    default:
                        {
                            CurrentReportViewer.ZoomMode = ZoomMode.Percent;
                            string zoomstr = tscZoom.SelectedItem.ToString();
                            zoomstr = zoomstr.Substring(0, zoomstr.Length - 1);
                            CurrentReportViewer.ZoomPercent = Convert.ToInt32(zoomstr);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void tsbPageSetup_Click(object sender, EventArgs e)
        {
            try
            {
                ReportPageSettings rs = CurrentReport.GetDefaultPageSettings();
                PageSettings ps = new PageSettings();
                ps.Margins = rs.Margins;
                ps.PaperSize = rs.PaperSize;
                ps_Report.PageSettings = ps;
                if (DialogResult.OK == ps_Report.ShowDialog(this))
                {
                    //TODO:  Figure out what to do here
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void excelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReportExportType i = ReportExportType.Excel;
            if (sender is ToolStripMenuItem)
                i = (ReportExportType)(sender as ToolStripMenuItem).MergeIndex;  //Overloaded to keep code generic
            ExportCurrentReport(i);
        }

        private void ExportCurrentReport(ReportExportType exportType)
        {
            sd_Report.FileName = "";
            sd_Report.FilterIndex = (int)exportType;
            if (DialogResult.OK == sd_Report.ShowDialog(this))
            {
                if (Path.GetExtension (sd_Report.FileName).ToUpper() != ".XLS")
                {
                    LogMessage("Only excel file type is supported", MessageOptions.All);
                    return;
                }
                ExportReport((ReportExportType)exportType, sd_Report.FileName);
            }
        }

        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportReport(ReportExportType.Clipboard);
        }

        private void tsbDocMap_Click(object sender, EventArgs e)
        {
            CurrentReportViewer.DocumentMapCollapsed = !CurrentReportViewer.DocumentMapCollapsed;
        }

        #endregion Report toolbar syncs

        #region Explorer bar syncs

        private void pbExpandReports_Click(object sender, EventArgs e)
        {
            CollapseExpandPanel(paReportsBody, pbCollapseReports, pbExpandReports);
        }

        private void CollapseExpandPanel(Panel panel, PictureBox collapseBox, PictureBox expandBox)
        {
            panel.Visible = !panel.Visible;
            expandBox.Visible = !panel.Visible;
            collapseBox.Visible = panel.Visible;
        }

        private void pbExpandData_Click(object sender, EventArgs e)
        {
            CollapseExpandPanel(paLogBody, pbCollapseData, pbExpandData);
        }

        private void llReports_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CollapseExpandPanel(paReportsBody, pbCollapseReports, pbExpandReports);
        }

        private void llData_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CollapseExpandPanel(paLogBody, pbCollapseData, pbExpandData);
        }

        private void pbCollapseTasks_Click(object sender, EventArgs e)
        {
            CollapseExpandPanel(paTasksBody, pbCollapseTasks, pbExpandTasks);
        }

        private void llTasks_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CollapseExpandPanel(paTasksBody, pbCollapseTasks, pbExpandTasks);
        }

        private void llPrint_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PrintCurrentReport();
        }

        private void linkLabel8_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ExportCurrentReport(ReportExportType.Excel);
        }

        private void linkLabel7_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MailCurrentReport();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ExportReport(ReportExportType.Clipboard);
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CollapseExpandAll(true);
        }

        private void CollapseExpandAll(bool collapse)
        {
            if (paReportsBody.Visible == collapse)
                CollapseExpandPanel(paReportsBody, pbCollapseReports, pbExpandReports);
            if (paTasksBody.Visible == collapse)
                CollapseExpandPanel(paTasksBody, pbCollapseTasks, pbExpandTasks);
            if (paLogBody.Visible == collapse)
                CollapseExpandPanel(paLogBody, pbCollapseData, pbExpandData);
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CollapseExpandAll(false);
        }

        #endregion Explorer bar syncs

        #region Service routines
        private void SetButtonsStatus(bool bStatus)
        {
            tsbStart.Enabled = bStatus;
            tsbStop.Enabled = bStatus;
        }

        private bool UpdateServiceButtons()
        {
            bool bRes = false;
            try
            {
                scSQLDiag.Refresh();
                //If we've just now detected that it has stopped, alert the user
                if ((!tsbStart.Enabled) && (ServiceControllerStatus.Stopped == scSQLDiag.Status))
                    LogMessage(sqlnexus.Properties.Resources.Msg_ServiceStopped);
                tsbStart.Enabled = (ServiceControllerStatus.Stopped == scSQLDiag.Status);
                tsbStop.Enabled = ((!tsbStart.Enabled) && (ServiceControllerStatus.Running == scSQLDiag.Status));
                tscbAutoUpdate.Enabled = tsbStop.Enabled;
                tiReportAutoUpdate.Enabled = tsbStop.Enabled;
                bRes = true;
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, MessageOptions.Silent);
                SetButtonsStatus(false);
            }
            return bRes;
        }

        private bool CheckAndRegisterService()
        {
            bool bRes = false;
            try
            {
                if (!UpdateServiceButtons())
                {
                    if (!Util.ConnectingtoCurrentMachine(Globals.credentialMgr.Server))  //Can't register service on remote machine
                    {
                        LogMessage(sqlnexus.Properties.Resources.Msg_CantRegister, MessageOptions.Dialog);
                        return false;
                    }

                    //Check all local drives for the right sqldiag rather than 
                    //Letting the path hand us whatever it feels like
                    string exename= "sqldiag.exe";
                    foreach (string drv in System.Environment.GetLogicalDrives())
                    {
                        if (0 == string.Compare(drv, @"A:\", true, CultureInfo.InvariantCulture))
                            continue;
                        if (0 == string.Compare(drv, @"B:\", true, CultureInfo.InvariantCulture))
                            continue;
                        string pth = string.Format(@"{0}program files\microsoft sql server\90\tools\binn\", drv) + exename;
                        if (File.Exists(pth))
                        {
                            exename=pth;
                            break;
                        }
                    }
                    string regparams = "/R /I SQLNexusRealtime.xml /P \"" + Application.StartupPath + "\\Collection\" /O \"" + Application.StartupPath + string.Format("\\Collection\\Output\" /Vsqlnexusdb={0} /Asqlnexus", Globals.credentialMgr.Database);
                    ProcessStartInfo si = new ProcessStartInfo(exename, regparams);
                    si.UseShellExecute = false;
                    si.WindowStyle = ProcessWindowStyle.Hidden;
                    Process p = Process.Start(si);
                    if (null != p)
                        p.WaitForExit();
                }
                bRes = UpdateServiceButtons();
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, MessageOptions.Silent);
            }
            return bRes;
        }

        private bool InitializeService()
        {
            if (UpdateServiceButtons())
            {
                return true;
            }
            else //if not able to update button status, service may not be registered
            {
                return CheckAndRegisterService();
            }
        }

        private void tiServicePoll_Tick(object sender, EventArgs e)
        {
            try
            {
                tiServicePoll.Enabled = false;
                try
                {
                    UpdateServiceButtons();
                }
                finally
                {
                    tiServicePoll.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
                tiServicePoll.Enabled = false;
                SetButtonsStatus(false);
            }

        }

        private void tsbStart_Click(object sender, EventArgs e)
        {
            StartService();
        }

        private void StartService()
        {
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_StartingService);
                    tiServicePoll.Enabled = true;
                    SetButtonsStatus(false);
                    scSQLDiag.Start();
                    LogMessage(sqlnexus.Properties.Resources.Msg_ServiceStarted);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void tsbStop_Click(object sender, EventArgs e)
        {
            StopService();
        }

        private void StopService()
        {
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    SetButtonsStatus(false);
                    scSQLDiag.Stop(); //Abort; Pause = controlled shutdown
                    LogMessage(sqlnexus.Properties.Resources.Msg_StoppingService);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void tiReportAutoUpdate_Tick(object sender, EventArgs e)
        {
            if (null != CurrentReport)
            {
                RefreshReport(CurrentReport, CurrentReportViewer);
            }
        }

        int[] rupdVals = new int[] { 5000, 10000, 30000, 60000, 300000, 600000, 1800000 };

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            tiReportAutoUpdate.Enabled = false;
            if (0 != tscbAutoUpdate.SelectedIndex)
            {
                tiReportAutoUpdate.Interval = rupdVals[tscbAutoUpdate.SelectedIndex-1];
                tiReportAutoUpdate.Enabled = true;
            }
        }

        #endregion Service routines

        #region Mail methods

        private void CreateEmail(string[] ReportFiles)
        {/*
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    Microsoft.Office.Interop.Word.Application app = new Microsoft.Office.Interop.Word.Application();

                    object FileName = Application.StartupPath + @"\Docs\Analysis.Doc";
                    System.Diagnostics.Debug.Assert(File.Exists((string)FileName));

                    app.Visible = true;
                    object m = Missing.Value;
                    app.Documents.Open(ref FileName, ref m, ref m, ref m, ref m,
                        ref m, ref m, ref m, ref m, ref m,
                        ref m, ref m, ref m, ref m,
                        ref m, ref m);
                    app.ActiveWindow.EnvelopeVisible = true;
                    foreach (string f in ReportFiles)
                    {
                        object bFalse = false;
                        object bTrue = true;
                        app.Selection.InsertFile(f, ref m, ref bFalse, ref bFalse, ref bTrue);
                    }
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }*/
        }

        private void mailCurrentReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MailCurrentReport();
        }

        private void MailCurrentReport()
        {
            MailReport(Globals.AppDataPath  + @"\temp\_analysis.xls");
        }

        private void MailReport(string fname)
        {
            try
            {
                ExportReport(ReportExportType.Excel, fname);
                CreateEmail(new string[] { fname });
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void mailAllReportsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MailReports();
        }

        private void MailReports()
        {
            try
            {
                int rptcount = tcReports.TabPages.IndexOfKey("Instructions");
                if (-1 == rptcount)
                    rptcount = tcReports.TabCount;
                else
                    rptcount = tcReports.TabCount - 1;
                string[] files = new string[rptcount];

                int i = 0;
                foreach (TabPage p in tcReports.TabPages)
                {
                    if (0 == string.Compare("instructions", p.Text, true, CultureInfo.InvariantCulture))
                        continue;
                    files[i] = Application.StartupPath + @"\_" + p.Text + ".xls";
                    ExportReport(ReportExportType.Excel, files[i++]);
                }
                CreateEmail(files);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }
        #endregion Mail methods

        #region Report event syncs

        int InDrillthrough = 0;

        private void rvTemplate_Drillthrough(object sender, DrillthroughEventArgs e)
        {
            ReportViewer rv =(ReportViewer) sender ;
            //#1781
            // this is handling nasty problem when someone doubleclick the drill through
            //apparanetly reportviewer will render same report twice with doubleclick causing issues
            rv.Enabled = false;
            Cursor save = StartWaiting();
            try
            {

                // this is handling nasty problem when someone doubleclick the drill through
                //apparanetly reportviewer will render same report twice with doubleclick causing issues

                if (1 < System.Threading.Interlocked.Increment(ref InDrillthrough))
                {
                    LogMessage("Aborting drillthrough.  Another drillthrough is already in-progress.", MessageOptions.Silent);
                    return;
                }
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_RVSyncDrillthrough, MessageOptions.Silent);
                    LogMessage("rvTemplate_Drillthrough - Drillthrough", MessageOptions.Silent);
                    e.Cancel = DrillThrough((LocalReport)e.Report, e.ReportPath);
                    LogMessage("rvTemplate_Drillthrough - Done with Drillthrough", MessageOptions.Silent);
                }
                catch (LocalProcessingException lex)
                {
                    MessageBox.Show(e.Report.DisplayName + " Failed to load report : \r\n" + lex.InnerException.Message);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
                LogMessage("rvTemplate_Drillthrough - End", MessageOptions.Silent);
            }
            finally
            {
                LogMessage("rvTemplate_Drillthrough - Finally", MessageOptions.Silent);
                StopWaiting(save);
                System.Threading.Interlocked.Decrement (ref InDrillthrough);
                rv.Enabled = true;
            }
        }

        private static Object TryMethodCall(Type type, MethodInfo method, String name, String[] args)
        {
            //Names match
            if (String.Compare(method.Name, name, false, CultureInfo.InvariantCulture) != 0)
            {
                throw new Exception(method.DeclaringType + "." + method.Name + sqlnexus.Properties.Resources.Error_MethodDoesntMatch);
            }

            //Number of parameters matches
            ParameterInfo[] param = method.GetParameters();

            if (param.Length != args.Length)
            {
                throw new Exception(method.DeclaringType + "." + method.Name + sqlnexus.Properties.Resources.Error_MethodsSigsDontMatch);
            }

            //Types convertible
            Object[] newArgs = new Object[args.Length];

            for (int index = 0; index < args.Length; index++)
            {
                try
                {
                    newArgs[index] = Convert.ChangeType(args[index], param[index].ParameterType, CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    throw new Exception(method.DeclaringType + "." + method.Name + sqlnexus.Properties.Resources.Error_ArgConversionFailed, e);
                }
            }

            //Static or instance?
            Object instance = null;

            if (!method.IsStatic)
            {
                instance = Activator.CreateInstance(type);
            }

            //Invoke the method
            return method.Invoke(instance, newArgs);
        }

        private void ExecuteMethod(string[] args)
        {
            Assembly assembly;
            Type type;

            try
            {
                //Load the specified assembly and get the specified type
                if (args[0] != ".")
                    assembly = Assembly.LoadFrom(args[0]);
                else
                    assembly = Assembly.GetCallingAssembly();
                type = assembly.GetType(args[1], true);
            }
            catch (FileNotFoundException)
            {
                LogMessage(string.Format(sqlnexus.Properties.Resources.Error_NoAssembly, args[0]));
                return;
            }
            catch (TypeLoadException)
            {
                LogMessage(string.Format(sqlnexus.Properties.Resources.Error_NoType, args[1], args[0]));
                return;
            }

            //Get the type's methods
            MethodInfo[] methods = type.GetMethods();

            if (methods == null)
            {
                LogMessage(sqlnexus.Properties.Resources.Error_NoTypes);
                return;
            }

            //Create a new array for the call's args
            String[] newArgs = new String[args.Length - 3];

            if (newArgs.Length != 0)
            {
                Array.Copy(args, 3, newArgs, 0, newArgs.Length);
            }

            //Try each method for a match
            StringBuilder failureExcuses = new StringBuilder();

            foreach (MethodInfo m in methods)
            {
                Object obj = null;

                try
                {
                    obj = TryMethodCall(type, m, args[2], newArgs);
                }
                catch (Exception e)
                {
                    failureExcuses.Append(e.Message + "\r\n");
                    continue;
                }

                return;
            }
            LogMessage(sqlnexus.Properties.Resources.Error_NoMethod, MessageOptions.Dialog);
            LogMessage(failureExcuses.ToString(), MessageOptions.Silent);
        }

        private void rvTemplate_Hyperlink(object sender, HyperlinkEventArgs e)
        {
            try
            {
                //TODO: Put hyperlink code here
                LogMessage(sqlnexus.Properties.Resources.Msg_RVSyncLink, MessageOptions.Silent);
                const string ourtoken = "sqlnexus://";
                if (0==string.Compare(e.Hyperlink.Substring(0,ourtoken.Length),ourtoken,true, CultureInfo.InvariantCulture))
                {
                    e.Cancel = true;
                    string[] parts = e.Hyperlink.Substring(ourtoken.Length).Split('/');
                    if (parts.Length<3)
                    {
                        LogMessage(sqlnexus.Properties.Resources.Error_InvalidURL, MessageOptions.Dialog);
                        return;
                    }
                    ExecuteMethod(parts);
                }
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void rvTemplate_Toggle(object sender, CancelEventArgs e)
        {
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    //TODO:  Put Toggle  here
                    LogMessage(sqlnexus.Properties.Resources.Msg_RVSyncToggle, MessageOptions.Silent);

                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void rvTemplate_ReportRefresh(object sender, CancelEventArgs e)
        {
             Cursor save = StartWaiting();
            try
            {
                try
                {
                    LogMessage(sqlnexus.Properties.Resources.Msg_RVSyncRefresh, MessageOptions.Silent);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void rvTemplate_Back(object sender, BackEventArgs e)
        {
            Cursor save = StartWaiting();
            try
            {
                try
                {
                    //TODO:  Put Back code here
                    LogMessage(sqlnexus.Properties.Resources.Msg_RVSyncBack, MessageOptions.Silent);
                }
                catch (Exception ex)
                {
                    Globals.HandleException(ex, this, this);
                }
            }
            finally
            {
                StopWaiting(save);
            }
        }

        private void rvTemplate_RenderingComplete(object sender, RenderingCompleteEventArgs e)
        {
            try
            {
                UpdateReportButtons();
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void rvTemplate_RenderingBegin(object sender, CancelEventArgs e)
        {
            try
            {
                tsbStopAct.Enabled = true;
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
        }

        private void rvTemplate_ReportError(object sender, ReportErrorEventArgs e)
        {
            Globals.HandleException(e.Exception,null,this);
            e.Handled = true;
        }

        void ProcessSubreport(object sender, SubreportProcessingEventArgs e)
        {
            string filename = "";
            try
            {
                filename = GetFullReportPath(e.ReportPath, RDL_EXT);
            }
            catch (ArgumentException ex)
            {   // ArgEx thrown if filename does not exist.  
                // Report may be RDLC, not RDL -- we'll try again with that extension. 
                LogMessage (ex.ToString(), MessageOptions.Silent);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }

            try
            {
                if (!File.Exists(filename))
                {
                    filename = GetFullReportPath(e.ReportPath, RDLC_EXT);
                }

                FixupDataSources(filename, e.ReportPath, e.DataSources, e.Parameters);
            }
            catch (Exception ex)
            {
                Globals.HandleException(ex, this, this);
            }
       }

        private void rvTemplate_Click(object sender, EventArgs e)
        {
            menuBarMain.Visible = mainMenuToolStripMenuItem.Checked;
        }


        #endregion Report event syncs

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
        

            if (null == fmImportForm || fmImportForm.IsDisposed) 
            {
                fmImportForm = new fmImport(this);
                fmImportForm.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.fmNexus_FormClosed);
                fmImportForm.Show(this);
            }
            else
            {
                if (fmImportForm.WindowState == FormWindowState.Minimized)
                    fmImportForm.WindowState = FormWindowState.Normal;
                else
                    if (!fmImportForm.Visible)
                        fmImportForm.Show(this);
                fmImportForm.BringToFront();
            }
        }

        private void fmNexus_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (sender is fmImport)
                fmImportForm = null;
        }

    
       

        private void tsiShowReportTabs_Click(object sender, EventArgs e)
        {
            ToggleTabs(this.tsiShowReportTabs.Checked);
        }

        private void ToggleTabs(bool tabsenabled)
        {
            if (!tabsenabled)
            {
                tcReports.SizeMode = TabSizeMode.Fixed;
                tcReports.ItemSize = new Size(0, 1);
            }
            else
            {
                tcReports.SizeMode = TabSizeMode.Normal;
                tcReports.ItemSize = new Size(58, 18);  // Default tab size
            }
        }

        private void toolbarReport_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void tcReports_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        private void linkLabelImport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Globals.credentialMgr.Database.ToUpper() == "TEMPDB")
            {
                MessageBox.Show("Warning: You are using tempdb to import data");
            }
        }
        
        private bool CreateDB(String dbName)
        {
            
            Regex re = new Regex(@"((;|\[|\]|\s)+)|(^master$|^tempdb$|^msdb$|^model$)+");
            if (re.IsMatch(dbName))
            {
                LogMessage (String.Format("invalid database name {0} entered. try again",dbName), MessageOptions.All);
                return false;
            }
            Globals.credentialMgr.Database = "master";
            
            SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString);
            SqlCommand cmd = conn.CreateCommand();
            bool success = true;
            cmd.CommandText = String.Format(SQLScripts.CreateDB, dbName);
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            catch (SqlException sqlEx)
            {
                success = false;
                string dlgTitle = "Database Creation Failure";

                if (dbName.ToUpper() == "SQLNEXUS")
                {
                    LogMessage(String.Format(Properties.Resources.Error_Nexus_CreateDBFailure, dbName, Globals.credentialMgr.Server, sqlEx.Message), MessageOptions.Silent, TraceEventType.Error, dlgTitle);
                }
                else
                {
                    LogMessage(String.Format(Properties.Resources.Error_Nexus_CreateDBFailure, dbName, Globals.credentialMgr.Server, sqlEx.Message), MessageOptions.All, TraceEventType.Error, dlgTitle);
                }

            }
            finally
            {
                conn.Close();
            }
            if (success)
            {
                Globals.credentialMgr.Database = dbName;
                PopulateDatabaseList(dbName);
            }
            else
            {
                if (dbName.ToUpper() == "SQLNEXUS")
                {
                    LogMessage("Create database \"" + dbName + "\"" + " failed. Switching to use tempdb. But you can switch to other available databases", MessageOptions.Silent);
                }
                else
                {
                    LogMessage("Create database \"" + dbName + "\"" + " failed. Switching to use tempdb. But you can switch to other available databases", MessageOptions.All);
                }
                Globals.credentialMgr.Database = "tempdb";
                PopulateDatabaseList("tempdb");
            }

            //alwasy return true because we want to use tempdb instead
            return true; 

        }

        private void tscCurrentDatabase_TextChanged(object sender, EventArgs e)
        {

            
        }

        private void tscCurrentDatabase_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            if (tscCurrentDatabase.SelectedItem.ToString() == "<New Database>")
            {
                String CurrentDatabase = Globals.credentialMgr.Database;

                String NewDBName =Microsoft.VisualBasic.Interaction.InputBox("Enter your database name", "Database Name", "", this.Location.X + this.Size.Width / 2, this.Location.Y + this.Size.Height / 2);
                if (NewDBName.Trim().Length == 0)
                {
                    PopulateDatabaseList(CurrentDatabase);
                    return;
                }
                bool createDB = CreateDB(NewDBName);
                
                if (createDB)
                    PopulateDatabaseList(NewDBName);
                else
                    PopulateDatabaseList("sqlnexus");
                 
            }
            else
            {
                refreshAfterDBChange();
            }
        }

        private void refreshAfterDBChange()
        {
            Globals.credentialMgr.Database = tscCurrentDatabase.SelectedItem.ToString();
            CloseAll();
            EnumReports();
            if (0 != tvReports.Nodes.Count)
                tvReports.SelectedNode = tvReports.Nodes[0];
            ShowHideUIElements();
            Application.DoEvents();
        }

        private void tsb_CustomRowset_Click(object sender, EventArgs e)
        {
            fmCustomRowset fm = new fmCustomRowset(this);
            fm.Show();
        }

        private void tscCurrentDatabase_Click(object sender, EventArgs e)
        {

        }

        private void ll_CustomRowset_Click(object sender, EventArgs e)
        {
            fmCustomRowset fm = new fmCustomRowset(this);
            fm.Show();
        }

        private void t(object sender, EventArgs e)
        {

        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //ProcessStartInfo pi = new ProcessStartInfo ("notepad.exe", Util.Env.ReadTraceLogFile
        }

        private void llOpenNexusLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Util.OpenFile(Util.Env.NexusLogFile, " ");
            
        }

        private void llOpenReadTraceLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Util.OpenFile(Util.Env.ReadTraceLogFile, "You need to run import at least once in order to generate this file");

        }

        private void picOpenNexusLog_Click(object sender, EventArgs e)
        {
            Util.OpenFile(Util.Env.NexusLogFile, " ");
        }

        private void picOpenReadTraceLog_Click(object sender, EventArgs e)
        {
            Util.OpenFile(Util.Env.ReadTraceLogFile, "You need to run import at least once in order to generate this file");

        }

        private void tscCurrentDatabase_TextUpdate(object sender, EventArgs e)
        {
            //MessageBox.Show(tscCurrentDatabase.Text);
        }

        private void tscCurrentDatabase_Leave(object sender, EventArgs e)
        {
           // PopulateDatabaseList(tscCurrentDatabase.Text);
           // tscCurrentDatabase.SelectedItem = tscCurrentDatabase.Text;

            
            //refreshAfterDBChange();
        }

        private void tscCurrentDatabase_KeyPress(object sender, KeyPressEventArgs e)
        {
            
          //  keypressed(sender, e);

          

        }

        private void keypressed(Object o, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return || e.KeyChar==Convert.ToChar(103))
            {
                PopulateDatabaseList(tscCurrentDatabase.Text);
                //tscCurrentDatabase.SelectedItem = tscCurrentDatabase.Text;
                e.Handled = true;
            }
        }

        private void tscCurrentDatabase_KeyUp(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.Return )
            {
                PopulateDatabaseList(tscCurrentDatabase.Text);
                //tscCurrentDatabase.SelectedItem = tscCurrentDatabase.Text;
                e.Handled = true;
            }

        }

        private void linkLabelImport_LocationChanged(object sender, EventArgs e)
        {

        }

        private void linkLabel2_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Globals.LuanchPowerBI();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            Globals.LuanchPowerBI();
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void toolbarMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void llData_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }
    }

    /// <summary>
    /// Used to sort an array of FileInfo objects by filename.
    /// </summary>
    class CompareFileInfoNames : IComparer
    {
        public int Compare(object x, object y)
        {
            FileInfo firstFile = (FileInfo)x;
            FileInfo secondFile = (FileInfo)y;
            return String.Compare(firstFile.FullName, secondFile.FullName);
        }
    }

    /// <summary>
    /// Used to sort an array of ReportParameterInfo objects in descending order according to the length of the parameter name string. 
    /// </summary>
    class CompareXmlNodeNameAttrLength : IComparer
    {
        public int Compare(object x, object y)
        {
            XmlNode firstXmlNode = (XmlNode)x;
            XmlNode secondXmlNode = (XmlNode)y;
            return (-1) * (firstXmlNode.Attributes["Name"].Value.Length.CompareTo(secondXmlNode.Attributes["Name"].Value.Length));
        }
    }

}