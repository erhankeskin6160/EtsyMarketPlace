using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Auth.Validation;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "E-posta adresi gereklidir.",
                "Email address is required."))
            .EmailAddress()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir e-posta adresi giriniz.",
                "Please enter a valid email address."));

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı rolü gereklidir.",
                "User role is required."))
            .Must(role => role is UserRoles.Admin or UserRoles.StoreOwner or UserRoles.Demo or UserRoles.Operator)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz kullanıcı rolü.",
                "Invalid user role."));

        RuleFor(x => x.MonthlyAiTokenQuota)
            .GreaterThanOrEqualTo(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Aylık yapay zeka token kotası negatif olamaz.",
                "Monthly AI token quota cannot be negative."));
    }
}
