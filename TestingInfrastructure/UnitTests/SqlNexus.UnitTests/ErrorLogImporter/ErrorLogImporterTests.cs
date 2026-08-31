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
        public void IsHeadAndTailMarker_LogScoutMarkerWithWhitespace_ReturnsTrue()
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
        public void IsHeadAndTailMarker_AlteredMarker_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "<<... middle part of file not captured because the file is too large (>2 GB) ...>>");

            Assert.IsFalse(result);
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
