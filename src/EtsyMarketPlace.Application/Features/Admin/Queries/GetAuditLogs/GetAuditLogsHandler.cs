namespace EtsyMarketPlace.Application.Features.Admin.Queries.GetAuditLogs;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAuditLogsHandler(
    IUserRepository userRepo
) : IRequestHandler<GetAuditLogsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAuditLogsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        int clamped = Math.Clamp(request.Limit, 1, 500);
        var logs = await userRepo.GetAuditLogsAsync(clamped, cancellationToken);
        var dtos = logs.Select(l => new AuditLogDto(l.Id, l.UserId, l.Username, l.Action, l.Details, l.IpAddress, l.Timestamp.ToString("O"))).ToList();

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, dtos);
    }
}
