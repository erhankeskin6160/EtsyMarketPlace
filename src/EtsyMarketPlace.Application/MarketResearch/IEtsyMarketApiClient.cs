namespace EtsyMarketPlace.Application.MarketResearch;

using System.Threading;
using System.Threading.Tasks;

public interface IEtsyMarketApiClient
{
    Task<string> SearchActiveListingsRawJsonAsync(
        string shopId,
        string keyword,
        int limit,
        CancellationToken cancellationToken = default);
}
