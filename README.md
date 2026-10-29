# What is SQL Nexus?

SQL Nexus is a tool that helps you identify the root cause of SQL Server performance issues. It loads and analyzes performance data collected by [SQL LogScout](https://github.com/Microsoft/sql_logscout) or [PSSDIAG](https://github.com/Microsoft/diagmanager). It can dramatically reduce the amount of time you spend manually analyzing data. Visit  [Getting Started](https://github.com/Microsoft/SqlNexus/wiki/Getting-Started) page.

# Download

Download the latest build of SQL Nexus from the [releases page](https://github.com/microsoft/SqlNexus/releases/).

# Feature Highlights

1. **AI-assisted diagnostics**: Analyze collected SQL Server diagnostic data with GitHub Copilot through the local SQL Nexus MCP server.
2. **Fast, easy data loading**: Quickly load SQL Trace files, T-SQL script output (including SQL DMV queries), and Performance Monitor logs into a SQL Server database for analysis. These facilities use bulk-load APIs to insert data quickly.
3. **Performance Monitor analysis**: Use the Perfmon importer to import and visualize Windows Performance Monitor data through Perfmon Summary and its CPU, I/O, memory, and network reports. The legacy PAL reports have been removed because SQL Nexus does not provide their required `pal_Counters` catalog.
4. **ERRORLOG and XEL analysis**: Import SQL Server ERRORLOG files and supported Extended Events data with the ERRORLOG and Custom XEL importers.
5. **Trace aggregation**: Identify the top N most expensive queries using the native Trace Managed importer or [RML](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility).
6. **Command-line operation**: Run SQL Nexus from the command line to support scripted and repeatable workflows.
7. **Visual reports**: Explore loaded data through a variety of [charts and reports](https://github.com/Microsoft/SqlNexus/wiki/Reports). The unused legacy WASD reports are no longer included.
8. **Wait statistics analysis**: Visualize blocking and other resource-contention issues in data collected by [PSSDIAG](https://github.com/Microsoft/diagmanager).
9. **Full-featured reporting engine**: SQL Nexus uses the SQL Server Reporting Services client-side report viewer and does not require a Reporting Services instance. Zoom in or out to examine server performance during a particular time window, expand or collapse report regions for easier navigation, and export reports to Excel, PDF, and other formats.

## Import security

Custom XEL imports bind capture paths as SQL parameters. SQL plan files are read locally
from the selected import folder and loaded as XML parameters using the configured database
connection, including its authentication and encryption settings. Post-processing no longer
requires `PostProcess.cmd` or `sqlcmd.exe`; SQL scripts remain integrity-checked before execution.
The managed script runner recognizes standalone `GO` batch separators, including consecutive
separators and scripts using either Windows or Unix line endings.
Custom XEL category failures are reported without stopping other importers or host
post-processing. Post-processing failures stop subsequent processing. Previously completed
imports remain committed; review category failures before using dependent reports. Failure
or cancellation must not be interpreted as a safe no-op or proof of rollback.

General post-processing still runs for partial importer selections such as
`/MCustomXEL`: it cleans and enriches rowset/OS data independently of trace import.
Trace timestamp conversion is a separate, integrity-checked
`TraceTimestampPostProcessing.sql` stage. It runs only when a ReadTrace or managed
Trace Event importer actually completed successfully in the current run, with no
failed or cancelled trace imports. Leftover `ReadTrace.*` tables do not enable this
stage. Missing optional timestamp flags are checked before their SQL is compiled;
UTC offset conversion and already-local timestamp handling remain supported.

## Custom XEL importer

`Custom XEL Importer` is shipped in the signed `CustomXELImporter.dll` assembly and
discovered through the standard `INexusImporter` DLL pipeline, with normal importer options,
status reporting, cancellation, and the existing `/M CustomXEL` selection token.
It is disabled by default. Saved SQLDiag/AlwaysOn options migrate when **Save Import
Options** is enabled. Command-line selection does not change saved GUI preferences.

Deploy the complete build output, including `CustomXELImporter.dll`, beside
`sqlnexus.exe`; copying only the executable is not sufficient. The importer has no
reference back to the host executable and retains its existing ID and option keys.
Within each importer directory, Custom XEL is ordered after the other importers,
before host post-processing. Discovery locations and trust behavior are unchanged.

The manual release scripts include this DLL in signing and signed-file replacement,
and exclude its PDB from the signed customer package. For the DPDiagnosticTools
OneBranch release, refresh the mirrored SQLNexus sources with the updated
`Refresh-BuildFoldersFromSource.ps1` before building; that repository maintains its
own source-folder allowlist and SDK-project restore list.

Only matching files directly in the selected **primary folder** are imported;
subfolders and `SharedOutputFiles` are excluded. Each category is loaded once,
including all matching rollover files and filename prefixes:

| Source mask | Destination |
|---|---|
| `*_SQLDIAG*.xel` | `dbo.tbl_SqlDiagXel` |
| `*AlwaysOn_health*.xel` | `dbo.tbl_AlwaysOnHealthXel` |
| `*system_health*.xel` | `dbo.tbl_SystemHealthXel` |

The parameterized SQL reader is retained to preserve complete SQL-generated
`event_data`, timestamps, file offsets, and the reader's remaining columns. SQL
Server's service account must be able to read the selected paths; remote SQL
instances therefore need a path accessible from that server. No local-reader
fallback silently changes the imported representation.

**Drop existing tables** defaults to enabled and replaces only the corresponding
canonical table. With it disabled, missing tables are created and compatible tables
are appended to (re-importing the same capture appends duplicates). Incompatible
schemas fail explicitly. Each category is transactional: read, schema, or
cancellation failures roll back that category, while earlier categories or importers
may already be committed. Missing categories leave their existing tables untouched.

Legacy `tbl_SQL_Base_*` tables are not renamed, deleted, or hidden behind
compatibility views. If only a legacy table exists, append mode rejects the import:
re-import with **Drop existing tables** enabled to populate the new table. Update
external queries to the canonical names; any retained legacy tables are historical
and are no longer refreshed.

# Common Tasks

1. [How To Use SQL Nexus](https://github.com/microsoft/SqlNexus/wiki/How-to-use-SQL-Nexus)
2. [How to Videos](https://github.com/Microsoft/SqlNexus/wiki/How-To-Videos)
3. [Frequently asked questions(FAQ)](https://github.com/Microsoft/SqlNexus/wiki/FAQ)
4. [Installation](https://github.com/Microsoft/SqlNexus/wiki/Installation)
5. [Sqldiag data collection templates including performance scripts](https://github.com/Microsoft/SqlNexus/wiki/Data-Collection-Templates)
6. [RML Utility/ReadTrace download](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
7. [Top Issues](https://github.com/Microsoft/SqlNexus/wiki/Top-Issues)

# GitHub Copilot + MCP integration

SQL Nexus also includes a local MCP server and integration scripts for AI-assisted diagnostics:

- MCP server docs: [`SqlNexus.McpServer/README.md`](SqlNexus.McpServer/README.md)
- Copilot integration/register scripts: [`CopilotIntegration/README.md`](CopilotIntegration/README.md)


# Microsoft Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


# License
see License.md


# More information
More information and help can be found in the [wiki](https://github.com/Microsoft/SqlNexus/wiki)

The obsolete CHM help files are no longer bundled. Legacy CHM help actions in the application have not been redirected.
