using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImporterPlugin = global::TraceEventImporter.TraceEventImporterPlugin;

namespace SqlNexus.UnitTests.TraceEventImporter
{
    [TestClass]
    public class TraceEventImporterPluginTests
    {
        [TestMethod]
        public void Constructor_DefaultConfiguration_MatchesImporterContract()
        {
            var importer = new ImporterPlugin();

            CollectionAssert.AreEqual(
                new[] { "*pssdiag*.xel", "*LogScout*.xel" },
                importer.SupportedMasks);
            CollectionAssert.AreEqual(
                new[] { "ReadTracePostProcessing.sql" },
                importer.PostScripts);
            Assert.AreEqual(true, importer.Options["Enabled"]);
            Assert.AreEqual(true, importer.Options["Drop existing ReadTrace tables"]);
            Assert.AreEqual(0, importer.Options["Aggregation interval (seconds)"]);
            Assert.AreEqual(false, importer.Options["Import events using local server time (not UTC)"]);
        }

        [TestMethod]
        public void EmbeddedSchemaResources_ArePresentAndNonEmpty()
        {
            Assembly assembly = typeof(ImporterPlugin).Assembly;

            string createSchema = ReadResource(assembly, "TraceEventImporter.Schema.CreateSchema.sql");
            string postLoadFixups = ReadResource(assembly, "TraceEventImporter.Schema.PostLoadFixups.sql");

            StringAssert.Contains(createSchema, "CREATE TABLE ReadTrace.tblBatches");
            StringAssert.Contains(postLoadFixups, "ALTER TABLE ReadTrace.tblBatches");
        }

        [TestMethod]
        public void CreateSchema_AggregationView_ExposesExpectedColumnsAndEmptyIntervals()
        {
            string sql = ReadResource(typeof(ImporterPlugin).Assembly, "TraceEventImporter.Schema.CreateSchema.sql");
            Match view = Regex.Match(
                sql,
                @"CREATE VIEW ReadTrace\.vwBatchPartialAggsByGroupTimeInterval\s+AS\s+(?<definition>.*?)\s+GO",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Assert.IsTrue(view.Success, "The expected aggregation view definition was not found.");
            string definition = view.Groups["definition"].Value;
            StringAssert.Contains(definition, "t.StartTime");
            StringAssert.Contains(definition, "t.EndTime");
            StringAssert.Contains(definition, "t.TimeInterval");
            StringAssert.Contains(definition, "ISNULL(SUM(a.CompletedEvents), 0) AS CompletedEvents");
            StringAssert.Contains(definition, "FROM ReadTrace.tblTimeIntervals t");
            StringAssert.Contains(definition, "LEFT JOIN ReadTrace.tblBatchPartialAggs a");
        }

        [TestMethod]
        public void PostLoadFixups_UsesReadTraceCompatibleIndexNames()
        {
            string sql = ReadResource(typeof(ImporterPlugin).Assembly, "TraceEventImporter.Schema.PostLoadFixups.sql");

            StringAssert.Contains(sql, "CREATE NONCLUSTERED INDEX tblBatches_HashID");
            StringAssert.Contains(sql, "CREATE NONCLUSTERED INDEX tblStatements_HashID");
            Assert.IsFalse(Regex.IsMatch(sql, @"CREATE\s+(?:NONCLUSTERED\s+)?INDEX\s+IX_", RegexOptions.IgnoreCase));
        }

        private static string ReadResource(Assembly assembly, string resourceName)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                Assert.IsNotNull(stream, "Embedded resource was not found: " + resourceName);
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

    }
}