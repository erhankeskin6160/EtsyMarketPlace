namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public interface IModelSearchExpander
{
    Task<ModelSearchQueryExpansion> ExpandQueryAsync(
        string userQuery,
        ShopNicheProfile? shopProfile = null,
        CancellationToken ct = default);
}
