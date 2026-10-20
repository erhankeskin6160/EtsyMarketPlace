namespace EtsyMarketPlace.Application.Features.Auth.Queries.GetCurrentUser;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetCurrentUserHandler(
    IUserRepository userRepo
) : IRequestHandler<GetCurrentUserRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetCurrentUserRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return ServiceResponse.CreateError("Kullanıcı bulunamadı veya hesap askıya alınmış.", HttpStatusCode.Forbidden);
        }

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), user.LastLoginAt?.ToString("O"));
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, userDto);
    }
}
