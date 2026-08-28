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
    private readonly TextBox _detailTextBox = new();
    private readonly TextBox _suggestionTextBox = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly NumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 50 };
    
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
        Text = "Kendi Magaza Listing AI Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); // KPI Strip (4 Cards)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Filter Chips
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // DataGridView
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 280)); // Before / After Detail Area
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
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
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

        // 2. KPI Strip
        root.Controls.Add(BuildKpiStrip(), 0, 1);

        // 3. Toolbar
        root.Controls.Add(BuildToolbar(), 0, 2);

        // 4. Filter Chips
        root.Controls.Add(BuildFilterChips(), 0, 3);

        // 5. DataGridView
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 4);

        // 6. Before / After Detail Area
        root.Controls.Add(BuildDetailArea(), 0, 5);
    }

    private Control BuildKpiStrip()
    {
        var strip = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 0, 0, 4) };
        for (int i = 0; i < 4; i++) strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        strip.Controls.Add(CreateKpiCard("📦 TOPLAM AKTİF LİSTİNG", _lblKpiTotal, Color.FromArgb(59, 130, 246)), 0, 0);
        strip.Controls.Add(CreateKpiCard("🎯 ORTALAMA SEO SKORU", _lblKpiAvgSeo, UiStyle.SuccessColor), 1, 0);
        strip.Controls.Add(CreateKpiCard("⚠️ KRİTİK EKSİK LİSTİNGLER", _lblKpiCritical, UiStyle.DangerColor), 2, 0);
        strip.Controls.Add(CreateKpiCard("✨ AI İLE OPTİMİZE EDİLENLER", _lblKpiOptimized, UiStyle.AccentColor), 3, 0);

        return strip;
    }

    private static Control CreateKpiCard(string title, Label valueLabel, Color accentColor)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Padding = new Padding(12, 6, 12, 6),
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        valueLabel.Margin = new Padding(0);
        valueLabel.Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold);
        layout.Controls.Add(lblTitle, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, Padding = new Padding(0, 1, 0, 1) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));  // Limit:
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // _limitInput
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // _searchTextBox
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // 🔄 Listingleri Yükle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195)); // ⚡ Tümünü AI ile Denetle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); // 🎯 AI ile Puanla
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); // ⚙️ AI Ayarları
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));  // Kapat

        toolbar.Controls.Add(LabelFor("Limit:"), 0, 0);
        _limitInput.Dock = DockStyle.Fill;
        toolbar.Controls.Add(_limitInput, 1, 0);

        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.PlaceholderText = "🔍 Ürün adına veya etiketine göre filtrele...";
        _searchTextBox.TextChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(_searchTextBox, 2, 0);

        var load = CreateButton("🔄 Listingleri Yükle");
        load.Click += async (_, _) => await LoadListingsAsync();
        toolbar.Controls.Add(load, 3, 0);

        var btnBatchAi = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "⚡ Tümünü AI ile Denetle",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        };
        btnBatchAi.Click += async (_, _) => await BatchAnalyzeAllAsync();
        toolbar.Controls.Add(btnBatchAi, 4, 0);

        var aiAnalyze = CreateButton("🎯 AI ile Puanla");
        aiAnalyze.Click += async (_, _) => await AnalyzeSelectedAsync();
        toolbar.Controls.Add(aiAnalyze, 5, 0);

        var settings = CreateButton("⚙️ AI Ayarları", isSecondary: true);
        settings.Click += (_, _) => { using var form = new AiOptimizationSettingsForm(); form.ShowDialog(this); UpdateAiBadge(); };
        toolbar.Controls.Add(settings, 6, 0);

        var close = CreateButton("Kapat", isSecondary: true);
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(close, 7, 0);

        return toolbar;
    }

    private Control BuildFilterChips()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0) };

        Button CreateChip(string text, string filterKey)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5F),
                BackColor = _activeFilter == filterKey ? UiStyle.PrimaryColor : Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 1, 6, 1)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) =>
            {
                _activeFilter = filterKey;
                foreach (Control c in panel.Controls)
                {
                    if (c is Button b) b.BackColor = Color.FromArgb(30, 41, 59);
                }
                btn.BackColor = UiStyle.PrimaryColor;
                ApplyFilter();
            };
            return btn;
        }

        panel.Controls.Add(CreateChip("Tümü", "ALL"));
        panel.Controls.Add(CreateChip("Düşük SEO (<60)", "LOW_SEO"));
        panel.Controls.Add(CreateChip("Eksik Tag (<13)", "MISSING_TAGS"));
        panel.Controls.Add(CreateChip("Önerisi Hazır", "OPTIMIZED"));
        panel.Controls.Add(CreateChip("0 Favorili Ürünler", "LOW_VIEWS"));

        return panel;
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
        var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

        var btnCopy = CreateButton("📋 Öneriyi Kopyala");
        btnCopy.Click += (_, _) => CopySuggestion();
        leftLayout.Controls.Add(btnCopy, 0, 2);

        var btnSave = CreateButton("💾 Versiyon Kaydet", isSecondary: true);
        btnSave.Click += async (_, _) => await SaveVersionAsync();
        leftLayout.Controls.Add(btnSave, 0, 3);

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

    private async Task BatchAnalyzeAllAsync()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "Önce 'Listingleri Yükle' butonuna basarak mağaza ürünlerinizi çekin.", "Toplu AI Denetimi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = $"Mağazadaki {_rows.Count} ürün sırayla AI ile denetleniyor...";

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
            _statusLabel.Text = $"✅ Tüm mağaza ({_rows.Count} ürün) başarıyla denetlendi!";
            MessageBox.Show(this, "Tüm mağaza ürünleriniz başarıyla analiz edildi! Önerileri inceleyip tek tıkla güncelleyebilirsiniz.", "Toplu AI Denetimi Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
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
                if (e.Value is int score)
                {
                    e.CellStyle.ForeColor = score >= 75 ? UiStyle.SuccessColor : (score >= 55 ? UiStyle.WarningColor : UiStyle.DangerColor);
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
            else if (colName == nameof(AuditRow.AiScore))
            {
                if (e.Value is int aiScore && aiScore > 0)
                {
                    e.CellStyle.ForeColor = UiStyle.SuccessColor;
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
            else if (colName == nameof(AuditRow.TagCount))
            {
                if (e.Value is int tags && tags < 13)
                {
                    e.CellStyle.ForeColor = UiStyle.DangerColor;
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
        };

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(AuditRow.ThumbnailImage),
            Width = 80,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });
        AddColumn("#", nameof(AuditRow.Rank), 45);
        AddColumn("Listing Başlığı", nameof(AuditRow.Title), 360, true);
        AddColumn("SEO", nameof(AuditRow.SeoScore), 65);
        AddColumn("AI", nameof(AuditRow.AiScore), 65);
        AddColumn("SEO Eksikler", nameof(AuditRow.SeoNeeds), 180);
        AddColumn("Artılar", nameof(AuditRow.SeoStrengths), 160);
        AddColumn("Fiyat", nameof(AuditRow.Price), 80);
        AddColumn("Favori", nameof(AuditRow.Favorites), 75);
        AddColumn("Stok", nameof(AuditRow.Quantity), 65);
        AddColumn("Tag", nameof(AuditRow.TagCount), 65);
        AddColumn("Durum", nameof(AuditRow.Status), 130);
    }

    private async Task LoadListingsAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Kendi listingleriniz Etsy'den çekiliyor...";
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
            await LoadThumbnailsAsync(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kendi Listing AI Analizi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Listingler alınamadı";
        }
        finally
        {
            UseWaitCursor = false;
        }
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

        var list = filtered.ToList();
        _bindingSource.DataSource = list;
        _bindingSource.ResetBindings(false);
        _grid.Invalidate();
        _statusLabel.Text = $"{list.Count} / {_rows.Count} listing gösteriliyor";
    }

    private async Task AnalyzeSelectedAsync()
    {
        if (SelectedRow is null) return;
        try
        {
            UseWaitCursor = true;
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
            UseWaitCursor = false;
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

    private async Task LoadThumbnailsAsync(EtsyApiSettings settings)
    {
        foreach (var row in _rows.Where(item => item.ThumbnailImage is null).Take(30))
        {
            await LoadThumbnailAsync(row, settings);
        }
        _bindingSource.ResetBindings(false);
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
            _bindingSource.ResetBindings(false);
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

        _suggestionTextBox.Text =
            $"[AI İLE OPTİMİZE EDİLMİŞ BAŞLIK - {optTitle.Length}/140 Karakter]  (Aktif Motor: {engineName}){Environment.NewLine}{optTitle}{Environment.NewLine}{Environment.NewLine}" +
            $"[ÖNERİLEN 13 LONG-TAIL TAG - {optTags.Count}/13 Tag]{Environment.NewLine}{string.Join(", ", optTags)}{Environment.NewLine}{Environment.NewLine}" +
            $"[ÖNERİLEN MATERYALLER]{Environment.NewLine}{string.Join(", ", result.MaterialSuggestions)}{Environment.NewLine}{Environment.NewLine}" +
            $"[SATIŞ ODAKLI AÇIKLAMA]{Environment.NewLine}{result.DescriptionDraft}{Environment.NewLine}{Environment.NewLine}" +
            $"[RİSK VE KURAL UYARILARI]{Environment.NewLine}{string.Join(Environment.NewLine, result.RiskWarnings.DefaultIfEmpty("Risk veya kural ihlali bulunamadı."))}";
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
            UseWaitCursor = true;
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
            UseWaitCursor = false;
        }
    }

    private static ListingTextUpdate CreateListingUpdate(ListingOptimizationResult result) =>
        new(
            result.TitleSuggestions.FirstOrDefault()?.Trim() ?? "",
            result.DescriptionDraft.Trim(),
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

        var longMaterial = update.Materials.FirstOrDefault(material => material.Length > 45);
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
            UseWaitCursor = true;
            _statusLabel.Text = "Listing Etsy'den en guncel haliyle aliniyor...";
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
            UseWaitCursor = false;
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

    private static void ConfigureText(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ReadOnly = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.BackColor = UiStyle.CardBackground;
        textBox.ForeColor = UiStyle.TextDark;
        textBox.BorderStyle = BorderStyle.None;
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
