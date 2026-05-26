using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
                    (CAST(CAST(t2.spins AS FLOAT) - CAST(t1.spins AS FLOAT) AS BIGINT)) / DATEDIFF(MILLISECOND, t1.runtime, t2.runtime) / @cpus AS spins_per_millisecond_per_CPU,
                    CASE WHEN (CAST(CAST(t2.spins AS FLOAT) - CAST(t1.spins AS FLOAT) AS BIGINT)) / DATEDIFF(MILLISECOND, t1.runtime, t2.runtime) / @cpus > 20000 THEN 1 ELSE 0 END AS is_high_cpu_driver
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
                IF OBJECT_ID('dbo.tbl_RUNTIMES') IS NOT NULL
                BEGIN
                    SELECT
                        MIN(runtime) AS CollectionStartTime,
                        MAX(runtime) AS CollectionEndTime,
                        DATEDIFF(MINUTE, MIN(runtime), MAX(runtime)) AS CollectionDuration_min
                    FROM dbo.tbl_RUNTIMES
                END
                ELSE IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                BEGIN
                    SELECT 
                        MIN(tb.StartTime) AS CollectionStartTime, 
                        MAX(tb.EndTime) AS CollectionEndTime, 
                        DATEDIFF(MINUTE, MIN(tb.StartTime), MAX(tb.EndTime)) AS CollectionDuration_min
                    FROM ReadTrace.tblBatches tb
                END";

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
        /// List known SQL Nexus tables with descriptions, annotated with whether each table is present in the connected database.
        /// </summary>
        public string ListNexusTables()
        {
            // Curated catalog of the most analytically significant SQL Nexus tables.
            // Key = table name as it appears in the dbo schema (or schema-qualified for ReadTrace).
            // Value = plain-English description for AI discovery.
            var catalog = new Dictionary<string, string>
            {
                // ── Core diagnostic / runtime captures ──────────────────────────────
                { "tbl_OS_WAIT_STATS",                   "Snapshot deltas of sys.dm_os_wait_stats. Use the first/last runtime pair to compute net wait time per wait type during the collection window. Essential for bottleneck analysis." },
                { "tbl_SPINLOCKSTATS",                   "Snapshot deltas of sys.dm_os_spinlock_stats. Use first/last runtime pair to compute net spins and backoffs. High spins on SOS_CACHESTORE or ACCESS_METHODS_* indicate CPU-bound spinlock contention." },
                { "tbl_SPINLOCKSTATS2",                  "Additional spinlock snapshot table (same schema as tbl_SPINLOCKSTATS) captured by some PSSDiag configurations." },
                { "tbl_NOTABLEACTIVEQUERIES",             "Active queries sampled at collection time: session_id, procname, stmt_text, CPU, reads, writes. Primary source for identifying queries running during the incident." },
                { "tbl_REQUESTS",                        "Snapshot of sys.dm_exec_requests: session_id, blocking_session_id, wait_type, wait_duration_ms, wait_resource. Join with tbl_NOTABLEACTIVEQUERIES on session_id+runtime to correlate queries with waits." },
                { "tbl_HEADBLOCKERSUMMARY",              "Aggregated blocking chains: head_blocker_session_id, blocked_task_count, total and average wait durations. Use analyze_blocking tool to query this." },
                { "tbl_SYSPROCESSES",                    "Snapshot of sys.sysprocesses (legacy): spid, blocked, waittype, waittime, lastwaittype, cpu, physical_io, memusage. Available when sys.dm_exec_requests is not captured." },
                { "tbl_System_Requests",                 "System thread requests from sys.dm_exec_requests where session_id < 0: os_thread_id, session_id, request_id, start_time, status, command, wait_type, wait_time, wait_resource. Covers internal SQL Server system tasks such as checkpoint, lazy writer, log writer, and ghost cleanup — not user sessions." },
                // ── Query performance ────────────────────────────────────────────────
                { "tbl_DM_EXEC_QUERY_STATS",             "Snapshot of sys.dm_exec_query_stats: cumulative CPU, elapsed time, logical reads/writes, execution counts per query plan. Use deltas between earliest and latest runtime to estimate workload during collection." },
                { "tbl_Hist_Top10_CPU_Queries_ByQueryHash",  "Periodic snapshots of the top 10 CPU-consuming queries (by query_hash). Delta between first and last snapshot isolates CPU used only during the collection window." },
                { "tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash", "Periodic snapshots of the top 10 queries by logical reads (by query_hash). Delta first/last for collection-window read attribution." },
                { "tbl_TopN_QueryPlanStats",             "Point-in-time snapshot of top N queries by various metrics (plan_handle, query_hash, total_worker_time, etc.)." },
                { "tbl_Top10_CPU_Consuming_Procedures",  "Top stored procedures by cumulative CPU from sys.dm_exec_procedure_stats." },
                { "tbl_Top10_CPU_Consuming_Triggers",    "Top triggers by cumulative CPU from sys.dm_exec_trigger_stats." },
                { "tbl_MissingIndexes",                  "Missing index recommendations from sys.dm_db_missing_index_details at collection time: create_index_statement, improvement_measure, user_seeks, user_scans." },
                { "tbl_DisabledIndexes",                 "Indexes that are currently disabled, which may indicate intentional maintenance or oversight." },
                { "tbl_SYSINDEXES",                      "Index metadata snapshot from sysindexes: rowcnt, used, dpages, origfillfactor. Useful for table size estimation." },
                // ── CPU & scheduling ─────────────────────────────────────────────────
                { "tbl_SQL_CPU_HEALTH",                  "CPU health data from the SQL Server ring buffer ('Recent SQL Processor Utilization Health Records'): record_id, EventTime, system_idle_cpu, sql_cpu_utilization. Populated by PSSDiag/SQLLogScout when ring buffer data is available. Used as the fallback CPU time-series source when Perfmon CounterData is absent." },
                { "tbl_RESOURCE_STATS",                  "sys.dm_os_ring_buffers resource stats ring buffer: memory pressure notifications and CPU scheduler stats." },
                { "tbl_RESOURCE_USAGE",                  "Scheduler-level CPU and I/O resource usage counters from sys.dm_os_schedulers or ring buffer." },
                { "tbl_Thread_Stats",                    "sys.dm_os_threads snapshot: active worker threads, CPU affinity, thread state. Relevant for thread-pool exhaustion analysis." },
                { "tbl_Thread_Stats_Snapshot",           "Point-in-time snapshot variant of tbl_Thread_Stats." },
                { "tbl_ThreadStats",                     "Alternative thread-stats capture (same conceptual content as tbl_Thread_Stats, different PSSDiag script version)." },
                { "tbl_UMSSTATS",                        "UMS (User Mode Scheduler) statistics: schedulers, workers, runnable count. Older-format equivalent of sys.dm_os_schedulers." },
                // ── Memory ───────────────────────────────────────────────────────────
                { "tbl_DM_OS_MEMORY_CLERKS",             "Memory clerk allocations from sys.dm_os_memory_clerks: type, pages_kb. Top clerks reveal which SQL Server component consumes the most memory." },
                { "tbl_MEMORYSTATUS_BUF_COUNTS",         "DBCC MEMORYSTATUS buffer pool counts: page counts by state (free, dirty, clean, stolen)." },
                { "tbl_MEMORYSTATUS_BUF_DISTRIBUTION",   "DBCC MEMORYSTATUS buffer pool distribution by NUMA node and age bucket." },
                { "tbl_MEMORYSTATUS_DYNAMIC_MEM_MGR",    "DBCC MEMORYSTATUS dynamic memory manager section: stolen, reserved, committed pages." },
                { "tbl_MEMORYSTATUS_PROC_CACHE",         "DBCC MEMORYSTATUS procedure cache section: total plans, cached objects, plan memory." },
                { "tbl_Query_Execution_Memory",          "sys.dm_exec_query_memory_grants snapshot: granted, used, and waiting memory grants per query." },
                { "tbl_dm_exec_query_memory_grants",     "Alias/alternative capture of sys.dm_exec_query_memory_grants (same content as tbl_Query_Execution_Memory in some configurations)." },
                // ── I/O ──────────────────────────────────────────────────────────────
                { "tbl_dm_io_virtual_file_stats",        "sys.dm_io_virtual_file_stats snapshot: io_stall_read_ms, io_stall_write_ms, num_of_reads, num_of_writes per database file. Key for file-level I/O latency analysis." },
                { "tbl_DatabaseFiles",                   "sys.master_files / sys.database_files snapshot: logical name, physical path, file type, size, autogrowth settings." },
                { "tbl_tempdb_space_usage_by_file",      "Tempdb space usage per file from sys.dm_db_file_space_usage: unallocated, version store, internal, user allocation." },
                { "tbl_tempdb_usage_by_object",          "Tempdb space used by object (work tables, worktables, sort runs)." },
                { "tbl_tempdb_waits",                    "Wait stats specific to tempdb contention (PFS, GAM, SGAM page latch waits)." },
                // ── Locking & transactions ───────────────────────────────────────────
                { "tbl_SYSLOCKINFO",                     "Lock details from sys.dm_tran_locks or syslockinfo: resource_type, request_mode, request_status, request_session_id." },
                { "tbl_LockSummary",                     "Aggregated lock summary: count by lock type and status." },
                { "tbl_DBCC_OPENTRAN_RAW",               "Open transaction details from DBCC OPENTRAN: oldest active transaction, session_id, begin time." },
                { "tbl_open_transactions",               "sys.dm_tran_session_transactions snapshot: active transactions, transaction_begin_time, is_user_transaction." },
                // ── Server properties & configuration ────────────────────────────────
                { "tbl_ServerProperties",                "SERVERPROPERTY() snapshot: key-value pairs including Edition, ProductVersion, ProcessID, cpu_count, physical_memory_kb, max_workers_count. Frequently JOINed by other queries for cpu_count." },
                { "tbl_sp_configure",                    "sp_configure output: server-level configuration names, minimum, maximum, config_value, run_value. Check max server memory, max degree of parallelism, cost threshold for parallelism." },
                { "tbl_SPCONFIGURE",                     "Alternative capture of sp_configure (same content; present in older PSSDiag script versions)." },
                { "tbl_Sys_Configurations",              "sys.configurations snapshot (same logical content as sp_configure but direct DMV capture)." },
                { "tbl_TraceFlags",                      "Active trace flags from DBCC TRACESTATUS: traceflag, status, global, session." },
                { "tbl_StartupParameters",               "SQL Server startup parameters (e.g., -T trace flags, -d/-l/-e file paths) from registry or sys.dm_server_registry." },
                { "tbl_SystemInformation",               "OS-level system information: total physical memory, number of CPUs, NUMA nodes, OS version, page size." },
                // ── Databases & storage ──────────────────────────────────────────────
                { "tbl_SysDatabases",                    "sys.databases snapshot: database_id, name, state_desc, recovery_model_desc, compatibility_level, collation_name." },
                { "tbl_SPHELPDB",                        "sp_helpdb output: database name, size, owner, dbid, created, status, compatibility_level." },
                { "tbl_dm_db_log_info",                  "sys.dm_db_log_info: VLF (virtual log file) details per database — file_id, vlf_size_mb, vlf_sequence_number, vlf_status. Many active VLFs can slow log writes." },
                { "tbl_dm_db_stats_properties",          "sys.dm_db_stats_properties snapshot: last_updated, rows, rows_sampled, modification_counter. Stale stats identified by high modification_counter relative to rows." },
                { "tbl_disk_information",                "Disk/volume information from sys.dm_os_volume_stats or xp_fixeddrives: drive, total MB, free MB." },
                { "tbl_disk_volume_information",         "Extended disk volume information including volume label, file system type, and cluster size." },
                // ── Wait/latch internals ─────────────────────────────────────────────
                { "tbl_dm_os_latch_stats",               "sys.dm_os_latch_stats snapshot: latch class, wait_count, wait_time_ms. High BUFFER latch waits indicate I/O or memory pressure; other classes point to internal resource contention." },
                { "tbl_WAITSTATS",                       "Alternative or supplemental wait stats capture (same conceptual content as tbl_OS_WAIT_STATS; may appear in some PSSDiag configurations)." },
                // ── Perfmon (CounterData) ────────────────────────────────────────────
                { "CounterData",                         "Raw Perfmon counter values captured by SQL Server data collector or PAL. JOIN with CounterDetails on CounterID to get ObjectName, CounterName, InstanceName. Time axis via CounterDateTime and RecordIndex." },
                { "CounterDetails",                      "Perfmon counter metadata: CounterID (PK), MachineName, ObjectName, CounterName, InstanceName, InstanceIndex, CounterType. Always JOIN this with CounterData." },
                // ── ReadTrace (profiler/XEvent trace replay) ─────────────────────────
                { "ReadTrace.tblBatches",                "ReadTrace schema: one row per batch execution — HashID, Session, StartTime, EndTime, Duration (microseconds), CPU (ms), Reads, Writes. Primary table for query-level performance analysis." },
                { "ReadTrace.tblUniqueBatches",          "ReadTrace schema: one row per unique batch (by HashID) — NormText (normalized SQL), OrigText. JOIN with tblBatches on HashID to attach SQL text." },
                { "ReadTrace.tblStatements",             "ReadTrace schema: individual statement executions within a batch — HashID, BatchHashID, Duration, CPU, Reads, Writes." },
                { "ReadTrace.tblUniqueStatements",       "ReadTrace schema: unique statement text — HashID, NormText. JOIN with tblStatements on HashID." },
                { "ReadTrace.tblConnections",            "ReadTrace schema: connection-level events — LoginName, HostName, ApplicationName, NTUserName, SPID." },
                // ── Miscellaneous ────────────────────────────────────────────────────
                { "tbl_IMPORTEDFILES",                   "Metadata about which PSSDiag/SQLLogScout script files were imported: script_name, revision, imported_by, imported_date, input_file_name." },
                { "tbl_DiagInfo",                        "General diagnostic info captured at collection start: SQL Server version string, edition, and other environmental details." },
                { "tbl_RUNTIMES",                        "Timestamps of each data collection cycle (runtime column). Use MIN/MAX to determine the effective collection window for delta-based analyses." },
                { "tbl_XPMSVER",                         "xp_msver output: SQL Server version components (major, minor, build, revision) and other server properties." },
                { "tbl_SCRIPT_ENVIRONMENT_DETAILS",      "Environment details recorded by the PSSDiag/SQLLogScout collection script: collection host, SQL instance name, collection timestamp." },
                { "tbl_ActiveProcesses_OS",              "OS-level active processes at collection time (tasklist equivalent): name, PID, memory usage." },
                { "tbl_installed_programs",              "Programs installed on the server from registry/WMI: DisplayName, DisplayVersion, Publisher, InstallDate." },
                { "tbl_running_drivers",                 "Device drivers loaded on the server: driver name, state, start mode. Relevant for filter drivers (e.g., antivirus, backup agents) that may add latency to I/O stack calls." },
                { "tbl_sqlagent_jobs",                   "SQL Server Agent job definitions imported at collection time: job_id, name, enabled, description, date_created, date_modified, owner_sid. Use to identify scheduled maintenance jobs (index rebuilds, backups, integrity checks) that may overlap with the performance incident window." },
                { "tbl_windows_hotfixes_installed",      "Windows hotfixes/patches installed on the server from WMI Win32_QuickFixEngineering." },
                { "tbl_PowerPlan",                       "Active Windows power plan. 'Balanced' instead of 'High performance' causes CPU throttling and degrades SQL Server throughput." },
                { "tbl_SYSPERFINFO",                     "sys.dm_os_performance_counters snapshot (equivalent to sysperfinfo): object_name, counter_name, instance_name, cntr_value, cntr_type." },
                { "tbl_sysperfinfo_raw",                 "Raw sys.dm_os_performance_counters capture without delta computation — useful for computing rates between two snapshots." },
                { "tbl_ERRORLOG",                        "SQL Server errorlog entries imported into the database: LogDate, ProcessInfo, Text. Search for errors, stack dumps, memory pressure warnings, or recovery events." },
                { "tbl_database_options",                 "sys.databases / DATABASEPROPERTYEX snapshot of per-database options: compatibility_level, recovery_model_desc, page_verify_option_desc, is_auto_update_stats_on, is_auto_create_stats_on, is_query_store_on, collation_name. Useful for identifying configuration differences across databases." },
                { "ReadTrace.tblInterestingEvents",       "ReadTrace schema: non-batch events captured in the trace that are considered interesting (e.g., deadlock graphs, attention events, errors, logins). Columns include EventClass, EventSubClass, TextData, SPID, StartTime. Useful for deadlock and error correlation." },
                { "tbl_PlanCache_Stats",                  "Plan cache summary from sys.dm_exec_cached_plans: plan count by cache type, total memory pages. High single-use plans indicate plan cache bloat." },
                { "tbl_proccache_pollution",             "Single-use query plans polluting the plan cache: query text, usecounts, size_in_bytes. Indicates ad-hoc workload without parameterization." },
                { "tbl_proccache_summary",               "Aggregated plan cache summary: object type, number of plans, total size in MB." },
                { "tbl_QDS_Query_Stats",                 "Query Store runtime statistics snapshot: query_id, plan_id, execution_type_desc, avg_duration, avg_cpu_time, avg_logical_io_reads." },
            };

            // Check which tables actually exist in the connected database
            const string existenceQuery = @"
                SELECT SCHEMA_NAME(schema_id) + '.' + name
                FROM sys.tables";

            var existingTables = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand(existenceQuery, connection) { CommandTimeout = 30 };
                connection.Open();
                using var dr = command.ExecuteReader();
                while (dr.Read())
                    existingTables.Add(dr.GetString(0));
            }
            catch
            {
                // If existence check fails, return catalog without presence flags
            }

            var tableList = new List<object>();
            int presentCount = 0;
            foreach (var entry in catalog)
            {
                // Normalize to schema.table for lookup (handle both "tbl_Foo" as "dbo.tbl_Foo" and "ReadTrace.tblBatches")
                string lookupName = entry.Key.IndexOf('.') >= 0 ? entry.Key : "dbo." + entry.Key;
                bool present = existingTables.Contains(lookupName);
                if (present) presentCount++;
                tableList.Add(new
                {
                    table_name  = entry.Key,
                    description = entry.Value,
                    present_in_database = present
                });
            }

            var result = new
            {
                summary             = "SQL Nexus Table Catalog (curated subset)",
                discovery_hint      = "This list covers the most analytically significant tables. The connected database may contain additional tables not listed here. To discover all tables, use the query_nexus_database tool with: SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME",
                total_known_tables  = tableList.Count,
                tables_present      = presentCount,
                tables              = tableList
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        /// <summary>
        /// Query #2: Detailed execution history for a specific query by HashID.
        /// Shows each individual execution: duration, CPU, reads, writes, row counts, start/end time.
        /// </summary>
        public string GetQueryExecutionDetails(long hashId)
        {
            string query = $@"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                BEGIN
                    SELECT TOP 200
                        b.StartTime,
                        b.EndTime,
                        b.Duration / 1000 AS Duration_ms,
                        b.CPU            AS CPU_ms,
                        b.Duration / 1000 - b.CPU AS WaitTime_ms,
                        CONVERT(DECIMAL(5,2),
                            CASE WHEN b.Duration = 0 THEN 0
                                 ELSE 100.0 * (b.Duration / 1000.0 - b.CPU) / (b.Duration / 1000.0)
                            END) AS WaitPct,
                        b.Reads,
                        b.Writes,
                        b.RowCounts,
                        SUBSTRING(ub.NormText, 1, 500) AS NormText
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                    WHERE b.HashID = {hashId}
                    ORDER BY b.StartTime DESC;
                END";

            return ExecuteQueryAndReturnJson(query, $"Execution Details for HashID: {hashId}");
        }

        /// <summary>
        /// Query #7: Wait resource hot spots — which specific pages, rows, objects are most contended.
        /// Groups by wait_resource to find the hot table/page/key causing blocking or latch contention.
        /// </summary>
        public string GetWaitResourceHotspots()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_REQUESTS') IS NOT NULL
                BEGIN
                    SELECT TOP 50
                        COUNT(*)                    AS occurrences,
                        wait_resource,
                        wait_type,
                        MAX(wait_duration_ms)       AS max_wait_ms,
                        AVG(wait_duration_ms)       AS avg_wait_ms,
                        SUM(wait_duration_ms)       AS total_wait_ms
                    FROM tbl_REQUESTS r
                    WHERE wait_type IS NOT NULL
                        AND wait_resource IS NOT NULL
                        AND wait_resource <> ''
                        AND wait_type NOT IN (
                            'BACKUPIO','BROKER_RECEIVE_WAITFOR','CXPACKET','XE_DISPATCHER_WAIT',
                            'XE_TIMER_EVENT','REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR','LOGMGR_QUEUE',
                            'CHECKPOINT_QUEUE','SLEEP_TASK','FT_IFTS_SCHEDULER_IDLE_WAIT',
                            'SLEEP_SYSTEMTASK','PREEMPTIVE_XE_DISPATCHER','SP_SERVER_DIAGNOSTICS_SLEEP',
                            'LAZYWRITER_SLEEP')
                    GROUP BY wait_resource, wait_type
                    ORDER BY total_wait_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Wait Resource Hot Spots");
        }

        /// <summary>
        /// Query #8: Wait type frequency distribution from tbl_REQUESTS (request-level, more granular than tbl_OS_WAIT_STATS).
        /// Shows occurrence count, average/max/total wait ms, and % of total wait per wait type.
        /// </summary>
        public string GetWaitTypeDistribution()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_REQUESTS') IS NOT NULL
                BEGIN
                    SELECT TOP 30
                        COUNT(*)                    AS occurrences,
                        wait_type,
                        MAX(wait_duration_ms)       AS max_wait_ms,
                        AVG(wait_duration_ms)       AS avg_wait_ms,
                        SUM(wait_duration_ms)       AS total_wait_ms,
                        CAST(100.0 * SUM(wait_duration_ms)
                             / NULLIF(SUM(SUM(wait_duration_ms)) OVER(), 0)
                             AS DECIMAL(5,2))       AS pct_total_wait
                    FROM tbl_REQUESTS r
                    WHERE wait_type IS NOT NULL
                        AND wait_type NOT IN (
                            'BACKUPIO','BROKER_RECEIVE_WAITFOR','CXPACKET','XE_DISPATCHER_WAIT',
                            'XE_TIMER_EVENT','REQUEST_FOR_DEADLOCK_SEARCH','WAITFOR','LOGMGR_QUEUE',
                            'CHECKPOINT_QUEUE','SLEEP_TASK','FT_IFTS_SCHEDULER_IDLE_WAIT',
                            'SLEEP_SYSTEMTASK','PREEMPTIVE_XE_DISPATCHER','SP_SERVER_DIAGNOSTICS_SLEEP',
                            'LAZYWRITER_SLEEP')
                    GROUP BY wait_type
                    ORDER BY total_wait_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Wait Type Frequency Distribution (Request-Level)");
        }

        /// <summary>
        /// Query #14: Find queries spending most time waiting (wait-bound queries).
        /// Returns queries where CPU is less than 80% of total duration, sorted by total wait time.
        /// </summary>
        public string GetWaitHeavyQueries()
        {
            const string query = @"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                   AND OBJECT_ID('dbo.tbl_REQUESTS') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblUniqueBatches') IS NOT NULL
                BEGIN
                    WITH BatchesData AS (
                        SELECT
                            b.Session,
                            b.StartTime, b.EndTime,
                            b.HashID,
                            b.CPU,
                            b.Duration,
                            CAST(b.CPU AS FLOAT) / NULLIF(CAST(b.Duration AS FLOAT), 0) AS CpuFraction,
                            SUBSTRING(ub.NormText, 1, 500) AS NormText
                        FROM ReadTrace.tblBatches b
                        JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                        WHERE b.Duration > 0
                    )
                    SELECT TOP 50
                        MAX(r.wait_duration_ms)                     AS MaxWaitDuration_ms,
                        SUM(r.wait_duration_ms)                     AS TotalWaitDuration_ms,
                        COUNT(*)                                     AS WaitOccurrences,
                        CAST(AVG(t.Duration) / 1000.0 AS DECIMAL(18,2)) AS AvgQueryDuration_ms,
                        CAST(AVG(t.CpuFraction * 100) AS DECIMAL(5,2))  AS AvgCpuPct,
                        CAST(100 - AVG(t.CpuFraction * 100) AS DECIMAL(5,2)) AS AvgWaitPct,
                        r.wait_type,
                        t.NormText AS Query,
                        t.HashID
                    FROM tbl_REQUESTS r
                    JOIN BatchesData t
                        ON r.runtime BETWEEN t.StartTime AND t.EndTime
                        AND r.session_id = t.Session
                    WHERE t.CpuFraction < 0.80
                        AND r.task_state NOT IN ('running','runnable')
                        AND r.wait_type IS NOT NULL
                    GROUP BY r.wait_type, t.NormText, t.HashID
                    ORDER BY TotalWaitDuration_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Wait-Heavy Queries (CPU < 80% of Duration)");
        }

        /// <summary>
        /// Query #11 (deepdive): Break down a batch into individual statements.
        /// Requires DetailedPerf collection with statement-level trace (ReadTrace.tblStatements).
        /// </summary>
        public string GetStatementsInBatch(long batchSeq)
        {
            string query = $@"
                IF OBJECT_ID('ReadTrace.tblStatements') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblUniqueStatements') IS NOT NULL
                BEGIN
                    SELECT
                        SUM(b.CPU)            AS CPU_ms,
                        SUM(b.Duration)/1000.0 AS Duration_ms,
                        COUNT(*)               AS Occurrences,
                        AVG(b.Duration)/1000.0 AS AvgDuration_ms,
                        MAX(b.Duration)/1000.0 AS MaxDuration_ms,
                        SUM(b.Reads)           AS TotalReads,
                        AVG(b.Reads)           AS AvgReads,
                        SUBSTRING(ub.NormText, 1, 500) AS Statement,
                        b.HashID               AS StatementHashID
                    FROM ReadTrace.tblStatements b
                    JOIN ReadTrace.tblUniqueStatements ub ON b.HashID = ub.HashID
                    WHERE b.BatchSeq = {batchSeq}
                    GROUP BY ub.NormText, b.HashID
                    ORDER BY Duration_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, $"Statements in Batch (BatchSeq: {batchSeq})");
        }

        /// <summary>
        /// Query #11 (blocking): Recursive blocking chain tree showing full hierarchy.
        /// Shows root blocker (level 0) through all downstream blocked sessions.
        /// </summary>
        public string GetBlockingChainTree()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_BLOCKING_CHAINS') IS NOT NULL
                BEGIN
                    WITH BlockingChain AS (
                        SELECT
                            session_id,
                            blocking_session_id,
                            wait_type,
                            wait_duration_ms,
                            wait_resource,
                            0 AS level
                        FROM tbl_BLOCKING_CHAINS
                        WHERE blocking_session_id = 0

                        UNION ALL

                        SELECT
                            bc.session_id,
                            bc.blocking_session_id,
                            bc.wait_type,
                            bc.wait_duration_ms,
                            bc.wait_resource,
                            chain.level + 1
                        FROM tbl_BLOCKING_CHAINS bc
                        INNER JOIN BlockingChain chain ON bc.blocking_session_id = chain.session_id
                    )
                    SELECT TOP 200
                        level,
                        session_id,
                        blocking_session_id,
                        wait_type,
                        wait_duration_ms,
                        wait_resource,
                        REPLICATE('  ', level) + CAST(session_id AS VARCHAR(10)) AS blocking_hierarchy
                    FROM BlockingChain
                    ORDER BY level, session_id;
                END
                ELSE IF OBJECT_ID('dbo.tbl_REQUESTS') IS NOT NULL
                BEGIN
                    -- Fallback: derive chain from tbl_REQUESTS snapshots
                    SELECT TOP 100
                        r.runtime,
                        r.session_id,
                        r.blocking_session_id,
                        r.wait_type,
                        r.wait_duration_ms,
                        r.wait_resource
                    FROM tbl_REQUESTS r
                    WHERE r.blocking_session_id <> 0
                      AND r.blocking_session_id IS NOT NULL
                    ORDER BY r.wait_duration_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Blocking Chain Tree (Full Hierarchy)");
        }

        /// <summary>
        /// Query #12 (blocking): Lock summary by object — which tables/resources have most lock contention.
        /// </summary>
        public string GetLockSummaryByObject()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_BLOCKING_CHAINS') IS NOT NULL
                BEGIN
                    SELECT TOP 50
                        database_name,
                        wait_resource,
                        COUNT(*)                AS lock_count,
                        SUM(wait_duration_ms)   AS total_wait_ms,
                        AVG(wait_duration_ms)   AS avg_wait_ms,
                        MAX(wait_duration_ms)   AS max_wait_ms
                    FROM tbl_BLOCKING_CHAINS
                    WHERE wait_resource IS NOT NULL
                    GROUP BY database_name, wait_resource
                    ORDER BY total_wait_ms DESC;
                END
                ELSE IF OBJECT_ID('dbo.tbl_REQUESTS') IS NOT NULL
                BEGIN
                    SELECT TOP 50
                        wait_resource,
                        wait_type,
                        COUNT(*)                AS lock_count,
                        SUM(wait_duration_ms)   AS total_wait_ms,
                        AVG(wait_duration_ms)   AS avg_wait_ms,
                        MAX(wait_duration_ms)   AS max_wait_ms
                    FROM tbl_REQUESTS
                    WHERE wait_resource IS NOT NULL
                      AND wait_type LIKE 'LCK_%'
                    GROUP BY wait_resource, wait_type
                    ORDER BY total_wait_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Lock Summary by Object/Resource");
        }

        /// <summary>
        /// Query #12 (application): Find all queries executed by a specific application name.
        /// Pass null or empty appName to get aggregate stats across ALL applications.
        /// </summary>
        public string GetQueriesByApplication(string? appName = null)
        {
            bool filterByApp = !string.IsNullOrWhiteSpace(appName);
            // Sanitize: escape single quotes to prevent SQL injection
            string safeAppName = (appName ?? "").Replace("'", "''");

            string query = filterByApp
                ? $@"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblConnections') IS NOT NULL
                BEGIN
                    SELECT TOP 100
                        COUNT(*)                                                AS Executions,
                        SUM(b.Duration) / 1000                                  AS Total_Duration_ms,
                        AVG(b.Duration) / 1000                                  AS Avg_Duration_ms,
                        MIN(b.Duration) / 1000                                  AS Min_Duration_ms,
                        MAX(b.Duration) / 1000                                  AS Max_Duration_ms,
                        SUM(b.CPU)                                              AS Total_CPU_ms,
                        AVG(b.CPU)                                              AS Avg_CPU_ms,
                        SUM(b.Reads)                                            AS Total_Reads,
                        AVG(b.Reads)                                            AS Avg_Reads,
                        SUM(b.Writes)                                           AS Total_Writes,
                        SUBSTRING(ub.NormText, 1, 400)                          AS Query,
                        b.HashID
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblConnections c
                        ON b.ConnSeq = c.ConnSeq AND b.session = c.session
                    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                    WHERE c.ApplicationName = '{safeAppName}'
                    GROUP BY ub.NormText, b.HashID
                    ORDER BY Total_Duration_ms DESC;
                END"
                : @"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblConnections') IS NOT NULL
                BEGIN
                    SELECT TOP 100
                        COUNT(*)                                                AS Executions,
                        SUM(b.Duration) / 1000                                  AS Total_Duration_ms,
                        AVG(b.Duration) / 1000                                  AS Avg_Duration_ms,
                        SUM(b.CPU)                                              AS Total_CPU_ms,
                        SUM(b.Reads)                                            AS Total_Reads,
                        c.ApplicationName,
                        SUBSTRING(ub.NormText, 1, 400)                          AS Query,
                        b.HashID
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblConnections c
                        ON b.ConnSeq = c.ConnSeq AND b.session = c.session
                    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                    GROUP BY ub.NormText, b.HashID, c.ApplicationName
                    ORDER BY Total_Duration_ms DESC;
                END";

            string title = filterByApp
                ? $"Queries by Application: {appName}"
                : "Top Queries Across All Applications";
            return ExecuteQueryAndReturnJson(query, title);
        }

        /// <summary>
        /// Query #13: Aggregate performance metrics grouped by application name.
        /// Shows which application is consuming the most duration, CPU, reads, and writes.
        /// </summary>
        public string GetPerformanceByApplication()
        {
            const string query = @"
                IF OBJECT_ID('ReadTrace.tblBatchPartialAggs') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblUniqueAppNames') IS NOT NULL
                BEGIN
                    SELECT
                        SUM(TotalDuration)                                              AS Duration_ms,
                        SUM(TotalCPU)                                                   AS CPU_ms,
                        SUM(TotalReads)                                                 AS Reads,
                        SUM(TotalWrites)                                                AS Writes,
                        COUNT(DISTINCT HashID)                                          AS Unique_Queries,
                        AppName,
                        CAST(100.0 * SUM(TotalDuration)
                             / NULLIF(SUM(SUM(TotalDuration)) OVER(), 0)
                             AS DECIMAL(5,2))                                           AS Pct_Total_Duration,
                        CAST(100.0 * SUM(TotalCPU)
                             / NULLIF(SUM(SUM(TotalCPU)) OVER(), 0)
                             AS DECIMAL(5,2))                                           AS Pct_Total_CPU,
                        CAST(100.0 * SUM(TotalReads)
                             / NULLIF(SUM(SUM(TotalReads)) OVER(), 0)
                             AS DECIMAL(5,2))                                           AS Pct_Total_Reads
                    FROM ReadTrace.tblBatchPartialAggs b
                    INNER JOIN ReadTrace.tblUniqueAppNames a ON a.iID = b.AppNameID
                    GROUP BY AppName
                    ORDER BY Duration_ms DESC;
                END
                ELSE IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                   AND OBJECT_ID('ReadTrace.tblConnections') IS NOT NULL
                BEGIN
                    -- Fallback: compute directly from tblBatches + tblConnections
                    SELECT TOP 30
                        c.ApplicationName                           AS AppName,
                        SUM(b.Duration) / 1000                     AS Duration_ms,
                        SUM(b.CPU)                                 AS CPU_ms,
                        SUM(b.Reads)                               AS Reads,
                        SUM(b.Writes)                              AS Writes,
                        COUNT(DISTINCT b.HashID)                   AS Unique_Queries,
                        COUNT(*)                                   AS Executions,
                        CAST(100.0 * SUM(b.Duration)
                             / NULLIF(SUM(SUM(b.Duration)) OVER(), 0)
                             AS DECIMAL(5,2))                      AS Pct_Total_Duration,
                        CAST(100.0 * SUM(b.CPU)
                             / NULLIF(SUM(SUM(b.CPU)) OVER(), 0)
                             AS DECIMAL(5,2))                      AS Pct_Total_CPU
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblConnections c
                        ON b.ConnSeq = c.ConnSeq AND b.session = c.session
                    GROUP BY c.ApplicationName
                    ORDER BY Duration_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Performance by Application Name");
        }

        /// <summary>
        /// Query #19: CPU breakdown by database — which database on the instance uses the most CPU.
        /// </summary>
        public string GetCpuByDatabase()
        {
            const string query = @"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                BEGIN
                    SELECT
                        DB_NAME(b.DatabaseID)   AS database_name,
                        b.DatabaseID,
                        SUM(b.CPU)              AS Total_CPU_ms,
                        COUNT(*)                AS Executions,
                        SUM(b.CPU) / NULLIF(COUNT(*), 0) AS Avg_CPU_ms,
                        CAST(100.0 * SUM(b.CPU)
                             / NULLIF(SUM(SUM(b.CPU)) OVER(), 0)
                             AS DECIMAL(5,2))   AS CPU_Pct
                    FROM ReadTrace.tblBatches b
                    WHERE b.DatabaseID IS NOT NULL
                    GROUP BY b.DatabaseID
                    ORDER BY Total_CPU_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "CPU Consumption by Database");
        }

        /// <summary>
        /// Query #26 (I/O): Top queries sorted by physical reads — identifies I/O-intensive queries.
        /// </summary>
        public string GetTopQueriesByReads(int topN = 50)
        {
            string query = $@"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                BEGIN
                    SELECT TOP {topN}
                        SUM(b.Reads)            AS Total_Reads,
                        COUNT(*)                AS Executions,
                        SUM(b.Reads) / NULLIF(COUNT(*), 0) AS Avg_Reads,
                        SUM(b.Duration) / 1000  AS Total_Duration_ms,
                        SUM(b.CPU)              AS Total_CPU_ms,
                        SUBSTRING(ub.NormText, 1, 400) AS Query,
                        b.HashID
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                    GROUP BY ub.NormText, b.HashID
                    ORDER BY Total_Reads DESC;
                END
                ELSE IF OBJECT_ID('dbo.tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash') IS NOT NULL
                BEGIN
                    WITH
                        first_snap AS (SELECT * FROM tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash
                                       WHERE runtime = (SELECT MIN(runtime) FROM tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash)),
                        last_snap  AS (SELECT * FROM tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash
                                       WHERE runtime = (SELECT MAX(runtime) FROM tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash))
                    SELECT TOP {topN}
                        l.total_logical_reads - COALESCE(f.total_logical_reads, 0)  AS delta_logical_reads,
                        l.execution_count     - COALESCE(f.execution_count, 0)       AS delta_executions,
                        l.sample_statement_text
                    FROM last_snap l
                    LEFT JOIN first_snap f ON l.query_hash = f.query_hash
                    ORDER BY delta_logical_reads DESC;
                END";

            return ExecuteQueryAndReturnJson(query, $"Top {topN} Queries by Physical Reads");
        }

        /// <summary>
        /// Query #27 (I/O): Top queries sorted by writes — identifies write-heavy / log-intensive queries.
        /// </summary>
        public string GetTopQueriesByWrites(int topN = 50)
        {
            string query = $@"
                IF OBJECT_ID('ReadTrace.tblBatches') IS NOT NULL
                BEGIN
                    SELECT TOP {topN}
                        SUM(b.Writes)           AS Total_Writes,
                        COUNT(*)                AS Executions,
                        SUM(b.Writes) / NULLIF(COUNT(*), 0) AS Avg_Writes,
                        SUM(b.RowCounts)        AS Total_Rows_Affected,
                        SUM(b.Duration) / 1000  AS Total_Duration_ms,
                        SUBSTRING(ub.NormText, 1, 400) AS Query,
                        b.HashID
                    FROM ReadTrace.tblBatches b
                    JOIN ReadTrace.tblUniqueBatches ub ON b.HashID = ub.HashID
                    WHERE b.Writes > 0
                    GROUP BY ub.NormText, b.HashID
                    ORDER BY Total_Writes DESC;
                END";

            return ExecuteQueryAndReturnJson(query, $"Top {topN} Queries by Writes");
        }

        /// <summary>
        /// Query #16 (SQL file stats): Per-database-file I/O latency from tbl_FILE_STATS.
        /// Reports avg read/write latency per .mdf/.ldf/.ndf file — distinct from Perfmon disk counters.
        /// </summary>
        public string GetSqlFileIoStats()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_FILE_STATS') IS NOT NULL
                BEGIN
                    SELECT
                        database_name,
                        file_type,
                        num_of_reads,
                        num_of_writes,
                        io_stall_read_ms,
                        io_stall_write_ms,
                        CASE WHEN num_of_reads  = 0 THEN 0
                             ELSE io_stall_read_ms  / num_of_reads  END AS avg_read_latency_ms,
                        CASE WHEN num_of_writes = 0 THEN 0
                             ELSE io_stall_write_ms / num_of_writes END AS avg_write_latency_ms,
                        size_on_disk_bytes / 1024 / 1024 AS file_size_mb,
                        physical_name
                    FROM tbl_FILE_STATS
                    ORDER BY io_stall_read_ms + io_stall_write_ms DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "SQL Server File I/O Statistics (per file latency)");
        }

        /// <summary>
        /// Query #28: Compilation and recompilation rates from Perfmon CounterData.
        /// High SQL Compilations/sec indicates ad-hoc query workload or plan cache pressure.
        /// Also checks plan cache composition from tbl_CACHEOBJECTS if available.
        /// </summary>
        public string GetCompilationStats()
        {
            const string query = @"
                IF OBJECT_ID('dbo.CounterData') IS NOT NULL
                   AND OBJECT_ID('dbo.CounterDetails') IS NOT NULL
                BEGIN
                    SELECT
                        CONVERT(DATETIME, ctr.CounterDateTime)  AS CounterDateTime,
                        det.CounterName,
                        ctr.CounterValue                        AS Value_Per_Second
                    FROM dbo.CounterData ctr
                    JOIN dbo.CounterDetails det ON ctr.CounterID = det.CounterID
                    WHERE det.CounterName IN ('SQL Compilations/sec','SQL Re-Compilations/sec')
                      AND det.ObjectName LIKE '%SQL Statistics%'
                    ORDER BY ctr.CounterDateTime, det.CounterName;
                END

                IF OBJECT_ID('dbo.tbl_CACHEOBJECTS') IS NOT NULL
                BEGIN
                    SELECT
                        objtype,
                        cacheobjtype,
                        COUNT(*)                        AS plan_count,
                        SUM(size_in_bytes) / 1024 / 1024 AS cache_size_mb,
                        SUM(usecounts)                  AS total_use_count,
                        AVG(usecounts)                  AS avg_use_count,
                        MIN(usecounts)                  AS min_use_count,
                        MAX(usecounts)                  AS max_use_count
                    FROM tbl_CACHEOBJECTS
                    GROUP BY objtype, cacheobjtype
                    ORDER BY plan_count DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Compilation Stats and Plan Cache Analysis");
        }

        /// <summary>
        /// Query #21 (CPU): Plan cache composition analysis — identifies ad-hoc query bloat.
        /// avg_use_count ≈ 1 means plans are compiled once and discarded (compilation CPU waste).
        /// </summary>
        public string GetPlanCacheAnalysis()
        {
            const string query = @"
                IF OBJECT_ID('dbo.tbl_CACHEOBJECTS') IS NOT NULL
                BEGIN
                    SELECT
                        objtype,
                        cacheobjtype,
                        COUNT(*)                         AS plan_count,
                        SUM(size_in_bytes) / 1024 / 1024 AS cache_size_mb,
                        SUM(usecounts)                   AS total_use_count,
                        AVG(usecounts)                   AS avg_use_count,
                        MIN(usecounts)                   AS min_use_count,
                        MAX(usecounts)                   AS max_use_count,
                        SUM(CASE WHEN usecounts = 1 THEN 1 ELSE 0 END) AS single_use_plans,
                        CAST(100.0 * SUM(CASE WHEN usecounts = 1 THEN 1 ELSE 0 END)
                             / NULLIF(COUNT(*), 0) AS DECIMAL(5,2))    AS single_use_pct
                    FROM tbl_CACHEOBJECTS
                    GROUP BY objtype, cacheobjtype
                    ORDER BY plan_count DESC;
                END";

            return ExecuteQueryAndReturnJson(query, "Plan Cache Composition Analysis");
        }

        /// <summary>
        /// Query #27 (index): Table statistics health — last updated date, row counts, modification counters.
        /// Stale statistics (high modification_percent, old last_updated) cause bad query plans.
        /// Pass null dbName to return stats for all user databases.
        /// </summary>
        public string GetTableStatisticsHealth(string? dbName = null)
        {
            bool filterByDb = !string.IsNullOrWhiteSpace(dbName);
            string safeDbName = (dbName ?? "").Replace("'", "''");

            string whereClause = filterByDb
                ? $"WHERE Database_Name NOT IN ('msdb','master','model','tempdb') AND Database_Name = '{safeDbName}'"
                : "WHERE Database_Name NOT IN ('msdb','master','model','tempdb')";

            string query = $@"
                IF OBJECT_ID('dbo.tbl_dm_db_stats_properties') IS NOT NULL
                BEGIN
                    SELECT TOP 200
                        Database_Name,
                        Object_Name,
                        stats_id,
                        last_updated,
                        rows,
                        rows_sampled,
                        CAST(100.0 * rows_sampled / NULLIF(rows, 0) AS DECIMAL(5,2)) AS sample_percent,
                        modification_counter,
                        CAST(100.0 * modification_counter / NULLIF(rows, 0) AS DECIMAL(5,2)) AS modification_percent,
                        persisted_sample_percent
                    FROM dbo.tbl_dm_db_stats_properties
                    {whereClause}
                    ORDER BY last_updated ASC;
                END";

            string title = filterByDb
                ? $"Table Statistics Health: {dbName}"
                : "Table Statistics Health (All User Databases)";
            return ExecuteQueryAndReturnJson(query, title);
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
