namespace EtsyMarketPlace.Infrastructure.AiUsage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using Microsoft.Data.Sqlite;

public sealed class SqliteAiUsageRepository : IAiUsageRepository
{
    private readonly string _connectionString;

    public SqliteAiUsageRepository(string databasePath)
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

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ai_usage_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                module_name TEXT NOT NULL DEFAULT '',
                provider TEXT NOT NULL DEFAULT '',
                model_name TEXT NOT NULL DEFAULT '',
                prompt_tokens INTEGER NOT NULL DEFAULT 0,
                completion_tokens INTEGER NOT NULL DEFAULT 0,
                total_tokens INTEGER NOT NULL DEFAULT 0,
                cost_usd REAL NOT NULL DEFAULT 0,
                cost_try REAL NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'Başarılı',
                note TEXT
            );

            CREATE INDEX IF NOT EXISTS ix_ai_usage_history_created
                ON ai_usage_history(created_at DESC);

            CREATE INDEX IF NOT EXISTS ix_ai_usage_history_provider
                ON ai_usage_history(provider);

            CREATE TABLE IF NOT EXISTS ai_remote_usage_cache (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                provider TEXT NOT NULL,
                cache_key TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'general',
                payload_json TEXT NOT NULL,
                is_finalized INTEGER NOT NULL DEFAULT 0,
                expires_at TEXT,
                updated_at TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_ai_remote_cache_key
                ON ai_remote_usage_cache(provider, cache_key);

            CREATE INDEX IF NOT EXISTS ix_ai_remote_cache_cat
                ON ai_remote_usage_cache(provider, category, is_finalized);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AiUsageRecord> SaveUsageAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ai_usage_history(
                created_at, module_name, provider, model_name,
                prompt_tokens, completion_tokens, total_tokens,
                cost_usd, cost_try, status, note
            )
            VALUES (
                $created_at, $module_name, $provider, $model_name,
                $prompt_tokens, $completion_tokens, $total_tokens,
                $cost_usd, $cost_try, $status, $note
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$created_at", record.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("$module_name", record.ModuleName ?? "");
        command.Parameters.AddWithValue("$provider", record.Provider ?? "");
        command.Parameters.AddWithValue("$model_name", record.ModelName ?? "");
        command.Parameters.AddWithValue("$prompt_tokens", record.PromptTokens);
        command.Parameters.AddWithValue("$completion_tokens", record.CompletionTokens);
        command.Parameters.AddWithValue("$total_tokens", record.TotalTokens);
        command.Parameters.AddWithValue("$cost_usd", (double)record.EstimatedCostUsd);
        command.Parameters.AddWithValue("$cost_try", (double)record.EstimatedCostTry);
        command.Parameters.AddWithValue("$status", record.Status ?? "Başarılı");
        command.Parameters.AddWithValue("$note", (object?)record.Note ?? DBNull.Value);

        var idObj = await command.ExecuteScalarAsync(cancellationToken);
        record.Id = Convert.ToInt64(idObj);
        return record;
    }

    public async Task<IReadOnlyList<AiUsageRecord>> GetHistoryAsync(
        string? providerFilter = null,
        DateTimeOffset? since = null,
        int limit = 300,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AiUsageRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var query = "SELECT id, created_at, module_name, provider, model_name, prompt_tokens, completion_tokens, total_tokens, cost_usd, cost_try, status, note FROM ai_usage_history WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(providerFilter) && !providerFilter.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
        {
            query += " AND provider LIKE $provider";
            command.Parameters.AddWithValue("$provider", $"%{providerFilter}%");
        }

        if (since.HasValue)
        {
            query += " AND created_at >= $since";
            command.Parameters.AddWithValue("$since", since.Value.ToString("o"));
        }

        query += " ORDER BY created_at DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);

        command.CommandText = query;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AiUsageRecord
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTimeOffset.TryParse(reader.GetString(1), out var dt) ? dt : DateTimeOffset.Now,
                ModuleName = reader.GetString(2),
                Provider = reader.GetString(3),
                ModelName = reader.GetString(4),
                PromptTokens = reader.GetInt32(5),
                CompletionTokens = reader.GetInt32(6),
                TotalTokens = reader.GetInt32(7),
                EstimatedCostUsd = Convert.ToDecimal(reader.GetDouble(8)),
                EstimatedCostTry = Convert.ToDecimal(reader.GetDouble(9)),
                Status = reader.GetString(10),
                Note = reader.IsDBNull(11) ? null : reader.GetString(11)
            });
        }

        return results;
    }

    public async Task<AiUsageSummaryStats> GetSummaryStatsAsync(
        string? providerFilter = null,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var stats = new AiUsageSummaryStats
        {
            ProviderFilter = string.IsNullOrWhiteSpace(providerFilter) ? "Tümü" : providerFilter
        };

        var history = await GetHistoryAsync(providerFilter, since, limit: 10000, cancellationToken);
        foreach (var r in history)
        {
            stats.TotalRequests++;
            if (r.Status.Contains("429", StringComparison.OrdinalIgnoreCase) || r.Status.Contains("Kota", StringComparison.OrdinalIgnoreCase))
            {
                stats.Blocked429Requests++;
            }
            else if (r.Status.Contains("Hata", StringComparison.OrdinalIgnoreCase) || r.Status.Contains("Error", StringComparison.OrdinalIgnoreCase))
            {
                stats.ErrorRequests++;
            }
            else
            {
                stats.SuccessfulRequests++;
            }

            stats.TotalPromptTokens += r.PromptTokens;
            stats.TotalCompletionTokens += r.CompletionTokens;
            stats.TotalTokens += r.TotalTokens;
            stats.TotalCostUsd += r.EstimatedCostUsd;
            stats.TotalCostTry += r.EstimatedCostTry;

            if (!string.IsNullOrWhiteSpace(r.ModelName))
            {
                if (!stats.CostByModel.ContainsKey(r.ModelName)) stats.CostByModel[r.ModelName] = 0;
                stats.CostByModel[r.ModelName] += r.EstimatedCostUsd;
            }

            if (!string.IsNullOrWhiteSpace(r.Provider))
            {
                if (!stats.TokensByProvider.ContainsKey(r.Provider)) stats.TokensByProvider[r.Provider] = 0;
                stats.TokensByProvider[r.Provider] += r.TotalTokens;
            }
        }

        return stats;
    }

    public async Task<string?> GetCachedPayloadAsync(string provider, string cacheKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload_json FROM ai_remote_usage_cache
            WHERE provider = $provider AND cache_key = $cache_key
              AND (expires_at IS NULL OR expires_at > $now)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$cache_key", cacheKey);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value ? Convert.ToString(result) : null;
    }

    public async Task SaveCachedPayloadAsync(
        string provider,
        string cacheKey,
        string category,
        string payloadJson,
        bool isFinalized,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ai_remote_usage_cache(provider, cache_key, category, payload_json, is_finalized, expires_at, updated_at)
            VALUES ($provider, $cache_key, $category, $payload_json, $is_finalized, $expires_at, $updated_at)
            ON CONFLICT(provider, cache_key) DO UPDATE SET
                category = excluded.category,
                payload_json = excluded.payload_json,
                is_finalized = excluded.is_finalized,
                expires_at = excluded.expires_at,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$cache_key", cacheKey);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$payload_json", payloadJson);
        command.Parameters.AddWithValue("$is_finalized", isFinalized ? 1 : 0);
        command.Parameters.AddWithValue("$expires_at", expiresAt.HasValue ? expiresAt.Value.ToString("o") : (object)DBNull.Value);
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetFinalizedDailyItemsAsync(
        string provider,
        string category,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cache_key, payload_json FROM ai_remote_usage_cache
            WHERE provider = $provider AND category = $category AND is_finalized = 1
              AND cache_key >= $sinceKey;
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$sinceKey", since.ToString("yyyy-MM-dd"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var payload = reader.GetString(1);
            dict[key] = payload;
        }

        return dict;
    }

    public async Task InvalidateCacheAsync(string? provider = null, string? cacheKey = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var query = "DELETE FROM ai_remote_usage_cache WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(provider))
        {
            query += " AND provider = $provider";
            command.Parameters.AddWithValue("$provider", provider);
        }
        if (!string.IsNullOrWhiteSpace(cacheKey))
        {
            query += " AND cache_key = $cache_key";
            command.Parameters.AddWithValue("$cache_key", cacheKey);
        }

        command.CommandText = query;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
