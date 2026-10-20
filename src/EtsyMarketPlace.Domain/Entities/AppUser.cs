namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Sistem kullanıcısı (Admin, StoreOwner vb.) ve AI kullanım kotası.
/// </summary>
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Role { get; set; } = "StoreOwner";
    public string AssignedShopIds { get; set; } = "[]";
    public int MonthlyAiTokenQuota { get; set; } = 500000;
    public int UsedAiTokens { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
