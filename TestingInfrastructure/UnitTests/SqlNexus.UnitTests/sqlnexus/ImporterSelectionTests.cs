using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    /// <summary>
    /// Tests for the /M importer-selection switch (Program.TryParseImporterSelection).
    ///
    /// The /M switch controls which built-in importers run. Supported syntaxes (tokens are
    /// case-insensitive):
    ///   /MReadTrace+Perfmon        additive: only the listed importers
    ///   /MAll                      every importer
    ///   /MAll-ReadTrace            subtractive: every importer except the listed ones
    /// Unknown/empty tokens are rejected, and '+' (add) and '-' (subtract) cannot be mixed.
    /// The two XEvent trace importers (ReadTrace and TraceEventImporter) are one logical
    /// capability: subtracting either (or the generic "Trace") suppresses BOTH.
    /// </summary>
    [TestClass]
    public class ImporterSelectionTests
    {
        private static HashSet<string> Parse(string mVal)
        {
            HashSet<string> selected;
            bool ok = Program.TryParseImporterSelection(mVal, out selected);
            Assert.IsTrue(ok, "Expected '{0}' to parse successfully.", mVal);
            Assert.IsNotNull(selected);
            return selected;
        }

        private static void AssertRejected(string mVal)
        {
            HashSet<string> selected;
            bool ok = Program.TryParseImporterSelection(mVal, out selected);
            Assert.IsFalse(ok, "Expected '{0}' to be rejected.", mVal);
            Assert.IsNull(selected);
        }

        private static void AssertSetEquals(IEnumerable<string> expected, HashSet<string> actual)
        {
            var expectedSet = new HashSet<string>(expected, System.StringComparer.OrdinalIgnoreCase);
            Assert.IsTrue(expectedSet.SetEquals(actual),
                "Expected [{0}] but got [{1}].",
                string.Join(", ", expectedSet), string.Join(", ", actual));
        }

        // ---- All -------------------------------------------------------------

        [TestMethod]
        public void All_ExpandsToEveryImporter()
        {
            var result = Parse("All");
            AssertSetEquals(
                new[] { "ReadTrace", "Perfmon", "Linux", "Errorlog", "CustomXEL", "TraceEventImporter" },
                result);
        }

        [TestMethod]
        public void All_IsCaseInsensitive()
        {
            var result = Parse("aLL");
            Assert.AreEqual(6, result.Count);
        }

        // ---- Additive syntax -------------------------------------------------

        [TestMethod]
        public void Additive_SingleToken_SelectsOnlyThatImporter()
        {
            var result = Parse("Perfmon");
            AssertSetEquals(new[] { "Perfmon" }, result);
        }

        [TestMethod]
        public void Additive_MultipleTokens_SelectsAllListed()
        {
            var result = Parse("ReadTrace+Perfmon");
            AssertSetEquals(new[] { "ReadTrace", "Perfmon" }, result);
        }

        [TestMethod]
        public void Additive_IsCaseInsensitive()
        {
            var result = Parse("perFMON+errorLOG");
            AssertSetEquals(new[] { "Perfmon", "Errorlog" }, result);
        }

        [TestMethod]
        public void Additive_ToleratesSurroundingWhitespaceAroundTokens()
        {
            var result = Parse("ReadTrace + Perfmon");
            AssertSetEquals(new[] { "ReadTrace", "Perfmon" }, result);
        }

        [TestMethod]
        public void Additive_DuplicateTokens_AreDeduplicated()
        {
            var result = Parse("Perfmon+Perfmon");
            AssertSetEquals(new[] { "Perfmon" }, result);
        }

        [DataTestMethod]
        [DataRow("ReadTrace")]
        [DataRow("Perfmon")]
        [DataRow("Linux")]
        [DataRow("Errorlog")]
        [DataRow("CustomXEL")]
        [DataRow("TraceEventImporter")]
        public void Additive_EachCanonicalToken_IsAccepted(string token)
        {
            var result = Parse(token);
            AssertSetEquals(new[] { token }, result);
        }

        // ---- Trace synonyms --------------------------------------------------

        [DataTestMethod]
        [DataRow("Trace")]
        [DataRow("TraceImp")]
        [DataRow("TraceImporter")]
        public void Additive_TraceSynonyms_CanonicalizeToTraceEventImporter(string synonym)
        {
            var result = Parse(synonym);
            AssertSetEquals(new[] { "TraceEventImporter" }, result);
        }

        // ---- Subtractive syntax ----------------------------------------------

        [TestMethod]
        public void Subtractive_AllMinusOne_RemovesThatImporter()
        {
            var result = Parse("All-Perfmon");
            AssertSetEquals(
                new[] { "ReadTrace", "Linux", "Errorlog", "CustomXEL", "TraceEventImporter" },
                result);
        }

        [TestMethod]
        public void Subtractive_AllMinusMultiple_RemovesEach()
        {
            var result = Parse("All-Perfmon-Linux");
            AssertSetEquals(
                new[] { "ReadTrace", "Errorlog", "CustomXEL", "TraceEventImporter" },
                result);
        }

        [TestMethod]
        public void Subtractive_ResultNeverContainsAllMarker()
        {
            var result = Parse("All-Perfmon");
            Assert.IsFalse(result.Contains("All"));
        }

        [TestMethod]
        public void Subtractive_IsCaseInsensitive()
        {
            var result = Parse("aLL-perFMON");
            Assert.IsFalse(result.Contains("Perfmon"));
            Assert.AreEqual(5, result.Count);
        }

        // ---- Trace is a single logical capability ----------------------------

        [TestMethod]
        public void Subtractive_MinusReadTrace_SuppressesBothTraceImporters()
        {
            var result = Parse("All-ReadTrace");
            Assert.IsFalse(result.Contains("ReadTrace"));
            Assert.IsFalse(result.Contains("TraceEventImporter"));
        }

        [TestMethod]
        public void Subtractive_MinusTraceEventImporter_SuppressesBothTraceImporters()
        {
            var result = Parse("All-TraceEventImporter");
            Assert.IsFalse(result.Contains("ReadTrace"));
            Assert.IsFalse(result.Contains("TraceEventImporter"));
        }

        [TestMethod]
        public void Subtractive_MinusTraceSynonym_SuppressesBothTraceImporters()
        {
            var result = Parse("All-Trace");
            Assert.IsFalse(result.Contains("ReadTrace"));
            Assert.IsFalse(result.Contains("TraceEventImporter"));
            AssertSetEquals(new[] { "Perfmon", "Linux", "Errorlog", "CustomXEL" }, result);
        }

        // ---- Rejected input --------------------------------------------------

        [DataTestMethod]
        [DataRow((string)null)]
        [DataRow("")]
        [DataRow("   ")]
        public void NullOrEmpty_IsRejected(string mVal)
        {
            AssertRejected(mVal);
        }

        [DataTestMethod]
        [DataRow("Bogus")]
        [DataRow("Perfmon+Bogus")]
        [DataRow("All-Bogus")]
        public void UnknownToken_IsRejected(string mVal)
        {
            AssertRejected(mVal);
        }

        [TestMethod]
        public void MixingPlusAndMinus_IsRejected()
        {
            AssertRejected("All-Perfmon+Linux");
        }

        [DataTestMethod]
        [DataRow("+Perfmon")]
        [DataRow("Perfmon+")]
        [DataRow("Perfmon++Linux")]
        public void AdditiveWithEmptyToken_IsRejected(string mVal)
        {
            AssertRejected(mVal);
        }

        [DataTestMethod]
        [DataRow("All--Perfmon")]
        [DataRow("All-Perfmon-")]
        public void SubtractiveWithEmptyToken_IsRejected(string mVal)
        {
            AssertRejected(mVal);
        }

        [DataTestMethod]
        [DataRow("Perfmon-Linux")] // subtractive must start with "All"
        [DataRow("ReadTrace-Perfmon")]
        public void Subtractive_NotStartingWithAll_IsRejected(string mVal)
        {
            AssertRejected(mVal);
        }
    }
}
