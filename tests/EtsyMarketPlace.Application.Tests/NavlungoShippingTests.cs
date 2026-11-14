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
            HeightCm = 10,
            DeclaredValueUsd = 1m
        };

        var fallbackQuotes = NavlungoApiClient.GenerateRealisticFallbackQuotes(req);

        Assert.NotNull(fallbackQuotes);
        Assert.Equal(4, fallbackQuotes.Count);

        var widect = Assert.Single(fallbackQuotes, q => q.Carrier == "Widect");
        Assert.Equal(16.03m, widect.Price);
        Assert.Equal(14.03m, widect.BaseSystemOfferUsd);
        Assert.Equal(2.00m, widect.PeakSeasonSurchargeUsd);
        Assert.Equal(0.67m, widect.DdpFeeUsd);
        Assert.Equal(16.70m, widect.DdpIncludedTotalUsd);

        var fedex = Assert.Single(fallbackQuotes, q => q.Carrier == "FedEx");
        Assert.Equal(21.25m, fedex.Price);
        Assert.Equal(20.25m, fedex.BaseSystemOfferUsd);
        Assert.Equal(1.00m, fedex.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, fedex.DdpFeeUsd);
        Assert.Equal(25.93m, fedex.DdpIncludedTotalUsd);

        var upsExpress = Assert.Single(fallbackQuotes, q => q.Carrier == "UPS" && q.ServiceType == "Express");
        Assert.Equal(34.84m, upsExpress.Price);
        Assert.Equal(22.04m, upsExpress.BaseSystemOfferUsd);
        Assert.Equal(11.80m, upsExpress.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsExpress.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, upsExpress.DdpFeeUsd);
        Assert.Equal(39.52m, upsExpress.DdpIncludedTotalUsd);

        var upsSaver = Assert.Single(fallbackQuotes, q => q.Carrier == "UPS" && q.ServiceType == "Express Saver");
        Assert.Equal(39.73m, upsSaver.Price);
        Assert.Equal(25.25m, upsSaver.BaseSystemOfferUsd);
        Assert.Equal(13.48m, upsSaver.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsSaver.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, upsSaver.DdpFeeUsd);
        Assert.Equal(44.41m, upsSaver.DdpIncludedTotalUsd);

        Assert.All(fallbackQuotes, q => Assert.True(q.Price > 0));
    }

    [Fact]
    public void GenerateRealisticFallbackQuotes_MatchesMemberPortalAt160DesiWithDdp()
    {
        var req = new NavlungoQuoteRequest
        {
            WeightKg = 1.0,
            LengthCm = 25,
            WidthCm = 16,
            HeightCm = 20,
            PostalCode = "78654",
            DeclaredValueUsd = 1m
        };

        var quotes = NavlungoApiClient.GenerateRealisticFallbackQuotes(req);

        Assert.Equal(4, quotes.Count);

        var widect = Assert.Single(quotes, q => q.Carrier == "Widect");
        Assert.Equal(23.20m, widect.Price);
        Assert.Equal(21.20m, widect.BaseSystemOfferUsd);
        Assert.Equal(2.00m, widect.PeakSeasonSurchargeUsd);
        Assert.Equal(1.68m, widect.DdpFeeUsd);
        Assert.Equal(24.88m, widect.DdpIncludedTotalUsd);

        var fedex = Assert.Single(quotes, q => q.Carrier == "FedEx");
        Assert.Equal(25.64m, fedex.Price);
        Assert.Equal(24.64m, fedex.BaseSystemOfferUsd);
        Assert.Equal(1.00m, fedex.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, fedex.DdpFeeUsd);
        Assert.Equal(30.32m, fedex.DdpIncludedTotalUsd);

        var upsExpress = Assert.Single(quotes, q => q.Carrier == "UPS" && q.ServiceType == "Express");
        Assert.Equal(44.46m, upsExpress.Price);
        Assert.Equal(28.53m, upsExpress.BaseSystemOfferUsd);
        Assert.Equal(14.93m, upsExpress.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsExpress.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, upsExpress.DdpFeeUsd);
        Assert.Equal(49.14m, upsExpress.DdpIncludedTotalUsd);

        var upsSaver = Assert.Single(quotes, q => q.Carrier == "UPS" && q.ServiceType == "Express Saver");
        Assert.Equal(48.48m, upsSaver.Price);
        Assert.Equal(31.16m, upsSaver.BaseSystemOfferUsd);
        Assert.Equal(16.32m, upsSaver.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsSaver.PeakSeasonSurchargeUsd);
        Assert.Equal(4.68m, upsSaver.DdpFeeUsd);
        Assert.Equal(53.16m, upsSaver.DdpIncludedTotalUsd);
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_UsesPortalShipmentQuotesApi_WhenSessionTokenPresent()
    {
        string officialJson = @"{
  ""quotes"": [
    {
      ""shipmentId"": ""0eed4cc2-47c9-4d63-bcd0-90adc8ac86f7"",
      ""serviceType"": ""economy"",
      ""lastMile"": ""thy"",
      ""customerName"": ""THY Widect Amazon Shipping"",
      ""minimumTransitTime"": 3,
      ""maximumTransitTime"": 7,
      ""quotePrice"": { ""amount"": 16.03, ""currency"": { ""value"": ""USD"" } },
      ""priceLines"": [{ ""code"": ""system-offer"", ""price"": { ""amount"": 14.03, ""currency"": { ""value"": ""USD"" } } }],
      ""surcharges"": [{ ""code"": ""peak-season-surcharge"", ""price"": { ""amount"": 2, ""currency"": { ""value"": ""USD"" } } }],
      ""valueAddedServices"": [{ ""code"": ""ddp"", ""price"": { ""amount"": 0.67, ""currency"": { ""value"": ""USD"" } } }],
      ""isDDPRequired"": true
    }
  ]
}";

        string? capturedAuth = null;
        string? capturedOrigin = null;
        string? capturedQuotesUrl = null;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == "https://api.navlungo.com/api/shipments")
            {
                capturedAuth = req.Headers.Authorization?.ToString();
                capturedOrigin = req.Headers.TryGetValues("Origin", out var origs) ? origs.FirstOrDefault() : null;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"0eed4cc2-47c9-4d63-bcd0-90adc8ac86f7\"")
                };
            }

            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsoluteUri.EndsWith("/quotes"))
            {
                capturedQuotesUrl = req.RequestUri.AbsoluteUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(officialJson)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var client = new NavlungoApiClient(httpClient);
        var settings = new NavlungoSettings { IdToken = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjMifQ.sig" };
        var req = new NavlungoQuoteRequest { WeightKg = 0.4, LengthCm = 20, WidthCm = 15, HeightCm = 10, PostalCode = "78654" };

        var offers = await client.FetchLiveQuotesAsync(req, settings);

        Assert.Equal("Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjMifQ.sig", capturedAuth);
        Assert.Equal("https://ship.navlungo.com", capturedOrigin);
        Assert.Equal("https://api.navlungo.com/api/shipments/0eed4cc2-47c9-4d63-bcd0-90adc8ac86f7/quotes", capturedQuotesUrl);
        var widect = Assert.Single(offers);
        Assert.Equal(16.03m, widect.Price);
        Assert.Equal(0.67m, widect.DdpFeeUsd);
        Assert.Equal(16.70m, widect.DdpIncludedTotalUsd);
        Assert.Contains("Üye", widect.Note);
        Assert.Contains("Canlı", widect.Note);
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
        // Anonim yol CANLI fiyat döndürür: "Üye oturumu kullanılamadı" açıklamasıyla
        // CANLI olarak işaretlenmeli (tahmini DEĞİL).
        Assert.Contains("Canlı Teklif", widect.Note);
        Assert.Contains("üye oturumu kullanılamadı", widect.Note);
        Assert.Equal(QuoteSource.Live, CarrierQuoteTrust.Classify(null, widect.Note));
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

    [Fact]
    public void ParseNavlungoResponse_ParsesOfficialShipmentsQuotesJsonWithDdpAndSurcharges()
    {
        string officialJson = @"{
  ""shipmentSummary"": {
    ""fromPostalCode"": ""06130"",
    ""fromCountryName"": ""Türkiye"",
    ""destinationPostalCode"": ""78654"",
    ""destinationCountryName"": ""Amerika Birlesik Devletleri"",
    ""totalWeight"": 1,
    ""totalProformaPrice"": 1,
    ""proformaCurrency"": ""USD""
  },
  ""quotes"": [
    {
      ""shipmentId"": ""5b6084ea-92da-4b1b-af52-28b40cb7107a"",
      ""searchId"": ""fda614c8-027d-4f7e-af2c-2c51247d2710"",
      ""quoteId"": ""dd590411-bf5c-403a-a5c3-61189beb925c"",
      ""serviceType"": ""economy"",
      ""lastMile"": ""thy"",
      ""customerName"": ""THY Widect Amazon Shipping"",
      ""minimumTransitTime"": 3,
      ""maximumTransitTime"": 7,
      ""totalVolumetricWeight"": 1.6,
      ""chargeableWeight"": 1.6,
      ""quotePrice"": { ""amount"": 23.2, ""currency"": { ""name"": ""USD"", ""value"": ""USD"" } },
      ""priceLines"": [{ ""code"": ""system-offer"", ""price"": { ""amount"": 21.2, ""currency"": { ""value"": ""USD"" } } }],
      ""surcharges"": [{ ""code"": ""peak-season-surcharge"", ""price"": { ""amount"": 2, ""currency"": { ""value"": ""USD"" } } }],
      ""valueAddedServices"": [
        { ""code"": ""ddp"", ""price"": { ""amount"": 1.73, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""insurance"", ""price"": { ""amount"": 3, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""tags"": [""best-price""],
      ""earlyTrackingEnabled"": true,
      ""tryEquivalentAmount"": 1204.312,
      ""isDDPRequired"": true
    },
    {
      ""shipmentId"": ""5b6084ea-92da-4b1b-af52-28b40cb7107a"",
      ""searchId"": ""fda614c8-027d-4f7e-af2c-2c51247d2710"",
      ""quoteId"": ""0920db43-2754-4090-af59-249d48b3eecd"",
      ""serviceType"": ""express"",
      ""lastMile"": ""fedex"",
      ""customerName"": ""Federal Express Express"",
      ""minimumTransitTime"": 1,
      ""maximumTransitTime"": 3,
      ""totalVolumetricWeight"": 1.6,
      ""chargeableWeight"": 1.6,
      ""quotePrice"": { ""amount"": 25.64, ""currency"": { ""name"": ""USD"", ""value"": ""USD"" } },
      ""priceLines"": [{ ""code"": ""system-offer"", ""price"": { ""amount"": 24.64, ""currency"": { ""value"": ""USD"" } } }],
      ""surcharges"": [{ ""code"": ""peak-season-surcharge"", ""price"": { ""amount"": 1, ""currency"": { ""value"": ""USD"" } } }],
      ""valueAddedServices"": [
        { ""code"": ""ddp"", ""price"": { ""amount"": 4.73, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""premium-tracking"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""signature-required"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""tags"": [""best-express-price""],
      ""earlyTrackingEnabled"": true,
      ""tryEquivalentAmount"": 1330.9724,
      ""isDDPRequired"": true
    },
    {
      ""shipmentId"": ""5b6084ea-92da-4b1b-af52-28b40cb7107a"",
      ""searchId"": ""fda614c8-027d-4f7e-af2c-2c51247d2710"",
      ""quoteId"": ""e9b0c8a9-d2ac-4097-8028-0819cc9a464d"",
      ""serviceType"": ""express"",
      ""lastMile"": ""ups"",
      ""customerName"": ""United Parcel Service Express"",
      ""minimumTransitTime"": 1,
      ""maximumTransitTime"": 3,
      ""totalVolumetricWeight"": 1.6,
      ""chargeableWeight"": 1.6,
      ""quotePrice"": { ""amount"": 44.46, ""currency"": { ""name"": ""USD"", ""value"": ""USD"" } },
      ""priceLines"": [{ ""code"": ""system-offer"", ""price"": { ""amount"": 28.53, ""currency"": { ""value"": ""USD"" } } }],
      ""surcharges"": [
        { ""code"": ""fuel-surcharge"", ""price"": { ""amount"": 14.93, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""peak-season-surcharge"", ""price"": { ""amount"": 1, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""valueAddedServices"": [
        { ""code"": ""ddp"", ""price"": { ""amount"": 4.73, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""premium-tracking"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""signature-required"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""tags"": [],
      ""earlyTrackingEnabled"": true,
      ""tryEquivalentAmount"": 2307.9186,
      ""isDDPRequired"": true
    },
    {
      ""shipmentId"": ""5b6084ea-92da-4b1b-af52-28b40cb7107a"",
      ""searchId"": ""fda614c8-027d-4f7e-af2c-2c51247d2710"",
      ""quoteId"": ""f77a0123-0000-4000-8000-000000000004"",
      ""serviceType"": ""express"",
      ""lastMile"": ""ups"",
      ""customerName"": ""United Parcel Service Expedited"",
      ""minimumTransitTime"": 1,
      ""maximumTransitTime"": 3,
      ""totalVolumetricWeight"": 1.6,
      ""chargeableWeight"": 1.6,
      ""quotePrice"": { ""amount"": 48.48, ""currency"": { ""name"": ""USD"", ""value"": ""USD"" } },
      ""priceLines"": [{ ""code"": ""system-offer"", ""price"": { ""amount"": 31.16, ""currency"": { ""value"": ""USD"" } } }],
      ""surcharges"": [
        { ""code"": ""fuel-surcharge"", ""price"": { ""amount"": 16.32, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""peak-season-surcharge"", ""price"": { ""amount"": 1, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""valueAddedServices"": [
        { ""code"": ""ddp"", ""price"": { ""amount"": 4.73, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""premium-tracking"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } },
        { ""code"": ""signature-required"", ""price"": { ""amount"": 5, ""currency"": { ""value"": ""USD"" } } }
      ],
      ""tags"": [],
      ""earlyTrackingEnabled"": true,
      ""tryEquivalentAmount"": 2516.5968,
      ""isDDPRequired"": true
    }
  ]
}";

        var offers = NavlungoApiClient.ParseNavlungoResponse(officialJson, new NavlungoQuoteRequest { WeightKg = 1.0 });

        Assert.Equal(4, offers.Count);

        var widect = Assert.Single(offers, o => o.Carrier == "Widect");
        Assert.Equal(23.20m, widect.Price);
        Assert.Equal(21.20m, widect.BaseSystemOfferUsd);
        Assert.Equal(2.00m, widect.PeakSeasonSurchargeUsd);
        Assert.Equal(0m, widect.FuelSurchargeUsd);
        Assert.Equal(1.73m, widect.DdpFeeUsd);
        Assert.Equal(24.93m, widect.DdpIncludedTotalUsd);
        Assert.Equal(3.00m, widect.InsuranceFeeUsd);
        Assert.True(widect.IsBestEconomy);
        Assert.True(widect.IsDdpRequired);
        Assert.True(widect.EarlyTrackingEnabled);
        Assert.Equal(1.6, widect.ChargeableWeightKg);
        Assert.Equal(1204.312m, widect.TryEquivalentAmount);

        var fedex = Assert.Single(offers, o => o.Carrier == "FedEx");
        Assert.Equal(25.64m, fedex.Price);
        Assert.Equal(24.64m, fedex.BaseSystemOfferUsd);
        Assert.Equal(1.00m, fedex.PeakSeasonSurchargeUsd);
        Assert.Equal(4.73m, fedex.DdpFeeUsd);
        Assert.Equal(30.37m, fedex.DdpIncludedTotalUsd);
        Assert.True(fedex.IsBestExpress);

        var upsExpress = Assert.Single(offers, o => o.Carrier == "UPS" && o.ServiceType == "Express");
        Assert.Equal(44.46m, upsExpress.Price);
        Assert.Equal(28.53m, upsExpress.BaseSystemOfferUsd);
        Assert.Equal(14.93m, upsExpress.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsExpress.PeakSeasonSurchargeUsd);
        Assert.Equal(4.73m, upsExpress.DdpFeeUsd);
        Assert.Equal(49.19m, upsExpress.DdpIncludedTotalUsd);

        var upsExpedited = Assert.Single(offers, o => o.Carrier == "UPS" && o.ServiceType == "Express Saver");
        Assert.Equal(48.48m, upsExpedited.Price);
        Assert.Equal(31.16m, upsExpedited.BaseSystemOfferUsd);
        Assert.Equal(16.32m, upsExpedited.FuelSurchargeUsd);
        Assert.Equal(1.00m, upsExpedited.PeakSeasonSurchargeUsd);
        Assert.Equal(4.73m, upsExpedited.DdpFeeUsd);
        Assert.Equal(53.21m, upsExpedited.DdpIncludedTotalUsd);
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
