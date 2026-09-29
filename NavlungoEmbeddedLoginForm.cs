namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

/// <summary>
/// Navlungo oturumunu uygulama içinde güvenle açan, son kullanıcının teknik detaylarla (token, devtools, f12)
/// uğraşmasını engelleyen ve oturumu (id_token + oturum çerezleri) arka planda kendiliğinden yakalayıp
/// kaydeden gömülü WebView2 formu. ShipEntegra gömülü giriş formuyla aynı desendedir.
/// </summary>
internal sealed class NavlungoEmbeddedLoginForm : Form
{
    private const string LoginUrl = "https://ship.navlungo.com/login";
    private const string CalculatorUrl = "https://quick-price-calculator.navlungo.com/tr?source=user";

    public string? CapturedToken { get; private set; }

    private readonly WebView2 _webView = new();
    private readonly Label _lblTitle = new();
    private readonly Label _lblStatus = new();
    private readonly Panel _topBar = new();
    private readonly System.Windows.Forms.Timer _storageCheckTimer = new();
    private bool _tokenCaptured = false;
    private bool _autoFallbackStarted = false;

    public NavlungoEmbeddedLoginForm()
    {
        Text = "Navlungo - Güvenli Oturum Açma";
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

        _lblTitle.Text = "Navlungo Giriş Paneli";
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

        // Periyodik token/çerez tarama zamanlayıcısı (kullanıcı giriş yaptığında anında yakalar)
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
                "WebView2_Navlungo");

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
                    "Navlungo gömülü tarayıcısı ilk denemede açılamadı (profil kilitli olabilir); taze profil ile yeniden denenecek.",
                    nameof(NavlungoEmbeddedLoginForm));

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

            // 1b. İstek başlıklarını dinle (Authorization: Bearer ***) ve login isteğinden
            //     kimlik bilgilerini yakala (otomatik yenileme için şifreli saklanır).
            _webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

            // 1c. Yeni pencere istekleri (ör. dış bağlantılar) gömülü tarayıcıda açılsın
            _webView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (!_tokenCaptured && !string.IsNullOrWhiteSpace(e.Uri))
                {
                    _webView.CoreWebView2.Navigate(e.Uri);
                }
            };

            // 2. Sayfa yönlendirmelerini dinle
            _webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (_tokenCaptured)
                {
                    return;
                }

                if (e.IsSuccess)
                {
                    _lblStatus.Text = "Lütfen Navlungo hesabınızla giriş yapın.";
                    _lblStatus.ForeColor = Color.FromArgb(56, 189, 248); // Sky 400
                    _storageCheckTimer.Start();
                }
                else
                {
                    _lblStatus.Text = "Navlungo sayfasına bağlanılamadı. İnternet bağlantınızı kontrol edin.";
                    _lblStatus.ForeColor = Color.FromArgb(239, 68, 68); // Red

                    AppLog.Warn(
                        "Navlungo gömülü tarayıcı sayfayı yükleyemedi (WebView2: " + e.WebErrorStatus + "); gerçek tarayıcı yedeği değerlendiriliyor.",
                        nameof(NavlungoEmbeddedLoginForm));

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

            _webView.CoreWebView2.Navigate(LoginUrl);
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
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser(LoginUrl);
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
        // Bu makinede devre dışı bırakılmış kurumsal proxy WebView2'yi yavaşlatabiliyor;
        // --no-proxy-server ile doğrudan bağlantı kullanılır.
        var envOptions = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--no-proxy-server --disable-background-networking"
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
            if (!uri.Contains("navlungo", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 1) Token taşıyan başlıkları tara (Authorization: Bearer *** gibi)
            foreach (var requestHeader in e.Request.Headers)
            {
                string headerValue = requestHeader.Value ?? string.Empty;
                if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    string bearer = headerValue.Substring(7).Trim();
                    if (bearer.Length >= 40)
                    {
                        string captured = bearer;
                        BeginInvoke(new Action(async () => await CompleteCaptureAsync(captured)));
                        return;
                    }
                }
            }

            // 2) Login isteğinden kimlik bilgilerini yakala (sonraki oturumlar otomatik yenilenebilsin)
            if (string.Equals(e.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                (uri.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                 uri.Contains("auth", StringComparison.OrdinalIgnoreCase)))
            {
                _ = TryCaptureLoginCredentialsAsync(e);
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.OnWebResourceRequested");
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
            if (string.IsNullOrWhiteSpace(body) || body.Length > 20000)
            {
                return;
            }

            string? email = null;
            string? password = null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string name = prop.Name.ToLowerInvariant();
                        if (name is "email" or "useremail" or "mail" or "username")
                        {
                            email = prop.Value.GetString();
                        }
                        else if (name is "password" or "pass" or "pwd")
                        {
                            password = prop.Value.GetString();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // form-urlencoded gövde olabilir: email=...&password=...
                var emailMatch = Regex.Match(body, @"(?:email|useremail|mail)=([^&]+)", RegexOptions.IgnoreCase);
                var passMatch = Regex.Match(body, @"(?:password|pass|pwd)=([^&]+)", RegexOptions.IgnoreCase);
                if (emailMatch.Success)
                {
                    email = Uri.UnescapeDataString(emailMatch.Groups[1].Value);
                }
                if (passMatch.Success)
                {
                    password = Uri.UnescapeDataString(passMatch.Groups[1].Value);
                }
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
                !email.Contains('@'))
            {
                return;
            }

            var settings = NavlungoSettingsStore.Load();
            settings.SavedEmail = email;
            settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
            NavlungoSettingsStore.Save(settings);
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.TryCaptureLoginCredentialsAsync");
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
            if (uri.Contains("navlungo", StringComparison.OrdinalIgnoreCase) && e.Response.StatusCode == 200)
            {
                using var stream = await e.Response.GetContentAsync();
                if (stream is null)
                {
                    return;
                }

                using var reader = new StreamReader(stream);
                string body = await reader.ReadToEndAsync();
                if (body.Length > 200000)
                {
                    return;
                }

                string? token = NavlungoSessionTokenExtractor.TryExtractIdToken(body);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    await CompleteCaptureAsync(token);
                }
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.OnWebResourceResponseReceived");
        }
    }

    private async Task CheckStorageTokenAsync()
    {
        if (_tokenCaptured || _webView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            // localStorage + sessionStorage içindeki oturum alanlarını dökümle (Navlungo SPA oturumu burada tutar).
            const string script = @"(() => { try { const o={}; for(let i=0;i<localStorage.length;i++){const k=localStorage.key(i);const v=localStorage.getItem(k);if(v&&v.length<8000)o['ls:'+k]=v;} for(let j=0;j<sessionStorage.length;j++){const k2=sessionStorage.key(j);const v2=sessionStorage.getItem(k2);if(v2&&v2.length<8000)o['ss:'+k2]=v2;} return JSON.stringify(o);} catch(e){ return ''; } })()";
            string dump = await _webView.ExecuteScriptAsync(script);
            if (!string.IsNullOrWhiteSpace(dump) && dump != "null" && dump.Length > 4)
            {
                string? token = NavlungoSessionTokenExtractor.TryExtractIdToken(dump);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    await CompleteCaptureAsync(token);
                    return;
                }
            }

            // Çerezlerde oturum oluştu mu? (id_token çerezi veya oturum çerezleri)
            var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://ship.navlungo.com");
            if (cookies == null || cookies.Count == 0)
            {
                return;
            }

            var idTokenCookie = cookies.FirstOrDefault(c => c.Name.Equals("id_token", StringComparison.OrdinalIgnoreCase));
            if (idTokenCookie != null && !string.IsNullOrWhiteSpace(idTokenCookie.Value))
            {
                await CompleteCaptureAsync(idTokenCookie.Value);
                return;
            }

            string currentUrl = _webView.CoreWebView2.Source ?? string.Empty;
            bool navigatedAwayFromLogin = !currentUrl.Contains("/login", StringComparison.OrdinalIgnoreCase) &&
                                          !currentUrl.Contains("/giris", StringComparison.OrdinalIgnoreCase) &&
                                          !currentUrl.Contains("/auth", StringComparison.OrdinalIgnoreCase);
            bool hasSessionCookie = cookies.Any(c =>
                c.Name.Contains("_SessionUser_", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("nv_attr", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains("auth", StringComparison.OrdinalIgnoreCase));

            if (navigatedAwayFromLogin && hasSessionCookie)
            {
                await CompleteCaptureAsync(null);
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.CheckStorageTokenAsync");
        }
    }

    private async Task<string> CollectCookiesAsync()
    {
        var all = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Append(List<CoreWebView2Cookie>? cookies)
        {
            if (cookies == null)
            {
                return;
            }

            foreach (var cookie in cookies)
            {
                if (string.IsNullOrWhiteSpace(cookie.Name) || string.IsNullOrWhiteSpace(cookie.Value))
                {
                    continue;
                }

                if (seen.Add(cookie.Name))
                {
                    all.Add(cookie.Name + "=" + cookie.Value);
                }
            }
        }

        Append(await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://ship.navlungo.com"));
        Append(await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://quick-price-calculator.navlungo.com"));

        return string.Join("; ", all);
    }

    private async Task CompleteCaptureAsync(string? idToken)
    {
        if (_tokenCaptured)
        {
            return;
        }

        _tokenCaptured = true;
        _storageCheckTimer.Stop();
        CapturedToken = string.IsNullOrWhiteSpace(idToken) ? "navlungo_browser_session" : idToken;

        // 1) Mevcut çerezler (ship.navlungo.com)
        string cookieString = string.Empty;
        try
        {
            cookieString = await CollectCookiesAsync();
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.CompleteCaptureAsync.Collect");
        }

        // 2) Hesaplayıcı alanına bir kez gidip çerezleri eşitle (üye fiyat listesi bu oturumla gelir)
        try
        {
            _lblStatus.Text = "🌐 Oturum yakalandı, çerezler eşitleniyor...";
            _webView.CoreWebView2.Navigate(CalculatorUrl);
            await Task.Delay(3500);
            string syncedCookies = await CollectCookiesAsync();
            if (!string.IsNullOrWhiteSpace(syncedCookies))
            {
                cookieString = syncedCookies;
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "NavlungoEmbeddedLoginForm.CompleteCaptureAsync.Sync");
        }

        // 3) Ayarları kalıcı olarak kaydet
        var settings = NavlungoSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            settings.IdToken = idToken;
        }
        if (!string.IsNullOrWhiteSpace(cookieString))
        {
            settings.SessionCookie = cookieString;
        }
        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        NavlungoSettingsStore.Save(settings);

        // 4) UI güncelle
        void ApplySuccessUi()
        {
            _topBar.BackColor = Color.FromArgb(6, 78, 59); // Emerald 900
            _lblTitle.Text = "Giriş Başarılı!";
            _lblStatus.Text = "Navlungo oturumunuz bağlandı. Pencere kapatılıyor...";
            _lblStatus.ForeColor = Color.FromArgb(167, 243, 208); // Emerald 200
        }

        if (!IsDisposed)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApplySuccessUi));
            }
            else
            {
                ApplySuccessUi();
            }
        }

        await Task.Delay(1000);

        if (!IsDisposed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task OpenInRealBrowserFallbackAsync()
    {
        try
        {
            _lblStatus.Text = "Gerçek tarayıcı açılıyor; lütfen Navlungo hesabınıza giriş yapın...";
            _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            var settings = NavlungoSettingsStore.Load();
            string email = settings.SavedEmail ?? string.Empty;
            string pass = ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword);

            var sessionManager = new PuppeteerShippingSessionManager();
            string? token = await sessionManager.RefreshNavlungoTokenAsync(
                email,
                pass,
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
                // RefreshNavlungoTokenAsync ayarları (id_token + çerezler) diske kendisi kaydetmiştir.
                _tokenCaptured = true;
                _storageCheckTimer.Stop();
                CapturedToken = token;

                _topBar.BackColor = Color.FromArgb(6, 78, 59);
                _lblTitle.Text = "Giriş Başarılı!";
                _lblStatus.Text = "Navlungo oturumunuz bağlandı. Pencere kapatılıyor...";
                _lblStatus.ForeColor = Color.FromArgb(167, 243, 208);

                await Task.Delay(1000);
                if (!IsDisposed)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            else
            {
                _lblStatus.Text = "Tarayıcıdan oturum alınamadı. Pencereyi kapatıp tekrar deneyin.";
                _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Tarayıcı yedeği başarısız: {ex.Message}";
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
        }
    }
}
