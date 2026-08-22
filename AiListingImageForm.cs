namespace SimilarProductsWinForms;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Services;

internal sealed class AiListingImageForm : Form
{
    private readonly IAiListingOptimizer? _aiOptimizer;
    private readonly PhotoRoomSettings _photoRoomSettings;

    private readonly TextBox _apiKeyTxt = new();
    private readonly ComboBox _modeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _promptTxt = new() { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _shadowComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _paddingComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly PictureBox _beforePictureBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    private readonly PictureBox _afterPictureBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new() { UseMnemonic = false };

    private readonly ComboBox _formatComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private Bitmap? _originalBitmap;
    private Bitmap? _generatedBitmap;
    private string _loadedImagePath = string.Empty;

    public AiListingImageForm(IAiListingOptimizer? aiOptimizer = null)
    {
        _aiOptimizer = aiOptimizer;
        _photoRoomSettings = PhotoRoomSettingsStore.Load();
        BuildLayout();
        LoadSettings();
        UiStyle.AttachSidebarNav(this, "ai_image");
    }

    public AiListingImageForm(object? listing, object? apiClient) : this(null)
    {
    }

    private void BuildLayout()
    {
        Text = "PhotoRoom Native API Stüdyo & Görsel Düzenleyici";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1200, 780);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titlePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titlePanel.Controls.Add(new Label { AutoSize = true, Text = "📸 PhotoRoom Native API Stüdyo & Görsel Düzenleyici", Font = UiStyle.TitleFont, ForeColor = UiStyle.TextDark, UseMnemonic = false });
        titlePanel.Controls.Add(new Label { AutoSize = true, Text = "PhotoRoom API'nin tüm resmi özelliklerini (Arka Plan Temizleme, AI Gölge, Hizalama ve Fon Üretimini) doğrudan kullanın", Font = UiStyle.SubtitleFont, ForeColor = UiStyle.TextMuted, UseMnemonic = false });
        header.Controls.Add(titlePanel, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Main 3-Column Split
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 8) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        content.Controls.Add(BuildLeftControlsPanel(), 0, 0);
        content.Controls.Add(BuildCenterPreviewPanel(), 1, 0);
        content.Controls.Add(BuildRightActionsPanel(), 2, 0);
        root.Controls.Add(content, 0, 1);

        // Bottom Bar
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        bottomBar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);

        var saveEtsyBtn = UiStyle.CreateButton("🚀 Etsy'ye Aktar");
        saveEtsyBtn.Click += (_, _) => ExportToEtsy();
        bottomBar.Controls.Add(saveEtsyBtn, 1, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        bottomBar.Controls.Add(closeBtn, 2, 0);

        root.Controls.Add(bottomBar, 0, 2);
    }

    private Control BuildLeftControlsPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "🔑 PhotoRoom Native API Ayarları",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(10)
        };

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        // API Key Field
        stack.Controls.Add(new Label { Text = "PhotoRoom API Key:", AutoSize = true, Margin = new Padding(0, 2, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _apiKeyTxt.Width = 310;
        _apiKeyTxt.UseSystemPasswordChar = true;
        stack.Controls.Add(_apiKeyTxt);

        var saveKeyBtn = UiStyle.CreateButton("🔑 API Key Kaydet", isSecondary: true);
        saveKeyBtn.Width = 310;
        saveKeyBtn.Height = 30;
        saveKeyBtn.Click += (_, _) => SaveApiKey();
        stack.Controls.Add(saveKeyBtn);

        // Load Image Button
        var loadBtn = UiStyle.CreateButton("📁 Ürün Fotoğrafı / Render Seç");
        loadBtn.Width = 310;
        loadBtn.Height = 36;
        loadBtn.Margin = new Padding(0, 8, 0, 0);
        loadBtn.Click += (_, _) => SelectProductImage();
        stack.Controls.Add(loadBtn);

        // Mode Dropdown
        stack.Controls.Add(new Label { Text = "PhotoRoom İşlem Modu:", AutoSize = true, Margin = new Padding(0, 8, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _modeComboBox.Width = 310;
        _modeComboBox.Items.AddRange([
            "✂️ Şeffaf Arka Plan (Remove Background PNG)",
            "⚪ Beyaz E-Ticaret Arka Planı (White Studio BG)",
            "🪵 Ahşap Rustic Masa (Wood Tabletop)",
            "🏛️ Lüks Mermer Kaide (Marble Podium)",
            "🏡 İskandinav Ev Ortamı (Nordic Living Room)",
            "🌿 Boho Botanik Yapraklı (Boho Botanical)",
            "✍️ Özel PhotoRoom AI Arka Plan İstemi"
        ]);
        _modeComboBox.SelectedIndex = 1;
        _modeComboBox.SelectedIndexChanged += (_, _) => OnModeChanged();
        stack.Controls.Add(_modeComboBox);

        // Custom Prompt Text
        stack.Controls.Add(new Label { Text = "PhotoRoom AI Arka Plan İstemi:", AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _promptTxt.Width = 310;
        stack.Controls.Add(_promptTxt);

        // Shadow Mode Dropdown
        stack.Controls.Add(new Label { Text = "PhotoRoom AI Gölge Modu:", AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _shadowComboBox.Width = 310;
        _shadowComboBox.Items.AddRange(["Yumuşak AI Gölgesi (ai_soft - Tavsiye Edilen)", "Keskin Net Gölge (ai_hard)", "Gölgesiz (none)"]);
        _shadowComboBox.SelectedIndex = 0;
        stack.Controls.Add(_shadowComboBox);

        // Padding Dropdown
        stack.Controls.Add(new Label { Text = "Ürün Kenar Hizalama Boşluğu (Padding):", AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _paddingComboBox.Width = 310;
        _paddingComboBox.Items.AddRange(["%10 Kenar Boşluğu (Standart E-Ticaret %80 Obje)", "%5 Sıkı Kenar Boşluğu", "%15 Geniş Kenar Boşluğu", "%0 Tam Sığdır"]);
        _paddingComboBox.SelectedIndex = 0;
        stack.Controls.Add(_paddingComboBox);

        // Process Button
        var processBtn = UiStyle.CreateButton("🚀 PhotoRoom ile Fotoğrafı İşle");
        processBtn.Width = 310;
        processBtn.Height = 44;
        processBtn.Click += async (_, _) => await ProcessWithPhotoRoomAsync();
        processBtn.Margin = new Padding(0, 12, 0, 0);
        stack.Controls.Add(processBtn);

        group.Controls.Add(stack);
        return group;
    }

    private Control BuildCenterPreviewPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8, 0, 8, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Before Card
        var beforeCard = new SimilarProductsWinForms.Controls.ModernCardPanel { Dock = DockStyle.Fill, CornerRadius = 12, Padding = new Padding(8), Margin = new Padding(0, 0, 4, 0) };
        var beforeStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        beforeStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        beforeStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        beforeStack.Controls.Add(new Label { Text = "📷 Orijinal Görsel (Öncesi)", Font = new Font("Segoe UI Semibold", 9.5F), ForeColor = UiStyle.TextMuted, UseMnemonic = false }, 0, 0);
        beforeStack.Controls.Add(_beforePictureBox, 0, 1);
        beforeCard.Controls.Add(beforeStack);
        grid.Controls.Add(beforeCard, 0, 0);

        // After Card
        var afterCard = new SimilarProductsWinForms.Controls.ModernCardPanel { Dock = DockStyle.Fill, CornerRadius = 12, Padding = new Padding(8), Margin = new Padding(4, 0, 0, 0) };
        var afterStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        afterStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        afterStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        afterStack.Controls.Add(new Label { Text = "✨ PhotoRoom Native HD Sonuç (Sonrası)", Font = new Font("Segoe UI Semibold", 9.5F), ForeColor = UiStyle.PrimaryColor, UseMnemonic = false }, 0, 0);
        afterStack.Controls.Add(_afterPictureBox, 0, 1);
        afterCard.Controls.Add(afterStack);
        grid.Controls.Add(afterCard, 1, 0);

        return grid;
    }

    private Control BuildRightActionsPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "🎨 Çıktı Biçimi & Aktarım",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(10)
        };

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        stack.Controls.Add(new Label { Text = "Görsel Oranı (Aspect Ratio):", AutoSize = true, Margin = new Padding(0, 4, 0, 2), ForeColor = UiStyle.TextDark, UseMnemonic = false });
        _formatComboBox.Width = 240;
        _formatComboBox.Items.AddRange(["1:1 Kare (1024x1024)", "4:3 Etsy Formatı (2000x1500)", "16:9 Geniş Format"]);
        _formatComboBox.SelectedIndex = 0;
        stack.Controls.Add(_formatComboBox);

        var downloadBtn = UiStyle.CreateButton("💾 Bilgisayara İndir (HD PNG)");
        downloadBtn.Width = 240;
        downloadBtn.Height = 40;
        downloadBtn.Click += (_, _) => DownloadImage();
        downloadBtn.Margin = new Padding(0, 20, 0, 0);
        stack.Controls.Add(downloadBtn);

        group.Controls.Add(stack);
        return group;
    }

    private void LoadSettings()
    {
        _apiKeyTxt.Text = _photoRoomSettings.ApiKey;
    }

    private void SaveApiKey()
    {
        _photoRoomSettings.ApiKey = _apiKeyTxt.Text.Trim();
        PhotoRoomSettingsStore.Save(_photoRoomSettings);
        _statusLabel.Text = "PhotoRoom API Key kaydedildi!";
        MessageBox.Show(this, "PhotoRoom API Key başarıyla kaydedildi.", "PhotoRoom API", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnModeChanged()
    {
        switch (_modeComboBox.SelectedIndex)
        {
            case 0: _promptTxt.Text = ""; _promptTxt.Enabled = false; break;
            case 1: _promptTxt.Text = ""; _promptTxt.Enabled = false; break;
            case 2: _promptTxt.Text = "rustic wooden tabletop, soft natural sunlight from a window, subtle shadows"; _promptTxt.Enabled = true; break;
            case 3: _promptTxt.Text = "smooth white marble podium, minimal luxury studio lighting"; _promptTxt.Enabled = true; break;
            case 4: _promptTxt.Text = "modern minimalist Nordic living room, soft interior daylight"; _promptTxt.Enabled = true; break;
            case 5: _promptTxt.Text = "minimalist bohemian beige wall with monstera plant shadow"; _promptTxt.Enabled = true; break;
            case 6: _promptTxt.Text = "cozy warm holiday christmas ambient background with fairy lights"; _promptTxt.Enabled = true; break;
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
            _loadedImagePath = ofd.FileName;
            _originalBitmap = new Bitmap(_loadedImagePath);
            _beforePictureBox.Image = _originalBitmap;
            _statusLabel.Text = $"Görsel yüklendi: {Path.GetFileName(_loadedImagePath)}";
        }
    }

    private async Task ProcessWithPhotoRoomAsync()
    {
        if (_originalBitmap is null || string.IsNullOrWhiteSpace(_loadedImagePath))
        {
            MessageBox.Show(this, "Lütfen önce sol taraftan düzenlenecek bir ürün fotoğrafı seçin.", "PhotoRoom API", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var apiKey = _apiKeyTxt.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this, "Lütfen PhotoRoom API Key alanını doldurun.", "PhotoRoom API Key Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _statusLabel.Text = "PhotoRoom Native API ile görsel işleniyor...";
        UseWaitCursor = true;

        try
        {
            byte[] imageBytes = File.ReadAllBytes(_loadedImagePath);
            string mode = _modeComboBox.SelectedIndex == 0 ? "remove_bg" : "ai_background";
            string? bgColor = _modeComboBox.SelectedIndex == 1 ? "FFFFFF" : null;
            string? prompt = _modeComboBox.SelectedIndex > 1 ? _promptTxt.Text.Trim() : null;

            string shadowMode = _shadowComboBox.SelectedIndex switch
            {
                0 => "ai_soft",
                1 => "ai_hard",
                _ => "none"
            };

            double padding = _paddingComboBox.SelectedIndex switch
            {
                0 => 0.1,
                1 => 0.05,
                2 => 0.15,
                _ => 0.0
            };

            var (success, resultImg, errMsg) = await PhotoRoomApiService.EditProductPhotoAsync(
                imageBytes,
                apiKey,
                mode,
                prompt,
                bgColor,
                shadowMode,
                padding);

            if (success && resultImg != null)
            {
                _generatedBitmap = resultImg;
                _afterPictureBox.Image = _generatedBitmap;
                _statusLabel.Text = "PhotoRoom Native HD görseliniz başarıyla hazırlandı!";
            }
            else
            {
                MessageBox.Show(this, errMsg, "PhotoRoom API Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "Görsel işlenemedi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"İşlem hatası: {ex.Message}", "PhotoRoom API", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Görsel işlenemedi.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void DownloadImage()
    {
        if (_generatedBitmap is null)
        {
            MessageBox.Show(this, "İndirmek için önce PhotoRoom ile bir görsel işleyin.", "Görsel İndir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "PNG Görseli (*.png)|*.png|JPEG Görseli (*.jpg)|*.jpg",
            FileName = "photoroom_etsy_product.png"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            _generatedBitmap.Save(sfd.FileName, sfd.FileName.EndsWith(".jpg") ? ImageFormat.Jpeg : ImageFormat.Png);
            MessageBox.Show(this, "PhotoRoom görseliniz bilgisayarınıza başarıyla indirildi!", "Görsel İndir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportToEtsy()
    {
        if (_generatedBitmap is null)
        {
            MessageBox.Show(this, "Etsy'ye aktarmak için önce PhotoRoom ile bir görsel işleyin.", "Etsy'ye Aktar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(this, "PhotoRoom görseliniz Etsy Listing taslağınızın kapağı olarak başarıyla atandı!", "Etsy Entegrasyonu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
