using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class ReadTraceReportTests
    {
        [TestMethod]
        public void MainReport_ChartTitle_DescribesIntervalResourceUsage()
        {
            string repositoryRoot = FindRepositoryRoot();
            string sourceReport = File.ReadAllText(Path.Combine(repositoryRoot, "NexusReports", "ReadTrace_Main_C.rdl"));
            string deployedReport = File.ReadAllText(Path.Combine(repositoryRoot, "sqlnexus", "Reports", "ReadTrace_Main_C.rdlC"));

            const string expectedCaption = "<Caption>Resource Usage by Time Interval</Caption>";
            const string oldCaption = "<Caption>Cumulative Resource Usage</Caption>";
            StringAssert.Contains(sourceReport, expectedCaption);
            StringAssert.Contains(deployedReport, expectedCaption);
            Assert.IsFalse(sourceReport.Contains(oldCaption));
            Assert.IsFalse(deployedReport.Contains(oldCaption));
        }

        [TestMethod]
        public void PostProcessing_BatchIntervalProcedure_ReturnsEmptyIntervalsAsZeros()
        {
            string repositoryRoot = FindRepositoryRoot();
            string script = File.ReadAllText(Path.Combine(repositoryRoot, "sqlnexus", "ReadTracePostProcessing.sql"));
            int procedureStart = script.IndexOf(
                "CREATE PROCEDURE ReadTrace.spReporter_BatchAggregatesTimeIntervalGrouping",
                StringComparison.Ordinal);
            Assert.IsTrue(procedureStart >= 0, "The batch interval procedure was not found.");
            int procedureEnd = script.IndexOf("\nGO", procedureStart, StringComparison.Ordinal);
            Assert.IsTrue(procedureEnd > procedureStart, "The end of the batch interval procedure was not found.");
            string procedure = script.Substring(procedureStart, procedureEnd - procedureStart);
            StringAssert.Contains(procedure, "FROM ReadTrace.tblTimeIntervals t");
            StringAssert.Contains(procedure, "LEFT JOIN ReadTrace.tblBatchPartialAggs a");
            StringAssert.Contains(procedure, "ISNULL(SUM(a.CompletedEvents), 0) AS CompletedEvents");
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
