namespace sqlnexus
{
    partial class fmStartCollectorPrompt
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmStartCollectorPrompt));
            this.label1 = new System.Windows.Forms.Label();
            this.btYes = new System.Windows.Forms.Button();
            this.btNo = new System.Windows.Forms.Button();
            this.ckDontAsk = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // btYes
            // 
            this.btYes.DialogResult = System.Windows.Forms.DialogResult.Yes;
            resources.ApplyResources(this.btYes, "btYes");
            this.btYes.Name = "btYes";
            this.btYes.UseVisualStyleBackColor = true;
            // 
            // btNo
            // 
            this.btNo.DialogResult = System.Windows.Forms.DialogResult.No;
            resources.ApplyResources(this.btNo, "btNo");
            this.btNo.Name = "btNo";
            this.btNo.UseVisualStyleBackColor = true;
            // 
            // ckDontAsk
            // 
            resources.ApplyResources(this.ckDontAsk, "ckDontAsk");
            this.ckDontAsk.Checked = global::sqlnexus.Properties.Settings.Default.RealtimeDontAskAgain;
            this.ckDontAsk.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::sqlnexus.Properties.Settings.Default, "RealtimeDontAskAgain", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.ckDontAsk.Name = "ckDontAsk";
            this.ckDontAsk.UseVisualStyleBackColor = true;
            // 
            // fmStartCollectorPrompt
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.ckDontAsk);
            this.Controls.Add(this.btNo);
            this.Controls.Add(this.btYes);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "fmStartCollectorPrompt";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.Button btYes;
        public System.Windows.Forms.Button btNo;
        public System.Windows.Forms.CheckBox ckDontAsk;
    }
}