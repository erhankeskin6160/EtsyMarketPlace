namespace EtsyMarketPlace.Application.Features.Common.Validation;

using EtsyMarketPlace.Application.Auth.Validation;
using EtsyMarketPlace.Application.Features.AbTests.Commands.DeleteAbTest;
using EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus;
using EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;
using EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;
using EtsyMarketPlace.Application.Features.AiUsage.Commands.RecordAiUsage;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.EnqueueBatchItems;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;
using EtsyMarketPlace.Application.Features.Financials;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;
using EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;
using EtsyMarketPlace.Application.Features.Orders.Commands.UploadOrdersCsv;
using EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;
using EtsyMarketPlace.Application.Features.Tracking.Commands.DeleteTrackingItem;
using EtsyMarketPlace.Application.Features.Tracking.Commands.SaveTrackingCapture;
using FluentValidation;

public sealed class UploadOrdersCsvRequestValidator : AbstractValidator<UploadOrdersCsvRequest>
{
    public UploadOrdersCsvRequestValidator()
    {
        RuleFor(x => x.Records)
            .NotEmpty()
            .WithMessage("Yüklenecek sipariş adresi kaydı bulunamadı.");
    }
}

public sealed class SyncExtensionOrdersRequestValidator : AbstractValidator<SyncExtensionOrdersRequest>
{
    public SyncExtensionOrdersRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Orders)
            .NotEmpty()
            .WithMessage("Sipariş listesi boş olamaz.");
    }
}

public sealed class GetReceiptDetailRequestValidator : AbstractValidator<GetReceiptDetailRequest>
{
    public GetReceiptDetailRequestValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage("Geçersiz sipariş (receipt) numarası.");
    }
}

public sealed class GetCarrierQuotesRequestValidator : AbstractValidator<GetCarrierQuotesRequest>
{
    public GetCarrierQuotesRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotNull()
            .WithMessage("Navlun sorgu parametreleri boş olamaz.");

        When(x => x.Query != null, () =>
        {
            RuleFor(x => x.Query.WeightKg)
                .GreaterThan(0d)
                .WithMessage("Paket ağırlığı 0'dan büyük olmalıdır.");
        });
    }
}

public sealed class SavePaymentReserveSnapshotCommandValidator : AbstractValidator<SavePaymentReserveSnapshotCommand>
{
    public SavePaymentReserveSnapshotCommandValidator()
    {
        RuleFor(x => x.Snapshot)
            .NotNull()
            .WithMessage("Ödeme rezerv verisi boş olamaz.");

        When(x => x.Snapshot != null, () =>
        {
            RuleFor(x => x.Snapshot.ShopId)
                .NotEmpty()
                .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

            RuleFor(x => x.Snapshot.ReservePercent)
                .InclusiveBetween(0m, 100m)
                .WithMessage("Rezerv oranı %0 ile %100 arasında olmalıdır.");
        });
    }
}

public sealed class GetBankPayoutsRequestValidator : AbstractValidator<GetBankPayoutsRequest>
{
    public GetBankPayoutsRequestValidator()
    {
        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.StartDate)
                .Must((req, start) => start!.Value <= req.EndDate!.Value)
                .WithMessage("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        });
    }
}

public sealed class EnqueueBatchItemsRequestValidator : AbstractValidator<EnqueueBatchItemsRequest>
{
    public EnqueueBatchItemsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Kuyruğa eklenecek en az 1 ürün olmalıdır.");

        When(x => x.Items != null, () =>
        {
            RuleFor(x => x.Items.Count)
                .LessThanOrEqualTo(50)
                .OverridePropertyName("Items")
                .WithMessage("Tek seferde en fazla 50 ürün kuyruğa eklenebilir.");
        });
    }
}

public sealed class ProcessBatchItemRequestValidator : AbstractValidator<ProcessBatchItemRequest>
{
    public ProcessBatchItemRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Geçerli bir kuyruk kayıt numarası belirtilmelidir.");

        RuleFor(x => x.OverallScore)
            .InclusiveBetween(0, 100)
            .WithMessage("Kalite puanı 0 ile 100 arasında olmalıdır.");
    }
}

public sealed class UpdateAbTestStatusRequestValidator : AbstractValidator<UpdateAbTestStatusRequest>
{
    public UpdateAbTestStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Güncellenecek A/B testi kimliği geçersiz.");
    }
}

public sealed class DeleteAbTestRequestValidator : AbstractValidator<DeleteAbTestRequest>
{
    public DeleteAbTestRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Silinecek A/B testi kimliği geçersiz.");
    }
}

public sealed class UpdateUserCommandRequestValidator : AbstractValidator<UpdateUserCommandRequest>
{
    public UpdateUserCommandRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Güncellenecek kullanıcı kimliği belirtilmelidir.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Kullanıcı güncelleme verileri boş olamaz.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request).SetValidator(new UpdateUserRequestValidator());
        });
    }
}

public sealed class ToggleUserStatusRequestValidator : AbstractValidator<ToggleUserStatusRequest>
{
    public ToggleUserStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Durumu değiştirilecek kullanıcı kimliği belirtilmelidir.");
    }
}

public sealed class SaveTrackingCaptureRequestValidator : AbstractValidator<SaveTrackingCaptureRequest>
{
    public SaveTrackingCaptureRequestValidator()
    {
        RuleFor(x => x.Capture)
            .NotNull()
            .WithMessage("Takip görüntüsü boş olamaz.");
    }
}

public sealed class DeleteTrackingItemRequestValidator : AbstractValidator<DeleteTrackingItemRequest>
{
    public DeleteTrackingItemRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Silinecek takip kaydı kimliği geçersiz.");
    }
}

public sealed class RecordAiUsageRequestValidator : AbstractValidator<RecordAiUsageRequest>
{
    public RecordAiUsageRequestValidator()
    {
        RuleFor(x => x.Record)
            .NotNull()
            .WithMessage("AI kullanım kaydı boş olamaz.");
    }
}
