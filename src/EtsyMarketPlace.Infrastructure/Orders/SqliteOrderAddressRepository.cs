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
                updated_at_utc TEXT NOT NULL,
                ioss_number TEXT NOT NULL DEFAULT '',
                product_cost_usd REAL NOT NULL DEFAULT 0,
                shipping_cost_usd REAL NOT NULL DEFAULT 0,
                weight_kg REAL NOT NULL DEFAULT 0,
                width_cm REAL NOT NULL DEFAULT 0,
                length_cm REAL NOT NULL DEFAULT 0,
                height_cm REAL NOT NULL DEFAULT 0,
                gtip_code TEXT NOT NULL DEFAULT '',
                selected_carrier TEXT NOT NULL DEFAULT '',
                carrier_service_name TEXT NOT NULL DEFAULT '',
                tracking_code TEXT NOT NULL DEFAULT '',
                label_url TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS ix_order_addresses_updated 
                ON order_addresses(updated_at_utc DESC);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        string[] alterStatements =
        [
            "ALTER TABLE order_addresses ADD COLUMN ioss_number TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE order_addresses ADD COLUMN product_cost_usd REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN shipping_cost_usd REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN weight_kg REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN width_cm REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN length_cm REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN height_cm REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE order_addresses ADD COLUMN gtip_code TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE order_addresses ADD COLUMN selected_carrier TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE order_addresses ADD COLUMN carrier_service_name TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE order_addresses ADD COLUMN tracking_code TEXT NOT NULL DEFAULT '';",
            "ALTER TABLE order_addresses ADD COLUMN label_url TEXT NOT NULL DEFAULT '';"
        ];

        foreach (string sql in alterStatements)
        {
            try
            {
                await using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = sql;
                await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Column already exists
            }
        }

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
                   city, state, postal_code, country_code, country_name, source, updated_at_utc,
                   ioss_number, product_cost_usd, shipping_cost_usd, weight_kg, width_cm, length_cm, height_cm,
                   gtip_code, selected_carrier, carrier_service_name, tracking_code, label_url
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
                   city, state, postal_code, country_code, country_name, source, updated_at_utc,
                   ioss_number, product_cost_usd, shipping_cost_usd, weight_kg, width_cm, length_cm, height_cm,
                   gtip_code, selected_carrier, carrier_service_name, tracking_code, label_url
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
                city, state, postal_code, country_code, country_name, source, updated_at_utc,
                ioss_number, product_cost_usd, shipping_cost_usd, weight_kg, width_cm, length_cm, height_cm,
                gtip_code, selected_carrier, carrier_service_name, tracking_code, label_url
            ) VALUES (
                @receipt_id, @buyer_name, @buyer_email, @phone, @street_address, @second_address,
                @city, @state, @postal_code, @country_code, @country_name, @source, @updated_at_utc,
                @ioss_number, @product_cost_usd, @shipping_cost_usd, @weight_kg, @width_cm, @length_cm, @height_cm,
                @gtip_code, @selected_carrier, @carrier_service_name, @tracking_code, @label_url
            )
            ON CONFLICT(receipt_id) DO UPDATE SET
                buyer_name = CASE WHEN excluded.buyer_name <> '' THEN excluded.buyer_name ELSE order_addresses.buyer_name END,
                buyer_email = CASE WHEN excluded.buyer_email <> '' THEN excluded.buyer_email ELSE order_addresses.buyer_email END,
                phone = CASE WHEN excluded.phone <> '' THEN excluded.phone ELSE order_addresses.phone END,
                street_address = CASE WHEN excluded.street_address <> '' THEN excluded.street_address ELSE order_addresses.street_address END,
                second_address = CASE WHEN excluded.second_address <> '' THEN excluded.second_address ELSE order_addresses.second_address END,
                city = CASE WHEN excluded.city <> '' THEN excluded.city ELSE order_addresses.city END,
                state = CASE WHEN excluded.state <> '' THEN excluded.state ELSE order_addresses.state END,
                postal_code = CASE WHEN excluded.postal_code <> '' THEN excluded.postal_code ELSE order_addresses.postal_code END,
                country_code = CASE WHEN excluded.country_code <> '' THEN excluded.country_code ELSE order_addresses.country_code END,
                country_name = CASE WHEN excluded.country_name <> '' THEN excluded.country_name ELSE order_addresses.country_name END,
                source = CASE WHEN excluded.source <> '' THEN excluded.source ELSE order_addresses.source END,
                updated_at_utc = excluded.updated_at_utc,
                ioss_number = CASE WHEN excluded.ioss_number <> '' THEN excluded.ioss_number ELSE order_addresses.ioss_number END,
                product_cost_usd = CASE WHEN excluded.product_cost_usd > 0 THEN excluded.product_cost_usd ELSE order_addresses.product_cost_usd END,
                shipping_cost_usd = CASE WHEN excluded.shipping_cost_usd > 0 THEN excluded.shipping_cost_usd ELSE order_addresses.shipping_cost_usd END,
                weight_kg = CASE WHEN excluded.weight_kg > 0 THEN excluded.weight_kg ELSE order_addresses.weight_kg END,
                width_cm = CASE WHEN excluded.width_cm > 0 THEN excluded.width_cm ELSE order_addresses.width_cm END,
                length_cm = CASE WHEN excluded.length_cm > 0 THEN excluded.length_cm ELSE order_addresses.length_cm END,
                height_cm = CASE WHEN excluded.height_cm > 0 THEN excluded.height_cm ELSE order_addresses.height_cm END,
                gtip_code = CASE WHEN excluded.gtip_code <> '' THEN excluded.gtip_code ELSE order_addresses.gtip_code END,
                selected_carrier = CASE WHEN excluded.selected_carrier <> '' THEN excluded.selected_carrier ELSE order_addresses.selected_carrier END,
                carrier_service_name = CASE WHEN excluded.carrier_service_name <> '' THEN excluded.carrier_service_name ELSE order_addresses.carrier_service_name END,
                tracking_code = CASE WHEN excluded.tracking_code <> '' THEN excluded.tracking_code ELSE order_addresses.tracking_code END,
                label_url = CASE WHEN excluded.label_url <> '' THEN excluded.label_url ELSE order_addresses.label_url END;
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
        cmd.Parameters.AddWithValue("@ioss_number", record.IossNumber ?? string.Empty);
        cmd.Parameters.AddWithValue("@product_cost_usd", (double)record.ProductCostUsd);
        cmd.Parameters.AddWithValue("@shipping_cost_usd", (double)record.ShippingCostUsd);
        cmd.Parameters.AddWithValue("@weight_kg", record.WeightKg);
        cmd.Parameters.AddWithValue("@width_cm", record.WidthCm);
        cmd.Parameters.AddWithValue("@length_cm", record.LengthCm);
        cmd.Parameters.AddWithValue("@height_cm", record.HeightCm);
        cmd.Parameters.AddWithValue("@gtip_code", record.GtipCode ?? string.Empty);
        cmd.Parameters.AddWithValue("@selected_carrier", record.SelectedCarrier ?? string.Empty);
        cmd.Parameters.AddWithValue("@carrier_service_name", record.CarrierServiceName ?? string.Empty);
        cmd.Parameters.AddWithValue("@tracking_code", record.TrackingCode ?? string.Empty);
        cmd.Parameters.AddWithValue("@label_url", record.LabelUrl ?? string.Empty);
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
            date)
        {
            IossNumber = reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : string.Empty,
            ProductCostUsd = reader.FieldCount > 14 && !reader.IsDBNull(14) ? (decimal)reader.GetDouble(14) : 0m,
            ShippingCostUsd = reader.FieldCount > 15 && !reader.IsDBNull(15) ? (decimal)reader.GetDouble(15) : 0m,
            WeightKg = reader.FieldCount > 16 && !reader.IsDBNull(16) ? reader.GetDouble(16) : 0.0,
            WidthCm = reader.FieldCount > 17 && !reader.IsDBNull(17) ? reader.GetDouble(17) : 0.0,
            LengthCm = reader.FieldCount > 18 && !reader.IsDBNull(18) ? reader.GetDouble(18) : 0.0,
            HeightCm = reader.FieldCount > 19 && !reader.IsDBNull(19) ? reader.GetDouble(19) : 0.0,
            GtipCode = reader.FieldCount > 20 && !reader.IsDBNull(20) ? reader.GetString(20) : string.Empty,
            SelectedCarrier = reader.FieldCount > 21 && !reader.IsDBNull(21) ? reader.GetString(21) : string.Empty,
            CarrierServiceName = reader.FieldCount > 22 && !reader.IsDBNull(22) ? reader.GetString(22) : string.Empty,
            TrackingCode = reader.FieldCount > 23 && !reader.IsDBNull(23) ? reader.GetString(23) : string.Empty,
            LabelUrl = reader.FieldCount > 24 && !reader.IsDBNull(24) ? reader.GetString(24) : string.Empty
        };
    }
}
