namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;

using FluentValidation;

public class SaveOrderAddressValidator : AbstractValidator<SaveOrderAddressRequest>
{
    public SaveOrderAddressValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage("Geçersiz sipariş numarası (ReceiptId 0'dan büyük olmalıdır).");

        RuleFor(x => x.UpdatedAddress)
            .NotNull()
            .WithMessage("Güncellenecek teslimat adresi bilgisi boş olamaz.");
    }
}
