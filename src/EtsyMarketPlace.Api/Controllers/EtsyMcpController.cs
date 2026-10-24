namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using EtsyMarketPlace.Application.Mcp;

[ApiController]
[Route("api/etsy/mcp")]
public sealed class EtsyMcpController : BaseApiController
{
    private readonly IMcpServerService _mcpService;
    private readonly IConfiguration _config;

    public EtsyMcpController(
        IMcpServerService mcpService,
        IConfiguration config)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    [HttpPost]
    [EndpointSummary("Standart MCP (Model Context Protocol) JSON-RPC 2.0 Streamable HTTP Uç Noktası (initialize, tools/list, tools/call)")]
    public async Task<IActionResult> HandleJsonRpc(
        [FromBody] McpJsonRpcRequest request,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mcpService.HandleJsonRpcAsync(request, resolvedShopId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("tools")]
    [EndpointSummary("VDS Canlı Mağaza & Etsy MCP Araç Tanımlarını Listele")]
    public IActionResult GetTools()
    {
        var tools = _mcpService.GetToolDefinitions();
        return Ok(new
        {
            server = "EtsyMarketPlace-VDS-Live-MCP",
            version = "2.0.0",
            officialEtsyMcpBridge = "https://mcp.api.etsycloud.com/mcp",
            count = tools.Count,
            tools
        });
    }

    [HttpPost("call")]
    [EndpointSummary("Belirtilen VDS / Etsy MCP Aracını Canlı Çalıştır")]
    public async Task<IActionResult> CallTool(
        [FromBody] McpToolCallRequest request,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request?.ShopId ?? shopId, _config);
        var result = await _mcpService.ExecuteToolAsync(
            request ?? new McpToolCallRequest(string.Empty, null),
            resolvedShopId,
            cancellationToken);

        return Ok(result);
    }
}
