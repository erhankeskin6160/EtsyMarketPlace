namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class SaveFulfillmentMetaValidator : AbstractValidator<SaveFulfillmentMetaRequest>
{
    public SaveFulfillmentMetaValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).",
                "Invalid order ID (ReceiptId must be greater than 0)."));

        RuleFor(x => x.UpdatedMeta)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Sipariş meta verisi boş olamaz.",
                "Order fulfillment metadata cannot be empty."));
    }
}
