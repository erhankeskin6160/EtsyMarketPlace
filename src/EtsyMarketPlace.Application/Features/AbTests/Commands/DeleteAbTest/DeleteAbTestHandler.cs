namespace EtsyMarketPlace.Application.Features.AbTests.Commands.DeleteAbTest;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class DeleteAbTestHandler(
    IAbTestRepository repository
) : IRequestHandler<DeleteAbTestRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(DeleteAbTestRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var deleted = await repository.DeleteAsync(request.Id, cancellationToken);
        if (!deleted)
        {
            return ServiceResponse.CreateNotFound($"Silinecek A/B test #{request.Id} bulunamadı.");
        }

        return ServiceResponse.CreateSuccess("A/B test başarıyla silindi.", new { success = true });
    }
}
