using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
