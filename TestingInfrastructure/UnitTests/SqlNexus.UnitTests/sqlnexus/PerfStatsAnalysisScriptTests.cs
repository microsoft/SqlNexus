using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class PerfStatsAnalysisScriptTests
    {
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
