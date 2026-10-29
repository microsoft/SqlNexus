using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusInterfaces;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SqlNexus.UnitTests.NexusInterfaces
{
    [TestClass]
    public class CSqlTests
    {
        [DataTestMethod]
        [DataRow("\r\n")]
        [DataRow("\n")]
        public void ParseBatches_ConsecutiveSeparators_RemovesEverySeparator(string newline)
        {
            string script = "SELECT 1;" + newline + "GO" + newline + "GO" + newline + "SELECT 2;";

            string[] batches = NonEmptyBatches(script);

            CollectionAssert.AreEqual(new[] { "SELECT 1;", "SELECT 2;" }, batches);
        }

        [DataTestMethod]
        [DataRow("\r\n")]
        [DataRow("\n")]
        public void ParseBatches_LeadingTrailingAndIndentedSeparators_ReturnsOnlySql(string newline)
        {
            string script = "GO" + newline + "\tgo  " + newline + "SELECT 1;" + newline + " Go\t";

            CollectionAssert.AreEqual(new[] { "SELECT 1;" }, NonEmptyBatches(script));
        }

        [TestMethod]
        public void ParseBatches_NoSeparators_PreservesScript()
        {
            string script = "SELECT 'GO', [GO], 'cargo';\r\n-- GO is a client command\r\nSELECT 2;";

            CollectionAssert.AreEqual(new[] { script }, NonEmptyBatches(script));
        }

        [TestMethod]
        public void ParseBatches_GoWithOtherTextInsideString_DoesNotSplit()
        {
            string script = "SELECT '\r\n  go shopping\r\n';";

            CollectionAssert.AreEqual(new[] { script }, NonEmptyBatches(script));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" \r\n\t")]
        [DataRow("GO\r\nGO\r\n")]
        public void ParseBatches_EmptyOrSeparatorOnlyScript_HasNoExecutableBatches(string script)
        {
            Assert.AreEqual(0, NonEmptyBatches(script).Length);
        }

        [TestMethod]
        public void ParseBatches_NullScript_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => CSql.ParseBatches(null));
        }

        [TestMethod]
        public void ParseBatches_PostProcessingScript_LeavesNoGoCommandsInBatches()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLNexus_PostProcessing.sql");
            string script = File.ReadAllText(path);
            int separatorCount = Regex.Matches(script, @"^[\t ]*GO[\t ]*\r?$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase).Count;

            string[] batches = CSql.ParseBatches(script);

            Assert.AreEqual(separatorCount + 1, batches.Length);
            foreach (string batch in batches)
                Assert.IsFalse(Regex.IsMatch(batch, @"^[\t ]*GO[\t ]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase));
            Assert.IsTrue(batches.Any(batch => batch.TrimStart().StartsWith("BEGIN TRAN;", StringComparison.Ordinal)));
        }

        private static string[] NonEmptyBatches(string script)
        {
            return CSql.ParseBatches(script)
                .Select(batch => batch.Trim())
                .Where(batch => batch.Length > 0)
                .ToArray();
        }
    }
}
