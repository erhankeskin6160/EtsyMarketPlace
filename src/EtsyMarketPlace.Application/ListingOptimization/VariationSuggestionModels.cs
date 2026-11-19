using System.Collections.Generic;

namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed record SuggestListingVariationsRequestDto(
    string? Title,
    string? Description = null,
    string? Category = null,
    List<string>? Tags = null,
    List<string>? Materials = null,
    string ListingType = "physical",
    decimal PriceUsd = 0m,
    string? CurrentVarType1 = null,
    string? CurrentVarValues1 = null,
    string? CurrentVarType2 = null,
    string? CurrentVarValues2 = null,
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null,
    bool StrictNeverOffline = false
);

public sealed class SuggestedVariationGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class SuggestedVariationOfferingDto
{
    public string Key { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class SuggestListingVariationsResultDto
{
    public bool UsedAi { get; set; }
    public string Provider { get; set; } = "Akıllı Ürün Analiz Motoru";
    public string Model { get; set; } = string.Empty;
    public string AnalysisSummary { get; set; } = string.Empty;
    public string? WarningMessage { get; set; }
    public List<SuggestedVariationGroupDto> SuggestedVariations { get; set; } = [];
    public List<SuggestedVariationOfferingDto> SuggestedOfferings { get; set; } = [];
}
