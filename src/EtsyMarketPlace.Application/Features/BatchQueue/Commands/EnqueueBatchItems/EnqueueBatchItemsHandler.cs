namespace EtsyMarketPlace.Application.Features.BatchQueue.Commands.EnqueueBatchItems;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class EnqueueBatchItemsHandler(
    IBatchQueueRepository repository
) : IRequestHandler<EnqueueBatchItemsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(EnqueueBatchItemsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var enqueued = await repository.EnqueueBatchAsync(request.Items, cancellationToken);
        return ServiceResponse.CreateSuccess("İlanlar başarıyla toplu işlem kuyruğuna eklendi.", enqueued);
    }
}
