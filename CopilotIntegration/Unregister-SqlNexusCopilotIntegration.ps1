[CmdletBinding()]
param(
    [Parameter()]
    [string] $CopilotHome = (Join-Path $HOME ".copilot"),

    [Parameter()]
    [string] $VsCodeUserData = (Join-Path $env:APPDATA "Code\User"),

    [Parameter()]
    [switch] $McpOnly,

    # Optional overrides. By default the server and user are read from the registered entry.
    [Parameter()]
    [string] $Server,

    [Parameter()]
    [string] $UserId,

    [Parameter()]
    [string] $McpExecutablePath,

    # Keep the stored SQL credential (for example when another registration shares it).
    [Parameter()]
    [switch] $KeepCredential
)

$ErrorActionPreference = "Stop"
$serverName = "sqlnexus_mcp"
$copilotConfigPath = Join-Path $CopilotHome "mcp-config.json"
$vscodeConfigPath = Join-Path $VsCodeUserData "mcp.json"
$agentDestination = Join-Path $CopilotHome "agents\sql-nexus-diagnostic.agent.md"

function Write-Status {
    param(
        [ValidateSet("INFO", "WARNING", "ERROR")]
        [string] $Level,
        [string] $Message
    )

    $color = switch ($Level) { "ERROR" { "Red" } "WARNING" { "Yellow" } default { "Cyan" } }
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message" -ForegroundColor $color
}

trap {
    Write-Host ""
    Write-Status -Level "ERROR" -Message "SQL Nexus Copilot integration was not unregistered."
    Write-Status -Level "ERROR" -Message $_.Exception.Message
    exit 1
}

function Read-JsonConfiguration {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $content = [System.IO.File]::ReadAllText($Path)
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [pscustomobject]@{}
    }

    try {
        $configuration = $content | ConvertFrom-Json
    }
    catch {
        throw "Cannot unregister SQL Nexus because '$Path' does not contain valid JSON. $($_.Exception.Message)"
    }

    if ($null -eq $configuration -or $configuration -is [System.Array]) {
        throw "Cannot unregister SQL Nexus because '$Path' must contain a JSON object."
    }

    return $configuration
}

function Remove-ServerEntry {
    param(
        [object] $Configuration,
        [string] $ContainerName,
        [string] $Name
    )

    if ($null -eq $Configuration) {
        return $false
    }

    $containerProperty = $Configuration.PSObject.Properties[$ContainerName]
    if ($null -eq $containerProperty -or $null -eq $containerProperty.Value) {
        return $false
    }

    $serverProperty = $containerProperty.Value.PSObject.Properties[$Name]
    if ($null -eq $serverProperty) {
        return $false
    }

    $containerProperty.Value.PSObject.Properties.Remove($Name)
    return $true
}

function Write-JsonConfiguration {
    param(
        [string] $Path,
        [object] $Configuration
    )

    $temporaryPath = "$Path.$PID.tmp"
    $json = ($Configuration | ConvertTo-Json -Depth 20) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($temporaryPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function ConvertTo-ProcessArgument {
    param([string] $Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

$vscodeConfiguration = Read-JsonConfiguration -Path $vscodeConfigPath
$copilotConfiguration = Read-JsonConfiguration -Path $copilotConfigPath

function Get-ArgumentValue {
    param([object[]] $Arguments, [string] $Name)

    for ($i = 0; $i -lt $Arguments.Count - 1; $i++) {
        if ([string]$Arguments[$i] -eq $Name) { return [string]$Arguments[$i + 1] }
    }

    return $null
}

# Returns the SQL-authentication identity of a registered entry, or $null for Windows authentication.
function Get-SqlAuthEntryIdentity {
    param([object] $Entry)

    $argumentsProperty = $Entry.PSObject.Properties["args"]
    if ($null -eq $argumentsProperty -or $null -eq $argumentsProperty.Value) { return $null }
    $entryArguments = @($argumentsProperty.Value)
    if ((Get-ArgumentValue -Arguments $entryArguments -Name "--trusted-connection") -ne "false") { return $null }

    $entryServer = Get-ArgumentValue -Arguments $entryArguments -Name "--server"
    $entryUser = Get-ArgumentValue -Arguments $entryArguments -Name "--user-id"
    if ([string]::IsNullOrWhiteSpace($entryServer) -or [string]::IsNullOrWhiteSpace($entryUser)) { return $null }

    $commandProperty = $Entry.PSObject.Properties["command"]
    return [pscustomobject]@{
        Server = $entryServer.Trim()
        UserId = $entryUser.Trim()
        Command = if ($null -ne $commandProperty) { [string]$commandProperty.Value } else { $null }
    }
}

function Get-RegisteredEntries {
    param([object] $Configuration, [string] $ContainerName)

    if ($null -eq $Configuration) { return @() }
    $containerProperty = $Configuration.PSObject.Properties[$ContainerName]
    if ($null -eq $containerProperty -or $null -eq $containerProperty.Value) { return @() }
    return @($containerProperty.Value.PSObject.Properties)
}

# Works out which stored credential belongs to the registration being removed, before the entries
# are deleted. Returns $null for Windows authentication or when nothing identifies a credential.
$credentialTarget = $null
if (-not $KeepCredential) {
    if (-not [string]::IsNullOrWhiteSpace($Server) -or -not [string]::IsNullOrWhiteSpace($UserId)) {
        if ([string]::IsNullOrWhiteSpace($Server) -or [string]::IsNullOrWhiteSpace($UserId)) {
            throw "-Server and -UserId must be specified together."
        }
        $credentialTarget = [pscustomobject]@{ Server = $Server.Trim(); UserId = $UserId.Trim(); Command = $null }
    }
    else {
        $registered = @(
            (Get-RegisteredEntries -Configuration $copilotConfiguration -ContainerName "mcpServers"),
            (Get-RegisteredEntries -Configuration $vscodeConfiguration -ContainerName "servers")
        ) | ForEach-Object { $_ } | Where-Object { $_.Name -eq $serverName }
        foreach ($entry in $registered) {
            $identity = Get-SqlAuthEntryIdentity -Entry $entry.Value
            if ($null -ne $identity) { $credentialTarget = $identity; break }
        }
    }

    # Another (user-created) entry may use the same credential; deleting it would break that entry.
    if ($null -ne $credentialTarget) {
        $others = @(
            (Get-RegisteredEntries -Configuration $copilotConfiguration -ContainerName "mcpServers"),
            (Get-RegisteredEntries -Configuration $vscodeConfiguration -ContainerName "servers")
        ) | ForEach-Object { $_ } | Where-Object { $_.Name -ne $serverName }
        foreach ($other in $others) {
            $otherIdentity = Get-SqlAuthEntryIdentity -Entry $other.Value
            if ($null -ne $otherIdentity -and
                $otherIdentity.Server -ieq $credentialTarget.Server -and
                $otherIdentity.UserId -ieq $credentialTarget.UserId) {
                Write-Status -Level "WARNING" -Message "The stored SQL credential for user '$($credentialTarget.UserId)' on '$($credentialTarget.Server)' is also used by the MCP entry '$($other.Name)' and was kept."
                $credentialTarget = $null
                break
            }
        }
    }
}

if (Remove-ServerEntry -Configuration $vscodeConfiguration -ContainerName "servers" -Name $serverName) {
    Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
}
if (Remove-ServerEntry -Configuration $copilotConfiguration -ContainerName "mcpServers" -Name $serverName) {
    Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration
}
if (-not $McpOnly -and (Test-Path -LiteralPath $agentDestination -PathType Leaf)) {
    Remove-Item -LiteralPath $agentDestination -Force
}

if ($null -ne $credentialTarget) {
    $mcpExecutable = $McpExecutablePath
    if ([string]::IsNullOrWhiteSpace($mcpExecutable) -and -not [string]::IsNullOrWhiteSpace($credentialTarget.Command)) {
        # The command comes from a user-writable configuration file; only run it if it is the MCP server.
        if ([System.IO.Path]::GetFileName($credentialTarget.Command) -like "SqlNexus.McpServer.*") {
            $mcpExecutable = $credentialTarget.Command
        }
        else {
            Write-Status -Level "WARNING" -Message "The registered command '$($credentialTarget.Command)' is not SqlNexus.McpServer and was not run. Using the default MCP server location instead."
        }
    }
    if ([string]::IsNullOrWhiteSpace($mcpExecutable)) {
        $mcpExecutable = Join-Path (Split-Path -Parent $PSScriptRoot) "SqlNexus.McpServer\SqlNexus.McpServer.exe"
    }

    $manualHint = "Run '--delete-credential --server `"$($credentialTarget.Server)`" --user-id `"$($credentialTarget.UserId)`"' with SqlNexus.McpServer.exe, or delete the matching file under '$env:LOCALAPPDATA\SqlNexus\McpCredentials'."
    if (-not (Test-Path -LiteralPath $mcpExecutable -PathType Leaf)) {
        Write-Status -Level "WARNING" -Message "The MCP server executable was not found at '$mcpExecutable', so the stored SQL credential was NOT deleted. $manualHint"
    }
    else {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $mcpExecutable
        $startInfo.Arguments = (@("--delete-credential", "--server", $credentialTarget.Server, "--user-id", $credentialTarget.UserId) |
            ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " "
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = $null
        $deleteOutput = ""
        $deleteError = ""
        $deleteExitCode = 1
        try {
            $process = [System.Diagnostics.Process]::Start($startInfo)
            $deleteOutput = $process.StandardOutput.ReadToEnd().Trim()
            $deleteError = $process.StandardError.ReadToEnd().Trim()
            $process.WaitForExit()
            $deleteExitCode = $process.ExitCode
        }
        catch {
            $deleteError = "The MCP server executable could not be started: $($_.Exception.Message)"
        }
        finally {
            if ($null -ne $process) { $process.Dispose() }
        }

        if ($deleteExitCode -ne 0) {
            Write-Host ""
            Write-Status -Level "ERROR" -Message "The MCP server entries were removed, but the stored SQL credential could not be deleted. $deleteError"
            Write-Status -Level "ERROR" -Message $manualHint
            exit 1
        }

        Write-Status -Level "INFO" -Message $deleteOutput
    }
}

if ($McpOnly) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server unregistered. The custom agent was preserved."
}
else {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration unregistered."
}
Write-Status -Level "INFO" -Message "Other VS Code and Copilot CLI configuration entries were preserved."