using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraceEventImporter.Models;
using TraceEventImporter.Processing;

namespace SqlNexus.UnitTests.TraceEventImporter.Processing
{
    [TestClass]
    public class EventProcessorTests
    {
        [TestMethod]
        public void ProcessEvent_CorrelatedBatchStatementAndAttention_PreservesRelationships()
        {
            var store = new UniqueStore();
            var processor = new EventProcessor(store);
            DateTime start = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            processor.ProcessEvent(Event(1, TraceEventType.AuditLogin, 51, 0, start, app: "SqlClient", login: "user"));
            processor.ProcessEvent(Event(10, TraceEventType.SqlBatchStarting, 51, 2, start, text: "select 42"));
            processor.ProcessEvent(Event(11, TraceEventType.StmtStarting, 51, 2, start.AddSeconds(1), text: "select 42"));
            processor.ProcessEvent(Event(12, TraceEventType.StmtCompleted, 51, 2, start.AddSeconds(2), text: "select 42", duration: 10));
            processor.ProcessEvent(Event(13, TraceEventType.Attention, 51, 2, start.AddSeconds(3)));
            processor.ProcessEvent(Event(14, TraceEventType.SqlBatchCompleted, 51, 2, start.AddSeconds(4), text: "select 42", duration: 40));

            Assert.AreEqual(1, processor.Batches.Count);
            Assert.AreEqual(1, processor.Statements.Count);

            BatchRow batch = processor.Batches.Single();
            Assert.AreEqual(10L, batch.BatchSeq);
            Assert.AreEqual(1L, batch.ConnSeq);
            Assert.AreEqual(13L, batch.AttnSeq);
            Assert.AreEqual(40L, batch.Duration);

            StatementRow statement = processor.Statements.Single();
            Assert.AreEqual(11L, statement.StmtSeq);
            Assert.AreEqual(10L, statement.BatchSeq);
            Assert.AreEqual(1L, statement.ConnSeq);
            Assert.AreEqual("SELECT {##}", store.GetUniqueStatements().Single().NormText);
        }

        [TestMethod]
        public void ProcessEvent_CompletedBatchWithoutLogin_CreatesSinglePlaceholderConnection()
        {
            var processor = new EventProcessor(new UniqueStore());
            DateTime time = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

            processor.ProcessEvent(Event(20, TraceEventType.SqlBatchCompleted, 9, 0, time, text: "select 1"));
            processor.ProcessEvent(Event(21, TraceEventType.SqlBatchCompleted, 9, 0, time.AddSeconds(1), text: "select 2"));

            Assert.AreEqual(1, processor.Connections.Count);
            Assert.AreEqual("CONNECTED BEFORE TRACE", processor.Connections[0].ApplicationName);
            Assert.AreEqual(processor.Batches[0].ConnSeq, processor.Batches[1].ConnSeq);
        }

        [TestMethod]
        public void ProcessEvent_RpcCompleted_NormalizesInnerSqlAndRecordsSpecialProcedure()
        {
            var store = new UniqueStore();
            var processor = new EventProcessor(store);
            TraceEvent completed = Event(
                30,
                TraceEventType.RpcCompleted,
                12,
                0,
                DateTime.UtcNow,
                "EXEC sp_executesql N'SELECT * FROM dbo.T WHERE Id = 7'",
                objectName: "master.dbo.sp_executesql");
            completed.DatabaseId = 5;
            completed.ObjectId = 77;

            processor.ProcessEvent(completed);

            UniqueBatch unique = store.GetUniqueBatches().Single();
            Assert.AreEqual("SELECT * FROM DBO.T WHERE ID = {##}", unique.NormText);
            Assert.AreEqual((byte)3, unique.SpecialProcID);
            Assert.AreEqual("master.dbo.sp_executesql", store.GetProcedureNames().Single().Name);
        }

        private static TraceEvent Event(
            long seq,
            TraceEventType type,
            int session,
            int request,
            DateTime time,
            string text = null,
            long? duration = null,
            string app = null,
            string login = null,
            string objectName = null)
        {
            return new TraceEvent
            {
                Seq = seq,
                EventId = (int)type,
                EventType = type,
                SessionId = session,
                RequestId = request,
                StartTime = time,
                EndTime = type == TraceEventType.SqlBatchCompleted || type == TraceEventType.RpcCompleted || type == TraceEventType.StmtCompleted
                    ? (DateTime?)time
                    : null,
                TextData = text,
                Duration = duration,
                ApplicationName = app,
                LoginName = login,
                ObjectName = objectName
            };
        }
    }
}