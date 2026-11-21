namespace EtsyMarketPlace.Application.Features.Auth.Commands.Login;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class LoginUserValidator : AbstractValidator<LoginUserRequest>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı veya e-posta gereklidir.",
                "Username or email is required."));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Şifre gereklidir.",
                "Password is required."));
    }
}
