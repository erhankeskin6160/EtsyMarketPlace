namespace EtsyMarketPlace.Domain.ShopVault.Entities;

public sealed class VaultBackupSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public long ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public int TotalListingsCount { get; set; }
    public int TotalImagesCount { get; set; }
    public long TotalSizeBytes { get; set; }

    public string LocalFolderPath { get; set; } = string.Empty;
    public string? ArchiveZipPath { get; set; }
    public string Status { get; set; } = "Incomplete"; // Completed, InProgress, Failed
    public string? ErrorMessage { get; set; }

    public VaultBackupSession() { }

    public VaultBackupSession(long shopId, string shopName, string shopUrl = "")
    {
        ShopId = shopId;
        ShopName = shopName;
        ShopUrl = shopUrl;
        CreatedAtUtc = DateTime.UtcNow;
        Status = "InProgress";
    }

    public void MarkCompleted(int listingsCount, int imagesCount, long sizeBytes, string? zipPath = null)
    {
        TotalListingsCount = listingsCount;
        TotalImagesCount = imagesCount;
        TotalSizeBytes = sizeBytes;
        ArchiveZipPath = zipPath;
        CompletedAtUtc = DateTime.UtcNow;
        Status = "Completed";
    }

    public void MarkFailed(string error)
    {
        ErrorMessage = error;
        CompletedAtUtc = DateTime.UtcNow;
        Status = "Failed";
    }
}
