# Memory Pressure Issues - Complete Diagnostic Workflow

## SYMPTOM
"Out of memory errors" / "Low memory warnings" / "Pages being swapped" / "701/802 errors" / "RESOURCE_SEMAPHORE waits"

---

## DECISION TREE

### Is SQL Server reporting memory pressure?

#### ✅ **YES** (SQL memory alerts / 701/802 errors)

**LogScout Scenario**: `Memory`  
**Command**: `.\SQL_LogScout.cmd start Memory`  
**Duration**: 2-3 minutes

**Analysis Sequence**:

1. **Check max server memory setting** → Query #24
2. **Review memory clerks breakdown** → Query #15
3. **Check for memory grant waits** → Query #4 (filter for RESOURCE_SEMAPHORE)
4. **Identify queries requesting large grants** → Query #18 (if available)

**Indicators from Query #24**:
- **max_server_memory too high**: SQL consuming too much, OS memory pressure
- **max_server_memory too low**: SQL can't allocate enough for workload

**Indicators from Query #15**:
- **Buffer pool size**: Should be largest consumer (data pages)
- **Other clerks high**: Plan cache, memory grants, CLR, etc.

**Root Cause Categories**:
- max_server_memory set too high (external pressure)
- Large queries requesting excessive grants
- Memory leak in specific clerk
- Inefficient plan cache usage

---

#### ❌ **NO** (but system memory low)

**LogScout Scenario**: `Memory + ProcessMonitor`  
**Command**: `.\SQL_LogScout.cmd start Memory,ProcessMonitor`  
**Analysis**: Identify non-SQL memory consumer

---

### Do you need memory dump for deep analysis?

#### **YES** (SQL Support requested / suspected internal issue)

**LogScout Scenario**: `DumpMemory`  
**Command**: `.\SQL_LogScout.cmd start DumpMemory`  
⚠️ **WARNING**: Creates full memory dump (size = RAM usage)  
**Duration**: Depends on memory size  
**Analysis**: Send dump to Microsoft Support

#### **NO** (standard troubleshooting)

**LogScout Scenario**: `Memory` (sufficient for most cases)

---

## NEXT STEPS

### If max_server_memory too high:
→ Reduce to leave room for OS (rule of thumb: leave 4-8 GB for OS + other apps)

### If buffer pool low:
→ Check for external pressure (OS, other apps)

### If RESOURCE_SEMAPHORE high:
→ Review query memory grants (large sorts, hash joins)

### If plan cache high:
→ Check for ad-hoc query workload, enable "optimize for ad hoc workloads"

### If clerk leak suspected:
→ Restart SQL (after gathering data), monitor clerk growth

---

## EMBEDDED QUERIES

### Query #15: Memory Clerks Analysis
**MCP Tool**: `get_memory_clerk_distribution`  
**Purpose**: Identify memory consumers within SQL Server  
**Use When**: Understanding where SQL Server memory is allocated

```sql
SELECT TOP 20
    type AS clerk_type,
    SUM(pages_kb) / 1024 AS memory_mb,
    CAST(100.0 * SUM(pages_kb) / SUM(SUM(pages_kb)) OVER() AS DECIMAL(5,2)) AS pct
FROM (
    SELECT 
        type,
        SUM(virtual_memory_committed_kb + shared_memory_committed_kb) AS pages_kb
    FROM tbl_MEMCLERKS
    GROUP BY type
) AS clerks
GROUP BY type
ORDER BY memory_mb DESC;
```

**Output Interpretation**:
- **MEMORYCLERK_SQLBUFFERPOOL**: Data/index pages (should be largest, typically 70-80%)
- **CACHESTORE_SQLCP**: SQL Plan cache (high = ad-hoc queries, consider "optimize for ad hoc")
- **CACHESTORE_OBJCP**: Object plan cache (procs, triggers, functions)
- **MEMORYCLERK_SQLQERESERVATIONS**: Query memory grants (sorts, hash joins)
- **USERSTORE_TOKENPERM**: Security token cache (high = many unique logins)
- **MEMORYCLERK_SQLCLR**: CLR assemblies
- **CACHESTORE_XPROC**: Extended procedure cache

**Analysis**:
1. **SQLBUFFERPOOL low**: Not enough buffer for data → Increase max_server_memory or add RAM
2. **CACHESTORE_SQLCP very high**: Ad-hoc query workload → Parameterize queries, enable optimize setting
3. **SQLQERESERVATIONS high**: Large memory grants → Review expensive queries (sorts, hash joins)
4. **SQLCLR high**: CLR assemblies consuming memory → Review CLR code, consider optimization

---

### Query #24: Server Configuration Properties
**MCP Tool**: `query_nexus_database`  
**Purpose**: Check max server memory and other memory-related settings  
**Use When**: First step in memory analysis

```sql
SELECT 
    name,
    value AS current_value,
    value_in_use,
    minimum,
    maximum,
    description
FROM tbl_ServerProperties
WHERE name IN (
    'max server memory (MB)',
    'min server memory (MB)',
    'index create memory (KB)',
    'min memory per query (KB)'
)
ORDER BY name;
```

**Output Interpretation**:
- **max server memory (MB)**: Maximum memory SQL can allocate
  - Default: 2147483647 MB (essentially unlimited) → BAD for shared servers
  - Best practice: Total RAM - (OS + other apps) = max_server_memory
  - Example: 64 GB server → max_server_memory = 56 GB (leave 8 GB for OS)

- **min server memory (MB)**: Minimum memory SQL will hold (default 0)
  - Usually fine at default
  - On dedicated SQL server, can set to reserve memory

**Action**:
```sql
-- Set appropriate max server memory (adjust value for your environment)
sp_configure 'show advanced options', 1;
RECONFIGURE;
sp_configure 'max server memory (MB)', 56000; -- Example: 56 GB
RECONFIGURE;
```

---

### Query #4: Overall Wait Statistics (Filter for Memory Waits)
**MCP Tool**: `analyze_wait_stats`  
**Purpose**: Identify memory grant waits  
**Use When**: Confirming memory pressure is affecting queries

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

**Memory-Specific Waits to Look For**:
- **RESOURCE_SEMAPHORE**: Queries waiting for memory grants (sorts, hash joins)
  - High % = Memory pressure, queries can't get enough memory to execute
  - Action: Add RAM, optimize queries to reduce memory needs, or reduce concurrent load

- **CMEMTHREAD**: Memory allocation contention (rare, usually indicates internal issue)

**Interpretation**:
- **RESOURCE_SEMAPHORE > 5%**: Significant memory pressure affecting query performance
- **RESOURCE_SEMAPHORE > 20%**: Critical memory pressure, queries severely delayed

---

### Query #22: Queries with Large Memory Grants
**Purpose**: Find queries requesting excessive memory  
**Use When**: RESOURCE_SEMAPHORE waits are high

```sql
-- Note: May need to adapt based on available columns
SELECT TOP 50
    ub.NormText,
    b.HashID,
    COUNT(*) AS executions,
    AVG(b.GrantedMemoryKB) AS avg_granted_memory_kb,
    MAX(b.GrantedMemoryKB) AS max_granted_memory_kb,
    SUM(b.GrantedMemoryKB) AS total_granted_memory_kb
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
WHERE b.GrantedMemoryKB > 0
GROUP BY ub.NormText, b.HashID
ORDER BY avg_granted_memory_kb DESC;
```

**Output Interpretation**:
- **avg_granted_memory_kb**: Average memory grant per execution
  - Very large values (> 100 MB) indicate queries with sorts/hash joins on large datasets
- **total_granted_memory_kb**: Total memory consumed by this query across all executions
  - High value = frequently-run query consuming significant memory

**Action**:
- Review execution plan for large grants
- Check for missing indexes (causing sorts)
- Consider columnstore indexes for large analytical queries
- Review joins (hash joins require memory)

---

### Query #23: Memory Grant Timeouts
**Purpose**: Identify queries that timeout waiting for memory  
**Use When**: RESOURCE_SEMAPHORE waits combined with query failures

```sql
-- Check XEvent data for memory grant timeout events
SELECT 
    event_timestamp,
    query_hash,
    requested_memory_kb,
    available_memory_kb,
    wait_time_ms
FROM ReadTrace.tblMemoryGrantTimeouts -- Table name may vary
ORDER BY wait_time_ms DESC;
```

**Output Interpretation**:
- **requested_memory_kb vs available_memory_kb**: Gap shows memory shortage
- **wait_time_ms**: How long query waited before timing out
- Indicates severe memory pressure, queries can't execute at all

---

### Query #25: Buffer Pool Usage by Database
**Purpose**: See which databases are consuming buffer pool memory  
**Use When**: Multiple databases, want to know which consumes most memory

```sql
SELECT 
    database_name,
    COUNT(*) * 8 / 1024 AS buffer_pool_mb,
    CAST(100.0 * COUNT(*) / SUM(COUNT(*)) OVER() AS DECIMAL(5,2)) AS pct
FROM tbl_BUFFERDESCRIPTORS
GROUP BY database_name
ORDER BY buffer_pool_mb DESC;
```

**Output Interpretation**:
- **buffer_pool_mb**: Memory occupied by this database's pages
- **pct**: Percentage of buffer pool consumed
- Helps identify which database is "hot" (frequently accessed data)

---

## REMEDIATION STRATEGIES

### Immediate Actions

1. **Check max_server_memory** (Query #24):
   ```sql
   -- If unlimited or too high, set appropriately
   sp_configure 'max server memory (MB)', <appropriate_value>;
   RECONFIGURE;
   ```

2. **If RESOURCE_SEMAPHORE high**:
   - Reduce concurrent workload (throttle application queries)
   - Add more RAM
   - Optimize queries with large grants (Query #22)

3. **If plan cache bloated** (Query #15, CACHESTORE_SQLCP high):
   ```sql
   -- Enable optimize for ad hoc workloads
   sp_configure 'optimize for ad hoc workloads', 1;
   RECONFIGURE;
   
   -- Clear plan cache (use with caution, causes recompilations)
   DBCC FREEPROCCACHE;
   ```

### Long-Term Optimization

1. **Right-Size max_server_memory**:
   - Dedicated SQL Server: Total RAM - 4 GB (for OS)
   - Shared server: Total RAM - (OS + other apps + 4 GB buffer)
   - 128+ GB RAM: Can leave 10% for OS

2. **Optimize Queries with Large Grants**:
   - Add missing indexes (eliminate sorts)
   - Use covering indexes (reduce lookup memory)
   - Review ORDER BY clauses (unnecessary sorts)
   - Review GROUP BY (hash aggregates use memory)

3. **Parameterize Ad-Hoc Queries**:
   - Use sp_executesql instead of dynamic SQL
   - Use stored procedures
   - Enable "optimize for ad hoc workloads"

4. **Scale Up**:
   - Add more RAM if consistently hitting memory limits
   - Consider In-Memory OLTP for hot tables

5. **Review CLR Usage** (if SQLCLR high):
   - Optimize CLR code
   - Consider T-SQL alternatives
   - Review CLR assembly memory management

---

## COMMON MEMORY ERRORS

### Error 701: "There is insufficient system memory in resource pool"
- **Cause**: Query can't get memory grant
- **Action**: Check RESOURCE_SEMAPHORE waits, review max_server_memory

### Error 802: "There is insufficient memory available in the buffer pool"
- **Cause**: Buffer pool exhausted
- **Action**: Increase max_server_memory or add RAM

### Error 8645: "A timeout occurred while waiting for memory resources"
- **Cause**: Query waited too long for memory grant
- **Action**: Optimize query, add RAM, reduce concurrent load

---

## NEXT STEPS BASED ON FINDINGS

### If Buffer Pool Low
→ Check external memory pressure (OS, other apps), consider adding RAM

### If Plan Cache High
→ Parameterize queries, enable optimize setting, clear cache

### If Specific Query Consuming Memory
→ Use [scenario-performance.md](scenario-performance.md) Query #2 to analyze that query

### If Memory Leaks Suspected
→ Monitor clerk growth over time, restart SQL (temporary fix), engage Microsoft Support
