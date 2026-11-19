namespace EtsyMarketPlace.Application.Features.Auth.Commands.Register;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Subscriptions;
using MediatR;

public class RegisterUserHandler(
    IUserRepository userRepo,
    JwtTokenService jwtService,
    IIdentityAuthService? identityAuthService = null
) : IRequestHandler<RegisterUserRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var now = DateTimeOffset.UtcNow;
        AppUser newUser;

        if (identityAuthService is not null)
        {
            var regResult = await identityAuthService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password,
                request.ShopId,
                cancellationToken);

            if (!regResult.Succeeded || regResult.Data is null)
            {
                return ServiceResponse.CreateError(
                    regResult.ErrorMessage ?? "Kullanıcı kaydı oluşturulamadı.",
                    regResult.StatusCode);
            }

            newUser = regResult.Data;
        }
        else
        {
            var existingUser = await userRepo.GetByUsernameOrEmailAsync(request.Username, cancellationToken)
                ?? await userRepo.GetByUsernameOrEmailAsync(request.Email, cancellationToken);

            if (existingUser != null)
            {
                return ServiceResponse.CreateError("Bu kullanıcı adı veya e-posta zaten kullanımda.", HttpStatusCode.Conflict);
            }

            var (hash, salt) = PasswordHasher.HashPassword(request.Password);
            var assignedShops = string.IsNullOrWhiteSpace(request.ShopId) ? new List<string> { "53236321" } : new List<string> { request.ShopId.Trim() };
            var starterPlan = SubscriptionFeatureKeys.GetDefaultPlans().First(p => p.Id == SubscriptionPlanCode.Starter);

            newUser = new AppUser(
                Guid.NewGuid().ToString(),
                request.Username.Trim(),
                request.Email.Trim().ToLowerInvariant(),
                hash,
                salt,
                UserRoles.StoreOwner,
                assignedShops,
                starterPlan.MonthlyAiTokenQuota,
                0,
                true,
                now,
                now,
                SubscriptionPlanCode.Starter,
                SubscriptionStatus.Active.ToString(),
                now.AddMonths(1));

            await userRepo.CreateUserAsync(newUser, cancellationToken);
        }

        await userRepo.AddAuditLogAsync(new AuditLogEntry(0, newUser.Id, newUser.Username, "Register", "Yeni kayıt oluşturuldu (Starter Paketi)", request.RemoteIpAddress, now), cancellationToken);

        var token = jwtService.GenerateToken(newUser);
        var userDto = newUser.ToDto(now);

        return ServiceResponse.CreateSuccess("Kayıt başarıyla tamamlandı.", new AuthResponse(true, token, "Kayıt başarıyla tamamlandı.", userDto));
    }
}
