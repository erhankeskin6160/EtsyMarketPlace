namespace SimilarProductsWinForms.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SimilarProductsWinForms.Models;

internal sealed class AiListingImageGenerator
{
    private static readonly HttpClient HttpClient = new();

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

    private static async Task<string> GenerateWithOpenAiAsync(
        AiOptimizationSettings settings,
        MarketListingResult listing,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(listing, userPrompt);
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
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI gorsel uretilemedi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
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
        var tags = string.Join(", ", listing.Tags.Take(10));
        return
            "Create a clean Etsy product listing image. The image must look like a real product photo or polished product mockup, not text-heavy advertising. " +
            "No logos, no copyrighted character names, no brand marks, no watermark, no readable text unless the user explicitly requested simple label text. " +
            "Use a neutral marketplace-ready background, good lighting, and a clear centered product composition. " +
            $"Product title: {listing.Title}. Tags: {tags}. " +
            $"Seller note: {userPrompt}";
    }
}
