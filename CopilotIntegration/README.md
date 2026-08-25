# SQL Nexus Copilot Integration

These scripts register or unregister the SQL Nexus MCP server and diagnostic agent for the
current Windows user. They do not install or remove GitHub Copilot.

## Register

Run from an extracted SQL Nexus release:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost\SQLEXPRESS" `
    -Database "SqlNexus"
```

Registration updates the following user-level locations while preserving unrelated entries:

- VS Code MCP configuration: `%APPDATA%\Code\User\mcp.json`
- Copilot CLI MCP configuration: `%USERPROFILE%\.copilot\mcp-config.json`
- Shared custom agent: `%USERPROFILE%\.copilot\agents\sql-nexus-diagnostic.agent.md`

Use `-Force` only when replacing an existing `sqlnexus_mcp` entry or SQL Nexus agent that has
different content. The scripts configure Windows Integrated Authentication and never request or
store a password.

Restart VS Code and any active Copilot CLI session after registration. Your organization must
allow MCP servers in Copilot before the tools can be used.

## Unregister

```powershell
.\CopilotIntegration\Unregister-SqlNexusCopilotIntegration.ps1
```

Unregistration removes only the `sqlnexus_mcp` entries and the SQL Nexus diagnostic agent.
Other user configuration is preserved.