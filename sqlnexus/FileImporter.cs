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
        readonly List<string> m_SearchPaths;
        CSql m_Csql;


        public RawFileImporter(string ServerName, string DatabaseName, string ImportPath)
        {

            m_ServerName = ServerName;
            m_DatabaseName = DatabaseName;
            m_Importpath = ImportPath;
            // Search the primary import folder plus the sibling SharedOutputFiles folder when it
            // exists (non-instance-specific host files such as event logs, tasklist, drivers land
            // there). When the sibling does not exist this is just the primary folder, so behavior
            // is unchanged.
            m_SearchPaths = SharedOutputFolder.GetImportSearchPaths(ImportPath);
            if (m_SearchPaths.Count == 0)
                m_SearchPaths.Add(ImportPath);
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

                // Track file names already imported for THIS mask from the primary folder so a
                // same-named file discovered in the sibling SharedOutputFiles folder is not imported
                // again into the same table (silent duplicate rows). SQL LogScout writes host/OS files
                // to EITHER folder exclusively, so this is a safety net; the primary folder wins.
                var importedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int idx = 0; idx < m_SearchPaths.Count; idx++)
                {
                    string searchPath = m_SearchPaths[idx];
                    if (string.IsNullOrEmpty(searchPath) || !Directory.Exists(searchPath))
                        continue;

                    bool isSharedFolder = idx > 0; // index 0 is always the primary import folder
                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(searchPath, rawfile.Mask);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is PathTooLongException)
                    {
                        // A restrictive-permission / too-long / transient-IO folder (often the sibling
                        // SharedOutputFiles the user never selected) must not abort the whole raw-file
                        // import. Log it and carry on with the remaining folders.
                        Util.Logger.LogMessage(
                            "Unable to enumerate '" + rawfile.Mask + "' in '" + searchPath + "': " +
                            ex.Message + " - skipping this folder and continuing.", MessageOptions.All);
                        continue;
                    }

                    // For the sibling folder, drop any file whose name was already imported from the
                    // primary folder (reuses the same name-based, unit-tested rule as the other
                    // importers). Primary wins; the duplicate is logged, not silently imported twice.
                    string[] filesToImport = files;
                    if (isSharedFolder)
                    {
                        filesToImport = SharedOutputFolder.GetSiblingOnlyFiles(importedNames, files).ToArray();

                        foreach (string dup in files.Except(filesToImport, StringComparer.OrdinalIgnoreCase))
                        {
                            Util.Logger.LogMessage(
                                "Shared folder: skipping duplicate file '" + Path.GetFileName(dup) +
                                "' found in " + SharedOutputFolder.SharedFolderName + " - a file with the " +
                                "same name is already being imported from the primary folder (avoids duplicate rows).",
                                MessageOptions.Both);
                        }
                    }

                    foreach (string file in filesToImport)
                    {
                        ImportFile(rawfile.TableName, file);
                        importedNames.Add(Path.GetFileName(file));
                        fileCntr++;
                    }
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
