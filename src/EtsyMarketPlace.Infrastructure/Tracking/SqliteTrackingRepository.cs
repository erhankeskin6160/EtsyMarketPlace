namespace EtsyMarketPlace.Infrastructure.Tracking;

using System.Globalization;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using Microsoft.Data.Sqlite;

public sealed class SqliteTrackingRepository : ITrackingRepository
{
    private readonly string _connectionString;

    public SqliteTrackingRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

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
            CREATE TABLE IF NOT EXISTS tracking_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                entity_type INTEGER NOT NULL,
                external_key TEXT NOT NULL,
                display_name TEXT NOT NULL,
                url TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL,
                UNIQUE(entity_type, external_key)
            );

            CREATE TABLE IF NOT EXISTS tracking_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tracking_item_id INTEGER NOT NULL,
                captured_at TEXT NOT NULL,
                price REAL NULL,
                currency_code TEXT NOT NULL DEFAULT '',
                favorites INTEGER NULL,
                views INTEGER NULL,
                shop_sales INTEGER NULL,
                review_count INTEGER NULL,
                review_average REAL NULL,
                seo_score INTEGER NULL,
                market_score INTEGER NULL,
                demand_score INTEGER NULL,
                competition_score INTEGER NULL,
                opportunity_score INTEGER NULL,
                result_count INTEGER NULL,
                sample_size INTEGER NULL,
                FOREIGN KEY(tracking_item_id) REFERENCES tracking_items(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_tracking_snapshots_item_date
                ON tracking_snapshots(tracking_item_id, captured_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrackingItem> SaveCaptureAsync(TrackingCapture capture, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO tracking_items(entity_type, external_key, display_name, url, created_at)
                VALUES ($type, $key, $name, $url, $created)
                ON CONFLICT(entity_type, external_key) DO UPDATE SET
                    display_name = excluded.display_name,
                    url = excluded.url;
                """;
            upsert.Parameters.AddWithValue("$type", (int)capture.EntityType);
            upsert.Parameters.AddWithValue("$key", capture.ExternalKey.Trim());
            upsert.Parameters.AddWithValue("$name", capture.DisplayName.Trim());
            upsert.Parameters.AddWithValue("$url", capture.Url?.Trim() ?? "");
            upsert.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        long itemId;
        DateTimeOffset createdAt;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT id, created_at FROM tracking_items WHERE entity_type = $type AND external_key = $key;";
            select.Parameters.AddWithValue("$type", (int)capture.EntityType);
            select.Parameters.AddWithValue("$key", capture.ExternalKey.Trim());
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Takip kaydi olusturulamadi.");
            }
            itemId = reader.GetInt64(0);
            createdAt = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        await InsertSnapshotAsync(connection, transaction, itemId, capture.Snapshot, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TrackingItem(itemId, capture.EntityType, capture.ExternalKey.Trim(), capture.DisplayName.Trim(), capture.Url?.Trim() ?? "", createdAt);
    }

    public async Task<IReadOnlyList<TrackingItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, entity_type, external_key, display_name, url, created_at FROM tracking_items ORDER BY created_at DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<TrackingItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TrackingItem(
                reader.GetInt64(0),
                (TrackingEntityType)reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return items;
    }

    public async Task<IReadOnlyList<TrackingSnapshot>> GetSnapshotsAsync(long trackingItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, tracking_item_id, captured_at, price, currency_code, favorites, views, shop_sales,
                   review_count, review_average, seo_score, market_score, demand_score, competition_score,
                   opportunity_score, result_count, sample_size
            FROM tracking_snapshots
            WHERE tracking_item_id = $itemId
            ORDER BY captured_at DESC;
            """;
        command.Parameters.AddWithValue("$itemId", trackingItemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var snapshots = new List<TrackingSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(new TrackingSnapshot(
                reader.GetInt64(0),
                reader.GetInt64(1),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                GetNullableDecimal(reader, 3),
                reader.GetString(4),
                GetNullableInt(reader, 5),
                GetNullableInt(reader, 6),
                GetNullableInt(reader, 7),
                GetNullableInt(reader, 8),
                GetNullableDecimal(reader, 9),
                GetNullableInt(reader, 10),
                GetNullableInt(reader, 11),
                GetNullableInt(reader, 12),
                GetNullableInt(reader, 13),
                GetNullableInt(reader, 14),
                GetNullableInt(reader, 15),
                GetNullableInt(reader, 16)));
        }
        return snapshots;
    }

    public async Task DeleteItemAsync(long trackingItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tracking_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", trackingItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task InsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long itemId,
        TrackingSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tracking_snapshots(
                tracking_item_id, captured_at, price, currency_code, favorites, views, shop_sales,
                review_count, review_average, seo_score, market_score, demand_score, competition_score,
                opportunity_score, result_count, sample_size)
            VALUES(
                $itemId, $captured, $price, $currency, $favorites, $views, $shopSales,
                $reviewCount, $reviewAverage, $seo, $market, $demand, $competition,
                $opportunity, $resultCount, $sampleSize);
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$captured", snapshot.CapturedAt.ToUniversalTime().ToString("O"));
        AddNullable(command, "$price", snapshot.Price);
        command.Parameters.AddWithValue("$currency", snapshot.CurrencyCode ?? "");
        AddNullable(command, "$favorites", snapshot.Favorites);
        AddNullable(command, "$views", snapshot.Views);
        AddNullable(command, "$shopSales", snapshot.ShopSales);
        AddNullable(command, "$reviewCount", snapshot.ReviewCount);
        AddNullable(command, "$reviewAverage", snapshot.ReviewAverage);
        AddNullable(command, "$seo", snapshot.SeoScore);
        AddNullable(command, "$market", snapshot.MarketScore);
        AddNullable(command, "$demand", snapshot.DemandScore);
        AddNullable(command, "$competition", snapshot.CompetitionScore);
        AddNullable(command, "$opportunity", snapshot.OpportunityScore);
        AddNullable(command, "$resultCount", snapshot.ResultCount);
        AddNullable(command, "$sampleSize", snapshot.SampleSize);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static int? GetNullableInt(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetInt32(index);
    private static decimal? GetNullableDecimal(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetDecimal(index);
}
