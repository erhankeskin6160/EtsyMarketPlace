using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
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
using EtsyMarketPlace.Infrastructure.AbTesting;
using EtsyMarketPlace.Infrastructure.AiUsage;
using EtsyMarketPlace.Infrastructure.BatchQueue;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using EtsyMarketPlace.Infrastructure.Tracking;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1. Controller Desteği
builder.Services.AddControllers();

// 2. Dependency Injection (Clean Architecture Servisleri)
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
builder.Services.AddScoped<IEtsyLedgerReportService, EtsyLedgerReportService>();
builder.Services.AddHostedService<EtsyIntegrationDatabaseInitializer>();
builder.Services.AddScoped<McpToolHandler>();

// 3. CORS (Angular Web Studio, Google Gemini Web ve Harici Entegrasyonlar İçin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 4. Swagger / OpenAPI
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

// 5. Güvenlik Middleware'i
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp") || context.Request.Path.StartsWithSegments("/api/etsy"))
    {
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

// 6. Controller Rotalarını Eşle
app.MapControllers();

app.Run();
