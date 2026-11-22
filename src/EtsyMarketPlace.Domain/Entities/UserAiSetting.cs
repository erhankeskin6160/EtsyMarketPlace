namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Kullanıcıya özel yapay zeka sağlayıcı tercihleri ve şifreli API anahtarları (PostgreSQL: user_ai_settings).
/// AspNetUsers("Id") ile 1:1 yabancı anahtar (ON DELETE CASCADE) ilişkisine sahiptir.
/// </summary>
public class UserAiSetting
{
    public string UserId { get; set; } = string.Empty;
    public string EncryptedSettingsJson { get; set; } = "{}";
    public string ActiveProvider { get; set; } = "Gemini";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
