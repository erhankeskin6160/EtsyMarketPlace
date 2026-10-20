namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Kullanıcı ve sistem hareketleri denetim kaydı.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
