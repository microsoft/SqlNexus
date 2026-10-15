using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class TestMcpServerScriptTests
    {
        [TestMethod]
        public void RegisteredConfiguration_MalformedJson_ReturnsFailure()
        {
            string configurationPath = CreateTemporaryConfiguration("{ not valid json");
            try
            {
                ProcessResult result = RunValidation(configurationPath);

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "does not contain valid JSON");
            }
            finally
            {
                File.Delete(configurationPath);
            }
        }

        [TestMethod]
        public void RegisteredConfiguration_MissingSqlNexusEntry_ReturnsFailure()
        {
            string configurationPath = CreateTemporaryConfiguration("{\"servers\":{\"other\":{\"command\":\"other.exe\"}}}");
            try
            {
                ProcessResult result = RunValidation(configurationPath);

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "does not contain a 'sqlnexus_mcp' server entry");
            }
            finally
            {
                File.Delete(configurationPath);
            }
        }

        [TestMethod]
        public void RegisteredConfiguration_UnexpectedCommand_ReturnsFailure()
        {
            string configurationPath = CreateTemporaryConfiguration(
                "{\"servers\":{\"sqlnexus_mcp\":{\"type\":\"stdio\",\"command\":\"other.exe\",\"args\":[\"--server\",\"localhost\",\"--database\",\"SqlNexus\"]}}}");
            try
            {
                ProcessResult result = RunValidation(configurationPath);

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "must point to SqlNexus.McpServer.exe");
            }
            finally
            {
                File.Delete(configurationPath);
            }
        }

        [TestMethod]
        public void DirectMode_ExecutableNotFound_ReturnsFailure()
        {
            ProcessResult result = RunValidationWithArguments(
                "-Server localhost -Database SqlNexus -All -NonInteractive -ExecutablePath "
                    + Quote(Path.Combine(Path.GetTempPath(), "SqlNexus.McpServer.exe_" + Guid.NewGuid().ToString("N"))));

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.Output, "The MCP server executable was not found");
        }

        [TestMethod]
        public void DirectMode_ExecutableNotFoundWithSkipIfNotConfigured_ReturnsSuccess()
        {
            ProcessResult result = RunValidationWithArguments(
                "-Server localhost -Database SqlNexus -All -NonInteractive -SkipIfNotConfigured -ExecutablePath "
                    + Quote(Path.Combine(Path.GetTempPath(), "SqlNexus.McpServer.exe_" + Guid.NewGuid().ToString("N"))));

            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            StringAssert.Contains(result.Output, "skipping MCP tool validation");
        }

        [TestMethod]
        public void RegisteredConfiguration_MissingAndSkipIfNotConfigured_ReturnsSuccess()
        {
            string missingConfigurationPath = Path.Combine(Path.GetTempPath(), "SqlNexusMissingConfig_" + Guid.NewGuid().ToString("N") + ".json");

            ProcessResult result = RunValidationWithArguments(
                "-UseRegisteredConfiguration -ConfigurationPath " + Quote(missingConfigurationPath)
                    + " -All -NonInteractive -SkipIfNotConfigured");

            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            StringAssert.Contains(result.Output, "skipping MCP tool validation");
        }

        [TestMethod]
        public void AutomatedValidation_WithoutAll_ReturnsFailure()
        {
            ProcessResult result = RunValidationWithArguments("-Server localhost -Database SqlNexus -NonInteractive");

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.Output, "currently require -All");
        }

        [TestMethod]
        public void DirectMode_StagedLayoutExecutableNextToScript_IsDiscoveredWithoutExecutablePath()
        {
            // Regression test for the staged/release layout produced by sqlnexus.csproj's
            // StageSqlNexusAiFiles target: SqlNexus.McpServer.exe is flattened directly next to
            // Test-McpServer.ps1, with no nested bin\Release subfolder. The default executable
            // resolution must find it there (not only in the dev bin\Release layout).
            string stagingDirectory = Path.Combine(Path.GetTempPath(), "SqlNexusStagedLayoutTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                string script = Path.Combine(FindRepositoryRoot(), "SqlNexus.McpServer", "Test-McpServer.ps1");
                string copiedScript = Path.Combine(stagingDirectory, "Test-McpServer.ps1");
                File.Copy(script, copiedScript);

                // A fake (non-functional) executable is enough: -SkipIfNotConfigured is intentionally
                // NOT used here, so we only need to confirm the resolver locates the file and attempts
                // to launch it (proving discovery succeeded) rather than reporting "not found".
                string fakeExecutable = Path.Combine(stagingDirectory, "SqlNexus.McpServer.exe");
                File.WriteAllText(fakeExecutable, string.Empty);

                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(copiedScript)
                        + " -Server localhost -Database SqlNexus -All -NonInteractive",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                string output;
                string error;
                using (Process process = Process.Start(startInfo))
                {
                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                }

                // The fake executable is not a real MCP server, so validation itself is expected to
                // fail once launched. The regression being guarded against is the executable being
                // reported as "not found" when it is actually present in the staged layout.
                Assert.IsFalse(
                    output.Contains("The MCP server executable was not found"),
                    "Staged-layout executable (next to the script) was not discovered: " + output + error);
            }
            finally
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        private static string CreateTemporaryConfiguration(string content)
        {
            string path = Path.Combine(Path.GetTempPath(), "SqlNexusMcpTest_" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, content);
            return path;
        }

        private static ProcessResult RunValidation(string configurationPath)
        {
            return RunValidationWithArguments(
                "-UseRegisteredConfiguration -ConfigurationPath " + Quote(configurationPath) + " -All -NonInteractive");
        }

        private static ProcessResult RunValidationWithArguments(string scriptArguments)
        {
            string script = Path.Combine(FindRepositoryRoot(), "SqlNexus.McpServer", "Test-McpServer.ps1");
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script) + " " + scriptArguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, output, error);
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

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
        }
    }
}
