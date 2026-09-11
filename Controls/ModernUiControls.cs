namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
