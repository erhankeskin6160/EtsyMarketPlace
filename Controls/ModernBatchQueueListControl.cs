namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

/// <summary>
/// Toplu AI Arka Plan Fabrikası için modern, kart tabanlı, SaaS tasarım diline uygun fotoğraf kuyruk kontrolü.
/// Windows'un eski Win32 ListView ve beyaz scroll bar'ları yerine şık ModernScrollPanel kullanır.
/// </summary>
internal sealed class ModernBatchQueueListControl : UserControl
{
    public event EventHandler<BatchInputItem>? SelectedItemChanged;

    private readonly ModernScrollPanel _scrollPanel;
    private readonly FlowLayoutPanel _cardFlow;
    private readonly Panel _emptyStatePanel;
    private readonly List<BatchQueueItemCard> _cards = [];
    private BatchInputItem? _selectedItem;

    public BatchInputItem? SelectedItem => _selectedItem;
    public int ItemsCount => _cards.Count;
    public IReadOnlyList<BatchInputItem> LoadedItems => _cards.Select(c => c.Item).ToList();

    public ModernBatchQueueListControl()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint,
            true);

        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        // Kaydırma Paneli (Modern ve akıcı)
        _scrollPanel = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0),
            ScrollBarGap = 8,
            BackColor = Color.FromArgb(15, 23, 42)
        };

        // Kart Akış Konteyneri
        _cardFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = new Padding(1, 2, 1, 6),
            BackColor = Color.FromArgb(15, 23, 42)
        };

        // Boş Durum Bilgisi (Empty State)
        _emptyStatePanel = BuildEmptyStatePanel();

        _scrollPanel.SetContent(_cardFlow);
        Controls.Add(_scrollPanel);
        Controls.Add(_emptyStatePanel);

        UpdateEmptyState();
    }

    private Panel BuildEmptyStatePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Visible = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));

        var contentBox = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var lblIcon = new Label
        {
            Text = "📷",
            Font = new Font("Segoe UI Emoji", 26F),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 6)
        };

        var lblTitle = new Label
        {
            Text = "Fotoğraf Kuyruğu Boş",
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 4)
        };

        var lblDesc = new Label
        {
            Text = "Yukarıdaki butonlarla bilgisayarınızdan veya\nEtsy mağazanızdan fotoğrafları aktarın.",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = true,
            Anchor = AnchorStyles.None
        };

        contentBox.Controls.Add(lblIcon);
        contentBox.Controls.Add(lblTitle);
        contentBox.Controls.Add(lblDesc);

        layout.Controls.Add(contentBox, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private void UpdateEmptyState()
    {
        bool isEmpty = _cards.Count == 0;
        _emptyStatePanel.Visible = isEmpty;
        _scrollPanel.Visible = !isEmpty;
        if (isEmpty) _emptyStatePanel.BringToFront();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateCardWidths();
    }

    private void UpdateCardWidths()
    {
        int availableWidth = Math.Max(220, _scrollPanel.ClientSize.Width - 14);
        foreach (var card in _cards)
        {
            if (card.Width != availableWidth)
            {
                card.Width = availableWidth;
            }
        }
    }

    public void AddItems(IReadOnlyList<BatchInputItem> items)
    {
        if (items.Count == 0) return;

        _cardFlow.SuspendLayout();
        int targetWidth = Math.Max(220, _scrollPanel.ClientSize.Width - 14);

        foreach (var item in items)
        {
            if (_cards.Any(c => c.Item.Id == item.Id)) continue;

            Bitmap? thumb = null;
            try
            {
                using var ms = new MemoryStream(item.ImageBytes);
                using var orig = new Bitmap(ms);
                thumb = new Bitmap(orig, new Size(64, 64));
            }
            catch { }

            int index = _cards.Count + 1;
            var card = new BatchQueueItemCard(item, thumb, index)
            {
                Width = targetWidth
            };

            card.CardClicked += (_, _) => SelectItem(card.Item);

            _cards.Add(card);
            _cardFlow.Controls.Add(card);
        }

        _cardFlow.ResumeLayout(true);
        UpdateEmptyState();

        if (_selectedItem == null && _cards.Count > 0)
        {
            SelectItem(_cards[0].Item);
        }
        else
        {
            _scrollPanel.RecalculateScroll();
        }
    }

    public void SelectItem(BatchInputItem item)
    {
        _selectedItem = item;
        foreach (var c in _cards)
        {
            c.IsSelected = c.Item.Id == item.Id;
        }

        SelectedItemChanged?.Invoke(this, item);
    }

    public void SelectFirst()
    {
        if (_cards.Count > 0)
        {
            SelectItem(_cards[0].Item);
        }
    }

    public void UpdateStatus(string itemId, string status)
    {
        var card = _cards.FirstOrDefault(c => c.Item.Id == itemId);
        if (card != null)
        {
            card.Status = status;
        }
    }

    public void ClearQueue()
    {
        _cardFlow.SuspendLayout();
        foreach (var card in _cards)
        {
            card.DisposeThumbnail();
            card.Dispose();
        }
        _cards.Clear();
        _cardFlow.Controls.Clear();
        _cardFlow.ResumeLayout(true);

        _selectedItem = null;
        UpdateEmptyState();
        _scrollPanel.RecalculateScroll();
    }
}

/// <summary>
/// Kuyruk listesi içindeki her bir görsel için tasarlanmış modern, çift arabellekli kart bileşeni.
/// </summary>
internal sealed class BatchQueueItemCard : Control
{
    public event EventHandler? CardClicked;

    public BatchInputItem Item { get; }
    public Bitmap? Thumbnail { get; private set; }
    public int Index { get; }

    private string _status = "⏳ Bekliyor";
    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                Invalidate();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Invalidate();
            }
        }
    }

    private bool _isHovered;

    public BatchQueueItemCard(BatchInputItem item, Bitmap? thumbnail, int index)
    {
        Item = item;
        Thumbnail = thumbnail;
        Index = index;

        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Height = 56;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 0, 4);
    }

    public void DisposeThumbnail()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            CardClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width;
        int h = Height;
        var rect = new Rectangle(0, 0, w - 1, h - 1);

        // 1. Arka Plan & Kenarlık
        Color bgColor = _isSelected
            ? Color.FromArgb(28, 38, 64)
            : (_isHovered ? Color.FromArgb(24, 34, 53) : Color.FromArgb(18, 26, 44));

        Color borderColor = _isSelected
            ? Color.FromArgb(99, 102, 241) // Neon Indigo
            : (_isHovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(38, 49, 68));

        using (var cardPath = ModernCardPanel.CreateRoundedRectanglePath(rect, 6))
        {
            using var bgBrush = new SolidBrush(bgColor);
            g.FillPath(bgBrush, cardPath);

            using var pen = new Pen(borderColor, _isSelected ? 1.4f : 1f);
            g.DrawPath(pen, cardPath);
        }

        // 2. Seçim Sol Neon Çizgisi
        if (_isSelected)
        {
            using var accentBrush = new SolidBrush(Color.FromArgb(99, 102, 241));
            g.FillRectangle(accentBrush, 1, 4, 3, h - 8);
        }

        // 3. Küçük Resim (Thumbnail)
        var thumbRect = new Rectangle(10, 6, 44, 44);
        using (var thumbPath = ModernCardPanel.CreateRoundedRectanglePath(thumbRect, 4))
        {
            using var thumbBg = new SolidBrush(Color.FromArgb(15, 23, 42));
            g.FillPath(thumbBg, thumbPath);

            if (Thumbnail != null)
            {
                g.SetClip(thumbPath);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Thumbnail, thumbRect);
                g.ResetClip();
            }
            else
            {
                using var iconFont = new Font("Segoe UI", 12F);
                using var iconBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("🖼️", iconFont, iconBrush, thumbRect, sfCenter);
            }

            using var thumbBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1f);
            g.DrawPath(thumbBorderPen, thumbPath);
        }

        // 4. Durum Rozeti (Badge) - Sağ Tarafta
        int badgeW = 74;
        int badgeH = 20;
        int badgeX = w - badgeW - 10;
        int badgeY = (h - badgeH) / 2;
        var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

        Color bBg, bBorder, bFg;
        if (_status.Contains("Başarılı") || _status.Contains("✅"))
        {
            bBg = Color.FromArgb(6, 78, 59);
            bBorder = Color.FromArgb(16, 185, 129);
            bFg = Color.FromArgb(110, 231, 183);
        }
        else if (_status.Contains("Hata") || _status.Contains("❌"))
        {
            bBg = Color.FromArgb(76, 5, 25);
            bBorder = Color.FromArgb(244, 63, 94);
            bFg = Color.FromArgb(254, 205, 211);
        }
        else if (_status.Contains("İşleniyor") || _status.Contains("⚡"))
        {
            bBg = Color.FromArgb(30, 58, 138);
            bBorder = Color.FromArgb(59, 130, 246);
            bFg = Color.FromArgb(191, 219, 254);
        }
        else
        {
            bBg = Color.FromArgb(28, 38, 56);
            bBorder = Color.FromArgb(71, 85, 105);
            bFg = Color.FromArgb(148, 163, 184);
        }

        using (var badgePath = ModernCardPanel.CreateRoundedRectanglePath(badgeRect, 4))
        {
            using var brush = new SolidBrush(bBg);
            g.FillPath(brush, badgePath);

            using var pen = new Pen(bBorder, 1f);
            g.DrawPath(pen, badgePath);

            using var bFont = new Font("Segoe UI Semibold", 7.2F, FontStyle.Bold);
            using var textBrush = new SolidBrush(bFg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_status, bFont, textBrush, badgeRect, sf);
        }

        // 5. Başlık ve Alt Açıklama
        int textX = 62;
        int maxTextWidth = badgeX - textX - 6;

        if (maxTextWidth > 30)
        {
            var titleRect = new RectangleF(textX, 9, maxTextWidth, 20);
            using (var titleFont = new Font("Segoe UI Semibold", 8.6F, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_isSelected ? Color.White : Color.FromArgb(226, 232, 240)))
            {
                var sfTitle = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(Item.Title, titleFont, titleBrush, titleRect, sfTitle);
            }

            var subRect = new RectangleF(textX, 30, maxTextWidth, 16);
            using (var subFont = new Font("Segoe UI", 7.4F))
            using (var subBrush = new SolidBrush(Color.FromArgb(120, 135, 155)))
            {
                var sfSub = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                string subText = $"#{Index} • {FormatBytes(Item.ImageBytes.Length)}";
                g.DrawString(subText, subFont, subBrush, subRect, sfSub);
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
