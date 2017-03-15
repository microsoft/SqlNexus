namespace sqlnexus
{
    partial class fmPrompt
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmPrompt));
            this.ckDontAsk = new System.Windows.Forms.CheckBox();
            this.btNo = new System.Windows.Forms.Button();
            this.btYes = new System.Windows.Forms.Button();
            this.laPrompt = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // ckDontAsk
            // 
            this.ckDontAsk.AutoSize = true;
            this.ckDontAsk.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.ckDontAsk.Location = new System.Drawing.Point(23, 102);
            this.ckDontAsk.Name = "ckDontAsk";
            this.ckDontAsk.Size = new System.Drawing.Size(162, 17);
            this.ckDontAsk.TabIndex = 8;
            this.ckDontAsk.Text = "&Don\'t ask this question again";
            this.ckDontAsk.UseVisualStyleBackColor = true;
            // 
            // btNo
            // 
            this.btNo.DialogResult = System.Windows.Forms.DialogResult.No;
            this.btNo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btNo.Location = new System.Drawing.Point(513, 98);
            this.btNo.Name = "btNo";
            this.btNo.Size = new System.Drawing.Size(75, 23);
            this.btNo.TabIndex = 7;
            this.btNo.Text = "&No";
            this.btNo.UseVisualStyleBackColor = true;
            // 
            // btYes
            // 
            this.btYes.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.btYes.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btYes.Location = new System.Drawing.Point(432, 98);
            this.btYes.Name = "btYes";
            this.btYes.Size = new System.Drawing.Size(75, 23);
            this.btYes.TabIndex = 6;
            this.btYes.Text = "&Yes";
            this.btYes.UseVisualStyleBackColor = true;
            // 
            // laPrompt
            // 
            this.laPrompt.AutoSize = true;
            this.laPrompt.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.laPrompt.Location = new System.Drawing.Point(103, 30);
            this.laPrompt.Name = "laPrompt";
            this.laPrompt.Size = new System.Drawing.Size(485, 13);
            this.laPrompt.TabIndex = 5;
            this.laPrompt.Text = "Do you want to start the SQLDiag data collector to perform real-time analysis of " +
                "activity on this server?";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::sqlnexus.Properties.Resources.otheroptions;
            this.pictureBox1.Location = new System.Drawing.Point(23, 21);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(60, 58);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 9;
            this.pictureBox1.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Location = new System.Drawing.Point(23, 91);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(565, 4);
            this.panel1.TabIndex = 10;
            // 
            // fmPrompt
            // 
            this.AcceptButton = this.btYes;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btNo;
            this.ClientSize = new System.Drawing.Size(622, 133);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.ckDontAsk);
            this.Controls.Add(this.btNo);
            this.Controls.Add(this.btYes);
            this.Controls.Add(this.laPrompt);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "fmPrompt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Prompt";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.CheckBox ckDontAsk;
        public System.Windows.Forms.Button btNo;
        public System.Windows.Forms.Button btYes;
        private System.Windows.Forms.Label laPrompt;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panel1;
    }
}