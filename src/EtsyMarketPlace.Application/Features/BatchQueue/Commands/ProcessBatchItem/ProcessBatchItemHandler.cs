namespace EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class ProcessBatchItemHandler(
    IBatchQueueRepository repository
) : IRequestHandler<ProcessBatchItemRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ProcessBatchItemRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null)
        {
            return ServiceResponse.CreateNotFound($"Kuyruk öğesi #{request.Id} bulunamadı.");
        }

        var updated = existing with
        {
            OptimizedTitle = request.OptimizedTitle ?? existing.OptimizedTitle,
            OptimizedDescription = request.OptimizedDescription ?? existing.OptimizedDescription,
            OptimizedTags = request.OptimizedTags ?? existing.OptimizedTags,
            OverallScore = request.OverallScore,
            Status = request.Status,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        var result = await repository.UpdateItemAsync(updated, cancellationToken);
        return ServiceResponse.CreateSuccess("Kuyruk öğesi başarıyla güncellendi.", result);
    }
}
