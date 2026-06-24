-- CreateSchema.sql
-- Creates the ReadTrace schema and all tables.
-- Ported verbatim from SRC/READ80TRACE/res/tsql/*.sql

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ReadTrace')
    EXEC('CREATE SCHEMA ReadTrace')
GO

-- ===========================================
-- Reference / Metadata Tables
-- ===========================================

IF OBJECT_ID('ReadTrace.tblMiscInfo', 'U') IS NOT NULL DROP TABLE ReadTrace.tblMiscInfo
GO
CREATE TABLE ReadTrace.tblMiscInfo
(
    Attribute nvarchar(50) NOT NULL,
    Value nvarchar(2000) NULL,
    iRow int NOT NULL IDENTITY(1,1) PRIMARY KEY CLUSTERED
)
GO

IF OBJECT_ID('ReadTrace.tblTraceFiles', 'U') IS NOT NULL DROP TABLE ReadTrace.tblTraceFiles
GO
CREATE TABLE ReadTrace.tblTraceFiles
(
    FileProcessed int IDENTITY(1, 1) NOT NULL,
    FirstSeqNumber bigint NULL,
    LastSeqNumber bigint NULL,
    FirstEventTime datetime NULL,
    LastEventTime datetime NULL,
    EventsRead bigint NOT NULL,
    TraceFileName nvarchar(512) NOT NULL,
    CONSTRAINT PK_tblTraceFiles PRIMARY KEY (FileProcessed)
)
GO

IF OBJECT_ID('ReadTrace.tblTracedEvents', 'U') IS NOT NULL DROP TABLE ReadTrace.tblTracedEvents
GO
CREATE TABLE ReadTrace.tblTracedEvents
(
    EventID smallint NOT NULL CONSTRAINT PK_TracedEvents PRIMARY KEY
)
GO

IF OBJECT_ID('ReadTrace.trace_events', 'U') IS NOT NULL DROP TABLE ReadTrace.trace_events
GO
CREATE TABLE ReadTrace.trace_events
(
    trace_event_id int NOT NULL PRIMARY KEY CLUSTERED,
    category_id int NOT NULL,
    name nvarchar(128) NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblUniqueAppNames', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniqueAppNames
GO
CREATE TABLE ReadTrace.tblUniqueAppNames
(
    [iID]       [int]   NOT NULL PRIMARY KEY IDENTITY(1,1),
    [AppName]   nvarchar(256) NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblUniqueLoginNames', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniqueLoginNames
GO
CREATE TABLE ReadTrace.tblUniqueLoginNames
(
    [iID]       [int]   NOT NULL PRIMARY KEY IDENTITY(1,1),
    [LoginName] nvarchar(256) NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblProcedureNames', 'U') IS NOT NULL DROP TABLE ReadTrace.tblProcedureNames
GO
CREATE TABLE ReadTrace.tblProcedureNames
(
    DBID int NULL,
    ObjectID int NULL,
    SpecialProcID tinyint NULL,
    [Name] nvarchar(388) NOT NULL
)
GO

-- ===========================================
-- Unique / Normalization Tables
-- ===========================================

IF OBJECT_ID('ReadTrace.tblUniqueBatches', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniqueBatches
GO
CREATE TABLE ReadTrace.tblUniqueBatches
(
    Seq bigint NOT NULL,
    HashID bigint NOT NULL,
    OrigText nvarchar(max) NOT NULL,
    NormText nvarchar(max) NOT NULL,
    SpecialProcID tinyint NULL
)
GO

IF OBJECT_ID('ReadTrace.tblUniqueStatements', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniqueStatements
GO
CREATE TABLE ReadTrace.tblUniqueStatements
(
    Seq bigint NOT NULL,
    HashID bigint NOT NULL,
    OrigText nvarchar(max) NULL,
    NormText nvarchar(max) NULL
)
GO

IF OBJECT_ID('ReadTrace.tblUniquePlans', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniquePlans
GO
CREATE TABLE ReadTrace.tblUniquePlans
(
    Seq bigint NOT NULL,
    PlanHashID bigint NOT NULL,
    DBID int NULL,
    NormPlanText nvarchar(max) NULL
)
GO

IF OBJECT_ID('ReadTrace.tblUniquePlanRows', 'U') IS NOT NULL DROP TABLE ReadTrace.tblUniquePlanRows
GO
CREATE TABLE ReadTrace.tblUniquePlanRows
(
    PlanHashID bigint NOT NULL,
    Rows bigint NULL,
    Executes bigint NULL,
    StmtText nvarchar(max) NOT NULL,
    StmtID int NOT NULL,
    NodeID smallint NOT NULL,
    Parent smallint NULL,
    PhysicalOp varchar(30) NULL,
    LogicalOp varchar(30) NULL,
    Argument nvarchar(256) NULL,
    DefinedValues nvarchar(256) NULL,
    EstimateRows float NULL,
    EstimateIO float NULL,
    EstimateCPU float NULL,
    AvgRowSize int NULL,
    TotalSubtreeCost float NULL,
    OutputList nvarchar(256) NULL,
    Warnings varchar(100) NULL,
    Type varchar(30) NULL,
    Parallel tinyint NULL,
    EstimateExecutions float NULL,
    RowOrder smallint NOT NULL
)
GO

-- ===========================================
-- Fact Tables
-- ===========================================

IF OBJECT_ID('ReadTrace.tblBatches', 'U') IS NOT NULL DROP TABLE ReadTrace.tblBatches
GO
CREATE TABLE ReadTrace.tblBatches
(
    BatchSeq bigint NOT NULL,
    HashID bigint NOT NULL,
    Session int NOT NULL,
    Request int NOT NULL,
    ConnId bigint NOT NULL,
    StartTime datetime NULL,
    EndTime datetime NULL,
    Duration bigint NULL,
    Reads bigint NULL,
    Writes bigint NULL,
    CPU bigint NULL,
    fRPCEvent tinyint NOT NULL,
    DBID int NULL,
    StartSeq bigint NULL,
    EndSeq bigint NULL,
    AttnSeq bigint NULL,
    ConnSeq bigint NULL,
    TextData nvarchar(max) NULL,
    OrigRowCount bigint NULL
)
GO

IF OBJECT_ID('ReadTrace.tblStatements', 'U') IS NOT NULL DROP TABLE ReadTrace.tblStatements
GO
CREATE TABLE ReadTrace.tblStatements
(
    StmtSeq bigint NOT NULL,
    HashID bigint NOT NULL,
    Session int NOT NULL,
    Request int NOT NULL,
    ConnId bigint NOT NULL,
    StartTime datetime NULL,
    EndTime datetime NULL,
    Duration bigint NULL,
    Reads bigint NULL,
    Writes bigint NULL,
    CPU bigint NULL,
    Rows bigint NULL,
    DBID int NULL,
    ObjectID int NULL,
    NestLevel tinyint NULL,
    fDynamicSQL bit NULL,
    StartSeq bigint NULL,
    EndSeq bigint NULL,
    ConnSeq bigint NULL,
    BatchSeq bigint NULL,
    ParentStmtSeq bigint NULL,
    AttnSeq bigint NULL,
    TextData nvarchar(max) NULL
)
GO

IF OBJECT_ID('ReadTrace.tblPlans', 'U') IS NOT NULL DROP TABLE ReadTrace.tblPlans
GO
CREATE TABLE ReadTrace.tblPlans
(
    Seq bigint NOT NULL,
    PlanHashID bigint NOT NULL,
    DBID int NULL,
    BatchSeq bigint NULL,
    StmtSeq bigint NULL,
    Session int NOT NULL,
    Request int NOT NULL,
    ConnId bigint NOT NULL,
    StartTime datetime NOT NULL,
    DOP tinyint NULL
)
GO

IF OBJECT_ID('ReadTrace.tblPlanRows', 'U') IS NOT NULL DROP TABLE ReadTrace.tblPlanRows
GO
CREATE TABLE ReadTrace.tblPlanRows
(
    Seq bigint NOT NULL,
    Rows bigint NULL,
    Executes bigint NULL,
    EstimateRows float NULL,
    EstimateExecutes float NULL,
    RowOrder smallint NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblConnections', 'U') IS NOT NULL DROP TABLE ReadTrace.tblConnections
GO
CREATE TABLE ReadTrace.tblConnections
(
    ConnSeq bigint NOT NULL,
    Session int NOT NULL,
    StartTime datetime NULL,
    EndTime datetime NULL,
    Duration bigint NULL,
    Reads bigint NULL,
    Writes bigint NULL,
    CPU bigint NULL,
    ApplicationName nvarchar(256) NULL,
    LoginName nvarchar(256) NULL,
    HostName nvarchar(256) NULL,
    NTDomainName nvarchar(256) NULL,
    NTUserName nvarchar(256) NULL,
    StartSeq bigint NULL,
    EndSeq bigint NULL,
    TextData nvarchar(max) NULL
)
GO

IF OBJECT_ID('ReadTrace.tblInterestingEvents', 'U') IS NOT NULL DROP TABLE ReadTrace.tblInterestingEvents
GO
CREATE TABLE ReadTrace.tblInterestingEvents
(
    Seq bigint NOT NULL,
    EventID int NOT NULL,
    Session int NOT NULL,
    Request int NOT NULL,
    ConnId bigint NOT NULL,
    StartTime datetime NULL,
    EndTime datetime NULL,
    Duration bigint NULL,
    DBID int NULL,
    IntegerData int NULL,
    EventSubclass int NULL,
    TextData varchar(1000) NULL,
    ObjectID int NULL,
    Error int NULL,
    BatchSeq bigint NULL,
    Severity int NULL,
    State int NULL
)
GO

-- ===========================================
-- Time Intervals and Aggregation Tables
-- ===========================================

IF OBJECT_ID('ReadTrace.tblTimeIntervals', 'U') IS NOT NULL DROP TABLE ReadTrace.tblTimeIntervals
GO
CREATE TABLE ReadTrace.tblTimeIntervals
(
    TimeInterval int NOT NULL IDENTITY(1, 1) PRIMARY KEY NONCLUSTERED,
    StartTime datetime NOT NULL,
    EndTime datetime NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblBatchPartialAggs', 'U') IS NOT NULL DROP TABLE ReadTrace.tblBatchPartialAggs
GO
CREATE TABLE ReadTrace.tblBatchPartialAggs
(
    [HashID] [bigint] NOT NULL,
    [TimeInterval] [int] NOT NULL,
    [StartingEvents] [int] NOT NULL,
    [CompletedEvents] [int] NOT NULL,
    [AttentionEvents] [int] NOT NULL,
    [MinDuration] [bigint] NULL,
    [MaxDuration] [bigint] NULL,
    [TotalDuration] [bigint] NULL,
    [MinReads] [bigint] NULL,
    [MaxReads] [bigint] NULL,
    [TotalReads] [bigint] NULL,
    [MinWrites] [bigint] NULL,
    [MaxWrites] [bigint] NULL,
    [TotalWrites] [bigint] NULL,
    [MinCPU] [bigint] NULL,
    [MaxCPU] [bigint] NULL,
    [TotalCPU] [bigint] NULL,
    [AppNameID] int NOT NULL,
    [LoginNameID] int NOT NULL,
    [DBID] int NOT NULL
)
GO

IF OBJECT_ID('ReadTrace.tblStmtPartialAggs', 'U') IS NOT NULL DROP TABLE ReadTrace.tblStmtPartialAggs
GO
CREATE TABLE ReadTrace.tblStmtPartialAggs
(
    [HashID] [bigint] NOT NULL,
    [TimeInterval] [int] NOT NULL,
    [ObjectID] int NULL,
    [DBID] int NULL,
    [AppNameID] int NOT NULL,
    [LoginNameID] int NOT NULL,
    [StartingEvents] [int] NOT NULL,
    [CompletedEvents] [int] NOT NULL,
    [AttentionEvents] [int] NOT NULL,
    [MinDuration] [bigint] NULL,
    [MaxDuration] [bigint] NULL,
    [TotalDuration] [bigint] NULL,
    [MinReads] [bigint] NULL,
    [MaxReads] [bigint] NULL,
    [TotalReads] [bigint] NULL,
    [MinWrites] [bigint] NULL,
    [MaxWrites] [bigint] NULL,
    [TotalWrites] [bigint] NULL,
    [MinCPU] [bigint] NULL,
    [MaxCPU] [bigint] NULL,
    [TotalCPU] [bigint] NULL
)
GO

IF OBJECT_ID('ReadTrace.tblComparisonBatchPartialAggs', 'U') IS NOT NULL DROP TABLE ReadTrace.tblComparisonBatchPartialAggs
GO
CREATE TABLE ReadTrace.tblComparisonBatchPartialAggs
(
    [b.HashID]              [bigint] NULL,
    [c.HashID]              [bigint] NULL,
    [b.StartingEvents]      [bigint] NOT NULL,
    [b.CompletedEvents]     [bigint] NOT NULL,
    [b.TotalCPU]            [bigint] NOT NULL,
    [b.TotalDuration]       [bigint] NOT NULL,
    [b.TotalReads]          [bigint] NOT NULL,
    [b.TotalWrites]         [bigint] NOT NULL,
    [c.StartingEvents]      [bigint] NOT NULL,
    [c.CompletedEvents]     [bigint] NOT NULL,
    [c.TotalCPU]            [bigint] NOT NULL,
    [c.TotalDuration]       [bigint] NOT NULL,
    [c.TotalReads]          [bigint] NOT NULL,
    [c.TotalWrites]         [bigint] NOT NULL,
    [ProjectedCPUDiff]          numeric(38,4) NOT NULL,
    [ProjectedReadsDiff]        numeric(38,4) NOT NULL,
    [ProjectedWritesDiff]       numeric(38,4) NOT NULL,
    [ProjectedDurationDiff]     numeric(38,4) NOT NULL
)
GO

-- ===========================================
-- Warnings Table
-- ===========================================

IF OBJECT_ID('ReadTrace.tblWarnings', 'U') IS NOT NULL DROP TABLE ReadTrace.tblWarnings
GO
CREATE TABLE ReadTrace.tblWarnings
(
    WarningID int NOT NULL IDENTITY(1, 1) PRIMARY KEY,
    WarningMessage varchar(2000) NOT NULL,
    NumberOfTimes int NULL,
    FirstGlobalSeq bigint NULL,
    fMayAffectCPU bit NOT NULL,
    fMayAffectIO bit NOT NULL,
    fMayAffectDuration bit NOT NULL,
    fAffectsEventAssociation bit NOT NULL
)
GO

-- ===========================================
-- Views
-- ===========================================

IF OBJECT_ID('ReadTrace.vwBatchPartialAggsByGroupTimeInterval', 'V') IS NOT NULL
    DROP VIEW ReadTrace.vwBatchPartialAggsByGroupTimeInterval
GO
CREATE VIEW ReadTrace.vwBatchPartialAggsByGroupTimeInterval
AS
SELECT
    t.StartTime,
    t.EndTime,
    a.TimeInterval,
    SUM(a.StartingEvents) AS StartingEvents,
    SUM(a.CompletedEvents) AS CompletedEvents,
    SUM(a.AttentionEvents) AS Attentions,
    SUM(a.TotalDuration) AS Duration,
    SUM(a.TotalReads) AS Reads,
    SUM(a.TotalWrites) AS Writes,
    SUM(a.TotalCPU) AS CPU
FROM ReadTrace.tblBatchPartialAggs a
INNER JOIN ReadTrace.tblTimeIntervals t ON a.TimeInterval = t.TimeInterval
GROUP BY a.TimeInterval, t.StartTime, t.EndTime
GO
