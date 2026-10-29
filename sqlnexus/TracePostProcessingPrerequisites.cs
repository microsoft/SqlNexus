using System;

namespace sqlnexus
{
    internal sealed class TracePostProcessingPrerequisites
    {
        internal const string TraceScript = "TraceTimestampPostProcessing.sql";
        internal const string GeneralScript = "SQLNexus_PostProcessing.sql";
        private bool traceAttempted;
        private bool traceFailed;

        internal bool CanRun => traceAttempted && !traceFailed;

        internal string[] Scripts => CanRun
            ? new[] { TraceScript, GeneralScript }
            : new[] { GeneralScript };

        internal void RecordResult(string importerName, bool success, bool cancelled)
        {
            if (!string.Equals(importerName, ImporterSelectionEvaluator.ReadTraceImporterName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(importerName, ImporterSelectionEvaluator.TraceEventImporterName, StringComparison.OrdinalIgnoreCase))
                return;

            traceAttempted = true;
            traceFailed |= !success || cancelled;
        }
    }
}
