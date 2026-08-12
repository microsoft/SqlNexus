using System;
using System.Data.SqlTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RowsetImportEngine;

namespace SqlNexus.UnitTests.RowsetImportEngine
{
    /// <summary>
    /// Regression tests for RowsetImportEngine.DateTimeColumn.ValidateData.
    ///
    /// A datetime value can be valid in .NET (year 0001+) but outside the SQL Server 'datetime'
    /// range (1753-01-01 .. 9999-12-31). Such values previously passed validation and then threw
    /// "SqlDateTime overflow" during SqlBulkCopy, aborting the entire rowset flush. The guard in
    /// DateTimeColumn.ValidateData coerces out-of-range values to NULL so one bad row cannot abort
    /// the load. These tests lock that behavior in.
    /// </summary>
    [TestClass]
    public class DateTimeColumnTests
    {
        private static object Validate(object input)
        {
            // ValidateData returns the coerced value (and also stores it internally).
            return new DateTimeColumn().ValidateData(input);
        }

        [TestMethod]
        public void ValueBelowSqlMin_IsCoercedToNull()
        {
            // Valid .NET DateTime, invalid SQL 'datetime' (before 1753-01-01).
            var belowMin = new DateTime(1, 1, 1);
            Assert.IsNull(Validate(belowMin));
        }

        [TestMethod]
        public void ValueJustBelowSqlMin_IsCoercedToNull()
        {
            var justBelow = SqlDateTime.MinValue.Value.AddSeconds(-1);
            Assert.IsNull(Validate(justBelow));
        }

        [TestMethod]
        public void ValueAtSqlMin_IsPreserved()
        {
            var atMin = SqlDateTime.MinValue.Value; // 1753-01-01
            Assert.AreEqual(atMin, Validate(atMin));
        }

        [TestMethod]
        public void ValueInRange_IsPreserved()
        {
            var inRange = new DateTime(2024, 7, 12, 23, 26, 18);
            Assert.AreEqual(inRange, Validate(inRange));
        }

        [TestMethod]
        public void ValueAtSqlMax_IsPreserved()
        {
            var atMax = SqlDateTime.MaxValue.Value; // 9999-12-31 23:59:59.997

            // The guard under test only coerces OUT-OF-RANGE values to NULL. An in-range max value
            // must be preserved (not nulled). We assert it is a non-null DateTime on the same date;
            // exact time-of-day is not asserted because the underlying TypeConverter round-trips via
            // ToString() and drops sub-second precision (pre-existing behavior, unrelated to the guard).
            var result = Validate(atMax);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DateTime));
            Assert.AreEqual(atMax.Date, ((DateTime)result).Date);
        }

        [TestMethod]
        public void InRangeDateAsString_IsParsedAndPreserved()
        {
            // The importer feeds string data from text rowsets; ensure a valid in-range
            // string still converts to the expected DateTime.
            var result = Validate("2004-07-12 23:26:18");
            Assert.AreEqual(new DateTime(2004, 7, 12, 23, 26, 18), result);
        }

        [TestMethod]
        public void NullInput_ReturnsNull()
        {
            Assert.IsNull(Validate(null));
        }
    }
}
