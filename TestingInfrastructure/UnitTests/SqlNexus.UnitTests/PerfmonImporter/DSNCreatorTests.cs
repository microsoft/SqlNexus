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

            // Assert exact tokens (not just substrings) so a malformed keyword such as ";UID=sa"
            // is caught. Each keyword=value pair must be its own '\0'-delimited token.
            Assert.IsTrue(ContainsToken(dsn, "UID=sa"));
            Assert.IsTrue(ContainsToken(dsn, "PWD=p@ss"));
            Assert.IsTrue(ContainsToken(dsn, "Trusted_Connection=no"));
            Assert.IsTrue(ContainsToken(dsn, "Encrypt=yes"));
        }

        [TestMethod]
        public void GetPreferredDrivers_ModernDriversTakePrecedenceOverLegacy()
        {
            // Simulate a machine with a mix of installed ODBC drivers (order intentionally shuffled).
            string[] installed = new string[]
            {
                "SQL Server",
                "ODBC Driver 17 for SQL Server",
                "Microsoft Access Driver (*.mdb, *.accdb)",
                "ODBC Driver 18 for SQL Server",
            };

            string[] drivers = DSNCreator.GetPreferredDrivers(installed);

            Assert.IsNotNull(drivers);
            // Newest modern SQL Server driver first, legacy "SQL Server" last, non-SQL drivers dropped.
            Assert.AreEqual(3, drivers.Length);
            Assert.AreEqual("ODBC Driver 18 for SQL Server", drivers[0]);
            Assert.AreEqual("ODBC Driver 17 for SQL Server", drivers[1]);
            Assert.AreEqual("SQL Server", drivers[drivers.Length - 1]);
        }

        [TestMethod]
        public void GetPreferredDrivers_FutureDriverVersion_IsPreferredAutomatically()
        {
            // A driver version newer than any hardcoded value must be picked first with no code change.
            string[] installed = new string[]
            {
                "ODBC Driver 18 for SQL Server",
                "ODBC Driver 99 for SQL Server",
                "SQL Server",
            };

            string[] drivers = DSNCreator.GetPreferredDrivers(installed);

            Assert.AreEqual("ODBC Driver 99 for SQL Server", drivers[0]);
            Assert.AreEqual("ODBC Driver 18 for SQL Server", drivers[1]);
            Assert.AreEqual("SQL Server", drivers[2]);
        }

        [TestMethod]
        public void GetPreferredDrivers_NoSqlServerDrivers_ReturnsEmpty()
        {
            string[] installed = new string[]
            {
                "Microsoft Access Driver (*.mdb, *.accdb)",
                "Microsoft Excel Driver (*.xls)",
            };

            string[] drivers = DSNCreator.GetPreferredDrivers(installed);

            Assert.IsNotNull(drivers);
            Assert.AreEqual(0, drivers.Length);
        }

        [TestMethod]
        public void GetPreferredDrivers_NullInput_ReturnsEmpty()
        {
            string[] drivers = DSNCreator.GetPreferredDrivers(null);

            Assert.IsNotNull(drivers);
            Assert.AreEqual(0, drivers.Length);
        }

        [TestMethod]
        public void GetPreferredDrivers_OnlyLegacyDriver_ReturnsLegacyOnly()
        {
            string[] drivers = DSNCreator.GetPreferredDrivers(new string[] { "SQL Server" });

            Assert.AreEqual(1, drivers.Length);
            Assert.AreEqual("SQL Server", drivers[0]);
        }
    }
}
