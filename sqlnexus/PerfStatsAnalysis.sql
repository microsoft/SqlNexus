PRINT 'Current database: ' + DB_NAME()
GO
SET NOCOUNT ON
SET QUOTED_IDENTIFIER ON
GO

-- TODO: store schema ver for possible upgrade tasks
IF '%runmode%' != 'REALTIME' AND OBJECT_ID ('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL 
BEGIN
  DECLARE @firstruntime datetime, @lastruntime datetime
  SELECT @firstruntime = MIN (runtime), @lastruntime = MAX (runtime) FROM tbl_RUNTIMES (NOLOCK)
  PRINT '=============================================='
  PRINT '        Perf Stats Script Analysis            '
  PRINT '=============================================='
  PRINT ' Script Exec Duration: ' + CONVERT (varchar, DATEDIFF (mi, @firstruntime, @lastruntime)) + ' minutes'
  PRINT '    Script Start Time: ' + CONVERT (varchar, @firstruntime, 120) 
  PRINT '      Script End Time: ' + CONVERT (varchar, @lastruntime, 120) 
  IF OBJECT_ID ('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL 
    SELECT RIGHT (REPLICATE (' ', 24) + LEFT ([Name], 20), 21) + ':', LEFT ([Value], 60) FROM tbl_SCRIPT_ENVIRONMENT_DETAILS 
    WHERE script_name = 'SQL 2005 Perf Stats Script'
  RAISERROR ('', 0, 1) WITH NOWAIT
END
GO

/* ========== BEGIN TABLE CREATES (realtime only) ========== */
-- DROP TABLE tbl_RUNTIMES
IF '%runmode%' = 'REALTIME' AND OBJECT_ID ('tbl_RUNTIMES') IS NULL
  CREATE TABLE [dbo].[tbl_RUNTIMES](
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [source_script] [varchar](80) NULL
  ) 
GO

IF '%runmode%' = 'REALTIME' AND OBJECT_ID ('tbl_BLOCKING_CHAINS') IS NULL
  CREATE TABLE [dbo].[tbl_BLOCKING_CHAINS](
    [first_rownum] [bigint] NULL,
    [last_rownum] [bigint] NULL,
    [num_snapshots] [int] NULL,
    [blocking_start] [datetime] NULL,
    [blocking_end] [datetime] NULL,
    [head_blocker_session_id] int NULL, 
    [blocking_wait_type] varchar(40) NULL, 
    [max_blocked_task_count] int NULL,
    [max_total_wait_duration_ms] [bigint] NULL,
    [avg_wait_duration_ms] [bigint] NULL,
    [max_wait_duration_ms] [bigint] NULL,
    [max_blocking_chain_depth] [int] NULL,
    [head_blocker_session_id_orig] [int] NULL
  ) 
GO

-- DROP VIEW vw_HEAD_BLOCKER_SUMMARY
-- DROP TABLE tbl_HEADBLOCKERSUMMARY
--we need to create the object for report to work

IF OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') IS NULL
  CREATE TABLE [dbo].[tbl_HEADBLOCKERSUMMARY](
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [head_blocker_session_id] [int] NULL,
    [blocked_task_count] [int] NULL,
    [tot_wait_duration_ms] [bigint] NULL,
    [blocking_resource_wait_type] varchar(40) NULL, 
    [avg_wait_duration_ms] [bigint] NULL,
    [max_wait_duration_ms] [bigint] NULL,
    [max_blocking_chain_depth] [int] NULL, 
    [head_blocker_proc_name] nvarchar(60), 
    [head_blocker_proc_objid] [int] NULL, 
    [stmt_text] nvarchar(1000) NULL, 
    [head_blocker_plan_handle] varbinary(64) NULL
  )
  
GO
-- DROP TABLE tbl_NOTABLEACTIVEQUERIES
IF  OBJECT_ID ('tbl_NOTABLEACTIVEQUERIES') IS NULL
  CREATE TABLE [dbo].[tbl_NOTABLEACTIVEQUERIES](
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [session_id] [int] NULL,
    [request_id] [int] NULL,
	ecid int null,
    [plan_total_exec_count] [bigint] NULL,
    [plan_total_cpu_ms] [bigint] NULL,
    [plan_total_duration_ms] [bigint] NULL,
    [plan_total_physical_reads] [bigint] NULL,
    [plan_total_logical_writes] [bigint] NULL,
    [plan_total_logical_reads] [bigint] NULL,
    [dbname] [varchar](40) NULL,
    [objectid] [int] NULL,
    [procname] [varchar](60) NULL,
    [plan_handle] varbinary(64) NULL,
	group_id int null,
	statement_start_offset int null,
	statement_end_offset int null,
    [stmt_text] [varchar](2000) NULL,
	plan_generation_num int null,
	creatioin_time datetime null
    --[stmt_text_agg]  AS (replace(replace(case when [stmt_text] IS NULL then NULL when charindex('noexec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],(1),(40)) when charindex('sp_executesql',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(200))),(60)) when charindex('sp_cursoropen',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_cursorprep',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_prepare',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_prepexec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(200))),(80)) when charindex('exec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(4000))),charindex(' ',substring(substring([stmt_text],(1),(200))+'   ',charindex('exec',substring([stmt_text],(1),(500))),(200)),(9))) when substring([stmt_text],(1),(2))='usp' OR substring([stmt_text],(1),(2))='xp' OR substring([stmt_text],(1),(2))='sp' then substring([stmt_text],(1),charindex(' ',substring([stmt_text],(1),(200))+' ')) when substring([stmt_text],(1),(30)) like '%UPDATE %' OR substring([stmt_text],(1),(30)) like '%INSERT %' OR substring([stmt_text],(1),(30)) like '%DELETE %' then substring([stmt_text],(1),(30)) else substring([stmt_text],(1),(45)) end,char((10)),' '),char((13)),' '))
  )
GO
-- DROP TABLE tbl_REQUESTS
IF OBJECT_ID ('tbl_REQUESTS') IS NULL
  CREATE TABLE [dbo].[tbl_REQUESTS](
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [session_id] [int] NULL,
    [request_id] [int] NULL, 
    [ecid] [int] NULL,
    [blocking_session_id] [int] NULL,
    [blocking_ecid] [int] NULL,
    [task_state] [varchar](15) NULL,
    [wait_type] [varchar](50) NULL,
    [wait_duration_ms] [bigint] NULL,
    [wait_resource] [varchar](40) NULL,
    [resource_description] [varchar](120) NULL,
    [last_wait_type] [varchar](50) NULL,
    [open_trans] [int] NULL,
    [transaction_isolation_level] [varchar](30) NULL,
    [is_user_process] [int] NULL,
    [request_cpu_time] [bigint] NULL,
    [request_logical_reads] [bigint] NULL,
    [request_reads] [bigint] NULL,
    [request_writes] [bigint] NULL,
    [memory_usage] [int] NULL,
    [session_cpu_time] [bigint] NULL,
    [session_reads] [bigint] NULL,
    [session_writes] [bigint] NULL,
    [session_logical_reads] [bigint] NULL,
    [total_scheduled_time] [bigint] NULL,
    [total_elapsed_time] [varchar](18) NULL,
    [last_request_start_time] [datetime] NULL,
    [last_request_end_time] [datetime] NULL,
    [session_row_count] [bigint] NULL,
    [prev_error] [int] NULL,
    [open_resultsets] [int] NULL,
    [request_total_elapsed_time] [bigint] NULL,
    [percent_complete] [int] NULL,
    [estimated_completion_time] [bigint] NULL,
    [tran_name] [varchar](32) NULL,
    [transaction_begin_time] [datetime] NULL,
    [tran_type] [varchar](15) NULL,
    [tran_state] [varchar](15) NULL,
    [request_start_time] [datetime] NULL,
    [request_status] [varchar](15) NULL,
    [command] [varchar](16) NULL,
    [statement_start_offset] [int] NULL,
    [statement_end_offset] [int] NULL,
    [database_id] [int] NULL,
    [user_id] [int] NULL,
    [executing_managed_code] [int] NULL,
    [pending_io_count] [int] NULL,
    [login_time] [datetime] NULL,
    [host_name] [varchar](20) NULL,
    [program_name] [varchar](50) NULL,
    [host_process_id] [int] NULL,
    [client_version] [varchar](14) NULL,
    [client_interface_name] [varchar](32) NULL,
    [login_name] [varchar](20) NULL,
    [nt_domain] [varchar](30) NULL,
    [nt_user_name] [varchar](20) NULL,
    [net_packet_size] [int] NULL,
    [client_net_address] [varchar](40) NULL,
    [session_status] [varchar](15) NULL,
    [scheduler_id] [int] NULL,
    [is_preemptive] [int] NULL,
    [is_sick] [int] NULL,
    [last_worker_exception] [int] NULL,
    [last_exception_address] [varbinary](22) NULL,
    [os_thread_id] [int] NULL
  )
GO

IF  OBJECT_ID ('tbl_OS_WAIT_STATS') IS NULL
  CREATE TABLE [dbo].[tbl_OS_WAIT_STATS](
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [wait_type] [varchar](45) NULL,
    [waiting_tasks_count] [bigint] NULL,
    [wait_time_ms] [bigint] NULL,
    [max_wait_time_ms] [bigint] NULL,
    [signal_wait_time_ms] [bigint] NULL
  )
GO
IF  OBJECT_ID ('tbl_SYSPERFINFO') IS NULL
  CREATE TABLE [dbo].[tbl_SYSPERFINFO] (
    [rownum] [bigint] IDENTITY NOT NULL,
    [runtime] [datetime] NULL,
    [object_name] nvarchar(256), 
    counter_name nvarchar(256), 
    instance_name nvarchar(256), 
    cntr_value bigint
  )
GO
IF  OBJECT_ID ('tbl_SQL_CPU_HEALTH') IS NULL
  CREATE TABLE tbl_SQL_CPU_HEALTH (
    rownum bigint IDENTITY NOT NULL, 
    runtime datetime, 
    record_id int, 
    EventTime datetime, 
    system_idle_cpu int, 
    sql_cpu_utilization int
  )
GO
IF  OBJECT_ID ('tbl_FILE_STATS') IS NULL
  CREATE TABLE [dbo].[tbl_FILE_STATS](
    [database] [sysname],
    [file] [nvarchar](260),
    [DbId] [smallint],
    [FileId] [smallint],
    [AvgIOTimeMS] [bigint] NULL,
    [TimeStamp] [int],
    [NumberReads] [bigint],
    [BytesRead] [bigint],
    [IoStallReadMS] [bigint],
    [NumberWrites] [bigint],
    [BytesWritten] [bigint],
    [IoStallWriteMS] [bigint],
    [IoStallMS] [bigint],
    [BytesOnDisk] [bigint],
    [type] [tinyint],
    [type_desc] [nvarchar](60),
    [data_space_id] [int],
    [state] [tinyint],
    [state_desc] [nvarchar](60),
    [size] [int],
    [max_size] [int],
    [growth] [int],
    [is_sparse] [bit],
    [is_percent_growth] [bit] 
  )
GO

-- "Register" perf stats tables for the background purge job
IF '%runmode%' = 'REALTIME' BEGIN
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_RUNTIMES') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_RUNTIMES', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_BLOCKING_CHAINS') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_BLOCKING_CHAINS', 'blocking_end')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_HEADBLOCKERSUMMARY') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_HEADBLOCKERSUMMARY', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_NOTABLEACTIVEQUERIES') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_NOTABLEACTIVEQUERIES', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_REQUESTS') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_REQUESTS', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_OS_WAIT_STATS') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_OS_WAIT_STATS', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_SYSPERFINFO') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_SYSPERFINFO', 'runtime')
  IF NOT EXISTS (SELECT * FROM tbl_NEXUS_PURGE_TABLES WHERE tablename = 'tbl_SQL_CPU_HEALTH') INSERT INTO tbl_NEXUS_PURGE_TABLES VALUES ('tbl_SQL_CPU_HEALTH', 'runtime')
END
GO

-- Compensate for missing sys.dm_os_sys_info in some very old perf stats script output. 
IF OBJECT_ID ('tbl_SYSINFO') IS NULL
BEGIN
  CREATE TABLE [dbo].[tbl_SYSINFO](
    tableinfo varchar(128), 
    cpu_ticks bigint, ms_ticks bigint, cpu_count int, cpu_ticks_in_ms bigint, 
    hyperthread_ratio int, physical_memory_in_bytes bigint, virtual_memory_in_bytes bigint, 
    bpool_committed int, bpool_commit_target int, bpool_visible int, 
    stack_size_in_bytes int, os_quantum bigint, os_error_code int, os_priority_class int, 
    max_workers_count int, schedulers_count smallint, scheduler_total_count int, 
    deadlock_monitor_serial_number int
  )
  EXEC ('INSERT INTO tbl_SYSINFO (tableinfo, cpu_count) 
    VALUES (''This table was created by PerfStatsAnalysis.sql due to missing Perf Stats Script data.'', 2)')
END
GO

-- Compensate for missing tbl_SQL_CPU_HEALTH bug in some perf stats script output
IF OBJECT_ID ('tbl_SQL_CPU_HEALTH') IS NULL 
BEGIN
  CREATE TABLE [dbo].[tbl_SQL_CPU_HEALTH](
    [rownum] int,
    [runtime] [datetime] NOT NULL,
    [record_id] [int] NULL,
    [EventTime] [varchar](30) NULL,
    [timestamp] [bigint] NOT NULL,
    [system_idle_cpu] [int] NULL,
    [sql_cpu_utilization] [int] NULL
  )
END
GO

/* ========== END TABLE CREATES ========== */



/* ========== BEGIN ANALYSIS HELPER OBJECTS ========== */
IF OBJECT_ID ('vw_BLOCKING_HIERARCHY') IS NOT NULL DROP VIEW vw_BLOCKING_HIERARCHY
GO
CREATE VIEW vw_BLOCKING_HIERARCHY AS 
WITH BlockingHierarchy (runtime, head_blocker_session_id, session_id, blocking_session_id, wait_type, 
  wait_duration_ms, wait_resource, resource_description, [Level]) 
AS (
  SELECT head.runtime, head.session_id AS head_blocker_session_id, head.session_id AS session_id, head.blocking_session_id, 
    head.wait_type, head.wait_duration_ms, head.wait_resource, head.resource_description, 0 AS [Level]
  FROM tbl_REQUESTS (NOLOCK) AS head
  WHERE (head.blocking_session_id IS NULL OR head.blocking_session_id = 0) 
    AND head.session_id IN (SELECT blocking_session_id FROM tbl_REQUESTS  (NOLOCK) AS r2 WHERE r2.blocking_session_id <> 0 AND r2.runtime = head.runtime) 
    AND (head.ecid = 0 OR head.ecid IS NULL)
  UNION ALL 
  SELECT h.runtime, h.head_blocker_session_id, blocked.session_id, blocked.blocking_session_id, blocked.wait_type, 
    CASE WHEN blocked.wait_type LIKE 'EXCHANGE%' OR blocked.wait_type LIKE 'CXPACKET%' THEN 0 ELSE blocked.wait_duration_ms END AS wait_duration_ms, 
    blocked.wait_resource, blocked.resource_description, [Level] + 1
  FROM tbl_REQUESTS (NOLOCK) AS blocked
  INNER JOIN BlockingHierarchy AS h ON h.runtime = blocked.runtime AND h.session_id = blocked.blocking_session_id
)
SELECT * FROM BlockingHierarchy 
GO

IF OBJECT_ID ('vw_FIRSTTIERBLOCKINGHIERARCHY') IS NOT NULL DROP VIEW vw_FIRSTTIERBLOCKINGHIERARCHY
GO 
CREATE VIEW vw_FIRSTTIERBLOCKINGHIERARCHY AS 
  WITH BlockingHierarchy (runtime, first_tier_blocked_session_id, session_id, blocking_session_id, wait_type, 
    wait_duration_ms, first_tier_wait_resource, wait_resource, resource_description, [Level]) 
  AS (
    SELECT firsttier.runtime, firsttier.session_id AS first_tier_blocked_session_id, firsttier.session_id AS session_id, firsttier.blocking_session_id, 
      firsttier.wait_type, firsttier.wait_duration_ms, firsttier.wait_resource AS first_tier_wait_resource, 
      firsttier.wait_resource, firsttier.resource_description, 1 AS [Level]
    FROM tbl_REQUESTS (NOLOCK) AS firsttier
    WHERE firsttier.blocking_session_id IN (SELECT session_id FROM tbl_REQUESTS (NOLOCK) AS r2 WHERE r2.blocking_session_id = 0 AND r2.runtime = firsttier.runtime) 
    UNION ALL 
    SELECT h.runtime, h.first_tier_blocked_session_id, blocked.session_id, blocked.blocking_session_id, blocked.wait_type, 
      CASE WHEN blocked.wait_type IN ('EXCHANGE', 'CXPACKET') THEN 0 ELSE blocked.wait_duration_ms END AS wait_duration_ms, 
      h.first_tier_wait_resource, blocked.wait_resource, blocked.resource_description, [Level] + 1
    FROM tbl_REQUESTS (NOLOCK) AS blocked
    INNER JOIN BlockingHierarchy AS h ON h.runtime = blocked.runtime AND h.session_id = blocked.blocking_session_id
  )
  SELECT * FROM BlockingHierarchy 
GO

-- Handle old tbl_RUNTIMES format
IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID ('tbl_RUNTIMES') AND name = 'source_script') 
  ALTER TABLE tbl_RUNTIMES
  ADD source_script varchar(80) NULL
GO

IF OBJECT_ID ('vw_HEAD_BLOCKER_SUMMARY') IS NOT NULL DROP VIEW vw_HEAD_BLOCKER_SUMMARY
GO
-- old 
IF EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'wait_type') 
EXEC ('
  CREATE VIEW vw_HEAD_BLOCKER_SUMMARY WITH SCHEMABINDING AS 
  SELECT rownum, runtime, CASE WHEN wait_type LIKE ''COMPILE%'' THEN ''COMPILE BLOCKING'' ELSE CONVERT (varchar(24), head_blocker_session_id) END AS head_blocker_session_id, 
    blocked_task_count, tot_wait_duration_ms, wait_type AS blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
    head_blocker_session_id AS head_blocker_session_id_orig
  FROM dbo.tbl_HEADBLOCKERSUMMARY (NOLOCK)
')
GO
-- new
IF EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'blocking_resource_wait_type') 
EXEC ('
  CREATE VIEW vw_HEAD_BLOCKER_SUMMARY WITH SCHEMABINDING AS 
  SELECT rownum, runtime, CASE WHEN blocking_resource_wait_type LIKE ''COMPILE%'' THEN ''COMPILE BLOCKING'' ELSE CONVERT (varchar(24), head_blocker_session_id) END AS head_blocker_session_id, 
    blocked_task_count, tot_wait_duration_ms, blocking_resource_wait_type AS blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
    head_blocker_session_id AS head_blocker_session_id_orig
  FROM dbo.tbl_HEADBLOCKERSUMMARY (NOLOCK)
')
GO
CREATE UNIQUE CLUSTERED INDEX cidx ON vw_HEAD_BLOCKER_SUMMARY (runtime, head_blocker_session_id, blocking_wait_type, rownum)
GO

IF OBJECT_ID ('tbl_PERF_STATS_SCRIPT_RUNTIMES') IS NOT NULL DROP TABLE tbl_PERF_STATS_SCRIPT_RUNTIMES
GO
--it used to use tbl_requests. but that table wont' get much data if server is idel. wait stats is gauranteed to generate
SELECT DISTINCT runtime INTO tbl_PERF_STATS_SCRIPT_RUNTIMES FROM tbl_OS_WAIT_STATS ORDER BY runtime
GO

IF OBJECT_ID ('vw_PERF_STATS_SCRIPT_RUNTIMES') IS NOT NULL DROP VIEW vw_PERF_STATS_SCRIPT_RUNTIMES
GO 
CREATE VIEW vw_PERF_STATS_SCRIPT_RUNTIMES AS 
SELECT runtime FROM tbl_PERF_STATS_SCRIPT_RUNTIMES 
--SELECT * FROM tbl_RUNTIMES (NOLOCK) 
--WHERE source_script IN ('SQL 2005 Perf Stats Script', '') OR source_script IS NULL
GO

IF OBJECT_ID ('ufn_REQUEST_DETAILS') IS NOT NULL DROP FUNCTION ufn_REQUEST_DETAILS
GO
CREATE FUNCTION ufn_REQUEST_DETAILS (@start_time datetime, @end_time datetime, @session_id int) RETURNS TABLE AS
RETURN SELECT TOP 1 runtime, session_id, ecid, wait_type, wait_duration_ms, request_status, wait_resource, open_trans, 
  transaction_isolation_level, tran_name, transaction_begin_time, request_start_time, command, resource_description, program_name, 
  [host_name], nt_user_name, nt_domain, login_name, last_request_start_time, last_request_end_time
FROM tbl_REQUESTS 
WHERE runtime >= @start_time AND runtime < @end_time AND session_id = @session_id
ORDER BY CASE WHEN wait_type IN ('EXCHANGE', 'CXPACKET') THEN 1 ELSE 0 END, -- prefer non-parallel waittypes
  wait_duration_ms DESC, -- prefer longer waits
  runtime
GO

IF OBJECT_ID ('ufn_QUERY_DETAILS') IS NOT NULL DROP FUNCTION ufn_QUERY_DETAILS
GO
CREATE FUNCTION ufn_QUERY_DETAILS (@start_time datetime, @end_time datetime, @session_id int) RETURNS TABLE AS
RETURN SELECT TOP 1 runtime, procname, stmt_text, session_id
FROM tbl_NOTABLEACTIVEQUERIES 
WHERE runtime >= @start_time AND runtime < @end_time AND session_id = @session_id AND (stmt_text IS NOT NULL OR procname IS NOT NULL)
ORDER BY runtime
GO

IF '%runmode%' != 'REALTIME' BEGIN
  -- Create tbl_BLOCKING_CHAINS (postmortem analysis mode only -- the realtime data capture proc populates this on-the-fly)
  IF OBJECT_ID ('tempdb.dbo.#head_blk_sum') IS NOT NULL DROP TABLE #head_blk_sum

  SELECT rownum, runtime AS blocking_start, 
    (
      SELECT TOP 1 runtime FROM vw_PERF_STATS_SCRIPT_RUNTIMES run WHERE run.runtime > b.runtime AND NOT EXISTS 
      (SELECT runtime FROM vw_HEAD_BLOCKER_SUMMARY AS b2 WHERE b2.runtime = run.runtime AND b2.head_blocker_session_id = b.head_blocker_session_id AND b2.blocking_wait_type = b.blocking_wait_type)
      ORDER BY run.runtime ASC
    ) AS blocking_end, 
    head_blocker_session_id,  blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
    blocked_task_count, tot_wait_duration_ms, head_blocker_session_id_orig
  INTO #head_blk_sum
  FROM vw_HEAD_BLOCKER_SUMMARY b
  WHERE runtime IS NOT NULL

  -- Set blocking end time to end-of-data-collection for any blocking chains that were still active when data collection stopped
  UPDATE #head_blk_sum SET blocking_end = (SELECT MAX (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES) WHERE blocking_end IS NULL

--   SELECT 
--     b1.rownum, 
--     b2.blocking_start, b2.blocking_end, b1.head_blocker_session_id, b1.blocking_wait_type, 
--     b1.blocked_task_count AS blocked_task_count, b1.tot_wait_duration_ms AS tot_wait_duration_ms, 
--     b1.avg_wait_duration_ms AS avg_wait_duration_ms, b1.max_wait_duration_ms AS max_wait_duration_ms, 
--     b1.max_blocking_chain_depth AS max_blocking_chain_depth, b1.blocked_task_count, b1.tot_wait_duration_ms, b1.head_blocker_session_id_orig
--   FROM vw_HEAD_BLOCKER_SUMMARY b1
--   INNER JOIN #head_blk_sum b2 ON b1.rownum = b2.rownum
--   WHERE NOT EXISTS (
--     SELECT * FROM #head_blk_sum b3 
--     WHERE b3.blocking_start < b2.blocking_start AND b3.blocking_end = b2.blocking_end AND b3.head_blocker_session_id = b2.head_blocker_session_id AND b3.blocking_wait_type = b2.blocking_wait_type)

  IF OBJECT_ID ('tbl_BLOCKING_CHAINS') IS NOT NULL DROP TABLE tbl_BLOCKING_CHAINS;
  WITH BlockingChainsIntermediate (rownum, blocking_start, blocking_end, head_blocker_session_id, blocking_wait_type, blocked_task_count, 
    tot_wait_duration_ms, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, head_blocker_session_id_orig) AS 
  (
    SELECT 
      rownum, 
      (SELECT MIN (blocking_start) FROM #head_blk_sum b3 WHERE b3.head_blocker_session_id = b2.head_blocker_session_id AND b3.blocking_end = b2.blocking_end AND b3.blocking_wait_type = b2.blocking_wait_type) AS blocking_start, 
      b2.blocking_end, b2.head_blocker_session_id, b2.blocking_wait_type, 
      b2.blocked_task_count, b2.tot_wait_duration_ms, 
      b2.avg_wait_duration_ms, b2.max_wait_duration_ms, 
      b2.max_blocking_chain_depth, b2.head_blocker_session_id_orig
    FROM #head_blk_sum b2 
  )
  SELECT 
    MIN (rownum) AS first_rownum, 
    MAX (rownum) AS last_rownum, 
    COUNT(*) AS num_snapshots, 
    blocking_start, blocking_end, head_blocker_session_id, blocking_wait_type, 
    MAX (blocked_task_count) AS max_blocked_task_count, MAX (tot_wait_duration_ms) AS max_total_wait_duration_ms, 
    AVG (avg_wait_duration_ms) AS avg_wait_duration_ms, MAX (max_wait_duration_ms) AS max_wait_duration_ms, 
    MAX (max_blocking_chain_depth) AS max_blocking_chain_depth, MIN (head_blocker_session_id_orig) AS head_blocker_session_id_orig
  INTO tbl_BLOCKING_CHAINS
  FROM BlockingChainsIntermediate
  GROUP BY blocking_end, blocking_start, head_blocker_session_id, blocking_wait_type
END
GO

IF OBJECT_ID ('vw_BLOCKING_CHAINS') IS NOT NULL DROP VIEW vw_BLOCKING_CHAINS
GO
-- CREATE VIEW vw_BLOCKING_CHAINS AS 
-- WITH BlockingPeriods (rownum, blocking_start, blocking_end, head_blocker_session_id, blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, head_blocker_session_id_orig) AS 
-- (
--   SELECT rownum, runtime AS blocking_start, 
--     (
--       SELECT TOP 1 runtime FROM vw_PERF_STATS_SCRIPT_RUNTIMES run WHERE run.runtime > b.runtime AND NOT EXISTS 
--         (SELECT runtime FROM vw_HEAD_BLOCKER_SUMMARY AS b2 WHERE b2.runtime = run.runtime AND b2.head_blocker_session_id = b.head_blocker_session_id AND b2.blocking_wait_type = b.blocking_wait_type)
--       ORDER BY run.runtime ASC
--     ) AS blocking_end, 
--     head_blocker_session_id,  blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
--     head_blocker_session_id_orig
--   FROM vw_HEAD_BLOCKER_SUMMARY b
-- )
-- SELECT p1.*, 
--   CASE 
--     WHEN DATEDIFF (s, blocking_start, blocking_end) >= 20 THEN DATEDIFF (s, blocking_start, blocking_end) 
--     ELSE max_wait_duration_ms / 1000
--   END AS blocking_duration_sec, 
--   (
--     SELECT MAX (blocked_task_count) FROM vw_HEAD_BLOCKER_SUMMARY b3 
--     WHERE b3.head_blocker_session_id = p1.head_blocker_session_id AND b3.blocking_wait_type = p1.blocking_wait_type AND b3.runtime >= p1.blocking_start AND b3.runtime < p1.blocking_end
--   ) AS max_blocked_task_count, 
--   (
--     SELECT MAX (tot_wait_duration_ms) FROM vw_HEAD_BLOCKER_SUMMARY b3 
--     WHERE b3.head_blocker_session_id = p1.head_blocker_session_id  AND b3.blocking_wait_type = p1.blocking_wait_type AND b3.runtime >= p1.blocking_start AND b3.runtime < p1.blocking_end
--   ) AS max_total_wait_duration_ms, 
--   req.runtime AS example_runtime, req.program_name, req.[host_name], req.nt_user_name, req.nt_domain, req.login_name, req.wait_type, 
--   req.wait_duration_ms, req.request_status, req.wait_resource, req.open_trans, req.transaction_isolation_level, req.tran_name, 
--   req.transaction_begin_time, req.request_start_time, req.command, req.resource_description, 
--   last_request_start_time, last_request_end_time, 
--   qry.procname, qry.stmt_text
-- FROM BlockingPeriods p1
-- OUTER APPLY dbo.ufn_REQUEST_DETAILS (p1.blocking_start, p1.blocking_end, p1.head_blocker_session_id_orig) req
-- OUTER APPLY dbo.ufn_QUERY_DETAILS (p1.blocking_start, p1.blocking_end, p1.head_blocker_session_id_orig) qry
-- WHERE NOT EXISTS 
--   (SELECT blocking_start FROM BlockingPeriods AS p2
--   WHERE p2.blocking_start < p1.blocking_start AND p2.blocking_end = p1.blocking_end AND p2.head_blocker_session_id = p1.head_blocker_session_id 
--     AND p2.blocking_wait_type = p1.blocking_wait_type)
CREATE VIEW vw_BLOCKING_CHAINS AS 
SELECT ch.*, 
  CASE 
    WHEN DATEDIFF (s, blocking_start, blocking_end) >= 20 THEN DATEDIFF (s, blocking_start, blocking_end) 
    ELSE max_wait_duration_ms / 1000
  END AS blocking_duration_sec, 
  req.runtime AS example_runtime, req.program_name, req.[host_name], req.nt_user_name, req.nt_domain, req.login_name, req.wait_type, 
  req.wait_duration_ms, req.request_status, req.wait_resource, req.open_trans, req.transaction_isolation_level, req.tran_name, 
  req.transaction_begin_time, req.request_start_time, req.command, req.resource_description, 
  last_request_start_time, last_request_end_time, 
  qry.procname, qry.stmt_text
FROM tbl_BLOCKING_CHAINS ch
OUTER APPLY dbo.ufn_REQUEST_DETAILS (ch.blocking_start, ch.blocking_end, ch.head_blocker_session_id_orig) req
OUTER APPLY dbo.ufn_QUERY_DETAILS (ch.blocking_start, ch.blocking_end, ch.head_blocker_session_id_orig) qry
GO

IF OBJECT_ID ('vw_BLOCKING_PERIODS') IS NOT NULL DROP VIEW vw_BLOCKING_PERIODS
GO 
CREATE VIEW vw_BLOCKING_PERIODS AS 
WITH BlockingPeriodsRaw (blocking_start, blocking_end, max_wait_duration_ms) AS (
  SELECT DISTINCT runtime AS blocking_start, 
    (
      SELECT TOP 1 runtime FROM vw_PERF_STATS_SCRIPT_RUNTIMES run WHERE run.runtime > b.runtime AND NOT EXISTS 
        (SELECT runtime FROM vw_HEAD_BLOCKER_SUMMARY AS b2 WHERE b2.runtime = run.runtime)
      ORDER BY run.runtime ASC
    ) AS blocking_end, max_wait_duration_ms
  FROM vw_HEAD_BLOCKER_SUMMARY b
)
SELECT p1.*, 
  CASE 
    WHEN DATEDIFF (s, blocking_start, blocking_end) >= 20 THEN DATEDIFF (s, blocking_start, blocking_end) 
    ELSE max_wait_duration_ms / 1000
  END AS blocking_duration_sec, 
  (SELECT MAX (blocked_task_count) FROM vw_HEAD_BLOCKER_SUMMARY b3 WHERE b3.runtime >= p1.blocking_start AND b3.runtime < p1.blocking_end) AS max_blocked_task_count, 
  (SELECT MAX (tot_wait_duration_ms) FROM vw_HEAD_BLOCKER_SUMMARY b3 WHERE b3.runtime >= p1.blocking_start AND b3.runtime < p1.blocking_end) AS max_total_wait_duration_ms 
FROM BlockingPeriodsRaw p1
WHERE NOT EXISTS 
  (SELECT blocking_start FROM BlockingPeriodsRaw AS p2
  WHERE p2.blocking_start < p1.blocking_start AND p2.blocking_end = p1.blocking_end)
GO
-- CREATE VIEW vw_BLOCKING_PERIODS AS 
-- WITH BlockingPeriodsRaw (blocking_start, blocking_end) AS (
--   SELECT runtime AS blocking_start, 
--     (SELECT TOP 1 runtime FROM tbl_REQUESTS (NOLOCK) AS r2 WHERE r2.runtime > r1.runtime AND NOT EXISTS 
--       (SELECT runtime FROM tbl_REQUESTS (NOLOCK) AS r3 WHERE r3.runtime = r2.runtime AND blocking_session_id != 0)
--        ORDER BY r2.runtime ASC) AS blocking_end 
--   FROM tbl_REQUESTS (NOLOCK) AS r1
--   WHERE blocking_session_id != 0
--     AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) AS r2 WHERE r1.runtime = r2.runtime AND r1.blocking_session_id = r2.session_id AND r2.blocking_session_id = 0)
--   GROUP BY runtime
-- )
-- SELECT *, 
--   CASE 
--     WHEN (SELECT COUNT(*) FROM tbl_RUNTIMES (NOLOCK) AS r WHERE r.runtime >= blocking_start AND r.runtime < blocking_end) > 1 
--     THEN DATEDIFF (s, blocking_start, blocking_end) 
--     ELSE 
--      (SELECT MAX (wait_duration_ms) FROM vw_BLOCKING_HIERARCHY h 
--       WHERE h.runtime >= blocking_start AND h.runtime < blocking_end) / 1000
--   END AS blocking_duration_sec
-- FROM BlockingPeriodsRaw ch1
-- WHERE NOT EXISTS 
--   (SELECT * FROM BlockingPeriodsRaw ch2 
--    WHERE ch2.blocking_start < ch1.blocking_start AND ch1.blocking_end = ch2.blocking_end)
-- GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID ('tbl_OS_WAIT_STATS') AND name = 'wait_category') 
  ALTER TABLE tbl_OS_WAIT_STATS
  ADD wait_category AS CASE 
    WHEN wait_type LIKE 'LCK%' THEN 'Locks'
    WHEN wait_type LIKE 'PAGEIO%' THEN 'Page I/O Latch'
    WHEN wait_type LIKE 'PAGELATCH%' THEN 'Page Latch (non-I/O)'
    WHEN wait_type LIKE 'LATCH%' THEN 'Latch (non-buffer)'
    WHEN wait_type LIKE 'IO_COMPLETION' THEN 'I/O Completion'
    WHEN wait_type LIKE 'ASYNC_NETWORK_IO' THEN 'Network I/O (client fetch)'
    --WHEN wait_type LIKE 'CLR_%' OR wait_type LIKE 'SQLCLR%' THEN 'SQLCLR'
    WHEN wait_type IN ('RESOURCE_SEMAPHORE', 'SOS_RESERVEDMEMBLOCKLIST','CMEMTHREAD') THEN 'Memory'
    WHEN wait_type LIKE 'RESOURCE_SEMAPHORE_%'  THEN 'Compilation'
    WHEN wait_type LIKE 'MSQL_XP' THEN 'XProc'
    WHEN wait_type LIKE 'WRITELOG' THEN 'Writelog'
    WHEN wait_type IN (
		'DBMIRROR_WORKER_QUEUE',
		'DBMIRRORING_CMD', 
		'DBMIRROR_DBM_EVENT',
		'DBMIRROR_EVENTS_QUEUE',
		'BROKER_EVENTHANDLER',
		'BROKER_RECEIVE_WAITFOR',
		'BROKER_TRANSMITTER',
		'BROKER_TASK_STOP',
		'BROKER_TO_FLUSH',
		'CHECKPOINT_QUEUE',
		'CHKPT',
		'CLR_AUTO_EVENT',
		'CLR_MANUAL_EVENT',
		'FSAGENT',
		'KSOURCE_WAKEUP',
		'LAZYWRITER_SLEEP',
		'LOGMGR_QUEUE',
		'ONDEMAND_TASK_QUEUE',
		'REQUEST_FOR_DEADLOCK_SEARCH',
		'RESOURCE_QUEUE',
		'SERVER_IDLE_CHECK',
		'SLEEP_BPOOL_FLUSH',
		'SLEEP_DBSTARTUP',
		'SLEEP_DCOMSTARTUP',
		'SLEEP_MSDBSTARTUP',
		'SLEEP_SYSTEMTASK',
		'SLEEP_TASK',
		'SLEEP_TEMPDBSTARTUP',
		'SNI_HTTP_ACCEPT',
		
		'SQLTRACE_BUFFER_FLUSH',
		--'TRACEWRITE',
		'WAIT_FOR_RESULTS',
		'WAITFOR_TASKSHUTDOWN',
		'XE_DISPATCHER_WAIT',
		'XE_TIMER_EVENT',
		--'CXPACKET',
		--'EXCHANGE',
		'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
		'EXECSYNC',
		'WAITFOR',
		'FT_IFTS_SCHEDULER_IDLE_WAIT',
		'DISPATCHER_QUEUE_SEMAPHORE',
		'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
		'DIRTY_PAGE_POLL',
		'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
		'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP',
		'PREEMPTIVE_XE_DISPATCHER',
		'SP_SERVER_DIAGNOSTICS_SLEEP',
		'SOS_WORK_DISPATCHER',
		'HADR_DB_COMMAND',
		'HADR_TRANSPORT_SESSION',
		'HADR_CLUSAPI_CALL',
		'HADR_WORK_POOL',
		'HADR_WORK_QUEUE',
		'HADR_LOGCAPTURE_SYNC',
		'HADR_AG_MUTEX',
		'HADR_FILESTREAM_MANAGER',
		'HADR_FILESTREAM_BLOCK_FLUSH',
		'HADR_FILESTREAM_IOMGR',
		'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
		'HADR_FILESTREAM_PREPROC',
		'HADR_DB_OP_START_SYNC',
		'HADR_DB_OP_COMPLETION_SYNC',
		'HADR_LOGPROGRESS_SYNC',
		'HADR_TRANSPORT_DBRLIST',
		'HADR_CONNECTIVITY_INFO',
		'HADR_AR_UNLOAD_COMPLETED',
		'HADR_PARTNER_SYNC',
		'HADR_DBSTATECHANGE_SYNC',
		'HADR_FILESTREAM_FILE_REQUEST',
		'HADR_REPLICAINFO_SYNC',
		'HADR_COMPRESSED_CACHE_SYNC',
		'HADR_AR_MANAGER_MUTEX',
		'HADR_NOTIFICATION_WORKER_TERMINATION_SYNC',
		'HADR_NOTIFICATION_DEQUEUE',
		'HADR_ARCONTROLLER_NOTIFICATIONS_SUBSCRIBER_LIST',
		'HADR_DBR_SUBSCRIBER_FILTER_LIST',
		'HADR_DBR_SUBSCRIBER',
		'HADR_NOTIFICATION_WORKER_STARTUP_SYNC',
		'HADR_NOTIFICATION_WORKER_EXCLUSIVE_ACCESS',
		'HADR_RECOVERY_WAIT_FOR_UNDO',
		'HADR_DATABASE_WAIT_FOR_RESTART',
		'HADR_DATABASE_WAIT_FOR_RECOVERY',
		'HADR_XRF_STACK_ACCESS',
		'HADR_RECOVERY_WAIT_FOR_CONNECTION',
		'HADR_TRANSPORT_FLOW_CONTROL',
		'HADR_DATABASE_FLOW_CONTROL',
		'HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING',
		'HADR_BACKUP_BULK_LOCK',
		'HADR_BACKUP_QUEUE',
		'HADR_LOGCAPTURE_WAIT',
		'HADR_AR_CRITICAL_SECTION_ENTRY',
		'HADR_TDS_LISTENER_SYNC',
		'HADR_READ_ALL_NETWORKS',
		'HADR_TDS_LISTENER_SYNC_PROCESSING',
		'HADR_TIMER_TASK',
		'HADR_GROUP_COMMIT',
		'HADR_SYNCHRONIZING_THROTTLE',
		'HADR_DATABASE_VERSIONING_STATE',
		'HADR_FILESTREAM_FILE_CLOSE',
		'HADR_FABRIC_CALLBACK',
		'HADR_DBSEEDING',
		'HADR_DBSEEDING_LIST',
		'HADR_THROTTLE_LOG_RATE_GOVERNOR',
		'HADR_SEEDING_LIMIT_BACKUPS',
		'HADR_SEEDING_CANCELLATION',
		'HADR_SEEDING_SYNC_COMPLETION',
		'HADR_SEEDING_TIMEOUT_TASK',
		'HADR_SEEDING_FILE_LIST',
		'HADR_SEEDING_WAIT_FOR_COMPLETION',
		'HADR_DBHEALTH_INFOMAP_ACCESS',
		'HADR_SEEDING_READY_FOR_RESTORE_STREAM'
	) 
      THEN 'IGNORABLE'
    ELSE wait_type 
  END 
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'wait_category') 
  ALTER TABLE tbl_REQUESTS
  ADD wait_category AS CASE 
    WHEN wait_type LIKE 'LCK%' THEN 'Locks'
    WHEN wait_type LIKE 'PAGEIO%' THEN 'Page I/O Latch'
    WHEN wait_type LIKE 'PAGELATCH%' THEN 'Page Latch (non-I/O)'
    WHEN wait_type LIKE 'LATCH%' THEN 'Latch (non-buffer)'
    WHEN wait_type LIKE 'IO_COMPLETION' THEN 'I/O Completion'
    WHEN wait_type LIKE 'ASYNC_NETWORK_IO' THEN 'Network I/O (client fetch)'
    --WHEN wait_type LIKE 'CLR_%' or wait_type like 'SQLCLR%' THEN 'SQLCLR'
    WHEN wait_type IN ('RESOURCE_SEMAPHORE', 'SOS_RESERVEDMEMBLOCKLIST', 'CMEMTHREAD') THEN 'Memory'
    WHEN wait_type LIKE 'RESOURCE_SEMAPHORE_%'  THEN 'Compilation'
    WHEN wait_type LIKE 'MSQL_XP' THEN 'XProc'
    WHEN wait_type LIKE 'WRITELOG' THEN 'Writelog'
    WHEN wait_type IN (
    				'DBMIRROR_WORKER_QUEUE',
		'DBMIRRORING_CMD', 
		'DBMIRROR_DBM_EVENT',
		'DBMIRROR_EVENTS_QUEUE',
		'BROKER_EVENTHANDLER',
		'BROKER_RECEIVE_WAITFOR',
		'BROKER_TRANSMITTER',
		'BROKER_TASK_STOP',
		'BROKER_TO_FLUSH',
		'CHECKPOINT_QUEUE',
		'CHKPT',
		'CLR_AUTO_EVENT',
		'CLR_MANUAL_EVENT',
		'FSAGENT',
		'KSOURCE_WAKEUP',
		'LAZYWRITER_SLEEP',
		'LOGMGR_QUEUE',
		'ONDEMAND_TASK_QUEUE',
		'REQUEST_FOR_DEADLOCK_SEARCH',
		'RESOURCE_QUEUE',
		'SERVER_IDLE_CHECK',
		'SLEEP_BPOOL_FLUSH',
		'SLEEP_DBSTARTUP',
		'SLEEP_DCOMSTARTUP',
		'SLEEP_MSDBSTARTUP',
		'SLEEP_SYSTEMTASK',
		'SLEEP_TASK',
		'SLEEP_TEMPDBSTARTUP',
		'SNI_HTTP_ACCEPT',
		
		'SQLTRACE_BUFFER_FLUSH',
		--'TRACEWRITE',
		'WAIT_FOR_RESULTS',
		'WAITFOR_TASKSHUTDOWN',
		'XE_DISPATCHER_WAIT',
		'XE_TIMER_EVENT',
		--'CXPACKET',
		--'EXCHANGE',
		'EXECSYNC',
		'WAITFOR',
		'FT_IFTS_SCHEDULER_IDLE_WAIT',
		'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
		'DISPATCHER_QUEUE_SEMAPHORE',
		'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
		'DIRTY_PAGE_POLL',
		'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
		'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP',
		'PREEMPTIVE_XE_DISPATCHER',
		'SP_SERVER_DIAGNOSTICS_SLEEP'

    
    
    ) 
      THEN 'IGNORABLE'
    ELSE wait_type 
  END 
GO

IF OBJECT_ID ('vw_WAIT_CATEGORY_STATS') IS NOT NULL DROP VIEW vw_WAIT_CATEGORY_STATS
GO
CREATE VIEW vw_WAIT_CATEGORY_STATS AS 
SELECT 
  runtime,
  wait_category, 
  SUM (ISNULL (waiting_tasks_count, 0)) AS waiting_tasks_count, 
  SUM (ISNULL (wait_time_ms, 0)) AS wait_time_ms, 
  SUM (ISNULL (signal_wait_time_ms, 0)) AS signal_wait_time_ms, 
  MAX (ISNULL (max_wait_time_ms, 0)) AS max_wait_time_ms
FROM dbo.tbl_OS_WAIT_STATS (NOLOCK) 
WHERE wait_category != 'IGNORABLE'
GROUP BY runtime, wait_category
GO

/* ============ Nexus Reporting DataSet Queries ============ */
-- Naming convention: DataSet_ReportName_ReportItem
IF OBJECT_ID ('DataSet_WaitStatsMain_WaitStatsChart') IS NOT NULL DROP PROC DataSet_WaitStatsMain_WaitStatsChart
GO
CREATE PROC DataSet_WaitStatsMain_WaitStatsChart @StartTime datetime='19000101', @EndTime datetime='29990101' AS 
IF @StartTime IS NULL OR @StartTime = '19000101' 
  SELECT  @StartTime = MIN (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
IF @EndTime IS NULL OR @EndTime = '29990101' 
  SELECT  @EndTime = MAX (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES

SELECT 
  MIN (w1.runtime) AS interval_start, 
  MAX (w2.runtime) AS interval_end, 
  w2.wait_category, 
  CONVERT (decimal (28,3), MAX (w2.wait_time_ms) - MIN (w1.wait_time_ms) + 0.001) AS wait_time_ms, 
  CONVERT (decimal (28,3), MAX (w2.wait_time_ms) - MIN (w1.wait_time_ms) + 0.001)
    / CASE WHEN DATEDIFF (s, MIN (w1.runtime), MAX (w2.runtime)) = 0 THEN 1 ELSE DATEDIFF (s, MIN (w1.runtime), MAX (w2.runtime)) END AS wait_time_per_sec, 
  CONVERT (decimal (28,3), MAX (w2.waiting_tasks_count) - MIN (w1.waiting_tasks_count)) + 0.001 AS wait_count
FROM vw_WAIT_CATEGORY_STATS w2
LEFT OUTER JOIN vw_WAIT_CATEGORY_STATS w1 ON w1.wait_category = w2.wait_category
  AND w1.runtime = (SELECT TOP 1 runtime FROM tbl_OS_WAIT_STATS w3 WHERE w3.runtime < w2.runtime ORDER BY w3.runtime DESC)
WHERE w2.wait_category IN ('Locks', 'Page I/O Latch', 'Page Latch (non-I/O)', 'Latch (non-buffer)', 'I/O Completion', 
    'Network I/O (client fetch)', 'Writelog', 'Compilation', 'Memory', 'OLEDB')
  AND w2.runtime >= @StartTime
  AND w2.runtime <= @EndTime
GROUP BY w2.wait_category, DATEDIFF (ss, '20000101', w2.runtime) / (DATEDIFF (s, @StartTime, @EndTime) / 50 + 1)
HAVING MIN (w1.runtime) IS NOT NULL
ORDER BY MIN (w2.runtime)
GO

IF OBJECT_ID ('DataSet_WaitStats_WaitStatsTop5Categories') IS NOT NULL DROP PROC DataSet_WaitStats_WaitStatsTop5Categories
GO
CREATE PROC DataSet_WaitStats_WaitStatsTop5Categories 
@StartTime datetime='19000101', 
@EndTime datetime='29990101' ,
@IncludeIgnorable bit = 0
AS 
SET NOCOUNT ON
-- DECLARE @StartTime datetime
-- DECLARE @EndTime datetime
IF (@StartTime IS NOT NULL AND @StartTime != '19000101') SELECT @StartTime = MAX (runtime) FROM tbl_OS_WAIT_STATS WHERE runtime <= @StartTime 
IF (@StartTime IS NULL OR @StartTime = '19000101') SELECT @StartTime = MIN (runtime) FROM tbl_OS_WAIT_STATS

IF (@EndTime IS NOT NULL AND @EndTime != '29990101') SELECT @EndTime = MIN (runtime) FROM tbl_OS_WAIT_STATS WHERE runtime >= @EndTime 
IF (@EndTime IS NULL OR @EndTime = '29990101') SELECT @EndTime = MAX (runtime) FROM tbl_OS_WAIT_STATS

-- Get basic wait stats for the specified interval
SELECT 
  w_end.wait_category, 
case when (CONVERT (bigint, w_end.wait_time_ms) - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) <=0 then 0 else (CONVERT (bigint, w_end.wait_time_ms) - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) end  AS total_wait_time_ms,   
  (CONVERT (bigint, w_end.wait_time_ms) - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) / (DATEDIFF (s, @StartTime, @EndTime) + 1) AS wait_time_ms_per_sec
INTO #waitstats_categories
FROM vw_WAIT_CATEGORY_STATS w_end
LEFT OUTER JOIN vw_WAIT_CATEGORY_STATS w_start ON w_end.wait_category = w_start.wait_category AND w_start.runtime = @StartTime
WHERE w_end.runtime = @EndTime
  AND (w_end.wait_category != 'IGNORABLE' or @IncludeIgnorable = 1)
ORDER BY (w_end.wait_time_ms - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) DESC

-- Get number of available "CPU seconds" in the specified interval (seconds in collection interval times # CPUs on the system)
DECLARE @avail_cpu_time_sec int 
SELECT @avail_cpu_time_sec = (SELECT TOP 1 cpu_count FROM tbl_SYSINFO) * DATEDIFF (s, @StartTime, @EndTime)

-- Get average % CPU utilization (this is the % of all CPUs on the box, ignoring affinity mask)
DECLARE @avg_sql_cpu int 
SELECT @avg_sql_cpu = AVG (sql_cpu_utilization) 
FROM (
  SELECT DISTINCT (SELECT TOP 1 EventTime FROM  tbl_SQL_CPU_HEALTH cpu2 WHERE cpu1.record_id = cpu2.record_id) AS EventTime, 
    record_id, system_idle_cpu, sql_cpu_utilization, 100 - sql_cpu_utilization - system_idle_cpu AS nonsql_cpu_utilization 
  FROM tbl_SQL_CPU_HEALTH cpu1
  WHERE EventTime BETWEEN @StartTime AND @EndTime
) AS sql_cpu

DECLARE @cpu_time_used_ms bigint
SET @cpu_time_used_ms = ISNULL ((0.01 * @avg_sql_cpu) * @avail_cpu_time_sec * 1000, 0)  -- CPU time used by SQL = (%CPU used by SQL) * (available CPU time)

-- Get total wait time for the specified interval
DECLARE @all_resources_wait_time_ms bigint
SELECT @all_resources_wait_time_ms = SUM (total_wait_time_ms) FROM #waitstats_categories
SET @all_resources_wait_time_ms = @all_resources_wait_time_ms + @cpu_time_used_ms

--this will prevent division by zero errors (bug 2119)
if @all_resources_wait_time_ms is null or @all_resources_wait_time_ms=0
begin
	
	return
end


-- Return stats for base wait categories
SELECT * FROM 
( SELECT TOP 5 
    cat.wait_category, 
    DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
    CONVERT (bigint, cat.total_wait_time_ms) AS total_wait_time_ms, 
    CONVERT (numeric(6,2), 100.0*CONVERT (bigint, cat.total_wait_time_ms)/@all_resources_wait_time_ms) AS percent_of_total_waittime, 
    cat.wait_time_ms_per_sec 
  FROM #waitstats_categories cat
  WHERE (cat.wait_time_ms_per_sec > 0 OR cat.total_wait_time_ms > 0)
    AND cat.wait_category != 'SOS_SCHEDULER_YIELD' -- don't include "waiting on CPU" time here; we'll include it in the next query
  ORDER BY wait_time_ms_per_sec DESC
) t
WHERE percent_of_total_waittime > 0
UNION ALL 
-- Add SOS_SCHEDULER_YIELD wait time (waiting to run on a CPU) to actual used CPU time to synthesize a "CPU" wait category
SELECT 
  'CPU' AS wait_category, 
  DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
  CONVERT (bigint, total_wait_time_ms) + @cpu_time_used_ms AS total_wait_time_ms, 
  100.0*(CONVERT (bigint, total_wait_time_ms) + @cpu_time_used_ms)/@all_resources_wait_time_ms AS percent_of_total_waittime, 
  wait_time_ms_per_sec + (@cpu_time_used_ms / (DATEDIFF (s, @StartTime, @EndTime) + 1)) AS wait_time_ms_per_sec
FROM #waitstats_categories cat
WHERE cat.wait_category = 'SOS_SCHEDULER_YIELD'
UNION ALL 
-- Add in an "other" category
SELECT * FROM 
( SELECT 
    'Other' AS wait_category, 
    DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
    SUM (CONVERT (bigint, cat.total_wait_time_ms)) AS total_wait_time_ms, 
    CONVERT (numeric(6,2), 100.0*SUM (CONVERT (bigint, cat.total_wait_time_ms))/@all_resources_wait_time_ms) AS percent_of_total_waittime, 
    SUM (cat.wait_time_ms_per_sec) AS wait_time_ms_per_sec
  FROM #waitstats_categories cat
  WHERE (cat.wait_time_ms_per_sec > 0 OR cat.total_wait_time_ms > 0) 
    -- don't include the categories that we are already identifying in the top 5 
    AND cat.wait_category NOT IN (SELECT TOP 5 cat.wait_category FROM #waitstats_categories cat ORDER BY wait_time_ms_per_sec DESC) 
) AS t
WHERE percent_of_total_waittime > 0
ORDER BY wait_time_ms_per_sec DESC

GO



CREATE PROC DataSet_WaitStats_WaitStatsTopCategoriesOther 
@StartTime datetime='19000101',
@EndTime datetime='29990101',
@IncludeIgnorable bit = 0
AS 
SET NOCOUNT ON
-- DECLARE @StartTime datetime
-- DECLARE @EndTime datetime
IF (@StartTime IS NOT NULL AND @StartTime != '19000101') SELECT @StartTime = MAX (runtime) FROM tbl_OS_WAIT_STATS WHERE runtime <= @StartTime 
IF (@StartTime IS NULL OR @StartTime = '19000101') SELECT @StartTime = MIN (runtime) FROM tbl_OS_WAIT_STATS

IF (@EndTime IS NOT NULL AND @EndTime != '29990101') SELECT @EndTime = MIN (runtime) FROM tbl_OS_WAIT_STATS WHERE runtime >= @EndTime 
IF (@EndTime IS NULL OR @EndTime = '29990101') SELECT @EndTime = MAX (runtime) FROM tbl_OS_WAIT_STATS

-- Get basic wait stats for the specified interval
SELECT 
  w_end.wait_category, 
  (CONVERT (bigint, w_end.wait_time_ms) - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) AS total_wait_time_ms, 
  (CONVERT (bigint, w_end.wait_time_ms) - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) / (DATEDIFF (s, @StartTime, @EndTime) + 1) AS wait_time_ms_per_sec
INTO #waitstats_categories
FROM vw_WAIT_CATEGORY_STATS w_end
LEFT OUTER JOIN vw_WAIT_CATEGORY_STATS w_start ON w_end.wait_category = w_start.wait_category AND w_start.runtime = @StartTime
WHERE w_end.runtime = @EndTime
  AND (w_end.wait_category != 'IGNORABLE' or @IncludeIgnorable = 1)
ORDER BY (w_end.wait_time_ms - CASE WHEN w_start.wait_time_ms IS NULL THEN 0 ELSE w_start.wait_time_ms END) DESC

-- Get number of available "CPU seconds" in the specified interval (seconds in collection interval times # CPUs on the system)
DECLARE @avail_cpu_time_sec int 
SELECT @avail_cpu_time_sec = (SELECT TOP 1 cpu_count FROM tbl_SYSINFO) * DATEDIFF (s, @StartTime, @EndTime)

-- Get average % CPU utilization (this is the % of all CPUs on the box, ignoring affinity mask)
DECLARE @avg_sql_cpu int 
SELECT @avg_sql_cpu = AVG (sql_cpu_utilization) 
FROM (
  SELECT DISTINCT (SELECT TOP 1 EventTime FROM  tbl_SQL_CPU_HEALTH cpu2 WHERE cpu1.record_id = cpu2.record_id) AS EventTime, 
    record_id, system_idle_cpu, sql_cpu_utilization, 100 - sql_cpu_utilization - system_idle_cpu AS nonsql_cpu_utilization 
  FROM tbl_SQL_CPU_HEALTH cpu1
  WHERE EventTime BETWEEN @StartTime AND @EndTime
) AS sql_cpu

DECLARE @cpu_time_used_ms bigint
SET @cpu_time_used_ms = ISNULL ((0.01 * @avg_sql_cpu) * @avail_cpu_time_sec * 1000, 0)  -- CPU time used by SQL = (%CPU used by SQL) * (available CPU time)

-- Get total wait time for the specified interval
DECLARE @all_resources_wait_time_ms bigint
SELECT @all_resources_wait_time_ms = SUM (total_wait_time_ms) FROM #waitstats_categories
SET @all_resources_wait_time_ms = @all_resources_wait_time_ms + @cpu_time_used_ms

--this will prevent division by zero errors (bug 2119)
if @all_resources_wait_time_ms is null or @all_resources_wait_time_ms=0
begin
	
	return
end

/*
-- Return other stats for base wait categories
SELECT * FROM 
( SELECT TOP 5 
    cat.wait_category, 
    DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
    CONVERT (bigint, cat.total_wait_time_ms) AS total_wait_time_ms, 
    CONVERT (numeric(6,2), 100.0*CONVERT (bigint, cat.total_wait_time_ms)/@all_resources_wait_time_ms) AS percent_of_total_waittime, 
    cat.wait_time_ms_per_sec 
  FROM #waitstats_categories cat
  WHERE (cat.wait_time_ms_per_sec > 0 OR cat.total_wait_time_ms > 0)
    AND cat.wait_category != 'SOS_SCHEDULER_YIELD' -- don't include "waiting on CPU" time here; we'll include it in the next query
  ORDER BY wait_time_ms_per_sec DESC
) t
WHERE percent_of_total_waittime > 0
UNION ALL 
-- Add SOS_SCHEDULER_YIELD wait time (waiting to run on a CPU) to actual used CPU time to synthesize a "CPU" wait category
SELECT 
  'CPU' AS wait_category, 
  DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
  CONVERT (bigint, total_wait_time_ms) + @cpu_time_used_ms AS total_wait_time_ms, 
  100.0*(CONVERT (bigint, total_wait_time_ms) + @cpu_time_used_ms)/@all_resources_wait_time_ms AS percent_of_total_waittime, 
  wait_time_ms_per_sec + (@cpu_time_used_ms / (DATEDIFF (s, @StartTime, @EndTime) + 1)) AS wait_time_ms_per_sec
FROM #waitstats_categories cat
WHERE cat.wait_category = 'SOS_SCHEDULER_YIELD'
UNION ALL 
*/
-- Add in an "other" category

SELECT TOP 10 
    cat.wait_category, 
    DATEDIFF (s, @StartTime, @EndTime) AS time_interval_sec, 
    CONVERT (bigint, cat.total_wait_time_ms) AS total_wait_time_ms, 
    CONVERT (numeric(6,2), 100.0*CONVERT (bigint, cat.total_wait_time_ms)/@all_resources_wait_time_ms) AS percent_of_total_waittime, 
    cat.wait_time_ms_per_sec 
  
  FROM #waitstats_categories cat
  WHERE (cat.wait_time_ms_per_sec > 0 OR cat.total_wait_time_ms > 0) 
    -- don't include the categories that we are already identifying in the top 5 
    AND cat.wait_category NOT IN (SELECT TOP 5 cat.wait_category FROM #waitstats_categories cat ORDER BY wait_time_ms_per_sec DESC) 
     AND cat.wait_category != 'SOS_SCHEDULER_YIELD' 
ORDER BY wait_time_ms_per_sec DESC





go
IF OBJECT_ID ('DataSet_WaitStats_BlockingChains') IS NOT NULL DROP PROC DataSet_WaitStats_BlockingChains
GO
CREATE PROC DataSet_WaitStats_BlockingChains @StartTime datetime='19000101', @EndTime datetime='29990101' AS 
IF @StartTime IS NULL OR @StartTime = '19000101' 
  SELECT  @StartTime = MIN (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
IF @EndTime IS NULL OR @EndTime = '29990101' 
  SELECT  @EndTime = MAX (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES

SELECT * 
FROM dbo.vw_BLOCKING_CHAINS
WHERE blocking_duration_sec > 0
  AND (blocking_start BETWEEN @StartTime AND @EndTime) 
  OR (blocking_end BETWEEN @StartTime AND @EndTime)
GO

select convert (varchar, getdate(), 126)

IF OBJECT_ID ('DataSet_BlockingChain_BlockingChainAllRuntimes') IS NOT NULL DROP PROC DataSet_BlockingChain_BlockingChainAllRuntimes
GO
CREATE PROC DataSet_BlockingChain_BlockingChainAllRuntimes @BlockingChainRowNum int AS 
  SELECT CONVERT (varchar, r.runtime, 121) AS runtime, CONVERT (varchar, r.runtime, 126) AS runtime_locale_insensitive, r.task_state, 
    LEFT (r.wait_category, 35) AS wait_category, r.wait_duration_ms, r.request_total_elapsed_time AS request_elapsed_time, 
    blk.blocked_task_count AS blocked_tasks, r.command, LEFT (ch.stmt_text, 100) + '...' AS query
  FROM vw_BLOCKING_CHAINS ch 
  INNER JOIN tbl_REQUESTS r ON r.session_id = ch.head_blocker_session_id_orig AND r.runtime BETWEEN ch.blocking_start AND ch.blocking_end
  INNER JOIN tbl_HEADBLOCKERSUMMARY blk ON ch.head_blocker_session_id_orig = blk.head_blocker_session_id AND blk.runtime = r.runtime
  WHERE ch.first_rownum = @BlockingChainRowNum
  ORDER BY r.runtime
GO

IF OBJECT_ID ('DataSet_BlockingChain_BlockingChainTextSummary') IS NOT NULL DROP PROC DataSet_BlockingChain_BlockingChainTextSummary 
GO
CREATE PROC DataSet_BlockingChain_BlockingChainTextSummary @BlockingChainRowNum int AS 
  DECLARE @txtout varchar(max)
  DECLARE @runtime char(31), @task_state char(16), @wait_category char(36), @wait_duration_ms char(21)
  DECLARE @request_elapsed_time char(21), @blocked_tasks char(14), @command char(17), @query char(24)
  SELECT TOP 1 @txtout = 
    'BLOCKING CHAIN STATISTICS:' + CHAR(13) + CHAR(10) + 
    '  Head Blocker Session ID: ' + CONVERT (char(40), head_blocker_session_id) + CHAR(13) + CHAR(10) + 
    '  Blocking Duration (sec): ' + CONVERT (char(40), blocking_duration_sec) + CHAR(13) + CHAR(10) +  
    '           Max Chain Size: ' + CONVERT (char(40), max_blocked_task_count, 121) + CHAR (13) + CHAR(10) +
    '           Blocking Start: ' + CONVERT (char(40), blocking_start, 121) + CHAR (13) + CHAR(10) + 
    '             Blocking End: ' + CONVERT (char(40), blocking_end, 121) + CHAR (13) + CHAR(10) + 
    CHAR (13) + CHAR(10) +
    'HEAD BLOCKER:' + CHAR(13) + CHAR(10) + 
    '   Program Name: ' + CONVERT (char(40), program_name)                  + '         Transaction Name: ' + CONVERT (char(40), tran_name) + CHAR(13) + CHAR(10) + 
    '      Host Name: ' + CONVERT (char(40), [host_name])                   + 'Transaction Isolation Lvl: ' + CONVERT (char(40), transaction_isolation_level) + CHAR(13) + CHAR(10) + 
    '     Login Name: ' + CONVERT (char(40), login_name)                    + '   Transaction Begin Time: ' + CONVERT (char(40), transaction_begin_time, 121) + CHAR(13) + CHAR(10)
  FROM dbo.vw_BLOCKING_CHAINS
  WHERE first_rownum = @BlockingChainRowNum

  SET @txtout = @txtout + CHAR(13) + CHAR(10)
  SET @txtout = @txtout + 'HEAD BLOCKER RUNTIME SUMMARY:' + CHAR(13) + CHAR(10)
  SET @txtout = @txtout + '  runtime                        task_state      wait_category                       wait_duration_ms     request_elapsed_time blocked_tasks command          query                   ' + CHAR(13) + CHAR(10)
  SET @txtout = @txtout + '  ------------------------------ --------------- ----------------------------------- -------------------- -------------------- ------------- ---------------- ----------------------- ' + CHAR(13) + CHAR(10)
  DECLARE c CURSOR FOR 
  SELECT CONVERT (varchar, r.runtime, 121) AS runtime, r.task_state, LEFT (r.wait_category, 35) AS wait_category, r.wait_duration_ms, r.request_total_elapsed_time AS request_elapsed_time, 
    blk.blocked_task_count AS blocked_tasks, r.command, LEFT (ch.stmt_text, 20) + '...' AS query
  FROM vw_BLOCKING_CHAINS ch 
  INNER JOIN tbl_REQUESTS r ON r.session_id = ch.head_blocker_session_id_orig AND r.runtime BETWEEN ch.blocking_start AND ch.blocking_end
  INNER JOIN tbl_HEADBLOCKERSUMMARY blk ON ch.head_blocker_session_id_orig = blk.head_blocker_session_id AND blk.runtime = r.runtime
  WHERE ch.first_rownum = @BlockingChainRowNum
  ORDER BY r.runtime
  OPEN c
  FETCH NEXT FROM c INTO @runtime, @task_state, @wait_category, @wait_duration_ms, @request_elapsed_time, @blocked_tasks, @command, @query
  WHILE (@@FETCH_STATUS<>-1) BEGIN
    SET @txtout = @txtout + '  ' + @runtime + @task_state + @wait_category + @wait_duration_ms + @request_elapsed_time + @blocked_tasks + @command + @query + CHAR(13) + CHAR(10)
    FETCH NEXT FROM c INTO @runtime, @task_state, @wait_category, @wait_duration_ms, @request_elapsed_time, @blocked_tasks, @command, @query
  END
  CLOSE c
  DEALLOCATE c
  SELECT REPLACE (REPLACE (@txtout, '/', '_'), '\', '_') AS summary
GO

IF OBJECT_ID ('DataSet_BlockingChain_HeadBlockerSampleRuntime') IS NOT NULL DROP PROC DataSet_BlockingChain_HeadBlockerSampleRuntime
GO
CREATE PROC DataSet_BlockingChain_HeadBlockerSampleRuntime @BlockingChainRowNum int AS 
  SELECT r.*, q.* FROM tbl_REQUESTS r
  INNER JOIN vw_BLOCKING_CHAINS ch ON ch.first_rownum = @BlockingChainRowNum AND ch.head_blocker_session_id_orig = r.session_id
  LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES q ON q.session_id = ch.head_blocker_session_id_orig AND q.runtime = r.runtime 
  WHERE r.runtime = ( -- attempt to find the "worst" example runtime for this chain
      SELECT TOP 1 runtime FROM vw_HEAD_BLOCKER_SUMMARY b 	-- attempt to find the "worst" example runtime for this chain
      WHERE ch.head_blocker_session_id =  b.head_blocker_session_id AND ch.blocking_wait_type = b.blocking_wait_type
        AND b.runtime >= ch.blocking_start AND b.runtime < ch.blocking_end 
      ORDER BY b.blocked_task_count * 4 + b.tot_wait_duration_ms / 1000 DESC
    )
  ORDER BY r.session_id
GO

IF OBJECT_ID ('DataSet_BlockingChain_BlockingChainDetails') IS NOT NULL DROP PROC DataSet_BlockingChain_BlockingChainDetails
GO
CREATE PROC DataSet_BlockingChain_BlockingChainDetails @BlockingChainRowNum int AS 
SELECT TOP 1 * 
FROM dbo.vw_BLOCKING_CHAINS
WHERE first_rownum = @BlockingChainRowNum 
GO

IF OBJECT_ID ('DataSet_Shared_SQLServerName') IS NOT NULL DROP PROC DataSet_Shared_SQLServerName
GO
CREATE PROC DataSet_Shared_SQLServerName @script_name varchar(80) = null, @name varchar(60) = null AS 
IF OBJECT_ID ('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
  SELECT [Value]
  FROM tbl_SCRIPT_ENVIRONMENT_DETAILS
  WHERE script_name like 'SQL 200% Perf Stats Script'
    AND [Name] = 'SQL Server Name'
ELSE 
  SELECT @@SERVERNAME AS [value]
  -- SELECT @@SERVERNAME AS [value]
GO


IF OBJECT_ID ('DataSet_Shared_SQLVersion') IS NOT NULL DROP PROC DataSet_Shared_SQLVersion
GO
CREATE PROC DataSet_Shared_SQLVersion @script_name varchar(80) = null, @name varchar(60) = null AS 
IF OBJECT_ID ('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
  SELECT [value]
  FROM tbl_SCRIPT_ENVIRONMENT_DETAILS
  WHERE script_name like 'SQL 200% Perf Stats Script'
    AND [Name] = 'SQL Version (SP)'
ELSE
  SELECT '' AS [value]
  -- SELECT CONVERT (varchar, SERVERPROPERTY ('ProductVersion')) + ' (' + CONVERT (varchar, SERVERPROPERTY ('ProductLevel')) + ')' AS [value]
GO

IF OBJECT_ID ('DataSet_WaitStats_ParamStartTime') IS NOT NULL DROP PROC DataSet_WaitStats_ParamStartTime
GO
CREATE PROC DataSet_WaitStats_ParamStartTime AS 
DECLARE @StartTime datetime
DECLARE @EndTime datetime
SELECT @StartTime = MIN (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
SELECT @EndTime = MAX (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
IF @StartTime IS NULL SET @StartTime = GETDATE()
IF @EndTime IS NULL SET @EndTime = GETDATE()

SELECT 
  CASE 
    WHEN DATEDIFF (mi, @StartTime, @EndTime) > 4*60 THEN DATEADD (mi, -60, @EndTime)
    ELSE @StartTime
  END AS StartTime
UNION ALL
SELECT @StartTime 
UNION ALL
SELECT @EndTime 
GO

IF OBJECT_ID ('DataSet_WaitStats_ParamEndTime') IS NOT NULL DROP PROC DataSet_WaitStats_ParamEndTime
GO
CREATE PROC DataSet_WaitStats_ParamEndTime AS 
DECLARE @StartTime datetime
DECLARE @EndTime datetime
SELECT @StartTime = MIN (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
SELECT @EndTime = MAX (runtime) FROM vw_PERF_STATS_SCRIPT_RUNTIMES
IF @StartTime IS NULL SET @StartTime = GETDATE()
IF @EndTime IS NULL SET @EndTime = GETDATE()

SELECT @EndTime AS EndTime 
UNION ALL
SELECT @StartTime AS EndTime 
UNION ALL
SELECT @EndTime AS EndTime 
GO

insert into tbl_PERF_STATS_SCRIPT_RUNTIMES select distinct runtime from tbl_requests where runtime not in (select runtime from tbl_PERF_STATS_SCRIPT_RUNTIMES)
go
insert into tbl_PERF_STATS_SCRIPT_RUNTIMES select distinct runtime from tbl_OS_WAIT_STATS where runtime not in (select runtime from tbl_PERF_STATS_SCRIPT_RUNTIMES)
go
insert into tbl_PERF_STATS_SCRIPT_RUNTIMES select distinct runtime  from tbl_NOTABLEACTIVEQUERIES where runtime not in (select runtime from tbl_PERF_STATS_SCRIPT_RUNTIMES)
go
/* ============ End Nexus Reporting DataSet Queries ============ */


IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_NOTABLEACTIVEQUERIES') AND name = 'idx1') BEGIN 
  RAISERROR ('Creating index 1 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx1 ON dbo.tbl_NOTABLEACTIVEQUERIES (runtime, session_id, procname) INCLUDE (stmt_text)
END
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'idx1') 
  AND EXISTS (SELECT * FROM syscolumns WHERE [id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'wait_type') 
  BEGIN 
  RAISERROR ('Creating index 2 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx1 ON dbo.tbl_HEADBLOCKERSUMMARY (runtime, head_blocker_session_id, wait_type, blocked_task_count) 
    INCLUDE (rownum, tot_wait_duration_ms, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth)
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'idx1') 
  AND EXISTS (SELECT * FROM syscolumns WHERE [id] = OBJECT_ID ('tbl_HEADBLOCKERSUMMARY') AND name = 'blocking_resource_wait_type') 
  BEGIN 
  RAISERROR ('Creating index 2 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx1 ON dbo.tbl_HEADBLOCKERSUMMARY (runtime, head_blocker_session_id, blocking_resource_wait_type, blocked_task_count) 
    INCLUDE (rownum, tot_wait_duration_ms, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth)
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'idx1') BEGIN 
  RAISERROR ('Creating index 3 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx1 ON [dbo].[tbl_REQUESTS] (runtime, session_id, program_name, [host_name], nt_user_name, nt_domain, login_name) 
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'idx2') BEGIN 
  RAISERROR ('Creating index 4 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx2 ON [dbo].[tbl_REQUESTS] (runtime, blocking_session_id, session_id) 
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'idx3') BEGIN 
  RAISERROR ('Creating index 5 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx3 ON [dbo].[tbl_REQUESTS] (blocking_session_id, runtime, wait_type, wait_resource) 
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'idx4') BEGIN 
  RAISERROR ('Creating index 6 of 7', 0, 1) WITH NOWAIT
  CREATE NONCLUSTERED INDEX idx4 ON [dbo].[tbl_REQUESTS] (wait_category, wait_duration_ms DESC, wait_type, runtime, session_id)
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_OS_WAIT_STATS') AND name = 'cidx') BEGIN 
  RAISERROR ('Creating index 7 of 7', 0, 1) WITH NOWAIT
  CREATE CLUSTERED INDEX cidx ON [dbo].[tbl_OS_WAIT_STATS] (runtime, wait_category) 
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_RUNTIMES') AND name = 'cidx') BEGIN 
  RAISERROR ('Creating index 8 of 8', 0, 1) WITH NOWAIT
  CREATE CLUSTERED INDEX cidx ON [dbo].[tbl_RUNTIMES] (runtime, source_script) 
END
GO
IF NOT EXISTS (SELECT * FROM sysindexes WHERE [id] = OBJECT_ID ('tbl_REQUESTS') AND name = 'stats1') BEGIN 
  CREATE STATISTICS stats1 ON [dbo].[tbl_REQUESTS] (runtime, wait_duration_ms) 
END
GO
/* ========== END ANALYSIS HELPER OBJECTS ========== */
 




/* ========== BEGIN ANALYSIS QUERIES ========== */

IF '%runmode%' != 'REALTIME' BEGIN
  IF OBJECT_ID ('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL BEGIN
    PRINT ''
    PRINT '==== Script Environment Details ====';
    SELECT [Name], [Value] FROM tbl_SCRIPT_ENVIRONMENT_DETAILS WHERE script_name = 'SQL 2005 Perf Stats Script'
  END
END
GO

IF '%runmode%' != 'REALTIME' BEGIN
  PRINT ''
  PRINT '==== Waitstats Resource Bottleneck Analysis ====';
  SELECT TOP 10 
     wait_category, 
    (MAX(wait_time_ms) - MIN(wait_time_ms)) AS wait_time_ms, 
    (MAX(wait_time_ms) - MIN(wait_time_ms)) / (1 + DATEDIFF (s, MIN(runtime), MAX(runtime))) AS wait_time_per_sec, 
    (MAX(waiting_tasks_count) - MIN(waiting_tasks_count)) AS wait_count, 
    (MAX(wait_time_ms) - MIN(wait_time_ms)) / 
       CASE (MAX(waiting_tasks_count) - MIN(waiting_tasks_count)) WHEN 0 THEN 1
       ELSE ((MAX(waiting_tasks_count) - MIN(waiting_tasks_count))) END AS average_wait_time_ms, 
    MAX(max_wait_time_ms) AS max_wait_time_ms 
  FROM vw_WAIT_CATEGORY_STATS
  WHERE wait_category != 'IGNORABLE'
  GROUP BY wait_category
  ORDER BY (MAX(wait_time_ms) - MIN(wait_time_ms)) DESC
END
GO

IF '%runmode%' != 'REALTIME' BEGIN
  DECLARE @wait_category_num int
  DECLARE @wait_category varchar(90)
  SET @wait_category_num = 1
  DECLARE c CURSOR FOR 
    SELECT wait_category FROM vw_WAIT_CATEGORY_STATS 
    WHERE wait_category != 'IGNORABLE'
    GROUP BY wait_category 
    ORDER BY (MAX(wait_time_ms) - MIN(wait_time_ms)) DESC 
  OPEN c
  FETCH NEXT FROM c INTO @wait_category
  WHILE (@@FETCH_STATUS = 0)
  BEGIN
    RAISERROR ('==== Top 10 longest individual waits for the "%s" wait category ====', 0, 1, @wait_category) WITH NOWAIT;
    SELECT TOP 10 * FROM tbl_REQUESTS (NOLOCK) AS r
    WHERE r.wait_category = @wait_category
    ORDER BY wait_duration_ms DESC
    FETCH NEXT FROM c INTO @wait_category
    SET @wait_category_num = @wait_category_num + 1
    IF @wait_category_num >= 3 BREAK
  END
  CLOSE c
  DEALLOCATE c
END
GO

IF '%runmode%' != 'REALTIME' BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Most Expensive Queries by CPU ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 MAX (ISNULL (plan_total_exec_count, 0)) AS exec_count, 
    MAX (ISNULL (plan_total_cpu_ms, 0)) AS total_cpu, 
    MAX (ISNULL (plan_total_duration_ms, 0)) AS total_duration, 
    MAX (ISNULL (plan_total_physical_reads, 0)) AS total_physical_reads,
    MAX (ISNULL (plan_total_Logical_writes, 0)) AS total_writes, 
    CAST (ISNULL (stmt_text, '') AS varchar(150)) AS stmt_text
  FROM tbl_NOTABLEACTIVEQUERIES (NOLOCK) 
  WHERE stmt_text IS NOT NULL
  GROUP BY stmt_text
  ORDER BY MAX (ISNULL (plan_total_cpu_ms, 0)) DESC
  PRINT '     Note: The query costs shown above were sampled from sys.dm_exec_query_stats. This is not
     as reliable as a trace, and may overlook some expensive queries or fail to aggregate the 
     cumulative costs of a query correctly.  Be cautious about drawing firm conclusions about 
     the most expensive query based on this data.'
  PRINT ''
END
GO

IF '%runmode%' != 'REALTIME' BEGIN
  IF NOT EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
    PRINT ''
    PRINT '     No blocking was detected.'
    PRINT ''
  END
END
GO

IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Worst Blocking Chains ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 CONVERT (varchar, blocking_start, 121) AS blocking_start, 
    CONVERT (varchar, blocking_end, 121) AS blocking_end, 
    blocking_duration_sec, 
    max_blocked_task_count, 
    head_blocker_session_id, 
    blocking_wait_type, 
    max_wait_duration_ms 
  FROM vw_BLOCKING_CHAINS d1
  ORDER BY blocking_duration_sec DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Periods of Sustained Blocking ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 CONVERT (varchar, blocking_start, 121) AS blocking_start, 
    CONVERT (varchar, blocking_end, 121) AS blocking_end, 
    blocking_duration_sec, 
    max_blocked_task_count
  FROM vw_BLOCKING_PERIODS d1
  ORDER BY blocking_duration_sec DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking Queries ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 
    COUNT(*) AS num_blocking_chains, 
    MAX (ISNULL (max_blocked_task_count, 0)) AS max_blocking_chain_size, 
    SUM (ISNULL (blocking_duration_sec, 0)) AS blocking_duration_sec, 
    MAX (ISNULL (max_wait_duration_ms, 0)) AS max_wait_duration_ms, 
    MIN (ISNULL (blocking_start, 0)) AS first_occurrence_runtime, 
    b.procname, 
    SUBSTRING (b.stmt_text, 1, 100) --, q.stmt_text
  FROM vw_BLOCKING_CHAINS b
--  LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES q ON b.blocking_start = q.runtime AND b.head_blocker_session_id = q.session_id
  GROUP BY b.procname, SUBSTRING (b.stmt_text, 1, 100)
  ORDER BY MAX (max_blocked_task_count) * 4 + SUM (blocking_duration_sec) DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking Programs ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 
    COUNT(*) AS num_blocking_chains, 
    MAX (ISNULL (max_blocked_task_count, 0)) AS max_blocking_chain_size, 
    SUM (ISNULL (blocking_duration_sec, 0)) AS blocking_duration_sec, 
    MAX (ISNULL (max_wait_duration_ms, 0)) AS max_wait_duration_ms, 
    MIN (ISNULL (blocking_start, 0)) AS first_occurrence_runtime, 
    program_name 
  FROM vw_BLOCKING_CHAINS b
  GROUP BY program_name
  ORDER BY MAX (max_blocked_task_count) * 4 + SUM (blocking_duration_sec) DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking Host Workstations ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 
    COUNT(*) AS num_blocking_chains, 
    MAX (ISNULL (max_blocked_task_count, 0)) AS max_blocking_chain_size, 
    SUM (ISNULL (blocking_duration_sec, 0)) AS blocking_duration_sec, 
    MAX (ISNULL (max_wait_duration_ms, 0)) AS max_wait_duration_ms, 
    MIN (ISNULL (blocking_start, 0)) AS first_occurrence_runtime, 
    [host_name] 
  FROM vw_BLOCKING_CHAINS b
  GROUP BY [host_name]
  ORDER BY MAX (max_blocked_task_count) * 4 + SUM (blocking_duration_sec) DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking NT Users ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 
    COUNT(*) AS num_blocking_chains, 
    MAX (ISNULL (max_blocked_task_count, 0)) AS max_blocking_chain_size, 
    SUM (ISNULL (blocking_duration_sec, 0)) AS blocking_duration_sec, 
    MAX (ISNULL (max_wait_duration_ms, 0)) AS max_wait_duration_ms, 
    MIN (ISNULL (blocking_start, 0)) AS first_occurrence_runtime, 
    ISNULL (nt_domain, '') + '\' + nt_user_name
  FROM vw_BLOCKING_CHAINS b
  GROUP BY ISNULL (nt_domain, '') + '\' + nt_user_name
  ORDER BY MAX (max_blocked_task_count) * 4 + SUM (blocking_duration_sec) DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking SQL Logins ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 
    COUNT(*) AS num_blocking_chains, 
    MAX (ISNULL (max_blocked_task_count, 0)) AS max_blocking_chain_size, 
    SUM (ISNULL (blocking_duration_sec, 0)) AS blocking_duration_sec, 
    MAX (ISNULL (max_wait_duration_ms, 0)) AS max_wait_duration_ms, 
    MIN (ISNULL (blocking_start, 0)) AS first_occurrence_runtime, 
    login_name
    -- , (blocked_task_count) + (MAX (tot_wait_duration_ms) / 5000) AS [Weighted Blocking Chain Score]
  FROM vw_BLOCKING_CHAINS b
  GROUP BY login_name
  ORDER BY MAX (max_blocked_task_count) * 4 + SUM (blocking_duration_sec) DESC
END
GO
IF '%runmode%' != 'REALTIME' AND EXISTS (SELECT * FROM tbl_REQUESTS (NOLOCK) WHERE blocking_session_id != 0) BEGIN
  PRINT ''
  RAISERROR ('==== Top 10 Blocking Resources ====', 0, 1) WITH NOWAIT;
  SELECT TOP 10 COUNT(DISTINCT runtime) AS num_blocking_incidents, 
    COUNT (*) AS total_blocked_sessions, 
    MAX (wait_duration_ms) AS max_wait_duration_ms, 
    AVG (wait_duration_ms) AS avg_wait_duration_ms, 
    (SELECT TOP 1 CONVERT (varchar, runtime, 121) FROM vw_FIRSTTIERBLOCKINGHIERARCHY 
     WHERE first_tier_wait_resource = f.first_tier_wait_resource 
     GROUP BY runtime, first_tier_wait_resource
     ORDER BY (SUM (wait_duration_ms)/5000) + COUNT(*) DESC) AS example_runtime, 
    first_tier_wait_resource 
    -- , (SUM (wait_duration_ms)/5000) + COUNT(*) AS [Weighted Blocking Resource Score]
  FROM vw_FIRSTTIERBLOCKINGHIERARCHY f
  GROUP BY first_tier_wait_resource
  ORDER BY (SUM (wait_duration_ms)/5000) + COUNT(*) DESC
END
GO

-- Run this to view a particular tbl_requests snapshot (tbl_REQUESTS is like sysprocesses, but with a richer data set) 
--   SELECT * FROM tbl_REQUESTS (NOLOCK) AS r 
--   LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES (NOLOCK) AS q ON r.runtime = q.runtime AND r.session_id = q.session_id 
--   WHERE r.runtime = '2006-08-15 13:25:31.550'
GO
 

IF OBJECT_ID ('GetTopNQueryHash') IS NOT NULL DROP PROC GetTopNQueryHash
go 
create procedure GetTopNQueryHash @OrderByCriteria nvarchar(20) = 'CPU'
as
declare @tableName nvarchar(50), @OrderName nvarchar(50), @DisplayValue nvarchar(100), @sql nvarchar(max)
if @OrderByCriteria = 'CPU'
begin
	set @tableName = 'tbl_TopNCPUByQueryHash'
	set @OrderName = 'total_worker_time'
	set @DisplayValue = 'total_worker_time'
end
else if (@OrderByCriteria = 'Duration')
begin
		set @tableName = 'tbl_TopNDurationByQueryHash'
	set @OrderName = 'total_elapsed_time'
	set @DisplayValue = 'total_elapsed_time'
end
else if (@OrderByCriteria = 'Logical Reads')
begin
		set @tableName = 'tbl_TopNLogicalReadsByQueryHash'
	set @OrderName = 'total_logical_reads'
	set @DisplayValue = 'total_logical_reads'
end


set @sql = 'select ROW_NUMBER() over (order by ' + @OrderName + ' desc) as ''rownumber'',  *, '+ @DisplayValue + ' as DisplayValue from '  + @tableName + ' order by ' + @OrderName + ' desc'
exec (@sql)

go

create table tbl_Reports 
( 
ReportId int identity primary key,
ReportName nvarchar(128),
ReportDisplayName nvarchar(256),
ReportDescription nvarchar(max),
VersionApplied int,
ValidationFunction nvarchar(128), 
Category nvarchar(50),
SeqNo int,

)
go
create unique index IX_tbl_Reports_ReportName on tbl_Reports (ReportName)

go
-- 1 =2000, 2=2005, 4=2008, 8=2008R2
insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Blocking and Wait Statistics_C', 'Blocking and Wait Statistics', 'Blocking and wait statistics',  2 | 4, 'Performance', 'dbo.fn_Validate_NexusCore', 100)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied,  Category, ValidationFunction, SeqNo) 
values ('Bottleneck Analysis_C', 'Bottleneck Analysis', 'Bottleneck Analysis',2 | 4,'Performance', 'dbo.fn_Validate_NexusCore', 200)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied,  Category, ValidationFunction, SeqNo) 
values ('Spinlock Stats_C', 'Spin Lock Stats', 'Spin Lock Stats',  2 | 4,'Performance', 'dbo.fn_Validate_NexusCore',900)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied,  Category, ValidationFunction, SeqNo) 
values ('Query Hash_C', 'Query Hash', 'This report is for Query hash.  It is only available in 2008',  4, 'Performance', 'dbo.fn_Validate_NexusCore', 400)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied,  Category, ValidationFunction, SeqNo) 
values ('Missing Indexes_C', 'Missing Indexes', 'Missing Indexes for SQL Server 2005/2008/2008R2',  2|4|8,'Performance', 'dbo.fn_Validate_NexusCore', 410)


insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('SQL 2000 Blocking_C', 'SQL Server 2000 blocking', 'SQL Server 2000 blocking',  1, 'Performance', 'dbo.fn_Validate_NexusCore2000', 1000)
	
insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied,  Category, ValidationFunction, SeqNo) 
values ('ReadTrace_Main', 'ReadTrace Reports', 'ReadTrace reports for Profiler traces (2000,2005,2008)', 1|2|4|8,'Performance', 'readtrace.fn_ShowReports',  50)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Perfmon_C', 'Perfmon Summary', 'Summary of most commonly looked at counters',  1|2|4|8, 'Performance', 'dbo.fn_Validate_NexusCore', 500)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Virtual File Stats_C', 'Virtual File Stats', 'Display Virtual File Stats related to IO performance',  1|2|4|8, 'Performance', 'dbo.fn_Validate_NexusCore', 500)


insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Memory Brokers_C', 'Memory Brokers', 'Display memory brokers',  1|2|4|8, 'Performance', 'dbo.fn_Validate_NexusCore', 600)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('SysIndexes_C', 'Indexes and Stats', 'Display Stats info on indexes and stats',  1|2|4|8, 'Performance', 'dbo.fn_Validate_NexusCore', 700)



insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Query Execution Memory_C', 'Query Execution Memory', 'Displays Query Execution Memory',  1|2|4|8, 'Performance', 'dbo.fn_Validate_NexusCore', 600)


insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Configuration_C', 'Environment(diagscan)', 'Server configuration values',  1|2|4|8, 'Common', 'dbo.fn_Validate_DiagScan', 500)

insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Errors and Warnings_C', 'Errors from errorlog(diagscan)', 'Error statistics from errorlog',  1|2|4|8, 'Common', 'dbo.fn_Validate_DiagScan', 510)
	
insert into tbl_Reports (ReportName, ReportDisplayName, ReportDescription, VersionApplied, Category,  ValidationFunction, SeqNo) 
values ('Loaded Modules_C', 'Loaded Modules', 'Modules loaded in SQL Server address space',  1|2|4|8, 'Common', 'dbo.fn_Validate_NexusCore', 100)




go
create function dbo.fn_Validate_Fake()
returns bit
as
begin
	return 1
end


go

create function dbo.fn_Validate_NexusCore()
returns bit
as
begin
declare @ret bit
set @ret = 1
if (object_id ('vw_PERF_STATS_SCRIPT_RUNTIMES') is null)
begin
	set @ret = 0
end
else if ((select count (*) from vw_PERF_STATS_SCRIPT_RUNTIMES) <=0)
begin
	set @ret = 0
end

return @ret
end

go
create function dbo.fn_Validate_NexusCore2000()
returns bit
as
begin
declare @ret bit
set @ret = 1
if (object_id ('tbl_sysprocesses') is not null)
begin
	set @ret = 1
end
else 
begin
	set @ret = 0
end

return @ret
end

go

create function dbo.fn_Validate_DiagScan()
returns bit
as
begin
declare @ret bit
set @ret = 1
if (object_id ('tblDiagScan') is not null)
begin
	set @ret = 1
end
else 
begin
	set @ret = 0
end

return @ret
end


go


create procedure proc_GetAllReports  
as  
declare @sql nvarchar(max)  
set @sql = ''  
select @sql = @sql + 'select  ' + CAST(ReportID as nvarchar(max)) + ', ' +  case when object_id (ValidationFunction) is not null then ValidationFunction  else 'dbo.fn_Validate_Fake' end+ '() as ValidateResults;' from tbl_Reports  
declare @t table (ReportID int, ReportAvailable bit)  
insert into @t exec (@sql)  
select distinct Category, ReportName, ReportDisplayName, ReportDescription,  
/*case  when (case [Value] when '8' then 1 when '9' then 2 when '10' then 4 else null end) & VersionApplied <> 0 and */
 --ReportAvailable = 1 then 1 else 0 end as 'ReportAvailable'  
 ReportAvailable, SeqNo
 from tbl_reports  rep inner join @t t on rep.reportid = t.reportid  
cross join tblNexusInfo   
where Attribute = 'SQLVersion'  
order by SeqNo


go


-- When the perfstats script starts it captures all of the CPU ring buffer entries and each minute 
-- thereafter captures the TOP 5 most recent entries.  The net affect is that the script output ends 
-- up having duplicate (up to 5) entries for the same record_id/timestamp.  For example, we end up 
-- retrieving the following records for times tN:
-- Snapshot 1: t1, t2, t3, t4, t5
-- Snapshot 2:     t2, t3, t4, t5, t6
-- Snapshot 3:         t3, t4, t5, t6, t7
-- ...
-- Snapshot 5:                 t5, t6, t7, t8, t9
-- Snapshot 6:                     t6, t7, t8, t9, t10
-- This query deletes all but the first row of the data for any particular record_id/time
delete dbo.tbl_SQL_CPU_HEALTH 
from dbo.tbl_SQL_CPU_HEALTH h
      left join (select record_id, min(rownum) as rownum from dbo.tbl_SQL_CPU_HEALTH group by record_id) as f
      on h.rownum = f.rownum
where f.rownum is null

go
if OBJECT_ID ('tbl_SysIndexes') is not null 
BEGIN
	--delete some noisy values from sysindexes
	delete tbl_SysIndexes where ISNUMERIC (row_mods) = 0 or ISNUMERIC (dbid)=0
	--get rid of text NULL
	update tbl_sysIndexes  set stats_updated=null where stats_updated='NULL'
END

go
if object_id ('tbl_dm_os_memory_brokers') is not null and   not exists (select * from sys.columns where object_id = object_id ('tbl_dm_os_memory_brokers') and name='pool_id')
	alter table tbl_dm_os_memory_brokers add pool_id int
	
go

create procedure DateSet_GetSnapshotTime @tablename sysname
as
declare @sql nvarchar(max)
set @sql = 'if OBJECT_ID (''' + @tablename + ''') is not null '
set @sql = @sql + ' begin '
set @sql = @sql + 'SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime from (select distinct runtime from ' +  @tablename + ') t '
set @sql = @sql + 'end '
set @sql = @sql + 'else '
set @sql = @sql + 'begin '
set @sql = @sql + '	select cast (1 as int)  ''RowNumber'', ''1990/1/1'' ''RunTime'''
set @sql = @sql + 'end '
--print @sql 
exec (@sql)


go

  create procedure DateSet_GetSnapshotTime_Default @tablename sysname
as
declare @sql nvarchar(max)
set @sql ='	if OBJECT_ID (''' + @tablename  + ''') is not null '
set @sql = @sql + 'begin '
set @sql = @sql + 'with MyRuntime '
set @sql = @sql + 'as ( '
set @sql = @sql + 'SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime  from (select distinct runtime from ' + @tablename + ') t)'
set @sql = @sql + 'select RowNumber, runtime from MyRuntime '
set @sql = @sql + 'where RowNumber = (select MAX(rowNumber) from MyRuntime) '
set @sql = @sql + 'end '
set @sql = @sql + 'else '
set @sql = @sql + 'begin '
set @sql = @sql + '	select cast (1 as int)  ''RowNumber'', cast(''1990/1/1'' as datetime) ''runttime'''
set @sql = @sql + 'end'
exec (@sql)


go
create procedure DataSet_GetSnapshot @tablename sysname, @rownumber int
as
declare @sql nvarchar(max)
set @sql = 'if OBJECT_ID (''' + @tablename + ''') is not null '
set @sql = @sql + 'begin '
set @sql = @sql + 'declare @runtime datetime '
if @rownumber is null
	set @sql = @sql + '	select @runtime = MAX (runtime) from ' + @tablename  + ' '
else
 set @sql = @sql + '	  select @runtime = runtime from (SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime  from (select distinct runtime from ' + @tablename + ') t   ) MyRunTime  where RowNumber = ' + cast(@rownumber as varchar(100)) + ' ' 
 set @sql = @sql + 'select  * from ' + @tablename + ' where runtime = @runtime '
set @sql = @sql + 'end '
exec (@sql)
--print @sql

go

if OBJECT_ID ('tbl_FileStats') is not null 
BEGIN
	update t1
	set t1.[database] = t2.[Database],
	t1.[File]=t2.[file]
	from 
	  tbl_FileStats t1
	inner join 
	(select [DATABASE], [file], [dbid], [fileid]  from tbl_FileStats where runtime = (select min(runtime) from tbl_FileStats) ) t2 on t1.dbid =t2.dbid and t1.fileid=t2.fileid
END
go


if OBJECT_ID ('CounterData') is not null 
begin
	alter table CounterData
		alter column CounterDateTime varchar(24);

	update CounterData
	set CounterDateTime = REPLACE(CounterDateTime, char(0), '')

	create index [INDX_CounterDateTime]
	on [dbo].[CounterData]
	(
		[CounterDateTime]
	)

	print 'Added index to Table - dbo.CounterData || Column CounterDateTime'
end
go
--clean up spinlock
if OBJECT_ID ('tbl_SPINLOCKSTATS') is not null 
	delete tbl_SPINLOCKSTATS where [name] like '%dbcc%'
go

 
--this is used to merge waits from tbl_requests
--if a wait never finishes in the tbl_requests, the waits are never counted
--for example, if long blocking chain is detected but blocking is not finished yet, this is never reflected in bottleneck analysis
--this update will do the following
--for any unfinished waits, we merge wait duration and task count into tbl_os_wait_stats
--to avoid double counting, we only add time difference between captures
--for example, suppose a lock wait started at t1, it lasted t2 and t3 capture.  
--for t1, we update whatever the duration is. for t2 and t3, we just add the difference between t2 and t1 and t3 and t2
update  wait
set wait_time_ms = wait_time_ms + isnull(t.wait_duration_ms,0),
waiting_tasks_count = waiting_tasks_count + isnull (t.task_cnt,0),
max_wait_time_ms = case when max_wait_time_ms < t.max_wait_duration_ms then t.max_wait_duration_ms else max_wait_time_ms end
from 
tbl_OS_Wait_Stats wait inner join 
(select req.runtime, 
wait_type, sum (case when wait_duration_ms >=timediff then timediff else wait_duration_ms end) 'wait_duration_ms',
sum(case when wait_duration_ms >=timediff then 0 else 1 end) 'task_cnt',
max(wait_duration_ms) 'max_wait_duration_ms' 
 from tbl_Requests req inner join 
(select t1.runtime, datediff (ms, t2.runtime,t1.runtime) 'timediff' from 
(select row_number() over (order by runtime asc) row_num, runtime  from (select distinct runtime from tbl_OS_WAIT_STATS)  t) t1 inner join 
(select row_number() over (order by runtime asc) row_num, runtime  from (select distinct runtime from tbl_OS_WAIT_STATS)  t)  t2 on t1.row_num=t2.row_num+1 ) df
on req.runtime=df.runtime
group by req.runtime, req.wait_type
) t
on wait.runtime=t.runtime and wait.wait_type=t.wait_type


go


--work around a bulkcopy problem
if object_id ('tbl_SysIndexes') is not null and exists (select * from sys.columns where name = 'row_mod2')
begin
	exec sp_RENAME 'tbl_SysIndexes.row_mod2', 'row_mods' , 'COLUMN'
end

go

--fixing up Azure and boxed product query difference
if object_id ('tbl_SysIndexes') is not null
begin
update tbl_Sysindexes
set [norecompute]= (case when  [norecompute] = '0' then 'no'  when [norecompute] ='1' then 'yes' else [norecompute] end)
end

go

if object_id ('tbl_trace_event_details') is null
begin
CREATE TABLE [dbo].[tbl_trace_event_details](
	[trace_id] [bigint] NULL,
	[status] [bigint] NULL,
	[path] [varchar](260) NULL,
	[max_size] [bigint] NULL,
	[start_time] [varchar](23) NULL,
	[stop_time] [varchar](23) NULL,
	[max_files] [bigint] NULL,
	[is_rowset] [bigint] NULL,
	[is_rollover] [bigint] NULL,
	[is_shutdown] [bigint] NULL,
	[is_default] [bigint] NULL,
	[buffer_count] [bigint] NULL,
	[buffer_size] [bigint] NULL,
	[last_event_time] [varchar](23) NULL,
	[event_count] [bigint] NULL,
	[trace_event_id] [bigint] NULL,
	[trace_event_name] [varchar](128) NULL,
	[trace_column_id] [bigint] NULL,
	[trace_column_name] [varchar](128) NULL,
	[expensive_event] [bigint] NULL
) ON [PRIMARY]
end
GO

-- default configurations
create table tblDefaultConfigures ([Configuration Option] nvarchar(128) unique, DefaultOption int)
go

insert into tblDefaultConfigures values ('access check cache bucket count',0)
insert into tblDefaultConfigures values ('access check cache quota',0)
insert into tblDefaultConfigures values ('ad hoc distributed queries',0)
insert into tblDefaultConfigures values ('affinity I/O mask',0)
insert into tblDefaultConfigures values ('affinity64 I/O mask',0)
insert into tblDefaultConfigures values ('affinity mask',0)
insert into tblDefaultConfigures values ('affinity64 mask',0)
insert into tblDefaultConfigures values ('Agent XPs ',0)
insert into tblDefaultConfigures values ('allow updates',0)
insert into tblDefaultConfigures values ('backup compression default',0)
insert into tblDefaultConfigures values ('blocked process threshold',0)
insert into tblDefaultConfigures values ('c2 audit mode',0)
insert into tblDefaultConfigures values ('clr enabled',0)
insert into tblDefaultConfigures values ('common criteria compliance enabled',0)
insert into tblDefaultConfigures values ('contained database authentication',0)
insert into tblDefaultConfigures values ('cost threshold for parallelism',5)
insert into tblDefaultConfigures values ('cross db ownership chaining',0)
insert into tblDefaultConfigures values ('cursor threshold',-1)
insert into tblDefaultConfigures values ('Database Mail XPs',0)
insert into tblDefaultConfigures values ('default full-text language',1033)
insert into tblDefaultConfigures values ('default language',0)
insert into tblDefaultConfigures values ('default trace enabled',1)
insert into tblDefaultConfigures values ('disallow results from triggers',0)
insert into tblDefaultConfigures values ('EKM provider enabled',0)
insert into tblDefaultConfigures values ('filestream_access_level',0)
insert into tblDefaultConfigures values ('fill factor (A, RR)',0)
insert into tblDefaultConfigures values ('ft crawl bandwidth (max), see ft crawl bandwidth(A)',100)
insert into tblDefaultConfigures values ('ft crawl bandwidth (min), see ft crawl bandwidth',0)
insert into tblDefaultConfigures values ('ft notify bandwidth (max), see ft notify bandwidth',100)
insert into tblDefaultConfigures values ('ft notify bandwidth (min), see ft notify bandwidth',0)
insert into tblDefaultConfigures values ('index create memory',0)
insert into tblDefaultConfigures values ('in-doubt xact resolution',0)
insert into tblDefaultConfigures values ('lightweight pooling',0)
insert into tblDefaultConfigures values ('locks',0)
insert into tblDefaultConfigures values ('max degree of parallelism',0)
insert into tblDefaultConfigures values ('max full-text crawl range',4)
insert into tblDefaultConfigures values ('max server memory',2147483647)
insert into tblDefaultConfigures values ('max text repl size',65536)
insert into tblDefaultConfigures values ('max worker threads',0)
insert into tblDefaultConfigures values ('media retention',0)
insert into tblDefaultConfigures values ('min memory per query',1024)
insert into tblDefaultConfigures values ('min server memory',0)
insert into tblDefaultConfigures values ('nested triggers',1)
insert into tblDefaultConfigures values ('network packet size',4096)
insert into tblDefaultConfigures values ('Ole Automation Procedures',0)
insert into tblDefaultConfigures values ('open objects',0)
insert into tblDefaultConfigures values ('optimize for ad hoc workloads',0)
insert into tblDefaultConfigures values ('PH_timeout',60)
insert into tblDefaultConfigures values ('precompute rank',0)
insert into tblDefaultConfigures values ('priority boost',0)
insert into tblDefaultConfigures values ('query governor cost limit',0)
insert into tblDefaultConfigures values ('query wait',-1)
insert into tblDefaultConfigures values ('recovery interval',0)
insert into tblDefaultConfigures values ('remote access',1)
insert into tblDefaultConfigures values ('remote admin connections',0)
insert into tblDefaultConfigures values ('remote login timeout',10)
insert into tblDefaultConfigures values ('remote proc trans',0)
insert into tblDefaultConfigures values ('remote query timeout',600)
insert into tblDefaultConfigures values ('Replication XPs Option',0)
insert into tblDefaultConfigures values ('scan for startup procs',0)
insert into tblDefaultConfigures values ('server trigger recursion',1)
insert into tblDefaultConfigures values ('set working set size',0)
insert into tblDefaultConfigures values ('show advanced options',0)
insert into tblDefaultConfigures values ('SMO and DMO XPs',1)
insert into tblDefaultConfigures values ('transform noise words',0)
insert into tblDefaultConfigures values ('two digit year cutoff',2049)
insert into tblDefaultConfigures values ('user connections',0)
insert into tblDefaultConfigures values ('user options',0)
insert into tblDefaultConfigures values ('xp_cmdshell',0)
go


/***********************************************************
 Top query plan analysis
 *************************************************************/

set QUOTED_IDENTIFIER on; 
WITH XMLNAMESPACES ('http://schemas.microsoft.com/sqlserver/2004/07/showplan' AS sp)  
select distinct  stmt.stmt_details.value ('@Database', 'varchar(max)') 'Database' ,  stmt.stmt_details.value ('@Schema', 'varchar(max)') 'Schema' ,  
 stmt.stmt_details.value ('@Table', 'varchar(max)') 'table'   into tblObjectsUsedByTopPlans

 from 
 (  select cast(FileContent as xml) sqlplan from tblTopSqlPlan) as p       cross apply sqlplan.nodes('//sp:Object') as stmt (stmt_details) 

go

--QDS
if object_id ('tbl_QDS_Query_Stats') is not null
begin
	alter table tbl_QDS_Query_Stats add Query_Number int
	alter table tbl_QDS_Query_Stats add Logical_Reads_Order int
	alter table tbl_QDS_Query_Stats add Logical_Writes_Order int
	alter table tbl_QDS_Query_Stats add Physical_Reads_Order int
	alter table tbl_QDS_Query_Stats add Duration_Order int
	alter table tbl_QDS_Query_Stats add Memory_Order int
end

GO
	
if object_id ('tbl_QDS_Query_Stats') is not null
begin

	update QS
	set QS.Query_Number=t2.QueryNumber, QS.Logical_Reads_Order = t2.Logical_Reads_Order, QS.Logical_Writes_Order=t2.Logical_Writes_Order, qs.Physical_Reads_Order=t2.Physical_reads_order, qs.Memory_Order=t2.Memory_order,
	qs.Duration_Order = t2.Duration_order
	from 
	tbl_QDS_Query_Stats QS
	join 
	(
	select *, row_number() over(order by total_cpu desc) as QueryNumber, row_number() over (order by total_logical_reads desc) Logical_Reads_Order,
	row_number() over (order by total_logical_writes desc) Logical_Writes_Order,row_number() over (order by total_physical_reads desc) Physical_reads_order,
	row_number() over (order by total_Query_Memory desc) Memory_order,
	row_number() over (order by total_duration desc) Duration_order

	from
	(
	select database_id, query_hash, query_sql_text, sum(count_executions*avg_cpu_time)  total_cpu, sum(count_executions*avg_duration) total_duration,sum(count_executions*avg_cpu_time) total_logical_reads,
	 sum(count_executions*avg_logical_io_writes) total_logical_writes, sum(count_executions*avg_physical_io_reads) total_physical_reads, sum(count_executions*avg_query_max_used_memory) total_Query_Memory
	from tbl_QDS_Query_Stats
	group by database_id, query_hash, query_sql_text

	--order by 3 desc 
	) t) t2  on QS.query_hash = t2.query_hash

end


go
--used to provide Analysis Summary for pssdiag
create table tbl_AnalysisSummary
(
	SolutionSourceId uniqueidentifier  primary key,
	Category nvarchar(100),
	[Type] nvarchar(10) check ([Type] in ('E', 'W', 'I')),
	[TypeDesc] nvarchar(10) check ([TypeDesc] in ('Error', 'Warning', 'Info')),
	Name nvarchar(255) unique,
	FriendlyName nvarchar(255),
	Description nvarchar(max),
	InternalUrl nvarchar (max),
	ExternalUrl nvarchar(max),
	Author nvarchar(30),
	Priority int,
	SeqNum int,
	[Status] tinyint,
	
)
go

--select newid()
/********************************************************
owner:jackli
*********************************************************/


insert into tbl_Analysissummary (SolutionSourceId,Category, type,typedesc, Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('1BDE61F7-CE1D-4C99-ABFB-31344A3E317D', 'Server Performance', 'W','Warning', 'AutoCreateStats','Auto Create Statistics is disabled', 'Some databases have auto create statistics disabled. This can negative impact performance.  See Database Configuration report for details', '', 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_create_statistics_async', '  jackli', 1, 99, 0)
insert into tbl_Analysissummary (SolutionSourceId,Category, type,typedesc, Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('E5335E8F-91F8-4B7D-842C-8C004159C749', 'Server Performance', 'W','Warning', 'AutoUpdateStats','Auto Update Statistics is disabled', 'Some databases have auto create statistics disabled. This can negative impact performance.  See Database Configuration report for details', '', 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_update_statistics_async', '  jackli', 1, 99, 0)
insert into tbl_Analysissummary (SolutionSourceId,Category, type,typedesc, Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('9E5F54A8-9B8B-46A5-B372-238E873F6277', 'Server Performance', 'W','Warning', 'PowerPlan','Power Plan not properly set', 'Power Plan is not set to High Performance which can impact overall server performance.', '', 'https://docs.microsoft.com/troubleshoot/windows-server/performance/slow-performance-when-using-power-plan', '  jackli', 1, 100, 0)
insert into tbl_Analysissummary (SolutionSourceId,Category, type,typedesc, Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('CCCDE188-8E68-4B87-9649-761AF3F48FC8','Server Performance', 'W','Warning', 'Trace Flag 4199', 'Trace flag 4199 not enabled', 'This trace flag is required to activiate all query optimizer fixes. Without this trace flag, none of the query optimizer fixes will be activated even you are on the latest hotfix or cumulative update. ', '', 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf4199', '  jackli', 1, 100, 0)
insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('D89F7B5E-25BA-460E-9628-F7B0F5E31FFE','Server Performance', 'W','Warning', 'Detailed XEvent Tracing','Some intensive XEvent tracing captured', 'Some high-impact Extended Events (XEvents) are active on the server.  For high volume systems, this can have negative performance impact. Turn off query_pre_execution_showplan, query_post_execution_showplan,query_post_compilation_showplan,lock_acquired,sql_statement_starting,sql_statement_completed,sp_statement_starting, and sp_statement_completed to reduce impact. See pssdiag file *Profiler Traces_Startup.OUT for details', '', '', '  jackli', 1, 200, 0)
insert into tbl_Analysissummary (SolutionSourceId,Category, type,typedesc, Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('3EAE7B17-7BE4-486D-98AC-309E74CE6771','Server Performance', 'W','Warning', 'Trace Flag 1118', 'Trace Flag 1118 not enabled', 'This trace flag help reduce tempdb contention. ', '', 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf1118', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('17C9E271-756E-46E8-A3D3-B9B15E5FA305','Server Performance', 'W', 'Warning', 'Trace Flag 8048', 'Trace flag 8048 not enabled', 'This trace flag partitions certain memory allocators by CPU and can improve performance for hihgly active servers.', '', 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf8048', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('AC4F983A-B8F9-4542-8971-B6052175F2B3','Server Performance', 'W','Warning', 'Trace Flag 9024', 'Trace flag 9024 not enabled', 'This trace flag can help reduce recovery time and log writes.', '', 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf9024', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('76F946D8-7AAD-400E-9CEF-1F071AA68868','Server Performance', 'W','Warning', 'Trace Flag 1236', 'Trace flag 1236 not enabled (sql 2014 SP1 and above, TF is not required)', 'This trace flag can help reduce contention on database lock for highly active servers.', '', 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf1236', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('047C814A-5D3D-4652-A2CF-A975399D11BF','Server Performance', 'I','Info', 'NonDefault_sp_configure', 'Some sp_configure values are not set to default.', 'Non Default values do not necessarily mean issues.  But please review Server Configuration report to make sure they are as intended', '', '', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('59B1DB60-ABC2-4981-A9EC-DF901C3A89B4','Server Performance', 'W','Warning', 'usp_RG_Idle', 'high RESOURCE_GOVERNOR_IDLE detected', 'High waits on RESOURCE_GOVERNOR_IDLE were deteted.  This is means CPU cap was configured for Resource Goverrnor and could force query to slowdown.  Make sure CPU cap for Resource Governor is properly configured. ', '','', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('773805A8-5DB4-4132-8488-E8FBDE57C67A','Server Performance', 'W','Warning', 'usp_HighCompile', 'Potential high compiles detected', 'Potential high compilation was detected. Please verify with Perfmon counters data.  This can cause high CPU issues', '','', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('F9BBF034-ACFE-4C98-AD32-6010573AFD3D','Server Performance', 'W','Warning', 'usp_HighCacheCount', 'High Cache Entries detected.', 'High number of SQL Server cache entries (Cache Object Counts) were detected.  This can cause high CPU and spinlock issue.', '','https://support.microsoft.com/en-us/topic/kb3026083-fix-sos-cachestore-spinlock-contention-on-ad-hoc-sql-server-plan-cache-causes-high-cpu-usage-in-sql-server-798ca4a5-3813-a3d2-f9c4-89eb1128fe68', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('64EEE25A-4B20-4C24-8F27-1E967011D69E','Server Performance', 'W','Warning', 'usp_HighStmtCount', 'Some queries had high statement execution count', 'Some queries had high number statement executions.  This makes it challenging to tune the queries. ', '','', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('12145143-A34C-4393-BC77-74E3F3A74D5D','Server Performance', 'W','Warning', 'usp_ExcessiveLockXevent', 'lock_acquired or lock_released xevent was detected. ',  'These events can cause high cpu or other performance issues. Turn lock_acquired or lock_released xevent off if not needed or use for very brief periods', '','', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('F9EF91B9-529B-4F72-8545-59689D43D37E','Server Performance', 'W','Warning', 'usp_McAFee_Intrusion', 'McAFee Host Intrusion Prevenstion loaded in SQL Process',  'Loading McAfee Host Intrusion Prevention into SQL can lead to performance and stability issues ', '','https://docs.microsoft.com/troubleshoot/sql/performance/performance-consistency-issues-filter-drivers-modules', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('F332275E-9CF4-4CFA-935D-AE248B74ADE4','Query Performance', 'W','Warning', 'usp_BatchSort', 'Batch sort is detected in query plan(s)',  'Batch sort can cause high CPU or memory grant issues due to cardinality over-estimation ', '','https://docs.microsoft.com/troubleshoot/sql/performance/decreased-perf-high-cpu-optimized-nested-loop', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('B21D0648-90FD-463B-B32B-C9E710D62B63','Query Performance', 'W','Warning', 'usp_SmallSampledStats', 'Some statistics have sample size less than 5%',  'Default sample size is sufficient for most normal workloads. But unevenly distributed data may require larger sample size or Full Scan sampling.', '','', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('E3CECDDA-EBEB-4E69-940C-660813ED5D93','Query Performance', 'W','Warning', 'usp_DisabledIndex', 'Some indexes are disabled',  'Disabling indexes may cause poor query performance. Check tbl_DisabledIndexes for details ', '','http://aka.ms/nexus/disabledindex', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('63C1DA9B-CAA5-4C9D-9CA0-3916ED6D5F98','Server Performance', 'W','Warning', 'usp_LongAutoUpdateStats', 'Long Auto update stats',  'Some auto statistics update took longer than 60 seconds.  Consider asynchronous stats update ', '','https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_update_statistics_async', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('6A67B697-F1AF-46D2-99D1-E7B6086B4D5D','Server Performance', 'W','Warning', 'usp_AccessCheck', 'Access Check Configuration',  'access check cache bucket count and access check cache quota are not configured per best practice ', '','https://docs.microsoft.com/en-us/troubleshoot/sql/performance/recommended-updates-configuration-options', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('D3E36E57-4DDE-44D3-939F-13D2A2608F02','Server Performance', 'W','Warning', 'usp_RedoThreadBlocked', 'Redo Thread wait ',  'Redo Thread may have waited excessively.  Check tbl_requests for command with DB STARTUP ', '','', '  jackli', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('4FE75D34-9AAE-440E-9758-1ABE2AA7B54D','Server Performance', 'W','Warning', 'usp_VirtualBytesLeak', 'Virtual bytes leak',  'Virtual bytes for SQL process were over 7TB.  This may indicate of virtual bytes leak. Please check perfmon counter.', '','https://support.microsoft.com/kb/3074434', '  jackli', 1, 100, 0)



insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('952A2770-4031-4B4F-B56E-6A3A0970FA26','Server Performance', 'W','Warning', 'usp_DeadlockTraceFlag', 'Trace flag 1222',  'Trace flag 1222 is meant for deadlock troubleshooting only. do NOT leave it on permanently ', 'https://blogs.msdn.microsoft.com/bobsql/2017/05/23/how-it-works-sql-server-deadlock-trace-flag-1222-output/','https://blogs.msdn.microsoft.com/bobsql/2017/05/23/how-it-works-sql-server-deadlock-trace-flag-1222-output/', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('0F58D750-92B4-43A9-BED1-95450EB63175','Server Performance', 'W','Warning', 'usp_PerfScriptsRunningLong', 'Perf scripts running long',  'run time gaps between DMV queries were exceptionally large.  Some of them took more than 120 seconds between runs. check tbl_requests.runtime for details. this can be system issue', '','', '  jackli', 1, 100, 0)


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('062A4FCD-C2D9-4A08-B3B0-C57251223450','Server Performance', 'W','Warning', 'usp_AttendtionCausedBlocking', 'Attention causing blocking',  'Some timeouts/attentions could have caused blocking.  see readtrace.tblInterestingEvents and vw_HEAD_BLOCKER_SUMMARY', '','', '  jackli', 1, 100, 0)





go
		

/********************************************************
owner:   ericbu
*********************************************************/

-- Added 5/27/2015
insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('25678531-4722-48C4-94B0-026C2ED1021F','Server Performance', 'W','Warning', 'usp_HighRecompiles', 'Potential high Recompiles detected, 50+ per second', 'Potential high recompilations were detected. Please verify with perfmon data.  This can cause high CPU issues.', '','', '  ericbu', 1, 100, 0)

-- TBD Service Broker
	


go

/**************************************************************************************************
owner:  jaynar
***************************************************************************************************/

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('47377EC8-BE56-4C92-B0F3-85FC0485D83B','Server Performance', 'W','Warning', 'HugeGrant', 'Huge Memory Grant found', 'Queries with big memory grant found check the detail report', '/Pages1/Memory%20Grants.aspx','', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('FDBF24A1-3EBE-49F1-A02B-FD5686ACDAE9','Server Performance', 'W','Warning', 'Optimizer_Memory_Leak', 'Optimizer Memory Leak', 'MEMORYCLERK_SQLOPTIMIZER memory may be high.  This could be a leak issue which is fixed in SQL 2012 Sp1 CU3', 'http://support.microsoft.com/kb/2803065','http://support.microsoft.com/kb/2803065', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('5E630273-C14F-4DCE-BDA0-24A1FD8E25CA','Server Performance', 'W','Warning', 'usp_IOAnalysis', 'Disk IO Analysis', 'The disk sec/transfer in following drives exceeded 20 ms, check the perfmon for complete analysis', '','https://docs.microsoft.com/en-us/troubleshoot/sql/performance/troubleshoot-sql-io-performance', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('5AE45557-E463-48D6-B135-11AADCB8642F','Server Performance', 'I','Info', 'usp_WarnmissingIndex', 'Missing Index detected', 'There are missing indexes detected.  Please review SQL Nexus report and make recommendations to your customer.', '','', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('0C73F3D4-6CCC-4FC9-AE37-58110F9C15DB','Server Performance', 'I','Info', 'StaleStatswarning2008', 'Stale Stats warning 2008', 'Statistics of some tables has not been updated for over 7 days', '','https://docs.microsoft.com/sql/relational-databases/statistics/statistics#UpdateStatistics', 'jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('010B3DBA-76CC-46C0-AC1B-3CAD09F95891','Server Performance', 'W','Warning', 'usp_SQLHighCPUconsumption', 'SQL High CPU consumption', 'CPU consumption from SQL Server was excessive (>80%) for an extended period of time', '','https://docs.microsoft.com/en-us/troubleshoot/sql/performance/troubleshoot-high-cpu-usage-issues', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('6C82DA17-D04C-4155-8702-19A9A1363A64','Server Performance', 'W','Warning', 'usp_KernelHighCPUconsumption', 'Kernel High CPU consumption', 'Kernel CPU consumption for SQL Server exceeded for an extended period of time.', '','', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('6E19B301-E83B-4E5F-AF4E-A8DD2251C1B6','Server Performance', 'W','Warning', 'usp_Non_SQL_CPU_consumption', 'High non-SQL CPU consumption detected', 'Much of the CPU utilization came from non-SQL Server process(es). Review the perfmon data to identify which processes caused this (Process object)', '','', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('57CAA4BB-C7BD-4F96-8040-3224008A3F39','Server Performance', 'I','Info', 'XEventcrash', 'XEvent may cause SQL Server crash', 'XEvent session retrieving the Query Hash can result in SQL Server shutdown', '','http://support.microsoft.com/kb/3004355', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('607B17FD-98F1-498E-9B93-F16E5A155730','Server Performance', 'W','Warning', 'OracleLinkedServerIssue', 'Oracle Driver SQL Server crash', 'Oracle driver loaded in SQL Server memory space may cause SQL Server to crash, refer the KB for solution', '','https://docs.microsoft.com/en-US/troubleshoot/sql/admin/crashes-run-oracle-linked-server-query', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('BBECDF81-DCCE-4E41-93C9-7EB9E11F53BD','Server Performance', 'W','Warning', 'usp_ExcessiveTrace_Warning', 'Excessive Trace Warning', 'Multiple traces were detected running on the server.  This can negatively impact server performance', '','', '  jaynar', 1, 100, 0)

insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('948756B6-A67F-4CB1-86F9-1B22C26F0B9C','Server Performance', 'W','Warning', 'usp_Many_Traces_Used', 'Excessive Trace events collected', 'Multiple non default trace events  were detected running on the server.  This can negatively impact server performance', '','', '  jaynar', 1, 100, 0)



go

/**************************************************************************************************
owner:  VIRANA

***************************************************************************************************/


insert into tbl_Analysissummary (SolutionSourceId,Category, type, typedesc,Name, FriendlyName, Description, InternalUrl, ExternalUrl, Author, Priority, SeqNum, Status)
values ('6D4B332C-67A0-428D-A08C-A48A5327DE60','Query Performance', 'W','Warning', 'usp_oldce', 'Customer using oldCE for database', 'Customer not taking advantage of newCE', '','https://cloudblogs.microsoft.com/sqlserver/2014/03/17/the-new-and-improved-cardinality-estimator-in-sql-server-2014/', '  virana', 1, 100, 0)




/*************************************************************************************************

creating rules
**********************************************************************************************/

go



/***************************************************************************************************

owner:jackli

****************************************************************************************************/
go
create procedure usp_AttendtionCausedBlocking
as
begin
	if (OBJECT_ID ('[readtrace].[tblInterestingEvents]') is not null) 
	begin
		if exists (select *  from readtrace.tblInterestingEvents evt join  vw_HEAD_BLOCKER_SUMMARY bloc on evt.Session=bloc.head_blocker_session_id where eventid = 16 and evt.StartTime < bloc.runtime)
		begin
			update tbl_AnalysisSummary
			set Status = 1
			where  Name =  OBJECT_NAME(@@PROCID)

		end
	end
end
GO

create procedure usp_PerfScriptsRunningLong
as
if exists (select * from 
(select runtime, lag(runtime, 1, runtime) over (order by runtime) as prev_runtime,
datediff (s, lag(runtime, 1, runtime) over (order by runtime), runtime) gap
from  (select distinct runtime from tbl_REQUESTS ) t ) t2  where gap > 120
)
begin
	update tbl_AnalysisSummary
	set Status = 1
	where  Name =  OBJECT_NAME(@@PROCID)

end
go

create procedure usp_DeadlockTraceFlag
as
IF ((OBJECT_ID ('tbl_TraceFlags') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists (select * from tbl_TraceFlags where TraceFlag in (1204, 1222))
	begin
		update tbl_AnalysisSummary
		set Status = 1
		where  Name =  OBJECT_NAME(@@PROCID)
	end
end
go


create procedure usp_ChangeTableCauseHighCPU
as
if (OBJECT_ID ('[readtrace].[tblUniqueBatches]') is not null) 
begin
	if exists (select * from readtrace.tblUniqueBatches where NormText like '%CHANGETABLE%') or exists (select * from readtrace.tblUniqueStatements where NormText like '%CHANGETABLE%')
	begin
		update tbl_AnalysisSummary
		set Status = 1
		where  Name =  OBJECT_NAME(@@PROCID)

	end
end
go

create procedure usp_RedoThreadBlocked
as
if exists (select * from tbl_requests where command = 'DB STARTUP' and wait_duration_ms > 15000)
begin
	update tbl_AnalysisSummary
	set Status = 1
	where  Name =  OBJECT_NAME(@@PROCID)

end
go
--https://support.microsoft.com/en-us/help/3074434/fix-out-of-memory-error-when-the-virtual-address-space-of-the-sql-serv
create procedure usp_VirtualBytesLeak
as
if (OBJECT_ID ('CounterDetails') is not null) and (OBJECT_ID ('CounterData') is not null) 
begin
	if exists (select   max(countervalue) from CounterDetails a join CounterData b on a.CounterID=b.CounterID  where CounterName ='Virtual Bytes' and InstanceName='sqlservr' and ObjectName='Process'  having   max(countervalue) > 7000000000000)
	begin
		update tbl_AnalysisSummary
		set Status = 1
		where  Name =  OBJECT_NAME(@@PROCID)

	end
end
go

-- Changed usp_AccessCheck SP code to make sure we do not check this for SQL SERVER 2016 and later version

create procedure usp_AccessCheck
as
IF ((OBJECT_ID ('tbl_ServerProperties') IS NOT NULL) and (OBJECT_ID ('tbl_Sys_Configurations') IS NOT NULL) 
	and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	IF EXISTS (select * from tbl_ServerProperties where PropertyName='MajorVersion' and PropertyValue<13)
	begin

		if not exists ( select * from tbl_Sys_Configurations where name= 'access check cache bucket count' and value_in_use = 256 )  or  not exists (select * from tbl_Sys_Configurations where name='access check cache quota' and value_in_use = 1024)
		begin
			update tbl_AnalysisSummary
			set Status = 1
			where  Name =  OBJECT_NAME(@@PROCID)
		end
	end
end

go

create procedure usp_LongAutoUpdateStats
as
begin
if ((OBJECT_ID ('[readtrace].[tblInterestingEvents]') is not null) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	declare @statstext nvarchar(max)
	select @statstext = textdata  from readtrace.tblInterestingEvents  where eventid=58 and (duration/1000.00/1000.00) > 60 order by duration desc
		if @statstext is not null
			begin
				update tbl_AnalysisSummary
				set status = 1, Description = 'Some auto statistics update took longer than 60 seconds.  Consider asynchronous stats update.' +  @statstext + ' is an example'
				where Name='usp_LongAutoUpdateStats'
			end
	end
end
go


create procedure usp_DisabledIndex
as
IF ((OBJECT_ID ('tbl_DisabledIndexes') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists ( select  *  from tbl_DisabledIndexes)
	begin
		update tbl_AnalysisSummary
		set status = 1
		where Name='usp_DisabledIndex'
	end
end

go
create procedure usp_SmallSampledStats
as
IF ((OBJECT_ID ('tbl_dm_db_stats_properties') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists ( select  *  from tbl_dm_db_stats_properties where (rows_sampled * 100.00)/ [rows] < 5.0 )
	begin
		update tbl_AnalysisSummary
		set status = 1, [Description]=[Description] + ' use query select *   from tbl_dm_db_stats_properties where (rows_sampled * 100.00)/ [rows] < 5.0 to identify tables with small sample sizes'
		where Name='usp_SmallSampledStats'
	end
end

go
create procedure usp_BatchSort
 as
  declare @filename nvarchar(max), @Optimized int
 select  top 1 @filename = [FileName],  @Optimized= c.value('@Optimized[1]', 'int')  from   (select FileName, cast(FileContent as xml) QueryPlan  from tblTopSqlPlan)  a cross apply a.QueryPlan.nodes('declare namespace SP="http://schemas.microsoft.com/sqlserver/2004/07/showplan";//SP:NestedLoops') as t(c)
 where c.value('@Optimized[1]', 'int') = 1
 if ( @fileName is not null)
 begin
	update tbl_AnalysisSummary
	set
	status = 1, 
	description =  @filename + ' is an example query plan'
	where name = 'usp_BatchSort'
 end

go

create procedure usp_McAFee_Intrusion
as
IF ((OBJECT_ID ('tbl_dm_os_loaded_modules') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists (select name from tbl_dm_os_loaded_modules  where name like '%HcThe%' or name like '%HcApi%' or name like '%HcSql%')
	begin
		update tbl_AnalysisSummary 		set [status]=1  		where name ='usp_McAFee_Intrusion'
	end
end

go

create procedure usp_ExcessiveLockXevent
as
IF ((OBJECT_ID ('tbl_Xevents') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists (select * from tbl_Xevents where event_name in ('lock_released', 'lock_acquired'))
	begin
		update tbl_AnalysisSummary	set [status]=1  		where name ='usp_ExcessiveLockXevent'
	end
end

go
create procedure usp_HighCacheCount 
as
begin
	if (OBJECT_ID ('CounterDetails') is not null) and (OBJECT_ID ('CounterData') is not null) 
	begin
		if ( select max(cast(countervalue as float)) from CounterDetails c join CounterData dat on c.CounterID = dat.CounterID where CounterName = 'Cache Object Counts') > 100000
		begin
			update tbl_analysissummary
			set [status]=1
			where name ='usp_HighCacheCount'
		end 
	end
end

go
create procedure usp_HighStmtCount
as
begin
	if (OBJECT_ID ('[readtrace].[tblStatements]') is not null) 
	begin
		if (select max(StmtCount) from (select BatchSeq , count (*) 'StmtCount' from readtrace.tblStatements group by BatchSeq ) t) > 1000
		 begin

			declare @query nvarchar(max)

			select @query = NormText from readtrace.tblBatches bat join readtrace.tblUniqueBatches ub on bat.hashid = ub.HashID
			where bat.BatchSeq in (
			select top 1 bat.BatchSeq from readtrace.tblStatements st join readtrace.tblBatches bat on st.BatchSeq = bat.BatchSeq 
			group by bat.BatchSeq
			order by count (*)  desc
			)

			update tbl_Analysissummary  set [status] = 1,
			[Description] = [Description] + ' here is an example query: ' + @query
			where Name = 'usp_HighStmtCount'
		 end
	 end
end
go

create function dbo.fn_CPUthresheldCrossed()
returns int
as
begin
	declare @ret int
	declare @cpu_count varchar(max)  
	 select @cpu_count= PropertyValue  from tbl_ServerProperties where PropertyName='cpu_count'
	if  (cast(@cpu_count as int) >= 32)
	begin 
		set @ret=1
	end
	else
	begin
		set @ret=0
	end
	return @ret
end
go
create procedure proc_CheckTraceFlags
as
IF ((OBJECT_ID ('tbl_ServerProperties') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if (dbo.fn_CputhresheldCrossed() = 1)
		begin
			declare @raise bit = 1
			if exists (select * from tbl_ServerProperties where PropertyName='MajorVersion' and  cast(PropertyValue as int)  >=13)
				set @raise = 0
			if  exists (select * from tbl_ServerProperties where PropertyName='MajorVersion' and  cast(PropertyValue as int)  =12  )  and exists (select *   from tbl_ServerProperties where PropertyName='ProductLevel' and PropertyValue>='SP2')
				set @raise = 0

			if @raise = 1 and  not exists (select * from tbl_traceflags where TraceFlag = 4199)
					update tbl_Analysissummary  set [status] = 1 	where Name = 'Trace Flag 4199'
		
			if @raise = 1 and @raise = 1 and not exists (select * from tbl_traceflags where TraceFlag = 1118)
				update tbl_Analysissummary  set [status] = 1 	where Name = 'Trace Flag 1118'

			if @raise = 1 and not exists (select * from tbl_traceflags where TraceFlag = 8048)
				update tbl_Analysissummary  set [status] = 1 	where Name = 'Trace Flag 8048'

			if @raise = 1 and not exists (select * from tbl_traceflags where TraceFlag = 1236)
				update tbl_Analysissummary  set [status] = 1 	where Name = 'Trace Flag 1236'
			if @raise = 1 and not exists (select * from tbl_traceflags where TraceFlag = 9024)
				update tbl_Analysissummary  set [status] = 1 	where Name = 'Trace Flag 9024'
		end
end
go

create procedure usp_HighCompile  --author:jackli
as
begin
	if (OBJECT_ID ('CounterDetails') is not null) and (OBJECT_ID ('CounterData') is not null) 
	begin
		if ( select max(cast(countervalue as float)) from CounterDetails c join CounterData dat on c.CounterID = dat.CounterID where CounterName = 'SQL Compilations/sec') > 300
		begin
			update tbl_analysissummary
			set [status]=1
			where name ='usp_HighCompile'
		end 
	end
end

go
create procedure usp_RG_Idle 
as
begin

if (select max(waiting_tasks_count) from tbl_OS_WAIT_STATS where wait_type = 'RESOURCE_GOVERNOR_IDLE') >  10000000 
begin
	update tbl_analysissummary
	set [status]=1
	where name ='usp_RG_Idle'
end 

end
go

create procedure proc_ExcessiveXevents
as
IF ((OBJECT_ID ('tbl_XEvents') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists (
		select event_name from tbl_XEvents
		where  event_name in
		('query_pre_execution_showplan', 'query_post_execution_showplan','query_post_compilation_showplan','lock_acquired','sql_statement_starting','sql_statement_completed','sp_statement_starting','sp_statement_completed')
	)
	begin
		update tbl_analysissummary
		set [status]=1
		where name ='Detailed XEvent Tracing'
	end
end

go

create procedure proc_PowerPlan
as

IF ((OBJECT_ID ('tbl_PowerPlan') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if not exists (select * from tbl_PowerPlan where ActivePlanName like '%High Performance%')
	begin
		update 	 tbl_AnalysisSummary
		set status = 1
		where name = 'PowerPlan'
	end
end

go

create procedure proc_AutoStats
as
IF ((OBJECT_ID ('tbl_SysDatabases') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	update tbl_SysDatabases set is_auto_update_stats_on = 0
	where name = 'notexist'
	select * from tbl_AnalysisSummary
	if exists (select * from tbl_SysDatabases where is_auto_create_stats_on=0)
		update tbl_AnalysisSummary 
		set [status]=1
		where Name='AutoCreateStats'


	if exists (select * from tbl_SysDatabases where is_auto_update_stats_on=0)
		update tbl_AnalysisSummary 
		set [status]=1
		where Name='AutoUpdateStats'
end

go


create procedure proc_nondefaultConfigDetected
as
IF ((OBJECT_ID ('tbl_Sys_Configurations') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
begin
	if exists (
		select value_in_use from tbl_Sys_Configurations con join tblDefaultConfigures def on con.name = def.[Configuration Option]
		where value_in_use <> DefaultOption
		and name not in ('show advanced options', 'Agent XPs','show advanced options')
	)
	begin
		update tbl_AnalysisSummary
		set [Status] = 1
		where Name = 'NonDefault_sp_configure'
	end

end


go



Create procedure HugeGrant 
as 
Begin
	declare @i  int
	set @i = 0
	select @i= Count(*)  from sys.objects where name = ltrim(rtrim('tbl_dm_exec_query_memory_grants'))
	if @i > 0 
	begin
		set @i = 0
		select @i= count(*) from dbo.tbl_dm_exec_query_memory_grants
		where   granted_memory_kb/1024 > 1024
		if @i > 0 
		begin
			 
			update tbl_AnalysisSummary
				set [Status] = 1
				where Name = 'HugeGrant'
		end
	end
End 

go

create  procedure  Optimizer_Memory_Leak
as
begin 
             DECLARE @SQLVersion  varchar(100)
             declare @is_Rulehit int
             declare @t_MajorVersion int
             declare @t_MinorVersion int
             --declare the delimeter between each minor version
             DECLARE @Delimeter char(1)
             SET @Delimeter = '.'
             Set @t_MajorVersion = 0
             Set @t_MinorVersion = 0
             --Parse the string and insert each  minor version into the @tblSQLVersion  table
             DECLARE @tblSQLVersion  TABLE(id int identity,tVersion varchar(50))
             DECLARE @tSQLVersion  varchar(50)
             DECLARE @StartPos int, @Length int
                    set @is_Rulehit = 0
             declare @i  int
             set @i = 0
             select @i= Count(*)  from sys.objects where name =  'tbl_SCRIPT_ENVIRONMENT_DETAILS' 
 
             if @i > 0     
                    begin
                           set @is_Rulehit = 1
                           SELECT @SQLVersion = value
                             FROM [tbl_SCRIPT_ENVIRONMENT_DETAILS]
                             where name like '%SQL Version%'
                    end
           if ( @is_Rulehit > 0)
             begin
                    WHILE LEN(@SQLVersion ) > 0
                      BEGIN
                           SET @StartPos = CHARINDEX(@Delimeter, @SQLVersion )
                           IF @StartPos < 0 SET @StartPos = 0
                           SET @Length = LEN(@SQLVersion ) - @StartPos - 1
                           IF @Length < 0 SET @Length = 0
                           IF @StartPos > 0
                             BEGIN
                                 SET @tSQLVersion  = SUBSTRING(@SQLVersion , 1, @StartPos - 1)
                                 SET @SQLVersion  = SUBSTRING(@SQLVersion , @StartPos + 1, LEN(@SQLVersion ) - @StartPos)
                             END
                           ELSE
                             BEGIN
                                 SET @tSQLVersion  = @SQLVersion 
                                 SET @SQLVersion  = ''
                             END
                           INSERT @tblSQLVersion  (tVersion) VALUES(@tSQLVersion )
                    END

                    --Get minor version from  @tblSQLVersion  table
                    SELECT @t_MajorVersion= convert (int,tVersion) FROM @tblSQLVersion where id = 1
                    SELECT @t_MinorVersion=  convert (int,tVersion)  FROM @tblSQLVersion where id = 3
                    if (@t_MajorVersion =11 and @t_MinorVersion < 2331)
                    begin
						update tbl_AnalysisSummary
						set [Status] = 1
						where Name = 'Optimizer_Memory_Leak'
					end       
       end
       
end
go

/****** Object:  StoredProcedure [dbo].[usp_IOAnalysis]    Script Date: 1/26/2022 3:03:56 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE  procedure  [dbo].[usp_IOAnalysis] 
as
begin
    IF ((OBJECT_ID ('counterdata') IS NOT NULL) and (OBJECT_ID ('counterdetails') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
	begin

		declare @max_sec_transfer decimal (12,3)	
		
		declare @IO_threshold decimal (12,3)
		declare @T_CounterDateTime datetime
		set @IO_threshold = 0.020

		Create table #tmp (CounterDateTime datetime, CounterValue decimal (12,3))

		insert into #tmp (CounterDateTime, CounterValue) 
		SELECT convert(datetime, CounterDateTime), CounterValue
		FROM counterdata dat INNER JOIN counterdetails dl ON dat.counterid = dl.counterid  
		WHERE dl.objectname in ('logicaldisk') 
				AND dl.countername in ('Avg. Disk sec/Transfer')
				AND counterValue >= @IO_threshold

		IF (@@ROWCOUNT > 0 )
		begin
             
			declare C_CounterDateTime cursor for select DISTINCT CounterDateTime from #tmp

			open C_CounterDateTime
			fetch next from C_CounterDateTime into @T_CounterDateTime

			while (@@fetch_status = 0)
			Begin
                                                            
				--insert into #tmpDisplayRecords (ObjectName,CounterName,InstanceName,avg)
				IF EXISTS 
				(SELECT 1
					FROM counterdata dat INNER JOIN counterdetails dli 
						ON dat.counterid = dli.counterid  
					WHERE dli.objectname in ('logicaldisk') 
						and  dli.countername in ('Avg. Disk sec/Transfer')  
					and ltrim(rtrim(SUBSTRING(COALESCE (InstanceName, ''), 1,10))) <> '_Total'
					and CounterDateTime between (@T_CounterDateTime - '00:00:30')  and (@T_CounterDateTime  + '00:00:30') 
					GROUP BY ObjectName, CounterName, InstanceName                      
					HAVING    cast (avg(counterValue)  as decimal (12,3))  >= @IO_threshold
                )
				begin 
					SELECT @max_sec_transfer = countervalue
					FROM #tmp

					update tbl_AnalysisSummary
						set [Status] = 1,
						Description = 'The "Avg. Disk sec/transfer" on some drives exceeded 20 ms, with max value found = '+ convert (varchar, @max_sec_transfer) + ' sec/transfer. Check the Perfmon for complete analysis'
						where Name = 'usp_IOAnalysis'
					
					BREAK -- we found one value, so we can quit the loop
				end 
				                                    
				fetch next from C_CounterDateTime into @T_CounterDateTime
			end
        
			close C_CounterDateTime
			deallocate C_CounterDateTime
                           
			drop table #tmp
		end
	end
end
go

Create procedure [usp_WarnmissingIndex]  
as 
begin 
		declare @t_DisplayMessage nvarchar(1256) 
		declare @t_improvement_measure varchar(100) 
		declare @t_create_index_statement varchar(100) 
		declare @is_Rulehit int 
		declare @message_Number int 
		set @is_Rulehit = 0 
		set @t_DisplayMessage = '' 
		set @message_Number = 1 
		Create table #tmp (improvement_measure varchar(100),create_index_statement varchar(5000)) 
		declare @i  int 
		set @i = 0 
			select @i= Count(*)  from sys.objects where name =  'tbl_MissingIndexes'  
		if @i > 0  
			begin 
				insert into #tmp (improvement_measure,create_index_statement)select top 5 improvement_measure, create_index_statement  
				from dbo.tbl_MissingIndexes  
				where improvement_measure > 1000000 
				order by improvement_measure desc 
			end 
		select  @is_Rulehit = COUNT(*) from #tmp 
		if ( @is_Rulehit > 0) 
			begin 
				update tbl_AnalysisSummary
				set [Status] = 1
				where Name = 'usp_WarnmissingIndex'
			
			end 

		drop table #tmp 
		end

go


Create procedure StaleStatswarning2008
as 
begin 

	IF OBJECT_ID ('tbl_SysIndexes') IS NOT NULL 
	BEGIN
		/*Begin  */ 
		declare @t_DisplayMessage nvarchar(1500) 
		declare @t_DB_Name_orID varchar(100) 
		declare @t_Number_ofObjects varchar(100) 
		declare @is_Rulehit int 
		set @is_Rulehit = 0 
		set @t_DisplayMessage = '' 
		Create table #tmp (DB_Name_orID varchar(100),Number_ofObjects varchar(100)) 
		declare @i  int 
		set @i = 0 
		select @i= Count(*)  FROM sysobjects WHERE   ltrim(rtrim(name)) in ( 'tbl_SPHELPDB' , 'tbl_SysIndexes' ) 
		if @i > 1  
		begin 

			insert into #tmp (DB_Name_orID,Number_ofObjects) select top 4 
			( select distinct name  from dbo.tbl_SPHELPDB a where a.dbid=b.dbid) as  DBNAME , COUNT(*) as [Number_of_objects]  
			from dbo.tbl_SysIndexes b 
			where  stats_updated < (SELECT CAST(Value as datetime) FROM [tbl_SCRIPT_ENVIRONMENT_DETAILS] where name like '%Script Begin Time%') 
			and convert (bigint ,dbid   ) > 4 
			group by dbid 
			order by 1  
		end 
		else  
			begin  
			insert into #tmp (DB_Name_orID,Number_ofObjects) select top 4 dbid, COUNT(*) as [Number_of_objects]   
			from dbo.tbl_SysIndexes b 
			where  stats_updated < (SELECT CAST(Value as datetime) FROM [tbl_SCRIPT_ENVIRONMENT_DETAILS] where name like '%Script Begin Time%') 
			and convert (bigint ,dbid   ) > 4 
			group by dbid 
			order by 1   
			 End 
		select  @is_Rulehit = COUNT(*) from #tmp 
		if ( @is_Rulehit > 0) 
			begin 
				update tbl_AnalysisSummary
					set [Status] = 1
					where Name = 'StaleStatswarning2008'
			end 
			drop table #tmp 
	END
end 
Go 


create procedure  [usp_SQLHighCPUconsumption]  
as
set nocount on
begin
	IF ((OBJECT_ID ('counterdata') IS NOT NULL) and (OBJECT_ID ('counterdetails') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
	begin
		declare @t_CBeginTime datetime 
		declare @is_Rulehit int 
		declare @cpuCount int 
		declare @CPU_threshold decimal (20,2) 
		declare @T_CounterDateTime datetime
		declare @t_AvgValue int 
		declare @InstanceIndex int

		set @is_Rulehit = 0 
		set @cpuCount = 1 
	

		create table #tmpCounterDateTime (CounterDateTime datetime, InstanceIndex int) 

		--get the CPUs

		--try to find CPU count different possible ways.  if we get zero or null, try another way
		
		DECLARE @schedCount int = 0
		IF EXISTS (SELECT 1 FROM sys.Tables WHERE NAME = N'tbl_ServerProperties')
		--#1 try 
		BEGIN
			--make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
			DECLARE @cpuRows int = 0;
			--unless there is more than 1 row
			SELECT DISTINCT @cpuRows = COUNT(PropertyValue)
			FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
			IF (@cpuRows = 1) --then we are OK to use the number
			BEGIN
				SELECT DISTINCT @cpuCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
				SELECT DISTINCT @schedCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('number of visible schedulers')
			END
		END -- END #1
		-- #2 try
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			--consider doing this now 
			IF (OBJECT_ID ('counterdetails') IS NOT NULL)
			BEGIN
				IF 0 = (SELECT COUNT(*)
					FROM sys.indexes 
					WHERE object_id = OBJECT_ID('dbo.counterdetails')
					AND name='procCount_idx')
				BEGIN
					CREATE INDEX procCount_idx on counterdetails (objectname)
					INCLUDE (countername, instancename)
					WHERE objectname in ('Processor Information')
						AND countername in ( '% User Time')
				END
				--as long as table existed, and we either created index or it is already there, try to get CPUs
				SELECT @cpuCount = count (DISTINCT InstanceName)
				FROM counterdata dat INNER JOIN counterdetails dli on dat.counterid = dli.counterid
				WHERE dli.objectname in ('Processor Information')
					AND dli.countername in ( '% User Time')
					AND dli.InstanceName not like ('%_Total%')
			END
		END  --END #2
		-- #3 TRY  ******
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			SELECT @cpuCount = cast(max(countervalue)/100 as int)
			  FROM [dbo].[CounterData] cdat JOIN [dbo].[CounterDetails] cdet ON cdat.CounterID = cdet.CounterID
			  WHERE ObjectName='Process' 
				  AND CounterName='% Processor Time' 
				  AND InstanceName = '_Total' and countervalue !=0
		END --END #3

		--did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly
		
		IF (@cpuCount = 0 OR @cpuCount is null)
			RETURN

		--set threshold at 80% of total CPU capacity
		set @CPU_threshold = 80 * @cpuCount

 		insert into #tmpCounterDateTime (CounterDateTime, InstanceIndex) 
		select convert(datetime, CounterDateTime), isnull (InstanceIndex, 0)    
		from counterdata dat 
				inner join counterdetails detl on dat.counterid = detl.counterid   
		where detl.objectname in ('Process')   
			and  detl.countername in ( '% User Time')  
			and detl.InstanceName like 'sqlservr%' 
			and    counterValue > @CPU_threshold 

		create clustered index dt_idx_kernel on #tmpCounterDateTime (CounterDateTime)

		select  @is_Rulehit = COUNT(*), @t_CBeginTime = min (CounterDateTime) from #tmpCounterDateTime 

		if ( @is_Rulehit > 0) 
		begin 
			declare C_CounterDateTime cursor for select CounterDateTime, InstanceIndex from #tmpCounterDateTime 

			open C_CounterDateTime 
			
			fetch next from C_CounterDateTime into @T_CounterDateTime, @InstanceIndex

			while (@@fetch_status = 0) 
			begin 

				--approximate algorithm - if avg CPU usage over a 3 min period exceeds the threshold - this means is prolonged
				--walk the time windows one at a time in a cursor
				select  @t_AvgValue =avg( cast (counterValue as bigint ))      
				from counterdata dat inner join counterdetails dli on dat.counterid = dli.counterid   
				where dli.objectname in ('Process' ) 
					and  dli.countername in ( '% User Time')  
					and dli.InstanceName like 'sqlservr%' 
					and CounterDateTime between (@T_CounterDateTime - '00:01:30')  and (@T_CounterDateTime  + '00:01:30') 
					and isnull (dli.InstanceIndex, 0) = @InstanceIndex  -- this would impact perf potentially, but alternatives are ugly
				

				if (@t_AvgValue > @CPU_threshold) 
				begin 

					update tbl_AnalysisSummary
					set [Status] = 1, 
					Description =  'CPU utilization from one or more SQL Server(s) was at least ' + convert(varchar, ROUND(@t_AvgValue/@cpuCount,0)) + '% of overall capacity for an extended period of time (3 min)'
					where Name = 'usp_SQLHighCPUconsumption'
					
					--if we found one event of extended CPU utilization, break
					break
				end 
				
				fetch next from C_CounterDateTime into @T_CounterDateTime , @InstanceIndex
			end 
			
			close C_CounterDateTime 
			deallocate C_CounterDateTime 
		end 
		
		drop table #tmpCounterDateTime 
	end
end

GO

Create procedure  [usp_KernelHighCPUconsumption]  
as 
set nocount on
begin 
    IF ((OBJECT_ID ('counterdata') IS NOT NULL) and (OBJECT_ID ('counterdetails') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
	begin
		DECLARE @is_Rulehit int 
		DECLARE @t_CBeginTime datetime  
		DECLARE @CPU_threshold decimal (38,2) 
		DECLARE @T_CounterDateTime datetime
		DECLARE @t_AvgValue int 
		DECLARE @cpuCount int = 0
		DECLARE @InstanceIndex int
		DECLARE @schedCount int = 0

		SET @is_Rulehit = 0 

		
		--get the CPUs
		--try to find CPU count different possible ways.  if we get zero or null, try another way
		
		
		IF EXISTS (SELECT 1 FROM sys.Tables WHERE NAME = N'tbl_ServerProperties')
		--#1 try 
		BEGIN
			--make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
			DECLARE @cpuRows int = 0;
			--unless there is more than 1 row
			SELECT DISTINCT @cpuRows = COUNT(PropertyValue)
			FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
			IF (@cpuRows = 1) --then we are OK to use the number
			BEGIN
				SELECT DISTINCT @cpuCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
				SELECT DISTINCT @schedCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('number of visible schedulers')
			END
		END -- END #1
		-- #2 try
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			--consider doing this now 
			IF (OBJECT_ID ('counterdetails') IS NOT NULL)
			BEGIN
				IF 0 = (SELECT COUNT(*)
					FROM sys.indexes 
					WHERE object_id = OBJECT_ID('dbo.counterdetails')
					AND name='procCount_idx')
				BEGIN
					CREATE INDEX procCount_idx on counterdetails (objectname)
					INCLUDE (countername, instancename)
					WHERE objectname in ('Processor Information')
					AND countername in ( '% User Time')
				END
				--as long as table existed, and we either created index or it is already there, try to get CPUs
				SELECT @cpuCount = count (DISTINCT InstanceName)
				FROM counterdata dat INNER JOIN counterdetails dli on dat.counterid = dli.counterid
				WHERE dli.objectname in ('Processor Information')
					AND dli.countername in ( '% User Time')
					AND dli.InstanceName not like ('%_Total%')
			END
		END  --END #2
		-- #3 TRY  ******
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			SELECT @cpuCount = cast(max(countervalue)/100 as int)
			  FROM [dbo].[CounterData] cdat JOIN [dbo].[CounterDetails] cdet ON cdat.CounterID = cdet.CounterID
			  WHERE ObjectName='Process' 
				  AND CounterName='% Processor Time' 
				  AND InstanceName = '_Total' and countervalue !=0
		END --END #3

		--did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly
		
		IF (@cpuCount = 0 OR @cpuCount is null)
			RETURN

		--set CPU threshold
		set @CPU_threshold = 30.0 * @cpuCount

		--create table #tmp (cnt_avg int, b_CounterDateTime datetime, e_CounterDateTime datetime,Outmsg varchar(100)) 
		create table #tmpCounterDateTime (CounterDateTime datetime, InstanceIndex int) 
	
	
		insert into #tmpCounterDateTime (CounterDateTime, InstanceIndex) 
		select convert(datetime, CounterDateTime), isnull (InstanceIndex, 0)   
		from	counterdata dat 
			inner join counterdetails dli on dat.counterid = dli.counterid   
		where dli.objectname in ('Process') --'physicaldisk','Processor' 
				and dli.countername in ( '% Privileged Time')  
				and dli.InstanceName like 'sqlservr%' 
				and counterValue  > @CPU_threshold  

		create clustered index dt_idx_sqluser on #tmpCounterDateTime (CounterDateTime)
		
		select  @is_Rulehit = COUNT(*), @t_CBeginTime = min (CounterDateTime) from #tmpCounterDateTime 

		if ( @is_Rulehit > 0) 
		begin  
			DECLARE c_counterdatetime CURSOR FOR
			  SELECT counterdatetime, InstanceIndex
			  FROM   #tmpcounterdatetime

			OPEN c_counterdatetime

			FETCH next FROM c_counterdatetime INTO @T_CounterDateTime, @InstanceIndex

			WHILE ( @@fetch_status = 0 )
			  BEGIN
				  SELECT @t_AvgValue = Cast (Avg(countervalue) AS DECIMAL (20, 2))
				  FROM   counterdata dat
						 INNER JOIN counterdetails dli
								 ON dat.counterid = dli.counterid
				  WHERE  dli.objectname IN ( 'Process' ) --'physicaldisk','Processor' 
						 AND dli.countername IN ( '% Privileged Time' )
						 AND dli.instancename LIKE 'sqlservr%'
						 AND counterdatetime BETWEEN (@T_CounterDateTime - '00:01:30' )
													 AND ( @T_CounterDateTime + '00:01:30')
						and isnull (dli.InstanceIndex, 0) = @InstanceIndex  -- this would impact perf potentially, but alternatives are ugly

				  IF ( Cast (@t_AvgValue AS DECIMAL (38, 2)) > @CPU_threshold )
					BEGIN
						update tbl_AnalysisSummary
						set [Status] = 1,
						Description =  'Kernel CPU utilization from SQL Server was at least ' + convert(varchar, ROUND(@t_AvgValue/@cpuCount,0)) + '% of overall capacity for an extended period of time (3 min.)'
						where Name = 'usp_KernelHighCPUconsumption'

						--if we found one event of extended CPU utilization, break
						break
					END

				  FETCH next FROM c_counterdatetime INTO @T_CounterDateTime, @InstanceIndex
			  END

			CLOSE c_counterdatetime
			DEALLOCATE c_counterdatetime 

		end 
		drop table #tmpCounterDateTime 
	end
end 

go 

CREATE PROCEDURE usp_Non_SQL_CPU_consumption
as
SET NOCOUNT ON
begin
	IF ((OBJECT_ID ('counterdata') IS NOT NULL) and (OBJECT_ID ('counterdetails') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
	begin
		declare @t_CBeginTime datetime 
		declare @is_Rulehit int 
		declare @cpuCount int 
		declare @CPU_threshold decimal (20,2) 
		declare @T_CounterDateTime datetime
		declare @t_AvgValue int 
		

		set @is_Rulehit = 0 
		set @cpuCount = 1 
	
		create table #tmpCounterDateTime (CounterDateTime datetime, NonSQLCpu int)

		--get the CPUs
		--try to find CPU count different possible ways.  if we get zero or null, try another way

		DECLARE @schedCount int = 0
		IF EXISTS (SELECT 1 FROM sys.Tables WHERE NAME = N'tbl_ServerProperties')
		--#1 try 
		BEGIN
			--make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
			DECLARE @cpuRows int = 0;
			--unless there is more than 1 row
			SELECT DISTINCT @cpuRows = COUNT(PropertyValue)
			FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
			IF (@cpuRows = 1) --then we are OK to use the number
			BEGIN
				SELECT DISTINCT @cpuCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('cpu_count')
				SELECT DISTINCT @schedCount = PropertyValue
				FROM [tbl_ServerProperties] WHERE PropertyName in ('number of visible schedulers')
			END
		END -- END #1
		-- #2 try
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			--consider doing this now 
			IF (OBJECT_ID ('counterdetails') IS NOT NULL)
			BEGIN
				IF 0 = (SELECT COUNT(*)
					FROM sys.indexes 
					WHERE object_id = OBJECT_ID('dbo.counterdetails')
					AND name='procCount_idx')
				BEGIN
					CREATE INDEX procCount_idx on counterdetails (objectname)
					INCLUDE (countername, instancename)
					WHERE objectname in ('Processor Information')
					AND countername in ( '% User Time')
				END
				--as long as table existed, and we either created index or it is already there, try to get CPUs
				SELECT @cpuCount = count (DISTINCT InstanceName)
				FROM counterdata dat INNER JOIN counterdetails dli on dat.counterid = dli.counterid
				WHERE dli.objectname in ('Processor Information')
					AND dli.countername in ( '% User Time')
					AND dli.InstanceName not like ('%_Total%')
			END
		END  --END #2
		-- #3 TRY  ******
		IF @cpuCount = 0 OR @cpuCount is null
		-- still didnt get CPUs, try again
		BEGIN
			SELECT @cpuCount = cast(max(countervalue)/100 as int)
			  FROM [dbo].[CounterData] cdat JOIN [dbo].[CounterDetails] cdet ON cdat.CounterID = cdet.CounterID
			  WHERE ObjectName='Process' 
				  AND CounterName='% Processor Time' 
				  AND InstanceName = '_Total' and countervalue !=0
		END --END #3

		--did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly
		
		IF (@cpuCount = 0 OR @cpuCount is null)
			RETURN

		--set threshold at 80% of total CPU capacity
		set @CPU_threshold = 80 * @cpuCount

 		insert into #tmpCounterDateTime (CounterDateTime, NonSQLCpu) 
		SELECT CONVERT(datetime, CounterDateTime), SUM (dat.CounterValue) 
		FROM counterdata dat 
				INNER JOIN counterdetails detl on dat.counterid = detl.counterid   
		WHERE detl.objectname in ('Process')   
				and  detl.countername in ('% User Time')  
                and (detl.InstanceName not like '%_Total%'
				and detl.InstanceName not like 'sqlservr%'  
				and detl.InstanceName != 'Idle')
		GROUP BY CounterDateTime


		create clustered index dt_idx_nonsqlcpu on #tmpCounterDateTime (CounterDateTime)

		select  @is_Rulehit = COUNT(*), @t_CBeginTime = min (CounterDateTime) from #tmpCounterDateTime 

		if ( @is_Rulehit > 0) 
		begin 
			declare C_CounterDateTime cursor for 
				SELECT CounterDateTime FROM #tmpCounterDateTime WHERE NonSQLCpu > @CPU_threshold 

			open C_CounterDateTime 
			
			fetch next from C_CounterDateTime into @T_CounterDateTime

			while (@@fetch_status = 0) 
			begin 

				--approximate algorithm - if avg CPU usage over a 3 min period exceeds the threshold - this means is prolonged
				--walk the time windows one at a time in a cursor
				SELECT  @t_AvgValue = AVG (NonSQLCpu)      
				FROM #tmpCounterDateTime
				WHERE CounterDateTime between (@T_CounterDateTime - '00:01:30')  and (@T_CounterDateTime  + '00:01:30') 
				
				if (@t_AvgValue > @CPU_threshold) 
				begin 
					update tbl_AnalysisSummary
					set [Status] = 1, 
					Description =  'Non-SQL CPU utilization on the system was at least ' + convert(varchar, ROUND(@t_AvgValue/@cpuCount,0)) + '% of overall CPU capacity for an extended period of time (3 min.)'
					where Name = 'usp_Non_SQL_CPU_consumption'
					
					--if we found one event of extended CPU utilization, break
					break
				end 
				
				fetch next from C_CounterDateTime into @T_CounterDateTime
			end 
			
			close C_CounterDateTime 
			deallocate C_CounterDateTime 
		end 
		
		drop table #tmpCounterDateTime 
	end
end
 
go


create  procedure  XEventcrash
as
begin 
             DECLARE @SQLVersion  varchar(100)
             declare @is_Rulehit int
             declare @t_MajorVersion int
             declare @t_MinorVersion int
             --declare the delimeter between each minor version
             DECLARE @Delimeter char(1)
             SET @Delimeter = '.'
             Set @t_MajorVersion = 0
             Set @t_MinorVersion = 0
             --Parse the string and insert each  minor version into the @tblSQLVersion  table
             DECLARE @tblSQLVersion  TABLE(id int identity,tVersion varchar(50))
             DECLARE @tSQLVersion  varchar(50)
             DECLARE @StartPos int, @Length int
                    set @is_Rulehit = 0
             declare @i  int
             set @i = 0
             select @i= Count(*)  from sys.objects where name =  'tbl_SCRIPT_ENVIRONMENT_DETAILS' 
 
             if @i > 0     
                    begin
                           set @is_Rulehit = 1
                           SELECT @SQLVersion = value
                             FROM [tbl_SCRIPT_ENVIRONMENT_DETAILS]
                             where name like '%SQL Version%'
                    end
           if ( @is_Rulehit > 0)
             begin
                    WHILE LEN(@SQLVersion ) > 0
                      BEGIN
                           SET @StartPos = CHARINDEX(@Delimeter, @SQLVersion )
                           IF @StartPos < 0 SET @StartPos = 0
                           SET @Length = LEN(@SQLVersion ) - @StartPos - 1
                           IF @Length < 0 SET @Length = 0
                           IF @StartPos > 0
                             BEGIN
                                 SET @tSQLVersion  = SUBSTRING(@SQLVersion , 1, @StartPos - 1)
                                 SET @SQLVersion  = SUBSTRING(@SQLVersion , @StartPos + 1, LEN(@SQLVersion ) - @StartPos)
                             END
                           ELSE
                             BEGIN
                                 SET @tSQLVersion  = @SQLVersion 
                                 SET @SQLVersion  = ''
                             END
                           INSERT @tblSQLVersion  (tVersion) VALUES(@tSQLVersion )
                    END

                    --Get minor version from  @tblSQLVersion  table
                    SELECT @t_MajorVersion= convert (int,tVersion) FROM @tblSQLVersion where id = 1
                    SELECT @t_MinorVersion=  convert (int,tVersion)  FROM @tblSQLVersion where id = 3
                    if (@t_MajorVersion =11 and @t_MinorVersion < 5556  )
                    begin
						update tbl_AnalysisSummary
						set [Status] = 1
						where Name = 'XEventcrash'
					end       
       end
       
end
go

Create procedure OracleLinkedServerIssue  
as  
begin
	IF ((OBJECT_ID ('tbl_dm_os_loaded_modules') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
	begin 
		declare @i  int 
		set @i = 0 
		
		select @i= count(*) 
		from    [dbo].[tbl_dm_os_loaded_modules] 
		where name like '%OraOLEDButl11%' or name like '%OraOLEDBrst11%' or name like '%OraOLEDBrst10%' 

		if (@i > 0 )
		begin 
			update tbl_AnalysisSummary
			set [Status] = 1
			where Name = 'OracleLinkedServerIssue'
		end 
	end  
end

go

create procedure  [usp_ExcessiveTrace_Warning]
as
begin
             declare @RuleInstanceID uniqueidentifier
             set @RuleInstanceID = newid()    
             declare @t_DisplayMessage nvarchar(1256)
             declare @t_traceid varchar(100)
             declare @t_value  varchar(100)
             declare @is_Rulehit int
                    set @is_Rulehit = 0
                    set @t_DisplayMessage = ''
             Create table #tmp (traceid varchar(100),value  varchar(100))
             declare @i  int
             set @i = 0
             select @i= Count(*)  from sys.objects where name =  'tbl_profiler_trace_summary' 
 
             if @i > 0 
                    begin
                           
                           insert into #tmp (traceid,value ) select traceid,value From   [dbo].[tbl_profiler_trace_summary]
                           where value not like '%SQLDiag%'
                           and property =2
                    End

             select  @is_Rulehit = COUNT(*) from #tmp
             if ( @is_Rulehit > 0)
              begin
						update tbl_AnalysisSummary
						set [Status] = 1
						where Name = 'usp_ExcessiveTrace_Warning'
					end     
             drop table #tmp
       
end
go

create procedure  [usp_Many_Traces_Used]
as
begin
		declare @RuleInstanceID uniqueidentifier
		set @RuleInstanceID = newid()	
		declare @t_DisplayMessage nvarchar(1256)
		declare @t_traceid varchar(100)
		declare @t_value  varchar(100)
		declare @is_Rulehit int
			set @is_Rulehit = 0
			set @t_DisplayMessage = ''
		Create table #tmp (traceid varchar(100),value  varchar(100))
		declare @i  int
             set @i = 0
             select @i= Count(*)  from sys.objects where name =  'tbl_profiler_trace_summary' 
 
             if @i > 0     
			 begin
				 
				insert into #tmp (traceid,value ) select top 1 traceid,value From   [dbo].[tbl_profiler_trace_summary] inner join  dbo.tbl_trace_event_details 
				on [tbl_profiler_trace_summary].traceid = dbo.tbl_trace_event_details.trace_id
				where value not like '%SQLDiag%'
				and trace_event_id in(23,24,44)
				and property =2
			 End

		select  @is_Rulehit = COUNT(*) from #tmp
		if ( @is_Rulehit > 0)
			 begin
						update tbl_AnalysisSummary
						set [Status] = 1
						where Name = 'usp_Many_Traces_Used'
				end 
		drop table #tmp
	 
end
go

/***************************************************************************************************

owner: EricBu

****************************************************************************************************/
go
CREATE PROCEDURE usp_HighRecompiles 
as
begin
	-- Created: 5/27/2015
	-- Fires when ...
	-- MAX SQL Re-Compilations/sec > 100
	-- AVG SQL Re-Compilations/sec > 50
	if (OBJECT_ID ('CounterDetails') is not null) and (OBJECT_ID ('CounterData') is not null) 
	begin
		IF (select max(cast(dat.countervalue as float)) from CounterDetails c join CounterData dat on c.CounterID = dat.CounterID where c.CounterName = 'SQL Re-Compilations/sec') > 100
		AND ( select avg(cast(dat.countervalue as float)) from CounterDetails c join CounterData dat on c.CounterID = dat.CounterID where c.CounterName = 'SQL Re-Compilations/sec') > 50
		BEGIN
			update tbl_analysissummary
			set [status]=1
			where name ='usp_HighRecompiles'
		END 
	end
end
GO

/**************************************************************************************************
owner:  VIRANA

***************************************************************************************************/


CREATE PROCEDURE usp_oldce
AS
BEGIN
	DECLARE @SQLVERSION INT
	DECLARE @oldCE INT
	DECLARE @database_id INT

	IF EXISTS (
			SELECT 1
			FROM sys.Tables
			WHERE NAME = N'tbl_SCRIPT_ENVIRONMENT_DETAILS'
			)
	BEGIN
		SELECT @SQLVERSION = LEFT(VALUE, 2)
		FROM tbl_SCRIPT_ENVIRONMENT_DETAILS
		WHERE NAME LIKE 'SQL Version%'
	END

	IF EXISTS (
			SELECT 1
			FROM sys.Tables
			WHERE NAME = N'tbl_SysDatabases'
			)
	BEGIN
		SELECT TOP 1 @database_id = database_id
		FROM [dbo].[tbl_SysDatabases]
		WHERE compatibility_level < 120
	END

	IF (
			@SQLVERSION >= 12
			AND @database_id > 0
			)
	BEGIN
		UPDATE tbl_AnalysisSummary
		SET [Status] = 1
		WHERE NAME = 'usp_oldce'
	END
END


GO


/********************************************************
Owner: Louis Li
********************************************************/
if (OBJECT_ID ('CounterDetails') is not null) and (OBJECT_ID ('CounterData') is not null) 
begin
	select 
		de.ObjectName,de.CounterName,de.InstanceName
		,cast(cast(CounterDateTime as varchar(19)) as time) as CounterDateTime
		,d.CounterValue
		,'\'+objectname + case when InstanceName is NULL then '' else '(' + InstanceName + ')' end + '\' + CounterName as FullCounterName
		,'\'+objectname + case when InstanceName is NULL then '' else '(*)' end + '\' + CounterName as FullCounterNameWithWildchar
	Into dbo.Counters
	from 
		dbo.counterdata d inner join dbo.CounterDetails de
			on d.CounterID = de.CounterID
	Order By
		de.ObjectName, de.CounterName,de.instancename,d.CounterDateTime
end
GO

/********************************************************
firing rules
********************************************************/


/*********************************************************

owner:jackli
**********************************************************/

exec usp_AttendtionCausedBlocking
go
exec usp_PerfScriptsRunningLong
go
exec usp_LongAutoUpdateStats
go
exec usp_HighStmtCount
go
exec usp_RG_Idle
go
exec usp_HighCompile
go
exec usp_HighCacheCount
go
exec proc_PowerPlan
go
exec proc_CheckTraceFlags
go
exec proc_ExcessiveXevents
go
exec proc_AutoStats
go
exec proc_nondefaultConfigDetected
go
exec usp_ExcessiveLockXevent
go
exec usp_McAFee_Intrusion
go
exec usp_BatchSort
go
exec usp_SmallSampledStats
go

exec usp_DisabledIndex
go
exec usp_AccessCheck
go
exec usp_RedoThreadBlocked
go
exec usp_VirtualBytesLeak
go
exec usp_ChangeTableCauseHighCPU
go
exec usp_DeadlockTraceFlag
go
/************************************************************
owner: jaynar
***********************************************************/
exec usp_Many_Traces_Used
go
exec usp_ExcessiveTrace_Warning
go
exec OracleLinkedServerIssue
go

exec XEventcrash
go

exec usp_Non_SQL_CPU_consumption 
go 


exec [usp_KernelHighCPUconsumption] 
go 



exec [usp_SQLHighCPUconsumption]
go

exec StaleStatswarning2008
go

exec dbo.usp_IOAnalysis
go

exec usp_WarnmissingIndex
go
exec Optimizer_Memory_Leak
go
exec HugeGrant
go


/*********************************************************

owner: EricBu
**********************************************************/
go
exec usp_HighRecompiles
go

/**************************************************************************************************
owner:  VIRANA

***************************************************************************************************/
go
exec usp_oldce
go


/******END of script***/