namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Text;
using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.KeywordResearch;
using EtsyMarketPlace.Domain.Tracking;
using SimilarProductsWinForms.Controls;

internal sealed class KeywordOpportunityAnalysisForm : Form
{
    private readonly AnalyzeKeywordUseCase _useCase;
    private readonly TrackingService _trackingService;
    private readonly HttpClient _imageClient = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly ModernNumericUpDown _limitInput = new();
    private readonly Button _analyzeButton = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _summaryTextBox = new();
    private readonly DataGridView _productsGrid = new();
    private readonly DataGridView _tagsGrid = new();
    private readonly DataGridView _termsGrid = new();
    private readonly DataGridView _longTailGrid = new();
    private readonly DataGridView _comparisonGrid = new();
    private readonly Dictionary<string, Label> _kpiValues = [];
    private readonly List<KeywordComparisonRow> _comparisons = [];
    private List<KeywordProductRow> _productRows = [];
    private KeywordAnalysisResult? _result;

    public KeywordOpportunityAnalysisForm(AnalyzeKeywordUseCase useCase, TrackingService trackingService, string initialKeyword)
    {
        _useCase = useCase;
        _trackingService = trackingService;
        BuildLayout();
        _keywordTextBox.Text = initialKeyword;
        Shown += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_keywordTextBox.Text))
            {
                await AnalyzeAsync();
            }
        };
    }

    private KeywordProductRow? SelectedProduct => _productsGrid.CurrentRow?.DataBoundItem as KeywordProductRow;

    private void BuildLayout()
    {
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Anahtar Kelime ve Firsat Analizi",
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Analiz icin anahtar kelime girin";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var search = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, Padding = new Padding(0, 3, 0, 7) };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        search.Controls.Add(CreateLabel("Anahtar kelime"), 0, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "ornek: sword display, cosplay helmet";
        _keywordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await AnalyzeAsync();
            }
        };
        search.Controls.Add(_keywordTextBox, 1, 0);
        search.Controls.Add(CreateLabel("Orneklem"), 2, 0);
        _limitInput.Dock = DockStyle.Fill;
        _limitInput.Minimum = 20;
        _limitInput.Maximum = 100;
        _limitInput.Increment = 10;
        _limitInput.Value = 100;
        search.Controls.Add(_limitInput, 3, 0);
        ConfigureButton(_analyzeButton, "Analiz Et");
        _analyzeButton.Click += async (_, _) => await AnalyzeAsync();
        search.Controls.Add(_analyzeButton, 4, 0);
        var csvButton = CreateButton("CSV Aktar");
        csvButton.Click += (_, _) => ExportCsv();
        search.Controls.Add(csvButton, 5, 0);
        var trackButton = CreateButton("Takibe Ekle");
        trackButton.Click += async (_, _) => await TrackKeywordAsync();
        search.Controls.Add(trackButton, 6, 0);
        var closeButton = CreateButton("Geri Don");
        closeButton.BackColor = Color.FromArgb(82, 93, 110);
        closeButton.Click += (_, _) => Close();
        search.Controls.Add(closeButton, 7, 0);
        root.Controls.Add(search, 0, 1);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(0, 0, 0, 8) };
        for (var column = 0; column < 4; column++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        AddKpi(kpis, 0, 0, "Toplam sonuc", "results");
        AddKpi(kpis, 1, 0, "Orneklem", "sample");
        AddKpi(kpis, 2, 0, "Medyan fiyat", "price");
        AddKpi(kpis, 3, 0, "Ortalama SEO", "seo");
        AddKpi(kpis, 0, 1, "Rekabet*", "competition");
        AddKpi(kpis, 1, 1, "Talep sinyali*", "demand");
        AddKpi(kpis, 2, 1, "Firsat puani*", "opportunity");
        AddKpi(kpis, 3, 1, "Veri guveni", "confidence");
        root.Controls.Add(kpis, 0, 2);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildOverviewTab());
        tabs.TabPages.Add(BuildTagsTab());
        tabs.TabPages.Add(BuildProductsTab());
        tabs.TabPages.Add(BuildComparisonTab());
        root.Controls.Add(tabs, 0, 3);
    }

    private TabPage BuildOverviewTab()
    {
        var page = new TabPage("Genel Bakis") { BackColor = Color.White, Padding = new Padding(12) };
        _summaryTextBox.Dock = DockStyle.Fill;
        _summaryTextBox.Multiline = true;
        _summaryTextBox.ReadOnly = true;
        _summaryTextBox.BorderStyle = BorderStyle.None;
        _summaryTextBox.BackColor = Color.White;
        _summaryTextBox.Font = new Font("Segoe UI", 11F);
        page.Controls.Add(_summaryTextBox);
        return page;
    }

    private TabPage BuildTagsTab()
    {
        var page = new TabPage("Etiketler ve Long-Tail") { BackColor = Color.White, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.Controls.Add(BuildMetricSection("En sik kullanilan etiketler", _tagsGrid), 0, 0);
        layout.Controls.Add(BuildMetricSection("Baslik kelimeleri", _termsGrid), 1, 0);
        layout.Controls.Add(BuildMetricSection("Long-tail onerileri", _longTailGrid), 2, 0);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildProductsTab()
    {
        var page = new TabPage("Guclu Urunler") { BackColor = Color.White, Padding = new Padding(8) };
        ConfigureBaseGrid(_productsGrid);
        _productsGrid.AutoGenerateColumns = false;
        _productsGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _productsGrid.RowTemplate.MinimumHeight = 76;
        _productsGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _productsGrid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(KeywordProductRow.Thumbnail),
            Width = 90,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null },
        });
        AddProductColumn("Urun basligi", nameof(KeywordProductRow.Title), 320, fill: true);
        AddProductColumn("Fiyat", nameof(KeywordProductRow.PriceDisplay), 105);
        AddProductColumn("Magaza", nameof(KeywordProductRow.ShopName), 145);
        AddProductColumn("Magaza satisi", nameof(KeywordProductRow.ShopSalesDisplay), 105);
        AddProductColumn("Favori", nameof(KeywordProductRow.Favorites), 72);
        AddProductColumn("Goruntulenme", nameof(KeywordProductRow.ViewsDisplay), 105);
        AddProductColumn("SEO", nameof(KeywordProductRow.SeoScore), 62);
        AddProductColumn("Pazar", nameof(KeywordProductRow.MarketScore), 66);
        _productsGrid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = "ListingLink",
            HeaderText = "Listing",
            Text = "Ac",
            UseColumnTextForLinkValue = true,
            Width = 70,
            TrackVisitedState = false,
        });
        _productsGrid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = "ShopLink",
            HeaderText = "Magaza",
            Text = "Ac",
            UseColumnTextForLinkValue = true,
            Width = 70,
            TrackVisitedState = false,
        });
        _productsGrid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            var row = _productsGrid.Rows[e.RowIndex].DataBoundItem as KeywordProductRow;
            if (_productsGrid.Columns[e.ColumnIndex].Name == "ListingLink") OpenUrl(row?.ListingUrl);
            if (_productsGrid.Columns[e.ColumnIndex].Name == "ShopLink") OpenUrl(row?.ShopUrl);
        };
        _productsGrid.CellDoubleClick += (_, _) => OpenUrl(SelectedProduct?.ListingUrl);
        page.Controls.Add(_productsGrid);
        return page;
    }

    private TabPage BuildComparisonTab()
    {
        var page = new TabPage("Kelime Karsilastirma") { BackColor = Color.White, Padding = new Padding(8) };
        ConfigureBaseGrid(_comparisonGrid);
        _comparisonGrid.AutoGenerateColumns = false;
        AddComparisonColumn("Anahtar kelime", nameof(KeywordComparisonRow.Keyword), 230, fill: true);
        AddComparisonColumn("Toplam sonuc", nameof(KeywordComparisonRow.TotalResultsDisplay), 120);
        AddComparisonColumn("Orneklem", nameof(KeywordComparisonRow.SampleSize), 90);
        AddComparisonColumn("Medyan fiyat", nameof(KeywordComparisonRow.MedianPriceDisplay), 125);
        AddComparisonColumn("Rekabet", nameof(KeywordComparisonRow.CompetitionDisplay), 100);
        AddComparisonColumn("Talep", nameof(KeywordComparisonRow.DemandDisplay), 100);
        AddComparisonColumn("Firsat", nameof(KeywordComparisonRow.OpportunityDisplay), 100);
        AddComparisonColumn("Guven", nameof(KeywordComparisonRow.ConfidenceDisplay), 100);
        page.Controls.Add(_comparisonGrid);
        return page;
    }

    private async Task AnalyzeAsync()
    {
        try
        {
            _analyzeButton.Enabled = false;
            _analyzeButton.Text = "Analiz ediliyor...";
            _statusLabel.Text = "Etsy verileri aliniyor ve puanlar hesaplaniyor...";
            _result = await _useCase.ExecuteAsync(_keywordTextBox.Text, (int)_limitInput.Value);
            BindResult(_result);
            AddComparison(_result);
            _statusLabel.Text = $"{_result.Keyword} analizi tamamlandi | {_result.AnalyzedAt:dd.MM.yyyy HH:mm}";
            _ = LoadThumbnailsAsync(_productRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Anahtar kelime analizi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Analiz basarisiz";
        }
        finally
        {
            _analyzeButton.Enabled = true;
            _analyzeButton.Text = "Analiz Et";
        }
    }

    private void BindResult(KeywordAnalysisResult result)
    {
        SetKpi("results", result.TotalResults.ToString("N0"));
        SetKpi("sample", result.SampleSize.ToString("N0"));
        SetKpi("price", FormatPrice(result.MedianPrice, result.PriceCurrency));
        SetKpi("seo", $"{result.AverageSeoScore} / 100");
        SetKpi("competition", $"{result.CompetitionScore} / 100");
        SetKpi("demand", $"{result.DemandSignalScore} / 100");
        SetKpi("opportunity", $"{result.OpportunityScore} / 100");
        SetKpi("confidence", $"{result.ConfidenceScore} / 100");

        _summaryTextBox.Text =
            $"ANAHTAR KELIME: {result.Keyword}{Environment.NewLine}{Environment.NewLine}" +
            $"FIYAT DAGILIMI{Environment.NewLine}" +
            $"En dusuk: {FormatPrice(result.MinimumPrice, result.PriceCurrency)} | En yuksek: {FormatPrice(result.MaximumPrice, result.PriceCurrency)}{Environment.NewLine}" +
            $"Ortalama: {FormatPrice(result.AveragePrice, result.PriceCurrency)} | Medyan: {FormatPrice(result.MedianPrice, result.PriceCurrency)}{Environment.NewLine}{Environment.NewLine}" +
            $"ETKILESIM SINYALLERI{Environment.NewLine}" +
            $"Medyan favori: {result.MedianFavorites:N1} | Medyan goruntulenme: {result.MedianViews:N1}{Environment.NewLine}" +
            $"Ortalama SEO: {result.AverageSeoScore}/100 | Veri guveni: {result.ConfidenceScore}/100{Environment.NewLine}{Environment.NewLine}" +
            $"YORUM{Environment.NewLine}{BuildInterpretation(result)}{Environment.NewLine}{Environment.NewLine}" +
            $"* Rekabet, talep ve firsat degerleri Etsy'nin resmi puanlari degildir. Incelenen orneklemden uretilen karar destek sinyalleridir.";

        _tagsGrid.DataSource = result.TopTags.ToList();
        _termsGrid.DataSource = result.TopTitleTerms.ToList();
        _longTailGrid.DataSource = result.LongTailSuggestions.ToList();
        _productRows = result.Listings
            .OrderByDescending(item => item.MarketScore)
            .ThenByDescending(item => item.Favorites)
            .Select(item => new KeywordProductRow(item))
            .ToList();
        _productsGrid.DataSource = _productRows;
    }

    private void AddComparison(KeywordAnalysisResult result)
    {
        _comparisons.RemoveAll(item => item.Keyword.Equals(result.Keyword, StringComparison.CurrentCultureIgnoreCase));
        _comparisons.Add(new KeywordComparisonRow(result));
        while (_comparisons.Count > 4)
        {
            _comparisons.RemoveAt(0);
        }
        _comparisonGrid.DataSource = null;
        _comparisonGrid.DataSource = _comparisons.ToList();
    }

    private async Task LoadThumbnailsAsync(IEnumerable<KeywordProductRow> rows)
    {
        using var semaphore = new SemaphoreSlim(6);
        var tasks = rows.Where(row => !string.IsNullOrWhiteSpace(row.ImageUrl)).Select(async row =>
        {
            await semaphore.WaitAsync();
            try
            {
                var bytes = await _imageClient.GetByteArrayAsync(row.ImageUrl);
                using var stream = new MemoryStream(bytes);
                using var image = Image.FromStream(stream);
                row.Thumbnail = new Bitmap(image);
            }
            catch
            {
                row.Thumbnail = null;
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

    private void ExportCsv()
    {
        if (_result is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV dosyasi (*.csv)|*.csv",
            FileName = $"anahtar-kelime-{SafeFileName(_result.Keyword)}-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Baslik,Fiyat,Magaza,MagazaSatisi,Favori,Goruntulenme,SEO,PazarPuani,Tagler,ListingLinki,MagazaLinki");
        foreach (var item in _result.Listings)
        {
            builder.AppendLine(string.Join(",", Csv(item.Title), Csv($"{item.CurrencyCode} {item.Price:0.##}"), Csv(item.ShopName), item.ShopSales, item.Favorites, item.Views, item.SeoScore, item.MarketScore, Csv(string.Join(", ", item.Tags)), Csv(item.ListingUrl), Csv(item.ShopUrl)));
        }
        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "Anahtar kelime CSV dosyasi kaydedildi";
    }

    private async Task TrackKeywordAsync()
    {
        if (_result is null)
        {
            MessageBox.Show(this, "Once anahtar kelime analizi yapin.", "Takibe Ekle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _trackingService.TrackAsync(new TrackingCapture(
            TrackingEntityType.Keyword,
            _result.Keyword.Trim().ToLowerInvariant(),
            _result.Keyword,
            $"https://www.etsy.com/search?q={Uri.EscapeDataString(_result.Keyword)}",
            new TrackingSnapshot(
                0,
                0,
                DateTimeOffset.Now,
                null,
                _result.PriceCurrency,
                null,
                null,
                null,
                null,
                null,
                _result.AverageSeoScore,
                null,
                _result.DemandSignalScore,
                _result.CompetitionScore,
                _result.OpportunityScore,
                _result.TotalResults,
                _result.SampleSize)));
        _statusLabel.Text = "Anahtar kelime takibe eklendi ve snapshot kaydedildi";
    }

    private void AddKpi(TableLayoutPanel parent, int column, int row, string title, string key)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(column == 0 ? 0 : 5, 2, column == 3 ? 0 : 5, 4),
            Padding = new Padding(12, 5, 12, 5),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, ForeColor = Color.FromArgb(82, 93, 110) }, 0, 0);
        var value = new Label
        {
            Dock = DockStyle.Fill,
            Text = "-",
            Font = new Font("Segoe UI Semibold", 12.5F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(value, 0, 1);
        _kpiValues[key] = value;
        parent.Controls.Add(panel, column, row);
    }

    private static Control BuildMetricSection(string title, DataGridView grid)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI Semibold", 12F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        ConfigureBaseGrid(grid);
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Deger", DataPropertyName = nameof(KeywordFrequencyMetric.Value), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Urun", DataPropertyName = nameof(KeywordFrequencyMetric.ListingCount), Width = 72 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Oran", DataPropertyName = nameof(KeywordFrequencyMetric.PercentageDisplay), Width = 72 });
        panel.Controls.Add(grid, 0, 1);
        return panel;
    }

    private static void ConfigureBaseGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
    }

    private void AddProductColumn(string title, string property, int width, bool fill = false) =>
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            DataPropertyName = property,
            Width = width,
            FillWeight = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });

    private void AddComparisonColumn(string title, string property, int width, bool fill = false) =>
        _comparisonGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });

    private void SetKpi(string key, string value) => _kpiValues[key].Text = value;
    private static string FormatPrice(decimal value, string currency) => value > 0 ? $"{currency} {value:0.##}" : "Veri yok";

    private static string BuildInterpretation(KeywordAnalysisResult result)
    {
        var demand = result.DemandSignalScore >= 65 ? "Talep sinyali guclu" : result.DemandSignalScore >= 40 ? "Talep sinyali orta" : "Talep sinyali zayif";
        var competition = result.CompetitionScore >= 70 ? "rekabet yuksek" : result.CompetitionScore >= 40 ? "rekabet orta" : "rekabet dusuk";
        var opportunity = result.OpportunityScore >= 65 ? "firsat arastirmaya deger" : result.OpportunityScore >= 40 ? "firsat dikkatli incelenmeli" : "firsat sinirli";
        return $"{demand}, {competition}; {opportunity}. Son karar vermeden once ilk urunleri, fiyatlari ve marka/telif riskini inceleyin.";
    }

    private static Label CreateLabel(string text) => new() { Dock = DockStyle.Fill, Text = text, TextAlign = ContentAlignment.MiddleLeft };
    private static Button CreateButton(string text) { var button = new Button(); ConfigureButton(button, text); return button; }
    private static void ConfigureButton(Button button, string text)
    {
        button.Dock = DockStyle.Fill;
        button.Text = text;
        button.BackColor = Color.FromArgb(32, 97, 165);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(6, 2, 0, 2);
    }

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string SafeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed class KeywordProductRow(KeywordListingSnapshot listing)
    {
        public Image? Thumbnail { get; set; }
        public string ImageUrl => listing.ImageUrl;
        public string Title => listing.Title;
        public string PriceDisplay => $"{listing.CurrencyCode} {listing.Price:0.##}";
        public string ShopName => listing.ShopName;
        public string ShopSalesDisplay => listing.ShopSales > 0 ? listing.ShopSales.ToString("N0") : "Veri yok";
        public int Favorites => listing.Favorites;
        public string ViewsDisplay => listing.Views > 0 ? listing.Views.ToString("N0") : "Veri yok";
        public int SeoScore => listing.SeoScore;
        public int MarketScore => listing.MarketScore;
        public string ListingUrl => listing.ListingUrl;
        public string ShopUrl => listing.ShopUrl;
    }

    private sealed class KeywordComparisonRow(KeywordAnalysisResult result)
    {
        public string Keyword => result.Keyword;
        public string TotalResultsDisplay => result.TotalResults.ToString("N0");
        public int SampleSize => result.SampleSize;
        public string MedianPriceDisplay => FormatPrice(result.MedianPrice, result.PriceCurrency);
        public string CompetitionDisplay => $"{result.CompetitionScore}/100";
        public string DemandDisplay => $"{result.DemandSignalScore}/100";
        public string OpportunityDisplay => $"{result.OpportunityScore}/100";
        public string ConfidenceDisplay => $"{result.ConfidenceScore}/100";
    }
}
