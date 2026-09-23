namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

public sealed class ShiptomoreShippingTests
{
    [Fact]
    public void VolumetricDesi_CalculatesAccurateDesi()
    {
        var req = new ShiptomoreQuoteRequest
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
    public void ShiptomoreSettingsStore_SavesAndLoadsCorrectly()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"stm-test-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new ShiptomoreSettings
            {
                SessionCookie = "session_id=mock_session_12345",
                SavedEmail = "test@shiptomore.com",
                TokenLastUpdatedUtc = DateTime.UtcNow
            };

            ShiptomoreSettingsStore.Save(settings, tempPath);
            Assert.True(File.Exists(tempPath));

            var loaded = ShiptomoreSettingsStore.Load(tempPath);
            Assert.Equal("session_id=mock_session_12345", loaded.SessionCookie);
            Assert.Equal("test@shiptomore.com", loaded.SavedEmail);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void CookieSanitizer_ExtractsSessionIdFromCurl()
    {
        string rawCurl = "curl 'https://shiptomore.com/parcel/calculate' -H 'Cookie: frontend_lang=tr_TR; session_id=abc123xyz456' --data-raw '{}'";
        string sanitized = NavlungoCookieSanitizer.Sanitize(rawCurl);

        Assert.Contains("session_id=abc123xyz456", sanitized);
    }

    [Fact]
    public void GenerateRealisticFallbackQuotes_ProducesAccurateMemberAndVisitorRates()
    {
        var req = new ShiptomoreQuoteRequest
        {
            FromCountry = "TR",
            ToCountry = "US",
            WeightKg = 0.4,
            LengthCm = 20,
            WidthCm = 15,
            HeightCm = 10
        };

        // Üye Fiyatı Testi ($13.00 & $18.55 baz)
        var memberOffers = ShiptomoreApiClient.GenerateRealisticFallbackQuotes(req, isMember: true);
        Assert.Equal(2, memberOffers.Count);
        var memberWidect = memberOffers.First(o => o.Carrier.Contains("Widect", StringComparison.OrdinalIgnoreCase));
        var memberFedex = memberOffers.First(o => o.Carrier.Contains("FedEx", StringComparison.OrdinalIgnoreCase));
        Assert.True(memberWidect.Price <= 15.00m, "Üye Widect fiyatı $15'ten küçük olmalı");
        Assert.True(memberFedex.Price <= 20.00m, "Üye FedEx fiyatı $20'den küçük olmalı");
        Assert.True(memberWidect.IsMemberRate);

        // Anonim / Ziyaretçi Fiyatı Testi ($16.96 & $24.19 baz)
        var visitorOffers = ShiptomoreApiClient.GenerateRealisticFallbackQuotes(req, isMember: false);
        var visitorWidect = visitorOffers.First(o => o.Carrier.Contains("Widect", StringComparison.OrdinalIgnoreCase));
        var visitorFedex = visitorOffers.First(o => o.Carrier.Contains("FedEx", StringComparison.OrdinalIgnoreCase));
        Assert.True(visitorWidect.Price > memberWidect.Price, "Ziyaretçi fiyatı üye fiyatından yüksek olmalı");
        Assert.True(visitorFedex.Price > memberFedex.Price, "Ziyaretçi fiyatı üye fiyatından yüksek olmalı");
        Assert.False(visitorWidect.IsMemberRate);
    }

    [Fact]
    public async Task FetchLiveQuotesAsync_WithValidRequest_ReturnsParsedOffers()
    {
        var client = new ShiptomoreApiClient();
        var req = new ShiptomoreQuoteRequest
        {
            FromCountry = "TR",
            ToCountry = "US",
            WeightKg = 0.4,
            LengthCm = 20,
            WidthCm = 15,
            HeightCm = 10
        };

        var offers = await client.FetchLiveQuotesAsync(req);

        Assert.NotEmpty(offers);
        Assert.Contains(offers, o => o.Carrier.Contains("Widect", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(offers, o => o.Carrier.Contains("FedEx", StringComparison.OrdinalIgnoreCase));
        foreach (var off in offers)
        {
            Assert.True(off.Price > 0, "Teklif fiyatı 0'dan büyük olmalıdır");
            Assert.Equal("USD", off.Currency);
        }
    }
}
