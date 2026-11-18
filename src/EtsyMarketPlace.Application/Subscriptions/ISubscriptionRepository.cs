using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Application.Subscriptions;

/// <summary>
/// Abonelik paketleri, kullanıcı abonelikleri ve fatura/ödeme hareketleri kalıcılık sözleşmesi.
/// </summary>
public interface ISubscriptionRepository
{
    Task<IReadOnlyList<SubscriptionPlan>> GetAllPlansAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> UpsertPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetUserSubscriptionAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserSubscription> UpsertUserSubscriptionAsync(UserSubscription subscription, int? syncedMonthlyAiQuota = null, CancellationToken cancellationToken = default);

    Task<SubscriptionTransaction> AddTransactionAsync(SubscriptionTransaction transaction, CancellationToken cancellationToken = default);
    Task<SubscriptionTransaction?> GetTransactionBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<SubscriptionTransaction> UpdateTransactionAsync(SubscriptionTransaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionTransaction>> GetTransactionsByUserAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
}
