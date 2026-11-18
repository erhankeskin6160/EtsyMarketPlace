using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Application.Subscriptions;

public sealed record SubscriptionPlanDto(
    string Id,
    string Name,
    string Tagline,
    string BadgeText,
    string AccentColor,
    decimal MonthlyPriceUsd,
    decimal YearlyPriceUsd,
    decimal MonthlyPriceTry,
    decimal YearlyPriceTry,
    int MaxShops,
    int MonthlyAiTokenQuota,
    int MaxMonthlyAiListings,
    int MaxBatchQueueSize,
    IReadOnlyList<string> AllowedFeatures,
    bool IsPopular,
    bool IsActive,
    int DisplayOrder);

public sealed record UserSubscriptionSummaryDto(
    string SubscriptionId,
    string UserId,
    string Username,
    string Role,
    string PlanId,
    string PlanName,
    string BadgeText,
    string AccentColor,
    string Status,
    string BillingCycle,
    string StartDateUtc,
    string CurrentPeriodStartUtc,
    string CurrentPeriodEndUtc,
    int DaysRemaining,
    bool AutoRenew,
    bool IsAdminExempt,
    int MaxShops,
    int AssignedShopsCount,
    int RemainingShopSlots,
    int MonthlyAiTokenQuota,
    int UsedAiTokens,
    int RemainingAiTokens,
    double AiUsagePercentage,
    int MaxMonthlyAiListings,
    int MaxBatchQueueSize,
    IReadOnlyList<string> AllowedFeatures);

public sealed record SubscriptionTransactionDto(
    long Id,
    string SessionId,
    string UserId,
    string Username,
    string PlanId,
    string PlanName,
    string BillingCycle,
    decimal AmountUsd,
    decimal AmountTry,
    string Currency,
    decimal ExchangeRate,
    string Status,
    string PaymentProvider,
    string? CardMaskedNumber,
    string? CardHolderName,
    string InvoiceNumber,
    string CreatedAtUtc,
    string? PaidAtUtc);

public sealed record CreateCheckoutSessionRequest(
    string PlanId,
    string BillingCycle = "Monthly",
    string Currency = "USD",
    string? ReturnBaseUrl = null);

public sealed record CheckoutSessionResponseDto(
    string SessionId,
    string CheckoutUrl,
    string Mode,
    string Provider,
    string PlanId,
    string PlanName,
    string BillingCycle,
    decimal AmountUsd,
    decimal AmountTry,
    string Currency,
    string InvoiceNumber,
    string ExpiresAtUtc);

public sealed record ConfirmVirtualPosPaymentRequest(
    string SessionId,
    string CardHolderName,
    string CardNumber,
    string ExpiryMonth,
    string ExpiryYear,
    string Cvc);

public sealed record ConfirmPaymentResponseDto(
    bool Success,
    string Message,
    SubscriptionTransactionDto Transaction,
    UserSubscriptionSummaryDto Subscription,
    UserDto? User = null,
    string? RefreshedToken = null);

public sealed record AdminAssignSubscriptionRequest(
    string PlanId,
    string BillingCycle = "Monthly",
    string Status = "Active",
    int? DurationDays = null,
    int? CustomMaxShopsOverride = null,
    int? CustomAiTokenQuotaOverride = null);

public sealed record AdminUpdateSubscriptionPlanRequest(
    string Name,
    string Tagline,
    string BadgeText,
    decimal MonthlyPriceUsd,
    decimal YearlyPriceUsd,
    decimal MonthlyPriceTry,
    decimal YearlyPriceTry,
    int MaxShops,
    int MonthlyAiTokenQuota,
    int MaxMonthlyAiListings,
    int MaxBatchQueueSize,
    List<string>? AllowedFeatures = null,
    bool IsPopular = false,
    bool IsActive = true);

public sealed record FeatureAccessCheckDto(
    bool Allowed,
    string FeatureKey,
    string NormalizedFeatureKey,
    string CurrentPlanId,
    string RequiredPlanId,
    string Reason);

public sealed record ShopLimitCheckDto(
    bool Allowed,
    int MaxShops,
    int CurrentShopsCount,
    int RemainingSlots,
    string Reason);

public sealed record AiQuotaCheckDto(
    bool Allowed,
    int MonthlyQuota,
    int UsedTokens,
    int RemainingTokens,
    string Reason);

public static class SubscriptionMappingExtensions
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan plan)
        => new(
            plan.Id,
            plan.Name,
            plan.Tagline,
            plan.BadgeText,
            plan.AccentColor,
            plan.MonthlyPriceUsd,
            plan.YearlyPriceUsd,
            plan.MonthlyPriceTry,
            plan.YearlyPriceTry,
            plan.MaxShops,
            plan.MonthlyAiTokenQuota,
            plan.MaxMonthlyAiListings,
            plan.MaxBatchQueueSize,
            SubscriptionFeatureKeys.ParseAllowedFeatures(plan.AllowedFeaturesJson),
            plan.IsPopular,
            plan.IsActive,
            plan.DisplayOrder);

    public static SubscriptionTransactionDto ToDto(this SubscriptionTransaction tx)
        => new(
            tx.Id,
            tx.SessionId,
            tx.UserId,
            tx.Username,
            tx.PlanId,
            tx.PlanName,
            tx.BillingCycle,
            tx.AmountUsd,
            tx.AmountTry,
            tx.Currency,
            tx.ExchangeRate,
            tx.Status,
            tx.PaymentProvider,
            tx.CardMaskedNumber,
            tx.CardHolderName,
            tx.InvoiceNumber,
            tx.CreatedAtUtc.ToString("O"),
            tx.PaidAtUtc?.ToString("O"));
}
