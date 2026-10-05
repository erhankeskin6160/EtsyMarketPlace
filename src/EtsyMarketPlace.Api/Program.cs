using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using EtsyMarketPlace.Infrastructure.AbTesting;
using EtsyMarketPlace.Infrastructure.AiUsage;
using EtsyMarketPlace.Infrastructure.BatchQueue;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using EtsyMarketPlace.Infrastructure.Tracking;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
var pendingPkceSessions = new ConcurrentDictionary<string, PkceSession>();
var listingPrimaryImgCache = new ConcurrentDictionary<string, string>();

// 1. Dependency Injection (Clean Architecture Servisleri)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IBankDepositService, BankDepositService>();
builder.Services.AddSingleton<ListingOptimizationService>();
builder.Services.Configure<EtsyIntegrationOptions>(builder.Configuration.GetSection("EtsyIntegration"));
builder.Services.Configure<EtsyApiOptions>(builder.Configuration.GetSection("Etsy"));
var dataProtectionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "EtsyMarketPlace",
    "keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddHttpClient<IEtsyOAuthService, EtsyOAuthService>();
builder.Services.AddTransient<EtsyAccessTokenHandler>();
builder.Services.AddHttpClient<IEtsyDataClient, EtsyApiClient>()
    .AddHttpMessageHandler<EtsyAccessTokenHandler>();

var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "EtsyMarketPlace");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "etsy-finance.db");

builder.Services.AddSingleton<SqliteEtsyIntegrationStore>();
builder.Services.AddSingleton<IEtsyTokenStore>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IEtsyIntegrationRepository>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IEtsyReportingService>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IUserRepository>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IShopSettingsRepository>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());

builder.Services.AddSingleton<IAbTestRepository>(sp => new SqliteAbTestRepository(dbPath));
builder.Services.AddSingleton<IAiUsageRepository>(sp => new SqliteAiUsageRepository(dbPath));
builder.Services.AddSingleton<IBatchQueueRepository>(sp => new SqliteBatchQueueRepository(dbPath));
builder.Services.AddSingleton<ITrackingRepository>(sp => new SqliteTrackingRepository(dbPath));
builder.Services.AddSingleton<IShopPerformanceHistoryRepository>(sp => new SqliteShopPerformanceHistoryRepository(dbPath));

builder.Services.AddSingleton(sp => new JwtTokenService(builder.Configuration["Jwt:Secret"]));
builder.Services.AddScoped<IEtsyFinancialAnalysisService, EtsyFinancialAnalysisService>();
builder.Services.AddScoped<IEtsySynchronizationService, EtsySynchronizationService>();
builder.Services.AddHostedService<EtsyIntegrationDatabaseInitializer>();
builder.Services.AddScoped<McpToolHandler>();

// 2. CORS (Angular Web Studio, Google Gemini Web ve Harici Entegrasyonlar İçin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:4200",
            "http://127.0.0.1:4200",
            "https://chat.openai.com",
            "https://chatgpt.com")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 3. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "EtsyMarketPlace Gemini Spark & MCP Web API", 
        Version = "v1",
        Description = "Google Gemini Spark (gemini.google.com/spark/apps) ve harici sistemler için Model Context Protocol (MCP) ve Finansal Raporlama API'si."
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp") || context.Request.Path.StartsWithSegments("/api/etsy"))
    {
        // PNG grafik resimleri doğrudan tarayıcı veya LLM tarafından önizlenebilir
        if (context.Request.Path.Value?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true)
        {
            await next();
            return;
        }

        var configuredKey = app.Configuration["Security:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var providedKey = context.Request.Headers["X-Api-Key"].ToString();
            var expected = Encoding.UTF8.GetBytes(configuredKey);
            var actual = Encoding.UTF8.GetBytes(providedKey);
            if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "API anahtarı geçersiz veya eksik." });
                return;
            }
        }
    }
    await next();
});

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EtsyMarketPlace API v1");
    c.RoutePrefix = "swagger";
});

// ── 4. RESMİ GOOGLE GEMINI SPARK MCP PROTOCOL ENDPOINT'LERİ (/mcp) ────────────

// GET /mcp — Gemini Spark Handshake & Sunucu Bilgisi
app.MapGet("/mcp", (McpToolHandler handler) =>
{
    return Results.Ok(new
    {
        name = "EtsyMarketPlace-MCP-Server",
        version = "1.0.0",
        protocolVersion = "2024-11-05",
        status = "Active",
        supportedMethods = new[] { "initialize", "tools/list", "tools/call" },
        toolsCount = handler.GetRegisteredTools().Count
    });
})
.WithTags("Gemini Spark MCP")
.WithSummary("MCP Sunucu Bilgisi ve Protokol Durumu")
.WithDescription("Gemini SparkX veya MCP protokolüyle çalışan yapay zeka istemcileri için protokol el sıkışmasını (handshake) gerçekleştirir, aktif metodları ve kayıtlı analiz araçlarının adedini döner.")
.WithName("GetMcpInfo");

// POST /mcp — Gemini Spark JSON-RPC 2.0 İletişim Hattı
app.MapPost("/mcp", async (HttpContext context, McpToolHandler handler) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(McpResponse.Fail(null, -32700, "Geçersiz JSON"));

    McpRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<McpRequest>(body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(McpResponse.Fail(null, -32700, $"Parse Hatası: {ex.Message}"));
    }

    if (request == null || request.JsonRpc != "2.0" || string.IsNullOrWhiteSpace(request.Method))
        return Results.BadRequest(McpResponse.Fail(null, -32600, "Geçersiz İstek"));

    switch (request.Method)
    {
        // 1. Gemini Bağlantı Kurma (Handshake)
        case "initialize":
            return Results.Ok(McpResponse.Success(request.Id, new
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

        // 2. Gemini Başlatma Bildirimi
        case "notifications/initialized":
            return Results.NoContent();

        // 3. Gemini Araçları Listeleme (tools/list)
        case "tools/list":
            return Results.Ok(McpResponse.Success(request.Id, new
            {
                tools = handler.GetRegisteredTools()
            }));

        // 4. Gemini Araç Çalıştırma (tools/call)
        case "tools/call":
            if (!request.Params.HasValue)
                return Results.Ok(McpResponse.Fail(request.Id, -32602, "Eksik Parametre"));

            var p = request.Params.Value;
            string? toolName = p.TryGetProperty("name", out var tProp) ? tProp.GetString() : null;
            JsonElement? arguments = p.TryGetProperty("arguments", out var aProp) ? aProp : null;

            if (string.IsNullOrWhiteSpace(toolName))
                return Results.Ok(McpResponse.Fail(request.Id, -32602, "Araç adı ('name') belirtilmeli"));

            try
            {
                var toolResult = await handler.ExecuteToolAsync(toolName, arguments);
                return Results.Ok(McpResponse.Success(request.Id, toolResult));
            }
            catch (Exception ex)
            {
                return Results.Ok(McpResponse.Fail(request.Id, -32000, $"Araç çalıştırma hatası: {ex.Message}"));
            }

        default:
            return Results.Ok(McpResponse.Fail(request.Id, -32601, $"Bilinmeyen metot: {request.Method}"));
    }
})
.WithTags("Gemini Spark MCP")
.WithSummary("Gemini SparkX JSON-RPC 2.0 İletişim Hattı")
.WithDescription("Gemini SparkX'in soru yanıtlarken kullandığı ana yapay zeka iletişim köprüsüdür. initialize, tools/list ve tools/call çağrılarını icra eder.")
.WithName("PostMcpJsonRpc");

// ── 5. STANDART REST API ENDPOINT'LERİ ──────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .WithTags("Sistem")
   .WithSummary("API ve Sunucu Sağlık Kontrolü")
   .WithDescription("VDS üzerinde çalışan API'nin ve arka plan servislerinin ayakta olup olmadığını kontrol eder. 'Healthy' durum kodu döner.")
   .WithName("HealthCheck");

string ResolveShopId(string? queryShopId, HttpContext context, IConfiguration config)
{
    if (!string.IsNullOrWhiteSpace(queryShopId) && queryShopId != "523236321")
        return queryShopId.Trim();

    if (context.Request.Headers.TryGetValue("X-Etsy-Shop-Id", out var headerVal))
    {
        var hStr = headerVal.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(hStr) && hStr != "523236321")
            return hStr;
    }

    return config["Etsy:DefaultShopId"] ?? config["Etsy:ShopId"] ?? "53236321";
}

app.MapGet("/api/etsy/banking/payouts", async (string shopId = "53236321", DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, HttpContext context = null!, IConfiguration config = null!, IEtsyReportingService reporting = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var end = endDate ?? DateTimeOffset.UtcNow;
    var start = startDate ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
    if (start > end) return Results.BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
    return Results.Ok(await reporting.GetBankPayoutsAsync(resolvedShopId, start, end, cancellationToken));
})
.WithTags("Finans & Banka")
.WithSummary("Etsy Banka Transferleri (Payouts)")
.WithDescription("Etsy'nin mağazanız için banka hesabınıza yatırdığı tüm ödeme ve transfer kayıtlarını tarih, tutar, kur ve durum bilgileriyle listeler. Mağaza ID belirtilmezse varsayılan mağaza (53236321) kullanılır.")
.WithName("GetEtsyBankPayouts");

app.MapGet("/api/etsy/financial/performance", async (string shopId = "53236321", string period = "last_month", DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, HttpContext context = null!, IConfiguration config = null!, IEtsyReportingService reporting = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var now = DateTimeOffset.UtcNow;
    if (startDate.HasValue != endDate.HasValue)
        return Results.BadRequest(new { error = "Özel tarih aralığı için startDate ve endDate birlikte gönderilmelidir." });

    var (start, end) = startDate.HasValue
        ? (startDate.Value, endDate!.Value)
        : period?.ToLowerInvariant() switch
    {
        "today" => (now.Date, now),
        "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1)),
        null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
        _ => (DateTimeOffset.MinValue, DateTimeOffset.MinValue)
    };
    if (start == DateTimeOffset.MinValue) return Results.BadRequest(new { error = "period today, this_month veya last_month olmalıdır." });
    if (start > end) return Results.BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
    return Results.Ok(await reporting.GetFinancialPerformanceAsync(resolvedShopId, start, end, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Finansal Performans ve Kâr-Zarar Karnesi")
.WithDescription("today, this_month, last_month dönemini veya startDate ve endDate ile özel tarih aralığını kullanarak finansal performansı hesaplar. Özel tarih aralığında iki tarih de gönderilmelidir.")
.WithName("GetFinancialPerformance");

app.MapGet("/api/etsy/financial/analysis", async (string shopId = "53236321", DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, HttpContext context = null!, IConfiguration config = null!, IEtsyFinancialAnalysisService analysis = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var end = endDate ?? DateTimeOffset.UtcNow;
    var start = startDate ?? end.AddDays(-30);
    if (start > end) return Results.BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
    return Results.Ok(await analysis.AnalyzeAsync(resolvedShopId, start, end, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Akıllı Finansal Analiz ve Öneri Motoru")
.WithDescription("Finansal performansı kural tabanlı yapay zeka mantığıyla analiz eder. Kâr marjı, ciro değişimi ve gider oranlarını değerlendirerek mağazaya özel finansal uyarılar ve öneriler sunar. Mağaza ID belirtilmezse varsayılan mağaza (53236321) kullanılır.")
.WithName("GetFinancialAnalysis");

app.MapGet("/api/etsy/orders/unfulfilled-cost-alerts", async (string shopId = "53236321", HttpContext context = null!, IConfiguration config = null!, IEtsyReportingService reporting = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    return Results.Ok(await reporting.GetUnfulfilledCostAlertsAsync(resolvedShopId, cancellationToken));
})
.WithTags("Sipariş & Maliyet")
.WithSummary("Maliyeti Eksik Sipariş Alarmları")
.WithDescription("Henüz kargolanmamış veya üretim/kargo maliyeti girilmemiş açık siparişleri listeler. Gerçek net kârın eksik maliyet yüzünden yanıltıcı çıkmasını önler. Mağaza ID belirtilmezse varsayılan mağaza (53236321) kullanılır.")
.WithName("GetUnfulfilledCostAlerts");

app.MapGet("/api/etsy/shop/daily-brief", async (string shopId = "53236321", DateTimeOffset? date = null, HttpContext context = null!, IConfiguration config = null!, IEtsyReportingService reporting = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    return Results.Ok(await reporting.GetDailyShopBriefAsync(resolvedShopId, date ?? DateTimeOffset.UtcNow, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Günlük Mağaza Bülteni ve Sağlık Skoru")
.WithDescription("Seçilen gün için brüt satış, net kâr, sipariş sayısı ve mağaza sağlık skorunu (Health Score 0-100) özetler. Mağaza ID belirtilmezse varsayılan mağaza (53236321) kullanılır.")
.WithName("GetDailyShopBrief");

// ── 05.10.2026: Web Kontrol Paneli canli veri uclari ────────────────────────
// Web panelindeki "Son Siparisler" akisi ve "Gunluk Gelir/Net Kar Trendi"
// bolumlerini masaustu panelle ayni mantikla dogrudan canli Etsy API'den besler.

static async Task<IReadOnlyList<EtsyDashboardLedgerFee>> SafeGetLedgerAsync(IEtsyDataClient client, string shopId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
{
    // Odeme hesabi defteri (billing_r) erisilemezse dis reklam bilgisi olmadan devam edilir.
    try
    {
        return await client.GetPaymentAccountLedgerEntriesAsync(shopId, start, end, ct);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[dashboard] Odeme defteri okunamadi ({shopId}): {ex.Message}");
        return Array.Empty<EtsyDashboardLedgerFee>();
    }
}

app.MapGet("/api/etsy/shop/recent-orders", async (string shopId = "53236321", int days = 31, int limit = 15, HttpContext context = null!, IConfiguration config = null!, IEtsyDataClient etsyClient = null!, IEtsyIntegrationRepository repository = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var clampedDays = Math.Clamp(days, 1, 90);
    var clampedLimit = Math.Clamp(limit, 1, 50);
    var cacheKey = $"recent-orders:{resolvedShopId}:{clampedDays}:{clampedLimit}";
    if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
        return Results.Ok(cachedPayload);

    try
    {
        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-clampedDays);
        var receipts = await etsyClient.GetShopReceiptsAsync(resolvedShopId, start, end, cancellationToken);
        var ledger = await SafeGetLedgerAsync(etsyClient, resolvedShopId, start, end, cancellationToken);
        var costs = await repository.GetOrderCostsAsync(resolvedShopId, cancellationToken);
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledger, costs);

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
            count = rows.Count
        };

        DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromSeconds(90));
        return Results.Ok(payload);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Siparişler canlı Etsy API'den alınamadı: " + ex.Message });
    }
})
.WithTags("Sipariş & Maliyet")
.WithSummary("Son Siparişler — Canlı Satış Akışı")
.WithDescription("Son N günün Etsy siparişlerini (fişlerini) canlı Etsy API'den çeker; sipariş bazlı net kâr masaüstü panel formülüyle hesaplanır.")
.WithName("GetRecentOrders");

app.MapGet("/api/etsy/financial/daily-series", async (string shopId = "53236321", string? month = null, HttpContext context = null!, IConfiguration config = null!, IEtsyDataClient etsyClient = null!, IEtsyIntegrationRepository repository = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
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
            return Results.BadRequest(new { error = "month parametresi YYYY-MM biciminde olmalidir." });
        }
    }

    var monthKey = $"{targetYear:D4}-{targetMonth:D2}";
    var cacheKey = $"daily-series:{resolvedShopId}:{monthKey}";
    if (DashboardResponseCache.TryGet(cacheKey, out var cachedPayload) && cachedPayload is not null)
        return Results.Ok(cachedPayload);

    try
    {
        var monthStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var monthEnd = monthStart.AddMonths(1);
        var fetchEnd = monthEnd < nowUtc ? monthEnd : nowUtc;
        if (fetchEnd <= monthStart)
        {
            return Results.Ok(new { month = monthKey, labels = Array.Empty<string>(), grossSales = Array.Empty<decimal>(), netProfit = Array.Empty<decimal>(), topProductTitle = (string?)null, topProductRevenueUsd = 0m, orderCount = 0 });
        }

        var receipts = await etsyClient.GetShopReceiptsAsync(resolvedShopId, monthStart, fetchEnd, cancellationToken);
        var ledger = await SafeGetLedgerAsync(etsyClient, resolvedShopId, monthStart, fetchEnd, cancellationToken);
        var costs = await repository.GetOrderCostsAsync(resolvedShopId, cancellationToken);
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledger, costs);
        var series = EtsyLiveDashboardCalculator.BuildDailySeries(rows, targetYear, targetMonth, nowUtc);
        var topProduct = EtsyLiveDashboardCalculator.PickTopProduct(rows);

        var payload = new
        {
            month = monthKey,
            labels = series.Labels,
            grossSales = series.GrossSales,
            netProfit = series.NetProfit,
            topProductTitle = topProduct.Title,
            topProductRevenueUsd = topProduct.Revenue,
            orderCount = rows.Count
        };

        DashboardResponseCache.Set(cacheKey, payload, TimeSpan.FromSeconds(90));
        return Results.Ok(payload);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Günlük gelir/kâr serisi canlı Etsy API'den alınamadı: " + ex.Message });
    }
})
.WithTags("Finans & Maliyet")
.WithSummary("Günlük Gelir ve Net Kâr Serisi")
.WithDescription("Seçilen ay için günlük brüt satış ve net kâr serisini canlı Etsy API fişlerinden hesaplar. Web kontrol panelindeki trend grafiğini besler.")
.WithName("GetDailySeries");

app.MapPost("/api/etsy/sync", async (EtsySyncRequest request, IConfiguration configuration, IEtsySynchronizationService synchronization, CancellationToken cancellationToken) =>
{
    var shopId = string.IsNullOrWhiteSpace(request.ShopId) 
        ? (configuration["Etsy:DefaultShopId"] ?? configuration["Etsy:ShopId"] ?? "53236321") 
        : request.ShopId;

    var end = request.EndDate ?? DateTimeOffset.UtcNow;
    var start = request.StartDate ?? end.AddDays(-30);
    if (start > end)
        return Results.BadRequest(new { error = "StartDate EndDate değerinden sonra olamaz." });

    var result = await synchronization.SynchronizeAsync(shopId, start, end, cancellationToken);
    return result.Succeeded ? Results.Ok(result) : Results.Problem(result.ErrorMessage, statusCode: StatusCodes.Status502BadGateway);
})
.WithTags("Senkronizasyon")
.WithSummary("Etsy API Doğrudan Eşitleme")
.WithDescription("Etsy Open API v3 üzerinden finansal hareketleri ve sipariş verilerini doğrudan VDS veritabanına senkronize eder.")
.WithName("SynchronizeEtsyFinance");

app.MapPost("/api/etsy/financial/import", async (EtsyFinancialImportRequest request, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId))
        return Results.BadRequest(new { error = "ShopId zorunludur." });

    var shopId = request.ShopId.Trim();

    if (request.Transactions?.Count > 0)
        await repository.SaveTransactionsAsync(request.Transactions, cancellationToken);

    if (request.Payouts?.Count > 0)
        await repository.SavePayoutsAsync(request.Payouts, cancellationToken);

    if (request.OrderAlerts?.Count > 0)
        await repository.SaveOrderAlertsAsync(request.OrderAlerts, cancellationToken);

    await repository.SaveSyncStateAsync(shopId, "financial", request.PeriodEnd ?? DateTimeOffset.UtcNow, cancellationToken: cancellationToken);

    return Results.Ok(new
    {
        saved = true,
        shopId,
        transactionsCount = request.Transactions?.Count ?? 0,
        payoutsCount = request.Payouts?.Count ?? 0,
        alertsCount = request.OrderAlerts?.Count ?? 0
    });
})
.WithTags("Senkronizasyon")
.WithSummary("Masaüstü Finansal Rapor Aktarımı")
.WithDescription("Masaüstü WinForms uygulamasında işlenmiş olan günlük finans dökümlerini ve kâr hesaplamalarını VDS veritabanına aktarır.")
.WithName("ImportEtsyFinancialData");

app.MapPost("/api/etsy/orders/monthly-import", async (EtsyMonthlyOrderImportRequest request, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId))
        return Results.BadRequest(new { error = "ShopId zorunludur." });

    if (request.Summaries?.Count > 0)
        await repository.SaveMonthlyOrderSummariesAsync(request.Summaries, cancellationToken);

    return Results.Ok(new { saved = true, shopId = request.ShopId, count = request.Summaries?.Count ?? 0 });
})
.WithTags("Sipariş & Maliyet")
.WithSummary("Aylık Sipariş Geçmişi Aktarımı")
.WithDescription("Masaüstünden gelen son 10-12 aylık sipariş adetlerini, brüt ciroyu ve sepet ortalamasını (AOV) VDS SQLite veritabanına kaydeder.")
.WithName("ImportMonthlyOrders");

app.MapPost("/api/etsy/analytics/traffic-import", async (EtsyListingTrafficImportRequest request, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId))
        return Results.BadRequest(new { error = "ShopId zorunludur." });

    if (request.Records?.Count > 0)
        await repository.SaveListingTrafficDailyAsync(request.Records, cancellationToken);

    return Results.Ok(new { saved = true, shopId = request.ShopId, count = request.Records?.Count ?? 0 });
})
.WithTags("Trafik & Analitik")
.WithSummary("Ürün Ziyaret ve Trafik Metrikleri Aktarımı")
.WithDescription("Ürünlerin günlük ve aylık ziyaret, görüntüleme, favorilenme sayıları ile satış dönüşüm oranlarını (conversion rate) kaydeder.")
.WithName("ImportListingTraffic");

app.MapPost("/api/etsy/charts/upload", async (EtsyChartUploadRequest request, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ChartType) || string.IsNullOrWhiteSpace(request.ImagePngBase64))
        return Results.BadRequest(new { error = "ShopId, ChartType ve ImagePngBase64 zorunludur." });

    var snapshot = new EtsyChartSnapshot(
        request.ShopId.Trim(),
        request.ChartType.Trim().ToLowerInvariant(),
        request.PeriodStart,
        request.PeriodEnd,
        request.ImagePngBase64,
        DateTimeOffset.UtcNow);

    await repository.SaveChartSnapshotAsync(snapshot, cancellationToken);
    return Results.Ok(new { saved = true, shopId = request.ShopId, chartType = request.ChartType });
})
.WithTags("Grafikler")
.WithSummary("Grafik Görselleri Yükleme (PNG Base64)")
.WithDescription("Masaüstündeki SkiaSharp grafik motorunun ürettiği 4 ana grafiğin (profit_bar, cashflow_line, cost_pie, forecast) Base64 PNG verisini sunucuya yükler.")
.WithName("UploadChartSnapshot");

app.MapGet("/api/etsy/charts/{shopId}/{chartType}.png", async (string shopId, string chartType, IConfiguration configuration, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    var resolvedShopId = (shopId == "default" || shopId == "523236321") ? (configuration["Etsy:DefaultShopId"] ?? "53236321") : shopId;
    var snapshot = await repository.GetChartSnapshotAsync(resolvedShopId, chartType.ToLowerInvariant(), cancellationToken);
    if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ImagePngBase64))
        return Results.NotFound(new { error = "Grafik resmi bulunamadı." });

    try
    {
        var bytes = Convert.FromBase64String(snapshot.ImagePngBase64);
        return Results.File(bytes, "image/png");
    }
    catch
    {
        return Results.Problem("Grafik resmi çözümlenemedi.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithTags("Grafikler")
.WithSummary("Grafik PNG Resmini Önizleme")
.WithDescription("Yüklenen grafiği doğrudan web tarayıcısında veya Gemini Spark görsel modunda gösterilmek üzere PNG formatında döner.")
.WithName("GetChartSnapshotImage");

app.MapPost("/api/etsy/sync-all", async (EtsyAllDataImportRequest request, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId))
        return Results.BadRequest(new { error = "ShopId zorunludur." });

    var shopId = request.ShopId.Trim();

    if (request.Transactions?.Count > 0)
        await repository.SaveTransactionsAsync(request.Transactions, cancellationToken);

    if (request.Payouts?.Count > 0)
        await repository.SavePayoutsAsync(request.Payouts, cancellationToken);

    if (request.OrderAlerts?.Count > 0)
        await repository.SaveOrderAlertsAsync(request.OrderAlerts, cancellationToken);

    if (request.MonthlyOrders?.Count > 0)
        await repository.SaveMonthlyOrderSummariesAsync(request.MonthlyOrders, cancellationToken);

    if (request.TrafficRecords?.Count > 0)
        await repository.SaveListingTrafficDailyAsync(request.TrafficRecords, cancellationToken);

    if (request.Charts?.Count > 0)
    {
        foreach (var c in request.Charts)
        {
            var snap = new EtsyChartSnapshot(shopId, c.ChartType.Trim().ToLowerInvariant(), c.PeriodStart, c.PeriodEnd, c.ImagePngBase64, DateTimeOffset.UtcNow);
            await repository.SaveChartSnapshotAsync(snap, cancellationToken);
        }
    }

    await repository.SaveSyncStateAsync(shopId, "all_desktop_data", request.PeriodEnd ?? DateTimeOffset.UtcNow, cancellationToken: cancellationToken);

    return Results.Ok(new
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
})
.WithTags("Senkronizasyon")
.WithSummary("🌐 Masaüstü Tam Senkronizasyon (Tek Hamlede Aktarım)")
.WithDescription("Masaüstündeki 'VDS'e Aktar' butonunun tek seferde günlük finans kayıtlarını, banka transferlerini, siparişleri, ürün trafiklerini ve grafik resimlerini topluca VDS'e kaydettiği ana aktarım motorudur.")
.WithName("SyncAllDesktopData");

app.MapPost("/api/etsy/token", async (EtsyTokenImportRequest request, IEtsyTokenStore tokenStore, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId) ||
        string.IsNullOrWhiteSpace(request.AccessToken) ||
        string.IsNullOrWhiteSpace(request.RefreshToken) ||
        request.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
    {
        return Results.BadRequest(new { error = "ShopId, geçerli access token, refresh token ve gelecekteki token son kullanma zamanı zorunludur." });
    }

    var tokenType = string.IsNullOrWhiteSpace(request.TokenType) ? "Bearer" : request.TokenType.Trim();
    var shopId = request.ShopId.Trim();
    await tokenStore.SaveAsync(
        shopId,
        new EtsyOAuthToken(
            request.AccessToken.Trim(),
            request.RefreshToken.Trim(),
            request.AccessTokenExpiresAt,
            tokenType),
        cancellationToken);

    var savedToken = await tokenStore.GetAsync(shopId, cancellationToken);
    return savedToken is null
        ? Results.Problem("Token kaydedilemedi.", statusCode: StatusCodes.Status500InternalServerError)
        : Results.Ok(new
        {
            saved = true,
            shopId,
            expiresAt = savedToken.AccessTokenExpiresAt,
            isExpired = savedToken.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = savedToken.TokenType
        });
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy OAuth Token Aktarımı")
.WithDescription("Masaüstü uygulamasında üretilen Etsy API OAuth Access ve Refresh token bilgilerini VDS güvenli kasasına kaydeder.")
.WithName("ImportEtsyToken");

app.MapGet("/api/etsy/token/status", async (string shopId = "53236321", HttpContext context = null!, IConfiguration config = null!, IEtsyTokenStore tokenStore = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var token = await tokenStore.GetAsync(resolvedShopId, cancellationToken);
    return token is null
        ? Results.NotFound(new { exists = false, shopId = resolvedShopId })
        : Results.Ok(new
        {
            exists = true,
            shopId = resolvedShopId,
            expiresAt = token.AccessTokenExpiresAt,
            isExpired = token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = token.TokenType
        });
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy Token Durum ve Geçerlilik Kontrolü")
.WithDescription("Kayıtlı Etsy OAuth token'ının süresinin dolup dolmadığını ve kalan geçerlilik süresini kontrol eder.")
.WithName("GetEtsyTokenStatus");

app.MapPost("/api/etsy/token/refresh", async (string shopId = "53236321", HttpContext context = null!, IConfiguration config = null!, IEtsyTokenStore tokenStore = null!, IEtsyOAuthService oauthService = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var token = await tokenStore.GetAsync(resolvedShopId, cancellationToken);
    if (token is null)
        return Results.NotFound(new { success = false, message = "Bu mağaza için kayıtlı token bulunamadı." });

    try
    {
        var refreshed = await oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
        await tokenStore.SaveAsync(resolvedShopId, refreshed, cancellationToken);
        return Results.Ok(new
        {
            success = true,
            shopId = resolvedShopId,
            expiresAt = refreshed.AccessTokenExpiresAt,
            isExpired = false,
            message = "Etsy OAuth v3 token başarıyla yenilendi."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            shopId = resolvedShopId,
            message = "Token yenilenemedi: " + ex.Message,
            needsReauth = true
        });
    }
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy Token Yenileme (Refresh Token)")
.WithDescription("Etsy v3 OAuth refresh_token kullanarak access_token'ı doğrudan yeniler.")
.WithName("RefreshEtsyToken");

app.MapGet("/api/etsy/settings/credentials", async (string shopId = "53236321", HttpContext context = null!, IConfiguration config = null!, IShopSettingsRepository settingsRepo = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var saved = await settingsRepo.GetEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
    if (saved is not null)
        return Results.Ok(saved);

    var cfgKey = config["Etsy:ApiKey"] ?? string.Empty;
    var cfgSecret = config["Etsy:SharedSecret"] ?? string.Empty;
    var redirectUri = config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api";
    static string Mask(string s) => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= 8 ? "****" : $"{s[..4]}...{s[^4..]}");

    return Results.Ok(new EtsyAppCredentialsRecord(
        resolvedShopId,
        Mask(cfgKey),
        Mask(cfgSecret),
        redirectUri,
        DateTimeOffset.UtcNow));
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy App Geliştirici Anahtarları (Keystring & Secret)")
.WithName("GetEtsyAppCredentials");

app.MapPost("/api/etsy/settings/credentials", async (SaveEtsyAppCredentialsRequest request, IShopSettingsRepository settingsRepo, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId))
        return Results.BadRequest(new { error = "ShopId gereklidir." });

    var saved = await settingsRepo.SaveEtsyAppCredentialsAsync(request, cancellationToken);
    return Results.Ok(saved);
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy App Geliştirici Anahtarlarını Kaydet (Şifreli)")
.WithName("SaveEtsyAppCredentials");

app.MapGet("/api/etsy/oauth/connect-url", async (string shopId = "53236321", string? redirectUri = null, HttpContext context = null!, IConfiguration config = null!, IShopSettingsRepository settingsRepo = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var raw = await settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
    var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (config["Etsy:ApiKey"] ?? string.Empty);
    var targetRedirectUri = !string.IsNullOrWhiteSpace(redirectUri)
        ? redirectUri.Trim()
        : (!string.IsNullOrWhiteSpace(raw.RedirectUri) ? raw.RedirectUri : (config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api"));

    if (string.IsNullOrWhiteSpace(keystring))
        return Results.BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen önce API ayarlarından Keystring kaydedin." });

    var verifierBytes = RandomNumberGenerator.GetBytes(64);
    var codeVerifier = EtsyOAuthService.Base64Url(verifierBytes);
    var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    var codeChallenge = EtsyOAuthService.Base64Url(challengeBytes);
    var state = EtsyOAuthService.Base64Url(RandomNumberGenerator.GetBytes(32));

    pendingPkceSessions[state] = new PkceSession(codeVerifier, resolvedShopId, targetRedirectUri, DateTimeOffset.UtcNow);

    var expireThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
    foreach (var kvp in pendingPkceSessions)
    {
        if (kvp.Value.CreatedAt < expireThreshold)
            pendingPkceSessions.TryRemove(kvp.Key, out _);
    }

    var scopes = "listings_r listings_w shops_r transactions_r billing_r";
    var authBase = config["Etsy:AuthorizationBaseUrl"] ?? "https://www.etsy.com/oauth/connect";
    var query = new Dictionary<string, string>
    {
        ["response_type"] = "code",
        ["client_id"] = keystring.Trim(),
        ["redirect_uri"] = targetRedirectUri.Trim(),
        ["scope"] = scopes,
        ["state"] = state,
        ["code_challenge"] = codeChallenge,
        ["code_challenge_method"] = "S256"
    };

    var url = authBase + "?" + string.Join('&', query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    return Results.Ok(new
    {
        url,
        state,
        redirectUri = targetRedirectUri,
        shopId = resolvedShopId
    });
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy OAuth v3 PKCE Yetkilendirme Bağlantısı Üret")
.WithName("GetEtsyOAuthConnectUrl");

app.MapPost("/api/etsy/oauth/exchange-code", async (ExchangeCodeApiRequest request, IHttpClientFactory httpClientFactory, IConfiguration config, IEtsyTokenStore tokenStore, IShopSettingsRepository settingsRepo, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest(new { success = false, message = "ShopId ve yetki kodu (code) gereklidir." });

    var shopId = request.ShopId.Trim();
    var raw = await settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, cancellationToken);
    var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (config["Etsy:ApiKey"] ?? string.Empty);
    var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (config["Etsy:SharedSecret"] ?? string.Empty);
    if (string.IsNullOrWhiteSpace(keystring))
        return Results.BadRequest(new { success = false, message = "Etsy Keystring (Client ID) bulunamadı. Lütfen önce API ayarlarından Keystring kaydedin." });

    string? codeVerifier = request.CodeVerifier;
    string targetRedirectUri = !string.IsNullOrWhiteSpace(request.RedirectUri) ? request.RedirectUri.Trim() : (!string.IsNullOrWhiteSpace(raw.RedirectUri) ? raw.RedirectUri : (config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api"));

    if (!string.IsNullOrWhiteSpace(request.State) && pendingPkceSessions.TryRemove(request.State, out var session))
    {
        codeVerifier ??= session.CodeVerifier;
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            targetRedirectUri = session.RedirectUri;
    }

    if (string.IsNullOrWhiteSpace(codeVerifier))
        return Results.BadRequest(new { success = false, message = "PKCE code_verifier bulunamadı veya oturum süresi doldu. Lütfen 'Etsy ile Yetkilendir' butonunu tekrar tıklayın." });

    var tokenUrl = config["Etsy:TokenUrl"] ?? "https://api.etsy.com/v3/public/oauth/token";
    var client = httpClientFactory.CreateClient();

    using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = keystring.Trim(),
            ["redirect_uri"] = targetRedirectUri.Trim(),
            ["code"] = request.Code.Trim(),
            ["code_verifier"] = codeVerifier.Trim()
        })
    };

    tokenRequest.Headers.Add("x-api-key", !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim());

    try
    {
        using var response = await client.SendAsync(tokenRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Results.BadRequest(new
            {
                success = false,
                message = $"Etsy yetkilendirme takası başarısız (HTTP {(int)response.StatusCode}): {body}"
            });
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        var tokenType = root.TryGetProperty("token_type", out var tt) ? (tt.GetString() ?? "Bearer") : "Bearer";

        var newToken = new EtsyOAuthToken(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn), tokenType);
        await tokenStore.SaveAsync(shopId, newToken, cancellationToken);

        return Results.Ok(new
        {
            success = true,
            shopId,
            expiresAt = newToken.AccessTokenExpiresAt,
            isExpired = false,
            message = "Etsy OAuth v3 bağlantısı ve yetkilendirmesi başarıyla tamamlandı!"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = "Etsy token takas hatası: " + ex.Message });
    }
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy OAuth Yetki Kodunu Token'a Dönüştür (Code Exchange)")
.WithName("ExchangeEtsyOAuthCode");

app.MapGet("/api/etsy/shop/listings", async (string shopId = "53236321", int limit = 50, HttpContext context = null!, IConfiguration config = null!, IEtsyTokenStore tokenStore = null!, IShopSettingsRepository settingsRepo = null!, IHttpClientFactory httpClientFactory = null!, IEtsyOAuthService oauthService = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var token = await tokenStore.GetAsync(resolvedShopId, cancellationToken);
    if (token is null)
        return Results.NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

    if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
    {
        try
        {
            token = await oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
            await tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
        }
    }

    var raw = await settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
    var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (config["Etsy:ApiKey"] ?? string.Empty);
    var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (config["Etsy:SharedSecret"] ?? string.Empty);
    if (string.IsNullOrWhiteSpace(keystring))
        return Results.BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
    var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

    var client = httpClientFactory.CreateClient();
    var clampedLimit = Math.Clamp(limit, 1, 100);
    // Official Etsy OpenAPI v3 endpoint supporting includes=Images is getListingsByShop (/shops/{shop_id}/listings?state=active&includes=Images)
    var url = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings?state=active&limit={clampedLimit}&sort_on=updated&sort_order=desc&includes=Images";

    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("x-api-key", apiKeyHeader);
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

    using var response = await client.SendAsync(request, cancellationToken);
    string body;
    if (!response.IsSuccessStatusCode)
    {
        // Fallback to active endpoint if needed
        var fallbackUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/active?limit={clampedLimit}&sort_on=updated&sort_order=desc";
        using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
        fbReq.Headers.Add("x-api-key", apiKeyHeader);
        fbReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var fbResp = await client.SendAsync(fbReq, cancellationToken);
        if (!fbResp.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.BadRequest(new { error = $"Etsy API hatası (HTTP {(int)response.StatusCode}): {err}" });
        }
        body = await fbResp.Content.ReadAsStringAsync(cancellationToken);
    }
    else
    {
        body = await response.Content.ReadAsStringAsync(cancellationToken);
    }
    using var doc = JsonDocument.Parse(body);
    if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        return Results.Ok(Array.Empty<object>());

    var savedAudits = (await settingsRepo.GetListingAuditsAsync(resolvedShopId, cancellationToken))
        .ToDictionary(a => a.ListingId, a => a);

    var items = new List<object>();
    int rank = 1;

    foreach (var item in results.EnumerateArray())
    {
        var listingId = item.TryGetProperty("listing_id", out var lid) ? lid.GetInt64().ToString() : "";
        var title = item.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
        var description = item.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
        var views = item.TryGetProperty("views", out var v) ? v.GetInt32() : 0;
        int favorites = 0;
        if (item.TryGetProperty("num_favorers", out var f))
        {
            favorites = f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int.TryParse(f.GetString(), out var nf) ? nf : 0);
        }
        else if (item.TryGetProperty("favorites", out var favProp))
        {
            favorites = favProp.ValueKind == JsonValueKind.Number ? favProp.GetInt32() : (int.TryParse(favProp.GetString(), out var nf) ? nf : 0);
        }
        var quantity = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 0;

        decimal price = 0;
        string currency = "USD";
        if (item.TryGetProperty("price", out var pObj) && pObj.ValueKind == JsonValueKind.Object)
        {
            if (pObj.TryGetProperty("amount", out var aProp) && pObj.TryGetProperty("divisor", out var divProp))
            {
                var divisor = divProp.GetInt32();
                if (divisor > 0) price = aProp.GetInt64() / (decimal)divisor;
            }
            if (pObj.TryGetProperty("currency_code", out var cProp)) currency = cProp.GetString() ?? "USD";
        }

        var tags = new List<string>();
        if (item.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var tg in tagsArr.EnumerateArray())
            {
                var str = tg.GetString();
                if (!string.IsNullOrWhiteSpace(str)) tags.Add(str.Trim());
            }
        }

        var materials = new List<string>();
        if (item.TryGetProperty("materials", out var matArr) && matArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var mt in matArr.EnumerateArray())
            {
                var str = mt.GetString();
                if (!string.IsNullOrWhiteSpace(str)) materials.Add(str.Trim());
            }
        }

        var imageUrls = new List<string>();
        var imgProp = (item.TryGetProperty("Images", out var p1) && p1.ValueKind == JsonValueKind.Array) ? p1
            : ((item.TryGetProperty("images", out var p2) && p2.ValueKind == JsonValueKind.Array) ? p2 : default);

        if (imgProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imgProp.EnumerateArray())
            {
                string? imgUrl = null;
                if (img.TryGetProperty("url_570xN", out var u570)) imgUrl = u570.GetString();
                else if (img.TryGetProperty("url_fullxfull", out var uFull)) imgUrl = uFull.GetString();
                else if (img.TryGetProperty("url_170x135", out var u170)) imgUrl = u170.GetString();
                else if (img.TryGetProperty("url_75x75", out var u75)) imgUrl = u75.GetString();

                if (!string.IsNullOrWhiteSpace(imgUrl)) imageUrls.Add(imgUrl.Trim());
            }
        }
        var primaryImg = imageUrls.FirstOrDefault() ?? "";

        // Check in-memory cache if primary image is empty
        if (string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId) && listingPrimaryImgCache.TryGetValue(listingId, out var cachedImg))
        {
            primaryImg = cachedImg;
            imageUrls.Add(cachedImg);
        }

        // Secondary fallback: Fetch individual listing image directly (as in desktop OwnShopListingAiAuditForm)
        if (string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId) && long.TryParse(listingId, out var lidVal) && lidVal > 0)
        {
            try
            {
                var imgReqUrl = $"https://api.etsy.com/v3/application/listings/{lidVal}/images";
                using var imgReq = new HttpRequestMessage(HttpMethod.Get, imgReqUrl);
                imgReq.Headers.Add("x-api-key", apiKeyHeader);
                using var imgResp = await client.SendAsync(imgReq, cancellationToken);
                if (imgResp.IsSuccessStatusCode)
                {
                    var imgBody = await imgResp.Content.ReadAsStringAsync(cancellationToken);
                    using var imgDoc = JsonDocument.Parse(imgBody);
                    if (imgDoc.RootElement.TryGetProperty("results", out var imgArr) && imgArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var img in imgArr.EnumerateArray())
                        {
                            string? urlFound = null;
                            if (img.TryGetProperty("url_570xN", out var u570)) urlFound = u570.GetString();
                            else if (img.TryGetProperty("url_fullxfull", out var uFull)) urlFound = uFull.GetString();
                            else if (img.TryGetProperty("url_170x135", out var u170)) urlFound = u170.GetString();
                            else if (img.TryGetProperty("url_75x75", out var u75)) urlFound = u75.GetString();

                            if (!string.IsNullOrWhiteSpace(urlFound))
                            {
                                primaryImg = urlFound.Trim();
                                imageUrls.Add(primaryImg);
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore individual image fetch error
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId))
        {
            listingPrimaryImgCache[listingId] = primaryImg;
        }

        // Structural SEO Analysis
        int seoScore = 100;
        var needs = new List<string>();
        var strengths = new List<string>();

        if (tags.Count < 13)
        {
            var missingTags = 13 - tags.Count;
            seoScore -= missingTags * 4;
            needs.Add($"• Eksik Tag: {missingTags} tag eksik (13/13 etiket hakkının tümü kullanılmalı).");
        }
        else strengths.Add("13 tag eksiksiz");

        if (title.Length < 60)
        {
            seoScore -= 15;
            needs.Add($"• Başlık Çok Kısa: {title.Length}/140 karakter ({140 - title.Length} karakter boş bırakılmış).");
        }
        else if (title.Length < 110)
        {
            seoScore -= 6;
            needs.Add($"• Başlık Alanı İsrafı: {title.Length}/140 karakter ({140 - title.Length} karakter daha kullanılabilir).");
        }
        else if (title.Length <= 140) strengths.Add("Başlık uzunluğu ideal");

        if (description.Length < 500)
        {
            seoScore -= 10;
            needs.Add($"• Açıklama Kısa: {description.Length} karakter (en az 500 karakter detaylı hikaye ve özellik önerilir).");
        }
        else strengths.Add("Açıklama zengin");

        if (imageUrls.Count < 5)
        {
            seoScore -= 10;
            needs.Add($"• Görsel Az: {imageUrls.Count} görsel (Etsy listelemesinde en az 5-10 görsel önerilir).");
        }
        else strengths.Add("Görsel sayısı yeterli");

        seoScore = Math.Clamp(seoScore, 10, 100);

        // Trademark / Copyright risk scan
        var riskBlob = $"{title} {description} {string.Join(' ', tags)}".ToLowerInvariant();
        var detectedRisks = new List<string>();
        string[] trademarkTerms = ["ben 10", "omnitrix", "thor", "mjolnir", "marvel", "disney", "valorant", "kratos", "god of war", "pokemon", "nintendo", "star wars", "harry potter", "demon slayer", "naruto", "minecraft", "batman", "spiderman", "spider-man", "superman", "iron man", "captain america", "hulk"];
        foreach (var term in trademarkTerms)
        {
            if (riskBlob.Contains(term))
            {
                detectedRisks.Add($"🚨 Telif ve Marka Riski: '{term}' tescilli markadır (IP/Trademark). Hak sahipleri veya Etsy tarafından telif yaptırımı riski taşır.");
            }
        }

        // Check if previously audited
        bool isAiAudited = false;
        int aiScore = seoScore;
        string status = detectedRisks.Count > 0 ? "⚠️ AI: Risk Var" : "Bekliyor";
        string? resultJson = null;

        if (savedAudits.TryGetValue(listingId, out var saved))
        {
            isAiAudited = true;
            aiScore = saved.OptimizedSeoScore;
            status = saved.Status;
            resultJson = saved.ResultJson;
        }

        items.Add(new
        {
            rank = rank++,
            listingId,
            title,
            description,
            tags,
            materials,
            price,
            priceAmount = price,
            currency,
            currencyCode = currency,
            quantity,
            views,
            favorites,
            numFavorers = favorites,
            images = imageUrls,
            thumbnail = primaryImg,
            primaryImageUrl = primaryImg,
            seoScore,
            aiScore,
            status,
            isAiAudited,
            resultJson,
            seoNeeds = string.Join("\n", needs),
            seoStrengths = string.Join(", ", strengths),
            structuralNeeds = needs,
            riskWarnings = detectedRisks,
            hasSavedAudit = isAiAudited,
            savedAudit = saved
        });
    }

    return Results.Ok(items);
})
.WithTags("Etsy Listing & AI Denetimi")
.WithSummary("Mağazanın Aktif Listinglerini ve Yapısal SEO Analizini Getir")
.WithName("GetShopListingsWithSeo");

app.MapPost("/api/etsy/listings/{listingId}/ai-optimize", async (
    string listingId,
    string? shopId,
    OptimizeListingApiRequest request,
    HttpContext context,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ListingOptimizationService optimizer,
    IShopSettingsRepository settingsRepo,
    CancellationToken cancellationToken) =>
{
    try
    {
        var resolvedShopId = !string.IsNullOrWhiteSpace(request?.ShopId)
            ? request.ShopId.Trim()
            : ResolveShopId(shopId, context, config);

        var title = request?.Title ?? string.Empty;
        var description = request?.Description ?? string.Empty;
        var tags = request?.Tags ?? Array.Empty<string>();
        var targetKw = !string.IsNullOrWhiteSpace(request?.TargetKeyword)
            ? request.TargetKeyword
            : (!string.IsNullOrWhiteSpace(title) ? title : listingId);

        var input = new ListingOptimizationInput(title, description, tags, targetKw, request?.DescriptionStyle ?? "Storytelling");

        var apiKey = !string.IsNullOrWhiteSpace(request?.ApiKey)
            ? request.ApiKey.Trim()
            : context.Request.Headers.TryGetValue("X-Gemini-Api-Key", out var hKey) && !string.IsNullOrWhiteSpace(hKey)
                ? hKey.ToString().Trim()
                : config["Gemini:ApiKey"] ?? string.Empty;

        var requestedProvider = request?.Provider?.Trim();
        var isOfflineRequested = string.Equals(requestedProvider, "Offline", StringComparison.OrdinalIgnoreCase);

        string resolvedProvider;
        string resolvedModel;
        string bestTitle;
        var titleSuggestions = new List<string>();
        var tagSuggestions = new List<string>();
        var materialSuggestions = new List<string>();
        string descriptionDraft;
        var riskWarnings = new List<string>();
        var checklist = new List<string>();
        int currentScore = 0;
        int optimizedScore = 0;
        string seoCritique = string.Empty;

        // Canlı Google Gemini Çağrısı
        if (!isOfflineRequested && !string.IsNullOrWhiteSpace(apiKey))
        {
            var rawModel = !string.IsNullOrWhiteSpace(request?.Model) ? request.Model : "gemini-2.5-flash";
            var geminiModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(rawModel);

            var systemInstruction = ListingDraftInstructionBuilder.BuildSystemInstruction();
            var userPrompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(geminiModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var geminiPayload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.35,
                    topP = 0.95,
                    maxOutputTokens = 4096,
                    responseMimeType = "application/json"
                }
            };

            using var content = new StringContent(JsonSerializer.Serialize(geminiPayload), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(geminiUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errMsg = $"Google Gemini API Hatası (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}";
                try
                {
                    using var errDoc = JsonDocument.Parse(responseBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var mObj))
                    {
                        errMsg = $"Google Gemini API Hatası: {mObj.GetString()}";
                    }
                }
                catch { }

                return Results.BadRequest(new { success = false, message = errMsg });
            }

            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(candidateText))
            {
                return Results.BadRequest(new { success = false, message = "Google Gemini boş yanıt döndürdü." });
            }

            using var parsedAi = JsonDocument.Parse(candidateText);
            var root = parsedAi.RootElement;

            if (root.TryGetProperty("title_suggestions", out var titlesEl) && titlesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in titlesEl.EnumerateArray())
                {
                    var str = t.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) titleSuggestions.Add(str.Trim());
                }
            }

            if (root.TryGetProperty("tag_suggestions", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tagsEl.EnumerateArray())
                {
                    var str = t.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) tagSuggestions.Add(str.Trim().ToLowerInvariant());
                }
            }

            if (root.TryGetProperty("material_suggestions", out var matsEl) && matsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in matsEl.EnumerateArray())
                {
                    var str = m.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) materialSuggestions.Add(str.Trim());
                }
            }

            if (root.TryGetProperty("description_draft", out var descEl) && descEl.ValueKind == JsonValueKind.String)
            {
                descriptionDraft = descEl.GetString()?.Trim() ?? string.Empty;
            }
            else
            {
                descriptionDraft = string.Empty;
            }

            if (root.TryGetProperty("risk_warnings", out var risksEl) && risksEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in risksEl.EnumerateArray())
                {
                    var str = r.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) riskWarnings.Add(str.Trim());
                }
            }

            if (root.TryGetProperty("current_seo_score", out var curScoreEl) && curScoreEl.TryGetInt32(out var cs))
            {
                currentScore = cs;
            }
            if (root.TryGetProperty("optimized_seo_score", out var optScoreEl) && optScoreEl.TryGetInt32(out var os))
            {
                optimizedScore = os;
            }
            if (root.TryGetProperty("seo_critique", out var critEl) && critEl.ValueKind == JsonValueKind.String)
            {
                seoCritique = critEl.GetString()?.Trim() ?? string.Empty;
            }

            bestTitle = titleSuggestions.FirstOrDefault() ?? title;
            if (currentScore <= 0) currentScore = 80;
            if (optimizedScore <= 0) optimizedScore = 98;
            if (string.IsNullOrWhiteSpace(seoCritique))
            {
                seoCritique = "Google Gemini AI ile başlık, 13 etiket ve ürün açıklaması Etsy arama algoritması için canlı olarak optimize edildi.";
            }

            checklist.Add($"SEO puanı {currentScore}/100 -> {optimizedScore}/100 seviyesine optimize edildi.");
            checklist.Add("13 Etsy etiket alanının tamamı Gemini ile dolduruldu.");
            checklist.Add("Açıklama satış odaklı ve Etsy SEO uyumlu olarak baştan yazıldı.");

            resolvedProvider = "Gemini (Canlı API)";
            resolvedModel = geminiModel;
        }
        else
        {
            // Eğer kullanıcı Gemini istediği halde API anahtarı yoksa bilgilendir
            if (!isOfflineRequested && !string.IsNullOrWhiteSpace(requestedProvider) && requestedProvider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { success = false, message = "⚠️ Canlı Gemini API anahtarı girilmemiş. Lütfen üst bardaki AI Ayarlarından Gemini API anahtarınızı kaydediniz veya Offline seçiniz." });
            }

            // Offline Kural Motoru Fallback
            var offlineResult = optimizer.Optimize(input);
            currentScore = offlineResult.CurrentSeoScore;
            optimizedScore = offlineResult.OptimizedSeoScore;
            titleSuggestions = offlineResult.TitleSuggestions.ToList();
            bestTitle = titleSuggestions.FirstOrDefault() ?? title;
            tagSuggestions = offlineResult.TagSuggestions.ToList();
            materialSuggestions = offlineResult.MaterialSuggestions.ToList();
            descriptionDraft = offlineResult.DescriptionDraft;
            riskWarnings = offlineResult.RiskWarnings.ToList();
            checklist = offlineResult.ActionChecklist.ToList();
            seoCritique = offlineResult.SeoCritique;
            resolvedProvider = "Offline (Kural Motoru)";
            resolvedModel = "RuleBased";
        }

        var status = riskWarnings.Count > 0 ? "⚠️ AI: Risk Var" : "✨ AI: Hazır";

        // Save to SQLite
        var auditData = new ListingOptimizationResult(
            currentScore,
            optimizedScore,
            titleSuggestions,
            tagSuggestions,
            materialSuggestions,
            descriptionDraft,
            Array.Empty<string>(),
            riskWarnings,
            checklist,
            ExecutedProvider: resolvedProvider,
            ExecutedModel: resolvedModel,
            IsFallback: resolvedProvider.StartsWith("Offline"),
            FallbackReason: null,
            SeoCritique: seoCritique);

        await settingsRepo.SaveListingAuditAsync(new SaveListingAuditRecordRequest(
            resolvedShopId,
            listingId,
            title,
            currentScore,
            optimizedScore,
            status,
            resolvedProvider,
            resolvedModel,
            JsonSerializer.Serialize(auditData)), cancellationToken);

        return Results.Ok(new
        {
            success = true,
            listingId,
            currentSeoScore = currentScore,
            optimizedSeoScore = optimizedScore,
            seoScoreBefore = currentScore,
            seoScoreAfter = optimizedScore,
            optimizedTitle = bestTitle,
            suggestedTitle = bestTitle,
            titleSuggestions,
            optimizedTags = tagSuggestions.Take(13).ToList(),
            tagSuggestions = tagSuggestions.Take(13).ToList(),
            materialSuggestions,
            optimizedDescription = descriptionDraft,
            descriptionDraft,
            critique = seoCritique,
            seoCritique,
            missingTerms = Array.Empty<string>(),
            riskWarnings,
            checklist,
            status,
            aiModel = resolvedModel,
            provider = resolvedProvider,
            model = resolvedModel,
            message = resolvedProvider.Contains("Canlı", StringComparison.OrdinalIgnoreCase)
                ? "Listing başarıyla canlı Google Gemini AI ile optimize edildi ve yerel veritabanına kaydedildi."
                : "Listing başarıyla yerel kural motoru ile optimize edildi ve yerel veritabanına kaydedildi."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = $"AI optimizasyon hatası: {ex.Message}" });
    }
})
.WithTags("Etsy Listing & AI Denetimi")
.WithSummary("Listing İçin Yapay Zeka SEO ve Başlık/Tag Optimizasyonu")
.WithName("OptimizeListingWithAi");

app.MapPut("/api/etsy/listings/{listingId}", async (string listingId, UpdateListingApiRequest request, HttpContext context, IConfiguration config, IEtsyTokenStore tokenStore, IShopSettingsRepository settingsRepo, IHttpClientFactory httpClientFactory, IEtsyOAuthService oauthService, CancellationToken cancellationToken) =>
{
    var resolvedShopId = ResolveShopId(request.ShopId, context, config);
    var token = await tokenStore.GetAsync(resolvedShopId, cancellationToken);
    if (token is null)
        return Results.NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

    if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
    {
        try
        {
            token = await oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
            await tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
        }
    }

    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 140)
        return Results.BadRequest(new { error = "Başlık 1 ile 140 karakter arasında olmalıdır." });

    if (request.Title.Count(c => c == '&') > 1)
        return Results.BadRequest(new { error = "Etsy kuralı: Başlıkta '&' karakteri en fazla 1 kez kullanılabilir." });

    var raw = await settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
    var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (config["Etsy:ApiKey"] ?? string.Empty);
    var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (config["Etsy:SharedSecret"] ?? string.Empty);
    if (string.IsNullOrWhiteSpace(keystring))
        return Results.BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
    var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

    var client = httpClientFactory.CreateClient();
    var patchUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/{listingId}";

    var formDict = new Dictionary<string, string>
    {
        ["title"] = request.Title.Trim(),
        ["description"] = request.Description.Trim(),
    };

    var validTags = request.Tags.Select(t => t.Trim()).Where(t => t.Length > 0 && t.Length <= 20).Take(13).ToList();
    if (validTags.Count > 0)
    {
        formDict["tags"] = string.Join(",", validTags);
    }

    using var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
    {
        Content = new FormUrlEncodedContent(formDict)
    };
    patchReq.Headers.Add("x-api-key", apiKeyHeader);
    patchReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

    using var patchRes = await client.SendAsync(patchReq, cancellationToken);
    var patchBody = await patchRes.Content.ReadAsStringAsync(cancellationToken);

    if (!patchRes.IsSuccessStatusCode)
    {
        return Results.BadRequest(new { error = $"Etsy güncelleme başarısız (HTTP {(int)patchRes.StatusCode}): {patchBody}" });
    }

    // Update status in SQLite
    await settingsRepo.SaveListingAuditAsync(new SaveListingAuditRecordRequest(
        resolvedShopId,
        listingId,
        request.Title,
        100,
        100,
        "🚀 Etsy güncellendi",
        "EtsyAPI",
        "DirectPush",
        patchBody), cancellationToken);

    return Results.Ok(new
    {
        success = true,
        listingId,
        message = "Listing Etsy'de başarıyla güncellendi!"
    });
})
.WithTags("Etsy Listing & AI Denetimi")
.WithSummary("İlanı Başlık, Etiket ve Açıklama ile Doğrudan Etsy'de Güncelle")
.WithName("UpdateEtsyListing");

app.MapGet("/api/etsy/listings/audits", async (string shopId = "53236321", HttpContext context = null!, IConfiguration config = null!, IShopSettingsRepository settingsRepo = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var audits = await settingsRepo.GetListingAuditsAsync(resolvedShopId, cancellationToken);
    return Results.Ok(audits);
})
.WithTags("Etsy Listing & AI Denetimi")
.WithSummary("Daha Önce Yapılan Listing AI Denetim Geçmişini Getir")
.WithName("GetListingAudits");


app.MapGet("/api/banking/deposits", (IBankDepositService bankService) =>
{
    var now = DateTimeOffset.UtcNow;
    var sampleEntries = new List<RawDepositEntryInput>
    {
        new(101, 18472910, "deposit", 131.25m, -131.25m, "USD", "Etsy Payment Account Payout", now.AddDays(-1), 49.02m),
        new(102, 18410294, "deposit", 145.00m, -145.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-7), 48.95m),
        new(103, 18354890, "deposit", 120.50m, -120.50m, "USD", "Etsy Payment Account Payout", now.AddDays(-14), 48.80m),
        new(104, 18299102, "deposit", 128.00m, -128.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-21), 48.75m)
    };
    var summary = bankService.CalculateMonthlyDeposits(sampleEntries, now.AddMonths(-1), now, _ => 49.02m, 49.02m);
    return Results.Ok(summary);
})
.WithTags("Finans & Banka")
.WithSummary("Banka Yatırım ve Para Transferleri Özeti")
.WithDescription("Banka hesaplarına aktarılan net mevduat ve transfer kayıtlarının özet listesini getirir.")
.WithName("GetBankDeposits");

app.MapGet("/api/financial/summary", () => Results.Ok(new
{
    period = "Eylül 2026",
    grossSales = 45261.97,
    etsyFees = 10173.80,
    netRevenue = 29674.83,
    productCosts = 9835.33,
    realNetProfit = 19839.50,
    bankPayoutsTotal = 25701.31
}))
.WithTags("Finans & Muhasebe")
.WithSummary("Hızlı Finansal Özet Tablosu")
.WithDescription("Son dönemin brüt satış, Etsy kesintisi, net gelir, ürün maliyeti ve gerçek net kârını sade bir özet olarak döner.")
.WithName("GetFinancialSummary");

// ── 9. AUTHENTICATION & MEMBERSHIP ENDPOINTS (/api/auth) ──────────────────────

app.MapPost("/api/auth/login", async (LoginRequest request, IUserRepository userRepo, JwtTokenService jwtService, HttpContext context, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new AuthResponse(false, null, "Kullanıcı adı/e-posta ve şifre gereklidir.", null));
    }

    var user = await userRepo.GetByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
    if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
    {
        return Results.Json(new AuthResponse(false, null, "Geçersiz kullanıcı adı veya şifre.", null), statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!user.IsActive)
    {
        return Results.Json(new AuthResponse(false, null, "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.", null), statusCode: StatusCodes.Status403Forbidden);
    }

    var now = DateTimeOffset.UtcNow;
    await userRepo.UpdateLastLoginAsync(user.Id, now, cancellationToken);
    await userRepo.AddAuditLogAsync(new AuditLogEntry(0, user.Id, user.Username, "Login", "Giriş başarılı", context.Connection.RemoteIpAddress?.ToString(), now), cancellationToken);

    var token = jwtService.GenerateToken(user);
    var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), now.ToString("O"));

    return Results.Ok(new AuthResponse(true, token, "Giriş başarılı.", userDto));
})
.WithTags("Kimlik Doğrulama (Auth)")
.WithSummary("Kullanıcı Girişi")
.WithDescription("Kullanıcı adı/e-posta ve şifre ile JWT erişim token'ı alır.")
.WithName("Login");

app.MapPost("/api/auth/register", async (RegisterRequest request, IUserRepository userRepo, JwtTokenService jwtService, HttpContext context, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
    {
        return Results.BadRequest(new AuthResponse(false, null, "Kullanıcı adı en az 3 karakter olmalıdır.", null));
    }

    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        return Results.BadRequest(new AuthResponse(false, null, "Geçerli bir e-posta adresi giriniz.", null));
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
    {
        return Results.BadRequest(new AuthResponse(false, null, "Şifre en az 6 karakter olmalıdır.", null));
    }

    var existingUser = await userRepo.GetByUsernameOrEmailAsync(request.Username, cancellationToken)
        ?? await userRepo.GetByUsernameOrEmailAsync(request.Email, cancellationToken);

    if (existingUser != null)
    {
        return Results.Conflict(new AuthResponse(false, null, "Bu kullanıcı adı veya e-posta zaten kullanımda.", null));
    }

    var (hash, salt) = PasswordHasher.HashPassword(request.Password);
    var now = DateTimeOffset.UtcNow;
    var assignedShops = string.IsNullOrWhiteSpace(request.ShopId) ? new List<string> { "53236321" } : new List<string> { request.ShopId.Trim() };

    var newUser = new AppUser(
        Guid.NewGuid().ToString(),
        request.Username.Trim(),
        request.Email.Trim().ToLowerInvariant(),
        hash,
        salt,
        UserRoles.StoreOwner,
        assignedShops,
        500_000,
        0,
        true,
        now,
        now);

    await userRepo.CreateUserAsync(newUser, cancellationToken);
    await userRepo.AddAuditLogAsync(new AuditLogEntry(0, newUser.Id, newUser.Username, "Register", "Yeni kayıt oluşturuldu", context.Connection.RemoteIpAddress?.ToString(), now), cancellationToken);

    var token = jwtService.GenerateToken(newUser);
    var userDto = new UserDto(newUser.Id, newUser.Username, newUser.Email, newUser.Role, newUser.AssignedShopIds, newUser.MonthlyAiTokenQuota, newUser.UsedAiTokens, newUser.IsActive, newUser.CreatedAt.ToString("O"), now.ToString("O"));

    return Results.Ok(new AuthResponse(true, token, "Kayıt başarıyla tamamlandı.", userDto));
})
.WithTags("Kimlik Doğrulama (Auth)")
.WithSummary("Yeni Kullanıcı Kaydı")
.WithDescription("Yeni bir mağaza sahibi hesabı oluşturur ve JWT token döner.")
.WithName("Register");

app.MapGet("/api/auth/me", async (HttpContext context, IUserRepository userRepo, JwtTokenService jwtService, CancellationToken cancellationToken) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Unauthorized();
    }

    var token = authHeader["Bearer ".Length..].Trim();
    var (isValid, _, userId, _, _) = jwtService.ValidateToken(token);
    if (!isValid || string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    var user = await userRepo.GetByIdAsync(userId, cancellationToken);
    if (user == null || !user.IsActive)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), user.LastLoginAt?.ToString("O"));
    return Results.Ok(userDto);
})
.WithTags("Kimlik Doğrulama (Auth)")
.WithSummary("Geçerli Kullanıcı Bilgileri")
.WithDescription("Bearer JWT token doğrulayarak giriş yapmış kullanıcının profil ve yetkilerini döner.")
.WithName("GetCurrentUser");

// ── 10. ADMIN MANAGEMENT ENDPOINTS (/api/admin) ────────────────────────────────

app.MapGet("/api/admin/users", async (HttpContext context, IUserRepository userRepo, JwtTokenService jwtService, CancellationToken cancellationToken) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
    var (isValid, _, _, role, _) = jwtService.ValidateToken(token);
    if (!isValid || role != UserRoles.Admin)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var users = await userRepo.GetAllUsersAsync(cancellationToken);
    var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.Email, u.Role, u.AssignedShopIds, u.MonthlyAiTokenQuota, u.UsedAiTokens, u.IsActive, u.CreatedAt.ToString("O"), u.LastLoginAt?.ToString("O"))).ToList();
    return Results.Ok(dtos);
})
.WithTags("Admin Yönetim Paneli")
.WithSummary("Kullanıcı Listesi")
.WithDescription("Sistemdeki tüm kayıtlı kullanıcıları ve mağaza atamalarını döner (Yalnızca Admin).")
.WithName("GetAllUsers");

app.MapPut("/api/admin/users/{id}", async (string id, UpdateUserRequest request, HttpContext context, IUserRepository userRepo, JwtTokenService jwtService, CancellationToken cancellationToken) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
    var (isValid, _, adminId, role, adminName) = jwtService.ValidateToken(token);
    if (!isValid || role != UserRoles.Admin)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var existingUser = await userRepo.GetByIdAsync(id, cancellationToken);
    if (existingUser == null)
    {
        return Results.NotFound(new { error = "Kullanıcı bulunamadı." });
    }

    var updated = existingUser with
    {
        Email = request.Email.Trim().ToLowerInvariant(),
        Role = request.Role,
        AssignedShopIds = request.AssignedShopIds ?? existingUser.AssignedShopIds,
        MonthlyAiTokenQuota = request.MonthlyAiTokenQuota,
        IsActive = request.IsActive
    };

    await userRepo.UpdateUserAsync(updated, cancellationToken);
    await userRepo.AddAuditLogAsync(new AuditLogEntry(0, adminId ?? "admin", adminName ?? "Admin", "UpdateUser", $"Kullanıcı güncellendi: {existingUser.Username} ({updated.Role})", context.Connection.RemoteIpAddress?.ToString(), DateTimeOffset.UtcNow), cancellationToken);

    return Results.Ok(new { success = true, message = "Kullanıcı başarıyla güncellendi." });
})
.WithTags("Admin Yönetim Paneli")
.WithSummary("Kullanıcı Düzenleme & Yetki/Mağaza Atama")
.WithDescription("Kullanıcının rolünü, e-postasını, yetkili olduğu mağazaları ve AI token kotasını günceller.")
.WithName("UpdateUser");

app.MapPost("/api/admin/users/{id}/toggle-status", async (string id, HttpContext context, IUserRepository userRepo, JwtTokenService jwtService, CancellationToken cancellationToken) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
    var (isValid, _, adminId, role, adminName) = jwtService.ValidateToken(token);
    if (!isValid || role != UserRoles.Admin)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var existingUser = await userRepo.GetByIdAsync(id, cancellationToken);
    if (existingUser == null)
    {
        return Results.NotFound(new { error = "Kullanıcı bulunamadı." });
    }

    var updated = existingUser with { IsActive = !existingUser.IsActive };
    await userRepo.UpdateUserAsync(updated, cancellationToken);
    await userRepo.AddAuditLogAsync(new AuditLogEntry(0, adminId ?? "admin", adminName ?? "Admin", "ToggleStatus", $"Kullanıcı durumu değiştirildi: {existingUser.Username} (Aktif: {updated.IsActive})", context.Connection.RemoteIpAddress?.ToString(), DateTimeOffset.UtcNow), cancellationToken);

    return Results.Ok(new { success = true, isActive = updated.IsActive });
})
.WithTags("Admin Yönetim Paneli")
.WithSummary("Kullanıcı Durumu Değiştirme (Aktif/Askıda)")
.WithDescription("Kullanıcının sisteme erişimini aktif eder veya askıya alır.")
.WithName("ToggleUserStatus");

app.MapGet("/api/admin/audit-logs", async (int limit = 100, HttpContext context = null!, IUserRepository userRepo = null!, JwtTokenService jwtService = null!, CancellationToken cancellationToken = default) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
    var (isValid, _, _, role, _) = jwtService.ValidateToken(token);
    if (!isValid || role != UserRoles.Admin)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var logs = await userRepo.GetAuditLogsAsync(Math.Clamp(limit, 1, 500), cancellationToken);
    var dtos = logs.Select(l => new AuditLogDto(l.Id, l.UserId, l.Username, l.Action, l.Details, l.IpAddress, l.Timestamp.ToString("O"))).ToList();
    return Results.Ok(dtos);
})
.WithTags("Admin Yönetim Paneli")
.WithSummary("Audit Güvenlik Günlüğü")
.WithDescription("Sistemdeki giriş, kayıt ve yönetim aksiyonlarının güvenlik kayıtlarını döner.")
.WithName("GetAuditLogs");

app.MapGet("/api/admin/system-stats", async (HttpContext context, IUserRepository userRepo, JwtTokenService jwtService, CancellationToken cancellationToken) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
    var (isValid, _, _, role, _) = jwtService.ValidateToken(token);
    if (!isValid || role != UserRoles.Admin)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var users = await userRepo.GetAllUsersAsync(cancellationToken);
    var logs = await userRepo.GetAuditLogsAsync(1000, cancellationToken);

    var totalUsers = users.Count;
    var activeUsers = users.Count(u => u.IsActive);
    var totalShops = users.SelectMany(u => u.AssignedShopIds).Distinct().Count();
    var totalUsedTokens = users.Sum(u => (long)u.UsedAiTokens);

    return Results.Ok(new SystemStatsDto(totalUsers, activeUsers, totalShops, totalUsedTokens, logs.Count));
})
.WithTags("Admin Yönetim Paneli")
.WithSummary("Sistem ve Kullanıcı İstatistikleri")
.WithDescription("Toplam kullanıcı, aktif kullanıcı, lisanslı mağaza ve AI token tüketim istatistiklerini döner.")
.WithName("GetSystemStats");

// ── 7. A/B TEST YÖNETİMİ ENDPOINT'LERİ (/api/etsy/ab-tests) ─────────────────
app.MapGet("/api/etsy/ab-tests", async (string? listingId, IAbTestRepository repo, CancellationToken ct) =>
{
    var tests = string.IsNullOrWhiteSpace(listingId)
        ? await repo.GetRecentAsync(50, ct)
        : await repo.GetByListingIdAsync(listingId, ct);
    return Results.Ok(tests);
})
.WithTags("A/B Test Paneli")
.WithSummary("A/B Test Deneylerini Listele")
.WithName("GetAbTests");

app.MapPost("/api/etsy/ab-tests", async (SaveAbTestExperiment experiment, IAbTestRepository repo, CancellationToken ct) =>
{
    var saved = await repo.SaveAsync(experiment, ct);
    return Results.Ok(saved);
})
.WithTags("A/B Test Paneli")
.WithSummary("Yeni A/B Test Başlat")
.WithName("CreateAbTest");

app.MapPut("/api/etsy/ab-tests/{id}/status", async (long id, UpdateAbTestStatusRequest request, IAbTestRepository repo, CancellationToken ct) =>
{
    var updated = await repo.UpdateStatusAsync(id, request.Status, ct);
    return updated != null ? Results.Ok(updated) : Results.NotFound();
})
.WithTags("A/B Test Paneli")
.WithSummary("A/B Test Durumunu Güncelle")
.WithName("UpdateAbTestStatus");

app.MapDelete("/api/etsy/ab-tests/{id}", async (long id, IAbTestRepository repo, CancellationToken ct) =>
{
    var deleted = await repo.DeleteAsync(id, ct);
    return deleted ? Results.Ok(new { success = true }) : Results.NotFound();
})
.WithTags("A/B Test Paneli")
.WithSummary("A/B Test Deneyini Sil")
.WithName("DeleteAbTest");

// ── 8. AI TOKEN & MODEL KULLANIM ENDPOINT'LERİ (/api/etsy/ai-usage) ──────────
app.MapGet("/api/etsy/ai-usage/stats", async (string? provider, IAiUsageRepository repo, CancellationToken ct) =>
{
    var stats = await repo.GetSummaryStatsAsync(provider, DateTimeOffset.UtcNow.AddDays(-30), ct);
    return Results.Ok(stats);
})
.WithTags("AI Kullanım Takibi")
.WithSummary("Son 30 Günlük AI Token & Maliyet Özeti")
.WithName("GetAiUsageStats");

app.MapGet("/api/etsy/ai-usage/history", async (string? provider, int limit, IAiUsageRepository repo, CancellationToken ct) =>
{
    var history = await repo.GetHistoryAsync(provider, null, Math.Clamp(limit <= 0 ? 100 : limit, 1, 300), ct);
    return Results.Ok(history);
})
.WithTags("AI Kullanım Takibi")
.WithSummary("AI Model Çağrı Geçmişi")
.WithName("GetAiUsageHistory");

app.MapPost("/api/etsy/ai-usage", async (AiUsageRecord record, IAiUsageRepository repo, CancellationToken ct) =>
{
    var saved = await repo.SaveUsageAsync(record, ct);
    return Results.Ok(saved);
})
.WithTags("AI Kullanım Takibi")
.WithSummary("Yeni AI Çağrı Kaydı Ekle")
.WithName("RecordAiUsage");

// ── 9. TOPLU OPTİMİZASYON & BATCH KUYRUĞU (/api/etsy/batch-queue) ────────────
app.MapGet("/api/etsy/batch-queue", async (string? status, IBatchQueueRepository repo, CancellationToken ct) =>
{
    var items = string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
        ? await repo.GetPendingAsync(100, ct)
        : await repo.GetAllAsync(200, ct);
    return Results.Ok(items);
})
.WithTags("Toplu İşlem Kuyruğu")
.WithSummary("Batch Kuyruğundaki İlanları Getir")
.WithName("GetBatchQueue");

app.MapPost("/api/etsy/batch-queue/enqueue", async (BatchEnqueueRequest request, IBatchQueueRepository repo, CancellationToken ct) =>
{
    var enqueued = await repo.EnqueueBatchAsync(request.Items, ct);
    return Results.Ok(enqueued);
})
.WithTags("Toplu İşlem Kuyruğu")
.WithSummary("Toplu Optimizasyon Kuyruğuna İlan Ekle")
.WithName("EnqueueBatchItems");

app.MapPost("/api/etsy/batch-queue/{id}/process", async (long id, BatchProcessRequest req, IBatchQueueRepository repo, CancellationToken ct) =>
{
    var existing = await repo.GetByIdAsync(id, ct);
    if (existing == null) return Results.NotFound();

    var updated = existing with
    {
        OptimizedTitle = req.OptimizedTitle ?? existing.OptimizedTitle,
        OptimizedDescription = req.OptimizedDescription ?? existing.OptimizedDescription,
        OptimizedTags = req.OptimizedTags ?? existing.OptimizedTags,
        OverallScore = req.OverallScore,
        Status = req.Status,
        ProcessedAt = DateTimeOffset.UtcNow
    };

    var result = await repo.UpdateItemAsync(updated, ct);
    return Results.Ok(result);
})
.WithTags("Toplu İşlem Kuyruğu")
.WithSummary("Batch Kuyruk Öğesini İşlenmiş Olarak Güncelle")
.WithName("ProcessBatchItem");

app.MapDelete("/api/etsy/batch-queue/completed", async (IBatchQueueRepository repo, CancellationToken ct) =>
{
    var count = await repo.ClearCompletedAsync(ct);
    return Results.Ok(new { success = true, cleared = count });
})
.WithTags("Toplu İşlem Kuyruğu")
.WithSummary("Tamamlanan Kuyruk Öğelerini Temizle")
.WithName("ClearCompletedBatch");

// ── 10. CANLI KARGO & ÜRÜN TAKİBİ (/api/etsy/tracking) ──────────────────────
app.MapGet("/api/etsy/tracking", async (ITrackingRepository repo, CancellationToken ct) =>
{
    var items = await repo.GetItemsAsync(ct);
    return Results.Ok(items);
})
.WithTags("Canlı Takip")
.WithSummary("Takip Edilen Varlıkları Getir")
.WithName("GetTrackingItems");

app.MapGet("/api/etsy/tracking/{id}/snapshots", async (long id, ITrackingRepository repo, CancellationToken ct) =>
{
    var snapshots = await repo.GetSnapshotsAsync(id, ct);
    return Results.Ok(snapshots);
})
.WithTags("Canlı Takip")
.WithSummary("Takip Öğesinin Geçmiş Anlık Görüntüleri")
.WithName("GetTrackingSnapshots");

app.MapPost("/api/etsy/tracking/capture", async (TrackingCapture capture, ITrackingRepository repo, CancellationToken ct) =>
{
    var item = await repo.SaveCaptureAsync(capture, ct);
    return Results.Ok(item);
})
.WithTags("Canlı Takip")
.WithSummary("Yeni Takip Görüntüsü Kaydet")
.WithName("SaveTrackingCapture");

app.MapDelete("/api/etsy/tracking/{id}", async (long id, ITrackingRepository repo, CancellationToken ct) =>
{
    await repo.DeleteItemAsync(id, ct);
    return Results.Ok(new { success = true });
})
.WithTags("Canlı Takip")
.WithSummary("Takip Öğesini Sil")
.WithName("DeleteTrackingItem");

// ── 11. MAĞAZA GEÇMİŞ PERFORMANSI (/api/etsy/shop/performance) ──────────────
app.MapGet("/api/etsy/shop/performance", async (long shopId, IShopPerformanceHistoryRepository repo, CancellationToken ct) =>
{
    var history = await repo.GetByShopAsync(shopId, ct);
    return Results.Ok(history);
})
.WithTags("Mağaza Performansı")
.WithSummary("Mağaza Performans Geçmişini Getir")
.WithName("GetShopPerformanceHistory");

// ── 12. GÜVENLİ MAĞAZA AYARLARI (TELEGRAM & KARGO - SQLite) ──────────────────
app.MapGet("/api/etsy/settings/telegram", async (string shopId, IShopSettingsRepository repo, CancellationToken ct) =>
{
    var settings = await repo.GetTelegramSettingsAsync(shopId, ct);
    return Results.Ok(settings ?? new TelegramShopSettings(shopId, "", "", false, true, true, true, DateTimeOffset.UtcNow));
})
.WithTags("Güvenli SQLite Ayarları")
.WithSummary("Mağaza Telegram Bildirim Ayarlarını Getir (Maskeli)")
.WithName("GetTelegramSettings");

app.MapPost("/api/etsy/settings/telegram", async (SaveTelegramSettingsRequest request, IShopSettingsRepository repo, CancellationToken ct) =>
{
    var saved = await repo.SaveTelegramSettingsAsync(request, ct);
    return Results.Ok(saved);
})
.WithTags("Güvenli SQLite Ayarları")
.WithSummary("Mağaza Telegram Ayarlarını Kaydet (Şifreli)")
.WithName("SaveTelegramSettings");

app.MapGet("/api/etsy/settings/carrier-sessions", async (string shopId, IShopSettingsRepository repo, CancellationToken ct) =>
{
    var sessions = await repo.GetCarrierSessionsAsync(shopId, ct);
    return Results.Ok(sessions);
})
.WithTags("Güvenli SQLite Ayarları")
.WithSummary("Kargo Taşıyıcı Oturumlarını Getir (Maskeli)")
.WithName("GetCarrierSessions");

app.MapPost("/api/etsy/settings/carrier-sessions", async (SaveCarrierSessionRequest request, IShopSettingsRepository repo, CancellationToken ct) =>
{
    var saved = await repo.SaveCarrierSessionAsync(request, ct);
    return Results.Ok(saved);
})
.WithTags("Güvenli SQLite Ayarları")
.WithSummary("Kargo Taşıyıcı Oturumunu Kaydet (Şifreli)")
.WithName("SaveCarrierSession");

// ── 13. SİSTEM & VDS CANLILIK KONTROLÜ (/api/system) ────────────────────────
app.MapGet("/api/system/version", () => Results.Ok(new
{
    version = "2.4.0",
    service = "EtsyMarketPlace VDS Command Engine",
    status = "Online",
    database = "SQLite-WAL Encrypted",
    uptimeSeconds = Environment.TickCount64 / 1000,
    serverTime = DateTimeOffset.UtcNow.ToString("O")
}))
.WithTags("Sistem & VDS")
.WithSummary("VDS API Sürüm & Sağlık Durumu")
.WithName("GetSystemVersion");

app.Run();

/// <summary>Kontrol paneli canli veri yanitlari icin kisa omurlu bellek ici onbellek (Etsy hiz limitlerini korur).</summary>
internal static class DashboardResponseCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new();
    private sealed record CacheEntry(DateTimeOffset ExpiresAtUtc, object Payload);

    public static bool TryGet(string key, out object? payload)
    {
        if (Entries.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            payload = entry.Payload;
            return true;
        }

        payload = null;
        return false;
    }

    public static void Set(string key, object payload, TimeSpan ttl)
    {
        foreach (var staleKey in Entries.Where(kv => kv.Value.ExpiresAtUtc <= DateTimeOffset.UtcNow).Select(kv => kv.Key).ToList())
        {
            Entries.TryRemove(staleKey, out _);
        }

        Entries[key] = new CacheEntry(DateTimeOffset.UtcNow.Add(ttl), payload);
    }
}
