using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.XEvent.XELite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class CustomXELImporterCompatibilityTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [TestCategory("CustomXelLocalCompatibility")]
        [DataRow("SQLDiag", "*_SQLDIAG*.xel")]
        [DataRow("AlwaysOnHealth", "*AlwaysOn_health*.xel")]
        [DataRow("SystemHealth", "*system_health*.xel")]
        public async Task Capture_ReadsLocalFilesAndReportsAvailableMetadata(string category, string mask)
        {
            if (Environment.GetEnvironmentVariable("SQLNEXUS_RUN_CUSTOM_XEL_COMPATIBILITY") != "1")
                Assert.Inconclusive("Set SQLNEXUS_RUN_CUSTOM_XEL_COMPATIBILITY=1 and SQLNEXUS_CUSTOM_XEL_PATH to run this local-only probe.");

            string[] files = Directory.GetFiles(RequiredSetting("SQLNEXUS_CUSTOM_XEL_PATH"), mask, SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
                Assert.Inconclusive("No " + category + " fixtures were supplied; this category has NOT been validated.");

            long count = 0;
            long fieldCount = 0;
            long actionCount = 0;
            var valueTypes = new HashSet<string>(StringComparer.Ordinal);
            var eventNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in files)
            {
                long length = new FileInfo(file).Length;
                var streamer = new XEFileEventStreamer(file);
                await streamer.ReadEventStream(
                    xe =>
                    {
                        Assert.IsFalse(String.IsNullOrWhiteSpace(xe.Name), category + ": missing event name.");
                        Assert.AreNotEqual(Guid.Empty, xe.UUID, category + ": missing event UUID.");
                        Assert.IsTrue(xe.XEventStartOffsetInBytes >= 0 && xe.XEventEndOffsetInBytes > xe.XEventStartOffsetInBytes
                            && xe.XEventEndOffsetInBytes <= length, category + ": event offsets are outside the file.");
                        Assert.AreEqual(xe.XEventEndOffsetInBytes - xe.XEventStartOffsetInBytes, xe.XEventSizeInBytes);
                        Assert.AreNotEqual(DateTimeOffset.MinValue, xe.Timestamp, category + ": missing timestamp.");
                        Assert.IsFalse(xe.ToString().TrimStart().StartsWith("<", StringComparison.Ordinal),
                            "Reassess the raw-XML capability: XeLite ToString format changed.");
                        count++;
                        fieldCount += xe.Fields.Count;
                        actionCount += xe.Actions.Count;
                        eventNames.Add(xe.Name);
                        foreach (object value in xe.Fields.Values.Concat(xe.Actions.Values))
                            valueTypes.Add(value == null ? "null" : value.GetType().FullName);
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);
            }

            Assert.IsTrue(count > 0, category + ": fixtures contained no events.");
            TestContext.WriteLine("{0}: files={1}; events={2}; fields={3}; actions={4}; valid timestamps/UUIDs/byte ranges.",
                category, files.Length, count, fieldCount, actionCount);
            TestContext.WriteLine("Event kinds: " + String.Join(", ", eventNames.OrderBy(value => value)));
            TestContext.WriteLine("Value types: " + String.Join(", ", valueTypes.OrderBy(value => value)));
            TestContext.WriteLine("Local readability only; SQL/XML/offset parity has NOT been certified.");
        }

        [DataTestMethod]
        [TestCategory("CustomXelCompatibility")]
        [DataRow("SQLDiag", "*_SQLDIAG*.xel")]
        [DataRow("AlwaysOnHealth", "*AlwaysOn_health*.xel")]
        [DataRow("SystemHealth", "*system_health*.xel")]
        public async Task Capture_ComparesSqlAndXeLiteWithoutAssumingFullParity(string category, string mask)
        {
            if (Environment.GetEnvironmentVariable("SQLNEXUS_RUN_CUSTOM_XEL_COMPATIBILITY") != "1")
                Assert.Inconclusive("Set SQLNEXUS_RUN_CUSTOM_XEL_COMPATIBILITY=1 and the documented fixture/SQL settings to run this read-only probe.");

            string path = Environment.GetEnvironmentVariable("SQLNEXUS_CUSTOM_XEL_PATH");
            Assert.IsFalse(String.IsNullOrWhiteSpace(path), "SQLNEXUS_CUSTOM_XEL_PATH is required.");
            string[] files = Directory.GetFiles(path, mask, SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
                Assert.Inconclusive("No " + category + " fixtures were supplied; this category has NOT been validated.");

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = RequiredSetting("SQLNEXUS_CUSTOM_XEL_SERVER"),
                InitialCatalog = RequiredSetting("SQLNEXUS_CUSTOM_XEL_DATABASE"),
                IntegratedSecurity = true,
                Encrypt = true,
                TrustServerCertificate = Environment.GetEnvironmentVariable("SQLNEXUS_CUSTOM_XEL_TRUST_SERVER_CERTIFICATE") == "1",
                ApplicationName = "SqlNexus Custom XEL read-only compatibility probe"
            };
            long eventCount = 0;
            long offsetMatches = 0;
            long comparedValues = 0;
            long mismatchedValues = 0;
            long displayTextValues = 0;
            long timestampMatches = 0;
            long millisecondTimestampMatches = 0;
            long xmlTimestampMatches = 0;
            long maxTimestampDifference = 0;
            long uuidMatchesSqlModule = 0;
            long uuidMatchesSqlPackage = 0;
            var timestampDifferences = new HashSet<long>();
            var mismatchedFields = new Dictionary<string, long>(StringComparer.Ordinal);
            var types = new HashSet<string>(StringComparer.Ordinal);
            var sqlColumns = new HashSet<string>(StringComparer.Ordinal);
            var missingFields = new HashSet<string>(StringComparer.Ordinal);
            var unexpectedFields = new HashSet<string>(StringComparer.Ordinal);

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();
                foreach (string file in files)
                {
                    var localEvents = new List<LocalEvent>();
                    var streamer = new XEFileEventStreamer(file);
                    await streamer.ReadEventStream(
                        xe =>
                        {
                            localEvents.Add(new LocalEvent(xe));
                            return Task.CompletedTask;
                        },
                        CancellationToken.None);

                    var sqlEvents = new List<SqlEvent>();
                    using (var command = new SqlCommand(
                        "SELECT module_guid, package_guid, object_name, event_data, file_name, file_offset, timestamp_utc " +
                        "FROM sys.fn_xe_file_target_read_file(@file, NULL, NULL, NULL);", connection))
                    {
                        command.CommandTimeout = 120;
                        command.Parameters.Add("@file", SqlDbType.NVarChar, 4000).Value = file;
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                                sqlColumns.Add(reader.GetName(i) + ":" + reader.GetDataTypeName(i));
                            while (await reader.ReadAsync())
                            {
                                Assert.AreEqual(Path.GetFileName(file), Path.GetFileName(reader.GetString(4)),
                                    category + ": SQL reader returned a different source file.");
                                sqlEvents.Add(new SqlEvent(
                                    reader.GetString(2), reader.GetDateTime(6), reader.GetInt64(5),
                                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(3)));
                            }
                        }
                    }

                    Assert.AreEqual(sqlEvents.Count, localEvents.Count, category + ": event counts differ.");
                    LocalEvent[] localOrdered = localEvents.OrderBy(row => row.Name, StringComparer.Ordinal)
                        .ThenBy(row => row.TimestampTicks).ThenBy(row => row.Start).ToArray();
                    SqlEvent[] sqlOrdered = sqlEvents.OrderBy(row => row.Name, StringComparer.Ordinal)
                        .ThenBy(row => row.TimestampTicks).ThenBy(row => row.FileOffset).ToArray();
                    for (int eventIndex = 0; eventIndex < localOrdered.Length; eventIndex++)
                    {
                        LocalEvent local = localOrdered[eventIndex];
                        SqlEvent sql = sqlOrdered[eventIndex];
                        Assert.AreEqual(sql.Name, local.Name, category + ": event names differ.");
                        long difference = local.TimestampTicks - sql.TimestampTicks;
                        timestampDifferences.Add(difference);
                        maxTimestampDifference = Math.Max(maxTimestampDifference, Math.Abs(difference));
                        if (difference == 0)
                            timestampMatches++;
                        if (local.TimestampTicks - local.TimestampTicks % TimeSpan.TicksPerMillisecond == sql.TimestampTicks)
                            millisecondTimestampMatches++;
                        if (local.UUID == sql.Module)
                            uuidMatchesSqlModule++;
                        if (local.UUID == sql.Package)
                            uuidMatchesSqlPackage++;
                        Assert.IsTrue(local.Start >= 0 && local.End > local.Start, category + ": invalid XeLite event byte offsets.");
                        if (local.Start == sql.FileOffset)
                            offsetMatches++;

                        XElement xml = ParseEventXml(sql.Xml);
                        Assert.AreEqual(local.Name, (string)xml.Attribute("name"));
                        long xmlTicks = DateTimeOffset.Parse((string)xml.Attribute("timestamp"),
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcTicks;
                        if (xmlTicks == local.TimestampTicks)
                            xmlTimestampMatches++;
                        foreach (string field in sql.Values.Keys.Except(local.Values.Keys))
                            missingFields.Add(field);
                        foreach (string field in local.Values.Keys.Except(sql.Values.Keys))
                            unexpectedFields.Add(field);
                        foreach (var field in local.Values)
                        {
                            types.Add(field.Key.Split(':')[0] + ":" + (field.Value == null ? "null" : field.Value.GetType().FullName));
                            if (!sql.Values.TryGetValue(field.Key, out string sqlValue))
                                continue;
                            comparedValues++;
                            if (!String.Equals(FormatValue(field.Value), sqlValue, StringComparison.Ordinal))
                            {
                                mismatchedValues++;
                                mismatchedFields.TryGetValue(field.Key, out long previous);
                                mismatchedFields[field.Key] = previous + 1;
                            }
                        }
                        displayTextValues += xml.Elements()
                            .Count(element => element.Element("text") != null);
                    }
                    eventCount += localEvents.Count;
                }
            }

            TestContext.WriteLine(
                "{0}: files={1}; events={2}; event names matched; SQL-offset/XeLite-start matches={3}/{2}; " +
                "field/action values compared={4}; lexical value differences={5}; SQL display-text elements={6}.",
                category, files.Length, eventCount, offsetMatches, comparedValues, mismatchedValues, displayTextValues);
            TestContext.WriteLine(
                "Exact SQL timestamp column matches={0}/{1}; exact SQL XML timestamp matches={2}/{1}; max timestamp column delta ticks={3}; " +
                "distinct tick deltas={4}; XeLite UUID equals SQL module={5}/{1}, SQL package={6}/{1}.",
                timestampMatches, eventCount, xmlTimestampMatches, maxTimestampDifference,
                String.Join(", ", timestampDifferences.OrderBy(value => value).Take(20)),
                uuidMatchesSqlModule, uuidMatchesSqlPackage);
            TestContext.WriteLine("SQL timestamp matches after truncating XeLite to milliseconds={0}/{1}.",
                millisecondTimestampMatches, eventCount);
            TestContext.WriteLine("SQL columns: " + String.Join(", ", sqlColumns.OrderBy(value => value)));
            TestContext.WriteLine("XeLite field/action value types: " + String.Join(", ", types.OrderBy(value => value)));
            TestContext.WriteLine("Missing field/action names: " + String.Join(", ", missingFields.OrderBy(value => value)));
            TestContext.WriteLine("Unexpected field/action names: " + String.Join(", ", unexpectedFields.OrderBy(value => value)));
            TestContext.WriteLine("Lexical differences by field: " + String.Join(", ", mismatchedFields
                .OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture))));
            TestContext.WriteLine("NOT a full parity certification: raw event XML, package/module GUIDs, enum display text, " +
                "XML value structure, and SQL block-offset semantics require explicit resolution before Phase 3.");
        }

        [DataTestMethod]
        [DataRow(null, "")]
        [DataRow(true, "true")]
        [DataRow(false, "false")]
        [DataRow(42, "42")]
        [DataRow("O'Brien;--", "O'Brien;--")]
        public void FormatValue_PrimitiveValue_UsesInvariantDiagnosticRepresentation(object value, string expected)
        {
            Assert.AreEqual(expected, FormatValue(value));
        }

        [TestMethod]
        public void FormatValue_BinaryValue_UsesHexadecimalWithoutTruncation()
        {
            Assert.AreEqual("0x0001ABFF", FormatValue(new byte[] { 0, 1, 171, 255 }));
        }

        [TestMethod]
        public void ReadSqlValues_NestedXml_PreservesStructureForCompatibilityInspection()
        {
            XElement xml = ParseEventXml("<event><data name=\"xml\"><value><root><child /></root></value></data></event>");

            Assert.AreEqual("<root><child /></root>", ReadSqlValues(xml)["data:xml"]);
        }

        [TestMethod]
        public void ParseEventXml_ExternalEntity_RejectsDocument()
        {
            Assert.ThrowsException<XmlException>(() => ParseEventXml(
                "<!DOCTYPE event [<!ENTITY external SYSTEM 'file:///not-read'>]><event>&external;</event>"));
        }

        [TestMethod]
        public void XeLitePublicApi_DoesNotOfferRawXmlOrSqlPackageModuleGuids()
        {
            string[] properties = typeof(IXEvent).GetProperties().Select(property => property.Name).ToArray();

            CollectionAssert.Contains(properties, "UUID");
            CollectionAssert.Contains(properties, "XEventStartOffsetInBytes");
            Assert.IsFalse(properties.Any(name => name.IndexOf("Xml", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Module", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static string RequiredSetting(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            Assert.IsFalse(String.IsNullOrWhiteSpace(value), name + " is required.");
            return value;
        }

        private static XElement ParseEventXml(string xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var input = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(input, settings))
                return XElement.Load(reader);
        }

        private static Dictionary<string, string> ReadSqlValues(XElement xml)
        {
            return xml.Elements().Where(element => element.Name == "data" || element.Name == "action")
                .ToDictionary(
                    element => element.Name.LocalName + ":" + (string)element.Attribute("name"),
                    element =>
                    {
                        XElement value = element.Element("value");
                        return value == null ? String.Empty : value.HasElements
                            ? String.Concat(value.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)))
                            : value.Value;
                    },
                    StringComparer.Ordinal);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return String.Empty;
            if (value is bool boolean)
                return boolean ? "true" : "false";
            if (value is byte[] bytes)
                return "0x" + BitConverter.ToString(bytes).Replace("-", "");
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private sealed class LocalEvent
        {
            public LocalEvent(IXEvent xe)
            {
                Name = xe.Name;
                TimestampTicks = xe.Timestamp.UtcTicks;
                UUID = xe.UUID;
                Start = xe.XEventStartOffsetInBytes;
                End = xe.XEventEndOffsetInBytes;
                Values = xe.Fields.ToDictionary(pair => "data:" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
                foreach (var action in xe.Actions)
                    Values.Add("action:" + action.Key, action.Value);
            }

            public string Name { get; }
            public long TimestampTicks { get; }
            public Guid UUID { get; }
            public long Start { get; }
            public long End { get; }
            public Dictionary<string, object> Values { get; }
        }

        private sealed class SqlEvent
        {
            public SqlEvent(string name, DateTime timestamp, long offset, Guid module, Guid package, string xml)
            {
                Name = name;
                TimestampTicks = timestamp.Ticks;
                Module = module;
                Package = package;
                FileOffset = offset;
                Xml = xml;
                Values = ReadSqlValues(ParseEventXml(xml));
            }

            public string Name { get; }
            public long TimestampTicks { get; }
            public Guid Module { get; }
            public Guid Package { get; }
            public long FileOffset { get; }
            public string Xml { get; }
            public Dictionary<string, string> Values { get; }
        }
    }
}
