namespace EtsyMarketPlace.Application.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;
using EtsyMarketPlace.Application.Features.Listings.Queries.GetShopListings;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;
using EtsyMarketPlace.Application.Features.MarketResearch;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetFinancialPerformance;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Shipping;

public sealed class McpServerService : IMcpServerService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IMediator _mediator;
    private readonly IEtsyDashboardService _dashboardService;
    private readonly IEtsyIntegrationRepository? _integrationRepository;
    private readonly IEtsyOfficialMcpClient _officialMcpClient;

    public McpServerService(
        IMediator mediator,
        IEtsyDashboardService dashboardService,
        IEtsyIntegrationRepository? integrationRepository,
        IEtsyOfficialMcpClient officialMcpClient)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _integrationRepository = integrationRepository;
        _officialMcpClient = officialMcpClient ?? throw new ArgumentNullException(nameof(officialMcpClient));
    }

    public IReadOnlyList<McpToolDefinition> GetToolDefinitions()
    {
        return new List<McpToolDefinition>
        {
            new(
                "get_store_financial_summary",
                "VDS Sunucu Finans Motorundan (canlı Etsy ödeme defteri) mağazanın seçilen dönemdeki brüt satış, komisyon, Etsy Ads, Offsite Ads, iade, ürün/kargo maliyeti, gerçek net kâr ve banka yatırımı (payout) özetini getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        period = new { type = "string", description = "Rapor dönemi: 'today', 'this_month', 'last_month', 'this_year', 'all_time'", @enum = new[] { "today", "this_month", "last_month", "this_year", "all_time" } },
                        startDate = new { type = "string", description = "Opsiyonel başlangıç tarihi (ISO 8601, örn: 2026-10-01)" },
                        endDate = new { type = "string", description = "Opsiyonel bitiş tarihi (ISO 8601, örn: 2026-10-25)" }
                    }
                }),
            new(
                "get_store_orders",
                "Mağazanın canlı siparişlerini, alıcı adlarını, satılan ürünleri, Etsy kesintilerini, kargo/üretim maliyetlerini (hasCostData) ve sipariş bazlı net kâr dökümünü getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        period = new { type = "string", description = "Dönem: 'this_month', 'last_month', 'this_year', 'all_time'", @enum = new[] { "this_month", "last_month", "this_year", "all_time" } },
                        statusFilter = new { type = "string", description = "Durum filtresi: 'all', 'completed', 'canceled', 'missing_cost'", @enum = new[] { "all", "completed", "canceled", "missing_cost" } },
                        limit = new { type = "number", description = "Maksimum sipariş sayısı (varsayılan: 50)" }
                    }
                }),
            new(
                "get_bank_payouts",
                "Etsy ödeme hesabından Türkiye'deki banka hesabına yapılan gerçek para transferlerini (disburse2), tarihlerini, referans numaralarını ve USD/TL tutarlarını getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        period = new { type = "string", description = "Dönem: 'this_month', 'last_month', 'this_year', 'all_time'", @enum = new[] { "this_month", "last_month", "this_year", "all_time" } }
                    }
                }),
            new(
                "save_order_cost",
                "Belirtilen Etsy siparişinin (receiptId) kargo, üretim/hammadde ve paketleme maliyetini VDS veritabanına kaydeder ve gerçek net kârı günceller.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        receiptId = new { type = "string", description = "Etsy sipariş/dekont numarası (örn: 4204665088)" },
                        shippingCostUsd = new { type = "number", description = "Kargo maliyeti (USD)" },
                        productCostUsd = new { type = "number", description = "Ürün üretim/hammadde maliyeti (USD)" },
                        packagingCostUsd = new { type = "number", description = "Paketleme maliyeti (USD)" },
                        notes = new { type = "string", description = "Kargo firması / takip kodu notu" }
                    },
                    required = new[] { "receiptId", "shippingCostUsd" }
                }),
            new(
                "get_shop_active_listings",
                "Mağazanın tüm aktif Etsy ilanlarını (50+ ilan), fiyatlarını, stoklarını, görüntülenme (views), favori (favorites) sayılarını, 13 etiketini, SEO puanlarını ve telif risk uyarılarını canlı Etsy v3 API'den getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        limit = new { type = "number", description = "Çekilecek maksimum ilan sayısı (varsayılan: 100)" },
                        sortBy = new { type = "string", description = "Sıralama ölçütü: 'views', 'favorites', 'price_desc', 'price_asc', 'seo_asc'", @enum = new[] { "views", "favorites", "price_desc", "price_asc", "seo_asc" } }
                    }
                }),
            new(
                "create_etsy_draft_listing",
                "Canlı Etsy v3 API üzerinden mağazaya doğrudan yeni bir TASLAK (draft) ilan açar.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Etsy SEO uyumlu ürün başlığı (maks 140 karakter)" },
                        priceUsd = new { type = "number", description = "Dolar satış fiyatı" },
                        tags = new { type = "array", items = new { type = "string" }, description = "Her biri en fazla 20 karakter olan 13 adet SEO etiketi" },
                        description = new { type = "string", description = "Detaylı ürün açıklaması" },
                        materials = new { type = "array", items = new { type = "string" }, description = "Kullanılan malzemeler" },
                        quantity = new { type = "number", description = "Stok adedi (varsayılan: 15)" },
                        taxonomyId = new { type = "number", description = "Etsy kategori ID (varsayılan: 1042)" }
                    },
                    required = new[] { "title", "priceUsd", "tags", "description" }
                }),
            new(
                "update_etsy_listing",
                "Mağazadaki mevcut bir Etsy ilanının (listingId) başlığını, açıklamasını ve 13 etiketini doğrudan canlı Etsy API üzerinden günceller.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        listingId = new { type = "string", description = "Güncellenecek Etsy ilan numarası" },
                        title = new { type = "string", description = "Yeni başlık (maks 140 karakter)" },
                        description = new { type = "string", description = "Yeni açıklama" },
                        tags = new { type = "array", items = new { type = "string" }, description = "13 adet SEO etiketi" },
                        materials = new { type = "array", items = new { type = "string" }, description = "Malzemeler" }
                    },
                    required = new[] { "listingId", "title" }
                }),
            new(
                "search_market_intelligence",
                "Etsy v3 API üzerinden canlı pazar araştırması yapar; rakip ilanları, ortalama/medyan fiyatları, favori sayılarını ve pazar fırsat skorunu getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        keyword = new { type = "string", description = "Aranacak ürün anahtar kelimesi" },
                        limit = new { type = "number", description = "İncelenecek rakip ilan sayısı (varsayılan: 12)" }
                    },
                    required = new[] { "keyword" }
                }),
            new(
                "calculate_shipping_rates",
                "VDS Taşıyıcı Kargo Motorundan (Navlungo, ShipEntegra, Aras Global) belirtilen ağırlık, ebat ve ülke için canlı/sözleşmeli kargo fiyat tekliflerini hesaplar.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        countryCode = new { type = "string", description = "Hedef ülke ISO kodu (varsayılan: 'US')" },
                        weightKg = new { type = "number", description = "Paket ağırlığı kg (örn: 0.4)" },
                        widthCm = new { type = "number", description = "En cm (varsayılan: 15)" },
                        lengthCm = new { type = "number", description = "Boy cm (varsayılan: 20)" },
                        heightCm = new { type = "number", description = "Yükseklik cm (varsayılan: 10)" }
                    },
                    required = new[] { "weightKg" }
                }),
            new(
                "query_etsy_official_dev_mcp",
                "Etsy'nin resmi Geliştirici MCP sunucusunu (https://mcp.api.etsycloud.com/mcp) sorgulayarak Etsy Open API v3 şemalarını, endpoint tanımlarını ve teknik kurallarını getirir.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        officialToolName = new { type = "string", description = "Çağrılacak resmi Etsy MCP aracı: 'learn_etsy_api', 'search_etsy_api', 'list_endpoints', 'get_endpoint', 'get_schema'", @enum = new[] { "learn_etsy_api", "search_etsy_api", "list_endpoints", "get_endpoint", "get_schema" } },
                        query = new { type = "string", description = "Aranacak kelime, operationId veya şema adı (örn: 'ShopReceipt', 'getShopListing')" }
                    },
                    required = new[] { "officialToolName" }
                })
        };
    }

    public async Task<McpToolCallResult> ExecuteToolAsync(
        McpToolCallRequest request,
        string defaultShopId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return CreateErrorResult("unknown", "Araç adı (name) belirtilmelidir.");
        }

        var shopId = !string.IsNullOrWhiteSpace(request.ShopId) ? request.ShopId.Trim() : defaultShopId;
        var args = request.Arguments;

        try
        {
            switch (request.Name.Trim())
            {
                case "get_store_financial_summary":
                    return await HandleFinancialSummaryAsync(shopId, args, cancellationToken);

                case "get_store_orders":
                    return await HandleStoreOrdersAsync(shopId, args, cancellationToken);

                case "get_bank_payouts":
                    return await HandleBankPayoutsAsync(shopId, args, cancellationToken);

                case "save_order_cost":
                    return await HandleSaveOrderCostAsync(shopId, args, cancellationToken);

                case "get_shop_active_listings":
                    return await HandleShopActiveListingsAsync(shopId, args, cancellationToken);

                case "create_etsy_draft_listing":
                    return await HandleCreateDraftListingAsync(shopId, args, cancellationToken);

                case "update_etsy_listing":
                    return await HandleUpdateListingAsync(shopId, args, cancellationToken);

                case "search_market_intelligence":
                    return await HandleMarketSearchAsync(shopId, args, cancellationToken);

                case "calculate_shipping_rates":
                    return await HandleShippingRatesAsync(args, cancellationToken);

                case "query_etsy_official_dev_mcp":
                    return await HandleOfficialEtsyMcpAsync(args, cancellationToken);

                default:
                    return CreateErrorResult(request.Name, $"Tanınmayan MCP aracı: {request.Name}");
            }
        }
        catch (Exception ex)
        {
            return CreateErrorResult(request.Name, $"MCP araç çalıştırma hatası ({request.Name}): {ex.Message}");
        }
    }

    public async Task<McpJsonRpcResponse> HandleJsonRpcAsync(
        McpJsonRpcRequest rpcRequest,
        string defaultShopId,
        CancellationToken cancellationToken = default)
    {
        if (rpcRequest == null || string.IsNullOrWhiteSpace(rpcRequest.Method))
        {
            return new McpJsonRpcResponse
            {
                Id = rpcRequest?.Id,
                Error = new McpJsonRpcError(-32600, "Geçersiz JSON-RPC 2.0 isteği.")
            };
        }

        var method = rpcRequest.Method.Trim();

        switch (method)
        {
            case "initialize":
                return new McpJsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { listChanged = false }
                        },
                        serverInfo = new
                        {
                            name = "EtsyMarketPlace-VDS-Live-MCP",
                            version = "2.0.0"
                        }
                    }
                };

            case "notifications/initialized":
            case "ping":
                return new McpJsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new { status = "ok" }
                };

            case "tools/list":
                return new McpJsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new
                    {
                        tools = GetToolDefinitions()
                    }
                };

            case "tools/call":
                string toolName = string.Empty;
                JsonElement? toolArgs = null;
                string? shopId = null;

                if (rpcRequest.Params.HasValue && rpcRequest.Params.Value.ValueKind == JsonValueKind.Object)
                {
                    var p = rpcRequest.Params.Value;
                    if (p.TryGetProperty("name", out var nProp))
                        toolName = nProp.GetString() ?? string.Empty;
                    if (p.TryGetProperty("arguments", out var aProp))
                        toolArgs = aProp;
                    if (p.TryGetProperty("shopId", out var sProp))
                        shopId = sProp.GetString();
                }

                var callResult = await ExecuteToolAsync(new McpToolCallRequest(toolName, toolArgs, shopId), defaultShopId, cancellationToken);
                return new McpJsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new
                    {
                        content = callResult.Content,
                        isError = callResult.IsError,
                        structuredData = callResult.StructuredData
                    }
                };

            default:
                return new McpJsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Error = new McpJsonRpcError(-32601, $"Desteklenmeyen MCP metodu: {method}")
                };
        }
    }

    private async Task<McpToolCallResult> HandleFinancialSummaryAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var period = GetStringArg(args, "period", "this_month");
        var startDate = GetDateArg(args, "startDate");
        var endDate = GetDateArg(args, "endDate");

        var req = new GetFinancialPerformanceRequest
        {
            ShopId = shopId,
            Period = period,
            StartDate = startDate,
            EndDate = endDate
        };

        var response = await _mediator.Send(req, cancellationToken);
        if (!response.Success)
            return CreateErrorResult("get_store_financial_summary", response.Message ?? "Finansal performans alınamadı.");

        return CreateSuccessResult("get_store_financial_summary", response.Data);
    }

    private async Task<McpToolCallResult> HandleStoreOrdersAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var period = GetStringArg(args, "period", "this_month");
        var statusFilter = GetStringArg(args, "statusFilter", "all").ToLowerInvariant();
        var limit = GetIntArg(args, "limit", 50);

        var (start, end) = ResolvePeriodDates(period);
        var result = await _dashboardService.GetOrderFinancialsAsync(shopId, period, start, end, cancellationToken);

        var orders = result.Orders ?? Array.Empty<EtsyOrderFinancialDetail>();
        IEnumerable<EtsyOrderFinancialDetail> filtered = orders;

        if (statusFilter == "completed")
            filtered = orders.Where(o => string.Equals(o.OrderStatus, "completed", StringComparison.OrdinalIgnoreCase));
        else if (statusFilter == "canceled" || statusFilter == "cancelled")
            filtered = orders.Where(o => string.Equals(o.OrderStatus, "canceled", StringComparison.OrdinalIgnoreCase) || string.Equals(o.OrderStatus, "cancelled", StringComparison.OrdinalIgnoreCase));
        else if (statusFilter == "missing_cost")
            filtered = orders.Where(o => !o.HasCostData && !string.Equals(o.OrderStatus, "canceled", StringComparison.OrdinalIgnoreCase));

        var list = filtered.Take(Math.Clamp(limit, 1, 250)).ToList();
        var missingCostTotal = orders.Count(o => !o.HasCostData && !string.Equals(o.OrderStatus, "canceled", StringComparison.OrdinalIgnoreCase));

        var payload = new
        {
            period,
            statusFilter,
            totalOrdersInPeriod = orders.Count,
            missingCostOrdersInPeriod = missingCostTotal,
            returnedCount = list.Count,
            totalOrderProfitUsd = result.TotalOrderProfitUsd,
            totalOrderProfitTry = result.TotalOrderProfitTry,
            exchangeRateUsed = result.ExchangeRateUsed,
            orders = list
        };

        return CreateSuccessResult("get_store_orders", payload);
    }

    private async Task<McpToolCallResult> HandleBankPayoutsAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var period = GetStringArg(args, "period", "this_month");
        var (start, end) = ResolvePeriodDates(period);

        var req = new GetBankPayoutsRequest
        {
            ShopId = shopId,
            StartDate = start,
            EndDate = end
        };

        var response = await _mediator.Send(req, cancellationToken);
        if (!response.Success)
            return CreateErrorResult("get_bank_payouts", response.Message ?? "Banka yatırımları alınamadı.");

        return CreateSuccessResult("get_bank_payouts", response.Data);
    }

    private async Task<McpToolCallResult> HandleSaveOrderCostAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var receiptId = GetStringArg(args, "receiptId", string.Empty);
        if (string.IsNullOrWhiteSpace(receiptId))
            return CreateErrorResult("save_order_cost", "receiptId zorunludur.");

        var shippingCost = GetDecimalArg(args, "shippingCostUsd", 0m);
        var productCost = GetDecimalArg(args, "productCostUsd", 0m);
        var packagingCost = GetDecimalArg(args, "packagingCostUsd", 0m);
        var notes = GetStringArg(args, "notes", "AI MCP Asistanı ile güncellendi");

        if (_integrationRepository != null)
        {
            await _integrationRepository.UpsertOrderCostAsync(
                shopId,
                receiptId.Trim(),
                productCost,
                shippingCost,
                packagingCost,
                notes,
                cancellationToken);
        }

        var payload = new
        {
            success = true,
            shopId,
            receiptId = receiptId.Trim(),
            shippingCostUsd = shippingCost,
            productCostUsd = productCost,
            packagingCostUsd = packagingCost,
            totalCostUsd = shippingCost + productCost + packagingCost,
            notes
        };

        return CreateSuccessResult("save_order_cost", payload);
    }

    private async Task<McpToolCallResult> HandleShopActiveListingsAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(GetIntArg(args, "limit", 100), 1, 100);
        var sortBy = GetStringArg(args, "sortBy", "views").ToLowerInvariant();

        var response = await _mediator.Send(new GetShopListingsRequest { ShopId = shopId, Limit = limit }, cancellationToken);
        if (!response.Success)
            return CreateErrorResult("get_shop_active_listings", response.Message ?? "Aktif ilanlar çekilemedi.");

        var items = (response.Data as IEnumerable<ShopListingItemDto>)?.ToList() ?? new List<ShopListingItemDto>();

        IEnumerable<ShopListingItemDto> sorted = sortBy switch
        {
            "favorites" => items.OrderByDescending(i => i.Favorites),
            "price_desc" => items.OrderByDescending(i => i.Price),
            "price_asc" => items.OrderBy(i => i.Price),
            "seo_asc" => items.OrderBy(i => i.SeoScore),
            _ => items.OrderByDescending(i => i.Views).ThenByDescending(i => i.Favorites)
        };

        var summary = new
        {
            shopId,
            totalActiveListings = items.Count,
            averagePriceUsd = items.Count > 0 ? Math.Round(items.Average(i => i.Price), 2) : 0m,
            averageSeoScore = items.Count > 0 ? Math.Round(items.Average(i => i.SeoScore), 1) : 0.0,
            listingsWithFewerThan13Tags = items.Count(i => (i.Tags?.Count ?? 0) < 13),
            listingsWithIpRisk = items.Count(i => (i.RiskWarnings?.Count ?? 0) > 0),
            listings = sorted.Select(i => new
            {
                i.ListingId,
                i.Title,
                i.Price,
                i.Quantity,
                i.Views,
                i.Favorites,
                tagCount = i.Tags?.Count ?? 0,
                i.Tags,
                i.SeoScore,
                i.Status,
                hasIpRisk = (i.RiskWarnings?.Count ?? 0) > 0
            }).ToList()
        };

        return CreateSuccessResult("get_shop_active_listings", summary);
    }

    private async Task<McpToolCallResult> HandleCreateDraftListingAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var title = GetStringArg(args, "title", "Custom 3D Printed Model");
        var price = GetDecimalArg(args, "priceUsd", 19.90m);
        var description = GetStringArg(args, "description", "Handcrafted 3D printed model.");
        var quantity = GetIntArg(args, "quantity", 15);
        var taxonomyId = GetIntArg(args, "taxonomyId", 1042);
        var tags = GetStringArrayArg(args, "tags");
        var materials = GetStringArrayArg(args, "materials");

        var cmd = new FastListingPublishCommand(
            ShopId: shopId,
            Title: title,
            Description: description,
            Price: price,
            Quantity: quantity,
            TaxonomyId: taxonomyId,
            ShippingProfileId: null,
            ReadinessStateId: null,
            IsDigital: false,
            Tags: tags,
            Materials: materials.Count > 0 ? materials : new[] { "PLA Filament", "Plastic" },
            State: "draft",
            WhoMade: "i_did",
            WhenMade: "made_to_order",
            Images: null,
            Variations: null,
            VariationGroups: null);

        var response = await _mediator.Send(new CreateListingCommandRequest { Command = cmd }, cancellationToken);
        if (!response.Success)
            return CreateErrorResult("create_etsy_draft_listing", response.Message ?? "Etsy taslak ilan oluşturulamadı.");

        return CreateSuccessResult("create_etsy_draft_listing", response.Data);
    }

    private async Task<McpToolCallResult> HandleUpdateListingAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var listingId = GetStringArg(args, "listingId", string.Empty);
        if (string.IsNullOrWhiteSpace(listingId))
            return CreateErrorResult("update_etsy_listing", "listingId zorunludur.");

        var title = GetStringArg(args, "title", string.Empty);
        var description = GetStringArg(args, "description", string.Empty);
        var tags = GetStringArrayArg(args, "tags");
        var materials = GetStringArrayArg(args, "materials");

        var dto = new UpdateListingRequestDto(shopId, title, description, tags, materials);
        var response = await _mediator.Send(new UpdateListingCommandRequest
        {
            ListingId = listingId,
            ShopId = shopId,
            Dto = dto
        }, cancellationToken);

        if (!response.Success)
            return CreateErrorResult("update_etsy_listing", response.Message ?? "Etsy ilanı güncellenemedi.");

        return CreateSuccessResult("update_etsy_listing", response.Data);
    }

    private async Task<McpToolCallResult> HandleMarketSearchAsync(string shopId, JsonElement? args, CancellationToken cancellationToken)
    {
        var keyword = GetStringArg(args, "keyword", "3d printed model");
        var limit = Math.Clamp(GetIntArg(args, "limit", 12), 1, 50);

        var query = new MarketSearchQuery(keyword, limit, "market_score", shopId);
        var response = await _mediator.Send(new SearchMarketQuery(query), cancellationToken);
        if (!response.Success)
            return CreateErrorResult("search_market_intelligence", response.Message ?? "Pazar araştırması yapılamadı.");

        return CreateSuccessResult("search_market_intelligence", response.Data);
    }

    private async Task<McpToolCallResult> HandleShippingRatesAsync(JsonElement? args, CancellationToken cancellationToken)
    {
        var country = GetStringArg(args, "countryCode", "US");
        var weight = (double)GetDecimalArg(args, "weightKg", 0.4m);
        var width = (double)GetDecimalArg(args, "widthCm", 15m);
        var length = (double)GetDecimalArg(args, "lengthCm", 20m);
        var height = (double)GetDecimalArg(args, "heightCm", 10m);

        var query = new CarrierQuotesQuery
        {
            CountryCode = country,
            WeightKg = weight > 0 ? weight : 0.4,
            WidthCm = width > 0 ? width : 15,
            LengthCm = length > 0 ? length : 20,
            HeightCm = height > 0 ? height : 10,
            UsdTryRate = 49.30m
        };

        var response = await _mediator.Send(new GetCarrierQuotesRequest { Query = query }, cancellationToken);
        if (!response.Success)
            return CreateErrorResult("calculate_shipping_rates", response.Message ?? "Kargo teklifleri hesaplanamadı.");

        return CreateSuccessResult("calculate_shipping_rates", response.Data);
    }

    private async Task<McpToolCallResult> HandleOfficialEtsyMcpAsync(JsonElement? args, CancellationToken cancellationToken)
    {
        var officialToolName = GetStringArg(args, "officialToolName", "learn_etsy_api");
        var rawResult = await _officialMcpClient.CallOfficialToolAsync(officialToolName, args, cancellationToken);

        return new McpToolCallResult(
            "query_etsy_official_dev_mcp",
            false,
            new[] { new McpContentBlock("text", rawResult) },
            new { officialToolName, rawResult });
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriodDates(string period)
    {
        var now = DateTimeOffset.UtcNow;
        var norm = (period ?? "this_month").Trim().ToLowerInvariant();
        return norm switch
        {
            "today" => (new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero), now),
            "last_month" =>
                (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1),
                 new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(-1)),
            "this_year" => (new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero), now),
            "all_time" => (new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), now),
            _ => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now)
        };
    }

    private static string GetStringArg(JsonElement? args, string key, string defaultValue)
    {
        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? defaultValue;
            return prop.ToString();
        }
        return defaultValue;
    }

    private static int GetIntArg(JsonElement? args, string key, int defaultValue)
    {
        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
                return val;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                return parsed;
        }
        return defaultValue;
    }

    private static decimal GetDecimalArg(JsonElement? args, string key, decimal defaultValue)
    {
        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val))
                return val;
            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return defaultValue;
    }

    private static DateTimeOffset? GetDateArg(JsonElement? args, string key)
    {
        var str = GetStringArg(args, key, string.Empty);
        if (!string.IsNullOrWhiteSpace(str) && DateTimeOffset.TryParse(str, out var dt))
            return dt;
        return null;
    }

    private static IReadOnlyList<string> GetStringArrayArg(JsonElement? args, string key)
    {
        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Array)
            {
                return prop.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? (e.GetString() ?? string.Empty) : e.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                return (prop.GetString() ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }
        }
        return Array.Empty<string>();
    }

    private static McpToolCallResult CreateSuccessResult(string toolName, object? data)
    {
        var json = JsonSerializer.Serialize(data, JsonOpts);
        return new McpToolCallResult(
            toolName,
            false,
            new[] { new McpContentBlock("text", json) },
            data);
    }

    private static McpToolCallResult CreateErrorResult(string toolName, string errorMessage)
    {
        var errObj = new { error = errorMessage };
        var json = JsonSerializer.Serialize(errObj, JsonOpts);
        return new McpToolCallResult(
            toolName,
            true,
            new[] { new McpContentBlock("text", json) },
            errObj);
    }
}
