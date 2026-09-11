namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public interface IEtsyCompetitionChecker
{
    Task<int> CheckEtsyCompetitionCountAsync(string modelTitle, CancellationToken ct = default);
}
