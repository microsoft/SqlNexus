using NexusInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace sqlnexus
{
    public class CustomXELImporter
    {

        //ILogger logger;

        string connStr;
        string server;
        bool usewindowsauth;
        string sqllogin;
        string sqlpassword;
        string databasename;
        string srcPath;
        int countTotalFilesFound = 0;
        int totalRowsAffected = 0;

        public string SQLBaseImport(string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, string srcpath)
        {

            connStr = connString;
            server = Server;
            usewindowsauth = UseWindowsAuth;
            sqllogin = SQLLogin;
            sqlpassword = SQLPassword;
            databasename = DatabaseName;
            srcPath = srcpath;


            int sqlDiagRowsImported = LoadSQLDiaglFiles();
            int alwaysOnRowsImported = LoadAlwaysonHealthFiles();
            int systemHealthRowsImported = LoadSystemHealthFiles();

            string retStr = String.Format("{0} rows imported (SqlDiag {2}, AOHealth {3}, SysHealth {4}) across {1} XEL files.", 
                                totalRowsAffected, 
                                countTotalFilesFound,
                                sqlDiagRowsImported,
                                alwaysOnRowsImported,
                                systemHealthRowsImported);
            return retStr;


        }

        SqlConnection cnn;

        // We can implement following methods more efficiently by combining them into just one method
        // we are just inserting a Raw XEL file into SQL Tables.
        public int LoadSQLDiaglFiles()
        {

            try
            {
                string sqlDiagXelFileToImport = "*_SQLDIAG*.xel";
                string[] XEFiles = Directory.GetFiles(srcPath, sqlDiagXelFileToImport);

                //count the files found to be imported
                int sqlDiagFileCount = XEFiles.Count();
                int sqlDiagRowsImported = 0;

                cnn = new SqlConnection(connStr);
                cnn.Open();

                if (sqlDiagFileCount > 0)
                {
                    //increment total number of files imported from this importer
                    countTotalFilesFound += sqlDiagFileCount;

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
                    totalRowsAffected += sqlDiagRowsImported = cmd.ExecuteNonQuery();

                    Util.Logger.LogMessage(String.Format("Custom XEL import for {0} finished: {1} rows imported from {2} files.", sqlDiagXelFileToImport, sqlDiagRowsImported, sqlDiagFileCount));
                }

                return sqlDiagRowsImported;
            }
            catch (Exception ex)
            {
                Util.Logger.LogMessage("Error importing SQLDiag XEL files: " + ex.Message);
                return -1;
            }
            
            finally
            {
                //cmd.Dispose();
                cnn.Close();
            }



        }

        public int LoadAlwaysonHealthFiles()
        {
            try
            {

                string AOHealthFileToImport = "*AlwaysOn_health*.xel";
                string[] XEFiles = Directory.GetFiles(srcPath, AOHealthFileToImport);

                //count the files found to be imported
                int AlwaysOnFileCount = XEFiles.Count();
                int AlwaysOnRowsImported = 0;

                cnn = new SqlConnection(connStr);
                cnn.Open();
                if (AlwaysOnFileCount > 0)
                {
                    //increment total number of files imported from this importer
                    countTotalFilesFound += AlwaysOnFileCount;

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
                    totalRowsAffected += AlwaysOnRowsImported = cmd.ExecuteNonQuery();


                    Util.Logger.LogMessage(String.Format("Custom XEL import for {0} finished: {1} rows imported from {2} files.", AOHealthFileToImport, AlwaysOnRowsImported, AlwaysOnFileCount));
                }

                return AlwaysOnRowsImported;
            }
            catch (Exception ex)
            {
                Util.Logger.LogMessage("Error importing AlwaysOn Health XEL files: " + ex.Message);
                return -1;
            }
            
            finally
            {
                cnn.Close();
            }
        }


        public int LoadSystemHealthFiles()
        {
            try
            {
                string sysHealthFilesToImport = "*system_health*.xel";
                string[] XEFiles = Directory.GetFiles(srcPath, sysHealthFilesToImport);

                //count the files found to be imported
                int systemHealthFileCount = XEFiles.Count();
                int systemHealthRowsImported = 0;


                cnn = new SqlConnection(connStr);
                cnn.Open();

            
                if (systemHealthFileCount > 0)
                {
                    //increment total number of files imported from this importer
                    countTotalFilesFound += systemHealthFileCount;

                    string XEFile = XEFiles[0];

                    int index = XEFile.IndexOf("system_health");
                    if (index > 0)
                        XEFile = XEFile.Substring(0, index);
                    string sqlstatment = @" IF OBJECT_ID(N'tbl_SQL_Base_SystemHealthXEL_Startup', N'U') IS NOT NULL
                            BEGIN
                            DROP TABLE tbl_SQL_Base_SystemHealthXEL_Startup;
                            END
                            SELECT * INTO tbl_SQL_Base_SystemHealthXEL_Startup FROM sys.fn_xe_file_target_read_file('" + XEFile + "*.XEL', NULL, null, null);";



                    SqlCommand cmd = new SqlCommand(sqlstatment, cnn);
                    cmd.CommandTimeout = 0;
                    totalRowsAffected += systemHealthRowsImported = cmd.ExecuteNonQuery();

                    Util.Logger.LogMessage(String.Format("Custom XEL import for {0} finished: {1} rows imported from {2} files.", sysHealthFilesToImport, systemHealthRowsImported, systemHealthFileCount));
                }

                return systemHealthRowsImported;
            }
            catch(Exception ex)
            {
                Util.Logger.LogMessage("Error importing System Health XEL files: " + ex.Message);
                return -1;
            }
            
            finally
            {
                cnn.Close();
            }

        }

    }//class

}//namespace
        
    
