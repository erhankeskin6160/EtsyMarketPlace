namespace EtsyMarketPlace.Application.Features.Listings.Images;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using MediatR;

public sealed class SyncListingImagesCommand : IRequest<ServiceResponse>
{
    public string ShopId { get; init; } = string.Empty;
    public long ListingId { get; init; }
    public IReadOnlyList<SyncListingImageItem> Items { get; init; } = Array.Empty<SyncListingImageItem>();
}

public sealed class SyncListingImagesCommandHandler(
    IEtsyListingApiClient apiClient,
    IExifStripper exifStripper
) : IRequestHandler<SyncListingImagesCommand, ServiceResponse>
{
    private const int MaxEtsyListingImages = 10;

    public async Task<ServiceResponse> Handle(SyncListingImagesCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.ListingId <= 0 || string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("Geçersiz mağaza veya ilan kimliği.", HttpStatusCode.BadRequest);

        if (request.Items == null || request.Items.Count == 0)
            return ServiceResponse.CreateError("İlanda en az 1 görsel bulunmalıdır.", HttpStatusCode.BadRequest);

        if (request.Items.Count > MaxEtsyListingImages)
            return ServiceResponse.CreateError($"Etsy ilanlarında en fazla {MaxEtsyListingImages} görsel bulunabilir.", HttpStatusCode.BadRequest);

        var currentRaw = await apiClient.GetListingImagesRawJsonAsync(request.ListingId, request.ShopId, cancellationToken);
        var currentImages = EtsyListingImageJsonParser.ParseImages(currentRaw);
        var currentById = currentImages.ToDictionary(i => i.ListingImageId);

        var orderedItems = request.Items
            .OrderBy(i => i.Rank)
            .Select((item, idx) => item with { Rank = idx + 1 })
            .ToList();

        int uploadedCount = 0;
        int deletedCount = 0;
        int reorderedCount = 0;

        // 1. Önce yeni eklenen görselleri Anti-Ban (EXIF + pHash) temizliği ile yükle
        foreach (var item in orderedItems.Where(i => !i.ListingImageId.HasValue || i.ListingImageId.Value <= 0))
        {
            if (string.IsNullOrWhiteSpace(item.DataUrl))
                continue;

            var rawBytes = DecodeDataUrl(item.DataUrl);
            if (rawBytes == null || rawBytes.Length == 0)
                return ServiceResponse.CreateError("Yüklenen görsel formatı geçersiz.", HttpStatusCode.BadRequest);

            var cleanBytes = exifStripper.ProcessImage(rawBytes, stripExif: true, permutateHash: true);
            var fileName = string.IsNullOrWhiteSpace(item.FileName) ? $"listing_{request.ListingId}_{item.Rank}.jpg" : item.FileName;

            var (success, _) = await apiClient.UploadListingImageAsync(
                request.ShopId,
                request.ListingId,
                cleanBytes,
                fileName,
                item.Rank,
                cancellationToken);

            if (!success)
                return ServiceResponse.CreateError($"Yeni görsel (#{item.Rank}) Etsy'ye yüklenemedi.", HttpStatusCode.BadGateway);

            uploadedCount++;
        }

        // 2. Kaldırılan mevcut görselleri sil (yeni görseller yüklendikten sonra — son görsel silme hatasını önler)
        var keptIds = orderedItems
            .Where(i => i.ListingImageId.HasValue && i.ListingImageId.Value > 0)
            .Select(i => i.ListingImageId!.Value)
            .ToHashSet();

        foreach (var existing in currentImages)
        {
            if (!keptIds.Contains(existing.ListingImageId))
            {
                var deleted = await apiClient.DeleteListingImageAsync(
                    request.ShopId,
                    request.ListingId,
                    existing.ListingImageId,
                    cancellationToken);

                if (deleted)
                    deletedCount++;
            }
        }

        // 3. Sırası (rank) değişen mevcut görselleri güncelle
        foreach (var item in orderedItems.Where(i => i.ListingImageId.HasValue && i.ListingImageId.Value > 0))
        {
            var id = item.ListingImageId!.Value;
            if (currentById.TryGetValue(id, out var existing) && existing.Rank != item.Rank)
            {
                var updated = await apiClient.UpdateListingImageRankAsync(
                    request.ShopId,
                    request.ListingId,
                    id,
                    item.Rank,
                    cancellationToken);

                if (updated)
                    reorderedCount++;
            }
        }

        // 4. Güncel görsel listesini Etsy'den tekrar çek
        var refreshedRaw = await apiClient.GetListingImagesRawJsonAsync(request.ListingId, request.ShopId, cancellationToken);
        var refreshedImages = EtsyListingImageJsonParser.ParseImages(refreshedRaw);

        var result = new SyncListingImagesResult(
            ListingId: request.ListingId,
            Images: refreshedImages,
            UploadedCount: uploadedCount,
            DeletedCount: deletedCount,
            ReorderedCount: reorderedCount
        );

        return ServiceResponse.CreateSuccess("İlan görselleri Etsy'de başarıyla güncellendi.", result);
    }

    private static byte[]? DecodeDataUrl(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');
        var base64Part = commaIndex >= 0 ? dataUrl[(commaIndex + 1)..] : dataUrl;
        try
        {
            return Convert.FromBase64String(base64Part);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

