namespace EtsyMarketPlace.Application.Features.Common.Validation;

using EtsyMarketPlace.Application.Auth.Validation;
using EtsyMarketPlace.Application.Common.Localization;
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
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Yüklenecek sipariş adresi kaydı bulunamadı.",
                "No order address records found to upload."));
    }
}

public sealed class SyncExtensionOrdersRequestValidator : AbstractValidator<SyncExtensionOrdersRequest>
{
    public SyncExtensionOrdersRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Orders)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Sipariş listesi boş olamaz.",
                "Order list cannot be empty."));
    }
}

public sealed class GetReceiptDetailRequestValidator : AbstractValidator<GetReceiptDetailRequest>
{
    public GetReceiptDetailRequestValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçersiz sipariş (receipt) numarası.",
                "Invalid order (receipt) ID."));
    }
}

public sealed class GetCarrierQuotesRequestValidator : AbstractValidator<GetCarrierQuotesRequest>
{
    public GetCarrierQuotesRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Navlun sorgu parametreleri boş olamaz.",
                "Shipping quote query parameters cannot be empty."));

        When(x => x.Query != null, () =>
        {
            RuleFor(x => x.Query.WeightKg)
                .GreaterThan(0d)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Paket ağırlığı 0'dan büyük olmalıdır.",
                    "Package weight must be greater than 0."));
        });
    }
}

public sealed class SavePaymentReserveSnapshotCommandValidator : AbstractValidator<SavePaymentReserveSnapshotCommand>
{
    public SavePaymentReserveSnapshotCommandValidator()
    {
        RuleFor(x => x.Snapshot)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ödeme rezerv verisi boş olamaz.",
                "Payment reserve data cannot be empty."));

        When(x => x.Snapshot != null, () =>
        {
            RuleFor(x => x.Snapshot.ShopId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Mağaza kimliği (Shop ID) zorunludur.",
                    "Shop ID is required."));

            RuleFor(x => x.Snapshot.ReservePercent)
                .InclusiveBetween(0m, 100m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Rezerv oranı %0 ile %100 arasında olmalıdır.",
                    "Reserve rate must be between 0% and 100%."));
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
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Başlangıç tarihi bitiş tarihinden sonra olamaz.",
                    "Start date cannot be after end date."));
        });
    }
}

public sealed class EnqueueBatchItemsRequestValidator : AbstractValidator<EnqueueBatchItemsRequest>
{
    public EnqueueBatchItemsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kuyruğa eklenecek en az 1 ürün olmalıdır.",
                "At least 1 item must be provided to enqueue."));

        When(x => x.Items != null, () =>
        {
            RuleFor(x => x.Items.Count)
                .LessThanOrEqualTo(50)
                .OverridePropertyName("Items")
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Tek seferde en fazla 50 ürün kuyruğa eklenebilir.",
                    "At most 50 items can be enqueued at once."));
        });
    }
}

public sealed class ProcessBatchItemRequestValidator : AbstractValidator<ProcessBatchItemRequest>
{
    public ProcessBatchItemRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir kuyruk kayıt numarası belirtilmelidir.",
                "A valid queue item ID must be specified."));

        RuleFor(x => x.OverallScore)
            .InclusiveBetween(0, 100)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kalite puanı 0 ile 100 arasında olmalıdır.",
                "Quality score must be between 0 and 100."));
    }
}

public sealed class UpdateAbTestStatusRequestValidator : AbstractValidator<UpdateAbTestStatusRequest>
{
    public UpdateAbTestStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Güncellenecek A/B testi kimliği geçersiz.",
                "Invalid A/B test ID to update."));
    }
}

public sealed class DeleteAbTestRequestValidator : AbstractValidator<DeleteAbTestRequest>
{
    public DeleteAbTestRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Silinecek A/B testi kimliği geçersiz.",
                "Invalid A/B test ID to delete."));
    }
}

public sealed class UpdateUserCommandRequestValidator : AbstractValidator<UpdateUserCommandRequest>
{
    public UpdateUserCommandRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Güncellenecek kullanıcı kimliği belirtilmelidir.",
                "User ID to update must be specified."));

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kullanıcı güncelleme verileri boş olamaz.",
                "User update data cannot be empty."));

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
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Durumu değiştirilecek kullanıcı kimliği belirtilmelidir.",
                "User ID whose status will be toggled must be specified."));
    }
}

public sealed class SaveTrackingCaptureRequestValidator : AbstractValidator<SaveTrackingCaptureRequest>
{
    public SaveTrackingCaptureRequestValidator()
    {
        RuleFor(x => x.Capture)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Takip görüntüsü boş olamaz.",
                "Tracking capture data cannot be empty."));
    }
}

public sealed class DeleteTrackingItemRequestValidator : AbstractValidator<DeleteTrackingItemRequest>
{
    public DeleteTrackingItemRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Silinecek takip kaydı kimliği geçersiz.",
                "Invalid tracking item ID to delete."));
    }
}

public sealed class RecordAiUsageRequestValidator : AbstractValidator<RecordAiUsageRequest>
{
    public RecordAiUsageRequestValidator()
    {
        RuleFor(x => x.Record)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "AI kullanım kaydı boş olamaz.",
                "AI usage record cannot be empty."));
    }
}
