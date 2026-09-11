namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using Microsoft.Data.Sqlite;

public sealed class Sqlite3DModelSnapshotRepository : I3DModelSnapshotRepository
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly object _initLock = new();

    public Sqlite3DModelSnapshotRepository(string? dbPath = null)
    {
        string path = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "viral_3d_snapshots.db");

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={path}";
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        lock (_initLock)
        {
            if (_isInitialized) return;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS trending_3d_snapshots (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    model_id TEXT NOT NULL,
                    platform TEXT NOT NULL,
                    title TEXT NOT NULL,
                    author TEXT NOT NULL,
                    downloads_count INTEGER NOT NULL,
                    prints_count INTEGER NOT NULL,
                    likes_count INTEGER NOT NULL,
                    snapshot_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_snapshots_lookup 
                ON trending_3d_snapshots(model_id, platform, snapshot_utc);

                CREATE TABLE IF NOT EXISTS trending_etsy_keyword_cache (
                    keyword TEXT PRIMARY KEY,
                    competition_count INTEGER NOT NULL,
                    cached_at_utc TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
            _isInitialized = true;
        }
    }

    public async Task SaveSnapshotsAsync(IEnumerable<Trending3DModel> models, CancellationToken ct = default)
    {
        EnsureInitialized();
        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var transaction = conn.BeginTransaction();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO trending_3d_snapshots 
            (model_id, platform, title, author, downloads_count, prints_count, likes_count, snapshot_utc)
            VALUES ($id, $plat, $title, $author, $down, $prints, $likes, $time);
        ";

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pPlat = cmd.Parameters.Add("$plat", SqliteType.Text);
        var pTitle = cmd.Parameters.Add("$title", SqliteType.Text);
        var pAuthor = cmd.Parameters.Add("$author", SqliteType.Text);
        var pDown = cmd.Parameters.Add("$down", SqliteType.Integer);
        var pPrints = cmd.Parameters.Add("$prints", SqliteType.Integer);
        var pLikes = cmd.Parameters.Add("$likes", SqliteType.Integer);
        var pTime = cmd.Parameters.Add("$time", SqliteType.Text);

        foreach (var m in models)
        {
            pId.Value = m.ExternalId;
            pPlat.Value = m.Platform.ToString();
            pTitle.Value = m.Title;
            pAuthor.Value = m.AuthorName;
            pDown.Value = m.TotalDownloads;
            pPrints.Value = m.PrintsCount;
            pLikes.Value = m.LikesCount;
            pTime.Value = nowUtc;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<ModelDeltaMetrics> GetDeltaMetricsAsync(
        string externalId,
        ModelPlatformType platform,
        int currentDownloads,
        int currentPrints,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT downloads_count, prints_count, snapshot_utc
            FROM trending_3d_snapshots
            WHERE model_id = $id AND platform = $plat
            ORDER BY snapshot_utc DESC;
        ";
        cmd.Parameters.AddWithValue("$id", externalId);
        cmd.Parameters.AddWithValue("$plat", platform.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        int count = 0;
        int oldestDownloads = -1;
        DateTime? oldestTime = null;
        DateTime? lastTime = null;

        while (await reader.ReadAsync(ct))
        {
            count++;
            int d = reader.GetInt32(0);
            string timeStr = reader.GetString(2);
            if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                lastTime ??= dt;

                // Find a snapshot around 18-30 hours ago, or take the oldest available if < 24h
                var diffHours = (DateTime.UtcNow - dt).TotalHours;
                if (diffHours >= 18 && diffHours <= 30)
                {
                    oldestDownloads = d;
                    oldestTime = dt;
                    break;
                }

                oldestDownloads = d;
                oldestTime = dt;
            }
        }

        if (count > 1 && oldestTime.HasValue && oldestDownloads >= 0)
        {
            double elapsedHours = (DateTime.UtcNow - oldestTime.Value).TotalHours;
            if (elapsedHours < 0.2) elapsedHours = 0.2;

            int rawDelta = Math.Max(0, currentDownloads - oldestDownloads);
            double hourly = Math.Round(rawDelta / elapsedHours, 1);
            int delta24h = (int)Math.Round(hourly * 24.0);

            double baseline = Math.Max(1, oldestDownloads);
            double growth = Math.Round(((double)rawDelta / baseline) * 100.0, 1);

            return new ModelDeltaMetrics
            {
                DeltaDownloads24h = delta24h > 0 ? delta24h : (int)(currentDownloads * 0.12),
                HourlyVelocity = hourly,
                GrowthRatePercentage = growth,
                HistoricalSnapshotCount = count,
                FirstSeenUtc = oldestTime,
                LastSnapshotUtc = lastTime
            };
        }

        // Single or no previous snapshot: estimate based on current volume
        int estimated24h = (int)Math.Round(currentDownloads * 0.15);
        return ModelDeltaMetrics.Estimated(currentDownloads, estimated24h);
    }
}
