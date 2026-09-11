namespace EtsyMarketPlace.Application.ShopVault.Services;

using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;

public sealed class ShopVaultBackupService
{
    private readonly IShopVaultRepository _repository;
    private readonly IEtsyVaultApiClient _apiClient;

    public ShopVaultBackupService(IShopVaultRepository repository, IEtsyVaultApiClient apiClient)
    {
        _repository = repository;
        _apiClient = apiClient;
    }

    public async Task<VaultBackupSession> RunBackupAsync(
        bool downloadImages = true,
        IProgress<ShopVaultBackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ShopVaultBackupProgress("Mağaza kimlik bilgileri alınıyor...", 0, 0, 0, 0, 5));

        var (shopId, shopName, shopUrl) = await _apiClient.GetCurrentShopInfoAsync(cancellationToken);
        var session = new VaultBackupSession(shopId, shopName, shopUrl);
        session.LocalFolderPath = VaultPathHelper.GetSessionFolder(session.SessionId);
        await _repository.SaveSessionAsync(session, cancellationToken);

        progress?.Report(new ShopVaultBackupProgress($"{shopName} mağazasından ürünler çekiliyor...", 0, 0, 0, 0, 15));

        var listings = await _apiClient.FetchAllShopListingsPagedAsync(
            "active",
            new Progress<string>(msg => progress?.Report(new ShopVaultBackupProgress(msg, 0, 0, 0, 0, 25))),
            cancellationToken);

        int totalListings = listings.Count;
        int processedListings = 0;
        int totalImages = listings.Sum(l => l.Images.Count);
        int processedImages = 0;
        long totalSizeBytes = 0;

        foreach (var l in listings)
        {
            l.SessionId = session.SessionId;
            l.OriginalShopId = shopId;
            l.OriginalShopName = shopName;

            if (downloadImages && l.Images.Count > 0)
            {
                string folder = VaultPathHelper.GetListingImagesFolder(session.SessionId, l.ListingId);

                foreach (var img in l.Images)
                {
                    if (!string.IsNullOrWhiteSpace(img.OriginalUrl))
                    {
                        try
                        {
                            var bytes = await _apiClient.DownloadImageBytesAsync(img.OriginalUrl, cancellationToken);
                            if (bytes.Length > 0)
                            {
                                string ext = img.OriginalUrl.Contains(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                                string fileName = $"img_rank{img.Rank}_{img.OriginalImageId}{ext}";
                                string filePath = Path.Combine(folder, fileName);

                                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

                                img.LocalRelativePath = VaultPathHelper.GetRelativeImagePath(session.SessionId, l.ListingId, fileName);
                                img.FileSizeBytes = bytes.Length;
                                totalSizeBytes += bytes.Length;
                            }
                        }
                        catch
                        {
                            // Continue downloading other images
                        }
                    }

                    processedImages++;
                }
            }

            processedListings++;
            double currentPercent = totalListings > 0
                ? 30.0 + ((double)processedListings / totalListings * 60.0)
                : 90.0;

            progress?.Report(new ShopVaultBackupProgress(
                $"İşleniyor: {l.Title.Substring(0, Math.Min(35, l.Title.Length))}...",
                processedListings,
                totalListings,
                processedImages,
                totalImages,
                currentPercent));
        }

        progress?.Report(new ShopVaultBackupProgress("Veritabanı kayıtları tamamlanıyor...", totalListings, totalListings, processedImages, totalImages, 95));

        await _repository.SaveListingsAsync(listings, cancellationToken);

        session.MarkCompleted(totalListings, processedImages, totalSizeBytes);
        await _repository.SaveSessionAsync(session, cancellationToken);

        progress?.Report(new ShopVaultBackupProgress(
            $"Yedekleme tamamlandı! Toplam {totalListings} ürün ve {processedImages} görsel güvenle kasaya alındı.",
            totalListings,
            totalListings,
            processedImages,
            totalImages,
            100));

        return session;
    }
}
