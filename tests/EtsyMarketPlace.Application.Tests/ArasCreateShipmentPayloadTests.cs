namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Text.Json;
using Xunit;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras CreateShipment isteğinin GERÇEK JSON gövdesini doğrular.
/// Amaç: "volumetricweightismissing" hatasının payload'dan mı kaynaklandığını
/// kesin olarak görebilmek ve Postman'de aynen tekrarlanabilecek gövdeyi üretmek.
/// </summary>
public sealed class ArasCreateShipmentPayloadTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Provider'ın ürettiği gövdenin aynısı (ArasGlobalShipmentCreationProvider ile aynı değerler).</summary>
    private static ArasCreateShipmentRequest BuildPayload()
    {
        double length = 20.0, width = 15.0, height = 10.0, weight = 0.40;
        double desi = Math.Round((length * width * height) / 5000.0, 2);

        var box = new ArasBox
        {
            Length = length,
            Width = width,
            Height = height,
            Weight = weight,
            VolumetricWeight = desi,
            Desi = desi,
            PackageCount = 1
        };

        var request = new ArasCreateShipmentRequest
        {
            Currency = "USD",
            Price = 11.0m,
            TotalPrice = 11.0m,
            CargoPrice = 13.13m,
            InternationalCargoProvider = "widect",
            InternationalShipmentCategory = "4",
            IsMicroExport = true,
            PackageCount = 1,
            Weight = weight,
            VolumetricWeight = desi,
            Desi = desi
        };

        request.ShipmentDimensions.Add(box);
        request.BoxList.Add(box);

        request.ShipmentItems.Add(new ArasShipmentItem
        {
            Description = "3D print figur",
            ItemDescription = "3D print figur",
            HsCode = "3926400000",
            Quantity = 1,
            UnitPrice = 25.0m,
            Length = box.Length,
            Width = box.Width,
            Height = box.Height,
            Weight = box.Weight,
            VolumetricWeight = desi,
            Desi = desi,
            Category = "1"
        });

        request.SenderAddress = new ArasAddress
        {
            Title = "Merkez",
            FirstName = "ERHAN",
            LastName = "KESKIN",
            Address = "Ankara",
            CityName = "Ankara",
            DistrictName = "Altindag",
            CountryCode = "TR",
            FromCountryCode = "TR",
            PostalCode = "06000",
            Phone = "05342600561",
            Email = "erhankeskin6160@gmail.com"
        };

        request.ReceiverAddress = new ArasAddress
        {
            FirstName = "Inge",
            LastName = "Neuer",
            Address = "Conrad-Scholl-Str 2",
            CityName = "Koblenz",
            CountryCode = "DE",
            PostalCode = "56068",
            Phone = "",
            Email = "",
            TaxId = "IM13720000224",
            IsResidentialAddress = true
        };

        return request;
    }

    [Fact]
    public void Payload_ContainsVolumetricWeightAtEveryLevel()
    {
        string json = JsonSerializer.Serialize(BuildPayload(), Options);

        Assert.Contains("\"volumetricWeight\"", json);
        Assert.Contains("\"desi\"", json);
        Assert.Contains("\"shipmentDimensions\"", json);
        Assert.Contains("\"shipmentItems\"", json);

        // Alan gerçekten dolu mu (0 değil)?
        Assert.Contains("\"volumetricWeight\":0.6", json);
    }

    [Fact]
    public void Payload_IsDumpedForPostmanComparison()
    {
        string json = JsonSerializer.Serialize(BuildPayload(), Options);
        string dir = Path.Combine(Path.GetTempPath(), "aras-postman");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "create-shipment-request.json");
        File.WriteAllText(path, json);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 200);
    }
}
