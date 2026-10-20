namespace EtsyMarketPlace.Application.Features.Tracking.Commands.SaveTrackingCapture;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Tracking;
using MediatR;

public class SaveTrackingCaptureHandler(
    ITrackingRepository repository
) : IRequestHandler<SaveTrackingCaptureRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveTrackingCaptureRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.Capture == null)
            return ServiceResponse.CreateError("Takip görüntüsü boş olamaz.", HttpStatusCode.BadRequest);

        var item = await repository.SaveCaptureAsync(request.Capture, cancellationToken);
        return ServiceResponse.CreateSuccess("Takip görüntüsü başarıyla kaydedildi.", item);
    }
}
