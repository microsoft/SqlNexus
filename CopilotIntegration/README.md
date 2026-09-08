# SQL Nexus Copilot Integration

These scripts register or unregister the SQL Nexus MCP server and diagnostic agent for the
current Windows user. They do not install or remove GitHub Copilot.

## Register SQL Nexus

Run from an extracted SQL Nexus release:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus"
```

Registration updates

- VS Code MCP configuration: `%APPDATA%\Code\User\mcp.json`
- Copilot CLI MCP configuration: `%USERPROFILE%\.copilot\mcp-config.json`
- Shared custom agent: `%USERPROFILE%\.copilot\agents\sql-nexus-diagnostic.agent.md`

When the script reports `SQL Nexus Copilot integration registered`, both the MCP server and the
SQL Nexus Diagnostic Agent are ready to use. You do not need to edit an MCP configuration file,
copy the agent manually, or run another installation command.

### Register only the MCP server

To register the MCP server for VS Code and Copilot CLI without installing or changing the SQL
Nexus Diagnostic Agent, add `-McpOnly`:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -McpOnly
```

This mode does not require the agent or `AI\Skills` files. Use it when you want to call the SQL
Nexus MCP tools from Copilot's default agent or when the custom agent is managed separately.

### Compare two databases

To enable the `compare_nexus_databases` tool, register a second (comparison) database with
`-Database2`. It is optional; omit it for single-database analysis.

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "sqlnexus_run1" `
    -Database2 "sqlnexus_run2"
```

This adds `--database2` to the registered MCP server arguments. After registration, invoke the
`compare_nexus_databases` tool to get a side-by-side comparison.

Use `-Force` only when replacing an existing `sqlnexus_mcp` entry or SQL Nexus agent that has
different content. The scripts configure Windows Integrated Authentication and never request or
store a password.

If you run the registration script again with the same settings, it leaves the existing
configuration and agent files unchanged and displays the settings that are already registered.

Your organization must allow MCP servers in Copilot before the SQL Nexus tools can be used.
After full registration, continue with the instructions for VS Code or Copilot CLI. You can use
either client or both.

## Use SQL Nexus in VS Code

1. Start VS Code. If VS Code was open during registration, restart it so it discovers the newly
    registered MCP server and agent.
2. Open Copilot Chat (`Ctrl+Alt+I`).
3. Open the agent picker in the Chat view and select **SQL Nexus Diagnostic Agent**.
4. Enter a diagnostic question, for example:

    ```text
    Analyze this SQL Nexus database for its primary performance bottleneck.
    ```

The agent and its SQL Nexus MCP tools are already configured. The first time the MCP server starts,
VS Code might ask you to confirm that you trust the local server. Review the displayed path and
approve it to continue.

If you registered with `-McpOnly`, use Copilot's default agent instead. The registered
`sqlnexus_mcp` tools are available to Copilot without the SQL Nexus Diagnostic Agent.

## Use SQL Nexus in Copilot CLI

1. Start a new Copilot CLI session after registration:

    ```powershell
    copilot
    ```

2. Enter `/agent`, select **SQL Nexus Diagnostic Agent**, and then enter a diagnostic question.

The agent and the `sqlnexus_mcp` server are already registered. No additional MCP command or
configuration change is required.

If you registered with `-McpOnly`, remain in Copilot CLI's default agent and ask it to use the
`sqlnexus_mcp` tools. The `/agent` selection and direct custom-agent command below apply only to a
full registration.

You can also invoke the agent directly without entering an interactive session:

```powershell
copilot --agent sql-nexus-diagnostic --prompt "Analyze this SQL Nexus database for its primary performance bottleneck."
```

## Unregister

```powershell
.\CopilotIntegration\Unregister-SqlNexusCopilotIntegration.ps1
```

Unregistration removes only the `sqlnexus_mcp` entries and the SQL Nexus diagnostic agent.
Other user configuration is preserved.

To remove only the MCP entries and preserve the installed SQL Nexus Diagnostic Agent, run:

```powershell
.\CopilotIntegration\Unregister-SqlNexusCopilotIntegration.ps1 -McpOnly
```
