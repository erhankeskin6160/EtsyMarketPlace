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
using SimilarProductsWinForms.Services;

public sealed class OpenAiImageEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiEndpoint = "https://api.openai.com/v1/images/generations";

    public string EngineId => "openai";
    public string DisplayName => "OpenAI (GPT Image 2.5 & DALL-E 3)";
    public string Description => "En yüksek görsel muhakeme kabiliyeti, milimetrik arka plan düzenleme ve sıfırdan e-ticaret sahneleri.";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.TextToImage |
        EngineCapabilities.BackgroundReplace |
        EngineCapabilities.ImageEdit |
        EngineCapabilities.DraftAndRefine |
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

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gpt-image-2.5-flare" : request.ModelName.Trim();
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

        // 🎨 Arka Plan Düzenleme / Edit Modu (Eğer girdi görseli varsa veya EditMode belirtilmişse)
        bool isEditMode = request.EditMode == "edit" || request.EditMode == "bg_replace" || (request.InputImage != null && request.PreserveProduct);
        if (isEditMode && request.InputImage != null)
        {
            var editSw = Stopwatch.StartNew();
            using var msInput = new MemoryStream();
            request.InputImage.Save(msInput, System.Drawing.Imaging.ImageFormat.Png);
            byte[] inputBytes = msInput.ToArray();

            string editModel = model.Contains("sunburst") ? "gpt-image-2.5-sunburst" : "gpt-image-2.5-flare";
            string quality = string.IsNullOrWhiteSpace(request.QualityTier) ? "high" : request.QualityTier;

            byte[]? maskBytes = request.MaskBytes;
            if (maskBytes == null)
            {
                string prKey = StudioConfigurationManager.Current.PhotoRoomApiKey?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(prKey))
                {
                    try
                    {
                        var maskRes = await BackgroundMaskService.CreateCutoutAndMaskWithPhotoRoomAsync(inputBytes, prKey, cancellationToken);
                        if (maskRes.Success && maskRes.MaskPngBytes != null)
                        {
                            maskBytes = maskRes.MaskPngBytes;
                        }
                    }
                    catch { }
                }
            }

            maskBytes ??= BackgroundMaskService.CreateFallbackOpenAiMask(request.InputImage);

            var (editSuccess, editImg, editErr) = await AiImageGenerationService.EditWithOpenAiAsync(
                inputBytes,
                finalPrompt,
                apiKey,
                maskBytes,
                editModel,
                quality,
                "2048x2048",
                cancellationToken);

            editSw.Stop();
            if (editSuccess && editImg != null)
            {
                return ImageEngineResult.Ok(editImg, DisplayName, editModel, editSw.ElapsedMilliseconds);
            }

            return ImageEngineResult.Fail(editErr, DisplayName);
        }

        if (model.Contains("gpt-image-2") && !model.Contains("2.5"))
        {
            // Normalize legacy
            model = "gpt-image-2.5-flare";
        }

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
