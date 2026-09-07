namespace EtsyMarketPlace.Application.KeywordResearch;

using EtsyMarketPlace.Domain.KeywordResearch;

public interface IKeywordMarketGateway
{
    Task<KeywordMarketSample> GetSampleAsync(
        string keyword,
        int limit,
        CancellationToken cancellationToken = default);
}
