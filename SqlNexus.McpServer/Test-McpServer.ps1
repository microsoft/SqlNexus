# SQL Nexus MCP Server Test Script
# This script helps validate your MCP server setup

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  SQL Nexus MCP Server - Configuration Test" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET 8.0 is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ? .NET SDK Version: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  ? .NET SDK not found. Please install .NET 8.0+" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check if MCP Server executable exists
Write-Host "Checking MCP Server executable..." -ForegroundColor Yellow
$exePath = "C:\GitRepos\SqlNexus\SqlNexus.McpServer\bin\Debug\net8.0\SqlNexus.McpServer.exe"
if (Test-Path $exePath) {
    Write-Host "  ? MCP Server found: $exePath" -ForegroundColor Green
} else {
    Write-Host "  ? MCP Server not found. Run 'dotnet build' first" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check configuration
Write-Host "Checking configuration..." -ForegroundColor Yellow
$configPath = "C:\GitRepos\SqlNexus\SqlNexus.McpServer\appsettings.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    Write-Host "  ? Configuration file found" -ForegroundColor Green
    Write-Host "    Server: $($config.SqlNexus.Server)" -ForegroundColor Gray
    Write-Host "    Database: $($config.SqlNexus.Database)" -ForegroundColor Gray
    Write-Host "    TrustedConnection: $($config.SqlNexus.TrustedConnection)" -ForegroundColor Gray
} else {
    Write-Host "  ? Configuration file not found" -ForegroundColor Red
    exit 1
}
Write-Host ""


# Attempt to test SQL connection
Write-Host "Testing SQL Server connection..." -ForegroundColor Yellow
try {
    $serverName = $config.SqlNexus.Server
    $databaseName = $config.SqlNexus.Database

    # Build connection string
    if ($config.SqlNexus.TrustedConnection) {
        $connString = "Server=$serverName;Database=$databaseName;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5"
    } else {
        Write-Host "  ? SQL Authentication configured - skipping connection test" -ForegroundColor Cyan
        $connString = $null
    }

    if ($connString) {
        $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
        $connection.Open()
        $connection.Close()
        Write-Host "  ? Successfully connected to $serverName/$databaseName" -ForegroundColor Green
    }
} catch {
    Write-Host "  ? Could not connect to SQL Server: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "    Please verify server name, database name, and authentication" -ForegroundColor Gray
}
Write-Host ""

# Summary
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Setup Status" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "? MCP Server is built and ready to use!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Update appsettings.json with your SQL Server details" -ForegroundColor White
Write-Host "  2. Import PSSDiag/SQLLogScout data into SQL Nexus database" -ForegroundColor White
Write-Host "  3. Restart VS Code to load MCP server configuration" -ForegroundColor White
Write-Host "  4. Ask Copilot: 'Is there high CPU on this system?'" -ForegroundColor White
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Yellow
Write-Host "  � README.md - Full documentation" -ForegroundColor White
Write-Host "  � GETTING_STARTED.md - Quick start guide" -ForegroundColor White
Write-Host "  � BUILD_SUMMARY.md - Project overview" -ForegroundColor White
Write-Host ""
