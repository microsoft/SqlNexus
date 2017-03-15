namespace sqlnexus
{
    partial class fmAbout
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmAbout));
            this.tiStarter = new System.Windows.Forms.Timer(this.components);
            this.tiMover = new System.Windows.Forms.Timer(this.components);
            this.laTitle = new System.Windows.Forms.Label();
            this.laVersion = new System.Windows.Forms.Label();
            this.llSupport = new System.Windows.Forms.LinkLabel();
            this.tiDissolve = new System.Windows.Forms.Timer(this.components);
            this.laCopyright = new System.Windows.Forms.Label();
            this.btClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // tiStarter
            // 
            this.tiStarter.Interval = 5000;
            this.tiStarter.Tick += new System.EventHandler(this.tiStarter_Tick);
            // 
            // tiMover
            // 
            this.tiMover.Interval = 10;
            this.tiMover.Tick += new System.EventHandler(this.tiMover_Tick);
            // 
            // laTitle
            // 
            resources.ApplyResources(this.laTitle, "laTitle");
            this.laTitle.BackColor = System.Drawing.Color.Transparent;
            this.laTitle.ForeColor = System.Drawing.Color.Plum;
            this.laTitle.Name = "laTitle";
            // 
            // laVersion
            // 
            resources.ApplyResources(this.laVersion, "laVersion");
            this.laVersion.BackColor = System.Drawing.Color.Transparent;
            this.laVersion.ForeColor = System.Drawing.Color.Plum;
            this.laVersion.Name = "laVersion";
            this.laVersion.Click += new System.EventHandler(this.laVersion_Click);
            // 
            // llSupport
            // 
            resources.ApplyResources(this.llSupport, "llSupport");
            this.llSupport.BackColor = System.Drawing.Color.Transparent;
            this.llSupport.LinkBehavior = System.Windows.Forms.LinkBehavior.AlwaysUnderline;
            this.llSupport.LinkColor = System.Drawing.Color.Plum;
            this.llSupport.Name = "llSupport";
            this.llSupport.TabStop = true;
            this.llSupport.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // tiDissolve
            // 
            this.tiDissolve.Enabled = true;
            this.tiDissolve.Interval = 10;
            this.tiDissolve.Tick += new System.EventHandler(this.tiDissolve_Tick);
            // 
            // laCopyright
            // 
            this.laCopyright.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.laCopyright, "laCopyright");
            this.laCopyright.ForeColor = System.Drawing.Color.Plum;
            this.laCopyright.Name = "laCopyright";
            // 
            // btClose
            // 
            resources.ApplyResources(this.btClose, "btClose");
            this.btClose.Image = global::sqlnexus.Properties.Resources.pinkbutton;
            this.btClose.Name = "btClose";
            this.btClose.UseVisualStyleBackColor = true;
            this.btClose.Click += new System.EventHandler(this.btClose_Click);
            this.btClose.Paint += new System.Windows.Forms.PaintEventHandler(this.btClose_Paint);
            // 
            // fmAbout
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ControlBox = false;
            this.Controls.Add(this.btClose);
            this.Controls.Add(this.laCopyright);
            this.Controls.Add(this.llSupport);
            this.Controls.Add(this.laVersion);
            this.Controls.Add(this.laTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "fmAbout";
            this.Opacity = 0D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.fmAbout_FormClosing);
            this.Load += new System.EventHandler(this.fmAbout_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.fmAbout_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.fmAbout_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.fmAbout_MouseMove);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer tiStarter;
        private System.Windows.Forms.Timer tiMover;
        private System.Windows.Forms.Label laTitle;
        private System.Windows.Forms.Label laVersion;
        private System.Windows.Forms.LinkLabel llSupport;
        private System.Windows.Forms.Timer tiDissolve;
        private System.Windows.Forms.Label laCopyright;
        private System.Windows.Forms.Button btClose;

    }
}