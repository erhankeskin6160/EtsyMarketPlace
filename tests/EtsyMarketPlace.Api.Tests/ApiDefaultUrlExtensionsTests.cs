namespace EtsyMarketPlace.Api.Tests;

using System.Collections.Generic;
using EtsyMarketPlace.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

public class ApiDefaultUrlExtensionsTests
{
    private static IConfiguration Build(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void WhenNoUrlConfigured_ReturnsPublicPort5263()
    {
        var result = ApiDefaultUrlExtensions.ResolveDefaultUrlIfMissing(Build(new()));
        Assert.Equal("http://0.0.0.0:5263", result);
    }

    [Fact]
    public void WhenUrlsArgumentProvided_DoesNotOverride()
    {
        var result = ApiDefaultUrlExtensions.ResolveDefaultUrlIfMissing(Build(new() { ["urls"] = "http://0.0.0.0:6000" }));
        Assert.Null(result);
    }

    [Fact]
    public void WhenKestrelEndpointsConfigured_DoesNotOverride()
    {
        var result = ApiDefaultUrlExtensions.ResolveDefaultUrlIfMissing(Build(new()
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://0.0.0.0:7000"
        }));
        Assert.Null(result);
    }
}
