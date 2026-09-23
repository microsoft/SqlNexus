using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class ScriptIntegrityCheckerTests
    {
        [TestMethod]
        public void IsExpectedHash_DeployedPostProcessingScript_ReturnsTrue()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLNexus_PostProcessing.sql");
            string actualHash;
            using (FileStream stream = File.OpenRead(scriptPath))
            using (SHA256 sha = SHA256.Create())
            {
                actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }

            bool result = ScriptIntegrityChecker.IsExpectedHash("SQLNexus_PostProcessing.sql", actualHash);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyScript_UnknownScript_ReturnsFalse()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unknown.sql");

            bool result = ScriptIntegrityChecker.VerifyScript(scriptPath);

            Assert.IsFalse(result);
        }
    }
}
