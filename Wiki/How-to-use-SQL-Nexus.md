## Steps 1-2-3

1. **Install** SQL Nexus. RML Utilities is optional for legacy `.trc` files or the ReadTrace importer.
2. **Collect** perf data from your SQL Server instance using [SQL LogScout](https://github.com/microsoft/sql_logscout).
3. **Import** and analyze the data using SQL Nexus

## Download and Installation

- Get the latest version for SQL Nexus on https://github.com/microsoft/SqlNexus/releases
- To install, see [[Installation]] page

## Collecting data

In order to use SQL Nexus, you will first need to collect diagnostic data. You can use either of the following tools:

- [SQL LogScout](https://github.com/microsoft/sql_logscout) *(recommended)* – a modern, lightweight data collection tool for SQL Server on Windows and Linux

SQL Nexus supports importing data from **SQL Server 2014 and later**, including SQL Server on Linux.

## Importing data and Analyzing

[[/Images/SQLNexusImport.png]]

1. Launch **sqlnexus.exe** and log on to a non-production SQL Server instance where you have SQL Nexus installed.
1. Click on **Import** in the left pane of the main SQL Nexus window.
1. Provide the folder path where you stored the collected data. **Note:** provide a directory path, not a file name.
1. SQL Nexus will automatically detect and list the files to import, including:
   - **Rowset output files** (`.out` text files from SQLDiag/SQL LogScout)
   - **SQL Trace files** (`.trc`) — processed via RML Utilities/ReadTrace
  - **PSSDIAG XEL files** (`*pssdiag*.xel`) — processed by the managed Trace Event Importer without RML Utilities
  - **SQL LogScout XEL files** (`*LogScout*.xel`, e.g. `xevent_LogScout_target*.xel`, `SQLLogScout_AlwaysOn_Data_Movement*.xel`) — processed by the managed Trace Event Importer without RML Utilities
  - **SQLDiag XEL files** (`*_SQLDIAG*.xel`) — processed by the optional Custom XEL importer
  - **Always On Health XEL files** (`*AlwaysOn_health*.xel`) — processed by the optional Custom XEL importer
  - **System Health XEL files** (`*system_health*.xel`) — processed by the optional Custom XEL importer
   - **Performance Monitor logs** (`.blg`) via Perfmon Importer
  - **SQL Server ERRORLOG files** (`*_ERRORLOG*`, e.g. `SERVERNAME_ERRORLOG`, `SERVERNAME_ERRORLOG.1`) via ERRORLOG Importer
  - **Linux performance data** (`*.perf`) including I/O, CPU, memory, networking, and process metrics

### SQL LogScout All Instances collections

An **All Instances** SQL LogScout collection separates instance-specific files from shared
host/OS diagnostics. Its output is typically arranged like this:

```text
output_AllInstances\
|-- SERVER_SQL2019\
|-- SERVER_SQL2022\
`-- SharedOutputFiles\
```

Select the folder for the SQL Server instance you want to analyze, such as
`output_AllInstances\SERVER_SQL2019`. If that folder has a direct sibling named exactly
`SharedOutputFiles`, SQL Nexus automatically scans both folders. This works for imports started
from the UI and for command-line imports that use `/I`. If the sibling folder does not exist,
SQL Nexus imports only the selected folder as before.

The Import page displays **Also scanning:** followed by the shared folder path. Files discovered
there are labeled **(from SharedOutputFiles)** in the import list. Shared host/OS diagnostics can
include running drivers, disk information, event logs, and some Perfmon or Linux performance data.

To prevent duplicate data and unsafe table re-creation, importers handle the second folder as
follows:

| Importer/file type | Shared folder behavior |
| --- | --- |
| Rowset (`.OUT`/`.TXT`), ERRORLOG, and raw files | Files from both folders are eligible. If the same file name exists in both, the selected instance-folder copy wins. A different-size collision is reported so you can import `SharedOutputFiles` separately if it represents different data. |
| Linux performance (`.perf`) | Matching files can be imported from `SharedOutputFiles`; SQL Nexus passes the shared folder as the Linux importer's working directory for those rows. |
| Perfmon (`.blg`) and ReadTrace | `SharedOutputFiles` is a fallback. It is used only when the selected folder has no files matching that importer mask. If both folders contain matches, SQL Nexus imports the selected-folder files and records that the shared matches were skipped. |
| SQLDiag, Always On Health, and System Health XEL | The Custom XEL importer reads only the selected folder. If matching XEL files exist only in `SharedOutputFiles`, SQL Nexus reports them; move the needed files into the selected instance folder and re-import. |

> Select the instance folder, not the common parent or `SharedOutputFiles`, to get the combined
> instance and shared-host view automatically.

1. Press the **Import** button.
1. Once the import is finished, click on the reports in the left pane. Start with the **Bottleneck Analysis** report to determine where most query execution time was spent.
1. Then you can navigate the various reports in SQL Nexus to discover potential issues or examine the state of the SQL Server under investigation. **For information on the various available reports, see [[Reports]].**
1. You can also manually connect to the SQL Nexus database via tools like SSMS and write custom queries to discover answers to any questions. **For more information, see [[Reports via SQL Queries]]**
1. Finally, for more automated and in-depth custom analysis, use the AI MCP Server and Agent. **See [[AI Assisted Analysis]]**


## Importer Options

When you click the **Options** button on the Import page, a dropdown menu appears with an
**Importers** submenu. Each importer can be enabled/disabled and fine-tuned there, and your
choices can optionally be persisted between sessions.

At a glance:

| Importer | Enabled by default | Handles |
|---|---|---|
| **Rowset Importer** | ✅ | `*.OUT` / `*.TXT` rowset output |
| **Trace Event Importer (Managed)** | ✅ | `*.xel` — *recommended*, no RML Utilities needed |
| **ReadTrace (SQL XEL/TRC Files)** | ❌ | `*.trc` and `*.xel` via RML Utilities |
| **ERRORLOG Importer** | ✅ | `*_ERRORLOG*` → `tbl_ERRORLOG` |
| **BLG Blaster (Perfmon/Sysmon)** | ✅ | `*.blg` |
| **Import Linux Performance Files** | ❌ | `*.perf` |
| **Import SQLDiag / AlwaysOn / System Health XEL** | ❌ | `*_SQLDIAG*.xel`, `*AlwaysOn_health*.xel`, `*system_health*.xel` |

> Only one of *Trace Event Importer (Managed)* or *ReadTrace* can be enabled at a time —
> enabling one automatically disables the other.

**For the full list of options per importer, how settings are persisted in `user.config`, and
the `/M` command-line switch, see [[Importer Options]].**

## Themes and keyboard navigation

Use the **Theme** selector on the report toolbar to choose one of the available display themes:

- **Default** uses the standard SQL Nexus and Windows control colors.
- **Aquatic** uses a dark, high-contrast background.
- **Desert** uses a light, high-contrast background.

The selected theme is applied to the SQL Nexus interface and reports and is saved for the next
session. When Windows High Contrast mode is enabled, SQL Nexus uses Windows system colors instead
of the selected theme.

Press **F6** to move keyboard focus forward through the visible report, standard, and data
collection toolbars and then the main content area. Press **Shift+F6** to move through those areas
in reverse. Toolbar visibility choices are also retained between sessions.

## Using SQL Nexus from Command Prompt to Analyze data

You can use SQLNexus as a command line utility. You can get the following help menu via `sqlnexus.exe /?` or `sqlnexus --help`

    sqlnexus.exe [ [ /S [ /E | /Uuser /Ppwd ] [/D"database"] ] | [/C"connstr"] ] [/Iinputpath] [/Ooutputpath] [/Rreport] [/X] [/Q] [/N] [/M<importers>]

    /S"server"      Specifies a SQL Server name to connect to.
    /D"database"    Database to connect to
    /E              Log in to SQL using Windows/integrated security
    /Uuser          Specifies a SQL (non-Windows) login name
    /Ppassword      Specifies the password for a SQL (non-Windows) login
    /C"connstring"  Specifies the SqlClient ConnectionString to use (can be used instead of /S /E).
    /I"path"        Import SQL diagnostic data from this path
    /R"report"      Specifies a report file name to run.
    /O"path"        Specifies an export path for reports executed via /R. Also this is where the sqlnexus.000.log would get created
    /X              Exit after importing (/I) or exporting (/O) the specified report (/R)
    /Vparam=value   Specify the value of a form parameter
    /Q              Quiet Mode - minimize windows in console mode
    /N              Create a new SQLNexus database before importing (drop existing).
    /M<importers>   Enable specific importers for this run (see below).

> Switches do not accept a space between the switch and its value — `/Rreport` is valid, `/R report` is not.

## Selecting importers from the command line (/M)

The `/M` switch controls exactly which importers run for a single command-line import, without
changing any saved UI settings. Tokens are case-insensitive:
`TraceEventImporter` (aliases `Trace`, `TraceImp`, `TraceImporter`), `ReadTrace`, `Perfmon`,
`Linux`, `Errorlog`, `CustomXEL`, and `All`.

| Form | Meaning | Example |
|---|---|---|
| `/M<token>[+<token>...]` | Run only the listed importers | `/MPerfmon+Errorlog` |
| `/MAll` | Run every wired built-in importer | `/MAll` |
| `/MAll-<token>[-<token>...]` | Run everything except the listed importers | `/MAll-Trace-Perfmon` |

> The **Rowset Importer always runs**, regardless of `/M`. `+` and `-` cannot be combined in one
> value, and unknown tokens are rejected.

**For the complete token reference, mutual-exclusivity and fallback rules, exit codes, and more
examples, see [[Importer Options]].**

## Examples

**A.** Import data using the local instance and Windows authentication. The default log location is `%temp%`.

    sqlnexus.exe /S"." /E /D"sqlnexus" /I"C:\data_collection\output"

**B.** Import data using a named SQL Server instance, drop the existing `sqlnexus` database, write the log to a custom folder, and run quietly without interaction.

    sqlnexus.exe /S"sqlmachine\sql2017" /E /D"sqlnexus" /I"C:\data_collection\output" /N /O"c:\temp\" /Q

**C.** Import only the ERRORLOG and Perfmon data into a brand-new database and exit.

    sqlnexus.exe /S"." /E /D"sqlnexus_errorlog" /I"C:\data_collection\output" /N /MErrorlog+Perfmon /X /Q

## AI-Assisted Analysis: SQL Nexus MCP Server and Diagnostic Agent

SQL Nexus ships with a **Model Context Protocol (MCP) server** (`SqlNexus.McpServer.exe`) and a
**SQL Nexus Diagnostic Agent** for GitHub Copilot, so you can ask questions about an imported
SQL Nexus database in plain English instead of clicking through reports or writing T-SQL:

- *"Is there high CPU on this system, and which queries are causing it?"*
- *"Is I/O slow, and is SQL Server the contributing factor?"*
- *"Compare the slow run and the fast run and tell me what changed."*

The recommended installer registers the MCP server for both VS Code and GitHub Copilot CLI and
installs the agent with its diagnostic skill files. Open PowerShell in the extracted SQL Nexus
installation directory and run:

```powershell
Set-Location .\CopilotIntegration
.\Register-SqlNexusCopilotIntegration.ps1 `
    -Server "localhost" `
    -Database "SqlNexus" `
    -EncryptionMode Mandatory `
    -CertificateValidation Validate
```

Replace the server and database values as needed. The script configures Windows Integrated
Authentication. To compare two collections, add `-Database2 "sqlnexus_fast"`, with `-Database`
identifying the primary or problem run.

Both TLS parameters are required on every registration. `Mandatory` requires encryption and
`Validate` checks the SQL Server certificate. For Force Strict Encryption, use `Strict`.
See [Encryption and certificate validation](AI-Assisted-Analysis#encryption-and-certificate-validation)
for supported combinations and lab-only certificate bypass guidance.

Restart VS Code or start a new Copilot CLI session after registration. In VS Code, open Copilot
Chat and select **SQL Nexus Diagnostic Agent**. In Copilot CLI, run `/agent` and select the same
agent. The agent automatically enables all 39 SQL Nexus MCP tools and starts each investigation by
orienting itself to the available collection data before following the evidence.

All MCP tools are **read-only** and run against the already-imported diagnostic database — never
against a live or production SQL Server.

> **AI-generated content notice** — the agent's analysis is AI-assisted and **may be incomplete
> or inaccurate**. Validate every finding against the underlying SQL Nexus tables before acting on
> it. See the [Transparency Note](https://github.com/microsoft/SqlNexus/blob/master/TRANSPARENCY_NOTE.md).

**For the full step-by-step walkthrough, `-Force` and `-McpOnly` options, manual configuration,
SQL Authentication, the complete tool list, the two-database comparison workflow
(`compare_nexus_databases`), example prompts, and troubleshooting, see [[AI Assisted Analysis]].**

## Tutorials

Look at [SQL LogScout](https://github.com/microsoft/sql_logscout).
