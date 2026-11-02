## Purpose

This page provides queries that you can run directly against the SQL Nexus database to get useful information from your performance data. These queries mimic some of the GUI reports but mostly expand on them or provide more advanced analysis scenarios.

## Queries

Below are the most common troubleshooting queries you can use in analyzing the SQL Nexus data:

### Top 50 longest-running queries by duration (aggregate stats)

```sql
SELECT TOP 50 SUM(b.Duration)/1000 Duration_ms, 
		SUM(b.CPU) CPU_ms, 
		SUM(b.Duration)/1000 - SUM(b.CPU) WaitTime_ms, 
		CONVERT(decimal(8,2),	(((SUM(b.Duration)/1000.00) - SUM(b.cpu))/(CASE WHEN SUM(b.Duration)/1000 = 0 THEN 1 ELSE SUM(b.Duration)/1000 END )))*100 WaitPercentage, 
		SUM(b.Reads) Reads, 
		COUNT(*) Executions, 
		(SUM(b.Duration)/1000)/ (CASE WHEN COUNT(*) = 0 THEN 1 ELSE COUNT(*) END) AvgDuration, 
		SUM(b.CPU)/COUNT(*) AvgCPU, 
		SUBSTRING(ub.NormText, 1, 100) NormText, 
		b.HashID
FROM  ReadTrace.tblBatches b 
JOIN  ReadTrace.tblUniqueBatches ub
  ON b.HashID = ub.HashID
GROUP BY ub.NormText, b.HashID
ORDER BY Duration_ms DESC
```
### Stats for a specific query (based on HashID pulled form above)

Replace <hash_id> with a valid HashID from ReadTrace.tblBatches that matches a query text (NormText) of your choice. You may get HashID values from the top longest-running queries by using [Top 50 longest-running queries ](#top-50-longest-running-queries-by-duration-aggregate-stats)

```sql
SELECT TOP 20000 b.Session, b.Duration/1000 Duration, 
	(b.CPU) CPU, (b.Reads) Reads, b.attnseq, 
	b.StartTime as StartTime,  b.EndTime as EndTime,
	substring(u.OrigText, 1, 1000) OrigText, batchseq 
FROM  ReadTrace.tblBatches b
JOIN  ReadTrace.tblUniqueBatches u
  ON b.HashID = u.HashID
JOIN  ReadTrace.tblConnections c
  ON b.ConnSeq = c.ConnSeq 
  AND b.session = c.session 
WHERE b.HashID = <hash_id> 
ORDER BY duration DESC
```

### What is the overall SQLLogScout data collection time - in traces


Replace <hash_id> with a valid HashID from ReadTrace.tblBatches that matches a query text (NormText) of your choice. You may get HashID values from the top longest-running queries by using [Top 50 longest-running queries ](#top-50-longest-running-queries-by-duration-aggregate-stats)


```sql
SELECT
            MIN(tb.StartTime) AS CollectionStartTime,
            MAX(tb.EndTime)   AS CollectionEndTime,
            DATEDIFF(MINUTE,
                     MIN(tb.StartTime),
                     MAX(tb.EndTime))  AS CollectionDuration_min
        FROM ReadTrace.tblBatches tb;
```

### Get the overall waits on server (Bottleneck Analysis)
```sql
IF OBJECT_ID ('DataSet_WaitStats_WaitStatsTop5Categories') IS NOT NULL 
  AND OBJECT_ID ('tbl_OS_WAIT_STATS') IS NOT NULL
BEGIN
   EXEC DataSet_WaitStats_WaitStatsTop5Categories
END
```

### Find any waits on a bottleneck for a particular query (using the HashID of query)

Replace <hash_id> with a valid HashID from ReadTrace.tblBatches that matches a query text (NormText) of your choice. You may get HashID values from the top longest-running queries by using [Top 50 longest-running queries ](#top-50-longest-running-queries-by-duration-aggregate-stats)

```sql
SELECT runtime,ecid,blocking_session_id, task_state, wait_type, 
	wait_duration_ms, wait_resource, tran_name, command, request_status 
FROM  tbl_REQUESTS r JOIN  ReadTrace.tblBatches b
	ON r.session_id = b.Session
	and r.runtime between b.StartTime and EndTime
WHERE HashID = <hash_id>  -- change this value 
	AND task_state != 'running' AND task_state != 'runnable' 
```

If you are using an older version of SQLNexus which did not have the option to import events using local time in the StartTime and EndTime columns, use this query and you have to modify datediff to account for Xevents (UTC) times vs. local server times
Also, replace <hash_id> with a valid HashID from ReadTrace.tblBatches that matches a query text (NormText) of your choice. You may get HashID values from the top longest-running queries by using [Top 50 longest-running queries ](#top-50-longest-running-queries-by-duration-aggregate-stats)


```sql
SELECT runtime,ecid,blocking_session_id, task_state, wait_type, 
	wait_duration_ms, wait_resource, tran_name, command, request_status 
FROM  tbl_REQUESTS r JOIN  ReadTrace.tblBatches b
	ON r.session_id = b.Session
	AND dateadd(hh, -2, r.runtime) between b.StartTime AND b.EndTime
WHERE HashID = <hash_id>  -- change this value 
	AND task_state != 'running' AND task_state != 'runnable' 
```

### Find any waits for a particular query (using top 20 waits for a particular query)

Replace <hash_id> with a valid HashID from ReadTrace.tblBatches that matches a query text (NormText) of your choice. You may get HashID values from the top longest-running queries by using [Top 50 longest-running queries ](#top-50-longest-running-queries-by-duration-aggregate-stats)

```sql
SELECT runtime,ecid,blocking_session_id, task_state, wait_type,
       wait_duration_ms, wait_resource, tran_name, command, request_status 
FROM  tbl_REQUESTS r 
WHERE session_id in (
	SELECT DISTINCT top 20 t.session FROM  ReadTrace.tblBatches t
	WHERE HashID = <hash_id> -- change this value with a valid HashID
      AND r.runtime between t.starttime AND t.endtime)
ORDER BY runtime asc
```

### Aggregate waits AND the waiting queries from tbl_batches table (To get an idea where issue may be)

```sql
SELECT count(*) occurrences, sum(r.wait_duration_ms) WaitDensity_ms, r.wait_type, 
     q.procname, q.stmt_text 
FROM  tbl_REQUESTS r
     JOIN  tbl_notableactivequeries q
     ON r.session_id = q.session_id
     AND r.runtime = q.runtime
WHERE wait_type IS NOT NULL 
      AND wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY r.wait_type, q.procname, q.stmt_text
ORDER BY WaitDensity_ms DESC
```   

### Waits aggregated by wait resource and wait type

```sql
SELECT COUNT(*) occurrences, wait_resource, wait_type, 
	MAX(wait_duration_ms) maxWaitMs
FROM  tbl_REQUESTS 
WHERE wait_type IS NOT NULL
      AND wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY wait_resource, wait_type
ORDER BY occurrences DESC
```

### Counts per wait type

How many occurrences of a particular wait type was encountered?

```sql
SELECT count(*) occurrences, wait_type 
FROM  tbl_REQUESTS r
WHERE wait_type IS NOT NULL
      AND wait_type NOT IN 
        ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 
        'REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
        'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
        'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
GROUP BY wait_type
ORDER BY occurrences Desc
```
### Find head blockers AND their queries


```sql
SELECT runtime, head_blocker_session_id, head_blocker_proc_name,stmt_text AS head_blocker_stmt, 
       blocked_task_count, tot_wait_duration_ms AS blocked_total_wait_dur_ms, 
       avg_wait_duration_ms AS blocked_avg_wait_dur_ms 
FROM  tbl_HEADBLOCKERSUMMARY
ORDER BY runtime 
```

### Find all blocked sessions and queries

Find blocked sessions throughout entire SQLLogScout collection and the queries they are running

```sql
SELECT * 
FROM  tbl_REQUESTS r 
JOIN  tbl_NOTABLEACTIVEQUERIES q
	ON r.session_id = q.session_id
	AND r.runtime = q.runtime
WHERE blocking_session_id <> 0
ORDER BY r.rownum
```

### Find all the statements that belong to a single batch (if statement events were collected) 

Please replace <BatchSeq> with a valid BatchSeq from ReadTrace.tblBatches. You can find what query text corresponds to a batch by joining with ReadTrace.tblUniqueBatches and look at OrigText or NormText columns.

```sql
SELECT sum(cpu) CPU, SUM(Duration/1000.0) Duration , COUNT(*) Occurrences, ub.NormText
FROM  ReadTrace.tblStatements b 
JOIN  ReadTrace.tblUniqueStatements ub
	ON b.HashID = ub.HashID
WHERE b.BatchSeq = <BatchSeq> 
GROUP BY NormText
ORDER BY Duration Desc
```

### Find all the batches executed by a particular application
```sql
SELECT * 
FROM  ReadTrace.tblBatches b 
JOIN  ReadTrace.tblConnections c
  ON b.ConnSeq = c.ConnSeq AND b.session = c.session 
WHERE c.ApplicationName = 'sqlcmd'
```

### Find Application Names 
This script finds the top 100 long running queries for which the CPU time is less than 80% of Duration , 
meaning there was a wait. 
Then it finds what wait types are responsible for this wait AND summarizes the total wait_time by wait type for that query.
Feel free to change something else or change the 80% to a smaller percent AS these are arbitrary choices. The latter would mean that if say 50% was chosen, then out of the total duration the query ran on the CPU only 50% of the time, AND the rest it waited for something. 

```sql
WITH BatchesData (Session, starttime, endtime, hashid, cpu,duration, CpuPercentOfDuration, NormText)
as 
(
	SELECT Session, StartTime as starttime, EndTime as endtime, b.hashid, cpu,duration, 
	  (CPU * 1000.0) / Duration CpuPercentOfDuration, NormText
	FROM  ReadTrace.tblBatches b JOIN  ReadTrace.tblUniqueBatches ub
	  ON b.HashID = ub.HashID
	WHERE duration !=0
)
SELECT top 100 MAX(wait_duration_ms) MaxWaitDuration, r.wait_type, 
       t.NormText--aggreate the duration per wait_type and normtext
FROM  tbl_REQUESTS r
JOIN  BatchesData t
  ON r.runtime between t.starttime AND t.endtime
  AND r.session_id = t.Session
WHERE t.cpupercentofduration < 0.80               -- WHERE CPU is less than 80% of duration
  AND task_state != 'running' AND task_state != 'runnable' 
GROUP BY wait_type, NormText
ORDER BY MaxWaitDuration DESC
```

### Find the waits for SQL Server 

```sql
DECLARE @minruntime datetime, @maxruntime datetime, @cpu_count int 
SELECT @minruntime = MIN(runtime), @maxruntime = MAX(runtime) FROM  tbl_OS_WAIT_STATS
SELECT @cpu_count = PropertyValue FROM  tbl_ServerProperties WHERE PropertyName = 'cpu_count'

SELECT a.[wait_type], (b.[wait_time_ms]-a.[wait_time_ms]) TotalWait_ms_AcrossAllCPUs, 
	DATEDIFF(SECOND,a.runtime,b.runtime) CollectionTimeMin, 
	(b.[wait_time_ms]-a.[wait_time_ms])/(DATEDIFF(SECOND,a.runtime,b.runtime)*@cpu_count) WaitTime_ms_per_second_per_cpu,
	CASE WHEN a.[wait_type] in 
	('CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 'REQUEST_FOR_DEADLOCK_SEARCH',
	 'WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
	 'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
	 'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP'
	) 
	THEN 'IGNORE' END AS Ignorable
FROM  
	(SELECT * FROM  tbl_OS_WAIT_STATS a WHERE a.runtime = @minruntime) AS a
INNER JOIN  
	(SELECT * FROM  tbl_OS_WAIT_STATS b WHERE b.runtime =@maxruntime) AS b
ON a.[wait_type] = b.[wait_type]
ORDER BY TotalWait_ms_AcrossAllCPUs DESC
```

### Performance stats by app name
```sql
SELECT sum(TotalDuration) Duration_ms, sum(TotalCPU) CPU_ms, sum(TotalReads) Reads, AppName 
FROM   ReadTrace.tblBatchPartialAggs b 
INNER JOIN ReadTrace.tblUniqueAppNames a 
  ON a.iID = b.AppNameID
GROUP BY AppName
ORDER BY Duration_ms DESC
```

### Find any I/O delays on SQL Server machine

**AvgDiskSecPerTransfer**: Any values that show up here are greater than 20 ms per I/O and could indicate issues with an overwhelmed I/O subsystem on the one hand, or filter drivers on another, or a misconfigured storage system (or driver issues) when I/O isn't overwhelmed. See [Troubleshoot slow SQL Server performance caused by I/O issues methodology](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/troubleshoot-sql-io-performance?branch=main)

```sql
DECLARE @IO_threshold DECIMAL(12, 3);
SET @IO_threshold = 0.020;

IF ((OBJECT_ID('dbo.CounterData') IS NOT NULL)
   )
BEGIN
	SELECT CONVERT(DATETIME, dat.CounterDateTime),
				convert(decimal(10,3), dat.CounterValue) DiskSec_Per_Transfer,
				dl.ObjectName,
				dl.CounterName,
				dl.InstanceName AS DiskVolume
		FROM dbo.CounterData dat
			INNER JOIN dbo.CounterDetails dl
				ON dat.CounterID = dl.CounterID
		WHERE dl.ObjectName IN ( 'logicaldisk' )
				AND dl.CounterName IN ( 'Avg. Disk sec/Transfer' )
				AND dl.InstanceName <> '_Total'
				AND dat.CounterValue >= @IO_threshold;
END
```


### Find I/O waits inside SQL Server (overall and per CPU)

Understand the overall magnitude of I/O waits as reported within SQL Server and as captured by periodic snapshot of performance data

```SQL
DECLARE @minruntime DATETIME, @maxruntime DATETIME, @cpu_count INT;

SELECT @minruntime = MIN(runtime), @maxruntime = MAX(runtime) 
FROM tbl_OS_WAIT_STATS;

SELECT @cpu_count = PropertyValue 
FROM tbl_ServerProperties 
WHERE PropertyName = 'cpu_count';

SELECT 
    a.wait_type, 
    (b.wait_time_ms - a.wait_time_ms) AS TotalWait_ms_AcrossAllCPUs, 
    DATEDIFF(SECOND, a.runtime, b.runtime) AS CollectionTime_sec, 
    (b.wait_time_ms - a.wait_time_ms) / 
        (DATEDIFF(SECOND, a.runtime, b.runtime) * @cpu_count) AS WaitTime_ms_per_second_per_cpu,
    CASE WHEN a.wait_type IN 
        ('CXPACKET', 'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT', 'REQUEST_FOR_DEADLOCK_SEARCH',
         'WAITFOR', 'LOGMGR_QUEUE', 'CHECKPOINT_QUEUE', 'SLEEP_TASK', 
         'FT_IFTS_SCHEDULER_IDLE_WAIT', 'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER',
         'SP_SERVER_DIAGNOSTICS_SLEEP', 'LAZYWRITER_SLEEP')
        THEN 'IGNORE' 
    END AS Ignorable
FROM (SELECT * FROM tbl_OS_WAIT_STATS WHERE runtime = @minruntime) AS a
INNER JOIN (SELECT * FROM tbl_OS_WAIT_STATS WHERE runtime = @maxruntime) AS b
    ON a.wait_type = b.wait_type
WHERE a.wait_type LIKE 'PAGEIOLATCH_%' 
	OR a.wait_type = 'WRITELOG' 
	OR a.wait_type = 'LOGBUFFER'
	OR a.wait_type = 'IO_COMPLETION'
	OR a.wait_type = 'ASYNC_IO_COMPLETION'
ORDER BY TotalWait_ms_AcrossAllCPUs DESC;
```



### Find SPINLOCKS for SQL Server
```sql
DECLARE @cpus int
SELECT @cpus = PropertyValue FROM  tbl_ServerProperties
WHERE PropertyName = 'cpu_count'

SELECT  
	t2.[name] AS spinlock_name,  cast(cast(t2.spins AS float) - cast(t1.spins AS float) AS bigint) delta_spins,  
	cast (cast(t2.Backoffs AS float) - cast (t1.Backoffs AS float) AS bigint) delta_backoff, 
	DATEDIFF(MI,t1.runtime,t2.runtime) delta_minuntes,
	(cast(cast(t2.spins AS float) - cast(t1.spins AS float) AS bigint) )/DATEDIFF(millisecond,t1.runtime,t2.runtime)/@cpus  spins_per_millisecond_per_CPU
FROM  
	(SELECT row_number () over ( partition by [name]  ORDER BY    runtime) row, *  
	 FROM  [tbl_SPINLOCKSTATS] 
	 WHERE runtime in (SELECT MIN(runtime) FROM  tbl_spinlockstats) ) t1
JOIN  
	(SELECT row_number () over ( partition by [name]  ORDER BY    runtime) row, *  
	 FROM  [tbl_SPINLOCKSTATS]  
	 WHERE runtime in 
	   (SELECT MAX(runtime) 
        FROM  tbl_spinlockstats) ) AS  t2
       ON t1.row = t2.row 
	   AND t1.[name]=t2.[name]
ORDER BY delta_spins DESC
```

### Performance Comparison between two log collections (Slow and Fast for example). 

Using SQL Nexus import two separate SQL LogScout collections into two different databases. Then in the query below replace the database name DB_MO_Slow and DB_MO_Fast with your SQLNexus database names.

```sql
SELECT SlowRun_AvgDuration, FastRun_AvgDuration, 
  SlowRun_AvgDuration - FastRun_AvgDuration  AS SlowRunMinusFastRun_Delta_AvgDuration, 
  SlowRun_AvgCPU, FastRun_AvgCPU, SlowRun_AvgCPU - FastRun_AvgCPU SlowRunMinusFastRun_AvgCPU ,SlowRun_Executions, 
  FastRun_Executions, NormText 
FROM  (
	SELECT top 100 sum(Duration)/1000 Duration, SUM(CPU) CPU, sum(Reads) Reads, COUNT(*) SlowRun_Executions, 
	  (sum(Duration)/1000)/COUNT(*) SlowRun_AvgDuration, sum(CPU)/count(*) SlowRun_AvgCPU 
	  /*, substring(NormText, 1, 120) NormText*/ , t.HashID
	FROM  [DB_MO_Slow].ReadTrace.tblBatches t 
	JOIN  [DB_MO_Slow].ReadTrace.tblUniqueBatches u
	  ON t.HashID = u.HashID
	WHERE u.normtext not like '%SP_MSFOREACHDB%' 
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_SQLDIAG%' 
	  AND u.NormText not like '%repl%'
	  AND u.NormText not like '%distribution%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_SQLAGENT_LOG_JOBHISTORY%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_SQLAGENT_GET_PERF_COUNTERS%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_HELP_JOBSTEP%'
	  AND u.NormText not like 'DECLARE @DBNAME SYSNAME DECLARE @CMD NVARCHAR({##}) DECLARE DB_CURSOR CURSOR FOR SELECT NAME FROM MASTER.DBO.SYSDATABASES %'
	  AND u.NormText not like 'PRINT {STR} PRINT {STR} SELECT LEFT (NAME, {##}) AS NAME, DBID, CMPTLEVEL, CONVERT (INT, (SELECT SUM (CONVERT (BIGINT%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_TRACE%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_SET_BLK_THRESHOLD09%'
	  AND u.NormText not like '%SP_HELPDB%'
	  AND u.NormText not like '%SP_DIAG_TRACE_FLAG%'
	  AND u.NormText not like 'MASTER.DBO.XP_MSVER%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_CODE_RUNNER%'
	  AND u.NormText not like '%DBCC SQLPERF%'
	  AND u.NormText not like '%XP_MSVER%'
	  AND u.NormText not like '%DBCC TRACESTATUS%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_BLOCKER_PSS%'
	  AND u.NormText not like 'USE MASTER;DECLARE @ISREADONLY AS INT; SELECT @ISREADONLY = COUNT(*) FROM SYSDATABASES AS DATABASES%'
	  AND u.NormText not like '%TEMPDB.DBO.TRACEFLAGORIGINALSTATUS%'
	  AND u.NormText not like 'SELECT VALUE FROM MASTER.DBO.SYSCONFIGURES WHERE CONFIG%'
	  AND u.NormText not like '%PRINT {STR}%'
	  AND u.normtext not like '%SP_GET_DISTRIBUTOR%'
	  AND u.normtext not like '%MSGETVERSION%'
	  AND u.normtext not like '%SP_GET_DTSPACKAGE%'
	  AND u.normtext not like '%BACKUPSET%'
	  AND u.NormText not like '%#MSDBFILELIST%'
	  AND u.normtext not like '%SYSALTFILES%'
	  AND u.normtext not like '%SYSDATABASES%'
	  AND u.NormText not like '%SP_MSSQLDMO%'
	  AND u.NormText not like '%CREATE TABLE #ERRORLOG%'
	  AND u.normtext not like '%HASMEMORYSCRIBBLERISSUE%'
	  AND u.normtext not like '%SYSCURCONFIGS%'
	  AND u.normtext not like '%SP_PERF_STATS%'
	  AND u.normtext not like '%FN_TRACE_GETINFO%'
	  AND u.normtext not like '%##MAXNAMEWIDTH%'
	GROUP BY u.NormText, t.HashID
	ORDER BY duration DESC
) slow 
JOIN 
(
	SELECT top 100 sum(Duration)/1000 Duration, SUM(CPU) CPU, sum(Reads) Reads, COUNT(*) FastRun_Executions, 
	  (sum(Duration)/1000)/COUNT(*) FastRun_AvgDuration, sum(CPU)/count(*) FastRun_AvgCPU, 
      substring(NormText, 1, 120) NormText , t.HashID
	FROM  [DB_MO_Fast].ReadTrace.tblBatches t 
	JOIN  [DB_MO_Fast].ReadTrace.tblUniqueBatches u
	  ON t.HashID = u.HashID
	WHERE u.normtext not like '%SP_MSFOREACHDB%' 
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_SQLDIAG%' 
	  AND u.NormText not like '%repl%'
	  AND u.NormText not like '%distribution%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_SQLAGENT_LOG_JOBHISTORY%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_SQLAGENT_GET_PERF_COUNTERS%'
	  AND u.NormText not like 'EXECUTE MSDB.DBO.SP_HELP_JOBSTEP%'
	  AND u.NormText not like 'DECLARE @DBNAME SYSNAME DECLARE @CMD NVARCHAR({##}) DECLARE DB_CURSOR CURSOR FOR SELECT NAME FROM MASTER.DBO.SYSDATABASES %'
	  AND u.NormText not like 'PRINT {STR} PRINT {STR} SELECT LEFT (NAME, {##}) AS NAME, DBID, CMPTLEVEL, CONVERT (INT, (SELECT SUM (CONVERT (BIGINT%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_TRACE%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_SET_BLK_THRESHOLD09%'
	  AND u.NormText not like '%SP_HELPDB%'
	  AND u.NormText not like '%SP_DIAG_TRACE_FLAG%'
	  AND u.NormText not like 'MASTER.DBO.XP_MSVER%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_CODE_RUNNER%'
	  AND u.NormText not like '%DBCC SQLPERF%'
	  AND u.NormText not like '%XP_MSVER%'
	  AND u.NormText not like '%DBCC TRACESTATUS%'
	  AND u.NormText not like 'EXEC TEMPDB.DBO.SP_BLOCKER_PSS%'
	  AND u.NormText not like 'USE MASTER;DECLARE @ISREADONLY AS INT; SELECT @ISREADONLY = COUNT(*) FROM SYSDATABASES AS DATABASES%'
	  AND u.NormText not like '%TEMPDB.DBO.TRACEFLAGORIGINALSTATUS%'
	  AND u.NormText not like 'SELECT VALUE FROM MASTER.DBO.SYSCONFIGURES WHERE CONFIG%'
	  AND u.NormText not like '%PRINT {STR}%'
	  AND u.normtext not like '%SP_GET_DISTRIBUTOR%'
	  AND u.normtext not like '%MSGETVERSION%'
	  AND u.normtext not like '%SP_GET_DTSPACKAGE%'
	  AND u.normtext not like '%BACKUPSET%'
	  AND u.NormText not like '%#MSDBFILELIST%'
	  AND u.normtext not like '%SYSALTFILES%'
	  AND u.normtext not like '%SYSDATABASES%'
	  AND u.NormText not like '%SP_MSSQLDMO%'
	  AND u.NormText not like '%CREATE TABLE #ERRORLOG%'
	  AND u.normtext not like '%HASMEMORYSCRIBBLERISSUE%'
	  AND u.normtext not like '%SYSCURCONFIGS%'
	  AND u.normtext not like '%SP_PERF_STATS%'
	  AND u.normtext not like '%FN_TRACE_GETINFO%'
	  AND u.normtext not like '%##MAXNAMEWIDTH%'
	GROUP BY u.NormText, t.HashID
	ORDER BY Duration DESC
) fast
ON slow.hashid = fast.hashid
ORDER BY SlowRunMinusFastRun_Delta_AvgDuration asc
```
### CPU used by SQL Server queries as percentage of total CPU capacity

```sql
DECLARE @collection_dur int, @cpu_count int, @cpu_usedby_sql decimal(10,4)

--calculate the total collection duration
SELECT @collection_dur = DATEDIFF(MINUTE, MIN(tb.starttime), MAX(tb.EndTime)) 
FROM  ReadTrace.tblBatches tb

--get the count of CPUs
SELECT @cpu_count = cpu_count 
FROM  tbl_SYSINFO

--calculate the total CPU used by SQL Server in all completed batches
SELECT @cpu_usedby_sql= SUM(cpu)/60000  
FROM  ReadTrace.tblBatches

--What percentage of total CPU capacity was used by SQL Server
SELECT convert (decimal(10,4), (@cpu_usedby_sql/(@cpu_count * @collection_dur) ) )*100 
  AS [CPU from Queries as Percent of Total CPU Capacity]
```

### Calculate the cumulative CPU usage of the top 100 queries as percent of CPU capacity

```sql
DECLARE @collection_dur1 int, @cpu_count1 int

--calculate the total collection duration
SELECT @collection_dur1 = DATEDIFF(MINUTE, MIN(tb.starttime), MAX(tb.EndTime)) 
FROM  ReadTrace.tblBatches tb

--get the count of CPUs
SELECT @cpu_count1 = cpu_count 
FROM  tbl_SYSINFO


--calculate CPU used by top X queries
DECLARE @top_cpu_qrs decimal(10,4)

SELECT @top_cpu_qrs = SUM(cpu)/60000.00  
FROM   
(SELECT top 100  sum(Duration)/1000 Duration, SUM(CPU) CPU, sum(Reads) Reads, 
  COUNT(*) Executions, (sum(Duration)/1000)/COUNT(*) AvgDuration, sum(CPU)/count(*) AvgCPU, 
  substring(NormText, 1, 100) NormText 
FROM  ReadTrace.tblBatches t 
	JOIN  ReadTrace.tblUniqueBatches u
	ON t.HashID = u.HashID
GROUP BY u.NormText
ORDER BY CPU DESC
) AS t
SELECT @top_cpu_qrs CPU_in_minutes
--What percentage of total CPU capacity was used by SQL Server
SELECT convert (decimal(10,4), (@top_cpu_qrs/(@cpu_count1 * @collection_dur1) ) )*100 PercentSQLCPU_of_TotalCPUCapacity
```

### Pulling query plan from SQLLogScout (if captured) for a specific statement

Replace <stmt_seq> with a value you pulled from ReadTrace.tblStatements

```sql
SELECT a.[StmtSeq]
      ,b.[EstimateRows]
      ,ROUND(b.[Rows]/(b.[Executes]+0.000000000001),3) AS [RowsPerExec]
      ,b.[Rows]
      ,b.[Executes]
      ,c.[StmtText]
      ,c.[StmtID]
      ,c.[NodeID]
      ,c.[Parent]
      ,c.[PhysicalOp]
      ,c.[LogicalOp]
      ,c.[Argument]
      ,c.[DefinedValues]
      ,b.[EstimateRows]
      ,c.[EstimateIO]
      ,c.[EstimateCPU]
      ,c.[AvgRowSize]
      ,c.[TotalSubtreeCost]
      ,c.[OutputList]
      ,c.[Warnings]
      ,c.[Type]
      ,c.[Parallel]
      ,b.[EstimateExecutes]
FROM  readtrace.tblPlans a 
JOIN  readtrace.tblPlanRows b ON a.seq = b.seq
JOIN  readtrace.tblUniquePlanRows c 
  ON a.PlanHashId = c.PlanHashId AND b.RowOrder = c.RowOrder
WHERE a.stmtseq = <stmt_seq>
ORDER BY b.roworder asc

--Transforming XML Plan to Legacy
exec msdb.dbo.[usp_TransformShowplanXMLToLegacyShowplan] N'<paste your XML showplan here>'
```

### Statistics information about a particular database

```sql
SELECT Database_Id,Database_Name ,Object_Name ,object_id ,stats_id ,
       last_updated ,rows ,rows_sampled ,steps ,unfiltered_rows ,
	   modification_counter ,persisted_sample_percent
FROM dbo.tbl_dm_db_stats_properties
WHERE Database_Name not in ('msdb', 'master', 'model', 'tempdb')
		AND Database_Name = '<YourDBName>'
ORDER BY last_updated asc



```

### SQL CPU Usage on the system over time (uses Perfmon data)

```sql

-- this ANSI setting is to allow WHERE value = NULL type of syntax in case @inst_index is NULL
SET ANSI_NULLS OFF;

IF (
       (OBJECT_ID('dbo.tbl_ServerProperties') IS NOT NULL)
       AND (OBJECT_ID('dbo.CounterData') IS NOT NULL)
   )
BEGIN
    DECLARE @process_id INT = 0,
            @cpu_count INT,
            @inst_name VARCHAR(64),
            @inst_index INT;

    SELECT @process_id = sp.PropertyValue
    FROM dbo.tbl_ServerProperties sp
    WHERE sp.PropertyName = 'ProcessID';


    SELECT @cpu_count = CASE
                            WHEN sp.PropertyValue = 0 THEN
                                1
                            ELSE
                                sp.PropertyValue
                        END
    FROM dbo.tbl_ServerProperties sp
    WHERE sp.PropertyName = 'cpu_count';

    --get processID of SQL assumes that the instance was not restarted during data collection and preserved its PID

    SELECT TOP 1
           @inst_name = cdet.InstanceName,
           @inst_index = cdet.InstanceIndex
    FROM dbo.CounterData ctr
        JOIN dbo.CounterDetails cdet
            ON ctr.CounterID = cdet.CounterID
    WHERE cdet.ObjectName = 'Process'
          AND cdet.CounterName LIKE 'ID Process'
          AND cdet.InstanceName LIKE 'sqlservr%'
          AND ctr.CounterValue = @process_id;

    --combine SQL and OS counter data into a data set by using a join 

    SELECT sql_cpu.CounterDateTime AS EventTime,
           sql_cpu.RecordIndex AS record_id,
           os_cpu.system_idle_cpu,
           CASE
               WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization THEN
                   os_cpu.total_cpu_utilization
               ELSE
                   sql_cpu.sql_cpu_utilization
           END AS sql_cpu_utilization,
           os_cpu.total_cpu_utilization - (CASE
                                        WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization THEN
                                            os_cpu.total_cpu_utilization
                                        ELSE
                                            sql_cpu.sql_cpu_utilization
                                    END
                                   ) AS nonsql_cpu_utilization
    FROM
    (
        -- get SQL CPU for the imported instance
        SELECT ctr.CounterDateTime,
               ctr.RecordIndex,
               CONVERT(INT, (FLOOR(ctr.CounterValue) / (100 * @cpu_count)) * 100) AS sql_cpu_utilization,
               det.InstanceName,
               det.InstanceIndex
        FROM dbo.CounterData ctr
            JOIN dbo.CounterDetails det
                ON ctr.CounterID = det.CounterID
        WHERE det.ObjectName = 'Process'
              AND det.CounterName LIKE '[%] Processor Time'
              AND det.InstanceName = @inst_name
              AND det.InstanceIndex = @inst_index
    ) AS sql_cpu
        INNER JOIN
        (
            SELECT ctr.CounterDateTime,
                   ctr.RecordIndex,
                   FLOOR(ctr.CounterValue) AS total_cpu_utilization,
                   100 - FLOOR(ctr.CounterValue) AS system_idle_cpu
            FROM dbo.CounterData ctr
                JOIN dbo.CounterDetails det
                    ON ctr.CounterID = det.CounterID
            WHERE det.ObjectName = 'Processor Information'
                  AND det.CounterName LIKE '[%] Processor Time'
                  AND det.InstanceName = '_Total'
        ) AS os_cpu
            ON sql_cpu.RecordIndex = os_cpu.RecordIndex;

END;
```


### Loaded modules inside SQL Server (non-Microsoft)

```sql

--loaded modules
SELECT name,*
FROM [dbo].[tbl_dm_os_loaded_modules]
WHERE company NOT LIKE '%Microsoft%'
```

### Hash warnings analysis: queries that produced hash warnings 

```sql
SELECT ts.subclass_name AS [Hash Warning type]
             ,count(a.seq) AS [# Hash Warning events]
             ,substring(c.Origtext,1,3000) AS [Query Text]
FROM readtrace.tblInterestingEvents a
       INNER JOIN readtrace.tblBatches b ON a.Session = b.Session AND a.DBID = b.DBID 
	     AND a.Seq BETWEEN b.StartSeq AND b.EndSeq
       INNER JOIN readtrace.tblUniqueBatches c ON b.HashID = c.HashID
       INNER JOIN sys.trace_events te ON te.trace_event_id = a.EventID
       LEFT OUTER JOIN sys.trace_subclass_values ts 
	     ON ts.trace_event_id = te.trace_event_id 
	       AND ts.subclass_value=a.EventSubclass
WHERE te.name = 'Hash Warning'
GROUP BY substring(c.Origtext,1,3000)
             ,ts.subclass_name
ORDER BY [# Hash Warning events] DESC
```

### Sort warnings analysis:  queries that produced sort warnings

```sql
SELECT ts.subclass_name AS [Sort Warning type]
             ,count(a.seq) AS [# Sort Warning events]
             ,substring(c.Origtext,1,3000) AS [Query Text]
FROM readtrace.tblInterestingEvents a
       INNER JOIN readtrace.tblBatches b ON a.Session = b.Session 
	     AND a.DBID = b.DBID 
	     AND a.Seq BETWEEN b.StartSeq AND b.EndSeq
       INNER JOIN readtrace.tblUniqueBatches c ON b.HashID = c.HashID
       INNER JOIN sys.trace_events te ON te.trace_event_id = a.EventID
       LEFT OUTER JOIN sys.trace_subclass_values ts 
	     ON ts.trace_event_id = te.trace_event_id 
		   AND ts.subclass_value=a.EventSubclass
WHERE te.name = 'Sort Warnings'
GROUP BY substring(c.Origtext,1,3000)
             ,ts.subclass_name
ORDER BY [# Sort Warning events] DESC
```

### Cross Join analysis: batches that encountered Missing Join Predicate errors

```sql
SELECT  count(a.seq) AS [# Cross Join Warning events]
        ,substring(c.Origtext,1,3000) AS [Query Text]
FROM readtrace.tblInterestingEvents a
       INNER JOIN readtrace.tblBatches b ON a.Session = b.Session 
         AND a.DBID = b.DBID 
         AND a.Seq BETWEEN b.StartSeq AND b.EndSeq
       INNER JOIN readtrace.tblUniqueBatches c ON b.HashID = c.HashID
       INNER JOIN sys.trace_events te ON te.trace_event_id = a.EventID
       LEFT OUTER JOIN sys.trace_subclass_values ts 
	     ON ts.trace_event_id = te.trace_event_id 
		   AND ts.subclass_value=a.EventSubclass
WHERE te.name = 'Missing Join Predicate'
GROUP BY substring(c.Origtext,1,3000)
ORDER BY [# Cross Join Warning events] DESC
```

### Queries that encountered various Errors

```sql
SELECT        SUBSTRING(a.TextData, 1,50)  [Exception message]
             ,SUBSTRING(c.Origtext,1,3000) AS [Query Text]
FROM readtrace.tblInterestingEvents a
       INNER JOIN readtrace.tblBatches b ON a.Session = b.Session  
	     AND a.DBID = b.DBID  
		 AND a.Seq BETWEEN b.StartSeq  AND b.EndSeq
       INNER JOIN readtrace.tblUniqueBatches c ON b.HashID = c.HashID
       INNER JOIN sys.trace_events te ON te.trace_event_id = a.EventID
       LEFT OUTER JOIN sys.trace_subclass_values ts 
	     ON ts.trace_event_id = te.trace_event_id  
	       AND ts.subclass_value=a.EventSubclass
WHERE te.name = 'Exception'
  AND isnull(a.Error,0) not in ( 208 -- Missing objects
                                 ,7969  -- No active open transactions (DBCC OPENTRAN)
                                 ,2812  -- Could not find Stored Procedure
                                 ,2714   -- Object already exists
             )      
```

### Missing Indexes on the system 

These are missing index since the start of SQL Server

```sql
IF OBJECT_ID ('tbl_MissingIndexes') IS NOT NULL
BEGIN
	DECLARE @max_datetime DATETIME
	SELECT @max_datetime = MAX(runtime) FROM tbl_MissingIndexes

	SELECT TOP 30 create_index_statement, improvement_measure, user_seeks, user_scans, runtime, object_id 
	FROM tbl_MissingIndexes
	WHERE runtime = @max_datetime
	ORDER BY improvement_measure DESC
END
```

### Memory Clerks memory distribution on SQL Server

Get the distribution of memory usage per memory clerks inside SQL Server

```sql
IF (OBJECT_ID ('tbl_DM_OS_MEMORY_CLERKS') IS NOT NULL)
BEGIN
SELECT 
    type AS clerk_type,
    SUM(pages_kb) AS total_size_kb,
    SUM(pages_kb) / 1024 AS total_size_mb
FROM tbl_DM_OS_MEMORY_CLERKS 
GROUP BY type
ORDER BY total_size_kb DESC;
END
```

### Memory grant (sort and hash operations) waits captured

```sql

IF (OBJECT_ID ('tbl_dm_exec_query_memory_grants') IS NOT NULL 
	AND OBJECT_ID ('tbl_NOTABLEACTIVEQUERIES') IS NOT NULL )
BEGIN
     SELECT  q.runtime, q.session_id, grant_time, requested_memory_kb, 
             granted_memory_kb, required_memory_kb, used_memory_kb, query_cost,  
             g.dop, timeout_sec, q.stmt_text, q.procname
     FROM tbl_dm_exec_query_memory_grants g
       LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES q
       ON g.plan_handle = q.plan_handle
     ORDER BY granted_memory_kb desc
END
``` 



### Compilations and recompilations in SQL Server

Find out the rate of compilations and re-compilations in SQL Server when you resolve high CPU issues

```sql
IF (OBJECT_ID ('CounterDetails') IS NOT NULL)
BEGIN
     SELECT 
             ctr.RecordIndex,
             ctr.CounterDateTime,
             det.ObjectName,
             det.CounterName,
             ctr.CounterValue
     FROM dbo.CounterData ctr
         JOIN dbo.CounterDetails det
             ON ctr.CounterID = det.CounterID
     WHERE det.CounterName IN ('SQL Compilations/sec', 'SQL Re-Compilations/sec')
     		AND ObjectName = 'SQLServer:SQL Statistics'
     ORDER BY RecordIndex
END
```


### Always On Availability Group replica health

Review the health state of AG replicas captured during data collection

```sql
IF OBJECT_ID('tbl_dm_hadr_availability_replica_states') IS NOT NULL
BEGIN
    SELECT 
        ag.name AS availability_group,
        ar.replica_server_name,
        ars.role_desc,
        ars.operational_state_desc,
        ars.connected_state_desc,
        ars.synchronization_health_desc,
        ars.last_connect_error_number,
        ars.last_connect_error_description,
        ars.last_connect_error_timestamp
    FROM tbl_dm_hadr_availability_replica_states ars
    JOIN tbl_availability_replicas ar ON ars.replica_id = ar.replica_id
    JOIN tbl_availability_groups ag ON ar.group_id = ag.group_id
    ORDER BY ag.name, ar.replica_server_name
END
```

### TempDB space usage by session

Identify sessions consuming the most TempDB space

```sql
IF OBJECT_ID('tbl_dm_db_session_space_usage') IS NOT NULL
BEGIN
    SELECT TOP 20
        session_id,
        user_objects_alloc_page_count * 8 AS user_objects_kb,
        internal_objects_alloc_page_count * 8 AS internal_objects_kb,
        (user_objects_alloc_page_count + internal_objects_alloc_page_count) * 8 AS total_kb
    FROM tbl_dm_db_session_space_usage
    ORDER BY total_kb DESC
END
```

### Latch contention analysis

Identify latch types with highest wait times — useful when investigating contention beyond lock waits

```sql
IF OBJECT_ID('tbl_dm_os_latch_stats') IS NOT NULL
BEGIN
    SELECT TOP 20
        latch_class,
        wait_time_ms,
        waiting_requests_count,
        CASE WHEN waiting_requests_count = 0 THEN 0
             ELSE wait_time_ms / waiting_requests_count 
        END AS avg_wait_time_ms
    FROM tbl_dm_os_latch_stats
    WHERE latch_class NOT LIKE 'SOS_%'
    ORDER BY wait_time_ms DESC
END
```

### Historical top queries by CPU, reads, and elapsed time (from DMV snapshots)

These are captured from sys.dm_exec_query_stats snapshots, independent of trace/XEL data

```sql
-- Top CPU queries
IF OBJECT_ID('tbl_Hist_Top10_CPU_Queries_ByQueryHash') IS NOT NULL
    SELECT TOP 20 * FROM tbl_Hist_Top10_CPU_Queries_ByQueryHash 
    ORDER BY total_worker_time DESC

-- Top logical reads queries
IF OBJECT_ID('tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash') IS NOT NULL
    SELECT TOP 20 * FROM tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash 
    ORDER BY total_logical_reads DESC

-- Top elapsed time queries
IF OBJECT_ID('tbl_Hist_Top10_ElapsedTime_Queries_ByQueryHash') IS NOT NULL
    SELECT TOP 20 * FROM tbl_Hist_Top10_ElapsedTime_Queries_ByQueryHash 
    ORDER BY total_elapsed_time DESC
```

### Query Store: top queries by average CPU

Useful when Query Store data was collected via SQL LogScout

```sql
IF OBJECT_ID('tbl_query_store_runtime_stats') IS NOT NULL
   AND OBJECT_ID('tbl_query_store_query_text') IS NOT NULL
   AND OBJECT_ID('tbl_query_store_plan') IS NOT NULL
BEGIN
    SELECT TOP 20
        qt.query_sql_text,
        rs.count_executions,
        rs.avg_cpu_time / 1000.0 AS avg_cpu_ms,
        rs.avg_duration / 1000.0 AS avg_duration_ms,
        rs.avg_logical_io_reads,
        rs.avg_rowcount,
        p.query_plan
    FROM tbl_query_store_runtime_stats rs
    JOIN tbl_query_store_plan p ON rs.plan_id = p.plan_id
    JOIN tbl_query_store_query q ON p.query_id = q.query_id
    JOIN tbl_query_store_query_text qt ON q.query_text_id = qt.query_text_id
    ORDER BY avg_cpu_ms DESC
END
```

### Power plan configuration

An incorrect power plan (e.g. Balanced instead of High Performance) can significantly impact SQL Server performance

```sql
IF OBJECT_ID('tbl_PowerPlan') IS NOT NULL
BEGIN
    SELECT * FROM tbl_PowerPlan
END
```


### Most frequent ERRORLOG errors

```sql
IF OBJECT_ID('tbl_ERRORLOG') IS NOT NULL
BEGIN
    SELECT ErrorNumber,
           COUNT(*) AS Occurrences,
           MIN(LogDateTime) AS FirstSeen,
           MAX(LogDateTime) AS LastSeen,
           MIN(SUBSTRING([Message], 1, 300)) AS SampleMessage
    FROM tbl_ERRORLOG
    WHERE ErrorNumber IS NOT NULL
    GROUP BY ErrorNumber
    ORDER BY Occurrences DESC
END
```

### Search the ERRORLOG for a specific condition

Useful for spotting I/O stalls, memory dumps, long checkpoints, failovers, and login failures.

```sql
IF OBJECT_ID('tbl_ERRORLOG') IS NOT NULL
BEGIN
    SELECT LogDateTime, Process, ErrorNumber, [Message]
    FROM tbl_ERRORLOG
    WHERE [Message] LIKE '%longer than 15 seconds%'    -- slow I/O
       OR [Message] LIKE '%Stack Dump%'
       OR [Message] LIKE '%non-yielding%'
       OR [Message] LIKE '%Login failed%'
       OR [Message] LIKE '%FlushCache%'
    ORDER BY LogDateTime
END
```

### SQL Assessment API results

Best-practice checks captured by the collector, ordered by severity.

```sql
IF OBJECT_ID('tbl_assessment_api') IS NOT NULL
BEGIN
    SELECT [Check], Severity, [Message], HelpURL
    FROM tbl_assessment_api
    ORDER BY CASE Severity
                 WHEN 'Critical' THEN 1
                 WHEN 'High'     THEN 2
                 WHEN 'Medium'   THEN 3
                 WHEN 'Low'      THEN 4
                 ELSE 5
             END, [Check]
END
```

### Disk volume information

Check the NTFS allocation unit (block) size and free space of the volumes hosting SQL Server
data and log files. A block size other than 64 KB for data volumes is a common misconfiguration.

```sql
IF OBJECT_ID('tbl_disk_volume_information') IS NOT NULL
BEGIN
    SELECT Volume_Name,
           Volume_Label,
           File_System,
           Block_Size,
           Capacity_GB,
           FreeSpace_GB,
           CONVERT(DECIMAL(5,2), (FreeSpace_GB / NULLIF(Capacity_GB, 0)) * 100) AS FreePercent,
           Boot_Volume,
           System_Volume,
           Compression
    FROM tbl_disk_volume_information
    ORDER BY FreePercent
END
```

### Running (non-Microsoft) device and filter drivers

Third-party storage filter drivers and antivirus drivers are a frequent cause of unexplained
I/O latency and instability.

```sql
IF OBJECT_ID('tbl_running_drivers') IS NOT NULL
BEGIN
    SELECT File_Name,
           Company_Name,
           File_Description,
           Product_Version,
           File_Version,
           File_Size_MB,
           Last_Write_Time
    FROM tbl_running_drivers
    WHERE Company_Name NOT LIKE '%Microsoft%'
    ORDER BY Company_Name, File_Name
END
```

### Memory health history

`sys.dm_os_memory_health_history` (SQL Server 2022 and later) records periodic memory-pressure
snapshots, including out-of-memory events and memory grant timeouts.

```sql
IF OBJECT_ID('tbl_dm_os_memory_health_history') IS NOT NULL
BEGIN
    SELECT snapshot_time,
           severity_level,
           severity_level_desc,
           allocation_potential_memory_mb,
           reclaimable_cache_memory_mb,
           clerk_type,
           pages_allocated_kb / 1024 AS pages_allocated_mb,
           out_of_memory_event_count,
           memgrant_timeout_count,
           memgrant_waiter_count
    FROM tbl_dm_os_memory_health_history
    ORDER BY snapshot_time
END
```

Show only the snapshots where SQL Server reported memory pressure:

```sql
IF OBJECT_ID('tbl_dm_os_memory_health_history') IS NOT NULL
BEGIN
    SELECT snapshot_time, severity_level_desc, clerk_type,
           out_of_memory_event_count, memgrant_timeout_count, memgrant_waiter_count
    FROM tbl_dm_os_memory_health_history
    WHERE severity_level > 0
       OR out_of_memory_event_count > 0
       OR memgrant_timeout_count > 0
    ORDER BY snapshot_time
END
```

### In-Memory OLTP checkpoint file usage

Diagnose runaway In-Memory OLTP storage growth by looking at checkpoint file counts and sizes
per database and file state.

```sql
IF OBJECT_ID('tbl_dm_db_xtp_checkpoint_files') IS NOT NULL
BEGIN
    SELECT runtime,
           dbname,
           file_type_desc,
           state_desc,
           COUNT(*) AS file_count,
           SUM(file_size_in_bytes) / 1024 / 1024 AS total_size_mb,
           SUM(file_size_used_in_bytes) / 1024 / 1024 AS used_size_mb
    FROM tbl_dm_db_xtp_checkpoint_files
    GROUP BY runtime, dbname, file_type_desc, state_desc
    ORDER BY runtime, dbname, total_size_mb DESC
END
```

### Connection encryption and authentication scheme

`tbl_dm_exec_connections` shows how clients are connecting — transport, encryption, auth scheme
and packet size — which is useful when diagnosing TLS overhead or unexpected protocol usage.

```sql
IF OBJECT_ID('tbl_dm_exec_connections') IS NOT NULL
BEGIN
    SELECT net_transport,
           protocol_type,
           encrypt_option,
           auth_scheme,
           net_packet_size,
           COUNT(DISTINCT session_id) AS session_count
    FROM tbl_dm_exec_connections
    GROUP BY net_transport, protocol_type, encrypt_option, auth_scheme, net_packet_size
    ORDER BY session_count DESC
END
```

Chatty connections (high read/write counts) are often a sign of excessive round trips:

```sql
IF OBJECT_ID('tbl_dm_exec_connections') IS NOT NULL
BEGIN
    SELECT TOP 25
           runtime, session_id, client_net_address, client_tcp_port,
           num_reads, num_writes, connect_time, last_read, last_write
    FROM tbl_dm_exec_connections
    ORDER BY num_reads + num_writes DESC
END
```

### SQL connectivity and security configuration

SQL LogScout connectivity collections import the host's TLS protocols and cipher suites, installed
SQL client drivers, client network settings, SPNs, SQL Server service accounts, aliases, and
certificates. These tables help diagnose encryption, Kerberos, certificate, and client-driver
configuration problems.

Review enabled TLS protocols and the configured cipher-suite order:

```sql
IF OBJECT_ID('tbl_tls_ssl_protocols') IS NOT NULL
BEGIN
    SELECT Protocol, Role, RegEnabled, RegDisabledByDefault,
           EffectiveState, Source, RegistryPath
    FROM tbl_tls_ssl_protocols
    ORDER BY Protocol, Role
END

IF OBJECT_ID('tbl_tls_ssl_cipher_suites') IS NOT NULL
BEGIN
    SELECT Ordinal, CipherSuite, KeyExchange, Cipher, Hash, Source, RegistryPath
    FROM tbl_tls_ssl_cipher_suites
    ORDER BY TRY_CONVERT(INT, Ordinal), CipherSuite
END
```

Check installed SQL client drivers and their SNI network settings:

```sql
IF OBJECT_ID('tbl_sql_client_drivers') IS NOT NULL
BEGIN
    SELECT Name, Type, Version, TLS12, TLS13, MSF, Bitness, Path
    FROM tbl_sql_client_drivers
    ORDER BY Name, Bitness
END

IF OBJECT_ID('tbl_sql_client_driver_sni_settings') IS NOT NULL
BEGIN
    SELECT ClientName, Architecture, ProtocolOrder, ForceEncryption,
           TrustServerCert, TcpDefaultPort, KeepAliveInterval_ms,
           KeepAliveRetryInterval_ms
    FROM tbl_sql_client_driver_sni_settings
    ORDER BY ClientName, Architecture
END
```

Find duplicate, missing, or incorrectly owned SQL Server SPNs:

```sql
IF OBJECT_ID('tbl_sql_spn_registered') IS NOT NULL
BEGIN
    SELECT Account, SPN, Type, Duplicate
    FROM tbl_sql_spn_registered
    ORDER BY Duplicate DESC, Account, SPN
END

IF OBJECT_ID('tbl_sql_spn_suggested') IS NOT NULL
BEGIN
    SELECT Instance, SuggestedSPN, ExpectedAccount, Status
    FROM tbl_sql_spn_suggested
    ORDER BY Instance, Status, SuggestedSPN
END

IF OBJECT_ID('tbl_sql_spn_service_accounts') IS NOT NULL
BEGIN
    SELECT ServiceName, ServiceAccountName, SpnAccount, State
    FROM tbl_sql_spn_service_accounts
    ORDER BY ServiceName
END
```

Inspect SQL Server certificates and private-key access. Pay particular attention to expired
certificates, missing private keys, and certificates that the SQL Server service account cannot
access.

```sql
IF OBJECT_ID('tbl_sql_server_certificates') IS NOT NULL
BEGIN
    SELECT CertID, Thumbprint, Subject, SubjectAltNames, ServerCert,
           NotBefore, NotAfter, Validity, HasPrivKey, AllowAce, SqlUsed,
           KeyContainerFile
    FROM tbl_sql_server_certificates
    ORDER BY NotAfter, Subject
END

IF OBJECT_ID('tbl_sql_server_certificate_key_acl') IS NOT NULL
BEGIN
    SELECT CertID, Thumbprint, [Identity], Access, Rights
    FROM tbl_sql_server_certificate_key_acl
    ORDER BY CertID, [Identity]
END
```

### File I/O stats

The `tbl_FileStats` rowset identifier was corrected in this release, so file-level I/O data now
imports reliably. Use it to find the files with the highest average stall per read/write.

```sql
IF OBJECT_ID('tbl_FileStats') IS NOT NULL
BEGIN
    SELECT TOP 25 *
    FROM tbl_FileStats
    ORDER BY runtime DESC
END
```
