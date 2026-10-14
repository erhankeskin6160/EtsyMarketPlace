namespace EtsyMarketPlace.Application.MarketResearch;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IEtsyMarketApiClient
{
    Task<string> SearchActiveListingsRawJsonAsync(
        string shopId,
        string keyword,
        int limit,
        CancellationToken cancellationToken = default);

    Task<string> GetListingsBatchRawJsonAsync(
        string shopId,
        IReadOnlyList<long> listingIds,
        CancellationToken cancellationToken = default);
}
