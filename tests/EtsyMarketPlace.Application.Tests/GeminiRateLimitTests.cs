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

        var report = GeminiRateLimitReport.BuildFromHistory(models, records, days: 28, projectName: "ffff");

        Assert.NotNull(report);
        Assert.Equal("ffff", report.ProjectName);
        Assert.Equal("Free Tier", report.Tier);
        Assert.Equal("Son 28 Gün", report.TimeRange);
        Assert.NotEmpty(report.Models);
        Assert.Equal(28, report.DailyTrends.Count);

        var model37 = report.Models.FirstOrDefault(m => m.ModelName.Contains("3.7"));
        Assert.NotNull(model37);
        Assert.Equal(5, model37.LimitRpm);
        Assert.Equal(250_000, model37.LimitTpm);
        Assert.Equal(20, model37.LimitRpd);
    }

    [Fact]
    public void BuildFromHistory_DetectsExceededRpmAndRpd()
    {
        var models = new List<string> { "gemini-3.7-flash" };
        var records = new List<AiUsageRecord>();

        var now = DateTimeOffset.UtcNow;
        // 7 requests within the exact same minute to exceed 5 RPM limit
        for (int i = 0; i < 7; i++)
        {
            records.Add(new AiUsageRecord
            {
                Provider = "Google Gemini",
                ModelName = "gemini-3.7-flash",
                Timestamp = now.AddSeconds(i * 5),
                TotalTokens = 1500,
                Status = "Başarılı"
            });
        }

        // Add 25 total requests today to exceed 20 RPD limit
        for (int i = 0; i < 18; i++)
        {
            records.Add(new AiUsageRecord
            {
                Provider = "Google Gemini",
                ModelName = "gemini-3.7-flash",
                Timestamp = now.AddHours(-1).AddMinutes(i),
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

        var now = DateTimeOffset.UtcNow;
        // 3 requests in the same minute with total 30,000 tokens
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = now, TotalTokens = 10_000 });
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = now.AddSeconds(10), TotalTokens = 12_000 });
        records.Add(new AiUsageRecord { Provider = "Google Gemini", ModelName = "gemini-2.5-flash", Timestamp = now.AddSeconds(20), TotalTokens = 8_000 });

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
