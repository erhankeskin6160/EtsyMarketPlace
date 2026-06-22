namespace EtsyMarketPlace.Application.Tests;

using System.Net;
using System.Net.Http.Headers;
using EtsyMarketPlace.Infrastructure.Http;
using Xunit;

public sealed class ApiResilienceHandlerTests
{
    [Fact]
    public async Task Get_429UsesRetryAfterAndReturnsSecondResponse()
    {
        var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
        var inner = new SequenceHandler(rateLimited, new HttpResponseMessage(HttpStatusCode.OK));
        var delays = new List<TimeSpan>();
        using var client = CreateClient(inner, delays);

        using var response = await client.GetAsync("https://example.test/listings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.RequestCount);
        Assert.Contains(TimeSpan.FromSeconds(2), delays);
    }

    [Fact]
    public async Task Get_503UsesExponentialBackoff()
    {
        var inner = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));
        var delays = new List<TimeSpan>();
        using var client = CreateClient(inner, delays);

        using var response = await client.GetAsync("https://example.test/shops");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.RequestCount);
        Assert.Contains(TimeSpan.FromSeconds(1), delays);
    }

    [Fact]
    public async Task Get_400DoesNotRetry()
    {
        var inner = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var delays = new List<TimeSpan>();
        using var client = CreateClient(inner, delays);

        using var response = await client.GetAsync("https://example.test/invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, inner.RequestCount);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Post_503DoesNotRetry()
    {
        var inner = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var delays = new List<TimeSpan>();
        using var client = CreateClient(inner, delays);

        using var response = await client.PostAsync("https://example.test/oauth", new StringContent("code=one-time"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, inner.RequestCount);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task ConcurrentRequests_AreSerializedByRateLimitQueue()
    {
        var inner = new ConcurrencyHandler();
        using var client = CreateClient(inner, []);

        var first = client.GetAsync("https://example.test/first");
        await inner.FirstRequestEntered;
        var second = client.GetAsync("https://example.test/second");
        await Task.Delay(50);

        Assert.Equal(1, inner.MaximumConcurrency);
        inner.ReleaseFirstRequest();
        await Task.WhenAll(first, second);
        Assert.Equal(1, inner.MaximumConcurrency);
        Assert.Equal(2, inner.RequestCount);
    }

    private static HttpClient CreateClient(HttpMessageHandler inner, ICollection<TimeSpan> delays)
    {
        var options = new ApiResilienceOptions
        {
            MaxRetries = 3,
            MinimumRequestInterval = TimeSpan.Zero,
            BaseRetryDelay = TimeSpan.FromSeconds(1),
            MaximumRetryDelay = TimeSpan.FromSeconds(10),
            UseSharedLimiter = false,
        };
        var handler = new ApiResilienceHandler(
            inner,
            options,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            () => new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero));
        return new HttpClient(handler);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0) throw new InvalidOperationException("Sahte HTTP yaniti kalmadi.");
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ConcurrencyHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeRequests;
        private int _requestCount;
        private int _maximumConcurrency;

        public Task FirstRequestEntered => _firstEntered.Task;
        public int RequestCount => Volatile.Read(ref _requestCount);
        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public void ReleaseFirstRequest() => _releaseFirst.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            var active = Interlocked.Increment(ref _activeRequests);
            InterlockedExtensions.Max(ref _maximumConcurrency, active);
            try
            {
                if (requestNumber == 1)
                {
                    _firstEntered.TrySetResult();
                    await _releaseFirst.Task.WaitAsync(cancellationToken);
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            var current = Volatile.Read(ref location);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref location, value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }
}
