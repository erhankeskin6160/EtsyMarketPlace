namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ProductDiscoveryListingCreatorForm(IAiListingOptimizer aiOptimizer) : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly AiListingImageGenerator _imageGenerator = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _shopTypeTextBox = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly TextBox _includeTextBox = new();
    private readonly TextBox _excludeTextBox = new();
    private readonly TextBox _titleTextBox = new();
    private readonly TextBox _descriptionTextBox = new();
    private readonly TextBox _tagsTextBox = new();
    private readonly TextBox _materialsTextBox = new();
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
    private readonly Label _statusLabel = new();
    private List<IdeaRow> _rows = [];
    private int _selectedImageIndex;

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
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
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
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Anahtar kelime girip Etsy'de urun arayin";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildToolbar(), 0, 1);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDraftArea(), 0, 3);
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 1, Padding = new Padding(0, 18, 0, 18) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

        var middle = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6 };
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        middle.Controls.Add(LabelFor("Tagler"), 0, 0);
        ConfigureMultiline(_tagsTextBox);
        middle.Controls.Add(_tagsTextBox, 0, 1);
        middle.Controls.Add(LabelFor("Materyaller"), 0, 2);
        ConfigureMultiline(_materialsTextBox);
        middle.Controls.Add(_materialsTextBox, 0, 3);
        middle.Controls.Add(LabelFor("AI gorsel promptlari (satir satir)"), 0, 4);
        ConfigureMultiline(_imagePromptTextBox);
        middle.Controls.Add(_imagePromptTextBox, 0, 5);
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
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Color.White;
        _grid.RowTemplate.Height = 68;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => FillFromSelectedIdea();
        _grid.CellDoubleClick += (_, _) => OpenSelectedCompetitorListing();
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
            UseWaitCursor = true;
            _statusLabel.Text = "Etsy'de anahtar kelimeye gore urun araniyor...";
            _shopTypeTextBox.Text = keyword;
            _includeTextBox.Clear();
            _excludeTextBox.Clear();
            var settings = EtsyApiSettingsStore.Load();
            var results = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            EtsyApiSettingsStore.Save(settings);
            _rows = results
                .Select(item => new IdeaRow(item))
                .OrderByDescending(row => row.OpportunityScore)
                .ToList();
            await EnrichCategoriesAsync(settings);
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
            UseWaitCursor = false;
        }
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
            var input = new ListingOptimizationInput(
                BuildDraftSourceTitle(listing),
                BuildDraftSourceDescription(listing),
                listing.Tags,
                PrimaryKeyword());
            var result = await aiOptimizer.OptimizeAsync(input);
            _titleTextBox.Text = SelectRelevantTitle(result.TitleSuggestions, listing);
            _descriptionTextBox.Text = result.DescriptionDraft;
            _tagsTextBox.Text = string.Join(", ", result.TagSuggestions.Take(13));
            _materialsTextBox.Text = string.Join(", ", EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions));
            _imagePromptTextBox.Text = BuildImagePrompt();
            _notesTextBox.Text = "AI taslak hazir. Etsy'ye eklemeden once fiyat, stok, taxonomy ve kargo profilini kontrol et.";
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

        var confirmationText =
            $"Bu islem Etsy magazanda TASLAK listing olusturacak.{Environment.NewLine}{Environment.NewLine}" +
            $"Baslik: {draft.Title}{Environment.NewLine}" +
            $"Fiyat: {draft.Price:0.00}{Environment.NewLine}" +
            $"Stok: {draft.Quantity}{Environment.NewLine}" +
            $"Taxonomy: {draft.TaxonomyId}{Environment.NewLine}" +
            $"Tip: {(draft.IsDigital ? "Dijital" : "Fiziksel")}{Environment.NewLine}" +
            $"Kargo profili: {(draft.IsDigital ? "Gerekmez" : draft.ShippingProfileId)}{Environment.NewLine}" +
            $"Hazirlik durumu: {(draft.IsDigital ? "Gerekmez" : draft.ReadinessStateId)}{Environment.NewLine}" +
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
            foreach (var imagePath in SelectedImagePaths())
            {
                await _apiClient.UploadOwnShopListingImageAsync(settings, created.ListingId, imagePath);
            }

            EtsyApiSettingsStore.Save(settings);
            _statusLabel.Text = $"Taslak listing olusturuldu: #{created.ListingId}";
            if (MessageBox.Show(this, "Taslak listing olusturuldu. Etsy'de acmak ister misin?", "Etsy taslak", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
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
        var description = _descriptionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 140)
        {
            message = "Baslik bos olamaz ve 140 karakteri gecemez.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
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
            $"Selected marketplace listing: {listing.Title}",
            $"Etsy search keyword: {PrimaryKeyword()}",
            $"Use the selected listing as the product reference",
        };
        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string BuildDraftSourceDescription(MarketListingResult listing) =>
        "Write the Etsy draft for the selected listing's actual product type and the user's search intent. " +
        "Do not switch to another character, object, theme, or product name from unrelated tags. " +
        "Use the selected listing title as the main product anchor, then make original buyer-facing English copy. " +
        "Avoid official, licensed, endorsed, or affiliated claims unless legally proven. " +
        $"Selected listing title: {listing.Title}{Environment.NewLine}" +
        $"Etsy search keyword: {PrimaryKeyword()}{Environment.NewLine}" +
        $"Competitor description: {listing.Description}";

    private string SelectRelevantTitle(IReadOnlyList<string> suggestions, MarketListingResult listing)
    {
        var requiredTerms = ImportantTerms($"{PrimaryKeyword()} {listing.Title}");
        foreach (var suggestion in suggestions)
        {
            var title = suggestion.Trim();
            if (title.Length == 0) continue;
            if (requiredTerms.Count == 0 || requiredTerms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return title.Length <= 140 ? title : title[..140].TrimEnd();
            }
        }

        return BuildSafeTitle(listing);
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
        ForeColor = Color.FromArgb(23, 32, 49),
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.3F),
            UseVisualStyleBackColor = false,
            AutoEllipsis = true,
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
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
