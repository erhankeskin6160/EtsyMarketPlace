namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.ListingOptimization;

internal sealed class ListingOptimizationHistoryForm(ListingOptimizationHistoryService historyService) : Form
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _detailTextBox = new();
    private readonly BindingSource _bindingSource = new();

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        await LoadHistoryAsync();
    }

    private void BuildLayout()
    {
        Text = "Listing Optimizasyon Gecmisi";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);
        Padding = new Padding(18);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Listing Optimizasyon Gecmisi",
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 1);

        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.BackColor = Color.White;
        root.Controls.Add(_detailTextBox, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        var refresh = CreateButton("Yenile");
        refresh.Click += async (_, _) => await LoadHistoryAsync();
        footer.Controls.Add(refresh, 1, 0);
        var delete = CreateButton("Sil");
        delete.BackColor = Color.FromArgb(180, 58, 58);
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        footer.Controls.Add(delete, 2, 0);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close, 3, 0);
        root.Controls.Add(footer, 0, 3);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => UpdateDetail();
        AddColumn("Tarih", nameof(HistoryRow.CreatedAt), 145);
        AddColumn("Listing", nameof(HistoryRow.ListingTitle), 330, fill: true);
        AddColumn("Kelime", nameof(HistoryRow.TargetKeyword), 160);
        AddColumn("Once", nameof(HistoryRow.CurrentSeoScore), 70);
        AddColumn("Sonra", nameof(HistoryRow.OptimizedSeoScore), 70);
        AddColumn("Artis", nameof(HistoryRow.ScoreDelta), 70);
        AddColumn("Risk", nameof(HistoryRow.RiskCount), 70);
    }

    private async Task LoadHistoryAsync()
    {
        var entries = await historyService.GetRecentAsync();
        _bindingSource.DataSource = entries.Select(item => new HistoryRow(item)).ToList();
        UpdateDetail();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_bindingSource.Current is not HistoryRow row) return;
        await historyService.DeleteAsync(row.Id);
        await LoadHistoryAsync();
    }

    private void UpdateDetail()
    {
        if (_bindingSource.Current is not HistoryRow row)
        {
            _detailTextBox.Text = "Kayit secilmedi.";
            return;
        }

        _detailTextBox.Text =
            $"LISTING{Environment.NewLine}{row.ListingTitle}{Environment.NewLine}{Environment.NewLine}" +
            $"HEDEF KELIME{Environment.NewLine}{row.TargetKeyword}{Environment.NewLine}{Environment.NewLine}" +
            $"SEO{Environment.NewLine}{row.CurrentSeoScore}/100 -> {row.OptimizedSeoScore}/100 ({row.ScoreDelta:+#;-#;0}){Environment.NewLine}{Environment.NewLine}" +
            $"ONERILEN BASLIK{Environment.NewLine}{row.SuggestedTitle}{Environment.NewLine}{Environment.NewLine}" +
            $"ONERILEN TAGLER{Environment.NewLine}{string.Join(", ", row.Entry.SuggestedTags)}{Environment.NewLine}{Environment.NewLine}" +
            $"RISKLER{Environment.NewLine}{string.Join(Environment.NewLine, row.Entry.RiskWarnings.DefaultIfEmpty("Risk uyarisi yok."))}{Environment.NewLine}{Environment.NewLine}" +
            $"ACIKLAMA TASLAGI{Environment.NewLine}{row.Entry.DescriptionDraft}";
    }

    private void AddColumn(string header, string property, int width, bool fill = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });
    }

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
            Margin = new Padding(6, 3, 0, 3),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private sealed class HistoryRow(ListingOptimizationHistoryEntry entry)
    {
        public ListingOptimizationHistoryEntry Entry { get; } = entry;
        public long Id => Entry.Id;
        public string CreatedAt => Entry.CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public string ListingTitle => Entry.ListingTitle;
        public string TargetKeyword => Entry.TargetKeyword;
        public int CurrentSeoScore => Entry.CurrentSeoScore;
        public int OptimizedSeoScore => Entry.OptimizedSeoScore;
        public int ScoreDelta => Entry.OptimizedSeoScore - Entry.CurrentSeoScore;
        public int RiskCount => Entry.RiskWarnings.Count;
        public string SuggestedTitle => Entry.SuggestedTitle;
    }
}
