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
    private readonly Label _lblKpiExpensesSub = new();
    private readonly SimilarProductsWinForms.Controls.AnimatedToolTipForm _customToolTipForm = new();
    private readonly System.Windows.Forms.Timer _hoverCheckTimer = new() { Interval = 50 };
    private Control? _hoveredCard = null;
    private SimilarProductsWinForms.Controls.ToolTipDataPayload? _expensesTooltipPayload = null;

    private readonly DataGridView _gridRecentOrders = new();
    private readonly Label _lblOrdersSummary = new();
    private readonly FlowLayoutPanel _pnlAiCopilot = new();
    private ModernScrollPanel? _copilotScroll;

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

        _hoverCheckTimer.Tick += (_, _) =>
        {
            if (_hoveredCard == null)
            {
                _customToolTipForm.HideTooltip();
                _hoverCheckTimer.Stop();
                return;
            }

            Point mousePos = Cursor.Position;
            Rectangle cardBounds = _hoveredCard.RectangleToScreen(_hoveredCard.ClientRectangle);
            if (!cardBounds.Contains(mousePos) && !_customToolTipForm.Bounds.Contains(mousePos))
            {
                _customToolTipForm.HideTooltip();
                _hoveredCard = null;
                _hoverCheckTimer.Stop();
            }
        };

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            // Ctrl + Shift + U: Canlı Güncelleme Bildirim Animasyonu Testi
            if (e.Control && e.Shift && e.KeyCode == Keys.U)
            {
                bool newState = !_sidebarNav.HasUpdateNotification;
                _sidebarNav.SetUpdateNotification(newState);
                _btnVdsUpdate.Visible = newState;
                if (newState) _btnVdsUpdate.Text = "⚡ Yeni Sürüm (Test)";
            }
        };

        VdsUpdateNotifierService.UpdateStatusChecked += update =>
        {
            if (!IsDisposed)
            {
                SafeBeginInvoke(() =>
                {
                    if (update.IsUpdateAvailable)
                    {
                        _btnVdsUpdate.Visible = true;
                        var commitStr = !string.IsNullOrEmpty(update.RemoteCommitSha)
                            ? $" ({update.RemoteCommitSha[..Math.Min(7, update.RemoteCommitSha.Length)]})"
                            : (update.PublishedAt != DateTimeOffset.MinValue ? $" ({update.PublishedAt.LocalDateTime:HH:mm})" : "");
                        _btnVdsUpdate.Text = $"⚡ Yeni Sürüm{commitStr}";
                        _sidebarNav.SetUpdateNotification(true);
                    }
                    else
                    {
                        _btnVdsUpdate.Visible = false;
                        _sidebarNav.SetUpdateNotification(false);
                    }
                });
            }
        };

        FormClosed += (_, _) =>
        {
            try
            {
                VdsUpdateNotifierService.StopPeriodicAutoUpdater();
                _hoverCheckTimer.Dispose();
                _customToolTipForm.Dispose();
            }
            catch { }
        };

        Shown += async (_, _) =>
        {
            DailyFinancialReportScheduler.Instance.Start();
            VdsUpdateNotifierService.StartPeriodicAutoUpdater(TimeSpan.FromSeconds(45), statusMsg =>
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

        _sidebarNav = new ModernSidebarNav();
        _sidebarNav.Dock = DockStyle.Left;
        _sidebarNav.Width = _sidebarNav.IsCollapsed ? ModernSidebarNav.CollapsedWidth : ModernSidebarNav.DefaultExpandedWidth;
        PopulateSidebarItems();
        _sidebarNav.ItemSelected += OnSidebarItemSelected;

        _mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            AutoScroll = false,
            BackColor = UiStyle.BackgroundColor
        };

        _sidebarNav.CollapsedChanged += (_, _) =>
        {
            _sidebarNav.Width = _sidebarNav.IsCollapsed ? ModernSidebarNav.CollapsedWidth : ModernSidebarNav.DefaultExpandedWidth;
            UpdateActiveViewLayout();
            SafeBeginInvoke(UpdateActiveViewLayout);
        };

        _mainContainer.Resize += (_, _) => UpdateActiveViewLayout();
        _mainContainer.Layout += (_, _) => UpdateActiveViewLayout();

        Controls.Add(_mainContainer);
        Controls.Add(_sidebarNav);
        _mainContainer.BringToFront();

        Resize += (_, _) =>
        {
            UpdateActiveViewLayout();
            SafeBeginInvoke(UpdateActiveViewLayout);
        };

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
        _dashboardRootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 108)); // Quick Action Hub (Matches Hero KPI Strip height)
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
            using var dlg = new ClientUpdateDialog();
            dlg.ShowDialog(this);
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

        var numRate = new ModernNumericUpDown
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

        var cardExpenses = CreateHeroKpiCard("📢 ETSY KESİNTİLERİ & REKLAM", _lblKpiExpenses, UiStyle.DangerColor, "Etsy Kesintisi & Reklam", _lblKpiExpensesSub);
        cardExpenses.Cursor = Cursors.Hand;
        AttachAnimatedHover(cardExpenses, cardExpenses, (tt, pt) =>
        {
            if (_expensesTooltipPayload != null)
                tt.ShowStructuredTooltip(_expensesTooltipPayload, pt, 2500);
        });
        cardExpenses.Click += (_, _) => _ = OpenModuleByIdAsync("accounting");
        foreach (Control child in cardExpenses.Controls)
        {
            child.Cursor = Cursors.Hand;
            child.Click += (_, _) => _ = OpenModuleByIdAsync("accounting");
            foreach (Control subChild in child.Controls)
            {
                subChild.Cursor = Cursors.Hand;
                subChild.Click += (_, _) => _ = OpenModuleByIdAsync("accounting");
            }
        }
        strip.Controls.Add(cardExpenses, 4, 0);

        return strip;
    }

    private void AttachAnimatedHover(Control rootCard, Control currentControl, Action<SimilarProductsWinForms.Controls.AnimatedToolTipForm, Point> showAction)
    {
        currentControl.MouseEnter += (s, e) => 
        {
            if (_hoveredCard == rootCard) return;
            _hoveredCard = rootCard;

            var cardScreenBounds = rootCard.RectangleToScreen(rootCard.ClientRectangle);
            int centerX = cardScreenBounds.Left + cardScreenBounds.Width / 2;
            int belowY = cardScreenBounds.Bottom + 4;
            var position = new Point(centerX, belowY);

            showAction(_customToolTipForm, position);
            _hoverCheckTimer.Start();
        };

        currentControl.MouseLeave += (s, e) =>
        {
            BeginInvoke((Action)(() =>
            {
                if (_hoveredCard != rootCard) return;

                Point mousePos = Cursor.Position;
                Rectangle cardBounds = rootCard.RectangleToScreen(rootCard.ClientRectangle);

                if (!cardBounds.Contains(mousePos))
                {
                    _customToolTipForm.HideTooltip();
                    _hoveredCard = null;
                    _hoverCheckTimer.Stop();
                }
            }));
        };

        foreach (Control child in currentControl.Controls)
        {
            AttachAnimatedHover(rootCard, child, showAction);
        }
    }

    private static ModernCardPanel CreateHeroKpiCard(string title, Label valueLabel, Color accentColor, string subtext, Label? customSubLabel = null)
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

        var lblSub = customSubLabel ?? new Label();
        lblSub.Dock = DockStyle.Fill;
        lblSub.Text = subtext;
        lblSub.Font = new Font("Segoe UI", 7.5F);
        lblSub.ForeColor = UiStyle.TextMuted;
        lblSub.TextAlign = ContentAlignment.MiddleLeft;
        lblSub.AutoEllipsis = true;
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
            Padding = new Padding(0, 2, 0, 4)
        };
        for (int i = 0; i < 4; i++)
            hub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        hub.Controls.Add(CreateActionCard("💳 FİNANS MODÜLÜ", "Finans & Muhasebe", "Ödeme defteri, komisyonlar ve banka transferleri", UiStyle.PrimaryColor, () => _ = OpenModuleByIdAsync("financial")), 0, 0);
        hub.Controls.Add(CreateActionCard("🛍️ TREND & TASLAK", "AI Ürün Bul & Taslak", "Trend ürün araştırması ve tek tıkla taslak listeleme", UiStyle.EtsyColor, () => _ = OpenModuleByIdAsync("creator")), 1, 0);
        hub.Controls.Add(CreateActionCard("🖼️ AI GÖRSEL STÜDYO", "AI Görsel Studio", "AI ile stüdyo kalitesinde ürün fotoğrafları oluşturma", UiStyle.AiColor, () => _ = OpenModuleByIdAsync("ai_image")), 2, 0);
        hub.Controls.Add(CreateActionCard("🏬 MAĞAZA DENETİMİ", "Mağazama Git & AI Denetim", "Siparişler, SEO skoru ve AI mağaza denetim raporu", UiStyle.SuccessColor, () => _ = OpenModuleByIdAsync("shop")), 3, 0);

        return hub;
    }

    private static Control CreateActionCard(string categoryTag, string mainTitle, string description, Color accentColor, Action onClick)
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
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        var lblCategory = new Label
        {
            Dock = DockStyle.Fill,
            Text = categoryTag,
            Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };
        layout.Controls.Add(lblCategory, 0, 0);

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = mainTitle,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = accentColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };
        layout.Controls.Add(lblTitle, 0, 1);

        var lblDesc = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            AutoEllipsis = true,
            UseMnemonic = false
        };
        layout.Controls.Add(lblDesc, 0, 2);

        void SetHover(bool isHover)
        {
            card.BorderColor = isHover ? accentColor : UiStyle.BorderColor;
            card.Invalidate();
        }

        card.MouseEnter += (_, _) => SetHover(true);
        card.MouseLeave += (_, _) => SetHover(false);
        layout.MouseEnter += (_, _) => SetHover(true);
        layout.MouseLeave += (_, _) => SetHover(false);
        lblCategory.MouseEnter += (_, _) => SetHover(true);
        lblCategory.MouseLeave += (_, _) => SetHover(false);
        lblTitle.MouseEnter += (_, _) => SetHover(true);
        lblTitle.MouseLeave += (_, _) => SetHover(false);
        lblDesc.MouseEnter += (_, _) => SetHover(true);
        lblDesc.MouseLeave += (_, _) => SetHover(false);

        card.Click += (_, _) => onClick();
        layout.Click += (_, _) => onClick();
        lblCategory.Click += (_, _) => onClick();
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

        _copilotScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };

        _pnlAiCopilot.Dock = DockStyle.Top;
        _pnlAiCopilot.AutoSize = true;
        _pnlAiCopilot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _pnlAiCopilot.FlowDirection = FlowDirection.TopDown;
        _pnlAiCopilot.WrapContents = false;
        _pnlAiCopilot.AutoScroll = false;
        _pnlAiCopilot.Padding = new Padding(2, 2, 6, 2);
        _pnlAiCopilot.Resize += (_, _) =>
        {
            int availWidth = _copilotScroll != null && _copilotScroll.ClientSize.Width > 40
                ? _copilotScroll.ClientSize.Width - (_copilotScroll.ScrollBar.Visible ? 12 : 4) - 10
                : _pnlAiCopilot.ClientSize.Width - 14;
            int targetWidth = Math.Max(200, availWidth);
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
            _copilotScroll?.RecalculateScroll();
        };

        _copilotScroll.SetContent(_pnlAiCopilot);
        copilotLayout.Controls.Add(_copilotScroll, 0, 1);

        copilotCard.Controls.Add(copilotLayout);
        split.Controls.Add(copilotCard, 1, 0);

        return split;
    }

    private void ConfigureRecentOrdersGrid()
    {
        _gridRecentOrders.Columns.Clear();
        _gridRecentOrders.AlternatingRowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.NotSet;
        _gridRecentOrders.AlternatingRowsDefaultCellStyle.Padding = Padding.Empty;

        var colDate = new DataGridViewTextBoxColumn
        {
            HeaderText = "Tarih",
            DataPropertyName = "DateStr",
            Width = 95,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colDate.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colDate.HeaderCell.Style.Padding = new Padding(8, 0, 8, 0);
        colDate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colDate.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);

        var colReceipt = new DataGridViewTextBoxColumn
        {
            HeaderText = "Sipariş No",
            DataPropertyName = "ReceiptIdStr",
            Width = 105,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colReceipt.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colReceipt.HeaderCell.Style.Padding = new Padding(8, 0, 8, 0);
        colReceipt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colReceipt.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);

        var colTitle = new DataGridViewTextBoxColumn
        {
            HeaderText = "Satılan Ürün",
            DataPropertyName = "ProductTitle",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 140,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colTitle.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colTitle.HeaderCell.Style.Padding = new Padding(8, 0, 8, 0);
        colTitle.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        colTitle.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);

        var colQty = new DataGridViewTextBoxColumn
        {
            HeaderText = "Adet",
            DataPropertyName = "Quantity",
            Width = 70,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colQty.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colQty.HeaderCell.Style.Padding = new Padding(0, 0, 0, 0);
        colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colQty.DefaultCellStyle.Padding = new Padding(0, 2, 0, 2);

        var colTotal = new DataGridViewTextBoxColumn
        {
            HeaderText = "Tutar ($)",
            DataPropertyName = "TotalUSDStr",
            Width = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colTotal.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        colTotal.HeaderCell.Style.Padding = new Padding(0, 0, 10, 0);
        colTotal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colTotal.DefaultCellStyle.Padding = new Padding(0, 2, 10, 2);

        var colProfit = new DataGridViewTextBoxColumn
        {
            HeaderText = "Net Kâr ($ / ₺)",
            DataPropertyName = "NetProfitCombinedStr",
            Width = 160,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colProfit.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        colProfit.HeaderCell.Style.Padding = new Padding(0, 0, 10, 0);
        colProfit.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colProfit.DefaultCellStyle.Padding = new Padding(0, 2, 10, 2);

        _gridRecentOrders.Columns.AddRange(colDate, colReceipt, colTitle, colQty, colTotal, colProfit);
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
            bool hasApi = settings.HasApiCredentials && (!string.IsNullOrWhiteSpace(settings.AccessToken) || !string.IsNullOrWhiteSpace(settings.RefreshToken));

            if (hasApi)
            {
                _liveReport = await _financialService.GetReportAsync(settings, from, to, _liveExchangeRate, _cts.Token);

                if (_liveReport.IsFallbackMode)
                {
                    _lblApiStatus.Text = "⚠️ Tahmini Sipariş Modu";
                    _lblApiStatus.ForeColor = UiStyle.WarningColor;
                }
                else
                {
                    string shopInfo = !string.IsNullOrWhiteSpace(settings.ShopId) ? $" (#{settings.ShopId})" : string.Empty;
                    _lblApiStatus.Text = $"🟢 Canlı Etsy API{shopInfo}";
                    _lblApiStatus.ForeColor = UiStyle.SuccessColor;
                }

                int activeListings = await _financialService.GetActiveListingCountAsync(settings, _cts.Token);
                if (activeListings == 0 && _liveReport.OrderSummaries.Count > 0)
                {
                    activeListings = _liveReport.OrderSummaries.Select(o => o.ListingId).Distinct().Count();
                }
                _lblKpiListings.Text = activeListings > 0 ? $"{activeListings} Aktif İlan" : "0 Aktif İlan";
            }
            else
            {
                _liveReport = FinancialReport.Empty;
                _lblApiStatus.Text = "⚪ Mağaza Bağlı Değil";
                _lblApiStatus.ForeColor = UiStyle.TextMuted;
                _lblKpiListings.Text = "0 İlan (Bağlantı Yok)";
            }

            _lblKpiGross.Text = $"{FormatUSD(_liveReport.TotalGross)}  ({FormatTRY(_liveReport.TotalGrossTRY)})";
            _lblKpiNetProfit.Text = $"{FormatUSD(_liveReport.RealNetProfitUSD)}  ({FormatTRY(_liveReport.RealNetProfitTRY)})";
            _lblKpiNetProfit.ForeColor = _liveReport.RealNetProfitUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;
            _lblKpiOrders.Text = $"{_liveReport.OrderSummaries.Count} Sipariş";
            
            decimal totalDeductionsUSD = -Math.Abs(_liveReport.TotalDeductionsUSD);
            decimal totalDeductionsTRY = -Math.Abs(_liveReport.TotalDeductionsTRY);
            _lblKpiExpenses.Text = $"{FormatUSD(totalDeductionsUSD)}  ({FormatTRY(totalDeductionsTRY)})";

            decimal totalAdsUSD = -Math.Abs(_liveReport.TotalAdsOnlyUSD);
            decimal totalAdsTRY = -Math.Abs(_liveReport.TotalAdsOnlyTRY);
            decimal totalFeesUSD = -Math.Abs(_liveReport.TotalFeesOnlyUSD);
            decimal totalFeesTRY = -Math.Abs(_liveReport.TotalFeesOnlyTRY);
            _lblKpiExpensesSub.Text = $"Reklam: {FormatTRY(totalAdsTRY)} ({FormatUSD(totalAdsUSD)}) • Komisyon: {FormatTRY(totalFeesTRY)} ({FormatUSD(totalFeesUSD)})";

            UpdateExpensesToolTip();

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
            if (!IsDisposed)
            {
                SafeBeginInvoke(() =>
                {
                    if (update.IsUpdateAvailable)
                    {
                        _btnVdsUpdate.Visible = true;
                        var commitStr = !string.IsNullOrEmpty(update.RemoteCommitSha)
                            ? $" ({update.RemoteCommitSha[..Math.Min(7, update.RemoteCommitSha.Length)]})"
                            : (update.PublishedAt != DateTimeOffset.MinValue ? $" ({update.PublishedAt.LocalDateTime:HH:mm})" : "");
                        _btnVdsUpdate.Text = $"⚡ Yeni Sürüm{commitStr}";
                        _sidebarNav.SetUpdateNotification(true);
                    }
                    else
                    {
                        _btnVdsUpdate.Visible = false;
                        _sidebarNav.SetUpdateNotification(false);
                    }
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
        int availWidth = _copilotScroll != null && _copilotScroll.ClientSize.Width > 40
            ? _copilotScroll.ClientSize.Width - (_copilotScroll.ScrollBar.Visible ? 12 : 4) - 10
            : _pnlAiCopilot.ClientSize.Width - 14;
        if (availWidth < 200 && _dashboardRootPanel.Width > 0)
        {
            availWidth = (int)(_dashboardRootPanel.Width * 0.40f) - 30;
        }
        int targetWidth = Math.Max(200, availWidth);

        if (_liveReport.OrderSummaries.Count == 0)
        {
            var settings = EtsyApiSettingsStore.Load();
            bool hasApi = settings.HasApiCredentials && (!string.IsNullOrWhiteSpace(settings.AccessToken) || !string.IsNullOrWhiteSpace(settings.RefreshToken));
            if (!hasApi)
            {
                _pnlAiCopilot.Controls.Add(CreateInsightCard(
                    "🔌 Etsy Mağazası Bağlantısı",
                    "Canlı siparişlerinizi, ciroyu ve kârınızı görmek için Etsy mağazanızı API ayarlarından bağlayabilirsiniz.",
                    UiStyle.AccentColor,
                    targetWidth,
                    "⚙️ API Ayarları",
                    () => { using var dlg = new EtsyApiSettingsForm(); dlg.ShowDialog(this); _ = LoadLiveDashboardAsync(); }
                ));
            }
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
            decimal totalAds = Math.Abs(_liveReport.TotalAdsOnlyUSD);
            decimal adPct = Math.Round(totalAds / _liveReport.TotalGross * 100, 1);
            decimal totalFees = Math.Abs(_liveReport.TotalFeesOnlyUSD);
            decimal feePct = Math.Round(totalFees / _liveReport.TotalGross * 100, 1);

            _pnlAiCopilot.Controls.Add(CreateInsightCard(
                "📢 Reklam & Komisyon Durumu",
                $"Reklam harcaması cironuzun %{adPct:N1}'i (${totalAds:N2}), Etsy komisyonları %{feePct:N1}'i (${totalFees:N2}) seviyesindedir. Toplam net kâr marjınız: %{_liveReport.ProfitMarginPct:N1}.",
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
        _copilotScroll?.RecalculateScroll();
    }

    private void UpdateExpensesToolTip()
    {
        if (_liveReport == null || (_liveReport.IsFallbackMode && _liveReport.DailySummaries.Count == 0 && _liveReport.Entries.Count == 0))
        {
            _expensesTooltipPayload = null;
            return;
        }

        decimal innerAdsUSD = Math.Abs(_liveReport.TotalInnerAdsUSD);
        decimal innerAdsTRY = Math.Abs(_liveReport.TotalInnerAdsTRY);

        decimal offsiteAdsUSD = Math.Abs(_liveReport.TotalOffsiteAdsUSD);
        decimal offsiteAdsTRY = Math.Abs(_liveReport.TotalOffsiteAdsTRY);

        decimal totalAdsUSD = innerAdsUSD + offsiteAdsUSD;
        decimal totalAdsTRY = innerAdsTRY + offsiteAdsTRY;

        decimal feesUSD = Math.Abs(_liveReport.TotalFeesUSD);
        decimal feesTRY = Math.Abs(_liveReport.TotalFeesTRY);

        decimal refundsUSD = Math.Abs(_liveReport.TotalRefundsUSD);
        decimal refundsTRY = Math.Abs(_liveReport.TotalRefundsTRY);

        decimal totalDeductionsUSD = totalAdsUSD + feesUSD + refundsUSD;
        decimal totalDeductionsTRY = totalAdsTRY + feesTRY + refundsTRY;

        double feeSharePct = totalDeductionsTRY > 0 ? (double)(feesTRY / totalDeductionsTRY * 100) : 0;

        var kpiCards = new List<SimilarProductsWinForms.Controls.ToolTipKpiCard>
        {
            new("📢 İç Reklam (Etsy Ads)", $"-₺{innerAdsTRY:N2}", $"-${innerAdsUSD:N2} (%{(totalDeductionsTRY > 0 ? (innerAdsTRY / totalDeductionsTRY * 100) : 0):N1})", Color.FromArgb(239, 68, 68)),
            new("🌐 Dış Reklam (Offsite)", $"-₺{offsiteAdsTRY:N2}", $"-${offsiteAdsUSD:N2} (%{(totalDeductionsTRY > 0 ? (offsiteAdsTRY / totalDeductionsTRY * 100) : 0):N1})", Color.FromArgb(249, 115, 22)),
            new("📋 Etsy Komisyon & Harç", $"-₺{feesTRY:N2}", $"-${feesUSD:N2} (%{feeSharePct:N1})", Color.FromArgb(99, 102, 241))
        };

        var entries = _liveReport.Entries;
        decimal txFeesTRY = entries.Where(e => e.Type == "transaction_fee").Sum(e => Math.Abs(e.AmountTRY));
        decimal procFeesTRY = entries.Where(e => e.Type == "payment_processing").Sum(e => Math.Abs(e.AmountTRY));
        decimal regFeesTRY = entries.Where(e => e.Type == "regulatory_operating_fee").Sum(e => Math.Abs(e.AmountTRY));
        decimal listFeesTRY = entries.Where(e => e.Type == "listing_fee").Sum(e => Math.Abs(e.AmountTRY));
        decimal taxFeesTRY = entries.Where(e => e.Type == "etsy_tax_fee").Sum(e => Math.Abs(e.AmountTRY));

        if (txFeesTRY + procFeesTRY + regFeesTRY + listFeesTRY + taxFeesTRY <= 0 && feesTRY > 0)
        {
            txFeesTRY = Math.Round(feesTRY * 0.45m, 2);
            procFeesTRY = Math.Round(feesTRY * 0.40m, 2);
            regFeesTRY = Math.Round(feesTRY * 0.10m, 2);
            listFeesTRY = Math.Round(feesTRY * 0.05m, 2);
        }

        var rows = new List<SimilarProductsWinForms.Controls.ToolTipTableRow>
        {
            new("Reklam", "İç Reklam (Etsy Ads)", "Tıklama", $"-₺{innerAdsTRY:N2} (-${innerAdsUSD:N2})", false, "", "Etsy platform içi arama sponsorlu reklam harcaması"),
            new("Reklam", "Dış Reklam (Offsite Ads)", "%15", $"-₺{offsiteAdsTRY:N2} (-${offsiteAdsUSD:N2})", false, "", "Google & sosyal medya dış reklam satış komisyonu"),
            new("Kesinti", "İşlem Komisyonu", "%6.5", $"-₺{txFeesTRY:N2}", false, "", "Ürün ve kargo tutarı üzerinden Etsy standart komisyonu"),
            new("Kesinti", "Ödeme İşleme Ücreti", "%6.5+3TL", $"-₺{procFeesTRY:N2}", false, "", "Etsy Payments güvenli ödeme tahsilat masrafı"),
            new("Kesinti", "Yasal İşletim & KDV", "%1.5 + KDV", $"-₺{(regFeesTRY + taxFeesTRY):N2}", false, "", "Türkiye yasal işletim payı ve komisyon KDV'si"),
            new("Kesinti", "İlan Listeleme Ücreti", "$0.20", $"-₺{listFeesTRY:N2}", false, "", "Ürün listeleme ve 4 aylık yenileme bedelleri"),
            new("İade", "İptal ve İadeler", $"{entries.Count(e => e.Type == "refund")} Adet", $"-₺{refundsTRY:N2} (-${refundsUSD:N2})", false, "", "Müşterilere iade edilen sipariş tutarları"),
            new("Toplam", "TOPLAM GİDER & KESİNTİ", "Tümü", $"-₺{totalDeductionsTRY:N2} (-${totalDeductionsUSD:N2})", true, "📢 Toplam", "Brüt cirodan düşülen tüm Etsy kesintileri ve reklam")
        };

        _expensesTooltipPayload = new SimilarProductsWinForms.Controls.ToolTipDataPayload(
            "📢 Etsy Kesintileri & Reklam Harcamaları Analizi",
            $"Toplam Etsy Kesintisi: -₺{totalDeductionsTRY:N2} (-${totalDeductionsUSD:N2}) | Cironun %{(_liveReport.TotalGrossTRY > 0 ? (totalDeductionsTRY / _liveReport.TotalGrossTRY * 100) : 0):N1}'i",
            kpiCards,
            new[] { "Tür", "Kalem Adı", "Oran / Tür", "Tutar (TL / USD)", "Durum", "Açıklama / Muhasebe Mantığı" },
            new[] { 0.10f, 0.24f, 0.13f, 0.21f, 0.08f, 0.24f },
            rows,
            $"💡 Reklam Harcaması: ₺{totalAdsTRY:N2} (${totalAdsUSD:N2}) | Etsy Komisyonları: ₺{feesTRY:N2} (${feesUSD:N2}) • Tıklayarak Muhasebe Paneline geçebilirsiniz."
        );
    }

    private static Control CreateInsightCard(string title, string text, Color accentColor, int width, string? buttonText = null, Action? onButtonClick = null)
    {
        int cardWidth = Math.Max(200, width);
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
        _sidebarNav.AddItem("ai_image", "AI Görsel Stüdyosu", "🎨", "Genel", "PRO");
        _sidebarNav.AddItem("shop", "Mağazam Performansı", "🏬", "Genel");

        _sidebarNav.AddItem("research", "Pazar Araştırması", "🔍", "Araştırma & Analiz");
        _sidebarNav.AddItem("viral_3d", "Viral 3D Model Avcısı", "🔥", "Araştırma & Analiz", "YENİ");
        _sidebarNav.AddItem("competitor_spy", "Rakip & Trend Casusu", "🕵️", "Araştırma & Analiz", "YENİ");
        _sidebarNav.AddItem("external", "Dış Pazar Yeri Bulucu", "🌐", "Araştırma & Analiz");
        _sidebarNav.AddItem("ai_audit", "Mağaza AI Analizi", "🤖", "Araştırma & Analiz", "YENİ");
        _sidebarNav.AddItem("ab_test", "A/B Test Paneli", "📈", "Araştırma & Analiz");

        _sidebarNav.AddItem("automation", "Otomasyon Raporu", "⚡", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("batch", "Toplu İşlem Kuyruğu", "📦", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("shop_vault", "Mağaza Yedek & Transfer", "🛡️", "Otomasyon & Araçlar", "YENİ");
        _sidebarNav.AddItem("profit", "Kâr Simülatörü", "💰", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("aras_shipping", "Aras Global Kargo", "🚚", "Otomasyon & Araçlar", "YENİ");
        _sidebarNav.AddItem("shipentegra_shipping", "ShipEntegra Kargo", "📦", "Otomasyon & Araçlar", "YENİ");
        _sidebarNav.AddItem("tracking", "Takip Geçmişi", "🎯", "Otomasyon & Araçlar");
        _sidebarNav.AddItem("financial", "Finansal Raporlama", "💳", "Otomasyon & Araçlar", "YENİ");
        _sidebarNav.AddItem("ai_usage", "AI Token & Bakiye Takip", "📊", "Otomasyon & Araçlar", "YENİ");

        _sidebarNav.AddItem("notifications", "Telegram Bildirim Botu", "✈️", "Sistem", "YENİ");
        _sidebarNav.AddItem("theme", UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? "Açık Moda Geç" : "Karanlık Moda Geç", UiStyle.CurrentTheme == UiStyle.AppTheme.Dark ? "☀️" : "🌙", "Sistem");
        _sidebarNav.AddItem("api", "Etsy API Ayarları", "⚙️", "Sistem");
        _sidebarNav.AddItem("update", "Sürüm Güncelle (Client)", "🚀", "Sistem", "YENİ");
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

        moduleForm.WindowState = FormWindowState.Normal;
        moduleForm.TopLevel = false;
        moduleForm.FormBorderStyle = FormBorderStyle.None;
        moduleForm.MinimumSize = Size.Empty;
        moduleForm.MaximumSize = Size.Empty;
        moduleForm.Bounds = _mainContainer.ClientRectangle;
        moduleForm.Dock = DockStyle.Fill;
        _mainContainer.Controls.Add(moduleForm);
        moduleForm.Show();
        moduleForm.BringToFront();

        UpdateActiveViewLayout();
        SafeBeginInvoke(UpdateActiveViewLayout);

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
        _dashboardRootPanel.Bounds = _mainContainer.ClientRectangle;
        _dashboardRootPanel.Dock = DockStyle.Fill;
        _mainContainer.Controls.Add(_dashboardRootPanel);

        UpdateActiveViewLayout();
        SafeBeginInvoke(UpdateActiveViewLayout);

        UseWaitCursor = false;
        Cursor = Cursors.Default;
        Cursor.Current = Cursors.Default;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateActiveViewLayout();
    }

    private void SafeBeginInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(action);
        }
        catch { }
    }

    private void UpdateActiveViewLayout()
    {
        if (IsDisposed || _mainContainer == null || _mainContainer.IsDisposed) return;
        var clientRect = _mainContainer.ClientRectangle;
        if (clientRect.Width <= 0 || clientRect.Height <= 0) return;

        if (_currentEmbeddedForm != null && !_currentEmbeddedForm.IsDisposed)
        {
            bool needsLayout = _currentEmbeddedForm.WindowState != FormWindowState.Normal ||
                               _currentEmbeddedForm.Bounds != clientRect ||
                               _currentEmbeddedForm.Dock != DockStyle.Fill;

            if (needsLayout)
            {
                _currentEmbeddedForm.SuspendLayout();
                if (_currentEmbeddedForm.WindowState != FormWindowState.Normal)
                {
                    _currentEmbeddedForm.WindowState = FormWindowState.Normal;
                }
                _currentEmbeddedForm.MinimumSize = Size.Empty;
                _currentEmbeddedForm.MaximumSize = Size.Empty;
                if (_currentEmbeddedForm.Bounds != clientRect)
                {
                    _currentEmbeddedForm.Bounds = clientRect;
                }
                if (_currentEmbeddedForm.Dock != DockStyle.Fill)
                {
                    _currentEmbeddedForm.Dock = DockStyle.Fill;
                }
                _currentEmbeddedForm.ResumeLayout(true);
                _currentEmbeddedForm.PerformLayout();
            }
        }
        else if (_dashboardRootPanel != null && !_dashboardRootPanel.IsDisposed && _dashboardRootPanel.Parent == _mainContainer)
        {
            bool needsLayout = _dashboardRootPanel.Bounds != clientRect || _dashboardRootPanel.Dock != DockStyle.Fill;
            if (needsLayout)
            {
                _dashboardRootPanel.SuspendLayout();
                if (_dashboardRootPanel.Bounds != clientRect)
                {
                    _dashboardRootPanel.Bounds = clientRect;
                }
                if (_dashboardRootPanel.Dock != DockStyle.Fill)
                {
                    _dashboardRootPanel.Dock = DockStyle.Fill;
                }
                _dashboardRootPanel.ResumeLayout(true);
                _dashboardRootPanel.PerformLayout();
            }
        }
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
        if (targetModule is "update")
        {
            using var form = new ClientUpdateDialog();
            form.ShowDialog(this);
            return;
        }

        Form? nextForm = targetModule switch
        {
            "fast_creator" => new FastListingCreatorForm(_aiListingOptimizer),
            "creator" => new ProductDiscoveryListingCreatorForm(_aiListingOptimizer, historyService: _optimizationHistoryService),
            "ai_image" => new AiListingImageForm(_aiListingOptimizer, initialTab: 0),
            "bg_editor" => new AiListingImageForm(_aiListingOptimizer, initialTab: 1),
            "shop" => new OwnShopPerformanceForm(_shopPerformanceService, _shopPerformanceHistoryService, _aiListingOptimizer, _optimizationHistoryService),
            "research" => new MarketResearchForm(_keywordUseCase, _trackingService, _optimizationHistoryService, _aiListingOptimizer),
            "competitor_spy" => new CompetitorAndTrendSpyForm(_aiListingOptimizer),
            "external" => new ExternalMarketplaceDiscoveryForm(_aiListingOptimizer),
            "ai_audit" => new OwnShopListingAiAuditForm(_aiListingOptimizer, _optimizationHistoryService),
            "ab_test" => new ListingAbTestForm(_abTestService, _aiListingOptimizer),
            "automation" => new AutomationReportingForm(_automationSettingsStore, _automationScheduler, _windowsTaskScheduler),
            "batch" => new BatchQueueForm(_batchQueueProcessorService, _abTestService),
            "shop_vault" => new ShopVaultForm(),
            "viral_3d" => new Trending3DModelHunterForm(),
            "profit" => new ProfitCalculatorForm(),
            "aras_shipping" => new ArasGlobalShippingCalculatorForm(),
            "shipentegra_shipping" => new ShipEntegraShippingCalculatorForm(),
            "tracking" => new TrackingHistoryForm(_trackingService),
            "financial" => new FinancialReportForm(),
            "ai_usage" => new AiUsageDashboardForm(),
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
