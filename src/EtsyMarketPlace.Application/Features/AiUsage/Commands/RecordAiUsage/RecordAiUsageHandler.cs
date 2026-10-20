namespace EtsyMarketPlace.Application.Features.AiUsage.Commands.RecordAiUsage;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class RecordAiUsageHandler(
    IAiUsageRepository repository
) : IRequestHandler<RecordAiUsageRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RecordAiUsageRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.Record == null)
            return ServiceResponse.CreateError("AI kullanım kaydı boş olamaz.", HttpStatusCode.BadRequest);

        var saved = await repository.SaveUsageAsync(request.Record, cancellationToken);
        return ServiceResponse.CreateSuccess("AI kullanım kaydı başarıyla eklendi.", saved);
    }
}
