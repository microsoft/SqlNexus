# Bottleneck Analysis Queries

## CPU Utilization Over Time
Combines SQL Server process CPU with overall OS CPU from perfmon counters.
Requires: tbl_ServerProperties, CounterData, CounterDetails

```sql
SET ANSI_NULLS OFF;
IF (OBJECT_ID('dbo.tbl_ServerProperties') IS NOT NULL
    AND OBJECT_ID('dbo.CounterData') IS NOT NULL)
BEGIN
    DECLARE @process_id INT = 0, @cpu_count INT,
            @inst_name VARCHAR(64), @inst_index INT;

    SELECT @process_id = sp.PropertyValue
    FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'ProcessID';

    SELECT @cpu_count = CASE WHEN sp.PropertyValue = 0 THEN 1 ELSE sp.PropertyValue END
    FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'cpu_count';

    SELECT TOP 1 @inst_name = cdet.InstanceName, @inst_index = cdet.InstanceIndex
    FROM dbo.CounterData ctr
    JOIN dbo.CounterDetails cdet ON ctr.CounterID = cdet.CounterID
    WHERE cdet.ObjectName = 'Process' AND cdet.CounterName LIKE 'ID Process'
          AND cdet.InstanceName LIKE 'sqlservr%' AND ctr.CounterValue = @process_id;

    SELECT sql_cpu.CounterDateTime AS EventTime,
           os_cpu.system_idle_cpu,
           CASE WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization
                THEN os_cpu.total_cpu_utilization ELSE sql_cpu.sql_cpu_utilization
           END AS sql_cpu_utilization,
           os_cpu.total_cpu_utilization - (CASE WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization
                THEN os_cpu.total_cpu_utilization ELSE sql_cpu.sql_cpu_utilization END) AS nonsql_cpu_utilization
    FROM (
        SELECT ctr.CounterDateTime,
               CONVERT(INT, (FLOOR(ctr.CounterValue) / (100 * @cpu_count)) * 100) AS sql_cpu_utilization,
               det.InstanceName, det.InstanceIndex
        FROM dbo.CounterData ctr
        JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
        WHERE det.ObjectName = 'Process' AND det.CounterName LIKE '[%] Processor Time'
              AND det.InstanceName = @inst_name AND det.InstanceIndex = @inst_index
    ) AS sql_cpu
    INNER JOIN (
        SELECT ctr.CounterDateTime,
               FLOOR(ctr.CounterValue) AS total_cpu_utilization,
               100 - FLOOR(ctr.CounterValue) AS system_idle_cpu
        FROM dbo.CounterData ctr
        JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
        WHERE det.ObjectName = 'Processor Information'
              AND det.CounterName LIKE '[%] Processor Time' AND det.InstanceName = '_Total'
    ) AS os_cpu ON sql_cpu.CounterDateTime = os_cpu.CounterDateTime;
END;
```

NOTE: This query uses variables and joins that may not work with the read-only query tool
directly. Adapt by using simpler versions or running parts separately.

## Simplified CPU Query (recommended for tool use)
```sql
SELECT TOP 50 ctr.CounterDateTime AS EventTime,
       FLOOR(ctr.CounterValue) AS total_cpu_percent
FROM dbo.CounterData ctr
JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
WHERE det.ObjectName = 'Processor Information'
      AND det.CounterName LIKE '%Processor Time%' AND det.InstanceName = '_Total'
ORDER BY ctr.CounterDateTime
```

## Top Wait Categories
Uses the stored procedure DataSet_WaitStats_WaitStatsTop5Categories which aggregates
wait statistics from tbl_OS_WAIT_STATS into categories.
Requires: tbl_OS_WAIT_STATS

```sql
SELECT TOP 10 wait_category,
       SUM(wait_time_ms) AS total_wait_time_ms,
       SUM(waiting_tasks_count) AS total_waiting_tasks
FROM (
    SELECT CASE
        WHEN wait_type LIKE 'LCK_%' THEN 'Lock'
        WHEN wait_type LIKE 'PAGEIOLATCH_%' THEN 'Buffer I/O'
        WHEN wait_type LIKE 'PAGELATCH_%' THEN 'Buffer Latch'
        WHEN wait_type LIKE 'LATCH_%' THEN 'Latch'
        WHEN wait_type LIKE 'IO_COMPLETION%' OR wait_type = 'WRITELOG' THEN 'I/O'
        WHEN wait_type LIKE 'ASYNC_NETWORK_IO%' THEN 'Network'
        WHEN wait_type IN ('SOS_SCHEDULER_YIELD','THREADPOOL','CXPACKET','CXCONSUMER') THEN 'CPU'
        WHEN wait_type LIKE 'RESOURCE_SEMAPHORE%' THEN 'Memory'
        WHEN wait_type LIKE 'CLR_%' THEN 'CLR'
        WHEN wait_type LIKE 'HADR_%' THEN 'AlwaysOn'
        ELSE 'Other'
    END AS wait_category,
    wait_time_ms, waiting_tasks_count
    FROM tbl_OS_WAIT_STATS
    WHERE wait_type NOT IN ('SLEEP_TASK','BROKER_TO_FLUSH','SQLTRACE_BUFFER_FLUSH',
          'CLR_AUTO_EVENT','CLR_MANUAL_EVENT','LAZYWRITER_SLEEP','CHECKPOINT_QUEUE',
          'WAITFOR','XE_TIMER_EVENT','XE_DISPATCHER_WAIT','FT_IFTS_SCHEDULER_IDLE_WAIT',
          'LOGMGR_QUEUE','DIRTY_PAGE_POLL','REQUEST_FOR_DEADLOCK_SEARCH',
          'HADR_FILESTREAM_IOMGR_IOCOMPLETION','SP_SERVER_DIAGNOSTICS_SLEEP')
) categorized
GROUP BY wait_category
ORDER BY total_wait_time_ms DESC
```

## Average CPU Utilization Summary
```sql
SELECT AVG(FLOOR(ctr.CounterValue)) AS avg_total_cpu_percent,
       MAX(FLOOR(ctr.CounterValue)) AS max_total_cpu_percent,
       MIN(FLOOR(ctr.CounterValue)) AS min_total_cpu_percent
FROM dbo.CounterData ctr
JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
WHERE det.ObjectName = 'Processor Information'
      AND det.CounterName LIKE '%Processor Time%' AND det.InstanceName = '_Total'
```
