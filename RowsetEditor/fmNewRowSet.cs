using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Data.Common;

namespace RowsetEditor
{
    public partial class fmNewRowset : Form
    {
        public String newRowsetName;
        public String newIdentifier;
        public XmlNode xKnownColumns;
        private SqlConnection sqlServerConnection;
        private String sqlConnectionString;
        public DataTable tblQueryColumns;

        public fmNewRowset()
        {
            InitializeComponent();
            tabMain.SelectTab("tabGrid");
            tblQueryColumns = new DataTable("QueryColumns");
            tblQueryColumns.Columns.Add("Name");
            tblQueryColumns.Columns.Add("Type");
            tblQueryColumns.Columns.Add("Length");

            dgvNewRowset.DataSource = tblQueryColumns;
        }

        private void btnReturn_Click(object sender, EventArgs e)
        {
            newRowsetName = txtRowsetName.Text;
            newIdentifier = txtIdentifier.Text;
            if (chkFromQuery.Checked)
            {
                if (dgvNewRowset.Rows.Count>0)
                {
                    this.Close();
                } else
                {
                    MessageBox.Show("Please run a query that returns rows");
                    
                }
                
            } else
            {
                
                //btnExec.PerformClick();
                this.Close();
               // xKnownColumns = new XmlNode("Node");
                return;
            }
        }

        private void chkFromQuery_CheckedChanged(object sender, EventArgs e)
        {
            if (chkFromQuery.Checked)
            {
                if ( String.IsNullOrEmpty(txtIdentifier.Text) || String.IsNullOrEmpty(txtRowsetName.Text))
                {
                    MessageBox.Show("Please enter a Table Name and Idnetifier before proceeding...");
                    chkFromQuery.Checked = false;
                    return;
                }
                txtServerName.Text = System.Environment.MachineName;
                tabMain.SelectTab ("tabConnect");
                //connectToSQL();
                //tabMain.SelectTab("tabSQL");
                //txtQuery.Enabled = chkFromQuery.Checked;
            }
            
            
        }


        private Boolean connectToSQL(String SQLName)
        {
            String serverName = SQLName;

            // Create a connection string
            sqlConnectionString = $"Data Source={serverName};Initial Catalog=Master;Integrated Security=True;TrustServerCertificate=True";

            // Create a SqlConnection object
            sqlServerConnection = new SqlConnection(sqlConnectionString);
            using (sqlServerConnection)
            {
                // Open the connection
                try
                {
                    sqlServerConnection.Open();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(String.Concat(ex.Message, "\n Connection String \n", sqlConnectionString));
                    return false;
                }
            }
            return true;
        }
        private void connectToSQL()
        {
            Form fmGetServer = new Form
            {
                Text = "SQL Server Connection",
                Size = new Size(300, 200),
                //BackColor = Color.LightBlue
            };

            TextBox txtServerName = new TextBox
            {
                Location = new Point(50, 60),
                Width = 200
                
            };
            txtServerName.Text = System.Environment.MachineName;
            txtServerName.Focus();

            fmGetServer.Controls.Add(txtServerName);

            Button btnOK = new Button

            {
                Text = "Connect",
                Location = new Point(200, 100)
            };

            btnOK.Click += new EventHandler(btnOK_Click);

            fmGetServer.Controls.Add(btnOK);
            fmGetServer.ShowDialog();

            return;
            void btnOK_Click(object sender, EventArgs e)
            {
                String serverName = txtServerName.Text;
      
                // Create a connection string
                sqlConnectionString = $"Data Source={serverName};Initial Catalog=Master;Integrated Security=True;TrustServerCertificate=True";

                // Create a SqlConnection object
                sqlServerConnection = new SqlConnection(sqlConnectionString);
                using (sqlServerConnection )
                {
                    // Open the connection
                    try
                    {
                        sqlServerConnection.Open();
                        //MessageBox.Show("Connection successful!");
                        fmGetServer.Close();
                        fmGetServer.Dispose();

                    } catch (Exception ex )
                    {
                        MessageBox.Show(String.Concat(ex.Message, "\n Connection String \n", sqlConnectionString));
                    }
                }
                return;
            }
        }

        private void btnExec_Click(object sender, EventArgs e)
        {
            if (sqlServerConnection != null )
            {
                if ( sqlServerConnection.State == ConnectionState.Open)
                {
                    //MessageBox.Show("Hurray Open connection");
                } else
                {
                    sqlServerConnection.ConnectionString = sqlConnectionString;
                    sqlServerConnection.Open();
                    //MessageBox.Show("Opened now");
                }

                String queryString = txtQuery.Text;
                try
                {
                    // Create a SqlCommand object
                    using (SqlCommand command = new SqlCommand(queryString, sqlServerConnection))
                    {
                        // Create a SqlDataReader object
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            //We don't need to Loop through the rows of the SqlDataReader object
                            //we will only loop through the fields to identify their types and names.


                            var schemaTable = reader.GetSchemaTable();
                            foreach (DataRow schemaRow in schemaTable.Rows)
                            {
                                //String name = r.Field<string>("ColumnName");
                                //String type = r.Field<string>("DataTypeName");
                                //int colSize = r.Field<int>("ColumnSize");

                            //}

                            //for (int i = 0; i < reader.FieldCount; i++)
                            //{
                                String colName = schemaRow.Field<string>("ColumnName");// reader.GetName(i);
                                String colTypeName = schemaRow.Field<string>("DataTypeName");// reader.GetDataTypeName(i);
                                Type colType = schemaRow.Field<Type>("DataType");//reader.GetFieldType(i);
                                String colLength = "";

                                if (colType == typeof(String))
                                {
                                    colLength = schemaRow.Field<int>("ColumnSize").ToString() ;
                                }
                                
                                String colRowsetType;
                                
                                switch (colTypeName.ToLower())
                                {
                                    case "smallint" or "int" or "tinyint":
                                        colRowsetType = "IntColumn";
                                        break;
                                    case "bigint":
                                        colRowsetType = "BigIntColumn";
                                        break;
                                    case "varchar" or "nvarchar":
                                        colRowsetType = "VarCharColumn";
                                        break;
                                    case "float" or "double":
                                        colRowsetType = "FloatColumn";
                                        break;
                                    case "datetime":
                                        colRowsetType = "DateTimeColumn";
                                        break;
                                    case "varbinary":
                                        colRowsetType = "VarBinaryColumn";
                                        break;
                                    case "datetimeoffset":
                                        colRowsetType = "DateTimeOffsetColumn";
                                        break;
                                    default:
                                        colRowsetType = "VarCharColumn";
                                        break;
                                }

                                //MessageBox.Show($"{colName} : {colRowsetType} : {colTypeName}");
                                DataRow row = tblQueryColumns.NewRow();
                                row["Name"] = colName;
                                row["Type"] = colRowsetType;
                                row["Length"] = colLength;

                                tblQueryColumns.Rows.Add(row);

                                tabMain.SelectTab("tabGrid");
                                dgvNewRowset.DataSource = tblQueryColumns;
                            }
                            // Print the values of each column to the console                               
                            
                        }
                    }
                } catch (Exception ex )
                {
                    MessageBox.Show ("Query Failed \n\n" + ex.Message);
                }
                

            } else
            {
                MessageBox.Show("No active connections");
            }
        }

        private void btnconnect_Click(object sender, EventArgs e)
        {
            if (connectToSQL(txtServerName.Text))
            {
                tabMain.SelectTab("tabSQL");
                txtQuery.Enabled = true;
            }
        }
    }
}
