namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.AiSettings;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;

/// <summary>
/// PostgreSQL 16 EF Core + IDataProtector tabanlı kullanıcı bazlı AI API anahtarları deposu.
/// Her kullanıcı (AspNetUsers.Id) yalnızca kendi şifreli ayarlarına erişir.
/// PostgreSQL erişilemez olduğunda yerel SQLite yedeğine düşer.
/// </summary>
public sealed class EfPostgreSqlUserAiSettingsRepository : IUserAiSettingsRepository
{
    public const string DataProtectionPurpose = "EtsyMarketPlace.UserAiSettings.v1";

    private readonly DbContextLeaseProvider _dbLease;
    private readonly IDataProtector _protector;
    private readonly ILogger<EfPostgreSqlUserAiSettingsRepository> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    public EfPostgreSqlUserAiSettingsRepository(
        EtsyMarketPlaceDbContext context,
        IDataProtectionProvider protectionProvider,
        ILogger<EfPostgreSqlUserAiSettingsRepository> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null,
        IDbContextFactory<EtsyMarketPlaceDbContext>? contextFactory = null)
    {
        if (context is null && contextFactory is null)
            throw new ArgumentNullException(nameof(context));
        if (protectionProvider is null)
            throw new ArgumentNullException(nameof(protectionProvider));

        _dbLease = new DbContextLeaseProvider(context, contextFactory);
        _protector = protectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackStore = fallbackStore;
    }

    public async Task<string?> GetUserAiSettingsJsonAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var uid = userId.Trim();
        try
        {
            await using var lease = await _dbLease.AcquireAsync(cancellationToken);
            var entity = await lease.Context.UserAiSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == uid, cancellationToken);

            if (entity is null)
                return null;

            return UnprotectOrRaw(entity.EncryptedSettingsJson);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, kullanıcı AI ayarları SQLite yedeğinden alınıyor: {Message}", ex.Message);
            if (_fallbackStore is null) return null;
            var fallbackRaw = await _fallbackStore.GetUserAiSettingsJsonAsync(uid, cancellationToken);
            return fallbackRaw is null ? null : UnprotectOrRaw(fallbackRaw);
        }
    }

    public async Task SaveUserAiSettingsJsonAsync(string userId, string settingsJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Kullanıcı kimliği boş olamaz.", nameof(userId));

        var uid = userId.Trim();
        var rawJson = string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson;
        var activeProvider = ExtractActiveProvider(rawJson);
        var protectedJson = _protector.Protect(rawJson);

        try
        {
            await using var lease = await _dbLease.AcquireAsync(cancellationToken);
            var context = lease.Context;

            var userExists = await context.Users.AsNoTracking().AnyAsync(u => u.Id == uid, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException($"Kullanıcı bulunamadı (UserId: {uid}). AI ayarları yalnızca kayıtlı kullanıcılara bağlanabilir.");
            }

            var existing = await context.UserAiSettings
                .FirstOrDefaultAsync(s => s.UserId == uid, cancellationToken);

            if (existing is null)
            {
                context.UserAiSettings.Add(new UserAiSetting
                {
                    UserId = uid,
                    EncryptedSettingsJson = protectedJson,
                    ActiveProvider = activeProvider,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.EncryptedSettingsJson = protectedJson;
                existing.ActiveProvider = activeProvider;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, kullanıcı AI ayarları SQLite yedeğine kaydediliyor: {Message}", ex.Message);
            if (_fallbackStore is not null)
                await _fallbackStore.SaveUserAiSettingsJsonAsync(uid, protectedJson, activeProvider, cancellationToken);
            else
                throw;
        }
    }

    public async Task DeleteUserAiSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var uid = userId.Trim();
        try
        {
            await using var lease = await _dbLease.AcquireAsync(cancellationToken);
            var existing = await lease.Context.UserAiSettings
                .FirstOrDefaultAsync(s => s.UserId == uid, cancellationToken);

            if (existing is not null)
            {
                lease.Context.UserAiSettings.Remove(existing);
                await lease.Context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, kullanıcı AI ayarları SQLite yedeğinden siliniyor: {Message}", ex.Message);
            if (_fallbackStore is not null)
                await _fallbackStore.DeleteUserAiSettingsAsync(uid, cancellationToken);
        }
    }

    public static string ExtractActiveProvider(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("provider", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var p = prop.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(p))
                    return p.Length > 50 ? p[..50] : p;
            }
        }
        catch
        {
            // Geçersiz JSON için varsayılan sağlayıcı döner
        }

        return "Gemini";
    }

    private string UnprotectOrRaw(string cipherOrRaw)
    {
        if (string.IsNullOrWhiteSpace(cipherOrRaw))
            return "{}";
        try
        {
            return _protector.Unprotect(cipherOrRaw);
        }
        catch
        {
            return cipherOrRaw;
        }
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
}
