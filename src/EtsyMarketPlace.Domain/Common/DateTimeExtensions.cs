namespace EtsyMarketPlace.Domain.Common;

using System;

public static class DateTimeExtensions
{
    public static DateTimeOffset ToUtcOffset(this DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public static DateTimeOffset? ToUtcOffset(this DateTime? dt)
    {
        return dt.HasValue ? dt.Value.ToUtcOffset() : null;
    }
}
