using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class PALReportTests
    {
        [DataTestMethod]
        [DataRow(@"NexusReports\PAL_C.rdl")]
        [DataRow(@"NexusReports\PAL_sub_C.rdl")]
        [DataRow(@"NexusReports_bak\PAL_C.rdl")]
        [DataRow(@"NexusReports_bak\PAL_sub_C.rdl")]
        [DataRow(@"sqlnexus\Reports\PAL_C.rdlC")]
        [DataRow(@"sqlnexus\Reports\PAL_sub_C.rdlC")]
        public void RetiredPALReport_IsAbsent(string relativePath)
        {
            string reportPath = Path.Combine(FindRepositoryRoot(), relativePath);

            bool exists = File.Exists(reportPath);

            Assert.IsFalse(exists, "The unsupported PAL report must not be retained: " + relativePath);
        }

        [DataTestMethod]
        [DataRow(@"sqlnexus\sqlnexus.csproj")]
        [DataRow(@"NexusReports\NexusReports.rptproj")]
        [DataRow(@"NexusReports\NexusReports.rptproj.bak")]
        [DataRow(@"NexusReports_bak\NexusReports.csproj")]
        public void ProjectManifest_ExcludesPALReports(string relativePath)
        {
            string projectPath = Path.Combine(FindRepositoryRoot(), relativePath);
            XDocument project = XDocument.Load(projectPath);

            string projectXml = project.ToString();

            Assert.AreEqual(-1, projectXml.IndexOf("PAL_C.rdl", StringComparison.OrdinalIgnoreCase),
                "The manifest must not reference the retired PAL parent report.");
            Assert.AreEqual(-1, projectXml.IndexOf("PAL_sub_C.rdl", StringComparison.OrdinalIgnoreCase),
                "The manifest must not reference the retired PAL detail report.");
        }

        [DataTestMethod]
        [DataRow(@"NexusReports\SQL Perf Main.rdl")]
        [DataRow(@"NexusReports_bak\SQL Perf Main.rdl")]
        [DataRow(@"sqlnexus\Reports\SQL Perf Main.rdl")]
        public void MainReport_RetainsPerfmonWithoutPAL(string relativePath)
        {
            string reportPath = Path.Combine(FindRepositoryRoot(), relativePath);
            XDocument report = XDocument.Load(reportPath);

            string[] queries = report.Descendants()
                .Where(element => element.Name.LocalName == "CommandText")
                .Select(element => element.Value)
                .ToArray();

            Assert.IsTrue(queries.Any(query => query.Contains("'Perfmon_C'")),
                "The supported Perfmon Summary report must remain registered.");
            Assert.IsFalse(queries.Any(query => query.Contains("'PAL_C'")),
                "The retired PAL report must not remain registered.");
        }

        [DataTestMethod]
        [DataRow("Perfmon_C")]
        [DataRow("Perfmon_CPU_C")]
        [DataRow("Perfmon_IO_C")]
        [DataRow("Perfmon_Memory_C")]
        [DataRow("Perfmon_Network_C")]
        public void SupportedPerfmonReport_RemainsIncluded(string reportName)
        {
            string repositoryRoot = FindRepositoryRoot();
            XDocument productProject = XDocument.Load(Path.Combine(repositoryRoot, "sqlnexus", "sqlnexus.csproj"));
            XDocument reportProject = XDocument.Load(Path.Combine(repositoryRoot, "NexusReports", "NexusReports.rptproj"));
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            bool copiedToOutput = productProject.Descendants(msbuild + "Content").Any(item =>
                (string)item.Attribute("Include") == @"Reports\" + reportName + ".rdlC"
                && (string)item.Element(msbuild + "CopyToOutputDirectory") == "Always");
            bool includedInReportProject = reportProject.Descendants(msbuild + "Report").Any(item =>
                (string)item.Attribute("Include") == reportName + ".rdl");

            Assert.IsTrue(copiedToOutput, "The supported Perfmon report must still be shipped.");
            Assert.IsTrue(includedInReportProject, "The supported Perfmon source report must remain included.");
            Assert.IsTrue(File.Exists(Path.Combine(repositoryRoot, "sqlnexus", "Reports", reportName + ".rdlC")));
            Assert.IsTrue(File.Exists(Path.Combine(repositoryRoot, "NexusReports", reportName + ".rdl")));
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
