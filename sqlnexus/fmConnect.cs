// TODO: remember last machine name, windows/sql auth, and sql login name
#define TRACE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using NexusInterfaces;

namespace sqlnexus
{
    public partial class fmConnect : Form
    {
        private ILogger logger;
        private string cnStr = "";
        public fmConnect(ILogger logger)
        {
            InitializeComponent();
            if (null == logger || !(logger is ILogger))
            {
                MessageBox.Show(Properties.Resources.Error_InvalidLogger);
                Application.Exit();
            }
            this.logger = logger;
            txtSqlServer.Text = Environment.MachineName;
            rbWindowsAuth.Checked = true;
        }

        public string SqlServer 
        {
            get { return txtSqlServer.Text; }
            set { txtSqlServer.Text = value; }
        }

  
        public bool WindowsAuth
        {
            get { return rbWindowsAuth.Checked; }
            set { rbWindowsAuth.Checked = value; }
        }

        public string ConnectionString
        {
            get { return cnStr; }
            set { cnStr = value; }
        }

        private void fmConnect_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.LastUsedServerName.Trim().Length > 0)
                  SqlServer = Properties.Settings.Default.LastUsedServerName.Trim();
            else
                SqlServer = Environment.MachineName;
            
        }

        void LogMessage(string msg)
        {
            System.Diagnostics.Trace.WriteLine(msg);
            if (null != logger)
                logger.LogMessage(msg);
        }

        private void Init()
        {
            rbWindowsAuth.Checked = true;
            
            
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            bool LoginSuccess = true;
            if (SqlServer.Trim().Length == 0)
            {
                logger.LogMessage("You must specify a server name", MessageOptions.Dialog);
                this.DialogResult = DialogResult.None;
                return;
            }
            SqlConnectionStringBuilder cnStrBldr = new SqlConnectionStringBuilder();
            SqlConnection cn = new SqlConnection();
            cnStrBldr.ApplicationName = Application.ProductName;
            cnStrBldr.DataSource = txtSqlServer.Text;
            cnStrBldr.IntegratedSecurity = rbWindowsAuth.Checked;
            //cnStrBldr.UserID = txtSqlLogin.Text;
            //cnStrBldr.Password = txtSqlPassword.Text;
            cnStrBldr.PacketSize = 8000;
            cnStrBldr.PersistSecurityInfo = true;
            cnStrBldr.WorkstationID = Environment.MachineName;
            cnStr = cnStrBldr.ConnectionString;
            cn.ConnectionString = cnStrBldr.ConnectionString;
            cnStrBldr.Password = "********";
            try
            {
                cn.Open();
                
                LogMessage(String.Format("Successfully opened a connection to server {0} from workstation {1} ", SqlServer, Environment.MachineName));
                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                logger.LogMessage (String.Format(Properties.Resources.Error_Connect_Failure, SqlServer,  ex.ToString()), MessageOptions.Silent);
                logger.LogMessage(String.Format(Properties.Resources.Error_Connect_Failure, SqlServer, ex.Message), MessageOptions.Dialog);
                this.DialogResult = DialogResult.None;
                LoginSuccess = false;
                
            }
            finally
            {
                cn.Close();
            }

            if (LoginSuccess)
            {
                Globals.credentialMgr.Server = SqlServer;
                Properties.Settings.Default.LastUsedServerName = SqlServer;
                Properties.Settings.Default.Save();
                Globals.credentialMgr.WindowsAuth = rbWindowsAuth.Checked;
            }


        }

     
     
    }
}
