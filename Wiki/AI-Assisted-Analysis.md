Starting with this release, SQL Nexus ships with a **Model Context Protocol (MCP) server**
(`SqlNexus.McpServer.exe`) and a **SQL Nexus Diagnostic Agent** for GitHub Copilot. Together they
let you ask questions about an imported SQL Nexus database in plain English and get data-driven
answers, instead of manually clicking through reports or writing T-SQL.

Example questions:

- *"Is there high CPU on this system, and which queries are causing it?"*
- *"Is I/O slow, and is SQL Server the contributing factor?"*
- *"Who are the head blockers during the collection window?"*
- *"Compare the slow run and the fast run and tell me what changed."*

> **⚠️ AI-generated content notice**
> The analysis produced by the agent is AI-assisted and **may be incomplete or inaccurate**.
> Treat every finding as a starting point, validate it against the underlying SQL Nexus tables,
> and review any generated report before sharing it. See the
> [Transparency Note](https://github.com/microsoft/SqlNexus/blob/master/TRANSPARENCY_NOTE.md).

## How it works

```
GitHub Copilot (VS Code or Copilot CLI)
        │  JSON-RPC over stdio
        ▼
SQL Nexus MCP Server (.NET Framework 4.8)
        │  Microsoft.Data.SqlClient (read-only)
        ▼
Your imported SQL Nexus database
```

The MCP server connects **only** to the SQL Nexus database you already imported into a
non-production instance. It never touches a production or live SQL Server.

## Safety and privacy

- **Read-only.** Only `SELECT`, `WITH` (CTE), `DECLARE` and `IF` are permitted. All DDL/DML
  (`DROP`, `DELETE`, `UPDATE`, `INSERT`, …) is blocked.
- **Offline data only.** The server reads a database of already-collected diagnostic data.
- **Encrypted SQL connections by default.** The MCP server requires encryption and validates
  the SQL Server certificate by default. Registration requires an explicit TLS policy; see
  [Encryption and certificate validation](#encryption-and-certificate-validation).
- **Least-privilege SQL execution.** Elevated SQL identities are blocked by default. An explicit
  restricted-reader policy is available for engineers connecting with an elevated Windows
  identity; MCP queries still never run with elevated permissions.
- **PII scrubbing.** Tool output is passed through a scrubber that redacts GUIDs, IP addresses,
  computer/UNC paths, Windows user paths, NT domain tokens, e-mail addresses, phone numbers and
  SQL login fields before it is returned to the AI model.
- **Query timeout** of 120 seconds protects the analysis instance.
- Its activity is logged to `%TEMP%\sqlnexus_mcpserver.log` (see [[App log]]).

## Prerequisites

- .NET Framework 4.8 (already required by SQL Nexus)
- A SQL Nexus database with imported SQL LogScout data
- **VS Code** with the GitHub Copilot extension, **or** the **GitHub Copilot CLI**

---

# End-to-end walkthrough

## Step 1 — Import your data

Import a SQL LogScout collection into a SQL Nexus database (UI or command line — see
[[How to use SQL Nexus]]). Note the SQL Server instance name and the database name.

## Step 2 — Register the MCP server and agent

`SqlNexus.McpServer.exe`, the SQL Nexus Diagnostic Agent, and its diagnostic skill files ship
pre-built with SQL Nexus. The recommended registration script installs the integration for the
current Windows user; no build, manual JSON editing, or manual agent copy is required.

Open **Windows PowerShell 5.1** (`powershell.exe`; PowerShell 7 / `pwsh` is not supported by SQL
Nexus or SQL LogScout) in the extracted SQL Nexus installation directory and run:

```powershell
Set-Location .\CopilotIntegration
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate
```

Replace `localhost` and `SqlNexus` with the instance and imported database from Step 1. The script:

- Registers `sqlnexus_mcp` in VS Code user configuration and GitHub Copilot CLI configuration
- Installs **SQL Nexus Diagnostic Agent** in `%USERPROFILE%\.copilot\agents`
- Resolves the installed `AI\Skills` directory so the agent can use its scenario guidance
- Uses Windows Integrated Authentication by default and does not request or store a password
  (SQL Authentication is optional; see [SQL Authentication](#sql-authentication-optional))
- Preserves unrelated MCP servers and agents in both clients

Run the same command again to confirm or restore the same registration. If an existing
`sqlnexus_mcp` entry or SQL Nexus agent has different settings, review it and add `-Force` to
replace only that SQL Nexus entry and agent. Add `-McpOnly` when you want the MCP tools without
installing or changing the custom agent.

To configure a second database for comparisons, include `-Database2`:

```powershell
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "sqlnexus_slow" `
    -Database2 "sqlnexus_fast" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate
```

### Registration parameters

`-EncryptionMode` and `-CertificateValidation` are required on every registration, including
when using `-Force` or `-McpOnly`. PowerShell prompts for them if omitted; supply them explicitly
in scripts.

| Parameter | Purpose / accepted values | Default |
|---|---|---|
| `-Server` | SQL Server instance hosting the imported data | `localhost` |
| `-Database` | Primary SQL Nexus database | `SqlNexus` |
| `-Database2` | Optional comparison database on the same SQL Server instance | Not configured |
| `-EncryptionMode` | `Optional`, `Mandatory`, or `Strict`; see the TLS policy below | Required |
| `-CertificateValidation` | `Validate` or `TrustServerCertificate` | Required |
| `-ElevatedPrincipalPolicy` | `Block` or `ImpersonateReader`; see the restricted-reader policy below | `Block` |
| `-InstallRoot` | Installation directory containing the MCP server, `.github\agents`, and `AI\Skills` | Parent of the script's directory |
| `-McpExecutablePath` | Explicit MCP executable path, overriding the installation-layout convention | `SqlNexus.McpServer\SqlNexus.McpServer.exe` under `InstallRoot` |
| `-CopilotHome` | Copilot CLI configuration and shared agent directory | `%USERPROFILE%\.copilot` |
| `-VsCodeUserData` | VS Code user configuration directory | `%APPDATA%\Code\User` |
| `-Force` | Replace a differently configured `sqlnexus_mcp` entry or installed SQL Nexus agent; preserve unrelated entries | Off |
| `-McpOnly` | Register the MCP server in both clients without installing or changing the custom agent; agent and skill files are not required | Off |
| `-AuthMode` | `Windows` (Windows Integrated Authentication) or `Sql`; see [SQL Authentication](#sql-authentication-optional) | `Windows` |
| `-UserId` | SQL login name; required with `-AuthMode Sql` | Not configured |
| `-SqlPassword` | Optional `SecureString` for automation with `-AuthMode Sql`; the script prompts when omitted | Prompt |
| `-UpdateCredential` | Update the locally stored credential, for example after the login's password changed in SQL Server. This does not change the password in SQL Server | Off |

### SQL Authentication (optional)

Windows Integrated Authentication is recommended and is the default. If SQL Authentication is
required (for example, the SQL Server accepts only SQL logins), register with a least-privilege,
read-only login, never `sa`:

```powershell
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -AuthMode Sql `
    -UserId "sqlnexus_reader"
```

The script prompts for the password (masked, entered twice) and stores it in a Windows
DPAPI-protected file for the current user in `%LOCALAPPDATA%\SqlNexus\McpCredentials`. The
registered MCP entry contains only `--trusted-connection false` and `--user-id`; the password is
never written to `mcp.json`, `mcp-config.json`, `appsettings.json`, an environment variable, or a
command line. If a credential already exists it is kept; add `-UpdateCredential` to update the locally stored credential.

Things to know:

- The credential works only for the same Windows user on the same machine, and is keyed by the
  exact `-Server` text (case-insensitive; `localhost`, `.` and `localhost,1433` are different keys).
- DPAPI protects the file from other users and from copying. It does not protect against malware
  running as the same user, and the password is in the MCP server's memory while it runs.
- The MCP server refuses to start if any JSON configuration file contains `SqlNexus:Password`.
- `SqlNexus__Password` in the environment is still honored for compatibility but logs a warning;
  environment variables can be exposed through process inspection, dumps, parent processes and logs.
- If the login's password changes in SQL Server, rerun the script with `-UpdateCredential` to update the
  locally stored credential (the script never changes the password in SQL Server). To manage the credential directly,
  use `SqlNexus.McpServer.exe --set-credential`, `--check-credential` or `--delete-credential` with the
  same `--server` and `--user-id`.
- Upgrading: remove any password you previously placed in `appsettings.json` or an MCP `env` block,
  then re-register with `-AuthMode Sql -UserId <login> -Force`.

### Encryption and certificate validation

Registration requires an explicit TLS policy. `Mandatory` requires encryption
and works with SQL Server **Force Encryption**. **Force Strict Encryption** is a different
server setting: it requires `Strict` (TDS 8.0), not just `Mandatory`.

| Encryption mode | Certificate validation | Result |
|---|---|---|
| `Optional` | `Validate` | Allowed with a warning: the client does not require encryption; SQL Server can still require it |
| `Optional` | `TrustServerCertificate` | Rejected |
| `Mandatory` | `Validate` | Encryption required; server certificate validated (recommended) |
| `Mandatory` | `TrustServerCertificate` | Encryption required; certificate validation bypassed (warning, lab use only) |
| `Strict` | `Validate` | TDS 8.0 Strict encryption with certificate validation |
| `Strict` | `TrustServerCertificate` | Rejected: Strict always requires certificate validation |

Rejected combinations do not write configuration changes. Prefer a certificate whose chain
and server name the client can validate.

```powershell
# SQL Server configured with Force Strict Encryption
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "sql2022.contoso.com" -Database "SqlNexus" `
    -EncryptionMode Strict -CertificateValidation Validate

# Lab-only opt-in for an untrusted or self-signed server certificate
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "labserver" -Database "SqlNexus" `
    -EncryptionMode Mandatory -CertificateValidation TrustServerCertificate

# Client does not require encryption (not recommended)
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" -Database "SqlNexus" `
    -EncryptionMode Optional -CertificateValidation Validate
```

With `Optional`, traffic can be observed or tampered with if SQL Server does not require
encryption. `TrustServerCertificate` disables server identity validation, allowing an on-path
attacker to impersonate SQL Server; use it only in a lab you control. The script prints warnings
for these choices.

`-CertificateValidation TrustServerCertificate` writes both `--trust-server-certificate true`
and `--allow-certificate-validation-bypass true`. The MCP server refuses the first without the
second. When launched without TLS arguments, the server itself defaults to `Mandatory` with
certificate validation, even though the registration script requires an explicit choice.

When running `SqlNexus.McpServer\Test-McpServer.ps1` interactively, connection setup prompts for
encryption mode and certificate validation after the elevated principal policy. Enter keeps
`Mandatory` and `Validate`; explicit `-EncryptionMode` and `-CertificateValidation` values skip
their respective prompts. The selection applies to individual tests, all-tools validation, and
menu-based registration. Interactive `-ForceRegistration` prompts for omitted TLS settings too;
non-interactive runs never prompt.

The validator drains stdout and stderr before sending requests to avoid redirected-pipe deadlocks
on Windows PowerShell 5.1. If all-tools validation hangs with an older build, update the packaged
`sqlnexus\bin\Release\SqlNexus.McpServer\Test-McpServer.ps1` from the current source script.

For an untrusted lab certificate, choose `Mandatory` with `TrustServerCertificate` (or supply
both parameters explicitly). `Optional` does not bypass certificate validation if SQL Server
requires encryption. The preliminary basic connectivity check is not a validation of MCP TLS
settings; the actual tool tests validate those settings.

### Connecting with an elevated Windows identity

The default `-ElevatedPrincipalPolicy Block` rejects SQL identities with `sysadmin`, `db_owner`,
or effective database write/DDL permissions. SQL Nexus imports create a loginless,
passwordless `SqlNexusMcpReader` user with only `db_datareader` membership and set the completed
database to `READ_ONLY`.

If your Windows identity is elevated, explicitly select `ImpersonateReader`:

```powershell
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -ElevatedPrincipalPolicy ImpersonateReader `
    -Force
```

`-Force` is needed only when replacing different existing settings. Each SQL connection enters
`EXECUTE AS USER = 'SqlNexusMcpReader' WITH NO REVERT` and verifies the restricted context before
processing MCP requests. This does not allow queries to run elevated. Startup fails if the
reader is missing, cannot be impersonated, or has unexpected permissions. Older databases
must be re-imported or hardened by an administrator before using this policy.

### Registering only the MCP server

```powershell
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -McpOnly
```

Use Copilot's default agent for this mode, or a separately managed custom agent. Both clients
receive the MCP registration, but the SQL Nexus Diagnostic Agent is left unchanged.

## Step 3 — Restart the client and verify the server

If VS Code was open during registration, restart it. Run **MCP: List Servers** from the Command
Palette and confirm `sqlnexus_mcp` is available; start it if necessary. The first launch may ask
you to trust the local executable path.

For GitHub Copilot CLI, start a new `copilot` session and run `/mcp` to confirm that
`sqlnexus_mcp` is connected.

## Step 4 — Open GitHub Copilot Chat

Open the Chat view in VS Code, or start GitHub Copilot CLI with `copilot`.

## Step 5 — Switch to the SQL Nexus Diagnostic Agent

In the agent picker, select **SQL Nexus Diagnostic Agent**. In Copilot CLI, enter `/agent` and
select the same agent. You can also invoke it directly:

```powershell
copilot --agent sql-nexus-diagnostic --prompt "Analyze this SQL Nexus database for its primary performance bottleneck."
```

The agent:

- Enables every SQL Nexus MCP tool automatically
- Follows a senior-DBA investigation flow: orient on the collection time range → form a hypothesis
  → call tools → drill down → synthesize a root cause
- Cross-checks its findings against the scenario **skill files** in `AI/Skills/`
  (CPU, memory, I/O, blocking, HADR, setup, index optimization, application analysis,
  query deep-dive, comparative analysis, …)
- Always closes with a **"Validate this analysis"** section telling you which SQL Nexus tables
  the conclusion came from so you can verify it yourself

> If you do not switch to the agent, the default Copilot agent can still call the MCP tools,
> but you lose the guided diagnostic workflow and the skill-file cross-checks.

## Step 6 — Ask a question

Example prompt:

```
Analyze the SQL Nexus database and tell me what the dominant bottleneck was during
the collection window. If it is CPU, show me the top queries responsible and whether
any of them look like parameter-sensitive plan problems.
```

Other prompts that work well:

```
Is I/O slow on this system? Break it down by database file and tell me whether
SQL Server itself or the storage subsystem is the contributing factor.
```

```
Show me the blocking chains, identify the head blockers, and tell me what those
sessions were doing.
```

```
Compare sqlnexus_slow and sqlnexus_fast: what changed in server properties,
database options, database scoped configurations, and query performance?
```

```
Give me a full performance summary and a prioritized list of recommended actions.
```

## Step 7 — Validate the findings

Follow the agent's "Validate this analysis" section. Use `list_nexus_tables` to discover related
tables and `query_nexus_database` to inspect the raw data, for example:

```
Run: SELECT TOP 100 * FROM tbl_OS_WAIT_STATS ORDER BY runtime
```

You can also cross-check the same data using the built-in reports (see [[Reports]]) and the
queries on the [[Reports via SQL Queries]] page.

## Manual configuration (advanced)

Use manual configuration only when the registration script cannot be used, when you need SQL
Authentication, or when client configuration is managed centrally. Replace the executable path,
server, and database values below with your installed values.

### VS Code

Run **MCP: Open User Configuration** and add this entry under `servers`:

```json
{
  "servers": {
    "sqlnexus_mcp": {
      "type": "stdio",
      "command": "C:\\path\\to\\SqlNexus\\SqlNexus.McpServer\\SqlNexus.McpServer.exe",
      "args": ["--server", "localhost", "--database", "SqlNexus", "--trusted-connection", "true", "--encrypt", "Mandatory", "--trust-server-certificate", "false", "--elevated-principal-policy", "block"]
    }
  }
}
```

### GitHub Copilot CLI

Start `copilot`, run `/mcp` to find the displayed configuration path, and add:

```json
{
  "mcpServers": {
    "sqlnexus_mcp": {
      "type": "stdio",
      "command": "C:\\path\\to\\SqlNexus\\SqlNexus.McpServer\\SqlNexus.McpServer.exe",
      "args": ["--server", "localhost", "--database", "SqlNexus", "--trusted-connection", "true", "--encrypt", "Mandatory", "--trust-server-certificate", "false", "--elevated-principal-policy", "block"],
      "env": {},
      "tools": ["*"]
    }
  }
}
```

Manual server arguments are `--server`, `--database`, optional `--database2` (also
`--database-for-comparison` or `--database_for_comparison`), and `--trusted-connection`.

TLS arguments are `--encrypt Optional|Mandatory|Strict`, `--trust-server-certificate true|false`,
and `--allow-certificate-validation-bypass true|false`. For lab-only certificate bypass, both
boolean arguments must be `true` and encryption must be `Mandatory`. Never combine bypass with
`Optional` or `Strict`.

The MCP test script validates these TLS arguments when reading registered configurations, including
those generated by `-ForceRegistration`. Invalid values and unsupported certificate-bypass
combinations fail before the MCP server is launched; valid TLS arguments are passed through unchanged.
Existing registrations without explicit TLS arguments continue to use the MCP server's secure defaults.

Use `--elevated-principal-policy block|impersonate-reader` to choose the SQL identity policy.
The same TLS and restricted-reader requirements described above apply to manual configuration.

For SQL Authentication, prefer the registration script (`-AuthMode Sql -UserId <login>`), which
stores the password in a DPAPI-protected file. To do it manually, set `--trusted-connection` to
`false`, add `--user-id <login>` to `args`, and store the password once with
`SqlNexus.McpServer.exe --set-credential --server <server> --user-id <login>` (use the same
`--server` text as in `args`). Do not put a password in `args`, `appsettings.json`, or the entry's
`env` object (`SqlNexus__Password` is honored only as a compatibility fallback and logs a warning).
Use a least-privilege login with read access to the SQL Nexus database rather than `sa`.

Manual setup must also install the agent file and make its `AI/Skills/` references resolve to the
installed skills directory. The registration script is preferred because it performs both steps
consistently.

## Remove the integration

From the `CopilotIntegration` directory, run:

```powershell
.\Unregister-SqlNexusCopilotIntegration.ps1
```

This removes only the `sqlnexus_mcp` entries and the installed SQL Nexus Diagnostic Agent;
unrelated client configuration is preserved. Add `-McpOnly` to remove the MCP entries while
preserving the agent.

For a SQL Authentication registration, the stored DPAPI credential for that server and user is
also deleted (it is kept, with a warning, if another MCP entry uses the same server and user).
Add `-KeepCredential` to always keep it.

---

# Comparing two collections (slow run vs fast run)

A very common troubleshooting task is *"it was fast yesterday and slow today — what changed?"*.
The `compare_nexus_databases` tool answers this by diffing **two** SQL Nexus databases side by side.

## 1. Import each collection into its own database

```
:: The "good" / fast baseline collection
sqlnexus.exe /S"." /E /D"sqlnexus_fast" /I"C:\collections\fast_run" /N /X /Q

:: The "bad" / slow collection
sqlnexus.exe /S"." /E /D"sqlnexus_slow" /I"C:\collections\slow_run" /N /X /Q
```

## 2. Configure the MCP server with both databases

From the extracted SQL Nexus installation, register the problem run as the primary database and
the healthy baseline as the comparison database:

```powershell
Set-Location .\CopilotIntegration
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "sqlnexus_slow" `
    -Database2 "sqlnexus_fast" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate `
    -Force
```

Both databases must be on the same SQL Server instance. `-Database` is the **primary** database
(typically the slow/problem run), and `-Database2` is the **comparison** database (typically the
healthy baseline). All other tools continue to run against the primary database only. `-Force`
replaces the previous `sqlnexus_mcp` registration; omit it if this is the first registration.

For centrally managed manual configuration, add `--database2`, `--database-for-comparison`, or
`--database_for_comparison` and the baseline database name to the MCP `args` shown in
[[Manual configuration (advanced)|AI-Assisted-Analysis#manual-configuration-advanced]].

## 3. Start the server and switch to the SQL Nexus Diagnostic Agent

Same as Steps 3–5 above.

## 4. Ask for the comparison

Example prompt:

```
Compare the two SQL Nexus databases I configured (sqlnexus_slow and sqlnexus_fast).
Tell me every difference in server properties, sp_configure settings, database options,
and database scoped configurations, then compare query performance between the two runs.
For each difference, tell me whether it could plausibly explain the slowdown and rank
them by likelihood. Cite the specific values you compared.
```

Other comparison prompts:

```
Which queries regressed the most between the fast run and the slow run?
Show duration, CPU and reads per execution for both runs, side by side.
```

```
Did anything change in the SQL Server configuration between these two collections?
Ignore differences that are expected to vary (uptime, process IDs, timestamps).
```

```
The slow run has higher CPU. Compare CPU consumption and compilation rates between
the two databases and tell me whether the extra CPU comes from more executions,
more expensive plans, or more compilations.
```

## What the comparison covers

| Section | Source tables |
|---|---|
| Server properties | `tbl_ServerProperties` |
| `sp_configure` settings | `tbl_Sys_Configurations` |
| Database options | `tbl_database_options` |
| Database scoped configurations | `tbl_database_scoped_configurations` |
| Query performance (only when trace/XEL data exists in **both** databases) | `ReadTrace.tblBatches`, `ReadTrace.tblUniqueBatches` |

If `--database2` was not supplied, the tool returns a clear error asking you to configure it.
If either database name does not exist on the server, the tool reports which one is missing.

> Any request to compare, diff or contrast two databases, servers, captures or runs is answered
> by `compare_nexus_databases` — the agent will not hand-write cross-database SQL for it.

You can achieve a similar comparison manually with the cross-database query on the
[[Reports via SQL Queries]] page.

---

# Available MCP tools

The MCP server exposes 39 read-only diagnostic tools. The installed agent enables the full tool
catalog automatically through the `sqlnexus_mcp/*` wildcard. Highlights:

| Area | Tools |
|---|---|
| **Orientation** | `get_collection_time_range`, `get_performance_summary`, `list_nexus_tables` |
| **CPU** | `analyze_cpu_usage`, `get_top_cpu_queries`, `get_sql_cpu_usage_over_time`, `get_cpu_by_database`, `get_compilation_stats` |
| **I/O** | `analyze_io_performance`, `analyze_io_waits`, `get_sql_file_io_stats`, `get_top_queries_by_reads`, `get_top_queries_by_writes` |
| **Waits** | `analyze_wait_stats`, `get_wait_type_distribution`, `get_wait_resource_hotspots`, `get_wait_heavy_queries`, `get_aggregate_waits_and_queries`, `get_waits_for_query` |
| **Blocking / locking** | `analyze_blocking`, `get_blocked_sessions`, `get_blocking_chain_tree`, `get_lock_summary_by_object` |
| **Queries** | `get_top_queries_by_duration`, `get_query_execution_details`, `get_statements_in_batch`, `get_queries_by_application`, `get_performance_by_application` |
| **Memory / plans / indexes** | `get_memory_clerk_distribution`, `get_plan_cache_analysis`, `get_missing_indexes`, `get_table_statistics_health` |
| **Other** | `analyze_spinlocks`, `analyze_tracing_overhead`, `analyze_hadr_health`, `analyze_setup_health`, `get_error_log_summary`, `compare_nexus_databases`, `query_nexus_database` |

# Troubleshooting

## Diagnosing MCP server startup failures

If the MCP server reports errors or fails to start, first check
`%TEMP%\sqlnexus_mcpserver.log` for startup, configuration, authentication, and database
connection details. In VS Code, also open **View > Output**, select the MCP-related output
channel, and review the `sqlnexus_mcp` server status from **MCP: List Servers**.

To determine whether the problem is in the MCP server or its VS Code configuration, run the
executable directly with the same arguments configured in VS Code.

In PowerShell:

```powershell
& "D:\SQLNexus\SqlNexus.McpServer\SqlNexus.McpServer.exe" `
    --server "." `
    --database "sqlnexus" `
    --trusted-connection "true" `
    --encrypt "Mandatory" `
    --trust-server-certificate "false" `
    --elevated-principal-policy "block"
```

In Command Prompt:

```cmd
"D:\SQLNexus\SqlNexus.McpServer\SqlNexus.McpServer.exe" --server "." --database "sqlnexus" --trusted-connection "true" --encrypt "Mandatory" --trust-server-certificate "false" --elevated-principal-policy "block"
```

Replace the executable path, SQL Server instance, and database name as needed. The
examples use validated certificates and block elevated identities, also substitute the TLS and
identity policy arguments from your actual registration when reproducing a startup failure.
Because the server communicates over standard input/output, a successful launch may remain running without
displaying a prompt; press `Ctrl+C` to stop it. If it exits or displays an error, use that message
together with `%TEMP%\sqlnexus_mcpserver.log` to investigate the executable path, .NET Framework
availability, SQL Server connectivity, database name, authentication, and database read
permissions. If it runs successfully outside VS Code, recheck the VS Code MCP `command` and
`args`, then restart VS Code and the server.

| Symptom | What to check |
|---|---|
| `sqlnexus_mcp` shows ✗ in Copilot CLI `/mcp` | Confirm `"type": "stdio"` is present in the CLI config; verify the `command` path points at `SqlNexus.McpServer.exe`; restart Copilot CLI |
| **MCP: Open User Configuration** not found | Update VS Code and make sure the GitHub Copilot extension is installed and active |
| Registration reports an existing configuration is different | Review the existing `sqlnexus_mcp` entry, then rerun the registration script with `-Force` if it should be replaced |
| Registration prompts for TLS parameters | Supply both `-EncryptionMode` and `-CertificateValidation`; they are required even with `-Force` or `-McpOnly` |
| Certificate validation or TLS handshake fails | Verify the certificate chain and server name; use `Strict` for Force Strict Encryption. Prefer fixing trust over bypassing validation; lab bypass requires `Mandatory` |
| Elevated SQL identity is rejected | Prefer a least-privilege identity, or explicitly register with `-ElevatedPrincipalPolicy ImpersonateReader` and ensure the imported database has the restricted `SqlNexusMcpReader` user |
| Copilot never calls the tools | Confirm the server is started (`MCP: List Servers` or `/mcp`) and that you selected the SQL Nexus Diagnostic Agent; its wildcard enables every SQL Nexus tool automatically |
| Cannot connect to the database | Verify the instance is reachable, the database name matches, and the auth mode (`--trusted-connection`) is correct — test in SSMS first |
| Empty results / missing tables | Import the data first. `ReadTrace.*` tables only exist once trace/XEL data has been imported; Perfmon tools require `.blg` data |
| Registration or configuration change not picked up | Restart VS Code or start a new Copilot CLI session; then stop and start the server from `MCP: List Servers` or verify it with `/mcp` |
| Need more detail | Check `%TEMP%\sqlnexus_mcpserver.log` |
