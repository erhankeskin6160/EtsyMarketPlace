using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Domain.Subscriptions;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı kimliği gereklidir.",
                "User ID is required."));

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ödeme oturumu isteği boş olamaz.",
                "Checkout session request cannot be empty."));

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Paket kimliği gereklidir.",
                    "Plan ID is required."))
                .Must(SubscriptionPlanCode.IsValid)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçersiz abonelik paketi seçildi. (starter, growth, pro, enterprise)",
                    "Invalid subscription plan selected (starter, growth, pro, enterprise)."));

            RuleFor(x => x.Request.BillingCycle)
                .Must(c => string.IsNullOrWhiteSpace(c) ||
                           string.Equals(c.Trim(), "Monthly", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(c.Trim(), "Yearly", StringComparison.OrdinalIgnoreCase))
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Faturalama dönemi 'Monthly' veya 'Yearly' olmalıdır.",
                    "Billing cycle must be 'Monthly' or 'Yearly'."));

            RuleFor(x => x.Request.Currency)
                .Must(c => string.IsNullOrWhiteSpace(c) ||
                           string.Equals(c.Trim(), "USD", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(c.Trim(), "TRY", StringComparison.OrdinalIgnoreCase))
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Para birimi 'USD' veya 'TRY' olmalıdır.",
                    "Currency must be 'USD' or 'TRY'."));
        });
    }
}
