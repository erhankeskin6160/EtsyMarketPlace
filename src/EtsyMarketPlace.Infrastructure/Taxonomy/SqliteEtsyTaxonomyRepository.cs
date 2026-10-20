namespace EtsyMarketPlace.Infrastructure.Taxonomy;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Taxonomy;
using EtsyMarketPlace.Domain.Taxonomy;
using Microsoft.Data.Sqlite;

public sealed class SqliteEtsyTaxonomyRepository : IEtsyTaxonomyRepository
{
    private readonly string _connectionString;

    public SqliteEtsyTaxonomyRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS etsy_taxonomy_nodes (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                path TEXT NOT NULL,
                parent_id INTEGER NOT NULL DEFAULT 0,
                level INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_etsy_taxonomy_nodes_path ON etsy_taxonomy_nodes(path);
            CREATE INDEX IF NOT EXISTS ix_etsy_taxonomy_nodes_name ON etsy_taxonomy_nodes(name);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM etsy_taxonomy_nodes;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<EtsyTaxonomyNode>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, path, parent_id, level FROM etsy_taxonomy_nodes ORDER BY path ASC;";

        var list = new List<EtsyTaxonomyNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new EtsyTaxonomyNode
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Path = reader.GetString(2),
                ParentId = reader.GetInt64(3),
                Level = reader.GetInt32(4)
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<EtsyTaxonomyNode>> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(query))
        {
            command.CommandText = "SELECT id, name, path, parent_id, level FROM etsy_taxonomy_nodes ORDER BY path ASC LIMIT @limit;";
            command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
        }
        else
        {
            command.CommandText = """
                SELECT id, name, path, parent_id, level 
                FROM etsy_taxonomy_nodes 
                WHERE path LIKE @query OR name LIKE @query
                ORDER BY 
                    CASE WHEN path LIKE @exactStart THEN 1 ELSE 2 END,
                    path ASC
                LIMIT @limit;
                """;
            var cleanQuery = query.Trim();
            command.Parameters.AddWithValue("@query", $"%{cleanQuery}%");
            command.Parameters.AddWithValue("@exactStart", $"{cleanQuery}%");
            command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
        }

        var list = new List<EtsyTaxonomyNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new EtsyTaxonomyNode
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Path = reader.GetString(2),
                ParentId = reader.GetInt64(3),
                Level = reader.GetInt32(4)
            });
        }
        return list;
    }

    public async Task<EtsyTaxonomyNode?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, path, parent_id, level FROM etsy_taxonomy_nodes WHERE id = @id LIMIT 1;";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new EtsyTaxonomyNode
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Path = reader.GetString(2),
                ParentId = reader.GetInt64(3),
                Level = reader.GetInt32(4)
            };
        }
        return null;
    }

    public async Task SaveAllAsync(IEnumerable<EtsyTaxonomyNode> nodes, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO etsy_taxonomy_nodes (id, name, path, parent_id, level)
            VALUES (@id, @name, @path, @parent_id, @level);
            """;

        var pId = command.Parameters.Add("@id", SqliteType.Integer);
        var pName = command.Parameters.Add("@name", SqliteType.Text);
        var pPath = command.Parameters.Add("@path", SqliteType.Text);
        var pParentId = command.Parameters.Add("@parent_id", SqliteType.Integer);
        var pLevel = command.Parameters.Add("@level", SqliteType.Integer);

        foreach (var node in nodes)
        {
            pId.Value = node.Id;
            pName.Value = node.Name ?? string.Empty;
            pPath.Value = node.Path ?? string.Empty;
            pParentId.Value = node.ParentId;
            pLevel.Value = node.Level;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
