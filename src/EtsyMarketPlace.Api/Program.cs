using Asp.Versioning;
using EtsyMarketPlace.Api.Middleware;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Infrastructure.AbTesting;
using EtsyMarketPlace.Api.Filters;
using EtsyMarketPlace.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using EtsyMarketPlace.Application.Common.Validation;
using EtsyMarketPlace.Infrastructure.AiUsage;
using EtsyMarketPlace.Infrastructure.BatchQueue;
using EtsyMarketPlace.Infrastructure.Database.Migrations;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using EtsyMarketPlace.Infrastructure.Tracking;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentValidation;
using EtsyMarketPlace.Domain.Common;
using EtsyMarketPlace.Infrastructure.Persistence;
using EtsyMarketPlace.Infrastructure.Persistence.Repositories;
using EtsyMarketPlace.Application.Common.Behaviours;
using EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1. Controller Desteği & Global Validation Pipeline & MediatR Pipeline (FlowaErp CQRS)
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddApplicationValidation();

// 1.01. API Sürümleme (Dual-Routing v1 & Sürümsüz Geriye Uyumluluk)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// 1.1. Kimlik doğrulama (P0): JWT Bearer + X-Api-Key (makine-makine) ve varsayılan zorunlu politika.
builder.Services.AddAuthentication("Multi")
    .AddPolicyScheme("Multi", "JWT Bearer veya X-Api-Key", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName) ? "ApiKey" : "Bearer";
    })
    .AddScheme<AuthenticationSchemeOptions, BearerAuthenticationHandler>("Bearer", null)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddValidatorsFromAssembly(typeof(ImportFullFinancialsValidator).Assembly);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ImportFullFinancialsHandler).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ExceptionBehaviour<,>));
});

// 2. Dependency Injection (Clean Architecture Servisleri)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IBankDepositService, BankDepositService>();
builder.Services.AddSingleton<ListingOptimizationService>();
builder.Services.Configure<EtsyIntegrationOptions>(builder.Configuration.GetSection("EtsyIntegration"));
builder.Services.Configure<EtsyApiOptions>(builder.Configuration.GetSection("Etsy"));

// 2.0. Başlangıç Yapılandırma Doğrulamaları (GitHub Issue #44: ValidateOnStart)
builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection("Security"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(j => string.IsNullOrWhiteSpace(j.Secret) || j.Secret.Trim().Length >= 32, "Jwt:Secret tanımlandığında en az 32 karakter uzunluğunda olmalıdır.")
    .ValidateOnStart();

builder.Services.AddScoped<IConfigurationStatusService, ConfigurationStatusService>();

// 2.1. PostgreSQL Entity Framework Core DbContext Kaydı
var rawPgConnectionString = builder.Configuration.GetConnectionString("PostgreSql")
    ?? "Host=localhost;Port=5432;Database=etsy_marketplace_db;Username=postgres;Password=admin123";
var pgCsBuilder = new Npgsql.NpgsqlConnectionStringBuilder(rawPgConnectionString);
if (!rawPgConnectionString.Contains("Timeout=", StringComparison.OrdinalIgnoreCase) && pgCsBuilder.Timeout > 5)
{
    pgCsBuilder.Timeout = 5;
}
var pgConnectionString = pgCsBuilder.ConnectionString;

builder.Services.AddDbContextFactory<EtsyMarketPlaceDbContext>(options =>
{
    options.UseNpgsql(pgConnectionString);
});
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<EtsyMarketPlaceDbContext>>().CreateDbContext());
builder.Services.AddScoped<EtsyMarketPlace.Infrastructure.Database.Postgres.PostgreSqlDataMigrator>();

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

builder.Services.AddSingleton<ISqliteMigrationRunner, SqliteMigrationRunner>();
builder.Services.AddSingleton<SqliteEtsyIntegrationStore>();
builder.Services.AddSingleton<SqliteAccountingStore>();
builder.Services.AddScoped<IEtsyTokenStore, EfPostgreSqlTokenStore>();
builder.Services.AddScoped<IUserRepository, EfPostgreSqlUserRepository>();
builder.Services.AddScoped<IShopSettingsRepository, EfPostgreSqlShopSettingsRepository>();
builder.Services.AddScoped<IAccountingRepository, EfPostgreSqlAccountingRepository>();

// PostgreSQL 16 EF Core Persistence & Unit of Work (FAZ 1: Finansal Veri Görünürlüğü)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<EfPostgreSqlIntegrationRepository>();
builder.Services.AddScoped<IEtsyIntegrationRepository>(services => services.GetRequiredService<EfPostgreSqlIntegrationRepository>());
builder.Services.AddScoped<IEtsyReportingService>(services => services.GetRequiredService<EfPostgreSqlIntegrationRepository>());

// PostgreSQL 16 EF Core Repositories (FAZ 4: İlanlar, A/B Testleri, Batch Queue, AI Usage & Tracking)
builder.Services.AddScoped<IAbTestRepository, EfPostgreSqlAbTestRepository>();
builder.Services.AddScoped<IAiUsageRepository, EfPostgreSqlAiUsageRepository>();
builder.Services.AddScoped<IBatchQueueRepository, EfPostgreSqlBatchQueueRepository>();
builder.Services.AddScoped<ITrackingRepository, EfPostgreSqlTrackingRepository>();
builder.Services.AddSingleton<IShopPerformanceHistoryRepository>(sp => new SqliteShopPerformanceHistoryRepository(dbPath));
builder.Services.AddSingleton<EtsyMarketPlace.Application.Taxonomy.IEtsyTaxonomyRepository>(sp =>
{
    var repo = new EtsyMarketPlace.Infrastructure.Taxonomy.SqliteEtsyTaxonomyRepository(dbPath);
    repo.InitializeAsync().GetAwaiter().GetResult();
    return repo;
});

var jwtSigningSecret = JwtSecretProvider.Resolve(builder.Configuration, dataDir);
builder.Services.AddSingleton(new JwtTokenService(jwtSigningSecret));
builder.Services.AddScoped<IEtsyFinancialAnalysisService, EtsyFinancialAnalysisService>();
builder.Services.AddScoped<IEtsySynchronizationService, EtsySynchronizationService>();
builder.Services.AddScoped<IEtsyLedgerReportService, EtsyLedgerReportService>();
builder.Services.AddScoped<IEtsyDashboardService, EtsyDashboardService>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.ListingOptimization.IEtsyListingApiClient, EtsyMarketPlace.Infrastructure.EtsyIntegration.EtsyListingApiClient>();
builder.Services.AddSingleton<EtsyMarketPlace.Domain.ShopVault.Interfaces.IExifStripper, EtsyMarketPlace.Infrastructure.ShopVault.ImageSanitization.ExifStripperImageProcessor>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.ListingOptimization.IGeminiListingApiClient, EtsyMarketPlace.Infrastructure.ListingOptimization.GeminiListingApiClient>();
builder.Services.AddScoped<EtsyMarketPlace.Application.ListingOptimization.IFastListingPublishService, EtsyMarketPlace.Application.ListingOptimization.FastListingPublishService>();
builder.Services.AddScoped<EtsyMarketPlace.Application.ListingOptimization.IEtsyListingService, EtsyMarketPlace.Application.ListingOptimization.EtsyListingService>();
builder.Services.AddScoped<EtsyMarketPlace.Application.ListingOptimization.IAiListingOptimizerUseCase, EtsyMarketPlace.Application.ListingOptimization.AiListingOptimizationUseCase>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Shipping.IEtsyLogisticsApiClient, EtsyMarketPlace.Infrastructure.Shipping.EtsyLogisticsApiClient>();
builder.Services.AddScoped<EtsyMarketPlace.Application.Shipping.IEtsyLogisticsService, EtsyMarketPlace.Application.Shipping.EtsyLogisticsService>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Shipping.IShipEntegraApiClient, EtsyMarketPlace.Infrastructure.Shipping.ShipEntegraApiClient>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Shipping.IArasGlobalApiClient, EtsyMarketPlace.Infrastructure.Shipping.ArasGlobalApiClient>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Shipping.IShiptomoreOfficialApi, EtsyMarketPlace.Infrastructure.Shipping.ShiptomoreOfficialApiClient>();
EtsyMarketPlace.Application.Shipping.ShippingSecretProtector.Current =
    new EtsyMarketPlace.Infrastructure.Shipping.DpapiShippingSecretProtector();

// PostgreSQL 16 EF Core Sipariş & Adres Deposu (FAZ 2: Kargo & Lojistik)
builder.Services.AddScoped<EtsyMarketPlace.Domain.Orders.IOrderAddressRepository, EtsyMarketPlace.Infrastructure.Persistence.Repositories.EfPostgreSqlOrderAddressRepository>();
builder.Services.AddScoped<EtsyMarketPlace.Application.Orders.EtsyOrderService>();
builder.Services.AddScoped<EtsyMarketPlace.Application.Orders.IEtsyOrderFulfillmentService, EtsyMarketPlace.Application.Orders.EtsyOrderFulfillmentService>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Orders.IAiAddressParserService, EtsyMarketPlace.Infrastructure.Orders.AiAddressParserService>();

builder.Services.AddSingleton<EtsyMarketPlace.Application.Shipping.ShipmentCreationManager>(sp =>
{
    var manager = new EtsyMarketPlace.Application.Shipping.ShipmentCreationManager();
    var shipEntegraApi = sp.GetService<EtsyMarketPlace.Application.Shipping.IShipEntegraApiClient>();
    EtsyMarketPlace.Application.Shipping.IShippingSessionManager? shipEntegraSession = null;
    try { shipEntegraSession = sp.GetService<EtsyMarketPlace.Application.Shipping.IShippingSessionManager>(); } catch { }
    var shipEntegraProtector = EtsyMarketPlace.Application.Shipping.ShippingSecretProtector.Current;
    Func<string, string>? shipEntegraDecryptor = shipEntegraProtector is null
        ? null
        : new Func<string, string>(cipher => shipEntegraProtector.Unprotect(cipher));
    manager.RegisterProvider(new EtsyMarketPlace.Application.Shipping.ShipEntegraShipmentCreationProvider(
        shipEntegraApi,
        shipEntegraSession,
        shipEntegraDecryptor));
    var arasApi = sp.GetService<EtsyMarketPlace.Application.Shipping.IArasGlobalApiClient>();
    if (arasApi != null)
    {
        manager.RegisterProvider(new EtsyMarketPlace.Application.Shipping.ArasGlobalShipmentCreationProvider(arasApi));
    }
    var shiptomoreApi = sp.GetService<EtsyMarketPlace.Application.Shipping.IShiptomoreOfficialApi>();
    if (shiptomoreApi != null)
    {
        manager.RegisterProvider(new EtsyMarketPlace.Application.Shipping.ShiptomoreOfficialShipmentCreationProvider(shiptomoreApi));
    }
    return manager;
});
builder.Services.AddSingleton<EtsyMarketPlace.Application.Shipping.IShippingSessionManager, EtsyMarketPlace.Infrastructure.Shipping.PuppeteerShippingSessionManager>();
builder.Services.AddScoped<EtsyMarketPlace.Application.Shipping.IShipmentOrchestratorService, EtsyMarketPlace.Application.Shipping.ShipmentOrchestratorService>();

builder.Services.AddHttpClient<EtsyMarketPlace.Application.MarketResearch.IEtsyMarketApiClient, EtsyMarketPlace.Infrastructure.MarketResearch.EtsyMarketApiClient>();
builder.Services.AddScoped<EtsyMarketPlace.Application.MarketResearch.IMarketResearchService, EtsyMarketPlace.Application.MarketResearch.MarketResearchService>();
builder.Services.AddScoped<EtsyMarketPlace.Application.MarketResearch.IMarketKeywordTrackingService, EtsyMarketPlace.Application.MarketResearch.MarketKeywordTrackingService>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.AiUsage.IAiBalanceService, EtsyMarketPlace.Application.AiUsage.AiBalanceService>();
builder.Services.AddHostedService<EtsyIntegrationDatabaseInitializer>();
builder.Services.AddScoped<McpToolHandler>();
builder.Services.AddHttpClient<EtsyMarketPlace.Application.Mcp.IEtsyOfficialMcpClient, EtsyMarketPlace.Infrastructure.Mcp.EtsyOfficialMcpClient>();
builder.Services.AddScoped<EtsyMarketPlace.Application.Mcp.IMcpServerService, EtsyMarketPlace.Application.Mcp.McpServerService>();

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
        Description = "Google Gemini Spark (gemini.google.com/spark/apps) ve harici sistemler iÃ§in Model Context Protocol (MCP) ve Finansal Raporlama API'si."
    });

    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "VDS API anahtarÄ±. Ä°stek baÅŸlÄ±ÄŸÄ±nda 'X-API-KEY: {anahtar}' ÅŸeklinde gÃ¶nderilmelidir.",
        Name = "X-API-KEY",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Bearer yetkilendirme baÅŸlÄ±ÄŸÄ±. 'Bearer {token}' formatÄ±nda girilmelidir.",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey", doc),
            new List<string>()
        },
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});
var app = builder.Build();

// 4.1. PostgreSQL Otomatik Tablo Başlatıcı
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EtsyMarketPlaceDbContext>();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    try
    {
        await db.Database.MigrateAsync(cts.Token);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "PostgreSQL EF Core MigrateAsync uyarısı (mevcut şema olabilir): {Message}", ex.Message);
    }

    try
    {
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(
            db.Database,
            "ALTER TABLE order_costs ADD COLUMN IF NOT EXISTS packaging_cost numeric(18,4) NULL;",
            cts.Token);

        await AccountingPostgresSchemaBootstrapper.EnsureTablesExistAsync(db, app.Logger, cts.Token);

        var migrator = scope.ServiceProvider.GetRequiredService<PostgreSqlDataMigrator>();
        var migrationRes = await migrator.MigrateAndSeedAsync();
        app.Logger.LogInformation("PostgreSQL Veri Tohumlama ve Göç: {Message}", migrationRes.Message);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "PostgreSQL otomatik oluşturma uyarısı (Postgres henüz aktif olmayabilir): {Message}", ex.Message);
    }
}

// 5. Güvenlik Hattı: proxy başlıkları → (opsiyonel) HTTPS/HSTS → CORS → kimlik doğrulama → yetkilendirme
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Configuration.GetValue<bool>("Security:RequireHttps"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();

var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (string.Equals(name, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
        else if (name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
};

app.UseDefaultFiles();
app.UseStaticFiles(staticFileOptions);
app.UseMiddleware<ClientVersionGateMiddleware>();
app.UseMiddleware<SwaggerBasicAuthMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EtsyMarketPlace API v1");
    c.RoutePrefix = "swagger";
});
app.UseAuthentication();
app.UseAuthorization();

// 6. Controller Rotalarını Eşle
app.MapControllers();
app.MapPost("/api/system/migrate-postgres", async (EtsyMarketPlace.Infrastructure.Database.Postgres.PostgreSqlDataMigrator migrator) => Results.Ok(await migrator.MigrateAndSeedAsync()))
    .AllowAnonymous();

// 7. Angular Web Kokpiti SPA Fallback
app.MapFallbackToFile("index.html", staticFileOptions).AllowAnonymous();

app.Run();

public partial class Program { }
