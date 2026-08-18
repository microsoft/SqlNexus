using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class InlineDateTimeTraceListenerTests
    {
        [TestMethod]
        public void TraceEvent_Message_IncludesTimestampAndSeverityWithoutSource()
        {
            var writer = new StringWriter();
            var listener = new global::sqlnexus.InlineDateTimeTraceListener(writer);

            listener.TraceEvent(new TraceEventCache(), "SQLNexus", TraceEventType.Information, 0, "Flushing rowset tbl_test");
            listener.Flush();

            string output = writer.ToString();

            StringAssert.Contains(output, "\tInformation: 0 : Flushing rowset tbl_test");
            Assert.IsFalse(output.Contains("SQLNexus"), "Did not expect the redundant trace source in the output.");
            Assert.IsTrue(Regex.IsMatch(output, @"^\d{4}-\d{2}-\d{2}T.*[\+\-]\d{2}:\d{2}\tInformation: 0 : Flushing rowset tbl_test"),
                "Expected inline local timestamp prefix (with offset) in round-trip format.");
        }

        [TestMethod]
        public void TraceEvent_FormatArgs_FormatsMessageWithoutSource()
        {
            var writer = new StringWriter();
            var listener = new global::sqlnexus.InlineDateTimeTraceListener(writer);

            listener.TraceEvent(new TraceEventCache(), "SQLNexus", TraceEventType.Information, 0, "Flushing rowset {0}", "tbl_test2");
            listener.Flush();

            string output = writer.ToString();

            StringAssert.Contains(output, "\tInformation: 0 : Flushing rowset tbl_test2");
            Assert.IsTrue(Regex.IsMatch(output, @"^\d{4}-\d{2}-\d{2}T"), "Expected timestamp at start of line.");
            Assert.IsFalse(output.StartsWith("DateTime="), "Did not expect 'DateTime=' label in prefix.");
        }

        [TestMethod]
        public void TraceEvent_NonDefaultSourceAndSeverity_OmitsSourceAndPreservesSeverityAndId()
        {
            var writer = new StringWriter();
            var listener = new global::sqlnexus.InlineDateTimeTraceListener(writer);

            listener.TraceEvent(new TraceEventCache(), "OtherModule", TraceEventType.Warning, 42, "Check configuration");
            listener.Flush();

            string output = writer.ToString();

            StringAssert.Contains(output, "\tWarning: 42 : Check configuration");
            Assert.IsFalse(output.Contains("OtherModule"), "Did not expect a trace source in the output.");
        }
    }
}
