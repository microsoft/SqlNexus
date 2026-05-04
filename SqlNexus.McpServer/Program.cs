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

                // Store connection string — defer actual SQL connection until first tool call
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

            // Notifications have no id — handle but never write a response
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
            // notifications/initialized signals client is ready — no response required
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
                    Description = "Answer: 'Is there high CPU on this system?' Returns two sections: (1) CPU-related wait types (SOS_SCHEDULER_YIELD, CXPACKET, CXCONSUMER) from tbl_OS_WAIT_STATS, and (2) Perfmon CPU summary from CounterData including max SQL CPU %, average SQL CPU %, max total system CPU %, and how many Perfmon samples exceeded 70% CPU threshold.",
                    InputSchema = new { type = "object", properties = new { } }
                },
                new McpTool
                {
                    Name = "get_top_cpu_queries",
                    Description = "Get top CPU-consuming queries from tbl_NOTABLEACTIVEQUERIES. Answer: 'Which queries are causing high CPU?'",
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
                    Description = "Answer: 'Is SQL Server the contributing factor to slow I/O?' Shows PAGEIOLATCH, WRITELOG, IO_COMPLETION waits.",
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
                    Description = "Get the overall PSSDiag/SQLLogScout data collection time range.",
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
                case "query_nexus_database":
                    resultText = GetAnalyzer().ExecuteCustomQuery(
                        arguments.Value<string>("query") ?? throw new ArgumentException("Query parameter required"));
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
