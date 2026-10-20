namespace EtsyMarketPlace.Application.Features.AbTests.Queries.GetAbTests;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAbTestsHandler(
    IAbTestRepository repository
) : IRequestHandler<GetAbTestsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAbTestsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var tests = string.IsNullOrWhiteSpace(request.ListingId)
            ? await repository.GetRecentAsync(request.Limit, cancellationToken)
            : await repository.GetByListingIdAsync(request.ListingId, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, tests);
    }
}
