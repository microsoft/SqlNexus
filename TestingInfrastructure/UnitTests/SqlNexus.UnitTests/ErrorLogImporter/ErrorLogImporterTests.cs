using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.ErrorLogImporter
{
    [TestClass]
    public class ErrorLogImporterTests
    {
        [TestMethod]
        public void IsHeadAndTailMarker_LogScoutMarkerWithLeadingWhitespace_ReturnsTrue()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "   <<... middle part of file not captured because the file is too large (>1 GB) ...>>");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_RegularErrorLogLine_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "2026-04-14 22:37:11.55 Server      SQL Server is starting.");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_NullLine_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_EmptyLine_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker("");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_MarkerWithDifferentSize_ReturnsTrue()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "<<... middle part of file not captured because the file is too large (>2 GB) ...>>");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ProcessLogEntries_HeadAndTailFile_SkipsMarkerAndInsertsIncompleteNotice()
        {
            var rows = new List<ImportedRow>();
            const string marker = "<<... middle part of file not captured because the file is too large (>1 GB) ...>>";

            using (var reader = new StringReader(
                "2026-04-14 22:37:11.55 Server      First message." + Environment.NewLine +
                marker + Environment.NewLine +
                "2026-04-14 22:37:12.55 Server      Last message."))
            {
                global::ErrorLogImporter.ErrorLogImporter.ProcessLogEntries(
                    reader,
                    () => false,
                    line => { },
                    (logDateTime, process, message) => rows.Add(new ImportedRow(logDateTime, process, message)),
                    () => rows.Add(new ImportedRow(null, null, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE)));
            }

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("First message.", rows[0].Message);
            Assert.IsFalse(rows[0].Message.Contains(marker));
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE, rows[1].Message);
            Assert.IsNull(rows[1].LogDateTime);
            Assert.AreEqual("Last message.", rows[2].Message);
        }

        [TestMethod]
        public void ProcessLogEntries_MultipleMarkers_InsertsSingleIncompleteNotice()
        {
            var rows = new List<ImportedRow>();
            const string marker = "<<... middle part of file not captured because the file is too large (>1 GB) ...>>";

            using (var reader = new StringReader(
                marker + Environment.NewLine +
                marker + Environment.NewLine +
                "2026-04-14 22:37:12.55 Server      Last message."))
            {
                global::ErrorLogImporter.ErrorLogImporter.ProcessLogEntries(
                    reader,
                    () => false,
                    line => { },
                    (logDateTime, process, message) => rows.Add(new ImportedRow(logDateTime, process, message)),
                    () => rows.Add(new ImportedRow(null, null, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE)));
            }

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE, rows[0].Message);
            Assert.AreEqual("Last message.", rows[1].Message);
        }

        [TestMethod]
        public void ProcessLogEntries_MarkerWithLeadingWhitespace_InsertsIncompleteNoticeAsSeparateRow()
        {
            var rows = new List<ImportedRow>();
            const string marker = "   <<... middle part of file not captured because the file is too large (>1 GB) ...>>";

            using (var reader = new StringReader(
                "2026-08-18 01:38:53.73 spid5s      SQL Trace was stopped due to server shutdown. Trace ID = '10101010101010'. This is an informational message only; no user action is required." + Environment.NewLine +
                marker + Environment.NewLine +
                "2026-08-13 05:57:14.81 Server      All rights reserved."))
            {
                global::ErrorLogImporter.ErrorLogImporter.ProcessLogEntries(
                    reader,
                    () => false,
                    line => { },
                    (logDateTime, process, message) => rows.Add(new ImportedRow(logDateTime, process, message)),
                    () => rows.Add(new ImportedRow(null, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_PROCESS_MARKER, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE)));
            }

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("SQL Trace was stopped due to server shutdown. Trace ID = '10101010101010'. This is an informational message only; no user action is required.", rows[0].Message);
            Assert.IsFalse(rows[0].Message.Contains("<<..."), "First row should not contain the marker");
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE, rows[1].Message);
            Assert.IsNull(rows[1].LogDateTime, "Incomplete log notice should have null LogDateTime");
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_PROCESS_MARKER, rows[1].Process, "Incomplete log notice should use the INCOMPLETE process marker");
            Assert.AreEqual("All rights reserved.", rows[2].Message);
        }

        [TestMethod]
        public void ProcessLogEntries_HeadAndTailFileWithBlankLinesAndBom_ProducesSeparateRows()
        {
            // Mirrors the real SQLLogScout output: blank lines around the marker and a mangled
            // UTF-8 BOM ('?') prefixing the first log line of the tail section.
            var rows = new List<ImportedRow>();
            const string marker = "   <<... middle part of file not captured because the file is too large (>1 GB) ...>>";

            using (var reader = new StringReader(
                "2026-08-18 01:38:53.73 spid5s      SQL Server shutdown has been initiated" + Environment.NewLine +
                "2026-08-18 01:38:53.73 spid5s      SQL Trace was stopped due to server shutdown. Trace ID = '10101010101010'. This is an informational message only; no user action is required." + Environment.NewLine +
                "" + Environment.NewLine +
                marker + Environment.NewLine +
                "" + Environment.NewLine +
                "?2026-08-13 05:57:14.80 Server      Microsoft SQL Server 2017 (RTM-CU31-GDR)" + Environment.NewLine +
                "2026-08-13 05:57:14.81 Server      All rights reserved."))
            {
                global::ErrorLogImporter.ErrorLogImporter.ProcessLogEntries(
                    reader,
                    () => false,
                    line => { },
                    (logDateTime, process, message) => rows.Add(new ImportedRow(logDateTime, process, message)),
                    () => rows.Add(new ImportedRow(null, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_PROCESS_MARKER, global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE)));
            }

            Assert.AreEqual(5, rows.Count, $"Expected 5 rows but got {rows.Count}");
            Assert.AreEqual("SQL Server shutdown has been initiated", rows[0].Message);
            Assert.AreEqual("SQL Trace was stopped due to server shutdown. Trace ID = '10101010101010'. This is an informational message only; no user action is required.", rows[1].Message.Trim());
            Assert.IsFalse(rows[1].Message.Contains("<<..."), "Row before marker should not contain the marker text");
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_LOG_MESSAGE, rows[2].Message);
            Assert.IsNull(rows[2].LogDateTime);
            Assert.AreEqual(global::ErrorLogImporter.ErrorLogImporter.INCOMPLETE_PROCESS_MARKER, rows[2].Process);
            // The BOM-mangled '?' prefix is stripped so the first tail line is captured, not lost.
            Assert.AreEqual("Microsoft SQL Server 2017 (RTM-CU31-GDR)", rows[3].Message);
            Assert.AreEqual("Server", rows[3].Process);
            Assert.AreEqual("All rights reserved.", rows[4].Message);
        }

        [TestMethod]
        public void StripLeadingByteOrderMark_MangledBomBeforeDate_StripsQuestionMark()
        {
            string result = global::ErrorLogImporter.ErrorLogImporter.StripLeadingByteOrderMark(
                "?2026-08-13 05:57:14.80 Server      Microsoft SQL Server 2017");

            Assert.AreEqual("2026-08-13 05:57:14.80 Server      Microsoft SQL Server 2017", result);
        }

        [TestMethod]
        public void StripLeadingByteOrderMark_UnicodeBomBeforeDate_StripsBom()
        {
            string result = global::ErrorLogImporter.ErrorLogImporter.StripLeadingByteOrderMark(
                "\uFEFF2026-08-13 05:57:14.80 Server      message");

            Assert.AreEqual("2026-08-13 05:57:14.80 Server      message", result);
        }

        [TestMethod]
        public void StripLeadingByteOrderMark_QuestionMarkNotFollowedByDigit_LeavesLineUnchanged()
        {
            const string line = "?This is genuine message text.";

            string result = global::ErrorLogImporter.ErrorLogImporter.StripLeadingByteOrderMark(line);

            Assert.AreEqual(line, result);
        }

        [TestMethod]
        public void StripLeadingByteOrderMark_NullOrEmpty_ReturnsInput()
        {
            Assert.IsNull(global::ErrorLogImporter.ErrorLogImporter.StripLeadingByteOrderMark(null));
            Assert.AreEqual(string.Empty, global::ErrorLogImporter.ErrorLogImporter.StripLeadingByteOrderMark(string.Empty));
        }

        [TestMethod]
        public void AdvancePosition_Utf8Line_AddsLineAndNewlineBytes()
        {
            long result = global::ErrorLogImporter.ErrorLogImporter.AdvancePosition(
                0, 1000, "hello", System.Text.Encoding.UTF8);

            long expected = System.Text.Encoding.UTF8.GetByteCount("hello")
                + System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AdvancePosition_ExceedsFileSize_ClampsToFileSize()
        {
            long result = global::ErrorLogImporter.ErrorLogImporter.AdvancePosition(
                990, 1000, new string('x', 500), System.Text.Encoding.UTF8);

            Assert.AreEqual(1000, result, "Position must never overshoot the file size");
        }

        [TestMethod]
        public void AdvancePosition_NullEncoding_DefaultsToUtf8()
        {
            long result = global::ErrorLogImporter.ErrorLogImporter.AdvancePosition(
                0, 1000, "abc", null);

            long expected = System.Text.Encoding.UTF8.GetByteCount("abc")
                + System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AdvancePosition_NullLine_AddsOnlyNewlineBytes()
        {
            long result = global::ErrorLogImporter.ErrorLogImporter.AdvancePosition(
                10, 1000, null, System.Text.Encoding.UTF8);

            Assert.AreEqual(10 + System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine), result);
        }

        [TestMethod]
        public void AdvancePosition_UnknownFileSize_DoesNotClamp()
        {
            long result = global::ErrorLogImporter.ErrorLogImporter.AdvancePosition(
                100, 0, "data", System.Text.Encoding.UTF8);

            long expected = 100 + System.Text.Encoding.UTF8.GetByteCount("data")
                + System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine);
            Assert.AreEqual(expected, result);
        }

        private sealed class ImportedRow
        {
            public ImportedRow(DateTime? logDateTime, string process, string message)
            {
                LogDateTime = logDateTime;
                Process = process;
                Message = message;
            }

            public DateTime? LogDateTime { get; private set; }
            public string Process { get; private set; }
            public string Message { get; private set; }
        }
    }
}
