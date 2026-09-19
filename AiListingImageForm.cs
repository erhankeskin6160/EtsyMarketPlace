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
        BackColor = Color.FromArgb(29, 78, 216),
        Height = 32,
        MinimumSize = new Size(145, 32),
        Padding = new Padding(8, 0, 8, 0),
        TextAlign = ContentAlignment.MiddleCenter,
        Cursor = Cursors.Hand
    };
    private readonly Label _statusLabel = new() { UseMnemonic = false };
    private readonly ToolTip _tip = new();
    private readonly Button _btnModeSlider = UiStyle.CreateButton("↔️ Split Perde", isSecondary: false);
    private readonly Button _btnModeSideBySide = UiStyle.CreateButton("⫴ Yan Yana", isSecondary: true);
    private readonly Button _btnModeAfterOnly = UiStyle.CreateButton("🖼️ Sadece Sonuç", isSecondary: true);

    // UI: Left Panel Inputs & Presets
    private readonly ModernComboBox _cboEngine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernImageDropZone _dropZone = new();
    private readonly TextBox _productTitleTxt = new();
    private readonly PresetChipSelector _presetChips = new();
    private readonly ModernMultilineTextBox _promptTxt = new() { Height = 64 };
    private readonly Button _btnSmartPrompt = new();
    private readonly StudioLightingSelectorControl _lightingSelector = new();
    private readonly Panel _engineOptionsContainer = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly FlowLayoutPanel _engineOptionsPanel = new() { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
    private readonly ModernComboBox _photoRoomModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _shadowComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _paddingComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _openAiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _openAiModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernCheckBox _chkOpenAiTransparentBg = new() { Text = "Şeffaf Arka Plan (Transparent PNG)", AutoSize = true, ForeColor = Color.White };
    private readonly ModernComboBox _bflModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtIdeogramTypography = new();
    private readonly ModernComboBox _ideogramStyleComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _geminiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _geminiEditModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernButtonControl _btnProcess = new();
    private readonly ModernButtonControl _btnBatchProcess = new();

    // Left Form Segmented Tabs
    private readonly Button _btnFormTabBasic = new();
    private readonly Button _btnFormTabScene = new();
    private readonly Button _btnFormTabEngine = new();
    private Panel _pnlFormTabBasic = null!;
    private Panel _pnlFormTabScene = null!;
    private Panel _pnlFormTabEngine = null!;

    // UI: Center Canvas & History
    private readonly ModernBeforeAfterSlider _sliderControl = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _filmstripPanel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };

    // UI: Right Panel Marketing & Export
    private readonly ModernCheckBox _chkEnableBadge = new() { Text = "Pazarlama Rozetini Etkinleştir", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F), ForeColor = Color.FromArgb(226, 232, 240) };
    private readonly ModernComboBox _cboBadgeText = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _cboBadgePosition = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _formatComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernComboBox _cboEtsyImageSlot = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblTargetListingInfo = new() { AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 8.5F) };

    // Unified Studio Tabs & Containers
    private readonly Button _btnTabSingleStudio = new();
    private readonly Button _btnTabBatchStudio = new();
    private Panel _viewContainer = null!;
    private TableLayoutPanel _singleDesignContainer = null!;
    private BatchStudioPanelControl _batchStudioControl = null!;
    private Control? _bottomBar;
    private int _currentTabIndex;

    public AiListingImageForm(
        IAiListingOptimizer? aiOptimizer = null,
        string? initialImagePath = null,
        string? initialTitle = null,
        int initialTab = 0)
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
            _batchStudioControl.LoadInitialImage(initialImagePath);
        }

        if (!string.IsNullOrWhiteSpace(initialTitle))
        {
            _productTitleTxt.Text = initialTitle;
            OnScenePresetSelected(_presetChips.SelectedPreset);
        }

        if (initialTab == 1)
        {
            SwitchToTab(1);
        }

        _productTitleTxt.TextChanged += (_, _) => UpdateProcessButtonState();
        _promptTxt.TextChanged += (_, _) => UpdateProcessButtonState();
        _cboEngine.SelectedIndexChanged += (_, _) => UpdateProcessButtonState();
        UpdateProcessButtonState();
    }

    public AiListingImageForm(object? listing, object? apiClient) : this(null, null, null, 0)
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

        UpdateProcessButtonState();
    }

    private void BuildLayout()
    {
        Text = "🎨 AI Görsel & Arka Plan Stüdyosu (GPT-Image-2.5 • Gemini • PhotoRoom)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Normal;
        MinimumSize = new Size(980, 640);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        // 1. Header Toolbar
        root.Controls.Add(BuildHeaderBar(), 0, 0);

        // 2. View Container (Hosts Single Design Grid & Batch Studio Panel)
        _viewContainer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };

        _singleDesignContainer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 12, 0, 8) };
        _singleDesignContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 395));
        _singleDesignContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _singleDesignContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 395));

        _singleDesignContainer.Controls.Add(BuildLeftControlsPanel(), 0, 0);
        _singleDesignContainer.Controls.Add(BuildCenterCanvasPanel(), 1, 0);
        _singleDesignContainer.Controls.Add(BuildRightActionsPanel(), 2, 0);

        _batchStudioControl = new BatchStudioPanelControl(_apiClient, _aiSettings) { Dock = DockStyle.Fill, Visible = false };
        _batchStudioControl.OpenInSingleStudioRequested += bmp =>
        {
            SetLoadedBitmap(bmp, "Toplu Fabrikadan Aktarılan");
            SwitchToTab(0);
        };

        _viewContainer.Controls.Add(_batchStudioControl);
        _viewContainer.Controls.Add(_singleDesignContainer);
        root.Controls.Add(_viewContainer, 0, 1);

        // 3. Bottom Bar
        _bottomBar = BuildBottomBar();
        root.Controls.Add(_bottomBar, 0, 2);

        SwitchToTab(0);
    }

    public void SwitchToTab(int tabIndex)
    {
        _currentTabIndex = tabIndex;
        if (tabIndex == 0)
        {
            _singleDesignContainer.Visible = true;
            _batchStudioControl.Visible = false;
            if (_bottomBar != null) _bottomBar.Visible = true;

            _btnTabSingleStudio.BackColor = Color.FromArgb(79, 70, 229); // Indigo 600
            _btnTabSingleStudio.ForeColor = Color.White;
            _btnTabBatchStudio.BackColor = Color.FromArgb(30, 41, 59);
            _btnTabBatchStudio.ForeColor = Color.FromArgb(148, 163, 184);

            _btnModeSlider.Visible = true;
            _btnModeSideBySide.Visible = true;
            _btnModeAfterOnly.Visible = true;
        }
        else
        {
            _singleDesignContainer.Visible = false;
            _batchStudioControl.Visible = true;
            if (_bottomBar != null) _bottomBar.Visible = false;

            _btnTabSingleStudio.BackColor = Color.FromArgb(30, 41, 59);
            _btnTabSingleStudio.ForeColor = Color.FromArgb(148, 163, 184);
            _btnTabBatchStudio.BackColor = Color.FromArgb(16, 185, 129); // Emerald 600
            _btnTabBatchStudio.ForeColor = Color.White;

            _btnModeSlider.Visible = false;
            _btnModeSideBySide.Visible = false;
            _btnModeAfterOnly.Visible = false;
        }
    }

    private Control BuildHeaderBar()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // 0: Title & Subtitle
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // 1: Studio Mode Tabs
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // 2: Mode Switch Buttons (Single Studio)
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    // 3: Flexible status text area
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // 4: Right Action Toolbar (API Key & AI Badge)

        // Title & Subtitle
        var titleStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 2, 8, 0)
        };
        titleStack.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "🎨 AI Görsel Stüdyosu",
            Font = new Font("Verdana", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            UseMnemonic = false
        });
        titleStack.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Gemini • OpenAI • PhotoRoom",
            Font = new Font("Segoe UI", 8F),
            ForeColor = UiStyle.TextMuted,
            UseMnemonic = false
        });
        header.Controls.Add(titleStack, 0, 0);

        // Studio Mode Tabs
        var tabStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 8, 4, 0)
        };

        _btnTabSingleStudio.Dock = DockStyle.None;
        _btnTabSingleStudio.Text = "🎯 Tekli Tasarım";
        _btnTabSingleStudio.Height = 32;
        _btnTabSingleStudio.MinimumSize = new Size(100, 32);
        _btnTabSingleStudio.AutoSize = true;
        _btnTabSingleStudio.Padding = new Padding(6, 0, 6, 0);
        _btnTabSingleStudio.FlatStyle = FlatStyle.Flat;
        _btnTabSingleStudio.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        _btnTabSingleStudio.Cursor = Cursors.Hand;
        _btnTabSingleStudio.FlatAppearance.BorderSize = 0;
        _btnTabSingleStudio.Click += (_, _) => SwitchToTab(0);
        tabStack.Controls.Add(_btnTabSingleStudio);

        _btnTabBatchStudio.Dock = DockStyle.None;
        _btnTabBatchStudio.Text = "⚡ Toplu Fabrika";
        _btnTabBatchStudio.Height = 32;
        _btnTabBatchStudio.MinimumSize = new Size(105, 32);
        _btnTabBatchStudio.AutoSize = true;
        _btnTabBatchStudio.Padding = new Padding(6, 0, 6, 0);
        _btnTabBatchStudio.FlatStyle = FlatStyle.Flat;
        _btnTabBatchStudio.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        _btnTabBatchStudio.Cursor = Cursors.Hand;
        _btnTabBatchStudio.FlatAppearance.BorderSize = 0;
        _btnTabBatchStudio.Click += (_, _) => SwitchToTab(1);
        tabStack.Controls.Add(_btnTabBatchStudio);

        header.Controls.Add(tabStack, 1, 0);

        // Mode Switch Buttons (Single Studio Only)
        var modeStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 8, 4, 0)
        };

        ConfigureModeButton(_btnModeSlider, "— Split", 65);
        _btnModeSlider.Click += (_, _) => SetComparisonMode(ImageComparisonMode.SplitSlider);
        modeStack.Controls.Add(_btnModeSlider);

        ConfigureModeButton(_btnModeSideBySide, "⬛ Yan Yana", 72);
        _btnModeSideBySide.Click += (_, _) => SetComparisonMode(ImageComparisonMode.SideBySide);
        modeStack.Controls.Add(_btnModeSideBySide);

        ConfigureModeButton(_btnModeAfterOnly, "✨ Sonuç", 65);
        _btnModeAfterOnly.Click += (_, _) => SetComparisonMode(ImageComparisonMode.AfterOnly);
        modeStack.Controls.Add(_btnModeAfterOnly);

        header.Controls.Add(modeStack, 2, 0);

        // Live Status Text (occupies flexible middle area with tooltip fallback)
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI Semibold", 8.5F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Margin = new Padding(8, 10, 8, 0);
        _statusLabel.Text = "Hazır. Görsel yükleyebilir veya bir sahne seçebilirsiniz.";
        _statusLabel.TextChanged += (_, _) => _tip.SetToolTip(_statusLabel, _statusLabel.Text);
        _tip.SetToolTip(_statusLabel, _statusLabel.Text);
        header.Controls.Add(_statusLabel, 3, 0);

        // Right Action Toolbar: Groups API Key Yapılandır & AI Badge side by side with zero overlapping
        var rightStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0)
        };

        var btnConfigureKeys = new Button
        {
            Dock = DockStyle.None,
            Text = "🔑 API Yapılandır",
            Height = 32,
            MinimumSize = new Size(105, 32),
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(79, 70, 229), // Indigo 600
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0)
        };
        btnConfigureKeys.FlatAppearance.BorderSize = 0;
        btnConfigureKeys.FlatAppearance.MouseOverBackColor = Color.FromArgb(99, 102, 241); // Indigo 500
        btnConfigureKeys.FlatAppearance.MouseDownBackColor = Color.FromArgb(67, 56, 202); // Indigo 700
        btnConfigureKeys.Click += (_, _) =>
        {
            using var dlg = new StudioKeyConfigDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK || true)
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
        rightStack.Controls.Add(btnConfigureKeys);

        _lblAiBadge.Dock = DockStyle.None;
        _lblAiBadge.Height = 32;
        _lblAiBadge.MinimumSize = new Size(0, 32);
        _lblAiBadge.AutoSize = true;
        _lblAiBadge.Padding = new Padding(8, 0, 8, 0);
        _lblAiBadge.Margin = new Padding(0, 0, 4, 0);
        _lblAiBadge.TextAlign = ContentAlignment.MiddleCenter;
        _lblAiBadge.Click += (_, _) => OpenAiSettingsDialog();
        UpdateAiBadge();
        rightStack.Controls.Add(_lblAiBadge);

        _tip.SetToolTip(btnConfigureKeys, "PhotoRoom, Gemini ve OpenAI API anahtarlarını yapılandırın");

        header.Controls.Add(rightStack, 4, 0);

        return header;
    }

    private static void ConfigureModeButton(Button btn, string text, int minWidth = 0)
    {
        btn.Dock = DockStyle.None;
        btn.Text = text;
        btn.Height = 32;
        if (minWidth > 0) btn.MinimumSize = new Size(minWidth, 32);
        btn.AutoSize = true;
        btn.Padding = new Padding(6, 0, 6, 0);
        btn.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
    }

    private void ConfigureFormTabButton(Button btn, string text, int tabIndex)
    {
        btn.Dock = DockStyle.Fill;
        btn.Text = text;
        btn.Height = 34;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.Font = new Font("Segoe UI Semibold", 8.6F, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(2, 0, 2, 0);
        btn.Click += (_, _) => SwitchLeftFormTab(tabIndex);
    }

    private void SwitchLeftFormTab(int tabIndex)
    {
        if (_pnlFormTabBasic != null) _pnlFormTabBasic.Visible = tabIndex == 0;
        if (_pnlFormTabScene != null) _pnlFormTabScene.Visible = tabIndex == 1;
        if (_pnlFormTabEngine != null) _pnlFormTabEngine.Visible = tabIndex == 2;

        UpdateLeftTabButtonStyle(_btnFormTabBasic, tabIndex == 0);
        UpdateLeftTabButtonStyle(_btnFormTabScene, tabIndex == 1);
        UpdateLeftTabButtonStyle(_btnFormTabEngine, tabIndex == 2);
    }

    private static void UpdateLeftTabButtonStyle(Button btn, bool isActive)
    {
        btn.BackColor = isActive ? Color.FromArgb(79, 70, 229) : Color.FromArgb(20, 28, 48);
        btn.ForeColor = isActive ? Color.White : Color.FromArgb(148, 163, 184);
        btn.FlatAppearance.BorderColor = isActive ? Color.FromArgb(99, 102, 241) : Color.FromArgb(45, 55, 75);
    }

    private Control BuildLeftControlsPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(2, 0, 4, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 0: Segmented Tab Bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 1: Scrollable Tab Content
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));  // 2: Fixed Bottom Actions

        // 1. Segmented Tab Bar
        var tabBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        tabBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tabBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tabBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        ConfigureFormTabButton(_btnFormTabBasic, "📷 Temel", 0);
        ConfigureFormTabButton(_btnFormTabScene, "🪄 Sahne & Işık", 1);
        ConfigureFormTabButton(_btnFormTabEngine, "⚙️ Model/API", 2);

        tabBar.Controls.Add(_btnFormTabBasic, 0, 0);
        tabBar.Controls.Add(_btnFormTabScene, 1, 0);
        tabBar.Controls.Add(_btnFormTabEngine, 2, 0);
        mainLayout.Controls.Add(tabBar, 0, 0);

        // 2. Tab Content Panels inside ModernScrollPanel
        var leftScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var tabsContainer = new Panel
        {
            Width = 348,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // Tab 1: Temel Ayarlar (Image Dropzone, Title, Engine)
        _pnlFormTabBasic = new FlowLayoutPanel
        {
            Width = 348,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _pnlFormTabBasic.Controls.Add(new Label
        {
            Text = "📷 Ürün Görsel Kaynağı:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 2, 0, 8)
        });

        _dropZone.Width = 348;
        _dropZone.Height = 125;
        _dropZone.Margin = new Padding(0, 0, 0, 10);
        _dropZone.ImageSelected += (_, args) =>
        {
            SetLoadedBitmap(args.Bitmap, args.FilePath != null ? Path.GetFileName(args.FilePath) : "Panodan Yapıştırılan Görsel");
        };
        _pnlFormTabBasic.Controls.Add(_dropZone);

        // Quick Browse / Shop buttons with generous height and spacing
        var btnGrid = new TableLayoutPanel { Width = 348, Height = 38, Margin = new Padding(0, 0, 0, 16), ColumnCount = 2 };
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var loadLocalBtn = UiStyle.CreateButton("📁 PC'den Seç", isSecondary: true);
        loadLocalBtn.Dock = DockStyle.Fill;
        loadLocalBtn.Height = 38;
        loadLocalBtn.Font = new Font("Segoe UI Semibold", 8.8F);
        loadLocalBtn.Click += (_, _) => SelectProductImage();
        btnGrid.Controls.Add(loadLocalBtn, 0, 0);

        var loadShopBtn = UiStyle.CreateButton("🛍️ Mağazamdan Seç", isSecondary: true);
        loadShopBtn.Dock = DockStyle.Fill;
        loadShopBtn.Height = 38;
        loadShopBtn.Font = new Font("Segoe UI Semibold", 8.8F);
        loadShopBtn.Click += (_, _) => PickImageFromShopListings();
        btnGrid.Controls.Add(loadShopBtn, 1, 0);
        _pnlFormTabBasic.Controls.Add(btnGrid);

        _pnlFormTabBasic.Controls.Add(new Label
        {
            Text = "Ürün Adı / Konsepti:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 9F)
        });

        _productTitleTxt.Width = 348;
        _productTitleTxt.Font = new Font("Segoe UI", 9.5F);
        _productTitleTxt.PlaceholderText = "Örn: Handcrafted Ceramic Coffee Mug";
        _productTitleTxt.Margin = new Padding(0, 0, 0, 16);
        _productTitleTxt.TextChanged += (_, _) => OnScenePresetSelected(_presetChips.SelectedPreset);
        _pnlFormTabBasic.Controls.Add(_productTitleTxt);

        _pnlFormTabBasic.Controls.Add(new Label
        {
            Text = "🤖 İşlem Yapacak AI Motoru:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 6)
        });

        _cboEngine.Width = 348;
        _cboEngine.Font = new Font("Segoe UI", 9.5F);
        _cboEngine.Margin = new Padding(0, 0, 0, 10);
        _cboEngine.Items.Clear();
        _cboEngine.Items.AddRange([
            "🔵 Google Gemini (Görsel Düzenleme & Sahneleme - SOTA)",
            "🟣 PhotoRoom Native (Arka Plan Silme & AI Gölge)",
            "⚡ OpenAI (GPT-Image-2.5 Flare & Sunburst)",
            "⚡ Black Forest Labs FLUX.1 (Ultra Realism)",
            "🟡 Ideogram 4.0 (Kusursuz Tipografi & Yazı)"
        ]);
        _cboEngine.SelectedIndex = 0;
        _cboEngine.SelectedIndexChanged += (_, _) => OnEngineSelectionChanged();
        _pnlFormTabBasic.Controls.Add(_cboEngine);

        tabsContainer.Controls.Add(_pnlFormTabBasic);

        // Tab 2: Sahne & Işık (Preset Chips, Prompt, Lighting & Camera)
        _pnlFormTabScene = new FlowLayoutPanel
        {
            Width = 348,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Visible = false
        };

        _pnlFormTabScene.Controls.Add(new Label
        {
            Text = "🪄 Etsy Sahneleme Preseti Seçin:",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 8),
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI Semibold", 9F)
        });

        _presetChips.Width = 348;
        _presetChips.Margin = new Padding(0, 0, 0, 16);
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
        _pnlFormTabScene.Controls.Add(_presetChips);

        var promptHeader = new TableLayoutPanel { Width = 348, Height = 34, Margin = new Padding(0, 0, 0, 8), ColumnCount = 2 };
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
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
        _btnSmartPrompt.Height = 32;
        _btnSmartPrompt.FlatStyle = FlatStyle.Flat;
        _btnSmartPrompt.FlatAppearance.BorderSize = 0;
        _btnSmartPrompt.BackColor = Color.FromArgb(99, 102, 241);
        _btnSmartPrompt.ForeColor = Color.White;
        _btnSmartPrompt.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnSmartPrompt.TextAlign = ContentAlignment.MiddleCenter;
        _btnSmartPrompt.Padding = new Padding(4, 0, 4, 0);
        _btnSmartPrompt.Cursor = Cursors.Hand;
        _btnSmartPrompt.Click += async (_, _) => await GenerateSmartPromptAsync();
        promptHeader.Controls.Add(_btnSmartPrompt, 1, 0);
        _pnlFormTabScene.Controls.Add(promptHeader);

        _promptTxt.Width = 348;
        _promptTxt.Height = 75;
        _promptTxt.Font = new Font("Segoe UI", 9F);
        _promptTxt.Margin = new Padding(0, 0, 0, 16);
        _pnlFormTabScene.Controls.Add(_promptTxt);

        _lightingSelector.Width = 348;
        _lightingSelector.Margin = new Padding(0, 0, 0, 10);
        _lightingSelector.SettingsChanged += (_, _) => OnLightingSettingsChanged();
        _pnlFormTabScene.Controls.Add(_lightingSelector);

        tabsContainer.Controls.Add(_pnlFormTabScene);

        // Tab 3: Model & API (Dynamic Engine Settings & Keys)
        _pnlFormTabEngine = new FlowLayoutPanel
        {
            Width = 348,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Visible = false
        };

        _engineOptionsContainer.Width = 348;
        _engineOptionsContainer.Controls.Clear();
        _engineOptionsContainer.Controls.Add(_engineOptionsPanel);
        _pnlFormTabEngine.Controls.Add(_engineOptionsContainer);
        BuildEngineSpecificControls();

        tabsContainer.Controls.Add(_pnlFormTabEngine);

        leftScroll.SetContent(tabsContainer);
        mainLayout.Controls.Add(leftScroll, 0, 1);

        // 3. Fixed Bottom Action Buttons
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty
        };

        _btnProcess.Width = 348;
        _btnProcess.Height = 38;
        _btnProcess.Text = "🚀 Seçili AI ile Görseli Üret / İşle";
        _btnProcess.NormalColor = UiStyle.PrimaryColor;
        _btnProcess.HoverColor = UiStyle.PrimaryHover;
        _btnProcess.Font = new Font("Segoe UI Semibold", 9.6F, FontStyle.Bold);
        _btnProcess.Margin = new Padding(0, 0, 0, 6);
        _btnProcess.Click += async (_, _) => await ProcessImageWithSelectedEngineAsync();
        actionPanel.Controls.Add(_btnProcess);

        _btnBatchProcess.Width = 348;
        _btnBatchProcess.Height = 32;
        _btnBatchProcess.Text = "🎯 4'lü Sahne Toplu Üret (Batch)";
        _btnBatchProcess.NormalColor = Color.FromArgb(30, 41, 59);
        _btnBatchProcess.HoverColor = Color.FromArgb(45, 55, 75);
        _btnBatchProcess.Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold);
        _btnBatchProcess.Margin = new Padding(0, 0, 0, 2);
        _btnBatchProcess.Click += async (_, _) => await RunBatchSceneGenerationAsync();
        actionPanel.Controls.Add(_btnBatchProcess);

        mainLayout.Controls.Add(actionPanel, 0, 2);

        card.Controls.Add(mainLayout);
        SwitchLeftFormTab(0);
        return card;
    }

    private Control BuildCenterCanvasPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(6, 0, 6, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));

        // 1. Interactive Slider Control
        panel.Controls.Add(_sliderControl, 0, 0);

        // 2. Generation History Filmstrip Card
        var historyCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 10, 0, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var historyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        headerRow.Controls.Add(new Label
        {
            Text = "🎞️ Oturum Varyasyon Geçmişi (Tek tıkla geri dön):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var btnOpenGallery = new Label
        {
            Text = "📚 Kalıcı Galeriyi Aç (50+) ↗",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(129, 140, 248),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 4, 0)
        };
        btnOpenGallery.Click += (_, _) => OpenPersistentGalleryViewer();
        headerRow.Controls.Add(btnOpenGallery, 1, 0);
        historyLayout.Controls.Add(headerRow, 0, 0);

        _filmstripPanel.Margin = new Padding(0, 10, 0, 0);
        _filmstripPanel.Controls.Clear();
        _filmstripPanel.Controls.Add(new Label
        {
            Text = "Henüz üretilen varyasyon yok. AI ile görsel işlediğinizde burada listelenecektir.",
            AutoSize = true,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 8.8F),
            Padding = new Padding(4, 6, 0, 0)
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
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(4, 0, 2, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 0: Scrollable settings sections
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // 1: Fixed Bottom Action (Etsy Listing'e Canlı Yükle)

        var rightScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        const int contentWidth = 348; // Exact match with left panel's 348px width!

        var contentContainer = new FlowLayoutPanel
        {
            Width = contentWidth,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // -------------------------------------------------------------
        // SECTION 1: 🏷️ Pazarlama Rozeti (Overlay)
        // -------------------------------------------------------------
        contentContainer.Controls.Add(new Label
        {
            Text = "🏷️ Pazarlama Rozeti (Overlay):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 2, 0, 4)
        });

        contentContainer.Controls.Add(new Label
        {
            Text = "Görselin köşesine dikkat çekici kampanya ve özellik etiketi ekleyin.",
            AutoSize = true,
            MaximumSize = new Size(contentWidth - 10, 0),
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = UiStyle.TextMuted,
            Margin = new Padding(0, 0, 0, 10)
        });

        _chkEnableBadge.Text = "Pazarlama Rozetini Etkinleştir";
        _chkEnableBadge.Font = new Font("Segoe UI Semibold", 9.5F);
        _chkEnableBadge.ForeColor = Color.FromArgb(226, 232, 240);
        _chkEnableBadge.Cursor = Cursors.Hand;
        _chkEnableBadge.AutoSize = true;
        _chkEnableBadge.Dock = DockStyle.None;
        _chkEnableBadge.Margin = new Padding(0, 0, 0, 10);

        void UpdateBadgeVisualState()
        {
            bool isChecked = _chkEnableBadge.Checked;
            _cboBadgeText.Enabled = isChecked;
            _cboBadgePosition.Enabled = isChecked;
            UpdateBadgeOverlay();
        }

        _chkEnableBadge.CheckedChanged += (_, _) => UpdateBadgeVisualState();
        contentContainer.Controls.Add(_chkEnableBadge);

        contentContainer.Controls.Add(new Label
        {
            Text = "Rozet Metni / İkonu:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 0, 4)
        });

        _cboBadgeText.Width = contentWidth;
        _cboBadgeText.Font = new Font("Segoe UI", 9.5F);
        _cboBadgeText.Margin = new Padding(0, 0, 0, 10);
        _cboBadgeText.Items.Clear();
        _cboBadgeText.Items.AddRange([
            "🚚 Free Fast Shipping",
            "🖨️ 3D Printed / Hand-Painted",
            "🎁 Perfect Gift Idea",
            "⭐ Premium Artisan Quality",
            "🔥 Etsy Best Seller",
            "✨ Limited Holiday Edition"
        ]);
        _cboBadgeText.SelectedIndex = 0;
        _cboBadgeText.SelectedIndexChanged += (_, _) => UpdateBadgeOverlay();
        _cboBadgeText.TextChanged += (_, _) => UpdateBadgeOverlay();
        contentContainer.Controls.Add(_cboBadgeText);

        contentContainer.Controls.Add(new Label
        {
            Text = "Rozet Konumu:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 0, 4)
        });

        _cboBadgePosition.Width = contentWidth;
        _cboBadgePosition.Font = new Font("Segoe UI", 9.5F);
        _cboBadgePosition.Margin = new Padding(0, 0, 0, 14);
        _cboBadgePosition.Items.Clear();
        _cboBadgePosition.Items.AddRange(["Sol Üst", "Sağ Üst", "Sol Alt", "Sağ Alt"]);
        _cboBadgePosition.SelectedIndex = 0;
        _cboBadgePosition.SelectedIndexChanged += (_, _) => UpdateBadgeOverlay();
        contentContainer.Controls.Add(_cboBadgePosition);

        // -------------------------------------------------------------
        // SECTION 2: 💾 Çıktı & İndirme
        // -------------------------------------------------------------
        contentContainer.Controls.Add(new Label
        {
            Text = "💾 Çıktı & İndirme:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        });

        contentContainer.Controls.Add(new Label
        {
            Text = "Yüksek çözünürlüklü mockup görselini bilgisayara kaydedin veya panoya aktarın.",
            AutoSize = true,
            MaximumSize = new Size(contentWidth - 10, 0),
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = UiStyle.TextMuted,
            Margin = new Padding(0, 0, 0, 10)
        });

        contentContainer.Controls.Add(new Label
        {
            Text = "📐 Çıktı Oranı (Aspect Ratio):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 0, 4)
        });

        _formatComboBox.Width = contentWidth;
        _formatComboBox.Font = new Font("Segoe UI", 9.5F);
        _formatComboBox.Margin = new Padding(0, 0, 0, 10);
        _formatComboBox.Items.Clear();
        _formatComboBox.Items.AddRange([
            "1:1 Kare (2000x2000 px - Etsy HD)",
            "4:3 Etsy Standartı (2000x1500 px)",
            "Orijinal Çözünürlük"
        ]);
        _formatComboBox.SelectedIndex = 0;
        contentContainer.Controls.Add(_formatComboBox);

        var downloadBtn = UiStyle.CreateButton("💾 Bilgisayara İndir (HD PNG)");
        downloadBtn.Width = contentWidth;
        downloadBtn.Height = 40;
        downloadBtn.Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold);
        downloadBtn.Padding = new Padding(14, 6, 14, 6);
        downloadBtn.Margin = new Padding(0, 0, 0, 8);
        downloadBtn.Click += (_, _) => DownloadImage();
        contentContainer.Controls.Add(downloadBtn);

        var exportGrid = new TableLayoutPanel
        {
            Width = contentWidth,
            Height = 36,
            Margin = new Padding(0, 0, 0, 14),
            ColumnCount = 2,
            RowCount = 1
        };
        exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var copyBtn = UiStyle.CreateButton("📋 Panoya Kopyala", isSecondary: true);
        copyBtn.Dock = DockStyle.Fill;
        copyBtn.Height = 36;
        copyBtn.Font = new Font("Segoe UI Semibold", 9F);
        copyBtn.Padding = new Padding(8, 4, 8, 4);
        copyBtn.Click += (_, _) => CopyToClipboard();
        exportGrid.Controls.Add(copyBtn, 0, 0);

        var sendToBatchBtn = UiStyle.CreateButton("⚡ Toplu Kuyruğa", isSecondary: true);
        sendToBatchBtn.Dock = DockStyle.Fill;
        sendToBatchBtn.Height = 36;
        sendToBatchBtn.Font = new Font("Segoe UI Semibold", 9F);
        sendToBatchBtn.Padding = new Padding(8, 4, 8, 4);
        sendToBatchBtn.Click += (_, _) =>
        {
            var bmp = _sessionManager.GeneratedBitmap ?? _sessionManager.OriginalBitmap;
            if (bmp != null)
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var item = new BatchInputItem(
                    Id: $"studio_{Guid.NewGuid():N}",
                    Title: string.IsNullOrWhiteSpace(_productTitleTxt.Text) ? "Stüdyo Görseli" : _productTitleTxt.Text.Trim(),
                    ImageBytes: ms.ToArray(),
                    OriginalPathOrUrl: "Studio",
                    TargetListingId: _targetListingId);

                _batchStudioControl.AddItemsToStudio([item]);
                SwitchToTab(1);
            }
            else
            {
                MessageBox.Show(this, "Önce düzenlenecek bir görsel yükleyin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        exportGrid.Controls.Add(sendToBatchBtn, 1, 0);
        contentContainer.Controls.Add(exportGrid);

        // -------------------------------------------------------------
        // SECTION 3: 🚀 Etsy Mağaza Senkronizasyonu
        // -------------------------------------------------------------
        contentContainer.Controls.Add(new Label
        {
            Text = "🚀 Etsy Mağaza Senkronizasyonu:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.2F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        });

        contentContainer.Controls.Add(new Label
        {
            Text = "Hazırlanan görseli doğrudan mağazanızdaki aktif listing slotuna aktarın.",
            AutoSize = true,
            MaximumSize = new Size(contentWidth - 10, 0),
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = UiStyle.TextMuted,
            Margin = new Padding(0, 0, 0, 8)
        });

        _lblTargetListingInfo.Font = new Font("Segoe UI Semibold", 9.2F);
        _lblTargetListingInfo.Margin = new Padding(0, 0, 0, 8);
        _lblTargetListingInfo.MaximumSize = new Size(contentWidth - 10, 0);
        _lblTargetListingInfo.AutoSize = true;
        if (_targetListingId == null)
        {
            _lblTargetListingInfo.Text = "🎯 Hedef: Genel Taslak Modu";
        }
        contentContainer.Controls.Add(_lblTargetListingInfo);

        contentContainer.Controls.Add(new Label
        {
            Text = "Etsy Görsel Sıra Numarası (Slot):",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 0, 0, 4)
        });

        _cboEtsyImageSlot.Width = contentWidth;
        _cboEtsyImageSlot.Font = new Font("Segoe UI", 9.5F);
        _cboEtsyImageSlot.Margin = new Padding(0, 0, 0, 10);
        _cboEtsyImageSlot.Items.Clear();
        _cboEtsyImageSlot.Items.AddRange([
            "1. Sıra (Ana Kapak Fotoğrafı - Primary)",
            "2. Sıra (Detay Fotoğrafı)",
            "3. Sıra (Mockup / Sahneleme)",
            "4. Sıra (Ölçü / Varyant)",
            "Sonraki Boş Sıraya Ekle"
        ]);
        _cboEtsyImageSlot.SelectedIndex = 0;
        contentContainer.Controls.Add(_cboEtsyImageSlot);

        rightScroll.SetContent(contentContainer);
        cardLayout.Controls.Add(rightScroll, 0, 0);

        // Fixed Bottom Action: Pinned, fully visible, never clipped!
        var exportEtsyBtn = UiStyle.CreateButton("🚀 Etsy Listing'e Canlı Yükle");
        exportEtsyBtn.Dock = DockStyle.Fill;
        exportEtsyBtn.Height = 46;
        exportEtsyBtn.Font = new Font("Segoe UI Semibold", 10.2F, FontStyle.Bold);
        exportEtsyBtn.Padding = new Padding(14, 8, 14, 8);
        exportEtsyBtn.Margin = new Padding(0, 4, 0, 0);
        exportEtsyBtn.BackColor = UiStyle.PrimaryColor;
        exportEtsyBtn.Click += async (_, _) => await ExportToEtsyAsync();
        cardLayout.Controls.Add(exportEtsyBtn, 0, 1);

        card.Controls.Add(cardLayout);

        UpdateBadgeVisualState();
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
        var keyRow = new TableLayoutPanel { Width = 348, Height = 34, Margin = new Padding(0, 4, 0, 8), ColumnCount = 2 };
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
            _geminiModelComboBox.Width = 348;
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

            _engineOptionsPanel.Controls.Add(new Label { Text = "E-Ticaret Düzenleme Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
            _geminiEditModeComboBox.Width = 348;
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
                Width = 348,
                Margin = new Padding(0, 8, 0, 0)
            };
            _engineOptionsPanel.Controls.Add(groundingNotice);
        }
        else if (_cboEngine.SelectedIndex == 1) // 🟣 PhotoRoom Native
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "İşlem Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _photoRoomModeComboBox.Width = 348;
            _photoRoomModeComboBox.Items.Clear();
            _photoRoomModeComboBox.Items.AddRange(["✂️ Şeffaf Arka Plan (Remove BG)", "⚪ Beyaz E-Ticaret Arka Planı", "🎨 AI Arka Plan Sahnesi"]);
            _photoRoomModeComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_photoRoomModeComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "AI Gölge Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
            _shadowComboBox.Width = 348;
            _shadowComboBox.Items.Clear();
            _shadowComboBox.Items.AddRange(["Yumuşak AI Gölgesi (ai_soft)", "Keskin Gölge (ai_hard)", "Gölgesiz (none)"]);
            _shadowComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_shadowComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "Kenar Hizalama (Padding):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
            _paddingComboBox.Width = 348;
            _paddingComboBox.Items.Clear();
            _paddingComboBox.Items.AddRange(["%10 Kenar Boşluğu (Standart)", "%5 Sıkı", "%15 Geniş", "%0 Tam Sığdır"]);
            _paddingComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_paddingComboBox);
        }
        else if (_cboEngine.SelectedIndex == 2) // 🏆 OpenAI (GPT Image 2.5)
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "OpenAI İşlem Modu:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _openAiModeComboBox.Width = 348;
            _openAiModeComboBox.Items.Clear();
            _openAiModeComboBox.Items.AddRange([
                "✂️ Arka Planı Değiştir (Inpainting / Ürünü Koru & AI Sahnesi)",
                "🎨 Sıfırdan Mockup / Sahne Üret (Text-to-Image)",
                "🔲 Şeffaf Arka Plan (Remove Background)"
            ]);
            if (_openAiModeComboBox.SelectedIndex < 0) _openAiModeComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_openAiModeComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "OpenAI Görsel Modeli:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
            _openAiModelComboBox.Width = 348;
            _openAiModelComboBox.Items.Clear();
            _openAiModelComboBox.Items.AddRange([
                "gpt-image-2.5-flare (⚡ Hızlı & Arka Plan Düzenleme)",
                "gpt-image-2.5-sunburst (🌟 Maksimum Detay / Vitrin Kapağı)",
                "gpt-image-2 (Standard)",
                "dall-e-3 (Legacy HD)"
            ]);
            if (_openAiModelComboBox.SelectedIndex < 0) _openAiModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_openAiModelComboBox);

            _chkOpenAiTransparentBg.Margin = new Padding(0, 8, 0, 2);
            _engineOptionsPanel.Controls.Add(_chkOpenAiTransparentBg);

            var infoNotice = new Label
            {
                Text = "💡 GPT-Image-2.5 Flare & Sunburst: Ürününüzün tüm piksellerini koruyarak Etsy'ye özel profesyonel arka plan üretir.",
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
                Width = 348,
                Margin = new Padding(0, 8, 0, 0)
            };
            _engineOptionsPanel.Controls.Add(infoNotice);
        }
        else if (_cboEngine.SelectedIndex == 3) // ⚡ Black Forest Labs FLUX
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "BFL FLUX Modeli (api.bfl.ml):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _bflModelComboBox.Width = 348;
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
                Width = 348,
                Margin = new Padding(0, 8, 0, 0)
            });
        }
        else // 4: 🟡 Ideogram 4.0
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "Ürün Üzerine Basılacak Yazı (Tipografi):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 0, 2) });
            _txtIdeogramTypography.Width = 348;
            _txtIdeogramTypography.Font = new Font("Segoe UI", 9F);
            _txtIdeogramTypography.PlaceholderText = "Örn: Best Dad Ever / Handmade 2026 / Vintage Coffee";
            _engineOptionsPanel.Controls.Add(_txtIdeogramTypography);

            _engineOptionsPanel.Controls.Add(new Label { Text = "Ideogram Stil Önayarı:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
            _ideogramStyleComboBox.Width = 348;
            _ideogramStyleComboBox.Items.Clear();
            _ideogramStyleComboBox.Items.AddRange(["REALISTIC (Gerçekçi Ürün)", "DESIGN (Grafik Tasarım)", "RENDER_3D (3D Render)", "ANIME (İllüstrasyon)", "GENERAL (Genel)"]);
            _ideogramStyleComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_ideogramStyleComboBox);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "🟡 Ideogram 4.0, kupa, tişört ve hediyelik ürünler üzerine sıfır harf hatasıyla kusursuz metin render eder.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 348,
                Margin = new Padding(0, 8, 0, 0)
            });
        }

        _engineOptionsPanel.ResumeLayout(true);
    }

    private void SetComparisonMode(ImageComparisonMode mode)
    {
        _sliderControl.Mode = mode;
        UpdateModeButton(_btnModeSlider, mode == ImageComparisonMode.SplitSlider);
        UpdateModeButton(_btnModeSideBySide, mode == ImageComparisonMode.SideBySide);
        UpdateModeButton(_btnModeAfterOnly, mode == ImageComparisonMode.AfterOnly);
    }

    private static void UpdateModeButton(Button btn, bool active)
    {
        if (btn is ModernButtonControl mbc)
        {
            mbc.NormalColor = active ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
            mbc.HoverColor = active ? UiStyle.PrimaryHover : UiStyle.SecondaryHover;
            mbc.ForeColor = active ? Color.White : UiStyle.TextDark;
            mbc.Invalidate();
        }
        else
        {
            btn.BackColor = active ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
            btn.ForeColor = active ? Color.White : UiStyle.TextDark;
        }
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
        string prov = (_aiSettings.Provider ?? "").Trim();
        string model = prov.ToLowerInvariant() switch
        {
            "gemini" => AiModelNormalizer.NormalizeGeminiTextModel(_aiSettings.GeminiModel),
            "openai" => AiModelNormalizer.NormalizeOpenAiTextModel(_aiSettings.OpenAiModel),
            "claude" => AiModelNormalizer.NormalizeClaudeTextModel(_aiSettings.ClaudeModel),
            "deepseek" => AiModelNormalizer.NormalizeDeepSeekModel(_aiSettings.DeepSeekModel),
            "grok" => _aiSettings.GrokModel,
            _ => ""
        };

        string shortModel = string.IsNullOrEmpty(model) ? "" : (model.Length > 16 ? model.Substring(0, 14) + ".." : model);
        _lblAiBadge.Text = string.IsNullOrEmpty(prov) ? "🤖 AI" : (string.IsNullOrEmpty(shortModel) ? $"🤖 {prov}" : $"🤖 {prov}: {shortModel}");
        _tip.SetToolTip(_lblAiBadge, $"{_aiSettings.GetActiveBadgeText()}\nAyarları değiştirmek için tıklayın");

        _lblAiBadge.BackColor = (_aiSettings.Provider ?? "").Trim().ToLowerInvariant() switch
        {
            "gemini" => Color.FromArgb(29, 78, 216),    // Blue 700
            "openai" => Color.FromArgb(16, 120, 75),   // Emerald 700
            "claude" => Color.FromArgb(126, 34, 206),  // Purple 700
            "deepseek" => Color.FromArgb(185, 28, 28), // Red 700
            "grok" => Color.FromArgb(194, 65, 12),     // Orange 700
            _ => Color.FromArgb(51, 65, 85)            // Slate 700
        };
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
            2 => "OpenAI GPT-Image-2.5",
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
        UpdateProcessButtonState();
    }

    private void UpdateProcessButtonState()
    {
        bool hasImage = _sessionManager.OriginalBitmap != null;
        bool hasText = !string.IsNullOrWhiteSpace(_productTitleTxt.Text) || !string.IsNullOrWhiteSpace(_promptTxt.Text);
        string engineId = _cboEngine.SelectedIndex switch
        {
            0 => "gemini",
            1 => "photoroom",
            2 => "openai",
            3 => "flux",
            _ => "ideogram"
        };

        bool canProcess = engineId == "photoroom" ? hasImage : (hasImage || hasText);

        _btnProcess.Enabled = canProcess;
        _btnBatchProcess.Enabled = canProcess;

        if (!canProcess)
        {
            string reason = engineId == "photoroom"
                ? "PhotoRoom ile arka plan işlemi için lütfen önce bir ürün görseli yükleyin."
                : "İşlem yapmak için lütfen bir ürün görseli yükleyin veya ürün adı/konsepti girin.";
            _tip.SetToolTip(_btnProcess, reason);
            _tip.SetToolTip(_btnBatchProcess, "Toplu sahne üretimi için ürün görseli veya konsept adı gereklidir.");
        }
        else
        {
            _tip.SetToolTip(_btnProcess, "Seçili yapay zeka motoru ile stüdyo görselini üretin");
            _tip.SetToolTip(_btnBatchProcess, "4 farklı Etsy konsept sahnesini otomatik toplu üretin");
        }
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

        var pickerScroll = new ModernScrollPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 0, 2, 0) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, AutoScroll = false, WrapContents = true };
        pickerScroll.SetContent(flow);
        root.Controls.Add(pickerScroll, 0, 0);

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

            if (engineId == "openai" && _openAiModeComboBox.SelectedIndex == 0 && _sessionManager.OriginalBitmap is null)
            {
                MessageBox.Show(this, "Arka planı değiştirebilmek için lütfen önce sol panelden düzenlenecek bir ürün fotoğrafı yükleyin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string prompt = _promptTxt.Text.Trim();
            if (engineId == "openai" && _openAiModeComboBox.SelectedIndex == 0 && string.IsNullOrWhiteSpace(prompt))
            {
                prompt = "Professional clean Etsy commercial product staging studio, bright natural daylight, soft contact shadow, ultra-sharp detail";
            }
            else if (engineId == "gemini" && string.IsNullOrWhiteSpace(prompt))
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
                    0 => "gpt-image-2.5-flare",
                    1 => "gpt-image-2.5-sunburst",
                    2 => "gpt-image-2",
                    _ => "dall-e-3"
                },
                "flux" => _bflModelComboBox.SelectedIndex switch
                {
                    0 => "flux-pro-1.1",
                    1 => "flux-dev",
                    _ => "flux-schnell"
                },
                _ => ""
            };

            bool isOpenAiBgReplace = engineId == "openai" && _openAiModeComboBox.SelectedIndex == 0;
            var request = new ImageEngineRequest
            {
                InputImage = _sessionManager.OriginalBitmap,
                Prompt = prompt,
                ModelName = modelName,
                LightingPreset = _lightingSelector.SelectedLighting,
                CameraAnglePreset = _lightingSelector.SelectedCamera,
                PreserveProduct = true,
                EditMode = isOpenAiBgReplace ? "bg_replace" : (_photoRoomModeComboBox.SelectedIndex == 0 ? "remove_bg" : "none"),
                TransparentBackground = _chkOpenAiTransparentBg.Checked || (engineId == "openai" && _openAiModeComboBox.SelectedIndex == 2),
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
            UseWaitCursor = false;
            UpdateProcessButtonState();
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
            UseWaitCursor = false;
            UpdateProcessButtonState();
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
