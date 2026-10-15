using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class WASDReportTests
    {
        [DataTestMethod]
        [DataRow("NexusReports")]
        [DataRow("NexusReports_bak")]
        [DataRow(@"sqlnexus\Reports")]
        public void RetiredWASDReports_AreAbsent(string relativeDirectory)
        {
            string reportDirectory = Path.Combine(FindRepositoryRoot(), relativeDirectory);

            string[] reports = Directory.GetFiles(reportDirectory, "WASD*.rdl*");

            Assert.AreEqual(0, reports.Length,
                "Unused WASD reports must not remain in " + relativeDirectory);
        }

        [DataTestMethod]
        [DataRow(@"sqlnexus\sqlnexus.csproj")]
        [DataRow(@"NexusReports\NexusReports.rptproj")]
        [DataRow(@"NexusReports\NexusReports.rptproj.bak")]
        public void ProjectManifest_ExcludesWASDReports(string relativePath)
        {
            string projectPath = Path.Combine(FindRepositoryRoot(), relativePath);
            XDocument project = XDocument.Load(projectPath);

            string projectXml = project.ToString();

            Assert.AreEqual(-1, projectXml.IndexOf("WASD", StringComparison.OrdinalIgnoreCase),
                "The manifest must not build, copy, or publish unused WASD reports.");
        }

        [DataTestMethod]
        [DataRow(@"NexusReports\SQL Perf Main.rdl")]
        [DataRow(@"NexusReports_bak\SQL Perf Main.rdl")]
        [DataRow(@"sqlnexus\Reports\SQL Perf Main.rdl")]
        public void MainReport_RetainsPerfmonWithoutWASD(string relativePath)
        {
            string reportPath = Path.Combine(FindRepositoryRoot(), relativePath);
            XDocument report = XDocument.Load(reportPath);

            string[] queries = report.Descendants()
                .Where(element => element.Name.LocalName == "CommandText")
                .Select(element => element.Value)
                .ToArray();

            Assert.IsTrue(queries.Any(query => query.Contains("'Perfmon_C'")),
                "The supported Perfmon Summary report must remain registered.");
            Assert.IsFalse(queries.Any(query => query.IndexOf("WASD", StringComparison.OrdinalIgnoreCase) >= 0),
                "Unused WASD reports, including the missing SLO report, must not remain registered.");
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
