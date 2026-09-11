namespace EtsyMarketPlace.Application.Viral3DModels.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public interface IShopNicheAnalyzer
{
    Task<ShopNicheProfile> AnalyzeShopNicheAsync(
        string shopName,
        IEnumerable<ShopListingItem> listings,
        CancellationToken ct = default);
}
