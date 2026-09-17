namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.AiUsage;
using Xunit;

public sealed class GeminiRateLimitTests
{
    [Fact]
    public void BuildFromHistory_WithEmptyRecords_ProvidesDefaultsMatchingFreeTier()
    {
        var models = new List<string> { "gemini-3.7-flash", "gemini-2.5-flash", "gemini-1.5-flash" };
        var records = new List<AiUsageRecord>();

        var report = GeminiRateLimitReport.BuildFromHistory(models, records, days: 28, projectName: "test-user-proj");

        Assert.NotNull(report);
        Assert.Equal("test-user-proj", report.ProjectName);
        Assert.Equal("Free Tier", report.Tier);
        Assert.Equal("Son 28 Gün", report.TimeRange);
        Assert.NotEmpty(report.Models);
        Assert.Equal(28, report.DailyTrends.Count);

        var model37 = report.Models.FirstOrDefault(m => m.ModelName.Contains("3.7"));
        Assert.NotNull(model37);
        Assert.Equal(5, model37.LimitRpm);
        Assert.Equal(250_000, model37.LimitTpm);
        Assert.Equal(20, model37.LimitRpd);

        // Crucial: A user with empty records must have 0 peak usage, not simulated hardcoded numbers
        Assert.Equal(0, model37.PeakRpm);
        Assert.Equal(0, model37.PeakTpm);
        Assert.Equal(0, model37.PeakRpd);
        Assert.Equal(0, model37.CurrentRpd);
        Assert.False(model37.IsCritical);
        Assert.Equal(0, report.TotalCriticalOverQuotaModels);
        Assert.All(report.DailyTrends, t => Assert.Equal(0, t.RequestCount));
    }

    [Fact]
    public void BuildFromHistory_DetectsExceededRpmAndRpd()
    {
        var models = new List<string> { "gemini-3.7-flash" };
        var records = new List<AiUsageRecord>();

        // Use a fixed timestamp exactly at the start of a minute (e.g. 12:00:00 UTC today)
        var todayNoon = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 12, 0, 0, TimeSpan.Zero);

        // 7 requests within the exact same minute (12:00:01 to 12:00:13) to safely exceed 5 RPM limit
        for (int i = 0; i < 7; i++)
        {
            records.Add(new AiUsageRecord
            {
                Provider = "Google Gemini",
                ModelName = "gemini-3.7-flash",
                Timestamp = todayNoon.AddSeconds(i * 2),
                TotalTokens = 1500,
                Status = "Başarılı"
            });
        }

        // Add 18 more requests today in distinct minutes to reach 25 total requests today (exceeds 20 RPD)
        for (int i = 0; i < 18; i++)
        {
            records.Add(new AiUsageRecord
            {
                Provider = "Google Gemini",
                ModelName = "gemini-3.7-flash",
                Timestamp = todayNoon.AddMinutes(i + 1),
                TotalTokens = 800,
                Status = "Başarılı"
            });
        }

        var report = GeminiRateLimitReport.BuildFromHistory(models, records, days: 28);

        var model37 = report.Models.First(m => m.ModelName.Contains("3.7"));
        Assert.Equal(7, model37.PeakRpm);
        Assert.True(model37.IsExceededRpm, "Peak RPM should exceed 5");

        Assert.Equal(25, model37.PeakRpd);
        Assert.True(model37.IsExceededRpd, "Peak RPD should exceed 20");
        Assert.True(model37.IsCritical);

        Assert.True(report.TotalCriticalOverQuotaModels >= 1);
    }

    [Fact]
    public void BuildFromHistory_CorrectlyCalculatesPeakTpm()
    {
        var models = new List<string> { "gemini-2.5-flash" };
        var records = new List<AiUsageRecord>();

        // Fixed minute start at 12:00:00 UTC
        var todayNoon = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 12, 0, 0, TimeSpan.Zero);

        // 3 requests strictly inside 12:00:00 - 12:00:20 with total 30,000 tokens
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = todayNoon.AddSeconds(1), TotalTokens = 10_000 });
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = todayNoon.AddSeconds(5), TotalTokens = 12_000 });
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = todayNoon.AddSeconds(10), TotalTokens = 8_000 });

        var report = GeminiRateLimitReport.BuildFromHistory(models, records, days: 28);

        var model25 = report.Models.First(m => m.ModelName.Contains("2.5"));
        Assert.Equal(30_000, model25.PeakTpm);
        Assert.False(model25.IsExceededRpm); // 3 <= 5
        Assert.False(model25.IsExceededRpd); // 3 <= 20
    }

    [Fact]
    public void BuildFromHistory_Builds28DayTrendsProperly()
    {
        var models = new List<string> { "gemini-1.5-flash" };
        var records = new List<AiUsageRecord>();

        var report = GeminiRateLimitReport.BuildFromHistory(models, records, days: 28);

        Assert.Equal(28, report.DailyTrends.Count);
        for (int i = 0; i < 28; i++)
        {
            Assert.Equal(i + 1, report.DailyTrends[i].DayIndex);
        }
    }
}
