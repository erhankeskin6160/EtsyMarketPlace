namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public sealed class FastListingPublishService : IFastListingPublishService
{
    private readonly IEtsyListingApiClient _apiClient;
    private readonly HttpClient _httpClient;

    public FastListingPublishService(
        IEtsyListingApiClient apiClient,
        HttpClient httpClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<FastListingPublishResult> PublishListingAsync(
        FastListingPublishCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.ShopId))
            return Failure(0, "", "Mağaza kimliği (Shop ID) belirtilmedi.");

        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Trim().Length > FastListingDraftHelper.MaxTitleLength)
            return Failure(0, "", $"Başlık 1 ile {FastListingDraftHelper.MaxTitleLength} karakter arasında olmalıdır.");

        if (command.Price <= 0)
            return Failure(0, "", "Fiyat 0'dan büyük olmalıdır.");

        if (command.Quantity <= 0)
            return Failure(0, "", "Stok adedi en az 1 olmalıdır.");

        var shopId = command.ShopId.Trim();

        // 1. Kargo Profili & Hazırlık Durumu Çözümleme
        long? effectiveShippingProfileId = command.ShippingProfileId;
        long? effectiveReadinessStateId = command.ReadinessStateId;

        if (!command.IsDigital)
        {
            if (!effectiveShippingProfileId.HasValue || effectiveShippingProfileId.Value <= 0)
            {
                effectiveShippingProfileId = await _apiClient.GetDefaultShippingProfileIdAsync(shopId, cancellationToken);
            }

            if (!effectiveShippingProfileId.HasValue || effectiveShippingProfileId.Value <= 0)
            {
                return Failure(0, "", "Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili gereklidir. Lütfen Etsy Mağaza Yöneticisi alanından bir kargo profili oluşturun veya ilanı 'Dijital' olarak seçin.");
            }

            if (!effectiveReadinessStateId.HasValue || effectiveReadinessStateId.Value <= 0)
            {
                effectiveReadinessStateId = await _apiClient.GetDefaultReadinessStateIdAsync(shopId, cancellationToken);
            }
        }

        // 2. Etiket ve Materyalleri Temizleme
        var sanitizedTags = FastListingDraftHelper.SanitizeTags(command.Tags);
        var sanitizedMaterials = FastListingDraftHelper.SanitizeMaterials(command.Materials);

        // 3. Taslak İlan Oluşturma (POST /v3/application/shops/{shop_id}/listings)
        var rawDraftRequest = new CreateDraftListingRawRequest(
            Title: command.Title.Trim(),
            Description: string.IsNullOrWhiteSpace(command.Description) ? "Handmade artisan item." : command.Description.Trim(),
            Price: command.Price,
            Quantity: Math.Max(1, command.Quantity),
            TaxonomyId: command.TaxonomyId > 0 ? command.TaxonomyId : 1042,
            Type: command.IsDigital ? "download" : "physical",
            State: command.State.Equals("active", StringComparison.OrdinalIgnoreCase) ? "active" : "draft",
            WhoMade: string.IsNullOrWhiteSpace(command.WhoMade) ? "i_did" : command.WhoMade.Trim(),
            WhenMade: string.IsNullOrWhiteSpace(command.WhenMade) ? "made_to_order" : command.WhenMade.Trim(),
            ShippingProfileId: effectiveShippingProfileId,
            ReadinessStateId: effectiveReadinessStateId,
            Tags: sanitizedTags,
            Materials: sanitizedMaterials
        );

        long createdListingId;
        string listingUrl;
        try
        {
            var created = await _apiClient.CreateDraftListingAsync(shopId, rawDraftRequest, cancellationToken);
            createdListingId = created.ListingId;
            listingUrl = created.Url;
        }
        catch (Exception ex)
        {
            return Failure(0, "", $"Etsy ilan oluşturma hatası: {ex.Message}");
        }

        if (createdListingId <= 0)
        {
            return Failure(0, "", "İlan oluşturuldu ancak geçerli bir listing_id alınamadı.");
        }

        // 4. Görselleri Sırayla Yükleme
        int uploadedImageCount = 0;
        if (command.Images != null && command.Images.Count > 0)
        {
            int rank = 1;
            foreach (var img in command.Images.Take(10))
            {
                try
                {
                    byte[]? imageBytes = null;
                    string fileName = $"image_{rank}.jpg";

                    if (!string.IsNullOrWhiteSpace(img.DataUrl) && img.DataUrl.StartsWith("data:image/"))
                    {
                        var base64Part = img.DataUrl[(img.DataUrl.IndexOf(',') + 1)..];
                        imageBytes = Convert.FromBase64String(base64Part);
                    }
                    else if (!string.IsNullOrWhiteSpace(img.Url) && (img.Url.StartsWith("http://") || img.Url.StartsWith("https://")))
                    {
                        using var imgDownload = await _httpClient.GetAsync(img.Url, cancellationToken);
                        if (imgDownload.IsSuccessStatusCode)
                        {
                            imageBytes = await imgDownload.Content.ReadAsByteArrayAsync(cancellationToken);
                        }
                    }

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        var uploadSuccess = await _apiClient.UploadListingImageAsync(
                            shopId, createdListingId, imageBytes, fileName, rank, cancellationToken);
                        if (uploadSuccess)
                        {
                            uploadedImageCount++;
                        }
                    }
                }
                catch
                {
                    // Görsel yükleme hatası ilanın tamamını durdurmaz
                }
                rank++;
            }
        }

        // 5. Varyasyonları ve Envanteri Hazırlayıp Güncelleme
        int uploadedVariationCount = 0;
        string? variationErrorMessage = null;

        if (command.Variations != null && command.Variations.Count > 0)
        {
            try
            {
                var inventoryPayload = BuildInventoryPayload(
                    createdListingId,
                    command,
                    effectiveReadinessStateId);

                if (inventoryPayload != null && inventoryPayload.Products.Count > 0)
                {
                    var (success, error) = await _apiClient.UpdateListingInventoryAsync(
                        shopId, createdListingId, inventoryPayload, cancellationToken);

                    if (success)
                    {
                        uploadedVariationCount = inventoryPayload.Products.Count;
                    }
                    else
                    {
                        variationErrorMessage = error ?? "Etsy varyasyon güncelleme reddi.";
                    }
                }
            }
            catch (Exception ex)
            {
                variationErrorMessage = $"Varyasyon hazırlama hatası: {ex.Message}";
            }
        }

        // 6. Canlı Yayın Aktivasyonu (State PATCH)
        string finalState = "draft";
        if (command.State.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var activated = await _apiClient.SetListingStateAsync(shopId, createdListingId, "active", cancellationToken);
                if (activated)
                {
                    finalState = "active";
                }
            }
            catch
            {
                // State aktifleşemezse taslak olarak kalır
            }
        }

        var message = uploadedVariationCount > 0
            ? $"İlan ({finalState}) ve {uploadedVariationCount} varyasyon başarıyla oluşturuldu."
            : (variationErrorMessage != null
                ? $"İlan oluşturuldu ancak varyasyonlar eklenemedi: {variationErrorMessage}"
                : $"İlan ({finalState}) başarıyla oluşturuldu.");

        return new FastListingPublishResult(
            Success: true,
            ListingId: createdListingId,
            Url: listingUrl,
            State: finalState,
            UploadedImages: uploadedImageCount,
            VariationsCount: uploadedVariationCount,
            VariationError: variationErrorMessage,
            Message: message
        );
    }

    private static EtsyInventoryPayload? BuildInventoryPayload(
        long listingId,
        FastListingPublishCommand command,
        long? effectiveReadinessStateId)
    {
        var groups = new List<(string Name, long PropertyId, List<string> Values)>();

        if (command.VariationGroups != null && command.VariationGroups.Count > 0)
        {
            int gIdx = 1;
            foreach (var g in command.VariationGroups.Take(2))
            {
                var (name, propId) = FastListingDraftHelper.ParseVariationType(g.Name, gIdx);
                var vals = g.Values
                    .Select(v => v.Trim())
                    .Where(v => v.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (vals.Count > 0)
                {
                    groups.Add((name, propId, vals));
                    gIdx++;
                }
            }
        }

        if (groups.Count == 0 && command.Variations != null)
        {
            var distinctKeys = command.Variations.Select(v => v.Key).Distinct().ToList();
            bool hasCompoundKeys = distinctKeys.Any(k => k.Contains('/'));
            if (hasCompoundKeys)
            {
                var g1Vals = distinctKeys.Select(k => k.Split('/')[0].Trim()).Where(v => v.Length > 0).Distinct().ToList();
                var g2Vals = distinctKeys.Where(k => k.Contains('/')).Select(k => k.Split('/')[1].Trim()).Where(v => v.Length > 0).Distinct().ToList();
                groups.Add(("Size", 513, g1Vals));
                if (g2Vals.Count > 0)
                {
                    groups.Add(("Color", 514, g2Vals));
                }
            }
            else
            {
                var g1Vals = distinctKeys.Select(k => k.Trim()).Where(v => v.Length > 0).Distinct().ToList();
                groups.Add(("Custom", 513, g1Vals));
            }
        }

        if (groups.Count == 0) return null;

        var customPricingMap = (command.Variations ?? [])
            .ToDictionary(v => v.Key.Trim(), v => v, StringComparer.OrdinalIgnoreCase);

        var combinations = new List<List<(string GroupName, long PropId, string Value)>>();
        if (groups.Count == 1)
        {
            foreach (var val in groups[0].Values)
            {
                combinations.Add([ (groups[0].Name, groups[0].PropertyId, val) ]);
            }
        }
        else if (groups.Count >= 2)
        {
            foreach (var val1 in groups[0].Values)
            {
                foreach (var val2 in groups[1].Values)
                {
                    combinations.Add([
                        (groups[0].Name, groups[0].PropertyId, val1),
                        (groups[1].Name, groups[1].PropertyId, val2)
                    ]);
                }
            }
        }

        var productsList = new List<EtsyInventoryProduct>();
        var distinctPricesG1 = new HashSet<decimal>();
        var distinctPricesG2 = new HashSet<decimal>();
        var sharedSku = $"AUTO-{listingId}";

        int prodIdx = 1;
        foreach (var combo in combinations.Take(FastListingDraftHelper.MaxVariationCombinations))
        {
            var comboKey = string.Join(" / ", combo.Select(c => c.Value));
            decimal rowPrice = command.Price;
            int rowQty = Math.Max(1, command.Quantity);
            bool isEnabled = true;

            if (customPricingMap.TryGetValue(comboKey, out var matchedVar) ||
                (combo.Count > 0 && customPricingMap.TryGetValue(combo[0].Value, out matchedVar)))
            {
                rowPrice = matchedVar.Price > 0 ? matchedVar.Price : command.Price;
                rowQty = matchedVar.Quantity > 0 ? matchedVar.Quantity : Math.Max(1, command.Quantity);
                isEnabled = matchedVar.Active;
            }

            distinctPricesG1.Add(rowPrice);
            if (combo.Count > 1) distinctPricesG2.Add(rowPrice);

            var offering = new EtsyInventoryOffering
            {
                Price = Math.Round((double)rowPrice, 2), // Etsy Open API v3 expects a numeric float
                Quantity = Math.Max(1, rowQty),
                IsEnabled = isEnabled,
                ReadinessStateId = effectiveReadinessStateId
            };

            var propValues = combo.Select(c => new EtsyInventoryPropertyValue
            {
                PropertyId = c.PropId,
                PropertyName = c.GroupName,
                ScaleId = null,
                ValueIds = [],
                Values = [ c.Value ]
            }).ToList();

            productsList.Add(new EtsyInventoryProduct
            {
                Sku = $"{sharedSku}-{prodIdx}",
                PropertyValues = propValues,
                Offerings = [ offering ]
            });

            prodIdx++;
        }

        var priceOnProperty = new List<long>();
        if (distinctPricesG1.Count > 1 && groups.Count >= 1)
        {
            priceOnProperty.Add(groups[0].PropertyId);
        }
        if (distinctPricesG2.Count > 1 && groups.Count >= 2 && !priceOnProperty.Contains(groups[1].PropertyId))
        {
            priceOnProperty.Add(groups[1].PropertyId);
        }

        return new EtsyInventoryPayload
        {
            Products = productsList,
            PriceOnProperty = priceOnProperty.ToArray(),
            QuantityOnProperty = [],
            SkuOnProperty = []
        };
    }

    private static FastListingPublishResult Failure(long listingId, string url, string message) =>
        new(
            Success: false,
            ListingId: listingId,
            Url: url,
            State: "draft",
            UploadedImages: 0,
            VariationsCount: 0,
            VariationError: null,
            Message: message
        );
}
