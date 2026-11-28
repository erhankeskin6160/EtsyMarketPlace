namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

public class NavlungoShipmentCreationProviderTests
{
    private static ShipmentCreationContext CreateSampleContext(bool notifyEtsy = false) => new()
    {
        Order = new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4226762400,
            BuyerName = "Bryan Sanon",
            BuyerEmail = "order.0@etsy-buyer.com",
            Phone = "+15551234567",
            StreetAddress = "3562 E presidio circle",
            City = "Mesa",
            State = "AZ",
            PostalCode = "85213",
            CountryCode = "US",
            TotalPrice = 10.00m
        },
        WeightKg = 0.4,
        LengthCm = 10.5,
        WidthCm = 25,
        HeightCm = 24,
        HsCode = "3926400000",
        SelectedSubCarrier = "widect",
        SelectedServiceCode = "nav-widect",
        ServiceType = "Widect — Navlungo",
        IsDdp = true,
        NotifyEtsy = notifyEtsy
    };

    [Fact]
    public async Task CreateShipmentAsync_ReturnsSessionError_WhenNavlungoSessionMissing()
    {
        var fakeApi = new FakeNavlungoApiClient();
        var provider = new NavlungoPortalShipmentCreationProvider(fakeApi, () => new NavlungoSettings());

        var result = await provider.CreateShipmentAsync(CreateSampleContext());

        Assert.False(result.IsSuccess);
        Assert.Contains("oturumu bağlı değil", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(fakeApi.CapturedRequest);
    }

    [Fact]
    public async Task CreateShipmentAsync_RequiresStateCode_ForUsDestination()
    {
        var fakeApi = new FakeNavlungoApiClient();
        var settings = new NavlungoSettings { SessionCookie = "session=valid" };
        var provider = new NavlungoPortalShipmentCreationProvider(fakeApi, () => settings);
        var ctx = CreateSampleContext();
        ctx.Order.State = "";

        var result = await provider.CreateShipmentAsync(ctx);

        Assert.False(result.IsSuccess);
        Assert.Contains("eyalet", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(fakeApi.CapturedRequest);
    }

    [Fact]
    public async Task CreateShipmentAsync_SucceedsThroughShipmentCreationManager_WhenRegistered()
    {
        var fakeApi = new FakeNavlungoApiClient
        {
            NextResponse = new NavlungoCreateShipmentResponse
            {
                IsSuccess = true,
                ShipmentId = "bcecacf1-6161-4d17-ae6d-563fc70c61cc",
                TrackingNumber = "9261290422808606839179",
                LabelUrl = "https://s3.amazonaws.com/navlungo/label.pdf"
            }
        };
        var settings = new NavlungoSettings { SessionCookie = "session=valid" };
        var manager = new ShipmentCreationManager(new IShipmentCreationProvider[]
        {
            new NavlungoPortalShipmentCreationProvider(fakeApi, () => settings)
        });

        var result = await manager.CreateShipmentAsync("Navlungo", CreateSampleContext());

        Assert.True(result.IsSuccess);
        Assert.Equal("bcecacf1-6161-4d17-ae6d-563fc70c61cc", result.ShipmentId);
        Assert.Equal("9261290422808606839179", result.TrackingNumber);
        Assert.Equal("https://s3.amazonaws.com/navlungo/label.pdf", result.LabelUrl);
        Assert.NotNull(fakeApi.CapturedRequest);
        Assert.Equal("US", fakeApi.CapturedRequest!.ReceiverCountryCode);
        Assert.Equal("AZ", fakeApi.CapturedRequest.ReceiverStateCode);
        Assert.Equal("+14805550199", fakeApi.CapturedRequest.ReceiverPhone);
        Assert.True(fakeApi.CapturedRequest.ProductDescription.Length >= 15);
    }

    [Fact]
    public async Task NavlungoApiClient_CreateShipmentAsync_ReturnsPortalQuoteUrl_WhenSelectQuoteFails()
    {
        using var http = new HttpClient(new StubHandler(req =>
        {
            string url = req.RequestUri!.AbsoluteUri;
            if (req.Method == HttpMethod.Get && url.Contains("stored-sender-"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(@"[{""id"":1,""contactName"":""Test"",""city"":""Ankara""}]") };
            if (req.Method == HttpMethod.Post && url == "https://api.navlungo.com/api/shipments/init")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"draft-guid-42\"") };
            if (req.Method == HttpMethod.Get && url.EndsWith("/quotes"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""quotes"":[{""shipmentId"":""draft-guid-42"",""searchId"":""s1"",""searchReference"":""SR1"",""quoteId"":""q1"",""serviceType"":""economy"",""lastMile"":""thy"",""quotePrice"":{""amount"":19.18,""currency"":{""value"":""USD""}}}]}")
                };
            if (req.Method == HttpMethod.Post && url.EndsWith("/select-quote"))
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(@"{""message"":""Yetersiz bakiye""}") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var client = new NavlungoApiClient(http);
        var resp = await client.CreateShipmentAsync(new NavlungoCreateShipmentRequest
        {
            ReceiverName = "Bryan Sanon",
            ReceiverCountryCode = "GB",
            ReceiverPhone = "+447700900123",
            SelectedSubCarrier = "widect"
        }, new NavlungoSettings { SessionCookie = "session=valid" });

        Assert.False(resp.IsSuccess);
        Assert.Equal("draft-guid-42", resp.ShipmentId);
        Assert.Equal("https://ship.navlungo.com/ship/quotes/draft-guid-42", resp.PortalQuoteUrl);
        Assert.Contains("Yetersiz bakiye", resp.ErrorMessage);
    }

    [Fact]
    public void NavlungoSettings_IsSessionValid_RemainsTrue_WhenValidCookiePresentAlongsideExpiredJwt()
    {
        // exp = 1600000000 (2020 — expired)
        string expiredJwt = "eyJhbGciOiJSUzI1NiJ9.eyJleHAiOjE2MDAwMDAwMDB9.sig";
        var settings = new NavlungoSettings
        {
            IdToken = $"\"{expiredJwt}\"\r\n",
            SessionCookie = "session_id=active-cookie",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(6)
        };

        Assert.True(NavlungoSettings.LooksLikeJwt(settings.IdToken));
        Assert.Equal(expiredJwt, settings.CleanToken);
        Assert.True(settings.IsSessionValid);
    }

    [Fact]
    public void NavlungoSettings_IsSessionValid_ReturnsFalse_WhenOnlyExpiredJwtPresent()
    {
        string expiredJwt = "eyJhbGciOiJSUzI1NiJ9.eyJleHAiOjE2MDAwMDAwMDB9.sig";
        var settings = new NavlungoSettings
        {
            IdToken = $"Bearer {expiredJwt}"
        };

        Assert.False(settings.IsSessionValid);
    }

    [Fact]
    public void ExtractPortalErrorSummary_FormatsAspNetCoreValidationProblemDetailsErrors()
    {
        string problemJson = @"{
            ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.1"",
            ""title"": ""One or more validation errors occurred."",
            ""status"": 400,
            ""errors"": {
                ""SenderShippingAddress"": [""The SenderShippingAddress field is required.""],
                ""Packages[0].Type"": [""Invalid package type.""]
            }
        }";

        string summary = NavlungoApiClient.ExtractPortalErrorSummary(problemJson);

        Assert.Contains("SenderShippingAddress: The SenderShippingAddress field is required.", summary);
        Assert.Contains("Packages[0].Type: Invalid package type.", summary);
        Assert.DoesNotContain("One or more validation errors occurred.", summary);
    }

    [Fact]
    public async Task NavlungoApiClient_CreateShipmentAsync_ExecutesPortalInitQuotesSelectQuoteAndConsignment()
    {
        var calledUrls = new List<string>();
        string? capturedInitJson = null;
        string? capturedSelectQuoteJson = null;

        using var http = new HttpClient(new StubHandler(req =>
        {
            string url = req.RequestUri!.AbsoluteUri;
            calledUrls.Add($"{req.Method} {url}");

            if (req.Method == HttpMethod.Get && url == "https://api.navlungo.com/api/Shippers/stored-sender-shipping-address")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"[{""id"":223721,""contactName"":""Erhan Keskin"",""city"":""Ankara"",""postalCode"":""06000"",""country"":{""code"":""TR"",""alphaThree"":""TUR"",""isInEuropeanUnion"":false},""addressLine"":{""firstLine"":""Test Cad. No:1"",""secondLine"":""""},""phoneCode"":""+90"",""phoneNumber"":""5551112233"",""email"":""sender@example.com"",""identificationNumber"":""11111111110"",""addressType"":""individual"",""isFavorited"":true}]")
                };
            }

            if (req.Method == HttpMethod.Get && url == "https://api.navlungo.com/api/Shippers/stored-sender-invoice-address")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"[{""id"":223720,""contactName"":""Erhan Keskin"",""city"":""Ankara"",""postalCode"":""06000"",""country"":{""code"":""TR"",""alphaThree"":""TUR"",""isInEuropeanUnion"":false},""addressLine"":{""firstLine"":""Test Cad. No:1""},""phoneCode"":""+90"",""phoneNumber"":""5551112233"",""email"":""sender@example.com"",""identificationNumber"":""11111111110"",""addressType"":""individual"",""isFavorited"":true}]")
                };
            }

            if (req.Method == HttpMethod.Get && url.StartsWith("https://quote-search-api.navlungo.com/htsuscodes/v1"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""items"":[{""htsCode"":""3926.40.00.00""}]}")
                };
            }

            if (req.Method == HttpMethod.Post && url == "https://api.navlungo.com/api/shipments/init")
            {
                capturedInitJson = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"bcecacf1-6161-4d17-ae6d-563fc70c61cc\"")
                };
            }

            if (req.Method == HttpMethod.Get && url.EndsWith("/quotes"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{
                        ""quotes"": [{
                            ""shipmentId"": ""bcecacf1-6161-4d17-ae6d-563fc70c61cc"",
                            ""searchId"": ""search-123"",
                            ""searchReference"": ""SREF-999"",
                            ""quoteId"": ""quote-widect-1"",
                            ""serviceType"": ""economy"",
                            ""lastMile"": ""thy"",
                            ""quotePrice"": { ""amount"": 19.18, ""currency"": { ""value"": ""USD"" } },
                            ""valueAddedServices"": [{ ""code"": ""ddp"", ""price"": { ""amount"": 2.27, ""currency"": { ""value"": ""USD"" } } }]
                        }]
                    }")
                };
            }

            if (req.Method == HttpMethod.Post && url.EndsWith("/select-quote"))
            {
                capturedSelectQuoteJson = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{ ""lastMileTrackingNumber"": ""TRK-NAVLUNGO-999"" }")
                };
            }

            if (req.Method == HttpMethod.Get && url.EndsWith("/consignment"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{ ""files"": [{ ""remoteFileUrl"": ""https://s3.example.com/awb.pdf"" }] }")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var client = new NavlungoApiClient(http);
        var settings = new NavlungoSettings { SessionCookie = "session=valid" };
        var req = new NavlungoCreateShipmentRequest
        {
            ReceiverName = "Bryan Sanon",
            ReceiverEmail = "order.0@etsy-buyer.com",
            ReceiverPhone = "+14805550199",
            ReceiverStreet1 = "3562 E presidio circle",
            ReceiverCity = "Mesa",
            ReceiverStateCode = "AZ",
            ReceiverPostalCode = "85213",
            ReceiverCountryCode = "US",
            HsCode = "3926400000",
            SelectedSubCarrier = "widect",
            SelectedServiceCode = "nav-widect",
            IsDdp = true
        };

        var resp = await client.CreateShipmentAsync(req, settings);

        Assert.True(resp.IsSuccess);
        Assert.Equal("bcecacf1-6161-4d17-ae6d-563fc70c61cc", resp.ShipmentId);
        Assert.Equal("TRK-NAVLUNGO-999", resp.TrackingNumber);
        Assert.Equal("https://s3.example.com/awb.pdf", resp.LabelUrl);
        Assert.NotNull(capturedInitJson);
        Assert.Contains("\"proformaCurrencyCode\":\"USD\"", capturedInitJson);
        Assert.Contains("\"senderShippingAddress\":", capturedInitJson);
        Assert.Contains("\"senderInvoiceAddress\":", capturedInitJson);
        Assert.Contains("\"products\":[", capturedInitJson);
        Assert.Contains("\"gtipCode\":\"3926.40.00.00\"", capturedInitJson);
        Assert.Contains("\"packageType\":\"box\"", capturedInitJson);
        Assert.NotNull(capturedSelectQuoteJson);
        Assert.Contains("\"searchReference\":\"SREF-999\"", capturedSelectQuoteJson);
        Assert.Contains("\"quoteId\":\"quote-widect-1\"", capturedSelectQuoteJson);
        Assert.Contains("\"selectedValueAddedServices\":[\"ddp\"]", capturedSelectQuoteJson);
        Assert.Contains("POST https://api.navlungo.com/api/shipments/bcecacf1-6161-4d17-ae6d-563fc70c61cc/select-quote", calledUrls);
    }

    [Fact]
    public async Task NavlungoApiClient_CreateShipmentAsync_ReturnsClearWarning_WhenStoredSenderAddressMissing()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }));

        var client = new NavlungoApiClient(http);
        var resp = await client.CreateShipmentAsync(new NavlungoCreateShipmentRequest
        {
            ReceiverName = "Bryan Sanon",
            ReceiverCountryCode = "US"
        }, new NavlungoSettings { SessionCookie = "session=valid" });

        Assert.False(resp.IsSuccess);
        Assert.Contains("Adreslerim", resp.ErrorMessage);
    }

    private sealed class FakeNavlungoApiClient : INavlungoApiClient
    {
        public NavlungoCreateShipmentRequest? CapturedRequest { get; private set; }
        public NavlungoCreateShipmentResponse NextResponse { get; set; } = new() { IsSuccess = true };

        public Task<List<NavlungoQuoteOffer>> FetchLiveQuotesAsync(
            NavlungoQuoteRequest request,
            NavlungoSettings? settings = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<NavlungoQuoteOffer>());

        public Task<NavlungoCreateShipmentResponse> CreateShipmentAsync(
            NavlungoCreateShipmentRequest request,
            NavlungoSettings settings,
            CancellationToken cancellationToken = default)
        {
            CapturedRequest = request;
            return Task.FromResult(NextResponse);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_fn(request));
    }
}
