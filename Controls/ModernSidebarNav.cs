namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

public class ModernSidebarNav : UserControl
{
    public event EventHandler<SidebarItemSelectedEventArgs>? ItemSelected;
    public event EventHandler? CollapsedChanged;

    private readonly List<SidebarItem> _items = new();
    private string _selectedItemId = string.Empty;
    private int _hoveredIndex = -1;
    private bool _isCollapsed = false;
    private bool _isHeaderToggleHovered = false;

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
                Width = _isCollapsed ? 64 : 260;
                Invalidate();
                CollapsedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ToggleCollapse()
    {
        IsCollapsed = !_isCollapsed;
    }

    public ModernSidebarNav()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Left;
        Width = 260;
        BackColor = NavBackColor;
        Font = new Font("Segoe UI", 9.5F);
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
        Invalidate();
    }

    public void ClearItems()
    {
        _items.Clear();
        _selectedItemId = string.Empty;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool toggleHovered = HeaderToggleRect.Contains(e.Location);
        if (toggleHovered != _isHeaderToggleHovered)
        {
            _isHeaderToggleHovered = toggleHovered;
            Invalidate();
        }

        int newHovered = GetItemIndexAtPoint(e.Location);
        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            Invalidate();
        }

        Cursor = (_isHeaderToggleHovered || _hoveredIndex >= 0) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex != -1 || _isHeaderToggleHovered)
        {
            _hoveredIndex = -1;
            _isHeaderToggleHovered = false;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
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
        int yOffset = 70; // header space
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == 0 || _items[i].Category != _items[i - 1].Category)
            {
                if (!string.IsNullOrEmpty(_items[i].Category) && !_isCollapsed)
                {
                    yOffset += 26;
                }
            }

            Rectangle itemRect = new Rectangle(10, yOffset, Width - 20, 42);
            if (itemRect.Contains(pt))
            {
                return i;
            }
            yOffset += 46;
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

        // Background
        using (var bgBrush = new SolidBrush(NavBackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Right border line
        using (var borderPen = new Pen(BorderColor, 1f))
        {
            g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
        }

        // Header Branding & Toggle Button
        var logoImg = GetAppLogo();

        if (!_isCollapsed)
        {
            if (logoImg != null)
            {
                var logoRect = new Rectangle(12, 14, 32, 32);
                g.DrawImage(logoImg, logoRect);

                using (var titleFont = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("EtsyMarketPlace", titleFont, titleBrush, new PointF(48, 12));
                }
                using (var subFont = new Font("Segoe UI", 8F))
                using (var subBrush = new SolidBrush(TextMutedColor))
                {
                    g.DrawString("3DArtDesignsStore Engine", subFont, subBrush, new PointF(50, 32));
                }
            }
            else
            {
                using (var titleFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("⚡ EtsyMarketPlace", titleFont, titleBrush, new PointF(16, 16));
                }
                using (var subFont = new Font("Segoe UI", 8F))
                using (var subBrush = new SolidBrush(TextMutedColor))
                {
                    g.DrawString("3DArtDesignsStore Engine", subFont, subBrush, new PointF(18, 38));
                }
            }

            // Expanded Toggle Icon (◀ or ☰)
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
                var logoRect = new Rectangle(14, 14, 32, 32);
                g.DrawImage(logoImg, logoRect);
            }
            else
            {
                using (var iconFont = new Font("Segoe UI", 13F))
                using (var iconBrush = new SolidBrush(ItemActiveColor))
                {
                    g.DrawString("⚡", iconFont, iconBrush, new PointF(12, 18));
                }

                using (var arrowFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
                using (var arrowBrush = new SolidBrush(_isHeaderToggleHovered ? Color.White : TextMutedColor))
                {
                    g.DrawString("▶", arrowFont, arrowBrush, new PointF(34, 21));
                }
            }
        }

        // Divider
        using (var divPen = new Pen(BorderColor, 1f))
        {
            g.DrawLine(divPen, 12, 60, Width - 12, 60);
        }

        // Items Rendering
        int yOffset = 70;
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
                    using (var catFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold))
                    using (var catBrush = new SolidBrush(TextMutedColor))
                    {
                        g.DrawString(item.Category.ToUpperInvariant(), catFont, catBrush, new PointF(16, yOffset + 4));
                    }
                    yOffset += 26;
                }
            }

            Rectangle itemRect = new Rectangle(10, yOffset, Width - 20, 42);

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
            using (var iconFont = new Font("Segoe UI Emoji", 11F))
            using (var iconBrush = new SolidBrush(isSelected ? ItemActiveColor : (isHovered ? Color.White : TextMutedColor)))
            {
                g.DrawString(item.IconSymbol, iconFont, iconBrush, new PointF(itemRect.X + (_isCollapsed ? 14 : 12), itemRect.Y + 10));
            }

            // Title & Badge (when expanded)
            if (!_isCollapsed)
            {
                float badgeReservedWidth = 0;
                if (!string.IsNullOrEmpty(item.BadgeText))
                {
                    using (var badgeFont = new Font("Segoe UI Semibold", 7.5F))
                    {
                        var badgeSize = g.MeasureString(item.BadgeText, badgeFont);
                        badgeReservedWidth = badgeSize.Width + 14;
                        var badgeRect = new RectangleF(itemRect.Right - badgeSize.Width - 10, itemRect.Y + 11, badgeSize.Width + 8, 18);

                        using var badgeBrush = new SolidBrush(item.BadgeColor);
                        using var badgePath = ModernCardPanel.CreateRoundedRectanglePath(Rectangle.Round(badgeRect), 6);
                        g.FillPath(badgeBrush, badgePath);

                        using var badgeTextBrush = new SolidBrush(Color.White);
                        g.DrawString(item.BadgeText, badgeFont, badgeTextBrush, badgeRect.X + 4, badgeRect.Y + 1);
                    }
                }

                float maxTitleWidth = Math.Max(40, itemRect.Width - 46 - badgeReservedWidth);
                var titleBounds = new RectangleF(itemRect.X + 38, itemRect.Y + 11, maxTitleWidth, 20);
                using (var titleFont = new Font(Font, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (var titleBrush = new SolidBrush(isSelected ? Color.White : (isHovered ? Color.White : TextColor)))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(item.Title, titleFont, titleBrush, titleBounds, sf);
                }
            }

            yOffset += 46;
        }
    }
}
