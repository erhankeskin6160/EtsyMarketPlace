namespace EtsyMarketPlace.Infrastructure.Orders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;
using Microsoft.Data.Sqlite;

/// <summary>
/// Sipariş teslimat adreslerini yerel SQLite veri tabanında kalıcı olarak saklayan ve yöneten repository.
/// </summary>
public sealed class SqliteOrderAddressRepository : IOrderAddressRepository
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly object _initLock = new();

    public SqliteOrderAddressRepository(string? databasePath = null)
    {
        string path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "order_addresses.db");

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS order_addresses (
                receipt_id INTEGER PRIMARY KEY,
                buyer_name TEXT NOT NULL DEFAULT '',
                buyer_email TEXT NOT NULL DEFAULT '',
                phone TEXT NOT NULL DEFAULT '',
                street_address TEXT NOT NULL DEFAULT '',
                second_address TEXT NOT NULL DEFAULT '',
                city TEXT NOT NULL DEFAULT '',
                state TEXT NOT NULL DEFAULT '',
                postal_code TEXT NOT NULL DEFAULT '',
                country_code TEXT NOT NULL DEFAULT '',
                country_name TEXT NOT NULL DEFAULT '',
                source TEXT NOT NULL DEFAULT '',
                updated_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_order_addresses_updated 
                ON order_addresses(updated_at_utc DESC);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        lock (_initLock)
        {
            _initialized = true;
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    public async Task<OrderAddressRecord?> GetByReceiptIdAsync(long receiptId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT receipt_id, buyer_name, buyer_email, phone, street_address, second_address,
                   city, state, postal_code, country_code, country_name, source, updated_at_utc
            FROM order_addresses
            WHERE receipt_id = @id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@id", receiptId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadRecord(reader);
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<long, OrderAddressRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var result = new Dictionary<long, OrderAddressRecord>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT receipt_id, buyer_name, buyer_email, phone, street_address, second_address,
                   city, state, postal_code, country_code, country_name, source, updated_at_utc
            FROM order_addresses;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader);
            result[record.ReceiptId] = record;
        }

        return result;
    }

    public async Task SaveAsync(OrderAddressRecord record, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO order_addresses (
                receipt_id, buyer_name, buyer_email, phone, street_address, second_address,
                city, state, postal_code, country_code, country_name, source, updated_at_utc
            ) VALUES (
                @receipt_id, @buyer_name, @buyer_email, @phone, @street_address, @second_address,
                @city, @state, @postal_code, @country_code, @country_name, @source, @updated_at_utc
            )
            ON CONFLICT(receipt_id) DO UPDATE SET
                buyer_name = CASE WHEN excluded.buyer_name <> '' THEN excluded.buyer_name ELSE order_addresses.buyer_name END,
                buyer_email = CASE WHEN excluded.buyer_email <> '' THEN excluded.buyer_email ELSE order_addresses.buyer_email END,
                phone = CASE WHEN excluded.phone <> '' THEN excluded.phone ELSE order_addresses.phone END,
                street_address = excluded.street_address,
                second_address = excluded.second_address,
                city = excluded.city,
                state = excluded.state,
                postal_code = excluded.postal_code,
                country_code = excluded.country_code,
                country_name = excluded.country_name,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            """;

        BindParameters(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveBatchAsync(IEnumerable<OrderAddressRecord> records, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var tx = conn.BeginTransaction();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO order_addresses (
                receipt_id, buyer_name, buyer_email, phone, street_address, second_address,
                city, state, postal_code, country_code, country_name, source, updated_at_utc
            ) VALUES (
                @receipt_id, @buyer_name, @buyer_email, @phone, @street_address, @second_address,
                @city, @state, @postal_code, @country_code, @country_name, @source, @updated_at_utc
            )
            ON CONFLICT(receipt_id) DO UPDATE SET
                buyer_name = CASE WHEN excluded.buyer_name <> '' THEN excluded.buyer_name ELSE order_addresses.buyer_name END,
                buyer_email = CASE WHEN excluded.buyer_email <> '' THEN excluded.buyer_email ELSE order_addresses.buyer_email END,
                phone = CASE WHEN excluded.phone <> '' THEN excluded.phone ELSE order_addresses.phone END,
                street_address = excluded.street_address,
                second_address = excluded.second_address,
                city = excluded.city,
                state = excluded.state,
                postal_code = excluded.postal_code,
                country_code = excluded.country_code,
                country_name = excluded.country_name,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            """;

        var pReceiptId = cmd.Parameters.Add("@receipt_id", SqliteType.Integer);
        var pBuyerName = cmd.Parameters.Add("@buyer_name", SqliteType.Text);
        var pBuyerEmail = cmd.Parameters.Add("@buyer_email", SqliteType.Text);
        var pPhone = cmd.Parameters.Add("@phone", SqliteType.Text);
        var pStreet = cmd.Parameters.Add("@street_address", SqliteType.Text);
        var pSecond = cmd.Parameters.Add("@second_address", SqliteType.Text);
        var pCity = cmd.Parameters.Add("@city", SqliteType.Text);
        var pState = cmd.Parameters.Add("@state", SqliteType.Text);
        var pPostal = cmd.Parameters.Add("@postal_code", SqliteType.Text);
        var pCountryCode = cmd.Parameters.Add("@country_code", SqliteType.Text);
        var pCountryName = cmd.Parameters.Add("@country_name", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pUpdated = cmd.Parameters.Add("@updated_at_utc", SqliteType.Text);

        foreach (var record in records)
        {
            if (record.ReceiptId <= 0 || string.IsNullOrWhiteSpace(record.StreetAddress)) continue;

            pReceiptId.Value = record.ReceiptId;
            pBuyerName.Value = record.BuyerName ?? string.Empty;
            pBuyerEmail.Value = record.BuyerEmail ?? string.Empty;
            pPhone.Value = record.Phone ?? string.Empty;
            pStreet.Value = record.StreetAddress ?? string.Empty;
            pSecond.Value = record.SecondAddress ?? string.Empty;
            pCity.Value = record.City ?? string.Empty;
            pState.Value = record.State ?? string.Empty;
            pPostal.Value = record.PostalCode ?? string.Empty;
            pCountryCode.Value = record.CountryCode ?? string.Empty;
            pCountryName.Value = record.CountryName ?? string.Empty;
            pSource.Value = record.Source ?? string.Empty;
            pUpdated.Value = record.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static void BindParameters(SqliteCommand cmd, OrderAddressRecord record)
    {
        cmd.Parameters.AddWithValue("@receipt_id", record.ReceiptId);
        cmd.Parameters.AddWithValue("@buyer_name", record.BuyerName ?? string.Empty);
        cmd.Parameters.AddWithValue("@buyer_email", record.BuyerEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("@phone", record.Phone ?? string.Empty);
        cmd.Parameters.AddWithValue("@street_address", record.StreetAddress ?? string.Empty);
        cmd.Parameters.AddWithValue("@second_address", record.SecondAddress ?? string.Empty);
        cmd.Parameters.AddWithValue("@city", record.City ?? string.Empty);
        cmd.Parameters.AddWithValue("@state", record.State ?? string.Empty);
        cmd.Parameters.AddWithValue("@postal_code", record.PostalCode ?? string.Empty);
        cmd.Parameters.AddWithValue("@country_code", record.CountryCode ?? string.Empty);
        cmd.Parameters.AddWithValue("@country_name", record.CountryName ?? string.Empty);
        cmd.Parameters.AddWithValue("@source", record.Source ?? string.Empty);
        cmd.Parameters.AddWithValue("@updated_at_utc", record.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static OrderAddressRecord ReadRecord(SqliteDataReader reader)
    {
        long receiptId = reader.GetInt64(0);
        string name = reader.GetString(1);
        string email = reader.GetString(2);
        string phone = reader.GetString(3);
        string street = reader.GetString(4);
        string second = reader.GetString(5);
        string city = reader.GetString(6);
        string state = reader.GetString(7);
        string postal = reader.GetString(8);
        string countryCode = reader.GetString(9);
        string countryName = reader.GetString(10);
        string source = reader.GetString(11);
        string dateStr = reader.GetString(12);

        DateTime date = DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTime.UtcNow;

        return new OrderAddressRecord(
            receiptId,
            name,
            email,
            phone,
            street,
            second,
            city,
            state,
            postal,
            countryCode,
            countryName,
            source,
            date);
    }
}
