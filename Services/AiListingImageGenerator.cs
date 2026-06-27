namespace SimilarProductsWinForms.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SimilarProductsWinForms.Models;

internal sealed class AiListingImageGenerator
{
    private static readonly HttpClient HttpClient = new();
    private static readonly string[] BrandRiskTerms =
    [
        "donkey kong",
        "nintendo",
        "mario",
        "pokemon",
        "zelda",
        "marvel",
        "dc comics",
        "star wars",
        "league of legends",
        "valorant",
        "minecraft",
        "dragon ball",
        "naruto",
        "one piece",
        "demon slayer",
        "harry potter",
        "lord of the rings",
        "lotr",
        "disney",
        "pixar",
        "sony",
        "playstation",
        "xbox",
    ];

    public async Task<string> GenerateAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (settings.UseOpenAi)
        {
            return await GenerateWithOpenAiAsync(settings, listing, userPrompt, cancellationToken);
        }

        if (settings.UseGemini)
        {
            return await GenerateWithGeminiAsync(settings, listing, userPrompt, cancellationToken);
        }

        throw new InvalidOperationException("AI gorsel uretimi icin AI Ayarlari ekraninda OpenAI veya Gemini saglayicisini ve API key'i secin.");
    }

    public async Task<string> GenerateFromReferenceAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        string referenceImagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceImagePath) || !File.Exists(referenceImagePath))
        {
            throw new InvalidOperationException("Referans gorsel bulunamadi. Once rakip listing gorselini indirin veya dosyadan secin.");
        }

        if (settings.UseOpenAi)
        {
            return await EditWithOpenAiAsync(settings, listing, userPrompt, referenceImagePath, cancellationToken);
        }

        if (settings.UseGemini)
        {
            return await EditWithGeminiAsync(settings, listing, userPrompt, referenceImagePath, cancellationToken);
        }

        throw new InvalidOperationException("Referans gorsel duzenleme icin AI Ayarlari ekraninda OpenAI veya Gemini saglayicisini ve API key'i secin.");
    }

    private static async Task<string> GenerateWithOpenAiAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(listing, userPrompt);
        var result = await SendOpenAiImageRequestAsync(settings, prompt, cancellationToken);
        if (!result.IsSuccess && IsModerationBlocked(result.Body))
        {
            prompt = BuildStrictFallbackPrompt(listing, userPrompt);
            result = await SendOpenAiImageRequestAsync(settings, prompt, cancellationToken);
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(CreateOpenAiImageErrorMessage(result.StatusCode, result.Body));
        }

        using var document = JsonDocument.Parse(result.Body);
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("AI gorsel yanitinda veri bulunamadi.");
        }

        var first = data[0];
        byte[] bytes;
        if (first.TryGetProperty("b64_json", out var b64))
        {
            bytes = Convert.FromBase64String(b64.GetString() ?? "");
        }
        else if (first.TryGetProperty("url", out var url))
        {
            bytes = await HttpClient.GetByteArrayAsync(url.GetString(), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("AI gorsel yaniti b64_json veya url icermiyor.");
        }

        return await SaveImageAsync(listing.ListingId, bytes, cancellationToken);
    }

    private static async Task<OpenAiImageHttpResult> SendOpenAiImageRequestAsync(
        AiOptimizationSettings settings,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = string.IsNullOrWhiteSpace(settings.OpenAiImageModel) ? "gpt-image-1" : settings.OpenAiImageModel.Trim(),
                prompt,
                size = "1024x1024",
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new OpenAiImageHttpResult(response.IsSuccessStatusCode, (int)response.StatusCode, body);
    }

    private static async Task<string> EditWithOpenAiAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        string referenceImagePath,
        CancellationToken cancellationToken)
    {
        var prompt = BuildReferenceEditPrompt(listing, userPrompt);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
        await using var imageStream = File.OpenRead(referenceImagePath);
        using var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageContentType(referenceImagePath));
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(string.IsNullOrWhiteSpace(settings.OpenAiImageModel) ? "gpt-image-1" : settings.OpenAiImageModel.Trim()), "model");
        form.Add(new StringContent(prompt), "prompt");
        form.Add(new StringContent("1024x1024"), "size");
        form.Add(imageContent, "image", Path.GetFileName(referenceImagePath));
        request.Content = form;

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(CreateOpenAiImageErrorMessage((int)response.StatusCode, body));
        }

        var bytes = await ExtractOpenAiImageBytesAsync(body, cancellationToken);
        return await SaveImageAsync(listing.ListingId, bytes, cancellationToken);
    }

    private static async Task<string> GenerateWithGeminiAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(listing, userPrompt);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
        request.Headers.Add("x-goog-api-key", settings.GeminiApiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = string.IsNullOrWhiteSpace(settings.GeminiImageModel) ? "gemini-3.1-flash-image" : settings.GeminiImageModel.Trim(),
                input = prompt,
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini gorsel uretilemedi. HTTP {(int)response.StatusCode}: {body}");
        }

        var bytes = ExtractGeminiImageBytes(body);
        return await SaveImageAsync(listing.ListingId, bytes, cancellationToken);
    }

    private static async Task<string> EditWithGeminiAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        string referenceImagePath,
        CancellationToken cancellationToken)
    {
        var prompt = BuildReferenceEditPrompt(listing, userPrompt);
        var imageBytes = await File.ReadAllBytesAsync(referenceImagePath, cancellationToken);
        var model = string.IsNullOrWhiteSpace(settings.GeminiImageModel) ? "gemini-3.1-flash-image" : settings.GeminiImageModel.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(settings.GeminiApiKey.Trim())}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = GetImageContentType(referenceImagePath),
                                    data = Convert.ToBase64String(imageBytes),
                                },
                            },
                        },
                    },
                },
                generationConfig = new
                {
                    responseModalities = new[] { "TEXT", "IMAGE" },
                },
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini referans gorsel duzenleme basarisiz. HTTP {(int)response.StatusCode}: {body}");
        }

        var bytes = ExtractGeminiImageBytes(body);
        return await SaveImageAsync(listing.ListingId, bytes, cancellationToken);
    }

    private static async Task<byte[]> ExtractOpenAiImageBytesAsync(string responseBody, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(responseBody);
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("AI gorsel yanitinda veri bulunamadi.");
        }

        var first = data[0];
        if (first.TryGetProperty("b64_json", out var b64))
        {
            return Convert.FromBase64String(b64.GetString() ?? "");
        }

        if (first.TryGetProperty("url", out var url))
        {
            return await HttpClient.GetByteArrayAsync(url.GetString(), cancellationToken);
        }

        throw new InvalidOperationException("AI gorsel yaniti b64_json veya url icermiyor.");
    }

    private static byte[] ExtractGeminiImageBytes(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var found = FindBase64Image(root);
        if (!string.IsNullOrWhiteSpace(found))
        {
            return Convert.FromBase64String(found);
        }

        throw new InvalidOperationException("Gemini yanitinda gorsel verisi bulunamadi. Modelin gorsel uretim destekledigini kontrol edin.");
    }

    private static string? FindBase64Image(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("inline_data", out var inlineData) &&
                inlineData.TryGetProperty("data", out var data))
            {
                return data.GetString();
            }

            if (element.TryGetProperty("inlineData", out var inlineDataCamel) &&
                inlineDataCamel.TryGetProperty("data", out var dataCamel))
            {
                return dataCamel.GetString();
            }

            if (element.TryGetProperty("image", out var image) &&
                image.ValueKind == JsonValueKind.String)
            {
                return image.GetString();
            }

            if (element.TryGetProperty("b64_json", out var b64Json) &&
                b64Json.ValueKind == JsonValueKind.String)
            {
                return b64Json.GetString();
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindBase64Image(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindBase64Image(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static async Task<string> SaveImageAsync(
        long listingId,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms",
            "generated-images");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"listing-{listingId}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    private static string BuildPrompt(MarketListingResult listing, string userPrompt)
    {
        var safeTitle = SanitizeForImagePrompt(listing.Title);
        var tags = string.Join(", ", listing.Tags.Select(SanitizeForImagePrompt).Where(tag => tag.Length > 0).Take(8));
        var safeUserPrompt = SanitizeForImagePrompt(userPrompt);
        return
            "Create a clean Etsy product listing image. The image must look like a real product photo or polished product mockup, not text-heavy advertising. " +
            "Use only an original generic product design. No logos, no copyrighted character names, no brand marks, no watermark, no readable text. " +
            "Do not imitate any game, movie, anime, brand, mascot, or franchise. Use a neutral marketplace-ready background, good lighting, and a clear centered product composition. " +
            $"Generic product idea: {safeTitle}. Generic tags: {tags}. " +
            $"Seller note: {safeUserPrompt}";
    }

    private static string BuildStrictFallbackPrompt(MarketListingResult listing, string userPrompt)
    {
        var productType = InferGenericProductType($"{listing.Title} {string.Join(' ', listing.Tags)} {userPrompt}");
        return
            "Create a fully original Etsy product photo/mockup for a handmade marketplace listing. " +
            $"Subject: a generic {productType}. " +
            "No logos, no brand references, no franchise references, no copyrighted characters, no game or movie references, no readable text, no watermark. " +
            "Use a clean neutral background, realistic studio lighting, product centered, polished e-commerce composition, high quality, safe generic design.";
    }

    private static string BuildReferenceEditPrompt(MarketListingResult listing, string userPrompt)
    {
        var safeTitle = SanitizeForImagePrompt(listing.Title);
        var safeUserPrompt = SanitizeForImagePrompt(userPrompt);
        return
            "Edit the provided product photo for an Etsy listing. Preserve the same product category, silhouette, pose, scale, proportions, and main physical details from the reference image. " +
            "Do not invent a different product, do not add accessories, do not add brand logos, do not add characters, and do not add readable text or watermark. " +
            "Improve the marketplace presentation: clean neutral background, realistic studio lighting, sharper product focus, natural shadow, centered e-commerce composition. " +
            "If the seller requests a painted or premium collectible look, add tasteful realistic hand-painted miniature colors while keeping the original product shape and structure recognizable. " +
            $"Reference product title for context: {safeTitle}. Seller background/style request: {safeUserPrompt}";
    }

    private static string GetImageContentType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png",
        };
    }

    private static string SanitizeForImagePrompt(string value)
    {
        var text = value ?? "";
        foreach (var term in BrandRiskTerms)
        {
            text = ReplaceIgnoreCase(text, term, "original fantasy inspired");
        }

        var blockedWords = new[]
        {
            "copyrighted", "licensed", "official", "replica", "fan art", "fanart", "character", "mascot",
        };
        foreach (var term in blockedWords)
        {
            text = ReplaceIgnoreCase(text, term, "original");
        }

        return string.Join(
                ' ',
                text
                    .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                    .Take(60))
            .Trim();
    }

    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue) =>
        source.Replace(oldValue, newValue, StringComparison.OrdinalIgnoreCase);

    private static string InferGenericProductType(string text)
    {
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("barrel")) return "retro wooden barrel shelf decor prop";
        if (normalized.Contains("sword")) return "fantasy sword display prop";
        if (normalized.Contains("helmet")) return "fantasy helmet display prop";
        if (normalized.Contains("mask")) return "fantasy mask display prop";
        if (normalized.Contains("bust")) return "fantasy bust statue";
        if (normalized.Contains("figure") || normalized.Contains("statue")) return "original collectible figure";
        return "3D printed cosplay display prop";
    }

    private static bool IsModerationBlocked(string body) =>
        body.Contains("moderation_blocked", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("safety system", StringComparison.OrdinalIgnoreCase);

    private static string CreateOpenAiImageErrorMessage(int statusCode, string body)
    {
        if (IsModerationBlocked(body))
        {
            return "AI gorsel uretimi guvenlik/telif filtresine takildi. Urun adi veya prompt marka/oyun/film/karakter cagrisimi iceriyor olabilir. Promptu markasiz ve genel urun diliyle tekrar deneyin; ornek: 'generic fantasy display prop, neutral background, no logo, no character'.";
        }

        if (statusCode == 429)
        {
            return "AI gorsel kotasi veya hiz limiti doldu. Biraz bekleyin ya da API hesabinizdaki kota/billing ayarlarini kontrol edin.";
        }

        return $"AI gorsel uretilemedi. HTTP {statusCode}: {body}";
    }

    private sealed record OpenAiImageHttpResult(bool IsSuccess, int StatusCode, string Body);
}
