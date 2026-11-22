namespace EtsyMarketPlace.Api.Security;

using System;
using System.Security.Claims;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Http;

/// <summary>
/// HttpContext.User taleplerinden aktif kullanıcı kimliğini okuyan ICurrentUserAccessor somutlaştırması.
/// </summary>
public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Username =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true &&
        !string.IsNullOrWhiteSpace(UserId);

    public bool IsApiClient =>
        string.Equals(UserId, "api-client", StringComparison.OrdinalIgnoreCase);
}
