namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;

public interface IAiUsageRecorder
{
    Task RecordAsync(
        string moduleName,
        LlmJsonResult result,
        string? note = null,
        CancellationToken cancellationToken = default);
}

public sealed class AiUsageRecorderService : IAiUsageRecorder
{
    private readonly IAiUsageRepository _repository;

    public AiUsageRecorderService(IAiUsageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task RecordAsync(
        string moduleName,
        LlmJsonResult result,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (result == null) return;

        var totalTokens = result.TotalTokens > 0
            ? result.TotalTokens
            : result.PromptTokens + result.CompletionTokens;

        var modelName = string.IsNullOrWhiteSpace(result.ResolvedModel) ? "unknown" : result.ResolvedModel;
        var provider = ResolveProviderLabel(result.Provider, modelName);
        var costUsd = AiPriceCalculator.CalculateCostUsd(modelName, result.PromptTokens, result.CompletionTokens);
        var costTry = Math.Round(costUsd * AiPriceCalculator.DefaultExchangeRateUsdTry, 4);

        var record = new AiUsageRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            ModuleName = string.IsNullOrWhiteSpace(moduleName) ? "AI Modülü" : moduleName.Trim(),
            Provider = provider,
            ModelName = modelName,
            PromptTokens = Math.Max(0, result.PromptTokens),
            CompletionTokens = Math.Max(0, result.CompletionTokens),
            TotalTokens = Math.Max(0, totalTokens),
            EstimatedCostUsd = costUsd,
            EstimatedCostTry = costTry,
            Status = result.Success ? "Başarılı" : "Hata",
            Note = string.IsNullOrWhiteSpace(note)
                ? (result.Success ? null : result.ErrorMessage)
                : note
        };

        try
        {
            await _repository.SaveUsageAsync(record, cancellationToken);
        }
        catch
        {
            // Best-effort telemetry: never fail the primary business workflow
        }
    }

    private static string ResolveProviderLabel(string? provider, string modelName)
    {
        if (!string.IsNullOrWhiteSpace(provider))
            return provider.Trim();

        var m = (modelName ?? string.Empty).ToLowerInvariant();
        if (m.Contains("deepseek")) return "DeepSeek";
        if (m.Contains("claude")) return "Claude";
        if (m.Contains("grok")) return "Grok";
        if (m.StartsWith("gpt-") || m.StartsWith("o1") || m.StartsWith("o3") || m.StartsWith("o4")) return "OpenAI";
        return "Google Gemini";
    }
}
