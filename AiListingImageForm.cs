namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AiListingImageForm(
    MarketListingResult listing,
    EtsyApiClient apiClient) : Form
{
    private readonly AiListingImageGenerator _imageGenerator = new();
    private readonly PictureBox _previewBox = new();
    private readonly TextBox _promptTextBox = new();
    private readonly TextBox _statusTextBox = new();
    private string _selectedImagePath = "";

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "AI Gorsel Uret ve Etsy'ye Ekle";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 720);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(16);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"AI gorsel: {listing.Title}",
            Font = new Font("Segoe UI Semibold", 15F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _promptTextBox.Dock = DockStyle.Fill;
        _promptTextBox.Multiline = true;
        _promptTextBox.ScrollBars = ScrollBars.Vertical;
        _promptTextBox.Text = BuildDefaultPrompt();
        root.Controls.Add(_promptTextBox, 0, 1);

        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        _previewBox.Dock = DockStyle.Fill;
        _previewBox.SizeMode = PictureBoxSizeMode.Zoom;
        _previewBox.BackColor = Color.White;
        _previewBox.BorderStyle = BorderStyle.FixedSingle;
        previewLayout.Controls.Add(_previewBox, 0, 0);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        _statusTextBox.BackColor = Color.White;
        _statusTextBox.Text = "Promptu istedigin gibi yazabilirsin. Saglayici AI Ayarlari ekranindan secilir: OpenAI veya Gemini. Etsy'ye yukleme icin son onay istenir.";
        previewLayout.Controls.Add(_statusTextBox, 1, 0);
        root.Controls.Add(previewLayout, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var close = CreateButton("Kapat", Color.FromArgb(82, 93, 110));
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);

        var upload = CreateButton("Etsy'ye Gorsel Ekle", Color.FromArgb(20, 126, 76));
        upload.Click += async (_, _) => await UploadSelectedImageAsync();
        buttons.Controls.Add(upload);

        var choose = CreateButton("Dosyadan Sec", Color.FromArgb(32, 97, 165));
        choose.Click += (_, _) => ChooseImage();
        buttons.Controls.Add(choose);

        var generate = CreateButton("AI ile Gorsel Uret", Color.FromArgb(32, 97, 165));
        generate.Click += async (_, _) => await GenerateImageAsync();
        buttons.Controls.Add(generate);
        root.Controls.Add(buttons, 0, 3);
    }

    private string BuildDefaultPrompt() =>
        $"Create an Etsy product photo/mockup for this product: {listing.Title}.{Environment.NewLine}{Environment.NewLine}" +
        "Kullanici istegi: clean neutral background, realistic lighting, marketplace-ready composition, no watermark, no logo, no copyrighted character branding.";

    private async Task GenerateImageAsync()
    {
        try
        {
            UseWaitCursor = true;
            var settings = AiOptimizationSettingsStore.Load();
            WriteStatus($"{settings.Provider} ile AI gorsel uretiliyor...");
            _selectedImagePath = await _imageGenerator.GenerateAsync(settings, listing, _promptTextBox.Text);
            LoadPreview(_selectedImagePath);
            WriteStatus($"Gorsel uretildi: {_selectedImagePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            WriteStatus(ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ChooseImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Etsy'ye eklenecek gorseli sec",
            Filter = "Gorseller|*.png;*.jpg;*.jpeg;*.webp;*.gif",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedImagePath = dialog.FileName;
        LoadPreview(_selectedImagePath);
        WriteStatus($"Gorsel secildi: {_selectedImagePath}");
    }

    private async Task UploadSelectedImageAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedImagePath) || !File.Exists(_selectedImagePath))
        {
            MessageBox.Show(this, "Once AI ile gorsel uretin veya dosyadan gorsel secin.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "Bu gorsel secili Etsy listing'e eklenecek. Gorselde telif/marka riski olmadigini kontrol ettiniz mi?",
            "Etsy gorsel ekleme onayi",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            WriteStatus("Etsy'ye gorsel yukleniyor...");
            var settings = EtsyApiSettingsStore.Load();
            await apiClient.UploadOwnShopListingImageAsync(settings, listing.ListingId, _selectedImagePath);
            EtsyApiSettingsStore.Save(settings);
            WriteStatus("Gorsel Etsy listing'e eklendi. Listing sayfasindan kontrol edin.");
            MessageBox.Show(this, "Gorsel Etsy listing'e eklendi.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            WriteStatus(ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void LoadPreview(string path)
    {
        using var image = Image.FromFile(path);
        _previewBox.Image?.Dispose();
        _previewBox.Image = new Bitmap(image);
    }

    private void WriteStatus(string message)
    {
        _statusTextBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}{Environment.NewLine}{_statusTextBox.Text}";
    }

    private static Button CreateButton(string text, Color backColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 170,
            Height = 38,
            Margin = new Padding(8, 10, 0, 10),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
