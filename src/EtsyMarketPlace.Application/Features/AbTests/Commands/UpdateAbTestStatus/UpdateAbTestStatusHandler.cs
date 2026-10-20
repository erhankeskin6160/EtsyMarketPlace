namespace EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class UpdateAbTestStatusHandler(
    IAbTestRepository repository
) : IRequestHandler<UpdateAbTestStatusRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpdateAbTestStatusRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var updated = await repository.UpdateStatusAsync(request.Id, request.Status, cancellationToken);
        if (updated is null)
        {
            return ServiceResponse.CreateNotFound($"A/B test #{request.Id} bulunamadı.");
        }

        return ServiceResponse.CreateSuccess("A/B test durumu güncellendi.", updated);
    }
}
