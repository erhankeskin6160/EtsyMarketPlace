namespace EtsyMarketPlace.Application.Features.Admin.Queries.GetSystemStats;

using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetSystemStatsHandler(
    IUserRepository userRepo
) : IRequestHandler<GetSystemStatsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetSystemStatsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var users = await userRepo.GetAllUsersAsync(cancellationToken);
        var logs = await userRepo.GetAuditLogsAsync(1000, cancellationToken);

        var totalUsers = users.Count;
        var activeUsers = users.Count(u => u.IsActive);
        var totalShops = users.SelectMany(u => u.AssignedShopIds).Distinct().Count();
        var totalUsedTokens = users.Sum(u => (long)u.UsedAiTokens);

        var stats = new SystemStatsDto(totalUsers, activeUsers, totalShops, totalUsedTokens, logs.Count);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, stats);
    }
}
