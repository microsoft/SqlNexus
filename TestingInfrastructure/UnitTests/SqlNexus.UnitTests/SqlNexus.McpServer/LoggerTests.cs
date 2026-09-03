using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlNexus.McpServer;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    /// <summary>
    /// Tests for <see cref="Logger"/>, the self-contained file logger for the MCP server.
    ///
    /// A core design constraint is that logging must NEVER throw (a logging failure must not crash
    /// the server or corrupt the stdio JSON-RPC stream). These tests assert that contract and the
    /// stability of the resolved log-file path. They avoid asserting on file contents to stay
    /// deterministic and free of file-system race conditions in CI.
    /// </summary>
    [TestClass]
    public class LoggerTests
    {
        [TestMethod]
        public void LogFilePath_UsesDedicatedMcpServerLogName()
        {
            // The MCP server intentionally logs to its own file, separate from sqlnexus.log.
            StringAssert.Contains(Logger.LogFilePath, "sqlnexus_mcpserver.log");
        }

        [TestMethod]
        public void Info_DoesNotThrow()
        {
            Logger.Info("unit-test info message");
        }

        [TestMethod]
        public void Warn_DoesNotThrow()
        {
            Logger.Warn("unit-test warn message");
        }

        [TestMethod]
        public void Error_WithMessageOnly_DoesNotThrow()
        {
            Logger.Error("unit-test error message");
        }

        [TestMethod]
        public void Error_WithException_DoesNotThrow()
        {
            Logger.Error("unit-test error with exception", new InvalidOperationException("boom"));
        }

        [TestMethod]
        public void Info_FileTargetOnly_DoesNotThrow()
        {
            Logger.Info("file-only message", Logger.LogTarget.File);
        }

        [TestMethod]
        public void LogTarget_Both_IncludesFileAndConsoleFlags()
        {
            Assert.IsTrue((Logger.LogTarget.Both & Logger.LogTarget.File) == Logger.LogTarget.File);
            Assert.IsTrue((Logger.LogTarget.Both & Logger.LogTarget.Console) == Logger.LogTarget.Console);
        }

        [TestMethod]
        public void LogFilePath_IsStableAcrossCalls()
        {
            string first = Logger.LogFilePath;
            Logger.Info("trigger initialization");
            string second = Logger.LogFilePath;
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void SanitizeForRequestLog_ScrubsEmailAddresses()
        {
            var payload = new Dictionary<string, object>
            {
                ["query"] = "SELECT 'user@example.com'"
            };

            string text = Logger.SanitizeForRequestLog(payload);

            Assert.IsFalse(text.Contains("user@example.com"));
            StringAssert.Contains(text, "<EMAIL>");
        }

        [TestMethod]
        public void BuildToolResultLogLine_JsonPayload_ExtractsSummaryAndRowCount()
        {
            string line = Logger.BuildToolResultLogLine(
                "analyze_wait_stats",
                "{\"summary\":\"Wait Stats\",\"row_count\":5,\"data\":[]}",
                123);

            StringAssert.Contains(line, "tool=analyze_wait_stats");
            StringAssert.Contains(line, "elapsed_ms=123");
            StringAssert.Contains(line, "row_count=5");
            StringAssert.Contains(line, "summary=Wait Stats");
        }
    }
}
