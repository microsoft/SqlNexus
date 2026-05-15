# Quick wrapper to query SQL Nexus MCP Server
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(
        'list_tables',
        'analyze_cpu',
        'top_cpu_queries',
        'analyze_waits',
        'analyze_blocking',
        'performance_summary'
    )]
    [string]$Tool
)

$exePath = Join-Path $PSScriptRoot "bin\Release\SqlNexus.McpServer.exe"
$server = "localhost\SQLEXPRESS"
$database = "NexusDiagnosticsTest"

# Map friendly names to actual tool names
$toolMap = @{
    'list_tables' = 'list_nexus_tables'
    'analyze_cpu' = 'analyze_cpu_usage'
    'top_cpu_queries' = 'get_top_cpu_queries'
    'analyze_waits' = 'analyze_wait_stats'
    'analyze_blocking' = 'analyze_blocking'
    'performance_summary' = 'get_performance_summary'
}

$actualTool = $toolMap[$Tool]

# Create MCP request
$initRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "PowerShell"
            version = "1.0"
        }
    }
} | ConvertTo-Json -Depth 10 -Compress

$toolRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/call"
    params = @{
        name = $actualTool
        arguments = @{}
    }
} | ConvertTo-Json -Depth 10 -Compress

# Send requests
$input = "$initRequest`n$toolRequest"
$result = $input | & $exePath --server $server --database $database --trusted-connection true 2>&1

# Parse and display result
$lines = $result -split "`n"
$jsonLine = $lines | Where-Object { $_ -match '^\{.*"result"' } | Select-Object -Last 1

if ($jsonLine) {
    $response = $jsonLine | ConvertFrom-Json
    if ($response.result.content) {
        $response.result.content[0].text | ConvertFrom-Json | ConvertTo-Json -Depth 10
    }
} else {
    Write-Host "Raw output:" -ForegroundColor Yellow
    $result
}
