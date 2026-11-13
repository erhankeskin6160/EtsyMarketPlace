namespace EtsyMarketPlace.Application.Features.Listings.Images;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public sealed class GetListingImagesQuery : IRequest<ServiceResponse>
{
    public string ShopId { get; init; } = string.Empty;
    public long ListingId { get; init; }
}

public sealed class GetListingImagesQueryHandler(
    IEtsyListingApiClient apiClient
) : IRequestHandler<GetListingImagesQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetListingImagesQuery request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.ListingId <= 0 || string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("Geçersiz mağaza veya ilan kimliği.", HttpStatusCode.BadRequest);

        var rawJson = await apiClient.GetListingImagesRawJsonAsync(request.ListingId, request.ShopId, cancellationToken);
        var images = EtsyListingImageJsonParser.ParseImages(rawJson);

        return ServiceResponse.CreateSuccess("İlan görselleri getirildi.", images);
    }
}

