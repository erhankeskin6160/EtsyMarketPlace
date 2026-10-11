using FluentValidation;

namespace EtsyMarketPlace.Application.Auth.Validation;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("E-posta adresi gereklidir.")
            .EmailAddress()
            .WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Kullanıcı rolü gereklidir.")
            .Must(role => role is UserRoles.Admin or UserRoles.StoreOwner or UserRoles.Demo or UserRoles.Operator)
            .WithMessage("Geçersiz kullanıcı rolü.");

        RuleFor(x => x.MonthlyAiTokenQuota)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Aylık yapay zeka token kotası negatif olamaz.");
    }
}
