using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class SqlSecurityContextTests
    {
        [TestMethod]
        public void ParsePolicy_EmptyValue_ReturnsBlock()
        {
            var result = global::SqlNexus.McpServer.SqlSecurityContext.ParsePolicy(null);

            Assert.AreEqual(global::SqlNexus.McpServer.ElevatedPrincipalPolicy.Block, result);
        }

        [TestMethod]
        public void ParsePolicy_ImpersonateReader_ReturnsImpersonateReader()
        {
            var result = global::SqlNexus.McpServer.SqlSecurityContext.ParsePolicy("impersonate-reader");

            Assert.AreEqual(global::SqlNexus.McpServer.ElevatedPrincipalPolicy.ImpersonateReader, result);
        }

        [TestMethod]
        public void ParsePolicy_UnknownValue_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                global::SqlNexus.McpServer.SqlSecurityContext.ParsePolicy("allow-everything"));
        }

        [TestMethod]
        public void ValidateSnapshot_ElevatedPrincipalWithBlockPolicy_ReturnsError()
        {
            var snapshot = CreateSecureSnapshot();
            snapshot.IsElevated = true;

            string result = global::SqlNexus.McpServer.SqlSecurityContext.ValidateSnapshot(
                snapshot,
                global::SqlNexus.McpServer.ElevatedPrincipalPolicy.Block);

            StringAssert.Contains(result, "principal is elevated");
        }

        [TestMethod]
        public void ValidateSnapshot_DataReaderWithBlockPolicy_ReturnsSuccess()
        {
            var snapshot = CreateSecureSnapshot();

            string result = global::SqlNexus.McpServer.SqlSecurityContext.ValidateSnapshot(
                snapshot,
                global::SqlNexus.McpServer.ElevatedPrincipalPolicy.Block);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ValidateSnapshot_ElevatedPrincipalWithValidReaderAndImpersonation_ReturnsSuccess()
        {
            var snapshot = CreateSecureSnapshot();
            snapshot.IsElevated = true;

            string result = global::SqlNexus.McpServer.SqlSecurityContext.ValidateSnapshot(
                snapshot,
                global::SqlNexus.McpServer.ElevatedPrincipalPolicy.ImpersonateReader);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ValidateSnapshot_WritableDatabase_ReturnsError()
        {
            var snapshot = CreateSecureSnapshot();
            snapshot.IsDatabaseReadOnly = false;

            string result = global::SqlNexus.McpServer.SqlSecurityContext.ValidateSnapshot(
                snapshot,
                global::SqlNexus.McpServer.ElevatedPrincipalPolicy.ImpersonateReader);

            StringAssert.Contains(result, "not read-only");
        }

        [TestMethod]
        public void ValidateSnapshot_ElevatedReaderUser_ReturnsError()
        {
            var snapshot = CreateSecureSnapshot();
            snapshot.ReaderUserIsElevated = true;

            string result = global::SqlNexus.McpServer.SqlSecurityContext.ValidateSnapshot(
                snapshot,
                global::SqlNexus.McpServer.ElevatedPrincipalPolicy.ImpersonateReader);

            StringAssert.Contains(result, "not configured as a dedicated db_datareader");
        }

        [TestMethod]
        public void GetPrivilegeQuery_ChecksServerDatabaseAndReaderPrivileges()
        {
            string query = global::SqlNexus.McpServer.SqlSecurityContext.GetPrivilegeQuery();

            StringAssert.Contains(query, "IS_SRVROLEMEMBER(N'sysadmin')");
            StringAssert.Contains(query, "IS_ROLEMEMBER(N'db_owner')");
            StringAssert.Contains(query, "HAS_PERMS_BY_NAME");
            StringAssert.Contains(query, "SqlNexusMcpReader");
            StringAssert.Contains(query, "Updateability");
        }

        private static global::SqlNexus.McpServer.SqlPrivilegeSnapshot CreateSecureSnapshot()
        {
            return new global::SqlNexus.McpServer.SqlPrivilegeSnapshot
            {
                HasSelect = true,
                IsDatabaseReadOnly = true,
                ReaderUserExists = true,
                ReaderUserIsDataReader = true
            };
        }
    }
}
