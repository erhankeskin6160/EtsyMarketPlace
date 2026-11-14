namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
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

    [Theory]
    [InlineData("Shipentegra", "shipentegra-express")]
    [InlineData("shipentegra-ups-express", "shipentegra-ups")]
    [InlineData("shipentegra-eko-plus", "shipentegra-amerika-eko-plus")]
    [InlineData("shipentegra-amerika-eko-plus", "shipentegra-amerika-eko-plus")]
    public void NormalizeTariffCarrierSlug_MapsShipEntegraAliasesCorrectly(string input, string expectedSlug)
    {
        Assert.Equal(expectedSlug, ShipEntegraTariffCalculator.NormalizeTariffCarrierSlug(input));
    }

    [Fact]
    public void ComputeFallbackTariff_ComputesExpectedUsDutyAndCarrierFee_ForOneAndFifteenUsd()
    {
        var oneUsd = ShipEntegraTariffCalculator.ComputeFallbackTariff(new ShipEntegraTariffRequest
        {
            Carrier = "shipentegra-amerika-eko-plus",
            HtsCode = "3926909990",
            Destination = "US",
            UnitPrice = 1.00m,
            Quantity = 1
        });

        Assert.True(oneUsd.Success);
        Assert.Equal(5.3m, oneUsd.GeneralDutyRatePercent);
        Assert.Equal(0.05m, oneUsd.GeneralDutyUsd);
        Assert.Equal(12.5m, oneUsd.AdditionalDutyRatePercent);
        Assert.Equal(0.13m, oneUsd.AdditionalDutyUsd);
        Assert.Equal(0.18m, oneUsd.TaxAndDutySubtotalUsd);
        Assert.Equal(0.45m, oneUsd.CarrierProcessingFeeUsd);
        Assert.Equal(0.63m, oneUsd.FastCustomsPaymentUsd);

        var fifteenUsd = ShipEntegraTariffCalculator.ComputeFallbackTariff(new ShipEntegraTariffRequest
        {
            Carrier = "shipentegra-express",
            HtsCode = "3926909990",
            Destination = "US",
            UnitPrice = 15.00m,
            Quantity = 1
        });

        Assert.Equal(0.80m, fifteenUsd.GeneralDutyUsd);
        Assert.Equal(1.88m, fifteenUsd.AdditionalDutyUsd);
        Assert.Equal(2.68m, fifteenUsd.TaxAndDutySubtotalUsd);
        Assert.Equal(4.50m, fifteenUsd.CarrierProcessingFeeUsd);
        Assert.Equal(7.18m, fifteenUsd.FastCustomsPaymentUsd);
    }

    [Fact]
    public async Task CalculateTariffByCarrierAsync_ParsesLiveDutiesAndNormalizesCarrierSlug()
    {
        string? capturedRequestBody = null;
        var stubHandler = new StubShipEntegraHttpHandler(async req =>
        {
            capturedRequestBody = await req.Content!.ReadAsStringAsync();
            using var parsed = JsonDocument.Parse(capturedRequestBody);
            decimal unitPrice = parsed.RootElement.GetProperty("unitPrice").GetDecimal();
            string carrier = parsed.RootElement.GetProperty("carrier").GetString() ?? "";

            string json = unitPrice >= 15.0m
                ? """
                  {
                    "status": "success",
                    "data": {
                      "duties": [
                        { "type": "general_duty", "htsCode": "3926909971", "price": 0.80, "adValRate": 5.3 },
                        { "type": "additional_duty", "htsCode": "99030579", "price": 1.88, "adValRate": 12.5 },
                        { "type": "carrier_fee", "htsCode": "", "price": 1.45, "adValRate": 0 }
                      ]
                    }
                  }
                  """
                : carrier == "shipentegra-express"
                ? """
                  {
                    "status": "success",
                    "data": {
                      "duties": [
                        { "type": "general_duty", "htsCode": "3926909971", "price": 0.05, "adValRate": 5.3 },
                        { "type": "additional_duty", "htsCode": "99030579", "price": 0.13, "adValRate": 12.5 },
                        { "type": "carrier_fee", "htsCode": "", "price": 0.50, "adValRate": 0 }
                      ]
                    }
                  }
                  """
                : """
                  {
                    "status": "success",
                    "data": {
                      "duties": [
                        { "type": "general_duty", "htsCode": "3926909971", "price": 0.05, "adValRate": 5.3 },
                        { "type": "additional_duty", "htsCode": "99030579", "price": 0.13, "adValRate": 12.5 },
                        { "type": "carrier_fee", "htsCode": "", "price": 1.45, "adValRate": 0 }
                      ]
                    }
                  }
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(stubHandler);
        var client = new ShipEntegraApiClient(httpClient);
        var validFormatToken = "test_bearer_token_over_20_chars_long_12345";

        var expressOne = await client.CalculateTariffByCarrierAsync(new ShipEntegraTariffRequest
        {
            Carrier = "Shipentegra",
            HtsCode = "3926909971",
            Destination = "US",
            UnitPrice = 1.00m,
            Quantity = 1
        }, validFormatToken);

        Assert.NotNull(capturedRequestBody);
        Assert.Contains("\"carrier\":\"shipentegra-express\"", capturedRequestBody);
        Assert.Equal(0.05m, expressOne.GeneralDutyUsd);
        Assert.Equal(0.13m, expressOne.AdditionalDutyUsd);
        Assert.Equal(0.50m, expressOne.CarrierProcessingFeeUsd);
        Assert.Equal(0.68m, expressOne.FastCustomsPaymentUsd);

        var ekoOne = await client.CalculateTariffByCarrierAsync(new ShipEntegraTariffRequest
        {
            Carrier = "shipentegra-eko-plus",
            HtsCode = "3926909971",
            Destination = "US",
            UnitPrice = 1.00m,
            Quantity = 1
        }, validFormatToken);

        Assert.Equal(1.45m, ekoOne.CarrierProcessingFeeUsd);
        Assert.Equal(1.63m, ekoOne.FastCustomsPaymentUsd);

        var fifteenRes = await client.CalculateTariffByCarrierAsync(new ShipEntegraTariffRequest
        {
            Carrier = "shipentegra-ups-express",
            HtsCode = "3926909971",
            Destination = "US",
            UnitPrice = 15.00m,
            Quantity = 1
        }, validFormatToken);

        Assert.Contains("\"carrier\":\"shipentegra-ups\"", capturedRequestBody);
        Assert.Equal(0.80m, fifteenRes.GeneralDutyUsd);
        Assert.Equal(1.88m, fifteenRes.AdditionalDutyUsd);
        Assert.Equal(1.45m, fifteenRes.CarrierProcessingFeeUsd);
        Assert.Equal(4.13m, fifteenRes.FastCustomsPaymentUsd);
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_ParsesFreightBreakdownAndTariffCustomsTotals()
    {
        var stubHandler = new StubShipEntegraHttpHandler(req =>
        {
            string url = req.RequestUri!.AbsolutePath;
            string json = url.Contains("/tools/calculate/all", StringComparison.OrdinalIgnoreCase)
                ? """
                  {
                    "status": "success",
                    "code": 200,
                    "data": {
                      "bestCarrier": "Shipentegra",
                      "prices": [
                        {
                          "serviceName": "Shipentegra",
                          "clearServiceName": "Express",
                          "serviceType": "EXPRESS",
                          "cargoPrice": 17.54,
                          "fuelCost": 5.27,
                          "fuelMultiplier": 30.05,
                          "additionalFee": 1.05,
                          "additionalFeeDescription": "Yoğun sezon ek ücreti",
                          "discountedCargoPrice": 16.96,
                          "totalPrice": 23.86,
                          "pricing": "ShipEntegra Template 261",
                          "currency": "USD",
                          "additionalDescription": "Amerika içi geri iade ücretsiz<br>100 $'a kadar sigortalı<br>Tahmini Teslim Süresi 2-4 iş günü",
                          "tooltip": "Express"
                        }
                      ]
                    }
                  }
                  """
                : """
                  {
                    "status": "success",
                    "data": {
                      "duties": [
                        { "type": "general_duty", "htsCode": "3926909988", "price": 0.05, "adValRate": 5.3 },
                        { "type": "additional_duty", "htsCode": "99030579", "price": 0.13, "adValRate": 12.5 },
                        { "type": "carrier_fee", "htsCode": "", "price": 0.50, "adValRate": 0 }
                      ]
                    }
                  }
                  """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(stubHandler);
        var client = new ShipEntegraApiClient(httpClient);
        var offers = await client.FetchLiveQuotesAsync(new ShipEntegraQuoteRequest
        {
            ReceiverCountry = "US",
            WeightKg = 1.0,
            WidthCm = 15.0,
            LengthCm = 20.0,
            HeightCm = 10.0,
            HsCode = "3926909988",
            DeclaredValueUsd = 1.00m,
            Quantity = 1
        }, "test_bearer_token_over_20_chars_long_98765");

        var offer = Assert.Single(offers);
        Assert.Equal(17.54m, offer.CargoPrice);
        Assert.Equal(5.27m, offer.FuelCost);
        Assert.Equal(30.05m, offer.FuelMultiplierPercent);
        Assert.Equal(1.05m, offer.AdditionalFeeUsd);
        Assert.Equal("Yoğun sezon ek ücreti", offer.AdditionalFeeDescription);
        Assert.Equal(23.86m, offer.TotalPrice);
        Assert.Equal(23.28m, offer.DiscountedTotalPriceUsd);
        Assert.Equal("Amerika içi geri iade ücretsiz", offer.ReturnPolicyNote);
        Assert.Equal(0.05m, offer.GeneralDutyUsd);
        Assert.Equal(0.13m, offer.AdditionalDutyUsd);
        Assert.Equal(0.18m, offer.TaxAndDutySubtotalUsd);
        Assert.Equal(0.50m, offer.CarrierProcessingFeeUsd);
        Assert.Equal(0.68m, offer.FastCustomsPaymentUsd);
        Assert.Equal(24.54m, offer.GrandTotalUsd);
        Assert.Equal(23.96m, offer.DiscountedGrandTotalUsd);
    }

    [Fact]
    public void CarrierQuotesQuery_DeserializesRawNumberAndWrappedObjectDeclaredValue()
    {
        var rawQuery = JsonSerializer.Deserialize<CarrierQuotesQuery>(
            """{"countryCode":"US","weightKg":0.5,"declaredValueUsd":15,"hsCode":"3926909990","quantity":2}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(rawQuery);
        Assert.NotNull(rawQuery.DeclaredValueUsd);
        Assert.Equal(15m, rawQuery.DeclaredValueUsd.Value.Value);
        Assert.Equal("3926909990", rawQuery.HsCode);
        Assert.Equal(2, rawQuery.Quantity);

        var wrappedQuery = JsonSerializer.Deserialize<CarrierQuotesQuery>(
            """{"countryCode":"US","weightKg":0.5,"declaredValueUsd":{"value":45.50}}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(wrappedQuery);
        Assert.NotNull(wrappedQuery.DeclaredValueUsd);
        Assert.Equal(45.50m, wrappedQuery.DeclaredValueUsd.Value.Value);
    }

    private sealed class StubShipEntegraHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request);
    }
}
