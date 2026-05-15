# Blocking & Deadlocking Issues - Complete Diagnostic Workflow

## SYMPTOM
"Queries are blocked" / "Deadlocks occurring" / "Timeouts in application" / "LCK_M_* waits"

---

## DECISION TREE

### Is blocking happening RIGHT NOW?

#### ✅ **YES (active blocking)**

**LogScout Scenario**: `GeneralPerf`  
**Command**: `.\SQL_LogScout.cmd start GeneralPerf`  
**Duration**: 2-5 minutes during blocking

**Analysis Sequence**:

1. **Check for LCK_* waits** → Query #4
2. **Identify head blockers** → Query #9
3. **Find blocked sessions** → Query #10
4. **Analyze wait resources** → Check wait_resource column in Query #10

**Indicators from Query #4**:
- High **LCK_M_*** waits confirm blocking
- **LCK_M_S**: Shared lock waits
- **LCK_M_X**: Exclusive lock waits
- **LCK_M_U**: Update lock waits

**Indicators from Query #9**:
- Session ID of blocker
- Query text of blocker
- Number of blocked sessions
- Duration of blocking

**Indicators from Query #10**:
- Specific table/page causing contention
- Blocking chain details
- Queries involved in blocking

**Root Cause Categories**:
- Long-running transaction holding locks
- Missing indexes causing lock escalation
- Hotspot on specific table/row
- Inappropriate isolation level

---

#### ❌ **NO (historical / intermittent)**

**LogScout Scenario**: `GeneralPerf` (includes deadlock XEvent capture)  
**Command**: `.\SQL_LogScout.cmd start GeneralPerf`  
**Note**: GeneralPerf automatically includes deadlock Extended Events

**Analysis**:
1. Review `ReadTrace.tblDeadlocks` (if available)
2. Check blocking chain aggregates using Query #10
3. Identify recurring patterns:
   - Same tables involved?
   - Same query patterns?
   - Specific time windows?

---

### Blocking or Deadlocking?

#### **BLOCKING** (waits but eventually completes)

**Next Steps**:
1. Identify blocker query from Query #9
2. Check transaction isolation level
3. Review transaction duration
4. Consider index optimization (reduce lock scope)
5. Evaluate query logic (reduce lock hold time)
6. Consider `READ_COMMITTED_SNAPSHOT` isolation level

---

#### **DEADLOCKING** (1205 errors, victims chosen)

**Next Steps**:
1. Analyze deadlock graph from XEvents
2. Identify deadlock pattern (cycle)
3. Reorder operations to prevent cycle
4. Add RETRY logic to application
5. Consider indexes to reduce lock scope
6. Review transaction boundaries

---

### Advanced: Complex/Distributed Blocking

**LogScout Scenario**: `DetailedPerf` (more granular trace)  
**Command**: `.\SQL_LogScout.cmd start DetailedPerf`  
**Analysis**: Statement-level lock analysis with full trace details

---

## EMBEDDED QUERIES

### Query #4: Overall Wait Statistics
**Purpose**: Confirm blocking via LCK_* waits  
**Use When**: First step to verify blocking is the issue

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

**Look for**:
- **LCK_M_S**: Shared lock waits
- **LCK_M_X**: Exclusive lock waits
- **LCK_M_U**: Update lock waits
- **LCK_M_IX**, **LCK_M_IS**: Intent locks

High percentages of LCK_M_* waits confirm blocking is the primary bottleneck.

---

### Query #9: Head Blockers Summary
**Purpose**: Identify sessions causing the most blocking  
**Use When**: Finding the root blocker in blocking chains

```sql
SELECT TOP 20
    head_blocker_session_id,
    COUNT(DISTINCT blocked_session_id) AS sessions_blocked,
    COUNT(*) AS total_blocking_instances,
    MAX(wait_duration_ms) AS max_block_duration_ms,
    AVG(wait_duration_ms) AS avg_block_duration_ms
FROM tbl_HEADBLOCKERSUMMARY
GROUP BY head_blocker_session_id
ORDER BY total_blocking_instances DESC;
```

**Output Interpretation**:
- **head_blocker_session_id**: The session ID causing blocking (root blocker)
- **sessions_blocked**: How many distinct sessions this blocker has blocked
- **total_blocking_instances**: Total number of times blocking occurred
- **max_block_duration_ms**: Longest single block duration
- **avg_block_duration_ms**: Average block duration

**Action**: Take the `head_blocker_session_id` values and find their queries using Query #10.

---

### Query #10: Blocked Sessions Detail
**Purpose**: Show all blocked sessions with blocker info  
**Use When**: Investigating specific blocking incident details

```sql
SELECT 
    runtime,
    blocked_session_id,
    blocking_session_id AS blocker,
    wait_type,
    wait_duration_ms,
    wait_resource,
    database_name,
    blocking_text AS blocker_query,
    blocked_text AS blocked_query
FROM tbl_BLOCKING_CHAINS
ORDER BY wait_duration_ms DESC;
```

**Output Interpretation**:
- **runtime**: When the blocking was captured
- **blocked_session_id**: Session that is waiting (victim)
- **blocker**: Session that is holding the lock (blocker)
- **wait_type**: Type of lock being waited on (LCK_M_X, LCK_M_S, etc.)
- **wait_duration_ms**: How long the blocking has lasted
- **wait_resource**: Specific resource being blocked (database, file, page, key)
- **blocker_query**: The query that is holding the lock
- **blocked_query**: The query that is waiting

**Analysis Tips**:
1. **Identify the pattern**: Are the same tables/resources repeatedly blocked?
2. **Find the hotspot**: Look at `wait_resource` to see which table/page is contention point
3. **Analyze blocker query**: Is it a long-running transaction? Missing WHERE clause? Full scan?
4. **Review transaction isolation**: Are queries using SERIALIZABLE or REPEATABLE READ unnecessarily?

**Common wait_resource formats**:
- `DATABASE: [id]` - Database-level lock
- `PAGE: [dbid]:[fileid]:[pageid]` - Page-level lock (hot spot)
- `KEY: [dbid]:[hobtid]:[hash]` - Row-level lock
- `OBJECT: [dbid]:[objectid]` - Table-level lock (escalation)

---

### Query #11: Blocking Chain Tree (Advanced)
**Purpose**: Visualize full blocking chain hierarchy  
**Use When**: Complex multi-level blocking scenarios

```sql
-- Build blocking chain hierarchy
WITH BlockingChain AS (
    SELECT 
        session_id,
        blocking_session_id,
        wait_type,
        wait_duration_ms,
        wait_resource,
        0 AS level
    FROM tbl_BLOCKING_CHAINS
    WHERE blocking_session_id = 0  -- Root blocker
    
    UNION ALL
    
    SELECT 
        bc.session_id,
        bc.blocking_session_id,
        bc.wait_type,
        bc.wait_duration_ms,
        bc.wait_resource,
        chain.level + 1
    FROM tbl_BLOCKING_CHAINS bc
    INNER JOIN BlockingChain chain ON bc.blocking_session_id = chain.session_id
)
SELECT 
    level,
    session_id,
    blocking_session_id,
    wait_type,
    wait_duration_ms,
    wait_resource,
    REPLICATE('  ', level) + CAST(session_id AS VARCHAR(10)) AS blocking_hierarchy
FROM BlockingChain
ORDER BY level, session_id;
```

**Output Interpretation**:
- **level**: How deep in the blocking chain (0 = root blocker)
- **blocking_hierarchy**: Visual indentation showing chain structure
- Shows full tree: Root blocker → Blocked sessions → Sessions blocked by those, etc.

---

### Query #12: Lock Summary by Object
**Purpose**: Identify which tables/objects have most lock contention  
**Use When**: Finding hotspot tables for blocking

```sql
SELECT 
    database_name,
    wait_resource,
    COUNT(*) AS lock_count,
    SUM(wait_duration_ms) AS total_wait_ms,
    AVG(wait_duration_ms) AS avg_wait_ms,
    MAX(wait_duration_ms) AS max_wait_ms
FROM tbl_BLOCKING_CHAINS
WHERE wait_resource IS NOT NULL
GROUP BY database_name, wait_resource
ORDER BY total_wait_ms DESC;
```

**Output Interpretation**:
- **wait_resource**: The specific resource (page/key/object) with contention
- **lock_count**: How many times this resource was blocked
- **total_wait_ms**: Cumulative wait time on this resource
- Identifies hotspot tables/pages that need optimization (indexes, partitioning, etc.)

---

## REMEDIATION STRATEGIES

### Short-Term Fixes

1. **Kill Blocking Session** (if justified):
   ```sql
   KILL <head_blocker_session_id>;
   ```
   ⚠️ Only if blocker is hung or misbehaving!

2. **Enable READ_COMMITTED_SNAPSHOT** (reduces reader blocking):
   ```sql
   ALTER DATABASE YourDatabase SET READ_COMMITTED_SNAPSHOT ON;
   ```

3. **Set Lock Timeout** (application-level):
   ```sql
   SET LOCK_TIMEOUT 5000; -- 5 seconds
   ```

### Long-Term Fixes

1. **Optimize Blocker Queries**:
   - Add missing indexes (reduce scan duration = reduce lock hold time)
   - Add WHERE clauses (reduce rows locked)
   - Review execution plans

2. **Reduce Transaction Scope**:
   - Keep transactions short
   - Move non-critical operations outside transactions
   - Batch large operations

3. **Change Isolation Level**:
   - Use `READ COMMITTED` instead of `REPEATABLE READ`
   - Use `SNAPSHOT` isolation for readers
   - Avoid `SERIALIZABLE` unless absolutely required

4. **Index Optimization**:
   - Add covering indexes (eliminate lookups)
   - Add filtered indexes (reduce index size)
   - Consider partitioning for hotspot tables

5. **Application Changes**:
   - Implement retry logic for deadlocks
   - Access tables in consistent order (prevent deadlocks)
   - Use `WITH (NOLOCK)` for read-only queries (accept dirty reads)

---

## DEADLOCK-SPECIFIC ANALYSIS

### Finding Deadlock Graphs

```sql
-- If tblDeadlocks table exists
SELECT 
    event_timestamp,
    deadlock_xml
FROM ReadTrace.tblDeadlocks
ORDER BY event_timestamp DESC;
```

**Analysis**:
- Open `deadlock_xml` in SQL Server Management Studio
- SSMS will render the deadlock graph visually
- Identify the cycle: Process A waiting for Process B, Process B waiting for Process A

### Deadlock Prevention Strategies

1. **Access objects in same order** across all transactions
2. **Keep transactions short** (less time = less chance of overlap)
3. **Use row-level locking** (avoid lock escalation to table level)
4. **Implement retry logic** in application (deadlocks will happen, be resilient)

---

## NEXT STEPS BASED ON FINDINGS

### If Blocker Query is Slow
→ Use [scenario-performance.md](scenario-performance.md) to optimize the blocker query

### If Blocking on Specific Table
→ Review table schema, add missing indexes, consider partitioning

### If Application Issue
→ Review transaction boundaries, isolation levels, access patterns

### If Lock Escalation Occurring
→ Add indexes to prevent scans, consider `ROWLOCK` hint, increase lock escalation threshold
