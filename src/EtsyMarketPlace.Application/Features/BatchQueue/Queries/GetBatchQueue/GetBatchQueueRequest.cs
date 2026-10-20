namespace EtsyMarketPlace.Application.Features.BatchQueue.Queries.GetBatchQueue;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetBatchQueueRequest : IRequest<ServiceResponse>
{
    public string? Status { get; set; }
}
