namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateReturnPolicy;

using System.Linq;
using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class CreateReturnPolicyValidator : AbstractValidator<CreateReturnPolicyRequest>
{
    private static readonly int[] AllowedDeadlines = [7, 14, 21, 30, 45, 60, 90];

    public CreateReturnPolicyValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İade politikası oluşturma komutu boş olamaz.",
                "Return policy creation command cannot be empty."));

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.ShopId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Mağaza kimliği (Shop ID) belirtilmelidir.",
                    "Shop ID must be specified."));

            When(x => x.Command.AcceptsReturns || x.Command.AcceptsExchanges, () =>
            {
                RuleFor(x => x.Command.ReturnDeadline)
                    .NotNull()
                    .Must(d => d.HasValue && AllowedDeadlines.Contains(d.Value))
                    .WithMessage(_ => ValidationMessageLocalizer.Pick(
                        "İade veya değişim kabul edildiğinde geçerli bir iade süresi seçilmelidir (7, 14, 21, 30, 45, 60 veya 90 gün).",
                        "A valid return deadline must be selected when returns or exchanges are accepted (7, 14, 21, 30, 45, 60, or 90 days)."));
            });
        });
    }
}
