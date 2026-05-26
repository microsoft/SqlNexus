# Application-Specific Performance Analysis - Complete Diagnostic Workflow

## SYMPTOM
"Specific application is slow" / "Need to analyze performance by application" / "Compare application performance"

---

## DECISION TREE

### Do you know which application is having issues?

#### ✅ **YES (specific application name)**

**Analysis Sequence**:
1. **Find all queries from this application** → Query #12
2. **Get aggregate performance by application** → Query #13
3. **Find slowest queries from this app** → Query #12 with filtering
4. **Compare with other applications** → Query #13 for all apps

---

#### ❌ **NO (need to identify problematic application)**

**Analysis Sequence**:
1. **Get performance by all applications** → Query #13
2. **Identify top resource consumers** → Sort by Duration, CPU, or Reads
3. **Drill into specific application** → Query #12 with identified app name

---

## USE CASES

### Application Troubleshooting
- Application team reports slow performance
- Need to isolate application-specific queries
- Compare application behavior over time

### Multi-Tenant Analysis
- Multiple applications on same SQL instance
- Identify noisy neighbor applications
- Resource allocation decisions

### Application Comparison
- Compare production vs staging application
- Identify regression after code deployment
- Baseline application performance

---

## EMBEDDED QUERIES

### Query #12: Find Queries by Application Name
**MCP Tool**: `get_queries_by_application`  
**Purpose**: Identify all queries executed by a specific application  
**Use When**: Need to see what a specific app is doing

```sql
-- Replace 'YourAppName' with actual application name
DECLARE @AppName NVARCHAR(128) = 'YourAppName';

SELECT 
    b.Session,
    b.StartTime,
    b.EndTime,
    b.Duration/1000 AS Duration_ms,
    b.CPU AS CPU_ms,
    b.Reads,
    b.Writes,
    ub.NormText AS Query,
    b.HashID
FROM ReadTrace.tblBatches b 
JOIN ReadTrace.tblConnections c
    ON b.ConnSeq = c.ConnSeq 
    AND b.session = c.session 
JOIN ReadTrace.tblUniqueBatches ub 
    ON b.HashID = ub.HashID
WHERE c.ApplicationName = @AppName
ORDER BY b.Duration DESC;
```

**Output Interpretation**:
- **Duration_ms**: How long each query took
- **CPU_ms**: CPU time consumed
- **Reads/Writes**: I/O operations
- **NormText**: Normalized query text (parameters removed)
- **HashID**: Use to drill deeper with other queries

**Analysis Tips**:
1. **High variance in Duration**: Possible parameter sniffing or caching issues
2. **Specific slow queries**: Isolate HashID and use Query #2 from scenario-performance.md
3. **Pattern analysis**: Are there common table/index access patterns?

**Common Application Names**:
- `.Net SqlClient Data Provider`
- `Microsoft SQL Server Management Studio`
- Custom application names from connection strings

---

### Query #12a: Find Queries by Application with Aggregation
**MCP Tool**: `get_queries_by_application`  
**Purpose**: Get aggregate stats per query for specific application  
**Use When**: Want to see query patterns, not individual executions

```sql
-- Replace 'YourAppName' with actual application name
DECLARE @AppName NVARCHAR(128) = 'YourAppName';

SELECT 
    COUNT(*) AS Executions,
    SUM(b.Duration)/1000 AS Total_Duration_ms,
    AVG(b.Duration)/1000 AS Avg_Duration_ms,
    MIN(b.Duration)/1000 AS Min_Duration_ms,
    MAX(b.Duration)/1000 AS Max_Duration_ms,
    SUM(b.CPU) AS Total_CPU_ms,
    AVG(b.CPU) AS Avg_CPU_ms,
    SUM(b.Reads) AS Total_Reads,
    AVG(b.Reads) AS Avg_Reads,
    SUBSTRING(ub.NormText, 1, 200) AS Query,
    b.HashID
FROM ReadTrace.tblBatches b 
JOIN ReadTrace.tblConnections c
    ON b.ConnSeq = c.ConnSeq 
    AND b.session = c.session 
JOIN ReadTrace.tblUniqueBatches ub 
    ON b.HashID = ub.HashID
WHERE c.ApplicationName = @AppName
GROUP BY ub.NormText, b.HashID
ORDER BY Total_Duration_ms DESC;
```

**Output Interpretation**:
- **Executions**: How many times this query ran
- **Total_Duration_ms**: Cumulative impact (high = performance driver)
- **Avg_Duration_ms**: Typical execution time
- **Max - Min variance**: High variance suggests instability (parameter sniffing, caching)

**Action Items**:
1. **High Total_Duration + High Executions**: Frequently-run query → Optimize for maximum impact
2. **High Avg_Duration + Low Executions**: Expensive one-off query → May be acceptable
3. **High variance (Max >> Avg)**: Parameter sniffing or plan cache issues → Consider `OPTION (RECOMPILE)` or plan guides

---

### Query #13: Performance by Application Name
**MCP Tool**: `get_performance_by_application`  
**Purpose**: Aggregate performance metrics across all applications  
**Use When**: Comparing application performance or identifying noisy neighbors

```sql
SELECT 
    SUM(TotalDuration) AS Duration_ms, 
    SUM(TotalCPU) AS CPU_ms, 
    SUM(TotalReads) AS Reads,
    SUM(TotalWrites) AS Writes,
    COUNT(DISTINCT HashID) AS Unique_Queries,
    AppName,
    CAST(100.0 * SUM(TotalDuration) / SUM(SUM(TotalDuration)) OVER() AS DECIMAL(5,2)) AS Pct_Total_Duration,
    CAST(100.0 * SUM(TotalCPU) / SUM(SUM(TotalCPU)) OVER() AS DECIMAL(5,2)) AS Pct_Total_CPU,
    CAST(100.0 * SUM(TotalReads) / SUM(SUM(TotalReads)) OVER() AS DECIMAL(5,2)) AS Pct_Total_Reads
FROM ReadTrace.tblBatchPartialAggs b 
INNER JOIN ReadTrace.tblUniqueAppNames a 
    ON a.iID = b.AppNameID
GROUP BY AppName
ORDER BY Duration_ms DESC;
```

**Output Interpretation**:
- **Duration_ms**: Total time consumed by this application
- **CPU_ms**: Total CPU consumed
- **Reads/Writes**: Total I/O operations
- **Unique_Queries**: Query diversity (high = ad-hoc queries?)
- **Pct_Total_***: Percentage of total server resources consumed

**Analysis Patterns**:

1. **Resource Hog Applications**:
   - Pct_Total_Duration > 50% = One app dominating
   - Action: Optimize this app's queries or scale resources

2. **Noisy Neighbors**:
   - Multiple apps competing, none > 30%
   - Action: Review workload isolation, consider resource governor

3. **Ad-Hoc Query Applications**:
   - High Unique_Queries / Duration ratio
   - Action: Parameterize queries, enable "optimize for ad hoc workloads"

4. **I/O-Heavy Applications**:
   - Pct_Total_Reads >> Pct_Total_CPU
   - Action: Add indexes, optimize queries for I/O reduction

5. **CPU-Heavy Applications**:
   - Pct_Total_CPU >> Pct_Total_Reads
   - Action: Review query complexity, check for expensive calculations

---

### Query #13a: Application Performance Comparison Over Time
**MCP Tool**: `query_nexus_database`  
**Purpose**: Track application performance trends  
**Use When**: Monitoring application behavior changes

```sql
-- Get performance metrics with time buckets
SELECT 
    DATEADD(MINUTE, DATEDIFF(MINUTE, 0, b.StartTime) / 5 * 5, 0) AS TimeBucket_5min,
    c.ApplicationName,
    COUNT(*) AS QueryCount,
    SUM(b.Duration)/1000 AS Total_Duration_ms,
    AVG(b.Duration)/1000 AS Avg_Duration_ms,
    SUM(b.CPU) AS Total_CPU_ms,
    SUM(b.Reads) AS Total_Reads
FROM ReadTrace.tblBatches b 
JOIN ReadTrace.tblConnections c
    ON b.ConnSeq = c.ConnSeq 
    AND b.session = c.session 
GROUP BY 
    DATEADD(MINUTE, DATEDIFF(MINUTE, 0, b.StartTime) / 5 * 5, 0),
    c.ApplicationName
ORDER BY TimeBucket_5min, Total_Duration_ms DESC;
```

**Output Interpretation**:
- **TimeBucket_5min**: 5-minute time windows
- Shows performance trends over time
- Identify peak load times
- Spot unusual spikes or degradation

**Visualization**: Plot Duration/CPU over time by application to see patterns

---

## WORKFLOW EXAMPLES

### Scenario 1: "ReportingApp is slow"

```
Step 1: Run Query #13 to confirm ReportingApp is a top consumer
Step 2: Run Query #12a with ApplicationName = 'ReportingApp' to see query breakdown
Step 3: Identify top 3 slowest queries by Total_Duration_ms
Step 4: For each HashID:
   - Use Query #2 from scenario-performance.md to see execution variance
   - Use Query #5 from scenario-performance.md to see wait types
   - Review execution plan
Step 5: Recommendations:
   - Add missing indexes
   - Optimize query logic
   - Consider caching results
   - Schedule reports during off-peak hours
```

---

### Scenario 2: "Need to compare all applications"

```
Step 1: Run Query #13 to get overall application breakdown
Step 2: Identify top 3 resource consumers
Step 3: For each top application:
   - Run Query #12a to see query patterns
   - Check Unique_Queries count (high = ad-hoc workload)
   - Check Pct_Total_CPU vs Pct_Total_Reads (I/O vs CPU bound)
Step 4: Recommendations by pattern:
   - High ad-hoc queries → Parameterization
   - I/O-heavy → Index optimization
   - CPU-heavy → Query simplification
   - Even resource distribution → All good!
```

---

### Scenario 3: "Which app is causing blocking?"

```
Step 1: Run Query #9 from scenario-blocking.md to find head blockers
Step 2: Get session_id of blocker
Step 3: Find application:
   SELECT c.ApplicationName, c.HostName, c.LoginName
   FROM ReadTrace.tblConnections c
   JOIN tbl_HEADBLOCKERSUMMARY h
       ON c.session = h.head_blocker_session_id
   WHERE h.runtime = '<specific_time>'
Step 4: Run Query #12 for that ApplicationName to see all its queries
Step 5: Identify blocker query pattern and optimize
```

---

## INTEGRATION WITH OTHER SCENARIOS

### If Application Has High CPU
→ Use [scenario-cpu.md](scenario-cpu.md) to investigate CPU-intensive queries from this app

### If Application Has Blocking
→ Use [scenario-blocking.md](scenario-blocking.md) to analyze blocking patterns

### If Application Has I/O Issues
→ Use [scenario-io.md](scenario-io.md) to check file I/O and query reads/writes

### If Specific Query Needs Deep Dive
→ Use [scenario-query-deepdive-wait-analysis.md](scenario-query-deepdive-wait-analysis.md) for detailed wait correlation

---

## REMEDIATION STRATEGIES

### Application-Level Fixes

1. **Connection Pooling**:
   - Reduce connection churn
   - Set appropriate min/max pool size
   - Monitor connection reuse

2. **Query Parameterization**:
   - Use sp_executesql instead of dynamic SQL
   - Enable "optimize for ad hoc workloads"
   - Use stored procedures

3. **Result Set Management**:
   - Fetch only needed columns (SELECT specific columns, not SELECT *)
   - Use paging (TOP/OFFSET-FETCH)
   - Limit result set size

4. **Transaction Management**:
   - Keep transactions short
   - Avoid holding locks during user interaction
   - Use appropriate isolation level

5. **Caching**:
   - Cache frequently-accessed, rarely-changing data
   - Implement application-level caching
   - Use SQL Server query result caching features

### Resource Governor (Multi-Tenant Scenarios)

```sql
-- Example: Create resource pool for specific application
CREATE RESOURCE POOL AppPool
WITH (
    MIN_CPU_PERCENT = 10,
    MAX_CPU_PERCENT = 40,
    MIN_MEMORY_PERCENT = 10,
    MAX_MEMORY_PERCENT = 30
);

CREATE WORKLOAD GROUP AppGroup
USING AppPool;

-- Create classifier function to route application to pool
CREATE FUNCTION dbo.ClassifyApplicationWorkload()
RETURNS SYSNAME
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @WorkloadGroup SYSNAME;
    
    IF (APP_NAME() LIKE '%ReportingApp%')
        SET @WorkloadGroup = 'AppGroup';
    ELSE
        SET @WorkloadGroup = 'default';
    
    RETURN @WorkloadGroup;
END;
GO

-- Enable Resource Governor with classifier
ALTER RESOURCE GOVERNOR WITH (CLASSIFIER_FUNCTION = dbo.ClassifyApplicationWorkload);
ALTER RESOURCE GOVERNOR RECONFIGURE;
```

---

## NEXT STEPS BASED ON FINDINGS

### If Application Has Specific Slow Query
→ Use HashID with Query #2 from [scenario-performance.md](scenario-performance.md)

### If Application Executes Many Ad-Hoc Queries
→ Review code for dynamic SQL, recommend parameterization

### If Application Has High I/O
→ Use [scenario-io.md](scenario-io.md) and Query #26 (Top Queries by Reads)

### If Application Has High CPU
→ Use [scenario-cpu.md](scenario-cpu.md) and Query #18 (Top Queries by CPU)

### If Need to Compare Before/After Application Changes
→ Use [scenario-comparative-analysis.md](scenario-comparative-analysis.md) Query #21
