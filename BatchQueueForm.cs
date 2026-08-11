namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.BatchQueue;

internal sealed class BatchQueueForm : Form
{
    private readonly BatchQueueProcessorService _processorService;
    private readonly DataGridView _grid = new();
    private readonly BindingSource _bindingSource = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();

    private readonly Label _totalBadge = new();
    private readonly Label _pendingBadge = new();
    private readonly Label _completedBadge = new();
    private readonly Label _riskBadge = new();
    private readonly Label _avgScoreBadge = new();

    private readonly TextBox _optTitleTextBox = new() { ReadOnly = true, Multiline = true };
    private readonly TextBox _optTagsTextBox = new() { ReadOnly = true, Multiline = true };
    private readonly ListBox _issuesListBox = new();

    private CancellationTokenSource? _cts;
    private List<BatchQueueItem> _items = [];

    public BatchQueueForm(BatchQueueProcessorService processorService)
    {
        _processorService = processorService;
        BuildLayout();
        Shown += async (_, _) => await LoadQueueAsync();
    }

    private void BuildLayout()
    {
        Text = "Toplu Ürün Optimizasyonu ve Yayınlama Sırası (Batch Queue Manager)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // KPI Summary Badges
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Toolbar + Progress
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main Content (Grid + Details)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Bottom Bar
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));

        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Toplu Ürün Optimizasyonu Sırası (Batch Queue)",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Kuyruk durumu: Hazır";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        // KPI Badges Row
        var kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        for (var i = 0; i < 5; i++) kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        AddKpiBadge(kpiTable, 0, "Toplam Ürün", _totalBadge, UiStyle.SecondaryColor);
        AddKpiBadge(kpiTable, 1, "Bekleyen", _pendingBadge, UiStyle.AccentColor);
        AddKpiBadge(kpiTable, 2, "Tamamlanan", _completedBadge, UiStyle.SuccessColor);
        AddKpiBadge(kpiTable, 3, "Telif Riski", _riskBadge, UiStyle.DangerColor);
        AddKpiBadge(kpiTable, 4, "Ortalama Puan", _avgScoreBadge, UiStyle.PrimaryColor);
        root.Controls.Add(kpiTable, 0, 1);

        // Toolbar + Progress Bar
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        for (var i = 1; i < 6; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        toolbar.Controls.Add(_progressBar, 0, 0);

        var addItemsBtn = UiStyle.CreateButton("Toplu Ürün Ekle");
        addItemsBtn.Click += async (_, _) => await OpenAddBatchItemsDialogAsync();
        toolbar.Controls.Add(addItemsBtn, 1, 0);

        var startBatchBtn = UiStyle.CreateButton("Optimizasyonu Başlat");
        startBatchBtn.Click += async (_, _) => await StartBatchProcessingAsync();
        toolbar.Controls.Add(startBatchBtn, 2, 0);

        var stopBatchBtn = UiStyle.CreateButton("Durdur", isSecondary: true);
        stopBatchBtn.Click += (_, _) => StopBatchProcessing();
        toolbar.Controls.Add(stopBatchBtn, 3, 0);

        var clearBtn = UiStyle.CreateButton("Temizle", isSecondary: true);
        clearBtn.Click += async (_, _) => await ClearCompletedAsync();
        toolbar.Controls.Add(clearBtn, 4, 0);

        var refreshBtn = UiStyle.CreateButton("Yenile");
        refreshBtn.Click += async (_, _) => await LoadQueueAsync();
        toolbar.Controls.Add(refreshBtn, 5, 0);
        root.Controls.Add(toolbar, 0, 2);

        // Main Content (Grid left 52%, Details right 48%)
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        ConfigureGrid();
        content.Controls.Add(_grid, 0, 0);
        content.Controls.Add(BuildDetailPanel(), 1, 0);
        root.Controls.Add(content, 0, 3);

        // Bottom Bar
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        bottom.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        bottom.Controls.Add(closeBtn, 1, 0);
        root.Controls.Add(bottom, 0, 4);

        _bindingSource.CurrentChanged += (_, _) => DisplaySelectedItemDetails();
    }

    private Control BuildDetailPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Optimize Edilmiş Ürün Önizleme ve Detaylar",
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(8) };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Optimize Edilmiş Başlık", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 0);
        _optTitleTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_optTitleTextBox, 0, 1);

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Optimize Edilmiş Tagler", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 2);
        _optTagsTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_optTagsTextBox, 0, 3);

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Kalite Eksikleri / Risk Uyarıları", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 4);
        _issuesListBox.Dock = DockStyle.Fill;
        _issuesListBox.Font = new Font("Segoe UI", 8.5F);
        table.Controls.Add(_issuesListBox, 0, 5);

        group.Controls.Add(table);
        return group;
    }

    private static void AddKpiBadge(TableLayoutPanel parent, int col, string title, Label badge, Color color)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White, Margin = new Padding(col == 0 ? 0 : 3, 2, col == 4 ? 0 : 3, 2), Padding = new Padding(6, 2, 6, 2), CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI", 8F), ForeColor = UiStyle.TextMuted }, 0, 0);
        badge.Dock = DockStyle.Fill;
        badge.Text = "0";
        badge.Font = new Font("Segoe UI Semibold", 13F);
        badge.ForeColor = color;
        badge.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(badge, 0, 1);

        parent.Controls.Add(panel, col, 0);
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(BatchGridRow.Id), Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Orijinal Başlık", DataPropertyName = nameof(BatchGridRow.OriginalTitle), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Anahtar Kelime", DataPropertyName = nameof(BatchGridRow.TargetKeyword), Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Durum", DataPropertyName = nameof(BatchGridRow.Status), Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Puan", DataPropertyName = nameof(BatchGridRow.Score), Width = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tarih", DataPropertyName = nameof(BatchGridRow.Created), Width = 110 });
        _grid.DataSource = _bindingSource;
    }

    private async Task LoadQueueAsync()
    {
        try
        {
            _statusLabel.Text = "Kuyruk verileri yükleniyor...";
            _items = (await _processorService.GetAllAsync()).ToList();
            _bindingSource.DataSource = _items.Select(i => new BatchGridRow(i)).ToList();

            var summary = await _processorService.GetSummaryAsync();
            UpdateSummaryBadges(summary);

            if (_items.Count > 0)
            {
                DisplaySelectedItemDetails();
                _statusLabel.Text = summary.StatusMessage;
            }
            else
            {
                ResetDetailDisplay();
                _statusLabel.Text = "Kuyruk boş. 'Toplu Ürün Ekle' butonuna basarak ürün ekleyin.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Toplu Optimizasyon Sırası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateSummaryBadges(BatchQueueSummary summary)
    {
        _totalBadge.Text = summary.TotalItems.ToString();
        _pendingBadge.Text = summary.PendingItems.ToString();
        _completedBadge.Text = summary.CompletedItems.ToString();
        _riskBadge.Text = summary.RiskWarningItems.ToString();
        _avgScoreBadge.Text = summary.AverageScore > 0 ? $"{summary.AverageScore}/100" : "-";
        _progressBar.Value = Math.Clamp((int)summary.ProgressPercent, 0, 100);
        _statusLabel.Text = summary.StatusMessage;
    }

    private void DisplaySelectedItemDetails()
    {
        if (_bindingSource.Current is not BatchGridRow row)
        {
            ResetDetailDisplay();
            return;
        }

        var item = _items.FirstOrDefault(i => i.Id == row.Id);
        if (item is null)
        {
            ResetDetailDisplay();
            return;
        }

        _optTitleTextBox.Text = !string.IsNullOrWhiteSpace(item.OptimizedTitle) ? item.OptimizedTitle : item.OriginalTitle;
        _optTagsTextBox.Text = item.OptimizedTags.Count > 0 ? string.Join(", ", item.OptimizedTags) : string.Join(", ", item.OriginalTags);

        _issuesListBox.Items.Clear();

        if (item.RiskWarnings.Count > 0)
        {
            foreach (var risk in item.RiskWarnings)
            {
                _issuesListBox.Items.Add($"❌ MARKARİSKİ: {risk}");
            }
        }

        if (item.Issues.Count > 0)
        {
            foreach (var issue in item.Issues)
            {
                _issuesListBox.Items.Add($"• {issue}");
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
        {
            _issuesListBox.Items.Add($"❌ HATA: {item.ErrorMessage}");
        }

        if (_issuesListBox.Items.Count == 0)
        {
            _issuesListBox.Items.Add("✅ Ürün Etsy standartlarına tam uygun olarak optimize edildi.");
        }
    }

    private void ResetDetailDisplay()
    {
        _optTitleTextBox.Clear();
        _optTagsTextBox.Clear();
        _issuesListBox.Items.Clear();
    }

    private async Task StartBatchProcessingAsync()
    {
        if (_cts is not null) return;

        _cts = new CancellationTokenSource();
        var progress = new Progress<BatchQueueSummary>(summary =>
        {
            UpdateSummaryBadges(summary);
            LoadQueueAsync().GetAwaiter().GetResult();
        });

        try
        {
            _statusLabel.Text = "Toplu optimizasyon işlemi başlatıldı...";
            await _processorService.ProcessQueueAsync(progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "İşlem durduruldu.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Toplu İşleme Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            await LoadQueueAsync();
        }
    }

    private void StopBatchProcessing()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _statusLabel.Text = "Durdurma isteği gönderildi...";
    }

    private async Task ClearCompletedAsync()
    {
        await _processorService.ClearCompletedAsync();
        await LoadQueueAsync();
    }

    private async Task OpenAddBatchItemsDialogAsync()
    {
        using var dlg = new Form
        {
            Text = "Toplu Ürün Ekle",
            Width = 560,
            Height = 440,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        UiStyle.ApplyTheme(dlg);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(14) };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        var keywordInput = new TextBox { Dock = DockStyle.Fill, Text = "dragon bust" };
        var titlesInput = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        titlesInput.Text = "Resin Dragon Bust Fantasy Sculpture\nHand Painted Dragon Statue Shelf Decor\nTabletop Gaming Dragon Figurine Collectible";

        table.Controls.Add(new Label { Text = "Ortak Hedef Anahtar Kelime:", Dock = DockStyle.Fill }, 0, 0);
        table.Controls.Add(keywordInput, 0, 1);
        table.Controls.Add(new Label { Text = "Ürün Başlıkları (Her satıra 1 ürün yazın):", Dock = DockStyle.Fill }, 0, 2);
        table.Controls.Add(titlesInput, 0, 3);

        var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var saveBtn = UiStyle.CreateButton("Kuyruğa Ekle");
        saveBtn.Click += async (_, _) =>
        {
            var lines = titlesInput.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show(dlg, "Lütfen en az bir ürün başlığı yazın.", "Toplu Ekle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var itemsToSave = lines.Select((title, idx) => new SaveBatchQueueItem(
                $"batch-item-{idx + 1}",
                title,
                $"Auto generated description for {title}",
                [keywordInput.Text.Trim(), "handcrafted", "decor"],
                keywordInput.Text.Trim(),
                "Sculptures & Figurines")).ToList();

            await _processorService.EnqueueAsync(itemsToSave);
            dlg.DialogResult = DialogResult.OK;
        };

        var cancelBtn = UiStyle.CreateButton("İptal", isSecondary: true);
        cancelBtn.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;

        btnPanel.Controls.Add(saveBtn, 0, 0);
        btnPanel.Controls.Add(cancelBtn, 1, 0);
        table.Controls.Add(btnPanel, 0, 4);

        dlg.Controls.Add(table);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadQueueAsync();
        }
    }

    private sealed class BatchGridRow(BatchQueueItem item)
    {
        public long Id => item.Id;
        public string OriginalTitle => item.OriginalTitle;
        public string TargetKeyword => item.TargetKeyword;
        public string Status => item.Status.ToString();
        public string Score => item.OverallScore > 0 ? $"{item.OverallScore}/100" : "-";
        public string Created => item.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    }
}
