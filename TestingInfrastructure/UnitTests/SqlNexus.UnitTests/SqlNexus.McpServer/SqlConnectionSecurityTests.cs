using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Security = global::SqlNexus.McpServer.SqlConnectionSecurity;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class SqlConnectionSecurityTests
    {
        private static string Build(
            List<string> warnings,
            string encrypt = null,
            string trust = null,
            string allow = null,
            bool trusted = true,
            string userId = null,
            string password = null)
        {
            return Security.BuildConnectionString(
                "server1", "SqlNexus", trusted, userId, password,
                encrypt, trust, allow, "sqlnexus-mcp-server", warnings);
        }

        [TestMethod]
        public void BuildConnectionString_Defaults_EncryptsAndValidatesCertificate()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(Build(warnings));

            Assert.IsTrue((bool)builder.Encrypt);
            Assert.IsFalse(builder.TrustServerCertificate);
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public void BuildConnectionString_EmptyValues_UseSecureDefaults()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(Build(warnings, "", " ", ""));

            Assert.IsTrue((bool)builder.Encrypt);
            Assert.IsFalse(builder.TrustServerCertificate);
        }

        [TestMethod]
        public void BuildConnectionString_TrustWithoutOptIn_Throws()
        {
            // Regression: TrustServerCertificate used to be hard-coded to true.
            var warnings = new List<string>();

            Assert.ThrowsException<InvalidOperationException>(() => Build(warnings, trust: "true"));
            Assert.ThrowsException<InvalidOperationException>(() => Build(warnings, trust: "true", allow: "false"));
        }

        [TestMethod]
        public void BuildConnectionString_AllowWithoutTrust_StillValidatesCertificate()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(Build(warnings, allow: "true"));

            Assert.IsFalse(builder.TrustServerCertificate);
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public void BuildConnectionString_TrustWithOptIn_TrustsCertificateAndWarns()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(Build(warnings, trust: "true", allow: "true"));

            Assert.IsTrue((bool)builder.Encrypt);
            Assert.IsTrue(builder.TrustServerCertificate);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "certificate validation");
        }

        [TestMethod]
        public void BuildConnectionString_EncryptDisabled_WarnsAndDoesNotEncrypt()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(Build(warnings, encrypt: "false"));

            Assert.IsFalse((bool)builder.Encrypt);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "encryption");
        }

        [TestMethod]
        public void BuildConnectionString_InvalidBoolean_Throws()
        {
            var warnings = new List<string>();

            Assert.ThrowsException<ArgumentException>(() => Build(warnings, encrypt: "maybe"));
            Assert.ThrowsException<ArgumentException>(() => Build(warnings, trust: "1"));
            Assert.ThrowsException<ArgumentException>(() => Build(warnings, allow: "yes"));
        }

        [TestMethod]
        public void BuildConnectionString_SqlAuthWithoutCredentials_Throws()
        {
            var warnings = new List<string>();

            Assert.ThrowsException<InvalidOperationException>(() => Build(warnings, trusted: false, userId: "u"));
            Assert.ThrowsException<InvalidOperationException>(() => Build(warnings, trusted: false, password: "p"));
        }

        [TestMethod]
        public void BuildConnectionString_SqlAuth_SetsCredentials()
        {
            var warnings = new List<string>();

            var builder = new SqlConnectionStringBuilder(
                Build(warnings, trusted: false, userId: "reader", password: "pw"));

            Assert.IsFalse(builder.IntegratedSecurity);
            Assert.AreEqual("reader", builder.UserID);
            Assert.AreEqual("pw", builder.Password);
        }

        [TestMethod]
        public void BuildConnectionString_Warnings_DoNotContainConnectionStringOrSecrets()
        {
            var warnings = new List<string>();

            Build(warnings, trust: "true", allow: "true", trusted: false, userId: "reader", password: "S3cret!");

            foreach (var warning in warnings)
            {
                Assert.IsFalse(warning.Contains("S3cret!"));
                Assert.IsFalse(warning.Contains("server1"));
            }
        }

        [TestMethod]
        public void ParseBool_NullOrWhitespace_ReturnsDefault()
        {
            Assert.IsTrue(Security.ParseBool(null, true, "X"));
            Assert.IsFalse(Security.ParseBool("  ", false, "X"));
        }

        [TestMethod]
        public void ParseBool_CaseAndWhitespaceTolerant_Parses()
        {
            Assert.IsTrue(Security.ParseBool(" TRUE ", false, "X"));
            Assert.IsFalse(Security.ParseBool("False", true, "X"));
        }
    }
}
