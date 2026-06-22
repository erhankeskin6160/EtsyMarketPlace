namespace SimilarProductsWinForms;

using System.Diagnostics;
using EtsyMarketPlace.Application.ShopPerformance;

internal sealed class OwnShopPerformanceForm(ShopPerformanceService performanceService) : Form
{
    private readonly DateTimePicker _startPicker = new();
    private readonly DateTimePicker _endPicker = new();
    private readonly Label _titleLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Dictionary<string, Label> _kpis = [];
    private readonly DataGridView _productsGrid = new();
    private ShopPerformanceReport? _report;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        _startPicker.Value = DateTime.Today.AddDays(-30);
        _endPicker.Value = DateTime.Today;
        _ = LoadReportAsync();
    }

    private void BuildLayout()
    {
        Text = "Kendi Magaza Performansi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(20) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = "Kendi Etsy Magazam";
        _titleLabel.Font = new Font("Segoe UI Semibold", 22F);
        _titleLabel.ForeColor = Color.FromArgb(23, 32, 49);
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_titleLabel, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, Padding = new Padding(0, 4, 0, 8) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.Controls.Add(LabelFor("Baslangic"), 0, 0);
        ConfigurePicker(_startPicker);
        toolbar.Controls.Add(_startPicker, 1, 0);
        toolbar.Controls.Add(LabelFor("Bitis"), 2, 0);
        ConfigurePicker(_endPicker);
        toolbar.Controls.Add(_endPicker, 3, 0);
        var refresh = CreateButton("Raporu Yenile");
        refresh.Click += async (_, _) => await LoadReportAsync();
        toolbar.Controls.Add(refresh, 5, 0);
        var shop = CreateButton("Magazayi Ac");
        shop.Click += (_, _) => OpenShop();
        toolbar.Controls.Add(shop, 6, 0);
        var api = CreateButton("API Ayarlari");
        api.Click += (_, _) => { using var form = new EtsyApiSettingsForm(); form.ShowDialog(this); };
        toolbar.Controls.Add(api, 7, 0);
        root.Controls.Add(toolbar, 0, 1);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 4, 0, 8) };
        for (var column = 0; column < 5; column++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        AddKpi(kpis, 0, "Siparis", "orders");
        AddKpi(kpis, 1, "Satilan adet", "units");
        AddKpi(kpis, 2, "Brut ciro", "revenue");
        AddKpi(kpis, 3, "Ort. siparis", "average");
        AddKpi(kpis, 4, "Urun", "products");
        root.Controls.Add(kpis, 0, 2);

        ConfigureProductsGrid();
        root.Controls.Add(_productsGrid, 0, 3);
    }

    private async Task LoadReportAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Yetkili magaza verileri aliniyor...";
            var start = new DateTimeOffset(_startPicker.Value.Date, TimeZoneInfo.Local.GetUtcOffset(_startPicker.Value.Date));
            var endDate = _endPicker.Value.Date.AddDays(1).AddTicks(-1);
            var end = new DateTimeOffset(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate));
            _report = await performanceService.GetReportAsync(start, end);
            BindReport(_report);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Magaza raporu yuklenemedi";
            MessageBox.Show(this, ex.Message, "Kendi Magazam", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void BindReport(ShopPerformanceReport report)
    {
        _titleLabel.Text = $"Magaza Performansi: {report.Shop.ShopName}";
        _kpis["orders"].Text = report.OrderCount.ToString("N0");
        _kpis["units"].Text = report.UnitsSold.ToString("N0");
        _kpis["revenue"].Text = $"{report.CurrencyCode} {report.GrossRevenue:N2}";
        _kpis["average"].Text = $"{report.CurrencyCode} {report.AverageOrderValue:N2}";
        _kpis["products"].Text = report.Products.Count.ToString("N0");
        _productsGrid.DataSource = report.Products.Select((item, index) => new ProductRow(index + 1, item)).ToList();
        _statusLabel.Text = $"{report.PeriodStart:dd.MM.yyyy} - {report.PeriodEnd:dd.MM.yyyy} | {report.OrderCount:N0} siparis";
    }

    private void ConfigureProductsGrid()
    {
        _productsGrid.Dock = DockStyle.Fill;
        _productsGrid.ReadOnly = true;
        _productsGrid.AutoGenerateColumns = false;
        _productsGrid.AllowUserToAddRows = false;
        _productsGrid.AllowUserToDeleteRows = false;
        _productsGrid.RowHeadersVisible = false;
        _productsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _productsGrid.BackgroundColor = Color.White;
        _productsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        AddColumn("#", nameof(ProductRow.Rank), 55);
        AddColumn("Listing", nameof(ProductRow.ListingId), 105);
        AddColumn("Urun basligi", nameof(ProductRow.Title), 500, true);
        AddColumn("Siparis", nameof(ProductRow.OrderCount), 100);
        AddColumn("Satilan adet", nameof(ProductRow.UnitsSold), 110);
        AddColumn("Urun cirosu", nameof(ProductRow.Revenue), 150);
    }

    private void AddColumn(string header, string property, int width, bool fill = false) =>
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });

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

    private void OpenShop()
    {
        if (_report is null || string.IsNullOrWhiteSpace(_report.Shop.ShopUrl)) return;
        Process.Start(new ProcessStartInfo(_report.Shop.ShopUrl) { UseShellExecute = true });
    }

    private static void ConfigurePicker(DateTimePicker picker)
    {
        picker.Dock = DockStyle.Fill;
        picker.Format = DateTimePickerFormat.Custom;
        picker.CustomFormat = "dd.MM.yyyy";
    }

    private static Label LabelFor(string text) => new() { Dock = DockStyle.Fill, Text = text, TextAlign = ContentAlignment.MiddleLeft };

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
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private sealed class ProductRow(int rank, ProductPerformance item)
    {
        public int Rank => rank;
        public long ListingId => item.ListingId;
        public string Title => item.Title;
        public int OrderCount => item.OrderCount;
        public int UnitsSold => item.UnitsSold;
        public string Revenue => $"{item.CurrencyCode} {item.Revenue:N2}";
    }
}
