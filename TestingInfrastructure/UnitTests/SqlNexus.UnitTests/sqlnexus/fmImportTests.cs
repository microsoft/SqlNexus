using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;
using System;
using System.Data;
using System.Data.SqlTypes;
using System.IO;
using System.Security;
using System.Xml;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using NexusInterfaces;

namespace SqlNexus.UnitTests.sqlnexus
{
    using CustomXELImporter = global::CustomXELImporter.CustomXELImporter;

    [TestClass]
    public class fmImportTests
    {
        [TestMethod]
        public void CreateImporterMenu_CustomXel_UsesStandardTaggedAccessibleOptions()
        {
            var importer = new CustomXELImporter();
            importer.Options["Enabled"] = true;
            importer.Options[CustomXELImporter.DropTablesOption] = false;
            using (ToolStripMenuItem menu = fmImport.CreateImporterMenu(importer))
            {
                Assert.AreSame(importer, menu.Tag);
                Assert.AreEqual(importer.Name, menu.AccessibleName);
                Assert.AreEqual(2, menu.DropDownItems.Count);
                foreach (ToolStripMenuItem item in menu.DropDownItems)
                {
                    Assert.AreSame(importer, item.Tag);
                    Assert.IsTrue(item.CheckOnClick);
                    Assert.AreEqual((bool)importer.Options[item.Text], item.Checked);
                    Assert.AreEqual(item.Text, item.AccessibleName);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.AccessibleDescription));
                }

                var closing = new ToolStripDropDownClosingEventArgs(ToolStripDropDownCloseReason.ItemClicked);
                typeof(ToolStripDropDown).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(menu.DropDown, new object[] { closing });
                Assert.IsTrue(closing.Cancel);
            }
        }

        [TestMethod]
        public void ImporterAssembly_ReflectionDiscovery_FindsSignedPluginWithoutHostDependency()
        {
            string dll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomXELImporter.dll");
            Assembly assembly = Assembly.LoadFile(dll);
            Type type = assembly.GetExportedTypes().Single(candidate =>
                candidate.IsClass && !candidate.IsAbstract && typeof(INexusImporter).IsAssignableFrom(candidate));
            var importer = (INexusImporter)Activator.CreateInstance(type);

            Assert.AreEqual("CustomXELImporter", assembly.GetName().Name);
            Assert.AreEqual(ImporterSelectionEvaluator.CustomXelImporterName, importer.Name);
            Assert.AreEqual(new Guid("05F43A63-2922-457B-BDB2-368280A8DB9E"), importer.ID);
            Assert.AreEqual(false, importer.Options["Enabled"]);
            Assert.AreNotSame(typeof(fmImport).Assembly, assembly);
            Assert.IsNull(typeof(fmImport).Assembly.GetType("sqlnexus.CustomXELImporter"));
            Assert.IsFalse(assembly.GetReferencedAssemblies().Any(reference => reference.Name == "sqlnexus"));
            CollectionAssert.AreEqual(typeof(INexusImporter).Assembly.GetName().GetPublicKeyToken(),
                assembly.GetName().GetPublicKeyToken());
        }

        [TestMethod]
        public void OrderedImporterFiles_CustomXel_IsLastWithoutChangingKnownImporterOrder()
        {
            string[] files = { "CustomXELImporter.dll", "PerfmonImporter.dll", "ReadTraceNexusImporter.dll",
                "TraceEventImporter.dll", "RowsetImportEngine.dll", "ErrorLogImporter.dll", "LinuxPerfImporter.dll" };
            List<string> ordered = fmImport.OrderedImporterFiles(files);

            Assert.AreEqual("CustomXELImporter.dll", ordered.Last());
            Assert.IsTrue(ordered.IndexOf("RowsetImportEngine.dll") < ordered.IndexOf("TraceEventImporter.dll"));
            Assert.IsTrue(ordered.IndexOf("TraceEventImporter.dll") < ordered.IndexOf("ReadTraceNexusImporter.dll"));
            Assert.IsTrue(ordered.IndexOf("ReadTraceNexusImporter.dll") < ordered.IndexOf("PerfmonImporter.dll"));
            CollectionAssert.AreEquivalent(files, ordered);
        }

        [TestMethod]
        public void OrderedImporterFiles_EmptyNullAndCaseInsensitiveName_AreHandledExplicitly()
        {
            Assert.AreEqual(0, fmImport.OrderedImporterFiles(new string[0]).Count);
            Assert.ThrowsException<ArgumentNullException>(() => fmImport.OrderedImporterFiles(null));
            CollectionAssert.AreEqual(new[] { @"C:\Plugins\ErrorLogImporter.dll", @"C:\Plugins\customxelimporter.DLL" },
                fmImport.OrderedImporterFiles(new[] { @"C:\Plugins\customxelimporter.DLL", @"C:\Plugins\ErrorLogImporter.dll" }));
        }

        [TestMethod]
        public void CustomXelSearchPaths_SharedSiblingExists_ReturnsOnlyPrimary()
        {
            using (var fixture = new PlanFixture())
            {
                string sibling = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(fixture.Root), "SharedOutputFiles")).FullName;
                CollectionAssert.AreEqual(new[] { fixture.Root },
                    SharedOutputFolder.GetImportSearchPaths(fixture.Root, new CustomXELImporter()));
                CollectionAssert.AreEqual(new[] { fixture.Root, sibling },
                    SharedOutputFolder.GetImportSearchPaths(fixture.Root));
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

        [DataTestMethod]
        [DataRow("<ShowPlan>'); DROP TABLE dbo.Sensitive;--</ShowPlan>")]
        [DataRow("<?xml version=\"1.0\" encoding=\"utf-8\"?><ShowPlan>caf\u00e9</ShowPlan>")]
        [DataRow("<?xml version=\"1.0\" encoding=\"utf-16\"?><ShowPlan />")]
        public void CreateSqlPlanInsertCommand_ValidXml_UsesXmlParameter(string planXml)
        {
            using (var connection = new SqlConnection())
            using (SqlCommand command = fmImport.CreateSqlPlanInsertCommand(connection, planXml))
            {
                Assert.AreEqual("INSERT INTO dbo.tblPlansTemp (sqlplan) VALUES (@sqlplan);", command.CommandText);
                Assert.IsFalse(command.CommandText.Contains(planXml));
                Assert.AreEqual(SqlDbType.Xml, command.Parameters["@sqlplan"].SqlDbType);
                var expected = new XmlDocument { XmlResolver = null };
                expected.LoadXml(planXml);
                var actual = new XmlDocument { XmlResolver = null };
                actual.LoadXml(((SqlXml)command.Parameters["@sqlplan"].Value).Value);
                Assert.AreEqual(expected.DocumentElement.OuterXml, actual.DocumentElement.OuterXml);
                Assert.AreSame(connection, command.Connection);
            }
        }

        [TestMethod]
        public void CreateSqlPlanInsertCommand_NullConnection_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                fmImport.CreateSqlPlanInsertCommand(null, "<ShowPlan />"));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void CreateSqlPlanInsertCommand_MissingXml_ThrowsArgumentException(string planXml)
        {
            using (var connection = new SqlConnection())
            {
                Assert.ThrowsException<ArgumentException>(() =>
                    fmImport.CreateSqlPlanInsertCommand(connection, planXml));
            }
        }

        [DataTestMethod]
        [DataRow("<ShowPlan>")]
        [DataRow("not XML")]
        [DataRow("<!DOCTYPE ShowPlan [<!ENTITY file SYSTEM 'file:///not-read'>]><ShowPlan>&file;</ShowPlan>")]
        public void CreateSqlPlanInsertCommand_InvalidOrExternalEntityXml_ThrowsXmlException(string planXml)
        {
            using (var connection = new SqlConnection())
            {
                Assert.ThrowsException<XmlException>(() =>
                    fmImport.CreateSqlPlanInsertCommand(connection, planXml));
            }
        }

        [TestMethod]
        public void GetSqlPlanFiles_SpecialCharactersAndNestedFiles_ReturnsOnlyTopLevelPlans()
        {
            using (var fixture = new PlanFixture())
            {
                string planFile = Path.Combine(fixture.Root, "plan'; DROP TABLE dbo.Sensitive;--.sqlplan");
                File.WriteAllText(planFile, "<ShowPlan />");
                File.WriteAllText(Path.Combine(fixture.Root, "not-a-plan.txt"), "ignored");
                string child = Directory.CreateDirectory(Path.Combine(fixture.Root, "nested")).FullName;
                File.WriteAllText(Path.Combine(child, "nested.sqlplan"), "<ShowPlan />");

                string[] files = fmImport.GetSqlPlanFiles(fixture.Root + Path.DirectorySeparatorChar);

                CollectionAssert.AreEqual(new[] { planFile }, files);
            }
        }

        [TestMethod]
        public void GetSqlPlanFiles_EmptyFolder_ReturnsEmptyArray()
        {
            using (var fixture = new PlanFixture())
                Assert.AreEqual(0, fmImport.GetSqlPlanFiles(fixture.Root).Length);
        }

        [TestMethod]
        public void GetSqlPlanFiles_NonexistentFolder_ThrowsDirectoryNotFoundException()
        {
            using (var fixture = new PlanFixture())
            {
                Assert.ThrowsException<DirectoryNotFoundException>(() =>
                    fmImport.GetSqlPlanFiles(Path.Combine(fixture.Root, "missing")));
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow(@"C:\capture\..\other")]
        public void GetSqlPlanFiles_InvalidSourcePath_ThrowsArgumentException(string sourcePath)
        {
            Assert.ThrowsException<ArgumentException>(() => fmImport.GetSqlPlanFiles(sourcePath));
        }

        [TestMethod]
        public void ReadVerifiedScript_MissingFile_ThrowsFileNotFoundException()
        {
            using (var fixture = new PlanFixture())
            {
                Assert.ThrowsException<FileNotFoundException>(() =>
                    fmImport.ReadVerifiedScript(Path.Combine(fixture.Root, "missing.sql")));
            }
        }

        [TestMethod]
        public void ReadVerifiedScript_UntrustedScript_ThrowsSecurityException()
        {
            using (var fixture = new PlanFixture())
            {
                string script = Path.Combine(fixture.Root, "untrusted.sql");
                File.WriteAllText(script, "SELECT 1;");

                Assert.ThrowsException<SecurityException>(() => fmImport.ReadVerifiedScript(script));
            }
        }

        private sealed class PlanFixture : IDisposable
        {
            public string Root { get; } = Path.Combine(Path.GetTempPath(), "SqlNexusPlans_" + Guid.NewGuid().ToString("N"), "output (2)");

            public PlanFixture()
            {
                Directory.CreateDirectory(Root);
            }

            public void Dispose()
            {
                Directory.Delete(Path.GetDirectoryName(Root), true);
            }
        }
    }
}
