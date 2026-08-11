namespace EtsyMarketPlace.Application.ExternalMarketplaces;

public interface IExternalMarketplaceProvider
{
    string Name { get; }

    IReadOnlyList<MarketplaceProduct> Search(ExternalMarketplaceSearchContext context);
}
