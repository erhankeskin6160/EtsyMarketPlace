namespace EtsyMarketPlace.Infrastructure.Taxonomy;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        await using (var command = connection.CreateCommand())
        {
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

        // Tablo boş ise hazır JSON dosyasından otomatik tohumlama (offline seed) yap
        await TrySeedFromLocalJsonInternalAsync(connection, cancellationToken);
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
        await SaveAllInternalAsync(connection, nodes, cancellationToken);
    }

    private static async Task SaveAllInternalAsync(SqliteConnection connection, IEnumerable<EtsyTaxonomyNode> nodes, CancellationToken cancellationToken)
    {
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

    private async Task TrySeedFromLocalJsonInternalAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM etsy_taxonomy_nodes;";
            var countObj = await countCmd.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(countObj) > 0) return;

            var baseDir = AppContext.BaseDirectory;
            var candidatePaths = new List<string>
            {
                Path.Combine(baseDir, "etsy_seller_taxonomy.json"),
                Path.Combine(baseDir, "Taxonomy", "etsy_seller_taxonomy.json")
            };

            if (_connectionString.Contains("EtsyMarketPlace", StringComparison.OrdinalIgnoreCase) ||
                _connectionString.Contains("etsy-finance.db", StringComparison.OrdinalIgnoreCase))
            {
                candidatePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EtsyMarketPlace", "etsy_seller_taxonomy.json"));
            }

            string? foundPath = candidatePaths.FirstOrDefault(File.Exists);
            if (foundPath == null) return;

            var json = await File.ReadAllTextAsync(foundPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json)) return;

            using var doc = JsonDocument.Parse(json);
            var nodes = new List<EtsyTaxonomyNode>();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    ParseFlatJsonNode(el, nodes);
                }
            }
            else if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in results.EnumerateArray())
                {
                    ParseFlatJsonNode(el, nodes);
                }
            }

            if (nodes.Count > 0)
            {
                await SaveAllInternalAsync(connection, nodes, cancellationToken);
            }
        }
        catch
        {
            // Tohumlama hatası uygulamanın açılmasını engellemez
        }
    }

    private static void ParseFlatJsonNode(JsonElement el, List<EtsyTaxonomyNode> nodes)
    {
        if (el.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var id) &&
            el.TryGetProperty("name", out var nameProp) &&
            el.TryGetProperty("path", out var pathProp))
        {
            var name = nameProp.GetString() ?? string.Empty;
            var path = pathProp.GetString() ?? name;
            var parentId = el.TryGetProperty("parent_id", out var pidProp) && pidProp.TryGetInt64(out var pid) ? pid : 0;
            var level = el.TryGetProperty("level", out var lvlProp) && lvlProp.TryGetInt32(out var lvl) ? lvl : 0;
            nodes.Add(new EtsyTaxonomyNode(id, name, path, parentId, level));
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
