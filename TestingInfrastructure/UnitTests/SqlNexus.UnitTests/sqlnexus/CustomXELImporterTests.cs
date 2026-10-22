using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using NexusInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class CustomXELImporterTests
    {
        [TestMethod]
        public void NewImporter_StandardContract_HasSafeDefaults()
        {
            INexusImporter importer = new CustomXELImporter();
            Assert.AreEqual(CustomXELImporter.ImporterName, importer.Name);
            Assert.AreNotEqual(Guid.Empty, importer.ID);
            Assert.AreEqual(new CustomXELImporter().ID, importer.ID);
            Assert.AreEqual(false, importer.Options["Enabled"]);
            Assert.AreEqual(true, importer.Options[CustomXELImporter.DropTablesOption]);
            Assert.IsTrue(((INexusImporterPolicy)importer).PrimaryFolderOnly);
            Assert.IsTrue(((INexusImporterPolicy)importer).StopOnFailure);
            Assert.IsFalse(importer is INexusFileImporter);
            Assert.AreEqual(0, importer.PreScripts.Length);
            Assert.AreEqual(0, importer.PostScripts.Length);
            Assert.IsNull(importer.OptionsDialog);
            Assert.AreEqual(ImportState.Idle, importer.State);
        }

        [TestMethod]
        public void SupportedMasks_ModifiedCopy_DoesNotChangeCategoryDefinitions()
        {
            var importer = new CustomXELImporter();
            importer.SupportedMasks[0] = "*.xel";
            CollectionAssert.AreEqual(new[] { "*_SQLDIAG*.xel", "*AlwaysOn_health*.xel", "*system_health*.xel" },
                importer.SupportedMasks);
        }

        [DataTestMethod]
        [DataRow(0, "tbl_SqlDiagXel")]
        [DataRow(1, "tbl_AlwaysOnHealthXel")]
        [DataRow(2, "tbl_SystemHealthXel")]
        public void CreateImportCommand_AdversarialPath_PreservesParameterAndCanonicalTable(int category, string table)
        {
            string pattern = @"C:\O'Brien;-- capture (2)\diagnostic'; DROP TABLE dbo.Sensitive;--*.xel";
            using (var connection = new SqlConnection())
            using (var command = CustomXELImporter.CreateImportCommand(connection, (CustomXelSource)category, pattern, true))
            {
                Assert.AreSame(connection, command.Connection);
                Assert.AreEqual(0, command.CommandTimeout);
                Assert.AreEqual(pattern, command.Parameters["@filePattern"].Value);
                Assert.AreEqual(System.Data.SqlDbType.NVarChar, command.Parameters["@filePattern"].SqlDbType);
                Assert.AreEqual(4000, command.Parameters["@filePattern"].Size);
                Assert.IsFalse(command.CommandText.Contains(pattern));
                StringAssert.Contains(command.CommandText, "[dbo].[" + table + "]");
                StringAssert.Contains(command.CommandText, "sys.fn_xe_file_target_read_file(@filePattern, NULL, NULL, NULL)");
                Assert.IsTrue(command.CommandText.IndexOf("SELECT * INTO #CustomXelInput", StringComparison.Ordinal)
                    < command.CommandText.IndexOf("DROP TABLE", StringComparison.Ordinal));
                StringAssert.Contains(command.CommandText, "SELECT COUNT_BIG(*)");
            }
        }

        [TestMethod]
        public void CreateImportCommand_Append_ValidatesBothSchemaDirectionsAndRejectsUnmigratedLegacyData()
        {
            using (var connection = new SqlConnection())
            using (var command = CustomXELImporter.CreateImportCommand(connection, CustomXelSource.SqlDiag, @"C:\capture\*.xel", false))
            {
                Assert.IsFalse(command.CommandText.Contains("DROP TABLE"));
                StringAssert.Contains(command.CommandText, "INSERT INTO [dbo].[tbl_SqlDiagXel]");
                StringAssert.Contains(command.CommandText, "EXCEPT");
                StringAssert.Contains(command.CommandText, "OR EXISTS");
                StringAssert.Contains(command.CommandText, "collation_name COLLATE DATABASE_DEFAULT");
                StringAssert.Contains(command.CommandText, "THROW 50002");
                StringAssert.Contains(command.CommandText, "tbl_SQL_Base_SQLDIAGXEL_Startup");
                StringAssert.Contains(command.CommandText, "THROW 50001");
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void CreateImportCommand_MissingPattern_RejectsInput(string pattern)
        {
            using (var connection = new SqlConnection())
                Assert.ThrowsException<ArgumentException>(() =>
                    CustomXELImporter.CreateImportCommand(connection, CustomXelSource.SqlDiag, pattern, true));
        }

        [TestMethod]
        public void CreateImportCommand_NullConnection_RejectsInput()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                CustomXELImporter.CreateImportCommand(null, CustomXelSource.SqlDiag, @"C:\capture\*.xel", true));
        }

        [TestMethod]
        public void CreateImportCommand_UnknownCategory_RejectsInput()
        {
            using (var connection = new SqlConnection())
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    CustomXELImporter.CreateImportCommand(connection, (CustomXelSource)99, @"C:\capture\*.xel", true));
        }

        [DataTestMethod]
        [DataRow(4000)]
        [DataRow(4001)]
        public void CreateImportCommand_ParameterSizeBoundary_DoesNotTruncate(int length)
        {
            string pattern = new string('x', length);
            using (var connection = new SqlConnection())
            {
                if (length > 4000)
                    Assert.ThrowsException<ArgumentException>(() =>
                        CustomXELImporter.CreateImportCommand(connection, CustomXelSource.SqlDiag, pattern, false));
                else
                    using (var command = CustomXELImporter.CreateImportCommand(connection, CustomXelSource.SqlDiag, pattern, false))
                        Assert.AreEqual(pattern, command.Parameters["@filePattern"].Value);
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow(@"capture\*_SQLDIAG*.xel")]
        [DataRow(@"C:capture\*_SQLDIAG*.xel")]
        [DataRow(@"\capture\*_SQLDIAG*.xel")]
        [DataRow(@"C:\capture\..\other\*_SQLDIAG*.xel")]
        [DataRow(@"C:\capture\.\*_SQLDIAG*.xel")]
        [DataRow(@"C:\cap*\*_SQLDIAG*.xel")]
        [DataRow(@"C:\capture\*.xel")]
        [DataRow(@"C:\capture\server_SQLDIAG_1.xel")]
        public void Initialize_InvalidMaskOrTraversal_RejectsInput(string mask)
        {
            var importer = new CustomXELImporter();
            Assert.ThrowsException<ArgumentException>(() => Initialize(importer, mask, new CapturingLogger()));
        }

        [TestMethod]
        public void Initialize_MissingConnectionOrLogger_RejectsInput()
        {
            var importer = new CustomXELImporter();
            Assert.ThrowsException<ArgumentNullException>(() => Initialize(importer, @"C:\capture\*_SQLDIAG*.xel", null));
            Assert.ThrowsException<ArgumentException>(() => importer.Initialize(@"C:\capture\*_SQLDIAG*.xel", "",
                "", true, "", "", "", new CapturingLogger()));
        }

        [TestMethod]
        public void DoImport_BeforeInitialize_RejectsCall()
        {
            Assert.ThrowsException<InvalidOperationException>(() => new CustomXELImporter().DoImport());
        }

        [DataTestMethod]
        [DataRow(0, "a_SQLDIAG_1.xel", "b_SQLDIAG_2.xel")]
        [DataRow(1, "a_AlwaysOn_health_1.xel", "b_AlwaysOn_health_2.xel")]
        [DataRow(2, "a_system_health_1.xel", "b_system_health_2.xel")]
        public void DoImport_HostSelectedCategory_LoadsAllPrefixesOnceWithLongCounts(int category, string first, string second)
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, first), "");
                File.WriteAllText(Path.Combine(fixture.Primary, second), "");
                long expected = (long)int.MaxValue + 1;
                int calls = 0;
                var importer = new CustomXELImporter((source, pattern, drop) =>
                {
                    calls++;
                    Assert.AreEqual((CustomXelSource)category, source);
                    Assert.AreEqual(Path.Combine(fixture.Primary, CustomXELImporter.CustomXelFileMasks[category]), pattern);
                    Assert.IsTrue(drop);
                    return expected;
                });
                var states = new List<ImportState>();
                importer.StatusChanged += (sender, e) => states.Add(importer.State);
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[category]), new CapturingLogger());

                Assert.IsTrue(importer.DoImport()); // Host gating must not re-read the saved Enabled option.
                Assert.AreEqual(false, importer.Options["Enabled"]);
                Assert.AreEqual(1, calls);
                Assert.AreEqual(2, importer.TotalFilesFound);
                Assert.AreEqual(expected, importer.TotalRowsInserted);
                Assert.AreEqual(expected, ((INexusImportedRowset)importer.KnownRowsets[0]).RowsInserted);
                Assert.AreEqual(CustomXELImporter.GetTableName((CustomXelSource)category),
                    ((INexusImportedRowset)importer.KnownRowsets[0]).Name);
                CollectionAssert.Contains(states, ImportState.Importing);
                Assert.AreEqual(ImportState.Idle, importer.State);
            }
        }

        [TestMethod]
        public void DoImport_NestedAndSiblingFiles_AreNotLoadedAndTablesAreUntouched()
        {
            using (var fixture = new XelFixture())
            {
                string nested = Directory.CreateDirectory(Path.Combine(fixture.Primary, "nested")).FullName;
                File.WriteAllText(Path.Combine(nested, "nested_SQLDIAG_1.xel"), "");
                File.WriteAllText(Path.Combine(fixture.Shared, "shared_SQLDIAG_1.xel"), "");
                int calls = 0;
                var importer = new CustomXELImporter((source, pattern, drop) => { calls++; return 0; });
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), new CapturingLogger());

                CollectionAssert.AreEqual(new[] { fixture.Primary },
                    SharedOutputFolder.GetImportSearchPaths(fixture.Primary, importer));
                CollectionAssert.AreEqual(new[] { fixture.Primary, fixture.Shared },
                    SharedOutputFolder.GetImportSearchPaths(fixture.Primary));
                Assert.IsTrue(importer.DoImport());
                Assert.AreEqual(ImportState.NoFiles, importer.State);
                Assert.AreEqual(0, calls);
                Assert.AreEqual(0, importer.TotalFilesFound);
                Assert.AreEqual(0L, importer.TotalRowsInserted);
            }
        }

        [TestMethod]
        public void DoImport_ReadFailure_ReportsFailureWithoutClaimingRows()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                var logger = new CapturingLogger();
                var importer = new CustomXELImporter((source, pattern, drop) => { throw new IOException("fixture failure"); });
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), logger);

                Assert.IsFalse(importer.DoImport());
                Assert.AreEqual(0L, importer.TotalRowsInserted);
                Assert.AreEqual(0, importer.KnownRowsets.Count);
                StringAssert.Contains(string.Join(" ", logger.Messages), "fixture failure");
            }
        }

        [TestMethod]
        public void DoImport_AmbiguousCategoryName_RejectsFileBeforeExecutingSql()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_system_health_1.xel"), "");
                int calls = 0;
                var importer = new CustomXELImporter((source, pattern, drop) => { calls++; return 10; });
                var logger = new CapturingLogger();
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), logger);

                Assert.IsFalse(importer.DoImport());
                Assert.AreEqual(0, calls);
                StringAssert.Contains(string.Join(" ", logger.Messages), "multiple categories");
            }
        }

        [TestMethod]
        public void DoImport_NegativeCount_IsFailure()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                var importer = new CustomXELImporter((source, pattern, drop) => -1);
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), new CapturingLogger());
                Assert.IsFalse(importer.DoImport());
                Assert.AreEqual(0L, importer.TotalRowsInserted);
            }
        }

        [TestMethod]
        public void Cancel_BeforeSqlExecution_StopsAndInitializeResetsForNextRun()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                int calls = 0;
                var importer = new CustomXELImporter((source, pattern, drop) => { calls++; return 3; });
                string mask = Path.Combine(fixture.Primary, importer.SupportedMasks[0]);
                Initialize(importer, mask, new CapturingLogger());
                importer.Cancel();
                Assert.IsFalse(importer.DoImport());
                Assert.IsTrue(importer.Cancelled);
                Assert.AreEqual(ImportState.Canceling, importer.State);
                Assert.AreEqual(0, calls);

                Initialize(importer, mask, new CapturingLogger());
                Assert.IsFalse(importer.Cancelled);
                Assert.IsTrue(importer.DoImport());
                Assert.AreEqual(3L, importer.TotalRowsInserted);

                Initialize(importer, mask, new CapturingLogger());
                Assert.AreEqual(0L, importer.TotalRowsInserted);
                Assert.AreEqual(0, importer.KnownRowsets.Count);
                Assert.IsTrue(importer.DoImport());
                Assert.AreEqual(3L, importer.TotalRowsInserted);
                Assert.AreEqual(2, calls);
            }
        }

        [TestMethod]
        public void Cancel_WhileImporting_ReportsCancellationNotSuccess()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                CustomXELImporter importer = null;
                importer = new CustomXELImporter((source, pattern, drop) =>
                {
                    importer.Cancel();
                    throw new OperationCanceledException();
                });
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), new CapturingLogger());
                Assert.IsFalse(importer.DoImport());
                Assert.IsTrue(importer.Cancelled);
                Assert.AreEqual(0L, importer.TotalRowsInserted);
            }
        }

        [TestMethod]
        public void DoImport_AppendOption_IsPassedToReader()
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                var importer = new CustomXELImporter((source, pattern, drop) =>
                {
                    Assert.IsFalse(drop);
                    return 0;
                });
                importer.Options[CustomXELImporter.DropTablesOption] = false;
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), new CapturingLogger());
                Assert.IsTrue(importer.DoImport());
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("invalid")]
        public void DoImport_InvalidDropOption_IsRejectedBeforeReading(string value)
        {
            using (var fixture = new XelFixture())
            {
                File.WriteAllText(Path.Combine(fixture.Primary, "server_SQLDIAG_1.xel"), "");
                int calls = 0;
                var importer = new CustomXELImporter((source, pattern, drop) => { calls++; return 5; });
                importer.Options[CustomXELImporter.DropTablesOption] = value;
                var logger = new CapturingLogger();
                Initialize(importer, Path.Combine(fixture.Primary, importer.SupportedMasks[0]), logger);
                Assert.IsFalse(importer.DoImport());
                Assert.AreEqual(0, calls);
                StringAssert.Contains(string.Join(" ", logger.Messages), "must be boolean");
            }
        }

        [DataTestMethod]
        [DataRow(true, true, false)]
        [DataRow(true, false, true)]
        [DataRow(false, true, false)]
        public void ApplySavedOptions_LegacyValues_MigratesOnlyWhenSavingEnabled(bool save, bool enabled, bool drop)
        {
            var saved = new Dictionary<string, bool>
            {
                { "SaveImportOptions", save },
                { "SQLDiagAlwaysOnXEL.Enabled", enabled },
                { "SQLDiagAlwaysOnXEL.DropExistingTables", drop }
            };
            var importer = new CustomXELImporter();
            Assert.AreEqual(save, ImportOptions.ApplySavedOptions(importer, saved));
            Assert.AreEqual(save ? enabled : false, importer.Options["Enabled"]);
            Assert.AreEqual(save ? drop : true, importer.Options[CustomXELImporter.DropTablesOption]);
            Assert.AreEqual(save, saved.ContainsKey(importer.Name + ".Enabled"));
        }

        [TestMethod]
        public void ApplySavedOptions_NewKeysWinAndMissingKeysKeepDefaults()
        {
            var saved = new Dictionary<string, bool>
            {
                { "SaveImportOptions", true },
                { "SQLDiagAlwaysOnXEL.Enabled", true },
                { CustomXELImporter.ImporterName + ".Enabled", false }
            };
            var importer = new CustomXELImporter();
            Assert.IsFalse(ImportOptions.ApplySavedOptions(importer, saved));
            Assert.AreEqual(false, importer.Options["Enabled"]);
            Assert.AreEqual(true, importer.Options[CustomXELImporter.DropTablesOption]);
        }

        private static void Initialize(CustomXELImporter importer, string mask, ILogger logger)
        {
            importer.Initialize(mask, "Data Source=unused;Integrated Security=true", "unused", true, "", "", "unused", logger);
        }

        private sealed class XelFixture : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "SqlNexusXel_" + Guid.NewGuid().ToString("N"));
            public string Primary { get; }
            public string Shared { get; }

            public XelFixture()
            {
                Primary = Directory.CreateDirectory(Path.Combine(root, "O'Brien;-- capture (2)")).FullName;
                Shared = Directory.CreateDirectory(Path.Combine(root, "SharedOutputFiles")).FullName;
            }

            public void Dispose() { Directory.Delete(root, true); }
        }

        internal sealed class CapturingLogger : ILogger
        {
            public List<string> Messages { get; } = new List<string>();
            public TraceSource TraceLogger { get; } = new TraceSource("CustomXelTests");
            public void InitializeLog(string logfilename) { }
            public void LogMessage(string msg) { Messages.Add(msg); }
            public void LogMessage(string msg, MessageOptions options) { Messages.Add(msg); }
            public void LogMessage(string msg, MessageOptions options, TraceEventType eventtype, string title) { Messages.Add(msg); }
            public void LogMessage(string msg, string[] args) { Messages.Add(msg); }
            public void LogMessage(string msg, string[] args, MessageOptions options, TraceEventType eventtype) { Messages.Add(msg); }
            public void ClearMessage() { }
            public DialogResult LogMessage(string msg, string title, MessageBoxButtons buttons)
            {
                Messages.Add(msg);
                return DialogResult.OK;
            }
        }
    }
}
