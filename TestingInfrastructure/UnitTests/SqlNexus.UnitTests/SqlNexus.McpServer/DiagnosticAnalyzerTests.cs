using System;
using System.Data;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class DiagnosticAnalyzerTests
    {
        [TestMethod]
        public void ValidateReadOnlyCustomQuery_SelectStatement_DoesNotThrow()
        {
            global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                "SELECT TOP 100 wait_type, wait_time_ms FROM dbo.tbl_OS_WAIT_STATS ORDER BY wait_time_ms DESC;");
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_CteSelect_DoesNotThrow()
        {
            const string query = @"
WITH WaitTotals AS
(
    SELECT wait_type, SUM(wait_time_ms) AS total_wait_ms
    FROM dbo.tbl_OS_WAIT_STATS
    GROUP BY wait_type
)
SELECT wait_type, total_wait_ms FROM WaitTotals;";

            global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(query);
        }

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
        public void ValidateReadOnlyCustomQuery_KeywordInsideComment_DoesNotThrow()
        {
            global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                "/* WAITFOR DELAY and SELECT INTO are prohibited */ SELECT 1 AS value");
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_SelectInto_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * INTO dbo.CopiedWaits FROM dbo.tbl_OS_WAIT_STATS"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_SelectIntoTempTable_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * INTO #CopiedWaits FROM dbo.tbl_OS_WAIT_STATS"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_WaitForDelay_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery("WAITFOR DELAY '00:00:05'"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_IfWaitForDelay_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "IF 1 = 1 WAITFOR DELAY '00:00:05'"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_CrossDatabaseReference_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM OtherDatabase.dbo.CustomerData"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_LinkedServerReference_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM LinkedServer.OtherDatabase.dbo.CustomerData"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_OpenRowset_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM OPENROWSET(BULK 'C:\\customer-data.txt', SINGLE_CLOB) AS contents"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_ExternalFileTableFunction_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM sys.fn_get_audit_file('C:\\audit\\*.sqlaudit', DEFAULT, DEFAULT)"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_ExtendedEventFileTableFunction_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM sys.fn_xe_file_target_read_file('C:\\xevents\\*.xel', NULL, NULL, NULL)"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_CustomTableValuedFunction_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM dbo.ReadExternalDiagnostics()"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_CustomScalarFunction_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT dbo.ReadExternalSecret() AS value"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_BuiltInAggregateFunction_DoesNotThrow()
        {
            global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                "SELECT COUNT(*) AS wait_count, SUM(wait_time_ms) AS total_wait_ms FROM dbo.tbl_OS_WAIT_STATS");
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_TableHint_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM dbo.tbl_OS_WAIT_STATS WITH (TABLOCKX)"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_QueryHint_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT * FROM dbo.tbl_OS_WAIT_STATS OPTION (MAXDOP 0)"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_NextSequenceValue_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery(
                    "SELECT NEXT VALUE FOR dbo.DiagnosticSequence"));
        }

        [TestMethod]
        public void ValidateReadOnlyCustomQuery_MalformedSelect_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                global::SqlNexus.McpServer.DiagnosticAnalyzer.ValidateReadOnlyCustomQuery("SELECT FROM"));
        }

        [TestMethod]
        public void CustomQueryExecutionLimits_UseSecurityBounds()
        {
            Assert.AreEqual(60, global::SqlNexus.McpServer.DiagnosticAnalyzer.CustomQueryCommandTimeoutSeconds);
            Assert.AreEqual(1000, global::SqlNexus.McpServer.DiagnosticAnalyzer.CustomQueryMaximumRows);
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
        public void BuildErrorLogSummaryQuery_GuardsTableAndGroupsByErrorNumber()
        {
            string query = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildErrorLogSummaryQuery(25);

            StringAssert.Contains(query, "OBJECT_ID('tbl_ERRORLOG')");
            StringAssert.Contains(query, "TOP 25");
            StringAssert.Contains(query, "GROUP BY ErrorNumber");
            StringAssert.Contains(query, "ORDER BY Occurrences DESC");
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

        [TestMethod]
        public void BuildComparisonRows_DuplicateKeys_ReturnsAllMatchingPairs()
        {
            DataTable first = CreateComparisonTable();
            first.Rows.Add("DatabaseA", "100");
            first.Rows.Add("DatabaseA", "200");
            DataTable second = CreateComparisonTable();
            second.Rows.Add("DatabaseA", "110");
            second.Rows.Add("DatabaseA", "210");

            var result = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildComparisonRows(
                first,
                second,
                new[] { "name" },
                new[] { "value" },
                "first",
                "second",
                false);

            Assert.AreEqual(4, result.Count);
        }

        [TestMethod]
        public void BuildComparisonRows_NoMatchingKey_ReturnsEmptyList()
        {
            DataTable first = CreateComparisonTable();
            first.Rows.Add("DatabaseA", "100");
            DataTable second = CreateComparisonTable();
            second.Rows.Add("DatabaseB", "100");

            var result = global::SqlNexus.McpServer.DiagnosticAnalyzer.BuildComparisonRows(
                first,
                second,
                new[] { "name" },
                new[] { "value" },
                "first",
                "second",
                false);

            Assert.AreEqual(0, result.Count);
        }

        private static DataTable CreateComparisonTable()
        {
            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("value", typeof(string));
            return table;
        }
    }
}
