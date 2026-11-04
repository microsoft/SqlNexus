# SQL Server log collection for SQL Nexus

SQL Nexus requires diagnostic data collected from your SQL Server instance. 
This page describes the supported data collection methods.

## Recommended: SQL LogScout (Modern Tool)

[SQL LogScout](https://github.com/microsoft/sql_logscout) is the recommended 
tool for collecting SQL Server diagnostic data on both **Windows and Linux**. 
It collects Extended Events (XEL), DMV output, Performance Monitor logs, and 
more — all of which SQL Nexus can import and analyze.

1. Download SQL LogScout from [https://github.com/microsoft/sql_logscout](https://github.com/microsoft/sql_logscout)
2. Run it against your SQL Server instance to collect diagnostic data
3. Point SQL Nexus at the output folder to import and analyze

For an **All Instances** collection, select the subfolder for the SQL Server instance you want to
analyze, not the common parent folder. SQL Nexus also scans a direct sibling named
`SharedOutputFiles` automatically, bringing in the non-instance-specific host/OS diagnostics that
SQL LogScout stores there. See [[How to use SQL Nexus#sql-logscout-all-instances-collections]] for
the expected layout, duplicate handling, and importer-specific behavior.

SQL LogScout supports **SQL Server 2012 and later**, including SQL Server on Linux.

## Legacy: PSSDIAG / DiagManager (SQLDiag-based)

For older environments or specific scenarios, 
[DiagManager/PSSDIAG](https://github.com/microsoft/diagmanager) can be used to 
generate SQLDiag-based collection configurations. This method uses `sqldiag.exe`, 
which ships as part of the SQL Server installation.

> **Note:** SQL Server Profiler trace (`.trc`) collection via SQLDiag has been 
> deprecated since SQL Server 2012. Extended Events (`.xel`) is the recommended 
> replacement and is fully supported by SQL Nexus.

See the [DiagManager wiki](https://github.com/microsoft/diagmanager/wiki) for 
instructions on how to configure and run data collection.

## What SQL Nexus Can Import

Once data is collected, SQL Nexus automatically detects and imports the following:

| Data Type | File Format | Notes |
|-----------|-------------|-------|
| DMV / rowset output | `.out` text files | Produced by SQL LogScout or SQLDiag |
| Extended Events | `.xel` | SQLDiag XEL, Always On health, System Health sessions |
| SQL Trace | `.trc` | Processed via RML Utilities / ReadTrace |
| Performance Monitor | `.blg` | Windows Perfmon logs |
| Linux performance data | `.tsv` / converted `.blg` | IO stats, CPU, memory, networking, process stats |

## Tutorials

- [SQL LogScout documentation](https://github.com/microsoft/sql_logscout)
- [DiagManager/PSSDIAG wiki](https://github.com/microsoft/diagmanager/wiki)
- [RML Utilities / ReadTrace](https://github.com/Microsoft/SqlNexus/wiki/RML-Utility)
