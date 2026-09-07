namespace EtsyMarketPlace.Infrastructure.ExternalMarketplaces;

public sealed class EbayApiSettings
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string MarketplaceId { get; set; } = "EBAY_US";
    public int Limit { get; set; } = 20;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed record EbayApiProduct(
    string Title,
    string Url,
    string ImageUrl,
    string SellerName,
    string Category,
    decimal Price,
    string Currency,
    int DemandSignal,
    int ShopSignal,
    IReadOnlyList<string> Tags);

public interface IEbayApiClient
{
    Task<IReadOnlyList<EbayApiProduct>> SearchAsync(
        EbayApiSettings settings,
        string query,
        CancellationToken cancellationToken = default);
}
