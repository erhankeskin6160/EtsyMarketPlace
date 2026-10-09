namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyOAuthService
{
    Task<string> CreateAuthorizationUrlAsync(string userId, string state, CancellationToken cancellationToken = default);
    Task<EtsyOAuthToken> ExchangeCodeAsync(string shopId, string code, string codeVerifier, CancellationToken cancellationToken = default);
    Task<EtsyOAuthToken> RefreshTokenAsync(string shopId, string refreshToken, CancellationToken cancellationToken = default);
}

public interface IEtsyTokenStore
{
    Task<EtsyOAuthToken?> GetAsync(string shopId, CancellationToken cancellationToken = default);
    Task SaveAsync(string shopId, EtsyOAuthToken token, CancellationToken cancellationToken = default);
}

public interface IEtsyDataClient
{
    Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyFinancialTransaction>> GetFinancialTransactionsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default);

    /// <summary>Web kontrol paneli: magaza fislerini (siparisleri) canli Etsy API'den ceker.</summary>
    Task<IReadOnlyList<EtsyDashboardReceipt>> GetShopReceiptsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);

    /// <summary>Web kontrol paneli: odeme hesabi defter kayitlarini (dis reklam kesintileri eslesmesi icin) ceker.</summary>
    Task<IReadOnlyList<EtsyDashboardLedgerFee>> GetPaymentAccountLedgerEntriesAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);

    /// <summary>Sunucu finans motoru: tam alanli odeme hesabi defter kayitlari (siniflandirma + USD donusumu ile).</summary>
    Task<IReadOnlyList<EtsyLedgerEntryDetail>> GetLedgerEntriesDetailedAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
}

public interface IEtsyFinancialReportService
{
    Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default);
}

public interface IEtsySynchronizationService
{
    Task<EtsySyncResult> SynchronizeAsync(string shopId, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default);
}
