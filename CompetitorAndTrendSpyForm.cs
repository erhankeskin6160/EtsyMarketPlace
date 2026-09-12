namespace SimilarProductsWinForms;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.SeasonalTrends;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class CompetitorAndTrendSpyForm : Form
{
    private static readonly ConcurrentDictionary<long, string> _imageUrlCache = new();
    private static readonly ConcurrentDictionary<long, Image> _thumbnailCache = new();

    private readonly EtsyApiClient _apiClient = new();
    private readonly SeasonalTrendService _trendService = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly IAiListingOptimizer? _aiOptimizer;

    // Header & Navigation
    private readonly Label _statusLabel = new();
    private readonly Button _tabCompetitorBtn = new();
    private readonly Button _tabTrendBtn = new();
    private readonly Panel _contentPanel = new();

    // Tab 1: Competitor Spy Controls
    private readonly Panel _competitorPanel = new();
    private readonly TextBox _shopInput = new();
    private readonly ComboBox _watchlistComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _saveWatchlistBtn = new();
    private readonly ModernNumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 50 };
    private readonly Button _spyShopBtn = new();
    private readonly Button _openEtsyShopBtn = new();
    private readonly Button _exportCsvBtn = new();
    private readonly TableLayoutPanel _kpiTable = new();

    // Listings Grid & Filter Controls
    private readonly TextBox _searchListingTextBox = new();
    private readonly ComboBox _sortComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _priceFilterComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _listingCountLabel = new();
    private readonly DataGridView _competitorGrid = new();

    // Right Side: Selected Product Action Hub & Tags
    private readonly PictureBox _selectedProductPic = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(20, 29, 47) };
    private readonly Label _selectedTitleLabel = new();
    private readonly Label _selectedStatsLabel = new();
    private readonly Label _selectedSalesEstLabel = new();
    private readonly Button _createDraftBtn = new();
    private readonly Button _sendToImageStudioBtn = new();
    private readonly Button _copyItemTagsBtn = new();
    private readonly FlowLayoutPanel _selectedItemTagsContainer = new();
    private ModernScrollPanel? _tagsScroll;

    // Bottom Intelligence Switch: Tags vs AI Gap vs eRank Audit
    private readonly Button _btnViewTags = new();
    private readonly Button _btnViewAiGap = new();
    private readonly Button _btnViewAudit = new();
    private readonly Panel _intelligenceContentPanel = new();
    private readonly Panel _tagsPanel = new();
    private readonly Panel _aiGapPanel = new();
    private readonly Panel _auditPanel = new();
    private readonly DataGridView _tagsGrid = new();
    private readonly TextBox _tagSearchTextBox = new();
    private readonly Button _copyTagsBtn = new();
    private readonly ModernMultilineTextBox _gapAnalysisText = new();
    private readonly ModernMultilineTextBox _auditAnalysisText = new();

    private CompetitorShopAnalysis? _currentAnalysis;
    private List<MarketListingResult> _filteredListings = [];
    private MarketListingResult? _selectedListing;
    private CancellationTokenSource? _thumbnailCts;
    private bool _isDisposed;

    // Tab 2: Seasonal Trend Radar Controls
    private readonly Panel _trendPanel = new();
    private readonly TextBox _trendKeywordInput = new();
    private readonly Button _trendSearchBtn = new();
    private readonly FlowLayoutPanel _trendEventsContainer = new();
    private readonly FlowLayoutPanel _trendKpiPillContainer = new();
    private readonly TextBox _trendAdviceText = new();
    private readonly ListBox _winningTagsListBox = new();
    private readonly ListBox _topTitlesListBox = new();
    private readonly Button _copyTrendTagsBtn = new();
    private readonly Button _createDraftFromTrendBtn = new();

    public CompetitorAndTrendSpyForm(IAiListingOptimizer? aiOptimizer = null, string? initialShopName = null)
    {
        _aiOptimizer = aiOptimizer;
        BuildLayout();

        LoadWatchlist();

        if (!string.IsNullOrWhiteSpace(initialShopName))
        {
            _shopInput.Text = initialShopName;
            Shown += async (_, _) => await RunCompetitorAnalysisAsync();
        }
        else
        {
            LoadSeasonalCalendar();
        }
    }

    private void BuildLayout()
    {
        Text = "Rakip ve Trend Casusluğu";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 680);
        BackColor = Color.FromArgb(15, 23, 42); // Dark slate
        ForeColor = Color.FromArgb(241, 245, 249);
        Font = new Font("Segoe UI", 9.5F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(15, 23, 42),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Tab Navigation Strip
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content View
        Controls.Add(root);

        // 1. Header
        root.Controls.Add(BuildHeader(), 0, 0);

        // 2. Tab Navigation Strip
        root.Controls.Add(BuildTabStrip(), 0, 1);

        // 3. Content View Panel
        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = Color.FromArgb(15, 23, 42);
        root.Controls.Add(_contentPanel, 0, 2);

        // Prepare both tabs
        BuildCompetitorTab();
        BuildTrendTab();

        // Show default tab
        SwitchTab(isCompetitorTab: true);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var titleBox = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var title = new Label
        {
            Text = "🕵️ Rakip & Trend Casusluğu",
            Font = new Font("Segoe UI Semibold", 14.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            AutoSize = true,
            Margin = new Padding(0, 4, 10, 0),
        };
        var subtitle = new Label
        {
            Text = "EverBee & eRank Destekli Rakip Zekası Suite v2.5",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        titleBox.Controls.Add(title);
        titleBox.Controls.Add(subtitle);
        header.Controls.Add(titleBox, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Hazır. Rakip mağaza adı girin veya takip listesinden seçin.";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        _statusLabel.ForeColor = Color.FromArgb(148, 163, 184);
        header.Controls.Add(_statusLabel, 1, 0);

        return header;
    }

    private Control BuildTabStrip()
    {
        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 2, 0, 2),
            BackColor = Color.FromArgb(15, 23, 42),
            WrapContents = false,
        };

        ConfigureTabButton(_tabCompetitorBtn, "🏬 1. Rakip Mağaza Casusu", isSelected: true);
        _tabCompetitorBtn.Click += (_, _) => SwitchTab(isCompetitorTab: true);

        ConfigureTabButton(_tabTrendBtn, "📈 2. Mevsimsel Trend & Fırsat Radarı", isSelected: false);
        _tabTrendBtn.Click += (_, _) => SwitchTab(isCompetitorTab: false);

        strip.Controls.Add(_tabCompetitorBtn);
        strip.Controls.Add(_tabTrendBtn);
        return strip;
    }

    private void ConfigureTabButton(Button btn, string text, bool isSelected)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.Height = 32;
        btn.Padding = new Padding(12, 2, 12, 2);
        btn.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(0, 0, 8, 0);

        if (isSelected)
        {
            btn.BackColor = Color.FromArgb(37, 99, 235);
            btn.ForeColor = Color.White;
        }
        else
        {
            btn.BackColor = Color.FromArgb(30, 41, 59);
            btn.ForeColor = Color.FromArgb(203, 213, 225);
        }
    }

    private void SwitchTab(bool isCompetitorTab)
    {
        ConfigureTabButton(_tabCompetitorBtn, "🏬 1. Rakip Mağaza Casusu", isSelected: isCompetitorTab);
        ConfigureTabButton(_tabTrendBtn, "📈 2. Mevsimsel Trend & Fırsat Radarı", isSelected: !isCompetitorTab);

        _contentPanel.Controls.Clear();
        if (isCompetitorTab)
        {
            _contentPanel.Controls.Add(_competitorPanel);
            _statusLabel.Text = _currentAnalysis != null
                ? $"'{_currentAnalysis.Shop.ShopName}' mağaza analizi gösteriliyor."
                : "Mağaza adı girip 'Mağazayı Çözümle' butonuna tıklayın.";
        }
        else
        {
            _contentPanel.Controls.Add(_trendPanel);
            _statusLabel.Text = "Etsy mevsimsel trend takvimi ve anlık niş fırsat analizi.";
        }
    }

    #region Tab 1: Competitor Spy v2.5 Implementation

    private void BuildCompetitorTab()
    {
        _competitorPanel.Dock = DockStyle.Fill;
        _competitorPanel.BackColor = Color.FromArgb(15, 23, 42);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Toolbar
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // KPI Table
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main SplitContainer
        _competitorPanel.Controls.Add(table);

        // 1. Toolbar
        table.Controls.Add(BuildCompetitorToolbar(), 0, 0);

        // 2. KPI Table (7 Flexible columns)
        _kpiTable.Dock = DockStyle.Fill;
        _kpiTable.RowCount = 1;
        _kpiTable.ColumnCount = 7;
        _kpiTable.Margin = new Padding(0, 1, 0, 3);
        for (int i = 0; i < 7; i++)
            _kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        table.Controls.Add(_kpiTable, 0, 1);
        ResetCompetitorKpis();

        // 3. Main SplitContainer
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(30, 41, 59),
            Panel1MinSize = 0,
            Panel2MinSize = 0,
        };

        // Left Panel: Listings Grid + Search / Filter Bar
        mainSplit.Panel1.Controls.Add(BuildLeftListingsPanel());

        // Right Panel: Selected Item Action Hub + Intelligence Views
        mainSplit.Panel2.Controls.Add(BuildRightDetailPanel());

        table.Controls.Add(mainSplit, 0, 2);

        void AdjustSplitter()
        {
            try
            {
                if (mainSplit.Width > 200)
                {
                    int target = (int)(mainSplit.Width * 0.56);
                    int minLeft = 320;
                    int maxLeft = Math.Max(minLeft, mainSplit.Width - 340);
                    mainSplit.SplitterDistance = Math.Clamp(target, minLeft, maxLeft);
                }
            }
            catch { }
        }
        Shown += (_, _) => AdjustSplitter();
        mainSplit.SizeChanged += (_, _) => { if (mainSplit.Width > 200) AdjustSplitter(); };
    }

    private Control BuildCompetitorToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var lbl = new Label
        {
            Text = "Mağaza:",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(0, 6, 4, 0),
        };
        toolbar.Controls.Add(lbl);

        _shopInput.Width = 200;
        _shopInput.Height = 30;
        _shopInput.BackColor = Color.FromArgb(30, 41, 59);
        _shopInput.ForeColor = Color.White;
        _shopInput.Font = new Font("Segoe UI", 9.5F);
        _shopInput.PlaceholderText = "Mağaza adı veya Etsy linki";
        _shopInput.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunCompetitorAnalysisAsync(); } };
        toolbar.Controls.Add(_shopInput);

        // Watchlist ComboBox
        _watchlistComboBox.Width = 140;
        _watchlistComboBox.Height = 30;
        _watchlistComboBox.BackColor = Color.FromArgb(30, 41, 59);
        _watchlistComboBox.ForeColor = Color.FromArgb(56, 189, 248);
        _watchlistComboBox.Font = new Font("Segoe UI", 8.5F);
        _watchlistComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (_watchlistComboBox.SelectedItem is string shop && !string.IsNullOrWhiteSpace(shop) && shop != "📌 Takip Listesi")
            {
                _shopInput.Text = shop;
                await RunCompetitorAnalysisAsync();
            }
        };
        toolbar.Controls.Add(_watchlistComboBox);

        _saveWatchlistBtn.Text = "⭐ Takip Et";
        _saveWatchlistBtn.Height = 30;
        _saveWatchlistBtn.AutoSize = true;
        _saveWatchlistBtn.BackColor = Color.FromArgb(30, 41, 59);
        _saveWatchlistBtn.ForeColor = Color.FromArgb(250, 204, 21);
        _saveWatchlistBtn.Font = new Font("Segoe UI Semibold", 8.5F);
        _saveWatchlistBtn.FlatStyle = FlatStyle.Flat;
        _saveWatchlistBtn.Cursor = Cursors.Hand;
        _saveWatchlistBtn.Margin = new Padding(4, 0, 0, 0);
        _saveWatchlistBtn.Click += (_, _) =>
        {
            var shop = _shopInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(shop))
            {
                CompetitorWatchlistStore.Add(shop);
                LoadWatchlist();
                MessageBox.Show($"'{shop}' takip listenize kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        toolbar.Controls.Add(_saveWatchlistBtn);

        var limitLbl = new Label
        {
            Text = "Limit:",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(8, 6, 2, 0),
        };
        toolbar.Controls.Add(limitLbl);

        _limitInput.Width = 52;
        _limitInput.BackColor = Color.FromArgb(30, 41, 59);
        _limitInput.ForeColor = Color.White;
        toolbar.Controls.Add(_limitInput);

        _spyShopBtn.Text = "🕵️ Mağazayı Çözümle";
        _spyShopBtn.AutoSize = true;
        _spyShopBtn.Height = 30;
        _spyShopBtn.BackColor = Color.FromArgb(37, 99, 235);
        _spyShopBtn.ForeColor = Color.White;
        _spyShopBtn.Font = new Font("Segoe UI Semibold", 9F);
        _spyShopBtn.FlatStyle = FlatStyle.Flat;
        _spyShopBtn.Cursor = Cursors.Hand;
        _spyShopBtn.Margin = new Padding(6, 0, 0, 0);
        _spyShopBtn.Click += async (_, _) => await RunCompetitorAnalysisAsync();
        toolbar.Controls.Add(_spyShopBtn);

        _openEtsyShopBtn.Text = "🌐 Etsy";
        _openEtsyShopBtn.AutoSize = true;
        _openEtsyShopBtn.Height = 30;
        _openEtsyShopBtn.BackColor = Color.FromArgb(30, 41, 59);
        _openEtsyShopBtn.ForeColor = Color.FromArgb(203, 213, 225);
        _openEtsyShopBtn.Font = new Font("Segoe UI Semibold", 8.5F);
        _openEtsyShopBtn.FlatStyle = FlatStyle.Flat;
        _openEtsyShopBtn.Cursor = Cursors.Hand;
        _openEtsyShopBtn.Margin = new Padding(4, 0, 0, 0);
        _openEtsyShopBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_currentAnalysis?.Shop.ShopUrl))
                Process.Start(new ProcessStartInfo { FileName = _currentAnalysis.Shop.ShopUrl, UseShellExecute = true });
        };
        toolbar.Controls.Add(_openEtsyShopBtn);

        _exportCsvBtn.Text = "📥 CSV";
        _exportCsvBtn.AutoSize = true;
        _exportCsvBtn.Height = 30;
        _exportCsvBtn.BackColor = Color.FromArgb(30, 41, 59);
        _exportCsvBtn.ForeColor = Color.FromArgb(203, 213, 225);
        _exportCsvBtn.Font = new Font("Segoe UI Semibold", 8.5F);
        _exportCsvBtn.FlatStyle = FlatStyle.Flat;
        _exportCsvBtn.Cursor = Cursors.Hand;
        _exportCsvBtn.Margin = new Padding(4, 0, 0, 0);
        _exportCsvBtn.Click += (_, _) => ExportCompetitorCsv();
        toolbar.Controls.Add(_exportCsvBtn);

        return toolbar;
    }

    private void LoadWatchlist()
    {
        var list = CompetitorWatchlistStore.Load();
        _watchlistComboBox.Items.Clear();
        _watchlistComboBox.Items.Add("📌 Takip Listesi");
        foreach (var item in list)
        {
            _watchlistComboBox.Items.Add(item);
        }
        _watchlistComboBox.SelectedIndex = 0;
    }

    private Control BuildLeftListingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Filter Toolbar
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Listings DataGridView
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Footer count/status

        // 1. Filter Toolbar
        var filterBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 1, 0, 1),
        };

        _searchListingTextBox.Width = 150;
        _searchListingTextBox.Height = 26;
        _searchListingTextBox.BackColor = Color.FromArgb(30, 41, 59);
        _searchListingTextBox.ForeColor = Color.White;
        _searchListingTextBox.Font = new Font("Segoe UI", 8.5F);
        _searchListingTextBox.PlaceholderText = "🔍 Ürünlerde ara...";
        _searchListingTextBox.TextChanged += (_, _) => ApplyListingFilters();
        filterBar.Controls.Add(_searchListingTextBox);

        _sortComboBox.Width = 190;
        _sortComboBox.Height = 26;
        _sortComboBox.BackColor = Color.FromArgb(30, 41, 59);
        _sortComboBox.ForeColor = Color.White;
        _sortComboBox.Font = new Font("Segoe UI", 8.5F);
        _sortComboBox.Items.AddRange([
            "🔥 En Çok Satanlar (EverBee Satış)",
            "💵 En Yüksek Ciro (EverBee Ciro)",
            "❤️ En Çok Favori Alanlar",
            "👁️ En Çok Görüntülenenler",
            "⚠️ En Zayıf SEO (eRank Açıkları)",
        ]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => ApplyListingFilters();
        filterBar.Controls.Add(_sortComboBox);

        _priceFilterComboBox.Width = 95;
        _priceFilterComboBox.Height = 26;
        _priceFilterComboBox.BackColor = Color.FromArgb(30, 41, 59);
        _priceFilterComboBox.ForeColor = Color.White;
        _priceFilterComboBox.Font = new Font("Segoe UI", 8.5F);
        _priceFilterComboBox.Items.AddRange(["Tüm Fiyat", "$0 - $30", "$30 - $75", "$75+"]);
        _priceFilterComboBox.SelectedIndex = 0;
        _priceFilterComboBox.SelectedIndexChanged += (_, _) => ApplyListingFilters();
        filterBar.Controls.Add(_priceFilterComboBox);

        _listingCountLabel.Text = "0 Ürün";
        _listingCountLabel.AutoSize = true;
        _listingCountLabel.ForeColor = Color.FromArgb(148, 163, 184);
        _listingCountLabel.Font = new Font("Segoe UI Semibold", 8.5F);
        _listingCountLabel.Margin = new Padding(6, 4, 0, 0);
        filterBar.Controls.Add(_listingCountLabel);

        panel.Controls.Add(filterBar, 0, 0);

        // 2. DataGridView
        ConfigureCompetitorGrid();
        panel.Controls.Add(_competitorGrid, 0, 1);

        // 3. Footer info
        var hint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "💡 EverBee & eRank Metrikleri: Satış ve Ciro verileri halka açık sinyallerle hesaplanmış projeksiyonlardır.",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 7.5F),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(hint, 0, 2);

        return panel;
    }

    private void ConfigureCompetitorGrid()
    {
        _competitorGrid.Dock = DockStyle.Fill;
        _competitorGrid.BackgroundColor = Color.FromArgb(20, 29, 47);
        _competitorGrid.BorderStyle = BorderStyle.None;
        _competitorGrid.RowHeadersVisible = false;
        _competitorGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _competitorGrid.MultiSelect = false;
        _competitorGrid.ReadOnly = true;
        _competitorGrid.AllowUserToAddRows = false;
        _competitorGrid.EnableHeadersVisualStyles = false;
        _competitorGrid.RowTemplate.Height = 52;

        _competitorGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _competitorGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
        _competitorGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F);
        _competitorGrid.ColumnHeadersHeight = 30;

        _competitorGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 29, 47);
        _competitorGrid.DefaultCellStyle.ForeColor = Color.FromArgb(241, 245, 249);
        _competitorGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
        _competitorGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        _competitorGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);

        _competitorGrid.Columns.Clear();

        // 0: Resim (Thumbnail)
        _competitorGrid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Görsel",
            Width = 52,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });

        // 1: Rank (#)
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Width = 32 });
        // 2: Başlık
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün Başlığı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        // 3: Fiyat
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fiyat", Width = 75 });
        // 4: EverBee Tahmini Aylık Satış
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Aylık Satış", Width = 85 });
        // 5: EverBee Tahmini Aylık Ciro
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Aylık Ciro", Width = 85 });
        // 6: Favori
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Favori", Width = 60 });
        // 7: Görüntülenme
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Görüntüleme", Width = 75 });
        // 8: eRank LQS Notu
        _competitorGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "LQS", Width = 45 });

        _competitorGrid.SelectionChanged += (_, _) => OnListingSelected();
        _competitorGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _filteredListings.Count)
            {
                var listing = _filteredListings[e.RowIndex];
                if (!string.IsNullOrWhiteSpace(listing.ListingUrl))
                    Process.Start(new ProcessStartInfo { FileName = listing.ListingUrl, UseShellExecute = true });
            }
        };
    }

    private Control BuildRightDetailPanel()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(30, 41, 59),
            Panel1MinSize = 0,
            Panel2MinSize = 0,
        };

        // Upper: Selected Product Action Hub
        split.Panel1.Controls.Add(BuildSelectedProductCard());

        // Lower: Intelligence View (Tags vs eRank Audit vs AI Gap)
        split.Panel2.Controls.Add(BuildIntelligenceSection());

        void AdjustSubSplitter()
        {
            try
            {
                if (split.Height > 100)
                {
                    int target = 235;
                    int minTop = 160;
                    int maxTop = Math.Max(minTop, split.Height - 120);
                    split.SplitterDistance = Math.Clamp(target, minTop, maxTop);
                }
            }
            catch { }
        }
        Shown += (_, _) => AdjustSubSplitter();
        split.SizeChanged += (_, _) => { if (split.Height > 100) AdjustSubSplitter(); };

        return split;
    }

    private Control BuildSelectedProductCard()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = " 🎯 Seçili Rakip Ürün & Aksiyon ",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8.5F),
            Padding = new Padding(6),
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 2,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95)); // Product Picture
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Details & Buttons
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));        // Pic & Title/Stats
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));        // Action Buttons Row
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));       // Chips for tags

        // Pic (Col 0, Row 0)
        _selectedProductPic.Dock = DockStyle.Fill;
        _selectedProductPic.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_selectedProductPic, 0, 0);

        // Title & Stats (Col 1, Row 0)
        var titleStatsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        titleStatsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        titleStatsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        titleStatsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        _selectedTitleLabel.Dock = DockStyle.Fill;
        _selectedTitleLabel.Text = "Sol listeden bir ürün seçin.";
        _selectedTitleLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _selectedTitleLabel.ForeColor = Color.White;
        _selectedTitleLabel.AutoEllipsis = true;
        titleStatsPanel.Controls.Add(_selectedTitleLabel, 0, 0);

        _selectedStatsLabel.Dock = DockStyle.Fill;
        _selectedStatsLabel.Text = "Fiyat: — | ❤️ 0 Fav | 👁️ 0 Gör | LQS: —";
        _selectedStatsLabel.Font = new Font("Segoe UI", 8F);
        _selectedStatsLabel.ForeColor = Color.FromArgb(56, 189, 248);
        titleStatsPanel.Controls.Add(_selectedStatsLabel, 0, 1);

        _selectedSalesEstLabel.Dock = DockStyle.Fill;
        _selectedSalesEstLabel.Text = "EverBee Tahmini: —";
        _selectedSalesEstLabel.Font = new Font("Segoe UI Semibold", 8F);
        _selectedSalesEstLabel.ForeColor = Color.FromArgb(52, 211, 153);
        titleStatsPanel.Controls.Add(_selectedSalesEstLabel, 0, 2);

        root.Controls.Add(titleStatsPanel, 1, 0);

        // Action Buttons Row (Span both cols, Row 1)
        var btnBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 2),
        };

        _createDraftBtn.Text = "✨ Bu Ürünle AI Taslak";
        _createDraftBtn.Height = 28;
        _createDraftBtn.AutoSize = true;
        _createDraftBtn.BackColor = Color.FromArgb(37, 99, 235);
        _createDraftBtn.ForeColor = Color.White;
        _createDraftBtn.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        _createDraftBtn.FlatStyle = FlatStyle.Flat;
        _createDraftBtn.Cursor = Cursors.Hand;
        _createDraftBtn.Click += (_, _) => TransferToDraftCreator();
        btnBar.Controls.Add(_createDraftBtn);

        _sendToImageStudioBtn.Text = "🖼️ Görsel";
        _sendToImageStudioBtn.Height = 28;
        _sendToImageStudioBtn.AutoSize = true;
        _sendToImageStudioBtn.BackColor = Color.FromArgb(30, 41, 59);
        _sendToImageStudioBtn.ForeColor = Color.FromArgb(203, 213, 225);
        _sendToImageStudioBtn.Font = new Font("Segoe UI Semibold", 8F);
        _sendToImageStudioBtn.FlatStyle = FlatStyle.Flat;
        _sendToImageStudioBtn.Cursor = Cursors.Hand;
        _sendToImageStudioBtn.Margin = new Padding(4, 0, 0, 0);
        _sendToImageStudioBtn.Click += (_, _) => TransferToImageStudio();
        btnBar.Controls.Add(_sendToImageStudioBtn);

        _copyItemTagsBtn.Text = "📋 13 Tag";
        _copyItemTagsBtn.Height = 28;
        _copyItemTagsBtn.AutoSize = true;
        _copyItemTagsBtn.BackColor = Color.FromArgb(30, 41, 59);
        _copyItemTagsBtn.ForeColor = Color.FromArgb(203, 213, 225);
        _copyItemTagsBtn.Font = new Font("Segoe UI Semibold", 8F);
        _copyItemTagsBtn.FlatStyle = FlatStyle.Flat;
        _copyItemTagsBtn.Cursor = Cursors.Hand;
        _copyItemTagsBtn.Margin = new Padding(4, 0, 0, 0);
        _copyItemTagsBtn.Click += (_, _) => CopySelectedItemTags();
        btnBar.Controls.Add(_copyItemTagsBtn);

        root.Controls.Add(btnBar, 0, 1);
        root.SetColumnSpan(btnBar, 2);

        // Product Tags Chips Container
        _tagsScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };
        _selectedItemTagsContainer.Dock = DockStyle.Top;
        _selectedItemTagsContainer.AutoSize = true;
        _selectedItemTagsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _selectedItemTagsContainer.AutoScroll = false;
        _selectedItemTagsContainer.FlowDirection = FlowDirection.LeftToRight;
        _selectedItemTagsContainer.Padding = new Padding(0, 2, 0, 0);
        _tagsScroll.SetContent(_selectedItemTagsContainer);
        root.Controls.Add(_tagsScroll, 0, 2);
        root.SetColumnSpan(_tagsScroll, 2);

        group.Controls.Add(root);
        return group;
    }

    private Control BuildIntelligenceSection()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Switch Bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Panel

        // 1. Switch Bar (3-way)
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 1, 0, 1),
        };

        ConfigureSwitchButton(_btnViewTags, "🏷️ Kazandıran Tagler", isSelected: true);
        _btnViewTags.Click += (_, _) => SwitchIntelligenceView(0);

        ConfigureSwitchButton(_btnViewAudit, "📊 eRank SEO Denetimi", isSelected: false);
        _btnViewAudit.Click += (_, _) => SwitchIntelligenceView(1);

        ConfigureSwitchButton(_btnViewAiGap, "🤖 AI Strateji Raporu", isSelected: false);
        _btnViewAiGap.Click += (_, _) => SwitchIntelligenceView(2);

        bar.Controls.Add(_btnViewTags);
        bar.Controls.Add(_btnViewAudit);
        bar.Controls.Add(_btnViewAiGap);
        root.Controls.Add(bar, 0, 0);

        // 2. Intelligence Content Panel
        _intelligenceContentPanel.Dock = DockStyle.Fill;
        root.Controls.Add(_intelligenceContentPanel, 0, 1);

        // Prepare Panels
        BuildTagsPanel();
        BuildAuditPanel();
        BuildAiGapPanel();

        // Default to Tags
        SwitchIntelligenceView(0);

        return root;
    }

    private void ConfigureSwitchButton(Button btn, string text, bool isSelected)
    {
        btn.Text = text;
        btn.AutoSize = true;
        btn.Height = 26;
        btn.Padding = new Padding(8, 2, 8, 2);
        btn.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(0, 0, 4, 0);

        if (isSelected)
        {
            btn.BackColor = Color.FromArgb(37, 99, 235);
            btn.ForeColor = Color.White;
        }
        else
        {
            btn.BackColor = Color.FromArgb(30, 41, 59);
            btn.ForeColor = Color.FromArgb(203, 213, 225);
        }
    }

    private void SwitchIntelligenceView(int viewIndex)
    {
        ConfigureSwitchButton(_btnViewTags, "🏷️ Kazandıran Tagler", isSelected: viewIndex == 0);
        ConfigureSwitchButton(_btnViewAudit, "📊 eRank SEO Denetimi", isSelected: viewIndex == 1);
        ConfigureSwitchButton(_btnViewAiGap, "🤖 AI Strateji Raporu", isSelected: viewIndex == 2);

        _intelligenceContentPanel.Controls.Clear();
        if (viewIndex == 0) _intelligenceContentPanel.Controls.Add(_tagsPanel);
        else if (viewIndex == 1) _intelligenceContentPanel.Controls.Add(_auditPanel);
        else _intelligenceContentPanel.Controls.Add(_aiGapPanel);
    }

    private void BuildTagsPanel()
    {
        _tagsPanel.Dock = DockStyle.Fill;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tb = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _tagSearchTextBox.Width = 140;
        _tagSearchTextBox.Height = 24;
        _tagSearchTextBox.BackColor = Color.FromArgb(30, 41, 59);
        _tagSearchTextBox.ForeColor = Color.White;
        _tagSearchTextBox.Font = new Font("Segoe UI", 8F);
        _tagSearchTextBox.PlaceholderText = "🔍 Tag filtrele...";
        _tagSearchTextBox.TextChanged += (_, _) => FilterTags();
        tb.Controls.Add(_tagSearchTextBox);

        _copyTagsBtn.Text = "📋 Seçilileri Kopyala";
        _copyTagsBtn.Height = 24;
        _copyTagsBtn.AutoSize = true;
        _copyTagsBtn.BackColor = Color.FromArgb(30, 41, 59);
        _copyTagsBtn.ForeColor = Color.FromArgb(56, 189, 248);
        _copyTagsBtn.Font = new Font("Segoe UI Semibold", 8F);
        _copyTagsBtn.FlatStyle = FlatStyle.Flat;
        _copyTagsBtn.Cursor = Cursors.Hand;
        _copyTagsBtn.Margin = new Padding(4, 0, 0, 0);
        _copyTagsBtn.Click += (_, _) => CopySelectedTags();
        tb.Controls.Add(_copyTagsBtn);

        table.Controls.Add(tb, 0, 0);

        ConfigureTagsGrid();
        table.Controls.Add(_tagsGrid, 0, 1);

        _tagsPanel.Controls.Add(table);
    }

    private void BuildAuditPanel()
    {
        _auditPanel.Dock = DockStyle.Fill;
        _auditAnalysisText.Dock = DockStyle.Fill;
        _auditAnalysisText.ReadOnly = true;
        _auditAnalysisText.Font = new Font("Consolas", 8.5F);
        _auditAnalysisText.Text = "eRank SEO Denetimi: Bir ürün seçtiğinizde başlık uzunluğu, 13 tag eksiksizliği ve long-tail kelime oranı burada detaylandırılacaktır.";
        _auditPanel.Controls.Add(_auditAnalysisText);
    }

    private void BuildAiGapPanel()
    {
        _aiGapPanel.Dock = DockStyle.Fill;
        _gapAnalysisText.Dock = DockStyle.Fill;
        _gapAnalysisText.ReadOnly = true;
        _gapAnalysisText.Font = new Font("Consolas", 8.5F);
        _gapAnalysisText.Text = "AI Rakip Açığı Raporu: Fiyat fırsatları ve zayıf SEO ürünleri burada listelenecektir.";
        _aiGapPanel.Controls.Add(_gapAnalysisText);
    }

    private void ConfigureTagsGrid()
    {
        _tagsGrid.Dock = DockStyle.Fill;
        _tagsGrid.BackgroundColor = Color.FromArgb(20, 29, 47);
        _tagsGrid.BorderStyle = BorderStyle.None;
        _tagsGrid.RowHeadersVisible = false;
        _tagsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _tagsGrid.MultiSelect = true;
        _tagsGrid.ReadOnly = true;
        _tagsGrid.AllowUserToAddRows = false;
        _tagsGrid.EnableHeadersVisualStyles = false;
        _tagsGrid.RowTemplate.Height = 24;

        _tagsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _tagsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
        _tagsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F);
        _tagsGrid.ColumnHeadersHeight = 26;

        _tagsGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 29, 47);
        _tagsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(241, 245, 249);
        _tagsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
        _tagsGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8F);

        _tagsGrid.Columns.Clear();
        _tagsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "🏷️ Kazandıran Tag", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _tagsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Adet", Width = 45 });
        _tagsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Oran %", Width = 55 });
    }

    private void ResetCompetitorKpis()
    {
        _kpiTable.Controls.Clear();
        AddKpiCardToTable(0, "🏬 Satış", "—");
        AddKpiCardToTable(1, "⭐ Puan", "—");
        AddKpiCardToTable(2, "📦 Ürün", "—");
        AddKpiCardToTable(3, "💰 Ort. Fiyat", "—");
        AddKpiCardToTable(4, "⚡ Günlük Hız", "—", Color.FromArgb(56, 189, 248));
        AddKpiCardToTable(5, "💵 Aylık Ciro", "—", Color.FromArgb(52, 211, 153));
        AddKpiCardToTable(6, "💪 Güç Skoru", "—", Color.FromArgb(251, 146, 60));
    }

    private void AddKpiCardToTable(int col, string label, string value, Color? accentColor = null)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(col == 0 ? 0 : 2, 0, col == 6 ? 0 : 2, 0),
            Padding = new Padding(4, 2, 4, 2),
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var lbl = new Label
        {
            Text = $"{label}: ",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 0),
        };

        var val = new Label
        {
            Text = value,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = accentColor ?? Color.FromArgb(248, 250, 252),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };

        flow.Controls.Add(lbl);
        flow.Controls.Add(val);
        panel.Controls.Add(flow);
        _kpiTable.Controls.Add(panel, col, 0);
    }

    private async Task RunCompetitorAnalysisAsync()
    {
        var input = _shopInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("Lütfen bir rakip mağaza adı veya Etsy linki girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true, $"'{input}' mağazası taranıyor...");
        try
        {
            if (_thumbnailCts != null)
            {
                try { _thumbnailCts.Cancel(); } catch { }
                try { _thumbnailCts.Dispose(); } catch { }
                _thumbnailCts = null;
            }
            _thumbnailCts = new CancellationTokenSource();

            var settings = EtsyApiSettingsStore.Load();
            var analysis = await _apiClient.GetCompetitorShopAnalysisByNameOrUrlAsync(settings, input, (int)_limitInput.Value);
            _currentAnalysis = analysis;

            // Render KPI Table
            _kpiTable.Controls.Clear();
            AddKpiCardToTable(0, "🏬 Satış", analysis.Shop.TotalSales.ToString("N0"));
            AddKpiCardToTable(1, "⭐ Puan", $"{analysis.Shop.ReviewAverage:0.0} ({analysis.Shop.ReviewCount:N0})");
            AddKpiCardToTable(2, "📦 Ürün", analysis.Shop.ActiveListingCount.ToString("N0"));
            AddKpiCardToTable(3, "💰 Ort. Fiyat", $"{analysis.PriceCurrency} {analysis.AveragePrice:0.##}");
            AddKpiCardToTable(4, "⚡ Günlük Hız", $"~{analysis.EstimatedDailySalesVelocity:0.#}/Gün", Color.FromArgb(56, 189, 248));
            AddKpiCardToTable(5, "💵 Aylık Ciro", $"{analysis.PriceCurrency} ~{analysis.EstimatedMonthlyTurnover:N0}/Ay", Color.FromArgb(52, 211, 153));
            AddKpiCardToTable(6, "💪 Güç Skoru", $"{analysis.CompetitorStrengthScore}/100", Color.FromArgb(251, 146, 60));

            // Populate Tags Grid
            FilterTags();

            // AI Gap Insight
            _gapAnalysisText.Text = analysis.AiCompetitiveGapInsight;

            // Apply Filters & Populate Listings
            ApplyListingFilters();

            SetBusy(false, $"'{analysis.Shop.ShopName}' mağazası başarıyla çözümlendi ({analysis.Listings.Count} ürün analiz edildi).");

            // Lazy load thumbnails in background with real image fetching
            _ = LoadThumbnailsInBackgroundAsync(analysis.Listings, _thumbnailCts.Token);
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Hata: {ex.Message}");
            MessageBox.Show($"Rakip analizi sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyListingFilters()
    {
        if (_currentAnalysis == null) return;

        var query = _searchListingTextBox.Text.Trim();
        var sortIndex = _sortComboBox.SelectedIndex;
        var priceIndex = _priceFilterComboBox.SelectedIndex;

        var items = _currentAnalysis.Listings.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(l => l.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     l.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        if (priceIndex == 1) items = items.Where(l => l.Price <= 30m);
        else if (priceIndex == 2) items = items.Where(l => l.Price > 30m && l.Price <= 75m);
        else if (priceIndex == 3) items = items.Where(l => l.Price > 75m);

        // Sort using EverBee and eRank metrics
        items = sortIndex switch
        {
            0 => items.OrderByDescending(l => CompetitorListingAnalytics.EstimateListingSales(l, _currentAnalysis.EstimatedDailySalesVelocity, _currentAnalysis.Listings).EstMonthlySales),
            1 => items.OrderByDescending(l => CompetitorListingAnalytics.EstimateListingSales(l, _currentAnalysis.EstimatedDailySalesVelocity, _currentAnalysis.Listings).EstMonthlyRevenue),
            2 => items.OrderByDescending(l => l.Favorites),
            3 => items.OrderByDescending(l => l.Views),
            4 => items.OrderBy(l => l.SeoScore),
            _ => items.OrderByDescending(l => l.Favorites),
        };

        _filteredListings = items.ToList();
        _listingCountLabel.Text = $"{_filteredListings.Count} / {_currentAnalysis.Listings.Count} Ürün";

        _competitorGrid.Rows.Clear();
        for (int i = 0; i < _filteredListings.Count; i++)
        {
            var l = _filteredListings[i];
            var (monthlySales, monthlyRev, badge) = CompetitorListingAnalytics.EstimateListingSales(l, _currentAnalysis.EstimatedDailySalesVelocity, _currentAnalysis.Listings);
            var (grade, _, _) = CompetitorListingAnalytics.CalculateLqs(l);

            // Assign elegant dark placeholder if thumbnail not ready yet (Never show gray 'X')
            var img = l.ThumbnailImage ?? _thumbnailCache.GetOrAdd(l.ListingId, _ => CompetitorListingAnalytics.GeneratePlaceholderThumbnail(l.Title));

            var salesDisplay = !string.IsNullOrEmpty(badge) ? $"{badge} (~{monthlySales})" : $"~{monthlySales}/Ay";
            var revDisplay = $"{_currentAnalysis.PriceCurrency} ~{monthlyRev:N0}";

            _competitorGrid.Rows.Add(
                img,
                i + 1,
                l.Title,
                l.PriceDisplay,
                salesDisplay,
                revDisplay,
                l.Favorites.ToString("N0"),
                l.ViewsDisplay,
                grade
            );
        }

        if (_filteredListings.Count > 0)
        {
            _competitorGrid.Rows[0].Selected = true;
            OnListingSelected();
        }
        else
        {
            ClearSelectedProductView();
        }
    }

    private void OnListingSelected()
    {
        if (_competitorGrid.CurrentRow == null || _competitorGrid.CurrentRow.Index < 0 || _competitorGrid.CurrentRow.Index >= _filteredListings.Count || _currentAnalysis == null)
        {
            ClearSelectedProductView();
            return;
        }

        var l = _filteredListings[_competitorGrid.CurrentRow.Index];
        _selectedListing = l;

        var (monthlySales, monthlyRev, badge) = CompetitorListingAnalytics.EstimateListingSales(l, _currentAnalysis.EstimatedDailySalesVelocity, _currentAnalysis.Listings);
        var (grade, gradeColor, explanation) = CompetitorListingAnalytics.CalculateLqs(l);

        _selectedTitleLabel.Text = l.Title;
        _selectedStatsLabel.Text = $"{l.PriceDisplay} | ❤️ {l.Favorites:N0} Fav | 👁️ {l.ViewsDisplay} | LQS: {grade}";
        _selectedSalesEstLabel.Text = $"EverBee Projeksiyonu: ~{monthlySales} Adet/Ay | {_currentAnalysis.PriceCurrency} ~{monthlyRev:N0}/Ay {badge}";
        _selectedProductPic.Image = l.ThumbnailImage ?? _thumbnailCache.GetOrAdd(l.ListingId, _ => CompetitorListingAnalytics.GeneratePlaceholderThumbnail(l.Title));

        // Render chips for this item's tags
        _selectedItemTagsContainer.Controls.Clear();
        foreach (var tag in l.Tags)
        {
            var chip = new Label
            {
                Text = tag,
                AutoSize = true,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 7.5F),
                Padding = new Padding(5, 2, 5, 2),
                Margin = new Padding(0, 0, 3, 3),
                Cursor = Cursors.Hand,
            };
            chip.Click += (_, _) =>
            {
                Clipboard.SetText(tag);
                MessageBox.Show($"'{tag}' panoya kopyalandı!", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _selectedItemTagsContainer.Controls.Add(chip);
        }
        _tagsScroll?.RecalculateScroll();

        // Render eRank Audit for this item
        var sbAudit = new StringBuilder();
        sbAudit.AppendLine($"📊 eRANK LISTING QUALITY SCORE (LQS): {grade} NOTU");
        sbAudit.AppendLine(new string('-', 55));
        sbAudit.AppendLine($"• Değerlendirme: {explanation}");
        sbAudit.AppendLine($"• Başlık Uzunluğu: {l.Title.Length} karakter (İdeal: 120-140 karakter)");
        if (l.Title.Length < 100) sbAudit.AppendLine("  ⚠️ Başlık kısa kalmış! 140 karakter altın formülümüzle bu ürünü rahatça geçebilirsiniz.");
        sbAudit.AppendLine($"• Etiket Sayısı: {l.Tags.Count}/13 etiket kullanılmış.");
        if (l.Tags.Count < 13) sbAudit.AppendLine($"  ⚠️ {13 - l.Tags.Count} adet etiket eksik bırakılmış! Etsy 13 etiketin tamamını zorunlu görür.");
        var multiWords = l.Tags.Count(t => t.Trim().Contains(' '));
        sbAudit.AppendLine($"• Long-Tail Tag Oranı: {multiWords}/{l.Tags.Count} tanesi 2-3 kelimelik zengin arama terimi.");
        sbAudit.AppendLine();
        sbAudit.AppendLine($"• EverBee Tahmini: Aylık ~{monthlySales} adet satış, ~{monthlyRev:N0} dolar ciro.");
        _auditAnalysisText.Text = sbAudit.ToString();
    }

    private void ClearSelectedProductView()
    {
        _selectedListing = null;
        _selectedTitleLabel.Text = "Sol listeden bir ürün seçin.";
        _selectedStatsLabel.Text = "Fiyat: — | Favori: — | Görüntülenme: — | LQS: —";
        _selectedSalesEstLabel.Text = "EverBee Projeksiyonu: —";
        _selectedProductPic.Image = null;
        _selectedItemTagsContainer.Controls.Clear();
        _tagsScroll?.RecalculateScroll();
        _auditAnalysisText.Text = "Bir ürün seçtiğinizde eRank kalite ve SEO denetimi burada listelenir.";
    }

    private async Task LoadThumbnailsInBackgroundAsync(List<MarketListingResult> listings, CancellationToken cancellationToken)
    {
        var settings = EtsyApiSettingsStore.Load();
        using var semaphore = new SemaphoreSlim(4);

        var tasks = listings.Select(async listing =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                // 1. Check if we already have the URL or need to fetch via Etsy API
                var imageUrl = listing.ImageUrl;
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    if (_imageUrlCache.TryGetValue(listing.ListingId, out var cachedUrl))
                    {
                        imageUrl = cachedUrl;
                    }
                    else if (listing.ListingId > 0)
                    {
                        try
                        {
                            var urls = await _apiClient.GetListingImagesAsync(settings, listing.ListingId, cancellationToken);
                            if (urls.Count > 0)
                            {
                                imageUrl = urls[0];
                                listing.ImageUrls = urls;
                                listing.ImageUrl = imageUrl;
                                _imageUrlCache[listing.ListingId] = imageUrl;
                            }
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrWhiteSpace(imageUrl)) return;

                // 2. Download Image Stream
                await using var stream = await _imageHttpClient.GetStreamAsync(imageUrl, cancellationToken);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                using var bmp = Image.FromStream(memory);
                var thumb = new Bitmap(bmp, new Size(50, 50));
                listing.ThumbnailImage = thumb;
                _thumbnailCache[listing.ListingId] = thumb;

                if (IsDisposed || cancellationToken.IsCancellationRequested) return;

                BeginInvoke(() =>
                {
                    for (int i = 0; i < _filteredListings.Count; i++)
                    {
                        if (_filteredListings[i].ListingId == listing.ListingId)
                        {
                            _competitorGrid.Rows[i].Cells[0].Value = thumb;
                            if (ReferenceEquals(_selectedListing, listing))
                            {
                                _selectedProductPic.Image = thumb;
                            }
                            break;
                        }
                    }
                });
            }
            catch
            {
                // Network or cancellation ignore
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch { }
    }

    private void TransferToDraftCreator()
    {
        if (_selectedListing == null)
        {
            MessageBox.Show("Lütfen taslağa dönüştürmek için önce bir ürün seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var optimizer = _aiOptimizer ?? new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService());
        var form = new ProductDiscoveryListingCreatorForm(optimizer, initialListing: _selectedListing);

        if (DashboardForm.Instance != null)
        {
            DashboardForm.Instance.EmbedModuleForm(form);
        }
        else
        {
            form.ShowDialog(this);
        }
    }

    private void TransferToImageStudio()
    {
        if (_selectedListing == null)
        {
            MessageBox.Show("Lütfen önce bir ürün seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? tempImagePath = null;
        if (_selectedListing.ThumbnailImage != null)
        {
            try
            {
                tempImagePath = Path.Combine(Path.GetTempPath(), $"competitor_{_selectedListing.ListingId}.png");
                _selectedListing.ThumbnailImage.Save(tempImagePath);
            }
            catch { }
        }

        var form = new AiListingImageForm(_aiOptimizer, tempImagePath, _selectedListing.Title);
        if (DashboardForm.Instance != null)
        {
            DashboardForm.Instance.EmbedModuleForm(form);
        }
        else
        {
            form.ShowDialog(this);
        }
    }

    private void CopySelectedItemTags()
    {
        if (_selectedListing == null || _selectedListing.Tags.Count == 0)
        {
            MessageBox.Show("Seçili ürünün etiketi bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var tags = string.Join(", ", _selectedListing.Tags);
        Clipboard.SetText(tags);
        MessageBox.Show($"Bu ürünün {_selectedListing.Tags.Count} etiketi panoya kopyalandı:\n\n{tags}", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void FilterTags()
    {
        if (_currentAnalysis == null) return;
        var query = _tagSearchTextBox.Text.Trim();
        var tags = _currentAnalysis.TopTags.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            tags = tags.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _tagsGrid.Rows.Clear();
        foreach (var tag in tags)
        {
            _tagsGrid.Rows.Add(tag.Name, tag.Count, tag.PercentageDisplay);
        }
    }

    private void CopySelectedTags()
    {
        var selected = new List<string>();
        foreach (DataGridViewRow row in _tagsGrid.SelectedRows)
        {
            if (row.Cells[0].Value is string tag && !string.IsNullOrWhiteSpace(tag))
                selected.Add(tag);
        }

        if (selected.Count == 0)
        {
            foreach (DataGridViewRow row in _tagsGrid.Rows)
            {
                if (row.Cells[0].Value is string tag && !string.IsNullOrWhiteSpace(tag))
                {
                    selected.Add(tag);
                    if (selected.Count >= 13) break;
                }
            }
        }

        if (selected.Count > 0)
        {
            Clipboard.SetText(string.Join(", ", selected));
            MessageBox.Show($"{selected.Count} adet tag panoya kopyalandı:\n\n{string.Join(", ", selected)}", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportCompetitorCsv()
    {
        if (_currentAnalysis == null || _currentAnalysis.Listings.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"{_currentAnalysis.Shop.ShopName}_everbee_erank_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Rank,Title,Price,EstMonthlySales,EstMonthlyRevenue,Favorites,Views,LQS,Tags,ListingUrl");
            int rank = 1;
            foreach (var l in _currentAnalysis.Listings.OrderByDescending(x => x.Favorites))
            {
                var (mSales, mRev, _) = CompetitorListingAnalytics.EstimateListingSales(l, _currentAnalysis.EstimatedDailySalesVelocity, _currentAnalysis.Listings);
                var (grade, _, _) = CompetitorListingAnalytics.CalculateLqs(l);
                sb.AppendLine($"{rank++},\"{l.Title.Replace("\"", "\"\"")}\",{l.Price},{mSales},{mRev},{l.Favorites},{l.Views},{grade},\"{string.Join(";", l.Tags)}\",\"{l.ListingUrl}\"");
            }
            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("EverBee & eRank destekli CSV başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    #endregion

    #region Tab 2: Seasonal Trend Radar Implementation

    private void BuildTrendTab()
    {
        _trendPanel.Dock = DockStyle.Fill;
        _trendPanel.BackColor = Color.FromArgb(15, 23, 42);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Toolbar
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); // Seasonal Trend Calendar Cards
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Niche Opportunity Evaluation
        _trendPanel.Controls.Add(table);

        table.Controls.Add(BuildTrendToolbar(), 0, 0);

        _trendEventsContainer.Dock = DockStyle.Fill;
        _trendEventsContainer.FlowDirection = FlowDirection.LeftToRight;
        _trendEventsContainer.WrapContents = false;
        _trendEventsContainer.AutoScroll = true;
        _trendEventsContainer.Padding = new Padding(0, 2, 0, 4);
        table.Controls.Add(_trendEventsContainer, 0, 1);

        table.Controls.Add(BuildNicheOpportunityArea(), 0, 2);
    }

    private Control BuildTrendToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var lbl = new Label
        {
            Text = "Trend / Niş Kelime Araştır:",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(0, 6, 6, 0),
        };
        toolbar.Controls.Add(lbl);

        _trendKeywordInput.Width = 260;
        _trendKeywordInput.Height = 30;
        _trendKeywordInput.BackColor = Color.FromArgb(30, 41, 59);
        _trendKeywordInput.ForeColor = Color.White;
        _trendKeywordInput.Font = new Font("Segoe UI", 9.5F);
        _trendKeywordInput.PlaceholderText = "Örn: halloween mug, leather wallet";
        _trendKeywordInput.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunTrendNicheAnalysisAsync(); } };
        toolbar.Controls.Add(_trendKeywordInput);

        _trendSearchBtn.Text = "🚀 Fırsatını Analiz Et";
        _trendSearchBtn.AutoSize = true;
        _trendSearchBtn.Height = 30;
        _trendSearchBtn.BackColor = Color.FromArgb(37, 99, 235);
        _trendSearchBtn.ForeColor = Color.White;
        _trendSearchBtn.Font = new Font("Segoe UI Semibold", 9F);
        _trendSearchBtn.FlatStyle = FlatStyle.Flat;
        _trendSearchBtn.Cursor = Cursors.Hand;
        _trendSearchBtn.Margin = new Padding(8, 0, 0, 0);
        _trendSearchBtn.Click += async (_, _) => await RunTrendNicheAnalysisAsync();
        toolbar.Controls.Add(_trendSearchBtn);

        return toolbar;
    }

    private void LoadSeasonalCalendar()
    {
        _trendEventsContainer.Controls.Clear();
        var events = _trendService.GetActiveSeasonalTrends();
        foreach (var ev in events)
        {
            var card = CreateTrendEventCard(ev);
            _trendEventsContainer.Controls.Add(card);
        }
    }

    private Control CreateTrendEventCard(SeasonalTrendItem ev)
    {
        var card = new Panel
        {
            Width = 260,
            Height = 175,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(8),
            Cursor = Cursors.Hand,
        };

        var box = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Title & Icon
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // Status Pill
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); // Date Range
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Keywords & Action

        var titleLabel = new Label
        {
            Text = $"{ev.Icon} {ev.Title}",
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
        };
        box.Controls.Add(titleLabel, 0, 0);

        var statusBadge = new Label
        {
            Text = ev.Status,
            Font = new Font("Segoe UI Semibold", 7.5F),
            ForeColor = ColorTranslator.FromHtml(ev.StatusColorHex),
            Dock = DockStyle.Fill,
        };
        box.Controls.Add(statusBadge, 0, 1);

        var dateLabel = new Label
        {
            Text = $"📅 {ev.DateRangeDisplay}",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
        };
        box.Controls.Add(dateLabel, 0, 2);

        var kwLabel = new Label
        {
            Text = $"🔥 Trendler: {string.Join(", ", ev.HighDemandKeywords.Take(4))}",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(203, 213, 225),
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
        };
        box.Controls.Add(kwLabel, 0, 3);

        card.Controls.Add(box);

        card.Click += async (_, _) =>
        {
            if (ev.HighDemandKeywords.Count > 0)
            {
                _trendKeywordInput.Text = ev.HighDemandKeywords[0];
                await RunTrendNicheAnalysisAsync();
            }
        };

        return card;
    }

    private Control BuildNicheOpportunityArea()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // KPI Strip
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Columns
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Advice Box

        // 1. KPI Strip
        _trendKpiPillContainer.Dock = DockStyle.Fill;
        _trendKpiPillContainer.FlowDirection = FlowDirection.LeftToRight;
        _trendKpiPillContainer.WrapContents = false;
        _trendKpiPillContainer.Padding = new Padding(0, 1, 0, 1);
        root.Controls.Add(_trendKpiPillContainer, 0, 0);
        ResetTrendKpiPills();

        // 2. 2-Column Box
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Column 1: Top 13 Long-tail Tags
        var tagsGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = " 🏷️ En Çok Kazandıran 13 Long-Tail Tag (Etsy Trendi) ",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8.5F),
        };
        var tagTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        tagTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tagTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        _winningTagsListBox.Dock = DockStyle.Fill;
        _winningTagsListBox.BackColor = Color.FromArgb(20, 29, 47);
        _winningTagsListBox.ForeColor = Color.FromArgb(241, 245, 249);
        _winningTagsListBox.Font = new Font("Segoe UI", 8.5F);
        _winningTagsListBox.BorderStyle = BorderStyle.None;
        tagTable.Controls.Add(_winningTagsListBox, 0, 0);

        _copyTrendTagsBtn.Text = "📋 13 Trend Tag'i Kopyala";
        _copyTrendTagsBtn.Dock = DockStyle.Fill;
        _copyTrendTagsBtn.FlatStyle = FlatStyle.Flat;
        _copyTrendTagsBtn.BackColor = Color.FromArgb(30, 41, 59);
        _copyTrendTagsBtn.ForeColor = Color.FromArgb(56, 189, 248);
        _copyTrendTagsBtn.Font = new Font("Segoe UI Semibold", 8F);
        _copyTrendTagsBtn.Click += (_, _) =>
        {
            var tags = _winningTagsListBox.Items.Cast<string>().ToList();
            if (tags.Count > 0)
            {
                Clipboard.SetText(string.Join(", ", tags));
                MessageBox.Show("13 adet trend tag panoya kopyalandı!", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        tagTable.Controls.Add(_copyTrendTagsBtn, 0, 1);
        tagsGroup.Controls.Add(tagTable);
        columns.Controls.Add(tagsGroup, 0, 0);

        // Column 2: Top Selling Titles & AI Draft
        var titlesGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = " 🏆 Zirvedeki Rakip Başlıkları & Hızlı Taslak ",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 8.5F),
        };
        var titlesTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        titlesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titlesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        _topTitlesListBox.Dock = DockStyle.Fill;
        _topTitlesListBox.BackColor = Color.FromArgb(20, 29, 47);
        _topTitlesListBox.ForeColor = Color.FromArgb(241, 245, 249);
        _topTitlesListBox.Font = new Font("Segoe UI", 8.5F);
        _topTitlesListBox.BorderStyle = BorderStyle.None;
        titlesTable.Controls.Add(_topTitlesListBox, 0, 0);

        _createDraftFromTrendBtn.Text = "✨ Bu Trendle AI Taslak Oluştur";
        _createDraftFromTrendBtn.Dock = DockStyle.Fill;
        _createDraftFromTrendBtn.FlatStyle = FlatStyle.Flat;
        _createDraftFromTrendBtn.BackColor = Color.FromArgb(37, 99, 235);
        _createDraftFromTrendBtn.ForeColor = Color.White;
        _createDraftFromTrendBtn.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        _createDraftFromTrendBtn.Click += (_, _) =>
        {
            var kw = _trendKeywordInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw)) return;
            var optimizer = _aiOptimizer ?? new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService());
            var form = new ProductDiscoveryListingCreatorForm(optimizer, initialKeyword: kw);
            if (DashboardForm.Instance != null) DashboardForm.Instance.EmbedModuleForm(form);
            else form.ShowDialog(this);
        };
        titlesTable.Controls.Add(_createDraftFromTrendBtn, 0, 1);
        titlesGroup.Controls.Add(titlesTable);
        columns.Controls.Add(titlesGroup, 1, 0);

        root.Controls.Add(columns, 0, 1);

        _trendAdviceText.Dock = DockStyle.Fill;
        _trendAdviceText.Multiline = true;
        _trendAdviceText.ReadOnly = true;
        _trendAdviceText.BackColor = Color.FromArgb(30, 41, 59);
        _trendAdviceText.ForeColor = Color.FromArgb(52, 211, 153);
        _trendAdviceText.Font = new Font("Segoe UI Semibold", 9F);
        _trendAdviceText.Text = "Yukarıdan bir takvim kartına tıklayın veya arama kutusuna 'halloween mug' gibi bir niş kelime yazıp fırsatı analiz edin.";
        root.Controls.Add(_trendAdviceText, 0, 2);

        return root;
    }

    private void ResetTrendKpiPills()
    {
        _trendKpiPillContainer.Controls.Clear();
        AddTrendPill("🎯 Fırsat Skoru", "—", Color.FromArgb(52, 211, 153));
        AddTrendPill("🔥 Talep Düzeyi", "—", Color.FromArgb(239, 68, 68));
        AddTrendPill("⚔️ Rekabet Skoru", "—", Color.FromArgb(251, 146, 60));
        AddTrendPill("💰 Ort. Fiyat", "—");
        AddTrendPill("❤️ Ort. Favori", "—");
    }

    private void AddTrendPill(string label, string value, Color? accentColor = null)
    {
        var panel = new Panel
        {
            AutoSize = true,
            Height = 28,
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(6, 3, 6, 3),
            BackColor = Color.FromArgb(30, 41, 59),
        };

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var lbl = new Label
        {
            Text = $"{label}: ",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };

        var val = new Label
        {
            Text = value,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = accentColor ?? Color.FromArgb(248, 250, 252),
            AutoSize = true,
        };

        flow.Controls.Add(lbl);
        flow.Controls.Add(val);
        panel.Controls.Add(flow);
        _trendKpiPillContainer.Controls.Add(panel);
    }

    private async Task RunTrendNicheAnalysisAsync()
    {
        var kw = _trendKeywordInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(kw))
        {
            MessageBox.Show("Lütfen analiz edilecek bir niş veya trend anahtar kelime girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true, $"'{kw}' kelimesinde Etsy pazar verileri toplanıyor...");
        try
        {
            var settings = EtsyApiSettingsStore.Load();
            var listings = await _apiClient.FindMarketListingsAsync(settings, kw, 40);

            var samples = listings.Select(l => new NicheListingSample(l.Title, l.Price, l.Favorites, l.Views, l.Tags)).ToList();
            var result = _trendService.AnalyzeNicheOpportunity(kw, samples);

            _trendKpiPillContainer.Controls.Clear();
            AddTrendPill("🎯 Fırsat Skoru", $"{result.OpportunityScore}/100", Color.FromArgb(52, 211, 153));
            AddTrendPill("🔥 Talep Düzeyi", $"{result.DemandScore}/100", Color.FromArgb(239, 68, 68));
            AddTrendPill("⚔️ Rekabet Skoru", $"{result.CompetitionScore}/100", Color.FromArgb(251, 146, 60));
            AddTrendPill("💰 Ort. Fiyat", $"${result.AveragePrice:0.##}");
            AddTrendPill("❤️ Ort. Favori", result.AverageFavorites.ToString("N0"));

            _winningTagsListBox.Items.Clear();
            foreach (var tag in result.TopWinningTags)
            {
                _winningTagsListBox.Items.Add(tag);
            }

            _topTitlesListBox.Items.Clear();
            foreach (var title in result.HighOpportunityTitles)
            {
                _topTitlesListBox.Items.Add(title);
            }

            _trendAdviceText.Text = result.Recommendation;

            SetBusy(false, $"'{kw}' analizi tamamlandı. Fırsat Skoru: {result.OpportunityScore}/100");
        }
        catch (Exception ex)
        {
            SetBusy(false, $"Hata: {ex.Message}");
            MessageBox.Show($"Trend analizi sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    private void SetBusy(bool busy, string? status = null)
    {
        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;
        if (ParentForm != null) ParentForm.UseWaitCursor = false;
        if (TopLevelControl is Form top) top.Cursor = Cursors.Default;

        _statusLabel.Text = status ?? (busy ? "İşlem yapılıyor..." : "Hazır");
        _spyShopBtn.Enabled = !busy;
        _trendSearchBtn.Enabled = !busy;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        {
            try
            {
                if (_thumbnailCts != null)
                {
                    try { _thumbnailCts.Cancel(); } catch { }
                    try { _thumbnailCts.Dispose(); } catch { }
                    _thumbnailCts = null;
                }
            }
            catch { }

            try { _imageHttpClient.Dispose(); } catch { }
        }
        _isDisposed = true;
        base.Dispose(disposing);
    }
}
