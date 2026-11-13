namespace EtsyMarketPlace.Application.Tests.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Listings.Images;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ListingImageSeoAndCtrTests
{
    [Fact]
    public async Task GetListingImagesQueryHandler_ShouldParseAltText()
    {
        var etsyClient = new FakeEtsyClient
        {
            ImagesJson = """
            {
              "results": [
                { "listing_image_id": 501, "rank": 1, "url_570xN": "https://img/501.jpg", "alt_text": "3D printed Omnitrix cosplay watch" }
              ]
            }
            """
        };

        var handler = new GetListingImagesQueryHandler(etsyClient);
        var res = await handler.Handle(new GetListingImagesQuery { ShopId = "53236321", ListingId = 4434124339 }, CancellationToken.None);

        Assert.True(res.Success);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ListingImageDetailDto>>(res.Data);
        Assert.Single(list);
        Assert.Equal("3D printed Omnitrix cosplay watch", list[0].AltText);
    }

    [Fact]
    public async Task AnalyzeListingImagesCommandHandler_ShouldClampCtrScoresAndEnforceMax250AltText()
    {
        var longAlt = new string('a', 260) + " tail";
        var etsyClient = new FakeEtsyClient
        {
            ImagesJson = """
            {
              "results": [
                { "listing_image_id": 101, "rank": 1, "url_570xN": "https://img/101.jpg" },
                { "listing_image_id": 102, "rank": 2, "url_570xN": "https://img/102.jpg" }
              ]
            }
            """
        };
        var aiClient = new FakeAiClient
        {
            JsonToReturn = $$"""
            {
              "cover_ctr": {
                "overall_score": 140,
                "contrast_score": 92,
                "mobile_crop_score": -5,
                "background_score": 87,
                "tip": "Hafif parlaklık artışı tıklanmayı artırır."
              },
              "images": [
                { "rank": 1, "angle_label": "Kapak", "alt_text": "{{longAlt}}" },
                { "rank": 2, "angle_label": "Yan Açı", "alt_text": "Side view of green cosplay watch bracelet" }
              ]
            }
            """
        };

        var handler = new AnalyzeListingImagesCommandHandler(etsyClient, aiClient, new FakeSettingsRepo());
        var res = await handler.Handle(new AnalyzeListingImagesCommand
        {
            ShopId = "53236321",
            ListingId = 4434124339,
            Title = "Ben 10 Omnitrix Watch",
            Provider = "Gemini",
            ApiKey = "test-key"
        }, CancellationToken.None);

        Assert.True(res.Success);
        var dto = Assert.IsType<AnalyzeListingImagesResultDto>(res.Data);
        Assert.Equal(100, dto.CoverCtr.OverallScore);
        Assert.Equal(0, dto.CoverCtr.MobileCropScore);
        Assert.Equal(92, dto.CoverCtr.ContrastScore);
        Assert.Equal(2, dto.AltTextSuggestions.Count);
        Assert.True(dto.AltTextSuggestions[0].SuggestedAltText.Length <= 250);
        Assert.Equal("Side view of green cosplay watch bracelet", dto.AltTextSuggestions[1].SuggestedAltText);
    }

    [Fact]
    public async Task UpdateListingImagesAltTextCommandHandler_ShouldRejectOver250AndUpdateValid()
    {
        var etsyClient = new FakeEtsyClient();
        var handler = new UpdateListingImagesAltTextCommandHandler(etsyClient);

        var badRes = await handler.Handle(new UpdateListingImagesAltTextCommand
        {
            ShopId = "53236321",
            ListingId = 4434124339,
            Items = new[] { new UpdateImageAltTextItem(101, 1, new string('x', 251)) }
        }, CancellationToken.None);

        Assert.False(badRes.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, badRes.StatusCode);

        var okRes = await handler.Handle(new UpdateListingImagesAltTextCommand
        {
            ShopId = "53236321",
            ListingId = 4434124339,
            Items = new[] { new UpdateImageAltTextItem(101, 1, "Handmade 3D printed cosplay watch") }
        }, CancellationToken.None);

        Assert.True(okRes.Success);
        Assert.Single(etsyClient.UpdatedMetadata);
        Assert.Equal("Handmade 3D printed cosplay watch", etsyClient.UpdatedMetadata[0].AltText);
    }

    private sealed class FakeAiClient : IGeminiListingApiClient
    {
        public string JsonToReturn { get; set; } = "{}";

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction, string userPrompt, string model, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?, string?)>((true, JsonToReturn, null));

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
            string systemInstruction, string userPrompt, IReadOnlyList<VisionImageInput> images, string model, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?, string?)>((true, JsonToReturn, null));
    }

    private sealed class FakeSettingsRepo : IShopSettingsRepository
    {
        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken ct = default) => Task.FromResult<TelegramShopSettings?>(null);
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CarrierSessionRecord>>(Array.Empty<CarrierSessionRecord>());
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken ct = default) => Task.FromResult<EtsyAppCredentialsRecord?>(null);
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken ct = default) => Task.FromResult(("", "", ""));
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SavedListingAuditRecord>>(Array.Empty<SavedListingAuditRecord>());
        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetPaymentReserveSnapshotJsonAsync(string shopId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SavePaymentReserveSnapshotJsonAsync(string shopId, string snapshotJson, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEtsyClient : IEtsyListingApiClient
    {
        public string ImagesJson { get; set; } = "{\"results\":[]}";
        public List<(long Id, int Rank, string? AltText)> UpdatedMetadata { get; } = new();

        public Task<string?> GetListingImagesRawJsonAsync(long listingId, string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(ImagesJson);

        public Task<bool> UpdateListingImageMetadataAsync(string shopId, long listingId, long listingImageId, int rank, string? altText, CancellationToken cancellationToken = default)
        {
            UpdatedMetadata.Add((listingImageId, rank, altText));
            return Task.FromResult(true);
        }

        public Task<(long ListingId, string Url)> CreateDraftListingAsync(string shopId, CreateDraftListingRawRequest rawRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(bool Success, long ImageId)> UploadListingImageAsync(string shopId, long listingId, byte[] imageBytes, string fileName, int rank, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteListingImageAsync(string shopId, long listingId, long listingImageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UpdateListingImageRankAsync(string shopId, long listingId, long listingImageId, int rank, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(bool Success, string? ErrorMessage, string? ResponseJson)> UpdateListingInventoryAsync(string shopId, long listingId, EtsyInventoryPayload payload, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(bool Success, string? ErrorMessage, int Count)> UpdateVariationImagesAsync(string shopId, long listingId, IReadOnlyList<EtsyVariationImageItem> variationImages, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> SetListingStateAsync(string shopId, long listingId, string state, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> GetShopListingsRawJsonAsync(string shopId, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(bool Success, string Body, string? ErrorMessage)> UpdateListingRawAsync(string shopId, string listingId, IEnumerable<KeyValuePair<string, string>> formFields, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
