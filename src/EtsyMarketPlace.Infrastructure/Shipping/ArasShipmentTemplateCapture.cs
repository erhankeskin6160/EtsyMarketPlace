namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
using EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Aras panelinde kullanıcının yaptığı gerçek bir işlemin ağ trafiğini kaydeder.
/// Amaç: API'nin beklediği <strong>gerçek istek gövdesini</strong> birebir görmek.
/// Bizim uygulamamız CreateShipment için 400 "volumetricweightismissing" alırken
/// panelin kendi isteği başarılı oluyorsa, iki gövdeyi karşılaştırmak sorunu kesin çözer.
/// </summary>
public sealed class ArasShipmentTemplateCapture
{
    private const string PortalUrl = "https://panel.arasglobalcargo.com";

    /// <summary>Yakalanacak uç noktalar (URL parçası).</summary>
    private static readonly string[] TargetUrlParts =
    {
        "CreateShipment",
        "StartCargoProviderBasePriceCalculation",
        "CalculateShipmentPrice"
    };

    public string OutputDirectory { get; }

    public ArasShipmentTemplateCapture(string? outputDirectory = null)
    {
        OutputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms",
            "captures");
    }

    public sealed class CapturedExchange
    {
        public string CapturedAtUtc { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public string RequestBody { get; set; } = string.Empty;
        public int? HttpStatus { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tarayıcıyı açar ve hedef uç noktalara giden istekleri kaydeder.
    /// Kullanıcı panelde işlemi tamamladığında veya süre dolduğunda dosya yazılır.
    /// Dönen değer: yazılan JSON dosyasının tam yolu (kayıt yoksa null).
    /// </summary>
    public async Task<string?> CaptureAsync(
        bool showBrowser = true,
        TimeSpan? maxWait = null,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        string? browserPath = VisualBrowserAgentService.ResolveInstalledBrowserPath();
        if (string.IsNullOrEmpty(browserPath))
        {
            statusCallback?.Invoke("Yüklü Chrome veya Edge bulunamadı.");
            return null;
        }

        var exchanges = new List<CapturedExchange>();
        var gate = new object();
        string profileDir = PuppeteerShippingSessionManager.GetProfileDirectory("ArasGlobal");

        var launchOptions = new LaunchOptions
        {
            Headless = !showBrowser,
            ExecutablePath = browserPath,
            UserDataDir = profileDir,
            IgnoredDefaultArgs = new[] { "--enable-automation" },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1280,900",
                "--remote-debugging-port=0"
            },
            DefaultViewport = showBrowser ? null : new ViewPortOptions { Width = 1280, Height = 900 }
        };

        IBrowser? browser = null;

        try
        {
            browser = await Puppeteer.LaunchAsync(launchOptions);
            var pages = await browser.PagesAsync();
            var page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();

            // İstek gövdelerini yakala
            page.Request += (_, e) =>
            {
                try
                {
                    string url = e.Request.Url ?? string.Empty;
                    if (!TargetUrlParts.Any(p => url.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    var entry = new CapturedExchange
                    {
                        CapturedAtUtc = DateTime.UtcNow.ToString("O"),
                        Method = e.Request.Method.ToString(),
                        Url = url,
                        RequestBody = e.Request.PostData ?? string.Empty
                    };

                    foreach (var header in e.Request.Headers)
                    {
                        // Güvenlik: yetki başlığındaki token kısaltılarak saklanır.
                        string value = header.Value ?? string.Empty;
                        if (header.Key.Equals("authorization", StringComparison.OrdinalIgnoreCase) && value.Length > 24)
                        {
                            value = value[..16] + "...(" + value.Length + " karakter)";
                        }

                        entry.RequestHeaders[header.Key] = value;
                    }

                    lock (gate)
                    {
                        exchanges.Add(entry);
                    }

                    statusCallback?.Invoke($"İstek yakalandı: {e.Request.Method} {Shorten(url)}");
                }
                catch
                {
                }
            };

            // Yanıt gövdelerini eşleştir
            page.Response += async (_, e) =>
            {
                try
                {
                    string url = e.Response.Url ?? string.Empty;
                    if (!TargetUrlParts.Any(p => url.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    string body = string.Empty;
                    try
                    {
                        body = await e.Response.TextAsync();
                    }
                    catch
                    {
                    }

                    CapturedExchange? target;
                    lock (gate)
                    {
                        target = exchanges.LastOrDefault(x =>
                            string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase) &&
                            x.HttpStatus == null);
                    }

                    if (target != null)
                    {
                        target.HttpStatus = (int)e.Response.Status;
                        target.ResponseBody = Truncate(body, 20000);
                    }

                    statusCallback?.Invoke($"Yanıt yakalandı: {(int)e.Response.Status} {Shorten(url)}");
                }
                catch
                {
                }
            };

            statusCallback?.Invoke("Panel açılıyor. Lütfen gönderi işlemini panelden tamamlayın...");
            await page.GoToAsync(PortalUrl + "/auth", new NavigationOptions { Timeout = 45000 });

            TimeSpan limit = maxWait ?? TimeSpan.FromMinutes(12);
            var started = DateTime.UtcNow;

            while (!ct.IsCancellationRequested && DateTime.UtcNow - started < limit)
            {
                bool foundCreateShipment;
                int count;
                lock (gate)
                {
                    count = exchanges.Count;
                    foundCreateShipment = exchanges.Any(x =>
                        x.Url.Contains("CreateShipment", StringComparison.OrdinalIgnoreCase));
                }

                if (foundCreateShipment)
                {
                    // Yanıtın da gelmesi için kısa bir bekleme
                    await Task.Delay(2500, CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                statusCallback?.Invoke($"Dinleniyor... yakalanan istek: {count} (iptal için bekleyin)");
                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            List<CapturedExchange> snapshot;
            lock (gate)
            {
                snapshot = exchanges.ToList();
            }

            if (snapshot.Count == 0)
            {
                statusCallback?.Invoke("Hiçbir hedef istek yakalanamadı.");
                return null;
            }

            return WriteCapture(snapshot);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke("Yakalama hatası: " + ex.Message);
            return null;
        }
        finally
        {
            if (browser != null)
            {
                try
                {
                    await browser.CloseAsync();
                }
                catch
                {
                }

                try
                {
                    await browser.DisposeAsync();
                }
                catch
                {
                }
            }
        }
    }

    private string WriteCapture(List<CapturedExchange> exchanges)
    {
        Directory.CreateDirectory(OutputDirectory);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(OutputDirectory, $"aras-capture-{stamp}.json");

        var payload = new
        {
            note = "Aras panelinden yakalanan gercek istek/yanit ciftleri. Token degerleri kisaltilmistir.",
            capturedAtLocal = DateTime.Now.ToString("O"),
            count = exchanges.Count,
            exchanges
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));

        WriteComparison(path, exchanges);

        return path;
    }

    /// <summary>
    /// Uygulamanın son gönderdiği CreateShipment gövdesi ile panelden yakalanan gövdeyi karşılaştırır.
    /// </summary>
    private void WriteComparison(string capturePath, List<CapturedExchange> exchanges)
    {
        try
        {
            var panelCreate = exchanges.FirstOrDefault(x =>
                x.Url.Contains("CreateShipment", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.RequestBody));

            if (panelCreate == null)
            {
                return;
            }

            string lastRequestPath = Path.Combine(OutputDirectory, "last-createshipment-request.json");
            if (!File.Exists(lastRequestPath))
            {
                return;
            }

            string ours = File.ReadAllText(lastRequestPath);
            var diffs = ArasPayloadComparer.Compare(ours, panelCreate.RequestBody,
                new[] { "shipmentId", "referenceCode", "createdAt", "updatedAt", "date" });

            var sb = new StringBuilder();
            sb.AppendLine("Aras CreateShipment govde karsilastirmasi");
            sb.AppendLine("Bizim govde  : " + lastRequestPath);
            sb.AppendLine("Panel govdesi: " + capturePath);
            sb.AppendLine("Fark sayisi  : " + diffs.Count);
            sb.AppendLine(new string('-', 60));
            foreach (var diff in diffs)
            {
                sb.AppendLine(diff.ToString());
            }

            File.WriteAllText(capturePath + ".fark.txt", sb.ToString(), new UTF8Encoding(false));
        }
        catch
        {
        }
    }
    private static string Shorten(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        int idx = url.IndexOf("//", StringComparison.Ordinal);
        if (idx >= 0)
        {
            url = url[(idx + 2)..];
        }

        return url.Length <= 70 ? url : url[..70] + "…";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max] + "…(kısaltıldı)";
    }
}
