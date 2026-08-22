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

    private ModernSidebarNav _sidebarNav = null!;

    private void BuildLayout()
    {
        Text = "Etsy Market Place - Kontrol Paneli";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        var formGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _sidebarNav = new ModernSidebarNav();
        _sidebarNav.Dock = DockStyle.Fill;
        PopulateSidebarItems();
        _sidebarNav.ItemSelected += OnSidebarItemSelected;

        var mainContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), AutoScroll = true };

        formGrid.Controls.Add(_sidebarNav, 0, 0);
        formGrid.Controls.Add(mainContainer, 1, 0);
        Controls.Add(formGrid);

        UiStyle.MakeResponsive(this, _sidebarNav);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainContainer.Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        var titlePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Etsy Pazar Kontrol Paneli",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
        });
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "3DArtDesignsStore Canlı İstatistikler ve Mağaza Performansı",
            Font = UiStyle.SubtitleFont,
            ForeColor = UiStyle.TextMuted,
        });
        header.Controls.Add(titlePanel, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Yerel pazar verileri yukleniyor...";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);

        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildActionHub(), 0, 1);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 0, 0, 6) };
        for (var column = 0; column < 5; column++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        UiStyle.AddKpiCard(kpis, 0, 0, "Toplam takip", "total", _kpis);
        UiStyle.AddKpiCard(kpis, 1, 0, "Ürün", "listings", _kpis);
        UiStyle.AddKpiCard(kpis, 2, 0, "Mağaza", "shops", _kpis);
        UiStyle.AddKpiCard(kpis, 3, 0, "Anahtar kelime", "keywords", _kpis);
        UiStyle.AddKpiCard(kpis, 4, 0, "Snapshot", "snapshots", _kpis);
        root.Controls.Add(kpis, 0, 2);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        content.Controls.Add(BuildGridSection("🔥 En iyi anahtar kelime fırsatları", _opportunitiesGrid, ConfigureOpportunitiesGrid), 0, 0);
        content.Controls.Add(BuildGridSection("⚡ En büyük değişimler", _changesGrid, ConfigureChangesGrid), 1, 0);
        var trend = BuildTrendSection();
        content.Controls.Add(trend, 0, 1);
        content.SetColumnSpan(trend, 2);
        root.Controls.Add(content, 0, 3);
    }

    private void PopulateSidebarItems()
    {
        _sidebarNav.ClearItems();
        _sidebarNav.AddItem("dashboard", "Kontrol Paneli", "📊", "Genel");
        _sidebarNav.AddItem("creator", "Ürün Bul & Taslak", "🛍️", "Genel", "YENİ");
        _sidebarNav.AddItem("ai_image", "AI Görsel Studio", "🖼️", "Genel", "YENİ");
        _sidebarNav.AddItem("shop", "Mağazam Performansı", "🏬", "Genel");

        _sidebarNav.AddItem("research", "Pazar Araştırması", "🔍", "Araştırma & Analiz");
        _sidebarNav.AddItem("external", "Dış Pazar Yeri Bulucu", "🌐", "Araştırma & Analiz");
        _sidebarNav.AddItem("ai_audit", "Mağaza AI Analizi", "🤖", "Araştırma & Analiz", "YENİ");
        _sidebarNav.AddItem("ab_test", "A/B Test Paneli", "📈", "Araştırma & Analiz");

        _sidebarNav.AddItem("automation", "Otomasyon Raporu", "⚡", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("batch", "Toplu İşlem Kuyruğu", "📦", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("profit", "Kâr Simülatörü", "💰", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("tracking", "Takip Geçmişi", "🎯", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("financial", "Finansal Raporlama", "💳", "Otomasyon & Araçlar", "YENİ");

        _sidebarNav.AddItem("notifications", "Bildirim & Bot Ayarları", "🔔", "Sistem");
        _sidebarNav.AddItem("theme", UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? "Açık Moda Geç" : "Karanlık Moda Geç", UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? "☀️" : "🌙", "Sistem");
        _sidebarNav.AddItem("api", "Etsy API Ayarları", "⚙️", "Sistem");
    }

    private async void OnSidebarItemSelected(object? sender, SidebarItemSelectedEventArgs e)
    {
        if (e.Item.Id == "theme")
        {
            ToggleTheme();
            return;
        }
        await OpenModuleByIdAsync(e.Item.Id);
    }

    public async Task OpenModuleByIdAsync(string targetModule)
    {
        switch (targetModule)
        {
            case "dashboard":
                await LoadDashboardAsync();
                break;
            case "creator":
                await ShowModuleDialogAsync(new ProductDiscoveryListingCreatorForm(_aiListingOptimizer, historyService: _optimizationHistoryService));
                break;
            case "ai_image":
                await ShowModuleDialogAsync(new AiListingImageForm(_aiListingOptimizer));
                break;
            case "shop":
                await ShowModuleDialogAsync(new OwnShopPerformanceForm(_shopPerformanceService, _shopPerformanceHistoryService, _aiListingOptimizer, _optimizationHistoryService));
                break;
            case "research":
                await ShowModuleDialogAsync(new MarketResearchForm(_keywordUseCase, _trackingService, _optimizationHistoryService, _aiListingOptimizer));
                await LoadDashboardAsync();
                break;
            case "external":
                await ShowModuleDialogAsync(new ExternalMarketplaceDiscoveryForm(_aiListingOptimizer));
                break;
            case "ai_audit":
                await ShowModuleDialogAsync(new OwnShopListingAiAuditForm(_aiListingOptimizer, _optimizationHistoryService));
                break;
            case "ab_test":
                await ShowModuleDialogAsync(new ListingAbTestForm(_abTestService));
                break;
            case "automation":
                await ShowModuleDialogAsync(new AutomationReportingForm(_automationSettingsStore, _automationScheduler, _windowsTaskScheduler));
                break;
            case "batch":
                await ShowModuleDialogAsync(new BatchQueueForm(_batchQueueProcessorService));
                break;
            case "profit":
                await ShowModuleDialogAsync(new ProfitCalculatorForm());
                break;
            case "tracking":
                await ShowModuleDialogAsync(new TrackingHistoryForm(_trackingService));
                await LoadDashboardAsync();
                break;
            case "api":
                await ShowModuleDialogAsync(new EtsyApiSettingsForm());
                break;
            case "notifications":
                await ShowModuleDialogAsync(new NotificationSettingsForm());
                break;
            case "financial":
                await ShowModuleDialogAsync(new FinancialReportForm());
                break;
        }
    }

    private async Task ShowModuleDialogAsync(Form form)
    {
        using (form)
        {
            var result = form.ShowDialog(this);
            if (result == DialogResult.Retry && form.Tag is string targetModule)
            {
                await OpenModuleByIdAsync(targetModule);
            }
        }
    }

    private async void OpenExternalDiscovery() => await OpenModuleByIdAsync("external");
    private async void OpenCreator() => await OpenModuleByIdAsync("creator");
    private async void OpenOwnShop() => await OpenModuleByIdAsync("shop");
    private async void OpenAutomation() => await OpenModuleByIdAsync("automation");
    private async void OpenBatchQueue() => await OpenModuleByIdAsync("batch");
    private async void OpenProfitCalc() => await OpenModuleByIdAsync("profit");
    private async void OpenAbTest() => await OpenModuleByIdAsync("ab_test");
    private async Task OpenResearchAsync() => await OpenModuleByIdAsync("research");
    private async Task OpenTrackingAsync() => await OpenModuleByIdAsync("tracking");

    private void ToggleTheme()
    {
        UiStyle.CurrentTheme = UiStyle.CurrentTheme == UiStyle.AppTheme.Light ? UiStyle.AppTheme.Dark : UiStyle.AppTheme.Light;
        UiStyle.ApplyTheme(this);
        PopulateSidebarItems();
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

        var secondary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1 };
        for (var index = 0; index < 7; index++) secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.28f));

        var batchQueue = UiStyle.CreateButton("Toplu İşlem");
        batchQueue.Click += (_, _) => OpenBatchQueue();
        secondary.Controls.Add(batchQueue, 0, 0);

        var profitCalc = UiStyle.CreateButton("Kâr Simülatörü");
        profitCalc.Click += (_, _) => OpenProfitCalc();
        secondary.Controls.Add(profitCalc, 1, 0);

        var abTest = UiStyle.CreateButton("A/B Test");
        abTest.Click += (_, _) => OpenAbTest();
        secondary.Controls.Add(abTest, 2, 0);

        var tracking = UiStyle.CreateButton("Takip");
        tracking.Click += async (_, _) => await OpenTrackingAsync();
        secondary.Controls.Add(tracking, 3, 0);

        var api = UiStyle.CreateButton("API");
        api.Click += (_, _) => { _ = OpenModuleByIdAsync("api"); };
        secondary.Controls.Add(api, 4, 0);

        var refresh = UiStyle.CreateButton("Yenile");
        refresh.Click += async (_, _) => await LoadDashboardAsync();
        secondary.Controls.Add(refresh, 5, 0);

        var exit = UiStyle.CreateButton("Cikis", isSecondary: true);
        exit.Click += (_, _) => Close();
        secondary.Controls.Add(exit, 6, 0);
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
