namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Threading;
using System.Threading.Tasks;

public interface IGeminiListingApiClient
{
    Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);
}
