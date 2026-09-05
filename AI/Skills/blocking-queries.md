# Blocking Analysis Queries

## Blocking Chain Summary
Shows periods of blocking with head blocker session IDs, duration, and count of blocked sessions.
Requires: tbl_REQUESTS or blocking-related tables

```sql
SELECT TOP 50
    MIN(runtime) AS blocking_start,
    MAX(runtime) AS blocking_end,
    blocking_session_id AS head_blocker_session_id,
    wait_type AS blocking_wait_type,
    COUNT(DISTINCT session_id) AS blocked_session_count,
    MAX(wait_duration_ms) AS max_wait_duration_ms,
    AVG(wait_duration_ms) AS avg_wait_duration_ms
FROM tbl_REQUESTS
WHERE blocking_session_id <> 0
GROUP BY blocking_session_id, wait_type
ORDER BY max_wait_duration_ms DESC
```

## Head Blocker Summary
```sql
SELECT TOP 20 * FROM tbl_HEADBLOCKERSUMMARY ORDER BY cnt DESC
```

## Detailed Blocking Snapshot
Shows the full details of blocked sessions at a specific point in time.
```sql
SELECT TOP 50 runtime, session_id, blocking_session_id,
       wait_type, wait_duration_ms, wait_resource,
       [program_name], [host_name], login_name, command,
       open_trans, transaction_isolation_level
FROM tbl_REQUESTS
WHERE blocking_session_id <> 0
ORDER BY wait_duration_ms DESC
```

## Blocking by Wait Type Distribution
```sql
SELECT wait_type, COUNT(*) AS occurrence_count,
       SUM(wait_duration_ms) AS total_wait_ms,
       AVG(wait_duration_ms) AS avg_wait_ms,
       MAX(wait_duration_ms) AS max_wait_ms
FROM tbl_REQUESTS
WHERE blocking_session_id <> 0
GROUP BY wait_type
ORDER BY total_wait_ms DESC
```

## Analysis Tips
- Duration > 30 seconds indicates significant blocking
- More than 5 blocked sessions behind one head blocker is critical
- Look at the `wait_resource` to identify the contested resource
- Cross-reference head blocker session_id with active requests to find the blocking query
- LCK_M_* wait types indicate lock contention
