# I/O Performance Issues - Complete Diagnostic Workflow

## SYMPTOM
"Disk slow" / "I/O latency high" / "PAGEIOLATCH waits" / "WRITELOG waits"

---

## DECISION TREE

### Is I/O slow RIGHT NOW?

#### ✅ **YES** (currently experiencing latency)

**LogScout Scenario**: `IO`  
**Command**: `.\SQL_LogScout.cmd start IO`  
**Note**: Collects StorPort trace + High_IO_Perfstats  
**Duration**: 2-5 minutes

**Analysis Sequence**:

1. **Check for PAGEIOLATCH/WRITELOG waits** → Query #4
2. **Identify worst-performing files** → Query #16
3. **Check I/O-intensive queries** → Query #1 (sorted by Reads)
4. **Check if wait-heavy queries correlate with I/O** → Query #14

**Indicators from Query #4**:
- **PAGEIOLATCH_SH**: Data page read waits (reading data from disk)
- **PAGEIOLATCH_EX**: Data page write/modification waits
- **WRITELOG**: Transaction log write waits

**Indicators from Query #16**:
- **AvgIOTimeMS > 15-20ms**: Slow storage
- **High IoStallMS**: Cumulative I/O wait time
- Specific file with high latency

**Root Cause Categories**:
- Storage subsystem slow (hardware/SAN issue)
- Queries doing excessive reads (missing indexes)
- TempDB contention (check tempdb file stats)
- Log file on slow storage

---

#### ❌ **NO** (intermittent / historical)

**LogScout Scenario**: `LightPerf` + periodic IO snapshots  
**Command**: `.\SQL_LogScout.cmd start LightPerf`  
**Analysis**: Correlate I/O spikes with activity using Query #4 and Query #16

---

### Is it data file I/O or log file I/O?

#### **DATA FILES** (PAGEIOLATCH_*)

**Focus on**:
1. Database file latency (Query #16)
2. Queries with high Reads (Query #26)
3. Check for missing indexes

**Next Steps**:
- Add indexes to reduce reads
- Upgrade storage
- Move hot files to faster storage (SSDs)
- Enable read-ahead optimizations
- Consider In-Memory OLTP for hot tables

---

#### **LOG FILES** (WRITELOG)

**Focus on**:
1. Transaction log file latency (Query #16, filter for .ldf)
2. Queries with high Writes (Query #27)
3. Check log flush frequency

**Next Steps**:
- Move log to faster storage (dedicated SSDs)
- Review transaction patterns (batch commits?)
- Check log backup frequency (full recovery model)
- Consider delayed durability (if some data loss acceptable)
- Review VLF fragmentation

---

### Advanced: Complex I/O Issues

**LogScout Scenario**: `IO` (includes StorPort trace)  
**Command**: `.\SQL_LogScout.cmd start IO`  
**Analysis**: Windows storage stack analysis
- StorPort ETW trace
- I/O request timing
- Driver-level bottlenecks

---

## EMBEDDED QUERIES

### Query #4: Overall Wait Statistics (Filter for I/O Waits)
**Purpose**: Identify I/O as bottleneck and type of I/O  
**Use When**: First step in I/O analysis

```sql
SELECT TOP 20
    wait_type,
    SUM(wait_time_ms) AS total_wait_ms,
    SUM(wait_time_ms) - SUM(signal_wait_time_ms) AS resource_wait_ms,
    SUM(signal_wait_time_ms) AS signal_wait_ms,
    SUM(waiting_tasks_count) AS wait_count,
    CAST(100.0 * SUM(wait_time_ms) / SUM(SUM(wait_time_ms)) OVER() AS DECIMAL(5,2)) AS pct
FROM tbl_OS_WAIT_STATS
WHERE wait_type NOT IN (
    'BROKER_EVENTHANDLER', 'BROKER_RECEIVE_WAITFOR', 'BROKER_TASK_STOP',
    'BROKER_TO_FLUSH', 'BROKER_TRANSMITTER', 'CHECKPOINT_QUEUE',
    'CLR_AUTO_EVENT', 'CLR_MANUAL_EVENT', 'CLR_SEMAPHORE',
    'DBMIRROR_DBM_EVENT', 'DBMIRROR_DBM_MUTEX', 'DBMIRROR_EVENTS_QUEUE',
    'DBMIRRORING_CMD', 'DIRTY_PAGE_POLL', 'DISPATCHER_QUEUE_SEMAPHORE',
    'FT_IFTS_SCHEDULER_IDLE_WAIT', 'FT_IFTSHC_MUTEX', 'HADR_CLUSAPI_CALL',
    'HADR_FILESTREAM_IOMGR_IOCOMPLETION', 'HADR_LOGCAPTURE_WAIT',
    'HADR_NOTIFICATION_DEQUEUE', 'HADR_TIMER_TASK', 'HADR_WORK_QUEUE',
    'LAZYWRITER_SLEEP', 'LOGMGR_QUEUE', 'ONDEMAND_TASK_QUEUE',
    'PREEMPTIVE_OS_LIBRARYOPS', 'PREEMPTIVE_OS_COMOPS',
    'PREEMPTIVE_OS_CRYPTOPS', 'PREEMPTIVE_OS_PIPEOPS',
    'PREEMPTIVE_HADR_LEASE_MECHANISM', 'PWAIT_ALL_COMPONENTS_INITIALIZED',
    'QDS_ASYNC_QUEUE', 'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
    'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', 'QDS_SHUTDOWN_QUEUE',
    'REQUEST_FOR_DEADLOCK_SEARCH', 'RESOURCE_QUEUE', 'SERVER_IDLE_CHECK',
    'SLEEP_BPOOL_FLUSH', 'SLEEP_DBSTARTUP', 'SLEEP_DCOMSTARTUP',
    'SLEEP_MASTERDBREADY', 'SLEEP_MASTERMDREADY', 'SLEEP_MASTERUPGRADED',
    'SLEEP_MSDBSTARTUP', 'SLEEP_SYSTEMTASK', 'SLEEP_TASK',
    'SLEEP_TEMPDBSTARTUP', 'SNI_HTTP_ACCEPT', 'SP_SERVER_DIAGNOSTICS_SLEEP',
    'SQLTRACE_BUFFER_FLUSH', 'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
    'SQLTRACE_WAIT_ENTRIES', 'WAIT_FOR_RESULTS', 'WAITFOR',
    'WAITFOR_TASKSHUTDOWN', 'WAIT_XTP_HOST_WAIT', 'WAIT_XTP_OFFLINE_CKPT_NEW_LOG',
    'WAIT_XTP_CKPT_CLOSE', 'XE_DISPATCHER_JOIN', 'XE_DISPATCHER_WAIT',
    'XE_TIMER_EVENT'
)
GROUP BY wait_type
ORDER BY total_wait_ms DESC;
```

**I/O-Specific Waits to Look For**:
- **PAGEIOLATCH_SH**: Shared page latch (reading data pages from disk)
  - High % = Reads are slow, check Query #16 for read latency
- **PAGEIOLATCH_EX**: Exclusive page latch (modifying pages, writing to disk)
  - High % = Write latency or page contention
- **PAGEIOLATCH_UP**: Update page latch (page modifications)
- **WRITELOG**: Waiting for transaction log writes to complete
  - High % = Log file I/O bottleneck, check log file placement
- **IO_COMPLETION**: Waiting for I/O operations (backup/restore operations)
- **ASYNC_IO_COMPLETION**: Asynchronous I/O waits (checkpoints, lazy writer)

**Interpretation**:
- **PAGEIOLATCH_* > 30%**: Data file I/O is primary bottleneck
- **WRITELOG > 20%**: Transaction log I/O is bottleneck

---

### Query #16: File I/O Statistics
**Purpose**: Identify I/O bottlenecks by specific files  
**Use When**: Confirming which files have high latency

```sql
SELECT 
    database_name,
    file_type,
    num_of_reads,
    num_of_writes,
    io_stall_read_ms,
    io_stall_write_ms,
    CASE WHEN num_of_reads = 0 THEN 0 
         ELSE io_stall_read_ms / num_of_reads END AS avg_read_latency_ms,
    CASE WHEN num_of_writes = 0 THEN 0 
         ELSE io_stall_write_ms / num_of_writes END AS avg_write_latency_ms,
    size_on_disk_bytes / 1024 / 1024 AS file_size_mb,
    physical_name
FROM tbl_FILE_STATS
ORDER BY io_stall_read_ms + io_stall_write_ms DESC;
```

**Output Interpretation**:
- **avg_read_latency_ms**:
  - < 10ms: Excellent (SSD)
  - 10-15ms: Good (fast HDD or SAN)
  - 15-20ms: Acceptable (standard SAN/HDD)
  - **> 20ms**: Slow, investigate storage (⚠️ ISSUE)
  - **> 50ms**: Very slow, critical storage bottleneck (🔥 CRITICAL)

- **avg_write_latency_ms**:
  - < 5ms: Excellent (SSD)
  - 5-10ms: Good
  - **> 10ms**: Slow for log writes (⚠️ ISSUE)
  - **> 20ms**: Critical for log files (🔥 CRITICAL)

- **io_stall_read_ms / io_stall_write_ms**: Total cumulative wait time
  - Highest values = files causing most I/O bottleneck

**Analysis**:
1. **TempDB files with high latency**: TempDB contention → Add more tempdb files, faster storage
2. **Log files (.ldf) with high write latency**: Move to faster storage, dedicated SSDs
3. **Data files (.mdf) with high read latency**: Add indexes, upgrade storage, check buffer pool hit ratio

---

### Query #26: Top Queries by Logical Reads
**Purpose**: Find queries doing excessive reads (causing I/O pressure)  
**Use When**: PAGEIOLATCH_SH waits are high

```sql
SELECT TOP 50
    SUM(b.Reads) AS Total_Reads,
    COUNT(*) AS Executions,
    SUM(b.Reads) / COUNT(*) AS Avg_Reads,
    SUM(b.Duration)/1000 AS Total_Duration_ms,
    SUBSTRING(ub.NormText, 1, 200) AS Query,
    b.HashID
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
GROUP BY ub.NormText, b.HashID
ORDER BY Total_Reads DESC;
```

**Output Interpretation**:
- **Total_Reads**: Total logical reads (from buffer pool or disk)
  - Very high = query scans large tables or indexes
- **Avg_Reads**: Average reads per execution
  - High = inefficient query, likely missing indexes
- **Executions**: How often the query runs
  - High executions + high reads = major I/O driver

**Analysis**:
1. **High Avg_Reads**: Check execution plan for:
   - Table scans → Add clustered index
   - Index scans → Add covering index or filtered index
   - Key lookups → Add INCLUDE columns to index
   - Nested loop joins on large tables → Consider hash join or index tuning

2. **High Total_Reads + High Executions**: Frequently-run query
   - Optimize query (add indexes)
   - Cache results if data doesn't change often
   - Consider indexed views or materialized data

**Action**: Use HashID with Query #2 to get execution plan and analyze

---

### Query #27: Top Queries by Writes
**Purpose**: Find queries writing most data (log file pressure)  
**Use When**: WRITELOG waits are high

```sql
SELECT TOP 50
    SUM(b.Writes) AS Total_Writes,
    COUNT(*) AS Executions,
    SUM(b.Writes) / COUNT(*) AS Avg_Writes,
    SUM(b.RowCounts) AS Total_Rows_Affected,
    SUBSTRING(ub.NormText, 1, 200) AS Query,
    b.HashID
FROM ReadTrace.tblBatches b
JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
WHERE b.Writes > 0
GROUP BY ub.NormText, b.HashID
ORDER BY Total_Writes DESC;
```

**Output Interpretation**:
- **Total_Writes**: Total writes (transaction log activity)
- **Avg_Writes**: Average writes per execution
- **Total_Rows_Affected**: Total rows inserted/updated/deleted

**Analysis**:
1. **Large batch operations**: UPDATE/DELETE without WHERE clause → Add WHERE, batch operations
2. **High Avg_Writes**: Review query logic
   - Updating unnecessary columns?
   - Triggers causing cascading writes?
   - Wide rows (LOB data)?

**Action**:
- Batch large operations (commit every 1000-10000 rows)
- Move log file to faster storage
- Review full vs simple recovery model
- Check log backup frequency

---

### Query #28: TempDB File Stats
**Purpose**: Identify TempDB I/O bottleneck  
**Use When**: PAGEIOLATCH_* on tempdb files

```sql
SELECT 
    file_id,
    num_of_reads,
    num_of_writes,
    io_stall_read_ms,
    io_stall_write_ms,
    CASE WHEN num_of_reads = 0 THEN 0 
         ELSE io_stall_read_ms / num_of_reads END AS avg_read_latency_ms,
    CASE WHEN num_of_writes = 0 THEN 0 
         ELSE io_stall_write_ms / num_of_writes END AS avg_write_latency_ms,
    size_on_disk_bytes / 1024 / 1024 AS file_size_mb
FROM tbl_FILE_STATS
WHERE database_name = 'tempdb'
ORDER BY io_stall_read_ms + io_stall_write_ms DESC;
```

**Output Interpretation**:
- **Multiple tempdb files with uneven I/O**: Imbalanced allocation
  - Add more files (# of files = # of CPU cores, up to 8)
  - Ensure all files same size and growth settings
- **High latency on all tempdb files**: Storage bottleneck
  - Move tempdb to faster storage (SSDs)

**TempDB Best Practices**:
- **# of files**: Start with # of cores (up to 8), add more if contention persists
- **File size**: All files same size
- **Growth**: All files same growth increment (MB, not %)
- **Location**: Dedicated fast storage (separate from data/log)

---

### Query #29: I/O by Database
**Purpose**: Identify which database is driving I/O  
**Use When**: Multiple databases on instance

```sql
SELECT 
    database_name,
    SUM(num_of_reads) AS total_reads,
    SUM(num_of_writes) AS total_writes,
    SUM(io_stall_read_ms) AS total_read_stall_ms,
    SUM(io_stall_write_ms) AS total_write_stall_ms,
    CASE WHEN SUM(num_of_reads) = 0 THEN 0
         ELSE SUM(io_stall_read_ms) / SUM(num_of_reads) END AS avg_read_latency_ms,
    CASE WHEN SUM(num_of_writes) = 0 THEN 0
         ELSE SUM(io_stall_write_ms) / SUM(num_of_writes) END AS avg_write_latency_ms
FROM tbl_FILE_STATS
GROUP BY database_name
ORDER BY total_read_stall_ms + total_write_stall_ms DESC;
```

**Output Interpretation**:
- **total_read_stall_ms + total_write_stall_ms**: Total I/O wait time for this database
- Identifies which database is causing most I/O pressure
- Focus tuning efforts on highest-impact database

---

## REMEDIATION STRATEGIES

### Immediate Actions

1. **Identify Top I/O Queries** (Query #26, #27):
   - Get HashID
   - Review execution plan
   - Look for scans, lookups, missing indexes

2. **Check File Placement** (Query #16):
   - Log files on slow storage? → Move to dedicated SSDs
   - TempDB on slow storage? → Move to fastest available storage
   - Data files on RAID 5? → Consider RAID 10 for better write performance

3. **TempDB Tuning** (if tempdb high in Query #28):
   ```sql
   -- Add more tempdb files (example: 4 files for 4-core server)
   ALTER DATABASE tempdb ADD FILE (
       NAME = tempdev2,
       FILENAME = 'T:\TEMPDB\tempdev2.ndf',
       SIZE = 8GB,  -- Match existing file sizes
       FILEGROWTH = 512MB
   );
   ```

### Long-Term Optimization

1. **Add Missing Indexes**:
   - Review execution plans from Query #26
   - Add covering indexes to eliminate key lookups
   - Add filtered indexes for selective queries

2. **Upgrade Storage**:
   - Move to SSDs (especially for log files and tempdb)
   - Use dedicated storage for log files (separate from data)
   - Consider NVMe SSDs for high-throughput workloads

3. **Optimize Queries**:
   - Reduce reads by adding indexes
   - Batch large write operations
   - Review triggers (can multiply writes)

4. **Consider In-Memory OLTP**:
   - For hotspot tables with high I/O
   - Eliminates physical I/O entirely
   - Requires SQL Server Enterprise Edition

5. **Monitor and Alert**:
   - Set up alerts for avg latency > 20ms
   - Monitor I/O patterns over time
   - Identify recurring I/O spikes

---

## STORAGE SUBSYSTEM CHECKLIST

### If Storage Team Involvement Needed

**Provide this data**:
- Average read/write latency from Query #16
- IOPS requirements (reads + writes per second)
- Database file sizes
- Peak I/O times

**Questions to ask storage team**:
- What is the actual disk latency at the storage level?
- Is there SAN contention with other workloads?
- Are we hitting IOPS limits?
- What RAID level is configured?
- Is read-ahead/write-behind cache enabled?

---

## NEXT STEPS BASED ON FINDINGS

### If Specific Query Driving I/O
→ Use [scenario-performance.md](scenario-performance.md) Query #2 to analyze execution patterns and optimize

### If Log File I/O Bottleneck
→ Move log to faster storage, batch transactions, review recovery model

### If TempDB Contention
→ Add more tempdb files, move to faster storage, optimize queries using temp tables

### If Storage Hardware Bottleneck
→ Engage storage team, upgrade disks/SAN, consider SSD migration

### If Missing Indexes
→ Review execution plans, implement missing index recommendations, monitor index usage
