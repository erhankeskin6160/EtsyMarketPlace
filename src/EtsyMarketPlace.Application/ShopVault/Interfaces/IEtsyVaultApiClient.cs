namespace EtsyMarketPlace.Application.ShopVault.Interfaces;

using EtsyMarketPlace.Domain.ShopVault.Entities;

public interface IEtsyVaultApiClient
{
    Task<(long ShopId, string ShopName, string ShopUrl)> GetCurrentShopInfoAsync(CancellationToken cancellationToken = default);
    Task<List<VaultListing>> FetchAllShopListingsPagedAsync(string state = "active", IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default);
    Task<long> CreateDraftListingAsync(VaultListing listing, long targetShopId, CancellationToken cancellationToken = default);
    Task UploadListingImageAsync(long targetShopId, long listingId, string imagePath, int rank, CancellationToken cancellationToken = default);
    Task UpdateListingInventoryAsync(long targetShopId, long listingId, List<VaultListingVariation> variations, CancellationToken cancellationToken = default);
}
