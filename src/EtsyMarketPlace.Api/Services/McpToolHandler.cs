using System.Text.Json;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Services;

/// <summary>
/// Google Gemini Spark MCP (Model Context Protocol) araçlarını yöneten servis.
/// </summary>
public sealed class McpToolHandler
{
    private readonly IEtsyReportingService _reportingService;
    private readonly IEtsyFinancialAnalysisService _analysisService;
    private readonly IEtsyIntegrationRepository _repository;
    private readonly IConfiguration _configuration;

    public McpToolHandler(
        IEtsyReportingService reportingService,
        IEtsyFinancialAnalysisService analysisService,
        IEtsyIntegrationRepository repository,
        IConfiguration configuration)
    {
        _reportingService = reportingService;
        _analysisService = analysisService;
        _repository = repository;
        _configuration = configuration;
    }

    public string DefaultShopId =>
        _configuration["Etsy:DefaultShopId"]
        ?? _configuration["Etsy:ShopId"]
        ?? "53236321";

    public string ResolveShopId(JsonElement parameters)
    {
        if (parameters.TryGetProperty("shopId", out var value) && value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(raw) && raw != "523236321")
            {
                return raw;
            }
        }
        return DefaultShopId;
    }

    /// <summary>
    /// Gemini Spark'a sunulan tüm mağaza araçlarının listesi
    /// </summary>
    public List<McpToolDefinition> GetRegisteredTools() =>
        [
            Tool("get_etsy_bank_payouts", "Etsy banka transferlerini tarih, tutar, kur ve referans bilgileriyle listeler.", new { shopId = OptionalShopId(), startDate = OptionalDate("Başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Bitiş tarihi (YYYY-MM-DD)") }),
            Tool("get_financial_performance", "Mağazanın brüt satış, platform komisyonu, reklam, ürün ve kargo maliyetleri ile net kârını hem Türk Lirası (₺) hem Amerikan Doları ($) olarak döner. Masaüstü muhasebe paneliyle kuruşu kuruşuna senkronizedir.", new { shopId = OptionalShopId(), period = new { type = "string", @enum = new[] { "today", "this_month", "last_month" }, description = "Rapor dönemi" }, startDate = OptionalDate("Özel başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Özel bitiş tarihi (YYYY-MM-DD)") }),
            Tool("get_monthly_orders_breakdown", "Aylık sipariş adetlerini, satılan ürünleri, brüt ciroyu, sepet ortalamasını ve kargolama durumunu listeler.", new { shopId = OptionalShopId(), months = new { type = "integer", description = "Kaç aylık geçmiş (varsayılan 12)" } }),
            Tool("get_listing_traffic_analytics", "Hangi ürüne günde ve ayda kaç ziyaret/görüntülenme geldiğini, favorilenme sayılarını ve satış dönüşüm oranını listeler.", new { shopId = OptionalShopId(), limit = new { type = "integer", description = "Listelenecek ürün adedi (varsayılan 30)" } }),
            Tool("get_financial_chart_image", "Finansal modül grafiklerinin (gelir-gider bar, nakit akış çizgisi, gider pasta, 30 günlük tahmin) resim bağlantısını ve özetini döner.", new { shopId = OptionalShopId(), chartType = new { type = "string", @enum = new[] { "profit_bar", "cashflow_line", "cost_pie", "forecast" }, description = "Grafik tipi" } }),
            Tool("get_conversion_and_profit_alerts", "Yüksek görüntülenme alıp satışı olmayan (düşük dönüşüm) ürünleri ve maliyeti eksik siparişleri tespit eder.", new { shopId = OptionalShopId() }),
            Tool("get_unfulfilled_cost_alerts", "Maliyeti eksik açık siparişleri listeler.", new { shopId = OptionalShopId() }),
            Tool("get_daily_shop_brief", "Günlük mağaza sağlık skoru ve finans özetini döner.", new { shopId = OptionalShopId(), date = OptionalDate("Rapor tarihi (YYYY-MM-DD)") }),
            Tool("analyze_etsy_financials", "Finansal performansı kural tabanlı olarak analiz eder; marj, gider, uyarı ve önerileri döner.", new { shopId = OptionalShopId(), startDate = OptionalDate("Başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Bitiş tarihi (YYYY-MM-DD)") })
        ];

    /// <summary>
    /// Gemini Spark bir araç çağırdığında çalıştırılan ana motor
    /// </summary>
    public async Task<object> ExecuteToolAsync(string toolName, JsonElement? args)
    {
        var parameters = args is { ValueKind: JsonValueKind.Object } value ? value : throw new ArgumentException("Araç parametreleri nesne olmalıdır.");
        var shopId = ResolveShopId(parameters);
        return toolName switch
        {
            "get_etsy_bank_payouts" => Content(await GetPayoutsAsync(shopId, parameters)),
            "get_financial_performance" => Content(await GetPerformanceAsync(shopId, parameters)),
            "get_monthly_orders_breakdown" => Content(await GetMonthlyOrdersAsync(shopId, parameters)),
            "get_listing_traffic_analytics" => Content(await GetListingTrafficAsync(shopId, parameters)),
            "get_financial_chart_image" => Content(await GetChartImageAsync(shopId, parameters)),
            "get_conversion_and_profit_alerts" => Content(await GetConversionAndProfitAlertsAsync(shopId)),
            "get_unfulfilled_cost_alerts" => Content(new { shopId, alerts = await _reportingService.GetUnfulfilledCostAlertsAsync(shopId) }),
            "get_daily_shop_brief" => Content(new { shopId, brief = await _reportingService.GetDailyShopBriefAsync(shopId, ParseDate(parameters, "date") ?? DateTimeOffset.UtcNow) }),
            "analyze_etsy_financials" => Content(await GetAnalysisAsync(shopId, parameters)),
            _ => throw new InvalidOperationException($"Bilinmeyen MCP aracı: {toolName}")
        };
    }

    private async Task<object> GetMonthlyOrdersAsync(string shopId, JsonElement parameters)
    {
        var months = parameters.TryGetProperty("months", out var mVal) && mVal.TryGetInt32(out var m) ? Math.Clamp(m, 1, 36) : 12;
        var summaries = await _repository.GetMonthlyOrderSummariesAsync(shopId, months);
        var totalOrders = summaries.Sum(x => x.OrderCount);
        var totalRevenue = summaries.Sum(x => x.GrossRevenue);
        return new
        {
            shopId,
            queriedMonths = months,
            totalOrders,
            totalRevenue,
            monthlySummaries = summaries
        };
    }

    private async Task<object> GetListingTrafficAsync(string shopId, JsonElement parameters)
    {
        var limit = parameters.TryGetProperty("limit", out var lVal) && lVal.TryGetInt32(out var l) ? Math.Clamp(l, 1, 100) : 30;
        var records = await _repository.GetListingTrafficAnalyticsAsync(shopId, limit: limit);
        return new
        {
            shopId,
            listingCount = records.Count,
            topListingsByViews = records
        };
    }

    private async Task<object> GetChartImageAsync(string shopId, JsonElement parameters)
    {
        var chartType = Required(parameters, "chartType").ToLowerInvariant();
        var snapshot = await _repository.GetChartSnapshotAsync(shopId, chartType);
        if (snapshot == null)
        {
            return new
            {
                shopId,
                chartType,
                exists = false,
                message = "Bu grafik henüz masaüstünden aktarılmamış. Lütfen masaüstü uygulamasında Finansal Rapor'u açıp 'VDS'e Aktar' butonuna basın."
            };
        }

        var imageUrl = $"/api/etsy/charts/{Uri.EscapeDataString(shopId)}/{Uri.EscapeDataString(chartType)}.png";
        return new
        {
            shopId,
            chartType,
            exists = true,
            imageUrl,
            periodStart = snapshot.PeriodStart,
            periodEnd = snapshot.PeriodEnd,
            updatedAt = snapshot.UpdatedAt,
            message = $"Grafik resmi hazır. Doğrudan görüntülemek için: {imageUrl}",
            imagePreviewDataUrl = $"data:image/png;base64,{snapshot.ImagePngBase64}"
        };
    }

    private async Task<object> GetConversionAndProfitAlertsAsync(string shopId)
    {
        var traffic = await _repository.GetListingTrafficAnalyticsAsync(shopId, limit: 100);
        var unfulfilledAlerts = await _reportingService.GetUnfulfilledCostAlertsAsync(shopId);

        var lowConversion = traffic
            .Where(x => x.Views >= 30 && x.ConversionRate < 0.8m)
            .OrderByDescending(x => x.Views)
            .Take(10)
            .Select(x => new
            {
                x.ListingId,
                x.Title,
                x.Views,
                x.Favorites,
                x.UnitsSoldMonth,
                ConversionRatePercent = $"{x.ConversionRate:F1}%",
                Advice = "Trafik yüksek fakat satış zayıf. Fotoğraflar, başlık/etiket uygunluğu veya fiyat seviyesi optimize edilmeli."
            })
            .ToList();

        return new
        {
            shopId,
            lowConversionListingCount = lowConversion.Count,
            lowConversionListings = lowConversion,
            costAlertsCount = unfulfilledAlerts.Count,
            unfulfilledCostAlerts = unfulfilledAlerts
        };
    }

    private async Task<object> GetAnalysisAsync(string shopId, JsonElement parameters)
    {
        var end = ParseDate(parameters, "endDate") ?? DateTimeOffset.UtcNow;
        var start = ParseDate(parameters, "startDate") ?? end.AddDays(-30);
        if (start > end) throw new ArgumentException("startDate endDate değerinden sonra olamaz.");
        return new { shopId, analysis = await _analysisService.AnalyzeAsync(shopId, start, end) };
    }

    private async Task<object> GetPayoutsAsync(string shopId, JsonElement parameters)
    {
        var end = ParseDate(parameters, "endDate") ?? DateTimeOffset.UtcNow;
        var start = ParseDate(parameters, "startDate") ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
        if (start > end) throw new ArgumentException("startDate endDate değerinden sonra olamaz.");
        return new { shopId, startDate = start, endDate = end, payouts = await _reportingService.GetBankPayoutsAsync(shopId, start, end) };
    }

    private async Task<object> GetPerformanceAsync(string shopId, JsonElement parameters)
    {
        var (start, end) = ResolvePeriod(parameters);
        var perf = await _reportingService.GetFinancialPerformanceAsync(shopId, start, end);
        return new
        {
            shopId,
            bilgilendirme = "Kullanıcı masaüstü uygulamasında 'TR Oto Kur' aktif olarak Türk Lirası (₺) cinsinden takip yapmaktadır. Raporlama yaparken aşağıdaki 'ozetRapor' içindeki TL (₺) ve Dolar ($) karşılıklarını birlikte kullanın.",
            ozetRapor = new
            {
                donem = $"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}",
                paraBirimi = "TRY / USD",
                brutSatisTL = perf.OrderGrossSalesTRY ?? perf.GrossSalesTRY,
                gercekNetKarTL = perf.OrderNetProfitTRY ?? perf.NetProfitTRY,
                brutSatisUSD = perf.OrderGrossSalesUSD ?? perf.GrossSales,
                netKarUSD = perf.NetProfit,
                netKarMarjiYuzde = Math.Round(perf.NetProfitMargin, 2),
                kullanilanDovizKuru = perf.ExchangeRateUsed,
                giderDetaylariUSD = new
                {
                    etsyKomisyonlari = perf.PlatformFees,
                    icReklam = perf.InternalAdsCost,
                    disReklam = perf.ExternalAdsCost,
                    urunMaliyetleri = perf.ProductCosts,
                    iadeler = perf.Refunds
                }
            },
            hamPerformans = perf
        };
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(JsonElement parameters)
    {
        var now = DateTimeOffset.UtcNow;
        if (parameters.TryGetProperty("startDate", out var sVal) && DateTimeOffset.TryParse(sVal.GetString(), out var sDate))
        {
            var eDate = parameters.TryGetProperty("endDate", out var eVal) && DateTimeOffset.TryParse(eVal.GetString(), out var parsedEnd)
                ? parsedEnd
                : now;
            return (sDate, eDate);
        }

        var period = parameters.TryGetProperty("period", out var value) ? value.GetString() : "this_month";
        return period switch
        {
            "today" => (now.Date, now),
            "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1)),
            "this_month" or null or "" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
            _ => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now)
        };
    }

    private static string Required(JsonElement parameters, string name) => parameters.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new ArgumentException($"'{name}' parametresi zorunludur.");

    private static DateTimeOffset? ParseDate(JsonElement parameters, string name)
    {
        if (!parameters.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return DateTimeOffset.TryParse(value.GetString(), out var date) ? date : throw new ArgumentException($"'{name}' geçerli bir tarih olmalıdır.");
    }

    private static object Content(object value) => new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) } } };
    private static McpToolDefinition Tool(string name, string description, object schema) => new() { Name = name, Description = description, InputSchema = new { type = "object", properties = schema } };
    private static object OptionalShopId() => new { type = "string", description = "Mağaza kimliği (opsiyonel; belirtilmezse varsayılan mağaza 53236321 kullanılır)" };
    private static object RequiredString(string description) => new { type = "string", description };
    private static object OptionalDate(string description) => new { type = "string", format = "date", description };
}

