# Index Optimization & Statistics Analysis - Complete Diagnostic Workflow

## SYMPTOM
"Need missing index recommendations" / "Statistics outdated" / "High compilation rate" / "Plan cache churn"

---

## DECISION TREE

### What optimization area?

#### **Missing Indexes** (#26)
- SQL Server detected queries that would benefit from indexes
- Find indexes with highest improvement measure
- Prioritize by user seeks/scans

#### **Statistics Health** (#27)
- Check when statistics were last updated
- Identify stale statistics (high modification_counter)
- Review sample rates

#### **Compilation Performance** (#28)
- Check compilation/recompilation rates
- Identify compilation storms
- Review plan cache efficiency

---

## USE CASES

### Performance Optimization
- Slow queries need indexing strategy
- Outdated statistics causing bad plans
- High CPU from excessive compilation

### Proactive Maintenance
- Regular index review
- Statistics update scheduling
- Plan cache health monitoring

### Query Tuning
- Index recommendations for specific queries
- Statistics-driven plan issues
- Recompilation troubleshooting

---

## EMBEDDED QUERIES

### Query #26: Missing Indexes on the System
**MCP Tool**: `get_missing_indexes`  
**Purpose**: Get SQL Server's missing index recommendations  
**Use When**: Slow queries, high I/O, or proactive optimization

```sql
IF OBJECT_ID ('tbl_MissingIndexes') IS NOT NULL
BEGIN
    DECLARE @max_datetime DATETIME;
    SELECT @max_datetime = MAX(runtime) FROM tbl_MissingIndexes;

    SELECT TOP 30 
        create_index_statement, 
        improvement_measure, 
        user_seeks, 
        user_scans, 
        runtime, 
        object_id,
        equality_columns,
        inequality_columns,
        included_columns,
        avg_total_user_cost,
        avg_user_impact,
        CAST(avg_user_impact * (user_seeks + user_scans) * avg_total_user_cost AS BIGINT) AS impact_score
    FROM tbl_MissingIndexes
    WHERE runtime = @max_datetime
    ORDER BY improvement_measure DESC;
END
```

**Output Interpretation**:
- **create_index_statement**: Ready-to-execute CREATE INDEX statement
- **improvement_measure**: SQL Server's calculated improvement metric (higher = more beneficial)
- **user_seeks**: Number of seeks that would have used this index
- **user_scans**: Number of scans that would have used this index
- **avg_total_user_cost**: Average query cost that would benefit
- **avg_user_impact**: Percentage reduction in query cost (0-100%)
- **impact_score**: Calculated as `avg_user_impact * (seeks + scans) * avg_total_user_cost`

**Index Columns**:
- **equality_columns**: Columns used in `WHERE col = value` (should be key columns)
- **inequality_columns**: Columns used in `WHERE col > value` or ranges (should be key columns after equality)
- **included_columns**: Columns in SELECT/WHERE but not for seek (should be INCLUDE)

**Analysis Patterns**:

1. **High improvement_measure (> 100000)**:
   - Very high impact index
   - Likely frequently-used query with expensive scans
   - **Priority**: Implement immediately

2. **High user_seeks/user_scans**:
   - Index would be used frequently
   - Good candidate even if avg_user_impact is moderate
   - **Priority**: High

3. **High avg_user_impact (> 80%) but low seeks/scans**:
   - Expensive query but infrequent
   - **Priority**: Medium (evaluate business criticality)

4. **Many indexes on same table**:
   - Possible over-indexing recommendation
   - **Priority**: Review existing indexes first, consolidate if possible

**Before Creating Indexes**:

```sql
-- Check existing indexes on table to avoid duplicates
DECLARE @object_id INT = <object_id_from_missing_index>;

SELECT 
    i.name AS IndexName,
    i.type_desc,
    i.is_unique,
    i.is_primary_key,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS key_columns,
    i.index_id
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = @object_id
    AND ic.is_included_column = 0
GROUP BY i.name, i.type_desc, i.is_unique, i.is_primary_key, i.index_id
ORDER BY i.index_id;
```

**Best Practices**:
1. **Review create_index_statement**: Customize index name
2. **Check for duplicates**: Don't create redundant indexes
3. **Test in non-prod first**: Measure impact before prod deployment
4. **Monitor index usage**: Track if new index is actually used
5. **Consider consolidation**: Combine similar indexes with INCLUDE

**Example Output**:
```
create_index_statement: CREATE INDEX [IX_Orders_CustomerId_OrderDate] ON [dbo].[Orders] (CustomerId, OrderDate) INCLUDE (OrderTotal)
improvement_measure: 258456
user_seeks: 5234
user_scans: 892
avg_user_impact: 85.2
impact_score: 1523456

Action: HIGH PRIORITY - Frequently-used query, high impact
```

---

### Query #26a: Missing Index Details with Query Context
**MCP Tool**: `get_missing_indexes`  
**Purpose**: See which queries would benefit from each missing index  
**Use When**: Need to understand query patterns for index decision

```sql
IF OBJECT_ID ('tbl_MissingIndexes') IS NOT NULL 
    AND OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
BEGIN
    DECLARE @max_datetime DATETIME;
    SELECT @max_datetime = MAX(runtime) FROM tbl_MissingIndexes;

    -- Cross-reference with slow queries on same objects
    SELECT 
        mi.create_index_statement,
        mi.improvement_measure,
        mi.object_id,
        OBJECT_NAME(mi.object_id) AS TableName,
        mi.equality_columns,
        mi.inequality_columns,
        mi.included_columns,
        COUNT(DISTINCT b.HashID) AS related_query_count,
        SUM(b.Duration)/1000 AS total_query_duration_ms
    FROM tbl_MissingIndexes mi
    LEFT JOIN ReadTrace.tblBatches b
        ON b.ObjectID = mi.object_id  -- May need adjustment based on schema
    WHERE mi.runtime = @max_datetime
    GROUP BY 
        mi.create_index_statement,
        mi.improvement_measure,
        mi.object_id,
        mi.equality_columns,
        mi.inequality_columns,
        mi.included_columns
    ORDER BY mi.improvement_measure DESC;
END
```

---

### Query #27: Table Statistics for Query Optimizer
**MCP Tool**: `get_table_statistics_health`  
**Purpose**: Check statistics health - when last updated, sample rates, modifications  
**Use When**: Bad execution plans, parameter sniffing, performance degradation

```sql
-- Replace '<your db name>' with actual database name
DECLARE @db_name VARCHAR(100) = '<your db name>';

SELECT 
    Database_Id,
    Database_Name,
    Object_Name,
    object_id,
    stats_id,
    last_updated,
    rows,
    rows_sampled,
    CAST(100.0 * rows_sampled / NULLIF(rows, 0) AS DECIMAL(5,2)) AS sample_percent,
    steps,
    unfiltered_rows,
    modification_counter,
    CAST(100.0 * modification_counter / NULLIF(rows, 0) AS DECIMAL(5,2)) AS modification_percent,
    persisted_sample_percent
FROM dbo.tbl_dm_db_stats_properties
WHERE Database_Name NOT IN ('msdb', 'master', 'model', 'tempdb')
    AND Database_Name = @db_name
ORDER BY last_updated ASC;  -- Oldest statistics first
```

**Output Interpretation**:
- **last_updated**: When statistics were last refreshed
- **rows**: Total rows in table/index
- **rows_sampled**: How many rows were sampled for statistics
- **sample_percent**: Percentage of rows sampled (100% = full scan)
- **steps**: Number of histogram steps (max 200)
- **modification_counter**: Number of modifications since last update (INSERT/UPDATE/DELETE)
- **modification_percent**: % of rows modified since last update
- **persisted_sample_percent**: Custom sample rate if specified

**Analysis Patterns**:

1. **last_updated > 7 days ago + High modification_percent (> 20%)**:
   - **Status**: STALE statistics
   - **Impact**: Query optimizer has outdated distribution info
   - **Action**: Update statistics immediately
   - **Example**: Table with 1M rows, 250K modifications = 25% stale

2. **sample_percent < 100% on small tables (< 1M rows)**:
   - **Status**: Sampled statistics on small table
   - **Impact**: May not be accurate enough
   - **Action**: Update with FULLSCAN

3. **modification_counter very high (> 100K) but modification_percent low**:
   - **Status**: Large table with many changes but still < 20% threshold
   - **Impact**: May need manual update before auto-update triggers
   - **Action**: Consider proactive statistics update

4. **steps < 200 on large skewed columns**:
   - **Status**: Histogram may not capture distribution well
   - **Impact**: Cardinality estimation errors
   - **Action**: Consider filtered statistics for subsets

**Statistics Update Thresholds**:
- **Auto-update triggers** when modifications exceed:
  - Tables < 500 rows: 500 modifications
  - Tables >= 500 rows: 500 + (20% * rows)
  - Example: 1M row table → auto-update at 200,500 modifications

**Remediation Actions**:

```sql
-- Update statistics for specific table (with FULLSCAN for accuracy)
UPDATE STATISTICS dbo.YourTableName WITH FULLSCAN;

-- Update all statistics in database (use with caution, can be slow)
EXEC sp_updatestats;

-- Update statistics for specific table with custom sample rate
UPDATE STATISTICS dbo.YourTableName WITH SAMPLE 50 PERCENT;

-- Check when auto-update statistics last ran
SELECT 
    OBJECT_NAME(object_id) AS TableName,
    name AS StatName,
    STATS_DATE(object_id, stats_id) AS LastUpdated
FROM sys.stats
WHERE OBJECT_NAME(object_id) = 'YourTableName'
ORDER BY LastUpdated;
```

**Proactive Maintenance**:
```sql
-- Schedule weekly statistics update for critical tables
-- In SQL Agent job or maintenance plan:
UPDATE STATISTICS dbo.CriticalTable1 WITH FULLSCAN;
UPDATE STATISTICS dbo.CriticalTable2 WITH FULLSCAN;
UPDATE STATISTICS dbo.CriticalTable3 WITH FULLSCAN;
```

---

### Query #27a: Find Tables with Stale Statistics Causing Performance Issues
**MCP Tool**: `get_table_statistics_health`  
**Purpose**: Correlate stale statistics with slow queries  
**Use When**: Investigating if statistics are root cause

```sql
DECLARE @db_name VARCHAR(100) = '<your db name>';

-- Find stale statistics
WITH StaleStats AS (
    SELECT 
        Database_Name,
        Object_Name,
        object_id,
        last_updated,
        modification_counter,
        rows,
        CAST(100.0 * modification_counter / NULLIF(rows, 0) AS DECIMAL(5,2)) AS modification_percent
    FROM dbo.tbl_dm_db_stats_properties
    WHERE Database_Name = @db_name
        AND modification_counter > 0
        AND (
            CAST(100.0 * modification_counter / NULLIF(rows, 0) AS DECIMAL(5,2)) > 15
            OR DATEDIFF(DAY, last_updated, GETDATE()) > 7
        )
)
-- Correlate with slow queries on those objects
SELECT 
    ss.Object_Name AS TableWithStaleStats,
    ss.last_updated AS StatsLastUpdated,
    ss.modification_percent,
    COUNT(b.HashID) AS SlowQueryCount,
    SUM(b.Duration)/1000 AS TotalQueryDuration_ms,
    AVG(b.Duration)/1000 AS AvgQueryDuration_ms
FROM StaleStats ss
LEFT JOIN ReadTrace.tblBatches b
    ON b.DatabaseID = DB_ID(ss.Database_Name)  -- Adjust join as needed
GROUP BY 
    ss.Object_Name,
    ss.last_updated,
    ss.modification_percent
ORDER BY TotalQueryDuration_ms DESC;
```

---

### Query #28: Compilations and Recompilations in SQL Server
**MCP Tool**: `get_compilation_stats`  
**Purpose**: Track compilation rates to identify plan cache issues  
**Use When**: High CPU, plan cache churn, or "optimize for ad hoc" assessment

```sql
IF (OBJECT_ID ('CounterDetails') IS NOT NULL)
BEGIN
    SELECT 
        ctr.RecordIndex,
        ctr.CounterDateTime,
        det.ObjectName,
        det.CounterName,
        ctr.CounterValue AS Compilations_Per_Second,
        LAG(ctr.CounterValue) OVER (PARTITION BY det.CounterName ORDER BY ctr.RecordIndex) AS PrevValue,
        ctr.CounterValue - LAG(ctr.CounterValue) OVER (PARTITION BY det.CounterName ORDER BY ctr.RecordIndex) AS Delta
    FROM dbo.CounterData ctr
        JOIN dbo.CounterDetails det
            ON ctr.CounterID = det.CounterID
    WHERE det.CounterName IN ('SQL Compilations/sec', 'SQL Re-Compilations/sec')
        AND det.ObjectName = 'SQLServer:SQL Statistics'
    ORDER BY ctr.RecordIndex, det.CounterName;
END
```

**Output Interpretation**:
- **SQL Compilations/sec**: Rate of query plan compilations
- **SQL Re-Compilations/sec**: Rate of plan recompilations (query already compiled but recompiling)
- **Delta**: Change from previous snapshot

**Analysis Thresholds**:

1. **SQL Compilations/sec > 100 (or > 10% of batch requests/sec)**:
   - **Status**: High compilation rate
   - **Impact**: CPU consumed by compilation instead of execution
   - **Cause**: Ad-hoc queries, dynamic SQL without parameterization
   - **Action**: 
     - Enable "optimize for ad hoc workloads"
     - Parameterize queries (use sp_executesql)
     - Use stored procedures

2. **SQL Re-Compilations/sec > 10**:
   - **Status**: High recompilation rate
   - **Impact**: Plans being invalidated and recompiled
   - **Cause**: 
     - Statistics updates during query execution
     - Schema changes
     - `SET` option changes
     - Explicit `OPTION (RECOMPILE)`
     - Temp table usage patterns
   - **Action**:
     - Review use of `OPTION (RECOMPILE)` (is it necessary?)
     - Check statistics update frequency
     - Review temp table patterns (table variables vs temp tables)

3. **Compilations/sec suddenly spikes**:
   - **Status**: Compilation storm
   - **Cause**: Plan cache flush, server restart, memory pressure
   - **Action**: Investigate what caused cache flush

**Compilation Efficiency Check**:

```sql
-- Check plan cache reuse (requires tbl_CACHEOBJECTS or similar)
IF OBJECT_ID('tbl_CACHEOBJECTS') IS NOT NULL
BEGIN
    SELECT 
        objtype,
        cacheobjtype,
        COUNT(*) AS plan_count,
        SUM(size_in_bytes) / 1024 / 1024 AS cache_size_mb,
        SUM(usecounts) AS total_use_count,
        AVG(usecounts) AS avg_use_count,
        MIN(usecounts) AS min_use_count,
        MAX(usecounts) AS max_use_count
    FROM tbl_CACHEOBJECTS
    GROUP BY objtype, cacheobjtype
    ORDER BY plan_count DESC;
END
```

**Interpretation**:
- **avg_use_count ≈ 1**: Plans used once then discarded (ad-hoc queries, compilation overhead)
- **avg_use_count > 10**: Good plan reuse
- **High plan_count with low avg_use_count**: Plan cache bloat

**Remediation**:

```sql
-- Enable optimize for ad hoc workloads (keeps only compiled plan stub until reused)
sp_configure 'show advanced options', 1;
RECONFIGURE;
sp_configure 'optimize for ad hoc workloads', 1;
RECONFIGURE;

-- After enabling, clear plan cache to see effect (use with caution!)
DBCC FREEPROCCACHE;

-- Check if option is enabled
SELECT name, value_in_use 
FROM sys.configurations 
WHERE name = 'optimize for ad hoc workloads';
```

**Compilation Storm Investigation**:

```sql
-- Find queries being compiled/recompiled most frequently
-- (Requires extended events or query store data)
-- Check XEvent data for sql_statement_recompile events
```

---

## WORKFLOW EXAMPLES

### Scenario 1: "Optimize slow queries with missing indexes"

```
Step 1: Run Query #26 (Missing Indexes)
Step 2: Identify top 5 by improvement_measure
Step 3: For each missing index:
   - Check existing indexes on same table (avoid duplicates)
   - Review equality/inequality/included columns
   - Evaluate if index will actually be used (user_seeks/user_scans)
Step 4: Create indexes in test environment
Step 5: Measure query performance before/after
Step 6: Deploy to production during maintenance window
Step 7: Monitor index usage with sys.dm_db_index_usage_stats
```

---

### Scenario 2: "Bad execution plans, possible statistics issue"

```
Step 1: Run Query #27 (Table Statistics)
Step 2: Sort by last_updated ASC to find oldest statistics
Step 3: Identify tables with:
   - last_updated > 7 days
   - modification_percent > 20%
Step 4: Update statistics:
   UPDATE STATISTICS dbo.TableName WITH FULLSCAN;
Step 5: Clear query plan cache for affected queries (if needed):
   DBCC FREEPROCCACHE;
Step 6: Test query performance
Step 7: Schedule regular statistics updates for these tables
```

---

### Scenario 3: "High CPU, suspected compilation storm"

```
Step 1: Run Query #28 (Compilations/Recompilations)
Step 2: Check SQL Compilations/sec value
   - If > 100: High compilation rate
   - If spiking: Compilation storm
Step 3: Check plan cache efficiency:
   - Run avg_use_count query
   - If avg ≈ 1: Ad-hoc query problem
Step 4: Enable optimize for ad hoc workloads
Step 5: Review application code for dynamic SQL
Step 6: Implement parameterization (sp_executesql or stored procedures)
Step 7: Monitor compilation rate after changes
```

---

### Scenario 4: "Proactive index and statistics maintenance"

```
Monthly Review Process:
Step 1: Run Query #26 (Missing Indexes)
   - Implement top 3-5 high-impact indexes
Step 2: Run Query #27 (Statistics Health)
   - Update statistics for tables with modification_percent > 15%
Step 3: Run Query #28 (Compilation Rates)
   - Check if compilation rate is trending up
   - Review plan cache efficiency
Step 4: Review index usage:
   - Check sys.dm_db_index_usage_stats
   - Drop unused indexes (0 seeks/scans/lookups)
Step 5: Document changes and measure impact
```

---

## INTEGRATION WITH OTHER SCENARIOS

### If Missing Index Recommendations Point to Specific Queries
→ Use [scenario-performance.md](scenario-performance.md) Query #2 to analyze query execution patterns

### If Statistics Issue Correlates with Blocking
→ Use [scenario-blocking.md](scenario-blocking.md) - outdated stats can cause bad plans leading to blocking

### If High Compilation Rate Found
→ Use [scenario-cpu.md](scenario-cpu.md) Query #18 to find expensive compilation patterns

### If Need to See Which Queries Use Indexes
→ Use [scenario-query-deepdive-wait-analysis.md](scenario-query-deepdive-wait-analysis.md) for detailed query analysis

---

## BEST PRACTICES

### Index Creation Guidelines

1. **Key Column Order Matters**:
   - Equality columns first (WHERE col = value)
   - Inequality columns next (WHERE col > value)
   - Most selective columns first

2. **Use INCLUDE for Covering Indexes**:
   - Columns in SELECT but not in WHERE/JOIN
   - Eliminates key lookups

3. **Avoid Over-Indexing**:
   - Each index has maintenance cost (INSERT/UPDATE/DELETE)
   - Maximum ~5-10 indexes per table (guideline, not rule)
   - Consolidate similar indexes

4. **Name Indexes Meaningfully**:
   - `IX_TableName_Column1_Column2_INCLUDES_Column3`
   - Documents purpose and columns

5. **Test Before Production**:
   - Measure query performance improvement
   - Check index creation time on large tables
   - Consider online index creation (Enterprise Edition)

### Statistics Maintenance

1. **Schedule Regular Updates**:
   - Weekly for high-modification tables
   - Monthly for moderate-change tables
   - Use FULLSCAN for critical tables

2. **Monitor Auto-Update**:
   - Auto-update is reactive (waits for threshold)
   - Proactive updates prevent bad plans

3. **Consider Trace Flags**:
   - TF 2371: Dynamic statistics update threshold (recommended for large tables)

### Compilation Optimization

1. **Parameterize Queries**:
   ```sql
   -- Bad: Ad-hoc query
   SELECT * FROM Orders WHERE OrderID = 12345;
   
   -- Good: Parameterized
   EXEC sp_executesql N'SELECT * FROM Orders WHERE OrderID = @OrderID', 
        N'@OrderID INT', @OrderID = 12345;
   ```

2. **Use Stored Procedures**:
   - Compiled once, reused many times
   - Natural parameterization

3. **Enable "Optimize for Ad Hoc Workloads"**:
   - Reduces plan cache bloat from ad-hoc queries

---

## NEXT STEPS BASED ON FINDINGS

### If High-Impact Missing Indexes Found
→ Create indexes, monitor usage, measure performance improvement

### If Stale Statistics Identified
→ Update statistics, consider scheduled maintenance, enable TF 2371

### If High Compilation Rate
→ Enable optimize for ad hoc, review dynamic SQL usage, implement parameterization

### If Need Query Performance Comparison After Index Creation
→ Use [scenario-comparative-analysis.md](scenario-comparative-analysis.md) Query #21
