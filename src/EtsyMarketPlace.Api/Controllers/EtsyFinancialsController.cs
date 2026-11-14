using EtsyMarketPlace.Application.Features.Financials;
using EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetFinancialPerformance;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetMonthlyOrderSummaries;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}")]
[Route("api")]
public class EtsyFinancialsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;
    private readonly IEtsyDashboardService _dashboard;
    private readonly IEtsyReportingService _reporting;
    private readonly IEtsyFinancialAnalysisService _analysis;
    private readonly IEtsyIntegrationRepository _repository;
    private readonly IEtsySynchronizationService _synchronization;
    private readonly EtsyMarketPlace.Domain.Orders.IOrderAddressRepository? _addressRepository;

    public EtsyFinancialsController(
        IConfiguration config,
        IMediator mediator,
        IEtsyDashboardService dashboard,
        IEtsyReportingService reporting,
        IEtsyFinancialAnalysisService analysis,
        IEtsyIntegrationRepository repository,
        IEtsySynchronizationService synchronization,
        EtsyMarketPlace.Domain.Orders.IOrderAddressRepository? addressRepository = null)
    {
        _config = config;
        _mediator = mediator;
        _dashboard = dashboard;
        _reporting = reporting;
        _analysis = analysis;
        _repository = repository;
        _synchronization = synchronization;
        _addressRepository = addressRepository;
    }

    [HttpGet("etsy/banking/payouts")]
    [EndpointSummary("Etsy Banka Transferleri (Payouts)")]
    public async Task<IActionResult> GetEtsyBankPayouts(
        [FromQuery] string shopId = "53236321",
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var end = endDate ?? DateTimeOffset.UtcNow;
        var start = startDate ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });

        var query = new GetBankPayoutsRequest
        {
            ShopId = resolvedShopId,
            StartDate = start,
            EndDate = end
        };

        var response = await _mediator.Send(query, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/financial/performance")]
    [EndpointSummary("Finansal Performans ve Kâr-Zarar Karnesi")]
    public async Task<IActionResult> GetFinancialPerformance(
        [FromQuery] string shopId = "53236321",
        [FromQuery] string period = "last_month",
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"fin-ledger:{resolvedShopId}:{period}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        object? uncachedDegradedData = null;

        var response = await DashboardResponseCache.GetOrAddAsync(
            cacheKey,
            TimeSpan.FromMinutes(4),
            async () =>
            {
                var query = new GetFinancialPerformanceRequest
                {
                    ShopId = resolvedShopId,
                    Period = period,
                    StartDate = startDate,
                    EndDate = endDate
                };
                var res = await _mediator.Send(query, cancellationToken);
                if (!res.Success || res.Data is null)
                    return null!;

                if (res.Data is LiveFinancialPerformanceResult livePerf && !string.IsNullOrWhiteSpace(livePerf.LedgerWarning))
                {
                    uncachedDegradedData = livePerf;
                    return null!;
                }

                return res;
            },
            forceRefresh: refresh);

        if (response != null && response.Success)
            return Ok(response.Data);

        if (uncachedDegradedData is not null)
            return Ok(uncachedDegradedData);

        var fallbackQuery = new GetFinancialPerformanceRequest
        {
            ShopId = resolvedShopId,
            Period = period,
            StartDate = startDate,
            EndDate = endDate
        };
        var fallbackResponse = await _mediator.Send(fallbackQuery, cancellationToken);
        return StatusCode(fallbackResponse.StatusCode, fallbackResponse);
    }

    [HttpGet("etsy/orders/monthly-summaries")]
    [EndpointSummary("Aylık Sipariş ve Ciro Özetleri (PostgreSQL)")]
    public async Task<IActionResult> GetMonthlyOrderSummaries(
        [FromQuery] string shopId = "53236321",
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var query = new GetMonthlyOrderSummariesRequest
        {
            ShopId = resolvedShopId,
            Months = months
        };

        var response = await _mediator.Send(query, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/financial/analysis")]
    [EndpointSummary("Akıllı Finansal Analiz ve Öneri Motoru")]
    public async Task<IActionResult> GetFinancialAnalysis(
        [FromQuery] string shopId = "53236321",
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var end = endDate ?? DateTimeOffset.UtcNow;
        var start = startDate ?? end.AddDays(-30);
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });

        var cacheKey = $"fin-analysis:{resolvedShopId}:{start:yyyyMMdd}:{end:yyyyMMdd}";
        var result = await DashboardResponseCache.GetOrAddAsync(
            cacheKey,
            TimeSpan.FromMinutes(3),
            () => _analysis.AnalyzeAsync(resolvedShopId, start, end, cancellationToken),
            forceRefresh: refresh);

        return Ok(result);
    }

    [HttpPost("etsy/financial/ai-forecast")]
    [EndpointSummary("Canlı AI CFO & Gelecek Ay Kâr/Ciro Tahmin Motoru")]
    public async Task<IActionResult> GenerateAiFinancialForecast(
        [FromBody] AiFinancialForecastCommand request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(new { error = "Geçersiz tahmin isteği." });

        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var normalizedCommand = request with { ShopId = resolvedShopId };
        var result = await _analysis.GenerateAiForecastAsync(normalizedCommand, cancellationToken);
        return Ok(result);
    }

    [HttpGet("etsy/orders/unfulfilled-cost-alerts")]
    [EndpointSummary("Maliyeti Eksik Sipariş Alarmları")]
    public async Task<IActionResult> GetUnfulfilledCostAlerts(
        [FromQuery] string shopId = "53236321",
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"uncosted-alerts:{resolvedShopId}";
        var result = await DashboardResponseCache.GetOrAddAsync(
            cacheKey,
            TimeSpan.FromMinutes(2),
            () => _reporting.GetUnfulfilledCostAlertsAsync(resolvedShopId, cancellationToken),
            forceRefresh: refresh);

        return Ok(result);
    }

    [HttpGet("etsy/shop/daily-brief")]
    [EndpointSummary("Günlük Mağaza Bülteni ve Sağlık Skoru")]
    public async Task<IActionResult> GetDailyShopBrief(
        [FromQuery] string shopId = "53236321",
        [FromQuery] DateTimeOffset? date = null,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"daily-brief:{resolvedShopId}:{date:yyyyMMdd}";
        var result = await DashboardResponseCache.GetOrAddAsync(
            cacheKey,
            TimeSpan.FromMinutes(2),
            () => _dashboard.GetDailyShopBriefAsync(resolvedShopId, date, cancellationToken),
            forceRefresh: refresh);

        return Ok(result);
    }

    [HttpGet("etsy/shop/recent-orders")]
    [EndpointSummary("Son Siparişler — Canlı Satış Akışı")]
    public async Task<IActionResult> GetRecentOrders(
        [FromQuery] string shopId = "53236321",
        [FromQuery] int days = 31,
        [FromQuery] int limit = 15,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var clampedDays = Math.Clamp(days, 1, 90);
        var clampedLimit = Math.Clamp(limit, 1, 50);
        var cacheKey = $"recent-orders:{resolvedShopId}:{clampedDays}:{clampedLimit}";

        try
        {
            var result = await DashboardResponseCache.GetOrAddAsync(
                cacheKey,
                TimeSpan.FromSeconds(90),
                () => _dashboard.GetRecentOrdersAsync(resolvedShopId, clampedDays, clampedLimit, cancellationToken),
                forceRefresh: refresh);

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
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"fin-orders:{resolvedShopId}:{period}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";

        try
        {
            var result = await DashboardResponseCache.GetOrAddAsync(
                cacheKey,
                TimeSpan.FromMinutes(2),
                () => _dashboard.GetOrderFinancialsAsync(resolvedShopId, period, startDate, endDate, cancellationToken),
                forceRefresh: refresh);

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

        DashboardResponseCache.Clear();

        return Ok(new { success = true, receiptId, updated = true });
    }

    [HttpGet("etsy/financial/daily-series")]
    [EndpointSummary("Günlük Gelir ve Net Kâr Serisi")]
    public async Task<IActionResult> GetDailySeries(
        [FromQuery] string shopId = "53236321",
        [FromQuery] string? month = null,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cacheKey = $"daily-series:{resolvedShopId}:{month ?? "current"}";

        try
        {
            var result = await DashboardResponseCache.GetOrAddAsync(
                cacheKey,
                TimeSpan.FromSeconds(90),
                () => _dashboard.GetDailySeriesAsync(resolvedShopId, month, cancellationToken),
                forceRefresh: refresh);

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
        var command = new ImportFullFinancialsRequest
        {
            ShopId = request.ShopId,
            Transactions = request.Transactions,
            Payouts = request.Payouts,
            OrderAlerts = request.OrderAlerts,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd
        };

        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(response.StatusCode, response.Success ? response.Data : response);
    }

    [HttpPost("etsy/orders/monthly-import")]
    [EndpointSummary("Aylık Sipariş Geçmişi Aktarımı")]
    public async Task<IActionResult> ImportMonthlyOrders([FromBody] EtsyMonthlyOrderImportRequest request, CancellationToken cancellationToken)
    {
        var command = new ImportFullFinancialsRequest
        {
            ShopId = request.ShopId,
            MonthlyOrders = request.Summaries
        };

        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(response.StatusCode, response.Success ? response.Data : response);
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
        var command = new ImportFullFinancialsRequest
        {
            ShopId = request.ShopId,
            Transactions = request.Transactions,
            Payouts = request.Payouts,
            OrderAlerts = request.OrderAlerts,
            MonthlyOrders = request.MonthlyOrders,
            TrafficRecords = request.TrafficRecords,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd
        };

        if (request.Charts?.Count > 0)
        {
            command.ChartPayloads = request.Charts.Select(c => new EtsyChartSnapshot(
                request.ShopId.Trim(),
                c.ChartType.Trim().ToLowerInvariant(),
                c.PeriodStart,
                c.PeriodEnd,
                c.ImagePngBase64,
                DateTimeOffset.UtcNow
            )).ToList();
        }

        var response = await _mediator.Send(command, cancellationToken);

        // Masaüstünden yeni veriler aktarıldığında eski önbelleği anında temizle (tam parite)
        DashboardResponseCache.Clear();

        return StatusCode(response.StatusCode, response.Success ? response.Data : response);
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

    [HttpGet("etsy/financial/payment-reserve-snapshot")]
    [AllowAnonymous]
    [EndpointSummary("Etsy Canlı Ödeme Rezervi ve Bakiye Durumunu Getir")]
    public async Task<IActionResult> GetPaymentReserveSnapshot(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetPaymentReserveSnapshotQuery(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("etsy/financial/payment-reserve-snapshot")]
    [AllowAnonymous]
    [EndpointSummary("Etsy Canlı Ödeme Rezervi ve Bakiye Durumunu Kaydet / Senkronize Et")]
    public async Task<IActionResult> SavePaymentReserveSnapshot(
        [FromBody] EtsyPaymentReserveSnapshotDto snapshot,
        CancellationToken cancellationToken = default)
    {
        if (snapshot == null) return BadRequest(new { error = "Snapshot boş olamaz." });
        var resolvedShopId = ResolveShopId(snapshot.ShopId, _config);
        var normalized = snapshot with { ShopId = resolvedShopId, UpdatedAt = DateTimeOffset.UtcNow };
        var response = await _mediator.Send(new SavePaymentReserveSnapshotCommand(normalized), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
