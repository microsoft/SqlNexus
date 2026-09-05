# Analysis Summary Queries

## Best Practices and Analysis Summary
Returns the auto-detected rules and findings from SqlNexus import analysis.
Status values: 0 = Passed, 1 = Warning (issue found).
Requires: tbl_AnalysisSummary

```sql
SELECT [FriendlyName], [Description], [Status],
       CASE WHEN [Status] = 0 THEN 'Passed' ELSE 'Warning' END AS StatusText,
       [ExternalUrl]
FROM dbo.tbl_AnalysisSummary
ORDER BY [Status] DESC, [FriendlyName]
```

## Warnings Only
```sql
SELECT [FriendlyName], [Description], [ExternalUrl]
FROM dbo.tbl_AnalysisSummary
WHERE [Status] >= 1
ORDER BY [FriendlyName]
```

## Missing Indexes
```sql
SELECT TOP 20 * FROM tbl_MissingIndexes ORDER BY improvement_measure DESC
```

## Top Expensive Queries (from ReadTrace data)
```sql
SELECT TOP 20
    SUM(b.Duration)/1000 AS total_duration_ms,
    SUM(b.CPU) AS total_cpu_ms,
    SUM(b.Reads) AS total_reads,
    COUNT(*) AS execution_count,
    SUBSTRING(ub.NormText, 1, 200) AS query_text
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
GROUP BY ub.NormText, b.HashID
ORDER BY total_duration_ms DESC
```

## Performance Counters Trend
```sql
SELECT TOP 100 cd.CounterDateTime, cdet.ObjectName, cdet.CounterName,
       cdet.InstanceName, cd.CounterValue
FROM CounterData cd
JOIN CounterDetails cdet ON cd.CounterID = cdet.CounterID
WHERE cdet.CounterName LIKE '%Batch Requests%'
   OR cdet.CounterName LIKE '%SQL Compilations%'
   OR cdet.CounterName LIKE '%Page life%'
ORDER BY cd.CounterDateTime
```

## Memory Grants
```sql
SELECT TOP 20 session_id, requested_memory_kb, granted_memory_kb,
       required_memory_kb, used_memory_kb, max_used_memory_kb,
       query_cost, is_small, ideal_memory_kb
FROM tbl_dm_exec_query_memory_grants
ORDER BY requested_memory_kb DESC
```

## Server Information
```sql
SELECT [Value] FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
WHERE script_name LIKE 'SQL Server Perf Stats Script' AND [Name] = 'SQL Server Name'
```

```sql
SELECT [Value] FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
WHERE script_name LIKE 'SQL Server Perf Stats Script' AND [Name] = 'SQL Version (SP)'
```
