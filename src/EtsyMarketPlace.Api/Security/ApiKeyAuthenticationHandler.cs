using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Api.Security;

/// <summary>
/// Makine-makine istemciler (masaüstü veri aktarımı, MCP, otomasyon) için X-Api-Key kimlik doğrulaması.
/// Anahtar yalnızca <c>Security:ApiKey</c> yapılandırmasından okunur; kod içinde sabit/varsayılan
/// anahtar YOKTUR. Yapılandırma boşsa bu şema ile kimlik doğrulama yapılmaz (fail-closed).
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string HeaderName = "X-Api-Key";

    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            // Yapılandırılmış anahtar yoksa bu şema ile giriş yapılamaz.
            Logger.LogWarning("Security:ApiKey tanımlı değil; X-Api-Key kimlik doğrulaması devre dışı.");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(HeaderName, out var providedValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = providedValues.ToString().Trim();
        if (providedKey.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!FixedTimeEquals(providedKey, configuredKey.Trim()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz API anahtarı."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "api-client"),
            new(ClaimTypes.Name, "api-client"),
            new(ClaimTypes.Role, UserRoles.Admin)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);

        // Uzunluk farkı erken döner; içerik karşılaştırması sabit zamanlıdır.
        if (providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":\"Yetkisiz erişim: geçerli bir Bearer token veya X-Api-Key gerekli.\"}");
    }
}