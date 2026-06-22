namespace EtsyMarketPlace.Infrastructure.ShopPerformance;

using System.Globalization;
using EtsyMarketPlace.Application.ShopPerformance;
using Microsoft.Data.Sqlite;

public sealed class SqliteShopPerformanceHistoryRepository : IShopPerformanceHistoryRepository
{
    private readonly string _connectionString;

    public SqliteShopPerformanceHistoryRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS shop_performance_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                shop_id INTEGER NOT NULL,
                shop_name TEXT NOT NULL,
                shop_url TEXT NOT NULL DEFAULT '',
                period_start TEXT NOT NULL,
                period_end TEXT NOT NULL,
                captured_at TEXT NOT NULL,
                capture_day TEXT NOT NULL,
                currency_code TEXT NOT NULL,
                order_count INTEGER NOT NULL,
                units_sold INTEGER NOT NULL,
                gross_revenue REAL NOT NULL,
                average_order_value REAL NOT NULL,
                product_count INTEGER NOT NULL,
                UNIQUE(shop_id, period_start, period_end, capture_day)
            );

            CREATE TABLE IF NOT EXISTS shop_product_performance_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                snapshot_id INTEGER NOT NULL,
                listing_id INTEGER NOT NULL,
                title TEXT NOT NULL,
                order_count INTEGER NOT NULL,
                units_sold INTEGER NOT NULL,
                revenue REAL NOT NULL,
                currency_code TEXT NOT NULL,
                FOREIGN KEY(snapshot_id) REFERENCES shop_performance_snapshots(id) ON DELETE CASCADE,
                UNIQUE(snapshot_id, listing_id)
            );

            CREATE INDEX IF NOT EXISTS ix_shop_performance_shop_captured
                ON shop_performance_snapshots(shop_id, captured_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShopPerformanceHistoryRecord> SaveDailyAsync(
        ShopPerformanceReport report,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var periodStart = report.PeriodStart.ToUniversalTime().ToString("O");
        var periodEnd = report.PeriodEnd.ToUniversalTime().ToString("O");
        var captureDay = capturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO shop_performance_snapshots(
                    shop_id, shop_name, shop_url, period_start, period_end, captured_at, capture_day,
                    currency_code, order_count, units_sold, gross_revenue, average_order_value, product_count)
                VALUES(
                    $shopId, $shopName, $shopUrl, $periodStart, $periodEnd, $capturedAt, $captureDay,
                    $currency, $orders, $units, $revenue, $average, $products)
                ON CONFLICT(shop_id, period_start, period_end, capture_day) DO UPDATE SET
                    shop_name = excluded.shop_name,
                    shop_url = excluded.shop_url,
                    captured_at = excluded.captured_at,
                    currency_code = excluded.currency_code,
                    order_count = excluded.order_count,
                    units_sold = excluded.units_sold,
                    gross_revenue = excluded.gross_revenue,
                    average_order_value = excluded.average_order_value,
                    product_count = excluded.product_count;
                """;
            upsert.Parameters.AddWithValue("$shopId", report.Shop.ShopId);
            upsert.Parameters.AddWithValue("$shopName", report.Shop.ShopName);
            upsert.Parameters.AddWithValue("$shopUrl", report.Shop.ShopUrl);
            upsert.Parameters.AddWithValue("$periodStart", periodStart);
            upsert.Parameters.AddWithValue("$periodEnd", periodEnd);
            upsert.Parameters.AddWithValue("$capturedAt", capturedAt.ToUniversalTime().ToString("O"));
            upsert.Parameters.AddWithValue("$captureDay", captureDay);
            upsert.Parameters.AddWithValue("$currency", report.CurrencyCode);
            upsert.Parameters.AddWithValue("$orders", report.OrderCount);
            upsert.Parameters.AddWithValue("$units", report.UnitsSold);
            upsert.Parameters.AddWithValue("$revenue", report.GrossRevenue);
            upsert.Parameters.AddWithValue("$average", report.AverageOrderValue);
            upsert.Parameters.AddWithValue("$products", report.Products.Count);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        long snapshotId;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT id FROM shop_performance_snapshots
                WHERE shop_id = $shopId AND period_start = $periodStart AND period_end = $periodEnd AND capture_day = $captureDay;
                """;
            select.Parameters.AddWithValue("$shopId", report.Shop.ShopId);
            select.Parameters.AddWithValue("$periodStart", periodStart);
            select.Parameters.AddWithValue("$periodEnd", periodEnd);
            select.Parameters.AddWithValue("$captureDay", captureDay);
            snapshotId = Convert.ToInt64(await select.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        await using (var deleteProducts = connection.CreateCommand())
        {
            deleteProducts.Transaction = transaction;
            deleteProducts.CommandText = "DELETE FROM shop_product_performance_snapshots WHERE snapshot_id = $snapshotId;";
            deleteProducts.Parameters.AddWithValue("$snapshotId", snapshotId);
            await deleteProducts.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var product in report.Products)
        {
            await InsertProductAsync(connection, transaction, snapshotId, product, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ShopPerformanceHistoryRecord(
            snapshotId, report.Shop, report.PeriodStart, report.PeriodEnd, capturedAt,
            report.CurrencyCode, report.OrderCount, report.UnitsSold, report.GrossRevenue,
            report.AverageOrderValue, report.Products);
    }

    public async Task<IReadOnlyList<ShopPerformanceHistoryRecord>> GetByShopAsync(
        long shopId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shop_id, shop_name, shop_url, period_start, period_end, captured_at,
                   currency_code, order_count, units_sold, gross_revenue, average_order_value
            FROM shop_performance_snapshots
            WHERE shop_id = $shopId
            ORDER BY captured_at DESC;
            """;
        command.Parameters.AddWithValue("$shopId", shopId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<SnapshotRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SnapshotRow(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                ParseDate(reader.GetString(4)), ParseDate(reader.GetString(5)), ParseDate(reader.GetString(6)),
                reader.GetString(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetDecimal(10), reader.GetDecimal(11)));
        }
        await reader.CloseAsync();

        var history = new List<ShopPerformanceHistoryRecord>();
        foreach (var row in rows)
        {
            var products = await GetProductsAsync(connection, row.Id, cancellationToken);
            history.Add(new ShopPerformanceHistoryRecord(
                row.Id, new OwnShopProfile(row.ShopId, row.ShopName, row.ShopUrl), row.PeriodStart,
                row.PeriodEnd, row.CapturedAt, row.CurrencyCode, row.OrderCount, row.UnitsSold,
                row.GrossRevenue, row.AverageOrderValue, products));
        }
        return history;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertProductAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long snapshotId,
        ProductPerformance product,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO shop_product_performance_snapshots(
                snapshot_id, listing_id, title, order_count, units_sold, revenue, currency_code)
            VALUES($snapshotId, $listingId, $title, $orders, $units, $revenue, $currency);
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);
        command.Parameters.AddWithValue("$listingId", product.ListingId);
        command.Parameters.AddWithValue("$title", product.Title);
        command.Parameters.AddWithValue("$orders", product.OrderCount);
        command.Parameters.AddWithValue("$units", product.UnitsSold);
        command.Parameters.AddWithValue("$revenue", product.Revenue);
        command.Parameters.AddWithValue("$currency", product.CurrencyCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ProductPerformance>> GetProductsAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT listing_id, title, order_count, units_sold, revenue, currency_code
            FROM shop_product_performance_snapshots
            WHERE snapshot_id = $snapshotId
            ORDER BY units_sold DESC, revenue DESC;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<ProductPerformance>();
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new ProductPerformance(
                reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetDecimal(4), reader.GetString(5)));
        }
        return products;
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed record SnapshotRow(
        long Id, long ShopId, string ShopName, string ShopUrl, DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd, DateTimeOffset CapturedAt, string CurrencyCode,
        int OrderCount, int UnitsSold, decimal GrossRevenue, decimal AverageOrderValue);
}
