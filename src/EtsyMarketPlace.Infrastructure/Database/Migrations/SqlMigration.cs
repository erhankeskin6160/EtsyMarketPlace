using Microsoft.Data.Sqlite;

namespace EtsyMarketPlace.Infrastructure.Database.Migrations;

/// <summary>
/// Convenient base class for migrations that execute raw DDL scripts.
/// </summary>
public abstract class SqlMigration : ISchemaMigration
{
    public abstract int Version { get; }
    public abstract string Name { get; }
    public abstract string UpSql { get; }

    public virtual async Task UpAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UpSql)) return;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
