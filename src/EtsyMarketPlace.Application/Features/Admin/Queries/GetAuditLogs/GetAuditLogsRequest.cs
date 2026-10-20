namespace EtsyMarketPlace.Application.Features.Admin.Queries.GetAuditLogs;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAuditLogsRequest : IRequest<ServiceResponse>
{
    public int Limit { get; set; } = 100;
}
