## Requirements

### For **SQL Nexus version 8.26.09.24** and later, the following must be installed:
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
* An instance of SQL Server 2016 or later to import data into. It does not need to be on the same machine where you run SQL Nexus
* [RML Utilities](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility) — **optional**, see below

> ℹ️ **RML Utilities is no longer required for Extended Events (`.xel`) files.**
> Starting with this release, SQL Nexus ships with a built-in **Trace Event Importer (Managed)** that reads
> `.xel` files directly using the managed TraceEvent (XeLite) library. It is enabled by default and populates
> the same `ReadTrace.*` tables, so all ReadTrace-based reports keep working.
>
> You still need RML Utilities if you want to:
> * Import legacy SQL Trace `.trc` files, or
> * Explicitly use the **ReadTrace** importer (for example via `/MReadTrace`), or
> * Produce `.rml` replay files or per-SPID `.trc` output.

### For SQL Nexus version 7.24.02.18 through 7.25.04.25, the following must be installed:
* [RML Utilities](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
* An instance of SQL Server 2016 or later to import data into. It does not need to be on the same machine where you run SQL Nexus

### For SQL Nexus version 7.23.06.06 or earlier, the following must be installed:
* [CLR Types](https://download.microsoft.com/download/B/1/7/B1783FE9-717B-4F78-A39A-A2E27E3D679D/ENU/x64/SQLSysClrTypes.msi)
* [Report Viewer](https://download.microsoft.com/download/B/1/7/B1783FE9-717B-4F78-A39A-A2E27E3D679D/ENU/x86/ReportViewer.msi)
* [RML Utilities](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
* An instance of SQL Server 2016 or later to import data into. It does not need to be on the same machine where you run SQL Nexus

## Data Collection Tool (Recommended)

SQL Nexus analyzes diagnostic data collected from your SQL Server. You will also need one of the following data collection tools:

* **[SQL LogScout](https://github.com/microsoft/sql_logscout)** *(recommended)* — modern, lightweight collection tool for SQL Server on Windows and Linux
* **[PSSDIAG/DiagManager](https://github.com/microsoft/diagmanager)** — legacy SQLDiag-based collection tool

## Automated Installation of Prerequisites

You can use the following **[PowerShell script](https://github.com/microsoft/SqlNexus/blob/master/Setup-Related/SetupSQLNexusPrereq.ps1)** to install all prerequisites automatically:

1. Copy the file (or its contents to a new file with a **.ps1** extension) onto the target system
2. Run using the following command:

   `powershell -File SetupSQLNexusPrereq.ps1`

## Installation Steps

1. Download the latest binary files from [Releases](https://github.com/microsoft/SqlNexus/releases/)
2. Unzip the file to a local directory
3. Run **sqlnexus.exe** to start SQL Nexus

> ⚠️ **Important:** Do not point SQL Nexus at a production SQL Server instance. Use a dedicated non-production instance for importing and analyzing data.

## Install the SQL Nexus MCP Server and Diagnostic Agent (Optional)

SQL Nexus includes an MCP server and a custom GitHub Copilot agent for AI-assisted analysis of
an imported SQL Nexus database. The registration script configures both VS Code and GitHub
Copilot CLI for the current Windows user and installs the agent with its SQL Nexus diagnostic
skills. It does not install GitHub Copilot itself.

1. If it is not already installed, install [Visual Studio Code](https://code.visualstudio.com/download).
2. Install the [GitHub Copilot extension for VS Code](https://learn.microsoft.com/azure/developer/github-copilot-azure/get-started?pivots=visual-studio-code#install-github-copilot-for-azure). You can also install GitHub Copilot CLI if
   you want to run the agent from a terminal.
3. Open PowerShell in the extracted SQL Nexus installation directory.
4. Run:

   ```powershell
   Set-Location .\CopilotIntegration
   .\Register-SqlNexusCopilotIntegration.ps1 `
      -Server "localhost" `
      -Database "SqlNexus"
   ```

   Replace `localhost` with the non-production SQL Server instance where SQL Nexus imported the
   SQL LogScout data. Replace `SqlNexus` with the SQL Nexus destination database that contains
   that imported collection. These values identify the analysis database, not the production or
   source SQL Server from which SQL LogScout collected the diagnostic data. The script uses
   Windows Integrated Authentication.

5. Restart VS Code or start a new Copilot CLI session.
6. Select **SQL Nexus Diagnostic Agent** from the agent picker and ask a diagnostic question.

The script registers `sqlnexus_mcp` in both clients and installs the shared agent under
`%USERPROFILE%\.copilot\agents`. No manual JSON configuration or agent copy is required.

To compare two imported collections, include a second database:

```powershell
.\Register-SqlNexusCopilotIntegration.ps1 `
   -Server "localhost" `
   -Database "sqlnexus_slow" `
   -Database2 "sqlnexus_fast"
```

Use `-Force` to replace an existing SQL Nexus registration that has different settings. Use
`-McpOnly` to register the MCP server without installing or changing the custom agent. For setup,
usage, manual configuration, and troubleshooting details, see [[AI Assisted Analysis]].
