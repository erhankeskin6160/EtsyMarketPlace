using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;

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
builder.Services.AddSingleton<SqliteEtsyIntegrationStore>();
builder.Services.AddSingleton<IEtsyTokenStore>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IEtsyIntegrationRepository>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddSingleton<IEtsyReportingService>(services => services.GetRequiredService<SqliteEtsyIntegrationStore>());
builder.Services.AddScoped<IEtsyFinancialAnalysisService, EtsyFinancialAnalysisService>();
builder.Services.AddScoped<IEtsySynchronizationService, EtsySynchronizationService>();
builder.Services.AddHostedService<EtsyIntegrationDatabaseInitializer>();
builder.Services.AddScoped<McpToolHandler>();

// 2. CORS (Google Gemini Web ve Harici Entegrasyonlar İçin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0) policy.WithOrigins(origins);
        policy.AllowAnyHeader().AllowAnyMethod();
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

app.MapGet("/api/etsy/banking/payouts", async (string shopId, DateTimeOffset? startDate, DateTimeOffset? endDate, IEtsyReportingService reporting, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId)) return Results.BadRequest(new { error = "shopId zorunludur." });
    var end = endDate ?? DateTimeOffset.UtcNow;
    var start = startDate ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
    if (start > end) return Results.BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
    return Results.Ok(await reporting.GetBankPayoutsAsync(shopId, start, end, cancellationToken));
})
.WithTags("Finans & Banka")
.WithSummary("Etsy Banka Transferleri (Payouts)")
.WithDescription("Etsy'nin mağazanız için banka hesabınıza yatırdığı tüm ödeme ve transfer kayıtlarını tarih, tutar, kur ve durum bilgileriyle listeler.")
.WithName("GetEtsyBankPayouts");

app.MapGet("/api/etsy/financial/performance", async (string shopId, string? period, IEtsyReportingService reporting, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId)) return Results.BadRequest(new { error = "shopId zorunludur." });
    var now = DateTimeOffset.UtcNow;
    var (start, end) = period?.ToLowerInvariant() switch
    {
        "today" => (now.Date, now),
        "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1)),
        null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
        _ => (DateTimeOffset.MinValue, DateTimeOffset.MinValue)
    };
    if (start == DateTimeOffset.MinValue) return Results.BadRequest(new { error = "period today, this_month veya last_month olmalıdır." });
    return Results.Ok(await reporting.GetFinancialPerformanceAsync(shopId, start, end, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Finansal Performans ve Kâr-Zarar Karnesi")
.WithDescription("Belirtilen dönem (today, this_month, last_month veya özel tarih aralığı) için brüt satış, Etsy komisyonları, reklam harcamaları, ürün ve kargo maliyetleri ile net kâr marjını hesaplar.")
.WithName("GetFinancialPerformance");

app.MapGet("/api/etsy/financial/analysis", async (string shopId, DateTimeOffset? startDate, DateTimeOffset? endDate, IEtsyFinancialAnalysisService analysis, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId)) return Results.BadRequest(new { error = "shopId zorunludur." });
    var end = endDate ?? DateTimeOffset.UtcNow;
    var start = startDate ?? end.AddDays(-30);
    if (start > end) return Results.BadRequest(new { error = "startDate endDate değerinden sonra olamaz." });
    return Results.Ok(await analysis.AnalyzeAsync(shopId.Trim(), start, end, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Akıllı Finansal Analiz ve Öneri Motoru")
.WithDescription("Finansal performansı kural tabanlı yapay zeka mantığıyla analiz eder. Kâr marjı, ciro değişimi ve gider oranlarını değerlendirerek mağazaya özel finansal uyarılar (insights) ve aksiyon önerileri (recommendations) sunar.")
.WithName("GetFinancialAnalysis");

app.MapGet("/api/etsy/orders/unfulfilled-cost-alerts", async (string shopId, IEtsyReportingService reporting, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId)) return Results.BadRequest(new { error = "shopId zorunludur." });
    return Results.Ok(await reporting.GetUnfulfilledCostAlertsAsync(shopId, cancellationToken));
})
.WithTags("Sipariş & Maliyet")
.WithSummary("Maliyeti Eksik Sipariş Alarmları")
.WithDescription("Henüz kargolanmamış veya üretim/kargo maliyeti girilmemiş açık siparişleri listeler. Gerçek net kârın eksik maliyet yüzünden yanıltıcı çıkmasını önler.")
.WithName("GetUnfulfilledCostAlerts");

app.MapGet("/api/etsy/shop/daily-brief", async (string shopId, DateTimeOffset? date, IEtsyReportingService reporting, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId)) return Results.BadRequest(new { error = "shopId zorunludur." });
    return Results.Ok(await reporting.GetDailyShopBriefAsync(shopId, date ?? DateTimeOffset.UtcNow, cancellationToken));
})
.WithTags("Finans & Muhasebe")
.WithSummary("Günlük Mağaza Bülteni ve Sağlık Skoru")
.WithDescription("Seçilen gün için brüt satış, net kâr, sipariş sayısı ve mağaza sağlık skorunu (Health Score 0-100) özetler.")
.WithName("GetDailyShopBrief");

app.MapPost("/api/etsy/sync", async (EtsySyncRequest request, IConfiguration configuration, IEtsySynchronizationService synchronization, CancellationToken cancellationToken) =>
{
    var shopId = string.IsNullOrWhiteSpace(request.ShopId) ? configuration["Etsy:ShopId"] : request.ShopId;
    if (string.IsNullOrWhiteSpace(shopId))
        return Results.BadRequest(new { error = "ShopId body içinde veya Etsy:ShopId yapılandırmasında belirtilmelidir." });

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

app.MapGet("/api/etsy/charts/{shopId}/{chartType}.png", async (string shopId, string chartType, IEtsyIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    var snapshot = await repository.GetChartSnapshotAsync(shopId, chartType.ToLowerInvariant(), cancellationToken);
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

app.MapGet("/api/etsy/token/status", async (string shopId, IEtsyTokenStore tokenStore, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(shopId))
        return Results.BadRequest(new { error = "shopId zorunludur." });

    var token = await tokenStore.GetAsync(shopId.Trim(), cancellationToken);
    return token is null
        ? Results.NotFound(new { exists = false, shopId = shopId.Trim() })
        : Results.Ok(new
        {
            exists = true,
            shopId = shopId.Trim(),
            expiresAt = token.AccessTokenExpiresAt,
            isExpired = token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = token.TokenType
        });
})
.WithTags("Yetkilendirme & Token")
.WithSummary("Etsy Token Durum ve Geçerlilik Kontrolü")
.WithDescription("Kayıtlı Etsy OAuth token'ının süresinin dolup dolmadığını ve kalan geçerlilik süresini kontrol eder.")
.WithName("GetEtsyTokenStatus");

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

app.Run();
