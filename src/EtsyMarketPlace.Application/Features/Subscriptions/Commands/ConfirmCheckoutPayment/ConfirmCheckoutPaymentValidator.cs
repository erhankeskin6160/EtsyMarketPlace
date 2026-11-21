using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;

public sealed class ConfirmCheckoutPaymentValidator : AbstractValidator<ConfirmCheckoutPaymentCommand>
{
    public ConfirmCheckoutPaymentValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı kimliği gereklidir.",
                "User ID is required."));

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ödeme onay bilgileri gereklidir.",
                "Payment confirmation details are required."));

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SessionId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Ödeme oturum kimliği (sessionId) gereklidir.",
                    "Payment session ID (sessionId) is required."));

            RuleFor(x => x.Request.CardHolderName)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Kart üzerindeki ad soyad gereklidir.",
                    "Cardholder name is required."))
                .MinimumLength(2)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Kart üzerindeki ad en az 2 karakter olmalıdır.",
                    "Cardholder name must be at least 2 characters."));

            RuleFor(x => x.Request.CardNumber)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Kart numarası gereklidir.",
                    "Card number is required."))
                .Must(num =>
                {
                    var digits = new string((num ?? string.Empty).Where(char.IsDigit).ToArray());
                    return digits.Length is >= 13 and <= 19;
                })
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçerli bir 13-19 haneli kart numarası giriniz.",
                    "Please enter a valid 13–19 digit card number."));

            RuleFor(x => x.Request.ExpiryMonth)
                .NotEmpty()
                .Must(m => int.TryParse(m?.Trim(), out var mm) && mm is >= 1 and <= 12)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçerli bir son kullanma ayı (01-12) giriniz.",
                    "Please enter a valid expiration month (01–12)."));

            RuleFor(x => x.Request.ExpiryYear)
                .NotEmpty()
                .Must(y => int.TryParse(y?.Trim(), out var yy) && yy is >= 24 and <= 2099)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçerli bir son kullanma yılı giriniz.",
                    "Please enter a valid expiration year."));

            RuleFor(x => x.Request.Cvc)
                .NotEmpty()
                .Must(c =>
                {
                    var digits = new string((c ?? string.Empty).Where(char.IsDigit).ToArray());
                    return digits.Length is 3 or 4;
                })
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Geçerli 3 veya 4 haneli CVC güvenlik kodu giriniz.",
                    "Please enter a valid 3- or 4-digit CVC security code."));
        });
    }
}
