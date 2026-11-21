namespace EtsyMarketPlace.Application.Features.Auth.Commands.Register;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı boş olamaz.",
                "Username cannot be empty."))
            .MinimumLength(3)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı adı en az 3 karakter olmalıdır.",
                "Username must be at least 3 characters."));

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "E-posta adresi boş olamaz.",
                "Email address cannot be empty."))
            .EmailAddress()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir e-posta adresi giriniz.",
                "Please enter a valid email address."));

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Şifre boş olamaz.",
                "Password cannot be empty."))
            .MinimumLength(6)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Şifre en az 6 karakter olmalıdır.",
                "Password must be at least 6 characters."));
    }
}
