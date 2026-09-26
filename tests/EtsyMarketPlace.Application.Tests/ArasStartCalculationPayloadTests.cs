namespace EtsyMarketPlace.Application.Tests;

using System.Text.Json;
using Xunit;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Fiyat hesaplama başlatma isteğinin resmi sözleşmeye uyduğunu doğrular.
/// Regresyon nedeni: bu istek daha önce PascalCase anahtarlarla ve
/// <c>receiverCountryCode</c> adıyla gönderiliyordu; API <c>receiverCountry</c> bekler.
/// </summary>
public sealed class ArasStartCalculationPayloadTests
{
    private static string Serialize(ArasStartCalculationPayload payload)
        => JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static ArasStartCalculationPayload Build() => new()
    {
        Currency = "USD",
        ReceiverCity = "Koblenz",
        ReceiverCountry = "DE",
        ReceiverPostalCode = "56068",
        ReceiverState = string.Empty,
        SenderCountry = "TR",
        ShipmentDimensions =
        {
            new ArasStartCalculationBox { Length = 20, Width = 15, Height = 10, Weight = 0.4, PackageCount = 1 }
        }
    };

    [Fact]
    public void Payload_UsesDocumentedCamelCaseKeys()
    {
        string json = Serialize(Build());

        foreach (string key in new[]
                 {
                     "currency", "discountCode", "internationalShipmentCategory", "isIndividualCustomer",
                     "isMicroExport", "packageCount", "packageType", "receiverCity", "receiverCountry",
                     "receiverPostalCode", "receiverState", "receiverTown", "senderCountry", "shipmentDimensions"
                 })
        {
            Assert.Contains($"\"{key}\"", json);
        }

        foreach (string key in new[] { "length", "width", "height", "weight", "packageCount" })
        {
            Assert.Contains($"\"{key}\"", json);
        }
    }

    [Fact]
    public void Payload_DoesNotUseTheWrongCountryKey()
    {
        string json = Serialize(Build());

        Assert.DoesNotContain("receiverCountryCode", json);
        Assert.Contains("\"receiverCountry\":\"DE\"", json);
    }

    [Fact]
    public void Payload_DoesNotUsePascalCaseKeys()
    {
        string json = Serialize(Build());

        Assert.DoesNotContain("\"ShipmentDimensions\"", json);
        Assert.DoesNotContain("\"ReceiverCountry\"", json);
        Assert.DoesNotContain("\"Currency\"", json);
    }

    [Fact]
    public void Payload_OmitsVolumetricWeightAtPricingStage()
    {
        // Sözleşmede bu adımda yalnız ebat + ağırlık gönderilir; desi/hacim ağırlığı istemez.
        string json = Serialize(Build());

        Assert.DoesNotContain("volumetricWeight", json);
        Assert.DoesNotContain("\"desi\"", json);
    }

    [Fact]
    public void Payload_CarriesReceiverAddressFieldsNeededByPricing()
    {
        string json = Serialize(Build());

        Assert.Contains("\"receiverCity\":\"Koblenz\"", json);
        Assert.Contains("\"receiverPostalCode\":\"56068\"", json);
        Assert.Contains("\"isIndividualCustomer\":true", json);
        Assert.Contains("\"packageType\":1", json);
    }
}
