namespace EtsyMarketPlace.Application.Features.Listings.Images;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public sealed record UpdateImageAltTextItem(
    long ListingImageId,
    int Rank,
    string AltText
);

public sealed class UpdateListingImagesAltTextCommand : IRequest<ServiceResponse>
{
    public string ShopId { get; init; } = string.Empty;
    public long ListingId { get; init; }
    public IReadOnlyList<UpdateImageAltTextItem> Items { get; init; } = Array.Empty<UpdateImageAltTextItem>();
}

public sealed class UpdateListingImagesAltTextCommandHandler(
    IEtsyListingApiClient apiClient
) : IRequestHandler<UpdateListingImagesAltTextCommand, ServiceResponse>
{
    private const int MaxAltTextLength = 250;

    public async Task<ServiceResponse> Handle(UpdateListingImagesAltTextCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.ListingId <= 0 || string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("Geçersiz mağaza veya ilan kimliği.", HttpStatusCode.BadRequest);

        if (request.Items == null || request.Items.Count == 0)
            return ServiceResponse.CreateError("Güncellenecek görsel bulunamadı.", HttpStatusCode.BadRequest);

        int updatedCount = 0;
        foreach (var item in request.Items)
        {
            if (item.ListingImageId <= 0)
                continue;

            var alt = (item.AltText ?? string.Empty).Trim();
            if (alt.Length > MaxAltTextLength)
                return ServiceResponse.CreateError($"Alt-Text en fazla {MaxAltTextLength} karakter olabilir (#{item.Rank}).", HttpStatusCode.BadRequest);

            var ok = await apiClient.UpdateListingImageMetadataAsync(
                request.ShopId,
                request.ListingId,
                item.ListingImageId,
                Math.Max(1, item.Rank),
                alt,
                cancellationToken);

            if (ok)
                updatedCount++;
        }

        var refreshedRaw = await apiClient.GetListingImagesRawJsonAsync(request.ListingId, request.ShopId, cancellationToken);
        var refreshedImages = EtsyListingImageJsonParser.ParseImages(refreshedRaw);

        return ServiceResponse.CreateSuccess($"{updatedCount} görselin Alt-Text bilgisi Etsy'de güncellendi.", new
        {
            listingId = request.ListingId,
            updatedCount,
            images = refreshedImages
        });
    }
}

