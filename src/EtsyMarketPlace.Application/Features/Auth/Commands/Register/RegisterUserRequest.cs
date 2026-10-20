namespace EtsyMarketPlace.Application.Features.Auth.Commands.Register;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class RegisterUserRequest : IRequest<ServiceResponse>
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ShopId { get; set; }
    public string? RemoteIpAddress { get; set; }
}
