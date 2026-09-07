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

            if (!root.TryGetProperty("published_at", out var pubProp) ||
                !DateTimeOffset.TryParse(pubProp.GetString(), out var publishedAt))
            {
                return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
            }

            // Mevcut çalışan EXE'nin derlenme / yazılma zamanı
            var currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            {
                return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
            }

            var localWriteTime = new DateTimeOffset(File.GetLastWriteTimeUtc(currentExePath), TimeSpan.Zero);

            // Eğer GitHub'daki release, yerel exe'den en az 2 dakika daha yeniyse güncelleme var demektir
            if (publishedAt > localWriteTime.AddMinutes(2))
            {
                string downloadUrl = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe";
                return new UpdateCheckResult(true, "dev-latest", publishedAt, downloadUrl);
            }
        }
        catch
        {
            // Ağ hatası veya GitHub rate-limit durumunda sessizce yut
        }

        return new UpdateCheckResult(false, string.Empty, DateTimeOffset.MinValue, string.Empty);
    }

    public static void TriggerVdsUpdateAndRestart()
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
            }
            else
            {
                // Doğrudan GitHub releases sayfasını aç
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/tag/dev-latest",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update trigger error: {ex.Message}");
        }
    }
}
