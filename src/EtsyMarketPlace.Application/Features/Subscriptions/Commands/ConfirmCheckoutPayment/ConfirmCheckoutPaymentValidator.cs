using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;

public sealed class ConfirmCheckoutPaymentValidator : AbstractValidator<ConfirmCheckoutPaymentCommand>
{
    public ConfirmCheckoutPaymentValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Kullanıcı kimliği gereklidir.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Ödeme onay bilgileri gereklidir.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SessionId)
                .NotEmpty()
                .WithMessage("Ödeme oturum kimliği (sessionId) gereklidir.");

            RuleFor(x => x.Request.CardHolderName)
                .NotEmpty()
                .WithMessage("Kart üzerindeki ad soyad gereklidir.")
                .MinimumLength(2)
                .WithMessage("Kart üzerindeki ad en az 2 karakter olmalıdır.");

            RuleFor(x => x.Request.CardNumber)
                .NotEmpty()
                .WithMessage("Kart numarası gereklidir.")
                .Must(num =>
                {
                    var digits = new string((num ?? string.Empty).Where(char.IsDigit).ToArray());
                    return digits.Length is >= 13 and <= 19;
                })
                .WithMessage("Geçerli bir 13-19 haneli kart numarası giriniz.");

            RuleFor(x => x.Request.ExpiryMonth)
                .NotEmpty()
                .Must(m => int.TryParse(m?.Trim(), out var mm) && mm is >= 1 and <= 12)
                .WithMessage("Geçerli bir son kullanma ayı (01-12) giriniz.");

            RuleFor(x => x.Request.ExpiryYear)
                .NotEmpty()
                .Must(y => int.TryParse(y?.Trim(), out var yy) && yy is >= 24 and <= 2099)
                .WithMessage("Geçerli bir son kullanma yılı giriniz.");

            RuleFor(x => x.Request.Cvc)
                .NotEmpty()
                .Must(c =>
                {
                    var digits = new string((c ?? string.Empty).Where(char.IsDigit).ToArray());
                    return digits.Length is 3 or 4;
                })
                .WithMessage("Geçerli 3 veya 4 haneli CVC güvenlik kodu giriniz.");
        });
    }
}
