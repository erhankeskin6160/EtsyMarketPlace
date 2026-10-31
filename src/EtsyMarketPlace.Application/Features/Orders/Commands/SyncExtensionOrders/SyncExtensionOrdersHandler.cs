namespace EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Orders;
using MediatR;

public class SyncExtensionOrdersHandler(
    IEtsyIntegrationRepository repository,
    IOrderAddressRepository addressRepository
) : IRequestHandler<SyncExtensionOrdersRequest, ServiceResponse>
{
    private static readonly HashSet<string> EuCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
        "NL", "PL", "PT", "RO", "SE", "SI", "SK"
    };

    public async Task<ServiceResponse> Handle(SyncExtensionOrdersRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request?.Orders == null || request.Orders.Count == 0)
            return ServiceResponse.CreateError("Sipariş listesi boş olamaz.", HttpStatusCode.BadRequest);

        var shopId = !string.IsNullOrWhiteSpace(request.ShopId) ? request.ShopId.Trim() : "53236321";
        int updatedCount = 0;

        foreach (var order in request.Orders)
        {
            if (string.IsNullOrWhiteSpace(order.ReceiptId)) continue;
            if (!long.TryParse(order.ReceiptId.Trim(), out var rId) || rId <= 0) continue;

            // 1. Maliyet ve sipariş notunu güncelle
            var note = $"Müşteri: {order.BuyerName}";
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
                note += $" | Adres: {order.ShippingAddress}";

            await repository.UpsertOrderCostAsync(
                shopId,
                order.ReceiptId.Trim(),
                null,
                null,
                null,
                note,
                cancellationToken);

            // 2. Ülke ve IOSS normalizasyonu
            var (countryName, countryCode) = NormalizeCountry(order.Country, order.Currency);
            string ioss = ResolveIossNumber(countryCode, order.IossNumber);

            // 3. Takip Kodu çözümlemesi
            string tracking = !string.IsNullOrWhiteSpace(order.TrackingCode)
                ? order.TrackingCode.Trim()
                : (!string.IsNullOrWhiteSpace(order.TrackingNumber) ? order.TrackingNumber.Trim() : string.Empty);
            if (string.Equals(tracking, "No tracking", StringComparison.OrdinalIgnoreCase) || string.Equals(tracking, "no-tracking", StringComparison.OrdinalIgnoreCase)) tracking = string.Empty;

            // 4. Veri ezilme koruması ile adres belirleme
            var existingRecord = await addressRepository.GetByReceiptIdAsync(rId, cancellationToken);
            string street;
            string second;
            string city;
            string state;
            string postalCode;
            string phone;

            bool isFull = order.IsFullAddress == true;

            if (isFull)
            {
                street = order.StreetAddress?.Trim() ?? string.Empty;
                second = order.SecondAddress?.Trim() ?? string.Empty;
                city = order.City?.Trim() ?? string.Empty;
                state = order.State?.Trim() ?? string.Empty;
                postalCode = order.PostalCode?.Trim() ?? string.Empty;
                phone = order.Phone?.Trim() ?? (existingRecord?.Phone ?? string.Empty);
            }
            else
            {
                // Kapalı liste özeti: Mevcut kayıttaki tam sokak adresini ASLA ezme!
                street = existingRecord != null && !string.IsNullOrWhiteSpace(existingRecord.StreetAddress)
                    ? existingRecord.StreetAddress
                    : (order.StreetAddress?.Trim() ?? string.Empty);

                second = existingRecord != null && !string.IsNullOrWhiteSpace(existingRecord.SecondAddress)
                    ? existingRecord.SecondAddress
                    : (order.SecondAddress?.Trim() ?? string.Empty);

                city = !string.IsNullOrWhiteSpace(order.City)
                    ? order.City.Trim()
                    : (existingRecord?.City ?? string.Empty);

                state = !string.IsNullOrWhiteSpace(order.State)
                    ? order.State.Trim()
                    : (existingRecord?.State ?? string.Empty);

                postalCode = !string.IsNullOrWhiteSpace(order.PostalCode)
                    ? order.PostalCode.Trim()
                    : (existingRecord?.PostalCode ?? string.Empty);

                phone = existingRecord != null && !string.IsNullOrWhiteSpace(existingRecord.Phone)
                    ? existingRecord.Phone
                    : (order.Phone?.Trim() ?? string.Empty);
            }

            string rawBuyer = order.BuyerName?.Trim() ?? string.Empty;
            bool isInvalidBuyer = string.IsNullOrWhiteSpace(rawBuyer) ||
                System.Text.RegularExpressions.Regex.IsMatch(rawBuyer, @"^(?:day|days|ago|\d+\s*days?\s*ago|today|yesterday|overdue|select\s*all)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                rawBuyer.Length < 2;
            string effectiveBuyer = !isInvalidBuyer ? rawBuyer : (existingRecord?.BuyerName ?? string.Empty);

            var addrRecord = new OrderAddressRecord(
                rId,
                effectiveBuyer,
                existingRecord?.BuyerEmail ?? string.Empty,
                phone,
                street,
                second,
                city,
                state,
                postalCode,
                countryCode,
                countryName,
                "EtsyExtension",
                DateTime.UtcNow)
            {
                TrackingCode = !string.IsNullOrWhiteSpace(tracking) ? tracking : (existingRecord?.TrackingCode ?? string.Empty),
                IossNumber = !string.IsNullOrWhiteSpace(ioss) ? ioss : (existingRecord?.IossNumber ?? string.Empty)
            };

            await addressRepository.SaveAsync(addrRecord, cancellationToken);
            updatedCount++;
        }

        return ServiceResponse.CreateSuccess(
            $"{updatedCount} sipariş ve müşteri bilgisi VDS'e başarıyla aktarıldı.",
            new { success = true, updatedCount });
    }

    public static (string CountryName, string CountryCode) NormalizeCountry(string? raw, string? currency = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (currency == "TRY") return ("Turkey", "TR");
            if (currency == "EUR") return ("Germany", "DE");
            if (currency == "GBP") return ("United Kingdom", "GB");
            return ("United States", "US");
        }

        string upper = raw.Trim().ToUpperInvariant();
        return upper switch
        {
            "US" or "USA" or "UNITED STATES" or "UNITED STATES OF AMERICA" or "AMERİKA" or "AMERIKA" or "AMERIKA BIRLESIK DEVLETLERI" => ("United States", "US"),
            "CA" or "CAN" or "CANADA" or "KANADA" => ("Canada", "CA"),
            "DE" or "GERMANY" or "DEUTSCHLAND" or "ALMANYA" => ("Germany", "DE"),
            "GB" or "UK" or "UNITED KINGDOM" or "GREAT BRITAIN" or "İNGİLTERE" or "INGILTERE" => ("United Kingdom", "GB"),
            "FR" or "FRANCE" or "FRANSA" => ("France", "FR"),
            "IT" or "ITALY" or "ITALIA" or "İTALYA" or "ITALYA" => ("Italy", "IT"),
            "ES" or "SPAIN" or "ESPAÑA" or "İSPANYA" or "ISPANYA" => ("Spain", "ES"),
            "NL" or "NETHERLANDS" or "HOLLAND" or "HOLLANDA" => ("Netherlands", "NL"),
            "AU" or "AUSTRALIA" or "AVUSTRALYA" => ("Australia", "AU"),
            "TR" or "TURKEY" or "TÜRKİYE" or "TURKIYE" => ("Turkey", "TR"),
            "AT" or "AUSTRIA" or "ÖSTERREICH" or "AVUSTURYA" => ("Austria", "AT"),
            "BE" or "BELGIUM" or "BELÇİKA" => ("Belgium", "BE"),
            "CH" or "SWITZERLAND" or "İSVİÇRE" => ("Switzerland", "CH"),
            "SE" or "SWEDEN" or "İSVEÇ" => ("Sweden", "SE"),
            "NO" or "NORWAY" or "NORVEÇ" => ("Norway", "NO"),
            "DK" or "DENMARK" or "DANİMARKA" => ("Denmark", "DK"),
            "FI" or "FINLAND" or "FİNLANDİYA" => ("Finland", "FI"),
            "PL" or "POLAND" or "POLONYA" => ("Poland", "PL"),
            "IE" or "IRELAND" or "İRLANDA" => ("Ireland", "IE"),
            _ when upper.Length == 2 => (upper, upper),
            _ => (raw.Trim(), upper.Length == 2 ? upper : "US")
        };
    }

    public static string ResolveIossNumber(string countryCode, string? explicitIoss)
    {
        if (!string.IsNullOrWhiteSpace(explicitIoss)) return explicitIoss.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(countryCode)) return string.Empty;

        string code = countryCode.Trim().ToUpperInvariant();
        if (EuCountryCodes.Contains(code)) return "IM3720000224"; // Resmi Etsy AB IOSS Numarası
        if (code == "GB" || code == "UK") return "370 6004 28";   // Resmi Etsy UK VAT Numarası
        return string.Empty;
    }
}
