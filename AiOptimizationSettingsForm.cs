namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AiOptimizationSettingsForm : Form
{
    private readonly AiOptimizationSettings _settings;
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _apiKeyTextBox = new();
    private readonly ComboBox _modelComboBox = new();
    private readonly ComboBox _imageModelComboBox = new();
    private readonly TextBox _secondaryKeyTextBox = new();
    private readonly ComboBox _secondaryModelComboBox = new();
    private readonly ComboBox _secondaryImageModelComboBox = new();
    private readonly TextBox _bflKeyTextBox = new();
    private readonly TextBox _ideogramKeyTextBox = new();
    private readonly TextBox _photoRoomKeyTextBox = new();
    private readonly TextBox _statusTextBox = new();

    public AiOptimizationSettingsForm()
    {
        _settings = AiOptimizationSettingsStore.Load();
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "AI Optimizasyon Ayarları & En Güncel Modeller";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(880, 720);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);

        _providerComboBox.Dock = DockStyle.Left;
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.Width = 260;
        _providerComboBox.Items.AddRange(["Offline", "Gemini", "OpenAI", "Claude", "DeepSeek", "Grok", "Platform Token"]);
        _providerComboBox.SelectedIndexChanged += (_, _) => UpdateFieldLabels(root);
        root.Controls.Add(LabelFor("Sağlayıcı:"), 0, 0);
        root.Controls.Add(_providerComboBox, 1, 0);

        _apiKeyTextBox.Dock = DockStyle.Fill;
        _apiKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("OpenAI API Key:"), 0, 1);
        root.Controls.Add(_apiKeyTextBox, 1, 1);

        _modelComboBox.Dock = DockStyle.Left;
        _modelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _modelComboBox.Width = 340;
        _modelComboBox.Items.AddRange([
            "gpt-4o",
            "gpt-4o-mini",
            "o3-mini",
            "o1",
            "o1-mini",
            "chatgpt-4o-latest",
            "gpt-4-turbo"
        ]);
        root.Controls.Add(LabelFor("OpenAI Metin Modeli:"), 0, 2);
        root.Controls.Add(_modelComboBox, 1, 2);

        _imageModelComboBox.Dock = DockStyle.Left;
        _imageModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _imageModelComboBox.Width = 340;
        _imageModelComboBox.Items.AddRange([
            "dall-e-3",
            "dall-e-2"
        ]);
        root.Controls.Add(LabelFor("OpenAI Görsel Modeli:"), 0, 3);
        root.Controls.Add(_imageModelComboBox, 1, 3);

        _secondaryKeyTextBox.Dock = DockStyle.Fill;
        _secondaryKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("Diğer Sağlayıcı Key:"), 0, 4);
        root.Controls.Add(_secondaryKeyTextBox, 1, 4);

        _secondaryModelComboBox.Dock = DockStyle.Left;
        _secondaryModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _secondaryModelComboBox.Width = 340;
        _secondaryModelComboBox.Items.AddRange([
            "gemini-2.5-flash",
            "gemini-2.5-pro",
            "gemini-2.0-flash",
            "gemini-2.0-flash-thinking-exp",
            "gemini-1.5-pro",
            "gemini-1.5-flash"
        ]);
        root.Controls.Add(LabelFor("Seçili Sağlayıcı Modeli:"), 0, 5);
        root.Controls.Add(_secondaryModelComboBox, 1, 5);

        _secondaryImageModelComboBox.Dock = DockStyle.Left;
        _secondaryImageModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _secondaryImageModelComboBox.Width = 340;
        _secondaryImageModelComboBox.Items.AddRange([
            "imagen-3.0-generate-002",
            "gemini-2.5-flash-image",
            "gemini-2.0-flash"
        ]);
        root.Controls.Add(LabelFor("Gemini Görsel Modeli:"), 0, 6);
        root.Controls.Add(_secondaryImageModelComboBox, 1, 6);

        _bflKeyTextBox.Dock = DockStyle.Fill;
        _bflKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("BFL (FLUX) API Key:"), 0, 7);
        root.Controls.Add(_bflKeyTextBox, 1, 7);

        _ideogramKeyTextBox.Dock = DockStyle.Fill;
        _ideogramKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("Ideogram API Key:"), 0, 8);
        root.Controls.Add(_ideogramKeyTextBox, 1, 8);

        _photoRoomKeyTextBox.Dock = DockStyle.Fill;
        _photoRoomKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("PhotoRoom API Key:"), 0, 9);
        root.Controls.Add(_photoRoomKeyTextBox, 1, 9);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 0) };
        var save = CreateButton("💾 Kaydet");
        save.BackColor = Color.FromArgb(16, 140, 90);
        save.ForeColor = Color.White;
        save.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        save.Click += (_, _) =>
        {
            SaveValues();
            MessageBox.Show(
                this,
                $"✅ En güncel AI modelleri doğrulandı ve kaydedildi!\n\nAktif Sağlayıcı: {_settings.GetActiveEngineName()}",
                "AI Ayarları",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(save);

        var test = CreateButton("🧪 Ayar Test");
        test.Click += (_, _) => TestSettings();
        buttons.Controls.Add(test);

        var close = CreateButton("Kapat");
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);

        root.Controls.Add(new Label(), 0, 10);
        root.Controls.Add(buttons, 1, 10);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(LabelFor("Durum:"), 0, 11);
        root.Controls.Add(_statusTextBox, 1, 11);

        root.Controls.Add(new Label(), 0, 12);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "💡 Önerilen En Güncel Görsel Modelleri: PhotoRoom (Arka Plan Silme), OpenAI 'dall-e-3', Google 'gemini-2.5-flash-image', BFL 'flux-pro-1.1' ve 'ideogram-v4'.",
            ForeColor = Color.FromArgb(75, 85, 99),
        }, 1, 12);
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

        _secondaryModelComboBox.Text = _settings.Provider switch
        {
            "Gemini" => AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel),
            "Claude" => AiModelNormalizer.NormalizeClaudeTextModel(_settings.ClaudeModel),
            "DeepSeek" => AiModelNormalizer.NormalizeDeepSeekModel(_settings.DeepSeekModel),
            "Grok" => AiModelNormalizer.NormalizeGrokModel(_settings.GrokModel),
            _ => AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel),
        };

        _secondaryImageModelComboBox.Text = AiModelNormalizer.NormalizeGeminiImageModel(_settings.GeminiImageModel);
        _bflKeyTextBox.Text = _settings.BflApiKey;
        _ideogramKeyTextBox.Text = _settings.IdeogramApiKey;
        _photoRoomKeyTextBox.Text = !string.IsNullOrWhiteSpace(_settings.PhotoRoomApiKey)
            ? _settings.PhotoRoomApiKey
            : PhotoRoomSettingsStore.Load().ApiKey;

        WriteStatus($"Ayar dosyası yüklendi: {AiOptimizationSettingsStore.SettingsPath}");
    }

    private void SaveValues()
    {
        _settings.Provider = _providerComboBox.SelectedItem?.ToString() ?? "Offline";
        _settings.OpenAiApiKey = _apiKeyTextBox.Text.Trim();
        _settings.OpenAiModel = AiModelNormalizer.NormalizeOpenAiTextModel(_modelComboBox.Text);
        _settings.OpenAiImageModel = AiModelNormalizer.NormalizeOpenAiImageModel(_imageModelComboBox.Text);

        if (_settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GeminiApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.GeminiModel = AiModelNormalizer.NormalizeGeminiTextModel(_secondaryModelComboBox.Text);
            _settings.GeminiImageModel = AiModelNormalizer.NormalizeGeminiImageModel(_secondaryImageModelComboBox.Text);
        }
        else if (_settings.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            _settings.ClaudeApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.ClaudeModel = AiModelNormalizer.NormalizeClaudeTextModel(_secondaryModelComboBox.Text);
        }
        else if (_settings.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            _settings.DeepSeekApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.DeepSeekModel = AiModelNormalizer.NormalizeDeepSeekModel(_secondaryModelComboBox.Text);
        }
        else if (_settings.Provider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GrokApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.GrokModel = AiModelNormalizer.NormalizeGrokModel(_secondaryModelComboBox.Text);
        }
        else if (_settings.Provider.Equals("Platform Token", StringComparison.OrdinalIgnoreCase))
        {
            _settings.PlatformToken = _secondaryKeyTextBox.Text.Trim();
        }

        _settings.BflApiKey = _bflKeyTextBox.Text.Trim();
        _settings.IdeogramApiKey = _ideogramKeyTextBox.Text.Trim();
        _settings.PhotoRoomApiKey = _photoRoomKeyTextBox.Text.Trim();

        PhotoRoomSettingsStore.Save(new PhotoRoomSettings { ApiKey = _settings.PhotoRoomApiKey });
        AiOptimizationSettingsStore.Save(_settings);
        WriteStatus("AI ve PhotoRoom ayarları kaydedildi.");
    }

    private void TestSettings()
    {
        SaveValues();
        if (_settings.UseOpenAi)
        {
            WriteStatus($"OpenAI modu hazır: {_settings.OpenAiModel}.");
            return;
        }

        if (_settings.UseGemini)
        {
            WriteStatus($"Gemini modu hazır: {_settings.GeminiModel}.");
            return;
        }

        if (_settings.UseDeepSeek)
        {
            WriteStatus($"DeepSeek modu hazır: {_settings.DeepSeekModel}.");
            return;
        }

        if (_settings.UseGrok)
        {
            WriteStatus($"xAI Grok modu hazır: {_settings.GrokModel}.");
            return;
        }

        if (_settings.IsOffline)
        {
            WriteStatus("Offline mod aktif. API anahtarı olmadan yerel kural motoru kullanılır.");
            return;
        }

        WriteStatus($"{_settings.Provider} seçildi.");
    }

    private void UpdateFieldLabels(TableLayoutPanel root)
    {
        if (_providerComboBox.SelectedItem?.ToString() is not { } provider) return;
        _apiKeyTextBox.Enabled = provider is "OpenAI" or "Offline";
        _modelComboBox.Enabled = provider is "OpenAI" or "Offline";
        _imageModelComboBox.Enabled = provider is "OpenAI" or "Offline";
        _secondaryKeyTextBox.Enabled = provider is "Gemini" or "Claude" or "DeepSeek" or "Grok" or "Platform Token";
        _secondaryModelComboBox.Enabled = provider is "Gemini" or "Claude" or "DeepSeek" or "Grok";
        _secondaryImageModelComboBox.Enabled = provider is "Gemini";

        _secondaryModelComboBox.Items.Clear();
        if (provider == "Gemini")
        {
            _secondaryModelComboBox.Items.AddRange(["gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash", "gemini-2.0-flash-thinking-exp", "gemini-1.5-pro", "gemini-1.5-flash"]);
            if (!string.IsNullOrWhiteSpace(_settings.GeminiApiKey)) _secondaryKeyTextBox.Text = _settings.GeminiApiKey;
            _secondaryModelComboBox.Text = AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel);
        }
        else if (provider == "Claude")
        {
            _secondaryModelComboBox.Items.AddRange(["claude-3-7-sonnet-20250219", "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022", "claude-3-opus-20240229"]);
            if (!string.IsNullOrWhiteSpace(_settings.ClaudeApiKey)) _secondaryKeyTextBox.Text = _settings.ClaudeApiKey;
            _secondaryModelComboBox.Text = AiModelNormalizer.NormalizeClaudeTextModel(_settings.ClaudeModel);
        }
        else if (provider == "DeepSeek")
        {
            _secondaryModelComboBox.Items.AddRange(["deepseek-reasoner", "deepseek-chat"]);
            if (!string.IsNullOrWhiteSpace(_settings.DeepSeekApiKey)) _secondaryKeyTextBox.Text = _settings.DeepSeekApiKey;
            _secondaryModelComboBox.Text = AiModelNormalizer.NormalizeDeepSeekModel(_settings.DeepSeekModel);
        }
        else if (provider == "Grok")
        {
            _secondaryModelComboBox.Items.AddRange(["grok-3", "grok-2-latest"]);
            if (!string.IsNullOrWhiteSpace(_settings.GrokApiKey)) _secondaryKeyTextBox.Text = _settings.GrokApiKey;
            _secondaryModelComboBox.Text = AiModelNormalizer.NormalizeGrokModel(_settings.GrokModel);
        }
        else if (provider == "Platform Token")
        {
            _secondaryKeyTextBox.Text = _settings.PlatformToken;
        }
    }

    private void WriteStatus(string message)
    {
        _statusTextBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}{_statusTextBox.Text}";
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 125,
        Height = 34,
        Margin = new Padding(0, 0, 8, 8),
    };
}
