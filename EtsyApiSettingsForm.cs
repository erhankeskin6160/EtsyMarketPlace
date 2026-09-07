namespace SimilarProductsWinForms;

using System.Diagnostics;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class EtsyApiSettingsForm : Form
{
    private const string DefaultScopes = "shops_r listings_r listings_w transactions_r billing_r";
    private readonly EtsyApiClient _apiClient = new();
    private readonly EtsyApiSettings _settings;

    private readonly TextBox _keystringTextBox = new();
    private readonly TextBox _sharedSecretTextBox = new();
    private readonly TextBox _redirectUriTextBox = new();
    private readonly TextBox _authorizationUrlTextBox = new();
    private readonly TextBox _authorizationCodeTextBox = new();
    private readonly TextBox _statusTextBox = new();

    public EtsyApiSettingsForm()
    {
        _settings = EtsyApiSettingsStore.Load();
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "Etsy API Ayarlari";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(850, 620);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(root, 0, "Keystring", _keystringTextBox);
        _sharedSecretTextBox.UseSystemPasswordChar = true;
        AddRow(root, 1, "Shared secret", _sharedSecretTextBox);
        AddRow(root, 2, "Redirect URI", _redirectUriTextBox);

        var authPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        authPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        authPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        authPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        _authorizationUrlTextBox.Dock = DockStyle.Fill;
        _authorizationUrlTextBox.ReadOnly = true;
        authPanel.Controls.Add(_authorizationUrlTextBox, 0, 0);

        var copyAuthButton = CreateButton("Link kopyala");
        copyAuthButton.Click += (_, _) => CopyText(_authorizationUrlTextBox.Text, "OAuth linki kopyalandi.");
        authPanel.Controls.Add(copyAuthButton, 1, 0);

        var openAuthButton = CreateButton("Tarayicida ac");
        openAuthButton.Click += (_, _) => OpenUrl(_authorizationUrlTextBox.Text);
        authPanel.Controls.Add(openAuthButton, 2, 0);
        root.Controls.Add(CreateLabel("OAuth linki"), 0, 3);
        root.Controls.Add(authPanel, 1, 3);

        AddRow(root, 4, "Auth code", _authorizationCodeTextBox);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };

        var saveButton = CreateButton("Kaydet");
        saveButton.Click += (_, _) => SaveValues();
        buttonPanel.Controls.Add(saveButton);

        var createAuthButton = CreateButton("OAuth link uret");
        createAuthButton.Click += (_, _) => CreateAuthorizationUrl();
        buttonPanel.Controls.Add(createAuthButton);

        var exchangeButton = CreateButton("Code ile token al");
        exchangeButton.Click += async (_, _) => await ExchangeAuthorizationCodeAsync();
        buttonPanel.Controls.Add(exchangeButton);

        var refreshButton = CreateButton("Token yenile");
        refreshButton.Click += async (_, _) => await RefreshTokenAsync();
        buttonPanel.Controls.Add(refreshButton);

        var testButton = CreateButton("API test");
        testButton.Click += async (_, _) => await TestConnectionAsync();
        buttonPanel.Controls.Add(testButton);

        root.Controls.Add(new Label(), 0, 5);
        root.Controls.Add(buttonPanel, 1, 5);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(CreateLabel("Durum"), 0, 6);
        root.Controls.Add(_statusTextBox, 1, 6);

        var infoLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Not: Magaza raporu shops_r, listings_r ve transactions_r izinlerini ister. AI onerisiyle listing guncellemek icin listings_w izni gerekir. Eski token bu izinleri icermiyorsa OAuth baglantisini yeniden kurun.",
            ForeColor = Color.FromArgb(75, 85, 99),
        };
        root.Controls.Add(new Label(), 0, 7);
        root.Controls.Add(infoLabel, 1, 7);

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        Controls.Add(root);
    }

    private static void AddRow(TableLayoutPanel root, int row, string label, TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(textBox, 1, row);
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 128,
        Height = 34,
        Margin = new Padding(0, 0, 8, 8),
    };

    private void LoadValues()
    {
        _keystringTextBox.Text = _settings.Keystring;
        _sharedSecretTextBox.Text = _settings.SharedSecret;
        _redirectUriTextBox.Text = _settings.RedirectUri;
        WriteStatus($"Ayar dosyasi: {EtsyApiSettingsStore.SettingsPath}");
    }

    private void SaveValues()
    {
        _settings.Keystring = _keystringTextBox.Text.Trim();
        _settings.SharedSecret = _sharedSecretTextBox.Text.Trim();
        _settings.RedirectUri = _redirectUriTextBox.Text.Trim();
        EtsyApiSettingsStore.Save(_settings);
        WriteStatus("Ayarlar kaydedildi.");
    }

    private void CreateAuthorizationUrl()
    {
        try
        {
            SaveValues();
            var uri = _apiClient.CreateAuthorizationUri(_settings, DefaultScopes);
            _authorizationUrlTextBox.Text = uri.ToString();
            EtsyApiSettingsStore.Save(_settings);
            WriteStatus("OAuth linki uretildi. Linki tarayicida acip Etsy'de onay verin.");
        }
        catch (Exception ex)
        {
            WriteStatus(ex.Message);
        }
    }

    private async Task ExchangeAuthorizationCodeAsync()
    {
        try
        {
            SaveValues();
            await _apiClient.ExchangeAuthorizationCodeAsync(_settings, ExtractAuthorizationCode(_authorizationCodeTextBox.Text));
            EtsyApiSettingsStore.Save(_settings);
            WriteStatus($"Token alindi. Access token bitis UTC: {_settings.AccessTokenExpiresAtUtc:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            WriteStatus(ex.Message);
        }
    }

    private async Task RefreshTokenAsync()
    {
        try
        {
            SaveValues();
            await _apiClient.RefreshAccessTokenAsync(_settings);
            EtsyApiSettingsStore.Save(_settings);
            WriteStatus($"Token yenilendi. Access token bitis UTC: {_settings.AccessTokenExpiresAtUtc:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            WriteStatus(ex.Message);
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            SaveValues();
            var result = await _apiClient.TestConnectionAsync(_settings);
            WriteStatus(result);
        }
        catch (Exception ex)
        {
            WriteStatus(ex.Message);
        }
    }

    private void CopyText(string text, string message)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            WriteStatus("Kopyalanacak metin yok.");
            return;
        }

        Clipboard.SetText(text);
        WriteStatus(message);
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            WriteStatus("Acilacak link yok.");
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void WriteStatus(string message)
    {
        _statusTextBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}{Environment.NewLine}{_statusTextBox.Text}";
    }

    private string ExtractAuthorizationCode(string input)
    {
        var value = input.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");

        if (query.TryGetValue("state", out var state) && !string.Equals(state, _settings.LastState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("OAuth state degeri eslesmedi. Guvenlik icin token alinmadi.");
        }

        if (query.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        throw new InvalidOperationException("Yapistirilan URL icinde code parametresi bulunamadi.");
    }
}
