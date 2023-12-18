using System;
using System.Collections.Generic;
using System.Text;
using NexusInterfaces;
using Microsoft.Data;
using Microsoft.Data.SqlClient;

namespace sqlnexus
{
    class NexusInfo
    {
        //private static bool m_SchemaCreated = false;
        private ILogger m_logger;
        private string m_ConnectionString;

        public NexusInfo(String connString, ILogger logger )
        {
            m_ConnectionString = connString;
            m_logger = logger;
        }

        public bool HasNexusInfo()
        {
            bool tableExists = false;
            SqlConnection conn = new SqlConnection(m_ConnectionString);
            SqlCommand cmd = new SqlCommand("select count(*) 'cnt' from sys.objects where name = 'tblNexusInfo' and type = 'U'", conn);
            try
            {
                conn.Open();
                Int32 cnt = (Int32)cmd.ExecuteScalar();
                if (cnt > 0)
                    tableExists = true;
            }
            catch (SqlException sqlex)
            {
                m_logger.LogMessage(sqlex.ToString(), MessageOptions.Both);
            }
            finally
            {
                conn.Close();

            }
            return tableExists;
        }

        public void SetAttribute(String Attribute, String Val)
        { 
            SqlConnection conn = new SqlConnection (m_ConnectionString);
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = conn;
            try
            {
                conn.Open();
                //cmd.CommandText = "if not exists (select * from sys.objects where name = 'tblNexusInfo' and type = 'U') create table dbo.tblNexusInfo (Attribute nvarchar (200), Value nvarchar(2048))";
                //cmd.ExecuteNonQuery();
                cmd.CommandText = "merge tblNexusInfo as target  using (values (@Attribute, @Value) ) as source (Attribute, Value)  on target.Attribute = source.Attribute  when matched then update set target.[Value] = source.[Value] when not matched then insert (Attribute, [Value]) values (source.Attribute, source.Value);";
                SqlParameter attrib = new SqlParameter("@Attribute", Attribute);
                SqlParameter attrib_value = new SqlParameter("@Value", Val);
                cmd.Parameters.Add(attrib);
                cmd.Parameters.Add(attrib_value);
                cmd.ExecuteNonQuery();

            }
            catch (SqlException sqlex)
            {
                m_logger.LogMessage(string.Format ("unable to set Nexus Info attribute {0} with exception {1}", Attribute, sqlex.ToString()), MessageOptions.Both);

            }
            finally
            {
                conn.Close();
            }


            

        }

    }
}
