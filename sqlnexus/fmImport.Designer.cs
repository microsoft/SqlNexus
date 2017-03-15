namespace sqlnexus
{
    partial class fmImport
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmImport));
            this.fb_Path = new System.Windows.Forms.FolderBrowserDialog();
            this.paTop = new System.Windows.Forms.Panel();
            this.cmOptions = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tsiImporters = new System.Windows.Forms.ToolStripMenuItem();
            this.tsiDropDBBeforeImporting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsiSaveOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.tsiUseDefaultOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.cbPath = new System.Windows.Forms.ComboBox();
            this.llOptions = new System.Windows.Forms.LinkLabel();
            this.btPath = new System.Windows.Forms.Button();
            this.tsbGo = new System.Windows.Forms.Button();
            this.laInstructions = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.laPath = new System.Windows.Forms.Label();
            this.tlpFiles = new System.Windows.Forms.TableLayoutPanel();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.ssStatus = new System.Windows.Forms.StatusStrip();
            this.ssText = new System.Windows.Forms.ToolStripStatusLabel();
            this.paTop.SuspendLayout();
            this.cmOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.tlpFiles.SuspendLayout();
            this.ssStatus.SuspendLayout();
            this.SuspendLayout();
            // 
            // paTop
            // 
            this.paTop.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.paTop.ContextMenuStrip = this.cmOptions;
            this.paTop.Controls.Add(this.cbPath);
            this.paTop.Controls.Add(this.llOptions);
            this.paTop.Controls.Add(this.btPath);
            this.paTop.Controls.Add(this.tsbGo);
            this.paTop.Controls.Add(this.laInstructions);
            this.paTop.Controls.Add(this.pictureBox1);
            this.paTop.Controls.Add(this.laPath);
            this.paTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.paTop.Location = new System.Drawing.Point(0, 0);
            this.paTop.Name = "paTop";
            this.paTop.Size = new System.Drawing.Size(457, 86);
            this.paTop.TabIndex = 0;
            this.paTop.Paint += new System.Windows.Forms.PaintEventHandler(this.paTop_Paint);
            // 
            // cmOptions
            // 
            this.cmOptions.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsiImporters,
            this.tsiDropDBBeforeImporting,
            this.tsiSaveOptions,
            this.tsiUseDefaultOptions});
            this.cmOptions.Name = "cmOptions";
            this.cmOptions.Size = new System.Drawing.Size(255, 92);
            this.cmOptions.Opening += new System.ComponentModel.CancelEventHandler(this.cmOptions_Opening);
            // 
            // tsiImporters
            // 
            this.tsiImporters.Name = "tsiImporters";
            this.tsiImporters.Size = new System.Drawing.Size(254, 22);
            this.tsiImporters.Text = "Importers";
            // 
            // tsiDropDBBeforeImporting
            // 
            this.tsiDropDBBeforeImporting.CheckOnClick = true;
            this.tsiDropDBBeforeImporting.Name = "tsiDropDBBeforeImporting";
            this.tsiDropDBBeforeImporting.Size = new System.Drawing.Size(254, 22);
            this.tsiDropDBBeforeImporting.Text = "Drop Current DB Before Importing";
            // 
            // tsiSaveOptions
            // 
            this.tsiSaveOptions.CheckOnClick = true;
            this.tsiSaveOptions.Name = "tsiSaveOptions";
            this.tsiSaveOptions.Size = new System.Drawing.Size(254, 22);
            this.tsiSaveOptions.Text = "Save My Options";
            // 
            // tsiUseDefaultOptions
            // 
            this.tsiUseDefaultOptions.Name = "tsiUseDefaultOptions";
            this.tsiUseDefaultOptions.Size = new System.Drawing.Size(254, 22);
            this.tsiUseDefaultOptions.Text = "Restore Default Options";
            // 
            // cbPath
            // 
            this.cbPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbPath.FormattingEnabled = true;
            this.cbPath.Location = new System.Drawing.Point(115, 32);
            this.cbPath.Name = "cbPath";
            this.cbPath.Size = new System.Drawing.Size(302, 21);
            this.cbPath.TabIndex = 1;
            this.cbPath.SelectedIndexChanged += new System.EventHandler(this.tbPath_TextChanged);
            this.cbPath.TextChanged += new System.EventHandler(this.tbPath_TextChanged);
            // 
            // llOptions
            // 
            this.llOptions.AutoSize = true;
            this.llOptions.Location = new System.Drawing.Point(9, 66);
            this.llOptions.Name = "llOptions";
            this.llOptions.Size = new System.Drawing.Size(43, 13);
            this.llOptions.TabIndex = 3;
            this.llOptions.TabStop = true;
            this.llOptions.Text = "Options";
            this.llOptions.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.llOptions_LinkClicked);
            // 
            // btPath
            // 
            this.btPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btPath.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btPath.Image = global::sqlnexus.Properties.Resources.openHS;
            this.btPath.ImageAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btPath.Location = new System.Drawing.Point(419, 32);
            this.btPath.Name = "btPath";
            this.btPath.Size = new System.Drawing.Size(25, 21);
            this.btPath.TabIndex = 2;
            this.btPath.UseVisualStyleBackColor = true;
            this.btPath.Click += new System.EventHandler(this.tsbPath_Click);
            // 
            // tsbGo
            // 
            this.tsbGo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.tsbGo.BackColor = System.Drawing.SystemColors.Control;
            this.tsbGo.Enabled = false;
            this.tsbGo.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.tsbGo.Image = global::sqlnexus.Properties.Resources.PlayHS1;
            this.tsbGo.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.tsbGo.Location = new System.Drawing.Point(342, 58);
            this.tsbGo.Name = "tsbGo";
            this.tsbGo.Size = new System.Drawing.Size(75, 25);
            this.tsbGo.TabIndex = 4;
            this.tsbGo.Text = "Import";
            this.tsbGo.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.tsbGo.UseVisualStyleBackColor = false;
            this.tsbGo.Click += new System.EventHandler(this.tsbGo_Click);
            // 
            // laInstructions
            // 
            this.laInstructions.AutoSize = true;
            this.laInstructions.Location = new System.Drawing.Point(12, 6);
            this.laInstructions.Name = "laInstructions";
            this.laInstructions.Size = new System.Drawing.Size(290, 13);
            this.laInstructions.TabIndex = 5;
            this.laInstructions.Text = "Please supply the source path for the files you wish to import";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::sqlnexus.Properties.Resources.otheroptions;
            this.pictureBox1.Location = new System.Drawing.Point(9, 26);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(32, 32);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureBox1.TabIndex = 2;
            this.pictureBox1.TabStop = false;
            // 
            // laPath
            // 
            this.laPath.AutoSize = true;
            this.laPath.Location = new System.Drawing.Point(47, 36);
            this.laPath.Name = "laPath";
            this.laPath.Size = new System.Drawing.Size(68, 13);
            this.laPath.TabIndex = 0;
            this.laPath.Text = "Source path:";
            // 
            // tlpFiles
            // 
            this.tlpFiles.AutoScroll = true;
            this.tlpFiles.BackColor = System.Drawing.Color.White;
            this.tlpFiles.ColumnCount = 3;
            this.tlpFiles.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpFiles.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpFiles.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpFiles.Controls.Add(this.progressBar1, 1, 0);
            this.tlpFiles.Controls.Add(this.linkLabel1, 2, 0);
            this.tlpFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFiles.Location = new System.Drawing.Point(0, 86);
            this.tlpFiles.Name = "tlpFiles";
            this.tlpFiles.RowCount = 1;
            this.tlpFiles.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFiles.Size = new System.Drawing.Size(457, 0);
            this.tlpFiles.TabIndex = 1;
            this.tlpFiles.Visible = false;
            // 
            // progressBar1
            // 
            this.progressBar1.ForeColor = System.Drawing.Color.Blue;
            this.progressBar1.Location = new System.Drawing.Point(0, 0);
            this.progressBar1.Margin = new System.Windows.Forms.Padding(0);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(100, 13);
            this.progressBar1.TabIndex = 0;
            this.progressBar1.Visible = false;
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(103, 0);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(55, 13);
            this.linkLabel1.TabIndex = 1;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "linkLabel1";
            this.linkLabel1.Visible = false;
            // 
            // ssStatus
            // 
            this.ssStatus.AutoSize = false;
            this.ssStatus.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ssText});
            this.ssStatus.Location = new System.Drawing.Point(0, 64);
            this.ssStatus.Name = "ssStatus";
            this.ssStatus.Size = new System.Drawing.Size(457, 22);
            this.ssStatus.TabIndex = 2;
            this.ssStatus.Text = "statusStrip1";
            this.ssStatus.Visible = false;
            // 
            // ssText
            // 
            this.ssText.AutoSize = false;
            this.ssText.Name = "ssText";
            this.ssText.Size = new System.Drawing.Size(442, 17);
            this.ssText.Spring = true;
            this.ssText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // fmImport
            // 
            this.AcceptButton = this.tsbGo;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(457, 86);
            this.Controls.Add(this.ssStatus);
            this.Controls.Add(this.tlpFiles);
            this.Controls.Add(this.paTop);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "fmImport";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Data Import";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.fmImport_FormClosing);
            this.Load += new System.EventHandler(this.fmImport_Load);
            this.paTop.ResumeLayout(false);
            this.paTop.PerformLayout();
            this.cmOptions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.tlpFiles.ResumeLayout(false);
            this.tlpFiles.PerformLayout();
            this.ssStatus.ResumeLayout(false);
            this.ssStatus.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FolderBrowserDialog fb_Path;
        private System.Windows.Forms.Panel paTop;
        private System.Windows.Forms.Label laPath;
        private System.Windows.Forms.TableLayoutPanel tlpFiles;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.StatusStrip ssStatus;
        private System.Windows.Forms.ToolStripStatusLabel ssText;
        private System.Windows.Forms.Label laInstructions;
        private System.Windows.Forms.Button btPath;
        private System.Windows.Forms.Button tsbGo;
        private System.Windows.Forms.LinkLabel llOptions;
        private System.Windows.Forms.ContextMenuStrip cmOptions;
        private System.Windows.Forms.ComboBox cbPath;
        private System.Windows.Forms.ToolStripMenuItem tsiImporters;
        private System.Windows.Forms.ToolStripMenuItem tsiSaveOptions;
        private System.Windows.Forms.ToolStripMenuItem tsiUseDefaultOptions;
        private System.Windows.Forms.ToolStripMenuItem tsiDropDBBeforeImporting;
    }
}