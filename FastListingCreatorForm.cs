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
    private readonly Label _lblTagStatus = new() { AutoSize = true };
    private readonly TextBox _txtDescription = new() { Multiline = true, Height = 120, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _txtMaterials = new() { Height = 26 };

    // Template Toolbar Controls
    private readonly ComboBox _cboTemplates = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button _btnApplyTemplate = new();
    private readonly Button _btnSaveTemplate = new();
    private readonly Button _btnDeleteTemplate = new();

    // Primary Action Buttons
    private readonly ModernButtonControl _btnHeaderPreview = new();
    private readonly ModernButtonControl _btnHeaderPublish = new();
    private readonly Button _btnClearAll = new();

    // Center Column Controls (Gallery & AI Generator)
    private readonly FlowLayoutPanel _galleryFlow = new();
    private readonly Label _lblGalleryCount = new() { AutoSize = true };
    private readonly TextBox _txtAiPrompt = new() { Multiline = true, Height = 56, ScrollBars = ScrollBars.Vertical };
    private readonly PictureBox _picAiPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Height = 180 };
    private readonly ModernButtonControl _btnGenerateAi = new();
    private readonly ModernButtonControl _btnAddToGallery = new();

    // Right Column Controls (Variations & Publish)
    private readonly CheckBox _chkEnableVariations = new() { Text = "🎨 Bu ürüne varyasyon ekle (Boyut, Renk vb.)", AutoSize = true };
    private readonly ComboBox _cboVarType1 = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtVarValues1 = new() { Text = "Small, Medium, Large" };
    private readonly CheckBox _chkEnableVar2 = new() { Text = "➕ İkinci varyasyon grubu ekle", AutoSize = true };
    private readonly ComboBox _cboVarType2 = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtVarValues2 = new() { Text = "Siyah, Beyaz, Altın" };
    private readonly Label _lblVarCombinations = new() { AutoSize = true };

    // Custom Variation Pricing Controls
    private readonly CheckBox _chkCustomVariationPricing = new() { Text = "💲 Her varyasyona özel farklı fiyat & stok belirle", AutoSize = true };
    private readonly Panel _pnlVariationPricing = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false };
    private readonly DataGridView _gridVariationPricing = new();
    private readonly Label _lblPriceRangeBadge = new() { AutoSize = true };
    private readonly Button _btnSyncBasePrice = new();
    private readonly Button _btnStepPrice = new();
    private readonly Dictionary<string, (decimal Price, int Quantity, bool IsEnabled)> _customVariationPrices = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingVariationGrid;

    private readonly CheckBox _chkMakeActive = new() { Text = "🚀 Hemen Canlı Yayına Al (Aktif Yap)", AutoSize = true, Checked = false };
    private readonly ModernButtonControl _btnPublish = new();
    private readonly Button _btnPreviewSecondary = new();
    private readonly Label _statusLabel = new() { AutoSize = true };

    // Live Readiness Checklist Labels
    private readonly Label _chkItemTitle = new() { AutoSize = true };
    private readonly Label _chkItemPrice = new() { AutoSize = true };
    private readonly Label _chkItemImage = new() { AutoSize = true };
    private readonly Label _chkItemShipping = new() { AutoSize = true };
    private readonly Label _chkItemDesc = new() { AutoSize = true };
    private readonly Label _chkItemTags = new() { AutoSize = true };

    public FastListingCreatorForm(IAiListingOptimizer aiOptimizer)
    {
        _aiOptimizer = aiOptimizer;
        Text = "🛍️ Hızlı Ürün Ekle & AI Stüdyosu";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 740);
        UiStyle.ApplyTheme(this);

        BuildLayout();
        WireEvents();

        Shown += async (_, _) => await InitializeFormDataAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = UiStyle.BackgroundColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // Row 0: Modern Command Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // Row 1: Template Strip
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Row 2: 3 Responsive Columns
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // Row 3: Status bar
        Controls.Add(root);

        // Row 0: Top Command Header
        root.Controls.Add(BuildTopHeader(), 0, 0);

        // Row 1: Template Strip
        root.Controls.Add(BuildTemplateToolbar(), 0, 1);

        // Row 2: 3 Responsive Content Columns
        var contentGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 4, 0, 4)
        };
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); // Col 1: SEO & Details
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); // Col 2: Gallery & AI
        contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); // Col 3: Variations & Checklist

        contentGrid.Controls.Add(BuildLeftColumn(), 0, 0);
        contentGrid.Controls.Add(BuildCenterColumn(), 1, 0);
        contentGrid.Controls.Add(BuildRightColumn(), 2, 0);
        root.Controls.Add(contentGrid, 0, 2);

        // Row 3: Modern Status bar
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Font = new Font("Segoe UI", 9F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Text = "Hazır.";
        root.Controls.Add(_statusLabel, 0, 3);
    }

    private Control BuildTopHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        // Left: Branding & Subtitle
        var titleBox = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };

        var lblBadge = new Label
        {
            Text = "⚡ AI STUDIO",
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = UiStyle.PrimaryColor,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 8, 10, 0),
            AutoSize = true
        };
        titleBox.Controls.Add(lblBadge);

        var lblTitle = new Label
        {
            Text = "Hızlı Ürün Ekleme & AI Stüdyosu",
            Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 4, 12, 0)
        };
        titleBox.Controls.Add(lblTitle);

        var lblSubtitle = new Label
        {
            Text = "Yeni listeleme kurgulayın, AI ile görsel üretin ve Etsy'ye gönderin.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        titleBox.Controls.Add(lblSubtitle);

        header.Controls.Add(titleBox, 0, 0);

        // Right: Primary Actions Cluster
        var actionCluster = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        // 1. Etsy'ye Gönder
        _btnHeaderPublish.Text = "🚀 Etsy'ye Gönder";
        _btnHeaderPublish.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _btnHeaderPublish.Height = 34;
        _btnHeaderPublish.Width = 150;
        _btnHeaderPublish.NormalColor = UiStyle.SuccessColor;
        _btnHeaderPublish.HoverColor = Color.FromArgb(5, 150, 105);
        _btnHeaderPublish.ForeColor = Color.White;
        _btnHeaderPublish.Cursor = Cursors.Hand;
        _btnHeaderPublish.Margin = new Padding(4, 6, 0, 0);
        _btnHeaderPublish.Click += async (_, _) => await PublishListingToEtsyAsync();
        actionCluster.Controls.Add(_btnHeaderPublish);

        // 2. Canlı Önizleme
        _btnHeaderPreview.Text = "👁️ Canlı Önizleme";
        _btnHeaderPreview.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _btnHeaderPreview.Height = 34;
        _btnHeaderPreview.Width = 140;
        _btnHeaderPreview.NormalColor = UiStyle.PrimaryColor;
        _btnHeaderPreview.HoverColor = UiStyle.PrimaryHover;
        _btnHeaderPreview.ForeColor = Color.White;
        _btnHeaderPreview.Cursor = Cursors.Hand;
        _btnHeaderPreview.Margin = new Padding(4, 6, 0, 0);
        _btnHeaderPreview.Click += (_, _) => OpenEtsyListingPreview();
        actionCluster.Controls.Add(_btnHeaderPreview);

        // 3. Formu Temizle
        _btnClearAll.Text = "🔄 Temizle";
        _btnClearAll.Font = new Font("Segoe UI", 8.5F);
        _btnClearAll.Height = 34;
        _btnClearAll.AutoSize = true;
        _btnClearAll.BackColor = UiStyle.SecondaryColor;
        _btnClearAll.ForeColor = UiStyle.TextDark;
        _btnClearAll.FlatStyle = FlatStyle.Flat;
        _btnClearAll.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnClearAll.Cursor = Cursors.Hand;
        _btnClearAll.Margin = new Padding(4, 6, 0, 0);
        _btnClearAll.Click += (_, _) => ResetForm();
        actionCluster.Controls.Add(_btnClearAll);

        header.Controls.Add(actionCluster, 1, 0);
        return header;
    }

    private Control BuildTemplateToolbar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 0, 6)
        };

        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(UiStyle.BorderColor, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, bar.Width - 1, bar.Height - 1);
        };

        var flowLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        flowLeft.Controls.Add(new Label
        {
            Text = "📋 Hazır Şablon:",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0)
        });

        _cboTemplates.Font = new Font("Segoe UI", 9F);
        _cboTemplates.Width = 240;
        _cboTemplates.Margin = new Padding(0, 5, 6, 0);
        flowLeft.Controls.Add(_cboTemplates);

        _btnApplyTemplate.Text = "⚡ Uygula";
        _btnApplyTemplate.Font = new Font("Segoe UI Semibold", 8.5F);
        _btnApplyTemplate.Height = 28;
        _btnApplyTemplate.AutoSize = true;
        _btnApplyTemplate.BackColor = UiStyle.PrimaryColor;
        _btnApplyTemplate.ForeColor = Color.White;
        _btnApplyTemplate.FlatStyle = FlatStyle.Flat;
        _btnApplyTemplate.FlatAppearance.BorderSize = 0;
        _btnApplyTemplate.Cursor = Cursors.Hand;
        _btnApplyTemplate.Margin = new Padding(0, 4, 6, 0);
        _btnApplyTemplate.Click += (_, _) => ApplySelectedTemplate();
        flowLeft.Controls.Add(_btnApplyTemplate);

        _btnSaveTemplate.Text = "💾 Şablon Kaydet";
        _btnSaveTemplate.Font = new Font("Segoe UI", 8.5F);
        _btnSaveTemplate.Height = 28;
        _btnSaveTemplate.AutoSize = true;
        _btnSaveTemplate.BackColor = UiStyle.SecondaryColor;
        _btnSaveTemplate.ForeColor = UiStyle.TextDark;
        _btnSaveTemplate.FlatStyle = FlatStyle.Flat;
        _btnSaveTemplate.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnSaveTemplate.Cursor = Cursors.Hand;
        _btnSaveTemplate.Margin = new Padding(0, 4, 4, 0);
        _btnSaveTemplate.Click += (_, _) => SaveCurrentAsTemplate();
        flowLeft.Controls.Add(_btnSaveTemplate);

        _btnDeleteTemplate.Text = "🗑️";
        _btnDeleteTemplate.Font = new Font("Segoe UI", 8.5F);
        _btnDeleteTemplate.Height = 28;
        _btnDeleteTemplate.Width = 32;
        _btnDeleteTemplate.BackColor = UiStyle.SecondaryColor;
        _btnDeleteTemplate.ForeColor = UiStyle.DangerColor;
        _btnDeleteTemplate.FlatStyle = FlatStyle.Flat;
        _btnDeleteTemplate.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnDeleteTemplate.Cursor = Cursors.Hand;
        _btnDeleteTemplate.Margin = new Padding(0, 4, 0, 0);
        _btnDeleteTemplate.Click += (_, _) => DeleteSelectedTemplate();
        flowLeft.Controls.Add(_btnDeleteTemplate);

        var flowRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        flowRight.Controls.Add(new Label
        {
            Text = "💡 Şablonlar fiyat, kargo ve varyasyon ayarlarını saniyeler içinde otomatik doldurur.",
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 9, 4, 0)
        });

        bar.Controls.Add(flowLeft);
        bar.Controls.Add(flowRight);
        return bar;
    }

    private Control BuildLeftColumn()
    {
        var grp = new GroupBox
        {
            Text = "1. Ürün & SEO Bilgileri",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(0, 0, 5, 0)
        };

        var scrollContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 6, 0)
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        // 1. Temel Bilgiler (Ürün Tipi, Fiyat, Stok)
        var topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Height = 56,
            Margin = new Padding(0, 0, 0, 4)
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _cboListingType.Items.AddRange(["Fiziksel Ürün", "Dijital Ürün"]);
        _cboListingType.SelectedIndex = 0;
        _cboListingType.Dock = DockStyle.Fill;
        topRow.Controls.Add(CreateLabeledControl("Ürün Tipi:", _cboListingType), 0, 0);

        _numPrice.Dock = DockStyle.Fill;
        _numPrice.Font = new Font("Segoe UI Semibold", 9F);
        topRow.Controls.Add(CreateLabeledControl("Fiyat ($):", _numPrice), 1, 0);

        _numQuantity.Dock = DockStyle.Fill;
        _numQuantity.Font = new Font("Segoe UI Semibold", 9F);
        topRow.Controls.Add(CreateLabeledControl("Stok:", _numQuantity), 2, 0);
        stack.Controls.Add(topRow);

        // 2. Başlık & AI Öneri
        var titleContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Margin = new Padding(0, 2, 0, 4)
        };
        var titleHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        titleHeader.Controls.Add(new Label { Text = "Ürün Başlığı (Etsy): ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 2, 0, 0) });
        _lblTitleCounter.Text = "0 / 140";
        _lblTitleCounter.Font = new Font("Segoe UI", 8F);
        _lblTitleCounter.ForeColor = UiStyle.TextMuted;
        _lblTitleCounter.Margin = new Padding(0, 3, 0, 0);
        titleHeader.Controls.Add(_lblTitleCounter);

        var btnAiTitle = new Button
        {
            Text = "✨ AI Başlık Öner",
            Font = new Font("Segoe UI", 8F),
            Height = 24,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(12, 0, 0, 0),
        };
        btnAiTitle.FlatAppearance.BorderSize = 0;
        btnAiTitle.Click += async (_, _) => await SuggestAiTitleAsync();
        titleHeader.Controls.Add(btnAiTitle);

        _txtTitle.Dock = DockStyle.Bottom;
        _txtTitle.Font = new Font("Segoe UI", 9.5F);
        titleContainer.Controls.Add(titleHeader);
        titleContainer.Controls.Add(_txtTitle);
        stack.Controls.Add(titleContainer);

        // 3. Kategori & Taxonomy
        var catRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Height = 56,
            Margin = new Padding(0, 2, 0, 4)
        };
        catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

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
        stack.Controls.Add(catRow);

        // 4. Kargo Profili (Shipping Profile)
        _cboShippingProfile.Dock = DockStyle.Fill;
        _cboShippingProfile.DisplayMember = nameof(EtsyShippingProfileOption.DisplayName);
        var shipPanel = CreateLabeledControl("Kargo Profili (Shipping Profile):", _cboShippingProfile);
        shipPanel.Margin = new Padding(0, 2, 0, 4);
        stack.Controls.Add(shipPanel);

        // 5. Etiketler (Tags)
        var tagContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 126,
            Margin = new Padding(0, 2, 0, 4)
        };
        var tagHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        tagHeader.Controls.Add(new Label { Text = "Etiketler (Maks 13): ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 2, 0, 0) });
        _lblTagCounter.Text = "0 / 13";
        _lblTagCounter.Font = new Font("Segoe UI", 8F);
        _lblTagCounter.ForeColor = UiStyle.TextMuted;
        _lblTagCounter.Margin = new Padding(0, 3, 0, 0);
        tagHeader.Controls.Add(_lblTagCounter);

        var btnAiTags = new Button
        {
            Text = "✨ 13 AI Tag",
            Font = new Font("Segoe UI", 8F),
            Height = 24,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
        };
        btnAiTags.FlatAppearance.BorderSize = 0;
        btnAiTags.Click += async (_, _) => await SuggestAiTagsAsync();
        tagHeader.Controls.Add(btnAiTags);

        var btnCleanTags = new Button
        {
            Text = "🧹 Kırp & Düzelt",
            Font = new Font("Segoe UI", 8F),
            Height = 24,
            AutoSize = true,
            BackColor = UiStyle.SecondaryColor,
            ForeColor = UiStyle.TextDark,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0),
        };
        btnCleanTags.FlatAppearance.BorderColor = UiStyle.BorderColor;
        btnCleanTags.Click += (_, _) => CleanAndFormatTags();
        tagHeader.Controls.Add(btnCleanTags);

        _txtTags.Dock = DockStyle.Top;
        _txtTags.Height = 64;
        _txtTags.Font = new Font("Segoe UI", 9F);

        _lblTagStatus.Dock = DockStyle.Bottom;
        _lblTagStatus.Font = new Font("Segoe UI", 8F);
        _lblTagStatus.ForeColor = UiStyle.TextMuted;
        _lblTagStatus.Text = "Henüz etiket eklenmedi. En fazla 13 etiket ekleyebilirsiniz.";

        tagContainer.Controls.Add(_lblTagStatus);
        tagContainer.Controls.Add(_txtTags);
        tagContainer.Controls.Add(tagHeader);
        stack.Controls.Add(tagContainer);

        // 6. Ürün Açıklaması
        var descContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 160,
            Margin = new Padding(0, 2, 0, 4)
        };
        var descHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        descHeader.Controls.Add(new Label { Text = "Ürün Açıklaması: ", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 2, 0, 0) });

        var btnAiDesc = new Button
        {
            Text = "✨ AI Açıklama Üret",
            Font = new Font("Segoe UI", 8F),
            Height = 24,
            AutoSize = true,
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
        };
        btnAiDesc.FlatAppearance.BorderSize = 0;
        btnAiDesc.Click += async (_, _) => await SuggestAiDescriptionAsync();
        descHeader.Controls.Add(btnAiDesc);

        _txtDescription.Dock = DockStyle.Bottom;
        _txtDescription.Font = new Font("Segoe UI", 9F);
        descContainer.Controls.Add(descHeader);
        descContainer.Controls.Add(_txtDescription);
        stack.Controls.Add(descContainer);

        // 7. Malzemeler
        _txtMaterials.Dock = DockStyle.Fill;
        var matPanel = CreateLabeledControl("Kullanılan Malzemeler (Virgülle ayırın):", _txtMaterials);
        matPanel.Margin = new Padding(0, 2, 0, 8);
        stack.Controls.Add(matPanel);

        scrollContainer.Controls.Add(stack);
        grp.Controls.Add(scrollContainer);
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
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(4, 0, 4, 0)
        };

        var rootTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // Add Image Buttons
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 45));  // Gallery List
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 55));  // AI Generator Box

        // 1. Add Image Toolbar
        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 4)
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));

        var btnBrowse = new Button
        {
            Dock = DockStyle.Fill,
            Height = 32,
            Text = "📂 Bilgisayardan Seç",
            Font = new Font("Segoe UI Semibold", 8.5F),
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnBrowse.FlatAppearance.BorderSize = 0;
        btnBrowse.Click += (_, _) => BrowseLocalImages();
        topBar.Controls.Add(btnBrowse, 0, 0);

        var btnFromStudio = new Button
        {
            Dock = DockStyle.Fill,
            Height = 32,
            Text = "🖼️ Stüdyo Galerisi",
            Font = new Font("Segoe UI Semibold", 8.5F),
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnFromStudio.FlatAppearance.BorderSize = 0;
        btnFromStudio.Click += (_, _) => OpenStudioGalleryPicker();
        topBar.Controls.Add(btnFromStudio, 1, 0);

        _lblGalleryCount.Text = "0/10";
        _lblGalleryCount.Font = new Font("Segoe UI Semibold", 9.5F);
        _lblGalleryCount.ForeColor = UiStyle.TextDark;
        _lblGalleryCount.TextAlign = ContentAlignment.MiddleCenter;
        _lblGalleryCount.Dock = DockStyle.Fill;
        topBar.Controls.Add(_lblGalleryCount, 2, 0);
        rootTable.Controls.Add(topBar, 0, 0);

        // 2. Gallery Flow Panel
        _galleryFlow.Dock = DockStyle.Fill;
        _galleryFlow.AutoScroll = true;
        _galleryFlow.BackColor = UiStyle.CardBackground;
        _galleryFlow.BorderStyle = BorderStyle.FixedSingle;
        _galleryFlow.Padding = new Padding(6);
        rootTable.Controls.Add(_galleryFlow, 0, 1);

        // Initial render for empty gallery
        RefreshGalleryCards();

        // 3. AI Generator Box
        var aiBox = new GroupBox
        {
            Text = "✨ AI Görsel Stüdyosu (Doğrudan Üret & Ekle)",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.AiColor,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
            Margin = new Padding(0, 4, 0, 0)
        };

        var aiContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        var aiStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        // Preset Chips
        var chipsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        var promptPresets = new (string Label, string StyleKeyword)[]
        {
            ("☕ Masa/Kafe", "on a warm cozy wooden cafe table with morning sunlight, subtle steam, aesthetic atmosphere"),
            ("🛋️ Salon", "displayed in a stylish Scandinavian modern living room, neutral aesthetic, architectural interior"),
            ("📸 Stüdyo Beyaz", "isolated on a seamless pure white studio background, commercial softbox lighting, clean catalog"),
            ("🌿 Bohem Ahşap", "on rustic reclaimed wood with lush green indoor potted plants and bohemian vibes"),
            ("🎁 Hediye Paketi", "with luxury artisan kraft gift wrapping, elegant satin ribbon, greeting card")
        };

        foreach (var (pLabel, pStyle) in promptPresets)
        {
            var btnChip = new Button
            {
                Text = pLabel,
                Font = new Font("Segoe UI", 7.5F),
                Height = 24,
                AutoSize = true,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(1, 2, 3, 2)
            };
            btnChip.FlatAppearance.BorderColor = UiStyle.BorderColor;
            btnChip.Click += (_, _) =>
            {
                var prod = !string.IsNullOrWhiteSpace(_txtTitle.Text) ? _txtTitle.Text.Trim() : "Etsy product";
                _txtAiPrompt.Text = $"High quality commercial product photography of {prod}, {pStyle}, professional 8k catalog shot, sharp details, realistic textures";
            };
            chipsPanel.Controls.Add(btnChip);
        }
        aiStack.Controls.Add(chipsPanel);

        // Prompt input row
        var promptRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Height = 56,
            Margin = new Padding(0, 0, 0, 4)
        };
        promptRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        promptRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));

        _txtAiPrompt.Dock = DockStyle.Fill;
        _txtAiPrompt.Font = new Font("Segoe UI", 8.5F);
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
        btnPromptFromTitle.FlatAppearance.BorderColor = UiStyle.BorderColor;
        btnPromptFromTitle.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                _txtAiPrompt.Text = $"Professional commercial product photography of {_txtTitle.Text.Trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase";
            }
        };
        promptRow.Controls.Add(btnPromptFromTitle, 1, 0);
        aiStack.Controls.Add(promptRow);

        // Guaranteed Height AI Preview Box
        _picAiPreview.Dock = DockStyle.Top;
        _picAiPreview.Height = 175;
        _picAiPreview.BackColor = UiStyle.CardBackground;
        _picAiPreview.BorderStyle = BorderStyle.FixedSingle;
        _picAiPreview.Paint += (s, e) =>
        {
            if (_picAiPreview.Image == null)
            {
                using var font = new Font("Segoe UI", 8.5F);
                using var brush = new SolidBrush(UiStyle.TextMuted);
                var text = "🎨 AI Görsel Önizleme\nPrompt yazıp 'Görseli Üret' butonuna basın.";
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(text, font, brush, _picAiPreview.ClientRectangle, sf);
            }
        };
        aiStack.Controls.Add(_picAiPreview);

        // AI Buttons
        var aiActionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Height = 36,
            Margin = new Padding(0, 4, 0, 0)
        };
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        aiActionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _btnGenerateAi.Dock = DockStyle.Fill;
        _btnGenerateAi.Text = "🎨 Görseli Üret";
        _btnGenerateAi.NormalColor = UiStyle.AiColor;
        _btnGenerateAi.HoverColor = UiStyle.AiHover;
        _btnGenerateAi.ForeColor = Color.White;
        _btnGenerateAi.Margin = new Padding(2);
        _btnGenerateAi.Click += async (_, _) => await GenerateAiImageAsync();
        aiActionRow.Controls.Add(_btnGenerateAi, 0, 0);

        _btnAddToGallery.Dock = DockStyle.Fill;
        _btnAddToGallery.Text = "➕ Galerisine Ekle";
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
        aiStack.Controls.Add(aiActionRow);

        aiContainer.Controls.Add(aiStack);
        aiBox.Controls.Add(aiContainer);
        rootTable.Controls.Add(aiBox, 0, 2);

        grp.Controls.Add(rootTable);
        return grp;
    }

    private Control BuildRightColumn()
    {
        var grp = new GroupBox
        {
            Text = "3. Varyasyonlar & Kontrol Listesi",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(5, 0, 0, 0)
        };

        var scrollContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 6, 0)
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        // 1. Variations Box
        var varBox = new GroupBox
        {
            Text = "Ürün Varyasyonları (Seçenekler)",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true
        };
        var varTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true
        };

        varTable.Controls.Add(_chkEnableVariations);

        var lblVar1 = new Label { Text = "1. Varyasyon Tipi:", AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 6, 0, 2) };
        varTable.Controls.Add(lblVar1);

        _cboVarType1.Items.AddRange([
            "📏 Boyut / Size (100)",
            "🎨 Renk / Primary Color (506)",
            "🪵 Malzeme / Material (507)",
            "✨ Stil / Style (514)",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType1.SelectedIndex = 0;
        _cboVarType1.Dock = DockStyle.Top;
        varTable.Controls.Add(_cboVarType1);

        _txtVarValues1.Dock = DockStyle.Top;
        _txtVarValues1.Margin = new Padding(0, 4, 0, 6);
        varTable.Controls.Add(_txtVarValues1);

        varTable.Controls.Add(_chkEnableVar2);

        _cboVarType2.Items.AddRange([
            "🎨 Renk / Color (506)",
            "📏 Boyut / Size (100)",
            "🪵 Malzeme / Material (507)",
            "✨ Stil / Style (514)",
            "⚙️ Özel / Custom..."
        ]);
        _cboVarType2.SelectedIndex = 0;
        _cboVarType2.Dock = DockStyle.Top;
        _cboVarType2.Visible = false;
        varTable.Controls.Add(_cboVarType2);

        _txtVarValues2.Dock = DockStyle.Top;
        _txtVarValues2.Visible = false;
        _txtVarValues2.Margin = new Padding(0, 4, 0, 6);
        varTable.Controls.Add(_txtVarValues2);

        _lblVarCombinations.Dock = DockStyle.Top;
        _lblVarCombinations.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        _lblVarCombinations.ForeColor = UiStyle.TextMuted;
        _lblVarCombinations.Text = "Varyasyon kapalı.";
        _lblVarCombinations.Margin = new Padding(0, 4, 0, 4);
        varTable.Controls.Add(_lblVarCombinations);

        // Custom Variation Pricing Toggle & Grid
        _chkCustomVariationPricing.Margin = new Padding(0, 6, 0, 4);
        varTable.Controls.Add(_chkCustomVariationPricing);
        varTable.Controls.Add(BuildVariationPricingPanel());

        varBox.Controls.Add(varTable);
        stack.Controls.Add(varBox);

        // 2. Canlı Hazırlık Kontrol Listesi & Yayınlama Kutusu
        var pubBox = new GroupBox
        {
            Text = "Listeleme Hazırlık & Dağıtım",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            AutoSize = true
        };

        var pubTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true
        };

        // Checklist items
        var chkHeader = new Label
        {
            Text = "📋 Canlı Kontrol Listesi:",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        pubTable.Controls.Add(chkHeader);

        InitChecklistLabel(_chkItemTitle, "Ürün başlığı hazır");
        InitChecklistLabel(_chkItemPrice, "Fiyat ve stok geçerli");
        InitChecklistLabel(_chkItemImage, "En az 1 görsel eklendi");
        InitChecklistLabel(_chkItemShipping, "Kargo profili seçildi");
        InitChecklistLabel(_chkItemDesc, "Açıklama dolduruldu");
        InitChecklistLabel(_chkItemTags, "Etiketler (Tag) hazır");

        pubTable.Controls.Add(_chkItemTitle);
        pubTable.Controls.Add(_chkItemPrice);
        pubTable.Controls.Add(_chkItemImage);
        pubTable.Controls.Add(_chkItemShipping);
        pubTable.Controls.Add(_chkItemDesc);
        pubTable.Controls.Add(_chkItemTags);

        // Checkbox Active
        _chkMakeActive.Margin = new Padding(0, 10, 0, 6);
        pubTable.Controls.Add(_chkMakeActive);

        // Secondary Publish Button
        _btnPublish.Dock = DockStyle.Top;
        _btnPublish.Height = 40;
        _btnPublish.Text = "🚀 Etsy'ye Gönder (Yayınla)";
        _btnPublish.NormalColor = UiStyle.SuccessColor;
        _btnPublish.HoverColor = Color.FromArgb(5, 150, 105);
        _btnPublish.ForeColor = Color.White;
        _btnPublish.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _btnPublish.Click += async (_, _) => await PublishListingToEtsyAsync();
        pubTable.Controls.Add(_btnPublish);

        // Secondary Preview Button
        _btnPreviewSecondary.Dock = DockStyle.Top;
        _btnPreviewSecondary.Height = 30;
        _btnPreviewSecondary.Text = "👁️ Canlı Önizleme Yap";
        _btnPreviewSecondary.Font = new Font("Segoe UI", 8.5F);
        _btnPreviewSecondary.BackColor = UiStyle.SecondaryColor;
        _btnPreviewSecondary.ForeColor = UiStyle.TextDark;
        _btnPreviewSecondary.FlatStyle = FlatStyle.Flat;
        _btnPreviewSecondary.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnPreviewSecondary.Cursor = Cursors.Hand;
        _btnPreviewSecondary.Margin = new Padding(0, 4, 0, 6);
        _btnPreviewSecondary.Click += (_, _) => OpenEtsyListingPreview();
        pubTable.Controls.Add(_btnPreviewSecondary);

        var lblNote = new Label
        {
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            Text = "İpucu: 'Canlı Yayına Al' işaretlenmezse listeleme güvenli şekilde TASLAK (Draft) olarak açılır.",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        pubTable.Controls.Add(lblNote);

        pubBox.Controls.Add(pubTable);
        stack.Controls.Add(pubBox);

        scrollContainer.Controls.Add(stack);
        grp.Controls.Add(scrollContainer);
        return grp;
    }

    private Control BuildVariationPricingPanel()
    {
        _pnlVariationPricing.Padding = new Padding(0, 2, 0, 4);

        var topFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 28,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4)
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

        _pnlVariationPricing.Controls.Add(topFlow);

        // Grid setup
        _gridVariationPricing.Dock = DockStyle.Top;
        _gridVariationPricing.Height = 150;
        _gridVariationPricing.BackgroundColor = UiStyle.CardBackground;
        _gridVariationPricing.GridColor = UiStyle.BorderColor;
        _gridVariationPricing.BorderStyle = BorderStyle.FixedSingle;
        _gridVariationPricing.RowHeadersVisible = false;
        _gridVariationPricing.AllowUserToAddRows = false;
        _gridVariationPricing.AllowUserToDeleteRows = false;
        _gridVariationPricing.AllowUserToResizeRows = false;
        _gridVariationPricing.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridVariationPricing.Font = new Font("Segoe UI", 8F);
        _gridVariationPricing.EnableHeadersVisualStyles = false;
        _gridVariationPricing.ColumnHeadersDefaultCellStyle.BackColor = UiStyle.SecondaryColor;
        _gridVariationPricing.ColumnHeadersDefaultCellStyle.ForeColor = UiStyle.TextDark;
        _gridVariationPricing.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F);
        _gridVariationPricing.DefaultCellStyle.BackColor = UiStyle.CardBackground;
        _gridVariationPricing.DefaultCellStyle.ForeColor = UiStyle.TextDark;
        _gridVariationPricing.DefaultCellStyle.SelectionBackColor = UiStyle.PrimaryColor;
        _gridVariationPricing.DefaultCellStyle.SelectionForeColor = Color.White;

        _gridVariationPricing.Columns.Clear();
        var colKey = new DataGridViewTextBoxColumn
        {
            Name = "ColKey",
            HeaderText = "Seçenek",
            ReadOnly = true,
            FillWeight = 46
        };
        var colPrice = new DataGridViewTextBoxColumn
        {
            Name = "ColPrice",
            HeaderText = "Fiyat ($)",
            FillWeight = 26
        };
        var colQty = new DataGridViewTextBoxColumn
        {
            Name = "ColQty",
            HeaderText = "Stok",
            FillWeight = 16
        };
        var colActive = new DataGridViewCheckBoxColumn
        {
            Name = "ColActive",
            HeaderText = "Aktif",
            FillWeight = 12
        };

        _gridVariationPricing.Columns.AddRange([colKey, colPrice, colQty, colActive]);
        _pnlVariationPricing.Controls.Add(_gridVariationPricing);

        _lblPriceRangeBadge.Dock = DockStyle.Top;
        _lblPriceRangeBadge.Font = new Font("Segoe UI Semibold", 8F);
        _lblPriceRangeBadge.ForeColor = UiStyle.PrimaryColor;
        _lblPriceRangeBadge.Text = "📊 Fiyat Aralığı: Belirlenmedi";
        _lblPriceRangeBadge.Margin = new Padding(0, 4, 0, 4);
        _pnlVariationPricing.Controls.Add(_lblPriceRangeBadge);

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
            Dock = DockStyle.Top,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        panel.Controls.Add(new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill
        }, 0, 0);

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
            UpdateChecklist();
        };

        _numPrice.ValueChanged += (_, _) =>
        {
            _btnSyncBasePrice.Text = $"⚡ Eşitle (${_numPrice.Value:0.00})";
            UpdateChecklist();
        };
        _numQuantity.ValueChanged += (_, _) => UpdateChecklist();
        _cboShippingProfile.SelectedIndexChanged += (_, _) => UpdateChecklist();
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
            if (_chkEnableVariations.Checked && _chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
            UpdateChecklist();
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
        };

        _txtVarValues1.TextChanged += (_, _) =>
        {
            UpdateVariationsDisplay();
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
        };

        _txtVarValues2.TextChanged += (_, _) =>
        {
            UpdateVariationsDisplay();
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
        };

        _chkCustomVariationPricing.CheckedChanged += (_, _) =>
        {
            _pnlVariationPricing.Visible = _chkEnableVariations.Checked && _chkCustomVariationPricing.Checked;
            if (_chkCustomVariationPricing.Checked)
            {
                RefreshVariationPricingGrid();
            }
            UpdateChecklist();
        };

        _gridVariationPricing.CellValueChanged += (_, _) => OnVariationGridCellValueChanged();
        _gridVariationPricing.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_gridVariationPricing.IsCurrentCellDirty)
            {
                _gridVariationPricing.CommitEdit(DataGridViewDataErrorContexts.Commit);
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

        // 4. Shipping Profile
        bool isDigital = _cboListingType.SelectedIndex == 1;
        bool shippingOk = isDigital || _cboShippingProfile.SelectedItem != null;
        SetChecklistItem(_chkItemShipping, isDigital ? "Dijital Ürün (Kargo gerekmez)" : "Kargo profili seçildi", shippingOk);

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

            decimal price = decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : _numPrice.Value;
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
            var emptyPanel = new Panel
            {
                Width = 260,
                Height = 110,
                Margin = new Padding(12, 16, 12, 16),
                BackColor = Color.Transparent
            };

            var lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📷 Henüz görsel eklenmedi\n\nBilgisayardan dosya seçebilir veya\naşağıdaki AI stüdyosundan üretebilirsiniz.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiStyle.TextMuted
            };
            emptyPanel.Controls.Add(lblEmpty);
            _galleryFlow.Controls.Add(emptyPanel);
            return;
        }

        for (int i = 0; i < _galleryImagePaths.Count; i++)
        {
            var index = i;
            var path = _galleryImagePaths[i];

            var card = new Panel
            {
                Width = 96,
                Height = 136,
                Margin = new Padding(4),
                BackColor = UiStyle.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
            };

            var pic = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 82,
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
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextDark,
                Enabled = index > 0,
            };
            btnStar.FlatAppearance.BorderSize = 0;
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
                BackColor = UiStyle.SecondaryColor,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (_, _) =>
            {
                _galleryImagePaths.RemoveAt(index);
                RefreshGalleryCards();
                UpdateChecklist();
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
                Dictionary<string, DraftListingVariationPricing>? customPricing = null;
                if (_chkCustomVariationPricing.Checked)
                {
                    customPricing = BuildCustomPricingForInventory();
                }

                inventory = new DraftListingInventoryUpdate(_numPrice.Value, (int)_numQuantity.Value, null, variationGroups, customPricing);
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
        _chkCustomVariationPricing.Checked = false;
        _customVariationPrices.Clear();
        _gridVariationPricing.Rows.Clear();
        RefreshGalleryCards();
        UpdateTagStatus();
        UpdateChecklist();
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
