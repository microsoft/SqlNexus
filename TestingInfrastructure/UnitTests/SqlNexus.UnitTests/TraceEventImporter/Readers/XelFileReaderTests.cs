using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraceEventImporter.Models;
using TraceEventImporter.Readers;

namespace SqlNexus.UnitTests.TraceEventImporter.Readers
{
    [TestClass]
    public class XelFileReaderTests
    {
        [TestMethod]
        public void SetCompletedEventTimes_TimestampAndDuration_DerivesStartFromCompletion()
        {
            DateTime completion = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
            var traceEvent = new TraceEvent
            {
                StartTime = completion,
                Duration = 2500000
            };

            InvokeSetCompletedEventTimes(traceEvent);

            Assert.AreEqual(completion.AddMilliseconds(-2500), traceEvent.StartTime);
            Assert.AreEqual(completion, traceEvent.EndTime);
        }

        [TestMethod]
        public void SetCompletedEventTimes_MissingDuration_LeavesTimestampUnchanged()
        {
            DateTime timestamp = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
            var traceEvent = new TraceEvent { StartTime = timestamp };

            InvokeSetCompletedEventTimes(traceEvent);

            Assert.AreEqual(timestamp, traceEvent.StartTime);
            Assert.IsNull(traceEvent.EndTime);
        }

        private static void InvokeSetCompletedEventTimes(TraceEvent traceEvent)
        {
            MethodInfo method = typeof(XelFileReader).GetMethod(
                "SetCompletedEventTimes",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { traceEvent });
        }
    }
}
