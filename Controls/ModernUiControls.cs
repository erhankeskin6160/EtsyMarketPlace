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
/// Modern dark-theme ComboBox with custom-painted sleek dark dropdown button, chevrons, and styled popup list.
/// </summary>
public class ModernComboBox : ComboBox
{
    private const int WM_PAINT = 0x000F;
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
        ItemHeight = 24;
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

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        bool isClosedArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;

        Color bg = isSelected ? UiStyle.PrimaryColor : UiStyle.InputBackground;
        Color fg = isSelected ? Color.White : UiStyle.TextDark;

        using (var bgBrush = new SolidBrush(bg))
        {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        string text = GetItemText(Items[e.Index]) ?? string.Empty;
        int btnWidth = 26;
        var textRect = new Rectangle(
            e.Bounds.X + 8,
            e.Bounds.Y,
            Math.Max(0, e.Bounds.Width - (isClosedArea ? btnWidth + 6 : 16)),
            e.Bounds.Height);

        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            textRect,
            fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && DropDownStyle != ComboBoxStyle.Simple)
        {
            using var g = Graphics.FromHwnd(Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int btnWidth = 26;
            var btnRect = new Rectangle(Width - btnWidth, 1, btnWidth - 1, Height - 2);
            Color btnBg = _isHovered ? UiStyle.SecondaryHover : UiStyle.SecondaryColor;

            using (var brush = new SolidBrush(btnBg))
            {
                g.FillRectangle(brush, btnRect);
            }

            using (var sepPen = new Pen(UiStyle.BorderColor, 1f))
            {
                g.DrawLine(sepPen, btnRect.X, 1, btnRect.X, Height - 2);
            }

            int centerX = btnRect.X + (btnRect.Width / 2);
            int centerY = btnRect.Y + (btnRect.Height / 2);
            Color arrowColor = _isHovered ? Color.White : UiStyle.TextMuted;

            using (var arrowPen = new Pen(arrowColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(arrowPen, centerX - 4, centerY - 2, centerX, centerY + 2);
                g.DrawLine(arrowPen, centerX, centerY + 2, centerX + 4, centerY - 2);
            }

            Color borderColor = (_isHovered || DroppedDown) ? UiStyle.PrimaryColor : UiStyle.BorderColor;
            using (var borderPen = new Pen(borderColor, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}

/// <summary>
/// Modern dark-theme NumericUpDown with custom-painted dark spinner buttons, sleek chevrons, and themed border.
/// </summary>
public class ModernNumericUpDown : NumericUpDown
{
    private readonly UpDownButtonsPainter _buttonsPainter;

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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawOuterBorder(e.Graphics);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x000F) // WM_PAINT
        {
            using var g = Graphics.FromHwnd(Handle);
            DrawOuterBorder(g);
        }
    }

    private void DrawOuterBorder(Graphics g)
    {
        Color borderColor = Focused ? UiStyle.PrimaryColor : UiStyle.BorderColor;
        using var borderPen = new Pen(borderColor, 1f);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private sealed class UpDownButtonsPainter : NativeWindow
    {
        private readonly ModernNumericUpDown _owner;
        private Control? _buttonsControl;
        private bool _upHover;
        private bool _downHover;
        private bool _upPressed;
        private bool _downPressed;

        public UpDownButtonsPainter(ModernNumericUpDown owner)
        {
            _owner = owner;
            _owner.HandleCreated += (_, _) => Attach();
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
                _buttonsControl.Paint += (_, e) => DrawButtons(e.Graphics);
                _buttonsControl.Resize += (_, _) => _buttonsControl.Invalidate();
            }

            if (_owner.Controls.Count > 1)
            {
                var editBox = _owner.Controls[1];
                editBox.BackColor = UiStyle.InputBackground;
                editBox.ForeColor = UiStyle.TextDark;
                editBox.Enter += (_, _) => _owner.Invalidate();
                editBox.Leave += (_, _) => _owner.Invalidate();
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (_buttonsControl == null || !_buttonsControl.IsHandleCreated) return;

            switch (m.Msg)
            {
                case 0x000F: // WM_PAINT
                    using (var g = Graphics.FromHwnd(_buttonsControl.Handle))
                    {
                        DrawButtons(g);
                    }
                    break;

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
                    }
                    break;

                case 0x02A3: // WM_MOUSELEAVE
                    if (_upHover || _downHover)
                    {
                        _upHover = false;
                        _downHover = false;
                        _buttonsControl.Invalidate();
                    }
                    break;

                case 0x0201: // WM_LBUTTONDOWN
                    int yDown = (int)(m.LParam.ToInt64() >> 16) & 0xFFFF;
                    int halfDown = _buttonsControl.Height / 2;
                    if (yDown < halfDown) _upPressed = true; else _downPressed = true;
                    _buttonsControl.Invalidate();
                    break;

                case 0x0202: // WM_LBUTTONUP
                    _upPressed = false;
                    _downPressed = false;
                    _buttonsControl.Invalidate();
                    break;
            }
        }

        private void DrawButtons(Graphics g)
        {
            if (_buttonsControl == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = _buttonsControl.Width;
            int h = _buttonsControl.Height;
            if (w <= 0 || h <= 0) return;

            int half = h / 2;

            // 1. Up Button
            var upRect = new Rectangle(0, 0, w, half);
            Color upColor = _upPressed ? UiStyle.PrimaryColor : (_upHover ? UiStyle.SecondaryHover : UiStyle.SecondaryColor);
            using (var brush = new SolidBrush(upColor))
            {
                g.FillRectangle(brush, upRect);
            }

            int cx = w / 2;
            int cyUp = half / 2;
            Color upArrowColor = (_upHover || _upPressed) ? Color.White : UiStyle.TextMuted;
            using (var pen = new Pen(upArrowColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx - 3, cyUp + 1, cx, cyUp - 2);
                g.DrawLine(pen, cx, cyUp - 2, cx + 3, cyUp + 1);
            }

            // 2. Down Button
            var downRect = new Rectangle(0, half, w, h - half);
            Color downColor = _downPressed ? UiStyle.PrimaryColor : (_downHover ? UiStyle.SecondaryHover : UiStyle.SecondaryColor);
            using (var brush = new SolidBrush(downColor))
            {
                g.FillRectangle(brush, downRect);
            }

            int cyDown = half + ((h - half) / 2);
            Color downArrowColor = (_downHover || _downPressed) ? Color.White : UiStyle.TextMuted;
            using (var pen = new Pen(downArrowColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx - 3, cyDown - 2, cx, cyDown + 1);
                g.DrawLine(pen, cx, cyDown + 1, cx + 3, cyDown - 2);
            }

            // 3. Dividers
            using (var divPen = new Pen(UiStyle.BorderColor, 1f))
            {
                g.DrawLine(divPen, 0, half, w, half);
                g.DrawLine(divPen, 0, 0, 0, h);
            }
        }
    }
}
