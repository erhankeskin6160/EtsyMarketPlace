using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace EtsyMarketPlace.Api.Hosting;

/// <summary>
/// VDS API'nin, başlatma argümanı verilmediğinde de dış erişime açık sabit portta dinlemesini sağlar.
/// </summary>
public static class ApiDefaultUrlExtensions
{
    public const string DefaultApiUrl = "http://0.0.0.0:5263";

    /// <summary>
    /// --urls, ASPNETCORE_URLS / DOTNET_URLS veya Kestrel:Endpoints yapılandırılmamışsa varsayılan URL'yi döner; aksi halde null.
    /// </summary>
    public static string? ResolveDefaultUrlIfMissing(IConfiguration configuration)
    {
        var configuredUrls = configuration["urls"];
        var hasKestrelEndpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().Any();
        return string.IsNullOrWhiteSpace(configuredUrls) && !hasKestrelEndpoints ? DefaultApiUrl : null;
    }

    public static IWebHostBuilder UseApiDefaultUrlIfMissing(this IWebHostBuilder webHost, IConfiguration configuration)
    {
        var defaultUrl = ResolveDefaultUrlIfMissing(configuration);
        return defaultUrl is null ? webHost : webHost.UseUrls(defaultUrl);
    }
}
