namespace SimilarProductsWinForms;

using EtsyMarketPlace.Infrastructure.ExternalMarketplaces;
using SimilarProductsWinForms.Services;

internal sealed class ExternalMarketplaceSettingsForm : Form
{
    private readonly TextBox _clientIdTextBox = new();
    private readonly TextBox _clientSecretTextBox = new();
    private readonly ComboBox _marketplaceComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _limitInput = new() { Minimum = 1, Maximum = 50, Value = 20 };
    private readonly TextBox _settingsPathTextBox = new();
    private readonly TextBox _statusTextBox = new();
    private readonly EbayApiClient _ebayClient = new();

    public ExternalMarketplaceSettingsForm()
    {
        Text = "Dis Pazar API Ayarlari";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 520);
        Size = new Size(860, 560);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        BuildLayout();
        LoadSettings();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Dış Pazar API Ayarları",
            Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        AddField(form, "eBay Client ID", _clientIdTextBox, 0);
        _clientSecretTextBox.UseSystemPasswordChar = true;
        AddField(form, "eBay Client Secret", _clientSecretTextBox, 1);
        _marketplaceComboBox.Items.AddRange([
            "EBAY_US",
            "EBAY_GB",
            "EBAY_DE",
            "EBAY_FR",
            "EBAY_IT",
            "EBAY_ES",
            "EBAY_CA",
            "EBAY_AU",
        ]);
        AddField(form, "Marketplace", _marketplaceComboBox, 2);
        AddField(form, "Arama limiti", _limitInput, 3);
        _settingsPathTextBox.ReadOnly = true;
        AddField(form, "Ayar dosyası", _settingsPathTextBox, 4);
        root.Controls.Add(form, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var close = CreateButton("Kapat", Color.FromArgb(82, 93, 110));
        close.Click += (_, _) => Close();
        var test = CreateButton("Baglantiyi Test Et", Color.FromArgb(32, 97, 165));
        test.Click += async (_, _) => await TestConnectionAsync();
        var save = CreateButton("Kaydet", Color.FromArgb(20, 126, 76));
        save.Click += (_, _) => SaveSettings();
        actions.Controls.Add(close);
        actions.Controls.Add(test);
        actions.Controls.Add(save);
        root.Controls.Add(actions, 0, 2);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        _statusTextBox.BackColor = Color.White;
        root.Controls.Add(_statusTextBox, 0, 3);
    }

    private void LoadSettings()
    {
        var settings = EbayApiSettingsStore.Load();
        _clientIdTextBox.Text = settings.ClientId;
        _clientSecretTextBox.Text = settings.ClientSecret;
        _marketplaceComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.MarketplaceId)
            ? "EBAY_US"
            : settings.MarketplaceId;
        if (_marketplaceComboBox.SelectedIndex < 0)
        {
            _marketplaceComboBox.SelectedIndex = 0;
        }

        _limitInput.Value = Math.Clamp(settings.Limit <= 0 ? 20 : settings.Limit, 1, 50);
        _settingsPathTextBox.Text = EbayApiSettingsStore.SettingsPath;
        Log("Ayarlar yuklendi. eBay gercek veri icin Client ID ve Client Secret girip kaydedin.");
    }

    private void SaveSettings()
    {
        EbayApiSettingsStore.Save(BuildSettings());
        Log("Ayarlar kaydedildi.");
    }

    private async Task TestConnectionAsync()
    {
        var settings = BuildSettings();
        SaveSettings();
        try
        {
            UseWaitCursor = true;
            Log("eBay baglantisi test ediliyor...");
            var result = await _ebayClient.TestConnectionAsync(settings);
            Log(result);
            MessageBox.Show(this, result, "eBay API", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"Test basarisiz: {ex.Message}");
            MessageBox.Show(this, ex.Message, "eBay API", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private EbayApiSettings BuildSettings() => new()
    {
        ClientId = _clientIdTextBox.Text.Trim(),
        ClientSecret = _clientSecretTextBox.Text.Trim(),
        MarketplaceId = _marketplaceComboBox.SelectedItem?.ToString() ?? "EBAY_US",
        Limit = (int)_limitInput.Value,
    };

    private void Log(string message)
    {
        _statusTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
    }

    private static void AddField(TableLayoutPanel form, string label, Control control, int row)
    {
        form.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = label,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(23, 32, 49),
        }, 0, row);
        control.Dock = DockStyle.Fill;
        form.Controls.Add(control, 1, row);
    }

    private static Button CreateButton(string text, Color backColor)
    {
        var button = new Button
        {
            Width = 150,
            Height = 34,
            Text = text,
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.2F),
            UseVisualStyleBackColor = false,
            Margin = new Padding(8, 4, 0, 4),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
