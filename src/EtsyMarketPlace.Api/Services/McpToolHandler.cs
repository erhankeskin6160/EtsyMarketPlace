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

    public McpToolHandler(IEtsyReportingService reportingService, IEtsyFinancialAnalysisService analysisService)
    {
        _reportingService = reportingService;
        _analysisService = analysisService;
    }

    /// <summary>
    /// Gemini Spark'a sunulan tüm mağaza araçlarının listesi
    /// </summary>
    public List<McpToolDefinition> GetRegisteredTools() =>
        [
            Tool("get_etsy_bank_payouts", "Etsy banka transferlerini tarih, tutar, kur ve referans bilgileriyle listeler.", new { shopId = RequiredString("Mağaza kimliği"), startDate = OptionalDate("Başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Bitiş tarihi (YYYY-MM-DD)") }),
            Tool("get_financial_performance", "Brüt satış, platform komisyonu, reklam, ürün ve kargo maliyetleri ile net kâr marjını döner.", new { shopId = RequiredString("Mağaza kimliği"), period = new { type = "string", @enum = new[] { "today", "this_month", "last_month" }, description = "Rapor dönemi" }, startDate = OptionalDate("Özel başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Özel bitiş tarihi (YYYY-MM-DD)") }),
            Tool("get_unfulfilled_cost_alerts", "Maliyeti eksik açık siparişleri listeler.", new { shopId = RequiredString("Mağaza kimliği") }),
            Tool("get_daily_shop_brief", "Günlük mağaza sağlık skoru ve finans özetini döner.", new { shopId = RequiredString("Mağaza kimliği"), date = OptionalDate("Rapor tarihi (YYYY-MM-DD)") }),
            Tool("analyze_etsy_financials", "Finansal performansı kural tabanlı olarak analiz eder; marj, gider, uyarı ve önerileri döner.", new { shopId = RequiredString("Mağaza kimliği"), startDate = OptionalDate("Başlangıç tarihi (YYYY-MM-DD)"), endDate = OptionalDate("Bitiş tarihi (YYYY-MM-DD)") })
        ];

    /// <summary>
    /// Gemini Spark bir araç çağırdığında çalıştırılan ana motor
    /// </summary>
    public async Task<object> ExecuteToolAsync(string toolName, JsonElement? args)
    {
        var parameters = args is { ValueKind: JsonValueKind.Object } value ? value : throw new ArgumentException("Araç parametreleri nesne olmalıdır.");
        var shopId = Required(parameters, "shopId");
        return toolName switch
        {
            "get_etsy_bank_payouts" => Content(await GetPayoutsAsync(shopId, parameters)),
            "get_financial_performance" => Content(await GetPerformanceAsync(shopId, parameters)),
            "get_unfulfilled_cost_alerts" => Content(new { shopId, alerts = await _reportingService.GetUnfulfilledCostAlertsAsync(shopId) }),
            "get_daily_shop_brief" => Content(new { shopId, brief = await _reportingService.GetDailyShopBriefAsync(shopId, ParseDate(parameters, "date") ?? DateTimeOffset.UtcNow) }),
            "analyze_etsy_financials" => Content(await GetAnalysisAsync(shopId, parameters)),
            _ => throw new InvalidOperationException($"Bilinmeyen MCP aracı: {toolName}")
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
        return new { shopId, performance = await _reportingService.GetFinancialPerformanceAsync(shopId, start, end) };
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
    private static McpToolDefinition Tool(string name, string description, object schema) => new() { Name = name, Description = description, InputSchema = new { type = "object", properties = schema, required = new[] { "shopId" } } };
    private static object RequiredString(string description) => new { type = "string", description };
    private static object OptionalDate(string description) => new { type = "string", format = "date", description };
}
