namespace EtsyMarketPlace.Application.Features.Auth.Commands.Logout;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public sealed class LogoutUserHandler(
    IUserRepository userRepo,
    IIdentityAuthService? identityAuthService = null
) : IRequestHandler<LogoutUserRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(LogoutUserRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (string.IsNullOrWhiteSpace(request.UserId))
            return ServiceResponse.CreateError("Geçersiz kullanıcı oturumu.", HttpStatusCode.Unauthorized);

        if (identityAuthService is not null)
        {
            await identityAuthService.LogoutAsync(request.UserId, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var username = string.IsNullOrWhiteSpace(request.Username) ? request.UserId : request.Username;
        await userRepo.AddAuditLogAsync(
            new AuditLogEntry(0, request.UserId, username, "Logout", "Oturum güvenli şekilde kapatıldı", request.RemoteIpAddress, now),
            cancellationToken);

        return ServiceResponse.CreateSuccess("Oturum başarıyla kapatıldı.", new { success = true, message = "Oturum başarıyla kapatıldı." });
    }
}
