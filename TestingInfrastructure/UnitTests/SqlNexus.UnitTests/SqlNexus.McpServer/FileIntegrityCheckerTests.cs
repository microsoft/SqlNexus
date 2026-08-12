using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlNexus.McpServer;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    /// <summary>
    /// Tests for <see cref="FileIntegrityChecker.VerifyAll"/>, the startup integrity gate that
    /// refuses to run the MCP server if the protected AI guidance files are missing, unreadable,
    /// or tampered with.
    ///
    /// The result depends on the machine's file layout (whether the repository root and the exact,
    /// unmodified guidance files are present), so these tests assert the method's *contract
    /// invariants* rather than a fixed true/false, keeping them deterministic on any machine/CI:
    ///   - The out 'error' is always non-null.
    ///   - A failure (false) always yields a non-empty, user-facing error message.
    ///   - Success (true) always yields an empty error.
    ///   - Repeated calls are consistent (the method is a pure read-only check).
    /// </summary>
    [TestClass]
    public class FileIntegrityCheckerTests
    {
        [TestMethod]
        public void VerifyAll_ErrorOutParameter_IsNeverNull()
        {
            FileIntegrityChecker.VerifyAll(out string error);
            Assert.IsNotNull(error);
        }

        [TestMethod]
        public void VerifyAll_ReturnAndErrorAreConsistent()
        {
            bool ok = FileIntegrityChecker.VerifyAll(out string error);

            if (ok)
            {
                Assert.AreEqual(string.Empty, error, "Success must return an empty error message.");
            }
            else
            {
                Assert.IsFalse(string.IsNullOrEmpty(error),
                    "Failure must return an explicit, user-facing error message.");
                StringAssert.Contains(error, "SQL Nexus MCP Server cannot start");
            }
        }

        [TestMethod]
        public void VerifyAll_IsDeterministic_RepeatedCallsAgree()
        {
            bool first = FileIntegrityChecker.VerifyAll(out _);
            bool second = FileIntegrityChecker.VerifyAll(out _);
            Assert.AreEqual(first, second, "A read-only integrity check must be deterministic.");
        }
    }
}
