namespace EtsyMarketPlace.Application.ShopVault.Services;

using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;

public sealed class ShopMigrationDeploymentService
{
    private readonly IEtsyVaultApiClient _apiClient;
    private readonly IAntiBanSanitizer _antiBanSanitizer;

    public ShopMigrationDeploymentService(
        IEtsyVaultApiClient apiClient,
        IAntiBanSanitizer antiBanSanitizer)
    {
        _apiClient = apiClient;
        _antiBanSanitizer = antiBanSanitizer;
    }

    public async Task<ShopMigrationResult> DeployListingsAsync(
        IReadOnlyList<VaultListing> listingsToDeploy,
        MigrationMappingProfile mapping,
        AntiBanSettings antiBanSettings,
        string? aiApiKey = null,
        IProgress<ShopMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int total = listingsToDeploy.Count;
        int processed = 0;
        int success = 0;
        int failed = 0;
        var deployedNewIds = new List<long>();
        var errors = new List<string>();

        progress?.Report(new ShopMigrationProgress("Transfer oturumu başlatılıyor...", 0, total, 0, 0, 0));

        foreach (var originalListing in listingsToDeploy)
        {
            if (cancellationToken.IsCancellationRequested) break;

            processed++;
            string shortTitle = originalListing.Title.Length > 30 ? originalListing.Title.Substring(0, 30) + "..." : originalListing.Title;
            progress?.Report(new ShopMigrationProgress($"İşleniyor ({processed}/{total}): {shortTitle}", processed - 1, total, success, failed, (double)(processed - 1) / total * 100, originalListing.Title));

            try
            {
                // 1. Anti-Ban Metadata Sanitization
                var sanitized = _antiBanSanitizer.SanitizeListingMetadata(originalListing, antiBanSettings);

                // 2. AI Rewriting if enabled
                if (antiBanSettings.RewriteTitleWithAi)
                {
                    sanitized.Title = await _antiBanSanitizer.RewriteTextWithAiAsync(sanitized.Title, true, aiApiKey, cancellationToken: cancellationToken);
                }
                if (antiBanSettings.RewriteDescriptionWithAi && !string.IsNullOrWhiteSpace(sanitized.Description))
                {
                    sanitized.Description = await _antiBanSanitizer.RewriteTextWithAiAsync(sanitized.Description, false, aiApiKey, cancellationToken: cancellationToken);
                }

                // 3. Apply Shipping & Return Policy Mapping
                if (sanitized.ShippingProfileId.HasValue && mapping.ShippingProfileMap.TryGetValue(sanitized.ShippingProfileId.Value, out var mappedShippingId))
                {
                    sanitized.ShippingProfileId = mappedShippingId;
                }
                else if (mapping.DefaultTargetShippingProfileId > 0)
                {
                    sanitized.ShippingProfileId = mapping.DefaultTargetShippingProfileId;
                }

                if (sanitized.ReturnPolicyId.HasValue && mapping.ReturnPolicyMap.TryGetValue(sanitized.ReturnPolicyId.Value, out var mappedReturnId))
                {
                    sanitized.ReturnPolicyId = mappedReturnId;
                }
                else if (mapping.DefaultTargetReturnPolicyId > 0)
                {
                    sanitized.ReturnPolicyId = mapping.DefaultTargetReturnPolicyId;
                }

                // 4. Create Draft Listing on Target Shop
                long newListingId = await _apiClient.CreateDraftListingAsync(sanitized, mapping.TargetShopId, cancellationToken);
                if (newListingId <= 0)
                {
                    throw new InvalidOperationException("Etsy taslak listing oluşturamadı.");
                }

                // 5. Sanitize and Upload Images with Rank
                var orderedImages = originalListing.Images.OrderBy(i => i.Rank).ToList();
                int currentRank = 1;

                foreach (var img in orderedImages)
                {
                    string fullLocalPath = VaultPathHelper.ResolveFullPath(img.LocalRelativePath);
                    if (File.Exists(fullLocalPath))
                    {
                        var rawBytes = await File.ReadAllBytesAsync(fullLocalPath, cancellationToken);
                        var cleanBytes = await _antiBanSanitizer.SanitizeImageAsync(
                            rawBytes,
                            antiBanSettings.StripExifMetadata,
                            antiBanSettings.PermutateImageHash,
                            cancellationToken);

                        string tempCleanPath = Path.Combine(Path.GetTempPath(), $"clean_upload_{Guid.NewGuid():N}.jpg");
                        try
                        {
                            await File.WriteAllBytesAsync(tempCleanPath, cleanBytes, cancellationToken);
                            await _apiClient.UploadListingImageAsync(mapping.TargetShopId, newListingId, tempCleanPath, currentRank, cancellationToken);
                        }
                        finally
                        {
                            try { File.Delete(tempCleanPath); } catch { }
                        }

                        currentRank++;
                    }
                }

                // 6. Connect Variations Inventory if available
                if (sanitized.Variations.Count > 0)
                {
                    try
                    {
                        await _apiClient.UpdateListingInventoryAsync(mapping.TargetShopId, newListingId, sanitized.Variations, cancellationToken);
                    }
                    catch
                    {
                        // Variation inventory update error should not block listing creation
                    }
                }

                success++;
                deployedNewIds.Add(newListingId);

                // 7. Throttle delay to mimic natural human behavior and prevent anti-bot bans
                if (processed < total && antiBanSettings.ThrottleDelaySeconds > 0)
                {
                    progress?.Report(new ShopMigrationProgress($"Bekleniyor ({antiBanSettings.ThrottleDelaySeconds} sn insan taklidi)...", processed, total, success, failed, (double)processed / total * 100, originalListing.Title));
                    await Task.Delay(antiBanSettings.ThrottleDelaySeconds * 1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                failed++;
                string err = $"Ürün #{originalListing.ListingId} ({shortTitle}) yüklenemedi: {ex.Message}";
                errors.Add(err);
                progress?.Report(new ShopMigrationProgress(err, processed, total, success, failed, (double)processed / total * 100, originalListing.Title, ex.Message));
            }
        }

        progress?.Report(new ShopMigrationProgress(
            $"Transfer tamamlandı! Başarılı: {success}, Başarısız: {failed}",
            total,
            total,
            success,
            failed,
            100));

        return new ShopMigrationResult(total, success, failed, deployedNewIds, errors);
    }
}

public sealed record ShopMigrationResult(
    int TotalProcessed,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<long> CreatedListingIds,
    IReadOnlyList<string> Errors);
