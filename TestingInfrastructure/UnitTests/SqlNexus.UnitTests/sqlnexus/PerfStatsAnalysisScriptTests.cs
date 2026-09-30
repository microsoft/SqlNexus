using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
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
