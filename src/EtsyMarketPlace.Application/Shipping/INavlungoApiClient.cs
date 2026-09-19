namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Navlungo canlı fiyat teklif istemcisi arayüzü.
/// </summary>
public interface INavlungoApiClient
{
    Task<List<NavlungoQuoteOffer>> FetchLiveQuotesAsync(
        NavlungoQuoteRequest request,
        NavlungoSettings? settings = null,
        CancellationToken cancellationToken = default);
}
