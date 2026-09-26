namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Çift tamponlu (Double-buffered), yuvarlak köşeli, modern SaaS kart paneli.
/// </summary>
public class SaasCardPanel : Panel
{
    public int CornerRadius { get; set; } = 10;
    public Color CardBackground { get; set; } = Color.FromArgb(21, 30, 48); // #151E30
    public Color BorderColor { get; set; } = Color.FromArgb(37, 51, 71);    // #253347
    public float BorderWidth { get; set; } = 1f;
    public bool IsSelected { get; set; } = false;
    public Color SelectedBorderColor { get; set; } = Color.FromArgb(6, 182, 212); // Neon Cyan #06B6D4

    public SaasCardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(10);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedPath(rect, CornerRadius);

        // Arka plan dolgusu
        using var fillBrush = new SolidBrush(CardBackground);
        e.Graphics.FillPath(fillBrush, path);

        // Kenarlık
        Color curBorder = IsSelected ? SelectedBorderColor : BorderColor;
        float curWidth = IsSelected ? Math.Max(1.8f, BorderWidth) : BorderWidth;
        using var borderPen = new Pen(curBorder, curWidth);
        e.Graphics.DrawPath(borderPen, path);

        base.OnPaint(e);
    }

    public static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Sol üst
        path.AddArc(arc, 180, 90);
        // Sağ üst
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Sağ alt
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Sol alt
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Görseldeki SaaS Sipariş Kartı (Thumbnail, Unfulfilled badge, IOSS, Seçim Neon Glow).
/// </summary>
public sealed class SaasOrderCardControl : UserControl
{
    private readonly EtsyOrderFulfillmentItem _order;
    private bool _isSelected;

    public EtsyOrderFulfillmentItem Order => _order;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Invalidate();
            }
        }
    }

    public event EventHandler? OrderSelected;

    public SaasOrderCardControl(EtsyOrderFulfillmentItem order)
    {
        _order = order;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        DoubleBuffered = true;

        Size = new Size(245, 145);
        Margin = new Padding(0, 0, 0, 10);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        OrderSelected?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = SaasCardPanel.CreateRoundedPath(bounds, 10);

        // 1. Kart Zemin Rengi
        Color bgColor = _isSelected ? Color.FromArgb(24, 38, 61) : Color.FromArgb(21, 30, 48);
        using var bgBrush = new SolidBrush(bgColor);
        g.FillPath(bgBrush, path);

        // 2. Kenarlık (Seçiliyse Neon Cyan Glow)
        Color borderColor = _isSelected ? Color.FromArgb(6, 182, 212) : Color.FromArgb(37, 51, 71);
        using var borderPen = new Pen(borderColor, _isSelected ? 2f : 1f);
        g.DrawPath(borderPen, path);

        // 3. Başlık: Order #ID
        using var fontTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var brushWhite = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.DrawString($"Order #{_order.OrderNumber}", fontTitle, brushWhite, 12, 10);

        // 4. Sağ Üst Rozet: Unfulfilled (veya Shipped)
        bool isShipped = _order.Status == "Shipped";
        string statusText = isShipped ? "Shipped" : "Unfulfilled";
        Color badgeBg = isShipped ? Color.FromArgb(6, 78, 59) : Color.FromArgb(69, 26, 3);
        Color badgeBorder = isShipped ? Color.FromArgb(16, 185, 129) : Color.FromArgb(146, 64, 14);
        Color badgeText = isShipped ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36);

        DrawPillBadge(g, statusText, Width - 82, 9, 70, 20, badgeBg, badgeBorder, badgeText);

        // 5. Alıcı Adı ve Ülke
        using var fontSub = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var brushGray = new SolidBrush(Color.FromArgb(203, 213, 225));
        using var brushCountry = new SolidBrush(Color.FromArgb(148, 163, 184));

        g.DrawString($"👤 {_order.BuyerName}", fontSub, brushGray, 12, 33);
        g.DrawString($"📍 {_order.CountryName} ({_order.CountryCode})", fontSub, brushCountry, 12, 50);

        // 6. Sol Bölme: 3D Model Thumbnail Kutusu (Görseldeki gibi)
        int thumbX = 12;
        int thumbY = 72;
        int thumbSize = 52;
        var thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);
        using var thumbPath = SaasCardPanel.CreateRoundedPath(thumbRect, 8);

        using var thumbBgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillPath(thumbBgBrush, thumbPath);
        using var thumbBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1f);
        g.DrawPath(thumbBorderPen, thumbPath);

        // Thumbnail içine 3D Model / Gargoyle minyatür çizimi
        DrawMiniatureThumbnail(g, thumbRect);

        // 7. Sağ Bölme: IOSS Rozeti ve Numarası
        int iossX = thumbX + thumbSize + 12;
        int iossY = 74;

        if (!string.IsNullOrWhiteSpace(_order.IossNumber))
        {
            using var fontIossLabel = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var brushCyan = new SolidBrush(Color.FromArgb(56, 189, 248));
            g.DrawString("🛡️ IOSS", fontIossLabel, brushCyan, iossX, iossY);

            using var fontIossVal = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var brushTeal = new SolidBrush(Color.FromArgb(6, 182, 212));
            g.DrawString($"! {_order.IossNumber}", fontIossVal, brushTeal, iossX, iossY + 16);
        }
        else
        {
            using var fontIossVal = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("Standart İhracat", fontIossVal, brushMuted, iossX, iossY + 8);
        }

        // 8. Kart İçi Aksiyon Butonu / Durum Hapı (Görseldeki alt buton)
        int btnW = Width - iossX - 12;
        int btnH = 22;
        int btnY = 102;
        var btnRect = new Rectangle(iossX, btnY, btnW, btnH);
        using var btnPath = SaasCardPanel.CreateRoundedPath(btnRect, 6);

        Color actionColor = _isSelected ? Color.FromArgb(8, 145, 178) : Color.FromArgb(30, 41, 59);
        using var actionBrush = new SolidBrush(actionColor);
        g.FillPath(actionBrush, btnPath);

        using var fontBtn = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var brushBtnText = new SolidBrush(Color.White);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(_isSelected ? "✓ Seçildi" : "Seç & Hazırla", fontBtn, brushBtnText, btnRect, sf);
    }

    private static void DrawMiniatureThumbnail(Graphics g, Rectangle r)
    {
        // 3D Model / Gargoyle heykeli tarzında şık gradyanlı minyatür ikon
        int cx = r.X + (r.Width / 2);
        int cy = r.Y + (r.Height / 2);

        using var glowBrush = new SolidBrush(Color.FromArgb(30, 56, 189, 248));
        g.FillEllipse(glowBrush, cx - 16, cy - 16, 32, 32);

        // Heykelcik gövdesi (üçgen / polygon)
        Point[] body =
        {
            new Point(cx, cy - 14),
            new Point(cx + 12, cy + 12),
            new Point(cx - 12, cy + 12)
        };
        using var modelBrush = new LinearGradientBrush(
            new Rectangle(cx - 14, cy - 14, 28, 28),
            Color.FromArgb(56, 189, 248),
            Color.FromArgb(30, 41, 59),
            LinearGradientMode.Vertical);
        g.FillPolygon(modelBrush, body);

        // Kanatlar / detaylar
        using var wingPen = new Pen(Color.FromArgb(125, 211, 252), 1.5f);
        g.DrawLine(wingPen, cx - 12, cy + 4, cx - 18, cy - 6);
        g.DrawLine(wingPen, cx + 12, cy + 4, cx + 18, cy - 6);
    }

    private static void DrawPillBadge(Graphics g, string text, int x, int y, int w, int h, Color bg, Color border, Color textColor)
    {
        var rect = new Rectangle(x, y, w, h);
        using var path = SaasCardPanel.CreateRoundedPath(rect, h / 2);

        using var bgBrush = new SolidBrush(bg);
        g.FillPath(bgBrush, path);

        using var borderPen = new Pen(border, 1f);
        g.DrawPath(borderPen, path);

        using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(textColor);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, rect, sf);
    }
}

/// <summary>
/// Görseldeki Canlı Kargo Teklifi Kartı (Çift Kurumsal Logo: Sağlayıcı + Taşıyıcı, Servis Adı, Canlı Fiyat, Teslimat Süresi, En Ucuz Rozeti).
/// </summary>
public sealed class SaasCarrierCardControl : UserControl
{
    private string _providerName = string.Empty;
    private string _carrierName = string.Empty;
    private string _subCarrier = string.Empty;
    private string _serviceType = string.Empty;
    private decimal _price = 0m;
    private string _currency = "$";
    private decimal _priceTry = 0m;
    private string _deliveryDays = "3-5 gün";
    private bool _isCheapest = false;
    private bool _isLive = true;
    private bool _isSelected = false;
    private bool _isHovered = false;

    public string ProviderName { get => _providerName; set { _providerName = value; Invalidate(); } }
    public string CarrierName { get => _carrierName; set { _carrierName = value; Invalidate(); } }
    public string SubCarrier { get => _subCarrier; set { _subCarrier = value; Invalidate(); } }
    public string ServiceType { get => _serviceType; set { _serviceType = value; Invalidate(); } }
    public decimal Price { get => _price; set { _price = value; Invalidate(); } }
    public string Currency { get => _currency; set { _currency = value; Invalidate(); } }
    public decimal PriceTry { get => _priceTry; set { _priceTry = value; Invalidate(); } }
    public string DeliveryDays { get => _deliveryDays; set { _deliveryDays = value; Invalidate(); } }
    public bool IsCheapest { get => _isCheapest; set { _isCheapest = value; Invalidate(); } }
    public bool IsLive { get => _isLive; set { _isLive = value; Invalidate(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; Invalidate(); } }

    public object? AssociatedModel { get; set; }

    public event EventHandler? CarrierSelected;

    public SaasCarrierCardControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        DoubleBuffered = true;

        Size = new Size(270, 72);
        Margin = new Padding(0, 0, 0, 8);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        CarrierSelected?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = SaasCardPanel.CreateRoundedPath(bounds, 10);

        // 1. Zemin Rengi
        Color bgColor = _isSelected 
            ? Color.FromArgb(22, 38, 59) 
            : (_isHovered 
                ? Color.FromArgb(28, 41, 64) 
                : (_isCheapest ? Color.FromArgb(19, 36, 48) : Color.FromArgb(21, 30, 48)));
        using var bgBrush = new SolidBrush(bgColor);
        g.FillPath(bgBrush, path);

        // 2. Kenarlık (Seçiliyse Parlak Zümrüt Vurgu, Hover ise Açık Mavi)
        Color borderColor = _isSelected 
            ? Color.FromArgb(16, 185, 129) 
            : (_isHovered 
                ? Color.FromArgb(59, 130, 246) 
                : (_isCheapest ? Color.FromArgb(16, 185, 129, 130) : Color.FromArgb(37, 51, 71)));
        using var borderPen = new Pen(borderColor, _isSelected ? 2f : 1f);
        g.DrawPath(borderPen, path);

        // 3. Sol Çift Logo Kutusu (1: Aracı Sağlayıcı + 2: Alt Taşıyıcı)
        int box1W = 44, box1H = 34, box1X = 8, box1Y = 12;
        var logo1Rect = new Rectangle(box1X, box1Y, box1W, box1H);
        using var logo1Path = SaasCardPanel.CreateRoundedPath(logo1Rect, 6);
        using var logo1BgBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.FillPath(logo1BgBrush, logo1Path);
        using var logo1BorderPen = new Pen(Color.FromArgb(226, 232, 240), 1f);
        g.DrawPath(logo1BorderPen, logo1Path);

        var provImg = ShippingLogoHelper.GetProviderLogo(!string.IsNullOrWhiteSpace(_providerName) ? _providerName : _carrierName);
        var inner1 = new Rectangle(box1X + 2, box1Y + 2, box1W - 4, box1H - 4);
        ShippingLogoHelper.DrawImagePreserveAspect(g, provImg, inner1);

        int box2W = 44, box2H = 34, box2X = 56, box2Y = 12;
        var logo2Rect = new Rectangle(box2X, box2Y, box2W, box2H);
        using var logo2Path = SaasCardPanel.CreateRoundedPath(logo2Rect, 6);
        g.FillPath(logo1BgBrush, logo2Path);
        g.DrawPath(logo1BorderPen, logo2Path);

        var carrImg = ShippingLogoHelper.GetCarrierLogo(_subCarrier, _serviceType, _carrierName);
        var inner2 = new Rectangle(box2X + 2, box2Y + 2, box2W - 4, box2H - 4);
        ShippingLogoHelper.DrawImagePreserveAspect(g, carrImg, inner2);

        // 4. Orta Bilgiler: Servis Adı & Sağlayıcı/Süre Detayı
        int textX = 106;
        int rightColWidth = 86;
        int middleWidth = Math.Max(50, Width - textX - rightColWidth);

        string displayTitle = !string.IsNullOrWhiteSpace(_serviceType) ? _serviceType : _carrierName;
        using var fontName = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        using var brushWhite = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var sfLeftEllipsis = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        var titleRect = new RectangleF(textX, 11, middleWidth, 18);
        g.DrawString(displayTitle, fontName, brushWhite, titleRect, sfLeftEllipsis);

        string provName = !string.IsNullOrWhiteSpace(_providerName) ? _providerName : _carrierName;
        string subInfo = $"{provName} • {_deliveryDays}";
        using var fontSub = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));
        var subRect = new RectangleF(textX, 31, middleWidth, 16);
        g.DrawString(subInfo, fontSub, brushMuted, subRect, sfLeftEllipsis);

        if (_isLive)
        {
            using var liveDotBrush = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.FillEllipse(liveDotBrush, textX, 54, 5, 5);
            using var fontTag = new Font("Segoe UI", 7.2f, FontStyle.Regular);
            using var brushTag = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("Canlı Entegrasyon", fontTag, brushTag, textX + 8, 50);
        }

        // 5. Sağ Bilgiler: Fiyat, Yaklaşık TL & Teslimat Süresi / Rozet
        using var fontPrice = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var brushPrice = new SolidBrush(Color.FromArgb(16, 185, 129)); // Emerald #10B981
        using var sfRight = new StringFormat { Alignment = StringAlignment.Far };

        string priceText = $"{_currency}{_price:N2}";
        g.DrawString(priceText, fontPrice, brushPrice, Width - 10, 10, sfRight);

        if (_priceTry > 0)
        {
            using var fontTry = new Font("Segoe UI", 8f, FontStyle.Regular);
            g.DrawString($"≈ {_priceTry:N0} ₺", fontTry, brushMuted, Width - 10, 30, sfRight);
        }

        // 6. "🏆 EN UCUZ" Rozeti
        if (_isCheapest)
        {
            int badgeW = 66;
            int badgeH = 17;
            var badgeRect = new Rectangle(Width - 10 - badgeW, 49, badgeW, badgeH);
            using var badgePath = SaasCardPanel.CreateRoundedPath(badgeRect, 5);

            using var badgeBgBrush = new SolidBrush(Color.FromArgb(6, 78, 59));
            g.FillPath(badgeBgBrush, badgePath);

            using var fontBadge = new Font("Segoe UI", 6.8f, FontStyle.Bold);
            using var brushBadgeText = new SolidBrush(Color.FromArgb(52, 211, 153));
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("🏆 EN UCUZ", fontBadge, brushBadgeText, badgeRect, sfCenter);
        }
    }
}

/// <summary>
/// Görseldeki Modern Giriş Kutusu (Sağında entegre 'kg', 'cm' suffix rozeti olan kutular).
/// </summary>
public sealed class SaasUnitInputBox : Panel
{
    private readonly TextBox _textBox;
    private readonly Label _lblUnit;
    private readonly Label _lblTitle;

    public string Title { get => _lblTitle.Text; set => _lblTitle.Text = value; }
    public string Unit { get => _lblUnit.Text; set => _lblUnit.Text = value; }
    public string Value { get => _textBox.Text; set => _textBox.Text = value; }
    private readonly string _defaultVal;

    public event EventHandler? ValueChanged;

    public SaasUnitInputBox(string title, string initialVal, string unit)
    {
        _defaultVal = initialVal;
        Size = new Size(115, 54);
        BackColor = Color.Transparent;

        _lblTitle = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 18,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var innerBox = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0)
        };

        _lblUnit = new Label
        {
            Text = unit,
            Dock = DockStyle.Right,
            Width = 32,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        innerBox.Controls.Add(_lblUnit);

        _textBox = new TextBox
        {
            Text = initialVal,
            Location = new Point(8, 6),
            Width = 65,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _textBox.TextChanged += (s, e) =>
        {
            innerBox.Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        _textBox.Leave += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_textBox.Text) || !decimal.TryParse(_textBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal v) || v <= 0)
            {
                _textBox.Text = _defaultVal;
                innerBox.Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        innerBox.Controls.Add(_textBox);

        innerBox.SizeChanged += (s, e) =>
        {
            _textBox.Width = Math.Max(20, innerBox.Width - _lblUnit.Width - 14);
            innerBox.Invalidate();
        };

        innerBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = SaasCardPanel.CreateRoundedPath(new Rectangle(0, 0, innerBox.Width - 1, innerBox.Height - 1), 6);
            bool isInvalid = GetDecimal() <= 0;
            Color borderColor = isInvalid ? Color.FromArgb(239, 68, 68) : Color.FromArgb(51, 65, 85);
            using var pen = new Pen(borderColor, isInvalid ? 1.5f : 1f);
            e.Graphics.DrawPath(pen, p);
        };

        Controls.Add(innerBox);
        Controls.Add(_lblTitle);
        _lblTitle.BringToFront();
    }

    public decimal GetDecimal()
    {
        if (decimal.TryParse(_textBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
            return val;
        return 0m;
    }
}

/// <summary>
/// Görseldeki Gerçekçi Beyaz Barkod Kargo Etiketi Önizleme Bileşeni.
/// </summary>
public sealed class SaasBarcodeLabelControl : UserControl
{
    private string _carrierName = "Aras Global";
    private string _receiverName = "Inge Neuer";
    private string _addressLine = "Am Rheinufer 12";
    private string _barcodeNumber = "10092370104092";

    public string CarrierName { get => _carrierName; set { _carrierName = value; Invalidate(); } }
    public string ReceiverName { get => _receiverName; set { _receiverName = value; Invalidate(); } }
    public string AddressLine { get => _addressLine; set { _addressLine = value; Invalidate(); } }
    public string BarcodeNumber { get => _barcodeNumber; set { _barcodeNumber = value; Invalidate(); } }

    public SaasBarcodeLabelControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        DoubleBuffered = true;

        Size = new Size(245, 95);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = SaasCardPanel.CreateRoundedPath(bounds, 8);

        // Beyaz Etiket Zemini
        using var whiteBrush = new SolidBrush(Color.White);
        g.FillPath(whiteBrush, path);

        using var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1f);
        g.DrawPath(borderPen, path);

        // Barkod Çizgileri
        int startX = 20;
        int barY = 12;
        int barH = 34;

        using var pen1 = new Pen(Color.Black, 1.5f);
        using var pen2 = new Pen(Color.Black, 3.5f);

        for (int i = 0; i < 38; i++)
        {
            var p = (i % 3 == 0 || i % 7 == 0) ? pen2 : pen1;
            g.DrawLine(p, startX + (i * 5), barY, startX + (i * 5), barY + barH);
        }

        // Barkod Numarası
        using var fontCode = new Font("Consolas", 8.5f, FontStyle.Bold);
        using var brushBlack = new SolidBrush(Color.Black);
        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(_barcodeNumber, fontCode, brushBlack, Width / 2f, barY + barH + 2, sfCenter);

        // Alt Çizgi Ayırıcı
        using var sepPen = new Pen(Color.FromArgb(226, 232, 240), 1f);
        g.DrawLine(sepPen, 10, barY + barH + 18, Width - 10, barY + barH + 18);

        // Alt Satır: Aras Logo + "Shipping Label Aras Global" + Alıcı
        using var redBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
        g.FillEllipse(redBrush, 12, barY + barH + 22, 10, 10);

        using var fontLabel = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var brushDark = new SolidBrush(Color.FromArgb(30, 41, 59));
        g.DrawString($"Shipping Label {_carrierName}", fontLabel, brushDark, 26, barY + barH + 20);

        using var fontSub = new Font("Segoe UI", 7f, FontStyle.Regular);
        using var brushGray = new SolidBrush(Color.FromArgb(100, 116, 139));
        string recShort = $"{_receiverName} - {_addressLine}";
        if (recShort.Length > 30) recShort = recShort.Substring(0, 28) + "..";
        g.DrawString(recShort, fontSub, brushGray, 26, barY + barH + 32);
    }
}
