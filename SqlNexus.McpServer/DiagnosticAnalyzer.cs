using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace SqlNexus.McpServer
{
    public class DiagnosticAnalyzer
    {
        private readonly string _connectionString;

        public DiagnosticAnalyzer(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Top 50 longest-running queries by duration (aggregate stats)
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetTopQueriesByDuration(int topN = 50)
        {
            string query = $@"
                SELECT TOP {topN} 
                    SUM(b.Duration)/1000 Duration_ms, 
                    SUM(b.CPU) CPU_ms, 
                    SUM(b.Duration)/1000 - SUM(b.CPU) WaitTime_ms, 
                    CONVERT(DECIMAL(8,2), (((SUM(b.Duration)/1000.00) - SUM(b.cpu))/(CASE WHEN SUM(b.Duration)/1000 = 0 THEN 1 ELSE SUM(b.Duration)/1000 END )))*100 WaitPercentage, 
                    SUM(b.Reads) Reads, 
                    COUNT(*) Executions, 
                    (SUM(b.Duration)/1000)/ (CASE WHEN COUNT(*) = 0 THEN 1 ELSE COUNT(*) END) AvgDuration, 
                    SUM(b.CPU)/COUNT(*) AvgCPU, 
                    SUBSTRING(ub.NormText, 1, 500) NormText, 
                    b.HashID
                FROM ReadTrace.tblBatches b 
                JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                GROUP BY ub.NormText, b.HashID
                ORDER BY Duration_ms DESC";

            return ExecuteQueryAndReturnJson(query, "Top Queries by Duration");
        }

        /// <summary>
        /// Get CPU usage - is there high CPU on this system?
        /// Queries per-sample CPU rows from CounterData (or tbl_SQL_CPU_HEALTH fallback),
        /// then computes aggregate summary in C# from the same rows.
        /// </summary>
        public string AnalyzeCpuUsage()
        {
            // SET ANSI_NULLS OFF: if @inst_index is NULL, the join on det.InstanceIndex = @inst_index
            // would match nothing under standard ANSI nulls — this matches the RDL DataSet behaviour.
            const string samplesQuery = @"
                SET ANSI_NULLS OFF;

                IF OBJECT_ID('dbo.tbl_ServerProperties') IS NOT NULL AND OBJECT_ID('dbo.CounterData') IS NOT NULL
                BEGIN
                    DECLARE @process_id INT = 0, @cpu_count INT = 1, @inst_name VARCHAR(64), @inst_index INT;

                    SELECT @process_id = sp.PropertyValue FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'ProcessID';
                    SELECT @cpu_count  = CASE WHEN sp.PropertyValue = 0 THEN 1 ELSE sp.PropertyValue END
                    FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'cpu_count';

                    SELECT TOP 1 @inst_name = cdet.InstanceName, @inst_index = cdet.InstanceIndex
                    FROM dbo.CounterData ctr JOIN dbo.CounterDetails cdet ON ctr.CounterID = cdet.CounterID
                    WHERE cdet.ObjectName = 'Process' AND cdet.CounterName LIKE 'ID Process'
                        AND cdet.InstanceName LIKE 'sqlservr%' AND ctr.CounterValue = @process_id;

                    SELECT
                        CONVERT(DATETIME, sql_cpu.CounterDateTime)                                                                               AS sample_time,
                        CASE WHEN sql_cpu.sql_cpu_pct > os_cpu.total_cpu_pct THEN os_cpu.total_cpu_pct ELSE sql_cpu.sql_cpu_pct END              AS sql_cpu_pct,
                        os_cpu.total_cpu_pct
                            - CASE WHEN sql_cpu.sql_cpu_pct > os_cpu.total_cpu_pct THEN os_cpu.total_cpu_pct ELSE sql_cpu.sql_cpu_pct END        AS nonsql_cpu_pct,
                        os_cpu.system_idle_pct
                    FROM (
                        SELECT ctr.CounterDateTime, ctr.RecordIndex,
                               CONVERT(INT, (FLOOR(ctr.CounterValue) / (100 * @cpu_count)) * 100) AS sql_cpu_pct
                        FROM dbo.CounterData ctr JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
                        WHERE det.ObjectName = 'Process' AND det.CounterName LIKE '[%] Processor Time'
                              AND det.InstanceName = @inst_name AND det.InstanceIndex = @inst_index
                    ) AS sql_cpu
                    INNER JOIN (
                        SELECT ctr.CounterDateTime, ctr.RecordIndex,
                               FLOOR(ctr.CounterValue)        AS total_cpu_pct,
                               100 - FLOOR(ctr.CounterValue)  AS system_idle_pct
                        FROM dbo.CounterData ctr JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
                        WHERE det.ObjectName = 'Processor Information' AND det.CounterName LIKE '[%] Processor Time'
                              AND det.InstanceName = '_Total'
                    ) AS os_cpu ON sql_cpu.RecordIndex = os_cpu.RecordIndex
                    ORDER BY sql_cpu.CounterDateTime;
                END
                ELSE IF OBJECT_ID('dbo.tbl_SQL_CPU_HEALTH') IS NOT NULL
                BEGIN
                    SELECT
                        EventTime                                                                    AS sample_time,
                        sql_cpu_utilization                                                          AS sql_cpu_pct,
                        100 - sql_cpu_utilization - ISNULL(system_idle_cpu, 0)                     AS nonsql_cpu_pct,
                        ISNULL(system_idle_cpu, 0)                                                  AS system_idle_pct
                    FROM dbo.tbl_SQL_CPU_HEALTH
                    WHERE EventTime IS NOT NULL
                    ORDER BY EventTime;
                END";

            var samplesTable = ExecuteQueryToDataTable(samplesQuery);
            var samples      = ConvertDataTableToList(samplesTable);

            // Compute aggregate stats from the per-sample rows
            object perfmonSummary;
            if (samples.Count > 0)
            {
                double maxSql = 0, sumSql = 0, maxTotal = 0, sumTotal = 0;
                int aboveSql70 = 0, aboveTotal70 = 0;
                DateTime? start = null, end = null;

                // Consecutive high-CPU run detection (threshold: 70%, minimum run: 3 samples)
                const int    highCpuThreshold   = 70;
                const int    minConsecutive      = 3;
                var          highCpuRuns         = new List<object>();
                int          currentRunLen       = 0;
                DateTime?    currentRunStart     = null;
                double       currentRunPeak      = 0;

                foreach (var row in samples)
                {
                    double sqlPct   = row["sql_cpu_pct"]    != null ? Convert.ToDouble(row["sql_cpu_pct"])     : 0;
                    double idlePct  = row["system_idle_pct"]!= null ? Convert.ToDouble(row["system_idle_pct"]) : 0;
                    double totalPct = 100 - idlePct;

                    if (sqlPct   > maxSql)   maxSql   = sqlPct;
                    if (totalPct > maxTotal) maxTotal = totalPct;
                    sumSql   += sqlPct;
                    sumTotal += totalPct;
                    if (sqlPct   > highCpuThreshold) aboveSql70++;
                    if (totalPct > highCpuThreshold) aboveTotal70++;

                    DateTime? sampleTime = row["sample_time"] != null ? Convert.ToDateTime(row["sample_time"]) : (DateTime?)null;
                    if (sampleTime != null)
                    {
                        if (start == null || sampleTime < start) start = sampleTime;
                        if (end   == null || sampleTime > end)   end   = sampleTime;
                    }

                    // Track consecutive SQL CPU above threshold
                    if (sqlPct > highCpuThreshold)
                    {
                        currentRunLen++;
                        if (currentRunLen == 1) currentRunStart = sampleTime;
                        if (sqlPct > currentRunPeak) currentRunPeak = sqlPct;
                    }
                    else
                    {
                        if (currentRunLen >= minConsecutive)
                        {
                            highCpuRuns.Add(new
                            {
                                run_start        = currentRunStart,
                                run_end          = sampleTime,   // first sample that broke the run
                                consecutive_samples = currentRunLen,
                                peak_sql_cpu_pct = (int)currentRunPeak
                            });
                        }
                        currentRunLen  = 0;
                        currentRunPeak = 0;
                        currentRunStart = null;
                    }
                }
                // Flush any run that extends to the last sample
                if (currentRunLen >= minConsecutive)
                {
                    highCpuRuns.Add(new
                    {
                        run_start           = currentRunStart,
                        run_end             = end,
                        consecutive_samples = currentRunLen,
                        peak_sql_cpu_pct    = (int)currentRunPeak
                    });
                }

                perfmonSummary = new
                {
                    max_sql_cpu_pct               = (int)maxSql,
                    avg_sql_cpu_pct               = Math.Round(sumSql   / samples.Count, 1),
                    max_total_cpu_pct             = (int)maxTotal,
                    avg_total_cpu_pct             = Math.Round(sumTotal / samples.Count, 1),
                    samples_sql_above_70pct       = aboveSql70,
                    samples_total_above_70pct     = aboveTotal70,
                    total_samples                 = samples.Count,
                    sample_start                  = start,
                    sample_end                    = end,
                    sustained_high_cpu_runs       = highCpuRuns,   // runs of 3+ consecutive samples above 70%
                    sustained_high_cpu_detected   = highCpuRuns.Count > 0
                };
            }
            else
            {
                perfmonSummary = new { message = "No Perfmon or ring buffer CPU data available" };
            }

            var result = new
            {
                summary             = "CPU Analysis - Is CPU High?",
                perfmon_cpu_summary = perfmonSummary,
                cpu_samples         = samples
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        /// <summary>
        /// Top CPU consuming queries
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
		public string GetTopCpuQueries(int topN = 20)
		{
			string query = $@"
				IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
				BEGIN
					DECLARE @cap_ms decimal(19,4) =
						CAST(COALESCE((SELECT TRY_CONVERT(int, PropertyValue) FROM tbl_ServerProperties WHERE PropertyName = 'cpu_count'), 1) AS decimal(19,4))
						* CAST(COALESCE(DATEDIFF(MILLISECOND, (SELECT MIN(StartTime) FROM ReadTrace.tblBatches), (SELECT MAX(EndTime) FROM ReadTrace.tblBatches)), 0) AS decimal(19,4));

					SELECT TOP {topN}
						SUM(b.CPU)                                                                       AS total_cpu_ms,
						CONVERT(decimal(5,2), COALESCE((SUM(b.CPU) * 100.0) / NULLIF(@cap_ms, 0), 0))  AS pct_of_cpu_capacity,
						SUM(b.Duration)/1000                                                             AS total_duration_ms,
						SUM(b.Reads)                                                                     AS total_physical_reads,
						SUM(b.Writes)                                                                    AS total_writes,
						COUNT(*)                                                                         AS executions,
						SUM(b.CPU) / NULLIF(COUNT(*), 0)                                                 AS avg_cpu_ms,
						SUBSTRING(ub.NormText, 1, 500)                                                   AS stmt_text
					FROM ReadTrace.tblBatches b
					JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
					WHERE b.CPU IS NOT NULL AND b.Duration IS NOT NULL
					GROUP BY ub.NormText, b.HashID
					ORDER BY total_cpu_ms DESC
				END
				ELSE IF OBJECT_ID('dbo.tbl_Hist_Top10_CPU_Queries_ByQueryHash') IS NOT NULL
				BEGIN
					DECLARE @cap_ms2 decimal(19,4) =
							CAST(COALESCE((SELECT TRY_CONVERT(int, PropertyValue) FROM tbl_ServerProperties WHERE PropertyName = 'cpu_count'), 1) AS decimal(19,4))
							* CAST(COALESCE(
								CASE WHEN OBJECT_ID('dbo.tbl_RUNTIMES') IS NOT NULL
									THEN DATEDIFF(MILLISECOND, (SELECT MIN(runtime) FROM tbl_RUNTIMES), (SELECT MAX(runtime) FROM tbl_RUNTIMES))
									ELSE 0
								END, 0) AS decimal(19,4));

					-- Delta between first and last snapshot isolates CPU used only during the collection window,
					-- rather than cumulative totals since SQL Server startup.
					WITH
						first_snap AS (SELECT * FROM tbl_Hist_Top10_CPU_Queries_ByQueryHash WHERE runtime = (SELECT MIN(runtime) FROM tbl_Hist_Top10_CPU_Queries_ByQueryHash)),
						last_snap  AS (SELECT * FROM tbl_Hist_Top10_CPU_Queries_ByQueryHash WHERE runtime = (SELECT MAX(runtime) FROM tbl_Hist_Top10_CPU_Queries_ByQueryHash))
					SELECT
						(l.total_worker_time - COALESCE(f.total_worker_time, 0)) / 1000                                                                          AS delta_cpu_ms,
						CONVERT(decimal(5,2), COALESCE(((l.total_worker_time - COALESCE(f.total_worker_time, 0)) / 1000.0 * 100.0) / NULLIF(@cap_ms2, 0), 0))   AS pct_of_cpu_capacity,
						(l.total_worker_time - COALESCE(f.total_worker_time, 0)) / 1000
							/ NULLIF(l.execution_count - COALESCE(f.execution_count, 0), 0)                                                                      AS avg_cpu_ms,
						l.execution_count - COALESCE(f.execution_count, 0)                                                                                       AS delta_executions,
						l.total_logical_reads - COALESCE(f.total_logical_reads, 0)                                                                               AS delta_logical_reads,
						l.sample_statement_text
					FROM last_snap l
					LEFT JOIN first_snap f ON l.query_hash = f.query_hash
					ORDER BY delta_cpu_ms DESC
				END";

			return ExecuteQueryAndReturnJson(query, $"Top {topN} CPU Consuming Queries");
		}

        /// <summary>
        /// Find I/O delays - is I/O slow?
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string AnalyzeIoPerformance(decimal thresholdMs = 20.0m)
        {
            string query = $@"
                DECLARE @IO_threshold DECIMAL(12, 3) = {thresholdMs};

                IF ((OBJECT_ID('dbo.CounterData') IS NOT NULL))
                BEGIN
                    SELECT 
                        CONVERT(DATETIME, dat.CounterDateTime) AS CounterDateTime,
                        CONVERT(DECIMAL(10,3), dat.CounterValue) AS DiskSec_Per_Transfer,
                        dl.ObjectName,
                        dl.CounterName,
                        dl.InstanceName AS DiskVolume
                    FROM dbo.CounterData dat
                    INNER JOIN dbo.CounterDetails dl ON dat.CounterID = dl.CounterID
                    WHERE dl.ObjectName IN ('logicaldisk')
                        AND dl.CounterName IN ('Avg. Disk sec/Transfer')
                        AND dl.InstanceName <> '_Total'
                        AND dat.CounterValue >= @IO_threshold
                    ORDER BY dat.CounterValue DESC;
                END";

            return ExecuteQueryAndReturnJson(query, $"I/O Performance Analysis (Threshold: {thresholdMs}ms)");
        }

        /// <summary>
        /// Find I/O waits inside SQL Server
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string AnalyzeIoWaits()
        {
            const string query = @"
                DECLARE @minruntime DATETIME, @maxruntime DATETIME, @cpu_count INT;

                SELECT @minruntime = MIN(runtime), @maxruntime = MAX(runtime) FROM tbl_OS_WAIT_STATS;
                SELECT @cpu_count = PropertyValue FROM tbl_ServerProperties WHERE PropertyName = 'cpu_count';

                SELECT 
                    a.wait_type, 
                    (b.wait_time_ms - a.wait_time_ms) AS TotalWait_ms_AcrossAllCPUs, 
                    DATEDIFF(SECOND, a.runtime, b.runtime) AS CollectionTime_sec, 
                    (b.wait_time_ms - a.wait_time_ms) / (DATEDIFF(SECOND, a.runtime, b.runtime) * @cpu_count) AS WaitTime_ms_per_second_per_cpu
                FROM (SELECT * FROM tbl_OS_WAIT_STATS WHERE runtime = @minruntime) AS a
                INNER JOIN (SELECT * FROM tbl_OS_WAIT_STATS WHERE runtime = @maxruntime) AS b ON a.wait_type = b.wait_type
                WHERE a.wait_type LIKE 'PAGEIOLATCH_%' 
                    OR a.wait_type = 'WRITELOG' 
                    OR a.wait_type = 'LOGBUFFER'
                    OR a.wait_type = 'IO_COMPLETION'
                    OR a.wait_type = 'ASYNC_IO_COMPLETION'
                ORDER BY TotalWait_ms_AcrossAllCPUs DESC";

            return ExecuteQueryAndReturnJson(query, "I/O Wait Statistics - SQL Server Internal");
        }

        /// <summary>
        /// Overall wait statistics (Bottleneck Analysis)
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string AnalyzeWaitStats()
        {
            const string query = @"
                IF OBJECT_ID('DataSet_WaitStats_WaitStatsTop5Categories') IS NOT NULL 
                  AND OBJECT_ID('tbl_OS_WAIT_STATS') IS NOT NULL
                BEGIN
                   EXEC DataSet_WaitStats_WaitStatsTop5Categories
                END";

            return ExecuteQueryAndReturnJson(query, "Overall Wait Statistics - Bottleneck Analysis");
        }

        /// <summary>
        /// Find blocking chains
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string AnalyzeBlocking()
        {
            const string query = @"
                SELECT TOP 50
                    runtime, 
                    head_blocker_session_id, 
                    head_blocker_proc_name,
                    SUBSTRING(stmt_text, 1, 200) AS head_blocker_stmt, 
                    blocked_task_count, 
                    tot_wait_duration_ms AS blocked_total_wait_dur_ms, 
                    avg_wait_duration_ms AS blocked_avg_wait_dur_ms 
                FROM tbl_HEADBLOCKERSUMMARY
                ORDER BY blocked_total_wait_dur_ms DESC, runtime";

            return ExecuteQueryAndReturnJson(query, "Head Blocker Analysis");
        }

        /// <summary>
        /// Find all blocked sessions and their queries
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetBlockedSessions()
        {
            const string query = @"
                SELECT TOP 100
                    r.runtime,
                    r.session_id,
                    r.blocking_session_id,
                    r.wait_type,
                    r.wait_duration_ms,
                    r.wait_resource,
                    q.procname,
                    SUBSTRING(q.stmt_text, 1, 200) AS stmt_text
                FROM tbl_REQUESTS r 
                JOIN tbl_NOTABLEACTIVEQUERIES q ON r.session_id = q.session_id AND r.runtime = q.runtime
                WHERE blocking_session_id <> 0
                ORDER BY r.wait_duration_ms DESC";

            return ExecuteQueryAndReturnJson(query, "All Blocked Sessions and Queries");
        }

        /// <summary>
        /// Spinlock contention analysis
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string AnalyzeSpinlocks()
        {
            const string query = @"
                DECLARE @cpus INT;
                SELECT @cpus = PropertyValue FROM tbl_ServerProperties WHERE PropertyName = 'cpu_count';

                SELECT TOP 20
                    t2.[name] AS spinlock_name,  
                    CAST(CAST(t2.spins AS FLOAT) - CAST(t1.spins AS FLOAT) AS BIGINT) delta_spins,  
                    CAST(CAST(t2.Backoffs AS FLOAT) - CAST(t1.Backoffs AS FLOAT) AS BIGINT) delta_backoff, 
                    DATEDIFF(MI, t1.runtime, t2.runtime) delta_minutes,
                    (CAST(CAST(t2.spins AS FLOAT) - CAST(t1.spins AS FLOAT) AS BIGINT)) / DATEDIFF(MILLISECOND, t1.runtime, t2.runtime) / @cpus AS spins_per_millisecond_per_CPU
                FROM  
                    (SELECT ROW_NUMBER() OVER (PARTITION BY [name] ORDER BY runtime) row, *  
                     FROM [tbl_SPINLOCKSTATS] 
                     WHERE runtime IN (SELECT MIN(runtime) FROM tbl_spinlockstats)) t1
                JOIN  
                    (SELECT ROW_NUMBER() OVER (PARTITION BY [name] ORDER BY runtime) row, *  
                     FROM [tbl_SPINLOCKSTATS]  
                     WHERE runtime IN (SELECT MAX(runtime) FROM tbl_spinlockstats)) AS t2
                ON t1.row = t2.row AND t1.[name] = t2.[name]
                ORDER BY delta_spins DESC";

            return ExecuteQueryAndReturnJson(query, "Spinlock Contention Analysis");
        }

        /// <summary>
        /// Get collection time range
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetCollectionTimeRange()
        {
            const string query = @"
                SELECT 
                    MIN(tb.StartTime) AS CollectionStartTime, 
                    MAX(tb.EndTime) AS CollectionEndTime, 
                    DATEDIFF(MINUTE, MIN(tb.starttime), MAX(tb.EndTime)) AS CollectionDuration_min
                FROM ReadTrace.tblBatches tb";

            return ExecuteQueryAndReturnJson(query, "PSSDiag/SQLLogScout Collection Time Range");
        }

        /// <summary>
        /// Find waits for a specific query by HashID
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetWaitsForQuery(long hashId)
        {
            string query = $@"
                SELECT 
                    runtime, ecid, blocking_session_id, task_state, wait_type, 
                    wait_duration_ms, wait_resource, tran_name, command, request_status 
                FROM tbl_REQUESTS r 
                JOIN ReadTrace.tblBatches b ON r.session_id = b.Session
                    AND r.runtime BETWEEN b.StartTime AND b.EndTime
                WHERE HashID = {hashId}
                    AND task_state != 'running' AND task_state != 'runnable' 
                ORDER BY wait_duration_ms DESC";

            return ExecuteQueryAndReturnJson(query, $"Waits for Query HashID: {hashId}");
        }

        /// <summary>
        /// Aggregate waits and waiting queries
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetAggregateWaitsAndQueries()
        {
            const string query = @"
                SELECT TOP 50
                    COUNT(*) occurrences, 
                    SUM(r.wait_duration_ms) WaitDensity_ms, 
                    r.wait_type, 
                    q.procname, 
                    SUBSTRING(q.stmt_text, 1, 200) AS stmt_text 
                FROM tbl_REQUESTS r
                JOIN tbl_notableactivequeries q ON r.session_id = q.session_id AND r.runtime = q.runtime
                WHERE wait_type IS NOT NULL 
                    AND wait_type NOT IN ('BACKUPIO', 'BROKER_RECEIVE_WAITFOR', 'CXPACKET', 'XE_DISPATCHER_WAIT', 
                                          'XE_TIMER_EVENT', 'REQUEST_FOR_DEADLOCK_SEARCH', 'WAITFOR', 'LOGMGR_QUEUE', 
                                          'CHECKPOINT_QUEUE', 'SLEEP_TASK', 'FT_IFTS_SCHEDULER_IDLE_WAIT', 
                                          'SLEEP_SYSTEMTASK', 'PREEMPTIVE_XE_DISPATCHER', 'SP_SERVER_DIAGNOSTICS_SLEEP', 
                                          'LAZYWRITER_SLEEP')
                GROUP BY r.wait_type, q.procname, q.stmt_text
                ORDER BY WaitDensity_ms DESC";

            return ExecuteQueryAndReturnJson(query, "Aggregate Waits and Waiting Queries");
        }

        /// <summary>
        /// Missing indexes analysis
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetMissingIndexes(int topN = 30)
        {
            string query = $@"
                IF OBJECT_ID('tbl_MissingIndexes') IS NOT NULL
                BEGIN
                    DECLARE @max_datetime DATETIME;
                    SELECT @max_datetime = MAX(runtime) FROM tbl_MissingIndexes;

                    SELECT TOP {topN} 
                        create_index_statement, 
                        improvement_measure, 
                        user_seeks, 
                        user_scans, 
                        runtime, 
                        object_id 
                    FROM tbl_MissingIndexes
                    WHERE runtime = @max_datetime
                    ORDER BY improvement_measure DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Missing Indexes Analysis");
        }

        /// <summary>
        /// SQL Server CPU usage over time
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetSqlCpuUsageOverTime()
        {
            const string query = @"
                SET ANSI_NULLS OFF;

                IF ((OBJECT_ID('dbo.tbl_ServerProperties') IS NOT NULL) AND (OBJECT_ID('dbo.CounterData') IS NOT NULL))
                BEGIN
                    DECLARE @process_id INT = 0, @cpu_count INT, @inst_name VARCHAR(64), @inst_index INT;

                    SELECT @process_id = sp.PropertyValue FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'ProcessID';
                    SELECT @cpu_count = CASE WHEN sp.PropertyValue = 0 THEN 1 ELSE sp.PropertyValue END 
                    FROM dbo.tbl_ServerProperties sp WHERE sp.PropertyName = 'cpu_count';

                    SELECT TOP 1 @inst_name = cdet.InstanceName, @inst_index = cdet.InstanceIndex
                    FROM dbo.CounterData ctr JOIN dbo.CounterDetails cdet ON ctr.CounterID = cdet.CounterID
                    WHERE cdet.ObjectName = 'Process' AND cdet.CounterName LIKE 'ID Process'
                        AND cdet.InstanceName LIKE 'sqlservr%' AND ctr.CounterValue = @process_id;

                    SELECT TOP 1000
                        sql_cpu.CounterDateTime AS EventTime,
                        sql_cpu.RecordIndex AS record_id,
                        os_cpu.system_idle_cpu,
                        CASE WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization 
                             THEN os_cpu.total_cpu_utilization ELSE sql_cpu.sql_cpu_utilization END AS sql_cpu_utilization,
                        os_cpu.total_cpu_utilization - (CASE WHEN sql_cpu.sql_cpu_utilization > os_cpu.total_cpu_utilization 
                                                             THEN os_cpu.total_cpu_utilization ELSE sql_cpu.sql_cpu_utilization END) AS nonsql_cpu_utilization
                    FROM
                    (SELECT ctr.CounterDateTime, ctr.RecordIndex, 
                            CONVERT(INT, (FLOOR(ctr.CounterValue) / (100 * @cpu_count)) * 100) AS sql_cpu_utilization,
                            det.InstanceName, det.InstanceIndex
                     FROM dbo.CounterData ctr JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
                     WHERE det.ObjectName = 'Process' AND det.CounterName LIKE '[%] Processor Time'
                           AND det.InstanceName = @inst_name AND det.InstanceIndex = @inst_index) AS sql_cpu
                    INNER JOIN
                    (SELECT ctr.CounterDateTime, ctr.RecordIndex, FLOOR(ctr.CounterValue) AS total_cpu_utilization,
                            100 - FLOOR(ctr.CounterValue) AS system_idle_cpu
                     FROM dbo.CounterData ctr JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
                     WHERE det.ObjectName = 'Processor Information' AND det.CounterName LIKE '[%] Processor Time'
                           AND det.InstanceName = '_Total') AS os_cpu ON sql_cpu.RecordIndex = os_cpu.RecordIndex
                    ORDER BY sql_cpu.CounterDateTime;
                END";

            return ExecuteQueryAndReturnJson(query, "SQL Server CPU Usage Over Time (Perfmon)");
        }

        /// <summary>
        /// Memory clerks distribution
        /// From: Reports-via-SQL-Queries.md
        /// </summary>
        public string GetMemoryClerkDistribution()
        {
            const string query = @"
                IF (OBJECT_ID('tbl_DM_OS_MEMORY_CLERKS') IS NOT NULL)
                BEGIN
                    SELECT TOP 20
                        type AS clerk_type,
                        SUM(pages_kb) AS total_size_kb,
                        SUM(pages_kb) / 1024 AS total_size_mb
                    FROM tbl_DM_OS_MEMORY_CLERKS 
                    GROUP BY type
                    ORDER BY total_size_kb DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Memory Clerk Distribution");
        }

        /// <summary>
        /// Get performance summary combining multiple analyses
        /// </summary>
        public string GetPerformanceSummary()
        {
            var summary = new
            {
                timestamp = DateTime.Now,
                collection_time_range = JsonConvert.DeserializeObject(GetCollectionTimeRange()),
                cpu_analysis = JsonConvert.DeserializeObject(AnalyzeCpuUsage()),
                top_cpu_queries = JsonConvert.DeserializeObject(GetTopCpuQueries(5)),
                io_analysis = JsonConvert.DeserializeObject(AnalyzeIoWaits()),
                io_disk_analysis = JsonConvert.DeserializeObject(AnalyzeIoPerformance(20)),
                blocking_analysis = JsonConvert.DeserializeObject(AnalyzeBlocking()),
                wait_analysis = JsonConvert.DeserializeObject(AnalyzeWaitStats()),
                spinlock_analysis = JsonConvert.DeserializeObject(AnalyzeSpinlocks()),
                memory_clerks = JsonConvert.DeserializeObject(GetMemoryClerkDistribution())
            };

            return JsonConvert.SerializeObject(summary, Formatting.Indented);
        }

        /// <summary>
        /// Execute custom query with validation
        /// </summary>
        public string ExecuteCustomQuery(string query)
        {
            var trimmedQuery = query.Trim();
            if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmedQuery.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) &&
                !trimmedQuery.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase) &&
                !trimmedQuery.StartsWith("IF", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only SELECT queries, CTEs, and queries with DECLARE/IF are allowed");
            }

            var dangerousKeywords = new[] { "DROP", "DELETE", "INSERT", "UPDATE", "TRUNCATE", "ALTER", "CREATE" };
            foreach (var keyword in dangerousKeywords)
            {
                if (trimmedQuery.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException($"Query contains disallowed keyword: {keyword}");
                }
            }

            return ExecuteQueryAndReturnJson(query, "Custom Query Results");
        }

        /// <summary>
        /// Helper method to execute query and return DataTable (used when composing multi-section JSON)
        /// </summary>
        private DataTable ExecuteQueryToDataTable(string query)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection) { CommandTimeout = 120 };
            connection.Open();
            var dataTable = new DataTable();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        /// <summary>
        /// Helper method to execute query and return JSON
        /// </summary>
        private string ExecuteQueryAndReturnJson(string query, string summaryTitle)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 120;

            connection.Open();
            var dataTable = new DataTable();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(dataTable);

            var result = new
            {
                summary = summaryTitle,
                row_count = dataTable.Rows.Count,
                data = ConvertDataTableToList(dataTable)
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        private static List<Dictionary<string, object>> ConvertDataTableToList(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null! : row[col];
                }
                list.Add(dict);
            }
            return list;
        }
    }
}
