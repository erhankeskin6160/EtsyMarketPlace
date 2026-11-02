namespace EtsyMarketPlace.Api.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Security;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

public sealed class ConfigurationStatusService : IConfigurationStatusService
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<SecurityOptions> _securityOptions;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IOptions<EtsyApiOptions> _etsyOptions;
    private readonly JwtTokenService? _jwtService;

    public ConfigurationStatusService(
        IConfiguration configuration,
        IOptions<SecurityOptions> securityOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<EtsyApiOptions> etsyOptions,
        JwtTokenService? jwtService = null)
    {
        _configuration = configuration;
        _securityOptions = securityOptions;
        _jwtOptions = jwtOptions;
        _etsyOptions = etsyOptions;
        _jwtService = jwtService;
    }

    public Task<ConfigStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = new ConfigStatusResult();
        var components = result.Components;
        var missingRequired = result.MissingRequired;
        var missingOptional = result.MissingOptional;

        // 1. Veritabanı (PostgreSQL)
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
        else
        {
            components["database"] = new ConfigComponentStatus
            {
                IsConfigured = true,
                Status = "Healthy",
                Summary = "PostgreSQL bağlantı dizesi tanımlı."
            };
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

        // 5. Genel Durum Belirleme (Healthy, Degraded, Unhealthy)
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

        return Task.FromResult(result);
    }
}
