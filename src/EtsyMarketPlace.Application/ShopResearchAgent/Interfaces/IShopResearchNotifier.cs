namespace EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.ShopResearchAgent;

/// <summary>
/// Sends concise notifications (e.g., Telegram) when a scheduled store research run discovers high-opportunity products.
/// </summary>
public interface IShopResearchNotifier
{
    Task NotifyRunCompletedAsync(ResearchRun run, CancellationToken ct = default);
}
