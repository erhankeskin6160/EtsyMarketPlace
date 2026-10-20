namespace EtsyMarketPlace.Application.Features.Listings.Commands.AiOptimizeListing;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class AiOptimizeListingHandler(
    IAiListingOptimizerUseCase aiOptimizer
) : IRequestHandler<AiOptimizeListingRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(AiOptimizeListingRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await aiOptimizer.OptimizeListingAsync(request.ListingId, request.ShopId, request.Command, cancellationToken);
        return ServiceResponse.CreateSuccess("AI optimizasyon tamamlandı.", result);
    }
}
