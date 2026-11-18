using System.Collections.Generic;

namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed record VariationAgentInputRowDto(
    string Key,
    decimal CurrentPrice = 0m,
    int Quantity = 1,
    bool Active = true
);

public sealed record VariationPricingAgentRequestDto(
    string? ShopId,
    string Title,
    string? Description = null,
    string? Category = null,
    List<string>? Materials = null,
    List<string>? Tags = null,
    string? VariationType1 = null,
    string? VariationValues1 = null,
    bool EnableVariation2 = false,
    string? VariationType2 = null,
    string? VariationValues2 = null,
    List<VariationAgentInputRowDto>? ExistingVariations = null,
    string TargetCountryCode = "US",
    int BaseWeightGrams = 250,
    double LengthCm = 18.0,
    double WidthCm = 14.0,
    double HeightCm = 8.0,
    decimal ProductionCostUsd = 0m,
    decimal TargetNetMarginPercent = 30m,
    bool IncludeOffsiteAds = false,
    decimal UsdTryRate = 50.45m,
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null
);

public sealed class CompetitorPriceSampleDto
{
    public long ListingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int ShopSales { get; set; }
    public bool HasFreeShipping { get; set; }
    public int EstimatedMonthlySales { get; set; }
}

public sealed class VariationMarketBenchmarkDto
{
    public int CompetitorCount { get; set; }
    public decimal MinPriceUsd { get; set; }
    public decimal MedianPriceUsd { get; set; }
    public decimal AveragePriceUsd { get; set; }
    public decimal P75PriceUsd { get; set; }
    public decimal MaxPriceUsd { get; set; }
    public double FreeShippingRatioPct { get; set; }
    public bool UsedLiveMarketData { get; set; }
    public List<CompetitorPriceSampleDto> TopCompetitors { get; set; } = [];
}

public sealed class AgentCarrierQuoteSummaryDto
{
    public string CarrierKey { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public decimal PriceTry { get; set; }
    public string EstimatedDays { get; set; } = string.Empty;
    public bool IsLive { get; set; }
}

public sealed class VariationShippingBenchmarkDto
{
    public string TargetCountryCode { get; set; } = "US";
    public string SelectedCarrierKey { get; set; } = string.Empty;
    public string SelectedCarrierName { get; set; } = string.Empty;
    public string SelectedServiceName { get; set; } = string.Empty;
    public decimal BaseShippingCostUsd { get; set; }
    public decimal BaseShippingCostTry { get; set; }
    public double CalculatedDesi { get; set; }
    public double BillableWeightKg { get; set; }
    public string EstimatedDays { get; set; } = string.Empty;
    public string HsCodeUsed { get; set; } = "3926400000";
    public List<AgentCarrierQuoteSummaryDto> CarrierQuotes { get; set; } = [];
}

public sealed class VariationAgentStepDto
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
}

public sealed class PricedVariationRowDto
{
    public string Key { get; set; } = string.Empty;
    public decimal RecommendedPriceUsd { get; set; }
    public int EstimatedWeightGrams { get; set; }
    public decimal EstimatedShippingCostUsd { get; set; }
    public decimal EtsyFeesUsd { get; set; }
    public decimal ProductionCostUsd { get; set; }
    public decimal NetProfitUsd { get; set; }
    public decimal NetMarginPercent { get; set; }
    public decimal BreakEvenPriceUsd { get; set; }
    public string PricingTierLabel { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public sealed class VariationPricingAgentResultDto
{
    public bool UsedAi { get; set; }
    public string ProviderUsed { get; set; } = "Akıllı Fiyatlandırma Motoru";
    public string SearchKeywordUsed { get; set; } = string.Empty;
    public VariationMarketBenchmarkDto MarketBenchmark { get; set; } = new();
    public VariationShippingBenchmarkDto ShippingBenchmark { get; set; } = new();
    public decimal RecommendedBasePriceUsd { get; set; }
    public decimal RecommendedBaseShippingCostUsd { get; set; }
    public decimal AverageNetMarginPercent { get; set; }
    public string StrategySummary { get; set; } = string.Empty;
    public string SuggestedVarType1 { get; set; } = string.Empty;
    public string SuggestedVarValues1 { get; set; } = string.Empty;
    public bool SuggestedEnableVar2 { get; set; }
    public string SuggestedVarType2 { get; set; } = string.Empty;
    public string SuggestedVarValues2 { get; set; } = string.Empty;
    public List<VariationAgentStepDto> AgentSteps { get; set; } = [];
    public List<PricedVariationRowDto> PricedVariations { get; set; } = [];
}
