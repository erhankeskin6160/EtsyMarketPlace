namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class CreateCarrierShipmentValidator : AbstractValidator<CreateCarrierShipmentRequest>
{
    public CreateCarrierShipmentValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Gönderi oluşturma komutu boş olamaz.",
                "Shipment creation command cannot be empty."));

        RuleFor(x => x.Command.ReceiptId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).",
                "Invalid order ID (ReceiptId must be greater than 0)."));

        RuleFor(x => x.Command.CarrierKey)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kargo firması (CarrierKey) belirtilmelidir.",
                "Shipping carrier (CarrierKey) must be specified."));
    }
}
