using System;
using System.Collections.Generic;
using LinuxPerfImporter.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.LinuxPerfImporter
{
    [TestClass]
    public class LinuxOutFilePidStatTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ConfigValues.MachineName = "testnode";
            ConfigValues.TimeZone = 0;
            ConfigValues.PidStatFilter = new[] { "sqlservr", "sqlcmd" };
        }

        [TestMethod]
        public void ParsePidStat_WithLeadingDateLineAnd16Metrics_GeneratesMatchingHeaderAndMetrics()
        {
            // Arrange: Ubuntu 16.04 (sysstat 11.2.0) format with leading 'date' line at index 0
            var lines = new List<string>
            {
                "Tue Nov  3 23:18:11 PST 2026",
                "Linux 4.4.0-210-generic (testnode) \t11/03/2026 \t_x86_64_\t(4 CPU)",
                "",
                "#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ     RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s iodelay   cswch/s nvcswch/s  Command",
                " 1793776693     0      2852    6.97    1.99    0.00    2.24     1      1.00      0.00 15933688  882644   5.50      0.00      0.00      0.00       0      0.00      0.00  sqlservr",
                "",
                "#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ     RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s iodelay   cswch/s nvcswch/s  Command",
                " 1793776695     0      2852    5.50    1.50    0.00    1.75     1      0.00      0.00 15933688  882644   5.50      0.00      0.00      0.00       0      0.00      0.00  sqlservr"
            };

            // Act
            var pidStat = new LinuxOutFilePidStat(lines);

            // Assert
            string[] headerCols = pidStat.Header.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 16, headerCols.Length);
            StringAssert.Contains(pidStat.Header, @"\\testnode\Process(sqlservr#2852)\%usr");
            StringAssert.Contains(pidStat.Header, @"\\testnode\Process(sqlservr#2852)\nvcswch/s");
            Assert.AreEqual(1, pidStat.Metrics.Count);
            string[] rowCols = pidStat.Metrics[0].Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 16, rowCols.Length);
        }

        [TestMethod]
        public void ParsePidStat_Rhel79With15Metrics_DoesNotIncludeCommandNameInMetrics()
        {
            // Arrange: RHEL 7.9 (sysstat 10.1.5) format with 15 metrics (no iodelay column)
            var lines = new List<string>
            {
                "Wed Nov  4 02:18:22 EST 2026",
                "Linux 3.10.0-1160.59.1.el7.x86_64 (testnode) \t11/04/2026 \t_x86_64_\t(1 CPU)",
                "",
                "#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ    RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s   cswch/s nvcswch/s  Command",
                " 1793776704   994      2216    2.53    1.01    0.00    3.54     0      7.58      0.00 9177264 1965224  19.98      0.00      0.00      0.00      0.00      0.00  sqlservr",
                "",
                "#      Time   UID       PID    %usr %system  %guest    %CPU   CPU  minflt/s  majflt/s     VSZ    RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s   cswch/s nvcswch/s  Command",
                " 1793776706   994      2216    4.06    0.00    0.00    4.06     0      4.06      0.00 9177264 1965224  19.98      0.00      4.06      0.00      0.00      0.00  sqlservr"
            };

            // Act
            var pidStat = new LinuxOutFilePidStat(lines);

            // Assert
            string[] headerCols = pidStat.Header.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 15, headerCols.Length);
            Assert.AreEqual(1, pidStat.Metrics.Count);
            Assert.IsFalse(pidStat.Metrics[0].Contains("sqlservr"));
            string[] rowCols = pidStat.Metrics[0].Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 15, rowCols.Length);
        }

        [TestMethod]
        public void ParsePidStat_Rhel8With17MetricsAnd12HourTime_FormatsFullDateTimeAnd17Columns()
        {
            // Arrange: RHEL 8.10 (sysstat 11.7.3) format with %wait, iodelay (17 metrics) and 12-hour AM/PM time
            var lines = new List<string>
            {
                "Wed Nov  4 02:18:17 EST 2026",
                "Linux 4.18.0-553.33.1.el8_10.x86_64 (testnode) \t11/04/2026 \t_x86_64_\t(6 CPU)",
                "",
                "# Time        UID       PID    %usr %system  %guest   %wait    %CPU   CPU  minflt/s  majflt/s     VSZ     RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s iodelay   cswch/s nvcswch/s  Command",
                "02:18:19 AM   990      4160    2.48    0.00    0.00    0.00    0.41     3      6.93      0.00 11394836 1278612   8.26      0.00      0.00      0.00       0      0.00      0.00  sqlservr",
                "",
                "# Time        UID       PID    %usr %system  %guest   %wait    %CPU   CPU  minflt/s  majflt/s     VSZ     RSS   %MEM   kB_rd/s   kB_wr/s kB_ccwr/s iodelay   cswch/s nvcswch/s  Command",
                "02:18:21 AM   990      4160    2.50    0.50    0.00    0.00    0.50     3      0.00      0.00 11394836 1278612   8.26      0.00      0.00      0.00       0      0.00      0.00  sqlservr"
            };

            // Act
            var pidStat = new LinuxOutFilePidStat(lines);

            // Assert
            string[] headerCols = pidStat.Header.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 17, headerCols.Length);
            Assert.AreEqual(1, pidStat.Metrics.Count);
            StringAssert.StartsWith(pidStat.Metrics[0], "\"11/04/2026 02:18:19\"");
            string[] rowCols = pidStat.Metrics[0].Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(1 + 17, rowCols.Length);
        }

        [TestMethod]
        public void ParsePidStat_MissingHeaderLine_ThrowsApplicationException()
        {
            // Arrange
            var lines = new List<string>
            {
                "Linux 4.18.0 (testnode) \t11/04/2026 \t_x86_64_\t(6 CPU)",
                ""
            };

            // Act and Assert
            Assert.ThrowsException<ApplicationException>(() => new LinuxOutFilePidStat(lines));
        }
    }
}
