using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    /// <summary>
    /// Tests for <see cref="CustomXELImporter.AnyCustomXelSourceFailed"/>, the failure-decision
    /// helper used by ImportCustomXELFiles to set its out 'success' flag (which in turn drives the
    /// /M ImportIncomplete exit code).
    ///
    /// Each Load* method returns a non-negative row count on success and -1 on failure. The
    /// success paths now clamp ExecuteNonQuery() to 0 (guarding against a -1 rowcount from
    /// SET NOCOUNT ON), so a negative value here can ONLY mean a genuine failure.
    /// </summary>
    [TestClass]
    public class CustomXELImporterTests
    {
        [TestMethod]
        public void AllSourcesSucceeded_WithRows_NotFailed()
        {
            // Arrange / Act
            bool failed = CustomXELImporter.AnyCustomXelSourceFailed(5, 10, 3);

            // Assert
            Assert.IsFalse(failed);
        }

        [TestMethod]
        public void AllSourcesSucceeded_ZeroRows_NotFailed()
        {
            // Zero rows (e.g. no matching files, or a clamped -1 rowcount) is a success, not a failure.
            bool failed = CustomXELImporter.AnyCustomXelSourceFailed(0, 0, 0);

            Assert.IsFalse(failed);
        }

        [DataTestMethod]
        [DataRow(-1, 0, 0)]   // SqlDiag failed
        [DataRow(0, -1, 0)]   // AlwaysOn Health failed
        [DataRow(0, 0, -1)]   // system_health failed
        [DataRow(-1, -1, -1)] // all failed
        [DataRow(5, -1, 10)]  // one failure amongst successes
        public void AnySourceNegative_IsFailed(int sqlDiag, int alwaysOn, int systemHealth)
        {
            bool failed = CustomXELImporter.AnyCustomXelSourceFailed(sqlDiag, alwaysOn, systemHealth);

            Assert.IsTrue(failed);
        }

        [TestMethod]
        public void LargeRowCounts_NotFailed()
        {
            // Boundary: large positive counts must still be treated as success.
            bool failed = CustomXELImporter.AnyCustomXelSourceFailed(int.MaxValue, int.MaxValue, int.MaxValue);

            Assert.IsFalse(failed);
        }

        [TestMethod]
        public void CustomXelFileMasks_AreTheThreeCustomXelPatterns()
        {
            // Guards the single-source-of-truth contract (issue #556): fmImport's sibling-gap warning
            // reuses these exact masks, so if the importer's patterns change, this test flags it.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    CustomXELImporter.SqlDiagMask,
                    CustomXELImporter.AlwaysOnHealthMask,
                    CustomXELImporter.SystemHealthMask
                },
                CustomXELImporter.CustomXelFileMasks);
        }

        [DataTestMethod]
        [DataRow("*_SQLDIAG*.xel")]
        [DataRow("*AlwaysOn_health*.xel")]
        [DataRow("*system_health*.xel")]
        public void CustomXelFileMasks_ContainsExpectedPattern(string expectedMask)
        {
            // Regression: pins the literal patterns so a rename in the importer cannot silently
            // desync the SharedOutputFiles Custom XEL warning without failing a test.
            CollectionAssert.Contains(CustomXELImporter.CustomXelFileMasks, expectedMask);
        }

        [DataTestMethod]
        [DataRow(0, "tbl_SQL_Base_SQLDIAGXEL_Startup")]
        [DataRow(1, "tbl_SQL_Base_AlwaysOnHealth")]
        [DataRow(2, "tbl_SQL_Base_SystemHealthXEL_Startup")]
        public void CreateImportCommand_MaliciousFilePattern_UsesParameter(int sourceValue, string expectedTable)
        {
            string maliciousPattern = @"C:\capture\diagnostic'; DROP TABLE dbo.Sensitive;--*.xel";
            CustomXelSource source = (CustomXelSource)sourceValue;
            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand command = CustomXELImporter.CreateImportCommand(connection, source, maliciousPattern, true))
            {
                StringAssert.Contains(command.CommandText, expectedTable);
                StringAssert.Contains(command.CommandText, "@filePattern");
                Assert.IsFalse(command.CommandText.Contains(maliciousPattern));
                Assert.AreEqual(maliciousPattern, command.Parameters["@filePattern"].Value);
                Assert.AreEqual(System.Data.SqlDbType.NVarChar, command.Parameters["@filePattern"].SqlDbType);
                Assert.AreEqual(4000, command.Parameters["@filePattern"].Size);
            }
        }

        [TestMethod]
        public void CreateImportCommand_DropDisabled_DoesNotIncludeDropStatement()
        {
            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand command = CustomXELImporter.CreateImportCommand(
                connection, CustomXelSource.SqlDiag, @"C:\capture\*.xel", false))
            {
                Assert.IsFalse(command.CommandText.Contains("DROP TABLE"));
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void CreateImportCommand_MissingFilePattern_ThrowsArgumentException(string filePattern)
        {
            using (SqlConnection connection = new SqlConnection())
            {
                Assert.ThrowsException<System.ArgumentException>(() =>
                    CustomXELImporter.CreateImportCommand(connection, CustomXelSource.SqlDiag, filePattern, true));
            }
        }
    }
}
