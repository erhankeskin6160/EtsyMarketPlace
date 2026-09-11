namespace EtsyMarketPlace.Infrastructure.ShopVault.Repositories;

using System.Data;
using System.Text.Json;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using Microsoft.Data.Sqlite;

public sealed class SqliteShopVaultRepository : IShopVaultRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private bool _isInitialized;
    private readonly object _initLock = new();

    public SqliteShopVaultRepository(string? dbPath = null)
    {
        var path = dbPath ?? VaultPathHelper.DatabasePath;
        var dir = Path.GetDirectoryName(path);
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
                CREATE TABLE IF NOT EXISTS vault_sessions (
                    session_id TEXT PRIMARY KEY,
                    shop_id INTEGER NOT NULL,
                    shop_name TEXT NOT NULL,
                    shop_url TEXT,
                    created_at_utc TEXT NOT NULL,
                    completed_at_utc TEXT,
                    total_listings INTEGER NOT NULL DEFAULT 0,
                    total_images INTEGER NOT NULL DEFAULT 0,
                    total_size_bytes INTEGER NOT NULL DEFAULT 0,
                    archive_zip_path TEXT,
                    status TEXT NOT NULL,
                    error_message TEXT
                );

                CREATE TABLE IF NOT EXISTS vault_listings (
                    session_id TEXT NOT NULL,
                    listing_id INTEGER NOT NULL,
                    original_shop_id INTEGER NOT NULL,
                    original_shop_name TEXT NOT NULL,
                    title TEXT NOT NULL,
                    description TEXT,
                    price REAL NOT NULL,
                    currency TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    tags_json TEXT,
                    materials_json TEXT,
                    taxonomy_id INTEGER NOT NULL DEFAULT 0,
                    taxonomy_path TEXT,
                    who_made TEXT,
                    when_made TEXT,
                    is_supply INTEGER NOT NULL DEFAULT 0,
                    is_digital INTEGER NOT NULL DEFAULT 0,
                    shipping_profile_id INTEGER,
                    shipping_profile_title TEXT,
                    return_policy_id INTEGER,
                    state TEXT NOT NULL,
                    original_url TEXT,
                    backed_up_at_utc TEXT NOT NULL,
                    PRIMARY KEY (session_id, listing_id)
                );

                CREATE TABLE IF NOT EXISTS vault_images (
                    id TEXT PRIMARY KEY,
                    session_id TEXT NOT NULL,
                    listing_id INTEGER NOT NULL,
                    original_image_id INTEGER NOT NULL,
                    rank INTEGER NOT NULL,
                    local_relative_path TEXT NOT NULL,
                    original_url TEXT,
                    hex_code TEXT,
                    file_size_bytes INTEGER NOT NULL DEFAULT 0,
                    width INTEGER NOT NULL DEFAULT 0,
                    height INTEGER NOT NULL DEFAULT 0,
                    downloaded_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS vault_variations (
                    id TEXT PRIMARY KEY,
                    session_id TEXT NOT NULL,
                    listing_id INTEGER NOT NULL,
                    property_id INTEGER NOT NULL,
                    property_name TEXT NOT NULL,
                    value_id INTEGER NOT NULL,
                    value_name TEXT NOT NULL,
                    price_difference REAL NOT NULL DEFAULT 0,
                    sku TEXT,
                    stock_quantity INTEGER NOT NULL DEFAULT 999,
                    is_available INTEGER NOT NULL DEFAULT 1
                );

                CREATE INDEX IF NOT EXISTS idx_listings_session ON vault_listings(session_id);
                CREATE INDEX IF NOT EXISTS idx_images_session_listing ON vault_images(session_id, listing_id);
                CREATE INDEX IF NOT EXISTS idx_variations_session_listing ON vault_variations(session_id, listing_id);
            ";
            cmd.ExecuteNonQuery();
            _isInitialized = true;
        }
    }

    public async Task<List<VaultBackupSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var result = new List<VaultBackupSession>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT session_id, shop_id, shop_name, shop_url, created_at_utc, completed_at_utc, total_listings, total_images, total_size_bytes, archive_zip_path, status, error_message FROM vault_sessions ORDER BY datetime(created_at_utc) DESC;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var session = new VaultBackupSession
            {
                SessionId = reader.GetString(0),
                ShopId = reader.GetInt64(1),
                ShopName = reader.GetString(2),
                ShopUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                CreatedAtUtc = DateTime.Parse(reader.GetString(4)),
                CompletedAtUtc = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                TotalListingsCount = reader.GetInt32(6),
                TotalImagesCount = reader.GetInt32(7),
                TotalSizeBytes = reader.GetInt64(8),
                ArchiveZipPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                Status = reader.GetString(10),
                ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
            };
            result.Add(session);
        }

        return result;
    }

    public async Task<VaultBackupSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT session_id, shop_id, shop_name, shop_url, created_at_utc, completed_at_utc, total_listings, total_images, total_size_bytes, archive_zip_path, status, error_message FROM vault_sessions WHERE session_id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", sessionId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new VaultBackupSession
            {
                SessionId = reader.GetString(0),
                ShopId = reader.GetInt64(1),
                ShopName = reader.GetString(2),
                ShopUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                CreatedAtUtc = DateTime.Parse(reader.GetString(4)),
                CompletedAtUtc = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                TotalListingsCount = reader.GetInt32(6),
                TotalImagesCount = reader.GetInt32(7),
                TotalSizeBytes = reader.GetInt64(8),
                ArchiveZipPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                Status = reader.GetString(10),
                ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
            };
        }

        return null;
    }

    public async Task SaveSessionAsync(VaultBackupSession session, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO vault_sessions (
                session_id, shop_id, shop_name, shop_url, created_at_utc, completed_at_utc,
                total_listings, total_images, total_size_bytes, archive_zip_path, status, error_message
            ) VALUES (
                @session_id, @shop_id, @shop_name, @shop_url, @created_at_utc, @completed_at_utc,
                @total_listings, @total_images, @total_size_bytes, @archive_zip_path, @status, @error_message
            )
            ON CONFLICT(session_id) DO UPDATE SET
                completed_at_utc = excluded.completed_at_utc,
                total_listings = excluded.total_listings,
                total_images = excluded.total_images,
                total_size_bytes = excluded.total_size_bytes,
                archive_zip_path = excluded.archive_zip_path,
                status = excluded.status,
                error_message = excluded.error_message;
        ";

        cmd.Parameters.AddWithValue("@session_id", session.SessionId);
        cmd.Parameters.AddWithValue("@shop_id", session.ShopId);
        cmd.Parameters.AddWithValue("@shop_name", session.ShopName);
        cmd.Parameters.AddWithValue("@shop_url", (object?)session.ShopUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at_utc", session.CreatedAtUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@completed_at_utc", session.CompletedAtUtc.HasValue ? session.CompletedAtUtc.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@total_listings", session.TotalListingsCount);
        cmd.Parameters.AddWithValue("@total_images", session.TotalImagesCount);
        cmd.Parameters.AddWithValue("@total_size_bytes", session.TotalSizeBytes);
        cmd.Parameters.AddWithValue("@archive_zip_path", (object?)session.ArchiveZipPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", session.Status);
        cmd.Parameters.AddWithValue("@error_message", (object?)session.ErrorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = "DELETE FROM vault_variations WHERE session_id = @id; DELETE FROM vault_images WHERE session_id = @id; DELETE FROM vault_listings WHERE session_id = @id; DELETE FROM vault_sessions WHERE session_id = @id;";
        cmd.Parameters.AddWithValue("@id", sessionId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<List<VaultListing>> GetListingsBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var listings = new List<VaultListing>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // 1. Fetch Listings
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT session_id, listing_id, original_shop_id, original_shop_name, title, description, price, currency, quantity, tags_json, materials_json, taxonomy_id, taxonomy_path, who_made, when_made, is_supply, is_digital, shipping_profile_id, shipping_profile_title, return_policy_id, state, original_url, backed_up_at_utc FROM vault_listings WHERE session_id = @id ORDER BY listing_id ASC;";
            cmd.Parameters.AddWithValue("@id", sessionId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var listing = new VaultListing
                {
                    SessionId = reader.GetString(0),
                    ListingId = reader.GetInt64(1),
                    OriginalShopId = reader.GetInt64(2),
                    OriginalShopName = reader.GetString(3),
                    Title = reader.GetString(4),
                    Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Price = Convert.ToDecimal(reader.GetDouble(6)),
                    Currency = reader.GetString(7),
                    Quantity = reader.GetInt32(8),
                    TaxonomyId = reader.GetInt64(11),
                    TaxonomyPath = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    WhoMade = reader.IsDBNull(13) ? "i_did" : reader.GetString(13),
                    WhenMade = reader.IsDBNull(14) ? "made_to_order" : reader.GetString(14),
                    IsSupply = reader.GetInt32(15) == 1,
                    IsDigital = reader.GetInt32(16) == 1,
                    ShippingProfileId = reader.IsDBNull(17) ? null : reader.GetInt64(17),
                    ShippingProfileTitle = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                    ReturnPolicyId = reader.IsDBNull(19) ? null : reader.GetInt64(19),
                    State = reader.GetString(20),
                    OriginalListingUrl = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                    BackedUpAtUtc = DateTime.Parse(reader.GetString(22)),
                };

                if (!reader.IsDBNull(9))
                {
                    try { listing.Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(9), JsonOptions) ?? []; } catch { }
                }
                if (!reader.IsDBNull(10))
                {
                    try { listing.Materials = JsonSerializer.Deserialize<List<string>>(reader.GetString(10), JsonOptions) ?? []; } catch { }
                }

                listings.Add(listing);
            }
        }

        // 2. Fetch Images
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, listing_id, original_image_id, rank, local_relative_path, original_url, hex_code, file_size_bytes, width, height, downloaded_at_utc FROM vault_images WHERE session_id = @id ORDER BY listing_id, rank ASC;";
            cmd.Parameters.AddWithValue("@id", sessionId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var listingDict = listings.ToDictionary(l => l.ListingId);
            while (await reader.ReadAsync(cancellationToken))
            {
                var listingId = reader.GetInt64(1);
                if (listingDict.TryGetValue(listingId, out var listing))
                {
                    var img = new VaultListingImage
                    {
                        Id = reader.GetString(0),
                        ListingId = listingId,
                        OriginalImageId = reader.GetInt64(2),
                        Rank = reader.GetInt32(3),
                        LocalRelativePath = reader.GetString(4),
                        OriginalUrl = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        HexCode = reader.IsDBNull(6) ? null : reader.GetString(6),
                        FileSizeBytes = reader.GetInt64(7),
                        Width = reader.GetInt32(8),
                        Height = reader.GetInt32(9),
                        DownloadedAtUtc = DateTime.Parse(reader.GetString(10))
                    };
                    listing.Images.Add(img);
                }
            }
        }

        // 3. Fetch Variations
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, listing_id, property_id, property_name, value_id, value_name, price_difference, sku, stock_quantity, is_available FROM vault_variations WHERE session_id = @id ORDER BY listing_id, property_id ASC;";
            cmd.Parameters.AddWithValue("@id", sessionId);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var listingDict = listings.ToDictionary(l => l.ListingId);
            while (await reader.ReadAsync(cancellationToken))
            {
                var listingId = reader.GetInt64(1);
                if (listingDict.TryGetValue(listingId, out var listing))
                {
                    var varItem = new VaultListingVariation
                    {
                        Id = reader.GetString(0),
                        ListingId = listingId,
                        PropertyId = reader.GetInt64(2),
                        PropertyName = reader.GetString(3),
                        ValueId = reader.GetInt64(4),
                        ValueName = reader.GetString(5),
                        PriceDifference = Convert.ToDecimal(reader.GetDouble(6)),
                        Sku = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        StockQuantity = reader.GetInt32(8),
                        IsAvailable = reader.GetInt32(9) == 1
                    };
                    listing.Variations.Add(varItem);
                }
            }
        }

        return listings;
    }

    public async Task<VaultListing?> GetListingAsync(string sessionId, long listingId, CancellationToken cancellationToken = default)
    {
        var listings = await GetListingsBySessionIdAsync(sessionId, cancellationToken);
        return listings.FirstOrDefault(l => l.ListingId == listingId);
    }

    public async Task SaveListingsAsync(IEnumerable<VaultListing> listings, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction();

        foreach (var l in listings)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO vault_listings (
                        session_id, listing_id, original_shop_id, original_shop_name, title, description, price, currency, quantity,
                        tags_json, materials_json, taxonomy_id, taxonomy_path, who_made, when_made, is_supply, is_digital,
                        shipping_profile_id, shipping_profile_title, return_policy_id, state, original_url, backed_up_at_utc
                    ) VALUES (
                        @session_id, @listing_id, @original_shop_id, @original_shop_name, @title, @description, @price, @currency, @quantity,
                        @tags_json, @materials_json, @taxonomy_id, @taxonomy_path, @who_made, @when_made, @is_supply, @is_digital,
                        @shipping_profile_id, @shipping_profile_title, @return_policy_id, @state, @original_url, @backed_up_at_utc
                    )
                    ON CONFLICT(session_id, listing_id) DO UPDATE SET
                        title = excluded.title,
                        description = excluded.description,
                        price = excluded.price,
                        quantity = excluded.quantity,
                        tags_json = excluded.tags_json,
                        materials_json = excluded.materials_json,
                        shipping_profile_id = excluded.shipping_profile_id,
                        shipping_profile_title = excluded.shipping_profile_title,
                        state = excluded.state;
                ";

                cmd.Parameters.AddWithValue("@session_id", l.SessionId);
                cmd.Parameters.AddWithValue("@listing_id", l.ListingId);
                cmd.Parameters.AddWithValue("@original_shop_id", l.OriginalShopId);
                cmd.Parameters.AddWithValue("@original_shop_name", l.OriginalShopName);
                cmd.Parameters.AddWithValue("@title", l.Title);
                cmd.Parameters.AddWithValue("@description", (object?)l.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@price", (double)l.Price);
                cmd.Parameters.AddWithValue("@currency", l.Currency);
                cmd.Parameters.AddWithValue("@quantity", l.Quantity);
                cmd.Parameters.AddWithValue("@tags_json", JsonSerializer.Serialize(l.Tags));
                cmd.Parameters.AddWithValue("@materials_json", JsonSerializer.Serialize(l.Materials));
                cmd.Parameters.AddWithValue("@taxonomy_id", l.TaxonomyId);
                cmd.Parameters.AddWithValue("@taxonomy_path", (object?)l.TaxonomyPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@who_made", (object?)l.WhoMade ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@when_made", (object?)l.WhenMade ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is_supply", l.IsSupply ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_digital", l.IsDigital ? 1 : 0);
                cmd.Parameters.AddWithValue("@shipping_profile_id", (object?)l.ShippingProfileId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@shipping_profile_title", (object?)l.ShippingProfileTitle ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@return_policy_id", (object?)l.ReturnPolicyId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@state", l.State);
                cmd.Parameters.AddWithValue("@original_url", (object?)l.OriginalListingUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@backed_up_at_utc", l.BackedUpAtUtc.ToString("o"));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Save images
            foreach (var img in l.Images)
            {
                using var cmdImg = conn.CreateCommand();
                cmdImg.Transaction = tx;
                cmdImg.CommandText = @"
                    INSERT INTO vault_images (
                        id, session_id, listing_id, original_image_id, rank, local_relative_path,
                        original_url, hex_code, file_size_bytes, width, height, downloaded_at_utc
                    ) VALUES (
                        @id, @session_id, @listing_id, @original_image_id, @rank, @local_relative_path,
                        @original_url, @hex_code, @file_size_bytes, @width, @height, @downloaded_at_utc
                    )
                    ON CONFLICT(id) DO UPDATE SET
                        session_id = excluded.session_id,
                        rank = excluded.rank,
                        local_relative_path = excluded.local_relative_path;
                ";

                cmdImg.Parameters.AddWithValue("@id", img.Id);
                cmdImg.Parameters.AddWithValue("@session_id", l.SessionId);
                cmdImg.Parameters.AddWithValue("@listing_id", l.ListingId);
                cmdImg.Parameters.AddWithValue("@original_image_id", img.OriginalImageId);
                cmdImg.Parameters.AddWithValue("@rank", img.Rank);
                cmdImg.Parameters.AddWithValue("@local_relative_path", img.LocalRelativePath);
                cmdImg.Parameters.AddWithValue("@original_url", (object?)img.OriginalUrl ?? DBNull.Value);
                cmdImg.Parameters.AddWithValue("@hex_code", (object?)img.HexCode ?? DBNull.Value);
                cmdImg.Parameters.AddWithValue("@file_size_bytes", img.FileSizeBytes);
                cmdImg.Parameters.AddWithValue("@width", img.Width);
                cmdImg.Parameters.AddWithValue("@height", img.Height);
                cmdImg.Parameters.AddWithValue("@downloaded_at_utc", img.DownloadedAtUtc.ToString("o"));

                await cmdImg.ExecuteNonQueryAsync(cancellationToken);
            }

            // Save variations
            foreach (var v in l.Variations)
            {
                using var cmdVar = conn.CreateCommand();
                cmdVar.Transaction = tx;
                cmdVar.CommandText = @"
                    INSERT INTO vault_variations (
                        id, session_id, listing_id, property_id, property_name, value_id, value_name,
                        price_difference, sku, stock_quantity, is_available
                    ) VALUES (
                        @id, @session_id, @listing_id, @property_id, @property_name, @value_id, @value_name,
                        @price_difference, @sku, @stock_quantity, @is_available
                    )
                    ON CONFLICT(id) DO UPDATE SET
                        price_difference = excluded.price_difference,
                        sku = excluded.sku,
                        stock_quantity = excluded.stock_quantity;
                ";

                cmdVar.Parameters.AddWithValue("@id", v.Id);
                cmdVar.Parameters.AddWithValue("@session_id", l.SessionId);
                cmdVar.Parameters.AddWithValue("@listing_id", l.ListingId);
                cmdVar.Parameters.AddWithValue("@property_id", v.PropertyId);
                cmdVar.Parameters.AddWithValue("@property_name", v.PropertyName);
                cmdVar.Parameters.AddWithValue("@value_id", v.ValueId);
                cmdVar.Parameters.AddWithValue("@value_name", v.ValueName);
                cmdVar.Parameters.AddWithValue("@price_difference", (double)v.PriceDifference);
                cmdVar.Parameters.AddWithValue("@sku", (object?)v.Sku ?? DBNull.Value);
                cmdVar.Parameters.AddWithValue("@stock_quantity", v.StockQuantity);
                cmdVar.Parameters.AddWithValue("@is_available", v.IsAvailable ? 1 : 0);

                await cmdVar.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task UpdateListingAsync(VaultListing listing, CancellationToken cancellationToken = default)
    {
        await SaveListingsAsync([listing], cancellationToken);
    }

    public async Task DeleteListingAsync(string sessionId, long listingId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = "DELETE FROM vault_variations WHERE session_id = @s AND listing_id = @l; DELETE FROM vault_images WHERE session_id = @s AND listing_id = @l; DELETE FROM vault_listings WHERE session_id = @s AND listing_id = @l;";
        cmd.Parameters.AddWithValue("@s", sessionId);
        cmd.Parameters.AddWithValue("@l", listingId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<int> GetListingCountBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM vault_listings WHERE session_id = @id;";
        cmd.Parameters.AddWithValue("@id", sessionId);

        var count = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count);
    }
}
