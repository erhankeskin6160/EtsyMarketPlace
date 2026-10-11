namespace EtsyMarketPlace.Application.Shipping;

using System.Threading;
using System.Threading.Tasks;

public interface IEtsyLogisticsApiClient
{
    Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken cancellationToken = default);
}
