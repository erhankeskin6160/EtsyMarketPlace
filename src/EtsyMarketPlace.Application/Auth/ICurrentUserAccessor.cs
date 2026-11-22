namespace EtsyMarketPlace.Application.Auth;

/// <summary>
/// Geçerli HTTP isteğindeki kimliği doğrulanmış kullanıcı bilgilerine erişim soyutlaması.
/// Clean Architecture gereği Application ve Infrastructure katmanları HttpContext'e doğrudan bağımlı olmaz.
/// </summary>
public interface ICurrentUserAccessor
{
    string? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    bool IsApiClient { get; }
}
