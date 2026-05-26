using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqlNexus.McpServer
{
    class Program
    {
        private static DiagnosticAnalyzer? _analyzer;
        private static string _connectionString = string.Empty;
        private static readonly string ServerName = "sqlnexus-mcp-server";
        private static readonly string ServerVersion = "1.0.0";

        static void Main(string[] args)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Command-line args take priority: --server <name> --database <name> --trusted-connection <true|false>
                var server = GetArgValue(args, "--server")
                    ?? config["SqlNexus:Server"]
                    ?? "localhost";

                var database = GetArgValue(args, "--database")
                    ?? config["SqlNexus:Database"]
                    ?? "SqlNexus";

                var trustedConnectionStr = GetArgValue(args, "--trusted-connection")
                    ?? config["SqlNexus:TrustedConnection"];
                var trustedConnection = string.IsNullOrEmpty(trustedConnectionStr) || bool.Parse(trustedConnectionStr);

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    IntegratedSecurity = trustedConnection,
                    TrustServerCertificate = true,
                    ApplicationName = ServerName,
                    ConnectTimeout = 30
                };

                if (!trustedConnection)
                {
                    builder.UserID = config["SqlNexus:UserId"];
                    builder.Password = config["SqlNexus:Password"];
                }

                // Store connection string � defer actual SQL connection until first tool call
                _connectionString = builder.ConnectionString;

                Console.Error.WriteLine($"{ServerName} v{ServerVersion} started");
                Console.Error.WriteLine($"Connected to: {server}/{database}");
                Console.Error.WriteLine($"Using Microsoft.Data.SqlClient");

                ProcessRequests();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static string? GetArgValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        static void ProcessRequests()
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            while (true)
            {
                try
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;
                    if (line.Length == 0)
                        continue;

                    // Handle JSON-RPC batch (array) or single request
                    if (line.TrimStart().StartsWith("["))
                    {
                        var batch = JArray.Parse(line);
                        foreach (var token in batch)
                        {
                            ProcessSingleMessage(token.ToString(), writer);
                        }
                    }
                    else
                    {
                        ProcessSingleMessage(line, writer);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing request: {ex.Message}");
                }
            }
        }

        static void ProcessSingleMessage(string json, StreamWriter writer)
        {
            var request = JsonConvert.DeserializeObject<JsonRpcRequest>(json);
            if (request == null)
                return;

            // Notifications have no id � handle but never write a response
            bool isNotification = request.Id == null;
            var response = HandleRequest(request, isNotification);
            if (!isNotification && response != null)
            {
                writer.WriteLine(JsonConvert.SerializeObject(response));
            }
        }

        static JsonRpcResponse? HandleRequest(JsonRpcRequest request, bool isNotification = false)
        {
            try
            {
                // Notifications (no id) must never receive a response
                if (isNotification)
                {
                    HandleNotification(request.Method);
                    return null;
                }

                object result;
                switch (request.Method)
                {
                    case "initialize":
                        result = HandleInitialize(request.Params);
                        break;
                    case "tools/list":
                        result = HandleListTools();
                        break;
                    case "tools/call":
                        result = HandleToolCall(request.Params);
                        break;
                    default:
                        throw new NotSupportedException($"Method not supported: {request.Method}");
                }

                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = ex.Message,
                        Data = ex.StackTrace
                    }
                };
            }
        }

        static DiagnosticAnalyzer GetAnalyzer()
        {
            if (_analyzer == null)
            {
                Console.Error.WriteLine("Initializing SQL connection...");
                _analyzer = new DiagnosticAnalyzer(_connectionString);
                Console.Error.WriteLine("SQL connection initialized.");
            }
            return _analyzer;
        }

        static void HandleNotification(string method)
        {
            // notifications/initialized signals client is ready � no response required
            // Log other unexpected notifications for diagnostics only
            if (!string.Equals(method, "notifications/initialized", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine($"Notification received: {method}");
        }

        static object HandleInitialize(Dictionary<string, object>? parameters)
        {
            // Echo back the client's requested protocolVersion if provided (MCP version negotiation)
            string protocolVersion = "2024-11-05";
            if (parameters != null && parameters.TryGetValue("protocolVersion", out var clientVersion))
                protocolVersion = clientVersion?.ToString() ?? protocolVersion;

            return new InitializeResult
            {
                ProtocolVersion = protocolVersion,
                ServerInfo = new ServerInfo
                {
                    Name = ServerName,
                    Version = ServerVersion
                },
                Capabilities = new ServerCapabilities
                {
                    Tools = new Dictionary<string, object>()
                }
            };
        }

        static object HandleListTools()
        {
            var tools = new List<McpTool>
            {
                new McpTool
                {
                    Name = "get_top_queries_by_duration",
                    Description = "Get top N longest-running queries by duration with aggregate statistics. Essential for identifying slow queries.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            top_n = new { type = "number", description = "Number of top queries (default: 50)", @default = 50 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "analyze_cpu_usage",
                    Description = "Answer: 'Is there high CPU on this system?' Queries per-sample CPU data from CounterData (Perfmon) if available, falling back to tbl_SQL_CPU_HEALTH ring-buffer data. Returns: (1) a perfmon_cpu_summary with max/avg SQL CPU %, max/avg total CPU %, sample counts above 70%, and any sustained high-CPU runs (3 or more consecutive samples above 70% SQL CPU); (2) the raw per-sample breakdown of sql_cpu_pct, nonsql_cpu_pct, and system_idle_pct.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_top_cpu_queries",
                    Description = "Answer: 'Which queries are causing high CPU?' If ReadTrace.tblBatches is present, aggregates total_cpu_ms, pct_of_cpu_capacity, avg_cpu_ms, executions, reads, writes, and statement text from tblBatches/tblUniqueBatches. Otherwise falls back to tbl_Hist_Top10_CPU_Queries_ByQueryHash using a delta between the first and last snapshot to isolate CPU consumed only during the collection window.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            top_n = new { type = "number", description = "Number of top queries (default: 20)", @default = 20 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "analyze_io_performance",
                    Description = "Answer: 'Is I/O slow?' Analyzes disk I/O latency from Perfmon counters (Avg. Disk sec/Transfer).",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            threshold_ms = new { type = "number", description = "I/O latency threshold in ms (default: 20)", @default = 20 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "analyze_io_waits",
                    Description = "Answer: 'Is SQL Server the contributing factor to slow I/O?' Shows delta wait time and wait-time-per-second-per-CPU for PAGEIOLATCH_*, WRITELOG, LOGBUFFER, IO_COMPLETION, and ASYNC_IO_COMPLETION wait types between the first and last tbl_OS_WAIT_STATS snapshots.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "analyze_wait_stats",
                    Description = "Overall bottleneck analysis - top wait categories causing performance issues.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "analyze_blocking",
                    Description = "Find head blockers and blocking chains. Shows who is blocking whom and for how long.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_blocked_sessions",
                    Description = "Get all blocked sessions and the queries they are running.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "analyze_spinlocks",
                    Description = "Analyze spinlock contention. High spins indicate CPU bottlenecks from internal SQL Server latches.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_collection_time_range",
                    Description = "Get the overall data collection time range (start, end, duration in minutes) from ReadTrace.tblBatches. Returns no data if ReadTrace was not part of the collection (e.g., SQLLogScout-only captures without a trace/XEvent session).",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_waits_for_query",
                    Description = "Find what wait types a specific query (by HashID) encountered during execution.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            hash_id = new { type = "number", description = "HashID from ReadTrace.tblBatches" }
                        },
                        required = new[] { "hash_id" }
                    }
                },
                new McpTool
                {
                    Name = "get_aggregate_waits_and_queries",
                    Description = "Aggregate view of waits and the queries that experienced them. Useful for correlation analysis.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_missing_indexes",
                    Description = "Get missing index recommendations from sys.dm_db_missing_index_details captured during collection.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            top_n = new { type = "number", description = "Number of recommendations (default: 30)", @default = 30 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_sql_cpu_usage_over_time",
                    Description = "Get SQL Server CPU usage over time from Perfmon data. Shows CPU % used by SQL vs. other processes.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_memory_clerk_distribution",
                    Description = "Get SQL Server memory distribution by memory clerk type. Useful for memory pressure analysis.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_performance_summary",
                    Description = "Comprehensive performance summary: CPU, I/O, blocking, waits, spinlocks, memory. One-stop health check.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "list_nexus_tables",
                    Description = "Returns a curated catalog of the most analytically significant SQL Nexus tables with plain-English descriptions and a flag indicating whether each table is present in the connected database. IMPORTANT: this is a known-good subset, not a complete list � the database may contain additional tables not covered here. To discover every table actually present, use query_nexus_database with: SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "query_nexus_database",
                    Description = "Execute custom SQL queries against SQL Nexus database. Supports SELECT, WITH (CTE), DECLARE, and IF statements.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "SQL query to execute" }
                        },
                        required = new[] { "query" }
                    }
                },
                // ── New tools covering previously missing skill queries ─────────────
                new McpTool
                {
                    Name = "get_query_execution_details",
                    Description = "Drill into a specific query by HashID — shows each individual execution with Duration_ms, CPU_ms, WaitTime_ms, WaitPct, Reads, Writes, RowCounts. Use after get_top_queries_by_duration or get_top_cpu_queries to investigate a specific slow query.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            hash_id = new { type = "number", description = "HashID from ReadTrace.tblBatches (from get_top_queries_by_duration or get_top_cpu_queries)" }
                        },
                        required = new[] { "hash_id" }
                    }
                },
                new McpTool
                {
                    Name = "get_wait_type_distribution",
                    Description = "Request-level wait type frequency distribution from tbl_REQUESTS. Complements analyze_wait_stats (which is system-level). Shows occurrences, avg/max/total wait ms, and % of total wait per wait type across all captured requests.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_wait_resource_hotspots",
                    Description = "Find specific resources (pages, rows, objects, keys) with highest lock/latch contention. Groups tbl_REQUESTS by wait_resource to identify the hot table, page, or row. wait_resource format: PAGE: dbid:fileid:pageid, KEY: ..., OBJECT: ..., RID: ...",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_wait_heavy_queries",
                    Description = "Find queries spending most time waiting vs executing (wait-bound queries, CPU < 80% of duration). Sorted by total wait time. Shows AvgWaitPct, wait_type, and query text. Use to identify queries bottlenecked by I/O, locks, or memory grants.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_statements_in_batch",
                    Description = "Break down a batch into individual statements for statement-level performance analysis. Requires DetailedPerf collection (ReadTrace.tblStatements). Use after get_top_queries_by_duration to find the slow statement inside a stored procedure.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            batch_seq = new { type = "number", description = "BatchSeq value from ReadTrace.tblBatches (the row identifier of the specific batch execution)" }
                        },
                        required = new[] { "batch_seq" }
                    }
                },
                new McpTool
                {
                    Name = "get_blocking_chain_tree",
                    Description = "Full recursive blocking chain hierarchy: root blocker (level 0) through all downstream blocked sessions. Shows blocking_hierarchy with indentation. Use for complex multi-level blocking scenarios where analyze_blocking shows many blocked sessions.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_lock_summary_by_object",
                    Description = "Lock contention summary grouped by database object/resource. Shows which specific tables, pages, or rows have the most lock_count and total_wait_ms. Use to find hotspot tables driving blocking.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_queries_by_application",
                    Description = "Find queries executed by a specific application name (from connection string ApplicationName). Returns aggregate stats per query: Executions, Total/Avg Duration_ms, CPU_ms, Reads, Writes. Pass null/empty app_name to get top queries across all applications with AppName column.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            app_name = new { type = "string", description = "Application name to filter by (e.g. '.Net SqlClient Data Provider', 'SSMS'). Leave empty for all applications." }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_performance_by_application",
                    Description = "Aggregate performance metrics grouped by application name: Duration_ms, CPU_ms, Reads, Writes, Unique_Queries, and percentage of total server resources (Pct_Total_Duration, Pct_Total_CPU, Pct_Total_Reads). Use to identify which application is the biggest resource consumer.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_cpu_by_database",
                    Description = "CPU consumption breakdown by database on the SQL Server instance. Shows Total_CPU_ms, Executions, Avg_CPU_ms, and CPU_Pct per database. Use when multiple databases share an instance and you need to narrow focus to a specific database.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_top_queries_by_reads",
                    Description = "Top queries sorted by physical/logical reads — identifies I/O-intensive queries causing PAGEIOLATCH_* waits. Shows Total_Reads, Avg_Reads, Executions, Total_Duration_ms. Use when analyze_io_waits shows high PAGEIOLATCH_SH waits.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            top_n = new { type = "number", description = "Number of top queries to return (default: 50)", @default = 50 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_top_queries_by_writes",
                    Description = "Top queries sorted by writes — identifies write-heavy queries causing WRITELOG waits or log file pressure. Shows Total_Writes, Avg_Writes, Total_Rows_Affected, Total_Duration_ms. Use when analyze_io_waits shows high WRITELOG waits.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            top_n = new { type = "number", description = "Number of top queries to return (default: 50)", @default = 50 }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_sql_file_io_stats",
                    Description = "Per-database-file I/O statistics from tbl_FILE_STATS: avg_read_latency_ms, avg_write_latency_ms, io_stall_read_ms, io_stall_write_ms per .mdf/.ldf/.ndf file. Thresholds: reads >20ms = slow, writes >10ms = slow for log. Distinct from analyze_io_performance (which uses Perfmon disk counters).",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_compilation_stats",
                    Description = "SQL compilations and recompilations per second from Perfmon CounterData, plus plan cache composition from tbl_CACHEOBJECTS. High compilations/sec (>100) indicates ad-hoc queries or plan cache pressure. avg_use_count ≈ 1 = plans used once and discarded.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_plan_cache_analysis",
                    Description = "Plan cache composition from tbl_CACHEOBJECTS: plan_count, cache_size_mb, avg_use_count, single_use_plans, single_use_pct per objtype/cacheobjtype. High single_use_pct indicates ad-hoc query workload causing compilation CPU overhead.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_table_statistics_health",
                    Description = "Table statistics health from tbl_dm_db_stats_properties: last_updated, rows, sample_percent, modification_counter, modification_percent. Stale statistics (modification_percent > 20%, last_updated > 7 days) cause bad query plans. Optionally filter by database name.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            db_name = new { type = "string", description = "Database name to filter (optional, leave empty for all user databases)" }
                        }
                    }
                }
            };

            return new { tools };
        }

        static object HandleToolCall(Dictionary<string, object>? parameters)
        {
            if (parameters == null || !parameters.ContainsKey("name"))
                throw new ArgumentException("Tool name not specified");

            var toolName = parameters["name"].ToString()!;
            var arguments = parameters.ContainsKey("arguments") 
                ? JObject.FromObject(parameters["arguments"]) 
                : new JObject();

            string resultText;
            switch (toolName)
            {
                case "get_top_queries_by_duration":
                    resultText = GetAnalyzer().GetTopQueriesByDuration(arguments.Value<int?>("top_n") ?? 50);
                    break;
                case "analyze_cpu_usage":
                    resultText = GetAnalyzer().AnalyzeCpuUsage();
                    break;
                case "get_top_cpu_queries":
                    resultText = GetAnalyzer().GetTopCpuQueries(arguments.Value<int?>("top_n") ?? 20);
                    break;
                case "analyze_io_performance":
                    resultText = GetAnalyzer().AnalyzeIoPerformance(arguments.Value<decimal?>("threshold_ms") ?? 20.0m);
                    break;
                case "analyze_io_waits":
                    resultText = GetAnalyzer().AnalyzeIoWaits();
                    break;
                case "analyze_wait_stats":
                    resultText = GetAnalyzer().AnalyzeWaitStats();
                    break;
                case "analyze_blocking":
                    resultText = GetAnalyzer().AnalyzeBlocking();
                    break;
                case "get_blocked_sessions":
                    resultText = GetAnalyzer().GetBlockedSessions();
                    break;
                case "analyze_spinlocks":
                    resultText = GetAnalyzer().AnalyzeSpinlocks();
                    break;
                case "get_collection_time_range":
                    resultText = GetAnalyzer().GetCollectionTimeRange();
                    break;
                case "get_waits_for_query":
                    resultText = GetAnalyzer().GetWaitsForQuery(arguments.Value<long>("hash_id"));
                    break;
                case "get_aggregate_waits_and_queries":
                    resultText = GetAnalyzer().GetAggregateWaitsAndQueries();
                    break;
                case "get_missing_indexes":
                    resultText = GetAnalyzer().GetMissingIndexes(arguments.Value<int?>("top_n") ?? 30);
                    break;
                case "get_sql_cpu_usage_over_time":
                    resultText = GetAnalyzer().GetSqlCpuUsageOverTime();
                    break;
                case "get_memory_clerk_distribution":
                    resultText = GetAnalyzer().GetMemoryClerkDistribution();
                    break;
                case "get_performance_summary":
                    resultText = GetAnalyzer().GetPerformanceSummary();
                    break;
                case "list_nexus_tables":
                    resultText = GetAnalyzer().ListNexusTables();
                    break;
                case "query_nexus_database":
                    resultText = GetAnalyzer().ExecuteCustomQuery(
                        arguments.Value<string>("query") ?? throw new ArgumentException("Query parameter required"));
                    break;
                // ── New tools ────────────────────────────────────────────────────
                case "get_query_execution_details":
                    resultText = GetAnalyzer().GetQueryExecutionDetails(arguments.Value<long>("hash_id"));
                    break;
                case "get_wait_type_distribution":
                    resultText = GetAnalyzer().GetWaitTypeDistribution();
                    break;
                case "get_wait_resource_hotspots":
                    resultText = GetAnalyzer().GetWaitResourceHotspots();
                    break;
                case "get_wait_heavy_queries":
                    resultText = GetAnalyzer().GetWaitHeavyQueries();
                    break;
                case "get_statements_in_batch":
                    resultText = GetAnalyzer().GetStatementsInBatch(arguments.Value<long>("batch_seq"));
                    break;
                case "get_blocking_chain_tree":
                    resultText = GetAnalyzer().GetBlockingChainTree();
                    break;
                case "get_lock_summary_by_object":
                    resultText = GetAnalyzer().GetLockSummaryByObject();
                    break;
                case "get_queries_by_application":
                    resultText = GetAnalyzer().GetQueriesByApplication(arguments.Value<string?>("app_name"));
                    break;
                case "get_performance_by_application":
                    resultText = GetAnalyzer().GetPerformanceByApplication();
                    break;
                case "get_cpu_by_database":
                    resultText = GetAnalyzer().GetCpuByDatabase();
                    break;
                case "get_top_queries_by_reads":
                    resultText = GetAnalyzer().GetTopQueriesByReads(arguments.Value<int?>("top_n") ?? 50);
                    break;
                case "get_top_queries_by_writes":
                    resultText = GetAnalyzer().GetTopQueriesByWrites(arguments.Value<int?>("top_n") ?? 50);
                    break;
                case "get_sql_file_io_stats":
                    resultText = GetAnalyzer().GetSqlFileIoStats();
                    break;
                case "get_compilation_stats":
                    resultText = GetAnalyzer().GetCompilationStats();
                    break;
                case "get_plan_cache_analysis":
                    resultText = GetAnalyzer().GetPlanCacheAnalysis();
                    break;
                case "get_table_statistics_health":
                    resultText = GetAnalyzer().GetTableStatisticsHealth(arguments.Value<string?>("db_name"));
                    break;
                default:
                    throw new NotSupportedException($"Tool not supported: {toolName}");
            }

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = resultText }
                }
            };
        }
    }
}
