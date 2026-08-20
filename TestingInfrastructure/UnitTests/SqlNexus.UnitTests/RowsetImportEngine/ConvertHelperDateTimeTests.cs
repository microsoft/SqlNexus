using System;
using System.Data.SqlTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RowsetImportEngine.Helpers;

namespace SqlNexus.UnitTests.RowsetImportEngine
{
    /// <summary>
    /// Regression tests for ConvertHelper.CoerceToSqlDateTimeRange, the final safety net used by
    /// TextRowsetImporter.InsertRow before values are handed to SqlBulkCopy.
    ///
    /// Background: a value that is a valid .NET DateTime (year 0001+) but outside the SQL Server
    /// 'datetime' range (1753-01-01 .. 9999-12-31) previously flowed all the way to
    /// SqlBulkCopy.WriteToServer and threw "SqlDateTime overflow", aborting the entire rowset load
    /// (observed with the first-seen '-- repl_mssnapshot_history --' rowset). This guard coerces
    /// such values to NULL so a single bad source row cannot fail the whole bulk load.
    /// </summary>
    [TestClass]
    public class ConvertHelperDateTimeTests
    {
        [TestMethod]
        public void DateTimeBelowSqlMin_IsCoercedToNull()
        {
            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(new DateTime(1, 1, 1), out coerced);

            Assert.IsTrue(wasCoerced);
            Assert.IsNull(coerced);
        }

        [TestMethod]
        public void DateTimeJustBelowSqlMin_IsCoercedToNull()
        {
            var justBelow = SqlDateTime.MinValue.Value.AddSeconds(-1);

            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(justBelow, out coerced);

            Assert.IsTrue(wasCoerced);
            Assert.IsNull(coerced);
        }

        [TestMethod]
        public void DateTimeAtSqlMin_IsPreserved()
        {
            var atMin = SqlDateTime.MinValue.Value; // 1753-01-01

            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(atMin, out coerced);

            Assert.IsFalse(wasCoerced);
            Assert.AreEqual(atMin, coerced);
        }

        [TestMethod]
        public void DateTimeInRange_IsPreserved()
        {
            var inRange = new DateTime(2026, 8, 16, 10, 18, 3);

            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(inRange, out coerced);

            Assert.IsFalse(wasCoerced);
            Assert.AreEqual(inRange, coerced);
        }

        [TestMethod]
        public void DateTimeAtSqlMax_IsPreserved()
        {
            var atMax = SqlDateTime.MaxValue.Value; // 9999-12-31

            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(atMax, out coerced);

            Assert.IsFalse(wasCoerced);
            Assert.AreEqual(atMax, coerced);
        }

        [TestMethod]
        public void NonDateTimeValue_IsPreservedUnchanged()
        {
            // Non-datetime values must never be coerced by this guard.
            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange("some varchar", out coerced);

            Assert.IsFalse(wasCoerced);
            Assert.AreEqual("some varchar", coerced);
        }

        [TestMethod]
        public void NullValue_IsPreservedAsNull()
        {
            object coerced;
            bool wasCoerced = ConvertHelper.CoerceToSqlDateTimeRange(null, out coerced);

            Assert.IsFalse(wasCoerced);
            Assert.IsNull(coerced);
        }
    }
}
