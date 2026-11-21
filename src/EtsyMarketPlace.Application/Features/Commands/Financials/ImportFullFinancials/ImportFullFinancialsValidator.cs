using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

public class ImportFullFinancialsValidator : AbstractValidator<ImportFullFinancialsRequest>
{
    public ImportFullFinancialsValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza ID (ShopId) boş bırakılamaz.",
                "Shop ID cannot be empty."))
            .MaximumLength(64)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza ID en fazla 64 karakter olabilir.",
                "Shop ID can be at most 64 characters."));
    }
}
