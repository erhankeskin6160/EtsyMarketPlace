namespace EtsyMarketPlace.Application.Features.BatchQueue.Commands.ClearCompletedBatch;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class ClearCompletedBatchHandler(
    IBatchQueueRepository repository
) : IRequestHandler<ClearCompletedBatchRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ClearCompletedBatchRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var count = await repository.ClearCompletedAsync(cancellationToken);
        return ServiceResponse.CreateSuccess("Tamamlanan kuyruk öğeleri temizlendi.", new { success = true, cleared = count });
    }
}
