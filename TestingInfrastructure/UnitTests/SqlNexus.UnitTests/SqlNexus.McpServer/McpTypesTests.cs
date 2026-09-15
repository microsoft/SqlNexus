using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlNexus.McpServer;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    /// <summary>
    /// Tests for the MCP JSON-RPC data contracts in <c>McpTypes.cs</c>. These verify the on-the-wire
    /// JSON shape (property names, defaults, and null-omission behaviour) that MCP hosts depend on.
    /// All tests are pure serialization checks with no I/O.
    /// </summary>
    [TestClass]
    public class McpTypesTests
    {
        [TestMethod]
        public void JsonRpcRequest_DefaultVersion_Is20()
        {
            var request = new JsonRpcRequest();
            Assert.AreEqual("2.0", request.JsonRpc);
        }

        [TestMethod]
        public void JsonRpcRequest_Deserializes_MethodAndParams()
        {
            const string json = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"analyze\"}}";
            var request = JsonConvert.DeserializeObject<JsonRpcRequest>(json);

            Assert.AreEqual("tools/call", request.Method);
            Assert.IsNotNull(request.Params);
            Assert.IsTrue(request.Params.ContainsKey("name"));
        }

        [TestMethod]
        public void JsonRpcResponse_NullResultAndError_AreOmitted()
        {
            var response = new JsonRpcResponse { Id = 1 };
            string json = JsonConvert.SerializeObject(response);

            Assert.IsFalse(json.Contains("\"result\""), "Null result must be omitted from the wire format.");
            Assert.IsFalse(json.Contains("\"error\""), "Null error must be omitted from the wire format.");
            StringAssert.Contains(json, "\"jsonrpc\":\"2.0\"");
        }

        [TestMethod]
        public void JsonRpcResponse_WithError_SerializesErrorObject()
        {
            var response = new JsonRpcResponse
            {
                Id = 1,
                Error = new JsonRpcError { Code = -32601, Message = "Method not found" }
            };

            var parsed = JObject.Parse(JsonConvert.SerializeObject(response));
            Assert.AreEqual(-32601, (int)parsed["error"]["code"]);
            Assert.AreEqual("Method not found", (string)parsed["error"]["message"]);
        }

        [TestMethod]
        public void McpTool_UsesLowerCamelCasePropertyNames()
        {
            var tool = new McpTool { Name = "get_top_queries", Description = "Top queries" };
            var parsed = JObject.Parse(JsonConvert.SerializeObject(tool));

            Assert.IsNotNull(parsed["name"]);
            Assert.IsNotNull(parsed["description"]);
            Assert.IsNotNull(parsed["inputSchema"]);
        }

        [TestMethod]
        public void McpToolResult_DefaultContent_IsEmptyNotNull()
        {
            var result = new McpToolResult();
            Assert.IsNotNull(result.Content);
            Assert.AreEqual(0, result.Content.Count);
        }

        [TestMethod]
        public void McpContent_DefaultType_IsText()
        {
            var content = new McpContent { Text = "hello" };
            Assert.AreEqual("text", content.Type);
        }

        [TestMethod]
        public void InitializeResult_DefaultProtocolVersion_IsSet()
        {
            var init = new InitializeResult();
            Assert.AreEqual("2024-11-05", init.ProtocolVersion);
            Assert.IsNotNull(init.Capabilities);
            Assert.IsNotNull(init.ServerInfo);
        }

        [TestMethod]
        public void InitializeResult_NullInstructions_AreOmitted()
        {
            var init = new InitializeResult { Instructions = null };
            string json = JsonConvert.SerializeObject(init);
            Assert.IsFalse(json.Contains("\"instructions\""));
        }

        [TestMethod]
        public void InitializeResult_WithInstructions_AreSerialized()
        {
            var init = new InitializeResult { Instructions = "Responses are AI-assisted." };
            var parsed = JObject.Parse(JsonConvert.SerializeObject(init));
            Assert.AreEqual("Responses are AI-assisted.", (string)parsed["instructions"]);
        }

        [TestMethod]
        public void ServerCapabilities_Tools_DefaultsToEmptyDictionary()
        {
            var caps = new ServerCapabilities();
            Assert.IsNotNull(caps.Tools);
            Assert.IsInstanceOfType(caps.Tools, typeof(Dictionary<string, object>));
        }
    }
}
