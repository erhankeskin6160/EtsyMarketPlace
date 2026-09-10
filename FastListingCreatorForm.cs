namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class FastListingCreatorForm : Form
{
    private readonly IAiListingOptimizer _aiOptimizer;
    private readonly EtsyApiClient _apiClient = new();
    private readonly AiListingImageGenerator _imageGenerator = new();

    // Image gallery
    private readonly List<string> _galleryImagePaths = [];
    private string? _lastGeneratedImagePath;

    // Left Column Controls (Product & SEO)
    private readonly ComboBox _cboListingType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtTitle = new() { MaxLength = 140 };
    private readonly Label _lblTitleCounter = new() { AutoSize = true };
    private readonly NumericUpDown _numPrice = new() { Minimum = 0.20m, Maximum = 50000m, DecimalPlaces = 2, Value = 29.99m };
    private readonly NumericUpDown _numQuantity = new() { Minimum = 1, Maximum = 9999, Value = 10 };
    private readonly ComboBox _cboTaxonomy = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtCustomTaxonomy = new() { Text = "1239" };
    private readonly ComboBox _cboShippingProfile = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtTags = new() { Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };
    private readonly Label _lblTagCounter = new() { AutoSize = true };
    private readonly TextBox _txtDescription = new() { Multiline = true, Height = 130, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _txtMaterials = new() { Height = 26 };

    // Center Column Controls (Gallery & AI Generator)
    private readonly FlowLayoutPanel _galleryFlow = new();
    private readonly Label _lblGalleryCount = new() { AutoSize = true };
    private readonly TextBox _txtAiPrompt = new() { Multiline = true, Height = 58, ScrollBars = ScrollBars.Vertical };
    private readonly PictureBox _picAiPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(15, 23, 42), Height = 140 };
    private readonly ModernButtonControl _btnAddToGallery = new();

    // Right Column Controls (Variations & Publish)
    private readonly CheckBox _chkEnableVariations = new() { Text = "🎨 Bu ürüne varyasyon ekle (Boyut, Renk vb.)", AutoSize = true };
    private readonly Panel _panelVariations = new();
    private readonly ComboBox _cboVarType1 = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtVarValues1 = new() { Text = "Small, Medium, Large" };
    private readonly CheckBox _chkEnableVar2 = new() { Text = "➕ İkinci varyasyon grubu ekle", AutoSize = true };
    private readonly ComboBox _cboVarType2 = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtVarValues2 = new() { Text = "Siyah, Beyaz, Altın" };
    private readonly Label _lblVarCombinations = new() { AutoSize = true };

    private readonly CheckBox _chkMakeActive = new() { Text = "🚀 Hemen Canlı Yayına Al (Aktif Yap)", AutoSize = true, Checked = false };
    private readonly ModernButtonControl _btnPublish = new();
    private readonly Label _statusLabel = new() { AutoSize = true };

    public FastListingCreatorForm(IAiListingOptimizer aiOptimizer)
    {
        _aiOptimizer = aiOptimizer;
        Text = "🛍️ Hızlı Ürün Ekle & AI Stüdyo";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 720);
        UiStyle.ApplyTheme(this);

        BuildLayout();
        WireEvents();

        Shown += async (_, _) => await InitializeFormDataAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 3-Column main content
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // Status bar
        Controls.Add(root);

        // Row 0: Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titleBox = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        titleBox.Controls.Add(new Label
        {
            Text = "🛍️ Hızlı Ürün Ekleme & AI Stüdyosu",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = UiStyle.PrimaryColor,
            AutoSize = true,
            Margin = new Padding(0, 4, 12, 0)
        });
        titleBox.Controls.Add(new Label
        {
            Text = "Yeni bir listelemeyi doğrudan oluşturun, AI ile görsellerini üretin, varyasyonlarını belirleyin ve Etsy'ye gönderin.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        });
        header.Controls.Add(titleBox, 0, 0);

        var btnClearAll = UiStyle.CreateButton("Formu Temizle", isSecondary: true);
        btnClearAll.Height = 30;
        btnClearAll.Anchor = AnchorStyles.Right;
        btnClearAll.Click += (_, _) => ResetForm();
        header.Controls.Add(btnClearAll, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Row 1: 3-Column Content Layout
        var contentColumns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        contentColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); // Col 1: Details & SEO
        contentColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); // Col 2: Gallery & AI
        contentColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); // Col 3: Variations & Publish

        contentColumns.Controls.Add(BuildLeftColumn(), 0, 0);
        contentColumns.Controls.Add(BuildCenterColumn(), 1, 0);
        contentColumns.Controls.Add(BuildRightColumn(), 2, 0);
        root.Controls.Add(contentColumns, 0, 1);

        // Row 2: Status bar
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Font = new Font("Segoe UI", 9F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Text = "Hazır.";
        root.Controls.Add(_statusLabel, 0, 2);
    }

    private Control BuildLeftColumn()
    {
        var grp = new GroupBox
        {
            Text = "1. Ürün & SEO Bilgileri",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1 };

        // Listing Type & Price & Quantity
        var topRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, Height = 48 };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _cboListingType.Items.AddRange(["Fiziksel Ürün", "Dijital Ürün"]);
        _cboListingType.SelectedIndex = 0;
        _cboListingType.Dock = DockStyle.Fill;

        topRow.Controls.Add(CreateLabeledControl("Ürün Tipi:", _cboListingType), 0, 0);
        _numPrice.Dock = DockStyle.Fill;
        topRow.Controls.Add(CreateLabeledControl("Fiyat ($):", _numPrice), 1, 0);
        _numQuantity.Dock = DockStyle.Fill;
        topRow.Controls.Add(CreateLabeledControl("Stok:", _numQuantity), 2, 0);
        panel.Controls.Add(topRow);

        // Title with AI button & counter
        var titleContainer = new Panel { Dock = DockStyle.Top, Height = 74 };
        var titleHeader = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 24, FlowDirection = FlowDirection.LeftToRight };
        titleHeader.Controls.Add(new Label { Text = "Ürün Başlığı (Etsy): ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F) });
        _lblTitleCounter.Text = "0 / 140";
        _lblTitleCounter.Font = new Font("Segoe UI", 8F);
        _lblTitleCounter.ForeColor = UiStyle.TextMuted;
        titleHeader.Controls.Add(_lblTitleCounter);

        var btnAiTitle = new Button
        {
            Text = "✨ AI Başlık Öner",
            Font = new Font("Segoe UI", 8F),
            Height = 22,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(12, 0, 0, 0),
        };
        btnAiTitle.Click += async (_, _) => await SuggestAiTitleAsync();
        titleHeader.Controls.Add(btnAiTitle);

        _txtTitle.Dock = DockStyle.Bottom;
        _txtTitle.Font = new Font("Segoe UI", 9.5F);
        titleContainer.Controls.Add(titleHeader);
        titleContainer.Controls.Add(_txtTitle);
        panel.Controls.Add(titleContainer);

        // Category & Taxonomy
        var catRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 50, Margin = new Padding(0, 4, 0, 0) };
        catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _cboTaxonomy.Dock = DockStyle.Fill;
        _cboTaxonomy.Items.AddRange([
            "1239 - Art & Collectibles / 3D Printed",
            "1053 - Home & Living / Home Decor",
            "204 - Jewelry / Rings & Necklaces",
            "502 - Clothing / Unisex Adult",
            "100 - Accessories / Keychains & Bags",
            "68 - Craft Supplies & Tools",
            "Özel Kategori (Manuel ID)"
        ]);
        _cboTaxonomy.SelectedIndex = 0;
        _cboTaxonomy.SelectedIndexChanged += (_, _) =>
        {
            _txtCustomTaxonomy.Visible = _cboTaxonomy.SelectedIndex == _cboTaxonomy.Items.Count - 1;
            if (_cboTaxonomy.SelectedIndex != _cboTaxonomy.Items.Count - 1)
            {
                var selected = _cboTaxonomy.SelectedItem?.ToString() ?? "";
                var idStr = selected.Split('-')[0].Trim();
                _txtCustomTaxonomy.Text = idStr;
            }
        };

        catRow.Controls.Add(CreateLabeledControl("Kategori / Taxonomy:", _cboTaxonomy), 0, 0);
        _txtCustomTaxonomy.Dock = DockStyle.Fill;
        catRow.Controls.Add(CreateLabeledControl("Taxonomy ID:", _txtCustomTaxonomy), 1, 0);
        panel.Controls.Add(catRow);

        // Shipping Profile
        _cboShippingProfile.Dock = DockStyle.Fill;
        _cboShippingProfile.DisplayMember = nameof(EtsyShippingProfileOption.DisplayName);
        panel.Controls.Add(CreateLabeledControl("Kargo Profili (Shipping Profile):", _cboShippingProfile));

        // Tags
        var tagContainer = new Panel { Dock = DockStyle.Top, Height = 95, Margin = new Padding(0, 4, 0, 0) };
        var tagHeader = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 24, FlowDirection = FlowDirection.LeftToRight };
        tagHeader.Controls.Add(new Label { Text = "Etiketler (Virgülle ayırın, maks 13): ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F) });
        _lblTagCounter.Text = "0 / 13";
        _lblTagCounter.Font = new Font("Segoe UI", 8F);
        _lblTagCounter.ForeColor = UiStyle.TextMuted;
        tagHeader.Controls.Add(_lblTagCounter);

        var btnAiTags = new Button
        {
            Text = "✨ 13 AI Tag Doldur",
            Font = new Font("Segoe UI", 8F),
            Height = 22,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(12, 0, 0, 0),
        };
        btnAiTags.Click += async (_, _) => await SuggestAiTagsAsync();
        tagHeader.Controls.Add(btnAiTags);

        _txtTags.Dock = DockStyle.Bottom;
        _txtTags.Font = new Font("Segoe UI", 9F);
        tagContainer.Controls.Add(tagHeader);
        tagContainer.Controls.Add(_txtTags);
        panel.Controls.Add(tagContainer);

        // Description
        var descContainer = new Panel { Dock = DockStyle.Top, Height = 175, Margin = new Padding(0, 4, 0, 0) };
        var descHeader = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 24, FlowDirection = FlowDirection.LeftToRight };
        descHeader.Controls.Add(new Label { Text = "Ürün Açıklaması: ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F) });

        var btnAiDesc = new Button
        {
            Text = "✨ AI Açıklama Üret",
            Font = new Font("Segoe UI", 8F),
            Height = 22,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
        };
        btnAiDesc.Click += async (_, _) => await SuggestAiDescriptionAsync();
        descHeader.Controls.Add(btnAiDesc);

        _txtDescription.Dock = DockStyle.Bottom;
        _txtDescription.Font = new Font("Segoe UI", 9F);
        descContainer.Controls.Add(descHeader);
        descContainer.Controls.Add(_txtDescription);
        panel.Controls.Add(descContainer);

        // Materials
        _txtMaterials.Dock = DockStyle.Fill;
        panel.Controls.Add(CreateLabeledControl("Kullanılan Malzemeler (Virgülle ayırın):", _txtMaterials));

        grp.Controls.Add(panel);
        return grp;
    }

    private Control BuildCenterColumn()
    {
        var grp = new GroupBox
        {
            Text = "2. Görseller & AI Motoru",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };

        var rootTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // Add Image Buttons
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 55));  // Gallery List
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 45));  // AI Generator Box

        // 1. Add Image Toolbar
        var topBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));

        var btnBrowse = UiStyle.CreateButton("📂 Bilgisayardan Seç");
        btnBrowse.Height = 32;
        btnBrowse.Click += (_, _) => BrowseLocalImages();
        topBar.Controls.Add(btnBrowse, 0, 0);

        var btnFromStudio = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Height = 32,
            Text = "🖼️ Stüdyo Galerisinden",
            NormalColor = UiStyle.AiColor,
            HoverColor = UiStyle.AiHover,
            ForeColor = Color.White,
        };
        btnFromStudio.Click += (_, _) => OpenStudioGalleryPicker();
        topBar.Controls.Add(btnFromStudio, 1, 0);

        _lblGalleryCount.Text = "0/10";
        _lblGalleryCount.Font = new Font("Segoe UI Semibold", 9.5F);
        _lblGalleryCount.TextAlign = ContentAlignment.MiddleCenter;
        _lblGalleryCount.Dock = DockStyle.Fill;
        topBar.Controls.Add(_lblGalleryCount, 2, 0);
        rootTable.Controls.Add(topBar, 0, 0);

        // 2. Gallery Flow Panel
        _galleryFlow.Dock = DockStyle.Fill;
        _galleryFlow.AutoScroll = true;
        _galleryFlow.BackColor = UiStyle.CardBackground;
        _galleryFlow.BorderStyle = BorderStyle.FixedSingle;
        _galleryFlow.Padding = new Padding(4);
        rootTable.Controls.Add(_galleryFlow, 0, 1);

        // 3. AI Generator Box
        var aiBox = new GroupBox
        {
            Text = "✨ Doğrudan AI ile Yeni Görsel Üret & Ekle",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.PrimaryColor,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };
        var aiTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        aiTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Prompt
        aiTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Preview
        aiTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Buttons

        var promptRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        promptRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        promptRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _txtAiPrompt.Dock = DockStyle.Fill;
        promptRow.Controls.Add(_txtAiPrompt, 0, 0);

        var btnPromptFromTitle = new Button
        {
            Text = "Başlıktan\nPrompt",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 7.5F),
            BackColor = UiStyle.SecondaryColor,
            ForeColor = UiStyle.TextDark,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        btnPromptFromTitle.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                _txtAiPrompt.Text = $"Professional commercial product photography of {_txtTitle.Text.Trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase";
            }
        };
        promptRow.Controls.Add(btnPromptFromTitle, 1, 0);
        aiTable.Controls.Add(promptRow, 0, 0);

        _picAiPreview.Dock = DockStyle.Fill;
        aiTable.Controls.Add(_picAiPreview, 0, 1);

        var aiActionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var btnGenerate = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "🎨 Görseli Üret",
            NormalColor = UiStyle.AiColor,
            HoverColor = UiStyle.AiHover,
            ForeColor = Color.White,
            Margin = new Padding(2),
        };
        btnGenerate.Click += async (_, _) => await GenerateAiImageAsync();
        aiActionRow.Controls.Add(btnGenerate, 0, 0);

        _btnAddToGallery.Dock = DockStyle.Fill;
        _btnAddToGallery.Text = "➕ Ürün Galerisine Ekle";
        _btnAddToGallery.NormalColor = UiStyle.SuccessColor;
        _btnAddToGallery.HoverColor = Color.FromArgb(5, 150, 105);
        _btnAddToGallery.ForeColor = Color.White;
        _btnAddToGallery.Margin = new Padding(2);
        _btnAddToGallery.Enabled = false;
        _btnAddToGallery.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_lastGeneratedImagePath) && File.Exists(_lastGeneratedImagePath))
            {
                AddImageToGallery(_lastGeneratedImagePath);
                _btnAddToGallery.Enabled = false;
            }
        };
        aiActionRow.Controls.Add(_btnAddToGallery, 1, 0);
        aiTable.Controls.Add(aiActionRow, 0, 2);

        aiBox.Controls.Add(aiTable);
        rootTable.Controls.Add(aiBox, 0, 2);

        grp.Controls.Add(rootTable);
        return grp;
    }

    private Control BuildRightColumn()
    {
        var grp = new GroupBox
        {
            Text = "3. Varyasyonlar & Etsy Dağıtımı",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };

        var rootTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 65)); // Variations
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 35)); // Publish

        // 1. Variations Box
        var varBox = new GroupBox
        {
            Text = "Ürün Varyasyonları (Seçenekler)",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };
        var varTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, AutoScroll = true };
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Checkbox enable
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // Label Var 1
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Cbo Var 1
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Values Var 1
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Checkbox enable 2
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Cbo Var 2
        varTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Combinations Info

        varTable.Controls.Add(_chkEnableVariations, 0, 0);

        varTable.Controls.Add(new Label { Text = "1. Varyasyon Tipi:", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F) }, 0, 1);
        _cboVarType1.Items.AddRange([
            "📏 Boyut / Size (100)",
            "🎨 Renk / Primary Color (506)",
            "🪵 Malzeme / Material (507)",
            "✨ Stil / Style (514)",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType1.SelectedIndex = 0;
        _cboVarType1.Dock = DockStyle.Fill;
        varTable.Controls.Add(_cboVarType1, 0, 2);

        _txtVarValues1.Dock = DockStyle.Fill;
        varTable.Controls.Add(_txtVarValues1, 0, 3);

        varTable.Controls.Add(_chkEnableVar2, 0, 4);

        _cboVarType2.Items.AddRange([
            "🎨 Renk / Color (506)",
            "📏 Boyut / Size (100)",
            "🪵 Malzeme / Material (507)",
            "✨ Stil / Style (514)",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType2.SelectedIndex = 0;
        _cboVarType2.Dock = DockStyle.Fill;
        varTable.Controls.Add(_cboVarType2, 0, 5);

        _txtVarValues2.Dock = DockStyle.Fill;
        _txtVarValues2.Visible = false;
        _cboVarType2.Visible = false;

        _lblVarCombinations.Dock = DockStyle.Fill;
        _lblVarCombinations.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        _lblVarCombinations.ForeColor = UiStyle.TextMuted;
        _lblVarCombinations.Text = "Varyasyon kapalı.";
        varTable.Controls.Add(_lblVarCombinations, 0, 6);

        varBox.Controls.Add(varTable);
        rootTable.Controls.Add(varBox, 0, 0);

        // 2. Publish Box
        var pubBox = new GroupBox
        {
            Text = "Etsy'ye Gönder",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };
        var pubTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        pubTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Active Checkbox
        pubTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Publish button
        pubTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Help notes

        pubTable.Controls.Add(_chkMakeActive, 0, 0);

        _btnPublish.Dock = DockStyle.Fill;
        _btnPublish.Text = "🚀 Etsy'ye Gönder (Listelemeyi Oluştur)";
        _btnPublish.NormalColor = UiStyle.SuccessColor;
        _btnPublish.HoverColor = Color.FromArgb(5, 150, 105);
        _btnPublish.ForeColor = Color.White;
        _btnPublish.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _btnPublish.Click += async (_, _) => await PublishListingToEtsyAsync();
        pubTable.Controls.Add(_btnPublish, 0, 1);

        var lblNote = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            Text = "İpucu: 'Canlı Yayına Al' işaretlenmezse listeleme güvenli şekilde TASLAK (Draft) olarak açılır. Eklediğiniz tüm görseller ve varyasyonlar otomatik olarak ürüne iliştirilir.",
        };
        pubTable.Controls.Add(lblNote, 0, 2);

        pubBox.Controls.Add(pubTable);
        rootTable.Controls.Add(pubBox, 0, 1);

        grp.Controls.Add(rootTable);
        return grp;
    }

    private static Control CreateLabeledControl(string labelText, Control control)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, RowCount = 2, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.Controls.Add(new Label { Text = labelText, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.TextDark, Dock = DockStyle.Fill }, 0, 0);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private void WireEvents()
    {
        _txtTitle.TextChanged += (_, _) =>
        {
            var len = _txtTitle.Text.Length;
            _lblTitleCounter.Text = $"{len} / 140";
            _lblTitleCounter.ForeColor = len >= 80 && len <= 140 ? UiStyle.SuccessColor : len > 140 ? UiStyle.DangerColor : UiStyle.TextMuted;
        };

        _txtTags.TextChanged += (_, _) =>
        {
            var tags = SplitTags(_txtTags.Text);
            _lblTagCounter.Text = $"{tags.Count} / 13";
            _lblTagCounter.ForeColor = tags.Count == 13 ? UiStyle.SuccessColor : tags.Count > 13 ? UiStyle.DangerColor : UiStyle.TextMuted;
        };

        _chkEnableVariations.CheckedChanged += (_, _) => UpdateVariationsDisplay();
        _chkEnableVar2.CheckedChanged += (_, _) =>
        {
            _cboVarType2.Visible = _chkEnableVar2.Checked;
            _txtVarValues2.Visible = _chkEnableVar2.Checked;
            UpdateVariationsDisplay();
        };

        _txtVarValues1.TextChanged += (_, _) => UpdateVariationsDisplay();
        _txtVarValues2.TextChanged += (_, _) => UpdateVariationsDisplay();
    }

    private void UpdateVariationsDisplay()
    {
        if (!_chkEnableVariations.Checked)
        {
            _lblVarCombinations.Text = "Varyasyon kapalı.";
            _lblVarCombinations.ForeColor = UiStyle.TextMuted;
            return;
        }

        var v1Count = SplitTags(_txtVarValues1.Text).Count;
        if (!_chkEnableVar2.Checked)
        {
            _lblVarCombinations.Text = $"📊 {v1Count} adet seçenek tanımlandı.";
            _lblVarCombinations.ForeColor = v1Count > 0 ? UiStyle.PrimaryColor : UiStyle.DangerColor;
        }
        else
        {
            var v2Count = SplitTags(_txtVarValues2.Text).Count;
            var total = v1Count * v2Count;
            _lblVarCombinations.Text = $"📊 {v1Count} x {v2Count} = {total} varyasyon kombinasyonu oluşacak.";
            _lblVarCombinations.ForeColor = total > 0 ? UiStyle.PrimaryColor : UiStyle.DangerColor;
        }
    }

    private async Task InitializeFormDataAsync()
    {
        try
        {
            _statusLabel.Text = "Etsy mağaza kargo profilleri alınıyor...";
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials)
            {
                _statusLabel.Text = "Etsy API ayarları tanımlı değil. (Ayarlar menüsünden yapabilirsiniz)";
                return;
            }

            var profiles = await _apiClient.GetOwnShopShippingProfilesAsync(settings);
            EtsyApiSettingsStore.Save(settings);

            _cboShippingProfile.DataSource = profiles;
            if (profiles.Count > 0)
            {
                _cboShippingProfile.SelectedIndex = 0;
                _statusLabel.Text = $"{profiles.Count} kargo profili yüklendi.";
            }
            else
            {
                _statusLabel.Text = "Kargo profili bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Kargo profilleri alınamadı: {ex.Message}";
        }
    }

    // --- Image Gallery Operations ---

    private void BrowseLocalImages()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Ürün Görselleri Seç",
            Multiselect = true,
            Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tüm Dosyalar (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var file in dlg.FileNames)
            {
                AddImageToGallery(file);
            }
        }
    }

    private void OpenStudioGalleryPicker()
    {
        using var dlg = new AiStudioImagePickerDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var path in dlg.SelectedImagePaths)
            {
                AddImageToGallery(path);
            }
        }
    }

    private void AddImageToGallery(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        if (_galleryImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (_galleryImagePaths.Count >= 10)
        {
            MessageBox.Show(this, "Etsy en fazla 10 ürün görseline izin verir.", "Görsel Sınırı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _galleryImagePaths.Add(path);
        RefreshGalleryCards();
    }

    private void RefreshGalleryCards()
    {
        _galleryFlow.Controls.Clear();
        _lblGalleryCount.Text = $"{_galleryImagePaths.Count}/10";

        for (int i = 0; i < _galleryImagePaths.Count; i++)
        {
            var index = i;
            var path = _galleryImagePaths[i];

            var card = new Panel
            {
                Width = 95,
                Height = 135,
                Margin = new Padding(4),
                BackColor = UiStyle.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
            };

            var pic = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 80,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
            };

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var bmp = Image.FromStream(fs);
                pic.Image = new Bitmap(bmp);
            }
            catch { }
            card.Controls.Add(pic);

            var lblBadge = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 7.5F),
                Text = index == 0 ? "⭐ #1 Kapak" : $"#{index + 1}",
                ForeColor = index == 0 ? Color.White : UiStyle.TextDark,
                BackColor = index == 0 ? UiStyle.PrimaryColor : Color.Transparent,
            };
            card.Controls.Add(lblBadge);

            var btnRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, ColumnCount = 2 };
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var btnStar = new Button
            {
                Text = "⭐",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Enabled = index > 0,
            };
            btnStar.Click += (_, _) =>
            {
                var temp = _galleryImagePaths[index];
                _galleryImagePaths.RemoveAt(index);
                _galleryImagePaths.Insert(0, temp);
                RefreshGalleryCards();
            };
            btnRow.Controls.Add(btnStar, 0, 0);

            var btnDel = new Button
            {
                Text = "🗑️",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                ForeColor = UiStyle.DangerColor,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
            };
            btnDel.Click += (_, _) =>
            {
                _galleryImagePaths.RemoveAt(index);
                RefreshGalleryCards();
            };
            btnRow.Controls.Add(btnDel, 1, 0);

            card.Controls.Add(btnRow);
            _galleryFlow.Controls.Add(card);
        }
    }

    // --- AI Suggestions & Generation ---

    private async Task SuggestAiTitleAsync()
    {
        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show(this, "Lütfen AI için taslak bir başlık veya anahtar kelime giriniz.", "AI Öneri", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI başlık optimize ediyor...";
            var input = new ListingOptimizationInput(_txtTitle.Text.Trim(), _txtDescription.Text.Trim(), SplitTags(_txtTags.Text), "");
            var res = await _aiOptimizer.OptimizeAsync(input);

            if (res.TitleSuggestions.Count > 0)
            {
                _txtTitle.Text = res.TitleSuggestions[0];
                _statusLabel.Text = "Başlık AI tarafından optimize edildi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI başlık önerisi hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SuggestAiTagsAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI 13 adet SEO tagi üretiyor...";
            var input = new ListingOptimizationInput(_txtTitle.Text.Trim(), _txtDescription.Text.Trim(), SplitTags(_txtTags.Text), "");
            var res = await _aiOptimizer.OptimizeAsync(input);

            if (res.TagSuggestions.Count > 0)
            {
                _txtTags.Text = string.Join(", ", res.TagSuggestions.Take(13));
                _statusLabel.Text = $"{res.TagSuggestions.Count} adet SEO tagi oluşturuldu.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI tag önerisi hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SuggestAiDescriptionAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI ürün açıklaması yazıyor...";
            var input = new ListingOptimizationInput(_txtTitle.Text.Trim(), _txtDescription.Text.Trim(), SplitTags(_txtTags.Text), "");
            var res = await _aiOptimizer.OptimizeAsync(input);

            if (!string.IsNullOrWhiteSpace(res.DescriptionDraft))
            {
                _txtDescription.Text = res.DescriptionDraft;
                _statusLabel.Text = "Açıklama AI tarafından oluşturuldu.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI açıklama hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task GenerateAiImageAsync()
    {
        var prompt = _txtAiPrompt.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            if (!string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                prompt = $"Professional commercial product photography of {_txtTitle.Text.Trim()}, studio lighting, 8k resolution";
                _txtAiPrompt.Text = prompt;
            }
            else
            {
                MessageBox.Show(this, "Lütfen üretilecek görsel için bir prompt giriniz.", "Görsel Üretimi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI ürün görseli oluşturuluyor, lütfen bekleyin...";

            var settings = AiOptimizationSettingsStore.Load();
            var fakeListing = new MarketListingResult
            {
                ListingId = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Title = _txtTitle.Text.Trim(),
            };

            var imagePath = await _imageGenerator.GenerateAsync(settings, fakeListing, prompt);
            _lastGeneratedImagePath = imagePath;

            if (File.Exists(imagePath))
            {
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                using var bmp = Image.FromStream(fs);
                _picAiPreview.Image = new Bitmap(bmp);
                _btnAddToGallery.Enabled = true;
                _statusLabel.Text = "AI görseli başarıyla üretildi! 'Ürün Galerisine Ekle' butonuna basarak aktarabilirsiniz.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI Görsel üretimi hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Görsel üretilemedi.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    // --- Etsy Publishing ---

    private async Task PublishListingToEtsyAsync()
    {
        // 1. Validation
        var title = _txtTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 140)
        {
            MessageBox.Show(this, "Başlık boş olamaz ve 140 karakteri geçemez.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var description = _txtDescription.Text.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            MessageBox.Show(this, "Lütfen ürün açıklamasını doldurun.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var tags = SplitTags(_txtTags.Text).Take(13).ToList();
        if (tags.Count == 0)
        {
            MessageBox.Show(this, "En az bir etiket (tag) girmelisiniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!long.TryParse(_txtCustomTaxonomy.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var taxonomyId) || taxonomyId <= 0)
        {
            MessageBox.Show(this, "Geçerli bir Taxonomy ID giriniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool isDigital = _cboListingType.SelectedIndex == 1;
        long shippingProfileId = 0;
        if (!isDigital)
        {
            if (_cboShippingProfile.SelectedItem is EtsyShippingProfileOption prof)
            {
                shippingProfileId = prof.ShippingProfileId;
            }
            else
            {
                MessageBox.Show(this, "Fiziksel ürünler için geçerli bir Kargo Profili (Shipping Profile) seçmelisiniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 2. Build Variations
        DraftListingInventoryUpdate? inventory = null;
        if (_chkEnableVariations.Checked)
        {
            var variationGroups = BuildVariationGroups();
            if (variationGroups.Count > 0)
            {
                inventory = new DraftListingInventoryUpdate(_numPrice.Value, (int)_numQuantity.Value, null, variationGroups);
            }
        }

        // 3. Confirmation Dialog
        var confirmMsg =
            $"Etsy'de yeni listeleme oluşturulacak:\n\n" +
            $"• Başlık: {title}\n" +
            $"• Fiyat: {_numPrice.Value:0.00} USD | Stok: {_numQuantity.Value}\n" +
            $"• Kategori ID: {taxonomyId}\n" +
            $"• Görseller: {_galleryImagePaths.Count} adet\n" +
            $"• Varyasyonlar: {(inventory?.Variations.Count > 0 ? $"{inventory.Variations.Count} grup" : "Yok")}\n" +
            $"• Yayın Durumu: {(_chkMakeActive.Checked ? "🚀 Canlı (Active)" : "💾 Taslak (Draft)")}\n\n" +
            "Devam etmek istiyor musunuz?";

        if (MessageBox.Show(this, confirmMsg, "Etsy Listeleme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            _btnPublish.Enabled = false;
            _statusLabel.Text = "Etsy'de listeleme oluşturuluyor...";

            var settings = EtsyApiSettingsStore.Load();
            var draftReq = new DraftListingCreateRequest(
                title,
                description,
                _numPrice.Value,
                (int)_numQuantity.Value,
                taxonomyId,
                shippingProfileId,
                isDigital,
                tags,
                SplitTags(_txtMaterials.Text),
                0,
                "i_did",
                "made_to_order");

            // A. Create Listing
            var created = await _apiClient.CreateOwnShopDraftListingAsync(settings, draftReq);
            EtsyApiSettingsStore.Save(settings);

            // B. Add Variations if defined
            if (inventory != null)
            {
                try
                {
                    _statusLabel.Text = "Varyasyonlar ekleniyor...";
                    await _apiClient.UpdateOwnShopListingInventoryAsync(settings, created.ListingId, inventory);
                    EtsyApiSettingsStore.Save(settings);
                }
                catch (Exception varEx)
                {
                    MessageBox.Show(this, $"Listeleme oluşturuldu fakat varyasyon eklenirken hata alındı:\n{varEx.Message}", "Varyasyon Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // C. Upload Gallery Images
            if (_galleryImagePaths.Count > 0)
            {
                for (int i = 0; i < _galleryImagePaths.Count; i++)
                {
                    var imgPath = _galleryImagePaths[i];
                    _statusLabel.Text = $"Görsel yükleniyor ({i + 1}/{_galleryImagePaths.Count})...";
                    try
                    {
                        await _apiClient.UploadOwnShopListingImageAsync(settings, created.ListingId, imgPath, rank: i + 1);
                        EtsyApiSettingsStore.Save(settings);
                    }
                    catch (Exception imgEx)
                    {
                        // Continue uploading other images even if one fails
                        Debug.WriteLine($"Image upload error: {imgEx.Message}");
                    }
                }
            }

            _statusLabel.Text = $"Listeleme başarıyla oluşturuldu! (#{created.ListingId})";

            var openPrompt = MessageBox.Show(
                this,
                $"🎉 Tebrikler! Ürününüz Etsy'de başarıyla oluşturuldu!\n\n" +
                $"Listing ID: #{created.ListingId}\n" +
                $"Görsel Sayısı: {_galleryImagePaths.Count}\n\n" +
                $"Etsy sayfasını tarayıcıda açmak ister misiniz?",
                "Listeleme Başarılı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (openPrompt == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(created.Url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            var err = ex.InnerException != null ? $"{ex.Message}\n({ex.InnerException.Message})" : ex.Message;
            MessageBox.Show(this, $"Etsy listelemesi oluşturulurken hata oluştu:\n{err}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Listeleme oluşturulamadı.";
        }
        finally
        {
            _btnPublish.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private List<DraftListingVariationGroup> BuildVariationGroups()
    {
        var groups = new List<DraftListingVariationGroup>();

        // Group 1
        var v1Values = SplitTags(_txtVarValues1.Text);
        if (v1Values.Count > 0)
        {
            var (name1, propId1) = FastListingDraftHelper.ParseVariationType(_cboVarType1.SelectedItem?.ToString());
            groups.Add(new DraftListingVariationGroup(name1, propId1, v1Values));
        }

        // Group 2
        if (_chkEnableVar2.Checked)
        {
            var v2Values = SplitTags(_txtVarValues2.Text);
            if (v2Values.Count > 0)
            {
                var (name2, propId2) = FastListingDraftHelper.ParseVariationType(_cboVarType2.SelectedItem?.ToString());
                groups.Add(new DraftListingVariationGroup(name2, propId2, v2Values));
            }
        }

        return groups;
    }

    private static (string Name, long PropertyId) ParseVariationType(string? selected) =>
        FastListingDraftHelper.ParseVariationType(selected);

    private static List<string> SplitTags(string text) =>
        FastListingDraftHelper.SanitizeTags(text);

    private void ResetForm()
    {
        _txtTitle.Clear();
        _txtDescription.Clear();
        _txtTags.Clear();
        _txtMaterials.Clear();
        _galleryImagePaths.Clear();
        _lastGeneratedImagePath = null;
        _picAiPreview.Image = null;
        _btnAddToGallery.Enabled = false;
        RefreshGalleryCards();
        _statusLabel.Text = "Form temizlendi.";
    }
}
