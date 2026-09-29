namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
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
    public void ArasCreateShipmentRequest_SerializesToPanelContract()
    {
        var request = new ArasCreateShipmentRequest
        {
            SenderAddress = new ArasShipmentSenderAddress
            {
                CityName = "ANKARA",
                CountryName = "Turkiye",
                CountryCode = "TR",
                Details = "Ankara",
                FirstName = "ERHAN KESKIN",
                Title = "ankara",
                PostalCode = "06130",
                TownName = "ALTINDAĞ",
                Email = "e@x.com"
            },
            ReceiverAddress = new ArasReceiverAddress
            {
                FirstName = "John",
                LastName = "Doe",
                CityName = "Berlin",
                CountryCode = "DE",
                PostalCode = "10115",
                Details = "Str 1",
                Type = 2
            },
            PieceCount = 1,
            InternationalShipmentCategory = "0",
            Currency = "USD",
            IsDraftShipment = true,
            PackageType = 1
        };
        request.SenderBillingAddress = request.SenderAddress;
        request.Contents.Add(new ArasShipmentContent
        {
            EstimatedDimensions = new ArasEstimatedDimensions { Length = 20, Width = 15, Height = 10, Weight = 0.4 },
            Items =
            {
                new ArasShipmentContentItem
                {
                    Description = "Test Item",
                    HsCode = "3926400000",
                    ProductBarcode = "barcode-1",
                    Quantity = 1,
                    Amount = 1,
                    UnitPrice = 11.16m,
                    ManufacturerCountry = "TR"
                }
            }
        });

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        string json = System.Text.Json.JsonSerializer.Serialize(request, options);

        Assert.Contains("\"IsDraftShipment\":true", json);
        Assert.Contains("\"Contents\":[", json);
        Assert.Contains("\"EstimatedDimensions\"", json);
        Assert.Contains("\"productBarcode\":\"barcode-1\"", json);
        Assert.Contains("\"PieceCount\":1", json);
        Assert.DoesNotContain("\"volumetricWeight\"", json);
        Assert.DoesNotContain("\"ShipmentId\"", json);
    }

    [Fact]
    public async Task GetQuotesAsync_RefreshesExpiredTokenAndReturnsLiveOffers()
    {
        string expired = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(-5));
        var settings = new ArasGlobalSettings { BearerToken = expired, SavedEmail = "test@example.com" };
        var client = new FakeArasApiClient();
        client.Offers.Add(new ArasGlobalQuoteOffer
        {
            Cargo = "UPS",
            Price = 21.31m,
            Currency = "USD",
            ProviderServiceType = "Express",
            IsLivePrice = true
        });

        var service = new ArasGlobalPricingService(client, () => settings);
        service.TokenRefresher = (s, ct) => Task.FromResult<string?>("fresh-live-token-1234567890");

        var result = await service.GetQuotesAsync(new ArasGlobalQuoteRequest { ReceiverCountry = "US" });

        Assert.True(result.IsLive);
        Assert.Single(result.Offers);
        Assert.Equal(1, client.FetchCalls);
        Assert.Equal("fresh-live-token-1234567890", client.LastToken);
    }

    [Fact]
    public async Task GetQuotesAsync_RetriesOnceWhenTokenExpiresMidFlight()
    {
        var settings = new ArasGlobalSettings
        {
            BearerToken = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30)),
            SavedEmail = "test@example.com"
        };

        var client = new FakeArasApiClient();
        client.FetchBehaviors.Enqueue(new ArasGlobalTokenExpiredException("HTTP 401 Unauthorized"));
        client.Offers.Add(new ArasGlobalQuoteOffer
        {
            Cargo = "UPS",
            Price = 19.0m,
            Currency = "USD",
            IsLivePrice = true
        });

        var service = new ArasGlobalPricingService(client, () => settings);
        int refreshes = 0;
        service.TokenRefresher = (s, ct) =>
        {
            refreshes++;
            return Task.FromResult<string?>("fresh-2-token-123456789012");
        };

        var result = await service.GetQuotesAsync(new ArasGlobalQuoteRequest { ReceiverCountry = "US" });

        Assert.True(result.IsLive);
        Assert.Equal(1, refreshes);
        Assert.Equal(2, client.FetchCalls);
        Assert.Equal("fresh-2-token-123456789012", client.LastToken);
    }

    [Fact]
    public async Task GetQuotesAsync_WithoutRefresher_FallsBackWhenTokenMissing()
    {
        var settings = new ArasGlobalSettings { BearerToken = string.Empty };
        var service = new ArasGlobalPricingService(new FakeArasApiClient(), () => settings);

        var result = await service.GetQuotesAsync(new ArasGlobalQuoteRequest { ReceiverCountry = "US" });

        Assert.False(result.IsLive);
        Assert.True(result.TokenExpired);
        Assert.NotEmpty(result.Offers);
        Assert.Contains("token", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetQuotesAsync_RefresherReturningNull_FallsBack()
    {
        var settings = new ArasGlobalSettings { BearerToken = string.Empty, SavedEmail = "test@example.com" };
        var service = new ArasGlobalPricingService(new FakeArasApiClient(), () => settings);
        int calls = 0;
        service.TokenRefresher = (s, ct) =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        };

        var result = await service.GetQuotesAsync(new ArasGlobalQuoteRequest { ReceiverCountry = "US" });

        Assert.False(result.IsLive);
        Assert.Equal(1, calls);
    }

    private sealed class FakeArasApiClient : IArasGlobalApiClient
    {
        public int FetchCalls { get; private set; }
        public string? LastToken { get; private set; }
        public List<ArasGlobalQuoteOffer> Offers { get; } = new();
        public Queue<Exception?> FetchBehaviors { get; } = new();

        public Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(
            ArasGlobalQuoteRequest request,
            string rawBearerToken,
            CancellationToken cancellationToken = default)
        {
            FetchCalls++;
            LastToken = rawBearerToken;
            if (FetchBehaviors.Count > 0)
            {
                Exception? behavior = FetchBehaviors.Dequeue();
                if (behavior != null)
                {
                    throw behavior;
                }
            }

            return Task.FromResult(Offers.ToList());
        }

        public Task<decimal> TranslateCurrencyAsync(decimal price, string entryCurrency, string exitCurrency, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task<List<ArasGtipSearchResult>> SearchGtipCodeAsync(string keyword, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ArasGtipSearchResult>());

        public Task<ArasAdditionalOptions> GetAdditionalOptionsAsync(string destinationCountry, string provider, decimal orderTotalUsd, string currency, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult<ArasAdditionalOptions>(null!);

        public Task<string> GetAdditionalInformationAsync(string provider, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<ArasShipmentSenderAddress?> GetPrimarySenderAddressAsync(string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult<ArasShipmentSenderAddress?>(null);

        public Task<ArasCreateShipmentResponse> CreateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult<ArasCreateShipmentResponse>(null!);

        public Task<bool> UpdateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> StartPriceCalculationForShipmentAsync(ArasStartCalculationPayload payload, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<(bool IsConcluded, List<ArasGlobalQuoteOffer> Offers)> PollBasePriceListAsync(string referenceCode, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult((false, new List<ArasGlobalQuoteOffer>()));

        public Task<ArasShipmentPriceBreakdown> CalculateShipmentPriceAsync(string shipmentId, string provider, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult<ArasShipmentPriceBreakdown>(null!);

        public Task<string> GetShipmentLegalDocumentAsync(string shipmentId, string docType, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> SendShipmentPriceAsync(string shipmentId, string provider, decimal finalPrice, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
