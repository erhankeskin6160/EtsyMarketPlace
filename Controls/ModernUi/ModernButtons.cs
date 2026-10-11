namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return base.GetPreferredSize(proposedSize);
        }

        var textSize = TextRenderer.MeasureText(
            Text,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        int padX = Padding.Horizontal > 0 ? Padding.Horizontal : 22;
        int padY = Padding.Vertical > 0 ? Padding.Vertical : 8;
        int width = textSize.Width + padX;
        int height = Height > 0 ? Height : (textSize.Height + padY);

        return new Size(width, height);
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
            if (p is ModernCardPanel cardPanel)
            {
                return cardPanel.CardColor;
            }
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.CardBackground;
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
