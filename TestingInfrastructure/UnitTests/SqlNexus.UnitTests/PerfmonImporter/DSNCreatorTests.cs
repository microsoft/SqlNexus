using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfmonImporter;

namespace SqlNexus.UnitTests.PerfmonImporter
{
    /// <summary>
    /// Tests for <see cref="DSNCreator"/> DSN attribute-string construction. The actual
    /// SQLConfigDataSource P/Invoke cannot run in a unit test, so we validate the
    /// null-delimited attribute string produced by BuildDsnSettings instead.
    /// </summary>
    [TestClass]
    public class DSNCreatorTests
    {
        private static string[] SplitTokens(string dsn)
        {
            // BuildDsnSettings uses '\0' as the delimiter between keyword=value tokens.
            return dsn.Split('\0');
        }

        private static bool ContainsToken(string dsn, string token)
        {
            foreach (string t in SplitTokens(dsn))
            {
                if (t == token)
                {
                    return true;
                }
            }
            return false;
        }

        [TestMethod]
        public void BuildDsnSettings_WindowsAuth_IncludesTrustedConnection()
        {
            string dsn = DSNCreator.BuildDsnSettings("SQLNexusDSN", "myserver", "mydb", true, null, null, false, false);

            Assert.IsTrue(ContainsToken(dsn, "DSN=SQLNexusDSN"));
            Assert.IsTrue(ContainsToken(dsn, "Server=myserver"));
            Assert.IsTrue(ContainsToken(dsn, "Database=mydb"));
            Assert.IsTrue(ContainsToken(dsn, "Trusted_Connection=yes"));
        }

        [TestMethod]
        public void BuildDsnSettings_EncryptEnabled_AppendsEncryptYes()
        {
            string dsn = DSNCreator.BuildDsnSettings("SQLNexusDSN", "myserver", "mydb", true, null, null, true, false);

            Assert.IsTrue(ContainsToken(dsn, "Encrypt=yes"));
            Assert.IsTrue(ContainsToken(dsn, "TrustServerCertificate=no"));
        }

        [TestMethod]
        public void BuildDsnSettings_EncryptDisabled_AppendsEncryptNo()
        {
            string dsn = DSNCreator.BuildDsnSettings("SQLNexusDSN", "myserver", "mydb", true, null, null, false, false);

            Assert.IsTrue(ContainsToken(dsn, "Encrypt=no"));
            Assert.IsTrue(ContainsToken(dsn, "TrustServerCertificate=no"));
        }

        [TestMethod]
        public void BuildDsnSettings_TrustServerCertificateEnabled_AppendsTrustYes()
        {
            string dsn = DSNCreator.BuildDsnSettings("SQLNexusDSN", "myserver", "mydb", true, null, null, true, true);

            Assert.IsTrue(ContainsToken(dsn, "Encrypt=yes"));
            Assert.IsTrue(ContainsToken(dsn, "TrustServerCertificate=yes"));
        }

        [TestMethod]
        public void BuildDsnSettings_SqlAuth_IncludesCredentialsAndNoTrustedConnection()
        {
            string dsn = DSNCreator.BuildDsnSettings("SQLNexusDSN", "myserver", "mydb", false, "sa", "p@ss", true, false);

            Assert.IsTrue(dsn.Contains("UID=sa"));
            Assert.IsTrue(dsn.Contains("PWD=p@ss"));
            Assert.IsTrue(dsn.Contains("Trusted_Connection=no"));
            Assert.IsTrue(ContainsToken(dsn, "Encrypt=yes"));
        }

        [TestMethod]
        public void PreferredDrivers_ModernDriversTakePrecedenceOverLegacy()
        {
            string[] drivers = DSNCreator.PreferredDrivers;

            Assert.IsNotNull(drivers);
            Assert.IsTrue(drivers.Length >= 1);
            // Modern driver must be preferred; legacy "SQL Server" must be last (fallback only).
            Assert.AreEqual("ODBC Driver 18 for SQL Server", drivers[0]);
            Assert.AreEqual("SQL Server", drivers[drivers.Length - 1]);
        }
    }
}
