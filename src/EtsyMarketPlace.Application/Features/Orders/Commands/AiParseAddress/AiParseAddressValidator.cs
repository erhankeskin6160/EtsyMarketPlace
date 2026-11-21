namespace EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class AiParseAddressValidator : AbstractValidator<AiParseAddressRequest>
{
    public AiParseAddressValidator()
    {
        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Lütfen ayrıştırılacak adres metnini girin.",
                "Please enter the address text to parse."));
    }
}
