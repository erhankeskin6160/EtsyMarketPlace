namespace EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class ToggleUserStatusHandler(
    IUserRepository userRepo
) : IRequestHandler<ToggleUserStatusRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ToggleUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var existingUser = await userRepo.GetByIdAsync(request.Id, cancellationToken);
        if (existingUser == null)
        {
            return ServiceResponse.CreateNotFound("Kullanıcı bulunamadı.");
        }

        var updated = existingUser with { IsActive = !existingUser.IsActive };
        await userRepo.UpdateUserAsync(updated, cancellationToken);
        await userRepo.AddAuditLogAsync(new AuditLogEntry(0, request.AdminId ?? "admin", request.AdminName ?? "Admin", "ToggleStatus", $"Kullanıcı durumu değiştirildi: {existingUser.Username} (Aktif: {updated.IsActive})", request.RemoteIpAddress, DateTimeOffset.UtcNow), cancellationToken);

        return ServiceResponse.CreateSuccess("Kullanıcı durumu güncellendi.", new { success = true, isActive = updated.IsActive });
    }
}
