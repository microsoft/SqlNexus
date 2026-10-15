using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class McpServerSqlIntegrationTests
    {
        private const int ValidationTimeoutMilliseconds = 30 * 60 * 1000;

        [TestMethod]
        [TestCategory("SqlIntegration")]
        public void RegisteredMcpServer_AllAdvertisedToolsExecuteWithoutErrors()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("SQLNEXUS_RUN_MCP_INTEGRATION_TESTS"),
                "1",
                StringComparison.Ordinal))
            {
                Assert.Inconclusive(
                    "Set SQLNEXUS_RUN_MCP_INTEGRATION_TESTS=1 to validate all tools against the registered SQL Nexus database.");
            }

            string repositoryRoot = FindRepositoryRoot();
            string script = Path.Combine(repositoryRoot, "SqlNexus.McpServer", "Test-McpServer.ps1");
            string arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script)
                + " -UseRegisteredConfiguration -All -NonInteractive";
            string configurationPath = Environment.GetEnvironmentVariable("SQLNEXUS_MCP_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(configurationPath))
            {
                arguments += " -ConfigurationPath " + Quote(configurationPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ValidationTimeoutMilliseconds))
                {
                    process.Kill();
                    Assert.Fail("MCP SQL integration validation exceeded the 30-minute timeout.");
                }

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();
                Assert.AreEqual(0, process.ExitCode, output + Environment.NewLine + error);
                StringAssert.Contains(output, "failed: 0");
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "sqlnexus.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SQL Nexus repository root.");
        }
    }
}
