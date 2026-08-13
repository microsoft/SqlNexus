using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraceEventImporter.Processing;

namespace SqlNexus.UnitTests.TraceEventImporter.Processing
{
    [TestClass]
    public class AggregationAndUniqueStoreTests
    {
        [TestMethod]
        public void UniqueStore_DuplicateHashesAndNames_ReusesFirstValuesAndIds()
        {
            var store = new UniqueStore();

            Assert.IsTrue(store.TryAddBatch(10, 100, "first", "FIRST", 0));
            Assert.IsFalse(store.TryAddBatch(20, 100, "second", "SECOND", 3));
            int firstAppId = store.GetOrAddAppName("SqlClient");
            int sameAppId = store.GetOrAddAppName("sqlclient");
            int emptyLoginId = store.GetOrAddLoginName(null);
            int sameEmptyLoginId = store.GetOrAddLoginName(string.Empty);

            UniqueBatch unique = store.GetUniqueBatches().Single();
            Assert.AreEqual(10L, unique.Seq);
            Assert.AreEqual("first", unique.OrigText);
            Assert.AreEqual(firstAppId, sameAppId);
            Assert.AreEqual(emptyLoginId, sameEmptyLoginId);
            Assert.AreEqual(1, store.GetUniqueAppNames().Count());
            Assert.AreEqual(1, store.GetUniqueLoginNames().Count());
        }

        [TestMethod]
        public void Compute_BatchesAcrossIntervals_AggregatesCountsAndMetricsPerBucket()
        {
            DateTime start = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            var batches = new List<BatchRow>
            {
                Batch(1, start, start.AddSeconds(10), 10, 2, 1, 4),
                Batch(1, start.AddSeconds(20), start.AddSeconds(30), 30, 6, 3, 8, attention: 99),
                Batch(1, start.AddSeconds(65), start.AddSeconds(70), 50, 10, 5, 12)
            };
            var aggregator = new Aggregator(60);

            aggregator.Compute(batches, new List<StatementRow>());

            Assert.AreEqual(2, aggregator.TimeIntervals.Count);
            Assert.AreEqual(2, aggregator.BatchAggs.Count);

            BatchPartialAggRow first = aggregator.BatchAggs.Single(row => row.TimeInterval == 1);
            Assert.AreEqual(2, first.StartingEvents);
            Assert.AreEqual(2, first.CompletedEvents);
            Assert.AreEqual(1, first.AttentionEvents);
            Assert.AreEqual(40L, first.TotalDuration);
            Assert.AreEqual(10L, first.MinDuration);
            Assert.AreEqual(30L, first.MaxDuration);
            Assert.AreEqual(8L, first.TotalReads);
            Assert.AreEqual(12L, first.TotalCPU);

            BatchPartialAggRow second = aggregator.BatchAggs.Single(row => row.TimeInterval == 2);
            Assert.AreEqual(50L, second.TotalDuration);
            Assert.AreEqual(start.AddSeconds(70), aggregator.TimeIntervals[1].EndTime);
        }

        [TestMethod]
        public void Compute_InvalidInterval_UsesSixtySecondDefault()
        {
            DateTime start = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            var aggregator = new Aggregator(0);

            aggregator.Compute(
                new List<BatchRow> { Batch(1, start, start.AddSeconds(61), 1, 1, 1, 1) },
                new List<StatementRow>());

            Assert.AreEqual(2, aggregator.TimeIntervals.Count);
            Assert.AreEqual(start.AddSeconds(60), aggregator.TimeIntervals[0].EndTime);
        }

        [TestMethod]
        public void Compute_EmptyInput_ProducesNoIntervalsOrAggregates()
        {
            var aggregator = new Aggregator();

            aggregator.Compute(new List<BatchRow>(), new List<StatementRow>());

            Assert.AreEqual(0, aggregator.TimeIntervals.Count);
            Assert.AreEqual(0, aggregator.BatchAggs.Count);
            Assert.AreEqual(0, aggregator.StmtAggs.Count);
        }

        private static BatchRow Batch(
            long hashId,
            DateTime start,
            DateTime end,
            long duration,
            long reads,
            long writes,
            long cpu,
            long? attention = null)
        {
            return new BatchRow
            {
                HashID = hashId,
                StartTime = start,
                EndTime = end,
                Duration = duration,
                Reads = reads,
                Writes = writes,
                CPU = cpu,
                AttnSeq = attention,
                DBID = 5,
                AppNameID = 2,
                LoginNameID = 3
            };
        }
    }
}