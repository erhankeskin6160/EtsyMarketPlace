namespace EtsyMarketPlace.Infrastructure.Http;

using System.Net;

public sealed class ApiResilienceHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim SharedGate = new(1, 1);
    private static DateTimeOffset _sharedLastRequestAt = DateTimeOffset.MinValue;
    private static int _sharedQueueDepth;

    private readonly ApiResilienceOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _instanceGate = new(1, 1);
    private DateTimeOffset _instanceLastRequestAt = DateTimeOffset.MinValue;
    private int _instanceQueueDepth;

    public ApiResilienceHandler(
        HttpMessageHandler innerHandler,
        ApiResilienceOptions? options = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<DateTimeOffset>? utcNow = null)
        : base(innerHandler)
    {
        _options = options ?? new ApiResilienceOptions();
        _delay = delay ?? Task.Delay;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var retryAllowed = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        var maximumAttempts = retryAllowed ? _options.MaxRetries + 1 : 1;

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var attemptRequest = await CloneRequestAsync(request, cancellationToken);
            try
            {
                var response = await SendRateLimitedAsync(attemptRequest, attempt, cancellationToken);
                if (!ShouldRetry(response.StatusCode) || attempt == maximumAttempts)
                {
                    Publish(request, attempt, (int)response.StatusCode, TimeSpan.Zero,
                        attempt > 1 ? "Istek yeniden deneme sonrasinda tamamlandi." : "Istek tamamlandi.");
                    return response;
                }

                var retryDelay = CalculateRetryDelay(response, attempt);
                Publish(request, attempt, (int)response.StatusCode, retryDelay,
                    $"Gecici HTTP {(int)response.StatusCode}; yeniden denenecek.");
                response.Dispose();
                attemptRequest.Dispose();
                await _delay(retryDelay, cancellationToken);
            }
            catch (HttpRequestException) when (retryAllowed && attempt < maximumAttempts)
            {
                attemptRequest.Dispose();
                var retryDelay = CalculateBackoff(attempt);
                Publish(request, attempt, null, retryDelay, "Ag hatasi; yeniden denenecek.");
                await _delay(retryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("API yeniden deneme dongusu beklenmedik bicimde sonlandi.");
    }

    private async Task<HttpResponseMessage> SendRateLimitedAsync(
        HttpRequestMessage request,
        int attempt,
        CancellationToken cancellationToken)
    {
        var queueDepth = IncrementQueueDepth();
        if (queueDepth > 1)
            Publish(request, attempt, null, TimeSpan.Zero, "Istek API hiz kuyrugunda bekliyor.");

        var gate = _options.UseSharedLimiter ? SharedGate : _instanceGate;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var lastRequestAt = _options.UseSharedLimiter ? _sharedLastRequestAt : _instanceLastRequestAt;
            var elapsed = _utcNow() - lastRequestAt;
            var throttleDelay = _options.MinimumRequestInterval - elapsed;
            if (throttleDelay > TimeSpan.Zero)
            {
                Publish(request, attempt, null, throttleDelay, "API hiz siniri icin bekleniyor.");
                await _delay(throttleDelay, cancellationToken);
            }

            if (_options.UseSharedLimiter) _sharedLastRequestAt = _utcNow();
            else _instanceLastRequestAt = _utcNow();
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
            DecrementQueueDepth();
        }
    }

    private TimeSpan CalculateRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return Min(delta, _options.MaximumRetryDelay);
        if (retryAfter?.Date is { } date)
        {
            var duration = date - _utcNow();
            if (duration > TimeSpan.Zero) return Min(duration, _options.MaximumRetryDelay);
        }
        return CalculateBackoff(attempt);
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var milliseconds = _options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        return Min(TimeSpan.FromMilliseconds(milliseconds), _options.MaximumRetryDelay);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        if (request.Content is not null)
        {
            var content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }
        return clone;
    }

    private void Publish(
        HttpRequestMessage request,
        int attempt,
        int? statusCode,
        TimeSpan delay,
        string message) =>
        ApiResilienceTelemetry.Publish(new ApiRequestEvent(
            _utcNow(),
            request.Method.Method,
            request.RequestUri?.ToString() ?? "",
            attempt,
            statusCode,
            CurrentQueueDepth(),
            delay,
            message));

    private int IncrementQueueDepth() => _options.UseSharedLimiter
        ? Interlocked.Increment(ref _sharedQueueDepth)
        : Interlocked.Increment(ref _instanceQueueDepth);

    private void DecrementQueueDepth()
    {
        if (_options.UseSharedLimiter) Interlocked.Decrement(ref _sharedQueueDepth);
        else Interlocked.Decrement(ref _instanceQueueDepth);
    }

    private int CurrentQueueDepth() => _options.UseSharedLimiter
        ? Volatile.Read(ref _sharedQueueDepth)
        : Volatile.Read(ref _instanceQueueDepth);

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
}
