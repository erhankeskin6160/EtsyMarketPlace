namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

/// <summary>
/// ShipEntegra oturumunu uygulama içinde güvenle açan, son kullanıcının teknik detaylarla (token, devtools, f12)
/// uğraşmasını engelleyen ve tokeni arka planda kendiliğinden yakalayıp kaydeden gömülü WebView2 formu.
/// </summary>
internal sealed class ShipEntegraEmbeddedLoginForm : Form
{
    public string? CapturedToken { get; private set; }

    private readonly WebView2 _webView = new();
    private readonly Label _lblTitle = new();
    private readonly Label _lblStatus = new();
    private readonly Panel _topBar = new();
    private readonly System.Windows.Forms.Timer _storageCheckTimer = new();
    private bool _tokenCaptured = false;

    public ShipEntegraEmbeddedLoginForm()
    {
        Text = "ShipEntegra - Güvenli Oturum Açma";
        Size = new Size(540, 740);
        MinimumSize = new Size(480, 600);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        // Üst Bilgi Barı
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 64;
        _topBar.BackColor = Color.FromArgb(30, 41, 59); // Slate 800
        _topBar.Padding = new Padding(16, 10, 16, 10);

        _lblTitle.Text = "ShipEntegra Giriş Paneli";
        _lblTitle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        _lblTitle.ForeColor = Color.FromArgb(241, 245, 249);
        _lblTitle.Location = new Point(16, 10);
        _lblTitle.AutoSize = true;
        _topBar.Controls.Add(_lblTitle);

        _lblStatus.Text = "Güvenli tarayıcı başlatılıyor, lütfen bekleyin...";
        _lblStatus.Font = new Font("Segoe UI", 8.5f);
        _lblStatus.ForeColor = Color.FromArgb(148, 163, 184); // Slate 400
        _lblStatus.Location = new Point(16, 34);
        _lblStatus.AutoSize = true;
        _topBar.Controls.Add(_lblStatus);

        Controls.Add(_topBar);

        // WebView2 Bileşeni
        _webView.Dock = DockStyle.Fill;
        _webView.BackColor = Color.FromArgb(15, 23, 42);
        Controls.Add(_webView);

        // Periyodik localStorage tarama zamanlayıcısı (kullanıcı giriş yaptığında anında yakalar)
        _storageCheckTimer.Interval = 1000;
        _storageCheckTimer.Tick += async (_, _) => await CheckStorageTokenAsync();

        Load += async (_, _) => await InitializeWebViewAsync();
        FormClosed += (_, _) =>
        {
            _storageCheckTimer.Stop();
            _storageCheckTimer.Dispose();
            _webView.Dispose();
        };
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EtsyMarketPlace",
                "WebView2_ShipEntegra");

            Directory.CreateDirectory(userDataFolder);

            // Kilitli/eski bir kullanıcı veri klasörü başlatmayı sonsuza kadar bekletebilir
            // (lockfile + arkada kalan msedgewebview2 süreçleri). Bu yüzden hem zaman aşımı
            // koyuyoruz hem de ilk deneme takılırsa TAZE bir profille bir kez daha deniyoruz.
            CoreWebView2Environment env;
            try
            {
                env = await CreateEnvironmentWithTimeoutAsync(userDataFolder, TimeSpan.FromSeconds(20));
            }
            catch (TimeoutException)
            {
                AppLog.Warn(
                    "ShipEntegra gömülü tarayıcısı ilk denemede açılamadı (profil kilitli olabilir); taze profil ile yeniden denenecek.",
                    nameof(ShipEntegraEmbeddedLoginForm));

                userDataFolder = userDataFolder + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                Directory.CreateDirectory(userDataFolder);
                env = await CreateEnvironmentWithTimeoutAsync(userDataFolder, TimeSpan.FromSeconds(20));
            }

            await EnsureWithTimeoutAsync(env, TimeSpan.FromSeconds(25));

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // 1. Ağ yanıtlarını dinle (login/API yanıtlarındaki tokeni yakalar)
            _webView.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;

            // 2. Sayfa yönlendirmelerini dinle
            _webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                {
                    _lblStatus.Text = "Lütfen ShipEntegra hesabınızla giriş yapın.";
                    _lblStatus.ForeColor = Color.FromArgb(56, 189, 248); // Sky 400
                    _storageCheckTimer.Start();
                }
                else
                {
                    _lblStatus.Text = "ShipEntegra sayfasına bağlanılamadı. İnternet bağlantınızı kontrol edin.";
                    _lblStatus.ForeColor = Color.FromArgb(239, 68, 68); // Red
                }
            };

            _webView.CoreWebView2.Navigate("https://app.shipentegra.com/");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _lblStatus.Text = "Microsoft Edge WebView2 çalışma zamanı bulunamadı.";
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);

            var ask = MessageBox.Show(
                "Sisteminizde Microsoft Edge WebView2 bileşeni eksik.\n\nVarsayılan tarayıcınızda açmak ister misiniz?",
                "WebView2 Eksik",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (ask == DialogResult.Yes)
            {
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser("https://app.shipentegra.com/");
            }

            Close();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Tarayıcı başlatılamadı: {ex.Message}";
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
        }
    }

    /// <summary>Ortam oluşturmayı zaman aşımıyla sınırlar; kilitli profil sonsuza kadar beklemesin.</summary>
    private static async Task<CoreWebView2Environment> CreateEnvironmentWithTimeoutAsync(string folder, TimeSpan timeout)
    {
        var createTask = CoreWebView2Environment.CreateAsync(null, folder);
        var finished = await Task.WhenAny(createTask, Task.Delay(timeout));
        if (finished != createTask)
        {
            throw new TimeoutException($"Gömülü tarayıcı {timeout.TotalSeconds:0} saniyede başlatılamadı.");
        }

        return await createTask;
    }

    /// <summary>WebView2 çekirdeğini zaman aşımıyla başlatır.</summary>
    private async Task EnsureWithTimeoutAsync(CoreWebView2Environment env, TimeSpan timeout)
    {
        var ensureTask = _webView.EnsureCoreWebView2Async(env);
        var finished = await Task.WhenAny(ensureTask, Task.Delay(timeout));
        if (finished != ensureTask)
        {
            throw new TimeoutException($"Gömülü tarayıcı çekirdeği {timeout.TotalSeconds:0} saniyede hazır olmadı.");
        }

        await ensureTask;
    }

    private async void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_tokenCaptured)
        {
            return;
        }

        try
        {
            string uri = e.Request.Uri;
            if (uri.Contains("shipentegra", StringComparison.OrdinalIgnoreCase) && e.Response.StatusCode == 200)
            {
                using var stream = await e.Response.GetContentAsync();
                using var reader = new StreamReader(stream);
                string body = await reader.ReadToEndAsync();

                if (!string.IsNullOrWhiteSpace(body) && body.Contains("eyJ"))
                {
                    var match = Regex.Match(body, @"ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+");
                    if (match.Success)
                    {
                        string tokenCandidate = match.Value;
                        if (!JwtTokenInspector.IsExpired(tokenCandidate))
                        {
                            await CompleteLoginSuccessAsync(tokenCandidate);
                        }
                    }
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught, "ShipEntegraEmbeddedLoginForm.OnWebResourceResponseReceived"); }
    }

    private async Task CheckStorageTokenAsync()
    {
        if (_tokenCaptured || _webView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            // localStorage içindeki token alanlarını oku (ShipEntegra SPA tokeni burada tutar).
            string script = @"(() => {
                try {
                    const t = localStorage.getItem('token') || localStorage.getItem('access_token') || localStorage.getItem('auth_token') || localStorage.getItem('accessToken');
                    if (t && t.length > 25) return t;

                    const u = localStorage.getItem('user');
                    if (u) {
                        const parsed = JSON.parse(u);
                        if (parsed?.token?.accessToken) return parsed.token.accessToken;
                        if (parsed?.accessToken) return parsed.accessToken;
                    }
                } catch (e) { }
                return '';
            })()";

            string jsonResult = await _webView.ExecuteScriptAsync(script);
            if (!string.IsNullOrWhiteSpace(jsonResult) && jsonResult != "null" && jsonResult != "\"\"")
            {
                string rawToken = jsonResult.Trim('\"', ' ', '\\');
                if (rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    rawToken = rawToken.Substring(7).Trim();
                }

                if (rawToken.Length > 25)
                {
                    bool looksLikeJwt = rawToken.Contains("eyJ");
                    if (!looksLikeJwt || !JwtTokenInspector.IsExpired(rawToken))
                    {
                        await CompleteLoginSuccessAsync(rawToken);
                    }
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught, "ShipEntegraEmbeddedLoginForm.CheckStorageTokenAsync"); }
    }

    private async Task CompleteLoginSuccessAsync(string token)
    {
        if (_tokenCaptured)
        {
            return;
        }

        _tokenCaptured = true;
        _storageCheckTimer.Stop();

        CapturedToken = token;

        // Ayarları kalıcı olarak kaydet
        var settings = ShipEntegraSettingsStore.Load();
        settings.BearerToken = token;
        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        ShipEntegraSettingsStore.Save(settings);

        // UI Güncelle
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                _topBar.BackColor = Color.FromArgb(6, 78, 59); // Emerald 900
                _lblTitle.Text = "Giriş Başarılı!";
                _lblStatus.Text = "ShipEntegra oturumunuz bağlandı. Pencere kapatılıyor...";
                _lblStatus.ForeColor = Color.FromArgb(167, 243, 208); // Emerald 200
            }));
        }
        else
        {
            _topBar.BackColor = Color.FromArgb(6, 78, 59);
            _lblTitle.Text = "Giriş Başarılı!";
            _lblStatus.Text = "ShipEntegra oturumunuz bağlandı. Pencere kapatılıyor...";
            _lblStatus.ForeColor = Color.FromArgb(167, 243, 208);
        }

        await Task.Delay(1000);

        if (!IsDisposed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
