namespace EtsyMarketPlace.Domain.ShopVault.ValueObjects;

public sealed class MigrationMappingProfile
{
    public long TargetShopId { get; set; }
    public string TargetShopName { get; set; } = string.Empty;

    public Dictionary<long, long> ShippingProfileMap { get; set; } = [];
    public Dictionary<long, long> ReturnPolicyMap { get; set; } = [];
    public Dictionary<long, long> TaxonomyOverrideMap { get; set; } = [];

    public long DefaultTargetShippingProfileId { get; set; }
    public long DefaultTargetReturnPolicyId { get; set; }
}
