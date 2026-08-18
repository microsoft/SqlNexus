using Microsoft.VisualStudio.TestTools.UnitTesting;
using RowsetImportEngine.Helpers;

namespace SqlNexus.UnitTests.RowsetImportEngine
{
    [TestClass]
    public class ConvertHelperStringTests
    {
        [TestMethod]
        public void NullInput_ReturnsNullWithoutTruncation()
        {
            bool wasTruncated;
            string result = ConvertHelper.NormalizeStringForSql(null, 10, out wasTruncated);

            Assert.IsNull(result);
            Assert.IsFalse(wasTruncated);
        }

        [TestMethod]
        public void InRangeString_IsTrimmedAndNotTruncated()
        {
            bool wasTruncated;
            string result = ConvertHelper.NormalizeStringForSql("  abc  ", 10, out wasTruncated);

            Assert.AreEqual("abc", result);
            Assert.IsFalse(wasTruncated);
        }

        [TestMethod]
        public void OverMaxLengthString_IsTruncated()
        {
            bool wasTruncated;
            string result = ConvertHelper.NormalizeStringForSql("  abcdefghij  ", 5, out wasTruncated);

            Assert.AreEqual("abcde", result);
            Assert.IsTrue(wasTruncated);
        }

        [TestMethod]
        public void MaxLengthMinusOne_DoesNotTruncate()
        {
            bool wasTruncated;
            string result = ConvertHelper.NormalizeStringForSql("  abcdefghij  ", -1, out wasTruncated);

            Assert.AreEqual("abcdefghij", result);
            Assert.IsFalse(wasTruncated);
        }

        [TestMethod]
        public void MaxLengthZero_DoesNotTruncate()
        {
            bool wasTruncated;
            string result = ConvertHelper.NormalizeStringForSql("  abcdefghij  ", 0, out wasTruncated);

            Assert.AreEqual("abcdefghij", result);
            Assert.IsFalse(wasTruncated);
        }
    }
}
