# SQL Nexus MCP Server - Complete Guide

## ? Status: Built and Ready!

**Executable**: `C:\GitRepos\SqlNexus\SqlNexus.McpServer\bin\Release\SqlNexus.McpServer.exe`
**Framework**: .NET Framework 4.8 (matches SQL Nexus solution)
**Provider**: Microsoft.Data.SqlClient 5.2.0 (modern, supported)

---

## ?? Where to Ask Questions

### ? NOT Here (GitHub Copilot Workspace)
This current chat window does **NOT** have the MCP server loaded. It's a regular Copilot conversation.

### ? Use VS Code Copilot Chat
Open **Copilot Chat** in VS Code (`Ctrl+Shift+I`) after configuring the MCP server.

---

## ?? Quick Setup Guide

### 1. Configure MCP Server in VS Code

**Method 1: Standard MCP Configuration (Recommended)**

Press `Ctrl+Shift+P` in VS Code ? Type: **"MCP: Open User Configuration"**

This opens `mcp.json`. Add:

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

**Method 2: Workspace Settings (Alternative)**

You can also add the configuration to your workspace `.vscode\settings.json` file if you prefer project-specific configuration.

### 2. Update Database Connection

Edit the `env` section above:
- `SqlNexus__Server`: Your SQL Server instance name
- `SqlNexus__Database`: Your SQL Nexus database name
- `SqlNexus__TrustedConnection`: "true" for Windows Auth, "false" for SQL Auth

### 3. Restart VS Code

**Critical**: Close and reopen VS Code completely to load the MCP server.

### 4. Open Copilot Chat

Press `Ctrl+Shift+I` or click the Copilot icon in VS Code.

### 5. Ask Questions!

Examples:
- *"Is there high CPU on this system?"*
- *"Show me the top 10 slowest queries"*
- *"What are the blocking chains?"*

---

## ?? Example Conversation

```
You (in VS Code Copilot Chat): "Is there high CPU on this system?"

Copilot: [Calls analyze_cpu_usage tool]
"Yes, high CPU detected. SOS_SCHEDULER_YIELD shows 45,234 ms/sec/CPU, 
indicating significant CPU pressure. Total wait: 2.3M ms across all CPUs 
during the collection period."

You: "Which queries are causing it?"

Copilot: [Calls get_top_cpu_queries tool]
"Top 5 CPU-consuming queries:
1. 1.2M ms CPU: SELECT * FROM Orders WHERE...
2. 890K ms CPU: UPDATE Inventory SET...
..."

You: "Give me a complete performance summary"

Copilot: [Calls get_performance_summary tool]
[Returns comprehensive analysis of CPU, I/O, blocking, waits, memory...]
```

---

## ?? 17 Diagnostic Tools

### Answer These Key Questions:

| Question | Tool Used |
|----------|-----------|
| **"Is there high CPU?"** | `analyze_cpu_usage` |
| **"Which queries cause high CPU?"** | `get_top_cpu_queries` |
| **"Is I/O slow?"** | `analyze_io_performance` |
| **"Is SQL Server causing slow I/O?"** | `analyze_io_waits` |
| **"What's blocking?"** | `analyze_blocking` |
| **"What are the top waits?"** | `analyze_wait_stats` |
| **"What indexes are missing?"** | `get_missing_indexes` |
| **"Show me the slowest queries"** | `get_top_queries_by_duration` |
| **"Give me a health check"** | `get_performance_summary` |

---

## ?? Configuration Examples

### Local Server (Windows Auth)
```json
"env": {
  "SqlNexus__Server": "localhost",
  "SqlNexus__Database": "SqlNexus",
  "SqlNexus__TrustedConnection": "true"
}
```

### Remote Server (Windows Auth)
```json
"env": {
  "SqlNexus__Server": "PROD-SQL01\\INSTANCE1",
  "SqlNexus__Database": "SqlNexus_Production",
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

---

## ?? Testing Your Setup

### Test 1: Verify Executable

```powershell
Test-Path "C:\GitRepos\SqlNexus\SqlNexus.McpServer\bin\Release\SqlNexus.McpServer.exe"
```

Should return: `True`

### Test 2: Run Connection Test

```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer
.\Test-McpServer.ps1
```

### Test 3: Manual Run

```powershell
cd bin\Release
.\SqlNexus.McpServer.exe
```

Should output to stderr:
```
sqlnexus-mcp-server v1.0.0 started
Connected to: localhost/SqlNexus
Using Microsoft.Data.SqlClient
```

Press `Ctrl+C` to stop.

---

## ?? Troubleshooting

### "MCP: Open User Configuration" command not found

- Update VS Code to latest version
- Ensure GitHub Copilot extension is installed and active
- Try restarting VS Code

### Copilot doesn't respond to diagnostic questions

1. Verify MCP configuration is saved in `mcp.json`
2. Check the executable path uses **Release** folder: `bin\Release\`
3. Restart VS Code completely
4. Check VS Code Developer Console: `Help ? Toggle Developer Tools` ? Console tab

### "Cannot open database SqlNexus"

1. Create the database: Use SQL Nexus GUI to import PSSDiag/SQLLogScout data
2. Verify database name in MCP configuration matches actual database name
3. Ensure user has permissions to the database

### Empty results from queries

- SQL Nexus database needs imported data (PSSDiag, SQLLogScout, or Perfmon)
- Verify tables exist: Open SSMS ? Connect ? Check for `ReadTrace.tblBatches`, `tbl_REQUESTS`, etc.

---

## ?? Example Questions for Copilot

Once configured in VS Code Copilot Chat:

**CPU Issues**:
- "Is there high CPU on this system?"
- "Which queries are consuming the most CPU?"
- "Show me CPU usage trends over time"

**I/O Issues**:
- "Is I/O slow on this server?"
- "Is SQL Server the contributing factor to slow I/O?"
- "Show me disk latency from Perfmon"

**Blocking Issues**:
- "Show me blocking chains"
- "What queries are being blocked?"
- "Who are the head blockers?"

**General Performance**:
- "Give me a performance summary"
- "What are the top wait types?"
- "Show me the slowest queries"
- "What indexes are missing?"

**Custom Analysis**:
- "Query the SQL Nexus database: SELECT TOP 10 * FROM tbl_REQUESTS WHERE wait_type = 'PAGEIOLATCH_SH'"

---

## ?? Prerequisites Checklist

- [x] .NET Framework 4.8 (included with SQL Nexus)
- [x] MCP Server built successfully ?
- [ ] SQL Nexus database created
- [ ] PSSDiag/SQLLogScout data imported
- [ ] MCP configured in VS Code (`Ctrl+Shift+P` ? MCP: Open User Configuration)
- [ ] Connection details updated in mcp.json
- [ ] VS Code restarted

---

## ?? If You Make Code Changes

1. Rebuild:
```powershell
cd C:\GitRepos\SqlNexus\SqlNexus.McpServer
dotnet build -c Release
```

2. Restart VS Code to reload the MCP server

---

## ? You're Ready!

**Key Point**: Use **VS Code Copilot Chat** (`Ctrl+Shift+I`), not this GitHub Copilot Workspace, to interact with the MCP server.

**Setup**: `Ctrl+Shift+P` ? "MCP: Open User Configuration" ? Add the configuration above ? Restart VS Code

**Happy diagnosing!** ??
