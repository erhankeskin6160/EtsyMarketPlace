namespace EtsyMarketPlace.Application.AiUsage;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAiBalanceService
{
    Task<IReadOnlyList<AiProviderBalanceInfo>> GetProviderBalancesAsync(string shopId, decimal exchangeRate = 40.0m, CancellationToken ct = default);
}
