using System.Collections.Generic;
using TraceEventImporter.Models;

namespace TraceEventImporter.Readers
{
    /// <summary>
    /// Abstraction for reading trace events from different file formats (.trc, .xel).
    /// Implementations yield events in file order with monotonically increasing Seq numbers.
    /// </summary>
    public interface ITraceEventReader
    {
        /// <summary>
        /// Read all trace events from the specified file.
        /// </summary>
        /// <param name="filePath">Path to the trace file</param>
        /// <returns>Enumerable of trace events in file order</returns>
        IEnumerable<TraceEvent> ReadEvents(string filePath);

        /// <summary>
        /// File extensions supported by this reader (e.g., ".trc", ".xel").
        /// </summary>
        string[] SupportedExtensions { get; }

        /// <summary>
        /// Number of non-fatal field/action extraction failures encountered during the
        /// most recent read. A high count indicates a schema/mapping mismatch.
        /// </summary>
        long MappingErrorCount { get; }

        /// <summary>
        /// A capped, de-duplicated sample of mapping-failure messages for diagnostics.
        /// </summary>
        IEnumerable<string> MappingErrorSamples { get; }
    }
}
