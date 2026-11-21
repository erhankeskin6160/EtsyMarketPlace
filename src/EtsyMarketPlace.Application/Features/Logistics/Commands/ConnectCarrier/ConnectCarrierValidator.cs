namespace EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class ConnectCarrierValidator : AbstractValidator<ConnectCarrierRequest>
{
    public ConnectCarrierValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Taşıyıcı bağlantı komutu boş olamaz.",
                "Carrier connection command cannot be empty."));

        RuleFor(x => x.Command.CarrierKey)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kargo firması (CarrierKey) belirtilmelidir.",
                "Shipping carrier (CarrierKey) must be specified."));
    }
}
