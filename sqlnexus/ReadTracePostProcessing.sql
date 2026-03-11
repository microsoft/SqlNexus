--this is a rewrite of the script keeping the original logic but using UPPER CASE for T-SQL Syntax and improved readability.
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_TraceFiles'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_TraceFiles
GO

CREATE PROCEDURE ReadTrace.spReporter_TraceFiles
AS
BEGIN
	SET NOCOUNT ON
	SELECT FileProcessed, FirstSeqNumber, LastSeqNumber, ISNULL(CONVERT(NVARCHAR, FirstEventTime, 121), 'Unknown') AS [FirstEventTime], ISNULL(CONVERT(NVARCHAR, LastEventTime, 121), 'Unknown') AS [LastEventTime], EventsRead, TraceFileName 
	FROM ReadTrace.tblTraceFiles 
	ORDER BY FileProcessed asc	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_CurrentDB'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_CurrentDB
GO

CREATE PROCEDURE ReadTrace.spReporter_CurrentDB
AS
BEGIN

	SET NOCOUNT ON
	--waitfor delay '23:00:00'		--		Easy way to test query timeout
	SELECT DB_NAME() AS [Database]
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_MiscInfo'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_MiscInfo
GO

CREATE PROCEDURE ReadTrace.spReporter_MiscInfo
AS
BEGIN

	SELECT Attribute, Value FROM ReadTrace.tblMiscInfo
	UNION ALL
	SELECT 'Active SQL Version', @@VERSION
	UNION ALL
	SELECT 'Current Date', CAST(GETDATE() AS NVARCHAR)
	UNION ALL
	SELECT 'Database', DB_NAME()
	UNION ALL
	SELECT 'Database Sort Order', CAST(DATABASEPROPERTYEX(DB_NAME(), 'SQLSortOrder') AS NVARCHAR)
	UNION ALL
	SELECT 'Timing Base',
		CASE 
			WHEN Value < 9 THEN N'Milliseconds (ms)'
			ELSE N'Microseconds (' + NCHAR(181) + N's)'
		END 
	FROM ReadTrace.tblMiscInfo
	WHERE Attribute = 'EventVersion'
	ORDER BY Attribute
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_TimeIntervals'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_TimeIntervals
GO

--	Cast as proper format for parameter display 
CREATE PROCEDURE ReadTrace.spReporter_TimeIntervals
AS
BEGIN	
	-- Relies on NULLs sorting first so that Auto Select is the first row in the result set (and thus
	-- picked as the default value for the parameter in the reports)	
	SELECT      CONVERT(INT, NULL) AS TimeInterval, CONVERT(VARCHAR, '<Auto Select>') AS StartTime, CONVERT(VARCHAR, '<Auto Select>') AS EndTime
	UNION ALL	  	
	SELECT							  TimeInterval,CONVERT(VARCHAR, StartTime, 121) as [StartTime],	CONVERT(VARCHAR, EndTime, 121) as [EndTime]	FROM ReadTrace.tblTimeIntervals	ORDER BY TimeInterval
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_TracedEvents'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_TracedEvents
GO

CREATE PROCEDURE ReadTrace.spReporter_TracedEvents
AS
BEGIN
	SET NOCOUNT ON
	--	SQL Azure does not have trace events
	IF OBJECT_ID('sys.trace_events','V') IS NOT NULL
	BEGIN
		SELECT e.EventID, se.name 
		FROM ReadTrace.tblTracedEvents e
		INNER JOIN sys.trace_events se ON se.trace_event_id = e.EventID
	END
	ELSE
	BEGIN
		CREATE TABLE #tmp 	(trace_event_id	INT	NOT NULL PRIMARY KEY, 	name NVARCHAR(128))

		INSERT INTO #tmp (trace_event_id, name) 
		VALUES --select '(' + CAST(trace_event_id AS VARCHAR) +  ', ''' + name + '''), ' from sys.trace_events
		(10, 'RPC:Completed'), (11, 'RPC:Starting'),(12, 'SQL:BatchCompleted'),(13, 'SQL:BatchStarting'),(14, 'Audit Login'),(15, 'Audit Logout'),(16, 'Attention'),(17, 'ExistingConnection'), (18, 'Audit Server Starts And Stops'), (19, 'DTCTransaction'), 
		(20, 'Audit Login Failed'), (21, 'EventLog'), (22, 'ErrorLog'), (23, 'Lock:Released'), (24, 'Lock:Acquired'), (25, 'Lock:Deadlock'), (26, 'Lock:Cancel'), (27, 'Lock:Timeout'), (28, 'Degree of Parallelism'), 
		(33, 'Exception'), (34, 'SP:CacheMiss'), (35, 'SP:CacheInsert'), (36, 'SP:CacheRemove'), (37, 'SP:Recompile'), (38, 'SP:CacheHit'), 
		(40, 'SQL:StmtStarting'), (41, 'SQL:StmtCompleted'), (42, 'SP:Starting'), (43, 'SP:Completed'), (44, 'SP:StmtStarting'), (45, 'SP:StmtCompleted'), (46, 'Object:Created'), (47, 'Object:Deleted'), 
		(50, 'SQLTransaction'), (51, 'Scan:Started'), (52, 'Scan:Stopped'), (53, 'CursorOpen'), (54, 'TransactionLog'), (55, 'Hash Warning'), (58, 'Auto Stats'), (59, 'Lock:Deadlock Chain'), 
		(60, 'Lock:Escalation'), (61, 'OLEDB Errors'), (67, 'Execution Warnings'), (68, 'Showplan Text (Unencoded)'), (69, 'Sort Warnings'), 
		(70, 'CursorPrepare'), (71, 'Prepare SQL'), (72, 'Exec Prepared SQL'), (73, 'Unprepare SQL'), (74, 'CursorExecute'), (75, 'CursorRecompile'), (76, 'CursorImplicitConversion'), (77, 'CursorUnprepare'), (78, 'CursorClose'), (79, 'Missing Column Statistics'), 
		(80, 'Missing Join Predicate'), (81, 'Server Memory Change'), (82, 'UserConfigurable:0'), (83, 'UserConfigurable:1'), (84, 'UserConfigurable:2'), (85, 'UserConfigurable:3'), (86, 'UserConfigurable:4'), (87, 'UserConfigurable:5'), (88, 'UserConfigurable:6'), (89, 'UserConfigurable:7'), 
		(90, 'UserConfigurable:8'), (91, 'UserConfigurable:9'), (92, 'Data File Auto Grow'), (93, 'Log File Auto Grow'), (94, 'Data File Auto Shrink'), (95, 'Log File Auto Shrink'), (96, 'Showplan Text'), (97, 'Showplan All'), (98, 'Showplan Statistics Profile'), 
		(100, 'RPC Output Parameter'), (102, 'Audit Database Scope GDR Event'), (103, 'Audit Schema Object GDR Event'), (104, 'Audit Addlogin Event'), (105, 'Audit Login GDR Event'), (106, 'Audit Login Change Property Event'), (107, 'Audit Login Change Password Event'), (108, 'Audit Add Login to Server Role Event'), (109, 'Audit Add DB User Event'), 
		(110, 'Audit Add Member to DB Role Event'), (111, 'Audit Add Role Event'), (112, 'Audit App Role Change Password Event'), (113, 'Audit Statement Permission Event'), (114, 'Audit Schema Object Access Event'), (115, 'Audit Backup/Restore Event'), (116, 'Audit DBCC Event'), (117, 'Audit Change Audit Event'), (118, 'Audit Object Derived Permission Event'), (119, 'OLEDB Call Event'), 
		(120, 'OLEDB QueryInterface Event'), (121, 'OLEDB DataRead Event'), (122, 'Showplan XML'), (123, 'SQL:FullTextQuery'), (124, 'Broker:Conversation'), (125, 'Deprecation Announcement'), (126, 'Deprecation Final Support'), (127, 'Exchange Spill Event'), (128, 'Audit Database Management Event'), (129, 'Audit Database Object Management Event'), 
		(130, 'Audit Database Principal Management Event'), (131, 'Audit Schema Object Management Event'), (132, 'Audit Server Principal Impersonation Event'), (133, 'Audit Database Principal Impersonation Event'), (134, 'Audit Server Object Take Ownership Event'), (135, 'Audit Database Object Take Ownership Event'), (136, 'Broker:Conversation Group'), (137, 'Blocked process report'), (138, 'Broker:Connection'), (139, 'Broker:Forwarded Message Sent'), 
		(140, 'Broker:Forwarded Message Dropped'), (141, 'Broker:Message Classify'), (142, 'Broker:Transmission'), (143, 'Broker:Queue Disabled'), (144, 'Broker:Mirrored Route State Changed'), (146, 'Showplan XML Statistics Profile'), (148, 'Deadlock graph'), (149, 'Broker:Remote Message Acknowledgement'), 
		(150, 'Trace File Close'), (151, 'Database Mirroring Connection'), (152, 'Audit Change Database Owner'), (153, 'Audit Schema Object Take Ownership Event'), (154, 'Audit Database Mirroring Login'), (155, 'FT:Crawl Started'), (156, 'FT:Crawl Stopped'), (157, 'FT:Crawl Aborted'), (158, 'Audit Broker Conversation'), (159, 'Audit Broker Login'), 
		(160, 'Broker:Message Undeliverable'), (161, 'Broker:Corrupted Message'), (162, 'User Error Message'), (163, 'Broker:Activation'), (164, 'Object:Altered'), (165, 'Performance statistics'), (166, 'SQL:StmtRecompile'), (167, 'Database Mirroring State Change'), (168, 'Showplan XML For Query Compile'), (169, 'Showplan All For Query Compile'), 
		(170, 'Audit Server Scope GDR Event'), (171, 'Audit Server Object GDR Event'), (172, 'Audit Database Object GDR Event'), (173, 'Audit Server Operation Event'), (175, 'Audit Server Alter Trace Event'), (176, 'Audit Server Object Management Event'), (177, 'Audit Server Principal Management Event'), (178, 'Audit Database Operation Event'), 
		(180, 'Audit Database Object Access Event'), (181, 'TM: Begin Tran starting'), (182, 'TM: Begin Tran completed'), (183, 'TM: Promote Tran starting'), (184, 'TM: Promote Tran completed'), (185, 'TM: Commit Tran starting'), (186, 'TM: Commit Tran completed'), (187, 'TM: Rollback Tran starting'), (188, 'TM: Rollback Tran completed'), (189, 'Lock:Timeout (timeout > 0)'), 
		(190, 'Progress Report: Online Index Operation'), (191, 'TM: Save Tran starting'), (192, 'TM: Save Tran completed'), (193, 'Background Job Error'), (194, 'OLEDB Provider Information'), (195, 'Mount Tape'), (196, 'Assembly Load'), (198, 'XQuery Static Type'), (199, 'QN: Subscription'), 
		(200, 'QN: Parameter table'), (201, 'QN: Template'), (202, 'QN: Dynamics'), (212, 'Bitmap Warning'), (213, 'Database Suspect Data Page'), (214, 'CPU threshold exceeded'), (215, 'PreConnect:Starting'), (216, 'PreConnect:Completed'), (217, 'Plan Guide Successful'), (218, 'Plan Guide Unsuccessful'), 
		(235, 'Audit Fulltext');

		SELECT e.EventID, se.name 
		      FROM ReadTrace.tblTracedEvents e
		INNER JOIN #tmp                     se ON se.trace_event_id = e.EventID
	END
END
GO

/*
16          Attention
33          Exception
37          SP:Recompile
55          Hash Warning
58          Auto Stats
60          Lock:Escalation
67          Execution Warnings
69          Sort Warnings
80          Missing Join Predicate

SELECT * FROM ReadTrace.tblInterestingEvents e
SELECT * FROM ReadTrace.tblTimeIntervals

EXEC ReadTrace.spReporter_InterestingEvents
*/
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_InterestingEventsGrouped'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_InterestingEventsGrouped
GO

CREATE PROCEDURE ReadTrace.spReporter_InterestingEventsGrouped
	@StartTimeInterval INT = NULL,
	@EndTimeInterval INT = NULL,
	@EventID INT = NULL
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @StartTime DATETIME, @EndTime DATETIME

	IF(@StartTimeInterval IS NULL)
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF(@EndTimeInterval IS NULL)
		SELECT @EndTimeInterval   = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals

	SELECT @StartTime = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval
	SELECT @EndTime   = EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	--
	--	For any interesting events that we observed somewhere in the time window of interest, build the complete 
	--	list of all time intervals in the window, event id, event name combinations.  In the following query which
	--	does a LEFT JOIN using this set we'll return each time interval with a count of events (zero if NULL).  If we don't
	--	return the time interval or return a value of NULL the report control will continue charting the same
	--	value up until the next data point (rather than charting a count of zero).  For example, if you had 1 attention
	-- 	in TimeInterval 1 and 1 attention in TimeInterval 3, the chart would show 1 across interval 2 as well which
	--	is very misleading
	--
	CREATE TABLE #TimeAndEvents	(		TimeInterval			BIGINT,		IntervalStartTime		DATETIME,		IntervalEndTime			DATETIME,		trace_event_id			INT,		name					NVARCHAR(128)	)
	
	INSERT INTO #TimeAndEvents
	SELECT i.TimeInterval, 
		   i.StartTime AS IntervalStartTime, 
		   i.EndTime AS IntervalEndTime, 
		   e.trace_event_id,
		   e.name 
	      FROM ReadTrace.tblTimeIntervals i 
	CROSS JOIN (
				SELECT DISTINCT EventID 
				FROM ReadTrace.tblInterestingEvents 
				WHERE EventID = ISNULL(@EventID, EventID) AND COALESCE(EndTime, StartTime) BETWEEN @StartTime AND @EndTime
			   ) AS x
		  JOIN ReadTrace.trace_events e ON x.EventID = e.trace_event_id
	WHERE i.TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval
	OPTION (RECOMPILE)

	SELECT 
		z.TimeInterval,
		z.IntervalStartTime,
		z.IntervalEndTime,
		z.trace_event_id AS EventID,
		z.name,
		ISNULL(Count, 0) AS [Count]
	     FROM #TimeAndEvents AS z
	LEFT JOIN (
			   SELECT TimeInterval,
					  EventID,
					  COUNT(*) AS [Count]
				FROM (
					  SELECT e.EventID,
						-- project the time interval for the event
						CASE WHEN e.EndTime IS NOT NULL THEN (SELECT TOP 1 TimeInterval FROM ReadTrace.tblTimeIntervals ti WHERE e.EndTime <= ti.EndTime ORDER BY ti.EndTime ASC)
							                            ELSE (SELECT TOP 1 TimeInterval FROM ReadTrace.tblTimeIntervals ti WHERE e.StartTime >= ti.StartTime ORDER BY ti.StartTime DESC)
						END AS TimeInterval
					  FROM ReadTrace.tblInterestingEvents e
					  WHERE EventID = ISNULL(@EventID, EventID) and COALESCE(EndTime, StartTime) between @StartTime and @EndTime
					 ) AS x
				GROUP BY TimeInterval, EventID) AS g ON z.TimeInterval = g.TimeInterval and z.trace_event_id = g.EventID
	ORDER BY EventID, TimeInterval
	OPTION (RECOMPILE)
END
GO


IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_InterestingEventDetails'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_InterestingEventDetails
GO

CREATE PROCEDURE ReadTrace.spReporter_InterestingEventDetails
	@EventID INT,
	@StartTimeInterval INT = NULL,
	@EndTimeInterval INT = NULL
AS
BEGIN
	SET NOCOUNT ON
	IF(@StartTimeInterval IS NULL)
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF(@EndTimeInterval IS NULL)
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals

	DECLARE @dtStart DATETIME, @dtEnd DATETIME
	SELECT @dtStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval
	SELECT @dtEnd   = EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	--	SQL Trace is not part of SQL Azure
	IF OBJECT_ID('sys.trace_subclass_values','V') IS NOT NULL
	BEGIN
			SELECT TOP 5000
				e.Seq,
				e.ConnId,
				e.Session,
				e.Request,
				e.StartTime,
				e.EndTime,
				e.Duration,
				e.DBID,
				e.IntegerData,
				e.EventSubclass,
				sv.subclass_name as SubclassDescription,
				e.TextData,
				e.ObjectID,
				e.Error
			     FROM ReadTrace.tblInterestingEvents e
			LEFT JOIN sys.trace_subclass_values     sv ON e.EventID = sv.trace_event_id	AND e.EventSubclass = sv.subclass_value	AND sv.trace_column_id = 21		-- event subclass
			WHERE e.EventID = @EventID 	AND ISNULL(e.EndTime, e.StartTime) BETWEEN @dtStart AND @dtEnd
			ORDER BY e.StartTime
	END
	ELSE
	BEGIN
			CREATE TABLE #tmp (	trace_event_id	INT	NOT NULL,subclass_value  INT NOT NULL,subclass_name  NVARCHAR(128)	)

			INSERT INTO #tmp (trace_event_id, subclass_value, subclass_name) 
			VALUES --select '(' + CAST(trace_event_id AS VARCHAR) + ', ' + CAST(subclass_value AS VARCHAR) + ', ''' + subclass_name + '''), ' from sys.trace_subclass_values where trace_column_id = 21
				(46, 0, 'Begin'),(164, 0, 'Begin'),(47, 0, 'Begin'), 
				(46, 1, 'Commit'),(164, 1, 'Commit'),(47, 1, 'Commit'), 
				(46, 2, 'Rollback'),(164, 2, 'Rollback'),(47, 2, 'Rollback'), 
				(59, 101, 'Resource type Lock'),(59, 102, 'Resource type Exchange'),(55, 0, 'Recursion'), 
				(55, 1, 'Bailout'),(212, 0, 'Disabled'),(81, 1, 'Increase'),(81, 2, 'Decrease'), 
				(119, 0, 'Starting'),(119, 1, 'Completed'),(120, 0, 'Starting'),(120, 1, 'Completed'),(121, 0, 'Starting'),(121, 1, 'Completed'), 
				(67, 1, 'Query wait'),(67, 2, 'Query timeout'), 
				(28, 1, 'Select'),(28, 2, 'Insert'),(28, 3, 'Update'),(28, 4, 'Delete'),(28, 5, 'Merge'), 
				(69, 1, 'Single pass'),(69, 2, 'Multiple pass'), 
				(115, 1, 'Backup'),(115, 2, 'Restore'),(115, 3, 'BackupLog'), 
				(118, 1, 'Create'),(176, 1, 'Create'),(128, 1, 'Create'),(129, 1, 'Create'),(131, 1, 'Create'),(177, 1, 'Create'),(130, 1, 'Create'), 
				(118, 2, 'Alter'),(176, 2, 'Alter'),(128, 2, 'Alter'),(129, 2, 'Alter'),(131, 2, 'Alter'),(177, 2, 'Alter'),(130, 2, 'Alter'), 
				(118, 3, 'Drop'),(176, 3, 'Drop'),(128, 3, 'Drop'),(129, 3, 'Drop'),(131, 3, 'Drop'),(177, 3, 'Drop'),(130, 3, 'Drop'), 
				(118, 4, 'Backup'),(176, 4, 'Backup'),(128, 4, 'Backup'),(129, 4, 'Backup'),(131, 4, 'Backup'),(177, 4, 'Backup'),(130, 4, 'Backup'),(177, 5, 'Disable'), 
				(177, 6, 'Enable'),(176, 7, 'Credential mapped to login'),(131, 8, 'Transfer'),(176, 9, 'Credential Map Dropped'),(129, 10, 'Open'), 
				(118, 11, 'Restore'),(176, 11, 'Restore'),(128, 11, 'Restore'),(129, 11, 'Restore'),(131, 11, 'Restore'),(177, 11, 'Restore'),(130, 11, 'Restore'), 
				(129, 12, 'Access'),(130, 13, 'Change User Login - Update One'),(130, 14, 'Change User Login - Auto Fix'),(176, 15, 'Shutdown on Audit Failure'), 
				(111, 1, 'Add'),(109, 1, 'Add'),(104, 1, 'Add'),(108, 1, 'Add'),(110, 1, 'Add'),(111, 2, 'Drop'),(109, 2, 'Drop'),(104, 2, 'Drop'),(108, 2, 'Drop'),(110, 2, 'Drop'), 
				(110, 3, 'Change group'),(109, 3, 'Grant database access'),(109, 4, 'Revoke database access'), 
				(18, 1, 'Shutdown'),(18, 2, 'Started'),(18, 3, 'Paused'),(18, 4, 'Continue'), 
				(103, 1, 'Grant'),(102, 1, 'Grant'),(105, 1, 'Grant'),(170, 1, 'Grant'),(171, 1, 'Grant'),(172, 1, 'Grant'), 
				(103, 2, 'Revoke'),(102, 2, 'Revoke'),(105, 2, 'Revoke'),(170, 2, 'Revoke'),(171, 2, 'Revoke'),(172, 2, 'Revoke'), 
				(103, 3, 'Deny'),(102, 3, 'Deny'),(105, 3, 'Deny'),(170, 3, 'Deny'),(171, 3, 'Deny'),(172, 3, 'Deny'), 
				(158, 1, 'No Security Header'),(158, 2, 'No Certificate'),(158, 3, 'Invalid Signature'),(158, 4, 'Run As Target Failure'),(158, 5, 'Bad Data'),(159, 1, 'Login Success'), 
				(159, 2, 'Login Protocol Error'),(159, 3, 'Message Format Error'),(159, 4, 'Negotiate Failure'),(159, 5, 'Authentication Failure'),(159, 6, 'Authorization Failure'),(154, 1, 'Login Success'), 
				(154, 2, 'Login Protocol Error'),(154, 3, 'Message Format Error'),(154, 4, 'Negotiate Failure'),(154, 5, 'Authentication Failure'),(154, 6, 'Authorization Failure'),(142, 1, 'Transmission Exception'), 
				(117, 1, 'Audit started'),(117, 2, 'Audit stopped'),(117, 3, 'C2 mode ON'),(117, 4, 'C2 mode OFF'), 
				(19, 3, 'Close connection'),(19, 23, 'Unknown'),(19, 0, 'Get address'), 
				(19, 1, 'Propagate Transaction'),(19, 14, 'Preparing Transaction'),(19, 15, 'Transaction is prepared'),(19, 16, 'Transaction is aborting'),(19, 17, 'Transaction is committing'),(19, 22, 'TM failed while in prepared state'), 
				(19, 9, 'Internal commit'),(19, 10, 'Internal abort'), 
				(19, 6, 'Creating a new DTC transaction'),(19, 7, 'Enlisting in a DTC transaction'), 
				(50, 0, 'Begin'),(50, 1, 'Commit'),(50, 2, 'Rollback'),(50, 3, 'Savepoint'), 
				(37, 1, 'Schema changed'),(37, 2, 'Statistics changed'),(37, 3, 'Deferred compile'),(37, 4, 'Set option change'),(37, 5, 'Temp table changed'),(37, 6, 'Remote rowset changed'),
				(37, 7, 'For browse permissions changed'),(37, 8, 'Query notification environment changed'),(37, 9, 'PartitionView changed'),(37, 10, 'Cursor options changed'),(37, 11, 'OPTION (RECOMPILE) requested'),
				(37, 12, 'Parameterized plan flushed'),(37, 13, 'Test plan linearization'),(37, 14, 'Plan affecting database version changed'),(166, 1, 'Schema changed'),(166, 2, 'Statistics changed'), 
				(166, 3, 'Deferred compile'),(166, 4, 'Set option change'),(166, 5, 'Temp table changed'),(166, 6, 'Remote rowset changed'),(166, 7, 'For browse permissions changed'),(166, 8, 'Query notification environment changed'),(166, 9, 'PartitionView changed'),(166, 10, 'Cursor options changed'), 
				(166, 11, 'OPTION (RECOMPILE) requested'),(166, 12, 'Parameterized plan flushed'),(166, 13, 'Test plan linearization'),(166, 14, 'Plan affecting database version changed'),(106, 1, 'Default database changed'),(106, 2, 'Default language changed'), 
				(106, 3, 'Name changed'),(106, 5, 'Policy changed'),(106, 6, 'Expiration changed'),(106, 4, 'Credential changed'),(107, 1, 'Password self changed'),(107, 2, 'Password changed'), 
				(107, 3, 'Password self reset'),(107, 4, 'Password reset'),(107, 5, 'Password unlocked'),(107, 6, 'Password must change'), 
				(124, 1, 'SEND Message'),(124, 2, 'END CONVERSATION'),(124, 3, 'END CONVERSATION WITH ERROR'),(124, 4, 'Broker Initiated Error'),(124, 5, 'Terminate Dialog'), 
				(124, 6, 'Received Sequenced Message'),(124, 7, 'Received END CONVERSATION'),(124, 8, 'Received END CONVERSATION WITH ERROR'),(124, 9, 'Received Broker Error Message'),(124, 10, 'Received END CONVERSATION Ack'), 
				(124, 11, 'BEGIN DIALOG'),(124, 12, 'Dialog Created'),(124, 13, 'END CONVERSATION WITH CLEANUP'),(136, 1, 'Create'),(136, 2, 'Drop'),(149, 1, 'Message with Acknowledgement Sent'),(149, 2, 'Acknowledgement Sent'),(149, 3, 'Message with Acknowledgement Received'), 
				(149, 4, 'Acknowledgement Received'),(160, 1, 'Sequenced Message'),(160, 2, 'Unsequenced Message'),(163, 1, 'Started'),(163, 2, 'Ended'),(163, 3, 'Aborted'),(163, 4, 'Notified'), 
				(163, 5, 'Task Output'),(163, 6, 'Failed to start'),(138, 1, 'Connecting'),(138, 2, 'Connected'),(138, 3, 'Connect Failed'),(138, 4, 'Closing'),(138, 5, 'Closed'),(138, 6, 'Accept'),(138, 7, 'Send IO Error'),(138, 8, 'Receive IO Error'), 
				(144, 1, 'Operational'),(144, 2, 'Operational with principal only'),(144, 3, 'Not operational'),(151, 1, 'Connecting'),(151, 2, 'Connected'),(151, 3, 'Connect Failed'), 
				(151, 4, 'Closing'),(151, 5, 'Closed'),(151, 6, 'Accept'),(151, 7, 'Send IO Error'),(151, 8, 'Receive IO Error'), 
				(127, 1, 'Spill begin'),(127, 2, 'Spill end'),(185, 1, 'Commit'), 
				(186, 1, 'Commit'),(185, 2, 'Commit and Begin'),(186, 2, 'Commit and Begin'),(187, 1, 'Rollback'),(188, 1, 'Rollback'),(187, 2, 'Rollback and Begin'),(188, 2, 'Rollback and Begin'),(190, 1, 'Start'), 
				(190, 2, 'Stage 1 execution begin'),(190, 3, 'Stage 1 execution end'),(190, 4, 'Stage 2 execution begin'),(190, 5, 'Stage 2 execution end'), 
				(190, 6, 'Inserted row count'),(190, 7, 'Done'),(141, 1, 'Local'),(141, 2, 'Remote'),(141, 3, 'Delayed'),(195, 1, 'Tape mount request'),(195, 2, 'Tape mount complete'),(195, 3, 'Tape mount cancelled'), 
				(36, 1, 'Compplan Remove'),(36, 2, 'Proc Cache Flush'),(165, 0, 'SQL'),(165, 1, 'SP:Plan'),(165, 2, 'Batch:Plan'),(165, 3, 'QueryStats'),(165, 4, 'ProcedureStats'),(165, 5, 'TriggerStats'),(173, 175, 'Alter Server State'), 
				(178, 1, 'Checkpoint'),(178, 2, 'Subscribe to Query Notification'),(178, 3, 'Authenticate'),(178, 4, 'Showplan'),(178, 5, 'Connect'),(178, 6, 'View Database State'),(173, 1, 'Administer Bulk Operations'), 
				(173, 2, 'Alter Settings'),(173, 3, 'Alter Resources'),(173, 4, 'Authenticate'),(173, 5, 'External Access Assembly'),(173, 7, 'Unsafe Assembly'),(173, 8, 'Alter Connection'),(173, 9, 'Alter Resource Governor'), 
				(173, 10, 'Use Any Workload Group'),(173, 11, 'View Server State'),(199, 1, 'Subscription registered'),(199, 2, 'Subscription rewound'),(199, 3, 'Subscription fired'), 
				(199, 4, 'Firing failed with broker error'),(199, 5, 'Firing failed without broker error'),(199, 6, 'Broker error intercepted'),(199, 7, 'Subscription deletion attempt'),(199, 8, 'Subscription deletion failed'),(199, 9, 'Subscription destroyed'), 
				(200, 1, 'Table created'),(200, 2, 'Table drop attempt'),(200, 3, 'Table drop attempt failed'),(200, 4, 'Table dropped'),(200, 5, 'Table pinned'),(200, 6, 'Table unpinned'), 
				(200, 7, 'Number of users incremented'),(200, 8, 'Number of users decremented'), 
				(200, 9, 'LRU counter reset'),(200, 10, 'Cleanup task started'),(200, 11, 'Cleanup task finished'), 
				(201, 1, 'Template created'),(201, 2, 'Template matched'),(201, 3, 'Template dropped'), 
				(202, 1, 'Clock run started'),(202, 2, 'Clock run finished'),(202, 3, 'Master cleanup task started'),(202, 4, 'Master cleanup task finished'),(202, 5, 'Master cleanup task skipped'), 
				(215, 1, 'RG Classifier UDF'),(216, 1, 'RG Classifier UDF'),(215, 2, 'Logon Trigger'),(216, 2, 'Logon Trigger'), 
				(14, 1, 'Nonpooled'),(14, 2, 'Pooled'),(15, 1, 'Nonpooled'),(15, 2, 'Pooled'),(20, 1, 'Nonpooled'),(20, 2, 'Pooled'),(60, 0, 'LOCK_THRESHOLD'),(60, 1, 'MEMORY_THRESHOLD'), 
				(235, 1, 'Fulltext Filter Daemon Connect Success'),(235, 2, 'Fulltext Filter Daemon Connect Error'),(235, 3, 'Fulltext Launcher Connect Success'),(235, 4, 'Fulltext Launcher Connect Error'),(235, 5, 'Fulltext Inbound Shared Memory Corrupt'),(235, 6, 'Fulltext Inbound Pipe Message Corrupt');
	
			SELECT TOP 5000
			e.Seq,
			e.ConnId,
			e.Session,
			e.Request,
			e.StartTime,
			e.EndTime,
			e.Duration,
			e.DBID,
			e.IntegerData,
			e.EventSubclass,
			sv.subclass_name as SubclassDescription,
			e.TextData,
			e.ObjectID,
			e.Error
		     FROM ReadTrace.tblInterestingEvents e
        LEFT JOIN #tmp                          sv ON e.EventID = sv.trace_event_id	AND e.EventSubclass = sv.subclass_value	--and sv.trace_column_id = 21		-- event subclass
		WHERE e.EventID = @EventID AND ISNULL(e.EndTime, e.StartTime) BETWEEN @dtStart AND @dtEnd
		ORDER BY e.StartTime
	END	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchAggregatesTimeIntervalGrouping'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchAggregatesTimeIntervalGrouping
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchAggregatesTimeIntervalGrouping
	@StartTimeInterval INT = NULL,
	@EndTimeInterval   INT = NULL
AS
BEGIN
	SET NOCOUNT ON
	IF(@StartTimeInterval IS NULL)
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF(@EndTimeInterval IS NULL)
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals

	SELECT  * 
	FROM ReadTrace.vwBatchPartialAggsByGroupTimeInterval a
	WHERE a.TimeInterval BETWEEN @StartTimeInterval and @EndTimeInterval
	ORDER BY a.TimeInterval ASC
	OPTION (RECOMPILE)

END
GO


----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.fn_ReporterCalculateScaleFactor'), 'IsScalarFunction') = 1
	DROP FUNCTION ReadTrace.fn_ReporterCalculateScaleFactor
GO

/*
 *	Function: fn_ReporterCalculateScaleFactor
 *
 *	This function calculates a scale factor that the given input value must be multiplied by so that it is in the range of 0 to 100.
 *	This resultant value is used as a multiplier for each of the various series that will be charted in Reporter to ensure that they
 *	all plot within the same range.  Otherwise a value that is unusually large will skew the chart range so that reasonable/small 
 *	values can't be visually differentiated.  Currently, all values are scaled via a factor of 10, similar to Performance Monitor.  It
 *	is assumed that the input value will always be greater than or equal to zero (so that we don't have to multiply by values > 1)
 *
 *	When converting a float value to varchar using format specification 1 (below) the output is always formatted as 8 digits with 
 *	an exponent (i.e., 1.2345678e+nnn).  Use substring to extract the exponent.  Because we want all values to be scaled between 0 and 100, 
 *	subtract 2 from the exponent and build a fraction (0.nnnn1) that can be used to multiply so that the value will be in this range
 */
CREATE FUNCTION ReadTrace.fn_ReporterCalculateScaleFactor ( @Input FLOAT )
RETURNS NUMERIC(38, 20)
AS
BEGIN
	DECLARE @ScaleFactor NUMERIC(38, 20)
	SELECT @ScaleFactor = 
		CASE 
			WHEN @Input < 1     THEN 100
			WHEN @Input < 10    THEN 10
			WHEN @Input < 100   THEN 1
			WHEN @Input IS NULL THEN 1
			ELSE CONVERT(NUMERIC(38, 20), '0.' + REPLICATE('0', CONVERT(INT, SUBSTRING(CONVERT(VARCHAR(60), @Input, 1), 11, 10)) - 2) + '1')
		END
	RETURN @ScaleFactor
END
GO


----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchAggScaleFactor'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchAggScaleFactor
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchAggScaleFactor
	@StartTimeInterval INT = NULL,
	@EndTimeInterval   INT = NULL
AS
BEGIN
	--	In order to keep a single chart with reads, writes, CPU and such
	--	we have to adjust the values (as if scaled in perfmon) to be like the
	--	max setting so they can all live together with some sort of graph
	--	definition
	SET NOCOUNT ON
	DECLARE @MaxEventCount int

	IF(@StartTimeInterval IS NULL)
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF(@EndTimeInterval IS NULL)
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals

	CREATE TABLE #BatchAggs (StartTime DATETIME, EndTime DATETIME, TimeInterval INT, StartingEvents INT, CompletedEvents INT, Attentions INT, Duration BIGINT, Reads BIGINT, Writes BIGINT, CPU BIGINT)

	-- Insert the aggregated batch information for the specified time window into a local temp table
	INSERT INTO #BatchAggs EXEC ReadTrace.spReporter_BatchAggregatesTimeIntervalGrouping @StartTimeInterval, @EndTimeInterval
	
	-- I want to make sure that I always chart starting & completed events on the same scale, so that if
	-- their is some divergence in the number (due to longer running queries, blocking, etc) that the two
	-- lines diverge and make this very obvious.  Therefore I get the max of either of these two and use it
	-- as input for scaling in the final query below
	SELECT @MaxEventCount = MAX(NumberOfEvents) 
	FROM (
	      SELECT MAX(StartingEvents) AS NumberOfEvents  FROM #BatchAggs
		  UNION ALL
		  SELECT MAX(CompletedEvents) AS NumberOfEvents FROM #BatchAggs
		 ) AS t

	SELECT
		CASE WHEN @MaxEventCount  <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS StartingEventsScale,
		CASE WHEN @MaxEventCount  <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS CompletedEventsScale,
		CASE WHEN MAX(Attentions) <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(MAX(Attentions)) END AS AttentionEventsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.Duration)) AS DurationScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.Reads))    AS ReadsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.Writes))   AS WritesScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.CPU))      AS CPUScale
	FROM #BatchAggs AS a	
END
GO
---------------------------------------------------------------------------------------------- 
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_DetermineFilterValues'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_DetermineFilterValues
GO

--	It is possible that the user from a report can set the filter multiple
--	times.  INSERT INTO table and SELECT TOP 1 row back to get the actual values
CREATE PROCEDURE ReadTrace.spReporter_DetermineFilterValues
	@StartTimeInterval	INT OUTPUT,
	@EndTimeInterval	INT OUTPUT,
	@iDBID				INT OUTPUT,
	@iAppNameID			INT OUTPUT,
	@iLoginNameID		INT OUTPUT,
	@Filter1			NVARCHAR(256),
	@Filter2			NVARCHAR(256),
	@Filter3			NVARCHAR(256),
	@Filter4			NVARCHAR(256),
	@Filter1Name		NVARCHAR(64),
	@Filter2Name		NVARCHAR(64),
	@Filter3Name		NVARCHAR(64),
	@Filter4Name		NVARCHAR(64)
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @iTimeInterval 	INT

	SET @iTimeInterval = NULL

	CREATE TABLE #tblFilter (strFilterName		NVARCHAR(64)  COLLATE database_default NULL, strFilterValue		NVARCHAR(256) COLLATE database_default NULL	)

	INSERT INTO #tblFilter 
	VALUES
		(@Filter1Name, @Filter1),
		(@Filter2Name, @Filter2),
		(@Filter3Name, @Filter3),
		(@Filter4Name, @Filter4)
	
	----------------------------------------------------------------------------------------------
	--	Keep in sync with spReporter_PartialAggs_GroupBy
	----------------------------------------------------------------------------------------------
	SELECT TOP 1 @iTimeInterval = CAST(strFilterValue AS INT) 
	FROM #tblFilter f 	
	WHERE strFilterName = N'EndTime'

	SELECT TOP 1 @iDBID = CAST(strFilterValue AS INT) 
	FROM #tblFilter f	
	WHERE strFilterName = N'DBID'

	SELECT TOP 1 @iAppNameID = n.iID 
	      FROM #tblFilter f 
	INNER JOIN ReadTrace.tblUniqueAppNames n ON n.AppName = f.strFilterValue
	WHERE strFilterName = N'AppName'

	SELECT TOP 1 @iLoginNameID = n.iID 
	      FROM #tblFilter f 
	INNER JOIN ReadTrace.tblUniqueLoginNames n ON n.LoginName = f.strFilterValue
	WHERE strFilterName = N'LoginName'

	--	Has user set any of the time interval range to override the direct EndTime filter
	IF(	    @StartTimeInterval IS NULL 		AND @EndTimeInterval IS NULL		AND @iTimeInterval IS NOT NULL)
	BEGIN
		SELECT @StartTimeInterval = @iTimeInterval
		SELECT @EndTimeInterval = @iTimeInterval
	END
	ELSE
	BEGIN
		IF(@StartTimeInterval IS NULL)
			SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals
	
		IF(@EndTimeInterval IS NULL)
			SELECT @EndTimeInterval   = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
END
GO

--------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_GetFilterAsString'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_GetFilterAsString
GO

CREATE PROCEDURE ReadTrace.spReporter_GetFilterAsString
		@Filter1	    NVARCHAR(256) = NULL,
		@Filter2		NVARCHAR(256) = NULL,
		@Filter3		NVARCHAR(256) = NULL,
		@Filter4		NVARCHAR(256) = NULL,
		@Filter1Name	NVARCHAR(64) = NULL,
		@Filter2Name	NVARCHAR(64) = NULL,
		@Filter3Name	NVARCHAR(64) = NULL,
		@Filter4Name	NVARCHAR(64) = NULL
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @strFilterString		NVARCHAR(1024)	
	SELECT @strFilterString = ''

	IF('AppName' = @Filter1Name or 'LoginName' = @Filter1Name or 'DBID' = @Filter1Name)
	BEGIN
		SELECT @strFilterString = @strFilterString + @Filter1Name + ' = ' + @Filter1 + CHAR(10)
	END	
			
	IF('AppName' = @Filter2Name or 'LoginName' = @Filter2Name or 'DBID' = @Filter2Name)
	BEGIN
		SELECT @strFilterString = @strFilterString + @Filter2Name + ' = ' + @Filter2 + CHAR(10)
	END	

	IF('AppName' = @Filter3Name or 'LoginName' = @Filter3Name or 'DBID' = @Filter3Name)
	BEGIN
		SELECT @strFilterString = @strFilterString + @Filter3Name + ' = ' + @Filter3 + CHAR(10)
	END	

	IF('AppName' = @Filter4Name or 'LoginName' = @Filter4Name or 'DBID' = @Filter4Name)
	BEGIN
		SELECT @strFilterString = @strFilterString + @Filter4Name + ' = ' + @Filter4 + CHAR(10)
	END		

	SELECT  CASE WHEN @strFilterString = '' THEN NULL 
		         ELSE @strFilterString 
	END AS 'FilterString'
END
GO


/*
exec sp_executesql @CmdText=N'exec spReporter_GetActualTimeRange 
	@StartTimeInterval, @EndTimeInterval',@VarDefs=N'@StartTimeInterval nvarchar, 
	@EndTimeInterval nvarchar',@StartTimeInterval=NULL,@EndTimeInterval=NULL
*/
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_GetActualTimeRange'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_GetActualTimeRange
GO

CREATE PROCEDURE ReadTrace.spReporter_GetActualTimeRange
		@StartTimeInterval INT = NULL,
		@EndTimeInterval   INT = NULL,
		@Filter1		NVARCHAR(256) = NULL,
		@Filter2		NVARCHAR(256) = NULL,
		@Filter3		NVARCHAR(256) = NULL,
		@Filter4		NVARCHAR(256) = NULL,
		@Filter1Name	NVARCHAR(64)  = NULL,
		@Filter2Name	NVARCHAR(64)  = NULL,
		@Filter3Name	NVARCHAR(64)  = NULL,
		@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN

	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT  
	(SELECT StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval) AS [StartTime],
	(SELECT EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval)   AS [EndTime]
END
GO


----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchTopN'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchTopN
GO

--	ReadTrace.spReporter_BatchTopN  null, null, 9, null, null
--	SELECT * FROM tblUniqueLoginNames
--	SELECT * FROM tblUniqueAppNames
--	SELECT DISTINCT(LoginNameID) from tblBatchPartialAggs
--	ReadTrace.spReporter_BatchTopN  null, null, 9, 'Connected Before Trace', null, null, null, 'LoginName'
--	ReadTrace.spReporter_BatchTopN  null, null, 9, 'Unspecified', null, null, null, 'LoginName'
--	ReadTrace.spReporter_BatchTopN  null, null, 9, 'Unspecified', 'PRIMUS:d589844e-ca63-4be1-a126-02d6e1e62770', null, null, 'LoginName', 'AppName'
CREATE PROCEDURE ReadTrace.spReporter_BatchTopN
	@StartTimeInterval  INT = NULL,
	@EndTimeInterval	INT = NULL,
	@TopN				INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	IF @TopN IS NULL SET @TopN = 10

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	--	Use the row_number and ORDER BY's to get list the # of entries that match
	--	Since unique row only returned 1 time this works like a large set of unions
	SELECT *,
		ROW_NUMBER() OVER(ORDER BY CPU DESC) AS QueryNumber
	FROM (
		   SELECT 	a.HashID,
		   SUM(CompletedEvents) AS Executes,
		   SUM(TotalCPU) AS CPU,
		   SUM(TotalDuration) AS Duration,
		   SUM(TotalReads) AS Reads,
		   SUM(TotalWrites) AS Writes,
		   SUM(AttentionEvents) AS Attentions, 
		   (SELECT StartTime FROM ReadTrace.tblTimeIntervals i WHERE TimeInterval = @StartTimeInterval) AS [StartTime],
		   (SELECT EndTime   FROM ReadTrace.tblTimeIntervals i WHERE TimeInterval = @EndTimeInterval) AS [EndTime],
		   (SELECT CAST(NormText AS NVARCHAR(4000)) FROM ReadTrace.tblUniqueBatches b WHERE b.HashID = a.HashID) AS [NormText],
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalCPU) DESC) AS CPUDesc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalCPU) ASC) AS CPUAsc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalDuration) DESC) AS DurationDesc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalDuration) ASC) AS DurationAsc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalReads) DESC) AS ReadsDesc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalReads) ASC) AS ReadsAsc,
			ROW_NUMBER() OVER(ORDER BY SUM(TotalWrites) DESC) AS WritesDesc,
		    ROW_NUMBER() OVER(ORDER BY SUM(TotalWrites) ASC) AS WritesAsc
			FROM ReadTrace.tblBatchPartialAggs a
			WHERE TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval 	AND a.AppNameID = ISNULL(@iAppNameID, a.AppNameID)	AND a.LoginNameID = ISNULL(@iLoginNameID, a.LoginNameID) AND a.DBID = ISNULL(@iDBID, a.DBID)
			GROUP BY a.HashID
		  ) AS Outcome
		WHERE 	
			(
			   CPUDesc      <= @TopN 
			OR CPUAsc       <= @TopN
			OR DurationDesc <= @TopN 
			OR DurationAsc  <= @TopN
			OR ReadsDesc    <= @TopN 
			OR ReadsAsc     <= @TopN
			OR WritesDesc   <= @TopN 
			OR WritesAsc    <= @TopN)
		ORDER BY CPU desc
		OPTION (RECOMPILE)
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_TopN'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_TopN
GO

CREATE PROCEDURE ReadTrace.spReporter_TopN
AS
BEGIN
	SET NOCOUNT ON
	SELECT 3   AS [TopN]
		UNION ALL
	SELECT 5   AS [TopN]
		UNION ALL
	SELECT 10  AS [TopN]
		UNION ALL
	SELECT 25  AS [TopN]
		UNION ALL
	SELECT 50  AS [TopN]
		UNION ALL
	SELECT 100 AS [TopN]
	ORDER BY TopN ASC
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_OrderByColumns'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_OrderByColumns
GO

CREATE PROCEDURE ReadTrace.spReporter_OrderByColumns
AS
BEGIN
	SET NOCOUNT ON
	SELECT CAST('CPU' AS VARCHAR(30)) AS [OrderByColumn]
		UNION ALL
	SELECT 'Duration'
		UNION ALL
	SELECT 'Reads'
		UNION ALL
	SELECT 'Writes'
		UNION ALL
	SELECT 'Executes'
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_ResourceUsageDuringInterval'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_ResourceUsageDuringInterval
GO

CREATE PROCEDURE ReadTrace.spReporter_ResourceUsageDuringInterval
	@StartTimeInterval INT = NULL,
	@EndTimeInterval   INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID   INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	DECLARE @ms BIGINT
	DECLARE @dtStart DATETIME, @dtEnd DATETIME
	SELECT @dtStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval
	SELECT @dtEnd   = EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval
	SELECT @ms = DATEDIFF(dd, @dtStart, @dtEnd) * CAST(86400000 AS BIGINT) + DATEDIFF(ms, DATEADD(dd, DATEDIFF(dd, @dtStart, @dtEnd), @dtStart), @dtEnd)
	
	-- Calculate total consumption during the intervals based on batch partial aggs (if we have it) or
	-- stmt aggs if batch-level events weren't captured
	IF EXISTS (SELECT * FROM ReadTrace.tblBatchPartialAggs)
	BEGIN
		SELECT
			@ms                AS ElapsedMilliseconds,
			SUM(TotalCPU)      AS IntervalCPU,
			SUM(TotalDuration) AS IntervalDuration,
			SUM(TotalReads)    AS IntervalReads,
			SUM(TotalWrites)   AS IntervalWrites
		FROM ReadTrace.tblBatchPartialAggs a
		WHERE TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval AND a.AppNameID = ISNULL(@iAppNameID, a.AppNameID) AND a.LoginNameID = ISNULL(@iLoginNameID, a.LoginNameID) AND a.DBID = ISNULL(@iDBID, a.DBID)
		OPTION (RECOMPILE)
	END
	ELSE
	BEGIN
		SELECT
			@ms				   AS ElapsedMilliseconds,
			SUM(TotalCPU)      AS IntervalCPU,
			SUM(TotalDuration) AS IntervalDuration,
			SUM(TotalReads)	   AS IntervalReads,
			SUM(TotalWrites)   AS IntervalWrites
		FROM ReadTrace.tblStmtPartialAggs a
		WHERE TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval AND a.AppNameID = ISNULL(@iAppNameID, a.AppNameID) AND a.LoginNameID = ISNULL(@iLoginNameID, a.LoginNameID) AND a.DBID = ISNULL(@iDBID, a.DBID)
		OPTION (RECOMPILE)
	END
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_ExampleBatchDetails'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_ExampleBatchDetails
GO

CREATE PROCEDURE ReadTrace.spReporter_ExampleBatchDetails
	@HashID BIGINT
AS
BEGIN
	SET NOCOUNT ON
	SELECT TOP 1
		ub.NormText,
		ub.OrigText,
		p.[Name] AS SpecialProcName, 
		b.ConnId,
		b.Session,
		b.Request,
		CONVERT(VARCHAR(30), b.StartTime, 121) AS StartTime,
		CONVERT(VARCHAR(30), b.EndTime, 121) AS EndTime,
		b.Reads,
		b.Writes,
		b.CPU,
		b.Duration,
		(SELECT TOP 1 TraceFileName FROM ReadTrace.tblTraceFiles WHERE FirstSeqNumber <= [b].[BatchSeq] ORDER BY FirstSeqNumber DESC) AS [File]
		 FROM ReadTrace.tblUniqueBatches ub
		 JOIN ReadTrace.tblBatches        b ON ub.Seq           = b.BatchSeq
	LEFT JOIN ReadTrace.tblProcedureNames p ON ub.SpecialProcID = p.SpecialProcID
	WHERE ub.HashID = @HashID
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchDetails'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchDetails
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchDetails 
	@HashID				BIGINT, 
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval    INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name


	SELECT 
		MIN(t.StartTime) AS StartTime,
		MIN(t.EndTime)   AS EndTime,
		t.TimeInterval,
		SUM(ISNULL(pa.StartingEvents, 0))  AS StartingEvents,
		SUM(ISNULL(pa.CompletedEvents, 0)) AS CompletedEvents,
		SUM(ISNULL(pa.AttentionEvents, 0)) AS Attentions,
		SUM(ISNULL(pa.TotalDuration, 0)) AS Duration,
		--MIN(ISNULL(pa.MinDuration, 0)) AS MinDuration,
		--MAX(ISNULL(pa.MaxDuration, 0)) AS MaxDuration,
		SUM(ISNULL(pa.TotalCPU, 0)) AS CPU,
		--MIN(ISNULL(pa.MinCPU, 0)) AS MinCPU,
		--MAX(ISNULL(pa.MaxCPU, 0)) AS MaxCPU,
		SUM(ISNULL(pa.TotalReads, 0)) AS Reads,
		--MIN(ISNULL(pa.MinReads, 0)) AS MinReads,
		--MAX(ISNULL(pa.MaxReads, 0)) AS MaxReads,
		SUM(ISNULL(pa.TotalWrites, 0)) AS Writes
		--MIN(ISNULL(pa.MinWrites, 0)) AS MinWrites,
		--MAX(ISNULL(pa.MaxWrites, 0)) AS MaxWrites
	     FROM ReadTrace.tblTimeIntervals t
	LEFT JOIN (
				SELECT * FROM ReadTrace.tblBatchPartialAggs 
				WHERE HashID = @HashID AND DBID = ISNULL(@iDBID, DBID) AND AppNameID = ISNULL(@iAppNameID, AppNameID) AND LoginNameID = ISNULL(@iLoginNameID, LoginNameID)
			  ) AS pa   ON pa.TimeInterval = t.TimeInterval
	WHERE	t.TimeInterval >= @StartTimeInterval AND t.TimeInterval <= @EndTimeInterval
	GROUP BY t.TimeInterval
	ORDER BY t.TimeInterval
	OPTION (RECOMPILE)
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchDistinctPlans'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchDistinctPlans
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchDistinctPlans 
	@HashID			   BIGINT, 
	@StartTimeInterval INT = NULL, 
	@EndTimeInterval   INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		 INT
	DECLARE @iLoginNameID	 INT
	DECLARE @iDBID			 INT
	DECLARE @plans_collected BIT
	DECLARE @multiple_plans_per_batch BIT
	DECLARE @query_has_no_plan BIT
	DECLARE @dtStart DATETIME, @dtEnd DATETIME

	SELECT @plans_collected = 0x1, @multiple_plans_per_batch = 0x0, @query_has_no_plan = 0x0

	-- Exit immediately if they didn't capture showplan/statistics profile
	IF NOT EXISTS(SELECT * FROM ReadTrace.tblTracedEvents WHERE EventID in (97, 98))
	BEGIN
		PRINT 'No plans collected'
		SELECT @plans_collected = 0x0
		GOTO exit_now
	END

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT @dtStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval
	SELECT @dtEnd   = EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	-- If this is a batch which always had a single statement (i.e., one query plan event per batch)
	IF EXISTS (SELECT b.BatchSeq, COUNT(*) FROM ReadTrace.tblBatches AS b WITH (INDEX(tblBatches_HashID))
										   JOIN ReadTrace.tblPlans p on p.BatchSeq = b.BatchSeq WHERE b.HashID = @HashID AND b.StartTime >= @dtStart AND b.EndTime <= @dtEnd
			   GROUP BY b.BatchSeq
			   HAVING COUNT(*) > 1)
	BEGIN
		PRINT 'Multiple plans'
		SELECT @multiple_plans_per_batch = 0x1
		GOTO exit_now
	END
	ELSE
	BEGIN
		--		Azure does not support select into
		CREATE TABLE #temp
		(
			PlanHashID			BIGINT,
			Rows				BIGINT,
			Executes			BIGINT,
			StmtText			NVARCHAR(max),
			StmtID				INT,
			NodeID				SMALLINT,
			Parent				SMALLINT,
			PhysicalOp			VARCHAR(30),
			LogicalOp			VARCHAR(30),
			Argument			NVARCHAR(256),
			DefinedValues		NVARCHAR(256),
			EstimateRows		FLOAT,
			EstimateIO			FLOAT,
			EstimateCPU			FLOAT,
			AvgRowSize			INT,
			TotalSubtreeCost	FLOAT,
			OutputList			NVARCHAR(256),
			Warnings			VARCHAR(100),
			Type				VARCHAR(30),
			Parallel			TINYINT,
			EstimateExecutions	FLOAT,
			RowOrder			SMALLINT,
			--		Aggregates 
			PlanExecutes		BIGINT, 
			PlanFirstUsed		DATETIME, 
			PlanLastUsed		DATETIME,
			PlanMinReads		BIGINT,
			PlanMaxReads		BIGINT,
			PlanAvgReads		BIGINT,
			PlanTotalReads		BIGINT,
			PlanMinWrites		BIGINT,
			PlanMaxWrites		BIGINT,
			PlanAvgWrites		BIGINT,
			PlanTotalWrites		BIGINT,
			PlanMinCPU			BIGINT,
			PlanMaxCPU			BIGINT,
			PlanAvgCPU			BIGINT,
			PlanTotalCPU		BIGINT,
			PlanMinDuration		BIGINT,
			PlanMaxDuration		BIGINT,
			PlanAvgDuration		BIGINT,
			PlanTotalDuration	BIGINT,
			PlanAttnCount	    BIGINT
		)

		-- Then return the plan text, rows, executes information for each plan that was used, as well as 
		-- statistics about the number of times that plan was used, when it was first/last used, IO, CPU 
		-- and usage statistics, etc
		INSERT INTO #temp
		SELECT      upr.*,
					p.PlanExecutes, 
					p.PlanFirstUsed, 
					p.PlanLastUsed,
					p.PlanMinReads,
					p.PlanMaxReads,
					p.PlanAvgReads,
					p.PlanTotalReads,
					p.PlanMinWrites,
					p.PlanMaxWrites,
					p.PlanAvgWrites,
					p.PlanTotalWrites,
					p.PlanMinCPU,
					p.PlanMaxCPU,
					p.PlanAvgCPU,
					p.PlanTotalCPU,
					p.PlanMinDuration,
					p.PlanMaxDuration,
					p.PlanAvgDuration,
					p.PlanTotalDuration,
					p.PlanAttnCount
		FROM ReadTrace.tblUniquePlanRows upr
		JOIN ( SELECT p.PlanHashID, 
					COUNT_BIG(b.BatchSeq) AS PlanExecutes, 
					MIN(b.StartTime) AS PlanFirstUsed, 
					MAX(b.StartTime) AS PlanLastUsed,
					MIN(b.Reads) AS PlanMinReads,
					MAX(b.Reads) AS PlanMaxReads,
					AVG(b.Reads) AS PlanAvgReads,
					SUM(b.Reads) AS PlanTotalReads,
					MIN(b.Writes) AS PlanMinWrites,
					MAX(b.Writes) AS PlanMaxWrites,
					AVG(b.Writes) AS PlanAvgWrites,
					SUM(b.Writes) AS PlanTotalWrites,
					MIN(b.CPU) AS PlanMinCPU,
					MAX(b.CPU) AS PlanMaxCPU,
					AVG(b.CPU) AS PlanAvgCPU,
					SUM(b.CPU) AS PlanTotalCPU,
					MIN(b.Duration) AS PlanMinDuration,
					MAX(b.Duration) AS PlanMaxDuration,
					AVG(b.Duration) AS PlanAvgDuration,
					SUM(b.Duration) AS PlanTotalDuration,
					SUM(CASE WHEN b.AttnSeq IS NOT NULL THEN 1 ELSE 0 END) AS PlanAttnCount
				FROM ReadTrace.tblBatches b
		   LEFT JOIN ReadTrace.tblPlans   p ON p.BatchSeq = b.BatchSeq
			   WHERE b.HashID = @HashID AND b.StartTime >= @dtStart	AND b.EndTime <= @dtEnd
			   GROUP BY p.PlanHashID
			   ) as p on p.PlanHashID = upr.PlanHashID
			   OPTION (RECOMPILE);

		-- Many types of statements may not generate a showplan (e.g., DECLARE, IF (scalar), SET, RETURN, ...)
		-- Still need to ensure that we return a row indicating there is no plan
		IF @@ROWCOUNT = 0
		BEGIN
			PRINT 'No plan for this query'
			SET @query_has_no_plan = 0x1
			GOTO exit_now
		END
	END

	;WITH plan_hierarchy AS
	(
		SELECT *, 0 AS tree_level  FROM #temp t WHERE Parent IS NULL
		UNION ALL
		SELECT t.*, tree_level + 1 FROM #temp t 
			                       JOIN plan_hierarchy p ON t.PlanHashID = p.PlanHashID AND t.Parent = p.NodeID
	)
	SELECT 
		@plans_collected AS fPlansCollected,
		@multiple_plans_per_batch AS fMultiplePlansPerBatch,
		@query_has_no_plan AS fQueryHasNoPlan,
		p.PlanHashID, 
		p.PlanExecutes,
		p.PlanFirstUsed, 
		p.PlanLastUsed,
		p.PlanMinReads,
		p.PlanMaxReads,
		p.PlanAvgReads,
		p.PlanTotalReads,
		p.PlanMinWrites,
		p.PlanMaxWrites,
		p.PlanAvgWrites,
		p.PlanTotalWrites,
		p.PlanMinCPU,
		p.PlanMaxCPU,
		p.PlanAvgCPU,
		p.PlanTotalCPU,
		p.PlanMinDuration,
		p.PlanMaxDuration,
		p.PlanAvgDuration,
		p.PlanTotalDuration,
		p.PlanAttnCount,
		p.Warnings,
		p.EstimateRows,
		p.EstimateExecutions,
		p.RowOrder,
		p.tree_level,
		CASE WHEN PATINDEX(N'%|--%', StmtText) > 0  THEN SUBSTRING(StmtText, PATINDEX(N'%|--%', StmtText) + 3, DATALENGTH(StmtText) - PATINDEX(N'%|--%', StmtText) - 3)
			 ELSE LTRIM(StmtText)
		END AS StmtText
	FROm plan_hierarchy p
	ORDER BY p.PlanExecutes DESC, p.PlanHashID, p.RowOrder
	RETURN;

exit_now:
	SELECT 
		@plans_collected AS fPlansCollected,
		@multiple_plans_per_batch AS fMultiplePlansPerBatch,
		@query_has_no_plan AS fQueryHasNoPlan,
		CAST(NULL AS BIGINT) AS PlanHashID, 
		CAST(NULL AS BIGINT) AS PlanExecutes,
		CAST(NULL AS DATETIME) AS PlanFirstUsed, 
		CAST(NULL AS DATETIME) AS PlanLastUsed,
		CAST(NULL AS BIGINT) AS PlanMinReads,
		CAST(NULL AS BIGINT) AS PlanMaxReads,
		CAST(NULL AS BIGINT) AS PlanAvgReads,
		CAST(NULL AS BIGINT) AS PlanTotalReads,
		CAST(NULL AS BIGINT) AS PlanMinWrites,
		CAST(NULL AS BIGINT) AS PlanMaxWrites,
		CAST(NULL AS BIGINT) AS PlanAvgWrites,
		CAST(NULL AS BIGINT) AS PlanTotalWrites,
		CAST(NULL AS BIGINT) AS PlanMinCPU,
		CAST(NULL AS BIGINT) AS PlanMaxCPU,
		CAST(NULL AS BIGINT) AS PlanAvgCPU,
		CAST(NULL AS BIGINT) AS PlanTotalCPU,
		CAST(NULL AS BIGINT) AS PlanMinDuration,
		CAST(NULL AS BIGINT) AS PlanMaxDuration,
		CAST(NULL AS BIGINT) AS PlanAvgDuration,
		CAST(NULL AS BIGINT) AS PlanTotalDuration,
		CAST(NULL AS BIGINT) AS PlanAttnCount,
		CAST(NULL AS VARCHAR(100)) AS Warnings,
		CAST(NULL AS FLOAT) AS EstimateRows,
		CAST(NULL AS FLOAT) AS EstimateExecutions,
		CAST(NULL AS INT) AS RowOrder,
		CAST(NULL AS INT) AS tree_level,
		CAST(NULL AS NVARCHAR(max)) AS StmtText
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchDetailsScaleFactor'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchDetailsScaleFactor
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchDetailsScaleFactor 
	@HashID				BIGINT, 
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval    INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN	
	DECLARE @MaxEventCount int

	CREATE TABLE #BatchDetails (
		StartTime DATETIME, 
		EndTime DATETIME, 
		TimeInterval INT, 
		StartingEvents INT, 
		CompletedEvents INT, 
		Attentions INT, 
		Duration BIGINT,
		--MinDuration BIGINT,
		--MaxDuration BIGINT,
		CPU BIGINT,
		--MinCPU BIGINT,
		--MaxCPU BIGINT,
		Reads BIGINT, 
		--MinReads BIGINT,
		--MaxReads BIGINT,
		Writes BIGINT
		--MinWrites BIGINT,
		--MaxWrites bigint
		)

	-- Insert the aggregated batch information for the specified time window into a local temp table
	INSERT INTO #BatchDetails EXEC ReadTrace.spReporter_BatchDetails @HashID, @StartTimeInterval, @EndTimeInterval, @Filter1, @Filter2, @Filter3, @Filter4, @Filter1Name, @Filter2Name, @Filter3Name, @Filter4Name
	
	-- I want to make sure that I always chart starting & completed events on the same scale, so that if
	-- their is some divergence in the number (due to longer running queries, blocking, etc) that the two
	-- lines diverge and make this very obvious.  Therefore I get the max of either of these two and use it
	-- as input for scaling in the final query below
	SELECT @MaxEventCount = MAX(NumberOfEvents) 
	FROM (
		  SELECT MAX(StartingEvents)  AS NumberOfEvents FROM #BatchDetails
		  UNION ALL
		  SELECT MAX(CompletedEvents) AS NumberOfEvents FROM #BatchDetails
		 ) AS t

	SELECT
		CASE WHEN @MaxEventCount  <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS StartingEventsScale,
		CASE WHEN @MaxEventCount  <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS CompletedEventsScale,
		CASE WHEN MAX(Attentions) <= 100 THEN 1.0 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(MAX(Attentions)) END AS AttentionEventsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgDuration)) AS DurationScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgReads))    AS ReadsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgWrites))   AS WritesScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgCPU))      AS CPUScale,
		MAX(a.StartingEvents)  AS MaxStartingEvents,
		MAX(a.CompletedEvents) AS MaxCompletedEvents,
		MAX(a.Attentions)      AS MaxAttentionEvents,
		MAX(a.Duration)        AS MaxDuration,
		MAX(a.Reads)           AS MaxReads,
		MAX(a.Writes)          AS MaxWrites,
		MAX(a.CPU)             AS MaxCPU
	FROM (
			SELECT 
			StartingEvents,
			CompletedEvents,
			Attentions,
			Duration,
			Reads,
			Writes,
			CPU,
			CASE WHEN CompletedEvents > 0 THEN CPU / CompletedEvents      ELSE NULL END AS AvgCPU,
			CASE WHEN CompletedEvents > 0 THEN Duration / CompletedEvents ELSE NULL END AS AvgDuration,
			CASE WHEN CompletedEvents > 0 THEN Reads / CompletedEvents    ELSE NULL END AS AvgReads,
			CASE WHEN CompletedEvents > 0 THEN Writes / CompletedEvents   ELSE NULL END AS AvgWrites
		 FROM #BatchDetails) AS a	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchDetailsMinMaxAvg'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchDetailsMinMaxAvg
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchDetailsMinMaxAvg
	@HashID				 BIGINT,
	@StartTimeInterval   INT = NULL, 
	@EndTimeInterval     INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT 
		MIN(b.MinReads) AS BatchMinReads,
		MAX(b.MaxReads) AS BatchMaxReads,
		SUM(b.TotalReads) / SUM(b.CompletedEvents) AS BatchAvgReads,
		SUM(b.TotalReads) AS BatchTotalReads,
		MIN(b.MinWrites) AS BatchMinWrites,
		MAX(b.MaxWrites) AS BatchMaxWrites,
		SUM(b.TotalWrites) / SUM(b.CompletedEvents) AS BatchAvgWrites,
		SUM(b.TotalWrites) AS BatchTotalWrites,
		MIN(b.MinCPU) AS BatchMinCPU,
		MAX(b.MaxCPU) AS BatchMaxCPU,
		SUM(b.TotalCPU) / SUM(b.CompletedEvents) AS BatchAvgCPU,
		SUM(b.TotalCPU) AS BatchTotalCPU,
		MIN(b.MinDuration) AS BatchMinDuration,
		MAX(b.MaxDuration) AS BatchMaxDuration,
		SUM(b.TotalDuration) / SUM(b.CompletedEvents) AS BatchAvgDuration,
		SUM(b.TotalDuration) AS BatchTotalDuration
	FROM ReadTrace.tblBatchPartialAggs b
	WHERE b.HashID = @HashID  
		AND b.TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval
		AND b.DBID = ISNULL(@iDBID, b.DBID)
		AND b.AppNameID   = ISNULL(@iAppNameID, b.AppNameID)
		AND b.LoginNameID = ISNULL(@iLoginNameID, b.LoginNameID)
END
GO



----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_Warnings'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_Warnings
GO

CREATE PROCEDURE ReadTrace.spReporter_Warnings
AS
BEGIN
	SET NOCOUNT ON	
	SELECT WarningMessage, NumberOfTimes, FirstGlobalSeq, fMayAffectCPU, fMayAffectIO, fMayAffectDuration, fAffectsEventAssociation
	FROM ReadTrace.tblWarnings
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_PartialAggs_GroupBy'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_PartialAggs_GroupBy
GO

CREATE PROCEDURE ReadTrace.spReporter_PartialAggs_GroupBy
AS
BEGIN
	SET NOCOUNT ON
	SELECT 'Application Name' AS 'GroupBy', 'AppName' AS 'Value'
	UNION
	SELECT 'Login Name', 'LoginName'
	UNION
	SELECT 'Database Id', 'DBID'
	ORDER BY 1	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_PartialAggs_OrderBy'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_PartialAggs_OrderBy
GO

CREATE PROCEDURE ReadTrace.spReporter_PartialAggs_OrderBy
AS
BEGIN
	SET NOCOUNT ON
	CREATE TABLE #tbl 	(OrderBy	VARCHAR(30) COLLATE database_default NOT NULL,	Value	VARCHAR(30) COLLATE database_default NOT NULL	)

	INSERT INTO #tbl	
	EXEC ReadTrace.spReporter_PartialAggs_GroupBy


	SELECT * FROM #tbl
	UNION
	SELECT 'Reads' AS 'OrderBy', 'Reads' AS 'Value'
	UNION
	SELECT 'Reads Desc' , 'Reads Desc'
	UNION
	SELECT 'Writes', 'Writes'
	UNION
	SELECT 'Writes Desc', 'Writes Desc'
    UNION
	SELECT 'CPU', 'CPU'
	UNION
	SELECT 'CPU Desc', 'CPU Desc'
	UNION
	SELECT 'Duration', 'Duration'
	UNION
	SELECT 'Duration Desc', 'Duration Desc'
	UNION
	SELECT 'Batches Started', 'StartingEvents'
	UNION
	SELECT 'Batches Started Desc', 'StartingEvents Desc'
	UNION
	SELECT 'Batches Completed', 'CompletedEvents'
	UNION
	SELECT 'Batches Completed Desc', 'CompletedEvents Desc'
	UNION
	SELECT 'Attentions', 'Attentions'
	UNION
	SELECT 'Attentions Desc', 'Attentions Desc'
	ORDER BY 1	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_GetUnitsForDuration'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_GetUnitsForDuration
GO

CREATE PROCEDURE ReadTrace.spReporter_GetUnitsForDuration
AS
BEGIN
	SET NOCOUNT ON
	SELECT CASE WHEN Value < 9 THEN N'ms'
			    ELSE NCHAR(181) + N's'
		END AS DurationUnits
	FROM ReadTrace.tblMiscInfo
	WHERE Attribute = 'EventVersion'		
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchAggregatesGrouped'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchAggregatesGrouped
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchAggregatesGrouped
	@StartTimeInterval INT,
	@EndTimeInterval   INT,
	@Group1Field   VARCHAR(30),
	@Group1Value NVARCHAR(256) = NULL,
	@Group2Field   VARCHAR(30) = NULL,
	@Group2Value NVARCHAR(256) = NULL,
	@Group3Field   VARCHAR(30) = NULL
AS
BEGIN
	SET NOCOUNT ON

	IF(@StartTimeInterval IS NULL)
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF(@EndTimeInterval IS NULL)
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals

	IF EXISTS (SELECT * FROM 
					(
					SELECT @Group1Field AS param_value
					UNION ALL 
					SELECT @Group2Field
					UNION ALL
					SELECT @Group3Field
					) AS p
		WHERE p.param_value IS NOT NULL and p.param_value NOT IN ('DBID', 'AppName', 'LoginName'))
	BEGIN
		RAISERROR('ERROR: An invalid grouping parameter was specified', 16, 1)
		RETURN
	END

	DECLARE @column             VARCHAR(128)
	DECLARE @select_columns     VARCHAR(1000)
	DECLARE @grouping_columns   VARCHAR(1000)
	DECLARE @remaining_columns  VARCHAR(100)
	DECLARE @query_body         VARCHAR(8000)
	DECLARE @param_definition   NVARCHAR(4000)
	DECLARE @filter             VARCHAR(8000)

	SELECT @filter = CHAR(13) + CHAR(10)
	SELECT @grouping_columns = CHAR(13) + CHAR(10) + 'GROUP BY '
	SELECT @param_definition = N'@StartTimeInterval INT, @EndTimeInterval INT, @Group1Value NVARCHAR(256), @Group2Value NVARCHAR(256)'
	SELECT @query_body = ',
			SUM(StartingEvents) AS [StartingEvents],
			SUM(CompletedEvents) AS [CompletedEvents],
			SUM(AttentionEvents) AS [Attentions],
			SUM(TotalDuration) AS [Duration],
			SUM(TotalReads) AS [Reads],
			SUM(TotalWrites) AS [Writes],
			SUM(TotalCPU) AS [CPU]
		      FROM ReadTrace.tblBatchPartialAggs b
		INNER JOIN ReadTrace.tblUniqueAppNames   a ON a.iID = b.AppNameID
		INNER JOIN ReadTrace.tblUniqueLoginNames l ON l.iID = b.LoginNameID
		WHERE b.TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval'

	IF @Group1Value IS NOT NULL
	BEGIN
		SELECT @select_columns = '@Group1Value as [Group1]'
		SELECT @column = CASE @Group1Field WHEN 'DBID'      THEN 'b.DBID'
										   WHEN 'AppName'   THEN 'a.AppName'
										   WHEN 'LoginName' THEN 'l.LoginName'
						 END
		SELECT @filter = @filter + 'AND ' + @column + ' = @Group1Value'
	END
	ELSE IF @Group1Field IS NOT NULL
	BEGIN
		SELECT @column = CASE @Group1Field WHEN 'DBID'      THEN 'b.DBID'
										   WHEN 'AppName'   THEN 'a.AppName'
										   WHEN 'LoginName' THEN 'l.LoginName'
						 END

		SELECT @select_columns = @column + ' as [Group1]'
		SELECT @grouping_columns = @grouping_columns + @column
		SELECT @remaining_columns = ', NULL as [Group2], NULL as [Group3]'
	END
	IF @Group2Value IS NOT NULL
	BEGIN
		SELECT @select_columns = @select_columns + ', @Group2Value as [Group2]'

		SELECT @column = CASE @Group2Field WHEN 'DBID'      THEN 'b.DBID'
										   WHEN 'AppName'   THEN 'a.AppName'
										   WHEN 'LoginName' THEN 'l.LoginName'
						 END
		SELECT @filter = @filter + ' AND ' + @column + ' = @Group2Value'
	END
	ELSE IF @Group2Field IS NOT NULL
	BEGIN
		SELECT @column = CASE @Group2Field WHEN 'DBID'      THEN 'b.DBID'
										   WHEN 'AppName'   THEN 'a.AppName'
										   WHEN 'LoginName' THEN 'l.LoginName'
						 END

		SELECT @select_columns = @select_columns + ', ' + @column + ' as [Group2]'
		SELECT @grouping_columns = @grouping_columns + @column
		SELECT @remaining_columns = ', NULL as [Group3]'
	END
	IF @Group3Field IS NOT NULL
	BEGIN
		SELECT @column = CASE @Group3Field WHEN 'DBID'      THEN 'b.DBID'
								           WHEN 'AppName'   THEN 'a.AppName'
										   WHEN 'LoginName' THEN 'l.LoginName'
						 END

		SELECT @select_columns = @select_columns + ', ' + @column + ' as [Group3]'
		SELECT @grouping_columns = @grouping_columns + @column
		SELECT @remaining_columns = ''
	END

	DECLARE @final_query NVARCHAR(MAX)
	SELECT @final_query = 'SELECT ' + @select_columns + @remaining_columns + @query_body + @filter + @grouping_columns + ' OPTION (RECOMPILE)'
	--SELECT @final_query
	EXEC sp_executesql @final_query, @param_definition, @StartTimeInterval, @EndTimeInterval, @Group1Value, @Group2Value
END
GO


----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_GetQueriesAssociatedWithEvent'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_GetQueriesAssociatedWithEvent
GO

CREATE PROCEDURE ReadTrace.spReporter_GetQueriesAssociatedWithEvent
	@EventID INT,							-- the trace_event_id of interest/what queries caused this event
	@StartTimeInterval INT,					-- the starting time range of when the EVENT occurred (not when the batch/stmt started)
	@EndTimeInterval INT,					-- the ending time range of when the EVENT occurred (not when the batch/stmt completed)
	@TopN int								-- limit result set to this number of queries
AS
BEGIN
	DECLARE @dtEventStart DATETIME, @dtEventEnd DATETIME
	DECLARE @TotalEvents  FLOAT
	DECLARE @localEventID INT

	IF (@StartTimeInterval IS NULL)
	BEGIN
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
	
	SELECT @dtEventStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval

	IF (@EndTimeInterval IS NULL)
	BEGIN
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END

	SELECT @dtEventEnd = EndTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	SELECT @TopN = ISNULL(@TopN, 25), @localEventID = @EventID

	SELECT @TotalEvents = COUNT(*) 
	FROM ReadTrace.tblInterestingEvents 
	WHERE EventID = @EventID and ISNULL(EndTime, StartTime) BETWEEN @dtEventStart AND @dtEventEnd
	--
	-- Not all events can be associated with a query (e.g., server memory change) so NULLable HashIDs are necessary
	--
	DECLARE @AffectedQueries TABLE (BatchHashID BIGINT NULL, StmtHashID BIGINT NULL, NumberOfEvents BIGINT NOT NULL, StmtText NVARCHAR(MAX) NULL)
	--
	--	Attentions are important enough that I have precomputed this info.  Also normal matching rules
	--	wouldn't work anyway because attention event shows up AFTER the completed event
	--
	IF @EventID = 16
	BEGIN
		INSERT INTO @AffectedQueries (BatchHashID, StmtHashID, NumberOfEvents) 
			SELECT TOP (@TopN)
				b.HashID AS BatchHashID,
				s.HashID AS StmtHashID,
				COUNT(*)
			FROM ReadTrace.tblBatches b
			JOIN ReadTrace.tblInterestingEvents i ON b.AttnSeq = i.Seq
	   LEFT JOIN ReadTrace.tblStatements        s ON s.AttnSeq = b.AttnSeq
		   WHERE b.AttnSeq IS NOT NULL 	and i.StartTime BETWEEN @dtEventStart AND @dtEventEnd
		GROUP BY b.HashID, s.HashID
		ORDER BY COUNT(*) DESC
	END
	ELSE IF @EventID IN
	       (
	        37, /* SP:Recompile */
			58, /* Autostats */ 
			166 /* SQL:StmtRecompile */
		   )
	BEGIN
		--
		-- In general these events fire before the associated *Starting event and there isn't a very reliable
		-- way via query to associate to stmt-level event due to nestlevel and various other quirks, especially
		-- if SP:StmtStarting wasn't captured
		--
		INSERT INTO @AffectedQueries (BatchHashID, StmtHashID, NumberOfEvents) 
		SELECT TOP (@TopN)
		      b.HashID AS BatchHashID, 
			  NULL AS StmtHashID, 
			  COUNT(*)
--			  CASE WHEN @EventID = 166 THEN i.TextData			-- SQL:StmtRecompile has the text of the query in the event itself
--			  ELSE NULL
--            END AS StmtText
			FROM ReadTrace.tblInterestingEvents i
	   LEFT JOIN ReadTrace.tblBatches           b ON b.BatchSeq = i.BatchSeq
		   WHERE i.EventID = @EventID AND ISNULL(i.EndTime, i.StartTime) BETWEEN @dtEventStart AND @dtEventEnd
		GROUP BY b.HashID
		ORDER BY COUNT(*) DESC	
	END
	ELSE
	BEGIN
		-- All other events.  Assumption is that they happen on the same Session and the event's sequence number is
		-- between the startseq and endseq for the associated statement
		INSERT INTO @AffectedQueries (BatchHashID, StmtHashID, NumberOfEvents) 
		SELECT TOP (@TopN)
		      y.HashID AS BatchHashID, 
			  z.HashID AS StmtHashID, 
			  COUNT(*)
			FROM 
				(
				SELECT
				i.BatchSeq,
				(SELECT TOP 1 StmtSeq FROM ReadTrace.tblStatements WHERE BatchSeq = i.BatchSeq	AND i.Seq BETWEEN ISNULL(StartSeq, 0) AND ISNULL(EndSeq, 9223372036854775807) ORDER BY ISNULL(StartSeq, 0) DESC, ISNULL(EndSeq, 9223372036854775807) ASC) AS StmtSeq 
				 FROM ReadTrace.tblInterestingEvents i
				 WHERE i.EventID = @EventID AND ISNULL(i.EndTime, i.StartTime) BETWEEN @dtEventStart AND @dtEventEnd
				) AS x
			LEFT JOIN ReadTrace.tblBatches    y ON y.BatchSeq = x.BatchSeq
			LEFT JOIN ReadTrace.tblStatements z ON z.StmtSeq  = x.StmtSeq
			GROUP BY y.HashID, z.HashID
			ORDER BY COUNT(*) DESC	
	END
	SELECT
	    t.NumberOfEvents,
		CASE WHEN @TotalEvents > 0 THEN t.NumberOfEvents / @TotalEvents ELSE 0.0 END AS PctOfEvents,
		t.BatchHashID,
		ub.NormText AS BatchTemplate,
		t.StmtHashID,
		COALESCE(t.StmtText, us.NormText) AS StmtTemplate
     FROM @AffectedQueries AS t
LEFT JOIN ReadTrace.tblUniqueBatches    ub ON ub.HashID = t.BatchHashID
LEFT JOIN ReadTrace.tblUniqueStatements us ON us.HashID = t.StmtHashID
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_TopStatementsInBatch'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_TopStatementsInBatch
GO

CREATE PROCEDURE ReadTrace.spReporter_TopStatementsInBatch
	@StartTimeInterval INT,					-- the starting time range of when the associated batch completed
	@EndTimeInterval INT,					-- the ending time range of when the associated batch completed
	@HashID BIGINT,							-- show resource usage for batches with this hashid
	@TopN INT,								-- limit result set to this number of queries per tree-level in the hierarchy
	@OrderBy VARCHAR(20)					-- what to ORDER BY (CPU | Duration | Reads | Writes)
AS
BEGIN
	DECLARE @dtEventStart DATETIME, @dtEventEnd DATETIME

	IF (@StartTimeInterval IS NULL)
	BEGIN
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
	SELECT @dtEventStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval

	IF (@EndTimeInterval IS NULL)
	BEGIN
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
	SELECT @dtEventEnd = EndTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	SELECT @TopN = ISNULL(@TopN, 3)

	--
	-- This select is a relatively quick and efficient way to get the TopN statements from all nestlevels 
	-- associated with this batch.  But just because it was TopN at its own nestlevel doesn't mean that it is 
	-- part of the tree of most expensive statements though.  Consider this example:
	--		PROCA
	--			SELECT 1
	--			SELECT 2
	--			SELECT 3
	--			EXEC PROCB
	--				SELECT 4
	--				EXEC PROCC
	--					SELECT 5
	-- Let's assume that TopN = 3.  This initial query will return the TopN statements from all three procs (a, b, c).
	-- If TopN=3, and SELECT 1, SELECT 2 and SELECT 3 have higher usage than the cumulative usage of PROCB then it is 
	-- irrelevant what usage was for statements in PROCB and nested PROCC since it isn't in the TopN at the highest 
	-- level. All of these nested statements which don't have a parent statement in the TopN at the higher level will 
	-- be filtered out in the final CTE below
	--
	-- NOTE: If a statement with the same HashID has different ParentHashIDs (same query shows up in multiple procs, or the
	-- same proc is called from multiple places) the report viewer control's recursive hierarchy detection first seems to
	-- group on the specified column and looses any that have other parent grouping column value.  To avoid having rows
	-- disappear I create a unique row_id to use for the hierarchy grouping
	--
	--	drop table #u
	CREATE TABLE #u
	(
		ParentHashID		BIGINT,
		HashID				BIGINT,
		NestLevel			INT,
		Executes			BIGINT,
		CPU					BIGINT,
		Duration			BIGINT,
		Reads				BIGINT,
		Writes				BIGINT,
		ordering_column		NVARCHAR(128),
		rank_at_nestlevel	BIGINT,
		row_id				bigint
	)
	
	INSERT INTO #u
	SELECT *, ROW_NUMBER() OVER (ORDER BY HashID) AS row_id 
	FROM (
			SELECT t.*,
			ROW_NUMBER() OVER (PARTITION BY ParentHashID, NestLevel ORDER BY ordering_column DESC) AS rank_at_nestlevel
			FROM
			(	
						SELECT
						s2.HashID AS ParentHashID,
						s.HashID,
						s.NestLevel,
						COUNT_BIG(*) AS Executes,
						SUM(s.CPU) AS CPU,
						SUM(s.Duration) AS Duration,
						SUM(s.Reads) AS Reads,
						SUM(s.Writes) AS Writes,
						CASE WHEN @OrderBy = 'CPU'      THEN SUM(s.CPU)
							 WHEN @OrderBy = 'Duration' THEN SUM(s.Duration)
							 WHEN @OrderBy = 'Reads'    THEN SUM(s.Reads)
							 WHEN @OrderBy = 'Writes'   THEN SUM(s.Writes)
							 WHEN @OrderBy = 'Executes' THEN COUNT(*)
							 ELSE NULL
						END AS ordering_column
					FROM ReadTrace.tblStatements  s
					JOIN ReadTrace.tblBatches     b ON s.BatchSeq      = b.BatchSeq
			   LEFT JOIN ReadTrace.tblStatements s2 ON s.ParentStmtSeq = s2.StmtSeq
				   WHERE b.HashID = @HashID
						AND s.EndSeq IS NOT NULL									-- recompile and XStmtFlush may cause SP:StmtStarting, SP:StmtStarting, SP:StmtCompleted and we only want to count as one execute
						AND b.EndTime BETWEEN @dtEventStart AND @dtEventEnd			-- batch had to complete to show up on batch details report
					GROUP BY s2.HashID, s.HashID, s.NestLevel
			) AS t
	WHERE ISNULL(ordering_column, 1) > 0
	) AS u
	WHERE rank_at_nestlevel <= @TopN

	-- Define CTE to walk the hierarchy so as to only show statements who have a parent in the TopN at a higher level
	;WITH stmt_hierarchy AS
	(
		SELECT CAST(NULL AS BIGINT) AS parent_id,  row_id,  ParentHashID,  HashID,  Executes,  CPU,  Duration,  Reads,  Writes,  rank_at_nestlevel,  NestLevel,  ordering_column
		FROM #u 
		WHERE ParentHashID IS NULL
		UNION ALL 
		SELECT             h.row_id AS parent_id,u.row_id,u.ParentHashID,u.HashID,u.Executes,u.CPU,u.Duration,u.Reads,u.Writes,u.rank_at_nestlevel,u.NestLevel,u.ordering_column
		FROM #u AS u
		JOIN stmt_hierarchy h ON u.ParentHashID = h.HashID 
		WHERE u.NestLevel > h.NestLevel		-- nestlevel may skip, but child must have higher nestlevel or you can have infinite recursion
	)

	-- Final results, including the normalized text for the statements
	SELECT
		h.row_id,
		h.parent_id,
		h.ParentHashID,
		h.HashID, 
		h.Executes,
		h.CPU,
		h.Duration,
		h.Reads,
		h.Writes,
		h.rank_at_nestlevel,
		h.NestLevel,
--		h.ordering_column,
		us1.NormText AS ParentNormText,
		us2.NormText AS NormText
	     FROM stmt_hierarchy h
	LEFT JOIN ReadTrace.tblUniqueStatements us1 ON h.ParentHashID = us1.HashID
	LEFT JOIN ReadTrace.tblUniqueStatements us2 ON h.HashID       = us2.HashID
	ORDER BY ordering_column DESC
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_ExampleStmtDetails'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_ExampleStmtDetails
GO

CREATE PROCEDURE ReadTrace.spReporter_ExampleStmtDetails
	@HashID BIGINT
AS
BEGIN
	SET NOCOUNT ON
	SELECT TOP 1
		us.NormText,
		us.OrigText, 
		s.ConnId,
		s.Session,
		s.Request,
		CONVERT(VARCHAR(30), s.StartTime, 121) AS StartTime,
		CONVERT(VARCHAR(30), s.EndTime, 121)   AS EndTime,
		s.Reads,
		s.Writes,
		s.CPU,
		s.Duration,
		(SELECT TOP 1 TraceFileName FROM ReadTrace.tblTraceFiles WHERE FirstSeqNumber <= [s].[StmtSeq] ORDER BY FirstSeqNumber DESC) AS [File]
	FROM ReadTrace.tblUniqueStatements us
	JOIN ReadTrace.tblStatements        s ON us.Seq = s.StmtSeq
   WHERE us.HashID = @HashID
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StmtDetails'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StmtDetails
GO

CREATE PROCEDURE ReadTrace.spReporter_StmtDetails 
	@HashID				BIGINT, 
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval    INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name


	SELECT 
		MIN(t.StartTime) AS StartTime,
		MIN(t.EndTime)   AS EndTime,
		t.TimeInterval,
		SUM(ISNULL(pa.StartingEvents, 0))  AS StartingEvents,
		SUM(ISNULL(pa.CompletedEvents, 0)) AS CompletedEvents,
/*		SUM(ISNULL(pa.AttentionEvents, 0)) */ 0 AS Attentions,
		SUM(ISNULL(pa.TotalDuration, 0)) AS Duration,
		SUM(ISNULL(pa.TotalCPU, 0)) AS CPU,
		SUM(ISNULL(pa.TotalReads, 0)) AS Reads,
		SUM(ISNULL(pa.TotalWrites, 0)) AS Writes
	 FROM ReadTrace.tblTimeIntervals t
LEFT JOIN ( 
            SELECT * 
			FROM ReadTrace.tblStmtPartialAggs 
			WHERE HashID = @HashID
--				  and DBID = ISNULL(@iDBID, DBID)
--				  and AppNameID = ISNULL(@iAppNameID, AppNameID)
--				  and LoginNameID = ISNULL(@iLoginNameID, LoginNameID)
		  ) AS pa  ON pa.TimeInterval = t.TimeInterval
   WHERE  t.TimeInterval >= @StartTimeInterval AND t.TimeInterval <= @EndTimeInterval
GROUP BY t.TimeInterval
ORDER BY t.TimeInterval
OPTION(RECOMPILE)
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StmtDetailsScaleFactor'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StmtDetailsScaleFactor
GO

CREATE PROCEDURE ReadTrace.spReporter_StmtDetailsScaleFactor 
	@HashID				BIGINT, 
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval	INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	DECLARE @MaxEventCount INT

	CREATE TABLE #StmtDetails (StartTime DATETIME, 	EndTime DATETIME, TimeInterval INT, StartingEvents INT, CompletedEvents INT, Attentions INT, Duration BIGINT, CPU BIGINT,Reads BIGINT, 	Writes BIGINT)

	-- Insert the aggregated batch information for the specified time window into a local temp table
	INSERT INTO #StmtDetails EXEC ReadTrace.spReporter_StmtDetails @HashID, @StartTimeInterval, @EndTimeInterval,@Filter1, @Filter2, @Filter3, @Filter4, @Filter1Name, @Filter2Name, @Filter3Name, @Filter4Name
	
	-- I want to make sure that I always chart starting & completed events on the same scale, so that if
	-- their is some divergence in the number (due to longer running queries, blocking, etc) that the two
	-- lines diverge and make this very obvious.  Therefore I get the max of either of these two and use it
	-- as input for scaling in the final query below
	SELECT @MaxEventCount = MAX(NumberOfEvents) 
	FROM (
		  SELECT MAX(StartingEvents)  AS NumberOfEvents FROM #StmtDetails
		  UNION ALL
		  SELECT MAX(CompletedEvents) AS NumberOfEvents FROM #StmtDetails
		 ) AS t

	SELECT 
		CASE WHEN @MaxEventCount   <= 100 THEN 1 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS StartingEventsScale,
		CASE WHEN @MaxEventCount   <= 100 THEN 1 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(@MaxEventCount)  END AS CompletedEventsScale,
		CASE WHEN MAX(Attentions)  <= 100 THEN 1 ELSE ReadTrace.fn_ReporterCalculateScaleFactor(MAX(Attentions)) END AS AttentionEventsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgDuration)) AS DurationScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgReads))    AS ReadsScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgWrites))   AS WritesScale,
		ReadTrace.fn_ReporterCalculateScaleFactor(MAX(a.AvgCPU))      AS CPUScale,
		MAX(a.StartingEvents)     AS MaxStartingEvents,
		MAX(a.CompletedEvents)    AS MaxCompletedEvents,
/*		MAX(a.Attentions) */ NULL AS MaxAttentionEvents,
		MAX(a.Duration) AS MaxDuration,
		MAX(a.Reads)    AS MaxReads,
		MAX(a.Writes)   AS MaxWrites,
		MAX(a.CPU)      AS MaxCPU
	FROM (SELECT 
			StartingEvents,
			CompletedEvents,
			Attentions,
			Duration,
			Reads,
			Writes,
			CPU,
			CASE WHEN CompletedEvents > 0 THEN CPU / CompletedEvents      ELSE NULL END AS AvgCPU,
			CASE WHEN CompletedEvents > 0 THEN Duration / CompletedEvents ELSE NULL END AS AvgDuration,
			CASE WHEN CompletedEvents > 0 THEN Reads / CompletedEvents    ELSE NULL END AS AvgReads,
			CASE WHEN CompletedEvents > 0 THEN Writes / CompletedEvents   ELSE NULL END AS AvgWrites
		 FROM #StmtDetails
		 ) AS a		 
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StmtDetailsMinMaxAvg'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StmtDetailsMinMaxAvg
GO

CREATE PROCEDURE ReadTrace.spReporter_StmtDetailsMinMaxAvg
	@HashID				BIGINT,
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval    INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT 
		MIN(s.MinReads) AS StmtMinReads,
		MAX(s.MaxReads) AS StmtMaxReads,
		SUM(s.TotalReads) / SUM(s.CompletedEvents) AS StmtAvgReads,
		SUM(s.TotalReads) AS StmtTotalReads,
		MIN(s.MinWrites) AS StmtMinWrites,
		MAX(s.MaxWrites) AS StmtMaxWrites,
		SUM(s.TotalWrites) / SUM(s.CompletedEvents) AS StmtAvgWrites,
		SUM(s.TotalWrites) AS StmtTotalWrites,
		MIN(s.MinCPU) AS StmtMinCPU,
		MAX(s.MaxCPU) AS StmtMaxCPU,
		SUM(s.TotalCPU) / SUM(s.CompletedEvents) AS StmtAvgCPU,
		SUM(s.TotalCPU) AS StmtTotalCPU,
		MIN(s.MinDuration) AS StmtMinDuration,
		MAX(s.MaxDuration) AS StmtMaxDuration,
		SUM(s.TotalDuration) / SUM(s.CompletedEvents) AS StmtAvgDuration,
		SUM(s.TotalDuration) AS StmtTotalDuration
	FROM ReadTrace.tblStmtPartialAggs s
	WHERE s.HashID = @HashID 
		AND s.TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval
		AND s.DBID        = ISNULL(@iDBID, s.DBID)
		AND s.AppNameID   = ISNULL(@iAppNameID, s.AppNameID)
		AND s.LoginNameID = ISNULL(@iLoginNameID, s.LoginNameID)
	OPTION (RECOMPILE)
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StmtDistinctPlans'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StmtDistinctPlans
GO

CREATE PROCEDURE ReadTrace.spReporter_StmtDistinctPlans 
	@HashID				BIGINT, 
	@StartTimeInterval  INT = NULL, 
	@EndTimeInterval    INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT
	DECLARE @plans_collected BIT
	DECLARE @query_has_no_plan BIT
	DECLARE @dtStart DATETIME, @dtEnd DATETIME

	SELECT @plans_collected = 0x1, @query_has_no_plan = 0x0
	-- Exit immediately if they didn't capture showplan/statistics profile
	IF NOT EXISTS(SELECT * FROM ReadTrace.tblTracedEvents WHERE EventID in (97, 98))
	BEGIN
		PRINT 'No plans collected'
		SET @plans_collected = 0x0
		GOTO exit_now
	END
	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT @dtStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval
	SELECT @dtEnd   = EndTime   FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	-- Then return the plan text, rows, executes information for each plan that was used, as well as 
	-- statistics about the number of times that plan was used, when it was first/last used, IO, CPU 
	-- and usage statistics, etc
		--		Azure does not support select into
		CREATE TABLE #temp
		(
			PlanHashID			BIGINT,
			Rows				BIGINT,
			Executes			BIGINT,
			StmtText			NVARCHAR(MAX),
			StmtID				INT,
			NodeID				SMALLINT,
			Parent				SMALLINT,
			PhysicalOp			VARCHAR(30),
			LogicalOp			VARCHAR(30),
			Argument			NVARCHAR(256),
			DefinedValues		NVARCHAR(256),
			EstimateRows		FLOAT,
			EstimateIO			FLOAT,
			EstimateCPU			FLOAT,
			AvgRowSize			INT,
			TotalSubtreeCost	FLOAT,
			OutputList			NVARCHAR(256),
			Warnings			VARCHAR(100),
			Type				VARCHAR(30),
			Parallel			TINYINT,
			EstimateExecutions	FLOAT,
			RowOrder			SMALLINT,

			--		Aggregates 
			PlanExecutes		BIGINT, 
			PlanFirstUsed		DATETIME, 
			PlanLastUsed		DATETIME,
			PlanMinReads		BIGINT,
			PlanMaxReads		BIGINT,
			PlanAvgReads		BIGINT,
			PlanTotalReads		BIGINT,
			PlanMinWrites		BIGINT,
			PlanMaxWrites		BIGINT,
			PlanAvgWrites		BIGINT,
			PlanTotalWrites		BIGINT,
			PlanMinCPU			BIGINT,
			PlanMaxCPU			BIGINT,
			PlanAvgCPU			BIGINT,
			PlanTotalCPU		BIGINT,
			PlanMinDuration		BIGINT,
			PlanMaxDuration		BIGINT,
			PlanAvgDuration		BIGINT,
			PlanTotalDuration	BIGINT,
			PlanAttnCount    	BIGINT
		)

	INSERT INTO #temp
	select upr.*,
				p.PlanExecutes, 
				p.PlanFirstUsed, 
				p.PlanLastUsed,
				p.PlanMinReads,
				p.PlanMaxReads,
				p.PlanAvgReads,
				p.PlanTotalReads,
				p.PlanMinWrites,
				p.PlanMaxWrites,
				p.PlanAvgWrites,
				p.PlanTotalWrites,
				p.PlanMinCPU,
				p.PlanMaxCPU,
				p.PlanAvgCPU,
				p.PlanTotalCPU,
				p.PlanMinDuration,
				p.PlanMaxDuration,
				p.PlanAvgDuration,
				p.PlanTotalDuration,
				p.PlanAttnCount
	FROM ReadTrace.tblUniquePlanRows upr
	JOIN (
			SELECT p.PlanHashID,
				COUNT_BIG(b.BatchSeq) AS PlanExecutes, 
				MIN(b.StartTime) AS PlanFirstUsed, 
				MAX(b.StartTime) AS PlanLastUsed,
				MIN(b.Reads) AS PlanMinReads,
				MAX(b.Reads) AS PlanMaxReads,
				AVG(b.Reads) AS PlanAvgReads,
				SUM(b.Reads) AS PlanTotalReads,
				MIN(b.Writes) AS PlanMinWrites,
				MAX(b.Writes) AS PlanMaxWrites,
				AVG(b.Writes) AS PlanAvgWrites,
				SUM(b.Writes) AS PlanTotalWrites,
				MIN(b.CPU) AS PlanMinCPU,
				MAX(b.CPU) AS PlanMaxCPU,
				AVG(b.CPU) AS PlanAvgCPU,
				SUM(b.CPU) AS PlanTotalCPU,
				MIN(b.Duration) AS PlanMinDuration,
				MAX(b.Duration) AS PlanMaxDuration,
				AVG(b.Duration) AS PlanAvgDuration,
				SUM(b.Duration) AS PlanTotalDuration,
				SUM(CASE WHEN b.AttnSeq IS NOT NULL THEN 1 ELSE 0 END) AS PlanAttnCount
			FROM ReadTrace.tblStatements b
	   LEFT JOIN ReadTrace.tblPlans      p ON p.StmtSeq = b.StmtSeq
		   WHERE b.HashID = @HashID AND b.StartTime >= @dtStart AND b.EndTime <= @dtEnd
		GROUP BY p.PlanHashID
		) AS p ON p.PlanHashID = upr.PlanHashID
	OPTION (RECOMPILE);

	-- Many types of statements may not generate a showplan (e.g., DECLARE, IF (scalar), SET, RETURN, ...)
	-- Still need to ensure that we return a row indicating there is no plan
	IF @@ROWCOUNT = 0
	BEGIN
		PRINT 'Query has no plan'
		SET @query_has_no_plan = 0x1
		GOTO exit_now
	END

	;WITH plan_hierarchy AS
	(
		SELECT *, 0 AS tree_level   FROM #temp t WHERE Parent IS NULL
		UNION ALL
		SELECT t.*, tree_level + 1 FROM #temp t 
								   JOIN plan_hierarchy p ON t.PlanHashID = p.PlanHashID AND t.Parent = p.NodeID
	)
	SELECT 
		@plans_collected AS fPlansCollected,
		@query_has_no_plan AS fQueryHasNoPlan,
		p.PlanHashID, 
		p.PlanExecutes,
		p.PlanFirstUsed, 
		p.PlanLastUsed,
		p.PlanMinReads,
		p.PlanMaxReads,
		p.PlanAvgReads,
		p.PlanTotalReads,
		p.PlanMinWrites,
		p.PlanMaxWrites,
		p.PlanAvgWrites,
		p.PlanTotalWrites,
		p.PlanMinCPU,
		p.PlanMaxCPU,
		p.PlanAvgCPU,
		p.PlanTotalCPU,
		p.PlanMinDuration,
		p.PlanMaxDuration,
		p.PlanAvgDuration,
		p.PlanTotalDuration,
		p.PlanAttnCount,
		p.Warnings,
		p.EstimateRows,
		p.EstimateExecutions,
		p.RowOrder,
		p.tree_level,
		CASE WHEN PATINDEX(N'%|--%', StmtText) > 0 THEN SUBSTRING(StmtText, PATINDEX(N'%|--%', StmtText) + 3, DATALENGTH(StmtText) - PATINDEX(N'%|--%', StmtText) - 3)
			 ELSE LTRIM(StmtText)
		END AS StmtText
	FROM plan_hierarchy p
	ORDER BY p.PlanExecutes DESC, p.PlanHashID, p.RowOrder
	RETURN;

exit_now:
	SELECT 
		@plans_collected AS fPlansCollected,
		@query_has_no_plan AS fQueryHasNoPlan,
		CAST(NULL AS BIGINT) AS PlanHashID, 
		CAST(NULL AS BIGINT) AS PlanExecutes,
		CAST(NULL AS DATETIME) AS PlanFirstUsed, 
		CAST(NULL AS DATETIME) AS PlanLastUsed,
		CAST(NULL AS BIGINT) AS PlanMinReads,
		CAST(NULL AS BIGINT) AS PlanMaxReads,
		CAST(NULL AS BIGINT) AS PlanAvgReads,
		CAST(NULL AS BIGINT) AS PlanTotalReads,
		CAST(NULL AS BIGINT) AS PlanMinWrites,
		CAST(NULL AS BIGINT) AS PlanMaxWrites,
		CAST(NULL AS BIGINT) AS PlanAvgWrites,
		CAST(NULL AS BIGINT) AS PlanTotalWrites,
		CAST(NULL AS BIGINT) AS PlanMinCPU,
		CAST(NULL AS BIGINT) AS PlanMaxCPU,
		CAST(NULL AS BIGINT) AS PlanAvgCPU,
		CAST(NULL AS BIGINT) AS PlanTotalCPU,
		CAST(NULL AS BIGINT) AS PlanMinDuration,
		CAST(NULL AS BIGINT) AS PlanMaxDuration,
		CAST(NULL AS BIGINT) AS PlanAvgDuration,
		CAST(NULL AS BIGINT) AS PlanTotalDuration,
		CAST(NULL AS BIGINT) AS PlanAttnCount,
		CAST(NULL AS VARCHAR(100)) AS Warnings,
		CAST(NULL AS FLOAT) AS EstimateRows,
		CAST(NULL AS FLOAT) AS EstimateExecutions,
		CAST(NULL AS INT) AS RowOrder,
		CAST(NULL AS INT) AS tree_level,
		CAST(NULL AS NVARCHAR(max)) AS StmtText
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StmtTopN'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StmtTopN
GO

CREATE PROCEDURE ReadTrace.spReporter_StmtTopN
	@StartTimeInterval	INT = NULL,
	@EndTimeInterval	INT = NULL,
	@TopN				INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	-- Possible filters to be applied
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @iDBID			INT

	IF @TopN IS NULL SET @TopN = 10

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			@StartTimeInterval OUTPUT,
			@EndTimeInterval OUTPUT,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	--	Use the row_number and ORDER BY's to get list the # of entries that match
	--	Since unique row only returned 1 time this works like a large set of unions
	SELECT *,
		ROW_NUMBER() OVER(ORDER BY CPU DESC) AS QueryNumber
	FROM (
		  SELECT a.HashID,
			SUM(CompletedEvents) AS Executes,
		    SUM(TotalCPU) AS CPU,
			SUM(TotalDuration) AS Duration,
			SUM(TotalReads) AS Reads,
			SUM(TotalWrites) AS Writes,
			SUM(AttentionEvents) AS Attentions, 
			(SELECT StartTime FROM ReadTrace.tblTimeIntervals i WHERE TimeInterval = @StartTimeInterval) AS [StartTime],
			(SELECT EndTime FROM ReadTrace.tblTimeIntervals i where TimeInterval = @EndTimeInterval) AS [EndTime],
			(SELECT CAST(NormText AS NVARCHAR(4000)) FROM ReadTrace.tblUniqueStatements s WHERE s.HashID = a.HashID) AS [NormText],
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalCPU) DESC) AS CPUDesc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalCPU) ASC) AS CPUAsc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalDuration) DESC) AS DurationDesc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalDuration) ASC) AS DurationAsc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalReads) DESC) AS ReadsDesc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalReads) ASC) AS ReadsAsc,
				ROW_NUMBER() OVER(ORDER BY SUM(TotalWrites) DESC) AS WritesDesc,
		       	ROW_NUMBER() OVER(ORDER BY SUM(TotalWrites) ASC) AS WritesAsc
			FROM ReadTrace.tblStmtPartialAggs a
		   WHERE TimeInterval BETWEEN @StartTimeInterval AND @EndTimeInterval
--				 AND a.AppNameID = ISNULL(@iAppNameID, a.AppNameID)
--				 AND a.LoginNameID = ISNULL(@iLoginNameID, a.LoginNameID)
				 AND a.DBID = ISNULL(@iDBID, a.DBID)
			GROUP BY a.HashID
		       ) AS Outcome
		WHERE 	(  
		       CPUDesc      <= @TopN 
			or CPUAsc		<= @TopN
			or DurationDesc <= @TopN 
			or DurationAsc  <= @TopN
			or ReadsDesc	<= @TopN 
			or ReadsAsc		<= @TopN
			or WritesDesc	<= @TopN 
			or WritesAsc	<= @TopN
			)
		ORDER BY CPU DESC
		OPTION (RECOMPILE)
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_GetModulesContainingStatement'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_GetModulesContainingStatement
GO

CREATE PROCEDURE ReadTrace.spReporter_GetModulesContainingStatement
	@HashID			   BIGINT,
	@StartTimeInterval INT = NULL,
	@EndTimeInterval   INT = NULL,
	@Filter1		NVARCHAR(256) = NULL,
	@Filter2		NVARCHAR(256) = NULL,
	@Filter3		NVARCHAR(256) = NULL,
	@Filter4		NVARCHAR(256) = NULL,
	@Filter1Name	NVARCHAR(64)  = NULL,
	@Filter2Name	NVARCHAR(64)  = NULL,
	@Filter3Name	NVARCHAR(64)  = NULL,
	@Filter4Name	NVARCHAR(64)  = NULL
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @iDBID			INT
	DECLARE @iAppNameID		INT
	DECLARE @iLoginNameID	INT
	DECLARE @dtEventStart DATETIME, @dtEventEnd DATETIME
	DECLARE @crlf NVARCHAR(2)
	DECLARE @cmd NVARCHAR(MAX)

	SELECT @crlf = CHAR(13) + CHAR(10)

	IF (@StartTimeInterval IS NULL)
	BEGIN
		SELECT @StartTimeInterval = MIN(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
	SELECT @dtEventStart = StartTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @StartTimeInterval

	IF (@EndTimeInterval IS NULL)
	BEGIN
		SELECT @EndTimeInterval = MAX(TimeInterval) FROM ReadTrace.tblTimeIntervals
	END
	SELECT @dtEventEnd = EndTime FROM ReadTrace.tblTimeIntervals WHERE TimeInterval = @EndTimeInterval

	EXEC ReadTrace.spReporter_DetermineFilterValues 
			NULL,
			NULL,
			@iDBID OUTPUT,
			@iAppNameID OUTPUT,
			@iLoginNameID OUTPUT,
			@Filter1,
			@Filter2,
			@Filter3,
			@Filter4,
			@Filter1Name,
			@Filter2Name,
			@Filter3Name,
			@Filter4Name

	SELECT @cmd = N'SELECT 
					DISTINCT UPPER(p.Name) AS ModuleName
					FROM ReadTrace.tblProcedureNames p
					JOIN ReadTrace.tblStatements     s on p.DBID = s.DBID AND p.ObjectID = s.ObjectID'

	IF @iAppNameID IS NOT NULL OR @iLoginNameID IS NOT NULL
	BEGIN
		SELECT @cmd = @cmd + @crlf + N'	JOIN ReadTrace.tblConnections c ON s.ConnSeq = c.ConnSeq AND s.Session = c.Session'

		IF @iAppNameID IS NOT NULL
			SELECT @cmd = @cmd + @crlf + N'	join ReadTrace.tblUniqueAppNames ua ON ua.AppName = c.ApplicationName'

		IF @iLoginNameID IS NOT NULL
			SELECT @cmd = @cmd + @crlf + N'	join ReadTrace.tblUniqueLoginNames ul ON ul.LoginName = c.LoginName'
	END

	SELECT @cmd = @cmd + @crlf + N'WHERE s.HashID = @HashID
		AND COALESCE(s.EndTime, s.StartTime) BETWEEN @dtEventStart AND @dtEventEnd'

	IF @iDBID IS NOT NULL
		SELECT @cmd = @cmd + @crlf + N'		AND s.DBID = @iDBID'

	IF @iAppNameID IS NOT NULL
		SELECT @cmd = @cmd + @crlf + N'		AND ua.iID = @iAppNameID'

	IF @iLoginNameID IS NOT NULL
		SELECT @cmd = @cmd + @crlf + N'		AND ul.iID = @iLoginNameID'
		
	EXEC sp_executesql @cmd, 
		N'@HashID BIGINT, @dtEventStart DATETIME, @dtEventEnd DATETIME, @iDBID INT, @iAppNameID INT, @iLoginNameID int',
		@HashID,
		@dtEventStart,
		@dtEventEnd,
		@iDBID,
		@iAppNameID,
		@iLoginNameID
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_DTASample'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_DTASample
GO

--	ReadTrace.spReporter_DTASample 3492714307520456998
CREATE PROCEDURE ReadTrace.spReporter_DTASample
	@HashID BIGINT
AS
BEGIN
	--	SELECT * FROM ReadTrace.tblBatches
	--		TODO: May need to wind this together with connection options
	SELECT TextData
	FROM
		(SELECT TextData ,
				Duration,
				ROW_NUMBER() OVER(ORDER BY [Duration] DESC) AS DurationMaxRank,
				ROW_NUMBER() OVER(ORDER BY [Duration] ASC) AS DurationMinRank,
				ROW_NUMBER() OVER(ORDER BY [CPU] DESC) AS CPUMaxRank,
				ROW_NUMBER() OVER(ORDER BY [CPU] ASC) AS CPUMinRank,
				ROW_NUMBER() OVER(ORDER BY [Reads] DESC) AS ReadsMaxRank,
				ROW_NUMBER() OVER(ORDER BY [Reads] ASC) AS ReadsMinRank,
				ROW_NUMBER() OVER(ORDER BY [Writes] DESC) AS WritesMaxRank,
				ROW_NUMBER() OVER(ORDER BY [Writes] ASC) AS WritesMinRank
			FROM ReadTrace.tblBatches
			WHERE HashID = @HashID and TextData IS NOT NULL
		) AS Outcome
	WHERE (      DurationMaxRank<= 5
			  or DurationMinRank<= 5
			  or CPUMaxRank		<= 5
			  or CPUMinRank		<= 5
			  or ReadsMaxRank	<= 5
			  or ReadsMinRank	<= 5
			  or WritesMaxRank	<= 5
			  or WritesMinRank	<= 5
		  )
	UNION ALL
	SELECT TextData 
	FROM ReadTrace.tblBatches
	TABLESAMPLE (2 PERCENT)
	REPEATABLE (205)		
	WHERE HashID = @HashID AND TextData IS NOT NULL	
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_BatchISQL'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_BatchISQL
GO

CREATE PROCEDURE ReadTrace.spReporter_BatchISQL
	@HashID BIGINT
AS
BEGIN
	SELECT OrigText 
	FROM ReadTrace.tblUniqueBatches
	WHERE HashID = @HashID
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_StatementISQL'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_StatementISQL
GO


CREATE PROCEDURE ReadTrace.spReporter_StatementISQL
	@HashID BIGINT
AS
BEGIN
	SELECT OrigText 
	FROM ReadTrace.tblUniqueStatements
	WHERE HashID = @HashID
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_Compare_Overview_BatchUniqueHashInfo'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_Compare_Overview_BatchUniqueHashInfo
GO

CREATE PROCEDURE ReadTrace.spReporter_Compare_Overview_BatchUniqueHashInfo
AS
BEGIN
	SET NOCOUNT ON

	SELECT 'Matching' as [Desc],
			COUNT_BIG(*) as [Count]
   	  FROM ReadTrace.tblUniqueBatches		 b
INNER JOIN ReadTraceCompare.tblUniqueBatches c 	ON b.HashID = c.HashID
	UNION ALL
	SELECT 'BO' as [Desc],
			COUNT_BIG(*) 
	       FROM ReadTrace.tblUniqueBatches        b
LEFT OUTER JOIN ReadTraceCompare.tblUniqueBatches c ON b.HashID = c.HashID
		  WHERE c.HashID IS NULL
	UNION ALL
	SELECT 'CO' as [Desc],
			COUNT_BIG(*) 
    		FROM ReadTrace.tblUniqueBatches        b
RIGHT OUTER JOIN ReadTraceCompare.tblUniqueBatches c ON b.HashID = c.HashID
		   WHERE b.HashID IS NULL
	ORDER BY [Desc]
END
GO

----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_Overview_Counts'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_Overview_Counts
GO

-- ReadTrace.spReporter_Overview_Counts 'TotalReads'
CREATE PROCEDURE ReadTrace.spReporter_Overview_Counts
		@strColumn	SYSNAME
AS
BEGIN
	SET NOCOUNT ON
	DECLARE @strCmd		NVARCHAR(MAX)

	SET @strCmd = 
	'SELECT	''B'' as [Type],SUM([b.@strColumn]) as [Value]
	 FROM ReadTrace.tblComparisonBatchPartialAggs
	 WHERE [b.HashID] IS NOT NULL and [c.HashID] IS NOT NULL
	 
	 UNION ALL
	 SELECT	''C'',SUM([c.@strColumn])
	 FROM ReadTrace.tblComparisonBatchPartialAggs
	 WHERE [b.HashID] IS NOT NULL and [c.HashID] IS NOT NULL

	 UNION ALL
	 SELECT	 ''BO'',SUM([b.@strColumn]) 
	 FROM ReadTrace.tblComparisonBatchPartialAggs
	 WHERE [c.HashID] IS NULL 

	 UNION ALL 
	 SELECT	''CO'',SUM([c.@strColumn]) 
	 FROM ReadTrace.tblComparisonBatchPartialAggs
	 WHERE [b.HashID] IS NULL '

	SET @strCmd = REPLACE(@strCmd, '@strColumn', @strColumn);
	EXEC sp_executesql @strCmd
END
GO
----------------------------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_Compare_TopN'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_Compare_TopN
GO

/*
 *	PROCEDURE: ReadTrace.spReporter_Compare_TopN
 *
 *	PURPOSE:
 *	When running Reporter in comparison mode, this procedure is called to find the queries
 *	that ran ONLY in the baseline database or ONLY in the comparison database, then do a TOP N
 *	over those to see which were the most expensive queries unique to a given workload
 *
 *	PARAMETERS:
 *		@strDBSchema		the schema where we want to find the queries that only exist there and not the other workload
 *								Ex: If strDBSchema is 'ReadTrace' (baseline) then return queries which only ran in the baseline DB
 *								Ex: If strDBSchema is 'ReadTraceCompare' (comparison) then return queries which only ran in the comparison DB
 *		@TopN				Limit to top N queries of each category (reads/writes/cpu/duration)
 *
 *	NOTES:
 *	This is expected to be called from the context of the baseline database (that is the DB we are connected
 *	to when running the ReadTrace_CompareMain report).
 *
 */ 
CREATE PROCEDURE ReadTrace.spReporter_Compare_TopN
	@strDBSchema SYSNAME,
	@TopN INT = NULL								-- limit result set to this number of queries per tree-level in the hierarchy
AS
BEGIN
	SET NOCOUNT ON

	DECLARE @strCmd		NVARCHAR(MAX)
	DECLARE @strSchemaWhereNotRun SYSNAME;

	IF @strDBSchema not in ('ReadTrace', 'ReadTraceCompare')
	BEGIN		
		RAISERROR('Schema name must either be ''ReadTrace'' (baseline) or ''ReadTraceCompare'' (comparison)', 16, 1);
		RETURN 0;
	END
	
	IF @TopN IS NULL SET @TopN = 10

	SELECT @strSchemaWhereNotRun = CASE WHEN @strDBSchema = 'ReadTrace' THEN 'ReadTraceCompare' 
										ELSE 'ReadTrace'
								END;

	
	SET @strCmd = 

	'SELECT 
		ROW_NUMBER() OVER(ORDER BY [CPU] DESC, HashID ASC) AS QueryNumber,Executes, HashID, Text, CPU, Duration, Reads, Writes 
	FROM 
	(
		SELECT * FROM
		(
			SELECT 
				*,
				ROW_NUMBER() OVER(ORDER BY CPU DESC) AS CPURank,
				ROW_NUMBER() OVER(ORDER BY Reads DESC) AS ReadsRank,
				ROW_NUMBER() OVER(ORDER BY Writes DESC) AS WritesRank,
				ROW_NUMBER() OVER(ORDER BY Duration DESC) AS DurationRank
			FROM
				(
					-- Aggregate detail data all those queries that appeared in one workload/schema
					-- but don''t appear in tblUniqueBatches of the other schema (i.e. they didn''t run over there)
					SELECT pa.[HashID] AS [HashID],
						(SELECT NormText FROM @strDBSchema.tblUniqueBatches s WHERE s.HashID = pa.[HashID]) AS [Text],
						SUM(pa.CompletedEvents) AS [Executes],
						SUM(pa.TotalCPU) AS [CPU],
						SUM(pa.TotalDuration) AS [Duration],
						SUM(pa.TotalReads) AS [Reads],
						SUM(pa.TotalWrites) AS [Writes]
						FROM @strDBSchema.tblBatchPartialAggs pa
				   LEFT JOIN @strSchemaWhereNotRun.tblUniqueBatches ub on pa.HashID = ub.HashID
					   WHERE ub.HashID IS NULL
					   GROUP BY pa.HashID
				) AS Outcome
		) AS t
		WHERE
			CPURank    <= @TopN
		OR  ReadsRank  <= @TopN
		OR  WritesRank <= @TopN
		OR  DurationRank <= @TopN

	) AS Final
	ORDER BY [CPU] DESC, HashID'

	SET @strCmd = REPLACE(@strCmd, '@strDBSchema', @strDBSchema);
	SET @strCmd = REPLACE(@strCmd, '@strSchemaWhereNotRun', @strSchemaWhereNotRun);
	
	--print @strCmd
	EXEC sp_executesql @strCmd, N'@TopN int', @TopN

END
GO

----------------------------------------------------------------------------------------------
--	The Diff is calculated as TotalActual - (projected total)
--	so the ones we want are when the value is positive where
--	the projected from the baseline is less than actual used
IF OBJECTPROPERTY(OBJECT_ID('ReadTrace.spReporter_Overview_TopN'), 'IsProcedure') = 1
	DROP PROCEDURE ReadTrace.spReporter_Overview_TopN
GO


-- ReadTrace.spReporter_Overview_TopN  
CREATE PROCEDURE ReadTrace.spReporter_Overview_TopN
		@TopN INT = NULL								-- limit result set to this number of queries per tree-level in the hierarchy
AS
BEGIN
	SET NOCOUNT ON
	IF @TopN IS NULL SET @TopN = 10

	SELECT Outcome.HashID,
			Outcome.[b.CompletedEvents],
			Outcome.[c.CompletedEvents],
			Outcome.[b.TotalCPU],
			Outcome.[c.TotalCPU],
			Outcome.[b.TotalReads],
			Outcome.[c.TotalReads],
			Outcome.[b.TotalWrites],
			Outcome.[c.TotalWrites],
			Outcome.[b.TotalDuration],
			Outcome.[c.TotalDuration],
			ProjectedCPUDiff,
			ProjectedReadsDiff,
			ProjectedWritesDiff,
			ProjectedDurationDiff,
			ActualEventDiff,
			ROW_NUMBER() OVER(ORDER BY ProjectedCPUDiff DESC) AS QueryNumber,
			u.NormText
	FROM (	SELECT 
				[b.HashID] AS HashID,
				[b.CompletedEvents],
				[c.CompletedEvents],
				[b.TotalCPU],
				[c.TotalCPU],
				[b.TotalReads],
				[c.TotalReads],
				[b.TotalWrites],
				[c.TotalWrites],
				[b.TotalDuration],
				[c.TotalDuration],
				([c.CompletedEvents] - [b.CompletedEvents]) AS [ActualEventDiff],
				ProjectedCPUDiff,
				ProjectedReadsDiff,
				ProjectedWritesDiff,
				ProjectedDurationDiff,
				ROW_NUMBER() OVER(ORDER BY ProjectedCPUDiff DESC) AS CPUDesc,
				ROW_NUMBER() OVER(ORDER BY ProjectedReadsDiff DESC) AS ReadsDesc,
				ROW_NUMBER() OVER(ORDER BY ProjectedWritesDiff DESC) AS WritesDesc,
				ROW_NUMBER() OVER(ORDER BY ProjectedDurationDiff DESC) AS DurationDesc,
				ROW_NUMBER() OVER(ORDER BY ABS(([c.CompletedEvents] - [b.CompletedEvents])) DESC) AS EventDesc
			FROM ReadTrace.tblComparisonBatchPartialAggs
			WHERE [b.HashID] IS NOT NULL AND [c.HashID] IS NOT NULL
		) AS Outcome 
INNER JOIN ReadTrace.tblUniqueBatches u ON u.HashID = Outcome.HashID
	 WHERE
			CPUDesc    <= @TopN
		OR	ReadsDesc  <= @TopN
		OR	WritesDesc <= @TopN
		OR	DurationDesc <= @TopN
		OR	EventDesc  <= @TopN		
		ORDER BY QueryNumber	--	Default ordering
END