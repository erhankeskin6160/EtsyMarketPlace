using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AppUserRecord = EtsyMarketPlace.Application.Auth.AppUser;

namespace EtsyMarketPlace.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity (UserManager / RoleManager) tabanlı kimlik doğrulama,
/// kayıt, çıkış (SecurityStamp döndürme) ve token geçerlilik doğrulama servisi.
/// PostgreSQL erişilemez olduğunda yerel SQLite yedeğine düşer.
/// </summary>
public sealed class IdentityAuthService : IIdentityAuthService
{
    private static readonly TimeSpan StampCacheDuration = TimeSpan.FromSeconds(30);

    private readonly UserManager<ApplicationIdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly EtsyMarketPlaceDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IdentityAuthService> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    private sealed record CachedSecurityStampState(string? SecurityStamp, bool IsActive);

    public IdentityAuthService(
        UserManager<ApplicationIdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        EtsyMarketPlaceDbContext db,
        IMemoryCache cache,
        ILogger<IdentityAuthService> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackStore = fallbackStore;
    }

    public async Task<IdentityOperationResult<AppUserRecord>> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        string? remoteIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrEmpty(password))
        {
            return IdentityOperationResult<AppUserRecord>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized);
        }

        try
        {
            var identifier = usernameOrEmail.Trim();
            var user = await _userManager.FindByNameAsync(identifier)
                ?? await _userManager.FindByEmailAsync(identifier);

            if (user is null)
            {
                return IdentityOperationResult<AppUserRecord>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized);
            }

            if (!user.IsActive)
            {
                return IdentityOperationResult<AppUserRecord>.Fail(
                    "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.",
                    HttpStatusCode.Forbidden);
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return IdentityOperationResult<AppUserRecord>.Fail(
                    "Çok fazla hatalı giriş denemesi nedeniyle hesabınız geçici olarak kilitlenmiştir. Lütfen birkaç dakika sonra tekrar deneyin.",
                    HttpStatusCode.Locked);
            }

            var passwordValid = false;
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                passwordValid = await _userManager.CheckPasswordAsync(user, password);
            }

            // Eski app_users tablosundan göç edilen PBKDF2 şifrelerini ilk girişte şeffaf biçimde Identity formatına yükselt
            if (!passwordValid &&
                !string.IsNullOrWhiteSpace(user.LegacyPasswordHash) &&
                !string.IsNullOrWhiteSpace(user.LegacyPasswordSalt) &&
                PasswordHasher.VerifyPassword(password, user.LegacyPasswordHash, user.LegacyPasswordSalt))
            {
                passwordValid = true;
                user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, password);
                user.LegacyPasswordHash = null;
                user.LegacyPasswordSalt = null;
                if (string.IsNullOrWhiteSpace(user.SecurityStamp))
                {
                    user.SecurityStamp = Guid.NewGuid().ToString("N");
                }

                var upgradeRes = await _userManager.UpdateAsync(user);
                if (upgradeRes.Succeeded)
                {
                    _db.AuditLogs.Add(new AuditLog
                    {
                        UserId = user.Id,
                        Username = user.UserName ?? identifier,
                        Action = "PasswordMigratedToIdentity",
                        Details = "Eski PBKDF2 parolası şeffaf biçimde ASP.NET Core Identity formatına yükseltildi.",
                        IpAddress = remoteIpAddress,
                        Timestamp = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Kullanıcı {Username} parolası ASP.NET Core Identity formatına yükseltildi.", user.UserName);
                }
            }

            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);
                return IdentityOperationResult<AppUserRecord>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized);
            }

            if (user.AccessFailedCount > 0)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            if (string.IsNullOrWhiteSpace(user.SecurityStamp))
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            var roles = await _userManager.GetRolesAsync(user);
            _cache.Set(
                IdentityUserMapper.GetStampCacheKey(user.Id),
                new CachedSecurityStampState(user.SecurityStamp, user.IsActive),
                StampCacheDuration);

            return IdentityOperationResult<AppUserRecord>.Ok(IdentityUserMapper.ToRecord(user, roles));
        }
        catch (Exception ex) when (_fallbackStore is not null && IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL Identity giriş başarısız; SQLite yedeği kullanılıyor: {Message}", ex.Message);
            var fallbackUser = await _fallbackStore.GetByUsernameOrEmailAsync(usernameOrEmail, cancellationToken);
            if (fallbackUser is null || !PasswordHasher.VerifyPassword(password, fallbackUser.PasswordHash, fallbackUser.PasswordSalt))
            {
                return IdentityOperationResult<AppUserRecord>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized);
            }

            if (!fallbackUser.IsActive)
            {
                return IdentityOperationResult<AppUserRecord>.Fail(
                    "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.",
                    HttpStatusCode.Forbidden);
            }

            return IdentityOperationResult<AppUserRecord>.Ok(fallbackUser with { SecurityStamp = fallbackUser.SecurityStamp ?? "sqlite-fallback" });
        }
    }

    public async Task<IdentityOperationResult<AppUserRecord>> RegisterAsync(
        string username,
        string email,
        string password,
        string? shopId,
        CancellationToken cancellationToken = default)
    {
        var cleanUsername = username.Trim();
        var cleanEmail = email.Trim().ToLowerInvariant();
        var assignedShops = string.IsNullOrWhiteSpace(shopId)
            ? new List<string> { "53236321" }
            : new List<string> { shopId.Trim() };
        var starterPlan = SubscriptionFeatureKeys.GetDefaultPlans().First(p => p.Id == SubscriptionPlanCode.Starter);
        var now = DateTime.UtcNow;

        try
        {
            var existingByName = await _userManager.FindByNameAsync(cleanUsername);
            var existingByEmail = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingByName is not null || existingByEmail is not null)
            {
                return IdentityOperationResult<AppUserRecord>.Fail(
                    "Bu kullanıcı adı veya e-posta zaten kullanımda.",
                    HttpStatusCode.Conflict);
            }

            var identityUser = new ApplicationIdentityUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = cleanUsername,
                Email = cleanEmail,
                EmailConfirmed = true,
                PrimaryRole = UserRoles.StoreOwner,
                AssignedShopIds = JsonSerializer.Serialize(assignedShops),
                MonthlyAiTokenQuota = starterPlan.MonthlyAiTokenQuota,
                UsedAiTokens = 0,
                IsActive = true,
                SubscriptionPlanId = SubscriptionPlanCode.Starter,
                SubscriptionStatus = SubscriptionStatus.Active.ToString(),
                SubscriptionExpiresAt = now.AddMonths(1),
                CreatedAt = now,
                LastLoginAt = now,
                LockoutEnabled = true,
                AccessFailedCount = 0
            };

            var createResult = await _userManager.CreateAsync(identityUser, password);
            if (!createResult.Succeeded)
            {
                var errorMsg = TranslateIdentityErrors(createResult.Errors);
                return IdentityOperationResult<AppUserRecord>.Fail(errorMsg, HttpStatusCode.BadRequest);
            }

            if (!await _roleManager.RoleExistsAsync(UserRoles.StoreOwner))
            {
                await _roleManager.CreateAsync(new IdentityRole(UserRoles.StoreOwner));
            }
            await _userManager.AddToRoleAsync(identityUser, UserRoles.StoreOwner);

            var roles = await _userManager.GetRolesAsync(identityUser);
            return IdentityOperationResult<AppUserRecord>.Ok(IdentityUserMapper.ToRecord(identityUser, roles));
        }
        catch (Exception ex) when (_fallbackStore is not null && IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL Identity kayıt başarısız; SQLite yedeği kullanılıyor: {Message}", ex.Message);
            var existing = await _fallbackStore.GetByUsernameOrEmailAsync(cleanUsername, cancellationToken)
                ?? await _fallbackStore.GetByUsernameOrEmailAsync(cleanEmail, cancellationToken);
            if (existing is not null)
            {
                return IdentityOperationResult<AppUserRecord>.Fail(
                    "Bu kullanıcı adı veya e-posta zaten kullanımda.",
                    HttpStatusCode.Conflict);
            }

            var (hash, salt) = PasswordHasher.HashPassword(password);
            var fallbackRecord = new AppUserRecord(
                Guid.NewGuid().ToString("N"),
                cleanUsername,
                cleanEmail,
                hash,
                salt,
                UserRoles.StoreOwner,
                assignedShops,
                starterPlan.MonthlyAiTokenQuota,
                0,
                true,
                new DateTimeOffset(now, TimeSpan.Zero),
                new DateTimeOffset(now, TimeSpan.Zero),
                SubscriptionPlanCode.Starter,
                SubscriptionStatus.Active.ToString(),
                new DateTimeOffset(now.AddMonths(1), TimeSpan.Zero),
                "sqlite-fallback");

            await _fallbackStore.CreateUserAsync(fallbackRecord, cancellationToken);
            return IdentityOperationResult<AppUserRecord>.Ok(fallbackRecord);
        }
    }

    public async Task<bool> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        InvalidateSecurityStampCache(userId);

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return true;

            var result = await _userManager.UpdateSecurityStampAsync(user);
            InvalidateSecurityStampCache(userId);
            return result.Succeeded;
        }
        catch (Exception ex) when (_fallbackStore is not null && IsDatabaseUnavailableException(ex))
        {
            _logger.LogWarning(ex, "PostgreSQL Identity logout sırasında erişilemez; cache temizlendi: {Message}", ex.Message);
            return true;
        }
    }

    public async Task<bool> ValidateSecurityStampAsync(
        string userId,
        string? securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(securityStamp))
        {
            return false;
        }

        var cacheKey = IdentityUserMapper.GetStampCacheKey(userId);
        if (_cache.TryGetValue<CachedSecurityStampState>(cacheKey, out var cached) && cached is not null)
        {
            return cached.IsActive &&
                   !string.IsNullOrWhiteSpace(cached.SecurityStamp) &&
                   string.Equals(cached.SecurityStamp, securityStamp, StringComparison.Ordinal);
        }

        try
        {
            var state = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new CachedSecurityStampState(u.SecurityStamp, u.IsActive))
                .FirstOrDefaultAsync(cancellationToken);

            if (state is null)
            {
                return false;
            }

            _cache.Set(cacheKey, state, StampCacheDuration);
            return state.IsActive &&
                   !string.IsNullOrWhiteSpace(state.SecurityStamp) &&
                   string.Equals(state.SecurityStamp, securityStamp, StringComparison.Ordinal);
        }
        catch (Exception ex) when (_fallbackStore is not null && IsDatabaseUnavailableException(ex))
        {
            var fallbackUser = await _fallbackStore.GetByIdAsync(userId, cancellationToken);
            return fallbackUser is not null && fallbackUser.IsActive;
        }
    }

    public void InvalidateSecurityStampCache(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _cache.Remove(IdentityUserMapper.GetStampCacheKey(userId));
        }
    }

    private static string TranslateIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var messages = new List<string>();
        foreach (var err in errors)
        {
            messages.Add(err.Code switch
            {
                "DuplicateUserName" => "Bu kullanıcı adı zaten kullanımda.",
                "DuplicateEmail" => "Bu e-posta adresi zaten kullanımda.",
                "PasswordTooShort" => "Parola en az 6 karakter olmalıdır.",
                "PasswordRequiresDigit" => "Parola en az bir rakam (0-9) içermelidir.",
                "PasswordRequiresLower" => "Parola en az bir küçük harf (a-z) içermelidir.",
                "PasswordRequiresUpper" => "Parola en az bir büyük harf (A-Z) içermelidir.",
                "PasswordRequiresNonAlphanumeric" => "Parola en az bir özel karakter içermelidir.",
                "InvalidUserName" => "Geçersiz kullanıcı adı karakterleri içeriyor.",
                "InvalidEmail" => "Geçersiz e-posta adresi formatı.",
                _ => err.Description
            });
        }

        return messages.Count > 0 ? string.Join(" ", messages) : "Kullanıcı kaydı oluşturulamadı.";
    }

    private static bool IsDatabaseUnavailableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException ||
                current is System.Net.Sockets.SocketException ||
                current is TimeoutException ||
                current.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
