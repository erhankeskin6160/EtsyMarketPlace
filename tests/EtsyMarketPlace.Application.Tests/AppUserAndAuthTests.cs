using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class AppUserAndAuthTests : IDisposable
{
    private readonly string _tempDbPath;

    public AppUserAndAuthTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"etsy_auth_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }
        catch { }
    }

    [Fact]
    public void PasswordHasher_ShouldHashAndVerifySuccessfully()
    {
        // Arrange
        const string password = "MySecretPassword123!";

        // Act
        var (hash, salt) = PasswordHasher.HashPassword(password);
        var isValid = PasswordHasher.VerifyPassword(password, hash, salt);
        var isInvalid = PasswordHasher.VerifyPassword("WrongPassword", hash, salt);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));
        Assert.True(isValid);
        Assert.False(isInvalid);
    }

    [Fact]
    public void JwtTokenService_ShouldGenerateAndValidateTokenCorrectly()
    {
        // Arrange
        var jwt = new JwtTokenService("Test_Enterprise_Secret_Key_1234567890!");
        var user = new AppUser(
            "user-123",
            "erhankeskin",
            "erhan@test.com",
            "dummyHash",
            "dummySalt",
            UserRoles.Admin,
            new List<string> { "53236321", "99887766" },
            1_000_000,
            12_500,
            true,
            DateTimeOffset.UtcNow,
            null);

        // Act
        var token = jwt.GenerateToken(user);
        var (isValid, principal, userId, role, username) = jwt.ValidateToken(token);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(isValid);
        Assert.NotNull(principal);
        Assert.Equal("user-123", userId);
        Assert.Equal(UserRoles.Admin, role);
        Assert.Equal("erhankeskin", username);
    }

    [Fact]
    public void JwtTokenService_ShouldRejectTamperedToken()
    {
        // Arrange
        var jwt = new JwtTokenService("Test_Enterprise_Secret_Key_1234567890!");
        var user = new AppUser(
            "user-tamper",
            "tamperuser",
            "tamper@test.com",
            "dummyHash",
            "dummySalt",
            UserRoles.StoreOwner,
            new List<string> { "53236321" },
            500_000,
            0,
            true,
            DateTimeOffset.UtcNow,
            null);

        var token = jwt.GenerateToken(user);
        var tamperedToken = token[..^4] + "ABCD";

        // Act
        var (isValid, _, _, _, _) = jwt.ValidateToken(tamperedToken);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void JwtTokenService_ShouldRejectExpiredToken()
    {
        // Arrange
        var jwt = new JwtTokenService("Test_Enterprise_Secret_Key_1234567890!");
        var user = new AppUser(
            "user-expired",
            "expireduser",
            "exp@test.com",
            "dummyHash",
            "dummySalt",
            UserRoles.StoreOwner,
            new List<string> { "53236321" },
            500_000,
            0,
            true,
            DateTimeOffset.UtcNow,
            null);

        // Generate token with negative lifetime (already expired)
        var expiredToken = jwt.GenerateToken(user, TimeSpan.FromSeconds(-10));

        // Act
        var (isValid, _, _, _, _) = jwt.ValidateToken(expiredToken);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task SqliteStore_ShouldSeedDefaultAdminAndSupportUserOperations()
    {
        // Arrange
        var options = Options.Create(new EtsyIntegrationOptions { DatabasePath = _tempDbPath });
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        await using var store = new SqliteEtsyIntegrationStore(options, dataProtectionProvider);

        // Act 1: Initialize should seed admin
        await store.InitializeAsync();
        var adminUser = await store.GetByUsernameOrEmailAsync("admin");

        // Assert 1: Default admin exists
        Assert.NotNull(adminUser);
        Assert.Equal("admin", adminUser.Username);
        Assert.Equal(UserRoles.Admin, adminUser.Role);
        Assert.True(PasswordHasher.VerifyPassword("Admin123*!", adminUser.PasswordHash, adminUser.PasswordSalt));
        Assert.Contains("53236321", adminUser.AssignedShopIds);

        // Act 2: Create a second user
        var (hash, salt) = PasswordHasher.HashPassword("UserPassword123!");
        var newUser = new AppUser(
            Guid.NewGuid().ToString(),
            "seller1",
            "seller1@etsy.com",
            hash,
            salt,
            UserRoles.StoreOwner,
            new List<string> { "99881122" },
            500_000,
            0,
            true,
            DateTimeOffset.UtcNow,
            null);

        await store.CreateUserAsync(newUser);

        var retrievedUser = await store.GetByUsernameOrEmailAsync("seller1@etsy.com");
        Assert.NotNull(retrievedUser);
        Assert.Equal("seller1", retrievedUser.Username);
        Assert.Equal(UserRoles.StoreOwner, retrievedUser.Role);

        // Act 3: Update user
        var updated = retrievedUser with { Role = UserRoles.Admin, MonthlyAiTokenQuota = 750_000 };
        await store.UpdateUserAsync(updated);

        var afterUpdate = await store.GetByIdAsync(newUser.Id);
        Assert.NotNull(afterUpdate);
        Assert.Equal(UserRoles.Admin, afterUpdate.Role);
        Assert.Equal(750_000, afterUpdate.MonthlyAiTokenQuota);

        // Act 4: Audit log
        await store.AddAuditLogAsync(new AuditLogEntry(0, newUser.Id, "seller1", "TestLogin", "Test audit log details", "127.0.0.1", DateTimeOffset.UtcNow));
        var logs = await store.GetAuditLogsAsync(10);

        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Action == "TestLogin" && l.Username == "seller1");
    }
}
