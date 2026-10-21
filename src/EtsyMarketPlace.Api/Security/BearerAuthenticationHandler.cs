using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Api.Security;

/// <summary>
/// Özel JWT (HS256) doğrulamasını ASP.NET Core kimlik doğrulama hattına bağlar.
/// Geçersiz/süresi dolmuş token'da 401 ve tutarlı JSON gövdesi döner.
/// </summary>
public sealed class BearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly JwtTokenService _jwtTokenService;

    public BearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtTokenService jwtTokenService)
        : base(options, logger, encoder)
    {
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header["Bearer ".Length..].Trim();
        var (isValid, principal, _, _, _) = _jwtTokenService.ValidateToken(token);
        if (!isValid || principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz veya süresi dolmuş Bearer token."));
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":\"Yetkisiz erişim: geçerli bir Bearer token gerekli.\"}");
    }
}
