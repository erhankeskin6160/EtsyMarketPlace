using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetMonthlyOrderSummaries;

/// <summary>
/// PostgreSQL monthly_order_summaries tablosundan aylık sipariş ve ciro özetlerini getiren MediatR sorgu işleyicisi.
/// </summary>
public class GetMonthlyOrderSummariesHandler(
    IEtsyIntegrationRepository repository
) : IRequestHandler<GetMonthlyOrderSummariesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetMonthlyOrderSummariesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var summaries = await repository.GetMonthlyOrderSummariesAsync(request.ShopId, request.Months, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, summaries);
    }
}
