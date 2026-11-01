namespace EtsyMarketPlace.Api.Middleware;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class ClientVersionGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly ILogger<ClientVersionGateMiddleware> _logger;

    public const string HeaderClientType = "X-Client-Type";
    public const string HeaderClientVersion = "X-Client-Version";

    public ClientVersionGateMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<ClientVersionGateMiddleware> logger)
    {
        _next = next;
        _config = config;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Health, Root ve Swagger uç noktaları hiçbir zaman engellenmez
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(HeaderClientType, out var clientTypeVals))
        {
            var clientType = clientTypeVals.ToString().Trim();
            if (clientType.Equals("chrome-extension", StringComparison.OrdinalIgnoreCase))
            {
                var minExtVersionStr = _config["ClientCompatibility:MinExtensionVersion"] ?? "2.0.0";
                var hasClientVersion = context.Request.Headers.TryGetValue(HeaderClientVersion, out var clientVersionVals);
                var clientVersionStr = hasClientVersion ? clientVersionVals.ToString().Trim() : string.Empty;

                if (!IsVersionSupported(clientVersionStr, minExtVersionStr))
                {
                    _logger.LogWarning("Reddedilen eski Chrome Extension isteği: ClientVersion={Version}, MinSupported={MinVersion}, Path={Path}",
                        clientVersionStr, minExtVersionStr, path);

                    context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var responseBody = new
                    {
                        error = "CLIENT_UPGRADE_REQUIRED",
                        message = $"Etsy Chrome Uzantısı sürümünüz (v{(string.IsNullOrEmpty(clientVersionStr) ? "1.x" : clientVersionStr)}) artık desteklenmemektedir. Lütfen en az v{minExtVersionStr} sürümüne güncelleyin.",
                        minSupportedVersion = minExtVersionStr,
                        clientVersion = clientVersionStr
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(responseBody));
                    return;
                }
            }
        }

        await _next(context);
    }

    public static bool IsVersionSupported(string clientVersionStr, string minVersionStr)
    {
        if (string.IsNullOrWhiteSpace(clientVersionStr))
            return false;

        if (Version.TryParse(clientVersionStr, out var clientVersion) &&
            Version.TryParse(minVersionStr, out var minVersion))
        {
            return clientVersion >= minVersion;
        }

        var clientParts = clientVersionStr.Split('.');
        var minParts = minVersionStr.Split('.');
        for (int i = 0; i < Math.Max(clientParts.Length, minParts.Length); i++)
        {
            int cVal = i < clientParts.Length && int.TryParse(clientParts[i], out var c) ? c : 0;
            int mVal = i < minParts.Length && int.TryParse(minParts[i], out var m) ? m : 0;
            if (cVal > mVal) return true;
            if (cVal < mVal) return false;
        }

        return true;
    }
}
