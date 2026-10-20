namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;

using FluentValidation;

public class CreateCarrierShipmentValidator : AbstractValidator<CreateCarrierShipmentRequest>
{
    public CreateCarrierShipmentValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("Gönderi oluşturma komutu boş olamaz.");

        RuleFor(x => x.Command.ReceiptId)
            .GreaterThan(0)
            .WithMessage("Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).");

        RuleFor(x => x.Command.CarrierKey)
            .NotEmpty()
            .WithMessage("Kargo firması (CarrierKey) belirtilmelidir.");
    }
}
