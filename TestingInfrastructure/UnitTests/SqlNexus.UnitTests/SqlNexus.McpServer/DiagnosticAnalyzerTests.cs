using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class DiagnosticAnalyzerTests
    {
        [TestMethod]
        public void ValidateReadOnlyCustomQuery_ExecStatement_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery("SELECT 1; EXEC sp_configure 'show advanced options', 1"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_IfWrapperWithExec_ThrowsInvalidOperationException()
        {
            const string query = @"
IF 1 = 1
BEGIN
    EXEC xp_cmdshell 'whoami';
END";

            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(query));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_MultiStatementBatch_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery("SELECT 1; SELECT 2"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_KeywordInsideLiteral_DoesNotThrow()
        {
            global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery("SELECT 'DROP TABLE dbo.X' AS message");
        }

        [TestMethod]
        public void BuildAnalyzeIoPerformanceQuery_CommaCulture_UsesInvariantDecimalLiteral()
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");

                string query = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildAnalyzeIoPerformanceQuery(20.5m);

                StringAssert.Contains(query, "DECLARE @IO_threshold DECIMAL(12, 3) = 20.5;");
                Assert.IsFalse(query.Contains("20,5"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void InstalledProgramsNameFilter_UsesContainsPattern()
        {
            Assert.AreEqual("%sql%", global::SqlNexus.McpServer.DiagnosticAnalyzer.InstalledProgramsNameFilter);
        }

        [TestMethod]
        public void BuildQueriesByApplicationQuery_Filtered_UsesSqlParameterPlaceholder()
        {
            string query = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildQueriesByApplicationQuery(true);

            StringAssert.Contains(query, "WHERE c.ApplicationName = @app_name");
            Assert.IsFalse(query.Contains("ApplicationName = '"));
        }

        [TestMethod]
        public void BuildTableStatisticsHealthQuery_Filtered_UsesSqlParameterPlaceholder()
        {
            string query = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildTableStatisticsHealthQuery(true);

            StringAssert.Contains(query, "Database_Name = @db_name");
            Assert.IsFalse(query.Contains("Database_Name = '"));
        }
    }
}
