namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
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
/// Aras Global oturumunu uygulama içinde güvenle açan, son kullanıcının teknik detaylarla (token, devtools, f12)
/// uğraşmasını engelleyen ve tokeni arka planda kendiliğinden yakalayıp kaydeden gömülü WebView2 formu.
/// </summary>
internal sealed class ArasGlobalEmbeddedLoginForm : Form
{
    public string? CapturedToken { get; private set; }

    private readonly WebView2 _webView = new();
    private readonly Label _lblTitle = new();
    private readonly Label _lblStatus = new();
    private readonly Panel _topBar = new();
    private readonly System.Windows.Forms.Timer _storageCheckTimer = new();
    private bool _tokenCaptured = false;

    public ArasGlobalEmbeddedLoginForm()
    {
        Text = "Aras Global - Güvenli Oturum Açma";
        Size = new Size(540, 740);
        MinimumSize = new Size(480, 600);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        Icon = Icon;

        // Üst Bilgi Barı
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 64;
        _topBar.BackColor = Color.FromArgb(30, 41, 59); // Slate 800
        _topBar.Padding = new Padding(16, 10, 16, 10);

        _lblTitle.Text = "🚚 Aras Global Giriş Paneli";
        _lblTitle.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        _lblTitle.ForeColor = Color.FromArgb(241, 245, 249);
        _lblTitle.Location = new Point(16, 10);
        _lblTitle.AutoSize = true;
        _topBar.Controls.Add(_lblTitle);

        _lblStatus.Text = "⏳ Güvenli tarayıcı başlatılıyor, lütfen bekleyin...";
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
                "WebView2_ArasGlobal");

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
                    "Aras gömülü tarayıcısı ilk denemede açılamadı (profil kilitli olabilir); taze profil ile yeniden denenecek.",
                    nameof(ArasGlobalEmbeddedLoginForm));

                userDataFolder = userDataFolder + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                Directory.CreateDirectory(userDataFolder);
                env = await CreateEnvironmentWithTimeoutAsync(userDataFolder, TimeSpan.FromSeconds(20));
            }

            await EnsureWithTimeoutAsync(env, TimeSpan.FromSeconds(25));

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // 1. Giden API isteklerini dinle (Authorization: Bearer eyJ... başlığı ve /Auth/Login kimlik bilgileri)
            _webView.CoreWebView2.AddWebResourceRequestedFilter("*arasglobalcargo*", CoreWebView2WebResourceContext.All);
            _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

            // 2. Ağ yanıtlarını dinle (API /Auth/Login veya /Auth/GenerateAccessTokenFromRefreshToken döndüğünde)
            _webView.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;

            // 3. Sayfa yönlendirmelerini dinle
            _webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                {
                    _lblStatus.Text = "🔑 Lütfen telefon numaranız ve şifrenizle giriş yapın.";
                    _lblStatus.ForeColor = Color.FromArgb(56, 189, 248); // Sky 400
                    _storageCheckTimer.Start();
                }
                else
                {
                    _lblStatus.Text = "⚠️ Aras Global sayfasına bağlanılamadı. İnternet bağlantınızı kontrol edin.";
                    _lblStatus.ForeColor = Color.FromArgb(239, 68, 68); // Red
                }
            };

            _webView.CoreWebView2.Navigate("https://panel.arasglobalcargo.com/auth");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _lblStatus.Text = "❌ Microsoft Edge WebView2 çalışma zamanı bulunamadı.";
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);

            var ask = MessageBox.Show(
                "Sisteminizde Microsoft Edge WebView2 bileşeni eksik.\n\nVarsayılan tarayıcınızda açmak ister misiniz?",
                "WebView2 Eksik",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (ask == DialogResult.Yes)
            {
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser("https://panel.arasglobalcargo.com/auth");
            }
            Close();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Tarayıcı başlatılamadı: {ex.Message}";
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

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_tokenCaptured) return;

        try
        {
            string uri = e.Request.Uri ?? string.Empty;
            if (!uri.Contains("arasglobalcargo", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 1) Giden istek başlıklarında Authorization: Bearer eyJ... kontrolü
            foreach (var header in e.Request.Headers)
            {
                string val = header.Value ?? string.Empty;
                if (val.Contains("eyJ", StringComparison.Ordinal))
                {
                    var match = Regex.Match(val, @"ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+");
                    if (match.Success && !JwtTokenInspector.IsExpired(match.Value))
                    {
                        string captured = match.Value;
                        BeginInvoke(new Action(async () => await CompleteLoginSuccessAsync(captured)));
                        return;
                    }
                }
            }

            // 2) /Auth/Login isteğinden kullanıcı adı ve şifreyi yakala
            if (string.Equals(e.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase))
            {
                _ = TryCaptureLoginCredentialsAsync(e);
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "ArasGlobalEmbeddedLoginForm.OnWebResourceRequested");
        }
    }

    private async Task TryCaptureLoginCredentialsAsync(CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var content = e.Request.Content;
            if (content == null) return;

            using var reader = new StreamReader(content);
            string body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body) || body.Length > 10000) return;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            string? user = null;
            string? pass = null;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String) continue;
                string name = prop.Name.ToLowerInvariant();
                if (name is "username" or "email" or "phone" or "phonenumber" or "gsm")
                {
                    user = prop.Value.GetString();
                }
                else if (name is "password" or "pass" or "pwd")
                {
                    pass = prop.Value.GetString();
                }
            }

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
            {
                var settings = ArasGlobalSettingsStore.Load();
                settings.SavedEmail = user.Trim();
                settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
                ArasGlobalSettingsStore.Save(settings);
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "ArasGlobalEmbeddedLoginForm.TryCaptureLoginCredentialsAsync");
        }
    }

    private async void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_tokenCaptured) return;

        try
        {
            string uri = e.Request.Uri;
            if (uri.Contains("api.arasglobalcargo.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("arasglobalcargo", StringComparison.OrdinalIgnoreCase))
            {
                if (e.Response.StatusCode == 200)
                {
                    using var stream = await e.Response.GetContentAsync();
                    using var reader = new StreamReader(stream);
                    string body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrWhiteSpace(body) && body.Contains("eyJ"))
                    {
                        var (access, refresh) = ExtractTokensFromJsonOrRegex(body);
                        if (!string.IsNullOrWhiteSpace(access) && !JwtTokenInspector.IsExpired(access))
                        {
                            await CompleteLoginSuccessAsync(access, refresh);
                        }
                    }
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught, "ArasGlobalEmbeddedLoginForm.OnWebResourceResponseReceived"); }
    }

    private static (string? AccessToken, string? RefreshToken) ExtractTokensFromJsonOrRegex(string body)
    {
        string? access = null;
        string? refresh = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                if (payload.TryGetProperty("token", out var tokElem))
                {
                    if (tokElem.ValueKind == JsonValueKind.String)
                    {
                        access = tokElem.GetString();
                    }
                    else if (tokElem.ValueKind == JsonValueKind.Object)
                    {
                        if (tokElem.TryGetProperty("accessToken", out var at) && at.ValueKind == JsonValueKind.String)
                            access = at.GetString();
                        if (tokElem.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String)
                            refresh = rt.GetString();
                    }
                }
                if (string.IsNullOrWhiteSpace(access) && payload.TryGetProperty("accessToken", out var pAt) && pAt.ValueKind == JsonValueKind.String)
                {
                    access = pAt.GetString();
                }
                if (string.IsNullOrWhiteSpace(refresh) && payload.TryGetProperty("refreshToken", out var pRt) && pRt.ValueKind == JsonValueKind.String)
                {
                    refresh = pRt.GetString();
                }
            }
        }
        catch
        {
            // JSON parse edilemezse regex ile devam et
        }

        if (string.IsNullOrWhiteSpace(access))
        {
            var match = Regex.Match(body, @"ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+");
            if (match.Success)
            {
                access = match.Value;
            }
        }

        return (access, refresh);
    }

    private async Task CheckStorageTokenAsync()
    {
        if (_tokenCaptured || _webView.CoreWebView2 == null) return;

        try
        {
            // localStorage ve sessionStorage içindeki token, refresh_token ve user objesini oku
            const string script = @"(() => {
                try {
                    const jwtRe = /ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/;
                    let access = localStorage.getItem('token') || localStorage.getItem('accessToken') || '';
                    let refresh = localStorage.getItem('refresh_token') || localStorage.getItem('refreshToken') || '';

                    const u = localStorage.getItem('user');
                    if (u) {
                        try {
                            const parsed = JSON.parse(u);
                            if (!access && parsed && parsed.token && parsed.token.accessToken) {
                                access = parsed.token.accessToken;
                            }
                            if (!refresh && parsed && parsed.token && parsed.token.refreshToken) {
                                refresh = parsed.token.refreshToken;
                            }
                        } catch (e) {}
                    }

                    if (!access) {
                        for (let i = 0; i < localStorage.length; i++) {
                            const k = localStorage.key(i);
                            if (k && k.toLowerCase().includes('refresh')) continue;
                            const v = localStorage.getItem(k);
                            if (v && v.includes('eyJ')) {
                                const m = v.match(jwtRe);
                                if (m) { access = m[0]; break; }
                            }
                        }
                    }

                    if (!access) {
                        for (let i = 0; i < sessionStorage.length; i++) {
                            const k = sessionStorage.key(i);
                            if (k && k.toLowerCase().includes('refresh')) continue;
                            const v = sessionStorage.getItem(k);
                            if (v && v.includes('eyJ')) {
                                const m = v.match(jwtRe);
                                if (m) { access = m[0]; break; }
                            }
                        }
                    }

                    if (!access) return '';
                    return JSON.stringify({ accessToken: access, refreshToken: refresh });
                } catch (e) {
                    return '';
                }
            })()";

            string jsonResult = await _webView.ExecuteScriptAsync(script);
            if (!string.IsNullOrWhiteSpace(jsonResult) && jsonResult != "null" && jsonResult != "\"\"")
            {
                string? unquoted = JsonSerializer.Deserialize<string>(jsonResult);
                if (!string.IsNullOrWhiteSpace(unquoted))
                {
                    using var doc = JsonDocument.Parse(unquoted);
                    string rawToken = doc.RootElement.TryGetProperty("accessToken", out var at) ? at.GetString() ?? "" : "";
                    string rawRefresh = doc.RootElement.TryGetProperty("refreshToken", out var rt) ? rt.GetString() ?? "" : "";

                    rawToken = rawToken.Trim('\"', ' ', '\\');
                    if (rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        rawToken = rawToken.Substring(7).Trim();
                    }

                    if (rawToken.Length > 25 && rawToken.Contains("eyJ"))
                    {
                        if (!JwtTokenInspector.IsExpired(rawToken))
                        {
                            await CompleteLoginSuccessAsync(rawToken, rawRefresh);
                        }
                        else
                        {
                            // Süresi dolmuş eski token varsa localStorage'dan temizle ki yeni girişi engellemesin
                            await _webView.ExecuteScriptAsync("(() => { try { localStorage.removeItem('token'); localStorage.removeItem('accessToken'); localStorage.removeItem('user'); } catch(e){} })()");
                        }
                    }
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught, "ArasGlobalEmbeddedLoginForm.CheckStorageTokenAsync"); }
    }

    private async Task CompleteLoginSuccessAsync(string token, string? refreshToken = null)
    {
        if (_tokenCaptured) return;
        _tokenCaptured = true;
        _storageCheckTimer.Stop();

        // Eğer refresh token henüz parametreyle gelmediyse localStorage'dan son bir kez okumayı dene
        if (string.IsNullOrWhiteSpace(refreshToken) && _webView.CoreWebView2 != null)
        {
            try
            {
                string rfJson = await _webView.ExecuteScriptAsync("(() => { try { return localStorage.getItem('refresh_token') || (JSON.parse(localStorage.getItem('user') || '{}')?.token?.refreshToken) || ''; } catch(e){ return ''; } })()");
                if (!string.IsNullOrWhiteSpace(rfJson) && rfJson != "null" && rfJson != "\"\"")
                {
                    refreshToken = rfJson.Trim('\"', ' ', '\\');
                }
            }
            catch { }
        }

        CapturedToken = token;

        // Ayarları kalıcı olarak kaydet
        var settings = ArasGlobalSettingsStore.Load();
        settings.BearerToken = token;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            settings.RefreshToken = refreshToken.Trim();
        }
        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        ArasGlobalSettingsStore.Save(settings);

        // UI Güncelle
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                _topBar.BackColor = Color.FromArgb(6, 78, 59); // Emerald 900
                _lblTitle.Text = "✅ Giriş Başarılı!";
                _lblStatus.Text = "🎉 Aras Global oturumunuz bağlandı. Pencere kapatılıyor...";
                _lblStatus.ForeColor = Color.FromArgb(167, 243, 208); // Emerald 200
            }));
        }
        else
        {
            _topBar.BackColor = Color.FromArgb(6, 78, 59);
            _lblTitle.Text = "✅ Giriş Başarılı!";
            _lblStatus.Text = "🎉 Aras Global oturumunuz bağlandı. Pencere kapatılıyor...";
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
