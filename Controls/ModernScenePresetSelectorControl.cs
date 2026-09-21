namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SimilarProductsWinForms.Services;

/// <summary>
/// Modern, kategorize edilmiş ve şık Etsy Popüler Sahne Şablonu Seçim Bileşeni.
/// </summary>
internal sealed class ModernScenePresetSelectorControl : Panel
{
    public event EventHandler<BackgroundPreset>? PresetSelected;

    private readonly FlowLayoutPanel _pnlCategories;
    private readonly Panel _pnlCardsContainer;
    private readonly ModernScrollPanel _scrollCards;
    private readonly FlowLayoutPanel _flowCards;
    private readonly Label _lblActiveSummary;
    private readonly Button _btnRandom;

    private string _activeCategory = "Tümü";
    private BackgroundPreset? _selectedPreset;
    private readonly List<PresetCardButton> _cardButtons = [];

    private static readonly (string Key, string Label)[] Categories =
    [
        ("Tümü", "Tümü"),
        ("Ev & Yaşam", "🏠 Ev"),
        ("Doğal & Botanik", "🌿 Botanik"),
        ("Lüks & Takı", "🏛️ Lüks"),
        ("Artisan & Rustik", "🪵 Rustik"),
        ("Stüdyo & Katalog", "📦 Stüdyo"),
        ("Trend & Mevsim", "🎨 Trend")
    ];

    public BackgroundPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
            UpdateSelectionStates();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        return new Size(Width > 0 ? Width : 328, 216);
    }

    public ModernScenePresetSelectorControl()
    {
        DoubleBuffered = true;
        Width = 328;
        Height = 216;
        AutoSize = false;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 8);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // 2 satır kategori hapları
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138)); // 3 satır x 2 sütun kart ızgarası
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // Aktif özet şeridi
        Controls.Add(mainLayout);

        // 1. Kategori Hapları (FlowLayoutPanel içinde 2 satır akıcı haplar)
        _pnlCategories = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 1, 0, 2)
        };

        foreach (var (key, label) in Categories)
        {
            var btnCat = new Button
            {
                Text = label,
                AutoSize = true,
                Height = 22,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 1, 4, 3),
                Tag = key
            };
            btnCat.FlatAppearance.BorderSize = 0;
            btnCat.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is string cat)
                {
                    _activeCategory = cat;
                    UpdateCategoryStyles();
                    PopulateCards();
                }
            };
            _pnlCategories.Controls.Add(btnCat);
        }

        // 🎲 Rastgele İlham Butonu (Kategorilerin hemen yanında)
        _btnRandom = new Button
        {
            Text = "🎲 İlham Ver",
            AutoSize = true,
            Height = 22,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 1, 0, 3)
        };
        _btnRandom.FlatAppearance.BorderSize = 0;
        _btnRandom.Click += (_, _) => SelectRandomPreset();
        var tip = new ToolTip();
        tip.SetToolTip(_btnRandom, "Rastgele Popüler Bir Etsy Sahnesi Seç");
        _pnlCategories.Controls.Add(_btnRandom);

        mainLayout.Controls.Add(_pnlCategories, 0, 0);

        // 2. Şablon Kartları Konteyneri (2 Sütunlu Grid - Modern Özel Scroll)
        _pnlCardsContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(0, 2, 0, 4),
            Padding = new Padding(3)
        };
        _pnlCardsContainer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1.2f);
            using var path = ModernCardPanel.CreateRoundedRectanglePath(new Rectangle(0, 0, _pnlCardsContainer.Width - 1, _pnlCardsContainer.Height - 1), 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };

        _scrollCards = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ScrollBarGap = 4,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        _scrollCards.ScrollBar.Width = 6;
        _scrollCards.ScrollBar.TrackColor = Color.Transparent;
        _scrollCards.ScrollBar.ThumbNormalColor = Color.FromArgb(71, 85, 105);
        _scrollCards.ScrollBar.ThumbHoverColor = Color.FromArgb(100, 116, 139);
        _scrollCards.ScrollBar.ThumbActiveColor = Color.FromArgb(99, 102, 241);

        _flowCards = new FlowLayoutPanel
        {
            Width = 300,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = new Padding(2, 2, 2, 2),
            BackColor = Color.FromArgb(15, 23, 42)
        };
        _scrollCards.SetContent(_flowCards);
        _pnlCardsContainer.Controls.Add(_scrollCards);
        mainLayout.Controls.Add(_pnlCardsContainer, 0, 1);

        // 3. Aktif Şablon Bilgi Altlığı
        _lblActiveSummary = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 7.6F),
            ForeColor = Color.FromArgb(148, 163, 184),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "💡 Şablona tıklayarak prompt alanına anında uygulayabilirsiniz.",
            Margin = Padding.Empty
        };
        mainLayout.Controls.Add(_lblActiveSummary, 0, 2);

        UpdateCategoryStyles();
        PopulateCards();
    }

    private void UpdateCategoryStyles()
    {
        foreach (Control c in _pnlCategories.Controls)
        {
            if (c is Button b && b.Tag is string cat)
            {
                bool isActive = string.Equals(cat, _activeCategory, StringComparison.OrdinalIgnoreCase);
                b.BackColor = isActive ? Color.FromArgb(99, 102, 241) : Color.FromArgb(30, 41, 59);
                b.ForeColor = isActive ? Color.White : Color.FromArgb(148, 163, 184);
            }
        }
    }

    private void PopulateCards()
    {
        _flowCards.SuspendLayout();
        _flowCards.Controls.Clear();
        _cardButtons.Clear();

        var filtered = _activeCategory == "Tümü"
            ? PromptTipsService.Presets
            : PromptTipsService.Presets.Where(p => p.Category == _activeCategory).ToList();

        foreach (var preset in filtered)
        {
            var btn = new PresetCardButton(preset)
            {
                Width = 144, // 144 * 2 + 8 margin = 296px, 300px genişliğe 2 sütun tam oturur
                Height = 36,
                Margin = new Padding(2, 2, 2, 2)
            };

            btn.Click += (_, _) =>
            {
                _selectedPreset = preset;
                UpdateSelectionStates();
                _lblActiveSummary.Text = $"✨ Aktif: {preset.Name} ({preset.Vibe})";
                PresetSelected?.Invoke(this, preset);
            };

            _cardButtons.Add(btn);
            _flowCards.Controls.Add(btn);
        }

        UpdateSelectionStates();
        _flowCards.ResumeLayout(true);
        _scrollCards.RecalculateScroll();
    }

    private void UpdateSelectionStates()
    {
        foreach (var btn in _cardButtons)
        {
            btn.IsSelected = _selectedPreset != null &&
                             string.Equals(btn.Preset.Name, _selectedPreset.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SelectRandomPreset()
    {
        if (PromptTipsService.Presets.Count == 0) return;
        var rnd = new Random();
        int idx = rnd.Next(PromptTipsService.Presets.Count);
        var preset = PromptTipsService.Presets[idx];
        _selectedPreset = preset;

        _activeCategory = "Tümü";
        UpdateCategoryStyles();
        PopulateCards();

        _lblActiveSummary.Text = $"🎲 Rastgele: {preset.Name} ({preset.Vibe})";
        PresetSelected?.Invoke(this, preset);
    }

    private sealed class PresetCardButton : Control
    {
        public BackgroundPreset Preset { get; }
        private bool _isSelected;
        private bool _isHovered;

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

        public PresetCardButton(BackgroundPreset preset)
        {
            Preset = preset;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;
            var rect = new Rectangle(0, 0, w - 1, h - 1);

            using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 5);

            Color bg = _isSelected
                ? Color.FromArgb(37, 51, 80)
                : (_isHovered ? Color.FromArgb(30, 41, 59) : Color.FromArgb(20, 29, 45));

            Color border = _isSelected
                ? Color.FromArgb(99, 102, 241)
                : (_isHovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(35, 48, 68));

            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(border, _isSelected ? 1.5f : 1f))
            {
                g.DrawPath(pen, path);
            }

            // Sol İkon Kutusu
            int iconBoxSize = 22;
            int iconBoxX = 6;
            int iconBoxY = (h - iconBoxSize) / 2;
            var iconBoxRect = new Rectangle(iconBoxX, iconBoxY, iconBoxSize, iconBoxSize);

            using (var ibPath = ModernCardPanel.CreateRoundedRectanglePath(iconBoxRect, 4))
            {
                using var ibBrush = new SolidBrush(_isSelected ? Color.FromArgb(79, 70, 229) : Color.FromArgb(30, 41, 59));
                g.FillPath(ibBrush, ibPath);
            }

            using (var iconFont = new Font("Segoe UI Emoji", 8.5F, FontStyle.Regular))
            using (var iconBrush = new SolidBrush(Color.White))
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Preset.Icon, iconFont, iconBrush, iconBoxRect, sfCenter);
            }

            // Sağ Metinler (Başlık + Vibe)
            int textX = iconBoxX + iconBoxSize + 6;
            int textW = w - textX - 4;

            using (var titleFont = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold))
            using (var vibeFont = new Font("Segoe UI", 6.8F))
            using (var titleBrush = new SolidBrush(_isSelected ? Color.White : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(203, 213, 225))))
            using (var vibeBrush = new SolidBrush(_isSelected ? Color.FromArgb(165, 180, 252) : Color.FromArgb(100, 116, 139)))
            {
                g.DrawString(Preset.Name, titleFont, titleBrush, textX, 4);
                string vibeText = string.IsNullOrWhiteSpace(Preset.Vibe) ? Preset.Category : Preset.Vibe;
                g.DrawString(vibeText, vibeFont, vibeBrush, textX, 19);
            }
        }
    }
}
