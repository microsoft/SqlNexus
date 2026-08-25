[CmdletBinding()]
param(
    [Parameter()]
    [string] $CopilotHome = (Join-Path $HOME ".copilot"),

    [Parameter()]
    [string] $VsCodeUserData = (Join-Path $env:APPDATA "Code\User"),

    [Parameter()]
    [switch] $McpOnly
)

$ErrorActionPreference = "Stop"
$serverName = "sqlnexus_mcp"
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

$vscodeConfiguration = Read-JsonConfiguration -Path $vscodeConfigPath
$copilotConfiguration = Read-JsonConfiguration -Path $copilotConfigPath

if (Remove-ServerEntry -Configuration $vscodeConfiguration -ContainerName "servers" -Name $serverName) {
    Write-JsonConfiguration -Path $vscodeConfigPath -Configuration $vscodeConfiguration
}
if (Remove-ServerEntry -Configuration $copilotConfiguration -ContainerName "mcpServers" -Name $serverName) {
    Write-JsonConfiguration -Path $copilotConfigPath -Configuration $copilotConfiguration
}
if (-not $McpOnly -and (Test-Path -LiteralPath $agentDestination -PathType Leaf)) {
    Remove-Item -LiteralPath $agentDestination -Force
}

if ($McpOnly) {
    Write-Status -Level "INFO" -Message "SQL Nexus MCP server unregistered. The custom agent was preserved."
}
else {
    Write-Status -Level "INFO" -Message "SQL Nexus Copilot integration unregistered."
}
Write-Status -Level "INFO" -Message "Other VS Code and Copilot CLI configuration entries were preserved."