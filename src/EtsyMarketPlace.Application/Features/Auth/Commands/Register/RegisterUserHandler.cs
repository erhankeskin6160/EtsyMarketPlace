namespace EtsyMarketPlace.Application.Features.Auth.Commands.Register;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class RegisterUserHandler(
    IUserRepository userRepo,
    JwtTokenService jwtService
) : IRequestHandler<RegisterUserRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var existingUser = await userRepo.GetByUsernameOrEmailAsync(request.Username, cancellationToken)
            ?? await userRepo.GetByUsernameOrEmailAsync(request.Email, cancellationToken);

        if (existingUser != null)
        {
            return ServiceResponse.CreateError("Bu kullanıcı adı veya e-posta zaten kullanımda.", HttpStatusCode.Conflict);
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        var now = DateTimeOffset.UtcNow;
        var assignedShops = string.IsNullOrWhiteSpace(request.ShopId) ? new List<string> { "53236321" } : new List<string> { request.ShopId.Trim() };

        var newUser = new AppUser(
            Guid.NewGuid().ToString(),
            request.Username.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            hash,
            salt,
            UserRoles.StoreOwner,
            assignedShops,
            500_000,
            0,
            true,
            now,
            now);

        await userRepo.CreateUserAsync(newUser, cancellationToken);
        await userRepo.AddAuditLogAsync(new AuditLogEntry(0, newUser.Id, newUser.Username, "Register", "Yeni kayıt oluşturuldu", request.RemoteIpAddress, now), cancellationToken);

        var token = jwtService.GenerateToken(newUser);
        var userDto = new UserDto(newUser.Id, newUser.Username, newUser.Email, newUser.Role, newUser.AssignedShopIds, newUser.MonthlyAiTokenQuota, newUser.UsedAiTokens, newUser.IsActive, newUser.CreatedAt.ToString("O"), now.ToString("O"));

        return ServiceResponse.CreateSuccess("Kayıt başarıyla tamamlandı.", new AuthResponse(true, token, "Kayıt başarıyla tamamlandı.", userDto));
    }
}
