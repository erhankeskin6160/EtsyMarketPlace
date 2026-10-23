using EtsyMarketPlace.Domain.Common;
namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

/// <summary>
/// PostgreSQL 16 EF Core ve LINQ tabanlı Mağaza Ayarları Reposu.
/// Sıfır ham SQL veya ADO.NET; tamamen tip güvenli LINQ sorguları.
/// </summary>
public sealed class EfPostgreSqlShopSettingsRepository : IShopSettingsRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<EfPostgreSqlShopSettingsRepository> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    public EfPostgreSqlShopSettingsRepository(
        EtsyMarketPlaceDbContext context,
        IDataProtectionProvider protectionProvider,
        ILogger<EfPostgreSqlShopSettingsRepository> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _protector = protectionProvider.CreateProtector("EtsyMarketPlace.ShopSettings.v1");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackStore = fallbackStore;
    }

    private static bool IsDatabaseUnavailableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException ||
                current is System.Net.Sockets.SocketException ||
                current is TimeoutException)
            {
                return true;
            }
        }
        return false;
    }

    public async Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return null;

        try
        {
            var entity = await _context.ShopTelegramSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShopId == shopId.Trim(), cancellationToken);

            if (entity is null)
            {
                return _fallbackStore != null ? await _fallbackStore.GetTelegramSettingsAsync(shopId, cancellationToken) : null;
            }

            string maskedToken = MaskSecret(UnprotectOrRaw(entity.EncryptedBotToken));
            return new TelegramShopSettings(
                entity.ShopId,
                maskedToken,
                entity.ChatId,
                entity.IsEnabled,
                entity.NotifyOnOrders,
                entity.NotifyOnStock,
                entity.DailyBriefEnabled,
                entity.UpdatedAt.ToUtcOffset());
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, Telegram ayarları için fallback deneniyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetTelegramSettingsAsync(shopId, cancellationToken) : null;
        }
    }

    public async Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var sid = request.ShopId.Trim();

        try
        {
            await EnsureShopExistsAsync(sid, cancellationToken);

            var existing = await _context.ShopTelegramSettings
                .FirstOrDefaultAsync(s => s.ShopId == sid, cancellationToken);

            string encryptedToken = !string.IsNullOrWhiteSpace(request.BotToken)
                ? _protector.Protect(request.BotToken.Trim())
                : (existing?.EncryptedBotToken ?? string.Empty);

            if (existing is null)
            {
                existing = new ShopTelegramSetting
                {
                    ShopId = sid,
                    EncryptedBotToken = encryptedToken,
                    ChatId = request.ChatId ?? string.Empty,
                    IsEnabled = request.IsEnabled,
                    NotifyOnOrders = request.NotifyOnOrders,
                    NotifyOnStock = request.NotifyOnStock,
                    DailyBriefEnabled = request.DailyBriefEnabled,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ShopTelegramSettings.Add(existing);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.BotToken))
                    existing.EncryptedBotToken = encryptedToken;
                existing.ChatId = request.ChatId ?? string.Empty;
                existing.IsEnabled = request.IsEnabled;
                existing.NotifyOnOrders = request.NotifyOnOrders;
                existing.NotifyOnStock = request.NotifyOnStock;
                existing.DailyBriefEnabled = request.DailyBriefEnabled;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            string maskedToken = MaskSecret(UnprotectOrRaw(existing.EncryptedBotToken));
            return new TelegramShopSettings(
                existing.ShopId,
                maskedToken,
                existing.ChatId,
                existing.IsEnabled,
                existing.NotifyOnOrders,
                existing.NotifyOnStock,
                existing.DailyBriefEnabled,
                existing.UpdatedAt.ToUtcOffset());
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, Telegram ayarları SQLite yedeğe kaydediliyor: {Message}", ex.Message);
            if (_fallbackStore != null)
                return await _fallbackStore.SaveTelegramSettingsAsync(request, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        try
        {
            var entities = await _context.ShopCarrierSessions
                .AsNoTracking()
                .Where(c => c.ShopId == shopId.Trim())
                .ToListAsync(cancellationToken);

            if (entities.Count == 0 && _fallbackStore != null)
                return await _fallbackStore.GetCarrierSessionsAsync(shopId, cancellationToken);

            return entities.Select(e => new CarrierSessionRecord(
                e.ShopId,
                e.CarrierId,
                MaskSecret(UnprotectOrRaw(e.EncryptedCredentials)),
                e.AccountNo,
                e.ServiceLevel,
                e.IsConnected,
                e.UpdatedAt.ToUtcOffset()
            )).ToList();
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, kargo oturumları SQLite yedeğinden alınıyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetCarrierSessionsAsync(shopId, cancellationToken) : [];
        }
    }

    public async Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default)
    {
        var sid = request.ShopId.Trim();

        try
        {
            await EnsureShopExistsAsync(sid, cancellationToken);

            var existing = await _context.ShopCarrierSessions
                .FirstOrDefaultAsync(c => c.ShopId == sid && c.CarrierId == request.CarrierId, cancellationToken);

            string encryptedCreds = !string.IsNullOrWhiteSpace(request.Credentials)
                ? _protector.Protect(request.Credentials.Trim())
                : (existing?.EncryptedCredentials ?? string.Empty);

            if (existing is null)
            {
                existing = new ShopCarrierSession
                {
                    ShopId = sid,
                    CarrierId = request.CarrierId,
                    EncryptedCredentials = encryptedCreds,
                    AccountNo = request.AccountNo ?? string.Empty,
                    ServiceLevel = request.ServiceLevel ?? string.Empty,
                    IsConnected = request.IsConnected,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ShopCarrierSessions.Add(existing);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.Credentials))
                    existing.EncryptedCredentials = encryptedCreds;
                existing.AccountNo = request.AccountNo ?? string.Empty;
                existing.ServiceLevel = request.ServiceLevel ?? string.Empty;
                existing.IsConnected = request.IsConnected;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new CarrierSessionRecord(
                existing.ShopId,
                existing.CarrierId,
                MaskSecret(UnprotectOrRaw(existing.EncryptedCredentials)),
                existing.AccountNo,
                existing.ServiceLevel,
                existing.IsConnected,
                existing.UpdatedAt.ToUtcOffset());
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, kargo oturumu SQLite yedeğine kaydediliyor: {Message}", ex.Message);
            if (_fallbackStore != null)
                return await _fallbackStore.SaveCarrierSessionAsync(request, cancellationToken);
            throw;
        }
    }

    public async Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return null;

        try
        {
            var entity = await _context.EtsyAppCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ShopId == shopId.Trim(), cancellationToken);

            if (entity is null)
                return _fallbackStore != null ? await _fallbackStore.GetEtsyAppCredentialsAsync(shopId, cancellationToken) : null;

            return new EtsyAppCredentialsRecord(
                entity.ShopId,
                MaskSecret(UnprotectOrRaw(entity.EncryptedKeystring)),
                MaskSecret(UnprotectOrRaw(entity.EncryptedSharedSecret)),
                entity.RedirectUri,
                entity.UpdatedAt.ToUtcOffset());
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, Etsy kimlik bilgileri SQLite yedeğinden alınıyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetEtsyAppCredentialsAsync(shopId, cancellationToken) : null;
        }
    }

    public async Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return (string.Empty, string.Empty, string.Empty);

        try
        {
            var entity = await _context.EtsyAppCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ShopId == shopId.Trim(), cancellationToken);

            if (entity is null)
                return _fallbackStore != null ? await _fallbackStore.GetRawEtsyAppCredentialsAsync(shopId, cancellationToken) : (string.Empty, string.Empty, string.Empty);

            return (
                UnprotectOrRaw(entity.EncryptedKeystring),
                UnprotectOrRaw(entity.EncryptedSharedSecret),
                entity.RedirectUri);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, ham kimlik bilgileri SQLite yedeğinden alınıyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetRawEtsyAppCredentialsAsync(shopId, cancellationToken) : (string.Empty, string.Empty, string.Empty);
        }
    }

    public async Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        var sid = request.ShopId.Trim();

        try
        {
            await EnsureShopExistsAsync(sid, cancellationToken);

            var existing = await _context.EtsyAppCredentials
                .FirstOrDefaultAsync(e => e.ShopId == sid, cancellationToken);

            string encKey = !string.IsNullOrWhiteSpace(request.Keystring)
                ? _protector.Protect(request.Keystring.Trim())
                : (existing?.EncryptedKeystring ?? string.Empty);

            string encSecret = !string.IsNullOrWhiteSpace(request.SharedSecret)
                ? _protector.Protect(request.SharedSecret.Trim())
                : (existing?.EncryptedSharedSecret ?? string.Empty);

            if (existing is null)
            {
                existing = new EtsyAppCredential
                {
                    ShopId = sid,
                    EncryptedKeystring = encKey,
                    EncryptedSharedSecret = encSecret,
                    RedirectUri = request.RedirectUri ?? string.Empty,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.EtsyAppCredentials.Add(existing);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.Keystring)) existing.EncryptedKeystring = encKey;
                if (!string.IsNullOrWhiteSpace(request.SharedSecret)) existing.EncryptedSharedSecret = encSecret;
                if (!string.IsNullOrWhiteSpace(request.RedirectUri)) existing.RedirectUri = request.RedirectUri;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new EtsyAppCredentialsRecord(
                existing.ShopId,
                MaskSecret(UnprotectOrRaw(existing.EncryptedKeystring)),
                MaskSecret(UnprotectOrRaw(existing.EncryptedSharedSecret)),
                existing.RedirectUri,
                existing.UpdatedAt.ToUtcOffset());
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, Etsy kimlik bilgileri SQLite yedeğine kaydediliyor: {Message}", ex.Message);
            if (_fallbackStore != null)
                return await _fallbackStore.SaveEtsyAppCredentialsAsync(request, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        try
        {
            var entities = await _context.ListingAiAudits
                .AsNoTracking()
                .Where(a => a.ShopId == shopId.Trim())
                .OrderByDescending(a => a.AuditedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            if (entities.Count == 0 && _fallbackStore != null)
                return await _fallbackStore.GetListingAuditsAsync(shopId, cancellationToken);

            return entities.Select(e => new SavedListingAuditRecord(
                e.ShopId,
                e.ListingId,
                e.Title,
                e.CurrentSeoScore,
                e.OptimizedSeoScore,
                e.Status,
                e.Provider,
                e.Model,
                e.ResultJson,
                e.AuditedAt.ToUtcOffset()
            )).ToList();
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, denetim kayıtları SQLite yedeğinden alınıyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetListingAuditsAsync(shopId, cancellationToken) : [];
        }
    }

    public async Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var sid = request.ShopId.Trim();
            await EnsureShopExistsAsync(sid, cancellationToken);

            _context.ListingAiAudits.Add(new ListingAiAudit
            {
                ShopId = sid,
                ListingId = request.ListingId,
                Title = request.Title,
                CurrentSeoScore = request.CurrentSeoScore,
                OptimizedSeoScore = request.OptimizedSeoScore,
                Status = request.Status,
                Provider = request.Provider,
                Model = request.Model,
                ResultJson = request.ResultJson,
                AuditedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL'e listing audit kaydedilemedi (çevrimdışı/CI ortamı): {Message}", ex.Message);
            if (_fallbackStore != null)
                await _fallbackStore.SaveListingAuditAsync(request, cancellationToken);
        }
    }

    public async Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return null;

        try
        {
            var entity = await _context.ShopAiSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShopId == shopId.Trim(), cancellationToken);

            if (entity is null && _fallbackStore != null)
                return await _fallbackStore.GetAiSettingsJsonAsync(shopId, cancellationToken);

            return entity?.SettingsJson;
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, AI ayarları SQLite yedeğinden alınıyor: {Message}", ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetAiSettingsJsonAsync(shopId, cancellationToken) : null;
        }
    }

    public async Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken cancellationToken = default)
    {
        var sid = shopId.Trim();

        try
        {
            await EnsureShopExistsAsync(sid, cancellationToken);

            var existing = await _context.ShopAiSettings
                .FirstOrDefaultAsync(s => s.ShopId == sid, cancellationToken);

            if (existing is null)
            {
                existing = new ShopAiSetting
                {
                    ShopId = sid,
                    SettingsJson = settingsJson ?? "{}",
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ShopAiSettings.Add(existing);
            }
            else
            {
                existing.SettingsJson = settingsJson ?? "{}";
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, AI ayarları SQLite yedeğine kaydediliyor: {Message}", ex.Message);
            if (_fallbackStore != null)
                await _fallbackStore.SaveAiSettingsJsonAsync(shopId, settingsJson, cancellationToken);
            else
                throw;
        }
    }

    private async Task EnsureShopExistsAsync(string shopId, CancellationToken ct)
    {
        var exists = await _context.ShopConnections.AnyAsync(s => s.ShopId == shopId, ct);
        if (!exists)
        {
            _context.ShopConnections.Add(new ShopConnection
            {
                ShopId = shopId,
                ShopName = shopId,
                IsActive = true
            });
            await _context.SaveChangesAsync(ct);
        }
    }

    private string UnprotectOrRaw(string cipherOrRaw)
    {
        if (string.IsNullOrWhiteSpace(cipherOrRaw)) return string.Empty;
        try { return _protector.Unprotect(cipherOrRaw); }
        catch { return cipherOrRaw; }
    }

    private static string MaskSecret(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        if (s.Length <= 8) return "****";
        return $"{s[..4]}...{s[^4..]}";
    }
}
