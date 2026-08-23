namespace SimilarProductsWinForms.Services;

using Microsoft.Data.Sqlite;
using SimilarProductsWinForms.Models;

internal sealed class SqliteProductCostRepository
{
    private readonly string _connectionString;

    public SqliteProductCostRepository(string? connectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _connectionString = connectionString;
        }
        else
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            var dbPath = Path.Combine(folder, "product_costs.db");
            _connectionString = $"Data Source={dbPath}";
        }

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS product_costs (
                listing_id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                unit_cost REAL NOT NULL DEFAULT 0,
                unit_shipping_cost REAL NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task<List<ProductCostEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<ProductCostEntry>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT listing_id, title, unit_cost, unit_shipping_cost, updated_at FROM product_costs ORDER BY title;";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var listingId = reader.GetString(0);
            var title = reader.GetString(1);
            var unitCost = Convert.ToDecimal(reader.GetDouble(2));
            var unitShipping = Convert.ToDecimal(reader.GetDouble(3));
            var updatedAt = DateTimeOffset.TryParse(reader.GetString(4), out var dt) ? dt : DateTimeOffset.UtcNow;

            list.Add(new ProductCostEntry(listingId, title, unitCost, unitShipping, updatedAt));
        }

        return list;
    }

    public async Task SaveAsync(ProductCostEntry entry, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_costs (listing_id, title, unit_cost, unit_shipping_cost, updated_at)
            VALUES (@id, @title, @cost, @shipping, @updatedAt)
            ON CONFLICT(listing_id) DO UPDATE SET
                title = excluded.title,
                unit_cost = excluded.unit_cost,
                unit_shipping_cost = excluded.unit_shipping_cost,
                updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("@id", entry.ListingId);
        cmd.Parameters.AddWithValue("@title", entry.Title ?? "");
        cmd.Parameters.AddWithValue("@cost", (double)entry.UnitCost);
        cmd.Parameters.AddWithValue("@shipping", (double)entry.UnitShippingCost);
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string listingId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM product_costs WHERE listing_id = @id;";
        cmd.Parameters.AddWithValue("@id", listingId);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
