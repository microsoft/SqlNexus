using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class ReadTracePostProcessingScriptTests
    {
        [TestMethod]
        public void OverviewCounts_MetricAllowlist_ContainsOnlySupportedColumns()
        {
            string procedure = ReadOverviewCountsProcedure();
            Match guard = GetValidationGuard(procedure);
            string[] expectedMetrics =
            {
                "StartingEvents", "CompletedEvents", "TotalCPU",
                "TotalDuration", "TotalReads", "TotalWrites"
            };

            string[] metrics = GetAllowedMetrics(guard);

            CollectionAssert.AreEquivalent(expectedMetrics, metrics);
        }

        [TestMethod]
        public void OverviewCounts_InvalidMetricGuard_RejectsNullAndReturnsBeforeDynamicSql()
        {
            string procedure = ReadOverviewCountsProcedure();

            Match guard = GetValidationGuard(procedure);
            int commandStart = procedure.IndexOf("SET @strCmd", StringComparison.Ordinal);

            Assert.AreEqual(
                "RAISERROR('Unsupported comparison metric.', 16, 1); RETURN;",
                NormalizeWhitespace(guard.Groups["body"].Value));
            Assert.IsTrue(commandStart > guard.Index + guard.Length,
                "Invalid input must raise an error and return before constructing dynamic SQL.");
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("UnknownMetric")]
        [DataRow("HashID")]
        [DataRow("TotalReads]")]
        [DataRow("TotalReads]) + 1 + SUM([b.TotalReads")]
        [DataRow("TotalReads]); SELECT 1;--")]
        [DataRow("TotalReads/*comment*/")]
        [DataRow("TotalReads'")]
        public void OverviewCounts_InvalidMetricExamples_AreExcludedFromAllowlist(string metric)
        {
            Match guard = GetValidationGuard(ReadOverviewCountsProcedure());

            string[] metrics = GetAllowedMetrics(guard);

            Assert.IsFalse(metrics.Contains(metric, StringComparer.OrdinalIgnoreCase));
        }

        [DataTestMethod]
        [DataRow(127)]
        [DataRow(128)]
        [DataRow(129)]
        public void OverviewCounts_MetricLengthBoundaries_AreExcludedFromAllowlist(int length)
        {
            Match guard = GetValidationGuard(ReadOverviewCountsProcedure());
            string metric = new string('X', length);

            string[] metrics = GetAllowedMetrics(guard);

            Assert.IsFalse(metrics.Contains(metric, StringComparer.OrdinalIgnoreCase));
        }

        [DataTestMethod]
        [DataRow("StartingEvents")]
        [DataRow("CompletedEvents")]
        [DataRow("TotalCPU")]
        [DataRow("TotalDuration")]
        [DataRow("TotalReads")]
        [DataRow("TotalWrites")]
        public void OverviewCounts_SupportedMetric_PreservesAllFourAggregateQueries(string metric)
        {
            string procedure = ReadOverviewCountsProcedure();
            Match template = Regex.Match(procedure, @"SET @strCmd\s*=\s*'(?<query>(?:''|[^'])*)'");
            Assert.IsTrue(template.Success, "The overview query template was not found.");
            StringAssert.Contains(procedure,
                "SET @strCmd = REPLACE(@strCmd, '[b.@strColumn]', QUOTENAME(N'b.' + @strColumn));");
            StringAssert.Contains(procedure,
                "SET @strCmd = REPLACE(@strCmd, '[c.@strColumn]', QUOTENAME(N'c.' + @strColumn));");
            Assert.AreEqual(2, Regex.Matches(procedure, @"SET @strCmd = REPLACE").Count);
            StringAssert.Matches(procedure, new Regex(@"EXEC sp_executesql @strCmd\s+END\s+GO\s*$"));

            // Source-contract check: SQL execution requires a separate SQL Server integration test.
            string query = template.Groups["query"].Value.Replace("''", "'")
                .Replace("[b.@strColumn]", "[b." + metric + "]")
                .Replace("[c.@strColumn]", "[c." + metric + "]");
            string expectedQuery = string.Format(
                @"SELECT 'B' as [Type],SUM([b.{0}]) as [Value]
                  FROM ReadTrace.tblComparisonBatchPartialAggs
                  WHERE [b.HashID] IS NOT NULL and [c.HashID] IS NOT NULL
                  UNION ALL
                  SELECT 'C',SUM([c.{0}])
                  FROM ReadTrace.tblComparisonBatchPartialAggs
                  WHERE [b.HashID] IS NOT NULL and [c.HashID] IS NOT NULL
                  UNION ALL
                  SELECT 'BO',SUM([b.{0}])
                  FROM ReadTrace.tblComparisonBatchPartialAggs
                  WHERE [c.HashID] IS NULL
                  UNION ALL
                  SELECT 'CO',SUM([c.{0}])
                  FROM ReadTrace.tblComparisonBatchPartialAggs
                  WHERE [b.HashID] IS NULL", metric);

            Assert.AreEqual(NormalizeWhitespace(expectedQuery), NormalizeWhitespace(query));
        }

        [TestMethod]
        public void ReadTracePostProcessing_DeployedScript_MatchesIntegrityAllowlist()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReadTracePostProcessing.sql");
            FieldInfo hashesField = typeof(ScriptIntegrityChecker).GetField("ScriptHashes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(hashesField, "The script integrity allowlist was not found.");
            var hashes = (Dictionary<string, string>)hashesField.GetValue(null);
            string expectedHash = hashes.Single(entry =>
                string.Equals(Path.GetFileName(entry.Key), "ReadTracePostProcessing.sql", StringComparison.OrdinalIgnoreCase)).Value;

            string actualHash;
            using (FileStream stream = File.OpenRead(scriptPath))
            using (SHA256 sha256 = SHA256.Create())
            {
                actualHash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }

            Assert.AreEqual(expectedHash, actualHash, true,
                "The deployed ReadTracePostProcessing.sql must match its integrity allowlist entry.");
        }

        private static string ReadOverviewCountsProcedure()
        {
            string script = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReadTracePostProcessing.sql"));
            Match procedure = Regex.Match(script,
                @"CREATE PROCEDURE ReadTrace\.spReporter_Overview_Counts\b.*?^\s*GO\s*$",
                RegexOptions.Singleline | RegexOptions.Multiline);
            Assert.IsTrue(procedure.Success, "The overview counts procedure was not found.");
            return procedure.Value;
        }

        private static Match GetValidationGuard(string procedure)
        {
            Match guard = Regex.Match(procedure,
                @"IF\s+@strColumn\s+IS\s+NULL\s+OR\s+@strColumn\s+NOT\s+IN\s*\((?<metrics>[^)]+)\)\s*BEGIN(?<body>.*?)END",
                RegexOptions.Singleline);
            Assert.IsTrue(guard.Success, "A null-safe metric allowlist guard is required.");
            return guard;
        }

        private static string[] GetAllowedMetrics(Match guard)
        {
            return Regex.Matches(guard.Groups["metrics"].Value, @"N'(?<metric>[^']+)'")
                .Cast<Match>()
                .Select(match => match.Groups["metric"].Value)
                .ToArray();
        }

        private static string NormalizeWhitespace(string text)
        {
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
