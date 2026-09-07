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

public sealed class FluxImageEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();

    public string EngineId => "flux";
    public string DisplayName => "Black Forest Labs FLUX 1.1 Pro";
    public string Description => "Fotogerçekçi mikro dokular, gerçek optik lens simülasyonu ve stüdyo ışıklandırması.";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.TextToImage |
        EngineCapabilities.MultipleAspectRatios;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.BflApiKey);
    }

    public async Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = StudioConfigurationManager.Current.BflApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ImageEngineResult.Fail("BFL (FLUX) API Key girilmedi. Lütfen 'Key Yapılandır' butonu ile anahtarınızı kaydedin.", DisplayName);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "flux-pro-1.1" : request.ModelName.Trim();
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
            // 1. Submit Generation Task
            string endpoint = $"https://api.bfl.ml/v1/{model}";
            using var submitReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
            submitReq.Headers.Add("x-key", apiKey);

            var submitPayload = new
            {
                prompt = finalPrompt,
                width = 1024,
                height = 1024,
                prompt_upsampling = false,
                seed = Random.Shared.Next(1, 999999),
                safety_tolerance = 2
            };

            submitReq.Content = new StringContent(JsonSerializer.Serialize(submitPayload), Encoding.UTF8, "application/json");
            using var submitResp = await HttpClient.SendAsync(submitReq, cancellationToken);
            var submitBody = await submitResp.Content.ReadAsStringAsync(cancellationToken);

            if (!submitResp.IsSuccessStatusCode)
            {
                return ImageEngineResult.Fail($"FLUX Görev Başlatma Hatası ({submitResp.StatusCode}): {submitBody}", DisplayName);
            }

            using var submitDoc = JsonDocument.Parse(submitBody);
            if (!submitDoc.RootElement.TryGetProperty("id", out var idProp))
            {
                return ImageEngineResult.Fail("FLUX görev ID'si alınamadı.", DisplayName);
            }

            string taskId = idProp.GetString()!;

            // 2. Poll for Completion (up to 45 seconds)
            string pollUrl = $"https://api.bfl.ml/v1/get_result?id={taskId}";
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1500, cancellationToken);
                using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                pollReq.Headers.Add("x-key", apiKey);

                using var pollResp = await HttpClient.SendAsync(pollReq, cancellationToken);
                if (pollResp.IsSuccessStatusCode)
                {
                    var pollBody = await pollResp.Content.ReadAsStringAsync(cancellationToken);
                    using var pollDoc = JsonDocument.Parse(pollBody);
                    if (pollDoc.RootElement.TryGetProperty("status", out var statusProp))
                    {
                        string status = statusProp.GetString()!;
                        if (status.Equals("Ready", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pollDoc.RootElement.TryGetProperty("result", out var resProp) &&
                                resProp.TryGetProperty("sample", out var sampleProp))
                            {
                                string imageUrl = sampleProp.GetString()!;
                                var imageBytes = await HttpClient.GetByteArrayAsync(imageUrl, cancellationToken);
                                using var ms = new MemoryStream(imageBytes);
                                sw.Stop();
                                return ImageEngineResult.Ok(new Bitmap(ms), DisplayName, model, sw.ElapsedMilliseconds);
                            }
                        }
                        else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            return ImageEngineResult.Fail("FLUX görsel üretim görevi başarısız oldu.", DisplayName);
                        }
                    }
                }
            }

            sw.Stop();
            return ImageEngineResult.Fail("FLUX görsel üretimi zaman aşımına uğradı.", DisplayName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ImageEngineResult.Fail($"FLUX bağlantı hatası: {ex.Message}", DisplayName);
        }
    }
}
