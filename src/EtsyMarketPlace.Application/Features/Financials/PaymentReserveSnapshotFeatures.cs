using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Application.Features.Financials;

public sealed record EtsyPaymentReserveSnapshotDto(
    string ShopId,
    decimal CurrentBalanceTry,
    decimal PendingBalanceTry,
    decimal TotalBalanceTry,
    decimal ReservePercent,
    int ReserveHoldDays,
    string? ReevaluationDate,
    string? StatusMessage,
    DateTimeOffset UpdatedAt);

public sealed record GetPaymentReserveSnapshotQuery(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetPaymentReserveSnapshotHandler(IShopSettingsRepository repository)
    : IRequestHandler<GetPaymentReserveSnapshotQuery, ServiceResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ServiceResponse> Handle(
        GetPaymentReserveSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await repository.GetPaymentReserveSnapshotJsonAsync(request.ShopId, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                var fallback = new EtsyPaymentReserveSnapshotDto(
                    request.ShopId,
                    -50.35m,
                    28479.09m,
                    28428.74m,
                    75m,
                    60,
                    "November 30",
                    "75% is in reserve for 60 days. We'll reevaluate your reserve on November 30.",
                    DateTimeOffset.UtcNow);

                return ServiceResponse.CreateSuccess("Varsayılan Etsy ödeme hesabı rezerv bilgisi döndürüldü.", fallback);
            }

            var dto = JsonSerializer.Deserialize<EtsyPaymentReserveSnapshotDto>(json, JsonOptions);
            return ServiceResponse.CreateSuccess("Etsy ödeme hesabı rezerv bilgisi başarıyla getirildi.", dto);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError($"Rezerv bilgisi alınamadı: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
}

public sealed record SavePaymentReserveSnapshotCommand(EtsyPaymentReserveSnapshotDto Snapshot)
    : IRequest<ServiceResponse>;

public sealed class SavePaymentReserveSnapshotHandler(IShopSettingsRepository repository)
    : IRequestHandler<SavePaymentReserveSnapshotCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(
        SavePaymentReserveSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(request.Snapshot);
            await repository.SavePaymentReserveSnapshotJsonAsync(request.Snapshot.ShopId, json, cancellationToken);
            return ServiceResponse.CreateSuccess("Etsy rezerv ve bakiye durumu başarıyla güncellendi.", request.Snapshot);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError($"Rezerv bilgisi kaydedilemedi: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
}
