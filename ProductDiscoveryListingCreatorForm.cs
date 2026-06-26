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
    private readonly TextBox _taxonomyInput = new();
    private readonly TextBox _shippingProfileInput = new();
    private readonly CheckBox _digitalCheckBox = new() { Text = "Dijital urun", Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new();
    private List<IdeaRow> _rows = [];

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

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Magaza Turune Gore Urun Bul ve Listing Hazirla",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Once magaza turunu algila veya anahtar kelime yaz";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildToolbar(), 0, 1);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDraftArea(), 0, 3);
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 12, RowCount = 2, Padding = new Padding(0, 0, 0, 8) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        for (var index = 6; index < 12; index++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));

        toolbar.Controls.Add(LabelFor("Magaza turu"), 0, 0);
        _shopTypeTextBox.Dock = DockStyle.Fill;
        _shopTypeTextBox.PlaceholderText = "Orn: 3D cosplay, yatak, dijital davetiye";
        toolbar.Controls.Add(_shopTypeTextBox, 1, 0);
        toolbar.Controls.Add(LabelFor("Aranacak urun"), 2, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Orn: 3d cosplay prop";
        toolbar.Controls.Add(_keywordTextBox, 3, 0);
        toolbar.Controls.Add(LabelFor("Limit"), 4, 0);
        _limitInput.Dock = DockStyle.Left;
        toolbar.Controls.Add(_limitInput, 5, 0);

        toolbar.Controls.Add(LabelFor("Istenen urun"), 0, 1);
        _includeTextBox.Dock = DockStyle.Fill;
        _includeTextBox.PlaceholderText = "Orn: figure, bust, statue, prop";
        toolbar.Controls.Add(_includeTextBox, 1, 1);
        toolbar.Controls.Add(LabelFor("Haric kelimeler"), 2, 1);
        _excludeTextBox.Dock = DockStyle.Fill;
        _excludeTextBox.PlaceholderText = "Orn: stl, file, digital download";
        toolbar.Controls.Add(_excludeTextBox, 3, 1);
        toolbar.SetColumnSpan(_excludeTextBox, 3);

        var detect = CreateButton("Magazayi Algila");
        detect.Click += async (_, _) => await DetectShopTypeAsync();
        toolbar.Controls.Add(detect, 6, 0);
        var search = CreateButton("Urun Bul");
        search.Click += async (_, _) => await SearchIdeasAsync();
        toolbar.Controls.Add(search, 7, 0);
        var draft = CreateButton("Taslak Uret");
        draft.Click += async (_, _) => await GenerateDraftAsync();
        toolbar.Controls.Add(draft, 8, 0);
        var image = CreateButton("AI Gorsel");
        image.Click += async (_, _) => await GenerateImageAsync();
        toolbar.Controls.Add(image, 9, 0);
        var create = CreateButton("Etsy Taslak Ekle");
        create.BackColor = Color.FromArgb(20, 126, 76);
        create.Click += async (_, _) => await CreateDraftListingAsync();
        toolbar.Controls.Add(create, 10, 0);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(close, 11, 0);
        toolbar.SetRowSpan(detect, 2);
        toolbar.SetRowSpan(search, 2);
        toolbar.SetRowSpan(draft, 2);
        toolbar.SetRowSpan(image, 2);
        toolbar.SetRowSpan(create, 2);
        toolbar.SetRowSpan(close, 2);
        return toolbar;
    }

    private Control BuildDraftArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 10, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        left.Controls.Add(LabelFor("Baslik"), 0, 0);
        _titleTextBox.Dock = DockStyle.Fill;
        _titleTextBox.Multiline = true;
        left.Controls.Add(_titleTextBox, 0, 1);
        left.Controls.Add(LabelFor("Aciklama"), 0, 2);
        ConfigureMultiline(_descriptionTextBox);
        left.Controls.Add(_descriptionTextBox, 0, 3);
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
        middle.Controls.Add(LabelFor("AI gorsel promptu"), 0, 4);
        ConfigureMultiline(_imagePromptTextBox);
        middle.Controls.Add(_imagePromptTextBox, 0, 5);
        layout.Controls.Add(middle, 1, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 13 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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
        right.Controls.Add(LabelFor("Shipping profile ID"), 0, 6);
        _shippingProfileInput.Dock = DockStyle.Fill;
        right.Controls.Add(_shippingProfileInput, 0, 7);
        right.Controls.Add(_digitalCheckBox, 0, 8);
        right.Controls.Add(LabelFor("Gorsel dosyasi"), 0, 9);
        _imagePathTextBox.Dock = DockStyle.Fill;
        right.Controls.Add(_imagePathTextBox, 0, 10);
        var choose = CreateButton("Dosyadan Sec");
        choose.Click += (_, _) => ChooseImage();
        right.Controls.Add(choose, 0, 11);
        _notesTextBox.Dock = DockStyle.Fill;
        _notesTextBox.Multiline = true;
        _notesTextBox.ReadOnly = true;
        right.Controls.Add(_notesTextBox, 0, 12);
        layout.Controls.Add(right, 2, 0);
        return layout;
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
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => FillFromSelectedIdea();
        AddColumn("Firsat", nameof(IdeaRow.Opportunity), 70);
        AddColumn("Urun fikri", nameof(IdeaRow.Title), 380, true);
        AddColumn("Fiyat", nameof(IdeaRow.Price), 90);
        AddColumn("Magaza", nameof(IdeaRow.Shop), 150);
        AddColumn("Magaza satisi", nameof(IdeaRow.ShopSales), 110);
        AddColumn("Favori", nameof(IdeaRow.Favorites), 75);
        AddColumn("Goruntulenme", nameof(IdeaRow.Views), 105);
        AddColumn("SEO", nameof(IdeaRow.Seo), 60);
        AddColumn("Taxonomy", nameof(IdeaRow.TaxonomyId), 90);
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
        var keyword = string.IsNullOrWhiteSpace(_keywordTextBox.Text)
            ? _shopTypeTextBox.Text.Trim()
            : _keywordTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Once magaza turu veya aranacak urun girin.", "Urun bul", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Etsy pazarinda firsat urunleri araniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var results = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            EtsyApiSettingsStore.Save(settings);
            var includeTerms = SplitFilterTerms(_includeTextBox.Text);
            var excludeTerms = SplitFilterTerms(_excludeTextBox.Text);
            _rows = results
                .Where(item => MatchesProductIntent(item, includeTerms, excludeTerms))
                .Select(item => new IdeaRow(item))
                .OrderByDescending(row => row.OpportunityScore)
                .ToList();
            _bindingSource.DataSource = _rows;
            _statusLabel.Text = $"{_rows.Count} uygun pazar urunu listelendi";
            FillFromSelectedIdea();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Urun bul", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                listing.Title,
                listing.Description,
                listing.Tags,
                PrimaryKeyword());
            var result = await aiOptimizer.OptimizeAsync(input);
            _titleTextBox.Text = result.TitleSuggestions.FirstOrDefault() ?? BuildSafeTitle(listing);
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
            var prompt = string.IsNullOrWhiteSpace(_imagePromptTextBox.Text)
                ? BuildImagePrompt()
                : _imagePromptTextBox.Text.Trim();
            var path = await _imageGenerator.GenerateAsync(settings, SelectedRow.Listing, prompt);
            _imagePathTextBox.Text = path;
            _statusLabel.Text = "AI gorsel uretildi";
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
            $"Gorsel: {(string.IsNullOrWhiteSpace(_imagePathTextBox.Text) ? "Yok" : _imagePathTextBox.Text)}{Environment.NewLine}{Environment.NewLine}" +
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
            if (!string.IsNullOrWhiteSpace(_imagePathTextBox.Text) && File.Exists(_imagePathTextBox.Text))
            {
                await _apiClient.UploadOwnShopListingImageAsync(settings, created.ListingId, _imagePathTextBox.Text);
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

        if (listing.Price > 0)
        {
            _priceInput.Value = Math.Min(_priceInput.Maximum, Math.Max(_priceInput.Minimum, listing.Price));
        }

        _imagePromptTextBox.Text = BuildImagePrompt();
        _notesTextBox.Text =
            $"Rakip/ilham listing: {listing.Title}{Environment.NewLine}" +
            $"Magaza: {listing.ShopName} | Satis: {listing.ShopSales:N0} | Favori: {listing.Favorites:N0}{Environment.NewLine}" +
            "Bu veri ilham ve pazar analizi icindir; birebir kopyalama yapma.";
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

        long.TryParse(_shippingProfileInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var shippingProfileId);
        if (!_digitalCheckBox.Checked && shippingProfileId <= 0)
        {
            message = "Fiziksel urun icin shipping profile ID gerekli. Etsy Shop Manager > Settings > Shipping settings bolumunden profil ID'sini gir.";
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
            _digitalCheckBox.Checked,
            tags,
            SplitCommaList(_materialsTextBox.Text));
        message = "";
        return true;
    }

    private void ChooseImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Listing gorseli sec",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp;*.gif|All files|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _imagePathTextBox.Text = dialog.FileName;
        }
    }

    private string PrimaryKeyword() =>
        string.IsNullOrWhiteSpace(_keywordTextBox.Text)
            ? _shopTypeTextBox.Text.Trim()
            : _keywordTextBox.Text.Trim();

    private string BuildImagePrompt() =>
        "Create a marketplace-ready Etsy product photo/mockup for this product. Use clean neutral background, realistic lighting, clear product focus, no watermark, no logo, no copyrighted character branding. " +
        $"Shop type: {_shopTypeTextBox.Text.Trim()}. Product: {_titleTextBox.Text.Trim()}";

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
        public string Tags => string.Join(", ", Listing.Tags.Take(10));
    }
}
