using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;
using System;
using System.IO;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class fmImportTests
    {
        [TestMethod]
        public void CreateSqlPlanInsertCommand_MaliciousContent_UsesXmlParameter()
        {
            string maliciousPlan = "<ShowPlan>'); DROP TABLE dbo.Sensitive;--</ShowPlan>";

            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand command = fmImport.CreateSqlPlanInsertCommand(connection, maliciousPlan))
            {
                Assert.AreEqual("INSERT INTO dbo.tblPlansTemp (sqlplan) VALUES (@sqlplan);", command.CommandText);
                Assert.IsFalse(command.CommandText.Contains(maliciousPlan));
                Assert.AreEqual(maliciousPlan, command.Parameters["@sqlplan"].Value);
                Assert.AreEqual(System.Data.SqlDbType.Xml, command.Parameters["@sqlplan"].SqlDbType);
            }
        }

        [TestMethod]
        public void GetSqlPlanFiles_FilenameContainsSqlMetacharacters_ReturnsFileWithoutInterpretingName()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SqlNexusTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string maliciousFile = Path.Combine(directory, "plan'; DROP TABLE dbo.Sensitive;--.sqlplan");

            try
            {
                File.WriteAllText(maliciousFile, "<ShowPlan />");

                string[] files = fmImport.GetSqlPlanFiles(directory);

                Assert.AreEqual(1, files.Length);
                Assert.AreEqual(maliciousFile, files[0]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void GetSqlPlanFiles_MissingSourcePath_ThrowsArgumentException(string sourcePath)
        {
            Assert.ThrowsException<ArgumentException>(() => fmImport.GetSqlPlanFiles(sourcePath));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void CreateSqlPlanInsertCommand_MissingXml_ThrowsArgumentException(string planXml)
        {
            using (SqlConnection connection = new SqlConnection())
            {
                Assert.ThrowsException<ArgumentException>(() =>
                    fmImport.CreateSqlPlanInsertCommand(connection, planXml));
            }
        }
    }
}
