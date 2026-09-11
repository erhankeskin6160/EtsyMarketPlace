namespace EtsyMarketPlace.Domain.ShopVault.ValueObjects;

public sealed class AntiBanSettings
{
    public bool StripExifMetadata { get; set; } = true;
    public bool PermutateImageHash { get; set; } = true;
    public bool RewriteTitleWithAi { get; set; } = true;
    public bool RewriteDescriptionWithAi { get; set; } = true;
    public decimal PriceAdjustmentPercent { get; set; } = 0.0m;
    public int ThrottleDelaySeconds { get; set; } = 4;
    public bool CreateAsDraftFirst { get; set; } = true;
    public string SkuPrefix { get; set; } = "NEW_";

    public AntiBanSettings() { }

    public static AntiBanSettings Default => new();
    public static AntiBanSettings HighSecurity => new()
    {
        StripExifMetadata = true,
        PermutateImageHash = true,
        RewriteTitleWithAi = true,
        RewriteDescriptionWithAi = true,
        PriceAdjustmentPercent = 0.0m,
        ThrottleDelaySeconds = 6,
        CreateAsDraftFirst = true
    };
    public static AntiBanSettings MirrorClone => new()
    {
        StripExifMetadata = true,
        PermutateImageHash = false,
        RewriteTitleWithAi = false,
        RewriteDescriptionWithAi = false,
        PriceAdjustmentPercent = 0.0m,
        ThrottleDelaySeconds = 3,
        CreateAsDraftFirst = true
    };
}
