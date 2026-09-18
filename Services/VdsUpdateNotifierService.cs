namespace SimilarProductsWinForms.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Http.Headers;
using System.Reflection;

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
        string DownloadUrl,
        string RemoteCommitSha = "",
        string LocalCommitSha = "");

    public static string GetCurrentCommitHash()
    {
        try
        {
            var infoVer = typeof(VdsUpdateNotifierService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrEmpty(infoVer) && infoVer.Contains('+'))
            {
                var commit = infoVer.Split('+')[1].Trim();
                if (commit.Length >= 7)
                {
                    return commit;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var apiUrl = $"https://api.github.com/repos/erhankeskin6160/EtsyMarketPlace/releases/tags/dev-latest?t={timestamp}";
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };

            using var response = await HttpClient.SendAsync(request, ct);
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

            string remoteCommit = string.Empty;
            if (root.TryGetProperty("target_commitish", out var targetProp))
            {
                remoteCommit = targetProp.GetString()?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(remoteCommit) && root.TryGetProperty("body", out var bodyProp))
            {
                var body = bodyProp.GetString() ?? string.Empty;
                var match = System.Text.RegularExpressions.Regex.Match(body, @"\*\*Commit:\*\*\s*([a-fA-F0-9]{7,40})");
                if (match.Success)
                {
                    remoteCommit = match.Groups[1].Value.Trim();
                }
            }

            var localCommit = GetCurrentCommitHash();
            const string downloadUrl = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe";

            // 1. ÖNCELİKLİ VE KESİN KONTROL: Git Commit Hash Karşılaştırması
            if (!string.IsNullOrEmpty(localCommit) && !string.IsNullOrEmpty(remoteCommit))
            {
                bool isSameCommit = remoteCommit.StartsWith(localCommit, StringComparison.OrdinalIgnoreCase) ||
                                     localCommit.StartsWith(remoteCommit, StringComparison.OrdinalIgnoreCase);

                if (!isSameCommit)
                {
                    return new UpdateCheckResult(true, "dev-latest", effectiveTime, downloadUrl, remoteCommit, localCommit);
                }
                else
                {
                    return new UpdateCheckResult(false, "dev-latest", effectiveTime, string.Empty, remoteCommit, localCommit);
                }
            }

            // 2. FALLBACK KONTROL: Dosya Yazılma Zamanı ve Boyut Karşılaştırması
            var currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentExePath) && File.Exists(currentExePath))
            {
                var localFileInfo = new FileInfo(currentExePath);
                var localWriteTime = new DateTimeOffset(localFileInfo.LastWriteTimeUtc, TimeSpan.Zero);
                long localSize = localFileInfo.Length;

                bool isNewer = effectiveTime > localWriteTime.AddSeconds(2);
                bool isDifferentSize = remoteAssetSize > 10_000_000 && Math.Abs(remoteAssetSize - localSize) > 256;

                if (isNewer || isDifferentSize)
                {
                    return new UpdateCheckResult(true, "dev-latest", effectiveTime, downloadUrl, remoteCommit, localCommit);
                }
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
                // Standalone Client Modu: 'deploy' klasörü olmayan istemci bilgisayarlarda
                // dinamik bir updater betiği oluşturup çalıştırarak güncellemeyi tamamla.
                var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                {
                    return false;
                }

                var pid = Process.GetCurrentProcess().Id;
                var scriptPath = Path.Combine(Path.GetTempPath(), $"etsy_client_autoupdate_{pid}.bat");
                const string downloadUrl = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe";

                var script = $@"@echo off
chcp 65001 >nul
title EtsyMarketPlace Otomatik Guncelleyici
echo [1/3] Programin kapanmasi bekleniyor (PID: {pid})...
:waitloop
tasklist /FI ""PID eq {pid}"" 2>NUL | find /I ""{pid}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo [2/3] Yeni surum indiriliyor...
set ""TARGET={currentExe}""
set ""TEMP_DL={currentExe}.download""

powershell -NoProfile -ExecutionPolicy Bypass -Command ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13; try {{ Import-Module BitsTransfer -ErrorAction SilentlyContinue; Start-BitsTransfer -Source '{downloadUrl}' -Destination '$env:TEMP_DL' -DisplayName 'EtsyMarketPlace-Update' -Priority Foreground }} catch {{ (New-Object System.Net.WebClient).DownloadFile('{downloadUrl}', '$env:TEMP_DL') }}""

if exist ""%TEMP_DL%"" (
    echo [3/3] Dosyalar guncelleniyor...
    move /y ""%TEMP_DL%"" ""%TARGET%"" >nul
    start """" ""%TARGET%""
) else (
    echo Hata: Guncelleme dosyasi indirilemedi!
    pause
)
del ""%~f0"" & exit
";
                File.WriteAllText(scriptPath, script, System.Text.Encoding.GetEncoding(1254));

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    UseShellExecute = true
                });
                return true;
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

    public static event Action<UpdateCheckResult>? UpdateDetected;
    public static event Action<UpdateCheckResult>? UpdateStatusChecked;

    public static void StopPeriodicAutoUpdater()
    {
        try
        {
            _autoUpdateCts?.Cancel();
            _autoUpdateCts?.Dispose();
            _autoUpdateCts = null;
        }
        catch { }
    }

    public static void StartPeriodicAutoUpdater(TimeSpan checkInterval, Action<string>? onStatusChanged = null)
    {
        if (_autoUpdateCts != null) return; // Already running

        _autoUpdateCts = new CancellationTokenSource();
        var ct = _autoUpdateCts.Token;

        Task.Run(async () =>
        {
            // İlk kontrolü program açıldıktan 4 saniye sonra yap
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), ct);
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
                    UpdateStatusChecked?.Invoke(result);

                    if (result.IsUpdateAvailable)
                    {
                        UpdateDetected?.Invoke(result);

                        var pubTimeStr = result.PublishedAt != DateTimeOffset.MinValue 
                            ? result.PublishedAt.LocalDateTime.ToString("HH:mm") 
                            : "";
                        onStatusChanged?.Invoke($"⚡ Yeni publish algılandı ({pubTimeStr})");

                        // Sadece sunucu ortamında (deploy/Guncelle_Ve_Baslat.bat varsa) unattended restart yap
                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var vdsBat = Path.Combine(baseDir, "deploy", "Guncelle_Ve_Baslat.bat");
                        if (File.Exists(vdsBat) && !_isUpdating)
                        {
                            _isUpdating = true;
                            await Task.Delay(TimeSpan.FromSeconds(2), ct);
                            bool started = TriggerVdsUpdateAndRestart();
                            if (started)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
                                Environment.Exit(0);
                                return;
                            }
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

                // Belirtilen aralık kadar bekle
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
