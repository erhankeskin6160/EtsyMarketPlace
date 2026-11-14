namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed record EtsyRateLimitSnapshot(
    string ShopId,
    int? DailyLimit,
    int? RemainingToday,
    int? PerSecondLimit,
    int? RemainingThisSecond,
    long? RetryAfterSeconds,
    bool IsDailyLimitExceeded,
    string? LastErrorMessage,
    DateTimeOffset UpdatedAt);

public interface IEtsyRateLimitStatusProvider
{
    EtsyRateLimitSnapshot? GetSnapshot(string shopId);
}
