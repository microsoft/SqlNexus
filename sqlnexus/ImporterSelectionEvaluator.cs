using System;
using System.Collections.Generic;

namespace sqlnexus
{
    /// <summary>
    /// The outcome of evaluating a single importer against a /M token selection.
    /// </summary>
    internal enum ImporterGateResult
    {
        /// <summary>Runs unconditionally (Rowset Importer - core dependency).</summary>
        ForcedOn,
        /// <summary>Explicitly requested by a /M token and eligible to run.</summary>
        EnabledByToken,
        /// <summary>Wired to a /M token but that token was not selected.</summary>
        NotSelected,
        /// <summary>Selected, but suppressed because the other (preferred) trace importer is also selected.</summary>
        SuppressedByTraceExclusivity,
        /// <summary>Discovered importer that is not wired to any /M token (ignored by /M by design).</summary>
        NotWired
    }

    /// <summary>
    /// Pure, UI-free evaluation of the /M importer-selection semantics.
    ///
    /// This is the single source of truth for how a parsed /M token set maps to concrete
    /// runtime decisions (which importers run, mandatory Rowset, trace mutual-exclusivity,
    /// CustomXEL gating) and for the process exit code. Keeping it free of WinForms types
    /// makes the runtime gating logic unit-testable (see ImporterSelectionEvaluatorTests).
    /// </summary>
    internal static class ImporterSelectionEvaluator
    {
        /// <summary>Rowset Importer always runs - it populates core tables all other stages depend on.</summary>
        public const string RowsetImporterName = "Rowset Importer";
        public const string ReadTraceImporterName = "ReadTrace (SQL XEL/TRC Files)";
        public const string TraceEventImporterName = "Trace Event Importer (Managed)";

        /// <summary>The /M token for the CustomXEL importer (handled outside the INexusImporter menu).</summary>
        public const string CustomXelToken = "CustomXEL";

        /// <summary>The /M token for the preferred managed trace importer.</summary>
        public const string TraceEventToken = "TraceEventImporter";

        /// <summary>The /M token for the classic ReadTrace importer.</summary>
        public const string ReadTraceToken = "ReadTrace";

        /// <summary>
        /// Maps /M command-line tokens (case-insensitive) to the exact INexusImporter.Name values
        /// returned at runtime. CustomXEL is handled separately (it is not an INexusImporter menu item).
        /// </summary>
        public static readonly IDictionary<string, string> ImporterTokenToName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ReadTrace",          ReadTraceImporterName },
                { "Perfmon",            "BLG Blaster (Perfmon/Sysmon BLG files)" },
                { "Linux",              "Import Linux Performance Files (.perf)" },
                { "Errorlog",           "ERRORLOG Importer" },
                { TraceEventToken,      TraceEventImporterName },
            };

        /// <summary>
        /// True if the importer name is wired to a /M token (i.e. /M can control it).
        /// </summary>
        public static bool IsWiredImporter(string importerName)
        {
            if (string.IsNullOrEmpty(importerName))
                return false;

            foreach (var kvp in ImporterTokenToName)
            {
                if (string.Equals(kvp.Value, importerName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Evaluates a single importer against the selected /M token set.
        /// </summary>
        /// <param name="importerName">The INexusImporter.Name value.</param>
        /// <param name="selectedTokens">The parsed /M canonical token set (never null).</param>
        public static ImporterGateResult Evaluate(string importerName, ISet<string> selectedTokens)
        {
            if (selectedTokens == null)
                throw new ArgumentNullException(nameof(selectedTokens));

            // Rowset always runs, regardless of the token set.
            if (string.Equals(importerName, RowsetImporterName, StringComparison.OrdinalIgnoreCase))
                return ImporterGateResult.ForcedOn;

            string matchedToken = null;
            foreach (var kvp in ImporterTokenToName)
            {
                if (string.Equals(kvp.Value, importerName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedToken = kvp.Key;
                    break;
                }
            }

            // Discovered importer with no /M token: never enabled by /M (drop-in/unknown assemblies).
            if (matchedToken == null)
                return ImporterGateResult.NotWired;

            if (!selectedTokens.Contains(matchedToken))
                return ImporterGateResult.NotSelected;

            // Trace mutual-exclusivity: ReadTrace and TraceEventImporter write to the same
            // ReadTrace.* schema and must not run together. TraceEventImporter wins.
            if (string.Equals(importerName, ReadTraceImporterName, StringComparison.OrdinalIgnoreCase)
                && selectedTokens.Contains(TraceEventToken))
            {
                return ImporterGateResult.SuppressedByTraceExclusivity;
            }

            return ImporterGateResult.EnabledByToken;
        }

        /// <summary>
        /// True if the evaluation result means the importer will actually run.
        /// </summary>
        public static bool WillRun(ImporterGateResult result)
        {
            return result == ImporterGateResult.ForcedOn
                || result == ImporterGateResult.EnabledByToken;
        }

        /// <summary>
        /// Whether the CustomXEL importer should run for the given /M selection.
        /// </summary>
        public static bool IsCustomXelSelected(ISet<string> selectedTokens)
        {
            return selectedTokens != null && selectedTokens.Contains(CustomXelToken);
        }

        /// <summary>
        /// The outcome of resolving trace-importer availability against the /M selection.
        /// </summary>
        public enum TraceFallbackResult
        {
            /// <summary>No change needed (not a trace selection, or the preferred importer is available).</summary>
            None,
            /// <summary>Preferred TraceEventImporter is missing; fell back to ReadTrace (swapped tokens).</summary>
            FellBackToReadTrace,
            /// <summary>ReadTrace is missing; the managed TraceEventImporter will be used instead.</summary>
            FellBackToTraceEvent,
            /// <summary>Trace data was requested but no trace importer is installed at all.</summary>
            NoTraceImporterAvailable
        }

        /// <summary>
        /// Resolves trace-importer availability for a /M run so that trace data is imported by
        /// whichever trace importer is actually installed. The two trace importers are one logical
        /// capability writing to the same ReadTrace.* schema; TraceEventImporter is preferred, but
        /// if its assembly is not discovered we transparently fall back to ReadTrace (and vice
        /// versa). The <paramref name="selectedTokens"/> set is mutated in place to reflect the
        /// resolved choice so the normal gating in <see cref="Evaluate"/> enables the right one.
        /// </summary>
        /// <param name="selectedTokens">The parsed /M token set (mutated in place). May be null (no /M run).</param>
        /// <param name="isImporterDiscovered">Predicate: is the importer with this INexusImporter.Name available?</param>
        public static TraceFallbackResult ResolveTraceFallback(
            ISet<string> selectedTokens, Func<string, bool> isImporterDiscovered)
        {
            if (isImporterDiscovered == null)
                throw new ArgumentNullException(nameof(isImporterDiscovered));

            // No /M selection, or no trace capability requested: nothing to resolve.
            if (selectedTokens == null)
                return TraceFallbackResult.None;

            bool wantsTraceEvent = selectedTokens.Contains(TraceEventToken);
            bool wantsReadTrace = selectedTokens.Contains(ReadTraceToken);
            if (!wantsTraceEvent && !wantsReadTrace)
                return TraceFallbackResult.None;

            bool traceEventAvailable = isImporterDiscovered(TraceEventImporterName);
            bool readTraceAvailable = isImporterDiscovered(ReadTraceImporterName);

            // Preferred managed importer requested but unavailable: fall back to ReadTrace if present.
            if (wantsTraceEvent && !traceEventAvailable)
            {
                if (readTraceAvailable)
                {
                    selectedTokens.Remove(TraceEventToken);
                    selectedTokens.Add(ReadTraceToken);
                    return TraceFallbackResult.FellBackToReadTrace;
                }
                // Neither trace importer available at all.
                if (!wantsReadTrace)
                    return TraceFallbackResult.NoTraceImporterAvailable;
            }

            // ReadTrace requested but unavailable: use the managed importer if present.
            if (wantsReadTrace && !readTraceAvailable)
            {
                if (traceEventAvailable)
                {
                    selectedTokens.Remove(ReadTraceToken);
                    selectedTokens.Add(TraceEventToken);
                    return TraceFallbackResult.FellBackToTraceEvent;
                }
                if (!wantsTraceEvent)
                    return TraceFallbackResult.NoTraceImporterAvailable;
            }

            // If both were requested and at least one is available, existing exclusivity handles it.
            if ((wantsTraceEvent || wantsReadTrace) && !traceEventAvailable && !readTraceAvailable)
                return TraceFallbackResult.NoTraceImporterAvailable;

            return TraceFallbackResult.None;
        }

        /// <summary>
        /// Decides the process exit code.
        /// - Core (Rowset) failure  -> Exception (2): the load is unusable.
        /// - A requested importer was missing, imported nothing, or failed -> ImportIncomplete (3):
        ///   automation asked for data that did not arrive, so it must not see success.
        /// - Otherwise -> Normal (0).
        /// </summary>
        public static ProgramExitCodes DecideExitCode(bool coreImporterSuccessful, bool anyRequestedImporterMissingOrEmpty)
        {
            if (!coreImporterSuccessful)
                return ProgramExitCodes.Exception;

            if (anyRequestedImporterMissingOrEmpty)
                return ProgramExitCodes.ImportIncomplete;

            return ProgramExitCodes.Normal;
        }
    }
}
