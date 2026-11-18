namespace EtsyMarketPlace.Infrastructure.ShopResearchAgent;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background scheduler that runs personalized store research on Daily / Weekly / Monthly cadences.
/// </summary>
public sealed class ShopResearchSchedulerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShopResearchSchedulerBackgroundService> _logger;

    public ShopResearchSchedulerBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ShopResearchSchedulerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait briefly after startup before first check
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShopResearchSchedulerBackgroundService döngüsünde beklenmeyen hata.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IShopResearchRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ShopResearchAgentOrchestrator>();

        var profiles = await repo.GetScheduledProfilesAsync(ct);
        var utcNow = DateTime.UtcNow;

        foreach (var profile in profiles)
        {
            if (ct.IsCancellationRequested) break;
            if (!profile.IsScheduleDue(utcNow)) continue;

            var trigger = profile.ScheduleCadence switch
            {
                ResearchScheduleCadence.Daily => ResearchTriggerType.Daily,
                ResearchScheduleCadence.Weekly => ResearchTriggerType.Weekly,
                ResearchScheduleCadence.Monthly => ResearchTriggerType.Monthly,
                _ => ResearchTriggerType.Manual
            };

            _logger.LogInformation(
                "Zamanlanmış mağaza araştırması başlatılıyor. ShopId={ShopId}, Cadence={Cadence}",
                profile.ShopId, profile.ScheduleCadence);

            await orchestrator.RunResearchAsync(profile.OwnerUserId, profile.ShopId, trigger, null, ct);

            // Cooldown between shops to protect Etsy API rate limits
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
