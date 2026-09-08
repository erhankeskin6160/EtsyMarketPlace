namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.BatchQueue;
using SimilarProductsWinForms.Services;

internal sealed class BatchQueueForm : Form
{
    private readonly BatchQueueProcessorService _processorService;
    private readonly AbTestService _abTestService;
    private readonly EtsyApiClient _apiClient = new();
    private readonly DataGridView _grid = new();
    private readonly BindingSource _bindingSource = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();

    private readonly Label _totalBadge = new();
    private readonly Label _pendingBadge = new();
    private readonly Label _completedBadge = new();
    private readonly Label _syncedBadge = new();
    private readonly Label _riskBadge = new();
    private readonly Label _avgScoreBadge = new();

    private readonly TextBox _optTitleTextBox = new() { ReadOnly = true, Multiline = true };
    private readonly TextBox _optTagsTextBox = new() { ReadOnly = true, Multiline = true };
    private readonly Label _syncInfoLabel = new();
    private readonly ListBox _issuesListBox = new();

    private CancellationTokenSource? _cts;
    private List<BatchQueueItem> _items = [];

    public BatchQueueForm(BatchQueueProcessorService processorService, AbTestService? abTestService = null)
    {
        _processorService = processorService;
        _abTestService = abTestService ?? CreateDefaultAbTestService();
        BuildLayout();
        Shown += async (_, _) => await LoadQueueAsync();
    }

    private static AbTestService CreateDefaultAbTestService()
    {
        var dbPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "etsy_marketplace.db");
        return new AbTestService(new EtsyMarketPlace.Infrastructure.AbTesting.SqliteAbTestRepository(dbPath));
    }

    private void BuildLayout()
    {
        Text = "Toplu Ürün Optimizasyonu ve Canlı Etsy Senkronizasyon Sırası";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1300, 800);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); // KPI Summary Badges (6 badges)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Toolbar + Progress
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main Content (Grid + Details)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Bottom Bar
        Controls.Add(root);

        // 1. Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));

        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Toplu İşlem Kuyruğu & Canlı Etsy Senkronizasyonu",
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

        // 2. KPI Badges Row (6 cards)
        var kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        for (var i = 0; i < 6; i++) kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));

        AddKpiBadge(kpiTable, 0, "Toplam Ürün", _totalBadge, UiStyle.TextDark);
        AddKpiBadge(kpiTable, 1, "Bekleyen", _pendingBadge, UiStyle.AccentColor);
        AddKpiBadge(kpiTable, 2, "AI Tamamlanan", _completedBadge, UiStyle.PrimaryColor);
        AddKpiBadge(kpiTable, 3, "Etsy Canlıda", _syncedBadge, UiStyle.SuccessColor);
        AddKpiBadge(kpiTable, 4, "Telif Riski", _riskBadge, UiStyle.DangerColor);
        AddKpiBadge(kpiTable, 5, "Ort. Puan", _avgScoreBadge, UiStyle.WarningColor);
        root.Controls.Add(kpiTable, 0, 1);

        // 3. Toolbar + Progress Bar
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f)); // Progress bar
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f)); // Toplu Ekle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11f)); // Optimize Et
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 7f));  // Durdur
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f)); // ⚡ Etsy'ye Canlı Uygula
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f)); // ↩️ Rollback
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14f)); // 🧪 A/B Testine Gönder
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 6f));  // Temizle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 6f));  // Yenile

        var progressContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 14, 4, 14) };
        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        progressContainer.Controls.Add(_progressBar);
        toolbar.Controls.Add(progressContainer, 0, 0);

        var addItemsBtn = UiStyle.CreateButton("Toplu Ürün Ekle");
        addItemsBtn.Click += async (_, _) => await OpenAddBatchItemsDialogAsync();
        toolbar.Controls.Add(addItemsBtn, 1, 0);

        var startBatchBtn = UiStyle.CreateButton("AI Optimize Et");
        startBatchBtn.Click += async (_, _) => await StartBatchProcessingAsync();
        toolbar.Controls.Add(startBatchBtn, 2, 0);

        var stopBatchBtn = UiStyle.CreateButton("Durdur", isSecondary: true);
        stopBatchBtn.Click += (_, _) => StopBatchProcessing();
        toolbar.Controls.Add(stopBatchBtn, 3, 0);

        // ⚡ Etsy'ye Canlı Uygula Button
        var syncEtsyBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "⚡ Canlıya Uygula",
            NormalColor = UiStyle.EtsyColor,
            HoverColor = UiStyle.EtsyHover,
            ForeColor = Color.White,
            Margin = new Padding(4, 2, 4, 2),
        };
        syncEtsyBtn.Click += async (_, _) => await SyncSelectedToEtsyAsync();
        toolbar.Controls.Add(syncEtsyBtn, 4, 0);

        // ↩️ Orijinal Haline Geri Al Button
        var rollbackBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "↩️ Orijinale Geri Al",
            NormalColor = UiStyle.SecondaryColor,
            HoverColor = UiStyle.DangerColor,
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(4, 2, 4, 2),
        };
        rollbackBtn.Click += async (_, _) => await RollbackSelectedAsync();
        toolbar.Controls.Add(rollbackBtn, 5, 0);

        // 🧪 A/B Testine Gönder Button
        var abTestBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🧪 A/B Testine Gönder",
            NormalColor = UiStyle.AiColor,
            HoverColor = UiStyle.AiHover,
            ForeColor = Color.White,
            Margin = new Padding(4, 2, 4, 2),
        };
        abTestBtn.Click += async (_, _) => await SendSelectedToAbTestAsync();
        toolbar.Controls.Add(abTestBtn, 6, 0);

        var clearBtn = UiStyle.CreateButton("Temizle", isSecondary: true);
        clearBtn.Click += async (_, _) => await ClearCompletedAsync();
        toolbar.Controls.Add(clearBtn, 7, 0);

        var refreshBtn = UiStyle.CreateButton("Yenile", isSecondary: true);
        refreshBtn.Click += async (_, _) => await LoadQueueAsync();
        toolbar.Controls.Add(refreshBtn, 8, 0);
        root.Controls.Add(toolbar, 0, 2);

        // 4. Main Content (Grid left 53%, Details right 47%)
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));

        ConfigureGrid();
        content.Controls.Add(_grid, 0, 0);
        content.Controls.Add(BuildDetailPanel(), 1, 0);
        root.Controls.Add(content, 0, 3);

        // 5. Bottom Bar
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        var tipLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "💡 İpucu: Birden fazla ürün seçmek için Ctrl veya Shift tuşuna basılı tutarak satırlara tıklayabilirsiniz.",
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };
        bottom.Controls.Add(tipLabel, 0, 0);

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
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, Padding = new Padding(8) };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // Header 1
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 35));  // Title
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // Header 2
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));  // Tags
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Sync Info Banner
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // Header 3
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  // Issues List
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Optimize Edilmiş Başlık", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 0);
        _optTitleTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_optTitleTextBox, 0, 1);

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Optimize Edilmiş Tagler", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 2);
        _optTagsTextBox.Dock = DockStyle.Fill;
        table.Controls.Add(_optTagsTextBox, 0, 3);

        _syncInfoLabel.Dock = DockStyle.Fill;
        _syncInfoLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _syncInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
        _syncInfoLabel.Text = "Etsy Durumu: -";
        table.Controls.Add(_syncInfoLabel, 0, 4);

        table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Kalite Eksikleri / Risk Uyarıları", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, 0, 5);
        _issuesListBox.Dock = DockStyle.Fill;
        _issuesListBox.Font = new Font("Segoe UI", 8.5F);
        table.Controls.Add(_issuesListBox, 0, 6);

        group.Controls.Add(table);
        return group;
    }

    private static void AddKpiBadge(TableLayoutPanel parent, int col, string title, Label badge, Color color)
    {
        var card = new Controls.ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(col == 0 ? 0 : 3, 3, col == 5 ? 0 : 3, 3),
            Padding = new Padding(10, 8, 10, 8),
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title.ToUpperInvariant(),
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        layout.Controls.Add(titleLabel, 0, 0);

        badge.Dock = DockStyle.Fill;
        badge.Text = "0";
        badge.Font = UiStyle.KpiValueFont;
        badge.ForeColor = color;
        badge.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(badge, 0, 1);

        card.Controls.Add(layout);
        parent.Controls.Add(card, col, 0);
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;

        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(BatchGridRow.Id), Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Listing ID", DataPropertyName = nameof(BatchGridRow.ListingId), Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Orijinal Başlık", DataPropertyName = nameof(BatchGridRow.OriginalTitle), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Anahtar Kelime", DataPropertyName = nameof(BatchGridRow.TargetKeyword), Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kuyruk", DataPropertyName = nameof(BatchGridRow.Status), Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Etsy Durumu", DataPropertyName = nameof(BatchGridRow.SyncStatus), Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "A/B Test", DataPropertyName = nameof(BatchGridRow.AbTestInfo), Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Puan", DataPropertyName = nameof(BatchGridRow.Score), Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kayıt Tarihi", DataPropertyName = nameof(BatchGridRow.Created), Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Canlı Senkron", DataPropertyName = nameof(BatchGridRow.SyncedAt), Width = 105 });

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
        
        var syncedCount = _items.Count(i => i.IsSyncedToEtsy || i.Status == BatchQueueItemStatus.SyncedToEtsy);
        _syncedBadge.Text = syncedCount.ToString();

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

        // Sync Info Label
        if (item.IsSyncedToEtsy || item.Status == BatchQueueItemStatus.SyncedToEtsy)
        {
            _syncInfoLabel.ForeColor = UiStyle.SuccessColor;
            var timeStr = item.SyncedAt.HasValue ? item.SyncedAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm") : "-";
            _syncInfoLabel.Text = $"🟢 Etsy Canlıda Güncel (Senkronize Edildi: {timeStr})";
        }
        else if (item.Status == BatchQueueItemStatus.RolledBack)
        {
            _syncInfoLabel.ForeColor = UiStyle.WarningColor;
            _syncInfoLabel.Text = "↩️ Orijinal Haline Geri Alındı (Rollback Yapıldı)";
        }
        else
        {
            _syncInfoLabel.ForeColor = UiStyle.TextMuted;
            _syncInfoLabel.Text = "⚪ Henüz Etsy Canlı Mağazasına Gönderilmedi";
        }

        _issuesListBox.Items.Clear();

        if (item.RiskWarnings.Count > 0)
        {
            foreach (var risk in item.RiskWarnings)
            {
                _issuesListBox.Items.Add($"❌ MARKA RİSKİ: {risk}");
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
        _syncInfoLabel.Text = "Etsy Durumu: -";
        _syncInfoLabel.ForeColor = UiStyle.TextMuted;
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
            _statusLabel.Text = "Toplu AI optimizasyon işlemi başlatıldı...";
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

    private async Task SyncSelectedToEtsyAsync()
    {
        var selectedItemIds = GetSelectedRowItemIds();
        if (selectedItemIds.Count == 0)
        {
            // If none selected, offer to sync all completed
            var completedCandidateIds = _items
                .Where(i => !string.IsNullOrWhiteSpace(i.OptimizedTitle) && !i.IsSyncedToEtsy)
                .Select(i => i.Id)
                .ToList();

            if (completedCandidateIds.Count == 0)
            {
                MessageBox.Show(this, "Etsy'ye gönderilecek optimize edilmiş hazır ürün bulunamadı. Lütfen önce 'AI Optimize Et' işlemini tamamlayın veya tablodan ürün seçin.", "Canlı Etsy Senkronizasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dialog = MessageBox.Show(this,
                $"Tablodan özel seçim yapmadınız. Henüz senkronize edilmemiş {completedCandidateIds.Count} adet optimize ürünün tamamı canlı Etsy mağazanıza aktarılsın mı?",
                "Tüm Hazır Ürünleri Etsy'ye Aktar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dialog != DialogResult.Yes) return;
            selectedItemIds = completedCandidateIds;
        }

        // Validate items have optimized titles
        var validItems = _items.Where(i => selectedItemIds.Contains(i.Id) && !string.IsNullOrWhiteSpace(i.OptimizedTitle)).ToList();
        if (validItems.Count == 0)
        {
            MessageBox.Show(this, "Seçilen ürünler henüz optimize edilmemiş. Önce optimizasyonu tamamlayın.", "Senkronizasyon Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var riskCount = validItems.Count(i => i.RiskWarnings.Count > 0);
        string warningExtra = riskCount > 0
            ? $"\n\n⚠️ DİKKAT: Seçilen ürünlerin {riskCount} tanesinde marka/telif risk uyarısı bulunmaktadır!"
            : string.Empty;

        var confirm = MessageBox.Show(this,
            $"Seçilen {validItems.Count} adet ürünün optimize edilmiş başlık, açıklama ve etiketleri CANLI Etsy mağazanızda güncellenecektir.{warningExtra}\n\nİşlemi başlatmak istiyor musunuz?",
            "Canlı Etsy Senkronizasyon Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _statusLabel.Text = "Etsy API bağlantısı hazırlanıyor...";
            var settings = EtsyApiSettingsStore.Load();

            var progress = new Progress<BatchSyncProgress>(p =>
            {
                _progressBar.Value = Math.Clamp((int)((double)p.CurrentIndex / p.TotalCount * 100), 0, 100);
                _statusLabel.Text = $"Etsy Senkronizasyonu: {p.CurrentIndex}/{p.TotalCount} - {p.CurrentTitle}";
            });

            var result = await _processorService.SyncBatchToEtsyAsync(
                validItems.Select(i => i.Id).ToList(),
                async (listingId, title, desc, tags, materials) =>
                {
                    var update = new ListingTextUpdate(title, desc, tags, materials);
                    await _apiClient.UpdateOwnShopListingTextAsync(settings, listingId, update);
                    EtsyApiSettingsStore.Save(settings);
                },
                progress);

            await LoadQueueAsync();

            string msg = $"{result.SuccessCount} adet ürün başarıyla canlı Etsy mağazanıza aktarıldı!";
            if (result.FailedCount > 0)
            {
                msg += $"\n{result.FailedCount} adet üründe hata oluştu:\n" + string.Join("\n", result.Errors.Take(5));
            }

            MessageBox.Show(this, msg, "Canlı Etsy Senkronizasyonu", MessageBoxButtons.OK, result.FailedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Etsy senkronizasyon hatası: {ex.Message}", "Senkronizasyon Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RollbackSelectedAsync()
    {
        var selectedItemIds = GetSelectedRowItemIds();
        var rollbackCandidates = _items
            .Where(i => selectedItemIds.Contains(i.Id) && (i.IsSyncedToEtsy || i.Status == BatchQueueItemStatus.SyncedToEtsy))
            .ToList();

        if (rollbackCandidates.Count == 0)
        {
            MessageBox.Show(this, "Seçilen ürünler arasında daha önce canlı Etsy mağazasına aktarılmış (🟢 Canlıda) ürün bulunamadı. Yalnızca canlıya aktarılan ürünler orijinal hallerine geri alınabilir.", "Geri Alma (Rollback)", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Seçilen {rollbackCandidates.Count} adet ürünün başlık, açıklama ve etiketleri Etsy mağazanızda ilk ORİJİNAL hallerine geri yüklenecektir (Rollback).\n\nBu işlemi onaylıyor musunuz?",
            "Etsy Orijinal Haline Geri Alma (Rollback)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var settings = EtsyApiSettingsStore.Load();
            int success = 0;
            int failed = 0;

            for (int i = 0; i < rollbackCandidates.Count; i++)
            {
                var item = rollbackCandidates[i];
                _progressBar.Value = Math.Clamp((int)((double)(i + 1) / rollbackCandidates.Count * 100), 0, 100);
                _statusLabel.Text = $"Geri alınıyor ({i + 1}/{rollbackCandidates.Count}): {item.OriginalTitle}...";

                try
                {
                    await _processorService.RollbackItemAsync(
                        item.Id,
                        async (listingId, title, desc, tags, materials) =>
                        {
                            var update = new ListingTextUpdate(title, desc, tags, materials);
                            await _apiClient.UpdateOwnShopListingTextAsync(settings, listingId, update);
                            EtsyApiSettingsStore.Save(settings);
                        });
                    success++;
                }
                catch
                {
                    failed++;
                }

                if (i < rollbackCandidates.Count - 1)
                {
                    await Task.Delay(300);
                }
            }

            await LoadQueueAsync();
            MessageBox.Show(this, $"{success} adet ürün başarıyla orijinal haline geri alındı! (Hatalı: {failed})", "Rollback Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Rollback hatası: {ex.Message}", "Geri Alma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<long> GetSelectedRowItemIds()
    {
        var ids = new List<long>();
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.DataBoundItem is BatchGridRow itemRow)
            {
                ids.Add(itemRow.Id);
            }
        }
        return ids;
    }

    private List<BatchQueueItem> GetSelectedOrCompletedItems()
    {
        var selectedIds = new HashSet<long>(GetSelectedRowItemIds());
        if (selectedIds.Count > 0)
        {
            return _items.Where(i => selectedIds.Contains(i.Id)).ToList();
        }

        // If no rows explicitly selected, pick all completed/synced items
        return _items.Where(i => !string.IsNullOrWhiteSpace(i.OptimizedTitle)).ToList();
    }

    private async Task SendSelectedToAbTestAsync()
    {
        var selectedItems = GetSelectedOrCompletedItems();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show(
                this,
                "A/B testine göndermek için listeden optimize edilmiş (Tamamlandı veya Canlıda) en az bir ürün seçiniz.",
                "Toplu A/B Testi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Filter items that have an optimized title
        var optimizable = selectedItems.Where(i => !string.IsNullOrWhiteSpace(i.OptimizedTitle)).ToList();
        if (optimizable.Count == 0)
        {
            MessageBox.Show(
                this,
                "Seçilen ürünler henüz optimize edilmemiş. Lütfen önce 'AI Optimize Et' işlemini çalıştırın.",
                "Toplu A/B Testi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new BulkAbTestLaunchDialog(optimizable.Count);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var options = dialog.Options;
        _progressBar.Value = 0;
        _statusLabel.Text = $"Toplu A/B testi başlatılıyor ({optimizable.Count} ürün)...";

        var requests = optimizable.Select(i => new BulkAbTestItemRequest(
            BatchQueueItemId: i.Id,
            ListingId: i.ListingId,
            OriginalTitle: i.OriginalTitle,
            OriginalDescription: i.OriginalDescription,
            OriginalTags: i.OriginalTags,
            OptimizedTitle: i.OptimizedTitle,
            OptimizedDescription: i.OptimizedDescription,
            OptimizedTags: i.OptimizedTags
        )).ToList();

        var progress = new Progress<BulkAbTestLaunchProgress>(p =>
        {
            _progressBar.Value = Math.Clamp((int)((double)p.CurrentIndex / p.TotalCount * 100), 0, 100);
            _statusLabel.Text = $"A/B Testi: [{p.CurrentIndex}/{p.TotalCount}] {p.ListingTitle}";
        });

        Func<long, string, string, IReadOnlyList<string>, Task>? deployAction = null;
        if (options.AutoDeployVariantBToEtsy)
        {
            var settings = EtsyApiSettingsStore.Load();
            deployAction = async (listingId, title, desc, tags) =>
            {
                var update = new ListingTextUpdate(title, desc, tags, null);
                await _apiClient.UpdateOwnShopListingTextAsync(settings, listingId, update);
                EtsyApiSettingsStore.Save(settings);
            };
        }

        try
        {
            var result = await _abTestService.BulkStartExperimentsAsync(
                requests,
                options,
                deployAction,
                progress,
                CancellationToken.None);

            await LoadQueueAsync();

            var msg = $"{result.SuccessCount} adet ürün başarıyla {options.DurationDays} günlük A/B testine aktarıldı.";
            if (options.AutoDeployVariantBToEtsy)
            {
                msg += "\n⚡ Varyant B (AI sürümü) canlı Etsy mağazanıza uygulandı.";
            }
            if (result.FailedCount > 0)
            {
                msg += $"\n⚠️ {result.FailedCount} ürün aktarılırken hata oluştu:\n" + string.Join("\n", result.Errors.Take(3));
            }

            MessageBox.Show(this, msg, "Toplu A/B Testi Başlatıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"A/B testi başlatılırken hata oluştu:\n{ex.Message}", "A/B Test Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Value = 0;
        }
    }

    private async Task OpenAddBatchItemsDialogAsync()
    {
        using var dlg = new Form
        {
            Text = "Toplu Ürün Ekle (Etsy Listing ID ve Başlık)",
            Width = 620,
            Height = 480,
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
        titlesInput.Text = "1827364501 | Resin Dragon Bust Fantasy Sculpture\n1827364502 | Hand Painted Dragon Statue Shelf Decor\n1827364503 | Tabletop Gaming Dragon Figurine Collectible";

        table.Controls.Add(new Label { Text = "Ortak Hedef Anahtar Kelime:", Dock = DockStyle.Fill }, 0, 0);
        table.Controls.Add(keywordInput, 0, 1);
        table.Controls.Add(new Label { Text = "Ürünler (Her satıra 'ListingID | Başlık' veya sadece 'Başlık' yazın):", Dock = DockStyle.Fill }, 0, 2);
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

            var itemsToSave = new List<SaveBatchQueueItem>();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                string listingId = "";
                string title = line;

                if (line.Contains('|'))
                {
                    var parts = line.Split('|', 2);
                    listingId = parts[0].Trim();
                    title = parts[1].Trim();
                }

                if (string.IsNullOrWhiteSpace(listingId) || !long.TryParse(listingId, out _))
                {
                    listingId = (1800000000L + (long)Random.Shared.Next(1000000, 9999999)).ToString();
                }

                itemsToSave.Add(new SaveBatchQueueItem(
                    listingId,
                    title,
                    $"Auto generated description for {title}",
                    [keywordInput.Text.Trim(), "handcrafted", "decor"],
                    keywordInput.Text.Trim(),
                    "Sculptures & Figurines"));
            }

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
        public string ListingId => item.ListingId;
        public string OriginalTitle => item.OriginalTitle;
        public string TargetKeyword => item.TargetKeyword;
        public string Status => item.Status switch
        {
            BatchQueueItemStatus.Pending => "Bekliyor",
            BatchQueueItemStatus.Processing => "İşleniyor...",
            BatchQueueItemStatus.Completed => "Tamamlandı",
            BatchQueueItemStatus.SyncedToEtsy => "Etsy'ye Aktarıldı",
            BatchQueueItemStatus.RolledBack => "Geri Alındı",
            BatchQueueItemStatus.Failed => "Hata Alındı",
            _ => item.Status.ToString()
        };
        public string SyncStatus => item.Status switch
        {
            BatchQueueItemStatus.SyncedToEtsy => "🟢 Canlıda",
            BatchQueueItemStatus.RolledBack => "↩️ Geri Alındı",
            _ when item.IsSyncedToEtsy => "🟢 Canlıda",
            _ when item.Status == BatchQueueItemStatus.Completed => "⚪ Kuyrukta Hazır",
            _ when item.Status == BatchQueueItemStatus.Failed => "❌ Hata",
            _ => "⚪ Bekliyor"
        };
        public string AbTestInfo => !string.IsNullOrWhiteSpace(item.AbTestStatus) ? $"🧪 {item.AbTestStatus}" : "-";
        public string Score => item.OverallScore > 0 ? $"{item.OverallScore}/100" : "-";
        public string Created => item.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
        public string SyncedAt => item.SyncedAt.HasValue ? item.SyncedAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm") : "-";
    }
}
