[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Server = "localhost",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Database = "SqlNexus",

    [Parameter()]
    [string] $InstallRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter()]
    [string] $CopilotHome = (Join-Path $HOME ".copilot"),

    [Parameter()]
    [string] $VsCodeUserData = (Join-Path $env:APPDATA "Code\User"),

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $McpOnly
)

$ErrorActionPreference = "Stop"
$serverName = "sqlnexus_mcp"
$mcpExecutable = Join-Path $InstallRoot "SqlNexus.McpServer\SqlNexus.McpServer.exe"
$agentSource = Join-Path $InstallRoot ".github\agents\sql-nexus-diagnostic.agent.md"
$skillsDirectory = Join-Path $InstallRoot "AI\Skills"
$copilotConfigPath = Join-Path $CopilotHome "mcp-config.json"
$vscodeConfigPath = Join-Path $VsCodeUserData "mcp.json"
$agentDestination = Join-Path $CopilotHome "agents\sql-nexus-diagnostic.agent.md"

function Write-Status {
    param(
        [ValidateSet("INFO", "ERROR")]
        [string] $Level,
        [string] $Message
    )

    $color = if ($Level -eq "ERROR") { "Red" } else { "Cyan" }
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message" -ForegroundColor $color
}

trap {
    Write-Host ""
    Write-Status -Level "ERROR" -Message "SQL Nexus Copilot integration was not registered."
    Write-Status -Level "ERROR" -Message $_.Exception.Message
    exit 1
}

function Read-JsonConfiguration {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{}
    }

    $content = [System.IO.File]::ReadAllText($Path)
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [pscustomobject]@{}
    }

    try {
        $configuration = $content | ConvertFrom-Json
    }
    catch {
        throw "Cannot register SQL Nexus because '$Path' does not contain valid JSON. $($_.Exception.Message)"
    }

    if ($null -eq $configuration -or $configuration -is [System.Array]) {
        throw "Cannot register SQL Nexus because '$Path' must contain a JSON object."
    }

    return $configuration
}

function Get-OrAddObjectProperty {
    param(
        [object] $Object,
        [string] $Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $value = [pscustomobject]@{}
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $value
        return $value
    }

    if ($null -eq $property.Value -or $property.Value -is [System.Array] -or $property.Value -isnot [psobject]) {
        throw "Cannot register SQL Nexus because the '$Name' property must be a JSON object."
    }

    return $property.Value
}

function Set-ServerEntry {
    param(
        [object] $Container,
        [string] $Name,
        [object] $Entry
    )

    $property = $Container.PSObject.Properties[$Name]
    if ($null -ne $property) {
        $existingJson = $property.Value | ConvertTo-Json -Depth 20 -Compress
        $expectedJson = $Entry | ConvertTo-Json -Depth 20 -Compress
        if (-not $Force) {
            if ($existingJson -ne $expectedJson) {
                throw "An MCP server named '$Name' is already configured differently. Re-run with -Force to replace only that entry."
            }

            return $false
        }

        $property.Value = $Entry
        return $true
    }

    $Container | Add-Member -MemberType NoteProperty -Name $Name -Value $Entry
    return $true
}

function Write-JsonConfiguration {
    param(
        [string] $Path,
        [object] $Configuration
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $temporaryPath = "$Path.$PID.tmp"
    $json = ($Configuration | ConvertTo-Json -Depth 20) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($temporaryPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

$requiredPaths = @($mcpExecutable)
if (-not $McpOnly) {
    $requiredPaths += @($agentSource, $skillsDirectory)
}

foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "The SQL Nexus installation is incomplete. Required path not found: $requiredPath"
    }
}

$arguments = @("--server", $Server, "--database", $Database, "--trusted-connection", "true")
$vscodeEntry = [pscustomobject][ordered]@{
    type = "stdio"
    command = $mcpExecutable
    args = $arguments
}
$copilotEntry = [pscustomobject][ordered]@{
    type = "stdio"
    command = $mcpExecutable
    args = $arguments
    env = [pscustomobject]@{}
    tools = @("*")
}

$vscodeConfiguration = Read-JsonConfiguration -Path $vscodeConfigPath
$copilotConfiguration = Read-JsonConfiguration -Path $copilotConfigPath
$vscodeServers = Get-OrAddObjectProperty -Object $vscodeConfiguration -Name "servers"
$copilotServers = Get-OrAddObjectProperty -Object $copilotConfiguration -Name "mcpServers"
$vscodeChanged = Set-ServerEntry -Container $vscodeServers -Name $serverName -Entry $vscodeEntry
$copilotChanged = Set-ServerEntry -Container $copilotServers -Name $serverName -Entry $copilotEntry

$agentChanged = $false
if (-not $McpOnly) {
    $agentContent = [System.IO.File]::ReadAllText($agentSource)
    $portableSkillsPath = $skillsDirectory.Replace('\', '/').TrimEnd('/') + "/"
    $agentContent = $agentContent.Replace("AI/Skills/", $portableSkillsPath)
    $agentChanged = $true
    if ((Test-Path -LiteralPath $agentDestination -PathType Leaf) -and -not $Force) {
        $existingAgentContent = [System.IO.File]::ReadAllText($agentDestination)
        if ($existingAgentContent -ne $agentContent) {
            throw "A different SQL Nexus diagnostic agent already exists at '$agentDestination'. Re-run with -Force to replace it."
        }

        $agentChanged = $false
    }
}

if ($vscodeChanged) {
    Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
}
if ($copilotChanged) {
    Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration
}

if ($agentChanged) {
    $agentDirectory = Split-Path -Parent $agentDestination
    if (-not (Test-Path -LiteralPath $agentDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($agentDestination, $agentContent, (New-Object System.Text.UTF8Encoding($false)))
}

if ($McpOnly -and -not $vscodeChanged -and -not $copilotChanged) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server is already registered. Existing matching settings were not replaced."
}
elseif ($McpOnly) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server registered. The custom agent was not changed."
}
elseif (-not $vscodeChanged -and -not $copilotChanged -and -not $agentChanged) {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration is already registered. Existing matching settings were not replaced."
}
else {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration registered."
}
Write-Status -Level "INFO" -Message "MCP executable: $mcpExecutable"
Write-Status -Level "INFO" -Message "SQL Server: $Server"
Write-Status -Level "INFO" -Message "Database: $Database"
Write-Status -Level "INFO" -Message "Authentication: Windows Integrated Authentication"
Write-Status -Level "INFO" -Message "VS Code MCP configuration: $vscodeConfigPath"
Write-Status -Level "INFO" -Message "Copilot CLI MCP configuration: $copilotConfigPath"
if (-not $McpOnly) {
    Write-Status -Level "INFO" -Message "Custom agent: $agentDestination"
}