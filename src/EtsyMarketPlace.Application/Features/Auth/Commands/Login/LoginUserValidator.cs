namespace EtsyMarketPlace.Application.Features.Auth.Commands.Login;

using FluentValidation;

public class LoginUserValidator : AbstractValidator<LoginUserRequest>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage("Kullanıcı adı veya e-posta gereklidir.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Şifre gereklidir.");
    }
}
