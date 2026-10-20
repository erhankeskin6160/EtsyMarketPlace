namespace EtsyMarketPlace.Application.Taxonomy;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Taxonomy;

public interface IEtsyTaxonomyRepository
{
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyTaxonomyNode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyTaxonomyNode>> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
    Task<EtsyTaxonomyNode?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<EtsyTaxonomyNode> nodes, CancellationToken cancellationToken = default);
}
