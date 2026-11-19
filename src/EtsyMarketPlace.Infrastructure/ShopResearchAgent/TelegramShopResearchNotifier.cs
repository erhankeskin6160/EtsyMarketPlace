namespace EtsyMarketPlace.Infrastructure.ShopResearchAgent;

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches concise scheduled research summaries to Telegram (when configured) and structured server logs.
/// </summary>
public sealed class TelegramShopResearchNotifier : IShopResearchNotifier
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramShopResearchNotifier> _logger;

    public TelegramShopResearchNotifier(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramShopResearchNotifier> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyRunCompletedAsync(ResearchRun run, CancellationToken ct = default)
    {
        var top = run.Candidates.Take(3).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"🤖 *Mağaza Ürün Araştırma Ajanı ({run.TriggerType})*");
        sb.AppendLine($"🏪 Shop #{run.ShopId} | Bulunan Aday: *{run.Candidates.Count}*");
        foreach (var c in top)
        {
            sb.AppendLine($"• *{c.Title}* — Fırsat: {c.OpportunityScore}/100 | Talep: {c.DemandScore}/100 | Net: ${c.EstimatedNetProfitUsd:F2} (%{c.EstimatedNetMarginPct:F1})");
        }

        var message = sb.ToString();
        _logger.LogInformation("Scheduled ShopResearchAgent summary for ShopId={ShopId}: {Summary}", run.ShopId, run.Summary);

        var botToken = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(
                $"https://api.telegram.org/bot{botToken}/sendMessage",
                content,
                ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram research notification failed with HTTP {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram research notification could not be delivered.");
        }
    }
}
