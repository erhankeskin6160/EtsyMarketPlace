namespace EtsyMarketPlace.Application.AiSettings;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Kullanıcı bazlı şifreli AI model ve API anahtarları deposu (PostgreSQL: user_ai_settings).
/// </summary>
public interface IUserAiSettingsRepository
{
    Task<string?> GetUserAiSettingsJsonAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveUserAiSettingsJsonAsync(string userId, string settingsJson, CancellationToken cancellationToken = default);
    Task DeleteUserAiSettingsAsync(string userId, CancellationToken cancellationToken = default);
}
