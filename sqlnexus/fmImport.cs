using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
//using Microsoft.SqlServer.Management;
//using Microsoft.SqlServer.Management.Smo;
using System.Data.SqlClient;
using NexusInterfaces;
using System.Text.RegularExpressions;
namespace sqlnexus
{
    public partial class fmImport : Form
    {
        private List<ToolStripMenuItem> m_OptionList = new List<ToolStripMenuItem>();
        private SqlInstances instances;
        private fmImport()
        {
            InitializeComponent();
        }
        private fmNexus MainForm;  //Cache an instance of this for logging to the log file and other stuff
        public fmImport(fmNexus mainform)
        {
            InitializeComponent();
            MainForm = mainform;
        }
        public static void ImportFiles(fmNexus mainform, string path)
        {

            fmImport fmi = new fmImport(mainform);
            fmi.cbPath.Text = path;

            if (Globals.QuietNonInteractiveMode == true)
            {
                fmi.EnumImporters();
            }
            else
            {
                fmi.Show(mainform);
            }



            fmi.DoImport();
            fmi.Close();
        }

        // private long TotalRowsInserted = 0;
        //private long TotalLinesProcessed = 0;
        INexusImporter ri = null;
        ProgressBar currBar = null;
        Label currLabel = null;



        private bool BlockPerfStatsSnapshot(string FileName)
        {
            String fileDir = Path.GetDirectoryName(FileName);
            String[] files = Directory.GetFiles(fileDir, "*Perf_stats_snapshot_shutdown.out*");

            if (files.Length > 0 &&
                FileName.ToUpper().IndexOf("PERF_STATS_SNAPSHOT_STARTUP") >= 0)
                return true;
            else
                return false;


        }
        //fixing bug 2266 where it tries to add thousands of controls 
        //when the directory contain many text files (which are unrelated)
        //so add an exclusion mask
        private bool BlockFile(string FileName)
        {
            bool ToBlock = false;

            ToBlock = BlockPerfStatsSnapshot(FileName);



            if (FileName.ToUpper().IndexOf("SQLDMP") >= 0 ||  //dump log file
                    FileName.ToUpper().IndexOf("SQLDUMP") >= 0 ||  //dump log file
                    FileName.ToUpper().IndexOf(@"##") >= 0 || // internal files. no need to import  
                    FileName.IndexOf("SQL_Base_Errorlog") >= 0
                )

            {
                return true;
            }
            return ToBlock;

        }
        private void AddFiles(string Mask, INexusImporter Importer)
        {
            string[] files2 = Directory.GetFiles(cbPath.Text.Trim().Replace("\"", ""), Mask);

            //if no file found for this mask, just return
            if (files2.Length <= 0)
            {
                return;
            }
            int i = tlpFiles.RowCount - 1;


            if (Importer is INexusFileImporter)
            {
                string[] files = Directory.GetFiles(cbPath.Text.Trim().Replace("\"", ""), Mask);
                int blockedCounter = 0;
                foreach (string f in files)
                {


                    //handle multiple instances
                    //block all text files that are from excluded instances
                    if (BlockFile(f) || instances.Block(f))
                    {
                        blockedCounter++;
                        continue;
                    }

                    AddFileRow(i, Path.GetFileName(f), Importer, "");
                    i++;

                }
                MainForm.LogMessage("Number of files blocked for import (due to multiple instance or unrelated files such as sqldump*: " + blockedCounter, MessageOptions.Silent);
            }
            else
            {
                if (0 != Directory.GetFiles(cbPath.Text.Trim().Replace("\"", ""), Mask).Length)  //Only add the mask if matching files are found
                {
                    //need special handling read trace for multiple instances
                    //when multiple instances files are caputred, only provide the one instnance selected.
                    if (Importer.Name.ToUpper().IndexOf("READTRACE") >= 0 && instances.Count > 1)
                    {
                        if (Mask.ToUpper().Contains("XEL"))
                        {
                            AddFileRow(i, instances.SelectedXEventFileMask, Importer, "");
                        }
                        else
                        {
                            AddFileRow(i, instances.SelectedTraceFileMask, Importer, "");
                        }



                    }
                    else
                    {
                        AddFileRow(i, Mask, Importer, "");
                    }

                }
            }
        }

        private void AddFileRow(int row, string labelText, INexusImporter Importer, string RowType)
        {
            tlpFiles.RowCount += 1;

            //if RowType parameter is blank, then we must have a valid Importer

            //first column - files processed
            Label lab1 = new Label();
            if (Importer == null)
            {
                lab1.Name = RowType; //used for showing progress in non-file import scenarios, like running T-SQL
            }
            else
            {
                lab1.Name = "FileNameLabel";
            }

            lab1.AutoSize = true;
            tlpFiles.Controls.Add(lab1, 0, row);
            lab1.Text = labelText; //+"(" + Importer.Name + ")";
            lab1.Anchor = AnchorStyles.Left;
            lab1.Location = new Point(0, 3);
            if (Importer != null)
                lab1.Tag = Importer;


            //second column - progress bar
            ProgressBar pb = new ProgressBar();
            tlpFiles.Controls.Add(pb, 1, row);
            pb.Height = 13;
            pb.MarqueeAnimationSpeed = 25;


            //third column - lines processed. starts blank and filled dynamically as files are processed
            Label lab2 = new Label();
            lab2.AutoSize = true;
            tlpFiles.Controls.Add(lab2, 2, row);
            lab2.Text = "";
            lab2.Anchor = AnchorStyles.Left;
            lab2.Location = new Point(0, 3);
        }


        int ticks = Environment.TickCount;

        private delegate void UpdateProgressDelegate(object[] args);
        private void UpdateProgress(object[] args)
        {
            if (args[0] is INexusFileImporter)
            {
                INexusFileImporter rif = (args[0] as INexusFileImporter);
                int PctComplete;
                // Make sure we don't divide by 0 if someone points us at a 0 byte file. 
                if (rif.FileSize > 0)
                    PctComplete = (int)(((decimal)rif.CurrentPosition) / rif.FileSize * 100);
                else PctComplete = 0;

                if (PctComplete > 100)
                    PctComplete = 100;

                currBar.Value = PctComplete;
            }
            else
            {
                UpdateStatus(args);
            }
            if (null != currLabel)
            {
                INexusImporter ri = (args[0] as INexusImporter);
                currLabel.Text = string.Format("Importing: {0} lines processed; {1} rows inserted...", ri.TotalLinesProcessed, ri.TotalRowsInserted);
            }
            Application.DoEvents();
        }
        private UpdateProgressDelegate updateProgress;

        private void ImportProgressChanged(object sender, EventArgs e)
        {
            if ((Environment.TickCount - ticks) > 200)
            {

                ticks = Environment.TickCount;
                if (this.InvokeRequired)
                {
                    this.Invoke(updateProgress, new object[] { sender });
                }
                else
                {
                    UpdateProgress(new object[] { sender });
                }
            }
        }

        private delegate void UpdateStatusDelegate(object[] args);
        private void UpdateStatus(object[] args)
        {
            INexusImporter ri = (args[0] as INexusImporter);
            // Check for changes in import state and report them
            string msg = "";
            switch (ri.State)
            {
                case ImportState.Canceling:
                    msg = "Canceling import...";
                    break;
                case ImportState.CreatingDatabase:
                    msg = "Creating and sizing database...";
                    break;
                case ImportState.Idle:
                    msg = "Idle.";
                    break;
                case ImportState.Importing:
                    msg = "Importing...";
                    break;
                case ImportState.OpeningFile:
                    msg = "Opening file...";
                    break;
                case ImportState.OpeningDatabaseConnection:
                    msg = "Opening database connection...";
                    break;
            }
            currLabel.Text = "(" + ri.Name + ")" + msg;
            Application.DoEvents();
        }
        private UpdateStatusDelegate updateStatus;


        private void ImportStatusChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(updateStatus, new object[] { sender });
            }
            else
            {
                UpdateStatus(new object[] { sender });
            }
        }

        private void fmImport_Load(object sender, EventArgs e)
        {
            //            fb_Path.SelectedPath = Application.StartupPath;
            if (Globals.PathsToImport.Count > 0)
            {
                //currently do nothing.  basically this is for silent import
                //will have to thing stuff a bit more
            }
            else
            {
                this.cbPath.Text = sqlnexus.Properties.Settings.Default.ImportPath;
            }

            this.Left -= 100;

            updateProgress = new UpdateProgressDelegate(this.UpdateProgress);
            updateStatus = new UpdateStatusDelegate(this.UpdateStatus);
            EnumImporters();
        }



        private void llTemplate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (null == ((sender as LinkLabel).Tag))
                return;
            INexusImporter ri = ((sender as LinkLabel).Tag as INexusImporter);
            frmImportSummary fmIS = new frmImportSummary(ri);
            fmIS.ShowDialog(this);
        }

        private bool CheckAndStop()
        {
            if ("Stop" == tsbGo.Text)
            {
                if (null != ri)
                {
                    ri.Cancel();
                }
                return true;
            }
            return false;
        }

        public static bool loadIfcFilter(Type typeObj, Object criteriaObj)
        {
            if (typeObj.ToString() == criteriaObj.ToString())
                return true;
            else
                return false;
        }

        public void EnumImporters()
        {
            tsiImporters.DropDownItems.Clear();
            EnumImportersFromDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\SqlNexus\Importers");
            EnumImportersFromDirectory(Application.StartupPath);
        }
        private String FileVersions(String file)
        {
            String s = System.Diagnostics.FileVersionInfo.GetVersionInfo(file).FileVersion;
            FileInfo fi = new FileInfo(file);
            s += " " + fi.CreationTime.ToLongDateString();
            return s;
        }


        //order the importer to do rowset first
        private List<string> OrderedImporterFiles(string[] files)
        {
            Dictionary<Int32, string> ImporterList = new Dictionary<Int32, string>();


            foreach (string file in files)
            {
                if (file.ToUpper().Contains("PERFMONIMPORTER"))
                {
                    ImporterList.Add(300, file);
                }
                else if (file.ToUpper().Contains("ROWSETIMPORTENGINE"))
                {
                    ImporterList.Add(100, file);
                }
                else if (file.ToUpper().Contains("READTRACE"))
                {
                    ImporterList.Add(200, file);

                }
                else
                {
                    ImporterList.Add(file.GetHashCode(), file);
                }

            }


            List<Int32> OrderedList = new List<Int32>();

            foreach (Int32 key in ImporterList.Keys)
            {
                OrderedList.Add(key);
            }

            List<string> OrderedImporters = new List<string>();
            OrderedList.Sort();
            foreach (Int32 key in OrderedList)
            {

                OrderedImporters.Add(ImporterList[key]);


            }



            return OrderedImporters;
        }
        private void EnumImportersFromDirectory(string importerDirectory)
        {
            if (!Directory.Exists(importerDirectory))
                return;
            string[] Files = Directory.GetFiles(importerDirectory, "*.DLL");

            List<string> OrderedFiles = OrderedImporterFiles(Files);

            foreach (string file in OrderedFiles)
            {
                //we know this is a native image
                MainForm.LogMessage("detecting importer file " + file + " version and creation date " + FileVersions(file));

                if (Path.GetFileName(file).ToUpper() == "BulkLoad.dll".ToUpper())
                {
                    MainForm.LogMessage(String.Format(Properties.Resources.Msg_NativeImage, file));
                    continue;
                }
                Assembly Assem;
                try
                {
                    Assem = Assembly.LoadFile(file);


                }
                catch (Exception ex)
                {
                    MainForm.LogMessage("Assembly " + file + " could not be used as an importer: " + ex.Message, MessageOptions.Silent);
                    continue;
                }

                Type[] typs = Assem.GetExportedTypes();
                foreach (Type typ in typs)
                {
                    //Ignore abstract classes
                    if (typ.IsAbstract)
                        continue;

                    //Ignore non-classes
                    if (!typ.IsClass)
                        continue;

                    TypeFilter loadIfcFilt = new TypeFilter(loadIfcFilter);
                    Type[] ifcs = typ.FindInterfaces(loadIfcFilt, "NexusInterfaces.INexusImporter");
                    foreach (Type ifc in ifcs)
                    {

                        //If we get in here, the Class implements the interface, so add it to the list
                        //and bail
                        INexusImporter prod = (INexusImporter)Assem.CreateInstance(typ.FullName, true);

                        prod.StatusChanged += new System.EventHandler(this.ImportStatusChanged);
                        if (prod is INexusProgressReporter)
                        {
                            (prod as INexusProgressReporter).ProgressChanged += new System.EventHandler(this.ImportProgressChanged);
                        }

                        ToolStripMenuItem tsi = new ToolStripMenuItem(prod.Name);
                        tsi.Tag = prod;
                        tsiImporters.DropDownItems.Add(tsi);

                        // Add subitems

                        // Enabled item
                        // All importers require an "Enabled" menu option -- if this doesn't exist in the options collection, add it. 
                        object enabledoption;
                        if (!prod.Options.TryGetValue("Enabled", out enabledoption))
                        {
                            prod.Options.Add("Enabled", true);
                        }

                        // options
                        ToolStripMenuItem subtsi;
                        foreach (string option in prod.Options.Keys)
                        {
                            subtsi = new ToolStripMenuItem(option);
                            if (option.Substring(option.Length - 3) == "...") // dialog
                            {
                                subtsi.Tag = prod.OptionsDialog;
                                subtsi.Click += new System.EventHandler(this.tsiDialog_Click);
                            }
                            else // boolean
                            {
                                m_OptionList.Add(subtsi);

                                subtsi.Tag = prod;
                                subtsi.CheckOnClick = true;

                                bool UserSaved = ImportOptions.IsEnabled(String.Format("{0}.{1}", prod.Name, subtsi.Text));
                                MainForm.LogMessage("load: " + String.Format("{0}.{1}", prod.Name, option), MessageOptions.Silent);

                                if (ImportOptions.IsEnabled("SaveImportOptions"))
                                    subtsi.Checked = UserSaved;
                                else
                                    subtsi.Checked = (bool)prod.Options[option];

                                subtsi.Click += new System.EventHandler(this.tsiBool_Click);
                            }

                            tsi.DropDownItems.Add(subtsi);
                        }
                    }
                }
            }
        }


        private void EnumFiles()
        {
            tlpFiles.Visible = false;
            Application.DoEvents();


            string[] XEFiles = Directory.GetFiles(cbPath.Text.Trim().Replace("\"", ""), "*pssdiag*.xel");
            string[] trcFiles = Directory.GetFiles(cbPath.Text.Trim().Replace("\"", ""), "*sp_trace*.trc");

            if (XEFiles.Length > 0 && trcFiles.Length > 0)
            {
                Util.Logger.LogMessage("You have captured both trace and xeven files. import will fail! Please remove one of them before importing", MessageOptions.All);
            }


            tlpFiles.RowCount = 1;
            tlpFiles.Controls.Clear();

            INexusImporter prod;
            // For each importer listed in the Options menu on the import form
            foreach (ToolStripMenuItem tsi in tsiImporters.DropDownItems)
            {
                prod = (tsi.Tag as INexusImporter);
                // See whether this importer is enabled (each importer should have an "Enabled" option)
                bool Enabled = true;
                foreach (ToolStripMenuItem tsi2 in tsi.DropDownItems)
                {
                    if ("Enabled" == tsi2.Text)
                    {
                        Enabled = tsi2.Checked;
                        break;
                    }
                }

                //this is used for silent ude import which is controlled by AppConfig.xml
                //we will override user options regardless
                if (Globals.QuietNonInteractiveMode == true)
                {
                    FileMgr mgr = new FileMgr();
                    Importer imp = mgr[prod.Name];
                    if (imp != null)
                    {
                        Enabled = imp.ude;
                        Util.Logger.LogMessage("Silent option; importer " + imp.Name + " enabled = " + imp.ude);
                    }

                }


                if (Enabled)
                {
                    foreach (string s in prod.SupportedMasks)
                    {
                        AddFiles(s, prod);
                    }
                }
            }
            tlpFiles.Visible = true;
            Application.DoEvents();
        }

        Dictionary<string, string[]> PostScripts = new Dictionary<string, string[]>();

        public bool KeepPriorNonEmptyDb()
        {
            NexusInfo nInfo = new NexusInfo(Globals.credentialMgr.ConnectionString, this.MainForm);

            if (Globals.ConsoleMode)
            {
                if (nInfo.HasNexusInfo() && !Globals.DropExistingDb)
                {
                    MainForm.LogMessage(String.Format("Database '{0}' already contains Nexus data. Please choose or create a different database for a fresh data load", (Globals.credentialMgr.Database != null) ? Globals.credentialMgr.Database : " "), MessageOptions.All);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //db has been imported into before and user did not request a drop db
                if (nInfo.HasNexusInfo() && (!tsiDropDBBeforeImporting.Checked && !ImportOptions.IsEnabled("DropDbBeforeImporting")))
                {
                    MainForm.LogMessage(String.Format("Database '{0}' already contains Nexus data. Please choose or create a different database for a fresh data load", (Globals.credentialMgr.Database != null) ? Globals.credentialMgr.Database : " "), MessageOptions.All);
                    return true;
                }
                else
                {
                    return false;
                }
            }

        }

        private void tsbGo_Click(object sender, EventArgs e)
        {

            if (true == KeepPriorNonEmptyDb())
            {
                this.Visible = false;
                this.Dispose();
                MainForm.BringToFront();
                return;
            }

            btnClose.Visible = false;
            DoImport();
            btnClose.Visible = true;
        }

        private void DoImport()
        {
            if (CheckAndStop())
                return;
            if (!tlpFiles.Visible)
            {
                this.Left = 400;
                this.Top = 200;
                this.Height = 650;
                this.Width = 1100;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                tlpFiles.Visible = true;
                ssStatus.Visible = true;
            }

            MainForm.LogMessage("Starting import...");


            if (tsiDropDBBeforeImporting.Checked == true || ImportOptions.IsEnabled("DropDbBeforeImporting") || Globals.DropExistingDb == true)
            {

                if (Globals.ConsoleMode == false)
                {
                    DialogResult dr = MainForm.LogMessage(String.Format(Properties.Resources.Warning_ToDropDB, Globals.credentialMgr.Database), "Danger", MessageBoxButtons.YesNo);
                    MainForm.LogMessage("dialog result = " + dr.ToString());
                    if (dr == DialogResult.No)
                    {
                        return;
                    }
                    else if (dr == DialogResult.Yes && Globals.credentialMgr.Database.ToLower() != "sqlnexus")
                    {

                        DialogResult reconfirm = MainForm.LogMessage(String.Format("Are you sure you really want to drop database {0}?", Globals.credentialMgr.Database), "Danger", MessageBoxButtons.YesNo);
                        if (DialogResult.No == reconfirm)
                            return;

                    }
                }

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.ConnectionString = Globals.credentialMgr.ConnectionString;
                builder.InitialCatalog = "master";
                SqlConnection conn = new SqlConnection(builder.ConnectionString);
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = String.Format(Properties.Resources.CreateDropDB, Globals.credentialMgr.Database);
                try
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    MainForm.LogMessage("Dropped and created a new database: " + Globals.credentialMgr.Database);
                }
                catch (SqlException sqlex)
                {
                    MainForm.LogMessage("Create Db failed with exception " + sqlex.Message, MessageOptions.Dialog);
                    return;
                }
                finally
                {
                    conn.Close();
                }


            }

            RunScript("SqlNexus_PreProcessing.sql");
            MainForm.LogMessage("Adding Nexus Importer Version" + Globals.credentialMgr.ConnectionString);
            NexusInfo nInfo = new NexusInfo(Globals.credentialMgr.ConnectionString, this.MainForm);
            nInfo.SetAttribute("Nexus Importer Version", Application.ProductVersion);



            int startTicks = Environment.TickCount;
            MainForm.StartStopSpinner(true, MainForm.spnImporter);

            //Switch button/modes to Stop mode
            EnableButtons(false);
            tsbGo.Image = sqlnexus.Properties.Resources.RecordHS1;
            tsbGo.Text = "Stop";
            Application.DoEvents();

            // Close any open reports based on the data that we're about to overwrite
            fmNexus.singleton.CloseAll();

            string srcPath = Path.GetFullPath(cbPath.Text.Trim());
            /*
            string pathFromCmd = Globals.PathsToImport.Dequeue();
            if (pathFromCmd != null)
                srcPath = pathFromCmd;
            else
              srcPath=  Path.GetFullPath(cbPath.Text);
            */

            sqlnexus.Properties.Settings.Default.ImportPath = srcPath;
            if (srcPath[srcPath.Length - 1] != '\\')
                srcPath += '\\';

            //find the instance name by locating it inside ##SQLDIAG.LOG
            instances = new SqlInstances(srcPath);

            if (instances.Count > 1 && Globals.QuietNonInteractiveMode)
            {
                //get first instance for quiet mode (list already sorted;
                MainForm.LogMessage("Quiet mode. Silently selecting the first instance found in a sorted list. Instance name: " + instances.InstanceList()[0], MessageOptions.Silent);
                instances.InstanceToImport = instances.InstanceList()[0];
            }
            else if (instances.Count > 1)
            {
                fmSelectInstance chooseInstancefm = new fmSelectInstance();
                chooseInstancefm.Tag = instances;
                chooseInstancefm.ShowDialog();
            }

            DiagConfig config = new DiagConfig(srcPath);

            nInfo.SetAttribute("SQLVersion", config.SQLVersion);

            //enumerate the files to process and add them to list for processing
            EnumFiles();

            //add individual rows for each of these so they show up as progress bars in the summary window listview
            string rawFileImprtStr = "RawFileImport";
            AddFileRow((tlpFiles.RowCount - 1), "Raw file import", null, rawFileImprtStr);

            string postProcessStr = "PostProcess";
            AddFileRow((tlpFiles.RowCount - 1), "Post-Import processing ", null, postProcessStr);

            string perfStatsAnalysisStr = "PerfStatsAnalysis";
            AddFileRow((tlpFiles.RowCount - 1), "Running perfstats analysis", null, perfStatsAnalysisStr);

            string runtimeCountStr = "RuntimeCount";
            AddFileRow((tlpFiles.RowCount - 1), "Counting unique runtime snapshots", null, runtimeCountStr);

            string enumReportsStr = "EnumReports";
            AddFileRow((tlpFiles.RowCount - 1), "Enumerating reports", null, enumReportsStr);


            //AddLabel();
            bool RunScripts = true;
            bool Success = false;

            CustomXELImporter CI = new CustomXELImporter();
            CI.SQLBaseImport(Globals.credentialMgr.ConnectionString, Globals.credentialMgr.Server,
                                                    Globals.credentialMgr.WindowsAuth,
                                                    Globals.credentialMgr.User,
                                                    Globals.credentialMgr.Password,
                                                    Globals.credentialMgr.Database, srcPath);



            try
            {
                int j = 0;

                for (int i = 0; i < tlpFiles.Controls.Count; i++)
                {
                    //if (tlpFiles.Controls[i] is LinkLabel)
                    if (tlpFiles.Controls[i] is Label && tlpFiles.Controls[i].Name == "FileNameLabel")
                    {
                        System.Diagnostics.Debug.Assert((null != tlpFiles.Controls[i + 1]) && (null != tlpFiles.Controls[i + 2]));
                        //LinkLabel ll = (LinkLabel)tlpFiles.Controls[i];
                        Label ll = (Label)tlpFiles.Controls[i];
                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 0;
                        currLabel = (Label)tlpFiles.Controls[i + 2];

                        int ticks = Environment.TickCount;

                        ri = (ll.Tag as INexusImporter);
                        MainForm.LogMessage(ri.Name + " is a INexusImporter");

                        try
                        {
                            ri.Initialize(srcPath + (tlpFiles.Controls[i] as /*LinkLabel*/ Label).Text,
                                                    Globals.credentialMgr.ConnectionString,
                                                    Globals.credentialMgr.Server,
                                                    Globals.credentialMgr.WindowsAuth,
                                                    Globals.credentialMgr.User,
                                                    Globals.credentialMgr.Password,
                                                    Globals.credentialMgr.Database,
                                                    MainForm);

                            //Run pre-scripts and cache post scripts for later use
                            if (!PostScripts.ContainsKey(ri.GetType().Name))
                            {
                                PostScripts.Add(ri.GetType().Name, ri.PostScripts);
                                foreach (string s in ri.PreScripts)
                                {
                                    RunScript(s);
                                }
                            }

                            if (!(ri is INexusFileSizeReporter))
                            {
                                currBar.Style = ProgressBarStyle.Marquee;
                            }

                            //Import the data
                            Success = ri.DoImport();



                            if (ri.Name.ToLower().Contains("rowset"))
                            {
                                RunPostScripts();
                            }
                            Globals.IsNexusCoreImporterSuccessful = true;
                            //ll.LinkBehavior = LinkBehavior.HoverUnderline;
                        }
                        catch (Exception ex)
                        {
                            if (ri.Name == "Rowset Importer")
                            {
                                Globals.IsNexusCoreImporterSuccessful = false;
                            }
                            Success = false;
                            Globals.HandleException(ex, this, MainForm);
                        }

                        currBar.Style = ProgressBarStyle.Blocks;
                        string msg;
                        msg = "(Importer:" + ri.Name + ") ";
                        if (ri.Cancelled)	// different msg if import was canceled.
                        {
                            RunScripts = false;
                            msg += "Cancelled. (" + (Environment.TickCount - ticks) / 1000 + " sec, ";
                            if (ri is INexusFileImporter)
                            {
                                msg += this.currBar.Value.ToString() + "% complete)";
                            }
                            else
                            {
                                msg += ri.TotalLinesProcessed + " rows inserted)";
                            }
                            MainForm.LogMessage(msg);
                            currLabel.Text = msg;
                            break;
                        }
                        else if (!Success)	// set summary msg if import failed
                        {
                            RunScripts = false;
                            msg += "Import failed. (" + (Environment.TickCount - ticks) / 1000 + " sec, ";
                            if (ri is INexusFileImporter)
                            {
                                msg += this.currBar.Value.ToString() + "% complete)";
                            }
                            else
                            {
                                msg += ri.TotalLinesProcessed + " rows inserted)";
                            }
                            MainForm.LogMessage(msg);
                            currLabel.Text = msg;
                        }
                        else					// different msg if success
                        {
                            currBar.Value = currBar.Maximum;

                            msg += "Done. (" + (Environment.TickCount - ticks) / 1000 + " sec";
                            if (ri is INexusFileImporter)
                            {
                                msg += ", " + ((ri as INexusFileImporter).FileSize / 1000 / 1000) + "MB), ";
                            }
                            else
                            {
                                msg += "), ";
                            }
                            msg += msg = string.Format("{0} lines processed; {1} rows inserted.", ri.TotalLinesProcessed, ri.TotalRowsInserted);
                            MainForm.LogMessage(msg);
                            currLabel.Text = msg;
                        }
                        ri = null;
                        j++;

                    } //end of if (Name == "FileNameLabel")

                    else if (tlpFiles.Controls[i].Name == rawFileImprtStr)
                    {
                        int rawfileStartTicks = Environment.TickCount;

                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 20;

                        currLabel = (Label)tlpFiles.Controls[i + 2];
                        currLabel.Text = "Please wait for raw file import to complete...";


                        //raw file importer
                        MainForm.LogMessage("RawFileImporter starting");
                        Application.DoEvents();

                        RawFileImporter rawfileimporter = new RawFileImporter(Globals.credentialMgr.Server, Globals.credentialMgr.Database, srcPath);

                        //do the raw file import
                        string statusStr = rawfileimporter.DoImport();

                        currBar.Value = 100;
                        MainForm.LogMessage("RawFileImporter completed");

                        string rawfileMsg = "(Importer:" + rawFileImprtStr + ") " + "Done. (" + (Environment.TickCount - rawfileStartTicks) / 1000 + " sec), " + statusStr + ".";
                        currLabel.Text = rawfileMsg;
                        Application.DoEvents();
                    }

                    else if (tlpFiles.Controls[i].Name == postProcessStr)
                    {
                        Application.DoEvents();
                        //run Perfstats Analysis script just once
                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 20;

                        currLabel = (Label)tlpFiles.Controls[i + 2];
                        currLabel.Text = "Please wait for post-import process step to complete...";

                        MainForm.LogMessage("Running Post-Import processing...");
                        Application.DoEvents();

                        //run Post-processing
                        RunPostProcessing(srcPath);

                        currBar.Value = 100;
                        currLabel.Text = "(Post-import Processing) Done.";
                        MainForm.LogMessage("End of Post-Import processing");

                        Application.DoEvents();
                    }

                    else if (tlpFiles.Controls[i].Name == perfStatsAnalysisStr)
                    {
                        Application.DoEvents();
                        //run Perfstats Analysis script just once
                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 20;

                        currLabel = (Label)tlpFiles.Controls[i + 2];
                        currLabel.Text = "Please wait for PerfStats analysis step to complete...";

                        MainForm.LogMessage("Running Perfstats Analysis");
                        Application.DoEvents();

                        //do the analysis
                        RunScript("PerfStatsAnalysis.sql");

                        currBar.Value = 100;
                        currLabel.Text = "(PerfStatsAnalysis) Done.";
                        MainForm.LogMessage("End of Perfstats Analysis");

                        Application.DoEvents();
                    }

                    else if (tlpFiles.Controls[i].Name == runtimeCountStr)
                    {
                        Application.DoEvents();
                        int runtimeStartTicks = Environment.TickCount;
                        //run Perfstats Analysis script just once
                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 20;

                        currLabel = (Label)tlpFiles.Controls[i + 2];
                        currLabel.Text = "Please wait for this step to complete...";

                        MainForm.LogMessage("Running count of runtimes captured in the data");
                        Application.DoEvents();

                        //do the runtime count
                        string runtimesRet = RuntimeCount(startTicks);

                        currBar.Value = 100;

                        string runtimeMsg = "(" + runtimeCountStr + ") " + "Done. (" + (Environment.TickCount - runtimeStartTicks) / 1000 + " sec), " + runtimesRet + ".";
                        currLabel.Text = runtimeMsg;
                        MainForm.LogMessage("End of counting runtimes");

                        Application.DoEvents();
                    }

                    else if (tlpFiles.Controls[i].Name == enumReportsStr)
                    {
                        int enumReportsStartTicks = Environment.TickCount;
                        //run Perfstats Analysis script just once
                        currBar = (ProgressBar)tlpFiles.Controls[i + 1];
                        currBar.Value = 10;

                        currLabel = (Label)tlpFiles.Controls[i + 2];
                        currLabel.Text = "Please wait reports enumeration step to complete...";

                        MainForm.LogMessage("Enumerating reports");
                        Application.DoEvents();


                        //Refresh reports list in case provider changed it
                        MainForm.EnumReports();

                        currBar.Value = 100;

                        string runtimeMsg = "(" + enumReportsStr + ") " + "Done. (" + (Environment.TickCount - enumReportsStartTicks) / 1000 + " sec). Import Complete!";
                        currLabel.Text = runtimeMsg;
                        MainForm.LogMessage("End of report enumeration");

                        Application.DoEvents();

                    }

                } //end of for loop

            }//try block

            catch (Exception ex)
            {
                MainForm.LogMessage("Import failed.");
                Globals.HandleException(ex, this, MainForm);
            }
            finally
            {
                MainForm.StartStopSpinner(false, MainForm.spnImporter);
                tsbGo.Image = sqlnexus.Properties.Resources.PlayHS1;
                tsbGo.Text = "Import";
                EnableButtons(true);
                this.Cursor = Cursors.Default;
                Application.DoEvents();

                if (Globals.QuietNonInteractiveMode == true)
                {
                    Application.Exit();
                }
            }
        }

        private void RunPostProcessing(string sourcePath)
        {

            //post-process execution (call PostProcess.cmd)
            StringBuilder output = new StringBuilder();

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardInput = true;
            psi.UseShellExecute = false;
            psi.Arguments = string.Format("{0} {1} \"{2}\"", Globals.credentialMgr.Server, Globals.credentialMgr.Database, sourcePath);
            MainForm.LogMessage("PostProcess argument " + psi.Arguments);
            psi.FileName = "PostProcess.cmd";

            Process process = new Process();
            process.StartInfo = psi;

            process.EnableRaisingEvents = true;

            process.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate (object sender, DataReceivedEventArgs e)
                {
                    output.Append(e.Data);
                }
            );
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
            process.CancelOutputRead();
        }


        private string RuntimeCount(int initialTicks)
        {
            string retString = "";
            string lessThanFive = "The data was captured for a very short period of time.  Some reports may fail";

            MainForm.LogMessage(String.Format("Import complete. Total import time: {0} seconds", (Environment.TickCount - initialTicks) / 1000));

            SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString);
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "select count (distinct runtime) 'NumberOfRuntimes' from tbl_PERF_STATS_SCRIPT_RUNTIMES";
            conn.Open();
            Int32 NumberOfRuntimes = (Int32)cmd.ExecuteScalar();

            conn.Close();

            if (NumberOfRuntimes < 5)
            {
                MainForm.LogMessage(lessThanFive, MessageOptions.Both);
                retString = lessThanFive;
            }
            else
            {
                retString = NumberOfRuntimes.ToString() + " runtimes found.";
            }

            return retString;

        }

        private void RunPostScripts()
        {
            MainForm.LogMessage("Executing post-mortem analysis scripts...");
            //RunScript(Application.StartupPath + @"\PerfStatsAnalysis.sql");
            //RunScript(Application.StartupPath + @"\TraceAnalysis.sql");

            //nothing to run
            if (null == PostScripts || PostScripts.Count <= 0)
                return;

            foreach (string[] scripts in PostScripts.Values)
            {
                if (scripts == null || scripts.Length <= 0)
                    continue;
                foreach (string script in scripts)
                {
                    if (string.IsNullOrEmpty(script))
                        continue; //nothign to run
                    RunScript(script);
                }
            }

            MainForm.LogMessage("Execution of post-mortem analysis scripts complete.");
        }

        private void RunScript(string scriptname)
        {
            //if nothing to run, return
            if (string.IsNullOrEmpty(scriptname))
                return;

            Cursor saveCur = this.Cursor;
            string FullScriptName;
            try
            {
                this.Cursor = Cursors.WaitCursor;
                Application.DoEvents();

                MainForm.LogMessage("Db name = '" + Globals.credentialMgr.Database + "'");
                MainForm.LogMessage(string.Format("Executing {0} ...", scriptname, Globals.credentialMgr.Database));

                // Server srv = new Server(Globals.Server);
                //Database db = srv.Databases[Globals.Database];


                if (-1 == scriptname.IndexOf('\\'))
                {
                    FullScriptName = Application.StartupPath + "\\" + scriptname;
                    if (!File.Exists(FullScriptName))
                    {
                        FullScriptName = Application.StartupPath + @"\Reports\" + scriptname;
                        if (!File.Exists(FullScriptName))
                        {
                            FullScriptName = Globals.AppDataPath + "\\" + scriptname;
                            if (!File.Exists(FullScriptName))
                            {

                                FullScriptName = Globals.AppDataPath + "\\reports\\" + scriptname;
                            }
                        }
                    }
                }
                else
                    FullScriptName = scriptname;

                if (!File.Exists(FullScriptName))
                {
                    MainForm.LogMessage("Script '" + FullScriptName + "' doesn't exist", MessageOptions.All);
                    return;

                }

                //db.ExecuteNonQuery(File.ReadAllText(FullScriptName), Microsoft.SqlServer.Management.Common.ExecutionTypes.ContinueOnError);
                CSql mysql = new CSql(Globals.credentialMgr.ConnectionString);
                //MainForm.LogMessage("Connection string inside execute script " + Globals.credentialMgr.ConnectionString);
                mysql.ExecuteSqlScript(File.ReadAllText(FullScriptName));
                MainForm.LogMessage("full script " + FullScriptName);
                MainForm.LogMessage(string.Format("Execution of {0} complete.", FullScriptName));
            }
            finally
            {
                this.Cursor = saveCur;
            }
        }

        private void EnableButtons(bool enable)
        {
            laInstructions.Enabled = enable;
            laPath.Enabled = enable;
            cbPath.Enabled = enable;
            btPath.Enabled = enable;
            llOptions.Enabled = enable;
        }

        private void tbPath_TextChanged(object sender, EventArgs e)
        {
            tsbGo.Enabled = Directory.Exists(cbPath.Text);
        }

        private void tsbPath_Click(object sender, EventArgs e)
        {
            if (0 != cbPath.Text.Length)
            {
                fb_Path.SelectedPath = cbPath.Text;
            }
            if (DialogResult.OK == fb_Path.ShowDialog())
            {
                cbPath.Text = fb_Path.SelectedPath;
            }
        }

        private void llOptions_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Point p = new Point(this.Location.X + llOptions.Location.X, this.Location.Y + llOptions.Location.Y + llOptions.Height + 25);
            cmOptions.Show(p);
        }

        private void fmImport_FormClosing(object sender, FormClosingEventArgs e)
        {
            CheckAndStop();

        }


        private void tsiSaveOptions_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem tsi = (sender as ToolStripMenuItem);
            ImportOptions.Set("SaveImportOptions", tsi.Checked);

            StringBuilder sb = new StringBuilder();

            if (tsi.Checked)
            {

                foreach (ToolStripMenuItem tsi_ImporterMenu in cmOptions.Items)
                {
                    if (tsi_ImporterMenu.Text == "Importers")
                    {
                        foreach (ToolStripMenuItem tsi_ProductMenu in tsi_ImporterMenu.DropDownItems)
                        {
                            foreach (ToolStripMenuItem tsi_IndividualOptionMenu in tsi_ProductMenu.DropDownItems)
                            {
                                INexusImporter prod = (INexusImporter)tsi_IndividualOptionMenu.Tag;
                                String OptionName = String.Format("{0}.{1}", prod.Name, tsi_IndividualOptionMenu.Text);
                                ImportOptions.Set(OptionName, tsi_IndividualOptionMenu.Checked);

                            }
                        }

                    }
                }

                ImportOptions.Set("DropDbBeforeImporting", tsiDropDBBeforeImporting.Checked);
            }
            else
                ImportOptions.Clear();


        }
        private void tsiBool_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem tsi = (sender as ToolStripMenuItem);
            INexusImporter prod = (INexusImporter)tsi.Tag;
            prod.Options[tsi.Text] = tsi.Checked;
            if (ImportOptions.IsEnabled("SaveImportOptions"))
            {

                ImportOptions.Set(string.Format("{0}.{1}", prod.Name, tsi.Text), tsi.Checked);
                //LogMessage("strip: " + string.Format("{0}.{1}", prod.Name, tsi.Name), MessageOptions.Silent);

            }

        }

        private void tsiDialog_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem tsi = (sender as ToolStripMenuItem);
            Form frm = (Form)tsi.Tag;
            frm.ShowDialog(this);
        }

        private void tsiUseDefaultOptions_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem tsi = (ToolStripMenuItem)sender;
            ImportOptions.Clear();
            tsiDropDBBeforeImporting.Checked = false;
            tsiSaveOptions.Checked = false;
            EnumImporters();

        }
        private void tsiDropDBBeforeImporting_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem tsi = (ToolStripMenuItem)sender;

            //moved warning import
            /*if (tsi.Checked)
            {
                DialogResult dr = MainForm.LogMessage(Properties.Resources.Warning_ToEnableDropDB, "Danger", MessageBoxButtons.YesNo);
                MainForm.LogMessage("dialog result =" + dr.ToString());
                if (dr == DialogResult.No)
                {
                    tsi.Checked = false;
                }

            }*/
            if (ImportOptions.IsEnabled("SaveImportOptions"))
                ImportOptions.Set("DropDbBeforeImporting", tsi.Checked);


        }
        private void cmOptions_Opening(object sender, CancelEventArgs e)
        {
            tsiSaveOptions.Click += new EventHandler(tsiSaveOptions_Click);
            tsiUseDefaultOptions.Click += new EventHandler(tsiUseDefaultOptions_Click);
            tsiDropDBBeforeImporting.CheckedChanged += new EventHandler(tsiDropDBBeforeImporting_Click);
            tsiSaveOptions.Checked = ImportOptions.IsEnabled("SaveImportOptions");
            if (ImportOptions.IsEnabled("SaveImportOptions"))
                tsiDropDBBeforeImporting.Checked = ImportOptions.IsEnabled("DropDbBeforeImporting");

        }


        private void paTop_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }

}
