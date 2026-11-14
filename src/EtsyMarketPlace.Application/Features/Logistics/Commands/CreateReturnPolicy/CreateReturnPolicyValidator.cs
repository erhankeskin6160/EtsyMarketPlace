namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateReturnPolicy;

using System.Linq;
using FluentValidation;

public class CreateReturnPolicyValidator : AbstractValidator<CreateReturnPolicyRequest>
{
    private static readonly int[] AllowedDeadlines = [7, 14, 21, 30, 45, 60, 90];

    public CreateReturnPolicyValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("İade politikası oluşturma komutu boş olamaz.");

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.ShopId)
                .NotEmpty()
                .WithMessage("Mağaza kimliği (Shop ID) belirtilmelidir.");

            When(x => x.Command.AcceptsReturns || x.Command.AcceptsExchanges, () =>
            {
                RuleFor(x => x.Command.ReturnDeadline)
                    .NotNull()
                    .Must(d => d.HasValue && AllowedDeadlines.Contains(d.Value))
                    .WithMessage("İade veya değişim kabul edildiğinde geçerli bir iade süresi seçilmelidir (7, 14, 21, 30, 45, 60 veya 90 gün).");
            });
        });
    }
}
