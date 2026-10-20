namespace EtsyMarketPlace.Application.Features.BatchQueue.Queries.GetBatchQueue;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetBatchQueueHandler(
    IBatchQueueRepository repository
) : IRequestHandler<GetBatchQueueRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetBatchQueueRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var items = string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            ? await repository.GetPendingAsync(100, cancellationToken)
            : await repository.GetAllAsync(200, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, items);
    }
}
