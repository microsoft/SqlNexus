using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class PerfStatsAnalysisScriptTests
    {
        [TestMethod]
        public void PerfStatsAnalysis_DeployedScript_PassesIntegrityValidation()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerfStatsAnalysis.sql");
            FieldInfo hashesField = typeof(ScriptIntegrityChecker).GetField("ScriptHashes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(hashesField, "The script integrity allowlist was not found.");

            var scriptHashes = (Dictionary<string, string>)hashesField.GetValue(null);
            string expectedHash = null;
            foreach (KeyValuePair<string, string> scriptHash in scriptHashes)
            {
                if (string.Equals(Path.GetFileName(scriptHash.Key), "PerfStatsAnalysis.sql", StringComparison.OrdinalIgnoreCase))
                {
                    expectedHash = scriptHash.Value;
                    break;
                }
            }

            Assert.IsNotNull(expectedHash, "PerfStatsAnalysis.sql is not in the integrity allowlist.");

            string actualHash;
            using (FileStream stream = File.OpenRead(scriptPath))
            using (SHA256 sha256 = SHA256.Create())
            {
                actualHash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }

            Assert.AreEqual(expectedHash, actualHash, true, "The deployed PerfStatsAnalysis.sql hash must match the integrity allowlist.");
        }

        [TestMethod]
        public void WaitStatsTop5Categories_OtherCategory_ExcludesCpuWaits()
        {
            string script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "sqlnexus", "PerfStatsAnalysis.sql"));
            string procedure = GetSection(
                script,
                "CREATE PROC DataSet_WaitStats_WaitStatsTop5Categories",
                "IF OBJECT_ID('DataSet_WaitStats_WaitStatsTopCategoriesOther')");
            string otherCategoryQuery = GetSection(procedure, "-- Add in an \"other\" category", "ORDER BY wait_time_ms_per_sec DESC;");

            int topFiveOrderBy = otherCategoryQuery.IndexOf("ORDER BY wait_time_ms_per_sec DESC", StringComparison.Ordinal);
            Assert.IsTrue(topFiveOrderBy >= 0, "The Other category top-five subquery was not found.");

            int topFiveSubqueryEnd = otherCategoryQuery.IndexOf(')', topFiveOrderBy);
            Assert.IsTrue(topFiveSubqueryEnd > topFiveOrderBy, "The end of the Other category top-five subquery was not found.");

            int cpuExclusion = otherCategoryQuery.IndexOf(
                "AND cat.wait_category != 'SOS_SCHEDULER_YIELD'",
                topFiveSubqueryEnd,
                StringComparison.Ordinal);

            Assert.IsTrue(cpuExclusion > topFiveSubqueryEnd, "CPU waits must be excluded from the Other category outside the top-five subquery.");
        }

        [TestMethod]
        public void GetSnapshot_NullEmptyOrUnsafeTableName_RaisesErrorBeforeBuildingSql()
        {
            string procedure = ReadSnapshotProcedure();
            Match guard = Regex.Match(procedure,
                @"IF @tablename IS NULL\s+OR @tablename = N''\s+" +
                @"OR @tablename COLLATE Latin1_General_BIN2 LIKE N'%\[\^a-zA-Z0-9_\.\]%'");
            Match failure = Regex.Match(procedure,
                @"RAISERROR\('[^']+', 16, 1\);\s*RETURN 1;");

            Assert.IsTrue(guard.Success, "NULL, empty and non-allowlisted characters must be rejected.");
            Assert.IsTrue(failure.Success, "Invalid table names must raise an error and return failure.");
            Assert.IsTrue(guard.Index < failure.Index);
            Assert.IsTrue(failure.Index < procedure.IndexOf("QUOTENAME(", StringComparison.Ordinal),
                "Validation must occur before identifiers or SQL are constructed.");
        }

        [TestMethod]
        public void GetSnapshot_MalformedOrCrossDatabaseName_IsRejected()
        {
            string procedure = ReadSnapshotProcedure();

            StringAssert.Contains(procedure, "DECLARE @schema sysname = PARSENAME(@tablename, 2);");
            StringAssert.Contains(procedure, "DECLARE @table sysname = PARSENAME(@tablename, 1);");
            StringAssert.Contains(procedure, "OR @table IS NULL");
            StringAssert.Contains(procedure, "OR PARSENAME(@tablename, 3) IS NOT NULL");
            StringAssert.Contains(procedure, "OR PARSENAME(@tablename, 4) IS NOT NULL");
            StringAssert.Contains(procedure, "OR @tablename <> ISNULL(@schema + N'.', N'') + @table");
        }

        [TestMethod]
        public void GetSnapshot_LocalTableNames_QuotesEachIdentifierComponent()
        {
            string procedure = ReadSnapshotProcedure();

            StringAssert.Contains(procedure, "WHEN @schema IS NULL THEN QUOTENAME(@table)");
            StringAssert.Contains(procedure, "ELSE QUOTENAME(@schema) + N'.' + QUOTENAME(@table)");
            Assert.AreEqual(3, Regex.Matches(procedure, @"\+\s*@qualifiedTable\b").Count,
                "All three dynamic FROM clauses must use the quoted identifier.");
            Assert.IsFalse(Regex.IsMatch(procedure, @"\+\s*@tablename\b"),
                "Raw table-name input must never be concatenated into SQL.");
            Assert.IsFalse(Regex.IsMatch(procedure, @"EXEC\s*\(\s*@sql\s*\)"),
                "Use parameterized execution rather than EXEC(@sql).");
        }

        [TestMethod]
        public void GetSnapshot_MissingTable_ReturnsWithoutExecutingDynamicSql()
        {
            string procedure = ReadSnapshotProcedure();
            Match missingTable = Regex.Match(procedure,
                @"IF OBJECT_ID\(@qualifiedTable\) IS NULL\s*RETURN;");

            Assert.IsTrue(missingTable.Success, "Missing tables must retain the no-result behavior.");
            Assert.IsTrue(missingTable.Index < procedure.IndexOf("SET @sql =", StringComparison.Ordinal),
                "The existence check must be outside the dynamic SQL.");
            Assert.AreEqual(1, Regex.Matches(procedure, @"OBJECT_ID\s*\(").Count);
        }

        [TestMethod]
        public void GetSnapshot_RowNumber_IsPassedAsTypedParameter()
        {
            string procedure = ReadSnapshotProcedure();

            StringAssert.Contains(procedure, "where RowNumber = @rownumber");
            StringAssert.Contains(procedure,
                "EXEC sys.sp_executesql @sql, N'@rownumber INT', @rownumber = @rownumber;");
            Assert.IsFalse(Regex.IsMatch(procedure, @"CAST\s*\(\s*@rownumber"),
                "Row numbers must not be serialized into the SQL command.");
        }

        [TestMethod]
        public void GetSnapshot_QueryShape_PreservesLatestAndNumberedSnapshots()
        {
            string procedure = ReadSnapshotProcedure();

            StringAssert.Contains(procedure, "declare @runtime datetime");
            StringAssert.Contains(procedure, "IF @rownumber IS NULL");
            StringAssert.Contains(procedure, "select @runtime = MAX (runtime) from ");
            StringAssert.Contains(procedure, "ROW_NUMBER() OVER (ORDER BY runtime)");
            StringAssert.Contains(procedure, "select distinct runtime from ");
            StringAssert.Contains(procedure, "select  * from ");
            StringAssert.Contains(procedure, "where runtime = @runtime");
        }

        private static string ReadSnapshotProcedure()
        {
            string script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "sqlnexus", "PerfStatsAnalysis.sql"));
            return GetSection(script, "CREATE PROCEDURE DataSet_GetSnapshot", "\nGO");
        }

        private static string GetSection(string text, string startMarker, string endMarker)
        {
            int start = text.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Start marker was not found: " + startMarker);

            int end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, "End marker was not found: " + endMarker);
            return text.Substring(start, end - start);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "sqlnexus.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new DirectoryNotFoundException("Could not locate the SqlNexus repository root.");
            }

            return directory.FullName;
        }
    }
}
