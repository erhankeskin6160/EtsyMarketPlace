namespace EtsyMarketPlace.Api.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Security;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class ConfigurationStatusService : IConfigurationStatusService
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<SecurityOptions> _securityOptions;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IOptions<EtsyApiOptions> _etsyOptions;
    private readonly JwtTokenService? _jwtService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ConfigurationStatusService(
        IConfiguration configuration,
        IOptions<SecurityOptions> securityOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<EtsyApiOptions> etsyOptions,
        IServiceScopeFactory scopeFactory,
        JwtTokenService? jwtService = null)
    {
        _configuration = configuration;
        _securityOptions = securityOptions;
        _jwtOptions = jwtOptions;
        _etsyOptions = etsyOptions;
        _scopeFactory = scopeFactory;
        _jwtService = jwtService;
    }

    public async Task<ConfigStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = new ConfigStatusResult();
        var components = result.Components;
        var missingRequired = result.MissingRequired;
        var missingOptional = result.MissingOptional;

        // 1. Veritabanı (PostgreSQL Canlı Bağlantı Denetimi - GitHub Issue #43)
        var pgConn = _configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(pgConn))
        {
            components["database"] = new ConfigComponentStatus
            {
                IsConfigured = false,
                Status = "Unhealthy",
                Summary = "ConnectionStrings:PostgreSql tanımlı değil.",
                Action = "appsettings.json veya VDS ortam değişkenlerinde PostgreSQL bağlantı dizesini tanımlayın."
            };
            missingRequired.Add("ConnectionStrings:PostgreSql");
        }
        else if (_configuration.GetValue<bool>("HealthChecks:BypassDatabaseProbe", false))
        {
            components["database"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = "PostgreSQL bağlantı denetimi yapılandırma gereği baypas edildi (Test/Mock modu)."
            };
        }
        else
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<EtsyMarketPlaceDbContext>();
                if (db != null)
                {
                    var canConnect = await db.Database.CanConnectAsync(cts.Token);
                    if (canConnect)
                    {
                        components["database"] = new ConfigComponentStatus
                        {
                            IsConfigured = true,
                            Status = "Healthy",
                            Summary = "PostgreSQL veritabanı aktif ve canlı yanıt veriyor."
                        };
                    }
                    else
                    {
                        components["database"] = new ConfigComponentStatus
                        {
                            IsConfigured = true,
                            Status = "Unhealthy",
                            Summary = "PostgreSQL veritabanına bağlanılamadı (CanConnect = false).",
                            Action = "PostgreSQL servisinin çalıştığından ve bağlantı dizesinin doğruluğundan emin olun."
                        };
                        missingRequired.Add("Database:PostgreSQL");
                    }
                }
                else
                {
                    components["database"] = new ConfigComponentStatus
                    {
                        IsConfigured = true,
                        Status = "Healthy",
                        Summary = "PostgreSQL bağlantı dizesi mevcut."
                    };
                }
            }
            catch (Exception ex)
            {
                components["database"] = new ConfigComponentStatus
                {
                    IsConfigured = true,
                    Status = "Unhealthy",
                    Summary = $"PostgreSQL canlı bağlantı hatası: {ex.Message}",
                    Action = "PostgreSQL servisini kontrol edin."
                };
                missingRequired.Add("Database:PostgreSQL");
            }
        }

        // 2. Güvenlik (ApiKey & JWT)
        var apiKey = _securityOptions.Value.ApiKey;
        var isJwtActive = _jwtService != null || !string.IsNullOrWhiteSpace(_jwtOptions.Value.Secret);
        var isApiKeyActive = !string.IsNullOrWhiteSpace(apiKey);

        if (!isApiKeyActive || !isJwtActive)
        {
            components["security"] = new ConfigComponentStatus
            {
                IsConfigured = false,
                Status = "Unhealthy",
                Summary = "Security:ApiKey veya JwtTokenService eksik.",
                Action = "Security:ApiKey ve JWT imza anahtarını doğrulayın."
            };
            if (!isApiKeyActive) missingRequired.Add("Security:ApiKey");
            if (!isJwtActive) missingRequired.Add("Jwt:Secret");
        }
        else
        {
            components["security"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = "VDS API Key ve JWT koruması aktif."
            };
        }

        // 3. Etsy Entegrasyonu (Dış Servis)
        var etsy = _etsyOptions.Value;
        var hasEtsyKey = !string.IsNullOrWhiteSpace(etsy.ApiKey);

        if (!hasEtsyKey)
        {
            components["etsy"] = new ConfigComponentStatus
            {
                IsConfigured = false,
                Status = "Degraded",
                Summary = "Etsy API Keystring (Client ID) yapılandırılmamış.",
                Action = "Ayarlar > Etsy API sayfasından veya appsettings.json dosyasından Etsy Client ID kaydedin."
            };
            missingOptional.Add("Etsy:ApiKey");
        }
        else
        {
            components["etsy"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = $"Etsy API hazır (Mağaza ID: {etsy.ShopId})."
            };
        }

        // 4. Gemini AI / LLM (Opsiyonel)
        var geminiKey = _configuration["Gemini:ApiKey"] ?? _configuration["Ai:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(geminiKey))
        {
            components["geminiAi"] = new ConfigComponentStatus
            {
                IsConfigured = false,
                Status = "Optional",
                Summary = "Gemini AI API anahtarı boş bırakılmış (opsiyonel).",
                Action = "AI listing açıklaması ve görsel optimizasyonu için Ayarlar'dan Gemini API Key girebilirsiniz."
            };
            missingOptional.Add("Gemini:ApiKey");
        }
        else
        {
            components["geminiAi"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = "Google Gemini AI entegrasyonu aktif."
            };
        }

        // 5. Disk ve Veri Klasörü Yazma İzni Denetimi
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EtsyMarketPlace");
            Directory.CreateDirectory(dataDir);
            components["storage"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = $"Kalıcı veri depolama alanı erişilebilir."
            };
        }
        catch (Exception ex)
        {
            components["storage"] = new ConfigComponentStatus
            {
                IsConfigured = false,
                Status = "Degraded",
                Summary = $"Depolama dizini erişim uyarısı: {ex.Message}"
            };
            missingOptional.Add("Storage:Permissions");
        }

        // 6. Genel Durum Belirleme (Healthy, Degraded, Unhealthy)
        if (missingRequired.Count > 0)
        {
            result.Status = "Unhealthy";
        }
        else if (components.Values.Any(c => c.Status == "Degraded"))
        {
            result.Status = "Degraded";
        }
        else
        {
            result.Status = "Healthy";
        }

        return result;
    }
}
