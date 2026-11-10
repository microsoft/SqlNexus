using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                StringAssert.Matches(registerResult.Output, new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] SQL Nexus Copilot integration registered\."));
                JObject vscodeConfiguration = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                JObject copilotConfiguration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNotNull(vscodeConfiguration["servers"]["other"]);
                Assert.AreEqual(fixture.McpExecutable, (string)vscodeConfiguration["servers"]["sqlnexus_mcp"]["command"]);
                Assert.IsNotNull(copilotConfiguration["mcpServers"]["other"]);
                Assert.AreEqual("stdio", (string)copilotConfiguration["mcpServers"]["sqlnexus_mcp"]["type"]);
                CollectionAssert.Contains(
                    ((JArray)copilotConfiguration["mcpServers"]["sqlnexus_mcp"]["args"]).ToObject<string[]>(),
                    "block");
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
        public void Register_ImpersonateReaderPolicy_PersistsExplicitRestrictedPolicy()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(elevatedPrincipalPolicy: "ImpersonateReader");

                Assert.AreEqual(0, result.ExitCode, result.Error);
                StringAssert.Contains(result.Output, "Elevated principal policy: ImpersonateReader");
                StringAssert.Contains(result.Output, "MCP queries do not run elevated");
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                string[] arguments = ((JArray)configuration["mcpServers"]["sqlnexus_mcp"]["args"]).ToObject<string[]>();
                int policyIndex = Array.IndexOf(arguments, "--elevated-principal-policy");
                Assert.IsTrue(policyIndex >= 0);
                Assert.AreEqual("impersonate-reader", arguments[policyIndex + 1]);
            }
        }

        [TestMethod]
        public void Register_WithoutInstallRoot_DefaultsToParentOfScriptDirectory()
        {
            // Regression: with Mandatory parameters, Windows PowerShell 5.1 left $PSScriptRoot empty in the
            // InstallRoot parameter default, so the documented command (no -InstallRoot) failed.
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(passInstallRoot: false);

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.IsTrue(File.Exists(fixture.InstalledAgentPath));
                Assert.AreEqual(fixture.McpExecutable, (string)JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath))["mcpServers"]["sqlnexus_mcp"]["command"]);
            }
        }

        [TestMethod]
        public void Register_MandatoryValidate_PersistsEncryptAndExplicitNoTrust()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Mandatory", certificateValidation: "Validate");

                Assert.AreEqual(0, result.ExitCode, result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("Mandatory", arguments[Array.IndexOf(arguments, "--encrypt") + 1]);
                Assert.AreEqual("false", arguments[Array.IndexOf(arguments, "--trust-server-certificate") + 1]);
                Assert.AreEqual(-1, Array.IndexOf(arguments, "--allow-certificate-validation-bypass"));
                Assert.IsFalse(result.Output.Contains("[WARNING]"), result.Output);
            }
        }

        [TestMethod]
        public void Register_StrictValidate_PersistsStrictEncryption()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Strict", certificateValidation: "Validate");

                Assert.AreEqual(0, result.ExitCode, result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("Strict", arguments[Array.IndexOf(arguments, "--encrypt") + 1]);
            }
        }

        [TestMethod]
        public void Register_MandatoryTrustServerCertificate_EmitsBothBypassArgumentsAndWarns()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Mandatory", certificateValidation: "TrustServerCertificate");

                Assert.AreEqual(0, result.ExitCode, result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("true", arguments[Array.IndexOf(arguments, "--trust-server-certificate") + 1]);
                Assert.AreEqual("true", arguments[Array.IndexOf(arguments, "--allow-certificate-validation-bypass") + 1]);
                StringAssert.Contains(result.Output, "[WARNING]");
                StringAssert.Contains(result.Output, "certificate validation is disabled");
            }
        }

        [TestMethod]
        public void Register_OptionalValidate_PersistsOptionalAndWarns()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Optional", certificateValidation: "Validate");

                Assert.AreEqual(0, result.ExitCode, result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("Optional", arguments[Array.IndexOf(arguments, "--encrypt") + 1]);
                StringAssert.Contains(result.Output, "not required by the client");
            }
        }

        [TestMethod]
        public void Register_StrictWithTrustServerCertificate_IsRejectedAndWritesNothing()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Strict", certificateValidation: "TrustServerCertificate");

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "Strict always requires certificate validation (-CertificateValidation Validate)");
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
                Assert.IsFalse(File.Exists(fixture.VsCodeConfigPath));
            }
        }

        [TestMethod]
        public void Register_OptionalWithTrustServerCertificate_IsRejectedAndWritesNothing()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Optional", certificateValidation: "TrustServerCertificate");

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "Optional cannot be combined");
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_MissingTlsParameters_FailsWithoutRegistering()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: null, certificateValidation: null);

                Assert.AreNotEqual(0, result.ExitCode);
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_InvalidEncryptionMode_Fails()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(encryptionMode: "Required");

                Assert.AreNotEqual(0, result.ExitCode);
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_RepeatedWithSameValues_RemainsSuccessful()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult firstResult = fixture.RunRegister();
                DateTime retainedWriteTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(fixture.VsCodeConfigPath, retainedWriteTime);
                File.SetLastWriteTimeUtc(fixture.CopilotConfigPath, retainedWriteTime);
                File.SetLastWriteTimeUtc(fixture.InstalledAgentPath, retainedWriteTime);

                ProcessResult secondResult = fixture.RunRegister();

                Assert.AreEqual(0, firstResult.ExitCode, firstResult.Error);
                Assert.AreEqual(0, secondResult.ExitCode, secondResult.Error);
                StringAssert.Contains(secondResult.Output, "SQL Nexus Copilot integration is already registered. Existing matching settings were not replaced.");
                StringAssert.Contains(secondResult.Output, "SQL Server: localhost\\SQLEXPRESS");
                StringAssert.Contains(secondResult.Output, "Database: NexusDiagnostics");
                StringAssert.Contains(secondResult.Output, "Authentication: Windows Integrated Authentication");
                Assert.AreEqual(retainedWriteTime, File.GetLastWriteTimeUtc(fixture.VsCodeConfigPath));
                Assert.AreEqual(retainedWriteTime, File.GetLastWriteTimeUtc(fixture.CopilotConfigPath));
                Assert.AreEqual(retainedWriteTime, File.GetLastWriteTimeUtc(fixture.InstalledAgentPath));
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.AreEqual(1, ((JObject)configuration["mcpServers"]).Count);
            }
        }

        [TestMethod]
        public void Register_McpOnlyWithoutAgentAssets_RegistersServerWithoutAgent()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                Directory.Delete(fixture.SkillsDirectory, true);
                File.Delete(fixture.AgentSourcePath);

                ProcessResult result = fixture.RunRegister(mcpOnly: true);

                Assert.AreEqual(0, result.ExitCode, result.Error);
                StringAssert.Contains(result.Output, "SQL Nexus MCP server registered. The custom agent was not changed.");
                Assert.IsFalse(result.Output.Contains("Custom agent:"), result.Output);
                JObject vscodeConfiguration = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                JObject copilotConfiguration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNotNull(vscodeConfiguration["servers"]["sqlnexus_mcp"]);
                Assert.IsNotNull(copilotConfiguration["mcpServers"]["sqlnexus_mcp"]);
                Assert.IsFalse(File.Exists(fixture.InstalledAgentPath));
            }
        }

        [TestMethod]
        public void Unregister_McpOnlyWithInstalledAgent_RemovesServerAndPreservesAgent()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult registerResult = fixture.RunRegister();

                ProcessResult unregisterResult = fixture.RunUnregister(mcpOnly: true);

                Assert.AreEqual(0, registerResult.ExitCode, registerResult.Error);
                Assert.AreEqual(0, unregisterResult.ExitCode, unregisterResult.Error);
                StringAssert.Contains(unregisterResult.Output, "SQL Nexus MCP server unregistered. The custom agent was preserved.");
                JObject vscodeConfiguration = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                JObject copilotConfiguration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNull(vscodeConfiguration["servers"]["sqlnexus_mcp"]);
                Assert.IsNull(copilotConfiguration["mcpServers"]["sqlnexus_mcp"]);
                Assert.IsTrue(File.Exists(fixture.InstalledAgentPath));
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
                StringAssert.Matches(result.Output, new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[ERROR\] SQL Nexus Copilot integration was not registered\."));
                StringAssert.Contains(result.Output, "does not contain valid JSON");
                Assert.AreEqual(string.Empty, result.Error);
                Assert.AreEqual(malformedJson, File.ReadAllText(fixture.VsCodeConfigPath));
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
                Assert.IsFalse(File.Exists(fixture.InstalledAgentPath));
            }
        }

        [TestMethod]
        public void Register_ConflictingServerEntry_ShowsConciseActionableError()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                File.WriteAllText(
                    fixture.CopilotConfigPath,
                    "{\"mcpServers\":{\"sqlnexus_mcp\":{\"type\":\"stdio\",\"command\":\"old.exe\"}}}");

                ProcessResult result = fixture.RunRegister();

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Matches(result.Output, new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[ERROR\] SQL Nexus Copilot integration was not registered\."));
                StringAssert.Contains(result.Output, "Re-run with -Force to replace only that entry.");
                Assert.IsFalse(result.Output.Contains(" char:"), result.Output);
                Assert.IsFalse(result.Output.Contains("CategoryInfo"), result.Output);
                Assert.AreEqual(string.Empty, result.Error);
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.AreEqual("old.exe", (string)configuration["mcpServers"]["sqlnexus_mcp"]["command"]);
                Assert.IsFalse(File.Exists(fixture.InstalledAgentPath));
            }
        }

        [TestMethod]
        public void Unregister_MalformedExistingConfiguration_ShowsConciseErrorWithoutChangingFile()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                const string malformedJson = "{ not valid json";
                File.WriteAllText(fixture.CopilotConfigPath, malformedJson);

                ProcessResult result = fixture.RunUnregister();

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Matches(result.Output, new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[ERROR\] SQL Nexus Copilot integration was not unregistered\."));
                StringAssert.Contains(result.Output, "does not contain valid JSON");
                Assert.IsFalse(result.Output.Contains(" char:"), result.Output);
                Assert.AreEqual(string.Empty, result.Error);
                Assert.AreEqual(malformedJson, File.ReadAllText(fixture.CopilotConfigPath));
            }
        }

        private const string SqlPassword = "Zx9!unique-Passw0rd#42";

        private static string[] SqlAuthArguments(CopilotIntegrationFixture fixture)
        {
            return new[] { "-AuthMode", "Sql", "-UserId", "sqlnexus_reader", "-McpExecutablePath", fixture.StubPath };
        }

        [TestMethod]
        public void Register_SqlAuthWithoutUserId_FailsWithoutRegistering()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(extraArguments: new[] { "-AuthMode", "Sql" });

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "-UserId is required");
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_WindowsAuthWithSqlOnlyParameters_IsRejected()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(extraArguments: new[] { "-UserId", "sqlnexus_reader" });

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "only valid with -AuthMode Sql");
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_InvalidAuthMode_Fails()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister(extraArguments: new[] { "-AuthMode", "Kerberos" });

                Assert.AreNotEqual(0, result.ExitCode);
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
            }
        }

        [TestMethod]
        public void Register_WindowsAuth_DefaultDoesNotTouchCredentialsOrUseUserId()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunRegister();

                Assert.AreEqual(0, result.ExitCode, result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("true", arguments[Array.IndexOf(arguments, "--trusted-connection") + 1]);
                CollectionAssert.DoesNotContain(arguments, "--user-id");
                StringAssert.Contains(result.Output, "Windows Integrated Authentication");
            }
        }

        [TestMethod]
        public void Register_SqlAuthWithExistingCredential_KeepsItAndPersistsNoSecret()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");

                ProcessResult result = fixture.RunRegister(extraArguments: SqlAuthArguments(fixture));

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                string[] arguments = fixture.ReadRegisteredArguments();
                Assert.AreEqual("false", arguments[Array.IndexOf(arguments, "--trusted-connection") + 1]);
                Assert.AreEqual("sqlnexus_reader", arguments[Array.IndexOf(arguments, "--user-id") + 1]);
                Assert.AreEqual("localhost\\SQLEXPRESS", arguments[Array.IndexOf(arguments, "--server") + 1]);
                string calls = fixture.ReadCalls();
                StringAssert.Contains(calls, "--check-credential");
                Assert.IsFalse(calls.Contains("--set-credential"), calls);
                Assert.AreEqual("stored-earlier", fixture.ReadStoredCredential());
                StringAssert.Contains(result.Output, "was kept");
                Assert.IsFalse(File.ReadAllText(fixture.CopilotConfigPath).Contains("stored-earlier"));
            }
        }

        [TestMethod]
        public void Register_SqlAuthWithoutCredentialInNonInteractiveSession_FailsAndWritesNothing()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub();

                ProcessResult result = fixture.RunRegister(extraArguments: SqlAuthArguments(fixture));

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "not interactive");
                StringAssert.Contains(result.Output, "-SqlPassword");
                Assert.IsFalse(File.Exists(fixture.CopilotConfigPath));
                Assert.IsFalse(File.Exists(fixture.VsCodeConfigPath));
                Assert.IsNull(fixture.ReadStoredCredential());
            }
        }

        [TestMethod]
        public void Register_SqlAuthWithSecurePassword_SendsPasswordOnStandardInputOnly()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub();

                ProcessResult result = fixture.RunRegisterWithSecurePassword(SqlPassword, SqlAuthArguments(fixture));

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.AreEqual(SqlPassword, fixture.ReadStoredCredential(), result.Output + result.Error);
                string calls = fixture.ReadCalls();
                StringAssert.Contains(calls, "--set-credential");
                StringAssert.Contains(calls, "--password-stdin");
                Assert.IsFalse(calls.Contains(SqlPassword), "The password must never appear on a command line.");
                Assert.IsFalse(result.Output.Contains(SqlPassword));
                Assert.IsFalse(result.Error.Contains(SqlPassword));
                Assert.IsFalse(File.ReadAllText(fixture.CopilotConfigPath).Contains(SqlPassword));
                Assert.IsFalse(File.ReadAllText(fixture.VsCodeConfigPath).Contains(SqlPassword));
            }
        }

        [TestMethod]
        public void Register_SqlAuthUpdateCredential_ReplacesExistingPassword()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "old-password");
                string[] extra = SqlAuthArguments(fixture).Concat(new[] { "-UpdateCredential" }).ToArray();

                ProcessResult result = fixture.RunRegisterWithSecurePassword(SqlPassword, extra);

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.AreEqual(SqlPassword, fixture.ReadStoredCredential());
            }
        }

        [TestMethod]
        public void Register_SqlAuthConflictingEntry_FailsBeforeStoringCredential()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub();
                File.WriteAllText(fixture.CopilotConfigPath, "{\"mcpServers\":{\"sqlnexus_mcp\":{\"type\":\"stdio\",\"command\":\"old.exe\"}}}");

                ProcessResult result = fixture.RunRegisterWithSecurePassword(SqlPassword, SqlAuthArguments(fixture));

                Assert.AreNotEqual(0, result.ExitCode);
                Assert.IsNull(fixture.ReadStoredCredential());
            }
        }

        [TestMethod]
        public void Register_SqlAuthPasswordWithExistingCredentialAndNoUpdate_WarnsItWasIgnored()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "old-password");

                ProcessResult result = fixture.RunRegisterWithSecurePassword(SqlPassword, SqlAuthArguments(fixture));

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "-SqlPassword was ignored");
                Assert.AreEqual("old-password", fixture.ReadStoredCredential());
                Assert.IsFalse(result.Output.Contains(SqlPassword));
            }
        }

        [TestMethod]
        public void Unregister_RegisteredCommandIsNotTheMcpServer_IsNotExecuted()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                string unrelated = Path.Combine(Path.GetDirectoryName(fixture.StubPath), "unrelated-tool.cmd");
                File.Copy(fixture.StubPath, unrelated);
                File.WriteAllText(
                    fixture.CopilotConfigPath,
                    new JObject
                    {
                        ["mcpServers"] = new JObject
                        {
                            ["sqlnexus_mcp"] = new JObject
                            {
                                ["type"] = "stdio",
                                ["command"] = unrelated,
                                ["args"] = new JArray("--server", "localhost", "--trusted-connection", "false", "--user-id", "reader")
                            }
                        }
                    }.ToString());
                File.Delete(fixture.McpExecutable);

                ProcessResult result = fixture.RunUnregister();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "was not run");
                Assert.AreEqual(string.Empty, fixture.ReadCalls());
                Assert.AreEqual("stored-earlier", fixture.ReadStoredCredential());
            }
        }

        [TestMethod]
        public void Unregister_ExecutableCannotStart_ReportsCredentialNotDeletedAndFails()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                Assert.AreEqual(0, fixture.RunRegister(extraArguments: SqlAuthArguments(fixture)).ExitCode);

                // The default fixture "executable" is a text file, so it cannot be started.
                ProcessResult result = fixture.RunUnregister(
                    extraArguments: new[] { "-McpExecutablePath", fixture.McpExecutable });

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "could not be deleted");
                Assert.AreEqual("stored-earlier", fixture.ReadStoredCredential());
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNull(configuration["mcpServers"]["sqlnexus_mcp"]);
            }
        }

        [TestMethod]
        public void Unregister_SqlAuthRegistration_DeletesTheStoredCredential()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                Assert.AreEqual(0, fixture.RunRegister(extraArguments: SqlAuthArguments(fixture)).ExitCode);

                ProcessResult result = fixture.RunUnregister();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.IsNull(fixture.ReadStoredCredential());
                string calls = fixture.ReadCalls();
                StringAssert.Contains(calls, "--delete-credential --server localhost\\SQLEXPRESS --user-id sqlnexus_reader");
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNull(configuration["mcpServers"]["sqlnexus_mcp"]);
            }
        }

        [TestMethod]
        public void Unregister_KeepCredential_PreservesTheStoredCredential()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                Assert.AreEqual(0, fixture.RunRegister(extraArguments: SqlAuthArguments(fixture)).ExitCode);

                ProcessResult result = fixture.RunUnregister(extraArguments: new[] { "-KeepCredential" });

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.AreEqual("stored-earlier", fixture.ReadStoredCredential());
                Assert.IsFalse(fixture.ReadCalls().Contains("--delete-credential"));
            }
        }

        [TestMethod]
        public void Unregister_WindowsAuthRegistration_DoesNotInvokeCredentialDeletion()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "unrelated");
                Assert.AreEqual(0, fixture.RunRegister().ExitCode);

                ProcessResult result = fixture.RunUnregister();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.AreEqual("unrelated", fixture.ReadStoredCredential());
                Assert.AreEqual(string.Empty, fixture.ReadCalls());
            }
        }

        [TestMethod]
        public void Unregister_CredentialSharedWithAnotherEntry_KeepsCredentialAndWarns()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                Assert.AreEqual(0, fixture.RunRegister(extraArguments: SqlAuthArguments(fixture)).ExitCode);
                JObject vscode = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                ((JObject)vscode["servers"])["my_other_nexus"] = new JObject
                {
                    ["command"] = fixture.StubPath,
                    ["args"] = new JArray("--server", "LOCALHOST\\sqlexpress", "--trusted-connection", "false", "--user-id", "SQLNEXUS_READER")
                };
                File.WriteAllText(fixture.VsCodeConfigPath, vscode.ToString());

                ProcessResult result = fixture.RunUnregister();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "my_other_nexus");
                Assert.AreEqual("stored-earlier", fixture.ReadStoredCredential());
                JObject after = JObject.Parse(File.ReadAllText(fixture.VsCodeConfigPath));
                Assert.IsNull(after["servers"]["sqlnexus_mcp"]);
                Assert.IsNotNull(after["servers"]["my_other_nexus"]);
            }
        }

        [TestMethod]
        public void Unregister_ExecutableMissing_RemovesEntriesAndWarnsCredentialWasNotDeleted()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");
                Assert.AreEqual(0, fixture.RunRegister(extraArguments: SqlAuthArguments(fixture)).ExitCode);
                File.Delete(fixture.StubPath);

                ProcessResult result = fixture.RunUnregister();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "was NOT deleted");
                StringAssert.Contains(result.Output, "--delete-credential");
                JObject configuration = JObject.Parse(File.ReadAllText(fixture.CopilotConfigPath));
                Assert.IsNull(configuration["mcpServers"]["sqlnexus_mcp"]);
            }
        }

        [TestMethod]
        public void Unregister_ServerWithoutUserIdOverride_IsRejected()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                ProcessResult result = fixture.RunUnregister(extraArguments: new[] { "-Server", "localhost" });

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "-Server and -UserId must be specified together");
            }
        }

        [TestMethod]
        public void Unregister_ExplicitServerAndUser_DeletesThatCredentialEvenWithoutRegistration()
        {
            using (var fixture = new CopilotIntegrationFixture())
            {
                fixture.CreateStub(existingCredential: "stored-earlier");

                ProcessResult result = fixture.RunUnregister(
                    extraArguments: new[] { "-Server", "localhost", "-UserId", "reader", "-McpExecutablePath", fixture.StubPath });

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.IsNull(fixture.ReadStoredCredential());
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
            public string AgentSourcePath
            {
                get { return Path.Combine(InstallRoot, ".github", "agents", "sql-nexus-diagnostic.agent.md"); }
            }
            public string InstalledAgentPath { get; }
            public string VsCodeConfigPath { get; }
            public string CopilotConfigPath { get; }

            public string StubPath
            {
                get { return Path.Combine(root, "McpStub", "SqlNexus.McpServer.cmd"); }
            }

            // A stand-in for SqlNexus.McpServer.exe that records every call (arguments only) and keeps the
            // "stored credential" in a text file, so the scripts' credential handling can be tested
            // deterministically without DPAPI or a real executable.
            public void CreateStub(string existingCredential = null)
            {
                string stubDirectory = Path.GetDirectoryName(StubPath);
                Directory.CreateDirectory(stubDirectory);
                File.WriteAllText(
                    StubPath,
                    "@echo off\r\n" +
                    "setlocal EnableDelayedExpansion\r\n" +
                    ">>\"%~dp0calls.log\" echo %*\r\n" +
                    "if /i \"%~1\"==\"--check-credential\" goto check\r\n" +
                    "if /i \"%~1\"==\"--set-credential\" goto set\r\n" +
                    "if /i \"%~1\"==\"--delete-credential\" goto delete\r\n" +
                    "exit /b 9\r\n" +
                    ":check\r\n" +
                    "if exist \"%~dp0credential.txt\" exit /b 0\r\n" +
                    "echo No stored credential. 1>&2\r\n" +
                    "exit /b 1\r\n" +
                    ":set\r\n" +
                    "set /p PW=\r\n" +
                    ">\"%~dp0credential.txt\" echo !PW!\r\n" +
                    "echo Stored.\r\n" +
                    "exit /b 0\r\n" +
                    ":delete\r\n" +
                    "if exist \"%~dp0credential.txt\" del \"%~dp0credential.txt\"\r\n" +
                    "echo Deleted the stored SQL credential.\r\n" +
                    "exit /b 0\r\n");
                if (existingCredential != null)
                {
                    File.WriteAllText(Path.Combine(stubDirectory, "credential.txt"), existingCredential + "\r\n");
                }
            }

            public string ReadCalls()
            {
                string path = Path.Combine(Path.GetDirectoryName(StubPath), "calls.log");
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }

            public string ReadStoredCredential()
            {
                string path = Path.Combine(Path.GetDirectoryName(StubPath), "credential.txt");
                return File.Exists(path) ? File.ReadAllText(path).TrimEnd('\r', '\n') : null;
            }

            public string[] ReadRegisteredArguments()
            {
                JObject configuration = JObject.Parse(File.ReadAllText(CopilotConfigPath));
                return ((JArray)configuration["mcpServers"]["sqlnexus_mcp"]["args"]).ToObject<string[]>();
            }

            public ProcessResult RunRegister(
                bool mcpOnly = false,
                string elevatedPrincipalPolicy = "Block",
                string encryptionMode = "Mandatory",
                string certificateValidation = "Validate",
                bool passInstallRoot = true,
                string[] extraArguments = null)
            {
                return RunPowerShell(registerScript, BuildRegisterArguments(
                    mcpOnly, elevatedPrincipalPolicy, encryptionMode, certificateValidation, passInstallRoot, extraArguments));
            }

            // Runs the registration script through -Command so a real SecureString can be supplied.
            public ProcessResult RunRegisterWithSecurePassword(string password, string[] extraArguments)
            {
                string[] arguments = BuildRegisterArguments(false, "Block", "Mandatory", "Validate", true, extraArguments);
                string command = "$ErrorActionPreference = 'Stop'; "
                    + "$securePassword = New-Object System.Security.SecureString; "
                    + SingleQuote(password) + ".ToCharArray() | ForEach-Object { $securePassword.AppendChar($_) }; "
                    + "& " + SingleQuote(registerScript);
                foreach (string argument in arguments)
                {
                    command += " " + (argument.StartsWith("-") ? argument : SingleQuote(argument));
                }

                command += " -SqlPassword $securePassword; exit $LASTEXITCODE";
                return RunPowerShellCommand(command);
            }

            private string[] BuildRegisterArguments(
                bool mcpOnly,
                string elevatedPrincipalPolicy,
                string encryptionMode,
                string certificateValidation,
                bool passInstallRoot,
                string[] extraArguments)
            {
                var arguments = new System.Collections.Generic.List<string>
                {
                    "-CopilotHome", CopilotHome,
                    "-VsCodeUserData", VsCodeUserData,
                    "-Server", "localhost\\SQLEXPRESS",
                    "-Database", "NexusDiagnostics",
                    "-ElevatedPrincipalPolicy", elevatedPrincipalPolicy
                };
                if (passInstallRoot)
                {
                    arguments.Add("-InstallRoot");
                    arguments.Add(InstallRoot);
                }
                if (encryptionMode != null)
                {
                    arguments.Add("-EncryptionMode");
                    arguments.Add(encryptionMode);
                }
                if (certificateValidation != null)
                {
                    arguments.Add("-CertificateValidation");
                    arguments.Add(certificateValidation);
                }
                if (mcpOnly)
                {
                    arguments.Add("-McpOnly");
                }
                if (extraArguments != null)
                {
                    arguments.AddRange(extraArguments);
                }

                return arguments.ToArray();
            }

            public ProcessResult RunUnregister(bool mcpOnly = false, string[] extraArguments = null)
            {
                var arguments = new System.Collections.Generic.List<string>
                {
                    "-CopilotHome", CopilotHome,
                    "-VsCodeUserData", VsCodeUserData
                };
                if (mcpOnly)
                {
                    arguments.Add("-McpOnly");
                }
                if (extraArguments != null)
                {
                    arguments.AddRange(extraArguments);
                }

                return RunPowerShell(unregisterScript, arguments.ToArray());
            }

            public void Dispose()
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }

            private static string SingleQuote(string value)
            {
                return "'" + value.Replace("'", "''") + "'";
            }

            private static ProcessResult RunPowerShellCommand(string command)
            {
                return RunPowerShellWithArguments("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(command));
            }

            private static ProcessResult RunPowerShell(string script, params string[] arguments)
            {
                string commandArguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script);
                foreach (string argument in arguments)
                {
                    commandArguments += " " + Quote(argument);
                }

                return RunPowerShellWithArguments(commandArguments);
            }

            private static ProcessResult RunPowerShellWithArguments(string commandArguments)
            {
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