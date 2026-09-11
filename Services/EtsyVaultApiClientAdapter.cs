namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using SimilarProductsWinForms.Models;

internal sealed class EtsyVaultApiClientAdapter : IEtsyVaultApiClient
{
    private readonly EtsyApiClient _client;
    private readonly Func<EtsyApiSettings> _settingsProvider;

    public EtsyVaultApiClientAdapter(EtsyApiClient client, Func<EtsyApiSettings>? settingsProvider = null)
    {
        _client = client;
        _settingsProvider = settingsProvider ?? EtsyApiSettingsStore.Load;
    }

    public async Task<(long ShopId, string ShopName, string ShopUrl)> GetCurrentShopInfoAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        var profile = await _client.GetOwnShopProfileAsync(settings, cancellationToken);
        return (profile.ShopId, profile.ShopName, profile.ShopUrl);
    }

    public async Task<List<VaultListing>> FetchAllShopListingsPagedAsync(
        string state = "active",
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        var results = await _client.GetAllOwnShopListingsPagedAsync(settings, state, progress, cancellationToken);
        var vaultListings = new List<VaultListing>();

        foreach (var r in results)
        {
            var vl = new VaultListing
            {
                ListingId = r.ListingId,
                OriginalShopId = r.ShopId,
                OriginalShopName = r.ShopName,
                Title = r.Title,
                Description = r.Description,
                Price = r.Price,
                Currency = !string.IsNullOrWhiteSpace(r.CurrencyCode) ? r.CurrencyCode : "USD",
                Quantity = r.Quantity > 0 ? r.Quantity : 999,
                Tags = r.Tags?.ToList() ?? [],
                Materials = [],
                TaxonomyId = r.TaxonomyId,
                TaxonomyPath = r.TaxonomyName ?? string.Empty,
                WhoMade = "i_did",
                WhenMade = "made_to_order",
                IsSupply = false,
                IsDigital = false,
                ShippingProfileId = null,
                ShippingProfileTitle = string.Empty,
                State = "active",
                OriginalListingUrl = r.ListingUrl,
                BackedUpAtUtc = DateTime.UtcNow
            };

            int rank = 1;
            if (r.ImageUrls != null && r.ImageUrls.Count > 0)
            {
                foreach (var url in r.ImageUrls)
                {
                    vl.Images.Add(new VaultListingImage(r.ListingId, 0, rank++, "", url));
                }
            }
            else if (!string.IsNullOrWhiteSpace(r.ImageUrl))
            {
                vl.Images.Add(new VaultListingImage(r.ListingId, 0, 1, "", r.ImageUrl));
            }

            if (r.VariationOptions != null)
            {
                foreach (var opt in r.VariationOptions)
                {
                    foreach (var val in opt.Values)
                    {
                        vl.Variations.Add(new VaultListingVariation(
                            listingId: r.ListingId,
                            propertyId: opt.PropertyId,
                            propertyName: opt.Name,
                            valueId: 0,
                            valueName: val
                        ));
                    }
                }
            }

            vaultListings.Add(vl);
        }

        return vaultListings;
    }

    public async Task<byte[]> DownloadImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        return await _client.DownloadImageBytesAsync(imageUrl, cancellationToken);
    }

    public async Task<long> CreateDraftListingAsync(VaultListing listing, long targetShopId, CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        var draft = new DraftListingCreateRequest(
            Title: listing.Title,
            Description: listing.Description,
            Price: listing.Price,
            Quantity: Math.Max(1, listing.Quantity),
            TaxonomyId: listing.TaxonomyId,
            ShippingProfileId: listing.ShippingProfileId ?? 0,
            IsDigital: listing.IsDigital,
            Tags: listing.Tags,
            Materials: listing.Materials,
            ReadinessStateId: 0,
            WhoMade: listing.WhoMade,
            WhenMade: listing.WhenMade
        );

        var created = await _client.CreateOwnShopDraftListingAsync(settings, draft, cancellationToken);
        return created.ListingId;
    }

    public async Task UploadListingImageAsync(long targetShopId, long listingId, string imagePath, int rank, CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        await _client.UploadOwnShopListingImageAsync(settings, listingId, imagePath, rank, cancellationToken);
    }

    public async Task UpdateListingInventoryAsync(long targetShopId, long listingId, List<VaultListingVariation> variations, CancellationToken cancellationToken = default)
    {
        if (variations == null || variations.Count == 0) return;
        var settings = _settingsProvider();

        var groups = variations
            .GroupBy(v => v.PropertyName)
            .Select(g => new DraftListingVariationGroup(g.Key, g.First().PropertyId, g.Select(x => x.ValueName).Distinct().ToList()))
            .ToList();

        var customPricing = new Dictionary<string, DraftListingVariationPricing>();
        foreach (var v in variations)
        {
            if (v.PriceDifference != 0 || !string.IsNullOrWhiteSpace(v.Sku))
            {
                customPricing[v.ValueName] = new DraftListingVariationPricing(
                    v.ValueName,
                    v.PriceDifference,
                    v.StockQuantity,
                    v.IsAvailable);
            }
        }

        var inv = new DraftListingInventoryUpdate(
            0,
            999,
            null,
            groups,
            customPricing.Count > 0 ? customPricing : null
        );

        await _client.UpdateOwnShopListingInventoryAsync(settings, listingId, inv, cancellationToken);
    }
}
