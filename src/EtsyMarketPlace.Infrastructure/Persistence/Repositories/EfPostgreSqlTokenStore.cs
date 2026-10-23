namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Domain.Common;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

/// <summary>
/// PostgreSQL 16 EF Core ve LINQ tabanlı Etsy OAuth Token deposu.
/// Sıfır ham SQL veya ADO.NET; tamamen şifrelenmiş ve tip güvenli LINQ sorguları.
/// </summary>
public sealed class EfPostgreSqlTokenStore : IEtsyTokenStore
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<EfPostgreSqlTokenStore> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    public EfPostgreSqlTokenStore(
        EtsyMarketPlaceDbContext context,
        IDataProtectionProvider protectionProvider,
        ILogger<EfPostgreSqlTokenStore> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _protector = protectionProvider.CreateProtector("EtsyMarketPlace.EtsyOAuthToken.v2");
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

    public async Task<EtsyOAuthToken?> GetAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return null;

        try
        {
            var entity = await _context.OAuthTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ShopId == shopId.Trim(), cancellationToken);

            if (entity is null)
            {
                return _fallbackStore != null ? await _fallbackStore.GetAsync(shopId, cancellationToken) : null;
            }

            string accessToken = UnprotectOrRaw(entity.AccessToken);
            string refreshToken = UnprotectOrRaw(entity.RefreshToken);

            return new EtsyOAuthToken(
                accessToken,
                refreshToken,
                entity.AccessTokenExpiresAt.ToUtcOffset(),
                entity.TokenType ?? "Bearer");
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, OAuth token SQLite yedeğinden alınıyor (ShopId: {ShopId})", shopId);
            return _fallbackStore != null ? await _fallbackStore.GetAsync(shopId, cancellationToken) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL'den OAuth token alınırken hata oluştu (ShopId: {ShopId}): {Message}", shopId, ex.Message);
            return _fallbackStore != null ? await _fallbackStore.GetAsync(shopId, cancellationToken) : null;
        }
    }

    public async Task SaveAsync(string shopId, EtsyOAuthToken token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId) || token is null) return;

        try
        {
            var normalizedShopId = shopId.Trim();

            // Önce mağaza bağlantısının varlığından emin ol
            var shop = await _context.ShopConnections
                .FirstOrDefaultAsync(s => s.ShopId == normalizedShopId, cancellationToken);

            if (shop is null)
            {
                shop = new ShopConnection
                {
                    ShopId = normalizedShopId,
                    ShopName = normalizedShopId,
                    IsActive = true
                };
                _context.ShopConnections.Add(shop);
            }

            var existing = await _context.OAuthTokens
                .FirstOrDefaultAsync(t => t.ShopId == normalizedShopId, cancellationToken);

            var protectedAccess = _protector.Protect(token.AccessToken);
            var protectedRefresh = _protector.Protect(token.RefreshToken);

            if (existing is null)
            {
                _context.OAuthTokens.Add(new OAuthToken
                {
                    ShopId = normalizedShopId,
                    AccessToken = protectedAccess,
                    RefreshToken = protectedRefresh,
                    AccessTokenExpiresAt = token.AccessTokenExpiresAt.UtcDateTime,
                    TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType
                });
            }
            else
            {
                existing.AccessToken = protectedAccess;
                existing.RefreshToken = protectedRefresh;
                existing.AccessTokenExpiresAt = token.AccessTokenExpiresAt.UtcDateTime;
                existing.TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Etsy OAuth token PostgreSQL'e başarıyla kaydedildi (ShopId: {ShopId})", normalizedShopId);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL çevrimdışı, OAuth token SQLite yedeğine kaydediliyor (ShopId: {ShopId})", shopId);
            if (_fallbackStore != null)
                await _fallbackStore.SaveAsync(shopId, token, cancellationToken);
            else
                throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL'e OAuth token kaydedilirken hata oluştu (ShopId: {ShopId}): {Message}", shopId, ex.Message);
            if (_fallbackStore != null)
                await _fallbackStore.SaveAsync(shopId, token, cancellationToken);
            else
                throw;
        }
    }

    private string UnprotectOrRaw(string cipherOrRaw)
    {
        if (string.IsNullOrWhiteSpace(cipherOrRaw)) return string.Empty;
        try
        {
            return _protector.Unprotect(cipherOrRaw);
        }
        catch
        {
            // Şifresiz legacy token ise doğrudan dön
            return cipherOrRaw;
        }
    }
}
