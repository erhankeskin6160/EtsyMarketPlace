namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class ProductDiscoveryListingCreatorForm(
    IAiListingOptimizer aiOptimizer,
    string? initialKeyword = null,
    MarketListingResult? initialListing = null,
    ListingOptimizationHistoryService? historyService = null) : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly AiListingImageGenerator _imageGenerator = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _shopTypeTextBox = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly TextBox _listingLinkTextBox = new();
    private readonly TextBox _includeTextBox = new();
    private readonly TextBox _excludeTextBox = new();
    private readonly TextBox _titleTextBox = new();
    private readonly TextBox _descriptionTextBox = new();
    private readonly TextBox _tagsTextBox = new();
    private readonly TextBox _materialsTextBox = new();
    private readonly TextBox _variationsTextBox = new();
    private readonly TextBox _imagePromptTextBox = new();
    private readonly TextBox _imagePathTextBox = new();
    private readonly TextBox _notesTextBox = new();
    private readonly NumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 30 };
    private readonly NumericUpDown _priceInput = new() { Minimum = 1, Maximum = 100000, DecimalPlaces = 2, Value = 35 };
    private readonly NumericUpDown _quantityInput = new() { Minimum = 1, Maximum = 999, Value = 1 };
    private readonly NumericUpDown _imageCountInput = new() { Minimum = 1, Maximum = 10, Value = 1 };
    private readonly TextBox _taxonomyInput = new();
    private readonly TextBox _categoryTextBox = new() { ReadOnly = true };
    private readonly ComboBox _listingTypeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _shippingProfileComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _readinessStateComboBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly PictureBox _previewPictureBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _imageCounterLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Panel _busyOverlay = new() { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(235, 247, 248, 250) };
    private readonly Label _busyLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 12F), ForeColor = Color.FromArgb(23, 32, 49) };
    private readonly ProgressBar _busyProgressBar = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28 };
    private readonly ModernSpinner _busySpinner = new() { Width = 74, Height = 74, Anchor = AnchorStyles.None, BackColor = Color.White };
    private readonly System.Windows.Forms.Timer _busyTimer = new() { Interval = 350 };
    private readonly Label _statusLabel = new();
    private List<IdeaRow> _rows = [];
    private int _selectedImageIndex;
    private int _busyFrame;
    private bool _favoriteSortDescending;
    private bool _opportunitySortDescending;
    private bool _viewsSortDescending;
    private long? _lastCreatedListingId;

    private IdeaRow? SelectedRow => _bindingSource.Current as IdeaRow;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Urun Kesif ve Listing Olusturucu";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1320, 820);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        Controls.Add(root);
        _listingTypeComboBox.Items.AddRange(["Fiziksel urun", "Dijital urun"]);
        _listingTypeComboBox.SelectedIndex = 0;
        _listingTypeComboBox.SelectedIndexChanged += (_, _) => UpdateListingTypeControls();
        _shippingProfileComboBox.DisplayMember = nameof(EtsyShippingProfileOption.DisplayName);
        _shippingProfileComboBox.ValueMember = nameof(EtsyShippingProfileOption.ShippingProfileId);
        _shippingProfileComboBox.DropDown += async (_, _) => await LoadShippingProfilesAsync();
        _readinessStateComboBox.DisplayMember = nameof(EtsyReadinessStateOption.DisplayName);
        _readinessStateComboBox.ValueMember = nameof(EtsyReadinessStateOption.ReadinessStateId);
        _readinessStateComboBox.DropDown += async (_, _) => await LoadReadinessStatesAsync();

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Urun Bul ve Listing Hazirla",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Anahtar kelime girip Etsy'de urun arayin";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildToolbar(), 0, 1);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDraftArea(), 0, 3);
        Controls.Add(BuildBusyOverlay());
        _busyTimer.Tick += (_, _) => UpdateBusyAnimation();
        if (!string.IsNullOrWhiteSpace(initialKeyword))
        {
            _keywordTextBox.Text = initialKeyword.Trim();
        }

        ApplyInitialListing();
    }

    private void ApplyInitialListing()
    {
        if (initialListing is null)
        {
            return;
        }

        _rows = [new IdeaRow(initialListing)];
        _bindingSource.DataSource = _rows;
        _bindingSource.Position = 0;
        FillFromSelectedIdea();
        _statusLabel.Text = "Firsat Motoru secimi listing taslagina aktarildi";
    }

    private Control BuildBusyOverlay()
    {
        var outer = new TableLayoutPanel 
        { 
            Dock = DockStyle.Fill, 
            ColumnCount = 3,
            RowCount = 3,
            BackColor = Color.FromArgb(235, 247, 248, 250) 
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Color.White,
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        _busyLabel.Text = "Etsy'de urun araniyor";
        card.Controls.Add(_busyLabel, 0, 0);
        card.Controls.Add(_busySpinner, 0, 1);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Sonuclar, kategoriler ve gorseller yuklenirken lutfen bekle.",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiStyle.TextMuted,
            Font = UiStyle.BaseFont,
        }, 0, 2);

        outer.Controls.Add(card, 1, 1);
        _busyOverlay.Controls.Add(outer);
        return _busyOverlay;
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 2, Padding = new Padding(0, 10, 0, 10) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        for (var index = 4; index < 9; index++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));

        toolbar.Controls.Add(LabelFor("Anahtar kelime"), 0, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Orn: k sparrow figur, gandalf bust, 3d cosplay prop";
        toolbar.Controls.Add(_keywordTextBox, 1, 0);
        toolbar.Controls.Add(LabelFor("Sonuc"), 2, 0);
        _limitInput.Dock = DockStyle.Left;
        toolbar.Controls.Add(_limitInput, 3, 0);

        var search = CreateButton("Etsy'de Ara");
        search.Click += async (_, _) => await SearchIdeasAsync();
        toolbar.Controls.Add(search, 4, 0);
        var draft = CreateButton("Taslak Uret");
        draft.Click += async (_, _) => await GenerateDraftAsync();
        toolbar.Controls.Add(draft, 5, 0);
        var image = CreateButton("AI Gorsel");
        image.Click += async (_, _) => await GenerateImageAsync();
        toolbar.Controls.Add(image, 6, 0);
        var create = CreateButton("Etsy Taslak Ekle");
        create.BackColor = Color.FromArgb(20, 126, 76);
        create.Click += async (_, _) => await CreateDraftListingAsync();
        toolbar.Controls.Add(create, 7, 0);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(close, 8, 0);

        toolbar.Controls.Add(LabelFor("Etsy listing linki"), 0, 1);
        _listingLinkTextBox.Dock = DockStyle.Fill;
        _listingLinkTextBox.PlaceholderText = "Orn: https://www.etsy.com/listing/123456789/urun-adi";
        toolbar.Controls.Add(_listingLinkTextBox, 1, 1);
        toolbar.SetColumnSpan(_listingLinkTextBox, 3);
        var importLink = CreateButton("Linkten Al");
        importLink.Click += async (_, _) => await ImportListingLinkAsync();
        toolbar.Controls.Add(importLink, 4, 1);
        var aiReview = CreateButton("2. Sayfa");
        aiReview.Click += (_, _) => OpenAiReviewPage();
        toolbar.Controls.Add(aiReview, 5, 1);
        return toolbar;
    }

    private Control BuildDraftArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 10, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        left.Controls.Add(BuildImagePreviewPanel(), 0, 0);
        left.Controls.Add(LabelFor("Baslik"), 0, 1);
        _titleTextBox.Dock = DockStyle.Fill;
        _titleTextBox.Multiline = true;
        left.Controls.Add(_titleTextBox, 0, 2);
        left.Controls.Add(LabelFor("Aciklama"), 0, 3);
        ConfigureMultiline(_descriptionTextBox);
        left.Controls.Add(_descriptionTextBox, 0, 4);
        layout.Controls.Add(left, 0, 0);

        var middle = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8 };
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        middle.Controls.Add(LabelFor("Tagler"), 0, 0);
        ConfigureMultiline(_tagsTextBox);
        middle.Controls.Add(_tagsTextBox, 0, 1);
        middle.Controls.Add(LabelFor("Materyaller"), 0, 2);
        ConfigureMultiline(_materialsTextBox);
        middle.Controls.Add(_materialsTextBox, 0, 3);
        middle.Controls.Add(LabelFor("Varyasyon onerileri"), 0, 4);
        ConfigureMultiline(_variationsTextBox);
        middle.Controls.Add(_variationsTextBox, 0, 5);
        middle.Controls.Add(LabelFor("AI gorsel promptlari (satir satir)"), 0, 6);
        ConfigureMultiline(_imagePromptTextBox);
        middle.Controls.Add(_imagePromptTextBox, 0, 7);
        layout.Controls.Add(middle, 1, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 20, AutoScroll = true };
        for (var index = 0; index < 19; index++)
        {
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, index % 2 == 0 ? 18 : 28));
        }

        right.RowStyles[18] = new RowStyle(SizeType.Absolute, 32);
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(LabelFor("Fiyat"), 0, 0);
        _priceInput.Dock = DockStyle.Fill;
        right.Controls.Add(_priceInput, 0, 1);
        right.Controls.Add(LabelFor("Stok"), 0, 2);
        _quantityInput.Dock = DockStyle.Fill;
        right.Controls.Add(_quantityInput, 0, 3);
        right.Controls.Add(LabelFor("Taxonomy ID"), 0, 4);
        _taxonomyInput.Dock = DockStyle.Fill;
        right.Controls.Add(_taxonomyInput, 0, 5);
        right.Controls.Add(LabelFor("Kategori"), 0, 6);
        _categoryTextBox.Dock = DockStyle.Fill;
        right.Controls.Add(_categoryTextBox, 0, 7);
        right.Controls.Add(LabelFor("Listing tipi"), 0, 8);
        right.Controls.Add(_listingTypeComboBox, 0, 9);
        right.Controls.Add(LabelFor("AI gorsel adedi"), 0, 10);
        _imageCountInput.Dock = DockStyle.Fill;
        right.Controls.Add(_imageCountInput, 0, 11);
        right.Controls.Add(LabelFor("Shipping profile"), 0, 12);
        right.Controls.Add(_shippingProfileComboBox, 0, 13);
        right.Controls.Add(LabelFor("Hazirlik durumu"), 0, 14);
        right.Controls.Add(_readinessStateComboBox, 0, 15);
        right.Controls.Add(LabelFor("Gorsel dosyasi"), 0, 16);
        _imagePathTextBox.Dock = DockStyle.Fill;
        right.Controls.Add(_imagePathTextBox, 0, 17);
        var choose = CreateButton("Dosyadan Sec");
        choose.Click += (_, _) => ChooseImage();
        right.Controls.Add(choose, 0, 18);
        _notesTextBox.Dock = DockStyle.Fill;
        _notesTextBox.Multiline = true;
        _notesTextBox.ReadOnly = true;
        right.Controls.Add(_notesTextBox, 0, 19);
        layout.Controls.Add(right, 2, 0);
        UpdateListingTypeControls();
        return layout;
    }

    private Control BuildImagePreviewPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 0, 8, 8) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        panel.Controls.Add(_previewPictureBox, 0, 0);
        panel.SetColumnSpan(_previewPictureBox, 3);

        var previous = CreateButton("<");
        previous.Click += async (_, _) => await MoveSelectedImageAsync(-1);
        panel.Controls.Add(previous, 0, 1);
        _imageCounterLabel.Text = "Resim yok";
        panel.Controls.Add(_imageCounterLabel, 1, 1);
        var next = CreateButton(">");
        next.Click += async (_, _) => await MoveSelectedImageAsync(1);
        panel.Controls.Add(next, 2, 1);
        return panel;
    }

    private void ConfigureGrid()
    {
        UiStyle.ConfigureBaseGrid(_grid);
        _grid.RowTemplate.Height = 68;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => FillFromSelectedIdea();
        _grid.CellDoubleClick += (_, _) => OpenSelectedCompetitorListing();
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            var propName = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (propName == nameof(IdeaRow.Favorites))
            {
                ApplyFavoriteSort();
            }
            else if (propName == nameof(IdeaRow.Opportunity))
            {
                ApplyOpportunitySort();
            }
            else if (propName == nameof(IdeaRow.Views))
            {
                ApplyViewsSort();
            }
        };
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ListingLink")
            {
                OpenUrl((_grid.Rows[e.RowIndex].DataBoundItem as IdeaRow)?.Listing.ListingUrl);
            }
        };
        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(IdeaRow.Thumbnail),
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            Width = 78,
        });
        AddColumn("Firsat", nameof(IdeaRow.Opportunity), 70);
        AddColumn("Urun fikri", nameof(IdeaRow.Title), 380, true);
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = "Rakip listing",
            Name = "ListingLink",
            Text = "Ac",
            UseColumnTextForButtonValue = true,
            Width = 95,
        });
        AddColumn("Fiyat", nameof(IdeaRow.Price), 90);
        AddColumn("Magaza", nameof(IdeaRow.Shop), 150);
        AddColumn("Magaza satisi", nameof(IdeaRow.ShopSales), 110);
        AddColumn("Favori", nameof(IdeaRow.Favorites), 75);
        AddColumn("Goruntulenme", nameof(IdeaRow.Views), 105);
        AddColumn("SEO", nameof(IdeaRow.Seo), 60);
        AddColumn("Kategori", nameof(IdeaRow.Category), 170);
        AddColumn("Tagler", nameof(IdeaRow.Tags), 320);
    }

    private async Task DetectShopTypeAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Bagli magaza urunlerinden magaza turu algilaniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var shop = await _apiClient.GetOwnShopProfileAsync(settings);
            var listings = await _apiClient.GetOwnShopActiveListingsAsync(settings, 50);
            EtsyApiSettingsStore.Save(settings);
            var keywords = ExtractShopKeywords(listings);
            _shopTypeTextBox.Text = GuessShopType(keywords);
            _keywordTextBox.Text = BuildSearchKeyword(_shopTypeTextBox.Text, keywords);
            ApplyDefaultFiltersForShopType(_shopTypeTextBox.Text);
            _notesTextBox.Text = $"Bagli magaza: {shop.ShopName}{Environment.NewLine}Algilanan kelimeler: {string.Join(", ", keywords.Take(12))}";
            _statusLabel.Text = $"{shop.ShopName} icin magaza turu algilandi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Magaza algilama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Magaza turu algilanamadi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SearchIdeasAsync()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Once anahtar kelime girin.", "Etsy'de ara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            StartBusy("Etsy'de urun araniyor");
            _statusLabel.Text = "Etsy'de anahtar kelimeye gore urun araniyor...";
            _shopTypeTextBox.Text = keyword;
            _includeTextBox.Clear();
            _excludeTextBox.Clear();
            var settings = EtsyApiSettingsStore.Load();
            var results = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            EtsyApiSettingsStore.Save(settings);
            _rows = results
                .Select(item => new IdeaRow(item))
                .OrderByDescending(row => row.Listing.Views)
                .ToList();
            SetBusyMessage("Kategori adlari yukleniyor");
            await EnrichCategoriesAsync(settings);
            SetBusyMessage("Urun gorselleri yukleniyor");
            await LoadGridThumbnailsAsync();
            _bindingSource.DataSource = _rows;
            _statusLabel.Text = $"{_rows.Count} Etsy urunu listelendi";
            FillFromSelectedIdea();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy'de ara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Pazar aramasi basarisiz";
        }
        finally
        {
            StopBusy();
        }
    }

    private async Task ImportListingLinkAsync()
    {
        var link = _listingLinkTextBox.Text.Trim();
        if (!TryExtractListingId(link, out var listingId))
        {
            MessageBox.Show(this, "Gecerli bir Etsy listing linki girin. Ornek: https://www.etsy.com/listing/123456789/urun-adi", "Linkten al", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            StartBusy("Etsy linkinden listing aliniyor");
            ClearDraftFields();
            var settings = EtsyApiSettingsStore.Load();
            var listing = await _apiClient.GetPublicListingAsync(settings, listingId, PrimaryKeyword());
            EtsyApiSettingsStore.Save(settings);

            if (string.IsNullOrWhiteSpace(_keywordTextBox.Text))
            {
                _keywordTextBox.Text = GuessKeywordFromListing(listing);
            }

            _rows = [new IdeaRow(listing)];
            SetBusyMessage("Kategori adi yukleniyor");
            await EnrichCategoriesAsync(settings);
            SetBusyMessage("Urun gorseli yukleniyor");
            await LoadGridThumbnailsAsync();
            _bindingSource.DataSource = _rows;
            _bindingSource.Position = 0;
            FillFromSelectedIdea();
            _variationsTextBox.Text = BuildVariationSuggestions(listing);
            _statusLabel.Text = listing.VariationOptions.Count > 0
                ? "Etsy linkinden listing ve gercek varyasyonlar alindi"
                : "Etsy linkinden listing alindi; bu listingde okunabilir varyasyon bulunamadi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Linkten al", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Listing linkinden veri alinamadi";
        }
        finally
        {
            StopBusy();
        }
    }

    private static bool TryExtractListingId(string value, out long listingId)
    {
        listingId = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(value, @"(?:listing|copy)/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(value, @"(?<id>\d{8,})");
        }

        return match.Success && long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out listingId);
    }

    private void ClearDraftFields()
    {
        _titleTextBox.Clear();
        _descriptionTextBox.Clear();
        _tagsTextBox.Clear();
        _materialsTextBox.Clear();
        _variationsTextBox.Clear();
        _imagePromptTextBox.Clear();
        _imagePathTextBox.Clear();
        _taxonomyInput.Clear();
        _categoryTextBox.Clear();
        _notesTextBox.Clear();
    }

    private static string GuessKeywordFromListing(MarketListingResult listing)
    {
        var tagKeyword = listing.Tags.FirstOrDefault(tag => tag.Length >= 4);
        if (!string.IsNullOrWhiteSpace(tagKeyword))
        {
            return tagKeyword;
        }

        return string.Join(' ', listing.Title
            .Split([' ', '-', '|', ',', '/', '(', ')', ':', ';', '.', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length >= 4)
            .Take(4));
    }

    private async Task GenerateDraftAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once pazardan bir urun fikri secin.", "Taslak uret", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var listing = SelectedRow.Listing;
            var settings = AiOptimizationSettingsStore.Load();
            var input = new ListingOptimizationInput(
                BuildDraftSourceTitle(listing),
                BuildDraftSourceDescription(listing),
                listing.Tags,
                PrimaryKeyword());
            var result = await aiOptimizer.OptimizeAsync(input);
            var materials = EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions);
            _titleTextBox.Text = SelectEnglishTitle(result.TitleSuggestions, listing);
            _descriptionTextBox.Text = SelectEnglishDescription(result.DescriptionDraft, listing, materials);
            _tagsTextBox.Text = string.Join(", ", result.TagSuggestions.Take(13));
            _materialsTextBox.Text = string.Join(", ", materials);
            _variationsTextBox.Text = BuildVariationSuggestions(listing);
            ApplyDraftCategoryRecommendation(listing);
            _imagePromptTextBox.Text = BuildImagePrompt();

            // Validate and auto-repair loop
            var repairService = new ListingDraftRepairService();
            var validationReport = repairService.ValidateDraft(
                _titleTextBox.Text,
                _descriptionTextBox.Text,
                result.TagSuggestions.Take(13).ToList(),
                materials,
                _categoryTextBox.Text,
                PrimaryKeyword());

            var repairLog = new System.Text.StringBuilder();
            var decision = repairService.Evaluate(validationReport);

            if (decision.NeedsRepair && !settings.IsOffline)
            {
                for (var attempt = 0; attempt < ListingDraftRepairService.MaxRepairIterations && decision.NeedsRepair; attempt++)
                {
                    _statusLabel.Text = $"Taslak onariliyor (deneme {attempt + 1}/{ListingDraftRepairService.MaxRepairIterations})...";
                    repairLog.AppendLine($"Onarim denemesi {attempt + 1}: Puan {validationReport.OverallScore}/100, hedef alanlar: {string.Join(", ", decision.RepairTargets)}");

                    var repairPrompt = ListingDraftRepairService.BuildRepairPrompt(
                        decision,
                        _titleTextBox.Text,
                        _descriptionTextBox.Text,
                        result.TagSuggestions.Take(13).ToList(),
                        materials,
                        PrimaryKeyword());

                    var repairInput = new ListingOptimizationInput(
                        _titleTextBox.Text,
                        repairPrompt,
                        result.TagSuggestions.Take(13).ToList(),
                        PrimaryKeyword());

                    try
                    {
                        var repairResult = await aiOptimizer.OptimizeAsync(repairInput);

                        if (decision.RepairTargets.Contains("title") && repairResult.TitleSuggestions.Count > 0)
                            _titleTextBox.Text = SelectEnglishTitle(repairResult.TitleSuggestions, listing);
                        if (decision.RepairTargets.Contains("tags") && repairResult.TagSuggestions.Count > 0)
                            _tagsTextBox.Text = string.Join(", ", repairResult.TagSuggestions.Take(13));
                        if (decision.RepairTargets.Contains("description") && !string.IsNullOrWhiteSpace(repairResult.DescriptionDraft))
                            _descriptionTextBox.Text = SelectEnglishDescription(repairResult.DescriptionDraft, listing, materials);
                        if (decision.RepairTargets.Contains("materials") && repairResult.MaterialSuggestions.Count > 0)
                        {
                            materials = EtsyApiClient.NormalizeListingMaterialsForEtsy(repairResult.MaterialSuggestions);
                            _materialsTextBox.Text = string.Join(", ", materials);
                        }

                        result = repairResult;
                    }
                    catch
                    {
                        repairLog.AppendLine($"Onarim denemesi {attempt + 1} basarisiz, mevcut taslak korunuyor.");
                        break;
                    }

                    validationReport = repairService.ValidateDraft(
                        _titleTextBox.Text,
                        _descriptionTextBox.Text,
                        result.TagSuggestions.Take(13).ToList(),
                        materials,
                        _categoryTextBox.Text,
                        PrimaryKeyword());
                    decision = repairService.Evaluate(validationReport);
                }

                repairLog.AppendLine($"Son puan: {validationReport.OverallScore}/100");
            }

            _notesTextBox.Text =
                $"Taslak kaynagi: {DraftSourceLabel(settings)}. Metin Ingilizce uretildi; kategori taslak icerigine gore yeniden onerildi. Varyasyon alani sadece Etsy listinginden okunan gercek varyasyonlarla doldurulur, AI varyasyon uretmez. Etsy'ye eklemeden once fiyat, stok, taxonomy ve kargo profilini kontrol et.{Environment.NewLine}{Environment.NewLine}" +
                ListingDraftValidator.FormatReport(validationReport) +
                (repairLog.Length > 0 ? $"{Environment.NewLine}{Environment.NewLine}Onarim gecmisi:{Environment.NewLine}{repairLog}" : "");
            _statusLabel.Text = "Listing taslagi uretildi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Taslak uret", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Taslak uretilemedi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task GenerateImageAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir urun fikri secin.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI ile urun gorseli uretiliyor...";
            var settings = AiOptimizationSettingsStore.Load();
            var referencePath = await DownloadReferenceImageAsync(SelectedRow.Listing);
            var paths = new List<string>();
            var prompts = ImagePrompts();
            for (var index = 1; index <= (int)_imageCountInput.Value; index++)
            {
                _statusLabel.Text = $"Referans gorselden AI gorsel duzenleniyor ({index}/{_imageCountInput.Value})...";
                var prompt = PromptForImageIndex(prompts, index);
                var path = await _imageGenerator.GenerateFromReferenceAsync(settings, SelectedRow.Listing, prompt, referencePath);
                paths.Add(path);
            }

            _imagePathTextBox.Text = string.Join("; ", paths);
            _statusLabel.Text = $"{paths.Count} referans gorsel duzenlendi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "AI gorsel uretilemedi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task CreateDraftListingAsync()
    {
        if (!IsDigitalListingSelected() && _shippingProfileComboBox.Items.Count == 0)
        {
            await LoadShippingProfilesAsync();
        }

        if (!IsDigitalListingSelected() && _readinessStateComboBox.Items.Count == 0)
        {
            await LoadReadinessStatesAsync();
        }

        if (!TryBuildDraftRequest(out var draft, out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, "Etsy taslak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var inventory = BuildInventoryUpdate(draft);
        var variationStatus = inventory is not null
            ? "Etsy dropdown olarak eklenecek"
            : string.IsNullOrWhiteSpace(_variationsTextBox.Text)
                ? "Yok"
                : "Sadece aciklama notu olarak eklenecek";

        var confirmationText =
            $"Bu islem Etsy magazanda TASLAK listing olusturacak.{Environment.NewLine}{Environment.NewLine}" +
            $"Baslik: {draft.Title}{Environment.NewLine}" +
            $"Fiyat: {draft.Price:0.00}{Environment.NewLine}" +
            $"Stok: {draft.Quantity}{Environment.NewLine}" +
            $"Taxonomy: {draft.TaxonomyId}{Environment.NewLine}" +
            $"Tip: {(draft.IsDigital ? "Dijital" : "Fiziksel")}{Environment.NewLine}" +
            $"Kargo profili: {(draft.IsDigital ? "Gerekmez" : draft.ShippingProfileId)}{Environment.NewLine}" +
            $"Hazirlik durumu: {(draft.IsDigital ? "Gerekmez" : draft.ReadinessStateId)}{Environment.NewLine}" +
            $"Varyasyon: {variationStatus}{Environment.NewLine}" +
            $"Gorsel: {(SelectedImagePaths().Count == 0 ? "Yok" : $"{SelectedImagePaths().Count} dosya")}{Environment.NewLine}{Environment.NewLine}" +
            "Onayliyor musun?";
        if (MessageBox.Show(this, confirmationText, "Etsy taslak onayi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Etsy'de taslak listing olusturuluyor...";
            var settings = EtsyApiSettingsStore.Load();
            var created = await _apiClient.CreateOwnShopDraftListingAsync(settings, draft);
            _lastCreatedListingId = created.ListingId;
            var variationWarning = "";
            if (inventory is not null)
            {
                try
                {
                    _statusLabel.Text = "Etsy varyasyonlari ekleniyor...";
                    await _apiClient.UpdateOwnShopListingInventoryAsync(settings, created.ListingId, inventory);
                }
                catch (Exception variationEx)
                {
                    variationWarning = variationEx.Message;
                }
            }

            foreach (var imagePath in SelectedImagePaths())
            {
                await _apiClient.UploadOwnShopListingImageAsync(settings, created.ListingId, imagePath);
            }

            EtsyApiSettingsStore.Save(settings);
            _statusLabel.Text = string.IsNullOrWhiteSpace(variationWarning)
                ? $"Taslak listing ve varyasyonlar olusturuldu: #{created.ListingId}"
                : $"Taslak olustu ama varyasyon eklenemedi: #{created.ListingId}";
            var successMessage = string.IsNullOrWhiteSpace(variationWarning)
                ? "Taslak listing olusturuldu. Etsy'de acmak ister misin?"
                : $"Taslak listing olusturuldu ama varyasyonlar Etsy tarafindan kabul edilmedi.{Environment.NewLine}{Environment.NewLine}{variationWarning}{Environment.NewLine}{Environment.NewLine}Etsy'de acmak ister misin?";
            if (MessageBox.Show(this, successMessage, "Etsy taslak", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(created.Url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy taslak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Taslak listing olusturulamadi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OpenAiReviewPage()
    {
        if (historyService is null)
        {
            MessageBox.Show(this, "Bu ekran ana kontrol panelinden acildiginda 2. sayfa baglantisi aktif olur.", "2. Sayfa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new OwnShopListingAiAuditForm(aiOptimizer, historyService, _lastCreatedListingId);
        form.ShowDialog(this);
    }

    private void FillFromSelectedIdea()
    {
        if (SelectedRow is null) return;
        var listing = SelectedRow.Listing;
        _selectedImageIndex = 0;
        _ = ShowSelectedImageAsync();
        if (string.IsNullOrWhiteSpace(_titleTextBox.Text))
        {
            _titleTextBox.Text = BuildSafeTitle(listing);
        }

        if (string.IsNullOrWhiteSpace(_tagsTextBox.Text))
        {
            _tagsTextBox.Text = string.Join(", ", listing.Tags.Take(13));
        }

        if (string.IsNullOrWhiteSpace(_variationsTextBox.Text))
        {
            _variationsTextBox.Text = BuildVariationSuggestions(listing);
        }

        if (string.IsNullOrWhiteSpace(_taxonomyInput.Text) && listing.TaxonomyId > 0)
        {
            _taxonomyInput.Text = listing.TaxonomyId.ToString(CultureInfo.InvariantCulture);
        }

        _categoryTextBox.Text = listing.TaxonomyDisplay;

        if (listing.Price > 0)
        {
            _priceInput.Value = Math.Min(_priceInput.Maximum, Math.Max(_priceInput.Minimum, listing.Price));
        }

        _imagePromptTextBox.Text = BuildImagePrompt();
        _notesTextBox.Text =
            $"Rakip/ilham listing: {listing.Title}{Environment.NewLine}" +
            $"Magaza: {listing.ShopName} | Satis: {listing.ShopSales:N0} | Favori: {listing.Favorites:N0}{Environment.NewLine}" +
            $"Rakip listing: {listing.ListingUrl}{Environment.NewLine}" +
            "Bu veri ilham ve pazar analizi icindir; birebir kopyalama yapma.";
    }

    private void StartBusy(string message)
    {
        UseWaitCursor = true;
        _busyFrame = 0;
        SetBusyMessage(message);
        _busyOverlay.Visible = true;
        _busyOverlay.BringToFront();
        _busyTimer.Start();
        Application.DoEvents();
    }

    private void StopBusy()
    {
        _busyTimer.Stop();
        _busyOverlay.Visible = false;
        UseWaitCursor = false;
    }

    private void SetBusyMessage(string message)
    {
        var dots = new string('.', _busyFrame % 4);
        _busyLabel.Text = $"{message}{dots}";
        _statusLabel.Text = $"{message}{dots}";
    }

    private void UpdateBusyAnimation()
    {
        if (!_busyOverlay.Visible)
        {
            return;
        }

        _busyFrame++;
        var text = _busyLabel.Text.TrimEnd('.');
        SetBusyMessage(text);
    }

    private async Task EnrichCategoriesAsync(EtsyApiSettings settings)
    {
        var ids = _rows.Select(row => row.Listing.TaxonomyId).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        _statusLabel.Text = "Kategori adlari aliniyor...";
        var names = await _apiClient.GetSellerTaxonomyNamesAsync(settings, ids);
        foreach (var row in _rows)
        {
            if (names.TryGetValue(row.Listing.TaxonomyId, out var name))
            {
                row.Listing.TaxonomyName = name;
            }
        }
    }

    private async Task LoadGridThumbnailsAsync()
    {
        _statusLabel.Text = "Urun gorselleri yukleniyor...";
        foreach (var row in _rows)
        {
            var imageUrl = row.Listing.ImageUrls.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                imageUrl = row.Listing.ImageUrl;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            try
            {
                using var image = await DownloadImageAsync(imageUrl);
                row.Listing.ThumbnailImage = CreateThumbnail(image, 72, 58);
            }
            catch
            {
                // Gorsel yoksa listeyi yine kullanilabilir tut.
            }
        }
    }

    private async Task MoveSelectedImageAsync(int delta)
    {
        if (SelectedRow is null)
        {
            return;
        }

        var listing = SelectedRow.Listing;
        await EnsureListingImagesAsync(listing);
        var count = ImageUrlsFor(listing).Count;
        if (count == 0)
        {
            return;
        }

        _selectedImageIndex = (_selectedImageIndex + delta + count) % count;
        await ShowSelectedImageAsync();
    }

    private async Task ShowSelectedImageAsync()
    {
        if (SelectedRow is null)
        {
            SetPreviewImage(null, "Resim yok");
            return;
        }

        var listing = SelectedRow.Listing;
        await EnsureListingImagesAsync(listing);
        var urls = ImageUrlsFor(listing);
        if (urls.Count == 0)
        {
            SetPreviewImage(null, "Resim yok");
            return;
        }

        _selectedImageIndex = Math.Clamp(_selectedImageIndex, 0, urls.Count - 1);
        try
        {
            var image = await DownloadImageAsync(urls[_selectedImageIndex]);
            SetPreviewImage(image, $"{_selectedImageIndex + 1} / {urls.Count}");
        }
        catch
        {
            SetPreviewImage(null, "Resim yuklenemedi");
        }
    }

    private async Task EnsureListingImagesAsync(MarketListingResult listing)
    {
        if (listing.ImageUrls.Count > 0 || listing.ListingId <= 0)
        {
            return;
        }

        var settings = EtsyApiSettingsStore.Load();
        listing.ImageUrls = await _apiClient.GetListingImagesAsync(settings, listing.ListingId);
        EtsyApiSettingsStore.Save(settings);
    }

    private static IReadOnlyList<string> ImageUrlsFor(MarketListingResult listing)
    {
        if (listing.ImageUrls.Count > 0)
        {
            return listing.ImageUrls;
        }

        return string.IsNullOrWhiteSpace(listing.ImageUrl) ? [] : [listing.ImageUrl];
    }

    private async Task<Image> DownloadImageAsync(string imageUrl)
    {
        var bytes = await _imageHttpClient.GetByteArrayAsync(imageUrl);
        await using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Image CreateThumbnail(Image image, int width, int height)
    {
        var thumbnail = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(thumbnail);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var ratio = Math.Min(width / (float)image.Width, height / (float)image.Height);
        var targetWidth = (int)(image.Width * ratio);
        var targetHeight = (int)(image.Height * ratio);
        var x = (width - targetWidth) / 2;
        var y = (height - targetHeight) / 2;
        graphics.DrawImage(image, x, y, targetWidth, targetHeight);
        return thumbnail;
    }

    private void SetPreviewImage(Image? image, string counterText)
    {
        var oldImage = _previewPictureBox.Image;
        _previewPictureBox.Image = image;
        if (oldImage is not null && !ReferenceEquals(oldImage, image))
        {
            oldImage.Dispose();
        }

        _imageCounterLabel.Text = counterText;
    }

    private bool TryBuildDraftRequest(out DraftListingCreateRequest draft, out string message)
    {
        draft = new DraftListingCreateRequest("", "", 0, 0, 0, 0, false, [], []);
        var title = _titleTextBox.Text.Trim();
        var baseDescription = _descriptionTextBox.Text.Trim();
        var description = AppendVariationNotes(baseDescription);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 140)
        {
            message = "Baslik bos olamaz ve 140 karakteri gecemez.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(baseDescription))
        {
            message = "Aciklama bos olamaz.";
            return false;
        }

        if (!long.TryParse(_taxonomyInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var taxonomyId) || taxonomyId <= 0)
        {
            message = "Gecerli bir Etsy taxonomy ID girmen gerekiyor. Secili pazar urunundeki taxonomy otomatik gelebilir.";
            return false;
        }

        var isDigital = IsDigitalListingSelected();
        var shippingProfileId = SelectedShippingProfileId();
        var readinessStateId = SelectedReadinessStateId();
        if (!isDigital && shippingProfileId <= 0)
        {
            message = "Fiziksel urun icin shipping profile secmen gerekiyor. Shipping profile dropdown'una tiklayip magazadaki profillerden birini sec.";
            return false;
        }

        if (!isDigital && readinessStateId <= 0)
        {
            message = "Fiziksel urun icin hazirlik durumu secmen gerekiyor. Hazirlik durumu dropdown'una tiklayip mevcut listinglerinden gelen degeri sec veya ID'yi elle gir.";
            return false;
        }

        var tags = SplitCommaList(_tagsTextBox.Text).Take(13).ToList();
        if (tags.Count == 0)
        {
            message = "En az bir tag gir.";
            return false;
        }

        draft = new DraftListingCreateRequest(
            title,
            description,
            _priceInput.Value,
            (int)_quantityInput.Value,
            taxonomyId,
            shippingProfileId,
            isDigital,
            tags,
            SplitCommaList(_materialsTextBox.Text),
            readinessStateId);
        message = "";
        return true;
    }

    private string AppendVariationNotes(string description)
    {
        var variations = _variationsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(variations))
        {
            return description;
        }

        if (description.Contains("Variation options to configure", StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        return
            $"{description.Trim()}{Environment.NewLine}{Environment.NewLine}" +
            "Variation options to configure before publishing:" + Environment.NewLine +
            variations;
    }

    private DraftListingInventoryUpdate? BuildInventoryUpdate(DraftListingCreateRequest draft)
    {
        var groups = ParseInventoryVariationGroups(_variationsTextBox.Text);
        if (groups.Count == 0)
        {
            return null;
        }

        return new DraftListingInventoryUpdate(draft.Price, draft.Quantity, draft.IsDigital ? null : draft.ReadinessStateId, groups);
    }

    private static List<DraftListingVariationGroup> ParseInventoryVariationGroups(string value)
    {
        var groups = new List<DraftListingVariationGroup>();
        foreach (var rawLine in value.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            {
                continue;
            }

            var rawName = line[..separatorIndex].Trim();
            if (!TryExtractPropertyId(rawName, out var propertyId, out var nameWithoutId))
            {
                continue;
            }

            var name = NormalizeVariationText(nameWithoutId, 32);
            var options = SplitCommaList(line[(separatorIndex + 1)..])
                .Select(option => NormalizeVariationText(option, 40))
                .Where(option => option.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            if (name.Length == 0 || options.Count == 0)
            {
                continue;
            }

            groups.Add(new DraftListingVariationGroup(name, propertyId, options));
            if (groups.Count == 2)
            {
                break;
            }
        }

        return groups;
    }

    private static bool TryExtractPropertyId(string rawName, out long propertyId, out string cleanName)
    {
        propertyId = 0;
        var match = Regex.Match(rawName, @"^(?<name>.+?)\[(?<id>\d+)\]$");
        if (match.Success && long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
        {
            cleanName = match.Groups["name"].Value.Trim();
            propertyId = id;
            return true;
        }

        cleanName = rawName.Trim();
        return false;
    }

    private static string NormalizeVariationText(string value, int maxLength)
    {
        var clean = new string(value
            .Where(ch => !char.IsControl(ch) && ch != '"' && ch != '\\')
            .ToArray());
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        if (clean.Length > maxLength)
        {
            clean = clean[..maxLength].TrimEnd();
        }

        return clean;
    }

    private string BuildVariationSuggestions(MarketListingResult listing)
    {
        return string.Join(
            Environment.NewLine,
            listing.VariationOptions
                .Where(group => group.Values.Count > 0)
                .Select(group => $"{NormalizeVariationText(group.Name, 32)}[{group.PropertyId}]: {string.Join(", ", group.Values.Select(value => NormalizeVariationText(value, 40)).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(70))}"));
    }

    private void ChooseImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Listing gorseli sec",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp;*.gif|All files|*.*",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _imagePathTextBox.Text = string.Join("; ", dialog.FileNames);
        }
    }

    private string PrimaryKeyword() => _keywordTextBox.Text.Trim();

    private string BuildImagePrompt() =>
        "Use the selected competitor listing image as product reference. Keep the same product category, silhouette, pose, scale, and main physical details. Do not invent a different product. Improve the Etsy presentation with a clean studio background, realistic lighting, sharper product focus, natural shadow, and marketplace-ready composition. If the seller asks for a painted look, add tasteful hand-painted miniature colors while preserving the product shape. No watermark, no logo, no readable text, no official brand claims. " +
        $"Search keyword: {PrimaryKeyword()}. Selected Etsy listing: {SelectedRow?.Listing.Title ?? _titleTextBox.Text.Trim()}";

    private string BuildDraftSourceTitle(MarketListingResult listing)
    {
        var parts = new[]
        {
            "OUTPUT LANGUAGE: English only. Do not write Turkish.",
            $"Selected marketplace listing: {listing.Title}",
            $"Etsy search keyword: {PrimaryKeyword()}",
            $"Use the selected listing as the product reference",
            "Infer the best Etsy product category from the product itself, not from unrelated competitor categories.",
        };
        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string BuildDraftSourceDescription(MarketListingResult listing) =>
        "OUTPUT LANGUAGE: English only. Title, description, tags, materials, checklist, and warnings must be written in English. " +
        "Never write Turkish words such as urun, icin, taslak, listeleme, aciklama, or musteri. " +
        "Write the Etsy draft for the selected listing's actual product type and the user's search intent. " +
        "Do not switch to another character, object, theme, or product name from unrelated tags. " +
        "Use the selected listing title as the main product anchor, then make original buyer-facing English copy. " +
        "Choose the most accurate Etsy category/taxonomy concept from the actual product type and buyer intent. " +
        "If the competitor category conflicts with the product, prefer the real product type. " +
        "Avoid official, licensed, endorsed, or affiliated claims unless legally proven. " +
        $"Selected listing title: {listing.Title}{Environment.NewLine}" +
        $"Etsy search keyword: {PrimaryKeyword()}{Environment.NewLine}" +
        $"Current competitor category: {listing.TaxonomyDisplay}{Environment.NewLine}" +
        $"Competitor description: {listing.Description}";

    private string SelectEnglishTitle(IReadOnlyList<string> suggestions, MarketListingResult listing)
    {
        var title = SelectRelevantTitle(suggestions, listing);
        return LooksLikeTurkish(title) ? BuildSafeTitle(listing) : title;
    }

    private string SelectRelevantTitle(IReadOnlyList<string> suggestions, MarketListingResult listing)
    {
        var requiredTerms = ImportantTerms($"{PrimaryKeyword()} {listing.Title}");
        foreach (var suggestion in suggestions)
        {
            var title = suggestion.Trim();
            if (title.Length == 0) continue;
            if (LooksLikePromptLeak(title)) continue;
            if (requiredTerms.Count == 0 || requiredTerms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return title.Length <= 140 ? title : title[..140].TrimEnd();
            }
        }

        return BuildSafeTitle(listing);
    }

    private string SelectEnglishDescription(
        string description,
        MarketListingResult listing,
        IReadOnlyList<string> materialSuggestions)
    {
        var cleanDescription = description.Trim();
        if (cleanDescription.Length > 0 && !LooksLikeTurkish(cleanDescription))
        {
            return cleanDescription;
        }

        return BuildDynamicEnglishDescription(listing, materialSuggestions);
    }

    private string BuildDynamicEnglishDescription(
        MarketListingResult listing,
        IReadOnlyList<string> materialSuggestions)
    {
        var title = SelectRelevantTitle([listing.Title], listing);
        var productName = ShortProductName(title);
        var searchIntent = PrimaryKeyword();
        var category = ReadableCategoryName(listing);
        var tags = listing.Tags.Count > 0
            ? listing.Tags
                .Where(tag => tag.Length > 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList()
            : ImportantTerms($"{title} {searchIntent}").ToList();
        var materials = materialSuggestions.Count > 0
            ? materialSuggestions
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList()
            : InferMaterials(listing);
        var useCases = BuildUseCases($"{title} {listing.Description} {string.Join(' ', listing.Tags)}");

        var builder = new StringBuilder();
        builder.Append(productName);
        builder.Append(" is designed for shoppers looking for ");
        builder.Append(searchIntent.Length > 0 ? searchIntent : category.ToLowerInvariant());
        builder.Append(". This ");
        builder.Append(category.Length > 0 ? category.ToLowerInvariant() : "collectible piece");
        builder.Append(" brings a focused, marketplace-ready presentation for collectors, gift buyers, and display-focused Etsy customers.");
        builder.AppendLine();
        builder.AppendLine();

        builder.Append("The listing highlights ");
        builder.Append(tags.Count > 0 ? string.Join(", ", tags.Take(5)) : "clear product details, searchable style terms, and buyer intent");
        builder.Append(". It is written to help buyers quickly understand the product style, display purpose, and why it fits their collection or decor setup.");
        builder.AppendLine();
        builder.AppendLine();

        builder.Append("Materials and finish: ");
        builder.Append(materials.Count > 0 ? string.Join(", ", materials) : "quality materials selected according to the final production method");
        builder.Append(". Review the exact size, color, finish, and production details before publishing so the final Etsy listing matches the item you will ship.");
        builder.AppendLine();
        builder.AppendLine();

        builder.Append("Great for ");
        builder.Append(string.Join(", ", useCases));
        builder.Append(". Before publishing, check trademark, character, and brand references carefully and keep the final wording accurate to your own handmade product.");
        return builder.ToString();
    }

    private static string DraftSourceLabel(AiOptimizationSettings settings)
    {
        if (settings.IsOffline)
        {
            return "Offline dinamik motor";
        }

        if (settings.UseGemini)
        {
            return $"Gemini ({settings.GeminiModel})";
        }

        if (settings.UseOpenAi)
        {
            return $"OpenAI ({settings.OpenAiModel})";
        }

        return $"{settings.Provider} ayari eksik; offline fallback";
    }

    private static string ShortProductName(string title)
    {
        var firstPart = title
            .Split(['|', '-', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.Length > 0) ?? title.Trim();
        return firstPart.Length <= 85 ? firstPart : firstPart[..85].TrimEnd();
    }

    private static string ReadableCategoryName(MarketListingResult listing)
    {
        var category = listing.TaxonomyName.Length > 0 ? listing.TaxonomyName : listing.TaxonomyDisplay;
        if (category.Length > 0)
        {
            var lastPart = category
                .Split('>', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .LastOrDefault(part => part.Length > 0);
            if (!string.IsNullOrWhiteSpace(lastPart))
            {
                return lastPart;
            }
        }

        return ProductTypeFromText($"{listing.Title} {listing.Description}");
    }

    private static string ProductTypeFromText(string value)
    {
        var text = value.ToLowerInvariant();
        if (ContainsAny(text, "bust")) return "display bust";
        if (ContainsAny(text, "statue", "sculpture")) return "display statue";
        if (ContainsAny(text, "figurine", "figure")) return "collectible figure";
        if (ContainsAny(text, "helmet", "mask", "sword", "prop")) return "cosplay prop";
        if (ContainsAny(text, "lamp", "light")) return "decor light";
        if (ContainsAny(text, "poster", "print")) return "wall art";
        return "collectible item";
    }

    private static IReadOnlyList<string> InferMaterials(MarketListingResult listing)
    {
        var blob = $"{listing.Title} {listing.Description} {string.Join(' ', listing.Tags)}";
        var materials = new List<string>();
        AddMaterialIfMentioned(blob, materials, "resin", "resin");
        AddMaterialIfMentioned(blob, materials, "pla", "PLA");
        AddMaterialIfMentioned(blob, materials, "plastic", "plastic");
        AddMaterialIfMentioned(blob, materials, "paint", "paint");
        AddMaterialIfMentioned(blob, materials, "wood", "wood");
        AddMaterialIfMentioned(blob, materials, "metal", "metal");
        AddMaterialIfMentioned(blob, materials, "leather", "leather");
        AddMaterialIfMentioned(blob, materials, "fabric", "fabric");
        return materials.Count > 0 ? materials : ["quality materials"];
    }

    private static void AddMaterialIfMentioned(string blob, List<string> materials, string needle, string material)
    {
        if (blob.Contains(needle, StringComparison.OrdinalIgnoreCase) &&
            !materials.Contains(material, StringComparer.OrdinalIgnoreCase))
        {
            materials.Add(material);
        }
    }

    private static IReadOnlyList<string> BuildUseCases(string value)
    {
        var text = value.ToLowerInvariant();
        var useCases = new List<string>();
        if (ContainsAny(text, "cosplay", "prop", "helmet", "sword", "mask")) useCases.Add("cosplay displays");
        if (ContainsAny(text, "gamer", "gaming", "game", "rpg")) useCases.Add("gamer room decor");
        if (ContainsAny(text, "desk", "shelf", "office")) useCases.Add("desk and shelf styling");
        if (ContainsAny(text, "collector", "collectible", "statue", "figure", "bust")) useCases.Add("collector displays");
        if (ContainsAny(text, "gift", "birthday", "father", "mother")) useCases.Add("thoughtful gifting");
        if (useCases.Count == 0) useCases.AddRange(["home decor", "collection displays", "gift ideas"]);
        return useCases.Take(4).ToList();
    }

    private void ApplyDraftCategoryRecommendation(MarketListingResult selectedListing)
    {
        var recommended = BestCategoryCandidate(selectedListing);
        if (recommended.TaxonomyId <= 0)
        {
            return;
        }

        _taxonomyInput.Text = recommended.TaxonomyId.ToString(CultureInfo.InvariantCulture);
        _categoryTextBox.Text = recommended.TaxonomyDisplay;
    }

    private MarketListingResult BestCategoryCandidate(MarketListingResult fallback)
    {
        var draftText = $"{_titleTextBox.Text} {_descriptionTextBox.Text} {_tagsTextBox.Text} {PrimaryKeyword()}";
        var draftTerms = CategoryTerms(draftText);
        if (draftTerms.Count == 0)
        {
            return fallback;
        }

        return _rows
            .Select(row => row.Listing)
            .Where(listing => listing.TaxonomyId > 0)
            .Select(listing => new
            {
                Listing = listing,
                Score = CategoryScore(listing, draftTerms),
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Listing.SeoScore)
            .ThenByDescending(item => item.Listing.Favorites)
            .FirstOrDefault(item => item.Score > 0)?.Listing ?? fallback;
    }

    private static int CategoryScore(MarketListingResult listing, IReadOnlyList<string> draftTerms)
    {
        var text = $"{listing.Title} {listing.TaxonomyDisplay} {listing.Description} {string.Join(' ', listing.Tags)}";
        var score = draftTerms.Sum(term => text.Contains(term, StringComparison.OrdinalIgnoreCase) ? 3 : 0);
        if (listing.TaxonomyName.Length > 0)
        {
            score += draftTerms.Sum(term => listing.TaxonomyName.Contains(term, StringComparison.OrdinalIgnoreCase) ? 4 : 0);
        }

        score += CategoryIntentBonus(text);
        return score;
    }

    private static int CategoryIntentBonus(string value)
    {
        var text = value.ToLowerInvariant();
        var isPhysicalCollectible = ContainsAny(text, "bust", "statue", "figurine", "figure", "sculpture", "collectible", "miniature");
        if (!isPhysicalCollectible)
        {
            return 0;
        }

        var bonus = 0;
        if (ContainsAny(text, "art & collectibles", "sculpture", "figurines", "dolls & miniatures", "collectibles"))
        {
            bonus += 18;
        }

        if (ContainsAny(text, "patterns & how to", "craft supplies", "digital", "stl", "file", "download", "template"))
        {
            bonus -= 14;
        }

        return bonus;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> CategoryTerms(string value)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "with", "for", "from", "gift", "custom", "handmade", "printed", "print", "file", "files",
            "digital", "adult", "kids", "etsy", "ready", "listing", "selected", "marketplace", "product", "buyer",
            "description", "title", "search", "intent", "quality", "materials"
        };
        return value
            .Split([' ', '-', '|', ',', '/', '(', ')', ':', ';', '.', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim())
            .Where(term => term.Length >= 4 && !blocked.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();
    }

    private static bool LooksLikeTurkish(string value)
    {
        if (value.IndexOfAny(['ı', 'İ', 'ğ', 'Ğ', 'ş', 'Ş', 'ö', 'Ö', 'ü', 'Ü', 'ç', 'Ç']) >= 0)
        {
            return true;
        }

        var text = $" {value.ToLowerInvariant()} ";
        var markers = new[]
        {
            " icin ", " urun ", " urunu ", " listeleme ", " taslak ", " aciklama ", " alici ", " muster",
            " konumlandiril", " optimize edilmis ", " hazirlandi ", " arayan ", " magaza "
        };
        return markers.Any(text.Contains);
    }

    private static bool LooksLikePromptLeak(string value)
    {
        var text = value.ToLowerInvariant();
        return text.Contains("selected marketplace")
            || text.Contains("selected listing")
            || text.Contains("listing selected")
            || text.Contains("etsy draft")
            || text.Contains("search keyword");
    }

    private IReadOnlyList<string> ImagePrompts()
    {
        var prompts = _imagePromptTextBox.Text
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(prompt => prompt.Trim())
            .Where(prompt => prompt.Length > 0)
            .ToList();
        return prompts.Count > 0 ? prompts : [BuildImagePrompt()];
    }

    private string PromptForImageIndex(IReadOnlyList<string> prompts, int oneBasedIndex)
    {
        var index = Math.Clamp(oneBasedIndex - 1, 0, prompts.Count - 1);
        return prompts[index];
    }

    private async Task LoadShippingProfilesAsync()
    {
        if (_shippingProfileComboBox.Items.Count > 0 || IsDigitalListingSelected())
        {
            return;
        }

        try
        {
            _statusLabel.Text = "Magaza shipping profilleri aliniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var profiles = await _apiClient.GetOwnShopShippingProfilesAsync(settings);
            EtsyApiSettingsStore.Save(settings);
            _shippingProfileComboBox.DataSource = profiles;
            if (profiles.Count > 0)
            {
                _shippingProfileComboBox.SelectedIndex = 0;
                _statusLabel.Text = $"{profiles.Count} shipping profile listelendi";
            }
            else
            {
                _statusLabel.Text = "Shipping profile bulunamadi";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Shipping profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Shipping profile alinamadi";
        }
    }

    private async Task LoadReadinessStatesAsync()
    {
        if (_readinessStateComboBox.Items.Count > 0 || IsDigitalListingSelected())
        {
            return;
        }

        try
        {
            _statusLabel.Text = "Magaza hazirlik durumu secenekleri aliniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var states = await _apiClient.GetOwnShopReadinessStateOptionsAsync(settings);
            EtsyApiSettingsStore.Save(settings);
            _readinessStateComboBox.DataSource = states;
            if (states.Count > 0)
            {
                _readinessStateComboBox.SelectedIndex = 0;
                _statusLabel.Text = $"{states.Count} hazirlik durumu listelendi";
            }
            else
            {
                _statusLabel.Text = "Hazirlik durumu bulunamadi; ID'yi elle girebilirsin";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Hazirlik durumu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Hazirlik durumu alinamadi";
        }
    }

    private bool IsDigitalListingSelected() =>
        _listingTypeComboBox.SelectedItem?.ToString()?.Contains("Dijital", StringComparison.OrdinalIgnoreCase) == true;

    private long SelectedShippingProfileId() =>
        _shippingProfileComboBox.SelectedValue is long value
            ? value
            : _shippingProfileComboBox.SelectedItem is EtsyShippingProfileOption option
                ? option.ShippingProfileId
                : 0;

    private long SelectedReadinessStateId()
    {
        if (_readinessStateComboBox.SelectedValue is long value)
        {
            return value;
        }

        if (_readinessStateComboBox.SelectedItem is EtsyReadinessStateOption option)
        {
            return option.ReadinessStateId;
        }

        var text = _readinessStateComboBox.Text.Trim();
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var typedValue)
            ? typedValue
            : 0;
    }

    private void UpdateListingTypeControls()
    {
        var physical = !IsDigitalListingSelected();
        _shippingProfileComboBox.Enabled = physical;
        _readinessStateComboBox.Enabled = physical;
    }

    private IReadOnlyList<string> SelectedImagePaths() =>
        _imagePathTextBox.Text
            .Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => path.Length > 0 && File.Exists(path))
            .ToList();

    private async Task<string> DownloadReferenceImageAsync(MarketListingResult listing)
    {
        if (listing.ImageUrls.Count == 0 && listing.ListingId > 0)
        {
            var settings = EtsyApiSettingsStore.Load();
            listing.ImageUrls = await _apiClient.GetListingImagesAsync(settings, listing.ListingId);
            EtsyApiSettingsStore.Save(settings);
        }

        var imageUrl = listing.ImageUrls.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            imageUrl = listing.ImageUrl;
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new InvalidOperationException("Secili rakip listing icin referans gorsel bulunamadi. Baska bir listing sec veya gorseli dosyadan kendin sec.");
        }

        _statusLabel.Text = "Rakip listing referans gorseli indiriliyor...";
        var bytes = await _imageHttpClient.GetByteArrayAsync(imageUrl);
        await using var input = new MemoryStream(bytes);
        using var image = Image.FromStream(input);
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms",
            "reference-images");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"reference-{listing.ListingId}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        _notesTextBox.Text =
            $"Referans gorsel indirildi: {path}{Environment.NewLine}" +
            "AI bu gorseldeki urunu koruyup yalnizca arka plan/isik/kompozisyon duzenlemesi yapacak.";
        return path;
    }

    private void OpenSelectedCompetitorListing() => OpenUrl(SelectedRow?.Listing.ListingUrl);

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void ApplyDefaultFiltersForShopType(string shopType)
    {
        var normalized = shopType.ToLowerInvariant();
        if (normalized.Contains("3d") || normalized.Contains("cosplay") || normalized.Contains("prop"))
        {
            if (string.IsNullOrWhiteSpace(_includeTextBox.Text))
            {
                _includeTextBox.Text = "figure, bust, statue, prop, helmet, sword, mask, display";
            }

            if (string.IsNullOrWhiteSpace(_excludeTextBox.Text))
            {
                _excludeTextBox.Text = "stl, file, files, digital download, download, svg, png, pdf, template, pattern";
            }
        }
        else if (normalized.Contains("bed") || normalized.Contains("bedding"))
        {
            if (string.IsNullOrWhiteSpace(_includeTextBox.Text))
            {
                _includeTextBox.Text = "bed, bedding, pillow, duvet, blanket, sheet, cover";
            }

            if (string.IsNullOrWhiteSpace(_excludeTextBox.Text))
            {
                _excludeTextBox.Text = "pattern, digital, download, svg, png, pdf";
            }
        }
    }

    private static bool MatchesProductIntent(
        MarketListingResult listing,
        IReadOnlyList<string> includeTerms,
        IReadOnlyList<string> excludeTerms)
    {
        var searchable = BuildSearchableText(listing);
        if (excludeTerms.Any(term => ContainsTerm(searchable, term)))
        {
            return false;
        }

        return includeTerms.Count == 0 || includeTerms.Any(term => ContainsTerm(searchable, term));
    }

    private static string BuildSearchableText(MarketListingResult listing) =>
        $"{listing.Title} {listing.Description} {string.Join(' ', listing.Tags)}".ToLowerInvariant();

    private static bool ContainsTerm(string searchable, string term) =>
        searchable.Contains(term.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);

    private static string BuildSafeTitle(MarketListingResult listing)
    {
        var title = listing.Title;
        return title.Length <= 140 ? title : title[..140].TrimEnd();
    }

    private static IReadOnlyList<string> ImportantTerms(string value)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "with", "for", "from", "gift", "custom", "handmade", "printed", "print", "file", "files",
            "digital", "adult", "kids", "3d", "stl", "pla", "resin", "fan", "art", "sculpture", "statue", "figure",
            "bust", "prop", "collectible", "decor", "display", "inspired", "middle", "earth", "lord", "rings", "lotr"
        };
        return value
            .Split([' ', '-', '|', ',', '/', '(', ')', ':', ';', '.', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim())
            .Where(term => term.Length >= 4 && !blocked.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractShopKeywords(IEnumerable<MarketListingResult> listings)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "with", "for", "from", "gift", "custom", "handmade", "printed", "print", "file", "files", "digital", "adult", "kids"
        };
        return listings
            .SelectMany(item => item.Title.Split([' ', '-', '|', ',', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries).Concat(item.Tags))
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length >= 3 && !blocked.Contains(term))
            .GroupBy(term => term)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .Take(18)
            .ToList();
    }

    private static string GuessShopType(IReadOnlyList<string> keywords)
    {
        var text = string.Join(' ', keywords);
        if (text.Contains("cosplay") || text.Contains("prop") || text.Contains("helmet") || text.Contains("sword")) return "3D cosplay prop";
        if (text.Contains("bed") || text.Contains("pillow") || text.Contains("duvet") || text.Contains("bedding")) return "bed and bedding";
        if (text.Contains("svg") || text.Contains("png") || text.Contains("template")) return "digital printable design";
        if (text.Contains("lamp") || text.Contains("decor")) return "home decor";
        return keywords.Count == 0 ? "" : string.Join(' ', keywords.Take(3));
    }

    private static string BuildSearchKeyword(string shopType, IReadOnlyList<string> keywords) =>
        string.IsNullOrWhiteSpace(shopType)
            ? string.Join(' ', keywords.Take(3))
            : shopType;

    private static IReadOnlyList<string> SplitCommaList(string value) =>
        value
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

    private static IReadOnlyList<string> SplitFilterTerms(string value) =>
        value
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ConfigureMultiline(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Vertical;
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = UiStyle.TextDark,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = isSecondary ? UiStyle.SecondaryColor : UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            UseVisualStyleBackColor = false,
            AutoEllipsis = true,
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isSecondary ? UiStyle.SecondaryHover : UiStyle.PrimaryHover;
        return button;
    }

    private void AddColumn(string header, string property, int width, bool fill = false) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });

    private void ApplyFavoriteSort()
    {
        _favoriteSortDescending = !_favoriteSortDescending;
        _rows = _favoriteSortDescending
            ? _rows.OrderByDescending(row => row.Listing.Favorites).ThenByDescending(row => row.Listing.Views).ToList()
            : _rows.OrderBy(row => row.Listing.Favorites).ThenBy(row => row.Listing.Views).ToList();
        
        RefreshGridSort(nameof(IdeaRow.Favorites), _favoriteSortDescending);
        _statusLabel.Text = _favoriteSortDescending
            ? $"{_rows.Count} Etsy urunu | Favori: coktan aza"
            : $"{_rows.Count} Etsy urunu | Favori: azdan coga";
    }

    private void ApplyOpportunitySort()
    {
        _opportunitySortDescending = !_opportunitySortDescending;
        _rows = _opportunitySortDescending
            ? _rows.OrderByDescending(row => row.OpportunityScore).ThenByDescending(row => row.Listing.Views).ToList()
            : _rows.OrderBy(row => row.OpportunityScore).ThenBy(row => row.Listing.Views).ToList();
        
        RefreshGridSort(nameof(IdeaRow.Opportunity), _opportunitySortDescending);
        _statusLabel.Text = _opportunitySortDescending
            ? $"{_rows.Count} Etsy urunu | Firsat puani: coktan aza"
            : $"{_rows.Count} Etsy urunu | Firsat puani: azdan coga";
    }

    private void ApplyViewsSort()
    {
        _viewsSortDescending = !_viewsSortDescending;
        _rows = _viewsSortDescending
            ? _rows.OrderByDescending(row => row.Listing.Views).ThenByDescending(row => row.Listing.Favorites).ToList()
            : _rows.OrderBy(row => row.Listing.Views).ThenBy(row => row.Listing.Favorites).ToList();
        
        RefreshGridSort(nameof(IdeaRow.Views), _viewsSortDescending);
        _statusLabel.Text = _viewsSortDescending
            ? $"{_rows.Count} Etsy urunu | Goruntulenme: coktan aza"
            : $"{_rows.Count} Etsy urunu | Goruntulenme: azdan coga";
    }

    private void RefreshGridSort(string dataPropertyName, bool descending)
    {
        _bindingSource.DataSource = null;
        _bindingSource.DataSource = _rows;
        _grid.Refresh();

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            col.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        var targetColumn = _grid.Columns.Cast<DataGridViewColumn>()
            .FirstOrDefault(col => col.DataPropertyName == dataPropertyName);
        if (targetColumn != null)
        {
            targetColumn.HeaderCell.SortGlyphDirection = descending 
                ? SortOrder.Descending 
                : SortOrder.Ascending;
        }
    }

    private sealed class IdeaRow
    {
        public IdeaRow(MarketListingResult listing)
        {
            Listing = listing;
        }

        public MarketListingResult Listing { get; }
        public int OpportunityScore => Math.Clamp(Listing.MarketScore + Math.Min(15, Listing.ShopSales / 1000) - Math.Min(12, Listing.SeoScore / 10), 0, 100);
        public string Opportunity => $"{OpportunityScore}/100";
        public string Title => Listing.Title;
        public string Price => Listing.PriceDisplay;
        public string Shop => Listing.ShopName;
        public string ShopSales => Listing.ShopSales.ToString("N0");
        public string Favorites => Listing.Favorites.ToString("N0");
        public string Views => Listing.Views.ToString("N0");
        public string Seo => Listing.SeoScore.ToString(CultureInfo.InvariantCulture);
        public string TaxonomyId => Listing.TaxonomyId > 0 ? Listing.TaxonomyId.ToString(CultureInfo.InvariantCulture) : "-";
        public Image? Thumbnail => Listing.ThumbnailImage;
        public string Category => Listing.TaxonomyDisplay;
        public string Tags => string.Join(", ", Listing.Tags.Take(10));
    }
}
