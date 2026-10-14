namespace EtsyMarketPlace.Application.AiUsage;

using System;

public static class AiPeriodHelper
{
    public static (DateTimeOffset? Since, DateTimeOffset? Until) ResolveDateRange(string? period)
    {
        var now = DateTimeOffset.UtcNow;
        var p = (period ?? "30d").Trim().ToLowerInvariant();

        return p switch
        {
            "this_month" or "bu_ay" or "thismonth" => (
                new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
                null
            ),
            "last_month" or "gecen_ay" or "lastmonth" => (
                new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1),
                new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            "90d" or "son_90_gun" or "90days" => (
                now.AddDays(-90),
                null
            ),
            "all" or "tum_zamanlar" or "all_time" => (
                null,
                null
            ),
            _ => (
                now.AddDays(-30),
                null
            )
        };
    }
}
