namespace EtsyMarketPlace.Application.ShopVault.Services;

public sealed record ShopMigrationProgress(
    string StatusMessage,
    int ProcessedCount,
    int TotalCount,
    int SuccessCount,
    int FailedCount,
    double Percent,
    string? CurrentListingTitle = null,
    string? ErrorDetail = null);
