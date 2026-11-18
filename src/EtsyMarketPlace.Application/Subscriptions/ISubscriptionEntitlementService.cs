namespace EtsyMarketPlace.Application.Subscriptions;

/// <summary>
/// Kullanıcının abonelik paketine göre özellik erişimi, mağaza sınırı ve AI token kotasını hesaplayan servis.
/// </summary>
public interface ISubscriptionEntitlementService
{
    Task<UserSubscriptionSummaryDto> GetEntitlementSummaryAsync(
        string userId,
        string? roleHint = null,
        string? usernameHint = null,
        CancellationToken cancellationToken = default);

    Task<FeatureAccessCheckDto> CheckFeatureAccessAsync(
        string userId,
        string featureKey,
        string? roleHint = null,
        CancellationToken cancellationToken = default);

    Task<ShopLimitCheckDto> CheckShopLimitAsync(
        string userId,
        int requestedShopCount = 1,
        string? roleHint = null,
        CancellationToken cancellationToken = default);

    Task<AiQuotaCheckDto> CheckAiTokenQuotaAsync(
        string userId,
        int requiredTokens = 1,
        string? roleHint = null,
        CancellationToken cancellationToken = default);
}
