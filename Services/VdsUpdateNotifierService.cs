namespace SimilarProductsWinForms.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed class VdsUpdateNotifierService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static VdsUpdateNotifierService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "EtsyMarketPlace-VDS-Notifier");
    }

    public sealed record UpdateCheckResult(
        bool IsUpdateAvailable,
        string VersionTag,
        DateTimeOffset PublishedAt,
        string DownloadUrl);

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            const string apiUrl = "https://api.github.com/repos/erhankeskin6160/EtsyMarketPlace/releases/tags/dev-latest";
            using var response = await HttpClient.GetAsync(apiUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            DateTimeOffset effectiveTime = DateTimeOffset.MinValue;
            long remoteAssetSize = 0;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameProp) &&
                        nameProp.GetString()?.Equals("SimilarProductsWinForms.exe", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (asset.TryGetProperty("updated_at", out var updatedProp) &&
                            DateTimeOffset.TryParse(updatedProp.GetString(), out var assetUpdatedAt))
                        {
                            effectiveTime = assetUpdatedAt;
                        }

                        if (asset.TryGetProperty("size", out var sizeProp))
                        {
                            remoteAssetSize = sizeProp.GetInt64();
                        }
                        break;
                    }
                }
            }

            if (effectiveTime == DateTimeOffset.MinValue)
            {
                if (root.TryGetProperty("published_at", out var pubProp) &&
                    DateTimeOffset.TryParse(pubProp.GetString(), out var pubAt))
                {
                    effectiveTime = pubAt;
                }
            }

            // Mevcut çalışan EXE'nin derlenme / yazılma zamanı ve dosya boyutu
            var currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
            }

            var localFileInfo = new FileInfo(currentExePath);
            var localWriteTime = new DateTimeOffset(localFileInfo.LastWriteTimeUtc, TimeSpan.Zero);
            long localSize = localFileInfo.Length;

            // Eğer GitHub'daki asset dosyası yerel exe'den daha yeniyse (veya boyutu farklıysa) güncelleme var demektir
            bool isNewer = effectiveTime > localWriteTime.AddSeconds(10);
            bool isDifferentSize = remoteAssetSize > 10_000_000 && Math.Abs(remoteAssetSize - localSize) > 4096;

            if (isNewer || isDifferentSize)
            {
                string downloadUrl = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe";
                return new UpdateCheckResult(true, "dev-latest", effectiveTime, downloadUrl);
            }
        }
        catch
        {
            // Ağ hatası veya GitHub rate-limit durumunda sessizce yut
        }

        return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
    }

    public static bool TriggerVdsUpdateAndRestart()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterPs1 = Path.Combine(baseDir, "deploy", "vds_update.ps1");
            var updaterBat = Path.Combine(baseDir, "deploy", "Guncelle_Ve_Baslat.bat");

            if (File.Exists(updaterBat))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterBat,
                    WorkingDirectory = Path.GetDirectoryName(updaterBat),
                    UseShellExecute = true
                });
                return true;
            }
            else if (File.Exists(updaterPs1))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updaterPs1}\"",
                    WorkingDirectory = Path.GetDirectoryName(updaterPs1),
                    UseShellExecute = true
                });
                return true;
            }
            else
            {
                // Updater betiği bulunamazsa programı kapatma
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update trigger error: {ex.Message}");
            return false;
        }
    }

    private static CancellationTokenSource? _autoUpdateCts;
    private static bool _isUpdating = false;

    public static void StartPeriodicAutoUpdater(TimeSpan checkInterval, Action<string>? onStatusChanged = null)
    {
        if (_autoUpdateCts != null) return; // Already running

        _autoUpdateCts = new CancellationTokenSource();
        var ct = _autoUpdateCts.Token;

        Task.Run(async () =>
        {
            // İlk kontrolü program açıldıktan 5 saniye sonra yap
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await CheckForUpdateAsync(ct);
                    if (result.IsUpdateAvailable && !_isUpdating)
                    {
                        _isUpdating = true;
                        var pubTimeStr = result.PublishedAt.LocalDateTime.ToString("HH:mm:ss");
                        onStatusChanged?.Invoke($"⚡ Yeni publish algılandı ({pubTimeStr})! Güncelleme başlatılıyor...");

                        // Devam eden işlemlerin temiz kapanması için 2 saniye bekle
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);

                        bool started = TriggerVdsUpdateAndRestart();
                        if (started)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
                            Environment.Exit(0);
                            return;
                        }
                        else
                        {
                            _isUpdating = false;
                            onStatusChanged?.Invoke($"ℹ️ Yeni sürüm mevcut ({pubTimeStr}) - 'deploy/Guncelle_Ve_Baslat.bat' çalıştırabilirsiniz.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Auto-updater loop error: {ex.Message}");
                }

                // Belirtilen aralık kadar bekle (varsayılan 20 saniye)
                try
                {
                    await Task.Delay(checkInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }
}
