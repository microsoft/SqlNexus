PRINT 'Current database: ' + DB_NAME();
GO
SET NOCOUNT ON;
GO

IF OBJECT_ID('DistinctBatches', 'U') IS NULL
    CREATE TABLE [dbo].[DistinctBatches]
    (
        [Hash] [BIGINT] NULL,
        [OrigText] [NTEXT] NULL,
        [TemplateText] [NTEXT] NULL,
        [ServerName] [NVARCHAR](512) NULL,
        [ApplicationName] [NVARCHAR](512) NULL,
        [DatabaseID] [INT] NULL,
        [DatabaseName] [NVARCHAR](512) NULL,
        [FirstExecSeq] [BIGINT] NULL
    );
GO

IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('DistinctBatches')
          AND name = 'idx1'
)
    CREATE UNIQUE INDEX idx1
    ON dbo.DistinctBatches (
                           TemplateText,
                           Hash
                       )
    WITH IGNORE_DUP_KEY;
GO

IF OBJECT_ID('BatchExecs', 'U') IS NULL
    CREATE TABLE [dbo].[BatchExecs]
    (
        [Hash] [BIGINT] NULL,
        [SPID] [INT] NULL,
        [RequestID] [INT] NULL,
        [StartSeq] [BIGINT] NULL,
        [EndSeq] [BIGINT] NULL,
        [StartTime] [DATETIME] NULL,
        [EndTime] [DATETIME] NULL,
        [CPU] [BIGINT] NULL,
        [Duration] [BIGINT] NULL,
        [Reads] [BIGINT] NULL,
        [Writes] [BIGINT] NULL,
        [RowCounts] [BIGINT] NULL,
        [IsRPC] [INT] NULL,
        [Error] [INT] NULL
    );
GO
IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('BatchExecs')
          AND name = 'idx1'
)
    CREATE NONCLUSTERED INDEX idx1
    ON dbo.BatchExecs (
                      [Hash] ASC,
                      CPU,
                      Reads,
                      Writes,
                      Duration
                  );
GO

IF OBJECT_ID('TraceStats', 'U') IS NULL
    CREATE TABLE dbo.TraceStats
    (
        StartTime DATETIME,
        EndTime DATETIME,
        StartSequence BIGINT,
        EndSequence BIGINT
    );
GO

-- "Register" trace tables for the background purge job
IF '%runmode%' = 'REALTIME'
BEGIN
    IF NOT EXISTS
    (
        SELECT *
        FROM dbo.tbl_NEXUS_PURGE_TABLES
        WHERE tablename = 'BatchExecs'
    )
        INSERT INTO dbo.tbl_NEXUS_PURGE_TABLES
        VALUES
        ('BatchExecs', 'EndTime');
END;
GO

IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('DistinctStatements')
          AND name = 'cidx'
)
    CREATE CLUSTERED INDEX cidx
    ON dbo.DistinctStatements (
                              BatchHash,
                              NestLevel,
                              [Hash],
                              ParentStmtHash
                          );

IF NOT EXISTS
(
    SELECT *
    FROM sysindexes
    WHERE [id] = OBJECT_ID('StatementExecs')
          AND name = 'idx2'
)
    CREATE NONCLUSTERED INDEX idx2
    ON dbo.StatementExecs (
                          BatchHash,
                          [Hash],
                          ParentStmtHash
                      );

-- Drop old tables, if they exist
IF OBJECT_ID('tbl_TRACE_STATISTICS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_TRACE_STATISTICS;
IF OBJECT_ID('tbl_DISTINCT_BATCHES', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_DISTINCT_BATCHES;
IF OBJECT_ID('tbl_BATCH_STATISTICS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_BATCH_STATISTICS;
IF OBJECT_ID('tbl_DISTINCT_STATEMENTS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_DISTINCT_STATEMENTS;
IF OBJECT_ID('tbl_STATEMENT_STATISTICS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_STATEMENT_STATISTICS;
IF OBJECT_ID('tbl_SIGNIFICANT_EVENTS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_SIGNIFICANT_EVENTS;
IF OBJECT_ID('tbl_DISTINCT_PLANS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_DISTINCT_PLANS;
IF OBJECT_ID('tbl_PLAN_EXECS', 'U') IS NOT NULL
    DROP TABLE dbo.tbl_PLAN_EXECS;
GO

-- Create views for backwards compat with reports that rely on the old table names
IF OBJECT_ID('tbl_TRACE_STATISTICS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_TRACE_STATISTICS;
IF OBJECT_ID('tbl_DISTINCT_BATCHES', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_DISTINCT_BATCHES;
IF OBJECT_ID('tbl_BATCH_STATISTICS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_BATCH_STATISTICS;
IF OBJECT_ID('tbl_DISTINCT_STATEMENTS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_DISTINCT_STATEMENTS;
IF OBJECT_ID('tbl_STATEMENT_STATISTICS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_STATEMENT_STATISTICS;
IF OBJECT_ID('tbl_SIGNIFICANT_EVENTS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_SIGNIFICANT_EVENTS;
IF OBJECT_ID('tbl_DISTINCT_PLANS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_DISTINCT_PLANS;
IF OBJECT_ID('tbl_PLAN_EXECS', 'V') IS NOT NULL
    DROP VIEW dbo.tbl_PLAN_EXECS;
GO

CREATE VIEW dbo.tbl_TRACE_STATISTICS
AS
SELECT *
FROM dbo.TraceStats;
GO
CREATE VIEW dbo.tbl_DISTINCT_BATCHES
AS
SELECT *
FROM dbo.DistinctBatches;
GO
CREATE VIEW tbl_BATCH_STATISTICS
AS
SELECT *
FROM dbo.BatchExecs;
GO
CREATE VIEW tbl_DISTINCT_STATEMENTS
AS
SELECT *
FROM dbo.DistinctStatements;
GO
CREATE VIEW tbl_STATEMENT_STATISTICS
AS
SELECT *
FROM dbo.StatementExecs;
GO
CREATE VIEW tbl_SIGNIFICANT_EVENTS
AS
SELECT *
FROM dbo.SignificantEvents;
GO
CREATE VIEW tbl_DISTINCT_PLANS
AS
SELECT *
FROM dbo.DistinctPlans;
GO
CREATE VIEW tbl_PLAN_EXECS
AS
SELECT *
FROM dbo.PlanExecs;
GO
GO


-----------------------------------------------------
IF OBJECT_ID('DataSet_Trace_TopNQueries') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_TopNQueries;
GO
CREATE PROC dbo.DataSet_Trace_TopNQueries
    @StartTime DATETIME = '19000101',
    @EndTime DATETIME = '29990101',
    @ApplicationName NVARCHAR(256) = NULL,
    @DatabaseName NVARCHAR(256) = NULL,
    @DatabaseID VARCHAR(30) = NULL,
    @ServerName NVARCHAR(256) = NULL
AS
--DECLARE @StartTime datetime
--DECLARE @EndTime datetime
DECLARE @IntervalSec INT;
IF @StartTime IS NULL
   OR @StartTime = '19000101'
    SELECT @StartTime = MIN(EndTime)
    FROM BatchExecs (NOLOCK);
IF @EndTime IS NULL
   OR @EndTime = '29990101'
    SELECT @EndTime = MAX(EndTime)
    FROM BatchExecs (NOLOCK);
-- RS will truncate the milliseconds portion of a date, meaning that we will inadvertently filter out the final second of trace data
SET @EndTime = DATEADD(s, 1, @EndTime);
SET @IntervalSec = DATEDIFF(s, @StartTime, @EndTime);

SELECT b.ApplicationName,
       b.DatabaseName,
       REPLACE(REPLACE(SUBSTRING(b.OrigText, 1, 300), CHAR(10), ' '), CHAR(13), ' ')
       + CASE
             WHEN LEN(SUBSTRING(b.OrigText, 1, 400)) >= 300 THEN
                 '...'
             ELSE
                 ''
         END AS Query,
       t.*
FROM
(
    SELECT s.[Hash],
           COUNT(*) AS Executions,
           COUNT_BIG(*) * 60 / @IntervalSec AS avg_exec_per_min,
           SUM(s.CPU) AS total_cpu,
           SUM(s.CPU) / @IntervalSec AS avg_cpu_per_sec,
           MAX(s.CPU) AS max_cpu,
           SUM(s.Reads) AS total_reads,
           SUM(s.Reads) / @IntervalSec AS avg_reads_per_sec,
           MAX(s.Reads) AS max_reads,
           SUM(s.Writes) AS total_writes,
           SUM(s.Writes) / @IntervalSec AS avg_writes_per_sec,
           MAX(s.Writes) AS max_writes,
           SUM(s.Duration / 1000) AS total_duration,
           SUM(s.Duration / 1000) / @IntervalSec AS avg_duration_per_sec,
           MAX(s.Duration / 1000) AS max_duration,
           ROW_NUMBER() OVER (ORDER BY SUM(s.CPU) DESC) AS RN_CPU,
           ROW_NUMBER() OVER (ORDER BY SUM(s.Reads) DESC) AS RN_Reads,
           ROW_NUMBER() OVER (ORDER BY SUM(s.Writes) DESC) AS RN_Writes,
           ROW_NUMBER() OVER (ORDER BY SUM(s.Duration / 1000) DESC) AS RN_Duration
    FROM dbo.BatchExecs (NOLOCK) s
        INNER JOIN dbo.DistinctBatches b
            ON b.[Hash] = s.[Hash]
               AND
               (
                   @ApplicationName = '<All>'
                   OR @ApplicationName = ''
                   OR @ApplicationName IS NULL
                   OR b.ApplicationName = @ApplicationName
               )
               AND
               (
                   @DatabaseName = '<All>'
                   OR @DatabaseName = ''
                   OR @DatabaseName IS NULL
                   OR b.DatabaseName = @DatabaseName
               )
               AND
               (
                   @DatabaseID = '<All>'
                   OR @DatabaseID = ''
                   OR @DatabaseID IS NULL
                   OR CONVERT(VARCHAR(30), b.DatabaseID) = @DatabaseID
               )
               AND
               (
                   @ServerName = '<All>'
                   OR @ServerName = ''
                   OR @ServerName IS NULL
                   OR b.ServerName = @ServerName
               )
    WHERE s.EndTime
    BETWEEN @StartTime AND @EndTime
    GROUP BY s.[Hash]
) t
    INNER JOIN dbo.DistinctBatches (NOLOCK) b
        ON b.[Hash] = t.[Hash]
WHERE RN_CPU <= 20
      OR RN_Reads <= 20
      OR RN_Writes <= 20
      OR RN_Duration <= 20
ORDER BY RN_CPU DESC;
GO


-- Charts perf stats for either a single query or for all queries in the workload (@Hash=null)
IF OBJECT_ID('DataSet_Trace_QueryStatsChart') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_QueryStatsChart;
GO
CREATE PROC dbo.DataSet_Trace_QueryStatsChart
    @StartTime DATETIME = NULL,
    @EndTime DATETIME = NULL,
    @Hash BIGINT = NULL,
    @StmtHash BIGINT = NULL,
    @ApplicationName NVARCHAR(256) = NULL,
    @DatabaseName NVARCHAR(256) = NULL,
    @DatabaseID VARCHAR(30) = NULL,
    @ServerName NVARCHAR(256) = NULL
AS
--DECLARE @StartTime datetime
--DECLARE @EndTime datetime
DECLARE @NumSamplesInChart INT;
DECLARE @SecondsPerSample INT;
IF @StartTime IS NULL
   OR @StartTime = '19000101'
    SELECT @StartTime = MIN(EndTime)
    FROM dbo.BatchExecs (NOLOCK);
IF @EndTime IS NULL
   OR @EndTime = '29990101'
    SELECT @EndTime = MAX(EndTime)
    FROM dbo.BatchExecs (NOLOCK);
SET @NumSamplesInChart = 50;
SET @SecondsPerSample = (DATEDIFF(s, @StartTime, @EndTime) / @NumSamplesInChart) + 1;
IF @SecondsPerSample = 0
    SET @SecondsPerSample = 1;

IF (@StmtHash IS NULL OR @StmtHash = 0)
    SELECT DATEADD(ss, IntervalID * @SecondsPerSample, '20000101') AS interval_start,
           DATEADD(ss, IntervalID * @SecondsPerSample + @SecondsPerSample, '20000101') AS interval_end,
           *
    FROM
    (
        SELECT DATEDIFF(ss, '20000101', b.EndTime) / @SecondsPerSample AS IntervalID,
               CONVERT(DECIMAL(28, 3), COUNT_BIG(*)) / @SecondsPerSample + 0.001 AS Executions_per_sec,
               COUNT(*) AS Executions,
               CONVERT(DECIMAL(28, 3), SUM(b.CPU)) + 0.001 AS total_cpu,
               CONVERT(DECIMAL(28, 3), AVG(b.CPU)) + 0.001 AS avg_cpu,
               CONVERT(DECIMAL(28, 3), MAX(b.CPU)) + 0.001 AS max_cpu,
               CONVERT(DECIMAL(28, 3), SUM(b.CPU) / @SecondsPerSample) + 0.005 AS cpu_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Reads)) + 0.001 AS total_reads,
               CONVERT(DECIMAL(28, 3), AVG(b.Reads)) + 0.001 AS avg_reads,
               CONVERT(DECIMAL(28, 3), MAX(b.Reads)) + 0.001 AS max_reads,
               CONVERT(DECIMAL(28, 3), SUM(b.Reads) / @SecondsPerSample) + 0.005 AS reads_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Writes)) + 0.001 AS total_writes,
               CONVERT(DECIMAL(28, 3), AVG(b.Writes)) + 0.001 AS avg_writes,
               CONVERT(DECIMAL(28, 3), MAX(b.Writes)) + 0.001 AS max_writes,
               CONVERT(DECIMAL(28, 3), SUM(b.Writes) / @SecondsPerSample) + 0.005 AS writes_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Duration / 1000)) + 0.001 AS total_duration,
               CONVERT(DECIMAL(28, 3), AVG(b.Duration / 1000)) + 0.001 AS avg_duration,
               CONVERT(DECIMAL(28, 3), MAX(b.Duration / 1000)) + 0.001 AS max_duration,
               CONVERT(DECIMAL(28, 3), SUM(b.Duration / 1000) / @SecondsPerSample) + 0.005 AS duration_per_sec
        FROM dbo.BatchExecs b
            INNER JOIN dbo.DistinctBatches batch
                ON b.[Hash] = batch.[Hash]
        WHERE (
                  @Hash IS NULL
                  OR @Hash = b.[Hash]
              )
              AND b.EndTime
              BETWEEN @StartTime AND @EndTime
              AND
              (
                  @ApplicationName = '<All>'
                  OR @ApplicationName = ''
                  OR @ApplicationName IS NULL
                  OR batch.ApplicationName = @ApplicationName
              )
              AND
              (
                  @DatabaseName = '<All>'
                  OR @DatabaseName = ''
                  OR @DatabaseName IS NULL
                  OR batch.DatabaseName = @DatabaseName
              )
              AND
              (
                  @DatabaseID = '<All>'
                  OR @DatabaseID = ''
                  OR @DatabaseID IS NULL
                  OR CONVERT(VARCHAR(30), batch.DatabaseID) = @DatabaseID
              )
              AND
              (
                  @ServerName = '<All>'
                  OR @ServerName = ''
                  OR @ServerName IS NULL
                  OR batch.ServerName = @ServerName
              )
        GROUP BY DATEDIFF(ss, '20000101', b.EndTime) / @SecondsPerSample
    ) t
    ORDER BY interval_start;
ELSE
    SELECT DATEADD(ss, IntervalID * @SecondsPerSample, '20000101') AS interval_start,
           DATEADD(ss, IntervalID * @SecondsPerSample + @SecondsPerSample, '20000101') AS interval_end,
           *
    FROM
    (
        SELECT DATEDIFF(ss, '20000101', b.EndTime) / @SecondsPerSample AS IntervalID,
               CONVERT(DECIMAL(28, 3), COUNT_BIG(*)) / @SecondsPerSample + 0.001 AS Executions_per_sec,
               COUNT(*) AS Executions,
               CONVERT(DECIMAL(28, 3), SUM(b.CPU)) + 0.001 AS total_cpu,
               CONVERT(DECIMAL(28, 3), AVG(b.CPU)) + 0.001 AS avg_cpu,
               CONVERT(DECIMAL(28, 3), MAX(b.CPU)) + 0.001 AS max_cpu,
               CONVERT(DECIMAL(28, 3), SUM(b.CPU) / @SecondsPerSample) + 0.005 AS cpu_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Reads)) + 0.001 AS total_reads,
               CONVERT(DECIMAL(28, 3), AVG(b.Reads)) + 0.001 AS avg_reads,
               CONVERT(DECIMAL(28, 3), MAX(b.Reads)) + 0.001 AS max_reads,
               CONVERT(DECIMAL(28, 3), SUM(b.Reads) / @SecondsPerSample) + 0.005 AS reads_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Writes)) + 0.001 AS total_writes,
               CONVERT(DECIMAL(28, 3), AVG(b.Writes)) + 0.001 AS avg_writes,
               CONVERT(DECIMAL(28, 3), MAX(b.Writes)) + 0.001 AS max_writes,
               CONVERT(DECIMAL(28, 3), SUM(b.Writes) / @SecondsPerSample) + 0.005 AS writes_per_sec,
               CONVERT(DECIMAL(28, 3), SUM(b.Duration / 1000)) + 0.001 AS total_duration,
               CONVERT(DECIMAL(28, 3), AVG(b.Duration / 1000)) + 0.001 AS avg_duration,
               CONVERT(DECIMAL(28, 3), MAX(b.Duration / 1000)) + 0.001 AS max_duration,
               CONVERT(DECIMAL(28, 3), SUM(b.Duration / 1000) / @SecondsPerSample) + 0.005 AS duration_per_sec
        FROM dbo.StatementExecs b
            INNER JOIN dbo.DistinctBatches batch
                ON b.[BatchHash] = batch.[Hash]
        WHERE (
                  @Hash IS NULL
                  OR @Hash = b.[BatchHash]
              )
              AND (@StmtHash = b.[Hash])
              AND b.EndTime
              BETWEEN @StartTime AND @EndTime
              AND
              (
                  @ApplicationName = '<All>'
                  OR @ApplicationName = ''
                  OR @ApplicationName IS NULL
                  OR batch.ApplicationName = @ApplicationName
              )
              AND
              (
                  @DatabaseName = '<All>'
                  OR @DatabaseName = ''
                  OR @DatabaseName IS NULL
                  OR batch.DatabaseName = @DatabaseName
              )
              AND
              (
                  @DatabaseID = '<All>'
                  OR @DatabaseID = ''
                  OR @DatabaseID IS NULL
                  OR CONVERT(VARCHAR(30), batch.DatabaseID) = @DatabaseID
              )
              AND
              (
                  @ServerName = '<All>'
                  OR @ServerName = ''
                  OR @ServerName IS NULL
                  OR batch.ServerName = @ServerName
              )
        GROUP BY DATEDIFF(ss, '20000101', b.EndTime) / @SecondsPerSample
    ) t
    ORDER BY interval_start;
GO

IF OBJECT_ID('DataSet_Trace_QueryDetails') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_QueryDetails;
GO
CREATE PROC dbo.DataSet_Trace_QueryDetails @Hash BIGINT
AS
SELECT TOP 1
       [Hash],
       REPLACE(REPLACE(CONVERT(NVARCHAR(4000), OrigText), CHAR(10), ' '), CHAR(13), ' ') AS OrigText,
       REPLACE(REPLACE(CONVERT(NVARCHAR(4000), TemplateText), CHAR(10), ' '), CHAR(13), ' ') AS TemplateText,
       ApplicationName,
       DatabaseID,
       DatabaseName
FROM dbo.DistinctBatches (NOLOCK) b
WHERE [Hash] = @Hash;
GO

IF OBJECT_ID('DataSet_Trace_Duration') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_Duration;
GO
CREATE PROC DataSet_Trace_Duration
AS
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
DECLARE @IntervalSec INT;
SELECT @StartTime = MIN(EndTime)
FROM dbo.BatchExecs (NOLOCK);
SELECT @EndTime = MAX(EndTime)
FROM dbo.BatchExecs (NOLOCK);

IF @StartTime IS NULL
    SET @StartTime = GETDATE();
IF @EndTime IS NULL
    SET @EndTime = GETDATE();
SET @IntervalSec = DATEDIFF(s, @StartTime, @EndTime);

SELECT @StartTime AS StartTime,
       @EndTime AS EndTime,
       @IntervalSec AS TraceDurationSec;
GO


IF OBJECT_ID('DataSet_Trace_ParamStartTime') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_ParamStartTime;
GO
CREATE PROC dbo.DataSet_Trace_ParamStartTime
AS
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
SELECT @StartTime = StartTime
FROM dbo.TraceStats;
SELECT @EndTime = EndTime
FROM dbo.TraceStats;

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
SELECT @StartTime AS StartTime
UNION ALL
SELECT @EndTime AS StartTime;
GO

IF OBJECT_ID('DataSet_Trace_ParamEndTime') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_ParamEndTime;
GO
CREATE PROC dbo.DataSet_Trace_ParamEndTime
AS
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
SELECT @StartTime = StartTime
FROM dbo.TraceStats;
SELECT @EndTime = EndTime
FROM dbo.TraceStats;

IF @StartTime IS NULL
    SET @StartTime = GETDATE();
IF @EndTime IS NULL
    SET @EndTime = GETDATE();

SELECT @EndTime AS EndTime
UNION ALL
SELECT @StartTime AS StartTime
UNION ALL
SELECT @EndTime AS StartTime;
GO

IF OBJECT_ID('DataSet_Trace_Distinct_ApplicationNames') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_Distinct_ApplicationNames;
GO
CREATE PROC dbo.DataSet_Trace_Distinct_ApplicationNames
AS
SELECT '<All>' AS ApplicationName
UNION ALL
SELECT DISTINCT
       ApplicationName
FROM dbo.DistinctBatches;
GO

IF OBJECT_ID('DataSet_Trace_Distinct_DatabaseNames') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_Distinct_DatabaseNames;
GO
CREATE PROC DataSet_Trace_Distinct_DatabaseNames
AS
SELECT '<All>' AS DatabaseName
UNION ALL
SELECT DISTINCT
       DatabaseName
FROM dbo.DistinctBatches;
GO

IF OBJECT_ID('DataSet_Trace_Distinct_DatabaseIDs') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_Distinct_DatabaseIDs;
GO
CREATE PROC DataSet_Trace_Distinct_DatabaseIDs
AS
SELECT '<All>' AS DatabaseID
UNION ALL
SELECT DISTINCT
       CONVERT(VARCHAR(30), DatabaseID)
FROM dbo.DistinctBatches;
GO

IF OBJECT_ID('DataSet_Trace_Distinct_ServerNames') IS NOT NULL
    DROP PROC dbo.DataSet_Trace_Distinct_ServerNames;
GO
CREATE PROC dbo.DataSet_Trace_Distinct_ServerNames
AS
SELECT '<All>' AS ServerName
UNION ALL
SELECT DISTINCT
       ServerName
FROM dbo.DistinctBatches;
GO



IF OBJECT_ID('DataSet_Shared_SQLServerName') IS NOT NULL
    DROP PROC dbo.DataSet_Shared_SQLServerName;
GO
CREATE PROC dbo.DataSet_Shared_SQLServerName
    @script_name VARCHAR(80) = NULL,
    @name VARCHAR(60) = NULL
AS
IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
    SELECT [Value]
    FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
    WHERE script_name = 'SQL 2005 Perf Stats Script'
          AND [Name] = 'SQL Server Name';
ELSE
    SELECT @@SERVERNAME AS [value];
-- SELECT @@SERVERNAME AS [value]
GO


IF OBJECT_ID('DataSet_Shared_SQLVersion') IS NOT NULL
    DROP PROC dbo.DataSet_Shared_SQLVersion;
GO
CREATE PROC dbo.DataSet_Shared_SQLVersion
    @script_name VARCHAR(80) = NULL,
    @name VARCHAR(60) = NULL
AS
IF OBJECT_ID('tbl_SCRIPT_ENVIRONMENT_DETAILS') IS NOT NULL
    SELECT [Value]
    FROM dbo.tbl_SCRIPT_ENVIRONMENT_DETAILS
    WHERE script_name = 'SQL 2005 Perf Stats Script'
          AND [Name] = 'SQL Version (SP)';
ELSE
    SELECT '' AS [value];
-- SELECT CONVERT (varchar, SERVERPROPERTY ('ProductVersion')) + ' (' + CONVERT (varchar, SERVERPROPERTY ('ProductLevel')) + ')' AS [value]
GO

IF NOT EXISTS
(
    SELECT *
    FROM syscolumns
    WHERE [id] = OBJECT_ID('DistinctStatements')
          AND name = 'ObjectTypeDesc'
)
    ALTER TABLE dbo.DistinctStatements
    ADD ObjectTypeDesc AS CASE
                              WHEN ObjectType = 8259 THEN
                                  'Check Constraint'
                              WHEN ObjectType = 8260 THEN
                                  'Default'
                              WHEN ObjectType = 8262 THEN
                                  'Foreign Key'
                              WHEN ObjectType = 8272 THEN
                                  'Stored Proc'
                              WHEN ObjectType = 8274 THEN
                                  'Rule'
                              WHEN ObjectType = 8275 THEN
                                  'System Table'
                              WHEN ObjectType = 8276 THEN
                                  'Server Trigger'
                              WHEN ObjectType = 8277 THEN
                                  'User Table'
                              WHEN ObjectType = 8278 THEN
                                  'View'
                              WHEN ObjectType = 8280 THEN
                                  'XProc'
                              WHEN ObjectType = 16724 THEN
                                  'CLR Trigger'
                              WHEN ObjectType = 16964 THEN
                                  'Database'
                              WHEN ObjectType = 16975 THEN
                                  'Object'
                              WHEN ObjectType = 17222 THEN
                                  'FullText Catalog'
                              WHEN ObjectType = 17232 THEN
                                  'CLR Stored Proc'
                              WHEN ObjectType = 17235 THEN
                                  'Schema'
                              WHEN ObjectType = 17475 THEN
                                  'Credential'
                              WHEN ObjectType = 17491 THEN
                                  'DDL Event'
                              WHEN ObjectType = 17741 THEN
                                  'Management Event'
                              WHEN ObjectType = 17747 THEN
                                  'Security Event'
                              WHEN ObjectType = 17749 THEN
                                  'User Event'
                              WHEN ObjectType = 17985 THEN
                                  'CLR Agg Function'
                              WHEN ObjectType = 17993 THEN
                                  'Inline TVFunction'
                              WHEN ObjectType = 18000 THEN
                                  'Partition Function'
                              WHEN ObjectType = 18002 THEN
                                  'Repl Filter Proc'
                              WHEN ObjectType = 18004 THEN
                                  'TVFunction'
                              WHEN ObjectType = 18259 THEN
                                  'Server Role'
                              WHEN ObjectType = 18263 THEN
                                  'Windows Group'
                              WHEN ObjectType = 19265 THEN
                                  'Asymmetric Key'
                              WHEN ObjectType = 19277 THEN
                                  'Master Key'
                              WHEN ObjectType = 19280 THEN
                                  'Primary Key'
                              WHEN ObjectType = 19283 THEN
                                  'ObfusKey'
                              WHEN ObjectType = 19521 THEN
                                  'Asymmetric Key Login'
                              WHEN ObjectType = 19523 THEN
                                  'Certificate Login'
                              WHEN ObjectType = 19538 THEN
                                  'Role'
                              WHEN ObjectType = 19539 THEN
                                  'SQL Login'
                              WHEN ObjectType = 19543 THEN
                                  'Windows Login'
                              WHEN ObjectType = 20034 THEN
                                  'Remote Service Binding'
                              WHEN ObjectType = 20036 THEN
                                  'Event Notification on DB'
                              WHEN ObjectType = 20037 THEN
                                  'Event Notification'
                              WHEN ObjectType = 20038 THEN
                                  'Scalar Function'
                              WHEN ObjectType = 20047 THEN
                                  'Obj Event Notification'
                              WHEN ObjectType = 20051 THEN
                                  'Synonym'
                              WHEN ObjectType = 20549 THEN
                                  'End Point'
                              WHEN ObjectType = 20801 THEN
                                  'Adhoc Query'
                              WHEN ObjectType = 20816 THEN
                                  'Adhoc Query'
                              WHEN ObjectType = 20819 THEN
                                  'Service Broker Service Queue'
                              WHEN ObjectType = 20821 THEN
                                  'Unique Constraint'
                              WHEN ObjectType = 21057 THEN
                                  'App Role'
                              WHEN ObjectType = 21059 THEN
                                  'Certificate'
                              WHEN ObjectType = 21075 THEN
                                  'Server'
                              WHEN ObjectType = 21076 THEN
                                  'TSQL Trigger'
                              WHEN ObjectType = 21313 THEN
                                  'Assembly'
                              WHEN ObjectType = 21318 THEN
                                  'CLR Scalar Function'
                              WHEN ObjectType = 21321 THEN
                                  'Inline Scalar Function'
                              WHEN ObjectType = 21328 THEN
                                  'Partition Scheme'
                              WHEN ObjectType = 21333 THEN
                                  'User'
                              WHEN ObjectType = 21571 THEN
                                  'Service Broker Service Contract'
                              WHEN ObjectType = 21572 THEN
                                  'Databas Trigger'
                              WHEN ObjectType = 21574 THEN
                                  'CLR TVFunction'
                              WHEN ObjectType = 21577 THEN
                                  'Internal Table (e.g. XML Node Table, Queue Table)'
                              WHEN ObjectType = 21581 THEN
                                  'Service Broker Message Type'
                              WHEN ObjectType = 21586 THEN
                                  'Service Broker Route'
                              WHEN ObjectType = 21587 THEN
                                  'Statistics'
                              WHEN ObjectType = 21825 THEN
                                  'User'
                              WHEN ObjectType = 21827 THEN
                                  'User'
                              WHEN ObjectType = 21831 THEN
                                  'User'
                              WHEN ObjectType = 21843 THEN
                                  'User'
                              WHEN ObjectType = 21847 THEN
                                  'User'
                              WHEN ObjectType = 22099 THEN
                                  'Service Broker Service'
                              WHEN ObjectType = 22601 THEN
                                  'Index'
                              WHEN ObjectType = 22604 THEN
                                  'Certificate Login'
                              WHEN ObjectType = 22611 THEN
                                  'XMLSchema'
                              WHEN ObjectType = 22868 THEN
                                  'Type'
                              ELSE
                                  CONVERT(VARCHAR(30), 'Unknown (' + CONVERT(VARCHAR, ObjectType)) + ')'
                          END;
GO
EXEC sp_refreshview tbl_DISTINCT_STATEMENTS;
GO

