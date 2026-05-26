# High CPU Utilization - Complete Diagnostic Workflow

## SYMPTOM
"SQL Server using high CPU" / "CPU at 100%" / "Server unresponsive" / "SOS_SCHEDULER_YIELD waits"

---

## DECISION TREE

### Is CPU high RIGHT NOW?

#### ✅ **YES (currently high)**

**LogScout Scenario**: `GeneralPerf` or `HighCPU_perfstats` (if available)  
**Command**: `.\SQL_LogScout.cmd start GeneralPerf`  
**Duration**: 3-5 minutes

**Analysis Sequence**:

1. **Check SQL vs System CPU** → Query #17
2. **If SQL CPU high, find top CPU queries** → Query #18
3. **Check for SOS_SCHEDULER_YIELD waits** → Query #4 (filter for SOS_SCHEDULER_YIELD)
4. **Check for CXPACKET waits (parallelism)** → Query #4 (filter for CXPACKET)
5. **Check spinlock contention** → Query #20 (if table exists)

**Indicators from Query #17**:
- **SQLProcessUtilization > 80%**: SQL Server is consuming CPU
- **OtherProcessCPU > 50%**: Non-SQL process issue
- **SystemIdleCPU < 5%**: System-wide saturation

**Root Cause Categories**:
- Expensive queries (scans, missing indexes)
- Parameter sniffing causing bad plans
- High compilation (plan cache churn)
- Parallelism issues (CXPACKET contention)
- Spinlock contention (internal latch)

---

#### ❌ **NO (intermittent / historical)**

**LogScout Scenario**: `LightPerf` (continuous monitoring)  
**Command**: `.\SQL_LogScout.cmd start LightPerf`  
**Analysis**: Identify CPU spike patterns over time using Query #17

---

### Is it SQL Server or other processes?

#### **SQL SERVER** (SQLServerCPU high)
→ Proceed with query analysis (Query #18)

#### **OTHER PROCESSES** (OtherProcessCPU high)
**LogScout Scenario**: `ProcessMonitor` (if Sysinternals available)  
**Command**: `.\SQL_LogScout.cmd start ProcessMonitor`  
**Note**: Requires Sysinternals Procmon.exe  
**Analysis**: Identify non-SQL CPU consumer

#### **BOTH** (system-wide)
→ **Next Steps**: Infrastructure review (VM overcommit, etc.)

---

### Does CPU spike correlate with specific activity?

#### **YES** (e.g., "CPU spikes during report generation")
**LogScout Scenario**: `DetailedPerf` (statement-level trace)  
**Command**: `.\SQL_LogScout.cmd start DetailedPerf`  
**Analysis**: Filter by application/time using Query #13

#### **NO** (random spikes)
**LogScout Scenario**: `LightPerf` (long-term monitoring)  
**Analysis**: Correlate spikes with wait patterns

---

### Advanced: CPU issue persists without clear query cause

**LogScout Scenario**: `WPR` (Windows Performance Recorder)  
**Command**: `.\SQL_LogScout.cmd start WPR`  
**Note**: Deep Windows performance profiling  
**Analysis**: ETW trace analysis (Windows Performance Analyzer)

**Consider**: Compilation/recompilation storms
- Check plan cache churn
- Check recompile events

---

## EMBEDDED QUERIES

### Query #17a: CPU Utilization — Comprehensive Perfmon Analysis
**MCP Tool**: `analyze_cpu_usage`  
**Purpose**: Full CPU analysis combining Perfmon counter data with SQL ring-buffer. Returns a perfmon_cpu_summary (max/avg SQL CPU%, sustained high-CPU runs ≥3 consecutive samples >70%) plus raw per-sample SQL/non-SQL/idle breakdown. Preferred over Query #17 when Perfmon CounterData is available — automatically falls back to ring buffer if absent.  
**Use When**: First step in any CPU investigation; provides the complete picture before drilling into specific queries

> Call `analyze_cpu_usage` MCP tool directly. No manual SQL needed.

---

### Query #17: CPU Utilization Over Time
**MCP Tool**: `get_sql_cpu_usage_over_time`  
**Purpose**: Track CPU usage trends and identify if SQL Server is the CPU consumer  
**Use When**: First step in CPU investigation

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
- **SQL_CPU_Pct > 80%**: SQL Server is CPU-bound → Find expensive queries with Query #18
- **System_Idle_Pct < 10%**: Overall system CPU saturated → Check other processes
- **Other_Process_CPU_Pct high**: Non-SQL processes competing → Use ProcessMonitor

**Visualization**: Plot over time to see patterns (constant high, spikes, correlated with specific times)

---

### Query #18: Top Queries by CPU Time
**MCP Tool**: `get_top_cpu_queries`  
**Purpose**: Find most CPU-intensive queries  
**Use When**: SQL_CPU_Pct is high from Query #17

```sql
SELECT TOP 50
    SUM(b.CPU) AS Total_CPU_ms,
    COUNT(*) AS Executions,
    SUM(b.CPU) / COUNT(*) AS Avg_CPU_ms,
    SUM(b.Duration)/1000 AS Total_Duration_ms,
    SUBSTRING(ub.NormText, 1, 200) AS Query,
    b.HashID
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
GROUP BY ub.NormText, b.HashID
ORDER BY Total_CPU_ms DESC;
```

**Output Interpretation**:
- **Total_CPU_ms**: Cumulative CPU time (high = query runs often or is very expensive)
- **Executions**: How many times the query ran
- **Avg_CPU_ms**: Average CPU per execution (high = inefficient query)
- **HashID**: Use with Query #2 to drill into specific executions

**Analysis**:
1. **High Total_CPU + High Executions**: Frequently-run query → Optimize or cache results
2. **High Avg_CPU + Low Executions**: Expensive one-off query → Review execution plan, add indexes
3. **Scans in NormText**: Look for `Scan` in query text → Missing indexes

**Action**: For top queries, get execution plan from XEvent data and review for:
- Table scans (add indexes)
- Index scans (add covering indexes)
- Key lookups (add INCLUDE columns)
- Implicit conversions (fix data type mismatches)

---

### Query #4: Overall Wait Statistics (Filter for CPU-Related Waits)
**MCP Tool**: `analyze_wait_stats`  
**Purpose**: Identify CPU pressure waits  
**Use When**: Confirming CPU is the bottleneck

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

**CPU-Specific Waits to Look For**:
- **SOS_SCHEDULER_YIELD**: High CPU pressure, tasks yielding scheduler (runnable queue building up)
- **CXPACKET**: Parallelism contention (threads waiting for others in parallel query)
- **CXCONSUMER**: Parallel query thread coordination waits
- **THREADPOOL**: All worker threads busy (increase max worker threads or reduce load)

**Interpretation**:
- **SOS_SCHEDULER_YIELD** high → CPU-bound, more demand than CPU cores available
- **CXPACKET** high → Inefficient parallelism (consider lowering MAXDOP or cost threshold)
- **THREADPOOL** → All threads busy, need more workers or reduce concurrent load

---

### Query #19: CPU by Database
**Purpose**: Identify which database is consuming CPU  
**Use When**: Multiple databases on same instance

```sql
SELECT 
    DB_NAME(b.DatabaseID) AS database_name,
    SUM(b.CPU) AS Total_CPU_ms,
    COUNT(*) AS Executions,
    SUM(b.CPU) / COUNT(*) AS Avg_CPU_ms,
    CAST(100.0 * SUM(b.CPU) / SUM(SUM(b.CPU)) OVER() AS DECIMAL(5,2)) AS CPU_Pct
FROM ReadTrace.tblBatches b
WHERE b.DatabaseID IS NOT NULL
GROUP BY b.DatabaseID
ORDER BY Total_CPU_ms DESC;
```

**Output Interpretation**:
- **database_name**: Which database the CPU is attributed to
- **CPU_Pct**: Percentage of total CPU consumed by this database
- Helps narrow focus to specific database for further analysis

---

### Query #20: Spinlock Statistics (Advanced)
**MCP Tool**: `analyze_spinlocks`  
**Purpose**: Identify spinlock (internal latch) contention  
**Use When**: High CPU but low query CPU (internal SQL Server contention)

```sql
-- Note: This table may not exist in all Nexus imports
SELECT TOP 20
    name AS spinlock_type,
    collisions,
    spins,
    spins_per_collision,
    sleep_time,
    backoffs
FROM tbl_SPINLOCKSTATS
ORDER BY spins DESC;
```

**Output Interpretation**:
- **collisions**: How many times threads collided on this spinlock
- **spins**: Total spin attempts (busy-waiting = CPU consumption)
- **spins_per_collision**: Higher = more CPU wasted waiting
- **backoffs**: Thread had to back off and sleep

**Common Spinlock Types**:
- **SOS_CACHESTORE**: Plan cache contention (ad-hoc query workload)
- **SOS_SUSPEND_QUEUE**: Task scheduling contention
- **LOCK_HASH**: Lock manager hash table contention
- **LOGCACHE_ACCESS**: Transaction log cache contention (heavy writes)

**Action**:
- **SOS_CACHESTORE** high → Enable "Optimize for ad hoc workloads", review plan cache bloat
- **LOGCACHE_ACCESS** high → Review transaction patterns, batch commits

---

### Query #21: Plan Cache Analysis (Compilation CPU)
**MCP Tool**: `get_plan_cache_analysis`  
**Purpose**: Check if CPU is being consumed by query compilation  
**Use When**: High CPU but queries in Query #18 don't account for it

```sql
-- Note: This requires plan cache tables (may not be in all imports)
SELECT 
    objtype,
    cacheobjtype,
    COUNT(*) AS plan_count,
    SUM(size_in_bytes) / 1024 / 1024 AS cache_size_mb,
    SUM(usecounts) AS total_use_count,
    AVG(usecounts) AS avg_use_count
FROM tbl_CACHEOBJECTS
GROUP BY objtype, cacheobjtype
ORDER BY plan_count DESC;
```

**Interpretation**:
- **avg_use_count = 1**: Plans used once then discarded (ad-hoc queries, compilation storm)
- **plan_count very high**: Plan cache bloat (parameterize queries, use sp_executesql)

**Action**:
```sql
-- Enable optimize for ad hoc workloads
sp_configure 'optimize for ad hoc workloads', 1;
RECONFIGURE;
```

---

### Query #22: CPU Consumption by Database
**MCP Tool**: `get_cpu_by_database`  
**Purpose**: Break down CPU consumption by database — Total_CPU_ms, Executions, Avg_CPU_ms, CPU_Pct per database. Useful when multiple databases share the instance and you need to narrow the CPU investigation before running Query #18.  
**Use When**: Multiple databases on instance; determine which database to focus on first

```sql
SELECT
    DB_NAME(DatabaseID) AS DatabaseName,
    SUM(CPU) AS Total_CPU_ms,
    COUNT(*) AS Executions,
    AVG(CPU) AS Avg_CPU_ms,
    CAST(100.0 * SUM(CPU) / SUM(SUM(CPU)) OVER() AS DECIMAL(5,2)) AS CPU_Pct
FROM ReadTrace.tblBatches
WHERE DatabaseID IS NOT NULL
GROUP BY DatabaseID
ORDER BY Total_CPU_ms DESC;
```

**Output Interpretation**:
- **CPU_Pct**: Percentage of total SQL CPU consumed by this database
- Focus subsequent Query #18 and Query #2 analysis on the highest-CPU database
- Very high single-database CPU (>80%) = isolated workload issue
- CPU spread across many databases = instance-wide pressure

---

## REMEDIATION STRATEGIES

### Immediate Actions

1. **Identify Top CPU Query** (Query #18):
   - Get HashID of top consumer
   - Review execution plan
   - Look for missing indexes, scans, key lookups

2. **Check MAXDOP Settings** (if CXPACKET high):
   ```sql
   sp_configure 'max degree of parallelism';
   -- Consider lowering if CXPACKET contention high
   -- Rule of thumb: MAXDOP = # of physical cores (not logical)
   ```

3. **Check Cost Threshold for Parallelism**:
   ```sql
   sp_configure 'cost threshold for parallelism';
   -- Default is 5 (too low), consider 25-50 for OLTP
   ```

### Long-Term Optimization

1. **Add Missing Indexes**:
   - Review execution plans from top CPU queries
   - Add covering indexes to eliminate lookups
   - Use filtered indexes for selective queries

2. **Fix Parameter Sniffing**:
   - Use `OPTION (RECOMPILE)` for queries with varying parameters
   - Use `OPTION (OPTIMIZE FOR UNKNOWN)`
   - Use plan guides for vendor code

3. **Reduce Compilation**:
   - Parameterize ad-hoc queries
   - Use stored procedures or sp_executesql
   - Enable "Optimize for ad hoc workloads"

4. **Review Statistics**:
   - Update statistics on tables involved in top queries
   - Check auto-update statistics is enabled

5. **Scale Up**:
   - Add more CPU cores if consistently at 100%
   - Review CPU licensing (Physical vs Logical cores)

---

## NEXT STEPS BASED ON FINDINGS

### If Specific Query is CPU Hog
→ Use [scenario-performance.md](scenario-performance.md) Query #2 to analyze that query's execution patterns

### If Parallelism Issues (CXPACKET)
→ Review MAXDOP and cost threshold settings, consider query tuning to reduce parallelism needs

### If Compilation Storm
→ Enable optimize for ad hoc, parameterize queries, review plan cache

### If Spinlock Contention
→ Consult Microsoft Support (internal SQL Server tuning may be needed)

### If Non-SQL Process Consuming CPU
→ Use ProcessMonitor scenario to identify and address non-SQL CPU consumer
