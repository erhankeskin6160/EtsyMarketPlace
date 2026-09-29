namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
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
    private bool _autoFallbackStarted = false;

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

        // Yedek yol: gömülü tarayıcı bağlanamazsa gerçek Chrome ile giriş
        var btnOpenBrowser = new Button
        {
            Text = "Tarayıcıda Aç (yedek)",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(226, 232, 240),
            BackColor = Color.FromArgb(51, 65, 85),
            Size = new Size(150, 26),
            Location = new Point(372, 19),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnOpenBrowser.FlatAppearance.BorderSize = 0;
        btnOpenBrowser.Click += async (_, _) => await OpenInRealBrowserFallbackAsync();
        _topBar.Controls.Add(btnOpenBrowser);

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

            // 1b. İstek başlıklarını dinle (Authorization: Bearer ...) ve login isteğinden
            //     kimlik bilgilerini yakala (otomatik yenileme için şifreli saklanır).
            _webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

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

                    AppLog.Warn(
                        "ShipEntegra gömülü tarayıcı sayfayı yükleyemedi (WebView2: " + e.WebErrorStatus + "); gerçek tarayıcı yedeği değerlendiriliyor.",
                        nameof(ShipEntegraEmbeddedLoginForm));

                    if (!_autoFallbackStarted && !_tokenCaptured)
                    {
                        _autoFallbackStarted = true;
                        _lblStatus.Text = "Gömülü tarayıcı bağlanamadı — gerçek tarayıcı otomatik açılıyor...";
                        _lblStatus.ForeColor = Color.FromArgb(251, 191, 36); // Amber
                        BeginInvoke(new Action(async () =>
                        {
                            await Task.Delay(1500);
                            await OpenInRealBrowserFallbackAsync();
                        }));
                    }

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
        // Bazı makinelerde gömülü tarayıcının DNS çözümlemesi app.shipentegra.com için takılabiliyor
        // (ERR_NAME_NOT_RESOLVED). Alan adlarını Cloudflare kenar IP'lerine sabitleyerek bu katmanı atlarız.
        var envOptions = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--host-resolver-rules=\"MAP app.shipentegra.com 172.66.146.49, MAP api.shipentegra.com 172.66.146.49\" --no-proxy-server --disable-quic"
        };

        var createTask = CoreWebView2Environment.CreateAsync(null, folder, envOptions);
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

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_tokenCaptured)
        {
            return;
        }

        try
        {
            string uri = e.Request.Uri;
            if (!uri.Contains("shipentegra", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 1) Token taşıyan başlıkları tara (Authorization: Bearer ... gibi)
            foreach (var requestHeader in e.Request.Headers)
            {
                string headerValue = requestHeader.Value ?? string.Empty;
                if (headerValue.Length > 0 && headerValue.Length < 2000)
                {
                    string? token = ShipEntegraTokenExtractor.ExtractTokenFromAny(headerValue);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        string captured = token!;
                        BeginInvoke(new Action(async () => await CompleteLoginSuccessAsync(captured, null)));
                        return;
                    }
                }
            }

            // 2) Login isteğinden kimlik bilgilerini yakala (sonraki oturumlar otomatik yenilenebilsin)
            if (uri.EndsWith("/v1/auth/login", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _ = TryCaptureLoginCredentialsAsync(e);
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "ShipEntegraEmbeddedLoginForm.OnWebResourceRequested");
        }
    }

    private async Task TryCaptureLoginCredentialsAsync(CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var content = e.Request.Content;
            if (content == null)
            {
                return;
            }

            using var reader = new StreamReader(content);
            string body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string email = root.TryGetProperty("email", out var em) ? em.GetString() ?? string.Empty : string.Empty;
            string password = root.TryGetProperty("password", out var pw) ? pw.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var settings = ShipEntegraSettingsStore.Load();
            settings.SavedEmail = email;
            settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
            ShipEntegraSettingsStore.Save(settings);
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "ShipEntegraEmbeddedLoginForm.TryCaptureLoginCredentialsAsync");
        }
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
                if (stream is null)
                {
                    return;
                }

                using var reader = new StreamReader(stream);
                string body = await reader.ReadToEndAsync();

                var (accessToken, refreshToken) = ShipEntegraTokenExtractor.ExtractFromText(body);
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    await CompleteLoginSuccessAsync(accessToken!, refreshToken);
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
            // Tüm yerel depoları dökümle ve token çıkarıcıdan geçir (v4.public ve JWT biçimlerini tanır).
            const string script = @"(() => { try { const o={}; for(let i=0;i<localStorage.length;i++){const k=localStorage.key(i);const v=localStorage.getItem(k);if(v&&v.length<8000)o['L:'+k]=v;} for(let i=0;i<sessionStorage.length;i++){const k=sessionStorage.key(i);const v=sessionStorage.getItem(k);if(v&&v.length<8000)o['S:'+k]=v;} return JSON.stringify(o);} catch(e){return '';} })()";

            string dump = await _webView.ExecuteScriptAsync(script);
            if (!string.IsNullOrWhiteSpace(dump) && dump != "null" && dump.Length > 4)
            {
                var (access, refresh) = ShipEntegraTokenExtractor.ExtractFromText(dump);
                if (!string.IsNullOrWhiteSpace(access))
                {
                    await CompleteLoginSuccessAsync(access!, refresh);
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught, "ShipEntegraEmbeddedLoginForm.CheckStorageTokenAsync"); }
    }

    private async Task OpenInRealBrowserFallbackAsync()
    {
        try
        {
            _lblStatus.Text = "Gerçek tarayıcı açılıyor; lütfen ShipEntegra hesabınıza giriş yapın...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var sessionManager = new PuppeteerShippingSessionManager();
            string? token = await sessionManager.RefreshShipEntegraTokenAsync(
                null,
                null,
                showBrowser: true,
                statusCallback: message =>
                {
                    if (!IsDisposed)
                    {
                        Invoke(new Action(() => _lblStatus.Text = message));
                    }
                });

            if (!string.IsNullOrWhiteSpace(token))
            {
                await CompleteLoginSuccessAsync(token);
            }
            else
            {
                _lblStatus.Text = "Tarayıcıdan token alınamadı. Pencereyi kapatıp tekrar deneyin.";
                _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Tarayıcı yedeği başarısız: {ex.Message}";
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
        }
    }

    private async Task CompleteLoginSuccessAsync(string token, string? refreshToken = null)
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
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            settings.RefreshToken = refreshToken;
            settings.RefreshTokenLastUpdatedUtc = DateTime.UtcNow;
        }

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
