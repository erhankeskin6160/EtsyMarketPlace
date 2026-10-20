namespace EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;

using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class UpdateUserCommandRequest : IRequest<ServiceResponse>
{
    public string Id { get; set; } = string.Empty;
    public UpdateUserRequest Request { get; set; } = null!;
    public string? AdminId { get; set; }
    public string? AdminName { get; set; }
    public string? RemoteIpAddress { get; set; }
}
