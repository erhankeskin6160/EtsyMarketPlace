using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Auth.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı gereklidir.",
                "Username is required."))
            .MinimumLength(3)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı en az 3 karakter olmalıdır.",
                "Username must be at least 3 characters."));

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "E-posta adresi gereklidir.",
                "Email address is required."))
            .EmailAddress()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir e-posta adresi giriniz.",
                "Please enter a valid email address."));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Şifre gereklidir.",
                "Password is required."))
            .MinimumLength(6)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Şifre en az 6 karakter olmalıdır.",
                "Password must be at least 6 characters."));
    }
}
