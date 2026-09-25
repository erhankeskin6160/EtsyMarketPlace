namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Kâr Simülatörü içine entegre, aşağı doğru pürüzsüz animasyonla açılan,
/// hem ana sağlayıcı (Aras Global / ShipEntegra) hem de alt taşıyıcı (UPS, FedEx, Widect vb.)
/// logolarını içeren, tam responsive canlı kargo karşılaştırma ve seçim çekmecesi.
/// </summary>
public sealed class AnimatedShippingComparisonDrawer : Panel
{
    public event Action<decimal, string>? OnOfferSelected;
    public event Action<bool>? OnExpansionChanged;

    private readonly Timer _animTimer = new() { Interval = 16 };
    private int _targetHeight = 0;
    private const int ExpandedHeight = 650;
    private bool _isExpanded = false;

    // Girdi alanları
    private readonly ComboBox _cbCountry = new();
    private readonly NumericUpDown _numWeight = new();
    private readonly NumericUpDown _numWidth = new();
    private readonly NumericUpDown _numLength = new();
    private readonly NumericUpDown _numHeight = new();
    private readonly Label _lblDesi = new();
    private readonly Button _btnFetchQuotes = new();
    private readonly Button _btnClose = new();
    private readonly Label _lblStatus = new();

    // Çoklu Kargo Entegrasyon Hub'ı
    private readonly Panel _pnlAccountsHub = new();
    private readonly FlowLayoutPanel _pnlAccountsFlow = new();
    private readonly Button _btnToggleAccounts = new();
    private readonly IShippingSessionManager _sessionManager = new PuppeteerShippingSessionManager();
    private readonly ToolTip _cardToolTip = new() { InitialDelay = 300, ReshowDelay = 150 };

    // Filtreleme ve Sıralama
    private readonly ComboBox _cbSort = new();
    private string _selectedProviderFilter = "Tümü";
    private readonly FlowLayoutPanel _pnlFilterPills = new();

    // Teklif Listesi Paneli
    private readonly FlowLayoutPanel _pnlOffersList = new();

    // Logolar
    private readonly Image? _arasLogo;
    private readonly Image? _shipEntegraLogo;
    private readonly Image? _navlungoLogo;
    private readonly Image? _shiptomoreLogo;
    private static Image? _upsLogo;
    private static Image? _widectLogo;
    private static Image? _fedexLogo;
    private static readonly Dictionary<string, Image> _carrierLogoCache = new(StringComparer.OrdinalIgnoreCase);

    // Servisler
    private readonly ArasGlobalPricingService _arasService = new();
    private readonly ShipEntegraPricingService _shipEntegraService = new();
    private readonly INavlungoApiClient _navlungoApiClient = new NavlungoApiClient();
    private readonly IShiptomoreApiClient _shiptomoreApiClient = new ShiptomoreApiClient();

    // Canlı Döviz Kuru
    public decimal UsdTryRate { get; set; } = 48.26m;

    // Teklif Listesi Önbelleği
    private readonly List<UnifiedShippingQuote> _loadedQuotes = new();

    public bool IsExpanded => _isExpanded;

    public AnimatedShippingComparisonDrawer()
    {
        Dock = DockStyle.Top;
        Height = 0;
        Visible = false;
        BackColor = Color.FromArgb(17, 24, 39); // Gray 900
        Padding = new Padding(16, 12, 16, 12);
        DoubleBuffered = true;

        // Logoları yükle
        _arasLogo = LoadLogoSafely("aras_global.png");
        _shipEntegraLogo = LoadLogoSafely("shipentegra.jpg") ?? LoadLogoSafely("shipentegra.png");
        _upsLogo ??= LoadLogoSafely("ups.png");
        _widectLogo ??= LoadLogoSafely("widect.png");
        _navlungoLogo = LoadLogoSafely("navlungo.png");
        _shiptomoreLogo = LoadLogoSafely("shiptomore.png");
        _fedexLogo ??= LoadLogoSafely("fedex.png");

        _animTimer.Tick += AnimTimer_Tick;

        BuildLayout();
        CalculateDesi();

        _pnlOffersList.ClientSizeChanged += (_, _) => UpdateCardsWidth();
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
        catch { }

        // 2. Ardından Assembly Manifest Resources (EmbeddedResource) içini tara
        try
        {
            var asm = typeof(AnimatedShippingComparisonDrawer).Assembly;
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
        catch { }

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

    /// <summary>
    /// Taşıyıcı firmanın (UPS, Widect, FedEx, TNT vb.) yüksek kaliteli kurumsal logosunu döner veya üretir.
    /// </summary>
    private static Image GetCarrierMiniLogo(string subCarrier, string serviceName, string note = "")
    {
        string key = $"{subCarrier}_{serviceName}_{note}".ToLowerInvariant();
        if (_carrierLogoCache.TryGetValue(key, out var cached)) return cached;

        // 1. FedEx Resmi Logosu (FedEx veya ShipEntegra Smart FedEx/TNT altyapılı servisler öncelikli)
        if (key.Contains("fedex") || key.Contains("smart"))
        {
            _fedexLogo ??= LoadLogoSafely("fedex.png");
            if (_fedexLogo != null)
            {
                _carrierLogoCache[key] = _fedexLogo;
                return _fedexLogo;
            }
        }

        // 2. Widect Resmi Logosu
        if (key.Contains("widect"))
        {
            _widectLogo ??= LoadLogoSafely("widect.png");
            if (_widectLogo != null)
            {
                _carrierLogoCache[key] = _widectLogo;
                return _widectLogo;
            }
        }

        // 3. UPS Resmi Logosu
        if (key.Contains("ups"))
        {
            _upsLogo ??= LoadLogoSafely("ups.png");
            if (_upsLogo != null)
            {
                _carrierLogoCache[key] = _upsLogo;
                return _upsLogo;
            }
        }

        var bmp = new Bitmap(68, 36);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (key.Contains("fedex") || key.Contains("smart"))
        {
            // FedEx Vektörel Fallback: Mor "Fed" ve Turuncu "Ex"
            g.Clear(Color.FromArgb(255, 255, 255));
            using var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1f);
            g.DrawRectangle(borderPen, 0, 0, bmp.Width - 1, bmp.Height - 1);

            using var font = new Font("Segoe UI Black", 10.5F, FontStyle.Bold);
            using var purpleBrush = new SolidBrush(Color.FromArgb(77, 20, 140)); // FedEx Purple
            using var orangeBrush = new SolidBrush(Color.FromArgb(255, 102, 0)); // FedEx Orange

            g.DrawString("Fed", font, purpleBrush, new PointF(6, 7));
            g.DrawString("Ex", font, orangeBrush, new PointF(35, 7));
        }
        else if (key.Contains("ups"))
        {
            // UPS Fallback: Kahverengi kalkan ve altın sarısı "ups"
            g.Clear(Color.FromArgb(53, 26, 12)); // UPS Brown
            using var shieldBrush = new SolidBrush(Color.FromArgb(255, 181, 0)); // UPS Gold
            Point[] shield = { new(34, 2), new(62, 7), new(54, 28), new(34, 34), new(14, 28), new(6, 7) };
            g.DrawPolygon(new Pen(Color.FromArgb(255, 181, 0), 1.5f), shield);

            using var font = new Font("Segoe UI Black", 10F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(255, 181, 0));
            g.DrawString("ups", font, textBrush, new PointF(18, 8));
        }
        else if (key.Contains("widect"))
        {
            // Widect Fallback: Koyu zemin, mavi/turuncu kanat
            g.Clear(Color.FromArgb(15, 23, 42));
            using var pen = new Pen(Color.FromArgb(14, 165, 233), 1f);
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);

            using var cyanBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            using var orangeBrush = new SolidBrush(Color.FromArgb(251, 146, 60));

            Point[] wing = { new(6, 8), new(20, 18), new(6, 28) };
            g.FillPolygon(cyanBrush, wing);

            using var font = new Font("Segoe UI Black", 9F, FontStyle.Bold);
            g.DrawString("WID", font, cyanBrush, new PointF(22, 10));
            g.DrawString("ECT", font, orangeBrush, new PointF(42, 10));
        }
        else if (key.Contains("tnt"))
        {
            // TNT: Turuncu zemin, beyaz yazı
            g.Clear(Color.FromArgb(234, 88, 12));
            using var font = new Font("Segoe UI Black", 10.5F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("TNT", font, brush, new PointF(15, 7));
        }
        else
        {
            // Eko Plus / Standart Ekspres Rozeti
            g.Clear(Color.FromArgb(6, 78, 59)); // Emerald Dark
            using var pen = new Pen(Color.FromArgb(16, 185, 129), 1f);
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);

            using var font = new Font("Segoe UI Black", 9F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.DrawString("⚡EKO", font, brush, new PointF(11, 9));
        }

        _carrierLogoCache[key] = bmp;
        return bmp;
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        int diff = _targetHeight - Height;
        if (Math.Abs(diff) <= 12)
        {
            Height = _targetHeight;
            _animTimer.Stop();
            if (_targetHeight == 0)
            {
                Visible = false;
            }
            OnExpansionChanged?.Invoke(_isExpanded);
        }
        else
        {
            // Smooth ease-out lerp
            Height += (int)(diff * 0.28f);
        }
    }

    public void ToggleDrawer()
    {
        if (_isExpanded) Collapse();
        else Expand();
    }

    public void Expand()
    {
        _isExpanded = true;
        Visible = true;
        _targetHeight = ExpandedHeight;
        _animTimer.Start();
        if (_loadedQuotes.Count == 0)
        {
            _ = FetchAllQuotesAsync();
        }
    }

    public void Collapse()
    {
        _isExpanded = false;
        _targetHeight = 0;
        _animTimer.Start();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 0: Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));  // 1: Multi-Carrier Hub
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116)); // 2: 2-Row Responsive Inputs Card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 3: Offers List

        // 0: Header
        mainLayout.Controls.Add(BuildHeaderRow(), 0, 0);

        // 1: Multi-Carrier Accounts Hub
        mainLayout.Controls.Add(BuildAccountsHub(), 0, 1);

        // 2: Responsive 2-Row Inputs Card
        mainLayout.Controls.Add(BuildResponsiveInputsCard(), 0, 2);

        // 3: Offers List Container
        _pnlOffersList.Dock = DockStyle.Fill;
        _pnlOffersList.AutoScroll = true;
        _pnlOffersList.FlowDirection = FlowDirection.TopDown;
        _pnlOffersList.WrapContents = false;
        _pnlOffersList.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        _pnlOffersList.Padding = new Padding(8);
        mainLayout.Controls.Add(_pnlOffersList, 0, 3);

        Controls.Add(mainLayout);
    }

    private Control BuildHeaderRow()
    {
        var pnl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var lblTitle = new Label
        {
            Text = "🌐 Çoklu Kargo Karşılaştırma & Canlı Hesap Yönetim Merkezi",
            ForeColor = Color.FromArgb(56, 189, 248), // Cyan
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnl.Controls.Add(lblTitle, 0, 0);

        _btnToggleAccounts.Text = "🔌 Kargo Hesapları & Oturumlar ▲";
        _btnToggleAccounts.Height = 28;
        _btnToggleAccounts.AutoSize = true;
        _btnToggleAccounts.BackColor = Color.FromArgb(30, 41, 59);
        _btnToggleAccounts.ForeColor = Color.FromArgb(56, 189, 248);
        _btnToggleAccounts.FlatStyle = FlatStyle.Flat;
        _btnToggleAccounts.FlatAppearance.BorderColor = Color.FromArgb(56, 189, 248);
        _btnToggleAccounts.FlatAppearance.BorderSize = 1;
        _btnToggleAccounts.Cursor = Cursors.Hand;
        _btnToggleAccounts.Font = new Font("Segoe UI Semibold", 8.5F);
        _btnToggleAccounts.Margin = new Padding(0, 4, 10, 4);
        _btnToggleAccounts.Click += (_, _) =>
        {
            _pnlAccountsHub.Visible = !_pnlAccountsHub.Visible;
            _btnToggleAccounts.Text = _pnlAccountsHub.Visible
                ? "🔌 Kargo Hesapları & Oturumlar ▲"
                : "🔌 Kargo Hesapları & Oturumlar ▼";
        };
        pnl.Controls.Add(_btnToggleAccounts, 1, 0);

        _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
        _lblStatus.Font = new Font("Segoe UI", 9F);
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.TextAlign = ContentAlignment.MiddleRight;
        _lblStatus.AutoEllipsis = true;
        _lblStatus.Margin = new Padding(0, 0, 10, 0);
        pnl.Controls.Add(_lblStatus, 2, 0);

        _btnClose.Text = "▲ Kapat";
        _btnClose.Dock = DockStyle.Fill;
        _btnClose.BackColor = Color.FromArgb(51, 65, 85);
        _btnClose.ForeColor = Color.White;
        _btnClose.FlatStyle = FlatStyle.Flat;
        _btnClose.Cursor = Cursors.Hand;
        _btnClose.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Click += (_, _) => Collapse();
        pnl.Controls.Add(_btnClose, 3, 0);

        return pnl;
    }

    private Control BuildAccountsHub()
    {
        _pnlAccountsHub.Dock = DockStyle.Fill;
        _pnlAccountsHub.BackColor = Color.Transparent;
        _pnlAccountsHub.Margin = new Padding(0, 0, 0, 6);

        _pnlAccountsFlow.Dock = DockStyle.Fill;
        _pnlAccountsFlow.FlowDirection = FlowDirection.LeftToRight;
        _pnlAccountsFlow.WrapContents = false;
        _pnlAccountsFlow.AutoScroll = true;
        _pnlAccountsFlow.BackColor = Color.Transparent;

        RebuildAccountsHub();

        _pnlAccountsHub.Controls.Add(_pnlAccountsFlow);
        return _pnlAccountsHub;
    }

    private void RebuildAccountsHub()
    {
        _pnlAccountsFlow.Controls.Clear();

        var arasSettings = ArasGlobalSettingsStore.Load();
        bool arasConnected = arasSettings.HasValidTokenFormat;

        var seSettings = ShipEntegraSettingsStore.Load();
        bool seConnected = seSettings.HasValidTokenFormat;

        // 1. Aras Global Kartı
        _pnlAccountsFlow.Controls.Add(CreateCarrierAccountCard(
            "Aras Global",
            _arasLogo ?? CreateFallbackLogo("aras"),
            arasConnected,
            () => TriggerArasAutoLoginAsync(false),
            () => TriggerArasAutoLoginAsync(true),
            () => DisconnectCarrierAsync("Aras Global")));

        // 2. ShipEntegra Kartı
        _pnlAccountsFlow.Controls.Add(CreateCarrierAccountCard(
            "ShipEntegra",
            _shipEntegraLogo ?? CreateFallbackLogo("shipentegra"),
            seConnected,
            () => TriggerShipEntegraAutoLoginAsync(false),
            () => TriggerShipEntegraAutoLoginAsync(true),
            () => DisconnectCarrierAsync("ShipEntegra")));

        // 3. Navlungo Kartı
        var navSettings = NavlungoSettingsStore.Load();
        bool navConnected = !string.IsNullOrWhiteSpace(navSettings.IdToken) || !string.IsNullOrWhiteSpace(navSettings.SessionCookie);

        _pnlAccountsFlow.Controls.Add(CreateCarrierAccountCard(
            "Navlungo",
            _navlungoLogo ?? CreateFallbackLogo("navlungo"),
            navConnected,
            () => TriggerNavlungoAutoLoginAsync(false),
            () => TriggerNavlungoAutoLoginAsync(true),
            () => DisconnectCarrierAsync("Navlungo")));

        // 4. Shiptomore Kartı
        var stmSettings = ShiptomoreSettingsStore.Load();
        bool stmConnected = !string.IsNullOrWhiteSpace(stmSettings.SessionCookie);

        _pnlAccountsFlow.Controls.Add(CreateCarrierAccountCard(
            "Shiptomore",
            _shiptomoreLogo ?? CreateFallbackLogo("shiptomore"),
            stmConnected,
            () => TriggerShiptomoreAutoLoginAsync(false),
            () => TriggerShiptomoreAutoLoginAsync(true),
            () => DisconnectCarrierAsync("Shiptomore")));

        // 5. + Yeni Firma Ekle Kartı
        _pnlAccountsFlow.Controls.Add(CreateAddCarrierPlaceholderCard());
    }

    private Control CreateCarrierAccountCard(
        string title, 
        Image? logo, 
        bool isConnected, 
        Func<Task> onAutoLogin, 
        Func<Task>? onBrowserLogin, 
        Func<Task>? onDisconnect)
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
            Text = "⚡ Otomatik",
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
        _cardToolTip.SetToolTip(btnAuto, $"{title} için otomatik / kayıtlı bilgilerle giriş yap");
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

    private Control BuildResponsiveInputsCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59), // Slate 800
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 0, 8)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        // Sütun Genişlikleri:
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // 0: Hedef Ülke
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // 1: Ağırlık (kg)
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // 2: En (cm)
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // 3: Boy (cm)
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // 4: Yükseklik (cm)
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 5: Desi Rozeti & Butonlar
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));     // Satır 0: Parametreler
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));     // Satır 1: Filtreler & Aksiyon

        // --- SATIR 0: PARAMETRELER ---
        grid.Controls.Add(CreateFieldWrapper("Hedef Ülke:", _cbCountry), 0, 0);
        PopulateCountries();

        ConfigureNumeric(_numWeight, 0.01m, 70m, 0.40m, 2);
        grid.Controls.Add(CreateFieldWrapper("Ağırlık (kg):", _numWeight), 1, 0);

        ConfigureNumeric(_numWidth, 1m, 200m, 15m, 1);
        ConfigureNumeric(_numLength, 1m, 200m, 20m, 1);
        ConfigureNumeric(_numHeight, 1m, 200m, 10m, 1);

        _numWidth.ValueChanged += (_, _) => CalculateDesi();
        _numLength.ValueChanged += (_, _) => CalculateDesi();
        _numHeight.ValueChanged += (_, _) => CalculateDesi();
        _numWeight.ValueChanged += (_, _) => CalculateDesi();

        grid.Controls.Add(CreateFieldWrapper("En (cm):", _numWidth), 2, 0);
        grid.Controls.Add(CreateFieldWrapper("Boy (cm):", _numLength), 3, 0);
        grid.Controls.Add(CreateFieldWrapper("Yük. (cm):", _numHeight), 4, 0);

        var pnlDesiBox = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(8, 16, 0, 2)
        };
        _lblDesi.Dock = DockStyle.Fill;
        _lblDesi.ForeColor = Color.FromArgb(52, 211, 153); // Emerald 400
        _lblDesi.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _lblDesi.TextAlign = ContentAlignment.MiddleCenter;
        pnlDesiBox.Controls.Add(_lblDesi);
        grid.Controls.Add(pnlDesiBox, 5, 0);

        // --- SATIR 1: FİLTRELER, SIRALAMA & TEKLİFLERİ GETİR BUTONU ---
        var row1Layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        row1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // 0: Filtre Hapları (asla kesilmez, tam sığar)
        row1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // 1: Sıralama Dropdown (ferah ve kompakt)
        row1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 2: Teklifleri Getir Butonu

        _pnlFilterPills.Dock = DockStyle.Fill;
        _pnlFilterPills.FlowDirection = FlowDirection.LeftToRight;
        _pnlFilterPills.WrapContents = false;
        _pnlFilterPills.AutoSize = true;
        _pnlFilterPills.BackColor = Color.Transparent;
        _pnlFilterPills.Margin = new Padding(0, 6, 8, 0);

        BuildFilterPills();
        row1Layout.Controls.Add(_pnlFilterPills, 0, 0);

        // Orta Sıralama Dropdown
        var pnlSort = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 8, 0)
        };
        pnlSort.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        pnlSort.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblSort = new Label
        {
            Text = "Sırala:",
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI Semibold", 8.5F)
        };
        pnlSort.Controls.Add(lblSort, 0, 0);

        _cbSort.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbSort.BackColor = Color.FromArgb(15, 23, 42);
        _cbSort.ForeColor = Color.White;
        _cbSort.Font = new Font("Segoe UI", 8.5F);
        _cbSort.Dock = DockStyle.Fill;
        _cbSort.Items.Add("🔽 Fiyat (En Ucuz)");
        _cbSort.Items.Add("🔼 Fiyat (En Pahalı)");
        _cbSort.Items.Add("⚡ En Hızlı Teslimat");
        _cbSort.SelectedIndex = 0;
        _cbSort.SelectedIndexChanged += (_, _) => RenderFilteredOffers();
        pnlSort.Controls.Add(_cbSort, 1, 0);

        row1Layout.Controls.Add(pnlSort, 1, 0);

        // Sağ Aksiyon Butonu ("⚡ Teklifleri Getir")
        _btnFetchQuotes.Text = "⚡ Teklifleri Getir";
        _btnFetchQuotes.Dock = DockStyle.Fill;
        _btnFetchQuotes.BackColor = Color.FromArgb(16, 185, 129); // Emerald 500
        _btnFetchQuotes.ForeColor = Color.White;
        _btnFetchQuotes.FlatStyle = FlatStyle.Flat;
        _btnFetchQuotes.Cursor = Cursors.Hand;
        _btnFetchQuotes.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _btnFetchQuotes.FlatAppearance.BorderSize = 0;
        _btnFetchQuotes.Margin = new Padding(4, 6, 0, 0);
        _btnFetchQuotes.Click += async (_, _) => await FetchAllQuotesAsync();
        row1Layout.Controls.Add(_btnFetchQuotes, 2, 0);

        grid.Controls.Add(row1Layout, 0, 1);
        grid.SetColumnSpan(row1Layout, 6);

        card.Controls.Add(grid);
        return card;
    }

    private void BuildFilterPills()
    {
        _pnlFilterPills.Controls.Clear();
        string[] providers = { "Tümü", "Aras Global", "ShipEntegra", "Navlungo", "Shiptomore" };

        foreach (var p in providers)
        {
            bool isSelected = _selectedProviderFilter == p;
            var btn = new Button
            {
                Text = p == "Tümü" ? "🌐 Tümü" : (p == "Aras Global" ? "🚚 Aras Global" : (p == "ShipEntegra" ? "📦 ShipEntegra" : (p == "Navlungo" ? "🔵 Navlungo" : "🔴 Shiptomore"))),
                Height = 30,
                AutoSize = true,
                BackColor = isSelected ? Color.FromArgb(16, 185, 129) : Color.FromArgb(15, 23, 42),
                ForeColor = isSelected ? Color.White : Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8F, isSelected ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0),
                Padding = new Padding(6, 0, 6, 0)
            };
            btn.FlatAppearance.BorderColor = isSelected ? Color.FromArgb(52, 211, 153) : Color.FromArgb(51, 65, 85);
            btn.FlatAppearance.BorderSize = 1;

            string current = p;
            btn.Click += (_, _) =>
            {
                _selectedProviderFilter = current;
                BuildFilterPills();
                RenderFilteredOffers();
            };
            _pnlFilterPills.Controls.Add(btn);
        }
    }

    private void CalculateDesi()
    {
        decimal w = _numWidth.Value;
        decimal l = _numLength.Value;
        decimal h = _numHeight.Value;
        decimal weight = _numWeight.Value;

        decimal desi = Math.Round((w * l * h) / 5000m, 2);
        decimal chargeable = Math.Max(desi, weight);

        _lblDesi.Text = $"📐 Desi: {desi:0.00}  |  ⚖️ Faturalandırılacak: {chargeable:0.00} kg";
    }

    public async Task FetchAllQuotesAsync()
    {
        _btnFetchQuotes.Enabled = false;
        _btnFetchQuotes.Text = "⏳ Fiyatlar Çekiliyor...";
        _lblStatus.Text = "Canlı API'ler sorgulanıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        _pnlOffersList.Controls.Clear();
        var lblWait = new Label
        {
            Text = "🔄 Aras Global ve ShipEntegra sunucularından anlık canlı navlun teklifleri çekiliyor, lütfen bekleyiniz...",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12),
            AutoSize = true
        };
        _pnlOffersList.Controls.Add(lblWait);

        _loadedQuotes.Clear();

        string countryCode = GetSelectedCountryCode();
        double weight = (double)_numWeight.Value;
        double width = (double)_numWidth.Value;
        double length = (double)_numLength.Value;
        double height = (double)_numHeight.Value;

        // 1. Aras Global Sorgusu
        var arasTask = Task.Run(async () =>
        {
            try
            {
                var req = new ArasGlobalQuoteRequest
                {
                    ReceiverCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _arasService.GetQuotesAsync(req);
            }
            catch
            {
                return null;
            }
        });

        // 2. ShipEntegra Sorgusu
        var shipEntegraTask = Task.Run(async () =>
        {
            try
            {
                var req = new ShipEntegraQuoteRequest
                {
                    ReceiverCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _shipEntegraService.GetQuotesAsync(req);
            }
            catch
            {
                return null;
            }
        });

        // 3. Navlungo Sorgusu
        var navSettings = NavlungoSettingsStore.Load();
        string? navlungoError = null;
        var navlungoTask = Task.Run(async () =>
        {
            try
            {
                var req = new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height,
                    Source = "user"
                };
                return await _navlungoApiClient.FetchLiveQuotesAsync(req, navSettings);
            }
            catch (NavlungoApiException ex)
            {
                navlungoError = ex.StatusCode is { } status
                    ? $"HTTP {(int)status}: {ex.Message}"
                    : ex.Message;
                return NavlungoApiClient.GenerateRealisticFallbackQuotes(new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height,
                    Source = "user"
                });
            }
            catch (Exception ex)
            {
                navlungoError = ex.Message;
                return NavlungoApiClient.GenerateRealisticFallbackQuotes(new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height,
                    Source = "user"
                });
            }
        });

        // 4. Shiptomore Sorgusu
        var stmSettings = ShiptomoreSettingsStore.Load();
        string? shiptomoreError = null;
        var shiptomoreTask = Task.Run(async () =>
        {
            try
            {
                var req = new ShiptomoreQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _shiptomoreApiClient.FetchLiveQuotesAsync(req, stmSettings);
            }
            catch (Exception ex)
            {
                shiptomoreError = ex.Message;
                return ShiptomoreApiClient.GenerateRealisticFallbackQuotes(new ShiptomoreQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                }, !string.IsNullOrWhiteSpace(stmSettings.SessionCookie));
            }
        });

        await Task.WhenAll(arasTask, shipEntegraTask, navlungoTask, shiptomoreTask);

        var arasRes = await arasTask;
        var seRes = await shipEntegraTask;
        var navOffers = await navlungoTask;
        var stmOffers = await shiptomoreTask;

        // 1. Aras Tekliflerini Ekle
        if (arasRes != null && arasRes.Success && arasRes.Offers.Count > 0)
        {
            foreach (var off in arasRes.Offers)
            {
                string sName = $"{off.Cargo} {off.ProviderServiceType}".Trim();
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "Aras Global",
                    ServiceName = sName,
                    SubCarrier = off.Cargo,
                    PriceUsd = off.Price,
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryText = off.DeliveryDaysText,
                    DeliveryDaysMin = off.EstimatedStartDeliveryDate,
                    DeliveryDaysMax = off.EstimatedEndDeliveryDate,
                    Note = off.IsLivePrice ? "Aras Global Canlı Entegrasyon" : "Yedek Tarife",
                    IsLive = off.IsLivePrice
                });
            }
        }

        // 2. ShipEntegra Tekliflerini Ekle
        if (seRes != null && seRes.Success && seRes.Offers.Count > 0)
        {
            foreach (var off in seRes.Offers)
            {
                string sName = !string.IsNullOrWhiteSpace(off.ClearServiceName) ? off.ClearServiceName : off.ServiceName;
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "ShipEntegra",
                    ServiceName = sName,
                    SubCarrier = ExtractSubCarrierName(off.ServiceName),
                    PriceUsd = off.TotalPrice,
                    PriceTry = Math.Round(off.TotalPrice * UsdTryRate, 2),
                    DeliveryText = ExtractDeliveryTime(off.AdditionalDescription),
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.AdditionalDescription),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.AdditionalDescription),
                    Note = CleanHtml(off.AdditionalDescription),
                    IsLive = off.IsLivePrice
                });
            }
        }

        // 3. Navlungo Tekliflerini Ekle (Canlı Widect, FedEx, UPS)
        if (navOffers != null && navOffers.Count > 0)
        {
            foreach (var off in navOffers)
            {
                bool isLiveOffer = off.Note.Contains("Canlı", StringComparison.OrdinalIgnoreCase) || 
                                   (!off.Note.Contains("Referans", StringComparison.OrdinalIgnoreCase) && !off.Note.Contains("Yedek", StringComparison.OrdinalIgnoreCase));
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "Navlungo",
                    ServiceName = off.ServiceName,
                    SubCarrier = off.Carrier,
                    PriceUsd = off.Price,
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryText = off.DeliveryEstimate,
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.DeliveryEstimate),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.DeliveryEstimate),
                    Note = off.Note,
                    IsLive = isLiveOffer
                });
            }
        }

        // 4. Shiptomore Tekliflerini Ekle (Canlı Widect, FedEx, UPS)
        if (stmOffers != null && stmOffers.Count > 0)
        {
            foreach (var off in stmOffers)
            {
                bool isLiveOffer = off.Note.Contains("Canlı", StringComparison.OrdinalIgnoreCase) || 
                                   (!off.Note.Contains("Simüle", StringComparison.OrdinalIgnoreCase) && !off.Note.Contains("Yedek", StringComparison.OrdinalIgnoreCase));
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "Shiptomore",
                    ServiceName = off.ServiceName,
                    SubCarrier = off.Carrier,
                    PriceUsd = off.Price,
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryText = off.DeliveryEstimate,
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.DeliveryEstimate),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.DeliveryEstimate),
                    Note = off.Note,
                    IsLive = isLiveOffer
                });
            }
        }

        _btnFetchQuotes.Enabled = true;
        _btnFetchQuotes.Text = "⚡ Teklifleri Getir";

        if (_loadedQuotes.Count == 0)
        {
            _lblStatus.Text = "⚠️ Canlı teklif alınamadı (Token eksik veya yetkisiz).";
            _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
        }
        else
        {
            var errors = new List<string>();
            if (navlungoError != null) errors.Add($"Navlungo: {navlungoError}");
            if (shiptomoreError != null) errors.Add($"Shiptomore: {shiptomoreError}");

            _lblStatus.Text = errors.Count == 0
                ? $"✅ {_loadedQuotes.Count} adet alternatif kargo teklifi bulundu."
                : $"✅ {_loadedQuotes.Count} adet kargo teklifi bulundu. ⚠️ {string.Join(" | ", errors)}";
            _lblStatus.ForeColor = errors.Count == 0
                ? Color.FromArgb(52, 211, 153)
                : Color.FromArgb(251, 191, 36);
        }

        RenderFilteredOffers();
    }

    private void RenderFilteredOffers()
    {
        _pnlOffersList.Controls.Clear();

        if (_loadedQuotes.Count == 0)
        {
            var pnlEmpty = new Panel { AutoSize = true, Padding = new Padding(16) };
            var lblEmpty = new Label
            {
                Text = "Teklif bulunamadı veya canlı token geçerli değil.\nLütfen Aras Global veya ShipEntegra ekranından '⚡ Otomatik Giriş' yaparak tokeni güncelleyin.",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9.5F),
                AutoSize = true
            };
            pnlEmpty.Controls.Add(lblEmpty);
            _pnlOffersList.Controls.Add(pnlEmpty);
            return;
        }

        // 1. Sağlayıcı Filtresi
        IEnumerable<UnifiedShippingQuote> filtered = _loadedQuotes;
        if (_selectedProviderFilter != "Tümü")
        {
            filtered = filtered.Where(q => q.Provider.Equals(_selectedProviderFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Sıralama
        filtered = _cbSort.SelectedIndex switch
        {
            1 => filtered.OrderByDescending(q => q.PriceUsd), // Pahalıdan ucuza
            2 => filtered.OrderBy(q => q.DeliveryDaysMin).ThenBy(q => q.PriceUsd), // En hızlı
            _ => filtered.OrderBy(q => q.PriceUsd) // En ucuz (varsayılan)
        };

        var list = filtered.ToList();
        if (list.Count == 0)
        {
            var lbl = new Label { Text = "Seçilen filtrede kargo teklifi bulunamadı.", ForeColor = Color.FromArgb(148, 163, 184), AutoSize = true, Padding = new Padding(12) };
            _pnlOffersList.Controls.Add(lbl);
            return;
        }

        decimal minPrice = _loadedQuotes.Min(q => q.PriceUsd);

        int targetWidth = Math.Max(500, _pnlOffersList.ClientSize.Width - 24);

        foreach (var quote in list)
        {
            bool isCheapest = quote.PriceUsd == minPrice;
            var card = CreateOfferCard(quote, isCheapest, targetWidth);
            _pnlOffersList.Controls.Add(card);
        }
    }

    private void UpdateCardsWidth()
    {
        int targetWidth = Math.Max(500, _pnlOffersList.ClientSize.Width - 24);
        foreach (Control c in _pnlOffersList.Controls)
        {
            if (c is Panel p)
            {
                p.Width = targetWidth;
            }
        }
    }

    private Control CreateOfferCard(UnifiedShippingQuote quote, bool isCheapest, int cardWidth)
    {
        var card = new Panel
        {
            Width = cardWidth,
            Height = 74,
            BackColor = isCheapest ? Color.FromArgb(19, 42, 41) : Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 8, 10, 8)
        };

        // Border çizimi
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(isCheapest ? Color.FromArgb(16, 185, 129) : Color.FromArgb(51, 65, 85), isCheapest ? 1.5f : 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));  // 0: Ana Sağlayıcı Logosu (Aras / ShipEntegra)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));  // 1: Taşıyıcı Firma Resmi (UPS, Widect, FedEx vb.)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 2: Servis, Hat & Detay Bilgisi
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); // 3: En Uygun Rozeti
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125)); // 4: Fiyat Bloğu ($ ve TL)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); // 5: Bu Teklifi Kullan Butonu

        // 1. SÜTUN: ANA SAĞLAYICI LOGO KUTUSU
        var pnlLogoBox = new Panel
        {
            Width = 76,
            Height = 48,
            BackColor = Color.FromArgb(248, 250, 252), // Temiz beyaz/açık zemin
            Padding = new Padding(4),
            Margin = new Padding(0, 4, 6, 2)
        };
        pnlLogoBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlLogoBox.Width - 1, pnlLogoBox.Height - 1);
        };

        var picLogo = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        var logoImg = (quote.Provider == "Aras Global" 
                        ? _arasLogo 
                        : (quote.Provider == "ShipEntegra" 
                            ? _shipEntegraLogo 
                            : (quote.Provider == "Navlungo" ? _navlungoLogo : _shiptomoreLogo))) 
                      ?? (quote.Provider == "Aras Global" 
                          ? CreateFallbackLogo("aras") 
                          : (quote.Provider == "ShipEntegra" 
                              ? CreateFallbackLogo("shipentegra") 
                              : (quote.Provider == "Navlungo" ? CreateFallbackLogo("navlungo") : CreateFallbackLogo("shiptomore"))));
        picLogo.Image = logoImg;

        pnlLogoBox.Controls.Add(picLogo);
        layout.Controls.Add(pnlLogoBox, 0, 0);

        // 2. SÜTUN: TAŞIYICI FİRMA RESMİ / LOGOSU (UPS, Widect, FedEx, TNT vb.)
        var pnlCarrierLogoBox = new Panel
        {
            Width = 68,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252), // Temiz beyaz/açık zemin (Widect siyah yazısı ve UPS kalkanı için ferah kontrast)
            Padding = new Padding(3),
            Margin = new Padding(0, 6, 6, 2)
        };
        pnlCarrierLogoBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCarrierLogoBox.Width - 1, pnlCarrierLogoBox.Height - 1);
        };

        var picCarrier = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = GetCarrierMiniLogo(quote.SubCarrier, quote.ServiceName, quote.Note),
            BackColor = Color.Transparent
        };
        pnlCarrierLogoBox.Controls.Add(picCarrier);
        layout.Controls.Add(pnlCarrierLogoBox, 1, 0);

        // 3. SÜTUN: SERVİS BİLGİLERİ & DETAYLAR
        var pnlDetails = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        // Servis Adı
        var lblService = new Label
        {
            Text = quote.ServiceName,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        pnlDetails.Controls.Add(lblService);

        // Alt Rozetler Satırı (Taşıyıcı Adı + Teslimat Süresi)
        var pnlBadgesRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };

        var lblCarrierBadge = new Label
        {
            Text = $"✈️ {quote.SubCarrier}",
            ForeColor = quote.Provider == "Aras Global" ? Color.FromArgb(56, 189, 248) : Color.FromArgb(74, 222, 128),
            BackColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Padding = new Padding(4, 2, 4, 2),
            AutoSize = true,
            Margin = new Padding(0, 0, 6, 0)
        };
        pnlBadgesRow.Controls.Add(lblCarrierBadge);

        var lblDelivery = new Label
        {
            Text = $"⏱ {quote.DeliveryText}",
            ForeColor = Color.FromArgb(226, 232, 240),
            BackColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI Semibold", 8F),
            Padding = new Padding(4, 2, 4, 2),
            AutoSize = true
        };
        pnlBadgesRow.Controls.Add(lblDelivery);

        if (!string.IsNullOrWhiteSpace(quote.Note))
        {
            var lblNote = new Label
            {
                Text = $"• {quote.Note}",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5F),
                AutoSize = true,
                Margin = new Padding(6, 2, 0, 0)
            };
            pnlBadgesRow.Controls.Add(lblNote);
        }

        pnlDetails.Controls.Add(pnlBadgesRow);
        layout.Controls.Add(pnlDetails, 2, 0);

        // 4. SÜTUN: EN UYGUN ROZETİ
        if (isCheapest)
        {
            var pnlBadgeContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var badge = new Label
            {
                Text = "🏆 EN UCUZ",
                BackColor = Color.FromArgb(6, 78, 59),
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI Black", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(95, 28),
                Location = new Point(5, 14)
            };
            pnlBadgeContainer.Controls.Add(badge);
            layout.Controls.Add(pnlBadgeContainer, 3, 0);
        }
        else
        {
            layout.Controls.Add(new Label(), 3, 0);
        }

        // 5. SÜTUN: FİYAT BLOĞU
        var pnlPrice = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var lblPrice = new Label
        {
            Text = $"${quote.PriceUsd:0.00}",
            ForeColor = isCheapest ? Color.FromArgb(52, 211, 153) : Color.FromArgb(56, 189, 248),
            Font = new Font("Segoe UI Black", 12.5F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        var lblPriceTry = new Label
        {
            Text = $"≈ ₺{quote.PriceTry:N2}",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8.5F),
            AutoSize = true
        };
        pnlPrice.Controls.Add(lblPrice);
        pnlPrice.Controls.Add(lblPriceTry);
        layout.Controls.Add(pnlPrice, 4, 0);

        // 6. SÜTUN: BU TEKLİFİ KULLAN BUTONU
        var btnSelect = new Button
        {
            Text = "✅ Teklifi Seç",
            Dock = DockStyle.Fill,
            BackColor = isCheapest ? Color.FromArgb(16, 185, 129) : Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Margin = new Padding(4, 10, 4, 10)
        };
        btnSelect.FlatAppearance.BorderSize = 0;
        btnSelect.Click += (_, _) =>
        {
            OnOfferSelected?.Invoke(quote.PriceUsd, $"{quote.Provider} - {quote.ServiceName}");
            Collapse();
        };
        layout.Controls.Add(btnSelect, 5, 0);

        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateFieldWrapper(string labelText, Control inputControl)
    {
        var box = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
            BackColor = Color.Transparent
        };
        var lbl = new Label
        {
            Text = labelText,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8F),
            Dock = DockStyle.Top,
            Height = 16
        };
        inputControl.Dock = DockStyle.Top;
        box.Controls.Add(inputControl);
        box.Controls.Add(lbl);
        return box;
    }

    private static void ConfigureNumeric(NumericUpDown num, decimal min, decimal max, decimal val, int dec)
    {
        num.Minimum = min;
        num.Maximum = max;
        num.DecimalPlaces = dec;
        num.Value = val;
        num.BackColor = Color.FromArgb(15, 23, 42);
        num.ForeColor = Color.White;
        num.Font = new Font("Segoe UI", 9F);
    }

    private void PopulateCountries()
    {
        _cbCountry.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbCountry.BackColor = Color.FromArgb(15, 23, 42);
        _cbCountry.ForeColor = Color.White;
        _cbCountry.Font = new Font("Segoe UI", 9F);

        _cbCountry.Items.Add(new CountryItem("US", "Amerika (US)"));
        _cbCountry.Items.Add(new CountryItem("DE", "Almanya (DE)"));
        _cbCountry.Items.Add(new CountryItem("GB", "İngiltere (GB)"));
        _cbCountry.Items.Add(new CountryItem("FR", "Fransa (FR)"));
        _cbCountry.Items.Add(new CountryItem("IT", "İtalya (IT)"));
        _cbCountry.Items.Add(new CountryItem("CA", "Kanada (CA)"));
        _cbCountry.Items.Add(new CountryItem("AU", "Avustralya (AU)"));
        _cbCountry.Items.Add(new CountryItem("NL", "Hollanda (NL)"));
        _cbCountry.SelectedIndex = 0;
    }

    private string GetSelectedCountryCode()
    {
        if (_cbCountry.SelectedItem is CountryItem ci) return ci.Code;
        return "US";
    }

    private static string ExtractSubCarrierName(string serviceName)
    {
        if (serviceName.Contains("eko", StringComparison.OrdinalIgnoreCase)) return "Eko Plus";
        if (serviceName.Contains("smart", StringComparison.OrdinalIgnoreCase)) return "Smart (FedEx/TNT)";
        if (serviceName.Contains("ups", StringComparison.OrdinalIgnoreCase)) return "UPS";
        if (serviceName.Contains("widect", StringComparison.OrdinalIgnoreCase)) return "Widect";
        if (serviceName.Contains("express", StringComparison.OrdinalIgnoreCase)) return "Express";
        return "ShipEntegra";
    }

    private static string ExtractDeliveryTime(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return "3-6 iş günü";
        int idx = desc.IndexOf("Tahmini Teslimat Süresi", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = desc.IndexOf("Tahmini Teslim Süresi", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            string part = desc[idx..];
            int br = part.IndexOf("<br", StringComparison.OrdinalIgnoreCase);
            if (br > 0) part = part[..br];
            return part.Replace("Tahmini Teslimat Süresi", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("Tahmini Teslim Süresi", "", StringComparison.OrdinalIgnoreCase)
                       .Trim();
        }
        return "3-6 iş günü";
    }

    private static double ExtractDeliveryDaysMin(string desc)
    {
        string text = ExtractDeliveryTime(desc);
        var parts = text.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && double.TryParse(parts[0], out double val)) return val;
        return 5;
    }

    private static double ExtractDeliveryDaysMax(string desc)
    {
        string text = ExtractDeliveryTime(desc);
        var parts = text.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && double.TryParse(parts[1], out double val)) return val;
        return 7;
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return html.Replace("<br>", " • ").Replace("<br/>", " • ").Replace("<br />", " • ").Trim(' ', '•');
    }

    private sealed record CountryItem(string Code, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed class UnifiedShippingQuote
    {
        public string Provider { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public decimal PriceTry { get; set; }
        public string DeliveryText { get; set; } = string.Empty;
        public double DeliveryDaysMin { get; set; }
        public double DeliveryDaysMax { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsLive { get; set; }
    }

    private async Task TriggerArasAutoLoginAsync(bool directBrowser)
    {
        var settings = ArasGlobalSettingsStore.Load();
        string email = settings.SavedEmail ?? string.Empty;
        string pass = ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword);
        bool showBrowser = directBrowser;

        if (directBrowser || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass) || !settings.HasValidTokenFormat)
        {
            using var dlg = new ShippingLoginCredentialsDialog("Aras Global", email);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            // 1. Kullanıcı doğrudan Bearer token yapıştırdıysa
            if (!string.IsNullOrWhiteSpace(dlg.DirectToken))
            {
                string cleanTok = dlg.DirectToken.Trim();
                if (cleanTok.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    cleanTok = cleanTok.Substring(7).Trim();
                }

                if (cleanTok.Length > 20 && !JwtTokenInspector.IsExpired(cleanTok))
                {
                    settings.BearerToken = "Bearer " + cleanTok;
                    settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                    ArasGlobalSettingsStore.Save(settings);
                    _lblStatus.Text = "✅ Aras Global tokeni başarıyla güncellendi!";
                    _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                    RebuildAccountsHub();
                    await FetchAllQuotesAsync();
                    return;
                }
                else
                {
                    MessageBox.Show("Girilen token formatı geçersiz veya süresi dolmuş.", "Geçersiz Token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (dlg.OpenInDefaultBrowserRequested)
            {
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser("https://panel.arasglobalcargo.com/login");
                MessageBox.Show(
                    "Aras Global paneli varsayılan tarayıcınızda açıldı!\n\nLütfen giriş yaptıktan sonra F12 DevTools Network sekmesindeki Bearer tokenini kopyalayıp buradaki 'Canlı Token' kutusuna yapıştırın.",
                    "Tarayıcı Açıldı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            email = dlg.Email;
            pass = dlg.Password;
            showBrowser = dlg.OpenInBrowserRequested;
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
            {
                settings.SavedEmail = email;
                settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
                settings.AutoRefreshEnabled = dlg.AutoRefresh;
                ArasGlobalSettingsStore.Save(settings);
            }
        }

        _lblStatus.Text = "⏳ Aras Global oturumu açılıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        try
        {
            string? freshToken = await _sessionManager.RefreshArasGlobalTokenAsync(email, pass, showBrowser, s =>
            {
                _lblStatus.Text = s;
            }, knownExpiredToken: settings.CleanToken);

            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                settings.BearerToken = freshToken;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                ArasGlobalSettingsStore.Save(settings);
                _lblStatus.Text = "✅ Aras Global tokeni başarıyla güncellendi!";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                RebuildAccountsHub();
                await FetchAllQuotesAsync();
            }
            else
            {
                MessageBox.Show("Aras Global otomatik oturum açılamadı. 'Normal Chrome'da Aç' seçeneğiyle doğrudan giriş yapabilirsiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            var ask = MessageBox.Show(
                $"Aras otomatik tarayıcı hatası:\n{ex.Message}\n\nNormal Chrome tarayıcınızda açmak ister misiniz?",
                "Tarayıcıda Aç",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (ask == DialogResult.Yes)
            {
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser("https://panel.arasglobalcargo.com/login");
            }
        }
    }

    private async Task TriggerShipEntegraAutoLoginAsync(bool directBrowser)
    {
        var settings = ShipEntegraSettingsStore.Load();
        string email = settings.SavedEmail ?? string.Empty;
        string pass = ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword);
        bool showBrowser = directBrowser;

        if (directBrowser || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass) || !settings.HasValidTokenFormat)
        {
            using var dlg = new ShippingLoginCredentialsDialog("ShipEntegra", email);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            if (!string.IsNullOrWhiteSpace(dlg.DirectToken))
            {
                string cleanTok = dlg.DirectToken.Trim();
                if (cleanTok.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    cleanTok = cleanTok.Substring(7).Trim();
                }

                if (cleanTok.Length > 20 && !JwtTokenInspector.IsExpired(cleanTok))
                {
                    settings.BearerToken = "Bearer " + cleanTok;
                    settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                    ShipEntegraSettingsStore.Save(settings);
                    _lblStatus.Text = "✅ ShipEntegra tokeni başarıyla güncellendi!";
                    _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                    RebuildAccountsHub();
                    await FetchAllQuotesAsync();
                    return;
                }
            }

            if (dlg.OpenInDefaultBrowserRequested)
            {
                PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser("https://app.shipentegra.com/login");
                MessageBox.Show(
                    "ShipEntegra paneli varsayılan tarayıcınızda açıldı!\n\nLütfen giriş yaptıktan sonra F12 DevTools Network sekmesindeki Bearer tokenini kopyalayıp buradaki 'Canlı Token' kutusuna yapıştırın.",
                    "Tarayıcı Açıldı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            email = dlg.Email;
            pass = dlg.Password;
            showBrowser = dlg.OpenInBrowserRequested;
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
            {
                settings.SavedEmail = email;
                settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
                settings.AutoRefreshEnabled = dlg.AutoRefresh;
                ShipEntegraSettingsStore.Save(settings);
            }
        }

        _lblStatus.Text = "⏳ ShipEntegra oturumu açılıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        try
        {
            string? freshToken = await _sessionManager.RefreshShipEntegraTokenAsync(email, pass, showBrowser);
            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                settings.BearerToken = freshToken;
                ShipEntegraSettingsStore.Save(settings);
                _lblStatus.Text = "✅ ShipEntegra tokeni başarıyla güncellendi!";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                RebuildAccountsHub();
                await FetchAllQuotesAsync();
            }
            else
            {
                MessageBox.Show("ShipEntegra otomatik oturum açılamadı. 'Tarayıcı' butonunu deneyebilirsiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ShipEntegra oturum hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TriggerNavlungoAutoLoginAsync(bool directBrowser)
    {
        var settings = NavlungoSettingsStore.Load();
        string email = settings.SavedEmail ?? string.Empty;
        string pass = ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword);
        bool showBrowser = directBrowser;

        if (directBrowser)
        {
            showBrowser = true;
        }
        else if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            using var dlg = new ShippingLoginCredentialsDialog("Navlungo", email);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            email = dlg.Email;
            pass = dlg.Password;
            showBrowser = dlg.OpenInBrowserRequested;
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
            {
                settings.SavedEmail = email;
                settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
                settings.AutoRefreshEnabled = dlg.AutoRefresh;
                NavlungoSettingsStore.Save(settings);
            }
        }

        _lblStatus.Text = "⏳ Navlungo oturumu açılıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        try
        {
            string? freshToken = await _sessionManager.RefreshNavlungoTokenAsync(email, pass, showBrowser);
            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                // DİKKAT: RefreshNavlungoTokenAsync tüm çerezleri diske (navlungo-settings.json) kaydetmiştir.
                // Eski hafızadaki settings nesnesiyle ezmemek için diskteki taze ayarları yükle!
                var freshSettings = NavlungoSettingsStore.Load();
                if (freshToken != "connected" && !string.IsNullOrWhiteSpace(freshToken))
                {
                    freshSettings.IdToken = freshToken;
                }
                freshSettings.TokenLastUpdatedUtc = DateTime.UtcNow;
                NavlungoSettingsStore.Save(freshSettings);

                _lblStatus.Text = "✅ Navlungo oturumu başarıyla güncellendi!";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                RebuildAccountsHub();
                await FetchAllQuotesAsync();
            }
            else
            {
                MessageBox.Show("Navlungo oturumu açılamadı. 'Tarayıcı' (🌐) butonunu kullanarak giriş yapabilirsiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Navlungo oturum hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TriggerShiptomoreAutoLoginAsync(bool directBrowser)
    {
        var settings = ShiptomoreSettingsStore.Load();
        string email = settings.SavedEmail ?? string.Empty;
        string pass = ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword);
        bool showBrowser = directBrowser;

        if (directBrowser)
        {
            showBrowser = true;
        }
        else if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            using var dlg = new ShippingLoginCredentialsDialog("Shiptomore", email);
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            email = dlg.Email;
            pass = dlg.Password;
            showBrowser = dlg.OpenInBrowserRequested;
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(pass))
            {
                settings.SavedEmail = email;
                settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
                settings.AutoRefreshEnabled = dlg.AutoRefresh;
                ShiptomoreSettingsStore.Save(settings);
            }
        }

        _lblStatus.Text = "⏳ Shiptomore oturumu açılıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        try
        {
            string? freshToken = await _sessionManager.RefreshShiptomoreTokenAsync(email, pass, showBrowser);
            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                var freshSettings = ShiptomoreSettingsStore.Load();
                freshSettings.TokenLastUpdatedUtc = DateTime.UtcNow;
                if (!freshToken.Contains("session_id=") && !freshToken.Contains("="))
                {
                    freshSettings.SessionCookie = $"session_id={freshToken}";
                }
                else
                {
                    freshSettings.SessionCookie = freshToken;
                }
                ShiptomoreSettingsStore.Save(freshSettings);

                _lblStatus.Text = "✅ Shiptomore oturumu başarıyla güncellendi!";
                _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                RebuildAccountsHub();
                await FetchAllQuotesAsync();
            }
            else
            {
                MessageBox.Show("Shiptomore oturumu açılamadı. 'Tarayıcı' (🌐) butonunu kullanarak giriş yapabilirsiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Shiptomore oturum hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DisconnectCarrierAsync(string providerName)
    {
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

        _lblStatus.Text = $"🔴 {providerName} oturumu kapatıldı ve bağlantı kesildi.";
        _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);

        RebuildAccountsHub();
        await FetchAllQuotesAsync();
    }
}
