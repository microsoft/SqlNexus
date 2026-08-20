using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class DatabaseCommandTextTests
    {
        [TestMethod]
        public void GetCreateDatabaseCommandText_UsesDbNameParameterAndQuotename()
        {
            string sql = Program.GetCreateDatabaseCommandText();

            StringAssert.Contains(sql, "@DbName");
            StringAssert.Contains(sql, "QUOTENAME(@db)");
            Assert.IsFalse(sql.Contains("{0}"));
        }

        [TestMethod]
        public void GetCreateDropDatabaseCommandText_UsesDbNameParameterAndQuotename()
        {
            string sql = Program.GetCreateDropDatabaseCommandText();

            StringAssert.Contains(sql, "@DbName");
            StringAssert.Contains(sql, "QUOTENAME(@db)");
            Assert.IsFalse(sql.Contains("{0}"));
        }

        [DataTestMethod]
        [DataRow("SqlNexus")]
        [DataRow("SqlNexus_01")]
        [DataRow("A")]
        public void IsDbNameValid_AllowedNames_ReturnsTrue(string dbName)
        {
            bool result = Program.IsDbNameValid(dbName);

            Assert.IsTrue(result);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("master")]
        [DataRow("tempdb")]
        [DataRow("msdb")]
        [DataRow("model")]
        [DataRow("bad-name")]
        [DataRow("bad'name")]
        [DataRow("bad]name")]
        public void IsDbNameValid_DisallowedNames_ReturnsFalse(string dbName)
        {
            bool result = Program.IsDbNameValid(dbName);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsDbNameValid_LengthBoundary_Enforced()
        {
            string max = new string('A', 128);
            string tooLong = new string('A', 129);

            bool maxResult = Program.IsDbNameValid(max);
            bool tooLongResult = Program.IsDbNameValid(tooLong);

            Assert.IsTrue(maxResult);
            Assert.IsFalse(tooLongResult);
        }
    }
}
