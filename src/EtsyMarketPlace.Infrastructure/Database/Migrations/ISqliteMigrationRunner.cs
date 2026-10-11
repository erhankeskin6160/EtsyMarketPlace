namespace EtsyMarketPlace.Infrastructure.Database.Migrations;

/// <summary>
/// Executes and tracks schema migrations for a SQLite database.
/// </summary>
public interface ISqliteMigrationRunner
{
    /// <summary>
    /// Applies all pending migrations in ascending version order inside isolated transactions.
    /// Returns the list of migrations that were applied during this run.
    /// </summary>
    Task<IReadOnlyList<MigrationAppliedRecord>> MigrateAsync(
        string connectionString,
        IEnumerable<ISchemaMigration> migrations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all applied migrations from the __schema_migrations journal table.
    /// </summary>
    Task<IReadOnlyList<MigrationAppliedRecord>> GetAppliedMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
