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
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$serverName = "sqlnexus_mcp"
$mcpExecutable = Join-Path $InstallRoot "SqlNexus.McpServer\SqlNexus.McpServer.exe"
$agentSource = Join-Path $InstallRoot ".github\agents\sql-nexus-diagnostic.agent.md"
$skillsDirectory = Join-Path $InstallRoot "AI\Skills"
$copilotConfigPath = Join-Path $CopilotHome "mcp-config.json"
$vscodeConfigPath = Join-Path $VsCodeUserData "mcp.json"
$agentDestination = Join-Path $CopilotHome "agents\sql-nexus-diagnostic.agent.md"

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
        if (-not $Force -and $existingJson -ne $expectedJson) {
            throw "An MCP server named '$Name' is already configured differently. Re-run with -Force to replace only that entry."
        }

        $property.Value = $Entry
        return
    }

    $Container | Add-Member -MemberType NoteProperty -Name $Name -Value $Entry
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

foreach ($requiredPath in @($mcpExecutable, $agentSource, $skillsDirectory)) {
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
Set-ServerEntry -Container $vscodeServers -Name $serverName -Entry $vscodeEntry
Set-ServerEntry -Container $copilotServers -Name $serverName -Entry $copilotEntry

$agentContent = [System.IO.File]::ReadAllText($agentSource)
$portableSkillsPath = $skillsDirectory.Replace('\', '/').TrimEnd('/') + "/"
$agentContent = $agentContent.Replace("AI/Skills/", $portableSkillsPath)
if ((Test-Path -LiteralPath $agentDestination -PathType Leaf) -and -not $Force) {
    $existingAgentContent = [System.IO.File]::ReadAllText($agentDestination)
    if ($existingAgentContent -ne $agentContent) {
        throw "A different SQL Nexus diagnostic agent already exists at '$agentDestination'. Re-run with -Force to replace it."
    }
}

Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration

$agentDirectory = Split-Path -Parent $agentDestination
if (-not (Test-Path -LiteralPath $agentDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
}
[System.IO.File]::WriteAllText($agentDestination, $agentContent, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "SQL Nexus Copilot integration registered."
Write-Output "VS Code MCP configuration: $vscodeConfigPath"
Write-Output "Copilot CLI MCP configuration: $copilotConfigPath"
Write-Output "Custom agent: $agentDestination"