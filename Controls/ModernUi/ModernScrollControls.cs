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
        catch (Exception caught) { AppLog.Swallowed(caught); }
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
        catch (Exception caught) { AppLog.Swallowed(caught); }
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
        catch (Exception caught) { AppLog.Swallowed(caught); }
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
        catch (Exception caught) { AppLog.Swallowed(caught); }
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
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }
}

/// <summary>
/// Smooth scrollable container with sleek ModernVScrollBar and ModernHScrollBar, zero native white scrollbars.
/// Uses a double-buffered viewport clipping panel with direct coordinate translation
/// to guarantee zero visual tearing, zero ghosting, and instant smooth scrolling.
/// </summary>
public class ModernScrollPanel : Panel, IMessageFilter
{
    private sealed class ModernScrollViewport : Panel
    {
        public ModernScrollViewport()
        {
            AutoScroll = false;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint,
                true);
            SetStyle(ControlStyles.UserPaint, false);
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = UiStyle.CardBackground;
        }
    }

    private readonly ModernScrollViewport _viewport = new() { Location = new Point(0, 0) };
    private readonly ModernVScrollBar _scrollBar = new() { Width = 8, Visible = false };
    private readonly ModernHScrollBar _hScrollBar = new() { Height = 8, Visible = false };
    private readonly Panel _corner = new() { Size = new Size(8, 8), Visible = false, BackColor = UiStyle.CardBackground };
    private Control? _content;
    private bool _isFilterRegistered;
    private bool _isSyncing;
    private bool _isUpdating;

    public Panel Viewport => _viewport;
    public ModernVScrollBar ScrollBar => _scrollBar;
    public ModernHScrollBar HScrollBar => _hScrollBar;
    public bool HorizontalScrollEnabled { get; set; } = false;

    private int _scrollBarGap = 8;
    /// <summary>
    /// Ä°Ã§erik/tablo ile kaydÄ±rma Ã§ubuÄŸu (scroll bar) arasÄ±ndaki nefes payÄ± (piksel).
    /// </summary>
    public int ScrollBarGap
    {
        get => _scrollBarGap;
        set
        {
            if (_scrollBarGap != value)
            {
                _scrollBarGap = Math.Max(0, value);
                if (!_isUpdating) RecalculateScroll();
            }
        }
    }


    public ModernScrollPanel()
    {
        SuspendLayout();
        AutoScroll = false;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint,
            true);
        SetStyle(ControlStyles.UserPaint, false);
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _scrollBar.ValueChanged += (_, _) =>
        {
            ApplyScrollPosition();
        };

        _hScrollBar.ValueChanged += (_, _) =>
        {
            ApplyScrollPosition();
        };

        Controls.Add(_viewport);
        Controls.Add(_scrollBar);
        Controls.Add(_hScrollBar);
        Controls.Add(_corner);
        _scrollBar.BringToFront();
        _hScrollBar.BringToFront();
        _corner.BringToFront();

        try
        {
            System.Windows.Forms.Application.AddMessageFilter(this);
            _isFilterRegistered = true;
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }

        ResumeLayout(false);
    }

    public override Color BackColor
    {
        get => GetEffectiveParentBackColor();
        set
        {
            if (value != Color.Transparent && value != Color.Empty)
            {
                _viewport.BackColor = value;
                if (_content != null) _content.BackColor = value;
            }
        }
    }

    public Color GetEffectiveParentBackColor()
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

    private void ApplyScrollPosition()
    {
        if (_content == null || _isSyncing) return;
        _isSyncing = true;
        try
        {
            var newLoc = new Point(-_hScrollBar.Value, -_scrollBar.Value);
            if (_content.Location != newLoc)
            {
                _content.Location = newLoc;
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
        finally
        {
            _isSyncing = false;
        }
    }

    public void SyncScrollBarFromViewport()
    {
        ApplyScrollPosition();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        try
        {
            if (!_isUpdating) UpdateLayout();
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        try
        {
            if (!_isUpdating) UpdateLayout();
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }

    private void UpdateLayout()
    {
        if (_isUpdating || IsDisposed) return;
        RecalculateScroll();
    }

    public void SetContent(Control content)
    {
        try
        {
            if (_viewport == null || _scrollBar == null || _hScrollBar == null) return;

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
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }

    public void RecalculateScroll()
    {
        if (_isUpdating || IsDisposed) return;
        _isUpdating = true;
        try
        {
            if (_content == null || _viewport == null || _scrollBar == null || _hScrollBar == null || ClientSize.Height <= 0 || ClientSize.Width <= 0) return;

            Color effectiveBg = GetEffectiveParentBackColor();
            if (_viewport.BackColor != effectiveBg) _viewport.BackColor = effectiveBg;
            if (_content.BackColor == Color.Transparent || _content.BackColor == SystemColors.Control)
            {
                _content.BackColor = effectiveBg;
            }

            int scrollBarW = _scrollBar.Width > 0 ? _scrollBar.Width : 8;
            int scrollBarH = _hScrollBar.Height > 0 ? _hScrollBar.Height : 8;

            int availW = ClientSize.Width;
            int availH = ClientSize.Height;

            int padLeft = Padding.Left;
            int padTop = Padding.Top;
            int padRight = Padding.Right;
            int padBottom = Padding.Bottom;

            // Content preferred measurement
            int contentW = _content.PreferredSize.Width;
            int contentH = _content.PreferredSize.Height;
            foreach (Control c in _content.Controls)
            {
                if (c.Visible)
                {
                    int right = c.Right + c.Margin.Right;
                    if (right > contentW) contentW = right;
                    int bottom = c.Bottom + c.Margin.Bottom;
                    if (bottom > contentH) contentH = bottom;
                }
            }

            int gapV = Math.Max(_scrollBarGap, padRight);
            int gapH = Math.Max(_scrollBarGap, padBottom);

            int testW = HorizontalScrollEnabled ? contentW : Math.Max(100, availW - padLeft - scrollBarW - gapV);
            int prefH = _content.GetPreferredSize(new Size(testW, 0)).Height;
            if (prefH > contentH) contentH = prefH;

            int usableH = Math.Max(0, availH - padTop - padBottom);

            bool needVBar = contentH > usableH;
            int vBarSpace = needVBar ? (scrollBarW + gapV) : padRight;
            int visibleContentW = Math.Max(0, availW - padLeft - vBarSpace);

            bool needHBar = HorizontalScrollEnabled && (contentW > visibleContentW);
            int hBarSpace = needHBar ? (scrollBarH + gapH) : padBottom;
            int visibleContentH = Math.Max(0, availH - padTop - hBarSpace);

            if (!needVBar && contentH > visibleContentH)
            {
                needVBar = true;
                vBarSpace = scrollBarW + gapV;
                visibleContentW = Math.Max(0, availW - padLeft - vBarSpace);
                needHBar = HorizontalScrollEnabled && (contentW > visibleContentW);
                hBarSpace = needHBar ? (scrollBarH + gapH) : padBottom;
                visibleContentH = Math.Max(0, availH - padTop - hBarSpace);
            }

            int effectiveContentW = HorizontalScrollEnabled ? Math.Max(visibleContentW, contentW) : visibleContentW;
            int effectiveContentH = Math.Max(visibleContentH, contentH);

            var targetVpBounds = new Rectangle(padLeft, padTop, visibleContentW, visibleContentH);
            if (_viewport.Bounds != targetVpBounds)
            {
                _viewport.SetBounds(padLeft, padTop, visibleContentW, visibleContentH);
            }

            var targetContentSize = new Size(effectiveContentW, effectiveContentH);
            if (_content.Size != targetContentSize)
            {
                _content.Size = targetContentSize;
            }

            int maxV = Math.Max(0, effectiveContentH - visibleContentH);
            _scrollBar.Minimum = 0;
            _scrollBar.Maximum = maxV;
            _scrollBar.LargeChange = Math.Max(1, visibleContentH);
            _scrollBar.Visible = needVBar && maxV > 0;
            if (_scrollBar.Value > maxV) _scrollBar.Value = maxV;

            int maxH = Math.Max(0, effectiveContentW - visibleContentW);
            _hScrollBar.Minimum = 0;
            _hScrollBar.Maximum = maxH;
            _hScrollBar.LargeChange = Math.Max(1, visibleContentW);
            _hScrollBar.Visible = needHBar && maxH > 0;
            if (_hScrollBar.Value > maxH) _hScrollBar.Value = maxH;

            // Layout scrollbars and corner panel
            int vBarX = availW - scrollBarW;
            int vBarY = padTop;
            int vBarHeight = Math.Max(0, availH - padTop - (needHBar ? scrollBarH : 0));

            int hBarX = padLeft;
            int hBarY = availH - scrollBarH;
            int hBarWidth = Math.Max(0, availW - padLeft - (needVBar ? scrollBarW : 0));

            if (needVBar && needHBar)
            {
                _scrollBar.SetBounds(vBarX, vBarY, scrollBarW, vBarHeight);
                _hScrollBar.SetBounds(hBarX, hBarY, hBarWidth, scrollBarH);
                _corner.SetBounds(vBarX, hBarY, scrollBarW, scrollBarH);
                _corner.Visible = true;
                _corner.BackColor = effectiveBg;
            }
            else if (needVBar)
            {
                _scrollBar.SetBounds(vBarX, vBarY, scrollBarW, vBarHeight);
                _hScrollBar.Visible = false;
                _corner.Visible = false;
            }
            else if (needHBar)
            {
                _hScrollBar.SetBounds(hBarX, hBarY, hBarWidth, scrollBarH);
                _scrollBar.Visible = false;
                _corner.Visible = false;
            }
            else
            {
                _scrollBar.Visible = false;
                _hScrollBar.Visible = false;
                _corner.Visible = false;
            }

            _scrollBar.BringToFront();
            _hScrollBar.BringToFront();
            if (_corner.Visible) _corner.BringToFront();

            ApplyScrollPosition();
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
        finally
        {
            _isUpdating = false;
        }
    }

    public bool PreFilterMessage(ref Message m)
    {
        // Intercept WM_MOUSEWHEEL (0x020A) if cursor is inside this container
        if (m.Msg == 0x020A && IsHandleCreated && Visible && (_scrollBar.Visible || _hScrollBar.Visible))
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
                bool isShift = (ModifierKeys & Keys.Shift) != 0;
                if (isShift && _hScrollBar.Visible)
                {
                    _hScrollBar.ScrollBy(-Math.Sign(delta) * 50);
                    return true;
                }
                else if (_scrollBar.Visible)
                {
                    _scrollBar.ScrollBy(-Math.Sign(delta) * 50);
                    return true;
                }
                else if (_hScrollBar.Visible)
                {
                    _hScrollBar.ScrollBy(-Math.Sign(delta) * 50);
                    return true;
                }
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
            catch (Exception caught) { AppLog.Swallowed(caught); }
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// A high-performance, flicker-free vertical stack panel that sequentially stacks child controls top-to-bottom,
/// preserving their explicit heights and stretching their widths, with zero TableLayoutPanel deformation/squashing.
/// </summary>
public class ModernVerticalStackPanel : Panel
{
    public ModernVerticalStackPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        AutoScroll = false;
        Margin = Padding.Empty;
        Padding = new Padding(0, 0, 8, 0);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int y = Padding.Top;
        int clientW = Math.Max(50, ClientSize.Width - Padding.Horizontal);

        for (int i = 0; i < Controls.Count; i++)
        {
            var c = Controls[i];
            if (!c.Visible) continue;

            y += c.Margin.Top;
            int h = c.AutoSize ? c.GetPreferredSize(new Size(clientW - c.Margin.Horizontal, 0)).Height : c.Height;
            if (h <= 0) h = c.Height > 0 ? c.Height : 24;
            int x = Padding.Left + c.Margin.Left;
            int w = Math.Max(30, clientW - c.Margin.Horizontal);
            c.SetBounds(x, y, w, h);
            y += h + c.Margin.Bottom;
        }
        y += Padding.Bottom;

        if (Height != y)
        {
            Height = y;
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int y = Padding.Top;
        int maxW = proposedSize.Width > 0 ? proposedSize.Width : (Width > 0 ? Width : 300);
        for (int i = 0; i < Controls.Count; i++)
        {
            var c = Controls[i];
            if (!c.Visible) continue;
            int h = c.AutoSize ? c.GetPreferredSize(new Size(maxW - c.Margin.Horizontal, 0)).Height : c.Height;
            if (h <= 0) h = c.Height > 0 ? c.Height : 24;
            y += c.Margin.Top + h + c.Margin.Bottom;
        }
        y += Padding.Bottom;
        return new Size(maxW, y);
    }
}
