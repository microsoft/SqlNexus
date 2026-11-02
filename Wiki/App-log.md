SQL Nexus maintains a text-based log of activities performed by the application.
This log is the first place to look when an import fails, a report does not render,
or the application behaves unexpectedly.

## Locating the log

1. Using Windows Explorer or Command Prompt navigate to **%temp%** folder

   ```Command Prompt
   C:\> cd %temp%
   ```

1. Open the file **sqlnexus.00x.log**, where X is a number

   ```Command Prompt
   C:\Users\MyUser\AppData\Local\Temp\> notepad sqlnexus.001.log
   ```

### Why is there more than one log file?

Only one instance of `sqlnexus.exe` can write to a given log file at a time. To support
running several instances side by side, the first instance writes to `sqlnexus.000.log`,
the second to `sqlnexus.001.log`, and so on. When an instance starts, SQL Nexus reuses the
lowest-numbered log file that is not currently locked, so **older logs are overwritten**.
Copy a log elsewhere if you need to keep it.

### Changing the log location

When you run SQL Nexus from the command line with the `/O"path"` switch, the log is created
in that folder instead of `%temp%`. See [[How to use SQL Nexus]].

## What gets logged

Each line is written with an inline timestamp and an event severity
(`Information`, `Warning`, `Error`, `Critical`). At a high level the log contains:

| Category | Examples of what you will find |
|---|---|
| **Startup and environment** | SQL Nexus version, command-line switches as they are parsed, log file path |
| **Connection activity** | Target server and database name, connection attempts and failures. **Passwords and credentials are never written to the log** |
| **Database setup** | Creation/drop of the SQL Nexus database, execution of schema and stored-procedure scripts, script integrity verification results |
| **Import orchestration** | The import folder, files discovered, the order importers run in, and which importers were enabled or skipped — including decisions made by the `/M` switch and trace-importer fallback |
| **Rowset importer** | Rowset identifiers matched in each `.OUT`/`.TXT` file, target table names, row counts loaded, rowsets skipped or not recognized |
| **Trace importers** | Trace Event Importer (Managed) or ReadTrace progress, `.xel`/`.trc` files processed, aggregation interval, UTC-vs-local-time handling, and the `ReadTrace.exe` command line when ReadTrace is used |
| **Other importers** | Perfmon (`relog.exe`) processing, ERRORLOG parsing, Linux `.perf` parsing, SQLDiag/AlwaysOn/System Health XEL processing |
| **Post-import processing** | Post-import scripts and analysis steps, PerfStats analysis, runtime counting, report enumeration |
| **Reports** | Report being loaded, refreshed or exported, parameter values applied, and report rendering errors |
| **Errors and exceptions** | Exception messages, source and stack traces for handled and unhandled errors, plus `SqlException` details |
| **Status messages** | The same text that appears in the status bar at the bottom of the main window |

> **Note on data handling:** the log is written locally to your machine only and is never
> transmitted anywhere. It records table names, file names, counts and status — not the
> contents of the rows being imported, and connection strings are masked so passwords are not
> exposed. Because the log can still contain server names, database names, file paths and
> query text taken from error messages, review it before sharing it outside your organization.

## MCP Server log

The SQL Nexus MCP Server (used by GitHub Copilot / Copilot CLI to query an already-imported
SQL Nexus database) writes to its **own** log file, separate from the main application log:

```
%TEMP%\sqlnexus_mcpserver.log
```

It records server startup, the MCP tool requests received, the diagnostic queries executed,
and any errors. Look here when a Copilot agent session fails to return results.

