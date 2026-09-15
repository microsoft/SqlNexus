# General Performance Degradation - Complete Diagnostic Workflow

## SYMPTOM
"Database is slow" / "Queries taking longer than usual" / "Application performance degraded"

---

## DECISION TREE

### Is this happening RIGHT NOW or intermittent?

#### ✅ **RIGHT NOW (currently slow)**

**LogScout Scenario**: `GeneralPerf`  
**Command**: `.\SQL_LogScout.cmd start GeneralPerf`  
**Duration**: Run for at least 3-5 minutes during issue

**Analysis Sequence**:

1. **Check collection timeframe** → Query #3
2. **Identify overall bottleneck** → Query #4
3. **Find slowest queries** → Query #1
4. **For top queries, check waits** → Query #5
5. **Check CPU utilization** → Query #17

**Bottleneck Indicators** (from Query #4):
- **PAGEIOLATCH_*** → I/O bottleneck (use scenario-io.md)
- **LCK_*** → Blocking bottleneck (use scenario-blocking.md)
- **SOS_SCHEDULER_YIELD** → CPU bottleneck (use scenario-cpu.md)
- **WRITELOG** → Transaction log bottleneck
- **ASYNC_NETWORK_IO** → Network/client bottleneck
- **CXPACKET** → Parallelism contention

**Root Cause Categories**:
- Specific resource bottleneck (follow respective scenario)
- Problematic query patterns (missing indexes, scans)
- External resource constraints (storage, network)

---

#### ⏱️ **INTERMITTENT (specific time/condition)**

##### Can you reproduce the issue?

**YES (reproducible)**:
- **LogScout Scenario**: `DetailedPerf` (statement-level detail)
- **Command**: `.\SQL_LogScout.cmd start DetailedPerf`
- **Timing**: Start before issue, stop after issue resolves
- **Analysis**: Same as GeneralPerf + statement drilldown

**NO (not reproducible / historical)**:
- **LogScout Scenario**: `LightPerf` (continuous monitoring)
- **Command**: `.\SQL_LogScout.cmd start LightPerf`
- **Duration**: Run continuously until issue recurs
- **Analysis**: Track trends over time using Query #17, Query #4

##### Does it correlate with specific activity?

**YES (e.g., "slow during backups")**:
- **Combine scenarios**: `GeneralPerf + BackupRestore`
- **Command**: `.\SQL_LogScout.cmd start GeneralPerf,BackupRestore`

---

### Is it specific queries or entire system?

#### **SPECIFIC QUERIES**
- **Scenario**: `DetailedPerf` (get statement-level traces)
- **Analysis**: Focus on specific query patterns
  - Find query by application (Query #12)
  - Analyze execution variance (Query #2)
  - Check execution plans

#### **ENTIRE SYSTEM**
- **Scenario**: `GeneralPerf`
- **Analysis**: System-wide wait analysis (Query #4)

---

## EMBEDDED QUERIES

### Query #0: Performance Health Summary (One-Stop Triage)
**MCP Tool**: `get_performance_summary`  
**Purpose**: Comprehensive health snapshot covering CPU, top waits, blocking count, spinlock contention, and memory clerk distribution in a single call. Returns the most important metrics across all categories so you can identify which scenario file to open next.  
**Use When**: Starting a cold investigation with no prior context — use this first, then jump to the matching scenario (cpu/io/blocking/memory)

> Call `get_performance_summary` MCP tool directly. No manual SQL needed.

---

### Query #1: Top 50 Longest-Running Queries by Duration
**MCP Tool**: `get_top_queries_by_duration`  
**Purpose**: Identify the slowest queries by total duration  
**Use When**: General performance investigation  

```sql
SELECT TOP 50 
    SUM(b.Duration)/1000 AS Duration_ms, 
    SUM(b.CPU) AS CPU_ms, 
    SUM(b.Duration)/1000 - SUM(b.CPU) AS WaitTime_ms, 
    CONVERT(DECIMAL(8,2), 
        (((SUM(b.Duration)/1000.00) - SUM(b.CPU)) / 
        (CASE WHEN SUM(b.Duration)/1000 = 0 THEN 1 ELSE SUM(b.Duration)/1000 END)) * 100
    ) AS WaitPercentage, 
    SUM(b.Reads) AS Reads, 
    COUNT(*) AS Executions, 
    (SUM(b.Duration)/1000) / (CASE WHEN COUNT(*) = 0 THEN 1 ELSE COUNT(*) END) AS AvgDuration, 
    SUM(b.CPU) / COUNT(*) AS AvgCPU, 
    SUBSTRING(ub.NormText, 1, 200) AS NormText, 
    b.HashID
FROM ReadTrace.tblBatches b 
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
GROUP BY ub.NormText, b.HashID
ORDER BY Duration_ms DESC;
```

**Output Interpretation**:
- **Duration_ms**: Total time query spent executing
- **WaitTime_ms**: Time spent waiting (not on CPU)
- **WaitPercentage**: % of time waiting (high % = waits are bottleneck)
- **AvgDuration**: Average execution time per instance
- Use **HashID** to drill into specific query details

---

### Query #2: Stats for Specific Query (by HashID)
**MCP Tool**: `get_query_execution_details`  
**Purpose**: Detailed execution history for a specific query  
**Use When**: Drilling into a specific slow query from Query #1

```sql
-- Replace @HashID with the HashID from Query #1
DECLARE @HashID BIGINT = 1234567890; -- REPLACE THIS

SELECT 
    b.StartTime,
    b.EndTime,
    b.Duration/1000 AS Duration_ms,
    b.CPU AS CPU_ms,
    b.Duration/1000 - b.CPU AS WaitTime_ms,
    b.Reads,
    b.Writes,
    b.RowCounts,
    ub.NormText
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
WHERE b.HashID = @HashID
ORDER BY b.StartTime DESC;
```

---

### Query #3: Collection Time Window
**MCP Tool**: `get_collection_time_range`  
**Purpose**: Identify when diagnostics were collected  
**Use When**: Starting analysis to understand data coverage

```sql
SELECT 
    MIN(runtime) AS CollectionStart,
    MAX(runtime) AS CollectionEnd,
    DATEDIFF(MINUTE, MIN(runtime), MAX(runtime)) AS DurationMinutes
FROM tbl_RUNTIMES;
```

---

### Query #4: Overall Wait Statistics
**MCP Tool**: `analyze_wait_stats`  
**Purpose**: Identify primary bottleneck categories  
**Use When**: First step in performance analysis

```sql
SELECT TOP 20
    wait_type,
    SUM(wait_time_ms) AS total_wait_ms,
    SUM(wait_time_ms) - SUM(signal_wait_time_ms) AS resource_wait_ms,
    SUM(signal_wait_time_ms) AS signal_wait_ms,
    SUM(waiting_tasks_count) AS wait_count,
    CAST(100.0 * SUM(wait_time_ms) / SUM(SUM(wait_time_ms)) OVER() AS DECIMAL(5,2)) AS pct
FROM tbl_OS_WAIT_STATS
WHERE wait_type NOT IN (
    'BROKER_EVENTHANDLER', 'BROKER_RECEIVE_WAITFOR', 'BROKER_TASK_STOP',
    'BROKER_TO_FLUSH', 'BROKER_TRANSMITTER', 'CHECKPOINT_QUEUE',
    'CLR_AUTO_EVENT', 'CLR_MANUAL_EVENT', 'CLR_SEMAPHORE',
    'DBMIRROR_DBM_EVENT', 'DBMIRROR_DBM_MUTEX', 'DBMIRROR_EVENTS_QUEUE',
    'DBMIRRORING_CMD', 'DIRTY_PAGE_POLL', 'DISPATCHER_QUEUE_SEMAPHORE',
    'FT_IFTS_SCHEDULER_IDLE_WAIT', 'FT_IFTSHC_MUTEX', 'HADR_CLUSAPI_CALL',
    'HADR_FILESTREAM_IOMGR_IOCOMPLETION', 'HADR_LOGCAPTURE_WAIT',
    'HADR_NOTIFICATION_DEQUEUE', 'HADR_TIMER_TASK', 'HADR_WORK_QUEUE',
    'LAZYWRITER_SLEEP', 'LOGMGR_QUEUE', 'ONDEMAND_TASK_QUEUE',
    'PREEMPTIVE_OS_LIBRARYOPS', 'PREEMPTIVE_OS_COMOPS',
    'PREEMPTIVE_OS_CRYPTOPS', 'PREEMPTIVE_OS_PIPEOPS',
    'PREEMPTIVE_HADR_LEASE_MECHANISM', 'PWAIT_ALL_COMPONENTS_INITIALIZED',
    'QDS_ASYNC_QUEUE', 'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
    'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', 'QDS_SHUTDOWN_QUEUE',
    'REQUEST_FOR_DEADLOCK_SEARCH', 'RESOURCE_QUEUE', 'SERVER_IDLE_CHECK',
    'SLEEP_BPOOL_FLUSH', 'SLEEP_DBSTARTUP', 'SLEEP_DCOMSTARTUP',
    'SLEEP_MASTERDBREADY', 'SLEEP_MASTERMDREADY', 'SLEEP_MASTERUPGRADED',
    'SLEEP_MSDBSTARTUP', 'SLEEP_SYSTEMTASK', 'SLEEP_TASK',
    'SLEEP_TEMPDBSTARTUP', 'SNI_HTTP_ACCEPT', 'SP_SERVER_DIAGNOSTICS_SLEEP',
    'SQLTRACE_BUFFER_FLUSH', 'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
    'SQLTRACE_WAIT_ENTRIES', 'WAIT_FOR_RESULTS', 'WAITFOR',
    'WAITFOR_TASKSHUTDOWN', 'WAIT_XTP_HOST_WAIT', 'WAIT_XTP_OFFLINE_CKPT_NEW_LOG',
    'WAIT_XTP_CKPT_CLOSE', 'XE_DISPATCHER_JOIN', 'XE_DISPATCHER_WAIT',
    'XE_TIMER_EVENT'
)
GROUP BY wait_type
ORDER BY total_wait_ms DESC;
```

**Interpretation Guide**:
- **PAGEIOLATCH_***: I/O bottleneck → investigate storage/file placement
- **LCK_M_***: Blocking → investigate locking/transactions
- **SOS_SCHEDULER_YIELD**: CPU pressure → find expensive queries
- **WRITELOG**: Transaction log bottleneck → log file I/O or log growth
- **RESOURCE_SEMAPHORE**: Memory grant waits → memory pressure
- **ASYNC_NETWORK_IO**: Network/client slow to consume results
- **CXPACKET**: Parallelism contention → review MAXDOP

---

### Query #5: Find Waits for Specific Query
**MCP Tool**: `get_waits_for_query`  
**Purpose**: Identify what a specific query is waiting on  
**Use When**: Drilling into why a specific query is slow

```sql
-- Get waits for specific HashID
DECLARE @HashID BIGINT = 1234567890; -- REPLACE THIS

SELECT 
    w.wait_type,
    COUNT(*) AS wait_occurrences,
    SUM(w.wait_duration_ms) AS total_wait_ms,
    AVG(w.wait_duration_ms) AS avg_wait_ms
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblWaits w ON b.EventSequence = w.EventSequence
WHERE b.HashID = @HashID
GROUP BY w.wait_type
ORDER BY total_wait_ms DESC;
```

---

### Query #17: CPU Utilization Over Time
**MCP Tool**: `get_sql_cpu_usage_over_time`  
**Purpose**: Track CPU usage trends  
**Use When**: Investigating high CPU or performance degradation

```sql
SELECT 
    runtime AS EventTime,
    SQLProcessUtilization AS SQL_CPU_Pct,
    SystemIdle AS System_Idle_Pct,
    100 - SystemIdle - SQLProcessUtilization AS Other_Process_CPU_Pct
FROM tbl_SQL_CPU_HEALTH
ORDER BY runtime;
```

**Interpretation**:
- **SQL_CPU_Pct > 80%**: SQL Server is CPU-bound
- **System_Idle_Pct < 10%**: Overall system CPU saturated
- **Other_Process_CPU_Pct high**: Non-SQL processes competing

---

## NEXT STEPS BASED ON FINDINGS

### If I/O Bottleneck Found (PAGEIOLATCH_*)
→ Use [scenario-io.md](scenario-io.md) for detailed I/O analysis

### If Blocking Found (LCK_M_*)
→ Use [scenario-blocking.md](scenario-blocking.md) for detailed blocking analysis

### If CPU Pressure Found (SOS_SCHEDULER_YIELD)
→ Use [scenario-cpu.md](scenario-cpu.md) for detailed CPU analysis

### If Memory Pressure Found (RESOURCE_SEMAPHORE)
→ Use [scenario-memory.md](scenario-memory.md) for detailed memory analysis

### If Specific Query Issues
- Use Query #2 to analyze execution history
- Use Query #5 to identify query-specific waits
- Review execution plans from XEvent data
- Check for parameter sniffing, missing indexes, excessive scans
