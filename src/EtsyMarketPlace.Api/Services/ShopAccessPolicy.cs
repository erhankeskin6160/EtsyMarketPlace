namespace EtsyMarketPlace.Api.Services;

public sealed class ShopAccessPolicy(IConfiguration configuration)
{
    public bool IsAllowed(string shopId)
    {
        var allowed = configuration.GetSection("Security:AllowedShopIds").Get<string[]>();
        return allowed is null || allowed.Length == 0 || allowed.Contains(shopId, StringComparer.Ordinal);
    }
}
