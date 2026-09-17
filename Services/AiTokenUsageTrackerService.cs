namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Infrastructure.AiUsage;

public static class AiTokenUsageTrackerService
{
    private static IAiUsageRepository? _repository;
    private static readonly object Lock = new();
    private static bool _initialized;

    public static void Initialize(string? databasePath = null)
    {
        lock (Lock)
        {
            if (_initialized) return;

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace");
                databasePath = Path.Combine(appDataDir, "ai-usage.db");
            }

            var repo = new SqliteAiUsageRepository(databasePath);
            _repository = repo;
            _initialized = true;

            // Arka planda tabloyu oluştur
            _ = Task.Run(async () =>
            {
                try
                {
                    await repo.InitializeAsync();
                }
                catch { }
            });
        }
    }

    public static IAiUsageRepository GetRepository()
    {
        if (_repository == null)
        {
            Initialize();
        }
        return _repository!;
    }

    public static void TrackUsage(
        string moduleName,
        string provider,
        string modelName,
        int promptTokens,
        int completionTokens,
        string status = "Başarılı",
        string? note = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var repo = GetRepository();
                int totalTokens = promptTokens + completionTokens;
                decimal costUsd = AiPriceCalculator.CalculateCostUsd(modelName, promptTokens, completionTokens);
                decimal costTry = AiPriceCalculator.CalculateCostTry(costUsd);

                var record = new AiUsageRecord
                {
                    Timestamp = DateTimeOffset.Now,
                    ModuleName = moduleName,
                    Provider = provider,
                    ModelName = modelName,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    EstimatedCostUsd = costUsd,
                    EstimatedCostTry = costTry,
                    Status = status,
                    Note = note
                };

                await repo.SaveUsageAsync(record);
                AiDataCacheService.InvalidateTodayAndBalance(provider);
            }
            catch { }
        });
    }

    public static void TrackBlockedOrError(
        string moduleName,
        string provider,
        string modelName,
        int httpStatusCode,
        string? errorMessage = null,
        string? note = null)
    {
        string status = httpStatusCode == 429
            ? "⚠️ Engellendi (429 Kota Aşımı)"
            : httpStatusCode == 404
                ? "⚠️ Hata (404 Model Bulunamadı)"
                : $"⚠️ Hata (HTTP {httpStatusCode})";

        string combinedNote = string.IsNullOrWhiteSpace(errorMessage)
            ? (note ?? "")
            : $"{note ?? ""} | {errorMessage}".Trim(' ', '|');

        TrackUsage(moduleName, provider, modelName, 0, 0, status, combinedNote);
    }
}
