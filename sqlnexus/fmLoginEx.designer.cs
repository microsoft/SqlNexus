namespace sqlnexus
{
    partial class fmLoginEx
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
            this.lblServerName = new System.Windows.Forms.Label();
            this.lblAuthentication = new System.Windows.Forms.Label();
            this.lblUserName = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtServerName = new System.Windows.Forms.TextBox();
            this.cmbAuthentication = new System.Windows.Forms.ComboBox();
            this.txtUserName = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.chkTrustServerCertificate = new System.Windows.Forms.CheckBox();
            this.chkEncryptConnection = new System.Windows.Forms.CheckBox();
            this.cmbTheme = new System.Windows.Forms.ComboBox();
            this.lblThemea = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblServerName
            // 
            this.lblServerName.AutoSize = true;
            this.lblServerName.Location = new System.Drawing.Point(112, 72);
            this.lblServerName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblServerName.Name = "lblServerName";
            this.lblServerName.Size = new System.Drawing.Size(101, 20);
            this.lblServerName.TabIndex = 100;
            this.lblServerName.Text = "Server Name";
            // 
            // lblAuthentication
            // 
            this.lblAuthentication.AutoSize = true;
            this.lblAuthentication.Location = new System.Drawing.Point(112, 122);
            this.lblAuthentication.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblAuthentication.Name = "lblAuthentication";
            this.lblAuthentication.Size = new System.Drawing.Size(112, 20);
            this.lblAuthentication.TabIndex = 200;
            this.lblAuthentication.Text = "Authentication";
            // 
            // lblUserName
            // 
            this.lblUserName.AutoSize = true;
            this.lblUserName.Enabled = false;
            this.lblUserName.Location = new System.Drawing.Point(165, 172);
            this.lblUserName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(89, 20);
            this.lblUserName.TabIndex = 300;
            this.lblUserName.Text = "User Name";
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Enabled = false;
            this.lblPassword.Location = new System.Drawing.Point(165, 215);
            this.lblPassword.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(78, 20);
            this.lblPassword.TabIndex = 400;
            this.lblPassword.Text = "Password";
            // 
            // txtServerName
            // 
            this.txtServerName.AccessibleName = "Server Name";
            this.txtServerName.Location = new System.Drawing.Point(260, 61);
            this.txtServerName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtServerName.Name = "txtServerName";
            this.txtServerName.Size = new System.Drawing.Size(247, 26);
            this.txtServerName.TabIndex = 0;
            // 
            // cmbAuthentication
            // 
            this.cmbAuthentication.FormattingEnabled = true;
            this.cmbAuthentication.Items.AddRange(new object[] {
            "Windows Authentication",
            "SQL Server Authentication"});
            this.cmbAuthentication.Location = new System.Drawing.Point(260, 111);
            this.cmbAuthentication.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbAuthentication.Name = "cmbAuthentication";
            this.cmbAuthentication.Size = new System.Drawing.Size(247, 28);
            this.cmbAuthentication.TabIndex = 1;
            this.cmbAuthentication.Text = "Windows Authentication";
            this.cmbAuthentication.SelectedIndexChanged += new System.EventHandler(this.cmbAuthentication_SelectedIndexChanged);
            // 
            // txtUserName
            // 
            this.txtUserName.AccessibleName = "UserName";
            this.txtUserName.Enabled = false;
            this.txtUserName.Location = new System.Drawing.Point(260, 161);
            this.txtUserName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtUserName.Name = "txtUserName";
            this.txtUserName.Size = new System.Drawing.Size(247, 26);
            this.txtUserName.TabIndex = 2;
            // 
            // txtPassword
            // 
            this.txtPassword.AccessibleName = "Password";
            this.txtPassword.Enabled = false;
            this.txtPassword.Location = new System.Drawing.Point(260, 211);
            this.txtPassword.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(247, 26);
            this.txtPassword.TabIndex = 3;
            // 
            // btnConnect
            // 
            this.btnConnect.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnConnect.Location = new System.Drawing.Point(117, 337);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(170, 35);
            this.btnConnect.TabIndex = 6;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            this.btnConnect.Enter += new System.EventHandler(this.btnConnect_Enter);
            this.btnConnect.Leave += new System.EventHandler(this.btnConnect_Leave);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(387, 337);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(170, 35);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            this.btnCancel.Enter += new System.EventHandler(this.btnCancel_Enter);
            this.btnCancel.Leave += new System.EventHandler(this.btnCancel_Leave);
            this.btnCancel.MouseLeave += new System.EventHandler(this.btnCancel_Leave);
            this.btnCancel.MouseHover += new System.EventHandler(this.btnCancel_MouseHover);
            // 
            // chkTrustServerCertificate
            // 
            this.chkTrustServerCertificate.AutoSize = true;
            this.chkTrustServerCertificate.Location = new System.Drawing.Point(400, 303);
            this.chkTrustServerCertificate.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.chkTrustServerCertificate.Name = "chkTrustServerCertificate";
            this.chkTrustServerCertificate.Size = new System.Drawing.Size(197, 24);
            this.chkTrustServerCertificate.TabIndex = 5;
            this.chkTrustServerCertificate.Text = "Trust Server Certificate";
            this.chkTrustServerCertificate.UseVisualStyleBackColor = true;
            // 
            // chkEncryptConnection
            // 
            this.chkEncryptConnection.AutoSize = true;
            this.chkEncryptConnection.Checked = true;
            this.chkEncryptConnection.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkEncryptConnection.Location = new System.Drawing.Point(116, 303);
            this.chkEncryptConnection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.chkEncryptConnection.Name = "chkEncryptConnection";
            this.chkEncryptConnection.Size = new System.Drawing.Size(174, 24);
            this.chkEncryptConnection.TabIndex = 4;
            this.chkEncryptConnection.Text = "Encrypt Connection";
            this.chkEncryptConnection.UseVisualStyleBackColor = true;
            this.chkEncryptConnection.CheckedChanged += new System.EventHandler(this.chkEncryptConnection_CheckedChanged);
            // 
            // cmbTheme
            // 
            this.cmbTheme.AccessibleDescription = "Pick your layout theme";
            this.cmbTheme.FormattingEnabled = true;
            this.cmbTheme.DataSource = ThemeManager.Themes;
            this.cmbTheme.Location = new System.Drawing.Point(260, 256);
            this.cmbTheme.Name = "cmbTheme";
            this.cmbTheme.Size = new System.Drawing.Size(247, 28);
            this.cmbTheme.TabIndex = 401;
            this.cmbTheme.SelectedIndexChanged += new System.EventHandler(this.cmbTheme_SelectedIndexChanged);
            // 
            // lblThemea
            // 
            this.lblThemea.AutoSize = true;
            this.lblThemea.Location = new System.Drawing.Point(169, 256);
            this.lblThemea.Name = "lblThemea";
            this.lblThemea.Size = new System.Drawing.Size(58, 20);
            this.lblThemea.TabIndex = 402;
            this.lblThemea.Text = "Theme";
            // 
            // fmLoginEx
            // 
            this.AcceptButton = this.btnConnect;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(630, 432);
            this.Controls.Add(this.lblThemea);
            this.Controls.Add(this.cmbTheme);
            this.Controls.Add(this.chkEncryptConnection);
            this.Controls.Add(this.chkTrustServerCertificate);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.txtUserName);
            this.Controls.Add(this.cmbAuthentication);
            this.Controls.Add(this.txtServerName);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.lblUserName);
            this.Controls.Add(this.lblAuthentication);
            this.Controls.Add(this.lblServerName);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "fmLoginEx";
            this.Text = "Connect to Server";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.fmLoginEx_FormClosing);
            this.Load += new System.EventHandler(this.fmLoginEx_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblServerName;
        private System.Windows.Forms.Label lblAuthentication;
        private System.Windows.Forms.Label lblUserName;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtServerName;
        private System.Windows.Forms.ComboBox cmbAuthentication;
        private System.Windows.Forms.TextBox txtUserName;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox chkTrustServerCertificate;
        private System.Windows.Forms.CheckBox chkEncryptConnection;
        private System.Windows.Forms.ComboBox cmbTheme;
        private System.Windows.Forms.Label lblThemea;
    }
}