namespace EtsyMarketPlace.Infrastructure.Http;

public sealed class ApiResilienceOptions
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan MinimumRequestInterval { get; init; } = TimeSpan.FromMilliseconds(220);
    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(15);
    public bool UseSharedLimiter { get; init; } = true;
}

public sealed record ApiRequestEvent(
    DateTimeOffset OccurredAt,
    string Method,
    string RequestUri,
    int Attempt,
    int? StatusCode,
    int QueueDepth,
    TimeSpan Delay,
    string Message);

public static class ApiResilienceTelemetry
{
    private static readonly object Sync = new();
    private static ApiRequestEvent? _lastEvent;

    public static ApiRequestEvent? LastEvent
    {
        get { lock (Sync) return _lastEvent; }
    }

    public static event Action<ApiRequestEvent>? EventPublished;

    internal static void Publish(ApiRequestEvent item)
    {
        lock (Sync) _lastEvent = item;
        EventPublished?.Invoke(item);
    }
}
