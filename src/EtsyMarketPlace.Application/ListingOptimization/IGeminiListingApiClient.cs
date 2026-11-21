namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed record VisionImageInput(
    string? ImageUrl,
    byte[]? Bytes = null,
    string MimeType = "image/jpeg",
    int Rank = 1
);

public sealed record LlmJsonResult(
    bool Success,
    string? Content,
    string? ErrorMessage,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    int TotalTokens = 0,
    string Provider = "",
    string ResolvedModel = ""
);

public interface IGeminiListingApiClient
{
    Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);

    async Task<LlmJsonResult> GenerateOptimizationJsonWithUsageAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        int? maxOutputTokens = null,
        CancellationToken cancellationToken = default)
    {
        var (ok, content, err) = await GenerateOptimizationJsonAsync(
            systemInstruction, userPrompt, model, apiKey, cancellationToken);
        return new LlmJsonResult(ok, content, err, 0, 0, 0, string.Empty, model ?? string.Empty);
    }

    Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
        string systemInstruction,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);
}
