using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SqlNexus.McpServer
{
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object? Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; } = string.Empty;

        [JsonProperty("params")]
        public Dictionary<string, object>? Params { get; set; }
    }

    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object? Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object? Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError? Error { get; set; }
    }

    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("data")]
        public object? Data { get; set; }
    }

    public class McpTool
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("inputSchema")]
        public object InputSchema { get; set; } = new { };
    }

    public class McpToolResult
    {
        [JsonProperty("content")]
        public List<McpContent> Content { get; set; } = new();
    }

    public class McpContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class ServerInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class InitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonProperty("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new();

        [JsonProperty("serverInfo")]
        public ServerInfo ServerInfo { get; set; } = new();
    }

    public class ServerCapabilities
    {
        [JsonProperty("tools")]
        public Dictionary<string, object> Tools { get; set; } = new();
    }
}
