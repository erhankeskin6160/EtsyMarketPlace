namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Infrastructure.Orders;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class AiAddressParserServiceTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    private sealed class FakeConfiguration : IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    [Fact]
    public async Task ParseRawAddressAsync_ParsesUsAddressViaLocalEngine_WhenNoAiKeyPresent()
    {
        // Arrange
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var config = new FakeConfiguration();
        var service = new AiAddressParserService(client, config);

        string raw = """
            Barbara Yapana
            456 Oak Avenue
            Apt 4B
            Los Angeles, CA 90001
            United States
            Tel: +1 555-0199
            """;

        // Act
        var result = await service.ParseRawAddressAsync(raw);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Barbara Yapana", result.BuyerName);
        Assert.Equal("456 Oak Avenue", result.StreetAddress);
        Assert.Equal("Apt 4B", result.SecondAddress);
        Assert.Equal("Los Angeles", result.City);
        Assert.Equal("CA", result.State);
        Assert.Equal("90001", result.PostalCode);
        Assert.Equal("US", result.CountryCode);
        Assert.Contains("555-0199", result.Phone);
    }

    [Fact]
    public async Task ParseRawAddressAsync_ReturnsCleanEmptyDto_WhenInputIsBlank()
    {
        // Arrange
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var config = new FakeConfiguration();
        var service = new AiAddressParserService(client, config);

        // Act
        var result = await service.ParseRawAddressAsync("   ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.StreetAddress);
        Assert.Empty(result.City);
        Assert.Equal("Empty", result.Source);
    }

    [Fact]
    public async Task ParseRawAddressAsync_ExtractsEuropeanFormatProperly()
    {
        // Arrange
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var config = new FakeConfiguration();
        var service = new AiAddressParserService(client, config);

        string raw = """
            Hans Mueller
            Kurfuerstendamm 195
            10707 Berlin
            Germany
            Phone: +49 30 123456
            """;

        // Act
        var result = await service.ParseRawAddressAsync(raw);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hans Mueller", result.BuyerName);
        Assert.Equal("Kurfuerstendamm 195", result.StreetAddress);
        Assert.Equal("Berlin", result.City);
        Assert.Equal("10707", result.PostalCode);
        Assert.Contains("49 30 123456", result.Phone);
        Assert.Equal("DE", result.CountryCode);
        Assert.Equal("IM3720000224", result.IossNumber); // Otomatik Etsy AB IOSS numarası
    }

    [Fact]
    public async Task ParseRawAddressAsync_ApoMilitaryAddressWithZipPlusFour_DoesNotAssignZipToPhone()
    {
        // Arrange
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var config = new FakeConfiguration();
        var service = new AiAddressParserService(client, config);

        // Kullanıcının ekran görüntüsündeki gerçek sipariş adresi
        string raw = """
            Leon saetern vang
            PSC 41 BOX 4508
            Apo, AE 09464-0046
            United States
            """;

        // Act
        var result = await service.ParseRawAddressAsync(raw);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Leon saetern vang", result.BuyerName);
        Assert.Equal("PSC 41 BOX 4508", result.StreetAddress);
        Assert.Equal("Apo", result.City);
        Assert.Equal("AE", result.State);
        Assert.Equal("09464-0046", result.PostalCode);
        Assert.Equal("US", result.CountryCode);
        Assert.Empty(result.Phone); // 09464-0046 ZIP kodu telefona atanmamalı, telefon boş olmalı!
    }

    [Fact]
    public async Task ParseRawAddressAsync_UkAddress_AutomaticallyAssignsEtsyUkVatNumber()
    {
        // Arrange
        using var client = new HttpClient(new FakeHttpMessageHandler());
        var config = new FakeConfiguration();
        var service = new AiAddressParserService(client, config);

        string raw = """
            Emma Watson
            10 Downing Street
            SW1A 2AA London
            United Kingdom
            """;

        // Act
        var result = await service.ParseRawAddressAsync(raw);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GB", result.CountryCode);
        Assert.Equal("370 6004 28", result.IossNumber); // Otomatik Etsy UK VAT numarası
    }
}
