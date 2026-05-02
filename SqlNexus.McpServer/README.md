# SQL Nexus MCP Server

A Model Context Protocol (MCP) server for AI-assisted analysis of SQL Nexus diagnostic databases.

## Build Status

? **Built Successfully** - `.NET Framework 4.8`  
?? **Executable**: `bin\Release\SqlNexus.McpServer.exe`

## What It Does

Enables **AI-assisted SQL Server performance diagnostics** through natural language queries with GitHub Copilot. Ask questions like:

- *"Is there high CPU on this system?"*
- *"Which queries are causing it?"*
- *"Is I/O slow and is SQL Server the contributing factor?"*

And get **instant, data-driven answers** from your PSSDiag/SQLLogScout diagnostic collections.

## Quick Setup

### 1. Configure MCP in VS Code

Press `Ctrl+Shift+P` ? Type: **"MCP: Open User Configuration"**

Add this to `mcp.json`:

```json
{
  "mcpServers": {
    "sqlnexus": {
      "command": "C:\\GitRepos\\SqlNexus\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": [],
      "env": {
        "SqlNexus__Server": "localhost",
        "SqlNexus__Database": "SqlNexus",
        "SqlNexus__TrustedConnection": "true"
      }
    }
  }
}
```

**Important**: Update the `command` path if your repository is in a different location.

### 2. Update Connection Details

Modify the `env` section:
- `SqlNexus__Server` - Your SQL Server instance
- `SqlNexus__Database` - Your SQL Nexus database name

### 3. Restart VS Code

Close and reopen VS Code completely.

### 4. Ask Questions in Copilot Chat

Open Copilot Chat (`Ctrl+Shift+I`) and ask diagnostic questions!

## 17 Diagnostic Tools

### CPU Analysis
- `analyze_cpu_usage` - Detect CPU pressure
- `get_top_cpu_queries` - Top CPU-consuming queries
- `get_sql_cpu_usage_over_time` - CPU usage trends from Perfmon

### I/O Analysis
- `analyze_io_performance` - Disk latency analysis (>20ms)
- `analyze_io_waits` - SQL Server I/O bottlenecks

### Wait & Bottleneck Analysis
- `analyze_wait_stats` - Top wait categories
- `get_aggregate_waits_and_queries` - Wait/query correlation
- `analyze_spinlocks` - Spinlock contention

### Blocking Analysis
- `analyze_blocking` - Head blockers and chains
- `get_blocked_sessions` - All blocked sessions

### Query Performance
- `get_top_queries_by_duration` - Slowest queries
- `get_waits_for_query` - Waits for specific query (HashID)
- `get_missing_indexes` - Index recommendations

### Memory & Overview
- `get_memory_clerk_distribution` - Memory usage by clerk
- `get_collection_time_range` - Collection time window
- `get_performance_summary` - Complete health check
- `query_nexus_database` - Execute custom SQL queries

## Prerequisites

- .NET Framework 4.8 (installed with SQL Nexus)
- SQL Nexus database with imported PSSDiag/SQLLogScout data

## Build

```bash
cd SqlNexus.McpServer
dotnet restore
dotnet build -c Release
```

Output: `bin\Release\SqlNexus.McpServer.exe`

## Configuration

### Windows Authentication (Default)

```json
"env": {
  "SqlNexus__Server": "localhost",
  "SqlNexus__Database": "SqlNexus",
  "SqlNexus__TrustedConnection": "true"
}
```

### SQL Authentication

```json
"env": {
  "SqlNexus__Server": "localhost",
  "SqlNexus__Database": "SqlNexus",
  "SqlNexus__TrustedConnection": "false",
  "SqlNexus__UserId": "sa",
  "SqlNexus__Password": "YourPassword"
}
```

### Remote Server

```json
"env": {
  "SqlNexus__Server": "SERVERNAME\\INSTANCE",
  "SqlNexus__Database": "SqlNexus_Production",
  "SqlNexus__TrustedConnection": "true"
}
```

## Usage Examples

Once configured, ask Copilot in VS Code:

- *"Is there high CPU on this system?"*
- *"Show me the top 10 slowest queries"*
- *"What are the blocking chains?"*
- *"Is I/O slow?"*
- *"Give me a performance summary"*
- *"What indexes are missing?"*

## Technical Details

### Database Tables Used
- `ReadTrace.tblBatches` - Query execution data from traces
- `tbl_REQUESTS` - sys.dm_exec_requests snapshots
- `tbl_OS_WAIT_STATS` - sys.dm_os_wait_stats snapshots
- `tbl_NOTABLEACTIVEQUERIES` - Active query snapshots
- `vw_BLOCKING_CHAINS` - Blocking analysis view
- `tbl_SPINLOCKSTATS` - Spinlock statistics
- `tbl_FILE_STATS` - I/O statistics
- `CounterData/CounterDetails` - Perfmon counter data
- `tbl_MissingIndexes` - Missing index recommendations

### Security
- Read-only operations (SELECT, WITH, DECLARE, IF only)
- Blocks DDL/DML operations (DROP, DELETE, UPDATE, etc.)
- 120-second query timeout

### Architecture

Built using:
- **.NET Framework 4.8** (matches SQL Nexus solution)
- **Microsoft.Data.SqlClient** 5.2.0 (modern, supported provider)
- **Model Context Protocol** via JSON-RPC over stdio
- **C#** with production-tested SQL Nexus queries

## Troubleshooting

### MCP Configuration Not Working

1. Verify path in `mcp.json` points to `bin\Release\SqlNexus.McpServer.exe`
2. Restart VS Code completely
3. Check Developer Console: `Help ? Toggle Developer Tools`

### Cannot Connect to Database

1. Create SQL Nexus database and import diagnostic data
2. Update server/database names in MCP configuration
3. Verify authentication method (Windows vs SQL Auth)

### Empty Query Results

- Ensure SQL Nexus database has imported data
- Verify tables exist: `ReadTrace.tblBatches`, `tbl_REQUESTS`, etc.

## Documentation

- **HOW_TO_USE.md** - Complete setup and usage guide
- **SETUP.md** - Detailed configuration instructions  
- **BUILD_SUMMARY.md** - Project architecture overview

## License

MIT License - Part of Microsoft SQL Nexus project
