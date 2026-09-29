namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// GTİP kodunun Ship to More kod listesine çözümlenmesi:
/// 10-12 haneli TR kodları sağlayıcının 8 haneli kodlarıyla eşleştirilir;
/// ayraçlar yok sayılır; eşleşme yoksa kullanıcıya anlaşılır hata döner.
/// </summary>
public sealed class ShiptomoreHsCodeResolverTests
{
    private sealed class StubApi : IShiptomoreOfficialApi
    {
        public bool HasCredentials { get; set; } = true;
        public string BaseUrl { get; set; } = "https://api.test";
        public Dictionary<string, List<ShiptomoreHsCode>> Results { get; } = new();
        public List<string> Queries { get; } = new();

        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
        {
            Queries.Add(query);
            return Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(
                Results.TryGetValue(query, out var list) ? list : new List<ShiptomoreHsCode>());
        }

        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(Array.Empty<ShiptomorePriceOption>());

        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentResponse());

        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentDetail());

        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void BuildCandidates_TenDigitCode_AddsEightAndSixDigitSlices()
    {
        var candidates = ShiptomoreHsCodeResolver.BuildCandidates("3926400000");

        Assert.Equal(new[] { "3926400000", "39264000", "392640" }, candidates);
    }

    [Fact]
    public void KeepDigits_IgnoresSeparators()
    {
        Assert.Equal("3926400000", ShiptomoreHsCodeResolver.KeepDigits("3926.40.00.00"));
        Assert.Equal("39264000", ShiptomoreHsCodeResolver.KeepDigits("3926 40 00"));
        Assert.Equal(string.Empty, ShiptomoreHsCodeResolver.KeepDigits(null));
    }

    [Fact]
    public async Task Resolve_TenDigitCode_MatchesProviderEightDigitCode()
    {
        var api = new StubApi();
        api.Results["39264000"] = new List<ShiptomoreHsCode> { new() { Code = "39264000" } };

        var result = await ShiptomoreHsCodeResolver.ResolveAsync("3926400000", api);

        Assert.True(result.IsResolved);
        Assert.Equal("39264000", result.Code);
        Assert.Equal(new[] { "3926400000", "39264000" }, api.Queries);
    }

    [Fact]
    public async Task Resolve_ExactCodeInProviderList_ReturnsSameCode()
    {
        var api = new StubApi();
        api.Results["39264000"] = new List<ShiptomoreHsCode> { new() { Code = "39264000" } };

        var result = await ShiptomoreHsCodeResolver.ResolveAsync("3926.40.00", api);

        Assert.True(result.IsResolved);
        Assert.Equal("39264000", result.Code);
        Assert.Equal(new[] { "39264000" }, api.Queries);
    }

    [Fact]
    public async Task Resolve_UnknownCode_FailsWithProviderSuggestions()
    {
        var api = new StubApi();
        api.Results["392640"] = new List<ShiptomoreHsCode>
        {
            new() { Code = "39264000", Description = "Statuettes" }
        };

        var result = await ShiptomoreHsCodeResolver.ResolveAsync("3926409999", api);

        Assert.False(result.IsResolved);
        Assert.Contains("tanımıyor", result.ErrorMessage);
        Assert.Contains("39264000", result.ErrorMessage);
    }

    [Fact]
    public async Task Resolve_TooShortCode_FailsWithoutCallingApi()
    {
        var api = new StubApi();

        var result = await ShiptomoreHsCodeResolver.ResolveAsync("3", api);

        Assert.False(result.IsResolved);
        Assert.Empty(api.Queries);
    }
}
