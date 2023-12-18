PRINT 'Current database: ' + DB_NAME();
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- TODO: store schema ver for possible upgrade tasks
IF '%runmode%' != 'REALTIME'
   AND OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
BEGIN
    DECLARE @firstruntime DATETIME,
            @lastruntime DATETIME;
    SELECT @firstruntime = MIN(runtime),
           @lastruntime = MAX(runtime)
    FROM dbo.tbl_RUNTIMES (NOLOCK);
    PRINT '==============================================';
    PRINT '        Perf Stats Script Analysis            ';
    PRINT '==============================================';
    PRINT ' Script Exec Duration: ' + CONVERT(VARCHAR, DATEDIFF(mi, @firstruntime, @lastruntime)) + ' minutes';
    PRINT '    Script Start Time: ' + CONVERT(VARCHAR, @firstruntime, 120);
    PRINT '      Script End Time: ' + CONVERT(VARCHAR, @lastruntime, 120);
    IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
        SELECT RIGHT(REPLICATE(' ', 24) + LEFT([Name], 20), 21) + ':',
               LEFT([Value], 60)
        FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
        WHERE script_name = 'SQL 2005 Perf Stats Script';
    RAISERROR('', 0, 1) WITH NOWAIT;
END;
GO

/* ========== BEGIN TABLE CREATES (realtime only) ========== */
-- DROP TABLE tbl_RUNTIMES
IF '%runmode%' = 'REALTIME'
   AND OBJECT_ID('tbl_RUNTIMES') IS NULL
    CREATE TABLE [dbo].[tbl_RUNTIMES]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [source_script] [VARCHAR](80) NULL
    );
GO

IF '%runmode%' = 'REALTIME'
   AND OBJECT_ID('tbl_BLOCKING_CHAINS') IS NULL
    CREATE TABLE [dbo].[tbl_BLOCKING_CHAINS]
    (
        [first_rownum] [BIGINT] NULL,
        [last_rownum] [BIGINT] NULL,
        [num_snapshots] [INT] NULL,
        [blocking_start] [DATETIME] NULL,
        [blocking_end] [DATETIME] NULL,
        [head_blocker_session_id] INT NULL,
        [blocking_wait_type] VARCHAR(40) NULL,
        [max_blocked_task_count] INT NULL,
        [max_total_wait_duration_ms] [BIGINT] NULL,
        [avg_wait_duration_ms] [BIGINT] NULL,
        [max_wait_duration_ms] [BIGINT] NULL,
        [max_blocking_chain_depth] [INT] NULL,
        [head_blocker_session_id_orig] [INT] NULL
    );
GO

-- DROP VIEW vw_HEAD_BLOCKER_SUMMARY
-- DROP TABLE tbl_HEADBLOCKERSUMMARY
--we need to create the object for report to work

IF OBJECT_ID('tbl_HEADBLOCKERSUMMARY') IS NULL
    CREATE TABLE [dbo].[tbl_HEADBLOCKERSUMMARY]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [head_blocker_session_id] [INT] NULL,
        [blocked_task_count] [INT] NULL,
        [tot_wait_duration_ms] [BIGINT] NULL,
        [blocking_resource_wait_type] VARCHAR(40) NULL,
        [avg_wait_duration_ms] [BIGINT] NULL,
        [max_wait_duration_ms] [BIGINT] NULL,
        [max_blocking_chain_depth] [INT] NULL,
        [head_blocker_proc_name] NVARCHAR(60),
        [head_blocker_proc_objid] [INT] NULL,
        [stmt_text] NVARCHAR(1000) NULL,
        [head_blocker_plan_handle] VARBINARY(64) NULL
    );

GO
-- DROP TABLE tbl_NOTABLEACTIVEQUERIES
IF OBJECT_ID('tbl_NOTABLEACTIVEQUERIES') IS NULL
    CREATE TABLE [dbo].[tbl_NOTABLEACTIVEQUERIES]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [session_id] [INT] NULL,
        [request_id] [INT] NULL,
        ecid INT NULL,
        [plan_total_exec_count] [BIGINT] NULL,
        [plan_total_cpu_ms] [BIGINT] NULL,
        [plan_total_duration_ms] [BIGINT] NULL,
        [plan_total_physical_reads] [BIGINT] NULL,
        [plan_total_logical_writes] [BIGINT] NULL,
        [plan_total_logical_reads] [BIGINT] NULL,
        [dbname] [VARCHAR](40) NULL,
        [objectid] [INT] NULL,
        [procname] [VARCHAR](60) NULL,
        [plan_handle] VARBINARY(64) NULL,
        group_id INT NULL,
        statement_start_offset INT NULL,
        statement_end_offset INT NULL,
        [stmt_text] [VARCHAR](2000) NULL,
        plan_generation_num INT NULL,
        creatioin_time DATETIME NULL
    --[stmt_text_agg]  AS (replace(replace(case when [stmt_text] IS NULL then NULL when charindex('noexec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],(1),(40)) when charindex('sp_executesql',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(200))),(60)) when charindex('sp_cursoropen',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_cursorprep',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_prepare',substring([stmt_text],(1),(200)))>(0) OR charindex('sp_prepexec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(200))),(80)) when charindex('exec',substring([stmt_text],(1),(200)))>(0) then substring([stmt_text],charindex('exec',substring([stmt_text],(1),(4000))),charindex(' ',substring(substring([stmt_text],(1),(200))+'   ',charindex('exec',substring([stmt_text],(1),(500))),(200)),(9))) when substring([stmt_text],(1),(2))='usp' OR substring([stmt_text],(1),(2))='xp' OR substring([stmt_text],(1),(2))='sp' then substring([stmt_text],(1),charindex(' ',substring([stmt_text],(1),(200))+' ')) when substring([stmt_text],(1),(30)) like '%UPDATE %' OR substring([stmt_text],(1),(30)) like '%INSERT %' OR substring([stmt_text],(1),(30)) like '%DELETE %' then substring([stmt_text],(1),(30)) else substring([stmt_text],(1),(45)) end,char((10)),' '),char((13)),' '))
    );
GO
-- DROP TABLE tbl_REQUESTS
IF OBJECT_ID('tbl_REQUESTS') IS NULL
    CREATE TABLE [dbo].[tbl_REQUESTS]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [session_id] [INT] NULL,
        [request_id] [INT] NULL,
        [ecid] [INT] NULL,
        [blocking_session_id] [INT] NULL,
        [blocking_ecid] [INT] NULL,
        [task_state] [VARCHAR](15) NULL,
        [wait_type] [VARCHAR](50) NULL,
        [wait_duration_ms] [BIGINT] NULL,
        [wait_resource] [VARCHAR](40) NULL,
        [resource_description] [VARCHAR](120) NULL,
        [last_wait_type] [VARCHAR](50) NULL,
        [open_trans] [INT] NULL,
        [transaction_isolation_level] [VARCHAR](30) NULL,
        [is_user_process] [INT] NULL,
        [request_cpu_time] [BIGINT] NULL,
        [request_logical_reads] [BIGINT] NULL,
        [request_reads] [BIGINT] NULL,
        [request_writes] [BIGINT] NULL,
        [memory_usage] [INT] NULL,
        [session_cpu_time] [BIGINT] NULL,
        [session_reads] [BIGINT] NULL,
        [session_writes] [BIGINT] NULL,
        [session_logical_reads] [BIGINT] NULL,
        [total_scheduled_time] [BIGINT] NULL,
        [total_elapsed_time] [VARCHAR](18) NULL,
        [last_request_start_time] [DATETIME] NULL,
        [last_request_end_time] [DATETIME] NULL,
        [session_row_count] [BIGINT] NULL,
        [prev_error] [INT] NULL,
        [open_resultsets] [INT] NULL,
        [request_total_elapsed_time] [BIGINT] NULL,
        [percent_complete] [INT] NULL,
        [estimated_completion_time] [BIGINT] NULL,
        [tran_name] [VARCHAR](32) NULL,
        [transaction_begin_time] [DATETIME] NULL,
        [tran_type] [VARCHAR](15) NULL,
        [tran_state] [VARCHAR](15) NULL,
        [request_start_time] [DATETIME] NULL,
        [request_status] [VARCHAR](15) NULL,
        [command] [VARCHAR](16) NULL,
        [statement_start_offset] [INT] NULL,
        [statement_end_offset] [INT] NULL,
        [database_id] [INT] NULL,
        [user_id] [INT] NULL,
        [executing_managed_code] [INT] NULL,
        [pending_io_count] [INT] NULL,
        [login_time] [DATETIME] NULL,
        [host_name] [VARCHAR](20) NULL,
        [program_name] [VARCHAR](50) NULL,
        [host_process_id] [INT] NULL,
        [client_version] [VARCHAR](14) NULL,
        [client_interface_name] [VARCHAR](32) NULL,
        [login_name] [VARCHAR](20) NULL,
        [nt_domain] [VARCHAR](30) NULL,
        [nt_user_name] [VARCHAR](20) NULL,
        [net_packet_size] [INT] NULL,
        [client_net_address] [VARCHAR](40) NULL,
        [session_status] [VARCHAR](15) NULL,
        [scheduler_id] [INT] NULL,
        [is_preemptive] [INT] NULL,
        [is_sick] [INT] NULL,
        [last_worker_exception] [INT] NULL,
        [last_exception_address] [VARBINARY](22) NULL,
        [os_thread_id] [INT] NULL
    );
GO

IF OBJECT_ID('tbl_OS_WAIT_STATS') IS NULL
    CREATE TABLE [dbo].[tbl_OS_WAIT_STATS]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [wait_type] [VARCHAR](45) NULL,
        [waiting_tasks_count] [BIGINT] NULL,
        [wait_time_ms] [BIGINT] NULL,
        [max_wait_time_ms] [BIGINT] NULL,
        [signal_wait_time_ms] [BIGINT] NULL
    );
GO
IF OBJECT_ID('tbl_SYSPERFINFO') IS NULL
    CREATE TABLE [dbo].[tbl_SYSPERFINFO]
    (
        [rownum] [BIGINT] IDENTITY NOT NULL,
        [runtime] [DATETIME] NULL,
        [object_name] NVARCHAR(256),
        counter_name NVARCHAR(256),
        instance_name NVARCHAR(256),
        cntr_value BIGINT
    );
GO
IF OBJECT_ID('tbl_SQL_CPU_HEALTH') IS NULL
    CREATE TABLE tbl_SQL_CPU_HEALTH
    (
        rownum BIGINT IDENTITY NOT NULL,
        runtime DATETIME,
        record_id INT,
        EventTime DATETIME,
        system_idle_cpu INT,
        sql_cpu_utilization INT
    );
GO
IF OBJECT_ID('tbl_FILE_STATS') IS NULL
    CREATE TABLE [dbo].[tbl_FILE_STATS]
    (
        [database] [sysname],
        [file] [NVARCHAR](260),
        [DbId] [SMALLINT],
        [FileId] [SMALLINT],
        [AvgIOTimeMS] [BIGINT] NULL,
        [TimeStamp] [INT],
        [NumberReads] [BIGINT],
        [BytesRead] [BIGINT],
        [IoStallReadMS] [BIGINT],
        [NumberWrites] [BIGINT],
        [BytesWritten] [BIGINT],
        [IoStallWriteMS] [BIGINT],
        [IoStallMS] [BIGINT],
        [BytesOnDisk] [BIGINT],
        [type] [TINYINT],
        [type_desc] [NVARCHAR](60),
        [data_space_id] [INT],
        [state] [TINYINT],
        [state_desc] [NVARCHAR](60),
        [size] [INT],
        [max_size] [INT],
        [growth] [INT],
        [is_sparse] [BIT],
        [is_percent_growth] [BIT]
    );
GO

-- "Register" perf stats tables for the background purge job
IF '%runmode%' = 'REALTIME'
BEGIN
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_RUNTIMES'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_RUNTIMES', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_BLOCKING_CHAINS'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_BLOCKING_CHAINS', 'blocking_end');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_HEADBLOCKERSUMMARY'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_HEADBLOCKERSUMMARY', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_NOTABLEACTIVEQUERIES'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_NOTABLEACTIVEQUERIES', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_REQUESTS'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_REQUESTS', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_OS_WAIT_STATS'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_OS_WAIT_STATS', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_SYSPERFINFO'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_SYSPERFINFO', 'runtime');
    IF NOT EXISTS
    (
        SELECT *
        FROM tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'tbl_SQL_CPU_HEALTH'
    )
        INSERT INTO tbl_NEXUS_PURGE_TABLES
        VALUES
        ('tbl_SQL_CPU_HEALTH', 'runtime');
END;
GO

-- Compensate for missing sys.dm_os_sys_info in some very old perf stats script output. 
IF OBJECT_ID('tbl_SYSINFO') IS NULL
BEGIN
    CREATE TABLE [dbo].[tbl_SYSINFO]
    (
        tableinfo VARCHAR(128),
        cpu_ticks BIGINT,
        ms_ticks BIGINT,
        cpu_count INT,
        cpu_ticks_in_ms BIGINT,
        hyperthread_ratio INT,
        physical_memory_in_bytes BIGINT,
        virtual_memory_in_bytes BIGINT,
        bpool_committed INT,
        bpool_commit_target INT,
        bpool_visible INT,
        stack_size_in_bytes INT,
        os_quantum BIGINT,
        os_error_code INT,
        os_priority_class INT,
        max_workers_count INT,
        schedulers_count SMALLINT,
        scheduler_total_count INT,
        deadlock_monitor_serial_number INT
    );
    EXEC ('INSERT INTO dbo.tbl_SYSINFO (tableinfo, cpu_count) 
    VALUES (''This table was created by PerfStatsAnalysis.sql due to missing Perf Stats Script data.'', 2)');
END;
GO

-- Compensate for missing tbl_SQL_CPU_HEALTH bug in some perf stats script output
IF OBJECT_ID('tbl_SQL_CPU_HEALTH') IS NULL
BEGIN
    CREATE TABLE [dbo].[tbl_SQL_CPU_HEALTH]
    (
        [rownum] INT,
        [runtime] [DATETIME] NOT NULL,
        [record_id] [INT] NULL,
        [EventTime] [VARCHAR](30) NULL,
        [timestamp] [BIGINT] NOT NULL,
        [system_idle_cpu] [INT] NULL,
        [sql_cpu_utilization] [INT] NULL
    );
END;
GO

/* ========== END TABLE CREATES ========== */



/* ========== BEGIN ANALYSIS HELPER OBJECTS ========== */
IF OBJECT_ID('vw_BLOCKING_HIERARCHY') IS NOT NULL
    DROP VIEW vw_BLOCKING_HIERARCHY;
GO
CREATE VIEW vw_BLOCKING_HIERARCHY
AS
WITH BlockingHierarchy (runtime, head_blocker_session_id, session_id, blocking_session_id, wait_type, wait_duration_ms,
                        wait_resource, resource_description, [Level]
                       )
AS (
   SELECT head.runtime,
          head.session_id AS head_blocker_session_id,
          head.session_id AS session_id,
          head.blocking_session_id,
          head.wait_type,
          head.wait_duration_ms,
          head.wait_resource,
          head.resource_description,
          0 AS [Level]
   FROM dbo.tbl_REQUESTS (NOLOCK) AS head
   WHERE (
             head.blocking_session_id IS NULL
             OR head.blocking_session_id = 0
         )
         AND head.session_id IN
             (
                 SELECT r2.blocking_session_id
                 FROM dbo.tbl_REQUESTS (NOLOCK) AS r2
                 WHERE r2.blocking_session_id <> 0
                       AND r2.runtime = head.runtime
             )
         AND
         (
             head.ecid = 0
             OR head.ecid IS NULL
         )
   UNION ALL
   SELECT h.runtime,
          h.head_blocker_session_id,
          blocked.session_id,
          blocked.blocking_session_id,
          blocked.wait_type,
          CASE
              WHEN blocked.wait_type LIKE 'EXCHANGE%'
                   OR blocked.wait_type LIKE 'CXPACKET%' THEN
                  0
              ELSE
                  blocked.wait_duration_ms
          END AS wait_duration_ms,
          blocked.wait_resource,
          blocked.resource_description,
          h.Level + 1
   FROM dbo.tbl_REQUESTS (NOLOCK) AS blocked
       INNER JOIN BlockingHierarchy AS h
           ON h.runtime = blocked.runtime
              AND h.session_id = blocked.blocking_session_id)
SELECT *
FROM BlockingHierarchy;
GO

IF OBJECT_ID('vw_FIRSTTIERBLOCKINGHIERARCHY') IS NOT NULL
    DROP VIEW dbo.vw_FIRSTTIERBLOCKINGHIERARCHY;
GO
CREATE VIEW dbo.vw_FIRSTTIERBLOCKINGHIERARCHY
AS
WITH BlockingHierarchy (runtime, first_tier_blocked_session_id, session_id, blocking_session_id, wait_type,
                        wait_duration_ms, first_tier_wait_resource, wait_resource, resource_description, [Level]
                       )
AS (
   SELECT firsttier.runtime,
          firsttier.session_id AS first_tier_blocked_session_id,
          firsttier.session_id AS session_id,
          firsttier.blocking_session_id,
          firsttier.wait_type,
          firsttier.wait_duration_ms,
          firsttier.wait_resource AS first_tier_wait_resource,
          firsttier.wait_resource,
          firsttier.resource_description,
          1 AS [Level]
   FROM dbo.tbl_REQUESTS (NOLOCK) AS firsttier
   WHERE firsttier.blocking_session_id IN
         (
             SELECT r2.session_id
             FROM dbo.tbl_REQUESTS (NOLOCK) AS r2
             WHERE r2.blocking_session_id = 0
                   AND r2.runtime = firsttier.runtime
         )
   UNION ALL
   SELECT h.runtime,
          h.first_tier_blocked_session_id,
          blocked.session_id,
          blocked.blocking_session_id,
          blocked.wait_type,
          CASE
              WHEN blocked.wait_type IN ( 'EXCHANGE', 'CXPACKET' ) THEN
                  0
              ELSE
                  blocked.wait_duration_ms
          END AS wait_duration_ms,
          h.first_tier_wait_resource,
          blocked.wait_resource,
          blocked.resource_description,
          h.Level + 1
   FROM dbo.tbl_REQUESTS (NOLOCK) AS blocked
       INNER JOIN BlockingHierarchy AS h
           ON h.runtime = blocked.runtime
              AND h.session_id = blocked.blocking_session_id)
SELECT *
FROM BlockingHierarchy;
GO

-- Handle old tbl_RUNTIMES format
IF NOT EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('tbl_RUNTIMES')
          AND name = 'source_script'
)
    ALTER TABLE dbo.tbl_RUNTIMES ADD source_script VARCHAR(80) NULL;
GO

IF OBJECT_ID('vw_HEAD_BLOCKER_SUMMARY') IS NOT NULL
    DROP VIEW dbo.vw_HEAD_BLOCKER_SUMMARY;
GO
-- old 
IF EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'wait_type'
)
    EXEC ('
  CREATE VIEW dbo.vw_HEAD_BLOCKER_SUMMARY WITH SCHEMABINDING AS 
  SELECT rownum, runtime, CASE WHEN wait_type LIKE ''COMPILE%'' THEN ''COMPILE BLOCKING'' ELSE CONVERT (varchar(24), head_blocker_session_id) END AS head_blocker_session_id, 
    blocked_task_count, tot_wait_duration_ms, wait_type AS blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
    head_blocker_session_id AS head_blocker_session_id_orig
  FROM dbo.tbl_HEADBLOCKERSUMMARY (NOLOCK)
'        );
GO
-- new
IF EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'blocking_resource_wait_type'
)
    EXEC ('
  CREATE VIEW dbo.vw_HEAD_BLOCKER_SUMMARY WITH SCHEMABINDING AS 
  SELECT rownum, runtime, CASE WHEN blocking_resource_wait_type LIKE ''COMPILE%'' THEN ''COMPILE BLOCKING'' ELSE CONVERT (varchar(24), head_blocker_session_id) END AS head_blocker_session_id, 
    blocked_task_count, tot_wait_duration_ms, blocking_resource_wait_type AS blocking_wait_type, avg_wait_duration_ms, max_wait_duration_ms, max_blocking_chain_depth, 
    head_blocker_session_id AS head_blocker_session_id_orig
  FROM dbo.tbl_HEADBLOCKERSUMMARY (NOLOCK)
'        );
GO
CREATE UNIQUE CLUSTERED INDEX cidx
ON dbo.vw_HEAD_BLOCKER_SUMMARY (
                                   runtime,
                                   head_blocker_session_id,
                                   blocking_wait_type,
                                   rownum
                               );
GO


IF OBJECT_ID('tbl_PERF_STATS_SCRIPT_RUNTIMES') IS NOT NULL
    DROP TABLE dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES;
GO
--it used to use tbl_requests. but that table wont' get much data if server is idle. wait stats is likely to be generated
CREATE TABLE dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
(
    runtime DATETIME
);
GO
IF OBJECT_ID('tbl_OS_WAIT_STATS') IS NOT NULL
    INSERT INTO dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
    SELECT DISTINCT
           runtime
    FROM dbo.tbl_OS_WAIT_STATS
    ORDER BY runtime;



IF OBJECT_ID('vw_PERF_STATS_SCRIPT_RUNTIMES') IS NOT NULL
    DROP VIEW dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
GO
CREATE VIEW dbo.vw_PERF_STATS_SCRIPT_RUNTIMES
AS
SELECT runtime
FROM dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES;
--SELECT * FROM tbl_RUNTIMES (NOLOCK) 
--WHERE source_script IN ('SQL 2005 Perf Stats Script', '') OR source_script IS NULL
GO

IF OBJECT_ID('ufn_REQUEST_DETAILS') IS NOT NULL
    DROP FUNCTION ufn_REQUEST_DETAILS;
GO
CREATE FUNCTION ufn_REQUEST_DETAILS
(
    @start_time DATETIME,
    @end_time DATETIME,
    @session_id INT
)
RETURNS TABLE
AS
RETURN SELECT TOP 1
              runtime,
              session_id,
              ecid,
              wait_type,
              wait_duration_ms,
              request_status,
              wait_resource,
              open_trans,
              transaction_isolation_level,
              tran_name,
              transaction_begin_time,
              request_start_time,
              command,
              resource_description,
              program_name,
              [host_name],
              nt_user_name,
              nt_domain,
              login_name,
              last_request_start_time,
              last_request_end_time
       FROM dbo.tbl_REQUESTS
       WHERE runtime >= @start_time
             AND runtime < @end_time
             AND session_id = @session_id
       ORDER BY CASE
                    WHEN wait_type IN ( 'EXCHANGE', 'CXPACKET' ) THEN
                        1
                    ELSE
                        0
                END,                   -- prefer non-parallel waittypes
                wait_duration_ms DESC, -- prefer longer waits
                runtime;
GO

IF OBJECT_ID('ufn_QUERY_DETAILS') IS NOT NULL
    DROP FUNCTION ufn_QUERY_DETAILS;
GO
CREATE FUNCTION ufn_QUERY_DETAILS
(
    @start_time DATETIME,
    @end_time DATETIME,
    @session_id INT
)
RETURNS TABLE
AS
RETURN SELECT TOP 1
              runtime,
              procname,
              stmt_text,
              session_id
       FROM dbo.tbl_NOTABLEACTIVEQUERIES
       WHERE runtime >= @start_time
             AND runtime < @end_time
             AND session_id = @session_id
             AND
             (
                 stmt_text IS NOT NULL
                 OR procname IS NOT NULL
             )
       ORDER BY runtime;
GO

IF '%runmode%' != 'REALTIME'
BEGIN
    -- Create tbl_BLOCKING_CHAINS (postmortem analysis mode only -- the realtime data capture proc populates this on-the-fly)
    IF OBJECT_ID('tempdb.dbo.#head_blk_sum') IS NOT NULL
        DROP TABLE #head_blk_sum;

    SELECT rownum,
           runtime AS blocking_start,
           (
               SELECT TOP 1
                      runtime
               FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES run
               WHERE run.runtime > b.runtime
                     AND NOT EXISTS
               (
                   SELECT runtime
                   FROM dbo.vw_HEAD_BLOCKER_SUMMARY AS b2
                   WHERE b2.runtime = run.runtime
                         AND b2.head_blocker_session_id = b.head_blocker_session_id
                         AND b2.blocking_wait_type = b.blocking_wait_type
               )
               ORDER BY run.runtime ASC
           ) AS blocking_end,
           head_blocker_session_id,
           blocking_wait_type,
           avg_wait_duration_ms,
           max_wait_duration_ms,
           max_blocking_chain_depth,
           blocked_task_count,
           tot_wait_duration_ms,
           head_blocker_session_id_orig
    INTO #head_blk_sum
    FROM dbo.vw_HEAD_BLOCKER_SUMMARY b
    WHERE runtime IS NOT NULL;

    -- Set blocking end time to end-of-data-collection for any blocking chains that were still active when data collection stopped
    UPDATE #head_blk_sum
    SET blocking_end =
        (
            SELECT MAX(runtime)FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES
        )
    WHERE blocking_end IS NULL;

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

    IF OBJECT_ID('tbl_BLOCKING_CHAINS') IS NOT NULL
        DROP TABLE dbo.tbl_BLOCKING_CHAINS;
    WITH BlockingChainsIntermediate (rownum, blocking_start, blocking_end, head_blocker_session_id, blocking_wait_type,
                                     blocked_task_count, tot_wait_duration_ms, avg_wait_duration_ms,
                                     max_wait_duration_ms, max_blocking_chain_depth, head_blocker_session_id_orig
                                    )
    AS (SELECT rownum,
               (
                   SELECT MIN(blocking_start)
                   FROM #head_blk_sum b3
                   WHERE b3.head_blocker_session_id = b2.head_blocker_session_id
                         AND b3.blocking_end = b2.blocking_end
                         AND b3.blocking_wait_type = b2.blocking_wait_type
               ) AS blocking_start,
               b2.blocking_end,
               b2.head_blocker_session_id,
               b2.blocking_wait_type,
               b2.blocked_task_count,
               b2.tot_wait_duration_ms,
               b2.avg_wait_duration_ms,
               b2.max_wait_duration_ms,
               b2.max_blocking_chain_depth,
               b2.head_blocker_session_id_orig
        FROM #head_blk_sum b2)
    SELECT MIN(rownum) AS first_rownum,
           MAX(rownum) AS last_rownum,
           COUNT(*) AS num_snapshots,
           blocking_start,
           blocking_end,
           head_blocker_session_id,
           blocking_wait_type,
           MAX(blocked_task_count) AS max_blocked_task_count,
           MAX(tot_wait_duration_ms) AS max_total_wait_duration_ms,
           AVG(avg_wait_duration_ms) AS avg_wait_duration_ms,
           MAX(max_wait_duration_ms) AS max_wait_duration_ms,
           MAX(max_blocking_chain_depth) AS max_blocking_chain_depth,
           MIN(head_blocker_session_id_orig) AS head_blocker_session_id_orig
    INTO dbo.tbl_BLOCKING_CHAINS
    FROM BlockingChainsIntermediate
    GROUP BY blocking_end,
             blocking_start,
             head_blocker_session_id,
             blocking_wait_type;
END;
GO

IF OBJECT_ID('vw_BLOCKING_CHAINS') IS NOT NULL
    DROP VIEW dbo.vw_BLOCKING_CHAINS;
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
CREATE VIEW dbo.vw_BLOCKING_CHAINS
AS
SELECT ch.*,
       CASE
           WHEN DATEDIFF(s, blocking_start, blocking_end) >= 20 THEN
               DATEDIFF(s, blocking_start, blocking_end)
           ELSE
               max_wait_duration_ms / 1000
       END AS blocking_duration_sec,
       req.runtime AS example_runtime,
       req.program_name,
       req.[host_name],
       req.nt_user_name,
       req.nt_domain,
       req.login_name,
       req.wait_type,
       req.wait_duration_ms,
       req.request_status,
       req.wait_resource,
       req.open_trans,
       req.transaction_isolation_level,
       req.tran_name,
       req.transaction_begin_time,
       req.request_start_time,
       req.command,
       req.resource_description,
       last_request_start_time,
       last_request_end_time,
       qry.procname,
       qry.stmt_text
FROM dbo.tbl_BLOCKING_CHAINS ch
    OUTER APPLY dbo.ufn_REQUEST_DETAILS(ch.blocking_start, ch.blocking_end, ch.head_blocker_session_id_orig) req
    OUTER APPLY dbo.ufn_QUERY_DETAILS(ch.blocking_start, ch.blocking_end, ch.head_blocker_session_id_orig) qry;
GO

IF OBJECT_ID('vw_BLOCKING_PERIODS') IS NOT NULL
    DROP VIEW dbo.vw_BLOCKING_PERIODS;
GO
CREATE VIEW dbo.vw_BLOCKING_PERIODS
AS
WITH BlockingPeriodsRaw (blocking_start, blocking_end, max_wait_duration_ms)
AS (
   SELECT DISTINCT
          runtime AS blocking_start,
          (
              SELECT TOP 1
                     runtime
              FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES run
              WHERE run.runtime > b.runtime
                    AND NOT EXISTS
              (
                  SELECT runtime
                  FROM dbo.vw_HEAD_BLOCKER_SUMMARY AS b2
                  WHERE b2.runtime = run.runtime
              )
              ORDER BY run.runtime ASC
          ) AS blocking_end,
          max_wait_duration_ms
   FROM dbo.vw_HEAD_BLOCKER_SUMMARY b)
SELECT p1.*,
       CASE
           WHEN DATEDIFF(s, blocking_start, blocking_end) >= 20 THEN
               DATEDIFF(s, blocking_start, blocking_end)
           ELSE
               max_wait_duration_ms / 1000
       END AS blocking_duration_sec,
       (
           SELECT MAX(blocked_task_count)
           FROM dbo.vw_HEAD_BLOCKER_SUMMARY b3
           WHERE b3.runtime >= p1.blocking_start
                 AND b3.runtime < p1.blocking_end
       ) AS max_blocked_task_count,
       (
           SELECT MAX(tot_wait_duration_ms)
           FROM dbo.vw_HEAD_BLOCKER_SUMMARY b3
           WHERE b3.runtime >= p1.blocking_start
                 AND b3.runtime < p1.blocking_end
       ) AS max_total_wait_duration_ms
FROM BlockingPeriodsRaw p1
WHERE NOT EXISTS
(
    SELECT blocking_start
    FROM BlockingPeriodsRaw AS p2
    WHERE p2.blocking_start < p1.blocking_start
          AND p2.blocking_end = p1.blocking_end
);
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

IF NOT EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('tbl_OS_WAIT_STATS')
          AND name = 'wait_category'
)
    ALTER TABLE dbo.tbl_OS_WAIT_STATS
    ADD wait_category AS
            CASE
                WHEN wait_type LIKE 'LCK%' THEN
                    'Locks'
                WHEN wait_type LIKE 'PAGEIO%' THEN
                    'Page I/O Latch'
                WHEN wait_type LIKE 'PAGELATCH%' THEN
                    'Page Latch (non-I/O)'
                WHEN wait_type LIKE 'LATCH%' THEN
                    'Latch (non-buffer)'
                WHEN wait_type LIKE 'IO_COMPLETION' THEN
                    'I/O Completion'
                WHEN wait_type LIKE 'ASYNC_NETWORK_IO' THEN
                    'Network I/O (client fetch)'
                --WHEN wait_type LIKE 'CLR_%' OR wait_type LIKE 'SQLCLR%' THEN 'SQLCLR'
                WHEN wait_type IN ( 'RESOURCE_SEMAPHORE', 'SOS_RESERVEDMEMBLOCKLIST', 'CMEMTHREAD' ) THEN
                    'Memory'
                WHEN wait_type LIKE 'RESOURCE_SEMAPHORE_%' THEN
                    'Compilation'
                WHEN wait_type LIKE 'MSQL_XP' THEN
                    'XProc'
                WHEN wait_type LIKE 'WRITELOG' THEN
                    'Writelog'
                WHEN wait_type IN (   'DBMIRROR_WORKER_QUEUE', 'DBMIRRORING_CMD', 'DBMIRROR_DBM_EVENT',
                                      'DBMIRROR_EVENTS_QUEUE', 'BROKER_EVENTHANDLER', 'BROKER_RECEIVE_WAITFOR',
                                      'BROKER_TRANSMITTER', 'BROKER_TASK_STOP', 'BROKER_TO_FLUSH', 'CHECKPOINT_QUEUE',
                                      'CHKPT', 'CLR_AUTO_EVENT', 'CLR_MANUAL_EVENT', 'FSAGENT', 'KSOURCE_WAKEUP',
                                      'LAZYWRITER_SLEEP', 'LOGMGR_QUEUE', 'ONDEMAND_TASK_QUEUE',
                                      'REQUEST_FOR_DEADLOCK_SEARCH', 'RESOURCE_QUEUE', 'SERVER_IDLE_CHECK',
                                      'SLEEP_BPOOL_FLUSH', 'SLEEP_DBSTARTUP', 'SLEEP_DCOMSTARTUP', 'SLEEP_MSDBSTARTUP',
                                      'SLEEP_SYSTEMTASK', 'SLEEP_TASK', 'SLEEP_TEMPDBSTARTUP', 'SNI_HTTP_ACCEPT',
                                      'SQLTRACE_BUFFER_FLUSH',
                                      --'TRACEWRITE',
                                      'WAIT_FOR_RESULTS', 'WAITFOR_TASKSHUTDOWN', 'XE_DISPATCHER_WAIT',
                                      'XE_TIMER_EVENT',
                                      --'CXPACKET',
                                      --'EXCHANGE',
                                      'SQLTRACE_INCREMENTAL_FLUSH_SLEEP', 'EXECSYNC', 'WAITFOR',
                                      'FT_IFTS_SCHEDULER_IDLE_WAIT', 'DISPATCHER_QUEUE_SEMAPHORE',
                                      'HADR_FILESTREAM_IOMGR_IOCOMPLETION', 'DIRTY_PAGE_POLL',
                                      'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
                                      'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', 'PREEMPTIVE_XE_DISPATCHER',
                                      'SP_SERVER_DIAGNOSTICS_SLEEP', 'SOS_WORK_DISPATCHER', 'HADR_DB_COMMAND',
                                      'HADR_TRANSPORT_SESSION', 'HADR_CLUSAPI_CALL', 'HADR_WORK_POOL',
                                      'HADR_WORK_QUEUE', 'HADR_LOGCAPTURE_SYNC', 'HADR_AG_MUTEX',
                                      'HADR_FILESTREAM_MANAGER', 'HADR_FILESTREAM_BLOCK_FLUSH',
                                      'HADR_FILESTREAM_IOMGR', 'HADR_FILESTREAM_IOMGR_IOCOMPLETION',
                                      'HADR_FILESTREAM_PREPROC', 'HADR_DB_OP_START_SYNC', 'HADR_DB_OP_COMPLETION_SYNC',
                                      'HADR_LOGPROGRESS_SYNC', 'HADR_TRANSPORT_DBRLIST', 'HADR_CONNECTIVITY_INFO',
                                      'HADR_AR_UNLOAD_COMPLETED', 'HADR_PARTNER_SYNC', 'HADR_DBSTATECHANGE_SYNC',
                                      'HADR_FILESTREAM_FILE_REQUEST', 'HADR_REPLICAINFO_SYNC',
                                      'HADR_COMPRESSED_CACHE_SYNC', 'HADR_AR_MANAGER_MUTEX',
                                      'HADR_NOTIFICATION_WORKER_TERMINATION_SYNC', 'HADR_NOTIFICATION_DEQUEUE',
                                      'HADR_ARCONTROLLER_NOTIFICATIONS_SUBSCRIBER_LIST',
                                      'HADR_DBR_SUBSCRIBER_FILTER_LIST', 'HADR_DBR_SUBSCRIBER',
                                      'HADR_NOTIFICATION_WORKER_STARTUP_SYNC',
                                      'HADR_NOTIFICATION_WORKER_EXCLUSIVE_ACCESS', 'HADR_RECOVERY_WAIT_FOR_UNDO',
                                      'HADR_DATABASE_WAIT_FOR_RESTART', 'HADR_DATABASE_WAIT_FOR_RECOVERY',
                                      'HADR_XRF_STACK_ACCESS', 'HADR_RECOVERY_WAIT_FOR_CONNECTION',
                                      'HADR_TRANSPORT_FLOW_CONTROL', 'HADR_DATABASE_FLOW_CONTROL',
                                      'HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING', 'HADR_BACKUP_BULK_LOCK',
                                      'HADR_BACKUP_QUEUE', 'HADR_LOGCAPTURE_WAIT', 'HADR_AR_CRITICAL_SECTION_ENTRY',
                                      'HADR_TDS_LISTENER_SYNC', 'HADR_READ_ALL_NETWORKS',
                                      'HADR_TDS_LISTENER_SYNC_PROCESSING', 'HADR_TIMER_TASK', 'HADR_GROUP_COMMIT',
                                      'HADR_SYNCHRONIZING_THROTTLE', 'HADR_DATABASE_VERSIONING_STATE',
                                      'HADR_FILESTREAM_FILE_CLOSE', 'HADR_FABRIC_CALLBACK', 'HADR_DBSEEDING',
                                      'HADR_DBSEEDING_LIST', 'HADR_THROTTLE_LOG_RATE_GOVERNOR',
                                      'HADR_SEEDING_LIMIT_BACKUPS', 'HADR_SEEDING_CANCELLATION',
                                      'HADR_SEEDING_SYNC_COMPLETION', 'HADR_SEEDING_TIMEOUT_TASK',
                                      'HADR_SEEDING_FILE_LIST', 'HADR_SEEDING_WAIT_FOR_COMPLETION',
                                      'HADR_DBHEALTH_INFOMAP_ACCESS', 'HADR_SEEDING_READY_FOR_RESTORE_STREAM',
                                      'REDO_THREAD_PENDING_WORK', 'CHECKPOINT_QUEUE', 'QDS_ASYNC_QUEUE',
                                      'PARALLEL_REDO_WORKER_WAIT_WORK', 'PWAIT_EXTENSIBILITY_CLEANUP_TASK'
                                  ) THEN
                    'IGNORABLE'
                ELSE
                    wait_type
            END;
GO

IF NOT EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'wait_category'
)
    ALTER TABLE dbo.tbl_REQUESTS
    ADD wait_category AS
            CASE
                WHEN wait_type LIKE 'LCK%' THEN
                    'Locks'
                WHEN wait_type LIKE 'PAGEIO%' THEN
                    'Page I/O Latch'
                WHEN wait_type LIKE 'PAGELATCH%' THEN
                    'Page Latch (non-I/O)'
                WHEN wait_type LIKE 'LATCH%' THEN
                    'Latch (non-buffer)'
                WHEN wait_type LIKE 'IO_COMPLETION' THEN
                    'I/O Completion'
                WHEN wait_type LIKE 'ASYNC_NETWORK_IO' THEN
                    'Network I/O (client fetch)'
                --WHEN wait_type LIKE 'CLR_%' or wait_type like 'SQLCLR%' THEN 'SQLCLR'
                WHEN wait_type IN ( 'RESOURCE_SEMAPHORE', 'SOS_RESERVEDMEMBLOCKLIST', 'CMEMTHREAD' ) THEN
                    'Memory'
                WHEN wait_type LIKE 'RESOURCE_SEMAPHORE_%' THEN
                    'Compilation'
                WHEN wait_type LIKE 'MSQL_XP' THEN
                    'XProc'
                WHEN wait_type LIKE 'WRITELOG' THEN
                    'Writelog'
                WHEN wait_type IN (   'DBMIRROR_WORKER_QUEUE', 'DBMIRRORING_CMD', 'DBMIRROR_DBM_EVENT',
                                      'DBMIRROR_EVENTS_QUEUE', 'BROKER_EVENTHANDLER', 'BROKER_RECEIVE_WAITFOR',
                                      'BROKER_TRANSMITTER', 'BROKER_TASK_STOP', 'BROKER_TO_FLUSH', 'CHECKPOINT_QUEUE',
                                      'CHKPT', 'CLR_AUTO_EVENT', 'CLR_MANUAL_EVENT', 'FSAGENT', 'KSOURCE_WAKEUP',
                                      'LAZYWRITER_SLEEP', 'LOGMGR_QUEUE', 'ONDEMAND_TASK_QUEUE',
                                      'REQUEST_FOR_DEADLOCK_SEARCH', 'RESOURCE_QUEUE', 'SERVER_IDLE_CHECK',
                                      'SLEEP_BPOOL_FLUSH', 'SLEEP_DBSTARTUP', 'SLEEP_DCOMSTARTUP', 'SLEEP_MSDBSTARTUP',
                                      'SLEEP_SYSTEMTASK', 'SLEEP_TASK', 'SLEEP_TEMPDBSTARTUP', 'SNI_HTTP_ACCEPT',
                                      'SQLTRACE_BUFFER_FLUSH',
                                      --'TRACEWRITE',
                                      'WAIT_FOR_RESULTS', 'WAITFOR_TASKSHUTDOWN', 'XE_DISPATCHER_WAIT',
                                      'XE_TIMER_EVENT',
                                      --'CXPACKET',
                                      --'EXCHANGE',
                                      'EXECSYNC', 'WAITFOR', 'FT_IFTS_SCHEDULER_IDLE_WAIT',
                                      'SQLTRACE_INCREMENTAL_FLUSH_SLEEP', 'DISPATCHER_QUEUE_SEMAPHORE',
                                      'HADR_FILESTREAM_IOMGR_IOCOMPLETION', 'DIRTY_PAGE_POLL',
                                      'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',
                                      'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', 'PREEMPTIVE_XE_DISPATCHER',
                                      'SP_SERVER_DIAGNOSTICS_SLEEP'
                                  ) THEN
                    'IGNORABLE'
                ELSE
                    wait_type
            END;
GO

IF OBJECT_ID('vw_WAIT_CATEGORY_STATS') IS NOT NULL
    DROP VIEW vw_WAIT_CATEGORY_STATS;
GO
CREATE VIEW vw_WAIT_CATEGORY_STATS
AS
SELECT runtime,
       wait_category,
       SUM(ISNULL(waiting_tasks_count, 0)) AS waiting_tasks_count,
       SUM(ISNULL(wait_time_ms, 0)) AS wait_time_ms,
       SUM(ISNULL(signal_wait_time_ms, 0)) AS signal_wait_time_ms,
       MAX(ISNULL(max_wait_time_ms, 0)) AS max_wait_time_ms
FROM dbo.tbl_OS_WAIT_STATS (NOLOCK)
WHERE wait_category != 'IGNORABLE'
GROUP BY runtime,
         wait_category;
GO

/* ============ Nexus Reporting DataSet Queries ============ */
-- Naming convention: DataSet_ReportName_ReportItem
IF OBJECT_ID('DataSet_WaitStatsMain_WaitStatsChart') IS NOT NULL
    DROP PROC DataSet_WaitStatsMain_WaitStatsChart;
GO
CREATE PROC DataSet_WaitStatsMain_WaitStatsChart
    @StartTime DATETIME = '19000101',
    @EndTime DATETIME = '29990101'
AS
IF @StartTime IS NULL
   OR @StartTime = '19000101'
    SELECT @StartTime = MIN(runtime)
    FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
IF @EndTime IS NULL
   OR @EndTime = '29990101'
    SELECT @EndTime = MAX(runtime)
    FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;

SELECT MIN(w1.runtime) AS interval_start,
       MAX(w2.runtime) AS interval_end,
       w2.wait_category,
       CONVERT(DECIMAL(28, 3), MAX(w2.wait_time_ms) - MIN(w1.wait_time_ms) + 0.001) AS wait_time_ms,
       CONVERT(DECIMAL(28, 3), MAX(w2.wait_time_ms) - MIN(w1.wait_time_ms) + 0.001)
       / CASE
             WHEN DATEDIFF(s, MIN(w1.runtime), MAX(w2.runtime)) = 0 THEN
                 1
             ELSE
                 DATEDIFF(s, MIN(w1.runtime), MAX(w2.runtime))
         END AS wait_time_per_sec,
       CONVERT(DECIMAL(28, 3), MAX(w2.waiting_tasks_count) - MIN(w1.waiting_tasks_count)) + 0.001 AS wait_count
FROM dbo.vw_WAIT_CATEGORY_STATS w2
    LEFT OUTER JOIN dbo.vw_WAIT_CATEGORY_STATS w1
        ON w1.wait_category = w2.wait_category
           AND w1.runtime =
           (
               SELECT TOP 1
                      runtime
               FROM dbo.tbl_OS_WAIT_STATS w3
               WHERE w3.runtime < w2.runtime
               ORDER BY w3.runtime DESC
           )
WHERE w2.wait_category IN ( 'Locks', 'Page I/O Latch', 'Page Latch (non-I/O)', 'Latch (non-buffer)', 'I/O Completion',
                            'Network I/O (client fetch)', 'Writelog', 'Compilation', 'Memory', 'OLEDB'
                          )
      AND w2.runtime >= @StartTime
      AND w2.runtime <= @EndTime
GROUP BY w2.wait_category,
         DATEDIFF(ss, '20000101', w2.runtime) / (DATEDIFF(s, @StartTime, @EndTime) / 50 + 1)
HAVING MIN(w1.runtime) IS NOT NULL
ORDER BY MIN(w2.runtime);
GO

IF OBJECT_ID('DataSet_WaitStats_WaitStatsTop5Categories') IS NOT NULL
    DROP PROC DataSet_WaitStats_WaitStatsTop5Categories;
GO
CREATE PROC DataSet_WaitStats_WaitStatsTop5Categories
    @StartTime DATETIME = '19000101',
    @EndTime DATETIME = '29990101',
    @IncludeIgnorable BIT = 0
AS
SET NOCOUNT ON;
-- DECLARE @StartTime datetime
-- DECLARE @EndTime datetime
IF (@StartTime IS NOT NULL AND @StartTime != '19000101')
    SELECT @StartTime = MAX(runtime)
    FROM dbo.tbl_OS_WAIT_STATS
    WHERE runtime <= @StartTime;
IF (@StartTime IS NULL OR @StartTime = '19000101')
    SELECT @StartTime = MIN(runtime)
    FROM dbo.tbl_OS_WAIT_STATS;

IF (@EndTime IS NOT NULL AND @EndTime != '29990101')
    SELECT @EndTime = MIN(runtime)
    FROM dbo.tbl_OS_WAIT_STATS
    WHERE runtime >= @EndTime;
IF (@EndTime IS NULL OR @EndTime = '29990101')
    SELECT @EndTime = MAX(runtime)
    FROM dbo.tbl_OS_WAIT_STATS;

-- Get basic wait stats for the specified interval
SELECT w_end.wait_category,
       CASE
           WHEN (CONVERT(BIGINT, w_end.wait_time_ms) - CASE
                                                           WHEN w_start.wait_time_ms IS NULL THEN
                                                               0
                                                           ELSE
                                                               w_start.wait_time_ms
                                                       END
                ) <= 0 THEN
               0
           ELSE
       (CONVERT(BIGINT, w_end.wait_time_ms) - CASE
                                                  WHEN w_start.wait_time_ms IS NULL THEN
                                                      0
                                                  ELSE
                                                      w_start.wait_time_ms
                                              END
       )
       END AS total_wait_time_ms,
       (CONVERT(BIGINT, w_end.wait_time_ms) - CASE
                                                  WHEN w_start.wait_time_ms IS NULL THEN
                                                      0
                                                  ELSE
                                                      w_start.wait_time_ms
                                              END
       ) / (DATEDIFF(s, @StartTime, @EndTime) + 1) AS wait_time_ms_per_sec
INTO #waitstats_categories
FROM dbo.vw_WAIT_CATEGORY_STATS w_end
    LEFT OUTER JOIN dbo.vw_WAIT_CATEGORY_STATS w_start
        ON w_end.wait_category = w_start.wait_category
           AND w_start.runtime = @StartTime
WHERE w_end.runtime = @EndTime
      AND
      (
          w_end.wait_category != 'IGNORABLE'
          OR @IncludeIgnorable = 1
      )
ORDER BY (w_end.wait_time_ms - CASE
                                   WHEN w_start.wait_time_ms IS NULL THEN
                                       0
                                   ELSE
                                       w_start.wait_time_ms
                               END
         ) DESC;

-- Get number of available "CPU seconds" in the specified interval (seconds in collection interval times # CPUs on the system)
DECLARE @avail_cpu_time_sec INT;
SELECT @avail_cpu_time_sec =
(
    SELECT TOP 1 cpu_count FROM dbo.tbl_SYSINFO
) * DATEDIFF(s, @StartTime, @EndTime);

-- Get average % CPU utilization (this is the % of all CPUs on the box, ignoring affinity mask)
DECLARE @avg_sql_cpu INT;
SELECT @avg_sql_cpu = AVG(sql_cpu_utilization)
FROM
(
    SELECT DISTINCT
           (
               SELECT TOP 1
                      EventTime
               FROM dbo.tbl_SQL_CPU_HEALTH cpu2
               WHERE cpu1.record_id = cpu2.record_id
           ) AS EventTime,
           record_id,
           system_idle_cpu,
           sql_cpu_utilization,
           100 - sql_cpu_utilization - system_idle_cpu AS nonsql_cpu_utilization
    FROM dbo.tbl_SQL_CPU_HEALTH cpu1
    WHERE EventTime
    BETWEEN @StartTime AND @EndTime
) AS sql_cpu;

DECLARE @cpu_time_used_ms BIGINT;
SET @cpu_time_used_ms = ISNULL((0.01 * @avg_sql_cpu) * @avail_cpu_time_sec * 1000, 0); -- CPU time used by SQL = (%CPU used by SQL) * (available CPU time)

-- Get total wait time for the specified interval
DECLARE @all_resources_wait_time_ms BIGINT;
SELECT @all_resources_wait_time_ms = SUM(total_wait_time_ms)
FROM #waitstats_categories;
SET @all_resources_wait_time_ms = @all_resources_wait_time_ms + @cpu_time_used_ms;

--this will prevent division by zero errors (bug 2119)
IF @all_resources_wait_time_ms IS NULL
   OR @all_resources_wait_time_ms = 0
BEGIN

    RETURN;
END;


-- Return stats for base wait categories
SELECT *
FROM
(
    SELECT TOP 5
           cat.wait_category,
           DATEDIFF(s, @StartTime, @EndTime) AS time_interval_sec,
           CONVERT(BIGINT, cat.total_wait_time_ms) AS total_wait_time_ms,
           CONVERT(NUMERIC(6, 2), 100.0 * CONVERT(BIGINT, cat.total_wait_time_ms) / @all_resources_wait_time_ms) AS percent_of_total_waittime,
           cat.wait_time_ms_per_sec
    FROM #waitstats_categories cat
    WHERE (
              cat.wait_time_ms_per_sec > 0
              OR cat.total_wait_time_ms > 0
          )
          AND cat.wait_category != 'SOS_SCHEDULER_YIELD' -- don't include "waiting on CPU" time here; we'll include it in the next query
    ORDER BY wait_time_ms_per_sec DESC
) t
WHERE percent_of_total_waittime > 0
UNION ALL
-- Add SOS_SCHEDULER_YIELD wait time (waiting to run on a CPU) to actual used CPU time to synthesize a "CPU" wait category
SELECT 'CPU' AS wait_category,
       DATEDIFF(s, @StartTime, @EndTime) AS time_interval_sec,
       CONVERT(BIGINT, total_wait_time_ms) + @cpu_time_used_ms AS total_wait_time_ms,
       100.0 * (CONVERT(BIGINT, total_wait_time_ms) + @cpu_time_used_ms) / @all_resources_wait_time_ms AS percent_of_total_waittime,
       wait_time_ms_per_sec + (@cpu_time_used_ms / (DATEDIFF(s, @StartTime, @EndTime) + 1)) AS wait_time_ms_per_sec
FROM #waitstats_categories cat
WHERE cat.wait_category = 'SOS_SCHEDULER_YIELD'
UNION ALL
-- Add in an "other" category
SELECT *
FROM
(
    SELECT 'Other' AS wait_category,
           DATEDIFF(s, @StartTime, @EndTime) AS time_interval_sec,
           SUM(CONVERT(BIGINT, cat.total_wait_time_ms)) AS total_wait_time_ms,
           CONVERT(NUMERIC(6, 2), 100.0 * SUM(CONVERT(BIGINT, cat.total_wait_time_ms)) / @all_resources_wait_time_ms) AS percent_of_total_waittime,
           SUM(cat.wait_time_ms_per_sec) AS wait_time_ms_per_sec
    FROM #waitstats_categories cat
    WHERE (
              cat.wait_time_ms_per_sec > 0
              OR cat.total_wait_time_ms > 0
          )
          -- don't include the categories that we are already identifying in the top 5 
          AND cat.wait_category NOT IN
              (
                  SELECT TOP 5
                         cat.wait_category
                  FROM #waitstats_categories cat
                  ORDER BY wait_time_ms_per_sec DESC
              )
) AS t
WHERE percent_of_total_waittime > 0
ORDER BY wait_time_ms_per_sec DESC;

GO



CREATE PROC DataSet_WaitStats_WaitStatsTopCategoriesOther
    @StartTime DATETIME = '19000101',
    @EndTime DATETIME = '29990101',
    @IncludeIgnorable BIT = 0
AS
SET NOCOUNT ON;
-- DECLARE @StartTime datetime
-- DECLARE @EndTime datetime
IF (@StartTime IS NOT NULL AND @StartTime != '19000101')
    SELECT @StartTime = MAX(runtime)
    FROM dbo.tbl_OS_WAIT_STATS
    WHERE runtime <= @StartTime;
IF (@StartTime IS NULL OR @StartTime = '19000101')
    SELECT @StartTime = MIN(runtime)
    FROM dbo.tbl_OS_WAIT_STATS;

IF (@EndTime IS NOT NULL AND @EndTime != '29990101')
    SELECT @EndTime = MIN(runtime)
    FROM dbo.tbl_OS_WAIT_STATS
    WHERE runtime >= @EndTime;
IF (@EndTime IS NULL OR @EndTime = '29990101')
    SELECT @EndTime = MAX(runtime)
    FROM dbo.tbl_OS_WAIT_STATS;

-- Get basic wait stats for the specified interval
SELECT w_end.wait_category,
       (CONVERT(BIGINT, w_end.wait_time_ms) - CASE
                                                  WHEN w_start.wait_time_ms IS NULL THEN
                                                      0
                                                  ELSE
                                                      w_start.wait_time_ms
                                              END
       ) AS total_wait_time_ms,
       (CONVERT(BIGINT, w_end.wait_time_ms) - CASE
                                                  WHEN w_start.wait_time_ms IS NULL THEN
                                                      0
                                                  ELSE
                                                      w_start.wait_time_ms
                                              END
       ) / (DATEDIFF(s, @StartTime, @EndTime) + 1) AS wait_time_ms_per_sec
INTO #waitstats_categories
FROM dbo.vw_WAIT_CATEGORY_STATS w_end
    LEFT OUTER JOIN dbo.vw_WAIT_CATEGORY_STATS w_start
        ON w_end.wait_category = w_start.wait_category
           AND w_start.runtime = @StartTime
WHERE w_end.runtime = @EndTime
      AND
      (
          w_end.wait_category != 'IGNORABLE'
          OR @IncludeIgnorable = 1
      )
ORDER BY (w_end.wait_time_ms - CASE
                                   WHEN w_start.wait_time_ms IS NULL THEN
                                       0
                                   ELSE
                                       w_start.wait_time_ms
                               END
         ) DESC;

-- Get number of available "CPU seconds" in the specified interval (seconds in collection interval times # CPUs on the system)
DECLARE @avail_cpu_time_sec INT;
SELECT @avail_cpu_time_sec =
(
    SELECT TOP 1 cpu_count FROM tbl_SYSINFO
) * DATEDIFF(s, @StartTime, @EndTime);

-- Get average % CPU utilization (this is the % of all CPUs on the box, ignoring affinity mask)
DECLARE @avg_sql_cpu INT;
SELECT @avg_sql_cpu = AVG(sql_cpu_utilization)
FROM
(
    SELECT DISTINCT
           (
               SELECT TOP 1
                      EventTime
               FROM dbo.tbl_SQL_CPU_HEALTH cpu2
               WHERE cpu1.record_id = cpu2.record_id
           ) AS EventTime,
           record_id,
           system_idle_cpu,
           sql_cpu_utilization,
           100 - sql_cpu_utilization - system_idle_cpu AS nonsql_cpu_utilization
    FROM dbo.tbl_SQL_CPU_HEALTH cpu1
    WHERE EventTime
    BETWEEN @StartTime AND @EndTime
) AS sql_cpu;

DECLARE @cpu_time_used_ms BIGINT;
SET @cpu_time_used_ms = ISNULL((0.01 * @avg_sql_cpu) * @avail_cpu_time_sec * 1000, 0); -- CPU time used by SQL = (%CPU used by SQL) * (available CPU time)

-- Get total wait time for the specified interval
DECLARE @all_resources_wait_time_ms BIGINT;
SELECT @all_resources_wait_time_ms = SUM(total_wait_time_ms)
FROM #waitstats_categories;
SET @all_resources_wait_time_ms = @all_resources_wait_time_ms + @cpu_time_used_ms;

--this will prevent division by zero errors (bug 2119)
IF @all_resources_wait_time_ms IS NULL
   OR @all_resources_wait_time_ms = 0
BEGIN

    RETURN;
END;

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
       DATEDIFF(s, @StartTime, @EndTime) AS time_interval_sec,
       CONVERT(BIGINT, cat.total_wait_time_ms) AS total_wait_time_ms,
       CONVERT(NUMERIC(6, 2), 100.0 * CONVERT(BIGINT, cat.total_wait_time_ms) / @all_resources_wait_time_ms) AS percent_of_total_waittime,
       cat.wait_time_ms_per_sec
FROM #waitstats_categories cat
WHERE (
          cat.wait_time_ms_per_sec > 0
          OR cat.total_wait_time_ms > 0
      )
      -- don't include the categories that we are already identifying in the top 5 
      AND cat.wait_category NOT IN
          (
              SELECT TOP 5
                     cat.wait_category
              FROM #waitstats_categories cat
              ORDER BY wait_time_ms_per_sec DESC
          )
      AND cat.wait_category != 'SOS_SCHEDULER_YIELD'
ORDER BY wait_time_ms_per_sec DESC;





GO
IF OBJECT_ID('DataSet_WaitStats_BlockingChains') IS NOT NULL
    DROP PROC DataSet_WaitStats_BlockingChains;
GO
CREATE PROC DataSet_WaitStats_BlockingChains
    @StartTime DATETIME = '19000101',
    @EndTime DATETIME = '29990101'
AS
IF @StartTime IS NULL
   OR @StartTime = '19000101'
    SELECT @StartTime = MIN(runtime)
    FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
IF @EndTime IS NULL
   OR @EndTime = '29990101'
    SELECT @EndTime = MAX(runtime)
    FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;

SELECT *
FROM dbo.vw_BLOCKING_CHAINS
WHERE blocking_duration_sec > 0
      AND (blocking_start
      BETWEEN @StartTime AND @EndTime
          )
      OR (blocking_end
      BETWEEN @StartTime AND @EndTime
         );
GO

SELECT CONVERT(VARCHAR, GETDATE(), 126);

IF OBJECT_ID('DataSet_BlockingChain_BlockingChainAllRuntimes') IS NOT NULL
    DROP PROC DataSet_BlockingChain_BlockingChainAllRuntimes;
GO
CREATE PROC DataSet_BlockingChain_BlockingChainAllRuntimes @BlockingChainRowNum INT
AS
SELECT CONVERT(VARCHAR, r.runtime, 121) AS runtime,
       CONVERT(VARCHAR, r.runtime, 126) AS runtime_locale_insensitive,
       r.task_state,
       LEFT(r.wait_category, 35) AS wait_category,
       r.wait_duration_ms,
       r.request_total_elapsed_time AS request_elapsed_time,
       blk.blocked_task_count AS blocked_tasks,
       r.command,
       LEFT(ch.stmt_text, 100) + '...' AS query
FROM dbo.vw_BLOCKING_CHAINS ch
    INNER JOIN dbo.tbl_REQUESTS r
        ON r.session_id = ch.head_blocker_session_id_orig
           AND r.runtime
           BETWEEN ch.blocking_start AND ch.blocking_end
    INNER JOIN dbo.tbl_HEADBLOCKERSUMMARY blk
        ON ch.head_blocker_session_id_orig = blk.head_blocker_session_id
           AND blk.runtime = r.runtime
WHERE ch.first_rownum = @BlockingChainRowNum
ORDER BY r.runtime;
GO

IF OBJECT_ID('DataSet_BlockingChain_BlockingChainTextSummary') IS NOT NULL
    DROP PROC DataSet_BlockingChain_BlockingChainTextSummary;
GO
CREATE PROC DataSet_BlockingChain_BlockingChainTextSummary @BlockingChainRowNum INT
AS
DECLARE @txtout VARCHAR(MAX);
DECLARE @runtime CHAR(31),
        @task_state CHAR(16),
        @wait_category CHAR(36),
        @wait_duration_ms CHAR(21);
DECLARE @request_elapsed_time CHAR(21),
        @blocked_tasks CHAR(14),
        @command CHAR(17),
        @query CHAR(24);
SELECT TOP 1
       @txtout
           = 'BLOCKING CHAIN STATISTICS:' + CHAR(13) + CHAR(10) + '  Head Blocker Session ID: '
             + CONVERT(CHAR(40), head_blocker_session_id) + CHAR(13) + CHAR(10) + '  Blocking Duration (sec): '
             + CONVERT(CHAR(40), blocking_duration_sec) + CHAR(13) + CHAR(10) + '           Max Chain Size: '
             + CONVERT(CHAR(40), max_blocked_task_count, 121) + CHAR(13) + CHAR(10) + '           Blocking Start: '
             + CONVERT(CHAR(40), blocking_start, 121) + CHAR(13) + CHAR(10) + '             Blocking End: '
             + CONVERT(CHAR(40), blocking_end, 121) + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + 'HEAD BLOCKER:'
             + CHAR(13) + CHAR(10) + '   Program Name: ' + CONVERT(CHAR(40), program_name) + '         Transaction Name: '
             + CONVERT(CHAR(40), tran_name) + CHAR(13) + CHAR(10) + '      Host Name: ' + CONVERT(CHAR(40), [host_name])
             + 'Transaction Isolation Lvl: ' + CONVERT(CHAR(40), transaction_isolation_level) + CHAR(13) + CHAR(10)
             + '     Login Name: ' + CONVERT(CHAR(40), login_name) + '   Transaction Begin Time: '
             + CONVERT(CHAR(40), transaction_begin_time, 121) + CHAR(13) + CHAR(10)
FROM dbo.vw_BLOCKING_CHAINS
WHERE first_rownum = @BlockingChainRowNum;

SET @txtout = @txtout + CHAR(13) + CHAR(10);
SET @txtout = @txtout + 'HEAD BLOCKER RUNTIME SUMMARY:' + CHAR(13) + CHAR(10);
SET @txtout
    = @txtout
      + '  runtime                        task_state      wait_category                       wait_duration_ms     request_elapsed_time blocked_tasks command          query                   '
      + CHAR(13) + CHAR(10);
SET @txtout
    = @txtout
      + '  ------------------------------ --------------- ----------------------------------- -------------------- -------------------- ------------- ---------------- ----------------------- '
      + CHAR(13) + CHAR(10);
DECLARE c CURSOR FOR
SELECT CONVERT(VARCHAR, r.runtime, 121) AS runtime,
       r.task_state,
       LEFT(r.wait_category, 35) AS wait_category,
       r.wait_duration_ms,
       r.request_total_elapsed_time AS request_elapsed_time,
       blk.blocked_task_count AS blocked_tasks,
       r.command,
       LEFT(ch.stmt_text, 20) + '...' AS query
FROM dbo.vw_BLOCKING_CHAINS ch
    INNER JOIN dbo.tbl_REQUESTS r
        ON r.session_id = ch.head_blocker_session_id_orig
           AND r.runtime
           BETWEEN ch.blocking_start AND ch.blocking_end
    INNER JOIN dbo.tbl_HEADBLOCKERSUMMARY blk
        ON ch.head_blocker_session_id_orig = blk.head_blocker_session_id
           AND blk.runtime = r.runtime
WHERE ch.first_rownum = @BlockingChainRowNum
ORDER BY r.runtime;
OPEN c;
FETCH NEXT FROM c
INTO @runtime,
     @task_state,
     @wait_category,
     @wait_duration_ms,
     @request_elapsed_time,
     @blocked_tasks,
     @command,
     @query;
WHILE (@@FETCH_STATUS <> -1)
BEGIN
    SET @txtout
        = @txtout + '  ' + @runtime + @task_state + @wait_category + @wait_duration_ms + @request_elapsed_time
          + @blocked_tasks + @command + @query + CHAR(13) + CHAR(10);
    FETCH NEXT FROM c
    INTO @runtime,
         @task_state,
         @wait_category,
         @wait_duration_ms,
         @request_elapsed_time,
         @blocked_tasks,
         @command,
         @query;
END;
CLOSE c;
DEALLOCATE c;
SELECT REPLACE(REPLACE(@txtout, '/', '_'), '\', '_') AS summary;
GO

IF OBJECT_ID('DataSet_BlockingChain_HeadBlockerSampleRuntime') IS NOT NULL
    DROP PROC DataSet_BlockingChain_HeadBlockerSampleRuntime;
GO
CREATE PROC DataSet_BlockingChain_HeadBlockerSampleRuntime @BlockingChainRowNum INT
AS
SELECT r.*,
       q.*
FROM dbo.tbl_REQUESTS r
    INNER JOIN dbo.vw_BLOCKING_CHAINS ch
        ON ch.first_rownum = @BlockingChainRowNum
           AND ch.head_blocker_session_id_orig = r.session_id
    LEFT OUTER JOIN dbo.tbl_NOTABLEACTIVEQUERIES q
        ON q.session_id = ch.head_blocker_session_id_orig
           AND q.runtime = r.runtime
WHERE r.runtime =
(   -- attempt to find the "worst" example runtime for this chain
    SELECT TOP 1
           b.runtime
    FROM dbo.vw_HEAD_BLOCKER_SUMMARY b -- attempt to find the "worst" example runtime for this chain
    WHERE ch.head_blocker_session_id = b.head_blocker_session_id
          AND ch.blocking_wait_type = b.blocking_wait_type
          AND b.runtime >= ch.blocking_start
          AND b.runtime < ch.blocking_end
    ORDER BY b.blocked_task_count * 4 + b.tot_wait_duration_ms / 1000 DESC
)
ORDER BY r.session_id;
GO

IF OBJECT_ID('DataSet_BlockingChain_BlockingChainDetails') IS NOT NULL
    DROP PROC DataSet_BlockingChain_BlockingChainDetails;
GO
CREATE PROC DataSet_BlockingChain_BlockingChainDetails @BlockingChainRowNum INT
AS
SELECT TOP 1
       *
FROM dbo.vw_BLOCKING_CHAINS
WHERE first_rownum = @BlockingChainRowNum;
GO

IF OBJECT_ID('DataSet_Shared_SQLServerName') IS NOT NULL
    DROP PROC DataSet_Shared_SQLServerName;
GO
CREATE PROC DataSet_Shared_SQLServerName
    @script_name VARCHAR(80) = NULL,
    @name VARCHAR(60) = NULL
AS
IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
    SELECT [Value]
    FROM tbl_SCRIPT_ENVIRONMENT_DETAILS
    WHERE script_name LIKE 'SQL 200% Perf Stats Script'
          AND [Name] = 'SQL Server Name';
ELSE
    SELECT @@SERVERNAME AS [value];
-- SELECT @@SERVERNAME AS [value]
GO


IF OBJECT_ID('DataSet_Shared_SQLVersion') IS NOT NULL
    DROP PROC DataSet_Shared_SQLVersion;
GO
CREATE PROC DataSet_Shared_SQLVersion
    @script_name VARCHAR(80) = NULL,
    @name VARCHAR(60) = NULL
AS
IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
    SELECT [Value]
    FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
    WHERE script_name LIKE 'SQL 200% Perf Stats Script'
          AND [Name] = 'SQL Version (SP)';
ELSE
    SELECT '' AS [value];
-- SELECT CONVERT (varchar, SERVERPROPERTY ('ProductVersion')) + ' (' + CONVERT (varchar, SERVERPROPERTY ('ProductLevel')) + ')' AS [value]
GO

IF OBJECT_ID('DataSet_WaitStats_ParamStartTime') IS NOT NULL
    DROP PROC DataSet_WaitStats_ParamStartTime;
GO
CREATE PROC DataSet_WaitStats_ParamStartTime
AS
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
SELECT @StartTime = MIN(runtime)
FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
SELECT @EndTime = MAX(runtime)
FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
IF @StartTime IS NULL
    SET @StartTime = GETDATE();
IF @EndTime IS NULL
    SET @EndTime = GETDATE();

SELECT CASE
           WHEN DATEDIFF(mi, @StartTime, @EndTime) > 4 * 60 THEN
               DATEADD(mi, -60, @EndTime)
           ELSE
               @StartTime
       END AS StartTime
UNION ALL
SELECT @StartTime
UNION ALL
SELECT @EndTime;
GO

IF OBJECT_ID('DataSet_WaitStats_ParamEndTime') IS NOT NULL
    DROP PROC DataSet_WaitStats_ParamEndTime;
GO
CREATE PROC DataSet_WaitStats_ParamEndTime
AS
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
SELECT @StartTime = MIN(runtime)
FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
SELECT @EndTime = MAX(runtime)
FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES;
IF @StartTime IS NULL
    SET @StartTime = GETDATE();
IF @EndTime IS NULL
    SET @EndTime = GETDATE();

SELECT @EndTime AS EndTime
UNION ALL
SELECT @StartTime AS EndTime
UNION ALL
SELECT @EndTime AS EndTime;
GO

INSERT INTO dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
SELECT DISTINCT
       runtime
FROM tbl_requests
WHERE runtime NOT IN
      (
          SELECT runtime FROM dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
      );
GO
INSERT INTO dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
SELECT DISTINCT
       runtime
FROM dbo.tbl_OS_WAIT_STATS
WHERE runtime NOT IN
      (
          SELECT runtime FROM dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
      );
GO
INSERT INTO dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
SELECT DISTINCT
       runtime
FROM dbo.tbl_NOTABLEACTIVEQUERIES
WHERE runtime NOT IN
      (
          SELECT runtime FROM dbo.tbl_PERF_STATS_SCRIPT_RUNTIMES
      );
GO
/* ============ End Nexus Reporting DataSet Queries ============ */


IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_NOTABLEACTIVEQUERIES')
          AND name = 'idx1'
)
BEGIN
    RAISERROR('Creating index 1 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx1
    ON dbo.tbl_NOTABLEACTIVEQUERIES (
                                        runtime,
                                        session_id,
                                        procname
                                    )
    INCLUDE (stmt_text);
END;
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'idx1'
)
   AND EXISTS
(
    SELECT *
    FROM syscolumns
    WHERE [id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'wait_type'
)
BEGIN
    RAISERROR('Creating index 2 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx1
    ON dbo.tbl_HEADBLOCKERSUMMARY (
                                      runtime,
                                      head_blocker_session_id,
                                      blocked_task_count
                                  )
    INCLUDE (
                rownum,
                tot_wait_duration_ms,
                avg_wait_duration_ms,
                max_wait_duration_ms,
                max_blocking_chain_depth
            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'idx1'
)
   AND EXISTS
(
    SELECT *
    FROM syscolumns
    WHERE [id] = OBJECT_ID('tbl_HEADBLOCKERSUMMARY')
          AND name = 'blocking_resource_wait_type'
)
BEGIN
    RAISERROR('Creating index 2 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx1
    ON dbo.tbl_HEADBLOCKERSUMMARY (
                                      runtime,
                                      head_blocker_session_id,
                                      blocking_resource_wait_type,
                                      blocked_task_count
                                  )
    INCLUDE (
                rownum,
                tot_wait_duration_ms,
                avg_wait_duration_ms,
                max_wait_duration_ms,
                max_blocking_chain_depth
            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'idx1'
)
BEGIN
    RAISERROR('Creating index 3 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx1
    ON [dbo].[tbl_REQUESTS] (
                                runtime,
                                session_id,
                                program_name,
                                [host_name],
                                nt_user_name,
                                nt_domain,
                                login_name
                            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'idx2'
)
BEGIN
    RAISERROR('Creating index 4 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx2
    ON [dbo].[tbl_REQUESTS] (
                                runtime,
                                blocking_session_id,
                                session_id
                            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'idx3'
)
BEGIN
    RAISERROR('Creating index 5 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx3
    ON [dbo].[tbl_REQUESTS] (
                                blocking_session_id,
                                runtime,
                                wait_type,
                                wait_resource
                            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'idx4'
)
BEGIN
    RAISERROR('Creating index 6 of 7', 0, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX idx4
    ON [dbo].[tbl_REQUESTS] (
                                wait_category,
                                wait_duration_ms DESC,
                                wait_type,
                                runtime,
                                session_id
                            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_OS_WAIT_STATS')
          AND name = 'cidx'
)
BEGIN
    RAISERROR('Creating index 7 of 7', 0, 1) WITH NOWAIT;
    CREATE CLUSTERED INDEX cidx
    ON [dbo].[tbl_OS_WAIT_STATS] (
                                     runtime,
                                     wait_category
                                 );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_RUNTIMES')
          AND name = 'cidx'
)
BEGIN
    RAISERROR('Creating index 8 of 8', 0, 1) WITH NOWAIT;
    CREATE CLUSTERED INDEX cidx
    ON [dbo].[tbl_RUNTIMES] (
                                runtime,
                                source_script
                            );
END;
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('tbl_REQUESTS')
          AND name = 'stats1'
)
BEGIN
    CREATE STATISTICS stats1
    ON [dbo].[tbl_REQUESTS]
    (
        runtime,
        wait_duration_ms
    );
END;
GO
/* ========== END ANALYSIS HELPER OBJECTS ========== */





/* ========== BEGIN ANALYSIS QUERIES ========== */

IF '%runmode%' != 'REALTIME'
BEGIN
    IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
    BEGIN
        PRINT '';
        PRINT '==== Script Environment Details ====';
        SELECT [Name],
               [Value]
        FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
        WHERE script_name = 'SQL 2005 Perf Stats Script';
    END;
END;
GO

IF '%runmode%' != 'REALTIME'
BEGIN
    PRINT '';
    PRINT '==== Waitstats Resource Bottleneck Analysis ====';
    SELECT TOP 10
           wait_category,
           (MAX(wait_time_ms) - MIN(wait_time_ms)) AS wait_time_ms,
           (MAX(wait_time_ms) - MIN(wait_time_ms)) / (1 + DATEDIFF(s, MIN(runtime), MAX(runtime))) AS wait_time_per_sec,
           (MAX(waiting_tasks_count) - MIN(waiting_tasks_count)) AS wait_count,
           (MAX(wait_time_ms) - MIN(wait_time_ms)) / CASE (MAX(waiting_tasks_count) - MIN(waiting_tasks_count))
                                                         WHEN 0 THEN
                                                             1
                                                         ELSE
           ((MAX(waiting_tasks_count) - MIN(waiting_tasks_count)))
                                                     END AS average_wait_time_ms,
           MAX(max_wait_time_ms) AS max_wait_time_ms
    FROM dbo.vw_WAIT_CATEGORY_STATS
    WHERE wait_category != 'IGNORABLE'
    GROUP BY wait_category
    ORDER BY (MAX(wait_time_ms) - MIN(wait_time_ms)) DESC;
END;
GO

IF '%runmode%' != 'REALTIME'
BEGIN
    DECLARE @wait_category_num INT;
    DECLARE @wait_category VARCHAR(90);
    SET @wait_category_num = 1;
    DECLARE c CURSOR FOR
    SELECT wait_category
    FROM dbo.vw_WAIT_CATEGORY_STATS
    WHERE wait_category != 'IGNORABLE'
    GROUP BY wait_category
    ORDER BY (MAX(wait_time_ms) - MIN(wait_time_ms)) DESC;
    OPEN c;
    FETCH NEXT FROM c
    INTO @wait_category;
    WHILE (@@FETCH_STATUS = 0)
    BEGIN
        RAISERROR('==== Top 10 longest individual waits for the "%s" wait category ====', 0, 1, @wait_category) WITH NOWAIT;
        SELECT TOP 10
               *
        FROM dbo.tbl_REQUESTS (NOLOCK) AS r
        WHERE r.wait_category = @wait_category
        ORDER BY r.wait_duration_ms DESC;
        FETCH NEXT FROM c
        INTO @wait_category;
        SET @wait_category_num = @wait_category_num + 1;
        IF @wait_category_num >= 3
            BREAK;
    END;
    CLOSE c;
    DEALLOCATE c;
END;
GO

IF '%runmode%' != 'REALTIME'
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Most Expensive Queries by CPU ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           MAX(ISNULL(plan_total_exec_count, 0)) AS exec_count,
           MAX(ISNULL(plan_total_cpu_ms, 0)) AS total_cpu,
           MAX(ISNULL(plan_total_duration_ms, 0)) AS total_duration,
           MAX(ISNULL(plan_total_physical_reads, 0)) AS total_physical_reads,
           MAX(ISNULL(plan_total_logical_writes, 0)) AS total_writes,
           CAST(ISNULL(stmt_text, '') AS VARCHAR(150)) AS stmt_text
    FROM dbo.tbl_NOTABLEACTIVEQUERIES (NOLOCK)
    WHERE stmt_text IS NOT NULL
    GROUP BY stmt_text
    ORDER BY MAX(ISNULL(plan_total_cpu_ms, 0)) DESC;
    PRINT '     Note: The query costs shown above were sampled from sys.dm_exec_query_stats. This is not
     as reliable as a trace, and may overlook some expensive queries or fail to aggregate the 
     cumulative costs of a query correctly.  Be cautious about drawing firm conclusions about 
     the most expensive query based on this data.';
    PRINT '';
END;
GO

IF '%runmode%' != 'REALTIME'
BEGIN
    IF NOT EXISTS
    (
        SELECT *
        FROM dbo.tbl_REQUESTS (NOLOCK)
        WHERE blocking_session_id != 0
    )
    BEGIN
        PRINT '';
        PRINT '     No blocking was detected.';
        PRINT '';
    END;
END;
GO

IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Worst Blocking Chains ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           CONVERT(VARCHAR, d1.blocking_start, 121) AS blocking_start,
           CONVERT(VARCHAR, d1.blocking_end, 121) AS blocking_end,
           d1.blocking_duration_sec,
           d1.max_blocked_task_count,
           d1.head_blocker_session_id,
           d1.blocking_wait_type,
           d1.max_wait_duration_ms
    FROM dbo.vw_BLOCKING_CHAINS d1
    ORDER BY d1.blocking_duration_sec DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Periods of Sustained Blocking ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           CONVERT(VARCHAR, d1.blocking_start, 121) AS blocking_start,
           CONVERT(VARCHAR, d1.blocking_end, 121) AS blocking_end,
           d1.blocking_duration_sec,
           d1.max_blocked_task_count
    FROM dbo.vw_BLOCKING_PERIODS d1
    ORDER BY d1.blocking_duration_sec DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking Queries ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(*) AS num_blocking_chains,
           MAX(ISNULL(b.max_blocked_task_count, 0)) AS max_blocking_chain_size,
           SUM(ISNULL(b.blocking_duration_sec, 0)) AS blocking_duration_sec,
           MAX(ISNULL(b.max_wait_duration_ms, 0)) AS max_wait_duration_ms,
           MIN(ISNULL(b.blocking_start, 0)) AS first_occurrence_runtime,
           b.procname,
           SUBSTRING(b.stmt_text, 1, 100) --, q.stmt_text
    FROM dbo.vw_BLOCKING_CHAINS b
    --  LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES q ON b.blocking_start = q.runtime AND b.head_blocker_session_id = q.session_id
    GROUP BY b.procname,
             SUBSTRING(b.stmt_text, 1, 100)
    ORDER BY MAX(b.max_blocked_task_count) * 4 + SUM(blocking_duration_sec) DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking Programs ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(*) AS num_blocking_chains,
           MAX(ISNULL(b.max_blocked_task_count, 0)) AS max_blocking_chain_size,
           SUM(ISNULL(b.blocking_duration_sec, 0)) AS blocking_duration_sec,
           MAX(ISNULL(b.max_wait_duration_ms, 0)) AS max_wait_duration_ms,
           MIN(ISNULL(b.blocking_start, 0)) AS first_occurrence_runtime,
           b.program_name
    FROM dbo.vw_BLOCKING_CHAINS b
    GROUP BY b.program_name
    ORDER BY MAX(b.max_blocked_task_count) * 4 + SUM(blocking_duration_sec) DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking Host Workstations ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(*) AS num_blocking_chains,
           MAX(ISNULL(b.max_blocked_task_count, 0)) AS max_blocking_chain_size,
           SUM(ISNULL(b.blocking_duration_sec, 0)) AS blocking_duration_sec,
           MAX(ISNULL(b.max_wait_duration_ms, 0)) AS max_wait_duration_ms,
           MIN(ISNULL(b.blocking_start, 0)) AS first_occurrence_runtime,
           b.host_name
    FROM dbo.vw_BLOCKING_CHAINS b
    GROUP BY b.host_name
    ORDER BY MAX(b.max_blocked_task_count) * 4 + SUM(blocking_duration_sec) DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking NT Users ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(*) AS num_blocking_chains,
           MAX(ISNULL(b.max_blocked_task_count, 0)) AS max_blocking_chain_size,
           SUM(ISNULL(b.blocking_duration_sec, 0)) AS blocking_duration_sec,
           MAX(ISNULL(b.max_wait_duration_ms, 0)) AS max_wait_duration_ms,
           MIN(ISNULL(b.blocking_start, 0)) AS first_occurrence_runtime,
           ISNULL(b.nt_domain, '') + '\' + b.nt_user_name
    FROM dbo.vw_BLOCKING_CHAINS b
    GROUP BY ISNULL(b.nt_domain, '') + '\' + b.nt_user_name
    ORDER BY MAX(b.max_blocked_task_count) * 4 + SUM(blocking_duration_sec) DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking SQL Logins ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(*) AS num_blocking_chains,
           MAX(ISNULL(b.max_blocked_task_count, 0)) AS max_blocking_chain_size,
           SUM(ISNULL(b.blocking_duration_sec, 0)) AS blocking_duration_sec,
           MAX(ISNULL(b.max_wait_duration_ms, 0)) AS max_wait_duration_ms,
           MIN(ISNULL(b.blocking_start, 0)) AS first_occurrence_runtime,
           b.login_name
    -- , (blocked_task_count) + (MAX (tot_wait_duration_ms) / 5000) AS [Weighted Blocking Chain Score]
    FROM dbo.vw_BLOCKING_CHAINS b
    GROUP BY b.login_name
    ORDER BY MAX(b.max_blocked_task_count) * 4 + SUM(blocking_duration_sec) DESC;
END;
GO
IF '%runmode%' != 'REALTIME'
   AND EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS (NOLOCK)
    WHERE blocking_session_id != 0
)
BEGIN
    PRINT '';
    RAISERROR('==== Top 10 Blocking Resources ====', 0, 1) WITH NOWAIT;
    SELECT TOP 10
           COUNT(DISTINCT f.runtime) AS num_blocking_incidents,
           COUNT(*) AS total_blocked_sessions,
           MAX(f.wait_duration_ms) AS max_wait_duration_ms,
           AVG(f.wait_duration_ms) AS avg_wait_duration_ms,
           (
               SELECT TOP 1
                      CONVERT(VARCHAR, runtime, 121)
               FROM dbo.vw_FIRSTTIERBLOCKINGHIERARCHY
               WHERE first_tier_wait_resource = f.first_tier_wait_resource
               GROUP BY runtime,
                        first_tier_wait_resource
               ORDER BY (SUM(wait_duration_ms) / 5000) + COUNT(*) DESC
           ) AS example_runtime,
           f.first_tier_wait_resource
    -- , (SUM (wait_duration_ms)/5000) + COUNT(*) AS [Weighted Blocking Resource Score]
    FROM dbo.vw_FIRSTTIERBLOCKINGHIERARCHY f
    GROUP BY f.first_tier_wait_resource
    ORDER BY (SUM(f.wait_duration_ms) / 5000) + COUNT(*) DESC;
END;
GO

-- Run this to view a particular tbl_requests snapshot (tbl_REQUESTS is like sysprocesses, but with a richer data set) 
--   SELECT * FROM tbl_REQUESTS (NOLOCK) AS r 
--   LEFT OUTER JOIN tbl_NOTABLEACTIVEQUERIES (NOLOCK) AS q ON r.runtime = q.runtime AND r.session_id = q.session_id 
--   WHERE r.runtime = '2006-08-15 13:25:31.550'
GO


IF OBJECT_ID('GetTopNQueryHash') IS NOT NULL
    DROP PROC GetTopNQueryHash;
GO
CREATE PROCEDURE GetTopNQueryHash @OrderByCriteria NVARCHAR(20) = 'CPU'
AS
DECLARE @tableName NVARCHAR(50),
        @OrderName NVARCHAR(50),
        @DisplayValue NVARCHAR(100),
        @sql NVARCHAR(MAX);
IF @OrderByCriteria = 'CPU'
BEGIN
    SET @tableName = N'tbl_TopNCPUByQueryHash';
    SET @OrderName = N'total_worker_time';
    SET @DisplayValue = N'total_worker_time';
END;
ELSE IF (@OrderByCriteria = 'Duration')
BEGIN
    SET @tableName = N'tbl_TopNDurationByQueryHash';
    SET @OrderName = N'total_elapsed_time';
    SET @DisplayValue = N'total_elapsed_time';
END;
ELSE IF (@OrderByCriteria = 'Logical Reads')
BEGIN
    SET @tableName = N'tbl_TopNLogicalReadsByQueryHash';
    SET @OrderName = N'total_logical_reads';
    SET @DisplayValue = N'total_logical_reads';
END;


SET @sql
    = N'select ROW_NUMBER() over (order by ' + @OrderName + N' desc) as ''rownumber'',  *, ' + @DisplayValue
      + N' as DisplayValue from ' + @tableName + N' order by ' + @OrderName + N' desc';
EXEC (@sql);

GO

CREATE TABLE dbo.tbl_Reports
(
    ReportId INT IDENTITY PRIMARY KEY,
    ReportName NVARCHAR(128),
    ReportDisplayName NVARCHAR(256),
    ReportDescription NVARCHAR(MAX),
    VersionApplied INT,
    ValidationFunction NVARCHAR(128),
    Category NVARCHAR(50),
    SeqNo INT,
);
GO
CREATE UNIQUE INDEX IX_tbl_Reports_ReportName
ON dbo.tbl_Reports (ReportName);

GO
-- 1 =2000, 2=2005, 4=2008, 8=2008R2
INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Blocking and Wait Statistics_C', 'Blocking and Wait Statistics', 'Blocking and wait statistics', 2 | 4,
 'Performance', 'dbo.fn_Validate_NexusCore', 100);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Bottleneck Analysis_C', 'Bottleneck Analysis', 'Bottleneck Analysis', 2 | 4, 'Performance',
 'dbo.fn_Validate_NexusCore', 200);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Spinlock Stats_C', 'Spin Lock Stats', 'Spin Lock Stats', 2 | 4, 'Performance', 'dbo.fn_Validate_NexusCore', 900);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Query Hash_C', 'Query Hash', 'This report is for Query hash.  It is only available in 2008', 4, 'Performance',
 'dbo.fn_Validate_NexusCore', 400);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Missing Indexes_C', 'Missing Indexes', 'Missing Indexes for SQL Server 2005/2008/2008R2', 2 | 4 | 8, 'Performance',
 'dbo.fn_Validate_NexusCore', 410);


INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('SQL 2000 Blocking_C', 'SQL Server 2000 blocking', 'SQL Server 2000 blocking', 1, 'Performance',
 'dbo.fn_Validate_NexusCore2000', 1000);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('ReadTrace_Main', 'ReadTrace Reports', 'ReadTrace reports for Profiler traces (2000,2005,2008)', 1 | 2 | 4 | 8,
 'Performance', 'ReadTrace.fn_ShowReports', 50);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Perfmon_C', 'Perfmon Summary', 'Summary of most commonly looked at counters', 1 | 2 | 4 | 8, 'Performance',
 'dbo.fn_Validate_NexusCore', 500);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Virtual File Stats_C', 'Virtual File Stats', 'Display Virtual File Stats related to IO performance', 1 | 2 | 4 | 8,
 'Performance', 'dbo.fn_Validate_NexusCore', 500);


INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Memory Brokers_C', 'Memory Brokers', 'Display memory brokers', 1 | 2 | 4 | 8, 'Performance',
 'dbo.fn_Validate_NexusCore', 600);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('SysIndexes_C', 'Indexes and Stats', 'Display Stats info on indexes and stats', 1 | 2 | 4 | 8, 'Performance',
 'dbo.fn_Validate_NexusCore', 700);



INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Query Execution Memory_C', 'Query Execution Memory', 'Displays Query Execution Memory', 1 | 2 | 4 | 8, 'Performance',
 'dbo.fn_Validate_NexusCore', 600);


INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Configuration_C', 'Environment(diagscan)', 'Server configuration values', 1 | 2 | 4 | 8, 'Common',
 'dbo.fn_Validate_DiagScan', 500);

INSERT INTO tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Errors and Warnings_C', 'Errors from errorlog(diagscan)', 'Error statistics from errorlog', 1 | 2 | 4 | 8, 'Common',
 'dbo.fn_Validate_DiagScan', 510);

INSERT INTO dbo.tbl_Reports
(
    ReportName,
    ReportDisplayName,
    ReportDescription,
    VersionApplied,
    Category,
    ValidationFunction,
    SeqNo
)
VALUES
('Loaded Modules_C', 'Loaded Modules', 'Modules loaded in SQL Server address space', 1 | 2 | 4 | 8, 'Common',
 'dbo.fn_Validate_NexusCore', 100);




GO
CREATE FUNCTION dbo.fn_Validate_Fake
()
RETURNS BIT
AS
BEGIN
    RETURN 1;
END;


GO

CREATE FUNCTION dbo.fn_Validate_NexusCore
()
RETURNS BIT
AS
BEGIN
    DECLARE @ret BIT;
    SET @ret = 1;
    IF (OBJECT_ID('vw_PERF_STATS_SCRIPT_RUNTIMES') IS NULL)
    BEGIN
        SET @ret = 0;
    END;
    ELSE IF ((SELECT COUNT(*)FROM dbo.vw_PERF_STATS_SCRIPT_RUNTIMES) <= 0)
    BEGIN
        SET @ret = 0;
    END;

    RETURN @ret;
END;

GO
CREATE FUNCTION dbo.fn_Validate_NexusCore2000
()
RETURNS BIT
AS
BEGIN
    DECLARE @ret BIT;
    SET @ret = 1;
    IF (OBJECT_ID('tbl_sysprocesses') IS NOT NULL)
    BEGIN
        SET @ret = 1;
    END;
    ELSE
    BEGIN
        SET @ret = 0;
    END;

    RETURN @ret;
END;

GO

CREATE FUNCTION dbo.fn_Validate_DiagScan
()
RETURNS BIT
AS
BEGIN
    DECLARE @ret BIT;
    SET @ret = 1;
    IF (OBJECT_ID('tblDiagScan') IS NOT NULL)
    BEGIN
        SET @ret = 1;
    END;
    ELSE
    BEGIN
        SET @ret = 0;
    END;

    RETURN @ret;
END;


GO


CREATE PROCEDURE proc_GetAllReports
AS
DECLARE @sql NVARCHAR(MAX);
SET @sql = N'';
SELECT @sql
    = @sql + N'select  ' + CAST(ReportId AS NVARCHAR(MAX)) + N', '
      + CASE
            WHEN OBJECT_ID(ValidationFunction) IS NOT NULL THEN
                ValidationFunction
            ELSE
                'dbo.fn_Validate_Fake'
        END + N'() as ValidateResults;'
FROM dbo.tbl_Reports;
DECLARE @t TABLE
(
    ReportID INT,
    ReportAvailable BIT
);
INSERT INTO @t
EXEC (@sql);
SELECT DISTINCT
       Category,
       ReportName,
       ReportDisplayName,
       ReportDescription,
       /*case  when (case [Value] when '8' then 1 when '9' then 2 when '10' then 4 else null end) & VersionApplied <> 0 and */
       --ReportAvailable = 1 then 1 else 0 end as 'ReportAvailable'  
       ReportAvailable,
       SeqNo
FROM dbo.tbl_Reports rep
    INNER JOIN @t t
        ON rep.ReportId = t.ReportID
    CROSS JOIN dbo.tblNexusInfo
WHERE Attribute = 'SQLVersion'
ORDER BY SeqNo;


GO


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
DELETE dbo.tbl_SQL_CPU_HEALTH
FROM dbo.tbl_SQL_CPU_HEALTH h
    LEFT JOIN
    (
        SELECT record_id,
               MIN(rownum) AS rownum
        FROM dbo.tbl_SQL_CPU_HEALTH
        GROUP BY record_id
    ) AS f
        ON h.rownum = f.rownum
WHERE f.rownum IS NULL;

GO
IF OBJECT_ID('tbl_SysIndexes') IS NOT NULL
BEGIN
    --delete some noisy values from sysindexes
    DELETE dbo.tbl_SysIndexes
    WHERE ISNUMERIC(row_mods) = 0
          OR ISNUMERIC(dbid) = 0;
    --get rid of text NULL
    UPDATE tbl_sysIndexes
    SET stats_updated = NULL
    WHERE stats_updated = 'NULL';
END;

GO
IF OBJECT_ID('tbl_dm_os_memory_brokers') IS NOT NULL
   AND NOT EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE object_id = OBJECT_ID('tbl_dm_os_memory_brokers')
          AND name = 'pool_id'
)
    ALTER TABLE dbo.tbl_dm_os_memory_brokers ADD pool_id INT;

GO

CREATE PROCEDURE DateSet_GetSnapshotTime @tablename sysname
AS
DECLARE @sql NVARCHAR(MAX);
SET @sql = N'if OBJECT_ID (''' + @tablename + N''') is not null ';
SET @sql = @sql + N' begin ';
SET @sql
    = @sql
      + N'SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime from (select distinct runtime from '
      + @tablename + N') t ';
SET @sql = @sql + N'end ';
SET @sql = @sql + N'else ';
SET @sql = @sql + N'begin ';
SET @sql = @sql + N'	select cast (1 as int)  ''RowNumber'', ''1990/1/1'' ''RunTime''';
SET @sql = @sql + N'end ';
--print @sql 
EXEC (@sql);


GO

CREATE PROCEDURE DateSet_GetSnapshotTime_Default @tablename sysname
AS
DECLARE @sql NVARCHAR(MAX);
SET @sql = N'	if OBJECT_ID (''' + @tablename + N''') is not null ';
SET @sql = @sql + N'begin ';
SET @sql = @sql + N'with MyRuntime ';
SET @sql = @sql + N'as ( ';
SET @sql
    = @sql
      + N'SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime  from (select distinct runtime from '
      + @tablename + N') t)';
SET @sql = @sql + N'select RowNumber, runtime from MyRuntime ';
SET @sql = @sql + N'where RowNumber = (select MAX(RowNumber) from MyRuntime) ';
SET @sql = @sql + N'end ';
SET @sql = @sql + N'else ';
SET @sql = @sql + N'begin ';
SET @sql = @sql + N'	select cast (1 as int)  ''RowNumber'', cast(''1990/1/1'' as datetime) ''runttime''';
SET @sql = @sql + N'end';
EXEC (@sql);


GO
CREATE PROCEDURE DataSet_GetSnapshot
    @tablename sysname,
    @rownumber INT
AS
DECLARE @sql NVARCHAR(MAX);
SET @sql = N'if OBJECT_ID (''' + @tablename + N''') is not null ';
SET @sql = @sql + N'begin ';
SET @sql = @sql + N'declare @runtime datetime ';
IF @rownumber IS NULL
    SET @sql = @sql + N'	select @runtime = MAX (runtime) from ' + @tablename + N' ';
ELSE
    SET @sql
        = @sql
          + N'	  select @runtime = runtime from (SELECT     ROW_NUMBER() OVER (ORDER BY runtime) AS ''RowNumber'', runtime  from (select distinct runtime from '
          + @tablename + N') t   ) MyRunTime  where RowNumber = ' + CAST(@rownumber AS VARCHAR(100)) + N' ';
SET @sql = @sql + N'select  * from ' + @tablename + N' where runtime = @runtime ';
SET @sql = @sql + N'end ';
EXEC (@sql);
--print @sql

GO

IF OBJECT_ID('tbl_FileStats') IS NOT NULL
BEGIN
    UPDATE t1
    SET t1.[database] = t2.[database],
        t1.[file] = t2.[file]
    FROM dbo.tbl_FileStats t1
        INNER JOIN
        (
            SELECT [database],
                   [file],
                   [DbId],
                   [FileId]
            FROM dbo.tbl_FileStats
            WHERE runtime =
            (
                SELECT MIN(runtime)FROM dbo.tbl_FileStats
            )
        ) t2
            ON t1.DbId = t2.DbId
               AND t1.FileId = t2.FileId;
END;
GO

--ensuring CounterDateTime columns can be used in queries
IF OBJECT_ID('CounterData') IS NOT NULL
BEGIN

    --this deals with an Relog.exe issue that caused data was not being returned by queries on CounterDateTime, see #150

    UPDATE dbo.CounterData
    SET CounterDateTime = SUBSTRING(CounterDateTime, 1, 23) + CHAR(32)
    FROM dbo.CounterData;

    CREATE INDEX [INDX_CounterDateTime]
    ON [dbo].[CounterData] ([CounterDateTime]);

    PRINT 'Added index to Table - dbo.CounterData || Column CounterDateTime';
END;
GO

--clean up spinlock
IF OBJECT_ID('tbl_SPINLOCKSTATS') IS NOT NULL
    DELETE dbo.tbl_SPINLOCKSTATS
    WHERE [name] LIKE '%dbcc%';
GO


--this is used to merge waits from tbl_requests
--if a wait never finishes in the tbl_requests, the waits are never counted
--for example, if long blocking chain is detected but blocking is not finished yet, this is never reflected in bottleneck analysis
--this update will do the following
--for any unfinished waits, we merge wait duration and task count into tbl_OS_WAIT_STATS
--to avoid double counting, we only add time difference between captures
--for example, suppose a lock wait started at t1, it lasted t2 and t3 capture.  
--for t1, we update whatever the duration is. for t2 and t3, we just add the difference between t2 and t1 and t3 and t2
UPDATE wait
SET wait_time_ms = wait_time_ms + ISNULL(t.wait_duration_ms, 0),
    waiting_tasks_count = waiting_tasks_count + ISNULL(t.task_cnt, 0),
    max_wait_time_ms = CASE
                           WHEN max_wait_time_ms < t.max_wait_duration_ms THEN
                               t.max_wait_duration_ms
                           ELSE
                               max_wait_time_ms
                       END
FROM dbo.tbl_OS_WAIT_STATS wait
    INNER JOIN
    (
        SELECT req.runtime,
               wait_type,
               SUM(   CASE
                          WHEN wait_duration_ms >= df.timediff THEN
                              df.timediff
                          ELSE
                              wait_duration_ms
                      END
                  ) 'wait_duration_ms',
               SUM(   CASE
                          WHEN wait_duration_ms >= df.timediff THEN
                              0
                          ELSE
                              1
                      END
                  ) 'task_cnt',
               MAX(wait_duration_ms) 'max_wait_duration_ms'
        FROM dbo.tbl_REQUESTS req
            INNER JOIN
            (
                SELECT t1.runtime,
                       DATEDIFF(ms, t2.runtime, t1.runtime) 'timediff'
                FROM
                (
                    SELECT ROW_NUMBER() OVER (ORDER BY t.runtime ASC) row_num,
                           t.runtime
                    FROM
                    (SELECT DISTINCT runtime FROM dbo.tbl_OS_WAIT_STATS) t
                ) t1
                    INNER JOIN
                    (
                        SELECT ROW_NUMBER() OVER (ORDER BY t.runtime ASC) row_num,
                               t.runtime
                        FROM
                        (SELECT DISTINCT runtime FROM dbo.tbl_OS_WAIT_STATS) t
                    ) t2
                        ON t1.row_num = t2.row_num + 1
            ) df
                ON req.runtime = df.runtime
        GROUP BY req.runtime,
                 req.wait_type
    ) t
        ON wait.runtime = t.runtime
           AND wait.wait_type = t.wait_type;


GO


--work around a bulkcopy problem
IF OBJECT_ID('tbl_SysIndexes') IS NOT NULL
   AND EXISTS
(
    SELECT *
    FROM sys.columns
    WHERE name = 'row_mod2'
)
BEGIN
    EXEC sp_RENAME 'tbl_SysIndexes.row_mod2', 'row_mods', 'COLUMN';
END;

GO

--fixing up Azure and boxed product query difference
IF OBJECT_ID('tbl_SysIndexes') IS NOT NULL
BEGIN
    UPDATE dbo.tbl_Sysindexes
    SET [norecompute] = (CASE
                             WHEN [norecompute] = '0' THEN
                                 'no'
                             WHEN [norecompute] = '1' THEN
                                 'yes'
                             ELSE
                                 [norecompute]
                         END
                        );
END;

GO

IF OBJECT_ID('tbl_trace_event_details') IS NULL
BEGIN
    CREATE TABLE [dbo].[tbl_trace_event_details]
    (
        [trace_id] [BIGINT] NULL,
        [status] [BIGINT] NULL,
        [path] [VARCHAR](260) NULL,
        [max_size] [BIGINT] NULL,
        [start_time] [VARCHAR](23) NULL,
        [stop_time] [VARCHAR](23) NULL,
        [max_files] [BIGINT] NULL,
        [is_rowset] [BIGINT] NULL,
        [is_rollover] [BIGINT] NULL,
        [is_shutdown] [BIGINT] NULL,
        [is_default] [BIGINT] NULL,
        [buffer_count] [BIGINT] NULL,
        [buffer_size] [BIGINT] NULL,
        [last_event_time] [VARCHAR](23) NULL,
        [event_count] [BIGINT] NULL,
        [trace_event_id] [BIGINT] NULL,
        [trace_event_name] [VARCHAR](128) NULL,
        [trace_column_id] [BIGINT] NULL,
        [trace_column_name] [VARCHAR](128) NULL,
        [expensive_event] [BIGINT] NULL
    ) ON [PRIMARY];
END;
GO

-- default configurations
CREATE TABLE dbo.tblDefaultConfigures
(
    [Configuration Option] NVARCHAR(128)
        UNIQUE,
    DefaultOption INT,
    Alert INT NULL
);
GO
INSERT INTO dbo.tblDefaultConfigures
VALUES
('access check cache bucket count', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('access check cache quota', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('ad hoc distributed queries', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('affinity I/O mask', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('affinity64 I/O mask', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('affinity mask', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('affinity64 mask', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('Agent XPs ', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('allow updates', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('backup compression default', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('blocked process threshold', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('c2 audit mode', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('clr enabled', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('common criteria compliance enabled', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('contained database authentication', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('cost threshold for parallelism', 5, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('cross db ownership chaining', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('cursor threshold', -1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('Database Mail XPs', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('default full-text language', 1033, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('default language', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('default trace enabled', 1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('disallow results from triggers', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('EKM provider enabled', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('filestream_access_level', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('fill factor (A, RR,NULL)', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('ft crawl bandwidth (max), see ft crawl bandwidth(A)', 100, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('ft crawl bandwidth (min), see ft crawl bandwidth', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('ft notify bandwidth (max), see ft notify bandwidth', 100, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('ft notify bandwidth (min), see ft notify bandwidth', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('index create memory', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('in-doubt xact resolution', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('lightweight pooling', 0, 1);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('locks', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('max degree of parallelism', 0, 0);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('max full-text crawl range', 4, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('max server memory', 2147483647, 2147483647);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('max text repl size', 65536, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('max worker threads', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('media retention', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('min memory per query', 1024, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('min server memory', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('nested triggers', 1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('network packet size', 4096, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('Ole Automation Procedures', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('open objects', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('optimize for ad hoc workloads', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('PH_timeout', 60, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('precompute rank', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('priority boost', 0, 1);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('query governor cost limit', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('query wait', -1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('recovery interval', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('remote access', 1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('remote admin connections', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('remote login timeout', 10, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('remote proc trans', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('remote query timeout', 600, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('Replication XPs Option', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('scan for startup procs', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('server trigger recursion', 1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('set working set size', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('show advanced options', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('SMO and DMO XPs', 1, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('transform noise words', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('two digit year cutoff', 2049, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('user connections', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('user options', 0, NULL);
INSERT INTO dbo.tblDefaultConfigures
VALUES
('xp_cmdshell', 0, NULL);
GO


/***********************************************************
 Top query plan analysis
 *************************************************************/
--Add an XML column and an index to allow for subsequent query to run super fast
--the XML native column improves performance manyfold

IF OBJECT_ID('tblTopSqlPlan') IS NOT NULL
BEGIN
    ALTER TABLE dbo.tblTopSqlPlan ADD xml_plan XML;
END;
GO
IF OBJECT_ID('tblTopSqlPlan') IS NOT NULL
BEGIN
    UPDATE dbo.tblTopSqlPlan
    SET xml_plan = FileContent;
    CREATE PRIMARY XML INDEX PXML_tblTopSqlPlan
    ON dbo.tblTopSqlPlan (xml_plan);
END;


IF OBJECT_ID('tblTopSqlPlan') IS NOT NULL
BEGIN
    SET QUOTED_IDENTIFIER ON;
    WITH XMLNAMESPACES
    (
        'http://schemas.microsoft.com/sqlserver/2004/07/showplan' AS sp
    )
    SELECT DISTINCT
           stmt.stmt_details.value('@Database', 'varchar(max)') 'Database',
           stmt.stmt_details.value('@Schema', 'varchar(max)') 'Schema',
           stmt.stmt_details.value('@Table', 'varchar(max)') 'table'
    INTO dbo.tblObjectsUsedByTopPlans
    FROM
    (SELECT xml_plan AS sqlplan FROM dbo.tblTopSqlPlan) AS p
        CROSS APPLY sqlplan.nodes('//sp:Object') AS stmt(stmt_details);
END;
GO




--QDS
IF OBJECT_ID('tbl_QDS_Query_Stats') IS NOT NULL
BEGIN
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Query_Number INT;
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Logical_Reads_Order INT;
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Logical_Writes_Order INT;
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Physical_Reads_Order INT;
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Duration_Order INT;
    ALTER TABLE dbo.tbl_QDS_Query_Stats ADD Memory_Order INT;
END;

GO

IF OBJECT_ID('tbl_QDS_Query_Stats') IS NOT NULL
BEGIN

    UPDATE QS
    SET QS.Query_Number = t2.QueryNumber,
        QS.Logical_Reads_Order = t2.Logical_Reads_Order,
        QS.Logical_Writes_Order = t2.Logical_Writes_Order,
        QS.Physical_Reads_Order = t2.Physical_reads_order,
        QS.Memory_Order = t2.Memory_order,
        QS.Duration_Order = t2.Duration_order
    FROM dbo.tbl_QDS_Query_Stats QS
        JOIN
        (
            SELECT *,
                   ROW_NUMBER() OVER (ORDER BY total_cpu DESC) AS QueryNumber,
                   ROW_NUMBER() OVER (ORDER BY total_logical_reads DESC) Logical_Reads_Order,
                   ROW_NUMBER() OVER (ORDER BY total_logical_writes DESC) Logical_Writes_Order,
                   ROW_NUMBER() OVER (ORDER BY total_physical_reads DESC) Physical_reads_order,
                   ROW_NUMBER() OVER (ORDER BY total_Query_Memory DESC) Memory_order,
                   ROW_NUMBER() OVER (ORDER BY total_duration DESC) Duration_order
            FROM
            (
                SELECT database_id,
                       query_hash,
                       query_sql_text,
                       SUM(count_executions * avg_cpu_time) total_cpu,
                       SUM(count_executions * avg_duration) total_duration,
                       SUM(count_executions * avg_cpu_time) total_logical_reads,
                       SUM(count_executions * avg_logical_io_writes) total_logical_writes,
                       SUM(count_executions * avg_physical_io_reads) total_physical_reads,
                       SUM(count_executions * avg_query_max_used_memory) total_Query_Memory
                FROM dbo.tbl_QDS_Query_Stats
                GROUP BY database_id,
                         query_hash,
                         query_sql_text

            --order by 3 desc 
            ) t
        ) t2
            ON QS.query_hash = t2.query_hash;

END;


GO
--used to provide Analysis Summary for pssdiag
CREATE TABLE dbo.tbl_AnalysisSummary
(
    [SolutionSourceId] UNIQUEIDENTIFIER PRIMARY KEY,
    [Category] NVARCHAR(100),
    [Type] NVARCHAR(10) CHECK ([Type] IN ( 'E', 'W', 'I' )),
    [TypeDesc] NVARCHAR(10) CHECK ([TypeDesc] IN ( 'Error', 'Warning', 'Info' )),
    [Name] NVARCHAR(256)
        UNIQUE,
    [FriendlyName] NVARCHAR(256),
    [Description] NVARCHAR(MAX),
    [InternalUrl] NVARCHAR(256),
    [ExternalUrl] NVARCHAR(256),
    [Author] NVARCHAR(30),
    [Priority] INT,
    [SeqNum] INT,
    [Status] TINYINT,
    [Report] NVARCHAR(32)
);
GO

--select newid()

INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('1BDE61F7-CE1D-4C99-ABFB-31344A3E317D', 'Server Performance', 'W', 'Warning', 'AutoCreateStats',
 'Auto Create Statistics is disabled',
 'Some databases have auto create statistics disabled. This can negative impact performance.  See Database Configuration report for details',
 '' , 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_create_statistics_async', '  ',
 1  , 99, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('E5335E8F-91F8-4B7D-842C-8C004159C749', 'Server Performance', 'W', 'Warning', 'AutoUpdateStats',
 'Auto Update Statistics is disabled',
 'Some databases have auto create statistics disabled. This can negative impact performance.  See Database Configuration report for details',
 '' , 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_update_statistics_async', '  ',
 1  , 99, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('9E5F54A8-9B8B-46A5-B372-238E873F6277', 'Server Performance', 'W', 'Warning', 'PowerPlan',
 'Power Plan not properly set',
 'Power Plan is not set to High Performance which can impact overall server performance.', '',
 'https://docs.microsoft.com/troubleshoot/windows-server/performance/slow-performance-when-using-power-plan', '  ', 1,
 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('CCCDE188-8E68-4B87-9649-761AF3F48FC8', 'Server Performance', 'W', 'Warning', 'Trace Flag 4199',
 'Trace flag 4199 not enabled',
 'This trace flag is required to activiate all query optimizer fixes. Without this trace flag, none of the query optimizer fixes will be activated even you are on the latest hotfix or cumulative update. ',
 '' , 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf4199',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('D89F7B5E-25BA-460E-9628-F7B0F5E31FFE', 'Server Performance', 'W', 'Warning', 'Detailed XEvent Tracing',
 'Some intensive XEvent tracing captured',
 'Some high-impact Extended Events (XEvents) are active on the server.  For high volume systems, this can have negative performance impact. Turn off query_pre_execution_showplan, query_post_execution_showplan,query_post_compilation_showplan,lock_acquired,sql_statement_starting,sql_statement_completed,sp_statement_starting, and sp_statement_completed to reduce impact. See pssdiag file *Profiler Traces_Startup.OUT for details',
 '' , '', '  ', 1, 200, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('3EAE7B17-7BE4-486D-98AC-309E74CE6771', 'Server Performance', 'W', 'Warning', 'Trace Flag 1118',
 'Trace Flag 1118 not enabled', 'This trace flag help reduce tempdb contention. ', '',
 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf1118', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('17C9E271-756E-46E8-A3D3-B9B15E5FA305', 'Server Performance', 'W', 'Warning', 'Trace Flag 8048',
 'Trace flag 8048 not enabled',
 'This trace flag partitions certain memory allocators by CPU and can improve performance for hihgly active servers.',
 '' , 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf8048',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('AC4F983A-B8F9-4542-8971-B6052175F2B3', 'Server Performance', 'W', 'Warning', 'Trace Flag 9024',
 'Trace flag 9024 not enabled', 'This trace flag can help reduce recovery time and log writes.', '',
 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf9024', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('76F946D8-7AAD-400E-9CEF-1F071AA68868', 'Server Performance', 'W', 'Warning', 'Trace Flag 1236',
 'Trace flag 1236 not enabled (sql 2014 SP1 and above, TF is not required)',
 'This trace flag can help reduce contention on database lock for highly active servers.', '',
 'https://docs.microsoft.com/sql/t-sql/database-console-commands/dbcc-traceon-trace-flags-transact-sql#tf1236', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('59B1DB60-ABC2-4981-A9EC-DF901C3A89B4', 'Server Performance', 'W', 'Warning', 'usp_RG_Idle',
 'high RESOURCE_GOVERNOR_IDLE detected',
 'High waits on RESOURCE_GOVERNOR_IDLE were deteted.  This is means CPU cap was configured for Resource Goverrnor and could force query to slowdown.  Make sure CPU cap for Resource Governor is properly configured. ',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('773805A8-5DB4-4132-8488-E8FBDE57C67A', 'Server Performance', 'W', 'Warning', 'usp_HighCompile',
 'Potential high compiles detected',
 'Potential high compilation was detected. Please verify with Perfmon counters data.  This can cause high CPU issues',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('F9BBF034-ACFE-4C98-AD32-6010573AFD3D', 'Server Performance', 'W', 'Warning', 'usp_HighCacheCount',
 'High Cache Entries detected.',
 'High number of SQL Server cache entries (Cache Object Counts) were detected.  This can cause high CPU and spinlock issue.',
 '' ,
 'https://support.microsoft.com/en-us/topic/kb3026083-fix-sos-cachestore-spinlock-contention-on-ad-hoc-sql-server-plan-cache-causes-high-cpu-usage-in-sql-server-798ca4a5-3813-a3d2-f9c4-89eb1128fe68',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('64EEE25A-4B20-4C24-8F27-1E967011D69E', 'Server Performance', 'W', 'Warning', 'usp_HighStmtCount',
 'Some queries had high statement execution count',
 'Some queries had high number statement executions.  This makes it challenging to tune the queries. ', '', '', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('12145143-A34C-4393-BC77-74E3F3A74D5D', 'Server Performance', 'W', 'Warning', 'usp_ExcessiveLockXevent',
 'lock_acquired or lock_released xevent was detected. ',
 'These events can cause high cpu or other performance issues. Turn lock_acquired or lock_released xevent off if not needed or use for very brief periods',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('F9EF91B9-529B-4F72-8545-59689D43D37E', 'Server Performance', 'W', 'Warning', 'usp_McAFee_Intrusion',
 'McAFee Host Intrusion Prevenstion loaded in SQL Process',
 'Loading McAfee Host Intrusion Prevention into SQL can lead to performance and stability issues ', '',
 'https://docs.microsoft.com/troubleshoot/sql/performance/performance-consistency-issues-filter-drivers-modules', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('F332275E-9CF4-4CFA-935D-AE248B74ADE4', 'Query Performance', 'W', 'Warning', 'usp_BatchSort',
 'Batch sort is detected in query plan(s)',
 'Batch sort can cause high CPU or memory grant issues due to cardinality over-estimation ', '',
 'https://docs.microsoft.com/troubleshoot/sql/performance/decreased-perf-high-cpu-optimized-nested-loop', '  ', 1, 100,
 0  , ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('B216976B-7540-4903-B3AD-1895A40B0D4B', 'Query Performance', 'W', 'Warning', 'usp_OptimizerTimeout',
 'Optimizer timeout is detected in query plan(s)',
 'Optimizer timeout can cause a choice of query that run longer than expected and consume more resources. Examine queries for potential optimization',
 '' ,
 'https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/troubleshoot-optimizer-timeout-performance',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('B21D0648-90FD-463B-B32B-C9E710D62B63', 'Query Performance', 'W', 'Warning', 'usp_SmallSampledStats',
 'Some statistics have sample size less than 5%',
 'Default sample size is sufficient for most normal workloads. But unevenly distributed data may require larger sample size or Full Scan sampling.',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('E3CECDDA-EBEB-4E69-940C-660813ED5D93', 'Query Performance', 'W', 'Warning', 'usp_DisabledIndex',
 'Some indexes are disabled',
 'Disabling indexes may cause poor query performance. Check tbl_DisabledIndexes for details ', '',
 'http://aka.ms/nexus/disabledindex', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('63C1DA9B-CAA5-4C9D-9CA0-3916ED6D5F98', 'Server Performance', 'W', 'Warning', 'usp_LongAutoUpdateStats',
 'Long Auto update stats',
 'Some auto statistics update took longer than 60 seconds.  Consider asynchronous stats update ', '',
 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#auto_update_statistics_async', '  ', 1,
 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('6A67B697-F1AF-46D2-99D1-E7B6086B4D5D', 'Server Performance', 'W', 'Warning', 'usp_AccessCheck',
 'Access Check Configuration',
 'access check cache bucket count and access check cache quota are not configured per best practice ', '',
 'https://docs.microsoft.com/en-us/troubleshoot/sql/performance/recommended-updates-configuration-options', '  ', 1,
 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('D3E36E57-4DDE-44D3-939F-13D2A2608F02', 'Server Performance', 'W', 'Warning', 'usp_RedoThreadBlocked',
 'Redo Thread wait ', 'Redo Thread may have waited excessively.  Check tbl_requests for command with DB STARTUP ', '',
 '' , '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('4FE75D34-9AAE-440E-9758-1ABE2AA7B54D', 'Server Performance', 'W', 'Warning', 'usp_VirtualBytesLeak',
 'Virtual bytes leak',
 'Virtual bytes for SQL process were over 7TB.  This may indicate of virtual bytes leak. Please check perfmon counter.',
 '' , 'https://support.microsoft.com/kb/3074434', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('952A2770-4031-4B4F-B56E-6A3A0970FA26', 'Server Performance', 'W', 'Warning', 'usp_DeadlockTraceFlag',
 'Trace flag 1222 or 1204',
 'Trace flag 1222 and 1204 are meant for deadlock troubleshooting only. Do not leave it on permanently.',
 'https://blogs.msdn.microsoft.com/bobsql/2017/05/23/how-it-works-sql-server-deadlock-trace-flag-1222-output/',
 'https://blogs.msdn.microsoft.com/bobsql/2017/05/23/how-it-works-sql-server-deadlock-trace-flag-1222-output/', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('0F58D750-92B4-43A9-BED1-95450EB63175', 'Server Performance', 'W', 'Warning', 'usp_PerfScriptsRunningLong',
 'Perf scripts running long',
 'run time gaps between DMV queries were exceptionally large.  Some of them took more than 60 seconds between runs. check tbl_requests.runtime for details. this can be system issue',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('062A4FCD-C2D9-4A08-B3B0-C57251223450', 'Server Performance', 'W', 'Warning', 'usp_AttendtionCausedBlocking',
 'Attention causing blocking',
 'Some timeouts/attentions could have caused blocking.  see ReadTrace.tblInterestingEvents and vw_HEAD_BLOCKER_SUMMARY',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('047C814A-5D3D-4652-A2CF-A975399D11BF', 'Server Performance', 'W', 'Warning', 'sp_configure_max_memory',
 'Max server memory (MB) is set to default', 'Consider changing the default ''max server memory'' to 75% of RAM', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/server-memory-server-configuration-options#recommendations',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('C05E3B48-3F52-4509-AE30-7B21C303A92D', 'Server Performance', 'W', 'Warning', 'sp_configure_max_dop_zero',
 'Max degree of parallelism is set to 0', 'Consider changing MAXDOP to a value between 2 and 8', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-degree-of-parallelism-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('AA024F38-AA89-4D91-BBEF-DCA763E1600F', 'Server Performance', 'W', 'Warning', 'sp_configure_max_dop_one',
 'Max degree of parallelism is set to 1', 'Consider changing MAXDOP to a value between 2 and 8', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-degree-of-parallelism-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('2168C7D4-F540-4D8D-A56B-6C09640919CE', 'Server Performance', 'W', 'Warning', 'sp_configure_priority_boost',
 'Priority boost is set to 1', 'Change the priority boost value back to default of 0 ', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-priority-boost-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('E8059DEF-A830-417D-8BFA-254509E394E5', 'Server Performance', 'W', 'Warning', 'sp_configure_affinity_IO',
 'Affinity I/O is set to non-default values',
 'Change the affinity I/O value back to default of 0. The option may be deprecated.', '',
 'https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-input-output-mask-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('1227291E-6F11-4BE7-9ED3-6A4AFBABD62D', 'Server Performance', 'W', 'Warning', 'sp_configure_affinity64_IO',
 'Affinity64 I/O is set to non-default values',
 'Change the affinity64 I/O value back to default of 0. The option may be deprecated.', '',
 'https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-input-output-mask-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('64398060-8090-4BF0-BAD7-E88E3B276D40', 'Server Performance', 'W', 'Warning', 'sp_configure_affinity_cpu',
 'Affinity mask is set to non-default values',
 'Change the affinity mask value back to default of 0. The option will be deprecated.', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/affinity-mask-server-configuration-option', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('630F3C37-9FE5-4EA7-AF9A-CEEE154B9A94', 'Server Performance', 'W', 'Warning', 'sp_configure_affinity64_cpu',
 'Affinity64 mask is set to non-default values',
 'Change the affinity64 mask value back to default of 0. The option will be deprecated.', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/affinity-mask-server-configuration-option', '  ',
 1  , 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('B6871A6D-01EC-4247-BBF4-EC04244D6400', 'Server Performance', 'W', 'Warning', 'sp_configure_lightweight_pooling',
 'Lightweight Pooling is set to non-default values',
 'Change the Lightweight Pooling value back to default of 0. The option will be deprecated.', '',
 'https://learn.microsoft.com/sql/database-engine/configure-windows/lightweight-pooling-server-configuration-option',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('322DC73D-E4B8-450A-94B7-484DF292AA01', 'Server Performance', 'W', 'Warning', 'sp_configure_max_woker_threads',
 'Max Worker Thread is set to non-default values',
 'Consider changing the ''max worker threads'' value of back to default of 0 to allow SQL Server to manage the worker thread count and the memory they use.',
 '' ,
 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option',
 '  ', 1, 100, 0, ' ');

INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('25678531-4722-48C4-94B0-026C2ED1021F', 'Server Performance', 'W', 'Warning', 'usp_HighRecompiles',
 'Potential high Recompiles detected, 50+ per second',
 'Potential high recompilations were detected. Please verify with perfmon data.  This can cause high CPU issues.', '',
 '' , '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('47377EC8-BE56-4C92-B0F3-85FC0485D83B', 'Server Performance', 'W', 'Warning', 'usp_HugeGrant',
 'Huge Memory Grant found', 'Queries with big memory grant found check the detail report',
 '/Pages1/Memory%20Grants.aspx',
 'https://techcommunity.microsoft.com/t5/sql-server-support-blog/memory-grants-the-mysterious-sql-server-memory-consumer-with/ba-p/333994',
 ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('FDBF24A1-3EBE-49F1-A02B-FD5686ACDAE9', 'Server Performance', 'W', 'Warning', 'Optimizer_Memory_Leak',
 'Optimizer Memory Leak',
 'MEMORYCLERK_SQLOPTIMIZER memory may be high.  This could be a leak issue which is fixed in SQL 2012 Sp1 CU3',
 'http://support.microsoft.com/kb/2803065', 'http://support.microsoft.com/kb/2803065', ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('5E630273-C14F-4DCE-BDA0-24A1FD8E25CA', 'Server Performance', 'W', 'Warning', 'usp_IOAnalysis', 'Disk IO Analysis',
 'The disk sec/transfer in following drives exceeded 20 ms, check the perfmon for complete analysis', '',
 'https://docs.microsoft.com/en-us/troubleshoot/sql/performance/troubleshoot-sql-io-performance', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('5AE45557-E463-48D6-B135-11AADCB8642F', 'Server Performance', 'I', 'Info', 'usp_WarnmissingIndex',
 'Missing Index detected',
 'There are missing indexes detected.  Review the SQL Nexus report and apply some of the recommendations.', '',
 'https://learn.microsoft.com/sql/relational-databases/indexes/tune-nonclustered-missing-index-suggestions', ' ', 1,
 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('0C73F3D4-6CCC-4FC9-AE37-58110F9C15DB', 'Server Performance', 'I', 'Info', 'StaleStatswarning2008',
 'Stale Stats warning 2008', 'Statistics of some tables has not been updated for over 7 days', '',
 'https://docs.microsoft.com/sql/relational-databases/statistics/statistics#UpdateStatistics', ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('010B3DBA-76CC-46C0-AC1B-3CAD09F95891', 'Server Performance', 'W', 'Warning', 'usp_SQLHighCPUconsumption',
 'SQL High CPU consumption', 'CPU consumption from SQL Server was excessive (>80%) for an extended period of time', '',
 'https://docs.microsoft.com/en-us/troubleshoot/sql/performance/troubleshoot-high-cpu-usage-issues', '  ', 1, 100, 0,
 ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('6C82DA17-D04C-4155-8702-19A9A1363A64', 'Server Performance', 'W', 'Warning', 'usp_KernelHighCPUconsumption',
 'Kernel High CPU consumption', 'Kernel CPU consumption for SQL Server exceeded for an extended period of time.', '',
 '' , '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('6E19B301-E83B-4E5F-AF4E-A8DD2251C1B6', 'Server Performance', 'W', 'Warning', 'usp_Non_SQL_CPU_consumption',
 'High non-SQL CPU consumption detected',
 'Much of the CPU utilization came from non-SQL Server process(es). Review the perfmon data to identify which processes caused this (Process object)',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('57CAA4BB-C7BD-4F96-8040-3224008A3F39', 'Server Performance', 'I', 'Info', 'XEventcrash',
 'XEvent may cause SQL Server crash', 'XEvent session retrieving the Query Hash can result in SQL Server shutdown', '',
 'http://support.microsoft.com/kb/3004355', ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('607B17FD-98F1-498E-9B93-F16E5A155730', 'Server Performance', 'W', 'Warning', 'OracleLinkedServerIssue',
 'Oracle Driver SQL Server crash',
 'Oracle driver loaded in SQL Server memory space may cause SQL Server to crash, refer the KB for solution', '',
 'https://docs.microsoft.com/en-US/troubleshoot/sql/admin/crashes-run-oracle-linked-server-query', ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('BBECDF81-DCCE-4E41-93C9-7EB9E11F53BD', 'Server Performance', 'W', 'Warning', 'usp_ExcessiveTrace_Warning',
 'Many Active Traces Warning',
 'Multiple active SQL traces (> 4) were detected on the server.  This can negatively impact server performance. Review and see if you need all of them to be active',
 '' , 'https://learn.microsoft.com/sql/relational-databases/sql-trace/optimize-sql-trace', ' ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('948756B6-A67F-4CB1-86F9-1B22C26F0B9C', 'Server Performance', 'W', 'Warning', 'usp_Expensive_TraceEvts_Used',
 'Expensive, performance-impacting Trace events were identfied',
 'Multiple non default trace events  were detected running on the server.  This can negatively impact server performance',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('4415F4B3-603F-4F41-978E-9EE32BF2B2E9', 'Server Performance', 'W', 'Warning', 'usp_Expensive_XEvts_Used',
 'Expensive, performance-impacting Extended events were identfied',
 'Multiple non default trace events  were detected running on the server.  This can negatively impact server performance',
 '' , '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('6D4B332C-67A0-428D-A08C-A48A5327DE60', 'Query Performance', 'W', 'Warning', 'usp_oldce',
 'Using Legacy CE for database', 'Consider changing compatibility level to take advantage of Optimizer New CE', '',
 'https://learn.microsoft.com/sql/relational-databases/performance/cardinality-estimation-sql-server', '  ', 1, 100, 0,
 ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('91B2AA56-9CA2-4BDB-8D21-76A5CFF4D74A', 'Server Performance', 'W', 'Warning', 'usp_CalvsCore',
 'CAL license possibly limiting CPU',
 'SQL Server is using CAL license and CPUs are greater than schedulers online, check the errorlog to confirm. You could benefit by upgrading to CORE license.',
 '' ,
 'https://docs.microsoft.com/sql/database-engine/install-windows/upgrade-to-a-different-edition-of-sql-server-setup',
 '  jamgrif', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('4F942C36-D84D-4E34-A696-08C30CDCE3B9', 'Server Performance', 'W', 'Warning', 'usp_Spinlock_HighCPU',
 'High Spinlock rates, likey causing high CPU',
 'Excessive spins have been detected from a spinlock likely driving CPU(s) to high utilization', '',
 'https://learn.microsoft.com/sql/relational-databases/diagnose-resolve-spinlock-contention', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('5945C9B1-ED31-4D20-9093-613C9167BF36', 'Server Performance', 'W', 'Warning', 'usp_NonMS_LoadedModules',
 'Non-MS modules loaded, check if those may impact performance',
 'Non-MS modules loaded in SQL Server memory. If the issue you are t-shooting is unexplained performance or instability, see if disabling some of these modules will alleviate the issue.',
 '' ,
 'https://learn.microsoft.com/troubleshoot/sql/database-engine/performance/performance-consistency-issues-filter-drivers-modules',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('31E879BE-97A4-413F-880F-29BFC57C099F', 'AlwaysOn State', 'W', 'Warning', 'usp_AGHealthState',
 'AlwaysOn replica states', 'AG replica(s) is unhealthy or not online', '', '', '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('476E8B9B-81F7-494C-9039-71CD73EAD719', 'AlwaysOn State', 'W', 'Warning', 'usp_DBMEndPointState',
 'AlwaysOn endpoint state', 'AG DBM endpoint is not online.', '',
 'https://learn.microsoft.com/sql/database-engine/availability-groups/windows/troubleshoot-always-on-availability-groups-configuration-sql-server#Endpoints',
 '  ', 1, 100, 0, ' ');
INSERT INTO dbo.tbl_AnalysisSummary
(
    SolutionSourceId,
    Category,
    Type,
    TypeDesc,
    Name,
    FriendlyName,
    Description,
    InternalUrl,
    ExternalUrl,
    Author,
    Priority,
    SeqNum,
    Status,
    Report
)
VALUES
('EC9C67D8-E91A-452E-856C-4B2E5985716A', 'Server Performance', 'W', 'Warning', 'usp_CCC_Enabled',
 'Common Criteria Compliance Enabled', 'Enabling this can cause performance issues', '',
 'https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/common-criteria-compliance-enabled-server-configuration-option?view=sql-server-ver16',
 '  ', 1, 100, 0, 'Server Configuration');






/*************************************************************************************************

creating rules
**********************************************************************************************/

GO



/***************************************************************************************************

owner:

****************************************************************************************************/
GO
CREATE PROCEDURE usp_AttendtionCausedBlocking
AS
BEGIN
    IF (OBJECT_ID('[ReadTrace].[tblInterestingEvents]') IS NOT NULL)
    BEGIN
        IF EXISTS
        (
            SELECT *
            FROM ReadTrace.tblInterestingEvents evt
                JOIN dbo.vw_HEAD_BLOCKER_SUMMARY bloc
                    ON evt.Session = bloc.head_blocker_session_id
            WHERE evt.EventID = 16
                  AND evt.StartTime < bloc.runtime
        )
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET Status = 1
            WHERE Name = OBJECT_NAME(@@PROCID);

        END;
    END;
END;
GO

CREATE PROCEDURE usp_PerfScriptsRunningLong
AS
DECLARE @PerfStatsGap INT = 60,
        @PerfStatsDefaultInterval INT = 10;

IF EXISTS
(
    SELECT *
    FROM
    (
        SELECT runtime,
               LAG(runtime, 1, runtime) OVER (ORDER BY runtime) AS prev_runtime,
               DATEDIFF(s, LAG(runtime, 1, runtime) OVER (ORDER BY runtime), runtime) gap
        FROM
        (SELECT DISTINCT runtime FROM dbo.tbl_REQUESTS) t
    ) t2
    WHERE gap > @PerfStatsGap
)
BEGIN
    UPDATE dbo.tbl_AnalysisSummary
    SET Status = 1,
        [Description] = 'Run time gaps in perfstats queries were exceptionally large.  Some of them took more than '
                        + CONVERT(VARCHAR(8), @PerfStatsGap) + ' seconds between runs (default interval is '
                        + CONVERT(VARCHAR(8), @PerfStatsDefaultInterval)
                        + '). Check tbl_requests.runtime for details. This can be SQL scheduler or OS system issue.'
    WHERE Name = OBJECT_NAME(@@PROCID);
END;
GO

CREATE PROCEDURE usp_DeadlockTraceFlag
AS
IF (
       (OBJECT_ID('tbl_TraceFlags') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_TraceFlags
        WHERE TraceFlag IN ( 1204, 1222 )
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1,
            [Report] = 'Server Configuration',
            [Description] = 'Trace flag 1222 and 1204 are meant for deadlock troubleshooting only. Do not leave these trace flags on permanently.'
        WHERE Name = OBJECT_NAME(@@PROCID);
    END;
END;
GO


CREATE PROCEDURE usp_ChangeTableCauseHighCPU
AS
IF (OBJECT_ID('[ReadTrace].[tblUniqueBatches]') IS NOT NULL)
BEGIN
    IF EXISTS
    (
        SELECT *
        FROM ReadTrace.tblUniqueBatches
        WHERE NormText LIKE '%CHANGETABLE%'
    )
       OR EXISTS
    (
        SELECT *
        FROM ReadTrace.tblUniqueStatements
        WHERE NormText LIKE '%CHANGETABLE%'
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1
        WHERE Name = OBJECT_NAME(@@PROCID);

    END;
END;
GO

CREATE PROCEDURE usp_RedoThreadBlocked
AS
IF EXISTS
(
    SELECT *
    FROM dbo.tbl_REQUESTS
    WHERE command = 'DB STARTUP'
          AND wait_duration_ms > 15000
)
BEGIN
    UPDATE dbo.tbl_AnalysisSummary
    SET Status = 1
    WHERE Name = OBJECT_NAME(@@PROCID);

END;
GO
--https://support.microsoft.com/en-us/help/3074434/fix-out-of-memory-error-when-the-virtual-address-space-of-the-sql-serv
CREATE PROCEDURE usp_VirtualBytesLeak
AS
IF (OBJECT_ID('CounterDetails') IS NOT NULL)
   AND (OBJECT_ID('CounterData') IS NOT NULL)
BEGIN
    IF EXISTS
    (
        SELECT MAX(CounterValue)
        FROM dbo.CounterDetails a
            JOIN dbo.CounterData b
                ON a.CounterID = b.CounterID
        WHERE CounterName = 'Virtual Bytes'
              AND InstanceName = 'sqlservr'
              AND ObjectName = 'Process'
        HAVING MAX(CounterValue) > 7000000000000
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1
        WHERE Name = OBJECT_NAME(@@PROCID);

    END;
END;
GO

-- Changed usp_AccessCheck SP code to make sure we do not check this for SQL SERVER 2016 and later version

CREATE PROCEDURE usp_AccessCheck
AS
IF (
       (OBJECT_ID('tbl_ServerProperties') IS NOT NULL)
       AND (OBJECT_ID('tbl_Sys_Configurations') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_ServerProperties
        WHERE PropertyName = 'MajorVersion'
              AND PropertyValue < 13
    )
    BEGIN

        IF NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_Sys_Configurations
            WHERE name = 'access check cache bucket count'
                  AND value_in_use = 256
        )
           OR NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_Sys_Configurations
            WHERE name = 'access check cache quota'
                  AND value_in_use = 1024
        )
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET Status = 1
            WHERE Name = OBJECT_NAME(@@PROCID);
        END;
    END;
END;

GO

CREATE PROCEDURE usp_LongAutoUpdateStats
AS
BEGIN
    IF (
           (OBJECT_ID('[ReadTrace].[tblInterestingEvents]') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        DECLARE @statstext NVARCHAR(MAX);
        SELECT @statstext = TextData
        FROM ReadTrace.tblInterestingEvents
        WHERE EventID = 58
              AND (Duration / 1000.00 / 1000.00) > 60
        ORDER BY Duration DESC;
        IF @statstext IS NOT NULL
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET Status = 1,
                Description = 'Some auto statistics update took longer than 60 seconds.  Consider asynchronous stats update.'
                              + @statstext + ' is an example'
            WHERE Name = 'usp_LongAutoUpdateStats';
        END;
    END;
END;
GO


CREATE PROCEDURE usp_DisabledIndex
AS
IF (
       (OBJECT_ID('tbl_DisabledIndexes') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS (SELECT * FROM tbl_DisabledIndexes)
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1
        WHERE Name = 'usp_DisabledIndex';
    END;
END;

GO
CREATE PROCEDURE usp_SmallSampledStats
AS
IF (
       (OBJECT_ID('tbl_dm_db_stats_properties') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_dm_db_stats_properties
        WHERE (rows_sampled * 100.00) / [rows] < 5.0
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1,
            [Description] = [Description]
                            + ' use query select *   from tbl_dm_db_stats_properties where (rows_sampled * 100.00)/ [rows] < 5.0 to identify tables with small sample sizes'
        WHERE Name = 'usp_SmallSampledStats';
    END;
END;

GO
CREATE PROCEDURE usp_BatchSort
AS
DECLARE @FileName NVARCHAR(MAX),
        @Optimized INT,
        @SqlStmt VARCHAR(MAX);

--if xml_plan column is in the table, the the file name (only) of the file that contains the query plan with optimized batch sort
IF (COL_LENGTH('dbo.tblTopSqlPlan', 'xml_plan') IS NULL)
BEGIN
    SELECT TOP 1
           @FileName = RIGHT([FileName], CHARINDEX('\', REVERSE([FileName])) - 1),
           @Optimized = c.value('@Optimized[1]', 'int')
    FROM
    (SELECT FileName, xml_plan AS QueryPlan FROM dbo.tblTopSqlPlan) a
        CROSS APPLY a.QueryPlan.nodes('declare namespace SP="http://schemas.microsoft.com/sqlserver/2004/07/showplan";//SP:NestedLoops') AS t(c)
    WHERE c.value('@Optimized[1]', 'int') = 1;
END;

IF (@FileName IS NOT NULL)
BEGIN
    UPDATE dbo.tbl_AnalysisSummary
    SET [Status] = 1,
        [Description] = 'Found a query plan with a batch sort. ''' + @FileName
                        + ''' contains an example query plan. Batch sort can cause high CPU or memory grant issues due to cardinality over-estimation. Examine plan and query duration. '
    WHERE [Name] = 'usp_BatchSort';
END;

GO
CREATE PROCEDURE usp_OptimizerTimeout
AS

--create a report on tbl_OptimizerTimeout_Statements

IF (OBJECT_ID('tbl_OptimizerTimeout_Statements') IS NULL)
BEGIN

    DECLARE @FileName VARCHAR(128) = N'',
            @StmtId INT,
            @StmtTxt VARCHAR(256);

    SET QUOTED_IDENTIFIER ON;

    WITH XMLNAMESPACES
    (
        'http://schemas.microsoft.com/sqlserver/2004/07/showplan' AS sp
    )
    SELECT DISTINCT
           RIGHT([FileName], CHARINDEX('\', REVERSE([FileName])) - 1) AS FileName,
           stmt.stmt_details.value('@StatementId', 'int') AS StatementId,
           stmt.stmt_details.value('@StatementOptmLevel', 'varchar(64)') AS StatementOptmLevel,
           stmt.stmt_details.value('@StatementOptmEarlyAbortReason', 'varchar(64)') AS StatementOptmEarlyAbortReason,
           stmt.stmt_details.value('@CardinalityEstimationModelVersion', 'int') AS CE_Model,
           stmt.stmt_details.value('@StatementSubTreeCost', 'decimal(10,6)') AS StatementSubTreeCost,
           stmt.stmt_details.value('@StatementText', 'varchar(max)') AS StatementText,
           stmt.stmt_details.value('@QueryHash', 'varchar(128)') AS QueryHash,
           stmt.stmt_details.value('@QueryPlanHash', 'varchar(128)') AS QueryPlanHash,
           stmt.stmt_details.value('@RetrievedFromCache', 'varchar(16)') AS RetrievedFromCache
    INTO dbo.tbl_OptimizerTimeout_Statements
    FROM
    (SELECT FileName, xml_plan AS sqlplan FROM dbo.tblTopSqlPlan) AS p
        CROSS APPLY sqlplan.nodes('//sp:StmtSimple') AS stmt(stmt_details)
    WHERE stmt.stmt_details.value('@StatementOptmEarlyAbortReason', 'varchar(max)') = 'Timeout';


    IF (@@ROWCOUNT > 0)
    BEGIN
        SELECT TOP 1
               @FileName = FileName,
               @StmtId = StatementId,
               @StmtTxt = SUBSTRING(StatementText, 0, 256)
        FROM dbo.tbl_OptimizerTimeout_Statements
        ORDER BY StatementSubTreeCost DESC;


        IF (@FileName IS NOT NULL)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Description] = 'Found a query plan with an optimizer timeout. File ''' + @FileName
                                + ''' contains an example query plan, with Statement text starting like this:'''
                                + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) + @StmtTxt + '''. ' + CHAR(13) + CHAR(10)
                                + CHAR(13) + CHAR(10)
                                + 'An optimizer timeout can cause a choice of query that runs longer than expected and consume more resources. Examine queries for potential optimization '
            WHERE [Name] = OBJECT_NAME(@@PROCID);
        END;
    END;
END;

GO


CREATE PROCEDURE usp_McAFee_Intrusion
AS
IF (
       (OBJECT_ID('tbl_dm_os_loaded_modules') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS
    (
        SELECT name
        FROM dbo.tbl_dm_os_loaded_modules
        WHERE name LIKE '%HcThe%'
              OR name LIKE '%HcApi%'
              OR name LIKE '%HcSql%'
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1
        WHERE Name = 'usp_McAFee_Intrusion';
    END;
END;

GO

CREATE PROCEDURE usp_ExcessiveLockXevent
AS
IF (
       (OBJECT_ID('tbl_XEvents') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_XEvents
        WHERE event_name IN ( 'lock_released', 'lock_acquired' )
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1
        WHERE Name = 'usp_ExcessiveLockXevent';
    END;
END;

GO
CREATE PROCEDURE usp_HighCacheCount
AS
BEGIN
    IF (OBJECT_ID('CounterDetails') IS NOT NULL)
       AND (OBJECT_ID('CounterData') IS NOT NULL)
    BEGIN
        IF
        (
            SELECT MAX(CAST(CounterValue AS FLOAT))
            FROM dbo.CounterDetails c
                JOIN dbo.CounterData dat
                    ON c.CounterID = dat.CounterID
            WHERE CounterName = 'Cache Object Counts'
        ) > 100000
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'usp_HighCacheCount';
        END;
    END;
END;

GO
CREATE PROCEDURE usp_HighStmtCount
AS
BEGIN
    IF (OBJECT_ID('[ReadTrace].[tblStatements]') IS NOT NULL)
    BEGIN
        IF
        (
            SELECT MAX(StmtCount)
            FROM
            (
                SELECT BatchSeq,
                       COUNT(*) 'StmtCount'
                FROM ReadTrace.tblStatements
                GROUP BY BatchSeq
            ) t
        ) > 1000
        BEGIN

            DECLARE @query NVARCHAR(MAX);

            SELECT @query = NormText
            FROM ReadTrace.tblBatches bat
                JOIN ReadTrace.tblUniqueBatches ub
                    ON bat.HashID = ub.HashID
            WHERE bat.BatchSeq IN
                  (
                      SELECT TOP 1
                             bat.BatchSeq
                      FROM ReadTrace.tblStatements st
                          JOIN ReadTrace.tblBatches bat
                              ON st.BatchSeq = bat.BatchSeq
                      GROUP BY bat.BatchSeq
                      ORDER BY COUNT(*) DESC
                  );

            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Description] = [Description] + ' here is an example query: ' + @query
            WHERE Name = 'usp_HighStmtCount';
        END;
    END;
END;
GO

CREATE FUNCTION dbo.fn_CPUthresheldCrossed
()
RETURNS INT
AS
BEGIN
    DECLARE @ret INT;
    DECLARE @cpu_count VARCHAR(MAX);
    SELECT @cpu_count = PropertyValue
    FROM dbo.tbl_ServerProperties
    WHERE PropertyName = 'cpu_count';
    IF (CAST(@cpu_count AS INT) >= 32)
    BEGIN
        SET @ret = 1;
    END;
    ELSE
    BEGIN
        SET @ret = 0;
    END;
    RETURN @ret;
END;
GO
CREATE PROCEDURE proc_CheckTraceFlags
AS
IF (
       (OBJECT_ID('tbl_ServerProperties') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF (dbo.fn_CPUthresheldCrossed() = 1)
    BEGIN
        DECLARE @raise BIT = 1;
        IF EXISTS
        (
            SELECT *
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName = 'MajorVersion'
                  AND CAST(PropertyValue AS INT) >= 13
        )
            SET @raise = 0;
        IF EXISTS
        (
            SELECT *
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName = 'MajorVersion'
                  AND CAST(PropertyValue AS INT) = 12
        )
           AND EXISTS
        (
            SELECT *
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName = 'ProductLevel'
                  AND PropertyValue >= 'SP2'
        )
            SET @raise = 0;

        IF @raise = 1
           AND NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_TraceFlags
            WHERE TraceFlag = 4199
        )
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Trace Flag 4199';

        IF @raise = 1
           AND @raise = 1
           AND NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_TraceFlags
            WHERE TraceFlag = 1118
        )
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Trace Flag 1118';

        IF @raise = 1
           AND NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_TraceFlags
            WHERE TraceFlag = 8048
        )
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Trace Flag 8048';

        IF @raise = 1
           AND NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_TraceFlags
            WHERE TraceFlag = 1236
        )
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Trace Flag 1236';
        IF @raise = 1
           AND NOT EXISTS
        (
            SELECT *
            FROM dbo.tbl_TraceFlags
            WHERE TraceFlag = 9024
        )
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Trace Flag 9024';
    END;
END;
GO

CREATE PROCEDURE usp_HighCompile --author:jackli
AS
BEGIN
    IF (OBJECT_ID('CounterDetails') IS NOT NULL)
       AND (OBJECT_ID('CounterData') IS NOT NULL)
    BEGIN
        IF
        (
            SELECT MAX(CAST(dat.CounterValue AS FLOAT))
            FROM dbo.CounterDetails c
                JOIN dbo.CounterData dat
                    ON c.CounterID = dat.CounterID
            WHERE c.CounterName = 'SQL Compilations/sec'
        ) > 300
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'usp_HighCompile';
        END;
    END;
END;

GO
CREATE PROCEDURE usp_RG_Idle
AS
BEGIN

    IF
    (
        SELECT MAX(waiting_tasks_count)
        FROM dbo.tbl_OS_WAIT_STATS
        WHERE wait_type = 'RESOURCE_GOVERNOR_IDLE'
    ) > 10000000
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1
        WHERE Name = 'usp_RG_Idle';
    END;

END;
GO

-- create procedure proc_ExcessiveXevents
-- as
-- IF ((OBJECT_ID ('tbl_XEvents') IS NOT NULL) and (OBJECT_ID ('tbl_AnalysisSummary') IS NOT NULL))
-- begin
-- 	if exists (
-- 		select event_name from tbl_XEvents
-- 		where  event_name in
-- 		('query_pre_execution_showplan', 'query_post_execution_showplan','query_post_compilation_showplan','lock_acquired','sql_statement_starting','sql_statement_completed','sp_statement_starting','sp_statement_completed')
-- 	)
-- 	begin
-- 		update tbl_AnalysisSummary
-- 		set [status]=1
-- 		where name ='Detailed XEvent Tracing'
-- 	end
-- end

GO

CREATE PROCEDURE proc_PowerPlan
AS
IF (
       (OBJECT_ID('tbl_PowerPlan') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    IF NOT EXISTS
    (
        SELECT *
        FROM dbo.tbl_PowerPlan
        WHERE ActivePlanName LIKE '%High Performance%'
    )
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET Status = 1
        WHERE Name = 'PowerPlan';
    END;
END;

GO

CREATE PROCEDURE proc_AutoStats
AS
IF (
       (OBJECT_ID('tbl_SysDatabases') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    UPDATE dbo.tbl_SysDatabases
    SET is_auto_update_stats_on = 0
    WHERE name = 'notexist';
    SELECT *
    FROM dbo.tbl_AnalysisSummary;
    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_SysDatabases
        WHERE is_auto_create_stats_on = 0
    )
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1
        WHERE Name = 'AutoCreateStats';


    IF EXISTS
    (
        SELECT *
        FROM dbo.tbl_SysDatabases
        WHERE is_auto_update_stats_on = 0
    )
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1
        WHERE Name = 'AutoUpdateStats';
END;

GO


CREATE PROCEDURE proc_ConfigAlert
AS
IF (
       (OBJECT_ID('tbl_Sys_Configurations') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    SELECT *
    INTO #sp_configure_alerts
    FROM dbo.tbl_Sys_Configurations conf
    WHERE (
              conf.name = 'max server memory (MB)'
              AND conf.value_in_use = 2147483647
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options?view=sql-server-ver16#recommendations
          OR
          (
              conf.name = 'max degree of parallelism'
              AND conf.value_in_use IN ( 0, 1 )
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-degree-of-parallelism-server-configuration-option?view=sql-server-ver16
          OR
          (
              conf.name = 'priority boost'
              AND conf.value_in_use = 1
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-priority-boost-server-configuration-option?view=sql-server-ver16
          OR
          (
              conf.name LIKE 'affinity% I/O mask'
              AND conf.value_in_use <> 0
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-input-output-mask-server-configuration-option?view=sql-server-ver16
          OR
          (
              conf.name = 'affinity mask'
              AND conf.value_in_use <> 0
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-mask-server-configuration-option?view=sql-server-ver16
          OR
          (
              conf.name = 'affinity64 mask'
              AND conf.value_in_use <> 0
          )
          OR
          (
              conf.name = 'lightweight pooling'
              AND conf.value_in_use <> 0
          ) --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/lightweight-pooling-server-configuration-option?view=sql-server-ver16
          OR
          (
              conf.name = 'max worker threads'
              AND conf.value_in_use <> 0
          ); --https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option?view=sql-server-ver16


    IF @@ROWCOUNT > 0
    BEGIN

        DECLARE @value_in_use VARCHAR(32),
                @config_name VARCHAR(128),
                @descr_str VARCHAR(MAX) = '',
                @external_url VARCHAR(256) = '';

        DECLARE c_conf CURSOR FOR
        SELECT CONVERT(VARCHAR(32), value_in_use) AS value_in_use,
               name
        FROM #sp_configure_alerts;

        OPEN c_conf;
        FETCH NEXT FROM c_conf
        INTO @value_in_use,
             @config_name;

        WHILE (@@fetch_status = 0)
        BEGIN

            IF (@config_name = 'max server memory (MB)' AND @value_in_use = 2147483647)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the default ''max server memory'' value of ''' + @value_in_use
                      + ''' to a set value and provide sufficient memory for the OS and other processes (e.g. 75% of RAM). See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/server-memory-server-configuration-options#recommendations';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_max_memory';
            END;

            IF (@config_name = 'max degree of parallelism' AND @value_in_use = 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the default ''max degree of parallelism'' value of ''' + @value_in_use
                      + ''' to a value between 2 and 8 to avoid impacting performance of other tasks. See MAX DOP article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-degree-of-parallelism-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_max_dop_zero';
            END;

            IF (@config_name = 'max degree of parallelism' AND @value_in_use = 1)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the the current ''max degree of parallelism'' value of ''' + @value_in_use
                      + ''' to a value between 2 and 8 to allow the use of parallel processing. See MAX DOP article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-degree-of-parallelism-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_max_dop_one';
            END;

            IF (@config_name = 'priority boost' AND @value_in_use = 1)
            BEGIN
                SELECT @descr_str
                    = 'Change the current ''priority boost'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent system-wide performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-priority-boost-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_priority_boost';
            END;
            IF (@config_name = 'affinity I/O mask' AND @value_in_use <> 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the current ''affinity I/O mask'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent potential I/O or CPU scheduling performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-input-output-mask-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_affinity_IO';
            END;
            IF (@config_name = 'affinity64 I/O mask' AND @value_in_use <> 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the current ''affinity64 I/O mask'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent potential I/O or CPU scheduling performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/affinity-input-output-mask-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_affinity64_IO';
            END;
            IF (@config_name = 'affinity mask' AND @value_in_use <> 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the current ''affinity mask'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent potential CPU scheduling performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/affinity-mask-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_affinity_cpu';
            END;
            IF (@config_name = 'affinity64 mask' AND @value_in_use <> 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the current ''affinity64 mask'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent potential CPU scheduling performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/affinity-mask-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_affinity64_cpu';
            END;
            IF (@config_name = 'lightweight pooling' AND @value_in_use = 1)
            BEGIN
                SELECT @descr_str
                    = 'Change the current ''lightweight pooling'' value of ''' + @value_in_use
                      + ''' back to the default value of 0 to prevent potential CPU scheduling performance issues. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/lightweight-pooling-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_lightweight_pooling';
            END;
            IF (@config_name = 'max worker threads' AND @value_in_use <> 0)
            BEGIN
                SELECT @descr_str
                    = 'Consider changing the current ''max worker threads'' value of ''' + @value_in_use
                      + ''' to allow SQL Server to manage the worker thread count and the memory they use. See article for more information';
                SELECT @external_url
                    = 'https://learn.microsoft.com/sql/database-engine/configure-windows/configure-the-max-worker-threads-server-configuration-option';

                UPDATE dbo.tbl_AnalysisSummary
                SET [Status] = 1,
                    Description = @descr_str,
                    ExternalUrl = @external_url,
                    Report = 'Server Configuration'
                WHERE Name = 'sp_configure_max_woker_threads';
            END;



            FETCH NEXT FROM c_conf
            INTO @value_in_use,
                 @config_name;
        END;

        CLOSE c_conf;
        DEALLOCATE c_conf;

    END;

    DROP TABLE #sp_configure_alerts;
END;


GO


CREATE PROCEDURE usp_AGHealthState
AS
BEGIN
    DECLARE @avail_group_name VARCHAR(128),
            @op_state VARCHAR(64),
            @con_state VARCHAR(64),
            @sync_health_state VARCHAR(64);
    DECLARE @msg_string VARCHAR(MAX);

    IF (OBJECT_ID('tbl_hadr_ag_replica_states') IS NOT NULL)
    BEGIN
        SELECT TOP 1
               @avail_group_name = group_name,
               @op_state = operational_state_desc,
               @con_state = connected_state_desc,
               @sync_health_state = synchronization_health_desc
        FROM dbo.tbl_hadr_ag_replica_states
        WHERE operational_state_desc IN ( 'OFFLINE', 'FAILED', 'FAILED_NO_QUORUM' )
              OR synchronization_health_desc <> 'HEALTHY';

        IF (@@ROWCOUNT > 0 AND @avail_group_name IS NOT NULL)
        BEGIN
            SET @msg_string
                = 'At least one AG replica is unhealthy or not online. For example, AG = ''' + @avail_group_name
                  + ''' has operational state = ''' + @op_state + ''' connection state = ''' + @con_state
                  + ''' and sync_health_state  = ''' + @sync_health_state + '''. Check AG report for details.';

            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Description] = @msg_string,
                [Report] = 'AlwaysOn Report'
            WHERE Name = OBJECT_NAME(@@PROCID);
        END;
    END;

END;
GO

CREATE PROCEDURE usp_DBMEndPointState
AS
BEGIN
    DECLARE @hadr_endpoint VARCHAR(128),
            @endpoint_type VARCHAR(64),
            @endpoint_state VARCHAR(64);
    DECLARE @msg_string VARCHAR(MAX);

    IF (OBJECT_ID('tbl_hadr_endpoints_principals') IS NOT NULL)
    BEGIN
        SELECT @hadr_endpoint = name,
               @endpoint_type = type_desc,
               @endpoint_state = state_desc
        FROM dbo.tbl_hadr_endpoints_principals
        WHERE state_desc <> 'STARTED';

        IF (@@ROWCOUNT > 0 AND @hadr_endpoint IS NOT NULL)
        BEGIN
            SET @msg_string
                = 'The AG endpoint is not online. Endpoint name ''' + @hadr_endpoint + ''', endpoint type = '''
                  + @endpoint_type + ''', endpoint state = ''' + @endpoint_state + '';

            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Description] = @msg_string,
                [Report] = 'AlwaysOn Report'
            WHERE Name = OBJECT_NAME(@@PROCID);
        END;
    END;
END;
GO

CREATE PROCEDURE usp_HugeGrant
AS
BEGIN
    DECLARE @cnt INT,
            @max_grant_size DECIMAL(7, 2),
            @avg_grant_size DECIMAL(7, 2);
    SET @cnt = 0;
    SET @avg_grant_size = 0;
    SET @max_grant_size = 0;

    IF (
           (OBJECT_ID('tbl_dm_exec_query_memory_grants') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        SELECT @cnt = COUNT(*),
               @avg_grant_size = AVG(granted_memory_kb) / 1024 / 1024.0,
               @max_grant_size = MAX(granted_memory_kb) / 1024 / 1024.0
        FROM dbo.tbl_dm_exec_query_memory_grants
        WHERE granted_memory_kb / 1024 > 1024;

        IF @cnt > 0
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Description] = 'There are ' + CONVERT(VARCHAR, @cnt)
                                + ' queries with memory grants using > 1 GB of memory. Average grant size = '
                                + CONVERT(VARCHAR, @avg_grant_size) + ' GB. The largest grant size found = '
                                + CONVERT(VARCHAR, @max_grant_size)
                                + ' GB. Consider tuning queries to reduce memory usage.'
            WHERE Name = 'usp_HugeGrant';
        END;
    END;
END;

GO

CREATE PROCEDURE Optimizer_Memory_Leak
AS
BEGIN
    DECLARE @SQLVersion VARCHAR(100);
    DECLARE @is_Rulehit INT;
    DECLARE @t_MajorVersion INT;
    DECLARE @t_MinorVersion INT;
    --declare the delimeter between each minor version
    DECLARE @Delimeter CHAR(1);
    SET @Delimeter = '.';
    SET @t_MajorVersion = 0;
    SET @t_MinorVersion = 0;
    --Parse the string and insert each  minor version into the @tblSQLVersion  table
    DECLARE @tblSQLVersion TABLE
    (
        id INT IDENTITY,
        tVersion VARCHAR(50)
    );
    DECLARE @tSQLVersion VARCHAR(50);
    DECLARE @StartPos INT,
            @Length INT;
    SET @is_Rulehit = 0;
    DECLARE @i INT;
    SET @i = 0;
    SELECT @i = COUNT(*)
    FROM sys.objects
    WHERE name = 'tbl_SCRIPT_ENVIRONMENT_DETAILS';

    IF @i > 0
    BEGIN
        SET @is_Rulehit = 1;
        SELECT @SQLVersion = Value
        FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
        WHERE Name LIKE '%SQL Version%';
    END;
    IF (@is_Rulehit > 0)
    BEGIN
        WHILE LEN(@SQLVersion) > 0
        BEGIN
            SET @StartPos = CHARINDEX(@Delimeter, @SQLVersion);
            IF @StartPos < 0
                SET @StartPos = 0;
            SET @Length = LEN(@SQLVersion) - @StartPos - 1;
            IF @Length < 0
                SET @Length = 0;
            IF @StartPos > 0
            BEGIN
                SET @tSQLVersion = SUBSTRING(@SQLVersion, 1, @StartPos - 1);
                SET @SQLVersion = SUBSTRING(@SQLVersion, @StartPos + 1, LEN(@SQLVersion) - @StartPos);
            END;
            ELSE
            BEGIN
                SET @tSQLVersion = @SQLVersion;
                SET @SQLVersion = '';
            END;
            INSERT @tblSQLVersion
            (
                tVersion
            )
            VALUES
            (@tSQLVersion);
        END;

        --Get minor version from  @tblSQLVersion  table
        SELECT @t_MajorVersion = CONVERT(INT, tVersion)
        FROM @tblSQLVersion
        WHERE id = 1;
        SELECT @t_MinorVersion = CONVERT(INT, tVersion)
        FROM @tblSQLVersion
        WHERE id = 3;
        IF (@t_MajorVersion = 11 AND @t_MinorVersion < 2331)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'Optimizer_Memory_Leak';
        END;
    END;

END;
GO

/****** Object:  StoredProcedure [dbo].[usp_IOAnalysis]    Script Date: 1/26/2022 3:03:56 PM ******/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE PROCEDURE [dbo].[usp_IOAnalysis]
AS
BEGIN
    IF (
           (OBJECT_ID('CounterData') IS NOT NULL)
           AND (OBJECT_ID('CounterDetails') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN

        DECLARE @max_sec_transfer DECIMAL(12, 3);

        DECLARE @IO_threshold DECIMAL(12, 3);
        DECLARE @T_CounterDateTime DATETIME;
        DECLARE @prolonged_avg_sec_transfer DECIMAL(12, 3),
                @drive VARCHAR(128);
        SET @IO_threshold = 0.020;

        CREATE TABLE #tmp
        (
            CounterDateTime DATETIME,
            CounterValue DECIMAL(12, 3),
            ObjectName VARCHAR(1024),
            CounterName VARCHAR(1024),
            InstanceName VARCHAR(1024)
        );
        CREATE INDEX Idx_Dt
        ON #tmp (CounterDateTime)
        INCLUDE (
                    CounterValue,
                    ObjectName,
                    CounterName,
                    InstanceName
                );

        INSERT INTO #tmp
        (
            CounterDateTime,
            CounterValue,
            ObjectName,
            CounterName,
            InstanceName
        )
        SELECT CONVERT(DATETIME, dat.CounterDateTime),
               dat.CounterValue,
               dl.ObjectName,
               dl.CounterName,
               dl.InstanceName
        FROM dbo.CounterData dat
            INNER JOIN dbo.CounterDetails dl
                ON dat.CounterID = dl.CounterID
        WHERE dl.ObjectName IN ( 'logicaldisk' )
              AND dl.CounterName IN ( 'Avg. Disk sec/Transfer' )
              AND dl.InstanceName <> '_Total'
              AND dat.CounterValue >= @IO_threshold;

        IF (@@ROWCOUNT > 0)
        BEGIN

            DECLARE C_CounterDateTime CURSOR FOR
            SELECT DISTINCT
                   CounterDateTime
            FROM #tmp;

            OPEN C_CounterDateTime;
            FETCH NEXT FROM C_CounterDateTime
            INTO @T_CounterDateTime;

            WHILE (@@fetch_status = 0)
            BEGIN

                --find the avg value within a 1 min range. If that is high, we really have an I/O issue
                SELECT @prolonged_avg_sec_transfer = AVG(CounterValue),
                       @drive = InstanceName
                FROM #tmp
                WHERE CounterDateTime
                BETWEEN (@T_CounterDateTime - '00:00:30') AND (@T_CounterDateTime + '00:00:30')
                GROUP BY ObjectName,
                         CounterName,
                         InstanceName
                HAVING AVG(CounterValue) >= @IO_threshold
                       AND COUNT(CounterValue) > 1;


                IF (@@ROWCOUNT > 0)
                BEGIN
                    SELECT @prolonged_avg_sec_transfer,
                           @drive;

                    UPDATE dbo.tbl_AnalysisSummary
                    SET [Status] = 1,
                        Description = 'The "Avg. Disk sec/transfer" on some drives exceeded 20 ms for 1 min or more (not just a spike). Example: Drive '''
                                      + @drive + ''', ' + CONVERT(VARCHAR(32), @prolonged_avg_sec_transfer)
                                      + ' sec/transfer. Check the Perfmon for complete analysis',
                        Report = 'Perfmon IO'
                    WHERE Name = 'usp_IOAnalysis';

                    BREAK; -- we found one value, so we can quit the loop
                END;

                FETCH NEXT FROM C_CounterDateTime
                INTO @T_CounterDateTime;
            END;

            CLOSE C_CounterDateTime;
            DEALLOCATE C_CounterDateTime;

            DROP TABLE #tmp;
        END;
    END;
END;

GO

CREATE PROCEDURE [usp_WarnmissingIndex]
AS
BEGIN
    DECLARE @t_DisplayMessage NVARCHAR(1256);
    DECLARE @t_improvement_measure VARCHAR(100);
    DECLARE @t_create_index_statement VARCHAR(100);
    DECLARE @is_Rulehit INT;
    DECLARE @message_Number INT;
    SET @is_Rulehit = 0;
    SET @t_DisplayMessage = N'';
    SET @message_Number = 1;
    CREATE TABLE #tmp
    (
        improvement_measure VARCHAR(100),
        create_index_statement VARCHAR(5000)
    );
    DECLARE @i INT;
    SET @i = 0;
    SELECT @i = COUNT(*)
    FROM sys.objects
    WHERE name = 'tbl_MissingIndexes';
    IF @i > 0
    BEGIN
        INSERT INTO #tmp
        (
            improvement_measure,
            create_index_statement
        )
        SELECT TOP 5
               improvement_measure,
               create_index_statement
        FROM dbo.tbl_MissingIndexes
        WHERE improvement_measure > 1000000
        ORDER BY improvement_measure DESC;
    END;
    SELECT @is_Rulehit = COUNT(*)
    FROM #tmp;
    IF (@is_Rulehit > 0)
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1,
            Report = 'Missing Indexes'
        WHERE Name = 'usp_WarnmissingIndex';

    END;

    DROP TABLE #tmp;
END;

GO


CREATE PROCEDURE StaleStatswarning2008
AS
BEGIN

    IF OBJECT_ID('tbl_SysIndexes') IS NOT NULL
    BEGIN
        /*Begin  */
        DECLARE @t_DisplayMessage NVARCHAR(1500);
        DECLARE @t_DB_Name_orID VARCHAR(100);
        DECLARE @t_Number_ofObjects VARCHAR(100);
        DECLARE @is_Rulehit INT;
        SET @is_Rulehit = 0;
        SET @t_DisplayMessage = N'';
        CREATE TABLE #tmp
        (
            DB_Name_orID VARCHAR(100),
            Number_ofObjects VARCHAR(100)
        );
        DECLARE @i INT;
        SET @i = 0;
        SELECT @i = COUNT(*)
        FROM sysobjects
        WHERE LTRIM(RTRIM(name)) IN ( 'tbl_SPHELPDB', 'tbl_SysIndexes' );
        IF @i > 1
        BEGIN

            INSERT INTO #tmp
            (
                DB_Name_orID,
                Number_ofObjects
            )
            SELECT TOP 4
                   (
                       SELECT DISTINCT name FROM dbo.tbl_SPHELPDB a WHERE a.dbid = b.dbid
                   ) AS DBNAME,
                   COUNT(*) AS [Number_of_objects]
            FROM dbo.tbl_SysIndexes b
            WHERE stats_updated <
            (
                SELECT CAST(Value AS DATETIME)
                FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
                WHERE Name LIKE '%Script Begin Time%'
            )
                  AND CONVERT(BIGINT, dbid) > 4
            GROUP BY dbid
            ORDER BY 1;
        END;
        ELSE
        BEGIN
            INSERT INTO #tmp
            (
                DB_Name_orID,
                Number_ofObjects
            )
            SELECT TOP 4
                   dbid,
                   COUNT(*) AS [Number_of_objects]
            FROM dbo.tbl_SysIndexes b
            WHERE stats_updated <
            (
                SELECT CAST(Value AS DATETIME)
                FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
                WHERE Name LIKE '%Script Begin Time%'
            )
                  AND CONVERT(BIGINT, dbid) > 4
            GROUP BY dbid
            ORDER BY 1;
        END;
        SELECT @is_Rulehit = COUNT(*)
        FROM #tmp;
        IF (@is_Rulehit > 0)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'StaleStatswarning2008';
        END;
        DROP TABLE #tmp;
    END;
END;
GO


CREATE PROCEDURE [usp_SQLHighCPUconsumption]
AS
SET NOCOUNT ON;
BEGIN
    IF (
           (OBJECT_ID('CounterData') IS NOT NULL)
           AND (OBJECT_ID('CounterDetails') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        DECLARE @t_CBeginTime DATETIME;
        DECLARE @is_Rulehit INT;
        DECLARE @cpuCount INT;
        DECLARE @CPU_threshold DECIMAL(20, 2);
        DECLARE @T_CounterDateTime DATETIME;
        DECLARE @t_AvgValue INT;
        DECLARE @InstanceIndex INT;

        SET @is_Rulehit = 0;
        SET @cpuCount = 0;


        CREATE TABLE #tmpCounterDateTime
        (
            CounterDateTime DATETIME,
            InstanceIndex INT
        );

        --get the CPUs

        --try to find CPU count different possible ways.  if we get zero or null, try another way

        DECLARE @schedCount INT = 0;
        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'tbl_ServerProperties')
        --#1 try 
        BEGIN
            --make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
            DECLARE @cpuRows INT = 0;
            --unless there is more than 1 row
            SELECT DISTINCT
                   @cpuRows = COUNT(PropertyValue)
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName IN ( 'cpu_count' );
            IF (@cpuRows = 1) --then we are OK to use the number
            BEGIN
                SELECT DISTINCT
                       @cpuCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'cpu_count' );
                SELECT DISTINCT
                       @schedCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'number of visible schedulers' );
            END;
        END; -- END #1
        -- #2 try
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            --consider doing this now 
            IF (OBJECT_ID('CounterDetails') IS NOT NULL)
            BEGIN
                IF 0 =
                (
                    SELECT COUNT(*)
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.CounterDetails')
                          AND name = 'procCount_idx'
                )
                BEGIN
                    CREATE INDEX procCount_idx
                    ON dbo.CounterDetails (ObjectName)
                    INCLUDE (
                                CounterName,
                                InstanceName
                            )
                    WHERE ObjectName IN ( 'Processor Information' )
                          AND CounterName IN ( '% User Time' );
                END;
                --as long as table existed, and we either created index or it is already there, try to get CPUs
                SELECT @cpuCount = COUNT(DISTINCT dli.InstanceName)
                FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dli
                        ON dat.CounterID = dli.CounterID
                WHERE dli.ObjectName IN ( 'Processor Information' )
                      AND dli.CounterName IN ( '% User Time' )
                      AND dli.InstanceName NOT LIKE ('%_Total%');
            END;
        END; --END #2
             -- #3 TRY  ******
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            SELECT @cpuCount = CAST(MAX(CounterValue) / 100 AS INT)
            FROM [dbo].[CounterData] cdat
                JOIN [dbo].[CounterDetails] cdet
                    ON cdat.CounterID = cdet.CounterID
            WHERE ObjectName = 'Process'
                  AND CounterName = '% Processor Time'
                  AND InstanceName = '_Total'
                  AND CounterValue != 0;
        END; --END #3

        --did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly

        IF (@cpuCount = 0 OR @cpuCount IS NULL)
            RETURN;

        --set threshold at 80% of total CPU capacity
        SET @CPU_threshold = 80 * @cpuCount;

        INSERT INTO #tmpCounterDateTime
        (
            CounterDateTime,
            InstanceIndex
        )
        SELECT CONVERT(DATETIME, dat.CounterDateTime),
               ISNULL(detl.InstanceIndex, 0)
        FROM dbo.CounterData dat
            INNER JOIN dbo.CounterDetails detl
                ON dat.CounterID = detl.CounterID
        WHERE detl.ObjectName IN ( 'Process' )
              AND detl.CounterName IN ( '% User Time' )
              AND detl.InstanceName LIKE 'sqlservr%'
              AND dat.CounterValue > @CPU_threshold;

        CREATE CLUSTERED INDEX dt_idx_kernel
        ON #tmpCounterDateTime (CounterDateTime);

        SELECT @is_Rulehit = COUNT(*),
               @t_CBeginTime = MIN(CounterDateTime)
        FROM #tmpCounterDateTime;

        IF (@is_Rulehit > 0)
        BEGIN
            DECLARE C_CounterDateTime CURSOR FOR
            SELECT CounterDateTime,
                   InstanceIndex
            FROM #tmpCounterDateTime;

            OPEN C_CounterDateTime;

            FETCH NEXT FROM C_CounterDateTime
            INTO @T_CounterDateTime,
                 @InstanceIndex;

            WHILE (@@fetch_status = 0)
            BEGIN

                --approximate algorithm - if avg CPU usage over a 3 min period exceeds the threshold - this means is prolonged
                --walk the time windows one at a time in a cursor
                SELECT @t_AvgValue = AVG(CAST(dat.CounterValue AS BIGINT))
                FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dli
                        ON dat.CounterID = dli.CounterID
                WHERE dli.ObjectName IN ( 'Process' )
                      AND dli.CounterName IN ( '% User Time' )
                      AND dli.InstanceName LIKE 'sqlservr%'
                      AND dat.CounterDateTime
                      BETWEEN (@T_CounterDateTime - '00:01:30') AND (@T_CounterDateTime + '00:01:30')
                      AND ISNULL(dli.InstanceIndex, 0) = @InstanceIndex; -- this would impact perf potentially, but alternatives are ugly


                IF (@t_AvgValue > @CPU_threshold)
                BEGIN

                    UPDATE dbo.tbl_AnalysisSummary
                    SET [Status] = 1,
                        Description = 'CPU utilization from one or more SQL Server(s) was at least '
                                      + CONVERT(VARCHAR, ROUND(@t_AvgValue / @cpuCount, 0))
                                      + '% of overall capacity for an extended period of time (3 min)',
                        Report = 'Perfmon CPU'
                    WHERE Name = 'usp_SQLHighCPUconsumption';

                    --if we found one event of extended CPU utilization, break
                    BREAK;
                END;

                FETCH NEXT FROM C_CounterDateTime
                INTO @T_CounterDateTime,
                     @InstanceIndex;
            END;

            CLOSE C_CounterDateTime;
            DEALLOCATE C_CounterDateTime;
        END;

        DROP TABLE #tmpCounterDateTime;
    END;
END;

GO

CREATE PROCEDURE [usp_KernelHighCPUconsumption]
AS
SET NOCOUNT ON;
BEGIN
    IF (
           (OBJECT_ID('CounterData') IS NOT NULL)
           AND (OBJECT_ID('CounterDetails') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        DECLARE @is_Rulehit INT;
        DECLARE @t_CBeginTime DATETIME;
        DECLARE @CPU_threshold DECIMAL(38, 2);
        DECLARE @T_CounterDateTime DATETIME;
        DECLARE @t_AvgValue INT;
        DECLARE @cpuCount INT = 0;
        DECLARE @InstanceIndex INT;
        DECLARE @schedCount INT = 0;

        SET @is_Rulehit = 0;


        --get the CPUs
        --try to find CPU count different possible ways.  if we get zero or null, try another way


        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'tbl_ServerProperties')
        --#1 try 
        BEGIN
            --make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
            DECLARE @cpuRows INT = 0;
            --unless there is more than 1 row
            SELECT DISTINCT
                   @cpuRows = COUNT(PropertyValue)
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName IN ( 'cpu_count' );
            IF (@cpuRows = 1) --then we are OK to use the number
            BEGIN
                SELECT DISTINCT
                       @cpuCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'cpu_count' );
                SELECT DISTINCT
                       @schedCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'number of visible schedulers' );
            END;
        END; -- END #1
        -- #2 try
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            --consider doing this now 
            IF (OBJECT_ID('CounterDetails') IS NOT NULL)
            BEGIN
                IF 0 =
                (
                    SELECT COUNT(*)
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.CounterDetails')
                          AND name = 'procCount_idx'
                )
                BEGIN
                    CREATE INDEX procCount_idx
                    ON dbo.CounterDetails (ObjectName)
                    INCLUDE (
                                CounterName,
                                InstanceName
                            )
                    WHERE ObjectName IN ( 'Processor Information' )
                          AND CounterName IN ( '% User Time' );
                END;
                --as long as table existed, and we either created index or it is already there, try to get CPUs
                SELECT @cpuCount = COUNT(DISTINCT dli.InstanceName)
                FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dli
                        ON dat.CounterID = dli.CounterID
                WHERE dli.ObjectName IN ( 'Processor Information' )
                      AND dli.CounterName IN ( '% User Time' )
                      AND dli.InstanceName NOT LIKE ('%_Total%');
            END;
        END; --END #2
             -- #3 TRY  ******
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            SELECT @cpuCount = CAST(MAX(CounterValue) / 100 AS INT)
            FROM [dbo].[CounterData] cdat
                JOIN [dbo].[CounterDetails] cdet
                    ON cdat.CounterID = cdet.CounterID
            WHERE ObjectName = 'Process'
                  AND CounterName = '% Processor Time'
                  AND InstanceName = '_Total'
                  AND CounterValue != 0;
        END; --END #3

        --did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly

        IF (@cpuCount = 0 OR @cpuCount IS NULL)
            RETURN;

        --set CPU threshold
        SET @CPU_threshold = 30.0 * @cpuCount;

        --create table #tmp (cnt_avg int, b_CounterDateTime datetime, e_CounterDateTime datetime,Outmsg varchar(100)) 
        CREATE TABLE #tmpCounterDateTime
        (
            CounterDateTime DATETIME,
            InstanceIndex INT
        );


        INSERT INTO #tmpCounterDateTime
        (
            CounterDateTime,
            InstanceIndex
        )
        SELECT CONVERT(DATETIME, dat.CounterDateTime),
               ISNULL(dli.InstanceIndex, 0)
        FROM dbo.CounterData dat
            INNER JOIN dbo.CounterDetails dli
                ON dat.CounterID = dli.CounterID
        WHERE dli.ObjectName IN ( 'Process' ) --'physicaldisk','Processor' 
              AND dli.CounterName IN ( '% Privileged Time' )
              AND dli.InstanceName LIKE 'sqlservr%'
              AND dat.CounterValue > @CPU_threshold;

        CREATE CLUSTERED INDEX dt_idx_sqluser
        ON #tmpCounterDateTime (CounterDateTime);

        SELECT @is_Rulehit = COUNT(*),
               @t_CBeginTime = MIN(CounterDateTime)
        FROM #tmpCounterDateTime;

        IF (@is_Rulehit > 0)
        BEGIN
            DECLARE c_counterdatetime CURSOR FOR
            SELECT counterdatetime,
                   InstanceIndex
            FROM #tmpcounterdatetime;

            OPEN c_counterdatetime;

            FETCH NEXT FROM c_counterdatetime
            INTO @T_CounterDateTime,
                 @InstanceIndex;

            WHILE (@@fetch_status = 0)
            BEGIN
                SELECT @t_AvgValue = CAST(AVG(dat.CounterValue) AS DECIMAL(20, 2))
                FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dli
                        ON dat.CounterID = dli.CounterID
                WHERE dli.ObjectName IN ( 'Process' ) --'physicaldisk','Processor' 
                      AND dli.CounterName IN ( '% Privileged Time' )
                      AND dli.InstanceName LIKE 'sqlservr%'
                      AND dat.CounterDateTime
                      BETWEEN (@T_CounterDateTime - '00:01:30') AND (@T_CounterDateTime + '00:01:30')
                      AND ISNULL(dli.InstanceIndex, 0) = @InstanceIndex; -- this would impact perf potentially, but alternatives are ugly

                IF (CAST(@t_AvgValue AS DECIMAL(38, 2)) > @CPU_threshold)
                BEGIN
                    UPDATE dbo.tbl_AnalysisSummary
                    SET [Status] = 1,
                        Description = 'Kernel CPU utilization from SQL Server was at least '
                                      + CONVERT(VARCHAR, ROUND(@t_AvgValue / @cpuCount, 0))
                                      + '% of overall capacity for an extended period of time (3 min.)',
                        Report = 'Perfmon CPU'
                    WHERE Name = 'usp_KernelHighCPUconsumption';

                    --if we found one event of extended CPU utilization, break
                    BREAK;
                END;

                FETCH NEXT FROM c_counterdatetime
                INTO @T_CounterDateTime,
                     @InstanceIndex;
            END;

            CLOSE c_counterdatetime;
            DEALLOCATE c_counterdatetime;

        END;
        DROP TABLE #tmpCounterDateTime;
    END;
END;

GO

CREATE PROCEDURE usp_Non_SQL_CPU_consumption
AS
SET NOCOUNT ON;
BEGIN
    IF (
           (OBJECT_ID('CounterData') IS NOT NULL)
           AND (OBJECT_ID('CounterDetails') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        DECLARE @t_CBeginTime DATETIME;
        DECLARE @is_Rulehit INT;
        DECLARE @cpuCount INT;
        DECLARE @CPU_threshold DECIMAL(20, 2);
        DECLARE @T_CounterDateTime DATETIME;
        DECLARE @t_AvgValue INT;


        SET @is_Rulehit = 0;
        SET @cpuCount = 0;

        CREATE TABLE #tmpCounterDateTime
        (
            CounterDateTime DATETIME,
            NonSQLCpu INT
        );

        --get the CPUs
        --try to find CPU count different possible ways.  if we get zero or null, try another way

        DECLARE @schedCount INT = 0;
        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'tbl_ServerProperties')
        --#1 try 
        BEGIN
            --make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
            DECLARE @cpuRows INT = 0;
            --unless there is more than 1 row
            SELECT DISTINCT
                   @cpuRows = COUNT(PropertyValue)
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName IN ( 'cpu_count' );
            IF (@cpuRows = 1) --then we are OK to use the number
            BEGIN
                SELECT DISTINCT
                       @cpuCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'cpu_count' );
                SELECT DISTINCT
                       @schedCount = PropertyValue
                FROM dbo.tbl_ServerProperties
                WHERE PropertyName IN ( 'number of visible schedulers' );
            END;
        END; -- END #1
        -- #2 try
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            --consider doing this now 
            IF (OBJECT_ID('CounterDetails') IS NOT NULL)
            BEGIN
                IF 0 =
                (
                    SELECT COUNT(*)
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.CounterDetails')
                          AND name = 'procCount_idx'
                )
                BEGIN
                    CREATE INDEX procCount_idx
                    ON dbo.CounterDetails (ObjectName)
                    INCLUDE (
                                CounterName,
                                InstanceName
                            )
                    WHERE ObjectName IN ( 'Processor Information' )
                          AND CounterName IN ( '% User Time' );
                END;
                --as long as table existed, and we either created index or it is already there, try to get CPUs
                SELECT @cpuCount = COUNT(DISTINCT dli.InstanceName)
                FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dli
                        ON dat.CounterID = dli.CounterID
                WHERE dli.ObjectName IN ( 'Processor Information' )
                      AND dli.CounterName IN ( '% User Time' )
                      AND dli.InstanceName NOT LIKE ('%_Total%');
            END;
        END; --END #2
             -- #3 TRY  ******
        IF @cpuCount = 0
           OR @cpuCount IS NULL
        -- still didnt get CPUs, try again
        BEGIN
            SELECT @cpuCount = CAST(MAX(cdat.CounterValue) / 100 AS INT)
            FROM [dbo].[CounterData] cdat
                JOIN [dbo].[CounterDetails] cdet
                    ON cdat.CounterID = cdet.CounterID
            WHERE cdet.ObjectName = 'Process'
                  AND cdet.CounterName = '% Processor Time'
                  AND cdet.InstanceName = '_Total'
                  AND cdat.CounterValue != 0;
        END; --END #3

        --did we get any count > 0...if CPU is zero, need to RETURN or break out of report and not run it as our divisor wont work correctly

        IF (@cpuCount = 0 OR @cpuCount IS NULL)
            RETURN;

        --set threshold at 80% of total CPU capacity
        SET @CPU_threshold = 80 * @cpuCount;

        INSERT INTO #tmpCounterDateTime
        (
            CounterDateTime,
            NonSQLCpu
        )
        SELECT CONVERT(DATETIME, dat.CounterDateTime),
               SUM(dat.CounterValue)
        FROM dbo.CounterData dat
            INNER JOIN dbo.CounterDetails detl
                ON dat.CounterID = detl.CounterID
        WHERE detl.ObjectName IN ( 'Process' )
              AND detl.CounterName IN ( '% User Time' )
              AND
              (
                  detl.InstanceName NOT LIKE '%_Total%'
                  AND detl.InstanceName NOT LIKE 'sqlservr%'
                  AND detl.InstanceName != 'Idle'
              )
        GROUP BY dat.CounterDateTime;


        CREATE CLUSTERED INDEX dt_idx_nonsqlcpu
        ON #tmpCounterDateTime (CounterDateTime);

        SELECT @is_Rulehit = COUNT(*),
               @t_CBeginTime = MIN(CounterDateTime)
        FROM #tmpCounterDateTime;

        IF (@is_Rulehit > 0)
        BEGIN
            DECLARE C_CounterDateTime CURSOR FOR
            SELECT CounterDateTime
            FROM #tmpCounterDateTime
            WHERE NonSQLCpu > @CPU_threshold;

            OPEN C_CounterDateTime;

            FETCH NEXT FROM C_CounterDateTime
            INTO @T_CounterDateTime;

            WHILE (@@fetch_status = 0)
            BEGIN

                --approximate algorithm - if avg CPU usage over a 3 min period exceeds the threshold - this means is prolonged
                --walk the time windows one at a time in a cursor
                SELECT @t_AvgValue = AVG(NonSQLCpu)
                FROM #tmpCounterDateTime
                WHERE CounterDateTime
                BETWEEN (@T_CounterDateTime - '00:01:30') AND (@T_CounterDateTime + '00:01:30');

                IF (@t_AvgValue > @CPU_threshold)
                BEGIN
                    UPDATE dbo.tbl_AnalysisSummary
                    SET [Status] = 1,
                        Description = 'Non-SQL CPU utilization on the system was at least '
                                      + CONVERT(VARCHAR, ROUND(@t_AvgValue / @cpuCount, 0))
                                      + '% of overall CPU capacity for an extended period of time (3 min.)',
                        Report = 'Perfmon CPU'
                    WHERE Name = 'usp_Non_SQL_CPU_consumption';

                    --if we found one event of extended CPU utilization, break
                    BREAK;
                END;

                FETCH NEXT FROM C_CounterDateTime
                INTO @T_CounterDateTime;
            END;

            CLOSE C_CounterDateTime;
            DEALLOCATE C_CounterDateTime;
        END;

        DROP TABLE #tmpCounterDateTime;
    END;
END;

GO


CREATE PROCEDURE XEventcrash
AS
BEGIN
    DECLARE @SQLVersion VARCHAR(100);
    DECLARE @is_Rulehit INT;
    DECLARE @t_MajorVersion INT;
    DECLARE @t_MinorVersion INT;
    --declare the delimeter between each minor version
    DECLARE @Delimeter CHAR(1);
    SET @Delimeter = '.';
    SET @t_MajorVersion = 0;
    SET @t_MinorVersion = 0;
    --Parse the string and insert each  minor version into the @tblSQLVersion  table
    DECLARE @tblSQLVersion TABLE
    (
        id INT IDENTITY,
        tVersion VARCHAR(50)
    );
    DECLARE @tSQLVersion VARCHAR(50);
    DECLARE @StartPos INT,
            @Length INT;
    SET @is_Rulehit = 0;
    DECLARE @i INT;
    SET @i = 0;
    SELECT @i = COUNT(*)
    FROM sys.objects
    WHERE name = 'tbl_SCRIPT_ENVIRONMENT_DETAILS';

    IF @i > 0
    BEGIN
        SET @is_Rulehit = 1;
        SELECT @SQLVersion = Value
        FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
        WHERE Name LIKE '%SQL Version%';
    END;
    IF (@is_Rulehit > 0)
    BEGIN
        WHILE LEN(@SQLVersion) > 0
        BEGIN
            SET @StartPos = CHARINDEX(@Delimeter, @SQLVersion);
            IF @StartPos < 0
                SET @StartPos = 0;
            SET @Length = LEN(@SQLVersion) - @StartPos - 1;
            IF @Length < 0
                SET @Length = 0;
            IF @StartPos > 0
            BEGIN
                SET @tSQLVersion = SUBSTRING(@SQLVersion, 1, @StartPos - 1);
                SET @SQLVersion = SUBSTRING(@SQLVersion, @StartPos + 1, LEN(@SQLVersion) - @StartPos);
            END;
            ELSE
            BEGIN
                SET @tSQLVersion = @SQLVersion;
                SET @SQLVersion = '';
            END;
            INSERT @tblSQLVersion
            (
                tVersion
            )
            VALUES
            (@tSQLVersion);
        END;

        --Get minor version from  @tblSQLVersion  table
        SELECT @t_MajorVersion = CONVERT(INT, tVersion)
        FROM @tblSQLVersion
        WHERE id = 1;
        SELECT @t_MinorVersion = CONVERT(INT, tVersion)
        FROM @tblSQLVersion
        WHERE id = 3;
        IF (@t_MajorVersion = 11 AND @t_MinorVersion < 5556)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'XEventcrash';
        END;
    END;

END;
GO

CREATE PROCEDURE OracleLinkedServerIssue
AS
BEGIN
    IF (
           (OBJECT_ID('tbl_dm_os_loaded_modules') IS NOT NULL)
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        DECLARE @i INT;
        SET @i = 0;

        SELECT @i = COUNT(*)
        FROM [dbo].[tbl_dm_os_loaded_modules]
        WHERE name LIKE '%OraOLEDButl11%'
              OR name LIKE '%OraOLEDBrst11%'
              OR name LIKE '%OraOLEDBrst10%';

        IF (@i > 0)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'OracleLinkedServerIssue';
        END;
    END;
END;
GO

CREATE PROCEDURE [usp_ExcessiveTrace_Warning]
AS
BEGIN
    DECLARE @active_trace_count INT;
    SET @active_trace_count = 0;

    IF OBJECT_ID('tbl_profiler_trace_summary') IS NOT NULL
    BEGIN

        SELECT @active_trace_count = COUNT(DISTINCT traceid)
        FROM [dbo].[tbl_profiler_trace_summary]
        WHERE property = 5
              AND value = 1; --active traces

        IF (@active_trace_count > 4)
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                [Report] = 'Active Traces'
            WHERE Name = 'usp_ExcessiveTrace_Warning';
        END;
    END;
END;
GO

CREATE PROCEDURE [usp_Expensive_TraceEvts_Used]
AS
BEGIN

    IF (OBJECT_ID('tbl_profiler_trace_event_details') IS NOT NULL)
    BEGIN

        DECLARE @traceevent VARCHAR(256),
                @events_string VARCHAR(MAX) = '',
                @cntr INT = 0;

        DECLARE expensive_traceevents CURSOR FOR
        SELECT DISTINCT TOP 5
               trace_event_name
        FROM dbo.tbl_profiler_trace_event_details
        WHERE expensive_event = 1;


        OPEN expensive_traceevents;
        FETCH NEXT FROM expensive_traceevents
        INTO @traceevent;

        WHILE (@@fetch_status = 0)
        BEGIN

            SELECT @events_string = @events_string + @traceevent + ', ';
            SET @cntr = @cntr + 1;

            FETCH NEXT FROM expensive_traceevents
            INTO @traceevent;
        END;

        CLOSE expensive_traceevents;
        DEALLOCATE expensive_traceevents;

        SELECT @events_string = @events_string + '...';

        IF (@cntr > 0 AND @events_string != '')
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                Description = 'Expensive Trace events are active on the system. These can negatively impact performance. Examples include: '
                              + @events_string + '' + CHAR(13) + CHAR(10)
                              + ' Consider disabling these and review the feasibility of using long term. See *_ExistingProfilerXeventTraces.out for details.',
                Report = 'Active Traces'
            WHERE Name = 'usp_Expensive_TraceEvts_Used';

        END;

    END;
END;
GO

CREATE PROCEDURE [usp_Expensive_XEvts_Used]
AS
BEGIN

    IF (OBJECT_ID('tbl_XEvents') IS NOT NULL)
    BEGIN

        DECLARE @session_event VARCHAR(256),
                @events_string VARCHAR(MAX) = '',
                @cntr INT = 0;

        DECLARE expensive_xevents CURSOR FOR
        SELECT DISTINCT TOP 5
               session_name + '::' + event_name
        FROM dbo.tbl_XEvents
        WHERE expensive_event = 1;


        OPEN expensive_xevents;
        FETCH NEXT FROM expensive_xevents
        INTO @session_event;

        WHILE (@@fetch_status = 0)
        BEGIN

            SELECT @events_string = @events_string + @session_event + ', ';
            SET @cntr = @cntr + 1;

            FETCH NEXT FROM expensive_xevents
            INTO @session_event;
        END;

        CLOSE expensive_xevents;
        DEALLOCATE expensive_xevents;

        SELECT @events_string = @events_string + '...';

        IF (@cntr > 0 AND @events_string != '')
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1,
                Description = 'Expensive Xevents are active on the system. These can negatively impact performance. Examples include: '
                              + @events_string + CHAR(13) + CHAR(10)
                              + ' Consider disabling these and review the feasibility of using them long term. See *_ExistingProfilerXeventTraces.out  for details.',
                Report = 'Active Traces'
            WHERE Name = 'usp_Expensive_XEvts_Used';

        END;
    END;

END;
GO

CREATE PROCEDURE usp_HighRecompiles
AS
BEGIN
    -- Created: 5/27/2015
    -- Fires when ...
    -- MAX SQL Re-Compilations/sec > 100
    -- AVG SQL Re-Compilations/sec > 50
    IF (OBJECT_ID('CounterDetails') IS NOT NULL)
       AND (OBJECT_ID('CounterData') IS NOT NULL)
    BEGIN
        IF
        (
            SELECT MAX(CAST(dat.CounterValue AS FLOAT))
            FROM dbo.CounterDetails c
                JOIN dbo.CounterData dat
                    ON c.CounterID = dat.CounterID
            WHERE c.CounterName = 'SQL Re-Compilations/sec'
        ) > 100
        AND
        (
            SELECT AVG(CAST(dat.CounterValue AS FLOAT))
            FROM dbo.CounterDetails c
                JOIN dbo.CounterData dat
                    ON c.CounterID = dat.CounterID
            WHERE c.CounterName = 'SQL Re-Compilations/sec'
        ) > 50
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'usp_HighRecompiles';
        END;
    END;
END;
GO

CREATE PROCEDURE usp_oldce
AS
BEGIN
    DECLARE @SQLVERSION INT;
    DECLARE @oldCE INT;
    DECLARE @database_id INT;

    IF EXISTS
    (
        SELECT 1
        FROM sys.tables
        WHERE name = N'tbl_SCRIPT_ENVIRONMENT_DETAILS'
    )
    BEGIN
        SELECT @SQLVERSION = LEFT(Value, 2)
        FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
        WHERE Name LIKE 'SQL Version%';
    END;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'tbl_SysDatabases')
    BEGIN
        SELECT TOP 1
               @database_id = database_id
        FROM [dbo].[tbl_SysDatabases]
        WHERE compatibility_level < 120;
    END;

    IF (@SQLVERSION >= 12 AND @database_id > 0)
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1,
            [Description] = 'Consider raising compatibility level to take advantage of Optimizer New CE features'
        WHERE Name = 'usp_oldce';
    END;
END;
GO
CREATE PROCEDURE usp_CalvsCore
AS
IF (
       (OBJECT_ID('tbl_ServerProperties') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN
    DECLARE @schedCount INT = 0;
    DECLARE @cpuCount INT = 0;
    --make sure there is only 1 distinct row (this process might get more than 1 server...probably FCI situtation when it does)
    DECLARE @cpuRows INT = 0;
    --unless there is more than 1 row
    SELECT DISTINCT
           @cpuRows = COUNT(PropertyValue)
    FROM dbo.tbl_ServerProperties
    WHERE PropertyName IN ( 'cpu_count' );
    IF (@cpuRows = 1) --then we are OK to use the number
    BEGIN
        SELECT DISTINCT
               @cpuCount = PropertyValue
        FROM dbo.tbl_ServerProperties
        WHERE PropertyName IN ( 'cpu_count' );
        SELECT DISTINCT
               @schedCount = PropertyValue
        FROM dbo.tbl_ServerProperties
        WHERE PropertyName IN ( 'number of visible schedulers' );
    END;

    IF @cpuCount <> 0
       OR @cpuCount IS NOT NULL
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM [dbo].[tbl_ServerProperties]
            WHERE PropertyName = 'Edition'
                  AND PropertyValue NOT LIKE '%Core-based%'
        )
           AND @cpuCount > @schedCount
        BEGIN
            UPDATE [dbo].[tbl_AnalysisSummary]
            SET [Status] = 1,
                [Description] = 'Using CAL license. Also the CPU count (' + CONVERT(VARCHAR, @cpuCount)
                                + ') is greater than online Scheduler count (' + CONVERT(VARCHAR, @schedCount)
                                + '). Check the Errorlog to confirm. The user is not taking full advantage of available hardware and can benefit from upgrading to CORE licensing.',
                Report = 'Server Configuration'
            WHERE [Name] = 'usp_CalvsCore';
        END;
    END;
END;

/********************************************************
Owner: Louis Li
********************************************************/
IF (OBJECT_ID('CounterDetails') IS NOT NULL)
   AND (OBJECT_ID('CounterData') IS NOT NULL)
BEGIN
    SELECT de.ObjectName,
           de.CounterName,
           de.InstanceName,
           CAST(CAST(CounterDateTime AS VARCHAR(19)) AS TIME) AS CounterDateTime,
           d.CounterValue,
           '\' + ObjectName + CASE
                                  WHEN InstanceName IS NULL THEN
                                      ''
                                  ELSE
                                      '(' + InstanceName + ')'
                              END + '\' + CounterName AS FullCounterName,
           '\' + ObjectName + CASE
                                  WHEN InstanceName IS NULL THEN
                                      ''
                                  ELSE
                                      '(*)'
                              END + '\' + CounterName AS FullCounterNameWithWildchar
    INTO dbo.Counters
    FROM dbo.CounterData d
        INNER JOIN dbo.CounterDetails de
            ON d.CounterID = de.CounterID
    ORDER BY de.ObjectName,
             de.CounterName,
             de.InstanceName,
             d.CounterDateTime;
END;
GO


CREATE PROCEDURE usp_Spinlock_HighCPU
AS
IF (
       (OBJECT_ID('tbl_SPINLOCKSTATS') IS NOT NULL)
       AND (OBJECT_ID('tbl_ServerProperties') IS NOT NULL)
   )
BEGIN

    DECLARE @cpu_count INT;
    SELECT @cpu_count = PropertyValue
    FROM dbo.tbl_ServerProperties
    WHERE PropertyName = 'cpu_count';

    WITH spinlocks
    AS (SELECT first.[name] AS spinlock_name,
               (CAST(last.[spins] AS FLOAT) - CAST(first.[spins] AS FLOAT))
               / (CASE
                      WHEN DATEDIFF(ms, first.runtime, last.runtime) = 0 THEN
                          NULL
                      ELSE
                          DATEDIFF(ms, first.runtime, last.runtime)
                  END
                 ) / @cpu_count AS spin_diff_per_ms_per_cpu
        FROM dbo.tbl_SPINLOCKSTATS AS first
            JOIN dbo.tbl_SPINLOCKSTATS AS last
                ON first.[name] = last.[name]
                   AND first.runtime =
                   (
                       SELECT MIN(runtime)FROM dbo.tbl_SPINLOCKSTATS
                   )
                   AND last.runtime =
                   (
                       SELECT MAX(runtime)FROM dbo.tbl_SPINLOCKSTATS
                   )
        WHERE first.[name] NOT LIKE '%dbcc execution%'
              AND last.[name] NOT LIKE '%dbcc execution%')
    SELECT TOP 10
           spinlocks.spinlock_name,
           CAST(spinlocks.spin_diff_per_ms_per_cpu AS DECIMAL(22, 3)) AS spin_diff_per_ms_per_cpu
    INTO #massive_spinlock
    FROM spinlocks
    WHERE spin_diff_per_ms_per_cpu > 10000
    ORDER BY spin_diff_per_ms_per_cpu DESC;

    SELECT *
    FROM #massive_spinlock;



    IF (@@ROWCOUNT > 0)
    BEGIN
        DECLARE @spinlock_name VARCHAR(64),
                @max_spin_diff_per_ms_per_cpu DECIMAL(22, 2),
                @all_spinlock_names VARCHAR(4000) = '';

        --select the highest spins per ms/cpu
        SELECT @max_spin_diff_per_ms_per_cpu = MAX(spin_diff_per_ms_per_cpu)
        FROM #massive_spinlock;

        --use a cursor to get the name of all the high-CPU driving spinlocks
        DECLARE high_spinlock CURSOR FOR
        SELECT spinlock_name
        FROM #massive_spinlock;

        OPEN high_spinlock;

        FETCH NEXT FROM high_spinlock
        INTO @spinlock_name;
        WHILE (@@fetch_status = 0)
        BEGIN
            SELECT @all_spinlock_names = @all_spinlock_names + CHAR(13) + CHAR(10) + @spinlock_name;

            FETCH NEXT FROM high_spinlock
            INTO @spinlock_name;
        END;


        CLOSE high_spinlock;
        DEALLOCATE high_spinlock;

        --update the best practices table 
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1,
            [Description] = 'Found spinlocks that perform massive number of spins during the data collection period, likely causing high CPU. Spinlocks found are: '
                            + @all_spinlock_names + CHAR(13) + CHAR(10) + 'Maximum "spins per ms per CPU" value was '
                            + CONVERT(VARCHAR(32), @max_spin_diff_per_ms_per_cpu)
                            + '. Research these spinlock names to look for ways to reduce contention and spins and the resulting high CPU utilization',
            Report = 'Spinlock Stats'
        WHERE [Name] = OBJECT_NAME(@@PROCID);

    END;
END;
GO


CREATE PROCEDURE usp_NonMS_LoadedModules
AS
IF (
       (OBJECT_ID('tbl_dm_os_loaded_modules') IS NOT NULL)
       AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
   )
BEGIN

    DECLARE @sql_major_version INT,
            @sql_major_build INT,
            @sql NVARCHAR(MAX);
    SELECT @sql_major_version = (CAST(PARSENAME(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR(20)), 4) AS INT)),
           @sql_major_build = (CAST(PARSENAME(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR(20)), 2) AS INT));

    DECLARE @nonMs_module_list VARCHAR(MAX);

    --if SQL 2017+, use the STRING_AGG function
    IF (@sql_major_version >= 14)
    BEGIN
        --filter out known MS modules with no company name; also only get the data from shutdown snapshot
        SELECT @nonMs_module_list = STRING_AGG(CONVERT(NVARCHAR(MAX), name), (CHAR(13) + CHAR(10)))
        FROM dbo.tbl_dm_os_loaded_modules
        WHERE company != 'Microsoft Corporation'
              AND name != 'C:\WINDOWS\SYSTEM32\ODBC32.dll'
              AND name != 'C:\WINDOWS\system32\MsdaDiag.DLL'
              AND name != 'C:\WINDOWS\SYSTEM32\XmlLite.dll'
              AND name != 'C:\WINDOWS\System32\msxml3.dll'
              AND name != 'C:\Program Files\Common Files\System\Ole DB\oledb32.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\MSDART.DLL'
              AND name != 'C:\Program Files\Common Files\System\msadc\msadce.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\ODBC32.dll'
              AND name != 'C:\WINDOWS\system32\MsdaDiag.DLL'
              AND name != 'C:\WINDOWS\SYSTEM32\XmlLite.dll'
              AND name != 'C:\WINDOWS\System32\msxml3.dll'
              AND name != 'C:\Program Files\Common Files\System\Ole DB\oledb32.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\MSDART.DLL'
              AND name != 'C:\Program Files\Common Files\System\msadc\msadce.dll'
              AND name NOT LIKE '%sqlevn70.rll'
              AND input_file_name LIKE '%Shutdown.out';
    END;
    ELSE
    BEGIN
        --filter out known MS modules with no company name; also only get the data from shutdown snapshot
        SELECT @nonMs_module_list = COALESCE(@nonMs_module_list + (CHAR(13) + CHAR(10)) + name, name)
        FROM dbo.tbl_dm_os_loaded_modules
        WHERE company != 'Microsoft Corporation'
              AND name != 'C:\WINDOWS\SYSTEM32\ODBC32.dll'
              AND name != 'C:\WINDOWS\system32\MsdaDiag.DLL'
              AND name != 'C:\WINDOWS\SYSTEM32\XmlLite.dll'
              AND name != 'C:\WINDOWS\System32\msxml3.dll'
              AND name != 'C:\Program Files\Common Files\System\Ole DB\oledb32.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\MSDART.DLL'
              AND name != 'C:\Program Files\Common Files\System\msadc\msadce.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\ODBC32.dll'
              AND name != 'C:\WINDOWS\system32\MsdaDiag.DLL'
              AND name != 'C:\WINDOWS\SYSTEM32\XmlLite.dll'
              AND name != 'C:\WINDOWS\System32\msxml3.dll'
              AND name != 'C:\Program Files\Common Files\System\Ole DB\oledb32.dll'
              AND name != 'C:\WINDOWS\SYSTEM32\MSDART.DLL'
              AND name != 'C:\Program Files\Common Files\System\msadc\msadce.dll'
              AND name NOT LIKE '%sqlevn70.rll'
              AND input_file_name LIKE '%Shutdown.out';

    END;

    IF ((@nonMs_module_list IS NOT NULL) AND (@nonMs_module_list != ''))
    BEGIN
        UPDATE dbo.tbl_AnalysisSummary
        SET [Status] = 1,
            [Description] = 'These Non-MS modules are loaded in SQL Server memory:' + CHAR(13) + CHAR(10)
                            + @nonMs_module_list + +CHAR(13) + CHAR(10)
                            + 'If the issue you are t-shooting is unexplained performance or instability, see if disabling some of these modules will alleviate the issue.',
            [Report] = 'Loaded Modules'
        WHERE [Name] = OBJECT_NAME(@@PROCID);
    END;
END;
GO

CREATE PROCEDURE [dbo].[usp_CCC_Enabled]
AS
BEGIN
    IF (
           OBJECT_ID('tbl_Sys_Configurations') IS NOT NULL
           AND (OBJECT_ID('tbl_AnalysisSummary') IS NOT NULL)
       )
    BEGIN
        IF (
           (
               SELECT [value_in_use]
               FROM [dbo].[tbl_Sys_Configurations]
               WHERE name = 'common criteria compliance enabled'
           ) > 0
           )
        BEGIN
            UPDATE dbo.tbl_AnalysisSummary
            SET [Status] = 1
            WHERE Name = 'usp_CCC_Enabled';
        END;
    END;
END;
GO



/********************************************************
firing rules
********************************************************/

EXEC usp_AttendtionCausedBlocking;
GO
EXEC usp_PerfScriptsRunningLong;
GO
EXEC usp_LongAutoUpdateStats;
GO
EXEC usp_HighStmtCount;
GO
EXEC usp_RG_Idle;
GO
EXEC usp_HighCompile;
GO
EXEC usp_HighCacheCount;
GO
EXEC proc_PowerPlan;
GO
EXEC proc_CheckTraceFlags;
GO
EXEC proc_AutoStats;
GO
EXEC proc_ConfigAlert;
GO
EXEC usp_ExcessiveLockXevent;
GO
EXEC usp_McAFee_Intrusion;
GO
EXEC usp_BatchSort;
GO
EXEC dbo.usp_OptimizerTimeout;
GO
EXEC usp_SmallSampledStats;
GO
EXEC usp_DisabledIndex;
GO
EXEC usp_AccessCheck;
GO
EXEC usp_RedoThreadBlocked;
GO
EXEC usp_VirtualBytesLeak;
GO
EXEC usp_ChangeTableCauseHighCPU;
GO
EXEC dbo.usp_DeadlockTraceFlag;
GO
EXEC usp_Expensive_TraceEvts_Used;
GO
EXEC usp_Expensive_XEvts_Used;
GO
EXEC usp_ExcessiveTrace_Warning;
GO
EXEC OracleLinkedServerIssue;
GO
EXEC XEventcrash;
GO
EXEC usp_Non_SQL_CPU_consumption;
GO
EXEC usp_KernelHighCPUconsumption;
GO
EXEC usp_SQLHighCPUconsumption;
GO
EXEC StaleStatswarning2008;
GO
EXEC dbo.usp_IOAnalysis;
GO
EXEC usp_WarnmissingIndex;
GO
EXEC Optimizer_Memory_Leak;
GO
EXEC dbo.usp_HugeGrant;
GO
EXEC usp_HighRecompiles;
GO
EXEC usp_oldce;
GO
EXEC usp_CalvsCore;
GO
EXEC usp_Spinlock_HighCPU;
GO
EXEC usp_NonMS_LoadedModules;
GO
EXEC usp_DBMEndPointState;
GO
EXEC usp_AGHealthState;
GO
EXEC usp_CCC_Enabled;
/******END of script***/