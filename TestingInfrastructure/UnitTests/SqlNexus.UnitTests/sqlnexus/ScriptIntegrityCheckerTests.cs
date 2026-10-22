using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class ScriptIntegrityCheckerTests
    {
        [DataTestMethod]
        [DataRow("SQLNexus_PostProcessing.sql")]
        [DataRow("TraceTimestampPostProcessing.sql")]
        public void IsExpectedHash_DeployedPostProcessingScript_ReturnsTrue(string scriptName)
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptName);
            string actualHash;
            using (FileStream stream = File.OpenRead(scriptPath))
            using (SHA256 sha = SHA256.Create())
                actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");

            Assert.IsTrue(ScriptIntegrityChecker.IsExpectedHash(scriptName, actualHash));
            Assert.IsTrue(ScriptIntegrityChecker.IsExpectedHash(scriptName, actualHash.ToLowerInvariant()));
        }

        [DataTestMethod]
        [DataRow("SQLNexus_PostProcessing.sql", null)]
        [DataRow("SQLNexus_PostProcessing.sql", "")]
        [DataRow("SQLNexus_PostProcessing.sql", "incorrect hash")]
        [DataRow(null, "incorrect hash")]
        [DataRow("", "incorrect hash")]
        [DataRow("Unknown.sql", "incorrect hash")]
        public void IsExpectedHash_InvalidInputOrMismatchedHash_ReturnsFalse(string scriptName, string hash)
        {
            Assert.IsFalse(ScriptIntegrityChecker.IsExpectedHash(scriptName, hash));
        }

        [TestMethod]
        public void VerifyScript_UnknownScript_ReturnsFalse()
        {
            Assert.IsFalse(ScriptIntegrityChecker.VerifyScript(Path.Combine(Application.StartupPath, "Unknown.sql")));
        }

        [TestMethod]
        public void VerifyScript_RetiredCommandScript_ReturnsFalse()
        {
            Assert.IsFalse(ScriptIntegrityChecker.VerifyScript(Path.Combine(Application.StartupPath, "PostProcess.cmd")));
        }
    }
}
