namespace SimilarProductsWinForms.Studio.Engines;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Services;

public sealed class GoogleGeminiEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();

    public string EngineId => "gemini";
    public string DisplayName => "Google Gemini Flash Image (Banana / Multimodal)";
    public string Description => "Yerel multimodal görsel üretimi ve ürün korumalı yeniden bağlamlandırma (Visual Grounding).";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.TextToImage |
        EngineCapabilities.VisualGrounding |
        EngineCapabilities.MultipleAspectRatios;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.GoogleGeminiApiKey);
    }

    public async Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = StudioConfigurationManager.Current.GoogleGeminiApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ImageEngineResult.Fail("Google Gemini API Key girilmedi. Lütfen 'Key Yapılandır' butonu ile anahtarınızı kaydedin.", DisplayName);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gemini-3.1-flash-image" : request.ModelName.Trim();
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
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("x-goog-api-key", apiKey);

            var partsList = new List<object>();

            // 🍌 Visual Grounding (E-Ticaret Ürün Koruma):
            // Eğer kullanıcı bir ürün görseli yüklemişse, görseli inlineData olarak Gemini'ye besliyoruz!
            if (request.InputImage != null && request.PreserveProduct)
            {
                byte[] productJpeg = OptimizeImageForGrounding(request.InputImage);
                string b64Product = Convert.ToBase64String(productJpeg);

                string groundingText =
                    "You are a professional commercial e-commerce photographer. " +
                    "CRITICAL INSTRUCTION: Keep the product shown in the input image completely identical, maintaining its exact physical structure, geometry, materials, texture, colors, and branding without any distortion or hallucination. " +
                    $"Place this exact product into the following realistic commercial lifestyle scene: {finalPrompt}. " +
                    "Render realistic physical contact shadows and complementary studio lighting matching the environment.";

                partsList.Add(new { text = groundingText });
                partsList.Add(new
                {
                    inlineData = new
                    {
                        mimeType = "image/jpeg",
                        data = b64Product
                    }
                });
            }
            else
            {
                partsList.Add(new { text = finalPrompt });
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = partsList.ToArray()
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" }
                }
            };

            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var b64Output = FindBase64(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(b64Output))
                {
                    byte[] bytes = Convert.FromBase64String(b64Output);
                    using var ms = new MemoryStream(bytes);
                    return ImageEngineResult.Ok(new Bitmap(ms), DisplayName, model, sw.ElapsedMilliseconds);
                }

                return ImageEngineResult.Fail("Gemini API yanıt verdi fakat görsel verisi bulunamadı.", DisplayName);
            }

            string errSummary = ParseGeminiError((int)response.StatusCode, body);
            return ImageEngineResult.Fail(errSummary, DisplayName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ImageEngineResult.Fail($"Gemini bağlantı hatası: {ex.Message}", DisplayName);
        }
    }

    private static byte[] OptimizeImageForGrounding(Bitmap source)
    {
        int maxDim = 1024;
        int w = source.Width;
        int h = source.Height;

        if (w > maxDim || h > maxDim)
        {
            float scale = Math.Min((float)maxDim / w, (float)maxDim / h);
            w = (int)(w * scale);
            h = (int)(h * scale);
        }

        using var resized = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            g.DrawImage(source, 0, 0, w, h);
        }

        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static string? FindBase64(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
            {
                var s = dataEl.GetString();
                if (!string.IsNullOrEmpty(s) && s.Length > 200) return s;
            }
            if (element.TryGetProperty("bytesBase64Encoded", out var bEl) && bEl.ValueKind == JsonValueKind.String)
            {
                var s = bEl.GetString();
                if (!string.IsNullOrEmpty(s) && s.Length > 200) return s;
            }
            foreach (var prop in element.EnumerateObject())
            {
                var found = FindBase64(prop.Value);
                if (found != null) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindBase64(item);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static string ParseGeminiError(int statusCode, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errEl))
            {
                if (errEl.TryGetProperty("message", out var msgEl))
                {
                    var msg = msgEl.GetString() ?? "";
                    if (msg.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase))
                        return "Gemini API Key geçersiz. Lütfen doğru bir anahtar girin.";

                    if (msg.Contains("limit: 0", StringComparison.OrdinalIgnoreCase) || 
                        msg.Contains("free_tier", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Google Gemini Ücretsiz Katman (Free Tier) Kısıtlaması (limit: 0):\n\n" +
                               "Google politikası gereği, Görsel Üretim ve Düzenleme modelleri (gemini-flash-image / imagen) " +
                               "ücretsiz API projelerinde 0 KOTA (limit: 0) olarak kısıtlanmıştır.\n\n" +
                               "Çözüm Seçenekleri:\n" +
                               "1. Google AI Studio'da projenize 'Pay-as-you-go' (Fatura/Kart) bağlayıp yeni bir API anahtarı alın (Görsel başı ~$0.03).\n" +
                               "2. Veya üst menüden 'OpenAI (DALL-E 3)' ya da 'PhotoRoom' motoruna geçiş yaparak işlemi tamamlayın.";
                    }

                    if (msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                        return "Gemini anlık istek sınırına ulaşıldı (Rate Limit). Lütfen 30 saniye bekleyip tekrar deneyin.";

                    return $"Gemini Hatası ({statusCode}): {msg}";
                }
            }
        }
        catch { }

        return $"Gemini API Hatası ({statusCode}): {body}";
    }
}
