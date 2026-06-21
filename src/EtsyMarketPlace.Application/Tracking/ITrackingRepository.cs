namespace EtsyMarketPlace.Application.Tracking;

using EtsyMarketPlace.Domain.Tracking;

public interface ITrackingRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<TrackingItem> SaveCaptureAsync(TrackingCapture capture, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingItem>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingSnapshot>> GetSnapshotsAsync(long trackingItemId, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(long trackingItemId, CancellationToken cancellationToken = default);
}
