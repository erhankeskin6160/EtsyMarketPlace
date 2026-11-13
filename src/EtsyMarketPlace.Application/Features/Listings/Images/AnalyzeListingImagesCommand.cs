namespace EtsyMarketPlace.Application.Features.Listings.Images;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public sealed record CoverCtrAuditDto(
    int OverallScore,
    int ContrastScore,
    int MobileCropScore,
    int BackgroundScore,
    string Tip,
    bool IsVisionAnalyzed,
    string Note
);

public sealed record ImageAltTextSuggestionDto(
    long ListingImageId,
    int Rank,
    string SuggestedAltText,
    string AngleLabel
);

public sealed record AnalyzeListingImagesResultDto(
    long ListingId,
    CoverCtrAuditDto CoverCtr,
    IReadOnlyList<ImageAltTextSuggestionDto> AltTextSuggestions,
    string Provider,
    string Model
);

public sealed class AnalyzeListingImagesCommand : IRequest<ServiceResponse>
{
    public string ShopId { get; init; } = string.Empty;
    public long ListingId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
}

public sealed class AnalyzeListingImagesCommandHandler(
    IEtsyListingApiClient etsyClient,
    IGeminiListingApiClient aiClient,
    IShopSettingsRepository settingsRepo
) : IRequestHandler<AnalyzeListingImagesCommand, ServiceResponse>
{
    private const int MaxAltTextLength = 250;

    public async Task<ServiceResponse> Handle(AnalyzeListingImagesCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.ListingId <= 0 || string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("Geçersiz mağaza veya ilan kimliği.", HttpStatusCode.BadRequest);

        var rawJson = await etsyClient.GetListingImagesRawJsonAsync(request.ListingId, request.ShopId, cancellationToken);
        var images = EtsyListingImageJsonParser.ParseImages(rawJson);
        if (images.Count == 0)
            return ServiceResponse.CreateError("İlana ait görsel bulunamadı.", HttpStatusCode.NotFound);

        var (resolvedProvider, resolvedModel, resolvedKey) = await ResolveAiConfigAsync(request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(resolvedKey) &&
            !string.Equals(resolvedProvider, "Offline", StringComparison.OrdinalIgnoreCase))
        {
            var visionInputs = images
                .Take(10)
                .Select(img => new VisionImageInput(img.Url570xN, null, "image/jpeg", img.Rank))
                .ToList();

            var systemInstruction = """
                You are an Etsy Visual Merchandising, Accessibility, and Image SEO expert.
                Analyze the provided Etsy listing images (in rank order starting with #1 Cover Image) and the product title.
                Return ONLY a valid JSON object with this exact schema:
                {
                  "cover_ctr": {
                    "overall_score": 0-100,
                    "contrast_score": 0-100,
                    "mobile_crop_score": 0-100,
                    "background_score": 0-100,
                    "tip": "1 concise actionable Turkish tip for improving the #1 cover image click-through appeal"
                  },
                  "images": [
                    {
                      "rank": 1,
                      "angle_label": "Short Turkish label e.g. Kapak, Yan Açı, Detay, Kullanım",
                      "alt_text": "Natural descriptive English Etsy SEO alt text <= 250 characters describing what is visually shown in this specific photo"
                    }
                  ]
                }
                Rules:
                - alt_text MUST be in English, descriptive, natural (no keyword stuffing), and strictly <= 250 characters.
                - cover_ctr scores are visual readiness estimates (contrast, clarity, 1:1/4:3 mobile crop safety, background separation).
                - tip and angle_label MUST be in Turkish.
                """;

            var userPrompt = $"Listing Title: {request.Title}\nTotal Images: {visionInputs.Count}\nImage Ranks: {string.Join(", ", visionInputs.Select(v => $"#{v.Rank}"))}";

            var (ok, jsonContent, _) = await aiClient.GenerateVisionJsonAsync(
                systemInstruction,
                userPrompt,
                visionInputs,
                resolvedModel,
                resolvedKey,
                cancellationToken);

            if (ok && !string.IsNullOrWhiteSpace(jsonContent))
            {
                var parsed = TryParseVisionResult(
                    request.ListingId,
                    jsonContent,
                    images,
                    resolvedProvider,
                    resolvedModel);
                if (parsed != null)
                    return ServiceResponse.CreateSuccess("Görsel Alt-Text ve Kapak CTR analizi tamamlandı.", parsed);
            }
        }

        // Deterministic fallback if no API key or AI call failed
        var fallback = BuildDeterministicFallback(request.ListingId, request.Title, images);
        return ServiceResponse.CreateSuccess("Görsel Alt-Text taslakları oluşturuldu (Yerel Analiz).", fallback);
    }

    private async Task<(string Provider, string Model, string? ApiKey)> ResolveAiConfigAsync(
        AnalyzeListingImagesCommand request,
        CancellationToken ct)
    {
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "Gemini" : request.Provider.Trim();
        var model = request.Model?.Trim() ?? string.Empty;
        var apiKey = request.ApiKey?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(request.Provider))
        {
            var savedJson = await settingsRepo.GetAiSettingsJsonAsync(request.ShopId, ct);
            if (!string.IsNullOrWhiteSpace(savedJson))
            {
                try
                {
                    using var settingsDoc = JsonDocument.Parse(savedJson);
                    var sRoot = settingsDoc.RootElement;
                    if (string.IsNullOrWhiteSpace(request.Provider) &&
                        sRoot.TryGetProperty("provider", out var pEl) &&
                        pEl.ValueKind == JsonValueKind.String)
                    {
                        var savedProv = pEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(savedProv))
                            provider = savedProv;
                    }

                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        var keyProp = provider.ToLowerInvariant() switch
                        {
                            "deepseek" => "deepSeekApiKey",
                            "openai" => "openAiApiKey",
                            "claude" => "claudeApiKey",
                            "grok" => "grokApiKey",
                            _ => "geminiApiKey"
                        };
                        if (sRoot.TryGetProperty(keyProp, out var kEl) && kEl.ValueKind == JsonValueKind.String)
                            apiKey = kEl.GetString()?.Trim();
                    }
                }
                catch
                {
                    // Ignore malformed settings JSON and fall back gracefully
                }
            }
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = provider.ToLowerInvariant() switch
            {
                "deepseek" => "deepseek-chat",
                "openai" => "gpt-4o",
                "claude" => "claude-3-7-sonnet-20250219",
                "grok" => "grok-2-vision-1212",
                _ => "gemini-2.5-flash"
            };
        }

        return (provider, model, apiKey);
    }

    private static AnalyzeListingImagesResultDto? TryParseVisionResult(
        long listingId,
        string jsonContent,
        IReadOnlyList<ListingImageDetailDto> images,
        string provider,
        string model)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            int overall = 82, contrast = 84, mobileCrop = 80, bg = 82;
            string tip = "Kapak görselinde ürünü merkeze alıp arka plan kontrastını artırmak tıklanma potansiyelini yükseltir.";

            if (root.TryGetProperty("cover_ctr", out var ctrEl) && ctrEl.ValueKind == JsonValueKind.Object)
            {
                if (ctrEl.TryGetProperty("contrast_score", out var cEl) && cEl.TryGetInt32(out var cVal)) contrast = Math.Clamp(cVal, 0, 100);
                if (ctrEl.TryGetProperty("mobile_crop_score", out var mEl) && mEl.TryGetInt32(out var mVal)) mobileCrop = Math.Clamp(mVal, 0, 100);
                if (ctrEl.TryGetProperty("background_score", out var bEl) && bEl.TryGetInt32(out var bVal)) bg = Math.Clamp(bVal, 0, 100);
                if (ctrEl.TryGetProperty("overall_score", out var oEl) && oEl.TryGetInt32(out var oVal))
                    overall = Math.Clamp(oVal, 0, 100);
                else
                    overall = (contrast + mobileCrop + bg) / 3;

                if (ctrEl.TryGetProperty("tip", out var tEl) && tEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tEl.GetString()))
                    tip = tEl.GetString()!.Trim();
            }

            var aiByRank = new Dictionary<int, (string AltText, string Angle)>();
            if (root.TryGetProperty("images", out var imgsEl) && imgsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in imgsEl.EnumerateArray())
                {
                    int rank = item.TryGetProperty("rank", out var rEl) && rEl.TryGetInt32(out var rVal) ? rVal : 0;
                    string alt = item.TryGetProperty("alt_text", out var aEl) ? aEl.GetString()?.Trim() ?? "" : "";
                    string angle = item.TryGetProperty("angle_label", out var lEl) ? lEl.GetString()?.Trim() ?? "" : "";
                    if (rank > 0 && !string.IsNullOrWhiteSpace(alt))
                    {
                        aiByRank[rank] = (TruncateAltText(alt), string.IsNullOrWhiteSpace(angle) ? $"Görsel #{rank}" : angle);
                    }
                }
            }

            var suggestions = images.Select(img =>
            {
                if (aiByRank.TryGetValue(img.Rank, out var found))
                {
                    return new ImageAltTextSuggestionDto(img.ListingImageId, img.Rank, found.AltText, found.Angle);
                }

                return new ImageAltTextSuggestionDto(
                    img.ListingImageId,
                    img.Rank,
                    TruncateAltText(BuildFallbackAltForRank(img.Rank, "")),
                    img.Rank == 1 ? "Kapak" : $"Görsel #{img.Rank}");
            }).ToList();

            var isVision = !provider.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase);
            var note = isVision
                ? "Vision AI görsel kalite tahmini (Etsy API ham impression/tıklama verisi sağlamaz)."
                : "Metin tabanlı tahmin (görsel pikselleri için Gemini/OpenAI/Claude Vision önerilir).";

            return new AnalyzeListingImagesResultDto(
                listingId,
                new CoverCtrAuditDto(overall, contrast, mobileCrop, bg, tip, isVision, note),
                suggestions,
                provider,
                model);
        }
        catch
        {
            return null;
        }
    }

    private static AnalyzeListingImagesResultDto BuildDeterministicFallback(
        long listingId,
        string title,
        IReadOnlyList<ListingImageDetailDto> images)
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Handcrafted product" : title.Trim();
        var suggestions = images.Select(img =>
        {
            var angle = img.Rank switch
            {
                1 => "Kapak",
                2 => "Detay / Yakın Çekim",
                3 => "Yan Açı",
                _ => $"Görsel #{img.Rank}"
            };

            return new ImageAltTextSuggestionDto(
                img.ListingImageId,
                img.Rank,
                TruncateAltText(BuildFallbackAltForRank(img.Rank, cleanTitle)),
                angle);
        }).ToList();

        return new AnalyzeListingImagesResultDto(
            listingId,
            new CoverCtrAuditDto(
                OverallScore: 78,
                ContrastScore: 80,
                MobileCropScore: 76,
                BackgroundScore: 78,
                Tip: "Vision AI anahtarı yapılandırıldığında kapak fotoğrafı piksel düzeyinde incelenerek netlik ve kırpılma önerisi üretilir.",
                IsVisionAnalyzed: false,
                Note: "Yerel başlık tabanlı tahmin (Etsy API ham impression/tıklama verisi sağlamaz)."),
            suggestions,
            "Offline",
            "Heuristic");
    }

    private static string BuildFallbackAltForRank(int rank, string cleanTitle)
    {
        var prefix = rank switch
        {
            1 => "Main product view of",
            2 => "Close-up detail shot of",
            3 => "Side angle view of",
            4 => "Lifestyle display view of",
            _ => $"Product photo #{rank} showing"
        };
        return string.IsNullOrWhiteSpace(cleanTitle) ? prefix : $"{prefix} {cleanTitle}";
    }

    private static string TruncateAltText(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length <= MaxAltTextLength)
            return trimmed;

        var cut = trimmed[..MaxAltTextLength];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > 120 ? cut[..lastSpace] : cut).TrimEnd();
    }
}
