using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    /// <summary>
    /// Regression tests that lock in the intended exit-code outcome for the failure/edge scenarios
    /// that previously could exit 0 (success) incorrectly:
    ///  - a fatal exception during import,
    ///  - a cancelled /M run,
    ///  - a /M-selected importer (including CustomXEL) that matched no files.
    ///
    /// The runtime code (fmImport.DoImport / Program.Main) is WinForms/SQL-coupled and cannot be
    /// unit-tested deterministically, so it records the outcome by setting the two Globals flags:
    ///   Globals.IsNexusCoreImporterSuccessful  and  Globals.RequestedImporterMissingOrEmpty.
    /// These tests assert the flag -> exit-code contract those code paths rely on, so a regression
    /// in DecideExitCode (or a change to what the flags are expected to mean) is caught here.
    /// </summary>
    [TestClass]
    public class ImporterExitCodeScenarioTests
    {
        // A fatal exception now sets core-failure (and, under /M, requested-missing). Either way the
        // run must not report success.
        [TestMethod]
        public void FatalException_SetsCoreFailure_ExitsException()
        {
            // Arrange: what the Main / DoImport catch blocks now set on a fatal error.
            bool coreSuccessful = false;
            bool requestedMissingOrEmpty = true; // set only under /M, but core-failure dominates anyway.

            // Act
            var code = ImporterSelectionEvaluator.DecideExitCode(coreSuccessful, requestedMissingOrEmpty);

            // Assert
            Assert.AreEqual(ProgramExitCodes.Exception, code);
        }

        // A non-/M fatal exception (EnabledImporters == null) still fails core, so still non-zero.
        [TestMethod]
        public void FatalException_NonMRun_StillExitsException()
        {
            var code = ImporterSelectionEvaluator.DecideExitCode(
                coreImporterSuccessful: false, anyRequestedImporterMissingOrEmpty: false);
            Assert.AreEqual(ProgramExitCodes.Exception, code);
        }

        // A cancelled /M run leaves core intact but marks the import incomplete.
        [TestMethod]
        public void CancelledMRun_MarksIncomplete_ExitsImportIncomplete()
        {
            var code = ImporterSelectionEvaluator.DecideExitCode(
                coreImporterSuccessful: true, anyRequestedImporterMissingOrEmpty: true);
            Assert.AreEqual(ProgramExitCodes.ImportIncomplete, code);
        }

        // A /M-selected importer (or CustomXEL) that matched no files marks the import incomplete.
        [TestMethod]
        public void RequestedImporterEmpty_ExitsImportIncomplete()
        {
            var code = ImporterSelectionEvaluator.DecideExitCode(
                coreImporterSuccessful: true, anyRequestedImporterMissingOrEmpty: true);
            Assert.AreEqual(ProgramExitCodes.ImportIncomplete, code);
        }

        // Sanity: a fully successful /M run (core ok, nothing missing) still exits Normal.
        [TestMethod]
        public void SuccessfulMRun_ExitsNormal()
        {
            var code = ImporterSelectionEvaluator.DecideExitCode(
                coreImporterSuccessful: true, anyRequestedImporterMissingOrEmpty: false);
            Assert.AreEqual(ProgramExitCodes.Normal, code);
        }

        // Regression guard for the masking bug: a later importer must not be able to flip a Rowset
        // (core) failure back to success. This models the corrected fmImport behavior where only the
        // Rowset importer sets IsNexusCoreImporterSuccessful.
        [TestMethod]
        public void CoreFailure_NotMaskedByLaterImporterSuccess_ExitsException()
        {
            // Arrange: Rowset failed -> false. A later importer succeeds but MUST NOT set this true.
            bool coreSuccessful = false; // stays false after subsequent successful importers.

            // Act
            var code = ImporterSelectionEvaluator.DecideExitCode(
                coreSuccessful, anyRequestedImporterMissingOrEmpty: false);

            // Assert
            Assert.AreEqual(ProgramExitCodes.Exception, code);
        }

        // TODO (integration): end-to-end coverage of ProcessCmdLine -> EnumFiles -> DoImport -> Main
        // (settings preservation, CustomXEL emptiness, cancellation, exceptions, process exit codes)
        // requires refactoring the WinForms/SQL-coupled paths behind seams and/or InternalsVisibleTo.
        // Tracked separately; these tests cover the flag -> exit-code contract those paths depend on.
    }
}
