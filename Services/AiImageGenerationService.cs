namespace SimilarProductsWinForms.Services;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Models;

internal sealed class AiImageGenerationService
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// OpenAI DALL-E 3 / GPT Image API üzerinden görsel üretir.
    /// </summary>
    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> GenerateWithOpenAiAsync(
        string prompt,
        string apiKey,
        string model = "gpt-image-2",
        string size = "1024x1024",
        string? background = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "OpenAI API Key girmediniz. Lütfen AI Ayarları alanından geçerli bir API Key girin.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, null, "Lütfen görsel üretimi için bir sahne promptu girin.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            string actualModel = AiModelNormalizer.NormalizeOpenAiImageModel(model);

            object payload;
            if (!string.IsNullOrWhiteSpace(background) && (actualModel.Contains("image-2") || actualModel.Contains("gpt-image")))
            {
                payload = new
                {
                    prompt = prompt.Trim(),
                    model = actualModel,
                    n = 1,
                    size = size,
                    background = background.Trim()
                };
            }
            else
            {
                payload = new
                {
                    prompt = prompt.Trim(),
                    model = actualModel,
                    n = 1,
                    size = size
                };
            }

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Eğer DALL-E 3 hesap tier yetersizliği nedeniyle başarısız olduysa, otomatik DALL-E 2'yi dene
                if (actualModel == "dall-e-3" && body.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    return await GenerateWithOpenAiAsync(prompt, apiKey, "dall-e-2", "1024x1024", background: null, cancellationToken: cancellationToken);
                }

                string friendlyMsg = ParseOpenAiError((int)response.StatusCode, body);
                return (false, null, friendlyMsg);
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
            {
                var firstItem = dataArray[0];
                if (firstItem.TryGetProperty("b64_json", out var b64Prop))
                {
                    byte[] bytes = Convert.FromBase64String(b64Prop.GetString()!);
                    using var ms = new MemoryStream(bytes);
                    return (true, new Bitmap(ms), "Başarılı");
                }
                else if (firstItem.TryGetProperty("url", out var urlProp))
                {
                    var imgUrl = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(imgUrl))
                    {
                        var bytes = await HttpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                        using var ms = new MemoryStream(bytes);
                        return (true, new Bitmap(ms), "Başarılı");
                    }
                }
            }

            return (false, null, "OpenAI yanıtında görsel verisi bulunamadı.");
        }
        catch (Exception ex)
        {
            return (false, null, $"OpenAI Bağlantı Hatası: {ex.Message}");
        }
    }

    private static string ParseOpenAiError(int statusCode, string body)
    {
        if (body.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("invalid_value", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("billing_hard_limit_reached", StringComparison.OrdinalIgnoreCase))
        {
            return "⚠️ OpenAI Hesabınızda Görsel Üretim Bakiyesi / Kredisi Yok:\n\nOpenAI platformunda görsel üretimi (DALL-E / GPT Image) için hesabınızda ön ödemeli bakiye olması gerekmektedir (platform.openai.com/billing).\n\n👉 Ne Yapabilirsiniz?\n1. OpenAI hesabınıza (platform.openai.com) bakiye yükleyebilirsiniz,\n2. VEYA üstteki 'İşlem Yapacak AI Motoru' kutusundan 'Google Gemini (Imagen 3)' ya da 'PhotoRoom' motorunu seçerek ücretsiz görsel üretmeye hemen devam edebilirsiniz!";
        }

        if (statusCode == 401 || body.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
        {
            return "⚠️ Geçersiz OpenAI API Anahtarı:\n\nOpenAI API anahtarınız doğrulanamadı. Lütfen 'AI Ayarları' penceresinden geçerli bir API Key girdiğinizden emin olun.";
        }

        if (statusCode == 429)
        {
            return "⚠️ İstek Limiti Aşıldı (Rate Limit):\n\nOpenAI dakikalık istek limitine ulaşıldı veya bakiyeniz yetersiz. Lütfen 30 saniye sonra tekrar deneyin veya Google Gemini motorunu seçin.";
        }

        return $"OpenAI API Hatası (HTTP {statusCode}): {body}";
    }

    /// <summary>
    /// OpenAI /v1/images/edits endpoint'i ile görselin arka planını değiştirir (GPT-Image-2.5 Flare & Sunburst).
    /// </summary>
    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> EditWithOpenAiAsync(
        byte[] imageBytes,
        string prompt,
        string apiKey,
        byte[]? maskBytes = null,
        string model = "gpt-image-2.5-flare",
        string quality = "high",
        string size = "2048x2048",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "OpenAI API Key girmediniz. Lütfen AI Ayarları alanından geçerli bir API Key girin.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, null, "Lütfen arka plan için bir sahne promptu girin.");
        }

        if (imageBytes == null || imageBytes.Length == 0)
        {
            return (false, null, "Düzenlenecek ürün görseli bulunamadı.");
        }

        try
        {
            string actualModel = AiModelNormalizer.NormalizeOpenAiImageModel(model);
            using var content = new MultipartFormDataContent();

            var imgContent = new ByteArrayContent(imageBytes);
            imgContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(imgContent, "image", "input.png");

            if (maskBytes != null && maskBytes.Length > 0)
            {
                var maskContent = new ByteArrayContent(maskBytes);
                maskContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                content.Add(maskContent, "mask", "mask.png");
            }

            content.Add(new StringContent(prompt.Trim()), "prompt");
            content.Add(new StringContent(actualModel), "model");
            content.Add(new StringContent(size), "size");
            content.Add(new StringContent(quality), "quality");
            content.Add(new StringContent("b64_json"), "response_format");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Content = content;

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string friendlyMsg = ParseOpenAiError((int)response.StatusCode, body);
                return (false, null, friendlyMsg);
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
            {
                var firstItem = dataArray[0];
                if (firstItem.TryGetProperty("b64_json", out var b64Prop))
                {
                    byte[] bytes = Convert.FromBase64String(b64Prop.GetString()!);
                    using var ms = new MemoryStream(bytes);
                    return (true, new Bitmap(ms), "Başarılı");
                }
                else if (firstItem.TryGetProperty("url", out var urlProp))
                {
                    var imgUrl = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(imgUrl))
                    {
                        var downloadedBytes = await HttpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                        using var ms = new MemoryStream(downloadedBytes);
                        return (true, new Bitmap(ms), "Başarılı");
                    }
                }
            }

            return (false, null, "OpenAI yanıt verdi fakat görsel verisi bulunamadı.");
        }
        catch (Exception ex)
        {
            return (false, null, $"OpenAI Edit Bağlantı Hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Kullanıcının girdiği ham promptu AI (ChatGPT / Gemini) kullanarak profesyonel Etsy e-ticaret fotoğrafçılığı kalıplarına dönüştürür.
    /// </summary>
    public static async Task<string> EnhancePromptAsync(
        string rawPrompt,
        string productTitle,
        AiOptimizationSettings aiSettings,
        CancellationToken cancellationToken = default)
    {
        if (aiSettings.IsOffline)
        {
            return PromptTipsService.EnhancePromptLocally(rawPrompt, productTitle);
        }

        try
        {
            string systemPrompt = "You are an expert Etsy commercial product photographer. Enhance the given user prompt into an ultra-realistic, commercially appealing background scene prompt. Add professional lighting, depth of field, natural contact shadows, and complementary props. Output ONLY the enhanced English prompt without quotes, markdown, or commentary.";
            string userPrompt = $"Product: {productTitle}\nUser Idea: {rawPrompt}";

            if (aiSettings.UseGemini && !string.IsNullOrWhiteSpace(aiSettings.GeminiApiKey))
            {
                string actualModel = AiModelNormalizer.NormalizeGeminiTextModel(aiSettings.GeminiModel);
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{actualModel}:generateContent?key={aiSettings.GeminiApiKey.Trim()}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                var payload = new
                {
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                    contents = new[] { new { parts = new[] { new { text = userPrompt } } } },
                    generationConfig = new { temperature = 0.7 }
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var resp = await HttpClient.SendAsync(request, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    var text = ExtractTextFromGeminiResponse(body);
                    if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                }
            }
            else if (aiSettings.UseOpenAi && !string.IsNullOrWhiteSpace(aiSettings.OpenAiApiKey))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiSettings.OpenAiApiKey.Trim());
                var payload = new
                {
                    model = string.IsNullOrWhiteSpace(aiSettings.OpenAiModel) ? "gpt-4o" : aiSettings.OpenAiModel.Trim(),
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.7
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var resp = await HttpClient.SendAsync(request, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    var text = ExtractTextFromOpenAiResponse(body);
                    if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                }
            }
        }
        catch
        {
            // fallback to local enhancement
        }

        return PromptTipsService.EnhancePromptLocally(rawPrompt, productTitle);
    }

    /// <summary>
    /// Google Gemini Imagen 3 API üzerinden görsel üretir.
    /// </summary>
    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> GenerateWithGeminiImagenAsync(
        string prompt,
        string apiKey,
        string model = "imagen-3.0-generate-002",
        string aspectRatio = "1:1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Gemini API Key girmediniz. Lütfen AI Ayarları alanından geçerli bir API Key girin.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, null, "Lütfen görsel üretimi için bir sahne promptu girin.");
        }

        try
        {
            string actualModel = string.IsNullOrWhiteSpace(model) ? "gemini-3.1-flash-image" : model.Trim();
            string cleanKey = apiKey.Trim();

            // 🍌 1. YOL: Gemini 3.1 Flash Image (Banana 2) / Gemini Image Modelleri (:generateContent)
            if (actualModel.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(actualModel)}:generateContent?key={Uri.EscapeDataString(cleanKey)}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", cleanKey);

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt.Trim() }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        responseModalities = new[] { "IMAGE" }
                    }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await HttpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(body);
                    var b64 = FindBase64(doc.RootElement);
                    if (!string.IsNullOrWhiteSpace(b64))
                    {
                        byte[] bytes = Convert.FromBase64String(b64);
                        using var ms = new MemoryStream(bytes);
                        return (true, new Bitmap(ms), "Başarılı");
                    }
                }
                else if (!actualModel.Equals("imagen-3.0-generate-002", StringComparison.OrdinalIgnoreCase))
                {
                    string friendlyMsg = ParseGeminiError((int)response.StatusCode, body);
                    return (false, null, friendlyMsg);
                }
            }

            // 2. YOL: Imagen 3 :predict
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-002:predict?key={Uri.EscapeDataString(cleanKey)}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", cleanKey);

                var payload = new
                {
                    instances = new[]
                    {
                        new { prompt = prompt.Trim() }
                    },
                    parameters = new
                    {
                        sampleCount = 1,
                        aspectRatio = string.IsNullOrWhiteSpace(aspectRatio) ? "1:1" : aspectRatio,
                        outputOptions = new { mimeType = "image/jpeg" },
                        personGeneration = "ALLOW_ADULT"
                    }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await HttpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string friendlyMsg = ParseGeminiError((int)response.StatusCode, body);
                    return (false, null, friendlyMsg);
                }

                using var doc = JsonDocument.Parse(body);
                var b64 = FindBase64(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(b64))
                {
                    byte[] bytes = Convert.FromBase64String(b64);
                    using var ms = new MemoryStream(bytes);
                    return (true, new Bitmap(ms), "Başarılı");
                }
            }

            return (false, null, "Gemini görsel yanıtında veri bulunamadı.");
        }
        catch (Exception ex)
        {
            return (false, null, $"Gemini Bağlantı Hatası: {ex.Message}");
        }
    }

    private static string? FindBase64(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String) return data.GetString();
            if (element.TryGetProperty("imageBytes", out var imgBytes) && imgBytes.ValueKind == JsonValueKind.String) return imgBytes.GetString();
            if (element.TryGetProperty("bytesBase64Encoded", out var b64Enc) && b64Enc.ValueKind == JsonValueKind.String) return b64Enc.GetString();
            if (element.TryGetProperty("b64_json", out var b64Json) && b64Json.ValueKind == JsonValueKind.String) return b64Json.GetString();

            foreach (var prop in element.EnumerateObject())
            {
                var found = FindBase64(prop.Value);
                if (!string.IsNullOrWhiteSpace(found)) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindBase64(item);
                if (!string.IsNullOrWhiteSpace(found)) return found;
            }
        }

        return null;
    }

    private static string ParseGeminiError(int statusCode, string body)
    {
        if (body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) || statusCode == 429)
        {
            return "⚠️ Google Gemini API Kotası Aşıldı:\n\nGemini API istek limitine ulaşıldı. Lütfen 1 dakika sonra tekrar deneyin veya OpenAI / PhotoRoom motoruna geçiş yapın.";
        }

        if (body.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) || (statusCode == 400 && body.Contains("API key", StringComparison.OrdinalIgnoreCase)))
        {
            return "⚠️ Geçersiz Google Gemini API Anahtarı:\n\nLütfen 'AI Ayarları' penceresinden geçerli bir Gemini API Key girdiğinizden emin olun.";
        }

        return $"Gemini Imagen API Hatası (HTTP {statusCode}): {body}";
    }

    /// <summary>
    /// Black Forest Labs (BFL) Resmi API'si (api.bfl.ml) üzerinden FLUX.1 / FLUX.2 ile fotogerçekçi görsel üretir.
    /// </summary>
    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> GenerateWithBflFluxAsync(
        string prompt,
        string apiKey,
        string model = "flux-pro-1.1",
        int width = 1024,
        int height = 1024,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Black Forest Labs (BFL) API Key tanımlı değil. Lütfen AI Ayarları penceresinden BFL API Key girin.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, null, "Lütfen bir sahne promptu girin.");
        }

        try
        {
            string cleanKey = apiKey.Trim();
            string endpoint = model.Contains("dev")
                ? "https://api.bfl.ml/v1/flux-dev"
                : (model.Contains("schnell") ? "https://api.bfl.ml/v1/flux-schnell" : "https://api.bfl.ml/v1/flux-pro-1.1");

            using var submitReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
            submitReq.Headers.Add("x-key", cleanKey);

            var payload = new
            {
                prompt = prompt.Trim(),
                width = width,
                height = height,
                prompt_upsampling = false,
                safety_tolerance = 2
            };

            submitReq.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var submitResp = await HttpClient.SendAsync(submitReq, cancellationToken);
            var submitBody = await submitResp.Content.ReadAsStringAsync(cancellationToken);

            if (!submitResp.IsSuccessStatusCode)
            {
                return (false, null, $"BFL API Hatası (HTTP {(int)submitResp.StatusCode}): {submitBody}");
            }

            using var submitDoc = JsonDocument.Parse(submitBody);
            if (!submitDoc.RootElement.TryGetProperty("id", out var idProp))
            {
                return (false, null, "BFL API yanıtında task id bulunamadı.");
            }

            string taskId = idProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return (false, null, "Geçersiz BFL task id.");
            }

            // Polling loop (max 60 seconds)
            var pollUrl = $"https://api.bfl.ml/v1/get_result?id={Uri.EscapeDataString(taskId)}";
            for (int i = 0; i < 40; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1500, cancellationToken);

                using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                pollReq.Headers.Add("x-key", cleanKey);

                using var pollResp = await HttpClient.SendAsync(pollReq, cancellationToken);
                var pollBody = await pollResp.Content.ReadAsStringAsync(cancellationToken);

                if (!pollResp.IsSuccessStatusCode) continue;

                using var pollDoc = JsonDocument.Parse(pollBody);
                string status = pollDoc.RootElement.TryGetProperty("status", out var st) ? (st.GetString() ?? "") : "";

                if (status.Equals("Ready", StringComparison.OrdinalIgnoreCase))
                {
                    if (pollDoc.RootElement.TryGetProperty("result", out var resObj) &&
                        resObj.TryGetProperty("sample", out var sampleUrl))
                    {
                        var imgUrl = sampleUrl.GetString();
                        if (!string.IsNullOrWhiteSpace(imgUrl))
                        {
                            var bytes = await HttpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                            using var ms = new MemoryStream(bytes);
                            return (true, new Bitmap(ms), "Başarılı");
                        }
                    }
                    return (false, null, "BFL görsel URL'i okunamadı.");
                }
                else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase) || status.Equals("Error", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, null, $"BFL üretim başarısız oldu: {pollBody}");
                }
            }

            return (false, null, "BFL görsel üretimi zaman aşımına uğradı (60s). Lütfen tekrar deneyin.");
        }
        catch (Exception ex)
        {
            return (false, null, $"BFL FLUX Bağlantı Hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Ideogram 4.0 API (api.ideogram.ai) üzerinden görsel içine kusursuz tipografi basarak üretir.
    /// </summary>
    public static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> GenerateWithIdeogramAsync(
        string prompt,
        string apiKey,
        string? typographyText = null,
        string stylePreset = "REALISTIC",
        string aspectRatio = "ASPECT_1_1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Ideogram API Key tanımlı değil. Lütfen AI Ayarları penceresinden Ideogram API Key girin.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, null, "Lütfen bir sahne promptu girin.");
        }

        try
        {
            string finalPrompt = prompt.Trim();
            if (!string.IsNullOrWhiteSpace(typographyText))
            {
                finalPrompt += $", with crisp legible bold typography text \"{typographyText.Trim()}\" printed clearly on the product surface";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ideogram.ai/v1/ideogram-v4/generate");
            request.Headers.Add("Api-Key", apiKey.Trim());

            var payload = new
            {
                image_request = new
                {
                    prompt = finalPrompt,
                    aspect_ratio = aspectRatio,
                    model = "V_2",
                    magic_prompt_option = "AUTO",
                    style_preset = stylePreset
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Ideogram API Hatası (HTTP {(int)response.StatusCode}): {body}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
            {
                var first = dataArray[0];
                if (first.TryGetProperty("url", out var urlProp))
                {
                    var imgUrl = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(imgUrl))
                    {
                        var bytes = await HttpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                        using var ms = new MemoryStream(bytes);
                        return (true, new Bitmap(ms), "Başarılı");
                    }
                }
            }

            return (false, null, "Ideogram yanıtında görsel bulunamadı.");
        }
        catch (Exception ex)
        {
            return (false, null, $"Ideogram Bağlantı Hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// ChatGPT veya Gemini kullanarak ürün ve sahne temasına göre yüksek dönüşüm getiren Etsy stüdyo promptu üretir.
    /// </summary>
    public static async Task<string> GenerateSmartPromptAsync(
        string productTitle,
        string selectedScene,
        AiOptimizationSettings aiSettings,
        CancellationToken cancellationToken = default)
    {
        string baseProduct = string.IsNullOrWhiteSpace(productTitle) ? "handcrafted 3D printed artisan product" : productTitle.Trim();

        // 1. Canlı AI (ChatGPT veya Gemini) ile Üretim Denemesi
        if (!aiSettings.IsOffline)
        {
            try
            {
                string systemPrompt = "You are an elite commercial Etsy product photographer and visual prompt designer. Generate a single, concise, ultra-detailed photorealistic prompt in English for product scene rendering (DALL-E 3 / Imagen / Midjourney style). Output ONLY the final prompt text without markdown fences, quotes, or explanations.";
                string userPrompt = $"Product: {baseProduct}\nDesired Scene Theme: {selectedScene}\nRequirements: Photorealistic commercial product photography, 8k resolution, cinematic studio lighting, natural shadows, depth of field, clean composition, high-end Etsy marketplace style.";

                if (aiSettings.UseGemini && !string.IsNullOrWhiteSpace(aiSettings.GeminiApiKey))
                {
                    string actualModel = AiModelNormalizer.NormalizeGeminiTextModel(aiSettings.GeminiModel);
                    string url = $"https://generativelanguage.googleapis.com/v1beta/models/{actualModel}:generateContent?key={aiSettings.GeminiApiKey.Trim()}";

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    var payload = new
                    {
                        system_instruction = new
                        {
                            parts = new[] { new { text = systemPrompt } }
                        },
                        contents = new[]
                        {
                            new { parts = new[] { new { text = userPrompt } } }
                        },
                        generationConfig = new
                        {
                            temperature = 0.7
                        }
                    };
                    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    using var resp = await HttpClient.SendAsync(request, cancellationToken);
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                        var text = ExtractTextFromGeminiResponse(body);
                        if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                    }
                }
                else if (aiSettings.UseOpenAi && !string.IsNullOrWhiteSpace(aiSettings.OpenAiApiKey))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiSettings.OpenAiApiKey.Trim());
                    var payload = new
                    {
                        model = string.IsNullOrWhiteSpace(aiSettings.OpenAiModel) ? "gpt-5.5" : aiSettings.OpenAiModel.Trim(),
                        instructions = systemPrompt,
                        input = userPrompt
                    };
                    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    using var resp = await HttpClient.SendAsync(request, cancellationToken);
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                        var text = ExtractTextFromOpenAiResponse(body);
                        if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                    }
                }
            }
            catch
            {
                // Fallback to handcrafted template
            }
        }

        // 2. Fallback Handcrafted Template
        return selectedScene switch
        {
            "🪵 Ahşap Rustic Masa" => $"Commercial studio photography of {baseProduct} placed on a rustic weathered oak wooden tabletop, soft warm morning sunlight streaming from a side window, subtle realistic contact shadows, shallow depth of field, 8k sharp focus.",
            "🎮 RGB Gamer Masası" => $"High-end commercial product shot of {baseProduct} displayed on a sleek matte black gaming desk, ambient cyan and magenta neon LED backlighting, subtle surface reflections, clean modern aesthetic, sharp 8k detail.",
            "🏛️ Lüks Mermer Kaide" => $"Luxury minimalist product photography of {baseProduct} standing on a smooth white Carrara marble pedestal podium, elegant soft studio strobe lighting, clean neutral beige background, high-end museum gallery vibe.",
            "🎄 Sıcak Yılbaşı / Noel Ortamı" => $"Festive holiday Etsy product photoshoot of {baseProduct} on a cozy wooden mantle, out-of-focus bokeh fairy lights in the warm background, subtle pine branch accent, warm golden ambient glow.",
            "🌿 Boho Botanik & Gün Işığı" => $"Organic lifestyle product photography of {baseProduct} surrounded by lush green monstera and eucalyptus leaves, soft natural sun flare, clean warm terracotta and beige tones, bohemian home decor.",
            "⚪ Beyaz Stüdyo & AI Gölge" => $"Crisp clean commercial e-commerce product photography of {baseProduct} centered on an infinite seamless pure white studio background, soft natural drop shadow, 2000x2000 Etsy listing catalog quality.",
            _ => $"High-resolution professional commercial studio product photograph of {baseProduct}, soft balanced lighting, crisp details, natural contact shadows, 8k resolution."
        };
    }

    /// <summary>
    /// Görselin üzerine pazarlama rozeti (Overlay Badge) çizer.
    /// </summary>
    public static Bitmap ApplyOverlayBadge(
        Bitmap source,
        string badgeText,
        string position = "Sol Üst",
        Color? customBgColor = null,
        Color? customTextColor = null)
    {
        if (string.IsNullOrWhiteSpace(badgeText)) return new Bitmap(source);

        var result = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Orijinal görseli çiz
        g.DrawImage(source, 0, 0, source.Width, source.Height);

        // Rozet Boyutlandırması (Görsel boyutuna göre dinamik ölçekleme)
        float scale = Math.Max(1.0f, source.Width / 1000.0f);
        float fontSize = 16.0f * scale;
        using var font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        var textSize = g.MeasureString(badgeText, font);
        float paddingX = 18.0f * scale;
        float paddingY = 10.0f * scale;
        float badgeWidth = textSize.Width + (paddingX * 2);
        float badgeHeight = textSize.Height + (paddingY * 2);
        float margin = 24.0f * scale;
        float cornerRadius = 12.0f * scale;

        float x = margin;
        float y = margin;

        switch (position)
        {
            case "Sağ Üst":
                x = source.Width - badgeWidth - margin;
                y = margin;
                break;
            case "Sol Alt":
                x = margin;
                y = source.Height - badgeHeight - margin;
                break;
            case "Sağ Alt":
                x = source.Width - badgeWidth - margin;
                y = source.Height - badgeHeight - margin;
                break;
            default: // Sol Üst
                x = margin;
                y = margin;
                break;
        }

        var badgeRect = new RectangleF(x, y, badgeWidth, badgeHeight);

        // Rozet Arka Planı
        Color bg = customBgColor ?? Color.FromArgb(235, 15, 23, 42); // #0F172A Dark Slate Semi-Transparent
        Color fg = customTextColor ?? Color.FromArgb(255, 255, 255);

        using (var path = GetRoundedRectanglePath(badgeRect, cornerRadius))
        {
            // Hafif gölge
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                var shadowRect = new RectangleF(x + 2 * scale, y + 3 * scale, badgeWidth, badgeHeight);
                using var shadowPath = GetRoundedRectanglePath(shadowRect, cornerRadius);
                g.FillPath(shadowBrush, shadowPath);
            }

            // Rozet Gövdesi
            using var bgBrush = new SolidBrush(bg);
            g.FillPath(bgBrush, path);

            // İnce Border
            using var borderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.5f * scale);
            g.DrawPath(borderPen, path);
        }

        // Rozet Metni
        using (var textBrush = new SolidBrush(fg))
        {
            var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(badgeText, font, textBrush, badgeRect, stringFormat);
        }

        return result;
    }

    private static GraphicsPath GetRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string ExtractTextFromGeminiResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                if (parts[0].TryGetProperty("text", out var textElem))
                {
                    return textElem.GetString() ?? "";
                }
            }
        }
        if (doc.RootElement.TryGetProperty("output_text", out var outProp)) return outProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("output", out var outArray))
        {
            foreach (var item in outArray.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var contentArray))
                {
                    foreach (var c in contentArray.EnumerateArray())
                    {
                        if (c.TryGetProperty("text", out var textProp)) return textProp.GetString() ?? "";
                    }
                }
            }
        }
        return "";
    }

    private static string ExtractTextFromOpenAiResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("output_text", out var outProp)) return outProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }
        }
        return "";
    }
}
