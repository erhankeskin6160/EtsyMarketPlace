namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

internal sealed class SqliteExchangeRateRepository
{
    private readonly string _connectionString;

    public SqliteExchangeRateRepository(string? connectionString = null)
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
            var dbPath = Path.Combine(folder, "exchange_rates.db");
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
            CREATE TABLE IF NOT EXISTS exchange_rates (
                date TEXT PRIMARY KEY,
                rate REAL NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task<decimal?> GetRateAsync(DateTime date, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rate FROM exchange_rates WHERE date = @date;";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result != null && result != DBNull.Value)
        {
            return Convert.ToDecimal(result);
        }
        return null;
    }

    public async Task SaveRateAsync(DateTime date, decimal rate, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO exchange_rates (date, rate)
            VALUES (@date, @rate)
            ON CONFLICT(date) DO UPDATE SET rate = excluded.rate;";
        
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@rate", (double)rate);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
