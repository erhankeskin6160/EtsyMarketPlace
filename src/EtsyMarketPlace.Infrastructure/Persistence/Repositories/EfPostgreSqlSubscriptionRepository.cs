using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL 16 EF Core tabanlı Abonelik ve Sanal POS veri erişim katmanı.
/// Eksik tablo (42P01) durumunda şemayı otomatik oluşturur; PostgreSQL erişilemezse SQLite yedeği üzerinden çalışır.
/// </summary>
public sealed class EfPostgreSqlSubscriptionRepository(
    EtsyMarketPlaceDbContext context,
    ILogger<EfPostgreSqlSubscriptionRepository> logger,
    SqliteSubscriptionStore? fallbackStore = null) : ISubscriptionRepository
{
    private const string UndefinedTableSqlState = "42P01";

    public Task<IReadOnlyList<SubscriptionPlan>> GetAllPlansAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<IReadOnlyList<SubscriptionPlan>>(
            async () =>
            {
                var query = context.SubscriptionPlans.AsNoTracking();
                if (activeOnly)
                    query = query.Where(p => p.IsActive);

                var rows = await query.OrderBy(p => p.DisplayOrder).ToListAsync(cancellationToken);
                if (rows.Count == 0)
                {
                    await SubscriptionPostgresSchemaBootstrapper.EnsureTablesExistAsync(context, logger, cancellationToken);
                    rows = await query.OrderBy(p => p.DisplayOrder).ToListAsync(cancellationToken);
                }

                return rows.Count > 0 ? rows : SubscriptionFeatureKeys.GetDefaultPlans();
            },
            store => store.GetAllPlansAsync(activeOnly, cancellationToken),
            () => SubscriptionFeatureKeys.GetDefaultPlans(),
            "GetAllPlans",
            cancellationToken);
    }

    public Task<SubscriptionPlan?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default)
    {
        var key = planId.Trim().ToLowerInvariant();
        return ExecuteWithResilienceAsync<SubscriptionPlan?>(
            async () =>
            {
                var plan = await context.SubscriptionPlans.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
                if (plan is null)
                {
                    await SubscriptionPostgresSchemaBootstrapper.EnsureTablesExistAsync(context, logger, cancellationToken);
                    plan = await context.SubscriptionPlans.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
                }
                return plan;
            },
            store => store.GetPlanByIdAsync(key, cancellationToken),
            () => SubscriptionFeatureKeys.GetDefaultPlans().FirstOrDefault(p => p.Id == key),
            $"GetPlanById({key})",
            cancellationToken);
    }

    public Task<SubscriptionPlan> UpsertPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        var key = plan.Id.Trim().ToLowerInvariant();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
                if (existing is null)
                {
                    plan.Id = key;
                    plan.UpdatedAtUtc = DateTime.UtcNow;
                    context.SubscriptionPlans.Add(plan);
                    await context.SaveChangesAsync(cancellationToken);
                    return plan;
                }

                existing.Name = plan.Name;
                existing.Tagline = plan.Tagline;
                existing.BadgeText = plan.BadgeText;
                existing.AccentColor = plan.AccentColor;
                existing.MonthlyPriceUsd = plan.MonthlyPriceUsd;
                existing.YearlyPriceUsd = plan.YearlyPriceUsd;
                existing.MonthlyPriceTry = plan.MonthlyPriceTry;
                existing.YearlyPriceTry = plan.YearlyPriceTry;
                existing.MaxShops = plan.MaxShops;
                existing.MonthlyAiTokenQuota = plan.MonthlyAiTokenQuota;
                existing.MaxMonthlyAiListings = plan.MaxMonthlyAiListings;
                existing.MaxBatchQueueSize = plan.MaxBatchQueueSize;
                existing.AllowedFeaturesJson = plan.AllowedFeaturesJson;
                existing.IsPopular = plan.IsPopular;
                existing.IsActive = plan.IsActive;
                existing.DisplayOrder = plan.DisplayOrder;
                existing.UpdatedAtUtc = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpsertPlanAsync(plan, cancellationToken),
            null,
            $"UpsertPlan({key})",
            cancellationToken);
    }

    public Task<UserSubscription?> GetUserSubscriptionAsync(string userId, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<UserSubscription?>(
            () => context.UserSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken),
            store => store.GetUserSubscriptionAsync(userId, cancellationToken),
            () => null,
            $"GetUserSubscription({userId})",
            cancellationToken);
    }

    public Task<UserSubscription> UpsertUserSubscriptionAsync(
        UserSubscription subscription,
        int? syncedMonthlyAiQuota = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == subscription.UserId, cancellationToken);

                if (existing is null)
                {
                    subscription.Id = string.IsNullOrWhiteSpace(subscription.Id) ? Guid.NewGuid().ToString("N") : subscription.Id;
                    subscription.UpdatedAtUtc = DateTime.UtcNow;
                    context.UserSubscriptions.Add(subscription);
                }
                else
                {
                    existing.PlanId = subscription.PlanId;
                    existing.Status = subscription.Status;
                    existing.BillingCycle = subscription.BillingCycle;
                    existing.CurrentPeriodStartUtc = subscription.CurrentPeriodStartUtc;
                    existing.CurrentPeriodEndUtc = subscription.CurrentPeriodEndUtc;
                    existing.AutoRenew = subscription.AutoRenew;
                    existing.CustomMaxShopsOverride = subscription.CustomMaxShopsOverride;
                    existing.CustomAiTokenQuotaOverride = subscription.CustomAiTokenQuotaOverride;
                    existing.PaymentProvider = subscription.PaymentProvider;
                    existing.LastSessionId = subscription.LastSessionId;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    subscription = existing;
                }

                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == subscription.UserId, cancellationToken);
                if (user is not null)
                {
                    user.SubscriptionPlanId = subscription.PlanId;
                    user.SubscriptionStatus = subscription.Status;
                    user.SubscriptionExpiresAt = subscription.CurrentPeriodEndUtc;
                    if (syncedMonthlyAiQuota.HasValue)
                    {
                        user.MonthlyAiTokenQuota = syncedMonthlyAiQuota.Value;
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
                return subscription;
            },
            store => store.UpsertUserSubscriptionAsync(subscription, syncedMonthlyAiQuota, cancellationToken),
            null,
            $"UpsertUserSubscription({subscription.UserId})",
            cancellationToken);
    }

    public Task<SubscriptionTransaction> AddTransactionAsync(SubscriptionTransaction transaction, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            async () =>
            {
                context.SubscriptionTransactions.Add(transaction);
                await context.SaveChangesAsync(cancellationToken);
                return transaction;
            },
            store => store.AddTransactionAsync(transaction, cancellationToken),
            null,
            $"AddTransaction({transaction.SessionId})",
            cancellationToken);
    }

    public Task<SubscriptionTransaction?> GetTransactionBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<SubscriptionTransaction?>(
            () => context.SubscriptionTransactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.SessionId == sessionId, cancellationToken),
            store => store.GetTransactionBySessionIdAsync(sessionId, cancellationToken),
            () => null,
            $"GetTransactionBySessionId({sessionId})",
            cancellationToken);
    }

    public Task<SubscriptionTransaction> UpdateTransactionAsync(SubscriptionTransaction transaction, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.SubscriptionTransactions
                    .FirstOrDefaultAsync(t => t.SessionId == transaction.SessionId, cancellationToken);

                if (existing is null)
                {
                    context.SubscriptionTransactions.Add(transaction);
                    await context.SaveChangesAsync(cancellationToken);
                    return transaction;
                }

                existing.Status = transaction.Status;
                existing.CardMaskedNumber = transaction.CardMaskedNumber;
                existing.CardHolderName = transaction.CardHolderName;
                existing.PaidAtUtc = transaction.PaidAtUtc;
                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpdateTransactionAsync(transaction, cancellationToken),
            null,
            $"UpdateTransaction({transaction.SessionId})",
            cancellationToken);
    }

    public Task<IReadOnlyList<SubscriptionTransaction>> GetTransactionsByUserAsync(string userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<IReadOnlyList<SubscriptionTransaction>>(
            async () => await context.SubscriptionTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAtUtc)
                .ThenByDescending(t => t.Id)
                .Take(limit)
                .ToListAsync(cancellationToken),
            store => store.GetTransactionsByUserAsync(userId, limit, cancellationToken),
            () => Array.Empty<SubscriptionTransaction>(),
            $"GetTransactionsByUser({userId})",
            cancellationToken);
    }

    public Task<PaymentGatewayConfig?> GetPaymentGatewayConfigAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<PaymentGatewayConfig?>(
            () => context.PaymentGatewayConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == "default", cancellationToken),
            store => store.GetPaymentGatewayConfigAsync(cancellationToken),
            () => null,
            "GetPaymentGatewayConfig",
            cancellationToken);
    }

    public Task<PaymentGatewayConfig> UpsertPaymentGatewayConfigAsync(
        PaymentGatewayConfig config,
        CancellationToken cancellationToken = default)
    {
        config.Id = "default";
        config.UpdatedAtUtc = DateTime.UtcNow;

        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.PaymentGatewayConfigs.FirstOrDefaultAsync(c => c.Id == "default", cancellationToken);
                if (existing is null)
                {
                    context.PaymentGatewayConfigs.Add(config);
                    await context.SaveChangesAsync(cancellationToken);
                    return config;
                }

                existing.ActiveProvider = config.ActiveProvider;
                existing.IsSandbox = config.IsSandbox;
                existing.IyzicoApiKey = config.IyzicoApiKey;
                existing.IyzicoSecretKey = config.IyzicoSecretKey;
                existing.IyzicoBaseUrl = config.IyzicoBaseUrl;
                existing.PaytrMerchantId = config.PaytrMerchantId;
                existing.PaytrMerchantKey = config.PaytrMerchantKey;
                existing.PaytrMerchantSalt = config.PaytrMerchantSalt;
                existing.StripeSecretKey = config.StripeSecretKey;
                existing.UpdatedAtUtc = config.UpdatedAtUtc;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpsertPaymentGatewayConfigAsync(config, cancellationToken),
            null,
            "UpsertPaymentGatewayConfig",
            cancellationToken);
    }

    private async Task<T> ExecuteWithResilienceAsync<T>(
        Func<Task<T>> pgAction,
        Func<SqliteSubscriptionStore, Task<T>> sqliteAction,
        Func<T>? noFallbackResult,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await pgAction();
        }
        catch (Exception ex) when (IsUndefinedTableException(ex))
        {
            context.ChangeTracker.Clear();
            logger.LogWarning(ex, "PostgreSQL abonelik tabloları eksik (42P01), {Operation} öncesi otomatik oluşturuluyor.", operationName);
            var created = await SubscriptionPostgresSchemaBootstrapper.EnsureTablesExistAsync(context, logger, cancellationToken);
            if (created)
            {
                try
                {
                    return await pgAction();
                }
                catch (Exception retryEx) when (IsDatabaseUnavailableException(retryEx))
                {
                    context.ChangeTracker.Clear();
                    return await FallbackOrDefaultAsync(retryEx, sqliteAction, noFallbackResult, operationName);
                }
            }

            return await FallbackOrDefaultAsync(ex, sqliteAction, noFallbackResult, operationName);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            context.ChangeTracker.Clear();
            return await FallbackOrDefaultAsync(ex, sqliteAction, noFallbackResult, operationName);
        }
    }

    private async Task<T> FallbackOrDefaultAsync<T>(
        Exception ex,
        Func<SqliteSubscriptionStore, Task<T>> sqliteAction,
        Func<T>? noFallbackResult,
        string operationName)
    {
        if (fallbackStore is not null)
        {
            logger.LogWarning(ex, "PostgreSQL {Operation} başarısız; SQLite abonelik yedeği kullanılıyor: {Message}", operationName, ex.Message);
            return await sqliteAction(fallbackStore);
        }

        if (noFallbackResult is not null)
        {
            return noFallbackResult();
        }

        throw ex;
    }

    private static bool IsUndefinedTableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException pgEx && pgEx.SqlState == UndefinedTableSqlState)
                return true;
        }
        return false;
    }

    private static bool IsDatabaseUnavailableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException ||
                current is System.Net.Sockets.SocketException ||
                current is TimeoutException ||
                current.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
