namespace EtsyMarketPlace.Application.Tracking;

using EtsyMarketPlace.Domain.Tracking;

public sealed class TrackingService(ITrackingRepository repository)
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        repository.InitializeAsync(cancellationToken);

    public async Task<TrackingItem> TrackAsync(TrackingCapture capture, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capture.ExternalKey))
        {
            throw new ArgumentException("Takip anahtari bos olamaz.", nameof(capture));
        }

        if (string.IsNullOrWhiteSpace(capture.DisplayName))
        {
            throw new ArgumentException("Takip adi bos olamaz.", nameof(capture));
        }

        return await repository.SaveCaptureAsync(capture, cancellationToken);
    }

    public async Task<IReadOnlyList<TrackingHistory>> GetHistoriesAsync(CancellationToken cancellationToken = default)
    {
        var items = await repository.GetItemsAsync(cancellationToken);
        var histories = new List<TrackingHistory>(items.Count);
        foreach (var item in items)
        {
            var snapshots = await repository.GetSnapshotsAsync(item.Id, cancellationToken);
            histories.Add(new TrackingHistory(item, snapshots));
        }

        return histories
            .OrderByDescending(item => item.Latest?.CapturedAt ?? item.Item.CreatedAt)
            .ToList();
    }

    public Task DeleteAsync(long trackingItemId, CancellationToken cancellationToken = default) =>
        repository.DeleteItemAsync(trackingItemId, cancellationToken);
}
