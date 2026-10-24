namespace EtsyMarketPlace.Application.Mcp;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public interface IEtsyOfficialMcpClient
{
    Task<string> CallOfficialToolAsync(
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken = default);
}

public interface IMcpServerService
{
    IReadOnlyList<McpToolDefinition> GetToolDefinitions();

    Task<McpToolCallResult> ExecuteToolAsync(
        McpToolCallRequest request,
        string defaultShopId,
        CancellationToken cancellationToken = default);

    Task<McpJsonRpcResponse> HandleJsonRpcAsync(
        McpJsonRpcRequest rpcRequest,
        string defaultShopId,
        CancellationToken cancellationToken = default);
}
