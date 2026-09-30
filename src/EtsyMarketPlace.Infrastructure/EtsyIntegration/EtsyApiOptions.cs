namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyApiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SharedSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = ["shops_r", "transactions_r", "billing_r"];
    public string ApiBaseUrl { get; set; } = "https://api.etsy.com/v3/";
    public string AuthorizationBaseUrl { get; set; } = "https://www.etsy.com/oauth/connect";
    public string TokenUrl { get; set; } = "https://api.etsy.com/v3/public/oauth/token";
}