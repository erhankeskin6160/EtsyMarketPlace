using System;
using Microsoft.AspNetCore.Identity;

namespace EtsyMarketPlace.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity tabanlı uygulama kullanıcı varlığı.
/// Clean Architecture gereği Domain katmanını dış bağımlılıklardan uzak tutmak için
/// Infrastructure katmanında tanımlanmıştır.
/// </summary>
public class ApplicationIdentityUser : IdentityUser
{
    public string PrimaryRole { get; set; } = "StoreOwner";
    public string AssignedShopIds { get; set; } = "[]";
    public int MonthlyAiTokenQuota { get; set; } = 500000;
    public int UsedAiTokens { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string SubscriptionPlanId { get; set; } = "pro";
    public string SubscriptionStatus { get; set; } = "Active";
    public DateTime? SubscriptionExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Eski app_users tablosundan göç edilen kullanıcıların ilk girişte şifrelerini
    /// şeffaf biçimde ASP.NET Core Identity hash formatına yükseltmek için geçici alanlar.
    /// Yükseltme tamamlandığında null olarak temizlenir.
    /// </summary>
    public string? LegacyPasswordHash { get; set; }
    public string? LegacyPasswordSalt { get; set; }
}
