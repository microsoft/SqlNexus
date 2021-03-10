using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace NexusInterfaces
{
    public class CSql
    {
        private bool m_Success=true;
        private StringBuilder m_ErrorMessages;
        private SqlConnectionStringBuilder m_ConnStringBuilder;
        
        public bool IsSuccess
        {
            get { return m_Success; }
        }
        public string ErrorMessages
        {
            get { return m_ErrorMessages.ToString(); }
        }

        public CSql(String connString)
        {
            //use it to validate teh connection string;
            m_ConnStringBuilder = new SqlConnectionStringBuilder();
            m_ConnStringBuilder.ConnectionString = connString;
            ReInit();
        }

        private void ReInit()
        {
            m_Success = true; //assume true initially
            m_ErrorMessages = new StringBuilder();


        }
        public void  ExecuteSqlScript(String script)
        {
            string dlgTitle = "SQL Script Execution Failure";
            ExecuteBatches(ParseBatches(script));

            Util.Logger.LogMessage (m_ErrorMessages.ToString(), MessageOptions.Silent, ( (m_Success == true ) ? TraceEventType.Information: TraceEventType.Error), dlgTitle);
        }
        private String[] ParseBatches(string scriptText)
        {
            Regex blank = new Regex("\r\n\\s+\rn");
            scriptText = blank.Replace(scriptText, "");
            Regex reg1 = new Regex("\r\n\\s*go\\s*\r\n*", RegexOptions.IgnoreCase);

            return reg1.Split(scriptText);

        }
        public DataTable GetDataTable (string sql)
        {
            DataTable dt = new DataTable();
            SqlConnection conn = new SqlConnection (m_ConnStringBuilder.ConnectionString);
            conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
            try 
            {
                SqlDataAdapter da = new SqlDataAdapter();
                SqlCommand cmd = new SqlCommand (sql, conn);
                da.SelectCommand = cmd;
                
                da.Fill (dt);
                //m_Success = true;
            }
            catch (Exception ex)
            {
                string dlgTitle = "Failed to Get DataTable";
                m_Success = false;
                m_ErrorMessages.AppendFormat("{0} \r\n", ex.ToString());
                Util.Logger.LogMessage (m_ErrorMessages.ToString(), MessageOptions.Silent, (m_Success == true? TraceEventType.Information: TraceEventType.Error), dlgTitle);
                throw ex;
            }
            
            return dt;

        }
        private  void ExecuteBatches(string[] batches)
        {
            string batchText;
            SqlConnection conn = new SqlConnection(m_ConnStringBuilder.ConnectionString);
            conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
            conn.Open();  //we want this exception to pop up when we can't make a connection
            Int32 BatchesExecuted = 0;
            try
            {

                foreach (String bat in batches)
                {
                    if (bat.Trim().Length <= 0)
                        continue;
                    try
                    {
                        BatchesExecuted++;
                        SqlCommand cmd = new SqlCommand();
                        cmd.CommandText = bat;
                        cmd.Connection = conn;
                        cmd.CommandTimeout = 0;

                        //printing the fact that batch is being executed
                        if (String.IsNullOrEmpty(bat))
                            batchText = "Empty Batch";
                        else if (bat.Length <= 100)
                            batchText = bat;
                        else
                            batchText = bat.Substring(0, 100);

                        batchText = batchText.Replace("\r\n", " ");
                        batchText = batchText.Replace("*****", "*");
                        batchText = batchText.Replace("  ", " ");

                        int position = batchText.IndexOf("owner:");
                        if (position > -1)
                        {
                            //batchText = batchText.Substring(0, position) + batchText.Substring(position + 20);
                            batchText = "owner of script found here";
                        }
                        m_ErrorMessages.AppendFormat("Starting execution of {0} \r\n", batchText);

                        SqlDataReader dr = cmd.ExecuteReader();
                        while (dr.NextResult())
                        {
                            m_ErrorMessages.AppendFormat("{0} \r\n", GetStringFromReader(dr));
                        }
                        
                        
                        if (!dr.IsClosed)
                            dr.Close();
                        //m_Success=true;
                    }
                    catch (SqlException sqlex)
                    {
                        m_Success = false;
                        m_ErrorMessages.AppendFormat("{0} \r\n", sqlex.ToString());
                    }
                    catch  (Exception ex)
                    {
                        m_Success = false;
                        m_ErrorMessages.AppendFormat("{0} \r\n", ex.ToString());
                        throw ex;
                    }
                }

            }
            finally
            {
                conn.Close();
                m_ErrorMessages.AppendLine("Batches Executed " + BatchesExecuted);
            }



        }

        void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {
            foreach (SqlError err in args.Errors)
            {
                m_ErrorMessages.AppendFormat("{0} \r\n", err.Message);
            }
        }

        String GetStringFromReader(SqlDataReader reader)
        {
            StringBuilder sb = new StringBuilder();
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    sb.AppendFormat("{0}\t", reader.GetValue(i).ToString());
                    
                }
                sb.Append("\r\n");

            }
            
            return sb.ToString();
        }

    }



        
    }

