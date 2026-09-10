namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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
using SimilarProductsWinForms.Models;
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
    private readonly AbTestService _abTestService;
    private readonly BatchQueueProcessorService _batchQueueProcessorService;

    private readonly FinancialReportService _financialService = new();
    private readonly ExchangeRateService _exchangeRateService = new();
    private FinancialReport _liveReport = FinancialReport.Empty;
    private decimal _liveExchangeRate = 48.25m;
    private CancellationTokenSource _cts = new();

    private readonly Label _lblTitle = new();
    private readonly Label _lblSubtitle = new();
    private readonly Label _lblLiveRate = new();
    private readonly Label _lblApiStatus = new();
    private readonly Label _lblLastUpdated = new();
    private readonly ModernButtonControl _btnVdsUpdate = new();

    private readonly Label _lblKpiGross = new();
    private readonly Label _lblKpiNetProfit = new();
    private readonly Label _lblKpiOrders = new();
    private readonly Label _lblKpiListings = new();
    private readonly Label _lblKpiExpenses = new();

    private readonly DataGridView _gridRecentOrders = new();
    private readonly Label _lblOrdersSummary = new();
    private readonly FlowLayoutPanel _pnlAiCopilot = new();

    private CartesianChart _chartRevenueProfit = null!;

    public static DashboardForm? Instance { get; private set; }

    private ModernSidebarNav _sidebarNav = null!;
    private Panel _mainContainer = null!;
    private TableLayoutPanel _dashboardRootPanel = null!;
    private Form? _currentEmbeddedForm;

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
        Instance = this;
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

        InitializeChart();
        BuildLayout();
        UiStyle.ApplyTheme(this);

        Shown += async (_, _) =>
        {
            DailyFinancialReportScheduler.Instance.Start();
            VdsUpdateNotifierService.StartPeriodicAutoUpdater(TimeSpan.FromSeconds(20), statusMsg =>
            {
                if (!IsDisposed)
                {
                    try
                    {
                        BeginInvoke(() =>
                        {
                            _lblLastUpdated.Text = statusMsg;
                            _lblLastUpdated.ForeColor = UiStyle.EtsyColor;
                        });
                    }
                    catch { }
                }
            });
            await LoadLiveDashboardAsync();
        };
    }

    private void InitializeChart()
    {
        _chartRevenueProfit = new CartesianChart
        {
            Dock = DockStyle.Fill,
            Series = Array.Empty<ISeries>(),
            XAxes = new[] { new Axis { Labels = Array.Empty<string>(), TextSize = 10, LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)) } },
            YAxes = new[] { new Axis { TextSize = 10, Labeler = v => $"${v:N0}", LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)) } },
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Top,
            LegendTextPaint = new SolidColorPaint(new SKColor(248, 250, 252)),
            BackColor = Color.FromArgb(30, 41, 59),
        };
    }

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
        _sidebarNav = new ModernSidebarNav();
        _sidebarNav.Dock = DockStyle.Fill;
        PopulateSidebarItems();
        _sidebarNav.ItemSelected += OnSidebarItemSelected;

        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _sidebarNav.IsCollapsed ? 64 : 260));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _sidebarNav.CollapsedChanged += (_, _) =>
        {
            formGrid.SuspendLayout();
            _mainContainer.SuspendLayout();
            formGrid.ColumnStyles[0].Width = _sidebarNav.IsCollapsed ? 64 : 260;
            formGrid.ResumeLayout(true);
            _mainContainer.ResumeLayout(true);
            formGrid.PerformLayout();
            _mainContainer.PerformLayout();
            if (_currentEmbeddedForm != null)
            {
                _currentEmbeddedForm.Dock = DockStyle.None;
                _currentEmbeddedForm.Size = _mainContainer.ClientSize;
                _currentEmbeddedForm.Dock = DockStyle.Fill;
                _currentEmbeddedForm.PerformLayout();
                _currentEmbeddedForm.Invalidate(true);
            }
        };

        _mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            AutoScroll = true,
            BackColor = UiStyle.BackgroundColor
        };

        formGrid.Controls.Add(_sidebarNav, 0, 0);
        formGrid.Controls.Add(_mainContainer, 1, 0);
        Controls.Add(formGrid);

        UiStyle.MakeResponsive(this, _sidebarNav);

        _dashboardRootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(0),
            AutoScroll = true
        };
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));  // Header (72px to prevent subtitle clipping)
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 108)); // Hero KPI Strip
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));  // Quick Action Hub
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));   // Middle: Orders (Left) & Copilot (Right)
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));   // Bottom: Live Trend Chart

        _dashboardRootPanel.Controls.Add(BuildHeaderSection(), 0, 0);
        _dashboardRootPanel.Controls.Add(BuildHeroKpiStrip(), 0, 1);
        _dashboardRootPanel.Controls.Add(BuildQuickActionHub(), 0, 2);
        _dashboardRootPanel.Controls.Add(BuildMiddleSection(), 0, 3);
        _dashboardRootPanel.Controls.Add(BuildTrendChartSection(), 0, 4);

        _mainContainer.Controls.Add(_dashboardRootPanel);
    }

    private Control BuildHeaderSection()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 2)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var titlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        _lblTitle.AutoSize = true;
        _lblTitle.Text = "🚀 Etsy Mağaza Komuta Merkezi";
        _lblTitle.Font = UiStyle.TitleFont;
        _lblTitle.ForeColor = UiStyle.TextDark;
        _lblTitle.Margin = new Padding(0, 0, 0, 2);
        titlePanel.Controls.Add(_lblTitle);

        _lblSubtitle.AutoSize = true;
        _lblSubtitle.Text = "Canlı Mağaza İstatistikleri, Finansal Özet ve Akıllı Büyüme Paneli";
        _lblSubtitle.Font = UiStyle.SubtitleFont;
        _lblSubtitle.ForeColor = UiStyle.TextMuted;
        _lblSubtitle.Margin = new Padding(0, 0, 0, 0);
        titlePanel.Controls.Add(_lblSubtitle);

        header.Controls.Add(titlePanel, 0, 0);

        var rightPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0)
        };

        var btnRefresh = new ModernButtonControl
        {
            Text = "🔄 Yenile",
            Size = new Size(105, 34),
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Margin = new Padding(6, 0, 0, 0)
        };
        btnRefresh.Click += async (_, _) => await LoadLiveDashboardAsync();
        rightPanel.Controls.Add(btnRefresh);

        _btnVdsUpdate.Text = "⚡ Yeni Sürüm";
        _btnVdsUpdate.Size = new Size(140, 34);
        _btnVdsUpdate.NormalColor = UiStyle.EtsyColor;
        _btnVdsUpdate.HoverColor = UiStyle.EtsyHover;
        _btnVdsUpdate.ForeColor = Color.White;
        _btnVdsUpdate.Visible = false;
        _btnVdsUpdate.Margin = new Padding(6, 0, 0, 0);
        _btnVdsUpdate.Click += (_, _) =>
        {
            var res = MessageBox.Show(
                this,
                "GitHub üzerinde yeni bir VDS geliştirme sürümü (dev-latest) tespit edildi!\n\nUygulama otomatik güncellenip yeniden başlatılsın mı?",
                "VDS Otomatik Güncelleme",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                VdsUpdateNotifierService.TriggerVdsUpdateAndRestart();
                Application.Exit();
            }
        };
        rightPanel.Controls.Add(_btnVdsUpdate);

        _lblApiStatus.AutoSize = true;
        _lblApiStatus.Text = "🟢 Canlı Etsy API";
        _lblApiStatus.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _lblApiStatus.ForeColor = UiStyle.SuccessColor;
        _lblApiStatus.Padding = new Padding(8, 6, 8, 6);
        _lblApiStatus.Margin = new Padding(4, 0, 0, 0);
        rightPanel.Controls.Add(_lblApiStatus);

        _lblLiveRate.AutoSize = true;
        _lblLiveRate.Text = "💱 1 USD = 48.25 ₺";
        _lblLiveRate.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _lblLiveRate.ForeColor = UiStyle.AccentColor;
        _lblLiveRate.Padding = new Padding(8, 6, 8, 6);
        _lblLiveRate.Margin = new Padding(4, 0, 0, 0);
        _lblLiveRate.Cursor = Cursors.Hand;
        var toolTipRate = new ToolTip();
        toolTipRate.SetToolTip(_lblLiveRate, "Dolar kurunu anlık düzenlemek için tıklayın");
        _lblLiveRate.Click += async (_, _) => await ShowQuickExchangeRateDialogAsync();
        rightPanel.Controls.Add(_lblLiveRate);

        _lblLastUpdated.AutoSize = true;
        _lblLastUpdated.Text = $"Son Güncelleme: {DateTime.Now:HH:mm}";
        _lblLastUpdated.Font = new Font("Segoe UI", 8.5F);
        _lblLastUpdated.ForeColor = UiStyle.TextMuted;
        _lblLastUpdated.Padding = new Padding(0, 7, 4, 0);
        rightPanel.Controls.Add(_lblLastUpdated);

        header.Controls.Add(rightPanel, 1, 0);
        return header;
    }

    private async Task ShowQuickExchangeRateDialogAsync()
    {
        using var promptForm = new Form
        {
            Text = "Döviz Kuru Ayarla",
            Size = new Size(360, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.CardBackground,
            ForeColor = UiStyle.TextDark
        };

        var lblInfo = new Label
        {
            Text = "Güncel USD / TRY kurunu giriniz:",
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Location = new Point(20, 20),
            Size = new Size(300, 24)
        };
        promptForm.Controls.Add(lblInfo);

        var numRate = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 1m,
            Maximum = 200m,
            Increment = 0.10m,
            Value = Math.Max(1m, Math.Min(200m, _liveExchangeRate)),
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Location = new Point(20, 52),
            Size = new Size(300, 32),
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = UiStyle.AccentColor
        };
        promptForm.Controls.Add(numRate);

        var btnSave = new ModernButtonControl
        {
            Text = "💾 Kuru Güncelle",
            Size = new Size(140, 36),
            Location = new Point(20, 100),
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White
        };
        btnSave.Click += (_, _) =>
        {
            promptForm.DialogResult = DialogResult.OK;
            promptForm.Close();
        };
        promptForm.Controls.Add(btnSave);

        var btnCancel = new ModernButtonControl
        {
            Text = "İptal",
            Size = new Size(90, 36),
            Location = new Point(170, 100),
            NormalColor = Color.FromArgb(51, 65, 85),
            HoverColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White
        };
        btnCancel.Click += (_, _) =>
        {
            promptForm.DialogResult = DialogResult.Cancel;
            promptForm.Close();
        };
        promptForm.Controls.Add(btnCancel);

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            decimal newRate = numRate.Value;
            HistoricalExchangeRateProvider.SetCustomRate(DateTime.Today, newRate);
            _liveExchangeRate = newRate;
            await LoadLiveDashboardAsync();
        }
    }

    private static string FormatUSD(decimal amount, bool showSign = false)
    {
        if (amount < 0)
            return $"-${Math.Abs(amount):N2}";
        if (showSign && amount > 0)
            return $"+${amount:N2}";
        return $"${amount:N2}";
    }

    private static string FormatTRY(decimal amount, bool showSign = false)
    {
        if (amount < 0)
            return $"-₺{Math.Abs(amount):N0}";
        if (showSign && amount > 0)
            return $"+₺{amount:N0}";
        return $"₺{amount:N0}";
    }

    private Control BuildHeroKpiStrip()
    {
        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0, 2, 0, 4)
        };
        for (int i = 0; i < 5; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        strip.Controls.Add(CreateHeroKpiCard("💰 BU AYKİ BRÜT CİRO", _lblKpiGross, UiStyle.SuccessColor, "Etsy Mağaza Satış Geliri"), 0, 0);
        strip.Controls.Add(CreateHeroKpiCard("💵 GERÇEK NET KÂR", _lblKpiNetProfit, UiStyle.PrimaryColor, "Maliyet & Komisyonlar Düşülmüş"), 1, 0);
        strip.Controls.Add(CreateHeroKpiCard("📦 TOPLAM SİPARİŞ", _lblKpiOrders, UiStyle.WarningColor, "Dönem İçi Başarılı Satış"), 2, 0);
        strip.Controls.Add(CreateHeroKpiCard("🏷️ AKTİF İLAN SAYISI", _lblKpiListings, UiStyle.AccentColor, "Mağaza Portföyü"), 3, 0);
        strip.Controls.Add(CreateHeroKpiCard("📢 REKLAM VE GİDERLER", _lblKpiExpenses, UiStyle.DangerColor, "Etsy Kesintisi & Reklam"), 4, 0);

        return strip;
    }

    private static Control CreateHeroKpiCard(string title, Label valueLabel, Color accentColor, string subtext)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(12, 10, 12, 10),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblTitle, 0, 0);

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "—";
        valueLabel.Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold);
        valueLabel.ForeColor = accentColor;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(valueLabel, 0, 1);

        var lblSub = new Label
        {
            Dock = DockStyle.Fill,
            Text = subtext,
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        layout.Controls.Add(lblSub, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildQuickActionHub()
    {
        var hub = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 4)
        };
        for (int i = 0; i < 4; i++)
            hub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        hub.Controls.Add(CreateActionCard("💳 Finans & Muhasebe", "Ödeme defteri, komisyonlar ve banka transferleri", UiStyle.PrimaryColor, () => _ = OpenModuleByIdAsync("financial")), 0, 0);
        hub.Controls.Add(CreateActionCard("🛍️ AI Ürün Bul & Taslak", "Trend ürün araştırması ve tek tıkla taslak listeleme", UiStyle.EtsyColor, () => _ = OpenModuleByIdAsync("creator")), 1, 0);
        hub.Controls.Add(CreateActionCard("🖼️ AI Görsel Studio", "AI ile stüdyo kalitesinde ürün fotoğrafları oluşturma", UiStyle.AiColor, () => _ = OpenModuleByIdAsync("ai_image")), 2, 0);
        hub.Controls.Add(CreateActionCard("🏬 Mağazama Git & AI Denetim", "Siparişler, SEO skoru ve AI mağaza denetim raporu", UiStyle.SuccessColor, () => _ = OpenModuleByIdAsync("shop")), 3, 0);

        return hub;
    }

    private static Control CreateActionCard(string title, string description, Color accentColor, Action onClick)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(12, 10, 12, 10),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Cursor = Cursors.Hand
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = accentColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var lblDesc = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        layout.Controls.Add(lblDesc, 0, 1);

        card.Click += (_, _) => onClick();
        lblTitle.Click += (_, _) => onClick();
        lblDesc.Click += (_, _) => onClick();

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildMiddleSection()
    {
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 4)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));

        var ordersCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var ordersLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        ordersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        ordersLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var ordersHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        ordersHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        ordersHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var lblOrdersTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🟢 Son Siparişler & Canlı Satış Akışı",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        ordersHeader.Controls.Add(lblOrdersTitle, 0, 0);

        _lblOrdersSummary.Dock = DockStyle.Fill;
        _lblOrdersSummary.Text = "Yükleniyor...";
        _lblOrdersSummary.Font = new Font("Segoe UI", 8F);
        _lblOrdersSummary.ForeColor = UiStyle.TextMuted;
        _lblOrdersSummary.TextAlign = ContentAlignment.MiddleRight;
        ordersHeader.Controls.Add(_lblOrdersSummary, 1, 0);

        ordersLayout.Controls.Add(ordersHeader, 0, 0);

        UiStyle.ConfigureBaseGrid(_gridRecentOrders);
        ConfigureRecentOrdersGrid();
        _gridRecentOrders.CellDoubleClick += OnRecentOrderDoubleClick;
        ordersLayout.Controls.Add(_gridRecentOrders, 0, 1);

        ordersCard.Controls.Add(ordersLayout);
        split.Controls.Add(ordersCard, 0, 0);

        var copilotCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var copilotLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        copilotLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        copilotLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblCopilotTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "💡 Akıllı Mağaza Asistanı (Store Copilot)",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        copilotLayout.Controls.Add(lblCopilotTitle, 0, 0);

        _pnlAiCopilot.Dock = DockStyle.Fill;
        _pnlAiCopilot.FlowDirection = FlowDirection.TopDown;
        _pnlAiCopilot.WrapContents = false;
        _pnlAiCopilot.AutoScroll = true;
        _pnlAiCopilot.Padding = new Padding(2, 2, 6, 2);
        _pnlAiCopilot.Resize += (_, _) =>
        {
            int targetWidth = Math.Max(260, _pnlAiCopilot.ClientSize.Width - 14);
            foreach (Control c in _pnlAiCopilot.Controls)
            {
                if (c is ModernCardPanel card)
                {
                    card.Width = targetWidth;
                    foreach (Control child in card.Controls)
                    {
                        if (child is Label lbl && child.Location.Y > 20)
                        {
                            lbl.Size = new Size(targetWidth - 28, 0);
                            lbl.MaximumSize = new Size(targetWidth - 28, 0);
                        }
                    }
                }
            }
        };

        copilotLayout.Controls.Add(_pnlAiCopilot, 0, 1);

        copilotCard.Controls.Add(copilotLayout);
        split.Controls.Add(copilotCard, 1, 0);

        return split;
    }

    private void ConfigureRecentOrdersGrid()
    {
        _gridRecentOrders.Columns.Clear();
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tarih", DataPropertyName = "DateStr", Width = 80 });
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sipariş No", DataPropertyName = "ReceiptIdStr", Width = 95 });
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Satılan Ürün", DataPropertyName = "ProductTitle", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Adet", DataPropertyName = "Quantity", Width = 50 });
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tutar ($)", DataPropertyName = "TotalUSDStr", Width = 85 });
        _gridRecentOrders.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Net Kâr ($ / ₺)", DataPropertyName = "NetProfitCombinedStr", Width = 145 });

        _gridRecentOrders.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _gridRecentOrders.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _gridRecentOrders.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _gridRecentOrders.Columns[5].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    private void OnRecentOrderDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridRecentOrders.Rows.Count) return;
        var row = _gridRecentOrders.Rows[e.RowIndex];
        if (row.Tag is OrderFinancialSummary order)
        {
            using var form = new OrderDetailsForm(order);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadLiveDashboardAsync();
            }
        }
    }

    private Control BuildTrendChartSection()
    {
        var chartCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 4, 4, 0),
            Padding = new Padding(12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var chartLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        chartLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblChartTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📈 BU AYIN GÜNLÜK GELİR VE NET KÂR TRENDİ ($)",
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        chartLayout.Controls.Add(lblChartTitle, 0, 0);

        chartLayout.Controls.Add(_chartRevenueProfit, 0, 1);
        chartCard.Controls.Add(chartLayout);

        return chartCard;
    }

    private async Task LoadLiveDashboardAsync()
    {
        try
        {
            _lblLastUpdated.Text = "Veriler güncelleniyor...";

            // 1. Fetch real-time live currency rate from API purely for informative display widget in header
            try
            {
                _liveExchangeRate = await _exchangeRateService.GetLiveUsdTryRateAsync(_cts.Token);
                _lblLiveRate.Text = $"💱 Canlı Kur: 1 USD = {_liveExchangeRate:N2} ₺";
            }
            catch
            {
                _liveExchangeRate = HistoricalExchangeRateProvider.GetRateForDate(DateTime.Today, 38.45m);
                _lblLiveRate.Text = $"💱 Canlı Kur: 1 USD = {_liveExchangeRate:N2} ₺";
            }

            // 2. KONTROL PANELİ: Bulunduğumuz Ayın 1'inden Bugüne (Bu Ay)
            var now = DateTime.UtcNow;
            var from = new DateTimeOffset(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
            var to = DateTimeOffset.UtcNow;
            var settings = EtsyApiSettingsStore.Load();

            _liveReport = await _financialService.GetReportAsync(settings, from, to, _liveExchangeRate, _cts.Token);

            if (_liveReport.IsFallbackMode)
            {
                _lblApiStatus.Text = "🎮 Demo / Sipariş Modu";
                _lblApiStatus.ForeColor = UiStyle.WarningColor;
            }
            else
            {
                _lblApiStatus.Text = "🟢 Canlı Etsy API";
                _lblApiStatus.ForeColor = UiStyle.SuccessColor;
            }

            _lblKpiGross.Text = $"{FormatUSD(_liveReport.TotalGross)}  ({FormatTRY(_liveReport.TotalGrossTRY)})";
            _lblKpiNetProfit.Text = $"{FormatUSD(_liveReport.RealNetProfitUSD)}  ({FormatTRY(_liveReport.RealNetProfitTRY)})";
            _lblKpiNetProfit.ForeColor = _liveReport.RealNetProfitUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;
            _lblKpiOrders.Text = $"{_liveReport.OrderSummaries.Count} Sipariş";
            
            int activeListings = _liveReport.OrderSummaries.Select(o => o.ListingId).Distinct().Count();
            _lblKpiListings.Text = activeListings > 0 ? $"{activeListings} Aktif Ürün" : "12 Ürün";
            
            decimal totalDeductions = _liveReport.TotalFees + _liveReport.TotalInnerAdFees + _liveReport.TotalOffsiteAdFees + _liveReport.TotalRefunds;
            _lblKpiExpenses.Text = FormatUSD(-Math.Abs(totalDeductions));

            PopulateRecentOrdersGrid();
            PopulateAiCopilotInsights();
            UpdateRevenueTrendChart();

            _lblLastUpdated.Text = $"Son Güncelleme: {DateTime.Now:HH:mm:ss}";
            _ = CheckVdsUpdateAsync();
        }
        catch (Exception ex)
        {
            _lblLastUpdated.Text = $"Hata: {ex.Message}";
        }
    }

    private async Task CheckVdsUpdateAsync()
    {
        try
        {
            var update = await VdsUpdateNotifierService.CheckForUpdateAsync(_cts.Token);
            if (update.IsUpdateAvailable && !IsDisposed)
            {
                BeginInvoke(() =>
                {
                    _btnVdsUpdate.Visible = true;
                    _btnVdsUpdate.Text = $"⚡ Yeni Sürüm ({update.PublishedAt.LocalDateTime:HH:mm})";
                });
            }
        }
        catch { }
    }

    private void PopulateRecentOrdersGrid()
    {
        _gridRecentOrders.Rows.Clear();
        var recent = _liveReport.OrderSummaries.OrderByDescending(o => o.OrderDate).Take(15).ToList();
        _lblOrdersSummary.Text = $"Toplam {recent.Count} sipariş listelendi";

        foreach (var order in recent)
        {
            string netProfitFormatted = $"{FormatUSD(order.NetProfitUSD, true)} ({FormatTRY(order.NetProfitTRY)})";
            int index = _gridRecentOrders.Rows.Add(
                order.OrderDate.ToString("dd.MM.yy"),
                $"#{order.ReceiptId}",
                order.ProductTitle,
                order.Quantity,
                FormatUSD(order.GrandTotal),
                netProfitFormatted
            );
            _gridRecentOrders.Rows[index].Tag = order;
            _gridRecentOrders.Rows[index].Cells[5].Style.ForeColor = order.NetProfitUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;
        }
    }

    private void PopulateAiCopilotInsights()
    {
        _pnlAiCopilot.Controls.Clear();
        int targetWidth = Math.Max(260, _pnlAiCopilot.ClientSize.Width - 14);
        if (targetWidth < 260 && _dashboardRootPanel.Width > 0)
        {
            targetWidth = Math.Max(260, (int)(_dashboardRootPanel.Width * 0.40f) - 30);
        }

        int missingCostCount = _liveReport.OrderSummaries.Count(o => !o.HasCostData);
        if (missingCostCount > 0)
        {
            _pnlAiCopilot.Controls.Add(CreateInsightCard(
                "⚠️ Maliyet Bilgisi Eksik",
                $"{missingCostCount} siparişinizin henüz hammadde maliyeti girilmedi. Gerçek kârı tam görmek için maliyet ekleyin.",
                UiStyle.WarningColor,
                targetWidth,
                "💰 Maliyetleri Düzenle",
                () => { using var dlg = new ProductCostManagerForm(); dlg.ShowDialog(this); }
            ));
        }

        var topProduct = _liveReport.OrderSummaries
            .GroupBy(o => o.ProductTitle)
            .OrderByDescending(g => g.Sum(x => x.GrandTotal))
            .FirstOrDefault();

        if (topProduct != null)
        {
            decimal productRev = topProduct.Sum(x => x.GrandTotal);
            _pnlAiCopilot.Controls.Add(CreateInsightCard(
                "🌟 En Çok Ciro Getiren Ürün",
                $"'{topProduct.Key}' bu ay toplam ${productRev:N2} ciro sağlayarak mağazanızın yıldız ürünü oldu.",
                UiStyle.SuccessColor,
                targetWidth
            ));
        }

        if (_liveReport.TotalGross > 0)
        {
            decimal adPct = Math.Round((_liveReport.TotalInnerAdFees + _liveReport.TotalOffsiteAdFees) / _liveReport.TotalGross * 100, 1);
            _pnlAiCopilot.Controls.Add(CreateInsightCard(
                "📢 Reklam & Komisyon Durumu",
                $"Reklam giderleri bu ay cironuzun %{adPct}'ini oluşturuyor. Toplam net kâr marjınız: %{_liveReport.ProfitMarginPct:N1}.",
                UiStyle.AccentColor,
                targetWidth
            ));
        }

        _pnlAiCopilot.Controls.Add(CreateInsightCard(
            "🚀 Büyüme & SEO Tavsiyesi",
            "Ürün başlıklarında ve ilk 3 etiketinde en çok aranan uzun kuyruklu anahtar kelimeleri kullanarak organik trafiğinizi %25 artırabilirsiniz.",
            UiStyle.PrimaryColor,
            targetWidth,
            "🔍 Pazar Araştırması",
            () => _ = OpenModuleByIdAsync("research")
        ));
    }

    private static Control CreateInsightCard(string title, string text, Color accentColor, int width, string? buttonText = null, Action? onButtonClick = null)
    {
        int cardWidth = Math.Max(260, width);
        var card = new ModernCardPanel
        {
            Width = cardWidth,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 12, 14, 12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = Color.FromArgb(120, accentColor.R, accentColor.G, accentColor.B),
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = accentColor,
            Location = new Point(14, 12),
            AutoSize = true
        };
        card.Controls.Add(lblTitle);

        int textWidth = cardWidth - 28;
        var lblText = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = UiStyle.TextDark,
            Location = new Point(14, 34),
            Size = new Size(textWidth, 0),
            MaximumSize = new Size(textWidth, 0),
            AutoSize = true
        };
        card.Controls.Add(lblText);

        int bottomY = lblText.Bottom + 8;

        if (!string.IsNullOrEmpty(buttonText) && onButtonClick != null)
        {
            var btn = new ModernButtonControl
            {
                Text = buttonText,
                Size = new Size(160, 30),
                Location = new Point(14, bottomY),
                NormalColor = accentColor,
                HoverColor = Color.FromArgb(Math.Min(255, accentColor.R + 25), Math.Min(255, accentColor.G + 25), Math.Min(255, accentColor.B + 25)),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.Click += (_, _) => onButtonClick();
            card.Controls.Add(btn);
            bottomY = btn.Bottom + 10;
        }
        else
        {
            bottomY += 4;
        }

        card.Height = bottomY;
        return card;
    }

    private void UpdateRevenueTrendChart()
    {
        var daily = _liveReport.DailySummaries.OrderBy(d => d.SortDate).ToList();
        if (daily.Count == 0)
        {
            var now = DateTime.Today;
            int daysInMonth = Math.Max(1, now.Day);
            var days = Enumerable.Range(1, daysInMonth)
                .Select(d => new DateTime(now.Year, now.Month, d))
                .ToList();

            var labels = days.Select(d => d.ToString("dd MMM")).ToArray();
            var grossValues = days.Select(d => (double)_liveReport.OrderSummaries
                .Where(o => o.OrderDate.Date == d.Date)
                .Sum(o => o.GrandTotal)).ToArray();
            var profitValues = days.Select(d => (double)_liveReport.OrderSummaries
                .Where(o => o.OrderDate.Date == d.Date)
                .Sum(o => o.NetProfitUSD)).ToArray();

            RenderChart(labels, grossValues, profitValues);
            return;
        }

        var chartLabels = daily.Select(d => d.PeriodLabel).ToArray();
        var chartGross = daily.Select(d => (double)d.GrossSales).ToArray();
        var chartProfit = daily.Select(d => (double)d.RealNetProfitUSD).ToArray();

        RenderChart(chartLabels, chartGross, chartProfit);
    }

    private void RenderChart(string[] labels, double[] grossValues, double[] profitValues)
    {
        var textPaint = new SolidColorPaint(new SKColor(203, 213, 225));

        _chartRevenueProfit.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "💰 Brüt Satış ($)",
                Values = grossValues,
                Fill = new LinearGradientPaint(new SKColor(99, 102, 241, 40), new SKColor(99, 102, 241, 0)),
                Stroke = new SolidColorPaint(new SKColor(99, 102, 241), 2.5f),
                GeometrySize = 6,
                LineSmoothness = 0.6
            },
            new LineSeries<double>
            {
                Name = "💵 Net Kâr ($)",
                Values = profitValues,
                Fill = new LinearGradientPaint(new SKColor(16, 185, 129, 40), new SKColor(16, 185, 129, 0)),
                Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 2.5f),
                GeometrySize = 6,
                LineSmoothness = 0.6
            }
        };

        _chartRevenueProfit.XAxes = new[]
        {
            new Axis
            {
                Labels = labels,
                TextSize = 10,
                LabelsPaint = textPaint
            }
        };

        _chartRevenueProfit.YAxes = new[]
        {
            new Axis
            {
                TextSize = 10,
                LabelsPaint = textPaint,
                Labeler = v => $"${v:N0}"
            }
        };
    }

    private void PopulateSidebarItems()
    {
        _sidebarNav.ClearItems();
        _sidebarNav.AddItem("dashboard", "Kontrol Paneli", "📊", "Genel");
        _sidebarNav.AddItem("fast_creator", "Hızlı Ürün Ekle (AI)", "⚡", "Genel", "YENİ");
        _sidebarNav.AddItem("creator", "Ürün Bul & Taslak", "🛍️", "Genel");
        _sidebarNav.AddItem("ai_image", "AI Görsel Studio", "🖼️", "Genel", "YENİ");
        _sidebarNav.AddItem("shop", "Mağazam Performansı", "🏬", "Genel");

        _sidebarNav.AddItem("research", "Pazar Araştırması", "🔍", "Araştırma & Analiz");
        _sidebarNav.AddItem("competitor_spy", "Rakip & Trend Casusu", "🕵️", "Araştırma & Analiz", "YENİ");
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

    public void EmbedModuleForm(Form moduleForm)
    {
        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;

        if (_currentEmbeddedForm != null)
        {
            try
            {
                _mainContainer.Controls.Remove(_currentEmbeddedForm);
                _currentEmbeddedForm.Close();
                _currentEmbeddedForm.Dispose();
            }
            catch { }
            finally
            {
                _currentEmbeddedForm = null;
            }
        }

        _mainContainer.Controls.Clear();
        _mainContainer.Padding = new Padding(0);
        _currentEmbeddedForm = moduleForm;

        moduleForm.TopLevel = false;
        moduleForm.FormBorderStyle = FormBorderStyle.None;
        moduleForm.Dock = DockStyle.Fill;
        _mainContainer.Controls.Add(moduleForm);
        moduleForm.Show();

        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;
    }

    private void ShowDashboardView()
    {
        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;

        if (_currentEmbeddedForm != null)
        {
            try
            {
                _mainContainer.Controls.Remove(_currentEmbeddedForm);
                _currentEmbeddedForm.Close();
                _currentEmbeddedForm.Dispose();
            }
            catch { }
            finally
            {
                _currentEmbeddedForm = null;
            }
        }

        _mainContainer.Controls.Clear();
        _mainContainer.Padding = new Padding(16, 12, 16, 12);
        _mainContainer.Controls.Add(_dashboardRootPanel);

        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;
    }

    public async Task OpenModuleByIdAsync(string targetModule)
    {
        _sidebarNav.SelectedItemId = targetModule;

        if (targetModule == "dashboard")
        {
            ShowDashboardView();
            await LoadLiveDashboardAsync();
            return;
        }

        if (targetModule is "api")
        {
            using var form = new EtsyApiSettingsForm();
            form.ShowDialog(this);
            return;
        }
        if (targetModule is "notifications")
        {
            using var form = new NotificationSettingsForm();
            form.ShowDialog(this);
            return;
        }

        Form? nextForm = targetModule switch
        {
            "fast_creator" => new FastListingCreatorForm(_aiListingOptimizer),
            "creator" => new ProductDiscoveryListingCreatorForm(_aiListingOptimizer, historyService: _optimizationHistoryService),
            "ai_image" => new AiListingImageForm(_aiListingOptimizer),
            "shop" => new OwnShopPerformanceForm(_shopPerformanceService, _shopPerformanceHistoryService, _aiListingOptimizer, _optimizationHistoryService),
            "research" => new MarketResearchForm(_keywordUseCase, _trackingService, _optimizationHistoryService, _aiListingOptimizer),
            "competitor_spy" => new CompetitorAndTrendSpyForm(_aiListingOptimizer),
            "external" => new ExternalMarketplaceDiscoveryForm(_aiListingOptimizer),
            "ai_audit" => new OwnShopListingAiAuditForm(_aiListingOptimizer, _optimizationHistoryService),
            "ab_test" => new ListingAbTestForm(_abTestService, _aiListingOptimizer),
            "automation" => new AutomationReportingForm(_automationSettingsStore, _automationScheduler, _windowsTaskScheduler),
            "batch" => new BatchQueueForm(_batchQueueProcessorService, _abTestService),
            "profit" => new ProfitCalculatorForm(),
            "tracking" => new TrackingHistoryForm(_trackingService),
            "financial" => new FinancialReportForm(),
            _ => null
        };

        if (nextForm != null)
        {
            EmbedModuleForm(nextForm);
        }
    }

    private void ToggleTheme()
    {
        UiStyle.CurrentTheme = UiStyle.CurrentTheme == UiStyle.AppTheme.Light ? UiStyle.AppTheme.Dark : UiStyle.AppTheme.Light;
        UiStyle.ApplyTheme(this);
        PopulateSidebarItems();
    }

    private static AbTestService CreateDefaultAbTestService()
    {
        var databasePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var repo = new EtsyMarketPlace.Infrastructure.AbTesting.SqliteAbTestRepository(databasePath);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return new AbTestService(repo);
    }

    private static BatchQueueProcessorService CreateDefaultBatchQueueProcessorService()
    {
        var databasePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var repo = new EtsyMarketPlace.Infrastructure.BatchQueue.SqliteBatchQueueRepository(databasePath);
        repo.InitializeAsync().GetAwaiter().GetResult();
        return new BatchQueueProcessorService(repo, new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService()));
    }
}
