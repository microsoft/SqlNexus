# Comparative Performance Analysis - Complete Diagnostic Workflow

## SYMPTOM
"Performance was fine yesterday, now it's slow" / "Compare two time periods" / "What changed between slow and fast collections" / "Regression analysis"

---

## DECISION TREE

### What type of comparison?

#### **Time Period Comparison** (Query #21)
- Slow collection vs fast collection
- Before upgrade vs after upgrade
- Peak hours vs off-peak hours
- Production vs test environment

#### **Root Cause Categories**
After identifying differences:
- **Query performance regression** → scenario-performance.md
- **New blocking introduced** → scenario-blocking.md
- **CPU spike** → scenario-cpu.md
- **I/O degradation** → scenario-io.md
- **Statistics/plan changes** → scenario-index-optimization.md

---

## USE CASES

### Performance Regression Analysis
- "Yesterday the app was fast, today it's slow - what changed?"
- "After SQL Server upgrade, some queries are slower"
- "Production is slow but test is fast with same data"

### Before/After Validation
- "Did the new index help?"
- "After statistics update, did performance improve?"
- "Is the query optimization effective?"

### Baseline Comparison
- "How does current performance compare to last week's baseline?"
- "Is this slowdown consistent with historical patterns?"

---

## EMBEDDED QUERIES

### Query #21: Compare Two Collections (Slow vs Fast Analysis)
**MCP Tool**: `query_nexus_database`  
**Purpose**: Side-by-side comparison of two data collections to identify what changed  
**Use When**: Performance regression, before/after analysis, environment comparison

```sql
-- Replace database names with your actual PerfStats database names
-- Database1: Slow/problematic collection
-- Database2: Fast/baseline collection

DECLARE @DB1 VARCHAR(128) = '<Database name for 1st collection>';  -- Example: 'PerfStats_Slow_2024_01_15'
DECLARE @DB2 VARCHAR(128) = '<Database name for 2nd collection>';  -- Example: 'PerfStats_Fast_2024_01_14'

-- Dynamic SQL to query across two databases
DECLARE @SQL NVARCHAR(MAX);
SET @SQL = '
WITH Batch1 AS (
    SELECT 
        ub.NormText AS QueryText,
        COUNT(*) AS Execution_Count,
        AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
        MAX(b.Duration) / 1000.0 AS Max_Duration_ms,
        SUM(b.Duration) / 1000.0 AS Total_Duration_ms,
        AVG(b.CPU) AS Avg_CPU_ms,
        SUM(b.CPU) AS Total_CPU_ms,
        AVG(b.Reads) AS Avg_Reads,
        SUM(b.Reads) AS Total_Reads,
        AVG(b.Writes) AS Avg_Writes,
        b.HashID
    FROM [' + @DB1 + '].ReadTrace.tblBatches b
    JOIN [' + @DB1 + '].ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
    GROUP BY ub.NormText, b.HashID
),
Batch2 AS (
    SELECT 
        ub.NormText AS QueryText,
        COUNT(*) AS Execution_Count,
        AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
        MAX(b.Duration) / 1000.0 AS Max_Duration_ms,
        SUM(b.Duration) / 1000.0 AS Total_Duration_ms,
        AVG(b.CPU) AS Avg_CPU_ms,
        SUM(b.CPU) AS Total_CPU_ms,
        AVG(b.Reads) AS Avg_Reads,
        SUM(b.Reads) AS Total_Reads,
        AVG(b.Writes) AS Avg_Writes,
        b.HashID
    FROM [' + @DB2 + '].ReadTrace.tblBatches b
    JOIN [' + @DB2 + '].ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
    GROUP BY ub.NormText, b.HashID
)
SELECT 
    COALESCE(b1.QueryText, b2.QueryText) AS QueryText,
    COALESCE(b1.HashID, b2.HashID) AS HashID,
    
    -- Collection 1 (Slow) Metrics
    ISNULL(b1.Execution_Count, 0) AS Slow_Executions,
    ISNULL(b1.Avg_Duration_ms, 0) AS Slow_Avg_Duration_ms,
    ISNULL(b1.Max_Duration_ms, 0) AS Slow_Max_Duration_ms,
    ISNULL(b1.Total_Duration_ms, 0) AS Slow_Total_Duration_ms,
    ISNULL(b1.Avg_CPU_ms, 0) AS Slow_Avg_CPU_ms,
    ISNULL(b1.Avg_Reads, 0) AS Slow_Avg_Reads,
    
    -- Collection 2 (Fast) Metrics
    ISNULL(b2.Execution_Count, 0) AS Fast_Executions,
    ISNULL(b2.Avg_Duration_ms, 0) AS Fast_Avg_Duration_ms,
    ISNULL(b2.Max_Duration_ms, 0) AS Fast_Max_Duration_ms,
    ISNULL(b2.Total_Duration_ms, 0) AS Fast_Total_Duration_ms,
    ISNULL(b2.Avg_CPU_ms, 0) AS Fast_Avg_CPU_ms,
    ISNULL(b2.Avg_Reads, 0) AS Fast_Avg_Reads,
    
    -- Delta Analysis
    ISNULL(b1.Avg_Duration_ms, 0) - ISNULL(b2.Avg_Duration_ms, 0) AS Delta_Avg_Duration_ms,
    CASE 
        WHEN ISNULL(b2.Avg_Duration_ms, 0) = 0 THEN 999
        ELSE CAST(100.0 * (ISNULL(b1.Avg_Duration_ms, 0) - ISNULL(b2.Avg_Duration_ms, 0)) / ISNULL(b2.Avg_Duration_ms, 1) AS DECIMAL(10, 2))
    END AS Pct_Change_Duration,
    
    ISNULL(b1.Avg_CPU_ms, 0) - ISNULL(b2.Avg_CPU_ms, 0) AS Delta_Avg_CPU_ms,
    ISNULL(b1.Avg_Reads, 0) - ISNULL(b2.Avg_Reads, 0) AS Delta_Avg_Reads,
    
    -- Regression Indicator
    CASE 
        WHEN ISNULL(b1.Avg_Duration_ms, 0) > ISNULL(b2.Avg_Duration_ms, 0) * 1.5 THEN ''REGRESSION''
        WHEN ISNULL(b1.Avg_Duration_ms, 0) < ISNULL(b2.Avg_Duration_ms, 0) * 0.7 THEN ''IMPROVEMENT''
        ELSE ''SIMILAR''
    END AS Performance_Status,
    
    -- Only in which collection?
    CASE 
        WHEN b1.HashID IS NOT NULL AND b2.HashID IS NULL THEN ''Only in Slow''
        WHEN b1.HashID IS NULL AND b2.HashID IS NOT NULL THEN ''Only in Fast''
        ELSE ''In Both''
    END AS Presence
    
FROM Batch1 b1
FULL OUTER JOIN Batch2 b2 ON b1.HashID = b2.HashID
WHERE 
    -- Focus on queries that exist in slow collection
    b1.HashID IS NOT NULL
    -- Filter out very fast queries (< 10ms average)
    AND ISNULL(b1.Avg_Duration_ms, 0) > 10
ORDER BY 
    -- Sort by worst regressions first
    Delta_Avg_Duration_ms DESC;
';

EXEC sp_executesql @SQL;
```

**Output Interpretation**:
- **QueryText**: Normalized query text
- **HashID**: Unique identifier for this query pattern
- **Slow_*** columns: Metrics from first (problematic) collection
- **Fast_*** columns: Metrics from second (baseline) collection
- **Delta_*** columns: Difference (Slow - Fast)
- **Pct_Change_Duration**: Percentage change in duration ((Slow-Fast)/Fast * 100)
- **Performance_Status**: 
  - **REGRESSION**: Slow duration is >50% worse than fast
  - **IMPROVEMENT**: Slow duration is >30% better than fast (misnomer if "slow" is actually improved)
  - **SIMILAR**: Performance within ±30%
- **Presence**: Whether query appears in both collections or only one

**Analysis Patterns**:

### 1. **REGRESSION with High Delta_Avg_Duration_ms**
```
Example Output:
QueryText: SELECT * FROM Orders WHERE CustomerId = @p1
Slow_Avg_Duration_ms: 2500
Fast_Avg_Duration_ms: 150
Delta_Avg_Duration_ms: 2350
Pct_Change_Duration: 1566.67%
Performance_Status: REGRESSION

Analysis:
- Query is 15x slower in slow collection
- Likely root causes:
  * Statistics out of date (bad plan chosen)
  * Missing index
  * Blocking introduced
  * Parameter sniffing issue
  * Data volume increased significantly

Actions:
1. Compare execution plans (if available in plan cache)
2. Check statistics: Query #27 in scenario-index-optimization.md
3. Check for blocking: scenario-blocking.md
4. Check wait types for this query: Query #6 in scenario-query-deepdive-wait-analysis.md
```

### 2. **High Delta_Avg_Reads (I/O Increase)**
```
Example Output:
QueryText: SELECT TOP 1000 * FROM OrderDetails
Slow_Avg_Reads: 50000
Fast_Avg_Reads: 5000
Delta_Avg_Reads: 45000

Analysis:
- 10x more reads in slow collection
- Plan changed from index seek to scan
- Possible causes:
  * Index dropped or disabled
  * Statistics outdated
  * Different parameter causing different plan

Actions:
1. Check if index exists in both environments
2. Compare execution plans
3. Review index usage: scenario-index-optimization.md Query #26
```

### 3. **High Delta_Avg_CPU_ms (CPU Increase)**
```
Example Output:
QueryText: UPDATE Inventory SET Quantity = Quantity - @p1
Slow_Avg_CPU_ms: 1200
Fast_Avg_CPU_ms: 50
Delta_Avg_CPU_ms: 1150

Analysis:
- 24x more CPU in slow collection
- Likely causes:
  * Excessive recompilations
  * Plan changed to inefficient plan
  * Implicit conversions introduced

Actions:
1. Check compilation rate: Query #28 in scenario-index-optimization.md
2. Check for implicit conversions in plan
3. Review query for data type mismatches
```

### 4. **"Only in Slow" Queries (New Queries)**
```
Example Output:
QueryText: SELECT * FROM AuditLog WHERE EventDate > @p1
Presence: Only in Slow
Slow_Avg_Duration_ms: 5000
Slow_Total_Duration_ms: 500000

Analysis:
- Query didn't exist in fast/baseline collection
- New query introduced that is expensive
- May be new feature causing slowdown

Actions:
1. Review if this query is expected (new feature?)
2. Optimize this new query
3. Consider if this query should be running at all
```

### 5. **"Only in Fast" Queries (Query Eliminated)**
```
Example Output:
QueryText: SELECT * FROM Cache_Table
Presence: Only in Fast

Analysis:
- Query ran in fast collection but not in slow
- May indicate:
  * Query no longer being called (application change)
  * Process skipped due to error
  * Different workload pattern

Actions:
1. Verify if query should be running
2. Check application logs for errors
```

---

## WORKFLOW EXAMPLES

### Scenario 1: "App was fast yesterday, slow today - what changed?"

```
Step 1: Identify collection databases
   - Find PerfStats database for "slow" period (today)
   - Find PerfStats database for "fast" period (yesterday)
   
Step 2: Run Query #21 with database names

Step 3: Focus on top 10 queries by Delta_Avg_Duration_ms (worst regressions)

Step 4: For each regressed query:
   a. Note Pct_Change_Duration (how much worse)
   b. Check Delta_Avg_Reads (I/O change?)
   c. Check Delta_Avg_CPU_ms (CPU change?)
   
Step 5: Categorize root causes:
   - High Delta_Reads → I/O issue → scenario-io.md
   - High Delta_CPU → CPU issue → scenario-cpu.md
   - Check for blocking → scenario-blocking.md Query #9
   - Check statistics → scenario-index-optimization.md Query #27

Step 6: Address top 3-5 regressed queries for maximum impact
```

---

### Scenario 2: "Did the index we added help?"

```
Step 1: Run Query #21
   - Database1: Collection AFTER index creation
   - Database2: Collection BEFORE index creation

Step 2: Find queries that should benefit from new index
   - Look for Performance_Status = 'IMPROVEMENT'
   - Check Delta_Avg_Duration_ms < 0 (faster now)
   - Check Delta_Avg_Reads < 0 (fewer reads now)

Step 3: Measure impact:
   - Calculate total time saved: Sum of Delta_Total_Duration_ms for improved queries
   - Verify reads decreased

Step 4: Check if index is actually being used:
   - Query sys.dm_db_index_usage_stats for new index
   - If not used, investigate why (statistics? plan cache?)

Result Interpretation:
- IMPROVEMENT status + Lower reads = Index working ✓
- SIMILAR status + Same reads = Index not used ✗
```

---

### Scenario 3: "Production slow, test fast - why?"

```
Step 1: Collect PerfStats from both environments
   - Production (slow)
   - Test (fast)

Step 2: Run Query #21 comparing both

Step 3: Look for environmental differences:
   a. Queries with "Only in Production":
      - Production may have workload not in test
      - Evaluate if test data/workload is representative
      
   b. High Pct_Change_Duration for same queries:
      - Check execution plans (may differ due to statistics)
      - Check data volumes (production likely larger)
      - Check server resources (CPU, memory, I/O)
      
   c. Same queries, different resource usage:
      - Delta_Avg_Reads: Different plans chosen
      - Delta_Avg_CPU_ms: Different plan or contention

Step 4: Common production-specific issues:
   - Blocking (rare in test): scenario-blocking.md
   - Outdated statistics: scenario-index-optimization.md Query #27
   - Resource contention: scenario-cpu.md, scenario-io.md
   - Data volume: Large production data causing plan changes
```

---

### Scenario 4: "After SQL upgrade, some queries are slower"

```
Step 1: Run Query #21
   - Database1: Post-upgrade collection
   - Database2: Pre-upgrade collection

Step 2: Identify regressions (Performance_Status = 'REGRESSION')

Step 3: Common post-upgrade issues:
   a. Cardinality Estimator change (SQL 2012+ → SQL 2014+):
      - New CE may choose different plans
      - Action: Test with LEGACY CE (TF 9481) or query hint
      
   b. Statistics auto-update changes:
      - Update statistics with FULLSCAN
      - scenario-index-optimization.md Query #27
      
   c. Plan cache cleared during upgrade:
      - Initial compilations may be slow
      - Wait 24-48 hours for cache to warm up

Step 4: For each regressed query:
   - Capture post-upgrade plan
   - Compare with pre-upgrade plan (if available)
   - Identify plan differences

Step 5: Mitigation strategies:
   - Force old plan with query hint (temporary)
   - Update statistics
   - Add missing indexes
   - Consider compatibility level change (if applicable)
```

---

### Scenario 5: "Peak hours slow, off-peak fast - expected or fixable?"

```
Step 1: Run Query #21
   - Database1: Peak hours collection
   - Database2: Off-peak collection

Step 2: Analyze patterns:
   a. Higher execution counts during peak:
      - Expected (more users)
      - Check if duration per query is similar
      
   b. Higher duration per query during peak:
      - Indicates contention/resource exhaustion
      - Not just volume, but resource issue
      
   c. Check Delta_Avg_CPU_ms and Delta_Avg_Reads:
      - Similar values = Just volume increase (acceptable)
      - Higher values = Resource contention (investigate)

Step 3: If resource contention during peak:
   - Check blocking: scenario-blocking.md
   - Check CPU pressure: scenario-cpu.md Query #17
   - Check I/O pressure: scenario-io.md Query #16
   - Check wait types: scenario-query-deepdive-wait-analysis.md Query #8

Step 4: Capacity planning:
   - If only volume increase, may need scale-out
   - If contention, optimize queries/indexes
```

---

## INTEGRATION WITH OTHER SCENARIOS

### If Regression is I/O-Related (High Delta_Avg_Reads)
→ Use [scenario-io.md](scenario-io.md) to investigate file-level I/O and storage performance

### If Regression is CPU-Related (High Delta_Avg_CPU_ms)
→ Use [scenario-cpu.md](scenario-cpu.md) to check CPU utilization and top CPU queries

### If Blocking Suspected (High Delta_Avg_Duration but low Delta_CPU)
→ Use [scenario-blocking.md](scenario-blocking.md) to identify blockers

### If Statistics Issue Suspected
→ Use [scenario-index-optimization.md](scenario-index-optimization.md) Query #27 to check statistics health

### If Need Wait Analysis for Regressed Queries
→ Use [scenario-query-deepdive-wait-analysis.md](scenario-query-deepdive-wait-analysis.md) Query #6 to correlate waits

---

## ADVANCED TECHNIQUES

### Compare Specific Time Windows Within Same Collection

```sql
-- Useful if you have one long collection covering both slow and fast periods
-- Requires StartTime column in tblBatches

DECLARE @SlowStart DATETIME = '2024-01-15 14:00:00';  -- Slow period start
DECLARE @SlowEnd DATETIME = '2024-01-15 15:00:00';    -- Slow period end
DECLARE @FastStart DATETIME = '2024-01-15 10:00:00';  -- Fast period start
DECLARE @FastEnd DATETIME = '2024-01-15 11:00:00';    -- Fast period end

WITH SlowBatches AS (
    SELECT 
        b.HashID,
        ub.NormText,
        AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
        COUNT(*) AS Execution_Count,
        AVG(b.CPU) AS Avg_CPU_ms,
        AVG(b.Reads) AS Avg_Reads
    FROM ReadTrace.tblBatches b
    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
    WHERE b.StartTime BETWEEN @SlowStart AND @SlowEnd
    GROUP BY b.HashID, ub.NormText
),
FastBatches AS (
    SELECT 
        b.HashID,
        AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
        COUNT(*) AS Execution_Count,
        AVG(b.CPU) AS Avg_CPU_ms,
        AVG(b.Reads) AS Avg_Reads
    FROM ReadTrace.tblBatches b
    WHERE b.StartTime BETWEEN @FastStart AND @FastEnd
    GROUP BY b.HashID
)
SELECT 
    sb.NormText AS QueryText,
    sb.Avg_Duration_ms AS Slow_Period_Avg_ms,
    fb.Avg_Duration_ms AS Fast_Period_Avg_ms,
    sb.Avg_Duration_ms - fb.Avg_Duration_ms AS Delta_Duration_ms,
    sb.Avg_Reads AS Slow_Reads,
    fb.Avg_Reads AS Fast_Reads,
    CASE 
        WHEN fb.Avg_Duration_ms = 0 THEN 999
        ELSE CAST(100.0 * (sb.Avg_Duration_ms - fb.Avg_Duration_ms) / fb.Avg_Duration_ms AS DECIMAL(10,2))
    END AS Pct_Change
FROM SlowBatches sb
JOIN FastBatches fb ON sb.HashID = fb.HashID
WHERE sb.Avg_Duration_ms > 10  -- Filter out fast queries
ORDER BY Delta_Duration_ms DESC;
```

---

### Find Queries That Disappeared Between Collections

```sql
-- Useful to identify queries that stopped running (may indicate app issue)

DECLARE @DB1 VARCHAR(128) = '<Fast collection>';
DECLARE @DB2 VARCHAR(128) = '<Slow collection>';

DECLARE @SQL NVARCHAR(MAX) = '
SELECT 
    ub.NormText AS QueryText,
    COUNT(*) AS Execution_Count_In_Fast,
    AVG(b.Duration) / 1000.0 AS Avg_Duration_ms,
    b.HashID
FROM [' + @DB1 + '].ReadTrace.tblBatches b
JOIN [' + @DB1 + '].ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
WHERE b.HashID NOT IN (
    SELECT DISTINCT HashID 
    FROM [' + @DB2 + '].ReadTrace.tblBatches
)
GROUP BY ub.NormText, b.HashID
ORDER BY Execution_Count_In_Fast DESC;
';

EXEC sp_executesql @SQL;
```

---

### Top Contributors to Total Duration Change

```sql
-- Find queries contributing most to overall slowdown
-- Considers both duration per query AND execution frequency

-- (Modify Query #21 to add this calculated column)
Total_Impact_Change = (Slow_Total_Duration_ms - Fast_Total_Duration_ms)

-- Then order by Total_Impact_Change DESC
-- This finds queries that, due to duration increase AND/OR frequency increase,
-- contribute most to overall performance degradation
```

---

## INTERPRETATION GUIDE

### Good News Indicators ✓
- Most queries have Performance_Status = 'SIMILAR'
- Delta_Avg_Duration_ms close to 0 for most queries
- Pct_Change_Duration within ±30%
- Only a few queries regressed (focused optimization)

### Warning Signs ⚠️
- Many queries with Performance_Status = 'REGRESSION'
- Large Pct_Change_Duration (>100%) for common queries
- High Delta_Avg_Reads across many queries (systematic I/O issue)
- Many "Only in Slow" queries (workload changed)

### Critical Issues 🚨
- Top query has Pct_Change_Duration > 1000% (10x slower)
- Majority of queries regressed (systemic issue: statistics? resource exhaustion?)
- Total_Duration_ms for slow collection >> fast collection
- Critical queries (login, homepage) have REGRESSION status

---

## NEXT STEPS BASED ON FINDINGS

### If Individual Query Regressions Found
→ Use scenario-performance.md and scenario-query-deepdive-wait-analysis.md to optimize

### If Systematic I/O Degradation (All queries have high Delta_Reads)
→ Use scenario-io.md to check storage health

### If Systematic CPU Increase (All queries have high Delta_CPU)
→ Use scenario-cpu.md to check CPU pressure

### If Statistics Suspected (Many plan changes)
→ Use scenario-index-optimization.md Query #27 to update statistics

### If New Queries Causing Issues ("Only in Slow")
→ Review application changes, optimize new queries
