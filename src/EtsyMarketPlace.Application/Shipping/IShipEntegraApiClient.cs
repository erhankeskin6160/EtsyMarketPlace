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
    /// <summary>
    /// E-posta/şifre ile doğrudan API oturumu açar (panel ile aynı uç: /v1/auth/login).
    /// Otomatik token yenilemenin tarayıcısız yoludur.
    /// </summary>
    Task<ShipEntegraAuthTokens?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);


    Task<List<ShipEntegraQuoteOffer>> FetchLiveQuotesAsync(
        ShipEntegraQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<ShipEntegraOrderResult> CreateOrderAsync(
        ShipEntegraCreateOrderRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<List<long>> GetOrderItemsAsync(
        long orderId,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<byte[]?> CreateLabelAsync(
        ShipEntegraCreateLabelRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);
}
