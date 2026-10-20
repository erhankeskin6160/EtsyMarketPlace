using FluentValidation;

namespace EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

public class ImportFullFinancialsValidator : AbstractValidator<ImportFullFinancialsRequest>
{
    public ImportFullFinancialsValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("Mağaza ID (ShopId) boş bırakılamaz.")
            .MaximumLength(64).WithMessage("Mağaza ID en fazla 64 karakter olabilir.");
    }
}
