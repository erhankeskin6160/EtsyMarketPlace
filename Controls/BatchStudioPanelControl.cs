namespace SimilarProductsWinForms.Controls;

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
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Studio.Services;
using SimilarProductsWinForms.Studio.UI;

/// <summary>
/// Çoklu ürün fotoğrafları için toplu AI arka plan değiştirme,
/// akıllı prompt asistanı ve Etsy mağaza senkronizasyon kontrolü.
/// Hem AI Görsel Studio içerisinde sekme olarak hem de bağımsız çalışabilir.
/// </summary>
internal sealed class BatchStudioPanelControl : UserControl
{
    private readonly EtsyApiClient _apiClient;
    private readonly AiOptimizationSettings _aiSettings;
    private readonly EtsyApiSettings _etsySettings;

    // UI Panelleri
    private ModernBeforeAfterSlider _slider = null!;
    private ModernMultilineTextBox _txtPrompt = null!;
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
    private ModernButtonControl _btnSendToSingleStudio = null!;
    private RadioButton _rbSourceLocal = null!;
    private RadioButton _rbSourceEtsy = null!;

    // Veri Modelleri
    private readonly List<BatchInputItem> _loadedItems = [];
    private readonly Dictionary<string, BatchItemResult> _results = [];
    private BatchInputItem? _selectedItem;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;

    public event Action<Bitmap>? OpenInSingleStudioRequested;

    public BatchStudioPanelControl(
        EtsyApiClient? apiClient = null,
        AiOptimizationSettings? aiSettings = null)
    {
        _apiClient = apiClient ?? new EtsyApiClient();
        _aiSettings = aiSettings ?? AiOptimizationSettingsStore.Load();
        _etsySettings = EtsyApiSettingsStore.Load();

        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        BuildLayout();
    }

    public void LoadInitialImage(string? initialImagePath, long? initialListingId = null)
    {
        if (!string.IsNullOrWhiteSpace(initialImagePath) && File.Exists(initialImagePath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(initialImagePath);
                var item = new BatchInputItem(
                    Id: $"init_{Guid.NewGuid():N}",
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
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Üst Bilgi / Aksiyon Barı
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Ana 3 Panelli Gövde
        Controls.Add(root);

        // 1. ÜST HEADER BAR
        root.Controls.Add(BuildTopBar(), 0, 0);

        // 2. ANA 3 PANELLİ GÖVDE
        var mainGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0)
        };
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360)); // Sol: Ayarlar & Prompt
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Orta: Before/After Slider & Alt Çubuk
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330)); // Sağ: Fotoğraf Listesi & Toplu Kuyruk
        root.Controls.Add(mainGrid, 0, 1);

        mainGrid.Controls.Add(BuildLeftPanel(), 0, 0);
        mainGrid.Controls.Add(BuildCenterPanel(), 1, 0);
        mainGrid.Controls.Add(BuildRightPanel(), 2, 0);
    }

    private Control BuildTopBar()
    {
        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var titleStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var lblTitle = new Label
        {
            AutoSize = true,
            Text = "⚡ Toplu AI Arka Plan Fabrikası",
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 10, 0)
        };
        titleStack.Controls.Add(lblTitle);

        var badge = new Label
        {
            AutoSize = true,
            Text = "GPT-Image-2.5 • Gemini • PhotoRoom",
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153), // Emerald 400
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 4, 0, 0)
        };
        titleStack.Controls.Add(badge);
        topBar.Controls.Add(titleStack, 0, 0);

        var rightStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
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
            Margin = new Padding(0, 2, 4, 0)
        };
        btnConfigureKeys.FlatAppearance.BorderSize = 0;
        btnConfigureKeys.Click += (_, _) => OpenKeyConfigDialog("openai");
        rightStack.Controls.Add(btnConfigureKeys);

        topBar.Controls.Add(rightStack, 1, 0);
        return topBar;
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

        var leftScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false
        };

        // 1. Motor Seçimi
        panel.Controls.Add(CreateSectionTitle("⚙️ 1. AI İşlem Motoru"));
        _cboEngine = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 330,
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
        var presetsScroll = new ModernScrollPanel
        {
            Width = 330,
            Height = 110,
            Margin = new Padding(0, 2, 0, 6)
        };
        var flowPresets = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false
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
        presetsScroll.SetContent(flowPresets);
        panel.Controls.Add(presetsScroll);

        // 3. Prompt Giriş Alanı
        panel.Controls.Add(CreateSectionTitle("✍️ 3. Özel Sahne & Arka Plan Promptu"));
        _txtPrompt = new ModernMultilineTextBox
        {
            Width = 330,
            Height = 85,
            Font = new Font("Segoe UI", 9F),
            Text = "Professional commercial product photography, placed on a smooth polished white marble countertop in a modern bright sunlit studio, soft natural contact shadow, depth of field, 8k crisp details"
        };
        _txtPrompt.TextChanged += (_, _) => UpdatePromptAnalysis();
        panel.Controls.Add(_txtPrompt);

        // Prompt Büyücü Butonu
        _btnMagicEnhance = new ModernButtonControl
        {
            Text = "🪄 AI ile Promptu Profesyonelleştir",
            Width = 330,
            Height = 32,
            NormalColor = Color.FromArgb(99, 102, 241),
            HoverColor = Color.FromArgb(129, 140, 248),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 8)
        };
        _btnMagicEnhance.Click += async (_, _) => await MagicEnhancePromptAsync();
        panel.Controls.Add(_btnMagicEnhance);

        // 4. Prompt Kalite Skoru & İpuçları
        panel.Controls.Add(CreateSectionTitle("📊 4. Prompt Kalite Skoru"));
        _lblPromptScore = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Bold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            Text = "Kalite Skoru: 95/100 (Mükemmel)",
            Margin = new Padding(0, 2, 0, 2)
        };
        panel.Controls.Add(_lblPromptScore);

        _lblPromptTip = new Label
        {
            Width = 330,
            Height = 44,
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = Color.FromArgb(148, 163, 184),
            Text = "İpucu: Malzeme ve ışık detayları harika. Bu prompt ile doğal gölgeler elde edeceksiniz."
        };
        panel.Controls.Add(_lblPromptTip);

        leftScroll.SetContent(panel);
        card.Controls.Add(leftScroll);
        return card;
    }

    private Control BuildCenterPanel()
    {
        var centerContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Margin = new Padding(0)
        };
        centerContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Slider
        centerContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Alt Aksiyon Çubuğu

        // Before/After Slider
        _slider = new ModernBeforeAfterSlider
        {
            Dock = DockStyle.Fill,
            BeforeLabel = "Orijinal Ürün",
            AfterLabel = "AI Yeni Arka Plan",
            SplitRatio = 0.5f
        };
        centerContainer.Controls.Add(_slider, 0, 0);

        // Alt Panel: İlerleme Çubuğu ve Aksiyon Butonları
        var bottomCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(30, 41, 59),
            BorderColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(12)
        };

        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottomCard.Controls.Add(bottomLayout);

        // İlerleme Bilgisi
        var progressRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        _lblProgress = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Hazır. Görsel seçip işlemi başlatın.",
            Font = new Font("Segoe UI", 8.8F),
            ForeColor = Color.FromArgb(203, 213, 225),
            TextAlign = ContentAlignment.MiddleLeft
        };
        progressRow.Controls.Add(_lblProgress, 0, 0);

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 18,
            Style = ProgressBarStyle.Continuous
        };
        progressRow.Controls.Add(_progressBar, 1, 0);
        bottomLayout.Controls.Add(progressRow, 0, 0);

        // Butonlar Sırası
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };

        _btnRunSingle = new ModernButtonControl
        {
            Text = "🎯 Seçiliyi İşle",
            Width = 145,
            Height = 36,
            NormalColor = Color.FromArgb(79, 70, 229), // Indigo 600
            HoverColor = Color.FromArgb(99, 102, 241),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnRunSingle.Click += async (_, _) => await RunSingleImageAsync();
        btnRow.Controls.Add(_btnRunSingle);

        _btnRunBatch = new ModernButtonControl
        {
            Text = "⚡ Tüm Kuyruğu İşle",
            Width = 175,
            Height = 36,
            NormalColor = Color.FromArgb(16, 185, 129), // Emerald 600
            HoverColor = Color.FromArgb(52, 211, 153),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnRunBatch.Click += async (_, _) => await RunBatchProcessAsync();
        btnRow.Controls.Add(_btnRunBatch);

        _btnExportFolder = new ModernButtonControl
        {
            Text = "💾 Klasöre İndir",
            Width = 140,
            Height = 36,
            NormalColor = Color.FromArgb(51, 65, 85),
            HoverColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.8F),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnExportFolder.Click += async (_, _) => await ExportResultsToFolderAsync();
        btnRow.Controls.Add(_btnExportFolder);

        _btnUploadEtsy = new ModernButtonControl
        {
            Text = "🚀 Etsy'ye Yükle",
            Width = 140,
            Height = 36,
            NormalColor = Color.FromArgb(217, 119, 6), // Amber 600
            HoverColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.8F),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnUploadEtsy.Click += async (_, _) => await UploadResultsToEtsyAsync();
        btnRow.Controls.Add(_btnUploadEtsy);

        _btnSendToSingleStudio = new ModernButtonControl
        {
            Text = "🎨 Tekli Stüdyoya Aktar",
            Width = 175,
            Height = 36,
            NormalColor = Color.FromArgb(124, 58, 237), // Violet
            HoverColor = Color.FromArgb(139, 92, 246),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 0)
        };
        _btnSendToSingleStudio.Click += (_, _) =>
        {
            var target = _slider.AfterImage ?? _slider.BeforeImage;
            if (target is Bitmap bmp)
            {
                OpenInSingleStudioRequested?.Invoke(bmp);
            }
            else
            {
                MessageBox.Show(this, "Lütfen önce kuyruktan bir görsel seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        btnRow.Controls.Add(_btnSendToSingleStudio);

        bottomLayout.Controls.Add(btnRow, 0, 1);
        centerContainer.Controls.Add(bottomCard, 0, 1);

        return centerContainer;
    }

    private Control BuildRightPanel()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(30, 41, 59),
            BorderColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(12)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Başlık
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Kaynak Seçimi
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Butonlar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Liste

        card.Controls.Add(root);

        // Başlık
        root.Controls.Add(CreateSectionTitle("📋 Fotoğraf Kuyruğu"), 0, 0);

        // Kaynak Radyo Butonları
        var sourceFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 2)
        };
        _rbSourceLocal = new RadioButton
        {
            Text = "Yerel Dosyalar",
            Checked = true,
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F),
            Margin = new Padding(0, 0, 10, 0)
        };
        _rbSourceEtsy = new RadioButton
        {
            Text = "Etsy Mağazam",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F)
        };
        sourceFlow.Controls.Add(_rbSourceLocal);
        sourceFlow.Controls.Add(_rbSourceEtsy);
        root.Controls.Add(sourceFlow, 0, 1);

        // İşlem Butonları (Yükle / Temizle)
        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 4)
        };

        var btnAddFiles = CreateSmallButton("➕ Dosya Seç", async () => await BrowseLocalFilesAsync());
        var btnAddFolder = CreateSmallButton("📁 Klasör Seç", async () => await BrowseLocalFolderAsync());
        var btnFetchEtsy = CreateSmallButton("🏬 Etsy'den Çek", async () => await FetchEtsyListingsAsync());
        var btnClear = CreateSmallButton("🗑️ Temizle", () => ClearQueue());

        actionFlow.Controls.Add(btnAddFiles);
        actionFlow.Controls.Add(btnAddFolder);
        actionFlow.Controls.Add(btnFetchEtsy);
        actionFlow.Controls.Add(btnClear);
        root.Controls.Add(actionFlow, 0, 2);

        // Kuyruk ListView
        _imageList = new ImageList
        {
            ImageSize = new Size(64, 64),
            ColorDepth = ColorDepth.Depth32Bit
        };

        _lvImages = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White,
            SmallImageList = _imageList,
            Font = new Font("Segoe UI", 8.8F),
            BorderStyle = BorderStyle.None
        };
        _lvImages.Columns.Add("Görsel", 75);
        _lvImages.Columns.Add("Başlık / Ürün", 155);
        _lvImages.Columns.Add("Durum", 85);

        _lvImages.SelectedIndexChanged += (_, _) => OnSelectedImageChanged();
        root.Controls.Add(_lvImages, 0, 3);

        return card;
    }

    #region Kuyruk ve Görsel Yükleme

    public void AddItemsToStudio(IReadOnlyList<BatchInputItem> items)
    {
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            if (_loadedItems.Any(x => x.Id == item.Id)) continue;
            _loadedItems.Add(item);

            try
            {
                using var ms = new MemoryStream(item.ImageBytes);
                using var orig = new Bitmap(ms);
                var thumb = new Bitmap(orig, new Size(64, 64));
                _imageList.Images.Add(item.Id, thumb);
            }
            catch { }

            var lvi = new ListViewItem(string.Empty, item.Id);
            lvi.SubItems.Add(item.Title);
            lvi.SubItems.Add("⏳ Bekliyor");
            lvi.Tag = item;
            _lvImages.Items.Add(lvi);
        }

        _lblProgress.Text = $"{_loadedItems.Count} adet görsel kuyrukta.";
        if (_lvImages.Items.Count > 0 && _lvImages.SelectedItems.Count == 0)
        {
            _lvImages.Items[0].Selected = true;
        }
    }

    private void ClearQueue()
    {
        if (_isProcessing) return;
        _loadedItems.Clear();
        _results.Clear();
        _lvImages.Items.Clear();
        _imageList.Images.Clear();
        _slider.BeforeImage = null;
        _slider.AfterImage = null;
        _lblProgress.Text = "Kuyruk temizlendi.";
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
            _lblProgress.Text = $"Toplu işlem tamamlandı: {successCount}/{batchResults.Count} başarılı.";
            MessageBox.Show(this, $"Toplu işlem tamamlandı!\nBaşarılı: {successCount}\nHatalı: {batchResults.Count - successCount}", "İşlem Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private void UpdateListItemStatus(string itemId, string status)
    {
        foreach (ListViewItem item in _lvImages.Items)
        {
            if (item.Tag is BatchInputItem bItem && bItem.Id == itemId)
            {
                item.SubItems[2].Text = status;
                break;
            }
        }
    }

    private void SetBusy(bool busy)
    {
        _isProcessing = busy;
        _btnRunSingle.Enabled = !busy;
        _btnRunBatch.Enabled = !busy;
        _btnExportFolder.Enabled = !busy;
        _btnUploadEtsy.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    #endregion

    #region Dışa Aktarma & Etsy Yükleme

    private async Task ExportResultsToFolderAsync()
    {
        var successful = _results.Values.Where(r => r.Success && r.EditedImage != null).ToList();
        if (successful.Count == 0)
        {
            MessageBox.Show(this, "Henüz başarıyla işlenmiş bir görsel sonucu bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var fbd = new FolderBrowserDialog
        {
            Description = "İşlenmiş görsellerin kaydedileceği hedef klasörü seçin"
        };

        if (fbd.ShowDialog(this) != DialogResult.OK) return;

        _lblProgress.Text = "Görseller dışa aktarılıyor...";
        int exportedCount = await BatchBackgroundChangeService.ExportResultsToFolderAsync(successful, fbd.SelectedPath);

        string msg = $"{exportedCount} görsel başarıyla kaydedildi:\n{fbd.SelectedPath}";
        MessageBox.Show(this, msg, "Dışa Aktarma Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);

        try
        {
            Process.Start(new ProcessStartInfo { FileName = fbd.SelectedPath, UseShellExecute = true });
        }
        catch { }
    }

    private async Task UploadResultsToEtsyAsync()
    {
        var itemsWithListing = _results.Values
            .Where(r => r.Success && r.EditedImage != null && r.TargetListingId.HasValue && r.TargetListingId > 0)
            .ToList();

        if (itemsWithListing.Count == 0)
        {
            MessageBox.Show(this, "Kuyrukta bir Etsy Listing ID'si ile eşleşmiş ve başarıyla işlenmiş görsel bulunamadı.\n(Görselleri 'Etsy'den Çek' ile eklediğinizden emin olun)", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    #endregion
}
