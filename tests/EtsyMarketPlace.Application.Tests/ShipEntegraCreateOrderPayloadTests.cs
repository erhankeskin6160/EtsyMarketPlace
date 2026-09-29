namespace EtsyMarketPlace.Application.Tests;

using System.Text.Json;
using Xunit;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra sipariş ve etiket isteklerinin panel sözleşmesine uyduğunu doğrular.
/// Şema, panelin gerçek ağ trafiğinden (28-29.09 gece yakalaması) alınmıştır.
/// </summary>
public sealed class ShipEntegraCreateOrderPayloadTests
{
    [Fact]
    public void CreateOrderPayload_MatchesPanelContract()
    {
        var request = new ShipEntegraCreateOrderRequest
        {
            ShipTo = new ShipEntegraShipTo
            {
                Name = "Inge Neuer",
                Address1 = "Conrad-Scholl-Str 2",
                City = "Koblenz",
                ZipCode = "56068",
                Country = "DE"
            },
            Currency = "USD",
            Reference = "4176634453",
            Description = "3D Printed Gothic Gargoyle Mini Figure",
            Products =
            {
                new ShipEntegraOrderProduct { Name = "Figur", Quantity = 1, UnitPrice = 11m, HsCode = "0302530000" }
            },
            Packages =
            {
                new ShipEntegraOrderPackage { PackageQuantity = 1, Weight = 0.4, Width = 15, Length = 20, Height = 10 }
            }
        };

        string json = JsonSerializer.Serialize(request);

        Assert.Contains("\"shipTo\":", json);
        Assert.Contains("\"zipCode\":\"56068\"", json);
        Assert.Contains("\"country\":\"DE\"", json);
        Assert.Contains("\"products\":[", json);
        Assert.Contains("\"hsCode\":\"0302530000\"", json);
        Assert.Contains("\"packages\":[", json);
        Assert.Contains("\"packageQuantity\":1", json);
        Assert.Contains("\"reference\":\"4176634453\"", json);
        Assert.Contains("\"shippingType\":1", json);

        // Boş bırakılan opsiyonel alanlar istekte yer almaz.
        Assert.DoesNotContain("\"state\"", json);
        Assert.DoesNotContain("\"email\"", json);
        Assert.DoesNotContain("\"address2\"", json);
    }

    [Fact]
    public void CreateOrderPayload_IncludesStateAndEmail_WhenProvided()
    {
        var request = new ShipEntegraCreateOrderRequest
        {
            ShipTo = new ShipEntegraShipTo
            {
                Name = "John Doe",
                Address1 = "742 Evergreen Terrace",
                City = "Springfield",
                State = "OR",
                ZipCode = "97477",
                Country = "US",
                Email = "customer@example.com"
            },
            Currency = "USD",
            Reference = "1",
            Description = "Item",
            Products = { new ShipEntegraOrderProduct { Name = "Item", Quantity = 1, UnitPrice = 5m, HsCode = "0302530000" } },
            Packages = { new ShipEntegraOrderPackage { PackageQuantity = 1, Weight = 1, Width = 11, Length = 11, Height = 11 } }
        };

        string json = JsonSerializer.Serialize(request);

        Assert.Contains("\"state\":\"OR\"", json);
        Assert.Contains("\"shippingType\":1", json);
        Assert.Contains("\"email\":\"customer@example.com\"", json);
    }

    [Fact]
    public void CreateLabelPayload_MatchesPanelContract()
    {
        var request = new ShipEntegraCreateLabelRequest
        {
            SpecialService = "shipentegra-express",
            Content = "Figur",
            Weight = 0.4,
            Currency = "USD",
            Items =
            {
                new ShipEntegraLabelItem
                {
                    ItemId = 761193992,
                    DeclaredPrice = 11m,
                    DeclaredQuantity = 1,
                    OrderId = 592003769,
                    Gtip = "0302530000"
                }
            },
            OrderId = 592003769,
            ServiceType = 1,
            Country = "DE"
        };

        string json = JsonSerializer.Serialize(request);

        Assert.Contains("\"specialService\":\"shipentegra-express\"", json);
        Assert.Contains("\"itemId\":761193992", json);
        Assert.Contains("\"orderId\":592003769", json);
        Assert.Contains("\"gtip\":\"0302530000\"", json);
        Assert.Contains("\"verpackg\":-1", json);
        Assert.Contains("\"errorMessage\":{}", json);
        Assert.Contains("\"insurance\":false", json);
        Assert.Contains("\"noTracking\":false", json);
    }
}
