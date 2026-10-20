namespace EtsyMarketPlace.Application.Features.BatchQueue.Commands.EnqueueBatchItems;

using System.Collections.Generic;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class EnqueueBatchItemsRequest : IRequest<ServiceResponse>
{
    public IReadOnlyList<SaveBatchQueueItem> Items { get; set; } = [];
}
