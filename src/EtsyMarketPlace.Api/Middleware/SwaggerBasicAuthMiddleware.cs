namespace EtsyMarketPlace.Api.Middleware;

using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Swagger UI ve swagger.json uç noktalarını yetkisiz erişimlere karşı koruyan middleware.
/// Geliştirme (Development) ortamında varsayılan olarak açıktır.
/// Production ortamında HTTP Basic Authentication veya geçerli X-API-KEY başlığı gerektirir.
/// (Bkz. GitHub Issue #47)
/// </summary>
public class SwaggerBasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SwaggerBasicAuthMiddleware> _logger;

    public SwaggerBasicAuthMiddleware(
        RequestDelegate next,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<SwaggerBasicAuthMiddleware> logger)
    {
        _next = next;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Yalnızca /swagger ile başlayan istekler denetlenir
        if (!path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var isEnabled = _config.GetValue<bool?>("Security:Swagger:Enabled") ?? true;
        if (!isEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Swagger dökümantasyonu bu ortamda devre dışı bırakılmıştır.");
            return;
        }

        var isDevelopment = _env.IsDevelopment();
        var requireAuth = isDevelopment
            ? _config.GetValue<bool>("Security:Swagger:RequireAuthInDevelopment", false)
            : _config.GetValue<bool>("Security:Swagger:RequireAuth", true);

        if (!requireAuth)
        {
            await _next(context);
            return;
        }

        // 1. Alternatif yetkilendirme: Geçerli X-API-KEY başlığı varsa doğrudan geçiş ver
        if (context.Request.Headers.TryGetValue("X-API-KEY", out var apiKeyHeaderVals))
        {
            var providedApiKey = apiKeyHeaderVals.ToString().Trim();
            var configuredApiKey = _config["Security:ApiKey"];

            if (!string.IsNullOrWhiteSpace(configuredApiKey) &&
                string.Equals(providedApiKey, configuredApiKey, StringComparison.Ordinal))
            {
                await _next(context);
                return;
            }
        }

        // 2. HTTP Basic Authentication Kontrolü
        var expectedUsername = _config["Security:Swagger:Username"] ?? "admin";
        var expectedPassword = _config["Security:Swagger:Password"] ?? _config["Security:ApiKey"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(expectedPassword))
        {
            _logger.LogWarning("Swagger kimlik doğrulaması zorunlu ancak 'Security:Swagger:Password' veya 'Security:ApiKey' yapılandırılmamış. Erişim engellendi.");
            await ReturnUnauthorizedAsync(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authHeaderVals))
        {
            var authHeader = authHeaderVals.ToString().Trim();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedCredentials = authHeader["Basic ".Length..].Trim();
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                    var separatorIndex = decoded.IndexOf(':');
                    if (separatorIndex >= 0)
                    {
                        var username = decoded[..separatorIndex];
                        var password = decoded[(separatorIndex + 1)..];

                        if (string.Equals(username, expectedUsername, StringComparison.Ordinal) &&
                            string.Equals(password, expectedPassword, StringComparison.Ordinal))
                        {
                            await _next(context);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Swagger Basic Auth başlığı çözümlenirken hata oluştu.");
                }
            }
        }

        await ReturnUnauthorizedAsync(context);
    }

    private static async Task ReturnUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"EtsyMarketPlace API Documentation\", charset=\"UTF-8\"";
        context.Response.ContentType = "application/json; charset=utf-8";

        var responseObj = new
        {
            status = 401,
            title = "Unauthorized",
            detail = "Swagger API dökümantasyonuna erişim için kullanıcı adı ve şifre gereklidir."
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(responseObj));
    }
}
