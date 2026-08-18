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
    ///   2026-07-31T08:59:11.3419836-07:00\tInformation: 0 : &lt;message&gt;
    ///
    /// The default listener with <see cref="TraceOptions.DateTime"/> emits the timestamp as a
    /// footer on its own indented line, which makes it hard to correlate a timestamp with its
    /// message. This listener leaves <see cref="TraceListener.TraceOutputOptions"/> at
    /// <see cref="TraceOptions.None"/> and prepends the timestamp before a severity and event ID
    /// header, keeping one log entry per line. The trace source is retained for filtering but is
    /// omitted from the output because SQLNexus is the application's only trace source.
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

        private void WriteEvent(TraceEventType eventType, int id, string message)
        {
            WriteTimestampPrefix();
            WriteLine(eventType.ToString() + ": " + id.ToString(CultureInfo.InvariantCulture) + " : " + message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
                return;

            WriteEvent(eventType, id, message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
                return;

            WriteEvent(eventType, id, args == null
                ? format
                : String.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}
