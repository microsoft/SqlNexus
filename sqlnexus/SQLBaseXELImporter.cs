using NexusInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace sqlnexus
{
   public class SQLBaseXELImporter 
    {
      

        //ILogger logger;
        
        string connStr;
        string server;
        bool usewindowsauth;
        string sqllogin;
        string sqlpassword;
        string databasename;
        string srcPath;
        

        public void SQLBaseImport(string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName,string srcpath)
        {
            
            connStr = connString;
            server = Server;
            usewindowsauth = UseWindowsAuth;
            sqllogin = SQLLogin;
            sqlpassword = SQLPassword;
            databasename = DatabaseName;
            srcPath = srcpath;


            loadSQLDiaglFiles();
            loadAlwaysonHealthFiles();
            loadSystemHealthFiles();



        }
        string[] SQLBASE = { "SQL_Base_SQLDIAGXEL_Startup", "AlwaysOn_Basic_Info_AlwaysOnHealth_XEL_Startup", "SQL_Base_SystemHealthXEL_Startup_system_health" };
        SqlConnection cnn;

        // We can implement following methods more efficiently by combining them into just one method
        // we are just inserting a Raw XEL file into SQL Tables.
        public void loadSQLDiaglFiles()
        {

            try
            {
                string[] XEFiles = Directory.GetFiles(srcPath, "*SQL_Base_SQLDIAGXEL_Startup*.xel");

                cnn = new SqlConnection(connStr);
                cnn.Open();

                if (XEFiles.Count() > 0)
                {
                    string XEFile = XEFiles[0];
                    int index = XEFile.IndexOf("_SQLDIAG_");
                    if (index > 0)
                        XEFile = XEFile.Substring(0, index);

                    string sqlstatment = @"IF OBJECT_ID(N'tbl_SQL_Base_SQLDIAGXEL_Startup', N'U') IS NOT NULL
                            BEGIN
                            DROP TABLE tbl_SQL_Base_SQLDIAGXEL_Startup;
                            END
                            SELECT * INTO tbl_SQL_Base_SQLDIAGXEL_Startup FROM sys.fn_xe_file_target_read_file('" + XEFile + "*.XEL', NULL, null, null);";



                    SqlCommand cmd = new SqlCommand(sqlstatment, cnn);
                    cmd.CommandTimeout = 0;
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {

            }
            finally
            {
                //cmd.Dispose();
                cnn.Close();
            }
            


        }

        public void loadAlwaysonHealthFiles()
        {
            try
            {

                string[] XEFiles = Directory.GetFiles(srcPath, "*AlwaysOnHealth_XEL_Startup_AlwaysOn_health*.xel");
                
                cnn = new SqlConnection(connStr);
                cnn.Open();
                if (XEFiles.Count() > 0)                   
                {
                    string XEFile = XEFiles[0];

                    int index = XEFile.IndexOf("AlwaysOn_health");
                    if (index > 0)
                        XEFile = XEFile.Substring(0, index);

                    string sqlstatment = @"IF OBJECT_ID(N'tbl_SQL_Base_AlwaysOnHealth', N'U') IS NOT NULL
                             BEGIN
                            DROP TABLE tbl_SQL_Base_AlwaysOnHealth;
                                END
                            SELECT * INTO tbl_SQL_Base_AlwaysOnHealth FROM sys.fn_xe_file_target_read_file('" + XEFile + "*.XEL', NULL, null, null);";
                    SqlCommand cmd = new SqlCommand(sqlstatment, cnn);
                    cmd.CommandTimeout = 0;
                    cmd.ExecuteNonQuery();
                }
            }
            catch {
                
            }
            finally
            {
                cnn.Close();
            }
            
            

        }
        public void loadSystemHealthFiles()
        {
            string[] XEFiles = Directory.GetFiles(srcPath, "*SQL_Base_SystemHealthXEL_Startup_system_health*.xel");
            cnn = new SqlConnection(connStr);
            cnn.Open();
            try
            {
                if (XEFiles.Count() > 0)
                {
                    string XEFile = XEFiles[0];

                    int index = XEFile.IndexOf("Startup_system_health");
                    if (index > 0)
                        XEFile = XEFile.Substring(0, index);
                    string sqlstatment = @" IF OBJECT_ID(N'tbl_SQL_Base_SystemHealthXEL_Startup', N'U') IS NOT NULL
                            BEGIN
                            DROP TABLE tbl_SQL_Base_SystemHealthXEL_Startup;
                            END
                            SELECT * INTO tbl_SQL_Base_SystemHealthXEL_Startup FROM sys.fn_xe_file_target_read_file('" + XEFile + "*.XEL', NULL, null, null);";



                    SqlCommand cmd = new SqlCommand(sqlstatment, cnn);
                    cmd.CommandTimeout = 0;
                    cmd.ExecuteNonQuery();

                }
            }
            catch 
            {

                cnn.Close();
            }
            finally
            {
                cnn.Close();
            }

           
                
                
            }

        }

    }
        
    
