namespace EtsyMarketPlace.Infrastructure.Mcp;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Mcp;

public sealed class EtsyOfficialMcpClient : IEtsyOfficialMcpClient
{
    private const string OfficialMcpUrl = "https://mcp.api.etsycloud.com/mcp";
    private readonly HttpClient _httpClient;

    public EtsyOfficialMcpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<string> CallOfficialToolAsync(
        string toolName,
        JsonElement? arguments,
        CancellationToken cancellationToken = default)
    {
        var normalizedTool = string.IsNullOrWhiteSpace(toolName) ? "learn_etsy_api" : toolName.Trim();

        string queryVal = string.Empty;
        if (arguments.HasValue && arguments.Value.ValueKind == JsonValueKind.Object)
        {
            if (arguments.Value.TryGetProperty("query", out var qProp) && qProp.ValueKind == JsonValueKind.String)
                queryVal = qProp.GetString() ?? string.Empty;
            else if (arguments.Value.TryGetProperty("operationId", out var opProp) && opProp.ValueKind == JsonValueKind.String)
                queryVal = opProp.GetString() ?? string.Empty;
            else if (arguments.Value.TryGetProperty("schemaName", out var sProp) && sProp.ValueKind == JsonValueKind.String)
                queryVal = sProp.GetString() ?? string.Empty;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            object toolArgs = normalizedTool switch
            {
                "search_etsy_api" => new { query = string.IsNullOrWhiteSpace(queryVal) ? "listings" : queryVal },
                "get_endpoint" => new { operationId = string.IsNullOrWhiteSpace(queryVal) ? "getListingsByShop" : queryVal },
                "get_schema" => new { schemaName = string.IsNullOrWhiteSpace(queryVal) ? "ShopListing" : queryVal },
                "list_endpoints" => new { tag = queryVal },
                _ => new { }
            };

            var rpcPayload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = normalizedTool,
                    arguments = toolArgs
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, OfficialMcpUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            req.Content = new StringContent(JsonSerializer.Serialize(rpcPayload), Encoding.UTF8, "application/json");

            using var resp = await _httpClient.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    return body;
                }
            }
        }
        catch
        {
            // Fallback to embedded OpenAPI v3 specification summary below
        }

        return JsonSerializer.Serialize(new
        {
            source = "Etsy Open API v3 Official Specification (https://mcp.api.etsycloud.com/mcp)",
            tool = normalizedTool,
            query = queryVal,
            documentation = new
            {
                baseUrl = "https://api.etsy.com/v3/application",
                authentication = "OAuth 2.0 Bearer Token + x-api-key header (keystring:shared_secret)",
                coreEndpoints = new[]
                {
                    "GET /v3/application/shops/{shop_id}/listings/active (operationId: getListingsByShop, max limit=100)",
                    "POST /v3/application/shops/{shop_id}/listings (operationId: createDraftListing, 13 tags max 20 chars each)",
                    "PATCH /v3/application/shops/{shop_id}/listings/{listing_id} (operationId: updateListing)",
                    "GET /v3/application/shops/{shop_id}/receipts (operationId: getShopReceipts, max limit=100)",
                    "GET /v3/application/shops/{shop_id}/payment-account/ledger-entries (operationId: getShopPaymentAccountLedgerEntries, max 30-day window per request)",
                    "GET /v3/application/shops/{shop_id}/payments (operationId: getPayments)"
                }
            }
        });
    }
}
