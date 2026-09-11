namespace EtsyMarketPlace.Application.ShopVault.Services;

using System.IO.Compression;
using System.Text.Json;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;

public sealed class ShopVaultPackagerService
{
    private readonly IShopVaultRepository _repository;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ShopVaultPackagerService(IShopVaultRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ExportSessionToArchiveAsync(
        string sessionId,
        string? targetZipFilePath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetSessionByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Yedek oturumu bulunamadı: {sessionId}");

        var listings = await _repository.GetListingsBySessionIdAsync(sessionId, cancellationToken);

        string sessionFolder = VaultPathHelper.GetSessionFolder(sessionId);
        string defaultFileName = $"EtsyVault_{session.ShopName}_{session.CreatedAtUtc:yyyyMMdd_HHmmss}.etsyvault";
        string exportPath = targetZipFilePath ?? Path.Combine(VaultPathHelper.ExportsFolder, defaultFileName);

        var exportDir = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrWhiteSpace(exportDir))
        {
            Directory.CreateDirectory(exportDir);
        }

        if (File.Exists(exportPath))
        {
            File.Delete(exportPath);
        }

        progress?.Report("Arşiv paketi oluşturuluyor...");

        // Write manifest and listings metadata inside session folder temporarily
        string manifestJsonPath = Path.Combine(sessionFolder, "manifest.json");
        string listingsJsonPath = Path.Combine(sessionFolder, "listings.json");

        await File.WriteAllTextAsync(manifestJsonPath, JsonSerializer.Serialize(session, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(listingsJsonPath, JsonSerializer.Serialize(listings, JsonOptions), cancellationToken);

        progress?.Report("Görseller ve meta veriler sıkıştırılıyor...");

        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(sessionFolder, exportPath, CompressionLevel.Optimal, false);
        }, cancellationToken);

        // Update session with archive path
        session.ArchiveZipPath = exportPath;
        var fileInfo = new FileInfo(exportPath);
        session.TotalSizeBytes = fileInfo.Length;
        await _repository.SaveSessionAsync(session, cancellationToken);

        progress?.Report($"Arşiv başarıyla oluşturuldu: {Path.GetFileName(exportPath)} ({fileInfo.Length / (1024 * 1024.0):N1} MB)");

        return exportPath;
    }

    public async Task<VaultBackupSession> ImportArchiveAsync(
        string archiveFilePath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archiveFilePath))
        {
            throw new FileNotFoundException("İçe aktarılacak arşiv dosyası bulunamadı.", archiveFilePath);
        }

        progress?.Report("Arşiv dosyası açılıyor...");

        string newSessionId = Guid.NewGuid().ToString("N");
        string targetFolder = VaultPathHelper.GetSessionFolder(newSessionId);

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(archiveFilePath, targetFolder, true);
        }, cancellationToken);

        string manifestJsonPath = Path.Combine(targetFolder, "manifest.json");
        string listingsJsonPath = Path.Combine(targetFolder, "listings.json");

        if (!File.Exists(manifestJsonPath) || !File.Exists(listingsJsonPath))
        {
            throw new InvalidOperationException("Geçersiz arşiv paketi! manifest.json veya listings.json eksik.");
        }

        progress?.Report("Meta veriler veri tabanına kaydediliyor...");

        var sessionJson = await File.ReadAllTextAsync(manifestJsonPath, cancellationToken);
        var listingsJson = await File.ReadAllTextAsync(listingsJsonPath, cancellationToken);

        var session = JsonSerializer.Deserialize<VaultBackupSession>(sessionJson, JsonOptions)
            ?? throw new InvalidOperationException("Yedek oturum meta verisi okunamadı.");

        var listings = JsonSerializer.Deserialize<List<VaultListing>>(listingsJson, JsonOptions)
            ?? throw new InvalidOperationException("Listing verileri okunamadı.");

        // Re-assign new sessionId to avoid ID collisions
        string oldSessionId = session.SessionId;
        session.SessionId = newSessionId;
        session.ArchiveZipPath = archiveFilePath;
        session.LocalFolderPath = targetFolder;

        foreach (var l in listings)
        {
            l.SessionId = newSessionId;
            foreach (var img in l.Images)
            {
                img.Id = Guid.NewGuid().ToString("N");
                if (!string.IsNullOrWhiteSpace(img.LocalRelativePath) && img.LocalRelativePath.Contains(oldSessionId))
                {
                    img.LocalRelativePath = img.LocalRelativePath.Replace(oldSessionId, newSessionId);
                }
            }
            foreach (var v in l.Variations)
            {
                v.Id = Guid.NewGuid().ToString("N");
            }
        }

        await _repository.SaveSessionAsync(session, cancellationToken);
        await _repository.SaveListingsAsync(listings, cancellationToken);

        progress?.Report($"İçe aktarma tamamlandı: {session.ShopName} ({listings.Count} ürün)");

        return session;
    }
}
