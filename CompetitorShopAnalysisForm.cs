namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Text;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class CompetitorShopAnalysisForm : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly TrackingService _trackingService;
    private readonly HttpClient _imageHttpClient = new();
    private readonly long _shopId;
    private readonly string _initialShopName;
    private readonly Label _titleLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _refreshButton = new();
    private readonly DataGridView _productsGrid = new();
    private readonly DataGridView _topProductsGrid = new();
    private readonly DataGridView _tagsGrid = new();
    private readonly DataGridView _termsGrid = new();
    private readonly DataGridView _taxonomyGrid = new();
    private readonly TextBox _summaryTextBox = new();
    private readonly TextBox _productFilterTextBox = new();
    private readonly ComboBox _productSortComboBox = new();
    private readonly Dictionary<string, Label> _kpiValues = [];
    private CompetitorShopAnalysis? _analysis;

    public CompetitorShopAnalysisForm(MarketListingResult listing, TrackingService trackingService)
    {
        _trackingService = trackingService;
        _shopId = listing.ShopId;
        _initialShopName = listing.ShopName;
        BuildLayout();
        Shown += async (_, _) => await LoadAnalysisAsync();
    }

    private MarketListingResult? SelectedProduct => _productsGrid.CurrentRow?.DataBoundItem as MarketListingResult;

    private void BuildLayout()
    {
        Text = "Rakip Magaza Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = $"Rakip Analizi: {_initialShopName}";
        _titleLabel.Font = UiStyle.TitleFont;
        _titleLabel.ForeColor = UiStyle.TextDark;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_titleLabel, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Magaza verileri bekleniyor";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, Padding = new Padding(0, 0, 0, 10) };
        for (var column = 0; column < 7; column++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7));
        }
        UiStyle.AddKpiCard(kpis, 0, 0, "Toplam satis", "sales", _kpiValues);
        UiStyle.AddKpiCard(kpis, 1, 0, "Aktif urun", "listings", _kpiValues);
        UiStyle.AddKpiCard(kpis, 2, 0, "Magaza puani", "reviews", _kpiValues);
        UiStyle.AddKpiCard(kpis, 3, 0, "Ortalama fiyat", "averagePrice", _kpiValues);
        UiStyle.AddKpiCard(kpis, 4, 0, "Medyan fiyat", "medianPrice", _kpiValues);
        UiStyle.AddKpiCard(kpis, 5, 0, "Ortalama SEO", "seo", _kpiValues);
        UiStyle.AddKpiCard(kpis, 6, 0, "Rakip gucu", "strength", _kpiValues);
        root.Controls.Add(kpis, 0, 1);

        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(0, 5, 0, 7) };
        for (var i = 0; i < 5; i++) commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        var shopButton = CreateButton("Etsy'de Magazayi Ac");
        shopButton.Click += (_, _) => OpenUrl(_analysis?.Shop.ShopUrl);
        commands.Controls.Add(shopButton, 0, 0);
        var csvButton = CreateButton("CSV Aktar");
        csvButton.Click += (_, _) => ExportCsv();
        commands.Controls.Add(csvButton, 1, 0);
        var trackButton = CreateButton("Takibe Ekle");
        trackButton.Click += async (_, _) => await TrackShopAsync();
        commands.Controls.Add(trackButton, 2, 0);
        ConfigureButton(_refreshButton, "Verileri Yenile");
        _refreshButton.Click += async (_, _) => await LoadAnalysisAsync();
        commands.Controls.Add(_refreshButton, 3, 0);
        var closeButton = CreateButton("Geri Don", isSecondary: true);
        closeButton.Click += (_, _) => Close();
        commands.Controls.Add(closeButton, 4, 0);
        root.Controls.Add(commands, 0, 2);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildOverviewTab());
        tabs.TabPages.Add(BuildProductsTab());
        tabs.TabPages.Add(BuildSeoTab());
        root.Controls.Add(tabs, 0, 3);
    }

    private TabPage BuildOverviewTab()
    {
        var page = new TabPage("Genel Bakis") { BackColor = Color.White, Padding = new Padding(12) };
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 160,
        };
        _summaryTextBox.Dock = DockStyle.Fill;
        _summaryTextBox.Multiline = true;
        _summaryTextBox.ReadOnly = true;
        _summaryTextBox.BorderStyle = BorderStyle.None;
        _summaryTextBox.BackColor = Color.White;
        _summaryTextBox.Font = new Font("Segoe UI", 10.5F);
        split.Panel1.Controls.Add(_summaryTextBox);
        ConfigureProductGrid(_topProductsGrid, includeImage: false);
        split.Panel2.Controls.Add(_topProductsGrid);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildProductsTab()
    {
        var page = new TabPage("Urunler") { BackColor = Color.White, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        filters.Controls.Add(CreateFilterLabel("Urun ara"), 0, 0);
        _productFilterTextBox.Dock = DockStyle.Fill;
        _productFilterTextBox.PlaceholderText = "Baslik, etiket veya kategori";
        _productFilterTextBox.TextChanged += (_, _) => ApplyProductFilter();
        filters.Controls.Add(_productFilterTextBox, 1, 0);
        filters.Controls.Add(CreateFilterLabel("Sirala"), 2, 0);
        _productSortComboBox.Dock = DockStyle.Fill;
        _productSortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _productSortComboBox.Items.AddRange(["Pazar puani", "SEO puani", "Favori", "Goruntulenme", "Dusuk fiyat", "Yuksek fiyat"]);
        _productSortComboBox.SelectedIndex = 0;
        _productSortComboBox.SelectedIndexChanged += (_, _) => ApplyProductFilter();
        filters.Controls.Add(_productSortComboBox, 3, 0);
        layout.Controls.Add(filters, 0, 0);
        ConfigureProductGrid(_productsGrid, includeImage: true);
        _productsGrid.CellDoubleClick += (_, _) => OpenUrl(SelectedProduct?.ListingUrl);
        _productsGrid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _productsGrid.Columns[e.ColumnIndex].Name == "ListingLink")
            {
                OpenUrl((_productsGrid.Rows[e.RowIndex].DataBoundItem as MarketListingResult)?.ListingUrl);
            }
        };
        layout.Controls.Add(_productsGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildSeoTab()
    {
        var page = new TabPage("SEO / Etiketler") { BackColor = Color.White, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.Controls.Add(BuildMetricSection("En sik kullanilan etiketler", _tagsGrid), 0, 0);
        layout.Controls.Add(BuildMetricSection("Baslik kelimeleri", _termsGrid), 1, 0);
        layout.Controls.Add(BuildMetricSection("Kategori / taksonomi dagilimi", _taxonomyGrid), 2, 0);
        page.Controls.Add(layout);
        return page;
    }

    private static Control BuildMetricSection(string title, DataGridView grid)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 12F),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        ConfigureMetricGrid(grid);
        panel.Controls.Add(grid, 0, 1);
        return panel;
    }

    private static void ConfigureMetricGrid(DataGridView grid)
    {
        UiStyle.ConfigureBaseGrid(grid);
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Deger",
            DataPropertyName = nameof(FrequencyMetric.Name),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Kullanim",
            DataPropertyName = nameof(FrequencyMetric.Count),
            Width = 78,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Oran",
            DataPropertyName = nameof(FrequencyMetric.PercentageDisplay),
            Width = 72,
        });
    }

    private static void ConfigureProductGrid(DataGridView grid, bool includeImage)
    {
        UiStyle.ConfigureBaseGrid(grid);
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.RowTemplate.MinimumHeight = includeImage ? 72 : 48;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        if (includeImage)
        {
            grid.Columns.Add(new DataGridViewImageColumn
            {
                HeaderText = "Resim",
                DataPropertyName = nameof(MarketListingResult.ThumbnailImage),
                Width = 86,
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                DefaultCellStyle = new DataGridViewCellStyle { NullValue = null },
            });
        }
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Urun basligi",
            DataPropertyName = nameof(MarketListingResult.Title),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 230,
        });
        AddProductColumn(grid, "Fiyat", nameof(MarketListingResult.PriceDisplay), 100);
        AddProductColumn(grid, "Favori", nameof(MarketListingResult.Favorites), 72);
        AddProductColumn(grid, "Goruntulenme", nameof(MarketListingResult.ViewsDisplay), 105);
        AddProductColumn(grid, "Stok", nameof(MarketListingResult.Quantity), 62);
        AddProductColumn(grid, "SEO", nameof(MarketListingResult.SeoScore), 62);
        AddProductColumn(grid, "Pazar", nameof(MarketListingResult.MarketScore), 66);
        AddProductColumn(grid, "Kategori", nameof(MarketListingResult.TaxonomyDisplay), 135);
        grid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = "ListingLink",
            HeaderText = "Etsy",
            Text = "Listing ac",
            UseColumnTextForLinkValue = true,
            Width = 88,
            TrackVisitedState = false,
        });
    }

    private async Task LoadAnalysisAsync()
    {
        try
        {
            _refreshButton.Enabled = false;
            _statusLabel.Text = "Magaza ve urun verileri Etsy'den aliniyor...";
            var settings = EtsyApiSettingsStore.Load();
            _analysis = await _apiClient.GetCompetitorShopAnalysisAsync(settings, _shopId, 50);
            BindAnalysis(_analysis);
            _ = LoadThumbnailsAsync(_analysis.Listings);
            _statusLabel.Text = $"{_analysis.Listings.Count} urun incelendi | {_analysis.RetrievedAt:dd.MM.yyyy HH:mm}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Rakip analizi hatasi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Rakip analizi alinamadi";
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }

    private void BindAnalysis(CompetitorShopAnalysis analysis)
    {
        _titleLabel.Text = $"Rakip Analizi: {analysis.Shop.ShopName}";
        SetKpi("sales", analysis.Shop.TotalSales > 0 ? analysis.Shop.TotalSales.ToString("N0") : "Veri yok");
        SetKpi("listings", analysis.Shop.ActiveListingCount > 0 ? analysis.Shop.ActiveListingCount.ToString("N0") : analysis.Listings.Count.ToString("N0"));
        SetKpi("reviews", analysis.Shop.ReviewCount > 0 ? $"{analysis.Shop.ReviewAverage:0.0} / 5 ({analysis.Shop.ReviewCount:N0})" : "Veri yok");
        SetKpi("averagePrice", FormatPrice(analysis.AveragePrice, analysis));
        SetKpi("medianPrice", FormatPrice(analysis.MedianPrice, analysis));
        SetKpi("seo", $"{analysis.AverageSeoScore} / 100");
        SetKpi("strength", $"{analysis.CompetitorStrengthScore} / 100*");

        var topProducts = analysis.Listings
            .OrderByDescending(item => item.MarketScore)
            .ThenByDescending(item => item.Favorites)
            .Take(10)
            .ToList();
        _topProductsGrid.DataSource = topProducts;
        ApplyProductFilter();
        _tagsGrid.DataSource = analysis.TopTags;
        _termsGrid.DataSource = analysis.TopTitleTerms;
        _taxonomyGrid.DataSource = analysis.TaxonomyDistribution;

        _summaryTextBox.Text =
            $"MAGAZA OZETI{Environment.NewLine}" +
            $"{analysis.Shop.Title}{Environment.NewLine}" +
            $"Analiz edilen urun: {analysis.Listings.Count:N0} / Aktif urun: {analysis.Shop.ActiveListingCount:N0}{Environment.NewLine}" +
            $"Fiyat araligi: {FormatPrice(analysis.MinimumPrice, analysis)} - {FormatPrice(analysis.MaximumPrice, analysis)} | Para birimleri: {analysis.CurrencyDisplay}{Environment.NewLine}" +
            $"Ortalama favori: {analysis.AverageFavorites:N1} | Ortalama goruntulenme: {analysis.AverageViews:N1}{Environment.NewLine}" +
            $"* Rakip gucu Etsy'nin resmi puani degildir; magaza satisi, yorum, favori, goruntulenme, SEO ve cesitlilik sinyallerinden hesaplanir.";
    }

    private async Task LoadThumbnailsAsync(IEnumerable<MarketListingResult> listings)
    {
        using var semaphore = new SemaphoreSlim(6);
        var tasks = listings.Where(item => item.ImageUrls.Count > 0).Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var bytes = await _imageHttpClient.GetByteArrayAsync(item.ImageUrls[0]);
                using var stream = new MemoryStream(bytes);
                using var image = Image.FromStream(stream);
                item.ThumbnailImage = new Bitmap(image);
            }
            catch
            {
                item.ThumbnailImage = null;
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
        if (!IsDisposed)
        {
            BeginInvoke(() => _productsGrid.Refresh());
        }
    }

    private void ApplyProductFilter()
    {
        if (_analysis is null)
        {
            return;
        }

        var query = _productFilterTextBox.Text.Trim();
        IEnumerable<MarketListingResult> filtered = _analysis.Listings;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.TagsDisplay.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.TaxonomyDisplay.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        filtered = _productSortComboBox.SelectedItem?.ToString() switch
        {
            "SEO puani" => filtered.OrderByDescending(item => item.SeoScore),
            "Favori" => filtered.OrderByDescending(item => item.Favorites),
            "Goruntulenme" => filtered.OrderByDescending(item => item.Views),
            "Dusuk fiyat" => filtered.OrderBy(item => item.Price),
            "Yuksek fiyat" => filtered.OrderByDescending(item => item.Price),
            _ => filtered.OrderByDescending(item => item.MarketScore),
        };
        _productsGrid.DataSource = filtered.ToList();
    }

    private void ExportCsv()
    {
        if (_analysis is null || _analysis.Listings.Count == 0)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV dosyasi (*.csv)|*.csv",
            FileName = $"rakip-magaza-{_analysis.Shop.ShopName}-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Baslik,Fiyat,Favori,Goruntulenme,Stok,SEO,PazarPuani,Kategori,Tagler,ListingLinki");
        foreach (var item in _analysis.Listings)
        {
            builder.AppendLine(string.Join(",", Csv(item.Title), Csv(item.PriceDisplay), item.Favorites, item.Views, item.Quantity, item.SeoScore, item.MarketScore, Csv(item.TaxonomyDisplay), Csv(item.TagsDisplay), Csv(item.ListingUrl)));
        }
        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "Rakip magaza CSV dosyasi kaydedildi";
    }

    private async Task TrackShopAsync()
    {
        if (_analysis is null)
        {
            MessageBox.Show(this, "Once magaza analizinin tamamlanmasini bekleyin.", "Takibe Ekle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _trackingService.TrackAsync(new TrackingCapture(
            TrackingEntityType.Shop,
            _analysis.Shop.ShopId.ToString(),
            _analysis.Shop.ShopName,
            _analysis.Shop.ShopUrl,
            new TrackingSnapshot(
                0,
                0,
                DateTimeOffset.Now,
                _analysis.AveragePrice,
                _analysis.PriceCurrency,
                null,
                null,
                _analysis.Shop.TotalSales,
                _analysis.Shop.ReviewCount,
                _analysis.Shop.ReviewAverage,
                _analysis.AverageSeoScore,
                _analysis.CompetitorStrengthScore,
                null,
                null,
                null,
                _analysis.Shop.ActiveListingCount,
                _analysis.Listings.Count)));
        _statusLabel.Text = "Magaza takibe eklendi ve snapshot kaydedildi";
    }

    private void SetKpi(string key, string value) => _kpiValues[key].Text = value;

    private static string FormatPrice(decimal value, CompetitorShopAnalysis analysis)
    {
        return value > 0 ? $"{analysis.PriceCurrency} {value:0.##}" : "Veri yok";
    }

    private static Label CreateFilterLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = UiStyle.TextDark,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static void AddProductColumn(DataGridView grid, string title, string property, int width)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = property, Width = width });
    }

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button();
        ConfigureButton(button, text, isSecondary);
        return button;
    }

    private static void ConfigureButton(Button button, string text, bool isSecondary = false)
    {
        button.Dock = DockStyle.Fill;
        button.Text = text;
        button.BackColor = isSecondary ? UiStyle.SecondaryColor : UiStyle.PrimaryColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isSecondary ? UiStyle.SecondaryHover : UiStyle.PrimaryHover;
        button.Font = UiStyle.SemiboldBaseFont;
        button.Margin = new Padding(6, 2, 0, 2);
    }

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
