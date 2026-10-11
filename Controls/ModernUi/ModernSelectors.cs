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
/// Modern dark-theme TabControl with pill-style tab headers, smooth antialiasing, and zero white borders.
/// </summary>
public class ModernTabControl : TabControl
{
    public Color HeaderBackgroundColor { get; set; } = Color.FromArgb(15, 23, 42); // Slate 950 #0F172A
    public Color ActiveTabColor { get; set; } = Color.FromArgb(99, 102, 241); // Indigo 500
    public Color InactiveTabColor { get; set; } = Color.FromArgb(30, 41, 59); // Slate 800 #1E293B
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
        ItemSize = new Size(0, 40);
        Padding = new Point(22, 8);
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
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
        Cursor = _hoveredIndex != -1 ? Cursors.Hand : Cursors.Default;
        if (oldHover != _hoveredIndex) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Cursor = Cursors.Default;
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
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int headerHeight = TabCount > 0 ? GetTabRect(0).Bottom + 2 : 40;

        // 1. Fill entire control background with HeaderBackgroundColor to eliminate any white flicker
        using (var bgBrush = new SolidBrush(HeaderBackgroundColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // 2. Draw modern pill tabs
        for (int i = 0; i < TabCount; i++)
        {
            var tabRect = GetTabRect(i);
            var isSelected = (SelectedIndex == i);
            var isHovered = (_hoveredIndex == i && !isSelected);

            // Tab pill bounding box
            var pillRect = new Rectangle(tabRect.X + 2, tabRect.Y + 4, tabRect.Width - 4, tabRect.Height - 8);
            if (pillRect.Width <= 0 || pillRect.Height <= 0) continue;

            using var path = ModernCardPanel.CreateRoundedRectanglePath(pillRect, 8);

            if (isSelected)
            {
                // Active pill with rich Indigo vertical gradient and subtle sheen
                using var selBrush = new LinearGradientBrush(
                    pillRect,
                    ActiveTabColor,
                    Color.FromArgb(79, 70, 229), // Indigo 600
                    LinearGradientMode.Vertical);
                g.FillPath(selBrush, path);

                // Subtle active glowing border
                using var glowPen = new Pen(Color.FromArgb(165, 180, 252), 1.2f);
                g.DrawPath(glowPen, path);
            }
            else if (isHovered)
            {
                // Hovered inactive pill
                using var hoverBrush = new SolidBrush(UiStyle.CardHoverBackground);
                g.FillPath(hoverBrush, path);

                using var hoverPen = new Pen(Color.FromArgb(99, 102, 241), 1f);
                g.DrawPath(hoverPen, path);
            }
            else
            {
                // Inactive normal pill
                using var inactBrush = new SolidBrush(InactiveTabColor);
                g.FillPath(inactBrush, path);

                using var borderPen = new Pen(BorderColor, 1f);
                g.DrawPath(borderPen, path);
            }

            // Draw formatted text with ClearType
            Color fg = isSelected ? ActiveTextColor : (isHovered ? UiStyle.TextDark : InactiveTextColor);
            var tabText = TabPages[i].Text;
            TextRenderer.DrawText(
                g,
                tabText,
                Font,
                pillRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
        }

        // 3. Subtle dividing line along bottom of tab header strip
        using (var sepPen = new Pen(BorderColor, 1f))
        {
            g.DrawLine(sepPen, 0, headerHeight - 1, Width, headerHeight - 1);
        }

        // 4. Content area background
        if (SelectedTab != null)
        {
            var contentRect = new Rectangle(0, headerHeight, Width, Math.Max(0, Height - headerHeight));
            using var contentBrush = new SolidBrush(SelectedTab.BackColor);
            g.FillRectangle(contentBrush, contentRect);
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
        EnabledChanged += (_, _) => Invalidate();
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

        if (!Enabled)
        {
            // Devre dÄ±ÅŸÄ± (Disabled) durumunda Windows'un native beyaz/aÃ§Ä±k gri Ã§erÃ§evesini tamamen Ã¶rtÃ¼p modern slate temaya alalÄ±m:
            Color disabledBg = Color.FromArgb(20, 27, 45); // Koyu modern sÃ¶nÃ¼k arka plan
            Color disabledBorder = Color.FromArgb(45, 55, 75); // Asla beyaz olmayan zarif koyu slate bordÃ¼r
            Color disabledText = Color.FromArgb(100, 116, 139); // Slate 500 (Okunabilir ancak inaktif)
            Color disabledArrow = Color.FromArgb(71, 85, 105); // Slate 600

            using (var brush = new SolidBrush(disabledBg))
            {
                g.FillRectangle(brush, 0, 0, Width, Height);
            }

            string text = SelectedItem?.ToString() ?? Text;
            var textRect = new Rectangle(8, 0, Math.Max(0, Width - btnWidth - 10), Height);
            TextRenderer.DrawText(
                g,
                text,
                Font,
                textRect,
                disabledText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using (var sepPen = new Pen(disabledBorder, 1f))
            {
                g.DrawLine(sepPen, Width - btnWidth, 2, Width - btnWidth, Height - 3);
            }

            int disCenterX = Width - (btnWidth / 2);
            int disCenterY = Height / 2;
            using (var arrowPen = new Pen(disabledArrow, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                using var path = new GraphicsPath();
                path.AddLine(disCenterX - 4, disCenterY - 2, disCenterX, disCenterY + 2);
                path.AddLine(disCenterX, disCenterY + 2, disCenterX + 4, disCenterY - 2);
                g.DrawPath(arrowPen, path);
            }

            using (var borderPen = new Pen(disabledBorder, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
            return;
        }

        var btnRect = new Rectangle(Width - btnWidth, 1, btnWidth - 1, Height - 2);
        bool isActive = _isHovered || DroppedDown;
        Color btnBg = isActive ? UiStyle.SecondaryHover : UiStyle.SecondaryColor;

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
        Color arrowColor = isActive ? Color.White : UiStyle.TextMuted;

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

        Color borderColor = (Focused || _isHovered || DroppedDown) ? UiStyle.PrimaryColor : UiStyle.BorderColor;
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

        _btnMinus.Text = "âˆ’";
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
