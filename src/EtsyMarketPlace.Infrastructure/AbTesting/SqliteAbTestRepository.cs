namespace EtsyMarketPlace.Infrastructure.AbTesting;

using EtsyMarketPlace.Application.AbTesting;
using Microsoft.Data.Sqlite;

public sealed class SqliteAbTestRepository : IAbTestRepository
{
    private const string Separator = "\u001F";
    private readonly string _connectionString;

    public SqliteAbTestRepository(string databasePath)
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
            CREATE TABLE IF NOT EXISTS listing_ab_tests (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                listing_id TEXT NOT NULL DEFAULT '',
                listing_title TEXT NOT NULL DEFAULT '',
                experiment_name TEXT NOT NULL DEFAULT '',
                variant_a_title TEXT NOT NULL DEFAULT '',
                variant_b_title TEXT NOT NULL DEFAULT '',
                variant_a_tags TEXT NOT NULL DEFAULT '',
                variant_b_tags TEXT NOT NULL DEFAULT '',
                variant_a_description TEXT NOT NULL DEFAULT '',
                variant_b_description TEXT NOT NULL DEFAULT '',
                start_date TEXT NOT NULL,
                end_date TEXT NULL,
                status TEXT NOT NULL DEFAULT 'Active',
                before_views INTEGER NOT NULL DEFAULT 0,
                before_favorites INTEGER NOT NULL DEFAULT 0,
                before_sales INTEGER NOT NULL DEFAULT 0,
                after_views INTEGER NOT NULL DEFAULT 0,
                after_favorites INTEGER NOT NULL DEFAULT 0,
                after_sales INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_listing_ab_tests_created
                ON listing_ab_tests(created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_listing_ab_tests_listing
                ON listing_ab_tests(listing_id);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ListingAbTestExperiment> SaveAsync(
        SaveAbTestExperiment experiment,
        CancellationToken cancellationToken = default)
    {
        var createdAt = DateTimeOffset.Now;
        var startDate = DateTimeOffset.Now;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO listing_ab_tests(
                created_at,
                listing_id,
                listing_title,
                experiment_name,
                variant_a_title,
                variant_b_title,
                variant_a_tags,
                variant_b_tags,
                variant_a_description,
                variant_b_description,
                start_date,
                status,
                before_views,
                before_favorites,
                before_sales,
                after_views,
                after_favorites,
                after_sales)
            VALUES (
                $created_at,
                $listing_id,
                $listing_title,
                $experiment_name,
                $variant_a_title,
                $variant_b_title,
                $variant_a_tags,
                $variant_b_tags,
                $variant_a_description,
                $variant_b_description,
                $start_date,
                $status,
                $before_views,
                $before_favorites,
                $before_sales,
                $after_views,
                $after_favorites,
                $after_sales);

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$created_at", createdAt.ToString("O"));
        command.Parameters.AddWithValue("$listing_id", experiment.ListingId ?? "");
        command.Parameters.AddWithValue("$listing_title", experiment.ListingTitle ?? "");
        command.Parameters.AddWithValue("$experiment_name", experiment.ExperimentName ?? "A/B Test");
        command.Parameters.AddWithValue("$variant_a_title", experiment.VariantA_Title ?? "");
        command.Parameters.AddWithValue("$variant_b_title", experiment.VariantB_Title ?? "");
        command.Parameters.AddWithValue("$variant_a_tags", Join(experiment.VariantA_Tags));
        command.Parameters.AddWithValue("$variant_b_tags", Join(experiment.VariantB_Tags));
        command.Parameters.AddWithValue("$variant_a_description", experiment.VariantA_Description ?? "");
        command.Parameters.AddWithValue("$variant_b_description", experiment.VariantB_Description ?? "");
        command.Parameters.AddWithValue("$start_date", startDate.ToString("O"));
        command.Parameters.AddWithValue("$status", AbTestStatus.Active.ToString());
        command.Parameters.AddWithValue("$before_views", experiment.InitialViews);
        command.Parameters.AddWithValue("$before_favorites", experiment.InitialFavorites);
        command.Parameters.AddWithValue("$before_sales", experiment.InitialSales);
        command.Parameters.AddWithValue("$after_views", experiment.InitialViews);
        command.Parameters.AddWithValue("$after_favorites", experiment.InitialFavorites);
        command.Parameters.AddWithValue("$after_sales", experiment.InitialSales);

        var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);

        return new ListingAbTestExperiment(
            id,
            createdAt,
            experiment.ListingId ?? "",
            experiment.ListingTitle ?? "",
            experiment.ExperimentName ?? "A/B Test",
            experiment.VariantA_Title ?? "",
            experiment.VariantB_Title ?? "",
            experiment.VariantA_Tags ?? [],
            experiment.VariantB_Tags ?? [],
            experiment.VariantA_Description ?? "",
            experiment.VariantB_Description ?? "",
            startDate,
            null,
            AbTestStatus.Active,
            experiment.InitialViews,
            experiment.InitialFavorites,
            experiment.InitialSales,
            experiment.InitialViews,
            experiment.InitialFavorites,
            experiment.InitialSales);
    }

    public async Task<ListingAbTestExperiment?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM listing_ab_tests WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadRow(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM listing_ab_tests ORDER BY id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var list = new List<ListingAbTestExperiment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRow(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM listing_ab_tests WHERE listing_id = $listing_id ORDER BY id DESC;";
        command.Parameters.AddWithValue("$listing_id", listingId);

        var list = new List<ListingAbTestExperiment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRow(reader));
        }

        return list;
    }

    public async Task<ListingAbTestExperiment?> UpdateMetricsAsync(
        UpdateAbTestMetrics update,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var endDateStr = update.CompleteExperiment ? DateTimeOffset.Now.ToString("O") : null;
        var statusStr = update.CompleteExperiment ? AbTestStatus.Completed.ToString() : AbTestStatus.Active.ToString();

        command.CommandText = """
            UPDATE listing_ab_tests
            SET after_views = $after_views,
                after_favorites = $after_favorites,
                after_sales = $after_sales,
                end_date = COALESCE($end_date, end_date),
                status = $status
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", update.ExperimentId);
        command.Parameters.AddWithValue("$after_views", update.AfterViews);
        command.Parameters.AddWithValue("$after_favorites", update.AfterFavorites);
        command.Parameters.AddWithValue("$after_sales", update.AfterSales);
        command.Parameters.AddWithValue("$end_date", (object?)endDateStr ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", statusStr);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
        {
            return await GetByIdAsync(update.ExperimentId, cancellationToken);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM listing_ab_tests WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static ListingAbTestExperiment ReadRow(SqliteDataReader reader) => new(
        reader.GetInt64(reader.GetOrdinal("id")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
        reader.GetString(reader.GetOrdinal("listing_id")),
        reader.GetString(reader.GetOrdinal("listing_title")),
        reader.GetString(reader.GetOrdinal("experiment_name")),
        reader.GetString(reader.GetOrdinal("variant_a_title")),
        reader.GetString(reader.GetOrdinal("variant_b_title")),
        Split(reader.GetString(reader.GetOrdinal("variant_a_tags"))),
        Split(reader.GetString(reader.GetOrdinal("variant_b_tags"))),
        reader.GetString(reader.GetOrdinal("variant_a_description")),
        reader.GetString(reader.GetOrdinal("variant_b_description")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("start_date"))),
        reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("end_date"))),
        Enum.TryParse<AbTestStatus>(reader.GetString(reader.GetOrdinal("status")), out var status) ? status : AbTestStatus.Active,
        reader.GetInt32(reader.GetOrdinal("before_views")),
        reader.GetInt32(reader.GetOrdinal("before_favorites")),
        reader.GetInt32(reader.GetOrdinal("before_sales")),
        reader.GetInt32(reader.GetOrdinal("after_views")),
        reader.GetInt32(reader.GetOrdinal("after_favorites")),
        reader.GetInt32(reader.GetOrdinal("after_sales")));

    private static string Join(IReadOnlyList<string>? items) =>
        items is null || items.Count == 0 ? "" : string.Join(Separator, items);

    private static IReadOnlyList<string> Split(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
}
