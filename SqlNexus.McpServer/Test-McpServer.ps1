# SQL Nexus MCP Server Test Script
# SQL Nexus MCP Server Test Script
# This script helps validate your MCP server setup
[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet(
        'list_tables',
        'analyze_cpu',
        'top_cpu_queries',
        'analyze_waits',
        'analyze_blocking',
        'performance_summary'
    )]
    [string]$Tool,

    [Parameter(Mandatory=$false)]
    [string]$Server,

    [Parameter(Mandatory=$false)]
    [string]$Database,

    [Parameter(Mandatory=$false)]
    [string]$Database2,

    [Parameter(Mandatory=$false)]
    [ValidateSet('true','false','True','False')]
    [string]$TrustedConnection,

    [Parameter(Mandatory=$false)]
    [string]$UserId,

    [Parameter(Mandatory=$false)]
    [string]$Password,

    [Parameter(Mandatory=$false)]
    [ValidateSet('Block','ImpersonateReader')]
    [string]$ElevatedPrincipalPolicy,

    [Parameter(Mandatory=$false)]
    [string]$ExecutablePath,

    [Parameter(Mandatory=$false)]
    [switch]$UseRegisteredConfiguration,

    [Parameter(Mandatory=$false)]
    [string]$ConfigurationPath,

    [Parameter(Mandatory=$false)]
    [switch]$All,

    [Parameter(Mandatory=$false)]
    [switch]$NonInteractive,

    [Parameter(Mandatory=$false)]
    [switch]$SkipIfNotConfigured,

    [Parameter(Mandatory=$false)]
    [switch]$ForceRegistration
)

$ErrorActionPreference = "Stop"

function Read-McpConfigurationEntry {
    param([Parameter(Mandatory=$true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "MCP configuration file not found: $Path"
    }

    try {
        $configuration = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    }
    catch {
        throw "MCP configuration '$Path' does not contain valid JSON. $($_.Exception.Message)"
    }

    $entry = $null
    if ($null -ne $configuration.servers) {
        $entry = $configuration.servers.sqlnexus_mcp
    }
    if ($null -eq $entry -and $null -ne $configuration.mcpServers) {
        $entry = $configuration.mcpServers.sqlnexus_mcp
    }
    if ($null -eq $entry) {
        throw "MCP configuration '$Path' does not contain a 'sqlnexus_mcp' server entry."
    }
    if ($entry.type -and $entry.type -ne "stdio") {
        throw "The registered 'sqlnexus_mcp' server must use type 'stdio'."
    }
    if ([string]::IsNullOrWhiteSpace([string]$entry.command)) {
        throw "The registered 'sqlnexus_mcp' server does not specify a command."
    }

    $command = [string]$entry.command
    if ([System.IO.Path]::GetFileName($command) -ne "SqlNexus.McpServer.exe") {
        throw "The registered 'sqlnexus_mcp' command must point to SqlNexus.McpServer.exe."
    }
    if (-not (Test-Path -LiteralPath $command -PathType Leaf)) {
        throw "The registered MCP executable was not found: $command"
    }

    $arguments = @($entry.args | ForEach-Object { [string]$_ })
    $knownArguments = @{
        "--server" = $true
        "--database" = $true
        "--database2" = $true
        "--database-for-comparison" = $true
        "--database_for_comparison" = $true
        "--trusted-connection" = $true
        "--elevated-principal-policy" = $true
    }
    $seenArguments = @{}
    for ($index = 0; $index -lt $arguments.Count; $index += 2) {
        $name = $arguments[$index]
        if (-not $knownArguments.ContainsKey($name)) {
            throw "The registered 'sqlnexus_mcp' entry contains an unsupported argument '$name'."
        }
        if ($seenArguments.ContainsKey($name)) {
            throw "The registered 'sqlnexus_mcp' entry contains duplicate argument '$name'."
        }
        if ($index + 1 -ge $arguments.Count -or [string]::IsNullOrWhiteSpace($arguments[$index + 1])) {
            throw "The registered 'sqlnexus_mcp' argument '$name' requires a value."
        }

        $seenArguments[$name] = $arguments[$index + 1]
    }
    if (-not $seenArguments.ContainsKey("--server") -or -not $seenArguments.ContainsKey("--database")) {
        throw "The registered 'sqlnexus_mcp' entry must specify --server and --database."
    }
    if ($seenArguments.ContainsKey("--trusted-connection") -and $seenArguments["--trusted-connection"] -notin @("true", "false")) {
        throw "The registered --trusted-connection value must be 'true' or 'false'."
    }
    if ($seenArguments.ContainsKey("--elevated-principal-policy") -and $seenArguments["--elevated-principal-policy"] -notin @("block", "impersonate-reader")) {
        throw "The registered --elevated-principal-policy value is not supported."
    }

    return [pscustomobject]@{
        Command = $command
        Arguments = $arguments
        HasComparisonDatabase = $seenArguments.ContainsKey("--database2") -or
            $seenArguments.ContainsKey("--database-for-comparison") -or
            $seenArguments.ContainsKey("--database_for_comparison")
        Fingerprint = $command + "`n" + ($arguments -join "`n")
        Path = $Path
    }
}

function Get-RegisteredMcpConfigurationEntry {
    param([string]$Path)

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return Read-McpConfigurationEntry -Path $Path
    }

    $candidatePaths = @(
        (Join-Path $env:APPDATA "Code\User\mcp.json"),
        (Join-Path $HOME ".copilot\mcp-config.json")
    )
    $entries = @()
    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            try {
                $entries += Read-McpConfigurationEntry -Path $candidatePath
            }
            catch {
                if ($_.Exception.Message -notlike "*does not contain a 'sqlnexus_mcp' server entry*") {
                    throw
                }
            }
        }
    }
    if ($entries.Count -eq 0) {
        throw "No registered 'sqlnexus_mcp' entry was found. Run Register-SqlNexusCopilotIntegration.ps1 first or provide -ConfigurationPath."
    }
    if ($entries.Count -gt 1 -and $entries[0].Fingerprint -ne $entries[1].Fingerprint) {
        throw "The VS Code and Copilot CLI 'sqlnexus_mcp' registrations differ. Specify the configuration to test with -ConfigurationPath."
    }

    return $entries[0]
}

function Invoke-ForceRegistrationSync {
    param(
        [Parameter(Mandatory=$true)][string]$ExecutablePath,
        [string]$Server,
        [string]$Database,
        [string]$Database2,
        [string]$ElevatedPrincipalPolicy,
        [string]$TrustedConnection,
        [string]$ConfigurationPath,
        [switch]$NonInteractive
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfigurationPath)) {
        # Register-SqlNexusCopilotIntegration.ps1 always writes to its own default (or -CopilotHome/
        # -VsCodeUserData-derived) locations; it has no way to target an arbitrary -ConfigurationPath
        # file. Diffing against that file while writing somewhere else would silently register to the
        # wrong place, so refuse rather than guess.
        throw ("-ForceRegistration does not support -ConfigurationPath; it always registers to the default " +
            "VS Code (`$env:APPDATA\Code\User\mcp.json) and Copilot CLI (`$HOME\.copilot\mcp-config.json) locations.")
    }

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "Cannot force-register: the MCP server executable was not found: $ExecutablePath. Build it with 'dotnet build -c Release' first."
    }

    $registerScript = Join-Path (Split-Path -Parent $ExecutablePath) "..\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1"
    if (-not (Test-Path -LiteralPath $registerScript -PathType Leaf)) {
        # Script directory and exe directory are the same in the staged/release layout; try that too.
        $registerScript = Join-Path (Split-Path -Parent $PSCommandPath) "..\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1"
    }
    if (-not (Test-Path -LiteralPath $registerScript -PathType Leaf)) {
        throw "Cannot force-register: Register-SqlNexusCopilotIntegration.ps1 was not found next to this installation."
    }

    # Collect the values to register. We never trust/preserve values from an existing registration --
    # a stale registration could point at the wrong server/database just as easily as the wrong exe.
    $resolvedServer = $Server
    $resolvedDatabase = $Database
    $resolvedPolicy = if ([string]::IsNullOrWhiteSpace($ElevatedPrincipalPolicy)) { "Block" } else { $ElevatedPrincipalPolicy }
    if ([string]::IsNullOrWhiteSpace($resolvedServer) -or [string]::IsNullOrWhiteSpace($resolvedDatabase)) {
        if ($NonInteractive) {
            throw "Cannot force-register in -NonInteractive mode without -Server and -Database."
        }
        if ([string]::IsNullOrWhiteSpace($resolvedServer)) {
            $resolvedServer = Read-Host "  SQL Server instance to register [localhost]"
            if ([string]::IsNullOrWhiteSpace($resolvedServer)) { $resolvedServer = "localhost" }
        }
        if ([string]::IsNullOrWhiteSpace($resolvedDatabase)) {
            $resolvedDatabase = Read-Host "  SQL Nexus database to register [SqlNexus]"
            if ([string]::IsNullOrWhiteSpace($resolvedDatabase)) { $resolvedDatabase = "SqlNexus" }
        }
        if (-not $PSBoundParameters.ContainsKey('ElevatedPrincipalPolicy') -or [string]::IsNullOrWhiteSpace($ElevatedPrincipalPolicy)) {
            $inputPolicy = Read-Host "  Elevated principal policy to register (Block/ImpersonateReader) [Block]"
            if (-not [string]::IsNullOrWhiteSpace($inputPolicy)) {
                if ($inputPolicy -notin @('Block', 'ImpersonateReader')) {
                    Write-Host "  Invalid value '$inputPolicy'; using 'Block'." -ForegroundColor Yellow
                } else {
                    $resolvedPolicy = $inputPolicy
                }
            }
        }
    }

    $newEntry = [pscustomobject]@{
        Command = $ExecutablePath
        Server = $resolvedServer
        Database = $resolvedDatabase
        Database2 = $Database2
        Policy = $resolvedPolicy
    }

    $existingEntry = $null
    try {
        $existingRegistration = Get-RegisteredMcpConfigurationEntry -Path $ConfigurationPath
        $existingServerIndex = [Array]::IndexOf($existingRegistration.Arguments, "--server")
        $existingDatabaseIndex = [Array]::IndexOf($existingRegistration.Arguments, "--database")
        $existingPolicyIndex = [Array]::IndexOf($existingRegistration.Arguments, "--elevated-principal-policy")
        $existingEntry = [pscustomobject]@{
            Command = $existingRegistration.Command
            Server = if ($existingServerIndex -ge 0) { $existingRegistration.Arguments[$existingServerIndex + 1] } else { "<unknown>" }
            Database = if ($existingDatabaseIndex -ge 0) { $existingRegistration.Arguments[$existingDatabaseIndex + 1] } else { "<unknown>" }
            Policy = if ($existingPolicyIndex -ge 0) { $existingRegistration.Arguments[$existingPolicyIndex + 1] } else { "block" }
        }
    }
    catch {
        # No existing registration (or it is invalid/ambiguous) -- nothing to diff against or confirm.
        Write-Host "No existing 'sqlnexus_mcp' registration found; registering for the first time." -ForegroundColor Cyan
    }

    if ($null -ne $existingEntry) {
        Write-Host ""
        Write-Host "Existing registration:" -ForegroundColor Yellow
        Write-Host "  Command:  $($existingEntry.Command)"
        Write-Host "  Server:   $($existingEntry.Server)"
        Write-Host "  Database: $($existingEntry.Database)"
        Write-Host "  Policy:   $($existingEntry.Policy)"
        Write-Host "New registration (this folder):" -ForegroundColor Yellow
        Write-Host "  Command:  $($newEntry.Command)"
        Write-Host "  Server:   $($newEntry.Server)"
        Write-Host "  Database: $($newEntry.Database)"
        Write-Host "  Policy:   $($newEntry.Policy)"
        Write-Host "The existing registration will be fully replaced (not merged) if you proceed." -ForegroundColor Yellow
        Write-Host ""

        if (-not $NonInteractive) {
            $choice = Read-Host "Keep existing registration or overwrite with the values above? (K)eep/(O)verwrite [K]"
            if ([string]::IsNullOrWhiteSpace($choice) -or $choice -notmatch '^(o|overwrite)$') {
                Write-Host "Keeping the existing registration unchanged." -ForegroundColor Cyan
                return $false
            }
        }
        else {
            Write-Host "Running -NonInteractive with -ForceRegistration: overwriting the existing registration automatically." -ForegroundColor Yellow
        }
    }

    # Use a hashtable (not a flat string array) for splatting: splatting a flat array onto another
    # PowerShell script binds parameters POSITIONALLY, so "-Name" strings embedded in a plain array
    # do not bind by name the way they would as literal command-line arguments to a native .exe.
    $registerArgs = @{
        Server = $newEntry.Server
        Database = $newEntry.Database
        ElevatedPrincipalPolicy = $newEntry.Policy
        McpExecutablePath = $newEntry.Command
        McpOnly = $true
        Force = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($newEntry.Database2)) {
        $registerArgs["Database2"] = $newEntry.Database2
    }

    $registerArgsDisplay = ($registerArgs.GetEnumerator() | ForEach-Object { "-$($_.Key) $($_.Value)" }) -join ' '
    Write-Host "Running: $registerScript $registerArgsDisplay" -ForegroundColor Cyan

    # Register-SqlNexusCopilotIntegration.ps1 only calls 'exit 1' on failure (via its trap); it has
    # no 'exit 0' on success, so $LASTEXITCODE can be stale on success. Reset it first so a nonzero
    # value afterward can only have come from this specific invocation, and also rely on
    # $ErrorActionPreference = "Stop" (set at the top of this script) to catch any terminating error
    # that escapes before the script's own trap/exit runs.
    $global:LASTEXITCODE = 0
    & $registerScript @registerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Register-SqlNexusCopilotIntegration.ps1 failed with exit code $LASTEXITCODE."
    }

    return $true
}

function Resolve-DefaultMcpServerExecutablePath {
    param([Parameter(Mandatory=$true)][string]$ScriptDirectory)

    # Dev/source-tree layout: SqlNexus.McpServer\Test-McpServer.ps1 next to bin\Release\SqlNexus.McpServer.exe.
    $devLayoutPath = Join-Path $ScriptDirectory "bin\Release\SqlNexus.McpServer.exe"
    if (Test-Path -LiteralPath $devLayoutPath -PathType Leaf) {
        return $devLayoutPath
    }

    # Staged/release layout: StageSqlNexusAiFiles flattens SqlNexus.McpServer\bin\$(Configuration)\*
    # directly into sqlnexus\bin\$(Configuration)\SqlNexus.McpServer\, so the exe sits right next to
    # this script with no nested bin\Release folder.
    $stagedLayoutPath = Join-Path $ScriptDirectory "SqlNexus.McpServer.exe"
    if (Test-Path -LiteralPath $stagedLayoutPath -PathType Leaf) {
        return $stagedLayoutPath
    }

    # Nothing found at either known location; return the dev-layout path so error messages stay
    # consistent with the documented 'dotnet build -c Release' guidance.
    return $devLayoutPath
}

function Get-DirectMcpServerRegistration {
    param(
        [Parameter(Mandatory=$true)][string]$Server,
        [Parameter(Mandatory=$true)][string]$Database,
        [string]$Database2,
        [string]$ExecutablePath,
        [string]$TrustedConnection,
        [string]$ElevatedPrincipalPolicy
    )

    $command = if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) {
        $ExecutablePath
    } else {
        Resolve-DefaultMcpServerExecutablePath -ScriptDirectory (Split-Path -Parent $PSCommandPath)
    }

    if (-not (Test-Path -LiteralPath $command -PathType Leaf)) {
        throw "The MCP server executable was not found: $command. Build it with 'dotnet build -c Release' or pass -ExecutablePath."
    }

    $trustedConnectionArg = if ([string]::IsNullOrWhiteSpace($TrustedConnection)) { "true" } else { $TrustedConnection.ToLowerInvariant() }
    $policyArg = if ($ElevatedPrincipalPolicy -eq "ImpersonateReader") { "impersonate-reader" } else { "block" }

    $arguments = @(
        "--server", $Server,
        "--database", $Database,
        "--trusted-connection", $trustedConnectionArg,
        "--elevated-principal-policy", $policyArg
    )
    if (-not [string]::IsNullOrWhiteSpace($Database2)) {
        $arguments += @("--database2", $Database2)
    }

    return [pscustomobject]@{
        Command = $command
        Arguments = $arguments
        HasComparisonDatabase = -not [string]::IsNullOrWhiteSpace($Database2)
        Fingerprint = $command + "`n" + ($arguments -join "`n")
        Path = "$Server/$Database (direct)"
    }
}

function Get-ProcessFailureDetail {
    param(
        [Parameter(Mandatory=$true)][string]$Diagnostics,
        [Parameter(Mandatory=$true)][int]$ExitCode,
        [Parameter(Mandatory=$true)][AllowEmptyCollection()][AllowEmptyString()][string[]]$StdOutLines
    )

    if ([string]::IsNullOrWhiteSpace($Diagnostics)) {
        return "The MCP server exited before returning any JSON-RPC output. The process returned exit code $ExitCode and produced no stderr diagnostics."
    }

    $normalised = $Diagnostics.Trim()
    $fatalPatterns = @(
        '\[ERROR\]',
        'Fatal error',
        'Unhandled',
        'Exception',
        'StackTrace',
        'InvalidOperationException',
        'SqlException',
        'ArgumentException'
    )
    if ($fatalPatterns | Where-Object { $normalised -match $_ }) {
        return "The MCP server exited with code $ExitCode. Stderr diagnostics: $normalised"
    }

    $startupBanner = @($normalised -split "`r?`n" | Where-Object { $_ -match '\[INFO\]|

\[WARN\]|=+' -or [string]::IsNullOrWhiteSpace($_) })
    if ($startupBanner.Count -gt 0 -and $StdOutLines.Count -eq 0) {
        return "The MCP server exited before returning any JSON-RPC output. This is often a startup-validation failure; inspect %TEMP%\sqlnexus_mcpserver.log for the actual error. Startup diagnostics: $normalised"
    }

    return "The MCP server exited with code $ExitCode. Stderr diagnostics: $normalised"
}

function Quote-ProcessArgument {
    param([Parameter(Mandatory=$true)][string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-McpRequests {
    param(
        [Parameter(Mandatory=$true)][string]$Command,
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [Parameter(Mandatory=$true)][object[]]$Requests
    )

    $requestPayload = @($Requests | ForEach-Object { $_ | ConvertTo-Json -Depth 20 -Compress }) -join [Environment]::NewLine
    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $Command
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardInput = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.CreateNoWindow = $true
    $processInfo.Arguments = (($Arguments | ForEach-Object { Quote-ProcessArgument -Value $_ }) -join ' ')

    $process = [System.Diagnostics.Process]::Start($processInfo)
    try {
        $process.StandardInput.Write($requestPayload)
        $process.StandardInput.WriteLine()
        $process.StandardInput.Close()

        $stdOutTask = $process.StandardOutput.ReadToEndAsync()
        $stdErrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            throw "The MCP server did not respond within 30 seconds. Check the startup log at %TEMP%\sqlnexus_mcpserver.log and the registered server configuration."
        }

        $stdout = $stdOutTask.GetAwaiter().GetResult()
        $stderr = $stdErrTask.GetAwaiter().GetResult()
    }
    finally {
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }

    $exitCode = $process.ExitCode
    $diagnosticLines = @($stderr -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    foreach ($diagnosticLine in $diagnosticLines) {
        Write-Verbose "MCP server: $diagnosticLine"
    }
    $diagnostics = $diagnosticLines -join [Environment]::NewLine

    if ($exitCode -ne 0) {
        throw (Get-ProcessFailureDetail -Diagnostics $diagnostics -ExitCode $exitCode -StdOutLines @($stdout -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }))
    }

    $responseLines = @($stdout -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($responseLines.Count -eq 0) {
        throw (Get-ProcessFailureDetail -Diagnostics $diagnostics -ExitCode 0 -StdOutLines $responseLines)
    }

    $responses = @()
    foreach ($responseLine in $responseLines) {
        $text = [string]$responseLine
        if ([string]::IsNullOrWhiteSpace($text)) {
            continue
        }
        try {
            $responses += $text | ConvertFrom-Json
        }
        catch {
            $detail = if ([string]::IsNullOrWhiteSpace($diagnostics)) { "" } else { " MCP diagnostics: $diagnostics" }
            throw "The MCP server returned a non-JSON stdout response: $text.$detail"
        }
    }

    return $responses
}

function Get-ValidationArguments {
    param([Parameter(Mandatory=$true)][string]$ToolName)

    switch ($ToolName) {
        "get_top_queries_by_duration" { return @{ top_n = 1 } }
        "get_top_cpu_queries" { return @{ top_n = 1 } }
        "analyze_io_performance" { return @{ threshold_ms = 20 } }
        "get_waits_for_query" { return @{ hash_id = 0 } }
        "get_missing_indexes" { return @{ top_n = 1 } }
        "query_nexus_database" { return @{ query = "SELECT 1 AS validation_value" } }
        "get_query_execution_details" { return @{ hash_id = 0 } }
        "get_statements_in_batch" { return @{ batch_seq = 0 } }
        "get_top_queries_by_reads" { return @{ top_n = 1 } }
        "get_top_queries_by_writes" { return @{ top_n = 1 } }
        "get_error_log_summary" { return @{ top_n = 1 } }
        default { return @{} }
    }
}

function Invoke-AllMcpTools {
    param([Parameter(Mandatory=$true)]$Registration)

    $serverIndex = [Array]::IndexOf($Registration.Arguments, "--server")
    $databaseIndex = [Array]::IndexOf($Registration.Arguments, "--database")
    $registeredServer = if ($serverIndex -ge 0 -and $serverIndex + 1 -lt $Registration.Arguments.Count) { $Registration.Arguments[$serverIndex + 1] } else { "<unknown>" }
    $registeredDatabase = if ($databaseIndex -ge 0 -and $databaseIndex + 1 -lt $Registration.Arguments.Count) { $Registration.Arguments[$databaseIndex + 1] } else { "<unknown>" }

    Write-Host ""
    Write-Host "MCP server executable: $($Registration.Command)" -ForegroundColor Cyan
    if (Test-Path -LiteralPath $Registration.Command -PathType Leaf) {
        $exeItem = Get-Item -LiteralPath $Registration.Command
        $exeVersion = $exeItem.VersionInfo.FileVersion
        if ([string]::IsNullOrWhiteSpace($exeVersion)) { $exeVersion = "(no version resource)" }
        Write-Host "  File version: $exeVersion | Last built: $($exeItem.LastWriteTime)" -ForegroundColor Cyan
    }
    else {
        Write-Host "  WARNING: the registered executable path does not exist." -ForegroundColor Red
    }
    Write-Host "  Target server/database: $registeredServer / $registeredDatabase" -ForegroundColor Cyan

    $policyIndex = [Array]::IndexOf($Registration.Arguments, "--elevated-principal-policy")
    $registeredPolicy = if ($policyIndex -ge 0 -and $policyIndex + 1 -lt $Registration.Arguments.Count) { $Registration.Arguments[$policyIndex + 1] } else { "block" }
    Write-Host "Configured elevated principal policy: $registeredPolicy" -ForegroundColor Cyan
    if ($registeredPolicy -eq "impersonate-reader") {
        Write-Host "  MCP queries execute as the non-revertible, loginless SqlNexusMcpReader database principal." -ForegroundColor Gray
    }

    $initializeRequest = [ordered]@{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = [ordered]@{
            protocolVersion = "2024-11-05"
            capabilities = @{}
            clientInfo = @{ name = "sqlnexus-integration-test"; version = "1.0" }
        }
    }
    $listRequest = [ordered]@{ jsonrpc = "2.0"; id = 2; method = "tools/list"; params = @{} }
    $catalogResponses = @(Invoke-McpRequests -Command $Registration.Command -Arguments $Registration.Arguments -Requests @($initializeRequest, $listRequest))
    $listResponse = $catalogResponses | Where-Object { $_.id -eq 2 } | Select-Object -First 1
    if ($null -eq $listResponse -or $null -ne $listResponse.error -or $null -eq $listResponse.result.tools) {
        throw "The MCP server did not return a valid tools/list response."
    }

    $tools = @($listResponse.result.tools)
    if ($tools.Count -eq 0) {
        throw "The MCP server did not advertise any tools."
    }

    $requests = @($initializeRequest)
    $toolByRequestId = @{}
    $skippedTools = @()
    $requestId = 100
    foreach ($tool in $tools) {
        $toolName = [string]$tool.name
        if ([string]::IsNullOrWhiteSpace($toolName)) {
            throw "The MCP server advertised a tool without a name."
        }
        if ($toolName -eq "compare_nexus_databases" -and -not $Registration.HasComparisonDatabase) {
            $skippedTools += $toolName
            continue
        }

        $requests += [ordered]@{
            jsonrpc = "2.0"
            id = $requestId
            method = "tools/call"
            params = [ordered]@{
                name = $toolName
                arguments = Get-ValidationArguments -ToolName $toolName
            }
        }
        $toolByRequestId[$requestId] = $toolName
        $requestId++
    }

    $responses = @(Invoke-McpRequests -Command $Registration.Command -Arguments $Registration.Arguments -Requests $requests)
    $failedTools = @()
    foreach ($entry in $toolByRequestId.GetEnumerator() | Sort-Object Key) {
        $response = $responses | Where-Object { $_.id -eq $entry.Key } | Select-Object -First 1
        if ($null -eq $response) {
            Write-Host "[FAIL] $($entry.Value): no response" -ForegroundColor Red
            $failedTools += $entry.Value
        }
        elseif ($null -ne $response.error) {
            Write-Host "[FAIL] $($entry.Value): $($response.error.message)" -ForegroundColor Red
            $failedTools += $entry.Value
        }
        else {
            Write-Host "[PASS] $($entry.Value)" -ForegroundColor Green
        }
    }
    foreach ($skippedTool in $skippedTools) {
        Write-Host "[SKIP] ${skippedTool}: no comparison database is registered" -ForegroundColor Yellow
    }

    Write-Host "Validated $($toolByRequestId.Count) tool(s); failed: $($failedTools.Count); skipped: $($skippedTools.Count)."
    if ($failedTools.Count -gt 0) {
        throw "MCP tool validation failed for: $($failedTools -join ', ')"
    }
}

$requestedAutomatedValidation = -not $Tool -and (
    $UseRegisteredConfiguration -or
    -not [string]::IsNullOrWhiteSpace($ConfigurationPath) -or
    (-not [string]::IsNullOrWhiteSpace($Server) -and -not [string]::IsNullOrWhiteSpace($Database))
)

# -ForceRegistration always targets the registered configuration (mcp.json / mcp-config.json),
# never direct mode, since direct mode never touches those files in the first place.
if ($ForceRegistration -and -not $Tool) {
    try {
        $exePathForRegistration = if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) {
            $ExecutablePath
        } else {
            Resolve-DefaultMcpServerExecutablePath -ScriptDirectory (Split-Path -Parent $PSCommandPath)
        }

        Invoke-ForceRegistrationSync -ExecutablePath $exePathForRegistration -Server $Server -Database $Database `
            -Database2 $Database2 -ElevatedPrincipalPolicy $ElevatedPrincipalPolicy -TrustedConnection $TrustedConnection `
            -ConfigurationPath $ConfigurationPath -NonInteractive:$NonInteractive | Out-Null
    }
    catch {
        Write-Host "MCP registration sync failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    if (-not $All) {
        exit 0
    }

    # Proceed to validate against the (now freshly written) registered configuration.
    $UseRegisteredConfiguration = $true
    $requestedAutomatedValidation = $true
}

if ($requestedAutomatedValidation -and -not $All) {
    Write-Host "-UseRegisteredConfiguration, -ConfigurationPath, and -Server/-Database validation currently require -All." -ForegroundColor Red
    exit 1
}

if ($requestedAutomatedValidation -and $All) {
    try {
        $usingDirectMode = -not $UseRegisteredConfiguration -and [string]::IsNullOrWhiteSpace($ConfigurationPath)

        if ($usingDirectMode) {
            # Direct mode: caller supplies -Server/-Database explicitly (e.g. a sibling repo's
            # integration tests right after importing data), without requiring MCP client
            # registration (mcp.json / mcp-config.json) to exist on this machine.
            if ($SkipIfNotConfigured) {
                try {
                    $registration = Get-DirectMcpServerRegistration -Server $Server -Database $Database -Database2 $Database2 `
                        -ExecutablePath $ExecutablePath -TrustedConnection $TrustedConnection -ElevatedPrincipalPolicy $ElevatedPrincipalPolicy
                }
                catch {
                    Write-Host "MCP server not available; skipping MCP tool validation (-SkipIfNotConfigured). $($_.Exception.Message)" -ForegroundColor Yellow
                    exit 0
                }
            }
            else {
                $registration = Get-DirectMcpServerRegistration -Server $Server -Database $Database -Database2 $Database2 `
                    -ExecutablePath $ExecutablePath -TrustedConnection $TrustedConnection -ElevatedPrincipalPolicy $ElevatedPrincipalPolicy
            }
        }
        else {
            if ($SkipIfNotConfigured) {
                try {
                    $registration = Get-RegisteredMcpConfigurationEntry -Path $ConfigurationPath
                }
                catch {
                    Write-Host "MCP configuration not available; skipping MCP tool validation (-SkipIfNotConfigured). $($_.Exception.Message)" -ForegroundColor Yellow
                    exit 0
                }
            }
            else {
                $registration = Get-RegisteredMcpConfigurationEntry -Path $ConfigurationPath
            }
        }

        Write-Host "Testing MCP configuration: $($registration.Path)" -ForegroundColor Cyan
        Invoke-AllMcpTools -Registration $registration
        exit 0
    }
    catch {
        Write-Host "MCP integration validation failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($SkipIfNotConfigured) {
            Write-Host "Not treating this as a skip: the MCP server/configuration was found but validation failed." -ForegroundColor Red
        }
        exit 1
    }
}

if ($NonInteractive -or $All) {
    Write-Host "-NonInteractive and -All require -UseRegisteredConfiguration, -ConfigurationPath, or both -Server and -Database." -ForegroundColor Red
    exit 1
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  SQL Nexus MCP Server - Configuration Test" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usage:" -ForegroundColor Yellow
Write-Host "  .\Test-McpServer.ps1" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Tool top_cpu_queries -Server localhost -Database SqlNexus" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Tool analyze_waits -TrustedConnection false -UserId sqlnexus_reader -Password <password>" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Tool analyze_waits -ElevatedPrincipalPolicy ImpersonateReader" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -UseRegisteredConfiguration -All -NonInteractive" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Server localhost -Database SqlNexus -All -NonInteractive" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Server localhost -Database SqlNexus -All -NonInteractive -SkipIfNotConfigured" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -ForceRegistration -Server localhost -Database SqlNexus" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -ForceRegistration -Server localhost -Database SqlNexus -All -NonInteractive" -ForegroundColor Gray
Write-Host "  Note: TrustedConnection defaults to True unless -TrustedConnection is specified." -ForegroundColor Gray
Write-Host "  Note: ElevatedPrincipalPolicy defaults to Block (matching registration) unless -ElevatedPrincipalPolicy is specified." -ForegroundColor Gray
Write-Host "  Note: -Server/-Database with -All validates directly without requiring MCP client registration." -ForegroundColor Gray
Write-Host "  Note: -SkipIfNotConfigured exits 0 (instead of failing) when the MCP server/configuration is not available." -ForegroundColor Gray
Write-Host "  Note: -ForceRegistration registers (or re-registers) this folder's SqlNexus.McpServer.exe as 'sqlnexus_mcp'," -ForegroundColor Gray
Write-Host "        replacing any existing registration. Prompts to confirm interactively; auto-overwrites with -NonInteractive." -ForegroundColor Gray
Write-Host ""

$toolMap = @{
    'list_tables' = 'list_nexus_tables'
    'analyze_cpu' = 'analyze_cpu_usage'
    'top_cpu_queries' = 'get_top_cpu_queries'
    'analyze_waits' = 'analyze_wait_stats'
    'analyze_blocking' = 'analyze_blocking'
    'performance_summary' = 'get_performance_summary'
}

# Check if .NET SDK is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -eq 0) {
    Write-Host "  .NET SDK Version: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  .NET SDK not found. Please install .NET SDK (required to build/run this net48 project tooling)." -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check if MCP Server executable exists
Write-Host "Checking MCP Server executable..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Resolve-DefaultMcpServerExecutablePath -ScriptDirectory $scriptDir
if (Test-Path $exePath) {
    Write-Host "  MCP Server found: $exePath" -ForegroundColor Green
} else {
    Write-Host "  MCP Server not found. Run 'dotnet build -c Release' first" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Prompt for server and database with defaults from appsettings.json
$configPath = Join-Path $scriptDir "appsettings.json"
$defaultServer = "localhost"
$defaultDatabase = "SqlNexus"
$defaultTrusted = $true
$defaultElevatedPrincipalPolicy = "Block"
$configuredUserId = $env:SqlNexus__UserId
$configuredPassword = $env:SqlNexus__Password

if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    if ($config.SqlNexus.Server)   { $defaultServer   = $config.SqlNexus.Server }
    if ($config.SqlNexus.Database) { $defaultDatabase = $config.SqlNexus.Database }
    if ($config.SqlNexus.ElevatedPrincipalPolicy) { $defaultElevatedPrincipalPolicy = $config.SqlNexus.ElevatedPrincipalPolicy }
    if ([string]::IsNullOrWhiteSpace($configuredUserId) -and $config.SqlNexus.UserId) {
        $configuredUserId = $config.SqlNexus.UserId
    }
    if ([string]::IsNullOrWhiteSpace($configuredPassword) -and $config.SqlNexus.Password) {
        $configuredPassword = $config.SqlNexus.Password
    }
}

if ([string]::IsNullOrWhiteSpace($UserId)) { $UserId = $configuredUserId }
if ([string]::IsNullOrWhiteSpace($Password)) { $Password = $configuredPassword }

$effectiveElevatedPrincipalPolicy = if ($PSBoundParameters.ContainsKey('ElevatedPrincipalPolicy')) { $ElevatedPrincipalPolicy } else { $defaultElevatedPrincipalPolicy }
$elevatedPrincipalPolicyArg = if ($effectiveElevatedPrincipalPolicy -eq "ImpersonateReader") { "impersonate-reader" } else { "block" }

$serverName = $defaultServer
$databaseName = $defaultDatabase

if ($Tool) {
    if ($Server) { $serverName = $Server }
    if ($Database) { $databaseName = $Database }
} else {
    Write-Host "Connection Settings (press Enter to accept defaults):" -ForegroundColor Yellow
    if ($Server) {
        $serverName = $Server
    } else {
        $inputServer = Read-Host "  SQL Server instance [localhost]"
        if ($inputServer) { $serverName = $inputServer }
    }

    if ($Database) {
        $databaseName = $Database
    } else {
        $inputDatabase = Read-Host "  SQL Nexus database  [$defaultDatabase]"
        if ($inputDatabase) { $databaseName = $inputDatabase }
    }

    if (-not $PSBoundParameters.ContainsKey('ElevatedPrincipalPolicy')) {
        $inputPolicy = Read-Host "  Elevated principal policy (Block/ImpersonateReader) [$effectiveElevatedPrincipalPolicy]"
        if ($inputPolicy) {
            if ($inputPolicy -notin @('Block', 'ImpersonateReader')) {
                Write-Host "  Invalid value '$inputPolicy'; keeping '$effectiveElevatedPrincipalPolicy'." -ForegroundColor Yellow
            } else {
                $effectiveElevatedPrincipalPolicy = $inputPolicy
                $elevatedPrincipalPolicyArg = if ($effectiveElevatedPrincipalPolicy -eq "ImpersonateReader") { "impersonate-reader" } else { "block" }
            }
        }
    }
}

$trustedConnection = $defaultTrusted
if ($PSBoundParameters.ContainsKey('TrustedConnection')) {
    $trustedConnection = [System.Convert]::ToBoolean($TrustedConnection)
}
Write-Host "  Using: $serverName / $databaseName" -ForegroundColor Green
Write-Host "  Trusted connection: $trustedConnection" -ForegroundColor Green
Write-Host "  Elevated principal policy: $effectiveElevatedPrincipalPolicy" -ForegroundColor Green
if ($effectiveElevatedPrincipalPolicy -eq "ImpersonateReader") {
    Write-Host "    MCP queries execute as the non-revertible, loginless SqlNexusMcpReader database principal." -ForegroundColor Gray
}
Write-Host ""

# Attempt to test SQL connection
Write-Host "Testing SQL Server connection..." -ForegroundColor Yellow
try {
    if ($trustedConnection) {
        $connString = "Server=$serverName;Database=$databaseName;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5"
    } else {
        if (-not [string]::IsNullOrWhiteSpace($UserId) -and -not [string]::IsNullOrWhiteSpace($Password)) {
            $connString = "Server=$serverName;Database=$databaseName;User ID=$UserId;Password=$Password;TrustServerCertificate=true;Connect Timeout=5"
        } else {
            Write-Host "  SQL Authentication configured; no -UserId/-Password supplied, skipping direct connection test" -ForegroundColor Cyan
            $connString = $null
        }
    }

    if ($connString) {
        $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
        $connection.Open()
        $connection.Close()
        Write-Host "  Successfully connected to $serverName/$databaseName" -ForegroundColor Green
    }
} catch {
    Write-Host "  Could not connect to SQL Server: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "    Please verify server name, database name, and authentication" -ForegroundColor Gray
}
Write-Host ""

# Helper: sends MCP requests to the server and displays the data rows as a table
function Invoke-McpTool {
    param([string[]]$Messages)

    $exeArgs = @(
        "--server", $serverName,
        "--database", $databaseName,
        "--trusted-connection", $trustedConnection.ToString().ToLowerInvariant(),
        "--elevated-principal-policy", $elevatedPrincipalPolicyArg
    )

    $originalUserId = $env:SqlNexus__UserId
    $originalPassword = $env:SqlNexus__Password
    $setCredentials = (-not $trustedConnection) -and (-not [string]::IsNullOrWhiteSpace($UserId)) -and (-not [string]::IsNullOrWhiteSpace($Password))

    try {
        if ($setCredentials) {
            $env:SqlNexus__UserId = $UserId
            $env:SqlNexus__Password = $Password
        }

        $rawLines = $Messages -join "`n" | & $exePath @exeArgs
    }
    finally {
        if ($setCredentials) {
            if ($null -eq $originalUserId) { Remove-Item Env:SqlNexus__UserId -ErrorAction SilentlyContinue } else { $env:SqlNexus__UserId = $originalUserId }
            if ($null -eq $originalPassword) { Remove-Item Env:SqlNexus__Password -ErrorAction SilentlyContinue } else { $env:SqlNexus__Password = $originalPassword }
        }
    }

    foreach ($line in $rawLines) {
        try {
            $rpc = $line | ConvertFrom-Json
            if ($rpc.error) {
                Write-Host "  Error: $($rpc.error.message)" -ForegroundColor Red
                continue
            }
            if ($rpc.result.content) {
                $inner = $rpc.result.content[0].text | ConvertFrom-Json
                Write-Host "  $($inner.summary)  [rows: $($inner.row_count)]" -ForegroundColor Cyan
                if ($inner.row_count -gt 0) {
                    $inner.data | Format-Table -AutoSize | Out-String | Write-Host
                } else {
                    Write-Host "  (no data)" -ForegroundColor Gray
                }
            }
        } catch {
            Write-Host "  Invalid MCP response: $($_.Exception.Message)" -ForegroundColor Red
            throw
        }
    }
}

function Invoke-McpToolByAlias {
    param([string]$ToolAlias)

    if (-not $toolMap.ContainsKey($ToolAlias)) {
        Write-Host "Unknown tool alias: $ToolAlias" -ForegroundColor Red
        return
    }

    $actualTool = $toolMap[$ToolAlias]
    Write-Host "Testing tool: $ToolAlias ($actualTool)" -ForegroundColor Cyan
    Invoke-McpTool @(
      '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}',
      ('{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"' + $actualTool + '","arguments":{}}}')
    )
}

if ($Tool) {
    Invoke-McpToolByAlias -ToolAlias $Tool
    exit 0
}

$exit = $false
while (-not $exit) {
    Write-Host "Select an option to test MCP Server functionality:" -ForegroundColor Yellow
    Write-Host "  1. Test Top CPU Queries" -ForegroundColor White
    Write-Host "  2. Test Top Waits" -ForegroundColor White
    Write-Host "  3. Test Top Queries by Duration" -ForegroundColor White
    Write-Host "  4. Test List Nexus Tables" -ForegroundColor White
    Write-Host "  5. Test Blocking Analysis" -ForegroundColor White
    Write-Host "  6. Test Performance Summary" -ForegroundColor White
    Write-Host "  7. Change Elevated Principal Policy (current: $effectiveElevatedPrincipalPolicy)" -ForegroundColor White
    Write-Host "  8. Test All Tools Using Current Connection Settings" -ForegroundColor White
    Write-Host "  9. Force-Register This Folder's MCP Server (replaces existing registration)" -ForegroundColor White
    Write-Host "  10. Exit" -ForegroundColor White
    $choice = Read-Host "Enter your choice (1-10)"
    switch ($choice) {
        "1" {
            Write-Host "Testing Top CPU Queries..." -ForegroundColor Cyan
            Invoke-McpTool @(
              '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}',
              '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_top_cpu_queries","arguments":{"top_n":20}}}'
            )
        }
        "2" {
            Write-Host "Testing Top Waits..." -ForegroundColor Cyan
            Invoke-McpTool @(
              '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}',
              '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"analyze_wait_stats","arguments":{}}}'
            )
        }
        "3" {
            Write-Host "Testing Top Queries by Duration..." -ForegroundColor Cyan
            Invoke-McpTool @(
              '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}',
              '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_top_queries_by_duration","arguments":{"top_n":10}}}'
            )
        }
        "4" {
            Invoke-McpToolByAlias -ToolAlias 'list_tables'
        }
        "5" {
            Invoke-McpToolByAlias -ToolAlias 'analyze_blocking'
        }
        "6" {
            Invoke-McpToolByAlias -ToolAlias 'performance_summary'
        }
        "7" {
            $newPolicy = Read-Host "  Elevated principal policy (Block/ImpersonateReader) [$effectiveElevatedPrincipalPolicy]"
            if ($newPolicy) {
                if ($newPolicy -notin @('Block', 'ImpersonateReader')) {
                    Write-Host "  Invalid value '$newPolicy'; keeping '$effectiveElevatedPrincipalPolicy'." -ForegroundColor Yellow
                } else {
                    $effectiveElevatedPrincipalPolicy = $newPolicy
                    $elevatedPrincipalPolicyArg = if ($effectiveElevatedPrincipalPolicy -eq "ImpersonateReader") { "impersonate-reader" } else { "block" }
                    Write-Host "  Elevated principal policy set to '$effectiveElevatedPrincipalPolicy'." -ForegroundColor Green
                    if ($effectiveElevatedPrincipalPolicy -eq "ImpersonateReader") {
                        Write-Host "    MCP queries execute as the non-revertible, loginless SqlNexusMcpReader database principal." -ForegroundColor Gray
                    }
                }
            }
        }
        "8" {
            try {
                $registration = Get-DirectMcpServerRegistration -Server $serverName -Database $databaseName -Database2 $Database2 `
                    -ExecutablePath $exePath -TrustedConnection $trustedConnection.ToString() `
                    -ElevatedPrincipalPolicy $effectiveElevatedPrincipalPolicy
                Write-Host "Testing all tools using the current connection settings." -ForegroundColor Cyan
                Invoke-AllMcpTools -Registration $registration
            }
            catch {
                Write-Host "MCP integration validation failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        "9" {
            try {
                $exePathForRegistration = if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) { $ExecutablePath } else { $exePath }
                Invoke-ForceRegistrationSync -ExecutablePath $exePathForRegistration -Server $Server -Database $Database `
                    -Database2 $Database2 -ElevatedPrincipalPolicy $effectiveElevatedPrincipalPolicy -TrustedConnection $TrustedConnection `
                    -ConfigurationPath $ConfigurationPath | Out-Null
            }
            catch {
                Write-Host "MCP registration sync failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        "10" {
            Write-Host "Exiting test script. Goodbye!" -ForegroundColor Green
            $exit = $true
        }
        default {
            Write-Host "Invalid choice. Please enter a number between 1 and 10." -ForegroundColor Red
        }
    }
    Write-Host ""
}

# Summary
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Setup Status" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "MCP Server is built and ready to use!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Import SQLLogScout data into SQL Nexus database" -ForegroundColor White
Write-Host "  2. Configure mcp.json in VS Code or Copilot CLI" -ForegroundColor White
Write-Host "  3. Restart VS Code to load MCP server configuration" -ForegroundColor White
Write-Host "  4. Ask Copilot: 'Is there high CPU on this system?'" -ForegroundColor White
Write-Host ""
Write-Host "Documentation: README.md" -ForegroundColor Yellow
Write-Host ""
