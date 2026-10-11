namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;

public record ShopListingItemDto(
    int Rank,
    string ListingId,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Materials,
    decimal Price,
    decimal PriceAmount,
    string Currency,
    string CurrencyCode,
    int Quantity,
    int Views,
    int Favorites,
    int NumFavorers,
    IReadOnlyList<string> Images,
    string Thumbnail,
    string PrimaryImageUrl,
    int SeoScore,
    int? AiScore,
    string Status,
    bool IsAiAudited,
    string? ResultJson,
    string SeoNeeds,
    string SeoStrengths,
    IReadOnlyList<string> StructuralNeeds,
    IReadOnlyList<string> RiskWarnings,
    bool HasSavedAudit,
    object? SavedAudit);

public record UpdateListingRequestDto(
    string? ShopId,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string>? Materials = null);

public record ListingUpdateResultDto(
    bool Success,
    string? ListingId,
    string Message,
    string? State = null);

public record OptimizeListingCommandDto(
    string? ShopId,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Tags,
    string? TargetKeyword,
    string? DescriptionStyle,
    string? FocusKeywords,
    string? TargetBuyerPersona,
    string? Tone,
    string? Model,
    string? ApiKey,
    string? Provider);

public record OptimizeListingResultDto(
    bool Success,
    string ListingId,
    int CurrentSeoScore,
    int OptimizedSeoScore,
    int SeoScoreBefore,
    int SeoScoreAfter,
    string OptimizedTitle,
    string SuggestedTitle,
    IReadOnlyList<string> TitleSuggestions,
    IReadOnlyList<string> OptimizedTags,
    IReadOnlyList<string> TagSuggestions,
    IReadOnlyList<string> MaterialSuggestions,
    string OptimizedDescription,
    string DescriptionDraft,
    string Critique,
    string SeoCritique,
    IReadOnlyList<string> MissingTerms,
    IReadOnlyList<string> RiskWarnings,
    IReadOnlyList<string> Checklist,
    string Status,
    string AiModel,
    string Provider,
    string Model,
    string Message);
