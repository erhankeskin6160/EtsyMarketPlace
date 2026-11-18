using EtsyMarketPlace.Domain.Subscriptions;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdateSubscriptionPlan;

public sealed class AdminUpdateSubscriptionPlanValidator : AbstractValidator<AdminUpdateSubscriptionPlanCommand>
{
    public AdminUpdateSubscriptionPlanValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty()
            .Must(SubscriptionPlanCode.IsValid)
            .WithMessage("Geçersiz paket kimliği.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Paket güncelleme bilgileri boş olamaz.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Paket adı gereklidir ve en fazla 100 karakter olabilir.");

            RuleFor(x => x.Request.MonthlyPriceUsd)
                .GreaterThanOrEqualTo(0m)
                .WithMessage("Aylık USD fiyatı negatif olamaz.");

            RuleFor(x => x.Request.YearlyPriceUsd)
                .GreaterThanOrEqualTo(0m)
                .WithMessage("Yıllık USD fiyatı negatif olamaz.");

            RuleFor(x => x.Request.MonthlyPriceTry)
                .GreaterThanOrEqualTo(0m)
                .WithMessage("Aylık TRY fiyatı negatif olamaz.");

            RuleFor(x => x.Request.YearlyPriceTry)
                .GreaterThanOrEqualTo(0m)
                .WithMessage("Yıllık TRY fiyatı negatif olamaz.");

            RuleFor(x => x.Request.MaxShops)
                .GreaterThan(0)
                .WithMessage("Maksimum mağaza sayısı en az 1 olmalıdır.");

            RuleFor(x => x.Request.MonthlyAiTokenQuota)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Aylık AI token kotası negatif olamaz.");

            RuleFor(x => x.Request.MaxBatchQueueSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Toplu kuyruk limiti negatif olamaz.");
        });
    }
}
