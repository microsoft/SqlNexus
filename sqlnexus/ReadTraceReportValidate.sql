
----------------------------------------------------------------------------------------------
DECLARE @strVersion VARCHAR(10)

SET @strVersion = CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR(10))
IF( (SELECT CAST( SUBSTRING(@strVersion, 0, CHARINDEX('.', @strVersion)) AS INT)) < 9)
BEGIN
	RAISERROR('Reporter requires SQL Server 2005 or later.', 16, 1)
END
GO
----------------------------------------------------------------------------------------------
--	Validate we have some data.  Should be TimeInterval information at least and batch or stmt partial aggs
----------------------------------------------------------------------------------------------
IF (0 = (SELECT COUNT(*) FROM ReadTrace.tblTimeIntervals) OR
    (0 = (SELECT COUNT(*) FROM ReadTrace.tblBatchPartialAggs) AND 0 = (SELECT COUNT(*) FROM ReadTrace.tblStmtPartialAggs)))
BEGIN
	RAISERROR('The ReadTrace database does not appear to contain valid data points.  Recheck the load process and filters.', 16, 2)
END
GO
