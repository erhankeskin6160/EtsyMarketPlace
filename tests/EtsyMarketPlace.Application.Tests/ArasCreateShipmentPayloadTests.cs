namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras CreateShipment isteğinin GERÇEK JSON gövdesini doğrular.
/// Şema, panelin kendi başarılı isteğinden birebir alınmıştır (PascalCase sözleşme).
/// </summary>
public sealed class ArasCreateShipmentPayloadTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static ArasCreateShipmentRequest BuildDraftPayload()
    {
        var sender = new ArasShipmentSenderAddress
        {
            ExternalId = "5dc807f4-test",
            Id = "69c26fb6637a62ed6d370c5e",
            CityName = "ANKARA",
            CountryName = "Turkiye",
            CountryCode = "TR",
            Details = "Ankara Altındağ Çevreli Caddesi",
            PhoneNumber = "05342600561",
            FirstName = "ERHAN KESKIN",
            LastName = "",
            Title = "ankara",
            PostalCode = "06130",
            TownName = "ALTINDAĞ",
            TaxNumber = "10126091684",
            Email = "erhankeskin0661@gmail.com"
        };

        return new ArasCreateShipmentRequest
        {
            SenderAddress = sender,
            BillingAddress = null,
            ReceiverAddress = new ArasReceiverAddress
            {
                FirstName = "Inge",
                LastName = "Neuer",
                CityName = "Koblenz",
                CountryCode = "DE",
                PostalCode = "56068",
                Details = "Conrad-Scholl-Str 2",
                Type = 2
            },
            PieceCount = 1,
            InternationalShipmentCategory = "0",
            Contents =
            {
                new ArasShipmentContent
                {
                    EstimatedDimensions = new ArasEstimatedDimensions { Length = 20, Width = 15, Height = 10, Weight = 0.4 },
                    Items =
                    {
                        new ArasShipmentContentItem
                        {
                            Description = "3D Printed Gothic Gargoyle Mini Figure",
                            HsCode = "3926400000",
                            ProductBarcode = "cd0155ef-1eec-42e3-b1c6-2e84df70ae2c",
                            Quantity = 1,
                            Amount = 1,
                            UnitPrice = 11.0m,
                            ManufacturerCountry = "TR"
                        }
                    }
                }
            },
            Currency = "USD",
            IsDraftShipment = true,
            PackageType = 1,
            SenderBillingAddress = sender
        };
    }

    [Fact]
    public void DraftPayload_MatchesPanelContract()
    {
        string json = JsonSerializer.Serialize(BuildDraftPayload(), Options);

        Assert.Contains("\"SenderAddress\"", json);
        Assert.Contains("\"BillingAddress\":null", json);
        Assert.Contains("\"ReceiverAddress\"", json);
        Assert.Contains("\"PieceCount\":1", json);
        Assert.Contains("\"InternationalShipmentCategory\":\"0\"", json);
        Assert.Contains("\"Contents\"", json);
        Assert.Contains("\"EstimatedDimensions\"", json);
        Assert.Contains("\"productBarcode\"", json);
        Assert.Contains("\"IsDraftShipment\":true", json);
        Assert.Contains("\"SenderBillingAddress\"", json);

        // Taslak isteğinde ShipmentId ve taşıyıcı alanı HİÇ gönderilmez (panel sözleşmesi).
        Assert.DoesNotContain("\"ShipmentId\"", json);
        Assert.DoesNotContain("\"InternationalCargoProvider\"", json);
    }

    [Fact]
    public void UpdatePayload_IncludesShipmentIdAndProvider()
    {
        var request = BuildDraftPayload();
        request.IsDraftShipment = false;
        request.ShipmentId = "6abac6444e4bd2b9830fed2e";
        request.InternationalCargoProvider = "Widect";

        string json = JsonSerializer.Serialize(request, Options);

        Assert.Contains("\"IsDraftShipment\":false", json);
        Assert.Contains("\"ShipmentId\":\"6abac6444e4bd2b9830fed2e\"", json);
        Assert.Contains("\"InternationalCargoProvider\":\"Widect\"", json);
    }

    [Fact]
    public void DraftPayload_IsDumpedForComparison()
    {
        string json = JsonSerializer.Serialize(BuildDraftPayload(), Options);
        string dir = Path.Combine(Path.GetTempPath(), "aras-postman");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "create-shipment-request.json");
        File.WriteAllText(path, json);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 200);
    }
}
