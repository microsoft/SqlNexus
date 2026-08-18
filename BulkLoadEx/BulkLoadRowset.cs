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
                // Handle schema-qualified names (e.g. "ReadTrace.tblTraceFiles") by bracketing each part separately
                string quotedName;
                int dotIndex = targetTable.IndexOf('.');
                if (dotIndex >= 0)
                {
                    string schema = targetTable.Substring(0, dotIndex);
                    string table = targetTable.Substring(dotIndex + 1);
                    quotedName = "[" + schema + "].[" + table + "]";
                }
                else
                {
                    quotedName = "[" + targetTable + "]";
                }
                using (SqlDataAdapter schemaAdapter = new SqlDataAdapter("select * from " + quotedName + " where 1=0", cn))
                {
                    dataTableBuffer = new DataTable();
                    dataTableBuffer.TableName = targetTable;
                    schemaAdapter.Fill(this.dataTableBuffer);
                }
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
        /// Append a diagnostic note to a text column on the most recently buffered row.
        /// Used to indicate that continuation lines were skipped during import while still
        /// preserving a mostly clean rowset shape.
        /// </summary>
        /// <param name="columnName">Target string column name.</param>
        /// <param name="note">Diagnostic note to append.</param>
        /// <returns>True when the last buffered row was updated; otherwise false.</returns>
        public bool TryAnnotateLastBufferedRow(string columnName, string note)
        {
            if (string.IsNullOrEmpty(columnName) || string.IsNullOrEmpty(note))
                return false;

            if (dataTableBuffer == null || dataTableBuffer.Rows.Count == 0)
                return false;

            if (!dataTableBuffer.Columns.Contains(columnName))
                return false;

            DataColumn column = dataTableBuffer.Columns[columnName];
            DataRow lastRow = dataTableBuffer.Rows[dataTableBuffer.Rows.Count - 1];

            string existing = (lastRow[columnName] == DBNull.Value) ? string.Empty : Convert.ToString(lastRow[columnName]);
            bool noteAppended;
            string updated = AppendContinuationNote(existing, column.MaxLength, note, out noteAppended);

            if (!noteAppended)
                return false;

            lastRow[columnName] = string.IsNullOrEmpty(updated) ? (object)DBNull.Value : updated;
            return true;
        }

        /// <summary>
        /// Appends a note to existing text while respecting destination max length and avoiding duplicates.
        /// </summary>
        public static string AppendContinuationNote(string existingText, int maxLength, string note, out bool noteAppended)
        {
            noteAppended = false;
            string existing = existingText ?? string.Empty;
            if (string.IsNullOrEmpty(note))
                return existing;

            if (existing.IndexOf(note, StringComparison.OrdinalIgnoreCase) >= 0)
                return existing;

            string separator = string.IsNullOrEmpty(existing) ? string.Empty : " ";
            string candidate = existing + separator + note;

            if (maxLength > 0 && candidate.Length > maxLength)
            {
                const string fallback = " [See raw file for full multi-line error text]";

                if (existing.IndexOf(fallback, StringComparison.OrdinalIgnoreCase) >= 0)
                    return existing;

                candidate = existing + fallback;
                if (maxLength > 0 && candidate.Length > maxLength)
                {
                    int remaining = maxLength - fallback.Length;
                    if (remaining <= 0)
                        return maxLength > 0 && existing.Length > maxLength ? existing.Substring(0, maxLength) : existing;

                    string prefix = existing.Length > remaining ? existing.Substring(0, remaining) : existing;
                    candidate = prefix + fallback;
                }
            }

            noteAppended = true;
            return candidate;
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
