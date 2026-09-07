namespace SimilarProductsWinForms.Services;

public sealed class NotificationSettings
{
    public bool TelegramEnabled { get; set; } = true;
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;

    public bool WhatsAppEnabled { get; set; }
    public string WhatsAppApiUrl { get; set; } = string.Empty;
    public string WhatsAppToken { get; set; } = string.Empty;
    public string WhatsAppPhone { get; set; } = string.Empty;

    public bool NotifyOnNewOrder { get; set; } = true;
    public bool NotifyOnOpportunityFound { get; set; } = true;
    public bool NotifyOnBatchQueueFinished { get; set; } = true;
    public bool NotifyOnAutomationRun { get; set; } = true;
    public bool NotifyOnAbTestWinner { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;

    // ── 🌙 Günlük Gece Finans Raporu Ayarları ────────────────────────────────
    public bool EnableDailyFinancialNightReport { get; set; } = true;
    public string DailyFinancialReportTime { get; set; } = "23:55";
    public bool IncludeAiSummaryInNightReport { get; set; } = true;
    public string LastDailyFinancialReportSentDate { get; set; } = string.Empty;
}
