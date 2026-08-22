namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AbTesting;

internal sealed class ListingAbTestForm : Form
{
    private readonly AbTestService _abTestService;
    private readonly DataGridView _grid = new();
    private readonly BindingSource _bindingSource = new();
    private readonly Label _statusLabel = new();
    private readonly Label _winnerBadge = new();
    private readonly Label _summaryLabel = new();
    private readonly ListBox _takeawaysListBox = new();
    private readonly Label _viewsChangeLabel = new();
    private readonly Label _favsChangeLabel = new();
    private readonly Label _salesChangeLabel = new();
    private readonly TextBox _variantATitle = new() { ReadOnly = true, Multiline = true };
    private readonly TextBox _variantBTitle = new() { ReadOnly = true, Multiline = true };
    private readonly TextBox _variantATags = new() { ReadOnly = true, Multiline = true };
    private readonly TextBox _variantBTags = new() { ReadOnly = true, Multiline = true };
    private List<ListingAbTestExperiment> _experiments = [];

    public ListingAbTestForm(AbTestService abTestService)
    {
        _abTestService = abTestService;
        BuildLayout();
        Shown += async (_, _) => await LoadDataAsync();
    }

    private void BuildLayout()
    {
        Text = "Etsy Listing A/B Test ve Etki İzleyici";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Listing A/B Test ve Etki İzleyici",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "A/B test verileri yükleniyor...";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Toolbar
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (var i = 0; i < 4; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var newTestBtn = UiStyle.CreateButton("Yeni A/B Testi");
        newTestBtn.Click += async (_, _) => await OpenNewTestDialogAsync();
        toolbar.Controls.Add(newTestBtn, 0, 0);

        var updateMetricsBtn = UiStyle.CreateButton("Metrik Güncelle");
        updateMetricsBtn.Click += async (_, _) => await OpenUpdateMetricsDialogAsync();
        toolbar.Controls.Add(updateMetricsBtn, 1, 0);

        var refreshBtn = UiStyle.CreateButton("Yenile");
        refreshBtn.Click += async (_, _) => await LoadDataAsync();
        toolbar.Controls.Add(refreshBtn, 2, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        toolbar.Controls.Add(closeBtn, 3, 0);
        root.Controls.Add(toolbar, 0, 1);

        // Content Area (Grid left 45%, Details right 55%)
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

        // Grid Section
        ConfigureGrid();
        content.Controls.Add(_grid, 0, 0);

        // Right Detail Panel
        content.Controls.Add(BuildDetailPanel(), 1, 0);
        root.Controls.Add(content, 0, 2);

        _bindingSource.CurrentChanged += (_, _) => DisplaySelectedExperimentDetails();

        UiStyle.AttachSidebarNav(this, "ab_test");
    }

    private Control BuildDetailPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12, 0, 0, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Impact KPI Cards
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));   // Side by side comparison
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));   // Winner badge & Takeaways

        // KPI Cards Row
        var kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        for (var i = 0; i < 3; i++) kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));

        AddKpiCard(kpiTable, 0, "Görüntülenme Değişimi", _viewsChangeLabel);
        AddKpiCard(kpiTable, 1, "Favori Değişimi", _favsChangeLabel);
        AddKpiCard(kpiTable, 2, "Satış Değişimi", _salesChangeLabel);
        panel.Controls.Add(kpiTable, 0, 0);

        // Side by Side Comparison
        var compareGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Varyant Karşılaştırması (Varyant A: Orijinal vs Varyant B: Yeni AI)",
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        var compareTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(6) };
        compareTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        compareTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        compareTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        compareTable.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        compareTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        compareTable.RowStyles.Add(new RowStyle(SizeType.Percent, 52));

        compareTable.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Varyant A Başlık (Orijinal)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 0);
        compareTable.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Varyant B Başlık (Yeni AI)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 1, 0);
        _variantATitle.Dock = DockStyle.Fill;
        _variantBTitle.Dock = DockStyle.Fill;
        compareTable.Controls.Add(_variantATitle, 0, 1);
        compareTable.Controls.Add(_variantBTitle, 1, 1);

        compareTable.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Varyant A Tagler", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 2);
        compareTable.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Varyant B Tagler", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 1, 2);
        _variantATags.Dock = DockStyle.Fill;
        _variantBTags.Dock = DockStyle.Fill;
        compareTable.Controls.Add(_variantATags, 0, 3);
        compareTable.Controls.Add(_variantBTags, 1, 3);

        compareGroup.Controls.Add(compareTable);
        panel.Controls.Add(compareGroup, 0, 1);

        // Winner & Takeaways Section
        var winnerGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "A/B Testi Sonuç Karnesi ve Kazanan",
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        var winnerTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(6) };
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _winnerBadge.Dock = DockStyle.Fill;
        _winnerBadge.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _winnerBadge.TextAlign = ContentAlignment.MiddleCenter;
        _winnerBadge.ForeColor = UiStyle.TextDark;
        _winnerBadge.BackColor = Color.FromArgb(238, 242, 255); // Indigo Tint
        _winnerBadge.Text = "Kazanan: Belirlenmedi";
        winnerTable.Controls.Add(_winnerBadge, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Font = new Font("Segoe UI", 9F);
        _summaryLabel.ForeColor = UiStyle.TextDark;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.Text = "Seçili A/B testi henüz metrik içermiyor.";
        winnerTable.Controls.Add(_summaryLabel, 0, 1);

        _takeawaysListBox.Dock = DockStyle.Fill;
        _takeawaysListBox.Font = new Font("Segoe UI", 9F);
        winnerTable.Controls.Add(_takeawaysListBox, 0, 2);

        winnerGroup.Controls.Add(winnerTable);
        panel.Controls.Add(winnerGroup, 0, 2);

        return panel;
    }

    private static void AddKpiCard(TableLayoutPanel parent, int col, string title, Label valueLabel)
    {
        var card = new SimilarProductsWinForms.Controls.ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(col == 0 ? 0 : 4, 2, col == 2 ? 0 : 4, 4),
            Padding = new Padding(10, 6, 10, 6),
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Transparent };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.TextMuted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "-";
        valueLabel.Font = UiStyle.KpiValueFont;
        valueLabel.ForeColor = UiStyle.TextDark;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(valueLabel, 0, 1);

        card.Controls.Add(panel);
        parent.Controls.Add(card, col, 0);
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(AbTestGridRow.Id), Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Test Adı", DataPropertyName = nameof(AbTestGridRow.ExperimentName), Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün", DataPropertyName = nameof(AbTestGridRow.ListingTitle), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Durum", DataPropertyName = nameof(AbTestGridRow.Status), Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Öncesi/Sonrası Satiş", DataPropertyName = nameof(AbTestGridRow.SalesDisplay), Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tarih", DataPropertyName = nameof(AbTestGridRow.Created), Width = 110 });
        _grid.DataSource = _bindingSource;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _statusLabel.Text = "A/B testleri yükleniyor...";
            _experiments = (await _abTestService.GetRecentAsync()).ToList();
            _bindingSource.DataSource = _experiments.Select(e => new AbTestGridRow(e)).ToList();

            if (_experiments.Count > 0)
            {
                _statusLabel.Text = $"{_experiments.Count} A/B testi yüklendi";
                DisplaySelectedExperimentDetails();
            }
            else
            {
                _statusLabel.Text = "Henüz kaydedilmiş A/B testi bulunmuyor";
                ResetDetailDisplay();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "A/B Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "A/B testleri yüklenemedi";
        }
    }

    private void DisplaySelectedExperimentDetails()
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            ResetDetailDisplay();
            return;
        }

        var exp = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (exp is null)
        {
            ResetDetailDisplay();
            return;
        }

        var report = _abTestService.CalculateImpact(exp);

        _variantATitle.Text = exp.VariantA_Title;
        _variantBTitle.Text = exp.VariantB_Title;
        _variantATags.Text = string.Join(", ", exp.VariantA_Tags);
        _variantBTags.Text = string.Join(", ", exp.VariantB_Tags);

        _viewsChangeLabel.Text = $"{report.BeforeViews} ➔ {report.AfterViews} (%{report.ViewsChangePercent:+#;-#;0})";
        _viewsChangeLabel.ForeColor = report.ViewsChangePercent >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _favsChangeLabel.Text = $"{report.BeforeFavorites} ➔ {report.AfterFavorites} (%{report.FavoritesChangePercent:+#;-#;0})";
        _favsChangeLabel.ForeColor = report.FavoritesChangePercent >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _salesChangeLabel.Text = $"{report.BeforeSales} ➔ {report.AfterSales} (%{report.SalesChangePercent:+#;-#;0})";
        _salesChangeLabel.ForeColor = report.SalesChangePercent >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _winnerBadge.Text = $"🏆 Kazanan: {report.WinnerVariant}";
        _winnerBadge.BackColor = report.WinnerVariant.Contains("Varyant B")
            ? UiStyle.SuccessColor
            : report.WinnerVariant.Contains("Varyant A")
                ? UiStyle.DangerColor
                : UiStyle.SecondaryColor;

        _summaryLabel.Text = report.ImpactSummary;

        _takeawaysListBox.Items.Clear();
        foreach (var takeaway in report.KeyTakeaways)
        {
            _takeawaysListBox.Items.Add($"• {takeaway}");
        }
    }

    private void ResetDetailDisplay()
    {
        _variantATitle.Clear();
        _variantBTitle.Clear();
        _variantATags.Clear();
        _variantBTags.Clear();
        _viewsChangeLabel.Text = "-";
        _favsChangeLabel.Text = "-";
        _salesChangeLabel.Text = "-";
        _winnerBadge.Text = "Kazanan: Belirlenmedi";
        _winnerBadge.BackColor = UiStyle.SecondaryColor;
        _summaryLabel.Text = "Seçili test bulunmuyor.";
        _takeawaysListBox.Items.Clear();
    }

    private async Task OpenNewTestDialogAsync()
    {
        using var dlg = new Form
        {
            Text = "Yeni A/B Testi Başlat",
            Width = 580,
            Height = 480,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        UiStyle.ApplyTheme(dlg);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, Padding = new Padding(16) };
        for (var i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, i % 2 == 0 ? 22 : 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        var nameInput = new TextBox { Dock = DockStyle.Fill, Text = "Baslik & SEO Optimizasyon Testi" };
        var titleAInput = new TextBox { Dock = DockStyle.Fill, Text = "Orijinal Urun Basligi" };
        var titleBInput = new TextBox { Dock = DockStyle.Fill, Text = "Yeni Yapay Zeka Destekli Etsy Basligi" };
        var tagsAInput = new TextBox { Dock = DockStyle.Fill, Text = "tag1, tag2, tag3" };
        var tagsBInput = new TextBox { Dock = DockStyle.Fill, Text = "etsy tag 1, etsy tag 2, long tail tag" };

        table.Controls.Add(new Label { Text = "Test Adı:", Dock = DockStyle.Fill }, 0, 0);
        table.Controls.Add(nameInput, 0, 1);
        table.Controls.Add(new Label { Text = "Varyant A Başlık (Orijinal):", Dock = DockStyle.Fill }, 0, 2);
        table.Controls.Add(titleAInput, 0, 3);
        table.Controls.Add(new Label { Text = "Varyant B Başlık (Yeni AI):", Dock = DockStyle.Fill }, 0, 4);
        table.Controls.Add(titleBInput, 0, 5);
        table.Controls.Add(new Label { Text = "Varyant B Tagler (Virgülle ayırın):", Dock = DockStyle.Fill }, 0, 6);
        table.Controls.Add(tagsBInput, 0, 7);

        var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var saveBtn = UiStyle.CreateButton("Testi Başlat");
        saveBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(titleAInput.Text) || string.IsNullOrWhiteSpace(titleBInput.Text))
            {
                MessageBox.Show(dlg, "Lütfen her iki varyant için de başlık girin.", "A/B Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var exp = new SaveAbTestExperiment(
                "listing-1",
                titleAInput.Text.Trim(),
                nameInput.Text.Trim(),
                titleAInput.Text.Trim(),
                titleBInput.Text.Trim(),
                tagsAInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                tagsBInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                "Orijinal aciklama",
                "Yeni AI aciklamasi",
                InitialViews: 100,
                InitialFavorites: 10,
                InitialSales: 2);

            await _abTestService.StartExperimentAsync(exp);
            dlg.DialogResult = DialogResult.OK;
        };

        var cancelBtn = UiStyle.CreateButton("İptal", isSecondary: true);
        cancelBtn.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;

        btnPanel.Controls.Add(saveBtn, 0, 0);
        btnPanel.Controls.Add(cancelBtn, 1, 0);
        table.Controls.Add(btnPanel, 0, 7);
        dlg.Controls.Add(table);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadDataAsync();
        }
    }

    private async Task OpenUpdateMetricsDialogAsync()
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            MessageBox.Show(this, "Lütfen güncellenecek A/B testini seçin.", "Metrik Güncelle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var exp = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (exp is null) return;

        using var dlg = new Form
        {
            Text = $"A/B Test Metriklerini Güncelle: {exp.ExperimentName}",
            Width = 460,
            Height = 360,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        UiStyle.ApplyTheme(dlg);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, Padding = new Padding(16) };
        for (var i = 0; i < 6; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, i % 2 == 0 ? 22 : 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        var viewsInput = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 1000000, Value = Math.Max(exp.AfterViews, exp.BeforeViews + 25) };
        var favsInput = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 100000, Value = Math.Max(exp.AfterFavorites, exp.BeforeFavorites + 5) };
        var salesInput = new NumericUpDown { Dock = DockStyle.Fill, Maximum = 10000, Value = Math.Max(exp.AfterSales, exp.BeforeSales + 1) };

        table.Controls.Add(new Label { Text = "Güncel Görüntülenme Sayısı:", Dock = DockStyle.Fill }, 0, 0);
        table.Controls.Add(viewsInput, 0, 1);
        table.Controls.Add(new Label { Text = "Güncel Favori Sayısı:", Dock = DockStyle.Fill }, 0, 2);
        table.Controls.Add(favsInput, 0, 3);
        table.Controls.Add(new Label { Text = "Güncel Satış Adedi:", Dock = DockStyle.Fill }, 0, 4);
        table.Controls.Add(salesInput, 0, 5);

        var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var saveBtn = UiStyle.CreateButton("Güncelle & Kaydet");
        saveBtn.Click += async (_, _) =>
        {
            var update = new UpdateAbTestMetrics(
                exp.Id,
                (int)viewsInput.Value,
                (int)favsInput.Value,
                (int)salesInput.Value,
                CompleteExperiment: true);

            await _abTestService.UpdateMetricsAsync(update);
            dlg.DialogResult = DialogResult.OK;
        };

        var cancelBtn = UiStyle.CreateButton("İptal", isSecondary: true);
        cancelBtn.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;

        btnPanel.Controls.Add(saveBtn, 0, 0);
        btnPanel.Controls.Add(cancelBtn, 1, 0);
        table.Controls.Add(btnPanel, 0, 6);
        dlg.Controls.Add(table);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadDataAsync();
        }
    }

    private sealed class AbTestGridRow(ListingAbTestExperiment item)
    {
        public long Id => item.Id;
        public string ExperimentName => item.ExperimentName;
        public string ListingTitle => item.ListingTitle;
        public string Status => item.Status.ToString();
        public string SalesDisplay => $"{item.BeforeSales} ➔ {item.AfterSales}";
        public string Created => item.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    }
}
