using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlClient;
using NexusInterfaces;
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
            string tsqlStr = @"if OBJECT_ID ('{0}', 'U') is null
                                begin
	                                create table [{0}] (id int identity primary key, FileName nvarchar(max), FileContent nvarchar(max))
                                end";
            //string strSql = string.Format("create table [{0}] (id int identity primary key, FileName nvarchar(max), FileContent nvarchar(max))", tableName);
            string strSql = string.Format(tsqlStr, tableName);

            Util.Logger.LogMessage(string.Format("creating table [{0}]", tableName));
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
