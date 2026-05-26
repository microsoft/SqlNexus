# Query Deep Dive & Advanced Wait Analysis - Complete Diagnostic Workflow

## SYMPTOM
"Need detailed wait analysis for specific query" / "Understand statement-level performance" / "Correlate waits with queries" / "Which queries encounter which waits"

---

## DECISION TREE

### What level of analysis do you need?

#### **Statement-Level Analysis** (DetailedPerf collection)
- Break down batch into individual statements → Query #11
- Useful when batch contains multiple statements
- Requires DetailedPerf scenario (statement-level trace)

#### **Wait Correlation Analysis**
- Which queries encounter which waits most → Query #6
- Which resources are contended → Query #7
- Wait type frequency distribution → Query #8

#### **Wait-Heavy Query Analysis**
- Find queries spending most time waiting → Query #14
- Identify queries bottlenecked by waits vs CPU

---

## USE CASES

### Statement-Level Troubleshooting
- Batch contains multiple statements, need to identify slow statement
- Stored procedure with multiple queries
- Dynamic SQL with complex batches

### Wait Pattern Analysis
- Understand what queries wait on
- Correlate wait types with query patterns
- Identify resource contention points

### Query-Wait Correlation
- "Which queries encounter PAGEIOLATCH waits?"
- "What are the top queries causing blocking?"
- "Which queries wait on specific resources?"

---

## EMBEDDED QUERIES

### Query #6: Aggregate Waits and Waiting Queries
**MCP Tool**: `get_aggregate_waits_and_queries`  
**Purpose**: Correlate wait types with specific queries/procedures  
**Use When**: Need to see which queries encounter which waits most frequently

```sql
SELECT 
    COUNT(*) AS occurrences, 
    SUM(r.wait_duration_ms) AS WaitDensity_ms, 
    r.wait_type, 
    q.procname, 
    SUBSTRING(q.stmt_text, 1, 500) AS stmt_text
FROM tbl_REQUESTS r
JOIN tbl_NOTABLEACTIVEQUERIES q
    ON r.session_id = q.session_id
    AND r.runtime = q.runtime
WHERE r.wait_type IS NOT NULL 
     AND r.wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY r.wait_type, q.procname, q.stmt_text
ORDER BY WaitDensity_ms DESC;
```

**Output Interpretation**:
- **occurrences**: How many times this query/wait combination occurred
- **WaitDensity_ms**: Total time spent in this wait for this query
- **wait_type**: Type of wait encountered
- **procname**: Stored procedure name (if applicable)
- **stmt_text**: Query text

**Analysis Patterns**:

1. **High WaitDensity_ms + Specific Query + PAGEIOLATCH_***:
   - Query is I/O-bound
   - Action: Add indexes to reduce reads

2. **High occurrences + LCK_M_***:
   - Query frequently encounters blocking
   - Action: Review transaction scope, add indexes to reduce lock hold time

3. **High WaitDensity_ms + WRITELOG**:
   - Query generates heavy transaction log activity
   - Action: Batch large operations, move log file to faster storage

4. **Specific stored procedure + Multiple wait types**:
   - Procedure has complex workload
   - Action: Break down with Query #11 (statement-level)

**Example Output**:
```
occurrences | WaitDensity_ms | wait_type        | procname           | stmt_text
------------|----------------|------------------|--------------------|-----------
523         | 45892          | PAGEIOLATCH_SH   | usp_GetOrderHistory| SELECT * FROM Orders WHERE...
312         | 28451          | LCK_M_X          | usp_UpdateInventory| UPDATE Inventory SET...
198         | 15234          | WRITELOG         | usp_ProcessBatch   | INSERT INTO Archive...
```

**Action**: Focus on top 5-10 entries by WaitDensity_ms for maximum impact

---

### Query #7: Waits Aggregated by Wait Resource and Type
**MCP Tool**: `get_wait_resource_hotspots`  
**Purpose**: Identify specific resources (pages, objects, keys) causing contention  
**Use When**: Need to pinpoint hot spots in the database

```sql
SELECT 
    COUNT(*) AS occurrences, 
    wait_resource, 
    wait_type, 
    MAX(wait_duration_ms) AS maxWaitMs,
    AVG(wait_duration_ms) AS avgWaitMs,
    SUM(wait_duration_ms) AS totalWaitMs
FROM tbl_REQUESTS r
WHERE wait_type IS NOT NULL
     AND r.wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY wait_resource, wait_type
ORDER BY occurrences DESC;
```

**Output Interpretation**:
- **occurrences**: How many times this resource was contended
- **wait_resource**: Specific resource identifier
- **wait_type**: Type of wait on this resource
- **maxWaitMs**: Longest single wait
- **totalWaitMs**: Cumulative wait time on this resource

**Resource Format Examples**:
- `PAGE: 5:1:12345` - Database 5, File 1, Page 12345
- `KEY: 5:72057594038321152 (abc123)` - Row lock (key hash)
- `OBJECT: 5:245575913` - Table/object lock
- `RID: 5:1:12345:0` - Row identifier

**Analysis Patterns**:

1. **High occurrences on same PAGE**:
   - Hot spot page contention (usually PAGELATCH_EX)
   - Action: 
     - Identify table/index from page resource
     - Review for last-page insert contention (identity column?)
     - Consider partitioning or redesign

2. **High occurrences on same KEY**:
   - Row-level blocking (usually LCK_M_X or LCK_M_S)
   - Action:
     - Identify specific row being contended
     - Review why multiple queries access same row
     - Consider application-level locking or queuing

3. **High occurrences on same OBJECT**:
   - Table-level lock contention (lock escalation?)
   - Action:
     - Add indexes to prevent escalation
     - Review queries for full table scans
     - Check lock escalation thresholds

**Decoding wait_resource**:
```sql
-- To find table from page resource (e.g., PAGE: 5:1:12345)
DECLARE @dbid INT = 5;
DECLARE @fileid INT = 1;
DECLARE @pageid INT = 12345;

USE [YourDatabaseName]; -- Use database ID from wait_resource
SELECT 
    OBJECT_NAME(object_id) AS TableName,
    index_id,
    partition_id
FROM sys.dm_db_page_info(@dbid, @fileid, @pageid, 'DETAILED');
```

---

### Query #8: Count Per Wait Type
**MCP Tool**: `get_wait_type_distribution`  
**Purpose**: Frequency distribution of all wait types  
**Use When**: Understanding overall wait patterns and prevalence

```sql
SELECT 
    COUNT(*) AS occurrences, 
    wait_type,
    MAX(wait_duration_ms) AS max_wait_ms,
    AVG(wait_duration_ms) AS avg_wait_ms,
    SUM(wait_duration_ms) AS total_wait_ms,
    CAST(100.0 * SUM(wait_duration_ms) / SUM(SUM(wait_duration_ms)) OVER() AS DECIMAL(5,2)) AS pct_total_wait
FROM tbl_REQUESTS r
WHERE wait_type IS NOT NULL
     AND r.wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY wait_type
ORDER BY occurrences DESC;
```

**Output Interpretation**:
- **occurrences**: How many times this wait type occurred
- **max_wait_ms**: Longest single instance of this wait
- **avg_wait_ms**: Average wait duration
- **total_wait_ms**: Cumulative time in this wait
- **pct_total_wait**: Percentage of all wait time

**Use This vs Query #4**:
- **Query #4** (scenario-performance.md): Aggregate waits over time from `tbl_OS_WAIT_STATS` (system-level)
- **Query #8** (this): Snapshot waits from `tbl_REQUESTS` (request-level, more granular)

**Analysis Patterns**:

1. **High occurrences but low avg_wait_ms**:
   - Common but short waits (acceptable)
   - Example: Brief ASYNC_NETWORK_IO

2. **Low occurrences but high avg_wait_ms**:
   - Rare but severe waits
   - Example: Long WRITELOG during checkpoint

3. **High pct_total_wait**:
   - Primary bottleneck category
   - Focus optimization here

**Example Output**:
```
occurrences | wait_type          | max_wait_ms | avg_wait_ms | total_wait_ms | pct_total_wait
------------|-------------------|-------------|-------------|---------------|---------------
1523        | PAGEIOLATCH_SH    | 8521        | 152         | 231596        | 42.5%
892         | LCK_M_X           | 5234        | 98          | 87416         | 16.0%
645         | WRITELOG          | 2341        | 76          | 49020         | 9.0%
```

**Action**: Focus on waits with high `pct_total_wait` for maximum impact

---

### Query #11: Find All Statements in a Batch
**MCP Tool**: `get_statements_in_batch`  
**Purpose**: Break down batch into individual statements for statement-level analysis  
**Use When**: DetailedPerf collection, need to identify slow statement in a batch

```sql
-- Replace <BatchSeq> with actual BatchSeq value from ReadTrace.tblBatches
DECLARE @BatchSeq BIGINT = <BatchSeq>; -- Example: 12345

SELECT 
    SUM(cpu) AS CPU_ms, 
    SUM(Duration)/1000.0 AS Duration_ms, 
    COUNT(*) AS Occurrences, 
    AVG(Duration)/1000.0 AS AvgDuration_ms,
    MAX(Duration)/1000.0 AS MaxDuration_ms,
    SUM(Reads) AS TotalReads,
    AVG(Reads) AS AvgReads,
    ub.NormText AS Statement,
    b.HashID AS StatementHashID
FROM ReadTrace.tblStatements b 
JOIN ReadTrace.tblUniqueStatements ub ON b.HashID = ub.HashID
WHERE b.BatchSeq = @BatchSeq 
GROUP BY ub.NormText, b.HashID
ORDER BY Duration_ms DESC;
```

**Output Interpretation**:
- **Duration_ms**: Total time spent in this statement
- **CPU_ms**: CPU time for this statement
- **Occurrences**: How many times this statement executed (if batch ran multiple times)
- **NormText**: Normalized statement text
- **StatementHashID**: Unique ID for this statement

**How to Get @BatchSeq**:

```sql
-- First find the batch you're interested in
SELECT TOP 20
    b.BatchSeq,
    b.StartTime,
    b.Duration/1000 AS Duration_ms,
    SUBSTRING(ub.NormText, 1, 100) AS BatchText,
    b.HashID AS BatchHashID
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
ORDER BY b.Duration DESC;

-- Then use the BatchSeq value in Query #11
```

**Analysis Patterns**:

1. **One statement consumes 80%+ of batch duration**:
   - Identify problematic statement
   - Optimize that specific statement

2. **Multiple statements with similar duration**:
   - Batch has multiple bottlenecks
   - Prioritize by Duration_ms

3. **High Duration but low CPU**:
   - Statement is wait-bound
   - Check waits using StatementHashID with other queries

**Example Workflow**:
```
Batch: usp_ProcessOrders (BatchSeq = 12345, Duration = 5000ms)
  ├─ Statement 1: SELECT FROM Orders (500ms, 10% of batch)
  ├─ Statement 2: UPDATE OrderStatus (4200ms, 84% of batch) ← PROBLEM
  └─ Statement 3: INSERT INTO OrderLog (300ms, 6% of batch)

Action: Focus on Statement 2 (UPDATE OrderStatus)
```

---

### Query #14: Queries with High Wait Percentage
**MCP Tool**: `get_wait_heavy_queries`  
**Purpose**: Find queries spending most time waiting vs executing (wait-bound queries)  
**Use When**: Identifying queries bottlenecked by waits rather than CPU

```sql
WITH BatchesData (Session, starttime, endtime, hashid, cpu, duration, CpuPercentOfDuration, NormText)
AS 
(
    SELECT 
        Session, 
        starttime, 
        endtime, 
        b.hashid, 
        cpu, 
        duration, 
        (CPU * 1.0 / NULLIF(Duration, 0)) AS CpuPercentOfDuration, 
        SUBSTRING(NormText, 1, 500) AS NormText
    FROM ReadTrace.tblBatches b 
    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
    WHERE duration != 0
)
SELECT TOP 100 
    MAX(r.wait_duration_ms) AS MaxWaitDuration_ms, 
    SUM(r.wait_duration_ms) AS TotalWaitDuration_ms,
    COUNT(*) AS WaitOccurrences,
    AVG(t.duration)/1000.0 AS AvgQueryDuration_ms,
    AVG(t.CpuPercentOfDuration * 100) AS AvgCpuPct,
    100 - AVG(t.CpuPercentOfDuration * 100) AS AvgWaitPct,
    r.wait_type, 
    t.NormText AS Query,
    t.hashid
FROM tbl_REQUESTS r
JOIN BatchesData t
    ON r.runtime BETWEEN t.starttime AND t.endtime
    AND r.session_id = t.Session
WHERE t.CpuPercentOfDuration < 0.80   -- CPU is less than 80% of duration (i.e., >20% waiting)
    AND r.task_state != 'running' 
    AND r.task_state != 'runnable' 
    AND r.wait_type IS NOT NULL
GROUP BY r.wait_type, t.NormText, t.hashid
ORDER BY TotalWaitDuration_ms DESC;
```

**Output Interpretation**:
- **MaxWaitDuration_ms**: Longest single wait instance for this query/wait combo
- **TotalWaitDuration_ms**: Cumulative wait time
- **WaitOccurrences**: How many times query waited on this wait type
- **AvgQueryDuration_ms**: Average total query duration
- **AvgCpuPct**: Average CPU percentage of duration
- **AvgWaitPct**: Average wait percentage of duration (100 - CPU%)
- **wait_type**: What the query is waiting on

**Analysis Patterns**:

1. **AvgWaitPct > 80% (i.e., AvgCpuPct < 20%)**:
   - Query spends most time waiting, not executing
   - Focus on eliminating waits, not optimizing query logic

2. **High TotalWaitDuration_ms + PAGEIOLATCH_SH**:
   - I/O-bound query
   - Action: Add indexes, review execution plan for scans

3. **High TotalWaitDuration_ms + LCK_M_***:
   - Frequently blocked query
   - Action: Reduce transaction scope, add indexes, review locking

4. **High TotalWaitDuration_ms + WRITELOG**:
   - Heavy write activity
   - Action: Batch operations, move log to faster storage

**Example Output**:
```
MaxWaitDuration_ms | TotalWaitDuration_ms | WaitOccurrences | AvgQueryDuration_ms | AvgCpuPct | AvgWaitPct | wait_type      | Query
-------------------|----------------------|-----------------|---------------------|-----------|------------|----------------|-------
5234               | 245821               | 523             | 1250                | 15        | 85         | PAGEIOLATCH_SH | SELECT * FROM Orders...
3421               | 128456               | 312             | 980                 | 22        | 78         | LCK_M_X        | UPDATE Inventory...
```

**Action**: Top 10 queries by TotalWaitDuration_ms are prime optimization candidates

---

## WORKFLOW EXAMPLES

### Scenario 1: "usp_ProcessOrders is slow - which statement is the problem?"

```
Step 1: Find the batch
   SELECT BatchSeq, Duration, NormText 
   FROM ReadTrace.tblBatches b
   JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
   WHERE ub.NormText LIKE '%usp_ProcessOrders%'
   ORDER BY Duration DESC;

Step 2: Get BatchSeq value (e.g., 12345)

Step 3: Run Query #11 with @BatchSeq = 12345

Step 4: Identify statement consuming most Duration_ms

Step 5: For problematic statement:
   - Review execution plan
   - Check for missing indexes
   - Analyze wait types
```

---

### Scenario 2: "Which queries are causing PAGEIOLATCH waits?"

```
Step 1: Run Query #6 filtered for PAGEIOLATCH waits
   ...
   WHERE r.wait_type LIKE 'PAGEIOLATCH%'
   ...

Step 2: Identify top 5 queries by WaitDensity_ms

Step 3: For each query:
   - Run Query #7 to find specific pages causing contention
   - Use scenario-io.md Query #26 to check query's read patterns
   - Review execution plan for scans

Step 4: Remediation:
   - Add missing indexes
   - Optimize queries to reduce reads
   - Check storage performance with scenario-io.md Query #16
```

---

### Scenario 3: "Find all wait-bound queries (spending most time waiting)"

```
Step 1: Run Query #14 (Queries with High Wait Percentage)

Step 2: Focus on top 10 by TotalWaitDuration_ms

Step 3: Group by wait_type to identify patterns:
   - All PAGEIOLATCH? → I/O optimization needed
   - All LCK_M_*? → Blocking/locking optimization needed
   - Mixed? → Case-by-case analysis

Step 4: For each wait type:
   - PAGEIOLATCH → scenario-io.md
   - LCK_M_* → scenario-blocking.md
   - WRITELOG → scenario-io.md (log file focus)
   - SOS_SCHEDULER_YIELD → scenario-cpu.md
```

---

### Scenario 4: "Hot spot analysis - which resource is most contended?"

```
Step 1: Run Query #7 (Waits Aggregated by Wait Resource)

Step 2: Identify top 5 resources by occurrences

Step 3: For each contended resource:
   - Decode wait_resource to find table/page
   - Run Query #6 to find queries accessing this resource
   - Review queries for optimization

Step 4: Remediation based on resource type:
   - PAGE contention → Partitioning, index redesign
   - KEY contention → Application-level queuing
   - OBJECT contention → Reduce lock escalation
```

---

## INTEGRATION WITH OTHER SCENARIOS

### If Wait Analysis Points to I/O
→ Use [scenario-io.md](scenario-io.md) for file-level and query-level I/O analysis

### If Wait Analysis Points to Blocking
→ Use [scenario-blocking.md](scenario-blocking.md) for head blocker and blocking chain analysis

### If Wait Analysis Points to CPU
→ Use [scenario-cpu.md](scenario-cpu.md) for CPU utilization and top CPU queries

### If Need Application-Specific Wait Analysis
→ Combine with [scenario-application-analysis.md](scenario-application-analysis.md) Query #12

---

## ADVANCED TECHNIQUES

### Correlate Statement-Level Waits (Requires DetailedPerf)

```sql
-- Find waits for specific statement within batch
DECLARE @StatementHashID BIGINT = <StatementHashID_from_Query11>;

SELECT 
    r.runtime,
    r.wait_type,
    r.wait_duration_ms,
    r.wait_resource,
    s.NormText AS Statement
FROM tbl_REQUESTS r
JOIN ReadTrace.tblStatements s
    ON r.session_id = s.Session
    AND r.runtime BETWEEN s.StartTime AND s.EndTime
JOIN ReadTrace.tblUniqueStatements us ON s.HashID = us.HashID
WHERE s.HashID = @StatementHashID
    AND r.wait_type IS NOT NULL
ORDER BY r.wait_duration_ms DESC;
```

### Find Queries Waiting on Specific Table/Object

```sql
-- Find which queries are contending on specific table
-- First get object_id of table
DECLARE @object_id INT;
SELECT @object_id = OBJECT_ID('dbo.YourTableName');

-- Find waits on this object
SELECT 
    r.wait_type,
    r.wait_duration_ms,
    q.procname,
    q.stmt_text,
    r.wait_resource
FROM tbl_REQUESTS r
JOIN tbl_NOTABLEACTIVEQUERIES q
    ON r.session_id = q.session_id
    AND r.runtime = q.runtime
WHERE r.wait_resource LIKE '%:' + CAST(@object_id AS VARCHAR(20)) + '%'
ORDER BY r.wait_duration_ms DESC;
```

---

## NEXT STEPS BASED ON FINDINGS

### If Specific Statement in Batch is Slow
→ Isolate statement, review execution plan, apply optimizations

### If Wait-Bound Queries Identified
→ Address root cause waits (I/O, blocking, etc.) before query tuning

### If Resource Contention Found
→ Redesign schema, add partitioning, or implement application-level queuing

### If Need Index Recommendations
→ Use [scenario-index-optimization.md](scenario-index-optimization.md) Query #26
