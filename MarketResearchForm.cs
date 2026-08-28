namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class MarketResearchForm : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly AnalyzeKeywordUseCase _analyzeKeywordUseCase;
    private readonly TrackingService _trackingService;
    private readonly ListingOptimizationHistoryService _optimizationHistoryService;
    private readonly IAiListingOptimizer _aiListingOptimizer;
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private List<MarketListingResult> _results = [];
    private MarketAnalysisService.MarketSummaryKpis _currentKpis = new();

    private readonly TextBox _keywordTextBox = new();
    private readonly NumericUpDown _limitInput = new();
    private readonly ComboBox _sortComboBox = new();
    private readonly Button _searchButton = new();
    private readonly Button _btnAiMarketReport = new();
    private readonly Button _btnTop13Tags = new();
    private readonly DataGridView _grid = new();
    private readonly PictureBox _pictureBox = new();
    private readonly Label _imageIndexLabel = new();
    private readonly TextBox _detailTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lblAiBadge = new();

    // KPI Card Labels
    private readonly Label _lblKpiAvgPrice = new();
    private readonly Label _lblKpiAvgFavs = new();
    private readonly Label _lblKpiTopShop = new();
    private readonly Label _lblKpiOpportunity = new();

    private int _currentImageIndex;
    private bool _favoriteSortDescending;

    public MarketResearchForm(
        AnalyzeKeywordUseCase analyzeKeywordUseCase,
        TrackingService trackingService,
        ListingOptimizationHistoryService optimizationHistoryService,
        IAiListingOptimizer aiListingOptimizer)
    {
        _analyzeKeywordUseCase = analyzeKeywordUseCase;
        _trackingService = trackingService;
        _optimizationHistoryService = optimizationHistoryService;
        _aiListingOptimizer = aiListingOptimizer;
        BuildLayout();
        UpdateAiBadge();
    }

    private MarketListingResult? SelectedListing => _bindingSource.Current as MarketListingResult;

    private void UpdateAiBadge()
    {
        var aiSettings = AiOptimizationSettingsStore.Load();
        _lblAiBadge.Text = aiSettings.GetActiveBadgeText();
        if (aiSettings.UseOpenAi)
        {
            _lblAiBadge.BackColor = Color.FromArgb(16, 185, 129);
            _lblAiBadge.ForeColor = Color.White;
        }
        else if (aiSettings.UseGemini)
        {
            _lblAiBadge.BackColor = Color.FromArgb(59, 130, 246);
            _lblAiBadge.ForeColor = Color.White;
        }
        else
        {
            _lblAiBadge.BackColor = Color.FromArgb(71, 85, 105);
            _lblAiBadge.ForeColor = Color.White;
        }
    }

    private void BuildLayout()
    {
        Text = "Etsy Pazar Araştırması, Rakip İstihbaratı ve AI Fırsat Analizi";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1100, 720));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));  // 2-Row Clean Search Toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));  // KPI Summary Strip
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // DataGridView
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 225)); // Bottom Details & Actions
        Controls.Add(root);

        // 1. Header (Title + AI Provider Badge + Status)
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🔍 Etsy Pazar Araştırması & Rakip İstihbaratı",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        header.Controls.Add(titleLabel, 0, 0);

        _lblAiBadge.Dock = DockStyle.Fill;
        _lblAiBadge.TextAlign = ContentAlignment.MiddleCenter;
        _lblAiBadge.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _lblAiBadge.Margin = new Padding(4, 4, 4, 4);
        _lblAiBadge.Cursor = Cursors.Hand;
        _lblAiBadge.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            if (form.ShowDialog(this) == DialogResult.OK) UpdateAiBadge();
        };
        header.Controls.Add(_lblAiBadge, 1, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Anahtar kelime girip arama yapın...";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 2, 0);
        root.Controls.Add(header, 0, 0);

        // 2. Search Toolbar (2-Row Responsive Layout)
        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 2, 0, 2) };
        searchPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        searchPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        // Row 0: Search Inputs
        var row0 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        row0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Keyword input
        row0.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Limit input
        row0.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Sort combo
        row0.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Search button

        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Etsy pazarında aranacak kelime: örn. 3d printed desk organizer, cosplay helmet, resin lamp...";
        _keywordTextBox.Font = new Font("Segoe UI", 10F);
        _keywordTextBox.Margin = new Padding(0, 2, 6, 2);
        _keywordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        };
        row0.Controls.Add(_keywordTextBox, 0, 0);

        _limitInput.Dock = DockStyle.Fill;
        _limitInput.Minimum = 10;
        _limitInput.Maximum = 100;
        _limitInput.Increment = 10;
        _limitInput.Value = 30;
        _limitInput.Font = new Font("Segoe UI", 10F);
        _limitInput.Margin = new Padding(2, 2, 6, 2);
        row0.Controls.Add(_limitInput, 1, 0);

        _sortComboBox.Dock = DockStyle.Fill;
        _sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortComboBox.Font = new Font("Segoe UI", 9.5F);
        _sortComboBox.Items.AddRange(["Pazar Puanı", "SEO Puanı", "Mağaza Satışı", "Favori Sayısı", "Görüntülenme", "Düşük Fiyat"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.Margin = new Padding(2, 2, 6, 2);
        _sortComboBox.SelectedIndexChanged += (_, _) => ApplySort();
        row0.Controls.Add(_sortComboBox, 2, 0);

        ConfigureButton(_searchButton, "🔍 Etsy'de Ara");
        _searchButton.Height = 32;
        _searchButton.Click += async (_, _) => await SearchAsync();
        row0.Controls.Add(_searchButton, 3, 0);
        searchPanel.Controls.Add(row0, 0, 0);

        // Row 1: AI & Tool Buttons Flow
        var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };

        _btnAiMarketReport.Text = "✨ AI Pazar Özeti";
        _btnAiMarketReport.Height = 34;
        _btnAiMarketReport.Width = 165;
        _btnAiMarketReport.FlatStyle = FlatStyle.Flat;
        _btnAiMarketReport.FlatAppearance.BorderSize = 0;
        _btnAiMarketReport.BackColor = Color.FromArgb(124, 58, 237); // Purple AI
        _btnAiMarketReport.ForeColor = Color.White;
        _btnAiMarketReport.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _btnAiMarketReport.Cursor = Cursors.Hand;
        _btnAiMarketReport.Margin = new Padding(0, 0, 8, 0);
        _btnAiMarketReport.Click += async (_, _) => await RunAiMarketAnalysisAsync();
        row1.Controls.Add(_btnAiMarketReport);

        _btnTop13Tags.Text = "🏷️ 13 Altın Tag";
        _btnTop13Tags.Height = 34;
        _btnTop13Tags.Width = 145;
        _btnTop13Tags.FlatStyle = FlatStyle.Flat;
        _btnTop13Tags.FlatAppearance.BorderSize = 0;
        _btnTop13Tags.BackColor = Color.FromArgb(14, 165, 233); // Cyan
        _btnTop13Tags.ForeColor = Color.White;
        _btnTop13Tags.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _btnTop13Tags.Cursor = Cursors.Hand;
        _btnTop13Tags.Margin = new Padding(0, 0, 8, 0);
        _btnTop13Tags.Click += (_, _) => CopyTop13Tags();
        row1.Controls.Add(_btnTop13Tags);

        var apiButton = CreateButton("API Ayarları", true);
        apiButton.Height = 34;
        apiButton.Width = 120;
        apiButton.Margin = new Padding(0, 0, 8, 0);
        apiButton.Click += (_, _) =>
        {
            using var form = new EtsyApiSettingsForm();
            form.ShowDialog(this);
        };
        row1.Controls.Add(apiButton);

        var trackingButton = CreateButton("Takip Merkezi", true);
        trackingButton.Height = 34;
        trackingButton.Width = 130;
        trackingButton.Margin = new Padding(0, 0, 8, 0);
        trackingButton.Click += (_, _) => OpenTrackingCenter();
        row1.Controls.Add(trackingButton);

        searchPanel.Controls.Add(row1, 0, 1);
        root.Controls.Add(searchPanel, 0, 1);

        // 3. KPI Summary Strip (4 Beautiful Metric Cards)
        var kpiPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 2, 0, 4) };
        kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        kpiPanel.Controls.Add(CreateKpiCard("💰 Ortalama Fiyat (AOV)", _lblKpiAvgPrice, "$0.00", Color.FromArgb(16, 185, 129)), 0, 0);
        kpiPanel.Controls.Add(CreateKpiCard("⭐ Talep & Ort. Favori", _lblKpiAvgFavs, "0 fav / ürün", Color.FromArgb(245, 158, 11)), 1, 0);
        kpiPanel.Controls.Add(CreateKpiCard("🏆 Pazar Lideri Rakip", _lblKpiTopShop, "-", Color.FromArgb(99, 102, 241)), 2, 0);
        kpiPanel.Controls.Add(CreateKpiCard("📈 Pazar Fırsat Skoru", _lblKpiOpportunity, "%0 / 100", Color.FromArgb(236, 72, 153)), 3, 0);
        root.Controls.Add(kpiPanel, 0, 2);

        // 4. DataGridView
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 3);

        // 5. Bottom Detail & Action Panel
        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 0),
        };
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Image preview + slider + studio btn
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Details / Tags Textbox
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400)); // Structured Action Cards

        // Left Image Preview Box
        var imageSlider = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        imageSlider.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        imageSlider.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        imageSlider.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _pictureBox.BackColor = Color.FromArgb(15, 23, 42);
        _pictureBox.BorderStyle = BorderStyle.FixedSingle;
        imageSlider.Controls.Add(_pictureBox, 0, 0);

        var sliderControls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        var previousButton = CreateButton("<", true);
        previousButton.Click += (_, _) => ShowPreviousImage();
        sliderControls.Controls.Add(previousButton, 0, 0);
        _imageIndexLabel.Dock = DockStyle.Fill;
        _imageIndexLabel.Text = "Resim yok";
        _imageIndexLabel.TextAlign = ContentAlignment.MiddleCenter;
        sliderControls.Controls.Add(_imageIndexLabel, 1, 0);
        var nextButton = CreateButton(">", true);
        nextButton.Click += (_, _) => ShowNextImage();
        sliderControls.Controls.Add(nextButton, 2, 0);
        imageSlider.Controls.Add(sliderControls, 0, 1);

        var btnSendToStudio = new Button
        {
            Text = "📸 Görsel Stüdyo'ya Gönder",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 0)
        };
        btnSendToStudio.FlatAppearance.BorderSize = 0;
        btnSendToStudio.Click += (_, _) => SendSelectedImageToStudio();
        imageSlider.Controls.Add(btnSendToStudio, 0, 2);

        detailPanel.Controls.Add(imageSlider, 0, 0);

        // Middle Detail Box
        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.BackColor = Color.FromArgb(15, 23, 42);
        _detailTextBox.ForeColor = Color.FromArgb(226, 232, 240);
        _detailTextBox.Font = new Font("Consolas", 9F);
        _detailTextBox.BorderStyle = BorderStyle.FixedSingle;
        detailPanel.Controls.Add(_detailTextBox, 1, 0);

        // Right Actions (Grouped & Colorful)
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(4, 0, 0, 0),
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        // 1. Satır: Birincil Aksiyonlar
        var cloneButton = ActionButton("🚀 Taslağa Klonla", OpenListingClone);
        cloneButton.BackColor = Color.FromArgb(16, 185, 129); // Green
        cloneButton.ForeColor = Color.White;
        cloneButton.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        actions.Controls.Add(cloneButton, 0, 0);

        var optButton = ActionButton("✨ AI Optimizasyon", OpenListingOptimization);
        optButton.BackColor = Color.FromArgb(124, 58, 237); // Purple
        optButton.ForeColor = Color.White;
        actions.Controls.Add(optButton, 1, 0);

        actions.Controls.Add(ActionButton("Takibe Ekle", () => _ = TrackSelectedListingAsync()), 2, 0);

        // 2. Satır: Analiz Araçları
        actions.Controls.Add(ActionButton("Kelime Analizi", OpenKeywordAnalysis), 0, 1);
        actions.Controls.Add(ActionButton("Rakip Analizi", OpenCompetitorAnalysis), 1, 1);
        actions.Controls.Add(ActionButton("Mağaza Aç", OpenShop), 2, 1);

        // 3. Satır: Sağlık & Kopyalama
        var healthButton = ActionButton("Sağlık Skoru", OpenHealthScore);
        healthButton.BackColor = UiStyle.PrimaryColor;
        healthButton.ForeColor = Color.White;
        actions.Controls.Add(healthButton, 0, 2);
        actions.Controls.Add(ActionButton("Tagleri Kopyala", CopyTags), 1, 2);
        actions.Controls.Add(ActionButton("Başlığı Kopyala", CopyTitle), 2, 2);

        // 4. Satır: Dışa Aktarma & Linkler
        actions.Controls.Add(ActionButton("Etsy'de Aç", OpenListing), 0, 3);
        actions.Controls.Add(ActionButton("CSV Aktar", ExportCsv), 1, 3);
        actions.Controls.Add(ActionButton("Linki Kopyala", CopyListingUrl), 2, 3);

        detailPanel.Controls.Add(actions, 2, 0);
        root.Controls.Add(detailPanel, 0, 4);
    }

    private static Panel CreateKpiCard(string title, Label valLabel, string defaultVal, Color accent)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(3),
            Padding = new Padding(10, 6, 10, 6)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8F)
        };

        valLabel.Text = defaultVal;
        valLabel.Dock = DockStyle.Fill;
        valLabel.ForeColor = accent;
        valLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        valLabel.TextAlign = ContentAlignment.MiddleLeft;

        panel.Controls.Add(valLabel);
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.RowTemplate.MinimumHeight = 76;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => UpdateDetail();
        _grid.CellDoubleClick += (_, _) => OpenListing();
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(MarketListingResult.Favorites))
            {
                ApplyFavoriteSort();
            }
        };
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ShopUrlColumn")
            {
                OpenShop();
            }
        };

        var imageColumn = new DataGridViewImageColumn
        {
            Name = "ImageColumn",
            HeaderText = "Resim",
            DataPropertyName = nameof(MarketListingResult.ThumbnailImage),
            Width = 85,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null },
        };
        _grid.Columns.Add(imageColumn);
        AddColumn("Sıra", nameof(MarketListingResult.ListingRank), 45);
        AddColumn("Ürün Başlığı", nameof(MarketListingResult.Title), 290, fill: true);
        AddColumn("Fiyat", nameof(MarketListingResult.PriceDisplay), 95);
        AddColumn("Mağaza", nameof(MarketListingResult.ShopName), 140);
        _grid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = "ShopUrlColumn",
            HeaderText = "Etsy Mağaza Linki",
            DataPropertyName = nameof(MarketListingResult.ShopUrl),
            Width = 220,
            TrackVisitedState = false,
        });
        AddColumn("Mağaza Satışı", nameof(MarketListingResult.ShopSalesDisplay), 100);
        AddColumn("Favori", nameof(MarketListingResult.Favorites), 70);
        _grid.Columns[_grid.Columns.Count - 1].SortMode = DataGridViewColumnSortMode.Programmatic;
        AddColumn("Görüntülenme", nameof(MarketListingResult.ViewsDisplay), 95);
        AddColumn("SEO", nameof(MarketListingResult.SeoScore), 55);
        AddColumn("Pazar Puanı", nameof(MarketListingResult.MarketScore), 85);
        AddColumn("Tagler", nameof(MarketListingResult.TagsDisplay), 220, fill: true);
    }

    private async Task SearchAsync()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Lütfen aranacak anahtar kelimeyi girin.", "Arama Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _searchButton.Enabled = false;
            _searchButton.Text = "Aranıyor...";
            _statusLabel.Text = "Etsy API'den sonuçlar getiriliyor...";
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials)
            {
                using var form = new EtsyApiSettingsForm();
                form.ShowDialog(this);
                settings = EtsyApiSettingsStore.Load();
            }

            _results = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            _currentKpis = MarketAnalysisService.CalculateKpis(_results);
            UpdateKpiCards();
            ApplySort();
            _ = LoadThumbnailsAsync(_results);
            _statusLabel.Text = $"{_results.Count} ürün listelendi | Ortalama Fiyat: ${_currentKpis.AveragePrice} | Fırsat Skoru: %{_currentKpis.OpportunityScore}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy API Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Arama başarısız oldu.";
        }
        finally
        {
            _searchButton.Enabled = true;
            _searchButton.Text = "🔍 Etsy'de Ara";
        }
    }

    private void UpdateKpiCards()
    {
        if (_results.Count == 0)
        {
            _lblKpiAvgPrice.Text = "$0.00";
            _lblKpiAvgFavs.Text = "0 fav / ürün";
            _lblKpiTopShop.Text = "-";
            _lblKpiOpportunity.Text = "%0 / 100";
            return;
        }

        _lblKpiAvgPrice.Text = $"${_currentKpis.AveragePrice:N2} (${_currentKpis.MinPrice:N0}-${_currentKpis.MaxPrice:N0})";
        _lblKpiAvgFavs.Text = $"{_currentKpis.AverageFavorites:N1} favori / ürün";
        _lblKpiTopShop.Text = $"{_currentKpis.TopShopName} ({_currentKpis.TopShopSales:N0} satış)";
        _lblKpiOpportunity.Text = $"%{_currentKpis.OpportunityScore} / 100 ({(_currentKpis.OpportunityScore >= 75 ? "Yüksek Fırsat 🚀" : "Orta Fırsat ⭐")})";
    }

    private async Task RunAiMarketAnalysisAsync()
    {
        if (_results.Count == 0)
        {
            MessageBox.Show(this, "Lütfen önce bir anahtar kelime aratın.", "Pazar Raporu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string keyword = _keywordTextBox.Text.Trim();
        var aiSettings = AiOptimizationSettingsStore.Load();

        try
        {
            _btnAiMarketReport.Enabled = false;
            _btnAiMarketReport.Text = "Analiz Ediliyor...";
            _statusLabel.Text = "Yapay zeka pazar verilerini işliyor...";

            string report = await MarketAnalysisService.GenerateAiMarketReportAsync(keyword, _currentKpis, _results, aiSettings);

            using var form = new Form
            {
                Text = $"✨ AI Pazar Strateji Raporu - '{keyword}'",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.White
            };

            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 10F),
                Text = report
            };

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var btnCopy = new Button { Text = "📋 Raporu Kopyala", BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Height = 32, Width = 150 };
            btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(report);
                MessageBox.Show(form, "Rapor panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            bottom.Controls.Add(btnCopy);

            form.Controls.Add(txt);
            form.Controls.Add(bottom);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI Raporu oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnAiMarketReport.Enabled = true;
            _btnAiMarketReport.Text = "✨ AI Pazar Özeti";
            _statusLabel.Text = "AI analizi tamamlandı.";
        }
    }

    private void CopyTop13Tags()
    {
        if (_currentKpis.TopTags.Count == 0)
        {
            MessageBox.Show(this, "Önce bir arama yapmalısınız.", "Tag Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string tagsJoined = string.Join(", ", _currentKpis.TopTags.Select(t => t.Key));
        Clipboard.SetText(tagsJoined);

        var preview = string.Join("\n", _currentKpis.TopTags.Select((t, i) => $"{i + 1}. {t.Key} ({t.Value} rakip kullandı)"));
        MessageBox.Show(this, $"Rakiplerin en çok tutan 13 etiketi panoya kopyalandı:\n\n{preview}", "13 Altın Tag Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _statusLabel.Text = "En popüler 13 etiket panoya kopyalandı.";
    }

    private void SendSelectedImageToStudio()
    {
        var item = SelectedListing;
        if (item == null || item.ImageUrls.Count == 0)
        {
            MessageBox.Show(this, "Lütfen görseli olan bir ürün seçin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (DashboardForm.Instance != null)
        {
            _ = DashboardForm.Instance.OpenModuleByIdAsync("image-editor");
        }
    }

    private void ApplySort()
    {
        _favoriteSortDescending = false;
        IEnumerable<MarketListingResult> sorted = _sortComboBox.SelectedItem?.ToString() switch
        {
            "SEO Puanı" => _results.OrderByDescending(item => item.SeoScore),
            "Mağaza Satışı" => _results.OrderByDescending(item => item.ShopSales),
            "Favori Sayısı" => _results.OrderByDescending(item => item.Favorites),
            "Görüntülenme" => _results.OrderByDescending(item => item.Views),
            "Düşük Fiyat" => _results.OrderBy(item => item.Price),
            _ => _results.OrderByDescending(item => item.MarketScore),
        };

        BindResults(sorted.ToList());
        ClearSortGlyphs();
    }

    private void ApplyFavoriteSort()
    {
        _favoriteSortDescending = !_favoriteSortDescending;
        var sorted = _favoriteSortDescending
            ? _results.OrderByDescending(item => item.Favorites).ThenByDescending(item => item.Views)
            : _results.OrderBy(item => item.Favorites).ThenBy(item => item.Views);
        BindResults(sorted.ToList());
        ClearSortGlyphs();

        var favoriteColumn = _grid.Columns
            .Cast<DataGridViewColumn>()
            .First(column => column.DataPropertyName == nameof(MarketListingResult.Favorites));
        favoriteColumn.HeaderCell.SortGlyphDirection = _favoriteSortDescending
            ? SortOrder.Descending
            : SortOrder.Ascending;
        _statusLabel.Text = _favoriteSortDescending
            ? $"{_results.Count} ürün | Favori: çoktan aza"
            : $"{_results.Count} ürün | Favori: azdan çoğa";
    }

    private void BindResults(List<MarketListingResult> list)
    {
        for (var index = 0; index < list.Count; index++)
        {
            list[index].ListingRank = index + 1;
        }

        _bindingSource.DataSource = list;
        _bindingSource.Position = list.Count > 0 ? 0 : -1;
        UpdateDetail();
    }

    private void ClearSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }
    }

    private void UpdateDetail()
    {
        var item = SelectedListing;
        if (item is null)
        {
            _detailTextBox.Text = "Arama sonucu seçilmedi.";
            ClearPicture();
            return;
        }

        _currentImageIndex = 0;
        _ = EnsureImagesAndShowAsync(item);
        _detailTextBox.Text =
            $"📌 BAŞLIK{Environment.NewLine}{item.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"🏪 MAĞAZA / FİYAT{Environment.NewLine}{item.ShopName} | {item.PriceDisplay}{Environment.NewLine}" +
            $"Mağaza Toplam Satışı: {item.ShopSalesDisplay} | Yorum: {item.ReviewCount:N0} | Puan: {item.ReviewAverage:0.0} ⭐{Environment.NewLine}" +
            $"Favori: {item.Favorites:N0} | Görüntülenme: {item.ViewsDisplay} | Stok: {item.Quantity}{Environment.NewLine}{Environment.NewLine}" +
            $"📊 SEO / PAZAR{Environment.NewLine}SEO Puanı: {item.SeoScore}/100 | Pazar Puanı: {item.MarketScore}/100{Environment.NewLine}" +
            $"Ürün Tahmini Satışı: {item.ProductSalesDisplay}{Environment.NewLine}{Environment.NewLine}" +
            $"🏷️ ETİKETLER ({item.Tags.Count}){Environment.NewLine}{item.TagsDisplay}{Environment.NewLine}{Environment.NewLine}" +
            $"📝 ÜRÜN AÇIKLAMASI{Environment.NewLine}{TrimDescription(item.Description)}";
    }

    private void OpenListing() => OpenUrl(SelectedListing?.ListingUrl);
    private void OpenShop() => OpenUrl(SelectedListing?.ShopUrl);

    private void OpenKeywordAnalysis()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Analiz edilecek anahtar kelimeyi girin.", "Kelime Analizi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new KeywordOpportunityAnalysisForm(_analyzeKeywordUseCase, _trackingService, keyword);
        form.ShowDialog(this);
    }

    private void OpenCompetitorAnalysis()
    {
        var listing = SelectedListing;
        if (listing is null || (listing.ShopId <= 0 && string.IsNullOrWhiteSpace(listing.ShopName)))
        {
            MessageBox.Show(this, "Rakip analizi için mağaza bilgisi bulunan bir ürün seçin.", "Rakip Analizi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new CompetitorShopAnalysisForm(listing, _trackingService);
        if (DashboardForm.Instance != null) DashboardForm.Instance.EmbedModuleForm(form);
        else form.ShowDialog(this);
    }

    private void OpenListingOptimization()
    {
        var listing = SelectedListing;
        if (listing is null)
        {
            MessageBox.Show(this, "Optimizasyon için bir ürün seçin.", "AI Optimizasyon", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new ListingOptimizationForm(
            _optimizationHistoryService,
            _aiListingOptimizer,
            listing,
            _keywordTextBox.Text.Trim());
        if (DashboardForm.Instance != null) DashboardForm.Instance.EmbedModuleForm(form);
        else form.ShowDialog(this);
    }

    private async Task TrackSelectedListingAsync()
    {
        var item = SelectedListing;
        if (item is null)
        {
            MessageBox.Show(this, "Takip edilecek ürünü seçin.", "Takibe Ekle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _trackingService.TrackAsync(new TrackingCapture(
            TrackingEntityType.Listing,
            item.ListingId.ToString(CultureInfo.InvariantCulture),
            item.Title,
            item.ListingUrl,
            new TrackingSnapshot(
                0,
                0,
                DateTimeOffset.Now,
                item.Price,
                item.CurrencyCode,
                item.Favorites,
                item.Views,
                item.ShopSales,
                item.ReviewCount,
                item.ReviewAverage,
                item.SeoScore,
                item.MarketScore)));
        _statusLabel.Text = "Ürün takip merkezine eklendi ve snapshot kaydedildi.";
    }

    private void OpenTrackingCenter()
    {
        if (DashboardForm.Instance != null)
        {
            _ = DashboardForm.Instance.OpenModuleByIdAsync("tracking");
        }
        else
        {
            using var form = new TrackingHistoryForm(_trackingService);
            form.ShowDialog(this);
        }
    }

    private void CopyTags() => CopyText(SelectedListing?.TagsDisplay, "Tagler kopyalandı.");
    private void CopyTitle() => CopyText(SelectedListing?.Title, "Başlık kopyalandı.");
    private void CopyListingUrl() => CopyText(SelectedListing?.ListingUrl, "Listing linki kopyalandı.");

    private void OpenListingClone()
    {
        var listing = SelectedListing;
        if (listing is null)
        {
            MessageBox.Show(
                this,
                "Klonlanacak bir listing seçin.",
                "Listing Klonla",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var form = new ProductDiscoveryListingCreatorForm(
            _aiListingOptimizer,
            initialKeyword: _keywordTextBox.Text.Trim(),
            initialListing: listing,
            historyService: _optimizationHistoryService);
        if (DashboardForm.Instance != null) DashboardForm.Instance.EmbedModuleForm(form);
        else form.ShowDialog(this);
    }

    private void OpenHealthScore()
    {
        var listing = SelectedListing;
        if (listing is null)
        {
            MessageBox.Show(this, "Sağlık skoru hesaplamak için bir ürün seçin.", "Sağlık Skoru", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new ListingHealthScoreForm(listing, _aiListingOptimizer, _optimizationHistoryService);
        if (DashboardForm.Instance != null) DashboardForm.Instance.EmbedModuleForm(form);
        else form.ShowDialog(this);
    }

    private async Task EnsureImagesAndShowAsync(MarketListingResult item)
    {
        if (item.ImageUrls.Count == 0)
        {
            try
            {
                var settings = EtsyApiSettingsStore.Load();
                item.ImageUrls = await _apiClient.GetListingImagesAsync(settings, item.ListingId);
            }
            catch
            {
                item.ImageUrls = [];
            }
        }

        if (SelectedListing?.ListingId != item.ListingId)
        {
            return;
        }

        ShowCurrentImage();
        if (item.ThumbnailImage is null && item.ImageUrls.Count > 0)
        {
            item.ThumbnailImage = await DownloadImageAsync(item.ImageUrls[0]);
            _grid.Refresh();
        }
    }

    private async Task LoadThumbnailsAsync(IEnumerable<MarketListingResult> items)
    {
        using var semaphore = new SemaphoreSlim(6);
        var tasks = items.Where(item => item.ImageUrls.Count > 0).Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                item.ThumbnailImage = await DownloadImageAsync(item.ImageUrls[0]);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (!IsDisposed)
        {
            BeginInvoke(_grid.Refresh);
        }
    }

    private async Task<Image?> DownloadImageAsync(string url)
    {
        try
        {
            var bytes = await _imageHttpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private void ShowPreviousImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0) return;
        _currentImageIndex = (_currentImageIndex - 1 + images.Count) % images.Count;
        ShowCurrentImage();
    }

    private void ShowNextImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0) return;
        _currentImageIndex = (_currentImageIndex + 1) % images.Count;
        ShowCurrentImage();
    }

    private void ShowCurrentImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0)
        {
            ClearPicture();
            _imageIndexLabel.Text = "Resim yok";
            return;
        }

        _currentImageIndex = Math.Clamp(_currentImageIndex, 0, images.Count - 1);
        LoadPicture(images[_currentImageIndex]);
        _imageIndexLabel.Text = $"{_currentImageIndex + 1} / {images.Count}";
    }

    private void LoadPicture(string imageUrl)
    {
        ClearPicture();
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        try
        {
            _pictureBox.LoadAsync(imageUrl);
        }
        catch
        {
            ClearPicture();
        }
    }

    private void ClearPicture()
    {
        _pictureBox.CancelAsync();
        _pictureBox.ImageLocation = null;
        _pictureBox.Image = null;
    }

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void CopyText(string? text, string status)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            _statusLabel.Text = status;
        }
    }

    private void ExportCsv()
    {
        if (_results.Count == 0) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV dosyası (*.csv)|*.csv",
            FileName = $"etsy-pazar-arastirma-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var builder = new StringBuilder();
        builder.AppendLine("Baslik,Fiyat,Magaza,MagazaSatisi,Favori,Goruntulenme,SEO,PazarPuani,Tagler,ListingLinki,MagazaLinki");
        foreach (var item in _results)
        {
            builder.AppendLine(string.Join(",", Csv(item.Title), Csv(item.PriceDisplay), Csv(item.ShopName), item.ShopSales, item.Favorites, item.Views, item.SeoScore, item.MarketScore, Csv(item.TagsDisplay), Csv(item.ListingUrl), Csv(item.ShopUrl)));
        }
        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "CSV başarıyla kaydedildi.";
    }

    private void AddColumn(string header, string property, int width, bool fill = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            FillWeight = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });
    }

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button();
        ConfigureButton(button, text, isSecondary);
        return button;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = CreateButton(text);
        button.Margin = new Padding(2);
        button.Font = new Font("Segoe UI Semibold", 8.5F);
        button.Click += (_, _) => action();
        return button;
    }

    private static void ConfigureButton(Button button, string text, bool isSecondary = false)
    {
        button.Dock = DockStyle.Fill;
        button.Text = text;
        button.BackColor = isSecondary ? UiStyle.SecondaryColor : UiStyle.PrimaryColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isSecondary ? UiStyle.SecondaryHover : UiStyle.PrimaryHover;
        button.Font = UiStyle.SemiboldBaseFont;
        button.Margin = new Padding(2);
    }

    private static string TrimDescription(string description) =>
        description.Length <= 900 ? description : description[..900] + "...";

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
