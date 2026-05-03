# SQL Nexus MCP Server

A Model Context Protocol (MCP) server for AI-assisted analysis of SQL Nexus diagnostic databases.

**Built**: `.NET Framework 4.8` | **Executable**: `bin\Release\SqlNexus.McpServer.exe`

---

## What It Does

Enables **AI-assisted SQL Server performance diagnostics** through natural language queries with GitHub Copilot. Ask questions like:

- *"Is there high CPU on this system?"*
- *"Which queries are causing it?"*
- *"Is I/O slow and is SQL Server the contributing factor?"*

And get **instant, data-driven answers** from your PSSDiag/SQLLogScout diagnostic collections.

> **Note**: Use **VS Code Copilot Chat** (`Ctrl+Shift+I`) or **GitHub Copilot CLI** (`copilot`) to interact with the MCP server — not GitHub Copilot Workspace.

---

## Prerequisites

- .NET Framework 4.8 (installed with SQL Nexus)
- SQL Nexus database with imported PSSDiag/SQLLogScout data
- GitHub Copilot extension in VS Code

---

## Setup

### 1. Build

```powershell
cd SqlNexus.McpServer
dotnet restore
dotnet build -c Release
```

Output: `bin\Release\SqlNexus.McpServer.exe`

### 2. Configure MCP

#### Option A: VS Code

Press `Ctrl+Shift+P` → Type: **"MCP: Open User Configuration"**

Add this to `mcp.json`:

> **Note**: Replace `C:\path\to\SqlNexus.McpServer` in the examples below with the actual path where your SQL Nexus repository is located.

```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "command": "C:\\path\\to\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": ["--server", "localhost", "--database", "SqlNexus", "--trusted-connection", "true"]
    }
  }
}
```

> Change `localhost` and `SqlNexus` in `args` to match your SQL Server instance and database name. If omitted, both default to `localhost` and `SqlNexus`.

**1. Start Copilot CLI in interactive mode:**
```bash
copilot
```

**2. Type `/mcp` and press Enter.**

Copilot CLI will list all registered MCP servers and show a line at the bottom pointing to the configuration file like:
```
Config: C:\path\to\.copilot\mcp-config.json
```

**3. Open that config file in VS Code or any text editor:**
```bash
code "C:\path\to\.copilot\mcp-config.json"
```

**4. Add the following entry** — note the Copilot CLI config uses `"type": "stdio"` (VS Code does not):
```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "type": "stdio",
      "command": "C:\\path\\to\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": ["--server", "localhost", "--database", "SqlNexus", "--trusted-connection", "true"]
    }
  }
}
```

**5. Save the file**

### 3. Update Connection Details

All non-sensitive settings go in `args`. Passwords go in `env`:

| Argument | Description | Default |
|----------|-------------|--------|
| `--server` | SQL Server instance name | `localhost` |
| `--database` | SQL Nexus database name | `SqlNexus` |
| `--trusted-connection` | `true` = Windows Auth, `false` = SQL Auth | `true` |

**SQL Authentication** — add credentials in `env` (keep passwords out of `args`):
```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "command": "C:\\path\\to\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": ["--server", "localhost", "--database", "SqlNexus", "--trusted-connection", "false"],
      "env": {
        "SqlNexus__UserId": "sa",
        "SqlNexus__Password": "YourPassword"
      }
    }
  }
}
```

**Remote server example:**
```json
"args": ["--server", "SERVERNAME\\INSTANCE", "--database", "SqlNexus_Production", "--trusted-connection", "true"]
```

### 4. Restart VS Code

Close and reopen VS Code completely to load the MCP server.

### 5. Ask Questions in Copilot Chat

Open Copilot Chat (`Ctrl+Shift+I`) and start diagnosing!

---

## Testing

### Run the Test Script

```powershell
cd path\to\SqlNexus.McpServer
.\Test-McpServer.ps1
```

The script checks prerequisites, tests the SQL connection, and lets you interactively invoke tools.

### Manual MCP Protocol Test

```powershell
cd bin\Release
@(
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}',
  '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"analyze_wait_stats","arguments":{}}}'
) -join "`n" | .\SqlNexus.McpServer.exe --server localhost --database SqlNexus
```

---

## 17 Diagnostic Tools

| Tool | Answers |
|------|---------|
| `analyze_cpu_usage` | "Is there high CPU?" |
| `get_top_cpu_queries` | "Which queries cause high CPU?" |
| `get_sql_cpu_usage_over_time` | "Show CPU trends over time" |
| `analyze_io_performance` | "Is I/O slow?" |
| `analyze_io_waits` | "Is SQL Server causing slow I/O?" |
| `analyze_wait_stats` | "What are the top bottlenecks?" |
| `get_aggregate_waits_and_queries` | "Which queries have the most waits?" |
| `analyze_spinlocks` | "Is there spinlock contention?" |
| `analyze_blocking` | "Who are the head blockers?" |
| `get_blocked_sessions` | "What sessions are blocked?" |
| `get_top_queries_by_duration` | "What are the slowest queries?" |
| `get_waits_for_query` | "What waits did a specific query hit?" |
| `get_missing_indexes` | "What indexes are missing?" |
| `get_memory_clerk_distribution` | "How is SQL Server memory used?" |
| `get_collection_time_range` | "What is the data collection window?" |
| `get_performance_summary` | "Give me a full health check" |
| `query_nexus_database` | Execute any custom SQL query |

---

## Example Copilot Conversation

```
You: "Is there high CPU on this system?"
Copilot: [Calls analyze_cpu_usage]
"Yes, high CPU detected. SOS_SCHEDULER_YIELD shows 45,234 ms/sec/CPU..."

You: "Which queries are causing it?"
Copilot: [Calls get_top_cpu_queries]
"Top CPU query: 1.2M ms — SELECT * FROM Orders WHERE..."

You: "Give me a complete performance summary"
Copilot: [Calls get_performance_summary]
"CPU: elevated | I/O: normal | Blocking: none | Top wait: SOS_SCHEDULER_YIELD..."
```

---

## Technical Details

### Architecture

```
GitHub Copilot (VS Code)
        │ JSON-RPC over stdio
        ▼
SQL Nexus MCP Server (.NET Framework 4.8)
        │ Microsoft.Data.SqlClient 5.2.0
        ▼
SQL Nexus Database
  ├── ReadTrace.tblBatches      (trace query data)
  ├── tbl_REQUESTS              (dm_exec_requests)
  ├── tbl_OS_WAIT_STATS         (dm_os_wait_stats)
  ├── tbl_NOTABLEACTIVEQUERIES  (active query snapshots)
  ├── vw_BLOCKING_CHAINS        (blocking analysis)
  ├── tbl_SPINLOCKSTATS         (spinlock stats)
  ├── tbl_FILE_STATS            (I/O stats)
  ├── CounterData/CounterDetails (Perfmon)
  └── tbl_MissingIndexes        (index recommendations)
```

### MCP Protocol Flow

```
┌─────────────────────────────────────────────────────┐
│                  STARTUP (once)                     │
│                                                     │
│  Copilot ──── initialize ────────────────► Server   │
│           ◄─── {protocolVersion,                    │
│                 serverInfo, capabilities} ───────── │
│           (version echoed back from request)        │
│                                                     │
│  Copilot ──── notifications/initialized ──► Server  │
│               (notification — no response)          │
│                                                     │
│  Copilot ──── tools/list ────────────────► Server   │
│           ◄─── {tools: [{name, description,         │
│                          inputSchema}, ...]} ─────  │
│                (Copilot caches all 17 tools)        │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│           PER QUESTION (repeated)                   │
│                                                     │
│  User: "Is there high CPU on this system?"          │
│     │                                               │
│     ▼                                               │
│  Copilot matches question → tool descriptions       │
│  (semantic match: "high CPU" → analyze_cpu_usage)   │
│     │                                               │
│     ▼                                               │
│  Copilot extracts parameters → inputSchema          │
│  ("top 5" → top_n: 5)                               │
│     │                                               │
│     ▼                                               │
│  Copilot ──── tools/call ────────────────► Server   │
│               {name: "analyze_cpu_usage",           │
│                arguments: {}}                       │
│     │                                               │
│     ▼                                               │
│  Server executes SQL → SQL Nexus Database           │
│     │                                               │
│     ▼                                               │
│  Server ◄─── {content: [{type: "text",              │
│               text: "{...JSON results...}"}]}       │
│     │                                               │
│     ▼                                               │
│  Copilot interprets JSON → natural language answer  │
└─────────────────────────────────────────────────────┘
```

### Security
- Read-only: `SELECT`, `WITH` (CTE), `DECLARE`, `IF` only
- Blocks all DDL/DML: `DROP`, `DELETE`, `UPDATE`, `INSERT`, etc.
- 120-second query timeout

---

## Troubleshooting

### `sqlnexus_MCP` shows ✗ in Copilot CLI `/mcp`
1. Rebuild to ensure the latest binary is in `bin\Release\`
2. Verify the config file has `"type": "stdio"` — required by CLI, not by VS Code
3. Check the `command` path in the CLI config file is correct
4. Restart Copilot CLI after any config or binary change

### "MCP: Open User Configuration" not found
- Update VS Code to the latest version
- Ensure the GitHub Copilot extension is installed and active

### Copilot doesn't use the tools
1. Verify `mcp.json` is saved with the correct exe path (`bin\Release\`)
2. Restart VS Code completely
3. Check VS Code Developer Console: `Help → Toggle Developer Tools → Console`

### Cannot connect to database
1. Verify SQL Server is running and reachable
2. Confirm database name matches your imported SQL Nexus database
3. Test connection in SSMS first
4. Check Windows vs SQL Authentication setting

### Empty results / missing tables
- Import PSSDiag or SQLLogScout data into SQL Nexus first
- `ReadTrace.*` tables only exist after loading a trace file via **File → Load Trace**
- Perfmon tools work with PerfStats data only

### After code changes
```powershell
dotnet build -c Release
# Then restart VS Code to reload the MCP server
```

---

## License

MIT License - Part of the [Microsoft SQL Nexus](https://github.com/microsoft/SqlNexus) project
