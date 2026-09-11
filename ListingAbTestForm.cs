namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class ListingAbTestForm : Form
{
    private readonly AbTestService _abTestService;
    private readonly IAiListingOptimizer? _aiOptimizer;
    private readonly EtsyApiClient _apiClient = new();
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

    public ListingAbTestForm(AbTestService abTestService, IAiListingOptimizer? aiOptimizer = null)
    {
        _abTestService = abTestService;
        _aiOptimizer = aiOptimizer;
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
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f)); // Yeni Test
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f)); // 🔄 Etsy Canlı Senkron
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f)); // ✏️ Metrik Düzenle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f)); // 🗑️ Sil
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f)); // Yenile
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f)); // Kapat

        var newTestBtn = UiStyle.CreateButton("🧪 Yeni A/B Testi");
        newTestBtn.Click += async (_, _) => await OpenNewTestDialogAsync();
        toolbar.Controls.Add(newTestBtn, 0, 0);

        var syncEtsyBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🔄 Etsy'den Senkronize Et",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(4, 2, 4, 2),
        };
        syncEtsyBtn.Click += async (_, _) => await SyncLiveMetricsFromEtsyAsync();
        toolbar.Controls.Add(syncEtsyBtn, 1, 0);

        var updateMetricsBtn = UiStyle.CreateButton("✏️ Metrik Düzenle", isSecondary: true);
        updateMetricsBtn.Click += async (_, _) => await OpenUpdateMetricsDialogAsync();
        toolbar.Controls.Add(updateMetricsBtn, 2, 0);

        var deleteBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🗑️ Sil",
            NormalColor = UiStyle.SecondaryColor,
            HoverColor = UiStyle.DangerColor,
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(4, 2, 4, 2),
        };
        deleteBtn.Click += async (_, _) => await DeleteSelectedTestAsync();
        toolbar.Controls.Add(deleteBtn, 3, 0);

        var refreshBtn = UiStyle.CreateButton("Yenile", isSecondary: true);
        refreshBtn.Click += async (_, _) => await LoadDataAsync();
        toolbar.Controls.Add(refreshBtn, 4, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        toolbar.Controls.Add(closeBtn, 5, 0);
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
        var winnerTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(6) };
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        winnerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

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

        // Action Buttons Row (Row 3)
        var actionsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        var applyVariantBBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🏆 Varyant B'yi Kalıcı Yap",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(2),
        };
        applyVariantBBtn.Click += async (_, _) => await ResolveWinnerAsync("VariantB");
        actionsPanel.Controls.Add(applyVariantBBtn, 0, 0);

        var rollbackBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "↩️ Orijinale Dön (Rollback)",
            NormalColor = UiStyle.SecondaryColor,
            HoverColor = UiStyle.DangerColor,
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(2),
        };
        rollbackBtn.Click += async (_, _) => await ResolveWinnerAsync("VariantA");
        actionsPanel.Controls.Add(rollbackBtn, 1, 0);

        var guardrailBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🛡️ Güvenlik Kontrolü",
            NormalColor = UiStyle.WarningColor,
            HoverColor = Color.FromArgb(217, 119, 6),
            ForeColor = Color.White,
            Margin = new Padding(2),
        };
        guardrailBtn.Click += (_, _) => CheckSelectedGuardrail();
        actionsPanel.Controls.Add(guardrailBtn, 2, 0);

        winnerTable.Controls.Add(actionsPanel, 0, 3);

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

    private async Task ResolveWinnerAsync(string chosenVariant)
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            MessageBox.Show(this, "Lütfen işlem yapmak için listeden bir A/B testi seçiniz.", "A/B Testi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var experiment = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (experiment == null) return;

        bool isVariantB = chosenVariant.Equals("VariantB", StringComparison.OrdinalIgnoreCase);
        string variantName = isVariantB ? "Varyant B (AI Optimizasyonu)" : "Varyant A (Orijinal Listing)";

        var confirm = MessageBox.Show(
            this,
            $"Bu A/B testi sonuçlandırılacak ve {variantName} canlı Etsy mağazanıza uygulanacaktır.\n\nEmin misiniz?",
            "A/B Testini Sonuçlandır",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _statusLabel.Text = $"Etsy güncelleniyor ({variantName})...";
            var settings = EtsyApiSettingsStore.Load();

            Func<long, string, string, IReadOnlyList<string>, Task> deployAction = async (listingId, title, desc, tags) =>
            {
                var update = new ListingTextUpdate(title, desc, tags, []);
                await _apiClient.UpdateOwnShopListingTextAsync(settings, listingId, update);
                EtsyApiSettingsStore.Save(settings);
            };

            await _abTestService.ResolveExperimentAsync(
                experiment.Id,
                chosenVariant,
                deployAction,
                CancellationToken.None);

            await LoadDataAsync();

            MessageBox.Show(
                this,
                $"Tebrikler! {variantName} başarıyla kalıcı yapıldı ve A/B testi tamamlandı.",
                "A/B Testi Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"İşlem sırasında hata oluştu:\n{ex.Message}", "A/B Sonuçlandırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _statusLabel.Text = "Hazır";
        }
    }

    private void CheckSelectedGuardrail()
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            MessageBox.Show(this, "Lütfen listeden bir A/B testi seçiniz.", "Güvenlik Kalkanı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var experiment = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (experiment == null) return;

        var alert = _abTestService.CheckSafetyGuardrail(experiment, dropThresholdPercent: 30.0);
        if (alert != null)
        {
            var res = MessageBox.Show(
                this,
                $"⚠️ ANORMAL DÜŞÜŞ TESPİT EDİLDİ!\n\nÜrün: {alert.ListingTitle}\nDüşüş Oranı: %{alert.DropPercentage}\n\n{alert.WarningMessage}\n\nBu ürünü şimdi Orijinal Varyant A'ya geri almak (Rollback) ister misiniz?",
                "🛡️ Akıllı Güvenlik Kalkanı Uyarısı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                _ = ResolveWinnerAsync("VariantA");
            }
        }
        else
        {
            MessageBox.Show(
                this,
                "✅ Güvenlik Kontrolü Başarılı: Bu üründe herhangi bir anormal düşüş tespit edilmedi. Algoritma performansı olağan seyrinde ilerliyor.",
                "🛡️ Akıllı Güvenlik Kalkanı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task SyncLiveMetricsFromEtsyAsync()
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            MessageBox.Show(this, "Lütfen Etsy metriklerini senkronize etmek için listeden bir A/B testi seçiniz.", "Etsy Senkronizasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var experiment = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (experiment == null) return;

        if (!long.TryParse(experiment.ListingId, out var listingId) || listingId <= 0)
        {
            MessageBox.Show(this, $"Bu testin geçerli bir Etsy Listing ID'si bulunmuyor (ID: '{experiment.ListingId}'). Canlı senkronizasyon yalnızca gerçek Etsy ürünlerinde çalışır.", "Geçersiz Listing ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var settings = EtsyApiSettingsStore.Load();
        if (!settings.HasApiCredentials)
        {
            MessageBox.Show(this, "Etsy API ayarlarınız tanımlı değil. Lütfen önce Dashboard > Ayarlar menüsünden Etsy API anahtarlarınızı girin.", "Etsy API Ayarları Eksik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = $"Etsy'den güncel metrikler alınıyor (Listing #{listingId})...";

            var listing = await _apiClient.GetOwnShopListingAsync(settings, listingId);
            EtsyApiSettingsStore.Save(settings);

            // Update metrics in SQLite
            var update = new UpdateAbTestMetrics(
                experiment.Id,
                AfterViews: listing.Views,
                AfterFavorites: listing.Favorites,
                AfterSales: experiment.AfterSales,
                CompleteExperiment: false);

            await _abTestService.UpdateMetricsAsync(update);
            await LoadDataAsync();

            int viewsDiff = listing.Views - experiment.BeforeViews;
            int favsDiff = listing.Favorites - experiment.BeforeFavorites;

            MessageBox.Show(
                this,
                $"✅ Etsy canlı metrikleri başarıyla güncellendi!\n\n" +
                $"Listing ID: #{listingId}\n" +
                $"Ürün: {listing.Title}\n\n" +
                $"• Görüntülenme: {experiment.BeforeViews} ➔ {listing.Views} (Fark: {viewsDiff:+0;-#;0})\n" +
                $"• Favori: {experiment.BeforeFavorites} ➔ {listing.Favorites} (Fark: {favsDiff:+0;-#;0})",
                "Etsy Senkronizasyonu Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Etsy canlı metrikleri alınırken hata oluştu:\n{ex.Message}", "Etsy Senkronizasyon Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _statusLabel.Text = "Hazır";
        }
    }

    private async Task DeleteSelectedTestAsync()
    {
        if (_bindingSource.Current is not AbTestGridRow row)
        {
            MessageBox.Show(this, "Lütfen silmek istediğiniz A/B testini seçiniz.", "A/B Test Sil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var experiment = _experiments.FirstOrDefault(e => e.Id == row.Id);
        if (experiment == null) return;

        var confirm = MessageBox.Show(
            this,
            $"'{experiment.ExperimentName}' isimli A/B testini silmek istediğinize emin misiniz?\n\n(Not: Mağazanızdaki Etsy ürünü etkilenmez, sadece yerel test kaydı silinir.)",
            "A/B Testini Sil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            await _abTestService.DeleteAsync(experiment.Id);
            await LoadDataAsync();
            _statusLabel.Text = "A/B testi başarıyla silindi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"A/B testi silinirken hata oluştu:\n{ex.Message}", "Silme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OpenNewTestDialogAsync()
    {
        using var dlg = new Form
        {
            Text = "🧪 Yeni Etsy A/B Testi Başlat",
            Width = 720,
            Height = 680,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        UiStyle.ApplyTheme(dlg);

        var rootTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            Padding = new Padding(12),
        };
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Header
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // 1. Listing Picker
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 2. Variants (A vs B)
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));  // 3. Duration & Deploy
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // 4. Action buttons

        // Row 0: Header Banner
        var headerPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        var lblHead = new Label
        {
            Text = "Canlı Etsy Listeleme A/B Testi",
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = UiStyle.PrimaryColor,
            Dock = DockStyle.Fill,
        };
        var lblSub = new Label
        {
            Text = "Etsy mağazanızdaki aktif ürünü seçin, AI ile Varyant B üretin ve testi başlatın.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Fill,
        };
        headerPanel.Controls.Add(lblHead, 0, 0);
        headerPanel.Controls.Add(lblSub, 0, 1);
        rootTable.Controls.Add(headerPanel, 0, 0);

        // Row 1: Listing Picker GroupBox
        var grpPicker = new GroupBox
        {
            Text = "1. Etsy Mağazanızdan Ürün Seçin",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
        };
        var pickerTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        pickerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        pickerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var cboListings = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9F),
        };
        cboListings.Items.Add("Etsy mağazanız taranıyor, lütfen bekleyin...");
        cboListings.SelectedIndex = 0;
        pickerTable.Controls.Add(cboListings, 0, 0);

        var lblStats = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = UiStyle.TextMuted,
            Text = "Listelemeler mağazadan çekildikten sonra başlangıç metrikleri otomatik doldurulacaktır.",
        };
        pickerTable.Controls.Add(lblStats, 0, 1);
        grpPicker.Controls.Add(pickerTable);
        rootTable.Controls.Add(grpPicker, 0, 1);

        // Row 2: Variants GroupBox
        var grpVariants = new GroupBox
        {
            Text = "2. Test Başlığı ve Varyantlar (A vs B)",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
        };
        var variantsTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(6) };
        variantsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Test Name
        variantsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Two columns (A vs B)
        variantsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // AI Button

        // Test name
        var testNamePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        testNamePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        testNamePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        testNamePanel.Controls.Add(new Label { Text = "Test Adı:", Font = new Font("Segoe UI", 8.5F), Dock = DockStyle.Fill }, 0, 0);
        var txtTestName = new TextBox { Dock = DockStyle.Fill, Text = "Etsy SEO & Başlık Optimizasyon Testi" };
        testNamePanel.Controls.Add(txtTestName, 0, 1);
        variantsTable.Controls.Add(testNamePanel, 0, 0);

        // Two columns
        var colsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        colsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        colsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Col A (Variant A)
        var panelA = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        panelA.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panelA.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        panelA.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panelA.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        panelA.Controls.Add(new Label { Text = "Varyant A: Mevcut Canlı Başlık", Font = new Font("Segoe UI Semibold", 8.5F), Dock = DockStyle.Fill }, 0, 0);
        var txtTitleA = new ModernMultilineTextBox { Dock = DockStyle.Fill };
        panelA.Controls.Add(txtTitleA, 0, 1);
        panelA.Controls.Add(new Label { Text = "Varyant A: Mevcut Tagler", Font = new Font("Segoe UI Semibold", 8.5F), Dock = DockStyle.Fill }, 0, 2);
        var txtTagsA = new ModernMultilineTextBox { Dock = DockStyle.Fill };
        panelA.Controls.Add(txtTagsA, 0, 3);
        colsTable.Controls.Add(panelA, 0, 0);

        // Col B (Variant B)
        var panelB = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        panelB.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panelB.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        panelB.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panelB.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        panelB.Controls.Add(new Label { Text = "Varyant B: Yeni Test Başlığı", Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.PrimaryColor, Dock = DockStyle.Fill }, 0, 0);
        var txtTitleB = new ModernMultilineTextBox { Dock = DockStyle.Fill };
        panelB.Controls.Add(txtTitleB, 0, 1);
        panelB.Controls.Add(new Label { Text = "Varyant B: Yeni Test Tagleri", Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.PrimaryColor, Dock = DockStyle.Fill }, 0, 2);
        var txtTagsB = new ModernMultilineTextBox { Dock = DockStyle.Fill };
        panelB.Controls.Add(txtTagsB, 0, 3);
        colsTable.Controls.Add(panelB, 1, 0);

        variantsTable.Controls.Add(colsTable, 0, 1);

        // AI Suggest Button
        var btnAiSuggest = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "✨ AI Başlık ve Tag Öner (Gemini/OpenAI)",
            NormalColor = UiStyle.AccentColor,
            HoverColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 0, 0),
        };
        variantsTable.Controls.Add(btnAiSuggest, 0, 2);
        grpVariants.Controls.Add(variantsTable);
        rootTable.Controls.Add(grpVariants, 0, 2);

        // Row 3: Test Duration & Deployment Settings
        var grpSettings = new GroupBox
        {
            Text = "3. Test Süresi ve Dağıtım",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
        };
        var settingsTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        settingsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        settingsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var durationPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        durationPanel.Controls.Add(new Label { Text = "Test Süresi: ", Font = new Font("Segoe UI", 9F), AutoSize = true, Margin = new Padding(0, 4, 6, 0) });
        var cboDuration = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        cboDuration.Items.Add("7 Gün");
        cboDuration.Items.Add("14 Gün (Önerilen)");
        cboDuration.Items.Add("30 Gün");
        cboDuration.SelectedIndex = 1;
        durationPanel.Controls.Add(cboDuration);
        settingsTable.Controls.Add(durationPanel, 0, 0);

        var chkPublishLive = new ModernCheckBox
        {
            Text = "🚀 Varyant B'yi Şimdi Canlı Etsy'ye Uygula (Test başladığı an mağazada aktifleşir)",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.SuccessColor,
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };
        settingsTable.Controls.Add(chkPublishLive, 0, 1);
        grpSettings.Controls.Add(settingsTable);
        rootTable.Controls.Add(grpSettings, 0, 3);

        // Row 4: Dialog Buttons
        var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var saveBtn = new Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🧪 A/B Testini Başlat",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(4, 2, 4, 2),
        };

        var cancelBtn = UiStyle.CreateButton("İptal", isSecondary: true);
        cancelBtn.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;

        btnPanel.Controls.Add(saveBtn, 0, 0);
        btnPanel.Controls.Add(cancelBtn, 1, 0);
        rootTable.Controls.Add(btnPanel, 0, 4);

        dlg.Controls.Add(rootTable);

        // State variables
        string currentListingId = "";
        string currentDescA = "";
        string currentDescB = "";
        int initialViews = 0;
        int initialFavorites = 0;
        string prevTitleA = "";

        // Populate listings when shown
        dlg.Shown += async (_, _) =>
        {
            try
            {
                var settings = EtsyApiSettingsStore.Load();
                if (!settings.HasApiCredentials)
                {
                    cboListings.Items.Clear();
                    cboListings.Items.Add("⚠️ Etsy API ayarları yapılmamış. Ayarlar menüsünden API anahtarlarınızı girin.");
                    cboListings.SelectedIndex = 0;
                    return;
                }

                var activeListings = await _apiClient.GetOwnShopActiveListingsAsync(settings, 100);
                EtsyApiSettingsStore.Save(settings);

                cboListings.Items.Clear();
                if (activeListings.Count == 0)
                {
                    cboListings.Items.Add("Mağazanızda aktif listeleme bulunamadı.");
                    cboListings.SelectedIndex = 0;
                    return;
                }

                foreach (var item in activeListings)
                {
                    cboListings.Items.Add(new ListingComboItem(item));
                }
                cboListings.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                cboListings.Items.Clear();
                cboListings.Items.Add($"Etsy listelemeleri alınamadı: {ex.Message}");
                cboListings.SelectedIndex = 0;
            }
        };

        cboListings.SelectedIndexChanged += (_, _) =>
        {
            if (cboListings.SelectedItem is ListingComboItem item)
            {
                var l = item.Listing;
                currentListingId = l.ListingId.ToString();
                currentDescA = l.Description;
                currentDescB = l.Description;
                initialViews = l.Views;
                initialFavorites = l.Favorites;

                txtTestName.Text = $"A/B: {(l.Title.Length > 30 ? l.Title[..30] + "..." : l.Title)}";
                txtTitleA.Text = l.Title;
                txtTagsA.Text = string.Join(", ", l.Tags);

                if (string.IsNullOrWhiteSpace(txtTitleB.Text) || txtTitleB.Text == prevTitleA)
                {
                    txtTitleB.Text = l.Title;
                    txtTagsB.Text = string.Join(", ", l.Tags);
                }
                prevTitleA = l.Title;

                lblStats.Text = $"Seçili Ürün: #{l.ListingId} | 👁️ {l.Views} Görüntülenme | ⭐ {l.Favorites} Favori | Fiyat: {l.PriceDisplay}";
                lblStats.ForeColor = UiStyle.PrimaryColor;
            }
        };

        btnAiSuggest.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txtTitleA.Text))
            {
                MessageBox.Show(dlg, "Lütfen önce bir ürün seçin veya Varyant A başlığını girin.", "AI Öneri", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                dlg.UseWaitCursor = true;
                btnAiSuggest.Enabled = false;
                btnAiSuggest.Text = "⏳ Yapay Zeka Optimize Ediyor...";

                var optimizer = _aiOptimizer ?? new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService());
                var tagsList = txtTagsA.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
                var input = new ListingOptimizationInput(txtTitleA.Text.Trim(), currentDescA, tagsList, "");

                var result = await optimizer.OptimizeAsync(input);

                var suggestedTitle = result.TitleSuggestions.Count > 0 ? result.TitleSuggestions[0] : txtTitleA.Text;
                txtTitleB.Text = suggestedTitle;
                txtTagsB.Text = string.Join(", ", result.TagSuggestions);
                currentDescB = string.IsNullOrWhiteSpace(result.DescriptionDraft) ? currentDescA : result.DescriptionDraft;

                MessageBox.Show(
                    dlg,
                    "✨ Yapay Zeka Destekli Varyant B Başarıyla Oluşturuldu!\n\n" +
                    $"Yeni Başlık: {suggestedTitle}\n" +
                    $"Yeni Tag Sayısı: {result.TagSuggestions.Count}",
                    "AI Optimizasyonu Tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(dlg, $"AI optimizasyonu sırasında hata oluştu:\n{ex.Message}", "AI Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAiSuggest.Enabled = true;
                btnAiSuggest.Text = "✨ AI Başlık ve Tag Öner (Gemini/OpenAI)";
                dlg.UseWaitCursor = false;
            }
        };

        saveBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(currentListingId))
            {
                MessageBox.Show(dlg, "Lütfen mağazanızdan geçerli bir Etsy listelemesi seçiniz.", "A/B Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTitleA.Text) || string.IsNullOrWhiteSpace(txtTitleB.Text))
            {
                MessageBox.Show(dlg, "Lütfen her iki varyant için de başlık giriniz.", "A/B Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int durationDays = cboDuration.SelectedIndex switch
            {
                0 => 7,
                2 => 30,
                _ => 14
            };

            var tagsA = txtTagsA.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var tagsB = txtTagsB.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var endDate = DateTimeOffset.Now.AddDays(durationDays);

            var exp = new SaveAbTestExperiment(
                currentListingId,
                txtTitleA.Text.Trim(),
                txtTestName.Text.Trim(),
                txtTitleA.Text.Trim(),
                txtTitleB.Text.Trim(),
                tagsA,
                tagsB,
                currentDescA,
                string.IsNullOrWhiteSpace(currentDescB) ? currentDescA : currentDescB,
                InitialViews: initialViews,
                InitialFavorites: initialFavorites,
                InitialSales: 0,
                EndDate: endDate);

            try
            {
                dlg.UseWaitCursor = true;
                saveBtn.Enabled = false;

                // 1. Save experiment to SQLite
                await _abTestService.StartExperimentAsync(exp);

                // 2. Publish to live Etsy if requested
                if (chkPublishLive.Checked && long.TryParse(currentListingId, out var lid) && lid > 0)
                {
                    var settings = EtsyApiSettingsStore.Load();
                    if (settings.HasApiCredentials)
                    {
                        var update = new ListingTextUpdate(
                            txtTitleB.Text.Trim(),
                            string.IsNullOrWhiteSpace(currentDescB) ? currentDescA : currentDescB,
                            tagsB,
                            []);
                        await _apiClient.UpdateOwnShopListingTextAsync(settings, lid, update);
                        EtsyApiSettingsStore.Save(settings);
                    }
                }

                dlg.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? $"{ex.Message}\n({ex.InnerException.Message})" : ex.Message;
                MessageBox.Show(dlg, $"A/B testi başlatılırken hata oluştu:\n{errorMsg}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                saveBtn.Enabled = true;
                dlg.UseWaitCursor = false;
            }
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadDataAsync();
        }
    }

    private sealed class ListingComboItem(MarketListingResult listing)
    {
        public MarketListingResult Listing => listing;
        public override string ToString() =>
            $"[#{listing.ListingId}] {(listing.Title.Length > 55 ? listing.Title[..55] + "..." : listing.Title)} (👁️ {listing.Views} | ⭐ {listing.Favorites})";
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
