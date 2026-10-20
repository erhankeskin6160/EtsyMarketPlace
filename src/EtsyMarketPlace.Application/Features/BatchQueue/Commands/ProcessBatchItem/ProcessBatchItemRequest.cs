namespace EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;

using System;
using System.Collections.Generic;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class ProcessBatchItemRequest : IRequest<ServiceResponse>
{
    public long Id { get; set; }
    public string? OptimizedTitle { get; set; }
    public string? OptimizedDescription { get; set; }
    public IReadOnlyList<string>? OptimizedTags { get; set; }
    public int OverallScore { get; set; }
    public BatchQueueItemStatus Status { get; set; } = BatchQueueItemStatus.Completed;
}
