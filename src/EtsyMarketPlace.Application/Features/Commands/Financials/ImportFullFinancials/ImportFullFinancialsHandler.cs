using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

/// <summary>
/// Masaüstünden veya harici servislerden gönderilen tüm finansal kayıtları EF Core ile PostgreSQL'e işleyen MediatR işleyicisi (FlowaErp CQRS Şablonu).
/// </summary>
public class ImportFullFinancialsHandler(
    IEtsyIntegrationRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ImportFullFinancialsHandler> logger
) : IRequestHandler<ImportFullFinancialsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ImportFullFinancialsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        logger.LogInformation("ImportFullFinancials: {ShopId} için finansal veriler PostgreSQL'e aktarılıyor...", request.ShopId);

        if (request.Transactions is not null && request.Transactions.Count > 0)
            await repository.SaveTransactionsAsync(request.Transactions, cancellationToken);

        if (request.Payouts is not null && request.Payouts.Count > 0)
            await repository.SavePayoutsAsync(request.Payouts, cancellationToken);

        if (request.OrderAlerts is not null && request.OrderAlerts.Count > 0)
            await repository.SaveOrderAlertsAsync(request.OrderAlerts, cancellationToken);

        if (request.MonthlyOrders is not null && request.MonthlyOrders.Count > 0)
            await repository.SaveMonthlyOrderSummariesAsync(request.MonthlyOrders, cancellationToken);

        if (request.TrafficRecords is not null && request.TrafficRecords.Count > 0)
            await repository.SaveListingTrafficDailyAsync(request.TrafficRecords, cancellationToken);

        if (request.ChartPayloads is not null && request.ChartPayloads.Count > 0)
        {
            foreach (var chart in request.ChartPayloads)
                await repository.SaveChartSnapshotAsync(chart, cancellationToken);
        }

        var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);

        var responseData = new ImportFullFinancialsResponse
        {
            Success = true,
            Message = "Etsy tüm finansal verileri ve aylık sipariş özetleri PostgreSQL veritabanına başarıyla aktarıldı.",
            TransactionsCount = request.Transactions?.Count ?? 0,
            PayoutsCount = request.Payouts?.Count ?? 0,
            AlertsCount = request.OrderAlerts?.Count ?? 0,
            MonthlyOrdersCount = request.MonthlyOrders?.Count ?? 0,
            TrafficCount = request.TrafficRecords?.Count ?? 0,
            ChartsCount = request.ChartPayloads?.Count ?? 0
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, responseData);
    }
}
