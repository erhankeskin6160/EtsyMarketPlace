namespace SimilarProductsWinForms.Studio.Engines;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Services;

public sealed class PhotoRoomEngine : IAiImageEngine
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiEndpoint = "https://sdk.photoroom.com/v1/segment";

    public string EngineId => "photoroom";
    public string DisplayName => "PhotoRoom Native (Milimetrik Kesim & AI Gölge)";
    public string Description => "Piksel hassasiyetinde arka plan kaldırma, e-ticaret yumuşak gölgesi ve anında stüdyo arka planı.";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.BackgroundRemoval |
        EngineCapabilities.ShadowGeneration |
        EngineCapabilities.VisualGrounding |
        EngineCapabilities.TransparentBackground;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.PhotoRoomApiKey);
    }

    public async Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = StudioConfigurationManager.Current.PhotoRoomApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ImageEngineResult.Fail("PhotoRoom API Key girilmedi. Lütfen 'Key Yapılandır' butonu ile anahtarınızı kaydedin.", DisplayName);
        }

        if (request.InputImage == null)
        {
            return ImageEngineResult.Fail("Lütfen işlenecek orijinal ürün fotoğrafını yükleyin.", DisplayName);
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Convert InputImage to clean PNG bytes
            using var msInput = new MemoryStream();
            request.InputImage.Save(msInput, ImageFormat.Png);
            byte[] imageBytes = msInput.ToArray();

            using var content = new MultipartFormDataContent();
            var byteArrayContent = new ByteArrayContent(imageBytes);
            byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(byteArrayContent, "image_file", "product.png");

            // Shadow Mode
            string shadow = string.IsNullOrWhiteSpace(request.ShadowMode) ? "ai_soft" : request.ShadowMode;
            if (shadow != "none")
            {
                content.Add(new StringContent(shadow), "shadow.mode");
            }

            // Padding
            if (request.Padding > 0)
            {
                content.Add(new StringContent(request.Padding.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)), "padding");
            }

            // Mode & Background settings
            if (request.ProcessMode == "white_bg")
            {
                content.Add(new StringContent("FFFFFF"), "bg_color");
            }
            else if (request.ProcessMode == "ai_background" && !string.IsNullOrWhiteSpace(request.Prompt))
            {
                content.Add(new StringContent(request.Prompt.Trim()), "background.prompt");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Content = content;

            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var resultBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                using var msOutput = new MemoryStream(resultBytes);
                var bmp = new Bitmap(msOutput);
                return ImageEngineResult.Ok(new Bitmap(bmp), DisplayName, "photoroom-v1-segment", sw.ElapsedMilliseconds);
            }

            var errText = await response.Content.ReadAsStringAsync(cancellationToken);
            string friendlyMsg = ParsePhotoRoomError((int)response.StatusCode, errText);
            return ImageEngineResult.Fail(friendlyMsg, DisplayName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ImageEngineResult.Fail($"PhotoRoom bağlantı hatası: {ex.Message}", DisplayName);
        }
    }

    private static string ParsePhotoRoomError(int statusCode, string body)
    {
        if (statusCode == 402 || body.Contains("exhausted", StringComparison.OrdinalIgnoreCase))
        {
            return "⚠️ PhotoRoom Plan Limitiniz Doldu (402 Payment Required)!\n\n" +
                   "Girdiğiniz Live API Key'in ücretsiz deneme kredisi (10 adet görsel) tükenmiştir.\n\n" +
                   "💡 ÇÖZÜM:\n" +
                   "1. '🔑 Key Yapılandır' butonuna tıklayarak Sandbox Key'inizi (sandbox_sk_...) girin. Sandbox Key ile ayda 1.000 görseli ÜCRETSİZ işleyebilirsiniz.\n" +
                   "2. Veya PhotoRoom Dashboard üzerinden planınıza kredi yükleyebilirsiniz.";
        }

        if (statusCode == 401)
        {
            return "⚠️ PhotoRoom API Key Geçersiz (401 Unauthorized).\nLütfen '🔑 Key Yapılandır' butonuna tıklayarak doğru anahtarı girdiğinizden emin olun.";
        }

        if (statusCode == 429)
        {
            return "⚠️ PhotoRoom İstek Sınırı Aşıldı (429 Rate Limit).\nLütfen birkaç saniye bekleyip tekrar deneyin.";
        }

        return $"PhotoRoom Hatası ({statusCode}): {body}";
    }
}
