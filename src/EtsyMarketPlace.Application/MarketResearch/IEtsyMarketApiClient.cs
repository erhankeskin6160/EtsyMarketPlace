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

    Task<string> GetShopByNameRawJsonAsync(
        string authShopId,
        string targetShopName,
        CancellationToken cancellationToken = default) => Task.FromResult("{\"count\":0,\"results\":[]}");

    Task<string> GetShopByIdRawJsonAsync(
        string authShopId,
        long targetShopId,
        CancellationToken cancellationToken = default) => Task.FromResult("{}");

    Task<string> GetShopActiveListingsRawJsonAsync(
        string authShopId,
        long targetShopId,
        int limit = 50,
        CancellationToken cancellationToken = default) => Task.FromResult("{\"count\":0,\"results\":[]}");
}
