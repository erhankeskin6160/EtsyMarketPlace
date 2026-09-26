namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using Xunit;

public sealed class ArasGlobalShippingTests
{
    [Fact]
    public void ComputeDesi_CalculatesCorrectIataVolumetricWeight()
    {
        // 15 cm x 20 cm x 10 cm / 5000 = 3000 / 5000 = 0.60 desi
        double desi = ArasGlobalQuoteRequest.ComputeDesi(15.0, 20.0, 10.0);
        Assert.Equal(0.60, desi);
    }

    [Fact]
    public void ComputeBillableWeight_ReturnsMaxOfActualAndDesi()
    {
        // Actual 0.40 kg, Desi 0.60 kg -> Billable should be 0.60 kg
        double billable = ArasGlobalQuoteRequest.ComputeBillableWeight(0.40, 15.0, 20.0, 10.0);
        Assert.Equal(0.60, billable);

        // Actual 1.20 kg, Desi 0.60 kg -> Billable should be 1.20 kg
        double billable2 = ArasGlobalQuoteRequest.ComputeBillableWeight(1.20, 15.0, 20.0, 10.0);
        Assert.Equal(1.20, billable2);
    }

    [Fact]
    public void CleanToken_StripsBearerPrefixAndTrims()
    {
        var settings = new ArasGlobalSettings
        {
            BearerToken = "  Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE3..."
        };

        string clean = settings.CleanToken;
        Assert.StartsWith("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", clean);
        Assert.DoesNotContain("Bearer", clean);
        Assert.False(clean.StartsWith(" "));
    }

    [Fact]
    public async Task GetFallbackOffers_ReturnsStandardRatesWithFallbackFlag()
    {
        var service = new ArasGlobalPricingService();
        var req = new ArasGlobalQuoteRequest
        {
            ReceiverCountry = "US",
            WeightKg = 0.40,
            LengthCm = 15.0,
            WidthCm = 20.0,
            HeightCm = 10.0
        };

        var offers = await service.GetFallbackOffersAsync(req);

        Assert.NotEmpty(offers);
        // Tüm tekliflerin IsLivePrice bayrağı false olmalıdır (Yedek liste olduğunu belirtir)
        Assert.All(offers, o => Assert.False(o.IsLivePrice));

        // Widect ve UPS tekliflerinin bulunması beklenir
        var widect = offers.FirstOrDefault(o => o.Cargo.Contains("Widect", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(widect);
        Assert.Equal(13.13m, widect.Price);
        Assert.Equal("USD", widect.Currency);

        var ups = offers.FirstOrDefault(o => o.Cargo.Contains("UPS", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ups);
        Assert.Equal(21.16m, ups.Price);
        Assert.Equal("USD", ups.Currency);
    }

    [Fact]
    public void ArasGlobalSettingsStore_SaveAndLoad_RoundtripsSuccessfully()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "EtsyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var testFilePath = Path.Combine(testDir, "test-aras-settings.json");

        try
        {
            var initial = ArasGlobalSettingsStore.Load(testFilePath);
            Assert.NotNull(initial);
            Assert.Empty(initial.BearerToken);

            initial.BearerToken = "test_token_abc123";
            initial.DefaultCountry = "US";
            initial.DefaultWeightKg = 0.55;
            initial.DefaultLengthCm = 22.0;
            initial.DefaultWidthCm = 15.0;
            initial.DefaultHeightCm = 8.0;

            ArasGlobalSettingsStore.Save(initial, testFilePath);

            var reloaded = ArasGlobalSettingsStore.Load(testFilePath);
            Assert.Equal("test_token_abc123", reloaded.BearerToken);
            Assert.Equal("US", reloaded.DefaultCountry);
            Assert.Equal(0.55, reloaded.DefaultWeightKg);
            Assert.Equal(22.0, reloaded.DefaultLengthCm);
            Assert.Equal(15.0, reloaded.DefaultWidthCm);
            Assert.Equal(8.0, reloaded.DefaultHeightCm);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void ArasGlobalTokenExpiredException_InstantiatesWithProperMessage()
    {
        var ex = new ArasGlobalTokenExpiredException("401 Unauthorized token expired");
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public void ArasCreateShipmentRequest_SerializesToJson_WithoutPropertyCollisions()
    {
        var request = new ArasCreateShipmentRequest
        {
            ShipmentId = "TEST-SHIPMENT-01",
            Price = 11.16m,
            CargoPrice = 13.13m,
            Currency = "USD",
            InternationalCargoProvider = "widect",
            Weight = 0.4,
            VolumetricWeight = 0.6,
            Desi = 0.6
        };

        request.ShipmentDimensions.Add(new ArasBox
        {
            Length = 20,
            Width = 15,
            Height = 10,
            Weight = 0.4,
            VolumetricWeight = 0.6,
            Desi = 0.6,
            PackageCount = 1
        });
        request.BoxList.Add(new ArasBox
        {
            Length = 20,
            Width = 15,
            Height = 10,
            Weight = 0.4,
            VolumetricWeight = 0.6,
            Desi = 0.6,
            PackageCount = 1
        });

        request.ShipmentItems.Add(new ArasShipmentItem
        {
            Description = "Test Item",
            ItemDescription = "Test Item",
            HsCode = "3926400000",
            Quantity = 1,
            UnitPrice = 11.16m,
            Length = 20,
            Width = 15,
            Height = 10,
            Weight = 0.4,
            VolumetricWeight = 0.6,
            Desi = 0.6,
            Category = "1"
        });

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Serileştirme sırasında hiçbir 'collides with another property' hatası fırlatılmamalı
        // ve Aras Global backend'inin beklediği camelCase alanlar (volumetricWeight, desi vb.) üretilmeli
        string json = System.Text.Json.JsonSerializer.Serialize(request, options);

        Assert.NotNull(json);
        Assert.Contains("\"volumetricWeight\":0.6", json);
        Assert.Contains("\"desi\":0.6", json);
        Assert.Contains("\"shipmentDimensions\":[", json);
        Assert.Contains("\"boxList\":[", json);
        Assert.Contains("\"itemDescription\":\"Test Item\"", json);
    }
}
