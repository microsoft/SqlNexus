using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraceEventImporter.Normalization;
using TraceEventImporter.Processing;

namespace SqlNexus.UnitTests.TraceEventImporter.Normalization
{
    [TestClass]
    public class SqlNormalizationTests
    {
        [TestMethod]
        public void Normalize_LiteralsCommentsAndParameters_ReplacesExpectedTokens()
        {
            const string sql = "select * -- comment\r\nfrom [Sales] where Name = N'O''Brien' and Id = 42 and Price = 1.25 and Payload = 0xCAFE and P = @P12 and Job = @job_id";

            string normalized = SqlTextNormalizer.Normalize(sql);

            Assert.AreEqual("SELECT * FROM [SALES] WHERE NAME = {STR} AND ID = {##} AND PRICE = {##}.{##} AND PAYLOAD = {BS} AND P = @P# AND JOB = @JOB_ID", normalized);
        }

        [TestMethod]
        public void Normalize_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SqlTextNormalizer.Normalize(null));
            Assert.AreEqual(string.Empty, SqlTextNormalizer.Normalize(string.Empty));
        }

        [TestMethod]
        public void TryExtractInnerSql_QualifiedWrapperWithEscapedQuote_ReturnsInnerSql()
        {
            const string rpc = "EXEC master.dbo.[sp_executesql] N'SELECT ''quoted'' FROM dbo.T WHERE Id = @P0', N'@P0 int', @P0=7";

            string innerSql = SpExecuteSqlExtractor.TryExtractInnerSql(rpc);

            Assert.AreEqual("SELECT 'quoted' FROM dbo.T WHERE Id = @P0", innerSql);
        }

        [TestMethod]
        public void TryExtractInnerSql_RawSqlWithStringLiteral_ReturnsNull()
        {
            Assert.IsNull(SpExecuteSqlExtractor.TryExtractInnerSql("UPDATE dbo.T SET Name = 'value'"));
        }

        [TestMethod]
        public void SpecialProcDetector_QualifiedName_IsDetectedCaseInsensitively()
        {
            byte id = SpecialProcDetector.GetSpecialProcId("MASTER.dbo.SP_EXECUTESQL");

            Assert.AreEqual((byte)3, id);
            Assert.IsTrue(SpExecuteSqlExtractor.ShouldExtractInnerSql(id));
        }

        [TestMethod]
        public void ComputeHash_SameInput_IsStableAndSpecialProcChangesSeed()
        {
            const string normalized = "SELECT * FROM DBO.T WHERE ID = {##}";

            long first = HashComputer.ComputeHash(normalized);
            long second = HashComputer.ComputeHash(normalized);
            long specialProcHash = HashComputer.ComputeHash(normalized, 3);

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, specialProcHash);
            Assert.AreEqual(0L, HashComputer.ComputeHash(null));
        }
    }
}