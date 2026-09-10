namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Studio.Services;
using SimilarProductsWinForms.Studio.UI;

/// <summary>
/// Etsy ürün fotoğrafları için profesyonel AI arka plan değiştirme, akıllı prompt asistanı
/// ve toplu düzenleme stüdyosu.
/// </summary>
internal sealed class BackgroundEditorForm : Form
{
    private readonly EtsyApiClient _apiClient;
    private readonly AiOptimizationSettings _aiSettings;
    private readonly EtsyApiSettings _etsySettings;

    // UI Panelleri
    private ModernBeforeAfterSlider _slider = null!;
    private TextBox _txtPrompt = null!;
    private ComboBox _cboEngine = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgress = null!;
    private Label _lblPromptScore = null!;
    private Label _lblPromptTip = null!;
    private ListView _lvImages = null!;
    private ImageList _imageList = null!;
    private ModernButtonControl _btnRunSingle = null!;
    private ModernButtonControl _btnRunBatch = null!;
    private ModernButtonControl _btnMagicEnhance = null!;
    private ModernButtonControl _btnExportFolder = null!;
    private ModernButtonControl _btnUploadEtsy = null!;
    private RadioButton _rbSourceLocal = null!;
    private RadioButton _rbSourceEtsy = null!;

    // Veri Modelleri
    private readonly List<BatchInputItem> _loadedItems = [];
    private readonly Dictionary<string, BatchItemResult> _results = [];
    private BatchInputItem? _selectedItem;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;

    public BackgroundEditorForm(
        EtsyApiClient? apiClient = null,
        AiOptimizationSettings? aiSettings = null,
        string? initialImagePath = null,
        long? initialListingId = null)
    {
        _apiClient = apiClient ?? new EtsyApiClient();
        _aiSettings = aiSettings ?? AiOptimizationSettingsStore.Load();
        _etsySettings = EtsyApiSettingsStore.Load();

        InitializeStudio(initialImagePath, initialListingId);
    }

    private void InitializeStudio(string? initialImagePath, long? initialListingId)
    {
        Text = "🖼️ Etsy AI Arka Plan Stüdyosu & Toplu Düzenleyici";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 740);
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        BuildLayout();

        // Eğer başlangıç görseli verilmişse yükle
        if (!string.IsNullOrWhiteSpace(initialImagePath) && File.Exists(initialImagePath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(initialImagePath);
                var item = new BatchInputItem(
                    Id: "init_1",
                    Title: Path.GetFileNameWithoutExtension(initialImagePath),
                    ImageBytes: bytes,
                    OriginalPathOrUrl: initialImagePath,
                    TargetListingId: initialListingId);

                AddItemsToStudio([item]);
            }
            catch { }
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Üst Header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Ana 3 Panelli Gövde
        Controls.Add(root);

        // 1. ÜST HEADER
        root.Controls.Add(BuildHeaderBar(), 0, 0);

        // 2. ANA 3 PANELLİ GÖVDE
        var mainGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0)
        };
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360)); // Sol: Ayarlar & Prompt
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Orta: Before/After Slider
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320)); // Sağ: Fotoğraf Listesi & Toplu Kuyruk
        root.Controls.Add(mainGrid, 0, 1);

        mainGrid.Controls.Add(BuildLeftPanel(), 0, 0);
        mainGrid.Controls.Add(BuildCenterPanel(), 1, 0);
        mainGrid.Controls.Add(BuildRightPanel(), 2, 0);
    }

    private Control BuildHeaderBar()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "✨ Etsy AI Arka Plan Stüdyosu (GPT Image 2.5 • Gemini • PhotoRoom)",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var rightStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        // Model Badge
        var badge = new Label
        {
            AutoSize = true,
            Text = "⚡ GPT-Image-2.5 Flare & Sunburst Hazır",
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(8, 4, 0, 0)
        };

        var btnConfigureKeys = new Button
        {
            Text = "🔑 API Key Yapılandır",
            Height = 32,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(79, 70, 229), // Indigo
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 4, 0)
        };
        btnConfigureKeys.FlatAppearance.BorderSize = 0;
        btnConfigureKeys.Click += (_, _) => OpenKeyConfigDialog("openai");

        rightStack.Controls.Add(badge);
        rightStack.Controls.Add(btnConfigureKeys);
        header.Controls.Add(rightStack, 2, 0);

        return header;
    }

    private Control BuildLeftPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(30, 41, 59),
            BorderColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12)
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        card.Controls.Add(panel);

        // 1. Motor Seçimi
        panel.Controls.Add(CreateSectionTitle("⚙️ 1. AI İşlem Motoru"));
        _cboEngine = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 320,
            Height = 28,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F)
        };
        _cboEngine.Items.AddRange([
            "⚡ OpenAI GPT-Image-2.5 Flare (Hızlı / Toplu)",
            "🌟 OpenAI GPT-Image-2.5 Sunburst (Vitrin / Yüksek Sadakat)",
            "🍌 Google Gemini Flash Image (Visual Grounding)",
            "✨ PhotoRoom Native AI Background"
        ]);
        _cboEngine.SelectedIndex = 0;
        panel.Controls.Add(_cboEngine);

        // 2. Hazır Sahne Preset'leri
        panel.Controls.Add(CreateSectionTitle("🎨 2. Popüler Etsy Sahne Şablonları"));
        var flowPresets = new FlowLayoutPanel
        {
            Width = 320,
            Height = 110,
            WrapContents = true,
            AutoScroll = true,
            Margin = new Padding(0, 2, 0, 6)
        };
        foreach (var preset in PromptTipsService.Presets)
        {
            var btnChip = new Button
            {
                Text = $"{preset.Icon} {preset.Name}",
                AutoSize = true,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.2F),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 4)
            };
            btnChip.FlatAppearance.BorderSize = 0;
            btnChip.Click += (_, _) =>
            {
                _txtPrompt.Text = preset.Prompt;
                UpdatePromptAnalysis();
            };
            flowPresets.Controls.Add(btnChip);
        }
        panel.Controls.Add(flowPresets);

        // 3. Arka Plan Prompt Alanı
        panel.Controls.Add(CreateSectionTitle("✍️ 3. Arka Plan Sahne Promptu"));
        _txtPrompt = new TextBox
        {
            Multiline = true,
            Width = 320,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            Text = PromptTipsService.Presets[0].Prompt
        };
        _txtPrompt.TextChanged += (_, _) => UpdatePromptAnalysis();
        panel.Controls.Add(_txtPrompt);

        // Canlı Skor & İpucu Kutusu
        _lblPromptScore = new Label
        {
            Width = 320,
            Text = "Kalite Skoru: 80/100 (İyi)",
            ForeColor = Color.FromArgb(52, 211, 153),
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 2)
        };
        panel.Controls.Add(_lblPromptScore);

        _lblPromptTip = new Label
        {
            Width = 320,
            Height = 45,
            Text = "💡 Zemin ve aydınlatma belirtilmiş.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F),
            Margin = new Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(_lblPromptTip);

        // Magic Enhance Butonu
        _btnMagicEnhance = new ModernButtonControl
        {
            Text = "🪄 AI ile Promptu Profesyonelleştir",
            NormalColor = Color.FromArgb(99, 102, 241), // Indigo
            HoverColor = Color.FromArgb(129, 140, 248),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            Width = 320,
            Height = 32,
            Margin = new Padding(0, 0, 0, 10)
        };
        _btnMagicEnhance.Click += async (_, _) => await MagicEnhancePromptAsync();
        panel.Controls.Add(_btnMagicEnhance);

        // 4. İşlem Butonları
        panel.Controls.Add(CreateSectionTitle("🚀 4. Üretim & Düzenleme"));

        _btnRunSingle = new ModernButtonControl
        {
            Text = "▶ Seçili Görselin Arka Planını Değiştir",
            NormalColor = Color.FromArgb(16, 185, 129), // Emerald 500
            HoverColor = Color.FromArgb(52, 211, 153),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            Width = 320,
            Height = 38,
            Margin = new Padding(0, 0, 0, 6)
        };
        _btnRunSingle.Click += async (_, _) => await RunSingleImageAsync();
        panel.Controls.Add(_btnRunSingle);

        _btnRunBatch = new ModernButtonControl
        {
            Text = "⚡ Listedeki Tüm Görselleri Toplu Düzenle",
            NormalColor = Color.FromArgb(245, 158, 11), // Amber 500
            HoverColor = Color.FromArgb(251, 191, 36),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Width = 320,
            Height = 36,
            Margin = new Padding(0, 0, 0, 4)
        };
        _btnRunBatch.Click += async (_, _) => await RunBatchProcessAsync();
        panel.Controls.Add(_btnRunBatch);

        UpdatePromptAnalysis();
        return card;
    }

    private Control BuildCenterPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(30, 41, 59),
            BorderColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Mod çubuğu
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Slider
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Alt Aksiyonlar
        card.Controls.Add(layout);

        // 1. Üst Mod Butonları
        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        var btnSplit = CreateModeButton("⟺ Karşılaştırma Slider'ı", () => _slider.Mode = ImageComparisonMode.SplitSlider);
        var btnSide = CreateModeButton("◫ Yan Yana", () => _slider.Mode = ImageComparisonMode.SideBySide);
        var btnAfter = CreateModeButton("👁️ Yalnızca Sonuç", () => _slider.Mode = ImageComparisonMode.AfterOnly);
        topBar.Controls.AddRange([btnSplit, btnSide, btnAfter]);
        layout.Controls.Add(topBar, 0, 0);

        // 2. Before/After Slider
        _slider = new ModernBeforeAfterSlider
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        layout.Controls.Add(_slider, 0, 1);

        // 3. Alt Aksiyon Çubuğu
        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 6, 0, 0)
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        var lblSliderInfo = new Label
        {
            Dock = DockStyle.Fill,
            Text = "💡 Sol: Orijinal Fotoğraf | Sağ: AI Yeni Arka Plan (Kaydırarak inceleyin)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        bottomBar.Controls.Add(lblSliderInfo, 0, 0);

        var btnSaveSingle = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "💾 Bu Görseli Kaydet",
            NormalColor = Color.FromArgb(51, 65, 85),
            HoverColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        };
        btnSaveSingle.Click += (_, _) => SaveCurrentResult();
        bottomBar.Controls.Add(btnSaveSingle, 1, 0);

        var btnPushSingleEtsy = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📤 Etsy Listing'e Yükle",
            NormalColor = Color.FromArgb(20, 126, 76),
            HoverColor = Color.FromArgb(26, 150, 90),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        };
        btnPushSingleEtsy.Click += async (_, _) => await UploadCurrentToEtsyAsync();
        bottomBar.Controls.Add(btnPushSingleEtsy, 2, 0);

        layout.Controls.Add(bottomBar, 0, 2);
        return card;
    }

    private Control BuildRightPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(30, 41, 59),
            BorderColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0),
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Kaynak Seçimi
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Yükleme Butonları
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Görsel Kuyruk Listesi
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // İlerleme Barı
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Toplu Dışa Aktar Butonları
        card.Controls.Add(layout);

        // 1. Kaynak Seçimi (Radyo Butonlar)
        var flowSource = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        _rbSourceLocal = new RadioButton { Text = "📁 Bilgisayar", AutoSize = true, Checked = true, ForeColor = Color.White };
        _rbSourceEtsy = new RadioButton { Text = "🛒 Etsy Mağazam", AutoSize = true, ForeColor = Color.White };
        flowSource.Controls.AddRange([_rbSourceLocal, _rbSourceEtsy]);
        layout.Controls.Add(flowSource, 0, 0);

        // 2. Yükleme Butonları
        var flowButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        var btnAddFiles = CreateSmallButton("➕ Dosya Seç", async () => await BrowseLocalFilesAsync());
        var btnAddFolder = CreateSmallButton("📂 Klasör Aç", async () => await BrowseLocalFolderAsync());
        var btnFetchEtsy = CreateSmallButton("🔄 Etsy'den Getir", async () => await FetchEtsyListingsAsync());
        flowButtons.Controls.AddRange([btnAddFiles, btnAddFolder, btnFetchEtsy]);
        layout.Controls.Add(flowButtons, 0, 1);

        // 3. ListView Kuyruğu
        _imageList = new ImageList { ImageSize = new Size(48, 48), ColorDepth = ColorDepth.Depth32Bit };
        _lvImages = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White,
            SmallImageList = _imageList,
            Font = new Font("Segoe UI", 8.8F)
        };
        _lvImages.Columns.Add("Görsel / Ürün", 180);
        _lvImages.Columns.Add("Durum", 100);
        _lvImages.SelectedIndexChanged += (_, _) => OnSelectedImageChanged();
        layout.Controls.Add(_lvImages, 0, 2);

        // 4. İlerleme Alanı
        var flowProgress = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0, 4, 0, 0) };
        flowProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        flowProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        _lblProgress = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Kuyruk: 0 görsel hazır",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Maximum = 100,
            Value = 0
        };
        flowProgress.Controls.Add(_lblProgress, 0, 0);
        flowProgress.Controls.Add(_progressBar, 0, 1);
        layout.Controls.Add(flowProgress, 0, 3);

        // 5. Alt Toplu İhracat Butonları
        var flowExport = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 4, 0, 0) };
        flowExport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        flowExport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _btnExportFolder = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📁 Klasöre İndir",
            NormalColor = Color.FromArgb(51, 65, 85),
            HoverColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold)
        };
        _btnExportFolder.Click += async (_, _) => await ExportBatchResultsAsync();
        flowExport.Controls.Add(_btnExportFolder, 0, 0);

        _btnUploadEtsy = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "📤 Etsy'ye Toplu Yükle",
            NormalColor = Color.FromArgb(20, 126, 76),
            HoverColor = Color.FromArgb(26, 150, 90),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold)
        };
        _btnUploadEtsy.Click += async (_, _) => await UploadBatchToEtsyAsync();
        flowExport.Controls.Add(_btnUploadEtsy, 1, 0);

        layout.Controls.Add(flowExport, 0, 4);

        return card;
    }

    #region Kuyruk ve Görsel Seçimi

    private void AddItemsToStudio(List<BatchInputItem> newItems)
    {
        foreach (var item in newItems)
        {
            _loadedItems.Add(item);

            // Thumbnail üret
            try
            {
                using var ms = new MemoryStream(item.ImageBytes);
                using var bmp = new Bitmap(ms);
                _imageList.Images.Add(item.Id, new Bitmap(bmp, 48, 48));
            }
            catch
            {
                var blank = new Bitmap(48, 48);
                _imageList.Images.Add(item.Id, blank);
            }

            var lvi = new ListViewItem(item.Title)
            {
                ImageKey = item.Id,
                Tag = item
            };
            lvi.SubItems.Add("Hazır");
            _lvImages.Items.Add(lvi);
        }

        _lblProgress.Text = $"Kuyruk: {_loadedItems.Count} görsel hazır";
        if (_selectedItem == null && _loadedItems.Count > 0)
        {
            _lvImages.Items[0].Selected = true;
        }
    }

    private void OnSelectedImageChanged()
    {
        if (_lvImages.SelectedItems.Count == 0) return;

        var lvi = _lvImages.SelectedItems[0];
        if (lvi.Tag is not BatchInputItem item) return;

        _selectedItem = item;

        try
        {
            using var ms = new MemoryStream(item.ImageBytes);
            _slider.BeforeImage = new Bitmap(ms);

            if (_results.TryGetValue(item.Id, out var result) && result.EditedImage != null)
            {
                _slider.AfterImage = result.EditedImage;
            }
            else
            {
                _slider.AfterImage = null;
            }
        }
        catch { }
    }

    private async Task BrowseLocalFilesAsync()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Düzenlenecek Ürün Fotoğraflarını Seçin",
            Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tüm Dosyalar (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            var items = await BatchBackgroundChangeService.LoadItemsFromLocalFilesAsync(ofd.FileNames);
            AddItemsToStudio(items);
        }
    }

    private async Task BrowseLocalFolderAsync()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Ürün fotoğraflarının bulunduğu klasörü seçin"
        };

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            var exts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var files = Directory.GetFiles(fbd.SelectedPath)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (files.Count == 0)
            {
                MessageBox.Show(this, "Seçilen klasörde desteklenen görsel dosyası bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var items = await BatchBackgroundChangeService.LoadItemsFromLocalFilesAsync(files);
            AddItemsToStudio(items);
        }
    }

    private async Task FetchEtsyListingsAsync()
    {
        try
        {
            _lblProgress.Text = "Etsy mağazasından aktif listingler çekiliyor...";
            var listings = await _apiClient.GetOwnShopActiveListingsAsync(_etsySettings, limit: 30);

            if (listings == null || listings.Count == 0)
            {
                MessageBox.Show(this, "Mağazada aktif listing bulunamadı veya API yetkisi yetersiz.", "Etsy Listingleri", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var listWithImages = new List<(long ListingId, string Title, string ImageUrl)>();
            foreach (var l in listings)
            {
                if (!string.IsNullOrWhiteSpace(l.ImageUrl))
                {
                    listWithImages.Add((l.ListingId, l.Title, l.ImageUrl));
                }
                else if (l.ListingId > 0)
                {
                    try
                    {
                        var urls = await _apiClient.GetListingImagesAsync(_etsySettings, l.ListingId);
                        if (urls.Count > 0)
                        {
                            listWithImages.Add((l.ListingId, l.Title, urls[0]));
                        }
                    }
                    catch { }
                }
            }

            var progress = new Progress<string>(msg => _lblProgress.Text = msg);
            var items = await BatchBackgroundChangeService.LoadItemsFromEtsyUrlsAsync(listWithImages, progress);
            AddItemsToStudio(items);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Etsy listingleri alınırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Üretim & AI İşlemleri

    private void UpdatePromptAnalysis()
    {
        var analysis = PromptTipsService.AnalyzePrompt(_txtPrompt.Text);
        _lblPromptScore.Text = $"Kalite Skoru: {analysis.Score}/100" + (analysis.Score >= 75 ? " (Mükemmel)" : (analysis.Score >= 50 ? " (İyi)" : " (Geliştirilmeli)"));
        _lblPromptScore.ForeColor = analysis.Score >= 75 ? Color.FromArgb(52, 211, 153) : (analysis.Score >= 50 ? Color.FromArgb(251, 191, 36) : Color.FromArgb(248, 113, 113));

        if (analysis.Tips.Count > 0)
        {
            _lblPromptTip.Text = analysis.Tips[0];
        }
        else if (analysis.Strengths.Count > 0)
        {
            _lblPromptTip.Text = analysis.Strengths[0];
        }
    }

    private async Task MagicEnhancePromptAsync()
    {
        string currentPrompt = _txtPrompt.Text.Trim();
        string productTitle = _selectedItem?.Title ?? "Handcrafted Etsy Artisan Product";

        _btnMagicEnhance.Enabled = false;
        _btnMagicEnhance.Text = "⏳ İyileştiriliyor...";

        try
        {
            string enhanced = await AiImageGenerationService.EnhancePromptAsync(currentPrompt, productTitle, _aiSettings);
            _txtPrompt.Text = enhanced;
            UpdatePromptAnalysis();
        }
        finally
        {
            _btnMagicEnhance.Enabled = true;
            _btnMagicEnhance.Text = "🪄 AI ile Promptu Profesyonelleştir";
        }
    }

    private void OpenKeyConfigDialog(string? focus = "openai")
    {
        using var dlg = new StudioKeyConfigDialog(focus);
        if (dlg.ShowDialog(this) == DialogResult.OK || true)
        {
            var fresh = AiOptimizationSettingsStore.Load();
            _aiSettings.OpenAiApiKey = fresh.OpenAiApiKey;
            _aiSettings.GeminiApiKey = fresh.GeminiApiKey;
            _aiSettings.PhotoRoomApiKey = fresh.PhotoRoomApiKey;
            _aiSettings.BflApiKey = fresh.BflApiKey;
            _aiSettings.IdeogramApiKey = fresh.IdeogramApiKey;
        }
    }

    private bool EnsureApiKeyConfigured(int engineIndex)
    {
        string engineKeyName = engineIndex switch
        {
            0 or 1 => "OpenAI",
            2 => "Google Gemini",
            3 => "PhotoRoom",
            _ => "OpenAI"
        };

        bool hasKey = engineIndex switch
        {
            0 or 1 => !string.IsNullOrWhiteSpace(_aiSettings.OpenAiApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.OpenAiApiKey),
            2 => !string.IsNullOrWhiteSpace(_aiSettings.GeminiApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.GoogleGeminiApiKey),
            3 => !string.IsNullOrWhiteSpace(_aiSettings.PhotoRoomApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.PhotoRoomApiKey),
            _ => true
        };

        if (!hasKey)
        {
            var res = MessageBox.Show(this,
                $"{engineKeyName} motoru için henüz bir API Anahtarı girilmemiş.\n\nİşlemi başlatabilmek için şimdi anahtarınızı yapılandırmak ister misiniz?",
                $"{engineKeyName} API Anahtarı Gerekli",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                OpenKeyConfigDialog(engineIndex == 2 ? "gemini" : (engineIndex == 3 ? "photoroom" : "openai"));
                return engineIndex switch
                {
                    0 or 1 => !string.IsNullOrWhiteSpace(_aiSettings.OpenAiApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.OpenAiApiKey),
                    2 => !string.IsNullOrWhiteSpace(_aiSettings.GeminiApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.GoogleGeminiApiKey),
                    3 => !string.IsNullOrWhiteSpace(_aiSettings.PhotoRoomApiKey) || !string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.PhotoRoomApiKey),
                    _ => true
                };
            }
            return false;
        }

        return true;
    }

    private async Task RunSingleImageAsync()
    {
        if (_selectedItem == null)
        {
            MessageBox.Show(this, "Lütfen önce sağ taraftan işlenecek bir görsel seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_isProcessing) return;

        int engine = _cboEngine.SelectedIndex;
        if (!EnsureApiKeyConfigured(engine)) return;

        SetBusy(true);
        _lblProgress.Text = $"'{_selectedItem.Title}' işleniyor...";

        try
        {
            _cts = new CancellationTokenSource();
            var singleList = new List<BatchInputItem> { _selectedItem };

            var results = await BatchBackgroundChangeService.RunBatchAsync(
                singleList,
                _txtPrompt.Text.Trim(),
                engine,
                _aiSettings,
                maxParallel: 1,
                ct: _cts.Token);

            if (results.Count > 0)
            {
                var r = results[0];
                _results[_selectedItem.Id] = r;

                if (r.Success && r.EditedImage != null)
                {
                    _slider.AfterImage = r.EditedImage;
                    UpdateListItemStatus(_selectedItem.Id, "✅ Başarılı");
                    _lblProgress.Text = $"✅ Tamamlandı ({r.ElapsedMs} ms)";
                }
                else
                {
                    UpdateListItemStatus(_selectedItem.Id, "❌ Hata");
                    _lblProgress.Text = $"Hata: {r.ErrorMessage}";
                    MessageBox.Show(this, r.ErrorMessage ?? "Bilinmeyen hata", "Üretim Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _lblProgress.Text = $"Hata: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunBatchProcessAsync()
    {
        if (_loadedItems.Count == 0)
        {
            MessageBox.Show(this, "Kuyrukta işlenecek görsel bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_isProcessing) return;

        int engine = _cboEngine.SelectedIndex;
        if (!EnsureApiKeyConfigured(engine)) return;

        SetBusy(true);
        _progressBar.Value = 0;
        _progressBar.Maximum = _loadedItems.Count;

        try
        {
            _cts = new CancellationTokenSource();

            var progress = new Progress<BatchProgressReport>(report =>
            {
                _progressBar.Value = Math.Min(report.CurrentIndex, _progressBar.Maximum);
                _lblProgress.Text = report.StatusMessage;

                if (report.LastCompletedItem != null)
                {
                    _results[report.LastCompletedItem.Id] = report.LastCompletedItem;
                    UpdateListItemStatus(
                        report.LastCompletedItem.Id,
                        report.LastCompletedItem.Success ? "✅ Başarılı" : "❌ Hata");

                    if (_selectedItem?.Id == report.LastCompletedItem.Id && report.LastCompletedItem.EditedImage != null)
                    {
                        _slider.AfterImage = report.LastCompletedItem.EditedImage;
                    }
                }
            });

            var batchResults = await BatchBackgroundChangeService.RunBatchAsync(
                _loadedItems,
                _txtPrompt.Text.Trim(),
                engine,
                _aiSettings,
                maxParallel: 2,
                progress: progress,
                ct: _cts.Token);

            int successCount = batchResults.Count(r => r.Success);
            _lblProgress.Text = $"🎉 Toplu İşlem Bitti: {successCount}/{_loadedItems.Count} Başarılı!";
            MessageBox.Show(this, $"{successCount} görselin arka planı başarıyla yenilendi!", "Toplu İşlem Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _lblProgress.Text = "İşlem iptal edildi.";
        }
        catch (Exception ex)
        {
            _lblProgress.Text = $"Toplu işlem hatası: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateListItemStatus(string itemId, string status)
    {
        foreach (ListViewItem lvi in _lvImages.Items)
        {
            if (lvi.Tag is BatchInputItem it && it.Id == itemId)
            {
                if (lvi.SubItems.Count > 1)
                {
                    lvi.SubItems[1].Text = status;
                }
                break;
            }
        }
    }

    private void SetBusy(bool busy)
    {
        _isProcessing = busy;
        _btnRunSingle.Enabled = !busy;
        _btnRunBatch.Enabled = !busy;
        _cboEngine.Enabled = !busy;
        UseWaitCursor = busy;
    }

    #endregion

    #region Kaydetme & Etsy İhracatı

    private void SaveCurrentResult()
    {
        if (_slider.AfterImage == null)
        {
            MessageBox.Show(this, "Kaydedilecek düzenlenmiş görsel bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Düzenlenmiş Görseli Kaydet",
            Filter = "PNG Görseli (*.png)|*.png|JPEG Görseli (*.jpg)|*.jpg",
            FileName = $"{_selectedItem?.Title ?? "etsy_product"}_ai_bg.png"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var fmt = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png;
            _slider.AfterImage.Save(sfd.FileName, fmt);
            MessageBox.Show(this, "Görsel başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task UploadCurrentToEtsyAsync()
    {
        if (_slider.AfterImage == null)
        {
            MessageBox.Show(this, "Etsy'ye yüklenecek düzenlenmiş görsel bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long? listingId = _selectedItem?.TargetListingId;
        if (listingId is null or <= 0)
        {
            // Kullanıcıdan listing ID girmesini iste
            string input = Microsoft.VisualBasic.Interaction.InputBox("Lütfen görselin yükleneceği Etsy Listing ID'sini girin:", "Etsy Listing ID", "");
            if (long.TryParse(input, out long id) && id > 0)
            {
                listingId = id;
            }
            else
            {
                return;
            }
        }

        string tempFile = Path.Combine(Path.GetTempPath(), $"etsy_upload_{listingId}_{Guid.NewGuid():N}.png");
        try
        {
            _slider.AfterImage.Save(tempFile, ImageFormat.Png);
            _lblProgress.Text = $"Listing #{listingId} için görsel Etsy'ye yükleniyor...";

            await _apiClient.UploadOwnShopListingImageAsync(_etsySettings, listingId.Value, tempFile);
            _lblProgress.Text = $"✅ Listing #{listingId} görseli başarıyla güncellendi!";
            MessageBox.Show(this, $"Görsel Etsy listing #{listingId} fotoğrafları arasına başarıyla eklendi!", "Etsy Güncellendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Etsy'ye yüklenirken hata oluştu: {ex.Message}", "Yükleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private async Task ExportBatchResultsAsync()
    {
        var completed = _results.Values.Where(r => r.Success && r.EditedImage != null).ToList();
        if (completed.Count == 0)
        {
            MessageBox.Show(this, "Henüz başarıyla üretilmiş görsel bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var fbd = new FolderBrowserDialog
        {
            Description = "Düzenlenen görsellerin kaydedileceği klasörü seçin"
        };

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            int saved = await BatchBackgroundChangeService.ExportResultsToFolderAsync(completed, fbd.SelectedPath);
            MessageBox.Show(this, $"{saved} adet görsel '{fbd.SelectedPath}' klasörüne başarıyla kaydedildi!", "Dışa Aktarma Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task UploadBatchToEtsyAsync()
    {
        var itemsWithListing = _results.Values.Where(r => r.Success && r.EditedImage != null && r.TargetListingId is > 0).ToList();
        if (itemsWithListing.Count == 0)
        {
            MessageBox.Show(this, "Etsy Listing ID'sine sahip tamamlanmış görsel bulunamadı.\n(Yalnızca 'Etsy Mağazam' kaynağından çekilen görseller doğrudan yüklenebilir)", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"{itemsWithListing.Count} adet görseli kendi Etsy mağazanızdaki listing'lere yüklemek istiyor musunuz?", "Etsy Toplu Yükleme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var progress = new Progress<string>(msg => _lblProgress.Text = msg);
        var (success, failed, errors) = await BatchBackgroundChangeService.UploadResultsToEtsyAsync(itemsWithListing, _apiClient, _etsySettings, progress);

        string msg = $"{success} görsel başarıyla Etsy'ye yüklendi!";
        if (failed > 0)
        {
            msg += $"\n{failed} görsel yüklenemedi:\n" + string.Join("\n", errors.Take(5));
        }

        MessageBox.Show(this, msg, "Toplu Yükleme Sonucu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion

    #region Yardımcı UI Metodları

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4)
        };
    }

    private static Button CreateSmallButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 27,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.2F),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 4, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateModeButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8.5F),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    #endregion
}
