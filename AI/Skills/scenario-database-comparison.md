# Two-Database Comparison — SQL Nexus Configuration & Workload Diff

## SYMPTOM
"Compare two SQL Nexus databases" / "Compare two servers" / "Compare two captures/runs" / "What is different between setup1 and setup2" / "Diff the configuration of these two collections" / "Side-by-side comparison of two Nexus databases"

Use this skill when the user wants to compare **two separate SQL Nexus databases** — for example two different servers, two environments (prod vs test), or two collection runs of the same server.

---

## PRIMARY TOOL

### `compare_nexus_databases`
A dedicated, first-class MCP tool that compares the **primary** SQL Nexus database against a **second** SQL Nexus database. Prefer this tool over hand-writing cross-database SQL through `query_nexus_database` — it is safer (parameterized existence checks, bracket-quoted identifiers) and returns a consistent, structured result.

**Prerequisite**: The server must be started with a second database configured, e.g.:

```json
"args": ["--server", "localhost", "--database", "sqlnexus", "--database2", "sqlnexus_detailed", "--trusted-connection", "true"]
```

Accepted flags (all optional; comparison is disabled when none is supplied):
- `--database2 <name>` (primary spelling)
- `--database-for-comparison <name>` (alias)
- `--database_for_comparison <name>` (alias)
- `SqlNexus:Database2` configuration / `SqlNexus__Database2` environment variable

If no second database is configured, the tool returns a clear message explaining how to configure one. In that case, the server and all other tools still work normally.

**Inputs**: none — both database names come from server startup configuration.

---

## WHAT IT RETURNS

The tool returns a JSON object with a `sections` map. Each section is produced independently, so a missing table in one area does not fail the whole comparison.

| Section | Source table(s) | Description |
|---------|-----------------|-------------|
| `server_properties` | `dbo.tbl_ServerProperties` | Every server property side-by-side, with a `Different` flag ('Yes' when the two values differ). Covers edition, build, CPU count, memory, uptime, etc. |
| `database_options` | `dbo.tbl_database_options` | Per-database compatibility level and status, with `CmptLevel_Different` and `Status_Different` flags. Databases present in both are compared by `name`. **Only databases that differ are returned** (identical databases are omitted to reduce output). |
| `database_scoped_configurations` | `dbo.tbl_database_scoped_configurations` | Per-database scoped configuration values (MAXDOP, legacy cardinality estimation, etc.) with a `Different` flag. **Only settings that differ are returned** (identical settings are omitted to reduce output). |
| `sys_configurations` | `dbo.tbl_Sys_Configurations` | Server-level `sp_configure` settings compared by `name` on `value_in_use` (e.g. max degree of parallelism, cost threshold for parallelism, max server memory), with a `Different` flag. **Only settings that differ are returned** (identical settings are omitted to reduce output). |
| `query_performance` | `ReadTrace.tblBatches`, `ReadTrace.tblUniqueBatches` | Only when ReadTrace exists in **both** databases. Compares avg duration/CPU and execution counts for the **top 30** longest-running normalized queries from each database, joined on `HashID`, with deltas. Column aliases use the actual database names. Diagnostic-collector noise queries are filtered out. |

Column aliases in `server_properties`, `database_options`, and `database_scoped_configurations` are named after the two database names so the output is self-describing.

---

## DECISION TREE

1. **Is a second database configured?**
   - No ? tell the user how to set `--database2` (see prerequisite above) and stop.
   - Yes ? call `compare_nexus_databases`.

2. **Interpret the config sections first** (`server_properties`, `database_options`, `database_scoped_configurations`):
   - Filter to rows where the `Different` / `*_Different` flag is `'Yes'`.
   - Highlight impactful differences: edition/build, CPU count, max server memory, compatibility level, MAXDOP, cost threshold for parallelism, database status (READ_ONLY, RECOVERY, AUTO_CLOSE, etc.).

3. **Interpret `query_performance`** (when present):
   - Sort by `Delta_AvgDuration_ms` to find queries that regressed the most between the two captures.
   - **Caveat**: a meaningful before/after comparison requires the two captures to share query hashes. If there are **no common `HashID` values**, the join returns few/no rows — say so explicitly and do NOT present a misleading regression story.

4. **Account for inventory differences**: If one capture has many more databases than the other, most `database_scoped_configurations` differences reflect **database inventory growth**, not changed settings. Note this before concluding "settings changed".

---

## RELATED SKILLS
- **Before/after workload regression on ReadTrace** (single-database-style dynamic SQL, "was fast yesterday, slow today") ? `scenario-comparative-analysis.md`
- **Query regression root cause** ? `scenario-performance.md`, `scenario-cpu.md`, `scenario-io.md`
- **Statistics / plan changes** ? `scenario-index-optimization.md`

---

## NOTES & GUARDRAILS
- Treat all output as AI-assisted and potentially incomplete; validate differences against the underlying tables with `query_nexus_database` (e.g. `SELECT * FROM <db>.dbo.tbl_ServerProperties`).
- The tool validates that both databases exist on the server before running; a clear error is returned if either is missing.
- No production system is contacted and no data is modified — all comparison is read-only over pre-collected SQL Nexus data.
