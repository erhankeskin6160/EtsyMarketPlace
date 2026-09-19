namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
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
}
