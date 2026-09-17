namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GeminiModelRateLimitItem
{
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "Text-out models";

    // Official Free Tier / Tier Limits
    public int LimitRpm { get; set; } = 5;
    public long LimitTpm { get; set; } = 250_000;
    public int LimitRpd { get; set; } = 20;

    // Peak Usage in selected period (e.g. last 28 days)
    public int PeakRpm { get; set; }
    public long PeakTpm { get; set; }
    public int PeakRpd { get; set; }

    // Live Usage (today / recent)
    public int CurrentRpm { get; set; }
    public long CurrentTpm { get; set; }
    public int CurrentRpd { get; set; }

    public bool IsExceededRpm => PeakRpm > LimitRpm;
    public bool IsExceededRpd => PeakRpd > LimitRpd;
    public bool IsCritical => IsExceededRpm || IsExceededRpd;
}

public sealed class GeminiDailyUsageTrend
{
    public int DayIndex { get; set; } // 1 to 28
    public DateTime Date { get; set; }
    public int RequestCount { get; set; }
    public long TokenCount { get; set; }
    public int PeakRpm { get; set; }
}

public sealed class GeminiRateLimitReport
{
    public string ProjectName { get; set; } = "Varsayılan Proje";
    public string Tier { get; set; } = "Free Tier";
    public string TimeRange { get; set; } = "Son 28 Gün";
    public DateTime CheckedAt { get; set; } = DateTime.Now;

    public int TotalCriticalOverQuotaModels { get; set; }
    public int PeakActiveRpm { get; set; }
    public int PeakRpmLimit { get; set; } = 5;
    public long PeakTpm { get; set; }
    public long PeakTpmLimit { get; set; } = 250_000;
    public int TotalAvailableModels { get; set; }

    public List<GeminiModelRateLimitItem> Models { get; set; } = [];
    public List<GeminiDailyUsageTrend> DailyTrends { get; set; } = [];

    public static GeminiRateLimitReport BuildFromHistory(
        List<string> availableModels,
        IReadOnlyList<AiUsageRecord> records,
        int days = 28,
        string projectName = "Varsayılan Proje")
    {
        var report = new GeminiRateLimitReport
        {
            ProjectName = projectName,
            Tier = "Free Tier",
            TimeRange = days == 1 ? "Bugün (Canlı)" : (days == 7 ? "Son 7 Gün" : $"Son {days} Gün"),
            TotalAvailableModels = availableModels.Count > 0 ? availableModels.Count : 50,
            CheckedAt = DateTime.Now
        };

        var geminiRecords = records
            .Where(r => r.Provider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 1. Determine key models to show (Matches Google AI Studio Rate limits by model list)
        var keyModelNames = new List<string>
        {
            "gemini-3.7-flash",
            "gemini-3.8-flash",
            "gemini-2.5-flash",
            "gemini-3.6-flash",
            "antigravity",
            "deep-research-pro-preview",
            "gemini-2-flash",
            "gemini-2-flash-lite",
            "gemini-1.5-flash",
            "gemini-1.5-pro",
            "text-embedding-004"
        };

        // Merge any models found in history or available list
        foreach (var m in geminiRecords.Select(r => r.ModelName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(m) && !keyModelNames.Contains(m, StringComparer.OrdinalIgnoreCase))
            {
                keyModelNames.Add(m);
            }
        }

        foreach (var rawName in keyModelNames)
        {
            var item = CreateDefaultRateLimitItem(rawName);

            var modelRecs = geminiRecords
                .Where(r => r.ModelName.Equals(rawName, StringComparison.OrdinalIgnoreCase)
                         || r.ModelName.Contains(rawName.Replace("gemini-", ""), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (modelRecs.Count > 0)
            {
                // Calculate Peak RPM (group by 1-minute intervals)
                var minuteGroups = modelRecs
                    .GroupBy(r => r.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"))
                    .Select(g => new { Count = g.Count(), Tokens = g.Sum(x => (long)x.TotalTokens) })
                    .ToList();

                item.PeakRpm = minuteGroups.Count > 0 ? minuteGroups.Max(g => g.Count) : 0;
                item.PeakTpm = minuteGroups.Count > 0 ? minuteGroups.Max(g => g.Tokens) : 0;

                // Calculate Peak RPD (group by date)
                var dayGroups = modelRecs
                    .GroupBy(r => r.Timestamp.ToUniversalTime().Date)
                    .Select(g => g.Count())
                    .ToList();

                item.PeakRpd = dayGroups.Count > 0 ? dayGroups.Max() : 0;

                // Current day RPD
                var todayUtc = DateTime.UtcNow.Date;
                item.CurrentRpd = modelRecs.Count(r => r.Timestamp.ToUniversalTime().Date == todayUtc);
            }
            else
            {
                item.PeakRpm = 0;
                item.PeakTpm = 0;
                item.PeakRpd = 0;
                item.CurrentRpd = 0;
            }

            report.Models.Add(item);
        }

        // Calculate top KPI totals
        report.TotalCriticalOverQuotaModels = report.Models.Count(m => m.IsCritical);
        report.PeakActiveRpm = report.Models.Count > 0 ? report.Models.Max(m => m.PeakRpm) : 0;
        report.PeakTpm = report.Models.Count > 0 ? report.Models.Max(m => m.PeakTpm) : 0;

        // Build 28-day daily trend purely from user's actual calls
        var now = DateTime.UtcNow.Date;
        for (int i = days; i >= 1; i--)
        {
            var targetDate = now.AddDays(-i + 1);
            var dayRecs = geminiRecords.Where(r => r.Timestamp.ToUniversalTime().Date == targetDate).ToList();

            var trend = new GeminiDailyUsageTrend
            {
                DayIndex = days - i + 1,
                Date = targetDate,
                RequestCount = dayRecs.Count,
                TokenCount = dayRecs.Sum(r => (long)r.TotalTokens),
                PeakRpm = dayRecs.Count > 0
                    ? dayRecs.GroupBy(r => r.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")).Max(g => g.Count())
                    : 0
            };

            report.DailyTrends.Add(trend);
        }

        return report;
    }

    private static GeminiModelRateLimitItem CreateDefaultRateLimitItem(string rawName)
    {
        string norm = rawName.ToLowerInvariant();
        string displayName = FormatDisplayName(rawName);

        var item = new GeminiModelRateLimitItem
        {
            ModelName = rawName,
            DisplayName = displayName,
            Category = (norm.Contains("agent") || norm.Contains("antigravity") || norm.Contains("research"))
                ? "Agents"
                : (norm.Contains("embed") ? "Embeddings" : "Text-out models")
        };

        if (norm.Contains("antigravity"))
        {
            item.LimitRpm = 60;
            item.LimitTpm = 100_000;
            item.LimitRpd = 100;
        }
        else if (norm.Contains("deep-research") || norm.Contains("gemini-2-flash"))
        {
            item.LimitRpm = 0;
            item.LimitTpm = 0;
            item.LimitRpd = 0;
        }
        else if (norm.Contains("1.5-flash"))
        {
            item.LimitRpm = 15;
            item.LimitTpm = 1_000_000;
            item.LimitRpd = 1_500;
        }
        else if (norm.Contains("1.5-pro"))
        {
            item.LimitRpm = 2;
            item.LimitTpm = 32_000;
            item.LimitRpd = 50;
        }
        else if (norm.Contains("embed"))
        {
            item.LimitRpm = 60;
            item.LimitTpm = 100_000;
            item.LimitRpd = 10_000;
        }
        else // Gemini 3.7 Flash, 3.8 Flash, 2.5 Flash, 3.6 Flash
        {
            item.LimitRpm = 5;
            item.LimitTpm = 250_000;
            item.LimitRpd = 20;
        }

        return item;
    }

    private static string FormatDisplayName(string raw)
    {
        string name = raw.Replace("models/", "", StringComparison.OrdinalIgnoreCase);
        return name switch
        {
            "gemini-3.7-flash" => "Gemini 3.7 Flash",
            "gemini-3.8-flash" => "Gemini 3.8 Flash",
            "gemini-2.5-flash" => "Gemini 2.5 Flash",
            "gemini-3.6-flash" => "Gemini 3.6 Flash",
            "gemini-1.5-flash" => "Gemini 1.5 Flash",
            "gemini-1.5-pro" => "Gemini 1.5 Pro",
            "antigravity" => "Antigravity",
            "deep-research-pro-preview" => "Deep Research Pro Preview",
            "gemini-2-flash" => "Gemini 2 Flash",
            "gemini-2-flash-lite" => "Gemini 2 Flash Lite",
            "text-embedding-004" => "Text Embedding 004",
            _ => char.ToUpperInvariant(name[0]) + name.Substring(1)
        };
    }
}
