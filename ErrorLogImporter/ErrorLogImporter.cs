using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using NexusInterfaces;
using BulkLoadEx;

namespace ErrorLogImporter
{
    public class ErrorLogImporter : INexusFileImporter
    {
        private const string TABLE_NAME = "tbl_ERRORLOG";
        private const string OPTION_DROP_EXISTING = "Drop existing tables (ERRORLOG)";
        private const string OPTION_ENABLED = "Enabled";
        private const string HEAD_AND_TAIL_MARKER_PARTIAL = "<<... middle part of file not captured because";
        // Public so the (non-strong-named) unit test project can assert against these values; a
        // signed assembly cannot grant InternalsVisibleTo to an unsigned friend, so we widen instead.
        public const string INCOMPLETE_PROCESS_MARKER = "INCOMPLETE";
        public const string INCOMPLETE_LOG_MESSAGE = ">>>>> ERRORLOG file is incomplete: the middle part was not captured because the file was too large (>1 GB). <<<<<";

        // Regex to match ERRORLOG lines: datetime, process, message
        // Example: "2026-04-14 22:37:11.55 Server      Microsoft SQL Server 2022..."
        private static readonly Regex LogLineRegex = new Regex(
            @"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{2})\s+(\S+)\s+(.*)",
            RegexOptions.Compiled);

        // Regex to extract Error number and State from messages like "Error: 18456, Severity: 14, State: 38."
        private static readonly Regex ErrorStateRegex = new Regex(
            @"Error:\s*(\d+),\s*Severity:\s*\d+,\s*State:\s*(\d+)",
            RegexOptions.Compiled);

        private string fileMask = "";
        private string connStr = "";
        private ILogger logger;

        private ImportState state = ImportState.Idle;
        private bool cancelled = false;
        private long totalRowsInserted = 0;
        private long totalLinesProcessed = 0;
        private long fileSize = 0;
        private long currentPosition = 0;
        private bool hasDroppedTable = false;
        private readonly ArrayList knownRowsets = new ArrayList();
        private readonly Dictionary<string, object> options = new Dictionary<string, object>();


        public ErrorLogImporter()
        {
            options.Add(OPTION_DROP_EXISTING, true);
            options.Add(OPTION_ENABLED, true);
        }

        public static bool IsHeadAndTailMarker(string line)
        {
            return line != null && line.Trim().StartsWith(HEAD_AND_TAIL_MARKER_PARTIAL, StringComparison.Ordinal);
        }

        // A UTF-8 BOM (or its mangled single-character representation such as U+FEFF or U+FFFD, or a
        // literal '?' produced when the BOM is written to a non-UTF8 file) can be prepended to the
        // first line following the head-and-tail marker by the collection tooling. Strip it so the
        // date/time regex still matches and the line is not lost.
        public static string StripLeadingByteOrderMark(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            char first = line[0];
            if (first == '\uFEFF' || first == '\uFFFD' || first == '?')
            {
                // Only strip when what follows looks like the start of a real log line (a digit),
                // so genuine '?'-prefixed message text is never altered.
                if (line.Length > 1 && char.IsDigit(line[1]))
                    return line.Substring(1);
            }

            return line;
        }

        private void LogMessage(string msg)
        {
            if (null == logger)
                Trace.WriteLine(msg);
            else
                logger.LogMessage(msg);
        }

        #region INexusImporter Members

        public Guid ID
        {
            get { return new Guid("D7E8F9A0-B1C2-4D3E-5F6A-7B8C9D0E1F2A"); }
        }

        public void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            this.fileMask = Filemask;
            this.connStr = connString;
            this.logger = Logger;
            this.state = ImportState.Idle;
            this.cancelled = false;
            this.totalRowsInserted = 0;
            this.totalLinesProcessed = 0;
        }

        public Dictionary<string, object> Options
        {
            get { return options; }
        }

        public Form OptionsDialog
        {
            get { return null; }
        }

        public string[] SupportedMasks
        {
            get { return new string[] { "*_ERRORLOG*" }; }
        }

        public string[] PreScripts
        {
            get { return new string[] { }; }
        }

        public string[] PostScripts
        {
            get { return new string[] { }; }
        }

        public ImportState State
        {
            get { return state; }
            private set
            {
                state = value;
                OnStatusChanged(EventArgs.Empty);
            }
        }

        public bool Cancelled
        {
            get { return cancelled; }
            private set { cancelled = value; }
        }

        public ArrayList KnownRowsets
        {
            get { return knownRowsets; }
        }

        public long TotalRowsInserted
        {
            get { return totalRowsInserted; }
        }

        public long TotalLinesProcessed
        {
            get { return totalLinesProcessed; }
        }

        public string Name
        {
            get { return "ERRORLOG Importer"; }
        }

        public bool DoImport()
        {
            try
            {
                string dir = Path.GetDirectoryName(fileMask);
                string mask = Path.GetFileName(fileMask);

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    LogMessage("ErrorLogImporter: Directory not found: " + dir);
                    return false;
                }

                string[] files = Directory.GetFiles(dir, mask);
                if (files.Length == 0)
                {
                    State = ImportState.NoFiles;
                    LogMessage("ErrorLogImporter: No ERRORLOG files found matching " + fileMask);
                    return true;
                }

                State = ImportState.OpeningDatabaseConnection;

                if ((bool)options[OPTION_DROP_EXISTING] && !hasDroppedTable)
                {
                    DropExistingTable();
                    hasDroppedTable = true;
                }

                CreateTable();

                State = ImportState.Importing;

                foreach (string file in files)
                {
                    if (Cancelled)
                        break;

                    LogMessage("ErrorLogImporter: Importing file " + file);
                    ImportFile(file);
                }

                State = ImportState.Idle;
                LogMessage("ErrorLogImporter: Import complete. Total rows inserted: " + totalRowsInserted);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage("ErrorLogImporter: Error - " + ex);
                State = ImportState.Idle;
                return false;
            }
        }

        public void Cancel()
        {
            Cancelled = true;
            State = ImportState.Canceling;
            LogMessage("ErrorLogImporter: Received cancel request");
        }

        public event EventHandler StatusChanged;

        public void OnStatusChanged(EventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        #endregion

        #region INexusProgressReporter Members

        public long CurrentPosition
        {
            get { return currentPosition; }
        }

        public event EventHandler ProgressChanged;

        public void OnProgressChanged(EventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        #endregion

        #region INexusFileSizeReporter Members

        public long FileSize
        {
            get { return fileSize; }
        }

        #endregion

        #region Private Methods

        private void DropExistingTable()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                cn.Open();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = cn;
                    cmd.CommandTimeout = 0;
                    cmd.CommandText = "IF OBJECT_ID ('" + TABLE_NAME + "', 'U') IS NOT NULL DROP TABLE [" + TABLE_NAME + "]";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void CreateTable()
        {
            using (SqlConnection cn = new SqlConnection(connStr))
            {
                cn.Open();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = cn;
                    cmd.CommandTimeout = 0;
                    cmd.CommandText = @"
                        IF OBJECT_ID ('" + TABLE_NAME + @"', 'U') IS NULL
                        BEGIN
                            CREATE TABLE [" + TABLE_NAME + @"] (
                                [RowNum] bigint IDENTITY(1,1) NOT NULL,
                                [LogDateTime] datetime NULL,
                                [Process] varchar(50) NULL,
                                [Message] varchar(max) NULL,
                                [ErrorNumber] int NULL,
                                [State] int NULL,
                                [FileName] varchar(256) NULL
                            )
                            CREATE NONCLUSTERED INDEX [IX_" + TABLE_NAME + @"_LogDateTime_RowNum] ON [" + TABLE_NAME + @"] ([LogDateTime], [RowNum])
                        END";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ImportFile(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            fileSize = fi.Length;
            currentPosition = 0;
            string shortFileName = Path.GetFileName(filePath);

            BulkLoadRowset bulkLoad = new BulkLoadRowset(TABLE_NAME, connStr);

            try
            {
                using (StreamReader reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
                {
                    // Peek forces the StreamReader to read the BOM (if any) and settle on the actual
                    // encoding before we start estimating byte offsets for the progress bar.
                    reader.Peek();
                    Encoding encoding = reader.CurrentEncoding;

                    ProcessLogEntries(
                        reader,
                        () => Cancelled,
                        line =>
                        {
                            totalLinesProcessed++;
                            currentPosition = AdvancePosition(currentPosition, fileSize, line, encoding);
                            OnProgressChanged(EventArgs.Empty);
                        },
                        (logDateTime, process, message) => InsertRow(bulkLoad, logDateTime, process, message, shortFileName),
                        () => InsertIncompleteLogNotice(bulkLoad, shortFileName));
                }
            }
            finally
            {
                bulkLoad.Close();
            }
        }

        // Estimates how far into the file we have read after consuming a line, in bytes, using the
        // file's actual encoding and accounting for a stripped newline. The result is clamped to
        // fileSize so the reported progress can never overshoot the file (which would push a progress
        // bar past 100%). ReadLine drops the line terminator, so we add the encoding's byte count for
        // a newline; the estimate is approximate for LF-only vs CRLF files but never exceeds fileSize.
        public static long AdvancePosition(long currentPosition, long fileSize, string line, Encoding encoding)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;

            long lineBytes = line != null ? encoding.GetByteCount(line) : 0;
            long newlineBytes = encoding.GetByteCount(Environment.NewLine);
            long advanced = currentPosition + lineBytes + newlineBytes;

            if (fileSize > 0 && advanced > fileSize)
                return fileSize;

            return advanced;
        }

        public static void ProcessLogEntries(TextReader reader, Func<bool> isCancelled, Action<string> lineProcessed, Action<DateTime, string, string> insertRow, Action insertIncompleteLogNotice)
        {
            DateTime? pendingDateTime = null;
            string pendingProcess = null;
            StringBuilder pendingMessageBuilder = null;
            bool headAndTailMarkerFound = false;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (isCancelled())
                    break;

                lineProcessed(line);

                // The head-and-tail marker is written at most once per ERRORLOG file by the
                // collection tooling, so once we have seen it we stop checking for it. This avoids a
                // Trim()/StartsWith() call on every remaining line of what may be a very large tail
                // section (>1 GB), while still emitting exactly one incomplete-log notice.
                if (!headAndTailMarkerFound)
                {
                    if (IsHeadAndTailMarker(line))
                    {
                        if (pendingDateTime.HasValue)
                        {
                            insertRow(pendingDateTime.Value, pendingProcess, pendingMessageBuilder?.ToString());
                        }

                        insertIncompleteLogNotice();
                        headAndTailMarkerFound = true;
                        pendingDateTime = null;
                        pendingProcess = null;
                        pendingMessageBuilder = null;

                        // Note: if the first line of the truncated tail section is a continuation
                        // line (not a new dated entry), it is intentionally dropped because there is
                        // no pending entry to append it to (pendingMessageBuilder is null here). The
                        // source data is genuinely truncated at the marker, so no owning entry exists.
                        continue;
                    }
                }

                Match match = LogLineRegex.Match(StripLeadingByteOrderMark(line));
                if (match.Success)
                {
                    if (pendingDateTime.HasValue)
                    {
                        insertRow(pendingDateTime.Value, pendingProcess, pendingMessageBuilder?.ToString());
                    }

                    string dateStr = match.Groups[1].Value;
                    pendingProcess = match.Groups[2].Value;
                    pendingMessageBuilder = new StringBuilder(match.Groups[3].Value);
                    pendingDateTime = DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss.ff",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate)
                        ? parsedDate
                        : (DateTime?)null;
                }
                else if (pendingMessageBuilder != null)
                {
                    pendingMessageBuilder.Append(Environment.NewLine);
                    pendingMessageBuilder.Append(line);
                }
            }

            if (pendingDateTime.HasValue)
            {
                insertRow(pendingDateTime.Value, pendingProcess, pendingMessageBuilder?.ToString());
            }
        }

        private void InsertRow(BulkLoadRowset bulkLoad, DateTime logDateTime, string process, string message, string fileName)
        {
            System.Data.DataRow row = bulkLoad.GetNewRow();
            row["LogDateTime"] = logDateTime;
            row["Process"] = process != null && process.Length > 50 ? process.Substring(0, 50) : process;
            row["Message"] = message ?? "";

            // Extract Error number and State from message if present
            if (message != null)
            {
                Match errorMatch = ErrorStateRegex.Match(message);
                if (errorMatch.Success)
                {
                    row["ErrorNumber"] = int.Parse(errorMatch.Groups[1].Value);
                    row["State"] = int.Parse(errorMatch.Groups[2].Value);
                }
            }

            row["FileName"] = fileName != null && fileName.Length > 256 ? fileName.Substring(0, 256) : fileName;
            bulkLoad.InsertRow(row);
            totalRowsInserted++;
        }

        private void InsertIncompleteLogNotice(BulkLoadRowset bulkLoad, string fileName)
        {
            System.Data.DataRow row = bulkLoad.GetNewRow();
            row["LogDateTime"] = DBNull.Value;
            row["Process"] = INCOMPLETE_PROCESS_MARKER;
            row["Message"] = INCOMPLETE_LOG_MESSAGE;
            row["FileName"] = fileName != null && fileName.Length > 256 ? fileName.Substring(0, 256) : fileName;
            bulkLoad.InsertRow(row);
            totalRowsInserted++;
        }

        #endregion
    }
}
