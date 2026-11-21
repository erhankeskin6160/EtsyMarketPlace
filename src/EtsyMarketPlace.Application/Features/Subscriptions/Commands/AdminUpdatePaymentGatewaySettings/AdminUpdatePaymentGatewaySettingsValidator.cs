using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdatePaymentGatewaySettings;

public sealed class AdminUpdatePaymentGatewaySettingsValidator : AbstractValidator<AdminUpdatePaymentGatewaySettingsCommand>
{
    private static readonly string[] AllowedProviders = ["Unconfigured", "Iyzico", "PayTR", "Stripe"];

    public AdminUpdatePaymentGatewaySettingsValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ödeme altyapısı ayar isteği boş olamaz.",
                "Payment gateway settings request cannot be empty."));

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ActiveProvider)
                .NotEmpty()
                .Must(p => AllowedProviders.Contains(p?.Trim(), StringComparer.OrdinalIgnoreCase))
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçersiz ödeme sağlayıcısı (Unconfigured, Iyzico, PayTR veya Stripe olmalıdır).",
                    "Invalid payment provider (must be Unconfigured, Iyzico, PayTR, or Stripe)."));
        });
    }
}
