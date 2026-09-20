namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Modern, interaktif ve yüksek kaliteli AI İşlem Motoru Seçim Tablosu.
/// Dropdown kutusu yerine şık bir masaüstü SaaS tablosu görünümü sunar.
/// </summary>
public sealed class AiEngineSelectorTableControl : Control
{
    public event EventHandler? SelectedIndexChanged;
    public event Action<string>? KeyConfigRequested;

    private int _selectedIndex = 0;
    private int _hoveredIndex = -1;
    private int _hoveredKeyBadgeIndex = -1;

    // API Key Durumları
    private bool _hasOpenAiKey = false;
    private bool _hasGeminiKey = false;
    private bool _hasPhotoRoomKey = false;

    private readonly struct EngineRowData
    {
        public readonly string Icon;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string Tag;
        public readonly Color TagBg;
        public readonly Color TagBorder;
        public readonly Color TagFg;
        public readonly Color AccentColor;
        public readonly string ProviderId;
        public readonly string SummaryDesc;

        public EngineRowData(
            string icon,
            string title,
            string subtitle,
            string tag,
            Color tagBg,
            Color tagBorder,
            Color tagFg,
            Color accentColor,
            string providerId,
            string summaryDesc)
        {
            Icon = icon;
            Title = title;
            Subtitle = subtitle;
            Tag = tag;
            TagBg = tagBg;
            TagBorder = tagBorder;
            TagFg = tagFg;
            AccentColor = accentColor;
            ProviderId = providerId;
            SummaryDesc = summaryDesc;
        }
    }

    private static readonly EngineRowData[] Engines =
    [
        new(
            icon: "⚡",
            title: "OpenAI Flare 2.5",
            subtitle: "Toplu & Ultra Hızlı (1-2 sn)",
            tag: "Toplu",
            tagBg: Color.FromArgb(6, 78, 59),
            tagBorder: Color.FromArgb(16, 185, 129),
            tagFg: Color.FromArgb(110, 231, 183),
            accentColor: Color.FromArgb(16, 185, 129),
            providerId: "openai",
            summaryDesc: "Maksimum hız ve düşük gecikme ile toplu görsel üretimi."),
        new(
            icon: "🌟",
            title: "OpenAI Sunburst 2.5",
            subtitle: "Vitrin & 4K Sadakat (Studio)",
            tag: "Vitrin",
            tagBg: Color.FromArgb(49, 46, 129),
            tagBorder: Color.FromArgb(99, 102, 241),
            tagFg: Color.FromArgb(165, 180, 252),
            accentColor: Color.FromArgb(99, 102, 241),
            providerId: "openai",
            summaryDesc: "Vitrin fotoğrafları için 4K keskin detaylar ve stüdyo ışığı."),
        new(
            icon: "🔵",
            title: "Gemini Flash Image",
            subtitle: "Visual Grounding & Sahne",
            tag: "Zemin",
            tagBg: Color.FromArgb(12, 74, 110),
            tagBorder: Color.FromArgb(14, 165, 233),
            tagFg: Color.FromArgb(125, 211, 252),
            accentColor: Color.FromArgb(14, 165, 233),
            providerId: "gemini",
            summaryDesc: "Akıllı zemin algılama ile ürünü gerçekçi yüzeye yerleştirir."),
        new(
            icon: "✨",
            title: "PhotoRoom Native AI",
            subtitle: "Kusursuz Dekupe & Gölge",
            tag: "Dekupe",
            tagBg: Color.FromArgb(88, 28, 135),
            tagBorder: Color.FromArgb(168, 85, 247),
            tagFg: Color.FromArgb(216, 180, 254),
            accentColor: Color.FromArgb(168, 85, 247),
            providerId: "photoroom",
            summaryDesc: "Hassas kenar dekupe ve doğal temas gölgesi uygular.")
    ];

    private const int HeaderHeight = 26;
    private const int RowHeight = 44;
    private const int FooterHeight = 26;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, Engines.Length - 1);
            if (_selectedIndex != clamped)
            {
                _selectedIndex = clamped;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string SelectedEngineTitle => Engines[_selectedIndex].Title;

    public AiEngineSelectorTableControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        DoubleBuffered = true;
        Width = 330;
        Height = HeaderHeight + (Engines.Length * RowHeight) + FooterHeight; // 26 + 176 + 26 = 228
        Font = new Font("Segoe UI", 9F);
        Cursor = Cursors.Hand;
    }

    public void RefreshKeyStatuses(bool openAi, bool gemini, bool photoRoom)
    {
        _hasOpenAiKey = openAi;
        _hasGeminiKey = gemini;
        _hasPhotoRoomKey = photoRoom;
        Invalidate();
    }

    private bool HasKeyForEngine(int index)
    {
        return index switch
        {
            0 or 1 => _hasOpenAiKey,
            2 => _hasGeminiKey,
            3 => _hasPhotoRoomKey,
            _ => true
        };
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int newHoveredIndex = -1;
        int newHoveredKey = -1;

        if (e.Y >= HeaderHeight && e.Y < HeaderHeight + (Engines.Length * RowHeight))
        {
            newHoveredIndex = (e.Y - HeaderHeight) / RowHeight;
            if (newHoveredIndex >= 0 && newHoveredIndex < Engines.Length)
            {
                // Key badge bölgesi: sağdan yaklaşık 70 piksel
                int badgeRight = Width - 10;
                int badgeLeft = badgeRight - 62;
                int rowY = HeaderHeight + (newHoveredIndex * RowHeight);
                int badgeTop = rowY + 13;
                int badgeBottom = badgeTop + 18;

                if (e.X >= badgeLeft && e.X <= badgeRight && e.Y >= badgeTop && e.Y <= badgeBottom)
                {
                    newHoveredKey = newHoveredIndex;
                }
            }
        }

        if (_hoveredIndex != newHoveredIndex || _hoveredKeyBadgeIndex != newHoveredKey)
        {
            _hoveredIndex = newHoveredIndex;
            _hoveredKeyBadgeIndex = newHoveredKey;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex != -1 || _hoveredKeyBadgeIndex != -1)
        {
            _hoveredIndex = -1;
            _hoveredKeyBadgeIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (e.Y >= HeaderHeight && e.Y < HeaderHeight + (Engines.Length * RowHeight))
        {
            int clickedIndex = (e.Y - HeaderHeight) / RowHeight;
            if (clickedIndex >= 0 && clickedIndex < Engines.Length)
            {
                // Eğer doğrudan Key rozetine basıldıysa ve anahtar yoksa key dialogunu tetikle
                if (_hoveredKeyBadgeIndex == clickedIndex && !HasKeyForEngine(clickedIndex))
                {
                    KeyConfigRequested?.Invoke(Engines[clickedIndex].ProviderId);
                }

                SelectedIndex = clickedIndex;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width;
        int h = Height;

        var outerRect = new Rectangle(0, 0, w - 1, h - 1);

        // 1. Dış Arka Plan ve Kart Kenarlığı
        using (var cardPath = ModernCardPanel.CreateRoundedRectanglePath(outerRect, 8))
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42)); // Slate 950
            g.FillPath(bgBrush, cardPath);

            using var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.2f); // Slate 700
            g.DrawPath(borderPen, cardPath);
        }

        // 2. Tablo Başlık Barı (Header)
        var headerRect = new Rectangle(1, 1, w - 2, HeaderHeight);
        using (var headerBrush = new SolidBrush(Color.FromArgb(24, 34, 53)))
        {
            g.FillRectangle(headerBrush, headerRect);
        }

        using (var headerBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1f))
        {
            g.DrawLine(headerBorderPen, 1, HeaderHeight, w - 2, HeaderHeight);
        }

        using (var headerFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold))
        using (var textMutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
        {
            // Sol başlık
            using var dotBrush = new SolidBrush(Color.FromArgb(99, 102, 241));
            g.FillEllipse(dotBrush, 12, 10, 6, 6);
            g.DrawString("MODEL / MOTOR", headerFont, textMutedBrush, 22, 6);

            var sfRight = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString("TÜR & DURUM", headerFont, textMutedBrush, w - 12, 6, sfRight);
        }

        // 3. Satırlar (Engine Rows)
        using var titleFont = new Font("Segoe UI Semibold", 8.6F, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 7.2F);
        using var badgeFont = new Font("Segoe UI Semibold", 7.0F, FontStyle.Bold);

        for (int i = 0; i < Engines.Length; i++)
        {
            var data = Engines[i];
            int rowY = HeaderHeight + (i * RowHeight);
            var rowRect = new Rectangle(1, rowY, w - 2, RowHeight);
            bool isSelected = i == _selectedIndex;
            bool isHovered = i == _hoveredIndex;
            bool hasKey = HasKeyForEngine(i);

            // Satır Arka Planı
            if (isSelected)
            {
                using var selBrush = new LinearGradientBrush(
                    rowRect,
                    Color.FromArgb(30, 41, 68),
                    Color.FromArgb(22, 31, 52),
                    LinearGradientMode.Horizontal);
                g.FillRectangle(selBrush, rowRect);

                // Sol Neon Vurgu Çizgisi
                using var accentBrush = new SolidBrush(data.AccentColor);
                g.FillRectangle(accentBrush, 1, rowY, 3, RowHeight);
            }
            else if (isHovered)
            {
                using var hoverBrush = new SolidBrush(Color.FromArgb(26, 36, 56));
                g.FillRectangle(hoverBrush, rowRect);
            }

            // Satır Alt Ayrım Çizgisi (Son satır hariç)
            if (i < Engines.Length - 1)
            {
                using var divPen = new Pen(Color.FromArgb(33, 45, 66), 1f);
                g.DrawLine(divPen, 8, rowY + RowHeight, w - 9, rowY + RowHeight);
            }

            // A) Radyo Seçim Göstergesi
            int radioX = 10;
            int radioY = rowY + (RowHeight / 2) - 6;
            var radioRect = new Rectangle(radioX, radioY, 12, 12);

            if (isSelected)
            {
                using var radioPen = new Pen(data.AccentColor, 1.8f);
                g.DrawEllipse(radioPen, radioRect);

                var innerDot = new Rectangle(radioX + 3, radioY + 3, 6, 6);
                using var dotBrush = new SolidBrush(Color.White);
                g.FillEllipse(dotBrush, innerDot);
            }
            else
            {
                using var radioPen = new Pen(Color.FromArgb(71, 85, 105), 1.4f);
                g.DrawEllipse(radioPen, radioRect);
            }

            // B) İkon Kutusu
            int iconBoxX = 26;
            int iconBoxY = rowY + 10;
            var iconBoxRect = new Rectangle(iconBoxX, iconBoxY, 24, 24);
            using (var iconBoxPath = ModernCardPanel.CreateRoundedRectanglePath(iconBoxRect, 5))
            {
                using var iconBgBrush = new SolidBrush(Color.FromArgb(35, data.AccentColor));
                g.FillPath(iconBgBrush, iconBoxPath);

                using var iconBorderPen = new Pen(Color.FromArgb(90, data.AccentColor), 1f);
                g.DrawPath(iconBorderPen, iconBoxPath);
            }

            using (var iconFont = new Font("Segoe UI", 9F))
            using (var iconBrush = new SolidBrush(Color.White))
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(data.Icon, iconFont, iconBrush, iconBoxRect, sfCenter);
            }

            // C) Başlık ve Alt Başlık
            int textX = 56;
            Color titleColor = isSelected ? Color.White : (isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(203, 213, 225));
            using (var titleBrush = new SolidBrush(titleColor))
            {
                g.DrawString(data.Title, titleFont, titleBrush, textX, rowY + 7);
            }

            Color subColor = isSelected ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            using (var subBrush = new SolidBrush(subColor))
            {
                g.DrawString(data.Subtitle, subFont, subBrush, textX, rowY + 24);
            }

            // D) Sağ Taraf: Özellik Rozeti + Key Durum Rozeti
            // 1. Key Rozeti (En sağda)
            int keyBadgeW = 50;
            int keyBadgeH = 18;
            int keyBadgeX = w - 10 - keyBadgeW;
            int keyBadgeY = rowY + 13;
            var keyBadgeRect = new Rectangle(keyBadgeX, keyBadgeY, keyBadgeW, keyBadgeH);

            Color keyBg = hasKey ? Color.FromArgb(6, 78, 59) : Color.FromArgb(69, 26, 3);
            Color keyBorder = hasKey ? Color.FromArgb(16, 185, 129) : Color.FromArgb(217, 119, 6);
            Color keyFg = hasKey ? Color.FromArgb(110, 231, 183) : Color.FromArgb(252, 211, 77);
            string keyText = hasKey ? "Hazır" : "Key";

            if (!hasKey && _hoveredKeyBadgeIndex == i)
            {
                keyBg = Color.FromArgb(120, 53, 15);
                keyText = "Gir";
            }

            using (var keyPath = ModernCardPanel.CreateRoundedRectanglePath(keyBadgeRect, 4))
            {
                using var kbBrush = new SolidBrush(keyBg);
                g.FillPath(kbBrush, keyPath);

                using var kbPen = new Pen(keyBorder, 1f);
                g.DrawPath(kbPen, keyPath);

                // Küçük durum noktası çiz (emojisiz, kusursuz net)
                using var statDotBrush = new SolidBrush(hasKey ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36));
                g.FillEllipse(statDotBrush, keyBadgeX + 6, keyBadgeY + 6, 6, 6);

                using var kfBrush = new SolidBrush(keyFg);
                g.DrawString(keyText, badgeFont, kfBrush, keyBadgeX + 16, keyBadgeY + 2);
            }

            // 2. Özellik Rozeti (Key Rozetinin Solunda)
            int tagW = 40;
            int tagH = 18;
            int tagX = keyBadgeX - tagW - 4;
            int tagY = rowY + 13;
            var tagRect = new Rectangle(tagX, tagY, tagW, tagH);

            using (var tagPath = ModernCardPanel.CreateRoundedRectanglePath(tagRect, 4))
            {
                using var tbBrush = new SolidBrush(data.TagBg);
                g.FillPath(tbBrush, tagPath);

                using var tbPen = new Pen(data.TagBorder, 1f);
                g.DrawPath(tbPen, tagPath);

                using var tfBrush = new SolidBrush(data.TagFg);
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(data.Tag, badgeFont, tfBrush, tagRect, sfCenter);
            }
        }

        // 4. Alt Bilgi Şeridi (Dynamic Footer)
        int footerY = HeaderHeight + (Engines.Length * RowHeight);
        var footerRect = new Rectangle(1, footerY, w - 2, FooterHeight - 1);

        using (var footerBrush = new SolidBrush(Color.FromArgb(20, 29, 47)))
        {
            g.FillRectangle(footerBrush, footerRect);
        }

        using (var footerBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1f))
        {
            g.DrawLine(footerBorderPen, 1, footerY, w - 2, footerY);
        }

        using (var footerFont = new Font("Segoe UI", 7.4F))
        using (var footerTextBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
        using (var accentDotBrush = new SolidBrush(Engines[_selectedIndex].AccentColor))
        {
            g.FillEllipse(accentDotBrush, 12, footerY + 9, 7, 7);
            string footerDesc = Engines[_selectedIndex].SummaryDesc;
            g.DrawString(footerDesc, footerFont, footerTextBrush, 24, footerY + 5);
        }
    }
}
