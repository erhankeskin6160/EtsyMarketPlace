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
                unit_packaging_cost REAL NOT NULL DEFAULT 0,
                invoice_file_path TEXT,
                updated_at TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
        
        // Migration to add column to existing databases
        try
        {
            cmd.CommandText = "ALTER TABLE product_costs ADD COLUMN unit_packaging_cost REAL NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column likely already exists
        }

        try
        {
            cmd.CommandText = "ALTER TABLE product_costs ADD COLUMN invoice_file_path TEXT;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column likely already exists
        }
    }

    public async Task<List<ProductCostEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<ProductCostEntry>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT listing_id, title, unit_cost, unit_shipping_cost, unit_packaging_cost, updated_at, invoice_file_path FROM product_costs ORDER BY title;";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var listingId = reader.GetString(0);
            var title = reader.GetString(1);
            var unitCost = Convert.ToDecimal(reader.GetDouble(2));
            var unitShipping = Convert.ToDecimal(reader.GetDouble(3));
            var unitPackaging = Convert.ToDecimal(reader.GetDouble(4));
            var updatedAt = DateTimeOffset.TryParse(reader.GetString(5), out var dt) ? dt : DateTimeOffset.UtcNow;
            var invoicePath = reader.IsDBNull(6) ? null : reader.GetString(6);

            list.Add(new ProductCostEntry(listingId, title, unitCost, unitShipping, unitPackaging, updatedAt, invoicePath));
        }

        return list;
    }

    public async Task<ProductCostEntry?> GetByIdAsync(string listingId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT title, unit_cost, unit_shipping_cost, unit_packaging_cost, updated_at, invoice_file_path FROM product_costs WHERE listing_id = @id;";
        cmd.Parameters.AddWithValue("@id", listingId);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var title = reader.GetString(0);
            var unitCost = Convert.ToDecimal(reader.GetDouble(1));
            var unitShipping = Convert.ToDecimal(reader.GetDouble(2));
            var unitPackaging = Convert.ToDecimal(reader.GetDouble(3));
            var updatedAt = DateTimeOffset.TryParse(reader.GetString(4), out var dt) ? dt : DateTimeOffset.UtcNow;
            var invoicePath = reader.IsDBNull(5) ? null : reader.GetString(5);
            
            return new ProductCostEntry(listingId, title, unitCost, unitShipping, unitPackaging, updatedAt, invoicePath);
        }

        return null;
    }

    public async Task SaveAsync(ProductCostEntry entry, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO product_costs (listing_id, title, unit_cost, unit_shipping_cost, unit_packaging_cost, invoice_file_path, updated_at)
            VALUES (@id, @title, @cost, @shipping, @packaging, @invoice, @updatedAt)
            ON CONFLICT(listing_id) DO UPDATE SET
                title = excluded.title,
                unit_cost = excluded.unit_cost,
                unit_shipping_cost = excluded.unit_shipping_cost,
                unit_packaging_cost = excluded.unit_packaging_cost,
                invoice_file_path = excluded.invoice_file_path,
                updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("@id", entry.ListingId);
        cmd.Parameters.AddWithValue("@title", entry.Title ?? "");
        cmd.Parameters.AddWithValue("@cost", (double)entry.UnitCost);
        cmd.Parameters.AddWithValue("@shipping", (double)entry.UnitShippingCost);
        cmd.Parameters.AddWithValue("@packaging", (double)entry.UnitPackagingCost);
        cmd.Parameters.AddWithValue("@invoice", (object?)entry.InvoiceFilePath ?? DBNull.Value);
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
