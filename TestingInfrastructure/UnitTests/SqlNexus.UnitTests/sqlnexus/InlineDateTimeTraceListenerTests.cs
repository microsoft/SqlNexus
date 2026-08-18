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
        public void TraceEvent_Message_IncludesInlineLocalTimestampPrefix()
        {
            var writer = new StringWriter();
            var listener = new global::sqlnexus.InlineDateTimeTraceListener(writer);

            listener.TraceEvent(new TraceEventCache(), "SQLNexus", TraceEventType.Information, 0, "Flushing rowset tbl_test");
            listener.Flush();

            string output = writer.ToString();

            StringAssert.Contains(output, "\tSQLNexus Information: 0 : Flushing rowset tbl_test");
            Assert.IsTrue(Regex.IsMatch(output, @"^\d{4}-\d{2}-\d{2}T.*[\+\-]\d{2}:\d{2}\tSQLNexus Information: 0 : Flushing rowset tbl_test"),
                "Expected inline local timestamp prefix (with offset) in round-trip format.");
        }

        [TestMethod]
        public void TraceEvent_FormatArgs_IncludesInlineUtcTimestampPrefix()
        {
            var writer = new StringWriter();
            var listener = new global::sqlnexus.InlineDateTimeTraceListener(writer);

            listener.TraceEvent(new TraceEventCache(), "SQLNexus", TraceEventType.Information, 0, "Flushing rowset {0}", "tbl_test2");
            listener.Flush();

            string output = writer.ToString();

            StringAssert.Contains(output, "\tSQLNexus Information: 0 : Flushing rowset tbl_test2");
            Assert.IsTrue(Regex.IsMatch(output, @"^\d{4}-\d{2}-\d{2}T"), "Expected timestamp at start of line.");
            Assert.IsFalse(output.StartsWith("DateTime="), "Did not expect 'DateTime=' label in prefix.");
        }
    }
}
