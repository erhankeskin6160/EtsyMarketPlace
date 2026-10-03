namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed record TelegramShopSettings(
    string ShopId,
    string BotTokenMasked,
    string ChatId,
    bool IsEnabled,
    bool NotifyOnOrders,
    bool NotifyOnStock,
    bool DailyBriefEnabled,
    DateTimeOffset UpdatedAt);

public sealed record SaveTelegramSettingsRequest(
    string ShopId,
    string? BotToken,
    string ChatId,
    bool IsEnabled,
    bool NotifyOnOrders,
    bool NotifyOnStock,
    bool DailyBriefEnabled);

public sealed record CarrierSessionRecord(
    string ShopId,
    string CarrierId,
    string CredentialsMasked,
    string AccountNo,
    string ServiceLevel,
    bool IsConnected,
    DateTimeOffset UpdatedAt);

public sealed record SaveCarrierSessionRequest(
    string ShopId,
    string CarrierId,
    string? Credentials,
    string AccountNo,
    string ServiceLevel,
    bool IsConnected);

public sealed record EtsyAppCredentialsRecord(
    string ShopId,
    string KeystringMasked,
    string SecretMasked,
    string RedirectUri,
    DateTimeOffset UpdatedAt);

public sealed record SaveEtsyAppCredentialsRequest(
    string ShopId,
    string? Keystring,
    string? SharedSecret,
    string? RedirectUri);

public interface IShopSettingsRepository
{
    Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default);
    Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default);
}
