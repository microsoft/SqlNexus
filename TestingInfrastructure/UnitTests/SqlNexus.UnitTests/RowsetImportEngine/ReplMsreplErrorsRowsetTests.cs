using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RowsetImportEngine;

namespace SqlNexus.UnitTests.RowsetImportEngine
{
    [TestClass]
    public class ReplMsreplErrorsRowsetTests
    {
        [TestMethod]
        public void ShouldInsertCurrentRow_WithIdAndTime_ReturnsTrue()
        {
            var rowset = new ReplMsreplErrorsRowset();

            var id = new IntColumn { Name = "id" };
            id.Data = 2737510;

            var time = new DateTimeColumn { Name = "time" };
            time.Data = new DateTime(2026, 8, 16, 5, 0, 4);

            rowset.Columns.Add(id);
            rowset.Columns.Add(time);

            Assert.IsTrue(rowset.ShouldInsertCurrentRow());
        }

        [TestMethod]
        public void ShouldInsertCurrentRow_WithoutIdAndTime_ReturnsFalse()
        {
            var rowset = new ReplMsreplErrorsRowset();

            var id = new IntColumn { Name = "id" };
            id.Data = null;

            var time = new DateTimeColumn { Name = "time" };
            time.Data = null;

            rowset.Columns.Add(id);
            rowset.Columns.Add(time);

            Assert.IsFalse(rowset.ShouldInsertCurrentRow());
        }

        [TestMethod]
        public void ShouldInsertCurrentRow_WithIdOnly_ReturnsFalse()
        {
            var rowset = new ReplMsreplErrorsRowset();

            var id = new IntColumn { Name = "id" };
            id.Data = 2737510;

            var time = new DateTimeColumn { Name = "time" };
            time.Data = null;

            rowset.Columns.Add(id);
            rowset.Columns.Add(time);

            Assert.IsFalse(rowset.ShouldInsertCurrentRow());
        }
    }
}
