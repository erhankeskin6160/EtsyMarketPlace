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
    
    private readonly SimilarProductsWinForms.Controls.AnimatedToolTipForm _customToolTipForm = new();
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _hoverCheckTimer = new() { Interval = 50 };
    private Control? _hoveredCard = null;
    private ToolTipDataPayload? _salesTooltipPayload = null;
    private ToolTipDataPayload? _refundsTooltipPayload = null;
    private ToolTipDataPayload? _costsTooltipPayload = null;

    // ── UI: Filters & Currency ────────────────────────────────────────────────
    private readonly ComboBox _cboDateRange     = new();
    private readonly DateTimePicker _dtpFrom    = new();
    private readonly DateTimePicker _dtpTo      = new();
    private readonly CheckBox _chkUseTry        = new();
    private readonly NumericUpDown _numExchangeRate = new();
    private readonly Label _lblMode             = new();

    // ── UI: Charts & Grids ─────────────────────────────────────────────────────
    private CartesianChart  _barChart      = null!;
    private CartesianChart  _lineChart     = null!;
    private CartesianChart  _ratioChart    = null!;
    private CartesianChart  _forecastChart = null!;
    private PieChart        _pieChart      = null!;
    private readonly TabControl _tabMain   = new();

    private Label _lblForecastGross  = null!;
    private Label _lblForecastProfit = null!;
    private Label _lblForecastOrders = null!;
    private Label _lblForecastStock  = null!;
    private FlowLayoutPanel _pnlAiRecommendations = null!;
    private readonly Label _lblForecastAiBadge = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.FromArgb(30, 41, 59),
        Padding = new Padding(6, 3, 6, 3),
        Cursor = Cursors.Hand,
        Anchor = AnchorStyles.Right,
        Margin = new Padding(0, 2, 6, 0)
    };
    private readonly Button _btnRefreshForecastAi = new()
    {
        Text = "✨ Canlı CFO Analizi Al",
        AutoSize = true,
        Height = 26,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(99, 102, 241),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 1, 0, 0)
    };

    private readonly DataGridView _gridPeriod  = new();
    private readonly DataGridView _gridEntries = new();
    private readonly DataGridView _gridOrders  = new();
    private readonly ComboBox _cboPeriodType   = new();

    private string _periodSortColumn = "Period";
    private bool _periodSortAscending = true;

    private string _orderSortColumn = "ODate";
    private bool _orderSortAscending = false;

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

        _ratioChart = new CartesianChart
        {
            Dock = DockStyle.Fill,
            Series = Array.Empty<ISeries>(),
            XAxes = new[] { new Axis { Labels = Array.Empty<string>(), TextSize = 11 } },
            YAxes = new[] { new Axis { TextSize = 11, Labeler = v => $"%{v:N0}" } },
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            BackColor = Color.Transparent,
        };

        _forecastChart = new CartesianChart
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
        _statusLabel.Text = $"Son Güncelleme: {DateTime.Now:HH:mm:ss} | API Bağlantısı Başarılı";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        p.Controls.Add(_statusLabel, 1, 0);

        return p;
    }

    private void HoverCheckTimer_Tick(object? sender, EventArgs e)
    {
        if (_hoveredCard == null)
        {
            _customToolTipForm.HideTooltip();
            _hoverCheckTimer.Stop();
            return;
        }

        Point mousePos = Cursor.Position;
        Rectangle cardBounds = _hoveredCard.RectangleToScreen(_hoveredCard.ClientRectangle);

        // If mouse left the card, hide tooltip immediately
        if (!cardBounds.Contains(mousePos))
        {
            _customToolTipForm.HideTooltip();
            _hoveredCard = null;
            _hoverCheckTimer.Stop();
        }
    }

    private void AttachAnimatedHover(Control rootCard, Control currentControl, Action<SimilarProductsWinForms.Controls.AnimatedToolTipForm, Point> showAction)
    {
        currentControl.MouseEnter += (s, e) => 
        {
            if (_hoveredCard == rootCard) return; // Already hovering
            _hoveredCard = rootCard;

            // Calculate position: centered below the card
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

    // — Ana Layout —————————————————————————————————————————————————————————————

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
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Excel
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); // Telegram
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
        _chkUseTry.Text = "🇹🇷 Oto Kur:";
        _chkUseTry.Dock = DockStyle.Fill;
        _chkUseTry.Checked = true;
        _chkUseTry.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _chkUseTry.ForeColor = UiStyle.TextDark;
        _chkUseTry.CheckedChanged += (_, _) => { UpdateKpis(); UpdateForecastView(); UpdateCharts(); };
        bar.Controls.Add(_chkUseTry, 6, 0);

        _numExchangeRate.Dock = DockStyle.Fill;
        _numExchangeRate.DecimalPlaces = 2;
        _numExchangeRate.Maximum = 500;
        _numExchangeRate.Value = 36.50m;
        _numExchangeRate.Enabled = false; // Kullanıcı artık manuel giremez
        _numExchangeRate.ValueChanged += (_, _) => { UpdateKpis(); UpdateForecastView(); UpdateCharts(); };
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

        // Telegram Butonu
        var btnTelegram = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📱 Telegram'a Gönder",
            NormalColor = Color.FromArgb(14, 165, 233), // Sky Blue #0EA5E9
            HoverColor = Color.FromArgb(2, 132, 199),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 4, 0),
        };
        btnTelegram.Click += async (_, _) =>
        {
            btnTelegram.Enabled = false;
            try
            {
                var settings = NotificationSettingsStore.Load();
                if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.TelegramChatId))
                {
                    MessageBox.Show(this, "Telegram bildirimleri kapalı veya Bot Token / Chat ID girilmemiş.\nLütfen Bildirim Ayarları menüsünden Telegram'ı yapılandırın.", "📱 Telegram Raporu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                decimal rate = _numExchangeRate.Value > 0 ? _numExchangeRate.Value : 36.50m;
                string msgHtml = DailyFinancialReportNotificationService.FormatTelegramReportHtml(_report, rate, isManualTrigger: true, settings.IncludeAiSummaryInNightReport);

                var (success, msg) = await NotificationService.SendTelegramMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, msgHtml);
                MessageBox.Show(this, msg, "📱 Telegram Finans Raporu", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Gönderim hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTelegram.Enabled = true;
            }
        };
        bar.Controls.Add(btnTelegram, 9, 0);

        // Mod Etiketi
        _lblMode.Dock = DockStyle.Fill;
        _lblMode.Text = "🎮 Demo Modu";
        _lblMode.TextAlign = ContentAlignment.MiddleRight;
        _lblMode.ForeColor = UiStyle.WarningColor;
        _lblMode.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        bar.Controls.Add(_lblMode, 11, 0);

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

        // Attach ToolTips after controls are added
        strip.Layout += (s, e) => 
        {
            var grossCard = strip.GetControlFromPosition(0, 0);
            if (grossCard != null && grossCard.Tag == null)
            {
                grossCard.Tag = "attached";
                AttachAnimatedHover(grossCard, grossCard, (tt, pt) => 
                {
                    if (_salesTooltipPayload != null)
                        tt.ShowStructuredTooltip(_salesTooltipPayload, pt, 2000);
                });
            }

            var refundsCard = strip.GetControlFromPosition(4, 0);
            if (refundsCard != null && refundsCard.Tag == null)
            {
                refundsCard.Tag = "attached";
                AttachAnimatedHover(refundsCard, refundsCard, (tt, pt) => 
                {
                    if (_refundsTooltipPayload != null)
                        tt.ShowStructuredTooltip(_refundsTooltipPayload, pt, 2000);
                });
            }

            var costsCard = strip.GetControlFromPosition(6, 0);
            if (costsCard != null && costsCard.Tag == null)
            {
                costsCard.Tag = "attached";
                AttachAnimatedHover(costsCard, costsCard, (tt, pt) => 
                {
                    if (_costsTooltipPayload != null)
                        tt.ShowStructuredTooltip(_costsTooltipPayload, pt, 2000);
                });
            }
        };

        return strip;
    }

    private void BuildKpiCard(TableLayoutPanel parent, int col, string title, Label valueLabel, Color accentColor)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 0, 3, 0),
            Padding = new Padding(8, 6, 8, 6),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Cursor = Cursors.Hand,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 7F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
        };
        layout.Controls.Add(titleLabel, 0, 0);

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "—";
        valueLabel.Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold);
        valueLabel.ForeColor = accentColor;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Cursor = Cursors.Hand;
        layout.Controls.Add(valueLabel, 0, 1);

        var accent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = accentColor,
            Height = 3,
            Cursor = Cursors.Hand,
        };
        layout.Controls.Add(accent, 0, 2);

        // Click-to-copy handler
        Action copyAction = () =>
        {
            string txt = valueLabel.Text;
            if (!string.IsNullOrWhiteSpace(txt) && txt != "—")
            {
                Clipboard.SetText(txt);
                SetStatus($"📋 Kopyalandı: {title} ➔ {txt}", UiStyle.SuccessColor);
            }
        };

        card.Click += (s, e) => copyAction();
        layout.Click += (s, e) => copyAction();
        titleLabel.Click += (s, e) => copyAction();
        valueLabel.Click += (s, e) => copyAction();
        accent.Click += (s, e) => copyAction();

        var cms = new ContextMenuStrip();
        cms.Items.Add("📋 Değeri Kopyala", null, (s, e) => copyAction());
        card.ContextMenuStrip = cms;
        layout.ContextMenuStrip = cms;
        titleLabel.ContextMenuStrip = cms;
        valueLabel.ContextMenuStrip = cms;

        _toolTip.SetToolTip(card, $"Tıklayarak '{title}' tutarını panoya kopyalayın");
        _toolTip.SetToolTip(valueLabel, $"Tıklayarak '{title}' tutarını panoya kopyalayın");

        card.Controls.Add(layout);
        parent.Controls.Add(card, col, 0);
    }

    // — Ana Sekme Yapısı —————————————————————————————————————————————————————

    private Control BuildContent()
    {
        _tabMain.Dock = DockStyle.Fill;
        _tabMain.Font = new Font("Segoe UI Semibold", 9.5F);

        var tabCharts   = new TabPage("📊 Grafik Analizleri")       { Padding = new Padding(6) };
        var tabForecast = new TabPage("🔮 AI Kâr & Ciro Tahmini")   { Padding = new Padding(6) };
        var tabPeriod   = new TabPage("📅 Dönemsel Muhasebe")       { Padding = new Padding(6) };
        var tabOrders   = new TabPage("📦 Siparişler & Net Kâr")   { Padding = new Padding(6) };
        var tabGrid     = new TabPage("📋 Ödeme Defteri Kayıtları") { Padding = new Padding(6) };

        tabCharts.Controls.Add(BuildChartPanel());
        tabForecast.Controls.Add(BuildForecastPanel());
        tabPeriod.Controls.Add(BuildPeriodPanel());
        tabOrders.Controls.Add(BuildOrdersPanel());
        tabGrid.Controls.Add(BuildGridPanel());

        _tabMain.TabPages.Add(tabCharts);
        _tabMain.TabPages.Add(tabForecast);
        _tabMain.TabPages.Add(tabPeriod);
        _tabMain.TabPages.Add(tabOrders);
        _tabMain.TabPages.Add(tabGrid);

        return _tabMain;
    }

    private Control BuildForecastPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. Üst Satır: 4 Adet Projeksiyon Kartı
        var kpiGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 4,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
        };
        for (int i = 0; i < 4; i++)
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        _lblForecastGross  = CreateForecastKpiCard(kpiGrid, 0, "🔮 GELECEK AY BEKLENEN CİRO", "$0 / ₺0", "Min: $0 — Max: $0", UiStyle.PrimaryColor);
        _lblForecastProfit = CreateForecastKpiCard(kpiGrid, 1, "💵 BEKLENEN GERÇEK NET KÂR", "$0 / ₺0", "Beklenen Kâr Marjı: %0", UiStyle.SuccessColor);
        _lblForecastOrders = CreateForecastKpiCard(kpiGrid, 2, "📦 TAHMİNİ SİPARİŞ & BÜYÜME", "0 Sipariş", "Aylık Büyüme: %0", Color.FromArgb(56, 189, 248));
        _lblForecastStock  = CreateForecastKpiCard(kpiGrid, 3, "🖨️ STOK & HAMMADDE İHTİYACI", "0 kg Filament", "0 Adet Kargo Kutusu", UiStyle.WarningColor);

        root.Controls.Add(kpiGrid, 0, 0);

        // 2. Alt Satır: Sol Grafik, Sağ AI Öneri Paneli
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2,
            BackColor = Color.Transparent,
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));

        // Sol: LiveCharts2 Projeksiyon Grafiği
        var chartCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Margin = new Padding(0, 0, 6, 0)
        };
        var chartLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        chartLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var chartTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "📈 Gelecek 60 Gün Kâr & Ciro Projeksiyon Eğrisi (Zaman Serisi & Sezonluk Trend)",
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        chartLayout.Controls.Add(chartTitle, 0, 0);
        chartLayout.Controls.Add(_forecastChart, 0, 1);
        chartCard.Controls.Add(chartLayout);
        split.Controls.Add(chartCard, 0, 0);

        // Sağ: AI Finansal Tavsiyeler & Strateji Paneli
        var adviceCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Margin = new Padding(6, 0, 0, 0)
        };
        var adviceLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        adviceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        adviceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var adviceHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        adviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        adviceHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var adviceTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "🤖 Yapay Zekâ CFO & Finansal İçgörüler",
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = Color.FromArgb(167, 139, 250), // Vibrant Lavender/Purple
            TextAlign = ContentAlignment.MiddleLeft
        };
        adviceHeader.Controls.Add(adviceTitle, 0, 0);

        var rightPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _btnRefreshForecastAi.FlatAppearance.BorderSize = 0;
        _btnRefreshForecastAi.Click += async (_, _) => await RunLiveAiCfoAnalysisAsync();
        rightPanel.Controls.Add(_btnRefreshForecastAi);

        _lblForecastAiBadge.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
            UpdateForecastAiBadge();
        };
        UpdateForecastAiBadge();
        rightPanel.Controls.Add(_lblForecastAiBadge);

        adviceHeader.Controls.Add(rightPanel, 1, 0);
        adviceLayout.Controls.Add(adviceHeader, 0, 0);

        _pnlAiRecommendations = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 4, 0)
        };
        _pnlAiRecommendations.SizeChanged += (_, _) =>
        {
            int targetWidth = Math.Max(260, _pnlAiRecommendations.ClientSize.Width - 12);
            int contentWidth = Math.Max(220, targetWidth - 28);
            var fTitle = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            var fBody = new Font("Segoe UI", 9F);

            foreach (Control c in _pnlAiRecommendations.Controls)
            {
                if (c is ModernCardPanel card && card.Controls.Count >= 2)
                {
                    if (card.Controls[0] is Label lblT && card.Controls[1] is Label lblB)
                    {
                        var tSize = TextRenderer.MeasureText(lblT.Text, fTitle, new Size(contentWidth, 0), TextFormatFlags.WordBreak);
                        var bSize = TextRenderer.MeasureText(lblB.Text, fBody, new Size(contentWidth, 0), TextFormatFlags.WordBreak);
                        card.Width = targetWidth;
                        card.Height = tSize.Height + bSize.Height + 28;
                        lblT.Location = new Point(14, 8);
                        lblT.Size = new Size(contentWidth, tSize.Height + 2);
                        lblB.Location = new Point(14, 12 + tSize.Height);
                        lblB.Size = new Size(contentWidth, bSize.Height + 6);
                    }
                }
            }
        };

        adviceLayout.Controls.Add(_pnlAiRecommendations, 0, 1);
        adviceCard.Controls.Add(adviceLayout);
        split.Controls.Add(adviceCard, 1, 0);

        root.Controls.Add(split, 0, 1);
        return root;
    }

    private Label CreateForecastKpiCard(TableLayoutPanel parent, int col, string title, string val, string sub, Color accentColor)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Margin = new Padding(col == 0 ? 0 : 4, 0, col == 3 ? 0 : 4, 0),
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(titleLabel, 0, 0);

        var valLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = val,
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = accentColor,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(valLabel, 0, 1);

        var subLabel = new Label
        {
            Name = "lblSub",
            Dock = DockStyle.Fill,
            Text = sub,
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(subLabel, 0, 2);

        card.Controls.Add(layout);
        parent.Controls.Add(card, col, 0);
        return valLabel;
    }

    private Control BuildChartPanel()
    {
        var tabCharts = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F) };

        var tabBar   = new TabPage("📊 Aylık Gelir & Kâr (Bar)") { Padding = new Padding(4) };
        var tabLine  = new TabPage("📈 Gerçek Net Kâr Trendi")    { Padding = new Padding(4) };
        var tabRatio = new TabPage("🎯 Kâr Marjı & Oranlar (%)")  { Padding = new Padding(4) };
        var tabPie   = new TabPage("🥧 Maliyet & Gider Dağılımı") { Padding = new Padding(4) };

        tabBar.Controls.Add(_barChart);
        tabLine.Controls.Add(_lineChart);
        tabRatio.Controls.Add(_ratioChart);
        tabPie.Controls.Add(_pieChart);

        tabCharts.TabPages.Add(tabBar);
        tabCharts.TabPages.Add(tabLine);
        tabCharts.TabPages.Add(tabRatio);
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
        _gridPeriod.ColumnHeaderMouseClick += OnPeriodGridColumnHeaderClick;
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

    private Control BuildOrdersPanel()
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Özet bilgi etiketi (üstte)
        var summaryLabel = new Label
        {
            Name = "lblOrderSummary",
            Dock = DockStyle.Fill,
            Text = "📦 Sipariş listesi yükleniyor...",
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(summaryLabel, 0, 0);

        // Grid yapılandır
        UiStyle.ConfigureBaseGrid(_gridOrders);
        _gridOrders.Dock = DockStyle.Fill;
        _gridOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridOrders.MultiSelect = false;
        _gridOrders.CellDoubleClick += OnOrderGridDoubleClick;
        _gridOrders.ColumnHeaderMouseClick += OnOrdersGridColumnHeaderClick;
        var ctxMenu = new ContextMenuStrip();
        
        var mnuDetails = new ToolStripMenuItem("📄 Sipariş Detaylarını Gör", null, (s, e) =>
        {
            if (_gridOrders.SelectedRows.Count == 0) return;
            var row = _gridOrders.SelectedRows[0];
            if (row.Tag is OrderFinancialSummary order)
            {
                using var form = new OrderDetailsForm(order);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _ = LoadReportAsync();
                }
            }
        });

        var mnuOpenInvoice = new ToolStripMenuItem("📄 Kargo Faturasını Aç / Önizle", null, (s, e) =>
        {
            if (_gridOrders.SelectedRows.Count == 0) return;
            var row = _gridOrders.SelectedRows[0];
            if (row.Tag is OrderFinancialSummary order)
            {
                if (order.HasInvoice)
                {
                    InvoiceStorageService.OpenInvoice(order.InvoiceFilePath);
                }
                else
                {
                    MessageBox.Show(this, "Bu ürün/sipariş için yüklenmiş bir kargo faturası bulunamadı.\n'Hızlı Maliyet Düzenle' seçeneğiyle fatura (PDF/Resim) ekleyebilirsiniz.", "Fatura Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        });
        
        var mnuQuickCost = new ToolStripMenuItem("💰 Hızlı Maliyet & Fatura Düzenle", null, async (s, e) =>
        {
            if (_gridOrders.SelectedRows.Count == 0) return;
            var row = _gridOrders.SelectedRows[0];
            if (row.Tag is OrderFinancialSummary order)
            {
                var repo = new SqliteProductCostRepository();
                var currentCost = await repo.GetByIdAsync(order.ListingId.ToString());
                
                using var popup = new CostDetailsPopupForm(currentCost);
                if (popup.ShowDialog(this) == DialogResult.OK)
                {
                    var newEntry = new ProductCostEntry(
                        order.ListingId.ToString(), 
                        order.ProductTitle, 
                        popup.UnitCost, 
                        popup.UnitShippingCost, 
                        popup.UnitPackagingCost, 
                        DateTimeOffset.UtcNow,
                        popup.InvoiceFilePath);
                        
                    await repo.SaveAsync(newEntry);
                    _ = LoadReportAsync();
                }
            }
        });

        var mnuCopyCell = new ToolStripMenuItem("📋 Seçili Hücreyi Kopyala", null, (s, e) =>
        {
            if (_gridOrders.CurrentCell?.Value != null)
            {
                string val = _gridOrders.CurrentCell.Value.ToString() ?? "";
                Clipboard.SetText(val);
                SetStatus($"📋 Kopyalandı: {val}", UiStyle.SuccessColor);
            }
        });

        var mnuCopyRow = new ToolStripMenuItem("📑 Tüm Satırı Kopyala", null, (s, e) =>
        {
            if (_gridOrders.SelectedRows.Count > 0)
            {
                var row = _gridOrders.SelectedRows[0];
                var cells = new List<string>();
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cells.Add(cell.Value?.ToString() ?? "");
                }
                string rowText = string.Join(" | ", cells);
                Clipboard.SetText(rowText);
                SetStatus("📑 Satır panoya kopyalandı.", UiStyle.SuccessColor);
            }
        });
        
        ctxMenu.Items.Add(mnuDetails);
        ctxMenu.Items.Add(mnuOpenInvoice);
        ctxMenu.Items.Add(mnuQuickCost);
        ctxMenu.Items.Add(new ToolStripSeparator());
        ctxMenu.Items.Add(mnuCopyCell);
        ctxMenu.Items.Add(mnuCopyRow);
        _gridOrders.ContextMenuStrip = ctxMenu;
        _gridOrders.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

        _gridOrders.CellMouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _gridOrders.ClearSelection();
                _gridOrders.Rows[e.RowIndex].Selected = true;
                if (e.ColumnIndex >= 0)
                {
                    _gridOrders.CurrentCell = _gridOrders.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            }
        };

        root.Controls.Add(_gridOrders, 0, 1);

        card.Controls.Add(root);
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

        _statusLabel.Text = "Veriler yükleniyor... (Kur bilgileri güncelleniyor)";
        _statusLabel.ForeColor = UiStyle.PrimaryColor;
        Application.DoEvents();

        try
        {
            // O günkü (güncel) kuru çek ve kutuya otomatik yaz
            decimal todayRate = await new ExchangeRateService().GetHistoricalRateAsync(DateTime.UtcNow);
            _numExchangeRate.Value = todayRate;
        }
        catch { /* ignored, fallback is used */ }

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
            UpdateForecastView();
            UpdateGrid();
            UpdatePeriodGrid();
            UpdateOrdersGrid();
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

        string FormatKpi(decimal usdValue, decimal tryValue)
        {
            decimal val = showTry ? tryValue : usdValue;
            string prefix = showTry ? "₺" : "$";
            return val < 0 ? $"-{prefix}{Math.Abs(val):N2}" : $"{prefix}{val:N2}";
        }

        _kpiGross.Text        = FormatKpi(_report.TotalGross, _report.DailySummaries.Sum(d => d.GrossSales * d.AverageExchangeRate));
        _kpiFees.Text         = FormatKpi(_report.TotalFees, _report.DailySummaries.Sum(d => d.EtsyFees * d.AverageExchangeRate));
        _kpiInnerAds.Text     = FormatKpi(_report.TotalInnerAdFees, _report.DailySummaries.Sum(d => d.InnerAdFees * d.AverageExchangeRate));
        _kpiOffsiteAds.Text   = FormatKpi(_report.TotalOffsiteAdFees, _report.DailySummaries.Sum(d => d.OffsiteAdFees * d.AverageExchangeRate));
        _kpiRefunds.Text      = FormatKpi(_report.TotalRefunds, _report.DailySummaries.Sum(d => d.Refunds * d.AverageExchangeRate));
        _kpiNet.Text          = FormatKpi(_report.TotalNet, _report.DailySummaries.Sum(d => d.EtsyNetRevenue * d.AverageExchangeRate));
        _kpiProductCosts.Text = FormatKpi(-_report.TotalProductCosts, -_report.DailySummaries.Sum(d => d.ProductCosts * d.AverageExchangeRate));

        decimal profitUSD = _report.RealNetProfitUSD;
        decimal profitTRY = _report.DailySummaries.Sum(d => d.RealNetProfitTRY);
        _kpiRealProfit.Text   = FormatKpi(profitUSD, profitTRY);
        _kpiRealProfit.ForeColor = profitUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        if (_report.IsFallbackMode)
        {
            _kpiDeposits.Text = "YETKİ YOK";
            _kpiDeposits.ForeColor = UiStyle.DangerColor;
            _kpiDeposits.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        }
        else
        {
            _kpiDeposits.Text = FormatKpi(_report.TotalDeposits, _report.DailySummaries.Sum(d => d.Deposits * d.AverageExchangeRate));
            _kpiDeposits.ForeColor = UiStyle.AccentColor;
            _kpiDeposits.Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold);
        }

        _kpiNet.ForeColor = _report.TotalNet >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        UpdateRefundsToolTip(showTry);
        UpdateSalesToolTip(showTry);
        UpdateCostsToolTip(showTry);
    }

    private void UpdateRefundsToolTip(bool showTry)
    {
        if (_report.IsFallbackMode)
        {
            _refundsTooltipPayload = null;
            return;
        }

        var refundEntries = _report.Entries.Where(e => e.Type == "refund").ToList();
        string cur = showTry ? "₺" : "$";
        decimal totalRefundAmt = showTry ? _report.DailySummaries.Sum(d => d.Refunds * d.AverageExchangeRate) : _report.TotalRefunds;
        decimal totalGrossAmt = showTry ? _report.TotalGrossTRY : _report.TotalGross;
        double refundRate = totalGrossAmt > 0 ? (double)(Math.Abs(totalRefundAmt) / totalGrossAmt * 100) : 0;

        var kpiCards = new List<ToolTipKpiCard>
        {
            new("↩️ Toplam İade Tutarı", $"{cur}{Math.Abs(totalRefundAmt):N2}", $"Cironun %{refundRate:N1}'i", Color.FromArgb(239, 68, 68)),
            new("🔢 İade İşlem Adedi", $"{refundEntries.Count} Adet", null, Color.FromArgb(249, 115, 22)),
            new("📊 İade Oranı", $"%{refundRate:N1}", null, Color.FromArgb(245, 158, 11))
        };

        var rows = new List<ToolTipTableRow>();
        foreach (var r in refundEntries.Take(15))
        {
            decimal displayAmt = showTry ? Math.Abs(r.Amount * r.ExchangeRate) : Math.Abs(r.Amount);
            string dateStr = r.CreatedAt.ToString("dd.MM.yy");
            string orderNo = $"İşlem #{r.EntryId}";
            string title = string.IsNullOrWhiteSpace(r.Description) || r.Description == "refund_gross" ? "İade / Geri Ödeme Kesintisi" : r.Description;

            // 1. Check if description has 9+ digits receipt id
            var match = System.Text.RegularExpressions.Regex.Match(r.Description ?? "", @"\d{9,}");
            if (match.Success && long.TryParse(match.Value, out long receiptId))
            {
                orderNo = $"#{receiptId}";
                var order = _report.OrderSummaries.FirstOrDefault(o => o.ReceiptId == receiptId);
                if (order != null)
                {
                    title = order.ProductTitle;
                }
            }
            else
            {
                // 2. Proximity match with orders by date and amount
                var matchedOrder = _report.OrderSummaries.FirstOrDefault(o => 
                    Math.Abs((o.OrderDate - r.CreatedAt).TotalDays) <= 14 &&
                    (Math.Abs(o.GrandTotal - Math.Abs(r.Amount)) < 0.1m || Math.Abs(o.GrandTotal - Math.Abs(r.NetAmount)) < 0.1m));
                if (matchedOrder != null)
                {
                    orderNo = $"#{matchedOrder.ReceiptId}";
                    title = matchedOrder.ProductTitle;
                }
            }

            rows.Add(new ToolTipTableRow(dateStr, orderNo, "1 Ad", $"-{cur}{displayAmt:N2}", false, "", title));
        }

        _refundsTooltipPayload = new ToolTipDataPayload(
            "🔴 İade ve Geri Ödeme Analizi",
            "Dönem içinde müşterilere yapılan para iadeleri ve kesinti detayları",
            kpiCards,
            new[] { "Tarih", "Sipariş No", "Adet", "İade Tutarı", "Durum", "İade Açıklaması / Ürün" },
            new[] { 0.14f, 0.16f, 0.08f, 0.16f, 0.10f, 0.36f },
            rows,
            refundEntries.Count > 15 ? $"ℹ️ ... ve {refundEntries.Count - 15} adet iade daha listelenmedi." : null
        );
    }

    private void UpdateSalesToolTip(bool showTry)
    {
        if (_report.IsFallbackMode)
        {
            _salesTooltipPayload = null;
            return;
        }

        var salesEntries = _report.Entries.Where(e => e.Type == "sale").ToList();
        string cur = showTry ? "₺" : "$";
        decimal totalGross = showTry ? _report.TotalGrossTRY : _report.TotalGross;
        decimal totalNet = showTry ? _report.DailySummaries.Sum(d => d.EtsyNetRevenue * d.AverageExchangeRate) : _report.TotalNet;
        decimal aov = salesEntries.Count > 0 ? (totalGross / salesEntries.Count) : 0;

        var kpiCards = new List<ToolTipKpiCard>
        {
            new("💰 Toplam Brüt Satış", $"{cur}{totalGross:N2}", $"{salesEntries.Count} İşlem", Color.FromArgb(59, 130, 246)),
            new("✅ Etsy Net Gelir", $"{cur}{totalNet:N2}", null, Color.FromArgb(16, 185, 129)),
            new("🏷️ Ortalama Sepet (AOV)", $"{cur}{aov:N2}", null, Color.FromArgb(99, 102, 241))
        };

        var rows = new List<ToolTipTableRow>();
        foreach (var r in salesEntries.Take(15))
        {
            decimal displayAmt = showTry ? Math.Abs(r.Amount * r.ExchangeRate) : Math.Abs(r.Amount);
            string dateStr = r.CreatedAt.ToString("dd.MM.yy");
            string orderNo = $"İşlem #{r.EntryId}";
            string title = string.IsNullOrWhiteSpace(r.Description) || r.Description == "payment" ? "Satış Tahsilatı (Sipariş Ödemesi)" : r.Description;
            int qty = 1;

            var match = System.Text.RegularExpressions.Regex.Match(r.Description ?? "", @"\d{9,}");
            if (match.Success && long.TryParse(match.Value, out long receiptId))
            {
                orderNo = $"#{receiptId}";
                var order = _report.OrderSummaries.FirstOrDefault(o => o.ReceiptId == receiptId);
                if (order != null)
                {
                    title = order.ProductTitle;
                    qty = order.Quantity;
                }
            }
            else
            {
                var matchedOrder = _report.OrderSummaries.FirstOrDefault(o => 
                    Math.Abs((o.OrderDate - r.CreatedAt).TotalDays) <= 3 &&
                    (Math.Abs(o.GrandTotal - Math.Abs(r.Amount)) < 0.1m || Math.Abs(o.Subtotal - Math.Abs(r.Amount)) < 0.1m));
                if (matchedOrder != null)
                {
                    orderNo = $"#{matchedOrder.ReceiptId}";
                    title = matchedOrder.ProductTitle;
                    qty = matchedOrder.Quantity;
                }
            }

            rows.Add(new ToolTipTableRow(dateStr, orderNo, $"{qty} Ad", $"{cur}{displayAmt:N2}", false, "", title));
        }

        _salesTooltipPayload = new ToolTipDataPayload(
            "🟢 Satış ve Gelir Analizi",
            "Dönem içinde mağazanıza gelen siparişler ve brüt satış hareketleri",
            kpiCards,
            new[] { "Tarih", "Sipariş No", "Adet", "Tutar", "Durum", "Ürün / İlan Başlığı" },
            new[] { 0.14f, 0.16f, 0.08f, 0.16f, 0.10f, 0.36f },
            rows,
            salesEntries.Count > 15 ? $"ℹ️ ... ve {salesEntries.Count - 15} adet satış daha listelenmedi." : null
        );
    }

    private void UpdateCostsToolTip(bool showTry)
    {
        var orders = _report.OrderSummaries;
        string cur = showTry ? "₺" : "$";

        decimal totalCOGS_USD = orders.Sum(o => o.ProductCost);
        decimal totalShipping_USD = orders.Sum(o => o.TotalOrderShippingCost);
        decimal totalProduction_USD = orders.Sum(o => o.TotalOrderProductionCost);
        decimal totalPackaging_USD = orders.Sum(o => o.TotalOrderPackagingCost);

        decimal totalCOGS_TRY = orders.Sum(o => Math.Round(o.ProductCost * o.ExchangeRate, 2));
        decimal totalShipping_TRY = orders.Sum(o => Math.Round(o.TotalOrderShippingCost * o.ExchangeRate, 2));
        decimal totalProduction_TRY = orders.Sum(o => Math.Round(o.TotalOrderProductionCost * o.ExchangeRate, 2));
        decimal totalPackaging_TRY = orders.Sum(o => Math.Round(o.TotalOrderPackagingCost * o.ExchangeRate, 2));

        decimal dispCOGS = showTry ? totalCOGS_TRY : totalCOGS_USD;
        decimal dispShip = showTry ? totalShipping_TRY : totalShipping_USD;
        decimal dispProd = showTry ? totalProduction_TRY : totalProduction_USD;
        decimal dispPack = showTry ? totalPackaging_TRY : totalPackaging_USD;

        double shipShare = totalCOGS_USD > 0 ? (double)(totalShipping_USD / totalCOGS_USD * 100) : 0;
        int invoiceCount = orders.Count(o => o.HasInvoice);

        var kpiCards = new List<ToolTipKpiCard>
        {
            new("🚚 Toplam Kargo Maliyeti", $"{cur}{dispShip:N2}", $"Maliyetin %{shipShare:N1}'i", Color.FromArgb(99, 102, 241)),
            new("🏭 Üretim / Hammadde", $"{cur}{dispProd:N2}", null, Color.FromArgb(59, 130, 246)),
            new("🎁 Paketleme & Fatura", $"{cur}{dispPack:N2}", $"{invoiceCount} Fatura Kayıtlı", Color.FromArgb(16, 185, 129))
        };

        var rows = new List<ToolTipTableRow>();
        foreach (var o in orders.Take(15))
        {
            string dateStr = o.OrderDate.ToString("dd.MM.yy");
            string orderNo = $"#{o.ReceiptId}";
            decimal orderShipDisp = showTry ? Math.Round(o.TotalOrderShippingCost * o.ExchangeRate, 2) : o.TotalOrderShippingCost;

            rows.Add(new ToolTipTableRow(
                dateStr,
                orderNo,
                $"{o.Quantity} Ad",
                $"{cur}{orderShipDisp:N2}",
                o.HasInvoice,
                "📎 Fatura",
                o.ProductTitle
            ));
        }

        _costsTooltipPayload = new ToolTipDataPayload(
            "📦 Sipariş & Kargo Maliyet Analizi",
            $"Toplam Sipariş Maliyeti (COGS): {cur}{dispCOGS:N2} | Kargo Harcamaları ve Fatura Durumu",
            kpiCards,
            new[] { "Tarih", "Sipariş No", "Adet", "Kargo Maliyeti", "Fatura", "Ürün / İlan Başlığı" },
            new[] { 0.13f, 0.16f, 0.08f, 0.16f, 0.14f, 0.33f },
            rows,
            orders.Count > 15 ? $"ℹ️ ... ve {orders.Count - 15} adet sipariş daha listelenmedi." : null
        );
    }

    // ── Dönemsel Muhasebe Grid Güncellemesi ───────────────────────────────────

    private void OnPeriodGridColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.ColumnIndex >= _gridPeriod.Columns.Count) return;
        string colName = _gridPeriod.Columns[e.ColumnIndex].Name;

        if (_periodSortColumn == colName)
        {
            _periodSortAscending = !_periodSortAscending;
        }
        else
        {
            _periodSortColumn = colName;
            // Dönem/Tarih için varsayılan A-Z / Kronolojik (true), Sayısal kâr/ciro sütunları için varsayılan Yüksekten Düşüğe (false)
            _periodSortAscending = (colName == "Period");
        }

        UpdatePeriodGrid();
    }

    private void UpdatePeriodGrid()
    {
        _gridPeriod.Columns.Clear();
        UiStyle.ConfigureBaseGrid(_gridPeriod);

        AddPeriodCol("Dönem / Tarih", "Period", 120);
        AddPeriodCol("Brüt Satış", "Gross", 100);
        AddPeriodCol("Etsy Ücretleri", "Fees", 105);
        AddPeriodCol("İç Reklam", "InnerAds", 100);
        AddPeriodCol("Dış Reklam", "OffsiteAds", 100);
        AddPeriodCol("İadeler", "Refunds", 95);
        AddPeriodCol("Banka Yatırımı", "Deposits", 110);
        AddPeriodCol("Ürün Maliyetleri", "Costs", 115);
        AddPeriodCol("Etsy Net Gelir", "NetRevenue", 115);
        AddPeriodCol("Sipariş Gün Kuru", "Rate", 120);
        AddPeriodCol("Gerçek Net Kâr ($)", "ProfitUSD", 125);
        AddPeriodCol("Günlük TL Kârı (₺)", "ProfitTRY", 135);

        void AddPeriodCol(string headerText, string name, int width)
        {
            string displayHeader = headerText;
            if (_periodSortColumn == name)
            {
                displayHeader += _periodSortAscending ? " ▲" : " ▼";
            }
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = displayHeader,
                Name = name,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            _gridPeriod.Columns.Add(col);
            try
            {
                if (_periodSortColumn == name)
                {
                    col.HeaderCell.SortGlyphDirection = _periodSortAscending ? SortOrder.Ascending : SortOrder.Descending;
                }
            }
            catch { /* ignore */ }
        }

        List<PeriodFinancialSummary> rawSummaries = _cboPeriodType.SelectedIndex switch
        {
            0 => _report.DailySummaries,
            1 => _report.WeeklySummaries,
            2 => _report.MonthlySummaries,
            _ => _report.YearlySummaries,
        };

        IEnumerable<PeriodFinancialSummary> sorted = _periodSortColumn switch
        {
            "Period" => _periodSortAscending ? rawSummaries.OrderBy(s => s.SortDate) : rawSummaries.OrderByDescending(s => s.SortDate),
            "Gross" => _periodSortAscending ? rawSummaries.OrderBy(s => s.GrossSales) : rawSummaries.OrderByDescending(s => s.GrossSales),
            "Fees" => _periodSortAscending ? rawSummaries.OrderBy(s => s.EtsyFees) : rawSummaries.OrderByDescending(s => s.EtsyFees),
            "InnerAds" => _periodSortAscending ? rawSummaries.OrderBy(s => s.InnerAdFees) : rawSummaries.OrderByDescending(s => s.InnerAdFees),
            "OffsiteAds" => _periodSortAscending ? rawSummaries.OrderBy(s => s.OffsiteAdFees) : rawSummaries.OrderByDescending(s => s.OffsiteAdFees),
            "Refunds" => _periodSortAscending ? rawSummaries.OrderBy(s => s.Refunds) : rawSummaries.OrderByDescending(s => s.Refunds),
            "Deposits" => _periodSortAscending ? rawSummaries.OrderBy(s => s.Deposits) : rawSummaries.OrderByDescending(s => s.Deposits),
            "Costs" => _periodSortAscending ? rawSummaries.OrderBy(s => s.ProductCosts) : rawSummaries.OrderByDescending(s => s.ProductCosts),
            "NetRevenue" => _periodSortAscending ? rawSummaries.OrderBy(s => s.EtsyNetRevenue) : rawSummaries.OrderByDescending(s => s.EtsyNetRevenue),
            "Rate" => _periodSortAscending ? rawSummaries.OrderBy(s => s.AverageExchangeRate) : rawSummaries.OrderByDescending(s => s.AverageExchangeRate),
            "ProfitUSD" => _periodSortAscending ? rawSummaries.OrderBy(s => s.RealNetProfitUSD) : rawSummaries.OrderByDescending(s => s.RealNetProfitUSD),
            "ProfitTRY" => _periodSortAscending ? rawSummaries.OrderBy(s => s.RealNetProfitTRY) : rawSummaries.OrderByDescending(s => s.RealNetProfitTRY),
            _ => _periodSortAscending ? rawSummaries.OrderBy(s => s.SortDate) : rawSummaries.OrderByDescending(s => s.SortDate)
        };

        _gridPeriod.Rows.Clear();
        foreach (var s in sorted)
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

            _gridPeriod.Rows[idx].Tag = s;

            if (s.RealNetProfitUSD >= 0)
                _gridPeriod.Rows[idx].DefaultCellStyle.ForeColor = UiStyle.SuccessColor;
            else
                _gridPeriod.Rows[idx].DefaultCellStyle.ForeColor = UiStyle.DangerColor;
        }
    }

    // ── Sipariş Net Kâr Grid Güncellemesi ─────────────────────────────────────

    private void OnOrdersGridColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.ColumnIndex >= _gridOrders.Columns.Count) return;
        string colName = _gridOrders.Columns[e.ColumnIndex].Name;

        if (_orderSortColumn == colName)
        {
            _orderSortAscending = !_orderSortAscending;
        }
        else
        {
            _orderSortColumn = colName;
            _orderSortAscending = (colName == "OTitle");
        }

        UpdateOrdersGrid();
    }

    private void UpdateOrdersGrid()
    {
        _gridOrders.Columns.Clear();
        UiStyle.ConfigureBaseGrid(_gridOrders);

        AddOrderCol("Tarih", "ODate", 120);
        AddOrderCol("Sipariş No", "OReceiptId", 100);
        AddOrderCol("Ürün", "OTitle", 0, fill: true);
        AddOrderCol("Adet", "OQty", 60);
        AddOrderCol("Müşteri Ödemesi ($)", "OGross", 135);
        AddOrderCol("Etsy Kesimleri ($)", "OFees", 135);
        AddOrderCol("Dış Reklam ($)", "OAds", 110);
        AddOrderCol("Ürün Maliyeti ($)", "OCost", 125);
        AddOrderCol("Net Kâr ($)", "OProfitUSD", 110);
        AddOrderCol("Kur (₺)", "ORate", 85);
        AddOrderCol("Net Kâr (₺)", "OProfitTRY", 115);
        AddOrderCol("Maliyet", "OCostFlag", 75);

        void AddOrderCol(string headerText, string name, int width, bool fill = false)
        {
            string displayHeader = headerText;
            if (_orderSortColumn == name)
            {
                displayHeader += _orderSortAscending ? " ▲" : " ▼";
            }
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = displayHeader,
                Name = name,
                Width = width,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            _gridOrders.Columns.Add(col);
            try
            {
                if (_orderSortColumn == name)
                {
                    col.HeaderCell.SortGlyphDirection = _orderSortAscending ? SortOrder.Ascending : SortOrder.Descending;
                }
            }
            catch { /* ignore */ }
        }

        var orders = _report.OrderSummaries;
        _gridOrders.Rows.Clear();

        // Özet etiketi güncelle
        var lbl = _gridOrders.Parent?.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "lblOrderSummary")
                ?? Controls.Find("lblOrderSummary", true).OfType<Label>().FirstOrDefault();
        if (lbl != null)
        {
            int missingCost = orders.Count(o => !o.HasCostData);
            decimal totalNetUSD = orders.Sum(o => o.NetProfitUSD);
            decimal totalNetTRY = orders.Sum(o => o.NetProfitTRY);
            lbl.Text = $"📦 {orders.Count} sipariş  |  Toplam Net Kâr: ${totalNetUSD:N2}  /  ₺{totalNetTRY:N2}" +
                       (missingCost > 0 ? $"  |  ⚠️ {missingCost} siparişin maliyeti girilmemiş (çift tıklayın)" : "  |  ✅ Tüm maliyetler girilmiş");
            lbl.ForeColor = missingCost > 0 ? UiStyle.WarningColor : UiStyle.SuccessColor;
        }

        IEnumerable<OrderFinancialSummary> sorted = _orderSortColumn switch
        {
            "ODate" => _orderSortAscending ? orders.OrderBy(o => o.OrderDate) : orders.OrderByDescending(o => o.OrderDate),
            "OReceiptId" => _orderSortAscending ? orders.OrderBy(o => o.ReceiptId) : orders.OrderByDescending(o => o.ReceiptId),
            "OTitle" => _orderSortAscending ? orders.OrderBy(o => o.ProductTitle) : orders.OrderByDescending(o => o.ProductTitle),
            "OQty" => _orderSortAscending ? orders.OrderBy(o => o.Quantity) : orders.OrderByDescending(o => o.Quantity),
            "OGross" => _orderSortAscending ? orders.OrderBy(o => o.GrandTotal) : orders.OrderByDescending(o => o.GrandTotal),
            "OFees" => _orderSortAscending ? orders.OrderBy(o => o.EtsyFees) : orders.OrderByDescending(o => o.EtsyFees),
            "OAds" => _orderSortAscending ? orders.OrderBy(o => o.OffsiteAdFee) : orders.OrderByDescending(o => o.OffsiteAdFee),
            "OCost" => _orderSortAscending ? orders.OrderBy(o => o.ProductCost) : orders.OrderByDescending(o => o.ProductCost),
            "OProfitUSD" => _orderSortAscending ? orders.OrderBy(o => o.NetProfitUSD) : orders.OrderByDescending(o => o.NetProfitUSD),
            "ORate" => _orderSortAscending ? orders.OrderBy(o => o.ExchangeRate) : orders.OrderByDescending(o => o.ExchangeRate),
            "OProfitTRY" => _orderSortAscending ? orders.OrderBy(o => o.NetProfitTRY) : orders.OrderByDescending(o => o.NetProfitTRY),
            "OCostFlag" => _orderSortAscending ? orders.OrderBy(o => o.HasCostData) : orders.OrderByDescending(o => o.HasCostData),
            _ => _orderSortAscending ? orders.OrderBy(o => o.OrderDate) : orders.OrderByDescending(o => o.OrderDate)
        };

        foreach (var o in sorted)
        {
            int idx = _gridOrders.Rows.Add(
                o.OrderDate.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
                $"#{o.ReceiptId}",
                o.ProductTitle,
                o.Quantity,
                $"${o.GrandTotal:N2}",
                $"${o.EtsyFees:N2}",
                o.OffsiteAdFee > 0 ? $"${o.OffsiteAdFee:N2}" : "—",
                o.HasCostData ? $"${o.ProductCost:N2}" : "—",
                $"${o.NetProfitUSD:N2}",
                $"₺{o.ExchangeRate:N2}",
                $"₺{o.NetProfitTRY:N2}",
                o.HasCostData ? (o.HasInvoice ? "✅ 📎" : "✅") : "⚠️ Gir");

            var row = _gridOrders.Rows[idx];
            row.Tag = o;

            if (o.NetProfitUSD >= 0)
                row.DefaultCellStyle.ForeColor = UiStyle.SuccessColor;
            else
                row.DefaultCellStyle.ForeColor = UiStyle.DangerColor;

            if (!o.HasCostData)
                row.Cells["OCostFlag"].Style.ForeColor = UiStyle.WarningColor;
        }
    }

    private void OnOrderGridDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridOrders.Rows.Count) return;
        if (_gridOrders.Rows[e.RowIndex].Tag is not OrderFinancialSummary order) return;

        using var form = new OrderDetailsForm(order);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadReportAsync();
        }
    }

    private void UpdateCharts()
    {
        var months = _report.MonthlySummaries;
        if (months.Count == 0) return;

        bool useTry = _chkUseTry.Checked;
        string curSymbol = useTry ? "₺" : "$";

        var labels = months.Select(m => m.PeriodLabel).ToArray();
        var textPaint = new SolidColorPaint(new SKColor(51, 65, 85));
        var gridLinePaint = new SolidColorPaint(new SKColor(226, 232, 240, 180));

        var salesVals = months.Select(m => (double)(useTry ? m.GrossSalesTRY : m.GrossSales)).ToArray();
        var totalExpensesVals = months.Select(m => (double)(useTry ? m.TotalExpensesTRY : m.TotalExpensesUSD)).ToArray();
        var realProfitVals = months.Select(m => (double)(useTry ? m.RealNetProfitTRY : m.RealNetProfitUSD)).ToArray();
        var etsyNetVals = months.Select(m => (double)(useTry ? m.EtsyNetRevenueTRY : m.EtsyNetRevenue)).ToArray();
        var costsVals = months.Select(m => (double)(useTry ? m.ProductCostsTRY : m.ProductCosts)).ToArray();

        // 1. Bar Chart: 3 Temel Finansal Metrik Karşılaştırması
        _barChart.Series = new ISeries[]
        {
            new ColumnSeries<double> { Name = $"💰 Brüt Ciro ({curSymbol})", Values = salesVals, Fill = new SolidColorPaint(new SKColor(59, 130, 246, 230)), MaxBarWidth = 44, Rx = 6, Ry = 6 },
            new ColumnSeries<double> { Name = $"📉 Toplam Gider ({curSymbol})", Values = totalExpensesVals, Fill = new SolidColorPaint(new SKColor(244, 63, 94, 220)), MaxBarWidth = 44, Rx = 6, Ry = 6 },
            new ColumnSeries<double> { Name = $"💵 Gerçek Net Kâr ({curSymbol})", Values = realProfitVals, Fill = new SolidColorPaint(new SKColor(16, 185, 129, 240)), MaxBarWidth = 44, Rx = 6, Ry = 6 }
        };
        _barChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 12, LabelsPaint = textPaint, SeparatorsPaint = gridLinePaint } };
        _barChart.YAxes = new[] { new Axis { TextSize = 12, LabelsPaint = textPaint, Labeler = v => $"{curSymbol}{v:N0}", SeparatorsPaint = gridLinePaint } };
        _barChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Top;
        _barChart.LegendTextPaint = textPaint;

        // 2. Line Chart: Gerçek Net Kâr vs Etsy Net Gelir Trendi
        _lineChart.Series = new ISeries[]
        {
            new LineSeries<double> { Name = $"Gerçek Net Kâr ({curSymbol})", Values = realProfitVals, Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 3.5f), GeometrySize = 9 },
            new LineSeries<double> { Name = $"Etsy Net Gelir ({curSymbol})", Values = etsyNetVals, Stroke = new SolidColorPaint(new SKColor(99, 102, 241), 2.5f), GeometrySize = 7 },
            new LineSeries<double> { Name = $"Maliyet ({curSymbol})", Values = costsVals, Stroke = new SolidColorPaint(new SKColor(249, 115, 22), 2f), GeometrySize = 6 },
            new LineSeries<double> { Name = $"Brüt Satış ({curSymbol})", Values = salesVals, Stroke = new SolidColorPaint(new SKColor(148, 163, 184), 1.5f), GeometrySize = 0 }
        };
        _lineChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint, SeparatorsPaint = gridLinePaint } };
        _lineChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"{curSymbol}{v:N0}", SeparatorsPaint = gridLinePaint } };
        _lineChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Top;
        _lineChart.LegendTextPaint = textPaint;

        // 3. Ratio Chart: Kâr Marjı ve Maliyet Oranları
        var etsyRetentionPct = months.Select(m => m.EtsyNetRevenue <= 0 ? 0 : Math.Round((double)(m.RealNetProfitUSD / m.EtsyNetRevenue * 100), 1)).ToArray();
        var realMarginPct = months.Select(m => m.GrossSales == 0 ? 0 : Math.Round((double)(m.RealNetProfitUSD / m.GrossSales * 100), 1)).ToArray();
        var costToEtsyNetPct = months.Select(m => m.EtsyNetRevenue <= 0 ? 0 : Math.Round((double)(m.ProductCosts / m.EtsyNetRevenue * 100), 1)).ToArray();
        var feeToGrossPct = months.Select(m => m.GrossSales == 0 ? 0 : Math.Round((double)(m.EtsyFees / m.GrossSales * 100), 1)).ToArray();

        _ratioChart.Series = new ISeries[]
        {
            new LineSeries<double> { Name = "Etsy Net -> Kâr Dönüşümü (%)", Values = etsyRetentionPct, Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 3.5f), GeometrySize = 9 },
            new LineSeries<double> { Name = "Ciro Kâr Marjı (%)", Values = realMarginPct, Stroke = new SolidColorPaint(new SKColor(6, 182, 212), 2.5f), GeometrySize = 7 },
            new LineSeries<double> { Name = "Maliyet / Net Gelir Payı (%)", Values = costToEtsyNetPct, Stroke = new SolidColorPaint(new SKColor(249, 115, 22), 2f), GeometrySize = 6 },
            new LineSeries<double> { Name = "Etsy Kesinti / Ciro Oranı (%)", Values = feeToGrossPct, Stroke = new SolidColorPaint(new SKColor(239, 68, 68), 2f), GeometrySize = 6 }
        };
        _ratioChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint, SeparatorsPaint = gridLinePaint } };
        _ratioChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"%{v:N1}", SeparatorsPaint = gridLinePaint } };
        _ratioChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Top;
        _ratioChart.LegendTextPaint = textPaint;

        // 4. Pie Chart: Dağılım
        decimal totalCosts = useTry ? _report.DailySummaries.Sum(d => d.ProductCostsTRY) : _report.TotalProductCosts;
        decimal realProfit = Math.Max(0, useTry ? _report.DailySummaries.Sum(d => d.RealNetProfitTRY) : _report.RealNetProfitUSD);
        decimal fees = useTry ? _report.DailySummaries.Sum(d => d.EtsyFeesTRY) : _report.TotalFees;
        decimal innerAds = useTry ? _report.DailySummaries.Sum(d => d.InnerAdFeesTRY) : _report.TotalInnerAdFees;
        decimal offsiteAds = useTry ? _report.DailySummaries.Sum(d => d.OffsiteAdFeesTRY) : _report.TotalOffsiteAdFees;
        decimal refunds = useTry ? _report.DailySummaries.Sum(d => d.RefundsTRY) : _report.TotalRefunds;

        _pieChart.Series = new ISeries[]
        {
            new PieSeries<double> { Name = $"Gerçek Net Kâr ({curSymbol})", Values = new[] { (double)realProfit }, Fill = new SolidColorPaint(new SKColor(16, 185, 129)) },
            new PieSeries<double> { Name = $"Ürün Maliyetleri ({curSymbol})", Values = new[] { (double)totalCosts }, Fill = new SolidColorPaint(new SKColor(249, 115, 22)) },
            new PieSeries<double> { Name = $"Etsy Ücretleri ({curSymbol})", Values = new[] { (double)fees }, Fill = new SolidColorPaint(new SKColor(245, 158, 11)) },
            new PieSeries<double> { Name = $"İç Reklam ({curSymbol})", Values = new[] { (double)innerAds }, Fill = new SolidColorPaint(new SKColor(139, 92, 246)) },
            new PieSeries<double> { Name = $"Dış Reklam ({curSymbol})", Values = new[] { (double)offsiteAds }, Fill = new SolidColorPaint(new SKColor(234, 88, 12)) },
            new PieSeries<double> { Name = $"İadeler ({curSymbol})", Values = new[] { (double)refunds }, Fill = new SolidColorPaint(new SKColor(239, 68, 68)) },
        };
    }

    // ── AI Finansal Tahmin & Projeksiyon Güncellemesi ─────────────────────────

    private void UpdateForecastView()
    {
        bool useTry = _chkUseTry.Checked;
        decimal rate = _numExchangeRate.Value;

        var forecast = FinancialForecastingService.GenerateForecast(_report, rate);

        // 1. KPI Kartlarını Güncelle
        if (useTry)
        {
            _lblForecastGross.Text = $"₺{forecast.NextMonthGrossTRY:N0}";
            _lblForecastProfit.Text = $"₺{forecast.NextMonthNetProfitTRY:N0}";
            SetCardSubText(_lblForecastGross, $"Min: ₺{forecast.LowScenarioTRY:N0} — Max: ₺{forecast.HighScenarioTRY:N0}");
            SetCardSubText(_lblForecastProfit, $"Tahmini Dolar: ${forecast.NextMonthNetProfitUSD:N0}");
        }
        else
        {
            _lblForecastGross.Text = $"${forecast.NextMonthGrossUSD:N0}";
            _lblForecastProfit.Text = $"${forecast.NextMonthNetProfitUSD:N0}";
            SetCardSubText(_lblForecastGross, $"Min: ${forecast.LowScenarioUSD:N0} — Max: ${forecast.HighScenarioUSD:N0}");
            SetCardSubText(_lblForecastProfit, $"Tahmini TL: ₺{forecast.NextMonthNetProfitTRY:N0}");
        }

        string growthSign = forecast.GrowthRateMoM >= 0 ? "+" : "";
        _lblForecastOrders.Text = $"~{forecast.NextMonthOrders} Sipariş";
        SetCardSubText(_lblForecastOrders, $"Aylık Büyüme: {growthSign}%{forecast.GrowthRateMoM:N1}");

        _lblForecastStock.Text = $"~{forecast.EstimatedFilamentKg:N1} kg Filament";
        SetCardSubText(_lblForecastStock, $"~{forecast.EstimatedPackagingBoxes} Adet Kargo Kutusu");

        void SetCardSubText(Label lblVal, string text)
        {
            if (lblVal.Parent is TableLayoutPanel p)
            {
                foreach (Control c in p.Controls)
                {
                    if (c.Name == "lblSub" && c is Label sub)
                    {
                        sub.Text = text;
                        break;
                    }
                }
            }
        }

        // 2. Canlı Projeksiyon Grafiği
        if (forecast.Timeline.Count > 0)
        {
            var labels = forecast.Timeline.Select(t => t.PeriodLabel).ToArray();
            var textPaint = new SolidColorPaint(new SKColor(148, 163, 184));

            var profitVals = forecast.Timeline.Select(t => (double)t.ExpectedNetProfitUSD).ToArray();
            var grossVals = forecast.Timeline.Select(t => (double)t.ExpectedGrossUSD).ToArray();
            var lowVals = forecast.Timeline.Select(t => (double)t.LowScenarioProfitUSD).ToArray();
            var highVals = forecast.Timeline.Select(t => (double)t.HighScenarioProfitUSD).ToArray();

            _forecastChart.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Gerçek Net Kâr ($)",
                    Values = profitVals,
                    Fill = new LinearGradientPaint(new SKColor(16, 185, 129, 60), new SKColor(16, 185, 129, 0)),
                    Stroke = new SolidColorPaint(new SKColor(16, 185, 129), 3.5f),
                    GeometrySize = 9,
                    GeometryFill = new SolidColorPaint(new SKColor(16, 185, 129)),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                    LineSmoothness = 0.4,
                },
                new LineSeries<double>
                {
                    Name = "Tahmini Ciro ($)",
                    Values = grossVals,
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(99, 102, 241), 2.5f),
                    GeometrySize = 7,
                    GeometryFill = new SolidColorPaint(new SKColor(99, 102, 241)),
                    LineSmoothness = 0.4,
                },
                new LineSeries<double>
                {
                    Name = "İyimser Senaryo (+%20)",
                    Values = highVals,
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(16, 185, 129, 150), 2f),
                    GeometrySize = 5,
                    LineSmoothness = 0.4,
                },
                new LineSeries<double>
                {
                    Name = "Kötü Senaryo (-%20)",
                    Values = lowVals,
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(239, 68, 68, 150), 2f),
                    GeometrySize = 5,
                    LineSmoothness = 0.4,
                }
            };
            _forecastChart.XAxes = new[] { new Axis { Labels = labels, TextSize = 11, LabelsPaint = textPaint } };
            _forecastChart.YAxes = new[] { new Axis { TextSize = 11, LabelsPaint = textPaint, Labeler = v => $"${v:N0}" } };
        }

        // 3. AI Öneri Listesi (Kesin Piksel Boyutlandırmalı & Okunaklı Renkli Kartlar)
        UpdateForecastAiBadge();
        RenderAiRecommendations(forecast.AiRecommendations);
    }

    private void UpdateForecastAiBadge()
    {
        var settings = AiOptimizationSettingsStore.Load();
        _lblForecastAiBadge.Text = settings.GetActiveBadgeText();
        _lblForecastAiBadge.BackColor = settings.UseOpenAi
            ? Color.FromArgb(16, 80, 50)
            : (settings.UseGemini ? Color.FromArgb(20, 60, 120) : Color.FromArgb(40, 50, 65));
    }

    private async Task RunLiveAiCfoAnalysisAsync()
    {
        var settings = AiOptimizationSettingsStore.Load();
        decimal rate = _numExchangeRate.Value;
        var forecast = FinancialForecastingService.GenerateForecast(_report, rate);

        try
        {
            _btnRefreshForecastAi.Enabled = false;
            _btnRefreshForecastAi.Text = "⏳ Analiz Ediliyor...";
            var liveInsights = await FinancialForecastingService.GenerateDeepAiInsightsAsync(_report, forecast, rate, settings);
            RenderAiRecommendations(liveInsights);
        }
        finally
        {
            _btnRefreshForecastAi.Enabled = true;
            _btnRefreshForecastAi.Text = "✨ Canlı CFO Analizi Al";
        }
    }

    private void RenderAiRecommendations(IEnumerable<string> recommendations)
    {
        int targetWidth = Math.Max(260, _pnlAiRecommendations.ClientSize.Width - 12);
        _pnlAiRecommendations.Controls.Clear();

        var fontTitle = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        var fontBody = new Font("Segoe UI", 9F);
        int contentWidth = Math.Max(220, targetWidth - 28);

        foreach (var rec in recommendations)
        {
            Color accentColor = rec switch
            {
                var r when r.Contains("🔮") => Color.FromArgb(129, 140, 248), // Bright Indigo
                var r when r.Contains("🚀") || r.Contains("🌸") || r.Contains("☀️") => Color.FromArgb(251, 191, 36), // Bright Amber
                var r when r.Contains("📦") => Color.FromArgb(251, 146, 60), // Bright Orange
                var r when r.Contains("🎯") => Color.FromArgb(52, 211, 153), // Bright Emerald
                var r when r.Contains("⚠️") => Color.FromArgb(248, 113, 113), // Bright Red
                _ => Color.FromArgb(167, 139, 250)
            };

            string title = "💡 AI Tavsiyesi";
            string body = rec.Replace("**", "");
            var parts = rec.Split(new[] { "**" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                title = parts[0].Trim();
                body = string.Join("", parts.Skip(1)).Trim();
            }

            var titleSize = TextRenderer.MeasureText(title, fontTitle, new Size(contentWidth, 0), TextFormatFlags.WordBreak);
            var bodySize = TextRenderer.MeasureText(body, fontBody, new Size(contentWidth, 0), TextFormatFlags.WordBreak);
            int totalCardHeight = titleSize.Height + bodySize.Height + 28;

            var itemCard = new ModernCardPanel
            {
                Width = targetWidth,
                Height = totalCardHeight,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(12, 8, 12, 8),
                CornerRadius = 8,
                CardColor = Color.FromArgb(30, 41, 59), // #1E293B Dark Slate Card
                BorderColor = Color.FromArgb(51, 65, 85), // #334155 Slate Border
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = fontTitle,
                ForeColor = accentColor,
                BackColor = Color.Transparent,
                Location = new Point(14, 8),
                Size = new Size(contentWidth, titleSize.Height + 2),
                AutoEllipsis = false,
            };

            var lblBody = new Label
            {
                Text = body,
                Font = fontBody,
                ForeColor = Color.FromArgb(241, 245, 249), // Pure readable bright text (#F1F5F9)
                BackColor = Color.Transparent,
                Location = new Point(14, 12 + titleSize.Height),
                Size = new Size(contentWidth, bodySize.Height + 6),
                AutoEllipsis = false,
            };

            itemCard.Controls.Add(lblTitle);
            itemCard.Controls.Add(lblBody);
            _pnlAiRecommendations.Controls.Add(itemCard);
        }
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
