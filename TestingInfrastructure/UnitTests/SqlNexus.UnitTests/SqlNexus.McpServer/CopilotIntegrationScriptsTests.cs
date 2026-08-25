using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class CopilotIntegrationScriptsTests
    {
        [TestMethod]
        public void RegisterAndUnregister_ExistingConfigurations_PreservesUnrelatedEntries()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                File.WriteAllText(fixture.VsCodeConfigPath, "{\"servers\":{\"other\":{\"command\":\"other.exe\"}},\"inputs\":[]}");
                File.WriteAllText(fixture.CopilotConfigPath, "{\"mcpServers\":{\"other\":{\"type\":\"stdio\",\"command\":\"other.exe\"}}}");

                ProcessResult registerResult = fixture.RunRegister();

                Assert.AreEqual(0, registerResult.ExitCode, registerResult.Error);
                JObject vscodeConfiguration = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                JObject copilotConfiguration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNotNull(vscodeConfiguration["servers"]["other"]);
                Assert.AreEqual(fixture.McpExecutable, (string)vscodeConfiguration["servers"]["sqlnexus_mcp"]["command"]);
                Assert.IsNotNull(copilotConfiguration["mcpServers"]["other"]);
                Assert.AreEqual("stdio", (string)copilotConfiguration["mcpServers"]["sqlnexus_mcp"]["type"]);
                Assert.IsTrue(File.Exists(fixture.InstalledAgentPath));
                string installedAgent = File.ReadAllText(fixture.InstalledAgentPath);
                StringAssert.StartsWith(installedAgent, "---" + Environment.NewLine + "name:");
                StringAssert.Contains(installedAgent, fixture.SkillsDirectory.Replace('\\', '/') + "/scenario-performance.md");

                ProcessResult unregisterResult = fixture.RunUnregister();

                Assert.AreEqual(0, unregisterResult.ExitCode, unregisterResult.Error);
                vscodeConfiguration = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                copilotConfiguration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNotNull(vscodeConfiguration["servers"]["other"]);
                Assert.IsNull(vscodeConfiguration["servers"]["sqlnexus_mcp"]);
                Assert.IsNotNull(copilotConfiguration["mcpServers"]["other"]);
                Assert.IsNull(copilotConfiguration["mcpServers"]["sqlnexus_mcp"]);
                Assert.IsFalse(File.Exists(fixture.InstalledAgentPath));
            }
        }

        [TestMethod]
        public void Register_RepeatedWithSameValues_RemainsSuccessful()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult firstResult = fixture.RunRegister();
                ProcessResult secondResult = fixture.RunRegister();

                Assert.AreEqual(0, firstResult.ExitCode, firstResult.Error);
                Assert.AreEqual(0, secondResult.ExitCode, secondResult.Error);
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.AreEqual(1, ((JObject)configuration["mcpServers"]).Count);
            }
        }

        [TestMethod]
        public void Register_MalformedExistingConfiguration_RejectsWithoutChangingFile()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                const string malformedJson = "{ not valid json";
                File.WriteAllText(fixture.VsCodeConfigPath, malformedJson);

                ProcessResult result = fixture.RunRegister();

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Error, "does not contain valid JSON");
                Assert.AreEqual(malformedJson, File.ReadAllText(fixture.VsCodeConfigPath));
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
                Assert.IsFalse(File.Exists(fixture.InstalledAgentPath));
            }
        }

        private sealed class CopilotIntegrationFixture : IDisposable
        {
            private readonly string root;
            private readonly string registerScript;
            private readonly string unregisterScript;

            public CopilotIntegrationFixture()
            {
                root = Path.Combine(Path.GetTempPath(), "SqlNexusCopilotIntegrationTest_" + Guid.NewGuid().ToString("N"));
                InstallRoot = Path.Combine(root, "SQL Nexus Release");
                CopilotHome = Path.Combine(root, "User Profile", ".copilot");
                VsCodeUserData = Path.Combine(root, "VS Code", "User");
                SkillsDirectory = Path.Combine(InstallRoot, "AI", "Skills");
                McpExecutable = Path.Combine(InstallRoot, "SqlNexus.McpServer", "SqlNexus.McpServer.exe");
                InstalledAgentPath = Path.Combine(CopilotHome, "agents", "sql-nexus-diagnostic.agent.md");
                VsCodeConfigPath = Path.Combine(VsCodeUserData, "mcp.json");
                CopilotConfigPath = Path.Combine(CopilotHome, "mcp-config.json");

                Directory.CreateDirectory(Path.GetDirectoryName(McpExecutable));
                Directory.CreateDirectory(SkillsDirectory);
                Directory.CreateDirectory(Path.Combine(InstallRoot, ".github", "agents"));
                Directory.CreateDirectory(VsCodeUserData);
                Directory.CreateDirectory(CopilotHome);
                File.WriteAllText(McpExecutable, "test executable");
                File.WriteAllText(Path.Combine(SkillsDirectory, "scenario-performance.md"), "test skill");

                string repositoryRoot = FindRepositoryRoot();
                string sourceScripts = Path.Combine(repositoryRoot, "CopilotIntegration");
                string scriptDirectory = Path.Combine(InstallRoot, "CopilotIntegration");
                Directory.CreateDirectory(scriptDirectory);
                registerScript = Path.Combine(scriptDirectory, "Register-SqlNexusCopilotIntegration.ps1");
                unregisterScript = Path.Combine(scriptDirectory, "Unregister-SqlNexusCopilotIntegration.ps1");
                File.Copy(Path.Combine(sourceScripts, Path.GetFileName(registerScript)), registerScript);
                File.Copy(Path.Combine(sourceScripts, Path.GetFileName(unregisterScript)), unregisterScript);
                File.Copy(
                    Path.Combine(repositoryRoot, ".github", "agents", "sql-nexus-diagnostic.agent.md"),
                    Path.Combine(InstallRoot, ".github", "agents", "sql-nexus-diagnostic.agent.md"));
            }

            public string InstallRoot { get; }
            public string CopilotHome { get; }
            public string VsCodeUserData { get; }
            public string SkillsDirectory { get; }
            public string McpExecutable { get; }
            public string InstalledAgentPath { get; }
            public string VsCodeConfigPath { get; }
            public string CopilotConfigPath { get; }

            public ProcessResult RunRegister()
            {
                return RunPowerShell(
                    registerScript,
                    "-InstallRoot", InstallRoot,
                    "-CopilotHome", CopilotHome,
                    "-VsCodeUserData", VsCodeUserData,
                    "-Server", "localhost\\SQLEXPRESS",
                    "-Database", "NexusDiagnostics");
            }

            public ProcessResult RunUnregister()
            {
                return RunPowerShell(
                    unregisterScript,
                    "-CopilotHome", CopilotHome,
                    "-VsCodeUserData", VsCodeUserData);
            }

            public void Dispose()
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }

            private static ProcessResult RunPowerShell(string script, params string[] arguments)
            {
                string commandArguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script);
                foreach (string argument in arguments)
                {
                    commandArguments += " " + Quote(argument);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = commandArguments,
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
                    if (Directory.Exists(Path.Combine(directory.FullName, "CopilotIntegration"))
                        && Directory.Exists(Path.Combine(directory.FullName, ".github", "agents")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }

                throw new DirectoryNotFoundException("Could not locate the SQL Nexus repository root.");
            }
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