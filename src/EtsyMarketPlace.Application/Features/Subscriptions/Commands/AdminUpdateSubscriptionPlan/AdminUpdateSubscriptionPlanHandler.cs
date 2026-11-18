using System.Net;
using System.Text.Json;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdateSubscriptionPlan;

public sealed class AdminUpdateSubscriptionPlanHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository)
    : IRequestHandler<AdminUpdateSubscriptionPlanCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(AdminUpdateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var normalizedId = SubscriptionPlanCode.Normalize(request.PlanId);
        var plan = await SubscriptionLifecycleHelper.ResolvePlanAsync(
            subscriptionRepository,
            normalizedId,
            cancellationToken);

        if (plan is null)
        {
            return ServiceResponse.CreateNotFound("Güncellenecek abonelik paketi bulunamadı.");
        }

        var req = request.Request;
        plan.Name = req.Name.Trim();
        plan.Tagline = req.Tagline?.Trim() ?? plan.Tagline;
        plan.BadgeText = req.BadgeText?.Trim() ?? plan.BadgeText;
        plan.MonthlyPriceUsd = req.MonthlyPriceUsd;
        plan.YearlyPriceUsd = req.YearlyPriceUsd;
        plan.MonthlyPriceTry = req.MonthlyPriceTry;
        plan.YearlyPriceTry = req.YearlyPriceTry;
        plan.MaxShops = req.MaxShops;
        plan.MonthlyAiTokenQuota = req.MonthlyAiTokenQuota;
        plan.MaxMonthlyAiListings = req.MaxMonthlyAiListings;
        plan.MaxBatchQueueSize = req.MaxBatchQueueSize;
        if (req.AllowedFeatures is not null)
        {
            var normalizedFeatures = req.AllowedFeatures
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(SubscriptionFeatureKeys.NormalizeFeatureKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            plan.AllowedFeaturesJson = JsonSerializer.Serialize(normalizedFeatures);
        }
        plan.IsPopular = req.IsPopular;
        plan.IsActive = req.IsActive;
        plan.UpdatedAtUtc = DateTime.UtcNow;

        var saved = await subscriptionRepository.UpsertPlanAsync(plan, cancellationToken);

        await SubscriptionLifecycleHelper.TryAddAuditLogAsync(
            userRepository,
            request.AdminId ?? "admin",
            request.AdminName ?? "Admin",
            "AdminUpdateSubscriptionPlan",
            $"Paket güncellendi: {saved.Id} (${saved.MonthlyPriceUsd}/ay, {saved.MaxShops} mağaza, {saved.MonthlyAiTokenQuota:N0} token)",
            request.RemoteIpAddress,
            cancellationToken);

        return ServiceResponse.CreateSuccess($"{saved.Name} paketi güncellendi.", saved.ToDto());
    }
}
