namespace EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;

using System.Collections.Generic;
using System.Threading;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;

/// <summary>
/// Publishes and streams real-time SSE research funnel progress events scoped per user and shop.
/// </summary>
public interface IResearchProgressHub
{
    void Publish(string ownerUserId, string shopId, ResearchProgressEvent progressEvent);
    IAsyncEnumerable<ResearchProgressEvent> SubscribeAsync(string ownerUserId, string shopId, CancellationToken ct);
}
