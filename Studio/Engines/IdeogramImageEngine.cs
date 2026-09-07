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

public sealed class IdeogramImageEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiEndpoint = "https://api.ideogram.ai/generate";

    public string EngineId => "ideogram";
    public string DisplayName => "Ideogram 4.0 (Tipografi & Grafik Tasarım)";
    public string Description => "Kusursuz metin dizgisi, etiket, logo ve tipografik e-ticaret mockupları.";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.TextToImage |
        EngineCapabilities.MultipleAspectRatios;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.IdeogramApiKey);
    }

    public async Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = StudioConfigurationManager.Current.IdeogramApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ImageEngineResult.Fail("Ideogram API Key girilmedi. Lütfen 'Key Yapılandır' butonu ile anahtarınızı kaydedin.", DisplayName);
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

        var sw = Stopwatch.StartNew();

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            httpRequest.Headers.Add("Api-Key", apiKey);

            var payload = new
            {
                image_request = new
                {
                    prompt = finalPrompt,
                    aspect_ratio = "ASPECT_1_1",
                    model = "V_2",
                    magic_prompt_option = "AUTO"
                }
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
                    if (item.TryGetProperty("url", out var urlProp))
                    {
                        var bytes = await HttpClient.GetByteArrayAsync(urlProp.GetString()!, cancellationToken);
                        using var ms = new MemoryStream(bytes);
                        return ImageEngineResult.Ok(new Bitmap(ms), DisplayName, "ideogram-v2", sw.ElapsedMilliseconds);
                    }
                }

                return ImageEngineResult.Fail("Ideogram görsel verisi döndüremedi.", DisplayName);
            }

            return ImageEngineResult.Fail($"Ideogram Hatası ({response.StatusCode}): {body}", DisplayName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ImageEngineResult.Fail($"Ideogram bağlantı hatası: {ex.Message}", DisplayName);
        }
    }
}
