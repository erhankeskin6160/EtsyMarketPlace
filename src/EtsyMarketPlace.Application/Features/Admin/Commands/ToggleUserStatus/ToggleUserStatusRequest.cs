namespace EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class ToggleUserStatusRequest : IRequest<ServiceResponse>
{
    public string Id { get; set; } = string.Empty;
    public string? AdminId { get; set; }
    public string? AdminName { get; set; }
    public string? RemoteIpAddress { get; set; }
}
