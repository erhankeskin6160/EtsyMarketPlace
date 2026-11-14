using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Behaviours;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ListingOptimization.Validation;
using FluentValidation;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.ValidationTests;

public sealed class CreateListingValidationTests
{
    private sealed class StubEtsyListingApiClient(long? defaultShippingProfileId) : IEtsyListingApiClient
    {
        public Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult(defaultShippingProfileId);

        public Task<(long ListingId, string Url)> CreateDraftListingAsync(string shopId, CreateDraftListingRawRequest raw, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<(bool Success, long ImageId)> UploadListingImageAsync(string shopId, long listingId, byte[] imageBytes, string fileName, int rank, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<bool> DeleteListingImageAsync(string shopId, long listingId, long listingImageId, CancellationToken ct = default) =>
            throw new System.NotImplementedException();

        public Task<bool> UpdateListingImageRankAsync(string shopId, long listingId, long listingImageId, int rank, CancellationToken ct = default) =>
            throw new System.NotImplementedException();

        public Task<bool> UpdateListingImageMetadataAsync(string shopId, long listingId, long listingImageId, int rank, string? altText, CancellationToken ct = default) =>
            throw new System.NotImplementedException();

        public Task<(bool Success, string? ErrorMessage, string? ResponseJson)> UpdateListingInventoryAsync(string shopId, long listingId, EtsyInventoryPayload payload, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<(bool Success, string? ErrorMessage, int Count)> UpdateVariationImagesAsync(string shopId, long listingId, IReadOnlyList<EtsyVariationImageItem> variationImages, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<bool> SetListingStateAsync(string shopId, long listingId, string state, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<long?>(null);

        public Task<string> GetShopListingsRawJsonAsync(string shopId, int limit, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<string?> GetListingImagesRawJsonAsync(long listingId, string shopId, CancellationToken ct) =>
            throw new System.NotImplementedException();

        public Task<(bool Success, string Body, string? ErrorMessage)> UpdateListingRawAsync(string shopId, string listingId, IEnumerable<KeyValuePair<string, string>> formFields, CancellationToken ct) =>
            throw new System.NotImplementedException();
    }

    private static FastListingPublishCommand CreateCommand(
        bool isDigital = false,
        long? shippingProfileId = null,
        string title = "Handcrafted 3D Printed Lamp",
        decimal price = 29.90m,
        int quantity = 5) =>
        new(
            ShopId: "53236321",
            Title: title,
            Description: "Clean handcrafted item.",
            Price: price,
            Quantity: quantity,
            TaxonomyId: 1042,
            ShippingProfileId: shippingProfileId,
            ReadinessStateId: null,
            IsDigital: isDigital,
            Tags: new[] { "lamp", "3d printed" },
            Materials: new[] { "PLA" },
            State: "draft"
        );

    [Fact]
    public async Task PhysicalListing_WithoutShippingProfileAndNoDefault_ReturnsValidationErrorResponse()
    {
        var validator = new CreateListingCommandRequestValidator(
            new FastListingPublishCommandValidator(),
            new StubEtsyListingApiClient(defaultShippingProfileId: null));

        var behaviour = new ValidationBehaviour<CreateListingCommandRequest, ServiceResponse>(
            new IValidator<CreateListingCommandRequest>[] { validator });

        var handlerCalled = false;
        var request = new CreateListingCommandRequest { Command = CreateCommand(isDigital: false, shippingProfileId: null) };

        var response = await behaviour.Handle(request, () =>
        {
            handlerCalled = true;
            return Task.FromResult(ServiceResponse.CreateSuccess());
        }, CancellationToken.None);

        Assert.False(handlerCalled);
        var validationResponse = Assert.IsType<ValidationErrorResponse>(response);
        Assert.False(validationResponse.Success);
        Assert.Equal(400, validationResponse.StatusCode);
        Assert.True(validationResponse.Errors.ContainsKey("ShippingProfileId"));
        Assert.Contains(
            CreateListingCommandRequestValidator.MissingShippingProfileMessage,
            validationResponse.Errors["ShippingProfileId"]);
    }

    [Fact]
    public async Task PhysicalListing_WithDefaultShippingProfileInShop_PassesValidation()
    {
        var validator = new CreateListingCommandRequestValidator(
            new FastListingPublishCommandValidator(),
            new StubEtsyListingApiClient(defaultShippingProfileId: 98765));

        var behaviour = new ValidationBehaviour<CreateListingCommandRequest, ServiceResponse>(
            new IValidator<CreateListingCommandRequest>[] { validator });

        var handlerCalled = false;
        var request = new CreateListingCommandRequest { Command = CreateCommand(isDigital: false, shippingProfileId: null) };

        var response = await behaviour.Handle(request, () =>
        {
            handlerCalled = true;
            return Task.FromResult(ServiceResponse.CreateSuccess());
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task DigitalListing_WithoutShippingProfile_PassesValidation()
    {
        var validator = new CreateListingCommandRequestValidator(
            new FastListingPublishCommandValidator(),
            new StubEtsyListingApiClient(defaultShippingProfileId: null));

        var behaviour = new ValidationBehaviour<CreateListingCommandRequest, ServiceResponse>(
            new IValidator<CreateListingCommandRequest>[] { validator });

        var handlerCalled = false;
        var request = new CreateListingCommandRequest { Command = CreateCommand(isDigital: true, shippingProfileId: null) };

        var response = await behaviour.Handle(request, () =>
        {
            handlerCalled = true;
            return Task.FromResult(ServiceResponse.CreateSuccess());
        }, CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task InvalidFields_NormalizesCommandPrefixInValidationErrorResponse()
    {
        var validator = new CreateListingCommandRequestValidator(
            new FastListingPublishCommandValidator(),
            new StubEtsyListingApiClient(defaultShippingProfileId: 98765));

        var behaviour = new ValidationBehaviour<CreateListingCommandRequest, ServiceResponse>(
            new IValidator<CreateListingCommandRequest>[] { validator });

        var request = new CreateListingCommandRequest
        {
            Command = CreateCommand(isDigital: true, title: "", price: 0m, quantity: 0)
        };

        var response = await behaviour.Handle(
            request,
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var validationResponse = Assert.IsType<ValidationErrorResponse>(response);
        Assert.True(validationResponse.Errors.ContainsKey("Title"));
        Assert.True(validationResponse.Errors.ContainsKey("Price"));
        Assert.True(validationResponse.Errors.ContainsKey("Quantity"));
        Assert.DoesNotContain(validationResponse.Errors.Keys, k => k.StartsWith("Command."));
    }

    [Fact]
    public async Task PhysicalListing_WithNonPositiveReturnPolicyId_ReturnsValidationErrorResponse()
    {
        var validator = new CreateListingCommandRequestValidator(
            new FastListingPublishCommandValidator(),
            new StubEtsyListingApiClient(defaultShippingProfileId: 98765));

        var behaviour = new ValidationBehaviour<CreateListingCommandRequest, ServiceResponse>(
            new IValidator<CreateListingCommandRequest>[] { validator });

        var request = new CreateListingCommandRequest
        {
            Command = CreateCommand(isDigital: false, shippingProfileId: 98765) with { ReturnPolicyId = 0 }
        };

        var response = await behaviour.Handle(
            request,
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var validationResponse = Assert.IsType<ValidationErrorResponse>(response);
        Assert.True(validationResponse.Errors.ContainsKey("ReturnPolicyId"));
    }
}
