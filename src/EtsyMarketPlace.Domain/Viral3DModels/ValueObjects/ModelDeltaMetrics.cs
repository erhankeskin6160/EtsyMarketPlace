namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using System;

public record ModelDeltaMetrics
{
    public int DeltaDownloads24h { get; init; }
    public double HourlyVelocity { get; init; }
    public double GrowthRatePercentage { get; init; }
    public bool IsAccelerating => HourlyVelocity > 50 || GrowthRatePercentage > 20.0;
    public DateTime? FirstSeenUtc { get; init; }
    public DateTime? LastSnapshotUtc { get; init; }
    public int HistoricalSnapshotCount { get; init; }

    public static ModelDeltaMetrics Estimated(int currentDownloads, int downloads24h)
    {
        double hourly = Math.Round(downloads24h / 24.0, 1);
        double baseline = Math.Max(1, currentDownloads - downloads24h);
        double growth = Math.Round((downloads24h / baseline) * 100, 1);

        return new ModelDeltaMetrics
        {
            DeltaDownloads24h = downloads24h,
            HourlyVelocity = hourly,
            GrowthRatePercentage = growth,
            HistoricalSnapshotCount = 1,
            FirstSeenUtc = DateTime.UtcNow.AddHours(-24),
            LastSnapshotUtc = DateTime.UtcNow
        };
    }
}
