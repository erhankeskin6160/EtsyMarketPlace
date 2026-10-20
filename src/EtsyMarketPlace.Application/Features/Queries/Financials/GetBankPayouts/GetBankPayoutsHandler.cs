using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;

/// <summary>
/// PostgreSQL bank_payouts tablosundan deduplicate edilmiş banka hakediş transferlerini getiren MediatR sorgu işleyicisi.
/// </summary>
public class GetBankPayoutsHandler(
    IEtsyDashboardService dashboard
) : IRequestHandler<GetBankPayoutsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetBankPayoutsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var end = request.EndDate ?? DateTimeOffset.UtcNow;
        var start = request.StartDate ?? new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);

        if (start > end)
            return ServiceResponse.CreateError("startDate endDate değerinden sonra olamaz.", HttpStatusCode.BadRequest);

        var payouts = await dashboard.GetBankPayoutsAsync(request.ShopId, start, end, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, payouts);
    }
}
