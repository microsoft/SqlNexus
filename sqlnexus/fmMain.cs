using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using SQLAnalyzerInterfaces;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Data.SqlClient;
using System.Xml;

namespace SQLAnalyzer
{
	/// <summary>
	/// Summary description for fmMain.
	/// </summary>
	public class fmMain : System.Windows.Forms.Form, IShowProgress
	{
		private System.Windows.Forms.ImageList imGlyphs;
		private System.Windows.Forms.Panel paTop;
		private System.Windows.Forms.Panel paBottom;
		private System.Windows.Forms.FolderBrowserDialog fb_Path;
		private System.Windows.Forms.MenuItem miSelectAll;
		private System.Windows.Forms.MenuItem miSelectNone;
		private System.Windows.Forms.MenuItem miViewNone;
		private System.Windows.Forms.ContextMenu cmTask;
		private System.Windows.Forms.MenuItem menuItem1;
		private System.Windows.Forms.Panel paClient;
		private System.Windows.Forms.Button btNext;
		private System.Windows.Forms.Button btPrev;
		private System.Windows.Forms.GroupBox gbAnalyzers;
		private System.Windows.Forms.ListView lvAnalyzers;
		private System.Windows.Forms.ColumnHeader acName;
		private System.Windows.Forms.ColumnHeader chAnlProgress;
		private System.Windows.Forms.ColumnHeader chAnlStart;
		private System.Windows.Forms.ColumnHeader chAnlEnd;
		private System.Windows.Forms.ColumnHeader chAnlDuration;
		private System.Windows.Forms.GroupBox gbProducers;
		private System.Windows.Forms.ListView lvProducers;
		private System.Windows.Forms.ColumnHeader lcName;
		private System.Windows.Forms.ColumnHeader lcProducer;
		private System.Windows.Forms.ColumnHeader chProgress;
		private System.Windows.Forms.ColumnHeader chProdStart;
		private System.Windows.Forms.ColumnHeader chProdEnd;
		private System.Windows.Forms.ColumnHeader chProdDuration;
		private System.Windows.Forms.GroupBox gbAuthentication;
		private System.Windows.Forms.TextBox edPassword;
		private System.Windows.Forms.Label laPassword;
		private System.Windows.Forms.TextBox edUser;
		private System.Windows.Forms.Label laUser;
		private System.Windows.Forms.RadioButton rbSS;
		private System.Windows.Forms.RadioButton rbWindows;
		private System.Windows.Forms.TextBox edDatabase;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox edServer;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button btPath;
		private System.Windows.Forms.Label laPath;
		private System.Windows.Forms.CheckBox ckOverwrite;
		private System.Windows.Forms.TabControl tcWizard;
		private System.Windows.Forms.TabPage tpConnect;
		private System.Windows.Forms.TabPage tpPath;
		private System.Windows.Forms.TabPage tpProducers;
		private System.Windows.Forms.TabPage tpAnalyzers;
		private System.Windows.Forms.Button btFinish;
		private System.Windows.Forms.TextBox edInputPath;
		private System.Windows.Forms.TabPage tpFinish;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.CheckBox ckCreateDB;
		private System.Windows.Forms.Panel paTopLeft;
		private System.Windows.Forms.ToolBar tbProducers;
		private System.Windows.Forms.ToolBarButton btUp;
		private System.Windows.Forms.ToolBarButton btDown;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.ToolBar tbAnalyzers;
		private System.Windows.Forms.ToolBarButton btUpAnl;
		private System.Windows.Forms.ToolBarButton btDownAnl;
		private System.Windows.Forms.ColumnHeader chAnalyzer;
		private System.Windows.Forms.ColumnHeader chAnlAssembly;
		private System.Windows.Forms.ColumnHeader chAssembly;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.CheckBox ckProcessAll;
		private System.Windows.Forms.Label laStartDate;
		private System.Windows.Forms.Label laEndDate;
		private System.Windows.Forms.DateTimePicker dpStartDate;
		private System.Windows.Forms.DateTimePicker dpEndDate;
		private System.Windows.Forms.ErrorProvider epEndDate;
		private System.Windows.Forms.ErrorProvider epPath;
		private System.ComponentModel.IContainer components;

		public fmMain()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
		}

		XmlDocument m_ConfigDoc=null;
		public fmMain(XmlDocument ConfigDoc)
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			if (null!=ConfigDoc["dsConfig"]["Analysis"])
				m_ConfigDoc=ConfigDoc;
		}
		public Form ContainingForm
		{
			get
			{
				return this;
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(fmMain));
			System.Configuration.AppSettingsReader configurationAppSettings = new System.Configuration.AppSettingsReader();
			this.imGlyphs = new System.Windows.Forms.ImageList(this.components);
			this.paTop = new System.Windows.Forms.Panel();
			this.cmTask = new System.Windows.Forms.ContextMenu();
			this.miSelectAll = new System.Windows.Forms.MenuItem();
			this.miSelectNone = new System.Windows.Forms.MenuItem();
			this.miViewNone = new System.Windows.Forms.MenuItem();
			this.menuItem1 = new System.Windows.Forms.MenuItem();
			this.paBottom = new System.Windows.Forms.Panel();
			this.btFinish = new System.Windows.Forms.Button();
			this.btPrev = new System.Windows.Forms.Button();
			this.btNext = new System.Windows.Forms.Button();
			this.fb_Path = new System.Windows.Forms.FolderBrowserDialog();
			this.tcWizard = new System.Windows.Forms.TabControl();
			this.tpConnect = new System.Windows.Forms.TabPage();
			this.paClient = new System.Windows.Forms.Panel();
			this.ckCreateDB = new System.Windows.Forms.CheckBox();
			this.label5 = new System.Windows.Forms.Label();
			this.gbAuthentication = new System.Windows.Forms.GroupBox();
			this.edPassword = new System.Windows.Forms.TextBox();
			this.laPassword = new System.Windows.Forms.Label();
			this.edUser = new System.Windows.Forms.TextBox();
			this.laUser = new System.Windows.Forms.Label();
			this.rbSS = new System.Windows.Forms.RadioButton();
			this.rbWindows = new System.Windows.Forms.RadioButton();
			this.edDatabase = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.edServer = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.tpPath = new System.Windows.Forms.TabPage();
			this.dpEndDate = new System.Windows.Forms.DateTimePicker();
			this.laEndDate = new System.Windows.Forms.Label();
			this.dpStartDate = new System.Windows.Forms.DateTimePicker();
			this.laStartDate = new System.Windows.Forms.Label();
			this.ckProcessAll = new System.Windows.Forms.CheckBox();
			this.label8 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.btPath = new System.Windows.Forms.Button();
			this.edInputPath = new System.Windows.Forms.TextBox();
			this.laPath = new System.Windows.Forms.Label();
			this.ckOverwrite = new System.Windows.Forms.CheckBox();
			this.tpProducers = new System.Windows.Forms.TabPage();
			this.paTopLeft = new System.Windows.Forms.Panel();
			this.tbProducers = new System.Windows.Forms.ToolBar();
			this.btUp = new System.Windows.Forms.ToolBarButton();
			this.btDown = new System.Windows.Forms.ToolBarButton();
			this.label3 = new System.Windows.Forms.Label();
			this.gbProducers = new System.Windows.Forms.GroupBox();
			this.lvProducers = new System.Windows.Forms.ListView();
			this.lcName = new System.Windows.Forms.ColumnHeader();
			this.lcProducer = new System.Windows.Forms.ColumnHeader();
			this.chAssembly = new System.Windows.Forms.ColumnHeader();
			this.chProgress = new System.Windows.Forms.ColumnHeader();
			this.chProdStart = new System.Windows.Forms.ColumnHeader();
			this.chProdEnd = new System.Windows.Forms.ColumnHeader();
			this.chProdDuration = new System.Windows.Forms.ColumnHeader();
			this.tpAnalyzers = new System.Windows.Forms.TabPage();
			this.panel1 = new System.Windows.Forms.Panel();
			this.tbAnalyzers = new System.Windows.Forms.ToolBar();
			this.btUpAnl = new System.Windows.Forms.ToolBarButton();
			this.btDownAnl = new System.Windows.Forms.ToolBarButton();
			this.label4 = new System.Windows.Forms.Label();
			this.gbAnalyzers = new System.Windows.Forms.GroupBox();
			this.lvAnalyzers = new System.Windows.Forms.ListView();
			this.acName = new System.Windows.Forms.ColumnHeader();
			this.chAnalyzer = new System.Windows.Forms.ColumnHeader();
			this.chAnlAssembly = new System.Windows.Forms.ColumnHeader();
			this.chAnlProgress = new System.Windows.Forms.ColumnHeader();
			this.chAnlStart = new System.Windows.Forms.ColumnHeader();
			this.chAnlEnd = new System.Windows.Forms.ColumnHeader();
			this.chAnlDuration = new System.Windows.Forms.ColumnHeader();
			this.tpFinish = new System.Windows.Forms.TabPage();
			this.label7 = new System.Windows.Forms.Label();
			this.epEndDate = new System.Windows.Forms.ErrorProvider();
			this.epPath = new System.Windows.Forms.ErrorProvider();
			this.paBottom.SuspendLayout();
			this.tcWizard.SuspendLayout();
			this.tpConnect.SuspendLayout();
			this.paClient.SuspendLayout();
			this.gbAuthentication.SuspendLayout();
			this.tpPath.SuspendLayout();
			this.tpProducers.SuspendLayout();
			this.paTopLeft.SuspendLayout();
			this.gbProducers.SuspendLayout();
			this.tpAnalyzers.SuspendLayout();
			this.panel1.SuspendLayout();
			this.gbAnalyzers.SuspendLayout();
			this.tpFinish.SuspendLayout();
			this.SuspendLayout();
			// 
			// imGlyphs
			// 
			this.imGlyphs.ImageSize = new System.Drawing.Size(16, 16);
			this.imGlyphs.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imGlyphs.ImageStream")));
			this.imGlyphs.TransparentColor = System.Drawing.SystemColors.Window;
			// 
			// paTop
			// 
			this.paTop.Dock = System.Windows.Forms.DockStyle.Top;
			this.paTop.Location = new System.Drawing.Point(0, 0);
			this.paTop.Name = "paTop";
			this.paTop.Size = new System.Drawing.Size(912, 16);
			this.paTop.TabIndex = 5;
			this.paTop.Visible = false;
			// 
			// cmTask
			// 
			this.cmTask.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
																				   this.miSelectAll,
																				   this.miSelectNone,
																				   this.miViewNone,
																				   this.menuItem1});
			// 
			// miSelectAll
			// 
			this.miSelectAll.Index = 0;
			this.miSelectAll.Text = "Select &All";
			this.miSelectAll.Click += new System.EventHandler(this.miSelectAll_Click);
			// 
			// miSelectNone
			// 
			this.miSelectNone.Index = 1;
			this.miSelectNone.Text = "&Unselect All";
			this.miSelectNone.Click += new System.EventHandler(this.miSelectNone_Click);
			// 
			// miViewNone
			// 
			this.miViewNone.Index = 2;
			this.miViewNone.Text = "View &Output";
			this.miViewNone.Click += new System.EventHandler(this.miViewNone_Click);
			// 
			// menuItem1
			// 
			this.menuItem1.Index = 3;
			this.menuItem1.Text = "&Go";
			this.menuItem1.Click += new System.EventHandler(this.btGo_Click);
			// 
			// paBottom
			// 
			this.paBottom.Controls.Add(this.btFinish);
			this.paBottom.Controls.Add(this.btPrev);
			this.paBottom.Controls.Add(this.btNext);
			this.paBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.paBottom.Location = new System.Drawing.Point(0, 398);
			this.paBottom.Name = "paBottom";
			this.paBottom.Size = new System.Drawing.Size(912, 40);
			this.paBottom.TabIndex = 7;
			// 
			// btFinish
			// 
			this.btFinish.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btFinish.Enabled = false;
			this.btFinish.Location = new System.Drawing.Point(832, 8);
			this.btFinish.Name = "btFinish";
			this.btFinish.TabIndex = 1;
			this.btFinish.Text = "&Finish";
			this.btFinish.Click += new System.EventHandler(this.btFinish_Click);
			// 
			// btPrev
			// 
			this.btPrev.Enabled = false;
			this.btPrev.Location = new System.Drawing.Point(582, 8);
			this.btPrev.Name = "btPrev";
			this.btPrev.TabIndex = 3;
			this.btPrev.Text = "<< &Back";
			this.btPrev.Click += new System.EventHandler(this.btPrev_Click);
			// 
			// btNext
			// 
			this.btNext.Location = new System.Drawing.Point(670, 8);
			this.btNext.Name = "btNext";
			this.btNext.TabIndex = 2;
			this.btNext.Text = "&Next >>";
			this.btNext.Click += new System.EventHandler(this.btNext_Click);
			// 
			// tcWizard
			// 
			this.tcWizard.Alignment = System.Windows.Forms.TabAlignment.Right;
			this.tcWizard.Controls.Add(this.tpConnect);
			this.tcWizard.Controls.Add(this.tpPath);
			this.tcWizard.Controls.Add(this.tpProducers);
			this.tcWizard.Controls.Add(this.tpAnalyzers);
			this.tcWizard.Controls.Add(this.tpFinish);
			this.tcWizard.ItemSize = new System.Drawing.Size(1, 1);
			this.tcWizard.Location = new System.Drawing.Point(0, 16);
			this.tcWizard.Multiline = true;
			this.tcWizard.Name = "tcWizard";
			this.tcWizard.SelectedIndex = 0;
			this.tcWizard.Size = new System.Drawing.Size(917, 382);
			this.tcWizard.TabIndex = 8;
			this.tcWizard.SelectedIndexChanged += new System.EventHandler(this.tcWizard_SelectedIndexChanged);
			// 
			// tpConnect
			// 
			this.tpConnect.Controls.Add(this.paClient);
			this.tpConnect.Location = new System.Drawing.Point(4, 4);
			this.tpConnect.Name = "tpConnect";
			this.tpConnect.Size = new System.Drawing.Size(908, 374);
			this.tpConnect.TabIndex = 0;
			this.tpConnect.Text = "Connection";
			// 
			// paClient
			// 
			this.paClient.Controls.Add(this.ckCreateDB);
			this.paClient.Controls.Add(this.label5);
			this.paClient.Controls.Add(this.gbAuthentication);
			this.paClient.Controls.Add(this.edDatabase);
			this.paClient.Controls.Add(this.label2);
			this.paClient.Controls.Add(this.edServer);
			this.paClient.Controls.Add(this.label1);
			this.paClient.Dock = System.Windows.Forms.DockStyle.Fill;
			this.paClient.Location = new System.Drawing.Point(0, 0);
			this.paClient.Name = "paClient";
			this.paClient.Size = new System.Drawing.Size(908, 374);
			this.paClient.TabIndex = 7;
			// 
			// ckCreateDB
			// 
			this.ckCreateDB.Location = new System.Drawing.Point(568, 310);
			this.ckCreateDB.Name = "ckCreateDB";
			this.ckCreateDB.TabIndex = 34;
			this.ckCreateDB.Text = "Create";
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(8, 16);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(249, 16);
			this.label5.TabIndex = 33;
			this.label5.Text = "Enter the connection info for the target database:";
			// 
			// gbAuthentication
			// 
			this.gbAuthentication.Controls.Add(this.edPassword);
			this.gbAuthentication.Controls.Add(this.laPassword);
			this.gbAuthentication.Controls.Add(this.edUser);
			this.gbAuthentication.Controls.Add(this.laUser);
			this.gbAuthentication.Controls.Add(this.rbSS);
			this.gbAuthentication.Controls.Add(this.rbWindows);
			this.gbAuthentication.Location = new System.Drawing.Point(352, 88);
			this.gbAuthentication.Name = "gbAuthentication";
			this.gbAuthentication.Size = new System.Drawing.Size(200, 200);
			this.gbAuthentication.TabIndex = 30;
			this.gbAuthentication.TabStop = false;
			this.gbAuthentication.Text = "Authentication";
			// 
			// edPassword
			// 
			this.edPassword.Enabled = false;
			this.edPassword.Location = new System.Drawing.Point(32, 160);
			this.edPassword.Name = "edPassword";
			this.edPassword.PasswordChar = '*';
			this.edPassword.TabIndex = 5;
			this.edPassword.Text = "";
			// 
			// laPassword
			// 
			this.laPassword.Enabled = false;
			this.laPassword.Location = new System.Drawing.Point(32, 144);
			this.laPassword.Name = "laPassword";
			this.laPassword.TabIndex = 4;
			this.laPassword.Text = "&Password:";
			// 
			// edUser
			// 
			this.edUser.Enabled = false;
			this.edUser.Location = new System.Drawing.Point(32, 112);
			this.edUser.Name = "edUser";
			this.edUser.TabIndex = 3;
			this.edUser.Text = ((string)(configurationAppSettings.GetValue("edUser.Text", typeof(string))));
			// 
			// laUser
			// 
			this.laUser.Enabled = false;
			this.laUser.Location = new System.Drawing.Point(32, 96);
			this.laUser.Name = "laUser";
			this.laUser.TabIndex = 2;
			this.laUser.Text = "&User name:";
			// 
			// rbSS
			// 
			this.rbSS.Location = new System.Drawing.Point(16, 56);
			this.rbSS.Name = "rbSS";
			this.rbSS.TabIndex = 1;
			this.rbSS.Text = "S&QL Server";
			this.rbSS.CheckedChanged += new System.EventHandler(this.rbWindows_CheckedChanged);
			// 
			// rbWindows
			// 
			this.rbWindows.Checked = ((bool)(configurationAppSettings.GetValue("rbWindows.Checked", typeof(bool))));
			this.rbWindows.Location = new System.Drawing.Point(16, 24);
			this.rbWindows.Name = "rbWindows";
			this.rbWindows.TabIndex = 0;
			this.rbWindows.TabStop = true;
			this.rbWindows.Text = "&Windows";
			this.rbWindows.CheckedChanged += new System.EventHandler(this.rbWindows_CheckedChanged);
			// 
			// edDatabase
			// 
			this.edDatabase.Location = new System.Drawing.Point(408, 312);
			this.edDatabase.Name = "edDatabase";
			this.edDatabase.Size = new System.Drawing.Size(144, 20);
			this.edDatabase.TabIndex = 32;
			this.edDatabase.Text = ((string)(configurationAppSettings.GetValue("edDatabase.Text", typeof(string))));
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(352, 314);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 16);
			this.label2.TabIndex = 31;
			this.label2.Text = "&Database";
			// 
			// edServer
			// 
			this.edServer.Location = new System.Drawing.Point(392, 48);
			this.edServer.Name = "edServer";
			this.edServer.Size = new System.Drawing.Size(160, 20);
			this.edServer.TabIndex = 29;
			this.edServer.Text = ((string)(configurationAppSettings.GetValue("edServer.Text", typeof(string))));
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(352, 48);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(100, 16);
			this.label1.TabIndex = 28;
			this.label1.Text = "&Server";
			// 
			// tpPath
			// 
			this.tpPath.Controls.Add(this.dpEndDate);
			this.tpPath.Controls.Add(this.laEndDate);
			this.tpPath.Controls.Add(this.dpStartDate);
			this.tpPath.Controls.Add(this.laStartDate);
			this.tpPath.Controls.Add(this.ckProcessAll);
			this.tpPath.Controls.Add(this.label8);
			this.tpPath.Controls.Add(this.label6);
			this.tpPath.Controls.Add(this.btPath);
			this.tpPath.Controls.Add(this.edInputPath);
			this.tpPath.Controls.Add(this.laPath);
			this.tpPath.Controls.Add(this.ckOverwrite);
			this.tpPath.Location = new System.Drawing.Point(4, 4);
			this.tpPath.Name = "tpPath";
			this.tpPath.Size = new System.Drawing.Size(908, 374);
			this.tpPath.TabIndex = 1;
			this.tpPath.Text = "Input Path";
			// 
			// dpEndDate
			// 
			this.dpEndDate.Enabled = false;
			this.dpEndDate.Location = new System.Drawing.Point(504, 232);
			this.dpEndDate.Name = "dpEndDate";
			this.dpEndDate.TabIndex = 25;
			this.dpEndDate.Validating += new System.ComponentModel.CancelEventHandler(this.dpEndDate_Validating);
			this.dpEndDate.Validated += new System.EventHandler(this.dpEndDate_Validated);
			// 
			// laEndDate
			// 
			this.laEndDate.Enabled = false;
			this.laEndDate.Location = new System.Drawing.Point(452, 233);
			this.laEndDate.Name = "laEndDate";
			this.laEndDate.TabIndex = 28;
			this.laEndDate.Text = "To:";
			// 
			// dpStartDate
			// 
			this.dpStartDate.Enabled = false;
			this.dpStartDate.Location = new System.Drawing.Point(216, 232);
			this.dpStartDate.Name = "dpStartDate";
			this.dpStartDate.TabIndex = 24;
			// 
			// laStartDate
			// 
			this.laStartDate.Enabled = false;
			this.laStartDate.Location = new System.Drawing.Point(160, 233);
			this.laStartDate.Name = "laStartDate";
			this.laStartDate.TabIndex = 27;
			this.laStartDate.Text = "From:";
			// 
			// ckProcessAll
			// 
			this.ckProcessAll.Checked = true;
			this.ckProcessAll.CheckState = System.Windows.Forms.CheckState.Checked;
			this.ckProcessAll.Location = new System.Drawing.Point(332, 180);
			this.ckProcessAll.Name = "ckProcessAll";
			this.ckProcessAll.TabIndex = 26;
			this.ckProcessAll.Text = "Process All";
			this.ckProcessAll.CheckedChanged += new System.EventHandler(this.ckProcessAll_CheckedChanged);
			// 
			// label8
			// 
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(8, 184);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(274, 16);
			this.label8.TabIndex = 23;
			this.label8.Text = "Enter the date range for the data you want to process:";
			// 
			// label6
			// 
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 16);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(251, 16);
			this.label6.TabIndex = 22;
			this.label6.Text = "Enter the input path for the data you want to load:";
			// 
			// btPath
			// 
			this.btPath.Location = new System.Drawing.Point(704, 64);
			this.btPath.Name = "btPath";
			this.btPath.Size = new System.Drawing.Size(32, 23);
			this.btPath.TabIndex = 21;
			this.btPath.Text = "...";
			this.btPath.Click += new System.EventHandler(this.btPath_Click);
			// 
			// edInputPath
			// 
			this.edInputPath.Location = new System.Drawing.Point(216, 64);
			this.edInputPath.Name = "edInputPath";
			this.edInputPath.Size = new System.Drawing.Size(488, 20);
			this.edInputPath.TabIndex = 18;
			this.edInputPath.Text = ((string)(configurationAppSettings.GetValue("edInputPath.Text", typeof(string))));
			// 
			// laPath
			// 
			this.laPath.Location = new System.Drawing.Point(160, 64);
			this.laPath.Name = "laPath";
			this.laPath.Size = new System.Drawing.Size(100, 16);
			this.laPath.TabIndex = 20;
			this.laPath.Text = "Input Path";
			// 
			// ckOverwrite
			// 
			this.ckOverwrite.Checked = true;
			this.ckOverwrite.CheckState = System.Windows.Forms.CheckState.Checked;
			this.ckOverwrite.Location = new System.Drawing.Point(216, 112);
			this.ckOverwrite.Name = "ckOverwrite";
			this.ckOverwrite.Size = new System.Drawing.Size(312, 24);
			this.ckOverwrite.TabIndex = 19;
			this.ckOverwrite.Text = "Overwrite existing database objects";
			// 
			// tpProducers
			// 
			this.tpProducers.Controls.Add(this.paTopLeft);
			this.tpProducers.Controls.Add(this.label3);
			this.tpProducers.Controls.Add(this.gbProducers);
			this.tpProducers.Location = new System.Drawing.Point(4, 4);
			this.tpProducers.Name = "tpProducers";
			this.tpProducers.Size = new System.Drawing.Size(908, 374);
			this.tpProducers.TabIndex = 2;
			this.tpProducers.Text = "Producers";
			// 
			// paTopLeft
			// 
			this.paTopLeft.Controls.Add(this.tbProducers);
			this.paTopLeft.Dock = System.Windows.Forms.DockStyle.Right;
			this.paTopLeft.Location = new System.Drawing.Point(860, 0);
			this.paTopLeft.Name = "paTopLeft";
			this.paTopLeft.Size = new System.Drawing.Size(48, 44);
			this.paTopLeft.TabIndex = 14;
			// 
			// tbProducers
			// 
			this.tbProducers.Buttons.AddRange(new System.Windows.Forms.ToolBarButton[] {
																						   this.btUp,
																						   this.btDown});
			this.tbProducers.Divider = false;
			this.tbProducers.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.tbProducers.DropDownArrows = true;
			this.tbProducers.ImageList = this.imGlyphs;
			this.tbProducers.Location = new System.Drawing.Point(0, 4);
			this.tbProducers.Name = "tbProducers";
			this.tbProducers.ShowToolTips = true;
			this.tbProducers.Size = new System.Drawing.Size(48, 40);
			this.tbProducers.TabIndex = 1;
			this.tbProducers.ButtonClick += new System.Windows.Forms.ToolBarButtonClickEventHandler(this.tbProducers_ButtonClick);
			// 
			// btUp
			// 
			this.btUp.ImageIndex = 3;
			// 
			// btDown
			// 
			this.btDown.ImageIndex = 4;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 16);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(299, 16);
			this.label3.TabIndex = 13;
			this.label3.Text = "Select the data loaders you want to use from the list below:";
			// 
			// gbProducers
			// 
			this.gbProducers.Controls.Add(this.lvProducers);
			this.gbProducers.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.gbProducers.Location = new System.Drawing.Point(0, 44);
			this.gbProducers.Name = "gbProducers";
			this.gbProducers.Size = new System.Drawing.Size(908, 330);
			this.gbProducers.TabIndex = 10;
			this.gbProducers.TabStop = false;
			this.gbProducers.Text = "Loaders";
			// 
			// lvProducers
			// 
			this.lvProducers.CheckBoxes = true;
			this.lvProducers.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
																						  this.lcName,
																						  this.lcProducer,
																						  this.chAssembly,
																						  this.chProgress,
																						  this.chProdStart,
																						  this.chProdEnd,
																						  this.chProdDuration});
			this.lvProducers.ContextMenu = this.cmTask;
			this.lvProducers.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvProducers.GridLines = true;
			this.lvProducers.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.lvProducers.Location = new System.Drawing.Point(3, 16);
			this.lvProducers.Name = "lvProducers";
			this.lvProducers.Size = new System.Drawing.Size(902, 311);
			this.lvProducers.SmallImageList = this.imGlyphs;
			this.lvProducers.TabIndex = 9;
			this.lvProducers.View = System.Windows.Forms.View.Details;
			this.lvProducers.DoubleClick += new System.EventHandler(this.lvProducers_DoubleClick);
			// 
			// lcName
			// 
			this.lcName.Text = "Name";
			this.lcName.Width = 268;
			// 
			// lcProducer
			// 
			this.lcProducer.Text = "Loader";
			this.lcProducer.Width = 125;
			// 
			// chAssembly
			// 
			this.chAssembly.Text = "Assembly";
			this.chAssembly.Width = 125;
			// 
			// chProgress
			// 
			this.chProgress.Text = "Progress";
			this.chProgress.Width = 126;
			// 
			// chProdStart
			// 
			this.chProdStart.Text = "Start";
			this.chProdStart.Width = 80;
			// 
			// chProdEnd
			// 
			this.chProdEnd.Text = "End";
			this.chProdEnd.Width = 80;
			// 
			// chProdDuration
			// 
			this.chProdDuration.Text = "Duration";
			this.chProdDuration.Width = 80;
			// 
			// tpAnalyzers
			// 
			this.tpAnalyzers.Controls.Add(this.panel1);
			this.tpAnalyzers.Controls.Add(this.label4);
			this.tpAnalyzers.Controls.Add(this.gbAnalyzers);
			this.tpAnalyzers.Location = new System.Drawing.Point(4, 4);
			this.tpAnalyzers.Name = "tpAnalyzers";
			this.tpAnalyzers.Size = new System.Drawing.Size(908, 374);
			this.tpAnalyzers.TabIndex = 3;
			this.tpAnalyzers.Text = "Analyzers";
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.tbAnalyzers);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
			this.panel1.Location = new System.Drawing.Point(860, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(48, 44);
			this.panel1.TabIndex = 16;
			// 
			// tbAnalyzers
			// 
			this.tbAnalyzers.Buttons.AddRange(new System.Windows.Forms.ToolBarButton[] {
																						   this.btUpAnl,
																						   this.btDownAnl});
			this.tbAnalyzers.Divider = false;
			this.tbAnalyzers.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.tbAnalyzers.DropDownArrows = true;
			this.tbAnalyzers.ImageList = this.imGlyphs;
			this.tbAnalyzers.Location = new System.Drawing.Point(0, 4);
			this.tbAnalyzers.Name = "tbAnalyzers";
			this.tbAnalyzers.ShowToolTips = true;
			this.tbAnalyzers.Size = new System.Drawing.Size(48, 40);
			this.tbAnalyzers.TabIndex = 1;
			this.tbAnalyzers.ButtonClick += new System.Windows.Forms.ToolBarButtonClickEventHandler(this.tbProducers_ButtonClick);
			// 
			// btUpAnl
			// 
			this.btUpAnl.ImageIndex = 3;
			// 
			// btDownAnl
			// 
			this.btDownAnl.ImageIndex = 4;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(8, 16);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(285, 16);
			this.label4.TabIndex = 15;
			this.label4.Text = "Select the analyzers you want to use from the list below:";
			// 
			// gbAnalyzers
			// 
			this.gbAnalyzers.Controls.Add(this.lvAnalyzers);
			this.gbAnalyzers.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.gbAnalyzers.Location = new System.Drawing.Point(0, 44);
			this.gbAnalyzers.Name = "gbAnalyzers";
			this.gbAnalyzers.Size = new System.Drawing.Size(908, 330);
			this.gbAnalyzers.TabIndex = 14;
			this.gbAnalyzers.TabStop = false;
			this.gbAnalyzers.Text = "Analyzers";
			// 
			// lvAnalyzers
			// 
			this.lvAnalyzers.CheckBoxes = true;
			this.lvAnalyzers.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
																						  this.acName,
																						  this.chAnalyzer,
																						  this.chAnlAssembly,
																						  this.chAnlProgress,
																						  this.chAnlStart,
																						  this.chAnlEnd,
																						  this.chAnlDuration});
			this.lvAnalyzers.ContextMenu = this.cmTask;
			this.lvAnalyzers.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvAnalyzers.GridLines = true;
			this.lvAnalyzers.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.lvAnalyzers.Location = new System.Drawing.Point(3, 16);
			this.lvAnalyzers.Name = "lvAnalyzers";
			this.lvAnalyzers.Size = new System.Drawing.Size(902, 311);
			this.lvAnalyzers.SmallImageList = this.imGlyphs;
			this.lvAnalyzers.TabIndex = 9;
			this.lvAnalyzers.View = System.Windows.Forms.View.Details;
			this.lvAnalyzers.DoubleClick += new System.EventHandler(this.lvProducers_DoubleClick);
			// 
			// acName
			// 
			this.acName.Text = "Name";
			this.acName.Width = 271;
			// 
			// chAnalyzer
			// 
			this.chAnalyzer.Text = "Analyzer";
			this.chAnalyzer.Width = 125;
			// 
			// chAnlAssembly
			// 
			this.chAnlAssembly.Text = "Assembly";
			this.chAnlAssembly.Width = 125;
			// 
			// chAnlProgress
			// 
			this.chAnlProgress.Text = "Progress";
			this.chAnlProgress.Width = 128;
			// 
			// chAnlStart
			// 
			this.chAnlStart.Text = "Start";
			this.chAnlStart.Width = 80;
			// 
			// chAnlEnd
			// 
			this.chAnlEnd.Text = "End";
			this.chAnlEnd.Width = 80;
			// 
			// chAnlDuration
			// 
			this.chAnlDuration.Text = "Duration";
			this.chAnlDuration.Width = 80;
			// 
			// tpFinish
			// 
			this.tpFinish.Controls.Add(this.label7);
			this.tpFinish.Location = new System.Drawing.Point(4, 4);
			this.tpFinish.Name = "tpFinish";
			this.tpFinish.Size = new System.Drawing.Size(908, 374);
			this.tpFinish.TabIndex = 4;
			this.tpFinish.Text = "Finish";
			// 
			// label7
			// 
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 16);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(298, 16);
			this.label7.TabIndex = 13;
			this.label7.Text = "Analysis complete.  Check the Analysis window for results.";
			// 
			// epEndDate
			// 
			this.epEndDate.ContainerControl = this;
			// 
			// epPath
			// 
			this.epPath.ContainerControl = this;
			// 
			// fmMain
			// 
			this.AcceptButton = this.btNext;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(912, 438);
			this.Controls.Add(this.tcWizard);
			this.Controls.Add(this.paBottom);
			this.Controls.Add(this.paTop);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(918, 470);
			this.MinimizeBox = false;
			this.Name = "fmMain";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "SQL Nexus BETA 1";
			this.Load += new System.EventHandler(this.fmMain_Load);
			this.Closed += new System.EventHandler(this.fmMain_Closed);
			this.paBottom.ResumeLayout(false);
			this.tcWizard.ResumeLayout(false);
			this.tpConnect.ResumeLayout(false);
			this.paClient.ResumeLayout(false);
			this.gbAuthentication.ResumeLayout(false);
			this.tpPath.ResumeLayout(false);
			this.tpProducers.ResumeLayout(false);
			this.paTopLeft.ResumeLayout(false);
			this.gbProducers.ResumeLayout(false);
			this.tpAnalyzers.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.gbAnalyzers.ResumeLayout(false);
			this.tpFinish.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
//		[STAThread]
//		static void Main() 
//		{
//			Application.Run(new fmMain());
//		}

		private int CountProducers()
		{
			int iRes=0;
			foreach (ListViewItem itm in lvProducers.Items) 
			{
				if (itm.Checked) 
					iRes++;
			}
			return iRes;
		}

		private int CountAnalyzers()
		{
			int iRes=0;
			foreach (ListViewItem itm in lvAnalyzers.Items) 
			{
				if (itm.Checked) 
					iRes++;
			}
			return iRes;
		}

		Thread[] ProducerWorkerThreads;  //Pools of worker threads
		ThreadStart[] ProducerDelegates; //Pool of ProducerDelegates (required by Thread class)
		CProducer[] Producers;	 //Pool of Producer instances (used by ProducerDelegates)
		fmAnalysisParent m_AnalysisHost=null;

		string m_Server;
		bool m_WindowsAuth;
		string m_User;
		string m_Password;
		string m_Database;
		string m_Path;

		private void RunProducers()
		{
			int numThreads=CountProducers();
			ProducerWorkerThreads = new Thread[numThreads];
			ProducerDelegates=new ThreadStart[numThreads];
			Producers=new CProducer[numThreads];

			int i=0;
			foreach (ListViewItem itm in lvProducers.Items) 
			{
				if (!itm.Checked) 
					continue;  //If unselected, loop
				IProducer prod = (IProducer)itm.Tag;
				Producers[i]= new CProducer(prod,m_Path,m_Server,m_Database,m_WindowsAuth,m_User,m_Password,this,ckOverwrite.Checked,ckProcessAll.Checked,dpStartDate.Value,dpEndDate.Value);
				ProducerDelegates[i]=new ThreadStart(Producers[i].Load);
				ProducerWorkerThreads[i]=new Thread(ProducerDelegates[i]);
				ProducerWorkerThreads[i].Start();
				itm.SubItems[4].Text=DateTime.Now.ToLongTimeString();
				itm.SubItems[5].Text="";  //clear end
				itm.SubItems[6].Text="";  //clear duration

				i++;
			}

			//Wait in a way that allows the GUI to remain responsive
			bool bWait=true;
			while (bWait) 
			{
				bWait=false;
				for (int j=0; j<i;j++) 
				{
					if (System.Threading.ThreadState.Stopped !=ProducerWorkerThreads[j].ThreadState)
						bWait=true;
					else 
					{
						foreach (ListViewItem itm in lvProducers.Items) 
						{
							if ((itm.Tag==Producers[j].Producer) && (0==itm.SubItems[5].Text.Length))
							{
								itm.SubItems[5].Text=DateTime.Now.ToLongTimeString();
								TimeSpan ts=(DateTime.Parse(itm.SubItems[5].Text).Subtract(DateTime.Parse(itm.SubItems[4].Text)));
								itm.SubItems[6].Text=string.Format("{0}h {1}m {2}s",ts.Hours,ts.Minutes,ts.Seconds);
							}
						}
					}
				}
				Application.DoEvents();
				Thread.Sleep(250);
			}

					//Wait on all producers to finish
//					for (int j=0; j<i;j++) 
//				ProducerWorkerThreads[j].Join();  

			foreach (ListViewItem itm in lvProducers.Items) 
			{
				if (!itm.Checked) 
					continue;  //If unselected, loop
				IProducer prod = (IProducer)itm.Tag;
				itm.ImageIndex=(RunStatus.Done==prod.Status)?13:12;

			}
		}


/*		Thread[] AnalyzerWorkerThreads;  //Pools of worker threads
		ThreadStart[] AnalyzerDelegates; //Pool of AnalyzerDelegates (required by Thread class)
		CAnalyzer[] Analyzers;	 //Pool of Analyzer instances (used by AnalyzerDelegates)
		private void RunAnalyzersMT(IAnalyzerParent mdiparent)
		{
			int numThreads=CountAnalyzers();
			AnalyzerWorkerThreads = new Thread[numThreads];
			AnalyzerDelegates=new ThreadStart[numThreads];
			Analyzers=new CAnalyzer[numThreads];

			int i=0;
			foreach (ListViewItem itm in lvAnalyzers.Items) 
			{
				if (!itm.Checked) 
					continue;  //If unselected, loop
				IAnalyzer anl = (IAnalyzer)itm.Tag;
				Analyzers[i]= new CAnalyzer(anl,m_Path,edServer.Text,"",true,"","",mdiparent);
				AnalyzerDelegates[i]=new ThreadStart(Analyzers[i].InitAndAnalyze);
				AnalyzerWorkerThreads[i]=new Thread(AnalyzerDelegates[i]);
				AnalyzerWorkerThreads[i].Start();
				i++;
			}
			//Wait on all analyzers to finish
			for (int j=0; j<i;j++) 
				AnalyzerWorkerThreads[j].Join();  
		}
*/

		private int RunAnalyzers()
		{
			int iRes=0;
			foreach (ListViewItem itm in lvAnalyzers.Items) 
			{
				if (!itm.Checked) 
					continue;  //If unselected, loop
				IAnalyzer anl= (IAnalyzer)itm.Tag; 
				try
				{
					if (!ckProcessAll.Checked)
						itm.ImageIndex=(anl.Analyze(dpStartDate.Value,dpEndDate.Value,m_AnalysisHost))?13:12;
					else
						itm.ImageIndex=(anl.Analyze(DateTime.MinValue,DateTime.MaxValue,m_AnalysisHost))?13:12;
				}
				catch (Exception ex)
				{
					MessageBox.Show(anl.Name+": "+ex.Message);
				}
				iRes++;
			}
			return iRes;
		}

		private bool AddProducer(Type typ, IProducer prod, string assemblyName, bool selected)
		{
			bool bRes=true;
			bool bInitialized=false;
			ListViewItem itm=lvProducers.Items.Add(prod.Name);  
			itm.SubItems.Add(typ.FullName);
			itm.SubItems.Add(Path.GetFileName(assemblyName)); //Assembly name
			itm.SubItems.Add(""); //Setup progress display
			itm.SubItems.Add(""); //Setup start display
			itm.SubItems.Add(""); //Setup end display
			itm.SubItems.Add(""); //Setup duration display
			itm.Tag=prod;
			try
			{
				bInitialized=prod.Init(m_Path,m_Server,m_Database,m_WindowsAuth,m_User,m_Password,this,ckOverwrite.Checked);
				itm.Checked=((bInitialized) && (selected));
			}
			catch (Exception ex)
			{
				tcWizard.SelectedIndex+=1;
				MessageBox.Show(prod.Name+": "+ex.Message);
				itm.Checked=false;
				bRes=false;
			}
			if (!bInitialized) 
			{
				itm.ForeColor = SystemColors.GrayText;
			}
			return bRes;
		}

		object FindName(ArrayList list, string searchValue)
		{
			object Res=null;
			foreach (object obj in list)
			{
				if (obj is string) 
				{
					if (0==string.Compare((obj as string),searchValue,true)) 
					{
						Res=obj;
						break;
					}
				}
				else if (obj is XmlNode) 
				{
					if (0==string.Compare((obj as XmlNode).Attributes["name"].Value,searchValue,true)) 
					{
						Res=obj;
						break;
					}
				}
			}
			return Res;
		}

		object FindAssembly(ArrayList list, string searchValue)
		{
			object Res=null;
			foreach (object obj in list)
			{
				if (obj is string) 
				{
					if (0==string.Compare((obj as string),searchValue,true)) 
					{
						Res=obj;
						break;
					}
				}
				else if (obj is XmlNode) 
				{
					if (0==string.Compare(Application.StartupPath+"\\"+(obj as XmlNode).Attributes["assembly"].Value,searchValue,true)) 
					{
						Res=obj;
						break;
					}
				}
			}
			return Res;
		}

		private bool EnumProducers()
		{
			bool bRes=true;
			lvProducers.Items.Clear();

			ArrayList Producers = new ArrayList();

			if (File.Exists("manifest.xml")) 
			{
				XmlDocument manifestDoc = new XmlDocument();
				manifestDoc.Load("manifest.xml");
				foreach (XmlNode node in manifestDoc["Manifest"]["Producers"].ChildNodes)
				{
					if (null==FindName(Producers,node.Attributes["name"].Value))
						Producers.Add(node);				
				}
			}

			if (null!=m_ConfigDoc) 
			{
				foreach (XmlNode node in m_ConfigDoc["dsConfig"]["Analysis"]["Producers"].ChildNodes)
				{
					if (null==FindName(Producers,node.Attributes["name"].Value))
						Producers.Add(node);				
				}
			}

			string[] Files=Directory.GetFiles(Application.StartupPath,"*.DLL");
			foreach (string file in Files) 
			{
				if (null==FindAssembly(Producers,file))
					Producers.Add(file);				
			}


			foreach (object obj in Producers) 
			{
				string file;
				bool selected;
				if (obj is XmlNode) 
				{
					file=Application.StartupPath+"\\"+(obj as XmlNode).Attributes["assembly"].Value;
					selected=Convert.ToBoolean((obj as XmlNode).Attributes["selected"].Value);
				}
				else // assume string
				{
					file=(obj as string);
					selected=false;
				}
				Assembly Assem;
				try
				{
					Assem = Assembly.LoadFile(file);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
					continue;
				}
				Type[] typs=Assem.GetExportedTypes();
				foreach (Type typ in typs) 
				{
					//Ignore abstract classes
					if (typ.IsAbstract)
						continue;

					TypeFilter loadIfcFilt = new TypeFilter(loadIfcFilter);
					Type[] ifcs = typ.FindInterfaces(loadIfcFilt,"SQLAnalyzerInterfaces.IProducer");
					foreach (Type ifc in ifcs) 
					{

						//If we get in here, the Class implements the interface, so add it to the list
						//and bail
						Assembly assem = Assembly.LoadFile(file);
						IProducer prod = (IProducer)assem.CreateInstance(typ.FullName,true); 

						if (obj is XmlNode)
						{
							prod.Name=(obj as XmlNode).Attributes["name"].Value;
						}

						//Ignore anonymous producers
						if (0==prod.Name.Length) 
							continue;

						if (!AddProducer(typ, prod, file, selected))
							bRes=false;
					}
				}
			}
			return bRes;
		}


		private bool AddAnalyzer(Type typ, IAnalyzer anl, string assemblyName, bool selected)
		{
			bool bRes=true;
			ListViewItem itm=lvAnalyzers.Items.Add(anl.Name);  
			itm.SubItems.Add(typ.FullName);
			itm.SubItems.Add(Path.GetFileName(assemblyName)); //Setup progress display
			itm.SubItems.Add(""); //Setup progress display
			itm.Tag=anl;
			CDBObjectChecker DBObjectChecker = new CDBObjectChecker(m_Server,m_Database,m_WindowsAuth,m_User,m_Password);
			bool bInitialized=false;
			try
			{
				bInitialized=anl.Init(m_Path,m_Server,m_Database,m_WindowsAuth,m_User,m_Password,this,DBObjectChecker);
				itm.Checked=((bInitialized) && (selected));
			}
			catch (Exception ex)
			{
				bRes=false;
				tcWizard.SelectedIndex+=1;
				MessageBox.Show(anl.Name+": "+ex.Message);
			}
			if (!bInitialized) 
			{
				itm.ForeColor = SystemColors.GrayText;
			}
			return bRes;
		}

		private bool EnumAnalyzers()
		{

			bool bRes=true;
			lvAnalyzers.Items.Clear();


			ArrayList Analyzers = new ArrayList();

			if (File.Exists("manifest.xml")) 
			{
				XmlDocument manifestDoc = new XmlDocument();
				manifestDoc.Load("manifest.xml");
				foreach (XmlNode node in manifestDoc["Manifest"]["Analyzers"].ChildNodes)
				{
					if (null==FindName(Analyzers,node.Attributes["name"].Value))
						Analyzers.Add(node);				
				}
			}

			if (null!=m_ConfigDoc) 
			{
				foreach (XmlNode node in m_ConfigDoc["dsConfig"]["Analysis"]["Analyzers"].ChildNodes)
				{
					if (null==FindName(Analyzers,node.Attributes["name"].Value))
						Analyzers.Add(node);				
				}
			}

			string[] Files=Directory.GetFiles(Application.StartupPath,"*.DLL");
			foreach (string file in Files) 
			{
				if (null==FindAssembly(Analyzers,file))
					Analyzers.Add(file);				
			}

			foreach (object obj in Analyzers) 
			{
				string file;
				bool selected;
				if (obj is XmlNode) 
				{
					file=Application.StartupPath+"\\"+(obj as XmlNode).Attributes["assembly"].Value;
					selected=Convert.ToBoolean((obj as XmlNode).Attributes["selected"].Value);
				}
				else // assume string
				{
					file=(obj as string);
					selected=false;
				}
				Assembly Assem;
				try
				{
					Assem = Assembly.LoadFile(file);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
					continue;
				}
				Type[] typs=Assem.GetExportedTypes();
				foreach (Type typ in typs) 
				{
					//Ignore abstract classes
					if (typ.IsAbstract)
						continue;

					TypeFilter loadIfcFilt = new TypeFilter(loadIfcFilter);
					Type[] ifcs = typ.FindInterfaces(loadIfcFilt,"SQLAnalyzerInterfaces.IAnalyzer");
					foreach (Type ifc in ifcs) 
					{

						//If we get in here, the Class implements the interface, so add it to the list
						//and bail
						Assembly assem = Assembly.LoadFile(file);
						IAnalyzer anl = (IAnalyzer)assem.CreateInstance(typ.FullName,true); 

						if (obj is XmlNode)
						{
							anl.Name=(obj as XmlNode).Attributes["name"].Value;
						}

						//Ignore anonymous analyzers
						if (0==anl.Name.Length) 
							continue;

						if (!AddAnalyzer(typ,anl,file,selected))
							bRes=false;
					}
				}
			}
			return bRes;
		}

		XmlDocument m_cfgDoc= new XmlDocument();

		private void fmMain_Load(object sender, System.EventArgs e)
		{
			epEndDate.SetIconAlignment (this.dpEndDate, ErrorIconAlignment.MiddleRight);
			epEndDate.SetIconPadding (this.dpEndDate, 2);
			epEndDate.BlinkRate = 1000;
			epEndDate.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;

			epPath.SetIconAlignment (this.btPath, ErrorIconAlignment.MiddleRight);
			epPath.SetIconPadding (this.btPath, 2);
			epPath.BlinkRate = 1000;
			epPath.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;

			m_cfgDoc.Load("PSSDiagConfig.xml");
			edInputPath.Text=m_cfgDoc["Configuration"]["Analyzer"].Attributes["inputpath"].Value;
			edServer.Text=m_cfgDoc["Configuration"]["Analyzer"].Attributes["server"].Value;
			edDatabase.Text=m_cfgDoc["Configuration"]["Analyzer"].Attributes["database"].Value;
			ckOverwrite.Checked=Convert.ToBoolean(m_cfgDoc["Configuration"]["Analyzer"].Attributes["overwrite"].Value);
			ckCreateDB.Checked=Convert.ToBoolean(m_cfgDoc["Configuration"]["Analyzer"].Attributes["createdb"].Value);
			rbWindows.Checked=Convert.ToBoolean(m_cfgDoc["Configuration"]["Analyzer"].Attributes["windowsauth"].Value);
			rbSS.Checked=!rbWindows.Checked;
			edUser.Text=m_cfgDoc["Configuration"]["Analyzer"].Attributes["user"].Value;
			ckProcessAll.Checked=Convert.ToBoolean(m_cfgDoc["Configuration"]["Analyzer"].Attributes["processall"].Value);
			System.IFormatProvider dateFormat = new System.Globalization.CultureInfo("en-US");
			string[] expectedFormats = {"u"};
			dpStartDate.Value=DateTime.ParseExact(m_cfgDoc["Configuration"]["Analyzer"].Attributes["startdate"].Value,expectedFormats,dateFormat,System.Globalization.DateTimeStyles.None);
			dpEndDate.Value=DateTime.ParseExact(m_cfgDoc["Configuration"]["Analyzer"].Attributes["enddate"].Value,expectedFormats,dateFormat,System.Globalization.DateTimeStyles.None);

		}

		public static bool loadIfcFilter(Type typeObj,Object criteriaObj)
		{
			if (typeObj.ToString() == criteriaObj.ToString())
				return true;
			else
				return false;
		}

		public void ShowProgress(ITask Task, string Message, int Count, int Progress, int Ticker)
		{
			if (this.InvokeRequired) 
			{
				// Get ready to show progress asynchronously
				ShowProgressDelegate showProgress =
					new ShowProgressDelegate(UpdateProgress);

				// Show progress
				this.BeginInvoke(showProgress, new object[] {Task, Message, Count, Progress, Ticker});
			}
			else
			{
				UpdateProgress(Task, Message, Count, Progress, Ticker);
			}
		}

		public void UpdateProgress(ITask Task, string Message, int Count, int Progress, int Ticker)
		{
			// Make sure we're on the right thread
//			Debug.Assert(InvokeRequired == false);

			ListView lv=(Task is IProducer)?lvProducers:lvAnalyzers;


			foreach (ListViewItem itm in lv.Items) 
			{
				if (itm.Tag==Task) 
				{
					if (null!=Message) 
					{
						itm.SubItems[3].Text=Message;
					}
					else if (-1!=Count) 
					{
						itm.SubItems[3].Text=Count.ToString();
					}
					else if (-1!=Progress) 
					{
						itm.SubItems[3].Text=Progress.ToString()+"%";
					}
					else if (-1!=Ticker) 
					{
						// TODO:  Implement this
						// Used for rotating ticker or glyph of some type -- stub out for now
//						itm.SubItems[2].Text=Ticker.ToString();  
					}
					break;
				}
			}
			Application.DoEvents();
		}

		private void btGo_Click(object sender, System.EventArgs e)
		{
			Cursor=Cursors.WaitCursor;
			try
			{
				RunProducers();
				if (CountAnalyzers()>0)
				{
					if (null!=m_AnalysisHost)
						m_AnalysisHost.Dispose();
					m_AnalysisHost = new fmAnalysisParent(this,m_ConfigDoc);
					m_AnalysisHost.Show();
					RunAnalyzers();
				}
			}
			finally
			{
				Cursor=Cursors.Default;
			}
		}

		private void ShowTaskOutput(ListView lv)
		{
			if ((0==lv.SelectedItems.Count) || (null==lv.SelectedItems[0]) || (null==lv.SelectedItems[0].Tag)) return;
			fmTaskOutput f = new fmTaskOutput();
			ITask task=(ITask)lv.SelectedItems[0].Tag;
			f.Text=task.Name+" Output";
			foreach (string s in task.Output)
				f.edOutput.Text+=s+"\r\n";
			f.ShowDialog();
		}

		private void lvProducers_DoubleClick(object sender, System.EventArgs e)
		{
			Debug.Assert(sender is ListView);
			ShowTaskOutput((ListView)sender);
		}

		private void btPath_Click(object sender, System.EventArgs e)
		{
			if (DialogResult.OK==fb_Path.ShowDialog())
				m_Path=fb_Path.SelectedPath;
		}

		private void ToggleAll(ListView lv, bool bState)
		{
			foreach (ListViewItem itm in lv.Items) 
			{
				if (SystemColors.GrayText != itm.ForeColor)
					itm.Checked=bState;
			}
		}

		private void miSelectAll_Click(object sender, System.EventArgs e)
		{
			Debug.Assert(cmTask.SourceControl is ListView);
			ToggleAll((ListView)cmTask.SourceControl,true);
		}

		private void miSelectNone_Click(object sender, System.EventArgs e)
		{
			Debug.Assert(cmTask.SourceControl is ListView);
			ToggleAll((ListView)cmTask.SourceControl,false);
		}

		private void miViewNone_Click(object sender, System.EventArgs e)
		{
			Debug.Assert(cmTask.SourceControl is ListView);
			ShowTaskOutput((ListView)cmTask.SourceControl);
		}

		private void lvProducers_ItemCheck(object sender, System.Windows.Forms.ItemCheckEventArgs e)
		{
			ListView lv = (sender as ListView);
			ListViewItem lvi = lv.Items[e.Index];
			//Don't let 'disabled' items be changed
			if (SystemColors.GrayText==lvi.ForeColor)
				e.NewValue=e.CurrentValue;
		}

		private void btNext_Click(object sender, System.EventArgs e)
		{
			Cursor = Cursors.WaitCursor;
			try
			{
				if (tpConnect==tcWizard.SelectedTab)
				{
					m_Server=edServer.Text;
					m_WindowsAuth=rbWindows.Checked;
					m_User=edUser.Text;
					m_Password=edPassword.Text;
					m_Database=edDatabase.Text;

					if (ckCreateDB.Checked) 
					{
						try
						{
							SqlConnection cn = new SqlConnection(string.Format("Data Source={0}; {1}",m_Server,m_WindowsAuth?"Integrated Security=SSPI":"User Id="+m_User+";Password="+m_Password));
							cn.Open();
							SqlCommand cmd;
							cmd = new SqlCommand(string.Format("create database {0}",m_Database),cn);
							cmd.ExecuteNonQuery();
							cn.Close();
							tcWizard.SelectedIndex+=1;
						}
						catch(Exception ex)
						{
							MessageBox.Show("Error connecting to target server and creating database.  Message: "+ex.Message);
						}
					}
					else 
					{
						try
						{
							SqlConnection cn = new SqlConnection(string.Format("Data Source={0}; Initial Catalog={1}; {2}",m_Server,m_Database,m_WindowsAuth?"Integrated Security=SSPI":"User Id="+m_User+";Password="+m_Password));
							cn.Open();
							cn.Close();
							tcWizard.SelectedIndex+=1;
						}
						catch(Exception ex)
						{
							MessageBox.Show("Error connecting to specified server and database.  Message: "+ex.Message);
						}
					}

				}
				else if (tpPath==tcWizard.SelectedTab)
				{

					if (Directory.Exists(edInputPath.Text))
					{
						// Clear the error, if any, in the error provider.
						epPath.SetError(this.btPath, "");
						m_Path=edInputPath.Text;

						if ((ckProcessAll.Checked) || (dpEndDate.Value>=dpStartDate.Value))
						{
							// Clear the error, if any, in the error provider.
							epEndDate.SetError(this.dpEndDate, "");
							if (EnumProducers()) 
							{
								btNext.Text="Load";
								tcWizard.SelectedIndex+=1;
							}
						}
						else
						{
							// Set the error if the name is not valid.
							epEndDate.SetError(this.dpEndDate, "To Date must be greater than or equal to From Date.");
						}

					}
					else
					{
						// Set the error if the name is not valid.
						epPath.SetError(this.btPath, "Path must exist.");
					}

				}
				else if (tpProducers==tcWizard.SelectedTab)
				{
					if ("Load"==btNext.Text) 
					{
						Cursor=Cursors.WaitCursor;
						btNext.Enabled=false;
						try
						{
							RunProducers();
							btNext.Text="Next >>";
							EnumAnalyzers();
						}
						finally
						{
							Cursor=Cursors.Default;
							btNext.Enabled=true;
						}
					}
					else
					{
						btNext.Text="Analyze";
						tcWizard.SelectedIndex+=1;
					}
				}
				else if (tpAnalyzers==tcWizard.SelectedTab)
				{
					if ("Analyze"==btNext.Text) 
					{
						if (CountAnalyzers()>0)
						{
							m_AnalysisHost = new fmAnalysisParent(this,m_ConfigDoc);
							m_AnalysisHost.Show();
							btNext.Enabled=false;
							Cursor=Cursors.WaitCursor;
							try
							{
								RunAnalyzers();
							}
							finally
							{
								btNext.Enabled=true;
								Cursor=Cursors.Default;
							}
							btNext.Text="Next >>";
						}
					}
					else
					{
						tcWizard.SelectedIndex+=1;
					}
				}
			}
			finally
			{
				Cursor = Cursors.Default;
			}
		}

		private void tcWizard_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			if (tpFinish==tcWizard.SelectedTab)
			{
				btFinish.Enabled=true;
				btNext.Enabled=false;
			}
			else
			{
				btFinish.Enabled=false;
				btNext.Enabled=true;
			}
			btPrev.Enabled=(tcWizard.SelectedIndex>0);
		}

		private void btPrev_Click(object sender, System.EventArgs e)
		{
			if (tpProducers==tcWizard.SelectedTab)
			{
				if ("Next >>"==btNext.Text) 
				{
					btNext.Text="Load";
				}
				else
				{
					tcWizard.SelectedIndex-=1;
					btNext.Text="Next >>";
				}
			}
			else if (tpAnalyzers==tcWizard.SelectedTab)
			{
				if ("Next >>"==btNext.Text) 
				{
					btNext.Text="Analyze";
				}
				else
				{
					tcWizard.SelectedIndex-=1;
					btNext.Text="Next >>";
				}
			}
			else
			{
				if (tcWizard.SelectedIndex>0)
					tcWizard.SelectedIndex-=1;
				btNext.Text="Next >>";
			}
		}

		private void btFinish_Click(object sender, System.EventArgs e)
		{
			Close();
		}

		public void UpdateDocFromUI()
		{
			//Producers
			m_ConfigDoc["dsConfig"]["Analysis"]["Producers"].InnerText="";
			foreach (ListViewItem itm in lvProducers.Items)
			{
				XmlNode childnode=m_ConfigDoc.CreateElement("Producer");

				XmlAttribute newAttr = m_ConfigDoc.CreateAttribute("name");
				newAttr.Value = itm.Text;
				childnode.Attributes.SetNamedItem(newAttr);

				XmlAttribute newAttr2 = m_ConfigDoc.CreateAttribute("assembly");
				newAttr2.Value = Path.GetFileName(itm.SubItems[2].Text);
				childnode.Attributes.SetNamedItem(newAttr2);

				XmlAttribute newAttr3 = m_ConfigDoc.CreateAttribute("selected");
				newAttr3.Value = itm.Checked.ToString().ToLower();
				childnode.Attributes.SetNamedItem(newAttr3);

				m_ConfigDoc["dsConfig"]["Analysis"]["Producers"].AppendChild(childnode);				

			}

			//Analyzers
			m_ConfigDoc["dsConfig"]["Analysis"]["Analyzers"].InnerText="";
			foreach (ListViewItem itm in lvAnalyzers.Items)
			{
				XmlNode childnode=m_ConfigDoc.CreateElement("Analyzer");

				XmlAttribute newAttr = m_ConfigDoc.CreateAttribute("name");
				newAttr.Value = itm.Text;
				childnode.Attributes.SetNamedItem(newAttr);

				XmlAttribute newAttr2 = m_ConfigDoc.CreateAttribute("assembly");
				newAttr2.Value = Path.GetFileName(itm.SubItems[2].Text);
				childnode.Attributes.SetNamedItem(newAttr2);

				XmlAttribute newAttr3 = m_ConfigDoc.CreateAttribute("selected");
				newAttr3.Value = itm.Checked.ToString().ToLower();
				childnode.Attributes.SetNamedItem(newAttr3);

				m_ConfigDoc["dsConfig"]["Analysis"]["Analyzers"].AppendChild(childnode);				

			}
		}

		private void tbProducers_ButtonClick(object sender, System.Windows.Forms.ToolBarButtonClickEventArgs e)
		{
			ListView lv;
			if (sender == tbAnalyzers)
				lv=lvAnalyzers;
			else
				lv=lvProducers;

			if (((e.Button==btUp) || (e.Button==btUpAnl)) && (0!=lv.SelectedItems.Count) && (0!=lv.SelectedItems[0].Index))
			{
				int ind=lv.SelectedItems[0].Index;

				ListViewItem curItem = (ListViewItem)lv.Items[ind].Clone();

				lv.Items.Remove(lv.SelectedItems[0]);

				lv.Items.Insert(ind-1,curItem);

				curItem.Selected=true;

				return;

			}
			if (((e.Button==btDown) || (e.Button==btDownAnl)) && (0!=lv.SelectedItems.Count) && (lv.Items.Count-1!=lv.SelectedItems[0].Index))
			{
				int ind=lv.SelectedItems[0].Index;

				ListViewItem curItem = (ListViewItem)lv.Items[ind].Clone();

				lv.Items.Remove(lv.SelectedItems[0]);

				lv.Items.Insert(ind+1,curItem);

				curItem.Selected=true;

			}
		}

		private void rbWindows_CheckedChanged(object sender, System.EventArgs e)
		{
			edUser.Enabled=!rbWindows.Checked;
			edPassword.Enabled=!rbWindows.Checked;
			laUser.Enabled=edUser.Enabled;
			laPassword.Enabled=edPassword.Enabled;
		}

		private void fmMain_Closed(object sender, System.EventArgs e)
		{
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["inputpath"].Value=this.edInputPath.Text;
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["server"].Value=this.edServer.Text;
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["database"].Value=this.edDatabase.Text;
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["user"].Value=this.edUser.Text;
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["overwrite"].Value=ckOverwrite.Checked.ToString().ToLower();
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["createdb"].Value=ckCreateDB.Checked.ToString().ToLower();
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["windowsauth"].Value=rbWindows.Checked.ToString().ToLower();
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["processall"].Value=ckProcessAll.Checked.ToString().ToLower();
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["startdate"].Value=dpStartDate.Value.ToString("u");
			m_cfgDoc["Configuration"]["Analyzer"].Attributes["enddate"].Value=dpEndDate.Value.ToString("u");
			m_cfgDoc.Save("PSSDiagConfig.xml");
		}

		private void ckProcessAll_CheckedChanged(object sender, System.EventArgs e)
		{
			laStartDate.Enabled=!ckProcessAll.Checked;
			dpStartDate.Enabled=!ckProcessAll.Checked;
			laEndDate.Enabled=!ckProcessAll.Checked;
			dpEndDate.Enabled=!ckProcessAll.Checked;
		}

		private void dpEndDate_Validated(object sender, System.EventArgs e)
		{
		}

		private void dpEndDate_Validating(object sender, System.ComponentModel.CancelEventArgs e)
		{
		}



	}
}
