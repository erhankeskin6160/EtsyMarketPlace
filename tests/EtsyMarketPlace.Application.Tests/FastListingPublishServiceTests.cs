namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class FastListingPublishServiceTests
{
    private sealed class FakeEtsyListingApiClient : IEtsyListingApiClient
    {
        public long CreatedListingId { get; set; } = 99887766;
        public string CreatedUrl { get; set; } = "https://www.etsy.com/listing/99887766";
        public bool UpdateInventorySuccess { get; set; } = true;
        public string? UpdateInventoryError { get; set; } = null;
        public EtsyInventoryPayload? LastInventoryPayload { get; private set; }

        public Task<(long ListingId, string Url)> CreateDraftListingAsync(string shopId, CreateDraftListingRawRequest raw, CancellationToken ct) =>
            Task.FromResult((CreatedListingId, CreatedUrl));

        public Task<bool> UploadListingImageAsync(string shopId, long listingId, byte[] imageBytes, string fileName, int rank, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<(bool Success, string? ErrorMessage)> UpdateListingInventoryAsync(string shopId, long listingId, EtsyInventoryPayload payload, CancellationToken ct)
        {
            LastInventoryPayload = payload;
            return Task.FromResult((UpdateInventorySuccess, UpdateInventoryError));
        }

        public Task<bool> SetListingStateAsync(string shopId, long listingId, string state, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<long?>(123456);

        public Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<long?>(789);
    }

    [Fact]
    public async Task PublishListingAsync_WhenVariationsProvided_BuildsStrictEtsyFloatOfferings()
    {
        // Arrange
        var fakeClient = new FakeEtsyListingApiClient();
        using var httpClient = new HttpClient();
        var service = new FastListingPublishService(fakeClient, httpClient);

        var command = new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "Dragon Lamp Artisan 3D Night Light Fantasy",
            Description: "Realistic glowing dragon lamp handcrafted with care.",
            Price: 49.90m,
            Quantity: 20,
            TaxonomyId: 1042,
            ShippingProfileId: 123456,
            ReadinessStateId: 789,
            IsDigital: false,
            Tags: ["dragon lamp", "fire lamp"],
            Materials: ["PLA", "Resin"],
            State: "draft",
            VariationGroups:
            [
                new ListingVariationGroupInput("Materyal / Material", ["Red", "Green"]),
                new ListingVariationGroupInput("Renk / Color", ["Red", "Green"])
            ],
            Variations:
            [
                new ListingVariationInput("Red / Red", 49.90m, 5, true),
                new ListingVariationInput("Red / Green", 49.90m, 5, true),
                new ListingVariationInput("Green / Red", 59.90m, 5, true),
                new ListingVariationInput("Green / Green", 59.90m, 5, true)
            ]
        );

        // Act
        var result = await service.PublishListingAsync(command);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(99887766, result.ListingId);
        Assert.Equal(4, result.VariationsCount);
        Assert.Null(result.VariationError);

        var payload = fakeClient.LastInventoryPayload;
        Assert.NotNull(payload);
        Assert.Equal(4, payload.Products.Count);

        // Verify that price is numeric float (double), not string
        foreach (var prod in payload.Products)
        {
            Assert.Single(prod.Offerings);
            var offering = prod.Offerings[0];
            Assert.True(offering.Price > 0);
            Assert.IsType<double>(offering.Price);

            // Verify property_values contains value_ids: []
            Assert.Equal(2, prod.PropertyValues.Count);
            foreach (var pv in prod.PropertyValues)
            {
                Assert.NotNull(pv.ValueIds);
                Assert.Empty(pv.ValueIds);
                Assert.NotEmpty(pv.Values);
            }
        }
    }

    [Fact]
    public async Task PublishListingAsync_WhenVariationInventoryFails_ReturnsTransparentError()
    {
        // Arrange
        var fakeClient = new FakeEtsyListingApiClient
        {
            UpdateInventorySuccess = false,
            UpdateInventoryError = "HTTP 400: price must be a valid float value"
        };
        using var httpClient = new HttpClient();
        var service = new FastListingPublishService(fakeClient, httpClient);

        var command = new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "Dragon Lamp Artisan",
            Description: "A cool lamp.",
            Price: 39.90m,
            Quantity: 10,
            TaxonomyId: 1042,
            ShippingProfileId: 123456,
            ReadinessStateId: 789,
            IsDigital: false,
            Tags: ["dragon"],
            Materials: ["PLA"],
            State: "draft",
            Variations:
            [
                new ListingVariationInput("Small", 39.90m, 10, true)
            ]
        );

        // Act
        var result = await service.PublishListingAsync(command);

        // Assert
        Assert.True(result.Success); // Listing itself was created
        Assert.Equal(0, result.VariationsCount); // Variations failed
        Assert.NotNull(result.VariationError);
        Assert.Contains("price must be a valid float value", result.VariationError);
    }

    [Fact]
    public async Task PublishListingAsync_WhenTitleIsInvalid_ReturnsFailureWithoutCallingApi()
    {
        // Arrange
        var fakeClient = new FakeEtsyListingApiClient();
        using var httpClient = new HttpClient();
        var service = new FastListingPublishService(fakeClient, httpClient);

        var command = new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "", // Empty title
            Description: "Desc",
            Price: 39.90m,
            Quantity: 10,
            TaxonomyId: 1042,
            ShippingProfileId: 123456,
            ReadinessStateId: 789,
            IsDigital: false,
            Tags: [],
            Materials: [],
            State: "draft"
        );

        // Act
        var result = await service.PublishListingAsync(command);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Başlık", result.Message);
        Assert.Null(fakeClient.LastInventoryPayload);
    }
}
