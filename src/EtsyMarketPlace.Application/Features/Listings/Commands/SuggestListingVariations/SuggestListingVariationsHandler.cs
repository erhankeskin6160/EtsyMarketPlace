using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Listings.Commands.SuggestListingVariations;

public sealed class SuggestListingVariationsHandler(IGeminiListingApiClient aiClient)
    : IRequestHandler<SuggestListingVariationsCommand, ServiceResponse>
{
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    public async Task<ServiceResponse> Handle(SuggestListingVariationsCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var dto = request.Request;
        if (dto is null || (string.IsNullOrWhiteSpace(dto.Title) && string.IsNullOrWhiteSpace(dto.Description)))
        {
            return ServiceResponse.CreateError("AI varyasyon önerisi için en az ürün başlığı veya açıklaması gereklidir.", HttpStatusCode.BadRequest);
        }

        var provider = string.IsNullOrWhiteSpace(dto.Provider) ? "Gemini" : dto.Provider.Trim();
        var model = ResolveDefaultModel(provider, dto.Model);
        var apiKey = dto.ApiKey?.Trim() ?? string.Empty;
        var isOfflineProvider = string.Equals(provider, "Offline", StringComparison.OrdinalIgnoreCase);

        if (isOfflineProvider || string.IsNullOrWhiteSpace(apiKey))
        {
            if (dto.StrictNeverOffline && !isOfflineProvider)
            {
                return ServiceResponse.CreateError(
                    $"Canlı AI Zorunlu aktif fakat {provider} için API anahtarı bulunamadı. Lütfen AI Ayarlarından {provider} API anahtarınızı kaydedin.",
                    HttpStatusCode.BadRequest);
            }

            return BuildHeuristicSuccess(dto, isFallbackAfterError: false, errorDetail: null);
        }

        string? llmError = null;
        try
        {
            const string systemPrompt = """
                You are an elite Etsy Merchandising & Conversion Specialist.
                Your task is to analyze the seller's specific product and recommend 1 or 2 Etsy-compliant variation groups tailored strictly to THIS product.

                CRITICAL ANALYSIS RULES:
                1. Deeply read `title`, `description`, `tags`, `materials`, `category`, and `listing_type` (`physical` vs `digital`).
                2. First extract and preserve any measurements, dimensions, sizes, colors, finishes, styles, pack counts, capacities, scents, or personalization cues ALREADY stated in the seller's title or description.
                3. Recommend 1 or 2 Etsy variation groups that genuinely make sense for THIS specific product and increase buyer conversion or average order value. Do NOT force 2 groups if 1 group is cleaner. NEVER invent generic unrelated presets.
                4. Keep buyer-facing group `name` and `values` in natural, concise English (2 to 5 values per group, prefer <= 20 characters per option value, total combinations <= 15).
                5. Provide `price_multipliers` aligned 1-to-1 with `values` (e.g. [1.0, 1.25]) so larger sizes, multi-packs, or premium finishes scale realistically from `base_price_usd`.
                6. If `current_variations` is provided (seller wants fresh alternatives), propose a meaningfully DIFFERENT variation angle or option set tailored to the product instead of repeating the exact same groups/values.
                7. Write `analysis_summary` in concise Turkish (1-2 sentences) explicitly citing what clues from the title, description, or category shaped your suggestion, and write a short Turkish `reason` for each group.
                8. Output ONLY strict JSON with no markdown code fences:
                {
                  "analysis_summary": "<Turkish 1-2 sentence explanation grounded in title & description>",
                  "variations": [
                    {
                      "name": "<English Etsy variation group name>",
                      "reason": "<Short Turkish reason>",
                      "values": ["<English option 1>", "<English option 2>"],
                      "price_multipliers": [1.0, 1.2]
                    }
                  ]
                }
                """;

            var currentVariations = new List<object>();
            if (!string.IsNullOrWhiteSpace(dto.CurrentVarType1) || !string.IsNullOrWhiteSpace(dto.CurrentVarValues1))
            {
                currentVariations.Add(new { name = dto.CurrentVarType1 ?? "", values = dto.CurrentVarValues1 ?? "" });
            }
            if (!string.IsNullOrWhiteSpace(dto.CurrentVarType2) || !string.IsNullOrWhiteSpace(dto.CurrentVarValues2))
            {
                currentVariations.Add(new { name = dto.CurrentVarType2 ?? "", values = dto.CurrentVarValues2 ?? "" });
            }

            var promptModel = new
            {
                title = dto.Title?.Trim() ?? string.Empty,
                description = dto.Description?.Trim() ?? string.Empty,
                category = dto.Category?.Trim() ?? string.Empty,
                tags = dto.Tags ?? [],
                materials = dto.Materials ?? [],
                listing_type = string.IsNullOrWhiteSpace(dto.ListingType) ? "physical" : dto.ListingType.Trim(),
                base_price_usd = dto.PriceUsd,
                current_variations = currentVariations
            };

            var userPrompt = JsonSerializer.Serialize(promptModel, PromptJsonOptions);
            var (success, content, errorMessage) = await aiClient.GenerateOptimizationJsonAsync(
                systemPrompt, userPrompt, model, apiKey, cancellationToken);

            if (success && !string.IsNullOrWhiteSpace(content))
            {
                var parsed = ParseLlmResponse(content);
                if (parsed.Groups.Count > 0)
                {
                    var offerings = ProductAwareVariationHeuristic.BuildOfferings(
                        parsed.Groups,
                        dto.PriceUsd,
                        parsed.Multipliers);

                    var summary = string.IsNullOrWhiteSpace(parsed.AnalysisSummary)
                        ? $"{provider} ({model}) ürün başlığı ve açıklamasını analiz ederek {parsed.Groups.Count} özel varyasyon grubu önerdi."
                        : parsed.AnalysisSummary;

                    return ServiceResponse.CreateSuccess(
                        "Ürüne özel AI varyasyon önerileri oluşturuldu.",
                        new SuggestListingVariationsResultDto
                        {
                            UsedAi = true,
                            Provider = provider,
                            Model = model,
                            AnalysisSummary = summary,
                            SuggestedVariations = parsed.Groups,
                            SuggestedOfferings = offerings
                        });
                }

                llmError = "Model geçerli varyasyon grubu döndürmedi.";
            }
            else
            {
                llmError = errorMessage ?? "Model yanıt vermedi.";
            }
        }
        catch (Exception ex)
        {
            llmError = ex.Message;
        }

        if (dto.StrictNeverOffline)
        {
            return ServiceResponse.CreateError(
                $"Canlı AI varyasyon önerisi alınamadı ({provider}): {llmError ?? "Geçersiz model yanıtı"}. 'Canlı AI Zorunlu' aktif olduğu için çevrimdışı motora geçilmedi.",
                HttpStatusCode.BadGateway);
        }

        return BuildHeuristicSuccess(dto, isFallbackAfterError: true, errorDetail: llmError, attemptedProvider: provider);
    }

    private static ServiceResponse BuildHeuristicSuccess(
        SuggestListingVariationsRequestDto dto,
        bool isFallbackAfterError,
        string? errorDetail,
        string? attemptedProvider = null)
    {
        var (groups, summary) = ProductAwareVariationHeuristic.Suggest(
            dto.Title,
            dto.Description,
            dto.Category,
            dto.Tags,
            dto.Materials,
            dto.ListingType);

        var offerings = ProductAwareVariationHeuristic.BuildOfferings(groups, dto.PriceUsd);
        var warning = isFallbackAfterError
            ? $"Canlı {attemptedProvider ?? "AI"} yanıt veremedi ({errorDetail ?? "format hatası"}); başlık ve açıklama bazlı akıllı ürün motoru kullanıldı."
            : null;

        return ServiceResponse.CreateSuccess(
            "Ürün özelliklerine göre varyasyon önerileri oluşturuldu.",
            new SuggestListingVariationsResultDto
            {
                UsedAi = false,
                Provider = "Akıllı Ürün Analiz Motoru",
                Model = "product-aware-heuristic",
                AnalysisSummary = summary,
                WarningMessage = warning,
                SuggestedVariations = groups,
                SuggestedOfferings = offerings
            });
    }

    private static (string AnalysisSummary, List<SuggestedVariationGroupDto> Groups, List<IReadOnlyList<decimal>> Multipliers) ParseLlmResponse(string rawContent)
    {
        var clean = rawContent.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean[7..];
        if (clean.StartsWith("```")) clean = clean[3..];
        if (clean.EndsWith("```")) clean = clean[..^3];
        clean = clean.Trim();

        var firstBrace = clean.IndexOf('{');
        var lastBrace = clean.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            clean = clean[firstBrace..(lastBrace + 1)];
        }

        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;

        var analysisSummary = root.TryGetProperty("analysis_summary", out var asEl) && asEl.ValueKind == JsonValueKind.String
            ? (asEl.GetString() ?? string.Empty).Trim()
            : string.Empty;

        var groups = new List<SuggestedVariationGroupDto>();
        var multipliers = new List<IReadOnlyList<decimal>>();
        var seenGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("variations", out var varArr) && varArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var vObj in varArr.EnumerateArray())
            {
                if (groups.Count >= 2) break;

                var name = vObj.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                    ? (nEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || !seenGroupNames.Add(name))
                    continue;

                var reason = vObj.TryGetProperty("reason", out var rEl) && rEl.ValueKind == JsonValueKind.String
                    ? (rEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var rawMultipliers = new List<decimal>();
                if (vObj.TryGetProperty("price_multipliers", out var pmArr) && pmArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var mEl in pmArr.EnumerateArray())
                    {
                        if (mEl.ValueKind == JsonValueKind.Number && mEl.TryGetDecimal(out var dec))
                            rawMultipliers.Add(dec);
                        else if (mEl.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(mEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDec))
                            rawMultipliers.Add(parsedDec);
                    }
                }

                var values = new List<string>();
                var groupMults = new List<decimal>();
                var seenVals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (vObj.TryGetProperty("values", out var valArr) && valArr.ValueKind == JsonValueKind.Array)
                {
                    int idx = 0;
                    foreach (var valEl in valArr.EnumerateArray())
                    {
                        var valStr = valEl.ValueKind == JsonValueKind.String
                            ? SanitizeOptionValue(valEl.GetString() ?? string.Empty)
                            : string.Empty;

                        if (!string.IsNullOrWhiteSpace(valStr) && seenVals.Add(valStr) && values.Count < 5)
                        {
                            values.Add(valStr);
                            groupMults.Add(idx < rawMultipliers.Count ? rawMultipliers[idx] : 1.0m);
                        }
                        idx++;
                    }
                }

                if (values.Count > 0)
                {
                    groups.Add(new SuggestedVariationGroupDto
                    {
                        Name = name,
                        Values = values,
                        Reason = reason
                    });
                    multipliers.Add(groupMults);
                }
            }
        }

        return (analysisSummary, groups, multipliers);
    }

    private static string SanitizeOptionValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var sanitized = Regex.Replace(raw, @"\s*/\s*", " - ");
        sanitized = Regex.Replace(sanitized, @"\s*,\s*", " & ");
        sanitized = MultiWhitespaceRegex.Replace(sanitized, " ").Trim();
        return sanitized;
    }

    private static string ResolveDefaultModel(string provider, string? requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
            return requestedModel.Trim();

        return provider.Trim().ToLowerInvariant() switch
        {
            "deepseek" => "deepseek-chat",
            "openai" => "gpt-4o-mini",
            "claude" => "claude-3-7-sonnet-20250219",
            "grok" => "grok-3",
            _ => "gemini-2.5-flash"
        };
    }
}
