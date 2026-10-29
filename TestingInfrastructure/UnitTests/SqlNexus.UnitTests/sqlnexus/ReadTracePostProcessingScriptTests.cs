using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class ReadTracePostProcessingScriptTests
    {
        [TestMethod]
        public void OverviewCounts_ColumnAllowlist_ContainsOnlyComparisonMetrics()
        {
            string procedure = ReadOverviewCountsProcedure();
            Match guard = Regex.Match(procedure, @"IF @strColumn IS NULL OR @strColumn NOT IN\s*\((?<columns>[^)]+)\)");
            Assert.IsTrue(guard.Success, "NULL and unsupported columns must be rejected explicitly.");
            MatchCollection columns = Regex.Matches(guard.Groups["columns"].Value, @"N'(?<name>[^']+)'");
            string[] actual = new string[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                actual[i] = columns[i].Groups["name"].Value;
            }

            CollectionAssert.AreEquivalent(
                new[] { "StartingEvents", "CompletedEvents", "TotalCPU", "TotalDuration", "TotalReads", "TotalWrites" },
                actual);
        }

        [TestMethod]
        public void OverviewCounts_InvalidColumnGuard_RaisesErrorAndReturnsBeforeBuildingSql()
        {
            string procedure = ReadOverviewCountsProcedure();
            Match guard = Regex.Match(procedure,
                @"IF @strColumn IS NULL OR @strColumn NOT IN\s*\([^)]+\)\s*BEGIN\s*" +
                @"RAISERROR\('[^']+', 16, 1\);\s*RETURN 1;\s*END");

            Assert.IsTrue(guard.Success, "Invalid input must raise an error and return a failure status.");
            Assert.IsTrue(guard.Index < procedure.IndexOf("SET @strCmd =", StringComparison.Ordinal),
                "Validation must occur before dynamic SQL is constructed.");
        }

        [TestMethod]
        public void OverviewCounts_DynamicIdentifiers_QuotesCompleteBaselineAndComparisonNames()
        {
            string procedure = ReadOverviewCountsProcedure();

            StringAssert.Contains(procedure,
                "REPLACE(@strCmd, N'[b.@strColumn]', QUOTENAME(N'b.' + @strColumn))");
            StringAssert.Contains(procedure,
                "REPLACE(@strCmd, N'[c.@strColumn]', QUOTENAME(N'c.' + @strColumn))");
            Assert.IsFalse(procedure.Contains("REPLACE(@strCmd, '@strColumn', @strColumn)"),
                "Raw input must not be substituted into the SQL command.");
            Assert.AreEqual(2, Regex.Matches(procedure, @"SET @strCmd = REPLACE").Count);
        }

        [TestMethod]
        public void OverviewCounts_QueryShape_PreservesAllFourAggregateBranches()
        {
            string procedure = ReadOverviewCountsProcedure();

            Assert.AreEqual(3, Regex.Matches(procedure, "UNION ALL").Count);
            Assert.AreEqual(2, Regex.Matches(procedure, @"SUM\(\[b\.@strColumn\]\)").Count);
            Assert.AreEqual(2, Regex.Matches(procedure, @"SUM\(\[c\.@strColumn\]\)").Count);
            StringAssert.Contains(procedure, "''B'' as [Type]");
            StringAssert.Contains(procedure, "as [Value]");
            StringAssert.Contains(procedure, "''C''");
            StringAssert.Contains(procedure, "''BO''");
            StringAssert.Contains(procedure, "''CO''");
            Assert.AreEqual(2, Regex.Matches(procedure,
                @"WHERE \[b\.HashID\] IS NOT NULL and \[c\.HashID\] IS NOT NULL").Count);
            StringAssert.Contains(procedure, "WHERE [c.HashID] IS NULL");
            StringAssert.Contains(procedure, "WHERE [b.HashID] IS NULL");
            StringAssert.Contains(procedure, "EXEC sp_executesql @strCmd");
        }

        private static string ReadOverviewCountsProcedure()
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

            string script = File.ReadAllText(Path.Combine(directory.FullName, "sqlnexus", "ReadTracePostProcessing.sql"));
            const string procedureName = "CREATE PROCEDURE ReadTrace.spReporter_Overview_Counts";
            int start = script.IndexOf(procedureName, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "The overview counts procedure was not found.");
            int end = script.IndexOf("\nGO", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, "The end of the overview counts procedure was not found.");
            return script.Substring(start, end - start);
        }
    }
}
