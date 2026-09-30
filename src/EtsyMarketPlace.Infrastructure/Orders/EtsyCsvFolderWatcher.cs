namespace EtsyMarketPlace.Infrastructure.Orders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Kullanıcının İndirilenler ve Masaüstü klasörlerini izleyerek Etsy'den indirilen
/// sipariş CSV dosyalarını buton dahi tıklatmadan otomatik yakalayıp SQLite'a aktaran servis.
/// </summary>
public sealed class EtsyCsvFolderWatcher : IDisposable
{
    private readonly IOrderAddressRepository _repository;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly HashSet<string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event Action<IReadOnlyList<OrderAddressRecord>, string>? AddressesAutoImported;

    public EtsyCsvFolderWatcher(IOrderAddressRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Klasör izleyicilerini başlatır ve mevcut klasörlerdeki en son Etsy CSV'lerini hemen tarar.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var targetFolders = GetCandidateFolders();

        foreach (var folder in targetFolders)
        {
            if (!Directory.Exists(folder)) continue;

            try
            {
                // Mevcut en son dosyaları tara
                await ScanFolderAsync(folder, cancellationToken);

                // Canlı dosya indirme izleyicisi oluştur
                var watcher = new FileSystemWatcher(folder)
                {
                    Filter = "*.csv",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnFileEvent;
                watcher.Changed += OnFileEvent;
                _watchers.Add(watcher);
            }
            catch
            {
                // Klasör izin kısıtlamalarını sessizce tolere et
            }
        }
    }

    private static List<string> GetCandidateFolders()
    {
        var folders = new List<string>();

        // 1. İndirilenler (Downloads)
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(userProfile, "Downloads");
        if (Directory.Exists(downloads)) folders.Add(downloads);

        // 2. Masaüstü (Desktop)
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (Directory.Exists(desktop)) folders.Add(desktop);

        // 3. Uygulama çalışma dizini
        string current = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current) && !folders.Contains(current))
        {
            folders.Add(current);
        }

        return folders;
    }

    private async Task ScanFolderAsync(string folder, CancellationToken cancellationToken)
    {
        try
        {
            var files = Directory.GetFiles(folder, "*Etsy*.csv")
                .Concat(Directory.GetFiles(folder, "*Order*.csv"))
                .Distinct()
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(5)
                .ToList();

            foreach (var file in files)
            {
                await TryProcessFileAsync(file, isInitialScan: true, cancellationToken);
            }
        }
        catch
        {
            // İzin hatalarını tolere et
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!e.FullPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return;

        // Dosya adında etsy veya order geçiyorsa veya yeni indiyse
        string fileName = Path.GetFileName(e.FullPath).ToLowerInvariant();
        if (!fileName.Contains("etsy") && !fileName.Contains("order") && !fileName.Contains("receipt"))
        {
            return;
        }

        Task.Run(async () =>
        {
            // Tarayıcının dosyayı diske yazmasının tamamlanması için kısa bir süre bekle
            await Task.Delay(800);
            await TryProcessFileAsync(e.FullPath, isInitialScan: false, CancellationToken.None);
        });
    }

    public async Task<int> TryProcessFileAsync(string filePath, bool isInitialScan, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_processedFiles.Contains(filePath)) return 0;
        }

        if (!File.Exists(filePath)) return 0;

        try
        {
            // Dosyanın yazımı bitmiş mi kontrol et
            if (!IsFileReady(filePath))
            {
                await Task.Delay(600, cancellationToken);
                if (!IsFileReady(filePath)) return 0;
            }

            var records = EtsyOrderCsvParser.ParseFile(filePath, source: "EtsyCsvAuto");
            if (records.Count == 0) return 0;

            // Verileri SQLite'a kaydet
            await _repository.SaveBatchAsync(records, cancellationToken);

            lock (_lock)
            {
                _processedFiles.Add(filePath);
            }

            AddressesAutoImported?.Invoke(records, Path.GetFileName(filePath));
            return records.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsFileReady(string filename)
    {
        try
        {
            using var inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return inputStream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
    }
}
