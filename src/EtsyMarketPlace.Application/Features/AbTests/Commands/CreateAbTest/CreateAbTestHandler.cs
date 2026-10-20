namespace EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class CreateAbTestHandler(
    IAbTestRepository repository
) : IRequestHandler<CreateAbTestRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CreateAbTestRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var saved = await repository.SaveAsync(request.Experiment, cancellationToken);
        return ServiceResponse.CreateSuccess("A/B test deneyi başarıyla başlatıldı.", saved);
    }
}
