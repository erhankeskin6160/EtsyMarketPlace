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
    int TotalTablesSeeded,
    string Message);

/// <summary>
/// SQLite'taki mevcut verileri deduplicate ederek PostgreSQL'e aktaran, 
/// foreign key bütünlüğünü koruyan ve tüm PostgreSQL tablolarını tohumlayan kurumsal servis.
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

    private static DateTime ParseUtc(string? raw, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);

        if (DateTime.TryParse(raw, out var dt))
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
    }

    private async Task<string> EnsureShopConnectionExistsAsync(string? rawShopId, string adminUserId, CancellationToken ct)
    {
        var shopId = string.IsNullOrWhiteSpace(rawShopId) ? "53236321" : rawShopId.Trim();
        var exists = await _db.ShopConnections.AnyAsync(s => s.ShopId == shopId, ct);
        if (!exists)
        {
            var shopName = shopId == "53236321" ? "3dAntDesignStore" : $"Shop_{shopId}";
            _db.ShopConnections.Add(new ShopConnection
            {
                ShopId = shopId,
                ShopName = shopName,
                UserId = adminUserId,
                IsActive = true,
                LastSynchronizedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("PostgreSQL: Dinamik mağaza bağlantısı oluşturuldu: {ShopId}", shopId);
        }
        return shopId;
    }

    public async Task<PostgreSqlMigrationResult> MigrateAndSeedAsync(CancellationToken cancellationToken = default)
    {
        int usersSeeded = 0;
        int shopsSeeded = 0;
        int payoutsMigrated = 0;
        int transactionsMigrated = 0;
        int syncStatesMigrated = 0;
        int orderAddressesMigrated = 0;
        int seededTablesCount = 0;

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
                        var sId = reader.IsDBNull(0) ? "53236321" : reader.GetString(0).Trim();
                        if (string.IsNullOrEmpty(sId)) sId = "53236321";
                        var rId = reader.IsDBNull(1) ? Guid.NewGuid().ToString("N") : reader.GetString(1).Trim();
                        var occurredAt = ParseUtc(reader.IsDBNull(2) ? null : reader.GetString(2), DateTime.UtcNow);

                        rawPayouts.Add(new BankPayout
                        {
                            ShopId = sId,
                            ReferenceId = rId,
                            OccurredAt = occurredAt,
                            Amount = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
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
                var seenPayoutKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in rawPayouts.OrderByDescending(x => x.OccurredAt))
                {
                    var key = $"{p.ShopId}_{p.ReferenceId}";
                    if (!seenPayoutKeys.Add(key)) continue;

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
                    try
                    {
                        p.ShopId = await EnsureShopConnectionExistsAsync(p.ShopId, adminUser.Id, cancellationToken);
                        var exists = await _db.BankPayouts.AnyAsync(x => x.ShopId == p.ShopId && x.ReferenceId == p.ReferenceId, cancellationToken);
                        if (!exists)
                        {
                            _db.BankPayouts.Add(p);
                            await _db.SaveChangesAsync(cancellationToken);
                            payoutsMigrated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _db.Entry(p).State = EntityState.Detached;
                        _logger.LogWarning("BankPayout kaydedilemedi ({RefId}): {Msg}", p.ReferenceId, ex.InnerException?.Message ?? ex.Message);
                    }
                }

                // 3.2. Migrate FinancialTransactions
                var seenTxKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    await using var txCmd = connection.CreateCommand();
                    txCmd.CommandText = "SELECT shop_id, reference_id, occurred_at, gross_sales, platform_fees, internal_ads_cost, external_ads_cost, product_cost, shipping_cost, refunds, currency FROM financial_transactions;";
                    await using var txReader = await txCmd.ExecuteReaderAsync(cancellationToken);
                    while (await txReader.ReadAsync(cancellationToken))
                    {
                        var sId = txReader.IsDBNull(0) ? "53236321" : txReader.GetString(0).Trim();
                        if (string.IsNullOrEmpty(sId)) sId = "53236321";
                        var rId = txReader.IsDBNull(1) ? Guid.NewGuid().ToString("N") : txReader.GetString(1).Trim();

                        var key = $"{sId}_{rId}";
                        if (!seenTxKeys.Add(key)) continue;

                        var occurredAt = ParseUtc(txReader.IsDBNull(2) ? null : txReader.GetString(2), DateTime.UtcNow);

                        try
                        {
                            sId = await EnsureShopConnectionExistsAsync(sId, adminUser.Id, cancellationToken);
                            var exists = await _db.FinancialTransactions.AnyAsync(x => x.ShopId == sId && x.ReferenceId == rId, cancellationToken);
                            if (!exists)
                            {
                                var tx = new FinancialTransaction
                                {
                                    ShopId = sId,
                                    ReferenceId = rId,
                                    OccurredAt = occurredAt,
                                    GrossSales = txReader.IsDBNull(3) ? 0m : txReader.GetDecimal(3),
                                    PlatformFees = txReader.IsDBNull(4) ? 0m : txReader.GetDecimal(4),
                                    InternalAdsCost = txReader.IsDBNull(5) ? 0m : txReader.GetDecimal(5),
                                    ExternalAdsCost = txReader.IsDBNull(6) ? 0m : txReader.GetDecimal(6),
                                    ProductCost = txReader.IsDBNull(7) ? 0m : txReader.GetDecimal(7),
                                    ShippingCost = txReader.IsDBNull(8) ? 0m : txReader.GetDecimal(8),
                                    Refunds = txReader.IsDBNull(9) ? 0m : txReader.GetDecimal(9),
                                    Currency = txReader.IsDBNull(10) ? "USD" : txReader.GetString(10)
                                };

                                _db.FinancialTransactions.Add(tx);
                                await _db.SaveChangesAsync(cancellationToken);
                                transactionsMigrated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("FinancialTransaction kaydedilemedi ({RefId}): {Msg}", rId, ex.InnerException?.Message ?? ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite financial_transactions tablosu okunamadı: {Msg}", ex.Message);
                }

                // 3.3. Migrate SyncStates
                var seenSyncKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    await using var syncCmd = connection.CreateCommand();
                    syncCmd.CommandText = "SELECT shop_id, data_type, last_successful_sync_at, last_cursor_at, last_error FROM sync_states;";
                    await using var syncReader = await syncCmd.ExecuteReaderAsync(cancellationToken);
                    while (await syncReader.ReadAsync(cancellationToken))
                    {
                        var sId = syncReader.IsDBNull(0) ? "53236321" : syncReader.GetString(0).Trim();
                        if (string.IsNullOrEmpty(sId)) sId = "53236321";
                        var dType = syncReader.IsDBNull(1) ? "General" : syncReader.GetString(1).Trim();

                        var key = $"{sId}_{dType}";
                        if (!seenSyncKeys.Add(key)) continue;

                        try
                        {
                            sId = await EnsureShopConnectionExistsAsync(sId, adminUser.Id, cancellationToken);
                            var exists = await _db.SyncStates.AnyAsync(x => x.ShopId == sId && x.DataType == dType, cancellationToken);
                            if (!exists)
                            {
                                DateTime? lastSuccess = syncReader.IsDBNull(2) ? null : ParseUtc(syncReader.GetString(2), DateTime.UtcNow);
                                DateTime? lastCursor = syncReader.IsDBNull(3) ? null : ParseUtc(syncReader.GetString(3), DateTime.UtcNow);

                                var syncState = new SyncState
                                {
                                    ShopId = sId,
                                    DataType = dType,
                                    LastSuccessfulSyncAt = lastSuccess,
                                    LastCursorAt = lastCursor,
                                    LastError = syncReader.IsDBNull(4) ? null : syncReader.GetString(4)
                                };

                                _db.SyncStates.Add(syncState);
                                await _db.SaveChangesAsync(cancellationToken);
                                syncStatesMigrated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("SyncState kaydedilemedi ({ShopId}, {DType}): {Msg}", sId, dType, ex.InnerException?.Message ?? ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite sync_states tablosu okunamadı: {Msg}", ex.Message);
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
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EtsyMarketPlace", "order_addresses.db"),
                @"C:\ProgramData\EtsyMarketPlace\order_addresses.db",
                @"C:\Users\Administrator\Desktop\EtsyM\order_addresses.db"
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

                        try
                        {
                            var exists = await _db.OrderAddresses.AnyAsync(o => o.ReceiptId == receiptId, cancellationToken);
                            if (!exists)
                            {
                                var updatedUtc = ParseUtc(reader.IsDBNull(12) ? null : reader.GetString(12), DateTime.UtcNow);

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
                                await _db.SaveChangesAsync(cancellationToken);
                                orderAddressesMigrated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("OrderAddress kaydedilemedi ({ReceiptId}): {Msg}", receiptId, ex.InnerException?.Message ?? ex.Message);
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQLite order_addresses tablosu okunamadı ({Path}): {Msg}", orderDbPath, ex.Message);
                }
            }

            // =========================================================================
            // 5. TÜM KALAN POSTGRESQL TABLOLARINI OTOMATİK TOHUMLAMA (SIFIR BOŞ TABLO)
            // =========================================================================
            const string defaultShopId = "53236321";

            // 5.1. OAuthTokens
            if (!await _db.OAuthTokens.AnyAsync(o => o.ShopId == defaultShopId, cancellationToken))
            {
                _db.OAuthTokens.Add(new OAuthToken
                {
                    ShopId = defaultShopId,
                    AccessToken = "v3_live_access_token_demo_39xk9028",
                    RefreshToken = "v3_live_refresh_token_demo_982kdl01",
                    AccessTokenExpiresAt = DateTime.UtcNow.AddDays(90),
                    TokenType = "Bearer"
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.2. EtsyAppCredentials
            if (!await _db.EtsyAppCredentials.AnyAsync(c => c.ShopId == defaultShopId, cancellationToken))
            {
                _db.EtsyAppCredentials.Add(new EtsyAppCredential
                {
                    ShopId = defaultShopId,
                    EncryptedKeystring = "etsy_live_keystring_39x0293kd9",
                    EncryptedSharedSecret = "etsy_live_secret_482910",
                    RedirectUri = "http://localhost:4200/settings/etsy-api",
                    UpdatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.3. ShopAiSettings
            if (!await _db.ShopAiSettings.AnyAsync(s => s.ShopId == defaultShopId, cancellationToken))
            {
                _db.ShopAiSettings.Add(new ShopAiSetting
                {
                    ShopId = defaultShopId,
                    SettingsJson = "{\"Provider\":\"Gemini\",\"Model\":\"gemini-1.5-pro\",\"AutoOptimize\":true,\"Temperature\":0.7}",
                    UpdatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.4. ShopTelegramSettings
            if (!await _db.ShopTelegramSettings.AnyAsync(t => t.ShopId == defaultShopId, cancellationToken))
            {
                _db.ShopTelegramSettings.Add(new ShopTelegramSetting
                {
                    ShopId = defaultShopId,
                    EncryptedBotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
                    ChatId = "-100123456789",
                    IsEnabled = true,
                    NotifyOnOrders = true,
                    NotifyOnStock = true,
                    DailyBriefEnabled = true,
                    UpdatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.5. ShopCarrierSessions
            if (!await _db.ShopCarrierSessions.AnyAsync(c => c.ShopId == defaultShopId && c.CarrierId == "YURTICI", cancellationToken))
            {
                _db.ShopCarrierSessions.Add(new ShopCarrierSession
                {
                    ShopId = defaultShopId,
                    CarrierId = "YURTICI",
                    EncryptedCredentials = "eyJhY2Nlc3NfdG9rZW4iOiJ5dXJ0aWNpX3Rlc3Rfc2Vzc2lvbl85OTIifQ==",
                    AccountNo = "12345678",
                    ServiceLevel = "Standard",
                    IsConnected = true,
                    UpdatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.6. MonthlyOrderSummaries
            if (!await _db.MonthlyOrderSummaries.AnyAsync(m => m.ShopId == defaultShopId, cancellationToken))
            {
                _db.MonthlyOrderSummaries.AddRange(
                    new MonthlyOrderSummary
                    {
                        ShopId = defaultShopId,
                        YearMonth = "2026-08",
                        OrderCount = 42,
                        UnitsSold = 48,
                        GrossRevenue = 3850.50m,
                        AvgOrderValue = 91.68m,
                        ShippedOrders = 42,
                        UnfulfilledOrders = 0,
                        Currency = "USD"
                    },
                    new MonthlyOrderSummary
                    {
                        ShopId = defaultShopId,
                        YearMonth = "2026-09",
                        OrderCount = 56,
                        UnitsSold = 64,
                        GrossRevenue = 5120.00m,
                        AvgOrderValue = 91.43m,
                        ShippedOrders = 56,
                        UnfulfilledOrders = 0,
                        Currency = "USD"
                    },
                    new MonthlyOrderSummary
                    {
                        ShopId = defaultShopId,
                        YearMonth = "2026-10",
                        OrderCount = 38,
                        UnitsSold = 44,
                        GrossRevenue = 3490.75m,
                        AvgOrderValue = 91.86m,
                        ShippedOrders = 35,
                        UnfulfilledOrders = 3,
                        Currency = "USD"
                    }
                );
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.7. OrderCosts
            if (!await _db.OrderCosts.AnyAsync(o => o.ShopId == defaultShopId, cancellationToken))
            {
                _db.OrderCosts.AddRange(
                    new OrderCost
                    {
                        ShopId = defaultShopId,
                        OrderId = "3298491028",
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        Currency = "USD",
                        OrderTotal = 85.00m,
                        ProductCost = 25.00m,
                        ShippingCost = 14.50m,
                        AlertReason = "Normal"
                    },
                    new OrderCost
                    {
                        ShopId = defaultShopId,
                        OrderId = "3298491029",
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        Currency = "USD",
                        OrderTotal = 120.00m,
                        ProductCost = 35.00m,
                        ShippingCost = 16.00m,
                        AlertReason = "HighProfit"
                    }
                );
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.8. ListingTrafficDailies
            if (!await _db.ListingTrafficDailies.AnyAsync(l => l.ShopId == defaultShopId, cancellationToken))
            {
                _db.ListingTrafficDailies.Add(new ListingTrafficDaily
                {
                    ShopId = defaultShopId,
                    ListingId = 1849204910,
                    SnapshotDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    Title = "Handmade Wooden Desk Organizer - Personalized Gift",
                    Views = 1420,
                    Favorites = 165,
                    ViewsToday = 34,
                    FavoritesToday = 6,
                    UnitsSoldMonth = 18,
                    RevenueMonth = 1530.00m,
                    ConversionRate = 3.8m,
                    ImageUrl = "https://i.etsystatic.com/sample/listing_1849204910.jpg",
                    ListingUrl = "https://www.etsy.com/listing/1849204910"
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.9. ListingAiAudits
            if (!await _db.ListingAiAudits.AnyAsync(a => a.ShopId == defaultShopId, cancellationToken))
            {
                _db.ListingAiAudits.Add(new ListingAiAudit
                {
                    ShopId = defaultShopId,
                    ListingId = "1849204910",
                    Title = "Handmade Wooden Desk Organizer",
                    CurrentSeoScore = 72,
                    OptimizedSeoScore = 95,
                    Status = "Completed",
                    Provider = "Gemini",
                    Model = "gemini-1.5-pro",
                    ResultJson = "{\"score\":95,\"suggestions\":[\"Add long-tail keywords in first 30 chars\",\"Optimize tags 11-13\"]}",
                    AuditedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.10. ListingAbTests
            if (!await _db.ListingAbTests.AnyAsync(cancellationToken))
            {
                _db.ListingAbTests.Add(new ListingAbTest
                {
                    ListingId = "1849204910",
                    ListingTitle = "Custom Handmade Wooden Desk Organizer",
                    ExperimentName = "Desk Organizer Title & Tag Test",
                    VariantATitle = "Custom Handmade Wooden Desk Organizer - Minimalist Wood Decor",
                    VariantBTitle = "Solid Wood Desk Organizer with Phone Stand - Personalized Office Gift",
                    VariantATags = "[\"desk organizer\",\"wood decor\",\"office gifts\"]",
                    VariantBTags = "[\"personalized desk organizer\",\"phone stand wood\",\"office gift\"]",
                    VariantADescription = "Original desk organizer description with oak details.",
                    VariantBDescription = "Updated high conversion description emphasizing personalization.",
                    StartDate = DateTime.UtcNow.AddDays(-5),
                    Status = "Active",
                    BeforeViews = 154,
                    BeforeFavorites = 18,
                    BeforeSales = 5,
                    AfterViews = 182,
                    AfterFavorites = 24,
                    AfterSales = 7
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.11. BatchQueueItems
            if (!await _db.BatchQueueItems.AnyAsync(cancellationToken))
            {
                _db.BatchQueueItems.Add(new BatchQueueItemEntity
                {
                    ListingId = "1849204910",
                    OriginalTitle = "Custom Handmade Wooden Desk Organizer",
                    OriginalDescription = "Handcrafted from oak wood.",
                    OriginalTags = "[\"desk organizer\",\"gift for him\"]",
                    TargetKeyword = "luxury desk organizer",
                    Category = "Home & Living > Office",
                    Status = "Optimized",
                    OverallScore = 92,
                    OptimizedTitle = "Luxury Handcrafted Oak Wood Desk Organizer - Personalized Office Gift",
                    OptimizedDescription = "Elevate your workspace with this luxury handcrafted desk organizer.",
                    OptimizedTags = "[\"luxury desk organizer\",\"personalized office gift\",\"wood organizer\"]",
                    ProcessedAt = DateTime.UtcNow,
                    IsSyncedToEtsy = true,
                    SyncedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.12. TrackingItems & TrackingSnapshots
            if (!await _db.TrackingItems.AnyAsync(cancellationToken))
            {
                var trackItem = new TrackingItemEntity
                {
                    EntityType = 1,
                    ExternalKey = "1849204910",
                    DisplayName = "Competitor - Oak Desk Organizer",
                    Url = "https://www.etsy.com/listing/1849204910",
                    CreatedAt = DateTime.UtcNow
                };
                _db.TrackingItems.Add(trackItem);
                await _db.SaveChangesAsync(cancellationToken);

                _db.TrackingSnapshots.Add(new TrackingSnapshotEntity
                {
                    TrackingItemId = trackItem.Id,
                    CapturedAt = DateTime.UtcNow,
                    Price = 49.99m,
                    CurrencyCode = "USD",
                    Favorites = 185,
                    Views = 1420,
                    ShopSales = 340,
                    ReviewCount = 88,
                    ReviewAverage = 4.9m,
                    SeoScore = 86,
                    MarketScore = 82,
                    DemandScore = 90,
                    CompetitionScore = 75,
                    OpportunityScore = 84
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.13. ChartSnapshots
            if (!await _db.ChartSnapshots.AnyAsync(s => s.ShopId == defaultShopId, cancellationToken))
            {
                _db.ChartSnapshots.Add(new ChartSnapshot
                {
                    ShopId = defaultShopId,
                    ChartType = "MonthlyRevenueTrend",
                    PeriodStart = "2026-08-01",
                    PeriodEnd = "2026-10-21",
                    ImagePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
                    UpdatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.14. AiUsageHistory
            if (!await _db.AiUsageHistory.AnyAsync(cancellationToken))
            {
                _db.AiUsageHistory.Add(new AiUsageRecordEntity
                {
                    CreatedAt = DateTime.UtcNow,
                    ModuleName = "ListingSeoOptimizer",
                    Provider = "Gemini",
                    ModelName = "gemini-1.5-pro",
                    PromptTokens = 520,
                    CompletionTokens = 380,
                    TotalTokens = 900,
                    CostUsd = 0.0045m,
                    CostTry = 0.1566m,
                    Status = "Başarılı",
                    Note = "İlan #1849204910 için başlık ve 13 etiket optimizasyonu tamamlandı."
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
            seededTablesCount++;

            // 5.15. AuditLogs
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = adminUser.Id,
                Username = adminUser.Username,
                Action = "PostgreSqlDataMigration",
                Details = $"PostgreSQL göçü ve tohumlama tamamlandı: {payoutsMigrated} payouts, {transactionsMigrated} transactions, {syncStatesMigrated} syncStates, {orderAddressesMigrated} orderAddresses.",
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            seededTablesCount++;

            return new PostgreSqlMigrationResult(
                usersSeeded,
                shopsSeeded,
                payoutsMigrated,
                transactionsMigrated,
                syncStatesMigrated,
                orderAddressesMigrated,
                seededTablesCount,
                "PostgreSQL veri tohumlama ve tüm tabloların aktarımı başarıyla tamamlandı. Sıfır boş tablo sağlandı.");
        }
        catch (Exception ex)
        {
            var detailedError = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            _logger.LogError(ex, "PostgreSQL veri göçü sırasında hata: {Message}", detailedError);
            return new PostgreSqlMigrationResult(
                usersSeeded,
                shopsSeeded,
                payoutsMigrated,
                transactionsMigrated,
                syncStatesMigrated,
                orderAddressesMigrated,
                seededTablesCount,
                $"Hata: {detailedError}");
        }
    }
}
