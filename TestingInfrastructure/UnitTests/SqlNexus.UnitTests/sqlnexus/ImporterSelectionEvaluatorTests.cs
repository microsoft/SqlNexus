using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    /// <summary>
    /// Tests for the runtime /M gating semantics in ImporterSelectionEvaluator: mandatory Rowset,
    /// token gating, trace mutual-exclusivity, unwired importers, CustomXEL selection, and the
    /// process exit-code decision. Complements ImporterSelectionTests (which covers /M parsing).
    /// </summary>
    [TestClass]
    public class ImporterSelectionEvaluatorTests
    {
        private static HashSet<string> Tokens(params string[] tokens)
        {
            return new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
        }

        // ---- Mandatory Rowset ------------------------------------------------

        [TestMethod]
        public void Rowset_AlwaysForcedOn_EvenWhenNotInTokens()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                ImporterSelectionEvaluator.RowsetImporterName, Tokens("Perfmon"));
            Assert.AreEqual(ImporterGateResult.ForcedOn, result);
            Assert.IsTrue(ImporterSelectionEvaluator.WillRun(result));
        }

        [TestMethod]
        public void Rowset_ForcedOn_EvenWithEmptySelection()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                ImporterSelectionEvaluator.RowsetImporterName, Tokens());
            Assert.AreEqual(ImporterGateResult.ForcedOn, result);
        }

        // ---- Token gating ----------------------------------------------------

        [TestMethod]
        public void WiredImporter_Selected_IsEnabled()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                "BLG Blaster (Perfmon/Sysmon BLG files)", Tokens("Perfmon"));
            Assert.AreEqual(ImporterGateResult.EnabledByToken, result);
            Assert.IsTrue(ImporterSelectionEvaluator.WillRun(result));
        }

        [TestMethod]
        public void WiredImporter_NotSelected_IsNotEnabled()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                "ERRORLOG Importer", Tokens("Perfmon"));
            Assert.AreEqual(ImporterGateResult.NotSelected, result);
            Assert.IsFalse(ImporterSelectionEvaluator.WillRun(result));
        }

        [TestMethod]
        public void UnwiredImporter_IsNeverEnabledByM()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                "Some Third-Party Drop-In Importer", Tokens("Perfmon", "Errorlog"));
            Assert.AreEqual(ImporterGateResult.NotWired, result);
            Assert.IsFalse(ImporterSelectionEvaluator.WillRun(result));
        }

        // ---- Trace mutual-exclusivity ----------------------------------------

        [TestMethod]
        public void ReadTrace_Alone_IsEnabled()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                ImporterSelectionEvaluator.ReadTraceImporterName, Tokens("ReadTrace"));
            Assert.AreEqual(ImporterGateResult.EnabledByToken, result);
        }

        [TestMethod]
        public void ReadTrace_WithTraceEvent_IsSuppressed()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                ImporterSelectionEvaluator.ReadTraceImporterName,
                Tokens("ReadTrace", "TraceEventImporter"));
            Assert.AreEqual(ImporterGateResult.SuppressedByTraceExclusivity, result);
            Assert.IsFalse(ImporterSelectionEvaluator.WillRun(result));
        }

        [TestMethod]
        public void TraceEvent_WithReadTrace_StillRuns()
        {
            var result = ImporterSelectionEvaluator.Evaluate(
                ImporterSelectionEvaluator.TraceEventImporterName,
                Tokens("ReadTrace", "TraceEventImporter"));
            Assert.AreEqual(ImporterGateResult.EnabledByToken, result);
            Assert.IsTrue(ImporterSelectionEvaluator.WillRun(result));
        }

        // ---- IsWiredImporter -------------------------------------------------

        [DataTestMethod]
        [DataRow("BLG Blaster (Perfmon/Sysmon BLG files)", true)]
        [DataRow("ERRORLOG Importer", true)]
        [DataRow("ReadTrace (SQL XEL/TRC Files)", true)]
        [DataRow("Trace Event Importer (Managed)", true)]
        [DataRow("Rowset Importer", false)] // forced-on, but not token-wired
        [DataRow("Unknown", false)]
        [DataRow(null, false)]
        [DataRow("", false)]
        public void IsWiredImporter_ClassifiesCorrectly(string name, bool expected)
        {
            Assert.AreEqual(expected, ImporterSelectionEvaluator.IsWiredImporter(name));
        }

        // ---- CustomXEL -------------------------------------------------------

        [TestMethod]
        public void CustomXel_Selected_IsTrue()
        {
            Assert.IsTrue(ImporterSelectionEvaluator.IsCustomXelSelected(Tokens("CustomXEL")));
        }

        [TestMethod]
        public void CustomXel_NotSelected_IsFalse()
        {
            Assert.IsFalse(ImporterSelectionEvaluator.IsCustomXelSelected(Tokens("Perfmon")));
        }

        [TestMethod]
        public void CustomXel_NullSelection_IsFalse()
        {
            Assert.IsFalse(ImporterSelectionEvaluator.IsCustomXelSelected(null));
        }

        // ---- Null guard ------------------------------------------------------

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Evaluate_NullTokens_Throws()
        {
            ImporterSelectionEvaluator.Evaluate("ERRORLOG Importer", null);
        }

        // ---- Exit-code decision ----------------------------------------------

        [TestMethod]
        public void ExitCode_CoreFailure_IsException()
        {
            Assert.AreEqual(ProgramExitCodes.Exception,
                ImporterSelectionEvaluator.DecideExitCode(coreImporterSuccessful: false,
                    anyRequestedImporterMissingOrEmpty: false));
        }

        [TestMethod]
        public void ExitCode_CoreFailure_TakesPrecedenceOverMissing()
        {
            Assert.AreEqual(ProgramExitCodes.Exception,
                ImporterSelectionEvaluator.DecideExitCode(coreImporterSuccessful: false,
                    anyRequestedImporterMissingOrEmpty: true));
        }

        [TestMethod]
        public void ExitCode_RequestedMissingOrEmpty_IsImportIncomplete()
        {
            Assert.AreEqual(ProgramExitCodes.ImportIncomplete,
                ImporterSelectionEvaluator.DecideExitCode(coreImporterSuccessful: true,
                    anyRequestedImporterMissingOrEmpty: true));
        }

        [TestMethod]
        public void ExitCode_AllGood_IsNormal()
        {
            Assert.AreEqual(ProgramExitCodes.Normal,
                ImporterSelectionEvaluator.DecideExitCode(coreImporterSuccessful: true,
                    anyRequestedImporterMissingOrEmpty: false));
        }
    }
}
