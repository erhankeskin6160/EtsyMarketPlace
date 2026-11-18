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
            .WithMessage("Hedef kullanıcı kimliği gereklidir.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Abonelik atama isteği boş olamaz.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .Must(SubscriptionPlanCode.IsValid)
                .WithMessage("Geçersiz abonelik paketi kodu (starter, growth, pro, enterprise).");

            RuleFor(x => x.Request.Status)
                .Must(s => string.IsNullOrWhiteSpace(s) || ValidStatuses.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
                .WithMessage("Geçersiz abonelik durumu.");

            RuleFor(x => x.Request.DurationDays)
                .GreaterThan(0)
                .When(x => x.Request.DurationDays.HasValue)
                .WithMessage("Süre (gün) 0'dan büyük olmalıdır.");

            RuleFor(x => x.Request.CustomMaxShopsOverride)
                .GreaterThan(0)
                .When(x => x.Request.CustomMaxShopsOverride.HasValue)
                .WithMessage("Özel mağaza limiti 0'dan büyük olmalıdır.");

            RuleFor(x => x.Request.CustomAiTokenQuotaOverride)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Request.CustomAiTokenQuotaOverride.HasValue)
                .WithMessage("Özel AI token kotası negatif olamaz.");
        });
    }
}
