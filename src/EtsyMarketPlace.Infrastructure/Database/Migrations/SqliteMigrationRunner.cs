using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtsyMarketPlace.Infrastructure.Database.Migrations;

/// <summary>
/// Thread-safe, resilient SQLite schema migration runner with audit journal.
/// </summary>
public sealed class SqliteMigrationRunner : ISqliteMigrationRunner
{
    private const string JournalTableName = "__schema_migrations";
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> DatabaseLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<SqliteMigrationRunner> _logger;

    public SqliteMigrationRunner(ILogger<SqliteMigrationRunner>? logger = null)
    {
        _logger = logger ?? NullLogger<SqliteMigrationRunner>.Instance;
    }

    public async Task<IReadOnlyList<MigrationAppliedRecord>> MigrateAsync(
        string connectionString,
        IEnumerable<ISchemaMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(migrations);

        var dbLock = DatabaseLocks.GetOrAdd(connectionString, _ => new SemaphoreSlim(1, 1));
        await dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await ConfigureConnectionPragmasAsync(connection, cancellationToken);
            await EnsureJournalTableExistsAsync(connection, cancellationToken);

            var migrationList = migrations.OrderBy(m => m.Version).ToList();

            // Validate no duplicate versions
            var duplicate = migrationList.GroupBy(m => m.Version).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException($"Duplicate migration version detected: {duplicate.Key}");
            }

            var newlyApplied = new List<MigrationAppliedRecord>();

            foreach (var migration in migrationList)
            {
                _logger.LogInformation("Applying migration {Version}: '{Name}'...", migration.Version, migration.Name);
                var stopwatch = Stopwatch.StartNew();

                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Check if already applied within transaction (protects against concurrent runs)
                    await using (var checkCmd = connection.CreateCommand())
                    {
                        checkCmd.Transaction = transaction;
                        checkCmd.CommandText = $"SELECT COUNT(*) FROM {JournalTableName} WHERE version = $version;";
                        checkCmd.Parameters.AddWithValue("$version", migration.Version);
                        var alreadyApplied = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
                        if (alreadyApplied)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            continue;
                        }
                    }

                    await migration.UpAsync(connection, transaction, cancellationToken);

                    var appliedAt = DateTimeOffset.UtcNow;
                    stopwatch.Stop();
                    var elapsedMs = stopwatch.ElapsedMilliseconds;

                    await using (var insertCmd = connection.CreateCommand())
                    {
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = $"""
                            INSERT INTO {JournalTableName} (version, name, applied_at_utc, execution_time_ms)
                            VALUES ($version, $name, $appliedAt, $timeMs)
                            ON CONFLICT(version) DO NOTHING;
                            """;
                        insertCmd.Parameters.AddWithValue("$version", migration.Version);
                        insertCmd.Parameters.AddWithValue("$name", migration.Name);
                        insertCmd.Parameters.AddWithValue("$appliedAt", appliedAt.ToString("O"));
                        insertCmd.Parameters.AddWithValue("$timeMs", elapsedMs);
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);

                    var record = new MigrationAppliedRecord(migration.Version, migration.Name, appliedAt, elapsedMs);
                    newlyApplied.Add(record);
                    _logger.LogInformation("Migration {Version} '{Name}' successfully applied in {ElapsedMs}ms.", migration.Version, migration.Name, elapsedMs);
                }
                catch (Exception ex)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                    catch (Exception rbEx)
                    {
                        _logger.LogError(rbEx, "Failed to rollback transaction after migration failure.");
                    }

                    _logger.LogError(ex, "Failed to apply migration {Version}: '{Name}'.", migration.Version, migration.Name);
                    throw new InvalidOperationException($"Database migration {migration.Version} '{migration.Name}' failed: {ex.Message}", ex);
                }
            }

            return newlyApplied;
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<MigrationAppliedRecord>> GetAppliedMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var dbLock = DatabaseLocks.GetOrAdd(connectionString, _ => new SemaphoreSlim(1, 1));
        await dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await EnsureJournalTableExistsAsync(connection, cancellationToken);
            var dictionary = await LoadAppliedVersionsAsync(connection, cancellationToken);
            return dictionary.Values.OrderBy(x => x.Version).ToList();
        }
        finally
        {
            dbLock.Release();
        }
    }

    private static async Task ConfigureConnectionPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            PRAGMA busy_timeout=5000;
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureJournalTableExistsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {JournalTableName} (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at_utc TEXT NOT NULL,
                execution_time_ms INTEGER NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<int, MigrationAppliedRecord>> LoadAppliedVersionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, MigrationAppliedRecord>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT version, name, applied_at_utc, execution_time_ms FROM {JournalTableName} ORDER BY version ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var version = reader.GetInt32(0);
            var name = reader.GetString(1);
            var appliedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            var timeMs = reader.GetInt64(3);
            result[version] = new MigrationAppliedRecord(version, name, appliedAt, timeMs);
        }
        return result;
    }
}
