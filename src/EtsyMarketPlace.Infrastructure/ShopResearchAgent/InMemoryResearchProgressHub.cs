namespace EtsyMarketPlace.Infrastructure.ShopResearchAgent;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;

/// <summary>
/// Thread-safe in-memory pub/sub channel hub for real-time SSE research funnel progress streaming.
/// </summary>
public sealed class InMemoryResearchProgressHub : IResearchProgressHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<ResearchProgressEvent>>> _subscriptions =
        new(StringComparer.Ordinal);

    public void Publish(string ownerUserId, string shopId, ResearchProgressEvent progressEvent)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId) || string.IsNullOrWhiteSpace(shopId))
            return;

        var key = BuildKey(ownerUserId, shopId);
        if (!_subscriptions.TryGetValue(key, out var listeners))
            return;

        foreach (var ch in listeners.Values)
        {
            ch.Writer.TryWrite(progressEvent);
        }
    }

    public async IAsyncEnumerable<ResearchProgressEvent> SubscribeAsync(
        string ownerUserId,
        string shopId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var key = BuildKey(ownerUserId, shopId);
        var listenerId = Guid.NewGuid();
        var channel = Channel.CreateBounded<ResearchProgressEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var listeners = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Channel<ResearchProgressEvent>>());
        listeners[listenerId] = channel;

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            if (_subscriptions.TryGetValue(key, out var map))
            {
                map.TryRemove(listenerId, out _);
                if (map.IsEmpty)
                {
                    _subscriptions.TryRemove(key, out _);
                }
            }
            channel.Writer.TryComplete();
        }
    }

    private static string BuildKey(string ownerUserId, string shopId) => $"{ownerUserId.Trim()}|{shopId.Trim()}";
}
