using System;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using NexusInterfaces;

namespace sqlnexus
{
    public partial class fmLoginEx : Form
    {
        public fmLoginEx()
        {
            InitializeComponent();

            ThemeManager.ApplyTheme(this);
            chkTrustServerCertificate.Checked = Properties.Settings.Default.TrustCertificate;
            chkEncryptConnection.Checked = Properties.Settings.Default.EncryptConnection;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#848484");
        }

        private void EnableSqlLogin(bool enable)
        {
            // Keep labels always enabled so ForeColor is respected by WinForms rendering.
            // WinForms disabled labels ignore ForeColor and draw with a system-derived dark
            // gray that is invisible on dark theme backgrounds (e.g., Aquatic #202020).
            // Instead, simulate the disabled look by setting a muted ForeColor.
            lblUserName.Enabled = true;
            lblPassword.Enabled = true;
            txtUserName.Enabled = enable;
            txtPassword.Enabled = enable;

            // WinForms disabled labels ignore ForeColor and render with a system-derived
            // dark gray, which is invisible on dark theme backgrounds (e.g., Aquatic).
            // Explicitly set a muted but visible color when disabled.
            if (!enable)
            {
                Color dimmed = ThemeManager.CurrentThemeName == "Aquatic"
                    ? ColorTranslator.FromHtml("#808080")   // medium gray on dark bg
                    : ThemeManager.CurrentThemeName == "Desert"
                        ? ColorTranslator.FromHtml("#A0A0A0") // lighter gray on warm bg
                        : SystemColors.GrayText;              // default system disabled color

                lblUserName.ForeColor = dimmed;
                lblPassword.ForeColor = dimmed;
            }
            else
            {
                lblUserName.ForeColor = ThemeManager.CurrentForeColor;
                lblPassword.ForeColor = ThemeManager.CurrentForeColor;
            }
        }
        private void cmbAuthentication_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbAuthentication.SelectedIndex == 1)
            {
                EnableSqlLogin(true);
            }
            else
            {
                EnableSqlLogin(false);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {

            Globals.credentialMgr = new CredentialManager(txtServerName.Text, txtUserName.Text, txtPassword.Text, "master", (cmbAuthentication.SelectedIndex == 0 ? true : false), chkTrustServerCertificate.Checked, chkEncryptConnection.Checked);


            try
            {

                if (Globals.credentialMgr.VerifyCredential())
                {

                    this.DialogResult = DialogResult.OK;


                    Util.Logger.LogMessage(String.Format("Successfully opened a connection to server {0} from workstation {1} ", Globals.credentialMgr.Server, Environment.MachineName));
                    Properties.Settings.Default.LastUsedServerName = Globals.credentialMgr.Server;
                    Properties.Settings.Default.Save();
                    this.DialogResult = DialogResult.OK;
                }

            }
            catch (Exception ex)
            {
                Util.Logger.LogMessage(String.Format(Properties.Resources.Error_Connect_Failure, Globals.credentialMgr.Server, ex.ToString()), MessageOptions.Silent);
                Util.Logger.LogMessage(String.Format(Properties.Resources.Error_Connect_Failure, Globals.credentialMgr.Server, ex.Message), MessageOptions.Dialog);
                this.DialogResult = DialogResult.None;

            }
            finally
            {

                txtPassword.Text = "";//since this object is cached, erase the password
            }

            //Saving trustcertificate & encrypt connection for the user.
            Properties.Settings.Default.EncryptConnection = chkEncryptConnection.Checked;
            Properties.Settings.Default.TrustCertificate = chkTrustServerCertificate.Checked;
            Properties.Settings.Default.Save();

            //this.Dispose();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
        }

        private void fmLoginEx_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.LastUsedServerName.Trim().Length > 0)
                txtServerName.Text = Properties.Settings.Default.LastUsedServerName.Trim();
            else
                txtServerName.Text = Environment.MachineName;

            chkEncryptConnection.Checked = Properties.Settings.Default.EncryptConnection;
            chkTrustServerCertificate.Checked = Properties.Settings.Default.TrustCertificate;
            chkTrustServerCertificate.Enabled = chkEncryptConnection.Checked;
        }

        private void chkEncryptConnection_CheckedChanged(object sender, EventArgs e)
        {
            chkTrustServerCertificate.Enabled = chkEncryptConnection.Checked;
        }

        private void btnCancel_Enter(object sender, EventArgs e)
        {
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#0078D7");
            btnCancel.BackColor = ColorTranslator.FromHtml("#E5F1FB");
        }
        private void btnCancel_Leave(object sender, EventArgs e)
        {
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#848484");
        }

        private void btnCancel_MouseHover(object sender, EventArgs e)
        {
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#0078D7");
            btnCancel.BackColor = ColorTranslator.FromHtml("#E5F1FB");
        }

        private void btnCancel_MouseLeave(object sender, EventArgs e)
        {
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#848484");
        }

        private void btnConnect_Leave(object sender, EventArgs e)
        {
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#848484");
        }

        private void btnConnect_Enter(object sender, EventArgs e)
        {
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#0078D7");
            btnConnect.BackColor = ColorTranslator.FromHtml("#E5F1FB");

        }

        private void fmLoginEx_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
    }
}