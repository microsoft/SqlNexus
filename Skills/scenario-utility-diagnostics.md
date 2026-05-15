# Utility & Diagnostic Information - Reference Guide

## PURPOSE
Quick reference queries for system diagnostics, configuration checks, data validation, and troubleshooting utilities.

---

## WHEN TO USE
- Validate data collection coverage
- Check SQL Server configuration
- Identify bottleneck stored procedures
- Verify diagnostic data completeness
- Baseline system settings

---

## EMBEDDED QUERIES

### Query #19: Bottleneck SP (Stored Procedure Analysis)
**Purpose**: Identify stored procedures causing performance issues  
**Use When**: Need to find which stored procedures are slowest or most resource-intensive

```sql
-- Requires tbl_NOTABLEACTIVEQUERIES or similar execution tracking table
IF OBJECT_ID('tbl_NOTABLEACTIVEQUERIES') IS NOT NULL
BEGIN
    SELECT 
        procname AS StoredProcedureName,
        COUNT(*) AS Execution_Count,
        AVG(DATEDIFF(MILLISECOND, start_time, ISNULL(end_time, GETDATE()))) AS Avg_Duration_ms,
        MAX(DATEDIFF(MILLISECOND, start_time, ISNULL(end_time, GETDATE()))) AS Max_Duration_ms,
        SUM(DATEDIFF(MILLISECOND, start_time, ISNULL(end_time, GETDATE()))) AS Total_Duration_ms,
        AVG(cpu_time) AS Avg_CPU_ms,
        SUM(cpu_time) AS Total_CPU_ms,
        AVG(logical_reads) AS Avg_Logical_Reads,
        SUM(logical_reads) AS Total_Logical_Reads,
        AVG(writes) AS Avg_Writes,
        SUM(writes) AS Total_Writes,
        -- Calculate wait time if available
        AVG(wait_time) AS Avg_Wait_ms,
        SUM(wait_time) AS Total_Wait_ms
    FROM tbl_NOTABLEACTIVEQUERIES
    WHERE procname IS NOT NULL 
        AND procname <> ''
    GROUP BY procname
    ORDER BY Total_Duration_ms DESC;
END
ELSE
BEGIN
    -- Fallback: Use ReadTrace data if available
    IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
    BEGIN
        SELECT 
            ub.NormText AS StoredProcedureName,
            COUNT(*) AS Execution_Count,
            AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
            MAX(b.Duration) / 1000.0 AS Max_Duration_ms,
            SUM(b.Duration) / 1000.0 AS Total_Duration_ms,
            AVG(b.CPU) AS Avg_CPU_ms,
            SUM(b.CPU) AS Total_CPU_ms,
            AVG(b.Reads) AS Avg_Logical_Reads,
            SUM(b.Reads) AS Total_Logical_Reads,
            AVG(b.Writes) AS Avg_Writes,
            SUM(b.Writes) AS Total_Writes
        FROM ReadTrace.tblBatches b
        JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
        WHERE ub.NormText LIKE 'exec%' OR ub.NormText LIKE 'EXEC%'
            OR ub.NormText LIKE 'sp_%' OR ub.NormText LIKE 'usp_%'
        GROUP BY ub.NormText
        ORDER BY Total_Duration_ms DESC;
    END
END
```

**Output Interpretation**:
- **StoredProcedureName**: Name of stored procedure
- **Execution_Count**: How many times executed during collection
- **Avg_Duration_ms**: Average execution time
- **Max_Duration_ms**: Longest execution
- **Total_Duration_ms**: Cumulative time (Execution_Count × Avg_Duration_ms)
- **Avg_CPU_ms / Total_CPU_ms**: CPU consumption
- **Avg_Logical_Reads / Total_Logical_Reads**: I/O activity
- **Avg_Wait_ms / Total_Wait_ms**: Wait time (if available)

**Analysis Patterns**:

1. **High Total_Duration_ms**:
   - Procedure consuming most overall time
   - Priority for optimization
   - May be frequently called or slow per execution

2. **High Avg_Duration_ms but low Execution_Count**:
   - Slow procedure but infrequent
   - Evaluate business criticality before optimizing

3. **High Execution_Count**:
   - Frequently-called procedure
   - Even small optimization has large impact
   - Check if caching is possible

4. **High Avg_CPU_ms relative to Avg_Duration_ms**:
   - CPU-bound procedure
   - Review for inefficient logic, loops, cursors

5. **High Avg_Logical_Reads**:
   - I/O-intensive procedure
   - Review for missing indexes, large scans

**Use Case Examples**:
```
Example Output:
StoredProcedureName: usp_GetCustomerOrders
Execution_Count: 15234
Avg_Duration_ms: 250
Total_Duration_ms: 3,808,500  ← 3.8 million ms total (63 minutes!)
Avg_CPU_ms: 180
Avg_Logical_Reads: 25000

Analysis: 
- Frequently called (15K times)
- Moderate duration per call (250ms)
- But cumulative impact is huge (63 minutes total)
- High reads (25K per call)

Action:
- Add indexes to reduce reads
- Consider caching results
- Review if all 15K calls are necessary
```

**Integration**:
- Found slow SP? → Break down with Query #11 (scenario-query-deepdive-wait-analysis.md)
- High reads? → Check missing indexes with Query #26 (scenario-index-optimization.md)
- High CPU? → Investigate with scenario-cpu.md queries

---

### Query #22: Check Tables in Collection
**Purpose**: Validate which tables/objects were captured in the diagnostic collection  
**Use When**: Verifying data collection completeness, checking if specific tables were monitored

```sql
-- Check what tables are referenced in the diagnostic data
IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
BEGIN
    -- From trace data
    SELECT DISTINCT
        DatabaseID,
        DB_NAME(DatabaseID) AS DatabaseName,
        ObjectID,
        OBJECT_NAME(ObjectID, DatabaseID) AS ObjectName,
        COUNT(*) AS Reference_Count
    FROM ReadTrace.tblBatches
    WHERE ObjectID IS NOT NULL
        AND DatabaseID IS NOT NULL
    GROUP BY DatabaseID, ObjectID
    ORDER BY Reference_Count DESC;
END

-- Also check system tables captured
IF OBJECT_ID('tbl_DATABASES') IS NOT NULL
BEGIN
    SELECT 
        database_id,
        name AS DatabaseName,
        state_desc,
        recovery_model_desc,
        create_date,
        compatibility_level,
        is_read_only,
        is_auto_close_on,
        is_auto_shrink_on,
        runtime AS CollectionTime
    FROM tbl_DATABASES
    WHERE name NOT IN ('master', 'model', 'msdb', 'tempdb')
    ORDER BY name;
END

-- Check collected index information
IF OBJECT_ID('tbl_INDEXES') IS NOT NULL
BEGIN
    SELECT 
        database_name,
        table_name,
        index_name,
        index_type,
        is_unique,
        is_primary_key,
        COUNT(*) OVER (PARTITION BY database_name, table_name) AS indexes_on_table
    FROM tbl_INDEXES
    WHERE database_name NOT IN ('master', 'model', 'msdb', 'tempdb')
    ORDER BY database_name, table_name, index_name;
END
```

**Output Interpretation**:
- Lists all tables/objects referenced in queries during collection
- Reference_Count shows how often each table was accessed
- Helps validate collection scope and identify hot tables

**Use Cases**:
1. **Validate specific table was monitored**:
   - Search for your table name in results
   - If missing, queries against that table may not have been captured

2. **Identify most-accessed tables**:
   - Tables with high Reference_Count are critical
   - Focus optimization on these tables

3. **Check database coverage**:
   - Verify all relevant databases were monitored
   - Identify if collection missed a database

**Example Usage**:
```sql
-- Find if specific table 'Orders' was captured
SELECT * FROM (
    -- [Run Query #22]
) WHERE ObjectName = 'Orders';

-- If not found, collection may not have captured queries against this table
-- Or queries were too fast to be captured (depends on collection thresholds)
```

---

### Query #23: Perfstats Coverage (Data Collection Validation)
**Purpose**: Verify completeness and time range of diagnostic data collection  
**Use When**: Validating collection quality, checking for gaps in data

```sql
-- Check time range of collection
IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
BEGIN
    SELECT 
        'Trace Data Coverage' AS DataType,
        MIN(StartTime) AS Collection_Start,
        MAX(EndTime) AS Collection_End,
        DATEDIFF(MINUTE, MIN(StartTime), MAX(EndTime)) AS Duration_Minutes,
        COUNT(*) AS Total_Batches,
        COUNT(DISTINCT CAST(StartTime AS DATE)) AS Days_Covered,
        COUNT(DISTINCT Session) AS Unique_Sessions
    FROM ReadTrace.tblBatches;
END

-- Check performance counter data coverage
IF OBJECT_ID('CounterData') IS NOT NULL AND OBJECT_ID('CounterDetails') IS NOT NULL
BEGIN
    SELECT 
        'Performance Counters' AS DataType,
        MIN(cd.CounterDateTime) AS Collection_Start,
        MAX(cd.CounterDateTime) AS Collection_End,
        DATEDIFF(MINUTE, MIN(cd.CounterDateTime), MAX(cd.CounterDateTime)) AS Duration_Minutes,
        COUNT(*) AS Total_Samples,
        COUNT(DISTINCT cdt.CounterName) AS Unique_Counters
    FROM CounterData cd
    JOIN CounterDetails cdt ON cd.CounterID = cdt.CounterID;
END

-- Check wait stats coverage
IF OBJECT_ID('tbl_OS_WAIT_STATS') IS NOT NULL
BEGIN
    SELECT 
        'Wait Stats' AS DataType,
        MIN(runtime) AS Collection_Start,
        MAX(runtime) AS Collection_End,
        DATEDIFF(MINUTE, MIN(runtime), MAX(runtime)) AS Duration_Minutes,
        COUNT(*) AS Total_Snapshots,
        COUNT(DISTINCT wait_type) AS Unique_Wait_Types
    FROM tbl_OS_WAIT_STATS;
END

-- Check requests/active queries coverage
IF OBJECT_ID('tbl_REQUESTS') IS NOT NULL
BEGIN
    SELECT 
        'Active Requests' AS DataType,
        MIN(runtime) AS Collection_Start,
        MAX(runtime) AS Collection_End,
        DATEDIFF(MINUTE, MIN(runtime), MAX(runtime)) AS Duration_Minutes,
        COUNT(*) AS Total_Requests,
        COUNT(DISTINCT session_id) AS Unique_Sessions
    FROM tbl_REQUESTS;
END

-- Check blocking data coverage
IF OBJECT_ID('tbl_BLOCKING') IS NOT NULL
BEGIN
    SELECT 
        'Blocking Data' AS DataType,
        MIN(runtime) AS Collection_Start,
        MAX(runtime) AS Collection_End,
        DATEDIFF(MINUTE, MIN(runtime), MAX(runtime)) AS Duration_Minutes,
        COUNT(*) AS Total_Blocking_Events,
        COUNT(DISTINCT blocked_session_id) AS Unique_Blocked_Sessions
    FROM tbl_BLOCKING;
END

-- Summary: Check for gaps in data
SELECT 
    'Data Collection Summary' AS Summary,
    CASE 
        WHEN EXISTS (SELECT 1 FROM ReadTrace.tblBatches) THEN 'Available' 
        ELSE 'Missing' 
    END AS Trace_Data,
    CASE 
        WHEN EXISTS (SELECT 1 FROM CounterData) THEN 'Available' 
        ELSE 'Missing' 
    END AS Performance_Counters,
    CASE 
        WHEN EXISTS (SELECT 1 FROM tbl_OS_WAIT_STATS) THEN 'Available' 
        ELSE 'Missing' 
    END AS Wait_Stats,
    CASE 
        WHEN EXISTS (SELECT 1 FROM tbl_REQUESTS) THEN 'Available' 
        ELSE 'Missing' 
    END AS Request_Data,
    CASE 
        WHEN EXISTS (SELECT 1 FROM tbl_BLOCKING) THEN 'Available' 
        ELSE 'Missing' 
    END AS Blocking_Data;
```

**Output Interpretation**:
- **Collection_Start/End**: Time range of data
- **Duration_Minutes**: How long collection ran
- **Total_Batches/Samples/Events**: Volume of data collected
- **Unique_Sessions/Counters**: Diversity of data

**Validation Checks**:

1. **Duration_Minutes matches expected**:
   - If collected for 2 hours, should see ~120 minutes
   - If much shorter, collection may have failed

2. **All data types present**:
   - Trace_Data: Essential for query analysis
   - Performance_Counters: For CPU, memory, I/O metrics
   - Wait_Stats: For wait analysis
   - Blocking_Data: For blocking analysis

3. **Sufficient volume**:
   - Total_Batches should be proportional to workload
   - If too few, collection thresholds may be too high

4. **Time alignment**:
   - All data types should have similar Collection_Start/End
   - If not aligned, some data may be incomplete

**Example Red Flags**:
```
Trace Data Coverage:
  Collection_Start: 2024-01-15 10:00:00
  Collection_End: 2024-01-15 10:05:00
  Duration_Minutes: 5  ← Only 5 minutes! Expected 60 minutes.
  
Issue: Collection terminated early. Data incomplete.

OR

Blocking_Data: Missing

Issue: No blocking data collected. If blocking was issue, can't diagnose.
```

---

### Query #24: Server Configuration Settings
**Purpose**: Review SQL Server configuration for optimization opportunities and troubleshooting  
**Use When**: Checking server settings, baseline documentation, troubleshooting

```sql
-- Current server configuration
SELECT 
    configuration_id,
    name AS ConfigurationOption,
    value AS CurrentValue,
    value_in_use AS ValueInUse,
    minimum AS MinValue,
    maximum AS MaxValue,
    is_dynamic,
    is_advanced,
    description
FROM sys.configurations
WHERE 
    -- Focus on commonly-reviewed settings
    name IN (
        'max degree of parallelism',
        'cost threshold for parallelism',
        'max server memory (MB)',
        'min server memory (MB)',
        'optimize for ad hoc workloads',
        'backup compression default',
        'remote admin connections',
        'fill factor (%)',
        'priority boost',
        'affinity mask',
        'lightweight pooling',
        'max worker threads'
    )
    OR is_advanced = 0  -- Include all non-advanced settings
ORDER BY name;

-- If historical configuration data available
IF OBJECT_ID('tbl_dm_server_configuration') IS NOT NULL
BEGIN
    SELECT 
        name AS ConfigurationOption,
        value AS Value,
        value_in_use AS ValueInUse,
        runtime AS CollectionTime
    FROM tbl_dm_server_configuration
    WHERE name IN (
        'max degree of parallelism',
        'cost threshold for parallelism',
        'max server memory (MB)',
        'min server memory (MB)',
        'optimize for ad hoc workloads'
    )
    ORDER BY name, runtime DESC;
END
```

**Output Interpretation**:
- **ConfigurationOption**: Setting name
- **CurrentValue**: Configured value (what you set)
- **ValueInUse**: Active value (may differ if not dynamic)
- **is_dynamic**: 1 = Takes effect immediately, 0 = Requires restart

**Key Settings to Review**:

#### 1. **max degree of parallelism (MAXDOP)**
```
Recommended:
- OLTP workload: 4-8 (limit parallelism)
- Reporting/DW: 0 (unlimited) or = # of cores

Common Issues:
- MAXDOP = 0 on OLTP: Excessive parallelism causes CXPACKET waits
- MAXDOP = 1 on reporting: No parallelism, slow queries

Action if not optimal:
EXEC sp_configure 'max degree of parallelism', 4;
RECONFIGURE;
```

#### 2. **cost threshold for parallelism**
```
Default: 5 (very low, causes over-parallelization)
Recommended: 25-50 for most workloads

Common Issue:
- Default (5): Even small queries go parallel, causing overhead

Action:
EXEC sp_configure 'cost threshold for parallelism', 25;
RECONFIGURE;
```

#### 3. **max server memory (MB)**
```
Critical for performance!

Recommended: 
- Leave 4-8 GB for OS
- For 64 GB server: Set max server memory to 56 GB

Common Issue:
- Not set (default): SQL Server can consume all memory, starving OS

Action:
EXEC sp_configure 'max server memory (MB)', 56320;  -- 55 GB
RECONFIGURE;
```

#### 4. **min server memory (MB)**
```
Recommended: 
- Usually not set (0) is fine
- Or set to same as max server memory for dedicated SQL servers

Common Issue:
- Set too high: SQL reserves memory it may not need
```

#### 5. **optimize for ad hoc workloads**
```
Recommended: 1 (enabled) for OLTP with many ad-hoc queries

What it does:
- Stores only plan stub on first execution
- Stores full plan on second execution
- Reduces plan cache bloat

Action if high compilation rate:
EXEC sp_configure 'optimize for ad hoc workloads', 1;
RECONFIGURE;
```

#### 6. **priority boost** ⚠️
```
Recommended: 0 (disabled)

DANGER:
- If set to 1, SQL Server runs at higher OS priority
- Can destabilize entire server
- Microsoft does not recommend enabling

Action if enabled:
EXEC sp_configure 'priority boost', 0;
RECONFIGURE;  -- Requires restart
```

#### 7. **lightweight pooling** ⚠️
```
Recommended: 0 (disabled)

DANGER:
- Uses fiber mode instead of threads
- Can cause issues with many features
- Microsoft does not recommend enabling

Action if enabled:
EXEC sp_configure 'lightweight pooling', 0;
RECONFIGURE;  -- Requires restart
```

---

### Query #25: Database Configuration Settings
**Purpose**: Review database-level settings for each database  
**Use When**: Database-specific optimization, troubleshooting, baseline documentation

```sql
-- Current database configuration
SELECT 
    database_id,
    name AS DatabaseName,
    state_desc AS State,
    recovery_model_desc AS RecoveryModel,
    compatibility_level AS CompatibilityLevel,
    collation_name AS Collation,
    is_read_only AS IsReadOnly,
    is_auto_close_on AS AutoClose,
    is_auto_shrink_on AS AutoShrink,
    is_auto_create_stats_on AS AutoCreateStats,
    is_auto_update_stats_on AS AutoUpdateStats,
    is_auto_update_stats_async_on AS AutoUpdateStatsAsync,
    page_verify_option_desc AS PageVerify,
    is_read_committed_snapshot_on AS RCSI_Enabled,
    snapshot_isolation_state_desc AS SnapshotIsolation,
    is_parameterization_forced AS ForcedParameterization,
    is_query_store_on AS QueryStoreEnabled,
    -- File information
    (SELECT SUM(size) * 8 / 1024 FROM sys.master_files WHERE database_id = d.database_id AND type = 0) AS DataFileSizeMB,
    (SELECT SUM(size) * 8 / 1024 FROM sys.master_files WHERE database_id = d.database_id AND type = 1) AS LogFileSizeMB
FROM sys.databases d
WHERE name NOT IN ('master', 'model', 'msdb', 'tempdb')
ORDER BY name;

-- If historical database configuration available
IF OBJECT_ID('tbl_DATABASES') IS NOT NULL
BEGIN
    SELECT 
        database_id,
        name AS DatabaseName,
        state_desc,
        recovery_model_desc,
        compatibility_level,
        is_auto_close_on,
        is_auto_shrink_on,
        is_auto_create_stats_on,
        is_auto_update_stats_on,
        is_read_committed_snapshot_on,
        runtime AS CollectionTime
    FROM tbl_DATABASES
    WHERE name NOT IN ('master', 'model', 'msdb', 'tempdb')
    ORDER BY name, runtime DESC;
END
```

**Output Interpretation**:
- **State**: ONLINE (good), OFFLINE/RESTORING (issue)
- **RecoveryModel**: FULL, SIMPLE, or BULK_LOGGED
- **CompatibilityLevel**: 100=SQL2008, 110=SQL2012, 120=SQL2014, 130=SQL2016, 140=SQL2017, 150=SQL2019, 160=SQL2022

**Key Settings to Review**:

#### 1. **AutoClose** ⚠️
```
Recommended: 0 (disabled)

Issue if enabled (1):
- Database closes after last connection disconnects
- Next connection has to re-open database (slow)
- Causes performance issues

Action if enabled:
ALTER DATABASE [YourDB] SET AUTO_CLOSE OFF;
```

#### 2. **AutoShrink** ⚠️
```
Recommended: 0 (disabled)

Issue if enabled (1):
- SQL Server automatically shrinks files
- Causes index fragmentation
- Performance degradation

Action if enabled:
ALTER DATABASE [YourDB] SET AUTO_SHRINK OFF;
```

#### 3. **AutoCreateStats / AutoUpdateStats**
```
Recommended: Both enabled (1)

Critical for performance:
- Auto-create: Creates statistics for columns without them
- Auto-update: Updates statistics when data changes

Issue if disabled (0):
- Query optimizer has no statistics
- Generates terrible execution plans

Action if disabled:
ALTER DATABASE [YourDB] SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE [YourDB] SET AUTO_UPDATE_STATISTICS ON;
```

#### 4. **AutoUpdateStatsAsync**
```
Recommended: Usually disabled (0)

When to enable (1):
- Large databases where stats updates cause query delays
- If enabled, query doesn't wait for stats update (uses old stats)

Trade-off:
- Enabled = Queries don't block but may use old stats
- Disabled = Queries wait for stats update but always use current stats
```

#### 5. **PageVerify**
```
Recommended: CHECKSUM

Options:
- CHECKSUM: Detects page corruption (recommended)
- TORN_PAGE_DETECTION: Basic detection (older method)
- NONE: No detection (not recommended)

Action if not CHECKSUM:
ALTER DATABASE [YourDB] SET PAGE_VERIFY CHECKSUM;
```

#### 6. **RCSI_Enabled (Read Committed Snapshot Isolation)**
```
Common for modern applications: 1 (enabled)

Benefits:
- Readers don't block writers
- Writers don't block readers
- Uses row versioning in tempdb

Trade-off:
- Increased tempdb usage
- Small performance overhead

When to enable:
- Blocking issues due to reads blocking writes
- Application uses read-committed isolation level

Action:
-- Requires no other connections to database
ALTER DATABASE [YourDB] SET READ_COMMITTED_SNAPSHOT ON;
```

#### 7. **CompatibilityLevel**
```
Recommendation: Match SQL Server version for latest features

Examples:
- SQL Server 2019 → Compatibility 150
- SQL Server 2017 → Compatibility 140
- SQL Server 2016 → Compatibility 130

Issue:
- Old compatibility level (e.g., 100 on SQL 2019) uses old CE and features
- May be intentional (regression avoidance) or oversight

Action to upgrade:
ALTER DATABASE [YourDB] SET COMPATIBILITY_LEVEL = 150;
-- Test thoroughly! Query plans may change.
```

#### 8. **QueryStoreEnabled**
```
Recommended: 1 (enabled) for SQL 2016+

Benefits:
- Tracks query performance over time
- Identifies regressions
- Can force plans

Action:
ALTER DATABASE [YourDB] SET QUERY_STORE = ON;
```

---

## WORKFLOW EXAMPLES

### Scenario 1: "Which stored procedures are causing performance issues?"

```
Step 1: Run Query #19 (Bottleneck SP)
Step 2: Identify top 5 by Total_Duration_ms
Step 3: For each SP:
   - Note Avg_Duration_ms and Execution_Count
   - Check Avg_Logical_Reads (I/O-bound?)
   - Check Avg_CPU_ms (CPU-bound?)
Step 4: Deep dive into problem SP:
   - Use Query #11 to break down SP into statements
   - Use scenario-performance.md for query optimization
Step 5: After optimization, re-run Query #19 to verify improvement
```

---

### Scenario 2: "Is my data collection complete?"

```
Step 1: Run Query #23 (Perfstats Coverage)
Step 2: Check each data type:
   - Trace Data: Should have 1000+ batches for typical workload
   - Performance Counters: Should cover entire collection period
   - Wait Stats: Should have multiple snapshots
   - Blocking Data: May be empty if no blocking occurred
Step 3: Verify time range matches expected collection window
Step 4: If gaps found:
   - Re-run collection with lower thresholds
   - Ensure collection ran for full duration
```

---

### Scenario 3: "Baseline server configuration"

```
Step 1: Run Query #24 (Server Configuration)
Step 2: Document current settings for all key options
Step 3: Compare to best practices:
   - MAXDOP: 4-8 for OLTP
   - Cost threshold: 25-50
   - Max server memory: Set appropriately
   - Optimize for ad hoc: Enabled for OLTP
Step 4: Run Query #25 (Database Configuration)
Step 5: Check for issues:
   - AutoClose/AutoShrink enabled (bad)
   - AutoCreateStats/AutoUpdateStats disabled (bad)
   - PageVerify not CHECKSUM (sub-optimal)
Step 6: Document all settings for future reference
Step 7: Apply fixes for any issues found
```

---

### Scenario 4: "Review settings after server migration"

```
Step 1: Run Query #24 on old server (pre-migration)
Step 2: Document settings
Step 3: Run Query #24 on new server (post-migration)
Step 4: Compare settings:
   - Max server memory adjusted for new hardware?
   - MAXDOP appropriate for new # of cores?
   - All custom settings migrated?
Step 5: Run Query #25 for all databases
Step 6: Verify:
   - Compatibility levels correct
   - Database options preserved
   - Recovery models correct
Step 7: Adjust any settings that weren't migrated correctly
```

---

## INTEGRATION WITH OTHER SCENARIOS

### If Bottleneck SP Found
→ Use [scenario-query-deepdive-wait-analysis.md](scenario-query-deepdive-wait-analysis.md) Query #11 to break down statements

### If Server Settings Not Optimal
→ Review impact with [scenario-cpu.md](scenario-cpu.md) for MAXDOP/parallelism
→ Review impact with [scenario-index-optimization.md](scenario-index-optimization.md) Query #28 for "optimize for ad hoc"

### If Database Settings Cause Issues
→ RCSI not enabled causing blocking → [scenario-blocking.md](scenario-blocking.md)
→ AutoShrink causing fragmentation → Performance degradation investigation

---

## BEST PRACTICES

### Server Configuration Review Checklist
- [ ] max server memory set appropriately (not default)
- [ ] MAXDOP matches workload (4-8 for OLTP, 0 for DW)
- [ ] cost threshold for parallelism = 25-50
- [ ] optimize for ad hoc workloads enabled (for OLTP)
- [ ] priority boost = 0 (disabled)
- [ ] lightweight pooling = 0 (disabled)
- [ ] backup compression default = 1 (enabled)

### Database Configuration Review Checklist
- [ ] Auto Close = OFF
- [ ] Auto Shrink = OFF
- [ ] Auto Create Statistics = ON
- [ ] Auto Update Statistics = ON
- [ ] Page Verify = CHECKSUM
- [ ] Compatibility level matches SQL version (or intentionally lower)
- [ ] Recovery model appropriate (FULL for production, SIMPLE for dev/test)
- [ ] Query Store = ON (SQL 2016+)

### Data Collection Validation Checklist
- [ ] Collection duration matches expected
- [ ] Trace data available
- [ ] Performance counters available
- [ ] Wait stats available
- [ ] Request/blocking data available (if issues occurred)
- [ ] All relevant databases captured
- [ ] Time range covers problem period

---

## REFERENCE: Critical Settings Summary

| Setting | Recommended | Issue if Wrong | Fix Priority |
|---------|-------------|----------------|--------------|
| max server memory | Total RAM - 4-8 GB | Memory pressure, paging | 🔴 Critical |
| MAXDOP | 4-8 (OLTP) | CXPACKET waits, blocking | 🔴 Critical |
| Auto Close | OFF | Connection delays | 🟡 Medium |
| Auto Shrink | OFF | Fragmentation | 🟡 Medium |
| Auto Create/Update Stats | ON | Bad plans | 🔴 Critical |
| cost threshold parallelism | 25-50 | Over-parallelization | 🟡 Medium |
| optimize for ad hoc | ON (OLTP) | Plan cache bloat | 🟢 Low |
| Page Verify | CHECKSUM | Can't detect corruption | 🟡 Medium |
| priority boost | OFF | Server instability | 🔴 Critical |
| lightweight pooling | OFF | Feature issues | 🔴 Critical |

---

## NEXT STEPS BASED ON FINDINGS

### If Bottleneck SP Identified
→ Use Query #11 and scenario-query-deepdive-wait-analysis.md for statement-level analysis

### If Configuration Issues Found
→ Apply fixes during maintenance window
→ Document changes
→ Re-baseline performance

### If Data Collection Incomplete
→ Re-run collection with adjusted parameters
→ Ensure coverage of problem period

### If Settings Need Changes
→ Test in non-production first
→ Measure performance before/after
→ Use scenario-comparative-analysis.md Query #21 to validate improvement
