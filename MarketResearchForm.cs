namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class MarketResearchForm : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly AnalyzeKeywordUseCase _analyzeKeywordUseCase;
    private readonly TrackingService _trackingService;
    private readonly ListingOptimizationHistoryService _optimizationHistoryService;
    private readonly IAiListingOptimizer _aiListingOptimizer;
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private List<MarketListingResult> _results = [];

    private readonly TextBox _keywordTextBox = new();
    private readonly NumericUpDown _limitInput = new();
    private readonly ComboBox _sortComboBox = new();
    private readonly Button _searchButton = new();
    private readonly DataGridView _grid = new();
    private readonly PictureBox _pictureBox = new();
    private readonly Label _imageIndexLabel = new();
    private readonly TextBox _detailTextBox = new();
    private readonly Label _statusLabel = new();
    private int _currentImageIndex;
    private bool _favoriteSortDescending;

    public MarketResearchForm(
        AnalyzeKeywordUseCase analyzeKeywordUseCase,
        TrackingService trackingService,
        ListingOptimizationHistoryService optimizationHistoryService,
        IAiListingOptimizer aiListingOptimizer)
    {
        _analyzeKeywordUseCase = analyzeKeywordUseCase;
        _trackingService = trackingService;
        _optimizationHistoryService = optimizationHistoryService;
        _aiListingOptimizer = aiListingOptimizer;
        BuildLayout();
    }

    private MarketListingResult? SelectedListing => _bindingSource.Current as MarketListingResult;

    private void BuildLayout()
    {
        Text = "Etsy Pazar Arastirma ve SEO Araci";
        StartPosition = FormStartPosition.CenterScreen;
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 245));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Pazar Arastirma",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Anahtar kelime girip arama yapin";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1 };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));

        searchPanel.Controls.Add(CreateLabel("Anahtar kelime"), 0, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "ornek: omnitrix, sword display, cosplay prop";
        _keywordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        };
        searchPanel.Controls.Add(_keywordTextBox, 1, 0);

        searchPanel.Controls.Add(CreateLabel("Sonuc"), 2, 0);
        _limitInput.Dock = DockStyle.Fill;
        _limitInput.Minimum = 10;
        _limitInput.Maximum = 100;
        _limitInput.Increment = 10;
        _limitInput.Value = 30;
        searchPanel.Controls.Add(_limitInput, 3, 0);

        searchPanel.Controls.Add(CreateLabel("Sirala"), 4, 0);
        _sortComboBox.Dock = DockStyle.Fill;
        _sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortComboBox.Items.AddRange(["Pazar puani", "SEO puani", "Magaza satisi", "Favori", "Goruntulenme", "Dusuk fiyat"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => ApplySort();
        searchPanel.Controls.Add(_sortComboBox, 5, 0);

        ConfigureButton(_searchButton, "Etsy'de Ara");
        _searchButton.Click += async (_, _) => await SearchAsync();
        searchPanel.Controls.Add(_searchButton, 6, 0);

        var apiButton = CreateButton("API Ayarlari");
        apiButton.Click += (_, _) =>
        {
            using var form = new EtsyApiSettingsForm();
            form.ShowDialog(this);
        };
        searchPanel.Controls.Add(apiButton, 7, 0);

        var plannerButton = CreateButton("Planlayici");
        plannerButton.Click += (_, _) =>
        {
            using var form = new Form1();
            form.ShowDialog(this);
        };
        searchPanel.Controls.Add(plannerButton, 8, 0);

        var trackingButton = CreateButton("Takip Merkezi");
        trackingButton.Click += (_, _) => OpenTrackingCenter();
        searchPanel.Controls.Add(trackingButton, 9, 0);
        root.Controls.Add(searchPanel, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 12, 0, 0),
        };
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));

        var imageSlider = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        imageSlider.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        imageSlider.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _pictureBox.BackColor = Color.White;
        _pictureBox.BorderStyle = BorderStyle.FixedSingle;
        _pictureBox.InitialImage = null;
        _pictureBox.ErrorImage = null;
        imageSlider.Controls.Add(_pictureBox, 0, 0);

        var sliderControls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliderControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var previousButton = CreateButton("<");
        previousButton.Margin = new Padding(0, 3, 4, 0);
        previousButton.Click += (_, _) => ShowPreviousImage();
        sliderControls.Controls.Add(previousButton, 0, 0);
        _imageIndexLabel.Dock = DockStyle.Fill;
        _imageIndexLabel.Text = "Resim yok";
        _imageIndexLabel.TextAlign = ContentAlignment.MiddleCenter;
        sliderControls.Controls.Add(_imageIndexLabel, 1, 0);
        var nextButton = CreateButton(">");
        nextButton.Margin = new Padding(4, 3, 0, 0);
        nextButton.Click += (_, _) => ShowNextImage();
        sliderControls.Controls.Add(nextButton, 2, 0);
        imageSlider.Controls.Add(sliderControls, 0, 1);
        detailPanel.Controls.Add(imageSlider, 0, 0);

        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.BackColor = Color.White;
        detailPanel.Controls.Add(_detailTextBox, 1, 0);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(4, 0, 0, 0),
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        actions.Controls.Add(ActionButton("Listing Ac", OpenListing), 0, 0);
        actions.Controls.Add(ActionButton("Takibe Ekle", () => _ = TrackSelectedListingAsync()), 1, 0);
        actions.Controls.Add(ActionButton("AI Optimizasyon", OpenListingOptimization), 2, 0);

        actions.Controls.Add(ActionButton("Kelime Analizi", OpenKeywordAnalysis), 0, 1);
        actions.Controls.Add(ActionButton("Rakip Analizi", OpenCompetitorAnalysis), 1, 1);
        actions.Controls.Add(ActionButton("Magaza Ac", OpenShop), 2, 1);

        actions.Controls.Add(ActionButton("Tagleri Kopyala", CopyTags), 0, 2);
        actions.Controls.Add(ActionButton("Basligi Kopyala", CopyTitle), 1, 2);
        actions.Controls.Add(ActionButton("CSV Aktar", ExportCsv), 2, 2);

        // 4. satır — Listing Klonlama
        var cloneButton = ActionButton("Listing Klonla", OpenListingClone);
        cloneButton.BackColor = Color.FromArgb(20, 126, 76);   // yeşil — birincil aksiyon
        cloneButton.ForeColor = Color.White;
        actions.Controls.Add(cloneButton, 0, 3);
        actions.Controls.Add(ActionButton("Aciklamay\u0131 Kopyala", CopyDescription), 1, 3);
        actions.Controls.Add(ActionButton("Linki Kopyala", CopyListingUrl), 2, 3);

        detailPanel.Controls.Add(actions, 2, 0);
        root.Controls.Add(detailPanel, 0, 3);

        UiStyle.AttachSidebarNav(this, "research");
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.RowTemplate.MinimumHeight = 76;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => UpdateDetail();
        _grid.CellDoubleClick += (_, _) => OpenListing();
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(MarketListingResult.Favorites))
            {
                ApplyFavoriteSort();
            }
        };
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ShopUrlColumn")
            {
                OpenShop();
            }
        };

        var imageColumn = new DataGridViewImageColumn
        {
            Name = "ImageColumn",
            HeaderText = "Resim",
            DataPropertyName = nameof(MarketListingResult.ThumbnailImage),
            Width = 90,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null },
        };
        _grid.Columns.Add(imageColumn);
        AddColumn("Sira", nameof(MarketListingResult.ListingRank), 54);
        AddColumn("Urun basligi", nameof(MarketListingResult.Title), 290, fill: true);
        AddColumn("Fiyat", nameof(MarketListingResult.PriceDisplay), 105);
        AddColumn("Magaza", nameof(MarketListingResult.ShopName), 150);
        _grid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = "ShopUrlColumn",
            HeaderText = "Etsy magaza linki",
            DataPropertyName = nameof(MarketListingResult.ShopUrl),
            Width = 270,
            TrackVisitedState = false,
        });
        AddColumn("Magaza satisi", nameof(MarketListingResult.ShopSalesDisplay), 105);
        AddColumn("Favori", nameof(MarketListingResult.Favorites), 75);
        _grid.Columns[_grid.Columns.Count - 1].SortMode = DataGridViewColumnSortMode.Programmatic;
        AddColumn("Goruntulenme", nameof(MarketListingResult.ViewsDisplay), 105);
        AddColumn("SEO", nameof(MarketListingResult.SeoScore), 65);
        AddColumn("Pazar puani", nameof(MarketListingResult.MarketScore), 95);
        AddColumn("Tagler", nameof(MarketListingResult.TagsDisplay), 250, fill: true);
    }

    private async Task SearchAsync()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Aranacak anahtar kelimeyi girin.", "Arama", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _searchButton.Enabled = false;
            _searchButton.Text = "Araniyor...";
            _statusLabel.Text = "Etsy API sonuclari getiriliyor...";
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials)
            {
                using var form = new EtsyApiSettingsForm();
                form.ShowDialog(this);
                settings = EtsyApiSettingsStore.Load();
            }

            _results = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            ApplySort();
            _ = LoadThumbnailsAsync(_results);
            _statusLabel.Text = $"{_results.Count} urun bulundu | Urun satis adedi yerine magaza satisi gosterilir";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy API hatasi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Arama basarisiz";
        }
        finally
        {
            _searchButton.Enabled = true;
            _searchButton.Text = "Etsy'de Ara";
        }
    }

    private void ApplySort()
    {
        _favoriteSortDescending = false;
        IEnumerable<MarketListingResult> sorted = _sortComboBox.SelectedItem?.ToString() switch
        {
            "SEO puani" => _results.OrderByDescending(item => item.SeoScore),
            "Magaza satisi" => _results.OrderByDescending(item => item.ShopSales),
            "Favori" => _results.OrderByDescending(item => item.Favorites),
            "Goruntulenme" => _results.OrderByDescending(item => item.Views),
            "Dusuk fiyat" => _results.OrderBy(item => item.Price),
            _ => _results.OrderByDescending(item => item.MarketScore),
        };

        BindResults(sorted.ToList());
        ClearSortGlyphs();
    }

    private void ApplyFavoriteSort()
    {
        _favoriteSortDescending = !_favoriteSortDescending;
        var sorted = _favoriteSortDescending
            ? _results.OrderByDescending(item => item.Favorites).ThenByDescending(item => item.Views)
            : _results.OrderBy(item => item.Favorites).ThenBy(item => item.Views);
        BindResults(sorted.ToList());
        ClearSortGlyphs();

        var favoriteColumn = _grid.Columns
            .Cast<DataGridViewColumn>()
            .First(column => column.DataPropertyName == nameof(MarketListingResult.Favorites));
        favoriteColumn.HeaderCell.SortGlyphDirection = _favoriteSortDescending
            ? SortOrder.Descending
            : SortOrder.Ascending;
        _statusLabel.Text = _favoriteSortDescending
            ? $"{_results.Count} urun | Favori: coktan aza"
            : $"{_results.Count} urun | Favori: azdan coga";
    }

    private void BindResults(List<MarketListingResult> list)
    {
        for (var index = 0; index < list.Count; index++)
        {
            list[index].ListingRank = index + 1;
        }

        _bindingSource.DataSource = list;
        _bindingSource.Position = list.Count > 0 ? 0 : -1;
        UpdateDetail();
    }

    private void ClearSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }
    }

    private void UpdateDetail()
    {
        var item = SelectedListing;
        if (item is null)
        {
            _detailTextBox.Text = "Arama sonucu secilmedi.";
            ClearPicture();
            return;
        }

        _currentImageIndex = 0;
        _ = EnsureImagesAndShowAsync(item);
        _detailTextBox.Text =
            $"BASLIK{Environment.NewLine}{item.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"MAGAZA / FIYAT{Environment.NewLine}{item.ShopName} | {item.PriceDisplay}{Environment.NewLine}" +
            $"Magaza toplam satisi: {item.ShopSalesDisplay} | Yorum: {item.ReviewCount:N0} | Ortalama: {item.ReviewAverage:0.0}{Environment.NewLine}" +
            $"Favori: {item.Favorites:N0} | Goruntulenme: {item.ViewsDisplay} | Stok: {item.Quantity}{Environment.NewLine}{Environment.NewLine}" +
            $"SEO / PAZAR{Environment.NewLine}SEO puani: {item.SeoScore}/100 | Pazar puani: {item.MarketScore}/100{Environment.NewLine}" +
            $"Urun satis adedi: {item.ProductSalesDisplay}{Environment.NewLine}{Environment.NewLine}" +
            $"TAGLER ({item.Tags.Count}){Environment.NewLine}{item.TagsDisplay}{Environment.NewLine}{Environment.NewLine}" +
            $"ACIKLAMA{Environment.NewLine}{TrimDescription(item.Description)}";
    }

    private void OpenListing() => OpenUrl(SelectedListing?.ListingUrl);
    private void OpenShop() => OpenUrl(SelectedListing?.ShopUrl);

    private void OpenKeywordAnalysis()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Analiz edilecek anahtar kelimeyi girin.", "Kelime Analizi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new KeywordOpportunityAnalysisForm(_analyzeKeywordUseCase, _trackingService, keyword);
        form.ShowDialog(this);
    }

    private void OpenCompetitorAnalysis()
    {
        var listing = SelectedListing;
        if (listing is null || listing.ShopId <= 0)
        {
            MessageBox.Show(this, "Rakip analizi icin magaza bilgisi bulunan bir urun secin.", "Rakip Analizi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new CompetitorShopAnalysisForm(listing, _trackingService);
        form.ShowDialog(this);
    }

    private void OpenListingOptimization()
    {
        var listing = SelectedListing;
        if (listing is null)
        {
            MessageBox.Show(this, "Optimizasyon icin bir urun secin.", "AI Optimizasyon", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ListingOptimizationForm(
            _optimizationHistoryService,
            _aiListingOptimizer,
            listing,
            _keywordTextBox.Text.Trim());
        form.ShowDialog(this);
    }

    private async Task TrackSelectedListingAsync()
    {
        var item = SelectedListing;
        if (item is null)
        {
            MessageBox.Show(this, "Takip edilecek urunu secin.", "Takibe Ekle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _trackingService.TrackAsync(new TrackingCapture(
            TrackingEntityType.Listing,
            item.ListingId.ToString(CultureInfo.InvariantCulture),
            item.Title,
            item.ListingUrl,
            new TrackingSnapshot(
                0,
                0,
                DateTimeOffset.Now,
                item.Price,
                item.CurrencyCode,
                item.Favorites,
                item.Views,
                item.ShopSales,
                item.ReviewCount,
                item.ReviewAverage,
                item.SeoScore,
                item.MarketScore)));
        _statusLabel.Text = "Urun takip listesine eklendi ve snapshot kaydedildi";
    }

    private void OpenTrackingCenter()
    {
        using var form = new TrackingHistoryForm(_trackingService);
        form.ShowDialog(this);
    }

    private void CopyTags() => CopyText(SelectedListing?.TagsDisplay, "Tagler kopyalandi");
    private void CopyTitle() => CopyText(SelectedListing?.Title, "Baslik kopyalandi");
    private void CopyDescription() => CopyText(SelectedListing?.Description, "Aciklama kopyalandi");
    private void CopyListingUrl() => CopyText(SelectedListing?.ListingUrl, "Listing linki kopyalandi");

    private void OpenListingClone()
    {
        var listing = SelectedListing;
        if (listing is null)
        {
            MessageBox.Show(
                this,
                "Klonlanacak bir listing secin.",
                "Listing Klonla",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // ProductDiscoveryListingCreatorForm'u initialListing ile ac:
        // form baslik, tag, aciklama, fiyat ve kategori alanlarini otomatik doldurur.
        using var form = new ProductDiscoveryListingCreatorForm(
            _aiListingOptimizer,
            initialKeyword: _keywordTextBox.Text.Trim(),
            initialListing: listing,
            historyService: _optimizationHistoryService);
        form.ShowDialog(this);
    }

    private async Task EnsureImagesAndShowAsync(MarketListingResult item)
    {
        if (item.ImageUrls.Count == 0)
        {
            try
            {
                var settings = EtsyApiSettingsStore.Load();
                item.ImageUrls = await _apiClient.GetListingImagesAsync(settings, item.ListingId);
            }
            catch
            {
                item.ImageUrls = [];
            }
        }

        if (SelectedListing?.ListingId != item.ListingId)
        {
            return;
        }

        ShowCurrentImage();
        if (item.ThumbnailImage is null && item.ImageUrls.Count > 0)
        {
            item.ThumbnailImage = await DownloadImageAsync(item.ImageUrls[0]);
            _grid.Refresh();
        }
    }

    private async Task LoadThumbnailsAsync(IEnumerable<MarketListingResult> items)
    {
        using var semaphore = new SemaphoreSlim(6);
        var tasks = items.Where(item => item.ImageUrls.Count > 0).Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                item.ThumbnailImage = await DownloadImageAsync(item.ImageUrls[0]);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (!IsDisposed)
        {
            BeginInvoke(_grid.Refresh);
        }
    }

    private async Task<Image?> DownloadImageAsync(string url)
    {
        try
        {
            var bytes = await _imageHttpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private void ShowPreviousImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0)
        {
            return;
        }

        _currentImageIndex = (_currentImageIndex - 1 + images.Count) % images.Count;
        ShowCurrentImage();
    }

    private void ShowNextImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0)
        {
            return;
        }

        _currentImageIndex = (_currentImageIndex + 1) % images.Count;
        ShowCurrentImage();
    }

    private void ShowCurrentImage()
    {
        var images = SelectedListing?.ImageUrls;
        if (images is null || images.Count == 0)
        {
            ClearPicture();
            _imageIndexLabel.Text = "Resim yok";
            return;
        }

        _currentImageIndex = Math.Clamp(_currentImageIndex, 0, images.Count - 1);
        LoadPicture(images[_currentImageIndex]);
        _imageIndexLabel.Text = $"{_currentImageIndex + 1} / {images.Count}";
    }

    private void LoadPicture(string imageUrl)
    {
        ClearPicture();
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        try
        {
            _pictureBox.LoadAsync(imageUrl);
        }
        catch
        {
            ClearPicture();
        }
    }

    private void ClearPicture()
    {
        _pictureBox.CancelAsync();
        _pictureBox.ImageLocation = null;
        _pictureBox.Image = null;
    }

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void CopyText(string? text, string status)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            _statusLabel.Text = status;
        }
    }

    private void ExportCsv()
    {
        if (_results.Count == 0)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV dosyasi (*.csv)|*.csv",
            FileName = $"etsy-pazar-arastirma-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Baslik,Fiyat,Magaza,MagazaSatisi,Favori,Goruntulenme,SEO,PazarPuani,Tagler,ListingLinki,MagazaLinki");
        foreach (var item in _results)
        {
            builder.AppendLine(string.Join(",", Csv(item.Title), Csv(item.PriceDisplay), Csv(item.ShopName), item.ShopSales, item.Favorites, item.Views, item.SeoScore, item.MarketScore, Csv(item.TagsDisplay), Csv(item.ListingUrl), Csv(item.ShopUrl)));
        }
        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "CSV kaydedildi";
    }

    private void AddColumn(string header, string property, int width, bool fill = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            FillWeight = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = UiStyle.TextDark,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button();
        ConfigureButton(button, text, isSecondary);
        return button;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = CreateButton(text);
        button.Margin = new Padding(3, 3, 3, 3);
        button.Font = new Font("Segoe UI Semibold", 8.5F);
        button.Click += (_, _) => action();
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
        button.Margin = new Padding(6, 3, 0, 6);
    }

    private static string TrimDescription(string description) =>
        description.Length <= 900 ? description : description[..900] + "...";

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
