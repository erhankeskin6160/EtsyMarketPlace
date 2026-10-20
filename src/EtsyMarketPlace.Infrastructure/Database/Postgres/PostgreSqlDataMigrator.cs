using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DomainUser = EtsyMarketPlace.Domain.Entities.AppUser;

namespace EtsyMarketPlace.Infrastructure.Database.Postgres;

public record PostgreSqlMigrationResult(
    int UsersSeeded,
    int ShopConnectionsSeeded,
    int BankPayoutsMigrated,
    int FinancialTransactionsMigrated,
    int SyncStatesMigrated,
    int OrderAddressesMigrated,
    string Message);

/// <summary>
/// SQLite'taki mevcut verileri deduplicate ederek PostgreSQL'e aktaran ve başlangıç kullanıcılarını tohumlayan servis.
/// </summary>
public sealed class PostgreSqlDataMigrator
{
    private readonly EtsyMarketPlaceDbContext _db;
    private readonly EtsyIntegrationOptions _options;
    private readonly ILogger<PostgreSqlDataMigrator> _logger;

    public PostgreSqlDataMigrator(
        EtsyMarketPlaceDbContext db,
        IOptions<EtsyIntegrationOptions> options,
        ILogger<PostgreSqlDataMigrator> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _options = options?.Value ?? new EtsyIntegrationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PostgreSqlMigrationResult> MigrateAndSeedAsync(CancellationToken cancellationToken = default)
    {
        int usersSeeded = 0;
        int shopsSeeded = 0;
        int payoutsMigrated = 0;
        int transactionsMigrated = 0;
        int syncStatesMigrated = 0;
        int orderAddressesMigrated = 0;

        try
        {
            // 1. Seed default AppUser if empty
            DomainUser? adminUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == "admin", cancellationToken);
            if (adminUser is null)
            {
                var (hash, salt) = PasswordHasher.HashPassword("Admin123!");
                adminUser = new DomainUser
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Username = "admin",
                    Email = "admin@etsymarketplace.local",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = "SuperAdmin",
                    AssignedShopIds = "[\"53236321\"]",
                    MonthlyAiTokenQuota = 1000000,
                    UsedAiTokens = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Users.Add(adminUser);
                await _db.SaveChangesAsync(cancellationToken);
                usersSeeded++;
                _logger.LogInformation("PostgreSQL: Varsayılan admin kullanıcısı tohumlandı.");
            }

            // 2. Seed default ShopConnection if empty
            var defaultShop = await _db.ShopConnections.FirstOrDefaultAsync(s => s.ShopId == "53236321", cancellationToken);
            if (defaultShop is null)
            {
                defaultShop = new ShopConnection
                {
                    ShopId = "53236321",
                    ShopName = "3dAntDesignStore",
                    UserId = adminUser.Id,
                    IsActive = true,
                    LastSynchronizedAt = DateTime.UtcNow
                };
                _db.ShopConnections.Add(defaultShop);
                await _db.SaveChangesAsync(cancellationToken);
                shopsSeeded++;
                _logger.LogInformation("PostgreSQL: Varsayılan 53236321 (3dAntDesignStore) mağaza bağlantısı tohumlandı.");
            }

            // 3. Migrate from SQLite if database file exists
            var sqlitePath = _options.ResolveDatabasePath();
            if (File.Exists(sqlitePath))
            {
                _logger.LogInformation("PostgreSQL Migrator: SQLite veritabanı bulundu ({Path}). Aktarım başlatılıyor...", sqlitePath);
                var csb = new SqliteConnectionStringBuilder
                {
                    DataSource = sqlitePath,
                    Mode = SqliteOpenMode.ReadOnly
                };

                await using var connection = new SqliteConnection(csb.ToString());
                await connection.OpenAsync(cancellationToken);

                // 3.1. Migrate & Deduplicate BankPayouts
                var rawPayouts = new List<BankPayout>();
                try
                {
                    await using var payoutCmd = connection.CreateCommand();
                    payoutCmd.CommandText = "SELECT shop_id, reference_id, occurred_at, amount, currency, exchange_rate_to_try, status, description FROM bank_payouts;";
                    await using var reader = await payoutCmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var occurredAtStr = reader.GetString(2);
                        var occurredAt = DateTime.TryParse(occurredAtStr, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;
                        rawPayouts.Add(new BankPayout
                        {
                            ShopId = reader.GetString(0),
                            ReferenceId = reader.GetString(1),
                            OccurredAt = occurredAt,
                            Amount = reader.GetDecimal(3),
                            Currency = reader.IsDBNull(4) ? "USD" : reader.GetString(4),
                            ExchangeRateToTry = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                            Status = reader.IsDBNull(6) ? "completed" : reader.GetString(6),
                            Description = reader.IsDBNull(7) ? "Etsy payout" : reader.GetString(7)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite bank_payouts tablosu okunamadı: {Msg}", ex.Message);
                }

                // Smart Deduplication for BankPayouts:
                var dedupedPayouts = new List<BankPayout>();
                foreach (var p in rawPayouts.OrderByDescending(x => x.OccurredAt))
                {
                    var existingIdx = dedupedPayouts.FindIndex(ex =>
                        ex.ShopId == p.ShopId &&
                        Math.Abs((ex.OccurredAt - p.OccurredAt).TotalMinutes) < 2.0 &&
                        Math.Abs(ex.Amount - p.Amount) < 0.25m);

                    if (existingIdx < 0)
                    {
                        dedupedPayouts.Add(p);
                    }
                    else if (p.ReferenceId.Length >= dedupedPayouts[existingIdx].ReferenceId.Length && p.ReferenceId.StartsWith("2"))
                    {
                        dedupedPayouts[existingIdx] = p;
                    }
                }

                foreach (var p in dedupedPayouts)
                {
                    var exists = await _db.BankPayouts.AnyAsync(x => x.ShopId == p.ShopId && x.ReferenceId == p.ReferenceId, cancellationToken);
                    if (!exists)
                    {
                        _db.BankPayouts.Add(p);
                        payoutsMigrated++;
                    }
                }

                // 3.2. Migrate FinancialTransactions
                try
                {
                    await using var txCmd = connection.CreateCommand();
                    txCmd.CommandText = "SELECT shop_id, reference_id, occurred_at, gross_sales, platform_fees, internal_ads_cost, external_ads_cost, product_cost, shipping_cost, refunds, currency FROM financial_transactions;";
                    await using var txReader = await txCmd.ExecuteReaderAsync(cancellationToken);
                    while (await txReader.ReadAsync(cancellationToken))
                    {
                        var sId = txReader.GetString(0);
                        var rId = txReader.GetString(1);
                        var occurredAtStr = txReader.GetString(2);
                        var occurredAt = DateTime.TryParse(occurredAtStr, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;

                        var exists = await _db.FinancialTransactions.AnyAsync(x => x.ShopId == sId && x.ReferenceId == rId, cancellationToken);
                        if (!exists)
                        {
                            _db.FinancialTransactions.Add(new FinancialTransaction
                            {
                                ShopId = sId,
                                ReferenceId = rId,
                                OccurredAt = occurredAt,
                                GrossSales = txReader.GetDecimal(3),
                                PlatformFees = txReader.GetDecimal(4),
                                InternalAdsCost = txReader.GetDecimal(5),
                                ExternalAdsCost = txReader.GetDecimal(6),
                                ProductCost = txReader.GetDecimal(7),
                                ShippingCost = txReader.GetDecimal(8),
                                Refunds = txReader.GetDecimal(9),
                                Currency = txReader.IsDBNull(10) ? "USD" : txReader.GetString(10)
                            });
                            transactionsMigrated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite financial_transactions tablosu okunamadı: {Msg}", ex.Message);
                }

                // 3.3. Migrate SyncStates
                try
                {
                    await using var syncCmd = connection.CreateCommand();
                    syncCmd.CommandText = "SELECT shop_id, data_type, last_successful_sync_at, last_cursor_at, last_error FROM sync_states;";
                    await using var syncReader = await syncCmd.ExecuteReaderAsync(cancellationToken);
                    while (await syncReader.ReadAsync(cancellationToken))
                    {
                        var sId = syncReader.GetString(0);
                        var dType = syncReader.GetString(1);

                        var exists = await _db.SyncStates.AnyAsync(x => x.ShopId == sId && x.DataType == dType, cancellationToken);
                        if (!exists)
                        {
                            DateTime? lastSuccess = null;
                            if (!syncReader.IsDBNull(2) && DateTime.TryParse(syncReader.GetString(2), out var sDt))
                                lastSuccess = sDt.ToUniversalTime();

                            DateTime? lastCursor = null;
                            if (!syncReader.IsDBNull(3) && DateTime.TryParse(syncReader.GetString(3), out var cDt))
                                lastCursor = cDt.ToUniversalTime();

                            _db.SyncStates.Add(new SyncState
                            {
                                ShopId = sId,
                                DataType = dType,
                                LastSuccessfulSyncAt = lastSuccess,
                                LastCursorAt = lastCursor,
                                LastError = syncReader.IsDBNull(4) ? null : syncReader.GetString(4)
                            });
                            syncStatesMigrated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite sync_states tablosu okunamadı: {Msg}", ex.Message);
                }

                if (payoutsMigrated > 0 || transactionsMigrated > 0 || syncStatesMigrated > 0)
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("PostgreSQL: {Payouts} ödeme, {Tx} işlem, {Sync} senkron durumu aktarıldı.", payoutsMigrated, transactionsMigrated, syncStatesMigrated);
                }
            }
            else
            {
                _logger.LogInformation("PostgreSQL Migrator: SQLite veritabanı bulunamadı ({Path}), sadece varsayılan tohumlama yapıldı.", sqlitePath);
            }

            // 4. Migrate order_addresses from SQLite if order_addresses.db exists
            string[] possibleOrderDbPaths =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace", "order_addresses.db"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EtsyMarketPlace", "order_addresses.db")
            ];

            foreach (var orderDbPath in possibleOrderDbPaths)
            {
                if (!File.Exists(orderDbPath)) continue;

                try
                {
                    _logger.LogInformation("PostgreSQL Migrator: Sipariş adresleri SQLite veritabanı bulundu ({Path}). Aktarım başlatılıyor...", orderDbPath);
                    var csb = new SqliteConnectionStringBuilder
                    {
                        DataSource = orderDbPath,
                        Mode = SqliteOpenMode.ReadOnly
                    };

                    await using var connection = new SqliteConnection(csb.ToString());
                    await connection.OpenAsync(cancellationToken);

                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"SELECT receipt_id, buyer_name, buyer_email, phone, street_address, second_address,
                                               city, state, postal_code, country_code, country_name, source, updated_at_utc,
                                               ioss_number, product_cost_usd, shipping_cost_usd, weight_kg, width_cm,
                                               length_cm, height_cm, gtip_code, selected_carrier, carrier_service_name,
                                               tracking_code, label_url FROM order_addresses;";

                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        long receiptId = reader.GetInt64(0);
                        if (receiptId <= 0) continue;

                        var exists = await _db.OrderAddresses.AnyAsync(o => o.ReceiptId == receiptId, cancellationToken);
                        if (!exists)
                        {
                            var updatedUtc = DateTime.TryParse(reader.IsDBNull(12) ? null : reader.GetString(12), out var parsedDt)
                                ? parsedDt.ToUniversalTime()
                                : DateTime.UtcNow;

                            _db.OrderAddresses.Add(new OrderAddress
                            {
                                ReceiptId = receiptId,
                                BuyerName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                BuyerEmail = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                StreetAddress = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                SecondAddress = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                City = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                State = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                PostalCode = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                                CountryCode = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                                CountryName = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                                Source = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                                UpdatedAtUtc = updatedUtc,
                                IossNumber = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                                ProductCostUsd = reader.IsDBNull(14) ? 0m : Convert.ToDecimal(reader.GetDouble(14)),
                                ShippingCostUsd = reader.IsDBNull(15) ? 0m : Convert.ToDecimal(reader.GetDouble(15)),
                                WeightKg = reader.IsDBNull(16) ? 0.0 : reader.GetDouble(16),
                                WidthCm = reader.IsDBNull(17) ? 0.0 : reader.GetDouble(17),
                                LengthCm = reader.IsDBNull(18) ? 0.0 : reader.GetDouble(18),
                                HeightCm = reader.IsDBNull(19) ? 0.0 : reader.GetDouble(19),
                                GtipCode = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                                SelectedCarrier = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                                CarrierServiceName = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                                TrackingCode = reader.IsDBNull(23) ? string.Empty : reader.GetString(23),
                                LabelUrl = reader.IsDBNull(24) ? string.Empty : reader.GetString(24)
                            });
                            orderAddressesMigrated++;
                        }
                    }

                    if (orderAddressesMigrated > 0)
                    {
                        await _db.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("PostgreSQL: {Count} sipariş teslimat adresi SQLite'tan aktarıldı.", orderAddressesMigrated);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite order_addresses tablosu okunamadı ({Path}): {Msg}", orderDbPath, ex.Message);
                }
            }

            return new PostgreSqlMigrationResult(
                usersSeeded,
                shopsSeeded,
                payoutsMigrated,
                transactionsMigrated,
                syncStatesMigrated,
                orderAddressesMigrated,
                "PostgreSQL veri tohumlama ve aktarım başarıyla tamamlandı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL veri göçü sırasında hata: {Message}", ex.Message);
            return new PostgreSqlMigrationResult(
                usersSeeded,
                shopsSeeded,
                payoutsMigrated,
                transactionsMigrated,
                syncStatesMigrated,
                orderAddressesMigrated,
                $"Hata: {ex.Message}");
        }
    }
}
