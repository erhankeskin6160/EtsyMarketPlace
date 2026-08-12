namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.Dashboard;
using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Domain.Tracking;
using EtsyMarketPlace.Infrastructure.Automation;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

internal sealed class DashboardForm : Form
{
    private readonly AnalyzeKeywordUseCase _keywordUseCase;
    private readonly TrackingService _trackingService;
    private readonly DashboardService _dashboardService;
    private readonly ShopPerformanceService _shopPerformanceService;
    private readonly ShopPerformanceHistoryService _shopPerformanceHistoryService;
    private readonly IAutomationSettingsStore _automationSettingsStore;
    private readonly AutomationScheduler _automationScheduler;
    private readonly WindowsTaskSchedulerService _windowsTaskScheduler;
    private readonly ListingOptimizationHistoryService _optimizationHistoryService;
    private readonly IAiListingOptimizer _aiListingOptimizer;
    private readonly Label _statusLabel = new();
    private readonly Dictionary<string, Label> _kpis = [];
    private readonly DataGridView _opportunitiesGrid = new();
    private readonly DataGridView _changesGrid = new();
    private readonly ComboBox _trendComboBox = new();
    private readonly TrendChartControl _trendChart = new();
    private DashboardOverview? _overview;

    private readonly AbTestService _abTestService;
    private readonly BatchQueueProcessorService _batchQueueProcessorService;

    public DashboardForm(
        AnalyzeKeywordUseCase keywordUseCase,
        TrackingService trackingService,
        DashboardService dashboardService,
        ShopPerformanceService shopPerformanceService,
        ShopPerformanceHistoryService shopPerformanceHistoryService,
        IAutomationSettingsStore automationSettingsStore,
        AutomationScheduler automationScheduler,
        WindowsTaskSchedulerService windowsTaskScheduler,
        ListingOptimizationHistoryService optimizationHistoryService,
        IAiListingOptimizer aiListingOptimizer,
        AbTestService? abTestService = null,
        BatchQueueProcessorService? batchQueueProcessorService = null)
    {
        _keywordUseCase = keywordUseCase;
        _trackingService = trackingService;
        _dashboardService = dashboardService;
        _shopPerformanceService = shopPerformanceService;
        _shopPerformanceHistoryService = shopPerformanceHistoryService;
        _automationSettingsStore = automationSettingsStore;
        _automationScheduler = automationScheduler;
        _windowsTaskScheduler = windowsTaskScheduler;
        _optimizationHistoryService = optimizationHistoryService;
        _aiListingOptimizer = aiListingOptimizer;
        _abTestService = abTestService ?? CreateDefaultAbTestService();
        _batchQueueProcessorService = batchQueueProcessorService ?? CreateDefaultBatchQueueProcessorService();
        BuildLayout();
        Shown += async (_, _) => await LoadDashboardAsync();
    }

    private void BuildLayout()
    {
        Text = "Etsy Market Place - Kontrol Paneli";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Pazar Kontrol Paneli",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Yerel pazar verileri yukleniyor";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildActionHub(), 0, 1);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 0, 0, 8) };
        for (var column = 0; column < 5; column++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        UiStyle.AddKpiCard(kpis, 0, 0, "Toplam takip", "total", _kpis);
        UiStyle.AddKpiCard(kpis, 1, 0, "Urun", "listings", _kpis);
        UiStyle.AddKpiCard(kpis, 2, 0, "Magaza", "shops", _kpis);
        UiStyle.AddKpiCard(kpis, 3, 0, "Anahtar kelime", "keywords", _kpis);
        UiStyle.AddKpiCard(kpis, 4, 0, "Snapshot", "snapshots", _kpis);
        root.Controls.Add(kpis, 0, 2);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        content.Controls.Add(BuildGridSection("En iyi anahtar kelime firsatlari", _opportunitiesGrid, ConfigureOpportunitiesGrid), 0, 0);
        content.Controls.Add(BuildGridSection("En buyuk degisimler", _changesGrid, ConfigureChangesGrid), 1, 0);
        var trend = BuildTrendSection();
        content.Controls.Add(trend, 0, 1);
        content.SetColumnSpan(trend, 2);
        root.Controls.Add(content, 0, 3);
    }

    private Control BuildTrendSection()
    {
        var card = new SimilarProductsWinForms.Controls.ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Padding = new Padding(12),
            CornerRadius = 12,
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "TAKİP TRENDİ", Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold), ForeColor = UiStyle.TextDark, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _trendComboBox.Dock = DockStyle.Fill;
        _trendComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _trendComboBox.SelectedIndexChanged += (_, _) => _trendChart.SetHistory((_trendComboBox.SelectedItem as TrendOption)?.History);
        toolbar.Controls.Add(_trendComboBox, 1, 0);
        panel.Controls.Add(toolbar, 0, 0);
        _trendChart.Dock = DockStyle.Fill;
        panel.Controls.Add(_trendChart, 0, 1);
        card.Controls.Add(panel);
        return card;
    }

    private Control BuildActionHub()
    {
        var hub = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0, 4, 0, 10),
        };
        hub.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        hub.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        var primary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (var index = 0; index < 4; index++) primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        primary.Controls.Add(CreatePrimaryButton("Urun Bul ve Taslak"), 0, 0);
        ((Button)primary.GetControlFromPosition(0, 0)!).Click += (_, _) => OpenCreator();
        primary.Controls.Add(CreatePrimaryButton("Magazam"), 1, 0);
        ((Button)primary.GetControlFromPosition(1, 0)!).Click += (_, _) => OpenOwnShop();
        primary.Controls.Add(CreatePrimaryButton("Pazar Arastir"), 2, 0);
        ((Button)primary.GetControlFromPosition(2, 0)!).Click += async (_, _) => await OpenResearchAsync();
        primary.Controls.Add(CreatePrimaryButton("Otomasyon"), 3, 0);
        ((Button)primary.GetControlFromPosition(3, 0)!).Click += (_, _) => OpenAutomation();
        hub.Controls.Add(primary, 0, 0);

        var secondary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1 };
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 1; index < 8; index++) secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        secondary.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        var batchQueue = UiStyle.CreateButton("Toplu İşlem");
        batchQueue.Click += (_, _) => OpenBatchQueue();
        secondary.Controls.Add(batchQueue, 1, 0);
        var profitCalc = UiStyle.CreateButton("Kâr Simülatörü");
        profitCalc.Click += (_, _) => OpenProfitCalc();
        secondary.Controls.Add(profitCalc, 2, 0);
        var abTest = UiStyle.CreateButton("A/B Test");
        abTest.Click += (_, _) => OpenAbTest();
        secondary.Controls.Add(abTest, 3, 0);
        var tracking = UiStyle.CreateButton("Takip");
        tracking.Click += async (_, _) => await OpenTrackingAsync();
        secondary.Controls.Add(tracking, 4, 0);
        var api = UiStyle.CreateButton("API");
        api.Click += (_, _) => { using var form = new EtsyApiSettingsForm(); form.ShowDialog(this); };
        secondary.Controls.Add(api, 5, 0);
        var refresh = UiStyle.CreateButton("Yenile");
        refresh.Click += async (_, _) => await LoadDashboardAsync();
        secondary.Controls.Add(refresh, 6, 0);
        var exit = UiStyle.CreateButton("Cikis", isSecondary: true);
        exit.Click += (_, _) => Close();
        secondary.Controls.Add(exit, 7, 0);
        hub.Controls.Add(secondary, 0, 1);

        return hub;
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            _statusLabel.Text = "Dashboard hesaplanıyor...";
            _overview = await _dashboardService.GetOverviewAsync();
            SetKpi("total", _overview.TrackedItemCount);
            SetKpi("listings", _overview.ListingCount);
            SetKpi("shops", _overview.ShopCount);
            SetKpi("keywords", _overview.KeywordCount);
            SetKpi("snapshots", _overview.SnapshotCount);
            _opportunitiesGrid.DataSource = _overview.TopOpportunities.Select(item => new OpportunityRow(item)).ToList();
            _changesGrid.DataSource = _overview.BiggestChanges.Select(item => new ChangeRow(item)).ToList();
            _trendComboBox.DataSource = _overview.Histories.Select(item => new TrendOption(item)).ToList();
            _trendChart.SetHistory((_trendComboBox.SelectedItem as TrendOption)?.History);
            _statusLabel.Text = $"{_overview.TrackedItemCount} takip | {DateTime.Now:dd.MM.yyyy HH:mm}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kontrol Paneli", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Dashboard yuklenemedi";
        }
    }

    private async Task OpenResearchAsync()
    {
        using var form = new MarketResearchForm(
            _keywordUseCase,
            _trackingService,
            _optimizationHistoryService,
            _aiListingOptimizer);
        form.ShowDialog(this);
        await LoadDashboardAsync();
    }

    private void OpenCreator()
    {
        using var form = new ProductDiscoveryListingCreatorForm(
            _aiListingOptimizer,
            historyService: _optimizationHistoryService);
        form.ShowDialog(this);
    }

    private void OpenOwnShop()
    {
        using var form = new OwnShopPerformanceForm(
            _shopPerformanceService,
            _shopPerformanceHistoryService,
            _aiListingOptimizer,
            _optimizationHistoryService);
        form.ShowDialog(this);
    }

    private void OpenAutomation()
    {
        using var form = new AutomationReportingForm(
            _automationSettingsStore,
            _automationScheduler,
            _windowsTaskScheduler);
        form.ShowDialog(this);
    }

    private void OpenBatchQueue()
    {
        using var form = new BatchQueueForm(_batchQueueProcessorService);
        form.ShowDialog(this);
    }

    private void OpenProfitCalc()
    {
        using var form = new ProfitCalculatorForm();
        form.ShowDialog(this);
    }

    private void OpenAbTest()
    {
        using var form = new ListingAbTestForm(_abTestService);
        form.ShowDialog(this);
    }

    private async Task OpenTrackingAsync()
    {
        using var form = new TrackingHistoryForm(_trackingService);
        form.ShowDialog(this);
        await LoadDashboardAsync();
    }

    private void ConfigureOpportunitiesGrid()
    {
        UiStyle.ConfigureBaseGrid(_opportunitiesGrid);
        AddColumn(_opportunitiesGrid, "Anahtar kelime", nameof(OpportunityRow.Keyword), 220, true);
        AddColumn(_opportunitiesGrid, "Firsat", nameof(OpportunityRow.Opportunity), 75);
        AddColumn(_opportunitiesGrid, "Talep", nameof(OpportunityRow.Demand), 75);
        AddColumn(_opportunitiesGrid, "Rekabet", nameof(OpportunityRow.Competition), 80);
        AddColumn(_opportunitiesGrid, "Tarih", nameof(OpportunityRow.Captured), 130);
    }

    private void ConfigureChangesGrid()
    {
        UiStyle.ConfigureBaseGrid(_changesGrid);
        AddColumn(_changesGrid, "Takip edilen", nameof(ChangeRow.DisplayName), 230, true);
        AddColumn(_changesGrid, "Tur", nameof(ChangeRow.Type), 70);
        AddColumn(_changesGrid, "Metrik", nameof(ChangeRow.Metric), 105);
        AddColumn(_changesGrid, "Onceki", nameof(ChangeRow.Previous), 75);
        AddColumn(_changesGrid, "Son", nameof(ChangeRow.Latest), 75);
        AddColumn(_changesGrid, "Fark", nameof(ChangeRow.Difference), 75);
        AddColumn(_changesGrid, "Tarih", nameof(ChangeRow.Captured), 125);
    }

    private static Control BuildGridSection(string title, DataGridView grid, Action configure)
    {
        var card = new SimilarProductsWinForms.Controls.ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Padding = new Padding(12),
            CornerRadius = 12,
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold), ForeColor = UiStyle.TextDark, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        configure();
        panel.Controls.Add(grid, 0, 1);
        card.Controls.Add(panel);
        return card;
    }

    private void SetKpi(string key, int value) => _kpis[key].Text = value.ToString("N0");
    private static void AddColumn(DataGridView grid, string header, string property, int width, bool fill = false) => grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, Width = width, AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None });

    private static Button CreatePrimaryButton(string text)
    {
        var button = UiStyle.CreateButton(text);
        button.Font = new Font("Segoe UI Semibold", 12F);
        button.Margin = new Padding(0, 2, 8, 6);
        button.AutoEllipsis = false;
        return button;
    }

    private static AbTestService CreateDefaultAbTestService()
    {
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var repo = new EtsyMarketPlace.Infrastructure.AbTesting.SqliteAbTestRepository(databasePath);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return new AbTestService(repo);
    }

    private BatchQueueProcessorService CreateDefaultBatchQueueProcessorService()
    {
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var repo = new EtsyMarketPlace.Infrastructure.BatchQueue.SqliteBatchQueueRepository(databasePath);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return new BatchQueueProcessorService(repo, _aiListingOptimizer);
    }

    private sealed class OpportunityRow(DashboardOpportunity item)
    {
        public string Keyword => item.Keyword;
        public string Opportunity => $"{item.OpportunityScore}/100";
        public string Demand => $"{item.DemandScore}/100";
        public string Competition => $"{item.CompetitionScore}/100";
        public string Captured => item.CapturedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    }
    private sealed class ChangeRow(DashboardChange item)
    {
        public string DisplayName => item.DisplayName;
        public string Type => item.EntityType switch { TrackingEntityType.Listing => "Urun", TrackingEntityType.Shop => "Magaza", _ => "Kelime" };
        public string Metric => item.MetricName;
        public string Previous => item.PreviousValue.ToString("0.##");
        public string Latest => item.LatestValue.ToString("0.##");
        public string Difference => item.Difference.ToString("+0.##;-0.##;0");
        public string Captured => item.CapturedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    }
    private sealed class TrendOption(TrackingHistory history)
    {
        public TrackingHistory History => history;
        public override string ToString() => $"{history.Item.EntityType}: {history.Item.DisplayName}";
    }
}
