namespace EtsyMarketPlace.Application.Features.Auth.Commands.Login;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class LoginUserRequest : IRequest<ServiceResponse>
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? RemoteIpAddress { get; set; }
}
