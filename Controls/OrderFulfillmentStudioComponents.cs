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
/// Görseldeki Canlı Kargo Teklifi Kartı (Logo kutusu, Taşıyıcı Adı, Quotes, Fiyat, Teslimat Süresi, Cheapest Rozeti).
/// </summary>
public sealed class SaasCarrierCardControl : UserControl
{
    private string _carrierName = string.Empty;
    private string _subCarrier = string.Empty;
    private string _serviceType = string.Empty;
    private decimal _price = 0m;
    private string _currency = "€";
    private string _deliveryDays = "4 days";
    private bool _isCheapest = false;
    private bool _isSelected = false;

    public string CarrierName { get => _carrierName; set { _carrierName = value; Invalidate(); } }
    public string SubCarrier { get => _subCarrier; set { _subCarrier = value; Invalidate(); } }
    public string ServiceType { get => _serviceType; set { _serviceType = value; Invalidate(); } }
    public decimal Price { get => _price; set { _price = value; Invalidate(); } }
    public string Currency { get => _currency; set { _currency = value; Invalidate(); } }
    public string DeliveryDays { get => _deliveryDays; set { _deliveryDays = value; Invalidate(); } }
    public bool IsCheapest { get => _isCheapest; set { _isCheapest = value; Invalidate(); } }
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

        Size = new Size(245, 68);
        Margin = new Padding(0, 0, 0, 8);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
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
        Color bgColor = _isSelected ? Color.FromArgb(22, 38, 59) : Color.FromArgb(21, 30, 48);
        using var bgBrush = new SolidBrush(bgColor);
        g.FillPath(bgBrush, path);

        // 2. Kenarlık (Seçiliyse Parlak Yeşil / Zümrüt Vurgu)
        Color borderColor = _isSelected ? Color.FromArgb(16, 185, 129) : Color.FromArgb(37, 51, 71);
        using var borderPen = new Pen(borderColor, _isSelected ? 1.8f : 1f);
        g.DrawPath(borderPen, path);

        // 3. Sol Logo Kutusu (Aras Global, ShipEntegra, Navlungo, Shiptomore)
        int logoX = 10;
        int logoY = 12;
        int logoSize = 42;
        var logoRect = new Rectangle(logoX, logoY, logoSize, logoSize);
        using var logoPath = SaasCardPanel.CreateRoundedPath(logoRect, 8);

        using var logoBgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillPath(logoBgBrush, logoPath);
        using var logoBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1f);
        g.DrawPath(logoBorderPen, logoPath);

        DrawCarrierIcon(g, logoRect, _carrierName);

        // 4. Orta Bilgiler: Firma Adı & "Quotes"
        int textX = logoX + logoSize + 10;
        using var fontName = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var brushWhite = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.DrawString(_carrierName, fontName, brushWhite, textX, 14);

        using var fontSub = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));
        string subInfo = !string.IsNullOrWhiteSpace(_serviceType) ? _serviceType : "Quotes";
        if (subInfo.Length > 16) subInfo = subInfo.Substring(0, 14) + "..";
        g.DrawString(subInfo, fontSub, brushMuted, textX, 35);

        // 5. Sağ Bilgiler: Fiyat & Teslimat Süresi
        using var fontPrice = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var brushPrice = new SolidBrush(Color.FromArgb(16, 185, 129)); // Neon Emerald #10B981
        using var sfRight = new StringFormat { Alignment = StringAlignment.Far };

        string priceText = $"{_currency}{_price:N2}";
        g.DrawString(priceText, fontPrice, brushPrice, Width - 12, 13, sfRight);

        using var fontDays = new Font("Segoe UI", 8f, FontStyle.Regular);
        g.DrawString(_deliveryDays, fontDays, brushMuted, Width - 12, 36, sfRight);

        // 6. "Cheapest" Rozeti
        if (_isCheapest)
        {
            int badgeW = 60;
            int badgeH = 16;
            var badgeRect = new Rectangle(Width - 12 - badgeW, Height - 18, badgeW, badgeH);
            using var badgePath = SaasCardPanel.CreateRoundedPath(badgeRect, 8);

            using var badgeBgBrush = new SolidBrush(Color.FromArgb(6, 78, 59));
            g.FillPath(badgeBgBrush, badgePath);

            using var fontBadge = new Font("Segoe UI", 7f, FontStyle.Bold);
            using var brushBadgeText = new SolidBrush(Color.FromArgb(52, 211, 153));
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("✓ En Uygun", fontBadge, brushBadgeText, badgeRect, sfCenter);
        }
    }

    private static void DrawCarrierIcon(Graphics g, Rectangle r, string carrier)
    {
        int cx = r.X + (r.Width / 2);
        int cy = r.Y + (r.Height / 2);

        if (carrier.IndexOf("Aras", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Aras Global: Kırmızı zemin üzerinde beyaz ok / kuş simgesi
            using var b = new SolidBrush(Color.FromArgb(220, 38, 38));
            g.FillEllipse(b, cx - 12, cy - 12, 24, 24);

            Point[] arrow =
            {
                new Point(cx - 6, cy - 5),
                new Point(cx + 6, cy),
                new Point(cx - 6, cy + 5)
            };
            using var arrowBrush = new SolidBrush(Color.White);
            g.FillPolygon(arrowBrush, arrow);
        }
        else if (carrier.IndexOf("ShipEntegra", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // ShipEntegra: Turuncu / Mavi sarmal
            using var b = new SolidBrush(Color.FromArgb(234, 88, 12));
            g.FillEllipse(b, cx - 12, cy - 12, 24, 24);

            using var pen = new Pen(Color.FromArgb(6, 182, 212), 2f);
            g.DrawArc(pen, cx - 8, cy - 8, 16, 16, 45, 270);
        }
        else if (carrier.IndexOf("Navlungo", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Navlungo: Mavi N harfi
            using var b = new SolidBrush(Color.FromArgb(37, 99, 235));
            g.FillEllipse(b, cx - 12, cy - 12, 24, 24);

            using var f = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var wb = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("N", f, wb, r, sf);
        }
        else
        {
            // Shiptomore: Yeşil S harfi
            using var b = new SolidBrush(Color.FromArgb(5, 150, 105));
            g.FillEllipse(b, cx - 12, cy - 12, 24, 24);

            using var f = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var wb = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("S", f, wb, r, sf);
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

    public event EventHandler? ValueChanged;

    public SaasUnitInputBox(string title, string initialVal, string unit)
    {
        Size = new Size(110, 54);
        BackColor = Color.Transparent;

        _lblTitle = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            Location = new Point(0, 0),
            AutoSize = true
        };
        Controls.Add(_lblTitle);

        var innerBox = new Panel
        {
            Location = new Point(0, 20),
            Size = new Size(110, 32),
            BackColor = Color.FromArgb(30, 41, 59)
        };

        _textBox = new TextBox
        {
            Text = initialVal,
            Location = new Point(8, 6),
            Size = new Size(62, 20),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _textBox.TextChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);
        innerBox.Controls.Add(_textBox);

        _lblUnit = new Label
        {
            Text = unit,
            Location = new Point(72, 6),
            Size = new Size(32, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        innerBox.Controls.Add(_lblUnit);

        innerBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = SaasCardPanel.CreateRoundedPath(new Rectangle(0, 0, innerBox.Width - 1, innerBox.Height - 1), 6);
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1f);
            e.Graphics.DrawPath(pen, p);
        };

        Controls.Add(innerBox);
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
