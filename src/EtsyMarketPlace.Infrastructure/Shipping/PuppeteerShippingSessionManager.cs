namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
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
        return baseFolder;
    }

    public async Task<string?> RefreshArasGlobalTokenAsync(
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

            // Ağ trafiğini dinle: Authorization: Bearer başlıklarını yakala
            page.Request += (_, e) =>
            {
                if (e.Request.Headers.TryGetValue("authorization", out var auth) &&
                    !string.IsNullOrWhiteSpace(auth) &&
                    auth.StartsWith("Bearer eyJ", StringComparison.OrdinalIgnoreCase))
                {
                    capturedToken = auth[7..].Trim();
                }
            };

            statusCallback?.Invoke("🌐 Aras Global paneline bağlanılıyor...");
            await page.GoToAsync("https://panel.arasglobalcargo.com/self-service", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout = 25000
            });

            // 1. Önce localStorage kontrol et
            capturedToken = await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");

            // 2. Eğer token bulunamadıysa ve kullanıcı adı/şifre varsa giriş formunu doldur
            if (string.IsNullOrWhiteSpace(capturedToken) && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                statusCallback?.Invoke("🔑 Giriş bilgileri dolduruluyor...");
                await TryFillLoginFormAsync(page, email, password);
                await Task.Delay(3000, ct);

                capturedToken ??= await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");
            }

            if (showBrowser)
            {
                statusCallback?.Invoke("🌐 Tarayıcı açıldı. Lütfen Aras Global hesabınıza giriş yapın (Token otomatik yakalanacaktır)...");
            }

            // 3. Ağ dinlemesi ve token yakalama için bekle
            int maxWait = showBrowser ? 120 : 15;
            int waited = 0;
            while (string.IsNullOrWhiteSpace(capturedToken) && waited < maxWait)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
                waited++;
                capturedToken = await TryExtractLocalStorageTokenAsync(page, "token", "jwt", "accessToken");
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
                        capturedCookies = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
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
}
