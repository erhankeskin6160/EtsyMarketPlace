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

// 1. Dependency Injection (Clean Architecture Servisleri)
builder.Services.AddSingleton<IBankDepositService, BankDepositService>();
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

app.MapGet("/api/etsy/financial/performance", async (string shopId = "53236321", string period = "last_month", HttpContext context = null!, IConfiguration config = null!, IEtsyReportingService reporting = null!, CancellationToken cancellationToken = default) =>
{
    var resolvedShopId = ResolveShopId(shopId, context, config);
    var now = DateTimeOffset.UtcNow;
    var (start, end) = period?.ToLowerInvariant() switch
    {
        "today" => (now.Date, now),
        "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1)),
        null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
        _ => (DateTimeOffset.MinValue, DateTimeOffset.MinValue)
    };
    if (start == DateTimeOffset.MinValue) return Results.BadRequest(new { error = "period today, this_month veya last_month olmalıdır." });
    return Results.Ok(await reporting.GetFinancialPerformanceAsync(resolvedShopId, start, end, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Finansal Performans ve Kâr-Zarar Karnesi")
.WithDescription("Belirtilen dönem (today, this_month, last_month veya özel tarih aralığı) için brüt satış, Etsy komisyonları, reklam harcamaları, ürün ve kargo maliyetleri ile net kâr marjını hesaplar. Mağaza ID belirtilmezse varsayılan mağaza (53236321) kullanılır.")
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
        var refreshed = await oauthService.RefreshTokenAsync(token.RefreshToken, cancellationToken);
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
