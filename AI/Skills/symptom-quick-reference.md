# SQL Server Symptom-to-Analysis Quick Reference
## Fast Triage Guide for Diagnostic Agent

## PURPOSE
Instant mapping from user symptoms → LogScout scenario → Nexus queries → Expected root cause.  
Use this for **first-pass triage** before consulting detailed decision trees.

---

## QUICK SYMPTOM LOOKUP

### 🔥 **"SQL Server is slow" / "Everything is slow"**
**LogScout**: `GeneralPerf` (3-5 minutes during issue)  
**First Queries**:
1. Query #4 - Overall Wait Statistics → Identify bottleneck category
2. Query #1 - Top 50 Longest Queries → Find slow queries
3. Query #17 - CPU Utilization → Check CPU pressure

**Expected Root Causes**: Resource bottleneck (CPU/I/O/Memory), expensive queries, blocking

---

### ⏱️ **"Specific query is slow" / "Query never completes"**
**LogScout**: `DetailedPerf` (if reproducible) or `NeverEndingQuery` (if running now)  
**First Queries**:
1. Query #1 - Find the query and get its HashID
2. Query #2 - Stats for that specific query
3. Query #5 - Waits for that specific query
4. Analyze execution plan from XEvent data

**Expected Root Causes**: Missing indexes, parameter sniffing, poor plan choice, excessive scans

---

### 🔒 **"Blocked queries" / "Locking issues"**
**LogScout**: `GeneralPerf`  
**First Queries**:
1. Query #9 - Head Blockers Summary → Find blocking sessions
2. Query #10 - Blocked Sessions Detail → See what's blocked and by whom
3. Query #4 - Check for LCK_* waits

**Expected Root Causes**: Long-running transactions, hot spot contention, missing indexes causing long scans

---

### 💀 **"Deadlocks happening"**
**LogScout**: `GeneralPerf` (includes deadlock XEvents)  
**First Queries**:
1. Review deadlock graphs from XEvent files
2. Query #10 - Blocking patterns
3. Identify conflicting query patterns

**Expected Root Causes**: Update order conflicts, missing indexes, lock escalation

---

### 🔥 **"High CPU usage"**
**LogScout**: `GeneralPerf`  
**First Queries**:
1. Query #17 - CPU Utilization Over Time → Confirm high CPU
2. Query #18 - Top Queries by CPU → Find CPU hogs
3. Query #4 - Check for SOS_SCHEDULER_YIELD waits
4. Query #20 - Spinlock Stats (if available)

**Expected Root Causes**: Expensive queries, excessive parallelism, parameter sniffing, missing statistics

---

### 💾 **"Out of memory" / "Memory pressure"**
**LogScout**: `Memory`  
**First Queries**:
1. Query #15 - Memory Clerks Analysis → See what's consuming memory
2. Query #4 - Check for RESOURCE_SEMAPHORE waits
3. Check max server memory setting vs. available memory

**Expected Root Causes**: Oversized memory grants, max memory too high/low, memory leak, plan cache bloat

---

### 💿 **"Disk slow" / "I/O slow"**
**LogScout**: `IO`  
**First Queries**:
1. Query #16 - File I/O Statistics → Find slow files
2. Query #4 - Check for PAGEIOLATCH_* and WRITELOG waits
3. Correlate with storage performance metrics

**Expected Root Causes**: Storage bottleneck, excessive reads (missing indexes), transaction log on slow disk

---

### 📶 **"Can't connect" / "Connection drops"**
**LogScout**: `Setup` or `NetworkTrace`  
**First Analysis**:
- Review error logs for connection errors
- Check protocol configuration
- Review network trace for packet loss

**Expected Root Causes**: Firewall, authentication, client driver issues, network instability

---

### 🔄 **"Replication lag"**
**LogScout**: `Replication` (on both publisher and subscriber)  
**First Analysis**:
- Review replication DMV snapshots
- Check distribution database size
- Check network latency

**Expected Root Causes**: Large transactions, subscriber hardware, network bandwidth, schema conflicts

---

### 🔁 **"Always On AG not syncing"**
**LogScout**: `AlwaysOn` (run on both primary and secondary)  
**First Queries**:
- Check HADR_* wait types
- Review LOG_SEND_QUEUE and REDO_QUEUE sizes
- Check replica performance

**Expected Root Causes**: Network latency, secondary replica CPU/I/O bottleneck, large transactions

---

### 💾 **"Backup is slow"**
**LogScout**: `BackupRestore`  
**First Queries**:
1. Query #16 - File I/O Stats → Check backup destination I/O
2. Review backup XEvent traces
3. Check if backup compression is enabled

**Expected Root Causes**: Backup destination I/O bottleneck, no compression, network backup location

---

### 🕐 **"Issue only happens at specific time"** (e.g., 9 AM daily)
**LogScout**: `LightPerf` (continuous monitoring)  
**Strategy**: Run continuously, then correlate timing with:
- Scheduled jobs (backups, index maintenance)
- Business cycle patterns
- Batch processes

**Expected Root Causes**: Scheduled maintenance, ETL processes, peak user activity

---

## WAIT TYPE → BOTTLENECK MAPPING

| **Wait Type** | **Bottleneck Category** | **Investigation Path** |
|---------------|------------------------|------------------------|
| PAGEIOLATCH_* | I/O (reads) | Query #16 (File I/O), check storage performance |
| WRITELOG | I/O (log writes) | Query #16, check transaction log file placement |
| LCK_M_* | Blocking/Locking | Query #9, #10 (Head blockers, blocked sessions) |
| SOS_SCHEDULER_YIELD | CPU Pressure | Query #17, #18 (CPU utilization, top CPU queries) |
| RESOURCE_SEMAPHORE | Memory Grants | Query #15 (Memory clerks), check max memory |
| ASYNC_NETWORK_IO | Network/Client | Client slow to consume results, check network |
| CXPACKET / CXCONSUMER | Parallelism | Review MAXDOP, check query plans, Query #18 |
| PAGELATCH_* | Tempdb contention | Check tempdb file count, PFS/SGAM contention |

---

## QUERY SELECTION BY SYMPTOM

### Performance Investigation Workflow
```
1. Query #4 → Identify primary wait type
2. Match wait type to bottleneck (use table above)
3. Run targeted queries for that bottleneck:
   - I/O: Query #16
   - CPU: Query #17, #18
   - Blocking: Query #9, #10
   - Memory: Query #15
4. Query #1 → Find slowest queries
5. For top queries: Query #2, #5 → Drill into specific query
```

### Specific Query Investigation
```
1. Query #1 → Find query and get HashID
2. Query #2 → Execution history for that HashID
3. Query #5 → Wait analysis for that query
4. Review execution plan from XEvent data
```

### Blocking Investigation
```
1. Query #9 → Identify head blockers
2. Query #10 → See full blocking chain
3. Find blocker query patterns → look for long transactions
4. Check for missing indexes on blocked queries
```

### Resource Bottleneck
```
CPU: Query #17 → #18 → Review top queries
I/O: Query #16 → Check latency → Storage team
Memory: Query #15 → Check clerks → Review max memory
```

---

## AGENT RESPONSE TEMPLATES

### Template: General Performance
*"I'll collect performance diagnostics using the GeneralPerf scenario for 3-5 minutes during the issue. Then I'll analyze the wait statistics to identify the primary bottleneck, find the slowest queries, and check CPU utilization."*

### Template: Specific Slow Query
*"I'll capture detailed traces using DetailedPerf during the query execution, then analyze the execution plan, wait statistics, and resource usage for that specific query to identify why it's slow."*

### Template: Blocking Investigation
*"I'll collect blocking data with GeneralPerf, then identify the head blocker sessions, review what they're blocking, and analyze the queries involved to find the root cause of the locking."*

### Template: High CPU
*"I'll collect CPU diagnostics with GeneralPerf, then identify which queries are consuming the most CPU time and analyze their execution patterns to find optimization opportunities."*
