SET NOCOUNT ON;

-- This script is scheduled only after successful trace imports in the current run.
IF OBJECT_ID('[ReadTrace].[tblBatches]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('ReadTrace.tblBatches', 'StartTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblBatches] ADD StartTime_local DATETIME;
    IF COL_LENGTH('ReadTrace.tblBatches', 'EndTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblBatches] ADD EndTime_local DATETIME;
END;

IF OBJECT_ID('[ReadTrace].[tblStatements]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('ReadTrace.tblStatements', 'StartTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblStatements] ADD StartTime_local DATETIME;
    IF COL_LENGTH('ReadTrace.tblStatements', 'EndTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblStatements] ADD EndTime_local DATETIME;
END;

IF OBJECT_ID('[ReadTrace].[tblConnections]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('ReadTrace.tblConnections', 'StartTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblConnections] ADD StartTime_local DATETIME;
    IF COL_LENGTH('ReadTrace.tblConnections', 'EndTime_local') IS NULL
        ALTER TABLE [ReadTrace].[tblConnections] ADD EndTime_local DATETIME;
END;
GO

IF OBJECT_ID('[ReadTrace].[tblBatches]', 'U') IS NOT NULL
    OR OBJECT_ID('[ReadTrace].[tblStatements]', 'U') IS NOT NULL
    OR OBJECT_ID('[ReadTrace].[tblConnections]', 'U') IS NOT NULL
BEGIN
    DECLARE @timestamps_are_local BIT = 0;

    IF OBJECT_ID('dbo.tbl_ServerProperties', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM dbo.tbl_ServerProperties
            WHERE PropertyName = 'ImportedTraceTimestampsInLocalTime' AND PropertyValue = '1'
        )
            SET @timestamps_are_local = 1;
    END;
    ELSE IF OBJECT_ID('dbo.tbl_server_times', 'U') IS NOT NULL
        AND COL_LENGTH('dbo.tbl_server_times', 'ImportedTraceTimestampsInLocalTime') IS NOT NULL
    BEGIN
        -- Defer compilation until the optional column has been confirmed to exist.
        EXEC sys.sp_executesql
            N'IF EXISTS (SELECT 1 FROM dbo.tbl_server_times WHERE [ImportedTraceTimestampsInLocalTime] = 1)
                  SET @is_local = 1;',
            N'@is_local BIT OUTPUT',
            @is_local = @timestamps_are_local OUTPUT;
    END;

    IF @timestamps_are_local = 0 AND OBJECT_ID('ReadTrace.tblMiscInfo', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM ReadTrace.tblMiscInfo
            WHERE Attribute = 'ImportedTraceTimestampsInLocalTime' AND Value = '1'
        )
            SET @timestamps_are_local = 1;
    END;

    DECLARE @utc_to_local_offset NUMERIC(3, 0) = 0;
    IF @timestamps_are_local = 0
    BEGIN
        IF OBJECT_ID('dbo.tbl_ServerProperties', 'U') IS NOT NULL
        BEGIN
            SELECT @utc_to_local_offset = ISNULL(CONVERT(NUMERIC(3, 0), PropertyValue), 0)
            FROM dbo.tbl_ServerProperties
            WHERE PropertyName = 'UTCOffset_in_Hours';
        END;
        ELSE IF OBJECT_ID('dbo.tbl_server_times', 'U') IS NOT NULL
        BEGIN
            SELECT TOP 1 @utc_to_local_offset = ISNULL(CONVERT(NUMERIC(3, 0), time_delta_hours * -1), 0)
            FROM dbo.tbl_server_times;
        END;
    END;
    SET @utc_to_local_offset = ISNULL(@utc_to_local_offset, 0);

    IF COL_LENGTH('ReadTrace.tblBatches', 'StartTime_local') IS NOT NULL
        AND COL_LENGTH('ReadTrace.tblBatches', 'EndTime_local') IS NOT NULL
    BEGIN
        IF @timestamps_are_local = 1
            UPDATE [ReadTrace].[tblBatches]
            SET StartTime_local = StartTime, EndTime_local = EndTime;
        ELSE
            UPDATE [ReadTrace].[tblBatches]
            SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
                EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;

    IF COL_LENGTH('ReadTrace.tblStatements', 'StartTime_local') IS NOT NULL
        AND COL_LENGTH('ReadTrace.tblStatements', 'EndTime_local') IS NOT NULL
    BEGIN
        IF @timestamps_are_local = 1
            UPDATE [ReadTrace].[tblStatements]
            SET StartTime_local = StartTime, EndTime_local = EndTime;
        ELSE
            UPDATE [ReadTrace].[tblStatements]
            SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
                EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;

    IF COL_LENGTH('ReadTrace.tblConnections', 'StartTime_local') IS NOT NULL
        AND COL_LENGTH('ReadTrace.tblConnections', 'EndTime_local') IS NOT NULL
    BEGIN
        IF @timestamps_are_local = 1
            UPDATE [ReadTrace].[tblConnections]
            SET StartTime_local = StartTime, EndTime_local = EndTime;
        ELSE
            UPDATE [ReadTrace].[tblConnections]
            SET StartTime_local = DATEADD(HOUR, @utc_to_local_offset, StartTime),
                EndTime_local = DATEADD(HOUR, @utc_to_local_offset, EndTime);
    END;
END;
GO
