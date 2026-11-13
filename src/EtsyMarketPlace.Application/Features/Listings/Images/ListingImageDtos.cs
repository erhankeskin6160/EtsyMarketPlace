namespace EtsyMarketPlace.Application.Features.Listings.Images;

using System.Collections.Generic;
using System.Text.Json;

public sealed record ListingImageDetailDto(
    long ListingImageId,
    int Rank,
    string Url570xN,
    string UrlFullxfull,
    string Url75x75
);

public sealed record SyncListingImageItem(
    long? ListingImageId,
    string? DataUrl,
    string? FileName,
    int Rank
);

public sealed record SyncListingImagesResult(
    long ListingId,
    IReadOnlyList<ListingImageDetailDto> Images,
    int UploadedCount,
    int DeletedCount,
    int ReorderedCount
);

internal static class EtsyListingImageJsonParser
{
    public static List<ListingImageDetailDto> ParseImages(string? rawJson)
    {
        var list = new List<ListingImageDetailDto>();
        if (string.IsNullOrWhiteSpace(rawJson))
            return list;

        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in results.EnumerateArray())
        {
            long imageId = item.TryGetProperty("listing_image_id", out var idProp) && idProp.TryGetInt64(out var parsedId)
                ? parsedId
                : 0;
            int rank = item.TryGetProperty("rank", out var rankProp) && rankProp.TryGetInt32(out var parsedRank)
                ? parsedRank
                : list.Count + 1;

            string u570 = item.TryGetProperty("url_570xN", out var p570) ? p570.GetString() ?? "" : "";
            string uFull = item.TryGetProperty("url_fullxfull", out var pFull) ? pFull.GetString() ?? "" : "";
            string u75 = item.TryGetProperty("url_75x75", out var p75) ? p75.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(u570)) u570 = !string.IsNullOrWhiteSpace(uFull) ? uFull : u75;
            if (string.IsNullOrWhiteSpace(uFull)) uFull = u570;
            if (string.IsNullOrWhiteSpace(u75)) u75 = u570;

            if (imageId > 0 || !string.IsNullOrWhiteSpace(u570))
            {
                list.Add(new ListingImageDetailDto(imageId, rank, u570, uFull, u75));
            }
        }

        list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        return list;
    }
}

