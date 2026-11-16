using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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

        [TestMethod]
        public void BuildConfiguration_TestScript_IsOnlyCopiedToPackagedMcpDirectory()
        {
            string repositoryRoot = FindRepositoryRoot();
            XDocument mcpProject = XDocument.Load(Path.Combine(
                repositoryRoot,
                "SqlNexus.McpServer",
                "SqlNexus.McpServer.csproj"));
            XDocument sqlNexusProject = XDocument.Load(Path.Combine(repositoryRoot, "sqlnexus", "sqlnexus.csproj"));

            XElement testScriptItem = mcpProject
                .Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "None"
                    && (string)element.Attribute("Update") == "Test-McpServer.ps1");
            XElement supportFile = sqlNexusProject
                .Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "_McpServerSupportFiles"
                    && ((string)element.Attribute("Include"))?.EndsWith("SqlNexus.McpServer\\Test-McpServer.ps1") == true);
            XElement supportFileCopy = sqlNexusProject
                .Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "Copy"
                    && (string)element.Attribute("SourceFiles") == "@(_McpServerSupportFiles)");

            Assert.IsNull(testScriptItem, "The MCP project output should not contain a redundant test-script copy.");
            Assert.IsNotNull(supportFile, "The source test script must be included in the packaged SQL Nexus output.");
            Assert.IsNotNull(supportFileCopy, "The packaged test script copy operation is missing.");
            StringAssert.Contains(
                (string)supportFileCopy.Attribute("DestinationFolder"),
                "SqlNexus.McpServer",
                "The packaged script must be placed beside SqlNexus.McpServer.exe.");
        }

        [TestMethod]
        public void InteractiveAllTools_CurrentSessionPolicy_IsPassedToDirectRegistration()
        {
            string script = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "SqlNexus.McpServer",
                "Test-McpServer.ps1"));

            StringAssert.Contains(script, "8. Test All Tools Using Current Connection Settings");
            StringAssert.Contains(
                script,
                "Get-DirectMcpServerRegistration -Server $serverName -Database $databaseName");
            StringAssert.Contains(
                script,
                "-ElevatedPrincipalPolicy $effectiveElevatedPrincipalPolicy");
            Assert.IsFalse(
                script.Contains("8. Test All Tools Against Registered MCP Configuration"),
                "Interactive validation must not imply that it ignores the current session settings.");
            Assert.IsFalse(
                script.Contains("If results look stale"),
                "Current-session validation should not display stale-registration guidance.");
        }

        [TestMethod]
        public void ForceRegistration_WithConfigurationPath_ReturnsFailure()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunForceRegistration(
                    nonInteractive: true,
                    extraArguments: "-ConfigurationPath " + Quote(fixture.VsCodeConfigPath));

                Assert.AreNotEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "does not support -ConfigurationPath");
            }
        }

        [TestMethod]
        public void ForceRegistration_NonInteractiveWithExistingDifferentRegistration_OverwritesAutomatically()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                fixture.SeedExistingRegistration(fixture.OldFakeExecutable, "OLD-SERVER", "OldDb", "block");

                ProcessResult result = fixture.RunForceRegistration(nonInteractive: true, server: "NEW-SERVER", database: "NewDb");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "overwriting the existing registration automatically");
                string vscodeJson = File.ReadAllText(fixture.VsCodeConfigPath);
                StringAssert.Contains(vscodeJson, "NEW-SERVER");
                StringAssert.Contains(vscodeJson, "NewDb");
                StringAssert.Contains(vscodeJson, fixture.NewFakeExecutable.Replace("\\", "\\\\"));
                Assert.IsFalse(vscodeJson.Contains("OLD-SERVER"), "The stale registration was not replaced: " + vscodeJson);
            }
        }

        [TestMethod]
        public void ForceRegistration_NoExistingRegistration_RegistersWithoutPrompting()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunForceRegistration(nonInteractive: true, server: "NEW-SERVER", database: "NewDb");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "registering for the first time");
                string vscodeJson = File.ReadAllText(fixture.VsCodeConfigPath);
                StringAssert.Contains(vscodeJson, "NEW-SERVER");
            }
        }

        [TestMethod]
        public void ForceRegistration_InteractiveWithServerAndDatabase_DoesNotPromptForConnectionSettings()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunForceRegistration(
                    nonInteractive: false,
                    server: "NEW-SERVER",
                    database: "NewDb",
                    stdinInput: string.Empty);

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                Assert.IsFalse(
                    result.Output.Contains("Connection Settings"),
                    "Command-line connection values should not be requested again: " + result.Output);
                Assert.IsFalse(
                    result.Output.Contains("SQL Server instance ["),
                    "The server was supplied on the command line but was prompted for again: " + result.Output);
                Assert.IsFalse(
                    result.Output.Contains("SQL Nexus database  ["),
                    "The database was supplied on the command line but was prompted for again: " + result.Output);
                string vscodeJson = File.ReadAllText(fixture.VsCodeConfigPath);
                StringAssert.Contains(vscodeJson, "NEW-SERVER");
                StringAssert.Contains(vscodeJson, "NewDb");
            }
        }

        [TestMethod]
        public void ForceRegistration_InteractiveDeclineOverwrite_KeepsExistingRegistration()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                fixture.SeedExistingRegistration(fixture.OldFakeExecutable, "OLD-SERVER", "OldDb", "block");

                // Interactive (no -NonInteractive): simulate the user pressing Enter (accepting the
                // default 'Keep' choice) by supplying empty stdin.
                ProcessResult result = fixture.RunForceRegistration(nonInteractive: false, server: "NEW-SERVER", database: "NewDb", stdinInput: "\n");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "Keeping the existing registration unchanged");
                string vscodeJson = File.ReadAllText(fixture.VsCodeConfigPath);
                StringAssert.Contains(vscodeJson, "OLD-SERVER");
                Assert.IsFalse(vscodeJson.Contains("NEW-SERVER"), "The existing registration should not have been replaced: " + vscodeJson);
            }
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(",\"--encrypt\",\"Optional\",\"--trust-server-certificate\",\"false\"")]
        [DataRow(",\"--encrypt\",\"Mandatory\",\"--trust-server-certificate\",\"false\"")]
        [DataRow(",\"--encrypt\",\"Strict\",\"--trust-server-certificate\",\"false\"")]
        [DataRow(",\"--encrypt\",\"true\"")]
        [DataRow(",\"--encrypt\",\"false\"")]
        [DataRow(",\"--encrypt\",\" optional \",\"--trust-server-certificate\",\" FALSE \"")]
        [DataRow(",\"--encrypt\",\"Mandatory\",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"true\"")]
        [DataRow(",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"true\"")]
        [DataRow(",\"--encrypt\",\"Strict\",\"--allow-certificate-validation-bypass\",\"true\"")]
        public void RegisteredConfiguration_SupportedTlsArguments_ArePreserved(string tlsArguments)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunConfigurationReader(tlsArguments);

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "\"--server\",\"localhost\",\"--database\",\"SqlNexus\"" + tlsArguments);
            }
        }

        [DataTestMethod]
        [DataRow(",\"--encrypt\",\"Invalid\"", "--encrypt value")]
        [DataRow(",\"--encrypt\",\"\"", "requires a value")]
        [DataRow(",\"--encrypt\"", "requires a value")]
        [DataRow(",\"--encrypt\",\"Optional\",\"--encrypt\",\"Mandatory\"", "duplicate argument")]
        [DataRow(",\"--trust-server-certificate\",\"yes\"", "--trust-server-certificate value")]
        [DataRow(",\"--allow-certificate-validation-bypass\",\"yes\"", "--allow-certificate-validation-bypass value")]
        [DataRow(",\"--trust-server-certificate\",\"true\"", "requires --allow-certificate-validation-bypass true")]
        [DataRow(",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"false\"", "requires --allow-certificate-validation-bypass true")]
        [DataRow(",\"--encrypt\",\"Strict\",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"true\"", "cannot be combined")]
        [DataRow(",\"--encrypt\",\"Optional\",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"true\"", "cannot be combined")]
        [DataRow(",\"--encrypt\",\"false\",\"--trust-server-certificate\",\"true\",\"--allow-certificate-validation-bypass\",\"true\"", "cannot be combined")]
        [DataRow(",\"--unknown\",\"true\"", "unsupported argument")]
        public void RegisteredConfiguration_InvalidTlsArguments_ReturnFailure(string tlsArguments, string expectedMessage)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunConfigurationReader(tlsArguments);

                Assert.AreNotEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Error + result.Output, expectedMessage);
            }
        }

        [DataTestMethod]
        [DataRow("Optional", "Validate")]
        [DataRow("Mandatory", "Validate")]
        [DataRow("Strict", "Validate")]
        [DataRow("Mandatory", "TrustServerCertificate")]
        public void ForceRegistration_GeneratedTlsConfiguration_IsAccepted(string encryptionMode, string certificateValidation)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult registration = fixture.RunForceRegistration(
                    nonInteractive: true,
                    server: "localhost",
                    database: "SqlNexus",
                    extraArguments: "-EncryptionMode " + encryptionMode + " -CertificateValidation " + certificateValidation);
                Assert.AreEqual(0, registration.ExitCode, registration.Output + registration.Error);

                ProcessResult result = fixture.RunConfigurationReader();

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "\"--encrypt\",\"" + encryptionMode + "\"");
            }
        }

        [DataTestMethod]
        [DataRow("", "", "Mandatory", "Validate")]
        [DataRow("Optional", "", "Optional", "Validate")]
        [DataRow("Strict", "Validate", "Strict", "Validate")]
        [DataRow("Mandatory", "TrustServerCertificate", "Mandatory", "TrustServerCertificate")]
        [DataRow("invalid\nOptional", "invalid\nValidate", "Optional", "Validate")]
        public void InteractiveTlsPrompts_SelectedSettings_AreUsedByIndividualAndAllTools(
            string modeInput, string certificateInput, string expectedMode, string expectedCertificate)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                string responses = string.Join(",", (modeInput + "\n" + certificateInput)
                    .Split('\n').Select(value => "'" + value + "'"));
                ProcessResult result = fixture.RunScriptFunctions(@"
$script:responses = New-Object 'System.Collections.Generic.Queue[string]'
@(" + responses + @") | ForEach-Object { $script:responses.Enqueue($_) }
function Read-Host {
    param([string]$Prompt)
    Write-Host ('PROMPT: ' + $Prompt)
    if ($script:responses.Count -eq 0) { throw 'Unexpected extra prompt.' }
    return $script:responses.Dequeue()
}
$settings = Read-McpTlsSettings -PromptEncryptionMode -PromptCertificateValidation
$EncryptionMode = $settings.EncryptionMode
$CertificateValidation = $settings.CertificateValidation
$registration = Get-DirectMcpServerRegistration -Server localhost -Database SqlNexus -ExecutablePath $ExecutablePath `
    -EncryptionMode $EncryptionMode -CertificateValidation $CertificateValidation
Write-Output ('ALL: ' + (ConvertTo-Json -InputObject $registration.Arguments -Compress))
$serverName = 'localhost'
$databaseName = 'SqlNexus'
$trustedConnection = $true
$elevatedPrincipalPolicyArg = 'block'
$exePath = { $script:capturedArguments = @($args); return '{""result"":{}}' }
Invoke-McpTool -Messages @('{}')
Write-Output ('INDIVIDUAL: ' + (ConvertTo-Json -InputObject $script:capturedArguments -Compress))
");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "PROMPT:   Encryption mode (Optional/Mandatory/Strict) [Mandatory]");
                StringAssert.Contains(result.Output, "PROMPT:   Certificate validation (Validate/TrustServerCertificate) [Validate]");
                string trust = expectedCertificate == "TrustServerCertificate" ? "true" : "false";
                string expectedTls = "\"--encrypt\",\"" + expectedMode + "\",\"--trust-server-certificate\",\"" + trust + "\"";
                string[] argumentLines = result.Output.Split('\n')
                    .Where(line => line.StartsWith("ALL: ") || line.StartsWith("INDIVIDUAL: ")).ToArray();
                Assert.AreEqual(2, argumentLines.Length, result.Output);
                foreach (string line in argumentLines)
                {
                    StringAssert.Contains(line, expectedTls);
                    if (trust == "true")
                    {
                        StringAssert.Contains(line, "\"--allow-certificate-validation-bypass\",\"true\"");
                    }
                }
                if (expectedMode == "Optional")
                {
                    StringAssert.Contains(result.Output, "Encryption is not required");
                }
                if (trust == "true")
                {
                    StringAssert.Contains(result.Output, "certificate validation is disabled");
                }
                if (modeInput.Contains("invalid"))
                {
                    StringAssert.Contains(result.Output, "Invalid encryption mode");
                    StringAssert.Contains(result.Output, "Invalid certificate validation");
                }
            }
        }

        [TestMethod]
        public void InteractiveTlsPrompts_EndOfInput_KeepsSecureDefaults()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunScriptFunctions(@"
function Read-Host { return $null }
$settings = Read-McpTlsSettings -PromptEncryptionMode -PromptCertificateValidation
Write-Output ($settings.EncryptionMode + '/' + $settings.CertificateValidation)
");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "Mandatory/Validate");
            }
        }

        [DataTestMethod]
        [DataRow("Optional", "Validate")]
        [DataRow("Mandatory", "TrustServerCertificate")]
        public void ForceRegistration_InteractiveTlsSelection_IsWrittenToConfiguration(string mode, string certificate)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunForceRegistration(
                    nonInteractive: false,
                    server: "localhost",
                    database: "SqlNexus",
                    stdinInput: mode + "\n" + certificate + "\n");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                ProcessResult readerResult = fixture.RunConfigurationReader();
                Assert.AreEqual(0, readerResult.ExitCode, readerResult.Output + readerResult.Error);
                StringAssert.Contains(readerResult.Output, "\"--encrypt\",\"" + mode + "\"");
                if (certificate == "TrustServerCertificate")
                {
                    StringAssert.Contains(readerResult.Output, "\"--allow-certificate-validation-bypass\",\"true\"");
                }
            }
        }

        [DataTestMethod]
        [DataRow("Optional")]
        [DataRow("Mandatory")]
        [DataRow("Strict")]
        public void TlsSettings_ExplicitValues_DoNotPrompt(string encryptionMode)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunScriptFunctions(@"
function Read-Host { throw 'Explicit TLS settings must not prompt.' }
$settings = Read-McpTlsSettings -EncryptionMode '" + encryptionMode + @"' -CertificateValidation Validate
Write-Output ($settings.EncryptionMode + '/' + $settings.CertificateValidation)
");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, encryptionMode + "/Validate");
            }
        }

        [DataTestMethod]
        [DataRow("Optional")]
        [DataRow("Strict")]
        public void InteractiveTlsPrompts_UnsafeBypass_FailsBeforeLaunchingServer(string encryptionMode)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunScriptFunctions(@"
$script:responses = @('" + encryptionMode + @"', 'TrustServerCertificate')
$script:responseIndex = 0
function Read-Host { return $script:responses[$script:responseIndex++] }
Read-McpTlsSettings -PromptEncryptionMode -PromptCertificateValidation
Write-Output 'Unexpected success'
");

                Assert.AreNotEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Error, "requires EncryptionMode Mandatory");
                Assert.IsFalse(result.Output.Contains("Unexpected success"), result.Output);
            }
        }

        [TestMethod]
        public void InteractiveTlsSettings_AllLaunchPaths_ForwardSelectedPolicy()
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunScriptFunctions(@"
$directCalls = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -eq 'Get-DirectMcpServerRegistration'
}, $true))
if ($directCalls.Count -ne 3) { throw 'Expected both automated paths and the interactive all-tools path.' }
$registrationCalls = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -eq 'Invoke-ForceRegistrationSync'
}, $true))
foreach ($call in @($directCalls) + @($registrationCalls)) {
    if ($call.Extent.Text -notmatch '-EncryptionMode \$EncryptionMode' -or
        $call.Extent.Text -notmatch '-CertificateValidation \$CertificateValidation') {
        throw 'A launch path does not forward the selected TLS policy.'
    }
}
$promptCalls = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -eq 'Read-McpTlsSettings'
}, $true))
if (-not ($promptCalls | Where-Object {
    $_.Extent.Text.Contains(""-not `$PSBoundParameters.ContainsKey('EncryptionMode')"") -and
    $_.Extent.Text.Contains(""-not `$PSBoundParameters.ContainsKey('CertificateValidation')"") -and
    $_.Extent.Text.Contains('-not $Tool')
})) { throw 'Interactive prompts must respect explicit TLS settings and single-tool mode.' }
Write-Output 'TLS forwarding verified.'
");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "TLS forwarding verified.");
            }
        }

        [DataTestMethod]
        [DataRow("Out", 2)]
        [DataRow("Error", 1)]
        public void McpRequests_LargeRequestAndStartupOutput_CompletesWithoutPipeDeadlock(
            string startupStream, int expectedResponseCount)
        {
            using (var fixture = new ForceRegistrationFixture())
            {
                ProcessResult result = fixture.RunScriptFunctions(@"
$childScript = @'
$ErrorActionPreference = 'Stop'
# Bound the regression failure even if a synchronous pipe write blocks.
Add-Type -TypeDefinition 'public static class PipeTestWatchdog {
    public static readonly System.Threading.Timer Timer = new System.Threading.Timer(
        state => System.Environment.Exit(9), null, 10000, System.Threading.Timeout.Infinite);
}'
$null = [PipeTestWatchdog]::Timer
$message = 'd' * 65536
$startup = if ('" + startupStream + @"' -eq 'Out') {
    @{ jsonrpc = '2.0'; method = 'notifications/message'; params = @{ message = $message } } |
        ConvertTo-Json -Depth 4 -Compress
} else { $message }
[Console]::" + startupStream + @".WriteLine($startup)
$inputText = [Console]::In.ReadToEnd()
$request = $inputText | ConvertFrom-Json
@{ jsonrpc = '2.0'; id = $request.id; result = @{
    method = $request.method; payloadLength = $request.params.message.Length
} } | ConvertTo-Json -Depth 4 -Compress
'@
$encodedChild = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($childScript))
$responses = @(Invoke-McpRequests -Command (Join-Path $PSHOME 'powershell.exe') `
    -Arguments @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encodedChild) `
    -Requests @(@{ jsonrpc = '2.0'; id = 1; method = 'tools/call'; params = @{ message = 'r' * 65536 } }))
if ($responses.Count -ne " + expectedResponseCount + @") { throw 'Unexpected response count.' }
$response = $responses[-1]
if ($response.id -ne 1 -or $response.result.method -ne 'tools/call' -or
    $response.result.payloadLength -ne 65536) { throw 'Request or response was truncated.' }
Write-Output 'Large redirected-pipe exchange completed.'
");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "Large redirected-pipe exchange completed.");
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

        private static ProcessResult RunValidationWithArguments(string scriptArguments, string scriptPath = null)
        {
            string script = scriptPath ?? Path.Combine(FindRepositoryRoot(), "SqlNexus.McpServer", "Test-McpServer.ps1");
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
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                return new ProcessResult(
                    process.ExitCode,
                    outputTask.GetAwaiter().GetResult(),
                    errorTask.GetAwaiter().GetResult());
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

        private sealed class ForceRegistrationFixture : IDisposable
        {
            private readonly string root;
            private readonly string testScriptPath;

            public ForceRegistrationFixture()
            {
                root = Path.Combine(Path.GetTempPath(), "SqlNexusForceRegistrationTest_" + Guid.NewGuid().ToString("N"));
                string installRoot = Path.Combine(root, "SQL Nexus Release");
                string mcpDir = Path.Combine(installRoot, "SqlNexus.McpServer");
                string copilotIntegrationDir = Path.Combine(installRoot, "CopilotIntegration");
                FakeUserProfile = Path.Combine(root, "UserProfile");
                FakeAppData = Path.Combine(root, "AppData");
                string vsCodeUserData = Path.Combine(FakeAppData, "Code", "User");
                string copilotHome = Path.Combine(FakeUserProfile, ".copilot");

                Directory.CreateDirectory(mcpDir);
                Directory.CreateDirectory(copilotIntegrationDir);
                Directory.CreateDirectory(vsCodeUserData);
                Directory.CreateDirectory(copilotHome);

                string repositoryRoot = FindRepositoryRoot();
                testScriptPath = Path.Combine(mcpDir, "Test-McpServer.ps1");
                File.Copy(Path.Combine(repositoryRoot, "SqlNexus.McpServer", "Test-McpServer.ps1"), testScriptPath);
                File.Copy(
                    Path.Combine(repositoryRoot, "CopilotIntegration", "Register-SqlNexusCopilotIntegration.ps1"),
                    Path.Combine(copilotIntegrationDir, "Register-SqlNexusCopilotIntegration.ps1"));

                NewFakeExecutable = Path.Combine(mcpDir, "SqlNexus.McpServer.exe");
                File.WriteAllText(NewFakeExecutable, "new fake executable");

                OldFakeExecutable = Path.Combine(root, "Old Install", "SqlNexus.McpServer", "SqlNexus.McpServer.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(OldFakeExecutable));
                File.WriteAllText(OldFakeExecutable, "old fake executable");

                VsCodeConfigPath = Path.Combine(vsCodeUserData, "mcp.json");
                CopilotConfigPath = Path.Combine(copilotHome, "mcp-config.json");
            }

            public string NewFakeExecutable { get; }
            public string OldFakeExecutable { get; }
            public string VsCodeConfigPath { get; }
            public string CopilotConfigPath { get; }
            public string FakeUserProfile { get; }
            public string FakeAppData { get; }

            public ProcessResult RunScriptFunctions(string body)
            {
                string harnessScript = Path.Combine(root, "Test-ScriptFunctions.ps1");
                File.WriteAllText(harnessScript, @"
param([string]$ScriptPath, [string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw ($parseErrors.Message -join '; ') }
foreach ($function in $ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
}, $true)) {
    . ([scriptblock]::Create($function.Extent.Text))
}
" + body);
                return RunValidationWithArguments(
                    "-ScriptPath " + Quote(testScriptPath) + " -ExecutablePath " + Quote(NewFakeExecutable),
                    harnessScript);
            }

            public ProcessResult RunConfigurationReader(string tlsArguments = null)
            {
                if (tlsArguments != null)
                {
                    string escapedExecutable = NewFakeExecutable.Replace("\\", "\\\\");
                    File.WriteAllText(VsCodeConfigPath,
                        "{\"servers\":{\"sqlnexus_mcp\":{\"command\":\"" + escapedExecutable
                        + "\",\"args\":[\"--server\",\"localhost\",\"--database\",\"SqlNexus\""
                        + tlsArguments + "]}}}");
                }

                string readerScript = Path.Combine(root, "Read-Configuration.ps1");
                File.WriteAllText(readerScript, @"
param([string]$ScriptPath, [string]$ConfigurationPath)
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($ScriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw ($parseErrors.Message -join '; ') }
$reader = $ast.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Read-McpConfigurationEntry'
}, $true)
if ($null -eq $reader) { throw 'Read-McpConfigurationEntry was not found.' }
. ([scriptblock]::Create($reader.Extent.Text))
$entry = Read-McpConfigurationEntry -Path $ConfigurationPath
ConvertTo-Json -InputObject $entry.Arguments -Compress
");
                return RunValidationWithArguments(
                    "-ScriptPath " + Quote(testScriptPath) + " -ConfigurationPath " + Quote(VsCodeConfigPath),
                    readerScript);
            }

            public void SeedExistingRegistration(string executablePath, string server, string database, string policy)
            {
                string escapedExecutable = executablePath.Replace("\\", "\\\\");
                string vscodeJson = "{\"servers\":{\"sqlnexus_mcp\":{\"type\":\"stdio\",\"command\":\""
                    + escapedExecutable + "\",\"args\":[\"--server\",\"" + server + "\",\"--database\",\"" + database
                    + "\",\"--trusted-connection\",\"true\",\"--elevated-principal-policy\",\"" + policy + "\"]}}}";
                File.WriteAllText(VsCodeConfigPath, vscodeJson);

                string copilotJson = "{\"mcpServers\":{\"sqlnexus_mcp\":{\"type\":\"stdio\",\"command\":\""
                    + escapedExecutable + "\",\"args\":[\"--server\",\"" + server + "\",\"--database\",\"" + database
                    + "\",\"--trusted-connection\",\"true\",\"--elevated-principal-policy\",\"" + policy + "\"]}}}";
                File.WriteAllText(CopilotConfigPath, copilotJson);
            }

            public ProcessResult RunForceRegistration(
                bool nonInteractive,
                string server = null,
                string database = null,
                string extraArguments = null,
                string stdinInput = null)
            {
                string arguments = "-ForceRegistration";
                if (!string.IsNullOrEmpty(server))
                {
                    arguments += " -Server " + Quote(server);
                }
                if (!string.IsNullOrEmpty(database))
                {
                    arguments += " -Database " + Quote(database);
                }
                if (nonInteractive)
                {
                    arguments += " -NonInteractive";
                }
                if (!string.IsNullOrEmpty(extraArguments))
                {
                    arguments += " " + extraArguments;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe"),
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(testScriptPath) + " " + arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };
                // $HOME and $env:APPDATA are resolved once per PowerShell session (from USERPROFILE/
                // APPDATA respectively); isolate this child process's registration lookups from the
                // real user profile by overriding both before the new process starts.
                startInfo.EnvironmentVariables["USERPROFILE"] = FakeUserProfile;
                startInfo.EnvironmentVariables["APPDATA"] = FakeAppData;

                using (Process process = Process.Start(startInfo))
                {
                    if (stdinInput != null)
                    {
                        process.StandardInput.Write(stdinInput);
                    }
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return new ProcessResult(process.ExitCode, output, error);
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
