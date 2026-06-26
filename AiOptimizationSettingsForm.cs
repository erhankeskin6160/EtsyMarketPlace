namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AiOptimizationSettingsForm : Form
{
    private readonly AiOptimizationSettings _settings;
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _apiKeyTextBox = new();
    private readonly TextBox _modelTextBox = new();
    private readonly TextBox _imageModelTextBox = new();
    private readonly TextBox _secondaryKeyTextBox = new();
    private readonly TextBox _secondaryModelTextBox = new();
    private readonly TextBox _secondaryImageModelTextBox = new();
    private readonly TextBox _statusTextBox = new();

    public AiOptimizationSettingsForm()
    {
        _settings = AiOptimizationSettingsStore.Load();
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "AI Optimizasyon Ayarlari";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 540);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        _providerComboBox.Dock = DockStyle.Left;
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.Width = 220;
        _providerComboBox.Items.AddRange(["Offline", "OpenAI", "Gemini", "Claude", "Platform Token"]);
        _providerComboBox.SelectedIndexChanged += (_, _) => UpdateFieldLabels(root);
        root.Controls.Add(LabelFor("Saglayici"), 0, 0);
        root.Controls.Add(_providerComboBox, 1, 0);

        _apiKeyTextBox.Dock = DockStyle.Fill;
        _apiKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("OpenAI API key"), 0, 1);
        root.Controls.Add(_apiKeyTextBox, 1, 1);

        _modelTextBox.Dock = DockStyle.Left;
        _modelTextBox.Width = 240;
        root.Controls.Add(LabelFor("Model"), 0, 2);
        root.Controls.Add(_modelTextBox, 1, 2);

        _imageModelTextBox.Dock = DockStyle.Left;
        _imageModelTextBox.Width = 240;
        root.Controls.Add(LabelFor("Gorsel modeli"), 0, 3);
        root.Controls.Add(_imageModelTextBox, 1, 3);

        _secondaryKeyTextBox.Dock = DockStyle.Fill;
        _secondaryKeyTextBox.UseSystemPasswordChar = true;
        root.Controls.Add(LabelFor("Gemini/Claude key"), 0, 4);
        root.Controls.Add(_secondaryKeyTextBox, 1, 4);

        _secondaryModelTextBox.Dock = DockStyle.Left;
        _secondaryModelTextBox.Width = 240;
        root.Controls.Add(LabelFor("Gemini/Claude model"), 0, 5);
        root.Controls.Add(_secondaryModelTextBox, 1, 5);

        _secondaryImageModelTextBox.Dock = DockStyle.Left;
        _secondaryImageModelTextBox.Width = 240;
        root.Controls.Add(LabelFor("Gemini gorsel modeli"), 0, 6);
        root.Controls.Add(_secondaryImageModelTextBox, 1, 6);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var save = CreateButton("Kaydet");
        save.Click += (_, _) => SaveValues();
        buttons.Controls.Add(save);
        var test = CreateButton("Ayar Test");
        test.Click += (_, _) => TestSettings();
        buttons.Controls.Add(test);
        root.Controls.Add(new Label(), 0, 7);
        root.Controls.Add(buttons, 1, 7);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(LabelFor("Durum"), 0, 8);
        root.Controls.Add(_statusTextBox, 1, 8);

        root.Controls.Add(new Label(), 0, 9);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Not: Metin analizi OpenAI/Gemini ile calisir. AI gorsel uretimi simdilik OpenAI gorsel modeliyle calisir.",
            ForeColor = Color.FromArgb(75, 85, 99),
        }, 1, 9);
    }

    private void LoadValues()
    {
        _providerComboBox.SelectedItem = string.IsNullOrWhiteSpace(_settings.Provider) ? "Offline" : _settings.Provider;
        if (_providerComboBox.SelectedIndex < 0) _providerComboBox.SelectedIndex = 0;
        _apiKeyTextBox.Text = _settings.OpenAiApiKey;
        _modelTextBox.Text = _settings.OpenAiModel;
        _imageModelTextBox.Text = _settings.OpenAiImageModel;
        _secondaryKeyTextBox.Text = _settings.Provider switch
        {
            "Gemini" => _settings.GeminiApiKey,
            "Claude" => _settings.ClaudeApiKey,
            "Platform Token" => _settings.PlatformToken,
            _ => "",
        };
        _secondaryModelTextBox.Text = _settings.Provider switch
        {
            "Gemini" => _settings.GeminiModel,
            "Claude" => _settings.ClaudeModel,
            _ => "",
        };
        _secondaryImageModelTextBox.Text = _settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            ? _settings.GeminiImageModel
            : "";
        WriteStatus($"Ayar dosyasi: {AiOptimizationSettingsStore.SettingsPath}");
    }

    private void SaveValues()
    {
        _settings.Provider = _providerComboBox.SelectedItem?.ToString() ?? "Offline";
        _settings.OpenAiApiKey = _apiKeyTextBox.Text.Trim();
        _settings.OpenAiModel = string.IsNullOrWhiteSpace(_modelTextBox.Text) ? "gpt-5.5" : _modelTextBox.Text.Trim();
        _settings.OpenAiImageModel = string.IsNullOrWhiteSpace(_imageModelTextBox.Text) ? "gpt-image-1" : _imageModelTextBox.Text.Trim();
        if (_settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GeminiApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.GeminiModel = string.IsNullOrWhiteSpace(_secondaryModelTextBox.Text) ? "gemini-3.5-flash" : _secondaryModelTextBox.Text.Trim();
            _settings.GeminiImageModel = string.IsNullOrWhiteSpace(_secondaryImageModelTextBox.Text) ? "gemini-3.1-flash-image" : _secondaryImageModelTextBox.Text.Trim();
        }
        else if (_settings.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            _settings.ClaudeApiKey = _secondaryKeyTextBox.Text.Trim();
            _settings.ClaudeModel = string.IsNullOrWhiteSpace(_secondaryModelTextBox.Text) ? _settings.ClaudeModel : _secondaryModelTextBox.Text.Trim();
        }
        else if (_settings.Provider.Equals("Platform Token", StringComparison.OrdinalIgnoreCase))
        {
            _settings.PlatformToken = _secondaryKeyTextBox.Text.Trim();
        }
        AiOptimizationSettingsStore.Save(_settings);
        WriteStatus("AI ayarlari kaydedildi.");
    }

    private void TestSettings()
    {
        SaveValues();
        if (_settings.UseOpenAi)
        {
            WriteStatus("OpenAI modu hazir. AI ile Uret butonu gercek API cagrisi yapacak.");
            return;
        }

        if (_settings.UseGemini)
        {
            WriteStatus("Gemini modu hazir. AI ile Uret / AI ile Puanla butonu Gemini API cagrisi yapacak.");
            return;
        }

        if (_settings.IsOffline)
        {
            WriteStatus("Offline mod aktif. API key olmadan yerel kural motoru kullanilir.");
            return;
        }

        WriteStatus($"{_settings.Provider} secildi; adapter ve odeme/token servisi sonraki feature'da aktif edilecek.");
    }

    private void UpdateFieldLabels(TableLayoutPanel root)
    {
        if (_providerComboBox.SelectedItem?.ToString() is not { } provider) return;
        _apiKeyTextBox.Enabled = provider is "OpenAI" or "Offline";
        _modelTextBox.Enabled = provider is "OpenAI" or "Offline";
        _imageModelTextBox.Enabled = provider is "OpenAI" or "Offline";
        _secondaryKeyTextBox.Enabled = provider is "Gemini" or "Claude" or "Platform Token";
        _secondaryModelTextBox.Enabled = provider is "Gemini" or "Claude";
        _secondaryImageModelTextBox.Enabled = provider is "Gemini";
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
        Width = 120,
        Height = 34,
        Margin = new Padding(0, 0, 8, 8),
    };
}
