namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;

using FluentValidation;

public class SaveFulfillmentMetaValidator : AbstractValidator<SaveFulfillmentMetaRequest>
{
    public SaveFulfillmentMetaValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage("Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).");

        RuleFor(x => x.UpdatedMeta)
            .NotNull()
            .WithMessage("Sipariş meta verisi boş olamaz.");
    }
}
