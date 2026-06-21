namespace EtsyMarketPlace.Application.KeywordResearch;

using EtsyMarketPlace.Domain.KeywordResearch;

public sealed class AnalyzeKeywordUseCase(IKeywordMarketGateway gateway)
{
    public async Task<KeywordAnalysisResult> ExecuteAsync(
        string keyword,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalized = keyword.Trim();
        if (normalized.Length < 2)
        {
            throw new ArgumentException("Anahtar kelime en az 2 karakter olmali.", nameof(keyword));
        }

        var sample = await gateway.GetSampleAsync(normalized, Math.Clamp(limit, 10, 100), cancellationToken);
        return KeywordAnalysisCalculator.Calculate(sample);
    }
}
