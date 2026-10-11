namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Threading;
using System.Threading.Tasks;

public interface IEtsyListingApiClient
{
    Task<(long ListingId, string Url)> CreateDraftListingAsync(
        string shopId,
        CreateDraftListingRawRequest rawRequest,
        CancellationToken cancellationToken = default);

    Task<bool> UploadListingImageAsync(
        string shopId,
        long listingId,
        byte[] imageBytes,
        string fileName,
        int rank,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> UpdateListingInventoryAsync(
        string shopId,
        long listingId,
        EtsyInventoryPayload payload,
        CancellationToken cancellationToken = default);

    Task<bool> SetListingStateAsync(
        string shopId,
        long listingId,
        string state,
        CancellationToken cancellationToken = default);

    Task<long?> GetDefaultShippingProfileIdAsync(
        string shopId,
        CancellationToken cancellationToken = default);

    Task<long?> GetDefaultReadinessStateIdAsync(
        string shopId,
        CancellationToken cancellationToken = default);
}
