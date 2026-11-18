using EtsyMarketPlace.Domain.Subscriptions;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Kullanıcı kimliği gereklidir.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Ödeme oturumu isteği boş olamaz.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .WithMessage("Paket kimliği gereklidir.")
                .Must(SubscriptionPlanCode.IsValid)
                .WithMessage("Geçersiz abonelik paketi seçildi. (starter, growth, pro, enterprise)");

            RuleFor(x => x.Request.BillingCycle)
                .Must(c => string.IsNullOrWhiteSpace(c) ||
                           string.Equals(c.Trim(), "Monthly", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(c.Trim(), "Yearly", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Faturalama dönemi 'Monthly' veya 'Yearly' olmalıdır.");

            RuleFor(x => x.Request.Currency)
                .Must(c => string.IsNullOrWhiteSpace(c) ||
                           string.Equals(c.Trim(), "USD", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(c.Trim(), "TRY", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Para birimi 'USD' veya 'TRY' olmalıdır.");
        });
    }
}
