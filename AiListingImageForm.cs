namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Services;
using SimilarProductsWinForms.Studio.UI;

internal sealed class AiListingImageForm : Form
{
    private readonly IAiListingOptimizer? _aiOptimizer;
    private readonly PhotoRoomSettings _photoRoomSettings;
    private AiOptimizationSettings _aiSettings;
    private readonly ImageSessionManager _sessionManager = new();

    // Listing Integration
    private long? _targetListingId;
    private string? _targetListingTitle;
    private EtsyApiClient? _apiClient;

    // UI: Header Controls
    private readonly Label _lblAiBadge = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.FromArgb(30, 41, 59),
        Padding = new Padding(10, 5, 10, 5),
        Cursor = Cursors.Hand,
        Anchor = AnchorStyles.Right,
        Margin = new Padding(0, 0, 8, 0)
    };
    private readonly Label _statusLabel = new() { UseMnemonic = false };
    private readonly Button _btnModeSlider = UiStyle.CreateButton("↔️ Split Perde", isSecondary: false);
    private readonly Button _btnModeSideBySide = UiStyle.CreateButton("⫴ Yan Yana", isSecondary: true);
    private readonly Button _btnModeAfterOnly = UiStyle.CreateButton("🖼️ Sadece Sonuç", isSecondary: true);

    // UI: Left Panel Inputs & Presets
    private readonly ComboBox _cboEngine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernImageDropZone _dropZone = new();
    private readonly TextBox _productTitleTxt = new();
    private readonly PresetChipSelector _presetChips = new();
    private readonly TextBox _promptTxt = new() { Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };
    private readonly Button _btnSmartPrompt = new();
    private readonly StudioLightingSelectorControl _lightingSelector = new();
    private readonly Panel _engineOptionsContainer = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly FlowLayoutPanel _engineOptionsPanel = new() { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
    private readonly ComboBox _photoRoomModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _shadowComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _paddingComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _openAiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkOpenAiTransparentBg = new() { Text = "Şeffaf Arka Plan (Transparent PNG)", AutoSize = true, ForeColor = Color.White };
    private readonly ComboBox _bflModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtIdeogramTypography = new();
    private readonly ComboBox _ideogramStyleComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _geminiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _geminiEditModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernButtonControl _btnProcess = new();
    private readonly ModernButtonControl _btnBatchProcess = new();

    // UI: Center Canvas & History
    private readonly ModernBeforeAfterSlider _sliderControl = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _filmstripPanel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };

    // UI: Right Panel Marketing & Export
    private readonly CheckBox _chkEnableBadge = new() { Text = "Pazarlama Rozeti Ekle", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), ForeColor = Color.White };
    private readonly ComboBox _cboBadgeText = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _cboBadgePosition = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _formatComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboEtsyImageSlot = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblTargetListingInfo = new() { AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 8.5F) };

    public AiListingImageForm(IAiListingOptimizer? aiOptimizer = null, string? initialImagePath = null, string? initialTitle = null)
    {
        _aiOptimizer = aiOptimizer;
        _photoRoomSettings = PhotoRoomSettingsStore.Load();
        _aiSettings = AiOptimizationSettingsStore.Load();

        if (string.IsNullOrWhiteSpace(_photoRoomSettings.ApiKey) && !string.IsNullOrWhiteSpace(_aiSettings.PhotoRoomApiKey))
        {
            _photoRoomSettings.ApiKey = _aiSettings.PhotoRoomApiKey;
            PhotoRoomSettingsStore.Save(_photoRoomSettings);
        }
        else if (!string.IsNullOrWhiteSpace(_photoRoomSettings.ApiKey) && string.IsNullOrWhiteSpace(_aiSettings.PhotoRoomApiKey))
        {
            _aiSettings.PhotoRoomApiKey = _photoRoomSettings.ApiKey;
            AiOptimizationSettingsStore.Save(_aiSettings);
        }

        KeyPreview = true;
        BuildLayout();
        LoadSettings();

        _ = PersistentStudioGalleryService.InitializeAsync();

        if (!string.IsNullOrWhiteSpace(initialImagePath) && File.Exists(initialImagePath))
        {
            LoadImageFromPath(initialImagePath);
        }

        if (!string.IsNullOrWhiteSpace(initialTitle))
        {
            _productTitleTxt.Text = initialTitle;
            OnScenePresetSelected(_presetChips.SelectedPreset);
        }
    }

    public AiListingImageForm(object? listing, object? apiClient) : this(null)
    {
        _apiClient = apiClient as EtsyApiClient;

        if (listing is MarketListingResult mlr)
        {
            _targetListingId = mlr.ListingId;
            _targetListingTitle = mlr.Title;
            _productTitleTxt.Text = mlr.Title;
            _lblTargetListingInfo.Text = $"🎯 Bağlı Listing: #{mlr.ListingId} ({mlr.PriceDisplay})";
            _lblTargetListingInfo.ForeColor = UiStyle.SuccessColor;

            if (mlr.ThumbnailImage is Bitmap bmp)
            {
                SetLoadedBitmap(bmp, $"Listing #{mlr.ListingId} Kapak");
            }
            else if (!string.IsNullOrWhiteSpace(mlr.ImageUrl))
            {
                _ = LoadImageFromUrlAsync(mlr.ImageUrl);
            }
            else if (mlr.ImageUrls.Count > 0)
            {
                _ = LoadImageFromUrlAsync(mlr.ImageUrls[0]);
            }
        }
        else if (listing != null)
        {
            var titleProp = listing.GetType().GetProperty("Title");
            if (titleProp != null)
            {
                _productTitleTxt.Text = titleProp.GetValue(listing)?.ToString() ?? "";
            }
        }
    }

    private void BuildLayout()
    {
        Text = "AI Görsel Studio & Mockup Üretici (PhotoRoom / Gemini Imagen / DALL-E)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 820);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14, 10, 14, 10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        // 1. Header Toolbar
        root.Controls.Add(BuildHeaderBar(), 0, 0);

        // 2. Main 3-Column Content Split
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 4, 0, 4) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

        content.Controls.Add(BuildLeftControlsPanel(), 0, 0);
        content.Controls.Add(BuildCenterCanvasPanel(), 1, 0);
        content.Controls.Add(BuildRightActionsPanel(), 2, 0);
        root.Controls.Add(content, 0, 1);

        // 3. Bottom Bar
        root.Controls.Add(BuildBottomBar(), 0, 2);
    }

    private Control BuildHeaderBar()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        // Title & Subtitle
        var titleStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titleStack.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "📸 AI Görsel Studio & Mockup Üretici",
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            ForeColor = Color.White,
            UseMnemonic = false
        });
        titleStack.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "PhotoRoom, Google Gemini Imagen 3 ve OpenAI DALL-E ile profesyonel stüdyo mockup sahneleme",
            Font = new Font("Segoe UI", 8.8F),
            ForeColor = UiStyle.TextMuted,
            UseMnemonic = false
        });
        header.Controls.Add(titleStack, 0, 0);

        // Mode Switch Buttons
        var modeStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 8, 12, 0) };
        _btnModeSlider.Height = 32;
        _btnModeSlider.Click += (_, _) => SetComparisonMode(ImageComparisonMode.SplitSlider);
        modeStack.Controls.Add(_btnModeSlider);

        _btnModeSideBySide.Height = 32;
        _btnModeSideBySide.Click += (_, _) => SetComparisonMode(ImageComparisonMode.SideBySide);
        modeStack.Controls.Add(_btnModeSideBySide);

        _btnModeAfterOnly.Height = 32;
        _btnModeAfterOnly.Click += (_, _) => SetComparisonMode(ImageComparisonMode.AfterOnly);
        modeStack.Controls.Add(_btnModeAfterOnly);
        header.Controls.Add(modeStack, 1, 0);

        // AI Engine Status Badge
        _lblAiBadge.Click += (_, _) => OpenAiSettingsDialog();
        UpdateAiBadge();
        header.Controls.Add(_lblAiBadge, 2, 0);

        // Live Status Text
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Text = "Hazır. Görsel yükleyebilir veya bir sahne seçebilirsiniz.";
        header.Controls.Add(_statusLabel, 3, 0);

        return header;
    }

    private Control BuildLeftControlsPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            Padding = new Padding(14),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        // 1. AI Engine Selector
        stack.Controls.Add(new Label
        {
            Text = "🤖 İşlem Yapacak AI Motoru:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        });

        _cboEngine.Width = 340;
        _cboEngine.Font = new Font("Segoe UI", 9.5F);
        _cboEngine.Items.AddRange([
            "🔵 Google Gemini (Görsel Düzenleme & Sahneleme - SOTA)",
            "🟣 PhotoRoom Native (Arka Plan Silme & AI Gölge)",
            "🏆 OpenAI (GPT Image 2 / DALL-E 3)",
            "⚡ Black Forest Labs FLUX.1 (Ultra Realism)",
            "🟡 Ideogram 4.0 (Kusursuz Tipografi & Yazı)"
        ]);
        _cboEngine.SelectedIndex = 0;
        _cboEngine.SelectedIndexChanged += (_, _) => OnEngineSelectionChanged();
        stack.Controls.Add(_cboEngine);

        // 2. Drop Zone & Image Picker
        stack.Controls.Add(new Label
        {
            Text = "📷 Ürün Görsel Kaynağı:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 8, 0, 4)
        });

        _dropZone.Width = 340;
        _dropZone.ImageSelected += (_, args) =>
        {
            SetLoadedBitmap(args.Bitmap, args.FilePath != null ? Path.GetFileName(args.FilePath) : "Panodan Yapıştırılan Görsel");
        };
        stack.Controls.Add(_dropZone);

        // Quick Browse / Shop buttons
        var btnGrid = new TableLayoutPanel { Width = 340, Height = 34, Margin = new Padding(0, 6, 0, 6), ColumnCount = 2 };
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var loadLocalBtn = UiStyle.CreateButton("📁 PC'den Dosya Seç", isSecondary: true);
        loadLocalBtn.Dock = DockStyle.Fill;
        loadLocalBtn.Click += (_, _) => SelectProductImage();
        btnGrid.Controls.Add(loadLocalBtn, 0, 0);

        var loadShopBtn = UiStyle.CreateButton("🛍️ Mağazamdan Seç", isSecondary: true);
        loadShopBtn.Dock = DockStyle.Fill;
        loadShopBtn.Click += (_, _) => PickImageFromShopListings();
        btnGrid.Controls.Add(loadShopBtn, 1, 0);
        stack.Controls.Add(btnGrid);

        // 3. Product Title
        stack.Controls.Add(new Label
        {
            Text = "Ürün Adı / Konsepti:",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 2),
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 9F)
        });

        _productTitleTxt.Width = 340;
        _productTitleTxt.Font = new Font("Segoe UI", 9.2F);
        _productTitleTxt.PlaceholderText = "Örn: Handcrafted Ceramic Coffee Mug";
        _productTitleTxt.TextChanged += (_, _) => OnScenePresetSelected(_presetChips.SelectedPreset);
        stack.Controls.Add(_productTitleTxt);

        // 4. Preset Chips
        stack.Controls.Add(new Label
        {
            Text = "🪄 Etsy Sahneleme Preseti Seçin:",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4),
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 9F)
        });

        _presetChips.Width = 340;
        _presetChips.SetPresets([
            ("rustic", "Ahşap Rustic", "🪵"),
            ("gamer", "RGB Gamer", "🎮"),
            ("marble", "Lüks Mermer", "🏛️"),
            ("boho", "Boho Botanik", "🌿"),
            ("white", "Beyaz Stüdyo", "⚪"),
            ("festive", "Sıcak Noel", "🎄"),
            ("custom", "Özel Prompt", "✍️")
        ]);
        _presetChips.SelectedPresetChanged += (_, presetId) => OnScenePresetSelected(presetId);
        stack.Controls.Add(_presetChips);

        // 5. Prompt Box with Smart Prompt AI Button
        var promptHeader = new TableLayoutPanel { Width = 340, Height = 28, Margin = new Padding(0, 8, 0, 2), ColumnCount = 2 };
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        promptHeader.Controls.Add(new Label
        {
            Text = "AI Sahne Promptu:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 8.8F)
        }, 0, 0);

        _btnSmartPrompt.Text = "✨ AI ile Prompt Yaz";
        _btnSmartPrompt.Dock = DockStyle.Fill;
        _btnSmartPrompt.FlatStyle = FlatStyle.Flat;
        _btnSmartPrompt.FlatAppearance.BorderSize = 0;
        _btnSmartPrompt.BackColor = Color.FromArgb(99, 102, 241);
        _btnSmartPrompt.ForeColor = Color.White;
        _btnSmartPrompt.Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold);
        _btnSmartPrompt.Cursor = Cursors.Hand;
        _btnSmartPrompt.Click += async (_, _) => await GenerateSmartPromptAsync();
        promptHeader.Controls.Add(_btnSmartPrompt, 1, 0);
        stack.Controls.Add(promptHeader);

        _promptTxt.Width = 340;
        _promptTxt.Font = new Font("Segoe UI", 9F);
        stack.Controls.Add(_promptTxt);

        // 6. Phase 2: Studio Lighting & Camera Selector Control
        _lightingSelector.Width = 340;
        _lightingSelector.Margin = new Padding(0, 6, 0, 6);
        _lightingSelector.SettingsChanged += (_, _) => OnLightingSettingsChanged();
        stack.Controls.Add(_lightingSelector);

        // 7. Dynamic Engine Specific Controls Container
        _engineOptionsContainer.Width = 340;
        _engineOptionsContainer.Controls.Add(_engineOptionsPanel);
        stack.Controls.Add(_engineOptionsContainer);
        BuildEngineSpecificControls();

        // 8. Action Buttons (Single & Phase 2 Batch)
        _btnProcess.Width = 340;
        _btnProcess.Height = 42;
        _btnProcess.Text = "🚀 Seçili AI ile Görseli Üret / İşle";
        _btnProcess.NormalColor = UiStyle.PrimaryColor;
        _btnProcess.HoverColor = UiStyle.PrimaryHover;
        _btnProcess.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _btnProcess.Margin = new Padding(0, 10, 0, 6);
        _btnProcess.Click += async (_, _) => await ProcessImageWithSelectedEngineAsync();
        stack.Controls.Add(_btnProcess);

        _btnBatchProcess.Width = 340;
        _btnBatchProcess.Height = 38;
        _btnBatchProcess.Text = "🎯 4'lü Sahne Toplu Üret (Batch)";
        _btnBatchProcess.NormalColor = Color.FromArgb(30, 41, 59);
        _btnBatchProcess.HoverColor = Color.FromArgb(45, 55, 75);
        _btnBatchProcess.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
        _btnBatchProcess.Margin = new Padding(0, 0, 0, 12);
        _btnBatchProcess.Click += async (_, _) => await RunBatchSceneGenerationAsync();
        stack.Controls.Add(_btnBatchProcess);

        card.Controls.Add(stack);
        return card;
    }

    private Control BuildCenterCanvasPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6, 0, 6, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));

        // 1. Interactive Slider Control
        panel.Controls.Add(_sliderControl, 0, 0);

        // 2. Generation History Filmstrip Card
        var historyCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 6, 0, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var historyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        headerRow.Controls.Add(new Label
        {
            Text = "🎞️ Oturum Varyasyon Geçmişi (Tek tıkla geri dön):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8.2F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var btnOpenGallery = new Label
        {
            Text = "📚 Kalıcı Galeriyi Aç (50+) ↗",
            Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(129, 140, 248),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        btnOpenGallery.Click += (_, _) => OpenPersistentGalleryViewer();
        headerRow.Controls.Add(btnOpenGallery, 1, 0);
        historyLayout.Controls.Add(headerRow, 0, 0);

        _filmstripPanel.Controls.Add(new Label
        {
            Text = "Henüz üretilen varyasyon yok. AI ile görsel işlediğinizde burada listelenecektir.",
            AutoSize = true,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 8.2F),
            Padding = new Padding(4, 14, 0, 0)
        });
        historyLayout.Controls.Add(_filmstripPanel, 0, 1);
        historyCard.Controls.Add(historyLayout);

        panel.Controls.Add(historyCard, 0, 1);
        return panel;
    }

    private Control BuildRightActionsPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            Padding = new Padding(14),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        // 1. Marketing Overlay Badge
        stack.Controls.Add(new Label
        {
            Text = "🏷️ Pazarlama Rozeti (Overlay):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.White
        });

        _chkEnableBadge.Margin = new Padding(0, 4, 0, 6);
        _chkEnableBadge.CheckedChanged += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(_chkEnableBadge);

        stack.Controls.Add(new Label { Text = "Rozet Metni / İkonu:", AutoSize = true, Margin = new Padding(0, 2, 0, 2), ForeColor = UiStyle.TextMuted });
        _cboBadgeText.Width = 260;
        _cboBadgeText.Items.AddRange([
            "🚚 Free Fast Shipping",
            "🖨️ 3D Printed / Hand-Painted",
            "🎁 Perfect Gift Idea",
            "⭐ Premium Artisan Quality",
            "🔥 Etsy Best Seller",
            "✨ Limited Holiday Edition"
        ]);
        _cboBadgeText.SelectedIndex = 0;
        _cboBadgeText.TextChanged += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(_cboBadgeText);

        stack.Controls.Add(new Label { Text = "Rozet Konumu:", AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = UiStyle.TextMuted });
        _cboBadgePosition.Width = 260;
        _cboBadgePosition.Items.AddRange(["Sol Üst", "Sağ Üst", "Sol Alt", "Sağ Alt"]);
        _cboBadgePosition.SelectedIndex = 0;
        _cboBadgePosition.SelectedIndexChanged += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(_cboBadgePosition);

        // 2. Aspect Ratio / Resolution
        stack.Controls.Add(new Label
        {
            Text = "📐 Çıktı Oranı (Aspect Ratio):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Margin = new Padding(0, 14, 0, 2),
            ForeColor = Color.White
        });

        _formatComboBox.Width = 260;
        _formatComboBox.Items.AddRange([
            "1:1 Kare (2000x2000 px - Etsy HD)",
            "4:3 Etsy Standartı (2000x1500 px)",
            "Orijinal Çözünürlük"
        ]);
        _formatComboBox.SelectedIndex = 0;
        stack.Controls.Add(_formatComboBox);

        // 3. Export Buttons
        var downloadBtn = UiStyle.CreateButton("💾 Bilgisayara İndir (HD PNG)");
        downloadBtn.Width = 260;
        downloadBtn.Height = 38;
        downloadBtn.Margin = new Padding(0, 12, 0, 0);
        downloadBtn.Click += (_, _) => DownloadImage();
        stack.Controls.Add(downloadBtn);

        var copyBtn = UiStyle.CreateButton("📋 Panoya Kopyala", isSecondary: true);
        copyBtn.Width = 260;
        copyBtn.Height = 34;
        copyBtn.Margin = new Padding(0, 6, 0, 12);
        copyBtn.Click += (_, _) => CopyToClipboard();
        stack.Controls.Add(copyBtn);

        // 4. Etsy Direct Sync Section (Phase 2 Multi-Slot)
        stack.Controls.Add(new Label
        {
            Text = "🚀 Etsy Mağaza Senkronizasyonu:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 2),
            ForeColor = Color.White
        });

        _lblTargetListingInfo.Margin = new Padding(0, 0, 0, 4);
        if (_targetListingId == null)
        {
            _lblTargetListingInfo.Text = "🎯 Hedef: Genel Taslak Modu";
        }
        stack.Controls.Add(_lblTargetListingInfo);

        stack.Controls.Add(new Label { Text = "Etsy Görsel Sıra Numarası (Slot):", AutoSize = true, Margin = new Padding(0, 2, 0, 2), ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 8.2F) });
        _cboEtsyImageSlot.Width = 260;
        _cboEtsyImageSlot.Items.AddRange([
            "1. Sıra (Ana Kapak Fotoğrafı - Primary)",
            "2. Sıra (Detay Fotoğrafı)",
            "3. Sıra (Mockup / Sahneleme)",
            "4. Sıra (Ölçü / Varyant)",
            "Sonraki Boş Sıraya Ekle"
        ]);
        _cboEtsyImageSlot.SelectedIndex = 0;
        stack.Controls.Add(_cboEtsyImageSlot);

        var exportEtsyBtn = UiStyle.CreateButton("🚀 Etsy Listing'e Canlı Yükle");
        exportEtsyBtn.Width = 260;
        exportEtsyBtn.Height = 40;
        exportEtsyBtn.Margin = new Padding(0, 8, 0, 0);
        exportEtsyBtn.BackColor = Color.FromArgb(16, 140, 90);
        exportEtsyBtn.Click += async (_, _) => await ExportToEtsyAsync();
        stack.Controls.Add(exportEtsyBtn);

        card.Controls.Add(stack);
        return card;
    }

    private Control BuildBottomBar()
    {
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var shortcutTip = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "💡 İpucu: Panodan görsel yapıştırmak için Ctrl+V, kaydetmek için Ctrl+S, kopyalamak için Ctrl+C tuşlarını kullanabilirsiniz.",
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 8.5F)
        };
        bottomBar.Controls.Add(shortcutTip, 0, 0);

        var btnAiSettings = UiStyle.CreateButton("⚙️ AI Ayarları", isSecondary: true);
        btnAiSettings.Height = 34;
        btnAiSettings.Click += (_, _) => OpenAiSettingsDialog();
        bottomBar.Controls.Add(btnAiSettings, 1, 0);

        var saveEtsyBtn = UiStyle.CreateButton("🚀 Taslağa Ata", isSecondary: true);
        saveEtsyBtn.Height = 34;
        saveEtsyBtn.Click += async (_, _) => await ExportToEtsyAsync();
        bottomBar.Controls.Add(saveEtsyBtn, 2, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Height = 34;
        closeBtn.Click += (_, _) => Close();
        bottomBar.Controls.Add(closeBtn, 3, 0);

        return bottomBar;
    }

    private void BuildEngineSpecificControls()
    {
        _engineOptionsPanel.SuspendLayout();
        _engineOptionsPanel.Controls.Clear();

        string engineId = _cboEngine.SelectedIndex switch
        {
            0 => "gemini",
            1 => "photoroom",
            2 => "openai",
            3 => "flux",
            _ => "ideogram"
        };

        var (isConfigured, keyName) = AiImageEngineRegistry.GetConfigurationState(engineId);

        // Universal Engine Key Header Row
        var keyRow = new TableLayoutPanel { Width = 340, Height = 34, Margin = new Padding(0, 4, 0, 6), ColumnCount = 2 };
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var keyStatusLbl = new Label
        {
            Text = isConfigured ? $"🟢 {keyName} Bağlı" : $"⚠️ {keyName} Tanımsız",
            ForeColor = isConfigured ? Color.FromArgb(16, 185, 129) : Color.FromArgb(245, 158, 11),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8.8F)
        };
        keyRow.Controls.Add(keyStatusLbl, 0, 0);

        var keyBtn = UiStyle.CreateButton("🔑 Key Yapılandır", isSecondary: true);
        keyBtn.Dock = DockStyle.Fill;
        keyBtn.Height = 28;
        keyBtn.Font = new Font("Segoe UI Semibold", 8.2F);
        keyBtn.Click += (_, _) =>
        {
            using var dlg = new StudioKeyConfigDialog(engineId);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var refreshedPr = PhotoRoomSettingsStore.Load();
                _photoRoomSettings.ApiKey = refreshedPr.ApiKey;
                _photoRoomSettings.AddShadow = refreshedPr.AddShadow;
                _photoRoomSettings.Padding = refreshedPr.Padding;

                _aiSettings = AiOptimizationSettingsStore.Load();
                BuildEngineSpecificControls();
                UpdateAiBadge();
            }
        };
        keyRow.Controls.Add(keyBtn, 1, 0);
        _engineOptionsPanel.Controls.Add(keyRow);

        if (_cboEngine.SelectedIndex == 0) // 🔵 Google Gemini (Primary Default Engine)
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "Google Gemini Görsel Modeli:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _geminiModelComboBox.Width = 340;
            _geminiModelComboBox.Items.Clear();
            _geminiModelComboBox.Items.AddRange([
                "gemini-3.1-flash-image (Nano Banana 2 - SOTA)",
                "gemini-2.5-flash-image (Stabil GA)",
                "gemini-3.1-flash-lite-image (Ultra Hızlı)",
                "gemini-3-pro-image (Nano Banana Pro)",
                "imagen-3.0-generate-002 (Legacy)"
            ]);
            _geminiModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_geminiModelComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "E-Ticaret Düzenleme Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _geminiEditModeComboBox.Width = 340;
            _geminiEditModeComboBox.Items.Clear();
            _geminiEditModeComboBox.Items.AddRange([
                "🏛️ Lüks E-Ticaret Sahnesi (Mermer / Ahşap Kaide)",
                "👤 Manken & Yaşam Alanı (Model Üzerinde Göster)",
                "☀️ Stüdyo Işığı & Atmosfer Yenileme",
                "🎨 Özel Prompt ile Sahne Düzenleme"
            ]);
            _geminiEditModeComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_geminiEditModeComboBox);

            var groundingNotice = new Label
            {
                Text = "✨ Visual Grounding: Yüklediğiniz ürünün şekli, dokusu ve renkleri piksel seviyesinde korunarak Gemini tarafından yeni sahneye giydirilir.",
                ForeColor = Color.FromArgb(129, 140, 248),
                Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
                Width = 340,
                Margin = new Padding(0, 6, 0, 0)
            };
            _engineOptionsPanel.Controls.Add(groundingNotice);
        }
        else if (_cboEngine.SelectedIndex == 1) // 🟣 PhotoRoom Native
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "İşlem Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _photoRoomModeComboBox.Width = 340;
            _photoRoomModeComboBox.Items.Clear();
            _photoRoomModeComboBox.Items.AddRange(["✂️ Şeffaf Arka Plan (Remove BG)", "⚪ Beyaz E-Ticaret Arka Planı", "🎨 AI Arka Plan Sahnesi"]);
            _photoRoomModeComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_photoRoomModeComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "AI Gölge Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _shadowComboBox.Width = 340;
            _shadowComboBox.Items.Clear();
            _shadowComboBox.Items.AddRange(["Yumuşak AI Gölgesi (ai_soft)", "Keskin Gölge (ai_hard)", "Gölgesiz (none)"]);
            _shadowComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_shadowComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "Kenar Hizalama (Padding):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _paddingComboBox.Width = 340;
            _paddingComboBox.Items.Clear();
            _paddingComboBox.Items.AddRange(["%10 Kenar Boşluğu (Standart)", "%5 Sıkı", "%15 Geniş", "%0 Tam Sığdır"]);
            _paddingComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_paddingComboBox);
        }
        else if (_cboEngine.SelectedIndex == 2) // 🏆 OpenAI (GPT Image 2)
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "OpenAI Görsel Modeli:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _openAiModelComboBox.Width = 340;
            _openAiModelComboBox.Items.Clear();
            _openAiModelComboBox.Items.AddRange([
                "gpt-image-2 (Artificial Analysis #1 - ELO 1177)",
                "gpt-image-1.5 (Hızlı & Dengeli)",
                "dall-e-3 (Legacy HD)",
                "dall-e-2 (Geniş Uyumlu)"
            ]);
            _openAiModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_openAiModelComboBox);

            _chkOpenAiTransparentBg.Margin = new Padding(0, 6, 0, 2);
            _engineOptionsPanel.Controls.Add(_chkOpenAiTransparentBg);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "💡 GPT Image 2 görsel muhakeme kabiliyeti ile sıfırdan mockup ve yaşam alanı üretir. 'Şeffaf Arka Plan' seçildiğinde izole cutout çıktısı verir.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 340,
                Margin = new Padding(0, 6, 0, 0)
            });
        }
        else if (_cboEngine.SelectedIndex == 3) // ⚡ Black Forest Labs FLUX
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "BFL FLUX Modeli (api.bfl.ml):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _bflModelComboBox.Width = 340;
            _bflModelComboBox.Items.Clear();
            _bflModelComboBox.Items.AddRange([
                "flux-pro-1.1 (Ultra Realism / ELO 1152)",
                "flux-dev (Geliştirici / Dengeli)",
                "flux-schnell (Ultra Hızlı)"
            ]);
            _bflModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_bflModelComboBox);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "⚡ Black Forest Labs resmi API'si üzerinden fotogerçekçi doku, gerçek optik lens ve ışıklandırma simülasyonu sunar.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 340,
                Margin = new Padding(0, 6, 0, 0)
            });
        }
        else // 4: 🟡 Ideogram 4.0
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "Ürün Üzerine Basılacak Yazı (Tipografi):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _txtIdeogramTypography.Width = 340;
            _txtIdeogramTypography.Font = new Font("Segoe UI", 9F);
            _txtIdeogramTypography.PlaceholderText = "Örn: Best Dad Ever / Handmade 2026 / Vintage Coffee";
            _engineOptionsPanel.Controls.Add(_txtIdeogramTypography);

            _engineOptionsPanel.Controls.Add(new Label { Text = "Ideogram Stil Önayarı:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _ideogramStyleComboBox.Width = 340;
            _ideogramStyleComboBox.Items.Clear();
            _ideogramStyleComboBox.Items.AddRange(["REALISTIC (Gerçekçi Ürün)", "DESIGN (Grafik Tasarım)", "RENDER_3D (3D Render)", "ANIME (İllüstrasyon)", "GENERAL (Genel)"]);
            _ideogramStyleComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_ideogramStyleComboBox);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "🟡 Ideogram 4.0, kupa, tişört ve hediyelik ürünler üzerine sıfır harf hatasıyla kusursuz metin render eder.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 340,
                Margin = new Padding(0, 6, 0, 0)
            });
        }

        _engineOptionsPanel.ResumeLayout(true);
    }

    private void SetComparisonMode(ImageComparisonMode mode)
    {
        _sliderControl.Mode = mode;
        _btnModeSlider.BackColor = mode == ImageComparisonMode.SplitSlider ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
        _btnModeSideBySide.BackColor = mode == ImageComparisonMode.SideBySide ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
        _btnModeAfterOnly.BackColor = mode == ImageComparisonMode.AfterOnly ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
    }

    private void LoadSettings()
    {
        UpdateAiBadge();
        AutoSelectActiveEngine();
        OnScenePresetSelected(_presetChips.SelectedPreset);
    }

    private void AutoSelectActiveEngine()
    {
        var cfg = StudioConfigurationManager.Current;
        if (!string.IsNullOrWhiteSpace(cfg.DefaultEngineId))
        {
            int idx = cfg.DefaultEngineId.ToLowerInvariant() switch
            {
                "gemini" => 0,
                "photoroom" => 1,
                "openai" => 2,
                "flux" => 3,
                "ideogram" => 4,
                _ => 0
            };
            if (idx >= 0 && idx < _cboEngine.Items.Count)
            {
                _cboEngine.SelectedIndex = idx;
                return;
            }
        }

        // Default to Google Gemini (0)
        _cboEngine.SelectedIndex = 0;
    }

    private void UpdateAiBadge()
    {
        _lblAiBadge.Text = _aiSettings.GetActiveBadgeText();
        _lblAiBadge.BackColor = _aiSettings.UseOpenAi
            ? Color.FromArgb(16, 80, 50)
            : (_aiSettings.UseGemini ? Color.FromArgb(20, 60, 120) : Color.FromArgb(40, 50, 65));
    }

    private void OpenAiSettingsDialog()
    {
        using var form = new AiOptimizationSettingsForm();
        form.ShowDialog(this);
        _aiSettings = AiOptimizationSettingsStore.Load();
        UpdateAiBadge();
    }

    private void OnEngineSelectionChanged()
    {
        BuildEngineSpecificControls();
        string engineId = _cboEngine.SelectedIndex switch
        {
            0 => "gemini",
            1 => "photoroom",
            2 => "openai",
            3 => "flux",
            _ => "ideogram"
        };
        var cfg = StudioConfigurationManager.Current;
        cfg.DefaultEngineId = engineId;
        StudioConfigurationManager.Save(cfg);

        string engineName = _cboEngine.SelectedIndex switch
        {
            0 => "Google Gemini (Görsel Düzenleme & Sahneleme)",
            1 => "PhotoRoom Native",
            2 => "OpenAI GPT Image 2",
            3 => "Black Forest Labs FLUX.1 Pro",
            _ => "Ideogram 4.0"
        };
        _statusLabel.Text = $"Aktif Motor: {engineName}";
    }

    private void OnScenePresetSelected(string presetId)
    {
        string product = string.IsNullOrWhiteSpace(_productTitleTxt.Text) ? "handcrafted artisan product" : _productTitleTxt.Text.Trim();

        string baseTemplate = presetId switch
        {
            "rustic" => $"Commercial studio photography of {product} placed on a rustic weathered oak wooden tabletop, subtle realistic contact shadows, shallow depth of field, 8k sharp focus",
            "gamer" => $"High-end commercial product shot of {product} displayed on a sleek matte black gaming desk, ambient cyan and magenta neon LED backlighting, subtle surface reflections, clean modern aesthetic",
            "marble" => $"Luxury minimalist product photography of {product} standing on a smooth white Carrara marble pedestal podium, elegant soft studio strobe lighting, clean neutral beige background",
            "boho" => $"Organic lifestyle product photography of {product} surrounded by lush green monstera and eucalyptus leaves, soft natural sun flare, clean warm terracotta tones, bohemian home decor",
            "white" => $"Crisp clean commercial e-commerce product photography of {product} centered on an infinite seamless pure white studio background, soft natural drop shadow, 2000x2000 Etsy listing quality",
            "festive" => $"Festive holiday Etsy product photoshoot of {product} on a cozy wooden mantle, out-of-focus bokeh fairy lights in the warm background, subtle pine branch accent, warm golden ambient glow",
            _ => _promptTxt.Text
        };

        _promptTxt.Text = _lightingSelector.EnrichPrompt(baseTemplate);
    }

    private void OnLightingSettingsChanged()
    {
        if (!string.IsNullOrWhiteSpace(_promptTxt.Text))
        {
            _promptTxt.Text = _lightingSelector.EnrichPrompt(_promptTxt.Text);
        }
    }

    private async Task GenerateSmartPromptAsync()
    {
        _btnSmartPrompt.Enabled = false;
        _btnSmartPrompt.Text = "⏳ Yazılıyor...";
        _statusLabel.Text = "ChatGPT / Gemini ile yüksek dönüşümlü sahne promptu hazırlanıyor...";

        try
        {
            string scene = _presetChips.SelectedPreset;
            string prompt = await AiImageGenerationService.GenerateSmartPromptAsync(_productTitleTxt.Text, scene, _aiSettings);
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                _promptTxt.Text = _lightingSelector.EnrichPrompt(prompt);
                _statusLabel.Text = "✨ AI Sahne promptu başarıyla oluşturuldu!";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Prompt hatası: {ex.Message}";
        }
        finally
        {
            _btnSmartPrompt.Enabled = true;
            _btnSmartPrompt.Text = "✨ AI ile Prompt Yaz";
        }
    }

    private void SelectProductImage()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Görsel Dosyaları (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tüm Dosyalar (*.*)|*.*",
            Title = "Ürün Fotoğrafı Seç"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            LoadImageFromPath(ofd.FileName);
        }
    }

    private void LoadImageFromPath(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var stream = File.OpenRead(path);
            using var img = Image.FromStream(stream);
            var bmp = new Bitmap(img);
            SetLoadedBitmap(bmp, Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(_productTitleTxt.Text))
            {
                _productTitleTxt.Text = Path.GetFileNameWithoutExtension(path).Replace("_", " ").Replace("-", " ");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Görsel yüklenemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadImageFromUrlAsync(string url)
    {
        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            using var loaded = Image.FromStream(ms);
            var bmp = new Bitmap(loaded);
            SetLoadedBitmap(bmp, "Listing Kapak Görseli");
        }
        catch { }
    }

    private void SetLoadedBitmap(Bitmap bmp, string displayName)
    {
        _sessionManager.SetOriginalImage(bmp);
        _dropZone.SetThumbnail(bmp, displayName);
        _sliderControl.BeforeImage = _sessionManager.OriginalBitmap;
        _statusLabel.Text = $"Görsel yüklendi: {displayName}";
    }

    private void PickImageFromShopListings()
    {
        using var picker = new Form
        {
            Text = "🛍️ Mağazamın Listinglerinden Ürün Fotoğrafı Seç",
            Size = new Size(720, 520),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        root.Controls.Add(flow, 0, 0);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnCancel = UiStyle.CreateButton("İptal", isSecondary: true);
        btnCancel.Click += (_, _) => picker.Close();
        bottom.Controls.Add(btnCancel);
        root.Controls.Add(bottom, 0, 1);
        picker.Controls.Add(root);

        string[] searchDirs = [
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini")
        ];

        var sampleImages = new List<string>();
        foreach (var dir in searchDirs)
        {
            if (Directory.Exists(dir))
            {
                sampleImages.AddRange(Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Take(25));
                sampleImages.AddRange(Directory.GetFiles(dir, "*.jpg", SearchOption.AllDirectories).Take(25));
            }
        }

        if (sampleImages.Count == 0)
        {
            flow.Controls.Add(new Label
            {
                Text = "Yerel önbellekte hazır ürün resmi bulunamadı. Lütfen 'PC'den Dosya Seç' butonunu veya sürükle-bırak alanını kullanın.",
                AutoSize = true,
                ForeColor = UiStyle.TextMuted,
                Padding = new Padding(20)
            });
        }
        else
        {
            foreach (var imgPath in sampleImages.Distinct().Take(20))
            {
                var thumb = new PictureBox
                {
                    Width = 125,
                    Height = 125,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(6),
                    BackColor = Color.FromArgb(30, 41, 59)
                };
                try
                {
                    thumb.Image = Image.FromFile(imgPath);
                    thumb.Click += (_, _) =>
                    {
                        LoadImageFromPath(imgPath);
                        picker.DialogResult = DialogResult.OK;
                        picker.Close();
                    };
                    flow.Controls.Add(thumb);
                }
                catch { }
            }
        }

        picker.ShowDialog(this);
    }

    private async Task ProcessImageWithSelectedEngineAsync()
    {
        _statusLabel.Text = "Yapay zeka ile görsel işleniyor, lütfen bekleyin...";
        _btnProcess.Enabled = false;
        _btnBatchProcess.Enabled = false;
        UseWaitCursor = true;

        try
        {
            string engineId = _cboEngine.SelectedIndex switch
            {
                0 => "gemini",
                1 => "photoroom",
                2 => "openai",
                3 => "flux",
                _ => "ideogram"
            };

            var engine = AiImageEngineRegistry.GetEngine(engineId);
            if (engine == null)
            {
                MessageBox.Show(this, "Seçilen AI görsel motoru bulunamadı.", "Motor Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!engine.IsConfigured())
            {
                var ask = MessageBox.Show(
                    this,
                    $"{engine.DisplayName} için API Key henüz girilmemiş.\n\nŞimdi anahtarınızı yapılandırmak ister misiniz?",
                    "API Key Gerekli",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ask == DialogResult.Yes)
                {
                    using var dlg = new StudioKeyConfigDialog(engineId);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var refreshedPr = PhotoRoomSettingsStore.Load();
                        _photoRoomSettings.ApiKey = refreshedPr.ApiKey;
                        _photoRoomSettings.AddShadow = refreshedPr.AddShadow;
                        _photoRoomSettings.Padding = refreshedPr.Padding;

                        _aiSettings = AiOptimizationSettingsStore.Load();
                        BuildEngineSpecificControls();
                        UpdateAiBadge();
                    }
                }

                if (!engine.IsConfigured()) return;
            }

            // Engine-specific validations
            if (engineId == "photoroom" && _sessionManager.OriginalBitmap is null)
            {
                MessageBox.Show(this, "PhotoRoom için lütfen önce sol taraftan düzenlenecek bir ürün fotoğrafı yükleyin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string prompt = _promptTxt.Text.Trim();
            if (engineId == "gemini" && string.IsNullOrWhiteSpace(prompt))
            {
                // Auto-generate scene prompt based on selected Gemini Edit Mode
                prompt = _geminiEditModeComboBox.SelectedIndex switch
                {
                    0 => "Luxury minimalist marble and warm oak surface in a sunlit boutique studio, soft natural daylight, shallow depth of field, high-end commercial packaging photography",
                    1 => "Worn and held naturally in real life, lifestyle aesthetic, bright airy environment, authentic photography, depth of field",
                    2 => "Clean commercial studio strobe lighting, crisp high-key lighting, soft natural contact shadow, 8k ultra-sharp commercial packshot",
                    _ => "Professional commercial e-commerce product photograph, high detail, studio lighting"
                };
            }
            else if (engineId != "photoroom" && string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show(this, "Lütfen bir sahne promptu girin veya 'AI ile Prompt Yaz' butonunu kullanın.", "Prompt Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Determine model name
            string modelName = engineId switch
            {
                "gemini" => _geminiModelComboBox.SelectedIndex switch
                {
                    0 => "gemini-3.1-flash-image",
                    1 => "gemini-2.5-flash-image",
                    2 => "gemini-3.1-flash-lite-image",
                    3 => "gemini-3-pro-image",
                    _ => "imagen-3.0-generate-002"
                },
                "openai" => _openAiModelComboBox.SelectedIndex switch
                {
                    0 => "gpt-image-2",
                    1 => "gpt-image-1.5",
                    2 => "dall-e-3",
                    _ => "dall-e-2"
                },
                "flux" => _bflModelComboBox.SelectedIndex switch
                {
                    0 => "flux-pro-1.1",
                    1 => "flux-dev",
                    _ => "flux-schnell"
                },
                _ => ""
            };

            var request = new ImageEngineRequest
            {
                InputImage = _sessionManager.OriginalBitmap,
                Prompt = prompt,
                ModelName = modelName,
                LightingPreset = _lightingSelector.SelectedLighting,
                CameraAnglePreset = _lightingSelector.SelectedCamera,
                PreserveProduct = true,
                TransparentBackground = _chkOpenAiTransparentBg.Checked,
                ProcessMode = _photoRoomModeComboBox.SelectedIndex == 0 ? "remove_bg" : (_photoRoomModeComboBox.SelectedIndex == 1 ? "white_bg" : "ai_background"),
                ShadowMode = _shadowComboBox.SelectedIndex switch { 0 => "ai_soft", 1 => "ai_hard", _ => "none" },
                Padding = _paddingComboBox.SelectedIndex switch { 0 => 0.1, 1 => 0.05, 2 => 0.15, _ => 0.0 }
            };

            var result = await engine.ProcessAsync(request);

            if (result.Success && result.ResultImage != null)
            {
                string infoText = string.IsNullOrWhiteSpace(result.ModelUsed)
                    ? result.EngineName
                    : $"{result.EngineName} ({result.ModelUsed} - {result.ElapsedMilliseconds}ms)";
                OnGenerationSuccess(result.ResultImage, infoText);
            }
            else
            {
                MessageBox.Show(this, result.ErrorMessage, $"{engine.DisplayName} Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"İşlem hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnProcess.Enabled = true;
            _btnBatchProcess.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private async Task RunBatchSceneGenerationAsync()
    {
        if (_cboEngine.SelectedIndex == 0 && _sessionManager.OriginalBitmap == null)
        {
            MessageBox.Show(this, "PhotoRoom için lütfen önce sol taraftan düzenlenecek bir ürün fotoğrafı seçin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string engineName = _cboEngine.SelectedIndex switch
        {
            0 => "PhotoRoom Native",
            1 => "OpenAI DALL-E 3",
            _ => "Google Gemini Imagen 3"
        };

        var confirm = MessageBox.Show(
            this,
            $"'{engineName}' motoru ile 4 farklı ticari Etsy sahnesi (Beyaz Katalog, Ahşap Rustic, Lüks Mermer, Boho Botanik) üretilecektir.\n\nDevam etmek istiyor musunuz?",
            "4'lü Sahne Toplu Üretim Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _statusLabel.Text = "Çoklu sahne toplu üretimi başlatılıyor...";
        _btnProcess.Enabled = false;
        _btnBatchProcess.Enabled = false;
        UseWaitCursor = true;

        try
        {
            var progress = new Progress<(int Step, int Total, string Message, Bitmap? ResultImage)>(p =>
            {
                _statusLabel.Text = p.Message;
            });

            string lighting = _lightingSelector.SelectedLighting;
            string camera = _lightingSelector.SelectedCamera;
            string modifier = $"{lighting}, {camera}";

            var results = await BatchImageGenerationService.RunBatchAsync(
                _productTitleTxt.Text,
                _sessionManager.OriginalBitmap,
                _cboEngine.SelectedIndex,
                _aiSettings,
                _photoRoomSettings,
                modifier,
                progress);

            using var dlg = new BatchSceneGenerationDialog(results);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedImage != null)
            {
                OnGenerationSuccess(dlg.SelectedImage, dlg.SelectedSceneName ?? "Toplu Sahne");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Toplu üretim hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnProcess.Enabled = true;
            _btnBatchProcess.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void OnGenerationSuccess(Bitmap resultImg, string engineName)
    {
        _sessionManager.SetGeneratedImage(resultImg);
        UpdateBadgeOverlay();
        RefreshFilmstrip();
        _statusLabel.Text = $"✨ {engineName} görseliniz başarıyla üretildi!";

        // Save to Persistent SQLite Gallery in background
        _ = PersistentStudioGalleryService.SaveItemAsync(
            resultImg,
            _promptTxt.Text,
            engineName,
            _productTitleTxt.Text,
            _targetListingId);
    }

    private void UpdateBadgeOverlay()
    {
        if (_sessionManager.GeneratedBitmap is null) return;

        if (_chkEnableBadge.Checked && !string.IsNullOrWhiteSpace(_cboBadgeText.Text))
        {
            string pos = _cboBadgePosition.SelectedItem?.ToString() ?? "Sol Üst";
            var badged = AiImageGenerationService.ApplyOverlayBadge(_sessionManager.GeneratedBitmap, _cboBadgeText.Text, pos);
            _sessionManager.SetFinalDisplayImage(badged);
        }
        else
        {
            _sessionManager.SetFinalDisplayImage(new Bitmap(_sessionManager.GeneratedBitmap));
        }

        _sliderControl.AfterImage = _sessionManager.FinalDisplayBitmap;
    }

    private void RefreshFilmstrip()
    {
        _filmstripPanel.SuspendLayout();
        _filmstripPanel.Controls.Clear();

        var history = _sessionManager.SessionHistory;
        for (int i = 0; i < history.Count; i++)
        {
            var bmp = history[i];
            var pb = new PictureBox
            {
                Image = bmp,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 64,
                Height = 64,
                Margin = new Padding(3),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(30, 41, 59)
            };

            int captureIndex = i;
            pb.Click += (_, _) =>
            {
                _sessionManager.SetGeneratedImage(new Bitmap(bmp));
                UpdateBadgeOverlay();
                _statusLabel.Text = $"Geçmiş varyasyon #{captureIndex + 1} tuvale yüklendi.";
            };

            _filmstripPanel.Controls.Add(pb);
        }

        _filmstripPanel.ResumeLayout(true);
    }

    private void OpenPersistentGalleryViewer()
    {
        using var dlg = new StudioGalleryViewerDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedImage != null)
        {
            OnGenerationSuccess(dlg.SelectedImage, "Kayıtlı Galeri");
            if (!string.IsNullOrWhiteSpace(dlg.SelectedPrompt))
            {
                _promptTxt.Text = dlg.SelectedPrompt;
            }
        }
    }

    private void DownloadImage()
    {
        var targetImg = _sessionManager.FinalDisplayBitmap ?? _sessionManager.GeneratedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "İndirmek için önce seçtiğiniz AI motoru ile bir görsel üretin veya işleyin.", "Görsel İndir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string format = _formatComboBox.SelectedItem?.ToString() ?? "1:1 Kare (2000x2000 px - Etsy HD)";
        using var prepared = _sessionManager.PrepareOutputImage(targetImg, format);

        using var sfd = new SaveFileDialog
        {
            Filter = "PNG Görseli (*.png)|*.png|JPEG Görseli (*.jpg)|*.jpg",
            FileName = $"etsy_ai_mockup_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            prepared.Save(sfd.FileName, sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png);
            MessageBox.Show(this, "Görseliniz bilgisayarınıza HD kalitede başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CopyToClipboard()
    {
        var targetImg = _sessionManager.FinalDisplayBitmap ?? _sessionManager.GeneratedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "Panoya kopyalamak için önce bir görsel üretin.", "Panoya Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Clipboard.SetImage(targetImg);
        _statusLabel.Text = "📋 Görsel panoya kopyalandı! İstediğiniz yere (Ctrl+V) yapıştırabilirsiniz.";
    }

    private async Task ExportToEtsyAsync()
    {
        var targetImg = _sessionManager.FinalDisplayBitmap ?? _sessionManager.GeneratedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "Etsy'ye aktarmak için önce bir görsel üretin veya işleyin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_targetListingId.HasValue && _apiClient != null)
        {
            int? rank = _cboEtsyImageSlot.SelectedIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 3,
                3 => 4,
                _ => null
            };

            string slotDesc = _cboEtsyImageSlot.SelectedItem?.ToString() ?? "1. Sıra (Ana Kapak)";
            var confirm = MessageBox.Show(
                this,
                $"Bu görsel #{_targetListingId.Value} numaralı Etsy listing'inize '{slotDesc}' olarak yüklensin mi?",
                "Etsy Canlı Yükleme Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                _statusLabel.Text = "Etsy API'ye görsel yükleniyor...";
                UseWaitCursor = true;

                try
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), $"etsy_upload_{_targetListingId.Value}_{Guid.NewGuid():N}.png");
                    string format = _formatComboBox.SelectedItem?.ToString() ?? "1:1 Kare (2000x2000 px - Etsy HD)";
                    using (var prepared = _sessionManager.PrepareOutputImage(targetImg, format))
                    {
                        prepared.Save(tempFile, ImageFormat.Png);
                    }

                    var settings = EtsyApiSettingsStore.Load();
                    await _apiClient.UploadOwnShopListingImageAsync(settings, _targetListingId.Value, tempFile, rank);

                    try { File.Delete(tempFile); } catch { }

                    MessageBox.Show(this, $"Görsel Etsy listing'inize '{slotDesc}' olarak başarıyla yüklendi!", "Etsy Senkronizasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _statusLabel.Text = "✅ Görsel Etsy API üzerinden listing'e yüklendi!";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Etsy yükleme hatası: {ex.Message}", "Yükleme Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    UseWaitCursor = false;
                }
                return;
            }
        }

        MessageBox.Show(this, "Seçili görsel Etsy Listing taslağınızın kapak fotoğrafı olarak başarıyla tanımlandı!", "Etsy Entegrasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.V))
        {
            if (_dropZone.TryPasteFromClipboard())
            {
                _statusLabel.Text = "📋 Görsel panodan başarıyla yapıştırıldı!";
                return true;
            }
        }
        else if (keyData == (Keys.Control | Keys.S))
        {
            DownloadImage();
            return true;
        }
        else if (keyData == (Keys.Control | Keys.C))
        {
            CopyToClipboard();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowPhotoRoomKeyDialog(Label? statusLabel = null)
    {
        using var dialog = new Form
        {
            Text = "🪞 PhotoRoom API Key Yapılandırması",
            Size = new Size(480, 260),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.CardBackground,
            ForeColor = UiStyle.TextDark,
        };

        var lbl = new Label
        {
            Text = "PhotoRoom API Dashboard'dan aldığınız API Key'i yapıştırın:\n(Live: sk_pr_... veya Sandbox: sandbox_sk_...)",
            Location = new Point(20, 16),
            Size = new Size(420, 36),
            Font = new Font("Segoe UI", 9F),
        };
        dialog.Controls.Add(lbl);

        var txtKey = new TextBox
        {
            Location = new Point(20, 58),
            Size = new Size(420, 30),
            Font = new Font("Consolas", 10F),
            Text = _photoRoomSettings.ApiKey,
            PlaceholderText = "sk_pr_etsy_... veya sandbox_sk_...",
        };
        dialog.Controls.Add(txtKey);

        var info = new Label
        {
            Text = "💡 Live Key (sk_pr_...): Filigransız yüksek çözünürlüklü ticari görsel üretir.\n💡 Sandbox Key (sandbox_sk_...): Ayda 1000 görsel ücretsizdir (filigranlı).",
            Location = new Point(20, 96),
            Size = new Size(420, 38),
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = UiStyle.TextMuted,
        };
        dialog.Controls.Add(info);

        var btnOk = new Button
        {
            Text = "💾 Kaydet",
            DialogResult = DialogResult.OK,
            Location = new Point(20, 150),
            Size = new Size(110, 36),
            BackColor = UiStyle.SuccessColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        };
        dialog.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "İptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(140, 150),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        dialog.Controls.Add(btnCancel);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var newKey = txtKey.Text.Trim();
            _photoRoomSettings.ApiKey = newKey;
            PhotoRoomSettingsStore.Save(_photoRoomSettings);

            _aiSettings.PhotoRoomApiKey = newKey;
            AiOptimizationSettingsStore.Save(_aiSettings);

            if (statusLabel != null)
            {
                statusLabel.Text = string.IsNullOrWhiteSpace(newKey) ? "⚠️ Key Tanımsız" : "🟢 PhotoRoom Bağlı";
                statusLabel.ForeColor = string.IsNullOrWhiteSpace(newKey) ? Color.FromArgb(245, 158, 11) : Color.FromArgb(16, 185, 129);
            }

            MessageBox.Show(this, "PhotoRoom API Key başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
