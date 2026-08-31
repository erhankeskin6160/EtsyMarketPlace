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
            );
            CREATE TABLE IF NOT EXISTS order_locked_rates (
                receipt_id INTEGER PRIMARY KEY,
                order_date TEXT NOT NULL,
                rate REAL NOT NULL,
                source TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS daily_historical_rates (
                date TEXT PRIMARY KEY,
                rate REAL NOT NULL,
                source TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            -- Clean up legacy unrealistic rates for 2026 (< 40 TL)
            DELETE FROM exchange_rates WHERE date >= '2026-01-01' AND rate < 40.0;
            DELETE FROM daily_historical_rates WHERE date >= '2026-01-01' AND rate < 40.0;
            DELETE FROM order_locked_rates WHERE order_date >= '2026-01-01' AND rate < 40.0;
        ";
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
        if (rate <= 0) return;

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

    public async Task<decimal?> GetLockedRateForOrderAsync(long receiptId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rate FROM order_locked_rates WHERE receipt_id = @receiptId;";
        cmd.Parameters.AddWithValue("@receiptId", receiptId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result != null && result != DBNull.Value)
        {
            return Convert.ToDecimal(result);
        }
        return null;
    }

    public async Task LockRateForOrderAsync(long receiptId, DateTime orderDate, decimal rate, string source, CancellationToken ct = default)
    {
        if (rate <= 0) return;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO order_locked_rates (receipt_id, order_date, rate, source, created_at)
            VALUES (@receiptId, @orderDate, @rate, @source, @createdAt)
            ON CONFLICT(receiptId) DO UPDATE SET rate = excluded.rate, source = excluded.source;";

        cmd.Parameters.AddWithValue("@receiptId", receiptId);
        cmd.Parameters.AddWithValue("@orderDate", orderDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@rate", (double)rate);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<decimal?> GetDailyHistoricalRateAsync(DateTime date, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rate FROM daily_historical_rates WHERE date = @date;";
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result != null && result != DBNull.Value)
        {
            return Convert.ToDecimal(result);
        }
        return null;
    }

    public async Task SaveDailyHistoricalRateAsync(DateTime date, decimal rate, string source, CancellationToken ct = default)
    {
        if (rate <= 0) return;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO daily_historical_rates (date, rate, source, created_at)
            VALUES (@date, @rate, @source, @createdAt)
            ON CONFLICT(date) DO UPDATE SET rate = excluded.rate, source = excluded.source;";

        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@rate", (double)rate);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Belirtilen tarihlerin tamamına ait kurları tek bir SQL sorgusu ile SQLite'dan çeker.
    /// </summary>
    public async Task<Dictionary<DateTime, decimal>> GetRatesForDatesAsync(IEnumerable<DateTime> dates, CancellationToken ct = default)
    {
        var result = new Dictionary<DateTime, decimal>();
        var dateList = dates.Select(d => d.ToString("yyyy-MM-dd")).Distinct().ToList();
        if (dateList.Count == 0) return result;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // SQLite parameter chunking
        const int chunkSize = 100;
        for (int i = 0; i < dateList.Count; i += chunkSize)
        {
            var chunk = dateList.Skip(i).Take(chunkSize).ToList();
            using var cmd = conn.CreateCommand();

            var paramNames = new List<string>();
            for (int p = 0; p < chunk.Count; p++)
            {
                string pName = $"@d{p}";
                paramNames.Add(pName);
                cmd.Parameters.AddWithValue(pName, chunk[p]);
            }

            cmd.CommandText = $@"
                SELECT date, rate FROM daily_historical_rates WHERE date IN ({string.Join(",", paramNames)})
                UNION
                SELECT date, rate FROM exchange_rates WHERE date IN ({string.Join(",", paramNames)});";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                string dStr = reader.GetString(0);
                decimal rVal = Convert.ToDecimal(reader.GetDouble(1));
                if (DateTime.TryParse(dStr, out var parsedDate) && rVal >= 35m)
                {
                    result[parsedDate.Date] = rVal;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Birden fazla tarihi kuru tek bir SQLite Transaction içinde toplu kaydeder (I/O performansını maksimize eder).
    /// </summary>
    public async Task BulkSaveDailyRatesAsync(Dictionary<DateTime, decimal> rates, string source, CancellationToken ct = default)
    {
        if (rates == null || rates.Count == 0) return;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var kvp in rates)
            {
                if (kvp.Value <= 0) continue;

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO daily_historical_rates (date, rate, source, created_at)
                    VALUES (@date, @rate, @source, @createdAt)
                    ON CONFLICT(date) DO UPDATE SET rate = excluded.rate, source = excluded.source;";

                cmd.Parameters.AddWithValue("@date", kvp.Key.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@rate", (double)kvp.Value);
                cmd.Parameters.AddWithValue("@source", source);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
        }
    }
}
