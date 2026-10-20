namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IEtsyLogisticsService
{
    Task<IReadOnlyList<EtsyShippingProfileDto>> GetShippingProfilesAsync(
        string shopId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EtsyReadinessStateDto>> GetReadinessStatesAsync(
        string shopId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> GetSellerTaxonomyNodesAsync(
        string shopId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> SearchSellerTaxonomyNodesAsync(
        string shopId,
        string query,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
