using Microsoft.VisualStudio.TestTools.UnitTesting;
using BulkLoadEx;

namespace SqlNexus.UnitTests.BulkLoadEx
{
    [TestClass]
    public class BulkLoadRowsetTests
    {
        [TestMethod]
        public void AppendContinuationNote_WhenSpaceAvailable_AppendsNote()
        {
            const string existing = "Message: base error";
            const string note = "[SqlNexus note: skipped continuation lines]";

            bool noteAppended;
            string result = BulkLoadRowset.AppendContinuationNote(existing, 4000, note, out noteAppended);

            Assert.IsTrue(noteAppended);
            Assert.IsTrue(result.Contains(note));
            Assert.IsTrue(result.StartsWith(existing));
        }

        [TestMethod]
        public void AppendContinuationNote_WhenNoteAlreadyPresent_DoesNotDuplicate()
        {
            const string note = "[SqlNexus note: skipped continuation lines]";
            string existing = "Message: base error " + note;

            bool noteAppended;
            string result = BulkLoadRowset.AppendContinuationNote(existing, 4000, note, out noteAppended);

            Assert.IsFalse(noteAppended);
            Assert.AreEqual(existing, result);
        }

        [TestMethod]
        public void AppendContinuationNote_WhenExceedsMaxLength_KeepsWithinLimit()
        {
            string existing = new string('X', 50);
            const string note = "[SqlNexus note: skipped continuation lines]";

            bool noteAppended;
            string result = BulkLoadRowset.AppendContinuationNote(existing, 60, note, out noteAppended);

            Assert.IsTrue(noteAppended);
            Assert.IsTrue(result.Length <= 60);
            Assert.IsTrue(result.Contains("See raw file") || result.Contains(note));
        }
    }
}
