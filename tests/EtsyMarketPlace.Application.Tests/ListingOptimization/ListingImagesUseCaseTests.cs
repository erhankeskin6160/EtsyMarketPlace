namespace EtsyMarketPlace.Application.Tests.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Listings.Images;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using Xunit;

public sealed class ListingImagesUseCaseTests
{
    [Fact]
    public async Task GetListingImagesQueryHandler_ShouldParseAndSortImagesByRank()
    {
        var fakeClient = new FakeEtsyListingApiClient
        {
            ImagesJson = """
            {
              "results": [
                { "listing_image_id": 202, "rank": 2, "url_570xN": "https://img/2.jpg", "url_fullxfull": "https://img/2_full.jpg", "url_75x75": "https://img/2_75.jpg" },
                { "listing_image_id": 101, "rank": 1, "url_570xN": "https://img/1.jpg", "url_fullxfull": "https://img/1_full.jpg", "url_75x75": "https://img/1_75.jpg" }
              ]
            }
            """
        };

        var handler = new GetListingImagesQueryHandler(fakeClient);
        var response = await handler.Handle(new GetListingImagesQuery { ShopId = "53236321", ListingId = 4434124339 }, CancellationToken.None);

        Assert.True(response.Success);
        var images = Assert.IsAssignableFrom<IReadOnlyList<ListingImageDetailDto>>(response.Data);
        Assert.Equal(2, images.Count);
        Assert.Equal(101, images[0].ListingImageId);
        Assert.Equal(1, images[0].Rank);
        Assert.Equal(202, images[1].ListingImageId);
        Assert.Equal(2, images[1].Rank);
    }

    [Fact]
    public async Task SyncListingImagesCommandHandler_ShouldRejectEmptyOrOverLimitItems()
    {
        var handler = new SyncListingImagesCommandHandler(new FakeEtsyListingApiClient(), new FakeExifStripper());

        var emptyResp = await handler.Handle(new SyncListingImagesCommand
        {
            ShopId = "53236321",
            ListingId = 123,
            Items = Array.Empty<SyncListingImageItem>()
        }, CancellationToken.None);
        Assert.False(emptyResp.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, emptyResp.StatusCode);

        var overLimitItems = Enumerable.Range(1, 11)
            .Select(r => new SyncListingImageItem(r, null, null, r))
            .ToList();
        var overResp = await handler.Handle(new SyncListingImagesCommand
        {
            ShopId = "53236321",
            ListingId = 123,
            Items = overLimitItems
        }, CancellationToken.None);
        Assert.False(overResp.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, overResp.StatusCode);
    }

    [Fact]
    public async Task SyncListingImagesCommandHandler_ShouldStripExif_UploadBeforeDelete_AndReorder()
    {
        var fakeClient = new FakeEtsyListingApiClient
        {
            ImagesJson = """
            {
              "results": [
                { "listing_image_id": 1001, "rank": 1, "url_570xN": "https://img/1001.jpg" },
                { "listing_image_id": 1002, "rank": 2, "url_570xN": "https://img/1002.jpg" }
              ]
            }
            """
        };
        var fakeStripper = new FakeExifStripper();
        var handler = new SyncListingImagesCommandHandler(fakeClient, fakeStripper);

        var base64Sample = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var cmd = new SyncListingImagesCommand
        {
            ShopId = "53236321",
            ListingId = 4434124339,
            Items = new[]
            {
                // 1002 moved to rank 1, new image at rank 2, 1001 deleted
                new SyncListingImageItem(1002, null, null, 1),
                new SyncListingImageItem(null, base64Sample, "new_photo.jpg", 2)
            }
        };

        var response = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(response.Success);
        var result = Assert.IsType<SyncListingImagesResult>(response.Data);
        Assert.Equal(1, result.UploadedCount);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, result.ReorderedCount);

        Assert.True(fakeStripper.WasCalled);
        Assert.True(fakeStripper.LastStripExif);
        Assert.True(fakeStripper.LastPermutateHash);

        // Upload must happen before Delete to avoid Etsy's "cannot delete last image" error
        var uploadIndex = fakeClient.CallOrder.IndexOf("Upload:2");
        var deleteIndex = fakeClient.CallOrder.IndexOf("Delete:1001");
        Assert.True(uploadIndex >= 0 && deleteIndex > uploadIndex);
        Assert.Contains("Rank:1002->1", fakeClient.CallOrder);
    }

    private sealed class FakeExifStripper : IExifStripper
    {
        public bool WasCalled { get; private set; }
        public bool LastStripExif { get; private set; }
        public bool LastPermutateHash { get; private set; }

        public byte[] ProcessImage(byte[] inputBytes, bool stripExif = true, bool permutateHash = true)
        {
            WasCalled = true;
            LastStripExif = stripExif;
            LastPermutateHash = permutateHash;
            return inputBytes;
        }
    }

    private sealed class FakeEtsyListingApiClient : IEtsyListingApiClient
    {
        public string ImagesJson { get; set; } = "{\"results\":[]}";
        public List<string> CallOrder { get; } = new();

        public Task<string?> GetListingImagesRawJsonAsync(long listingId, string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(ImagesJson);

        public Task<(bool Success, long ImageId)> UploadListingImageAsync(
            string shopId, long listingId, byte[] imageBytes, string fileName, int rank, CancellationToken cancellationToken = default)
        {
            CallOrder.Add($"Upload:{rank}");
            return Task.FromResult((true, 9000L + rank));
        }

        public Task<bool> DeleteListingImageAsync(
            string shopId, long listingId, long listingImageId, CancellationToken cancellationToken = default)
        {
            CallOrder.Add($"Delete:{listingImageId}");
            return Task.FromResult(true);
        }

        public Task<bool> UpdateListingImageRankAsync(
            string shopId, long listingId, long listingImageId, int rank, CancellationToken cancellationToken = default)
        {
            CallOrder.Add($"Rank:{listingImageId}->{rank}");
            return Task.FromResult(true);
        }

        public Task<(long ListingId, string Url)> CreateDraftListingAsync(string shopId, CreateDraftListingRawRequest rawRequest, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<(bool Success, string? ErrorMessage, string? ResponseJson)> UpdateListingInventoryAsync(string shopId, long listingId, EtsyInventoryPayload payload, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<(bool Success, string? ErrorMessage, int Count)> UpdateVariationImagesAsync(string shopId, long listingId, IReadOnlyList<EtsyVariationImageItem> variationImages, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> SetListingStateAsync(string shopId, long listingId, string state, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<string> GetShopListingsRawJsonAsync(string shopId, int limit, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<(bool Success, string Body, string? ErrorMessage)> UpdateListingRawAsync(string shopId, string listingId, IEnumerable<KeyValuePair<string, string>> formFields, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}

