namespace EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;

using FluentValidation;

public class ConnectCarrierValidator : AbstractValidator<ConnectCarrierRequest>
{
    public ConnectCarrierValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("Taşıyıcı bağlantı komutu boş olamaz.");

        RuleFor(x => x.Command.CarrierKey)
            .NotEmpty()
            .WithMessage("Kargo firması (CarrierKey) belirtilmelidir.");
    }
}
