namespace EtsyMarketPlace.Application.Features.Auth.Commands.Login;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class LoginUserHandler(
    IUserRepository userRepo,
    JwtTokenService jwtService
) : IRequestHandler<LoginUserRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(LoginUserRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var user = await userRepo.GetByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);
        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return ServiceResponse.CreateError("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized);
        }

        if (!user.IsActive)
        {
            return ServiceResponse.CreateError("Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.", HttpStatusCode.Forbidden);
        }

        var now = DateTimeOffset.UtcNow;
        await userRepo.UpdateLastLoginAsync(user.Id, now, cancellationToken);
        await userRepo.AddAuditLogAsync(new AuditLogEntry(0, user.Id, user.Username, "Login", "Giriş başarılı", request.RemoteIpAddress, now), cancellationToken);

        var token = jwtService.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.Role, user.AssignedShopIds, user.MonthlyAiTokenQuota, user.UsedAiTokens, user.IsActive, user.CreatedAt.ToString("O"), now.ToString("O"));

        return ServiceResponse.CreateSuccess("Giriş başarılı.", new AuthResponse(true, token, "Giriş başarılı.", userDto));
    }
}
