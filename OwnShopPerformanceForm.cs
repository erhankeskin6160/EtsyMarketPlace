namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ShopPerformance;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class OwnShopPerformanceForm : Form
{
    private readonly ShopPerformanceService _performanceService;
    private readonly ShopPerformanceHistoryService _historyService;
    private readonly IAiListingOptimizer _aiListingOptimizer;
    private readonly ListingOptimizationHistoryService _optimizationHistoryService;

    private readonly ModernDateTimePicker _startPicker = new();
    private readonly ModernDateTimePicker _endPicker = new();
    private readonly Label _titleLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _lblAiBadge = new();
    private readonly Button _btnAiAudit = new();

    private readonly Dictionary<string, Label> _kpiValues = [];
    private readonly Dictionary<string, Label> _kpiChanges = [];
    private readonly DataGridView _productsGrid = new();
    private readonly DataGridView _paretoGrid = new();
    private readonly DataGridView _historyGrid = new();

    private ShopPerformanceComparison? _comparison;
    private ShopParetoAnalysisService.ParetoSummary? _paretoSummary;

    public OwnShopPerformanceForm(
        ShopPerformanceService performanceService,
        ShopPerformanceHistoryService historyService,
        IAiListingOptimizer aiListingOptimizer,
        ListingOptimizationHistoryService optimizationHistoryService)
    {
        _performanceService = performanceService;
        _historyService = historyService;
        _aiListingOptimizer = aiListingOptimizer;
        _optimizationHistoryService = optimizationHistoryService;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        UpdateAiBadge();
        SetDatePreset(30); // Default to Last 30 Days
        _ = LoadReportAsync();
    }

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
        Text = "Etsy Mağazamın Satış Performansı & Büyüme Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1100, 720));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));  // 2-Row Toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));  // KPI Summary Strip
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // TabControl (Grids)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Bottom Row Action Bar
        Controls.Add(root);

        // 1. Header (Title + AI Badge + Status)
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = "🏪 Mağaza Satış Performansı";
        _titleLabel.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
        _titleLabel.ForeColor = UiStyle.TextDark;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_titleLabel, 0, 0);

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
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 2, 0);
        root.Controls.Add(header, 0, 0);

        // 2. Toolbar (Row 0: Quick Presets & Date Pickers, Row 1: AI & Tools)
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 2, 0, 2) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        // Row 0: Quick Date Buttons + Pickers + Refresh
        var row0 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        row0.Controls.Add(CreatePresetButton("Son 7 Gün", () => SetDatePreset(7)));
        row0.Controls.Add(CreatePresetButton("Son 30 Gün", () => SetDatePreset(30)));
        row0.Controls.Add(CreatePresetButton("Bu Ay (MTD)", SetThisMonthPreset));
        row0.Controls.Add(CreatePresetButton("Son 90 Gün", () => SetDatePreset(90)));
        row0.Controls.Add(CreatePresetButton("Bu Yıl (YTD)", SetThisYearPreset));

        var lblStart = new Label { Text = "Başlangıç:", AutoSize = true, Margin = new Padding(8, 7, 2, 0), ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI Semibold", 8.5F) };
        row0.Controls.Add(lblStart);
        ConfigurePicker(_startPicker);
        row0.Controls.Add(_startPicker);

        var lblEnd = new Label { Text = "Bitiş:", AutoSize = true, Margin = new Padding(6, 7, 2, 0), ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI Semibold", 8.5F) };
        row0.Controls.Add(lblEnd);
        ConfigurePicker(_endPicker);
        row0.Controls.Add(_endPicker);

        var refresh = CreateButton("🔄 Raporu Yenile");
        refresh.Height = 32;
        refresh.Width = 150;
        refresh.Margin = new Padding(8, 0, 0, 0);
        refresh.Click += async (_, _) => await LoadReportAsync();
        row0.Controls.Add(refresh);
        toolbar.Controls.Add(row0, 0, 0);

        // Row 1: AI & Action Tools
        var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };

        _btnAiAudit.Text = "✨ AI Mağaza Analizi Al";
        _btnAiAudit.Height = 34;
        _btnAiAudit.Width = 190;
        _btnAiAudit.FlatStyle = FlatStyle.Flat;
        _btnAiAudit.FlatAppearance.BorderSize = 0;
        _btnAiAudit.BackColor = Color.FromArgb(124, 58, 237); // Purple AI
        _btnAiAudit.ForeColor = Color.White;
        _btnAiAudit.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _btnAiAudit.Cursor = Cursors.Hand;
        _btnAiAudit.Margin = new Padding(0, 0, 8, 0);
        _btnAiAudit.Click += async (_, _) => await RunAiStoreAuditAsync();
        row1.Controls.Add(_btnAiAudit);

        var listingAi = CreateButton("🎯 Listing AI Denetimi");
        listingAi.Height = 34;
        listingAi.Width = 175;
        listingAi.Margin = new Padding(0, 0, 8, 0);
        listingAi.Click += (_, _) =>
        {
            using var form = new OwnShopListingAiAuditForm(_aiListingOptimizer, _optimizationHistoryService);
            form.ShowDialog(this);
        };
        row1.Controls.Add(listingAi);

        var shop = CreateButton("🌐 Mağazayı Aç", isSecondary: true);
        shop.Height = 34;
        shop.Width = 135;
        shop.Margin = new Padding(0, 0, 8, 0);
        shop.Click += (_, _) => OpenShop();
        row1.Controls.Add(shop);

        var api = CreateButton("API Ayarları", isSecondary: true);
        api.Height = 34;
        api.Width = 120;
        api.Margin = new Padding(0, 0, 8, 0);
        api.Click += (_, _) => { using var form = new EtsyApiSettingsForm(); form.ShowDialog(this); };
        row1.Controls.Add(api);

        var btnCsv = CreateButton("📥 CSV Dışa Aktar", isSecondary: true);
        btnCsv.Height = 34;
        btnCsv.Width = 150;
        btnCsv.Click += (_, _) => ExportProductsCsv();
        row1.Controls.Add(btnCsv);

        toolbar.Controls.Add(row1, 0, 1);
        root.Controls.Add(toolbar, 0, 1);

        // 3. KPI Summary Strip (5 Sleek Cards)
        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 2, 0, 4) };
        for (var column = 0; column < 5; column++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        AddKpi(kpis, 0, "Sipariş Sayısı", "orders", Color.FromArgb(16, 185, 129));
        AddKpi(kpis, 1, "Satılan Adet", "units", Color.FromArgb(14, 165, 233));
        AddKpi(kpis, 2, "Brüt Ciro", "revenue", Color.FromArgb(99, 102, 241));
        AddKpi(kpis, 3, "Ort. Sipariş (AOV)", "average", Color.FromArgb(245, 158, 11));
        AddKpi(kpis, 4, "Aktif Satan Ürün", "products", Color.FromArgb(236, 72, 153));
        root.Controls.Add(kpis, 0, 2);

        // 4. Content Tabs
        ConfigureProductsGrid();
        ConfigureParetoGrid();
        ConfigureHistoryGrid();
        root.Controls.Add(BuildContentTabs(), 0, 3);

        // 5. Bottom Quick Action Bar
        var bottomActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        var btnOptSelected = CreateActionButton("✨ Seçili Ürünü AI ile Optimize Et", OpenSelectedListingAiOptimization, Color.FromArgb(124, 58, 237));
        var btnSendStudio = CreateActionButton("📸 Görsel Studio'ya Gönder", SendSelectedToImageStudio, Color.FromArgb(79, 70, 229));
        var btnProfitCalc = CreateActionButton("💰 Kâr Simülatöründe İncele", OpenSelectedInProfitCalculator, Color.FromArgb(16, 185, 129));
        var btnOpenEtsy = CreateActionButton("🌐 Etsy'de Canlı Aç", OpenSelectedListingOnEtsy, UiStyle.PrimaryColor);

        bottomActions.Controls.Add(btnOptSelected);
        bottomActions.Controls.Add(btnSendStudio);
        bottomActions.Controls.Add(btnProfitCalc);
        bottomActions.Controls.Add(btnOpenEtsy);
        root.Controls.Add(bottomActions, 0, 4);
    }

    private static Button CreatePresetButton(string text, Action action)
    {
        var btn = new Button
        {
            Text = text,
            Height = 32,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Segoe UI Semibold", 8.5F),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0)
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
        btn.Click += (_, _) => action();
        return btn;
    }

    private static Button CreateActionButton(string text, Action action, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            Height = 34,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => action();
        return btn;
    }

    private void SetDatePreset(int days)
    {
        _startPicker.Value = DateTime.Today.AddDays(-days);
        _endPicker.Value = DateTime.Today;
    }

    private void SetThisMonthPreset()
    {
        var today = DateTime.Today;
        _startPicker.Value = new DateTime(today.Year, today.Month, 1);
        _endPicker.Value = today;
    }

    private void SetThisYearPreset()
    {
        var today = DateTime.Today;
        _startPicker.Value = new DateTime(today.Year, 1, 1);
        _endPicker.Value = today;
    }

    private async Task LoadReportAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Yetkili mağaza sipariş ve ciro verileri çekiliyor...";
            var startDate = DateTime.SpecifyKind(_startPicker.Value.Date, DateTimeKind.Unspecified);
            var endDate = DateTime.SpecifyKind(_endPicker.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
            var start = new DateTimeOffset(startDate, TimeZoneInfo.Local.GetUtcOffset(startDate));
            var end = new DateTimeOffset(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate));

            _comparison = await _performanceService.GetComparisonAsync(start, end);
            _paretoSummary = ShopParetoAnalysisService.Analyze(_comparison.Current.Products);

            BindComparison(_comparison);
            BindPareto(_paretoSummary);
            await SaveAndLoadHistoryAsync(_comparison.Current);
            _statusLabel.Text = $"Rapor güncellendi | Toplam Ciro: ${_comparison.Current.GrossRevenue:N2} | {_comparison.Current.OrderCount} Sipariş";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Mağaza raporu yüklenemedi.";
            MessageBox.Show(this, ex.Message, "Mağazam Performansı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RunAiStoreAuditAsync()
    {
        if (_comparison == null || _paretoSummary == null)
        {
            MessageBox.Show(this, "Lütfen önce mağaza raporunu yükleyin.", "Rapor Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var aiSettings = AiOptimizationSettingsStore.Load();

        try
        {
            _btnAiAudit.Enabled = false;
            _btnAiAudit.Text = "Analiz Ediliyor...";
            _statusLabel.Text = "Yapay zeka mağaza satış ve Pareto verilerini inceliyor...";

            string report = await ShopAiConsultantService.GenerateStoreAuditReportAsync(_comparison, _paretoSummary, aiSettings);

            using var form = new Form
            {
                Text = "✨ AI Mağaza Büyüme & Satış Danışmanı Raporu",
                Size = new Size(820, 620),
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
            MessageBox.Show(this, $"AI Raporu üretilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnAiAudit.Enabled = true;
            _btnAiAudit.Text = "✨ AI Mağaza Analizi Al";
            _statusLabel.Text = "AI analizi tamamlandı.";
        }
    }

    private Control BuildContentTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var comparisonTab = new TabPage("📊 Dönem Karşılaştırması") { BackColor = Color.White, Padding = new Padding(4) };
        comparisonTab.Controls.Add(_productsGrid);
        tabs.TabPages.Add(comparisonTab);

        var paretoTab = new TabPage("⭐ ABC / Pareto Segmentasyonu") { BackColor = Color.White, Padding = new Padding(4) };
        paretoTab.Controls.Add(_paretoGrid);
        tabs.TabPages.Add(paretoTab);

        var historyTab = new TabPage("🕒 Kayıt & Snapshot Geçmişi") { BackColor = Color.White, Padding = new Padding(4) };
        historyTab.Controls.Add(_historyGrid);
        tabs.TabPages.Add(historyTab);
        return tabs;
    }

    private void AddKpi(TableLayoutPanel root, int column, string title, string key, Color accent)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(3),
            Padding = new Padding(10, 6, 10, 6)
        };

        var titleLbl = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = title,
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Color.FromArgb(148, 163, 184)
        };

        var valueLbl = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "-",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = accent
        };

        var changeLbl = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Önceki: -",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(203, 213, 225)
        };

        panel.Controls.Add(changeLbl);
        panel.Controls.Add(valueLbl);
        panel.Controls.Add(titleLbl);
        _kpiValues[key] = valueLbl;
        _kpiChanges[key] = changeLbl;
        root.Controls.Add(panel, column, 0);
    }

    private void ConfigureProductsGrid()
    {
        UiStyle.ConfigureBaseGrid(_productsGrid);
        _productsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _productsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _productsGrid.MultiSelect = false;
        _productsGrid.CellDoubleClick += (_, _) => OpenSelectedListingOnEtsy();

        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Width = 40, FillWeight = 40 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Listing ID", Width = 95, FillWeight = 95 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün Başlığı", FillWeight = 260 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sipariş", Width = 65, FillWeight = 65 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Önceki", Width = 65, FillWeight = 65 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Adet", Width = 60, FillWeight = 60 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Önceki Adet", Width = 75, FillWeight = 75 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Adet Fark", Width = 75, FillWeight = 75 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro", Width = 90, FillWeight = 90 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Önceki Ciro", Width = 90, FillWeight = 90 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro Fark", Width = 95, FillWeight = 95 });
    }

    private void ConfigureParetoGrid()
    {
        UiStyle.ConfigureBaseGrid(_paretoGrid);
        _paretoGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _paretoGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _paretoGrid.MultiSelect = false;

        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kategori (ABC)", Width = 180, FillWeight = 180 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Listing ID", Width = 95, FillWeight = 95 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün Başlığı", FillWeight = 260 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Satılan Adet", Width = 80, FillWeight = 80 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro", Width = 90, FillWeight = 90 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro Payı %", Width = 85, FillWeight = 85 });
        _paretoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kümülatif %", Width = 85, FillWeight = 85 });
    }

    private void ConfigureHistoryGrid()
    {
        UiStyle.ConfigureBaseGrid(_historyGrid);
        _historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tarih", FillWeight = 110 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Aralık", FillWeight = 160 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sipariş", FillWeight = 70 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Adet", FillWeight = 70 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro", FillWeight = 90 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ort. Sipariş", FillWeight = 90 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün Sayısı", FillWeight = 80 });
    }

    private void BindComparison(ShopPerformanceComparison comparison)
    {
        _titleLabel.Text = $"🏪 Mağaza: {comparison.Current.Shop.ShopName}";
        var cur = comparison.Current;
        var prev = comparison.Previous;

        _kpiValues["orders"].Text = cur.OrderCount.ToString(CultureInfo.InvariantCulture);
        _kpiChanges["orders"].Text = $"Önceki: {prev.OrderCount} | {FormatChange(comparison.Orders.Difference, comparison.Orders.PercentageChange)}";

        _kpiValues["units"].Text = cur.UnitsSold.ToString(CultureInfo.InvariantCulture);
        _kpiChanges["units"].Text = $"Önceki: {prev.UnitsSold} | {FormatChange(comparison.Units.Difference, comparison.Units.PercentageChange)}";

        _kpiValues["revenue"].Text = $"{cur.CurrencyCode} {cur.GrossRevenue:N2}";
        _kpiChanges["revenue"].Text = $"Önceki: {prev.GrossRevenue:N2} | {FormatChange(comparison.Revenue.Difference, comparison.Revenue.PercentageChange, cur.CurrencyCode)}";

        _kpiValues["average"].Text = $"{cur.CurrencyCode} {cur.AverageOrderValue:N2}";
        _kpiChanges["average"].Text = $"Önceki: {prev.AverageOrderValue:N2} | {FormatChange(comparison.AverageOrder.Difference, comparison.AverageOrder.PercentageChange, cur.CurrencyCode)}";

        _kpiValues["products"].Text = cur.Products.Count.ToString(CultureInfo.InvariantCulture);
        _kpiChanges["products"].Text = $"Önceki: {prev.Products.Count} | {cur.Products.Count - prev.Products.Count:+0;-0;0}";

        _productsGrid.Rows.Clear();
        var index = 1;
        foreach (var p in comparison.Products)
        {
            _productsGrid.Rows.Add(
                index++,
                p.ListingId,
                p.Title,
                p.CurrentOrderCount,
                p.PreviousOrderCount,
                p.CurrentUnitsSold,
                p.PreviousUnitsSold,
                FormatDiff(p.UnitDifference),
                $"{cur.CurrencyCode} {p.CurrentRevenue:N2}",
                $"{cur.CurrencyCode} {p.PreviousRevenue:N2}",
                FormatDiff(p.RevenueDifference, cur.CurrencyCode));
        }
    }

    private void BindPareto(ShopParetoAnalysisService.ParetoSummary pareto)
    {
        _paretoGrid.Rows.Clear();
        foreach (var item in pareto.Items)
        {
            var rowIndex = _paretoGrid.Rows.Add(
                item.CategoryDisplay,
                item.ListingId,
                item.Title,
                item.UnitsSold,
                $"${item.Revenue:N2}",
                $"%{item.RevenueSharePercent:F1}",
                $"%{item.CumulativeSharePercent:F1}");

            var row = _paretoGrid.Rows[rowIndex];

            if (item.Category == ShopParetoAnalysisService.ParetoCategory.A_Stars)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(16, 45, 34); // Deep Emerald Green
                row.DefaultCellStyle.ForeColor = Color.FromArgb(167, 243, 208); // Light Mint Green
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(22, 101, 52);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else if (item.Category == ShopParetoAnalysisService.ParetoCategory.B_Potentials)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59); // Deep Slate Navy
                row.DefaultCellStyle.ForeColor = Color.FromArgb(253, 224, 71); // Light Gold
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 65, 85);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else // C_SlowMovers
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(45, 18, 24); // Deep Wine/Crimson
                row.DefaultCellStyle.ForeColor = Color.FromArgb(254, 202, 202); // Light Rose
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(127, 29, 29);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
        }
    }

    private async Task SaveAndLoadHistoryAsync(ShopPerformanceReport report)
    {
        await _historyService.SaveAsync(report);
        var history = await _historyService.GetHistoryAsync(report.Shop.ShopId);
        _historyGrid.Rows.Clear();
        foreach (var item in history)
        {
            _historyGrid.Rows.Add(
                item.CapturedAt.ToLocalTime().ToString("g"),
                $"{item.PeriodStart.ToLocalTime():d} - {item.PeriodEnd.ToLocalTime():d}",
                item.OrderCount,
                item.UnitsSold,
                $"{item.CurrencyCode} {item.GrossRevenue:N2}",
                $"{item.CurrencyCode} {item.AverageOrderValue:N2}",
                item.Products.Count);
        }
    }

    private (long ListingId, string Title) GetSelectedListingInfo()
    {
        if (_productsGrid.CurrentRow != null && _productsGrid.CurrentRow.Cells.Count > 2)
        {
            var idObj = _productsGrid.CurrentRow.Cells[1].Value;
            var titleObj = _productsGrid.CurrentRow.Cells[2].Value;
            if (idObj != null && long.TryParse(idObj.ToString(), out long id))
            {
                return (id, titleObj?.ToString() ?? "");
            }
        }

        if (_paretoGrid.CurrentRow != null && _paretoGrid.CurrentRow.Cells.Count > 2)
        {
            var idObj = _paretoGrid.CurrentRow.Cells[1].Value;
            var titleObj = _paretoGrid.CurrentRow.Cells[2].Value;
            if (idObj != null && long.TryParse(idObj.ToString(), out long id))
            {
                return (id, titleObj?.ToString() ?? "");
            }
        }

        return (0, "");
    }

    private void OpenSelectedListingAiOptimization()
    {
        var (listingId, _) = GetSelectedListingInfo();
        if (listingId <= 0)
        {
            MessageBox.Show(this, "Lütfen tablodan bir ürün seçin.", "Ürün Seçin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new OwnShopListingAiAuditForm(_aiListingOptimizer, _optimizationHistoryService);
        form.ShowDialog(this);
    }

    private void SendSelectedToImageStudio()
    {
        var (listingId, title) = GetSelectedListingInfo();
        if (listingId <= 0)
        {
            MessageBox.Show(this, "Lütfen tablodan bir ürün seçin.", "Ürün Seçin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (DashboardForm.Instance != null)
        {
            _ = DashboardForm.Instance.OpenModuleByIdAsync("image-editor");
        }
    }

    private void OpenSelectedInProfitCalculator()
    {
        if (DashboardForm.Instance != null)
        {
            _ = DashboardForm.Instance.OpenModuleByIdAsync("profit-calc");
        }
    }

    private void OpenSelectedListingOnEtsy()
    {
        var (listingId, _) = GetSelectedListingInfo();
        if (listingId > 0)
        {
            Process.Start(new ProcessStartInfo($"https://www.etsy.com/listing/{listingId}") { UseShellExecute = true });
        }
        else
        {
            OpenShop();
        }
    }

    private void OpenShop()
    {
        if (_comparison != null && _comparison.Current.Shop.ShopId > 0)
        {
            Process.Start(new ProcessStartInfo($"https://www.etsy.com/shop/{_comparison.Current.Shop.ShopName}") { UseShellExecute = true });
        }
    }

    private void ExportProductsCsv()
    {
        if (_comparison == null || _comparison.Products.Count == 0)
        {
            MessageBox.Show(this, "Dışa aktarılacak ürün verisi bulunamadı.", "CSV Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"magaza-performansi-{DateTime.Now:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine("ListingId,Baslik,Siparis,OncekiSiparis,Adet,OncekiAdet,Ciro,OncekiCiro,CiroFarki");
        foreach (var p in _comparison.Products)
        {
            sb.AppendLine($"{p.ListingId},\"{p.Title.Replace("\"", "\"\"")}\",{p.CurrentOrderCount},{p.PreviousOrderCount},{p.CurrentUnitsSold},{p.PreviousUnitsSold},{p.CurrentRevenue:F2},{p.PreviousRevenue:F2},{p.RevenueDifference:F2}");
        }
        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        _statusLabel.Text = "CSV başarıyla dışa aktarıldı.";
    }

    private static string FormatChange(decimal difference, decimal? percent, string? currency = null)
    {
        var sign = difference >= 0 ? "+" : string.Empty;
        var diffText = currency is null ? $"{sign}{difference}" : $"{sign}{currency} {difference:N2}";
        var percentText = percent.HasValue ? $" ({sign}{percent.Value:0.0}%)" : "";
        return $"{diffText}{percentText}";
    }

    private static string FormatDiff(decimal diff, string? currency = null)
    {
        var sign = diff > 0 ? "+" : string.Empty;
        return currency is null ? $"{sign}{diff}" : $"{sign}{currency} {diff:N2}";
    }

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button();
        ConfigureButton(button, text, isSecondary);
        return button;
    }

    private static void ConfigureButton(Button button, string text, bool isSecondary = false)
    {
        button.Text = text;
        button.BackColor = isSecondary ? UiStyle.SecondaryColor : UiStyle.PrimaryColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isSecondary ? UiStyle.SecondaryHover : UiStyle.PrimaryHover;
        button.Font = UiStyle.SemiboldBaseFont;
        button.Cursor = Cursors.Hand;
    }

    private static void ConfigurePicker(ModernDateTimePicker picker)
    {
        picker.Format = DateTimePickerFormat.Short;
        picker.Font = UiStyle.BaseFont;
        picker.Width = 125;
        picker.Margin = new Padding(2, 4, 4, 4);
    }
}
