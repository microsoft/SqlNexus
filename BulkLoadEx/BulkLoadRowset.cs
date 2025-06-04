using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;


namespace BulkLoadEx
{
    public class BulkLoadRowset
    {
        // Number of rows in each client-side cache of rows (per rowset)
        private const int DEFAULT_MAX_CLIENT_BUF_SIZE = 50000;
        // Used for SqlBulkCopy.BatchSize
        private const int BULK_COPY_BATCH_SIZE = 50000;

        private SqlConnection cn;
        private SqlBulkCopy bulkCopy;
        private DataTable dataTableBuffer;
        private int maxClientBufSize = DEFAULT_MAX_CLIENT_BUF_SIZE;
        private int bulkCopyBatchSize = BULK_COPY_BATCH_SIZE;
        private string targetTable;

        /// <summary>
        /// Name of the table that this object will load data into
        /// </summary>
        public string TargetTable
        {
            get
            {
                return targetTable;
            }
        }

        /// <summary>
        /// BulkLoadRowset ctor
        /// </summary>
        /// <param name="TargetTable">Name of the table to insert into</param>
        /// <param name="ConnectionString">Connection string to use for the bulk insert</param>
        public BulkLoadRowset(string TargetTable, string ConnectionString)
        {
            Init(TargetTable, ConnectionString, DEFAULT_MAX_CLIENT_BUF_SIZE);
        }
        /// <summary>
        /// BulkLoadRowset ctor
        /// </summary>
        /// <param name="TargetTable">Name of the table to insert into</param>
        /// <param name="ConnectionString">Connection string to use for the bulk insert</param>
        /// <param name="MaxClientBufSize">Maximum size of the client-side row cache for this bulk load</param>
        public BulkLoadRowset(string TargetTable, string ConnectionString, int MaxClientBufSize)
        {
            Init(TargetTable, ConnectionString, MaxClientBufSize);
        }
        private void Init(string TargetTable, string ConnectionString, int MaxClientBufSize)
        {
            maxClientBufSize = MaxClientBufSize;
            targetTable = TargetTable;
            cn = new SqlConnection(ConnectionString);
            cn.Open();

            // Create SqlBulkLoad object
            bulkCopy = new SqlBulkCopy(cn, SqlBulkCopyOptions.TableLock, null);
            bulkCopy.BatchSize = bulkCopyBatchSize;
            bulkCopy.BulkCopyTimeout = 0;
            bulkCopy.DestinationTableName = TargetTable;
        }

        /// <summary>
        /// Allocate a new DataRow with the schema of TargetTable
        /// </summary>
        /// <remarks>
        /// Caller should populate the DataRow's columns, then pass the DataRow to InsertRow()
        /// </remarks>
        /// <returns>A new DataRow</returns>
        public DataRow GetNewRow()
        {
            if (null == dataTableBuffer)
            {
                // Get a DataTable w/no rows that captures the schema of the destination table
                SqlDataAdapter schemaAdapter = new SqlDataAdapter("select * from [" + targetTable + "] where 1=0", cn);
                dataTableBuffer = new DataTable();
                dataTableBuffer.TableName = targetTable;
                schemaAdapter.Fill(this.dataTableBuffer);
            }
            return this.dataTableBuffer.NewRow();
        }

        /// <summary>
        /// Inserts a new row into TargetTable
        /// </summary>
        /// <param name="newRow">DataRow allocated via GetNewRow()</param>
        public void InsertRow(DataRow newRow)
        {
            dataTableBuffer.Rows.Add(newRow);
            if (dataTableBuffer.Rows.Count >= maxClientBufSize)
            {
                Flush();
            }
        }

        /// <summary>
        /// Insert any cached rows into the target table
        /// </summary>
        public void Flush()
        {
            if ((null != dataTableBuffer) && (dataTableBuffer.Rows.Count > 0))
            {
                bulkCopy.WriteToServer(dataTableBuffer);
                dataTableBuffer.Rows.Clear();
            }
        }

        /// <summary>
        /// Flush any rows still on the client and close the SQL connection
        /// </summary>
        public void Close()
        {
            Flush();
            if (ConnectionState.Closed != cn.State)
                cn.Close();
        }
    }
}
