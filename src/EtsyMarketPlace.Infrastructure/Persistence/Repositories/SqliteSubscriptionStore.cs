using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// Abonelik paketleri, kullanıcı abonelikleri ve Sanal POS işlem geçmişi için SQLite yerel deposu (PostgreSQL yedeği).
/// </summary>
public sealed class SqliteSubscriptionStore : ISubscriptionRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _initialized;

    [ActivatorUtilitiesConstructor]
    public SqliteSubscriptionStore(IOptions<EtsyIntegrationOptions> options)
        : this(options.Value.ResolveDatabasePath())
    {
    }

    public SqliteSubscriptionStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);

            await using var ddlCmd = connection.CreateCommand();
            ddlCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS subscription_plans (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    tagline TEXT NOT NULL DEFAULT '',
                    badge_text TEXT NOT NULL DEFAULT '',
                    accent_color TEXT NOT NULL DEFAULT '#38bdf8',
                    monthly_price_usd REAL NOT NULL DEFAULT 0,
                    yearly_price_usd REAL NOT NULL DEFAULT 0,
                    monthly_price_try REAL NOT NULL DEFAULT 0,
                    yearly_price_try REAL NOT NULL DEFAULT 0,
                    max_shops INTEGER NOT NULL DEFAULT 1,
                    monthly_ai_token_quota INTEGER NOT NULL DEFAULT 100000,
                    max_monthly_ai_listings INTEGER NOT NULL DEFAULT 15,
                    max_batch_queue_size INTEGER NOT NULL DEFAULT 0,
                    allowed_features_json TEXT NOT NULL DEFAULT '[]',
                    is_popular INTEGER NOT NULL DEFAULT 0,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    display_order INTEGER NOT NULL DEFAULT 1,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS user_subscriptions (
                    id TEXT PRIMARY KEY,
                    user_id TEXT NOT NULL UNIQUE,
                    plan_id TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'Active',
                    billing_cycle TEXT NOT NULL DEFAULT 'Monthly',
                    start_date_utc TEXT NOT NULL,
                    current_period_start_utc TEXT NOT NULL,
                    current_period_end_utc TEXT NOT NULL,
                    auto_renew INTEGER NOT NULL DEFAULT 1,
                    custom_max_shops_override INTEGER NULL,
                    custom_ai_token_quota_override INTEGER NULL,
                    payment_provider TEXT NOT NULL DEFAULT 'StripeVirtualPos',
                    last_session_id TEXT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS subscription_transactions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL UNIQUE,
                    user_id TEXT NOT NULL,
                    username TEXT NOT NULL DEFAULT '',
                    plan_id TEXT NOT NULL,
                    plan_name TEXT NOT NULL DEFAULT '',
                    billing_cycle TEXT NOT NULL DEFAULT 'Monthly',
                    amount_usd REAL NOT NULL DEFAULT 0,
                    amount_try REAL NOT NULL DEFAULT 0,
                    currency TEXT NOT NULL DEFAULT 'USD',
                    exchange_rate REAL NOT NULL DEFAULT 35.0,
                    status TEXT NOT NULL DEFAULT 'Pending',
                    payment_provider TEXT NOT NULL DEFAULT 'StripeVirtualPos',
                    card_masked_number TEXT NULL,
                    card_holder_name TEXT NULL,
                    invoice_number TEXT NOT NULL DEFAULT '',
                    created_at_utc TEXT NOT NULL,
                    paid_at_utc TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_subscription_tx_user_created
                    ON subscription_transactions (user_id, created_at_utc DESC);
                """;
            await ddlCmd.ExecuteNonQueryAsync(cancellationToken);

            await SeedDefaultPlansAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllPlansAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = activeOnly
            ? "SELECT id, name, tagline, badge_text, accent_color, monthly_price_usd, yearly_price_usd, monthly_price_try, yearly_price_try, max_shops, monthly_ai_token_quota, max_monthly_ai_listings, max_batch_queue_size, allowed_features_json, is_popular, is_active, display_order, updated_at_utc FROM subscription_plans WHERE is_active = 1 ORDER BY display_order ASC;"
            : "SELECT id, name, tagline, badge_text, accent_color, monthly_price_usd, yearly_price_usd, monthly_price_try, yearly_price_try, max_shops, monthly_ai_token_quota, max_monthly_ai_listings, max_batch_queue_size, allowed_features_json, is_popular, is_active, display_order, updated_at_utc FROM subscription_plans ORDER BY display_order ASC;";

        var list = new List<SubscriptionPlan>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapPlan(reader));
        }
        return list;
    }

    public async Task<SubscriptionPlan?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, tagline, badge_text, accent_color, monthly_price_usd, yearly_price_usd,
                   monthly_price_try, yearly_price_try, max_shops, monthly_ai_token_quota,
                   max_monthly_ai_listings, max_batch_queue_size, allowed_features_json,
                   is_popular, is_active, display_order, updated_at_utc
            FROM subscription_plans WHERE id = $id LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", planId.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapPlan(reader) : null;
    }

    public async Task<SubscriptionPlan> UpsertPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await UpsertPlanCoreAsync(connection, plan, cancellationToken);
        return plan;
    }

    public async Task<UserSubscription?> GetUserSubscriptionAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, user_id, plan_id, status, billing_cycle, start_date_utc,
                   current_period_start_utc, current_period_end_utc, auto_renew,
                   custom_max_shops_override, custom_ai_token_quota_override,
                   payment_provider, last_session_id, updated_at_utc
            FROM user_subscriptions WHERE user_id = $userId LIMIT 1;
            """;
        command.Parameters.AddWithValue("$userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new UserSubscription
        {
            Id = reader.GetString(0),
            UserId = reader.GetString(1),
            PlanId = reader.GetString(2),
            Status = reader.GetString(3),
            BillingCycle = reader.GetString(4),
            StartDateUtc = ParseUtc(reader.GetString(5)),
            CurrentPeriodStartUtc = ParseUtc(reader.GetString(6)),
            CurrentPeriodEndUtc = ParseUtc(reader.GetString(7)),
            AutoRenew = reader.GetInt32(8) == 1,
            CustomMaxShopsOverride = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            CustomAiTokenQuotaOverride = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            PaymentProvider = reader.GetString(11),
            LastSessionId = reader.IsDBNull(12) ? null : reader.GetString(12),
            UpdatedAtUtc = ParseUtc(reader.GetString(13))
        };
    }

    public async Task<UserSubscription> UpsertUserSubscriptionAsync(
        UserSubscription subscription,
        int? syncedMonthlyAiQuota = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO user_subscriptions (
                id, user_id, plan_id, status, billing_cycle, start_date_utc,
                current_period_start_utc, current_period_end_utc, auto_renew,
                custom_max_shops_override, custom_ai_token_quota_override,
                payment_provider, last_session_id, updated_at_utc)
            VALUES (
                $id, $userId, $planId, $status, $billingCycle, $startDateUtc,
                $periodStartUtc, $periodEndUtc, $autoRenew,
                $customShops, $customQuota,
                $provider, $lastSessionId, $updatedAtUtc)
            ON CONFLICT(user_id) DO UPDATE SET
                plan_id = excluded.plan_id,
                status = excluded.status,
                billing_cycle = excluded.billing_cycle,
                current_period_start_utc = excluded.current_period_start_utc,
                current_period_end_utc = excluded.current_period_end_utc,
                auto_renew = excluded.auto_renew,
                custom_max_shops_override = excluded.custom_max_shops_override,
                custom_ai_token_quota_override = excluded.custom_ai_token_quota_override,
                payment_provider = excluded.payment_provider,
                last_session_id = excluded.last_session_id,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue("$id", string.IsNullOrWhiteSpace(subscription.Id) ? Guid.NewGuid().ToString("N") : subscription.Id);
        command.Parameters.AddWithValue("$userId", subscription.UserId);
        command.Parameters.AddWithValue("$planId", subscription.PlanId);
        command.Parameters.AddWithValue("$status", subscription.Status);
        command.Parameters.AddWithValue("$billingCycle", subscription.BillingCycle);
        command.Parameters.AddWithValue("$startDateUtc", subscription.StartDateUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$periodStartUtc", subscription.CurrentPeriodStartUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$periodEndUtc", subscription.CurrentPeriodEndUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$autoRenew", subscription.AutoRenew ? 1 : 0);
        command.Parameters.AddWithValue("$customShops", (object?)subscription.CustomMaxShopsOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("$customQuota", (object?)subscription.CustomAiTokenQuotaOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("$provider", subscription.PaymentProvider);
        command.Parameters.AddWithValue("$lastSessionId", (object?)subscription.LastSessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", subscription.UpdatedAtUtc.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return subscription;
    }

    public async Task<SubscriptionTransaction> AddTransactionAsync(
        SubscriptionTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO subscription_transactions (
                session_id, user_id, username, plan_id, plan_name, billing_cycle,
                amount_usd, amount_try, currency, exchange_rate, status,
                payment_provider, card_masked_number, card_holder_name,
                invoice_number, created_at_utc, paid_at_utc)
            VALUES (
                $sessionId, $userId, $username, $planId, $planName, $billingCycle,
                $amountUsd, $amountTry, $currency, $exchangeRate, $status,
                $provider, $cardMasked, $cardHolder,
                $invoiceNo, $createdAtUtc, $paidAtUtc);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$sessionId", transaction.SessionId);
        command.Parameters.AddWithValue("$userId", transaction.UserId);
        command.Parameters.AddWithValue("$username", transaction.Username);
        command.Parameters.AddWithValue("$planId", transaction.PlanId);
        command.Parameters.AddWithValue("$planName", transaction.PlanName);
        command.Parameters.AddWithValue("$billingCycle", transaction.BillingCycle);
        command.Parameters.AddWithValue("$amountUsd", Convert.ToDouble(transaction.AmountUsd));
        command.Parameters.AddWithValue("$amountTry", Convert.ToDouble(transaction.AmountTry));
        command.Parameters.AddWithValue("$currency", transaction.Currency);
        command.Parameters.AddWithValue("$exchangeRate", Convert.ToDouble(transaction.ExchangeRate));
        command.Parameters.AddWithValue("$status", transaction.Status);
        command.Parameters.AddWithValue("$provider", transaction.PaymentProvider);
        command.Parameters.AddWithValue("$cardMasked", (object?)transaction.CardMaskedNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$cardHolder", (object?)transaction.CardHolderName ?? DBNull.Value);
        command.Parameters.AddWithValue("$invoiceNo", transaction.InvoiceNumber);
        command.Parameters.AddWithValue("$createdAtUtc", transaction.CreatedAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$paidAtUtc", (object?)transaction.PaidAtUtc?.ToUniversalTime().ToString("O") ?? DBNull.Value);

        var idObj = await command.ExecuteScalarAsync(cancellationToken);
        transaction.Id = Convert.ToInt64(idObj ?? 0);
        return transaction;
    }

    public async Task<SubscriptionTransaction?> GetTransactionBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, user_id, username, plan_id, plan_name, billing_cycle,
                   amount_usd, amount_try, currency, exchange_rate, status,
                   payment_provider, card_masked_number, card_holder_name,
                   invoice_number, created_at_utc, paid_at_utc
            FROM subscription_transactions WHERE session_id = $sessionId LIMIT 1;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapTransaction(reader) : null;
    }

    public async Task<SubscriptionTransaction> UpdateTransactionAsync(
        SubscriptionTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE subscription_transactions SET
                status = $status,
                card_masked_number = $cardMasked,
                card_holder_name = $cardHolder,
                paid_at_utc = $paidAtUtc
            WHERE session_id = $sessionId;
            """;

        command.Parameters.AddWithValue("$sessionId", transaction.SessionId);
        command.Parameters.AddWithValue("$status", transaction.Status);
        command.Parameters.AddWithValue("$cardMasked", (object?)transaction.CardMaskedNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$cardHolder", (object?)transaction.CardHolderName ?? DBNull.Value);
        command.Parameters.AddWithValue("$paidAtUtc", (object?)transaction.PaidAtUtc?.ToUniversalTime().ToString("O") ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return transaction;
    }

    public async Task<IReadOnlyList<SubscriptionTransaction>> GetTransactionsByUserAsync(
        string userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, user_id, username, plan_id, plan_name, billing_cycle,
                   amount_usd, amount_try, currency, exchange_rate, status,
                   payment_provider, card_masked_number, card_holder_name,
                   invoice_number, created_at_utc, paid_at_utc
            FROM subscription_transactions
            WHERE user_id = $userId
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$limit", limit);

        var list = new List<SubscriptionTransaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapTransaction(reader));
        }
        return list;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task SeedDefaultPlansAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM subscription_plans;";
        var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count > 0) return;

        foreach (var plan in SubscriptionFeatureKeys.GetDefaultPlans())
        {
            await UpsertPlanCoreAsync(connection, plan, cancellationToken);
        }
    }

    private static async Task UpsertPlanCoreAsync(
        SqliteConnection connection,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO subscription_plans (
                id, name, tagline, badge_text, accent_color,
                monthly_price_usd, yearly_price_usd, monthly_price_try, yearly_price_try,
                max_shops, monthly_ai_token_quota, max_monthly_ai_listings, max_batch_queue_size,
                allowed_features_json, is_popular, is_active, display_order, updated_at_utc)
            VALUES (
                $id, $name, $tagline, $badge, $color,
                $mUsd, $yUsd, $mTry, $yTry,
                $maxShops, $aiQuota, $aiListings, $batchSize,
                $features, $popular, $active, $order, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                tagline = excluded.tagline,
                badge_text = excluded.badge_text,
                accent_color = excluded.accent_color,
                monthly_price_usd = excluded.monthly_price_usd,
                yearly_price_usd = excluded.yearly_price_usd,
                monthly_price_try = excluded.monthly_price_try,
                yearly_price_try = excluded.yearly_price_try,
                max_shops = excluded.max_shops,
                monthly_ai_token_quota = excluded.monthly_ai_token_quota,
                max_monthly_ai_listings = excluded.max_monthly_ai_listings,
                max_batch_queue_size = excluded.max_batch_queue_size,
                allowed_features_json = excluded.allowed_features_json,
                is_popular = excluded.is_popular,
                is_active = excluded.is_active,
                display_order = excluded.display_order,
                updated_at_utc = excluded.updated_at_utc;
            """;

        cmd.Parameters.AddWithValue("$id", plan.Id);
        cmd.Parameters.AddWithValue("$name", plan.Name);
        cmd.Parameters.AddWithValue("$tagline", plan.Tagline);
        cmd.Parameters.AddWithValue("$badge", plan.BadgeText);
        cmd.Parameters.AddWithValue("$color", plan.AccentColor);
        cmd.Parameters.AddWithValue("$mUsd", Convert.ToDouble(plan.MonthlyPriceUsd));
        cmd.Parameters.AddWithValue("$yUsd", Convert.ToDouble(plan.YearlyPriceUsd));
        cmd.Parameters.AddWithValue("$mTry", Convert.ToDouble(plan.MonthlyPriceTry));
        cmd.Parameters.AddWithValue("$yTry", Convert.ToDouble(plan.YearlyPriceTry));
        cmd.Parameters.AddWithValue("$maxShops", plan.MaxShops);
        cmd.Parameters.AddWithValue("$aiQuota", plan.MonthlyAiTokenQuota);
        cmd.Parameters.AddWithValue("$aiListings", plan.MaxMonthlyAiListings);
        cmd.Parameters.AddWithValue("$batchSize", plan.MaxBatchQueueSize);
        cmd.Parameters.AddWithValue("$features", plan.AllowedFeaturesJson);
        cmd.Parameters.AddWithValue("$popular", plan.IsPopular ? 1 : 0);
        cmd.Parameters.AddWithValue("$active", plan.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$order", plan.DisplayOrder);
        cmd.Parameters.AddWithValue("$updatedAt", plan.UpdatedAtUtc.ToUniversalTime().ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SubscriptionPlan MapPlan(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.GetString(1),
        Tagline = reader.GetString(2),
        BadgeText = reader.GetString(3),
        AccentColor = reader.GetString(4),
        MonthlyPriceUsd = Convert.ToDecimal(reader.GetDouble(5)),
        YearlyPriceUsd = Convert.ToDecimal(reader.GetDouble(6)),
        MonthlyPriceTry = Convert.ToDecimal(reader.GetDouble(7)),
        YearlyPriceTry = Convert.ToDecimal(reader.GetDouble(8)),
        MaxShops = reader.GetInt32(9),
        MonthlyAiTokenQuota = reader.GetInt32(10),
        MaxMonthlyAiListings = reader.GetInt32(11),
        MaxBatchQueueSize = reader.GetInt32(12),
        AllowedFeaturesJson = reader.GetString(13),
        IsPopular = reader.GetInt32(14) == 1,
        IsActive = reader.GetInt32(15) == 1,
        DisplayOrder = reader.GetInt32(16),
        UpdatedAtUtc = ParseUtc(reader.GetString(17))
    };

    private static SubscriptionTransaction MapTransaction(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        SessionId = reader.GetString(1),
        UserId = reader.GetString(2),
        Username = reader.GetString(3),
        PlanId = reader.GetString(4),
        PlanName = reader.GetString(5),
        BillingCycle = reader.GetString(6),
        AmountUsd = Convert.ToDecimal(reader.GetDouble(7)),
        AmountTry = Convert.ToDecimal(reader.GetDouble(8)),
        Currency = reader.GetString(9),
        ExchangeRate = Convert.ToDecimal(reader.GetDouble(10)),
        Status = reader.GetString(11),
        PaymentProvider = reader.GetString(12),
        CardMaskedNumber = reader.IsDBNull(13) ? null : reader.GetString(13),
        CardHolderName = reader.IsDBNull(14) ? null : reader.GetString(14),
        InvoiceNumber = reader.GetString(15),
        CreatedAtUtc = ParseUtc(reader.GetString(16)),
        PaidAtUtc = reader.IsDBNull(17) ? null : ParseUtc(reader.GetString(17))
    };

    private static DateTime ParseUtc(string raw)
        => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto.UtcDateTime
            : DateTime.UtcNow;

    public ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
