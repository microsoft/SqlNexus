/*
Run in a SqlNexus database containing ReadTrace.spReporter_Overview_Counts.
The login needs VIEW DEFINITION on that procedure and permission to create/drop databases.
Only the procedure definition is read from the source; no customer rows are copied.
All writes occur in a newly created, uniquely named test database or session temp tables.
*/
SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 51000, 'Run this test outside an existing transaction.', 1;

DECLARE @ProcedureSql NVARCHAR(MAX) =
    OBJECT_DEFINITION(OBJECT_ID(N'ReadTrace.spReporter_Overview_Counts', N'P'));

IF @ProcedureSql IS NULL
    THROW 51000, 'The source procedure is missing or its definition is not visible. Select the correct SqlNexus database.', 1;

SET @ProcedureSql = LTRIM(@ProcedureSql);
IF LEFT(@ProcedureSql, 15) = N'ALTER PROCEDURE'
    SET @ProcedureSql = STUFF(@ProcedureSql, 1, 5, N'CREATE');

DECLARE @TestDatabase SYSNAME = N'SqlNexusOverviewTest_' + REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N'');
DECLARE @DatabaseCreated BIT = 0;
DECLARE @RunInTestDatabase NVARCHAR(MAX) =
    N'EXEC ' + QUOTENAME(@TestDatabase) + N'.sys.sp_executesql @Statement;';
DECLARE @ExecuteProcedure NVARCHAR(MAX) =
    N'EXEC ' + QUOTENAME(@TestDatabase) + N'.ReadTrace.spReporter_Overview_Counts @strColumn = @Metric;';
DECLARE @DropDatabase NVARCHAR(MAX) = N'DROP DATABASE ' + QUOTENAME(@TestDatabase) + N';';
DECLARE @Sql NVARCHAR(MAX);
DECLARE @Passed INT = 0;

BEGIN TRY
    SET @Sql = N'CREATE DATABASE ' + QUOTENAME(@TestDatabase) + N';';
    EXEC sys.sp_executesql @Sql;
    SET @DatabaseCreated = 1;
    PRINT N'Test database: ' + QUOTENAME(@TestDatabase);

    EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)',
        @Statement = N'CREATE SCHEMA ReadTrace;';

    SET @Sql = N'
        CREATE TABLE ReadTrace.tblComparisonBatchPartialAggs
        (
            [b.HashID] BIGINT NULL,
            [c.HashID] BIGINT NULL,
            [b.StartingEvents] BIGINT NOT NULL,
            [b.CompletedEvents] BIGINT NOT NULL,
            [b.TotalCPU] BIGINT NOT NULL,
            [b.TotalDuration] BIGINT NOT NULL,
            [b.TotalReads] BIGINT NOT NULL,
            [b.TotalWrites] BIGINT NOT NULL,
            [c.StartingEvents] BIGINT NOT NULL,
            [c.CompletedEvents] BIGINT NOT NULL,
            [c.TotalCPU] BIGINT NOT NULL,
            [c.TotalDuration] BIGINT NOT NULL,
            [c.TotalReads] BIGINT NOT NULL,
            [c.TotalWrites] BIGINT NOT NULL
        );

        -- Nonzero metrics on the absent side detect incorrect branch filtering.
        INSERT ReadTrace.tblComparisonBatchPartialAggs VALUES
            (1, 1,    1, 2, 3, 4, 5, 6,                   10, 20, 30, 40, 50, 60),
            (2, 2,    2, 4, 6, 8, 10, 12,                 20, 40, 60, 80, 100, 120),
            (3, NULL, 7, 14, 21, 28, 35, 42,              1000, 2000, 3000, 4000, 5000, 6000),
            (NULL, 4, 1000, 2000, 3000, 4000, 5000, 6000, 11, 22, 33, 44, 55, 66);';
    EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)', @Statement = @Sql;
    EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)', @Statement = @ProcedureSql;

    CREATE TABLE #OverviewActual ([Type] NVARCHAR(2), [Value] BIGINT);
    CREATE TABLE #OverviewExpected ([Type] NVARCHAR(2), [Value] BIGINT);

    DECLARE @Metrics TABLE (Ordinal INT PRIMARY KEY, Metric SYSNAME);
    INSERT @Metrics VALUES
        (1, N'StartingEvents'), (2, N'CompletedEvents'), (3, N'TotalCPU'),
        (4, N'TotalDuration'), (5, N'TotalReads'), (6, N'TotalWrites');

    DECLARE @Ordinal INT = 1;
    DECLARE @Metric NVARCHAR(129);
    DECLARE @Failure NVARCHAR(2048);

    WHILE @Ordinal <= 6
    BEGIN
        SELECT @Metric = Metric FROM @Metrics WHERE Ordinal = @Ordinal;
        TRUNCATE TABLE #OverviewActual;
        TRUNCATE TABLE #OverviewExpected;
        INSERT #OverviewExpected VALUES
            (N'B', 3 * @Ordinal), (N'C', 30 * @Ordinal),
            (N'BO', 7 * @Ordinal), (N'CO', 11 * @Ordinal);

        INSERT #OverviewActual
            EXEC sys.sp_executesql @ExecuteProcedure, N'@Metric NVARCHAR(129)', @Metric = @Metric;

        IF (SELECT COUNT(*) FROM #OverviewActual) <> 4
            OR EXISTS (SELECT * FROM #OverviewActual EXCEPT SELECT * FROM #OverviewExpected)
            OR EXISTS (SELECT * FROM #OverviewExpected EXCEPT SELECT * FROM #OverviewActual)
        BEGIN
            SET @Failure = N'Incorrect B/C/BO/CO totals for ' + @Metric + N'.';
            THROW 51000, @Failure, 1;
        END
        SET @Passed += 1;
        SET @Ordinal += 1;
    END
    PRINT N'PASS: exact B/C/BO/CO totals for all six supported metrics.';

    EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)',
        @Statement = N'UPDATE ReadTrace.tblComparisonBatchPartialAggs
                       SET [b.TotalReads] = 3000000000
                       WHERE [b.HashID] IS NOT NULL AND [c.HashID] IS NOT NULL;';
    TRUNCATE TABLE #OverviewActual;
    TRUNCATE TABLE #OverviewExpected;
    INSERT #OverviewExpected VALUES (N'B', 6000000000), (N'C', 150), (N'BO', 35), (N'CO', 55);
    INSERT #OverviewActual
        EXEC sys.sp_executesql @ExecuteProcedure, N'@Metric NVARCHAR(129)', @Metric = N'TotalReads';

    IF (SELECT COUNT(*) FROM #OverviewActual) <> 4
        OR EXISTS (SELECT * FROM #OverviewActual EXCEPT SELECT * FROM #OverviewExpected)
        OR EXISTS (SELECT * FROM #OverviewExpected EXCEPT SELECT * FROM #OverviewActual)
        THROW 51000, 'BIGINT aggregate totals were not preserved.', 1;
    SET @Passed += 1;
    PRINT N'PASS: BIGINT totals exceed the INT limit without overflow.';

    DECLARE @InvalidMetrics TABLE (Id INT IDENTITY PRIMARY KEY, Metric NVARCHAR(129));
    INSERT @InvalidMetrics (Metric) VALUES
        (N'TotalReads]) + 1 + SUM([b.TotalReads'),
        (NULL), (N''), (N' '), (N'UnknownMetric'), (N'HashID'), (N'TotalReads]'),
        (N'TotalReads]); SELECT 1;--'), (N'TotalReads/*comment*/'), (N'TotalReads'''),
        (REPLICATE(N'X', 127)), (REPLICATE(N'X', 128)), (REPLICATE(N'X', 129));

    DECLARE @Phase INT = 1;
    DECLARE @InvalidId INT;

    WHILE @Phase <= 2
    BEGIN
        SET @InvalidId = 1;
        WHILE @InvalidId <= (SELECT COUNT(*) FROM @InvalidMetrics)
        BEGIN
            SELECT @Metric = Metric FROM @InvalidMetrics WHERE Id = @InvalidId;
            TRUNCATE TABLE #OverviewActual;
            BEGIN TRY
                INSERT #OverviewActual
                    EXEC sys.sp_executesql @ExecuteProcedure, N'@Metric NVARCHAR(129)', @Metric = @Metric;
                THROW 51000, 'An invalid metric was accepted instead of raising the validation error.', 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() <> 50000 OR ERROR_SEVERITY() <> 16 OR ERROR_STATE() <> 1
                    OR ERROR_MESSAGE() <> N'Unsupported comparison metric.'
                    THROW;
            END CATCH

            IF EXISTS (SELECT 1 FROM #OverviewActual)
                THROW 51000, 'An invalid metric returned data before raising an error.', 1;
            SET @Passed += 1;
            SET @InvalidId += 1;
        END

        IF @Phase = 1
        BEGIN
            PRINT N'PASS: all 13 invalid inputs rejected with the exact validation error.';
            EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)',
                @Statement = N'DELETE FROM ReadTrace.tblComparisonBatchPartialAggs;';

            SET @Ordinal = 1;
            TRUNCATE TABLE #OverviewExpected;
            INSERT #OverviewExpected VALUES (N'B', NULL), (N'C', NULL), (N'BO', NULL), (N'CO', NULL);
            WHILE @Ordinal <= 6
            BEGIN
                SELECT @Metric = Metric FROM @Metrics WHERE Ordinal = @Ordinal;
                TRUNCATE TABLE #OverviewActual;
                INSERT #OverviewActual
                    EXEC sys.sp_executesql @ExecuteProcedure, N'@Metric NVARCHAR(129)', @Metric = @Metric;

                IF (SELECT COUNT(*) FROM #OverviewActual) <> 4
                    OR EXISTS (SELECT * FROM #OverviewActual EXCEPT SELECT * FROM #OverviewExpected)
                    OR EXISTS (SELECT * FROM #OverviewExpected EXCEPT SELECT * FROM #OverviewActual)
                    THROW 51000, 'An empty table must return four rows with NULL aggregate values.', 1;
                SET @Passed += 1;
                SET @Ordinal += 1;
            END
            PRINT N'PASS: empty-table behavior preserved for all six metrics.';

            -- Missing-table errors would expose dynamic execution before validation.
            EXEC sys.sp_executesql @RunInTestDatabase, N'@Statement NVARCHAR(MAX)',
                @Statement = N'DROP TABLE ReadTrace.tblComparisonBatchPartialAggs;';
        END
        ELSE
            PRINT N'PASS: all 13 invalid inputs rejected before accessing a missing table.';

        SET @Phase += 1;
    END

    IF @Passed <> 39
        THROW 51000, 'The complete set of 39 runtime checks did not execute.', 1;

    DROP TABLE #OverviewActual;
    DROP TABLE #OverviewExpected;
    EXEC sys.sp_executesql @DropDatabase;
    SET @DatabaseCreated = 0;
    PRINT N'PASS: 39 runtime checks completed; disposable test database removed.';
END TRY
BEGIN CATCH
    IF OBJECT_ID(N'tempdb..#OverviewActual') IS NOT NULL DROP TABLE #OverviewActual;
    IF OBJECT_ID(N'tempdb..#OverviewExpected') IS NOT NULL DROP TABLE #OverviewExpected;
    IF @DatabaseCreated = 1
    BEGIN
        BEGIN TRY
            EXEC sys.sp_executesql @DropDatabase;
            PRINT N'Disposable test database removed after failure.';
        END TRY
        BEGIN CATCH
            PRINT N'Cleanup failed for ' + QUOTENAME(@TestDatabase) + N': ' + ERROR_MESSAGE();
        END CATCH
    END;
    THROW;
END CATCH;
