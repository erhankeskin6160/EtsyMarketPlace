namespace EtsyMarketPlace.Application.Features.Tracking.Commands.SaveTrackingCapture;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Tracking;
using MediatR;

public class SaveTrackingCaptureRequest : IRequest<ServiceResponse>
{
    public TrackingCapture Capture { get; set; } = null!;
}
