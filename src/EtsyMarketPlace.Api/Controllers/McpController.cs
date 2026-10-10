using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly McpToolHandler _handler;

    public McpController(McpToolHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("")]
    [EndpointSummary("MCP Sunucu Bilgisi ve Protokol Durumu")]
    public IActionResult GetMcpInfo()
    {
        return Ok(new
        {
            name = "EtsyMarketPlace-MCP-Server",
            version = "1.0.0",
            protocolVersion = "2024-11-05",
            status = "Active",
            supportedMethods = new[] { "initialize", "tools/list", "tools/call" },
            toolsCount = _handler.GetRegisteredTools().Count
        });
    }

    [HttpPost("")]
    [EndpointSummary("Gemini SparkX JSON-RPC 2.0 İletişim Hattı")]
    public async Task<IActionResult> PostMcpJsonRpc()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(McpResponse.Fail(null, -32700, "Geçersiz JSON"));

        McpRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<McpRequest>(body);
        }
        catch (Exception ex)
        {
            return BadRequest(McpResponse.Fail(null, -32700, $"Parse Hatası: {ex.Message}"));
        }

        if (request == null || request.JsonRpc != "2.0" || string.IsNullOrWhiteSpace(request.Method))
            return BadRequest(McpResponse.Fail(null, -32600, "Geçersiz İstek"));

        switch (request.Method)
        {
            case "initialize":
                return Ok(McpResponse.Success(request.Id, new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { listChanged = false }
                    },
                    serverInfo = new
                    {
                        name = "EtsyMarketPlace-SparkX-MCP",
                        version = "1.0.0"
                    }
                }));

            case "notifications/initialized":
                return NoContent();

            case "tools/list":
                return Ok(McpResponse.Success(request.Id, new
                {
                    tools = _handler.GetRegisteredTools()
                }));

            case "tools/call":
                if (!request.Params.HasValue)
                    return Ok(McpResponse.Fail(request.Id, -32602, "Eksik Parametre"));

                var p = request.Params.Value;
                string? toolName = p.TryGetProperty("name", out var tProp) ? tProp.GetString() : null;
                JsonElement? arguments = p.TryGetProperty("arguments", out var aProp) ? aProp : null;

                if (string.IsNullOrWhiteSpace(toolName))
                    return Ok(McpResponse.Fail(request.Id, -32602, "Araç adı ('name') belirtilmeli"));

                try
                {
                    var toolResult = await _handler.ExecuteToolAsync(toolName, arguments);
                    return Ok(McpResponse.Success(request.Id, toolResult));
                }
                catch (Exception ex)
                {
                    return Ok(McpResponse.Fail(request.Id, -32000, $"Araç çalıştırma hatası: {ex.Message}"));
                }

            default:
                return Ok(McpResponse.Fail(request.Id, -32601, $"Bilinmeyen metot: {request.Method}"));
        }
    }
}
