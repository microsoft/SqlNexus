using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NexusInterfaces;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class CustomXELImporterIntegrationTests
    {
        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [TestCategory("CustomXelImportIntegration")]
        [Timeout(120000)]
        public void SqlReader_CategoryLifecycle_PreservesExactDataAndRollsBackFailures(int category)
        {
            using (var fixture = new SqlFixture((CustomXelSource)category))
            {
                string table = CustomXELImporter.GetTableName((CustomXelSource)category);
                string legacy = GetLegacyTable((CustomXelSource)category);
                fixture.Execute("CREATE TABLE [dbo].[" + legacy + "] (sentinel int NOT NULL); INSERT INTO [dbo].[" + legacy + "] VALUES (42);");
                var logger = new CustomXELImporterTests.CapturingLogger();
                var importer = new CustomXELImporter();
                importer.Options[CustomXELImporter.DropTablesOption] = false;
                fixture.Initialize(importer, logger);

                Assert.IsFalse(importer.DoImport(), "Append must not silently abandon legacy-only data.");
                Assert.AreEqual(0L, fixture.Scalar("SELECT COUNT_BIG(*) FROM sys.tables WHERE name = N'" + table + "';"));
                StringAssert.Contains(string.Join(" ", logger.Messages), "Legacy Custom XEL data exists");

                importer.Options[CustomXELImporter.DropTablesOption] = true;
                for (int run = 0; run < 2; run++)
                {
                    fixture.Initialize(importer, logger);
                    Assert.IsTrue(importer.DoImport(), string.Join(" ", logger.Messages));
                    Assert.AreEqual(2, importer.TotalFilesFound);
                    Assert.AreEqual(fixture.SourceRows, importer.TotalRowsInserted);
                    fixture.AssertExactData(table, 1);
                }
                fixture.AssertSchema(table);
                Assert.AreEqual(1L, fixture.Scalar("SELECT COUNT_BIG(*) FROM [dbo].[" + legacy + "] WHERE sentinel = 42;"));

                importer.Options[CustomXELImporter.DropTablesOption] = false;
                fixture.Initialize(importer, logger);
                Assert.IsTrue(importer.DoImport(), string.Join(" ", logger.Messages));
                Assert.AreEqual(fixture.SourceRows, importer.TotalRowsInserted);
                fixture.AssertExactData(table, 2);

                fixture.Execute(@"CREATE TRIGGER [CustomXelTestRejectDrop] ON DATABASE FOR DROP_TABLE
AS BEGIN; THROW 50042, 'Intentional test rejection.', 1; END;");
                importer.Options[CustomXELImporter.DropTablesOption] = true;
                fixture.Initialize(importer, logger);
                Assert.IsFalse(importer.DoImport());
                Assert.AreEqual(0L, importer.TotalRowsInserted);
                fixture.Execute("DROP TRIGGER [CustomXelTestRejectDrop] ON DATABASE;");
                fixture.AssertExactData(table, 2);

                fixture.Execute("ALTER TABLE [dbo].[" + table + "] ADD [TestIncompatible] int NULL;");
                importer.Options[CustomXELImporter.DropTablesOption] = false;
                fixture.Initialize(importer, logger);
                Assert.IsFalse(importer.DoImport());
                StringAssert.Contains(string.Join(" ", logger.Messages), "schema is incompatible");
                Assert.AreEqual(fixture.SourceRows * 2, fixture.Scalar("SELECT COUNT_BIG(*) FROM [dbo].[" + table + "];"));

                importer.Options[CustomXELImporter.DropTablesOption] = true;
                fixture.Initialize(importer, logger);
                Assert.IsTrue(importer.DoImport(), string.Join(" ", logger.Messages));
                fixture.AssertExactData(table, 1);
                fixture.AssertSchema(table);
            }
        }

        [TestMethod]
        [TestCategory("CustomXelImportIntegration")]
        [Timeout(60000)]
        public void SqlReader_CancelBlockedReplacement_IsResponsiveAndPreservesExistingTable()
        {
            using (var fixture = new SqlFixture(CustomXelSource.SqlDiag))
            {
                var logger = new CustomXELImporterTests.CapturingLogger();
                var importer = new CustomXELImporter();
                fixture.Initialize(importer, logger);
                Assert.IsTrue(importer.DoImport(), string.Join(" ", logger.Messages));
                string table = CustomXELImporter.GetTableName(CustomXelSource.SqlDiag);

                using (var blocker = new SqlConnection(fixture.ConnectionString))
                using (var timer = new Timer { Interval = 200 })
                {
                    blocker.Open();
                    using (SqlTransaction transaction = blocker.BeginTransaction())
                    {
                        using (var command = new SqlCommand("SELECT TOP (1) 1 FROM [dbo].[" + table + "] WITH (TABLOCKX, HOLDLOCK);", blocker, transaction))
                            command.ExecuteScalar();
                        int importingStates = 0;
                        importer.StatusChanged += (sender, e) =>
                        {
                            if (importer.State == ImportState.Importing && ++importingStates == 2)
                                timer.Start();
                        };
                        timer.Tick += (sender, e) => { timer.Stop(); importer.Cancel(); };
                        fixture.Initialize(importer, logger);
                        Stopwatch elapsed = Stopwatch.StartNew();
                        try
                        {
                            Assert.IsFalse(importer.DoImport());
                            Assert.IsTrue(importer.Cancelled);
                            Assert.AreEqual(0L, importer.TotalRowsInserted);
                            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(5), "Cancellation must not wait for the blocking transaction.");
                        }
                        finally
                        {
                            timer.Stop();
                            transaction.Rollback();
                        }
                    }
                }
                fixture.AssertExactData(table, 1);
            }
        }

        private static string GetLegacyTable(CustomXelSource category)
        {
            switch (category)
            {
                case CustomXelSource.SqlDiag: return "tbl_SQL_Base_SQLDIAGXEL_Startup";
                case CustomXelSource.AlwaysOnHealth: return "tbl_SQL_Base_AlwaysOnHealth";
                case CustomXelSource.SystemHealth: return "tbl_SQL_Base_SystemHealthXEL_Startup";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private sealed class SqlFixture : IDisposable
        {
            private readonly string databaseName = "SqlNexus_CustomXelTests_" + Guid.NewGuid().ToString("N");
            private readonly string masterConnectionString;
            private readonly string directory;
            private bool databaseCreated;
            public string ConnectionString { get; }
            public string Pattern { get; }
            public long SourceRows { get; }

            public SqlFixture(CustomXelSource category)
            {
                if (Environment.GetEnvironmentVariable("SQLNEXUS_RUN_CUSTOM_XEL_IMPORT_INTEGRATION") != "1")
                    Assert.Inconclusive("Explicit opt-in required: creates and drops an isolated SQL test database.");
                string sourceFolder = Environment.GetEnvironmentVariable("SQLNEXUS_CUSTOM_XEL_PATH");
                if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                    Assert.Inconclusive("A local capture fixture folder is required.");

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = Environment.GetEnvironmentVariable("SQLNEXUS_CUSTOM_XEL_SERVER") ?? "localhost",
                    InitialCatalog = "master",
                    IntegratedSecurity = true,
                    Encrypt = true,
                    TrustServerCertificate = Environment.GetEnvironmentVariable("SQLNEXUS_CUSTOM_XEL_TRUST_SERVER_CERTIFICATE") == "1",
                    Pooling = false
                };
                masterConnectionString = builder.ConnectionString;
                builder.InitialCatalog = databaseName;
                ConnectionString = builder.ConnectionString;
                directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceFolder.TrimEnd('\\'))),
                    "SqlNexus_XelTests_" + Guid.NewGuid().ToString("N"), "O'Brien;-- capture (2)");
                string mask = CustomXELImporter.CustomXelFileMasks[(int)category];
                Pattern = Path.Combine(directory, mask);

                try
                {
                    using (var connection = new SqlConnection(masterConnectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand("CREATE DATABASE [" + databaseName + "];", connection))
                            command.ExecuteNonQuery();
                        databaseCreated = true;
                    }
                    FileInfo selected = null;
                    foreach (FileInfo candidate in new DirectoryInfo(sourceFolder).GetFiles(mask).OrderBy(file => file.Length))
                    {
                        if (SourceCount(candidate.FullName) > 0)
                        {
                            selected = candidate;
                            break;
                        }
                    }
                    Assert.IsNotNull(selected, "A nonempty fixture is required for this category.");
                    Directory.CreateDirectory(directory);
                    string suffix = Path.GetFileNameWithoutExtension(mask.Replace("*", ""));
                    File.Copy(selected.FullName, Path.Combine(directory, "sample1" + suffix + ".xel"));
                    File.Copy(selected.FullName, Path.Combine(directory, "sample2" + suffix + ".xel"));
                    SourceRows = SourceCount(Pattern);
                    Assert.IsTrue(SourceRows > 0);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Initialize(CustomXELImporter importer, ILogger logger)
            {
                importer.Initialize(Pattern, ConnectionString, "", true, "", "", databaseName, logger);
            }

            public void Execute(string sql)
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                        command.ExecuteNonQuery();
                }
            }

            public long Scalar(string sql, string pattern = null)
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                    {
                        if (pattern != null)
                            command.Parameters.Add("@pattern", SqlDbType.NVarChar, 4000).Value = pattern;
                        return (long)command.ExecuteScalar();
                    }
                }
            }

            private long SourceCount(string pattern)
            {
                return Scalar("SELECT COUNT_BIG(*) FROM sys.fn_xe_file_target_read_file(@pattern, NULL, NULL, NULL);", pattern);
            }

            public void AssertExactData(string table, int copies)
            {
                // The legacy reader materialized SELECT * directly; use that same storage contract.
                const string baseline = "SELECT * INTO #CustomXelExpected FROM sys.fn_xe_file_target_read_file(@pattern, NULL, NULL, NULL); ";
                const string rawColumns = "module_guid, package_guid, object_name, event_data, file_name, file_offset, timestamp_utc";
                const string columns = "module_guid, package_guid, CONVERT(varbinary(max), object_name), DATALENGTH(object_name), "
                    + "CONVERT(varbinary(max), event_data), DATALENGTH(event_data), CONVERT(varbinary(max), file_name), DATALENGTH(file_name), file_offset, timestamp_utc";
                string actual = "SELECT " + columns + ", COUNT_BIG(*) FROM [dbo].[" + table + "] GROUP BY " + columns;
                string expected = "SELECT " + columns + ", COUNT_BIG(*) * " + copies
                    + " FROM #CustomXelExpected GROUP BY " + columns;
                string sql = "SELECT CAST(CASE WHEN EXISTS (" + actual + " EXCEPT " + expected + ") OR EXISTS ("
                    + expected + " EXCEPT " + actual + ") THEN 1 ELSE 0 END AS bigint);";
                long difference = Scalar(baseline + sql, Pattern);
                if (difference != 0)
                {
                    string changed = "";
                    foreach (string column in rawColumns.Split(',').Select(value => value.Trim()))
                    {
                        string expression = column == "object_name" || column == "event_data" || column == "file_name"
                            ? "CONVERT(varbinary(max), " + column + "), DATALENGTH(" + column + ")"
                            : column;
                        string left = "SELECT " + expression + " FROM [dbo].[" + table + "]";
                        string right = "SELECT " + expression + " FROM #CustomXelExpected";
                        long mismatched = Scalar(baseline + "SELECT CAST(CASE WHEN EXISTS (" + left + " EXCEPT " + right
                            + ") OR EXISTS (" + right + " EXCEPT " + left + ") THEN 1 ELSE 0 END AS bigint);", Pattern);
                        if (mismatched != 0)
                            changed += column + " ";
                    }
                    Assert.Fail("Imported values or multiplicities differ; differing column sets: " + changed);
                }
                Assert.AreEqual(SourceRows * copies, Scalar("SELECT COUNT_BIG(*) FROM [dbo].[" + table + "];"));
            }

            public void AssertSchema(string table)
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    DataTable expected;
                    using (var command = new SqlCommand("SELECT * FROM sys.fn_xe_file_target_read_file(@pattern, NULL, NULL, NULL);", connection))
                    {
                        command.Parameters.Add("@pattern", SqlDbType.NVarChar, 4000).Value = Pattern;
                        using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SchemaOnly))
                            expected = reader.GetSchemaTable();
                    }
                    using (var command = new SqlCommand("SELECT * FROM [dbo].[" + table + "];", connection))
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SchemaOnly))
                    {
                        DataTable actual = reader.GetSchemaTable();
                        Assert.AreEqual(expected.Rows.Count, actual.Rows.Count);
                        for (int index = 0; index < expected.Rows.Count; index++)
                            foreach (string property in new[] { "ColumnName", "DataType", "ColumnSize", "NumericPrecision", "NumericScale", "AllowDBNull" })
                                Assert.AreEqual(expected.Rows[index][property], actual.Rows[index][property], "SQL schema property: " + property);
                    }
                }
            }

            public void Dispose()
            {
                if (databaseCreated)
                {
                    using (var connection = new SqlConnection(masterConnectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand("DROP DATABASE [" + databaseName + "];", connection))
                            command.ExecuteNonQuery();
                    }
                    databaseCreated = false;
                }
                if (directory != null && Directory.Exists(directory))
                    Directory.Delete(Path.GetDirectoryName(directory), true);
            }
        }
    }
}
