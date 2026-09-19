namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global API istemcisi arayüzü.
/// </summary>
public interface IArasGlobalApiClient
{
    Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(
        ArasGlobalQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);
}
