set nocount on 

--add extra columns that represent local server time, computed based on offset if the data is available
--these will facilitation joins between ReadTrace.* tables and other tbl_* tables - the latter storing datetime in local server time 

if ((OBJECT_ID('[ReadTrace].[tblBatches]') is not null) 
	and (OBJECT_ID('[ReadTrace].[tblStatements]') is not null) 
	and (OBJECT_ID('[ReadTrace].[tblConnections]') is not null) 
	)
begin


	--add columns to tblBatches
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblBatches] ADD StartTime_local datetime, EndTime_local datetime;
	end
	
	--add columns to tblStatements
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblStatements] ADD StartTime_local datetime, EndTime_local datetime;
	end

	--add columns to tblConnections
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId' ) IS NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId' ) IS NULL))
	begin
		ALTER TABLE [ReadTrace].[tblConnections] ADD StartTime_local datetime, EndTime_local datetime;
	end

end
go
if (((OBJECT_ID('tbl_ServerProperties') is not null) or (OBJECT_ID('tbl_server_times') is not null)) 
	and (OBJECT_ID('[ReadTrace].[tblBatches]') is not null) 
	and (OBJECT_ID('[ReadTrace].[tblStatements]') is not null) 
	and (OBJECT_ID('[ReadTrace].[tblConnections]') is not null) 
	)
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
	--update the new columns with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblBatches]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin
		update [ReadTrace].[tblBatches]
		set StartTime_local = DATEADD(hour, @utc_to_local_offset, StartTime) ,
			EndTime_local = DATEADD (hour, @utc_to_local_offset, EndTime) ;
	end

	--update the new columns with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblStatements]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin
		update [ReadTrace].[tblStatements]
		set StartTime_local = DATEADD(hour, -4, StartTime) ,
			EndTime_local = DATEADD (hour, -4, EndTime) ;
	end

	
	--update the new columns with local times
	if ((COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'StartTime_local', 'ColumnId' ) IS NOT NULL)
		and (COLUMNPROPERTY (OBJECT_ID('[ReadTrace].[tblConnections]'), 'EndTime_local', 'ColumnId' ) IS NOT NULL))
	begin

		update [ReadTrace].[tblConnections]
		set StartTime_local = DATEADD(hour, @utc_to_local_offset, StartTime) ,
			EndTime_local = DATEADD (hour, @utc_to_local_offset, EndTime) ;
	end

end