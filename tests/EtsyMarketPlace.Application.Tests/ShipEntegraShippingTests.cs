namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using Xunit;

public sealed class ShipEntegraShippingTests
{
    [Fact]
    public void ComputeDesi_CalculatesCorrectIataVolumetricWeight()
    {
        // 15 cm x 20 cm x 10 cm / 5000 = 3000 / 5000 = 0.60 desi
        double desi = ShipEntegraQuoteRequest.ComputeDesi(15.0, 20.0, 10.0);
        Assert.Equal(0.60, desi);
    }

    [Fact]
    public void ComputeBillableWeight_ReturnsMaxOfActualAndDesi()
    {
        // Actual 0.40 kg, Desi 0.60 kg -> Billable should be 0.60 kg
        double billable = ShipEntegraQuoteRequest.ComputeBillableWeight(0.40, 15.0, 20.0, 10.0);
        Assert.Equal(0.60, billable);

        // Actual 1.20 kg, Desi 0.60 kg -> Billable should be 1.20 kg
        double billable2 = ShipEntegraQuoteRequest.ComputeBillableWeight(1.20, 15.0, 20.0, 10.0);
        Assert.Equal(1.20, billable2);
    }

    [Fact]
    public void CleanToken_StripsBearerPrefixAndTrims()
    {
        var settings = new ShipEntegraSettings
        {
            BearerToken = "  Bearer vApukILayT... "
        };

        string clean = settings.CleanToken;
        Assert.Equal("vApukILayT...", clean);
        Assert.DoesNotContain("Bearer", clean);
    }

    [Fact]
    public async Task GetFallbackOffers_ReturnsExpectedBaseRatesWithFallbackFlag()
    {
        var service = new ShipEntegraPricingService();
        var req = new ShipEntegraQuoteRequest
        {
            ReceiverCountry = "US",
            WeightKg = 0.40,
            LengthCm = 15.0,
            WidthCm = 20.0,
            HeightCm = 10.0
        };

        var offers = await service.GetFallbackOffersAsync(req);

        Assert.NotEmpty(offers);
        Assert.Equal(6, offers.Count);
        Assert.All(offers, o => Assert.False(o.IsLivePrice));

        // En uygun servis: Amerika Eko Plus ($12.96 USD)
        var ekoPlus = offers.FirstOrDefault(o => o.ServiceName.Contains("eko-plus", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ekoPlus);
        Assert.Equal(12.96m, ekoPlus.TotalPrice);
        Assert.True(ekoPlus.IsBestCarrier);
        Assert.Equal("USD", ekoPlus.Currency);

        // Smart Express ($19.01 USD)
        var smart = offers.FirstOrDefault(o => o.ServiceName.Contains("smart-express", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(smart);
        Assert.Equal(19.01m, smart.TotalPrice);

        // Widect ($19.55 USD)
        var widect = offers.FirstOrDefault(o => o.ServiceName.Contains("widect", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(widect);
        Assert.Equal(19.55m, widect.TotalPrice);
    }

    [Fact]
    public void ShipEntegraSettingsStore_SaveAndLoad_RoundtripsSuccessfully()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "ShipEntegraTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var testFilePath = Path.Combine(testDir, "test-shipentegra-settings.json");

        try
        {
            var initial = ShipEntegraSettingsStore.Load(testFilePath);
            Assert.NotNull(initial);
            Assert.Empty(initial.BearerToken);

            initial.BearerToken = "test_shipentegra_token_xyz";
            initial.DefaultCountry = "US";
            initial.DefaultWeightKg = 0.65;
            initial.DefaultLengthCm = 25.0;
            initial.DefaultWidthCm = 18.0;
            initial.DefaultHeightCm = 12.0;

            ShipEntegraSettingsStore.Save(initial, testFilePath);

            var reloaded = ShipEntegraSettingsStore.Load(testFilePath);
            Assert.Equal("test_shipentegra_token_xyz", reloaded.BearerToken);
            Assert.Equal("US", reloaded.DefaultCountry);
            Assert.Equal(0.65, reloaded.DefaultWeightKg);
            Assert.Equal(25.0, reloaded.DefaultLengthCm);
            Assert.Equal(18.0, reloaded.DefaultWidthCm);
            Assert.Equal(12.0, reloaded.DefaultHeightCm);
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
    public void ShipEntegraTokenExpiredException_InstantiatesWithProperMessage()
    {
        var ex = new ShipEntegraTokenExpiredException("401 Unauthorized session expired");
        Assert.Contains("401", ex.Message);
    }
}
