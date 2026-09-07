namespace EtsyMarketPlace.Infrastructure.BatchQueue;

using EtsyMarketPlace.Application.BatchQueue;
using Microsoft.Data.Sqlite;

public sealed class SqliteBatchQueueRepository : IBatchQueueRepository
{
    private const string Separator = "\u001F";
    private readonly string _connectionString;

    public SqliteBatchQueueRepository(string databasePath)
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
            CREATE TABLE IF NOT EXISTS batch_queue_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                listing_id TEXT NOT NULL DEFAULT '',
                original_title TEXT NOT NULL DEFAULT '',
                original_description TEXT NOT NULL DEFAULT '',
                original_tags TEXT NOT NULL DEFAULT '',
                target_keyword TEXT NOT NULL DEFAULT '',
                category TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'Pending',
                overall_score INTEGER NOT NULL DEFAULT 0,
                optimized_title TEXT NOT NULL DEFAULT '',
                optimized_description TEXT NOT NULL DEFAULT '',
                optimized_tags TEXT NOT NULL DEFAULT '',
                optimized_materials TEXT NOT NULL DEFAULT '',
                risk_warnings TEXT NOT NULL DEFAULT '',
                issues TEXT NOT NULL DEFAULT '',
                processed_at TEXT NULL,
                error_message TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_batch_queue_items_status
                ON batch_queue_items(status);
            CREATE INDEX IF NOT EXISTS ix_batch_queue_items_created
                ON batch_queue_items(created_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Safe migrations for live sync
        try
        {
            command.CommandText = "ALTER TABLE batch_queue_items ADD COLUMN is_synced_to_etsy INTEGER NOT NULL DEFAULT 0;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException) { }

        try
        {
            command.CommandText = "ALTER TABLE batch_queue_items ADD COLUMN synced_at TEXT NULL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException) { }
    }

    public async Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(
        IReadOnlyList<SaveBatchQueueItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new List<BatchQueueItem>();
        var createdAt = DateTimeOffset.Now;

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var item in items)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO batch_queue_items(
                    created_at, listing_id, original_title, original_description, original_tags, target_keyword, category, status, is_synced_to_etsy, synced_at)
                VALUES (
                    $created_at, $listing_id, $original_title, $original_description, $original_tags, $target_keyword, $category, $status, 0, NULL);

                SELECT last_insert_rowid();
                """;

            command.Parameters.AddWithValue("$created_at", createdAt.ToString("O"));
            command.Parameters.AddWithValue("$listing_id", item.ListingId ?? "");
            command.Parameters.AddWithValue("$original_title", item.OriginalTitle ?? "");
            command.Parameters.AddWithValue("$original_description", item.OriginalDescription ?? "");
            command.Parameters.AddWithValue("$original_tags", Join(item.OriginalTags));
            command.Parameters.AddWithValue("$target_keyword", item.TargetKeyword ?? "");
            command.Parameters.AddWithValue("$category", item.Category ?? "");
            command.Parameters.AddWithValue("$status", BatchQueueItemStatus.Pending.ToString());

            var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);

            result.Add(new BatchQueueItem(
                id,
                createdAt,
                item.ListingId ?? "",
                item.OriginalTitle ?? "",
                item.OriginalDescription ?? "",
                item.OriginalTags ?? [],
                item.TargetKeyword ?? "",
                item.Category ?? "",
                BatchQueueItemStatus.Pending,
                0,
                "",
                "",
                [],
                [],
                [],
                [],
                null,
                null,
                false,
                null));
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<BatchQueueItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM batch_queue_items WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken)) return ReadRow(reader);
        return null;
    }

    public async Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM batch_queue_items WHERE status = 'Pending' ORDER BY id ASC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var list = new List<BatchQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(ReadRow(reader));
        return list;
    }

    public async Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM batch_queue_items ORDER BY id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var list = new List<BatchQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) list.Add(ReadRow(reader));
        return list;
    }

    public async Task<BatchQueueItem?> UpdateItemAsync(BatchQueueItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE batch_queue_items
            SET status = $status,
                overall_score = $overall_score,
                optimized_title = $optimized_title,
                optimized_description = $optimized_description,
                optimized_tags = $optimized_tags,
                optimized_materials = $optimized_materials,
                risk_warnings = $risk_warnings,
                issues = $issues,
                processed_at = $processed_at,
                error_message = $error_message,
                is_synced_to_etsy = $is_synced_to_etsy,
                synced_at = $synced_at
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$overall_score", item.OverallScore);
        command.Parameters.AddWithValue("$optimized_title", item.OptimizedTitle ?? "");
        command.Parameters.AddWithValue("$optimized_description", item.OptimizedDescription ?? "");
        command.Parameters.AddWithValue("$optimized_tags", Join(item.OptimizedTags));
        command.Parameters.AddWithValue("$optimized_materials", Join(item.OptimizedMaterials));
        command.Parameters.AddWithValue("$risk_warnings", Join(item.RiskWarnings));
        command.Parameters.AddWithValue("$issues", Join(item.Issues));
        command.Parameters.AddWithValue("$processed_at", item.ProcessedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$error_message", item.ErrorMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$is_synced_to_etsy", item.IsSyncedToEtsy ? 1 : 0);
        command.Parameters.AddWithValue("$synced_at", item.SyncedAt?.ToString("O") ?? (object)DBNull.Value);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0 ? item : null;
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM batch_queue_items WHERE status IN ('Completed', 'RiskWarning', 'Failed', 'SyncedToEtsy', 'RolledBack');";
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM batch_queue_items;";
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static BatchQueueItem ReadRow(SqliteDataReader reader)
    {
        var hasSyncedCol = HasColumn(reader, "is_synced_to_etsy");
        var hasSyncedAtCol = HasColumn(reader, "synced_at");

        bool isSynced = hasSyncedCol && !reader.IsDBNull(reader.GetOrdinal("is_synced_to_etsy")) && reader.GetInt32(reader.GetOrdinal("is_synced_to_etsy")) == 1;
        DateTimeOffset? syncedAt = hasSyncedAtCol && !reader.IsDBNull(reader.GetOrdinal("synced_at")) ? DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("synced_at"))) : null;

        return new(
            reader.GetInt64(reader.GetOrdinal("id")),
            DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            reader.GetString(reader.GetOrdinal("listing_id")),
            reader.GetString(reader.GetOrdinal("original_title")),
            reader.GetString(reader.GetOrdinal("original_description")),
            Split(reader.GetString(reader.GetOrdinal("original_tags"))),
            reader.GetString(reader.GetOrdinal("target_keyword")),
            reader.GetString(reader.GetOrdinal("category")),
            Enum.TryParse<BatchQueueItemStatus>(reader.GetString(reader.GetOrdinal("status")), out var status) ? status : BatchQueueItemStatus.Pending,
            reader.GetInt32(reader.GetOrdinal("overall_score")),
            reader.GetString(reader.GetOrdinal("optimized_title")),
            reader.GetString(reader.GetOrdinal("optimized_description")),
            Split(reader.GetString(reader.GetOrdinal("optimized_tags"))),
            Split(reader.GetString(reader.GetOrdinal("optimized_materials"))),
            Split(reader.GetString(reader.GetOrdinal("risk_warnings"))),
            Split(reader.GetString(reader.GetOrdinal("issues"))),
            reader.IsDBNull(reader.GetOrdinal("processed_at")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("processed_at"))),
            reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
            isSynced,
            syncedAt);
    }

    private static bool HasColumn(SqliteDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string Join(IReadOnlyList<string>? items) =>
        items is null || items.Count == 0 ? "" : string.Join(Separator, items);

    private static IReadOnlyList<string> Split(string text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
}
