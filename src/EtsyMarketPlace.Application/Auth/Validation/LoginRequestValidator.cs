using FluentValidation;

namespace EtsyMarketPlace.Application.Auth.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage("Kullanıcı adı/e-posta ve şifre gereklidir.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Kullanıcı adı/e-posta ve şifre gereklidir.");
    }
}
