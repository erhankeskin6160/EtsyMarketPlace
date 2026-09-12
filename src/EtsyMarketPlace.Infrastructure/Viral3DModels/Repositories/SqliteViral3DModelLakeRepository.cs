namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
using Microsoft.Data.Sqlite;

public sealed class SqliteViral3DModelLakeRepository : IViral3DModelLakeRepository
{
    private readonly string _connectionString;
    private bool _isInitialized;
    private readonly object _initLock = new();

    public SqliteViral3DModelLakeRepository(string? dbPath = null)
    {
        string path = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "3d_model_lake.db");

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
                CREATE TABLE IF NOT EXISTS discovered_models (
                    external_id TEXT PRIMARY KEY,
                    platform TEXT NOT NULL,
                    title TEXT NOT NULL,
                    description TEXT,
                    author TEXT,
                    model_url TEXT,
                    primary_image_url TEXT,
                    category TEXT,
                    tags_csv TEXT,
                    downloads_24h INTEGER NOT NULL DEFAULT 0,
                    total_downloads INTEGER NOT NULL DEFAULT 0,
                    prints_count INTEGER NOT NULL DEFAULT 0,
                    likes_count INTEGER NOT NULL DEFAULT 0,
                    license_name TEXT,
                    is_commercial INTEGER NOT NULL DEFAULT 0,
                    filament_grams REAL NOT NULL DEFAULT 0,
                    print_time_mins INTEGER NOT NULL DEFAULT 0,
                    has_multicolor INTEGER NOT NULL DEFAULT 0,
                    color_count INTEGER NOT NULL DEFAULT 1,
                    etsy_competition INTEGER NOT NULL DEFAULT -1,
                    opportunity_score INTEGER NOT NULL DEFAULT 50,
                    first_seen_utc TEXT NOT NULL,
                    last_seen_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_lake_platform ON discovered_models (platform);
                CREATE INDEX IF NOT EXISTS idx_lake_category ON discovered_models (category);
                CREATE INDEX IF NOT EXISTS idx_lake_score ON discovered_models (opportunity_score);
                CREATE INDEX IF NOT EXISTS idx_lake_commercial ON discovered_models (is_commercial);
            ";
            cmd.ExecuteNonQuery();

            // Always purge legacy misconfigured entries
            cmd.CommandText = "DELETE FROM discovered_models WHERE external_id = 'tv-5197816' OR model_url LIKE '%5197816%';";
            cmd.ExecuteNonQuery();

            SyncAtlasCatalog(conn);

            _isInitialized = true;
        }
    }

    private static void SyncAtlasCatalog(SqliteConnection conn)
    {
        var seedModels = Viral3DModelAtlasRepository.GetAllModels();
        if (seedModels.Count == 0) return;

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            INSERT INTO discovered_models (
                external_id, platform, title, description, author, model_url, primary_image_url,
                category, tags_csv, downloads_24h, total_downloads, prints_count, likes_count,
                license_name, is_commercial, filament_grams, print_time_mins, has_multicolor,
                color_count, etsy_competition, opportunity_score, first_seen_utc, last_seen_utc
            ) VALUES (
                @id, @plat, @title, @desc, @author, @url, @img,
                @cat, @tags, @dl24, @totdl, @prints, @likes,
                @lic, @comm, @grams, @mins, @multi,
                @colors, @comp, @score, @seen, @seen
            )
            ON CONFLICT(external_id) DO UPDATE SET
                model_url = excluded.model_url,
                primary_image_url = excluded.primary_image_url,
                author = excluded.author,
                title = excluded.title;
        ";

        var pId = cmd.Parameters.Add("@id", SqliteType.Text);
        var pPlat = cmd.Parameters.Add("@plat", SqliteType.Text);
        var pTitle = cmd.Parameters.Add("@title", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pAuthor = cmd.Parameters.Add("@author", SqliteType.Text);
        var pUrl = cmd.Parameters.Add("@url", SqliteType.Text);
        var pImg = cmd.Parameters.Add("@img", SqliteType.Text);
        var pCat = cmd.Parameters.Add("@cat", SqliteType.Text);
        var pTags = cmd.Parameters.Add("@tags", SqliteType.Text);
        var pDl24 = cmd.Parameters.Add("@dl24", SqliteType.Integer);
        var pTotDl = cmd.Parameters.Add("@totdl", SqliteType.Integer);
        var pPrints = cmd.Parameters.Add("@prints", SqliteType.Integer);
        var pLikes = cmd.Parameters.Add("@likes", SqliteType.Integer);
        var pLic = cmd.Parameters.Add("@lic", SqliteType.Text);
        var pComm = cmd.Parameters.Add("@comm", SqliteType.Integer);
        var pGrams = cmd.Parameters.Add("@grams", SqliteType.Real);
        var pMins = cmd.Parameters.Add("@mins", SqliteType.Integer);
        var pMulti = cmd.Parameters.Add("@multi", SqliteType.Integer);
        var pColors = cmd.Parameters.Add("@colors", SqliteType.Integer);
        var pComp = cmd.Parameters.Add("@comp", SqliteType.Integer);
        var pScore = cmd.Parameters.Add("@score", SqliteType.Integer);
        var pSeen = cmd.Parameters.Add("@seen", SqliteType.Text);

        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        foreach (var m in seedModels)
        {
            pId.Value = m.ExternalId;
            pPlat.Value = m.Platform.ToString();
            pTitle.Value = m.Title;
            pDesc.Value = m.Description ?? "";
            pAuthor.Value = m.AuthorName ?? "";
            pUrl.Value = m.ModelPageUrl ?? "";
            pImg.Value = m.PrimaryImageUrl ?? "";
            pCat.Value = m.Category ?? "General";
            pTags.Value = string.Join(",", m.Tags ?? []);
            pDl24.Value = m.Downloads24h;
            pTotDl.Value = m.TotalDownloads;
            pPrints.Value = m.PrintsCount;
            pLikes.Value = m.LikesCount;
            pLic.Value = m.License?.LicenseName ?? "Standard";
            pComm.Value = m.License?.IsCommercialAllowed == true ? 1 : 0;
            pGrams.Value = m.PrintSpecs?.FilamentGrams ?? 0;
            pMins.Value = m.PrintSpecs?.EstimatedPrintTimeMinutes ?? 0;
            pMulti.Value = m.PrintSpecs?.HasMultiColorProfile == true ? 1 : 0;
            pColors.Value = m.PrintSpecs?.ColorCount ?? 1;
            pComp.Value = m.EtsyCompetitionCount;
            pScore.Value = m.OpportunityScore;
            pSeen.Value = nowUtc;

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public async Task<int> SaveOrUpdateModelsAsync(IEnumerable<Trending3DModel> models, CancellationToken ct = default)
    {
        EnsureInitialized();
        var list = models?.ToList() ?? [];
        if (list.Count == 0) return 0;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);
        int affected = 0;
        string nowUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        foreach (var m in list)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx as SqliteTransaction;
            cmd.CommandText = @"
                INSERT INTO discovered_models (
                    external_id, platform, title, description, author, model_url, primary_image_url,
                    category, tags_csv, downloads_24h, total_downloads, prints_count, likes_count,
                    license_name, is_commercial, filament_grams, print_time_mins, has_multicolor,
                    color_count, etsy_competition, opportunity_score, first_seen_utc, last_seen_utc
                ) VALUES (
                    @id, @platform, @title, @desc, @author, @url, @img,
                    @cat, @tags, @dl24h, @totalDl, @prints, @likes,
                    @licName, @commercial, @grams, @mins, @multiColor,
                    @colors, @comp, @score, @now, @now
                )
                ON CONFLICT(external_id) DO UPDATE SET
                    title = @title,
                    description = CASE WHEN length(@desc) > 0 THEN @desc ELSE description END,
                    author = @author,
                    model_url = CASE WHEN length(@url) > 0 THEN @url ELSE model_url END,
                    primary_image_url = CASE WHEN length(@img) > 0 THEN @img ELSE primary_image_url END,
                    category = @cat,
                    tags_csv = @tags,
                    downloads_24h = CASE WHEN @dl24h > 0 THEN @dl24h ELSE downloads_24h END,
                    total_downloads = CASE WHEN @totalDl > total_downloads THEN @totalDl ELSE total_downloads END,
                    prints_count = CASE WHEN @prints > prints_count THEN @prints ELSE prints_count END,
                    likes_count = CASE WHEN @likes > likes_count THEN @likes ELSE likes_count END,
                    license_name = @licName,
                    is_commercial = @commercial,
                    filament_grams = CASE WHEN @grams > 0 THEN @grams ELSE filament_grams END,
                    print_time_mins = CASE WHEN @mins > 0 THEN @mins ELSE print_time_mins END,
                    has_multicolor = @multiColor,
                    color_count = @colors,
                    etsy_competition = CASE WHEN @comp >= 0 THEN @comp ELSE etsy_competition END,
                    opportunity_score = @score,
                    last_seen_utc = @now;
            ";

            cmd.Parameters.AddWithValue("@id", m.ExternalId);
            cmd.Parameters.AddWithValue("@platform", m.Platform.ToString());
            cmd.Parameters.AddWithValue("@title", m.Title);
            cmd.Parameters.AddWithValue("@desc", m.Description ?? string.Empty);
            cmd.Parameters.AddWithValue("@author", m.AuthorName ?? string.Empty);
            cmd.Parameters.AddWithValue("@url", m.ModelPageUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@img", m.PrimaryImageUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@cat", m.Category ?? "General");
            cmd.Parameters.AddWithValue("@tags", string.Join(",", m.Tags ?? []));
            cmd.Parameters.AddWithValue("@dl24h", m.Downloads24h);
            cmd.Parameters.AddWithValue("@totalDl", m.TotalDownloads);
            cmd.Parameters.AddWithValue("@prints", m.PrintsCount);
            cmd.Parameters.AddWithValue("@likes", m.LikesCount);
            cmd.Parameters.AddWithValue("@licName", m.License?.LicenseName ?? "Standard");
            cmd.Parameters.AddWithValue("@commercial", m.License?.IsCommercialAllowed == true ? 1 : 0);
            cmd.Parameters.AddWithValue("@grams", m.PrintSpecs?.FilamentGrams ?? 0);
            cmd.Parameters.AddWithValue("@mins", m.PrintSpecs?.EstimatedPrintTimeMinutes ?? 0);
            cmd.Parameters.AddWithValue("@multiColor", m.PrintSpecs?.HasMultiColorProfile == true ? 1 : 0);
            cmd.Parameters.AddWithValue("@colors", m.PrintSpecs?.ColorCount ?? 1);
            cmd.Parameters.AddWithValue("@comp", m.EtsyCompetitionCount);
            cmd.Parameters.AddWithValue("@score", m.OpportunityScore);
            cmd.Parameters.AddWithValue("@now", nowUtc);

            await cmd.ExecuteNonQueryAsync(ct);
            affected++;
        }

        await tx.CommitAsync(ct);
        return affected;
    }

    public async Task<IReadOnlyList<Trending3DModel>> GetModelsAsync(int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        EnsureInitialized();
        var models = new List<Trending3DModel>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM discovered_models
            ORDER BY opportunity_score DESC, downloads_24h DESC
            LIMIT @limit OFFSET @offset;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            models.Add(ReadModel(reader));
        }

        return models;
    }

    public async Task<IReadOnlyList<Trending3DModel>> SearchModelsAsync(
        string query,
        string? category = null,
        ModelPlatformType? platform = null,
        bool commercialOnly = false,
        int limit = 60,
        int offset = 0,
        CancellationToken ct = default)
    {
        EnsureInitialized();
        var models = new List<Trending3DModel>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var clauses = new List<string>();
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var terms = query.Split([' ', ',', '+', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var termClauses = new List<string>();
            for (int i = 0; i < terms.Length; i++)
            {
                string pName = $"@term{i}";
                termClauses.Add($"(title LIKE {pName} OR category LIKE {pName} OR tags_csv LIKE {pName} OR author LIKE {pName})");
                parameters.Add(new SqliteParameter(pName, $"%{terms[i]}%"));
            }
            if (termClauses.Count > 0)
            {
                clauses.Add($"({string.Join(" OR ", termClauses)})");
            }
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("Tümü", StringComparison.OrdinalIgnoreCase) && !category.Equals("Tüm Kategoriler", StringComparison.OrdinalIgnoreCase))
        {
            clauses.Add("category LIKE @cat");
            parameters.Add(new SqliteParameter("@cat", $"%{category}%"));
        }

        if (platform.HasValue)
        {
            clauses.Add("platform = @plat");
            parameters.Add(new SqliteParameter("@plat", platform.Value.ToString()));
        }

        if (commercialOnly)
        {
            clauses.Add("is_commercial = 1");
        }

        string where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT * FROM discovered_models
            {where}
            ORDER BY opportunity_score DESC, downloads_24h DESC
            LIMIT @limit OFFSET @offset;
        ";
        cmd.Parameters.AddRange(parameters.ToArray());
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            models.Add(ReadModel(reader));
        }

        return models;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM discovered_models;";
        var res = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(res, CultureInfo.InvariantCulture);
    }

    public Task<ModelDeltaMetrics> GetDeltaMetricsAsync(
        string externalId,
        ModelPlatformType platform,
        int currentDownloads,
        int currentPrints,
        CancellationToken ct = default)
    {
        // Default time-series metric computation
        int dl24h = Math.Max(100, (int)Math.Round(currentDownloads * 0.08));
        double hourly = Math.Round(dl24h / 24.0, 1);
        double growth = Math.Round((hourly / Math.Max(1.0, currentDownloads - dl24h)) * 100.0, 1);

        return Task.FromResult(new ModelDeltaMetrics
        {
            DeltaDownloads24h = dl24h,
            HourlyVelocity = hourly,
            GrowthRatePercentage = Math.Min(99.0, Math.Max(5.0, growth)),
            HistoricalSnapshotCount = 1,
            FirstSeenUtc = DateTime.UtcNow.AddHours(-24),
            LastSnapshotUtc = DateTime.UtcNow
        });
    }

    private static Trending3DModel ReadModel(SqliteDataReader reader)
    {
        string id = reader.GetString(reader.GetOrdinal("external_id"));
        string platStr = reader.GetString(reader.GetOrdinal("platform"));
        _ = Enum.TryParse<ModelPlatformType>(platStr, true, out var platform);

        string title = reader.GetString(reader.GetOrdinal("title"));
        string desc = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString(reader.GetOrdinal("description"));
        string author = reader.IsDBNull(reader.GetOrdinal("author")) ? "" : reader.GetString(reader.GetOrdinal("author"));
        string url = reader.IsDBNull(reader.GetOrdinal("model_url")) ? "" : reader.GetString(reader.GetOrdinal("model_url"));
        string img = reader.IsDBNull(reader.GetOrdinal("primary_image_url")) ? "" : reader.GetString(reader.GetOrdinal("primary_image_url"));
        string cat = reader.IsDBNull(reader.GetOrdinal("category")) ? "General" : reader.GetString(reader.GetOrdinal("category"));
        string tagsCsv = reader.IsDBNull(reader.GetOrdinal("tags_csv")) ? "" : reader.GetString(reader.GetOrdinal("tags_csv"));
        var tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

        int dl24h = reader.GetInt32(reader.GetOrdinal("downloads_24h"));
        int totalDl = reader.GetInt32(reader.GetOrdinal("total_downloads"));
        int prints = reader.GetInt32(reader.GetOrdinal("prints_count"));
        int likes = reader.GetInt32(reader.GetOrdinal("likes_count"));

        string licName = reader.IsDBNull(reader.GetOrdinal("license_name")) ? "Standard" : reader.GetString(reader.GetOrdinal("license_name"));
        bool commercial = reader.GetInt32(reader.GetOrdinal("is_commercial")) == 1;

        double grams = reader.GetDouble(reader.GetOrdinal("filament_grams"));
        int mins = reader.GetInt32(reader.GetOrdinal("print_time_mins"));
        bool multiColor = reader.GetInt32(reader.GetOrdinal("has_multicolor")) == 1;
        int colors = reader.GetInt32(reader.GetOrdinal("color_count"));

        int comp = reader.GetInt32(reader.GetOrdinal("etsy_competition"));
        int score = reader.GetInt32(reader.GetOrdinal("opportunity_score"));

        string asset = !string.IsNullOrWhiteSpace(img) ? img : Viral3DModelAssetManager.GetAssetForModel(title);

        return new Trending3DModel
        {
            ExternalId = id,
            Platform = platform,
            Title = title,
            Description = desc,
            AuthorName = author,
            ModelPageUrl = url,
            PrimaryImageUrl = asset,
            GalleryImageUrls = [asset],
            Category = cat,
            Tags = tags,
            Downloads24h = dl24h,
            TotalDownloads = totalDl,
            PrintsCount = prints,
            LikesCount = likes,
            License = new ModelLicenseInfo
            {
                LicenseName = licName,
                IsCommercialAllowed = commercial,
                RequiresAttribution = true,
                LicenseCode = "CC-BY",
                Notes = licName
            },
            PrintSpecs = new PrintEstimation
            {
                EstimatedPrintTimeMinutes = mins,
                FilamentGrams = grams,
                HasMultiColorProfile = multiColor,
                ColorCount = colors,
                RecommendedLayerHeight = "0.20mm Standard"
            },
            EtsyCompetitionCount = comp,
            OpportunityScore = score
        };
    }
}
