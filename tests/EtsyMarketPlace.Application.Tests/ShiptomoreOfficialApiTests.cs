namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Ship to More resmî API istemcisinin sözleşmeye uyduğunu doğrular:
/// alan adları, "sale" gönderim tipi, IOSS'un doğru alana yazılması, Basic auth
/// ve 422 hata gövdesinin okunabilir mesaja çevrilmesi.
/// Ağa çıkılmaz; sahte mesaj işleyici kullanılır.
/// </summary>
public sealed class ShiptomoreOfficialApiTests
{
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body)> _responses = new();

        public List<string> RequestBodies { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public ScriptedHandler(params (HttpStatusCode Code, string Body)[] responses)
        {
            foreach (var r in responses)
            {
                _responses.Enqueue(r);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            var (code, body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (ShiptomoreOfficialApiClient Client, ScriptedHandler Handler) Build(
        params (HttpStatusCode Code, string Body)[] responses)
    {
        var handler = new ScriptedHandler(responses);
        var settings = new ShiptomoreSettings
        {
            ClientId = "client-id",
            EncryptedClientSecret = "s3cr3t"
        };

        var client = new ShiptomoreOfficialApiClient(new HttpClient(handler), () => settings)
        {
            BaseUrl = "https://api.test"
        };

        return (client, handler);
    }

    private static ShiptomoreShipmentRequest SampleShipment() => new()
    {
        ProviderSlug = "ups",
        ServiceSlug = "ups_express_3day",
        ReceiverCountryCode = "DE",
        ReceiverName = "Inge Neuer",
        ReceiverPhone = "+4900000",
        ReceiverEmail = "inge@example.de",
        ReceiverStreet = "Conrad-Scholl-Str 2",
        ReceiverCity = "Koblenz",
        ReceiverZip = "56068",
        CollectIdNumber = "IM3720000224",
        CustomerReference = "#4176634453",
        IncludeLabels = true,
        LabelFormat = "thermal",
        Parcels = { new ShiptomoreParcel { Weight = 0.6, Height = 10, Width = 15, Length = 20 } },
        ProductLines = { new ShiptomoreProductLine { Description = "3D Figur", Qty = 1, UnitPrice = 11m, HsCode = "3926400000" } }
    };

    [Fact]
    public async Task PriceRequest_UsesSpecFieldNames_AndMapsOptions()
    {
        var (client, handler) = Build((HttpStatusCode.OK,
            """{"data":[{"provider_slug":"ups","service_slug":"ups_express_3day","price":12.85,"currency":"USD","billing_weight":0.6}]}"""));

        var options = await client.CalculatePricesAsync(new ShiptomorePriceRequest
        {
            CountryCode = "DE",
            Parcels = new List<ShiptomoreParcelDimensions>
            {
                new ShiptomoreParcelDimensions { Weight = 0.4, Height = 10, Width = 15, Length = 20 }
            }
        });

        string body = handler.RequestBodies[0];
        Assert.Contains("\"country_code\":\"DE\"", body);
        Assert.Contains("\"package_type\":\"custom\"", body);
        Assert.Contains("\"parcels\":[", body);
        Assert.Contains("\"weight\":0.4", body);
        Assert.Contains("\"qty\":1", body);

        var option = Assert.Single(options);
        Assert.Equal("ups", option.ProviderSlug);
        Assert.Equal("ups_express_3day", option.ServiceSlug);
        Assert.Equal(12.85m, option.Price);
        Assert.Equal(0.6, option.BillingWeight, 3);
    }

    [Fact]
    public async Task ShipmentRequest_SendsSaleTypeAndIossCollectId()
    {
        var (client, handler) = Build((HttpStatusCode.Created,
            """{"id":"abc123","odoo_name":"S0001","state":"confirmed","tracking_numbers":["TRK1"]}"""));

        var result = await client.CreateShipmentAsync(SampleShipment());

        string body = handler.RequestBodies[0];
        Assert.Contains("\"shipment_type\":\"sale\"", body);
        Assert.Contains("\"collect_id_number\":\"IM3720000224\"", body);
        Assert.Contains("\"collect_id_type\":\"ioss\"", body);
        Assert.Contains("\"customer_reference\":\"#4176634453\"", body);
        Assert.Contains("\"include_labels\":true", body);
        Assert.Contains("\"label_format\":\"thermal\"", body);
        Assert.Contains("\"hs_code\":\"3926400000\"", body);

        Assert.Equal("abc123", result.Id);
        Assert.Single(result.TrackingNumbers);
    }

    [Fact]
    public async Task Auth_UsesBasicWithClientIdAndSecret()
    {
        var (client, handler) = Build((HttpStatusCode.OK, "[]"));
        await client.GetProvidersAsync();

        var auth = handler.Requests[0].Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth!.Scheme);

        string expectedSecret = ShippingSecretProtector.Current?.Unprotect("s3cr3t") ?? "s3cr3t";
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!));
        Assert.Equal($"client-id:{expectedSecret}", decoded);
    }

    [Fact]
    public async Task ValidationError_IsMappedToReadableMessage()
    {
        var (client, _) = Build((HttpStatusCode.UnprocessableEntity,
            """{"detail":[{"loc":["body","receiver_zip"],"msg":"field required"}]}"""));

        var ex = await Assert.ThrowsAsync<ShiptomoreApiException>(() => client.CreateShipmentAsync(SampleShipment()));

        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("receiver_zip", ex.Message);
        Assert.Contains("field required", ex.Message);
    }

    [Fact]
    public async Task Label_IsBase64Decoded()
    {
        string pdfBytes = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        var (client, _) = Build((HttpStatusCode.OK, $$"""{"data":"{{pdfBytes}}","format":"pdf"}"""));

        byte[] label = await client.DownloadLabelAsync("abc123", "thermal");

        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, label);
    }

    [Fact]
    public async Task MissingCredentials_ThrowsWithGuidance()
    {
        var handler = new ScriptedHandler((HttpStatusCode.OK, "[]"));
        var client = new ShiptomoreOfficialApiClient(new HttpClient(handler), () => new ShiptomoreSettings())
        {
            BaseUrl = "https://api.test"
        };

        Assert.False(client.HasCredentials);
        var ex = await Assert.ThrowsAsync<ShiptomoreApiException>(() => client.GetProvidersAsync());
        Assert.Contains("Client ID", ex.Message);
    }

    [Fact]
    public async Task CreateShipment_RequiresSlugInResponse()
    {
        var (client, _) = Build((HttpStatusCode.Created, "{}"));

        await Assert.ThrowsAsync<ShiptomoreApiException>(() => client.CreateShipmentAsync(SampleShipment()));
    }

    [Fact]
    public async Task HasCredentials_TrueWhenBothValuesPresent()
    {
        var (client, _) = Build((HttpStatusCode.OK, "[]"));
        Assert.True(client.HasCredentials);
    }
}
