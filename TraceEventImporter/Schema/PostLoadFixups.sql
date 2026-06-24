-- PostLoadFixups.sql
-- Run after all data is bulk-loaded to create indexes and reconcile relationships.

-- ===========================================
-- Primary / Clustered Indexes
-- ===========================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblBatches') AND name = 'PK_tblBatches')
    ALTER TABLE ReadTrace.tblBatches ADD CONSTRAINT PK_tblBatches PRIMARY KEY CLUSTERED (BatchSeq)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblStatements') AND name = 'PK_tblStatements')
    ALTER TABLE ReadTrace.tblStatements ADD CONSTRAINT PK_tblStatements PRIMARY KEY CLUSTERED (StmtSeq)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblConnections') AND name = 'PK_tblConnections')
    ALTER TABLE ReadTrace.tblConnections ADD CONSTRAINT PK_tblConnections PRIMARY KEY CLUSTERED (ConnSeq)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblUniqueBatches') AND name = 'PK_tblUniqueBatches')
    ALTER TABLE ReadTrace.tblUniqueBatches ADD CONSTRAINT PK_tblUniqueBatches PRIMARY KEY CLUSTERED (HashID)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblUniqueStatements') AND name = 'PK_tblUniqueStatements')
    ALTER TABLE ReadTrace.tblUniqueStatements ADD CONSTRAINT PK_tblUniqueStatements PRIMARY KEY CLUSTERED (HashID)
GO

-- ===========================================
-- Nonclustered Indexes
-- ===========================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblBatches') AND name = 'tblBatches_HashID')
    CREATE NONCLUSTERED INDEX tblBatches_HashID ON ReadTrace.tblBatches (HashID)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblBatches') AND name = 'tblBatches_Session')
    CREATE NONCLUSTERED INDEX tblBatches_Session ON ReadTrace.tblBatches (Session, Request)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblStatements') AND name = 'tblStatements_HashID')
    CREATE NONCLUSTERED INDEX tblStatements_HashID ON ReadTrace.tblStatements (HashID)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblStatements') AND name = 'tblStatements_BatchSeq')
    CREATE NONCLUSTERED INDEX tblStatements_BatchSeq ON ReadTrace.tblStatements (BatchSeq)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblInterestingEvents') AND name = 'tblInterestingEvents_BatchSeq')
    CREATE NONCLUSTERED INDEX tblInterestingEvents_BatchSeq ON ReadTrace.tblInterestingEvents (BatchSeq)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblBatchPartialAggs') AND name = 'tblBatchPartialAggs_HashID')
    CREATE NONCLUSTERED INDEX tblBatchPartialAggs_HashID ON ReadTrace.tblBatchPartialAggs (HashID, TimeInterval)
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReadTrace.tblStmtPartialAggs') AND name = 'tblStmtPartialAggs_HashID')
    CREATE NONCLUSTERED INDEX tblStmtPartialAggs_HashID ON ReadTrace.tblStmtPartialAggs (HashID, TimeInterval)
GO

-- ===========================================
-- Post-load Fixups: Link ConnSeq in tblBatches
-- ===========================================
UPDATE b
SET b.ConnSeq = c.ConnSeq
FROM ReadTrace.tblBatches b
INNER JOIN ReadTrace.tblConnections c ON b.Session = c.Session
WHERE b.ConnSeq IS NULL
GO

-- ===========================================
-- Post-load Fixups: Link ParentStmtSeq for nested statements
-- ===========================================
;WITH StmtParents AS
(
    SELECT
        s.StmtSeq,
        s.BatchSeq,
        s.NestLevel,
        s.Session,
        s.Request,
        s.StartTime,
        LAG(s.StmtSeq) OVER (PARTITION BY s.Session, s.Request ORDER BY s.StmtSeq) AS PrevStmtSeq,
        LAG(s.NestLevel) OVER (PARTITION BY s.Session, s.Request ORDER BY s.StmtSeq) AS PrevNestLevel
    FROM ReadTrace.tblStatements s
    WHERE s.NestLevel > 1
)
UPDATE s
SET s.ParentStmtSeq = sp.PrevStmtSeq
FROM ReadTrace.tblStatements s
INNER JOIN StmtParents sp ON s.StmtSeq = sp.StmtSeq
WHERE sp.PrevNestLevel IS NOT NULL AND sp.PrevNestLevel < sp.NestLevel
GO
