using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api")]
public class EtsyFinancialsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyReportingService _reporting;
    private readonly IEtsyLedgerReportService _ledgerReport;
    private readonly IEtsyFinancialAnalysisService _analysis;
    private readonly IEtsyDataClient _etsyClient;
    private readonly IEtsyIntegrationRepository _repository;
    private readonly IEtsySynchronizationService _synchronization;
    private readonly IBankDepositService _bankService;

    public EtsyFinancialsController(
        IConfiguration config,
        IEtsyReportingService reporting,
        IEtsyLedgerReportService ledgerReport,
        IEtsyFinancialAnalysisService analysis,
        IEtsyDataClient etsyClient,
        IEtsyIntegrationRepository repository,
        IEtsySynchronizationService synchronization,
        IBankDepositService bankService)
    {
        _config = config;
        _reporting = reporting;
        _ledgerReport = ledgerReport;
        _analysis = analysis;
        _etsyClient = etsyClient;
        _repository = repository;
        _synchronization = synchronization;
        _bankService = bankService;
    }

    private static async Task<(IReadOnlyList<EtsyDashboardLedgerFee> Entries, string? Error)> SafeGetLedgerAsync(IEtsyDataClient client, string shopId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var entries = await client.GetPaymentAccountLedgerEntriesAsync(shopId, start, end, ct);
                return (entries, null);
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    Console.Error.WriteLine($"[dashboard] Odeme defteri okunamadi ({shopId}): {ex.Message}");
                    return (Array.Empty<EtsyDashboardLedgerFee>(), ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        return (Array.Empty<EtsyDashboardLedgerFee>(), "bilinmeyen hata");
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
        var resolvedShopId = ResolveShopId(shopId, _config);
        var now = DateTimeOffset.UtcNow;
        if (startDate.HasValue != endDate.HasValue)
            return BadRequest(new { error = "Özel tarih aralığı için startDate ve endDate birlikte gönderilmelidir." });

        var (start, end) = startDate.HasValue
            ? (startDate.Value, endDate!.Value)
            : period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now),
            "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)),
            null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
            _ => (DateTimeOffset.MinValue, DateTimeOffset.MinValue)
        };
        if (start == DateTimeOffset.MinValue) return BadRequest(new { error = "period today, this_month veya last_month olmalıdır." });
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });

        var cacheKey = $"fin-ledger:{resolvedShopId}:{start:yyyyMMddHHmm}:{end:yyyyMMddHHmm}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        var live = await _ledgerReport.GetLiveReportAsync(resolvedShopId, start, end, cancellationToken);
        if (live.LedgerOk)
        {
            var p = live.Performance;
            var payload = new
            {
                grossSales = p.GrossSales,
                platformFees = p.PlatformFees,
                internalAdsCost = p.InternalAdsCost,
                externalAdsCost = p.ExternalAdsCost,
                productCosts = p.ProductCosts,
                shippingCosts = p.ShippingCosts,
                refunds = p.Refunds,
                netProfit = p.NetProfit,
                netProfitMargin = p.NetProfitMargin,
                grossSalesTRY = p.GrossSalesTRY,
                netProfitTRY = p.NetProfitTRY,
                orderGrossSalesUSD = (decimal?)null,
                orderGrossSalesTRY = (decimal?)null,
                orderNetProfitTRY = (decimal?)null,
                exchangeRateUsed = p.ExchangeRateUsed,
                receiptCount = live.ReceiptCount,
                ledgerOk = true,
                ledgerWarning = live.Warning,
                source = live.Source,
                generatedAt = live.GeneratedAt
            };
            DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromMinutes(4));
            return Ok(payload);
        }

        var legacy = await _reporting.GetFinancialPerformanceAsync(resolvedShopId, start, end, cancellationToken);
        return Ok(new
        {
            grossSales = legacy.GrossSales,
            platformFees = legacy.PlatformFees,
            internalAdsCost = legacy.InternalAdsCost,
            externalAdsCost = legacy.ExternalAdsCost,
            productCosts = legacy.ProductCosts,
            shippingCosts = legacy.ShippingCosts,
            refunds = legacy.Refunds,
            netProfit = legacy.NetProfit,
            netProfitMargin = legacy.NetProfitMargin,
            grossSalesTRY = legacy.GrossSalesTRY,
            netProfitTRY = legacy.NetProfitTRY,
            orderGrossSalesUSD = legacy.OrderGrossSalesUSD,
            orderGrossSalesTRY = legacy.OrderGrossSalesTRY,
            orderNetProfitTRY = legacy.OrderNetProfitTRY,
            exchangeRateUsed = legacy.ExchangeRateUsed,
            receiptCount = live.ReceiptCount,
            ledgerOk = false,
            ledgerWarning = live.Warning,
            source = "legacy-fallback",
            generatedAt = live.GeneratedAt
        });
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
        var requestedDate = date ?? DateTimeOffset.UtcNow;
        var briefStart = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, requestedDate.Offset);
        var briefEnd = briefStart.AddDays(1).AddTicks(-1);

        var live = await _ledgerReport.GetLiveReportAsync(resolvedShopId, briefStart, briefEnd, cancellationToken);
        var performance = live.LedgerOk
            ? live.Performance
            : await _reporting.GetFinancialPerformanceAsync(resolvedShopId, briefStart, briefEnd, cancellationToken);

        var payouts = await _reporting.GetBankPayoutsAsync(resolvedShopId, briefStart, briefEnd, cancellationToken);
        var alerts = await _reporting.GetUnfulfilledCostAlertsAsync(resolvedShopId, cancellationToken);
        var score = performance.GrossSales == 0 ? 0 : Math.Clamp((int)Math.Round(performance.NetProfitMargin), 0, 100);

        return Ok(new DailyShopBrief(
            requestedDate,
            score,
            score >= 80 ? "Mükemmel" : score >= 50 ? "Dikkat" : "Riskli",
            0,
            performance.GrossSales,
            performance.NetProfit,
            payouts.Sum(x => x.Amount),
            alerts.Count,
            performance.GrossSalesTRY,
            performance.NetProfitTRY,
            performance.ExchangeRateUsed));
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
            var end = DateTimeOffset.UtcNow;
            var start = end.AddDays(-clampedDays);
            var receipts = await _etsyClient.GetShopReceiptsAsync(resolvedShopId, start, end, cancellationToken);
            var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, resolvedShopId, start, end, cancellationToken);
            var costs = await _repository.GetOrderCostsAsync(resolvedShopId, cancellationToken);
            var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledgerEntries, costs);

            var payload = new
            {
                orders = rows.Take(clampedLimit).Select(r => new
                {
                    date = r.CreatedAt.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yy", CultureInfo.InvariantCulture),
                    receiptId = r.ReceiptId,
                    title = EtsyLiveDashboardCalculator.ShortenTitle(r.Title),
                    quantity = r.Quantity,
                    totalUsd = r.GrandTotal,
                    netProfitUsd = r.NetProfitUsd,
                    hasCost = r.HasCostData
                }).ToList(),
                count = rows.Count,
                ledgerOk = ledgerError is null,
                ledgerWarning = ledgerError
            };

            DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromSeconds(90));
            return Ok(payload);
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
        var now = DateTimeOffset.UtcNow;
        if (startDate.HasValue != endDate.HasValue)
            return BadRequest(new { error = "Özel tarih aralığı için startDate ve endDate birlikte gönderilmelidir." });

        var (start, end) = startDate.HasValue
            ? (startDate.Value, endDate!.Value)
            : period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now),
            "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)),
            null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
            _ => (DateTimeOffset.MinValue, DateTimeOffset.MinValue)
        };
        if (start == DateTimeOffset.MinValue) return BadRequest(new { error = "period today, this_month veya last_month olmalıdır." });
        if (start > end) return BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });

        var cacheKey = $"fin-orders:{resolvedShopId}:{start:yyyyMMddHHmm}:{end:yyyyMMddHHmm}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        try
        {
            var receipts = await _etsyClient.GetShopReceiptsAsync(resolvedShopId, start, end, cancellationToken);
            var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, resolvedShopId, start, end, cancellationToken);
            var costs = await _repository.GetOrderCostsAsync(resolvedShopId, cancellationToken);

            var rate = 49.16m;
            try
            {
                var p = await _repository.GetBankPayoutsAsync(resolvedShopId, now.AddDays(-30), now, cancellationToken);
                var latestRate = p.FirstOrDefault(x => x.ExchangeRateToTry.HasValue && x.ExchangeRateToTry.Value > 30)?.ExchangeRateToTry;
                if (latestRate.HasValue) rate = latestRate.Value;
            }
            catch { }

            var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(receipts, ledgerEntries, costs, rate);
            var activeRows = rows.Where(r => r.OrderStatus != "canceled").ToList();

            var payload = new
            {
                orders = rows,
                count = rows.Count,
                totalOrderProfitUsd = activeRows.Sum(r => r.NetProfitUsd),
                totalOrderProfitTry = activeRows.Sum(r => r.NetProfitTry),
                exchangeRateUsed = rate,
                ledgerOk = ledgerError is null,
                ledgerWarning = ledgerError
            };

            DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromMinutes(2));
            return Ok(payload);
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
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = nowUtc.ToOffset(TimeSpan.FromHours(3));
        var targetYear = nowLocal.Year;
        var targetMonth = nowLocal.Month;

        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var parsedYear) && int.TryParse(parts[1], out var parsedMonth) && parsedMonth is >= 1 and <= 12)
            {
                targetYear = parsedYear;
                targetMonth = parsedMonth;
            }
            else
            {
                return BadRequest(new { error = "month parametresi YYYY-MM biciminde olmalidir." });
            }
        }

        var monthKey = $"{targetYear:D4}-{targetMonth:D2}";
        var cacheKey = $"daily-series:{resolvedShopId}:{monthKey}";
        if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
            return Ok(cachedPayload);

        try
        {
            var monthStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.FromHours(3));
            var monthEnd = monthStart.AddMonths(1);
            var fetchEnd = monthEnd < nowUtc ? monthEnd : nowUtc;
            if (fetchEnd <= monthStart)
            {
                return Ok(new { month = monthKey, labels = Array.Empty<string>(), grossSales = Array.Empty<decimal>(), netProfit = Array.Empty<decimal>(), topProductTitle = (string?)null, topProductRevenueUsd = 0m, orderCount = 0 });
            }

            var receipts = await _etsyClient.GetShopReceiptsAsync(resolvedShopId, monthStart, fetchEnd, cancellationToken);
            var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, resolvedShopId, monthStart, fetchEnd, cancellationToken);
            var costs = await _repository.GetOrderCostsAsync(resolvedShopId, cancellationToken);
            var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledgerEntries, costs);
            var series = EtsyLiveDashboardCalculator.BuildDailySeries(rows, targetYear, targetMonth, nowUtc);
            var topProduct = EtsyLiveDashboardCalculator.PickTopProduct(rows);

            var grossSeries = series.GrossSales;
            var netSeries = series.NetProfit;
            var seriesSource = "orders-fallback";
            var utcMonthStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.Zero);
            var utcMonthEnd = utcMonthStart.AddMonths(1);
            var utcFetchEnd = utcMonthEnd < nowUtc ? utcMonthEnd : nowUtc;
            var live = await _ledgerReport.GetLiveReportAsync(resolvedShopId, utcMonthStart, utcFetchEnd, cancellationToken);
            if (live.LedgerOk)
            {
                grossSeries = new decimal[series.Labels.Length];
                netSeries = new decimal[series.Labels.Length];
                foreach (var day in live.Daily)
                {
                    if (day.Date.Year == targetYear && day.Date.Month == targetMonth && day.Date.Day >= 1 && day.Date.Day <= series.Labels.Length)
                    {
                        grossSeries[day.Date.Day - 1] = day.GrossSales;
                        netSeries[day.Date.Day - 1] = day.RealNetProfitUsd;
                    }
                }
                seriesSource = live.Source;
            }

            var payload = new
            {
                month = monthKey,
                labels = series.Labels,
                grossSales = grossSeries,
                netProfit = netSeries,
                topProductTitle = topProduct.Title,
                topProductRevenueUsd = topProduct.Revenue,
                orderCount = rows.Count,
                ledgerOk = live.LedgerOk,
                ledgerWarning = live.LedgerOk ? null : (live.Warning ?? ledgerError),
                source = seriesSource
            };

            DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromSeconds(90));
            return Ok(payload);
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
    public IActionResult GetBankDeposits()
    {
        var now = DateTimeOffset.UtcNow;
        var sampleEntries = new List<RawDepositEntryInput>
        {
            new(101, 18472910, "deposit", 131.25m, -131.25m, "USD", "Etsy Payment Account Payout", now.AddDays(-1), 49.02m),
            new(102, 18410294, "deposit", 145.00m, -145.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-7), 48.95m),
            new(103, 18354890, "deposit", 120.50m, -120.50m, "USD", "Etsy Payment Account Payout", now.AddDays(-14), 48.80m),
            new(104, 18299102, "deposit", 128.00m, -128.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-21), 48.75m)
        };
        var summary = _bankService.CalculateMonthlyDeposits(sampleEntries, now.AddMonths(-1), now, _ => 49.02m, 49.02m);
        return Ok(summary);
    }

    [HttpGet("financial/summary")]
    [EndpointSummary("Hızlı Finansal Özet Tablosu")]
    public IActionResult GetFinancialSummary()
    {
        return Ok(new
        {
            period = "Eylül 2026",
            grossSales = 45261.97,
            etsyFees = 10173.80,
            netRevenue = 29674.83,
            productCosts = 9835.33,
            realNetProfit = 19839.50,
            bankPayoutsTotal = 25701.31
        });
    }
}
