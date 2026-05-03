# SQL Nexus MCP Server - Build Complete!

## What's Ready

Your **SQL Nexus MCP Server** is built and ready for testing!

**Location**: `bin\Release\SqlNexus.McpServer.exe`
**Framework**: .NET Framework 4.8 (matches SQL Nexus solution)

---

## IMPORTANT: Where to Use It

### You CANNOT Ask Questions Here
This GitHub Copilot Workspace chat does **NOT** have the MCP server loaded.

### Use VS Code Copilot Chat Instead

1. **Configure MCP**: Press `Ctrl+Shift+P` → "MCP: Open User Configuration"
2. **Add Configuration** (see below)
3. **Restart VS Code**
4. **Open Copilot Chat**: `Ctrl+Shift+I`
5. **Ask Questions**: "Is there high CPU on this system?"

---

## Quick Start Checklist

- [x] MCP Server built
- [ ] Configure in VS Code: `Ctrl+Shift+P` → "MCP: Open User Configuration"
- [ ] Update `--server` and `--database` args to match your environment
- [ ] Import PSSDiag/SQLLogScout data into SQL Nexus database
- [ ] Restart VS Code
- [ ] Test in Copilot Chat: "Is there high CPU?"

---

## MCP Configuration Template

Press `Ctrl+Shift+P` → "MCP: Open User Configuration" → Add:

> Replace `C:\path\to\SqlNexus.McpServer` with the actual path where your SQL Nexus repository is located.

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

---

## What You Can Ask (in VS Code Copilot Chat)

**CPU**: "Is there high CPU?" / "Which queries are consuming CPU?"

**I/O**: "Is I/O slow?" / "Is SQL Server causing slow I/O?"

**Blocking**: "Show me blocking chains" / "What queries are blocked?"

**Performance**: "Give me a performance summary" / "Show me the slowest queries" / "What indexes are missing?"

---

## Documentation

See **README.md** for full setup, testing, architecture, and troubleshooting.

