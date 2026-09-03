#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Lightweight, self-contained file logger for the SQL Nexus MCP server.
    ///
    /// It is intentionally independent of the main SQL Nexus log (%TEMP%\sqlnexus.log):
    /// the MCP server writes to its own file (%TEMP%\sqlnexus_mcpserver.log) in the same
    /// %TEMP% folder so the two logs never intermix, yet live side by side.
    ///
    /// Every line is prefixed with a local date/time stamp as the first column, e.g.:
    ///     2025-01-15 14:03:22.187  [INFO ]  sqlnexus-mcp-server v1.0.0 started
    ///
    /// Design constraints specific to a stdio-based MCP server:
    ///  - stdout is reserved exclusively for JSON-RPC traffic, so this logger NEVER
    ///    writes there. The selectable "console" target therefore resolves to stderr,
    ///    the protocol-safe diagnostic stream that MCP hosts capture as server logs.
    ///  - Each call can be directed to the log file, the console, or both via the
    ///    optional <see cref="LogTarget"/> parameter (modelled on SQL Nexus's
    ///    [Flags] MessageOptions). The default is <see cref="LogTarget.Both"/>, which
    ///    preserves the server's original file + console behaviour.
    ///  - Logging must never throw. A failure to write the log can never be allowed
    ///    to crash the server or corrupt the protocol stream, so every write is
    ///    best-effort and swallows its own exceptions.
    /// </summary>
    public static class Logger
    {
        public enum Level { Info, Warn, Error }

        /// <summary>
        /// Destination(s) for a log line. Modelled on SQL Nexus's [Flags] MessageOptions
        /// so a caller can send output to the log file, the console, or both.
        /// </summary>
        [Flags]
        public enum LogTarget
        {
            /// <summary>Write to the %TEMP%\sqlnexus_mcpserver.log file only.</summary>
            File = 1,

            /// <summary>
            /// Write to the console only. Resolves to stderr because stdout carries the
            /// JSON-RPC protocol stream and must never receive log text.
            /// </summary>
            Console = 2,

            /// <summary>Write to both the log file and the console.</summary>
            Both = File | Console
        }

        // Log file lives next to the SQL Nexus log (%TEMP%) but uses its own name.
        private const string LogFileName = "sqlnexus_mcpserver.log";

        // Requests and results can be large and may embed SQL text or PII, so only a
        // short prefix is persisted. This keeps the log a lightweight audit trail.
        private const int MaxPayloadChars = 300;

        // Simple size-based rollover so the temp file can't grow without bound.
        private const long MaxLogBytes = 5 * 1024 * 1024; // 5 MB
        private const int MaxBackupFiles = 5;

        private static readonly object s_sync = new object();
        private static string s_logFilePath = LogFileName;
        private static bool s_initialized;

        // SQL Server errors that are an expected part of probing the optional SQL Nexus
        // schema — the message alone is self-explanatory, so their (large) call stacks
        // are suppressed to keep the log compact. Many tools query tables that may not
        // exist in a given capture, producing "Invalid object name" by design.
        private static readonly HashSet<int> s_expectedSqlErrors = new HashSet<int>
        {
            208,  // Invalid object name (table/view not present in this capture)
            207,  // Invalid column name
            2812, // Could not find stored procedure
            4104, // The multi-part identifier could not be bound
        };

        /// <summary>Full path of the active log file (resolved on first use).</summary>
        public static string LogFilePath => s_logFilePath;

        /// <summary>
        /// Resolve the log file path and emit a startup banner. Safe to call more than
        /// once; the path is resolved a single time. The banner text is supplied by the
        /// caller so the logger stays free of server-specific constants.
        /// </summary>
        public static void Initialize(string banner)
        {
            EnsureInitialized();
            Info("========================================================");
            Info(banner);
            Info($"Log file: {s_logFilePath}");
            Info("========================================================");
        }

        public static void Info(string message, LogTarget target = LogTarget.Both)  => Write(Level.Info, message, target);
        public static void Warn(string message, LogTarget target = LogTarget.Both)  => Write(Level.Warn, message, target);
        public static void Error(string message, LogTarget target = LogTarget.Both) => Write(Level.Error, message, target);

        /// <summary>
        /// Log an error with a concise, single-line description. The full (and often large)
        /// call stack is appended only for genuinely unexpected exceptions — routine, self-
        /// explanatory errors such as SQL "Invalid object name" (raised while probing for
        /// optional SQL Nexus tables) are logged without a stack to keep the log compact.
        /// </summary>
        public static void Error(string message, Exception ex, LogTarget target = LogTarget.Both)
        {
            Write(Level.Error, $"{message} | {DescribeException(ex)}", target);

            if (!IsExpected(ex) && !string.IsNullOrEmpty(ex.StackTrace))
                Write(Level.Error, ex.StackTrace!, target);
        }

        /// <summary>
        /// Persist a small, truncated portion of an incoming JSON-RPC request. For tools/call
        /// the tool name and a trimmed view of its arguments are captured; other methods log a
        /// trimmed view of their params. Payloads are truncated to keep the log compact and to
        /// limit incidental exposure of SQL text or PII in arguments.
        /// </summary>
        public static void LogRequest(JsonRpcRequest? request)
        {
            if (request == null)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.Append("REQ id=").Append(request.Id?.ToString() ?? "(notification)")
                  .Append(" method=").Append(string.IsNullOrEmpty(request.Method) ? "(null)" : request.Method);

                if (string.Equals(request.Method, "tools/call", StringComparison.OrdinalIgnoreCase) &&
                    request.Params != null)
                {
                    if (request.Params.TryGetValue("name", out var name) && name != null)
                        sb.Append(" tool=").Append(name);
                    if (request.Params.TryGetValue("arguments", out var args) && args != null)
                        sb.Append(" args=").Append(SanitizeForRequestLog(args));
                }
                else if (string.Equals(request.Method, "initialize", StringComparison.OrdinalIgnoreCase) &&
                         request.Params != null)
                {
                    // The initialize params carry a large client capabilities/metadata blob
                    // that adds no diagnostic value — log only the negotiated protocol version.
                    if (request.Params.TryGetValue("protocolVersion", out var pv) && pv != null)
                        sb.Append(" protocol=").Append(pv);
                }
                else if (request.Params != null && request.Params.Count > 0)
                {
                    sb.Append(" params=").Append(SanitizeForRequestLog(request.Params));
                }

                Info(sb.ToString());
            }
            catch
            {
                // Logging must never interfere with request processing.
            }
        }

        /// <summary>
        /// Log a compact, low-risk summary of a tool response for troubleshooting:
        /// tool name, elapsed time, and (when present) summary/row_count from the JSON payload.
        /// </summary>
        public static void LogToolResult(string toolName, string resultText, long elapsedMs)
        {
            Info(BuildToolResultLogLine(toolName, resultText, elapsedMs));
        }

        private static void Write(Level level, string message, LogTarget target)
        {
            EnsureInitialized();

            // Timestamp is always the first column.
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [{level.ToString().ToUpperInvariant(),-5}]  {message}";

            // Persist to the log file (best-effort; never throw).
            if ((target & LogTarget.File) == LogTarget.File)
            {
                lock (s_sync)
                {
                    try
                    {
                        RollOverIfNeeded();
                        File.AppendAllText(s_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                    }
                    catch
                    {
                        // Swallow — logging must never break the server or the protocol stream.
                    }
                }
            }

            // Write to the console when requested. This goes to stderr, never stdout:
            // stdout is reserved for the JSON-RPC protocol and any stray text there
            // would corrupt every response.
            if ((target & LogTarget.Console) == LogTarget.Console)
            {
                try
                {
                    Console.Error.WriteLine(line);
                }
                catch
                {
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (s_initialized)
                return;

            lock (s_sync)
            {
                if (s_initialized)
                    return;

                try
                {
                    // Same location as the main SQL Nexus log (%TEMP%), separate file.
                    s_logFilePath = Path.Combine(Path.GetTempPath(), LogFileName);
                }
                catch
                {
                    // Fall back to the current directory if %TEMP% can't be resolved.
                    s_logFilePath = LogFileName;
                }

                s_initialized = true;
            }
        }

        // Caller must hold s_sync.
        private static void RollOverIfNeeded()
        {
            try
            {
                var fi = new FileInfo(s_logFilePath);
                if (!fi.Exists || fi.Length <= MaxLogBytes)
                    return;

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backup = s_logFilePath + "." + timestamp;
                if (File.Exists(backup))
                    backup = s_logFilePath + "." + timestamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                File.Move(s_logFilePath, backup);

                string backupPattern = Path.GetFileName(s_logFilePath) + ".*";
                string logDir = Path.GetDirectoryName(s_logFilePath) ?? Directory.GetCurrentDirectory();
                var backups = new DirectoryInfo(logDir)
                    .GetFiles(backupPattern)
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                for (int i = MaxBackupFiles; i < backups.Count; i++)
                {
                    try { backups[i].Delete(); } catch { }
                }
            }
            catch
            {
                // If rollover fails, keep appending to the existing file.
            }
        }

        internal static string SanitizeForRequestLog(object value)
        {
            string serialized = SafeSerialize(value);
            string scrubbed = PiiScrubber.Scrub(serialized);
            return Truncate(scrubbed);
        }

        internal static string BuildToolResultLogLine(string toolName, string resultText, long elapsedMs)
        {
            string safeToolName = string.IsNullOrWhiteSpace(toolName) ? "(unknown)" : toolName;
            string summary = "(unavailable)";
            string rowCount = "(unknown)";

            try
            {
                var obj = JObject.Parse(resultText);
                summary = obj.Value<string>("summary") ?? summary;

                JToken rowToken;
                if (obj.TryGetValue("row_count", out rowToken) && rowToken != null && rowToken.Type != JTokenType.Null)
                    rowCount = rowToken.ToString();
            }
            catch
            {
                // Not JSON, keep defaults.
            }

            summary = Truncate(PiiScrubber.Scrub(summary));
            return $"RES tool={safeToolName} elapsed_ms={elapsedMs} row_count={rowCount} summary={summary}";
        }

        private static string SafeSerialize(object value)
        {
            try
            {
                return JsonConvert.SerializeObject(value);
            }
            catch
            {
                return value.ToString() ?? string.Empty;
            }
        }

        /// <summary>
        /// Build a concise one-line description of an exception: type name, the SQL error
        /// number for a <see cref="SqlException"/>, the message, and a trimmed inner-exception
        /// summary when present. No stack trace is included.
        /// </summary>
        private static string DescribeException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append(ex.GetType().Name);

            if (ex is SqlException sqlEx)
                sb.Append(" (Msg ").Append(sqlEx.Number).Append(')');

            sb.Append(": ").Append(ex.Message);

            if (ex.InnerException != null)
                sb.Append(" | Inner: ").Append(ex.InnerException.GetType().Name)
                  .Append(": ").Append(ex.InnerException.Message);

            return sb.ToString();
        }

        /// <summary>
        /// True when the exception is a routine, self-explanatory error whose call stack
        /// adds no diagnostic value — currently SQL errors raised while probing optional
        /// SQL Nexus tables (see <see cref="s_expectedSqlErrors"/>).
        /// </summary>
        private static bool IsExpected(Exception ex)
        {
            return ex is SqlException sqlEx && s_expectedSqlErrors.Contains(sqlEx.Number);
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxPayloadChars)
                return value;
            return value.Substring(0, MaxPayloadChars) + "…(truncated)";
        }
    }
}
