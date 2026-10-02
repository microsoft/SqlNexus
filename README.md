# What is SQL Nexus?

SQL Nexus is a tool that helps you identify the root cause of SQL Server performance issues. It loads and analyzes performance data collected by [SQL LogScout](https://github.com/Microsoft/sql_logscout) or [PSSDIAG](https://github.com/Microsoft/diagmanager). It can dramatically reduce the amount of time you spend manually analyzing data. Visit  [Getting Started](https://github.com/Microsoft/SqlNexus/wiki/Getting-Started) page.

# Download

Download the latest build of SQL Nexus from the [releases page](https://github.com/microsoft/SqlNexus/releases/).

# Feature Highlights

1. **AI-assisted diagnostics**: Analyze collected SQL Server diagnostic data with GitHub Copilot through the local SQL Nexus MCP server.
2. **Fast, easy data loading**: Quickly load SQL Trace files, T-SQL script output (including SQL DMV queries), and Performance Monitor logs into a SQL Server database for analysis. These facilities use bulk-load APIs to insert data quickly.
3. **Performance Monitor analysis**: Use the Perfmon importer to import and visualize Windows Performance Monitor data.
4. **ERRORLOG and XEL analysis**: Import SQL Server ERRORLOG files and supported Extended Events data with the ERRORLOG and Custom XEL importers.
5. **Trace aggregation**: Identify the top N most expensive queries using the native Trace Managed importer or [RML](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility).
6. **Command-line operation**: Run SQL Nexus from the command line to support scripted and repeatable workflows.
7. **Visual reports**: Explore loaded data through a variety of [charts and reports](https://github.com/Microsoft/SqlNexus/wiki/Reports).
8. **Wait statistics analysis**: Visualize blocking and other resource-contention issues in data collected by [PSSDIAG](https://github.com/Microsoft/diagmanager).
9. **Full-featured reporting engine**: SQL Nexus uses the SQL Server Reporting Services client-side report viewer and does not require a Reporting Services instance. Zoom in or out to examine server performance during a particular time window, expand or collapse report regions for easier navigation, and export reports to Excel, PDF, and other formats.


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
