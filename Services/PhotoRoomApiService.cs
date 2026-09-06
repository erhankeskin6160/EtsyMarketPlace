namespace SimilarProductsWinForms.Services;

using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

public sealed class PhotoRoomApiService
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiEndpoint = "https://sdk.photoroom.com/v1/segment";

    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> EditProductPhotoAsync(
        byte[] imageBytes,
        string apiKey,
        string mode = "remove_bg",
        string? bgPrompt = null,
        string? bgColor = null,
        string shadowMode = "ai_soft",
        double padding = 0.1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "PhotoRoom API Key girmediniz. Lütfen geçerli bir PhotoRoom API Key girin.");
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var byteArrayContent = new ByteArrayContent(imageBytes);
            byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(byteArrayContent, "image_file", "input.png");

            // Shadow Mode
            if (!string.IsNullOrWhiteSpace(shadowMode) && shadowMode != "none")
            {
                content.Add(new StringContent(shadowMode), "shadow.mode");
            }

            // Padding / Margining
            if (padding > 0)
            {
                content.Add(new StringContent(padding.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)), "padding");
            }

            // Background Color
            if (!string.IsNullOrWhiteSpace(bgColor))
            {
                content.Add(new StringContent(bgColor.Replace("#", "")), "bg_color");
            }

            // Background Prompt (PhotoRoom Instant Backgrounds)
            if (!string.IsNullOrWhiteSpace(bgPrompt) && mode == "ai_background")
            {
                content.Add(new StringContent(bgPrompt), "background.prompt");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            request.Headers.Add("x-api-key", apiKey.Trim());
            request.Content = content;

            var response = await HttpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var resultBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                using var ms = new MemoryStream(resultBytes);
                var bmp = new Bitmap(ms);
                return (true, new Bitmap(bmp), "Başarılı");
            }

            var errText = await response.Content.ReadAsStringAsync();
            return (false, null, $"PhotoRoom API Hatası ({response.StatusCode}): {errText}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Bağlantı hatası: {ex.Message}");
        }
    }
}
