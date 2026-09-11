namespace EtsyMarketPlace.Domain.ShopVault.Interfaces;

using EtsyMarketPlace.Domain.ShopVault.Entities;

public interface IShopVaultRepository
{
    Task<List<VaultBackupSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default);
    Task<VaultBackupSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SaveSessionAsync(VaultBackupSession session, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<List<VaultListing>> GetListingsBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<VaultListing?> GetListingAsync(string sessionId, long listingId, CancellationToken cancellationToken = default);
    Task SaveListingsAsync(IEnumerable<VaultListing> listings, CancellationToken cancellationToken = default);
    Task UpdateListingAsync(VaultListing listing, CancellationToken cancellationToken = default);
    Task DeleteListingAsync(string sessionId, long listingId, CancellationToken cancellationToken = default);

    Task<int> GetListingCountBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
}
