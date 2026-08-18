using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace sqlnexus
{
    /// <summary>
    /// A <see cref="TextWriterTraceListener"/> that writes the local timestamp inline, on the SAME
    /// line as the trace message, e.g.:
    ///
    ///   2026-07-31T08:59:11.3419836-07:00\tSQLNexus Information: 0 : &lt;message&gt;
    ///
    /// The default listener with <see cref="TraceOptions.DateTime"/> emits the timestamp as a
    /// footer on its own indented line, which makes it hard to correlate a timestamp with its
    /// message. This listener leaves <see cref="TraceListener.TraceOutputOptions"/> at
    /// <see cref="TraceOptions.None"/> and prepends the timestamp before the standard header,
    /// keeping one log entry per line.
    /// </summary>
    internal sealed class InlineDateTimeTraceListener : TextWriterTraceListener
    {
        public InlineDateTimeTraceListener(Stream stream) : base(stream)
        {
        }

        public InlineDateTimeTraceListener(TextWriter writer) : base(writer)
        {
        }

        private void WriteTimestampPrefix()
        {
            // Write the round-trip local timestamp (with offset), followed by a tab so the message that the base
            // class writes next lands on the same line.
            Write(DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + "\t");
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
                return;

            WriteTimestampPrefix();
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
                return;

            WriteTimestampPrefix();
            base.TraceEvent(eventCache, source, eventType, id, format, args);
        }
    }
}
