using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private static string _database = string.Empty;
        private static string? _database2;
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

                // Optional second SQL Nexus database used by the comparison tool. Accepts several
                // flag spellings for convenience: --database2, --database-for-comparison,
                // --database_for_comparison.
                var database2 = GetArgValue(args, "--database2")
                    ?? GetArgValue(args, "--database-for-comparison")
                    ?? GetArgValue(args, "--database_for_comparison")
                    ?? config["SqlNexus:Database2"];

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

                _database = database;
                _database2 = string.IsNullOrWhiteSpace(database2) ? null : database2.Trim();

                Logger.Initialize($"{ServerName} v{ServerVersion} started");
                Logger.Info($"Connected to: {server}/{database}");
                if (_database2 != null)
                    Logger.Info($"Comparison database: {_database2}");
                Logger.Info("Using Microsoft.Data.SqlClient");

                // Integrity gate: refuse to run if the AI guidance files (skill files + agent
                // definition) have been tampered with, are missing, or are unreadable.
                if (!FileIntegrityChecker.VerifyAll(out string integrityError))
                {
                    Logger.Error(integrityError);
                    Console.Error.WriteLine(integrityError);
                    Environment.Exit(2);
                    return;
                }

                ProcessRequests();
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal error", ex);
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

        internal static long GetRequiredInt64Argument(JObject arguments, string argumentName)
        {
            if (!arguments.TryGetValue(argumentName, out JToken token) || token.Type == JTokenType.Null)
                throw new ArgumentException($"{argumentName} parameter required");

            return token.Value<long>();
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
                    Logger.Error("Error processing request", ex);
                }
            }
        }

        static void ProcessSingleMessage(string json, StreamWriter writer)
        {
            var request = JsonConvert.DeserializeObject<JsonRpcRequest>(json);
            if (request == null)
                return;

            // Persist a small, truncated portion of every request for diagnostics
            Logger.LogRequest(request);

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
                Logger.Error($"Error handling method '{request.Method}'", ex);
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
                Logger.Info("Initializing SQL connection...");
                _analyzer = new DiagnosticAnalyzer(_connectionString, _database, _database2);
                Logger.Info("SQL connection initialized.");
            }
            return _analyzer;
        }

        static void HandleNotification(string method)
        {
            // notifications/initialized signals client is ready � no response required
            // Log other unexpected notifications for diagnostics only
            if (!string.Equals(method, "notifications/initialized", StringComparison.OrdinalIgnoreCase))
                Logger.Warn($"Notification received: {method}");
        }

        static object HandleInitialize(Dictionary<string, object>? parameters)
        {
            // Echo back the client's requested protocolVersion if provided (MCP version negotiation)
            string protocolVersion = "2024-11-05";
            if (parameters != null && parameters.TryGetValue("protocolVersion", out var clientVersion))
                protocolVersion = clientVersion?.ToString() ?? protocolVersion;

            string instructions =
                "AI-GENERATED CONTENT NOTICE: This server provides AI-assisted SQL Server diagnostic " +
                "analysis over pre-collected, read-only SQL Nexus data. Results are generated with the help " +
                "of AI and MAY BE INCOMPLETE OR INACCURATE. Always treat findings as a starting point, not a " +
                "definitive conclusion. Review the supporting evidence, validate every finding against the " +
                "underlying SQL Nexus tables (each tool response names its source tables and you can inspect " +
                "them with the 'query_nexus_database' tool), and review and edit any generated report before " +
                "sharing it. No production system is contacted and no data is modified.";

            // When a second database is configured, strongly steer the agent toward the dedicated
            // comparison tool. (An MCP server cannot force a tool call — invocation is the client's
            // decision — but announcing the configured comparison database here makes the agent
            // reliably choose 'compare_nexus_databases' for any comparison request.)
            if (!string.IsNullOrWhiteSpace(_database2))
            {
                instructions +=
                    $"\n\nCOMPARISON MODE ENABLED: A second SQL Nexus database ('{_database2}') is configured " +
                    $"alongside the primary database ('{_database}'). For ANY request to compare, diff, or contrast " +
                    "two databases, servers, captures, environments, or runs, you MUST call the " +
                    "'compare_nexus_databases' tool and base your answer on its returned result. Do NOT hand-write " +
                    "cross-database SQL through 'query_nexus_database' for these comparisons, and do NOT rely on " +
                    "prior conversation context for the database names — the authoritative names are " +
                    $"'{_database}' and '{_database2}'.";
            }

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
                },
                // Surfaced by the host to the user/model at connection time to set expectations
                // that all output is AI-assisted and may be inaccurate.
                Instructions = instructions
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
                    Name = "analyze_tracing_overhead",
                    Description = "Answer: 'Could active XEvent sessions or SQL Traces be causing unexplained high CPU?' Inspects tbl_XEvents and tbl_profiler_trace_event_details for: (1) events flagged expensive by SQL Nexus; (2) known high-frequency event names that fire at extreme volume under OLTP load (lock_acquired, wait_info, showplan variants, statement-level events); (3) concurrent user-session count (overhead is additive); (4) SQL Trace presence (deprecated since SQL Server 2012, synchronous single-threaded writes - always higher baseline cost than XEvents). Returns per-event risk ratings (Critical/High/Medium/Low), specific recommendations, and an overall assessment.",
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
                    Description = "Execute read-only custom SQL against the SQL Nexus database. Allows only SELECT/WITH/DECLARE/IF patterns in a single statement and blocks DDL, DML, EXEC/EXECUTE, permission changes, backup/restore, configuration changes, and external data-source commands.",
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
                    Description = "Per-database-file I/O statistics from tbl_FILESTATS: avg_read_latency_ms, avg_write_latency_ms, io_stall_read_ms, io_stall_write_ms per .mdf/.ldf/.ndf file. Thresholds: reads >20ms = slow, writes >10ms = slow for log. Distinct from analyze_io_performance (which uses Perfmon disk counters).",
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
                },
                new McpTool
                {
                    Name = "analyze_hadr_health",
                    Description = "Answer: 'Is Always On / HADR healthy?' Inspects the SQL Nexus HADR tables (tbl_hadr_ag_states, tbl_hadr_ag_database_replica_states, tbl_hadr_ag_listeners, tbl_hadr_alwayson_health_availability_group_lease_expired, tbl_hadr_alwayson_health_failovers, tbl_hadr_alwayson_health_availability_replica_state_change, tbl_hadr_dm_os_server_diagnostics_log_configurations) when present. Returns availability group states, per-database/per-replica synchronization states, listener configuration, AlwaysOn health-session events (failovers, lease expirations, replica state changes), and the server diagnostics log configuration. Missing tables are reported under tables_not_present; unhealthy/non-synchronized replicas, suspended data movement, failovers, and lease expirations are surfaced under issues_found.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "analyze_setup_health",
                    Description = "Answer: 'Are there SQL Server Setup / Install / Installation/ Update/ Upgrade problems?' Keywords: setup, install, installation, installed, patching, patch, MSI, MSP, repair, uninstall, components. Inspects the SQL Nexus setup/installation tables when present: tbl_installed_programs (filtered with name LIKE '%sql%') to enumerate installed SQL Server components and flag well-known components as present/missing; and tbl_setup_missing_msi_msp_packages, where ANY row indicates a missing Windows Installer MSI/MSP cached package that can block SQL Server patching, repair, or uninstall. Missing tables are reported under tables_not_present; missing MSI/MSP packages are surfaced under issues_found.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "compare_nexus_databases",
                    Description = "Answer: 'What is different between two SQL Nexus captures/servers/runs?' Compares the primary SQL Nexus database against a second one (configured via --database2 / --database-for-comparison). Produces side-by-side sections: (1) server_properties from tbl_ServerProperties with a Different flag; (2) database_options from tbl_database_options (name, compatibility level, status); (3) database_scoped_configurations from tbl_database_scoped_configurations with a Different flag; (4) sys_configurations from tbl_Sys_Configurations comparing value_in_use per sp_configure setting (differences only); and (5) query_performance comparison from ReadTrace.tblBatches/tblUniqueBatches (avg duration/CPU deltas per normalized query) when ReadTrace tables are present in both databases. Requires a second database to be configured at startup.",
                    InputSchema = new { type = "object", properties = new { } }
                }
            };

            // Log the authoritative catalog the server advertises so it can be searched in the
            // MCP server log for future troubleshooting. NOTE: the server always exposes every tool
            // here; the enable/disable checkboxes are a CLIENT-side UI setting the server never sees,
            // so a tool being unchecked in the host cannot be detected or logged server-side. To help
            // diagnose "missing tool" reports we log the full list and explicitly flag the presence of
            // the comparison tool and whether comparison mode is active.
            var toolNames = tools.Select(t => t.Name).ToList();
            var sortedToolNames = toolNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var toolList = string.Join(Environment.NewLine, sortedToolNames.Select((n, i) => $"  {i + 1,2}. {n}"));
            Logger.Info($"Advertised {toolNames.Count} tools (sorted):{Environment.NewLine}{toolList}");

            bool comparisonToolPresent = toolNames.Contains("compare_nexus_databases", StringComparer.OrdinalIgnoreCase);
            bool comparisonModeEnabled = !string.IsNullOrWhiteSpace(_database2);
            Logger.Info($"Comparison tool 'compare_nexus_databases' advertised: {comparisonToolPresent}; " +
                        $"comparison mode enabled (database2 configured): {comparisonModeEnabled}" +
                        (comparisonModeEnabled ? $" (database2='{_database2}')" : string.Empty));

            // Defensive: surface any tool that failed to be advertised (e.g., accidentally left with
            // an empty name) so it is easy to spot in the log.
            var unnamedTools = tools.Count(t => string.IsNullOrWhiteSpace(t.Name));
            if (unnamedTools > 0)
                Logger.Warn($"{unnamedTools} tool(s) were advertised without a name and may not be usable by the client.");

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

            var stopwatch = Stopwatch.StartNew();
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
                    resultText = GetAnalyzer().GetWaitsForQuery(GetRequiredInt64Argument(arguments, "hash_id"));
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
                case "analyze_tracing_overhead":
                    resultText = GetAnalyzer().AnalyzeTracingOverhead();
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
                    resultText = GetAnalyzer().GetQueryExecutionDetails(GetRequiredInt64Argument(arguments, "hash_id"));
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
                    resultText = GetAnalyzer().GetStatementsInBatch(GetRequiredInt64Argument(arguments, "batch_seq"));
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
                case "analyze_hadr_health":
                    resultText = GetAnalyzer().AnalyzeHadrHealth();
                    break;
                case "analyze_setup_health":
                    resultText = GetAnalyzer().AnalyzeSetupHealth();
                    break;
                case "compare_nexus_databases":
                    resultText = GetAnalyzer().CompareNexusDatabases();
                    break;
                default:
                    throw new NotSupportedException($"Tool not supported: {toolName}");
            }
            stopwatch.Stop();

            // Scrub PII from tool output before returning to the agent
            resultText = PiiScrubber.Scrub(resultText);

            // Log lightweight response telemetry for troubleshooting without logging full payloads.
            Logger.LogToolResult(toolName, resultText, stopwatch.ElapsedMilliseconds);

            // Append Responsible AI validation guidance so every answer encourages the user to
            // review the supporting evidence and inspect the underlying SQL Nexus tables.
            resultText = AppendValidationGuidance(resultText, toolName);

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = resultText }
                }
            };
        }

        // Maps each tool to the primary SQL Nexus table(s) it reads, so the guidance can point the
        // user at the exact data to validate against.
        private static readonly Dictionary<string, string[]> ToolSourceTables = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["get_top_queries_by_duration"]      = new[] { "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches" },
            ["analyze_cpu_usage"]                = new[] { "CounterData", "CounterDetails", "tbl_SQL_CPU_HEALTH", "tbl_ServerProperties" },
            ["get_top_cpu_queries"]              = new[] { "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches", "tbl_Hist_Top10_CPU_Queries_ByQueryHash" },
            ["analyze_io_performance"]           = new[] { "CounterData", "CounterDetails" },
            ["analyze_io_waits"]                 = new[] { "tbl_OS_WAIT_STATS", "tbl_ServerProperties" },
            ["analyze_wait_stats"]               = new[] { "tbl_OS_WAIT_STATS" },
            ["analyze_blocking"]                 = new[] { "tbl_HEADBLOCKERSUMMARY" },
            ["get_blocked_sessions"]             = new[] { "tbl_REQUESTS", "tbl_NOTABLEACTIVEQUERIES" },
            ["analyze_spinlocks"]                = new[] { "tbl_SPINLOCKSTATS", "tbl_ServerProperties" },
            ["get_collection_time_range"]        = new[] { "tbl_RUNTIMES", "ReadTrace.tblBatches" },
            ["get_waits_for_query"]              = new[] { "tbl_REQUESTS", "ReadTrace.tblBatches" },
            ["get_aggregate_waits_and_queries"]  = new[] { "tbl_REQUESTS", "tbl_NOTABLEACTIVEQUERIES" },
            ["get_missing_indexes"]              = new[] { "tbl_MissingIndexes" },
            ["get_sql_cpu_usage_over_time"]      = new[] { "CounterData", "CounterDetails", "tbl_ServerProperties" },
            ["get_memory_clerk_distribution"]    = new[] { "tbl_DM_OS_MEMORY_CLERKS" },
            ["analyze_tracing_overhead"]         = new[] { "tbl_XEvents", "tbl_profiler_trace_event_details", "tbl_profiler_trace_summary" },
            ["get_performance_summary"]          = new[] { "tbl_RUNTIMES", "tbl_OS_WAIT_STATS", "tbl_HEADBLOCKERSUMMARY", "tbl_DM_OS_MEMORY_CLERKS", "CounterData" },
            ["list_nexus_tables"]                = new[] { "sys.tables (INFORMATION_SCHEMA.TABLES)" },
            ["query_nexus_database"]             = new string[0],
            ["get_query_execution_details"]      = new[] { "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches" },
            ["get_wait_type_distribution"]       = new[] { "tbl_REQUESTS" },
            ["get_wait_resource_hotspots"]       = new[] { "tbl_REQUESTS" },
            ["get_wait_heavy_queries"]           = new[] { "tbl_REQUESTS", "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches" },
            ["get_statements_in_batch"]          = new[] { "ReadTrace.tblStatements", "ReadTrace.tblUniqueStatements" },
            ["get_blocking_chain_tree"]          = new[] { "tbl_BLOCKING_CHAINS", "tbl_REQUESTS" },
            ["get_lock_summary_by_object"]       = new[] { "tbl_BLOCKING_CHAINS", "tbl_REQUESTS" },
            ["get_queries_by_application"]       = new[] { "ReadTrace.tblBatches", "ReadTrace.tblConnections", "ReadTrace.tblUniqueBatches" },
            ["get_performance_by_application"]   = new[] { "ReadTrace.tblBatchPartialAggs", "ReadTrace.tblUniqueAppNames", "ReadTrace.tblBatches", "ReadTrace.tblConnections" },
            ["get_cpu_by_database"]              = new[] { "ReadTrace.tblBatches" },
            ["get_top_queries_by_reads"]         = new[] { "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches", "tbl_Hist_Top10_LogicalReads_Queries_ByQueryHash" },
            ["get_top_queries_by_writes"]        = new[] { "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches" },
            ["get_sql_file_io_stats"]            = new[] { "tbl_FileStats" },
            ["get_compilation_stats"]            = new[] { "CounterData", "CounterDetails", "tbl_CACHEOBJECTS" },
            ["get_plan_cache_analysis"]          = new[] { "tbl_CACHEOBJECTS" },
            ["get_table_statistics_health"]      = new[] { "tbl_dm_db_stats_properties" },
            ["analyze_hadr_health"]              = new[]
            {
                "tbl_hadr_ag_states", "tbl_hadr_ag_database_replica_states", "tbl_hadr_ag_listeners",
                "tbl_hadr_alwayson_health_availability_group_lease_expired", "tbl_hadr_alwayson_health_failovers",
                "tbl_hadr_alwayson_health_availability_replica_state_change", "tbl_hadr_dm_os_server_diagnostics_log_configurations"
            },
            ["analyze_setup_health"]             = new[] { "tbl_installed_programs", "tbl_setup_missing_msi_msp_packages" },
            ["compare_nexus_databases"]          = new[] { "tbl_ServerProperties", "tbl_database_options", "tbl_database_scoped_configurations", "tbl_Sys_Configurations", "ReadTrace.tblBatches", "ReadTrace.tblUniqueBatches" },
        };

        // Short, model-facing disclaimer surfaced with every result.
        private const string AiGeneratedNotice =
            "⚠️ AI-GENERATED — MAY BE INACCURATE. This analysis was produced with AI assistance from " +
            "pre-collected SQL Nexus data and may be incomplete or incorrect. It is a starting point for " +
            "investigation, not a definitive conclusion. You MUST surface this notice to the user and never " +
            "omit it when summarizing this result.";

        private static readonly string[] ValidationSteps =
        {
            "Review the supporting evidence and the specific data values cited in this result.",
            "Inspect the underlying SQL Nexus source table(s) directly (see validate_against_tables) using the 'query_nexus_database' tool, e.g. SELECT TOP 100 * FROM <table>.",
            "Ask follow-up questions, or narrow/broaden the investigation scope if the result looks off.",
            "Adjust your prompt or the investigation scope to explore alternative root causes.",
            "Review and edit any generated report before sharing it with others."
        };

        /// <summary>
        /// Attaches a Responsible AI validation notice to a tool's result. When the result is JSON,
        /// the notice is injected as structured, top-level fields (ai_generated_notice,
        /// validate_against_tables, validation_steps) so it travels with the data and is far less
        /// likely to be dropped when the model paraphrases the output. For non-JSON payloads, or if
        /// JSON parsing fails, it falls back to appending the notice as trailing text.
        /// </summary>
        private static string AppendValidationGuidance(string resultText, string toolName)
        {
            ToolSourceTables.TryGetValue(toolName, out var tables);
            tables ??= Array.Empty<string>();

            // Preferred path: embed the notice as first-class JSON fields.
            var trimmed = resultText?.TrimStart();
            if (!string.IsNullOrEmpty(trimmed) && (trimmed[0] == '{' || trimmed[0] == '['))
            {
                try
                {
                    var token = JToken.Parse(resultText);

                    // Build a validation object that hosts/models see as part of the data.
                    var validation = new JObject
                    {
                        ["ai_generated_notice"] = AiGeneratedNotice,
                        ["validate_against_tables"] = new JArray(tables),
                        ["validation_steps"] = new JArray(ValidationSteps)
                    };

                    if (token is JObject obj)
                    {
                        // Insert the notice at the very top so it is the first thing seen.
                        var withNotice = new JObject { ["ai_generated_notice"] = AiGeneratedNotice };
                        foreach (var prop in obj.Properties())
                            withNotice.Add(prop.Name, prop.Value);
                        withNotice["responsible_ai_validation"] = validation;
                        return withNotice.ToString(Formatting.Indented);
                    }

                    if (token is JArray arr)
                    {
                        // Wrap arrays so we can attach the notice without losing the payload.
                        var wrapper = new JObject
                        {
                            ["ai_generated_notice"] = AiGeneratedNotice,
                            ["data"] = arr,
                            ["responsible_ai_validation"] = validation
                        };
                        return wrapper.ToString(Formatting.Indented);
                    }
                }
                catch (JsonException)
                {
                    // Fall through to text append if the payload is not valid JSON.
                }
            }

            // Fallback path: append the notice as trailing text.
            string tablesLine;
            if (tables.Length > 0)
            {
                tablesLine = $"- Inspect the underlying SQL Nexus source table(s) for this analysis: {string.Join(", ", tables)}. "
                           + "You can query them directly with the 'query_nexus_database' tool (e.g. SELECT TOP 100 * FROM <table>).";
            }
            else if (string.Equals(toolName, "query_nexus_database", StringComparison.OrdinalIgnoreCase))
            {
                tablesLine = "- Re-run or refine this query, and cross-check results against related SQL Nexus tables. "
                           + "Use the 'list_nexus_tables' tool to discover other relevant tables.";
            }
            else
            {
                tablesLine = "- Inspect the relevant SQL Nexus source tables directly with the 'query_nexus_database' tool. "
                           + "Use the 'list_nexus_tables' tool to discover which tables apply.";
            }

            var guidance =
                "\n\n---\n" +
                "⚠️ AI-GENERATED — MAY BE INACCURATE. VALIDATE THIS ANALYSIS.\n" +
                "This result was produced with AI assistance from pre-collected SQL Nexus data and may be " +
                "incomplete or incorrect. It is a starting point for investigation, not a definitive conclusion. " +
                "Before acting on it or sharing a report, please:\n" +
                "- Review the supporting evidence and the specific data values cited above.\n" +
                tablesLine + "\n" +
                "- Ask follow-up questions, or narrow/broaden the investigation scope if the result looks off.\n" +
                "- Adjust your prompt or the investigation scope to explore alternative root causes.\n" +
                "- Review and edit any generated report before sharing it with others.\n";

            return resultText + guidance;
        }
    }
}
