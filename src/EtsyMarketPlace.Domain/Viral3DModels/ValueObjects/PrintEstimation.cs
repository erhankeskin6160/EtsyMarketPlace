namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using System;

public record PrintEstimation
{
    public int EstimatedPrintTimeMinutes { get; init; } = 180;
    public double FilamentGrams { get; init; } = 80.0;
    public bool HasMultiColorProfile { get; init; } = false;
    public int ColorCount { get; init; } = 1;
    public string RecommendedLayerHeight { get; init; } = "0.20mm";
    public double EstimatedMaterialCostUsd => Math.Round(FilamentGrams * 0.025, 2); // ~$25/kg standard PLA filament

    public string FormattedPrintTime
    {
        get
        {
            var hours = EstimatedPrintTimeMinutes / 60;
            var mins = EstimatedPrintTimeMinutes % 60;
            return hours > 0 ? $"{hours}s {mins}dk" : $"{mins} dk";
        }
    }
}
