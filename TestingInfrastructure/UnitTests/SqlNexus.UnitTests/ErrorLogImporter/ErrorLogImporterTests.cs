using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.ErrorLogImporter
{
    [TestClass]
    public class ErrorLogImporterTests
    {
        [TestMethod]
        public void IsHeadAndTailMarker_LogScoutMarkerWithWhitespace_ReturnsTrue()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "   <<... middle part of file not captured because the file is too large (>1 GB) ...>>");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_RegularErrorLogLine_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "2026-04-14 22:37:11.55 Server      SQL Server is starting.");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_NullLine_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsHeadAndTailMarker_AlteredMarker_ReturnsFalse()
        {
            bool result = global::ErrorLogImporter.ErrorLogImporter.IsHeadAndTailMarker(
                "<<... middle part of file not captured because the file is too large (>2 GB) ...>>");

            Assert.IsFalse(result);
        }
    }
}
