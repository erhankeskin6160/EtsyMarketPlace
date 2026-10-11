using Microsoft.Data.Sqlite;

namespace EtsyMarketPlace.Infrastructure.Database.Migrations;

/// <summary>
/// Represents a versioned database schema migration for SQLite.
/// </summary>
public interface ISchemaMigration
{
    /// <summary>
    /// Sequential version number (e.g., 1, 2, 3...).
    /// Must be strictly positive and unique across the migration set.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Descriptive name of the migration (e.g. "CreateCoreIntegrationTables").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the migration within an active transaction.
    /// </summary>
    Task UpAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken);
}
