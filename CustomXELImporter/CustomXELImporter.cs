using NexusInterfaces;
using Microsoft.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomXELImporter
{
    internal enum CustomXelSource
    {
        SqlDiag,
        AlwaysOnHealth,
        SystemHealth
    }

    public sealed class CustomXELImporter : INexusImporter, INexusImporterPolicy
    {
        internal const string ImporterName = "Custom XEL Importer";
        internal const string DropTablesOption = "Drop existing tables";
        internal const string SqlDiagMask = "*_SQLDIAG*.xel";
        internal const string AlwaysOnHealthMask = "*AlwaysOn_health*.xel";
        internal const string SystemHealthMask = "*system_health*.xel";
        internal static readonly string[] CustomXelFileMasks =
            { SqlDiagMask, AlwaysOnHealthMask, SystemHealthMask };

        private readonly Func<CustomXelSource, string, bool, long> executeImport;
        private string connectionString;
        private string filePattern;
        private CustomXelSource source;
        private ILogger logger;
        private CancellationTokenSource cancellation;
        private bool initialized;

        public CustomXELImporter()
        {
            executeImport = ExecuteImport;
        }

        internal CustomXELImporter(Func<CustomXelSource, string, bool, long> executeImport)
        {
            this.executeImport = executeImport ?? throw new ArgumentNullException(nameof(executeImport));
        }

        public Guid ID { get; } = new Guid("05F43A63-2922-457B-BDB2-368280A8DB9E");
        public string Name => ImporterName;
        public bool PrimaryFolderOnly => true;
        public bool StopOnFailure => true;
        public Dictionary<string, object> Options { get; } = new Dictionary<string, object>
        {
            { "Enabled", false },
            { DropTablesOption, true }
        };
        public Form OptionsDialog => null;
        public string[] SupportedMasks => (string[])CustomXelFileMasks.Clone();
        public string[] PreScripts => new string[0];
        public string[] PostScripts => new string[0];
        public ImportState State { get; private set; }
        public bool Cancelled { get; private set; }
        public ArrayList KnownRowsets { get; } = new ArrayList();
        public long TotalRowsInserted { get; private set; }
        public long TotalLinesProcessed => 0;
        public int TotalFilesFound { get; private set; }
        public event EventHandler StatusChanged;

        public void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth,
            string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            initialized = false;
            logger = Logger ?? throw new ArgumentNullException(nameof(Logger));
            if (string.IsNullOrWhiteSpace(connString))
                throw new ArgumentException("A configured database connection is required.", nameof(connString));
            if (string.IsNullOrWhiteSpace(Filemask) || Filemask.Length > 4000)
                throw new ArgumentException("An absolute Custom XEL category mask of at most 4000 characters is required.", nameof(Filemask));

            string root = Path.GetPathRoot(Filemask);
            string directory = Path.GetDirectoryName(Filemask);
            if (string.IsNullOrEmpty(root) || root.Length < 3 ||
                !root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                string.IsNullOrEmpty(directory) || directory.IndexOfAny(new[] { '*', '?' }) >= 0)
                throw new ArgumentException("An absolute Custom XEL folder is required.", nameof(Filemask));
            foreach (string component in directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (component == "." || component == "..")
                    throw new ArgumentException("Directory traversal is not permitted.", nameof(Filemask));
            }

            source = GetSource(Path.GetFileName(Filemask));
            filePattern = Path.Combine(Path.GetFullPath(directory), Path.GetFileName(Filemask));
            connectionString = connString;
            Cancelled = false;
            TotalRowsInserted = 0;
            TotalFilesFound = 0;
            KnownRowsets.Clear();
            SetState(ImportState.Idle);
            initialized = true;
        }

        internal static CustomXelSource GetSource(string mask)
        {
            for (int index = 0; index < CustomXelFileMasks.Length; index++)
            {
                if (string.Equals(mask, CustomXelFileMasks[index], StringComparison.OrdinalIgnoreCase))
                    return (CustomXelSource)index;
            }
            throw new ArgumentException("Unknown Custom XEL category mask.", nameof(mask));
        }

        public bool DoImport()
        {
            if (!initialized)
                throw new InvalidOperationException("Initialize the Custom XEL importer before importing.");

            try
            {
                // Selection (including transient /M overrides) belongs to the host, not saved options.
                if (Cancelled)
                    throw new OperationCanceledException();
                string[] files = Directory.GetFiles(Path.GetDirectoryName(filePattern),
                    Path.GetFileName(filePattern), SearchOption.TopDirectoryOnly);
                TotalFilesFound = files.Length;
                if (files.Length == 0)
                {
                    SetState(ImportState.NoFiles);
                    logger.LogMessage("Custom XEL category has no matching files; existing tables were not changed.");
                    return true;
                }

                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    int categories = (name.IndexOf("_SQLDIAG", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                        + (name.IndexOf("AlwaysOn_health", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                        + (name.IndexOf("system_health", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0);
                    if (categories != 1)
                        throw new InvalidDataException("A Custom XEL filename matches multiple categories. Rename or remove the ambiguous file before importing.");
                }

                if (!(Options[DropTablesOption] is bool))
                    throw new InvalidOperationException("The Custom XEL drop-tables option must be boolean.");
                SetState(ImportState.Importing);
                if (Cancelled)
                    throw new OperationCanceledException();
                long rows = executeImport(source, filePattern, (bool)Options[DropTablesOption]);
                if (rows < 0)
                    throw new InvalidOperationException("The Custom XEL reader returned an invalid row count.");
                TotalRowsInserted = rows;
                KnownRowsets.Add(new ImportedRowset { Name = GetTableName(source), RowsInserted = rows });
                logger.LogMessage(string.Format("Custom XEL import for {0} finished: {1} rows imported from {2} files into dbo.{3}.",
                    Path.GetFileName(filePattern), rows, files.Length, GetTableName(source)));
                SetState(ImportState.Idle);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogMessage(Cancelled
                    ? "Custom XEL import cancelled; the current category transaction was rolled back."
                    : "Custom XEL import failed; subsequent processing must stop. " + ex.Message, MessageOptions.Both);
                return false;
            }
        }

        private long ExecuteImport(CustomXelSource category, string pattern, bool drop)
        {
            using (var cancellationSource = new CancellationTokenSource())
            using (var connection = new SqlConnection(connectionString))
            {
                cancellation = cancellationSource;
                try
                {
                    if (Cancelled)
                        cancellationSource.Cancel();
                    SetState(ImportState.OpeningDatabaseConnection);
                    WaitForSql(connection.OpenAsync(cancellationSource.Token));
                    cancellationSource.Token.ThrowIfCancellationRequested();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    using (SqlCommand command = CreateImportCommand(connection, category, pattern, drop))
                    {
                        command.Transaction = transaction;
                        SetState(ImportState.Importing);
                        object result = WaitForSql(command.ExecuteScalarAsync(cancellationSource.Token));
                        cancellationSource.Token.ThrowIfCancellationRequested();
                        if (!(result is long) || (long)result < 0)
                            throw new InvalidOperationException("The Custom XEL reader did not return a valid row count.");
                        transaction.Commit();
                        return (long)result;
                    }
                }
                finally
                {
                    cancellation = null;
                }
            }
        }

        private static void WaitForSql(Task task)
        {
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            task.GetAwaiter().GetResult();
        }

        private static T WaitForSql<T>(Task<T> task)
        {
            WaitForSql((Task)task);
            return task.GetAwaiter().GetResult();
        }

        internal static string GetTableName(CustomXelSource category)
        {
            switch (category)
            {
                case CustomXelSource.SqlDiag: return "tbl_SqlDiagXel";
                case CustomXelSource.AlwaysOnHealth: return "tbl_AlwaysOnHealthXel";
                case CustomXelSource.SystemHealth: return "tbl_SystemHealthXel";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        internal static SqlCommand CreateImportCommand(SqlConnection connection, CustomXelSource category,
            string pattern, bool drop)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 4000)
                throw new ArgumentException("An XEL file pattern of at most 4000 characters is required.", nameof(pattern));

            string table = GetTableName(category);
            string legacyTable;
            switch (category)
            {
                case CustomXelSource.SqlDiag: legacyTable = "tbl_SQL_Base_SQLDIAGXEL_Startup"; break;
                case CustomXelSource.AlwaysOnHealth: legacyTable = "tbl_SQL_Base_AlwaysOnHealth"; break;
                case CustomXelSource.SystemHealth: legacyTable = "tbl_SQL_Base_SystemHealthXEL_Startup"; break;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }

            // Stage before replacing a table; the caller's transaction covers loading and publication.
            string sql = @"SET NOCOUNT ON;
SET XACT_ABORT ON;
SELECT * INTO #CustomXelInput
FROM sys.fn_xe_file_target_read_file(@filePattern, NULL, NULL, NULL);
";
            if (drop)
                sql += "IF OBJECT_ID(N'dbo." + table + "', N'U') IS NOT NULL DROP TABLE [dbo].[" + table + "];\n";
            else
                sql += "IF OBJECT_ID(N'dbo." + table + "', N'U') IS NULL AND OBJECT_ID(N'dbo." + legacyTable + @"', N'U') IS NOT NULL
    THROW 50001, 'Legacy Custom XEL data exists. Re-import with Drop existing tables enabled to populate the canonical table; legacy tables are left unchanged.', 1;
";

            string columns = "[name] COLLATE DATABASE_DEFAULT, column_id, system_type_id, max_length, [precision], scale, is_nullable, is_identity, is_computed, collation_name COLLATE DATABASE_DEFAULT";
            string targetColumns = "SELECT " + columns + " FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo." + table + "')";
            string stagedColumns = "SELECT " + columns + " FROM tempdb.sys.columns WHERE object_id = OBJECT_ID(N'tempdb..#CustomXelInput')";
            sql += "IF OBJECT_ID(N'dbo." + table + @"', N'U') IS NULL
    SELECT * INTO [dbo].[" + table + @"] FROM #CustomXelInput;
ELSE
BEGIN
    IF EXISTS (" + targetColumns + " EXCEPT " + stagedColumns + @")
        OR EXISTS (" + stagedColumns + " EXCEPT " + targetColumns + @")
        THROW 50002, 'The canonical Custom XEL table schema is incompatible. Re-import with Drop existing tables enabled.', 1;
    INSERT INTO [dbo].[" + table + @"] SELECT * FROM #CustomXelInput;
END;
SELECT COUNT_BIG(*) FROM #CustomXelInput;";
            var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
            command.Parameters.Add("@filePattern", SqlDbType.NVarChar, 4000).Value = pattern;
            return command;
        }

        public void Cancel()
        {
            Cancelled = true;
            SetState(ImportState.Canceling);
            cancellation?.Cancel();
        }

        private void SetState(ImportState state)
        {
            State = state;
            OnStatusChanged(EventArgs.Empty);
        }

        public void OnStatusChanged(EventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        private sealed class ImportedRowset : INexusImportedRowset
        {
            public string Name { get; set; }
            public long RowsInserted { get; set; }
        }
    }
}
