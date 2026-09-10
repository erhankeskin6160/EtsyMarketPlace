namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Modern custom Panel with rounded corners, sleek border, and shadow feel.
/// </summary>
public class ModernCardPanel : Panel
{
    public int CornerRadius { get; set; } = 12;
    public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240);
    public Color CardColor { get; set; } = Color.White;

    public ModernCardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectanglePath(rect, CornerRadius);

        // 1. Zemin boyama (en altta)
        using var fillBrush = new SolidBrush(CardColor);
        e.Graphics.FillPath(fillBrush, path);

        // 2. Kenarlık çizimi
        using var borderPen = new Pen(BorderColor, 1.5f);
        e.Graphics.DrawPath(borderPen, path);

        // 3. Çocuk kontrolleri en üst katmana çiz (metinler asla zeminin altında kalmaz)
        base.OnPaint(e);
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top left arc
        path.AddArc(arc, 180, 90);

        // Top right arc
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right arc
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left arc
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Custom sleek KPI tile with large metric value, title, and trend badge (+15.3% vs geçen ay).
/// </summary>
public class ModernKpiTile : ModernCardPanel
{
    private string _title = "KPI Başlık";
    private string _value = "0";
    private string _trendText = "+0.0%";
    private bool _isPositive = true;

    public string Title
    {
        get => _title;
        set { _title = value; NotifyInvalidate(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; NotifyInvalidate(); }
    }

    public string TrendText
    {
        get => _trendText;
        set { _trendText = value; NotifyInvalidate(); }
    }

    public bool IsPositive
    {
        get => _isPositive;
        set { _isPositive = value; NotifyInvalidate(); }
    }

    public ModernKpiTile()
    {
        Size = new Size(220, 90);
    }

    private void NotifyInvalidate() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw Title
        using var titleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        g.DrawString(_title.ToUpperInvariant(), titleFont, titleBrush, new PointF(14, 12));

        // Draw Value
        using var valueFont = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
        using var valueBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.DrawString(_value, valueFont, valueBrush, new PointF(12, 32));

        // Draw Trend Badge
        if (!string.IsNullOrWhiteSpace(_trendText))
        {
            var badgeBg = _isPositive ? Color.FromArgb(236, 253, 245) : Color.FromArgb(254, 242, 242);
            var badgeFg = _isPositive ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68);

            var badgeText = $"{(_isPositive ? "▲ " : "▼ ")}{_trendText}";
            using var badgeFont = new Font("Segoe UI Semibold", 8F);
            var textSize = g.MeasureString(badgeText, badgeFont);

            var badgeRect = new RectangleF(Width - textSize.Width - 22, 14, textSize.Width + 12, 20);
            using var badgePath = CreateRoundedRectanglePath(Rectangle.Round(badgeRect), 8);
            using var badgeBrush = new SolidBrush(badgeBg);
            using var textBrush = new SolidBrush(badgeFg);

            g.FillPath(badgeBrush, badgePath);
            g.DrawString(badgeText, badgeFont, textBrush, badgeRect.X + 6, badgeRect.Y + 2);
        }
    }
}

/// <summary>
/// Custom flat button with GDI+ rounded corners and hover effect.
/// </summary>
public class ModernButtonControl : Button
{
    public int CornerRadius { get; set; } = 8;
    public Color NormalColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color HoverColor { get; set; } = Color.FromArgb(79, 70, 229);

    private bool _isHovered;

    public ModernButtonControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        Cursor = Cursors.Hand;
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

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, CornerRadius);

        Color currentBg = !Enabled ? Color.FromArgb(203, 213, 225) : (_isHovered ? HoverColor : NormalColor);
        Color currentFg = !Enabled ? Color.FromArgb(100, 116, 139) : ForeColor;

        using var bgBrush = new SolidBrush(currentBg);
        g.FillPath(bgBrush, path);

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            currentFg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Modern dark-theme TabControl with pill-style tab headers, smooth antialiasing, and zero white borders.
/// </summary>
public class ModernTabControl : TabControl
{
    public Color HeaderBackgroundColor { get; set; } = Color.FromArgb(15, 23, 42); // Slate 900
    public Color ActiveTabColor { get; set; } = Color.FromArgb(99, 102, 241); // Indigo 500
    public Color InactiveTabColor { get; set; } = Color.FromArgb(30, 41, 59); // Slate 800
    public Color ActiveTextColor { get; set; } = Color.White;
    public Color InactiveTextColor { get; set; } = Color.FromArgb(148, 163, 184); // Slate 400
    public Color BorderColor { get; set; } = Color.FromArgb(51, 65, 85); // Slate 700

    private int _hoveredIndex = -1;

    public ModernTabControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Normal;
        ItemSize = new Size(165, 36);
        Padding = new Point(16, 6);
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int oldHover = _hoveredIndex;
        _hoveredIndex = -1;
        for (int i = 0; i < TabCount; i++)
        {
            if (GetTabRect(i).Contains(e.Location))
            {
                _hoveredIndex = i;
                break;
            }
        }
        if (oldHover != _hoveredIndex) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Arka planı koyu temaya boya
        using (var bgBrush = new SolidBrush(HeaderBackgroundColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // 2. Tab başlıklarını çiz
        for (int i = 0; i < TabCount; i++)
        {
            var tabRect = GetTabRect(i);
            var isSelected = (SelectedIndex == i);
            var isHovered = (_hoveredIndex == i && !isSelected);

            // Tab hap (pill) alanı
            var pillRect = new Rectangle(tabRect.X + 2, tabRect.Y + 2, tabRect.Width - 4, tabRect.Height - 4);
            if (pillRect.Width <= 0 || pillRect.Height <= 0) continue;

            using var path = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 8);

            Color bg = isSelected ? ActiveTabColor : (isHovered ? Color.FromArgb(45, 55, 75) : InactiveTabColor);
            Color fg = isSelected ? ActiveTextColor : InactiveTextColor;

            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            if (!isSelected)
            {
                using var borderPen = new Pen(BorderColor, 1f);
                g.DrawPath(borderPen, path);
            }

            // Metni çiz
            var tabText = TabPages[i].Text;
            TextRenderer.DrawText(
                g,
                tabText,
                Font,
                pillRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // 3. TabPage içerik alanının etrafına şık çerçeve çiz
        if (SelectedTab != null)
        {
            var displayRect = DisplayRectangle;
            var borderRect = new Rectangle(displayRect.X - 1, displayRect.Y - 1, displayRect.Width + 1, displayRect.Height + 1);
            using var pageBorderPen = new Pen(BorderColor, 1.5f);
            g.DrawRectangle(pageBorderPen, borderRect);
        }
    }
}

/// <summary>
/// Modern vertical FlowLayoutPanel that suppresses horizontal scrollbars at the Win32 level.
/// </summary>
public class VerticalScrollFlowPanel : FlowLayoutPanel
{
    public VerticalScrollFlowPanel()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        WrapContents = false;
        FlowDirection = FlowDirection.TopDown;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style &= ~0x00100000; // WS_HSCROLL - Disable horizontal scrollbar completely
            return cp;
        }
    }
}

/// <summary>
/// Premium Store Copilot Insight Card featuring modern layered dark background,
/// left category accent indicator, pill badge, anti-aliased typography, and action button.
/// </summary>
public class StoreCopilotInsightCard : ModernCardPanel
{
    private readonly string _badgeText;
    private readonly string _icon;
    private readonly string _title;
    private readonly string _description;
    private readonly Color _accentColor;
    private readonly string? _buttonText;
    private readonly Action? _onButtonClick;

    private readonly ModernButtonControl? _actionButton;

    public StoreCopilotInsightCard(
        string icon,
        string badgeText,
        string title,
        string description,
        Color accentColor,
        int width,
        string? buttonText = null,
        Action? onButtonClick = null)
    {
        DoubleBuffered = true;
        _icon = icon;
        _badgeText = badgeText;
        _title = title;
        _description = description;
        _accentColor = accentColor;
        _buttonText = buttonText;
        _onButtonClick = onButtonClick;

        CornerRadius = 10;
        CardColor = Color.FromArgb(20, 27, 44);
        BorderColor = Color.FromArgb(45, 55, 78);
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(16, 12, 16, 12);
        Cursor = Cursors.Default;

        if (!string.IsNullOrWhiteSpace(_buttonText) && _onButtonClick != null)
        {
            _actionButton = new ModernButtonControl
            {
                Text = $"{_buttonText}  →",
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                NormalColor = Color.FromArgb(32, 44, 70),
                HoverColor = _accentColor,
                ForeColor = Color.White,
                CornerRadius = 7,
                Cursor = Cursors.Hand,
                Height = 32
            };
            _actionButton.Click += (_, _) => _onButtonClick();
            Controls.Add(_actionButton);
        }

        UpdateCardWidth(width);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        CardColor = Color.FromArgb(27, 36, 60);
        BorderColor = Color.FromArgb(100, _accentColor.R, _accentColor.G, _accentColor.B);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        CardColor = Color.FromArgb(20, 27, 44);
        BorderColor = Color.FromArgb(45, 55, 78);
        Invalidate();
    }

    public void UpdateCardWidth(int newWidth)
    {
        if (newWidth < 180) newWidth = 180;
        Width = newWidth;

        int contentWidth = Width - 36; // 18px left, 18px right

        using var titleFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        using var descFont = new Font("Segoe UI", 8.75F);

        var titleSize = TextRenderer.MeasureText(
            _title,
            titleFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        var descSize = TextRenderer.MeasureText(
            _description,
            descFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        int currentY = 12 + 20 + 8; // Top padding (12) + Badge (20) + Gap (8)
        currentY += titleSize.Height + 6; // Title + Gap
        currentY += descSize.Height + 12; // Description + Gap

        if (_actionButton != null)
        {
            int btnWidth = Math.Min(220, contentWidth);
            _actionButton.Size = new Size(btnWidth, 32);
            _actionButton.Location = new Point(18, currentY);
            currentY += _actionButton.Height + 14; // Button + Bottom padding
        }
        else
        {
            currentY += 4; // Extra bottom breathing room
        }

        Height = currentY;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var path = CreateRoundedRectanglePath(rect, CornerRadius);

        // 1. Layered dark background
        using (var bgBrush = new SolidBrush(CardColor))
        {
            g.FillPath(bgBrush, path);
        }

        // 2. Left vertical accent indicator bar
        var barRect = new Rectangle(1, 8, 4, Math.Max(12, Height - 16));
        using (var barPath = CreateRoundedRectanglePath(barRect, 2))
        using (var barBrush = new SolidBrush(_accentColor))
        {
            g.FillPath(barBrush, barPath);
        }

        // 3. Modern subtle border
        using (var borderPen = new Pen(BorderColor, 1.2f))
        {
            g.DrawPath(borderPen, path);
        }

        // 4. Pill Badge (with Icon + Category)
        string fullBadge = string.IsNullOrEmpty(_icon) ? _badgeText : $"{_icon}  {_badgeText}";
        using var badgeFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
        var badgeTextSize = g.MeasureString(fullBadge, badgeFont);
        var badgeRect = new Rectangle(18, 12, (int)badgeTextSize.Width + 14, 20);

        using (var badgePath = CreateRoundedRectanglePath(badgeRect, 6))
        using (var badgeBgBrush = new SolidBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)))
        using (var badgeBorderPen = new Pen(Color.FromArgb(70, _accentColor.R, _accentColor.G, _accentColor.B), 1f))
        using (var badgeTextBrush = new SolidBrush(_accentColor))
        {
            g.FillPath(badgeBgBrush, badgePath);
            g.DrawPath(badgeBorderPen, badgePath);
            g.DrawString(fullBadge, badgeFont, badgeTextBrush, badgeRect.X + 7, badgeRect.Y + 3);
        }

        // 5. Title
        int contentWidth = Width - 36;
        using var titleFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        int titleY = badgeRect.Bottom + 6;

        var measuredTitleSize = TextRenderer.MeasureText(
            _title,
            titleFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var titleRect = new Rectangle(18, titleY, contentWidth, measuredTitleSize.Height);

        TextRenderer.DrawText(
            g,
            _title,
            titleFont,
            titleRect,
            Color.FromArgb(248, 250, 252), // Crisp white
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        // 6. Description
        using var descFont = new Font("Segoe UI", 8.75F);
        int descY = titleRect.Bottom + 5;

        var measuredDescSize = TextRenderer.MeasureText(
            _description,
            descFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var descRect = new Rectangle(18, descY, contentWidth, measuredDescSize.Height);

        TextRenderer.DrawText(
            g,
            _description,
            descFont,
            descRect,
            Color.FromArgb(203, 213, 225), // Slate 300
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        // 7. Base child painting (e.g. ModernButtonControl)
        base.OnPaint(e);
    }
}

