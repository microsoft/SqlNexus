# SQL Nexus MCP Server - Setup Complete! ✓

**Date**: May 12, 2026  
**Status**: Ready for Testing

---

## ✓ What's Been Completed

### 1. Build ✓
- **Executable**: `bin\Release\SqlNexus.McpServer.exe`
- **Framework**: .NET Framework 4.8
- **Status**: Built successfully (with 11 non-critical nullable warnings)

### 2. Configuration Files Created ✓

Two MCP configuration files have been created with your SQL Server settings:

#### For VS Code Copilot Chat:
**File**: `mcp-config-vscode.json`
```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "command": "c:\\Users\\nishamohan\\OneDrive - Microsoft\\Desktop\\STAC\\SqlNexus\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": [
        "--server", "localhost\\SQLEXPRESS",
        "--database", "NexusDiagnosticsTest",
        "--trusted-connection", "true"
      ]
    }
  }
}
```

#### For GitHub Copilot CLI:
**File**: `mcp-config-copilot-cli.json`
```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "type": "stdio",
      "command": "c:\\Users\\nishamohan\\OneDrive - Microsoft\\Desktop\\STAC\\SqlNexus\\SqlNexus.McpServer\\bin\\Release\\SqlNexus.McpServer.exe",
      "args": [
        "--server", "localhost\\SQLEXPRESS",
        "--database", "NexusDiagnosticsTest",
        "--trusted-connection", "true"
      ]
    }
  }
}
```

### 3. SQL Server Discovery ✓
**Detected Instances**:
- `localhost\SQLEXPRESS` ✓ (configured)
- `localhost\SQLEXPRESS01` (available)

**Target Database**: `NexusDiagnosticsTest`

---

## 🚀 Next Steps to Enable in VS Code

### Option A: Use VS Code Command Palette

1. Press `Ctrl+Shift+P`
2. Type: **"MCP: Open User Configuration"**
3. Copy the contents from `mcp-config-vscode.json`
4. Paste into the MCP configuration editor
5. Save and close VS Code completely
6. Reopen VS Code

### Option B: Manual File Copy

If the command palette method doesn't work:

1. Create this directory if it doesn't exist:
   ```
   C:\Users\nishamohan\AppData\Roaming\Code\User\globalStorage\github.copilot-chat\
   ```

2. Copy `mcp-config-vscode.json` to:
   ```
   C:\Users\nishamohan\AppData\Roaming\Code\User\globalStorage\github.copilot-chat\mcp.json
   ```

3. Restart VS Code completely

---

## 🧪 Testing the Setup

### Before Testing: Import Data
The MCP server requires a SQL Nexus database with imported diagnostic data:

1. **Launch SQL Nexus** (the main application)
2. **Connect** to `localhost\SQLEXPRESS` / `NexusDiagnosticsTest`
3. **Import Data**:
   - PSSDiag collection: File → Import → PSSDiag
   - SQLLogScout: File → Import → SQLLogScout
   - Trace files: File → Load Trace

### After Configuring MCP in VS Code:

1. **Restart VS Code** completely
2. **Open Copilot Chat**: `Ctrl+Shift+I`
3. **Try these questions**:
   - "Is there high CPU on this system?"
   - "Which queries are consuming the most CPU?"
   - "Show me blocking chains"
   - "Give me a complete performance summary"
   - "What tables are available in the Nexus database?"

### Using the Test Script:

```powershell
cd "c:\Users\nishamohan\OneDrive - Microsoft\Desktop\STAC\SqlNexus\SqlNexus.McpServer"
.\Test-McpServer.ps1
```

The test script will:
- Verify prerequisites
- Test SQL Server connection
- Let you interactively test MCP tools

---

## 📊 Available Diagnostic Tools (17 total)

### CPU Analysis
- `analyze_cpu_usage` - Is CPU high?
- `get_top_cpu_queries` - Which queries cause high CPU?
- `get_sql_cpu_usage_over_time` - CPU trends

### I/O Performance
- `analyze_io_performance` - Is I/O slow?
- `analyze_io_waits` - SQL Server I/O waits

### Blocking & Waits
- `analyze_wait_stats` - Top bottlenecks
- `analyze_blocking` - Head blockers
- `get_blocked_sessions` - Blocked sessions
- `analyze_spinlocks` - Spinlock contention

### Query Analysis
- `get_top_queries_by_duration` - Slowest queries
- `get_waits_for_query` - Waits for specific query
- `get_aggregate_waits_and_queries` - Wait/query correlation

### Optimization & Memory
- `get_missing_indexes` - Index recommendations
- `get_memory_clerk_distribution` - Memory usage

### Utilities
- `get_collection_time_range` - Data window
- `get_performance_summary` - Complete health check
- `list_nexus_tables` - Available tables
- `query_nexus_database` - Custom SQL queries

---

## 🔧 Changing SQL Server Settings

### Different Instance (e.g., SQLEXPRESS01):
Edit the config files and change:
```json
"--server", "localhost\\SQLEXPRESS01"
```

### Different Database:
```json
"--database", "YourDatabaseName"
```

### SQL Authentication (instead of Windows Auth):
```json
{
  "mcpServers": {
    "sqlnexus_MCP": {
      "command": "c:\\path\\to\\SqlNexus.McpServer.exe",
      "args": [
        "--server", "localhost\\SQLEXPRESS",
        "--database", "NexusDiagnosticsTest",
        "--trusted-connection", "false"
      ],
      "env": {
        "SqlNexus__UserId": "sa",
        "SqlNexus__Password": "YourPassword"
      }
    }
  }
}
```

**After any config change**: Restart VS Code completely.

---

## 📝 Files Modified/Created

- ✓ `bin\Release\SqlNexus.McpServer.exe` - Built executable
- ✓ `mcp-config-vscode.json` - VS Code MCP configuration
- ✓ `mcp-config-copilot-cli.json` - Copilot CLI configuration
- ✓ `appsettings.json` - Updated with your server/database
- ✓ `SETUP-COMPLETE.md` - This file

---

## ❗ Important Notes

1. **No SQL Server connection tested yet** - The test failed because the database `NexusDiagnosticsTest` may not exist yet. You'll need to:
   - Create the database, OR
   - Import PSSDiag/SQLLogScout data which will create it automatically

2. **Data Required** - The MCP server queries tables created by SQL Nexus after importing diagnostic data:
   - ReadTrace.* tables (from trace files)
   - CounterData (from Perfmon)
   - tbl_OS_WAIT_STATS (from DMV snapshots)
   - And many more...

3. **Read-Only** - The MCP server only executes SELECT queries (security by design)

---

## 🆘 Troubleshooting

### MCP Server not showing in VS Code
- Verify `mcp.json` path is correct
- Check VS Code Developer Console: `Help → Toggle Developer Tools → Console`
- Ensure GitHub Copilot extension is installed and active
- Restart VS Code completely (close all windows)

### Cannot connect to database
- Verify SQL Server is running: `Get-Service MSSQL$SQLEXPRESS`
- Confirm database exists in SSMS
- Check Windows Authentication is working
- Review connection string in config files

### Empty results
- Import diagnostic data into SQL Nexus first
- Check which tables exist: Ask Copilot "What tables are in the database?"
- Some tools require specific data (e.g., ReadTrace.* needs trace import)

---

## 📚 Documentation

- **README.md** - Complete documentation
- **START_HERE.md** - Quick start guide
- **Test-McpServer.ps1** - Interactive testing tool

---

**Ready to test!** Configure VS Code using the steps above, then start asking Copilot about your SQL Server performance! 🎉
