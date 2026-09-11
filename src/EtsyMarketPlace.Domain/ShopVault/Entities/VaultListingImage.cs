namespace EtsyMarketPlace.Domain.ShopVault.Entities;

public sealed class VaultListingImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long ListingId { get; set; }
    public long OriginalImageId { get; set; }
    public int Rank { get; set; } = 1;
    public string LocalRelativePath { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime DownloadedAtUtc { get; set; } = DateTime.UtcNow;

    public VaultListingImage() { }

    public VaultListingImage(
        long listingId,
        long originalImageId,
        int rank,
        string localRelativePath,
        string originalUrl,
        string? hexCode = null,
        long fileSizeBytes = 0,
        int width = 0,
        int height = 0)
    {
        ListingId = listingId;
        OriginalImageId = originalImageId;
        Rank = rank;
        LocalRelativePath = localRelativePath;
        OriginalUrl = originalUrl;
        HexCode = hexCode;
        FileSizeBytes = fileSizeBytes;
        Width = width;
        Height = height;
        DownloadedAtUtc = DateTime.UtcNow;
    }
}
