namespace EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class UpdateUserCommandHandler(
    IUserRepository userRepo
) : IRequestHandler<UpdateUserCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpdateUserCommandRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var existingUser = await userRepo.GetByIdAsync(request.Id, cancellationToken);
        if (existingUser == null)
        {
            return ServiceResponse.CreateNotFound("Kullanıcı bulunamadı.");
        }

        var updated = existingUser with
        {
            Email = request.Request.Email.Trim().ToLowerInvariant(),
            Role = request.Request.Role,
            AssignedShopIds = request.Request.AssignedShopIds ?? existingUser.AssignedShopIds,
            MonthlyAiTokenQuota = request.Request.MonthlyAiTokenQuota,
            IsActive = request.Request.IsActive
        };

        await userRepo.UpdateUserAsync(updated, cancellationToken);
        await userRepo.AddAuditLogAsync(new AuditLogEntry(0, request.AdminId ?? "admin", request.AdminName ?? "Admin", "UpdateUser", $"Kullanıcı güncellendi: {existingUser.Username} ({updated.Role})", request.RemoteIpAddress, DateTimeOffset.UtcNow), cancellationToken);

        return ServiceResponse.CreateSuccess("Kullanıcı başarıyla güncellendi.", new { success = true });
    }
}
