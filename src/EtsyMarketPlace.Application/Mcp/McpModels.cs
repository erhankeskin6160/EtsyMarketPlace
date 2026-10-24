namespace EtsyMarketPlace.Application.Mcp;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema);

public sealed record McpToolCallRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonElement? Arguments,
    [property: JsonPropertyName("shopId")] string? ShopId = null);

public sealed record McpContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public sealed record McpToolCallResult(
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("isError")] bool IsError,
    [property: JsonPropertyName("content")] IReadOnlyList<McpContentBlock> Content,
    [property: JsonPropertyName("structuredData")] object? StructuredData = null);

public sealed class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public sealed class McpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpJsonRpcError? Error { get; set; }
}

public sealed record McpJsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null);
