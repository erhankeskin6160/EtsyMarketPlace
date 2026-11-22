namespace EtsyMarketPlace.Application.AiSettings;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Aktif kullanıcıya göre AI ayarları JSON'unu çözümleyen merkezi servis.
/// JWT ile giriş yapmış kullanıcılar için SADECE kendi user_ai_settings kaydını döner (kesin izolasyon).
/// Yalnızca X-Api-Key makine istemcisi (api-client) veya arka plan görevi için shop_ai_settings yedeğine düşer.
/// </summary>
public interface IUserAiSettingsResolver
{
    Task<string?> GetEffectiveAiSettingsJsonAsync(string? shopId = null, CancellationToken cancellationToken = default);
}

public sealed class UserAiSettingsResolver : IUserAiSettingsResolver
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserAiSettingsRepository _userAiSettingsRepo;
    private readonly IShopSettingsRepository? _shopSettingsRepo;

    public UserAiSettingsResolver(
        ICurrentUserAccessor currentUser,
        IUserAiSettingsRepository userAiSettingsRepo,
        IShopSettingsRepository? shopSettingsRepo = null)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _userAiSettingsRepo = userAiSettingsRepo ?? throw new ArgumentNullException(nameof(userAiSettingsRepo));
        _shopSettingsRepo = shopSettingsRepo;
    }

    public async Task<string?> GetEffectiveAiSettingsJsonAsync(string? shopId = null, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (!string.IsNullOrWhiteSpace(userId) && !_currentUser.IsApiClient)
        {
            // Katı kullanıcı izolasyonu: JWT kullanıcısı hiçbir zaman başka bir kullanıcının
            // veya ortak shop_ai_settings kaydının API anahtarını devralmaz.
            return await _userAiSettingsRepo.GetUserAiSettingsJsonAsync(userId.Trim(), cancellationToken);
        }

        // Makine-makine (X-Api-Key) veya HttpContext dışı arka plan görevi geriye uyumluluk yedeği
        if (_shopSettingsRepo is not null && !string.IsNullOrWhiteSpace(shopId))
        {
            return await _shopSettingsRepo.GetAiSettingsJsonAsync(shopId.Trim(), cancellationToken);
        }

        return null;
    }
}
