namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Sisteme bağlı Etsy mağaza kartı.
/// </summary>
public class ShopConnection
{
    public string ShopId { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime? LastSynchronizedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public OAuthToken? OAuthToken { get; set; }
    public EtsyAppCredential? AppCredential { get; set; }
    public ShopTelegramSetting? TelegramSetting { get; set; }
    public ShopAiSetting? AiSetting { get; set; }
    public ICollection<SyncState> SyncStates { get; set; } = new List<SyncState>();
    public ICollection<BankPayout> BankPayouts { get; set; } = new List<BankPayout>();
    public ICollection<FinancialTransaction> FinancialTransactions { get; set; } = new List<FinancialTransaction>();
    public ICollection<OrderCost> OrderCosts { get; set; } = new List<OrderCost>();
    public ICollection<MonthlyOrderSummary> MonthlyOrderSummaries { get; set; } = new List<MonthlyOrderSummary>();
    public ICollection<ListingTrafficDaily> ListingTrafficDailies { get; set; } = new List<ListingTrafficDaily>();
    public ICollection<ListingAiAudit> ListingAiAudits { get; set; } = new List<ListingAiAudit>();
    public ICollection<ShopCarrierSession> CarrierSessions { get; set; } = new List<ShopCarrierSession>();
}
