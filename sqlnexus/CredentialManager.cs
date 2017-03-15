using System;
using System.Collections.Generic;
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using System.Data.SqlClient;
namespace sqlnexus
{
    public class SecureStringEx
    {
        private SecureString m_SecureString;

        public SecureStringEx(String unsecured)
        {
            if (!String.IsNullOrEmpty(unsecured))
            {
                m_SecureString = new SecureString();

                foreach (char c in unsecured.ToCharArray())
                {
                    m_SecureString.AppendChar(c);
                }
                m_SecureString.MakeReadOnly();

            }


        }
        public override string ToString()
        {
            if (null == m_SecureString)
                return null;

            IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(m_SecureString);
            String unsecure = null;
            try
            {
                unsecure = Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);

            }
            return unsecure;


        }
    }
    public class CredentialManager
    {
        private SecureStringEx m_securePassword;
        private String m_UserName;
        private bool m_WindowsAuthentication;
        private String m_SQLServer;
        private String m_DefaultDatabase;

        public String User
        {
            get { return UserName; }
            set { UserName = value; }
        }
        public bool WindowsAuth
        {
            get { return WindowsAuthentication; }
            set { WindowsAuthentication = value; }
        }
        public String Database
        {
            get { return DefaultDatabase; }
            set { DefaultDatabase = value; }
        }
        public String DefaultDatabase
        {
            get { return m_DefaultDatabase; }
            set { m_DefaultDatabase = value; }
        }
        public CredentialManager()
        {
            Init(".", null, null, "sqlnexus", true);
        }
        public CredentialManager(String server, String username, String password, String defaultdb, bool windowsauthentication)
        {
            Init(server, username, password, defaultdb, windowsauthentication);

        }
        private void Init(String server, String username, String password, String defaultdb, bool windowsauthentication)
        {
            Server = server;
            UserName = username;
            Password = password;
            WindowsAuthentication = windowsauthentication;
            DefaultDatabase = defaultdb;
            if (!WindowsAuthentication && (String.IsNullOrEmpty(UserName) || String.IsNullOrEmpty(Password)))
            {
                throw new ArgumentException("You need to provide User Name and Password when using SQL Server authentication. Either user name or password, or both have null or empty values");
            }


        }
        public String ConnectionString
        {
            get
            {
                SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
                csb.DataSource = Server;
                csb.IntegratedSecurity = WindowsAuthentication;
                if (!WindowsAuthentication)
                {
                    csb.UserID = UserName;
                    csb.Password = Password;
                }
                csb.InitialCatalog = DefaultDatabase;
                csb.ConnectTimeout = 15;
                csb.ApplicationName = "SqlNexus";
                //disable connection pooling because alter database cause transport level error
                csb.Pooling = false;

                return csb.ConnectionString;

            }
            set
            {
                SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
                csb.ConnectionString = value;
                Server = csb.DataSource;
                WindowsAuthentication = csb.IntegratedSecurity;
                DefaultDatabase = csb.InitialCatalog;
                UserName = csb.UserID;
                Password = csb.Password;
            }
        }
        public string Password
        {
            set
            {
                m_securePassword = new SecureStringEx(value);
            }
            get
            {
                if (m_securePassword == null)
                    return null;
                return m_securePassword.ToString();
            }
        }
        public String UserName
        {
            get { return m_UserName; }
            set { m_UserName = value; }
        }
        public bool WindowsAuthentication
        {
            get { return m_WindowsAuthentication; }
            set { m_WindowsAuthentication = value; }
        }
        public String Server
        {
            get { return m_SQLServer; }
            set { m_SQLServer = value; }
        }
        public bool VerifyCredential()
        {
            bool connectSuccess = true;

            SqlConnection conn = new SqlConnection(ConnectionString);
            try
            {
                conn.Open();
            }
            catch (SqlException sqlex)
            {
                //Console.WriteLine(sqlex.ToString());
                connectSuccess = false;
                throw;
            }

            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return connectSuccess;

        }

    }

}