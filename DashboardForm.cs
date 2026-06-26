namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.Dashboard;
using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Application.ListingOptimization;
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
        IAiListingOptimizer aiListingOptimizer)
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
        BuildLayout();
        Shown += async (_, _) => await LoadDashboardAsync();
    }

    private void BuildLayout()
    {
        Text = "Etsy Market Place - Kontrol Paneli";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
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
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Yerel pazar verileri yukleniyor";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 8),
        };
        nav.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 1; index < 10; index++) nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        nav.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        var research = CreateButton("Pazar Arastirma");
        research.Click += async (_, _) => await OpenResearchAsync();
        nav.Controls.Add(research, 1, 0);
        var ownShop = CreateButton("Kendi Magazam");
        ownShop.Click += (_, _) =>
        {
            using var form = new OwnShopPerformanceForm(
                _shopPerformanceService,
                _shopPerformanceHistoryService,
                _aiListingOptimizer,
                _optimizationHistoryService);
            form.ShowDialog(this);
        };
        nav.Controls.Add(ownShop, 2, 0);
        var creator = CreateButton("Urun Uret");
        creator.Click += (_, _) =>
        {
            using var form = new ProductDiscoveryListingCreatorForm(_aiListingOptimizer);
            form.ShowDialog(this);
        };
        nav.Controls.Add(creator, 3, 0);
        var automation = CreateButton("Otomasyon");
        automation.Click += (_, _) =>
        {
            using var form = new AutomationReportingForm(
                _automationSettingsStore,
                _automationScheduler,
                _windowsTaskScheduler);
            form.ShowDialog(this);
        };
        nav.Controls.Add(automation, 4, 0);
        var optimization = CreateButton("AI Optimizasyon");
        optimization.Click += (_, _) =>
        {
            using var form = new ListingOptimizationForm(_optimizationHistoryService, _aiListingOptimizer);
            form.ShowDialog(this);
        };
        nav.Controls.Add(optimization, 5, 0);
        var tracking = CreateButton("Takip Merkezi");
        tracking.Click += async (_, _) => await OpenTrackingAsync();
        nav.Controls.Add(tracking, 6, 0);
        var api = CreateButton("API Ayarlari");
        api.Click += (_, _) => { using var form = new EtsyApiSettingsForm(); form.ShowDialog(this); };
        nav.Controls.Add(api, 7, 0);
        var refresh = CreateButton("Yenile");
        refresh.Click += async (_, _) => await LoadDashboardAsync();
        nav.Controls.Add(refresh, 8, 0);
        var exit = CreateButton("Cikis");
        exit.BackColor = Color.FromArgb(82, 93, 110);
        exit.Click += (_, _) => Close();
        nav.Controls.Add(exit, 9, 0);
        root.Controls.Add(nav, 0, 1);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 0, 0, 8) };
        for (var column = 0; column < 5; column++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        AddKpi(kpis, 0, "Toplam takip", "total");
        AddKpi(kpis, 1, "Urun", "listings");
        AddKpi(kpis, 2, "Magaza", "shops");
        AddKpi(kpis, 3, "Anahtar kelime", "keywords");
        AddKpi(kpis, 4, "Snapshot", "snapshots");
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
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Takip trendi", Font = new Font("Segoe UI Semibold", 12F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _trendComboBox.Dock = DockStyle.Fill;
        _trendComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _trendComboBox.SelectedIndexChanged += (_, _) => _trendChart.SetHistory((_trendComboBox.SelectedItem as TrendOption)?.History);
        toolbar.Controls.Add(_trendComboBox, 1, 0);
        panel.Controls.Add(toolbar, 0, 0);
        _trendChart.Dock = DockStyle.Fill;
        panel.Controls.Add(_trendChart, 0, 1);
        return panel;
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

    private async Task OpenTrackingAsync()
    {
        using var form = new TrackingHistoryForm(_trackingService);
        form.ShowDialog(this);
        await LoadDashboardAsync();
    }

    private void ConfigureOpportunitiesGrid()
    {
        ConfigureBaseGrid(_opportunitiesGrid);
        AddColumn(_opportunitiesGrid, "Anahtar kelime", nameof(OpportunityRow.Keyword), 220, true);
        AddColumn(_opportunitiesGrid, "Firsat", nameof(OpportunityRow.Opportunity), 75);
        AddColumn(_opportunitiesGrid, "Talep", nameof(OpportunityRow.Demand), 75);
        AddColumn(_opportunitiesGrid, "Rekabet", nameof(OpportunityRow.Competition), 80);
        AddColumn(_opportunitiesGrid, "Tarih", nameof(OpportunityRow.Captured), 130);
    }

    private void ConfigureChangesGrid()
    {
        ConfigureBaseGrid(_changesGrid);
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
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI Semibold", 12F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        configure();
        panel.Controls.Add(grid, 0, 1);
        return panel;
    }

    private void AddKpi(TableLayoutPanel parent, int column, string title, string key)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White, Margin = new Padding(column == 0 ? 0 : 5, 2, column == 4 ? 0 : 5, 4), Padding = new Padding(12, 7, 12, 7), CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, ForeColor = Color.FromArgb(82, 93, 110) }, 0, 0);
        var value = new Label { Dock = DockStyle.Fill, Text = "-", Font = new Font("Segoe UI Semibold", 16F), ForeColor = Color.FromArgb(23, 32, 49), TextAlign = ContentAlignment.MiddleLeft };
        panel.Controls.Add(value, 0, 1);
        _kpis[key] = value;
        parent.Controls.Add(panel, column, 0);
    }

    private void SetKpi(string key, int value) => _kpis[key].Text = value.ToString("N0");
    private static void ConfigureBaseGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
    }
    private static void AddColumn(DataGridView grid, string header, string property, int width, bool fill = false) => grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, Width = width, AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None });
    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            AutoEllipsis = true,
            Padding = new Padding(0),
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 112, 184);
        return button;
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
