# SQL Nexus MCP Server - Getting Started

## What You've Built

A **Model Context Protocol (MCP) server** that enables AI-assisted analysis of SQL Nexus diagnostic databases through natural language queries with GitHub Copilot.

## Build Status

? **Project compiled successfully** - Located at: `C:\GitRepos\SqlNexus\SqlNexus.McpServer\bin\Debug\net8.0\SqlNexus.McpServer.exe`

## Next Steps

### 1. Configure Connection to SQL Nexus Database

Edit `SqlNexus.McpServer\appsettings.json`:

```json
{
  "SqlNexus": {
    "Server": "localhost",           // Your SQL Server instance
    "Database": "SqlNexus",          // Your SQL Nexus database name
    "TrustedConnection": true        // Use Windows Authentication
  }
}
```

**For SQL Authentication**, use:
```json
{
  "SqlNexus": {
    "Server": "localhost",
    "Database": "SqlNexus",
    "TrustedConnection": false,
    "UserId": "your_username",
    "Password": "your_password"
  }
}
```

### 2. Test the MCP Server Manually

```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer\bin\Release\SqlNexus.McpServer.exe
```

The server will:
- Read JSON-RPC requests from **stdin**
- Write responses to **stdout**
- Write status/errors to **stderr**

Press `Ctrl+C` to stop.

### 3. Configure VS Code / GitHub Copilot

The `.vscode\settings.json` file is already configured. Update the paths if needed:

```json
{
  "github.copilot.advanced": {
    "mcp": {
      "servers": {
        "sqlnexus_MCP": {
          "command": "C:\\GitRepos\\SqlNexus\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
          "env": {
            "SqlNexus__Server": "localhost",
            "SqlNexus__Database": "SqlNexus",
            "SqlNexus__TrustedConnection": "true"
          }
        }
      }
    }
  }
}
```

**Important**: Restart VS Code after configuration changes.

### 4. Use with Copilot!

Once configured, ask Copilot questions like:

#### CPU Analysis
- *"Is there high CPU on this system?"*
- *"Which queries are causing high CPU?"*
- *"Show me the top 10 CPU consuming queries"*

#### I/O Analysis
- *"Is I/O slow?"*
- *"Is SQL Server the contributing factor to slow I/O?"*
- *"Show me disk latency from Perfmon"*

#### Blocking Analysis
- *"Show me blocking chains"*
- *"What queries are being blocked?"*
- *"Who are the head blockers?"*

#### General Performance
- *"Give me a performance summary"*
- *"What are the top wait types?"*
- *"Show me spinlock contention"*
- *"What's the data collection time range?"*

#### Custom Queries
- *"Query the SQL Nexus database: SELECT TOP 10 * FROM tbl_REQUESTS WHERE wait_type = 'PAGEIOLATCH_SH'"*

## 17 Available Tools

| Tool | Purpose |
|------|---------|
| `get_top_queries_by_duration` | Slowest queries by total duration |
| `analyze_cpu_usage` | CPU wait analysis (SOS_SCHEDULER_YIELD) |
| `get_top_cpu_queries` | Top CPU-consuming queries |
| `analyze_io_performance` | Disk I/O latency analysis (Perfmon) |
| `analyze_io_waits` | SQL Server I/O wait analysis |
| `analyze_wait_stats` | Overall bottleneck analysis |
| `analyze_blocking` | Head blocker analysis |
| `get_blocked_sessions` | All blocked sessions and queries |
| `analyze_spinlocks` | Spinlock contention analysis |
| `get_collection_time_range` | Data collection time window |
| `get_waits_for_query` | Waits for specific query (by HashID) |
| `get_aggregate_waits_and_queries` | Wait/query correlation |
| `get_missing_indexes` | Missing index recommendations |
| `get_sql_cpu_usage_over_time` | CPU usage trends (Perfmon) |
| `get_memory_clerk_distribution` | Memory clerk breakdown |
| `get_performance_summary` | Comprehensive health check |
| `query_nexus_database` | Execute custom SQL queries |

## Troubleshooting

### Connection Issues

If you get connection errors:
1. Verify SQL Server is running
2. Check server/database name in `appsettings.json`
3. Test connection with SSMS first
4. Check Windows Authentication vs SQL Authentication settings

### No Data Returned

If queries return empty results:
1. Verify SQL Nexus database has imported diagnostic data
2. Check that PSSDiag/SQLLogScout data was successfully imported
3. Run manually: `dotnet run` and check stderr output

### VS Code Not Seeing MCP Server

1. Restart VS Code completely
2. Check that the exe path in `.vscode\settings.json` is correct
3. Look for MCP server errors in VS Code Developer Console (Help ? Toggle Developer Tools)

## Database Tables Reference

### Trace/ReadTrace Tables
- `ReadTrace.tblBatches` - Batch execution data
- `ReadTrace.tblUniqueBatches` - Unique query templates
- `ReadTrace.tblStatements` - Statement-level data
- `ReadTrace.tblTimeIntervals` - Time bucketing

### Performance Stats Tables
- `tbl_REQUESTS` - sys.dm_exec_requests snapshots
- `tbl_OS_WAIT_STATS` - sys.dm_os_wait_stats snapshots
- `tbl_NOTABLEACTIVEQUERIES` - Active query snapshots
- `tbl_HEADBLOCKERSUMMARY` - Blocking summary
- `vw_BLOCKING_CHAINS` - Blocking chain view

### Perfmon Tables
- `CounterData` - Perfmon counter values
- `CounterDetails` - Counter metadata

### Other Analysis Tables
- `tbl_SPINLOCKSTATS` - Spinlock statistics
- `tbl_FILE_STATS` - Virtual file stats
- `tbl_MissingIndexes` - Missing index DMV data
- `tbl_DM_OS_MEMORY_CLERKS` - Memory clerk data

## Architecture

```
???????????????????????????????????????
?   GitHub Copilot (VS Code/CLI)     ?
?                                     ?
?  "Is there high CPU?"               ?
???????????????????????????????????????
               ? JSON-RPC over stdio
               ?
???????????????????????????????????????
?   SQL Nexus MCP Server (C#/.NET)   ?
?                                     ?
?  � Parses natural language intent   ?
?  � Maps to diagnostic queries       ?
?  � Executes against SQL Nexus DB    ?
???????????????????????????????????????
               ? Microsoft.Data.SqlClient
               ?
???????????????????????????????????????
?     SQL Nexus Database              ?
?                                     ?
?  � ReadTrace.tblBatches             ?
?  � tbl_REQUESTS                     ?
?  � tbl_OS_WAIT_STATS                ?
?  � CounterData (Perfmon)            ?
?  � And 50+ other diagnostic tables  ?
???????????????????????????????????????
```

## Next Iteration Ideas

- Add more specialized tools (AlwaysOn, TempDB, Query Store)
- Implement streaming for large result sets
- Add query result caching
- Create a test suite with sample queries
- Add prompt templates for common scenarios
- Export results to files (CSV, JSON)

## Support

For issues or questions:
- SQL Nexus GitHub: https://github.com/microsoft/SqlNexus
- Model Context Protocol: https://modelcontextprotocol.io/
