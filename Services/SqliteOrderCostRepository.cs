namespace SimilarProductsWinForms.Services;

using Microsoft.Data.Sqlite;
using SimilarProductsWinForms.Models;

internal sealed class SqliteOrderCostRepository
{
    private readonly string _connectionString;

    public SqliteOrderCostRepository(string? connectionString = null)
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
            CREATE TABLE IF NOT EXISTS order_costs (
                receipt_id TEXT PRIMARY KEY,
                listing_id TEXT NOT NULL,
                title TEXT NOT NULL,
                unit_cost REAL NOT NULL DEFAULT 0,
                shipping_cost REAL NOT NULL DEFAULT 0,
                packaging_cost REAL NOT NULL DEFAULT 0,
                invoice_file_path TEXT,
                buyer_user_id INTEGER DEFAULT 0,
                buyer_name TEXT,
                buyer_email TEXT,
                notes TEXT,
                updated_at TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        try
        {
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_order_costs_buyer ON order_costs(buyer_name, buyer_user_id);";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Ignore if index creation fails
        }
    }

    public async Task<List<OrderCostEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<OrderCostEntry>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT receipt_id, listing_id, title, unit_cost, shipping_cost, packaging_cost, 
                   updated_at, invoice_file_path, buyer_user_id, buyer_name, buyer_email, notes 
            FROM order_costs ORDER BY updated_at DESC;";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var receiptId = reader.GetString(0);
            var listingId = reader.GetString(1);
            var title = reader.GetString(2);
            var unitCost = Convert.ToDecimal(reader.GetDouble(3));
            var unitShipping = Convert.ToDecimal(reader.GetDouble(4));
            var unitPackaging = Convert.ToDecimal(reader.GetDouble(5));
            var updatedAt = DateTimeOffset.TryParse(reader.GetString(6), out var dt) ? dt : DateTimeOffset.UtcNow;
            var invoicePath = reader.IsDBNull(7) ? null : reader.GetString(7);
            var buyerUserId = reader.IsDBNull(8) ? 0L : reader.GetInt64(8);
            var buyerName = reader.IsDBNull(9) ? "" : reader.GetString(9);
            var buyerEmail = reader.IsDBNull(10) ? "" : reader.GetString(10);
            var notes = reader.IsDBNull(11) ? null : reader.GetString(11);

            list.Add(new OrderCostEntry(
                receiptId, listingId, title, unitCost, unitShipping, unitPackaging,
                updatedAt, invoicePath, buyerUserId, buyerName, buyerEmail, notes));
        }

        return list;
    }

    public async Task<OrderCostEntry?> GetByReceiptIdAsync(string receiptId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT listing_id, title, unit_cost, shipping_cost, packaging_cost, 
                   updated_at, invoice_file_path, buyer_user_id, buyer_name, buyer_email, notes 
            FROM order_costs WHERE receipt_id = @id;";
        cmd.Parameters.AddWithValue("@id", receiptId);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var listingId = reader.GetString(0);
            var title = reader.GetString(1);
            var unitCost = Convert.ToDecimal(reader.GetDouble(2));
            var unitShipping = Convert.ToDecimal(reader.GetDouble(3));
            var unitPackaging = Convert.ToDecimal(reader.GetDouble(4));
            var updatedAt = DateTimeOffset.TryParse(reader.GetString(5), out var dt) ? dt : DateTimeOffset.UtcNow;
            var invoicePath = reader.IsDBNull(6) ? null : reader.GetString(6);
            var buyerUserId = reader.IsDBNull(7) ? 0L : reader.GetInt64(7);
            var buyerName = reader.IsDBNull(8) ? "" : reader.GetString(8);
            var buyerEmail = reader.IsDBNull(9) ? "" : reader.GetString(9);
            var notes = reader.IsDBNull(10) ? null : reader.GetString(10);

            return new OrderCostEntry(
                receiptId, listingId, title, unitCost, unitShipping, unitPackaging,
                updatedAt, invoicePath, buyerUserId, buyerName, buyerEmail, notes);
        }

        return null;
    }

    public async Task SaveAsync(OrderCostEntry entry, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO order_costs (receipt_id, listing_id, title, unit_cost, shipping_cost, packaging_cost, invoice_file_path, buyer_user_id, buyer_name, buyer_email, notes, updated_at)
            VALUES (@receiptId, @listingId, @title, @cost, @shipping, @packaging, @invoice, @buyerUserId, @buyerName, @buyerEmail, @notes, @updatedAt)
            ON CONFLICT(receipt_id) DO UPDATE SET
                listing_id = excluded.listing_id,
                title = excluded.title,
                unit_cost = excluded.unit_cost,
                shipping_cost = excluded.shipping_cost,
                packaging_cost = excluded.packaging_cost,
                invoice_file_path = excluded.invoice_file_path,
                buyer_user_id = excluded.buyer_user_id,
                buyer_name = excluded.buyer_name,
                buyer_email = excluded.buyer_email,
                notes = excluded.notes,
                updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("@receiptId", entry.ReceiptId);
        cmd.Parameters.AddWithValue("@listingId", entry.ListingId ?? "");
        cmd.Parameters.AddWithValue("@title", entry.Title ?? "");
        cmd.Parameters.AddWithValue("@cost", (double)entry.UnitCost);
        cmd.Parameters.AddWithValue("@shipping", (double)entry.UnitShippingCost);
        cmd.Parameters.AddWithValue("@packaging", (double)entry.UnitPackagingCost);
        cmd.Parameters.AddWithValue("@invoice", (object?)entry.InvoiceFilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buyerUserId", entry.BuyerUserId);
        cmd.Parameters.AddWithValue("@buyerName", (object?)entry.BuyerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@buyerEmail", (object?)entry.BuyerEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", (object?)entry.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveInvoicePathAsync(string receiptId, string invoicePath, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO order_costs (receipt_id, listing_id, title, unit_cost, shipping_cost, packaging_cost, invoice_file_path, updated_at)
            VALUES (@receiptId, '', '', 0, 0, 0, @invoice, @updatedAt)
            ON CONFLICT(receipt_id) DO UPDATE SET
                invoice_file_path = excluded.invoice_file_path,
                updated_at = excluded.updated_at;";
        cmd.Parameters.AddWithValue("@receiptId", receiptId);
        cmd.Parameters.AddWithValue("@invoice", invoicePath);
        cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> BulkApplyCostToListingOrdersAsync(string listingId, decimal unitCost, decimal unitShippingCost, decimal unitPackagingCost, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(listingId) || listingId == "0") return 0;
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE order_costs 
            SET unit_cost = @cost, shipping_cost = @shipping, packaging_cost = @packaging, updated_at = @updatedAt
            WHERE listing_id = @listingId AND (unit_cost = 0 OR unit_cost IS NULL);";
        cmd.Parameters.AddWithValue("@cost", (double)unitCost);
        cmd.Parameters.AddWithValue("@shipping", (double)unitShippingCost);
        cmd.Parameters.AddWithValue("@packaging", (double)unitPackagingCost);
        cmd.Parameters.AddWithValue("@listingId", listingId);
        cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("o"));

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string receiptId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM order_costs WHERE receipt_id = @id;";
        cmd.Parameters.AddWithValue("@id", receiptId);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
