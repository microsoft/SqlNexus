set nocount on 

--add extra columns that represent local server time, computed based on offset if the data is available
--these will facilitation joins between ReadTrace.* tables and other tbl_* tables - the latter storing datetime in local server time 

if (OBJECT_ID('[ReadTrace].[tblBatches]') is not null) 
begin
	--add columns to tblBatches
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblBatches] ADD StartTime_local datetime, EndTime_local datetime;
	end
end

if (OBJECT_ID('[ReadTrace].[tblStatements]') is not null) 
begin
	--add columns to tblStatements
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblStatements] ADD StartTime_local datetime, EndTime_local datetime;
	end
end

if (OBJECT_ID('[ReadTrace].[tblConnections]') is not null) 
begin
	--add columns to tblConnections
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblConnections] ADD StartTime_local datetime, EndTime_local datetime;
	end
end

go
if ( (OBJECT_ID('tbl_ServerProperties') is not null) or (OBJECT_ID('tbl_server_times') is not null)  )
begin

	--get the offset from one of two possible tables
	declare @utc_to_local_offset numeric(3,0) = 0 

	if OBJECT_ID('tbl_ServerProperties') is not null
	begin
		select @utc_to_local_offset = PropertyValue from tbl_ServerProperties
		where PropertyName = 'UTCOffset_in_Hours'
	end
	else if OBJECT_ID('tbl_server_times') is not null
	begin
		select top 1 @utc_to_local_offset = time_delta_hours * -1 from tbl_server_times
	end
	--update the new columns in tblBatches with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin
		update [ReadTrace].[tblBatches]
		set StartTime_local = DATEADD(hour, @utc_to_local_offset, StartTime) ,
			EndTime_local = DATEADD (hour, @utc_to_local_offset, EndTime) ;
	end

	--update the new columns in tblStatements with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin
		update [ReadTrace].[tblStatements]
		set StartTime_local = DATEADD(hour, @utc_to_local_offset, StartTime) ,
			EndTime_local = DATEADD (hour, @utc_to_local_offset, EndTime) ;
	end

	
	--update the new columns in tblConnections with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin

		update [ReadTrace].[tblConnections]
		set StartTime_local = DATEADD(hour, @utc_to_local_offset, StartTime) ,
			EndTime_local = DATEADD (hour, @utc_to_local_offset, EndTime) ;
	end

end
go

--format the tasklist imported table

IF (OBJECT_ID('tbl_ActiveProcesses_OS') IS NOT NULL)
BEGIN
  ALTER TABLE tbl_ActiveProcesses_OS ADD MemUsage_MB DECIMAL (10,3)
END

IF (OBJECT_ID('tbl_ActiveProcesses_OS') IS NOT NULL)
BEGIN
  BEGIN TRY
    UPDATE tbl_ActiveProcesses_OS SET MemUsage_MB = CONVERT(DECIMAL(10,3), CONVERT (decimal(10,3), REPLACE(REPLACE ([Mem Usage], ' K', ''), ',', '' )) / 1024)  
  END TRY

  BEGIN CATCH
   SELECT  ERROR_NUMBER() AS ErrorNumber  
    ,ERROR_SEVERITY() AS ErrorSeverity  
    ,ERROR_STATE() AS ErrorState  
    ,ERROR_LINE() AS ErrorLine  
    ,ERROR_MESSAGE() AS ErrorMessage
  END CATCH
END


--clean up the Systeminfo table after import
IF ( (OBJECT_ID('tbl_SystemInformation') is not null) )
BEGIN
   BEGIN TRY
    UPDATE tbl_SystemInformation SET Property = REPLACE (Property, ':', '')
  END TRY

  BEGIN CATCH
   SELECT  ERROR_NUMBER() AS ErrorNumber  
    ,ERROR_SEVERITY() AS ErrorSeverity  
    ,ERROR_STATE() AS ErrorState  
    ,ERROR_LINE() AS ErrorLine  
    ,ERROR_MESSAGE() AS ErrorMessage
  END CATCH
END