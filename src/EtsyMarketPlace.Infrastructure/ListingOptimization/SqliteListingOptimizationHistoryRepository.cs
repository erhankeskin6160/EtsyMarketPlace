namespace EtsyMarketPlace.Infrastructure.ListingOptimization;

using EtsyMarketPlace.Application.ListingOptimization;
using Microsoft.Data.Sqlite;

public sealed class SqliteListingOptimizationHistoryRepository : IListingOptimizationHistoryRepository
{
    private const string Separator = "\u001F";
    private readonly string _connectionString;

    public SqliteListingOptimizationHistoryRepository(string databasePath)
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
            CREATE TABLE IF NOT EXISTS listing_optimization_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                listing_id TEXT NOT NULL DEFAULT '',
                listing_title TEXT NOT NULL DEFAULT '',
                target_keyword TEXT NOT NULL DEFAULT '',
                current_seo_score INTEGER NOT NULL,
                optimized_seo_score INTEGER NOT NULL,
                suggested_title TEXT NOT NULL DEFAULT '',
                suggested_tags TEXT NOT NULL DEFAULT '',
                description_draft TEXT NOT NULL DEFAULT '',
                risk_warnings TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS ix_listing_optimization_history_created
                ON listing_optimization_history(created_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ListingOptimizationHistoryEntry> SaveAsync(
        SaveListingOptimizationHistory history,
        CancellationToken cancellationToken = default)
    {
        var createdAt = DateTimeOffset.Now;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO listing_optimization_history(
                created_at,
                listing_id,
                listing_title,
                target_keyword,
                current_seo_score,
                optimized_seo_score,
                suggested_title,
                suggested_tags,
                description_draft,
                risk_warnings)
            VALUES (
                $created_at,
                $listing_id,
                $listing_title,
                $target_keyword,
                $current_seo_score,
                $optimized_seo_score,
                $suggested_title,
                $suggested_tags,
                $description_draft,
                $risk_warnings);

            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$created_at", createdAt.ToString("O"));
        command.Parameters.AddWithValue("$listing_id", history.ListingId);
        command.Parameters.AddWithValue("$listing_title", history.ListingTitle);
        command.Parameters.AddWithValue("$target_keyword", history.TargetKeyword);
        command.Parameters.AddWithValue("$current_seo_score", history.Result.CurrentSeoScore);
        command.Parameters.AddWithValue("$optimized_seo_score", history.Result.OptimizedSeoScore);
        command.Parameters.AddWithValue("$suggested_title", history.Result.TitleSuggestions.FirstOrDefault() ?? "");
        command.Parameters.AddWithValue("$suggested_tags", Join(history.Result.TagSuggestions));
        command.Parameters.AddWithValue("$description_draft", history.Result.DescriptionDraft);
        command.Parameters.AddWithValue("$risk_warnings", Join(history.Result.RiskWarnings));

        var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return ToEntry(id, createdAt, history);
    }

    public async Task<IReadOnlyList<ListingOptimizationHistoryEntry>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                created_at,
                listing_id,
                listing_title,
                target_keyword,
                current_seo_score,
                optimized_seo_score,
                suggested_title,
                suggested_tags,
                description_draft,
                risk_warnings
            FROM listing_optimization_history
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        var entries = new List<ListingOptimizationHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new ListingOptimizationHistoryEntry(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetString(7),
                Split(reader.GetString(8)),
                reader.GetString(9),
                Split(reader.GetString(10))));
        }

        return entries;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM listing_optimization_history WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static ListingOptimizationHistoryEntry ToEntry(
        long id,
        DateTimeOffset createdAt,
        SaveListingOptimizationHistory history) =>
        new(
            id,
            createdAt,
            history.ListingId,
            history.ListingTitle,
            history.TargetKeyword,
            history.Result.CurrentSeoScore,
            history.Result.OptimizedSeoScore,
            history.Result.TitleSuggestions.FirstOrDefault() ?? "",
            history.Result.TagSuggestions,
            history.Result.DescriptionDraft,
            history.Result.RiskWarnings);

    private static string Join(IEnumerable<string> values) =>
        string.Join(Separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static IReadOnlyList<string> Split(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
