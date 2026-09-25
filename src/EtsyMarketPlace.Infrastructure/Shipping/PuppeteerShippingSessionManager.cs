namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
using PuppeteerSharp;

/// <summary>
/// PuppeteerSharp tabanlı otomatik oturum açma ve token yakalama motoru.
/// Aras Global ve ShipEntegra panellerine sessizce bağlanarak canlı tokeni cımbızlar.
/// </summary>
public sealed class PuppeteerShippingSessionManager : IShippingSessionManager
{
    public static string GetProfileDirectory(string provider)
    {
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "ShippingProfiles",
            provider);
        Directory.CreateDirectory(baseFolder);
        CleanProfileLocks(baseFolder);
        return baseFolder;
    }

    public static void CleanProfileLocks(string profileDir)
    {
        try
        {
            if (!Directory.Exists(profileDir)) return;
            string[] lockFiles = ["SingletonLock", "SingletonCookie", "SingletonSocket", "lockfile"];
            foreach (var name in lockFiles)
            {
                string p = Path.Combine(profileDir, name);
                if (File.Exists(p))
                {
                    try { File.Delete(p); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Kullanıcının varsayılan sistem tarayıcısında (Chrome/Edge vb.) resmi paneli doğrudan açar.
    /// </summary>
    public static void OpenOfficialPortalInDefaultBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            }
            catch { }
        }
    }

    public async Task<string?> RefreshArasGlobalTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default,
        string? knownExpiredToken = null)
    {
        string? browserPath = VisualBrowserAgentService.ResolveInstalledBrowserPath();
        if (string.IsNullOrEmpty(browserPath))
        {
            statusCallback?.Invoke("⚠️ Yüklü Google Chrome veya Edge bulunamadı. Lütfen tarayıcı yükleyin.");
            return null;
        }

        string cleanExpired = (knownExpiredToken ?? string.Empty).Trim();
        if (cleanExpired.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            cleanExpired = cleanExpired[7..].Trim();
        }

        statusCallback?.Invoke("🚀 Aras Global oturum motoru başlatılıyor...");

        var launchOptions = new LaunchOptions
        {
            Headless = !showBrowser,
            ExecutablePath = browserPath,
            UserDataDir = GetProfileDirectory("ArasGlobal"),
            IgnoredDefaultArgs = new[] { "--enable-automation" },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1200,800",
                "--remote-debugging-port=0"
            },
            DefaultViewport = showBrowser ? null : new ViewPortOptions { Width = 1180, Height = 760 }
        };

        IBrowser? browser = null;
        string? capturedToken = null;

        try
        {
            browser = await Puppeteer.LaunchAsync(launchOptions);
            var pages = await browser.PagesAsync();
            var page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();

            // 1. Ağ isteklerini dinle: Authorization: Bearer başlıklarını yakala
            page.Request += (_, e) =>
            {
                if (e.Request.Headers.TryGetValue("authorization", out var auth) &&
                    !string.IsNullOrWhiteSpace(auth) &&
                    auth.StartsWith("Bearer eyJ", StringComparison.OrdinalIgnoreCase))
                {
                    string cand = auth[7..].Trim();
                    if (!cand.Equals(cleanExpired, StringComparison.OrdinalIgnoreCase) && !JwtTokenInspector.IsExpired(cand))
                    {
                        capturedToken = cand;
                    }
                }
            };

            // 2. Ağ yanıtlarını dinle: /login, /token, /auth endpointlerinden dönen JWT'leri yakala
            page.Response += async (_, e) =>
            {
                try
                {
                    string url = e.Response.Url;
                    if (url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
                        url.Contains("/auth", StringComparison.OrdinalIgnoreCase) ||
                        url.Contains("/token", StringComparison.OrdinalIgnoreCase) ||
                        url.Contains("arasglobalcargo.com", StringComparison.OrdinalIgnoreCase))
                    {
                        string body = await e.Response.TextAsync();
                        if (!string.IsNullOrWhiteSpace(body) && body.Contains("eyJ"))
                        {
                            string? extracted = ExtractJwtFromString(body);
                            if (!string.IsNullOrWhiteSpace(extracted) &&
                                !extracted.Equals(cleanExpired, StringComparison.OrdinalIgnoreCase) &&
                                !JwtTokenInspector.IsExpired(extracted))
                            {
                                capturedToken = extracted;
                            }
                        }
                    }
                }
                catch { }
            };

            statusCallback?.Invoke("🌐 Aras Global paneline bağlanılıyor...");
            await page.GoToAsync("https://panel.arasglobalcargo.com/login", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 25000
            });

            // 3. localStorage kontrol et; eğer süresi dolmuşsa veya bilinen eski tokense temizle!
            string? localTok = await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");
            if (!string.IsNullOrWhiteSpace(localTok))
            {
                if (localTok.Equals(cleanExpired, StringComparison.OrdinalIgnoreCase) || JwtTokenInspector.IsExpired(localTok))
                {
                    statusCallback?.Invoke("🧹 Süresi dolmuş eski oturum tokeni tarayıcıdan temizleniyor...");
                    await page.EvaluateFunctionAsync(@"() => {
                        try {
                            localStorage.removeItem('token');
                            localStorage.removeItem('jwt');
                            localStorage.removeItem('accessToken');
                            sessionStorage.clear();
                        } catch {}
                    }");
                    localTok = null;
                }
                else
                {
                    capturedToken = localTok;
                }
            }

            // 4. Eğer geçerli bir token yoksa ve giriş bilgileri tanımlıysa formu doldur
            if (string.IsNullOrWhiteSpace(capturedToken) && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                statusCallback?.Invoke("🔑 Giriş bilgileri dolduruluyor...");
                await TryFillLoginFormAsync(page, email, password);
                await Task.Delay(3000, ct);

                var checkAfterLogin = await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");
                if (!string.IsNullOrWhiteSpace(checkAfterLogin) &&
                    !checkAfterLogin.Equals(cleanExpired, StringComparison.OrdinalIgnoreCase) &&
                    !JwtTokenInspector.IsExpired(checkAfterLogin))
                {
                    capturedToken = checkAfterLogin;
                }
            }

            if (showBrowser)
            {
                statusCallback?.Invoke("🌐 Tarayıcı açıldı. Lütfen Aras Global hesabınıza giriş yapın (Token otomatik yakalanacaktır)...");
            }

            // 5. Ağ dinlemesi ve token yakalama için bekle
            int maxWait = showBrowser ? 120 : 18;
            int waited = 0;
            while (string.IsNullOrWhiteSpace(capturedToken) && waited < maxWait)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
                waited++;

                var loopTok = await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");
                if (!string.IsNullOrWhiteSpace(loopTok) &&
                    !loopTok.Equals(cleanExpired, StringComparison.OrdinalIgnoreCase) &&
                    !JwtTokenInspector.IsExpired(loopTok))
                {
                    capturedToken = loopTok;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(capturedToken))
            {
                statusCallback?.Invoke("✅ Canlı Aras Global tokeni başarıyla yakalandı ve kaydedildi!");
                var settings = ArasGlobalSettingsStore.Load();
                settings.BearerToken = capturedToken;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(email)) settings.SavedEmail = email;
                if (!string.IsNullOrWhiteSpace(password)) settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
                ArasGlobalSettingsStore.Save(settings);
                return capturedToken;
            }

            statusCallback?.Invoke("⚠️ Token otomatik yakalanamadı. Lütfen 'Tarayıcıda Aç' seçeneğiyle bir kez giriş yapın.");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"❌ Aras Global oturum hatası: {ex.Message}");
            if (showBrowser)
            {
                throw;
            }
            return null;
        }
        finally
        {
            if (browser != null)
            {
                await browser.CloseAsync();
            }
        }
    }

    public async Task<string?> RefreshShipEntegraTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        string? browserPath = VisualBrowserAgentService.ResolveInstalledBrowserPath();
        if (string.IsNullOrEmpty(browserPath))
        {
            statusCallback?.Invoke("⚠️ Yüklü Google Chrome veya Edge bulunamadı. Lütfen tarayıcı yükleyin.");
            return null;
        }

        statusCallback?.Invoke("🚀 ShipEntegra oturum motoru başlatılıyor...");

        var launchOptions = new LaunchOptions
        {
            Headless = !showBrowser,
            ExecutablePath = browserPath,
            UserDataDir = GetProfileDirectory("ShipEntegra"),
            IgnoredDefaultArgs = new[] { "--enable-automation" },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1200,800"
            },
            DefaultViewport = new ViewPortOptions { Width = 1180, Height = 760 }
        };

        IBrowser? browser = null;
        string? capturedToken = null;

        try
        {
            browser = await Puppeteer.LaunchAsync(launchOptions);
            var pages = await browser.PagesAsync();
            var page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();

            page.Request += (_, e) =>
            {
                if (e.Request.Headers.TryGetValue("authorization", out var auth) &&
                    !string.IsNullOrWhiteSpace(auth) &&
                    auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    capturedToken = auth[7..].Trim();
                }
            };

            statusCallback?.Invoke("🌐 ShipEntegra paneline bağlanılıyor...");
            await page.GoToAsync("https://app.shipentegra.com/", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 25000
            });

            // 1. Önce localStorage kontrol et
            capturedToken = await TryExtractLocalStorageTokenAsync(page, "token", "access_token", "auth_token");

            // 2. Eğer token bulunamadıysa ve kimlik bilgileri varsa giriş formunu doldur
            if (string.IsNullOrWhiteSpace(capturedToken) && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                statusCallback?.Invoke("🔑 ShipEntegra giriş bilgileri dolduruluyor...");
                await TryFillLoginFormAsync(page, email, password);
                await Task.Delay(3000, ct);

                capturedToken ??= await TryExtractLocalStorageTokenAsync(page, "token", "access_token", "auth_token");
            }

            if (showBrowser)
            {
                statusCallback?.Invoke("🌐 Tarayıcı açıldı. Lütfen ShipEntegra hesabınıza giriş yapın (Token otomatik yakalanacaktır)...");
            }

            // 3. Ağ dinlemesi ve token yakalama için bekle
            int maxWait = showBrowser ? 120 : 15;
            int waited = 0;
            while (string.IsNullOrWhiteSpace(capturedToken) && waited < maxWait)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
                waited++;
                capturedToken = await TryExtractLocalStorageTokenAsync(page, "token", "access_token", "auth_token");
            }

            if (!string.IsNullOrWhiteSpace(capturedToken))
            {
                statusCallback?.Invoke("✅ Canlı ShipEntegra tokeni başarıyla yakalandı ve kaydedildi!");
                var settings = ShipEntegraSettingsStore.Load();
                settings.BearerToken = capturedToken;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(email)) settings.SavedEmail = email;
                if (!string.IsNullOrWhiteSpace(password)) settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
                ShipEntegraSettingsStore.Save(settings);
                return capturedToken;
            }

            statusCallback?.Invoke("⚠️ Token otomatik yakalanamadı. Lütfen 'Tarayıcıda Aç' seçeneğiyle bir kez giriş yapın.");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"❌ ShipEntegra oturum hatası: {ex.Message}");
            return null;
        }
        finally
        {
            if (browser != null)
            {
                await browser.CloseAsync();
            }
        }
    }

    public async Task<string?> RefreshNavlungoTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        string? browserPath = VisualBrowserAgentService.ResolveInstalledBrowserPath();
        if (string.IsNullOrEmpty(browserPath))
        {
            statusCallback?.Invoke("⚠️ Yüklü Google Chrome veya Edge bulunamadı. Lütfen tarayıcı yükleyin.");
            return null;
        }

        statusCallback?.Invoke("🚀 Navlungo oturum motoru başlatılıyor...");

        var launchOptions = new LaunchOptions
        {
            Headless = !showBrowser,
            ExecutablePath = browserPath,
            UserDataDir = GetProfileDirectory("Navlungo"),
            IgnoredDefaultArgs = new[] { "--enable-automation" },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1200,800"
            },
            DefaultViewport = new ViewPortOptions { Width = 1180, Height = 760 }
        };

        IBrowser? browser = null;
        string? capturedToken = null;
        string? capturedCookies = null;

        try
        {
            browser = await Puppeteer.LaunchAsync(launchOptions);
            var pages = await browser.PagesAsync();
            var page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();

            page.Request += (_, e) =>
            {
                if (e.Request.Headers.TryGetValue("cookie", out var cookieStr) && !string.IsNullOrWhiteSpace(cookieStr))
                {
                    if (cookieStr.Contains("id_token=", StringComparison.OrdinalIgnoreCase) ||
                        cookieStr.Contains("_SessionUser_", StringComparison.OrdinalIgnoreCase))
                    {
                        capturedCookies = cookieStr;
                        var m = System.Text.RegularExpressions.Regex.Match(cookieStr, @"id_token=([^;]+)");
                        if (m.Success) capturedToken = m.Groups[1].Value.Trim();
                    }
                }
                if (e.Request.Headers.TryGetValue("authorization", out var auth) &&
                    !string.IsNullOrWhiteSpace(auth) &&
                    auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    capturedToken = auth[7..].Trim();
                }
            };

            statusCallback?.Invoke("🌐 Navlungo paneline bağlanılıyor...");
            await page.GoToAsync("https://ship.navlungo.com/login", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 25000
            });

            // 1. Önce localStorage kontrol et
            capturedToken ??= await TryExtractLocalStorageTokenAsync(page, "id_token", "token", "access_token", "accessToken");

            // 2. Bilgiler varsa form doldurmayı dene
            if (string.IsNullOrWhiteSpace(capturedToken) && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                statusCallback?.Invoke("🔑 Navlungo giriş bilgileri dolduruluyor...");
                await TryFillLoginFormAsync(page, email, password);
                await Task.Delay(3000, ct);

                capturedToken ??= await TryExtractLocalStorageTokenAsync(page, "id_token", "token", "access_token", "accessToken");
            }

            if (showBrowser)
            {
                statusCallback?.Invoke("🌐 Tarayıcı açıldı. Lütfen Navlungo hesabınıza giriş yapın (Oturum otomatik yakalanacaktır)...");
            }

            int maxWait = showBrowser ? 120 : 15;
            int waited = 0;
            while (string.IsNullOrWhiteSpace(capturedToken) && string.IsNullOrWhiteSpace(capturedCookies) && waited < maxWait)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
                waited++;
                capturedToken ??= await TryExtractLocalStorageTokenAsync(page, "id_token", "token", "access_token", "accessToken");

                var cookies = await page.GetCookiesAsync("https://ship.navlungo.com", "https://quick-price-calculator.navlungo.com");
                if (cookies != null && cookies.Length > 0)
                {
                    var idTokCookie = cookies.FirstOrDefault(c => c.Name.Equals("id_token", StringComparison.OrdinalIgnoreCase));
                    if (idTokCookie != null && !string.IsNullOrWhiteSpace(idTokCookie.Value))
                    {
                        capturedToken = idTokCookie.Value;
                    }

                    // Herhangi bir oturum çerezi veya kullanıcı göstergesi varsa (_SessionUser, nv_attr, session, token, auth vb.)
                    bool hasSessionCookie = cookies.Any(c => 
                        c.Name.Contains("session", StringComparison.OrdinalIgnoreCase) || 
                        c.Name.Contains("token", StringComparison.OrdinalIgnoreCase) || 
                        c.Name.Contains("nv_attr", StringComparison.OrdinalIgnoreCase) ||
                        c.Name.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                        c.Name.Contains("auth", StringComparison.OrdinalIgnoreCase));

                    bool navigatedAwayFromLogin = !page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) && 
                                                 !page.Url.Contains("/giris", StringComparison.OrdinalIgnoreCase) &&
                                                 !page.Url.Contains("/auth", StringComparison.OrdinalIgnoreCase);

                    if ((hasSessionCookie && navigatedAwayFromLogin) || !string.IsNullOrWhiteSpace(capturedToken))
                    {
                        try
                        {
                            statusCallback?.Invoke("🌐 Hesaplayıcı çerezleri eşitleniyor...");
                            await page.GoToAsync("https://quick-price-calculator.navlungo.com/tr?source=user", new NavigationOptions
                            {
                                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                                Timeout = 8000
                            });
                            var allCookies = await page.GetCookiesAsync("https://ship.navlungo.com", "https://quick-price-calculator.navlungo.com");
                            if (allCookies != null && allCookies.Length > 0)
                            {
                                capturedCookies = string.Join("; ", allCookies.Select(c => $"{c.Name}={c.Value}"));
                            }
                        }
                        catch { }

                        capturedCookies ??= string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                        capturedToken ??= "navlungo_browser_session";
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(capturedToken) || !string.IsNullOrWhiteSpace(capturedCookies))
            {
                statusCallback?.Invoke("✅ Canlı Navlungo oturumu başarıyla yakalandı ve kaydedildi!");
                var settings = NavlungoSettingsStore.Load();
                if (!string.IsNullOrWhiteSpace(capturedToken)) settings.IdToken = capturedToken;
                if (!string.IsNullOrWhiteSpace(capturedCookies)) settings.SessionCookie = capturedCookies;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(email)) settings.SavedEmail = email;
                if (!string.IsNullOrWhiteSpace(password)) settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
                NavlungoSettingsStore.Save(settings);
                return capturedToken ?? "connected";
            }

            statusCallback?.Invoke("⚠️ Oturum yakalanamadı. Lütfen 'Tarayıcı' butonunu kullanarak giriş yapın.");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"❌ Navlungo oturum hatası: {ex.Message}");
            return null;
        }
        finally
        {
            if (browser != null)
            {
                await browser.CloseAsync();
            }
        }
    }

    public async Task<string?> RefreshShiptomoreTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default)
    {
        string? browserPath = VisualBrowserAgentService.ResolveInstalledBrowserPath();
        if (string.IsNullOrEmpty(browserPath))
        {
            statusCallback?.Invoke("⚠️ Yüklü Google Chrome veya Edge bulunamadı. Lütfen tarayıcı yükleyin.");
            return null;
        }

        var launchOptions = new LaunchOptions
        {
            Headless = !showBrowser,
            ExecutablePath = browserPath,
            UserDataDir = GetProfileDirectory("Shiptomore"),
            IgnoredDefaultArgs = new[] { "--enable-automation" },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1200,800"
            },
            DefaultViewport = new ViewPortOptions { Width = 1180, Height = 760 }
        };

        IBrowser? browser = null;
        try
        {
            statusCallback?.Invoke("🚀 Shiptomore tarayıcı oturumu başlatılıyor...");
            browser = await Puppeteer.LaunchAsync(launchOptions);
            var pages = await browser.PagesAsync();
            var page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();

            string? capturedSessionId = null;
            string? capturedCookies = null;

            // Ağ trafiğini dinleyerek /parcel/calculate veya Odoo session çerezlerini yakala
            page.Response += (_, e) =>
            {
                try
                {
                    if (e.Response.Headers.TryGetValue("set-cookie", out var setCookieHeader))
                    {
                        if (setCookieHeader.Contains("session_id=", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(setCookieHeader, @"session_id=([^;,\s]+)");
                            if (match.Success)
                            {
                                capturedSessionId = match.Groups[1].Value;
                            }
                        }
                    }
                }
                catch { }
            };

            statusCallback?.Invoke("🌐 Shiptomore giriş sayfasına gidiliyor...");
            await page.GoToAsync("https://shiptomore.com/web/login?redirect=%2Fmy%2Fparcel-calculator", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 30000
            });

            if (!showBrowser && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                statusCallback?.Invoke("🔑 Kayıtlı bilgiler ile giriş yapılıyor...");
                var loginInput = await page.QuerySelectorAsync("input[name='login'], input[type='email'], input#login");
                if (loginInput != null)
                {
                    await loginInput.ClickAsync();
                    await loginInput.TypeAsync(email);
                }

                var passInput = await page.QuerySelectorAsync("input[name='password'], input[type='password'], input#password");
                if (passInput != null)
                {
                    await passInput.ClickAsync();
                    await passInput.TypeAsync(password);
                }

                var submitBtn = await page.QuerySelectorAsync("button[type='submit'], .oe_login_form button");
                if (submitBtn != null)
                {
                    await submitBtn.ClickAsync();
                }
            }
            else
            {
                statusCallback?.Invoke("🌐 Tarayıcı açıldı. Lütfen Shiptomore hesabınıza giriş yapın (Oturum otomatik yakalanacaktır)...");
            }

            int maxWait = showBrowser ? 180 : 20;
            int waited = 0;
            while (string.IsNullOrWhiteSpace(capturedSessionId) && waited < maxWait)
            {
                ct.ThrowIfCancellationRequested();

                // Kullanıcı pencereyi çarpıdan kapattıysa sahte çerez kaydetmeden temiz çık
                try
                {
                    if (page.IsClosed || (await browser.PagesAsync()).Length == 0)
                    {
                        statusCallback?.Invoke("⚠️ Tarayıcı penceresi kapatıldı, oturum açma iptal edildi.");
                        return null;
                    }
                }
                catch
                {
                    return null;
                }

                await Task.Delay(1000, ct);
                waited++;

                // 1. Sayfa login adresinden ayrıldı mı?
                bool navigatedAwayFromLogin = !page.Url.Contains("/web/login", StringComparison.OrdinalIgnoreCase);

                // 2. Sayfa içinde Odoo aktif kullanıcı UID'si oluştu mu?
                bool isOdooAuthenticated = false;
                try
                {
                    isOdooAuthenticated = await page.EvaluateFunctionAsync<bool>(@"() => {
                        if (window.odoo && window.odoo.__session_info__ && window.odoo.__session_info__.uid) {
                            return true;
                        }
                        return !!document.querySelector('a[href*=""/web/session/logout""], a[href*=""/my/home""], .o_user_bookmark');
                    }");
                }
                catch { }

                if (navigatedAwayFromLogin || isOdooAuthenticated)
                {
                    var cookies = await page.GetCookiesAsync("https://shiptomore.com");
                    if (cookies != null && cookies.Length > 0)
                    {
                        var sessCookie = cookies.FirstOrDefault(c => c.Name.Equals("session_id", StringComparison.OrdinalIgnoreCase));
                        if (sessCookie != null && !string.IsNullOrWhiteSpace(sessCookie.Value))
                        {
                            // Sunucu düzeyinde get_session_info ile UID'yi de kesin doğrula
                            string candidateCookie = $"session_id={sessCookie.Value}";
                            var (isValid, userName, uid) = await ShiptomoreApiClient.ValidateSessionAsync(candidateCookie, ct);
                            if (isValid)
                            {
                                capturedSessionId = sessCookie.Value;
                                capturedCookies = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                                statusCallback?.Invoke($"🎉 Hoş geldiniz {userName ?? "Kullanıcı"}! Canlı üye oturumu (UID: {uid}) doğrulandı.");
                                break;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(capturedSessionId) || !string.IsNullOrWhiteSpace(capturedCookies))
            {
                statusCallback?.Invoke("✅ Canlı Shiptomore oturumu başarıyla yakalandı ve kaydedildi!");
                var settings = ShiptomoreSettingsStore.Load();
                settings.SessionCookie = capturedCookies ?? $"session_id={capturedSessionId}";
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(email)) settings.SavedEmail = email;
                if (!string.IsNullOrWhiteSpace(password)) settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(password);
                ShiptomoreSettingsStore.Save(settings);
                return capturedSessionId ?? "connected";
            }

            statusCallback?.Invoke("⚠️ Oturum yakalanamadı. Lütfen 'Tarayıcı' (🌐) butonunu kullanarak giriş yapın.");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"❌ Shiptomore oturum hatası: {ex.Message}");
            return null;
        }
        finally
        {
            if (browser != null)
            {
                await browser.CloseAsync();
            }
        }
    }

    private static async Task<string?> TryExtractLocalStorageTokenAsync(IPage page, params string[] keys)
    {
        try
        {
            foreach (var key in keys)
            {
                string script = $"() => localStorage.getItem('{key}') || sessionStorage.getItem('{key}') || ''";
                var val = await page.EvaluateFunctionAsync<string>(script);
                if (!string.IsNullOrWhiteSpace(val) && val.Length > 25)
                {
                    if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) val = val[7..];
                    val = val.Trim('\"', '\'', ' ');
                    return val;
                }
            }
        }
        catch { }

        return null;
    }

    private static async Task TryFillLoginFormAsync(IPage page, string email, string password)
    {
        try
        {
            // Email input bulma
            var emailInput = await page.QuerySelectorAsync("input[type='email'], input[name='email'], input[name='username'], input[formcontrolname='email'], input[formcontrolname='userName']");
            if (emailInput != null)
            {
                await emailInput.ClickAsync();
                await emailInput.TypeAsync(email);
            }

            // Password input bulma
            var passInput = await page.QuerySelectorAsync("input[type='password'], input[name='password'], input[formcontrolname='password']");
            if (passInput != null)
            {
                await passInput.ClickAsync();
                await passInput.TypeAsync(password);
            }

            // Submit butonu tıklama
            var submitBtn = await page.QuerySelectorAsync("button[type='submit'], input[type='submit'], button.login-btn, button.btn-primary");
            if (submitBtn != null)
            {
                await submitBtn.ClickAsync();
            }
        }
        catch { }
    }

    private static string? ExtractJwtFromString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(input, @"ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+");
        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }
}
