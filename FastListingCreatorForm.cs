namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly IAiCategorySuggester _categorySuggester;
    private readonly EtsyApiClient _apiClient = new();
    private readonly AiListingImageGenerator _imageGenerator = new();

    // Image gallery
    private readonly List<string> _galleryImagePaths = [];
    private string? _lastGeneratedImagePath;
    private readonly ToolTip _galleryToolTip = new() { AutoPopDelay = 4000, InitialDelay = 250 };

    // Left Column Controls (Product & SEO)
    private readonly ModernComboBox _cboListingType = new();
    private readonly ModernMultilineTextBox _txtTitle = new() { Height = 58, MaxLength = 140 };
    private readonly Label _lblTitleCounter = new() { AutoSize = true };
    private readonly ModernNumericUpDown _numPrice = new() { Minimum = 0.20m, Maximum = 50000m, DecimalPlaces = 2, Value = 29.99m };
    private readonly ModernNumericUpDown _numQuantity = new() { Minimum = 1, Maximum = 9999, Value = 10 };
    private readonly ModernComboBox _cboTaxonomy = new();
    private readonly ModernTextBox _txtCustomTaxonomy = new() { Text = "1239" };
    private readonly ModernComboBox _cboShippingProfile = new();
    private readonly ModernComboBox _cboReadinessState = new();
    private readonly ModernMultilineTextBox _txtTags = new() { Height = 68 };
    private readonly Label _lblTagCounter = new() { AutoSize = true };
    private readonly Label _lblTagStatus = new() { AutoSize = true };
    private readonly ModernMultilineTextBox _txtDescription = new() { Height = 175 };
    private readonly ModernTextBox _txtMaterials = new() { Height = 30 };

    // Template Toolbar Controls
    private readonly ModernComboBox _cboTemplates = new() { Width = 260 };
    private readonly Button _btnApplyTemplate = new();
    private readonly Button _btnSaveTemplate = new();
    private readonly Button _btnDeleteTemplate = new();

    // Primary Action Buttons
    private readonly ModernButtonControl _btnHeaderPreview = new();
    private readonly ModernButtonControl _btnHeaderPublish = new();
    private readonly Button _btnClearAll = new();

    // Center Column Controls (Gallery & AI Generator)
    private readonly FlowLayoutPanel _galleryFlow = new();
    private ModernScrollPanel? _galleryScroll;
    private ModernScrollPanel? _rightScroll;
    private readonly Label _lblGalleryCount = new() { AutoSize = true };
    private readonly ModernMultilineTextBox _txtAiPrompt = new() { Height = 56 };
    private readonly PictureBox _picAiPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Height = 180 };
    private readonly ModernButtonControl _btnGenerateAi = new();
    private readonly ModernButtonControl _btnAddToGallery = new();

    // Right Column Controls (Variations & Publish)
    private readonly ModernCheckBox _chkEnableVariations = new() { Text = "🎨 Bu ürüne varyasyon ekle (Boyut, Renk vb.)", AutoSize = true };
    private readonly ModernComboBox _cboVarType1 = new();
    private readonly ModernTextBox _txtVarValues1 = new() { Height = 30, Text = "Small, Medium, Large" };
    private readonly ModernCheckBox _chkEnableVar2 = new() { Text = "➕ İkinci varyasyon grubu ekle", AutoSize = true };
    private readonly ModernComboBox _cboVarType2 = new();
    private readonly ModernTextBox _txtVarValues2 = new() { Height = 30, Text = "Siyah, Beyaz, Altın" };
    private readonly Label _lblVarCombinations = new() { AutoSize = true };

    // Custom Variation Pricing Controls
    private readonly ModernCheckBox _chkCustomVariationPricing = new() { Text = "💲 Her varyasyona özel farklı fiyat & stok belirle", AutoSize = true };
    private readonly TableLayoutPanel _pnlVariationPricing = new() { Dock = DockStyle.Top, Height = 180, Visible = false };
    private readonly DataGridView _gridVariationPricing = new();
    private readonly Label _lblPriceRangeBadge = new() { AutoSize = true };
    private readonly Button _btnSyncBasePrice = new();
    private readonly Button _btnStepPrice = new();
    private readonly Dictionary<string, (decimal Price, int Quantity, bool IsEnabled)> _customVariationPrices = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingVariationGrid;

    private readonly ModernPublishToggleCard _chkMakeActive = new();
    private readonly ModernButtonControl _btnPublish = new();
    private readonly Button _btnPreviewSecondary = new();
    private readonly Label _statusLabel = new() { AutoSize = true };

    // Live Readiness Checklist Labels
    private readonly Label _chkItemTitle = new() { AutoSize = true };
    private readonly Label _chkItemPrice = new() { AutoSize = true };
    private readonly Label _chkItemImage = new() { AutoSize = true };
    private readonly Label _chkItemShipping = new() { AutoSize = true };
    private readonly Label _chkItemReadiness = new() { AutoSize = true };
    private readonly Label _chkItemDesc = new() { AutoSize = true };
    private readonly Label _chkItemTags = new() { AutoSize = true };

    public FastListingCreatorForm(IAiListingOptimizer aiOptimizer, IAiCategorySuggester? categorySuggester = null)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        DoubleBuffered = true;

        _aiOptimizer = aiOptimizer;
        _categorySuggester = categorySuggester ?? new AiCategorySuggester();
        Text = "🛍️ Hızlı Ürün Ekle & AI Stüdyosu";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 740);

        SuspendLayout();
        BuildLayout();
        LoadTemplatesCombo();
        WireEvents();
        ResumeLayout(false);
        PerformLayout();

        Shown += async (_, _) => await InitializeFormDataAsync();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = UiStyle.BackgroundColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));  // Row 0: Unified Top Command & Template Bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Row 1: 3 Responsive Workspace Columns
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // Row 2: Status bar
        Controls.Add(root);

        // Row 0: Unified Top Command & Template Bar
        root.Controls.Add(BuildTopHeader(), 0, 0);

        // Row 1: 3 Responsive Content Columns
        var contentGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 4, 0, 4)
        };
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37)); // Col 1: SEO & Details
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); // Col 2: Gallery & AI
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); // Col 3: Variations & Checklist

        contentGrid.Controls.Add(BuildLeftColumn(), 0, 0);
        contentGrid.Controls.Add(BuildCenterColumn(), 1, 0);
        contentGrid.Controls.Add(BuildRightColumn(), 2, 0);
        root.Controls.Add(contentGrid, 0, 1);

        // Row 2: Modern Status bar
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Text = "Hazır.";
        root.Controls.Add(_statusLabel, 0, 2);

        ResumeLayout(false);
    }

    private Control BuildTopHeader()
    {
        var headerCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 0, 4)
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Left: Branding & Subtitle
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Center: Template Strip
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Right: Action Buttons

        // Left: Branding & Subtitle (Structured TableLayoutPanel to prevent vertical clipping)
        var titleContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 16, 0)
        };
        titleContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleContainer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblBadge = new Label
        {
            Text = "⚡ AI STUDIO",
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = UiStyle.AiColor,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 4, 12, 4),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter
        };
        titleContainer.Controls.Add(lblBadge, 0, 0);

        var titleStack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(0, 2, 0, 2),
            Padding = Padding.Empty
        };
        titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            Text = "Hızlı Ürün Ekle",
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 1)
        };
        var lblSubtitle = new Label
        {
            Text = "Etsy Listeleme & AI Görsel Stüdyosu",
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Margin = Padding.Empty
        };
        titleStack.Controls.Add(lblTitle, 0, 0);
        titleStack.Controls.Add(lblSubtitle, 0, 1);
        titleContainer.Controls.Add(titleStack, 1, 0);
        header.Controls.Add(titleContainer, 0, 0);

        // Center: Template Strip inside sleek capsule
        var templateCard = new ModernCardPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            CornerRadius = 8,
            CardColor = Color.FromArgb(28, 30, 42),
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(8, 2, 8, 2)
        };

        var templateFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };

        templateFlow.Controls.Add(new Label
        {
            Text = "📋 Şablon:",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0)
        });

        _cboTemplates.Font = new Font("Segoe UI", 8.5F);
        _cboTemplates.Width = 150;
        _cboTemplates.DropDownWidth = 220;
        _cboTemplates.Margin = new Padding(0, 2, 6, 0);
        templateFlow.Controls.Add(_cboTemplates);

        _btnApplyTemplate.Text = "⚡ Uygula";
        _btnApplyTemplate.Font = new Font("Segoe UI Semibold", 8F);
        _btnApplyTemplate.Height = 28;
        _btnApplyTemplate.AutoSize = true;
        _btnApplyTemplate.BackColor = UiStyle.PrimaryColor;
        _btnApplyTemplate.ForeColor = Color.White;
        _btnApplyTemplate.FlatStyle = FlatStyle.Flat;
        _btnApplyTemplate.FlatAppearance.BorderSize = 0;
        _btnApplyTemplate.Cursor = Cursors.Hand;
        _btnApplyTemplate.Margin = new Padding(0, 1, 4, 0);
        _btnApplyTemplate.Click += (_, _) => ApplySelectedTemplate();
        templateFlow.Controls.Add(_btnApplyTemplate);

        _btnSaveTemplate.Text = "💾 Kaydet";
        _btnSaveTemplate.Font = new Font("Segoe UI", 8F);
        _btnSaveTemplate.Height = 28;
        _btnSaveTemplate.AutoSize = true;
        _btnSaveTemplate.BackColor = UiStyle.SecondaryColor;
        _btnSaveTemplate.ForeColor = UiStyle.TextDark;
        _btnSaveTemplate.FlatStyle = FlatStyle.Flat;
        _btnSaveTemplate.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnSaveTemplate.Cursor = Cursors.Hand;
        _btnSaveTemplate.Margin = new Padding(0, 1, 4, 0);
        _btnSaveTemplate.Click += (_, _) => SaveCurrentAsTemplate();
        templateFlow.Controls.Add(_btnSaveTemplate);

        _btnDeleteTemplate.Text = "🗑️";
        _btnDeleteTemplate.Font = new Font("Segoe UI", 8F);
        _btnDeleteTemplate.Height = 28;
        _btnDeleteTemplate.Width = 28;
        _btnDeleteTemplate.BackColor = UiStyle.SecondaryColor;
        _btnDeleteTemplate.ForeColor = UiStyle.DangerColor;
        _btnDeleteTemplate.FlatStyle = FlatStyle.Flat;
        _btnDeleteTemplate.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnDeleteTemplate.Cursor = Cursors.Hand;
        _btnDeleteTemplate.Margin = new Padding(0, 1, 0, 0);
        _btnDeleteTemplate.Click += (_, _) => DeleteSelectedTemplate();
        templateFlow.Controls.Add(_btnDeleteTemplate);

        templateCard.Controls.Add(templateFlow);
        header.Controls.Add(templateCard, 1, 0);

        // Right: Primary Actions Cluster
        var actionCluster = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };

        // 1. Etsy'ye Gönder
        _btnHeaderPublish.Text = "🚀 Etsy'ye Gönder";
        _btnHeaderPublish.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        _btnHeaderPublish.Height = 32;
        _btnHeaderPublish.Width = 140;
        _btnHeaderPublish.NormalColor = UiStyle.SuccessColor;
        _btnHeaderPublish.HoverColor = Color.FromArgb(5, 150, 105);
        _btnHeaderPublish.ForeColor = Color.White;
        _btnHeaderPublish.Cursor = Cursors.Hand;
        _btnHeaderPublish.Margin = new Padding(6, 1, 0, 0);
        _btnHeaderPublish.Click += async (_, _) => await PublishListingToEtsyAsync();
        actionCluster.Controls.Add(_btnHeaderPublish);

        // 2. Canlı Önizleme
        _btnHeaderPreview.Text = "👁️ Önizleme";
        _btnHeaderPreview.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnHeaderPreview.Height = 32;
        _btnHeaderPreview.Width = 105;
        _btnHeaderPreview.NormalColor = UiStyle.PrimaryColor;
        _btnHeaderPreview.HoverColor = UiStyle.PrimaryHover;
        _btnHeaderPreview.ForeColor = Color.White;
        _btnHeaderPreview.Cursor = Cursors.Hand;
        _btnHeaderPreview.Margin = new Padding(4, 1, 0, 0);
        _btnHeaderPreview.Click += (_, _) => OpenEtsyListingPreview();
        actionCluster.Controls.Add(_btnHeaderPreview);

        // 3. Formu Temizle
        _btnClearAll.Text = "🔄 Temizle";
        _btnClearAll.Font = new Font("Segoe UI", 8.2F);
        _btnClearAll.Height = 32;
        _btnClearAll.AutoSize = true;
        _btnClearAll.BackColor = UiStyle.SecondaryColor;
        _btnClearAll.ForeColor = UiStyle.TextDark;
        _btnClearAll.FlatStyle = FlatStyle.Flat;
        _btnClearAll.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnClearAll.Cursor = Cursors.Hand;
        _btnClearAll.Margin = new Padding(4, 1, 0, 0);
        _btnClearAll.Click += (_, _) => ResetForm();
        actionCluster.Controls.Add(_btnClearAll);

        header.Controls.Add(actionCluster, 2, 0);
        headerCard.Controls.Add(header);
        return headerCard;
    }

    private Control BuildLeftColumn()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 4, 0)
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Header Bar
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));  // Divider Space
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Scrollable Form Content

        // 1. Header Bar with Title and Badge
        var headerBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            Text = "📝 1. Ürün & SEO Bilgileri",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        headerBar.Controls.Add(lblTitle, 0, 0);

        var lblAiBadge = new Label
        {
            Text = "✨ AI Destekli",
            Font = new Font("Segoe UI Semibold", 7.5F),
            ForeColor = Color.FromArgb(196, 181, 253),
            BackColor = Color.FromArgb(46, 32, 70),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 3, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        headerBar.Controls.Add(lblAiBadge, 1, 0);
        cardLayout.Controls.Add(headerBar, 0, 0);

        // Subtle Divider Line
        var divider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = UiStyle.BorderColor,
            Margin = new Padding(0, 1, 0, 2)
        };
        cardLayout.Controls.Add(divider, 0, 1);

        // 2. Scrollable Body
        var scrollContainer = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 2, 0)
        };

        var stack = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 8, 0)
        };
        stack.ColumnStyles.Clear();
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // --- SECTION 1: Temel Satış Bilgileri (Ürün Tipi, Fiyat, Stok) ---
        stack.Controls.Add(CreateSectionHeaderLabel("🏷️ Temel Satış Bilgileri"));

        var topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Height = 52,
            Margin = new Padding(0, 0, 0, 6)
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));

        _cboListingType.Items.Clear();
        _cboListingType.Items.AddRange(["📦 Fiziksel", "💻 Dijital"]);
        _cboListingType.SelectedIndex = 0;
        _cboListingType.Dock = DockStyle.Fill;
        topRow.Controls.Add(CreateLabeledControl("Ürün Tipi:", _cboListingType), 0, 0);

        _numPrice.Dock = DockStyle.Fill;
        _numPrice.Font = new Font("Segoe UI Semibold", 9F);
        topRow.Controls.Add(CreateLabeledControl("Fiyat ($):", _numPrice), 1, 0);

        _numQuantity.Dock = DockStyle.Fill;
        _numQuantity.Font = new Font("Segoe UI Semibold", 9F);
        topRow.Controls.Add(CreateLabeledControl("Stok Adedi:", _numQuantity), 2, 0);
        stack.Controls.Add(topRow);

        // --- SECTION 2: Etsy SEO Başlığı ---
        var titleHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 32,
            Margin = new Padding(0, 4, 0, 4)
        };
        titleHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        titleHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLeftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = Padding.Empty
        };
        var lblTitleSec = CreateSectionHeaderLabel("✍️ Ürün Başlığı (SEO)");
        lblTitleSec.Margin = new Padding(0, 3, 4, 0);
        _lblTitleCounter.Text = "0 / 140";
        _lblTitleCounter.Font = new Font("Segoe UI Semibold", 8.2F);
        _lblTitleCounter.ForeColor = UiStyle.TextMuted;
        _lblTitleCounter.AutoSize = true;
        _lblTitleCounter.Margin = new Padding(0, 5, 0, 0);
        titleLeftFlow.Controls.Add(lblTitleSec);
        titleLeftFlow.Controls.Add(_lblTitleCounter);
        titleHeader.Controls.Add(titleLeftFlow, 0, 0);

        var btnAiTitle = CreateModernActionButton("✨ AI Başlık Öner", UiStyle.AiColor, UiStyle.AiHover, Color.White, 26);
        btnAiTitle.Margin = new Padding(8, 2, 2, 2);
        btnAiTitle.Click += async (_, _) => await SuggestAiTitleAsync();
        _galleryToolTip.SetToolTip(btnAiTitle, "Ürün için Etsy SEO algoritmasına uyumlu optimize edilmiş başlık önerir.");
        titleHeader.Controls.Add(btnAiTitle, 1, 0);
        stack.Controls.Add(titleHeader);

        _txtTitle.Height = 58;
        _txtTitle.Font = new Font("Segoe UI", 8.8F);
        _txtTitle.Dock = DockStyle.Top;
        _txtTitle.Margin = new Padding(0, 0, 0, 6);
        _galleryToolTip.SetToolTip(_txtTitle, "80-140 karakter arası başlıklar Etsy SEO aramalarında en yüksek performansı verir.");
        stack.Controls.Add(_txtTitle);

        // --- SECTION 3: Kategori & Kargo Ayarları ---
        var sec3Header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 32,
            Margin = new Padding(0, 6, 0, 4)
        };
        sec3Header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        sec3Header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblSec3 = CreateSectionHeaderLabel("📂 Kategori & Lojistik");
        lblSec3.Margin = new Padding(0, 3, 0, 0);
        sec3Header.Controls.Add(lblSec3, 0, 0);

        var btnAiCategory = CreateModernActionButton("✨ AI Kategori Belirle", UiStyle.AiColor, UiStyle.AiHover, Color.White, 26);
        btnAiCategory.Margin = new Padding(8, 2, 2, 2);
        btnAiCategory.Click += async (_, _) => await SuggestAiCategoryAsync();
        _galleryToolTip.SetToolTip(btnAiCategory, "Başlık ve ürün görsellerini analiz ederek en uygun Etsy kategorisini belirler.");
        sec3Header.Controls.Add(btnAiCategory, 1, 0);
        stack.Controls.Add(sec3Header);

        _cboTaxonomy.Dock = DockStyle.Top;
        _cboTaxonomy.Font = new Font("Segoe UI", 8.8F);
        _cboTaxonomy.Margin = new Padding(0, 0, 0, 4);
        _cboTaxonomy.Items.Clear();
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
            bool isCustom = _cboTaxonomy.SelectedIndex == _cboTaxonomy.Items.Count - 1;
            _txtCustomTaxonomy.Visible = isCustom;
            _txtCustomTaxonomy.ReadOnly = !isCustom;
            if (!isCustom)
            {
                var selected = _cboTaxonomy.SelectedItem?.ToString() ?? "";
                var idStr = selected.Split('-')[0].Trim();
                _txtCustomTaxonomy.Text = idStr;
            }
            scrollContainer.RecalculateScroll();
        };
        stack.Controls.Add(_cboTaxonomy);

        _txtCustomTaxonomy.Dock = DockStyle.Top;
        _txtCustomTaxonomy.Visible = false;
        _txtCustomTaxonomy.Font = new Font("Segoe UI", 8.8F);
        _txtCustomTaxonomy.Margin = new Padding(0, 0, 0, 4);
        _galleryToolTip.SetToolTip(_txtCustomTaxonomy, "Özel Etsy Taxonomy / Kategori ID numarası");
        stack.Controls.Add(_txtCustomTaxonomy);

        var logisticsStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        logisticsStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _cboShippingProfile.Dock = DockStyle.Top;
        _cboShippingProfile.Font = new Font("Segoe UI", 8.8F);
        _cboShippingProfile.DisplayMember = nameof(EtsyShippingProfileOption.DisplayName);
        _cboShippingProfile.Margin = new Padding(0, 2, 0, 4);
        logisticsStack.Controls.Add(CreateLabeledControl("🚚 Kargo Profili:", _cboShippingProfile));

        _cboReadinessState.Dock = DockStyle.Top;
        _cboReadinessState.Font = new Font("Segoe UI", 8.8F);
        _cboReadinessState.DisplayMember = nameof(EtsyReadinessStateOption.DisplayName);
        _cboReadinessState.Margin = new Padding(0, 2, 0, 4);
        logisticsStack.Controls.Add(CreateLabeledControl("⏱️ Hazırlık Durumu:", _cboReadinessState));
        stack.Controls.Add(logisticsStack);

        // --- SECTION 4: Arama Etiketleri (Tags - Maks 13) ---
        var tagHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 32,
            Margin = new Padding(0, 6, 0, 4)
        };
        tagHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        tagHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var tagLeftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = Padding.Empty
        };
        var lblTagSec = CreateSectionHeaderLabel("🏷️ Etiketler (Tags)");
        lblTagSec.Margin = new Padding(0, 3, 4, 0);
        _lblTagCounter.Text = "0 / 13";
        _lblTagCounter.Font = new Font("Segoe UI Semibold", 8.2F);
        _lblTagCounter.ForeColor = UiStyle.TextMuted;
        _lblTagCounter.AutoSize = true;
        _lblTagCounter.Margin = new Padding(0, 5, 0, 0);
        tagLeftFlow.Controls.Add(lblTagSec);
        tagLeftFlow.Controls.Add(_lblTagCounter);
        tagHeader.Controls.Add(tagLeftFlow, 0, 0);

        var tagBtnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var btnAiTags = CreateModernActionButton("✨ 13 AI Tag", UiStyle.AiColor, UiStyle.AiHover, Color.White, 26);
        btnAiTags.Margin = new Padding(6, 2, 2, 2);
        btnAiTags.Click += async (_, _) => await SuggestAiTagsAsync();
        _galleryToolTip.SetToolTip(btnAiTags, "Ürününüz için en uygun 13 Etsy etiketini AI ile otomatik üretir.");

        var btnCleanTags = CreateModernActionButton("🧹 Kırp", UiStyle.SecondaryColor, UiStyle.SecondaryHover, UiStyle.TextDark, 26);
        btnCleanTags.Margin = new Padding(2, 2, 6, 2);
        btnCleanTags.FlatAppearance.BorderSize = 1;
        btnCleanTags.FlatAppearance.BorderColor = UiStyle.BorderColor;
        btnCleanTags.Click += (_, _) => CleanAndFormatTags();
        _galleryToolTip.SetToolTip(btnCleanTags, "Etiketleri maksimum 20 karaktere kırpar ve formatlar.");

        tagBtnFlow.Controls.Add(btnAiTags);
        tagBtnFlow.Controls.Add(btnCleanTags);
        tagHeader.Controls.Add(tagBtnFlow, 1, 0);
        stack.Controls.Add(tagHeader);

        _txtTags.Dock = DockStyle.Top;
        _txtTags.Height = 72;
        _txtTags.Font = new Font("Segoe UI", 8.8F);
        _txtTags.Margin = new Padding(0, 0, 0, 2);
        _galleryToolTip.SetToolTip(_txtTags, "Her etiket maksimum 20 karakterdir. Virgülle veya yeni satırla ayırabilirsiniz.");
        stack.Controls.Add(_txtTags);

        _lblTagStatus.Dock = DockStyle.Top;
        _lblTagStatus.Font = new Font("Segoe UI", 7.8F);
        _lblTagStatus.ForeColor = UiStyle.TextMuted;
        _lblTagStatus.UseMnemonic = false;
        _lblTagStatus.Text = "Henüz etiket eklenmedi. En fazla 13 etiket ekleyebilirsiniz.";
        _lblTagStatus.Margin = new Padding(1, 0, 0, 6);
        stack.Controls.Add(_lblTagStatus);

        // --- SECTION 5: Ürün Açıklaması ---
        var descHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 32,
            Margin = new Padding(0, 6, 0, 4)
        };
        descHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        descHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblDescSec = CreateSectionHeaderLabel("📄 Ürün Açıklaması");
        lblDescSec.Margin = new Padding(0, 3, 0, 0);
        descHeader.Controls.Add(lblDescSec, 0, 0);

        var btnAiDesc = CreateModernActionButton("✨ AI Açıklama Üret", UiStyle.AiColor, UiStyle.AiHover, Color.White, 26);
        btnAiDesc.Margin = new Padding(8, 2, 2, 2);
        btnAiDesc.Click += async (_, _) => await SuggestAiDescriptionAsync();
        _galleryToolTip.SetToolTip(btnAiDesc, "Etsy alıcılarını ikna eden profesyonel ürün açıklaması yazar.");
        descHeader.Controls.Add(btnAiDesc, 1, 0);
        stack.Controls.Add(descHeader);

        _txtDescription.Dock = DockStyle.Top;
        _txtDescription.Height = 240;
        _txtDescription.Font = new Font("Segoe UI", 8.8F);
        _txtDescription.Margin = new Padding(0, 0, 0, 6);
        stack.Controls.Add(_txtDescription);

        // --- SECTION 6: Kullanılan Malzemeler ---
        var sec6Header = CreateSectionHeaderLabel("🧵 Kullanılan Malzemeler (Materials)");
        sec6Header.Margin = new Padding(0, 2, 0, 2);
        stack.Controls.Add(sec6Header);

        _txtMaterials.Dock = DockStyle.Top;
        _txtMaterials.Font = new Font("Segoe UI", 8.8F);
        _txtMaterials.Height = 30;
        _txtMaterials.Margin = new Padding(0, 0, 0, 8);
        _galleryToolTip.SetToolTip(_txtMaterials, "Etsy filtreleri için virgülle ayırarak yazın (ör: Ahşap, PLA, Seramik, Deri).");
        stack.Controls.Add(_txtMaterials);

        scrollContainer.SetContent(stack);
        scrollContainer.Resize += (_, _) =>
        {
            if (scrollContainer.ClientSize.Width > 0)
            {
                stack.Width = Math.Max(200, scrollContainer.ClientSize.Width - 14);
            }
            if (scrollContainer.ClientSize.Height > 0)
            {
                int targetDescHeight = Math.Max(180, scrollContainer.ClientSize.Height - 510);
                if (_txtDescription.Height != targetDescHeight)
                {
                    _txtDescription.Height = targetDescHeight;
                    scrollContainer.RecalculateScroll();
                }
            }
        };
        cardLayout.Controls.Add(scrollContainer, 0, 2);
        card.Controls.Add(cardLayout);
        return card;
    }

    private Control BuildCenterColumn()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(4, 0, 4, 0)
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Header Bar
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));  // Divider Space
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main Content (Splitter)

        // 1. Header Bar with Title and Gallery Count Badge
        var headerBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            Text = "🖼️ 2. Görseller & AI Motoru",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        headerBar.Controls.Add(lblTitle, 0, 0);

        _lblGalleryCount.Text = "0/10";
        _lblGalleryCount.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        _lblGalleryCount.ForeColor = Color.FromArgb(196, 181, 253);
        _lblGalleryCount.BackColor = Color.FromArgb(46, 32, 70);
        _lblGalleryCount.AutoSize = true;
        _lblGalleryCount.Padding = new Padding(6, 2, 6, 2);
        _lblGalleryCount.Margin = new Padding(0, 2, 0, 0);
        _lblGalleryCount.TextAlign = ContentAlignment.MiddleCenter;
        headerBar.Controls.Add(_lblGalleryCount, 1, 0);
        cardLayout.Controls.Add(headerBar, 0, 0);

        // Subtle Divider Line
        var divider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = UiStyle.BorderColor,
            Margin = new Padding(0, 1, 0, 2)
        };
        cardLayout.Controls.Add(divider, 0, 1);

        // 2. Responsive Resizable Splitter between Gallery (Top) & AI Studio (Bottom)
        var centerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        bool hasInitializedSplitter = false;
        centerSplit.SizeChanged += (_, _) =>
        {
            if (centerSplit.Height > 240 && !hasInitializedSplitter)
            {
                try
                {
                    centerSplit.Panel1MinSize = 80;
                    centerSplit.Panel2MinSize = 120;
                    int target = (int)(centerSplit.Height * 0.36f);
                    if (target >= centerSplit.Panel1MinSize && target <= centerSplit.Height - centerSplit.Panel2MinSize)
                    {
                        centerSplit.SplitterDistance = target;
                        hasInitializedSplitter = true;
                    }
                }
                catch { }
            }
        };

        // --- Panel 1: Image Gallery & Upload Bar ---
        var galleryWrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty
        };
        galleryWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        galleryWrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Toolbar
        galleryWrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Gallery Box

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 2,
            Margin = new Padding(0, 2, 0, 6)
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var btnBrowse = new Button
        {
            Dock = DockStyle.Fill,
            Height = 30,
            Text = "📂 Bilgisayardan Seç",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 4, 2)
        };
        btnBrowse.FlatAppearance.BorderSize = 0;
        btnBrowse.Click += (_, _) => BrowseLocalImages();
        topBar.Controls.Add(btnBrowse, 0, 0);

        var btnFromStudio = new Button
        {
            Dock = DockStyle.Fill,
            Height = 30,
            Text = "🎨 Stüdyo Galerisi",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 2, 0, 2)
        };
        btnFromStudio.FlatAppearance.BorderSize = 0;
        btnFromStudio.Click += (_, _) => OpenStudioGalleryPicker();
        topBar.Controls.Add(btnFromStudio, 1, 0);
        galleryWrapper.Controls.Add(topBar, 0, 0);

        var galleryBox = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 8,
            CardColor = UiStyle.InputBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(4),
            Margin = new Padding(0, 1, 0, 1)
        };

        _galleryScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };
        _galleryFlow.Dock = DockStyle.Top;
        _galleryFlow.AutoSize = true;
        _galleryFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _galleryFlow.AutoScroll = false;
        _galleryFlow.BackColor = Color.Transparent;
        _galleryFlow.BorderStyle = BorderStyle.None;
        _galleryFlow.Padding = new Padding(2);
        _galleryScroll.SetContent(_galleryFlow);
        _galleryScroll.Resize += (_, _) =>
        {
            if (_galleryImagePaths.Count == 0) RefreshGalleryCards();
        };
        galleryBox.Controls.Add(_galleryScroll);
        galleryWrapper.Controls.Add(galleryBox, 0, 1);
        centerSplit.Panel1.Controls.Add(galleryWrapper);

        // Initial render for empty gallery
        RefreshGalleryCards();

        // --- Panel 2: AI Generator Studio ---
        var aiBox = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            CardColor = UiStyle.InputBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 1, 0, 0)
        };

        var aiLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty
        };
        aiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0: Header & Hint
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 1: Style Preset Chips
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 2: Prompt Header Row + Başlıktan Al
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // Row 3: Multiline Prompt Input (Spacious!)
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Row 4: Action Buttons (Görseli Üret & Galeriye Ekle)
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Row 5: AI Preview Frame (Dynamic remaining space)

        // Row 0: Section Header & Hint
        var aiHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 2, 0, 4)
        };
        aiHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        aiHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblAiTitle = new Label
        {
            Text = "✨ AI Görsel Stüdyosu",
            Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold),
            ForeColor = UiStyle.AiColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        };
        aiHeaderPanel.Controls.Add(lblAiTitle, 0, 0);

        var lblAiHint = new Label
        {
            Text = "⚡ Hızlı Üretim",
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true
        };
        aiHeaderPanel.Controls.Add(lblAiHint, 1, 0);
        aiLayout.Controls.Add(aiHeaderPanel, 0, 0);

        // Row 1: Style Preset Chips
        var chipsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0, 0, 0, 2),
            Padding = Padding.Empty
        };

        var promptPresets = new (string Label, string StyleKeyword)[]
        {
            ("☕ Masa", "on a warm cozy wooden cafe table with morning sunlight, subtle steam, aesthetic atmosphere"),
            ("🛋️ Salon", "displayed in a stylish Scandinavian modern living room, neutral aesthetic, architectural interior"),
            ("📸 Stüdyo", "isolated on a seamless pure white studio background, commercial softbox lighting, clean catalog"),
            ("🌿 Bohem", "on rustic reclaimed wood with lush green indoor potted plants and bohemian vibes"),
            ("🎁 Hediye", "with luxury artisan kraft gift wrapping, elegant satin ribbon, greeting card")
        };

        foreach (var (pLabel, pStyle) in promptPresets)
        {
            var btnChip = new Button
            {
                Text = pLabel,
                Font = new Font("Segoe UI Semibold", 7.5F),
                Height = 24,
                AutoSize = true,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 1, 3, 1)
            };
            btnChip.FlatAppearance.BorderSize = 1;
            btnChip.FlatAppearance.BorderColor = UiStyle.BorderColor;
            btnChip.Click += (_, _) =>
            {
                var prod = !string.IsNullOrWhiteSpace(_txtTitle.Text) ? _txtTitle.Text.Trim() : "Etsy product";
                _txtAiPrompt.Text = $"Professional commercial product photography of {prod}, {pStyle}, high detail, studio lighting, 8k render, etsy showcase";
            };
            chipsPanel.Controls.Add(btnChip);
        }
        aiLayout.Controls.Add(chipsPanel, 0, 1);

        // Row 2: Prompt Header Row + "✨ Başlıktan Al" Button
        var promptHeaderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 2, 0, 4)
        };
        promptHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        promptHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        promptHeaderRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var lblPromptLabel = new Label
        {
            Text = "✍️ Prompt / Görsel Açıklaması:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 1)
        };
        promptHeaderRow.Controls.Add(lblPromptLabel, 0, 0);

        var btnPromptFromTitle = new Button
        {
            Text = "✨ Başlıktan Al",
            Font = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold),
            Height = 24,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 2, 0, 2),
            Padding = new Padding(6, 1, 6, 1)
        };
        btnPromptFromTitle.FlatAppearance.BorderSize = 0;
        btnPromptFromTitle.MouseEnter += (_, _) => btnPromptFromTitle.BackColor = UiStyle.AiHover;
        btnPromptFromTitle.MouseLeave += (_, _) => btnPromptFromTitle.BackColor = UiStyle.AiColor;
        btnPromptFromTitle.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                _txtAiPrompt.Text = $"Professional commercial product photography of {_txtTitle.Text.Trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase";
            }
            else
            {
                MessageBox.Show(this, "Önce sol panelden bir ürün başlığı girin.", "Başlık Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _galleryToolTip.SetToolTip(btnPromptFromTitle, "Girdiğiniz ürün başlığını kullanarak otomatik profesyonel AI prompt oluşturur.");
        promptHeaderRow.Controls.Add(btnPromptFromTitle, 1, 0);
        aiLayout.Controls.Add(promptHeaderRow, 0, 2);

        // Row 3: Multiline Prompt Input (Full width, spacious & scrollable)
        _txtAiPrompt.Dock = DockStyle.Fill;
        _txtAiPrompt.Font = new Font("Segoe UI", 8.8F);
        _txtAiPrompt.Margin = new Padding(0, 2, 0, 6);
        _galleryToolTip.SetToolTip(_txtAiPrompt, "AI görsel üretimi için açıklama yazın veya yukarıdaki hazır stillerden birini seçin.");
        aiLayout.Controls.Add(_txtAiPrompt, 0, 3);

        // Row 4: Action Buttons (Görseli Üret & Galeriye Ekle)
        var aiActionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 6, 0, 8)
        };
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));

        _btnGenerateAi.Dock = DockStyle.Fill;
        _btnGenerateAi.Text = "🎨 Görseli Üret";
        _btnGenerateAi.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
        _btnGenerateAi.NormalColor = UiStyle.AiColor;
        _btnGenerateAi.HoverColor = UiStyle.AiHover;
        _btnGenerateAi.ForeColor = Color.White;
        _btnGenerateAi.Margin = new Padding(0, 2, 6, 2);
        _btnGenerateAi.Click += async (_, _) => await GenerateAiImageAsync();
        aiActionRow.Controls.Add(_btnGenerateAi, 0, 0);

        _btnAddToGallery.Dock = DockStyle.Fill;
        _btnAddToGallery.Text = "➕ Galeriye Ekle";
        _btnAddToGallery.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        _btnAddToGallery.NormalColor = UiStyle.SuccessColor;
        _btnAddToGallery.HoverColor = Color.FromArgb(5, 150, 105);
        _btnAddToGallery.ForeColor = Color.White;
        _btnAddToGallery.Margin = new Padding(2, 0, 0, 0);
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
        aiLayout.Controls.Add(aiActionRow, 0, 4);

        // Row 5: AI Preview Container (DockStyle.Fill - Adapts automatically without overflow)
        var previewContainer = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 8,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(4),
            Margin = new Padding(0, 2, 0, 0)
        };

        _picAiPreview.Dock = DockStyle.Fill;
        _picAiPreview.BackColor = Color.FromArgb(18, 20, 29);
        _picAiPreview.BorderStyle = BorderStyle.None;
        _picAiPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picAiPreview.Paint += (s, e) =>
        {
            if (_picAiPreview.Image == null)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using var titleFont = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
                using var hintFont = new Font("Segoe UI", 7.8F);
                using var titleBrush = new SolidBrush(Color.FromArgb(200, 210, 230));
                using var hintBrush = new SolidBrush(UiStyle.TextMuted);

                var rect = _picAiPreview.ClientRectangle;
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                int cy = rect.Height / 2;
                var topRect = new Rectangle(0, Math.Max(0, cy - 22), rect.Width, 22);
                var botRect = new Rectangle(0, cy + 2, rect.Width, 20);

                e.Graphics.DrawString("✨ AI Görsel Önizleme Alanı", titleFont, titleBrush, topRect, sf);
                e.Graphics.DrawString("Prompt yazıp 'Görseli Üret' butonuna basın", hintFont, hintBrush, botRect, sf);
            }
        };
        previewContainer.Controls.Add(_picAiPreview);
        aiLayout.Controls.Add(previewContainer, 0, 5);

        aiBox.Controls.Add(aiLayout);
        centerSplit.Panel2.Controls.Add(aiBox);

        cardLayout.Controls.Add(centerSplit, 0, 2);
        card.Controls.Add(cardLayout);
        return card;
    }

    private Control BuildRightColumn()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(4, 0, 0, 0)
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Header Bar
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));  // Divider Space
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Scrollable Form Content

        // 1. Header Bar with Title
        var headerBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Margin = new Padding(0)
        };
        headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "⚙️ 3. Varyasyonlar & Kontrol",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        headerBar.Controls.Add(lblTitle, 0, 0);
        cardLayout.Controls.Add(headerBar, 0, 0);

        // Subtle Divider Line
        var divider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = UiStyle.BorderColor,
            Margin = new Padding(0, 1, 0, 2)
        };
        cardLayout.Controls.Add(divider, 0, 1);

        _rightScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 2, 0)
        };

        var stack = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 8, 0)
        };
        stack.ColumnStyles.Clear();
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // 1. Variations Section
        stack.Controls.Add(CreateSectionHeaderLabel("🧩 Varyasyonlar (Seçenekler)"));

        var varTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        varTable.ColumnStyles.Clear();
        varTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _chkEnableVariations.Dock = DockStyle.Top;
        _chkEnableVariations.Margin = new Padding(0, 2, 0, 4);
        varTable.Controls.Add(_chkEnableVariations);

        var lblVar1 = new Label { Text = "1. Varyasyon Tipi:", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.2F), Margin = new Padding(0, 4, 0, 1) };
        varTable.Controls.Add(lblVar1);

        _cboVarType1.Items.Clear();
        _cboVarType1.Items.AddRange([
            "📏 Boyut / Size",
            "🎨 Renk / Color",
            "🪵 Malzeme / Material",
            "✨ Stil / Style",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType1.SelectedIndex = 0;
        _cboVarType1.Dock = DockStyle.Top;
        _cboVarType1.Font = new Font("Segoe UI", 8.8F);
        varTable.Controls.Add(_cboVarType1);

        _txtVarValues1.Dock = DockStyle.Top;
        _txtVarValues1.Font = new Font("Segoe UI", 8.8F);
        _txtVarValues1.Margin = new Padding(0, 3, 0, 4);
        varTable.Controls.Add(_txtVarValues1);

        _chkEnableVar2.Dock = DockStyle.Top;
        _chkEnableVar2.Margin = new Padding(0, 4, 0, 4);
        varTable.Controls.Add(_chkEnableVar2);

        _cboVarType2.Items.Clear();
        _cboVarType2.Items.AddRange([
            "🎨 Renk / Color",
            "📏 Boyut / Size",
            "🪵 Malzeme / Material",
            "✨ Stil / Style",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType2.SelectedIndex = 0;
        _cboVarType2.Dock = DockStyle.Top;
        _cboVarType2.Font = new Font("Segoe UI", 8.8F);
        _cboVarType2.Visible = false;
        varTable.Controls.Add(_cboVarType2);

        _txtVarValues2.Dock = DockStyle.Top;
        _txtVarValues2.Font = new Font("Segoe UI", 8.8F);
        _txtVarValues2.Visible = false;
        _txtVarValues2.Margin = new Padding(0, 3, 0, 4);
        varTable.Controls.Add(_txtVarValues2);

        _lblVarCombinations.Dock = DockStyle.Top;
        _lblVarCombinations.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
        _lblVarCombinations.ForeColor = UiStyle.TextMuted;
        _lblVarCombinations.Text = "Varyasyon kapalı.";
        _lblVarCombinations.Margin = new Padding(0, 2, 0, 3);
        varTable.Controls.Add(_lblVarCombinations);

        // Custom Variation Pricing Toggle & Grid
        _chkCustomVariationPricing.Dock = DockStyle.Top;
        _chkCustomVariationPricing.Margin = new Padding(0, 4, 0, 3);
        varTable.Controls.Add(_chkCustomVariationPricing);
        varTable.Controls.Add(BuildVariationPricingPanel());

        stack.Controls.Add(varTable);

        // 2. Canlı Hazırlık Kontrol Listesi & Yayınlama Kutusu
        stack.Controls.Add(CreateSectionHeaderLabel("🚀 Dağıtım & Kontrol"));

        var pubTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        pubTable.ColumnStyles.Clear();
        pubTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // Checklist items
        var chkHeader = new Label
        {
            Text = "📋 Canlı Kontrol Listesi:",
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        pubTable.Controls.Add(chkHeader);

        InitChecklistLabel(_chkItemTitle, "Ürün başlığı hazır");
        InitChecklistLabel(_chkItemPrice, "Fiyat ve stok geçerli");
        InitChecklistLabel(_chkItemImage, "En az 1 görsel eklendi");
        InitChecklistLabel(_chkItemShipping, "Kargo profili seçildi");
        InitChecklistLabel(_chkItemReadiness, "Hazırlık durumu seçildi");
        InitChecklistLabel(_chkItemDesc, "Açıklama dolduruldu");
        InitChecklistLabel(_chkItemTags, "Etiketler (0/13)");

        pubTable.Controls.Add(_chkItemTitle);
        pubTable.Controls.Add(_chkItemPrice);
        pubTable.Controls.Add(_chkItemImage);
        pubTable.Controls.Add(_chkItemShipping);
        pubTable.Controls.Add(_chkItemReadiness);
        pubTable.Controls.Add(_chkItemDesc);
        pubTable.Controls.Add(_chkItemTags);

        // Modern Publish Mode Toggle Card
        _chkMakeActive.Dock = DockStyle.Top;
        _chkMakeActive.Margin = new Padding(0, 6, 0, 5);
        _chkMakeActive.CheckedChanged += (_, _) => UpdatePublishButtonVisuals();
        pubTable.Controls.Add(_chkMakeActive);

        // Secondary Publish Button
        _btnPublish.Dock = DockStyle.Top;
        _btnPublish.Height = 40;
        _btnPublish.Margin = new Padding(0, 8, 0, 8);
        _btnPublish.ForeColor = Color.White;
        _btnPublish.Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold);
        _btnPublish.Click += async (_, _) => await PublishListingToEtsyAsync();
        UpdatePublishButtonVisuals();
        pubTable.Controls.Add(_btnPublish);

        // Secondary Preview Button
        _btnPreviewSecondary.Dock = DockStyle.Top;
        _btnPreviewSecondary.Height = 28;
        _btnPreviewSecondary.Text = "👁️ Canlı Önizleme";
        _btnPreviewSecondary.Font = new Font("Segoe UI", 8.2F);
        _btnPreviewSecondary.BackColor = UiStyle.SecondaryColor;
        _btnPreviewSecondary.ForeColor = UiStyle.TextDark;
        _btnPreviewSecondary.FlatStyle = FlatStyle.Flat;
        _btnPreviewSecondary.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnPreviewSecondary.Cursor = Cursors.Hand;
        _btnPreviewSecondary.Margin = new Padding(0, 0, 0, 4);
        _btnPreviewSecondary.Click += (_, _) => OpenEtsyListingPreview();
        pubTable.Controls.Add(_btnPreviewSecondary);

        var lblNote = new Label
        {
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 7.6F),
            ForeColor = UiStyle.TextMuted,
            Text = "💡 Güvenli Gönderim: Taslak listelemeler mağazanızda doğrudan görünmez, Etsy panelinizden onaylayabilirsiniz.",
            AutoSize = false,
            Height = 36,
            Margin = new Padding(0, 2, 0, 2)
        };
        pubTable.Controls.Add(lblNote);

        stack.Controls.Add(pubTable);
        stack.Controls.Add(BuildQualityAndReadinessSummaryCard());

        _rightScroll.SetContent(stack);
        _rightScroll.Resize += (_, _) =>
        {
            if (_rightScroll.ClientSize.Width > 0)
            {
                stack.Width = Math.Max(180, _rightScroll.ClientSize.Width - 14);
            }
        };
        cardLayout.Controls.Add(_rightScroll, 0, 2);
        card.Controls.Add(cardLayout);
        return card;
    }

    private Control BuildQualityAndReadinessSummaryCard()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Top,
            CornerRadius = 8,
            CardColor = UiStyle.InputBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 8, 0, 4)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var lblHeader = new Label
        {
            Text = "✨ Etsy Algoritma & Kalite Özeti",
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblHeader);

        var lblScore = new Label
        {
            Text = "⭐ Listeleme Kalite Skoru: 95/100 (Çok İyi)",
            Font = new Font("Segoe UI Semibold", 8.2F),
            ForeColor = UiStyle.SuccessColor,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblScore);

        var lblTip1 = new Label
        {
            Text = "• 📸 En az 5 görsel eklemek dönüşüm oranını %35 artırır.",
            Font = new Font("Segoe UI", 7.8F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        layout.Controls.Add(lblTip1);

        var lblTip2 = new Label
        {
            Text = "• 🏷️ 13 etiket ve 100+ karakter SEO başlığı Etsy aramasında öne çıkarır.",
            Font = new Font("Segoe UI", 7.8F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        layout.Controls.Add(lblTip2);

        var lblTip3 = new Label
        {
            Text = "• ⚡ Taslak gönderimler Etsy panelinizden onaylanmadan müşterilere görünmez.",
            Font = new Font("Segoe UI", 7.8F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };
        layout.Controls.Add(lblTip3);

        card.Controls.Add(layout);
        return card;
    }

    private void UpdatePublishButtonVisuals()
    {
        if (_chkMakeActive.Checked)
        {
            _btnPublish.Text = "🚀 Etsy'ye Gönder (Canlı)";
            _btnPublish.NormalColor = UiStyle.SuccessColor;
            _btnPublish.HoverColor = Color.FromArgb(5, 150, 105);
        }
        else
        {
            _btnPublish.Text = "💾 Etsy'ye Gönder (Taslak)";
            _btnPublish.NormalColor = Color.FromArgb(79, 70, 229);
            _btnPublish.HoverColor = Color.FromArgb(67, 56, 202);
        }
        _btnPublish.Invalidate();
    }

    private Control BuildVariationPricingPanel()
    {
        _pnlVariationPricing.Dock = DockStyle.Top;
        _pnlVariationPricing.Height = 186;
        _pnlVariationPricing.MinimumSize = new Size(240, 186);
        _pnlVariationPricing.ColumnCount = 1;
        _pnlVariationPricing.RowCount = 3;
        _pnlVariationPricing.ColumnStyles.Clear();
        _pnlVariationPricing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _pnlVariationPricing.RowStyles.Clear();
        _pnlVariationPricing.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // Row 0: Quick tools
        _pnlVariationPricing.RowStyles.Add(new RowStyle(SizeType.Absolute, 124)); // Row 1: DataGridView
        _pnlVariationPricing.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // Row 2: Price range badge
        _pnlVariationPricing.Padding = new Padding(0, 2, 0, 4);
        _pnlVariationPricing.Controls.Clear();

        var topFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 2)
        };

        _btnSyncBasePrice.Text = "⚡ Fiyatı Eşitle";
        _btnSyncBasePrice.Font = new Font("Segoe UI", 7.5F);
        _btnSyncBasePrice.Height = 24;
        _btnSyncBasePrice.AutoSize = true;
        _btnSyncBasePrice.BackColor = UiStyle.SecondaryColor;
        _btnSyncBasePrice.ForeColor = UiStyle.TextDark;
        _btnSyncBasePrice.FlatStyle = FlatStyle.Flat;
        _btnSyncBasePrice.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnSyncBasePrice.Cursor = Cursors.Hand;
        _btnSyncBasePrice.Click += (_, _) => SyncBasePriceToAllVariations();
        topFlow.Controls.Add(_btnSyncBasePrice);

        _btnStepPrice.Text = "📈 +$5 Kademeli";
        _btnStepPrice.Font = new Font("Segoe UI", 7.5F);
        _btnStepPrice.Height = 24;
        _btnStepPrice.AutoSize = true;
        _btnStepPrice.BackColor = UiStyle.SecondaryColor;
        _btnStepPrice.ForeColor = UiStyle.TextDark;
        _btnStepPrice.FlatStyle = FlatStyle.Flat;
        _btnStepPrice.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnStepPrice.Cursor = Cursors.Hand;
        _btnStepPrice.Margin = new Padding(4, 0, 0, 0);
        _btnStepPrice.Click += (_, _) => ApplyStepPricing();
        topFlow.Controls.Add(_btnStepPrice);

        _pnlVariationPricing.Controls.Add(topFlow, 0, 0);

        // Grid setup
        UiStyle.ConfigureBaseGrid(_gridVariationPricing);
        _gridVariationPricing.ReadOnly = false;
        _gridVariationPricing.EditMode = DataGridViewEditMode.EditOnEnter;
        _gridVariationPricing.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _gridVariationPricing.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridVariationPricing.RowTemplate.Height = 32;

        _gridVariationPricing.Columns.Clear();
        var colKey = new DataGridViewTextBoxColumn
        {
            Name = "ColKey",
            HeaderText = "Seçenek",
            ReadOnly = true,
            FillWeight = 46,
            MinimumWidth = 70
        };
        var colPrice = new DataGridViewTextBoxColumn
        {
            Name = "ColPrice",
            HeaderText = "Fiyat ($)",
            FillWeight = 26,
            MinimumWidth = 55
        };
        var colQty = new DataGridViewTextBoxColumn
        {
            Name = "ColQty",
            HeaderText = "Stok",
            FillWeight = 16,
            MinimumWidth = 40
        };
        var colActive = new DataGridViewCheckBoxColumn
        {
            Name = "ColActive",
            HeaderText = "Aktif",
            FillWeight = 12,
            MinimumWidth = 35
        };

        _gridVariationPricing.Columns.AddRange([colKey, colPrice, colQty, colActive]);
        _gridVariationPricing.CellPainting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _gridVariationPricing.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
            {
                e.PaintBackground(e.CellBounds, true);
                bool isChecked = false;
                if (e.FormattedValue is bool b) isChecked = b;
                else if (e.Value is bool b2) isChecked = b2;

                int boxSize = 16;
                int bx = e.CellBounds.X + (e.CellBounds.Width - boxSize) / 2;
                int by = e.CellBounds.Y + (e.CellBounds.Height - boxSize) / 2;
                var boxRect = new Rectangle(bx, by, boxSize, boxSize);

                if (e.Graphics != null)
                {
                    ModernCheckBox.DrawBox(
                        e.Graphics,
                        boxRect,
                        isChecked,
                        false,
                        false,
                        false,
                        true,
                        boxSize,
                        4);
                }
                e.Handled = true;
            }
        };
        _pnlVariationPricing.Controls.Add(_gridVariationPricing, 0, 1);

        _lblPriceRangeBadge.Dock = DockStyle.Fill;
        _lblPriceRangeBadge.Font = new Font("Segoe UI Semibold", 8F);
        _lblPriceRangeBadge.ForeColor = UiStyle.PrimaryColor;
        _lblPriceRangeBadge.Text = "📊 Fiyat Aralığı: Belirlenmedi";
        _lblPriceRangeBadge.Margin = new Padding(0, 4, 0, 2);
        _pnlVariationPricing.Controls.Add(_lblPriceRangeBadge, 0, 2);

        return _pnlVariationPricing;
    }

    private static void InitChecklistLabel(Label lbl, string text)
    {
        lbl.Text = $"⚪ {text}";
        lbl.Font = new Font("Segoe UI", 8F);
        lbl.ForeColor = UiStyle.TextMuted;
        lbl.Margin = new Padding(2, 1, 2, 1);
    }

    private static Control CreateLabeledControl(string labelText, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(2, 1, 2, 3)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        panel.Controls.Add(new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            UseMnemonic = false,
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);

        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private static Label CreateSectionHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 9.6F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Top,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 8, 0, 4)
        };
    }

    private static Button CreateModernActionButton(string text, Color backColor, Color hoverColor, Color foreColor, int height = 28)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Height = height,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 2, 2, 2),
            Padding = new Padding(8, 2, 8, 2)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (_, _) => btn.BackColor = hoverColor;
        btn.MouseLeave += (_, _) => btn.BackColor = backColor;
        return btn;
    }

    private void WireEvents()
    {
        _txtTitle.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
            }
        };

        _txtTitle.TextChanged += (_, _) =>
        {
            if (_txtTitle.Text.Contains('\n') || _txtTitle.Text.Contains('\r'))
            {
                var caret = _txtTitle.SelectionStart;
                _txtTitle.Text = _txtTitle.Text.Replace("\r", "").Replace("\n", " ");
                _txtTitle.SelectionStart = Math.Min(caret, _txtTitle.Text.Length);
            }

            var len = _txtTitle.Text.Length;
            _lblTitleCounter.Text = $"{len} / 140";
            _lblTitleCounter.ForeColor = len >= 80 && len <= 140 ? UiStyle.SuccessColor : len > 140 ? UiStyle.DangerColor : UiStyle.TextMuted;
            UpdateChecklist();
        };

        _numPrice.ValueChanged += (_, _) =>
        {
            _btnSyncBasePrice.Text = $"⚡ Eşitle (${_numPrice.Value:0.00})";
            UpdateChecklist();
        };
        _numQuantity.ValueChanged += (_, _) => UpdateChecklist();
        _cboShippingProfile.SelectedIndexChanged += (_, _) => UpdateChecklist();
        _cboReadinessState.SelectedIndexChanged += (_, _) => UpdateChecklist();
        _cboListingType.SelectedIndexChanged += (_, _) =>
        {
            bool isDig = _cboListingType.SelectedIndex == 1;
            _cboShippingProfile.Enabled = !isDig;
            _cboReadinessState.Enabled = !isDig;
            UpdateChecklist();
        };
        _txtDescription.TextChanged += (_, _) => UpdateChecklist();

        _txtTags.TextChanged += (_, _) =>
        {
            UpdateTagStatus();
            UpdateChecklist();
        };

        _chkEnableVariations.CheckedChanged += (_, _) =>
        {
            UpdateVariationsDisplay();
            _pnlVariationPricing.Visible = _chkEnableVariations.Checked && _chkCustomVariationPricing.Checked;
            if (_pnlVariationPricing.Visible)
            {
                _pnlVariationPricing.Height = 186;
                RefreshVariationPricingGrid();
            }
            UpdateChecklist();
            _rightScroll?.RecalculateScroll();
        };

        _chkEnableVar2.CheckedChanged += (_, _) =>
        {
            _cboVarType2.Visible = _chkEnableVar2.Checked;
            _txtVarValues2.Visible = _chkEnableVar2.Checked;
            UpdateVariationsDisplay();
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
            _rightScroll?.RecalculateScroll();
        };

        _txtVarValues1.TextChanged += (_, _) =>
        {
            UpdateVariationsDisplay();
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
            _rightScroll?.RecalculateScroll();
        };

        _txtVarValues2.TextChanged += (_, _) =>
        {
            UpdateVariationsDisplay();
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
            _rightScroll?.RecalculateScroll();
        };

        _chkCustomVariationPricing.CheckedChanged += (_, _) =>
        {
            if (_chkCustomVariationPricing.Checked && !_chkEnableVariations.Checked)
            {
                _chkEnableVariations.Checked = true;
            }

            _pnlVariationPricing.Visible = _chkEnableVariations.Checked && _chkCustomVariationPricing.Checked;
            if (_pnlVariationPricing.Visible)
            {
                _pnlVariationPricing.Height = 186;
                RefreshVariationPricingGrid();
            }
            UpdateChecklist();
            _rightScroll?.RecalculateScroll();
        };

        _gridVariationPricing.CellValueChanged += (_, _) => OnVariationGridCellValueChanged();
        _gridVariationPricing.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_gridVariationPricing.IsCurrentCellDirty && _gridVariationPricing.CurrentCell is DataGridViewCheckBoxCell)
            {
                _gridVariationPricing.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _gridVariationPricing.EditingControlShowing += (_, e) =>
        {
            if (e.Control is TextBox tb)
            {
                tb.SelectAll();
                tb.DoubleClick -= OnVariationGridEditingControlDoubleClick;
                tb.DoubleClick += OnVariationGridEditingControlDoubleClick;
            }
        };
        _gridVariationPricing.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                PromptEditVariationRow(e.RowIndex);
            }
        };
        _gridVariationPricing.CellMouseDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                PromptEditVariationRow(e.RowIndex);
            }
        };
        _gridVariationPricing.RowHeaderMouseDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                PromptEditVariationRow(e.RowIndex);
            }
        };

        UpdateChecklist();
    }

    private void UpdateChecklist()
    {
        // 1. Title
        bool titleOk = !string.IsNullOrWhiteSpace(_txtTitle.Text) && _txtTitle.Text.Length <= 140;
        SetChecklistItem(_chkItemTitle, "Ürün başlığı hazır", titleOk);

        // 2. Price & Stock
        bool priceOk = _numPrice.Value > 0 && _numQuantity.Value >= 1;
        string priceCheckText = $"Fiyat: ${_numPrice.Value:0.00} | Stok: {_numQuantity.Value}";
        if (_chkEnableVariations.Checked && _chkCustomVariationPricing.Checked && _customVariationPrices.Count > 0)
        {
            var activePrices = _customVariationPrices.Values.Where(p => p.IsEnabled).Select(p => p.Price).ToList();
            if (activePrices.Count > 0)
            {
                var min = activePrices.Min();
                var max = activePrices.Max();
                priceCheckText = min != max ? $"Fiyatlar: ${min:0.00} - ${max:0.00}" : $"Fiyat: ${min:0.00}";
            }
        }
        SetChecklistItem(_chkItemPrice, priceCheckText, priceOk);

        // 3. Images
        bool imageOk = _galleryImagePaths.Count > 0;
        SetChecklistItem(_chkItemImage, $"Görseller: {_galleryImagePaths.Count}/10 adet", imageOk);

        // 4. Shipping Profile & Readiness State
        bool isDigital = _cboListingType.SelectedIndex == 1;
        bool shippingOk = isDigital || _cboShippingProfile.SelectedItem != null;
        SetChecklistItem(_chkItemShipping, isDigital ? "Dijital Ürün (Kargo gerekmez)" : "Kargo profili seçildi", shippingOk);

        bool readinessOk = isDigital || SelectedReadinessStateId() > 0;
        SetChecklistItem(_chkItemReadiness, isDigital ? "Dijital (Hazırlık durumu gerekmez)" : "Hazırlık durumu seçildi", readinessOk);

        // 5. Description
        bool descOk = !string.IsNullOrWhiteSpace(_txtDescription.Text);
        SetChecklistItem(_chkItemDesc, "Açıklama dolduruldu", descOk);

        // 6. Tags
        var tags = SplitTags(_txtTags.Text);
        bool tagsOk = tags.Count > 0;
        SetChecklistItem(_chkItemTags, $"Etiketler ({tags.Count}/13)", tagsOk);
    }

    private static void SetChecklistItem(Label lbl, string text, bool isValid)
    {
        lbl.Text = isValid ? $"✅ {text}" : $"⚪ {text}";
        lbl.ForeColor = isValid ? UiStyle.SuccessColor : UiStyle.TextMuted;
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

    private void RefreshVariationPricingGrid()
    {
        if (_isUpdatingVariationGrid) return;

        var combinations = GetCurrentVariationCombinationKeys();
        if (combinations.Count == 0)
        {
            _gridVariationPricing.Rows.Clear();
            _lblPriceRangeBadge.Text = "📊 Fiyat Aralığı: Varyasyon değeri giriniz";
            return;
        }

        try
        {
            _isUpdatingVariationGrid = true;
            _gridVariationPricing.Rows.Clear();

            foreach (var key in combinations)
            {
                if (!_customVariationPrices.TryGetValue(key, out var pInfo))
                {
                    pInfo = (_numPrice.Value, (int)_numQuantity.Value, true);
                    _customVariationPrices[key] = pInfo;
                }

                int rowIdx = _gridVariationPricing.Rows.Add(
                    key,
                    pInfo.Price.ToString("0.00", CultureInfo.InvariantCulture),
                    pInfo.Quantity,
                    pInfo.IsEnabled);

                _gridVariationPricing.Rows[rowIdx].Tag = key;
            }

            UpdateVariationPriceRangeSummary();
        }
        finally
        {
            _isUpdatingVariationGrid = false;
        }
    }

    private List<string> GetCurrentVariationCombinationKeys()
    {
        var v1 = SplitTags(_txtVarValues1.Text);
        if (v1.Count == 0) return [];

        if (!_chkEnableVar2.Checked)
        {
            return v1;
        }

        var v2 = SplitTags(_txtVarValues2.Text);
        if (v2.Count == 0) return v1;

        var combinations = new List<string>();
        foreach (var val1 in v1)
        {
            foreach (var val2 in v2)
            {
                combinations.Add($"{val1} / {val2}");
            }
        }
        return combinations;
    }

    private void OnVariationGridCellValueChanged()
    {
        if (_isUpdatingVariationGrid) return;

        foreach (DataGridViewRow row in _gridVariationPricing.Rows)
        {
            var key = row.Cells["ColKey"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(key)) continue;

            var priceStr = row.Cells["ColPrice"].Value?.ToString() ?? "";
            var qtyStr = row.Cells["ColQty"].Value?.ToString() ?? "";
            var isActive = row.Cells["ColActive"].Value is bool b ? b : true;

            var normalizedPrice = priceStr.Trim().Replace(',', '.');
            decimal price = decimal.TryParse(normalizedPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : _numPrice.Value;
            int qty = int.TryParse(qtyStr, out var q) && q > 0 ? q : (int)_numQuantity.Value;

            _customVariationPrices[key] = (price, qty, isActive);
        }

        UpdateVariationPriceRangeSummary();
        UpdateChecklist();
    }

    private void SyncBasePriceToAllVariations()
    {
        _isUpdatingVariationGrid = true;
        try
        {
            foreach (DataGridViewRow row in _gridVariationPricing.Rows)
            {
                var key = row.Cells["ColKey"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;

                row.Cells["ColPrice"].Value = _numPrice.Value.ToString("0.00", CultureInfo.InvariantCulture);
                row.Cells["ColQty"].Value = (int)_numQuantity.Value;
                row.Cells["ColActive"].Value = true;

                _customVariationPrices[key] = (_numPrice.Value, (int)_numQuantity.Value, true);
            }
        }
        finally
        {
            _isUpdatingVariationGrid = false;
        }

        UpdateVariationPriceRangeSummary();
        UpdateChecklist();
    }

    private void ApplyStepPricing()
    {
        _isUpdatingVariationGrid = true;
        try
        {
            decimal curPrice = _numPrice.Value;
            foreach (DataGridViewRow row in _gridVariationPricing.Rows)
            {
                var key = row.Cells["ColKey"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;

                row.Cells["ColPrice"].Value = curPrice.ToString("0.00", CultureInfo.InvariantCulture);
                var isActive = row.Cells["ColActive"].Value is bool b ? b : true;
                int qty = int.TryParse(row.Cells["ColQty"].Value?.ToString(), out var q) ? q : (int)_numQuantity.Value;

                _customVariationPrices[key] = (curPrice, qty, isActive);
                curPrice += 5m;
            }
        }
        finally
        {
            _isUpdatingVariationGrid = false;
        }

        UpdateVariationPriceRangeSummary();
        UpdateChecklist();
    }

    private void OnVariationGridEditingControlDoubleClick(object? sender, EventArgs e)
    {
        if (_gridVariationPricing.CurrentRow != null && _gridVariationPricing.CurrentRow.Index >= 0)
        {
            var idx = _gridVariationPricing.CurrentRow.Index;
            _gridVariationPricing.EndEdit();
            PromptEditVariationRow(idx);
        }
    }

    private void PromptEditVariationRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _gridVariationPricing.Rows.Count) return;
        var row = _gridVariationPricing.Rows[rowIndex];
        var key = row.Cells["ColKey"].Value?.ToString() ?? "";
        var currentPriceStr = row.Cells["ColPrice"].Value?.ToString() ?? _numPrice.Value.ToString("0.00", CultureInfo.InvariantCulture);
        var currentQtyStr = row.Cells["ColQty"].Value?.ToString() ?? _numQuantity.Value.ToString(CultureInfo.InvariantCulture);
        var currentActive = row.Cells["ColActive"].Value is bool b ? b : true;

        using var dlg = new Form
        {
            Text = $"💰 Varyasyon Düzenle: {key}",
            Size = new Size(380, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.CardBackground,
            ShowInTaskbar = false
        };

        var lblOption = new Label
        {
            Text = $"Seçenek: {key}",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = UiStyle.PrimaryColor
        };

        var lblPrice = new Label
        {
            Text = "Fiyat ($):",
            Location = new Point(20, 48),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = UiStyle.TextDark
        };
        var txtPrice = new TextBox
        {
            Text = currentPriceStr,
            Location = new Point(130, 45),
            Width = 190,
            Font = new Font("Segoe UI Semibold", 9.5f)
        };
        txtPrice.SelectAll();

        var lblQty = new Label
        {
            Text = "Stok Adedi:",
            Location = new Point(20, 85),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = UiStyle.TextDark
        };
        var txtQty = new TextBox
        {
            Text = currentQtyStr,
            Location = new Point(130, 82),
            Width = 190,
            Font = new Font("Segoe UI Semibold", 9.5f)
        };

        txtPrice.KeyDown += (_, ke) =>
        {
            if (ke.KeyCode == Keys.Enter)
            {
                ke.SuppressKeyPress = true;
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
        };
        txtQty.KeyDown += (_, ke) =>
        {
            if (ke.KeyCode == Keys.Enter)
            {
                ke.SuppressKeyPress = true;
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
        };

        var chkActive = new ModernCheckBox
        {
            Text = "Satışta / Aktif",
            Location = new Point(130, 115),
            Checked = currentActive,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = UiStyle.TextDark
        };

        var btnOk = new Button
        {
            Text = "Kaydet",
            DialogResult = DialogResult.OK,
            Location = new Point(155, 150),
            Size = new Size(80, 32),
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text = "İptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(245, 150),
            Size = new Size(75, 32),
            BackColor = UiStyle.SecondaryColor,
            ForeColor = UiStyle.TextDark,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = UiStyle.BorderColor;

        dlg.Controls.AddRange([lblOption, lblPrice, txtPrice, lblQty, txtQty, chkActive, btnOk, btnCancel]);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            var pNorm = txtPrice.Text.Trim().Replace(',', '.');
            if (decimal.TryParse(pNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var pVal) && pVal > 0)
            {
                row.Cells["ColPrice"].Value = pVal.ToString("0.00", CultureInfo.InvariantCulture);
            }
            if (int.TryParse(txtQty.Text.Trim(), out var qVal) && qVal >= 0)
            {
                row.Cells["ColQty"].Value = qVal;
            }
            row.Cells["ColActive"].Value = chkActive.Checked;

            OnVariationGridCellValueChanged();
        }
    }

    private void UpdateVariationPriceRangeSummary()
    {
        var active = _customVariationPrices.Values.Where(v => v.IsEnabled).Select(v => v.Price).ToList();
        if (active.Count == 0)
        {
            _lblPriceRangeBadge.Text = "📊 Fiyat Aralığı: Aktif seçenek yok";
            _lblPriceRangeBadge.ForeColor = UiStyle.DangerColor;
            return;
        }

        var min = active.Min();
        var max = active.Max();
        var distinctCount = active.Distinct().Count();

        if (min == max)
        {
            _lblPriceRangeBadge.Text = $"📊 Fiyat: ${min:0.00} (Tüm seçenekler aynı)";
            _lblPriceRangeBadge.ForeColor = UiStyle.PrimaryColor;
        }
        else
        {
            _lblPriceRangeBadge.Text = $"📊 Fiyat Aralığı: ${min:0.00} — ${max:0.00} ({distinctCount} farklı fiyat)";
            _lblPriceRangeBadge.ForeColor = UiStyle.SuccessColor;
        }
    }

    private Dictionary<string, decimal>? GetCustomPricesForPreview()
    {
        if (!_chkEnableVariations.Checked || !_chkCustomVariationPricing.Checked || _customVariationPrices.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in _customVariationPrices)
        {
            dict[k] = v.Price;
        }
        return dict;
    }

    private Dictionary<string, DraftListingVariationPricing>? BuildCustomPricingForInventory()
    {
        if (!_chkEnableVariations.Checked || !_chkCustomVariationPricing.Checked || _customVariationPrices.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, DraftListingVariationPricing>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in _customVariationPrices)
        {
            dict[k] = new DraftListingVariationPricing(k, v.Price, v.Quantity, v.IsEnabled);
        }
        return dict;
    }

    private long SelectedReadinessStateId()
    {
        if (_cboReadinessState.SelectedValue is long val && val > 0)
        {
            return val;
        }
        if (_cboReadinessState.SelectedItem is EtsyReadinessStateOption opt && opt.ReadinessStateId > 0)
        {
            return opt.ReadinessStateId;
        }
        var text = _cboReadinessState.Text.Trim();
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var typedVal) && typedVal > 0)
        {
            return typedVal;
        }
        return 0;
    }

    private async Task InitializeFormDataAsync()
    {
        try
        {
            _statusLabel.Text = "Etsy mağaza kargo profilleri ve hazırlık durumları alınıyor...";
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials)
            {
                _statusLabel.Text = "Etsy API ayarları tanımlı değil. (Ayarlar menüsünden yapabilirsiniz)";
                return;
            }

            var profilesTask = _apiClient.GetOwnShopShippingProfilesAsync(settings);
            var readinessTask = _apiClient.GetOwnShopReadinessStateOptionsAsync(settings);

            await Task.WhenAll(profilesTask, readinessTask);
            EtsyApiSettingsStore.Save(settings);

            var profiles = await profilesTask;
            _cboShippingProfile.DataSource = profiles;
            if (profiles.Count > 0)
            {
                _cboShippingProfile.SelectedIndex = 0;
            }

            var readinessStates = await readinessTask;
            _cboReadinessState.DataSource = readinessStates;
            if (readinessStates.Count > 0)
            {
                _cboReadinessState.SelectedIndex = 0;
            }

            _statusLabel.Text = $"{profiles.Count} kargo profili, {readinessStates.Count} hazırlık durumu yüklendi.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Mağaza profilleri alınamadı: {ex.Message}";
        }
        finally
        {
            LoadTemplatesCombo();
            UpdateChecklist();
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
        UpdateChecklist();
    }

    private void RefreshGalleryCards()
    {
        _galleryFlow.Controls.Clear();
        _lblGalleryCount.Text = $"{_galleryImagePaths.Count}/10";

        if (_galleryImagePaths.Count == 0)
        {
            int availW = _galleryScroll != null && _galleryScroll.ClientSize.Width > 40
                ? _galleryScroll.ClientSize.Width - 20
                : 280;

            var emptyPanel = new Panel
            {
                Width = Math.Max(240, availW),
                Height = 90,
                Margin = new Padding(4, 8, 4, 8),
                BackColor = Color.FromArgb(20, 255, 255, 255)
            };

            var lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📷 Henüz görsel eklenmedi\nBilgisayardan dosya seçebilir veya AI stüdyosundan üretebilirsiniz.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.8F),
                ForeColor = UiStyle.TextMuted
            };
            emptyPanel.Controls.Add(lblEmpty);
            _galleryFlow.Controls.Add(emptyPanel);
            _galleryScroll?.RecalculateScroll();
            return;
        }

        for (int i = 0; i < _galleryImagePaths.Count; i++)
        {
            var index = i;
            var path = _galleryImagePaths[i];

            var card = new Panel
            {
                Width = 104,
                Height = 142,
                Margin = new Padding(4),
                BackColor = UiStyle.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true
            };

            var pic = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 84,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                Cursor = Cursors.SizeAll,
                AllowDrop = true
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
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 7.5F),
                Text = index == 0 ? "⭐ #1 Kapak" : $"#{index + 1}",
                ForeColor = index == 0 ? Color.White : UiStyle.TextDark,
                BackColor = index == 0 ? UiStyle.PrimaryColor : Color.Transparent,
            };
            card.Controls.Add(lblBadge);

            // 4-Button Reordering Toolbar: [ ◀ ] [ ⭐ ] [ ▶ ] [ 🗑️ ]
            var btnRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, ColumnCount = 4 };
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            // 1. Move Left / Forward
            var btnLeft = new Button
            {
                Text = "◀",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                Enabled = index > 0,
                Margin = new Padding(1)
            };
            btnLeft.FlatAppearance.BorderSize = 0;
            _galleryToolTip.SetToolTip(btnLeft, "Görseli Öne / Sola Taşı (◀)");
            btnLeft.Click += (_, _) => SwapGalleryImages(index, index - 1);
            btnRow.Controls.Add(btnLeft, 0, 0);

            // 2. Set as Cover Photo (#1)
            var btnStar = new Button
            {
                Text = "⭐",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                Enabled = index > 0,
                Margin = new Padding(1)
            };
            btnStar.FlatAppearance.BorderSize = 0;
            _galleryToolTip.SetToolTip(btnStar, "Kapak Görseli Yap (#1)");
            btnStar.Click += (_, _) =>
            {
                var temp = _galleryImagePaths[index];
                _galleryImagePaths.RemoveAt(index);
                _galleryImagePaths.Insert(0, temp);
                RefreshGalleryCards();
                UpdateChecklist();
            };
            btnRow.Controls.Add(btnStar, 1, 0);

            // 3. Move Right / Backward
            var btnRight = new Button
            {
                Text = "▶",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                Enabled = index < _galleryImagePaths.Count - 1,
                Margin = new Padding(1)
            };
            btnRight.FlatAppearance.BorderSize = 0;
            _galleryToolTip.SetToolTip(btnRight, "Görseli Arkaya / Sağa Taşı (▶)");
            btnRight.Click += (_, _) => SwapGalleryImages(index, index + 1);
            btnRow.Controls.Add(btnRight, 2, 0);

            // 4. Delete Image
            var btnDel = new Button
            {
                Text = "🗑️",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7F),
                ForeColor = UiStyle.DangerColor,
                BackColor = UiStyle.SecondaryColor,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(1)
            };
            btnDel.FlatAppearance.BorderSize = 0;
            _galleryToolTip.SetToolTip(btnDel, "Görseli Galeriden Sil");
            btnDel.Click += (_, _) =>
            {
                _galleryImagePaths.RemoveAt(index);
                RefreshGalleryCards();
                UpdateChecklist();
            };
            btnRow.Controls.Add(btnDel, 3, 0);

            card.Controls.Add(btnRow);

            // Drag & Drop Handling for both card and picture box
            _galleryToolTip.SetToolTip(pic, "Sırayı değiştirmek için basılı tutup sürükleyin veya alttaki ◀ ▶ butonlarına basın.");
            pic.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Clicks == 1)
                {
                    card.DoDragDrop(index, DragDropEffects.Move);
                }
            };
            card.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Clicks == 1)
                {
                    card.DoDragDrop(index, DragDropEffects.Move);
                }
            };

            void OnDragEnter(DragEventArgs e)
            {
                if (e.Data?.GetDataPresent(typeof(int)) == true)
                {
                    e.Effect = DragDropEffects.Move;
                    card.BackColor = UiStyle.PrimaryHover;
                }
            }

            void OnDragLeave()
            {
                card.BackColor = UiStyle.CardBackground;
            }

            void OnDragDrop(DragEventArgs e)
            {
                card.BackColor = UiStyle.CardBackground;
                if (e.Data?.GetDataPresent(typeof(int)) == true)
                {
                    int sourceIdx = (int)e.Data.GetData(typeof(int))!;
                    if (sourceIdx != index && sourceIdx >= 0 && sourceIdx < _galleryImagePaths.Count)
                    {
                        var item = _galleryImagePaths[sourceIdx];
                        _galleryImagePaths.RemoveAt(sourceIdx);
                        _galleryImagePaths.Insert(index, item);
                        RefreshGalleryCards();
                        UpdateChecklist();
                    }
                }
            }

            pic.DragEnter += (s, e) => OnDragEnter(e);
            card.DragEnter += (s, e) => OnDragEnter(e);
            pic.DragOver += (s, e) => { if (e.Data?.GetDataPresent(typeof(int)) == true) e.Effect = DragDropEffects.Move; };
            card.DragOver += (s, e) => { if (e.Data?.GetDataPresent(typeof(int)) == true) e.Effect = DragDropEffects.Move; };
            pic.DragLeave += (s, e) => OnDragLeave();
            card.DragLeave += (s, e) => OnDragLeave();
            pic.DragDrop += (s, e) => OnDragDrop(e);
            card.DragDrop += (s, e) => OnDragDrop(e);

            _galleryFlow.Controls.Add(card);
        }
        _galleryScroll?.RecalculateScroll();
    }

    private void SwapGalleryImages(int idx1, int idx2)
    {
        if (idx1 < 0 || idx1 >= _galleryImagePaths.Count || idx2 < 0 || idx2 >= _galleryImagePaths.Count || idx1 == idx2)
            return;

        (_galleryImagePaths[idx1], _galleryImagePaths[idx2]) = (_galleryImagePaths[idx2], _galleryImagePaths[idx1]);
        RefreshGalleryCards();
        UpdateChecklist();
    }

    // --- AI Suggestions & Generation ---

    private async Task SuggestAiCategoryAsync()
    {
        var title = _txtTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) && _galleryImagePaths.Count == 0 && string.IsNullOrWhiteSpace(_lastGeneratedImagePath))
        {
            MessageBox.Show(
                this,
                "Lütfen AI kategori analizi için bir ürün başlığı girin veya sol/orta panelden ürün görseli ekleyin.",
                "AI Kategori Analizi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "AI başlık ve ürün görsellerini analiz ederek en uygun Etsy kategorisini belirliyor...";

            var images = new List<string>(_galleryImagePaths);
            if (!string.IsNullOrWhiteSpace(_lastGeneratedImagePath) && File.Exists(_lastGeneratedImagePath) && !images.Contains(_lastGeneratedImagePath))
            {
                images.Insert(0, _lastGeneratedImagePath);
            }

            var result = await _categorySuggester.SuggestCategoryAsync(
                title,
                images,
                _txtDescription.Text.Trim(),
                _txtTags.Text.Trim());

            if (result.TaxonomyId > 0)
            {
                var mainDisplay = $"{result.TaxonomyId} - {result.CategoryPath}";

                // Ana öneriyi bul veya ekle ve seç
                int existingIdx = -1;
                for (int i = 0; i < _cboTaxonomy.Items.Count; i++)
                {
                    var itemStr = _cboTaxonomy.Items[i]?.ToString() ?? "";
                    if (itemStr.StartsWith($"{result.TaxonomyId} ", StringComparison.OrdinalIgnoreCase) ||
                        itemStr.StartsWith($"{result.TaxonomyId}-", StringComparison.OrdinalIgnoreCase) ||
                        itemStr.Equals(mainDisplay, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIdx = i;
                        break;
                    }
                }

                if (existingIdx >= 0)
                {
                    _cboTaxonomy.SelectedIndex = existingIdx;
                }
                else
                {
                    _cboTaxonomy.Items.Insert(0, mainDisplay);
                    _cboTaxonomy.SelectedIndex = 0;
                }

                _txtCustomTaxonomy.Text = result.TaxonomyId.ToString(CultureInfo.InvariantCulture);

                // Alternatif kategorileri de cboTaxonomy'ye ekleyelim (kullanıcı kolayca geçebilsin)
                foreach (var alt in result.Alternatives)
                {
                    var altDisplay = $"{alt.TaxonomyId} - {alt.CategoryPath}";
                    bool altExists = false;
                    for (int i = 0; i < _cboTaxonomy.Items.Count; i++)
                    {
                        var s = _cboTaxonomy.Items[i]?.ToString() ?? "";
                        if (s.StartsWith($"{alt.TaxonomyId} ", StringComparison.OrdinalIgnoreCase) ||
                            s.StartsWith($"{alt.TaxonomyId}-", StringComparison.OrdinalIgnoreCase))
                        {
                            altExists = true;
                            break;
                        }
                    }
                    if (!altExists)
                    {
                        _cboTaxonomy.Items.Insert(1, altDisplay);
                    }
                }

                UpdateChecklist();
                var reasoningShort = string.IsNullOrWhiteSpace(result.Reasoning) ? "" : $" ({result.Reasoning})";
                _statusLabel.Text = $"✨ Kategori belirlendi: {result.CategoryPath} [Taxonomy ID: {result.TaxonomyId}]{reasoningShort}";
            }
            else
            {
                _statusLabel.Text = "Kategori belirlenemedi; lütfen manuel Taxonomy ID giriniz.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"AI kategori belirleme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

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
            var title = _txtTitle.Text.Trim();
            var desc = _txtDescription.Text.Trim();
            var mats = _txtMaterials.Text.Trim();

            var combinedDesc = desc;
            if (!string.IsNullOrWhiteSpace(mats) && !combinedDesc.Contains(mats, StringComparison.OrdinalIgnoreCase))
            {
                combinedDesc = string.IsNullOrWhiteSpace(combinedDesc)
                    ? $"Materials: {mats}"
                    : $"{combinedDesc}\r\nMaterials: {mats}";
            }

            var tags = SplitTags(_txtTags.Text);
            var keyword = tags.Count > 0 ? tags[0] : title;
            var input = new ListingOptimizationInput(title, combinedDesc, tags, keyword);
            var res = await _aiOptimizer.OptimizeAsync(input);

            if (!string.IsNullOrWhiteSpace(res.DescriptionDraft))
            {
                var finalDesc = EtsyDescriptionFormatter.NormalizeForEtsy(res.DescriptionDraft);
                _txtDescription.Text = finalDesc;
                _statusLabel.Text = "Açıklama AI tarafından oluşturuldu ve standart Etsy şablonuna uyarlandı.";

                if (string.IsNullOrWhiteSpace(_txtMaterials.Text) && res.MaterialSuggestions.Count > 0)
                {
                    _txtMaterials.Text = string.Join(", ", res.MaterialSuggestions.Take(8));
                }
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
                _statusLabel.Text = "AI görseli başarıyla üretildi! 'Galerisine Ekle' butonuna basarak aktarabilirsiniz.";
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
        long readinessStateId = 0;
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

            readinessStateId = SelectedReadinessStateId();
            if (readinessStateId <= 0)
            {
                MessageBox.Show(this, "Fiziksel ürünler için Etsy geçerli bir Hazırlık Durumu (readiness_state_id) gerektirir. Lütfen Hazırlık Durumu alanından bir seçenek seçiniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                Dictionary<string, DraftListingVariationPricing>? customPricing = null;
                if (_chkCustomVariationPricing.Checked)
                {
                    customPricing = BuildCustomPricingForInventory();
                }

                inventory = new DraftListingInventoryUpdate(_numPrice.Value, (int)_numQuantity.Value, isDigital ? null : readinessStateId, variationGroups, customPricing);
            }
        }

        // 3. Confirmation Dialog
        string priceSummaryText = _chkEnableVariations.Checked && _chkCustomVariationPricing.Checked && _customVariationPrices.Count > 0
            ? _lblPriceRangeBadge.Text.Replace("📊 ", "")
            : $"{_numPrice.Value:0.00} USD | Stok: {_numQuantity.Value}";

        var confirmMsg =
            $"Etsy'de yeni listeleme oluşturulacak:\n\n" +
            $"• Başlık: {title}\n" +
            $"• Fiyatlandırma: {priceSummaryText}\n" +
            $"• Kategori ID: {taxonomyId}\n" +
            $"• Görseller: {_galleryImagePaths.Count} adet\n" +
            $"• Varyasyonlar: {(inventory?.Variations.Count > 0 ? $"{inventory.Variations.Count} grup ({GetCurrentVariationCombinationKeys().Count} seçenek)" : "Yok")}\n" +
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
            _btnHeaderPublish.Enabled = false;
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
                readinessStateId,
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
                    _statusLabel.Text = "Varyasyonlar ve özel fiyatlar ekleniyor...";
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
                        Debug.WriteLine($"Image upload error: {imgEx.Message}");
                    }
                }
            }

            // D. Activate Listing if requested
            bool isActuallyActive = false;
            if (_chkMakeActive.Checked)
            {
                _statusLabel.Text = "Listeleme canlı yayına alınıyor (Aktif yapılıyor)...";
                try
                {
                    await _apiClient.UpdateOwnShopListingStateAsync(settings, created.ListingId, "active");
                    EtsyApiSettingsStore.Save(settings);
                    isActuallyActive = true;
                }
                catch (Exception stateEx)
                {
                    MessageBox.Show(this, $"Listeleme ve görseller başarıyla yüklendi fakat canlı yayına alınırken Etsy hata verdi (Ürün taslak olarak kaydedildi):\n{stateEx.Message}", "Yayınlama Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            _statusLabel.Text = isActuallyActive
                ? $"Listeleme başarıyla oluşturuldu ve CANLI YAYINA ALINDI! (#{created.ListingId})"
                : $"Listeleme başarıyla TASLAK olarak oluşturuldu! (#{created.ListingId})";

            var openPrompt = MessageBox.Show(
                this,
                $"🎉 Tebrikler! Ürününüz Etsy'de başarıyla oluşturuldu!\n\n" +
                $"Listing ID: #{created.ListingId}\n" +
                $"Görsel Sayısı: {_galleryImagePaths.Count}\n" +
                $"Yayın Durumu: {(isActuallyActive ? "🚀 CANLI YAYINDA (Active)" : "💾 TASLAK (Draft)")}\n\n" +
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
            _btnHeaderPublish.Enabled = true;
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
            var (name1, propId1) = FastListingDraftHelper.ParseVariationType(_cboVarType1.SelectedItem?.ToString(), 1);
            groups.Add(new DraftListingVariationGroup(name1, propId1, v1Values));
        }

        // Group 2
        if (_chkEnableVar2.Checked)
        {
            var v2Values = SplitTags(_txtVarValues2.Text);
            if (v2Values.Count > 0)
            {
                var (name2, propId2) = FastListingDraftHelper.ParseVariationType(_cboVarType2.SelectedItem?.ToString(), 2);
                groups.Add(new DraftListingVariationGroup(name2, propId2, v2Values));
            }
        }

        return groups;
    }

    private static (string Name, long PropertyId) ParseVariationType(string? selected, int groupIndex = 1) =>
        FastListingDraftHelper.ParseVariationType(selected, groupIndex);

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
        _chkCustomVariationPricing.Checked = false;
        _customVariationPrices.Clear();
        _gridVariationPricing.Rows.Clear();
        RefreshGalleryCards();
        UpdateTagStatus();
        UpdateChecklist();
        _chkMakeActive.Checked = false;
        _statusLabel.Text = "Form temizlendi.";
    }

    // --- Template & Live Preview Operations ---

    private void LoadTemplatesCombo(string? selectId = null)
    {
        _cboTemplates.Items.Clear();
        var templates = FastListingTemplateStore.LoadAll();
        int selectedIndex = 0;

        for (int i = 0; i < templates.Count; i++)
        {
            _cboTemplates.Items.Add(templates[i]);
            if (selectId != null && templates[i].Id == selectId)
            {
                selectedIndex = i;
            }
        }

        if (_cboTemplates.Items.Count > 0)
        {
            _cboTemplates.SelectedIndex = selectedIndex;
        }
    }

    private void ApplySelectedTemplate()
    {
        if (_cboTemplates.SelectedItem is not FastListingTemplate tmpl) return;

        if (tmpl.DefaultPrice > 0) _numPrice.Value = tmpl.DefaultPrice;
        if (tmpl.DefaultQuantity > 0) _numQuantity.Value = tmpl.DefaultQuantity;
        _cboListingType.SelectedIndex = tmpl.IsDigital ? 1 : 0;

        if (!string.IsNullOrWhiteSpace(tmpl.DefaultTags)) _txtTags.Text = tmpl.DefaultTags;
        if (!string.IsNullOrWhiteSpace(tmpl.DefaultMaterials)) _txtMaterials.Text = tmpl.DefaultMaterials;
        if (!string.IsNullOrWhiteSpace(tmpl.DescriptionTemplate)) _txtDescription.Text = tmpl.DescriptionTemplate;

        _chkEnableVariations.Checked = tmpl.EnableVariations;
        if (tmpl.EnableVariations)
        {
            SelectVariationTypeInCombo(_cboVarType1, tmpl.VariationType1);
            _txtVarValues1.Text = tmpl.VariationValues1;

            _chkEnableVar2.Checked = tmpl.EnableVariation2;
            if (tmpl.EnableVariation2)
            {
                SelectVariationTypeInCombo(_cboVarType2, tmpl.VariationType2);
                _txtVarValues2.Text = tmpl.VariationValues2;
            }
        }
        else
        {
            _chkEnableVar2.Checked = false;
        }

        _customVariationPrices.Clear();
        if (tmpl.EnableCustomVariationPricing && tmpl.VariationPrices != null && tmpl.VariationPrices.Count > 0)
        {
            _chkCustomVariationPricing.Checked = true;
            foreach (var kvp in tmpl.VariationPrices)
            {
                _customVariationPrices[kvp.Key] = (kvp.Value, (int)_numQuantity.Value, true);
            }
            RefreshVariationPricingGrid();
        }
        else
        {
            _chkCustomVariationPricing.Checked = false;
            _pnlVariationPricing.Visible = false;
        }

        UpdateTagStatus();
        UpdateVariationsDisplay();
        UpdateChecklist();
        _statusLabel.Text = $"'{tmpl.Name}' şablonu başarıyla uygulandı.";
    }

    private void SaveCurrentAsTemplate()
    {
        var defaultName = !string.IsNullOrWhiteSpace(_txtTitle.Text)
            ? _txtTitle.Text.Split([' ', ',', '-'])[0] + " Şablonu"
            : "Yeni Ürün Şablonu";

        var name = ShowTextInputDialog(this, "Şablon Olarak Kaydet", "Yeni şablon için bir isim giriniz:", defaultName);
        if (string.IsNullOrWhiteSpace(name)) return;

        var tmpl = new FastListingTemplate
        {
            Name = name.Trim(),
            DefaultPrice = _numPrice.Value,
            DefaultQuantity = (int)_numQuantity.Value,
            IsDigital = _cboListingType.SelectedIndex == 1,
            DefaultTags = _txtTags.Text.Trim(),
            DefaultMaterials = _txtMaterials.Text.Trim(),
            DescriptionTemplate = _txtDescription.Text.Trim(),
            EnableVariations = _chkEnableVariations.Checked,
            VariationType1 = _cboVarType1.SelectedItem?.ToString() ?? "Boyut / Beden (Size - 100)",
            VariationValues1 = _txtVarValues1.Text.Trim(),
            EnableVariation2 = _chkEnableVar2.Checked,
            VariationType2 = _cboVarType2.SelectedItem?.ToString() ?? "Renk (Primary Color - 506)",
            VariationValues2 = _txtVarValues2.Text.Trim(),
            EnableCustomVariationPricing = _chkCustomVariationPricing.Checked,
            VariationPrices = _chkCustomVariationPricing.Checked
                ? _customVariationPrices.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Price)
                : []
        };

        FastListingTemplateStore.SaveCustom(tmpl);
        LoadTemplatesCombo(tmpl.Id);
        _statusLabel.Text = $"'{tmpl.Name}' şablonu kaydedildi.";
    }

    private void DeleteSelectedTemplate()
    {
        if (_cboTemplates.SelectedItem is not FastListingTemplate tmpl) return;

        if (tmpl.IsBuiltIn)
        {
            MessageBox.Show(this, "Yerleşik sistem şablonları silinemez.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, $"'{tmpl.Name}' şablonunu silmek istediğinize emin misiniz?", "Şablon Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            FastListingTemplateStore.DeleteCustom(tmpl.Id);
            LoadTemplatesCombo();
            _statusLabel.Text = $"'{tmpl.Name}' şablonu silindi.";
        }
    }

    private static void SelectVariationTypeInCombo(ComboBox cbo, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return;
        for (int i = 0; i < cbo.Items.Count; i++)
        {
            var s = cbo.Items[i]?.ToString() ?? "";
            if (s.Contains(targetName, StringComparison.OrdinalIgnoreCase) || targetName.Contains(s, StringComparison.OrdinalIgnoreCase))
            {
                cbo.SelectedIndex = i;
                return;
            }
        }
    }

    private void CleanAndFormatTags()
    {
        var sanitized = FastListingDraftHelper.SanitizeTags(_txtTags.Text);
        _txtTags.Text = string.Join(", ", sanitized);
        UpdateTagStatus();
        UpdateChecklist();
        _statusLabel.Text = $"{sanitized.Count} etiket Etsy standartlarına göre temizlendi (Maks 13 etiket, 20 karakter).";
    }

    private void UpdateTagStatus()
    {
        var rawTags = _txtTags.Text.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        var longTags = rawTags.Where(t => t.Length > 20).ToList();
        var duplicates = rawTags.GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (rawTags.Count == 0)
        {
            _lblTagCounter.Text = "0 / 13";
            _lblTagCounter.ForeColor = UiStyle.TextMuted;
            _lblTagStatus.Text = "Henüz etiket eklenmedi. En fazla 13 etiket ekleyebilirsiniz.";
            _lblTagStatus.ForeColor = UiStyle.TextMuted;
        }
        else if (longTags.Count > 0)
        {
            _lblTagCounter.Text = $"{rawTags.Count} / 13";
            _lblTagCounter.ForeColor = UiStyle.DangerColor;
            _lblTagStatus.Text = $"⚠️ {longTags.Count} etiket 20 karakter sınırını aşıyor! ('{longTags[0]}')";
            _lblTagStatus.ForeColor = UiStyle.DangerColor;
        }
        else if (duplicates.Count > 0)
        {
            _lblTagCounter.Text = $"{rawTags.Count} / 13";
            _lblTagCounter.ForeColor = UiStyle.WarningColor;
            _lblTagStatus.Text = $"⚠️ Yinelenen etiketler var: '{duplicates[0]}'";
            _lblTagStatus.ForeColor = UiStyle.WarningColor;
        }
        else
        {
            _lblTagCounter.Text = $"{rawTags.Count} / 13";
            _lblTagCounter.ForeColor = rawTags.Count == 13 ? UiStyle.SuccessColor : UiStyle.PrimaryColor;
            _lblTagStatus.Text = rawTags.Count == 13
                ? "Mükemmel! 13/13 etiket dolu ve Etsy kurallarına uygun ✅"
                : $"{rawTags.Count}/13 etiket kurallara uygun (Önerilen: 13)";
            _lblTagStatus.ForeColor = rawTags.Count == 13 ? UiStyle.SuccessColor : UiStyle.TextDark;
        }
    }

    private void OpenEtsyListingPreview()
    {
        var variations = new List<(string Name, IReadOnlyList<string> Values)>();
        if (_chkEnableVariations.Checked)
        {
            var v1 = SplitTags(_txtVarValues1.Text);
            if (v1.Count > 0)
            {
                var (n1, _) = ParseVariationType(_cboVarType1.SelectedItem?.ToString());
                variations.Add((n1, v1));
            }

            if (_chkEnableVar2.Checked)
            {
                var v2 = SplitTags(_txtVarValues2.Text);
                if (v2.Count > 0)
                {
                    var (n2, _) = ParseVariationType(_cboVarType2.SelectedItem?.ToString());
                    variations.Add((n2, v2));
                }
            }
        }

        Dictionary<string, decimal>? customPrices = null;
        if (_chkEnableVariations.Checked && _chkCustomVariationPricing.Checked)
        {
            customPrices = GetCustomPricesForPreview();
        }

        using var preview = new EtsyListingPreviewDialog(
            _txtTitle.Text.Trim(),
            _numPrice.Value,
            _txtDescription.Text,
            SplitTags(_txtTags.Text),
            SplitTags(_txtMaterials.Text),
            _galleryImagePaths,
            variations,
            customPrices);

        preview.ShowDialog(this);
    }

    private static string? ShowTextInputDialog(IWin32Window owner, string title, string prompt, string defaultText = "")
    {
        using var form = new Form
        {
            Text = title,
            Size = new Size(420, 180),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.CardBackground
        };

        var lbl = new Label
        {
            Text = prompt,
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = UiStyle.TextDark
        };
        var txt = new TextBox { Text = defaultText, Location = new Point(20, 45), Width = 360, Font = new Font("Segoe UI", 9.5f) };
        txt.SelectAll();

        var btnOk = new Button
        {
            Text = "Kaydet",
            DialogResult = DialogResult.OK,
            Location = new Point(210, 88),
            Size = new Size(85, 32),
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text = "İptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(302, 88),
            Size = new Size(78, 32),
            BackColor = UiStyle.SecondaryColor,
            ForeColor = UiStyle.TextDark,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = UiStyle.BorderColor;

        form.Controls.AddRange([lbl, txt, btnOk, btnCancel]);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
