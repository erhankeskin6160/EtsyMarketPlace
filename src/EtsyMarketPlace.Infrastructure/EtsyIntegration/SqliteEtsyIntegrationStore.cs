using System.Globalization;
using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class SqliteEtsyIntegrationStore : IEtsyTokenStore, IEtsyIntegrationRepository, IEtsyReportingService, IAsyncDisposable
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

                CREATE INDEX IF NOT EXISTS ix_bank_payouts_shop_date ON bank_payouts(shop_id, occurred_at);
                CREATE INDEX IF NOT EXISTS ix_financial_transactions_shop_date ON financial_transactions(shop_id, occurred_at);
                CREATE INDEX IF NOT EXISTS ix_order_costs_shop_date ON order_costs(shop_id, created_at);
                CREATE INDEX IF NOT EXISTS ix_listing_traffic_shop_date ON listing_traffic_daily(shop_id, snapshot_date);
                CREATE INDEX IF NOT EXISTS ix_monthly_orders_shop ON monthly_order_summaries(shop_id, year_month);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
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
                    product_cost=excluded.product_cost, shipping_cost=excluded.shipping_cost, alert_reason=excluded.alert_reason;
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

    public ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
