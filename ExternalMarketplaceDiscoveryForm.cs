namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ExternalMarketplaceDiscoveryForm(IAiListingOptimizer aiOptimizer) : Form
{
    private readonly ExternalMarketplaceSearchService _searchService = new();
    private readonly EtsyApiClient _apiClient = new();
    private readonly AiListingImageGenerator _imageGenerator = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly CheckedListBox _sourcesList = new();
    private readonly TextBox _shopTypeTextBox = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly TextBox _externalTitleTextBox = new();
    private readonly TextBox _externalUrlTextBox = new();
    private readonly TextBox _titleTextBox = new();
    private readonly TextBox _descriptionTextBox = new();
    private readonly TextBox _tagsTextBox = new();
    private readonly TextBox _materialsTextBox = new();
    private readonly TextBox _notesTextBox = new();
    private readonly TextBox _imagePromptTextBox = new();
    private readonly TextBox _sourceImagePathTextBox = new();
    private readonly TextBox _generatedImagePathTextBox = new();
    private readonly NumericUpDown _priceInput = new() { Minimum = 1, Maximum = 100000, DecimalPlaces = 2, Value = 35 };
    private readonly NumericUpDown _quantityInput = new() { Minimum = 1, Maximum = 999, Value = 1 };
    private readonly TextBox _taxonomyInput = new();
    private readonly ComboBox _listingTypeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _shippingProfileComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _readinessStateComboBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new();

    private ExternalProductIdea? SelectedIdea => _bindingSource.Current as ExternalProductIdea;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Dis Pazar Urun Kesfi ve Etsy Taslak";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 820);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        _listingTypeComboBox.Items.AddRange(["Fiziksel urun", "Dijital urun"]);
        _listingTypeComboBox.SelectedIndex = 0;
        _listingTypeComboBox.SelectedIndexChanged += (_, _) => UpdateListingTypeControls();
        _shippingProfileComboBox.DisplayMember = nameof(EtsyShippingProfileOption.DisplayName);
        _shippingProfileComboBox.ValueMember = nameof(EtsyShippingProfileOption.ShippingProfileId);
        _shippingProfileComboBox.DropDown += async (_, _) => await LoadShippingProfilesAsync();
        _readinessStateComboBox.DisplayMember = nameof(EtsyReadinessStateOption.DisplayName);
        _readinessStateComboBox.ValueMember = nameof(EtsyReadinessStateOption.ReadinessStateId);
        _readinessStateComboBox.DropDown += async (_, _) => await LoadReadinessStatesAsync();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Dis Pazarlardan Urun Bul ve Etsy Listing Hazirla",
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Google, Trendyol, Hepsiburada ve eBay kaynaklari hazir";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildToolbar(), 0, 1);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDraftArea(), 0, 3);
        UpdateListingTypeControls();
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 2, Padding = new Padding(0, 0, 0, 8) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        for (var index = 5; index < 10; index++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        toolbar.Controls.Add(LabelFor("Magaza turu"), 0, 0);
        _shopTypeTextBox.Dock = DockStyle.Fill;
        _shopTypeTextBox.PlaceholderText = "Orn: 3D cosplay, yatak, dekor";
        toolbar.Controls.Add(_shopTypeTextBox, 1, 0);
        toolbar.Controls.Add(LabelFor("Aranacak urun"), 2, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Orn: painted fantasy bust";
        toolbar.Controls.Add(_keywordTextBox, 3, 0);
        toolbar.Controls.Add(LabelFor("Kaynaklar"), 0, 1);
        _sourcesList.Dock = DockStyle.Fill;
        _sourcesList.CheckOnClick = true;
        foreach (var source in _searchService.SourceNames)
        {
            _sourcesList.Items.Add(source, true);
        }

        toolbar.Controls.Add(_sourcesList, 1, 1);
        toolbar.SetColumnSpan(_sourcesList, 4);

        var search = CreateButton("Kaynaklarda Ara");
        search.Click += (_, _) => SearchExternalSources();
        toolbar.Controls.Add(search, 5, 0);
        var open = CreateButton("Kaynak Ac");
        open.Click += (_, _) => OpenSelectedSource();
        toolbar.Controls.Add(open, 6, 0);
        var verify = CreateButton("Etsy'de Dogrula");
        verify.Click += (_, _) => VerifyOnEtsy();
        toolbar.Controls.Add(verify, 7, 0);
        var aiDraft = CreateButton("AI Taslak");
        aiDraft.Click += async (_, _) => await GenerateAiDraftAsync();
        toolbar.Controls.Add(aiDraft, 8, 0);
        var create = CreateButton("Etsy Taslak");
        create.BackColor = Color.FromArgb(20, 126, 76);
        create.Click += async (_, _) => await CreateDraftListingAsync();
        toolbar.Controls.Add(create, 9, 0);
        foreach (var button in new[] { search, open, verify, aiDraft, create })
        {
            toolbar.SetRowSpan(button, 2);
        }

        return toolbar;
    }

    private Control BuildDraftArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 10, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        left.Controls.Add(LabelFor("Dis kaynak basligi"), 0, 0);
        _externalTitleTextBox.Dock = DockStyle.Fill;
        left.Controls.Add(_externalTitleTextBox, 0, 1);
        left.Controls.Add(LabelFor("Dis kaynak linki"), 0, 2);
        _externalUrlTextBox.Dock = DockStyle.Fill;
        left.Controls.Add(_externalUrlTextBox, 0, 3);
        left.Controls.Add(LabelFor("Etsy basligi"), 0, 4);
        _titleTextBox.Dock = DockStyle.Fill;
        _titleTextBox.Multiline = true;
        left.Controls.Add(_titleTextBox, 0, 5);
        left.Controls.Add(LabelFor("Etsy aciklamasi"), 0, 6);
        ConfigureMultiline(_descriptionTextBox);
        left.Controls.Add(_descriptionTextBox, 0, 7);
        layout.Controls.Add(left, 0, 0);

        var middle = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8 };
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        middle.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        middle.Controls.Add(LabelFor("Tagler"), 0, 0);
        ConfigureMultiline(_tagsTextBox);
        middle.Controls.Add(_tagsTextBox, 0, 1);
        middle.Controls.Add(LabelFor("Materyaller"), 0, 2);
        ConfigureMultiline(_materialsTextBox);
        middle.Controls.Add(_materialsTextBox, 0, 3);
        middle.Controls.Add(LabelFor("AI gorsel promptu"), 0, 4);
        ConfigureMultiline(_imagePromptTextBox);
        middle.Controls.Add(_imagePromptTextBox, 0, 5);
        middle.Controls.Add(LabelFor("Notlar / kaynak kontrolu"), 0, 6);
        ConfigureMultiline(_notesTextBox);
        _notesTextBox.ReadOnly = true;
        middle.Controls.Add(_notesTextBox, 0, 7);
        layout.Controls.Add(middle, 1, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 28, AutoScroll = true };
        for (var index = 0; index < 27; index++)
        {
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

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
        right.Controls.Add(LabelFor("Listing tipi"), 0, 6);
        right.Controls.Add(_listingTypeComboBox, 0, 7);
        right.Controls.Add(LabelFor("Shipping profile"), 0, 8);
        right.Controls.Add(_shippingProfileComboBox, 0, 9);
        right.Controls.Add(LabelFor("Hazirlik durumu"), 0, 10);
        right.Controls.Add(_readinessStateComboBox, 0, 11);
        right.Controls.Add(LabelFor("Kaynak gorsel dosyasi"), 0, 12);
        _sourceImagePathTextBox.Dock = DockStyle.Fill;
        right.Controls.Add(_sourceImagePathTextBox, 0, 13);
        var chooseSourceImage = CreateButton("Kaynak Gorsel Sec");
        chooseSourceImage.Click += (_, _) => ChooseSourceImage();
        right.Controls.Add(chooseSourceImage, 0, 14);
        right.Controls.Add(LabelFor("AI gorsel dosyasi"), 0, 15);
        _generatedImagePathTextBox.Dock = DockStyle.Fill;
        right.Controls.Add(_generatedImagePathTextBox, 0, 16);
        var generateImage = CreateButton("AI Gorsel Uret");
        generateImage.Click += async (_, _) => await GenerateAiImageAsync();
        right.Controls.Add(generateImage, 0, 17);
        var openImage = CreateButton("Gorseli Ac");
        openImage.Click += (_, _) => OpenGeneratedImage();
        right.Controls.Add(openImage, 0, 18);
        var open = CreateButton("Dis Kaynagi Ac");
        open.Click += (_, _) => OpenUrl(_externalUrlTextBox.Text);
        right.Controls.Add(open, 0, 19);
        var ai = CreateButton("AI Taslak Uret");
        ai.Click += async (_, _) => await GenerateAiDraftAsync();
        right.Controls.Add(ai, 0, 20);
        var verify = CreateButton("Etsy'de Dogrula");
        verify.Click += (_, _) => VerifyOnEtsy();
        right.Controls.Add(verify, 0, 21);
        var create = CreateButton("Etsy Taslak Ekle");
        create.BackColor = Color.FromArgb(20, 126, 76);
        create.Click += async (_, _) => await CreateDraftListingAsync();
        right.Controls.Add(create, 0, 22);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        right.Controls.Add(close, 0, 23);
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
        _grid.SelectionChanged += (_, _) => FillSelectedIdea();
        _grid.CellDoubleClick += (_, _) => OpenSelectedSource();
        AddColumn("Kaynak", nameof(ExternalProductIdea.Source), 130);
        AddColumn("Firsat", nameof(ExternalProductIdea.Opportunity), 75);
        AddColumn("Arama / urun fikri", nameof(ExternalProductIdea.Title), 420, true);
        AddColumn("Fiyat", nameof(ExternalProductIdea.Price), 90);
        AddColumn("Link", nameof(ExternalProductIdea.ProductUrl), 360);
        AddColumn("Not", nameof(ExternalProductIdea.Notes), 420);
    }

    private void SearchExternalSources()
    {
        var enabled = _sourcesList.CheckedItems.Cast<string>().ToList();
        var rows = _searchService.BuildSearchIdeas(_shopTypeTextBox.Text, _keywordTextBox.Text, enabled);
        _bindingSource.DataSource = rows;
        _statusLabel.Text = $"{rows.Count} dis kaynak arama linki hazirlandi";
        FillSelectedIdea();
    }

    private async Task GenerateAiDraftAsync()
    {
        var sourceTitle = _externalTitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourceTitle))
        {
            MessageBox.Show(this, "Once dis kaynak basligi veya urun fikri gir.", "AI taslak", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Dis kaynak urunu Etsy diline cevriliyor...";
            var input = new ListingOptimizationInput(
                sourceTitle,
                BuildExternalDescriptionForAi(),
                BuildSeedTags(),
                PrimaryKeyword());
            var result = await aiOptimizer.OptimizeAsync(input);
            _titleTextBox.Text = result.TitleSuggestions.FirstOrDefault() ?? SafeTitle(sourceTitle);
            _descriptionTextBox.Text = result.DescriptionDraft;
            _tagsTextBox.Text = string.Join(", ", result.TagSuggestions.Take(13));
            _materialsTextBox.Text = string.Join(", ", EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions));
            _notesTextBox.Text =
                "AI taslak dis kaynak urununden ilham alarak hazirlandi. Birebir kopyalama yapma; marka/telif riskini kontrol et." +
                $"{Environment.NewLine}Risk notlari: {string.Join("; ", result.RiskWarnings)}";
            _statusLabel.Text = "AI Etsy taslagi hazir";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI taslak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "AI taslak uretilemedi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task CreateDraftListingAsync()
    {
        if (!TryBuildDraftRequest(out var draft, out var message))
        {
            MessageBox.Show(this, message, "Etsy taslak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirmation =
            $"Bu islem Etsy magazanda TASLAK listing olusturacak.{Environment.NewLine}{Environment.NewLine}" +
            $"Dis kaynak: {_externalUrlTextBox.Text.Trim()}{Environment.NewLine}" +
            $"Baslik: {draft.Title}{Environment.NewLine}" +
            $"Fiyat: {draft.Price:0.00}{Environment.NewLine}" +
            $"Tip: {(draft.IsDigital ? "Dijital" : "Fiziksel")}{Environment.NewLine}" +
            $"Gorsel: {(SelectedImagePaths().Count == 0 ? "Yok" : $"{SelectedImagePaths().Count} dosya")}{Environment.NewLine}{Environment.NewLine}" +
            "Onayliyor musun?";
        if (MessageBox.Show(this, confirmation, "Etsy taslak onayi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Etsy'de dis pazar kaynakli taslak olusturuluyor...";
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
                OpenUrl(created.Url);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy taslak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Taslak olusturulamadi";
        }
        finally
        {
            UseWaitCursor = false;
        }
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
            message = "Gecerli Etsy taxonomy ID girmen gerekiyor.";
            return false;
        }

        var isDigital = IsDigitalListingSelected();
        var shippingProfileId = SelectedShippingProfileId();
        var readinessStateId = SelectedReadinessStateId();
        if (!isDigital && shippingProfileId <= 0)
        {
            message = "Fiziksel urun icin shipping profile secmen gerekiyor.";
            return false;
        }

        if (!isDigital && readinessStateId <= 0)
        {
            message = "Fiziksel urun icin hazirlik durumu secmen gerekiyor.";
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

    private void ChooseSourceImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Dis kaynaktan veya kendi urununden referans gorsel sec",
            Filter = "Gorsel dosyalari|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Tum dosyalar|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _sourceImagePathTextBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_imagePromptTextBox.Text))
        {
            _imagePromptTextBox.Text = BuildExternalImagePrompt();
        }

        _statusLabel.Text = "Kaynak gorsel secildi. AI gorsel uretmeden once promptu kontrol et.";
    }

    private async Task GenerateAiImageAsync()
    {
        var sourcePath = _sourceImagePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            MessageBox.Show(this, "Once kaynak gorsel dosyasi sec.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Kaynak gorsel referans alinarak AI gorsel uretiliyor...";
            var settings = AiOptimizationSettingsStore.Load();
            var path = await _imageGenerator.GenerateFromReferenceAsync(settings, BuildReferenceListingForImage(), BuildExternalImagePrompt(), sourcePath);
            _generatedImagePathTextBox.Text = path;
            _statusLabel.Text = "AI gorsel uretildi. Etsy taslak eklerken bu gorsel yuklenecek.";
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

    private void OpenGeneratedImage()
    {
        var imagePath = _generatedImagePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            imagePath = _sourceImagePathTextBox.Text.Trim();
        }

        if (!File.Exists(imagePath))
        {
            MessageBox.Show(this, "Acilacak gorsel dosyasi bulunamadi.", "Gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(imagePath) { UseShellExecute = true });
    }

    private IReadOnlyList<string> SelectedImagePaths()
    {
        var generated = SplitPathList(_generatedImagePathTextBox.Text);
        if (generated.Count > 0)
        {
            return generated;
        }

        return SplitPathList(_sourceImagePathTextBox.Text);
    }

    private MarketListingResult BuildReferenceListingForImage()
    {
        var title = _titleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = _externalTitleTextBox.Text.Trim();
        }

        return new MarketListingResult
        {
            ListingId = 0,
            Title = SafeTitle(title),
            Description = _descriptionTextBox.Text.Trim(),
            ListingUrl = _externalUrlTextBox.Text.Trim(),
            Price = _priceInput.Value,
            CurrencyCode = "USD",
            Quantity = (int)_quantityInput.Value,
            Tags = SplitCommaList(_tagsTextBox.Text).Take(13).ToList(),
        };
    }

    private string BuildExternalImagePrompt()
    {
        var productTitle = _titleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(productTitle))
        {
            productTitle = _externalTitleTextBox.Text.Trim();
        }

        var userPrompt = _imagePromptTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(userPrompt))
        {
            return userPrompt;
        }

        return
            "Use the selected source image as the product reference. Do not invent a new product, do not change the product shape, color, scale, proportions, visible details, or physical category. " +
            "Only improve the Etsy presentation: clean neutral studio background, realistic lighting, sharper product focus, natural shadow, marketplace-ready composition. " +
            "No watermark, no logo, no copyrighted character branding, no text overlay. " +
            $"Shop type: {_shopTypeTextBox.Text.Trim()}. Product: {productTitle}.";
    }

    private void FillSelectedIdea()
    {
        if (SelectedIdea is null)
        {
            return;
        }

        _externalTitleTextBox.Text = SelectedIdea.Title;
        _externalUrlTextBox.Text = SelectedIdea.ProductUrl;
        _notesTextBox.Text = SelectedIdea.Notes;
        if (string.IsNullOrWhiteSpace(_imagePromptTextBox.Text))
        {
            _imagePromptTextBox.Text = BuildExternalImagePrompt();
        }
    }

    private void OpenSelectedSource() => OpenUrl(SelectedIdea?.ProductUrl ?? _externalUrlTextBox.Text);

    private void VerifyOnEtsy()
    {
        var keyword = _externalTitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            keyword = _keywordTextBox.Text.Trim();
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Etsy'de dogrulamak icin once dis kaynak basligi veya aranacak urun gir.", "Etsy'de dogrula", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ProductDiscoveryListingCreatorForm(aiOptimizer, keyword);
        form.ShowDialog(this);
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
            if (profiles.Count > 0) _shippingProfileComboBox.SelectedIndex = 0;
            _statusLabel.Text = $"{profiles.Count} shipping profile listelendi";
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
            _statusLabel.Text = "Hazirlik durumu secenekleri aliniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var states = await _apiClient.GetOwnShopReadinessStateOptionsAsync(settings);
            EtsyApiSettingsStore.Save(settings);
            _readinessStateComboBox.DataSource = states;
            if (states.Count > 0) _readinessStateComboBox.SelectedIndex = 0;
            _statusLabel.Text = $"{states.Count} hazirlik durumu listelendi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Hazirlik durumu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Hazirlik durumu alinamadi";
        }
    }

    private string BuildExternalDescriptionForAi() =>
        "Create an original Etsy listing draft in English inspired by this external marketplace product research. " +
        "Do not copy competitor wording. Do not claim official, licensed, endorsed, branded, or affiliated status unless legally proven. " +
        "Keep the Etsy title, tags, materials, and description buyer-facing and English. Use Turkish only inside risk warnings if needed. " +
        $"Shop type: {_shopTypeTextBox.Text.Trim()}. " +
        $"External product title: {_externalTitleTextBox.Text.Trim()}. " +
        $"External source URL: {_externalUrlTextBox.Text.Trim()}. " +
        $"Research notes: {_notesTextBox.Text.Trim()}.";

    private IReadOnlyList<string> BuildSeedTags() =>
        SplitCommaList($"{_shopTypeTextBox.Text}, {_keywordTextBox.Text}, {_externalTitleTextBox.Text}")
            .SelectMany(item => item.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(item => item.Trim('-', '|', ',', '.', ':'))
            .Where(item => item.Length is >= 3 and <= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();

    private string PrimaryKeyword() =>
        string.IsNullOrWhiteSpace(_keywordTextBox.Text)
            ? _shopTypeTextBox.Text.Trim()
            : _keywordTextBox.Text.Trim();

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
        if (_readinessStateComboBox.SelectedValue is long value) return value;
        if (_readinessStateComboBox.SelectedItem is EtsyReadinessStateOption option) return option.ReadinessStateId;
        return long.TryParse(_readinessStateComboBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var typedValue)
            ? typedValue
            : 0;
    }

    private void UpdateListingTypeControls()
    {
        var physical = !IsDigitalListingSelected();
        _shippingProfileComboBox.Enabled = physical;
        _readinessStateComboBox.Enabled = physical;
    }

    private static string SafeTitle(string title) => title.Length <= 140 ? title : title[..140].TrimEnd();

    private static IReadOnlyList<string> SplitCommaList(string value) =>
        value
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

    private static IReadOnlyList<string> SplitPathList(string value) =>
        value
            .Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => path.Length > 0 && File.Exists(path))
            .ToList();

    private static void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

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
            Font = new Font("Segoe UI Semibold", 9.2F),
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
}
