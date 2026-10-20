namespace EtsyMarketPlace.Application.Features.Admin.Queries.GetAllUsers;

using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAllUsersHandler(
    IUserRepository userRepo
) : IRequestHandler<GetAllUsersRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAllUsersRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var users = await userRepo.GetAllUsersAsync(cancellationToken);
        var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.Email, u.Role, u.AssignedShopIds, u.MonthlyAiTokenQuota, u.UsedAiTokens, u.IsActive, u.CreatedAt.ToString("O"), u.LastLoginAt?.ToString("O"))).ToList();

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, dtos);
    }
}
