namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Engines;
using SimilarProductsWinForms.Studio.Services;

internal sealed record BatchInputItem(
    string Id,
    string Title,
    byte[] ImageBytes,
    string? OriginalPathOrUrl = null,
    long? TargetListingId = null);

internal sealed record BatchItemResult(
    string Id,
    string Title,
    Bitmap OriginalImage,
    Bitmap? EditedImage,
    bool Success,
    string? ErrorMessage,
    long ElapsedMs,
    long? TargetListingId = null);

internal sealed record BatchProgressReport(
    int CurrentIndex,
    int TotalCount,
    string StatusMessage,
    BatchItemResult? LastCompletedItem);

/// <summary>
/// Çoklu fotoğraflar için toplu AI arka plan değiştirme kuyruğu ve işleme servisi.
/// Hem yerel klasör görsellerini hem de Etsy mağazası aktif listing fotoğraflarını destekler.
/// </summary>
internal static class BatchBackgroundChangeService
{
    private static readonly HttpClient ImageClient = new();

    /// <summary>
    /// Etsy Listing görsellerinden URL üzerinden BatchInputItem listesi hazırlar.
    /// </summary>
    public static async Task<List<BatchInputItem>> LoadItemsFromEtsyUrlsAsync(
        List<(long ListingId, string Title, string ImageUrl)> listingImages,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var items = new List<BatchInputItem>();
        int count = 0;

        foreach (var (listingId, title, url) in listingImages)
        {
            ct.ThrowIfCancellationRequested();
            count++;
            progress?.Report($"Görseller indiriliyor [{count}/{listingImages.Count}]: {title}...");

            try
            {
                byte[] bytes = await ImageClient.GetByteArrayAsync(url, ct);
                items.Add(new BatchInputItem(
                    Id: $"etsy_{listingId}_{count}",
                    Title: title,
                    ImageBytes: bytes,
                    OriginalPathOrUrl: url,
                    TargetListingId: listingId));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Listing görseli indirilemedi ({url}): {ex.Message}");
            }
        }

        return items;
    }

    /// <summary>
    /// Yerel dosya yollarından BatchInputItem listesi hazırlar.
    /// </summary>
    public static async Task<List<BatchInputItem>> LoadItemsFromLocalFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken ct = default)
    {
        var items = new List<BatchInputItem>();
        int idx = 0;

        foreach (var path in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;

            idx++;
            byte[] bytes = await File.ReadAllBytesAsync(path, ct);
            items.Add(new BatchInputItem(
                Id: $"local_{idx}",
                Title: Path.GetFileNameWithoutExtension(path),
                ImageBytes: bytes,
                OriginalPathOrUrl: path));
        }

        return items;
    }

    /// <summary>
    /// Toplu arka plan değiştirme kuyruğunu çalıştırır.
    /// </summary>
    public static async Task<List<BatchItemResult>> RunBatchAsync(
        List<BatchInputItem> items,
        string backgroundPrompt,
        int engineChoice, // 0 = GPT-2.5 Flare, 1 = GPT-2.5 Sunburst, 2 = Gemini, 3 = PhotoRoom
        AiOptimizationSettings aiSettings,
        int maxParallel = 2,
        IProgress<BatchProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        if (items.Count == 0) return [];

        var results = new ConcurrentBag<BatchItemResult>();
        int completedCount = 0;
        using var semaphore = new SemaphoreSlim(Math.Max(1, maxParallel));

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            var sw = Stopwatch.StartNew();
            BatchItemResult result;

            try
            {
                ct.ThrowIfCancellationRequested();

                using var msOrig = new MemoryStream(item.ImageBytes);
                var originalBmp = new Bitmap(msOrig);

                var (success, editedBmp, err) = await ProcessSingleImageAsync(
                    item.ImageBytes,
                    originalBmp,
                    backgroundPrompt,
                    engineChoice,
                    aiSettings,
                    ct);

                sw.Stop();
                result = new BatchItemResult(
                    Id: item.Id,
                    Title: item.Title,
                    OriginalImage: new Bitmap(originalBmp),
                    EditedImage: editedBmp != null ? new Bitmap(editedBmp) : null,
                    Success: success,
                    ErrorMessage: err,
                    ElapsedMs: sw.ElapsedMilliseconds,
                    TargetListingId: item.TargetListingId);
            }
            catch (Exception ex)
            {
                sw.Stop();
                using var msOrig = new MemoryStream(item.ImageBytes);
                result = new BatchItemResult(
                    Id: item.Id,
                    Title: item.Title,
                    OriginalImage: new Bitmap(msOrig),
                    EditedImage: null,
                    Success: false,
                    ErrorMessage: ex.Message,
                    ElapsedMs: sw.ElapsedMilliseconds,
                    TargetListingId: item.TargetListingId);
            }
            finally
            {
                semaphore.Release();
            }

            results.Add(result);
            int cur = Interlocked.Increment(ref completedCount);
            progress?.Report(new BatchProgressReport(
                CurrentIndex: cur,
                TotalCount: items.Count,
                StatusMessage: $"[{cur}/{items.Count}] '{item.Title}' tamamlandı ({(result.Success ? "✅ Başarılı" : "❌ Hata")})",
                LastCompletedItem: result));
        });

        await Task.WhenAll(tasks);

        // Orijinal sıraya göre sırala
        var orderMap = items.Select((it, i) => (it.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        return results.OrderBy(r => orderMap.TryGetValue(r.Id, out int idx) ? idx : 0).ToList();
    }

    private static async Task<(bool Success, Bitmap? ResultImage, string ErrorMessage)> ProcessSingleImageAsync(
        byte[] imageBytes,
        Bitmap originalBmp,
        string backgroundPrompt,
        int engineChoice,
        AiOptimizationSettings aiSettings,
        CancellationToken ct)
    {
        switch (engineChoice)
        {
            case 0: // OpenAI GPT-Image-2.5 Flare (Hızlı / Toplu)
            case 1: // OpenAI GPT-Image-2.5 Sunburst (Yüksek Kalite / Vitrin)
            {
                string model = engineChoice == 1 ? "gpt-image-2.5-sunburst" : "gpt-image-2.5-flare";
                string quality = engineChoice == 1 ? "xhigh" : "high";

                // Eğer PhotoRoom API anahtarı varsa mask oluşturup OpenAI Edit'e verelim
                byte[]? maskBytes = null;
                if (!string.IsNullOrWhiteSpace(aiSettings.PhotoRoomApiKey))
                {
                    try
                    {
                        var maskRes = await BackgroundMaskService.CreateCutoutAndMaskWithPhotoRoomAsync(imageBytes, aiSettings.PhotoRoomApiKey, ct);
                        if (maskRes.Success && maskRes.MaskPngBytes != null)
                        {
                            maskBytes = maskRes.MaskPngBytes;
                        }
                    }
                    catch { }
                }

                return await AiImageGenerationService.EditWithOpenAiAsync(
                    imageBytes,
                    backgroundPrompt,
                    aiSettings.OpenAiApiKey,
                    maskBytes,
                    model: model,
                    quality: quality,
                    size: "2048x2048",
                    cancellationToken: ct);
            }

            case 2: // Google Gemini Image / Visual Grounding
            {
                var geminiEngine = new GoogleGeminiEngine();
                var req = new ImageEngineRequest
                {
                    InputImage = originalBmp,
                    PreserveProduct = true,
                    Prompt = backgroundPrompt,
                    ProcessMode = "ai_background"
                };
                var res = await geminiEngine.ProcessAsync(req, ct);
                return (res.Success, res.ResultImage, res.ErrorMessage);
            }

            case 3: // PhotoRoom Native AI Background
            default:
            {
                return await PhotoRoomApiService.EditProductPhotoAsync(
                    imageBytes,
                    aiSettings.PhotoRoomApiKey,
                    mode: "ai_background",
                    bgPrompt: backgroundPrompt,
                    shadowMode: "ai_soft",
                    padding: 0.08,
                    cancellationToken: ct);
            }
        }
    }

    /// <summary>
    /// Başarıyla düzenlenen görselleri yerel bir klasöre toplu olarak kaydeder.
    /// </summary>
    public static async Task<int> ExportResultsToFolderAsync(
        IEnumerable<BatchItemResult> results,
        string targetDirectory,
        string format = "png",
        CancellationToken ct = default)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        int count = 0;
        foreach (var item in results.Where(r => r.Success && r.EditedImage != null))
        {
            ct.ThrowIfCancellationRequested();
            string safeTitle = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = item.Id;

            string ext = format.ToLowerInvariant() == "jpeg" || format.ToLowerInvariant() == "jpg" ? "jpg" : "png";
            string filePath = Path.Combine(targetDirectory, $"{safeTitle}_ai_bg.{ext}");

            var imgFormat = ext == "jpg" ? ImageFormat.Jpeg : ImageFormat.Png;
            item.EditedImage!.Save(filePath, imgFormat);
            count++;
        }

        return await Task.FromResult(count);
    }

    /// <summary>
    /// Düzenlenen görselleri doğrudan hedef Etsy Listing'lerine yeni fotoğraf olarak yükler.
    /// </summary>
    public static async Task<(int SuccessCount, int FailedCount, List<string> Errors)> UploadResultsToEtsyAsync(
        IEnumerable<BatchItemResult> results,
        EtsyApiClient apiClient,
        EtsyApiSettings etsySettings,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int success = 0;
        int failed = 0;
        var errors = new List<string>();

        var validItems = results.Where(r => r.Success && r.EditedImage != null && r.TargetListingId is > 0).ToList();

        for (int i = 0; i < validItems.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = validItems[i];
            progress?.Report($"Etsy'ye yükleniyor [{i + 1}/{validItems.Count}]: '{item.Title}'...");

            string tempFile = Path.Combine(Path.GetTempPath(), $"etsy_upload_{item.TargetListingId}_{Guid.NewGuid():N}.png");
            try
            {
                item.EditedImage!.Save(tempFile, ImageFormat.Png);
                await apiClient.UploadOwnShopListingImageAsync(etsySettings, item.TargetListingId!.Value, tempFile, rank: null, ct);
                success++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Listing #{item.TargetListingId} ({item.Title}): {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        return (success, failed, errors);
    }
}
