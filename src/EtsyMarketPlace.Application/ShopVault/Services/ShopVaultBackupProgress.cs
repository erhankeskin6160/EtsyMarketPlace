namespace EtsyMarketPlace.Application.ShopVault.Services;

public sealed record ShopVaultBackupProgress(
    string StatusMessage,
    int ProcessedListings,
    int TotalListings,
    int ProcessedImages,
    int TotalImages,
    double Percent);
