# What is SQL Nexus?

SQL Nexus is a tool that helps you identify the root cause of SQL Server performance issues. It loads and analyzes performance data collected by [SQL LogScout](https://github.com/Microsoft/sql_logscout) or [PSSDIAG](https://github.com/Microsoft/diagmanager). It can dramatically reduce the amount of time you spend manually analyzing data. Visit  [Getting Started](https://github.com/Microsoft/SqlNexus/wiki/Getting-Started) page.

# Latest release
Current release is 7.24.02.18. Please go to [latest release](https://github.com/microsoft/SqlNexus/releases/latest) to download latest build of SQL Nexus.

# Feature Highlights

1. **Fast, easy data loading** : You can quickly and easily load SQL Trace files; T-SQL script output, including SQL DMV queries; and Performance Monitor logs into a SQL Server database for analysis. All three facilities use bulk load APIs to insert data quickly. You can also create your own importer for a custom file type.
2. **Visualize loaded data via reports** : Once the data is loaded, you can fire up several different  [charts and reports](https://github.com/Microsoft/SqlNexus/wiki/Reports) to analyze it.
3. **Trace aggregation**  to show the TOP N most expensive queries (using  [RML](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)).
4. **Wait stats analysis**  for visualizing blocking and other resource contention issues ( [based on pssdiag](https://github.com/Microsoft/diagmanager)).
5. **Full-featured reporting engine** : SQL Nexus uses the SQL Server Reporting Services client-side report viewer (it does not require an RS instance). You can create reports for Nexus from either the RS report designer or the Visual Studio report designer. You can also modify the reports that ship with Nexus using either facility. Zoom in/Zoom out to view server performance during a particular time window. Expand/collapse report regions (subreports) for easier navigation of complex data. Export or email reports directly from SQL Nexus. Nexus supports exporting in Excel, PDF, and several other formats.
6. **Extensibility** : You can use the existing importers to load the output from any DMV query into a table, and any RS reports you drop in the Reports folder will automatically show up in the reports task pane. If you want, you can even add a new data importer for a new data type. SQL Nexus will automatically &quot;fix up&quot; the database references in your reports to reference the current server and database, and it will provide generic parameter prompting for any parameters your reports support.

# Common Tasks

1. [How To Use SQL Nexus](https://github.com/microsoft/SqlNexus/wiki/How-to-use-SQL-Nexus)
2. [How to Videos](https://github.com/Microsoft/SqlNexus/wiki/How-To-Videos)
3. [Frequently asked questions(FAQ)](https://github.com/Microsoft/SqlNexus/wiki/FAQ)
4. [Installation](https://github.com/Microsoft/SqlNexus/wiki/Installation)
5. [Sqldiag data collection templates including performance scripts](https://github.com/Microsoft/SqlNexus/wiki/Data-Collection-Templates)
6. [RML Utility/ReadTrace download](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
7. [Top Issues](https://github.com/Microsoft/SqlNexus/wiki/Top-Issues)


# Microsoft Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


# License
see License.md


# More information
More information and help can be found in the [wiki](https://github.com/Microsoft/SqlNexus/wiki)
