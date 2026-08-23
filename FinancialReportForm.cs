namespace SimilarProductsWinForms;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;
using System.Globalization;

internal sealed class FinancialReportForm : Form
{
    // ── State ──────────────────────────────────────────────────────────────────
    private FinancialReport _report = FinancialReport.Empty;
    private readonly FinancialReportService _service = new();
    private CancellationTokenSource _cts = new();

    // ── UI: KPI Labels ─────────────────────────────────────────────────────────
    private readonly Label _kpiGross        = new();
    private readonly Label _kpiFees         = new();
    private readonly Label _kpiInnerAds     = new();
    private readonly Label _kpiOffsiteAds   = new();
    private readonly Label _kpiRefunds      = new();
    private readonly Label _kpiNet          = new();
    private readonly Label _kpiProductCosts = new();
    private readonly Label _kpiRealProfit   = new();
    private readonly Label _kpiDeposits     = new();
    private readonly Label _statusLabel     = new();

    // ── UI: Filters & Currency ────────────────────────────────────────────────
    private readonly ComboBox _cboDateRange     = new();
    private readonly DateTimePicker _dtpFrom    = new();
    private readonly DateTimePicker _dtpTo      = new();
    private readonly CheckBox _chkUseTry        = new();
    private readonly NumericUpDown _numExchangeRate = new();
    private readonly Label _lblMode             = new();

    // ── UI: Charts & Grids ─────────────────────────────────────────────────────
    private CartesianChart  _barChart   = null!;
    private CartesianChart  _lineChart  = null!;
    private PieChart        _pieChart   = null!;
    private readonly TabControl _tabMain = new();

    private readonly DataGridView _gridPeriod  = new();
    private readonly DataGridView _gridEntries = new();
    private readonly ComboBox _cboPeriodType   = new();

    public FinancialReportForm()
    {
        Text = "💳 Finansal Raporlama & Muhasebe — Etsy Ödeme Analizi";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);

        InitializeCharts();
        BuildLayout();
        UiStyle.ApplyTheme(this);
        Shown += async (_, _) => await LoadReportAsync();
    }

    // ── Grafik Başlatma ────────────────────────────────────────────────────────

    private void InitializeCharts()
    {
        _barChart = new CartesianChart
        {
            Dock = DockStyle.Fill,
            Series = Array.Empty<ISeries>(),
            XAxes = new[] { new Axis { Labels = Array.Empty<string>(), TextSize = 11 } },
            YAxes = new[] { new Axis { TextSize = 11, Labeler = v => $"${v:N0}" } },
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            BackColor = Color.Transparent,
        };

        _lineChart = new CartesianChart
        {
            Dock = DockStyle.Fill,
            Series = Array.Empty<ISeries>(),
            XAxes = new[] { new Axis { Labels = Array.Empty<string>(), TextSize = 11 } },
            YAxes = new[] { new Axis { TextSize = 11, Labeler = v => $"${v:N0}" } },
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            BackColor = Color.Transparent,
        };

        _pieChart = new PieChart
        {
            Dock = DockStyle.Fill,
            Series = Array.Empty<ISeries>(),
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
            BackColor = Color.Transparent,
        };
    }

    // ── Layout Builder ─────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));   // Başlık
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));   // Filtre çubuğu
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));  // KPI kartları (8 Adet)
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // İçerik sekmeleri
        Controls.Add(root);

        root.Controls.Add(BuildHeader(),     0, 0);
        root.Controls.Add(BuildFilterBar(), 0, 1);
        root.Controls.Add(BuildKpiStrip(),  0, 2);
        root.Controls.Add(BuildContent(),   0, 3);
    }

    // — Başlık Satırı ——————————————————————————————————————————————————————————

    private Control BuildHeader()
    {
        var p = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 4),
        };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "💳 Finansal Raporlama & Muhasebe Paneli",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
        });
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Etsy Ödeme Defteri, Banka Transferleri, Ürün Maliyetleri ve TL Kâr Analizi",
            Font = UiStyle.SubtitleFont,
            ForeColor = UiStyle.TextMuted,
        });
        p.Controls.Add(titlePanel, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Veriler yükleniyor...";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        p.Controls.Add(_statusLabel, 1, 0);

        return p;
    }

    // — Filtre Çubuğu ——————————————————————————————————————————————————————————

    private Control BuildFilterBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 11,
            Padding = new Padding(0, 2, 0, 2),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // ComboBox
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); // DtpFrom
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); // DtpTo
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); // Yenile
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Maliyet Yönetimi
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); // API Ayarları
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // TL Kur Checkbox
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));  // Kur Tutar
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Excel
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Boşluk
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // Mode etiketi

        // Tarih ön-ayarları
        _cboDateRange.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboDateRange.Dock = DockStyle.Fill;
        _cboDateRange.Margin = new Padding(0, 0, 4, 0);
        _cboDateRange.Items.AddRange(new object[]
        {
            "Son 7 Gün", "Son 30 Gün", "Son 90 Gün",
            "Bu Ay", "Geçen Ay", "Bu Yıl", "Özel Aralık"
        });
        _cboDateRange.SelectedIndex = 1;
        _cboDateRange.SelectedIndexChanged += OnDateRangeChanged;
        bar.Controls.Add(_cboDateRange, 0, 0);

        _dtpFrom.Format = DateTimePickerFormat.Short;
        _dtpFrom.Dock = DockStyle.Fill;
        _dtpFrom.Margin = new Padding(0, 0, 4, 0);
        _dtpFrom.Value = DateTime.Today.AddDays(-30);
        bar.Controls.Add(_dtpFrom, 1, 0);

        _dtpTo.Format = DateTimePickerFormat.Short;
        _dtpTo.Dock = DockStyle.Fill;
        _dtpTo.Margin = new Padding(0, 0, 6, 0);
        _dtpTo.Value = DateTime.Today;
        bar.Controls.Add(_dtpTo, 2, 0);

        // Yenile Butonu
        var btnRefresh = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🔄 Etsy'den Yenile",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 4, 0),
        };
        btnRefresh.Click += async (_, _) => await LoadReportAsync(forceApi: true);
        bar.Controls.Add(btnRefresh, 3, 0);

        // Ürün Maliyetleri Butonu
        var btnCosts = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🏷️ Ürün Maliyetleri",
            NormalColor = UiStyle.SecondaryColor,
            HoverColor = UiStyle.SecondaryHover,
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 4, 0),
        };
        btnCosts.Click += (_, _) =>
        {
            using var form = new ProductCostManagerForm();
            form.ShowDialog(this);
            _ = LoadReportAsync(forceApi: false);
        };
        bar.Controls.Add(btnCosts, 4, 0);

        // API Ayarları Butonu
        var btnApi = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "⚙️ API Ayarları",
            NormalColor = UiStyle.AccentColor,
            HoverColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 4, 0),
        };
        btnApi.Click += (_, _) =>
        {
            using var form = new EtsyApiSettingsForm();
            form.ShowDialog(this);
            _ = LoadReportAsync(forceApi: true);
        };
        bar.Controls.Add(btnApi, 5, 0);

        // TL Kur Dönüşüm Checkbox & Input
        _chkUseTry.Text = "🇹🇷 TL (₺) Kur:";
        _chkUseTry.Dock = DockStyle.Fill;
        _chkUseTry.Checked = true;
        _chkUseTry.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _chkUseTry.ForeColor = UiStyle.TextDark;
        _chkUseTry.CheckedChanged += (_, _) => UpdateKpis();
        bar.Controls.Add(_chkUseTry, 6, 0);

        _numExchangeRate.Dock = DockStyle.Fill;
        _numExchangeRate.DecimalPlaces = 2;
        _numExchangeRate.Maximum = 500;
        _numExchangeRate.Value = 36.50m;
        _numExchangeRate.ValueChanged += (_, _) => UpdateKpis();
        bar.Controls.Add(_numExchangeRate, 7, 0);

        // Excel Butonu
        var btnExcel = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📊 Excel",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 4, 0),
        };
        btnExcel.Click += OnExportExcel;
        bar.Controls.Add(btnExcel, 8, 0);

        // Mod Etiketi
        _lblMode.Dock = DockStyle.Fill;
        _lblMode.Text = "🎮 Demo Modu";
        _lblMode.TextAlign = ContentAlignment.MiddleRight;
        _lblMode.ForeColor = UiStyle.WarningColor;
        _lblMode.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        bar.Controls.Add(_lblMode, 10, 0);

        return bar;
    }

    // — KPI Kartları Şeridi (8 Kartlı Yapı) ———————————————————————————————————

    // — KPI Kartları Şeridi (9 Kartlı Yapı - Etsy Shop Manager Uyumlu) ——————————

    private Control BuildKpiStrip()
    {
        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            Padding = new Padding(0, 0, 0, 4),
        };
        for (int i = 0; i < 9; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 9));

        BuildKpiCard(strip, 0, "💰 BRÜT SATIŞ",     _kpiGross,        UiStyle.SuccessColor);
        BuildKpiCard(strip, 1, "📋 ETSY KESİNTİSİ",  _kpiFees,         UiStyle.WarningColor);
        BuildKpiCard(strip, 2, "📢 İÇ REKLAM",       _kpiInnerAds,     UiStyle.DangerColor);
        BuildKpiCard(strip, 3, "🌐 DIŞ REKLAM",      _kpiOffsiteAds,   Color.FromArgb(234, 88, 12));
        BuildKpiCard(strip, 4, "↩️ İADELER",         _kpiRefunds,      UiStyle.DangerColor);
        BuildKpiCard(strip, 5, "✅ ETSY NET GELİR",  _kpiNet,          UiStyle.PrimaryColor);
        BuildKpiCard(strip, 6, "📦 SİPARİŞ MALİYETİ",_kpiProductCosts, UiStyle.WarningColor);
        BuildKpiCard(strip, 7, "💵 GERÇEK NET KÂR", _kpiRealProfit,   UiStyle.SuccessColor);
        BuildKpiCard(strip, 8, "🏦 BANKA YATIRIMI",  _kpiDeposits,     UiStyle.AccentColor);

        return strip;
    }

    private static void BuildKpiCard(TableLayoutPanel parent, int col, string title, Label valueLabel, Color accentColor)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 0, 3, 0),
            Padding = new Padding(8, 6, 8, 6),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 7F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "—";
        valueLabel.Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold);
        valueLabel.ForeColor = accentColor;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(valueLabel, 0, 1);

        var accent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = accentColor,
            Height = 3,
        };
        layout.Controls.Add(accent, 0, 2);

        card.Controls.Add(layout);
        parent.Controls.Add(card, col, 0);
    }

    // — Ana Sekme Yapısı —————————————————————————————————————————————————————

    private Control BuildContent()
    {
        _tabMain.Dock = DockStyle.Fill;
        _tabMain.Font = new Font("Segoe UI Semibold", 9.5F);

        var tabCharts = new TabPage("📊 Grafik Analizleri")   { Padding = new Padding(6) };
        var tabPeriod = new TabPage("📅 Dönemsel Muhasebe")   { Padding = new Padding(6) };
        var tabGrid   = new TabPage("📋 Ödeme Defteri Kayıtları") { Padding = new Padding(6) };

        tabCharts.Controls.Add(BuildChartPanel());
        tabPeriod.Controls.Add(BuildPeriodPanel());
        tabGrid.Controls.Add(BuildGridPanel());

        _tabMain.TabPages.Add(tabCharts);
        _tabMain.TabPages.Add(tabPeriod);
        _tabMain.TabPages.Add(tabGrid);

        return _tabMain;
    }

    private Control BuildChartPanel()
    {
        var tabCharts = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F) };

        var tabBar  = new TabPage("📊 Aylık Gelir/Gider") { Padding = new Padding(4) };
        var tabLine = new TabPage("📈 Net Gelir Trendi")  { Padding = new Padding(4) };
        var tabPie  = new TabPage("🥧 Dağılım Grafiği")  { Padding = new Padding(4) };

        tabBar.Controls.Add(_barChart);
        tabLine.Controls.Add(_lineChart);
        tabPie.Controls.Add(_pieChart);

        tabCharts.TabPages.Add(tabBar);
        tabCharts.TabPages.Add(tabLine);
        tabCharts.TabPages.Add(tabPie);

        return tabCharts;
    }

    private Control BuildPeriodPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        topBar.Controls.Add(new Label
        {
            Text = "📅 Dönem Tipi:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(0, 4, 6, 0),
        });

        _cboPeriodType.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboPeriodType.Items.AddRange(new object[] { "Günlük Muhasebe", "Haftalık Muhasebe", "Aylık Muhasebe", "Yıllık Muhasebe" });
        _cboPeriodType.SelectedIndex = 0;
        _cboPeriodType.Width = 160;
        _cboPeriodType.SelectedIndexChanged += (_, _) => UpdatePeriodGrid();
        topBar.Controls.Add(_cboPeriodType);

        root.Controls.Add(topBar, 0, 0);

        UiStyle.ConfigureBaseGrid(_gridPeriod);
        _gridPeriod.Dock = DockStyle.Fill;
        root.Controls.Add(_gridPeriod, 0, 1);

        card.Controls.Add(root);
        return card;
    }

    private Control BuildGridPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        ConfigureGrid();
        card.Controls.Add(_gridEntries);
        return card;
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_gridEntries);
        _gridEntries.Dock = DockStyle.Fill;
    }

    // ── Veri Yükleme ───────────────────────────────────────────────────────────

    private async Task LoadReportAsync(bool forceApi = false)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetStatus("⏳ Yükleniyor...", UiStyle.TextMuted);

        try
        {
            var fromDt = DateTime.SpecifyKind(_dtpFrom.Value.Date, DateTimeKind.Unspecified);
            var toDt   = DateTime.SpecifyKind(_dtpTo.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified);
            var from   = new DateTimeOffset(fromDt, TimeZoneInfo.Local.GetUtcOffset(fromDt));
            var to     = new DateTimeOffset(toDt, TimeZoneInfo.Local.GetUtcOffset(toDt));

            var settings = EtsyApiSettingsStore.Load();
            bool canAttemptApi = settings.HasApiCredentials && (!string.IsNullOrWhiteSpace(settings.AccessToken) || !string.IsNullOrWhiteSpace(settings.RefreshToken));

            if (canAttemptApi)
            {
                _report = await _service.GetReportAsync(settings, from, to, _numExchangeRate.Value, ct);
                _lblMode.Text = string.IsNullOrWhiteSpace(settings.ShopId) ? "🟢 Canlı Etsy API" : $"🟢 Canlı Etsy API ({settings.ShopId})";
                _lblMode.ForeColor = UiStyle.SuccessColor;
                SetStatus($"✅ Canlı Etsy API verisi yüklendi: {_report.Entries.Count} kayıt | {_report.PeriodStart:dd.MM.yyyy} — {_report.PeriodEnd:dd.MM.yyyy}", UiStyle.SuccessColor);
            }
            else
            {
                _report = FinancialReport.Empty;
                _lblMode.Text = "⚠️ Mağaza Bağlı Değil";
                _lblMode.ForeColor = UiStyle.WarningColor;
                SetStatus("⚠️ Gerçek dükkan verilerinizi görüntülemek için '⚙️ API Ayarları' butonuna tıklayarak Etsy mağazanızı bağlayın.", UiStyle.WarningColor);
            }

            if (ct.IsCancellationRequested) return;

            UpdateKpis();
            UpdateCharts();
            UpdateGrid();
            UpdatePeriodGrid();
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            SetStatus($"❌ Etsy API Hatası: {ex.Message}", UiStyle.DangerColor);
            _lblMode.Text = "❌ API Bağlantı Hatası";
            _lblMode.ForeColor = UiStyle.DangerColor;
        }
    }

    // ── KPI Güncellemesi ───────────────────────────────────────────────────────

    private void UpdateKpis()
    {
        decimal rate = _numExchangeRate.Value;
        bool showTry = _chkUseTry.Checked && rate > 0;

        _kpiGross.Text        = showTry ? $"₺{_report.DailySummaries.Sum(d => d.GrossSales * d.AverageExchangeRate):N2}" : $"${_report.TotalGross:N2}";
        _kpiFees.Text         = showTry ? $"₺{_report.DailySummaries.Sum(d => d.EtsyFees * d.AverageExchangeRate):N2}" : $"${_report.TotalFees:N2}";
        _kpiInnerAds.Text     = showTry ? $"₺{_report.DailySummaries.Sum(d => d.InnerAdFees * d.AverageExchangeRate):N2}" : $"${_report.TotalInnerAdFees:N2}";
        _kpiOffsiteAds.Text   = showTry ? $"₺{_report.DailySummaries.Sum(d => d.OffsiteAdFees * d.AverageExchangeRate):N2}" : $"${_report.TotalOffsiteAdFees:N2}";
        _kpiRefunds.Text      = showTry ? $"₺{_report.DailySummaries.Sum(d => d.Refunds * d.AverageExchangeRate):N2}"   : $"${_report.TotalRefunds:N2}";
        _kpiNet.Text          = showTry ? $"₺{_report.DailySummaries.Sum(d => d.EtsyNetRevenue * d.AverageExchangeRate):N2}" : $"${_report.TotalNet:N2}";
        _kpiProductCosts.Text = showTry ? $"₺{_report.DailySummaries.Sum(d => d.ProductCosts * d.AverageExchangeRate):N2}" : $"${_report.TotalProductCosts:N2}";

        decimal profitUSD = _report.RealNetProfitUSD;
        decimal profitTRY = _report.DailySummaries.Sum(d => d.RealNetProfitTRY);
        _kpiRealProfit.Text   = showTry ? $"₺{profitTRY:N2}" : $"${profitUSD:N2}";
        _kpiRealProfit.ForeColor = profitUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _kpiDeposits.Text     = showTry ? $"₺{_report.DailySummaries.Sum(d => d.Deposits * d.AverageExchangeRate):N2}"   : $"${_report.TotalDeposits:N2}";

        _kpiNet.ForeColor = _report.TotalNet >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;
    }

    // ── Dönemsel Muhasebe Grid Güncellemesi ───────────────────────────────────

    private void UpdatePeriodGrid()
    {
        _gridPeriod.Columns.Clear();
        UiStyle.ConfigureBaseGrid(_gridPeriod);

        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dönem / Tarih", Name = "Period", Width = 120 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Brüt Satış", Name = "Gross", Width = 100 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Etsy Ücretleri", Name = "Fees", Width = 105 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "İç Reklam", Name = "InnerAds", Width = 100 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dış Reklam", Name = "OffsiteAds", Width = 100 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "İadeler", Name = "Refunds", Width = 95 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Banka Yatırımı", Name = "Deposits", Width = 110 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün Maliyetleri", Name = "Costs", Width = 115 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Etsy Net Gelir", Name = "NetRevenue", Width = 115 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sipariş Gün Kuru", Name = "Rate", Width = 120 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Gerçek Net Kâr ($)", Name = "ProfitUSD", Width = 125 });
        _gridPeriod.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Günlük TL Kârı (₺)", Name = "ProfitTRY", Width = 135 });

        List<PeriodFinancialSummary> summaries = _cboPeriodType.SelectedIndex switch
        {
            0 => _report.DailySummaries,
            1 => _report.WeeklySummaries,
            2 => _report.MonthlySummaries,
            _ => _report.YearlySummaries,
        };

        _gridPeriod.Rows.Clear();
        foreach (var s in summaries)
        {
            int idx = _gridPeriod.Rows.Add(
                s.PeriodLabel,
                $"${s.GrossSales:N2}",
                $"${s.EtsyFees:N2}",
                $"${s.InnerAdFees:N2}",
                $"${s.OffsiteAdFees:N2}",
                $"${s.Refunds:N2}",
                $"${s.Deposits:N2}",
                $"${s.ProductCosts:N2}",
                $"${s.EtsyNetRevenue:N2}",
                $"₺{s.AverageExchangeRate:N2}",
                $"${s.RealNetProfitUSD:N2}",
                $"₺{s.RealNetProfitTRY:N2}");

            if (s.RealNetProfitUSD >= 0)
                _gridPeriod.Rows[idx].DefaultCellStyle.ForeColor = UiStyle.SuccessColor;
            else
                _gridPeriod.Rows[idx].DefaultCellStyle.ForeColor = UiStyle.DangerColor;
        }
    }

    // ── Grafik Güncellemesi ────────────────────────────────────────────────────

    private void UpdateCharts()
    {
        var months = _report.Monthly;
        if (months.Count == 0) return;

        var labels = months.Select(m => m.MonthName).ToArray();
        var textPaint = new SolidColorPaint(new SKColor(148, 163, 184));

        var salesVals    = months.Select(m => (double)m.GrossSales).ToArray();
        var netVals      = months.Select(m => (double)m.NetIncome).ToArray();
        var feeVals      = months.Select(m => (double)m.TotalFees).ToArray();
        var refundVals   = months.Select(m => (double)m.Refunds).ToArray();

        _barChart.Series = new ISeries[]
        {
            new ColumnSeries<double> { Name = "Brüt Satış", Values = salesVals, Fill = new SolidColorPaint(new SKColor(16, 185, 129, 200)), Rx = 4, Ry = 4 },
            new ColumnSeries<double> { Name = "Net Gelir", Values = netVals, Fill = new SolidColorPaint(new SKColor(99, 102, 241, 220)), Rx = 4, Ry = 4 },
            new ColumnSeries<double> { Name = "Ücretler", Values = feeVals, Fill = new SolidColorPaint(new SKColor(245, 158, 11, 200)), Rx = 4, Ry = 4 },
            new ColumnSeries<double> { Name = "İadeler", Values = refundVals, Fill = new SolidColorPaint(new SKColor(239, 68, 68, 200)), Rx = 4, Ry = 4 },
        };
        _barChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint } };
        _barChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"${v:N0}" } };

        _lineChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "Net Gelir", Values = netVals,
                Fill = new LinearGradientPaint(new SKColor(99, 102, 241, 60), new SKColor(99, 102, 241, 0)),
                Stroke = new SolidColorPaint(new SKColor(99, 102, 241), 3),
                GeometrySize = 8, LineSmoothness = 0.5,
            },
        };
        _lineChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint } };
        _lineChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"${v:N0}" } };

        _pieChart.Series = new ISeries[]
        {
            new PieSeries<double> { Name = "✅ Net Gelir", Values = new[] { (double)Math.Max(0, _report.TotalNet) }, Fill = new SolidColorPaint(new SKColor(16, 185, 129)) },
            new PieSeries<double> { Name = "📋 Etsy Ücretleri", Values = new[] { (double)_report.TotalFees }, Fill = new SolidColorPaint(new SKColor(245, 158, 11)) },
            new PieSeries<double> { Name = "📢 İç Reklam", Values = new[] { (double)_report.TotalInnerAdFees }, Fill = new SolidColorPaint(new SKColor(139, 92, 246)) },
            new PieSeries<double> { Name = "🌐 Dış Reklam", Values = new[] { (double)_report.TotalOffsiteAdFees }, Fill = new SolidColorPaint(new SKColor(234, 88, 12)) },
            new PieSeries<double> { Name = "↩️ İadeler", Values = new[] { (double)_report.TotalRefunds }, Fill = new SolidColorPaint(new SKColor(239, 68, 68)) },
        };
    }

    // ── Defter Kayıtları Grid Güncellemesi ─────────────────────────────────────

    private void UpdateGrid()
    {
        _gridEntries.Columns.Clear();
        UiStyle.ConfigureBaseGrid(_gridEntries);

        _gridEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tarih",       DataPropertyName = nameof(LedgerRow.Date),        Width = 115 });
        _gridEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tür",         DataPropertyName = nameof(LedgerRow.TypeLabel),    Width = 155 });
        _gridEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Brüt Tutar",  DataPropertyName = nameof(LedgerRow.AmountStr),    Width = 110 });
        _gridEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Net Tutar",   DataPropertyName = nameof(LedgerRow.NetAmountStr), Width = 110 });
        _gridEntries.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Para Birimi", DataPropertyName = nameof(LedgerRow.Currency),     Width = 90  });
        var descCol = new DataGridViewTextBoxColumn { HeaderText = "Açıklama", DataPropertyName = nameof(LedgerRow.Description), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
        _gridEntries.Columns.Add(descCol);

        var rows = _report.Entries
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new LedgerRow(e))
            .ToList();

        _gridEntries.DataSource = rows;
    }

    // ── Tarih Aralığı Değişimi ────────────────────────────────────────────────

    private void OnDateRangeChanged(object? sender, EventArgs e)
    {
        var today = DateTime.Today;
        switch (_cboDateRange.SelectedIndex)
        {
            case 0: _dtpFrom.Value = today.AddDays(-7); _dtpTo.Value = today; break;
            case 1: _dtpFrom.Value = today.AddDays(-30); _dtpTo.Value = today; break;
            case 2: _dtpFrom.Value = today.AddDays(-90); _dtpTo.Value = today; break;
            case 3: _dtpFrom.Value = new DateTime(today.Year, today.Month, 1); _dtpTo.Value = today; break;
            case 4:
                var lastMonth = today.AddMonths(-1);
                _dtpFrom.Value = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                _dtpTo.Value   = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)); break;
            case 5: _dtpFrom.Value = new DateTime(today.Year, 1, 1); _dtpTo.Value = today; break;
            case 6: return;
        }
        _ = LoadReportAsync();
    }

    // ── Dışa Aktarma ──────────────────────────────────────────────────────────

    private void OnExportExcel(object? sender, EventArgs e)
    {
        if (_report.Entries.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak veri yok.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Title = "Excel Raporu Kaydet",
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Etsy_Finansal_{_report.PeriodStart:yyyyMMdd}_{_report.PeriodEnd:yyyyMMdd}.xlsx",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            FinancialReportExporter.ExportToExcel(_report, dlg.FileName);
            MessageBox.Show($"Excel raporu kaydedildi:\n{dlg.FileName}", "✅ Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel dışa aktarma hatası: {ex.Message}", "❌ Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private sealed class LedgerRow(LedgerEntry e)
    {
        public string Date         => e.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
        public string TypeLabel    => e.Type switch
        {
            "sale"               => "💰 Satış",
            "refund"             => "↩️ İade",
            "listing_fee"        => "📋 Liste Ücreti",
            "transaction_fee"    => "🔄 İşlem Ücreti",
            "ad_fee"             => "📢 İç Reklam",
            "offsite_ads"        => "🌐 Dış Reklam",
            "deposit"            => "🏦 Banka Yatırımı",
            "shipping"           => "🚚 Kargo",
            "payment_processing" => "💳 Ödeme İşlem",
            _                    => e.Type
        };
        public string AmountStr    => $"{(e.Type == "sale" ? "+" : "")}{e.Amount:N2} {e.Currency}";
        public string NetAmountStr => $"{e.NetAmount:+#,##0.00;-#,##0.00} {e.Currency}";
        public string Currency     => e.Currency;
        public string Description  => e.Description;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosed(e);
    }
}
