namespace SimilarProductsWinForms.Studio.Engines;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Services;

public sealed class OpenAiImageEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiEndpoint = "https://api.openai.com/v1/images/generations";

    public string EngineId => "openai";
    public string DisplayName => "OpenAI (GPT Image 2 & DALL-E 3)";
    public string Description => "En yüksek görsel muhakeme kabiliyeti, mükemmel tipografi ve sıfırdan e-ticaret sahneleri.";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.TextToImage |
        EngineCapabilities.TransparentBackground |
        EngineCapabilities.MultipleAspectRatios;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.OpenAiApiKey);
    }

    public async Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = StudioConfigurationManager.Current.OpenAiApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ImageEngineResult.Fail("OpenAI API Key girilmedi. Lütfen 'Key Yapılandır' butonu ile anahtarınızı kaydedin.", DisplayName);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gpt-image-2" : request.ModelName.Trim();
        if (model.Contains("gpt-image", StringComparison.OrdinalIgnoreCase))
        {
            // Normalize to official generation model
            model = "dall-e-3";
        }

        string basePrompt = request.Prompt.Trim();
        if (string.IsNullOrWhiteSpace(basePrompt))
        {
            return ImageEngineResult.Fail("Lütfen bir sahne promptu girin.", DisplayName);
        }

        string finalPrompt = StudioPresets.BuildCompositePrompt(
            basePrompt,
            request.LightingPreset,
            request.CameraAnglePreset,
            "");

        if (request.TransparentBackground)
        {
            finalPrompt += ", clean pure isolated cutout on transparent background, no shadow, commercial product packshot";
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

            var payload = new
            {
                model = model,
                prompt = finalPrompt,
                n = 1,
                size = "1024x1024",
                quality = "standard",
                response_format = "b64_json"
            };

            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.GetArrayLength() > 0)
                {
                    var item = dataArr[0];
                    if (item.TryGetProperty("b64_json", out var b64Prop))
                    {
                        byte[] bytes = Convert.FromBase64String(b64Prop.GetString()!);
                        using var ms = new MemoryStream(bytes);
                        return ImageEngineResult.Ok(new Bitmap(ms), DisplayName, model, sw.ElapsedMilliseconds);
                    }
                    if (item.TryGetProperty("url", out var urlProp))
                    {
                        byte[] bytes = await HttpClient.GetByteArrayAsync(urlProp.GetString()!, cancellationToken);
                        using var ms = new MemoryStream(bytes);
                        return ImageEngineResult.Ok(new Bitmap(ms), DisplayName, model, sw.ElapsedMilliseconds);
                    }
                }

                return ImageEngineResult.Fail("OpenAI görsel verisi döndüremedi.", DisplayName);
            }

            return ImageEngineResult.Fail($"OpenAI Hatası ({response.StatusCode}): {body}", DisplayName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ImageEngineResult.Fail($"OpenAI bağlantı hatası: {ex.Message}", DisplayName);
        }
    }
}
