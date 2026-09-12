namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
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
/// Custom flat button with GDI+ rounded corners, hover effect, and clean container background clearing.
/// </summary>
public class ModernButtonControl : Button
{
    public int CornerRadius { get; set; } = 8;
    public Color NormalColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color HoverColor { get; set; } = Color.FromArgb(79, 70, 229);

    private bool _isHovered;

    public ModernButtonControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    public override Color BackColor
    {
        get => GetEffectiveParentBackColor();
        set { }
    }

    private Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.BackgroundColor;
    }

    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        Invalidate();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Invalidate();
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

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Explicitly clear background to container color so no dirty sibling pixels remain in buffer
        Color parentBg = GetEffectiveParentBackColor();
        using var clearBrush = new SolidBrush(parentBg);
        pevent.Graphics.FillRectangle(clearBrush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Clear control bounds with effective parent background color
        // This permanently eliminates any green/white/dirty corner artifacts outside the rounded pill
        Color parentBg = GetEffectiveParentBackColor();
        using (var clearBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(clearBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, CornerRadius);

        Color currentBg = !Enabled ? Color.FromArgb(71, 85, 105) : (_isHovered ? HoverColor : NormalColor);
        Color currentFg = !Enabled ? Color.FromArgb(148, 163, 184) : ForeColor;

        using var bgBrush = new SolidBrush(currentBg);
        g.FillPath(bgBrush, path);

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            currentFg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Modern custom-drawn CheckBox with rounded corners, smooth vector checkmark, hover glow, and theme integration.
/// </summary>
public class ModernCheckBox : CheckBox
{
    private bool _isHovered;
    private bool _isPressed;

    public int BoxSize { get; set; } = 19;
    public int CornerRadius { get; set; } = 5;
    public Color CheckColor { get; set; } = Color.White;
    public Color BoxBorderColor { get; set; } = Color.FromArgb(71, 85, 105);
    public Color BoxBorderHoverColor { get; set; } = Color.FromArgb(129, 140, 248);
    public Color BoxCheckedColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color BoxCheckedHoverColor { get; set; } = Color.FromArgb(79, 70, 229);
    public Color BoxUncheckedColor { get; set; } = Color.FromArgb(23, 32, 51);
    public Color BoxUncheckedHoverColor { get; set; } = Color.FromArgb(37, 49, 74);

    public ModernCheckBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        ForeColor = UiStyle.TextDark;
        Font = new Font("Segoe UI Semibold", 9F);
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
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Color effectiveBg = GetEffectiveParentBackColor();
        if (BackColor != Color.Transparent && BackColor != Color.Empty && BackColor.A == 255)
        {
            effectiveBg = BackColor;
        }

        using (var bgBrush = new SolidBrush(effectiveBg))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        int boxY = Math.Max(0, (Height - BoxSize) / 2);
        int boxX = Padding.Left + 2;
        var boxRect = new Rectangle(boxX, boxY, BoxSize, BoxSize);

        DrawBox(
            g,
            boxRect,
            Checked,
            CheckState == CheckState.Indeterminate,
            _isHovered,
            _isPressed,
            Enabled,
            BoxSize,
            CornerRadius,
            CheckColor,
            BoxBorderColor,
            BoxBorderHoverColor,
            BoxCheckedColor,
            BoxCheckedHoverColor,
            BoxUncheckedColor,
            BoxUncheckedHoverColor);

        // Draw Text
        if (!string.IsNullOrEmpty(Text))
        {
            int textX = boxRect.Right + 8;
            int textW = Math.Max(0, Width - textX - Padding.Right);
            var textRect = new Rectangle(textX, 0, textW, Height);
            Color textClr = Enabled ? ForeColor : UiStyle.TextMuted;

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                textClr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        if (Focused && ShowFocusCues)
        {
            var focusRect = new Rectangle(boxRect.X - 2, boxRect.Y - 2, boxRect.Width + 4, boxRect.Height + 4);
            using var focusPen = new Pen(Color.FromArgb(165, 180, 252), 1f) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(focusPen, focusRect);
        }
    }

    public static void DrawBox(
        Graphics g,
        Rectangle boxRect,
        bool isChecked,
        bool isIndeterminate = false,
        bool isHovered = false,
        bool isPressed = false,
        bool isEnabled = true,
        int boxSize = 19,
        int cornerRadius = 5,
        Color? checkClr = null,
        Color? borderClr = null,
        Color? borderHoverClr = null,
        Color? checkedClr = null,
        Color? checkedHoverClr = null,
        Color? uncheckedClr = null,
        Color? uncheckedHoverClr = null)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (isPressed && isEnabled && boxRect.Width > 4 && boxRect.Height > 4)
        {
            boxRect.Inflate(-1, -1);
        }

        Color cCheck = checkClr ?? Color.White;
        Color cBorder = borderClr ?? Color.FromArgb(71, 85, 105);
        Color cBorderHover = borderHoverClr ?? Color.FromArgb(129, 140, 248);
        Color cChecked = checkedClr ?? Color.FromArgb(99, 102, 241);
        Color cCheckedHover = checkedHoverClr ?? Color.FromArgb(79, 70, 229);
        Color cUnchecked = uncheckedClr ?? Color.FromArgb(23, 32, 51);
        Color cUncheckedHover = uncheckedHoverClr ?? Color.FromArgb(37, 49, 74);

        Color fill;
        Color stroke;

        if (!isEnabled)
        {
            fill = Color.FromArgb(40, 48, 64);
            stroke = Color.FromArgb(70, 80, 100);
        }
        else if (isChecked || isIndeterminate)
        {
            fill = isHovered ? cCheckedHover : cChecked;
            stroke = isHovered ? Color.FromArgb(165, 180, 252) : cChecked;
        }
        else
        {
            fill = isHovered ? cUncheckedHover : cUnchecked;
            stroke = isHovered ? cBorderHover : cBorder;
        }

        // Draw soft outer glow on hover
        if (isEnabled && isHovered)
        {
            var glowRect = new Rectangle(boxRect.X - 1, boxRect.Y - 1, boxRect.Width + 2, boxRect.Height + 2);
            using var glowPath = ModernCardPanel.CreateRoundedRectanglePath(glowRect, cornerRadius + 1);
            using var glowBrush = new SolidBrush(Color.FromArgb(35, 99, 102, 241));
            g.FillPath(glowBrush, glowPath);
        }

        // Draw rounded box
        using (var path = ModernCardPanel.CreateRoundedRectanglePath(boxRect, cornerRadius))
        {
            using (var fillBrush = new SolidBrush(fill))
            {
                g.FillPath(fillBrush, path);
            }

            using (var borderPen = new Pen(stroke, 1.5f))
            {
                g.DrawPath(borderPen, path);
            }
        }

        // Draw Vector Checkmark or Indeterminate dash
        if (isChecked)
        {
            using var checkPen = new Pen(isEnabled ? cCheck : Color.FromArgb(148, 163, 184), 2.3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            float p1x = boxRect.X + boxRect.Width * 0.27f;
            float p1y = boxRect.Y + boxRect.Height * 0.52f;

            float p2x = boxRect.X + boxRect.Width * 0.44f;
            float p2y = boxRect.Y + boxRect.Height * 0.72f;

            float p3x = boxRect.X + boxRect.Width * 0.75f;
            float p3y = boxRect.Y + boxRect.Height * 0.30f;

            g.DrawLines(checkPen, new[]
            {
                new PointF(p1x, p1y),
                new PointF(p2x, p2y),
                new PointF(p3x, p3y)
            });
        }
        else if (isIndeterminate)
        {
            using var dashPen = new Pen(isEnabled ? cCheck : Color.FromArgb(148, 163, 184), 2.3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            float cy = boxRect.Y + boxRect.Height * 0.5f;
            g.DrawLine(dashPen, boxRect.X + boxRect.Width * 0.28f, cy, boxRect.X + boxRect.Width * 0.72f, cy);
        }
    }

    private Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.BackgroundColor;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size textSize = Size.Empty;
        if (!string.IsNullOrEmpty(Text))
        {
            textSize = TextRenderer.MeasureText(Text, Font, proposedSize, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        int w = Padding.Horizontal + BoxSize + 10 + textSize.Width + 6;
        int h = Math.Max(BoxSize + Padding.Vertical + 4, Math.Max(26, textSize.Height + Padding.Vertical + 4));
        return new Size(w, h);
    }
}

/// <summary>
/// Modern soft vertical scrollbar matching the dark theme palette with rounded pill thumb.
/// </summary>
public class ModernVScrollBar : Control
{
    private int _min = 0;
    private int _max = 100;
    private int _val = 0;
    private int _largeChange = 20;
    private int _smallChange = 5;
    private bool _isHovered = false;
    private bool _isDragging = false;
    private int _dragStartY = 0;
    private int _dragStartVal = 0;

    public event EventHandler? ValueChanged;

    public int Minimum
    {
        get => _min;
        set { _min = value; Invalidate(); }
    }

    public int Maximum
    {
        get => _max;
        set { _max = Math.Max(_min, value); Invalidate(); }
    }

    public int Value
    {
        get => _val;
        set
        {
            int clamped = Math.Clamp(value, _min, _max);
            if (_val != clamped)
            {
                _val = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int LargeChange
    {
        get => _largeChange;
        set { _largeChange = Math.Max(1, value); Invalidate(); }
    }

    public int SmallChange
    {
        get => _smallChange;
        set { _smallChange = Math.Max(1, value); Invalidate(); }
    }

    public Color TrackColor { get; set; } = Color.Transparent;
    public Color ThumbNormalColor { get; set; } = Color.FromArgb(71, 85, 105);   // Slate 600
    public Color ThumbHoverColor { get; set; } = Color.FromArgb(100, 116, 139);  // Slate 500
    public Color ThumbActiveColor { get; set; } = Color.FromArgb(99, 102, 241);  // Indigo 500

    public ModernVScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        Width = 8;
        Cursor = Cursors.Default;
    }

    public override Color BackColor
    {
        get => GetEffectiveParentBackColor();
        set { }
    }

    public void ScrollBy(int delta)
    {
        Value += delta;
    }

    private Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.CardBackground;
    }

    private Rectangle GetThumbRect()
    {
        if (_max <= _min || Height <= 0) return Rectangle.Empty;

        int totalRange = (_max - _min) + _largeChange;
        int thumbH = Math.Max(26, (int)((float)_largeChange / totalRange * Height));
        if (thumbH > Height) thumbH = Height;

        int travel = Height - thumbH;
        int thumbY = travel > 0 ? (int)((float)(_val - _min) / (_max - _min) * travel) : 0;
        return new Rectangle(1, thumbY, Math.Max(4, Width - 2), thumbH);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = TrackColor != Color.Transparent ? TrackColor : GetEffectiveParentBackColor();
        using (var clearBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(clearBrush, ClientRectangle);
        }

        var thumb = GetThumbRect();
        if (thumb.IsEmpty || thumb.Height <= 0 || _max <= _min) return;

        Color thumbCol = _isDragging ? ThumbActiveColor : (_isHovered ? ThumbHoverColor : ThumbNormalColor);
        using var brush = new SolidBrush(thumbCol);

        int r = Math.Min(3, thumb.Width / 2);
        using var path = new GraphicsPath();
        path.AddArc(thumb.X, thumb.Y, r * 2, r * 2, 180, 90);
        path.AddArc(thumb.Right - r * 2, thumb.Y, r * 2, r * 2, 270, 90);
        path.AddArc(thumb.Right - r * 2, thumb.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(thumb.X, thumb.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var thumb = GetThumbRect();
        if (thumb.Contains(e.Location))
        {
            _isDragging = true;
            _dragStartY = e.Y;
            _dragStartVal = _val;
            Invalidate();
        }
        else if (!thumb.IsEmpty)
        {
            if (e.Y < thumb.Y) Value -= _largeChange;
            else Value += _largeChange;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            var thumb = GetThumbRect();
            int travel = Height - thumb.Height;
            if (travel > 0)
            {
                int deltaY = e.Y - _dragStartY;
                Value = _dragStartVal + (int)((float)deltaY / travel * (_max - _min));
            }
        }
        else
        {
            bool hover = GetThumbRect().Contains(e.Location);
            if (_isHovered != hover)
            {
                _isHovered = hover;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_isHovered)
        {
            _isHovered = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int steps = -Math.Sign(e.Delta) * (LargeChange > 0 ? Math.Max(1, LargeChange / 4) : 24);
        Value += steps;
    }
}

/// <summary>
/// Modern soft horizontal scrollbar matching the dark theme palette with rounded pill thumb.
/// </summary>
public class ModernHScrollBar : Control
{
    private int _min = 0;
    private int _max = 100;
    private int _val = 0;
    private int _largeChange = 20;
    private int _smallChange = 5;
    private bool _isHovered = false;
    private bool _isDragging = false;
    private int _dragStartX = 0;
    private int _dragStartVal = 0;

    public event EventHandler? ValueChanged;

    public int Minimum
    {
        get => _min;
        set { _min = value; Invalidate(); }
    }

    public int Maximum
    {
        get => _max;
        set { _max = Math.Max(_min, value); Invalidate(); }
    }

    public int Value
    {
        get => _val;
        set
        {
            int clamped = Math.Clamp(value, _min, _max);
            if (_val != clamped)
            {
                _val = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int LargeChange
    {
        get => _largeChange;
        set { _largeChange = Math.Max(1, value); Invalidate(); }
    }

    public int SmallChange
    {
        get => _smallChange;
        set { _smallChange = Math.Max(1, value); Invalidate(); }
    }

    public Color TrackColor { get; set; } = Color.Transparent;
    public Color ThumbNormalColor { get; set; } = Color.FromArgb(71, 85, 105);   // Slate 600
    public Color ThumbHoverColor { get; set; } = Color.FromArgb(100, 116, 139);  // Slate 500
    public Color ThumbActiveColor { get; set; } = Color.FromArgb(99, 102, 241);  // Indigo 500

    public ModernHScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        Height = 8;
        Cursor = Cursors.Default;
    }

    public override Color BackColor
    {
        get => GetEffectiveParentBackColor();
        set { }
    }

    public void ScrollBy(int delta)
    {
        Value += delta;
    }

    private Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.CardBackground;
    }

    private Rectangle GetThumbRect()
    {
        if (_max <= _min || Width <= 0) return Rectangle.Empty;

        int totalRange = (_max - _min) + _largeChange;
        int thumbW = Math.Max(26, (int)((float)_largeChange / totalRange * Width));
        if (thumbW > Width) thumbW = Width;

        int travel = Width - thumbW;
        int thumbX = travel > 0 ? (int)((float)(_val - _min) / (_max - _min) * travel) : 0;
        return new Rectangle(thumbX, 1, thumbW, Math.Max(4, Height - 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = TrackColor != Color.Transparent ? TrackColor : GetEffectiveParentBackColor();
        using (var clearBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(clearBrush, ClientRectangle);
        }

        var thumb = GetThumbRect();
        if (thumb.IsEmpty || thumb.Width <= 0 || _max <= _min) return;

        Color thumbCol = _isDragging ? ThumbActiveColor : (_isHovered ? ThumbHoverColor : ThumbNormalColor);
        using var brush = new SolidBrush(thumbCol);

        int r = Math.Min(3, thumb.Height / 2);
        using var path = new GraphicsPath();
        path.AddArc(thumb.X, thumb.Y, r * 2, r * 2, 180, 90);
        path.AddArc(thumb.Right - r * 2, thumb.Y, r * 2, r * 2, 270, 90);
        path.AddArc(thumb.Right - r * 2, thumb.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(thumb.X, thumb.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var thumb = GetThumbRect();
        if (thumb.Contains(e.Location))
        {
            _isDragging = true;
            _dragStartX = e.X;
            _dragStartVal = _val;
            Invalidate();
        }
        else if (!thumb.IsEmpty)
        {
            if (e.X < thumb.X) Value -= _largeChange;
            else Value += _largeChange;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            var thumb = GetThumbRect();
            int travel = Width - thumb.Width;
            if (travel > 0)
            {
                int deltaX = e.X - _dragStartX;
                Value = _dragStartVal + (int)((float)deltaX / travel * (_max - _min));
            }
        }
        else
        {
            bool hover = GetThumbRect().Contains(e.Location);
            if (_isHovered != hover)
            {
                _isHovered = hover;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_isHovered)
        {
            _isHovered = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int steps = -Math.Sign(e.Delta) * (LargeChange > 0 ? Math.Max(1, LargeChange / 4) : 24);
        Value += steps;
    }
}

/// <summary>
/// Attaches sleek ModernVScrollBar and ModernHScrollBar to any DataGridView,
/// hiding native Win32 scrollbars and smoothly syncing horizontal pixel offset and vertical rows.
/// </summary>
public class ModernGridScrollAdapter : IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, ModernGridScrollAdapter> _adapters = new();

    private readonly DataGridView _grid;
    private readonly ModernVScrollBar _vBar;
    private readonly ModernHScrollBar _hBar;
    private readonly Panel _corner;
    private bool _isSyncing = false;
    private bool _isDisposed = false;

    public static ModernGridScrollAdapter Attach(DataGridView grid)
    {
        if (_adapters.TryGetValue(grid, out var existing))
        {
            existing.RecalculateScroll();
            return existing;
        }

        var adapter = new ModernGridScrollAdapter(grid);
        _adapters.Add(grid, adapter);
        return adapter;
    }

    public ModernVScrollBar VScrollBar => _vBar;
    public ModernHScrollBar HScrollBar => _hBar;

    private ModernGridScrollAdapter(DataGridView grid)
    {
        _grid = grid;
        _grid.ScrollBars = ScrollBars.None;

        _vBar = new ModernVScrollBar
        {
            Width = 8,
            Visible = false
        };
        _hBar = new ModernHScrollBar
        {
            Height = 8,
            Visible = false
        };
        _corner = new Panel
        {
            Size = new Size(8, 8),
            BackColor = UiStyle.CardBackground,
            Visible = false
        };

        _vBar.ValueChanged += OnVBarValueChanged;
        _hBar.ValueChanged += OnHBarValueChanged;

        _grid.Controls.Add(_vBar);
        _grid.Controls.Add(_hBar);
        _grid.Controls.Add(_corner);

        _grid.Resize += OnGridResize;
        _grid.RowsAdded += OnGridRowsChanged;
        _grid.RowsRemoved += OnGridRowsChanged;
        _grid.ColumnWidthChanged += OnGridColumnsChanged;
        _grid.ColumnAdded += OnGridColumnsChanged;
        _grid.ColumnRemoved += OnGridColumnsChanged;
        _grid.DataSourceChanged += OnGridDataSourceChanged;
        _grid.DataBindingComplete += OnGridDataBindingComplete;
        _grid.SelectionChanged += OnGridSelectionChanged;
        _grid.CurrentCellChanged += OnGridCurrentCellChanged;
        _grid.MouseWheel += OnGridMouseWheel;
        _grid.VisibleChanged += OnGridVisibleChanged;
        _grid.HandleCreated += OnGridHandleCreated;
        _grid.Disposed += OnGridDisposed;

        if (_grid.IsHandleCreated)
        {
            RecalculateScroll();
        }
    }

    private void OnVBarValueChanged(object? sender, EventArgs e)
    {
        if (_isSyncing || !_grid.IsHandleCreated || _grid.RowCount == 0) return;
        _isSyncing = true;
        try
        {
            int targetRow = Math.Clamp(_vBar.Value, 0, _grid.RowCount - 1);
            if (_grid.FirstDisplayedScrollingRowIndex != targetRow)
            {
                _grid.FirstDisplayedScrollingRowIndex = targetRow;
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnHBarValueChanged(object? sender, EventArgs e)
    {
        if (_isSyncing || !_grid.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            _grid.HorizontalScrollingOffset = _hBar.Value;
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnGridMouseWheel(object? sender, MouseEventArgs e)
    {
        if ((Control.ModifierKeys & Keys.Shift) != 0)
        {
            if (_hBar.Visible)
            {
                int hDelta = -Math.Sign(e.Delta) * 50;
                _hBar.Value += hDelta;
            }
        }
        else
        {
            if (_vBar.Visible)
            {
                int vDelta = -Math.Sign(e.Delta) * 3;
                _vBar.Value += vDelta;
            }
        }
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e) => SyncFromGrid();
    private void OnGridCurrentCellChanged(object? sender, EventArgs e) => SyncFromGrid();

    private void SyncFromGrid()
    {
        if (_isSyncing || !_grid.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            if (_vBar.Visible && _grid.RowCount > 0 && _grid.FirstDisplayedScrollingRowIndex >= 0)
            {
                _vBar.Value = Math.Clamp(_grid.FirstDisplayedScrollingRowIndex, 0, _vBar.Maximum);
            }
            if (_hBar.Visible)
            {
                _hBar.Value = Math.Clamp(_grid.HorizontalScrollingOffset, 0, _hBar.Maximum);
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnGridResize(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridRowsChanged(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridColumnsChanged(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridDataSourceChanged(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridDataBindingComplete(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridVisibleChanged(object? sender, EventArgs e) => RecalculateScroll();
    private void OnGridHandleCreated(object? sender, EventArgs e) => RecalculateScroll();

    public void RecalculateScroll()
    {
        if (_isDisposed || _grid.IsDisposed || !_grid.IsHandleCreated || _grid.ClientSize.Width <= 0 || _grid.ClientSize.Height <= 0) return;

        try
        {
            // 1. Horizontal metrics
            int totalColsWidth = _grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
            if (_grid.RowHeadersVisible) totalColsWidth += _grid.RowHeadersWidth;
            int viewWidth = _grid.ClientSize.Width;
            int maxH = Math.Max(0, totalColsWidth - viewWidth);

            _hBar.Minimum = 0;
            _hBar.Maximum = maxH;
            _hBar.LargeChange = Math.Max(1, viewWidth);
            _hBar.Visible = maxH > 0;
            if (_hBar.Value > maxH) _hBar.Value = maxH;

            // 2. Vertical metrics
            int rowCount = _grid.RowCount;
            int displayedRows = _grid.DisplayedRowCount(false);
            int maxV = Math.Max(0, rowCount - displayedRows);

            _vBar.Minimum = 0;
            _vBar.Maximum = maxV;
            _vBar.LargeChange = Math.Max(1, displayedRows);
            _vBar.Visible = maxV > 0;
            if (_vBar.Value > maxV) _vBar.Value = maxV;

            // 3. Corner panel
            _corner.Visible = _vBar.Visible && _hBar.Visible;
            _corner.BackColor = _grid.BackgroundColor != Color.Transparent ? _grid.BackgroundColor : UiStyle.CardBackground;

            // 4. Update bounds
            UpdateLayout();
        }
        catch { }
    }

    private void UpdateLayout()
    {
        if (_isDisposed || _grid.IsDisposed || !_grid.IsHandleCreated) return;

        int vWidth = 8;
        int hHeight = 8;
        int clientW = _grid.ClientSize.Width;
        int clientH = _grid.ClientSize.Height;

        if (_vBar.Visible && _hBar.Visible)
        {
            _vBar.SetBounds(clientW - vWidth, 0, vWidth, clientH - hHeight);
            _hBar.SetBounds(0, clientH - hHeight, clientW - vWidth, hHeight);
            _corner.SetBounds(clientW - vWidth, clientH - hHeight, vWidth, hHeight);
        }
        else if (_vBar.Visible)
        {
            _vBar.SetBounds(clientW - vWidth, 0, vWidth, clientH);
        }
        else if (_hBar.Visible)
        {
            _hBar.SetBounds(0, clientH - hHeight, clientW, hHeight);
        }

        _vBar.BringToFront();
        _hBar.BringToFront();
        _corner.BringToFront();
    }

    private void OnGridDisposed(object? sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _grid.Resize -= OnGridResize;
            _grid.RowsAdded -= OnGridRowsChanged;
            _grid.RowsRemoved -= OnGridRowsChanged;
            _grid.ColumnWidthChanged -= OnGridColumnsChanged;
            _grid.ColumnAdded -= OnGridColumnsChanged;
            _grid.ColumnRemoved -= OnGridColumnsChanged;
            _grid.DataSourceChanged -= OnGridDataSourceChanged;
            _grid.DataBindingComplete -= OnGridDataBindingComplete;
            _grid.SelectionChanged -= OnGridSelectionChanged;
            _grid.CurrentCellChanged -= OnGridCurrentCellChanged;
            _grid.MouseWheel -= OnGridMouseWheel;
            _grid.VisibleChanged -= OnGridVisibleChanged;
            _grid.HandleCreated -= OnGridHandleCreated;
            _grid.Disposed -= OnGridDisposed;

            _vBar.Dispose();
            _hBar.Dispose();
            _corner.Dispose();
        }
        catch { }
    }
}

/// <summary>
/// Smooth scrollable container with a sleek ModernVScrollBar and zero native white scrollbars.
/// Uses WinForms native ScrollableControl architecture with a viewport-clipped native scrollbar
/// to guarantee zero visual tearing, zero ghosting, and full RDP hardware-accelerated scrolling.
/// </summary>
public class ModernScrollPanel : Panel, IMessageFilter
{
    private sealed class ModernScrollViewport : Panel
    {
        public ModernScrollViewport()
        {
            AutoScroll = true;
            DoubleBuffered = false;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = UiStyle.CardBackground;
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            if (Parent is ModernScrollPanel p)
            {
                p.SyncScrollBarFromViewport();
            }
        }
    }

    private readonly ModernScrollViewport _viewport = new() { Location = new Point(0, 0) };
    private readonly ModernVScrollBar _scrollBar = new() { Width = 8, Visible = false };
    private Control? _content;
    private bool _isFilterRegistered;
    private bool _isSyncing;
    private bool _isUpdating;

    public Panel Viewport => _viewport;
    public ModernVScrollBar ScrollBar => _scrollBar;

    public ModernScrollPanel()
    {
        SuspendLayout();
        AutoScroll = false;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _viewport.Resize += (_, _) =>
        {
            if (!_isUpdating) RecalculateScroll();
        };

        _scrollBar.ValueChanged += (_, _) =>
        {
            if (_isSyncing || _content == null || _viewport == null) return;
            _isSyncing = true;
            try
            {
                _viewport.AutoScrollPosition = new Point(0, _scrollBar.Value);
            }
            catch { }
            finally
            {
                _isSyncing = false;
            }
        };

        Controls.Add(_viewport);
        Controls.Add(_scrollBar);
        _scrollBar.BringToFront();

        try
        {
            System.Windows.Forms.Application.AddMessageFilter(this);
            _isFilterRegistered = true;
        }
        catch { }

        ResumeLayout(false);
    }

    public override Color BackColor
    {
        get => GetEffectiveParentBackColor();
        set { }
    }

    public Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.CardBackground;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        try
        {
            if (!_isUpdating) UpdateLayout();
        }
        catch { }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        try
        {
            if (!_isUpdating) UpdateLayout();
        }
        catch { }
    }

    private void UpdateLayout()
    {
        if (_isUpdating || IsDisposed) return;
        _isUpdating = true;
        try
        {
            UpdateLayoutCore();
        }
        catch { }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateLayoutCore()
    {
        if (_viewport == null || _scrollBar == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        int scrollBarW = 8;
        bool needBar = _scrollBar.Visible;
        int visibleContentW = needBar ? Math.Max(0, ClientSize.Width - scrollBarW) : ClientSize.Width;

        // Position native scrollbar off-screen by expanding viewport width past the visible content width
        int nativeBarW = SystemInformation.VerticalScrollBarWidth;
        int vpW = visibleContentW + nativeBarW + 6;

        var targetVpBounds = new Rectangle(0, 0, vpW, ClientSize.Height);
        if (_viewport.Bounds != targetVpBounds)
        {
            _viewport.SetBounds(0, 0, vpW, ClientSize.Height);
        }

        if (_content != null)
        {
            var targetMin = new Size(visibleContentW, 0);
            var targetMax = new Size(visibleContentW, 0);
            if (_content.MinimumSize != targetMin)
            {
                _content.MinimumSize = targetMin;
            }
            if (_content.MaximumSize != targetMax)
            {
                _content.MaximumSize = targetMax;
            }
            if (_content.Width != visibleContentW)
            {
                _content.Width = visibleContentW;
            }
        }

        var targetBarBounds = new Rectangle(ClientSize.Width - scrollBarW, 0, scrollBarW, ClientSize.Height);
        if (_scrollBar.Bounds != targetBarBounds)
        {
            _scrollBar.SetBounds(targetBarBounds.X, targetBarBounds.Y, targetBarBounds.Width, targetBarBounds.Height);
        }
        _scrollBar.BringToFront();
    }

    public void SetContent(Control content)
    {
        try
        {
            if (_viewport == null || _scrollBar == null) return;

            _content = content;
            _viewport.Controls.Clear();
            _viewport.Controls.Add(content);

            content.Dock = DockStyle.None;
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            content.Location = new Point(0, 0);

            Color effectiveBg = GetEffectiveParentBackColor();
            _viewport.BackColor = effectiveBg;
            if (content.BackColor == Color.Transparent || content.BackColor == SystemColors.Control)
            {
                content.BackColor = effectiveBg;
            }

            content.SizeChanged += (_, _) =>
            {
                if (!_isUpdating) RecalculateScroll();
            };
            RecalculateScroll();
        }
        catch { }
    }

    public void SyncScrollBarFromViewport()
    {
        if (_isSyncing || _viewport == null || _scrollBar == null) return;
        _isSyncing = true;
        try
        {
            int currentY = Math.Abs(_viewport.AutoScrollPosition.Y);
            if (_scrollBar.Value != currentY)
            {
                _scrollBar.Value = currentY;
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }

    public void RecalculateScroll()
    {
        if (_isUpdating || IsDisposed) return;
        _isUpdating = true;
        try
        {
            if (_content == null || _viewport == null || _scrollBar == null || ClientSize.Height <= 0) return;

            Color effectiveBg = GetEffectiveParentBackColor();
            if (_viewport.BackColor != effectiveBg) _viewport.BackColor = effectiveBg;
            if (_content.BackColor == Color.Transparent || _content.BackColor == SystemColors.Control)
            {
                _content.BackColor = effectiveBg;
            }

            int scrollBarW = 8;
            bool needBar = _scrollBar.Visible;
            int visibleContentW = needBar ? Math.Max(0, ClientSize.Width - scrollBarW) : ClientSize.Width;

            var targetMin = new Size(visibleContentW, 0);
            var targetMax = new Size(visibleContentW, 0);
            if (_content.MinimumSize != targetMin)
            {
                _content.MinimumSize = targetMin;
            }
            if (_content.MaximumSize != targetMax)
            {
                _content.MaximumSize = targetMax;
            }
            if (_content.Width != visibleContentW)
            {
                _content.Width = visibleContentW;
            }

            int contentH = _content.PreferredSize.Height;
            foreach (Control c in _content.Controls)
            {
                if (c.Visible)
                {
                    int bottom = c.Bottom + c.Margin.Bottom;
                    if (bottom > contentH) contentH = bottom;
                }
            }

            if (contentH <= 0 || contentH < _content.Height)
            {
                contentH = _content.Height;
            }

            var targetContentSize = new Size(visibleContentW, contentH);
            if (_content.Size != targetContentSize)
            {
                _content.Size = targetContentSize;
            }

            var targetMinSize = new Size(0, contentH);
            if (_viewport.AutoScrollMinSize != targetMinSize)
            {
                _viewport.AutoScrollMinSize = targetMinSize;
            }

            int max = Math.Max(0, contentH - ClientSize.Height);
            _scrollBar.Maximum = max;
            _scrollBar.LargeChange = Math.Max(1, ClientSize.Height);
            _scrollBar.Visible = max > 0;

            if (_scrollBar.Value > max)
            {
                _scrollBar.Value = max;
            }

            UpdateLayoutCore();

            if (!_isSyncing)
            {
                _isSyncing = true;
                try
                {
                    _viewport.AutoScrollPosition = new Point(0, _scrollBar.Value);
                }
                catch { }
                finally
                {
                    _isSyncing = false;
                }
            }
        }
        catch { }
        finally
        {
            _isUpdating = false;
        }
    }

    public bool PreFilterMessage(ref Message m)
    {
        // Intercept WM_MOUSEWHEEL (0x020A) if cursor is inside this container
        if (m.Msg == 0x020A && IsHandleCreated && Visible && _scrollBar.Visible)
        {
            var cursorScreen = Cursor.Position;
            var screenBounds = RectangleToScreen(ClientRectangle);
            if (screenBounds.Contains(cursorScreen))
            {
                var child = FromChildHandle(m.HWnd);
                if (child is TextBox tb && tb.Multiline && tb.ScrollBars != ScrollBars.None)
                {
                    return false;
                }

                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                _scrollBar.ScrollBy(-Math.Sign(delta) * 50);
                return true;
            }
        }
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _isFilterRegistered)
        {
            try
            {
                System.Windows.Forms.Application.RemoveMessageFilter(this);
                _isFilterRegistered = false;
            }
            catch { }
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Modern multiline text box with integrated soft ModernVScrollBar and dark styled focus border.
/// </summary>
public class ModernMultilineTextBox : Panel
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int EM_GETLINECOUNT = 0x00BA;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_LINESCROLL = 0x00B6;

    private readonly TextBox _innerBox;
    private readonly ModernVScrollBar _scrollBar;
    private bool _isFocused = false;
    private bool _isSyncing = false;

    public TextBox InnerTextBox => _innerBox;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _innerBox.Text ?? string.Empty;
        set
        {
            _innerBox.Text = value ?? string.Empty;
            SyncScrollBar();
        }
    }

    public string[] Lines
    {
        get => _innerBox.Lines;
        set
        {
            _innerBox.Lines = value ?? [];
            SyncScrollBar();
        }
    }

    public new event EventHandler? TextChanged
    {
        add => _innerBox.TextChanged += value;
        remove => _innerBox.TextChanged -= value;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override Font Font
    {
        get => _innerBox.Font;
        set
        {
            base.Font = value!;
            if (value != null) _innerBox.Font = value;
            SyncScrollBar();
        }
    }

    public bool ReadOnly
    {
        get => _innerBox.ReadOnly;
        set => _innerBox.ReadOnly = value;
    }

    public int MaxLength
    {
        get => _innerBox.MaxLength;
        set => _innerBox.MaxLength = value;
    }

    public void Clear()
    {
        _innerBox.Clear();
        SyncScrollBar();
    }

    public void AppendText(string text)
    {
        _innerBox.AppendText(text);
        SyncScrollBar();
    }

    public void Select(int start, int length)
    {
        _innerBox.Select(start, length);
    }

    public ModernMultilineTextBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        Padding = new Padding(6, 6, 2, 6);
        BackColor = UiStyle.InputBackground;

        _innerBox = new TextBox
        {
            Multiline = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.None,
            Dock = DockStyle.Fill,
            BackColor = UiStyle.InputBackground,
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI", 9F)
        };

        _scrollBar = new ModernVScrollBar
        {
            Dock = DockStyle.Right,
            Width = 8,
            Visible = false
        };

        _innerBox.Enter += (_, _) => { _isFocused = true; Invalidate(); };
        _innerBox.Leave += (_, _) => { _isFocused = false; Invalidate(); };
        _innerBox.TextChanged += (_, _) => SyncScrollBar();
        _innerBox.SizeChanged += (_, _) => SyncScrollBar();
        _innerBox.KeyUp += (_, _) => SyncFromBox();
        _innerBox.MouseUp += (_, _) => SyncFromBox();
        _innerBox.MouseWheel += (_, _) => SyncFromBox();

        _scrollBar.ValueChanged += (_, _) =>
        {
            if (_isSyncing || !_innerBox.IsHandleCreated) return;
            _isSyncing = true;
            try
            {
                int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
                int delta = _scrollBar.Value - currentTop;
                if (delta != 0)
                {
                    SendMessage(_innerBox.Handle, EM_LINESCROLL, 0, delta);
                }
            }
            finally
            {
                _isSyncing = false;
            }
        };

        Controls.Add(_innerBox);
        Controls.Add(_scrollBar);
    }

    private void SyncFromBox()
    {
        if (_isSyncing || !_innerBox.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            _scrollBar.Value = currentTop;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void SyncScrollBar()
    {
        if (_isSyncing || !_innerBox.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            int lineCount = SendMessage(_innerBox.Handle, EM_GETLINECOUNT, 0, 0);
            int fontH = Math.Max(1, _innerBox.Font.Height);
            int visibleLines = Math.Max(1, _innerBox.ClientSize.Height / fontH);

            int max = Math.Max(0, lineCount - visibleLines);
            _scrollBar.Maximum = max;
            _scrollBar.LargeChange = visibleLines;
            _scrollBar.Visible = max > 0;

            int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            _scrollBar.Value = Math.Clamp(currentTop, 0, max);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color borderColor = _isFocused ? UiStyle.PrimaryColor : UiStyle.BorderColor;
        using var pen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 6);
        g.DrawPath(pen, path);
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
/// Modern dark-theme ComboBox with custom-painted sleek dark dropdown button, chevrons, and styled popup list.
/// </summary>
public class ModernComboBox : ComboBox
{
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;
    private bool _isHovered;

    public ModernComboBox()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        ItemHeight = 26;
        Font = UiStyle.BaseFont;
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

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnDropDown(EventArgs e)
    {
        base.OnDropDown(e);
        Invalidate();
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        UiStyle.DrawComboBoxItem(this, e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && DropDownStyle != ComboBoxStyle.Simple)
        {
            PaintComboBoxOverlay();
        }
    }

    private void PaintComboBoxOverlay()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;

        using var g = Graphics.FromHwnd(Handle);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int btnWidth = 26;
        var btnRect = new Rectangle(Width - btnWidth, 1, btnWidth - 1, Height - 2);
        bool isActive = _isHovered || DroppedDown;
        Color btnBg = !Enabled
            ? UiStyle.InputBackground
            : (isActive ? UiStyle.SecondaryHover : UiStyle.SecondaryColor);

        using (var brush = new SolidBrush(btnBg))
        {
            g.FillRectangle(brush, btnRect);
        }

        using (var sepPen = new Pen(UiStyle.BorderColor, 1f))
        {
            g.DrawLine(sepPen, btnRect.X, 2, btnRect.X, Height - 3);
        }

        int centerX = btnRect.X + (btnRect.Width / 2);
        int centerY = btnRect.Y + (btnRect.Height / 2);
        Color arrowColor = !Enabled
            ? UiStyle.TextMuted
            : (isActive ? Color.White : UiStyle.TextMuted);

        using (var arrowPen = new Pen(arrowColor, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            using var path = new GraphicsPath();
            if (DroppedDown)
            {
                path.AddLine(centerX - 4, centerY + 2, centerX, centerY - 2);
                path.AddLine(centerX, centerY - 2, centerX + 4, centerY + 2);
            }
            else
            {
                path.AddLine(centerX - 4, centerY - 2, centerX, centerY + 2);
                path.AddLine(centerX, centerY + 2, centerX + 4, centerY - 2);
            }
            g.DrawPath(arrowPen, path);
        }

        Color borderColor = !Enabled
            ? UiStyle.BorderColor
            : ((Focused || _isHovered || DroppedDown) ? UiStyle.PrimaryColor : UiStyle.BorderColor);
        using (var borderPen = new Pen(borderColor, 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }
}

/// <summary>
/// Modern dark-theme NumericUpDown with custom-painted dark spinner buttons, sleek vector chevrons, and themed glowing border.
/// </summary>
public class ModernNumericUpDown : NumericUpDown
{
    private readonly UpDownButtonsPainter _buttonsPainter;
    private bool _isHovered;

    public bool IsControlFocused => Focused || _buttonsPainter.IsEditBoxFocused;
    public bool IsControlHovered => _isHovered || _buttonsPainter.IsHovered;

    public ModernNumericUpDown()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        BorderStyle = BorderStyle.FixedSingle;

        _buttonsPainter = new UpDownButtonsPainter(this);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawOuterBorder(e.Graphics);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x000F || m.Msg == 0x0085) // WM_PAINT or WM_NCPAINT
        {
            using var g = Graphics.FromHwnd(Handle);
            DrawOuterBorder(g);
        }
    }

    private void DrawOuterBorder(Graphics g)
    {
        Color borderColor = IsControlFocused
            ? UiStyle.PrimaryColor
            : (IsControlHovered ? Color.FromArgb(129, 140, 248) : UiStyle.BorderColor);
        using var borderPen = new Pen(borderColor, 1f);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private sealed class UpDownButtonsPainter : NativeWindow
    {
        private readonly ModernNumericUpDown _owner;
        private Control? _buttonsControl;
        private Control? _editBox;
        private bool _upHover;
        private bool _downHover;
        private bool _upPressed;
        private bool _downPressed;

        public bool IsEditBoxFocused => _editBox != null && _editBox.Focused;
        public bool IsHovered => _upHover || _downHover;

        public UpDownButtonsPainter(ModernNumericUpDown owner)
        {
            _owner = owner;
            _owner.HandleCreated += (_, _) => Attach();
            _owner.Layout += (_, _) => Attach();
            if (_owner.IsHandleCreated) Attach();
        }

        private void Attach()
        {
            if (_owner.Controls.Count > 0 && _buttonsControl == null)
            {
                _buttonsControl = _owner.Controls[0];
                if (_buttonsControl.IsHandleCreated)
                {
                    AssignHandle(_buttonsControl.Handle);
                }
                else
                {
                    _buttonsControl.HandleCreated += (_, _) => AssignHandle(_buttonsControl.Handle);
                }

                _buttonsControl.HandleDestroyed += (_, _) => ReleaseHandle();
                _buttonsControl.Resize += (_, _) => _buttonsControl.Invalidate();
            }

            if (_owner.Controls.Count > 1 && _editBox == null)
            {
                _editBox = _owner.Controls[1];
                _editBox.BackColor = UiStyle.InputBackground;
                _editBox.ForeColor = UiStyle.TextDark;
                _editBox.Enter += (_, _) => _owner.Invalidate();
                _editBox.Leave += (_, _) => _owner.Invalidate();
                _editBox.MouseEnter += (_, _) => _owner.Invalidate();
                _editBox.MouseLeave += (_, _) => _owner.Invalidate();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (_buttonsControl == null || !_buttonsControl.IsHandleCreated)
            {
                base.WndProc(ref m);
                return;
            }

            switch (m.Msg)
            {
                case 0x0014: // WM_ERASEBKGND
                    m.Result = (IntPtr)1;
                    return;

                case 0x000F: // WM_PAINT
                    base.WndProc(ref m);
                    PaintButtonsDoubleBuffered();
                    return;

                case 0x0318: // WM_PRINTCLIENT
                    using (var g = Graphics.FromHdc(m.WParam))
                    {
                        DrawButtons(g, _buttonsControl.Width, _buttonsControl.Height);
                    }
                    m.Result = IntPtr.Zero;
                    return;

                case 0x0020: // WM_SETCURSOR
                    Cursor.Current = Cursors.Hand;
                    m.Result = (IntPtr)1;
                    return;

                case 0x0200: // WM_MOUSEMOVE
                    int y = (int)(m.LParam.ToInt64() >> 16) & 0xFFFF;
                    int half = _buttonsControl.Height / 2;
                    bool newUp = y < half;
                    bool newDown = y >= half;
                    if (newUp != _upHover || newDown != _downHover)
                    {
                        _upHover = newUp;
                        _downHover = newDown;
                        _buttonsControl.Invalidate();
                        _owner.Invalidate();
                    }
                    base.WndProc(ref m);
                    break;

                case 0x02A3: // WM_MOUSELEAVE
                    if (_upHover || _downHover)
                    {
                        _upHover = false;
                        _downHover = false;
                        _buttonsControl.Invalidate();
                        _owner.Invalidate();
                    }
                    base.WndProc(ref m);
                    break;

                case 0x0201: // WM_LBUTTONDOWN
                    int yDown = (int)(m.LParam.ToInt64() >> 16) & 0xFFFF;
                    int halfDown = _buttonsControl.Height / 2;
                    if (yDown < halfDown) _upPressed = true; else _downPressed = true;
                    _buttonsControl.Invalidate();
                    base.WndProc(ref m);
                    PaintButtonsDoubleBuffered();
                    break;

                case 0x0202: // WM_LBUTTONUP
                    _upPressed = false;
                    _downPressed = false;
                    _buttonsControl.Invalidate();
                    base.WndProc(ref m);
                    PaintButtonsDoubleBuffered();
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private void PaintButtonsDoubleBuffered()
        {
            if (_buttonsControl == null || !_buttonsControl.IsHandleCreated) return;
            int w = _buttonsControl.Width;
            int h = _buttonsControl.Height;
            if (w <= 0 || h <= 0) return;

            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                DrawButtons(g, w, h);
            }
            using (var dc = Graphics.FromHwnd(_buttonsControl.Handle))
            {
                dc.DrawImageUnscaled(bmp, 0, 0);
            }
        }

        private void DrawButtons(Graphics g, int w, int h)
        {
            if (_buttonsControl == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int half = h / 2;
            bool isEnabled = _owner.Enabled;

            Color bgBase = UiStyle.CardBackground;
            Color bgHover = Color.FromArgb(49, 46, 129); // Modern Indigo Tint hover
            Color bgPressed = UiStyle.PrimaryColor;
            Color chevronMuted = UiStyle.TextMuted;
            Color chevronActive = Color.White;
            Color borderColor = UiStyle.BorderColor;

            // 1. Up Button Box
            var upRect = new Rectangle(0, 0, w, half);
            Color upBg = !isEnabled
                ? Color.FromArgb(20, 27, 45)
                : (_upPressed ? bgPressed : (_upHover ? bgHover : bgBase));

            using (var brush = new SolidBrush(upBg))
            {
                g.FillRectangle(brush, upRect);
            }

            // Up Chevron
            int cx = w / 2;
            int cyUp = half / 2;
            Color upChevronColor = !isEnabled
                ? Color.FromArgb(71, 85, 105)
                : ((_upHover || _upPressed) ? chevronActive : chevronMuted);

            float arrowHalfW = Math.Clamp(w * 0.22f, 3.5f, 5.5f);
            float arrowH = Math.Clamp(half * 0.28f, 2.5f, 4.5f);

            using (var path = new GraphicsPath())
            {
                path.AddLine(cx - arrowHalfW, cyUp + (arrowH / 2f), cx, cyUp - (arrowH / 2f));
                path.AddLine(cx, cyUp - (arrowH / 2f), cx + arrowHalfW, cyUp + (arrowH / 2f));
                using var pen = new Pen(upChevronColor, 2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(pen, path);
            }

            // 2. Down Button Box
            var downRect = new Rectangle(0, half, w, h - half);
            Color downBg = !isEnabled
                ? Color.FromArgb(20, 27, 45)
                : (_downPressed ? bgPressed : (_downHover ? bgHover : bgBase));

            using (var brush = new SolidBrush(downBg))
            {
                g.FillRectangle(brush, downRect);
            }

            // Down Chevron
            int cyDown = half + ((h - half) / 2);
            Color downChevronColor = !isEnabled
                ? Color.FromArgb(71, 85, 105)
                : ((_downHover || _downPressed) ? chevronActive : chevronMuted);

            using (var path = new GraphicsPath())
            {
                path.AddLine(cx - arrowHalfW, cyDown - (arrowH / 2f), cx, cyDown + (arrowH / 2f));
                path.AddLine(cx, cyDown + (arrowH / 2f), cx + arrowHalfW, cyDown - (arrowH / 2f));
                using var pen = new Pen(downChevronColor, 2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawPath(pen, path);
            }

            // 3. Dividers
            using (var divPen = new Pen(borderColor, 1f))
            {
                // Left vertical separator
                g.DrawLine(divPen, 0, 0, 0, h);
                // Middle horizontal separator
                g.DrawLine(divPen, 0, half, w, half);
            }
        }
    }
}

/// <summary>
/// Modern horizontal stepper control with sleek [-] and [+] action buttons, dark theme, and smooth value adjustments.
/// </summary>
public class ModernStepperControl : Panel
{
    private readonly ModernButtonControl _btnMinus = new();
    private readonly ModernButtonControl _btnPlus = new();
    private readonly TextBox _txtValue = new();
    private decimal _value = 0m;
    private decimal _minimum = 0m;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimalPlaces = 0;
    private bool _isUpdating = false;

    public event EventHandler? ValueChanged;

    public decimal Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                UpdateDisplay();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public decimal Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_value < _minimum) Value = _minimum; }
    }

    public decimal Maximum
    {
        get => _maximum;
        set { _maximum = value; if (_value > _maximum) Value = _maximum; }
    }

    public decimal Increment
    {
        get => _increment;
        set => _increment = value > 0 ? value : 1m;
    }

    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set { _decimalPlaces = Math.Max(0, value); UpdateDisplay(); }
    }

    public string Suffix { get; set; } = string.Empty;

    public ModernStepperControl()
    {
        Size = new Size(160, 36);
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        DoubleBuffered = true;

        BuildLayout();
    }

    private void BuildLayout()
    {
        Controls.Clear();

        _btnMinus.Text = "−";
        _btnMinus.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _btnMinus.Width = 34;
        _btnMinus.Dock = DockStyle.Left;
        _btnMinus.BackColor = UiStyle.CardBackground;
        _btnMinus.ForeColor = UiStyle.TextMuted;
        _btnMinus.Click += (_, _) => Value -= _increment;

        _btnPlus.Text = "+";
        _btnPlus.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        _btnPlus.Width = 34;
        _btnPlus.Dock = DockStyle.Right;
        _btnPlus.BackColor = UiStyle.CardBackground;
        _btnPlus.ForeColor = UiStyle.TextMuted;
        _btnPlus.Click += (_, _) => Value += _increment;

        _txtValue.Dock = DockStyle.Fill;
        _txtValue.BorderStyle = BorderStyle.None;
        _txtValue.TextAlign = HorizontalAlignment.Center;
        _txtValue.Font = new Font("Segoe UI Semibold", 10F);
        _txtValue.BackColor = UiStyle.InputBackground;
        _txtValue.ForeColor = Color.White;
        _txtValue.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;
            var clean = _txtValue.Text.Replace(Suffix, "").Trim();
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ||
                decimal.TryParse(clean, out v))
            {
                _value = Math.Clamp(v, _minimum, _maximum);
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        _txtValue.Leave += (_, _) => UpdateDisplay();

        Controls.Add(_txtValue);
        Controls.Add(_btnMinus);
        Controls.Add(_btnPlus);

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _isUpdating = true;
        try
        {
            string fmt = _decimalPlaces > 0 ? $"N{_decimalPlaces}" : "0";
            _txtValue.Text = string.IsNullOrEmpty(Suffix)
                ? _value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)
                : $"{_value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)} {Suffix}";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiStyle.BorderColor, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>
/// Modern custom-drawn CheckedListBox with sleek dark theme, custom vector checkboxes,
/// smooth scrolling via ModernVScrollBar, comfortable row spacing, and full WinForms API compatibility.
/// </summary>
public class ModernCheckedListBox : Control
{
    public class CheckedItemEntry
    {
        public object Value { get; set; }
        public CheckState CheckState { get; set; }
        public bool Checked => CheckState == CheckState.Checked;

        public CheckedItemEntry(object value, CheckState state)
        {
            Value = value;
            CheckState = state;
        }

        public override string ToString() => Value?.ToString() ?? string.Empty;
    }

    private readonly List<CheckedItemEntry> _items = new();
    private readonly ModernVScrollBar _scrollBar;
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;
    private int _itemHeight = 28;
    private bool _checkOnClick = true;
    private bool _isFocused = false;

    public event ItemCheckEventHandler? ItemCheck;
    public event EventHandler? SelectedIndexChanged;

    public int ItemHeight
    {
        get => _itemHeight;
        set { _itemHeight = Math.Max(20, value); UpdateScroll(); Invalidate(); }
    }

    public bool CheckOnClick
    {
        get => _checkOnClick;
        set => _checkOnClick = value;
    }

    public Color BorderColor { get; set; } = UiStyle.BorderColor;
    public Color BorderFocusColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color HoverColor { get; set; } = UiStyle.CardHoverBackground;

    public ObjectCollection Items { get; }
    public CheckedItemCollection CheckedItems { get; }
    public CheckedIndexCollection CheckedIndices { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = (value < 0 || value >= _items.Count) ? -1 : value;
            if (_selectedIndex != clamped)
            {
                _selectedIndex = clamped;
                EnsureVisible(_selectedIndex);
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? SelectedItem
    {
        get => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex].Value : null;
        set
        {
            int idx = -1;
            if (value != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(_items[i].Value, value))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            SelectedIndex = idx;
        }
    }

    public ModernCheckedListBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        Font = UiStyle.BaseFont;

        Items = new ObjectCollection(this);
        CheckedItems = new CheckedItemCollection(this);
        CheckedIndices = new CheckedIndexCollection(this);

        _scrollBar = new ModernVScrollBar
        {
            Visible = false,
            Width = 8,
            Dock = DockStyle.None
        };
        _scrollBar.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scrollBar);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateScroll();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateScroll();
    }

    private void UpdateScroll()
    {
        if (_scrollBar == null) return;
        int availableH = Math.Max(1, Height - 4);
        int visibleRows = availableH / _itemHeight;
        int maxScroll = Math.Max(0, _items.Count - visibleRows);

        if (maxScroll > 0)
        {
            _scrollBar.Visible = true;
            _scrollBar.Location = new Point(Width - _scrollBar.Width - 2, 2);
            _scrollBar.Height = Math.Max(10, Height - 4);
            _scrollBar.Maximum = maxScroll;
            _scrollBar.LargeChange = Math.Max(1, visibleRows);
            if (_scrollBar.Value > maxScroll) _scrollBar.Value = maxScroll;
        }
        else
        {
            _scrollBar.Visible = false;
            _scrollBar.Value = 0;
        }
    }

    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= _items.Count || !_scrollBar.Visible) return;
        int visibleRows = Math.Max(1, (Height - 4) / _itemHeight);
        if (index < _scrollBar.Value)
        {
            _scrollBar.Value = index;
        }
        else if (index >= _scrollBar.Value + visibleRows)
        {
            _scrollBar.Value = index - visibleRows + 1;
        }
    }

    public bool GetItemChecked(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        return _items[index].Checked;
    }

    public void SetItemChecked(int index, bool isChecked)
    {
        SetItemCheckState(index, isChecked ? CheckState.Checked : CheckState.Unchecked);
    }

    public CheckState GetItemCheckState(int index)
    {
        if (index < 0 || index >= _items.Count) return CheckState.Unchecked;
        return _items[index].CheckState;
    }

    public void SetItemCheckState(int index, CheckState value)
    {
        if (index < 0 || index >= _items.Count) return;
        var current = _items[index].CheckState;
        if (current != value)
        {
            var e = new ItemCheckEventArgs(index, value, current);
            ItemCheck?.Invoke(this, e);
            _items[index].CheckState = e.NewValue;
            Invalidate();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button == MouseButtons.Left)
        {
            int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
            int clickedIdx = scrollVal + ((e.Y - 2) / _itemHeight);
            if (clickedIdx >= 0 && clickedIdx < _items.Count)
            {
                SelectedIndex = clickedIdx;
                int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
                if (e.X >= 2 && e.X <= rowW)
                {
                    if (_checkOnClick || e.X <= 32)
                    {
                        SetItemChecked(clickedIdx, !GetItemChecked(clickedIdx));
                    }
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
        int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
        int prevHover = _hoverIndex;

        if (e.X >= 2 && e.X <= rowW && e.Y >= 2 && e.Y < Height - 2)
        {
            int idx = scrollVal + ((e.Y - 2) / _itemHeight);
            _hoverIndex = (idx >= 0 && idx < _items.Count) ? idx : -1;
        }
        else
        {
            _hoverIndex = -1;
        }

        if (prevHover != _hoverIndex) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scrollBar.Visible)
        {
            int delta = -Math.Sign(e.Delta);
            _scrollBar.Value += delta;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Down)
        {
            if (SelectedIndex < _items.Count - 1)
            {
                SelectedIndex++;
                EnsureVisible(SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
                EnsureVisible(SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Space)
        {
            if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
            {
                SetItemChecked(SelectedIndex, !GetItemChecked(SelectedIndex));
            }
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        using (var bgBrush = new SolidBrush(BackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
        int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
        int visibleCount = (Height / _itemHeight) + 2;

        int startIdx = scrollVal;
        int endIdx = Math.Min(_items.Count, startIdx + visibleCount);

        for (int i = startIdx; i < endIdx; i++)
        {
            int rowY = 2 + (i - startIdx) * _itemHeight;
            var rowRect = new Rectangle(2, rowY, rowW, _itemHeight);

            bool isHovered = (i == _hoverIndex);
            bool isSelected = (i == _selectedIndex);

            if (isHovered || isSelected)
            {
                Color rowBg = isHovered ? HoverColor : Color.FromArgb(40, 52, 75);
                using var rowBrush = new SolidBrush(rowBg);
                using var rowPath = ModernCardPanel.CreateRoundedRectanglePath(new Rectangle(rowRect.X + 2, rowRect.Y + 1, rowRect.Width - 4, rowRect.Height - 2), 4);
                g.FillPath(rowBrush, rowPath);
            }

            var entry = _items[i];
            int boxSize = 18;
            int boxX = rowRect.X + 8;
            int boxY = rowRect.Y + (_itemHeight - boxSize) / 2;
            var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            ModernCheckBox.DrawBox(
                g,
                boxRect,
                entry.Checked,
                entry.CheckState == CheckState.Indeterminate,
                isHovered,
                false,
                Enabled,
                boxSize,
                4);

            int textX = boxRect.Right + 8;
            int textW = Math.Max(0, rowRect.Right - textX - 4);
            var textRect = new Rectangle(textX, rowRect.Y, textW, _itemHeight);

            Color textClr = Enabled ? ForeColor : UiStyle.TextMuted;
            TextRenderer.DrawText(
                g,
                entry.ToString(),
                Font,
                textRect,
                textClr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // Draw Border
        Color border = _isFocused ? BorderFocusColor : BorderColor;
        using (var borderPen = new Pen(border, 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }

    public class ObjectCollection : IList, ICollection, IEnumerable
    {
        private readonly ModernCheckedListBox _owner;

        internal ObjectCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get => _owner._items[index].Value;
            set
            {
                _owner._items[index].Value = value ?? string.Empty;
                _owner.Invalidate();
            }
        }

        public int Add(object item)
        {
            return Add(item, CheckState.Unchecked);
        }

        public int Add(object item, bool isChecked)
        {
            return Add(item, isChecked ? CheckState.Checked : CheckState.Unchecked);
        }

        public int Add(object item, CheckState checkState)
        {
            var entry = new CheckedItemEntry(item, checkState);
            _owner._items.Add(entry);
            _owner.UpdateScroll();
            _owner.Invalidate();
            return _owner._items.Count - 1;
        }

        public void AddRange(object[] items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                _owner._items.Add(new CheckedItemEntry(item, CheckState.Unchecked));
            }
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public void AddRange(IEnumerable<object> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                _owner._items.Add(new CheckedItemEntry(item, CheckState.Unchecked));
            }
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public void Clear()
        {
            _owner._items.Clear();
            _owner._selectedIndex = -1;
            _owner._hoverIndex = -1;
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public bool Contains(object? value)
        {
            if (value == null) return false;
            return _owner._items.Any(e => Equals(e.Value, value));
        }

        public int IndexOf(object? value)
        {
            if (value == null) return -1;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (Equals(_owner._items[i].Value, value)) return i;
            }
            return -1;
        }

        public void Insert(int index, object? value)
        {
            _owner._items.Insert(index, new CheckedItemEntry(value ?? string.Empty, CheckState.Unchecked));
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        int IList.Add(object? value) => Add(value ?? string.Empty);

        public void Remove(object? value)
        {
            int idx = IndexOf(value);
            if (idx >= 0) RemoveAt(idx);
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _owner._items.Count)
            {
                _owner._items.RemoveAt(index);
                if (_owner._selectedIndex >= _owner._items.Count)
                    _owner._selectedIndex = _owner._items.Count - 1;
                _owner.UpdateScroll();
                _owner.Invalidate();
            }
        }

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < _owner._items.Count; i++)
            {
                array.SetValue(_owner._items[i].Value, index + i);
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var item in _owner._items)
            {
                yield return item.Value;
            }
        }
    }

    public class CheckedItemCollection : IList, ICollection, IEnumerable
    {
        private readonly ModernCheckedListBox _owner;

        internal CheckedItemCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count(e => e.Checked);
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get
            {
                int current = 0;
                foreach (var e in _owner._items)
                {
                    if (e.Checked)
                    {
                        if (current == index) return e.Value;
                        current++;
                    }
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            set => throw new NotSupportedException();
        }

        public bool Contains(object? value)
        {
            if (value == null) return false;
            return _owner._items.Any(e => e.Checked && Equals(e.Value, value));
        }

        public int IndexOf(object? value)
        {
            if (value == null) return -1;
            int idx = 0;
            foreach (var e in _owner._items)
            {
                if (e.Checked)
                {
                    if (Equals(e.Value, value)) return idx;
                    idx++;
                }
            }
            return -1;
        }

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            int i = 0;
            foreach (var e in _owner._items)
            {
                if (e.Checked)
                {
                    array.SetValue(e.Value, index + i);
                    i++;
                }
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var e in _owner._items)
            {
                if (e.Checked) yield return e.Value;
            }
        }
    }

    public class CheckedIndexCollection : IList, ICollection, IEnumerable<int>
    {
        private readonly ModernCheckedListBox _owner;

        internal CheckedIndexCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count(e => e.Checked);
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public int this[int index]
        {
            get
            {
                int current = 0;
                for (int i = 0; i < _owner._items.Count; i++)
                {
                    if (_owner._items[i].Checked)
                    {
                        if (current == index) return i;
                        current++;
                    }
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public bool Contains(int index) => _owner.GetItemChecked(index);
        bool IList.Contains(object? value) => value is int i && Contains(i);

        public int IndexOf(int index)
        {
            int current = 0;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked)
                {
                    if (i == index) return current;
                    current++;
                }
            }
            return -1;
        }
        int IList.IndexOf(object? value) => value is int i ? IndexOf(i) : -1;

        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            int cur = 0;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked)
                {
                    array.SetValue(i, index + cur);
                    cur++;
                }
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked) yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// Modern custom-styled DateTimePicker with rounded borders, glowing indigo focus/hover effects,
/// responsive vector calendar glyph, flip chevron, and multi-view SaaS calendar popup (Day/Month/Year).
/// </summary>
public class ModernDateTimePicker : Control
{
    private DateTime _value = DateTime.Today;
    private DateTime _minDate = new(1753, 1, 1);
    private DateTime _maxDate = new(9998, 12, 31);
    private DateTimePickerFormat _format = DateTimePickerFormat.Short;
    private string? _customFormat;
    private bool _isHovered;
    private bool _isButtonHovered;
    private bool _isFocused;
    private int _cornerRadius = 8;
    private bool _showLeadingIcon = true;
    private ModernCalendarDropDown? _dropDown;

    public event EventHandler? ValueChanged;

    public DateTime Value
    {
        get => _value;
        set
        {
            var clamped = value < _minDate ? _minDate : (value > _maxDate ? _maxDate : value);
            if (_value != clamped)
            {
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public DateTime MinDate
    {
        get => _minDate;
        set
        {
            _minDate = value;
            if (_value < _minDate) Value = _minDate;
        }
    }

    public DateTime MaxDate
    {
        get => _maxDate;
        set
        {
            _maxDate = value;
            if (_value > _maxDate) Value = _maxDate;
        }
    }

    public DateTimePickerFormat Format
    {
        get => _format;
        set { _format = value; Invalidate(); }
    }

    public string? CustomFormat
    {
        get => _customFormat;
        set { _customFormat = value; Invalidate(); }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    public bool ShowLeadingIcon
    {
        get => _showLeadingIcon;
        set { _showLeadingIcon = value; Invalidate(); }
    }

    public override string Text => GetFormattedText();

    public Color BorderColor { get; set; } = UiStyle.BorderColor;
    public Color BorderHoverColor { get; set; } = Color.FromArgb(100, 116, 139);
    public Color BorderFocusColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color ButtonHoverColor { get; set; } = UiStyle.CardHoverBackground;
    public Color AccentColor { get; set; } = Color.FromArgb(99, 102, 241);

    public bool IsOpen => _dropDown != null && _dropDown.Visible;

    public ModernDateTimePicker()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        Size = new Size(130, 32);
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        Font = UiStyle.BaseFont;
        Cursor = Cursors.Hand;
    }

    private string GetFormattedText()
    {
        return _format switch
        {
            DateTimePickerFormat.Long => _value.ToLongDateString(),
            DateTimePickerFormat.Time => _value.ToShortTimeString(),
            DateTimePickerFormat.Custom when !string.IsNullOrEmpty(_customFormat) => _value.ToString(_customFormat),
            _ => _value.ToShortDateString()
        };
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
        _isButtonHovered = false;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool prevBtnHover = _isButtonHovered;
        _isButtonHovered = (e.X >= Width - 28);
        if (prevBtnHover != _isButtonHovered) Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            ToggleDropDown();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || (e.Alt && e.KeyCode == Keys.Down) || e.KeyCode == Keys.F4)
        {
            ToggleDropDown();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && IsOpen)
        {
            CloseDropDown();
            e.Handled = true;
        }
        else if (!IsOpen)
        {
            if (e.KeyCode == Keys.Left)
            {
                Value = Value.AddDays(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                Value = Value.AddDays(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                Value = Value.AddDays(-7);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                Value = Value.AddDays(7);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                Value = Value.AddMonths(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                Value = Value.AddMonths(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                Value = DateTime.Today;
                e.Handled = true;
            }
        }
    }

    public void ToggleDropDown()
    {
        if (IsOpen)
            CloseDropDown();
        else
            ShowDropDown();
    }

    public void ShowDropDown()
    {
        if (IsOpen)
        {
            _dropDown?.Close();
            return;
        }

        var calendarView = new ModernCalendarView(this);
        var host = new ToolStripControlHost(calendarView)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false,
            Size = calendarView.Size
        };

        _dropDown = new ModernCalendarDropDown();
        _dropDown.Items.Add(host);
        _dropDown.Closed += (_, _) => { _dropDown = null; Invalidate(); };

        var screenPt = PointToScreen(new Point(0, Height + 3));
        var workingArea = Screen.FromControl(this).WorkingArea;
        if (screenPt.Y + calendarView.Height > workingArea.Bottom)
        {
            screenPt.Y = PointToScreen(new Point(0, -calendarView.Height - 3)).Y;
        }
        if (screenPt.X + calendarView.Width > workingArea.Right)
        {
            screenPt.X = Math.Max(0, workingArea.Right - calendarView.Width - 4);
        }

        _dropDown.Show(screenPt);
        calendarView.Focus();
        Invalidate();
    }

    public void CloseDropDown()
    {
        if (_dropDown != null && _dropDown.Visible)
        {
            _dropDown.Close();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Math.Min(_cornerRadius, Height / 2);

        // 1. Fill Background
        using (var path = ModernCardPanel.CreateRoundedRectanglePath(bounds, radius))
        {
            using (var bgBrush = new SolidBrush(BackColor))
            {
                g.FillPath(bgBrush, path);
            }

            // 2. Button hover pill or highlight on the right
            var btnRect = new Rectangle(Width - 28, 2, 26, Height - 4);
            if (_isButtonHovered || IsOpen)
            {
                using var btnBrush = new SolidBrush(ButtonHoverColor);
                using var btnPath = ModernCardPanel.CreateRoundedRectanglePath(btnRect, Math.Max(2, radius - 2));
                g.FillPath(btnBrush, btnPath);
            }

            // 3. Responsive Icon & Text Layout
            bool isCompact = Width < 125 || !_showLeadingIcon;
            int textLeft = 9;
            int textRight = Width - 28;

            if (!isCompact)
            {
                // Draw Vector Calendar Icon on Left
                int iconX = 9;
                int iconY = (Height - 15) / 2;
                DrawVectorCalendarIcon(g, iconX, iconY, 15, 15, _isFocused || IsOpen);
                textLeft = 30;
            }

            // Draw Formatted Date Text
            int textWidth = Math.Max(0, textRight - textLeft);
            var textRect = new Rectangle(textLeft, 0, textWidth, Height);
            Color textClr = Enabled ? ForeColor : UiStyle.TextMuted;
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                textClr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);

            // Draw Right Indicator (Chevron or Compact Calendar Icon)
            if (isCompact && _showLeadingIcon && Width >= 100)
            {
                // In compact mode, show vector calendar icon in right button
                int iconX = btnRect.X + (btnRect.Width - 14) / 2;
                int iconY = (Height - 14) / 2;
                DrawVectorCalendarIcon(g, iconX, iconY, 14, 14, _isFocused || IsOpen);
            }
            else
            {
                // Draw sleek Chevron (flips up when open)
                DrawVectorChevron(g, btnRect, IsOpen, _isFocused || IsOpen ? AccentColor : UiStyle.TextMuted);
            }

            // 4. Border (Normal, Hover, or Glowing Focus)
            Color borderClr;
            float borderWidth = 1.2f;

            if (_isFocused || IsOpen)
            {
                borderClr = BorderFocusColor;
                borderWidth = 1.5f;
            }
            else if (_isHovered)
            {
                borderClr = BorderHoverColor;
                borderWidth = 1.2f;
            }
            else
            {
                borderClr = BorderColor;
            }

            using var borderPen = new Pen(borderClr, borderWidth);
            g.DrawPath(borderPen, path);
        }
    }

    private void DrawVectorCalendarIcon(Graphics g, int x, int y, int w, int h, bool isHighlighted)
    {
        // Smooth rounded calendar body
        var calRect = new Rectangle(x, y + 2, w, h - 2);
        using (var calPath = ModernCardPanel.CreateRoundedRectanglePath(calRect, 3))
        {
            using (var bodyBrush = new SolidBrush(UiStyle.CardBackground))
            {
                g.FillPath(bodyBrush, calPath);
            }

            Color outlineClr = isHighlighted ? AccentColor : Color.FromArgb(148, 163, 184);
            using (var bodyPen = new Pen(outlineClr, 1.1f))
            {
                g.DrawPath(bodyPen, calPath);
            }
        }

        // Accent top header bar
        using (var headerBrush = new SolidBrush(isHighlighted ? AccentColor : Color.FromArgb(99, 102, 241)))
        {
            var headerRect = new Rectangle(x + 1, y + 2, w - 2, 4);
            using var headerPath = ModernCardPanel.CreateRoundedRectanglePath(headerRect, 2);
            g.FillPath(headerBrush, headerPath);
        }

        // Binder rings
        Color ringClr = isHighlighted ? Color.White : Color.FromArgb(203, 213, 225);
        using (var ringPen = new Pen(ringClr, 1.4f))
        {
            g.DrawLine(ringPen, x + 3, y, x + 3, y + 3);
            g.DrawLine(ringPen, x + w - 4, y, x + w - 4, y + 3);
        }

        // Mini calendar grid dots
        Color dotClr = isHighlighted ? AccentColor : Color.FromArgb(148, 163, 184);
        using (var dotBrush = new SolidBrush(dotClr))
        {
            g.FillRectangle(dotBrush, x + 3, y + 8, 2, 2);
            g.FillRectangle(dotBrush, x + 6, y + 8, 2, 2);
            g.FillRectangle(dotBrush, x + 9, y + 8, 2, 2);
            g.FillRectangle(dotBrush, x + 3, y + 11, 2, 2);
            g.FillRectangle(dotBrush, x + 6, y + 11, 2, 2);
            g.FillRectangle(dotBrush, x + 9, y + 11, 2, 2);
        }
    }

    private static void DrawVectorChevron(Graphics g, Rectangle btnRect, bool isOpen, Color color)
    {
        int cx = btnRect.X + (btnRect.Width / 2);
        int cy = btnRect.Y + (btnRect.Height / 2);

        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (isOpen)
        {
            // Point UP
            g.DrawLine(pen, cx - 4, cy + 2, cx, cy - 2);
            g.DrawLine(pen, cx, cy - 2, cx + 4, cy + 2);
        }
        else
        {
            // Point DOWN
            g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
            g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
        }
    }
}

/// <summary>
/// Frameless modern popup container with rounded corners and drop shadow for calendar picker.
/// </summary>
public class ModernCalendarDropDown : ToolStripDropDown
{
    public ModernCalendarDropDown()
    {
        AutoClose = true;
        DropShadowEnabled = true;
        DoubleBuffered = true;
        Padding = Padding.Empty;
        Margin = Padding.Empty;
        BackColor = UiStyle.CardBackground;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 8);
        using var pen = new Pen(UiStyle.BorderColor, 1.5f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>
/// Sleek multi-view SaaS calendar control with Day Grid, Month Selector, Year Selector,
/// vibrant Indigo selection, today indicator, and quick shortcut actions.
/// </summary>
public class ModernCalendarView : Control
{
    public enum CalendarViewMode
    {
        DayGrid,
        MonthGrid,
        YearGrid
    }

    private readonly ModernDateTimePicker _picker;
    private DateTime _viewDate;
    private int _yearRangeStart;
    private CalendarViewMode _viewMode = CalendarViewMode.DayGrid;

    private int _hoverCell = -1;
    private bool _hoverPrevBtn;
    private bool _hoverNextBtn;
    private bool _hoverTitleBtn;
    private bool _hoverTodayBtn;
    private bool _hoverYesterdayBtn;
    private bool _hoverCloseBtn;

    private const int HeaderHeight = 42;
    private const int WeekdayHeight = 26;
    private const int CellHeight = 29;
    private const int FooterHeight = 40;

    public ModernCalendarView(ModernDateTimePicker picker)
    {
        _picker = picker;
        _viewDate = new DateTime(picker.Value.Year, picker.Value.Month, 1);
        _yearRangeStart = (picker.Value.Year / 12) * 12;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        Size = new Size(280, HeaderHeight + WeekdayHeight + (CellHeight * 6) + FooterHeight);
        BackColor = UiStyle.CardBackground;
        ForeColor = UiStyle.TextDark;
        Font = UiStyle.BaseFont;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int prevHoverCell = _hoverCell;
        bool prevP = _hoverPrevBtn, prevN = _hoverNextBtn, prevT = _hoverTitleBtn;
        bool prevToday = _hoverTodayBtn, prevYest = _hoverYesterdayBtn, prevClose = _hoverCloseBtn;

        // Header hit test
        _hoverPrevBtn = (e.Y < HeaderHeight && e.X >= 6 && e.X <= 36);
        _hoverNextBtn = (e.Y < HeaderHeight && e.X >= Width - 36 && e.X <= Width - 6);
        _hoverTitleBtn = (e.Y < HeaderHeight && e.X > 36 && e.X < Width - 36);

        // Footer hit test
        int footerY = Height - FooterHeight;
        if (e.Y >= footerY)
        {
            int btnW = (Width - 16) / 3;
            _hoverTodayBtn = (e.X >= 6 && e.X < 6 + btnW);
            _hoverYesterdayBtn = (e.X >= 6 + btnW && e.X < 6 + (btnW * 2));
            _hoverCloseBtn = (e.X >= 6 + (btnW * 2) && e.X <= Width - 6);
        }
        else
        {
            _hoverTodayBtn = false;
            _hoverYesterdayBtn = false;
            _hoverCloseBtn = false;
        }

        // Content Grid hit test
        if (_viewMode == CalendarViewMode.DayGrid)
        {
            int gridTop = HeaderHeight + WeekdayHeight;
            int gridBottom = gridTop + (CellHeight * 6);
            if (e.Y >= gridTop && e.Y < gridBottom && e.X >= 6 && e.X < Width - 6)
            {
                int colW = (Width - 12) / 7;
                int col = Math.Clamp((e.X - 6) / colW, 0, 6);
                int row = Math.Clamp((e.Y - gridTop) / CellHeight, 0, 5);
                _hoverCell = (row * 7) + col;
            }
            else
            {
                _hoverCell = -1;
            }
        }
        else // MonthGrid or YearGrid (4 rows x 3 cols)
        {
            int gridTop = HeaderHeight + 6;
            int gridBottom = footerY - 6;
            int gridH = gridBottom - gridTop;
            int rowH = gridH / 4;
            int colW = (Width - 16) / 3;

            if (e.Y >= gridTop && e.Y < gridBottom && e.X >= 8 && e.X < Width - 8)
            {
                int col = Math.Clamp((e.X - 8) / colW, 0, 2);
                int row = Math.Clamp((e.Y - gridTop) / rowH, 0, 3);
                _hoverCell = (row * 3) + col;
            }
            else
            {
                _hoverCell = -1;
            }
        }

        if (prevHoverCell != _hoverCell || prevP != _hoverPrevBtn || prevN != _hoverNextBtn ||
            prevT != _hoverTitleBtn || prevToday != _hoverTodayBtn || prevYest != _hoverYesterdayBtn ||
            prevClose != _hoverCloseBtn)
        {
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCell = -1;
        _hoverPrevBtn = false;
        _hoverNextBtn = false;
        _hoverTitleBtn = false;
        _hoverTodayBtn = false;
        _hoverYesterdayBtn = false;
        _hoverCloseBtn = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        // 1. Header Navigation
        if (e.Y < HeaderHeight)
        {
            if (_hoverPrevBtn)
            {
                if (_viewMode == CalendarViewMode.DayGrid)
                    _viewDate = _viewDate.AddMonths(-1);
                else if (_viewMode == CalendarViewMode.MonthGrid)
                    _viewDate = _viewDate.AddYears(-1);
                else
                    _yearRangeStart = Math.Max(1752, _yearRangeStart - 12);

                Invalidate();
                return;
            }

            if (_hoverNextBtn)
            {
                if (_viewMode == CalendarViewMode.DayGrid)
                    _viewDate = _viewDate.AddMonths(1);
                else if (_viewMode == CalendarViewMode.MonthGrid)
                    _viewDate = _viewDate.AddYears(1);
                else
                    _yearRangeStart = Math.Min(9984, _yearRangeStart + 12);

                Invalidate();
                return;
            }

            if (_hoverTitleBtn)
            {
                // Toggle view modes: DayGrid -> MonthGrid -> YearGrid -> DayGrid
                if (_viewMode == CalendarViewMode.DayGrid)
                {
                    _viewMode = CalendarViewMode.MonthGrid;
                }
                else if (_viewMode == CalendarViewMode.MonthGrid)
                {
                    _viewMode = CalendarViewMode.YearGrid;
                    _yearRangeStart = (_viewDate.Year / 12) * 12;
                }
                else
                {
                    _viewMode = CalendarViewMode.DayGrid;
                }
                Invalidate();
                return;
            }
        }

        // 2. Footer Shortcuts
        int footerY = Height - FooterHeight;
        if (e.Y >= footerY)
        {
            if (_hoverTodayBtn)
            {
                _picker.Value = DateTime.Today;
                _picker.CloseDropDown();
                return;
            }
            if (_hoverYesterdayBtn)
            {
                _picker.Value = DateTime.Today.AddDays(-1);
                _picker.CloseDropDown();
                return;
            }
            if (_hoverCloseBtn)
            {
                _picker.CloseDropDown();
                return;
            }
        }

        // 3. Content Grid Selection
        if (_viewMode == CalendarViewMode.DayGrid)
        {
            int gridTop = HeaderHeight + WeekdayHeight;
            int gridBottom = gridTop + (CellHeight * 6);
            if (e.Y >= gridTop && e.Y < gridBottom && e.X >= 6 && e.X < Width - 6)
            {
                int colW = (Width - 12) / 7;
                int col = Math.Clamp((e.X - 6) / colW, 0, 6);
                int row = Math.Clamp((e.Y - gridTop) / CellHeight, 0, 5);
                int cellIdx = (row * 7) + col;

                var startDate = GetGridStartDate();
                var clickedDate = startDate.AddDays(cellIdx);

                if (clickedDate >= _picker.MinDate && clickedDate <= _picker.MaxDate)
                {
                    _picker.Value = clickedDate;
                    _picker.CloseDropDown();
                }
            }
        }
        else if (_viewMode == CalendarViewMode.MonthGrid)
        {
            int gridTop = HeaderHeight + 6;
            int gridBottom = footerY - 6;
            int gridH = gridBottom - gridTop;
            int rowH = gridH / 4;
            int colW = (Width - 16) / 3;

            if (e.Y >= gridTop && e.Y < gridBottom && e.X >= 8 && e.X < Width - 8)
            {
                int col = Math.Clamp((e.X - 8) / colW, 0, 2);
                int row = Math.Clamp((e.Y - gridTop) / rowH, 0, 3);
                int monthIdx = (row * 3) + col + 1; // 1 to 12

                _viewDate = new DateTime(_viewDate.Year, monthIdx, 1);
                _viewMode = CalendarViewMode.DayGrid;
                Invalidate();
            }
        }
        else if (_viewMode == CalendarViewMode.YearGrid)
        {
            int gridTop = HeaderHeight + 6;
            int gridBottom = footerY - 6;
            int gridH = gridBottom - gridTop;
            int rowH = gridH / 4;
            int colW = (Width - 16) / 3;

            if (e.Y >= gridTop && e.Y < gridBottom && e.X >= 8 && e.X < Width - 8)
            {
                int col = Math.Clamp((e.X - 8) / colW, 0, 2);
                int row = Math.Clamp((e.Y - gridTop) / rowH, 0, 3);
                int selectedYear = _yearRangeStart + (row * 3) + col;

                selectedYear = Math.Clamp(selectedYear, 1753, 9998);
                _viewDate = new DateTime(selectedYear, _viewDate.Month, 1);
                _viewMode = CalendarViewMode.MonthGrid;
                Invalidate();
            }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int direction = e.Delta > 0 ? -1 : 1;

        if (_viewMode == CalendarViewMode.DayGrid)
            _viewDate = _viewDate.AddMonths(direction);
        else if (_viewMode == CalendarViewMode.MonthGrid)
            _viewDate = _viewDate.AddYears(direction);
        else
            _yearRangeStart = Math.Clamp(_yearRangeStart + (direction * 12), 1752, 9984);

        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            _picker.CloseDropDown();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Left)
        {
            _picker.Value = _picker.Value.AddDays(-1);
            _viewDate = new DateTime(_picker.Value.Year, _picker.Value.Month, 1);
            Invalidate();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            _picker.Value = _picker.Value.AddDays(1);
            _viewDate = new DateTime(_picker.Value.Year, _picker.Value.Month, 1);
            Invalidate();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            _picker.Value = _picker.Value.AddDays(-7);
            _viewDate = new DateTime(_picker.Value.Year, _picker.Value.Month, 1);
            Invalidate();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            _picker.Value = _picker.Value.AddDays(7);
            _viewDate = new DateTime(_picker.Value.Year, _picker.Value.Month, 1);
            Invalidate();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
        {
            _picker.CloseDropDown();
            e.Handled = true;
        }
    }

    private DateTime GetGridStartDate()
    {
        var firstOfMonth = new DateTime(_viewDate.Year, _viewDate.Month, 1);
        var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int diff = ((int)firstOfMonth.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return firstOfMonth.AddDays(-diff);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 1. Background Fill
        using (var bgBrush = new SolidBrush(BackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // 2. Header
        DrawHeader(g);

        // 3. Body View
        if (_viewMode == CalendarViewMode.DayGrid)
        {
            DrawWeekdayHeaders(g);
            DrawDayGrid(g);
        }
        else if (_viewMode == CalendarViewMode.MonthGrid)
        {
            DrawMonthGrid(g);
        }
        else if (_viewMode == CalendarViewMode.YearGrid)
        {
            DrawYearGrid(g);
        }

        // 4. Footer
        DrawFooter(g);
    }

    private void DrawHeader(Graphics g)
    {
        // Prev Button
        var prevRect = new Rectangle(6, 6, 30, 30);
        if (_hoverPrevBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(prevRect, 6);
            g.FillPath(hBrush, hPath);
        }
        DrawVectorArrow(g, prevRect, isLeft: true);

        // Next Button
        var nextRect = new Rectangle(Width - 36, 6, 30, 30);
        if (_hoverNextBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(nextRect, 6);
            g.FillPath(hBrush, hPath);
        }
        DrawVectorArrow(g, nextRect, isLeft: false);

        // Center Title Button
        var titleRect = new Rectangle(38, 6, Width - 76, 30);
        if (_hoverTitleBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(titleRect, 6);
            g.FillPath(hBrush, hPath);
        }

        string titleText = _viewMode switch
        {
            CalendarViewMode.DayGrid => $"{_viewDate.ToString("MMMM yyyy", CultureInfo.CurrentCulture)} ▾",
            CalendarViewMode.MonthGrid => $"{_viewDate:yyyy} ▾",
            _ => $"{_yearRangeStart} – {_yearRangeStart + 11} ▴"
        };

        using var titleFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        TextRenderer.DrawText(
            g,
            titleText,
            titleFont,
            titleRect,
            UiStyle.TextDark,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Header separator line
        using var sepPen = new Pen(UiStyle.BorderColor, 1f);
        g.DrawLine(sepPen, 6, HeaderHeight, Width - 6, HeaderHeight);
    }

    private static void DrawVectorArrow(Graphics g, Rectangle rect, bool isLeft)
    {
        int cx = rect.X + (rect.Width / 2);
        int cy = rect.Y + (rect.Height / 2);

        using var pen = new Pen(UiStyle.TextDark, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (isLeft)
        {
            g.DrawLine(pen, cx + 2, cy - 5, cx - 3, cy);
            g.DrawLine(pen, cx - 3, cy, cx + 2, cy + 5);
        }
        else
        {
            g.DrawLine(pen, cx - 2, cy - 5, cx + 3, cy);
            g.DrawLine(pen, cx + 3, cy, cx - 2, cy + 5);
        }
    }

    private void DrawWeekdayHeaders(Graphics g)
    {
        int colW = (Width - 12) / 7;
        var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        using var dayHeadFont = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);

        for (int c = 0; c < 7; c++)
        {
            var dayOfWeek = (DayOfWeek)(((int)firstDayOfWeek + c) % 7);
            string dayName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(dayOfWeek);
            if (dayName.Length > 3) dayName = dayName[..3];

            bool isWeekend = (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday);
            Color clr = isWeekend
                ? (UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? Color.FromArgb(165, 180, 252) : Color.FromArgb(79, 70, 229))
                : UiStyle.TextMuted;

            var colRect = new Rectangle(6 + (c * colW), HeaderHeight, colW, WeekdayHeight);
            TextRenderer.DrawText(
                g,
                dayName,
                dayHeadFont,
                colRect,
                clr,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawDayGrid(Graphics g)
    {
        int colW = (Width - 12) / 7;
        var gridStart = GetGridStartDate();
        int gridTop = HeaderHeight + WeekdayHeight;

        using var dayFont = new Font("Segoe UI", 9F);
        using var boldDayFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        for (int i = 0; i < 42; i++)
        {
            int row = i / 7;
            int col = i % 7;
            var cellRect = new Rectangle(6 + (col * colW), gridTop + (row * CellHeight), colW, CellHeight);
            var date = gridStart.AddDays(i);

            bool isCurrentMonth = (date.Month == _viewDate.Month);
            bool isSelected = (date.Date == _picker.Value.Date);
            bool isToday = (date.Date == DateTime.Today);
            bool isHovered = (i == _hoverCell);
            bool isOutOfRange = (date < _picker.MinDate || date > _picker.MaxDate);

            var pillRect = new Rectangle(cellRect.X + 2, cellRect.Y + 1, cellRect.Width - 4, cellRect.Height - 2);

            // Draw Pill Backgrounds
            if (isSelected && !isOutOfRange)
            {
                using var selBrush = new SolidBrush(_picker.AccentColor);
                using var selPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(selBrush, selPath);
            }
            else if (isHovered && !isOutOfRange)
            {
                using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
                using var hPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(hBrush, hPath);
            }

            // Draw Today Outline and Dot indicator
            if (isToday && !isSelected && !isOutOfRange)
            {
                using var todayPen = new Pen(_picker.AccentColor, 1.4f);
                using var todayPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.DrawPath(todayPen, todayPath);

                // Small today dot below day number
                using var dotBrush = new SolidBrush(_picker.AccentColor);
                int dotX = cellRect.X + (cellRect.Width / 2) - 1;
                int dotY = cellRect.Bottom - 4;
                g.FillEllipse(dotBrush, dotX, dotY, 3, 3);
            }

            // Text Color
            Color dayClr;
            if (isOutOfRange)
            {
                dayClr = UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            }
            else if (isSelected)
            {
                dayClr = Color.White;
            }
            else if (isToday)
            {
                dayClr = UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? Color.FromArgb(129, 140, 248) : Color.FromArgb(79, 70, 229);
            }
            else if (isCurrentMonth)
            {
                dayClr = UiStyle.TextDark;
            }
            else
            {
                dayClr = UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184);
            }

            Font useFont = (isSelected || isToday) ? boldDayFont : dayFont;
            TextRenderer.DrawText(
                g,
                date.Day.ToString(),
                useFont,
                cellRect,
                dayClr,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawMonthGrid(Graphics g)
    {
        int footerY = Height - FooterHeight;
        int gridTop = HeaderHeight + 6;
        int gridH = footerY - 6 - gridTop;
        int rowH = gridH / 4;
        int colW = (Width - 16) / 3;

        using var font = new Font("Segoe UI", 9F);
        using var boldFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        for (int m = 1; m <= 12; m++)
        {
            int idx = m - 1;
            int row = idx / 3;
            int col = idx % 3;

            var cellRect = new Rectangle(8 + (col * colW), gridTop + (row * rowH), colW, rowH);
            var pillRect = new Rectangle(cellRect.X + 3, cellRect.Y + 3, cellRect.Width - 6, cellRect.Height - 6);

            bool isSelected = (_picker.Value.Year == _viewDate.Year && _picker.Value.Month == m);
            bool isCurrentMonth = (DateTime.Today.Year == _viewDate.Year && DateTime.Today.Month == m);
            bool isHovered = (idx == _hoverCell);

            if (isSelected)
            {
                using var selBrush = new SolidBrush(_picker.AccentColor);
                using var selPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(selBrush, selPath);
            }
            else if (isHovered)
            {
                using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
                using var hPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(hBrush, hPath);
            }

            if (isCurrentMonth && !isSelected)
            {
                using var outlinePen = new Pen(_picker.AccentColor, 1.4f);
                using var outlinePath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.DrawPath(outlinePen, outlinePath);
            }

            string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m);
            Color textClr = isSelected ? Color.White : (isCurrentMonth ? _picker.AccentColor : UiStyle.TextDark);
            Font useFont = (isSelected || isCurrentMonth) ? boldFont : font;

            TextRenderer.DrawText(
                g,
                monthName,
                useFont,
                pillRect,
                textClr,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawYearGrid(Graphics g)
    {
        int footerY = Height - FooterHeight;
        int gridTop = HeaderHeight + 6;
        int gridH = footerY - 6 - gridTop;
        int rowH = gridH / 4;
        int colW = (Width - 16) / 3;

        using var font = new Font("Segoe UI", 9F);
        using var boldFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        for (int y = 0; y < 12; y++)
        {
            int year = _yearRangeStart + y;
            int row = y / 3;
            int col = y % 3;

            var cellRect = new Rectangle(8 + (col * colW), gridTop + (row * rowH), colW, rowH);
            var pillRect = new Rectangle(cellRect.X + 3, cellRect.Y + 3, cellRect.Width - 6, cellRect.Height - 6);

            bool isSelected = (_picker.Value.Year == year);
            bool isCurrentYear = (DateTime.Today.Year == year);
            bool isHovered = (y == _hoverCell);

            if (isSelected)
            {
                using var selBrush = new SolidBrush(_picker.AccentColor);
                using var selPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(selBrush, selPath);
            }
            else if (isHovered)
            {
                using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
                using var hPath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.FillPath(hBrush, hPath);
            }

            if (isCurrentYear && !isSelected)
            {
                using var outlinePen = new Pen(_picker.AccentColor, 1.4f);
                using var outlinePath = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 6);
                g.DrawPath(outlinePen, outlinePath);
            }

            Color textClr = isSelected ? Color.White : (isCurrentYear ? _picker.AccentColor : UiStyle.TextDark);
            Font useFont = (isSelected || isCurrentYear) ? boldFont : font;

            TextRenderer.DrawText(
                g,
                year.ToString(),
                useFont,
                pillRect,
                textClr,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawFooter(Graphics g)
    {
        int footerY = Height - FooterHeight;

        // Separator line
        using (var sepPen = new Pen(UiStyle.BorderColor, 1f))
        {
            g.DrawLine(sepPen, 6, footerY, Width - 6, footerY);
        }

        int btnW = (Width - 16) / 3;
        using var footerFont = new Font("Segoe UI Semibold", 8.5F);

        // 1. Bugün Button
        var todayRect = new Rectangle(6, footerY + 5, btnW, FooterHeight - 10);
        if (_hoverTodayBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(todayRect, 5);
            g.FillPath(hBrush, hPath);
        }
        TextRenderer.DrawText(
            g,
            "📅 Bugün",
            footerFont,
            todayRect,
            _picker.AccentColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // 2. Dün Button
        var yesterdayRect = new Rectangle(6 + btnW, footerY + 5, btnW, FooterHeight - 10);
        if (_hoverYesterdayBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(yesterdayRect, 5);
            g.FillPath(hBrush, hPath);
        }
        TextRenderer.DrawText(
            g,
            "⚡ Dün",
            footerFont,
            yesterdayRect,
            UiStyle.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // 3. Kapat Button
        var closeRect = new Rectangle(6 + (btnW * 2), footerY + 5, btnW, FooterHeight - 10);
        if (_hoverCloseBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(closeRect, 5);
            g.FillPath(hBrush, hPath);
        }
        TextRenderer.DrawText(
            g,
            "✕ Kapat",
            footerFont,
            closeRect,
            UiStyle.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}


