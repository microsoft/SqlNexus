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
            chkTrustServerCertificate.Checked = Properties.Settings.Default.TrustCertificate;
            chkEncryptConnection.Checked =  Properties.Settings.Default.EncryptConnection;
            cmbTheme.SelectedItem = Properties.Settings.Default.Theme; 
        }

        private void EnableSqlLogin(bool enable)
        {
            lblUserName.Enabled = enable;
            lblPassword.Enabled = enable;
            txtUserName.Enabled = enable;
            txtPassword.Enabled = enable;

        }
        private void cmbAuthentication_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbAuthentication.SelectedIndex ==1) 
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
            
            //Saving loginscreen user settings (Encryption choice and Theme)
            Properties.Settings.Default.EncryptConnection = chkEncryptConnection.Checked;
            Properties.Settings.Default.TrustCertificate = chkTrustServerCertificate.Checked;

            Properties.Settings.Default.Theme = cmbTheme.SelectedItem.ToString();

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

        }

        private void chkEncryptConnection_CheckedChanged(object sender, EventArgs e)
        {
            chkTrustServerCertificate.Enabled = chkEncryptConnection.Checked;
        }
    }
}