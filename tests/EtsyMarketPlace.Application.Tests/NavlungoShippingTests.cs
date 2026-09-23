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
        Assert.Equal("406fad5769e32876f9c8eeccd5dab4d086b9068ca5", NavlungoApiClient.AnonymousActionId);
        Assert.Equal("40198494a378e36987abc2fe2dd302fceb194b28c4", NavlungoApiClient.AuthenticatedActionId);
    }

    [Fact]
    public void ParseNavlungoResponse_ParsesLiveNextJsRscFormat()
    {
        string liveRsc = @"
0:{""a"":""$@1""}
1:[{""serviceType"":""economy"",""lastMile"":""thy"",""price"":19.96,""currency"":""USD"",""maxTransitTime"":7,""minTransitTime"":3,""tags"":[],""description"":""widect-usps - THY Widect Usps""},
   {""serviceType"":""express"",""lastMile"":""fedex"",""price"":31.37,""currency"":""USD"",""maxTransitTime"":3,""minTransitTime"":1,""tags"":[""best-express-price""],""description"":""express - Federal Express Express""},
   {""serviceType"":""express"",""lastMile"":""ups"",""price"":34.66,""currency"":""USD"",""maxTransitTime"":3,""minTransitTime"":1,""tags"":[],""description"":""express - United Parcel Service Express""}]
";
        var req = new NavlungoQuoteRequest();
        var offers = NavlungoApiClient.ParseNavlungoResponse(liveRsc, req);

        Assert.NotEmpty(offers);
        Assert.Equal(3, offers.Count);

        var widect = offers.FirstOrDefault(o => o.Carrier.Equals("Widect", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(widect);
        Assert.Equal(19.96m, widect.Price);
        Assert.Equal("USD", widect.Currency);
        Assert.Equal("3-7 iş günü", widect.DeliveryEstimate);

        var fedex = offers.FirstOrDefault(o => o.Carrier.Equals("FedEx", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(fedex);
        Assert.Equal(31.37m, fedex.Price);
        Assert.True(fedex.IsBestExpress);

        var ups = offers.FirstOrDefault(o => o.Carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ups);
        Assert.Equal(34.66m, ups.Price);
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
        Assert.True(fallbackQuotes.Count >= 4);
        var widect = fallbackQuotes.FirstOrDefault(q => q.Carrier == "Widect");
        Assert.NotNull(widect);
        Assert.Equal(15.03m, widect.Price);

        var fedex = fallbackQuotes.FirstOrDefault(q => q.Carrier == "FedEx");
        Assert.NotNull(fedex);
        Assert.Equal(20.75m, fedex.Price);

        var upsExpress = fallbackQuotes.FirstOrDefault(q => q.Carrier == "UPS" && q.ServiceType == "Express");
        Assert.NotNull(upsExpress);
        Assert.Equal(34.00m, upsExpress.Price);

        var upsSaver = fallbackQuotes.FirstOrDefault(q => q.Carrier == "UPS" && q.ServiceType == "Express Saver");
        Assert.NotNull(upsSaver);
        Assert.Equal(38.85m, upsSaver.Price);

        Assert.All(fallbackQuotes, q => Assert.True(q.Price > 0));
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_FallsBackToAnonymous_WhenAuthenticatedReturns500()
    {
        string mockRscJsonChunk = @"
1:[{""lastMile"":""thy"",""price"":19.96,""currency"":""USD"",""serviceType"":""economy"",""minTransitTime"":3,""maxTransitTime"":7,""tags"":[]}]
";
        using var httpClient = new HttpClient(new StubHttpMessageHandler(req =>
        {
            // If authenticated request (has Cookie or Next-Action is auth), return 500
            if (req.Headers.Contains("Cookie"))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("1:E{\"digest\":\"3601470081\"}")
                };
            }
            // Fallback anonymous request returns 200 with live quotes
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockRscJsonChunk)
            };
        }));

        var client = new NavlungoApiClient(httpClient);
        var settings = new NavlungoSettings { SessionCookie = "invalid_cookie=123" };
        var offers = await client.FetchLiveQuotesAsync(new NavlungoQuoteRequest(), settings);

        Assert.NotEmpty(offers);
        var widect = offers.First();
        Assert.Equal("Widect", widect.Carrier);
        Assert.Equal(19.96m, widect.Price);
        Assert.Contains("Oturum Çerezi Yenilenmeli", widect.Note);
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
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
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
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-a-navlungo-response")
            }));
        var client = new NavlungoApiClient(httpClient);

        var ex = await Assert.ThrowsAsync<NavlungoApiException>(() =>
            client.FetchLiveQuotesAsync(new NavlungoQuoteRequest()));

        Assert.Contains("ayrıştırılamadı", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavlungoCookieSanitizer_ExtractsCookieFromCurl()
    {
        string curl = @"curl 'https://quick-price-calculator.navlungo.com/tr?source=user' \
  -H 'accept: text/x-component' \
  -H 'cookie: nv_attr_lt=abc123; EXP_e0771a79f8_identity=def456' \
  --data-raw '[{""fromCountry"":""TR""}]'";

        string extracted = NavlungoCookieSanitizer.Sanitize(curl);
        Assert.Equal("nv_attr_lt=abc123; EXP_e0771a79f8_identity=def456", extracted);
    }

    [Fact]
    public void NavlungoCookieSanitizer_ExtractsCookieFromHttpHeaders()
    {
        string rawHeaders = @"authority: quick-price-calculator.navlungo.com
method: POST
path: /tr?source=user
scheme: https
accept: text/x-component
cookie: nv_attr_lt=xyz789; _ga=GA1.1.123; EXP_e0771a79f8_identity=jwt999
content-length: 118";

        string extracted = NavlungoCookieSanitizer.Sanitize(rawHeaders);
        Assert.Equal("nv_attr_lt=xyz789; _ga=GA1.1.123; EXP_e0771a79f8_identity=jwt999", extracted);
    }

    [Fact]
    public void NavlungoCookieSanitizer_CleansRawCookieWithNewlines()
    {
        string multiline = "nv_attr_lt=abc;\r\n  EXP_identity=123;\n _ga=456";
        string extracted = NavlungoCookieSanitizer.Sanitize(multiline);
        Assert.Equal("nv_attr_lt=abc; EXP_identity=123; _ga=456", extracted);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(HttpResponseMessage response) => _responseFactory = _ => response;
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) => _responseFactory = responseFactory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_responseFactory(request));
    }
}
