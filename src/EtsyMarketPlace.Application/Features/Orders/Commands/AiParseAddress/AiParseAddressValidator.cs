namespace EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;

using FluentValidation;

public class AiParseAddressValidator : AbstractValidator<AiParseAddressRequest>
{
    public AiParseAddressValidator()
    {
        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .WithMessage("Lütfen ayrıştırılacak adres metnini girin.");
    }
}
