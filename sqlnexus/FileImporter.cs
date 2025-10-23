using Microsoft.Data.SqlClient;
using NexusInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
namespace sqlnexus
{
    public class RawFileImporter
    {
        FileMgr m_FileManager = new FileMgr();
        string m_ServerName;
        string m_DatabaseName;
        string m_Importpath;
        CSql m_Csql;


        public RawFileImporter(string ServerName, string DatabaseName, string ImportPath)
        {
            
            m_ServerName = ServerName;
            m_DatabaseName = DatabaseName;
            m_Importpath = ImportPath;
            //string ConnString = string.Format("Data Source={0}; Initial Catalog={1};Integrated Security=SSPI", m_ServerName, m_DatabaseName);
            string ConnString = string.Format(Globals.credentialMgr.ConnectionString);
            m_Csql = new CSql(ConnString);
        
            
        }

        private bool IsSafeSqlIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                return false;
            // Must start with a letter or underscore, followed by letters, numbers, spaces, underscores, hyphens, %, #, or $
            // ^[A-Za-z_][A-Za-z0-9 _\-#\%\$]*$
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9 _\-#\%\$]*$"))
                return false;
            return true;
        }

        public string DoImport()
        {
            int fileCntr = 0;
            string retStr = "";

            foreach (RawFile rawfile in m_FileManager.RawFileList)
            {
                CreateTable(rawfile.TableName);

                string[] files = Directory.GetFiles(m_Importpath, rawfile.Mask);

                 foreach (string file in files)
                 {
                    ImportFile(rawfile.TableName, file);
                    fileCntr++;
                }

                
            } //end of foreach

            
            if (fileCntr > 0)
            {
                retStr = fileCntr.ToString() + " raw files processed";
            }
            else
            {
                retStr = "No raw files processed";
            }

            return retStr;
        }

        public void CreateTable(string tableName)
        {

            if (!IsSafeSqlIdentifier(tableName))
            {
                Util.Logger.LogMessage($"DropObject: Unsafe object name '{tableName}'", MessageOptions.Silent);
                throw new ArgumentException("Unsafe object name.");
            }


            string tsqlStr = @"IF OBJECT_ID ('{0}', 'U') IS NULL
                                BEGIN
	                                CREATE TABLE [{0}] (id INT IDENTITY PRIMARY KEY, FileName NVARCHAR(MAX), FileContent NVARCHAR(MAX))
                                END";

            string strSql = string.Format(tsqlStr, tableName);

            Util.Logger.LogMessage(string.Format("Creating table [{0}]", tableName));
            m_Csql.ExecuteSqlScript(strSql);


        }

        public void ImportFile(string tableName, string FileName)
        {


            StreamReader sr = new StreamReader(FileName);
            string content = sr.ReadToEnd();
            //SqlConnection conn = new SqlConnection(string.Format("Data Source={0}; Initial Catalog={1}; Integrated Security=SSPI", m_ServerName, m_DatabaseName));
            SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = string.Format("insert into [{0}] (FileName,FileContent) values (@FileName,@FileContent)", tableName);
            SqlParameter paramFileName = cmd.Parameters.Add("@FileName", System.Data.SqlDbType.NVarChar, -1);
            paramFileName.Value = FileName;
            SqlParameter paramFileContent = cmd.Parameters.Add("@FileContent", System.Data.SqlDbType.NVarChar, -1);
             paramFileContent.Value = content;

             cmd.ExecuteNonQuery();

            
        }


    }
}
