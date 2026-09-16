namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Infrastructure.AiUsage;
using Xunit;

public sealed class AiUsageTrackerTests
{
    [Fact]
    public void AiPriceCalculator_CalculatesGpt56Luna_Accurately()
    {
        // 320,000 prompt tokens + 290,000 completion tokens
        // Luna: $0.20/1M prompt, $1.20/1M completion
        // Prompt: 0.32 * $0.20 = $0.064
        // Completion: 0.29 * $1.20 = $0.348
        // Total: $0.412
        var costUsd = AiPriceCalculator.CalculateCostUsd("gpt-5.6-luna", 320_000, 290_000);
        Assert.Equal(0.412m, Math.Round(costUsd, 3));

        var costTry = AiPriceCalculator.CalculateCostTry(costUsd, exchangeRate: 40.0m);
        Assert.Equal(16.48m, Math.Round(costTry, 2));
    }

    [Fact]
    public void AiPriceCalculator_CalculatesGemini25Flash_Accurately()
    {
        // 100,000 prompt + 50,000 completion
        // Flash: $0.30/1M prompt, $2.50/1M completion
        // Prompt: 0.10 * 0.30 = $0.03
        // Completion: 0.05 * 2.50 = $0.125
        // Total: $0.155
        var costUsd = AiPriceCalculator.CalculateCostUsd("gemini-2.5-flash", 100_000, 50_000);
        Assert.Equal(0.155m, Math.Round(costUsd, 3));
    }

    [Fact]
    public void AiPriceCalculator_CalculatesDeepSeek_Accurately()
    {
        // 1,000,000 prompt + 1,000,000 completion
        // DeepSeek Chat: $0.15 / $0.60 -> Total $0.75
        var costUsd = AiPriceCalculator.CalculateCostUsd("deepseek-chat", 1_000_000, 1_000_000);
        Assert.Equal(0.75m, Math.Round(costUsd, 2));
    }

    [Fact]
    public async Task SqliteAiUsageRepository_LifecycleAndFiltering_WorksCorrectly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test-ai-usage-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteAiUsageRepository(tempDbPath);
            await repo.InitializeAsync();

            // 1. Save successful record
            var rec1 = new AiUsageRecord
            {
                ModuleName = "Hızlı Ürün Ekle (Başlık)",
                Provider = "Google Gemini",
                ModelName = "gemini-2.5-flash",
                PromptTokens = 500,
                CompletionTokens = 200,
                TotalTokens = 700,
                EstimatedCostUsd = 0.00065m,
                EstimatedCostTry = 0.026m,
                Status = "Başarılı"
            };
            await repo.SaveUsageAsync(rec1);

            // 2. Save blocked 429 record
            var rec2 = new AiUsageRecord
            {
                ModuleName = "Hızlı Ürün Ekle (Kategori)",
                Provider = "Google Gemini",
                ModelName = "gemini-2.5-flash",
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                EstimatedCostUsd = 0m,
                EstimatedCostTry = 0m,
                Status = "⚠️ Engellendi (429 Kota Aşımı)",
                Note = "ResourceExhausted"
            };
            await repo.SaveUsageAsync(rec2);

            // 3. Save DeepSeek record
            var rec3 = new AiUsageRecord
            {
                ModuleName = "AI Görsel Stüdyosu",
                Provider = "DeepSeek",
                ModelName = "deepseek-reasoner",
                PromptTokens = 1200,
                CompletionTokens = 800,
                TotalTokens = 2000,
                EstimatedCostUsd = 0.002412m,
                EstimatedCostTry = 0.09648m,
                Status = "Başarılı"
            };
            await repo.SaveUsageAsync(rec3);

            // Query all
            var allHistory = await repo.GetHistoryAsync();
            Assert.Equal(3, allHistory.Count);

            // Query filtered by provider
            var deepSeekHistory = await repo.GetHistoryAsync(providerFilter: "DeepSeek");
            Assert.Single(deepSeekHistory);
            Assert.Equal("DeepSeek", deepSeekHistory[0].Provider);

            // Verify summary stats
            var stats = await repo.GetSummaryStatsAsync();
            Assert.Equal(3, stats.TotalRequests);
            Assert.Equal(2, stats.SuccessfulRequests);
            Assert.Equal(1, stats.Blocked429Requests);
            Assert.Equal(2700, stats.TotalTokens);
            Assert.True(stats.TotalCostUsd > 0);
            Assert.True(stats.CostByModel.ContainsKey("gemini-2.5-flash"));
            Assert.True(stats.CostByModel.ContainsKey("deepseek-reasoner"));
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void TestMaskApiKey_MasksAppropriately()
    {
        Assert.Equal("Tanımlanmadı", AiPriceCalculator.MaskApiKey(null));
        Assert.Equal("Tanımlanmadı", AiPriceCalculator.MaskApiKey(""));
        Assert.Equal("sk-proj-...8Abc", AiPriceCalculator.MaskApiKey("sk-proj-1234567890abcdef128Abc"));
        Assert.Equal("sk-admin-...mnop", AiPriceCalculator.MaskApiKey("sk-admin-1234567890abcdefmnop"));
        Assert.Equal("sk-1...mnop", AiPriceCalculator.MaskApiKey("sk-1234567890abcdefmnop"));
    }

    [Fact]
    public void TestParseOpenAiCostsJson_CalculatesTotal()
    {
        string sampleJson = """
        {
            "object": "page",
            "data": [
                {
                    "object": "organization.costs.result",
                    "amount": { "value": 0.25, "currency": "usd" }
                },
                {
                    "object": "organization.costs.result",
                    "amount": { "value": 0.35, "currency": "usd" }
                }
            ]
        }
        """;

        decimal total = AiPriceCalculator.ParseOpenAiCostsJson(sampleJson);
        Assert.Equal(0.60m, total);

        Assert.Equal(0m, AiPriceCalculator.ParseOpenAiCostsJson(""));
        Assert.Equal(0m, AiPriceCalculator.ParseOpenAiCostsJson("invalid json"));
    }

    [Fact]
    public void TestOpenAiOfficialUsageReport_Calculations()
    {
        var report = new OpenAiOfficialUsageReport
        {
            MonthName = "Eylül 2026",
            Year = 2026,
            Month = 9,
            TotalCostUsd = 0.00m,
            TotalTokens = 0,
            TotalRequests = 0,
            MaskedKey = "sk-proj-...8Abc"
        };

        Assert.Equal(0.00m, report.TotalCostTry);
        Assert.Equal(0, report.TotalTokens);
        Assert.Equal(0, report.TotalRequests);

        var item = new OpenAiDailyUsageItem
        {
            Date = new DateTime(2026, 9, 1),
            ServiceOrModel = "gpt-4o",
            RequestCount = 5,
            InputTokens = 1200,
            OutputTokens = 800,
            CostUsd = 0.05m
        };

        Assert.Equal(2000, item.TotalTokens);
        Assert.Equal(2.00m, item.CostTry);
    }

    [Fact]
    public void TestParseOpenAiCostsJson_NestedBuckets_CalculatesCorrectly()
    {
        string officialOpenAiCostsJson = """
        {
            "object": "page",
            "data": [
                {
                    "object": "bucket",
                    "start_time": 1725148800,
                    "end_time": 1725235200,
                    "results": [
                        {
                            "object": "organization.costs.result",
                            "amount": { "value": 0.1308, "currency": "usd" },
                            "line_item": "gpt-4o"
                        }
                    ]
                },
                {
                    "object": "bucket",
                    "start_time": 1725235200,
                    "end_time": 1725321600,
                    "results": [
                        {
                            "object": "organization.costs.result",
                            "amount": { "value": 0.05, "currency": "usd" },
                            "line_item": "gpt-5.6-luna"
                        }
                    ]
                }
            ]
        }
        """;

        decimal total = AiPriceCalculator.ParseOpenAiCostsJson(officialOpenAiCostsJson);
        Assert.Equal(0.1808m, total);
    }

    [Fact]
    public void TestParseOpenAiCostsDetailsJson_And_MergeOpenAiUsageJson_AccuratelyPopulatesTokens()
    {
        // 1. Costs JSON
        string costsJson = """
        {
            "object": "page",
            "next_page": "cursor_cost_page_2",
            "data": [
                {
                    "object": "bucket",
                    "start_time": 1724630400,
                    "end_time": 1724716800,
                    "results": [
                        {
                            "object": "organization.costs.result",
                            "amount": { "value": 2.5282, "currency": "usd" },
                            "line_item": "Genel"
                        }
                    ]
                },
                {
                    "object": "bucket",
                    "start_time": 1724284800,
                    "end_time": 1724371200,
                    "results": [
                        {
                            "object": "organization.costs.result",
                            "amount": { "value": 0.0447, "currency": "usd" },
                            "line_item": "Genel"
                        }
                    ]
                }
            ]
        }
        """;

        var (costItems, nextCostCursor) = AiPriceCalculator.ParseOpenAiCostsDetailsJson(costsJson);
        Assert.Equal(2, costItems.Count);
        Assert.Equal("cursor_cost_page_2", nextCostCursor);

        var report = new OpenAiOfficialUsageReport
        {
            HasAdminKey = true,
            MaskedKey = "sk-admin-...fcQA"
        };
        foreach (var c in costItems)
        {
            report.DailyItems.Add(c);
            report.TotalCostUsd += c.CostUsd;
        }

        Assert.Equal(2.5729m, report.TotalCostUsd);
        Assert.Equal(0, report.TotalTokens);
        Assert.Equal(0, report.TotalInputTokens);
        Assert.Equal(0, report.TotalOutputTokens);

        // 2. Usage Completions JSON with actual input_tokens and output_tokens
        string usageJson = """
        {
            "object": "page",
            "next_page": null,
            "data": [
                {
                    "object": "bucket",
                    "start_time": 1724630400,
                    "end_time": 1724716800,
                    "results": [
                        {
                            "object": "organization.usage.completions.result",
                            "model": "gpt-4o",
                            "input_tokens": 54200,
                            "output_tokens": 15932,
                            "num_model_requests": 48
                        }
                    ]
                },
                {
                    "object": "bucket",
                    "start_time": 1724284800,
                    "end_time": 1724371200,
                    "results": [
                        {
                            "object": "organization.usage.completions.result",
                            "model": "gpt-4o-mini",
                            "input_tokens": 1200,
                            "output_tokens": 800,
                            "num_model_requests": 3
                        }
                    ]
                }
            ]
        }
        """;

        var nextUsageCursor = AiPriceCalculator.MergeOpenAiUsageJson(report, usageJson);
        Assert.Null(nextUsageCursor);

        // Verify tokens are populated into report and items!
        Assert.Equal(72132, report.TotalTokens);
        Assert.Equal(55400, report.TotalInputTokens);
        Assert.Equal(16732, report.TotalOutputTokens);
        Assert.Equal(51, report.TotalRequests);

        // Verify first daily item
        var item1 = report.DailyItems.First(x => x.ServiceOrModel == "gpt-4o");
        Assert.Equal(54200, item1.InputTokens);
        Assert.Equal(15932, item1.OutputTokens);
        Assert.Equal(70132, item1.TotalTokens);
        Assert.Equal(48, item1.RequestCount);
        Assert.Equal(2.5282m, item1.CostUsd);

        // Verify second daily item
        var item2 = report.DailyItems.First(x => x.ServiceOrModel == "gpt-4o-mini");
        Assert.Equal(1200, item2.InputTokens);
        Assert.Equal(800, item2.OutputTokens);
        Assert.Equal(2000, item2.TotalTokens);
        Assert.Equal(3, item2.RequestCount);
        Assert.Equal(0.0447m, item2.CostUsd);
    }
}
