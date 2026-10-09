using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class SqliteEtsyIntegrationStore : IEtsyTokenStore, IEtsyIntegrationRepository, IEtsyReportingService, IUserRepository, IShopSettingsRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _initialized;

    public SqliteEtsyIntegrationStore(IOptions<EtsyIntegrationOptions> options, IDataProtectionProvider protectionProvider)
    {
        var path = options.Value.ResolveDatabasePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        _protector = protectionProvider.CreateProtector("EtsyMarketPlace.EtsyOAuthToken.v2");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=5000;

                CREATE TABLE IF NOT EXISTS shop_connections (
                    shop_id TEXT PRIMARY KEY,
                    shop_name TEXT NOT NULL DEFAULT '',
                    user_id TEXT NOT NULL DEFAULT '',
                    last_synchronized_at TEXT NULL,
                    is_active INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS oauth_tokens (
                    shop_id TEXT PRIMARY KEY,
                    access_token TEXT NOT NULL,
                    refresh_token TEXT NOT NULL,
                    access_token_expires_at TEXT NOT NULL,
                    token_type TEXT NOT NULL DEFAULT 'Bearer'
                );

                CREATE TABLE IF NOT EXISTS sync_states (
                    shop_id TEXT NOT NULL,
                    data_type TEXT NOT NULL,
                    last_successful_sync_at TEXT NULL,
                    last_cursor_at TEXT NULL,
                    last_error TEXT NULL,
                    PRIMARY KEY(shop_id, data_type)
                );

                CREATE TABLE IF NOT EXISTS bank_payouts (
                    shop_id TEXT NOT NULL,
                    reference_id TEXT NOT NULL,
                    occurred_at TEXT NOT NULL,
                    amount REAL NOT NULL,
                    currency TEXT NOT NULL,
                    exchange_rate_to_try REAL NULL,
                    status TEXT NOT NULL,
                    description TEXT NOT NULL,
                    PRIMARY KEY(shop_id, reference_id)
                );

                CREATE TABLE IF NOT EXISTS financial_transactions (
                    shop_id TEXT NOT NULL,
                    reference_id TEXT NOT NULL,
                    occurred_at TEXT NOT NULL,
                    gross_sales REAL NOT NULL DEFAULT 0,
                    platform_fees REAL NOT NULL DEFAULT 0,
                    internal_ads_cost REAL NOT NULL DEFAULT 0,
                    external_ads_cost REAL NOT NULL DEFAULT 0,
                    product_cost REAL NOT NULL DEFAULT 0,
                    shipping_cost REAL NOT NULL DEFAULT 0,
                    refunds REAL NOT NULL DEFAULT 0,
                    currency TEXT NOT NULL,
                    PRIMARY KEY(shop_id, reference_id)
                );

                CREATE TABLE IF NOT EXISTS order_costs (
                    shop_id TEXT NOT NULL,
                    order_id TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    currency TEXT NOT NULL,
                    order_total REAL NOT NULL DEFAULT 0,
                    product_cost REAL NULL,
                    shipping_cost REAL NULL,
                    alert_reason TEXT NULL,
                    PRIMARY KEY(shop_id, order_id)
                );

                CREATE TABLE IF NOT EXISTS monthly_order_summaries (
                    shop_id TEXT NOT NULL,
                    year_month TEXT NOT NULL,
                    order_count INTEGER NOT NULL DEFAULT 0,
                    units_sold INTEGER NOT NULL DEFAULT 0,
                    gross_revenue REAL NOT NULL DEFAULT 0,
                    avg_order_value REAL NOT NULL DEFAULT 0,
                    shipped_orders INTEGER NOT NULL DEFAULT 0,
                    unfulfilled_orders INTEGER NOT NULL DEFAULT 0,
                    currency TEXT NOT NULL DEFAULT 'USD',
                    PRIMARY KEY(shop_id, year_month)
                );

                CREATE TABLE IF NOT EXISTS listing_traffic_daily (
                    shop_id TEXT NOT NULL,
                    listing_id INTEGER NOT NULL,
                    snapshot_date TEXT NOT NULL,
                    title TEXT NOT NULL DEFAULT '',
                    views INTEGER NOT NULL DEFAULT 0,
                    favorites INTEGER NOT NULL DEFAULT 0,
                    views_today INTEGER NOT NULL DEFAULT 0,
                    favorites_today INTEGER NOT NULL DEFAULT 0,
                    units_sold_month INTEGER NOT NULL DEFAULT 0,
                    revenue_month REAL NOT NULL DEFAULT 0,
                    conversion_rate REAL NOT NULL DEFAULT 0,
                    image_url TEXT NOT NULL DEFAULT '',
                    listing_url TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY(shop_id, listing_id, snapshot_date)
                );

                CREATE TABLE IF NOT EXISTS chart_snapshots (
                    shop_id TEXT NOT NULL,
                    chart_type TEXT NOT NULL,
                    period_start TEXT NOT NULL,
                    period_end TEXT NOT NULL,
                    image_png_base64 TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY(shop_id, chart_type)
                );

                CREATE TABLE IF NOT EXISTS shop_telegram_settings (
                    shop_id TEXT PRIMARY KEY,
                    encrypted_bot_token TEXT NOT NULL DEFAULT '',
                    chat_id TEXT NOT NULL DEFAULT '',
                    is_enabled INTEGER NOT NULL DEFAULT 0,
                    notify_on_orders INTEGER NOT NULL DEFAULT 1,
                    notify_on_stock INTEGER NOT NULL DEFAULT 1,
                    daily_brief_enabled INTEGER NOT NULL DEFAULT 1,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS shop_carrier_sessions (
                    shop_id TEXT NOT NULL,
                    carrier_id TEXT NOT NULL,
                    encrypted_credentials TEXT NOT NULL DEFAULT '',
                    account_no TEXT NOT NULL DEFAULT '',
                    service_level TEXT NOT NULL DEFAULT '',
                    is_connected INTEGER NOT NULL DEFAULT 0,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY(shop_id, carrier_id)
                );

                CREATE TABLE IF NOT EXISTS etsy_app_credentials (
                    shop_id TEXT PRIMARY KEY,
                    encrypted_keystring TEXT NOT NULL DEFAULT '',
                    encrypted_shared_secret TEXT NOT NULL DEFAULT '',
                    redirect_uri TEXT NOT NULL DEFAULT '',
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS listing_ai_audits (
                    shop_id TEXT NOT NULL,
                    listing_id TEXT NOT NULL,
                    title TEXT NOT NULL,
                    current_seo_score INTEGER NOT NULL DEFAULT 0,
                    optimized_seo_score INTEGER NOT NULL DEFAULT 0,
                    status TEXT NOT NULL DEFAULT '',
                    provider TEXT NOT NULL DEFAULT '',
                    model TEXT NOT NULL DEFAULT '',
                    result_json TEXT NOT NULL DEFAULT '{}',
                    audited_at TEXT NOT NULL,
                    PRIMARY KEY(shop_id, listing_id)
                );

                CREATE INDEX IF NOT EXISTS ix_listing_ai_audits_shop ON listing_ai_audits(shop_id);

                CREATE INDEX IF NOT EXISTS ix_bank_payouts_shop_date ON bank_payouts(shop_id, occurred_at);
                CREATE INDEX IF NOT EXISTS ix_financial_transactions_shop_date ON financial_transactions(shop_id, occurred_at);
                CREATE INDEX IF NOT EXISTS ix_order_costs_shop_date ON order_costs(shop_id, created_at);
                CREATE INDEX IF NOT EXISTS ix_listing_traffic_shop_date ON listing_traffic_daily(shop_id, snapshot_date);
                CREATE INDEX IF NOT EXISTS ix_monthly_orders_shop ON monthly_order_summaries(shop_id, year_month);

                CREATE TABLE IF NOT EXISTS app_users (
                    id TEXT PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    email TEXT NOT NULL UNIQUE,
                    password_hash TEXT NOT NULL,
                    password_salt TEXT NOT NULL,
                    role TEXT NOT NULL DEFAULT 'StoreOwner',
                    assigned_shop_ids TEXT NOT NULL DEFAULT '[]',
                    monthly_ai_token_quota INTEGER NOT NULL DEFAULT 500000,
                    used_ai_tokens INTEGER NOT NULL DEFAULT 0,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT NOT NULL,
                    last_login_at TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS audit_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id TEXT NOT NULL,
                    username TEXT NOT NULL,
                    action TEXT NOT NULL,
                    details TEXT NULL,
                    ip_address TEXT NULL,
                    timestamp TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_app_users_username ON app_users(username);
                CREATE INDEX IF NOT EXISTS ix_app_users_email ON app_users(email);
                CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp ON audit_logs(timestamp);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Seed default admin user if none exists
            await using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM app_users;";
                var userCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                if (userCount == 0)
                {
                    var saltBytes = RandomNumberGenerator.GetBytes(16);
                    var salt = Convert.ToBase64String(saltBytes);
                    var hashBytes = Rfc2898DeriveBytes.Pbkdf2("Admin123*!", saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
                    var hash = Convert.ToBase64String(hashBytes);

                    await using var seedCmd = connection.CreateCommand();
                    seedCmd.CommandText = """
                        INSERT INTO app_users (id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at)
                        VALUES ($id, $username, $email, $hash, $salt, $role, $shops, $quota, 0, 1, $createdAt);
                        """;
                    seedCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                    seedCmd.Parameters.AddWithValue("$username", "admin");
                    seedCmd.Parameters.AddWithValue("$email", "admin@etsymarketplace.local");
                    seedCmd.Parameters.AddWithValue("$hash", hash);
                    seedCmd.Parameters.AddWithValue("$salt", salt);
                    seedCmd.Parameters.AddWithValue("$role", UserRoles.Admin);
                    seedCmd.Parameters.AddWithValue("$shops", "[\"53236321\"]");
                    seedCmd.Parameters.AddWithValue("$quota", 2000000);
                    seedCmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
                    await seedCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            _initialized = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task<EtsyOAuthToken?> GetAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT access_token, refresh_token, access_token_expires_at, token_type FROM oauth_tokens WHERE shop_id = $shopId;";
        command.Parameters.AddWithValue("$shopId", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new EtsyOAuthToken(
            _protector.Unprotect(reader.GetString(0)),
            _protector.Unprotect(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            reader.GetString(3));
    }

    public async Task SaveAsync(string shopId, EtsyOAuthToken token, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO oauth_tokens(shop_id, access_token, refresh_token, access_token_expires_at, token_type)
            VALUES($shopId, $accessToken, $refreshToken, $expiresAt, $tokenType)
            ON CONFLICT(shop_id) DO UPDATE SET
                access_token = excluded.access_token,
                refresh_token = excluded.refresh_token,
                access_token_expires_at = excluded.access_token_expires_at,
                token_type = excluded.token_type;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$accessToken", _protector.Protect(token.AccessToken));
        command.Parameters.AddWithValue("$refreshToken", _protector.Protect(token.RefreshToken));
        command.Parameters.AddWithValue("$expiresAt", token.AccessTokenExpiresAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$tokenType", token.TokenType);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT last_cursor_at FROM sync_states WHERE shop_id = $shopId AND data_type = $dataType;";
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$dataType", dataType);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var cursor) ? cursor : null;
    }

    public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default) =>
        UpsertPayoutsAsync(payouts, cancellationToken);

    private async Task UpsertPayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var payout in payouts)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO bank_payouts(shop_id, reference_id, occurred_at, amount, currency, exchange_rate_to_try, status, description)
                VALUES($shopId, $referenceId, $occurredAt, $amount, $currency, $rate, $status, $description)
                ON CONFLICT(shop_id, reference_id) DO UPDATE SET
                    occurred_at=excluded.occurred_at, amount=excluded.amount, currency=excluded.currency,
                    exchange_rate_to_try=excluded.exchange_rate_to_try, status=excluded.status, description=excluded.description;
                """;
            AddPayoutParameters(command, payout);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in transactions)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO financial_transactions(shop_id, reference_id, occurred_at, gross_sales, platform_fees, internal_ads_cost, external_ads_cost, product_cost, shipping_cost, refunds, currency)
                VALUES($shopId, $referenceId, $occurredAt, $gross, $fees, $internalAds, $externalAds, $product, $shipping, $refunds, $currency)
                ON CONFLICT(shop_id, reference_id) DO UPDATE SET
                    occurred_at=excluded.occurred_at, gross_sales=excluded.gross_sales, platform_fees=excluded.platform_fees,
                    internal_ads_cost=excluded.internal_ads_cost, external_ads_cost=excluded.external_ads_cost,
                    product_cost=excluded.product_cost, shipping_cost=excluded.shipping_cost, refunds=excluded.refunds, currency=excluded.currency;
                """;
            command.Parameters.AddWithValue("$shopId", item.ShopId);
            command.Parameters.AddWithValue("$referenceId", item.ReferenceId);
            command.Parameters.AddWithValue("$occurredAt", item.OccurredAt.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$gross", (double)item.GrossSales);
            command.Parameters.AddWithValue("$fees", (double)item.PlatformFees);
            command.Parameters.AddWithValue("$internalAds", (double)item.InternalAdsCost);
            command.Parameters.AddWithValue("$externalAds", (double)item.ExternalAdsCost);
            command.Parameters.AddWithValue("$product", (double)item.ProductCost);
            command.Parameters.AddWithValue("$shipping", (double)item.ShippingCost);
            command.Parameters.AddWithValue("$refunds", (double)item.Refunds);
            command.Parameters.AddWithValue("$currency", item.Currency);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var item in alerts)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO order_costs(shop_id, order_id, created_at, currency, order_total, product_cost, shipping_cost, alert_reason)
                VALUES($shopId, $orderId, $createdAt, $currency, $total, $product, $shipping, $reason)
                ON CONFLICT(shop_id, order_id) DO UPDATE SET
                    created_at=excluded.created_at, currency=excluded.currency, order_total=excluded.order_total,
                    -- Null gelen maliyet, mevcut dolu degeri EZMEZ (kostuk guncellemesi farkli kaynaklardan gelebilir).
                    product_cost=COALESCE(excluded.product_cost, order_costs.product_cost),
                    shipping_cost=COALESCE(excluded.shipping_cost, order_costs.shipping_cost),
                    alert_reason=excluded.alert_reason;
                """;
            command.Parameters.AddWithValue("$shopId", item.ShopId);
            command.Parameters.AddWithValue("$orderId", item.OrderId);
            command.Parameters.AddWithValue("$createdAt", item.CreatedAt.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$currency", item.Currency);
            command.Parameters.AddWithValue("$total", (double)item.OrderTotal);
            command.Parameters.AddWithValue("$product", (object?)item.ProductCost ?? DBNull.Value);
            command.Parameters.AddWithValue("$shipping", (object?)item.ShippingCost ?? DBNull.Value);
            command.Parameters.AddWithValue("$reason", (object?)item.Reason ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sync_states(shop_id, data_type, last_successful_sync_at, last_cursor_at, last_error)
            VALUES($shopId, $dataType, CASE WHEN $error IS NULL THEN $now ELSE NULL END, $cursor, $error)
            ON CONFLICT(shop_id, data_type) DO UPDATE SET
                last_successful_sync_at = CASE WHEN excluded.last_error IS NULL THEN excluded.last_successful_sync_at ELSE sync_states.last_successful_sync_at END,
                last_cursor_at = excluded.last_cursor_at, last_error = excluded.last_error;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$dataType", dataType);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$cursor", cursor.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT reference_id, occurred_at, amount, currency, exchange_rate_to_try, status, description FROM bank_payouts WHERE shop_id=$shopId AND occurred_at >= $start AND occurred_at <= $end ORDER BY occurred_at DESC;";
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$start", startDate.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$end", endDate.ToUniversalTime().ToString("O"));
        var result = new List<EtsyBankPayout>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new EtsyBankPayout(shopId, reader.GetString(0), ParseDate(reader.GetString(1)), reader.GetDecimal(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.GetString(5), reader.GetString(6)));
        return result;
    }

    public async Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT gross_sales, platform_fees, internal_ads_cost, external_ads_cost, product_cost, shipping_cost, refunds, currency FROM financial_transactions WHERE shop_id=$shopId AND occurred_at >= $start AND occurred_at <= $end;";
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$start", startDate.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$end", endDate.ToUniversalTime().ToString("O"));
        decimal gross=0, fees=0, internalAds=0, externalAds=0, product=0, shipping=0, refunds=0;
        string? currency = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowCurrency = reader.GetString(7);
            if (currency is not null && !string.Equals(currency, rowCurrency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Farklı para birimleri aynı finansal raporda birleştirilemez.");
            currency = rowCurrency;
            gross += reader.GetDecimal(0); fees += reader.GetDecimal(1); internalAds += reader.GetDecimal(2); externalAds += reader.GetDecimal(3);
            product += reader.GetDecimal(4); shipping += reader.GetDecimal(5); refunds += reader.GetDecimal(6);
        }
        var net = gross - fees - internalAds - externalAds - product - shipping - refunds;
        var margin = gross == 0 ? 0 : net / gross * 100;

        // 1. En güncel döviz kurunu çek (bank_payouts tablosundaki kur veya varsayılan 48.25)
        decimal exchangeRate = 48.25m;
        await using (var rateCmd = connection.CreateCommand())
        {
            rateCmd.CommandText = "SELECT exchange_rate_to_try FROM bank_payouts WHERE shop_id=$shopId AND exchange_rate_to_try > 30 ORDER BY occurred_at DESC LIMIT 1;";
            rateCmd.Parameters.AddWithValue("$shopId", shopId);
            var rateObj = await rateCmd.ExecuteScalarAsync(cancellationToken);
            if (rateObj is not null && rateObj is not DBNull)
            {
                exchangeRate = Convert.ToDecimal(rateObj, CultureInfo.InvariantCulture);
            }
        }

        // 2. monthly_order_summaries tablosundan sipariş bazlı ciroyu çek (Masaüstü paneliyle birebir senkron için)
        decimal? orderGrossUSD = null;
        var startMonth = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var endMonth = endDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        await using (var orderCmd = connection.CreateCommand())
        {
            orderCmd.CommandText = "SELECT SUM(gross_revenue) FROM monthly_order_summaries WHERE shop_id=$shopId AND year_month >= $startMonth AND year_month <= $endMonth;";
            orderCmd.Parameters.AddWithValue("$shopId", shopId);
            orderCmd.Parameters.AddWithValue("$startMonth", startMonth);
            orderCmd.Parameters.AddWithValue("$endMonth", endMonth);
            var orderObj = await orderCmd.ExecuteScalarAsync(cancellationToken);
            if (orderObj is not null && orderObj is not DBNull)
            {
                orderGrossUSD = Convert.ToDecimal(orderObj, CultureInfo.InvariantCulture);
            }
        }

        decimal grossSalesTRY = Math.Round(gross * exchangeRate, 2);
        decimal netProfitTRY = Math.Round(net * exchangeRate, 2);

        decimal? orderGrossSalesTRY = orderGrossUSD.HasValue ? Math.Round(orderGrossUSD.Value * exchangeRate, 2) : null;
        decimal? orderNetProfitTRY = orderGrossUSD.HasValue
            ? Math.Round((orderGrossUSD.Value - fees - internalAds - externalAds - product - shipping - refunds) * exchangeRate, 2)
            : null;

        return new FinancialPerformance(
            startDate, endDate, currency ?? "USD",
            gross, fees, internalAds, externalAds, product, shipping, refunds, net, margin,
            grossSalesTRY, netProfitTRY,
            orderGrossUSD, orderGrossSalesTRY, orderNetProfitTRY, exchangeRate);
    }

    public async Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT order_id, created_at, currency, order_total, product_cost, shipping_cost, alert_reason FROM order_costs WHERE shop_id=$shopId AND (product_cost IS NULL OR shipping_cost IS NULL) ORDER BY created_at;";
        command.Parameters.AddWithValue("$shopId", shopId);
        var result = new List<EtsyOrderCostAlert>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new EtsyOrderCostAlert(shopId, reader.GetString(0), ParseDate(reader.GetString(1)), reader.GetString(2), reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? "Maliyet bilgisi eksik." : reader.GetString(6)));
        return result;
    }

    public async Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT order_id, product_cost, shipping_cost FROM order_costs WHERE shop_id=$shopId AND (product_cost IS NOT NULL OR shipping_cost IS NOT NULL);";
        command.Parameters.AddWithValue("$shopId", shopId);
        var result = new Dictionary<long, EtsyDashboardOrderCost>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!long.TryParse(reader.GetString(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderId))
                continue;

            var productCost = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            var shippingCost = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            result[orderId] = new EtsyDashboardOrderCost(productCost, shippingCost);
        }

        return result;
    }

    public async Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO order_costs(shop_id, order_id, created_at, currency, order_total, product_cost, shipping_cost, alert_reason)
            VALUES($shopId, $orderId, $now, 'USD', 0, $productCost, $shippingCost, $reason)
            ON CONFLICT(shop_id, order_id) DO UPDATE SET
                product_cost = excluded.product_cost,
                shipping_cost = excluded.shipping_cost,
                alert_reason = COALESCE(excluded.alert_reason, order_costs.alert_reason);
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$orderId", orderId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$productCost", (object?)productCost ?? DBNull.Value);
        command.Parameters.AddWithValue("$shippingCost", (object?)shippingCost ?? DBNull.Value);
        command.Parameters.AddWithValue("$reason", (object?)notes ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default)
    {
        var start = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
        var performance = await GetFinancialPerformanceAsync(shopId, start, start.AddDays(1).AddTicks(-1), cancellationToken);
        var payouts = await GetBankPayoutsAsync(shopId, start, start.AddDays(1).AddTicks(-1), cancellationToken);
        var alerts = await GetUnfulfilledCostAlertsAsync(shopId, cancellationToken);
        var score = performance.GrossSales == 0 ? 0 : Math.Clamp((int)Math.Round(performance.NetProfitMargin), 0, 100);
        return new DailyShopBrief(
            date, score, score >= 80 ? "Mükemmel" : score >= 50 ? "Dikkat" : "Riskli",
            0, performance.GrossSales, performance.NetProfit, payouts.Sum(x => x.Amount), alerts.Count,
            performance.GrossSalesTRY, performance.NetProfitTRY, performance.ExchangeRateUsed);
    }

    public async Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default)
    {
        if (summaries.Count == 0) return;
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO monthly_order_summaries (
                shop_id, year_month, order_count, units_sold, gross_revenue,
                avg_order_value, shipped_orders, unfulfilled_orders, currency
            ) VALUES (
                $shopId, $yearMonth, $orderCount, $unitsSold, $grossRevenue,
                $avgOrderValue, $shippedOrders, $unfulfilledOrders, $currency
            )
            ON CONFLICT(shop_id, year_month) DO UPDATE SET
                order_count = excluded.order_count,
                units_sold = excluded.units_sold,
                gross_revenue = excluded.gross_revenue,
                avg_order_value = excluded.avg_order_value,
                shipped_orders = excluded.shipped_orders,
                unfulfilled_orders = excluded.unfulfilled_orders,
                currency = excluded.currency;
            """;
        var pShopId = command.Parameters.Add("$shopId", SqliteType.Text);
        var pYearMonth = command.Parameters.Add("$yearMonth", SqliteType.Text);
        var pOrderCount = command.Parameters.Add("$orderCount", SqliteType.Integer);
        var pUnitsSold = command.Parameters.Add("$unitsSold", SqliteType.Integer);
        var pGrossRevenue = command.Parameters.Add("$grossRevenue", SqliteType.Real);
        var pAvgOrderValue = command.Parameters.Add("$avgOrderValue", SqliteType.Real);
        var pShippedOrders = command.Parameters.Add("$shippedOrders", SqliteType.Integer);
        var pUnfulfilledOrders = command.Parameters.Add("$unfulfilledOrders", SqliteType.Integer);
        var pCurrency = command.Parameters.Add("$currency", SqliteType.Text);

        foreach (var item in summaries)
        {
            pShopId.Value = item.ShopId;
            pYearMonth.Value = item.YearMonth;
            pOrderCount.Value = item.OrderCount;
            pUnitsSold.Value = item.UnitsSold;
            pGrossRevenue.Value = (double)item.GrossRevenue;
            pAvgOrderValue.Value = (double)item.AvgOrderValue;
            pShippedOrders.Value = item.ShippedOrders;
            pUnfulfilledOrders.Value = item.UnfulfilledOrders;
            pCurrency.Value = item.Currency;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT year_month, order_count, units_sold, gross_revenue,
                   avg_order_value, shipped_orders, unfulfilled_orders, currency
            FROM monthly_order_summaries
            WHERE shop_id = $shopId
            ORDER BY year_month DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$limit", months);
        var result = new List<EtsyMonthlyOrderSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new EtsyMonthlyOrderSummary(
                shopId,
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetString(7)));
        }
        return result;
    }

    public async Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO listing_traffic_daily (
                shop_id, listing_id, snapshot_date, title, views, favorites,
                views_today, favorites_today, units_sold_month, revenue_month,
                conversion_rate, image_url, listing_url
            ) VALUES (
                $shopId, $listingId, $snapshotDate, $title, $views, $favorites,
                $viewsToday, $favoritesToday, $unitsSoldMonth, $revenueMonth,
                $conversionRate, $imageUrl, $listingUrl
            )
            ON CONFLICT(shop_id, listing_id, snapshot_date) DO UPDATE SET
                title = excluded.title,
                views = excluded.views,
                favorites = excluded.favorites,
                views_today = excluded.views_today,
                favorites_today = excluded.favorites_today,
                units_sold_month = excluded.units_sold_month,
                revenue_month = excluded.revenue_month,
                conversion_rate = excluded.conversion_rate,
                image_url = excluded.image_url,
                listing_url = excluded.listing_url;
            """;
        var pShopId = command.Parameters.Add("$shopId", SqliteType.Text);
        var pListingId = command.Parameters.Add("$listingId", SqliteType.Integer);
        var pSnapshotDate = command.Parameters.Add("$snapshotDate", SqliteType.Text);
        var pTitle = command.Parameters.Add("$title", SqliteType.Text);
        var pViews = command.Parameters.Add("$views", SqliteType.Integer);
        var pFavorites = command.Parameters.Add("$favorites", SqliteType.Integer);
        var pViewsToday = command.Parameters.Add("$viewsToday", SqliteType.Integer);
        var pFavoritesToday = command.Parameters.Add("$favoritesToday", SqliteType.Integer);
        var pUnitsSoldMonth = command.Parameters.Add("$unitsSoldMonth", SqliteType.Integer);
        var pRevenueMonth = command.Parameters.Add("$revenueMonth", SqliteType.Real);
        var pConversionRate = command.Parameters.Add("$conversionRate", SqliteType.Real);
        var pImageUrl = command.Parameters.Add("$imageUrl", SqliteType.Text);
        var pListingUrl = command.Parameters.Add("$listingUrl", SqliteType.Text);

        foreach (var item in records)
        {
            pShopId.Value = item.ShopId;
            pListingId.Value = item.ListingId;
            pSnapshotDate.Value = item.SnapshotDate;
            pTitle.Value = item.Title;
            pViews.Value = item.Views;
            pFavorites.Value = item.Favorites;
            pViewsToday.Value = item.ViewsToday;
            pFavoritesToday.Value = item.FavoritesToday;
            pUnitsSoldMonth.Value = item.UnitsSoldMonth;
            pRevenueMonth.Value = (double)item.RevenueMonth;
            pConversionRate.Value = (double)item.ConversionRate;
            pImageUrl.Value = item.ImageUrl;
            pListingUrl.Value = item.ListingUrl;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(snapshotDate))
        {
            command.CommandText = "SELECT MAX(snapshot_date) FROM listing_traffic_daily WHERE shop_id = $shopId;";
            command.Parameters.AddWithValue("$shopId", shopId);
            var maxObj = await command.ExecuteScalarAsync(cancellationToken);
            snapshotDate = maxObj is string str && !string.IsNullOrWhiteSpace(str) ? str : DateTime.UtcNow.ToString("yyyy-MM-dd");
            command.Parameters.Clear();
        }

        command.CommandText = """
            SELECT listing_id, snapshot_date, title, views, favorites,
                   views_today, favorites_today, units_sold_month, revenue_month,
                   conversion_rate, image_url, listing_url
            FROM listing_traffic_daily
            WHERE shop_id = $shopId AND snapshot_date = $snapshotDate
            ORDER BY views_today DESC, views DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$snapshotDate", snapshotDate);
        command.Parameters.AddWithValue("$limit", limit);

        var result = new List<EtsyListingTrafficRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new EtsyListingTrafficRecord(
                shopId,
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetString(10),
                reader.GetString(11)));
        }
        return result;
    }

    public async Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chart_snapshots (
                shop_id, chart_type, period_start, period_end, image_png_base64, updated_at
            ) VALUES (
                $shopId, $chartType, $periodStart, $periodEnd, $image, $updatedAt
            )
            ON CONFLICT(shop_id, chart_type) DO UPDATE SET
                period_start = excluded.period_start,
                period_end = excluded.period_end,
                image_png_base64 = excluded.image_png_base64,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$shopId", snapshot.ShopId);
        command.Parameters.AddWithValue("$chartType", snapshot.ChartType);
        command.Parameters.AddWithValue("$periodStart", snapshot.PeriodStart.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$periodEnd", snapshot.PeriodEnd.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$image", snapshot.ImagePngBase64);
        command.Parameters.AddWithValue("$updatedAt", snapshot.UpdatedAt.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT period_start, period_end, image_png_base64, updated_at
            FROM chart_snapshots
            WHERE shop_id = $shopId AND chart_type = $chartType;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        command.Parameters.AddWithValue("$chartType", chartType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new EtsyChartSnapshot(
            shopId,
            chartType,
            ParseDate(reader.GetString(0)),
            ParseDate(reader.GetString(1)),
            reader.GetString(2),
            ParseDate(reader.GetString(3)));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken) => await InitializeAsync(cancellationToken);

    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void AddPayoutParameters(SqliteCommand command, EtsyBankPayout payout)
    {
        command.Parameters.AddWithValue("$shopId", payout.ShopId);
        command.Parameters.AddWithValue("$referenceId", payout.ReferenceId);
        command.Parameters.AddWithValue("$occurredAt", payout.OccurredAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$amount", (double)payout.Amount);
        command.Parameters.AddWithValue("$currency", payout.Currency);
        command.Parameters.AddWithValue("$rate", (object?)payout.ExchangeRateToTry ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", payout.Status);
        command.Parameters.AddWithValue("$description", payout.Description);
    }

    public async Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at, last_login_at FROM app_users WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return MapUser(reader);
    }

    public async Task<AppUser?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at, last_login_at FROM app_users WHERE LOWER(username) = LOWER($id) OR LOWER(email) = LOWER($id);";
        command.Parameters.AddWithValue("$id", identifier.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return MapUser(reader);
    }

    public async Task<IReadOnlyList<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at, last_login_at FROM app_users ORDER BY created_at DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<AppUser>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(MapUser(reader));
        }
        return users;
    }

    public async Task CreateUserAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_users (id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at, last_login_at)
            VALUES ($id, $username, $email, $hash, $salt, $role, $shops, $quota, $usedTokens, $isActive, $createdAt, $lastLoginAt);
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$salt", user.PasswordSalt);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$shops", JsonSerializer.Serialize(user.AssignedShopIds));
        command.Parameters.AddWithValue("$quota", user.MonthlyAiTokenQuota);
        command.Parameters.AddWithValue("$usedTokens", user.UsedAiTokens);
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$lastLoginAt", (object?)user.LastLoginAt?.ToUniversalTime().ToString("O") ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE app_users SET
                email = $email,
                role = $role,
                assigned_shop_ids = $shops,
                monthly_ai_token_quota = $quota,
                used_ai_tokens = $usedTokens,
                is_active = $isActive
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$shops", JsonSerializer.Serialize(user.AssignedShopIds));
        command.Parameters.AddWithValue("$quota", user.MonthlyAiTokenQuota);
        command.Parameters.AddWithValue("$usedTokens", user.UsedAiTokens);
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(string id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE app_users SET last_login_at = $lastLogin WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$lastLogin", lastLoginAt.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_logs (user_id, username, action, details, ip_address, timestamp)
            VALUES ($userId, $username, $action, $details, $ip, $timestamp);
            """;
        command.Parameters.AddWithValue("$userId", entry.UserId);
        command.Parameters.AddWithValue("$username", entry.Username);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$details", (object?)entry.Details ?? DBNull.Value);
        command.Parameters.AddWithValue("$ip", (object?)entry.IpAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, user_id, username, action, details, ip_address, timestamp FROM audit_logs ORDER BY id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var logs = new List<AuditLogEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new AuditLogEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return logs;
    }

    private static AppUser MapUser(SqliteDataReader reader)
    {
        var assignedShopsRaw = reader.GetString(6);
        List<string> shops;
        try
        {
            shops = JsonSerializer.Deserialize<List<string>>(assignedShopsRaw) ?? [];
        }
        catch
        {
            shops = [];
        }

        return new AppUser(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            shops,
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9) == 1,
            DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, encrypted_bot_token, chat_id, is_enabled, notify_on_orders, notify_on_stock, daily_brief_enabled, updated_at
            FROM shop_telegram_settings
            WHERE shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shop_id", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var encryptedToken = reader.GetString(1);
        string maskedToken = string.Empty;
        if (!string.IsNullOrWhiteSpace(encryptedToken))
        {
            try
            {
                var decrypted = _protector.Unprotect(encryptedToken);
                maskedToken = MaskSecret(decrypted);
            }
            catch
            {
                maskedToken = "****";
            }
        }

        return new TelegramShopSettings(
            reader.GetString(0),
            maskedToken,
            reader.GetString(2),
            reader.GetInt32(3) == 1,
            reader.GetInt32(4) == 1,
            reader.GetInt32(5) == 1,
            reader.GetInt32(6) == 1,
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);

        string encryptedToken = string.Empty;
        if (string.IsNullOrWhiteSpace(request.BotToken) || request.BotToken.Contains("..."))
        {
            await using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT encrypted_bot_token FROM shop_telegram_settings WHERE shop_id = $shop_id LIMIT 1;";
            selectCmd.Parameters.AddWithValue("$shop_id", request.ShopId);
            var res = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (res is string existing) encryptedToken = existing;
        }
        else
        {
            encryptedToken = _protector.Protect(request.BotToken.Trim());
        }

        var updatedAt = DateTimeOffset.UtcNow;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO shop_telegram_settings(shop_id, encrypted_bot_token, chat_id, is_enabled, notify_on_orders, notify_on_stock, daily_brief_enabled, updated_at)
            VALUES($shop_id, $token, $chat_id, $is_enabled, $orders, $stock, $brief, $updated_at)
            ON CONFLICT(shop_id) DO UPDATE SET
                encrypted_bot_token = excluded.encrypted_bot_token,
                chat_id = excluded.chat_id,
                is_enabled = excluded.is_enabled,
                notify_on_orders = excluded.notify_on_orders,
                notify_on_stock = excluded.notify_on_stock,
                daily_brief_enabled = excluded.daily_brief_enabled,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$shop_id", request.ShopId);
        command.Parameters.AddWithValue("$token", encryptedToken);
        command.Parameters.AddWithValue("$chat_id", request.ChatId.Trim());
        command.Parameters.AddWithValue("$is_enabled", request.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$orders", request.NotifyOnOrders ? 1 : 0);
        command.Parameters.AddWithValue("$stock", request.NotifyOnStock ? 1 : 0);
        command.Parameters.AddWithValue("$brief", request.DailyBriefEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at", updatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        string maskedToken = string.Empty;
        if (!string.IsNullOrWhiteSpace(encryptedToken))
        {
            try
            {
                var decrypted = _protector.Unprotect(encryptedToken);
                maskedToken = MaskSecret(decrypted);
            }
            catch
            {
                maskedToken = "****";
            }
        }

        return new TelegramShopSettings(
            request.ShopId,
            maskedToken,
            request.ChatId,
            request.IsEnabled,
            request.NotifyOnOrders,
            request.NotifyOnStock,
            request.DailyBriefEnabled,
            updatedAt);
    }

    public async Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, carrier_id, encrypted_credentials, account_no, service_level, is_connected, updated_at
            FROM shop_carrier_sessions
            WHERE shop_id = $shop_id;
            """;
        command.Parameters.AddWithValue("$shop_id", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<CarrierSessionRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var enc = reader.GetString(2);
            string masked = string.Empty;
            if (!string.IsNullOrWhiteSpace(enc))
            {
                try
                {
                    var dec = _protector.Unprotect(enc);
                    masked = MaskSecret(dec);
                }
                catch
                {
                    masked = "****";
                }
            }

            list.Add(new CarrierSessionRecord(
                reader.GetString(0),
                reader.GetString(1),
                masked,
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) == 1,
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return list;
    }

    public async Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);

        string encCredentials = string.Empty;
        if (string.IsNullOrWhiteSpace(request.Credentials) || request.Credentials.Contains("..."))
        {
            await using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT encrypted_credentials FROM shop_carrier_sessions WHERE shop_id = $shop_id AND carrier_id = $carrier_id LIMIT 1;";
            selectCmd.Parameters.AddWithValue("$shop_id", request.ShopId);
            selectCmd.Parameters.AddWithValue("$carrier_id", request.CarrierId);
            var res = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (res is string existing) encCredentials = existing;
        }
        else
        {
            encCredentials = _protector.Protect(request.Credentials.Trim());
        }

        var updatedAt = DateTimeOffset.UtcNow;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO shop_carrier_sessions(shop_id, carrier_id, encrypted_credentials, account_no, service_level, is_connected, updated_at)
            VALUES($shop_id, $carrier_id, $enc, $account_no, $service_level, $is_connected, $updated_at)
            ON CONFLICT(shop_id, carrier_id) DO UPDATE SET
                encrypted_credentials = excluded.encrypted_credentials,
                account_no = excluded.account_no,
                service_level = excluded.service_level,
                is_connected = excluded.is_connected,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$shop_id", request.ShopId);
        command.Parameters.AddWithValue("$carrier_id", request.CarrierId);
        command.Parameters.AddWithValue("$enc", encCredentials);
        command.Parameters.AddWithValue("$account_no", request.AccountNo.Trim());
        command.Parameters.AddWithValue("$service_level", request.ServiceLevel.Trim());
        command.Parameters.AddWithValue("$is_connected", request.IsConnected ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at", updatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        string masked = string.Empty;
        if (!string.IsNullOrWhiteSpace(encCredentials))
        {
            try
            {
                var dec = _protector.Unprotect(encCredentials);
                masked = MaskSecret(dec);
            }
            catch
            {
                masked = "****";
            }
        }

        return new CarrierSessionRecord(
            request.ShopId,
            request.CarrierId,
            masked,
            request.AccountNo,
            request.ServiceLevel,
            request.IsConnected,
            updatedAt);
    }

    public async Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, encrypted_keystring, encrypted_shared_secret, redirect_uri, updated_at
            FROM etsy_app_credentials
            WHERE shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shop_id", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var encKey = reader.GetString(1);
        var encSecret = reader.GetString(2);
        string maskedKey = string.Empty;
        string maskedSecret = string.Empty;

        if (!string.IsNullOrWhiteSpace(encKey))
        {
            try { maskedKey = MaskSecret(_protector.Unprotect(encKey)); }
            catch { maskedKey = "****"; }
        }

        if (!string.IsNullOrWhiteSpace(encSecret))
        {
            try { maskedSecret = MaskSecret(_protector.Unprotect(encSecret)); }
            catch { maskedSecret = "****"; }
        }

        return new EtsyAppCredentialsRecord(
            reader.GetString(0),
            maskedKey,
            maskedSecret,
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT encrypted_keystring, encrypted_shared_secret, redirect_uri
            FROM etsy_app_credentials
            WHERE shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shop_id", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (string.Empty, string.Empty, string.Empty);

        var encKey = reader.GetString(0);
        var encSecret = reader.GetString(1);
        var redirectUri = reader.GetString(2);

        string rawKey = string.Empty;
        string rawSecret = string.Empty;

        if (!string.IsNullOrWhiteSpace(encKey))
        {
            try { rawKey = _protector.Unprotect(encKey); }
            catch { rawKey = string.Empty; }
        }

        if (!string.IsNullOrWhiteSpace(encSecret))
        {
            try { rawSecret = _protector.Unprotect(encSecret); }
            catch { rawSecret = string.Empty; }
        }

        return (rawKey, rawSecret, redirectUri);
    }

    public async Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);

        string encKey = string.Empty;
        string encSecret = string.Empty;

        // Existing lookup if masked or omitted
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT encrypted_keystring, encrypted_shared_secret FROM etsy_app_credentials WHERE shop_id = $shop_id LIMIT 1;";
            selectCmd.Parameters.AddWithValue("$shop_id", request.ShopId);
            await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                encKey = reader.GetString(0);
                encSecret = reader.GetString(1);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Keystring) && !request.Keystring.Contains("..."))
        {
            encKey = _protector.Protect(request.Keystring.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.SharedSecret) && !request.SharedSecret.Contains("..."))
        {
            encSecret = _protector.Protect(request.SharedSecret.Trim());
        }

        var redirectUri = request.RedirectUri?.Trim() ?? string.Empty;
        var updatedAt = DateTimeOffset.UtcNow;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO etsy_app_credentials(shop_id, encrypted_keystring, encrypted_shared_secret, redirect_uri, updated_at)
            VALUES($shop_id, $key, $secret, $redirect_uri, $updated_at)
            ON CONFLICT(shop_id) DO UPDATE SET
                encrypted_keystring = excluded.encrypted_keystring,
                encrypted_shared_secret = excluded.encrypted_shared_secret,
                redirect_uri = excluded.redirect_uri,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$shop_id", request.ShopId);
        command.Parameters.AddWithValue("$key", encKey);
        command.Parameters.AddWithValue("$secret", encSecret);
        command.Parameters.AddWithValue("$redirect_uri", redirectUri);
        command.Parameters.AddWithValue("$updated_at", updatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        string maskedKey = string.Empty;
        string maskedSecret = string.Empty;

        if (!string.IsNullOrWhiteSpace(encKey))
        {
            try { maskedKey = MaskSecret(_protector.Unprotect(encKey)); }
            catch { maskedKey = "****"; }
        }

        if (!string.IsNullOrWhiteSpace(encSecret))
        {
            try { maskedSecret = MaskSecret(_protector.Unprotect(encSecret)); }
            catch { maskedSecret = "****"; }
        }

        return new EtsyAppCredentialsRecord(request.ShopId, maskedKey, maskedSecret, redirectUri, updatedAt);
    }

    public async Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, listing_id, title, current_seo_score, optimized_seo_score, status, provider, model, result_json, audited_at
            FROM listing_ai_audits
            WHERE shop_id = $shop_id
            ORDER BY audited_at DESC;
            """;
        command.Parameters.AddWithValue("$shop_id", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<SavedListingAuditRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SavedListingAuditRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return list;
    }

    public async Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO listing_ai_audits(shop_id, listing_id, title, current_seo_score, optimized_seo_score, status, provider, model, result_json, audited_at)
            VALUES($shop_id, $listing_id, $title, $cur_seo, $opt_seo, $status, $provider, $model, $json, $audited_at)
            ON CONFLICT(shop_id, listing_id) DO UPDATE SET
                title = excluded.title,
                current_seo_score = excluded.current_seo_score,
                optimized_seo_score = excluded.optimized_seo_score,
                status = excluded.status,
                provider = excluded.provider,
                model = excluded.model,
                result_json = excluded.result_json,
                audited_at = excluded.audited_at;
            """;
        command.Parameters.AddWithValue("$shop_id", request.ShopId);
        command.Parameters.AddWithValue("$listing_id", request.ListingId);
        command.Parameters.AddWithValue("$title", request.Title);
        command.Parameters.AddWithValue("$cur_seo", request.CurrentSeoScore);
        command.Parameters.AddWithValue("$opt_seo", request.OptimizedSeoScore);
        command.Parameters.AddWithValue("$status", request.Status);
        command.Parameters.AddWithValue("$provider", request.Provider);
        command.Parameters.AddWithValue("$model", request.Model);
        command.Parameters.AddWithValue("$json", request.ResultJson);
        command.Parameters.AddWithValue("$audited_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        if (secret.Length <= 8) return "****";
        return $"{secret[..4]}...{secret[^4..]}";
    }

    public ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
