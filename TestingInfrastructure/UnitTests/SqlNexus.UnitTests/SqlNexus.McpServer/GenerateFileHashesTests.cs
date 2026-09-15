using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class GenerateFileHashesTests
    {
        [TestMethod]
        public void GenerateFileHashes_RestrictedModulePath_ProducesExpectedHashes()
        {
            string sourceScript = FindSourceScript();
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "SqlNexusHashTest_" + Guid.NewGuid().ToString("N"));
            string scriptDirectory = Path.Combine(fixtureRoot, "SqlNexus.McpServer");
            string skillsDirectory = Path.Combine(fixtureRoot, "AI", "Skills");
            string agentsDirectory = Path.Combine(fixtureRoot, ".github", "agents");
            string emptyModulesDirectory = Path.Combine(fixtureRoot, "EmptyModules");

            try
            {
                Directory.CreateDirectory(scriptDirectory);
                Directory.CreateDirectory(skillsDirectory);
                Directory.CreateDirectory(agentsDirectory);
                Directory.CreateDirectory(emptyModulesDirectory);

                string scriptPath = Path.Combine(scriptDirectory, "GenerateFileHashes.ps1");
                string agentPath = Path.Combine(agentsDirectory, "sql-nexus-diagnostic.agent.md");
                string skillPath = Path.Combine(skillsDirectory, "fixture-skill.md");
                File.Copy(sourceScript, scriptPath);
                File.WriteAllText(agentPath, "agent fixture");
                File.WriteAllText(skillPath, "skill fixture");

                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["PSModulePath"] = emptyModulesDirectory;

                string output;
                string error;
                int exitCode;
                using (Process process = Process.Start(startInfo))
                {
                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                Assert.AreEqual(0, exitCode, error);
                Assert.IsFalse(output.Contains("skipped due to error"), output);
                StringAssert.Contains(output, GetSha256(agentPath));
                StringAssert.Contains(output, GetSha256(skillPath));
            }
            finally
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, true);
                }
            }
        }

        private static string FindSourceScript()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "SqlNexus.McpServer", "GenerateFileHashes.ps1");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not locate SqlNexus.McpServer\\GenerateFileHashes.ps1.");
        }

        private static string GetSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}