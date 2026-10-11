using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api")]
public class EtsyFinancialsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyDashboardService _dashboard;
    private readonly IEtsyReportingService _reporting;
    private readonly IEtsyFinancialAnalysisService _analysis;
    private readonly IEtsyIntegrationRepository _repository;
    private readonly IEtsySynchronizationService _synchronization;

    public EtsyFinancialsController(
        IConfiguration config,
        IEtsyDashboardService dashboard,
        IEtsyReportingService reporting,
        IEtsyFinancialAnalysisService analysis,
        IEtsyIntegrationRepository repository,
        IEtsySynchronizationService synchronization)
    {
        _config = config;
        _dashboard = dashboard;
        _reporting = reporting;
        _analysis = analysis;
        _repository = repository;
        _synchronization = synchronization;
    }

    [HttpGet("etsy/banking/payouts")]
    [EndpointSummary("Etsy Banka Transferleri (Payouts)")]
    public async Task<IActionResult> GetEtsyBankPayouts([FromQuery] string shopId = "53236321", [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var end = endDate ?? DateTimeOffset.UtcNow;
        var start = startDate ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
        return Ok(await _reporting.GetBankPayoutsAsync(resolvedShopId, start, end, cancellationToken));
    }

    [HttpGet("etsy/financial/performance")]
    [EndpointSummary("Finansal Performans ve Kâr-Zarar Karnesi")]
    public async Task<IActionResult> GetFinancialPerformance([FromQuery] string shopId = "53236321", [FromQuery] string period = "last_month", [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedShopId = ResolveShopId(shopId, _config);
            var cacheKey = $"fin-ledger:{resolvedShopId}:{period}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
            if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
                return Ok(cachedPayload);

            var performance = await _dashboard.GetFinancialPerformanceAsync(resolvedShopId, period, startDate, endDate, cancellationToken);
            DashboardResponseCache.Set(cacheKey, performance, TimeSpan.FromMinutes(4));
            return Ok(performance);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("etsy/financial/analysis")]
    [EndpointSummary("Akıllı Finansal Analiz ve Öneri Motoru")]
    public async Task<IActionResult> GetFinancialAnalysis([FromQuery] string shopId = "53236321", [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var end = endDate ?? DateTimeOffset.UtcNow;
        var start = startDate ?? end.AddDays(-30);
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
        return Ok(await _analysis.AnalyzeAsync(resolvedShopId, start, end, cancellationToken));
    }

    [HttpGet("etsy/orders/unfulfilled-cost-alerts")]
    [EndpointSummary("Maliyeti Eksik Sipariş Alarmları")]
    public async Task<IActionResult> GetUnfulfilledCostAlerts([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        return Ok(await _reporting.GetUnfulfilledCostAlertsAsync(resolvedShopId, cancellationToken));
    }

    [HttpGet("etsy/shop/daily-brief")]
    [EndpointSummary("Günlük Mağaza Bülteni ve Sağlık Skoru")]
    public async Task<IActionResult> GetDailyShopBrief([FromQuery] string shopId = "53236321", [FromQuery] DateTimeOffset? date = null, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var brief = await _dashboard.GetDailyShopBriefAsync(resolvedShopId, date, cancellationToken);
        return Ok(brief);
    }

    [HttpGet("etsy/shop/recent-orders")]
    [EndpointSummary("Son Siparişler — Canlı Satış Akışı")]
    public async Task<IActionResult> GetRecentOrders([FromQuery] string shopId = "53236321", [FromQuery] int days = 31, [FromQuery] int limit = 15, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var clampedDays = Math.Clamp(days, 1, 90);
        var clampedLimit = Math.Clamp(limit, 1, 50);
        var cacheKey = $"recent-orders:{resolvedShopId}:{clampedDays}:{clampedLimit}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        try
        {
            var result = await _dashboard.GetRecentOrdersAsync(resolvedShopId, clampedDays, clampedLimit, cancellationToken);
            DashboardResponseCache.Set(cacheKey, result, TimeSpan.FromSeconds(90));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Siparişler canlı Etsy API'den alınamadı: " + ex.Message });
        }
    }

    [HttpGet("etsy/financial/orders")]
    [EndpointSummary("Siparişler & Net Kâr Detaylı Dökümü")]
    public async Task<IActionResult> GetOrderFinancials(
        [FromQuery] string shopId = "53236321",
        [FromQuery] string period = "this_month",
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"fin-orders:{resolvedShopId}:{period}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        try
        {
            var result = await _dashboard.GetOrderFinancialsAsync(resolvedShopId, period, startDate, endDate, cancellationToken);
            DashboardResponseCache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Sipariş finans dökümü alınamadı: " + ex.Message });
        }
    }

    [HttpPost("etsy/financial/orders/{receiptId}/cost")]
    [EndpointSummary("Sipariş Maliyetini Güncelle")]
    public async Task<IActionResult> UpdateOrderCost(
        string receiptId,
        [FromBody] EtsyUpdateOrderCostRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        if (string.IsNullOrWhiteSpace(receiptId))
            return BadRequest(new { error = "receiptId zorunludur." });

        await _repository.UpsertOrderCostAsync(
            resolvedShopId,
            receiptId.Trim(),
            request.ProductCost,
            request.ShippingCost,
            request.PackagingCost,
            request.Notes,
            cancellationToken);

        return Ok(new { success = true, receiptId, updated = true });
    }

    [HttpGet("etsy/financial/daily-series")]
    [EndpointSummary("Günlük Gelir ve Net Kâr Serisi")]
    public async Task<IActionResult> GetDailySeries([FromQuery] string shopId = "53236321", [FromQuery] string? month = null, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"daily-series:{resolvedShopId}:{month ?? "current"}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        try
        {
            var result = await _dashboard.GetDailySeriesAsync(resolvedShopId, month, cancellationToken);
            DashboardResponseCache.Set(cacheKey, result, TimeSpan.FromSeconds(90));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Günlük gelir/kâr serisi canlı Etsy API'den alınamadı: " + ex.Message });
        }
    }

    [HttpPost("etsy/sync")]
    [EndpointSummary("Etsy API Doğrudan Eşitleme")]
    public async Task<IActionResult> SynchronizeEtsyFinance([FromBody] EtsySyncRequest request, CancellationToken cancellationToken)
    {
        var shopId = string.IsNullOrWhiteSpace(request.ShopId)
            ? (_config["Etsy:DefaultShopId"] ?? _config["Etsy:ShopId"] ?? "53236321")
            : request.ShopId;

        var end = request.EndDate ?? DateTimeOffset.UtcNow;
        var start = request.StartDate ?? end.AddDays(-30);
        if (start > end)
            return BadRequest(new { error = "StartDate EndDate değerinden sonra olamaz." });

        var result = await _synchronization.SynchronizeAsync(shopId, start, end, cancellationToken);
        return result.Succeeded ? Ok(result) : Problem(result.ErrorMessage, statusCode: StatusCodes.Status502BadGateway);
    }

    [HttpPost("etsy/financial/import")]
    [EndpointSummary("Masaüstü Finansal Rapor Aktarımı")]
    public async Task<IActionResult> ImportEtsyFinancialData([FromBody] EtsyFinancialImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return BadRequest(new { error = "ShopId zorunludur." });

        var shopId = request.ShopId.Trim();

        if (request.Transactions?.Count > 0)
            await _repository.SaveTransactionsAsync(request.Transactions, cancellationToken);

        if (request.Payouts?.Count > 0)
            await _repository.SavePayoutsAsync(request.Payouts, cancellationToken);

        if (request.OrderAlerts?.Count > 0)
            await _repository.SaveOrderAlertsAsync(request.OrderAlerts, cancellationToken);

        await _repository.SaveSyncStateAsync(shopId, "financial", request.PeriodEnd ?? DateTimeOffset.UtcNow, cancellationToken: cancellationToken);

        return Ok(new
        {
            saved = true,
            shopId,
            transactionsCount = request.Transactions?.Count ?? 0,
            payoutsCount = request.Payouts?.Count ?? 0,
            alertsCount = request.OrderAlerts?.Count ?? 0
        });
    }

    [HttpPost("etsy/orders/monthly-import")]
    [EndpointSummary("Aylık Sipariş Geçmişi Aktarımı")]
    public async Task<IActionResult> ImportMonthlyOrders([FromBody] EtsyMonthlyOrderImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return BadRequest(new { error = "ShopId zorunludur." });

        if (request.Summaries?.Count > 0)
            await _repository.SaveMonthlyOrderSummariesAsync(request.Summaries, cancellationToken);

        return Ok(new { saved = true, shopId = request.ShopId, count = request.Summaries?.Count ?? 0 });
    }

    [HttpPost("etsy/analytics/traffic-import")]
    [EndpointSummary("Ürün Ziyaret ve Trafik Metrikleri Aktarımı")]
    public async Task<IActionResult> ImportListingTraffic([FromBody] EtsyListingTrafficImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return BadRequest(new { error = "ShopId zorunludur." });

        if (request.Records?.Count > 0)
            await _repository.SaveListingTrafficDailyAsync(request.Records, cancellationToken);

        return Ok(new { saved = true, shopId = request.ShopId, count = request.Records?.Count ?? 0 });
    }

    [HttpPost("etsy/charts/upload")]
    [EndpointSummary("Grafik Görselleri Yükleme (PNG Base64)")]
    public async Task<IActionResult> UploadChartSnapshot([FromBody] EtsyChartUploadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ChartType) || string.IsNullOrWhiteSpace(request.ImagePngBase64))
            return BadRequest(new { error = "ShopId, ChartType ve ImagePngBase64 zorunludur." });

        var snapshot = new EtsyChartSnapshot(
            request.ShopId.Trim(),
            request.ChartType.Trim().ToLowerInvariant(),
            request.PeriodStart,
            request.PeriodEnd,
            request.ImagePngBase64,
            DateTimeOffset.UtcNow);

        await _repository.SaveChartSnapshotAsync(snapshot, cancellationToken);
        return Ok(new { saved = true, shopId = request.ShopId, chartType = request.ChartType });
    }

    [HttpGet("etsy/charts/{shopId}/{chartType}.png")]
    [EndpointSummary("Grafik PNG Resmini Önizleme")]
    public async Task<IActionResult> GetChartSnapshotImage(string shopId, string chartType, CancellationToken cancellationToken)
    {
        var resolvedShopId = (shopId == "default" || shopId == "523236321") ? (_config["Etsy:DefaultShopId"] ?? "53236321") : shopId;
        var snapshot = await _repository.GetChartSnapshotAsync(resolvedShopId, chartType.ToLowerInvariant(), cancellationToken);
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ImagePngBase64))
            return NotFound(new { error = "Grafik resmi bulunamadı." });

        try
        {
            var bytes = Convert.FromBase64String(snapshot.ImagePngBase64);
            return File(bytes, "image/png");
        }
        catch
        {
            return Problem("Grafik resmi çözümlenemedi.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("etsy/sync-all")]
    [EndpointSummary("🌐 Masaüstü Tam Senkronizasyon (Tek Hamlede Aktarım)")]
    public async Task<IActionResult> SyncAllDesktopData([FromBody] EtsyAllDataImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return BadRequest(new { error = "ShopId zorunludur." });

        var shopId = request.ShopId.Trim();

        if (request.Transactions?.Count > 0)
            await _repository.SaveTransactionsAsync(request.Transactions, cancellationToken);

        if (request.Payouts?.Count > 0)
            await _repository.SavePayoutsAsync(request.Payouts, cancellationToken);

        if (request.OrderAlerts?.Count > 0)
            await _repository.SaveOrderAlertsAsync(request.OrderAlerts, cancellationToken);

        if (request.MonthlyOrders?.Count > 0)
            await _repository.SaveMonthlyOrderSummariesAsync(request.MonthlyOrders, cancellationToken);

        if (request.TrafficRecords?.Count > 0)
            await _repository.SaveListingTrafficDailyAsync(request.TrafficRecords, cancellationToken);

        if (request.Charts?.Count > 0)
        {
            foreach (var c in request.Charts)
            {
                var snap = new EtsyChartSnapshot(shopId, c.ChartType.Trim().ToLowerInvariant(), c.PeriodStart, c.PeriodEnd, c.ImagePngBase64, DateTimeOffset.UtcNow);
                await _repository.SaveChartSnapshotAsync(snap, cancellationToken);
            }
        }

        await _repository.SaveSyncStateAsync(shopId, "all_desktop_data", request.PeriodEnd ?? DateTimeOffset.UtcNow, cancellationToken: cancellationToken);

        return Ok(new
        {
            saved = true,
            shopId,
            transactionsCount = request.Transactions?.Count ?? 0,
            payoutsCount = request.Payouts?.Count ?? 0,
            alertsCount = request.OrderAlerts?.Count ?? 0,
            monthlyOrdersCount = request.MonthlyOrders?.Count ?? 0,
            trafficCount = request.TrafficRecords?.Count ?? 0,
            chartsCount = request.Charts?.Count ?? 0
        });
    }

    [HttpGet("banking/deposits")]
    [EndpointSummary("Banka Yatırım ve Para Transferleri Özeti")]
    public async Task<IActionResult> GetBankDeposits([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var summary = await _dashboard.GetRealBankDepositsAsync(resolvedShopId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("financial/summary")]
    [EndpointSummary("Hızlı Finansal Özet Tablosu")]
    public async Task<IActionResult> GetFinancialSummary([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var summary = await _dashboard.GetRealFinancialSummaryAsync(resolvedShopId, cancellationToken);
        return Ok(summary);
    }
}
