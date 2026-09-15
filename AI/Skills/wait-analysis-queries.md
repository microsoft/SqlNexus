# Wait Analysis Queries

## Wait Stats Over Time (Delta Calculation)
Calculates the wait time deltas between snapshots to show wait rate trends.
Requires: tbl_OS_WAIT_STATS

```sql
;WITH ordered_set AS (
    SELECT ROW_NUMBER() OVER (PARTITION BY wait_type ORDER BY rownum) AS row_order,
           wait_type, wait_category, runtime,
           waiting_tasks_count, wait_time_ms, max_wait_time_ms
    FROM dbo.tbl_OS_WAIT_STATS
)
SELECT TOP 100
    b.wait_type,
    b.runtime AS interval_start,
    e.runtime AS interval_end,
    DATEDIFF(ss, b.runtime, e.runtime) AS delta_seconds,
    CASE WHEN e.max_wait_time_ms >= b.max_wait_time_ms
         THEN e.wait_time_ms - b.wait_time_ms ELSE NULL END AS delta_wait_time_ms,
    CASE WHEN e.max_wait_time_ms >= b.max_wait_time_ms AND DATEDIFF(ss, b.runtime, e.runtime) > 0
         THEN (e.wait_time_ms - b.wait_time_ms) / DATEDIFF(ss, b.runtime, e.runtime)
         ELSE NULL END AS wait_ms_per_sec
FROM ordered_set e
LEFT JOIN ordered_set b ON e.wait_type = b.wait_type AND e.row_order = b.row_order + 1
WHERE b.runtime IS NOT NULL
ORDER BY interval_start, wait_type
```

## Top Wait Types (Cumulative)
```sql
SELECT TOP 20 wait_type, wait_category,
       SUM(waiting_tasks_count) AS total_waiting_tasks,
       SUM(wait_time_ms) AS total_wait_time_ms,
       SUM(signal_wait_time_ms) AS total_signal_wait_ms,
       SUM(wait_time_ms) - SUM(signal_wait_time_ms) AS total_resource_wait_ms
FROM tbl_OS_WAIT_STATS
WHERE wait_type NOT IN ('SLEEP_TASK','BROKER_TO_FLUSH','SQLTRACE_BUFFER_FLUSH',
      'CLR_AUTO_EVENT','CLR_MANUAL_EVENT','LAZYWRITER_SLEEP','CHECKPOINT_QUEUE',
      'WAITFOR','XE_TIMER_EVENT','XE_DISPATCHER_WAIT','FT_IFTS_SCHEDULER_IDLE_WAIT',
      'LOGMGR_QUEUE','DIRTY_PAGE_POLL','REQUEST_FOR_DEADLOCK_SEARCH',
      'HADR_FILESTREAM_IOMGR_IOCOMPLETION','SP_SERVER_DIAGNOSTICS_SLEEP')
GROUP BY wait_type, wait_category
ORDER BY total_wait_time_ms DESC
```

## Sampled Waits from Active Requests
Top sessions captured waiting, ordered by longest wait duration.
```sql
SELECT TOP 50 runtime, session_id, wait_type, wait_duration_ms,
       wait_resource, request_cpu_time, request_logical_reads,
       request_total_elapsed_time, command, [program_name]
FROM dbo.tbl_REQUESTS
WHERE wait_type IS NOT NULL AND wait_type <> ''
ORDER BY wait_duration_ms DESC
```

## Wait Category Interpretation
- **CPU**: SOS_SCHEDULER_YIELD, THREADPOOL, CXPACKET — CPU pressure or parallelism issues
- **Buffer I/O**: PAGEIOLATCH_* — Reading data from disk; check I/O subsystem
- **Lock**: LCK_M_* — Lock contention; check blocking and transaction isolation
- **Latch**: PAGELATCH_* — Buffer latch contention; often TempDB related
- **I/O**: WRITELOG, IO_COMPLETION — Log write or I/O operations
- **Network**: ASYNC_NETWORK_IO — Client not consuming results fast enough
- **Memory**: RESOURCE_SEMAPHORE — Memory grant waits; queries need more memory
