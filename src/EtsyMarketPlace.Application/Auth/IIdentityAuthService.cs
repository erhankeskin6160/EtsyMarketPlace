using System.Net;

namespace EtsyMarketPlace.Application.Auth;

/// <summary>
/// ASP.NET Core Identity işleminin standart sonuç modeli.
/// </summary>
public sealed record IdentityOperationResult<T>(
    bool Succeeded,
    T? Data = default,
    string? ErrorMessage = null,
    HttpStatusCode StatusCode = HttpStatusCode.OK)
{
    public static IdentityOperationResult<T> Ok(T data) => new(true, data, null, HttpStatusCode.OK);
    public static IdentityOperationResult<T> Fail(string errorMessage, HttpStatusCode statusCode) => new(false, default, errorMessage, statusCode);
}

/// <summary>
/// Clean Architecture (DIP) gereği ASP.NET Core Identity (UserManager / RoleManager)
/// operasyonlarını Application katmanına soyutlayan kimlik doğrulama portu.
/// </summary>
public interface IIdentityAuthService
{
    Task<IdentityOperationResult<AppUser>> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        string? remoteIpAddress = null,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<AppUser>> RegisterAsync(
        string username,
        string email,
        string password,
        string? shopId,
        CancellationToken cancellationToken = default);

    Task<bool> LogoutAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> ValidateSecurityStampAsync(string userId, string? securityStamp, CancellationToken cancellationToken = default);

    void InvalidateSecurityStampCache(string userId);
}
