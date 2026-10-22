using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusInterfaces;
using sqlnexus;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class TracePostProcessingPrerequisitesTests
    {
        [DataTestMethod]
        [DataRow(null)]
        [DataRow("Custom XEL Importer")]
        [DataRow("Rowset Importer")]
        [DataRow("ERRORLOG Importer")]
        public void RecordResult_NoTraceImporter_SchedulesOnlyGeneralProcessing(string importerName)
        {
            var prerequisites = new TracePostProcessingPrerequisites();
            prerequisites.RecordResult(importerName, true, false);
            Assert.IsFalse(prerequisites.CanRun);
            CollectionAssert.AreEqual(new[] { TracePostProcessingPrerequisites.GeneralScript }, prerequisites.Scripts);
        }

        [DataTestMethod]
        [DataRow("ReadTrace (SQL XEL/TRC Files)", true, false, true)]
        [DataRow("Trace Event Importer (Managed)", true, false, true)]
        [DataRow("ReadTrace (SQL XEL/TRC Files)", false, false, false)]
        [DataRow("Trace Event Importer (Managed)", false, false, false)]
        [DataRow("ReadTrace (SQL XEL/TRC Files)", true, true, false)]
        [DataRow("Trace Event Importer (Managed)", true, true, false)]
        public void RecordResult_TraceOutcome_SchedulesTraceOnlyForSuccessfulUncancelledImports(
            string importerName, bool success, bool cancelled, bool expected)
        {
            var prerequisites = new TracePostProcessingPrerequisites();
            prerequisites.RecordResult(importerName, success, cancelled);
            Assert.AreEqual(expected, prerequisites.CanRun);
            CollectionAssert.AreEqual(expected
                ? new[] { TracePostProcessingPrerequisites.TraceScript, TracePostProcessingPrerequisites.GeneralScript }
                : new[] { TracePostProcessingPrerequisites.GeneralScript }, prerequisites.Scripts);
        }

        [TestMethod]
        public void RecordResult_AnyTraceFailure_CannotBeMaskedByLaterSuccess()
        {
            var prerequisites = new TracePostProcessingPrerequisites();
            prerequisites.RecordResult(ImporterSelectionEvaluator.TraceEventImporterName, true, false);
            prerequisites.RecordResult(ImporterSelectionEvaluator.TraceEventImporterName, false, false);
            prerequisites.RecordResult(ImporterSelectionEvaluator.TraceEventImporterName, true, false);
            Assert.IsFalse(prerequisites.CanRun);
            Assert.IsFalse(new TracePostProcessingPrerequisites().CanRun);
        }

        [TestMethod]
        public void RecordResult_UnrelatedFailure_DoesNotChangeSuccessfulTracePrerequisite()
        {
            var prerequisites = new TracePostProcessingPrerequisites();
            prerequisites.RecordResult(ImporterSelectionEvaluator.ReadTraceImporterName, true, false);
            prerequisites.RecordResult(CustomXELImporter.ImporterName, false, false);
            Assert.IsTrue(prerequisites.CanRun);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [TestCategory("CustomXelImportIntegration")]
        public void GeneralPostProcessing_NoTraceImportAndMissingFlag_RunsWithoutTouchingTraceTables(bool staleTraceTables)
        {
            using (var database = new TestDatabase())
            {
                database.Execute(@"CREATE TABLE dbo.tbl_server_times (time_delta_hours int);
INSERT INTO dbo.tbl_server_times VALUES (7);
CREATE TABLE dbo.tbl_SystemInformation (Property nvarchar(128));
INSERT INTO dbo.tbl_SystemInformation VALUES (N'Test:');");
                if (staleTraceTables)
                    database.CreateTraceTables();

                database.RunScript("SQLNexus_PostProcessing.sql");

                Assert.AreEqual("Test", database.Scalar("SELECT Property FROM dbo.tbl_SystemInformation;"));
                Assert.IsTrue((int)database.Scalar("SELECT COUNT(*) FROM dbo.filter_driver_altitudes;") > 0);
                Assert.AreEqual(0, (int)database.Scalar(
                    "SELECT COUNT(*) FROM sys.columns WHERE name IN ('StartTime_local', 'EndTime_local') AND object_id IN " +
                    "(OBJECT_ID('ReadTrace.tblBatches'), OBJECT_ID('ReadTrace.tblStatements'), OBJECT_ID('ReadTrace.tblConnections'));"));
            }
        }

        [DataTestMethod]
        [DataRow(0, 5, false)]  // Missing optional flag: use rowset UTC offset.
        [DataRow(0, 5, true)]   // Recover a partially extended trace table.
        [DataRow(1, 5, false)]  // Explicit UTC flag.
        [DataRow(2, 12, false)] // Rowset flag indicates local timestamps.
        [DataRow(3, 12, false)] // Server-properties flag, optional rowset column absent.
        [DataRow(4, 12, false)] // Misc-info fallback, optional rowset column absent.
        [DataRow(5, 12, false)] // Trace-only import: no rowset metadata or offset.
        [DataRow(6, 5, false)]  // Server-properties UTC offset.
        [TestCategory("CustomXelImportIntegration")]
        public void TracePostProcessing_MetadataVariants_PreservesLocalTimeOrConvertsUtc(int metadata, int expectedHour, bool partialColumns)
        {
            using (var database = new TestDatabase())
            {
                database.CreateTraceTables();
                if (partialColumns)
                    database.Execute("ALTER TABLE ReadTrace.tblBatches ADD StartTime_local datetime;");
                if (metadata != 5)
                    database.Execute("CREATE TABLE dbo.tbl_server_times (time_delta_hours int); INSERT INTO dbo.tbl_server_times VALUES (7);");
                if (metadata == 1 || metadata == 2)
                    database.Execute("ALTER TABLE dbo.tbl_server_times ADD ImportedTraceTimestampsInLocalTime bit NULL; "
                        + "EXEC sys.sp_executesql N'UPDATE dbo.tbl_server_times SET ImportedTraceTimestampsInLocalTime = "
                        + (metadata == 2 ? "1" : "0") + "';");
                if (metadata == 3 || metadata == 6)
                    database.Execute(@"CREATE TABLE dbo.tbl_ServerProperties (PropertyName nvarchar(128), PropertyValue nvarchar(128));
INSERT INTO dbo.tbl_ServerProperties VALUES ('UTCOffset_in_Hours', '-7');"
                        + (metadata == 3 ? "INSERT INTO dbo.tbl_ServerProperties VALUES ('ImportedTraceTimestampsInLocalTime', '1');" : ""));
                if (metadata == 4)
                    database.Execute(@"CREATE TABLE ReadTrace.tblMiscInfo (Attribute nvarchar(128), Value nvarchar(128));
INSERT INTO ReadTrace.tblMiscInfo VALUES ('ImportedTraceTimestampsInLocalTime', '1');");
                var prerequisites = new TracePostProcessingPrerequisites();
                prerequisites.RecordResult(ImporterSelectionEvaluator.TraceEventImporterName, true, false);

                foreach (string script in prerequisites.Scripts)
                    database.RunScript(script);
                database.RunScript(TracePostProcessingPrerequisites.TraceScript);

                foreach (string table in new[] { "tblBatches", "tblStatements", "tblConnections" })
                {
                    Assert.AreEqual(new DateTime(2020, 1, 2, expectedHour, 0, 0),
                        database.Scalar("SELECT StartTime_local FROM [ReadTrace].[" + table + "];"));
                    Assert.AreEqual(new DateTime(2020, 1, 2, expectedHour + 1, 0, 0),
                        database.Scalar("SELECT EndTime_local FROM [ReadTrace].[" + table + "];"));
                }
            }
        }

        [TestMethod]
        [TestCategory("CustomXelImportIntegration")]
        public void TracePostProcessing_InvalidFlag_FailsRatherThanAssumingUtc()
        {
            using (var database = new TestDatabase())
            {
                database.CreateTraceTables();
                database.Execute(@"CREATE TABLE dbo.tbl_server_times
(time_delta_hours int, ImportedTraceTimestampsInLocalTime nvarchar(32));
INSERT INTO dbo.tbl_server_times VALUES (7, 'invalid');");
                Assert.ThrowsException<SqlException>(() => database.RunScript(TracePostProcessingPrerequisites.TraceScript));
            }
        }

        private sealed class TestDatabase : IDisposable
        {
            private readonly string name = "SqlNexus_CustomXelTests_" + Guid.NewGuid().ToString("N");
            private readonly string masterConnectionString;
            private readonly SqlConnection connection;

            public TestDatabase()
            {
                if (Environment.GetEnvironmentVariable("SQLNEXUS_RUN_CUSTOM_XEL_IMPORT_INTEGRATION") != "1")
                    Assert.Inconclusive("Explicit permission to create and drop an isolated SQL test database is required.");
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
                using (var master = new SqlConnection(masterConnectionString))
                {
                    master.Open();
                    using (var command = new SqlCommand("CREATE DATABASE [" + name + "];", master))
                        command.ExecuteNonQuery();
                }
                builder.InitialCatalog = name;
                connection = new SqlConnection(builder.ConnectionString);
                try
                {
                    connection.Open();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void CreateTraceTables()
            {
                Execute("CREATE SCHEMA ReadTrace;");
                foreach (string table in new[] { "tblBatches", "tblStatements", "tblConnections" })
                    Execute("CREATE TABLE [ReadTrace].[" + table + @"] (StartTime datetime, EndTime datetime);
INSERT INTO [ReadTrace].[" + table + "] VALUES ('2020-01-02T12:00:00', '2020-01-02T13:00:00');");
            }

            public void Execute(string sql)
            {
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                    command.ExecuteNonQuery();
            }

            public object Scalar(string sql)
            {
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 60 })
                    return command.ExecuteScalar();
            }

            public void RunScript(string scriptName)
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptName);
                using (var sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    Assert.IsTrue(ScriptIntegrityChecker.IsExpectedHash(scriptName,
                        BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "")), "Script integrity mismatch.");
                foreach (string batch in CSql.ParseBatches(File.ReadAllText(path)))
                    if (!string.IsNullOrWhiteSpace(batch))
                        Execute(batch);
            }

            public void Dispose()
            {
                connection.Dispose();
                using (var master = new SqlConnection(masterConnectionString))
                {
                    master.Open();
                    using (var command = new SqlCommand("DROP DATABASE [" + name + "];", master))
                        command.ExecuteNonQuery();
                }
            }
        }
    }
}
