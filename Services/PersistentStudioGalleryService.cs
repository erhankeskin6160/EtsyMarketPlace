namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

public sealed record StudioGalleryItem(
    long Id,
    DateTime CreatedAt,
    string ImagePath,
    string Prompt,
    string Engine,
    string ProductTitle,
    long? ListingId);

/// <summary>
/// Persistent SQLite and local storage service for generated AI mockups.
/// </summary>
internal sealed class PersistentStudioGalleryService
{
    private static readonly string StorageDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "StudioGallery");

    private static readonly string DbPath = Path.Combine(StorageDir, "gallery.db");
    private static readonly string ConnectionString = new SqliteConnectionStringBuilder
    {
        DataSource = DbPath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    private static bool _initialized;

    public static async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        try
        {
            if (!Directory.Exists(StorageDir))
            {
                Directory.CreateDirectory(StorageDir);
            }

            await using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS studio_gallery (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    created_at TEXT NOT NULL,
                    image_path TEXT NOT NULL,
                    prompt TEXT NOT NULL,
                    engine TEXT NOT NULL,
                    product_title TEXT NOT NULL,
                    listing_id INTEGER NULL
                );
                CREATE INDEX IF NOT EXISTS idx_gallery_created ON studio_gallery(created_at DESC);
            """;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        catch { }
    }

    public static async Task<StudioGalleryItem?> SaveItemAsync(
        Bitmap bitmap,
        string prompt,
        string engine,
        string productTitle,
        long? listingId = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            string fileName = $"mockup_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N[..6]}.png";
            string fullPath = Path.Combine(StorageDir, fileName);

            bitmap.Save(fullPath, ImageFormat.Png);

            var now = DateTime.UtcNow;
            await using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO studio_gallery (created_at, image_path, prompt, engine, product_title, listing_id)
                VALUES (@created, @path, @prompt, @engine, @title, @listing_id);
                SELECT last_insert_rowid();
            """;
            cmd.Parameters.AddWithValue("@created", now.ToString("o"));
            cmd.Parameters.AddWithValue("@path", fullPath);
            cmd.Parameters.AddWithValue("@prompt", prompt ?? "");
            cmd.Parameters.AddWithValue("@engine", engine ?? "");
            cmd.Parameters.AddWithValue("@title", productTitle ?? "");
            cmd.Parameters.AddWithValue("@listing_id", (object?)listingId ?? DBNull.Value);

            var newId = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
            return new StudioGalleryItem(newId, now, fullPath, prompt, engine, productTitle, listingId);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<List<StudioGalleryItem>> GetRecentItemsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var list = new List<StudioGalleryItem>();

        try
        {
            await using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, created_at, image_path, prompt, engine, product_title, listing_id
                FROM studio_gallery
                ORDER BY id DESC
                LIMIT @limit;
            """;
            cmd.Parameters.AddWithValue("@limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt64(0);
                var createdStr = reader.GetString(1);
                var path = reader.GetString(2);
                var prompt = reader.GetString(3);
                var engine = reader.GetString(4);
                var title = reader.GetString(5);
                long? listingId = reader.IsDBNull(6) ? null : reader.GetInt64(6);

                if (File.Exists(path))
                {
                    DateTime.TryParse(createdStr, out var createdAt);
                    list.Add(new StudioGalleryItem(id, createdAt, path, prompt, engine, title, listingId));
                }
            }
        }
        catch { }

        return list;
    }
}
