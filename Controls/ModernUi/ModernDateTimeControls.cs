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
            CalendarViewMode.DayGrid => $"{_viewDate.ToString("MMMM yyyy", CultureInfo.CurrentCulture)} â–¾",
            CalendarViewMode.MonthGrid => $"{_viewDate:yyyy} â–¾",
            _ => $"{_yearRangeStart} â€“ {_yearRangeStart + 11} â–´"
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

        // 1. BugÃ¼n Button
        var todayRect = new Rectangle(6, footerY + 5, btnW, FooterHeight - 10);
        if (_hoverTodayBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(todayRect, 5);
            g.FillPath(hBrush, hPath);
        }
        TextRenderer.DrawText(
            g,
            "ğŸ“… BugÃ¼n",
            footerFont,
            todayRect,
            _picker.AccentColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // 2. DÃ¼n Button
        var yesterdayRect = new Rectangle(6 + btnW, footerY + 5, btnW, FooterHeight - 10);
        if (_hoverYesterdayBtn)
        {
            using var hBrush = new SolidBrush(UiStyle.CardHoverBackground);
            using var hPath = ModernCardPanel.CreateRoundedRectanglePath(yesterdayRect, 5);
            g.FillPath(hBrush, hPath);
        }
        TextRenderer.DrawText(
            g,
            "âš¡ DÃ¼n",
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
            "âœ• Kapat",
            footerFont,
            closeRect,
            UiStyle.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}


