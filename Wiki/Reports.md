## Overview

This page describes the reports available in SQL Nexus. Reports are organized 
into categories in the left navigation pane. Reports are populated after 
importing data collected by [SQL LogScout](https://github.com/microsoft/sql_logscout) 
or [PSSDIAG/DiagManager](https://github.com/microsoft/diagmanager).

---

## Performance Overview

### Analysis Summary
Highlights common performance issues automatically detected from your collected 
data — including wait stats, missing indexes, non-Microsoft loaded modules, 
spinlock issues, and AG health. **Start here** for a quick overview of what 
SQL Nexus found.

[[/Images/Analysis_Summary.png]]

### Bottleneck Analysis
Identifies the dominant bottleneck (CPU, Memory, I/O, Waits) on your SQL Server 
during the collection period. Shows CPU usage over time broken down by SQL Server 
vs. non-SQL processes, and top wait categories.

- Data sources: `_TimeStamp_PerfStats.out`, `_HighCPU_perfstats.out`, Perfmon `.blg`
- Key tables: `tbl_OS_WAIT_STATS`, `CounterData`, `CounterDetails`

[[/Images/BottleneckAnalysis.png]]

### System Thread CPU Consumption
Shows CPU utilization by SQL Server system threads over time. CPU is normalized
by the number of logical processors, and the report can be filtered by time range
and CPU percentage threshold.

- Data source: Perf Stats Collector
- Key table: `tbl_System_Requests`

### Virtual File Stats
Shows read and write latency over time for SQL Server database files. The report
computes deltas between `sys.dm_io_virtual_file_stats` snapshots and supports
drill-through to file-level details.

- Data source: Perf Stats Collector
- Key table: `tbl_FileStats`

---

## Query Analysis

### ReadTrace Reports (Top Queries, Grouping, Statements, Lineage, Warnings)
A family of reports powered by RML Utilities/ReadTrace that analyze SQL trace 
(`.trc`) or XEL (`.xel`) data. Includes:

- **ReadTrace Main** — top queries by duration, CPU, and reads
- **Top N Unique Batches / Statements** — aggregate stats per normalized query
- **Batch / Statement Details** — drill into individual executions of a query
- **Grouping** — group queries by application, database, or login
- **Lineage** — track a query's execution history over time
- **Interesting Events / Warnings** — hash warnings, sort warnings, missing join predicates, exceptions

> These reports require the ReadTrace importer to be enabled and RML Utilities 
> to be installed. See [[RML Utility]].

### Active CPU Consuming Queries
Shows the top CPU-intensive requests that were still running when data collection
ended. These requests are not included in ReadTrace aggregates because ReadTrace
only counts completed executions.

- Data source: Perf Stats Collector
- Key tables: `tbl_REQUESTS`, `tbl_NOTABLEACTIVEQUERIES`

### Top Query Plan Analysis
Analyzes query plans for the top CPU-consuming queries. Shows tables referenced 
by those plans — useful for prioritizing which tables need statistics updates.

[[/Images/Top_Query_Plan_Analysis.png]]

### Query Hash
Groups queries by query hash to identify similar query patterns consuming 
the most resources.

### Query Store Reports
Shows top queries from Query Store data (`tbl_query_store_*`) if collected. 
Includes **Query Store** and **Query Store Details** drill-through reports, 
as well as **TopN Resources by QDS**.

> Requires Query Store to be enabled on the monitored database and Query Store 
> data to have been collected by SQL LogScout or PSSDIAG.

---

## Wait Statistics & Blocking

### Wait Details
Detailed breakdown of wait types over time from periodic DMV snapshots.

### Other Waits
Focuses on non-ignorable waits outside the main Bottleneck Analysis categories.

### Lock Summary
Shows the number of locks held or waited on, aggregated by resource type over 
time. High lock counts can indicate blocking risk and increased CPU consumption.

- Data source: `*_Perf_Stats_Startup.OUT`
- Key table: `tbl_LockSummary`

[[/Images/Lock_Summary.png]]

### Blocking and Wait Statistics
Overview of blocking events and associated wait statistics captured during 
the collection period.

### Blocking Chain Detail
Drill-through report showing the full blocking chain — which session is the 
head blocker and which sessions are being blocked.

### Blocking Runtime Detail
Shows blocking events correlated with runtime timestamps for timeline analysis.

---

## Memory

### Memory Clerks
Distribution of SQL Server memory usage across memory clerks. Useful for 
diagnosing unexpected memory pressure from specific components (e.g. plan cache, 
buffer pool, CLR).

### Memory Brokers
Shows SQL Server internal memory broker activity and how memory is being 
allocated across components.

### Query Execution Memory
Shows query memory grant information — requested vs. granted vs. used memory. 
Useful for diagnosing memory grant waits (`RESOURCE_SEMAPHORE`).

### Working Set Trim
Shows evidence of Windows trimming SQL Server's working set, which can cause 
sudden performance drops.

---

## Performance Monitor (Perfmon)

A suite of reports driven by Perfmon (`.blg`) data:

| Report | What it shows |
|--------|--------------|
| **Perfmon** | All captured Perfmon counters |
| **Perfmon CPU** | CPU-related counters (compilations, batch requests, etc.) |
| **Perfmon I/O** | Disk I/O counters (avg disk sec/transfer, queue length) |
| **Perfmon Memory** | Memory counters (page life expectancy, memory grants pending) |
| **Perfmon Network** | Network throughput counters |
| **PAL** | Performance Analysis of Logs — threshold-based analysis of Perfmon counters |

> Perfmon data is collected via SQL LogScout (`GeneralPerf`, `DetailedPerf`) 
> or PSSDIAG. The data is imported via the Perfmon Importer.

---

## Always On / High Availability

### AlwaysOn AG Basics
Overview of Availability Group configuration — AG names, replicas, databases, 
and listener details.

### AlwaysOn AG Details
Detailed replica state information including operational state, synchronization 
health, connection state, and last connect error details.

---

## Server & Database Configuration

### Server Configuration
Common server properties including:
- General server information (`tbl_ServerProperties`, `tbl_SYSINFO`)
- `sp_configure` settings
- Trace flags
- Startup parameters

[[/Images/Server_Configuration.png]]

### Database Configuration
Database-level settings such as auto close, auto shrink, auto create/update 
stats, recovery model, and compatibility level. Highlights databases with 
potentially harmful settings in red.

- Data source: `*_MiscPssdiagInfo_Startup.OUT`
- Key tables: `tbl_DatabaseFiles`, `tbl_SysDatabases`

[[/Images/Database_Configuration.png]]

### SQL Assessment API Rules
Displays recommendations collected from the SQL Assessment API, ordered by
severity. Each row includes the check name, severity, recommendation message,
and a link to supporting guidance. Medium- and high-severity findings are also
surfaced in Analysis Summary.

- Data source: SQL Base Collector
- Key table: `tbl_assessment_api`

[[/Images/SQL_Assessment_API.png]]

### Indexes and Stats
Shows index and statistics metadata captured for user databases. Use it to review
index definitions, statistics properties, and the state of statistics at collection
time.

- Data source: Perf Stats Collector
- Key table: `tbl_SysIndexes`

### Missing Indexes
Shows missing index recommendations from `sys.dm_db_missing_index_details` 
snapshots, ordered by improvement measure.

### Loaded Modules
Non-Microsoft modules loaded into SQL Server memory. Useful when diagnosing 
unexplained instability or performance issues caused by third-party drivers 
or antivirus software.

### Active Processes
Shows operating-system processes that were active during collection, including
CPU time, memory usage, session, status, user, and window title when available.

- Data source: SQL Base Collector
- Key table: `tbl_ActiveProcesses_OS`

> Process names, user names, and window titles can contain sensitive information.
> Review these fields before sharing an exported report.

### Active Traces and XEvents
Lists active non-Microsoft SQL Trace and Extended Events sessions and the events
they collect. Potentially expensive events are flagged to help identify tracing
that may add overhead.

- Data source: Perf Stats Collector
- Key tables: `tbl_profiler_trace_summary`, `tbl_profiler_trace_event_details`, `tbl_XEvents`

### Filter Drivers
Separates non-Microsoft file-system filter drivers from the complete filter-driver
and instance list. This is useful for identifying antivirus, backup, encryption,
and storage filters that may affect SQL Server I/O.

- Data source: SQL Base Collector
- Key tables: `tbl_fltmc_filters`, `tbl_fltmc_instances`

[[/Images/Filter_Drivers.png]]

### Running Device Drivers
Lists device-driver files running on the operating system, including description,
vendor, product version, file size, and last-write time.

- Data source: SQL Base Collector
- Key table: `tbl_running_drivers`

[[/Images/Running_Drivers.png]]

---

## Replication

### Replication Topology
Displays the replication hierarchy from distributor and publisher through
publications, articles, and subscribers. It supports transactional and merge
replication metadata when the Replication collector was included.

- Data source: Replication Collector
- Key tables: `tbl_repl_sysservers`, `tbl_repl_mspublications`,
  `tbl_repl_msreplservers`, `tbl_repl_msarticles`, `tbl_repl_mssubscriptions`

---

## Setup Health

### Missing MSI or MSP Packages
Shows SQL Server installation packages that are missing from the Windows Installer
cache or may be corrupt. Missing cached packages can prevent servicing, upgrades,
or uninstallation.

- Data source: Setup Collector
- Key table: `tbl_setup_missing_msi_msp_packages`

---

## TempDB

### TempDB Space Use
Shows TempDB space consumption over time, broken down by file and session. 
Useful for diagnosing TempDB exhaustion issues.

---

## Spinlock

Spinlock statistics including delta spins and backoffs over time. Drill-through 
is available for individual spinlock analysis.

- Key table: `tbl_SPINLOCKSTATS`

[[/Images/Spinlock_Report.png]]
[[/Images/Spinlock_Details.png]]

---

## Errors and Warnings
Shows SQL Server error and warning events captured in trace/XEL data including 
exceptions, attention events, and other notable events from `ReadTrace.tblInterestingEvents`.

---

## Reviewing Perfmon Data in Excel

As an alternative to the built-in Perfmon reports, you can export Perfmon data 
directly from the SQL Nexus database and visualize it in Excel.

**Step 1 — Query min/max/avg values per counter:**

```sql
    SELECT objectname, countername, instancename, MIN(countervalue) AS 'MIN', 
        MAX(countervalue) AS 'MAX', AVG(countervalue) AS 'AVG' FROM Counterdetails a
    INNER JOIN CounterData b ON a.counterid = b.counterid
    GROUP BY objectname, countername, instancename
    ORDER BY objectname, countername, instancename
```


[[/Images/QueryResult.png]]

**Step 2 — Query full time-series data for graphing:**

```sql
SELECT objectname, countername, instancename, CounterDateTime, CounterValue 
FROM CounterDetails a 
  INNER JOIN CounterData b ON a.counterid = b.counterid 
ORDER BY objectname, countername, instancename, a.counterid, recordindex
```


Save the results as a CSV file, then create a pivot table and chart in Excel to visualize trends and spot spikes.

[[/Images/csvOutput.png]]
[[/Images/PivottableinExcel.png]]

> **Tip:** If the query returns a very large dataset, add a `CounterDateTime` 
> filter to scope it to a specific time window of interest.

