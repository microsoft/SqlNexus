# SQL Nexus MCP Server Test Script
# SQL Nexus MCP Server Test Script
# This script helps validate your MCP server setup
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
    [ValidateSet('true','false','True','False')]
    [string]$TrustedConnection,

    [Parameter(Mandatory=$false)]
    [string]$UserId,

    [Parameter(Mandatory=$false)]
    [string]$Password
)

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  SQL Nexus MCP Server - Configuration Test" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usage:" -ForegroundColor Yellow
Write-Host "  .\Test-McpServer.ps1" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Tool top_cpu_queries -Server localhost -Database SqlNexus" -ForegroundColor Gray
Write-Host "  .\Test-McpServer.ps1 -Tool analyze_waits -TrustedConnection false -UserId sqlnexus_reader -Password <password>" -ForegroundColor Gray
Write-Host "  Note: TrustedConnection defaults to True unless -TrustedConnection is specified." -ForegroundColor Gray
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
$exePath = Join-Path $scriptDir "bin\Release\SqlNexus.McpServer.exe"
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
$configuredUserId = $env:SqlNexus__UserId
$configuredPassword = $env:SqlNexus__Password

if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    if ($config.SqlNexus.Server)   { $defaultServer   = $config.SqlNexus.Server }
    if ($config.SqlNexus.Database) { $defaultDatabase = $config.SqlNexus.Database }
    if ([string]::IsNullOrWhiteSpace($configuredUserId) -and $config.SqlNexus.UserId) {
        $configuredUserId = $config.SqlNexus.UserId
    }
    if ([string]::IsNullOrWhiteSpace($configuredPassword) -and $config.SqlNexus.Password) {
        $configuredPassword = $config.SqlNexus.Password
    }
}

if ([string]::IsNullOrWhiteSpace($UserId)) { $UserId = $configuredUserId }
if ([string]::IsNullOrWhiteSpace($Password)) { $Password = $configuredPassword }

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
}

$trustedConnection = $defaultTrusted
if ($PSBoundParameters.ContainsKey('TrustedConnection')) {
    $trustedConnection = [System.Convert]::ToBoolean($TrustedConnection)
}
Write-Host "  Using: $serverName / $databaseName" -ForegroundColor Green
Write-Host "  Trusted connection: $trustedConnection" -ForegroundColor Green
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
        "--trusted-connection", $trustedConnection.ToString().ToLowerInvariant()
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
        } catch { }
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
    Write-Host "  7. Exit" -ForegroundColor White
    $choice = Read-Host "Enter your choice (1-7)"
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
            Write-Host "Exiting test script. Goodbye!" -ForegroundColor Green
            $exit = $true
        }
        default {
            Write-Host "Invalid choice. Please enter a number between 1 and 7." -ForegroundColor Red
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
