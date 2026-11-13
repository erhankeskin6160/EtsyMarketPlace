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

public interface IGeminiListingApiClient
{
    Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
        string systemInstruction,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);
}
