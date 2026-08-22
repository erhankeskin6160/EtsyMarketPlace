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
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectanglePath(rect, CornerRadius);

        // Fill background
        using var fillBrush = new SolidBrush(CardColor);
        e.Graphics.FillPath(fillBrush, path);

        // Draw border
        using var borderPen = new Pen(BorderColor, 1.5f);
        e.Graphics.DrawPath(borderPen, path);
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
