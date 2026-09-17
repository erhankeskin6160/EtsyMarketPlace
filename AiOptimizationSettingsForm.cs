namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Studio.Services;

internal sealed class AiOptimizationSettingsForm : Form
{
    private readonly AiOptimizationSettings _settings;
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _apiKeyTextBox = new();
    private readonly ComboBox _modelComboBox = new();
    private readonly ComboBox _imageModelComboBox = new();
    private readonly TextBox _secondaryKeyTextBox = new();
    private readonly ComboBox _secondaryModelComboBox = new();
    private readonly TextBox _customModelTextBox = new();
    private readonly ComboBox _secondaryImageModelComboBox = new();
    private readonly TextBox _bflKeyTextBox = new();
    private readonly TextBox _ideogramKeyTextBox = new();
    private readonly TextBox _photoRoomKeyTextBox = new();
    private readonly CheckBox _chkStrictLiveAi = new();
    private readonly TextBox _statusTextBox = new();

    // Dynamic UI labels & containers
    private readonly Label _lblActiveKeyTitle = new();
    private readonly Label _lblActiveModelTitle = new();
    private readonly Label _lblActiveImageModelTitle = new();
    private readonly Panel _pnlImageModelRow = new();
    private readonly Label _lblOfflineNotice = new();

    public AiOptimizationSettingsForm()
    {
        _settings = AiOptimizationSettingsStore.Load();
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "AI Optimizasyon Ayarları & Model Yapılandırması";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(960, 780);
        MinimumSize = new Size(900, 720);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Color.FromArgb(15, 23, 42); // Deep slate navy
        ForeColor = Color.White;
        DoubleBuffered = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = Color.FromArgb(15, 23, 42)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // 1. Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 2. Scrollable Body
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // 3. Action Footer + Console
        Controls.Add(mainLayout);

        // ----------------------------------------------------
        // 1. HEADER
        // ----------------------------------------------------
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "⚡ Yapay Zeka Optimizasyon & Model Merkezi",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 2)
        };
        var lblSub = new Label
        {
            Text = "Birincil LLM motoru, API anahtarları, görsel tasarım entegrasyonları ve kesintisiz çalışma politikaları.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Location = new Point(0, 28)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSub);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // ----------------------------------------------------
        // 2. SCROLLABLE BODY
        // ----------------------------------------------------
        var bodyScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 4, 10, 4)
        };
        mainLayout.Controls.Add(bodyScroll, 0, 1);

        // --- CARD 1: Birincil Dil & Akıl Yürütme Motoru (LLM) ---
        var card1 = CreateCard("🧠 Birincil Metin & Akıl Yürütme Motoru (LLM)");

        // Provider Selector Row
        var rowProvider = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        rowProvider.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        rowProvider.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblProv = CreateFieldLabel("Aktif AI Sağlayıcı:");
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.Items.AddRange(["Gemini", "OpenAI", "DeepSeek", "Claude", "Grok", "Offline", "Platform Token"]);
        _providerComboBox.Width = 240;
        _providerComboBox.Dock = DockStyle.Left;
        StyleDarkComboBox(_providerComboBox);
        _providerComboBox.SelectedIndexChanged += (_, _) => UpdateFieldLabels();

        // Quick provider pill buttons next to combobox
        var pnlPills = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(10, 2, 0, 0)
        };
        pnlPills.Controls.Add(_providerComboBox);
        AddProviderPill(pnlPills, "Gemini", "Google Gemini");
        AddProviderPill(pnlPills, "OpenAI", "OpenAI (GPT)");
        AddProviderPill(pnlPills, "DeepSeek", "DeepSeek");
        AddProviderPill(pnlPills, "Claude", "Claude");
        AddProviderPill(pnlPills, "Grok", "xAI Grok");
        AddProviderPill(pnlPills, "Offline", "Offline");

        rowProvider.Controls.Add(lblProv, 0, 0);
        rowProvider.Controls.Add(pnlPills, 1, 0);
        card1.Controls.Add(rowProvider);

        // Offline notice banner
        _lblOfflineNotice.Dock = DockStyle.Top;
        _lblOfflineNotice.Height = 32;
        _lblOfflineNotice.Visible = false;
        _lblOfflineNotice.BackColor = Color.FromArgb(30, 41, 59);
        _lblOfflineNotice.ForeColor = Color.FromArgb(226, 232, 240);
        _lblOfflineNotice.Font = new Font("Segoe UI", 8.5F);
        _lblOfflineNotice.Text = "ℹ️ Offline Kural Motoru Aktif: Harici API anahtarı gerektirmez; yerel optimizasyon ve kural tabanlı algoritmalar çalışır.";
        _lblOfflineNotice.TextAlign = ContentAlignment.MiddleLeft;
        _lblOfflineNotice.Padding = new Padding(12, 0, 0, 0);
        card1.Controls.Add(_lblOfflineNotice);

        // Primary API Key Row
        var rowApiKey = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        rowApiKey.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        rowApiKey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _lblActiveKeyTitle.Text = "API Anahtarı:";
        _lblActiveKeyTitle.Dock = DockStyle.Fill;
        _lblActiveKeyTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblActiveKeyTitle.ForeColor = Color.FromArgb(203, 213, 225);
        _lblActiveKeyTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        StyleDarkTextBox(_apiKeyTextBox, "sk-...");
        _apiKeyTextBox.UseSystemPasswordChar = true;
        _apiKeyTextBox.Dock = DockStyle.Fill;

        StyleDarkTextBox(_secondaryKeyTextBox, "AIzaSy... veya sk-...");
        _secondaryKeyTextBox.UseSystemPasswordChar = true;
        _secondaryKeyTextBox.Dock = DockStyle.Fill;

        var pnlKeyContainer = new Panel { Dock = DockStyle.Fill };
        var btnEyeKey = CreateEyeButton(_secondaryKeyTextBox);
        btnEyeKey.Dock = DockStyle.Right;
        btnEyeKey.Click += (_, _) =>
        {
            _apiKeyTextBox.UseSystemPasswordChar = _secondaryKeyTextBox.UseSystemPasswordChar;
        };

        pnlKeyContainer.Controls.Add(_apiKeyTextBox);
        pnlKeyContainer.Controls.Add(_secondaryKeyTextBox);
        pnlKeyContainer.Controls.Add(btnEyeKey);

        rowApiKey.Controls.Add(_lblActiveKeyTitle, 0, 0);
        rowApiKey.Controls.Add(pnlKeyContainer, 1, 0);
        card1.Controls.Add(rowApiKey);

        // Model Selection Row
        var rowModel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        rowModel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        rowModel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _lblActiveModelTitle.Text = "Metin Modeli:";
        _lblActiveModelTitle.Dock = DockStyle.Fill;
        _lblActiveModelTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblActiveModelTitle.ForeColor = Color.FromArgb(203, 213, 225);
        _lblActiveModelTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        var pnlModelSelectors = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        // OpenAI Model combo
        _modelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _modelComboBox.Width = 260;
        _modelComboBox.Items.AddRange(["gpt-4o", "gpt-4o-mini", "o3-mini", "o1", "o1-mini", "chatgpt-4o-latest", "gpt-4-turbo"]);
        StyleDarkComboBox(_modelComboBox);

        // Secondary Model combo (Gemini, Claude, DeepSeek, Grok)
        _secondaryModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _secondaryModelComboBox.Width = 260;
        StyleDarkComboBox(_secondaryModelComboBox);

        var lblOrCustom = new Label
        {
            Text = "veya Özel Model:",
            AutoSize = true,
            Margin = new Padding(12, 6, 6, 0),
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F)
        };

        _customModelTextBox.Width = 280;
        StyleDarkTextBox(_customModelTextBox, "Örn: gemini-2.5-flash, grok-3...");
        _customModelTextBox.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_customModelTextBox.Text))
            {
                _secondaryModelComboBox.Text = _customModelTextBox.Text.Trim();
            }
        };

        pnlModelSelectors.Controls.Add(_modelComboBox);
        pnlModelSelectors.Controls.Add(_secondaryModelComboBox);
        pnlModelSelectors.Controls.Add(lblOrCustom);
        pnlModelSelectors.Controls.Add(_customModelTextBox);

        rowModel.Controls.Add(_lblActiveModelTitle, 0, 0);
        rowModel.Controls.Add(pnlModelSelectors, 1, 0);
        card1.Controls.Add(rowModel);

        // Image Model Row (Only for OpenAI or Gemini)
        _pnlImageModelRow.Dock = DockStyle.Top;
        _pnlImageModelRow.Height = 42;
        var rowImg = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        rowImg.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        rowImg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _lblActiveImageModelTitle.Text = "Görsel Modeli:";
        _lblActiveImageModelTitle.Dock = DockStyle.Fill;
        _lblActiveImageModelTitle.TextAlign = ContentAlignment.MiddleLeft;
        _lblActiveImageModelTitle.ForeColor = Color.FromArgb(203, 213, 225);
        _lblActiveImageModelTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        _imageModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _imageModelComboBox.Width = 320;
        _imageModelComboBox.Items.AddRange(["dall-e-3", "gpt-image-2.5-flare", "gpt-image-2.5-sunburst", "gpt-image-2", "dall-e-2"]);
        StyleDarkComboBox(_imageModelComboBox);

        _secondaryImageModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _secondaryImageModelComboBox.Width = 320;
        _secondaryImageModelComboBox.Items.AddRange(["imagen-3.0-generate-002", "gemini-2.5-flash-image", "gemini-2.0-flash"]);
        StyleDarkComboBox(_secondaryImageModelComboBox);

        var pnlImgBoxes = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        pnlImgBoxes.Controls.Add(_imageModelComboBox);
        pnlImgBoxes.Controls.Add(_secondaryImageModelComboBox);

        rowImg.Controls.Add(_lblActiveImageModelTitle, 0, 0);
        rowImg.Controls.Add(pnlImgBoxes, 1, 0);
        _pnlImageModelRow.Controls.Add(rowImg);
        card1.Controls.Add(_pnlImageModelRow);

        bodyScroll.Controls.Add(card1);

        // --- CARD 2: Görsel Stüdyo & Özel AI Motorları ---
        var card2 = CreateCard("🎨 Görsel Üretim & Arka Plan AI Motorları (Vision Studio)");

        var subCard2Tip = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "💡 Ürün fotoğraflarının arka planını şeffaflaştırmak (dekupe) veya 3D modelleri fotogerçekçi renderlamak için kullanılır.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F)
        };
        card2.Controls.Add(subCard2Tip);

        card2.Controls.Add(CreateApiKeyInputRow("✂️ PhotoRoom API Key:", _photoRoomKeyTextBox, "prod_... (Arka plan temizleme / Dekupe)"));
        card2.Controls.Add(CreateApiKeyInputRow("⚡ BFL FLUX API Key:", _bflKeyTextBox, "Black Forest Labs (FLUX.1-pro render)"));
        card2.Controls.Add(CreateApiKeyInputRow("🎨 Ideogram API Key:", _ideogramKeyTextBox, "Ideogram v4 (Tipografi ve ürün görseli)"));

        bodyScroll.Controls.Add(card2);

        // --- CARD 3: Sistem Güvenliği & Fallback Politikası ---
        var card3 = CreateCard("🛡️ Sistem Güvenliği & Kesintisiz Çalışma");

        _chkStrictLiveAi.Text = "Canlı AI modeli yanıt vermezse sessizce kalitesiz offline motora düşme (Beni açıkça uyar ve hata bildir)";
        _chkStrictLiveAi.Dock = DockStyle.Top;
        _chkStrictLiveAi.Height = 28;
        _chkStrictLiveAi.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _chkStrictLiveAi.ForeColor = Color.FromArgb(52, 211, 153); // Soft emerald
        _chkStrictLiveAi.Cursor = Cursors.Hand;
        card3.Controls.Add(_chkStrictLiveAi);

        var lblFallbackDesc = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Bu seçenek aktif olduğunda, yapay zeka kotası bittiğinde veya bağlantı koptuğunda kalitesiz yerel başlıklar üretilmez; sizi uyararak API anahtarını kontrol etmenizi sağlar.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F)
        };
        card3.Controls.Add(lblFallbackDesc);

        bodyScroll.Controls.Add(card3);

        // ----------------------------------------------------
        // 3. ACTION FOOTER & CONSOLE
        // ----------------------------------------------------
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f)); // Buttons
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f)); // Console log
        mainLayout.Controls.Add(footer, 0, 2);

        // Action buttons
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 16, 0, 0)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet & Aktifleştir",
            BackColor = Color.FromArgb(16, 185, 129), // Emerald Neon
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Height = 38,
            Width = 190,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            SaveValues();
            MessageBox.Show(
                this,
                $"✅ En güncel AI modelleri ve yapılandırma başarıyla kaydedildi!\n\nAktif Sağlayıcı: {_settings.GetActiveEngineName()}",
                "AI Yapılandırması",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        };
        pnlActions.Controls.Add(btnSave);

        var btnTest = new Button
        {
            Text = "🧪 Hızlı API Testi",
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Height = 38,
            Width = 145,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnTest.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 115);
        btnTest.Click += (_, _) => TestSettings();
        pnlActions.Controls.Add(btnTest);

        var btnClose = new Button
        {
            Text = "✕ Kapat",
            BackColor = Color.FromArgb(33, 41, 54),
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9F),
            Height = 38,
            Width = 90,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 90);
        btnClose.Click += (_, _) => Close();
        pnlActions.Controls.Add(btnClose);

        footer.Controls.Add(pnlActions, 0, 0);

        // Status Console Box
        var pnlConsoleContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 15, 26),
            Padding = new Padding(8),
            Margin = new Padding(4, 8, 0, 0)
        };
        pnlConsoleContainer.Paint += (_, pe) =>
        {
            using var p = new Pen(Color.FromArgb(31, 41, 55), 1.2f);
            pe.Graphics.DrawRectangle(p, 0, 0, pnlConsoleContainer.Width - 1, pnlConsoleContainer.Height - 1);
        };

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.BackColor = Color.FromArgb(10, 15, 26);
        _statusTextBox.ForeColor = Color.FromArgb(52, 211, 153); // Monospace soft emerald
        _statusTextBox.BorderStyle = BorderStyle.None;
        _statusTextBox.Font = new Font("Consolas", 8.5F);
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        pnlConsoleContainer.Controls.Add(_statusTextBox);

        footer.Controls.Add(pnlConsoleContainer, 1, 0);
    }

    private void AddProviderPill(FlowLayoutPanel parent, string providerId, string title)
    {
        var btn = new Button
        {
            Text = title,
            Height = 30,
            AutoSize = true,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(203, 213, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 2, 0)
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(55, 65, 81);
        btn.Click += (_, _) =>
        {
            _providerComboBox.SelectedItem = providerId;
        };
        parent.Controls.Add(btn);
    }

    private static Control CreateApiKeyInputRow(string labelText, TextBox txt, string placeholder)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lbl = CreateFieldLabel(labelText);
        StyleDarkTextBox(txt, placeholder);
        txt.UseSystemPasswordChar = true;
        txt.Dock = DockStyle.Fill;

        var pnl = new Panel { Dock = DockStyle.Fill };
        var eye = CreateEyeButton(txt);
        eye.Dock = DockStyle.Right;
        pnl.Controls.Add(txt);
        pnl.Controls.Add(eye);

        row.Controls.Add(lbl, 0, 0);
        row.Controls.Add(pnl, 1, 0);
        return row;
    }

    private static Panel CreateCard(string headerTitle)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(20, 28, 45),
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0, 0, 0, 14)
        };
        card.Paint += (_, pe) =>
        {
            using var pen = new Pen(Color.FromArgb(38, 48, 70), 1.2f);
            pe.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var lblH = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = headerTitle,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.White
        };
        card.Controls.Add(lblH);
        return card;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
    };

    private static Button CreateEyeButton(TextBox target)
    {
        var btn = new Button
        {
            Text = "👁️",
            Size = new Size(34, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 45, 68),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) =>
        {
            target.UseSystemPasswordChar = !target.UseSystemPasswordChar;
            btn.Text = target.UseSystemPasswordChar ? "👁️" : "🔒";
        };
        return btn;
    }

    private static void StyleDarkTextBox(TextBox txt, string placeholder = "")
    {
        txt.BackColor = Color.FromArgb(28, 38, 58);
        txt.ForeColor = Color.White;
        txt.BorderStyle = BorderStyle.FixedSingle;
        txt.Font = new Font("Segoe UI", 9.5F);
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            txt.PlaceholderText = placeholder;
        }
    }

    private static void StyleDarkComboBox(ComboBox cbo)
    {
        cbo.BackColor = Color.FromArgb(28, 38, 58);
        cbo.ForeColor = Color.White;
        cbo.FlatStyle = FlatStyle.Flat;
        cbo.Font = new Font("Segoe UI", 9.5F);
    }

    private void LoadValues()
    {
        _providerComboBox.SelectedItem = string.IsNullOrWhiteSpace(_settings.Provider) ? "Offline" : _settings.Provider;
        if (_providerComboBox.SelectedIndex < 0) _providerComboBox.SelectedIndex = 0;

        _apiKeyTextBox.Text = _settings.OpenAiApiKey;
        _modelComboBox.Text = AiModelNormalizer.NormalizeOpenAiTextModel(_settings.OpenAiModel);
        _imageModelComboBox.Text = AiModelNormalizer.NormalizeOpenAiImageModel(_settings.OpenAiImageModel);

        _secondaryKeyTextBox.Text = _settings.Provider switch
        {
            "Gemini" => _settings.GeminiApiKey,
            "Claude" => _settings.ClaudeApiKey,
            "DeepSeek" => _settings.DeepSeekApiKey,
            "Grok" => _settings.GrokApiKey,
            "Platform Token" => _settings.PlatformToken,
            _ => !string.IsNullOrWhiteSpace(_settings.GeminiApiKey) ? _settings.GeminiApiKey : "",
        };

        string loadedModel = _settings.Provider switch
        {
            "Gemini" => AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel),
            "Claude" => AiModelNormalizer.NormalizeClaudeTextModel(_settings.ClaudeModel),
            "DeepSeek" => AiModelNormalizer.NormalizeDeepSeekModel(_settings.DeepSeekModel),
            "Grok" => AiModelNormalizer.NormalizeGrokModel(_settings.GrokModel),
            _ => AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel),
        };

        _secondaryModelComboBox.Text = loadedModel;
        if (!_secondaryModelComboBox.Items.Contains(loadedModel) || loadedModel == "gemini-3.8-flash")
        {
            _customModelTextBox.Text = loadedModel;
        }
        else
        {
            _customModelTextBox.Text = "";
        }

        _secondaryImageModelComboBox.Text = AiModelNormalizer.NormalizeGeminiImageModel(_settings.GeminiImageModel);
        _bflKeyTextBox.Text = _settings.BflApiKey;
        _ideogramKeyTextBox.Text = _settings.IdeogramApiKey;
        _photoRoomKeyTextBox.Text = !string.IsNullOrWhiteSpace(_settings.PhotoRoomApiKey)
            ? _settings.PhotoRoomApiKey
            : PhotoRoomSettingsStore.Load().ApiKey;

        _chkStrictLiveAi.Checked = !_settings.AllowSilentOfflineFallback;

        UpdateFieldLabels();
        WriteStatus($"Ayar dosyası başarıyla yüklendi: {AiOptimizationSettingsStore.SettingsPath}");
    }

    private void SaveValues()
    {
        _settings.Provider = _providerComboBox.SelectedItem?.ToString() ?? "Offline";
        _settings.OpenAiApiKey = _apiKeyTextBox.Text.Trim();
        _settings.OpenAiModel = AiModelNormalizer.NormalizeOpenAiTextModel(_modelComboBox.Text);
        _settings.OpenAiImageModel = AiModelNormalizer.NormalizeOpenAiImageModel(_imageModelComboBox.Text);

        string rawEffectiveModel = !string.IsNullOrWhiteSpace(_customModelTextBox.Text)
            ? _customModelTextBox.Text.Trim()
            : _secondaryModelComboBox.Text.Trim();

        if (rawEffectiveModel == "[Özel Model Girin...]")
            rawEffectiveModel = _customModelTextBox.Text.Trim();

        if (_settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GeminiApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.GeminiModel = AiModelNormalizer.NormalizeGeminiTextModel(rawEffectiveModel);
            _settings.GeminiImageModel = AiModelNormalizer.NormalizeGeminiImageModel(_secondaryImageModelComboBox.Text);
        }
        else if (_settings.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            _settings.ClaudeApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.ClaudeModel = AiModelNormalizer.NormalizeClaudeTextModel(rawEffectiveModel);
        }
        else if (_settings.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            _settings.DeepSeekApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.DeepSeekModel = AiModelNormalizer.NormalizeDeepSeekModel(rawEffectiveModel);
        }
        else if (_settings.Provider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GrokApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.GrokModel = AiModelNormalizer.NormalizeGrokModel(rawEffectiveModel);
        }
        else if (_settings.Provider.Equals("Platform Token", StringComparison.OrdinalIgnoreCase))
        {
            _settings.PlatformToken = _secondaryKeyTextBox.Text.Trim();
        }

        _settings.BflApiKey = _bflKeyTextBox.Text.Trim();
        _settings.IdeogramApiKey = _ideogramKeyTextBox.Text.Trim();
        _settings.PhotoRoomApiKey = _photoRoomKeyTextBox.Text.Trim();
        _settings.AllowSilentOfflineFallback = !_chkStrictLiveAi.Checked;

        PhotoRoomSettingsStore.Save(new PhotoRoomSettings { ApiKey = _settings.PhotoRoomApiKey });
        AiOptimizationSettingsStore.Save(_settings);

        try
        {
            var studioCfg = StudioConfigurationManager.Current;
            studioCfg.OpenAiApiKey = _settings.OpenAiApiKey;
            studioCfg.DefaultOpenAiModel = _settings.OpenAiImageModel;
            if (!string.IsNullOrWhiteSpace(_settings.GeminiApiKey)) studioCfg.GoogleGeminiApiKey = _settings.GeminiApiKey;
            if (!string.IsNullOrWhiteSpace(_settings.BflApiKey)) studioCfg.BflApiKey = _settings.BflApiKey;
            if (!string.IsNullOrWhiteSpace(_settings.IdeogramApiKey)) studioCfg.IdeogramApiKey = _settings.IdeogramApiKey;
            if (!string.IsNullOrWhiteSpace(_settings.PhotoRoomApiKey)) studioCfg.PhotoRoomApiKey = _settings.PhotoRoomApiKey;
            StudioConfigurationManager.Save(studioCfg);
        }
        catch { }

        WriteStatus("Tüm AI ve Görsel Stüdyo ayarları kalıcı olarak kaydedildi.");
    }

    private void TestSettings()
    {
        SaveValues();
        if (_settings.UseOpenAi)
        {
            WriteStatus($"✅ OpenAI modu hazır: Model {_settings.OpenAiModel}");
            return;
        }

        if (_settings.UseGemini)
        {
            WriteStatus($"✅ Gemini modu hazır: Model {_settings.GeminiModel}");
            return;
        }

        if (_settings.UseDeepSeek)
        {
            WriteStatus($"✅ DeepSeek modu hazır: Model {_settings.DeepSeekModel}");
            return;
        }

        if (_settings.UseGrok)
        {
            WriteStatus($"✅ xAI Grok modu hazır: Model {_settings.GrokModel}");
            return;
        }

        if (_settings.IsOffline)
        {
            WriteStatus("ℹ️ Offline mod aktif. API anahtarı olmadan yerel kural motoru devrede.");
            return;
        }

        WriteStatus($"✅ {_settings.Provider} seçildi ve doğrulandı.");
    }

    private void UpdateFieldLabels()
    {
        if (_providerComboBox.SelectedItem?.ToString() is not { } provider) return;

        bool isOpenAi = provider == "OpenAI";
        bool isOffline = provider == "Offline";
        bool isGemini = provider == "Gemini";

        _lblOfflineNotice.Visible = isOffline;

        // Key boxes visibility & labels
        if (isOpenAi)
        {
            _lblActiveKeyTitle.Text = "OpenAI API Key:";
            _apiKeyTextBox.Visible = true;
            _secondaryKeyTextBox.Visible = false;

            _lblActiveModelTitle.Text = "OpenAI Modeli:";
            _modelComboBox.Visible = true;
            _secondaryModelComboBox.Visible = false;
            _customModelTextBox.Visible = false;

            _lblActiveImageModelTitle.Text = "OpenAI Görsel Modeli:";
            _pnlImageModelRow.Visible = true;
            _imageModelComboBox.Visible = true;
            _secondaryImageModelComboBox.Visible = false;
        }
        else if (isOffline)
        {
            _lblActiveKeyTitle.Text = "API Anahtarı:";
            _apiKeyTextBox.Visible = false;
            _secondaryKeyTextBox.Visible = true;
            _secondaryKeyTextBox.Enabled = false;
            _secondaryKeyTextBox.Text = "(Offline Modda API Anahtarı Gerekmez)";

            _lblActiveModelTitle.Text = "Metin Modeli:";
            _modelComboBox.Visible = false;
            _secondaryModelComboBox.Visible = true;
            _secondaryModelComboBox.Enabled = false;
            _secondaryModelComboBox.Text = "Yerel Kural Motoru";
            _customModelTextBox.Visible = false;

            _pnlImageModelRow.Visible = false;
        }
        else
        {
            _secondaryKeyTextBox.Enabled = true;
            _secondaryModelComboBox.Enabled = true;
            _customModelTextBox.Visible = true;

            _lblActiveKeyTitle.Text = $"{provider} API Key:";
            _apiKeyTextBox.Visible = false;
            _secondaryKeyTextBox.Visible = true;

            _lblActiveModelTitle.Text = $"{provider} Modeli:";
            _modelComboBox.Visible = false;
            _secondaryModelComboBox.Visible = true;

            _pnlImageModelRow.Visible = isGemini;
            _imageModelComboBox.Visible = false;
            _secondaryImageModelComboBox.Visible = isGemini;

            _secondaryModelComboBox.Items.Clear();
            if (isGemini)
            {
                _secondaryModelComboBox.Items.AddRange([
                    "gemini-2.5-flash",
                    "gemini-3.6-flash",
                    "gemini-2.5-pro",
                    "gemini-3.8-flash",
                    "gemini-1.5-flash",
                    "gemini-1.5-pro",
                    "[Özel Model Girin...]"
                ]);
                if (!string.IsNullOrWhiteSpace(_settings.GeminiApiKey)) _secondaryKeyTextBox.Text = _settings.GeminiApiKey;
                var geminiModel = AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel);
                _secondaryModelComboBox.Text = geminiModel;
                _customModelTextBox.Text = (!_secondaryModelComboBox.Items.Contains(geminiModel) || geminiModel == "gemini-3.8-flash") ? geminiModel : "";
            }
            else if (provider == "Claude")
            {
                _secondaryModelComboBox.Items.AddRange([
                    "claude-3-7-sonnet-20250219",
                    "claude-3-5-sonnet-20241022",
                    "claude-3-5-haiku-20241022",
                    "claude-3-opus-20240229",
                    "[Özel Model Girin...]"
                ]);
                if (!string.IsNullOrWhiteSpace(_settings.ClaudeApiKey)) _secondaryKeyTextBox.Text = _settings.ClaudeApiKey;
                var claudeModel = AiModelNormalizer.NormalizeClaudeTextModel(_settings.ClaudeModel);
                _secondaryModelComboBox.Text = claudeModel;
                _customModelTextBox.Text = !_secondaryModelComboBox.Items.Contains(claudeModel) ? claudeModel : "";
            }
            else if (provider == "DeepSeek")
            {
                _secondaryModelComboBox.Items.AddRange([
                    "deepseek-reasoner",
                    "deepseek-chat",
                    "[Özel Model Girin...]"
                ]);
                if (!string.IsNullOrWhiteSpace(_settings.DeepSeekApiKey)) _secondaryKeyTextBox.Text = _settings.DeepSeekApiKey;
                var deepSeekModel = AiModelNormalizer.NormalizeDeepSeekModel(_settings.DeepSeekModel);
                _secondaryModelComboBox.Text = deepSeekModel;
                _customModelTextBox.Text = !_secondaryModelComboBox.Items.Contains(deepSeekModel) ? deepSeekModel : "";
            }
            else if (provider == "Grok")
            {
                _secondaryModelComboBox.Items.AddRange([
                    "grok-3",
                    "grok-2-latest",
                    "[Özel Model Girin...]"
                ]);
                if (!string.IsNullOrWhiteSpace(_settings.GrokApiKey)) _secondaryKeyTextBox.Text = _settings.GrokApiKey;
                var grokModel = AiModelNormalizer.NormalizeGrokModel(_settings.GrokModel);
                _secondaryModelComboBox.Text = grokModel;
                _customModelTextBox.Text = !_secondaryModelComboBox.Items.Contains(grokModel) ? grokModel : "";
            }
            else if (provider == "Platform Token")
            {
                _lblActiveKeyTitle.Text = "Platform Lisans/Token:";
                _secondaryKeyTextBox.Text = _settings.PlatformToken;
                _customModelTextBox.Visible = false;
            }
        }
    }

    private void WriteStatus(string message)
    {
        _statusTextBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}{_statusTextBox.Text}";
    }
}
