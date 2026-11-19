using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Api.Security;

/// <summary>
/// JWT (HS256) + ASP.NET Core Identity SecurityStamp doğrulamasını ASP.NET Core kimlik doğrulama hattına bağlar.
/// Geçersiz, süresi dolmuş veya oturumu kapatılmış (revoked) token'da 401 ve tutarlı JSON gövdesi döner.
/// </summary>
public sealed class BearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly IIdentityAuthService? _identityAuthService;

    public BearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtTokenService jwtTokenService,
        IIdentityAuthService? identityAuthService = null)
        : base(options, logger, encoder)
    {
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _identityAuthService = identityAuthService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        var (isValid, principal, userId, _, _) = _jwtTokenService.ValidateToken(token);
        if (!isValid || principal is null || string.IsNullOrWhiteSpace(userId))
        {
            return AuthenticateResult.Fail("Geçersiz veya süresi dolmuş Bearer token.");
        }

        // ASP.NET Core Identity SecurityStamp doğrulaması (çıkış/rol değişikliği/askıya alma anında token iptali)
        if (_identityAuthService is not null)
        {
            var secStamp = principal.FindFirst("sec_stamp")?.Value;
            var stampValid = await _identityAuthService.ValidateSecurityStampAsync(
                userId,
                secStamp,
                Context.RequestAborted);

            if (!stampValid)
            {
                return AuthenticateResult.Fail("Oturum sonlandırılmış veya geçersiz Bearer token.");
            }
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        await Response.WriteAsync("{\"error\":\"Yetkisiz erişim: geçerli bir Bearer token gerekli.\"}");
    }
}
