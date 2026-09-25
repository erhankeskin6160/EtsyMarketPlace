namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global API istemcisi arayüzü (Teklif sorgulama ve gönderi oluşturma servisleri).
/// </summary>
public interface IArasGlobalApiClient
{
    Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(
        ArasGlobalQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<decimal> TranslateCurrencyAsync(
        decimal price,
        string entryCurrency,
        string exitCurrency,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<List<ArasGtipSearchResult>> SearchGtipCodeAsync(
        string keyword,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<ArasAdditionalOptions> GetAdditionalOptionsAsync(
        string destinationCountry,
        string provider,
        decimal orderTotalUsd,
        string currency,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<string> GetAdditionalInformationAsync(
        string provider,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<ArasCreateShipmentResponse> CreateShipmentAsync(
        ArasCreateShipmentRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateShipmentAsync(
        ArasCreateShipmentRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<ArasShipmentPriceBreakdown> CalculateShipmentPriceAsync(
        string shipmentId,
        string provider,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<string> GetShipmentLegalDocumentAsync(
        string shipmentId,
        string docType,
        string rawBearerToken,
        CancellationToken cancellationToken = default);

    Task<bool> SendShipmentPriceAsync(
        string shipmentId,
        string provider,
        decimal cargoPrice,
        string rawBearerToken,
        CancellationToken cancellationToken = default);
}
