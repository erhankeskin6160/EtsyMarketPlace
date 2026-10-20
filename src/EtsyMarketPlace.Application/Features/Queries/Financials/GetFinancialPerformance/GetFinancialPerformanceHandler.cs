using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetFinancialPerformance;

/// <summary>
/// PostgreSQL veritabanından ve canlı defterden finansal KPI ve karne verilerini getiren MediatR sorgu işleyicisi.
/// </summary>
public class GetFinancialPerformanceHandler(
    IEtsyDashboardService dashboard
) : IRequestHandler<GetFinancialPerformanceRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetFinancialPerformanceRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        try
        {
            var performance = await dashboard.GetFinancialPerformanceAsync(
                request.ShopId,
                request.Period,
                request.StartDate,
                request.EndDate,
                cancellationToken);

            return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, performance);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError(ex.Message, HttpStatusCode.BadRequest);
        }
    }
}
