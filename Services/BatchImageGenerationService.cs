namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Models;

public sealed record BatchSceneResult(
    string SceneName,
    string Prompt,
    Bitmap? Image,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Pipeline for batch-generating 4 distinct commercial Etsy mockups in sequence.
/// </summary>
internal sealed class BatchImageGenerationService
{
    public static readonly (string Key, string Name, string PromptTemplate)[] StandardScenes = [
        (
            "white_catalog",
            "⚪ Beyaz Katalog & AI Gölge",
            "Commercial e-commerce catalog product photograph of {0}, centered on pure seamless clean white infinite background, soft natural commercial contact shadow, sharp 8k studio quality"
        ),
        (
            "rustic_oak",
            "🪵 Ahşap Rustic Lifestyle",
            "Commercial lifestyle product photography of {0} placed on a rustic weathered oak wooden tabletop, soft warm morning window sunlight, subtle realistic contact shadow, shallow depth of field"
        ),
        (
            "luxury_marble",
            "🏛️ Lüks Mermer Kaide",
            "Minimalist luxury commercial product photograph of {0} standing on an elegant polished white Carrara marble pedestal podium, soft neutral museum gallery strobe lighting"
        ),
        (
            "cozy_boho",
            "🌿 Sıcak Boho & Botanik",
            "Warm bohemian lifestyle product photography of {0} surrounded by lush monstera leaves and natural terracotta pottery, soft sun flare, cozy inviting home decor aesthetic"
        )
    ];

    public static async Task<List<BatchSceneResult>> RunBatchAsync(
        string productTitle,
        Bitmap? originalImage,
        int engineIndex,
        AiOptimizationSettings aiSettings,
        PhotoRoomSettings photoRoomSettings,
        string? promptModifier = null,
        IProgress<(int Step, int Total, string Message, Bitmap? ResultImage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BatchSceneResult>();
        string product = string.IsNullOrWhiteSpace(productTitle) ? "handcrafted artisan product" : productTitle.Trim();

        byte[]? originalBytes = null;
        if (originalImage != null)
        {
            using var ms = new MemoryStream();
            originalImage.Save(ms, ImageFormat.Png);
            originalBytes = ms.ToArray();
        }

        int total = StandardScenes.Length;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (key, name, template) = StandardScenes[i];

            string basePrompt = string.Format(template, product);
            if (!string.IsNullOrWhiteSpace(promptModifier))
            {
                basePrompt += $", {promptModifier}";
            }

            progress?.Report((i + 1, total, $"[{i + 1}/{total}] {name} üretiliyor...", null));

            try
            {
                Bitmap? generated = null;
                bool success = false;
                string? error = null;

                if (engineIndex == 0) // PhotoRoom Native
                {
                    if (originalBytes == null)
                    {
                        results.Add(new BatchSceneResult(name, basePrompt, null, false, "Orijinal ürün görseli gerekli."));
                        continue;
                    }

                    string mode = key == "white_catalog" ? "remove_bg" : "ai_background";
                    string? bgColor = key == "white_catalog" ? "FFFFFF" : null;
                    string? prompt = key == "white_catalog" ? null : basePrompt;

                    var resp = await PhotoRoomApiService.EditProductPhotoAsync(
                        originalBytes,
                        photoRoomSettings.ApiKey,
                        mode,
                        prompt,
                        bgColor,
                        "ai_soft",
                        0.1,
                        cancellationToken);

                    success = resp.Success;
                    generated = resp.ResultImage;
                    error = resp.ErrorMessage;
                }
                else if (engineIndex == 1) // OpenAI (GPT Image 2)
                {
                    string model = AiModelNormalizer.NormalizeOpenAiImageModel(aiSettings.OpenAiImageModel);
                    var resp = await AiImageGenerationService.GenerateWithOpenAiAsync(
                        basePrompt,
                        aiSettings.OpenAiApiKey,
                        model,
                        "1024x1024",
                        null,
                        cancellationToken);

                    success = resp.Success;
                    generated = resp.ResultImage;
                    error = resp.ErrorMessage;
                }
                else if (engineIndex == 2) // Black Forest Labs (BFL FLUX)
                {
                    string bflModel = !string.IsNullOrWhiteSpace(aiSettings.BflModel) ? aiSettings.BflModel : "flux-pro-1.1";
                    var resp = await AiImageGenerationService.GenerateWithBflFluxAsync(
                        basePrompt,
                        aiSettings.BflApiKey,
                        bflModel,
                        1024,
                        1024,
                        cancellationToken);

                    success = resp.Success;
                    generated = resp.ResultImage;
                    error = resp.ErrorMessage;
                }
                else if (engineIndex == 3) // Ideogram 4.0
                {
                    var resp = await AiImageGenerationService.GenerateWithIdeogramAsync(
                        basePrompt,
                        aiSettings.IdeogramApiKey,
                        null,
                        "REALISTIC",
                        "ASPECT_1_1",
                        cancellationToken);

                    success = resp.Success;
                    generated = resp.ResultImage;
                    error = resp.ErrorMessage;
                }
                else // 4: Google Gemini (Gemini 3.1 Flash Image / Nano Banana 2)
                {
                    string geminiModel = !string.IsNullOrWhiteSpace(aiSettings.GeminiImageModel) ? aiSettings.GeminiImageModel : "gemini-3.1-flash-image";
                    var resp = await AiImageGenerationService.GenerateWithGeminiImagenAsync(
                        basePrompt,
                        aiSettings.GeminiApiKey,
                        geminiModel,
                        "1:1",
                        cancellationToken);

                    success = resp.Success;
                    generated = resp.ResultImage;
                    error = resp.ErrorMessage;
                }

                results.Add(new BatchSceneResult(name, basePrompt, generated, success, error));
                progress?.Report((i + 1, total, $"[{i + 1}/{total}] {name} tamamlandı.", generated));

                // Small delay between requests to be gentle on API quotas
                if (i < total - 1)
                {
                    await Task.Delay(800, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                results.Add(new BatchSceneResult(name, basePrompt, null, false, ex.Message));
            }
        }

        return results;
    }
}
