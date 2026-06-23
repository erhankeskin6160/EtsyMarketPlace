namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AiOptimizationSettingsForm : Form
{
    private readonly AiOptimizationSettings _settings;
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _apiKeyTextBox = new();
    private readonly TextBox _modelTextBox = new();
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
        MinimumSize = new Size(760, 420);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        _providerComboBox.Dock = DockStyle.Left;
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.Width = 180;
        _providerComboBox.Items.AddRange(["Offline", "OpenAI"]);
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

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var save = CreateButton("Kaydet");
        save.Click += (_, _) => SaveValues();
        buttons.Controls.Add(save);
        var test = CreateButton("Ayar Test");
        test.Click += (_, _) => TestSettings();
        buttons.Controls.Add(test);
        root.Controls.Add(new Label(), 0, 3);
        root.Controls.Add(buttons, 1, 3);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(LabelFor("Durum"), 0, 4);
        root.Controls.Add(_statusTextBox, 1, 4);

        root.Controls.Add(new Label(), 0, 5);
        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Not: Offline mod ucretsizdir. OpenAI secilirse AI ile Uret butonu API kullanir ve hesabinizdan kota/limit harcayabilir.",
            ForeColor = Color.FromArgb(75, 85, 99),
        }, 1, 5);
    }

    private void LoadValues()
    {
        _providerComboBox.SelectedItem = string.IsNullOrWhiteSpace(_settings.Provider) ? "Offline" : _settings.Provider;
        if (_providerComboBox.SelectedIndex < 0) _providerComboBox.SelectedIndex = 0;
        _apiKeyTextBox.Text = _settings.OpenAiApiKey;
        _modelTextBox.Text = _settings.OpenAiModel;
        WriteStatus($"Ayar dosyasi: {AiOptimizationSettingsStore.SettingsPath}");
    }

    private void SaveValues()
    {
        _settings.Provider = _providerComboBox.SelectedItem?.ToString() ?? "Offline";
        _settings.OpenAiApiKey = _apiKeyTextBox.Text.Trim();
        _settings.OpenAiModel = string.IsNullOrWhiteSpace(_modelTextBox.Text) ? "gpt-5.5" : _modelTextBox.Text.Trim();
        AiOptimizationSettingsStore.Save(_settings);
        WriteStatus("AI ayarlari kaydedildi.");
    }

    private void TestSettings()
    {
        SaveValues();
        WriteStatus(_settings.UseOpenAi
            ? "OpenAI modu hazir. AI ile Uret butonu gercek API cagrisi yapacak."
            : "Offline mod aktif. API key olmadan yerel kural motoru kullanilir.");
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
