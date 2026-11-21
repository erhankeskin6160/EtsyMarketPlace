using EtsyMarketPlace.Application.Common.Localization;
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
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz paket kimliği.",
                "Invalid plan ID."));

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Paket güncelleme bilgileri boş olamaz.",
                "Plan update details cannot be empty."));

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Paket adı gereklidir ve en fazla 100 karakter olabilir.",
                    "Plan name is required and can be at most 100 characters."));

            RuleFor(x => x.Request.MonthlyPriceUsd)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Aylık USD fiyatı negatif olamaz.",
                    "Monthly USD price cannot be negative."));

            RuleFor(x => x.Request.YearlyPriceUsd)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Yıllık USD fiyatı negatif olamaz.",
                    "Yearly USD price cannot be negative."));

            RuleFor(x => x.Request.MonthlyPriceTry)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Aylık TRY fiyatı negatif olamaz.",
                    "Monthly TRY price cannot be negative."));

            RuleFor(x => x.Request.YearlyPriceTry)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Yıllık TRY fiyatı negatif olamaz.",
                    "Yearly TRY price cannot be negative."));

            RuleFor(x => x.Request.MaxShops)
                .GreaterThan(0)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Maksimum mağaza sayısı en az 1 olmalıdır.",
                    "Maximum shop count must be at least 1."));

            RuleFor(x => x.Request.MonthlyAiTokenQuota)
                .GreaterThanOrEqualTo(0)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Aylık AI token kotası negatif olamaz.",
                    "Monthly AI token quota cannot be negative."));

            RuleFor(x => x.Request.MaxBatchQueueSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Toplu kuyruk limiti negatif olamaz.",
                    "Max batch queue size cannot be negative."));
        });
    }
}
