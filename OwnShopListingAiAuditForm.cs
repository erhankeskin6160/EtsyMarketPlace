namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class OwnShopListingAiAuditForm(
    IAiListingOptimizer aiOptimizer,
    ListingOptimizationHistoryService historyService,
    long? initialListingId = null) : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly PictureBox _pictureBox = new();
    private readonly ModernMultilineTextBox _detailTextBox = new();
    private readonly ModernMultilineTextBox _suggestionTextBox = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly NumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 50 };
    
    // Pagination Fields
    private int _currentPage = 1;
    private int _pageSize = 10;
    private List<AuditRow> _filteredRows = [];
    private readonly Label _lblPageInfo = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _lblTotalInfo = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = UiStyle.TextMuted, TextAlign = ContentAlignment.MiddleRight };
    private readonly Button _btnFirstPage = new();
    private readonly Button _btnPrevPage = new();
    private readonly Button _btnNextPage = new();
    private readonly Button _btnLastPage = new();
    private readonly ComboBox _cboPageSize = new();

    // KPI Labels
    private readonly Label _lblKpiTotal = new() { Text = "0 Ürün", Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblKpiAvgSeo = new() { Text = "0 / 100", Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), ForeColor = UiStyle.SuccessColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblKpiCritical = new() { Text = "0 Ürün", Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), ForeColor = UiStyle.DangerColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblKpiOptimized = new() { Text = "0 Ürün", Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), ForeColor = UiStyle.AccentColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    
    // Before / After Display Labels
    private readonly Label _lblBeforeScore = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), ForeColor = UiStyle.WarningColor };
    private readonly Label _lblAfterScore = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), ForeColor = UiStyle.SuccessColor };
    private readonly Label _lblAiBadge = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.FromArgb(30, 41, 59),
        Padding = new Padding(8, 4, 8, 4),
        Cursor = Cursors.Hand,
        Anchor = AnchorStyles.Right,
        Margin = new Padding(0, 0, 10, 0)
    };
    private string _activeFilter = "ALL";
    
    private List<AuditRow> _rows = [];
    private ListingOptimizationResult? _lastResult;
    private long _lastResultListingId;

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        if (initialListingId is > 0)
        {
            await LoadSingleListingAsync(initialListingId.Value);
        }
    }

    private AuditRow? SelectedRow => _bindingSource.Current as AuditRow;

    private void BuildLayout()
    {
        Text = "Kendi Mağaza Listing AI Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(12, 8, 12, 8) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 0: Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 1: KPI Stat Pills
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 2: Combined Toolbar & Filters
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 3: SplitContainer (Grid + Detail)
        Controls.Add(root);

        // 1. Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🚀 Kendi Mağaza Listing AI Analizi & Optimizasyon",
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _lblAiBadge.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
            UpdateAiBadge();
        };
        UpdateAiBadge();
        header.Controls.Add(_lblAiBadge, 1, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Text = "Listingleri yüklemek için 'Listingleri Yükle'ye basın";
        header.Controls.Add(_statusLabel, 2, 0);
        root.Controls.Add(header, 0, 0);

        // 2. KPI Stat Pills
        root.Controls.Add(BuildKpiStrip(), 0, 1);

        // 3. Combined Toolbar & Filters
        root.Controls.Add(BuildCombinedToolbar(), 0, 2);

        // 4. Responsive SplitContainer
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(30, 41, 59),
            Panel1MinSize = 0,
            Panel2MinSize = 0,
        };

        // Split Panel 1: DataGridView + Pagination
        var gridPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        gridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        gridPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        ConfigureGrid();
        gridPanel.Controls.Add(_grid, 0, 0);
        gridPanel.Controls.Add(BuildPaginationBar(), 0, 1);
        split.Panel1.Controls.Add(gridPanel);

        // Split Panel 2: Detail Area
        split.Panel2.Controls.Add(BuildDetailArea());

        void AdjustSplitter()
        {
            try
            {
                if (split.Height > 100)
                {
                    int target = (int)(split.Height * 0.48);
                    split.SplitterDistance = Math.Clamp(target, 30, Math.Max(40, split.Height - 40));
                }
            }
            catch { }
        }

        Shown += (_, _) => AdjustSplitter();
        split.SizeChanged += (_, _) =>
        {
            if (split.Height > 100 && split.SplitterDistance <= 0)
            {
                AdjustSplitter();
            }
        };

        root.Controls.Add(split, 0, 3);
    }

    private Control BuildKpiStrip()
    {
        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 2)
        };

        strip.Controls.Add(CreateStatPill("📦 Aktif:", _lblKpiTotal, Color.FromArgb(59, 130, 246)));
        strip.Controls.Add(CreateStatPill("🎯 Ort. SEO:", _lblKpiAvgSeo, UiStyle.SuccessColor));
        strip.Controls.Add(CreateStatPill("⚠️ Kritik:", _lblKpiCritical, UiStyle.DangerColor));
        strip.Controls.Add(CreateStatPill("✨ AI Hazır:", _lblKpiOptimized, Color.FromArgb(147, 51, 234)));

        return strip;
    }

    private static Control CreateStatPill(string prefix, Label valueLabel, Color accentColor)
    {
        var pill = new Panel
        {
            Height = 28,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 2, 10, 2),
            BackColor = Color.FromArgb(30, 41, 59),
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var lblPrefix = new Label
        {
            AutoSize = true,
            Text = prefix + " ",
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = accentColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 3, 0, 0)
        };

        valueLabel.AutoSize = true;
        valueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        valueLabel.ForeColor = Color.White;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Margin = new Padding(0, 2, 0, 0);

        flow.Controls.Add(lblPrefix);
        flow.Controls.Add(valueLabel);
        pill.Controls.Add(flow);

        pill.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B), 1.2f);
            var rect = new Rectangle(0, 0, pill.Width - 1, pill.Height - 1);
            using var path = CreateRoundedRectanglePath(rect, 8);
            e.Graphics.DrawPath(pen, path);
        };

        return pill;
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Control BuildCombinedToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0), Margin = new Padding(0, 0, 0, 2) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Sol: Limit + Arama + Filtreler
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Sağ: Aksiyon Butonları

        // 1. Sol: Arama ve Filtre Çipleri
        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var lblLimit = LabelFor("Limit:");
        lblLimit.AutoSize = true;
        lblLimit.Margin = new Padding(0, 6, 4, 0);
        leftFlow.Controls.Add(lblLimit);

        _limitInput.Width = 50;
        _limitInput.Margin = new Padding(0, 3, 8, 0);
        leftFlow.Controls.Add(_limitInput);

        _searchTextBox.Width = 175;
        _searchTextBox.Margin = new Padding(0, 3, 8, 0);
        _searchTextBox.PlaceholderText = "🔍 Ürün veya etiket ara...";
        _searchTextBox.TextChanged += (_, _) => ApplyFilter();
        leftFlow.Controls.Add(_searchTextBox);

        Button CreateChip(string text, string filterKey)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.2F),
                BackColor = _activeFilter == filterKey ? UiStyle.PrimaryColor : Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 4, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) =>
            {
                _activeFilter = filterKey;
                foreach (Control c in leftFlow.Controls)
                {
                    if (c is Button b && b.Tag is string)
                    {
                        b.BackColor = (string)b.Tag == filterKey ? UiStyle.PrimaryColor : Color.FromArgb(30, 41, 59);
                    }
                }
                ApplyFilter();
            };
            btn.Tag = filterKey;
            return btn;
        }

        leftFlow.Controls.Add(CreateChip("Tümü", "ALL"));
        leftFlow.Controls.Add(CreateChip("Düşük SEO (<60)", "LOW_SEO"));
        leftFlow.Controls.Add(CreateChip("Eksik Tag (<13)", "MISSING_TAGS"));
        leftFlow.Controls.Add(CreateChip("Önerisi Hazır", "OPTIMIZED"));
        leftFlow.Controls.Add(CreateChip("0 Favori", "LOW_VIEWS"));

        toolbar.Controls.Add(leftFlow, 0, 0);

        // 2. Sağ: Aksiyon Butonları
        var rightFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var load = CreateButton("🔄 Listingleri Yükle");
        load.Margin = new Padding(0, 2, 4, 0);
        load.Click += async (_, _) => await LoadListingsAsync();
        rightFlow.Controls.Add(load);

        var btnBatchAi = new ModernButtonControl
        {
            Text = "⚡ Tümünü AI ile Denetle",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
            Width = 165,
            Height = 28,
            Margin = new Padding(0, 2, 4, 0)
        };
        btnBatchAi.Click += async (_, _) => await BatchAnalyzeAllAsync();
        rightFlow.Controls.Add(btnBatchAi);

        var aiAnalyze = CreateButton("🎯 AI ile Puanla");
        aiAnalyze.Margin = new Padding(0, 2, 4, 0);
        aiAnalyze.Click += async (_, _) => await AnalyzeSelectedAsync();
        rightFlow.Controls.Add(aiAnalyze);

        var settings = CreateButton("⚙️ AI Ayarları", isSecondary: true);
        settings.Margin = new Padding(0, 2, 4, 0);
        settings.Click += (_, _) => { using var form = new AiOptimizationSettingsForm(); form.ShowDialog(this); UpdateAiBadge(); };
        rightFlow.Controls.Add(settings);

        var close = CreateButton("Kapat", isSecondary: true);
        close.Width = 60;
        close.Margin = new Padding(0, 2, 0, 0);
        close.Click += (_, _) => Close();
        rightFlow.Controls.Add(close);

        toolbar.Controls.Add(rightFlow, 1, 0);

        return toolbar;
    }

    private Control BuildPaginationBar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Padding = new Padding(4, 2, 4, 2),
            BackColor = Color.FromArgb(20, 20, 28),
            Margin = new Padding(0, 3, 0, 3)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Nav buttons (İlk, Önceki)
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Sayfa 1 / 5
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Nav buttons (Sonraki, Son)
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Sayfa Boyutu
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Toplam & Gösterilen

        // Left nav buttons
        var leftNav = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        ConfigureNavButton(_btnFirstPage, "⏮️ İlk", () => GoToPage(1));
        ConfigureNavButton(_btnPrevPage, "◀️ Önceki", () => GoToPage(_currentPage - 1));
        leftNav.Controls.Add(_btnFirstPage);
        leftNav.Controls.Add(_btnPrevPage);
        panel.Controls.Add(leftNav, 0, 0);

        _lblPageInfo.Text = "Sayfa 1 / 1";
        _lblPageInfo.Margin = new Padding(10, 5, 10, 0);
        panel.Controls.Add(_lblPageInfo, 1, 0);

        var rightNav = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        ConfigureNavButton(_btnNextPage, "Sonraki ▶️", () => GoToPage(_currentPage + 1));
        ConfigureNavButton(_btnLastPage, "Son ⏭️", () => GoToPage(TotalPages));
        rightNav.Controls.Add(_btnNextPage);
        rightNav.Controls.Add(_btnLastPage);
        panel.Controls.Add(rightNav, 2, 0);

        var sizePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(16, 0, 0, 0) };
        var lblSize = new Label { Text = "Sayfa Başına:", AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 5, 6, 0) };
        _cboPageSize.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboPageSize.Items.AddRange(new object[] { "10 Ürün", "15 Ürün", "25 Ürün", "50 Ürün" });
        _cboPageSize.SelectedIndex = 0; // 10 Ürün varsayılan
        _cboPageSize.Width = 90;
        _cboPageSize.Height = 26;
        _cboPageSize.BackColor = UiStyle.InputBackground;
        _cboPageSize.ForeColor = UiStyle.TextDark;
        _cboPageSize.SelectedIndexChanged += (_, _) =>
        {
            _pageSize = _cboPageSize.SelectedIndex switch
            {
                0 => 10,
                1 => 15,
                2 => 25,
                3 => 50,
                _ => 10
            };
            GoToPage(1);
        };
        sizePanel.Controls.Add(lblSize);
        sizePanel.Controls.Add(_cboPageSize);
        panel.Controls.Add(sizePanel, 3, 0);

        _lblTotalInfo.Text = "📦 Toplam: 0 listing";
        _lblTotalInfo.Margin = new Padding(0, 5, 8, 0);
        panel.Controls.Add(_lblTotalInfo, 5, 0);

        return panel;
    }

    private static void ConfigureNavButton(Button btn, string text, Action onClick)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.Height = 28;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.FromArgb(30, 41, 59);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(0, 0, 4, 0);
        btn.Click += (_, _) => onClick();
    }

    private Control BuildDetailArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 4, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); // Resim ve Hızlı Butonlar
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));  // Before (Mevcut Durum)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));  // After (AI İyileştirmesi)

        // SOL SÜTUN: Görsel & Aksiyon Butonları
        var leftCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(4),
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5 };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _pictureBox.BackColor = Color.Transparent;
        leftLayout.Controls.Add(_pictureBox, 0, 0);

        var btnPushEtsy = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🚀 Etsy'de Güncelle",
            NormalColor = Color.FromArgb(20, 126, 76),
            HoverColor = Color.FromArgb(26, 150, 90),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold)
        };
        btnPushEtsy.Click += async (_, _) => await UpdateListingWithConfirmationAsync();
        leftLayout.Controls.Add(btnPushEtsy, 0, 1);

        var btnFormatTemplate = CreateButton("📐 Şablonla Paragrafla", isSecondary: false);
        btnFormatTemplate.BackColor = Color.FromArgb(79, 70, 229); // Indigo
        btnFormatTemplate.Click += (_, _) => ApplyTemplateToSelected();
        leftLayout.Controls.Add(btnFormatTemplate, 0, 2);

        var btnCopy = CreateButton("📋 Öneriyi Kopyala");
        btnCopy.Click += (_, _) => CopySuggestion();
        leftLayout.Controls.Add(btnCopy, 0, 3);

        var btnSave = CreateButton("💾 Versiyon Kaydet", isSecondary: true);
        btnSave.Click += async (_, _) => await SaveVersionAsync();
        leftLayout.Controls.Add(btnSave, 0, 4);

        leftCard.Controls.Add(leftLayout);
        layout.Controls.Add(leftCard, 0, 0);

        // ORTA SÜTUN: Mevcut Listing (Before)
        var beforeCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            Padding = new Padding(8, 6, 8, 6),
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        var beforeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        beforeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        beforeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var beforeHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        beforeHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        beforeHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        beforeHeader.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "📌 MEVCUT LİSTİNG (BEFORE)",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _lblBeforeScore.Text = "SEO: --";
        _lblBeforeScore.TextAlign = ContentAlignment.MiddleRight;
        beforeHeader.Controls.Add(_lblBeforeScore, 1, 0);
        beforeLayout.Controls.Add(beforeHeader, 0, 0);

        ConfigureText(_detailTextBox);
        beforeLayout.Controls.Add(_detailTextBox, 0, 1);
        beforeCard.Controls.Add(beforeLayout);
        layout.Controls.Add(beforeCard, 1, 0);

        // SAĞ SÜTUN: AI İyileştirilmiş Listing (After)
        var afterCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            Padding = new Padding(8, 6, 8, 6),
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = Color.FromArgb(100, UiStyle.PrimaryColor.R, UiStyle.PrimaryColor.G, UiStyle.PrimaryColor.B)
        };
        var afterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        afterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        afterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var afterHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        afterHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        afterHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        afterHeader.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "✨ AI İYİLEŞTİRİLMİŞ LİSTİNG (AFTER)",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.PrimaryColor,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _lblAfterScore.Text = "Hedef: --";
        _lblAfterScore.TextAlign = ContentAlignment.MiddleRight;
        afterHeader.Controls.Add(_lblAfterScore, 1, 0);
        afterLayout.Controls.Add(afterHeader, 0, 0);

        ConfigureText(_suggestionTextBox);
        afterLayout.Controls.Add(_suggestionTextBox, 0, 1);
        afterCard.Controls.Add(afterLayout);
        layout.Controls.Add(afterCard, 2, 0);

        return layout;
    }

    private void UpdateKpis()
    {
        int total = _rows.Count;
        _lblKpiTotal.Text = $"{total} Ürün";

        if (total == 0)
        {
            _lblKpiAvgSeo.Text = "0 / 100";
            _lblKpiCritical.Text = "0 Ürün";
            _lblKpiOptimized.Text = "0 Ürün";
            return;
        }

        double avgSeo = Math.Round(_rows.Average(r => r.SeoScore), 0);
        _lblKpiAvgSeo.Text = $"{avgSeo} / 100";
        _lblKpiAvgSeo.ForeColor = avgSeo >= 75 ? UiStyle.SuccessColor : (avgSeo >= 55 ? UiStyle.WarningColor : UiStyle.DangerColor);

        int critical = _rows.Count(r => r.SeoScore < 60 || r.TagCount < 13);
        _lblKpiCritical.Text = $"{critical} Ürün";

        int optimized = _rows.Count(r => r.AiScore > 0 || r.Status == "Oneri hazir" || r.Status == "Etsy guncellendi");
        _lblKpiOptimized.Text = $"{optimized} Ürün";
    }

    private void SetBusy(bool busy, string? statusText = null)
    {
        if (statusText != null)
        {
            _statusLabel.Text = statusText;
        }

        UseWaitCursor = false;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        Cursor.Current = busy ? Cursors.WaitCursor : Cursors.Default;

        if (!busy)
        {
            if (ParentForm != null)
            {
                ParentForm.UseWaitCursor = false;
                ParentForm.Cursor = Cursors.Default;
            }
            if (TopLevelControl is Form topForm)
            {
                topForm.UseWaitCursor = false;
                topForm.Cursor = Cursors.Default;
            }
        }
    }

    private async Task BatchAnalyzeAllAsync()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "Önce 'Listingleri Yükle' butonuna basarak mağaza ürünlerinizi çekin.", "Toplu AI Denetimi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            SetBusy(true, $"Mağazadaki {_rows.Count} ürün sırayla AI ile denetleniyor...");

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                _statusLabel.Text = $"[{i + 1}/{_rows.Count}] '{row.Title}' analiz ediliyor...";
                try
                {
                    var input = ToOptimizationInput(row.Listing);
                    var result = await aiOptimizer.OptimizeAsync(input);
                    row.AiScore = result.OptimizedSeoScore;
                    row.Status = result.RiskWarnings.Count > 0 ? "Risk kontrol" : "Oneri hazir";
                    
                    if (ReferenceEquals(row, SelectedRow))
                    {
                        _lastResult = result;
                        _lastResultListingId = row.Listing.ListingId;
                        RenderSuggestion(row, result);
                    }
                }
                catch
                {
                    // Diğer ürünlerle devam et
                }
            }

            _grid.Refresh();
            UpdateKpis();
            RenderCurrentPage();
            _statusLabel.Text = $"✅ Tüm mağaza ({_rows.Count} ürün) başarıyla denetlendi!";
            MessageBox.Show(this, "Tüm mağaza ürünleriniz başarıyla analiz edildi! Önerileri inceleyip tek tıkla güncelleyebilirsiniz.", "Toplu AI Denetimi Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        try
        {
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, _grid, new object[] { true });
        }
        catch { }

        _grid.RowTemplate.MinimumHeight = 70;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += async (_, _) => await UpdateDetailAsync();
        _grid.CellDoubleClick += (_, _) => OpenListing();

        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var colName = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (colName == nameof(AuditRow.SeoScore))
            {
                if (e.CellStyle != null && e.Value is int score)
                {
                    e.CellStyle.ForeColor = score >= 75 ? UiStyle.SuccessColor : (score >= 55 ? UiStyle.WarningColor : UiStyle.DangerColor);
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
            else if (colName == nameof(AuditRow.AiScore))
            {
                if (e.CellStyle != null && e.Value is int aiScore && aiScore > 0)
                {
                    e.CellStyle.ForeColor = UiStyle.SuccessColor;
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
            else if (colName == nameof(AuditRow.TagCount))
            {
                if (e.CellStyle != null && e.Value is int tags && tags < 13)
                {
                    e.CellStyle.ForeColor = UiStyle.DangerColor;
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
        };

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Görsel",
            DataPropertyName = nameof(AuditRow.ThumbnailImage),
            Width = 65,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });
        AddColumn("#", nameof(AuditRow.Rank), 40);
        AddColumn("Listing Başlığı", nameof(AuditRow.Title), 320, true);
        AddColumn("SEO", nameof(AuditRow.SeoScore), 65);
        AddColumn("AI", nameof(AuditRow.AiScore), 65);
        AddColumn("Tag", nameof(AuditRow.TagCount), 55);
        AddColumn("Fiyat", nameof(AuditRow.Price), 75);
        AddColumn("Favori", nameof(AuditRow.Favorites), 65);
        AddColumn("Stok", nameof(AuditRow.Quantity), 55);
        AddColumn("Durum", nameof(AuditRow.Status), 120);
    }

    private async Task LoadListingsAsync()
    {
        try
        {
            SetBusy(true, "Kendi listingleriniz Etsy'den çekiliyor...");
            var settings = EtsyApiSettingsStore.Load();
            var listings = await _apiClient.GetOwnShopActiveListingsAsync(settings, (int)_limitInput.Value);
            EtsyApiSettingsStore.Save(settings);
            _rows = listings
                .Select((item, index) => new AuditRow(index + 1, item, ScoreListing(item), "Bekliyor"))
                .OrderBy(row => row.SeoScore)
                .ThenBy(row => row.Title)
                .ToList();
            
            UpdateKpis();
            ApplyFilter();
            _statusLabel.Text = $"{_rows.Count} listing yüklendi | Düşük SEO puanları üstte";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kendi Listing AI Analizi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Listingler alınamadı";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_filteredRows.Count / (double)_pageSize));

    private void GoToPage(int page)
    {
        if (page < 1) page = 1;
        if (page > TotalPages) page = TotalPages;
        _currentPage = page;

        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        int total = _filteredRows.Count;
        int totalPages = TotalPages;
        if (_currentPage > totalPages) _currentPage = totalPages;
        if (_currentPage < 1) _currentPage = 1;

        var pageItems = _filteredRows
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();

        _bindingSource.DataSource = pageItems;
        _bindingSource.ResetBindings(false);
        _grid.Invalidate();

        // Update Nav Buttons
        _btnFirstPage.Enabled = _currentPage > 1;
        _btnPrevPage.Enabled = _currentPage > 1;
        _btnNextPage.Enabled = _currentPage < totalPages;
        _btnLastPage.Enabled = _currentPage < totalPages;

        _btnFirstPage.BackColor = _btnFirstPage.Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(20, 25, 35);
        _btnPrevPage.BackColor = _btnPrevPage.Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(20, 25, 35);
        _btnNextPage.BackColor = _btnNextPage.Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(20, 25, 35);
        _btnLastPage.BackColor = _btnLastPage.Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(20, 25, 35);

        _lblPageInfo.Text = $"Sayfa {_currentPage} / {totalPages}";
        int startItem = total == 0 ? 0 : ((_currentPage - 1) * _pageSize + 1);
        int endItem = Math.Min(_currentPage * _pageSize, total);
        _lblTotalInfo.Text = $"📦 Gösterilen: {startItem} - {endItem} / {total} listing (Toplam {_rows.Count})";

        // Yalnızca geçerli sayfadaki resimleri indir (Lazy Loading)
        var currentSettings = EtsyApiSettingsStore.Load();
        _ = LoadThumbnailsForPageAsync(pageItems, currentSettings);
    }

    private void ApplyFilter()
    {
        var text = _searchTextBox.Text.Trim().ToLowerInvariant();
        var filtered = _rows.AsEnumerable();

        // 1. Text Search Filter
        if (!string.IsNullOrWhiteSpace(text))
        {
            filtered = filtered.Where(r => 
                r.Title.ToLowerInvariant().Contains(text) || 
                r.Listing.Tags.Any(t => t.ToLowerInvariant().Contains(text)));
        }

        // 2. Chip Filter
        switch (_activeFilter)
        {
            case "LOW_SEO":
                filtered = filtered.Where(r => r.SeoScore < 60);
                break;
            case "MISSING_TAGS":
                filtered = filtered.Where(r => r.TagCount < 13);
                break;
            case "OPTIMIZED":
                filtered = filtered.Where(r => r.AiScore > 0 || r.Status == "Oneri hazir" || r.Status == "Etsy guncellendi");
                break;
            case "LOW_VIEWS":
                filtered = filtered.Where(r => r.Favorites == 0);
                break;
        }

        _filteredRows = filtered.ToList();
        _currentPage = 1;
        RenderCurrentPage();
    }

    private async Task AnalyzeSelectedAsync()
    {
        if (SelectedRow is null) return;
        try
        {
            SetBusy(true);
            var row = SelectedRow;
            var input = ToOptimizationInput(row.Listing);
            _lastResult = await aiOptimizer.OptimizeAsync(input);
            _lastResultListingId = row.Listing.ListingId;
            row.AiScore = _lastResult.OptimizedSeoScore;
            row.Status = _lastResult.RiskWarnings.Count > 0 ? "Risk kontrol" : "Oneri hazir";
            _grid.Refresh();
            UpdateKpis();
            RenderSuggestion(row, _lastResult);
            _statusLabel.Text = $"'{row.Title}' başarıyla analiz edildi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI Analiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveVersionAsync()
    {
        if (SelectedRow is null || _lastResult is null) return;
        var row = SelectedRow;
        await historyService.SaveAsync(new SaveListingOptimizationHistory(
            row.Listing.ListingId.ToString(CultureInfo.InvariantCulture),
            row.Title,
            PrimaryKeyword(row.Listing),
            _lastResult));
        _statusLabel.Text = "Optimizasyon versiyonu geçmişe kaydedildi";
        MessageBox.Show(this, "Optimizasyon versiyonu başarıyla kaydedildi!", "Versiyon Kaydı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task UpdateDetailAsync()
    {
        if (SelectedRow is null)
        {
            _detailTextBox.Text = "Listing seçilmedi.";
            _suggestionTextBox.Clear();
            _lblBeforeScore.Text = "SEO: --";
            _lblAfterScore.Text = "Hedef: --";
            _pictureBox.Image = null;
            return;
        }

        var row = SelectedRow;
        _lblBeforeScore.Text = $"SEO: {row.SeoScore}/100" + (row.SeoScore >= 75 ? " (İyi)" : (row.SeoScore >= 55 ? " (Orta)" : " (Kritik)"));
        _lblBeforeScore.ForeColor = row.SeoScore >= 75 ? UiStyle.SuccessColor : (row.SeoScore >= 55 ? UiStyle.WarningColor : UiStyle.DangerColor);

        _detailTextBox.Text =
            $"[MEVCUT BAŞLIK - {row.Title.Length}/140 Karakter]{Environment.NewLine}{row.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"[MEVCUT TAGLER - {row.TagCount}/13 Tag]{Environment.NewLine}{string.Join(", ", row.Listing.Tags)}{Environment.NewLine}{Environment.NewLine}" +
            $"[PUAN & METRİKLER]{Environment.NewLine}SEO Puanı: {row.SeoScore}/100 | Fiyat: {row.Price} | Favori: {row.Favorites:N0} | Stok: {row.Quantity}{Environment.NewLine}{Environment.NewLine}" +
            $"[TESPİT EDİLEN EKSİKLER]{Environment.NewLine}{row.SeoNeeds}";

        if (_lastResult != null && _lastResultListingId == row.Listing.ListingId)
        {
            RenderSuggestion(row, _lastResult);
        }
        else
        {
            _lblAfterScore.Text = "Hedef: Bekleniyor...";
            _lblAfterScore.ForeColor = UiStyle.TextMuted;
            _suggestionTextBox.Text = "► 'AI ile Puanla' butonuna bastığınızda yapay zeka bu listing için 140 karakterlik kusursuz SEO başlığı, 13 adet long-tail tag ve optimize açıklama üretecektir.";
        }

        if (row.ThumbnailImage is not null)
        {
            _pictureBox.Image = row.ThumbnailImage;
        }
        else
        {
            var settings = EtsyApiSettingsStore.Load();
            await LoadThumbnailAsync(row, settings);
        }
    }

    private async Task LoadThumbnailsForPageAsync(List<AuditRow> pageItems, EtsyApiSettings settings)
    {
        var needed = pageItems.Where(item => item.ThumbnailImage is null).ToList();
        if (needed.Count == 0) return;

        foreach (var row in needed)
        {
            await LoadThumbnailAsync(row, settings);
        }
        _grid.Invalidate();
    }

    private async Task LoadThumbnailAsync(AuditRow row, EtsyApiSettings settings)
    {
        var imageUrl = row.Listing.ImageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl) && row.Listing.ListingId > 0)
        {
            try
            {
                var urls = await _apiClient.GetListingImagesAsync(settings, row.Listing.ListingId);
                row.Listing.ImageUrls = urls.ToList();
                imageUrl = urls.FirstOrDefault() ?? "";
            }
            catch
            {
                imageUrl = "";
            }
        }

        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        try
        {
            await using var stream = await _imageHttpClient.GetStreamAsync(imageUrl);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            using var image = Image.FromStream(memory);
            row.ThumbnailImage = new Bitmap(image);
            if (ReferenceEquals(row, SelectedRow)) _pictureBox.Image = row.ThumbnailImage;
            _grid.Invalidate();
        }
        catch
        {
            row.ThumbnailImage = null;
        }
    }

    private void UpdateAiBadge()
    {
        var settings = AiOptimizationSettingsStore.Load();
        _lblAiBadge.Text = settings.GetActiveBadgeText();
        _lblAiBadge.BackColor = settings.UseOpenAi
            ? Color.FromArgb(16, 80, 50)
            : (settings.UseGemini ? Color.FromArgb(20, 60, 120) : Color.FromArgb(40, 50, 65));
    }

    private void RenderSuggestion(AuditRow row, ListingOptimizationResult result)
    {
        int optScore = result.OptimizedSeoScore > 0 ? result.OptimizedSeoScore : 95;
        int diff = optScore - row.SeoScore;
        string diffStr = diff > 0 ? $"(+{diff} Puan Artış)" : "";
        _lblAfterScore.Text = $"Hedef: {optScore}/100 {diffStr}";
        _lblAfterScore.ForeColor = UiStyle.SuccessColor;

        var aiSettings = AiOptimizationSettingsStore.Load();
        string engineName = aiSettings.GetActiveEngineName();
        string optTitle = result.TitleSuggestions.FirstOrDefault() ?? row.Title;
        var optTags = result.TagSuggestions.Take(13).ToList();
        var normalizedDesc = EtsyMarketPlace.Application.ListingOptimization.EtsyDescriptionFormatter.NormalizeForEtsy(result.DescriptionDraft);

        _suggestionTextBox.Text =
            $"[AI İLE OPTİMİZE EDİLMİŞ BAŞLIK - {optTitle.Length}/140 Karakter]  (Aktif Motor: {engineName}){Environment.NewLine}{optTitle}{Environment.NewLine}{Environment.NewLine}" +
            $"[ÖNERİLEN 13 LONG-TAIL TAG - {optTags.Count}/13 Tag]{Environment.NewLine}{string.Join(", ", optTags)}{Environment.NewLine}{Environment.NewLine}" +
            $"[ÖNERİLEN MATERYALLER]{Environment.NewLine}{string.Join(", ", result.MaterialSuggestions)}{Environment.NewLine}{Environment.NewLine}" +
            $"[SATIŞ ODAKLI AÇIKLAMA]{Environment.NewLine}{normalizedDesc}{Environment.NewLine}{Environment.NewLine}" +
            $"[RİSK VE KURAL UYARILARI]{Environment.NewLine}{string.Join(Environment.NewLine, result.RiskWarnings.DefaultIfEmpty("Risk veya kural ihlali bulunamadı."))}";
    }

    private void ApplyTemplateToSelected()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Lütfen önce tablodan bir listing seçin.", "Şablon Uygula", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = SelectedRow;
        string sourceDesc = _lastResult?.DescriptionDraft ?? row.Listing.Description;
        var materials = _lastResult?.MaterialSuggestions ?? [];
        var formatted = EtsyMarketPlace.Application.ListingOptimization.EtsyDescriptionFormatter.FormatToStandardTemplate(
            sourceDesc,
            row.Title,
            row.Listing.Tags,
            materials,
            PrimaryKeyword(row.Listing));

        if (_lastResult is null || _lastResultListingId != row.Listing.ListingId)
        {
            _lastResult = new ListingOptimizationResult(
                row.SeoScore,
                Math.Max(85, row.SeoScore + 15),
                [row.Title],
                row.Listing.Tags,
                materials,
                formatted,
                [],
                [],
                []);
            _lastResultListingId = row.Listing.ListingId;
        }
        else
        {
            _lastResult = _lastResult with { DescriptionDraft = formatted };
        }

        RenderSuggestion(row, _lastResult);
        _statusLabel.Text = $"'{row.Title}' açıklaması standart Etsy paragraf şablonuna dönüştürüldü!";
    }

    private static int ScoreListing(MarketListingResult listing)
    {
        var tagScore = Math.Min(25, listing.Tags.Count * 25 / 13);
        var titleScore = listing.Title.Length is >= 55 and <= 135 ? 25 : listing.Title.Length is >= 35 and <= 140 ? 18 : 8;
        var imageScore = listing.ImageUrls.Count >= 5 ? 20 : listing.ImageUrls.Count * 4;
        var descriptionScore = listing.Description.Length >= 500 ? 20 : listing.Description.Length >= 250 ? 12 : 5;
        var signalScore = listing.Favorites > 0 ? 10 : 0;
        return Math.Clamp(tagScore + titleScore + imageScore + descriptionScore + signalScore, 0, 100);
    }

    private static ListingOptimizationInput ToOptimizationInput(MarketListingResult listing) =>
        new(listing.Title, listing.Description, listing.Tags, PrimaryKeyword(listing));

    private static string PrimaryKeyword(MarketListingResult listing)
    {
        var multiWordTag = listing.Tags.FirstOrDefault(tag => tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
        if (!string.IsNullOrWhiteSpace(multiWordTag)) return multiWordTag;

        if (listing.Tags.Count > 0 && !string.IsNullOrWhiteSpace(listing.Tags[0])) return listing.Tags[0];

        var mainTitlePart = listing.Title.Split(['|', '-', ',', '–', '—', '/'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();

        return !string.IsNullOrWhiteSpace(mainTitlePart) && mainTitlePart.Length >= 4
            ? mainTitlePart
            : listing.Title;
    }

    private void CopySuggestion()
    {
        if (!string.IsNullOrWhiteSpace(_suggestionTextBox.Text)) Clipboard.SetText(_suggestionTextBox.Text);
    }

    private async Task UpdateListingWithConfirmationAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_lastResult is null || _lastResultListingId != SelectedRow.Listing.ListingId)
        {
            MessageBox.Show(this, "Once secili listing icin AI ile Puanla calistirin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var update = CreateListingUpdate(_lastResult);
        if (!ValidateListingUpdate(update, out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var confirmation = new ListingUpdateConfirmationForm(SelectedRow.Listing, update);
        if (confirmation.ShowDialog(this) != DialogResult.OK || !confirmation.Confirmed)
        {
            return;
        }

        try
        {
            SetBusy(true);
            var settings = EtsyApiSettingsStore.Load();
            await _apiClient.UpdateOwnShopListingTextAsync(settings, SelectedRow.Listing.ListingId, update);
            EtsyApiSettingsStore.Save(settings);
            await SaveVersionAsync();
            SelectedRow.Status = "Etsy guncellendi";
            _grid.Refresh();
            _statusLabel.Text = $"Listing Etsy'de guncellendi: {SelectedRow.Title}";
            MessageBox.Show(this, "Listing Etsy'de guncellendi. Degisikligi Etsy sayfasinda kontrol edin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static ListingTextUpdate CreateListingUpdate(ListingOptimizationResult result) =>
        new(
            result.TitleSuggestions.FirstOrDefault()?.Trim() ?? "",
            EtsyMarketPlace.Application.ListingOptimization.EtsyDescriptionFormatter.NormalizeForEtsy(result.DescriptionDraft),
            result.TagSuggestions.Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Take(13).ToList(),
            EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions));

    private static bool ValidateListingUpdate(ListingTextUpdate update, out string message)
    {
        if (string.IsNullOrWhiteSpace(update.Title))
        {
            message = "AI onerisi baslik uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        if (update.Title.Length > 140)
        {
            message = "Baslik 140 karakterden uzun. Etsy kabul etmeyebilir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(update.Description))
        {
            message = "AI onerisi aciklama uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        if (update.Tags.Count == 0)
        {
            message = "AI onerisi tag uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        var longTag = update.Tags.FirstOrDefault(tag => tag.Length > 20);
        if (longTag is not null)
        {
            message = $"Tag 20 karakterden uzun: {longTag}";
            return false;
        }

        var longMaterial = update.Materials?.FirstOrDefault(material => material.Length > 45);
        if (longMaterial is not null)
        {
            message = $"Materyal 45 karakterden uzun: {longMaterial}";
            return false;
        }

        message = "";
        return true;
    }

    private async Task RefreshSelectedListingAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "Rev", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await LoadSingleListingAsync(SelectedRow.Listing.ListingId);
    }

    private async Task LoadSingleListingAsync(long listingId)
    {
        try
        {
            SetBusy(true, "Listing Etsy'den en guncel haliyle aliniyor...");
            var settings = EtsyApiSettingsStore.Load();
            var refreshed = await _apiClient.GetOwnShopListingAsync(settings, listingId);
            EtsyApiSettingsStore.Save(settings);
            var index = _rows.FindIndex(row => row.Listing.ListingId == listingId);
            if (index >= 0)
            {
                _rows[index] = new AuditRow(_rows[index].Rank, refreshed, ScoreListing(refreshed), "Yenilendi");
            }
            else
            {
                _rows.Insert(0, new AuditRow(1, refreshed, ScoreListing(refreshed), "Yenilendi"));
            }

            ApplyFilter();
            var row = _rows.FirstOrDefault(item => item.Listing.ListingId == listingId);
            if (row is not null)
            {
                _bindingSource.Position = Math.Max(0, (_bindingSource.DataSource as List<AuditRow>)?.FindIndex(item => item.Listing.ListingId == listingId) ?? 0);
                await LoadThumbnailAsync(row, settings);
            }

            _lastResult = null;
            _lastResultListingId = 0;
            _statusLabel.Text = "Rev tamamlandi: listing Etsy'den en guncel haliyle yuklendi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Rev", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Rev islemi basarisiz";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task OpenAiImageWorkflowAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new AiListingImageForm(SelectedRow.Listing, _apiClient);
        form.ShowDialog(this);
        await RefreshSelectedListingAsync();
    }

    private void OpenListing() => OpenUrl(SelectedRow?.Listing.ListingUrl);
    private void OpenShop() => OpenUrl(SelectedRow?.Listing.ShopUrl);

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void AddColumn(string header, string property, int width, bool fill = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });
    }

    private static void ConfigureText(ModernMultilineTextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        textBox.Font = new Font("Segoe UI", 9F);
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = isSecondary ? Color.FromArgb(47, 58, 77) : Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8.8F),
            Margin = new Padding(3),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void OpenHealthScore()
    {
        var row = SelectedRow;
        if (row is null)
        {
            MessageBox.Show(this, "Sağlık skoru hesaplamak için bir ürün seçin.", "Sağlık Skoru", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ListingHealthScoreForm(row.Listing, aiOptimizer, historyService);
        form.ShowDialog(this);
    }

    private sealed class AuditRow(int rank, MarketListingResult listing, int seoScore, string status)
    {
        public int Rank { get; } = rank;
        public MarketListingResult Listing { get; } = listing;
        public Image? ThumbnailImage { get; set; }
        public string Title => Listing.Title;
        public string Price => Listing.PriceDisplay;
        public int Favorites => Listing.Favorites;
        public int Quantity => Listing.Quantity;
        public int TagCount => Listing.Tags.Count;
        public int SeoScore { get; } = seoScore;
        public int AiScore { get; set; } = seoScore;
        public string Status { get; set; } = status;
        public string SeoNeeds => BuildSeoNeeds(Listing);
        public string SeoStrengths => BuildSeoStrengths(Listing);

        private static string BuildSeoNeeds(MarketListingResult listing)
        {
            var needs = new List<string>();
            if (listing.Tags.Count < 13) needs.Add($"{13 - listing.Tags.Count} tag eksik");
            if (listing.Title.Length < 55) needs.Add("baslik kisa");
            if (listing.Title.Length > 140) needs.Add("baslik uzun");
            if (listing.Description.Length < 500) needs.Add("aciklama kisa");
            if (listing.ImageUrls.Count < 5) needs.Add("gorsel az");
            return needs.Count == 0 ? "Temel eksik yok" : string.Join(", ", needs);
        }

        private static string BuildSeoStrengths(MarketListingResult listing)
        {
            var strengths = new List<string>();
            if (listing.Tags.Count >= 13) strengths.Add("13 tag");
            if (listing.Title.Length is >= 55 and <= 135) strengths.Add("baslik iyi");
            if (listing.Description.Length >= 500) strengths.Add("aciklama iyi");
            if (listing.ImageUrls.Count >= 5) strengths.Add("gorsel iyi");
            return strengths.Count == 0 ? "-" : string.Join(", ", strengths);
        }
    }
}
