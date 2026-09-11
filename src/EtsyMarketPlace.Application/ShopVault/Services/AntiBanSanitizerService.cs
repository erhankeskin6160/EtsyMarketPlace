namespace EtsyMarketPlace.Application.ShopVault.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;

public sealed class AntiBanSanitizerService : IAntiBanSanitizer
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly IExifStripper? _exifStripper;

    public AntiBanSanitizerService(IExifStripper? exifStripper = null)
    {
        _exifStripper = exifStripper;
    }

    public Task<byte[]> SanitizeImageAsync(
        byte[] inputBytes,
        bool stripExif = true,
        bool permutateHash = true,
        CancellationToken cancellationToken = default)
    {
        if (_exifStripper != null)
        {
            return Task.Run(() => _exifStripper.ProcessImage(inputBytes, stripExif, permutateHash), cancellationToken);
        }
        return Task.FromResult(inputBytes);
    }

    public async Task<string> RewriteTextWithAiAsync(
        string text,
        bool isTitle,
        string? apiKey,
        string provider = "OpenAI",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApplyLocalFallbackRewriting(text, isTitle);
        }

        try
        {
            string systemPrompt = isTitle
                ? "You are an expert Etsy SEO specialist. Slightly rephrase this product title to be fresh, appealing and distinct while preserving the main product keywords and style. Keep it concise, natural and high-converting. Return ONLY the rewritten title without quotes or explanations."
                : "You are an expert Etsy copywriter. Slightly rephrase this product description while keeping all key specifications, dimensions, materials, and care instructions intact. Return ONLY the polished, rewritten description without commentary.";

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text.Trim() }
                },
                temperature = 0.5,
                max_tokens = isTitle ? 80 : 800
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApplyLocalFallbackRewriting(text, isTitle);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content.Trim().Trim('\"');
                }
            }

            return ApplyLocalFallbackRewriting(text, isTitle);
        }
        catch
        {
            return ApplyLocalFallbackRewriting(text, isTitle);
        }
    }

    public VaultListing SanitizeListingMetadata(VaultListing listing, AntiBanSettings settings)
    {
        var sanitized = new VaultListing
        {
            ListingId = listing.ListingId,
            SessionId = listing.SessionId,
            OriginalShopId = listing.OriginalShopId,
            OriginalShopName = listing.OriginalShopName,
            Title = listing.Title,
            Description = listing.Description,
            Price = listing.Price,
            Currency = listing.Currency,
            Quantity = listing.Quantity,
            Tags = new List<string>(listing.Tags),
            Materials = new List<string>(listing.Materials),
            TaxonomyId = listing.TaxonomyId,
            TaxonomyPath = listing.TaxonomyPath,
            WhoMade = listing.WhoMade,
            WhenMade = listing.WhenMade,
            IsSupply = listing.IsSupply,
            IsDigital = listing.IsDigital,
            ShippingProfileId = listing.ShippingProfileId,
            ShippingProfileTitle = listing.ShippingProfileTitle,
            ReturnPolicyId = listing.ReturnPolicyId,
            State = settings.CreateAsDraftFirst ? "draft" : listing.State,
            OriginalListingUrl = listing.OriginalListingUrl,
            BackedUpAtUtc = listing.BackedUpAtUtc
        };

        // Price Adjustment Percent
        if (settings.PriceAdjustmentPercent != 0)
        {
            decimal multiplier = 1.0m + (settings.PriceAdjustmentPercent / 100.0m);
            sanitized.Price = Math.Round(Math.Max(0.20m, sanitized.Price * multiplier), 2);
        }

        // Tag shuffling & sanitizing
        if (sanitized.Tags.Count > 0)
        {
            sanitized.Tags = sanitized.Tags.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList();
        }

        // Copy images
        foreach (var img in listing.Images)
        {
            sanitized.Images.Add(new VaultListingImage
            {
                Id = Guid.NewGuid().ToString("N"),
                ListingId = img.ListingId,
                OriginalImageId = img.OriginalImageId,
                Rank = img.Rank,
                LocalRelativePath = img.LocalRelativePath,
                OriginalUrl = img.OriginalUrl,
                HexCode = img.HexCode,
                FileSizeBytes = img.FileSizeBytes,
                Width = img.Width,
                Height = img.Height,
                DownloadedAtUtc = img.DownloadedAtUtc
            });
        }

        // Copy variations with SKU prefix
        foreach (var v in listing.Variations)
        {
            string newSku = string.IsNullOrWhiteSpace(v.Sku)
                ? string.Empty
                : $"{settings.SkuPrefix}{v.Sku}";

            decimal varPriceDiff = v.PriceDifference;
            if (settings.PriceAdjustmentPercent != 0 && varPriceDiff != 0)
            {
                decimal multiplier = 1.0m + (settings.PriceAdjustmentPercent / 100.0m);
                varPriceDiff = Math.Round(varPriceDiff * multiplier, 2);
            }

            sanitized.Variations.Add(new VaultListingVariation
            {
                Id = Guid.NewGuid().ToString("N"),
                ListingId = v.ListingId,
                PropertyId = v.PropertyId,
                PropertyName = v.PropertyName,
                ValueId = v.ValueId,
                ValueName = v.ValueName,
                PriceDifference = varPriceDiff,
                Sku = newSku,
                StockQuantity = v.StockQuantity,
                IsAvailable = v.IsAvailable
            });
        }

        return sanitized;
    }

    private static string ApplyLocalFallbackRewriting(string text, bool isTitle)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text.Trim();
    }
}
