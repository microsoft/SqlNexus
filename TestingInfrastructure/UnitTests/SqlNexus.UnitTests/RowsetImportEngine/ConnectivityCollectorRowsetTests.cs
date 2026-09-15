using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RowsetImportEngine;

namespace SqlNexus.UnitTests.RowsetImportEngine
{
    /// <summary>
    /// Regression tests for importing the SQL LogScout connectivity/security collector rowsets
    /// (TLS protocols, client drivers, SPNs, service accounts, aliases, certificates) that were
    /// added to TextRowsets.xml.
    ///
    /// These collectors emit fixed-width, sqlcmd-style output. <see cref="TextRowset"/> derives each
    /// column's parse width from the dashed separator line (a run of dashes per column, separated by a
    /// single space), so a too-narrow trailing dash run silently truncates the LAST column on import.
    /// The collectors were widened so the last column (e.g. SQL_Client_Drivers 'Path',
    /// SQL_Server_Certificates 'KeyContainerFile') is captured in full. These tests exercise the real
    /// engine (DefineRowsetColumns + ParseRow) with that exact format to lock the contract in.
    /// </summary>
    [TestClass]
    public class ConnectivityCollectorRowsetTests
    {
        // Build a fixed-width header line + dashed separator line the way the collectors do:
        //   - each non-last column: name.PadRight(width)  /  (width-1) dashes then one space
        //   - last column:          name (unpadded)       /  lastDashWidth dashes
        private static void BuildHeaderAndDashes(string[] names, int[] widths, string lastName,
            int lastDashWidth, out string header, out string dashes)
        {
            var h = new StringBuilder();
            var d = new StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                h.Append(names[i].PadRight(widths[i]));
                d.Append(new string('-', widths[i] - 1).PadRight(widths[i]));
            }
            h.Append(lastName);
            d.Append(new string('-', lastDashWidth));
            header = h.ToString();
            dashes = d.ToString();
        }

        // Build a data row: non-last fields padded to their width, last field appended raw.
        private static string BuildRow(string[] values, int[] widths, string lastValue)
        {
            var r = new StringBuilder();
            for (int i = 0; i < values.Length; i++) r.Append(values[i].PadRight(widths[i]));
            r.Append(lastValue);
            return r.ToString();
        }

        private static string[] ColumnNames(TextRowset rs)
        {
            var names = new List<string>();
            foreach (Column c in rs.Columns) names.Add(c.Name);
            return names.ToArray();
        }

        private static string DataOf(TextRowset rs, string columnName)
        {
            foreach (Column c in rs.Columns)
            {
                if (c.Name == columnName) return c.Data == null ? null : c.Data.ToString();
            }
            throw new AssertFailedException("Column not found in parsed rowset: " + columnName);
        }

        private static TextRowset NewRowsetWithKnownColumns(string[] names, int sqlLen)
        {
            var rs = new TextRowset();
            foreach (var n in names) rs.KnownColumns.Add(new VarCharColumn { Name = n, SqlColumnLength = sqlLen });
            return rs;
        }

        [TestMethod]
        public void SqlClientDrivers_Header_MapsToExpectedColumnsInOrder()
        {
            // Arrange: the SQL_Client_Drivers rowset header/dashes (matches GetSQLClientDrivers).
            var names = new[] { "Name", "Type", "Version", "TLS12", "TLS13", "MSF", "Bitness", "CLSID", "Path" };
            var rs = NewRowsetWithKnownColumns(names, 260);
            var midNames = new[] { "Name", "Type", "Version", "TLS12", "TLS13", "MSF", "Bitness", "CLSID" };
            var midWidths = new[] { 32, 8, 18, 9, 9, 6, 9, 40 };
            BuildHeaderAndDashes(midNames, midWidths, "Path", 260, out string header, out string dashes);

            // Act
            rs.DefineRowsetColumns(dashes, header);

            // Assert: all nine columns resolved, in order, with the exact TextRowsets.xml names.
            CollectionAssert.AreEqual(names, ColumnNames(rs));
        }

        [TestMethod]
        public void SqlClientDrivers_WidenedLastColumn_CapturesFullPath()
        {
            // Arrange
            var names = new[] { "Name", "Type", "Version", "TLS12", "TLS13", "MSF", "Bitness", "CLSID", "Path" };
            var rs = NewRowsetWithKnownColumns(names, 260);
            var midNames = new[] { "Name", "Type", "Version", "TLS12", "TLS13", "MSF", "Bitness", "CLSID" };
            var midWidths = new[] { 32, 8, 18, 9, 9, 6, 9, 40 };
            BuildHeaderAndDashes(midNames, midWidths, "Path", 260, out string header, out string dashes);
            rs.DefineRowsetColumns(dashes, header);

            const string longPath = @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\msodbcsql17.dll";
            string row = BuildRow(
                new[] { "ODBC Driver 17 for SQL Server", "ODBC", "2017.1710.06.01", "Yes", "No", "Yes", "64-bit", "" },
                midWidths, longPath);

            // Act
            rs.ParseRow(row);

            // Assert: middle columns land correctly...
            Assert.AreEqual("ODBC Driver 17 for SQL Server", DataOf(rs, "Name"));
            Assert.AreEqual("Yes", DataOf(rs, "TLS12"));
            Assert.AreEqual("64-bit", DataOf(rs, "Bitness"));
            // ...and the widened trailing dash run captures the entire path. With the previous 4-dash
            // 'Path' separator the engine returned only "C:\P"; this guards against that regression.
            Assert.AreEqual(longPath, DataOf(rs, "Path"));
        }

        [TestMethod]
        public void SqlSpnServiceAccounts_RenamedHeader_MapsServiceAccountNameColumn()
        {
            // Arrange: the SQL_SPN_ServiceAccounts rowset, whose second column header was renamed
            // from "StartName" to "ServiceAccountName".
            var names = new[] { "ServiceName", "ServiceAccountName", "SpnAccount", "State" };
            var rs = NewRowsetWithKnownColumns(names, 64);
            var midNames = new[] { "ServiceName", "ServiceAccountName", "SpnAccount" };
            var midWidths = new[] { 24, 34, 38 };
            BuildHeaderAndDashes(midNames, midWidths, "State", 7, out string header, out string dashes);
            rs.DefineRowsetColumns(dashes, header);

            string row = BuildRow(new[] { "MSSQL$SQL2022", "CSSLABS\\SqlSvc", "CSSLABS\\SqlSvc" }, midWidths, "Running");

            // Act
            rs.ParseRow(row);

            // Assert: the renamed column is present and populated, and the last column parses.
            CollectionAssert.AreEqual(names, ColumnNames(rs));
            Assert.AreEqual("CSSLABS\\SqlSvc", DataOf(rs, "ServiceAccountName"));
            Assert.AreEqual("Running", DataOf(rs, "State"));
        }
    }
}
