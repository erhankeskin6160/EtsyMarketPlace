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
    private bool _isMockMode = true;
    private CancellationTokenSource _cts = new();

    // ── UI: KPI Labels ─────────────────────────────────────────────────────────
    private readonly Label _kpiGross        = new();
    private readonly Label _kpiNet          = new();
    private readonly Label _kpiFees         = new();
    private readonly Label _kpiRefunds      = new();
    private readonly Label _kpiAds          = new();
    private readonly Label _kpiOrders       = new();
    private readonly Label _kpiAov          = new();
    private readonly Label _statusLabel     = new();

    // ── UI: Filters ────────────────────────────────────────────────────────────
    private readonly ComboBox _cboDateRange     = new();
    private readonly DateTimePicker _dtpFrom    = new();
    private readonly DateTimePicker _dtpTo      = new();
    private readonly Label _lblMode             = new();

    // ── UI: Charts ────────────────────────────────────────────────────────────
    private CartesianChart  _barChart   = null!;
    private CartesianChart  _lineChart  = null!;
    private PieChart        _pieChart   = null!;
    private readonly TabControl _tabCharts = new();

    // ── UI: Grid ──────────────────────────────────────────────────────────────
    private readonly DataGridView _gridEntries = new();

    public FinancialReportForm()
    {
        Text = "💳 Finansal Raporlama — Etsy Ödeme Analizi";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);

        InitializeCharts();
        BuildLayout();
        UiStyle.AttachSidebarNav(this, "financial");
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // Başlık
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Filtre çubuğu
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));  // KPI kartları
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // İçerik (grafik + grid)
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
            Text = "💳 Finansal Raporlama",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
        });
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Etsy Ödeme Defteri — Gelir, Gider ve Net Kazanç Analizi",
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
            ColumnCount = 8,
            Padding = new Padding(0, 4, 0, 4),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); // ComboBox
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // DtpFrom
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // DtpTo
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Yenile
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Excel
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // CSV
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // boşluk
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // mode etiketi

        // Tarih ön-ayarları
        _cboDateRange.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboDateRange.Dock = DockStyle.Fill;
        _cboDateRange.Margin = new Padding(0, 0, 6, 0);
        _cboDateRange.Items.AddRange(new object[]
        {
            "Son 7 Gün", "Son 30 Gün", "Son 90 Gün",
            "Bu Ay", "Geçen Ay", "Bu Yıl", "Özel Aralık"
        });
        _cboDateRange.SelectedIndex = 1; // Son 30 Gün
        _cboDateRange.SelectedIndexChanged += OnDateRangeChanged;
        bar.Controls.Add(_cboDateRange, 0, 0);

        _dtpFrom.Format = DateTimePickerFormat.Short;
        _dtpFrom.Dock = DockStyle.Fill;
        _dtpFrom.Margin = new Padding(0, 0, 4, 0);
        _dtpFrom.Value = DateTime.Today.AddDays(-30);
        bar.Controls.Add(_dtpFrom, 1, 0);

        _dtpTo.Format = DateTimePickerFormat.Short;
        _dtpTo.Dock = DockStyle.Fill;
        _dtpTo.Margin = new Padding(0, 0, 8, 0);
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
            Margin = new Padding(0, 0, 6, 0),
        };
        btnRefresh.Click += async (_, _) => await LoadReportAsync(forceApi: true);
        bar.Controls.Add(btnRefresh, 3, 0);

        // Excel Butonu
        var btnExcel = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📊 Excel'e Aktar",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 6, 0),
        };
        btnExcel.Click += OnExportExcel;
        bar.Controls.Add(btnExcel, 4, 0);

        // CSV Butonu
        var btnCsv = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📋 CSV",
            NormalColor = UiStyle.SecondaryColor,
            HoverColor = UiStyle.SecondaryHover,
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 0, 0),
        };
        btnCsv.Click += OnExportCsv;
        bar.Controls.Add(btnCsv, 5, 0);

        // Mod Etiketi
        _lblMode.Dock = DockStyle.Fill;
        _lblMode.Text = "🎮 Demo Modu";
        _lblMode.TextAlign = ContentAlignment.MiddleRight;
        _lblMode.ForeColor = UiStyle.WarningColor;
        _lblMode.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        bar.Controls.Add(_lblMode, 7, 0);

        return bar;
    }

    // — KPI Kartları Şeridi ————————————————————————————————————————————————────

    private Control BuildKpiStrip()
    {
        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            Padding = new Padding(0, 0, 0, 6),
        };
        for (int i = 0; i < 7; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));

        BuildKpiCard(strip, 0, "💰 BRÜT SATIŞ",    _kpiGross,   UiStyle.SuccessColor);
        BuildKpiCard(strip, 1, "✅ NET GELİR",      _kpiNet,     UiStyle.PrimaryColor);
        BuildKpiCard(strip, 2, "📋 ETSy ÜCRETLERİ", _kpiFees,   UiStyle.WarningColor);
        BuildKpiCard(strip, 3, "📢 REKLAM GİDERİ",  _kpiAds,    UiStyle.DangerColor);
        BuildKpiCard(strip, 4, "↩️ İADELER",        _kpiRefunds, UiStyle.DangerColor);
        BuildKpiCard(strip, 5, "📦 SİPARİŞ SAYISI", _kpiOrders,  UiStyle.AccentColor);
        BuildKpiCard(strip, 6, "📐 ORT. SİPARİŞ",   _kpiAov,    UiStyle.TextMuted);

        return strip;
    }

    private static void BuildKpiCard(TableLayoutPanel parent, int col, string title, Label valueLabel, Color accentColor)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(12, 8, 12, 8),
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "—";
        valueLabel.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
        valueLabel.ForeColor = accentColor;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(valueLabel, 0, 1);

        // Alt renk çizgisi
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

    // — İçerik Alanı (Grafik + Grid) ——————————————————————————————————————————

    private Control BuildContent()
    {
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
        };
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        split.Controls.Add(BuildChartPanel(), 0, 0);
        split.Controls.Add(BuildGridPanel(), 0, 1);

        return split;
    }

    // — Grafik Paneli ——————————————————————————————————————————————————————————

    private Control BuildChartPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(12),
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        _tabCharts.Dock = DockStyle.Fill;
        _tabCharts.Font = new Font("Segoe UI Semibold", 9.5F);

        var tabBar   = new TabPage("📊 Aylık Gelir/Gider")  { Padding = new Padding(4) };
        var tabLine  = new TabPage("📈 Net Gelir Trendi")   { Padding = new Padding(4) };
        var tabPie   = new TabPage("🥧 Gelir Kaynakları")   { Padding = new Padding(4) };

        tabBar.Controls.Add(_barChart);
        tabLine.Controls.Add(_lineChart);
        tabPie.Controls.Add(_pieChart);

        _tabCharts.TabPages.Add(tabBar);
        _tabCharts.TabPages.Add(tabLine);
        _tabCharts.TabPages.Add(tabPie);

        card.Controls.Add(_tabCharts);
        return card;
    }

    // — Grid Paneli ————————————————————————————————————————————————————————────

    private Control BuildGridPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(12),
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inner.BackColor = Color.Transparent;

        inner.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "📋 Defter Kayıtları",
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        ConfigureGrid();
        inner.Controls.Add(_gridEntries, 0, 1);
        card.Controls.Add(inner);
        return card;
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_gridEntries);
        _gridEntries.Dock = DockStyle.Fill;

        AddCol("Tarih",         nameof(LedgerRow.Date),        120);
        AddCol("Tür",           nameof(LedgerRow.TypeLabel),   160);
        AddCol("Brüt Tutar",    nameof(LedgerRow.AmountStr),   110);
        AddCol("Net Tutar",     nameof(LedgerRow.NetAmountStr),110);
        AddCol("Para Birimi",   nameof(LedgerRow.Currency),    90);
        AddCol("Açıklama",      nameof(LedgerRow.Description), 0, fill: true);
    }

    private static void AddCol(string header, string prop, int width, bool fill = false)
    { /* Inline helper kullanmak yerine grid extension yapalım */ }

    // ── Veri Yükleme ───────────────────────────────────────────────────────────

    private async Task LoadReportAsync(bool forceApi = false)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetStatus("⏳ Yükleniyor...", UiStyle.TextMuted);

        try
        {
            var from = new DateTimeOffset(_dtpFrom.Value.Date, TimeSpan.Zero);
            var to   = new DateTimeOffset(_dtpTo.Value.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero);

            // API key kontrolü
            var settings = EtsyApiSettingsStore.Load();
            bool hasApiAccess = forceApi
                && !string.IsNullOrWhiteSpace(settings.AccessToken)
                && !string.IsNullOrWhiteSpace(settings.ShopId);

            if (hasApiAccess)
            {
                _isMockMode = false;
                _report = await _service.GetReportAsync(
                    settings.ShopId!, settings.Keystring!, settings.AccessToken!, from, to, ct);
                _lblMode.Text = "🟢 Canlı API";
                _lblMode.ForeColor = UiStyle.SuccessColor;
            }
            else
            {
                _isMockMode = true;
                await Task.Delay(400, ct); // animasyon etkisi
                _report = FinancialReportService.GenerateMockReport(from, to);
                _lblMode.Text = "🎮 Demo Modu";
                _lblMode.ForeColor = UiStyle.WarningColor;
            }

            if (ct.IsCancellationRequested) return;

            UpdateKpis();
            UpdateCharts();
            UpdateGrid();
            SetStatus($"{_report.Entries.Count} kayıt | {_report.PeriodStart:dd.MM.yyyy} — {_report.PeriodEnd:dd.MM.yyyy}", UiStyle.TextMuted);
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            SetStatus($"❌ Hata: {ex.Message}", UiStyle.DangerColor);
        }
    }

    // ── KPI Güncellemesi ───────────────────────────────────────────────────────

    private void UpdateKpis()
    {
        string sym = _report.Currency == "USD" ? "$" :
                     _report.Currency == "EUR" ? "€" :
                     _report.Currency == "TRY" ? "₺" : _report.Currency + " ";

        _kpiGross.Text   = $"{sym}{_report.TotalGross:N2}";
        _kpiNet.Text     = $"{sym}{_report.TotalNet:N2}";
        _kpiFees.Text    = $"{sym}{_report.TotalFees:N2} ({_report.FeeRatePct}%)";
        _kpiAds.Text     = $"{sym}{_report.TotalAdFees:N2} ({_report.AdSpendPct}%)";
        _kpiRefunds.Text = $"{sym}{_report.TotalRefunds:N2} ({_report.RefundRatePct}%)";
        _kpiOrders.Text  = _report.TransactionCount.ToString("N0");
        _kpiAov.Text     = $"{sym}{_report.AverageOrderValue:N2}";

        // Net gelirin rengini dinamik ayarla
        _kpiNet.ForeColor = _report.TotalNet >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;
    }

    // ── Grafik Güncellemesi ────────────────────────────────────────────────────

    private void UpdateCharts()
    {
        var months = _report.Monthly;
        if (months.Count == 0) return;

        var labels = months.Select(m => m.MonthName).ToArray();
        var textPaint = new SolidColorPaint(new SKColor(148, 163, 184)); // TextMuted

        // — Bar Chart (Aylık Gelir/Gider) ——————————————————————————————————————
        var salesVals    = months.Select(m => (double)m.GrossSales).ToArray();
        var netVals      = months.Select(m => (double)m.NetIncome).ToArray();
        var feeVals      = months.Select(m => (double)m.TotalFees).ToArray();
        var refundVals   = months.Select(m => (double)m.Refunds).ToArray();

        _barChart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Brüt Satış",
                Values = salesVals,
                Fill = new SolidColorPaint(new SKColor(16, 185, 129, 200)),  // Emerald
                Rx = 4, Ry = 4,
            },
            new ColumnSeries<double>
            {
                Name = "Net Gelir",
                Values = netVals,
                Fill = new SolidColorPaint(new SKColor(99, 102, 241, 220)),  // Indigo
                Rx = 4, Ry = 4,
            },
            new ColumnSeries<double>
            {
                Name = "Ücretler",
                Values = feeVals,
                Fill = new SolidColorPaint(new SKColor(245, 158, 11, 200)),  // Amber
                Rx = 4, Ry = 4,
            },
            new ColumnSeries<double>
            {
                Name = "İadeler",
                Values = refundVals,
                Fill = new SolidColorPaint(new SKColor(239, 68, 68, 200)),   // Red
                Rx = 4, Ry = 4,
            },
        };
        _barChart.XAxes = new[] { new Axis
        {
            Labels = labels,
            TextSize = 11,
            LabelsPaint = textPaint,
        }};
        _barChart.YAxes = new[] { new Axis
        {
            TextSize = 11,
            LabelsPaint = textPaint,
            Labeler = v => $"${v:N0}",
        }};

        // — Line Chart (Net Gelir Trendi) ——————————————————————————————————————
        _lineChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "Net Gelir",
                Values = netVals,
                Fill = new LinearGradientPaint(
                    new SKColor(99, 102, 241, 60),
                    new SKColor(99, 102, 241, 0)),
                Stroke = new SolidColorPaint(new SKColor(99, 102, 241), 3),
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(new SKColor(99, 102, 241)),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                LineSmoothness = 0.5,
            },
            new LineSeries<double>
            {
                Name = "Brüt Satış",
                Values = salesVals,
                Fill = null,
                Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 2) { PathEffect = new DashEffect(new float[] { 6, 3 }) },
                GeometrySize = 0,
                LineSmoothness = 0.5,
            },
        };
        _lineChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint } };
        _lineChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"${v:N0}" } };

        // — Pie Chart (Gelir Kaynakları dağılımı) ——————————————————————————————
        decimal totalCosts = _report.TotalFees + _report.TotalRefunds + _report.TotalAdFees;
        decimal netPortion = Math.Max(0, _report.TotalNet);

        _pieChart.Series = new ISeries[]
        {
            new PieSeries<double>
            {
                Name = "✅ Net Gelir",
                Values = new[] { (double)netPortion },
                Fill = new SolidColorPaint(new SKColor(16, 185, 129)),
                DataLabelsSize = 13,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue:N0}$",
            },
            new PieSeries<double>
            {
                Name = "📋 Etsy Ücretleri",
                Values = new[] { (double)_report.TotalFees },
                Fill = new SolidColorPaint(new SKColor(245, 158, 11)),
                DataLabelsSize = 11,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
            },
            new PieSeries<double>
            {
                Name = "↩️ İadeler",
                Values = new[] { (double)_report.TotalRefunds },
                Fill = new SolidColorPaint(new SKColor(239, 68, 68)),
                DataLabelsSize = 11,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
            },
            new PieSeries<double>
            {
                Name = "📢 Reklam",
                Values = new[] { (double)_report.TotalAdFees },
                Fill = new SolidColorPaint(new SKColor(139, 92, 246)),
                DataLabelsSize = 11,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
            },
        };
    }

    // ── Grid Güncellemesi ──────────────────────────────────────────────────────

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

        // Koşullu renklendirme
        _gridEntries.CellFormatting += (_, args) =>
        {
            if (args.RowIndex < 0 || args.RowIndex >= rows.Count) return;
            var row = rows[args.RowIndex];
            if (row.IsCredit)
                _gridEntries.Rows[args.RowIndex].DefaultCellStyle.ForeColor = UiStyle.SuccessColor;
            else if (row.IsDebit)
                _gridEntries.Rows[args.RowIndex].DefaultCellStyle.ForeColor = UiStyle.DangerColor;
        };
    }

    // ── Tarih Aralığı Değişimi ────────────────────────────────────────────────

    private void OnDateRangeChanged(object? sender, EventArgs e)
    {
        var today = DateTime.Today;
        switch (_cboDateRange.SelectedIndex)
        {
            case 0: // Son 7 Gün
                _dtpFrom.Value = today.AddDays(-7); _dtpTo.Value = today; break;
            case 1: // Son 30 Gün
                _dtpFrom.Value = today.AddDays(-30); _dtpTo.Value = today; break;
            case 2: // Son 90 Gün
                _dtpFrom.Value = today.AddDays(-90); _dtpTo.Value = today; break;
            case 3: // Bu Ay
                _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                _dtpTo.Value = today; break;
            case 4: // Geçen Ay
                var lastMonth = today.AddMonths(-1);
                _dtpFrom.Value = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                _dtpTo.Value   = new DateTime(lastMonth.Year, lastMonth.Month,
                    DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)); break;
            case 5: // Bu Yıl
                _dtpFrom.Value = new DateTime(today.Year, 1, 1); _dtpTo.Value = today; break;
            case 6: // Özel Aralık — tarihçi elle girer
                return;
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
            MessageBox.Show($"Excel raporu kaydedildi:\n{dlg.FileName}", "✅ Başarılı",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel dışa aktarma hatası: {ex.Message}", "❌ Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportCsv(object? sender, EventArgs e)
    {
        if (_report.Entries.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak veri yok.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Title = "CSV Raporu Kaydet",
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"Etsy_Defter_{_report.PeriodStart:yyyyMMdd}_{_report.PeriodEnd:yyyyMMdd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            FinancialReportExporter.ExportToCsv(_report, dlg.FileName);
            MessageBox.Show($"CSV raporu kaydedildi:\n{dlg.FileName}", "✅ Başarılı",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSV dışa aktarma hatası: {ex.Message}", "❌ Hata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    // ── Grid Satır Modeli ──────────────────────────────────────────────────────

    private sealed class LedgerRow(LedgerEntry e)
    {
        public string Date         => e.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
        public string TypeLabel    => e.Type switch
        {
            "sale"               => "💰 Satış",
            "refund"             => "↩️ İade",
            "listing_fee"        => "📋 Liste Ücreti",
            "transaction_fee"    => "🔄 İşlem Ücreti",
            "ad_fee"             => "📢 Reklam",
            "offsite_ads"        => "📢 Dış Reklam",
            "shipping"           => "🚚 Kargo",
            "payment_processing" => "💳 Ödeme İşlem",
            _                    => e.Type
        };
        public string AmountStr    => $"{(e.Type == "sale" ? "+" : "")}{e.Amount:N2} {e.Currency}";
        public string NetAmountStr => $"{e.NetAmount:+#,##0.00;-#,##0.00} {e.Currency}";
        public string Currency     => e.Currency;
        public string Description  => e.Description;
        public bool   IsCredit     => e.Type is "sale" or "shipping";
        public bool   IsDebit      => e.Type is "refund" or "listing_fee"
                                        or "transaction_fee" or "ad_fee"
                                        or "payment_processing";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosed(e);
    }
}
