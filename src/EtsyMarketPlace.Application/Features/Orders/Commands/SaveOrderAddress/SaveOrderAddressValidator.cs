namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class SaveOrderAddressValidator : AbstractValidator<SaveOrderAddressRequest>
{
    public SaveOrderAddressValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).",
                "Invalid order ID (ReceiptId must be greater than 0)."));

        RuleFor(x => x.UpdatedAddress)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Güncellenecek teslimat adresi bilgisi boş olamaz.",
                "Shipping address data to update cannot be empty."));
    }
}
