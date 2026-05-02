# SQL Nexus MCP Server - Project Summary

## ? Build Complete!

Your **SQL Nexus MCP Server** has been successfully created and compiled.

## ?? Project Structure

```
SqlNexus.McpServer/
??? SqlNexus.McpServer.csproj    # Project file (.NET 8.0)
??? Program.cs                    # MCP server main entry point
??? McpTypes.cs                   # MCP protocol type definitions
??? DiagnosticAnalyzer.cs         # SQL Nexus diagnostic queries
??? appsettings.json             # Configuration file
??? README.md                     # Full documentation
??? GETTING_STARTED.md           # Quick start guide
??? bin/Debug/net8.0/
    ??? SqlNexus.McpServer.exe   # Compiled executable ?
```

## ?? What This MCP Server Does

Enables **AI-assisted SQL Server performance diagnostics** through natural language queries against SQL Nexus databases. You can ask questions like:

- *"Is there high CPU on this system?"*
- *"Which queries are causing it?"*
- *"Is I/O slow and is SQL Server the contributing factor?"*

And get **instant, data-driven answers** from your PSSDiag/SQLLogScout collections.

## ??? 17 Diagnostic Tools Included

### CPU Diagnostics
1. `analyze_cpu_usage` - CPU wait analysis
2. `get_top_cpu_queries` - Top CPU-hungry queries
3. `get_sql_cpu_usage_over_time` - CPU trends

### I/O Diagnostics
4. `analyze_io_performance` - Disk latency analysis
5. `analyze_io_waits` - SQL Server I/O bottlenecks

### Wait & Bottleneck Analysis
6. `analyze_wait_stats` - Top wait categories
7. `get_aggregate_waits_and_queries` - Wait/query correlation
8. `analyze_spinlocks` - Spinlock contention

### Blocking Diagnostics
9. `analyze_blocking` - Head blockers
10. `get_blocked_sessions` - All blocked queries

### Query Analysis
11. `get_top_queries_by_duration` - Slowest queries
12. `get_waits_for_query` - Waits for specific query
13. `get_missing_indexes` - Index recommendations

### Memory & Overview
14. `get_memory_clerk_distribution` - Memory breakdown
15. `get_collection_time_range` - Collection window
16. `get_performance_summary` - Complete health check
17. `query_nexus_database` - Custom SQL queries

## ?? SQL Nexus Database Schema

The server queries these key SQL Nexus tables:

- **ReadTrace.tblBatches** - Query executions from traces
- **tbl_REQUESTS** - Request/session snapshots
- **tbl_OS_WAIT_STATS** - Wait statistics
- **vw_BLOCKING_CHAINS** - Blocking analysis
- **tbl_NOTABLEACTIVEQUERIES** - Active query snapshots
- **CounterData/CounterDetails** - Perfmon counters
- **tbl_SPINLOCKSTATS** - Spinlock data
- **tbl_FILE_STATS** - I/O statistics

## ?? Quick Start

### Step 1: Update Configuration

```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer
notepad appsettings.json
```

Set your SQL Server instance and SQL Nexus database name.

### Step 2: Test Connection (Manual)

```powershell
cd bin\Debug\net8.0
.\SqlNexus.McpServer.exe
```

You should see:
```
sqlnexus-mcp-server v1.0.0 started
Connected to: localhost/SqlNexus
Using Microsoft.Data.SqlClient
```

### Step 3: Configure Copilot

**Option A: VS Code** - Already configured in `.vscode\settings.json`

**Option B: Environment Variables**
```powershell
$env:SqlNexus__Server = "localhost"
$env:SqlNexus__Database = "SqlNexus"
```

### Step 4: Restart VS Code

Close and reopen VS Code to load the MCP server configuration.

### Step 5: Ask Copilot!

Open Copilot Chat and ask:
- *"Is there high CPU on this SQL Server?"*
- *"Show me the top 10 slowest queries"*
- *"Give me a performance summary"*

## ?? Technology Stack

- **.NET 8.0** - Modern .NET runtime
- **Microsoft.Data.SqlClient 5.2.0** - Modern SQL Server provider
- **Newtonsoft.Json** - JSON serialization
- **MCP Protocol** - JSON-RPC over stdio

## ?? Documentation References

### SQL Nexus Queries
All diagnostic queries are sourced from:
- `PerfStatsAnalysis.sql` - Wait stats, blocking, CPU analysis
- `ReadTracePostProcessing.sql` - Trace data analysis
- `Reports-via-SQL-Queries.md` - Community queries

### MCP Protocol
- Official Docs: https://modelcontextprotocol.io/
- Spec: https://spec.modelcontextprotocol.io/

## ?? Security

? **Read-Only Operations** - Only SELECT, WITH, DECLARE, IF allowed
? **Query Validation** - Blocks DDL/DML keywords
? **Timeout Protection** - 120-second query timeout
? **SQL Injection Prevention** - Parameterized queries where applicable

## ?? Example Scenarios

### Scenario 1: High CPU Investigation
```
You: "Is there high CPU on this system?"
Copilot: [Calls analyze_cpu_usage] "Yes, CPU waits show SOS_SCHEDULER_YIELD at 45,000 ms/sec/cpu"

You: "Which queries are causing it?"
Copilot: [Calls get_top_cpu_queries] "Top query consumed 3.2M ms CPU total..."

You: "Show me the query text"
Copilot: [Returns query details with statement text]
```

### Scenario 2: I/O Performance
```
You: "Is I/O slow?"
Copilot: [Calls analyze_io_performance] "Yes, drive D:\ has 45ms average disk sec/transfer"

You: "Is SQL Server the problem?"
Copilot: [Calls analyze_io_waits] "SQL Server shows 320,000ms of PAGEIOLATCH_SH waits"
```

### Scenario 3: Blocking Investigation
```
You: "Show me blocking chains"
Copilot: [Calls analyze_blocking] "Found 23 blocking chains, worst: session 87 blocked 45 tasks for 180 seconds"

You: "What was the head blocker doing?"
Copilot: [Shows stmt_text from blocking analysis results]
```

## ?? Next Iteration Ideas

- [ ] Add AlwaysOn-specific diagnostics
- [ ] TempDB contention analysis
- [ ] Query Store integration
- [ ] Plan cache analysis tools
- [ ] Deadlock graph analysis
- [ ] Historical trending (compare time windows)
- [ ] Export results to files
- [ ] Add caching for large result sets

## ?? Notes

- **Queries are optimized** from SQL Nexus reports and Wiki
- **Production-tested logic** from `PerfStatsAnalysis.sql`
- **Safe to run** - Read-only, no schema changes
- **Performant** - Uses proper indexes and filtering

## ? Success!

You now have a **production-ready MCP server** that brings **AI-powered diagnostics** to SQL Nexus! ??

**Next**: Configure your connection and start asking Copilot performance questions!
