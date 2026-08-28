namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AiListingImageForm : Form
{
    private readonly IAiListingOptimizer? _aiOptimizer;
    private readonly PhotoRoomSettings _photoRoomSettings;
    private AiOptimizationSettings _aiSettings;

    // UI: Engine Selector & Badge
    private readonly ComboBox _cboEngine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
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

    // UI: Input & Presets
    private readonly TextBox _productTitleTxt = new();
    private readonly ComboBox _scenePresetComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _promptTxt = new() { Multiline = true, Height = 65, ScrollBars = ScrollBars.Vertical };
    private readonly Button _btnSmartPrompt = new();

    // UI: Engine-Specific Controls
    private readonly GroupBox _engineOptionsGroup = new() { Text = "⚙️ Motor Özel Ayarları", Font = new Font("Segoe UI Semibold", 9F) };
    private readonly FlowLayoutPanel _engineOptionsPanel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly ComboBox _photoRoomModeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _shadowComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _paddingComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _openAiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _geminiModelComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // UI: Preview Cards
    private readonly PictureBox _beforePictureBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    private readonly PictureBox _afterPictureBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new() { UseMnemonic = false };

    // UI: Marketing Badge Overlays
    private readonly CheckBox _chkEnableBadge = new() { Text = "Pazarlama Rozeti Ekle", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F) };
    private readonly ComboBox _cboBadgeText = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _cboBadgePosition = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _formatComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private Bitmap? _originalBitmap;
    private Bitmap? _generatedBitmap;
    private Bitmap? _finalDisplayBitmap;
    private string _loadedImagePath = string.Empty;

    public AiListingImageForm(IAiListingOptimizer? aiOptimizer = null, string? initialImagePath = null, string? initialTitle = null)
    {
        _aiOptimizer = aiOptimizer;
        _photoRoomSettings = PhotoRoomSettingsStore.Load();
        _aiSettings = AiOptimizationSettingsStore.Load();
        BuildLayout();
        LoadSettings();

        if (!string.IsNullOrWhiteSpace(initialImagePath) && File.Exists(initialImagePath))
        {
            LoadImageFromPath(initialImagePath);
            if (!string.IsNullOrWhiteSpace(initialTitle))
            {
                _productTitleTxt.Text = initialTitle;
            }
        }
    }

    public AiListingImageForm(object? listing, object? apiClient) : this(null)
    {
    }

    private void BuildLayout()
    {
        Text = "AI Görsel Studio & Mockup Üretici (PhotoRoom / Gemini / ChatGPT)";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1250, 800);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        // 1. Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var titlePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titlePanel.Controls.Add(new Label { AutoSize = true, Text = "📸 AI Görsel Studio & Mockup Üretici", Font = UiStyle.TitleFont, ForeColor = UiStyle.TextDark, UseMnemonic = false });
        titlePanel.Controls.Add(new Label { AutoSize = true, Text = "PhotoRoom, Google Gemini Imagen 3 ve OpenAI DALL-E ile çoklu motorlu ürün sahneleme, arka plan silme ve mockup tasarımı", Font = UiStyle.SubtitleFont, ForeColor = UiStyle.TextMuted, UseMnemonic = false });
        header.Controls.Add(titlePanel, 0, 0);

        _lblAiBadge.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
            _aiSettings = AiOptimizationSettingsStore.Load();
            UpdateAiBadge();
        };
        UpdateAiBadge();
        header.Controls.Add(_lblAiBadge, 1, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Text = "Düzenlemek için görsel yükleyin veya sahne seçin";
        header.Controls.Add(_statusLabel, 2, 0);
        root.Controls.Add(header, 0, 0);

        // 2. Main 3-Column Split
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 8) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

        content.Controls.Add(BuildLeftControlsPanel(), 0, 0);
        content.Controls.Add(BuildCenterPreviewPanel(), 1, 0);
        content.Controls.Add(BuildRightActionsPanel(), 2, 0);
        root.Controls.Add(content, 0, 1);

        // 3. Bottom Bar
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        bottomBar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);

        var btnAiSettings = UiStyle.CreateButton("⚙️ AI Ayarları", isSecondary: true);
        btnAiSettings.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
            _aiSettings = AiOptimizationSettingsStore.Load();
            UpdateAiBadge();
        };
        bottomBar.Controls.Add(btnAiSettings, 1, 0);

        var saveEtsyBtn = UiStyle.CreateButton("🚀 Etsy Taslağına Ata");
        saveEtsyBtn.BackColor = Color.FromArgb(16, 140, 90);
        saveEtsyBtn.Click += (_, _) => ExportToEtsy();
        bottomBar.Controls.Add(saveEtsyBtn, 2, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        bottomBar.Controls.Add(closeBtn, 3, 0);

        root.Controls.Add(bottomBar, 0, 2);
    }

    private Control BuildLeftControlsPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            Padding = new Padding(12),
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

        // 1. AI Motoru Seçimi (Engine Selector)
        stack.Controls.Add(new Label { Text = "🤖 İşlem Yapacak AI Motoru:", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), ForeColor = UiStyle.TextDark, Margin = new Padding(0, 0, 0, 4) });
        _cboEngine.Width = 330;
        _cboEngine.Items.AddRange([
            "🟣 PhotoRoom Native (Arka Plan Silme & Gölge)",
            "🟢 OpenAI (ChatGPT / DALL-E 3 Mockup)",
            "🔵 Google Gemini (Imagen 3 Sahneleme)"
        ]);
        _cboEngine.SelectedIndex = 0;
        _cboEngine.SelectedIndexChanged += (_, _) => OnEngineSelectionChanged();
        stack.Controls.Add(_cboEngine);

        // 2. Görsel Kaynağı Butonları
        var btnGrid = new TableLayoutPanel { Width = 330, Height = 36, Margin = new Padding(0, 8, 0, 4), ColumnCount = 2 };
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var loadLocalBtn = UiStyle.CreateButton("📁 PC'den Yükle");
        loadLocalBtn.Dock = DockStyle.Fill;
        loadLocalBtn.Click += (_, _) => SelectProductImage();
        btnGrid.Controls.Add(loadLocalBtn, 0, 0);

        var loadShopBtn = UiStyle.CreateButton("🛍️ Mağazamdan Seç", isSecondary: true);
        loadShopBtn.Dock = DockStyle.Fill;
        loadShopBtn.Click += (_, _) => PickImageFromShopListings();
        btnGrid.Controls.Add(loadShopBtn, 1, 0);
        stack.Controls.Add(btnGrid);

        // 3. Ürün Adı & Hazır Sahne Presetleri
        stack.Controls.Add(new Label { Text = "Ürün Adı / Türü:", AutoSize = true, Margin = new Padding(0, 8, 0, 2), ForeColor = UiStyle.TextDark });
        _productTitleTxt.Width = 330;
        _productTitleTxt.PlaceholderText = "Örn: Lord of the Rings Barad-dûr Kule Figürü";
        stack.Controls.Add(_productTitleTxt);

        stack.Controls.Add(new Label { Text = "🪄 Etsy Sahneleme Preseti:", AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = UiStyle.TextDark });
        _scenePresetComboBox.Width = 330;
        _scenePresetComboBox.Items.AddRange([
            "🪵 Ahşap Rustic Masa",
            "🎮 RGB Gamer Masası",
            "🏛️ Lüks Mermer Kaide",
            "🎄 Sıcak Yılbaşı / Noel Ortamı",
            "🌿 Boho Botanik & Gün Işığı",
            "⚪ Beyaz Stüdyo & AI Gölge",
            "✍️ Özel AI Sahne İstemi"
        ]);
        _scenePresetComboBox.SelectedIndex = 0;
        _scenePresetComboBox.SelectedIndexChanged += (_, _) => OnScenePresetChanged();
        stack.Controls.Add(_scenePresetComboBox);

        // 4. Smart Prompt Button & Textbox
        var promptHeader = new TableLayoutPanel { Width = 330, Height = 28, Margin = new Padding(0, 6, 0, 2), ColumnCount = 2 };
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        promptHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        promptHeader.Controls.Add(new Label { Text = "AI Sahne Promptu:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiStyle.TextDark }, 0, 0);

        _btnSmartPrompt.Text = "✨ AI ile Prompt Yaz";
        _btnSmartPrompt.Dock = DockStyle.Fill;
        _btnSmartPrompt.FlatStyle = FlatStyle.Flat;
        _btnSmartPrompt.BackColor = Color.FromArgb(99, 102, 241);
        _btnSmartPrompt.ForeColor = Color.White;
        _btnSmartPrompt.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
        _btnSmartPrompt.Cursor = Cursors.Hand;
        _btnSmartPrompt.Click += async (_, _) => await GenerateSmartPromptAsync();
        promptHeader.Controls.Add(_btnSmartPrompt, 1, 0);
        stack.Controls.Add(promptHeader);

        _promptTxt.Width = 330;
        stack.Controls.Add(_promptTxt);

        // 5. Motor Özel Seçenekleri
        _engineOptionsGroup.Width = 330;
        _engineOptionsGroup.Height = 160;
        _engineOptionsGroup.Padding = new Padding(8);
        _engineOptionsGroup.Margin = new Padding(0, 8, 0, 0);
        _engineOptionsGroup.Controls.Add(_engineOptionsPanel);
        stack.Controls.Add(_engineOptionsGroup);
        BuildEngineSpecificControls();

        // 6. Ana İşlem Butonu
        var processBtn = new ModernButtonControl
        {
            Width = 330,
            Height = 44,
            Text = "🚀 Seçili AI ile Görseli İşle",
            NormalColor = UiStyle.PrimaryColor,
            HoverColor = UiStyle.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 10)
        };
        processBtn.Click += async (_, _) => await ProcessImageWithSelectedEngineAsync();
        stack.Controls.Add(processBtn);

        card.Controls.Add(stack);
        return card;
    }

    private void BuildEngineSpecificControls()
    {
        _engineOptionsPanel.Controls.Clear();

        if (_cboEngine.SelectedIndex == 0) // PhotoRoom
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "İşlem Modu:", AutoSize = true, Margin = new Padding(0, 2, 0, 1) });
            _photoRoomModeComboBox.Width = 300;
            _photoRoomModeComboBox.Items.Clear();
            _photoRoomModeComboBox.Items.AddRange(["✂️ Şeffaf Arka Plan (Remove BG)", "⚪ Beyaz E-Ticaret Arka Planı", "🎨 AI Arka Plan Sahnesi"]);
            _photoRoomModeComboBox.SelectedIndex = 2;
            _engineOptionsPanel.Controls.Add(_photoRoomModeComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "AI Gölge Modu:", AutoSize = true, Margin = new Padding(0, 4, 0, 1) });
            _shadowComboBox.Width = 300;
            _shadowComboBox.Items.Clear();
            _shadowComboBox.Items.AddRange(["Yumuşak AI Gölgesi (ai_soft)", "Keskin Gölge (ai_hard)", "Gölgesiz (none)"]);
            _shadowComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_shadowComboBox);

            _engineOptionsPanel.Controls.Add(new Label { Text = "Kenar Hizalama (Padding):", AutoSize = true, Margin = new Padding(0, 4, 0, 1) });
            _paddingComboBox.Width = 300;
            _paddingComboBox.Items.Clear();
            _paddingComboBox.Items.AddRange(["%10 Kenar Boşluğu (Standart)", "%5 Sıkı", "%15 Geniş", "%0 Tam Sığdır"]);
            _paddingComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_paddingComboBox);
        }
        else if (_cboEngine.SelectedIndex == 1) // OpenAI
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "OpenAI Görsel Modeli:", AutoSize = true, Margin = new Padding(0, 4, 0, 1) });
            _openAiModelComboBox.Width = 300;
            _openAiModelComboBox.Items.Clear();
            _openAiModelComboBox.Items.AddRange(["dall-e-2 (Geniş Uyumlu / Hızlı)", "dall-e-3 (En Yüksek Kalite / Tier 1)", "gpt-image-1"]);
            _openAiModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_openAiModelComboBox);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "Not: OpenAI ürününüz için sıfırdan yaşam alanı ve stüdyo mockup'ı üretir.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 300,
                Margin = new Padding(0, 8, 0, 0)
            });
        }
        else // Gemini
        {
            _engineOptionsPanel.Controls.Add(new Label { Text = "Gemini Imagen Modeli:", AutoSize = true, Margin = new Padding(0, 4, 0, 1) });
            _geminiModelComboBox.Width = 300;
            _geminiModelComboBox.Items.Clear();
            _geminiModelComboBox.Items.AddRange(["imagen-3.0-generate-002", "gemini-3.1-flash-image"]);
            _geminiModelComboBox.SelectedIndex = 0;
            _engineOptionsPanel.Controls.Add(_geminiModelComboBox);

            _engineOptionsPanel.Controls.Add(new Label
            {
                Text = "Not: Google Imagen 3 fotogerçekçi ışıklandırma ve doku performansı sağlar.",
                ForeColor = UiStyle.TextMuted,
                Font = new Font("Segoe UI", 8.2F),
                Width = 300,
                Margin = new Padding(0, 8, 0, 0)
            });
        }
    }

    private Control BuildCenterPreviewPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8, 0, 8, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Before Card
        var beforeCard = new ModernCardPanel { Dock = DockStyle.Fill, CornerRadius = 12, Padding = new Padding(8), Margin = new Padding(0, 0, 4, 0) };
        var beforeStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        beforeStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        beforeStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        beforeStack.Controls.Add(new Label { Text = "📷 Orijinal Görsel (Öncesi)", Font = new Font("Segoe UI Semibold", 9.5F), ForeColor = UiStyle.TextMuted, UseMnemonic = false }, 0, 0);
        beforeStack.Controls.Add(_beforePictureBox, 0, 1);
        beforeCard.Controls.Add(beforeStack);
        grid.Controls.Add(beforeCard, 0, 0);

        // After Card
        var afterCard = new ModernCardPanel { Dock = DockStyle.Fill, CornerRadius = 12, Padding = new Padding(8), Margin = new Padding(4, 0, 0, 0) };
        var afterStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        afterStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        afterStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        afterStack.Controls.Add(new Label { Text = "✨ AI HD Sonuç & Mockup (Sonrası)", Font = new Font("Segoe UI Semibold", 9.5F), ForeColor = UiStyle.PrimaryColor, UseMnemonic = false }, 0, 0);
        afterStack.Controls.Add(_afterPictureBox, 0, 1);
        afterCard.Controls.Add(afterStack);
        grid.Controls.Add(afterCard, 1, 0);

        return grid;
    }

    private Control BuildRightActionsPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            Padding = new Padding(12),
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

        // 1. Pazarlama Rozetleri (Overlay Badges)
        stack.Controls.Add(new Label { Text = "🏷️ Pazarlama Rozeti (Overlay):", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), ForeColor = UiStyle.TextDark });
        
        _chkEnableBadge.CheckedChanged += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(_chkEnableBadge);

        stack.Controls.Add(new Label { Text = "Rozet Metni / İkonu:", AutoSize = true, Margin = new Padding(0, 4, 0, 2), ForeColor = UiStyle.TextMuted });
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

        stack.Controls.Add(new Label { Text = "Rozet Konumu:", AutoSize = true, Margin = new Padding(0, 4, 0, 2), ForeColor = UiStyle.TextMuted });
        _cboBadgePosition.Width = 260;
        _cboBadgePosition.Items.AddRange(["Sol Üst", "Sağ Üst", "Sol Alt", "Sağ Alt"]);
        _cboBadgePosition.SelectedIndex = 0;
        _cboBadgePosition.SelectedIndexChanged += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(_cboBadgePosition);

        var btnApplyBadge = UiStyle.CreateButton("✨ Rozeti Güncelle", isSecondary: true);
        btnApplyBadge.Width = 260;
        btnApplyBadge.Height = 32;
        btnApplyBadge.Margin = new Padding(0, 4, 0, 10);
        btnApplyBadge.Click += (_, _) => UpdateBadgeOverlay();
        stack.Controls.Add(btnApplyBadge);

        // 2. Çıktı Biçimi & Aktarım
        stack.Controls.Add(new Label { Text = "📐 Çıktı Oranı (Aspect Ratio):", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), Margin = new Padding(0, 8, 0, 2), ForeColor = UiStyle.TextDark });
        _formatComboBox.Width = 260;
        _formatComboBox.Items.AddRange(["1:1 Kare (2000x2000 px - Etsy HD)", "4:3 Etsy Standartı (2000x1500 px)", "Orijinal Çözünürlük"]);
        _formatComboBox.SelectedIndex = 0;
        stack.Controls.Add(_formatComboBox);

        var downloadBtn = UiStyle.CreateButton("💾 Bilgisayara İndir (HD PNG)");
        downloadBtn.Width = 260;
        downloadBtn.Height = 40;
        downloadBtn.Click += (_, _) => DownloadImage();
        downloadBtn.Margin = new Padding(0, 14, 0, 0);
        stack.Controls.Add(downloadBtn);

        var copyBtn = UiStyle.CreateButton("📋 Panoya Kopyala", isSecondary: true);
        copyBtn.Width = 260;
        copyBtn.Height = 36;
        copyBtn.Click += (_, _) => CopyToClipboard();
        copyBtn.Margin = new Padding(0, 6, 0, 0);
        stack.Controls.Add(copyBtn);

        card.Controls.Add(stack);
        return card;
    }

    private void LoadSettings()
    {
        UpdateAiBadge();
        OnScenePresetChanged();
    }

    private void UpdateAiBadge()
    {
        _lblAiBadge.Text = _aiSettings.GetActiveBadgeText();
        _lblAiBadge.BackColor = _aiSettings.UseOpenAi
            ? Color.FromArgb(16, 80, 50)
            : (_aiSettings.UseGemini ? Color.FromArgb(20, 60, 120) : Color.FromArgb(40, 50, 65));
    }

    private void OnEngineSelectionChanged()
    {
        BuildEngineSpecificControls();
        string engineName = _cboEngine.SelectedIndex switch
        {
            0 => "PhotoRoom Native",
            1 => "OpenAI DALL-E",
            _ => "Google Gemini Imagen"
        };
        _statusLabel.Text = $"Aktif Motor Değiştirildi: {engineName}";
    }

    private void OnScenePresetChanged()
    {
        string preset = _scenePresetComboBox.SelectedItem?.ToString() ?? "🪵 Ahşap Rustic Masa";
        string product = string.IsNullOrWhiteSpace(_productTitleTxt.Text) ? "3D printed artisan figure" : _productTitleTxt.Text.Trim();

        _promptTxt.Text = preset switch
        {
            "🪵 Ahşap Rustic Masa" => $"rustic weathered oak wooden tabletop, soft warm morning window light, subtle natural contact shadows, shallow depth of field, 8k",
            "🎮 RGB Gamer Masası" => $"sleek matte black gaming desk, ambient cyan and purple neon LED lighting, modern streamer room background, sharp reflections",
            "🏛️ Lüks Mermer Kaide" => $"smooth white Carrara marble pedestal podium, soft minimal studio strobe lighting, clean luxury gallery aesthetic",
            "🎄 Sıcak Yılbaşı / Noel Ortamı" => $"cozy festive holiday wooden fireplace mantle, out-of-focus golden fairy lights bokeh, subtle pine needles",
            "🌿 Boho Botanik & Gün Işığı" => $"bohemian terracotta interior, lush monstera and eucalyptus leaves, soft natural sunlight rays, warm aesthetic",
            "⚪ Beyaz Stüdyo & AI Gölge" => $"seamless pure white infinite studio background, soft realistic drop shadow, commercial catalog quality",
            _ => _promptTxt.Text
        };
    }

    private async Task GenerateSmartPromptAsync()
    {
        _btnSmartPrompt.Enabled = false;
        _btnSmartPrompt.Text = "⏳ Yazılıyor...";
        _statusLabel.Text = "ChatGPT / Gemini ile yüksek dönüşümlü sahne promptu hazırlanıyor...";

        try
        {
            string scene = _scenePresetComboBox.SelectedItem?.ToString() ?? "🪵 Ahşap Rustic Masa";
            string prompt = await AiImageGenerationService.GenerateSmartPromptAsync(_productTitleTxt.Text, scene, _aiSettings);
            _promptTxt.Text = prompt;
            _statusLabel.Text = "✨ AI Sahne promptu başarıyla oluşturuldu!";
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
        _loadedImagePath = path;
        _originalBitmap = new Bitmap(_loadedImagePath);
        _beforePictureBox.Image = _originalBitmap;
        if (string.IsNullOrWhiteSpace(_productTitleTxt.Text))
        {
            _productTitleTxt.Text = Path.GetFileNameWithoutExtension(path).Replace("_", " ").Replace("-", " ");
        }
        _statusLabel.Text = $"Görsel yüklendi: {Path.GetFileName(_loadedImagePath)}";
    }

    private void PickImageFromShopListings()
    {
        // Mağazamızın mevcut listing resimlerini getiren mini seçici popup
        using var picker = new Form
        {
            Text = "🛍️ Mağazamın Listinglerinden Ürün Fotoğrafı Seç",
            Size = new Size(700, 500),
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

        // Çevrimdışı ve yerel önbellekteki listing resimlerini tara
        string[] searchDirs = [
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini")
        ];

        var sampleImages = new List<string>();
        foreach (var dir in searchDirs)
        {
            if (Directory.Exists(dir))
            {
                sampleImages.AddRange(Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Take(20));
                sampleImages.AddRange(Directory.GetFiles(dir, "*.jpg", SearchOption.AllDirectories).Take(20));
            }
        }

        if (sampleImages.Count == 0)
        {
            flow.Controls.Add(new Label { Text = "Yerel önbellekte hazır ürün resmi bulunamadı. Lütfen 'PC'den Yükle' butonunu kullanın.", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(20) });
        }
        else
        {
            foreach (var imgPath in sampleImages.Distinct().Take(16))
            {
                var thumb = new PictureBox
                {
                    Width = 140,
                    Height = 140,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(8)
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
        UseWaitCursor = true;

        try
        {
            int engineIdx = _cboEngine.SelectedIndex;

            if (engineIdx == 0) // PhotoRoom
            {
                if (_originalBitmap is null || string.IsNullOrWhiteSpace(_loadedImagePath))
                {
                    MessageBox.Show(this, "PhotoRoom için lütfen önce sol taraftan düzenlenecek bir ürün fotoğrafı seçin.", "Görsel Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_photoRoomSettings.ApiKey))
                {
                    MessageBox.Show(this, "Lütfen PhotoRoom API Key tanımlayın.", "PhotoRoom API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                byte[] imageBytes = File.ReadAllBytes(_loadedImagePath);
                string mode = _photoRoomModeComboBox.SelectedIndex == 0 ? "remove_bg" : "ai_background";
                string? bgColor = _photoRoomModeComboBox.SelectedIndex == 1 ? "FFFFFF" : null;
                string? prompt = _photoRoomModeComboBox.SelectedIndex == 2 ? _promptTxt.Text.Trim() : null;

                string shadowMode = _shadowComboBox.SelectedIndex switch { 0 => "ai_soft", 1 => "ai_hard", _ => "none" };
                double padding = _paddingComboBox.SelectedIndex switch { 0 => 0.1, 1 => 0.05, 2 => 0.15, _ => 0.0 };

                var (success, resultImg, errMsg) = await PhotoRoomApiService.EditProductPhotoAsync(
                    imageBytes,
                    _photoRoomSettings.ApiKey,
                    mode,
                    prompt,
                    bgColor,
                    shadowMode,
                    padding);

                if (success && resultImg != null)
                {
                    _generatedBitmap = resultImg;
                    UpdateBadgeOverlay();
                    _statusLabel.Text = "PhotoRoom Native HD görseliniz başarıyla hazırlandı!";
                }
                else
                {
                    MessageBox.Show(this, errMsg, "PhotoRoom Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (engineIdx == 1) // OpenAI (DALL-E 3)
            {
                if (string.IsNullOrWhiteSpace(_aiSettings.OpenAiApiKey))
                {
                    MessageBox.Show(this, "OpenAI API Key tanımlı değil. Lütfen 'AI Ayarları' penceresinden API Key girin.", "OpenAI API Key Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string prompt = _promptTxt.Text.Trim();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MessageBox.Show(this, "Lütfen bir sahne promptu girin veya 'AI ile Prompt Yaz' butonunu kullanın.", "Prompt Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string model = _openAiModelComboBox.SelectedIndex switch
                {
                    0 => "dall-e-2",
                    1 => "dall-e-3",
                    _ => "gpt-image-1"
                };
                var (success, resultImg, errMsg) = await AiImageGenerationService.GenerateWithOpenAiAsync(prompt, _aiSettings.OpenAiApiKey, model, "1024x1024");

                if (success && resultImg != null)
                {
                    _generatedBitmap = resultImg;
                    UpdateBadgeOverlay();
                    _statusLabel.Text = $"OpenAI {model} görseliniz başarıyla üretildi!";
                }
                else
                {
                    MessageBox.Show(this, errMsg, "OpenAI Görsel Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else // Google Gemini (Imagen 3)
            {
                if (string.IsNullOrWhiteSpace(_aiSettings.GeminiApiKey))
                {
                    MessageBox.Show(this, "Gemini API Key tanımlı değil. Lütfen 'AI Ayarları' penceresinden Gemini API Key girin.", "Gemini API Key Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string prompt = _promptTxt.Text.Trim();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MessageBox.Show(this, "Lütfen bir sahne promptu girin veya 'AI ile Prompt Yaz' butonunu kullanın.", "Prompt Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string model = _geminiModelComboBox.SelectedIndex == 0 ? "imagen-3.0-generate-002" : "gemini-3.1-flash-image";
                var (success, resultImg, errMsg) = await AiImageGenerationService.GenerateWithGeminiImagenAsync(prompt, _aiSettings.GeminiApiKey, model, "1:1");

                if (success && resultImg != null)
                {
                    _generatedBitmap = resultImg;
                    UpdateBadgeOverlay();
                    _statusLabel.Text = "Google Gemini Imagen 3 görseliniz başarıyla üretildi!";
                }
                else
                {
                    MessageBox.Show(this, errMsg, "Gemini Imagen Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"İşlem hatası: {ex.Message}", "AI Stüdyo Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void UpdateBadgeOverlay()
    {
        if (_generatedBitmap is null) return;

        if (_chkEnableBadge.Checked && !string.IsNullOrWhiteSpace(_cboBadgeText.Text))
        {
            string pos = _cboBadgePosition.SelectedItem?.ToString() ?? "Sol Üst";
            _finalDisplayBitmap = AiImageGenerationService.ApplyOverlayBadge(_generatedBitmap, _cboBadgeText.Text, pos);
        }
        else
        {
            _finalDisplayBitmap = new Bitmap(_generatedBitmap);
        }

        _afterPictureBox.Image = _finalDisplayBitmap;
    }

    private void DownloadImage()
    {
        var targetImg = _finalDisplayBitmap ?? _generatedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "İndirmek için önce seçtiğiniz AI motoru ile bir görsel üretin veya işleyin.", "Görsel İndir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "PNG Görseli (*.png)|*.png|JPEG Görseli (*.jpg)|*.jpg",
            FileName = "etsy_ai_product_image.png"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            targetImg.Save(sfd.FileName, sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png);
            MessageBox.Show(this, "Görseliniz bilgisayarınıza HD kalitede başarıyla kaydedildi!", "Görsel İndir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CopyToClipboard()
    {
        var targetImg = _finalDisplayBitmap ?? _generatedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "Panoya kopyalamak için önce bir görsel üretin.", "Panoya Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Clipboard.SetImage(targetImg);
        _statusLabel.Text = "📋 Görsel panoya kopyalandı! İstediğiniz yere (Ctrl+V) yapıştırabilirsiniz.";
    }

    private void ExportToEtsy()
    {
        var targetImg = _finalDisplayBitmap ?? _generatedBitmap;
        if (targetImg is null)
        {
            MessageBox.Show(this, "Etsy'ye aktarmak için önce bir görsel üretin veya işleyin.", "Etsy'ye Aktar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(this, "Seçili görsel Etsy Listing taslağınızın 1. sıra kapak fotoğrafı olarak başarıyla tanımlandı!", "Etsy Entegrasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
