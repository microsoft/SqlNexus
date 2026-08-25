[CmdletBinding()]
param(
    [Parameter()]
    [string] $CopilotHome = (Join-Path $HOME ".copilot"),

    [Parameter()]
    [string] $VsCodeUserData = (Join-Path $env:APPDATA "Code\User")
)

$ErrorActionPreference = "Stop"
$serverName = "sqlnexus_mcp"
$copilotConfigPath = Join-Path $CopilotHome "mcp-config.json"
$vscodeConfigPath = Join-Path $VsCodeUserData "mcp.json"
$agentDestination = Join-Path $CopilotHome "agents\sql-nexus-diagnostic.agent.md"

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

$vscodeConfiguration = Read-JsonConfiguration -Path $vscodeConfigPath
$copilotConfiguration = Read-JsonConfiguration -Path $copilotConfigPath

if (Remove-ServerEntry -Configuration $vscodeConfiguration -ContainerName "servers" -Name $serverName) {
    Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
}
if (Remove-ServerEntry -Configuration $copilotConfiguration -ContainerName "mcpServers" -Name $serverName) {
    Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration
}
if (Test-Path -LiteralPath $agentDestination -PathType Leaf) {
    Remove-Item -LiteralPath $agentDestination -Force
}

Write-Output "SQL Nexus Copilot integration unregistered."
Write-Output "Other VS Code and Copilot CLI configuration entries were preserved."