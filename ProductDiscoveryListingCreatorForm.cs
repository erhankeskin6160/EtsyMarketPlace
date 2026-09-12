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
    private readonly ModernMultilineTextBox _descriptionTextBox = new();
    private readonly ModernMultilineTextBox _tagsTextBox = new();
    private readonly ModernMultilineTextBox _materialsTextBox = new();
    private readonly ModernMultilineTextBox _variationsTextBox = new();
    private readonly ModernMultilineTextBox _imagePromptTextBox = new();
    private readonly TextBox _imagePathTextBox = new();
    private readonly ModernMultilineTextBox _notesTextBox = new();
    private readonly ListingQualityReportControl _qualityReportControl = new();
    private readonly ModernNumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 30 };
    private readonly ModernNumericUpDown _priceInput = new() { Minimum = 1, Maximum = 100000, DecimalPlaces = 2, Value = 35 };
    private readonly ModernNumericUpDown _quantityInput = new() { Minimum = 1, Maximum = 999, Value = 1 };
    private readonly ModernNumericUpDown _imageCountInput = new() { Minimum = 1, Maximum = 10, Value = 1 };
    private readonly TextBox _taxonomyInput = new();
    private readonly TextBox _categoryTextBox = new() { ReadOnly = true };
    private readonly ModernComboBox _listingTypeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ModernComboBox _shippingProfileComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ModernComboBox _readinessStateComboBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly PictureBox _previewPictureBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _imageCounterLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Panel _busyOverlay = new() { Dock = DockStyle.Fill, Visible = false, BackColor = Color.FromArgb(235, 247, 248, 250) };
    private readonly Label _busyLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 12F), ForeColor = UiStyle.TextDark };
    private readonly ProgressBar _busyProgressBar = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28 };
    private readonly ModernSpinner _busySpinner = new() { Width = 74, Height = 74, Anchor = AnchorStyles.None, BackColor = Color.White };
    private readonly System.Windows.Forms.Timer _busyTimer = new() { Interval = 350 };
    private readonly Label _lblTitleCounter = new() { AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 8.5F) };
    private readonly Label _lblTagCounter = new() { AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 8.5F) };
    private readonly Label _statusLabel = new();
    private readonly Label _lblAiBadge = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.FromArgb(30, 41, 59),
        Padding = new Padding(8, 4, 8, 4),
        Cursor = Cursors.Hand,
        Anchor = AnchorStyles.Right,
        Margin = new Padding(0, 0, 10, 0)
    };
    private List<IdeaRow> _rows = [];
    private int _selectedImageIndex;
    private int _busyFrame;
    private bool _favoriteSortDescending;
    private bool _opportunitySortDescending;
    private bool _viewsSortDescending;
    private long? _lastCreatedListingId;
    private int _hoveredButtonRow = -1;

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
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));

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

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Urun Bul ve Listing Hazirla",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _lblAiBadge.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
            UpdateAiBadge();
        };
        UpdateAiBadge();
        header.Controls.Add(_lblAiBadge, 1, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "Anahtar kelime girip Etsy'de urun arayin";
        header.Controls.Add(_statusLabel, 2, 0);
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

    private void UpdateAiBadge()
    {
        var settings = AiOptimizationSettingsStore.Load();
        _lblAiBadge.Text = settings.GetActiveBadgeText();
        _lblAiBadge.BackColor = settings.UseOpenAi
            ? Color.FromArgb(16, 80, 50)
            : (settings.UseGemini ? Color.FromArgb(20, 60, 120) : Color.FromArgb(40, 50, 65));
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
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2, Padding = new Padding(0, 4, 0, 8) };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Label Anahtar Kelime
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // _keywordTextBox
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // Label Sonuc
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // _limitInput
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Etsy'de Ara
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); // 🚀 1-Tık Full AI Listing
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); // Etsy Taslak Ekle
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Kapat

        toolbar.Controls.Add(LabelFor("Anahtar Kelime:"), 0, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Örn: 3d cosplay prop, miniature figure, wall art";
        toolbar.Controls.Add(_keywordTextBox, 1, 0);

        toolbar.Controls.Add(LabelFor("Adet:"), 2, 0);
        _limitInput.Dock = DockStyle.Fill;
        toolbar.Controls.Add(_limitInput, 3, 0);

        var search = CreateButton("🔍 Etsy'de Ara");
        search.Click += async (_, _) => await SearchIdeasAsync();
        toolbar.Controls.Add(search, 4, 0);

        var btnAutoPilot = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🚀 1-Tık Full AI Listing",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold)
        };
        btnAutoPilot.Click += async (_, _) => await GenerateFullAutoPilotListingAsync();
        toolbar.Controls.Add(btnAutoPilot, 5, 0);

        var create = CreateButton("💾 Etsy Taslak Ekle");
        create.BackColor = Color.FromArgb(20, 126, 76);
        create.Click += async (_, _) => await CreateDraftListingAsync();
        toolbar.Controls.Add(create, 6, 0);

        var close = CreateButton("Kapat", isSecondary: true);
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(close, 7, 0);

        // Row 2: Linkten Al & 2. Sayfa
        toolbar.Controls.Add(LabelFor("Etsy Listing Linki:"), 0, 1);
        _listingLinkTextBox.Dock = DockStyle.Fill;
        _listingLinkTextBox.PlaceholderText = "Örn: https://www.etsy.com/listing/123456789/urun-adi";
        toolbar.Controls.Add(_listingLinkTextBox, 1, 1);
        toolbar.SetColumnSpan(_listingLinkTextBox, 3);

        var importLink = CreateButton("🔗 Linkten Al");
        importLink.Click += async (_, _) => await ImportListingLinkAsync();
        toolbar.Controls.Add(importLink, 4, 1);

        var btnImage = CreateButton("✨ AI Görsel");
        btnImage.Click += async (_, _) => await GenerateImageAsync();
        toolbar.Controls.Add(btnImage, 5, 1);

        var aiReview = CreateButton("📄 2. Sayfa", isSecondary: true);
        aiReview.Click += (_, _) => OpenAiReviewPage();
        toolbar.Controls.Add(aiReview, 6, 1);

        return toolbar;
    }

    private Control BuildDraftArea()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 6, 0, 0) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Left Image Preview & Quick Actions
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Right Tabbed Workspace

        // --- SOL PANEL: Görsel Önizleme & Hızlı Butonlar ---
        var leftPanel = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(8),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Picture Preview
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // Carousel controls
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // 1-Tık Full AI
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // AI Görsel Üret

        leftLayout.Controls.Add(_previewPictureBox, 0, 0);

        var navPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        var prevBtn = CreateButton("<");
        prevBtn.Click += async (_, _) => await MoveSelectedImageAsync(-1);
        navPanel.Controls.Add(prevBtn, 0, 0);
        _imageCounterLabel.Text = "Resim yok";
        _imageCounterLabel.Font = new Font("Segoe UI Semibold", 8F);
        navPanel.Controls.Add(_imageCounterLabel, 1, 0);
        var nextBtn = CreateButton(">");
        nextBtn.Click += async (_, _) => await MoveSelectedImageAsync(1);
        navPanel.Controls.Add(nextBtn, 2, 0);
        leftLayout.Controls.Add(navPanel, 0, 1);

        var btnQuickFullAi = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🚀 1-Tık Full AI",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        };
        btnQuickFullAi.Click += async (_, _) => await GenerateFullAutoPilotListingAsync();
        leftLayout.Controls.Add(btnQuickFullAi, 0, 2);

        var btnGenImg = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "✨ AI Görsel Üret",
            NormalColor = UiStyle.AiColor,
            HoverColor = Color.FromArgb(147, 51, 234),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        };
        btnGenImg.Click += async (_, _) => await GenerateImageAsync();
        leftLayout.Controls.Add(btnGenImg, 0, 3);

        leftPanel.Controls.Add(leftLayout);
        root.Controls.Add(leftPanel, 0, 0);

        // --- SAĞ PANEL: Sekmeli Modern Çalışma Alanı ---
        var tabControl = new ModernTabControl 
        { 
            Dock = DockStyle.Fill, 
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ItemSize = new Size(160, 36),
            HeaderBackgroundColor = UiStyle.BackgroundColor,
            ActiveTabColor = UiStyle.PrimaryColor,
            InactiveTabColor = UiStyle.CardBackground,
            ActiveTextColor = Color.White,
            InactiveTextColor = UiStyle.TextMuted,
            BorderColor = UiStyle.BorderColor
        };

        // SEKME 1: ✍️ Başlık & SEO Açıklama
        var tabSeo = new TabPage("✍️ Başlık & SEO Açıklama") { BackColor = UiStyle.CardBackground, Padding = new Padding(12) };
        tabSeo.Controls.Add(BuildSeoTitleDescTab());
        tabControl.TabPages.Add(tabSeo);

        // SEKME 2: 🏷️ 13 Tag & Materyaller
        var tabTags = new TabPage("🏷️ 13 Tag & Materyaller") { BackColor = UiStyle.CardBackground, Padding = new Padding(12) };
        tabTags.Controls.Add(BuildTagsMaterialsTab());
        tabControl.TabPages.Add(tabTags);

        // SEKME 3: 💰 Fiyat, Kargo & Etsy Ayarları
        var tabSettings = new TabPage("💰 Fiyat & Kargo Ayarları") { BackColor = UiStyle.CardBackground, Padding = new Padding(12) };
        tabSettings.Controls.Add(BuildPriceShippingTab());
        tabControl.TabPages.Add(tabSettings);

        // SEKME 4: 🎨 AI Görsel Promptları
        var tabVisuals = new TabPage("🎨 AI Görsel & Promptlar") { BackColor = UiStyle.CardBackground, Padding = new Padding(12) };
        tabVisuals.Controls.Add(BuildVisualsTab());
        tabControl.TabPages.Add(tabVisuals);

        // SEKME 5: 📊 Kalite Karnesi & Notlar
        var tabQuality = new TabPage("📊 Kalite Karnesi & Notlar") { BackColor = UiStyle.CardBackground, Padding = new Padding(12) };
        tabQuality.Controls.Add(BuildQualityNotesTab());
        tabControl.TabPages.Add(tabQuality);

        root.Controls.Add(tabControl, 1, 0);
        UpdateListingTypeControls();

        _titleTextBox.TextChanged += (_, _) => UpdateTitleCounter();
        _tagsTextBox.TextChanged += (_, _) => UpdateTagCounter();

        return root;
    }

    private Control BuildSeoTitleDescTab()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Başlık header + Sayaç
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Başlık kutusu
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Açıklama header + AI butonu
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Açıklama kutusu

        var titleHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        titleHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        titleHeader.Controls.Add(LabelFor("Etsy SEO Başlığı (Maks. 140 Karakter):"), 0, 0);
        _lblTitleCounter.Text = "0 / 140 Karakter";
        _lblTitleCounter.TextAlign = ContentAlignment.MiddleRight;
        titleHeader.Controls.Add(_lblTitleCounter, 1, 0);
        panel.Controls.Add(titleHeader, 0, 0);

        _titleTextBox.Dock = DockStyle.Fill;
        _titleTextBox.Multiline = true;
        panel.Controls.Add(_titleTextBox, 0, 1);

        var descHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        descHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        descHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        descHeader.Controls.Add(LabelFor("Satış Odaklı Açıklama (Biçimlendirilmiş):"), 0, 0);

        var btnAiDesc = CreateButton("✨ AI Açıklama Yenile");
        btnAiDesc.BackColor = UiStyle.AiColor;
        btnAiDesc.ForeColor = Color.White;
        btnAiDesc.Font = UiStyle.SemiboldBaseFont;
        btnAiDesc.Click += async (_, _) => await GenerateDescriptionWithAiAsync();
        descHeader.Controls.Add(btnAiDesc, 1, 0);
        panel.Controls.Add(descHeader, 0, 2);

        ConfigureMultiline(_descriptionTextBox);
        panel.Controls.Add(_descriptionTextBox, 0, 3);

        return panel;
    }

    private Control BuildTagsMaterialsTab()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Tag Header + Sayaç + Casus Butonu
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); // Tag Kutusu
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Materyal Header
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Materyal Kutusu
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); // Varyasyon Kutusu

        var tagHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        tagHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tagHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        tagHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        tagHeader.Controls.Add(LabelFor("13 Adet Etsy Tag (Virgülle veya alt alta ayırın, maks 20 karakter):"), 0, 0);
        _lblTagCounter.Text = "0 / 13 Tag";
        _lblTagCounter.TextAlign = ContentAlignment.MiddleRight;
        tagHeader.Controls.Add(_lblTagCounter, 1, 0);

        var btnTagSpy = CreateButton("🕵️ Rakip Taglerini Al");
        btnTagSpy.Click += (_, _) => CopyCompetitorTags();
        tagHeader.Controls.Add(btnTagSpy, 2, 0);
        panel.Controls.Add(tagHeader, 0, 0);

        ConfigureMultiline(_tagsTextBox);
        panel.Controls.Add(_tagsTextBox, 0, 1);

        panel.Controls.Add(LabelFor("Ürün Materyalleri:"), 0, 2);
        ConfigureMultiline(_materialsTextBox);
        panel.Controls.Add(_materialsTextBox, 0, 3);

        var varHeader = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        varHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        varHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        varHeader.Controls.Add(LabelFor("Varyasyon & Beden Önerileri:"), 0, 0);
        ConfigureMultiline(_variationsTextBox);
        varHeader.Controls.Add(_variationsTextBox, 0, 1);
        panel.Controls.Add(varHeader, 0, 4);

        return panel;
    }

    private Control BuildPriceShippingTab()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(6) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

        // Sol Sütun 1: Fiyat ($)
        layout.Controls.Add(CreateFieldContainer("Satış Fiyatı ($ USD):", _priceInput), 0, 0);

        // Sağ Sütun 1: Taxonomy ID
        layout.Controls.Add(CreateFieldContainer("Taxonomy ID (Kategori Kodu):", _taxonomyInput), 1, 0);

        // Sol Sütun 2: Stok Adedi
        layout.Controls.Add(CreateFieldContainer("Stok Miktarı (Quantity):", _quantityInput), 0, 1);

        // Sağ Sütun 2: Kategori Açıklaması
        layout.Controls.Add(CreateFieldContainer("Etsy Kategori Yolu:", _categoryTextBox), 1, 1);

        // Sol Sütun 3: Listing Tipi
        layout.Controls.Add(CreateFieldContainer("Listing Türü:", _listingTypeComboBox), 0, 2);

        // Sağ Sütun 3: Kargo Profili
        layout.Controls.Add(CreateFieldContainer("Kargo Profili (Shipping Profile):", _shippingProfileComboBox), 1, 2);

        // Sol Sütun 4: AI Görsel Adedi
        layout.Controls.Add(CreateFieldContainer("Üretilecek AI Görsel Adedi:", _imageCountInput), 0, 3);

        // Sağ Sütun 4: Hazırlık Durumu
        layout.Controls.Add(CreateFieldContainer("Hazırlık Durumu (Readiness State):", _readinessStateComboBox), 1, 3);

        return layout;
    }

    private static Control CreateFieldContainer(string labelText, Control inputControl)
    {
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(4, 2, 4, 2) };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = labelText,
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 8.5F),
            TextAlign = ContentAlignment.BottomLeft
        };
        container.Controls.Add(label, 0, 0);

        inputControl.Dock = DockStyle.Fill;
        container.Controls.Add(inputControl, 0, 1);
        return container;
    }

    private Control BuildVisualsTab()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(8) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        panel.Controls.Add(LabelFor("AI Görsel Promptları (Midjourney / DALL-E - Satır Satır):"), 0, 0);
        ConfigureMultiline(_imagePromptTextBox);
        panel.Controls.Add(_imagePromptTextBox, 0, 1);

        panel.Controls.Add(LabelFor("Manuel Seçilen Görsel Dosyası Yolu:"), 0, 2);

        var fileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _imagePathTextBox.Dock = DockStyle.Fill;
        fileRow.Controls.Add(_imagePathTextBox, 0, 0);
        var choose = CreateButton("📁 Dosyadan Seç");
        choose.Click += (_, _) => ChooseImage();
        fileRow.Controls.Add(choose, 1, 0);
        panel.Controls.Add(fileRow, 0, 3);

        return panel;
    }

    private Control BuildQualityNotesTab()
    {
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(6) };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var qualityCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(8),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        qualityCard.Controls.Add(_qualityReportControl);
        split.Controls.Add(qualityCard, 0, 0);

        var notesCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(8),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        var notesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        notesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        notesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        notesLayout.Controls.Add(LabelFor("📝 Optimizasyon & Mağaza Notları:"), 0, 0);
        ConfigureMultiline(_notesTextBox);
        notesLayout.Controls.Add(_notesTextBox, 0, 1);
        notesCard.Controls.Add(notesLayout);
        split.Controls.Add(notesCard, 1, 0);

        return split;
    }

    private void UpdateTitleCounter()
    {
        int len = _titleTextBox.Text.Length;
        _lblTitleCounter.Text = $"{len} / 140 Karakter";
        _lblTitleCounter.ForeColor = len > 140 ? UiStyle.DangerColor : (len >= 100 ? UiStyle.SuccessColor : UiStyle.TextMuted);
    }

    private void UpdateTagCounter()
    {
        var tags = _tagsTextBox.Text
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        int overLengthCount = tags.Count(t => t.Length > 20);
        _lblTagCounter.Text = $"{tags.Count} / 13 Tag" + (overLengthCount > 0 ? $" ({overLengthCount} tag 20 karaktere sığmıyor!)" : "");
        _lblTagCounter.ForeColor = tags.Count > 13 || overLengthCount > 0 ? UiStyle.DangerColor : (tags.Count == 13 ? UiStyle.SuccessColor : UiStyle.TextMuted);
    }

    private void CopyCompetitorTags()
    {
        if (SelectedRow != null && SelectedRow.Listing.Tags.Count > 0)
        {
            _tagsTextBox.Text = string.Join(", ", SelectedRow.Listing.Tags.Take(13));
            UpdateTagCounter();
            _statusLabel.Text = $"Seçilen rakip üründen {Math.Min(13, SelectedRow.Listing.Tags.Count)} tag kopyalandı";
        }
        else
        {
            MessageBox.Show(this, "Seçilen üründe kopyalanacak tag bulunamadı.", "Tag Casusu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task GenerateFullAutoPilotListingAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Lütfen önce tablodan bir rakip ürün fikri seçin.", "1-Tık Full AI Listing", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            StartBusy("🚀 1-Tık Full AI Listing hazırlanıyor...");
            _statusLabel.Text = "Yapay zeka SEO başlığı, 13 tag, açıklama ve görselleri hazırlıyor...";
            
            // 1. Taslak ve SEO bilgilerini üret
            await GenerateDraftAsync();
            
            // 2. Açıklamayı AI ile zenginleştir
            SetBusyMessage("Satış odaklı AI açıklaması yazılıyor...");
            await GenerateDescriptionWithAiAsync();

            _statusLabel.Text = "✅ 1-Tık Full AI Listing başarıyla hazırlandı! İnceleyip 'Etsy Taslak Ekle' ile gönderebilirsiniz.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "AI Listing oluşturulurken hata: " + ex.Message, "Full AI Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            StopBusy();
        }
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
        _grid.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ListingLink")
            {
                OpenUrl((_grid.Rows[e.RowIndex].DataBoundItem as IdeaRow)?.Listing.ListingUrl);
            }
        };
        _grid.CellMouseMove += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ListingLink")
            {
                _grid.Cursor = Cursors.Hand;
                if (_hoveredButtonRow != e.RowIndex)
                {
                    int prev = _hoveredButtonRow;
                    _hoveredButtonRow = e.RowIndex;
                    if (prev >= 0 && prev < _grid.RowCount)
                    {
                        _grid.InvalidateCell(e.ColumnIndex, prev);
                    }
                    _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                }
            }
            else
            {
                if (_hoveredButtonRow != -1)
                {
                    int prev = _hoveredButtonRow;
                    _hoveredButtonRow = -1;
                    _grid.Cursor = Cursors.Default;
                    int linkColIndex = _grid.Columns["ListingLink"]?.Index ?? -1;
                    if (linkColIndex >= 0 && prev >= 0 && prev < _grid.RowCount)
                    {
                        _grid.InvalidateCell(linkColIndex, prev);
                    }
                }
            }
        };
        _grid.CellMouseLeave += (_, e) =>
        {
            if (_hoveredButtonRow != -1)
            {
                int prev = _hoveredButtonRow;
                _hoveredButtonRow = -1;
                _grid.Cursor = Cursors.Default;
                int linkColIndex = _grid.Columns["ListingLink"]?.Index ?? -1;
                if (linkColIndex >= 0 && prev >= 0 && prev < _grid.RowCount)
                {
                    _grid.InvalidateCell(linkColIndex, prev);
                }
            }
        };
        _grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ListingLink")
            {
                e.PaintBackground(e.ClipBounds, (e.State & DataGridViewElementStates.Selected) != 0);

                var g = e.Graphics!;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                bool isHovered = e.RowIndex == _hoveredButtonRow;
                Color linkColor = isHovered ? Color.FromArgb(56, 189, 248) : Color.FromArgb(14, 165, 233); // Sky 400 hover, Sky 500 normal
                FontStyle fontStyle = isHovered ? (FontStyle.Bold | FontStyle.Underline) : FontStyle.Regular;

                using var font = new Font("Segoe UI Semibold", 9F, fontStyle);
                TextRenderer.DrawText(
                    g,
                    "🔗 İlanı Gör",
                    font,
                    e.CellBounds,
                    linkColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
            }
        };
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(IdeaRow.Thumbnail))
            {
                if (_grid.Rows[e.RowIndex].DataBoundItem is IdeaRow row)
                {
                    e.Value = row.Listing.ThumbnailImage;
                    e.FormattingApplied = true;
                }
            }
        };
        _grid.DataError += (_, _) => { };
        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(IdeaRow.Thumbnail),
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            Width = 78,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null }
        });
        AddColumn("Firsat", nameof(IdeaRow.Opportunity), 70);
        AddColumn("Urun fikri", nameof(IdeaRow.Title), 360, true);
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = "Rakip listing",
            Name = "ListingLink",
            Text = "Aç ↗",
            UseColumnTextForButtonValue = true,
            Width = 115,
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
            _bindingSource.DataSource = _rows;
            _statusLabel.Text = $"{_rows.Count} Etsy urunu listelendi";
            FillFromSelectedIdea();
            _ = LoadGridThumbnailsAsync();
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
            _bindingSource.DataSource = _rows;
            _bindingSource.Position = 0;
            FillFromSelectedIdea();
            _ = LoadGridThumbnailsAsync();
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

        var match = Regex.Match(value, @"/listing/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(value, @"(?:listing|copy)/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        }
        if (!match.Success)
        {
            match = Regex.Match(value, @"listing_id=(?<id>\d{6,})", RegexOptions.IgnoreCase);
        }
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
            _statusLabel.Text = "AI taslak hazirlaniyor...";
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
                for (var attempt = 0; attempt < Math.Min(1, ListingDraftRepairService.MaxRepairIterations) && decision.NeedsRepair; attempt++)
                {
                    _statusLabel.Text = $"Taslak optimize ediliyor...";
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
                        using var repairCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        var repairResult = await aiOptimizer.OptimizeAsync(repairInput, repairCts.Token);

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
                        repairLog.AppendLine($"Onarim denemesi {attempt + 1} atlandi, mevcut kaliteli taslak korundu.");
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

            _qualityReportControl.SetReport(validationReport, async () => await RepairDraftManuallyAsync());
            _notesTextBox.Text =
                $"Taslak kaynagi: {DraftSourceLabel(settings)}. Metin Ingilizce uretildi; kategori taslak icerigine gore yeniden onerildi. Varyasyon alani sadece Etsy listinginden okunan gercek varyasyonlarla doldurulur, AI varyasyon uretmez. Etsy'ye eklemeden once fiyat, stok, taxonomy ve kargo profilini kontrol et.{Environment.NewLine}{Environment.NewLine}" +
                ListingDraftValidator.FormatReport(validationReport) +
                (repairLog.Length > 0 ? $"{Environment.NewLine}{Environment.NewLine}Onarim gecmisi:{Environment.NewLine}{repairLog}" : "");
            _statusLabel.Text = "Listing taslagi uretildi";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "AI zaman asimina ugradi, yerel kural motoru devreye girdi";
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

    private async Task RepairDraftManuallyAsync()
    {
        if (SelectedRow is null) return;
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI taslagi manuel onariyor...";
            var listing = SelectedRow.Listing;
            var tags = _tagsTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var materials = _materialsTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).ToList();
            var repairService = new ListingDraftRepairService();
            var validationReport = repairService.ValidateDraft(
                _titleTextBox.Text,
                _descriptionTextBox.Text,
                tags,
                materials,
                _categoryTextBox.Text,
                PrimaryKeyword());

            var decision = repairService.Evaluate(validationReport);
            if (!decision.NeedsRepair)
            {
                MessageBox.Show(this, "Taslak zaten yeterli kalitede (>=75), ekstra onarim gerekmiyor.", "AI Onarim", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _statusLabel.Text = "Taslak puani yeterli";
                return;
            }

            var repairPrompt = ListingDraftRepairService.BuildRepairPrompt(
                decision,
                _titleTextBox.Text,
                _descriptionTextBox.Text,
                tags,
                materials,
                PrimaryKeyword());

            var repairInput = new ListingOptimizationInput(
                _titleTextBox.Text,
                repairPrompt,
                tags,
                PrimaryKeyword());

            var repairResult = await aiOptimizer.OptimizeAsync(repairInput);

            if (repairResult.TitleSuggestions.Count > 0)
                _titleTextBox.Text = SelectEnglishTitle(repairResult.TitleSuggestions, listing);
            if (repairResult.TagSuggestions.Count > 0)
                _tagsTextBox.Text = string.Join(", ", repairResult.TagSuggestions.Take(13));
            if (!string.IsNullOrWhiteSpace(repairResult.DescriptionDraft))
                _descriptionTextBox.Text = SelectEnglishDescription(repairResult.DescriptionDraft, listing, materials);
            if (repairResult.MaterialSuggestions.Count > 0)
            {
                materials = EtsyApiClient.NormalizeListingMaterialsForEtsy(repairResult.MaterialSuggestions);
                _materialsTextBox.Text = string.Join(", ", materials);
            }

            tags = _tagsTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var updatedReport = repairService.ValidateDraft(
                _titleTextBox.Text,
                _descriptionTextBox.Text,
                tags,
                materials,
                _categoryTextBox.Text,
                PrimaryKeyword());

            _qualityReportControl.SetReport(updatedReport, async () => await RepairDraftManuallyAsync());
            _statusLabel.Text = $"Taslak manuel onarildi (Yeni puan: {updatedReport.OverallScore}/100)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI Onarim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Onarim basarisiz";
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

        if (!string.IsNullOrWhiteSpace(listing.Description))
        {
            _descriptionTextBox.Text = listing.Description;
        }
        else
        {
            _ = FetchAndSetDescriptionAsync(listing);
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

    private async Task FetchAndSetDescriptionAsync(MarketListingResult listing)
    {
        if (listing.ListingId <= 0) return;
        try
        {
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials) return;

            var fullListing = await _apiClient.GetPublicListingAsync(settings, listing.ListingId);
            if (!string.IsNullOrWhiteSpace(fullListing.Description))
            {
                listing.Description = fullListing.Description;
                if (SelectedRow?.Listing.ListingId == listing.ListingId)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(() =>
                        {
                            if (string.IsNullOrWhiteSpace(_descriptionTextBox.Text))
                            {
                                _descriptionTextBox.Text = listing.Description;
                            }
                        });
                    }
                }
            }
        }
        catch
        {
            // Silently ignore background fetch errors
        }
    }

    private async Task GenerateDescriptionWithAiAsync()
    {
        var listing = SelectedRow?.Listing;
        if (listing is null && string.IsNullOrWhiteSpace(_titleTextBox.Text))
        {
            MessageBox.Show(this, "Once bir urun fikri secin veya baslik girin.", "AI Aciklama Uret", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            StartBusy("AI ile aciklama uretiliyor");
            _statusLabel.Text = "Yapay zeka ile aciklama uretiliyor...";

            var title = string.IsNullOrWhiteSpace(_titleTextBox.Text) ? listing?.Title ?? "" : _titleTextBox.Text;
            var currentDesc = string.IsNullOrWhiteSpace(_descriptionTextBox.Text) ? listing?.Description ?? "" : _descriptionTextBox.Text;
            var tags = string.IsNullOrWhiteSpace(_tagsTextBox.Text)
                ? (listing?.Tags ?? [])
                : _tagsTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

            var input = new ListingOptimizationInput(title, currentDesc, tags, PrimaryKeyword());
            var result = await aiOptimizer.OptimizeAsync(input);

            var dummyListing = listing ?? new MarketListingResult { Title = title, Description = currentDesc, Tags = tags };
            var materials = EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions);
            var generatedDesc = SelectEnglishDescription(result.DescriptionDraft, dummyListing, materials);

            if (!string.IsNullOrWhiteSpace(generatedDesc))
            {
                _descriptionTextBox.Text = generatedDesc;
                _statusLabel.Text = "Yapay zeka ile yeni aciklama olusturuldu";
            }
            else
            {
                _statusLabel.Text = "Aciklama uretilemedi";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI Aciklama Uret", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "AI aciklama uretimi basarisiz";
        }
        finally
        {
            StopBusy();
        }
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
        if (_rows.Count == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(4);
        var currentRows = _rows.ToList();

        var tasks = currentRows.Select(async row =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (row.Listing.ThumbnailImage is not null)
                {
                    return;
                }

                await EnsureListingImagesAsync(row.Listing);

                var imageUrl = row.Listing.ImageUrls.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    imageUrl = row.Listing.ImageUrl;
                }

                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return;
                }

                using var image = await DownloadImageAsync(imageUrl);
                row.Listing.ThumbnailImage = CreateThumbnail(image, 72, 58);

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        try
                        {
                            _bindingSource.ResetBindings(false);
                            _grid.Invalidate();
                        }
                        catch { }
                    });
                }
            }
            catch
            {
                // Gorsel yuklenemezse urun listesini kullanilabilir tut
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (!IsDisposed && IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                try
                {
                    _bindingSource.ResetBindings(false);
                    _grid.Refresh();
                }
                catch { }
            });
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
            if (listing.ThumbnailImage is null)
            {
                listing.ThumbnailImage = CreateThumbnail(image, 72, 58);
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        try
                        {
                            _bindingSource.ResetBindings(false);
                            _grid.Invalidate();
                        }
                        catch { }
                    });
                }
            }
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
        return listing.Title.Trim();
    }

    private static string BuildDraftSourceDescription(MarketListingResult listing) =>
        listing.Description?.Trim() ?? "";

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
            var title = SanitizeTitle(suggestion);
            if (title.Length == 0) continue;
            if (LooksLikePromptLeak(title)) continue;
            if (requiredTerms.Count == 0 || requiredTerms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return title.Length <= 140 ? title : title[..140].TrimEnd();
            }
        }

        return BuildSafeTitle(listing);
    }

    private static string SanitizeTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = raw.Trim().Trim('"', '\'', '`', '*');
        var prefixesToRemove = new[]
        {
            "OUTPUT LANGUAGE: English only. Do not write Turkish.",
            "OUTPUT LANGUAGE: English only.",
            "OUTPUT LANGUAGE:",
            "English only.",
            "Title:",
            "Etsy Title:",
            "Title 1:",
            "Title 2:",
            "Title 3:",
            "1.",
            "2.",
            "3."
        };

        foreach (var prefix in prefixesToRemove)
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[prefix.Length..].Trim().TrimStart('-', '|', ':', ' ');
            }
        }

        return clean.Trim();
    }

    private string BuildSafeTitle(MarketListingResult listing)
    {
        var primaryPart = listing.Title.Split(['|', '-', ',', '–', '—', '/'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? listing.Title.Trim();
        var keyword = PrimaryKeyword();
        var tag = listing.Tags.FirstOrDefault() ?? "Handcrafted Gift";
        var candidate = $"{primaryPart} | {keyword} | {tag}";
        return candidate.Length <= 140 ? candidate : candidate[..140].TrimEnd();
    }

    private string SelectEnglishDescription(
        string description,
        MarketListingResult listing,
        IReadOnlyList<string> materialSuggestions)
    {
        var cleanDescription = SanitizeDescription(description);
        if (cleanDescription.Length > 50 && !LooksLikeTurkish(cleanDescription))
        {
            return cleanDescription;
        }

        return BuildDynamicEnglishDescription(listing, materialSuggestions);
    }

    private static string SanitizeDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var lines = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var cleanLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Selected listing title:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Etsy search keyword:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Current competitor category:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Competitor description:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("OUTPUT LANGUAGE:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Do not write Turkish", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Publishing review:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cleanLines.Add(line);
        }

        return string.Join(Environment.NewLine, cleanLines).Trim();
    }

    private string BuildDynamicEnglishDescription(
        MarketListingResult listing,
        IReadOnlyList<string> materialSuggestions)
    {
        var title = SelectRelevantTitle([listing.Title], listing);
        var productName = ShortProductName(title);
        var searchIntent = PrimaryKeyword();
        var materials = materialSuggestions.Count > 0
            ? materialSuggestions
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList()
            : InferMaterials(listing);
        var materialText = string.Join(", ", materials);
        var useCases = BuildUseCases($"{title} {listing.Description} {string.Join(' ', listing.Tags)}");

        var sb = new StringBuilder();

        // 1. Google SEO Hook & Product Identity (First 160-200 chars)
        sb.AppendLine($"Elevate your collection with this premium {productName}! Designed for enthusiasts searching for {searchIntent}, this handcrafted piece combines standout aesthetics with durable craftsmanship.");
        sb.AppendLine();

        // 2. Why You'll Love It
        sb.AppendLine("✨ WHY YOU'LL LOVE IT:");
        sb.AppendLine($"• Expertly crafted with high-grade {materialText} for a clean, premium finish.");
        sb.AppendLine("• Ideal for enthusiasts, tabletop displays, cosplay setups, or daily fidget practice.");
        sb.AppendLine("• Lightweight, durable, and balanced for effortless handling and display.");
        sb.AppendLine();

        // 3. Specifications & Materials
        sb.AppendLine("📏 SPECIFICATIONS & DETAILS:");
        sb.AppendLine($"• Material: {materialText}");
        sb.AppendLine("• Production: Precision 3D printed & hand-inspected before dispatch");
        sb.AppendLine("• Finish: Smooth, high-detail finish with rich color accuracy");
        sb.AppendLine();

        // 4. Perfect Gift & Audience
        sb.AppendLine("🎁 PERFECT GIFT FOR:");
        sb.AppendLine($"• Great for {string.Join(", ", useCases)}");
        sb.AppendLine("• Unique gift idea for gamers, collectors, hobbyists, birthdays, and holidays.");
        sb.AppendLine();

        // 5. Packaging & Shipping
        sb.AppendLine("📦 PACKAGING & SHIPPING:");
        sb.AppendLine("• Securely wrapped in protective packaging to ensure 100% safe worldwide delivery.");
        sb.AppendLine("• Tracking number provided immediately upon dispatch.");
        sb.AppendLine();

        // 6. Custom Requests
        sb.AppendLine("💬 CUSTOM REQUESTS & QUESTIONS:");
        sb.AppendLine("• Need custom colors, sizing, or have questions? Feel free to send us a message anytime!");

        return sb.ToString().Trim();
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
            || text.Contains("search keyword")
            || text.Contains("output language")
            || text.Contains("english only")
            || text.Contains("do not write")
            || text.Contains("write turkish")
            || text.Contains("system instruction")
            || text.Contains("json format");
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

    private static void ConfigureMultiline(ModernMultilineTextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        ForeColor = UiStyle.TextDark,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text, bool isSecondary = false) => UiStyle.CreateButton(text, isSecondary);

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
