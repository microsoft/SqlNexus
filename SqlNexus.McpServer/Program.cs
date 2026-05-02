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

                var server = config["SqlNexus:Server"] ?? "localhost";
                var database = config["SqlNexus:Database"] ?? "SqlNexus";
                var trustedConnectionStr = config["SqlNexus:TrustedConnection"];
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

                _analyzer = new DiagnosticAnalyzer(builder.ConnectionString);

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

        static void ProcessRequests()
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            while (true)
            {
                try
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var request = JsonConvert.DeserializeObject<JsonRpcRequest>(line);
                    if (request == null)
                        continue;

                    var response = HandleRequest(request);
                    var responseJson = JsonConvert.SerializeObject(response);
                    writer.WriteLine(responseJson);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing request: {ex.Message}");
                }
            }
        }

        static JsonRpcResponse HandleRequest(JsonRpcRequest request)
        {
            try
            {
                var result = request.Method switch
                {
                    "initialize" => HandleInitialize(),
                    "tools/list" => HandleListTools(),
                    "tools/call" => HandleToolCall(request.Params),
                    _ => throw new NotSupportedException($"Method not supported: {request.Method}")
                };

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

        static object HandleInitialize()
        {
            return new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
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
                    Description = "Answer: 'Is there high CPU on this system?' Analyzes CPU-related wait types (SOS_SCHEDULER_YIELD, CXPACKET) from sys.dm_os_wait_stats.",
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

            string resultText = toolName switch
            {
                "get_top_queries_by_duration" => _analyzer!.GetTopQueriesByDuration(
                    arguments.Value<int?>("top_n") ?? 50),

                "analyze_cpu_usage" => _analyzer!.AnalyzeCpuUsage(),

                "get_top_cpu_queries" => _analyzer!.GetTopCpuQueries(
                    arguments.Value<int?>("top_n") ?? 20),

                "analyze_io_performance" => _analyzer!.AnalyzeIoPerformance(
                    arguments.Value<decimal?>("threshold_ms") ?? 20.0m),

                "analyze_io_waits" => _analyzer!.AnalyzeIoWaits(),

                "analyze_wait_stats" => _analyzer!.AnalyzeWaitStats(),

                "analyze_blocking" => _analyzer!.AnalyzeBlocking(),

                "get_blocked_sessions" => _analyzer!.GetBlockedSessions(),

                "analyze_spinlocks" => _analyzer!.AnalyzeSpinlocks(),

                "get_collection_time_range" => _analyzer!.GetCollectionTimeRange(),

                "get_waits_for_query" => _analyzer!.GetWaitsForQuery(
                    arguments.Value<long>("hash_id")),

                "get_aggregate_waits_and_queries" => _analyzer!.GetAggregateWaitsAndQueries(),

                "get_missing_indexes" => _analyzer!.GetMissingIndexes(
                    arguments.Value<int?>("top_n") ?? 30),

                "get_sql_cpu_usage_over_time" => _analyzer!.GetSqlCpuUsageOverTime(),

                "get_memory_clerk_distribution" => _analyzer!.GetMemoryClerkDistribution(),

                "get_performance_summary" => _analyzer!.GetPerformanceSummary(),

                "query_nexus_database" => _analyzer!.ExecuteCustomQuery(
                    arguments.Value<string>("query") ?? throw new ArgumentException("Query parameter required")),

                _ => throw new NotSupportedException($"Tool not supported: {toolName}")
            };

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
