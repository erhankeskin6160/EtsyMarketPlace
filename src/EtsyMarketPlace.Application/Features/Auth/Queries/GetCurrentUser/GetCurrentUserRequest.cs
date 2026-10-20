namespace EtsyMarketPlace.Application.Features.Auth.Queries.GetCurrentUser;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetCurrentUserRequest : IRequest<ServiceResponse>
{
    public string UserId { get; set; } = string.Empty;
}
