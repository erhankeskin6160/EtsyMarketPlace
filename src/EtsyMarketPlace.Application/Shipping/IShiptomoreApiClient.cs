namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Shiptomore (shiptomore.com) canlı kargo fiyat teklif istemcisi arayüzü.
/// </summary>
public interface IShiptomoreApiClient
{
    Task<List<ShiptomoreQuoteOffer>> FetchLiveQuotesAsync(
        ShiptomoreQuoteRequest request,
        ShiptomoreSettings? settings = null,
        CancellationToken cancellationToken = default);
}
