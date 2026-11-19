namespace EtsyMarketPlace.Application.Features.Auth.Commands.Logout;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public sealed class LogoutUserRequest : IRequest<ServiceResponse>
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? RemoteIpAddress { get; set; }
}
