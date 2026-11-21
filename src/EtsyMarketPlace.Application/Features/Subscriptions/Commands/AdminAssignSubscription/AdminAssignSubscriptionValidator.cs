using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Domain.Subscriptions;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminAssignSubscription;

public sealed class AdminAssignSubscriptionValidator : AbstractValidator<AdminAssignSubscriptionCommand>
{
    private static readonly string[] ValidStatuses =
    [
        "Trialing",
        "Active",
        "PastDue",
        "Cancelled",
        "Expired"
    ];

    public AdminAssignSubscriptionValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Hedef kullanıcı kimliği gereklidir.",
                "Target user ID is required."));

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Abonelik atama isteği boş olamaz.",
                "Subscription assignment request cannot be empty."));

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .Must(SubscriptionPlanCode.IsValid)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçersiz abonelik paketi kodu (starter, growth, pro, enterprise).",
                    "Invalid subscription plan code (starter, growth, pro, enterprise)."));

            RuleFor(x => x.Request.Status)
                .Must(s => string.IsNullOrWhiteSpace(s) || ValidStatuses.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçersiz abonelik durumu.",
                    "Invalid subscription status."));

            RuleFor(x => x.Request.DurationDays)
                .GreaterThan(0)
                .When(x => x.Request.DurationDays.HasValue)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Süre (gün) 0'dan büyük olmalıdır.",
                    "Duration (days) must be greater than 0."));

            RuleFor(x => x.Request.CustomMaxShopsOverride)
                .GreaterThan(0)
                .When(x => x.Request.CustomMaxShopsOverride.HasValue)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Özel mağaza limiti 0'dan büyük olmalıdır.",
                    "Custom shop limit must be greater than 0."));

            RuleFor(x => x.Request.CustomAiTokenQuotaOverride)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Request.CustomAiTokenQuotaOverride.HasValue)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Özel AI token kotası negatif olamaz.",
                    "Custom AI token quota cannot be negative."));
        });
    }
}
