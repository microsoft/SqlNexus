# SQL Nexus Copilot Integration

These scripts register or unregister the SQL Nexus MCP server and diagnostic agent for the
current Windows user. They do not install or remove GitHub Copilot.

## Register SQL Nexus

Run from an extracted SQL Nexus release:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate
```

`-EncryptionMode` and `-CertificateValidation` are required on every registration so the TLS policy
is always an explicit choice. See [TLS policy](#tls-policy-encryption-and-certificate-validation).

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
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
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
    -Database2 "sqlnexus_run2" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate
```

This adds `--database2` to the registered MCP server arguments. After registration, invoke the
`compare_nexus_databases` tool to get a side-by-side comparison.

Use `-Force` only when replacing an existing `sqlnexus_mcp` entry or SQL Nexus agent that has
different content. The scripts configure Windows Integrated Authentication and never request or
store a password.

### TLS policy (encryption and certificate validation)

Registration requires an explicit choice for both settings:

| Parameter | Values |
|-----------|--------|
| `-EncryptionMode` | `Optional`, `Mandatory`, `Strict` |
| `-CertificateValidation` | `Validate`, `TrustServerCertificate` |

SQL Server **Force Encryption** and **Force Strict Encryption** are different settings:

- **Force Encryption** works with `-EncryptionMode Mandatory`.
- **Force Strict Encryption** requires TDS 8.0 and a client using `-EncryptionMode Strict`. A client using only `Mandatory` is expected to fail against a server that requires Strict encryption.
- `Optional` means encryption is **not required by the client**. The connection can still be encrypted if SQL Server requires encryption, but it is not guaranteed to be. Registration prints a warning.

| Encryption mode | Certificate validation | Result |
|-----------------|------------------------|--------|
| `Optional` | `Validate` | Allowed (warning) |
| `Optional` | `TrustServerCertificate` | Rejected |
| `Mandatory` | `Validate` | Allowed |
| `Mandatory` | `TrustServerCertificate` | Allowed (warning, lab use only) |
| `Strict` | `Validate` | Allowed |
| `Strict` | `TrustServerCertificate` | Rejected: Strict always requires certificate validation |

Examples:

```powershell
# SQL Server with Force Strict Encryption
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "sql2022.contoso.com" -Database "SqlNexus" `
    -EncryptionMode Strict -CertificateValidation Validate

# Lab server with an untrusted or self-signed certificate
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "labserver" -Database "SqlNexus" `
    -EncryptionMode Mandatory -CertificateValidation TrustServerCertificate
```

`-CertificateValidation TrustServerCertificate` writes both `--trust-server-certificate true` and
`--allow-certificate-validation-bypass true`; the MCP server refuses the first without the second.
The MCP server itself defaults to `Mandatory` with certificate validation when launched without
these arguments.

### Elevated SQL identities

Registration defaults to `-ElevatedPrincipalPolicy Block`. At startup, the MCP server refuses a Windows identity that is `sysadmin`, `db_owner`, or has effective database write/DDL permissions.

SQL Nexus imports create a passwordless, loginless `SqlNexusMcpReader` user with only `db_datareader` membership and set the completed database to `READ_ONLY`. When the engineer's Windows identity is elevated, explicitly select the restricted reader policy:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -ElevatedPrincipalPolicy ImpersonateReader
```

This permits the elevated identity only to establish a SQL connection. Before any MCP request is processed, each connection enters `EXECUTE AS USER = 'SqlNexusMcpReader' WITH NO REVERT`; MCP queries never run with the ambient elevated permissions. Registration stores the policy in the MCP configuration and does not store a password.

Use `-Force` when changing the policy of an existing `sqlnexus_mcp` registration:

```powershell
.\CopilotIntegration\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -ElevatedPrincipalPolicy ImpersonateReader `
    -Force
```

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
