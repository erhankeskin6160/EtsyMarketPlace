namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

public sealed class NavlungoShippingTests
{
    [Fact]
    public void VolumetricDesi_CalculatesAccurateDesi()
    {
        var req = new NavlungoQuoteRequest
        {
            LengthCm = 20,
            WidthCm = 15,
            HeightCm = 10,
            WeightKg = 0.4
        };

        // (20 * 15 * 10) / 5000 = 3000 / 5000 = 0.60
        Assert.Equal(0.60, req.VolumetricDesi);
        Assert.Equal(0.60, req.BillableWeightKg);
    }

    [Fact]
    public void ParseNavlungoResponse_ParsesRscStreamingText()
    {
        string mockRscResponse = @"
1:[""$@2"",[""$"", ""div"", null, {""children"": ""Widect 3-7 iş günü Ekonomi USD 15.03""}]]
2:[""$@3"",[""$"", ""div"", null, {""children"": ""FedEx 1-3 iş günü Express USD 20.75""}]]
3:[""$@4"",[""$"", ""div"", null, {""children"": ""UPS 1-3 iş günü Express USD 32.96""}]]
";
        var req = new NavlungoQuoteRequest();
        var offers = NavlungoApiClient.ParseNavlungoResponse(mockRscResponse, req);

        Assert.NotEmpty(offers);
        Assert.Contains(offers, o => o.Carrier.Equals("Widect", StringComparison.OrdinalIgnoreCase) && o.Price == 15.03m);
        Assert.Contains(offers, o => o.Carrier.Equals("FedEx", StringComparison.OrdinalIgnoreCase) && o.Price == 20.75m);
        Assert.Contains(offers, o => o.Carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase) && o.Price == 32.96m);
    }

    [Fact]
    public void ParseNavlungoResponse_ParsesRscJsonChunksWithLastMileMapping()
    {
        string mockRscJsonChunk = @"
0:{""status"":""ok""}
1:[{""lastMile"":""thy"",""price"":15.03,""currency"":""USD"",""serviceType"":""economy"",""minTransitTime"":3,""maxTransitTime"":7,""tags"":[""best-economy-price""]},
   {""lastMile"":""fedex"",""price"":20.75,""currency"":""USD"",""serviceType"":""express"",""minTransitTime"":1,""maxTransitTime"":3,""tags"":[""best-express-price""]},
   {""lastMile"":""ups"",""price"":32.96,""currency"":""USD"",""serviceType"":""express"",""minTransitTime"":1,""maxTransitTime"":3}]
";
        var req = new NavlungoQuoteRequest();
        var offers = NavlungoApiClient.ParseNavlungoResponse(mockRscJsonChunk, req);

        Assert.NotEmpty(offers);
        Assert.Equal(3, offers.Count);

        var widect = offers.FirstOrDefault(o => o.Carrier.Equals("Widect", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(widect);
        Assert.Equal(15.03m, widect.Price);
        Assert.Equal("USD", widect.Currency);
        Assert.Equal("Ekonomi", widect.ServiceType);
        Assert.Equal("3-7 iş günü", widect.DeliveryEstimate);
        Assert.True(widect.IsBestEconomy);

        var fedex = offers.FirstOrDefault(o => o.Carrier.Equals("FedEx", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(fedex);
        Assert.Equal(20.75m, fedex.Price);
        Assert.Equal("Express", fedex.ServiceType);
        Assert.Equal("1-3 iş günü", fedex.DeliveryEstimate);
        Assert.True(fedex.IsBestExpress);

        var ups = offers.FirstOrDefault(o => o.Carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ups);
        Assert.Equal(32.96m, ups.Price);
    }

    [Fact]
    public void NextActionIds_AreConfiguredProperly()
    {
        Assert.False(string.IsNullOrWhiteSpace(NavlungoApiClient.AnonymousActionId));
        Assert.False(string.IsNullOrWhiteSpace(NavlungoApiClient.AuthenticatedActionId));
        Assert.NotEqual(NavlungoApiClient.AnonymousActionId, NavlungoApiClient.AuthenticatedActionId);
    }

    [Fact]
    public void GenerateRealisticFallbackQuotes_ContainsWidectFedExAndUps()
    {
        var req = new NavlungoQuoteRequest
        {
            WeightKg = 0.4,
            LengthCm = 20,
            WidthCm = 15,
            HeightCm = 10
        };

        var fallbackQuotes = NavlungoApiClient.GenerateRealisticFallbackQuotes(req);

        Assert.NotNull(fallbackQuotes);
        Assert.True(fallbackQuotes.Count >= 3);
        Assert.Contains(fallbackQuotes, q => q.Carrier == "Widect");
        Assert.Contains(fallbackQuotes, q => q.Carrier == "FedEx");
        Assert.Contains(fallbackQuotes, q => q.Carrier == "UPS");
        Assert.All(fallbackQuotes, q => Assert.True(q.Price > 0));
    }

    [Fact]
    public void NavlungoSettingsStore_SavesAndLoadsCorrectly()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"navlungo-test-{Guid.NewGuid()}.json");
        try
        {
            var settings = new NavlungoSettings
            {
                IdToken = "test_id_token_jwt_12345",
                SessionCookie = "_SessionUser_3277588=test_cookie_abc",
                SavedEmail = "test@navlungo.com"
            };

            NavlungoSettingsStore.Save(settings, tempPath);
            var loaded = NavlungoSettingsStore.Load(tempPath);

            Assert.Equal("test_id_token_jwt_12345", loaded.IdToken);
            Assert.Equal("_SessionUser_3277588=test_cookie_abc", loaded.SessionCookie);
            Assert.Equal("test@navlungo.com", loaded.SavedEmail);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_ThrowsWhenApiReturnsErrorInsteadOfUsingFallback()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized")
            }));
        var client = new NavlungoApiClient(httpClient);

        var ex = await Assert.ThrowsAsync<NavlungoApiException>(() =>
            client.FetchLiveQuotesAsync(new NavlungoQuoteRequest()));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("unauthorized", ex.ResponseBody);
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_ThrowsWhenResponseCannotBeParsedInsteadOfUsingFallback()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-a-navlungo-response")
            }));
        var client = new NavlungoApiClient(httpClient);

        var ex = await Assert.ThrowsAsync<NavlungoApiException>(() =>
            client.FetchLiveQuotesAsync(new NavlungoQuoteRequest()));

        Assert.Contains("ayrıştırılamadı", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_response);
    }
}
