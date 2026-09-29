namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using SimilarProductsWinForms.Diagnostics;

/// <summary>
/// "Kargo Hesapları &amp; Oturumlar" merkezi: taşıyıcı hesap kartlarını (Aras Global,
/// ShipEntegra, Navlungo, Shiptomore) gösteren, oturum açma / bağlantı kesme
/// işlemlerini yürüten ortak denetim. Kart tasarımı, kâr simülatöründeki
/// (AnimatedShippingComparisonDrawer) hesap merkeziyle birebir aynıdır.
/// </summary>
public sealed class CarrierAccountsHubControl : UserControl
{
    private readonly IShippingSessionManager _sessionManager;
    private readonly FlowLayoutPanel _flow;
    private readonly ToolTip _cardToolTip = new() { InitialDelay = 300, ReshowDelay = 150 };
    private readonly Image? _arasLogo;
    private readonly Image? _shipEntegraLogo;
    private readonly Image? _navlungoLogo;
    private readonly Image? _shiptomoreLogo;

    /// <summary>Kullanıcıya gösterilecek durum mesajı (metin, tür).</summary>
    public event Action<string, CarrierHubStatusKind>? StatusMessage;

    /// <summary>Hesap/oturum durumu değiştiğinde tetiklenir (fiyatların yenilenmesi için).</summary>
    public event Action? SessionsChanged;

    public CarrierAccountsHubControl(IShippingSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        BackColor = Color.Transparent;

        _arasLogo = LoadLogoSafely("aras_global.png");
        _shipEntegraLogo = LoadLogoSafely("shipentegra.jpg") ?? LoadLogoSafely("shipentegra.png");
        _navlungoLogo = LoadLogoSafely("navlungo.png");
        _shiptomoreLogo = LoadLogoSafely("shiptomore.png");

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        Controls.Add(_flow);

        RebuildAccounts();
    }

    /// <summary>Hesap kartlarını güncel oturum durumlarıyla yeniden çizer.</summary>
    public void RebuildAccounts()
    {
        _flow.Controls.Clear();

        var arasSettings = ArasGlobalSettingsStore.Load();
        bool arasConnected = arasSettings.HasValidTokenFormat;

        var seSettings = ShipEntegraSettingsStore.Load();
        bool seConnected = seSettings.HasValidTokenFormat;

        _flow.Controls.Add(CreateCarrierAccountCard(
            "Aras Global",
            _arasLogo ?? CreateFallbackLogo("aras"),
            arasConnected,
            () => TriggerArasEmbeddedLoginAsync(),
            () => TriggerArasEmbeddedLoginAsync(),
            () => DisconnectCarrierAsync("Aras Global")));

        _flow.Controls.Add(CreateCarrierAccountCard(
            "ShipEntegra",
            _shipEntegraLogo ?? CreateFallbackLogo("shipentegra"),
            seConnected,
            () => TriggerShipEntegraEmbeddedLoginAsync(),
            () => TriggerShipEntegraEmbeddedLoginAsync(),
            () => DisconnectCarrierAsync("ShipEntegra")));

        var navSettings = NavlungoSettingsStore.Load();
        bool navConnected = !string.IsNullOrWhiteSpace(navSettings.IdToken) || !string.IsNullOrWhiteSpace(navSettings.SessionCookie);

        _flow.Controls.Add(CreateCarrierAccountCard(
            "Navlungo",
            _navlungoLogo ?? CreateFallbackLogo("navlungo"),
            navConnected,
            () => TriggerNavlungoEmbeddedLoginAsync(),
            () => TriggerNavlungoEmbeddedLoginAsync(),
            () => DisconnectCarrierAsync("Navlungo")));

        bool stmConnected = ShiptomoreSettingsStore.HasCredentials();
        _flow.Controls.Add(CreateCarrierAccountCard(
            "Shiptomore",
            _shiptomoreLogo ?? CreateFallbackLogo("shiptomore"),
            stmConnected,
            () => OpenShiptomoreConnectionFormAsync(),
            null,
            null,
            autoLabel: "⚙ Bağlantı"));

        _flow.Controls.Add(CreateAddCarrierPlaceholderCard());
    }

    private void Report(string message, CarrierHubStatusKind kind) => StatusMessage?.Invoke(message, kind);

    private async Task TriggerArasEmbeddedLoginAsync()
    {
        await Task.Yield();
        try
        {
            using var loginForm = new ArasGlobalEmbeddedLoginForm();
            if (loginForm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                Report("✅ Aras Global tokeni başarıyla güncellendi!", CarrierHubStatusKind.Success);
                RebuildAccounts();
                SessionsChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Oturum penceresi açılamadı:\n{ex.Message}", "Oturum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TriggerShipEntegraEmbeddedLoginAsync()
    {
        await Task.Yield();
        try
        {
            using var loginForm = new ShipEntegraEmbeddedLoginForm();
            if (loginForm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                Report("✅ ShipEntegra oturumu güncellendi!", CarrierHubStatusKind.Success);
                RebuildAccounts();
                SessionsChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Oturum açma penceresi açılamadı:\n{ex.Message}", "Oturum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TriggerNavlungoEmbeddedLoginAsync()
    {
        await Task.Yield();
        try
        {
            using var loginForm = new NavlungoEmbeddedLoginForm();
            if (loginForm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                Report("✅ Navlungo oturumu güncellendi!", CarrierHubStatusKind.Success);
                RebuildAccounts();
                SessionsChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Oturum açma penceresi açılamadı:\n{ex.Message}", "Oturum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Task OpenShiptomoreConnectionFormAsync()
    {
        try
        {
            using var connectionForm = new ShiptomoreConnectionForm();
            connectionForm.ShowDialog(FindForm());
            bool connected = ShiptomoreSettingsStore.HasCredentials();
            Report(
                connected ? "✅ Shiptomore API bağlantısı hazır." : "ℹ️ Shiptomore bağlantı ekranı kapatıldı.",
                connected ? CarrierHubStatusKind.Success : CarrierHubStatusKind.Info);
            RebuildAccounts();
            SessionsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Bağlantı penceresi açılamadı:\n{ex.Message}", "Shiptomore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return Task.CompletedTask;
    }

    private async Task DisconnectCarrierAsync(string providerName)
    {
        await Task.Yield();

        bool isAras = providerName.Contains("Aras", StringComparison.OrdinalIgnoreCase);
        bool isNav = providerName.Contains("Navlungo", StringComparison.OrdinalIgnoreCase);
        bool isStm = providerName.Contains("Shiptomore", StringComparison.OrdinalIgnoreCase);
        bool isSe = providerName.Contains("ShipEntegra", StringComparison.OrdinalIgnoreCase);

        bool isConnected = false;
        if (isAras) isConnected = !string.IsNullOrWhiteSpace(ArasGlobalSettingsStore.Load().BearerToken);
        else if (isNav)
        {
            var s = NavlungoSettingsStore.Load();
            isConnected = !string.IsNullOrWhiteSpace(s.SessionCookie) || !string.IsNullOrWhiteSpace(s.IdToken);
        }
        else if (isStm) isConnected = !string.IsNullOrWhiteSpace(ShiptomoreSettingsStore.Load().SessionCookie);
        else if (isSe) isConnected = !string.IsNullOrWhiteSpace(ShipEntegraSettingsStore.Load().BearerToken);

        if (!isConnected)
        {
            MessageBox.Show($"{providerName} için zaten aktif bir oturum veya bağlantı bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"{providerName} oturumunu kapatmak ve sistem bağlantısını kesmek istediğinize emin misiniz?\n\n(Bu işlem aktif oturum çerezlerini temizler ve canlı fiyatları standart liste durumuna çeker.)",
            $"{providerName} - Bağlantıyı Kes / Çıkış",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        if (isAras)
        {
            var s = ArasGlobalSettingsStore.Load();
            s.BearerToken = string.Empty;
            s.TokenLastUpdatedUtc = null;
            ArasGlobalSettingsStore.Save(s);
        }
        else if (isNav)
        {
            var s = NavlungoSettingsStore.Load();
            s.SessionCookie = null;
            s.IdToken = null;
            s.TokenLastUpdatedUtc = null;
            NavlungoSettingsStore.Save(s);
        }
        else if (isStm)
        {
            var s = ShiptomoreSettingsStore.Load();
            s.SessionCookie = null;
            s.TokenLastUpdatedUtc = null;
            ShiptomoreSettingsStore.Save(s);
        }
        else if (isSe)
        {
            var s = ShipEntegraSettingsStore.Load();
            s.BearerToken = string.Empty;
            s.TokenLastUpdatedUtc = null;
            ShipEntegraSettingsStore.Save(s);
        }

        Report($"🔴 {providerName} oturumu kapatıldı ve bağlantı kesildi.", CarrierHubStatusKind.Error);
        RebuildAccounts();
        SessionsChanged?.Invoke();
    }

    private Control CreateCarrierAccountCard(
        string title,
        Image? logo,
        bool isConnected,
        Func<Task> onAutoLogin,
        Func<Task>? onBrowserLogin,
        Func<Task>? onDisconnect,
        string autoLabel = "⚡ Otomatik")
    {
        var card = new Panel
        {
            Width = 245,
            Height = 84,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 0, 8, 0)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Logo kutusu
        var pnlLogo = new Panel
        {
            Width = 52,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(2),
            Margin = new Padding(0, 14, 4, 0)
        };
        pnlLogo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlLogo.Width - 1, pnlLogo.Height - 1);
        };
        var pic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = logo,
            BackColor = Color.Transparent
        };
        pnlLogo.Controls.Add(pic);
        layout.Controls.Add(pnlLogo, 0, 0);

        // Sağ taraf (Başlık + Durum + Butonlar)
        var pnlRight = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        pnlRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        pnlRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        // Satır 1: Başlık & Durum Rozeti
        var pnlTitleStatus = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        var lblName = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Bold", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 4, 6, 0)
        };
        var lblStatusBadge = new Label
        {
            Text = isConnected ? "● Bağlı" : "⚠️ Giriş Gerekli",
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            ForeColor = isConnected ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 146, 60),
            BackColor = isConnected ? Color.FromArgb(6, 78, 59) : Color.FromArgb(124, 45, 18),
            Padding = new Padding(4, 2, 4, 2),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        pnlTitleStatus.Controls.Add(lblName);
        pnlTitleStatus.Controls.Add(lblStatusBadge);
        pnlRight.Controls.Add(pnlTitleStatus, 0, 0);

        // Satır 2: İşlem Butonları
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var btnAuto = new Button
        {
            Text = autoLabel,
            Height = 28,
            Width = 86,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 4, 0)
        };
        btnAuto.FlatAppearance.BorderSize = 0;
        _cardToolTip.SetToolTip(btnAuto, $"{title} için oturum aç / kayıtlı bilgilerle bağlan");
        btnAuto.Click += async (_, _) => await onAutoLogin();
        pnlButtons.Controls.Add(btnAuto);

        if (onBrowserLogin != null)
        {
            var btnBrowser = new Button
            {
                Text = "🌐",
                Height = 28,
                Width = 32,
                BackColor = Color.FromArgb(99, 102, 241),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0)
            };
            btnBrowser.FlatAppearance.BorderSize = 0;
            _cardToolTip.SetToolTip(btnBrowser, $"{title} web sitesini tarayıcıda açarak oturum aç ve entegre et");
            btnBrowser.Click += async (_, _) => await onBrowserLogin();
            pnlButtons.Controls.Add(btnBrowser);
        }

        if (onDisconnect != null)
        {
            var btnDisconnect = new Button
            {
                Text = "🚪",
                Height = 28,
                Width = 32,
                BackColor = isConnected ? Color.FromArgb(220, 38, 38) : Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Emoji", 8.5F),
                Cursor = isConnected ? Cursors.Hand : Cursors.Default,
                Margin = new Padding(0)
            };
            btnDisconnect.FlatAppearance.BorderSize = 0;
            _cardToolTip.SetToolTip(btnDisconnect, isConnected
                ? $"{title} Bağlantısını Kes ve Oturumu Kapat"
                : $"{title} için aktif oturum yok");
            btnDisconnect.Click += async (_, _) => await onDisconnect();
            pnlButtons.Controls.Add(btnDisconnect);
        }

        pnlRight.Controls.Add(pnlButtons, 0, 1);
        layout.Controls.Add(pnlRight, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateAddCarrierPlaceholderCard()
    {
        var card = new Panel
        {
            Width = 160,
            Height = 84,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(71, 85, 105), 1.5f) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3);
        };
        var lbl = new Label
        {
            Text = "➕ Yeni Firma Ekle\n(DHL, PTT, vb.)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8.5F),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        lbl.Click += (_, _) => MessageBox.Show(
            "Yeni Kargo Entegrasyonu:\nYakında özel API Key / Secret girerek Navlungo, DHL, PTT ve diğer taşıyıcıları doğrudan bu merkeze ekleyebileceksiniz.",
            "Çoklu Kargo Entegrasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        card.Controls.Add(lbl);
        return card;
    }

    private static Image? LoadLogoSafely(string fileName)
    {
        // 1. Önce doğrudan diskteki fiziksel varlığı kontrol et (en güncel dosya öncelikli)
        try
        {
            string[] probePaths =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Shipping", fileName),
                Path.Combine(Application.StartupPath, "Assets", "Shipping", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Shipping", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Shipping", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "Shipping", fileName)
            };

            foreach (var p in probePaths)
            {
                if (File.Exists(p))
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    if (bytes.Length > 0)
                    {
                        var ms = new MemoryStream(bytes);
                        return Image.FromStream(ms);
                    }
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }

        // 2. Ardından Assembly Manifest Resources (EmbeddedResource) içini tara
        try
        {
            var asm = typeof(CarrierAccountsHubControl).Assembly;
            var resNames = asm.GetManifestResourceNames();
            var matchedRes = resNames.FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchedRes))
            {
                using var resStream = asm.GetManifestResourceStream(matchedRes);
                if (resStream != null)
                {
                    var ms = new MemoryStream();
                    resStream.CopyTo(ms);
                    ms.Position = 0;
                    return Image.FromStream(ms);
                }
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }

        // 3. Fallback: Dinamik Vektörel Kurumsal Logo Çiz
        return CreateFallbackLogo(fileName);
    }

    private static Image CreateFallbackLogo(string fileName)
    {
        var bmp = new Bitmap(160, 90);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        if (fileName.Contains("aras", StringComparison.OrdinalIgnoreCase))
        {
            // Aras Global Kırmızı Logo
            using var brush = new SolidBrush(Color.FromArgb(220, 38, 38)); // Red 600
            Point[] triangle = { new(110, 15), new(150, 75), new(110, 75) };
            g.FillPolygon(brush, triangle);

            using var fontBold = new Font("Segoe UI Black", 18F, FontStyle.Bold);
            g.DrawString("aras", fontBold, brush, new PointF(10, 18));
            using var fontGray = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            using var grayBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
            g.DrawString("global", fontGray, grayBrush, new PointF(14, 50));
        }
        else if (fileName.Contains("navlungo", StringComparison.OrdinalIgnoreCase))
        {
            // Navlungo Resmi Mavi Logo
            using var blueBrush = new SolidBrush(Color.FromArgb(0, 0, 255));
            using var fontBold = new Font("Segoe UI Black", 16F, FontStyle.Bold);
            g.DrawString("Navlungo", fontBold, blueBrush, new PointF(15, 26));
        }
        else if (fileName.Contains("shiptomore", StringComparison.OrdinalIgnoreCase))
        {
            // Shiptomore Kırmızı & Turuncu Logo
            using var redBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
            using var fontBold = new Font("Segoe UI Black", 13F, FontStyle.Bold);
            g.DrawString("SHIP TO", fontBold, redBrush, new PointF(10, 16));
            using var fontMore = new Font("Segoe UI Black", 15F, FontStyle.Bold);
            using var orangeBrush = new SolidBrush(Color.FromArgb(234, 88, 12));
            g.DrawString("MORE", fontMore, orangeBrush, new PointF(10, 42));
        }
        else
        {
            // ShipEntegra Zümrüt & Lacivert Logo
            using var hexBrush = new LinearGradientBrush(new Rectangle(10, 15, 45, 60), Color.FromArgb(14, 165, 233), Color.FromArgb(16, 185, 129), 45f);
            g.FillRectangle(hexBrush, 15, 20, 35, 50);

            using var fontBold = new Font("Segoe UI Black", 12F, FontStyle.Bold);
            using var darkBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
            g.DrawString("ShipEntegra", fontBold, darkBrush, new PointF(55, 30));
        }

        return bmp;
    }
}

/// <summary>Hesap merkezi durum mesajı türü.</summary>
public enum CarrierHubStatusKind
{
    Info,
    Success,
    Error
}
