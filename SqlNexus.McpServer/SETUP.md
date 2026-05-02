# SQL Nexus MCP Server - Setup Instructions

## ? Build Status

**Successfully compiled**: `bin\Release\SqlNexus.McpServer.exe`

Target Framework: **.NET Framework 4.8** (matches SQL Nexus solution)

---

## ?? Quick Setup (5 Steps)

### Step 1: Configure Database Connection

Edit `SqlNexus.McpServer\appsettings.json`:

```json
{
  "SqlNexus": {
    "Server": "localhost",           // ? Change to your SQL Server
    "Database": "SqlNexus",          // ? Change to your database name
    "TrustedConnection": true
  }
}
```

### Step 2: Configure MCP in VS Code

**Option A: Standard MCP Configuration (Recommended)**

1. Press **`Ctrl+Shift+P`** in VS Code
2. Type: **"MCP: Open User Configuration"**
3. Add this configuration:

```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
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

**Option B: Project-specific settings.json**

Already configured in `.vscode\settings.json`. Update the path:

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

### Step 3: Restart VS Code

**Important**: Close and reopen VS Code completely to load the MCP server.

### Step 4: Verify MCP Server is Loaded

1. Open Copilot Chat (`Ctrl+Shift+I`)
2. Look for MCP indicators or available tools
3. Check VS Code Developer Console (`Help ? Toggle Developer Tools`) for MCP logs

### Step 5: Ask Questions!

Try these examples:
- *"Is there high CPU on this system?"*
- *"Show me the top 10 slowest queries"*
- *"What are the top wait types?"*
- *"Is I/O slow?"*

---

## ?? Configuration Options

### Windows Authentication (Default)
```json
{
  "SqlNexus__Server": "localhost",
  "SqlNexus__Database": "SqlNexus",
  "SqlNexus__TrustedConnection": "true"
}
```

### SQL Authentication
```json
{
  "SqlNexus__Server": "localhost",
  "SqlNexus__Database": "SqlNexus",
  "SqlNexus__TrustedConnection": "false",
  "SqlNexus__UserId": "sa",
  "SqlNexus__Password": "YourPassword"
}
```

### Remote Server
```json
{
  "SqlNexus__Server": "SERVERNAME\\INSTANCE",
  "SqlNexus__Database": "SqlNexus_Production",
  "SqlNexus__TrustedConnection": "true"
}
```

---

## ?? Prerequisites Checklist

- [x] .NET Framework 4.8 installed (comes with SQL Nexus)
- [x] MCP Server built successfully
- [ ] SQL Nexus database created
- [ ] PSSDiag or SQLLogScout data imported into SQL Nexus
- [ ] appsettings.json updated with correct server/database
- [ ] MCP configuration added to VS Code (`Ctrl+Shift+P` ? MCP: Open User Configuration)
- [ ] VS Code restarted

---

## ?? Testing Your Setup

### Test 1: Manual Connection Test

```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer
.\Test-McpServer.ps1
```

Expected output:
```
? .NET SDK Version: 9.0.xxx
? MCP Server found
? Configuration file found
? Successfully connected to localhost/SqlNexus
```

### Test 2: Manual MCP Server Run

```powershell
cd bin\Release
.\SqlNexus.McpServer.exe
```

Expected stderr output:
```
sqlnexus-mcp-server v1.0.0 started
Connected to: localhost/SqlNexus
Using Microsoft.Data.SqlClient
```

Press `Ctrl+C` to stop. The server is now waiting for JSON-RPC input via stdin.

### Test 3: VS Code Copilot Test

1. Open VS Code
2. Open Copilot Chat (`Ctrl+Shift+I`)
3. Ask: *"Is there high CPU on this system?"*
4. Copilot should invoke the `analyze_cpu_usage` tool
5. You should see diagnostic data returned

---

## ?? 17 Available Tools

| Tool Name | What It Does | Example Question |
|-----------|--------------|------------------|
| `get_top_queries_by_duration` | Find slowest queries | "Show me the top 10 slowest queries" |
| `analyze_cpu_usage` | CPU wait analysis | "Is there high CPU?" |
| `get_top_cpu_queries` | Top CPU queries | "Which queries are using the most CPU?" |
| `analyze_io_performance` | Disk latency (Perfmon) | "Is I/O slow?" |
| `analyze_io_waits` | SQL I/O waits | "Is SQL Server causing slow I/O?" |
| `analyze_wait_stats` | Bottleneck analysis | "What are the top wait types?" |
| `analyze_blocking` | Head blockers | "Show me blocking chains" |
| `get_blocked_sessions` | Blocked queries | "What queries are blocked?" |
| `analyze_spinlocks` | Spinlock contention | "Show me spinlock contention" |
| `get_collection_time_range` | Collection window | "What's the data collection time range?" |
| `get_waits_for_query` | Waits for specific query | "What waits did query HashID 12345 encounter?" |
| `get_aggregate_waits_and_queries` | Wait/query correlation | "Which queries experienced the most waits?" |
| `get_missing_indexes` | Index recommendations | "What indexes are missing?" |
| `get_sql_cpu_usage_over_time` | CPU trends | "Show me CPU usage over time" |
| `get_memory_clerk_distribution` | Memory breakdown | "Show me memory usage by clerk" |
| `get_performance_summary` | Complete health check | "Give me a performance summary" |
| `query_nexus_database` | Custom queries | "Query: SELECT TOP 10 * FROM tbl_REQUESTS" |

---

## ?? Troubleshooting

### Issue: Copilot doesn't respond to MCP queries

**Solutions**:
1. Verify MCP server path in configuration (use Release folder)
2. Restart VS Code completely
3. Check VS Code Developer Console for errors (`Help ? Toggle Developer Tools`)
4. Ensure SQL Nexus database has imported data

### Issue: "Cannot open database SqlNexus"

**Solutions**:
1. Create SQL Nexus database: Open SQL Nexus GUI ? Import PSSDiag/SQLLogScout
2. Update `appsettings.json` with correct database name
3. Verify user has permissions to the database

### Issue: MCP server not found

**Solutions**:
1. Build in Release mode: `dotnet build -c Release`
2. Verify path in MCP configuration: `bin\Release\SqlNexus.McpServer.exe`
3. Check that .NET Framework 4.8 is installed

### Issue: Empty results from queries

**Solutions**:
1. Verify SQL Nexus database has imported diagnostic data
2. Check table existence: `SELECT * FROM INFORMATION_SCHEMA.TABLES`
3. Ensure PSSDiag/SQLLogScout import completed successfully

---

## ?? MCP Configuration Locations

### VS Code User-Level (Recommended)
- Press `Ctrl+Shift+P` ? **"MCP: Open User Configuration"**
- Edits: `%APPDATA%\Code\User\globalStorage\github.copilot\mcp.json`
- **Benefit**: Works across all VS Code workspaces

### VS Code Workspace-Level
- Edit: `.vscode\settings.json` in your workspace
- **Benefit**: Project-specific configuration, committed to Git

---

## ?? Example Conversation Flow

```
You: "Is there high CPU on this system?"

Copilot: [Invokes analyze_cpu_usage MCP tool]
"Yes, high CPU detected. SOS_SCHEDULER_YIELD shows 45,234 ms/sec/CPU 
indicating CPU pressure. Total CPU wait time: 2.3M ms across all CPUs."

You: "Which queries are causing it?"

Copilot: [Invokes get_top_cpu_queries MCP tool]
"Top 5 CPU-consuming queries:
1. Query consuming 1.2M ms CPU: SELECT * FROM Orders WHERE...
2. Query consuming 890K ms CPU: UPDATE Inventory SET...
..."

You: "Show me the full query text for the first one"

Copilot: [Returns the stmt_text from previous results]
"Full query: SELECT * FROM Orders WHERE OrderDate > ..."
```

---

## ?? What Data You Need

The MCP server queries these SQL Nexus tables, so you need imported data:

| Table | Data Source | Required? |
|-------|-------------|-----------|
| `ReadTrace.tblBatches` | SQL Trace / XEvents | Yes (query analysis) |
| `tbl_REQUESTS` | sys.dm_exec_requests | Yes (wait analysis) |
| `tbl_OS_WAIT_STATS` | sys.dm_os_wait_stats | Yes (CPU/wait analysis) |
| `CounterData` | Perfmon | Optional (I/O, CPU trends) |
| `tbl_SPINLOCKSTATS` | sys.dm_os_spinlock_stats | Optional (spinlock analysis) |

**Get this data by**: Importing PSSDiag or SQLLogScout output via SQL Nexus GUI

---

## ?? Rebuild Instructions

If you make code changes:

```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer
dotnet build -c Release
```

Then restart VS Code to reload the MCP server.

---

## ? You're Ready!

1. ? MCP Server built successfully
2. ?? Update configuration with your SQL Server details
3. ?? Import diagnostic data into SQL Nexus
4. ?? Configure MCP in VS Code (`Ctrl+Shift+P` ? MCP: Open User Configuration)
5. ?? Restart VS Code
6. ?? Ask Copilot diagnostic questions!

**Next**: Import PSSDiag/SQLLogScout data and start diagnosing! ??
