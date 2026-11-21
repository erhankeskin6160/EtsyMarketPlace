using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Auth.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı/e-posta ve şifre gereklidir.",
                "Username/email and password are required."));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı/e-posta ve şifre gereklidir.",
                "Username/email and password are required."));
    }
}
