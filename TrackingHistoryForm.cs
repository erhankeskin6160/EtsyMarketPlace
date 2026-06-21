namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Text;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;

internal sealed class TrackingHistoryForm : Form
{
    private readonly TrackingService _trackingService;
    private readonly DataGridView _itemsGrid = new();
    private readonly DataGridView _snapshotsGrid = new();
    private readonly TextBox _changeTextBox = new();
    private readonly Label _statusLabel = new();
    private List<TrackingHistory> _histories = [];

    public TrackingHistoryForm(TrackingService trackingService)
    {
        _trackingService = trackingService;
        BuildLayout();
        Shown += async (_, _) => await LoadHistoriesAsync();
    }

    private TrackingHistoryRow? SelectedHistory => _itemsGrid.CurrentRow?.DataBoundItem as TrackingHistoryRow;

    private void BuildLayout()
    {
        Text = "Takip Merkezi ve Gecmis Veriler";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Takip Merkezi ve Gecmis Veriler",
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Takip kayitlari yukleniyor";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, Padding = new Padding(0, 4, 0, 8) };
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 1; index < 6; index++) commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        commands.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        var refresh = CreateButton("Yenile");
        refresh.Click += async (_, _) => await LoadHistoriesAsync();
        commands.Controls.Add(refresh, 1, 0);
        var open = CreateButton("Etsy'de Ac");
        open.Click += (_, _) => OpenUrl(SelectedHistory?.Url);
        commands.Controls.Add(open, 2, 0);
        var export = CreateButton("CSV Aktar");
        export.Click += (_, _) => ExportCsv();
        commands.Controls.Add(export, 3, 0);
        var delete = CreateButton("Takibi Sil");
        delete.BackColor = Color.FromArgb(180, 58, 58);
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        commands.Controls.Add(delete, 4, 0);
        var close = CreateButton("Geri Don");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        commands.Controls.Add(close, 5, 0);
        root.Controls.Add(commands, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 545, Panel1MinSize = 420, Panel2MinSize = 520 };
        ConfigureItemsGrid();
        split.Panel1.Controls.Add(_itemsGrid);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        ConfigureSnapshotsGrid();
        right.Controls.Add(_snapshotsGrid, 0, 0);
        _changeTextBox.Dock = DockStyle.Fill;
        _changeTextBox.Multiline = true;
        _changeTextBox.ReadOnly = true;
        _changeTextBox.ScrollBars = ScrollBars.Vertical;
        _changeTextBox.BackColor = Color.White;
        _changeTextBox.Font = new Font("Segoe UI", 10.5F);
        right.Controls.Add(_changeTextBox, 0, 1);
        split.Panel2.Controls.Add(right);
        root.Controls.Add(split, 0, 2);
    }

    private void ConfigureItemsGrid()
    {
        ConfigureBaseGrid(_itemsGrid);
        _itemsGrid.AutoGenerateColumns = false;
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tur", DataPropertyName = nameof(TrackingHistoryRow.TypeDisplay), Width = 85 });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Takip edilen", DataPropertyName = nameof(TrackingHistoryRow.DisplayName), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kayit", DataPropertyName = nameof(TrackingHistoryRow.SnapshotCount), Width = 65 });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Son guncelleme", DataPropertyName = nameof(TrackingHistoryRow.LastCapturedDisplay), Width = 140 });
        _itemsGrid.SelectionChanged += (_, _) => ShowSelectedHistory();
    }

    private void ConfigureSnapshotsGrid()
    {
        ConfigureBaseGrid(_snapshotsGrid);
        _snapshotsGrid.AutoGenerateColumns = false;
        AddSnapshotColumn("Tarih", nameof(TrackingSnapshotRow.CapturedDisplay), 135);
        AddSnapshotColumn("Fiyat", nameof(TrackingSnapshotRow.PriceDisplay), 95);
        AddSnapshotColumn("Favori", nameof(TrackingSnapshotRow.FavoritesDisplay), 75);
        AddSnapshotColumn("Goruntulenme", nameof(TrackingSnapshotRow.ViewsDisplay), 105);
        AddSnapshotColumn("Magaza satisi", nameof(TrackingSnapshotRow.ShopSalesDisplay), 105);
        AddSnapshotColumn("SEO", nameof(TrackingSnapshotRow.SeoDisplay), 60);
        AddSnapshotColumn("Pazar", nameof(TrackingSnapshotRow.MarketDisplay), 65);
        AddSnapshotColumn("Talep", nameof(TrackingSnapshotRow.DemandDisplay), 65);
        AddSnapshotColumn("Rekabet", nameof(TrackingSnapshotRow.CompetitionDisplay), 70);
        AddSnapshotColumn("Firsat", nameof(TrackingSnapshotRow.OpportunityDisplay), 65);
    }

    private async Task LoadHistoriesAsync()
    {
        try
        {
            _statusLabel.Text = "Yerel SQLite verileri okunuyor...";
            _histories = (await _trackingService.GetHistoriesAsync()).ToList();
            _itemsGrid.DataSource = _histories.Select(item => new TrackingHistoryRow(item)).ToList();
            _statusLabel.Text = $"{_histories.Count} takip kaydi | {DateTime.Now:dd.MM.yyyy HH:mm}";
            ShowSelectedHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Takip Merkezi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Takip kayitlari okunamadi";
        }
    }

    private void ShowSelectedHistory()
    {
        var selected = SelectedHistory;
        if (selected is null)
        {
            _snapshotsGrid.DataSource = null;
            _changeTextBox.Text = "Takip kaydi secilmedi.";
            return;
        }

        _snapshotsGrid.DataSource = selected.History.Snapshots.Select(item => new TrackingSnapshotRow(item)).ToList();
        _changeTextBox.Text = BuildChangeSummary(selected.History);
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = SelectedHistory;
        if (selected is null) return;
        if (MessageBox.Show(this, $"'{selected.DisplayName}' takibini ve tum gecmisini silmek istiyor musunuz?", "Takibi Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await _trackingService.DeleteAsync(selected.Id);
        await LoadHistoriesAsync();
    }

    private void ExportCsv()
    {
        if (_histories.Count == 0) return;
        using var dialog = new SaveFileDialog { Filter = "CSV dosyasi (*.csv)|*.csv", FileName = $"etsy-takip-gecmisi-{DateTime.Now:yyyy-MM-dd}.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var builder = new StringBuilder();
        builder.AppendLine("Tur,TakipEdilen,Tarih,Fiyat,Favori,Goruntulenme,MagazaSatisi,Yorum,SEO,Pazar,Talep,Rekabet,Firsat,Sonuc,Orneklem,URL");
        foreach (var history in _histories)
        foreach (var item in history.Snapshots)
        {
            builder.AppendLine(string.Join(",", Csv(history.Item.EntityType.ToString()), Csv(history.Item.DisplayName), Csv(item.CapturedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm")), Csv(item.Price.HasValue ? $"{item.CurrencyCode} {item.Price:0.##}" : ""), item.Favorites, item.Views, item.ShopSales, item.ReviewCount, item.SeoScore, item.MarketScore, item.DemandScore, item.CompetitionScore, item.OpportunityScore, item.ResultCount, item.SampleSize, Csv(history.Item.Url)));
        }
        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "Takip gecmisi CSV dosyasi kaydedildi";
    }

    private static string BuildChangeSummary(TrackingHistory history)
    {
        var latest = history.Latest;
        if (latest is null) return "Snapshot bulunamadi.";
        var previous = history.Previous;
        var builder = new StringBuilder();
        builder.AppendLine($"TAKIP: {history.Item.DisplayName}");
        builder.AppendLine($"TUR: {history.Item.EntityType} | SNAPSHOT: {history.Snapshots.Count}");
        builder.AppendLine($"SON KAYIT: {latest.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}");
        if (previous is null)
        {
            builder.AppendLine();
            builder.AppendLine("Degisim hesaplamak icin ayni kaydi daha sonra tekrar takip edin.");
            return builder.ToString();
        }

        builder.AppendLine($"ONCEKI KAYIT: {previous.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}");
        builder.AppendLine();
        AppendDifference(builder, "Fiyat", latest.Price, previous.Price);
        AppendDifference(builder, "Favori", latest.Favorites, previous.Favorites);
        AppendDifference(builder, "Goruntulenme", latest.Views, previous.Views);
        AppendDifference(builder, "Magaza satisi", latest.ShopSales, previous.ShopSales);
        AppendDifference(builder, "Talep", latest.DemandScore, previous.DemandScore);
        AppendDifference(builder, "Rekabet", latest.CompetitionScore, previous.CompetitionScore);
        AppendDifference(builder, "Firsat", latest.OpportunityScore, previous.OpportunityScore);
        return builder.ToString();
    }

    private static void AppendDifference(StringBuilder builder, string name, decimal? latest, decimal? previous)
    {
        if (latest.HasValue && previous.HasValue) builder.AppendLine($"{name}: {previous:0.##} -> {latest:0.##} ({latest - previous:+0.##;-0.##;0})");
    }

    private static void AppendDifference(StringBuilder builder, string name, int? latest, int? previous)
    {
        if (latest.HasValue && previous.HasValue) builder.AppendLine($"{name}: {previous:N0} -> {latest:N0} ({latest - previous:+#;-#;0})");
    }

    private static void ConfigureBaseGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
    }

    private void AddSnapshotColumn(string header, string property, int width) => _snapshotsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, Width = width });
    private static Button CreateButton(string text)
    {
        var button = new Button { Dock = DockStyle.Fill, Text = text, BackColor = Color.FromArgb(32, 97, 165), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(6, 2, 0, 2) };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
    private static void OpenUrl(string? url) { if (!string.IsNullOrWhiteSpace(url)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed class TrackingHistoryRow(TrackingHistory history)
    {
        public TrackingHistory History => history;
        public long Id => history.Item.Id;
        public string TypeDisplay => history.Item.EntityType switch { TrackingEntityType.Listing => "Urun", TrackingEntityType.Shop => "Magaza", _ => "Kelime" };
        public string DisplayName => history.Item.DisplayName;
        public string Url => history.Item.Url;
        public int SnapshotCount => history.Snapshots.Count;
        public string LastCapturedDisplay => history.Latest?.CapturedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm") ?? "Veri yok";
    }

    private sealed class TrackingSnapshotRow(TrackingSnapshot snapshot)
    {
        public string CapturedDisplay => snapshot.CapturedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
        public string PriceDisplay => snapshot.Price.HasValue ? $"{snapshot.CurrencyCode} {snapshot.Price:0.##}" : "-";
        public string FavoritesDisplay => Format(snapshot.Favorites);
        public string ViewsDisplay => Format(snapshot.Views);
        public string ShopSalesDisplay => Format(snapshot.ShopSales);
        public string SeoDisplay => Format(snapshot.SeoScore);
        public string MarketDisplay => Format(snapshot.MarketScore);
        public string DemandDisplay => Format(snapshot.DemandScore);
        public string CompetitionDisplay => Format(snapshot.CompetitionScore);
        public string OpportunityDisplay => Format(snapshot.OpportunityScore);
        private static string Format(int? value) => value.HasValue ? value.Value.ToString("N0") : "-";
    }
}
