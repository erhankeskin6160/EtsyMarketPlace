namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

public class SidebarItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IconSymbol { get; set; } = "🔹";
    public string Category { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public Color BadgeColor { get; set; } = Color.FromArgb(99, 102, 241);
}

public class SidebarItemSelectedEventArgs : EventArgs
{
    public SidebarItem Item { get; }
    public SidebarItemSelectedEventArgs(SidebarItem item) => Item = item;
}

public class ModernSidebarNav : UserControl, IMessageFilter
{
    public const int DefaultExpandedWidth = 275;
    public const int CollapsedWidth = 64;

    public event EventHandler<SidebarItemSelectedEventArgs>? ItemSelected;
    public event EventHandler? CollapsedChanged;

    private readonly List<SidebarItem> _items = new();
    private string _selectedItemId = string.Empty;
    private int _hoveredIndex = -1;
    private bool _isCollapsed = false;
    private bool _isHeaderToggleHovered = false;

    // Scrolling & Dragging
    private int _scrollOffset = 0;
    private int _totalContentHeight = 0;
    private bool _isDraggingScrollbar = false;
    private int _dragStartY = 0;
    private int _dragStartScroll = 0;
    private bool _isScrollbarHovered = false;
    private bool _isFilterRegistered = false;

    private Rectangle HeaderToggleRect => _isCollapsed
        ? new Rectangle(8, 10, 48, 40)
        : new Rectangle(Width - 44, 14, 32, 32);

    // Palette tokens
    public Color NavBackColor { get; set; } = Color.FromArgb(15, 23, 42);       // Dark Slate 900
    public Color ItemHoverColor { get; set; } = Color.FromArgb(30, 41, 59);      // Slate 800
    public Color ItemActiveColor { get; set; } = Color.FromArgb(99, 102, 241);    // Indigo 500
    public Color ItemActiveBg { get; set; } = Color.FromArgb(30, 27, 75);        // Indigo 950 Tint
    public Color TextColor { get; set; } = Color.FromArgb(226, 232, 240);        // Slate 200
    public Color TextMutedColor { get; set; } = Color.FromArgb(148, 163, 184);   // Slate 400
    public Color BorderColor { get; set; } = Color.FromArgb(33, 41, 55);         // Slate border

    public string SelectedItemId
    {
        get => _selectedItemId;
        set
        {
            if (_selectedItemId != value)
            {
                _selectedItemId = value;
                EnsureVisible(value);
                Invalidate();
            }
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                Width = _isCollapsed ? CollapsedWidth : DefaultExpandedWidth;
                _scrollOffset = 0;
                Invalidate();
                CollapsedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ToggleCollapse()
    {
        IsCollapsed = !_isCollapsed;
    }

    private int MaxScroll => Math.Max(0, _totalContentHeight - Height);

    public ModernSidebarNav()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        Dock = DockStyle.Left;
        Width = DefaultExpandedWidth;
        BackColor = NavBackColor;
        Font = new Font("Segoe UI", 9.5F);

        try
        {
            Application.AddMessageFilter(this);
            _isFilterRegistered = true;
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _isFilterRegistered)
        {
            try
            {
                Application.RemoveMessageFilter(this);
                _isFilterRegistered = false;
            }
            catch { }
        }
        base.Dispose(disposing);
    }

    public bool PreFilterMessage(ref Message m)
    {
        // Intercept WM_MOUSEWHEEL (0x020A) if cursor is anywhere inside this sidebar control
        if (m.Msg == 0x020A && IsHandleCreated && Visible && MaxScroll > 0)
        {
            var cursorScreen = Cursor.Position;
            var screenBounds = RectangleToScreen(ClientRectangle);
            if (screenBounds.Contains(cursorScreen))
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                int scrollAmount = -Math.Sign(delta) * 50;
                SetScrollOffset(_scrollOffset + scrollAmount);
                return true;
            }
        }
        return false;
    }

    public void AddItem(string id, string title, string iconSymbol = "🔹", string category = "", string badgeText = "")
    {
        _items.Add(new SidebarItem
        {
            Id = id,
            Title = title,
            IconSymbol = iconSymbol,
            Category = category,
            BadgeText = badgeText
        });
        if (string.IsNullOrEmpty(_selectedItemId))
        {
            _selectedItemId = id;
        }
        _totalContentHeight = CalculateContentHeight();
        Invalidate();
    }

    public void ClearItems()
    {
        _items.Clear();
        _selectedItemId = string.Empty;
        _scrollOffset = 0;
        _totalContentHeight = 0;
        Invalidate();
    }

    private int CalculateContentHeight()
    {
        int y = 68;
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == 0 || _items[i].Category != _items[i - 1].Category)
            {
                if (!string.IsNullOrEmpty(_items[i].Category) && !_isCollapsed)
                {
                    y += 24;
                }
            }
            y += 40;
        }
        return y + 16;
    }

    public void SetScrollOffset(int newOffset)
    {
        int clamped = Math.Clamp(newOffset, 0, MaxScroll);
        if (clamped != _scrollOffset)
        {
            _scrollOffset = clamped;
            Invalidate();
        }
    }

    public void EnsureVisible(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || _items.Count == 0) return;
        int y = 68;
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == 0 || _items[i].Category != _items[i - 1].Category)
            {
                if (!string.IsNullOrEmpty(_items[i].Category) && !_isCollapsed)
                {
                    y += 24;
                }
            }
            if (_items[i].Id == itemId)
            {
                if (y < _scrollOffset + 68)
                {
                    SetScrollOffset(y - 68);
                }
                else if (y + 40 > _scrollOffset + Height)
                {
                    SetScrollOffset(y + 40 - Height + 16);
                }
                break;
            }
            y += 40;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _totalContentHeight = CalculateContentHeight();
        if (_scrollOffset > MaxScroll)
        {
            _scrollOffset = MaxScroll;
        }
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (MaxScroll > 0)
        {
            int delta = (e.Delta > 0 ? -1 : 1) * 50;
            SetScrollOffset(_scrollOffset + delta);
        }
    }

    private Rectangle GetScrollbarHitRect()
    {
        int trackY = 64;
        int trackHeight = Math.Max(10, Height - trackY - 4);
        return new Rectangle(Width - 14, trackY, 14, trackHeight);
    }

    private Rectangle GetScrollbarThumbRect()
    {
        if (MaxScroll <= 0) return Rectangle.Empty;
        int trackY = 64;
        int trackHeight = Math.Max(10, Height - trackY - 4);
        float viewRatio = (float)(Height - 64) / Math.Max(1, _totalContentHeight);
        int thumbHeight = Math.Clamp((int)(trackHeight * viewRatio), 24, trackHeight);
        int travel = trackHeight - thumbHeight;
        int thumbY = travel > 0 ? trackY + (int)(travel * ((float)_scrollOffset / MaxScroll)) : trackY;
        return new Rectangle(Width - 6, thumbY, 4, thumbHeight);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && MaxScroll > 0)
        {
            var hitRect = GetScrollbarHitRect();
            if (hitRect.Contains(e.Location))
            {
                var thumbRect = GetScrollbarThumbRect();
                if (thumbRect.Contains(e.Location))
                {
                    _isDraggingScrollbar = true;
                    _dragStartY = e.Y;
                    _dragStartScroll = _scrollOffset;
                    Capture = true;
                }
                else
                {
                    // Clicked on track: page scroll
                    int targetY = e.Y - 64;
                    int trackHeight = Height - 68;
                    if (trackHeight > 0)
                    {
                        float pct = Math.Clamp((float)targetY / trackHeight, 0f, 1f);
                        SetScrollOffset((int)(pct * MaxScroll));
                    }
                }
                return;
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDraggingScrollbar)
        {
            _isDraggingScrollbar = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDraggingScrollbar && MaxScroll > 0)
        {
            int deltaY = e.Y - _dragStartY;
            int trackHeight = Height - 68;
            var thumbRect = GetScrollbarThumbRect();
            int travel = trackHeight - thumbRect.Height;
            if (travel > 0)
            {
                float scrollPerPixel = (float)MaxScroll / travel;
                SetScrollOffset(_dragStartScroll + (int)(deltaY * scrollPerPixel));
            }
            return;
        }

        bool toggleHovered = HeaderToggleRect.Contains(e.Location);
        if (toggleHovered != _isHeaderToggleHovered)
        {
            _isHeaderToggleHovered = toggleHovered;
            Invalidate();
        }

        var barHit = GetScrollbarHitRect();
        bool scrollHovered = MaxScroll > 0 && barHit.Contains(e.Location);
        if (scrollHovered != _isScrollbarHovered)
        {
            _isScrollbarHovered = scrollHovered;
            Invalidate();
        }

        int newHovered = GetItemIndexAtPoint(e.Location);
        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            Invalidate();
        }

        Cursor = (_isHeaderToggleHovered || _hoveredIndex >= 0 || scrollHovered || _isDraggingScrollbar)
            ? Cursors.Hand
            : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_isDraggingScrollbar)
        {
            if (_hoveredIndex != -1 || _isHeaderToggleHovered || _isScrollbarHovered)
            {
                _hoveredIndex = -1;
                _isHeaderToggleHovered = false;
                _isScrollbarHovered = false;
                Invalidate();
            }
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && !_isDraggingScrollbar)
        {
            if (HeaderToggleRect.Contains(e.Location))
            {
                ToggleCollapse();
                return;
            }

            int index = GetItemIndexAtPoint(e.Location);
            if (index >= 0 && index < _items.Count)
            {
                var item = _items[index];
                SelectedItemId = item.Id;
                ItemSelected?.Invoke(this, new SidebarItemSelectedEventArgs(item));
            }
        }
    }

    private int GetItemIndexAtPoint(Point pt)
    {
        if (pt.Y < 62) return -1; // Header area

        int yOffset = 68 - _scrollOffset;
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == 0 || _items[i].Category != _items[i - 1].Category)
            {
                if (!string.IsNullOrEmpty(_items[i].Category) && !_isCollapsed)
                {
                    yOffset += 24;
                }
            }

            Rectangle itemRect = new Rectangle(8, yOffset, Width - 16, 36);
            if (itemRect.Contains(pt))
            {
                return i;
            }
            yOffset += 40;
        }
        return -1;
    }

    private static Image? _cachedAppLogo;
    private static Image? GetAppLogo()
    {
        if (_cachedAppLogo != null) return _cachedAppLogo;
        try
        {
            if (File.Exists("app_icon.png"))
            {
                _cachedAppLogo = Image.FromFile("app_icon.png");
            }
        }
        catch { }
        return _cachedAppLogo;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        _totalContentHeight = CalculateContentHeight();
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll);

        // 1. Background
        using (var bgBrush = new SolidBrush(NavBackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // 2. Right border line
        using (var borderPen = new Pen(BorderColor, 1f))
        {
            g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
        }

        // 3. Clip and render items underneath header
        var prevClip = g.Clip;
        g.SetClip(new Rectangle(0, 62, Width, Math.Max(0, Height - 62)));

        int yOffset = 68 - _scrollOffset;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            bool isSelected = item.Id == _selectedItemId;
            bool isHovered = i == _hoveredIndex;

            // Category Label Header
            if (i == 0 || item.Category != _items[i - 1].Category)
            {
                if (!string.IsNullOrEmpty(item.Category) && !_isCollapsed)
                {
                    if (yOffset + 24 >= 55 && yOffset <= Height)
                    {
                        using var catFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
                        using var catBrush = new SolidBrush(TextMutedColor);
                        g.DrawString(item.Category.ToUpperInvariant(), catFont, catBrush, new PointF(14, yOffset + 3));
                    }
                    yOffset += 24;
                }
            }

            Rectangle itemRect = new Rectangle(8, yOffset, Width - 16, 36);

            // Only draw if within visible viewport
            if (yOffset + 36 >= 55 && yOffset <= Height)
            {
                // Item Background
                if (isSelected)
                {
                    using var activeBgBrush = new SolidBrush(ItemActiveBg);
                    using var activePath = ModernCardPanel.CreateRoundedRectanglePath(itemRect, 8);
                    g.FillPath(activeBgBrush, activePath);

                    // Left Active Indicator Bar
                    var indicatorRect = new Rectangle(itemRect.X, itemRect.Y + 6, 4, itemRect.Height - 12);
                    using var barBrush = new SolidBrush(ItemActiveColor);
                    using var barPath = ModernCardPanel.CreateRoundedRectanglePath(indicatorRect, 2);
                    g.FillPath(barBrush, barPath);
                }
                else if (isHovered)
                {
                    using var hoverBgBrush = new SolidBrush(ItemHoverColor);
                    using var hoverPath = ModernCardPanel.CreateRoundedRectanglePath(itemRect, 8);
                    g.FillPath(hoverBgBrush, hoverPath);
                }

                // Icon
                using (var iconFont = new Font("Segoe UI Emoji", 10.5F))
                using (var iconBrush = new SolidBrush(isSelected ? ItemActiveColor : (isHovered ? Color.White : TextMutedColor)))
                {
                    g.DrawString(item.IconSymbol, iconFont, iconBrush, new PointF(itemRect.X + (_isCollapsed ? 15 : 10), itemRect.Y + 7));
                }

                // Title & Badge (when expanded)
                if (!_isCollapsed)
                {
                    float badgeReservedWidth = 0;
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        using var badgeFont = new Font("Segoe UI Semibold", 7.5F);
                        var badgeSize = g.MeasureString(item.BadgeText, badgeFont);
                        badgeReservedWidth = badgeSize.Width + 14;
                        var badgeRect = new RectangleF(itemRect.Right - badgeSize.Width - 8, itemRect.Y + 9, badgeSize.Width + 8, 18);

                        using var badgeBrush = new SolidBrush(item.BadgeColor);
                        using var badgePath = ModernCardPanel.CreateRoundedRectanglePath(Rectangle.Round(badgeRect), 6);
                        g.FillPath(badgeBrush, badgePath);

                        using var badgeTextBrush = new SolidBrush(Color.White);
                        g.DrawString(item.BadgeText, badgeFont, badgeTextBrush, badgeRect.X + 4, badgeRect.Y + 1);
                    }

                    float maxTitleWidth = Math.Max(40, itemRect.Width - 38 - badgeReservedWidth);
                    var titleBounds = new RectangleF(itemRect.X + 34, itemRect.Y + 8, maxTitleWidth, 20);
                    using var titleFont = new Font(Font, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    using var titleBrush = new SolidBrush(isSelected ? Color.White : (isHovered ? Color.White : TextColor));
                    using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
                    g.DrawString(item.Title, titleFont, titleBrush, titleBounds, sf);
                }
            }

            yOffset += 40;
        }

        g.Clip = prevClip;

        // 4. Fixed Header on Top (Opaque background so scrolling items cleanly hide behind it)
        using (var headerBgBrush = new SolidBrush(NavBackColor))
        {
            g.FillRectangle(headerBgBrush, 0, 0, Width - 1, 62);
        }

        var logoImg = GetAppLogo();

        if (!_isCollapsed)
        {
            if (logoImg != null)
            {
                var logoRect = new Rectangle(12, 14, 32, 32);
                g.DrawImage(logoImg, logoRect);

                using var titleFont = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.White);
                g.DrawString("EtsyMarketPlace", titleFont, titleBrush, new PointF(48, 12));

                using var subFont = new Font("Segoe UI", 8F);
                using var subBrush = new SolidBrush(TextMutedColor);
                g.DrawString("3DArtDesignsStore Engine", subFont, subBrush, new PointF(50, 32));
            }
            else
            {
                using var titleFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.White);
                g.DrawString("⚡ EtsyMarketPlace", titleFont, titleBrush, new PointF(14, 15));

                using var subFont = new Font("Segoe UI", 8F);
                using var subBrush = new SolidBrush(TextMutedColor);
                g.DrawString("3DArtDesignsStore Engine", subFont, subBrush, new PointF(16, 36));
            }

            // Expanded Toggle Icon (◀)
            var toggleRect = HeaderToggleRect;
            if (_isHeaderToggleHovered)
            {
                using var hoverBrush = new SolidBrush(ItemHoverColor);
                using var hoverPath = ModernCardPanel.CreateRoundedRectanglePath(toggleRect, 6);
                g.FillPath(hoverBrush, hoverPath);
            }
            using (var btnFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold))
            using (var btnBrush = new SolidBrush(_isHeaderToggleHovered ? Color.White : TextMutedColor))
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("◀", btnFont, btnBrush, toggleRect, format);
            }
        }
        else
        {
            var toggleRect = HeaderToggleRect;
            if (_isHeaderToggleHovered)
            {
                using var hoverBrush = new SolidBrush(ItemHoverColor);
                using var hoverPath = ModernCardPanel.CreateRoundedRectanglePath(toggleRect, 6);
                g.FillPath(hoverBrush, hoverPath);
            }

            if (logoImg != null)
            {
                var logoRect = new Rectangle(16, 14, 32, 32);
                g.DrawImage(logoImg, logoRect);
            }
            else
            {
                using var iconFont = new Font("Segoe UI", 13F);
                using var iconBrush = new SolidBrush(ItemActiveColor);
                g.DrawString("⚡", iconFont, iconBrush, new PointF(12, 18));

                using var arrowFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                using var arrowBrush = new SolidBrush(_isHeaderToggleHovered ? Color.White : TextMutedColor);
                g.DrawString("▶", arrowFont, arrowBrush, new PointF(34, 21));
            }
        }

        // Header Divider
        using (var divPen = new Pen(BorderColor, 1f))
        {
            g.DrawLine(divPen, 10, 60, Width - 10, 60);
        }

        // 5. Sleek Overlay Scrollbar
        if (MaxScroll > 0)
        {
            var thumbRect = GetScrollbarThumbRect();
            if (!thumbRect.IsEmpty)
            {
                Color thumbColor = _isDraggingScrollbar
                    ? Color.FromArgb(129, 140, 248)
                    : (_isScrollbarHovered ? Color.FromArgb(99, 102, 241) : Color.FromArgb(71, 85, 105));

                using var thumbBrush = new SolidBrush(thumbColor);
                using var thumbPath = ModernCardPanel.CreateRoundedRectanglePath(thumbRect, 2);
                g.FillPath(thumbBrush, thumbPath);
            }
        }
    }
}
