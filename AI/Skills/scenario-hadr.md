# Always On / HADR Health - Scenario Guide

## PURPOSE
Diagnose SQL Server Always On Availability Group (AG) and High Availability / Disaster Recovery (HADR) health from pre-collected SQL Nexus data: availability group states, per-database/per-replica synchronization, listener configuration, and AlwaysOn health-session events (failovers, lease expirations, replica state changes).

---

## WHEN TO USE
- Availability group reported as **NOT SYNCHRONIZING** / **NOT HEALTHY**
- A database is **not synchronizing**, **suspended**, or stuck in **RESOLVING**
- Unexpected or repeated **failovers**
- **Lease timeout** / cluster health-check timeout suspected
- Listener connectivity problems
- Data-loss risk assessment on a secondary replica
- General "is my Always On healthy?" health check

**Keywords**: always on, alwayson, availability group, AG, HADR, high availability, disaster recovery, replica, secondary, primary, synchronization, sync health, failover, lease expired, listener, RESOLVING, redo queue, log send queue.

---

## PRIMARY MCP TOOL

### `analyze_hadr_health`
**Purpose**: One-stop Always On / HADR health assessment.
**Use When**: Any of the symptoms above, or as a first pass for AG-related investigations.

Inspects these SQL Nexus tables when present (missing ones are reported under `tables_not_present`):

| Section | Table | What it tells you |
|---------|-------|-------------------|
| `ag_states` | `tbl_hadr_ag_states` | AG-level synchronization_health, primary replica, automated backup preference |
| `ag_database_replica_states` | `tbl_hadr_ag_database_replica_states` | Per-database, per-replica synchronization_state, synchronization_health, is_suspended, log send / redo queue sizes |
| `ag_listeners` | `tbl_hadr_ag_listeners` | Listener DNS name, port, IP configuration |
| `lease_expired` | `tbl_hadr_alwayson_health_availability_group_lease_expired` | AG lease-expired events (cluster/health-check timeouts) |
| `failovers` | `tbl_hadr_alwayson_health_failovers` | Availability replica failover events |
| `replica_state_change` | `tbl_hadr_alwayson_health_availability_replica_state_change` | Replica role/state change events (RESOLVING, PRIMARY, SECONDARY transitions) |
| `diagnostics_log_configurations` | `tbl_hadr_dm_os_server_diagnostics_log_configurations` | Server diagnostics log: is_enabled, max_size, max_files, path |

**Output shape**:
- `issues_found` — pre-triaged problems with a severity rating
- `tables_not_present` — HADR tables absent from this collection
- `sections` — full per-table data for deeper inspection

---

## ISSUE INTERPRETATION

The tool pre-flags the following under `issues_found`:

| Issue | Severity | Meaning / Action |
|-------|----------|------------------|
| AG synchronization not healthy | High | `synchronization_health` is NOT_HEALTHY (0) or PARTIALLY_HEALTHY (1). One or more replicas/databases are out of the expected sync state. Drill into `ag_database_replica_states`. |
| Database replica not healthy/synchronized | High | A database is not in a SYNCHRONIZED/SYNCHRONIZING state or its health is degraded. Check redo/log send queues for backlog; verify network and secondary I/O. |
| Data movement suspended | High | `is_suspended = 1` — data movement is paused for a database. It will fall behind and risk data loss on failover. Resume once the underlying cause is cleared. |
| Lease-expired event(s) recorded | High | Cluster/health-check lease timed out — a common trigger for automatic failover. Correlate the timestamp with CPU/I/O pressure and `sp_server_diagnostics`. |
| Failover event(s) recorded | High | One or more replica failovers occurred. Correlate with lease expirations, errorlog, and resource pressure at the same timestamps. |
| Replica state-change event(s) recorded | Medium | Role/state transitions occurred (e.g., PRIMARY?RESOLVING). Expected during planned failover; unexpected clusters of transitions indicate instability. |
| Server diagnostics log disabled | Low | `is_enabled = 0` — the AlwaysOn health/diagnostics log is off, reducing future diagnosability. Recommend enabling. |

---

## INVESTIGATION FLOW

1. Call `analyze_hadr_health`.
2. If `tables_not_present` lists all HADR tables ? this collection has **no Always On data**. Tell the user the LogScout **AlwaysOn** scenario is required to capture it.
3. Review `issues_found` first — these are the pre-triaged problems.
4. For sync problems: inspect `ag_database_replica_states` for large **log send queue** or **redo queue** sizes (backlog = data-loss / long failover risk).
5. For failovers: correlate `failovers` and `lease_expired` timestamps with:
   - `analyze_cpu_usage` / `get_sql_cpu_usage_over_time` (CPU starvation can cause lease timeouts)
   - `analyze_io_waits` / `get_sql_file_io_stats` (I/O stalls on the primary)
   - `tbl_ERRORLOG` (via `query_nexus_database`) around the same time
6. For listener issues: check `ag_listeners` for expected DNS name / port / IP.

---

## CROSS-CHECK QUERIES

If deeper detail is needed beyond the tool output, use `query_nexus_database`:

```sql
-- Databases with the largest replication backlog (risk of data loss / slow failover)
IF OBJECT_ID('dbo.tbl_hadr_ag_database_replica_states') IS NOT NULL
BEGIN
    SELECT *
    FROM dbo.tbl_hadr_ag_database_replica_states
    ORDER BY
        TRY_CONVERT(bigint, log_send_queue_size) DESC,
        TRY_CONVERT(bigint, redo_queue_size) DESC;
END
```

```sql
-- Timeline of AlwaysOn health-session failover and lease-expired events
IF OBJECT_ID('dbo.tbl_hadr_alwayson_health_failovers') IS NOT NULL
    SELECT 'failover' AS event_kind, * FROM dbo.tbl_hadr_alwayson_health_failovers;

IF OBJECT_ID('dbo.tbl_hadr_alwayson_health_availability_group_lease_expired') IS NOT NULL
    SELECT 'lease_expired' AS event_kind, * FROM dbo.tbl_hadr_alwayson_health_availability_group_lease_expired;
```

---

## KEY THRESHOLDS & RULES
- `synchronization_health`: 2 = HEALTHY, 1 = PARTIALLY_HEALTHY, 0 = NOT_HEALTHY.
- Synchronous-commit replica should be **SYNCHRONIZED**; asynchronous should be **SYNCHRONIZING**. Anything else warrants investigation.
- A **growing** log send / redo queue means the secondary cannot keep up — expect longer failover times and potential data loss on async replicas.
- Lease expirations are almost always a **symptom** — the root cause is usually resource pressure (CPU/I/O) or a stalled `sp_server_diagnostics` on the primary. Always correlate timestamps.

---

## DATA GAPS
If `analyze_hadr_health` reports HADR tables under `tables_not_present`, the collection did not include Always On data. Recommend re-running SQL LogScout with the **AlwaysOn / availability group** collection scenario, then re-import into SQL Nexus.
