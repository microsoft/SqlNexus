using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void GetRequiredInt64Argument_MissingValue_ThrowsArgumentException()
        {
            var arguments = new JObject();

            Assert.ThrowsException<ArgumentException>(() =>
                global::SqlNexus.McpServer.Program.GetRequiredInt64Argument(arguments, "hash_id"));
        }

        [TestMethod]
        public void GetRequiredInt64Argument_NullValue_ThrowsArgumentException()
        {
            var arguments = new JObject
            {
                ["hash_id"] = JValue.CreateNull()
            };

            Assert.ThrowsException<ArgumentException>(() =>
                global::SqlNexus.McpServer.Program.GetRequiredInt64Argument(arguments, "hash_id"));
        }

        [TestMethod]
        public void GetRequiredInt64Argument_ValidValue_ReturnsLong()
        {
            var arguments = new JObject
            {
                ["hash_id"] = 42
            };

            long value = global::SqlNexus.McpServer.Program.GetRequiredInt64Argument(arguments, "hash_id");

            Assert.AreEqual(42L, value);
        }
    }
}
