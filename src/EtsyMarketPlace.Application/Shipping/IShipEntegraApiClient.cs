namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra API istemcisi arayüzü.
/// </summary>
public interface IShipEntegraApiClient
{
    Task<List<ShipEntegraQuoteOffer>> FetchLiveQuotesAsync(
        ShipEntegraQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);
}
