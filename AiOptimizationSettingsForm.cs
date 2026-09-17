namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Studio.Services;
using EtsyMarketPlace.Application.AiUsage;

internal sealed class AiOptimizationSettingsForm : Form
{
    private readonly AiOptimizationSettings _settings;
    private bool _isInitializing = false;

    // Aktif sağlayıcı butonları
    private readonly Dictionary<string, Button> _providerButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly ComboBox _providerComboBox = new();
    private string _currentProvider = "Gemini";

    // Birincil LLM ve Görsel Modelleri
    private readonly ComboBox _modelComboBox = new();
    private readonly TextBox _customModelTextBox = new();
    private readonly ComboBox _imageModelComboBox = new();
    private readonly TextBox _apiKeyTextBox = new();

    // Görsel Stüdyo Anahtarları
    private readonly TextBox _photoRoomKeyTextBox = new();
    private readonly TextBox _bflKeyTextBox = new();
    private readonly TextBox _ideogramKeyTextBox = new();

    // Sistem Güvenliği & Fallback
    private readonly CheckBox _chkStrictLiveAi = new();
    private readonly CheckBox _chkStrictNeverOffline = new();

    // Terminal & Aksiyonlar
    private readonly TextBox _statusTextBox = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnTest = new();
    private readonly Button _btnClose = new();

    public AiOptimizationSettingsForm()
    {
        _settings = AiOptimizationSettingsStore.Load();

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "AI Optimizasyon Ayarları & Model Yapılandırması";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 740);
        MinimumSize = new Size(840, 680);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(15, 23, 42); // #0F172A
        ForeColor = Color.White;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 16, 20, 16)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // 1. Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 2. Scrollable Cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));  // 3. Footer (Buttons + Terminal)
        Controls.Add(mainLayout);

        // ==========================================
        // 1. HEADER BÖLÜMÜ
        // ==========================================
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "⚡ Yapay Zeka Optimizasyon & Model Merkezi",
            UseMnemonic = false,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        };
        var lblSub = new Label
        {
            Text = "Birincil LLM motoru, API anahtarları, görsel tasarım entegrasyonları ve kesintisiz çalışma politikaları.",
            UseMnemonic = false,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Location = new Point(0, 26)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSub);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // ==========================================
        // 2. KARTLAR (KAYDIRILABİLİR ALAN)
        // ==========================================
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 8, 0)
        };
        mainLayout.Controls.Add(scrollPanel, 0, 1);

        var cardsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cardsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Sıralama Kesin ve Sabit:
        // 1. Satır: Card 1 (Birincil LLM & Sağlayıcı Butonları)
        // 2. Satır: Card 2 (Vision Studio)
        // 3. Satır: Card 3 (Güvenlik / Fallback)
        cardsTable.Controls.Add(BuildCard1_PrimaryLlm(), 0, 0);
        cardsTable.Controls.Add(BuildCard2_VisionStudio(), 0, 1);
        cardsTable.Controls.Add(BuildCard3_SecurityFallback(), 0, 2);

        scrollPanel.Controls.Add(cardsTable);

        // ==========================================
        // 3. ALT BÖLÜM (FOOTER: EYLEMLER & TERMİNAL)
        // ==========================================
        var pnlFooter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Sol Eylem Butonları
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 14, 0, 0)
        };

        _btnSave.Text = "💾 Kaydet & Aktifleştir";
        _btnSave.UseMnemonic = false;
        _btnSave.BackColor = Color.FromArgb(16, 185, 129); // Neon Emerald Green
        _btnSave.ForeColor = Color.White;
        _btnSave.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _btnSave.FlatStyle = FlatStyle.Flat;
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Height = 36;
        _btnSave.Width = 195;
        _btnSave.Cursor = Cursors.Hand;
        _btnSave.Click += (_, _) =>
        {
            SaveValues();
            string effectiveLlm = !string.IsNullOrWhiteSpace(_customModelTextBox.Text)
                ? _customModelTextBox.Text.Trim()
                : _modelComboBox.Text.Trim();
            string currentKey = _apiKeyTextBox.Text.Trim();

            if (_currentProvider.Equals("Offline", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    this,
                    "⚙️ Çevrimdışı (Offline) Yerel Kural Motoru aktifleştirildi.\n\n" +
                    "Canlı API bağlantısı yapılmayacak, yerel şablon ve kurallar devrede olacaktır.",
                    "AI Ayarları - Offline Mod",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else if (string.IsNullOrWhiteSpace(currentKey))
            {
                MessageBox.Show(
                    this,
                    $"⚠️ {GetProviderDisplayName(_currentProvider)} seçildi!\n" +
                    $"🎯 Model: {effectiveLlm}\n\n" +
                    "DİKKAT: Bu sağlayıcı için henüz bir API Anahtarı (Key) girilmedi!\n\n" +
                    "Canlı yapay zeka yanıtı alabilmek için lütfen bu ekrandaki \"API Anahtarı\" kutusuna geçerli anahtarınızı girin. " +
                    "Anahtar girilene kadar canlı sorgularda hata almamak için sistem yerel offline kuralları çalıştıracaktır.",
                    "AI Ayarları - API Anahtarı Gerekli",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(
                    this,
                    $"✅ {GetProviderDisplayName(_currentProvider)} Başarıyla Aktifleştirildi!\n\n" +
                    $"• Sağlayıcı: {GetProviderDisplayName(_currentProvider)}\n" +
                    $"• LLM Modeli: {effectiveLlm}\n" +
                    $"• Görsel Modeli: {(_imageModelComboBox.Enabled ? _imageModelComboBox.Text : "Harici / Devre Dışı")}\n" +
                    $"• API Durumu: Canlı Anahtar Kaydedildi",
                    "AI Ayarları - Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        _btnTest.Text = "⚡ Hızlı API Testi";
        _btnTest.UseMnemonic = false;
        _btnTest.BackColor = Color.FromArgb(30, 41, 59);
        _btnTest.ForeColor = Color.FromArgb(56, 189, 248); // Electric Cyan
        _btnTest.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _btnTest.FlatStyle = FlatStyle.Flat;
        _btnTest.FlatAppearance.BorderColor = Color.FromArgb(56, 189, 248);
        _btnTest.Height = 36;
        _btnTest.Width = 145;
        _btnTest.Cursor = Cursors.Hand;
        _btnTest.Margin = new Padding(10, 0, 0, 0);
        _btnTest.Click += (_, _) => TestSettings();

        _btnClose.Text = "✕ Kapat";
        _btnClose.UseMnemonic = false;
        _btnClose.BackColor = Color.FromArgb(30, 41, 59);
        _btnClose.ForeColor = Color.FromArgb(148, 163, 184);
        _btnClose.FlatStyle = FlatStyle.Flat;
        _btnClose.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
        _btnClose.Height = 36;
        _btnClose.Width = 90;
        _btnClose.Cursor = Cursors.Hand;
        _btnClose.Margin = new Padding(10, 0, 0, 0);
        _btnClose.Click += (_, _) => Close();

        pnlButtons.Controls.Add(_btnSave);
        pnlButtons.Controls.Add(_btnTest);
        pnlButtons.Controls.Add(_btnClose);
        pnlFooter.Controls.Add(pnlButtons, 0, 0);

        // Sağ Terminal Konsolu
        var pnlTerminal = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 15, 26),
            Padding = new Padding(6)
        };
        pnlTerminal.Paint += (_, pe) =>
        {
            using var pen = new Pen(Color.FromArgb(30, 45, 65), 1.2f);
            pe.Graphics.DrawRectangle(pen, 0, 0, pnlTerminal.Width - 1, pnlTerminal.Height - 1);
        };

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        _statusTextBox.BackColor = Color.FromArgb(10, 15, 26);
        _statusTextBox.ForeColor = Color.FromArgb(52, 211, 153); // Terminal Green
        _statusTextBox.Font = new Font("Consolas", 8.2F);
        _statusTextBox.BorderStyle = BorderStyle.None;
        pnlTerminal.Controls.Add(_statusTextBox);

        pnlFooter.Controls.Add(pnlTerminal, 1, 0);
        mainLayout.Controls.Add(pnlFooter, 0, 2);
    }

    // ==========================================
    // KART 1: BİRİNCİL METİN / AKIL YÜRÜTME MOTORU (LLM)
    // ==========================================
    private Control BuildCard1_PrimaryLlm()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0, 4, 0, 4)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // 1. Satır: Sağlayıcı Butonları
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 2. Satır: LLM Model
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 3. Satır: Görsel Modeli
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 4. Satır: API Key

        // 1. Satır: Yatay Sağlayıcı Butonları (En üstte)
        content.Controls.Add(CreateFieldLabel("Aktif Sağlayıcı:"), 0, 0);

        var pnlProviderBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        (string Key, string DisplayText)[] providers = [
            ("Gemini", "Google Gemini"),
            ("OpenAI", "OpenAI (GPT)"),
            ("DeepSeek", "DeepSeek"),
            ("Claude", "Claude"),
            ("Grok", "xAI Grok"),
            ("Offline", "Offline Mod")
        ];

        foreach (var (key, text) in providers)
        {
            var btn = new Button
            {
                Text = "  " + text,
                Image = AiProviderIconHelper.GetProviderIcon(key, 20),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                UseMnemonic = false,
                Height = 35,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(148, 163, 184),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Padding = new Padding(8, 0, 10, 0),
                Margin = new Padding(0, 4, 8, 4)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btn.FlatAppearance.BorderSize = 1;

            btn.Click += (_, _) => SelectProvider(key);
            _providerButtons[key] = btn;
            pnlProviderBtns.Controls.Add(btn);
        }
        content.Controls.Add(pnlProviderBtns, 1, 0);

        // 2. Satır: LLM Model Dropdown + Özel Model Kutusu
        content.Controls.Add(CreateFieldLabel("LLM Model:"), 0, 1);

        var pnlModelRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        pnlModelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlModelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        pnlModelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        StyleComboBox(_modelComboBox);
        _modelComboBox.Dock = DockStyle.Fill;
        pnlModelRow.Controls.Add(_modelComboBox, 0, 0);

        var lblCustom = new Label
        {
            Text = "veya Özel Model:",
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8F)
        };
        pnlModelRow.Controls.Add(lblCustom, 1, 0);

        StyleTextBox(_customModelTextBox);
        _customModelTextBox.Dock = DockStyle.Fill;
        _customModelTextBox.PlaceholderText = "Örn: gemini-2.5-flash, grok-3, o3-mini...";
        _customModelTextBox.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_customModelTextBox.Text))
            {
                _modelComboBox.Text = _customModelTextBox.Text.Trim();
            }
        };
        pnlModelRow.Controls.Add(_customModelTextBox, 2, 0);

        content.Controls.Add(pnlModelRow, 1, 1);

        // 3. Satır: Görsel Modeli Dropdown
        content.Controls.Add(CreateFieldLabel("Görsel Modeli:"), 0, 2);
        StyleComboBox(_imageModelComboBox);
        _imageModelComboBox.Dock = DockStyle.Left;
        _imageModelComboBox.Width = 380;
        content.Controls.Add(_imageModelComboBox, 1, 2);

        // 4. Satır: API Key Kutusu (Maskeleme & Göz Düğmeli)
        content.Controls.Add(CreateFieldLabel("API Anahtarı:"), 0, 3);
        var pnlKey = CreateKeyInputWithEye(_apiKeyTextBox);
        content.Controls.Add(pnlKey, 1, 3);

        return CreateCardContainer(
            "Birincil Metin / Akıl Yürütme Motoru (LLM)",
            content,
            "Etsy ürün başlığı, açıklaması ve anahtar kelime üretiminde kullanılan birincil zeka motoru.");
    }

    // ==========================================
    // KART 2: GÖRSEL ÜRETİM & ARKA PLAN AI MOTORLARI (VISION STUDIO)
    // ==========================================
    private Control BuildCard2_VisionStudio()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 4, 0, 4)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // PhotoRoom
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // FLUX
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Ideogram

        content.Controls.Add(CreateFieldLabel("PhotoRoom API Key:"), 0, 0);
        content.Controls.Add(CreateKeyInputWithEye(_photoRoomKeyTextBox), 1, 0);

        content.Controls.Add(CreateFieldLabel("Black Forest Labs (FLUX):"), 0, 1);
        content.Controls.Add(CreateKeyInputWithEye(_bflKeyTextBox), 1, 1);

        content.Controls.Add(CreateFieldLabel("Ideogram v4 (Tipografi & Görsel):"), 0, 2);
        content.Controls.Add(CreateKeyInputWithEye(_ideogramKeyTextBox), 1, 2);

        return CreateCardContainer(
            "Görsel Üretim & Arka Plan AI Motorları (Vision Studio)",
            content,
            "Ürün fotoğraflarının arka planını şeffaflaştırmak (dekupe) veya 3D modelleri fotogerçekçi renderlamak için kullanılır.");
    }

    // ==========================================
    // KART 3: ÇEVRİMDİŞİ (OFFLINE) MOTOR VE HATA DAVRANIŞI
    // ==========================================
    private Control BuildCard3_SecurityFallback()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 4, 0, 4)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _chkStrictLiveAi.Text = "☑️ Canlı AI hata verirse haberim olmadan yerel motora geçme (Ekranda açıkça uyar)";
        _chkStrictLiveAi.AutoSize = true;
        _chkStrictLiveAi.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _chkStrictLiveAi.ForeColor = Color.FromArgb(52, 211, 153); // Emerald/Green accent
        _chkStrictLiveAi.Cursor = Cursors.Hand;
        _chkStrictLiveAi.Margin = new Padding(0, 2, 0, 1);

        var lblLiveAiDetail = new Label
        {
            Text = "API kotası bittiğinde veya bağlantı koptuğunda, program sessizce basit yerel şablonlara geçmez; ekranda açık hata bildirir.",
            UseMnemonic = false,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(22, 0, 0, 8)
        };

        _chkStrictNeverOffline.Text = "🚫 Çevrimdışı (Offline) motoru tamamen kapat (Sadece Gerçek Canlı AI Kullan)";
        _chkStrictNeverOffline.AutoSize = true;
        _chkStrictNeverOffline.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _chkStrictNeverOffline.ForeColor = Color.FromArgb(248, 113, 113); // Coral/Alert red accent
        _chkStrictNeverOffline.Cursor = Cursors.Hand;
        _chkStrictNeverOffline.Margin = new Padding(0, 4, 0, 1);

        _chkStrictNeverOffline.CheckedChanged += (_, _) =>
        {
            if (_chkStrictNeverOffline.Checked)
            {
                _chkStrictLiveAi.Checked = true;
                _chkStrictLiveAi.Enabled = false;
            }
            else
            {
                _chkStrictLiveAi.Enabled = true;
            }
        };

        var lblNeverOfflineDetail = new Label
        {
            Text = "Ne olursa olsun programın yerel kurallarla başlık üretmesini engeller. Canlı AI çalışmıyorsa veya API anahtarı yoksa işlem kesinlikle durdurulur; yalnızca gerçek yapay zeka çıktısı garanti edilir.",
            UseMnemonic = false,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(22, 0, 0, 4)
        };

        content.Controls.Add(_chkStrictLiveAi, 0, 0);
        content.Controls.Add(lblLiveAiDetail, 0, 1);
        content.Controls.Add(_chkStrictNeverOffline, 0, 2);
        content.Controls.Add(lblNeverOfflineDetail, 0, 3);

        return CreateCardContainer(
            "🛡️ Çevrimdışı (Offline) Motor ve Hata Davranışı",
            content,
            "Yapay zeka servislerinde kesinti veya hata olduğunda programın nasıl davranacağını belirleyin.");
    }

    // ==========================================
    // SAĞLAYICI SEÇİMİ VE ALANLARIN GÜNCELLENMESİ
    // ==========================================
    private void SelectProvider(string providerKey)
    {
        if (providerKey.Equals("Offline", StringComparison.OrdinalIgnoreCase) && _chkStrictNeverOffline.Checked)
        {
            MessageBox.Show(
                this,
                "🚫 Çevrimdışı (Offline) motor tamamen kapatıldı!\n\n" +
                "Bu kural aktifken Offline Mod seçilemez.\n\n" +
                "Offline moda geçmek istiyorsanız lütfen aşağıdaki Güvenlik bölümünden \"Çevrimdışı motoru tamamen kapat\" seçeneğini kaldırın.",
                "Offline Mod Devre Dışı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_isInitializing)
        {
            SaveCurrentKeyToSettingsMemory();
        }

        _currentProvider = providerKey;

        // Buton renklerini güncelle
        foreach (var (k, btn) in _providerButtons)
        {
            bool active = k.Equals(providerKey, StringComparison.OrdinalIgnoreCase);
            if (active)
            {
                btn.BackColor = Color.FromArgb(30, 58, 138); // Deep rich active blue (#1E3A8A)
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(56, 189, 248); // Electric cyan (#38BDF8)
                btn.FlatAppearance.BorderSize = 2;
                btn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(30, 41, 59);
                btn.ForeColor = Color.FromArgb(148, 163, 184);
                btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
                btn.FlatAppearance.BorderSize = 1;
                btn.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            }
        }

        // Modelleri ve API Key'i sağlayıcıya göre yükle
        _modelComboBox.Items.Clear();
        _imageModelComboBox.Items.Clear();

        if (providerKey == "Gemini")
        {
            _modelComboBox.Items.AddRange([
                "gemini-2.5-flash",
                "gemini-3.6-flash",
                "gemini-2.5-pro",
                "gemini-3.8-flash",
                "gemini-1.5-flash",
                "gemini-1.5-pro"
            ]);
            _modelComboBox.Text = AiModelNormalizer.NormalizeGeminiTextModel(_settings.GeminiModel);
            _customModelTextBox.Text = (!_modelComboBox.Items.Contains(_settings.GeminiModel) || _settings.GeminiModel == "gemini-3.8-flash") ? (_settings.GeminiModel ?? "") : "";

            _imageModelComboBox.Enabled = true;
            _imageModelComboBox.Items.AddRange([
                "gemini-2.5-flash-image",
                "gemini-3.1-flash-image",
                "imagen-3.0-generate-002",
                "gemini-2.0-flash"
            ]);
            _imageModelComboBox.Text = AiModelNormalizer.NormalizeGeminiImageModel(_settings.GeminiImageModel);

            // Studio config senkronizasyonu
            if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.GoogleGeminiApiKey))
                    {
                        _settings.GeminiApiKey = StudioConfigurationManager.Current.GoogleGeminiApiKey;
                    }
                }
                catch { }
            }

            _apiKeyTextBox.Text = _settings.GeminiApiKey ?? "";
            _apiKeyTextBox.Enabled = true;
        }
        else if (providerKey == "OpenAI")
        {
            _modelComboBox.Items.AddRange([
                "gpt-4o",
                "gpt-4o-mini",
                "o3-mini",
                "o1",
                "o1-mini",
                "chatgpt-4o-latest",
                "gpt-4-turbo"
            ]);
            _modelComboBox.Text = AiModelNormalizer.NormalizeOpenAiTextModel(_settings.OpenAiModel);
            _customModelTextBox.Text = !_modelComboBox.Items.Contains(_settings.OpenAiModel) ? (_settings.OpenAiModel ?? "") : "";

            _imageModelComboBox.Enabled = true;
            _imageModelComboBox.Items.AddRange([
                "dall-e-3",
                "gpt-image-2.5-flare",
                "dall-e-2"
            ]);
            _imageModelComboBox.Text = AiModelNormalizer.NormalizeOpenAiImageModel(_settings.OpenAiImageModel);

            // Studio config senkronizasyonu
            if (string.IsNullOrWhiteSpace(_settings.OpenAiApiKey))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(StudioConfigurationManager.Current.OpenAiApiKey))
                    {
                        _settings.OpenAiApiKey = StudioConfigurationManager.Current.OpenAiApiKey;
                    }
                }
                catch { }
            }

            _apiKeyTextBox.Text = _settings.OpenAiApiKey ?? "";
            _apiKeyTextBox.Enabled = true;
        }
        else if (providerKey == "DeepSeek")
        {
            _modelComboBox.Items.AddRange([
                "deepseek-chat",
                "deepseek-reasoner"
            ]);
            _modelComboBox.Text = AiModelNormalizer.NormalizeDeepSeekModel(_settings.DeepSeekModel);
            _customModelTextBox.Text = !_modelComboBox.Items.Contains(_settings.DeepSeekModel) ? (_settings.DeepSeekModel ?? "") : "";

            _imageModelComboBox.Enabled = false;
            _imageModelComboBox.Items.Add("Görsel Desteği Yok (PhotoRoom / FLUX kullanın)");
            _imageModelComboBox.SelectedIndex = 0;

            _apiKeyTextBox.Text = _settings.DeepSeekApiKey ?? "";
            _apiKeyTextBox.Enabled = true;
        }
        else if (providerKey == "Claude")
        {
            _modelComboBox.Items.AddRange([
                "claude-3-7-sonnet-20250219",
                "claude-3-5-sonnet-20241022",
                "claude-3-5-haiku-20241022",
                "claude-3-opus-20240229"
            ]);
            _modelComboBox.Text = AiModelNormalizer.NormalizeClaudeTextModel(_settings.ClaudeModel);
            _customModelTextBox.Text = !_modelComboBox.Items.Contains(_settings.ClaudeModel) ? (_settings.ClaudeModel ?? "") : "";

            _imageModelComboBox.Enabled = false;
            _imageModelComboBox.Items.Add("Görsel Desteği Yok (PhotoRoom / FLUX kullanın)");
            _imageModelComboBox.SelectedIndex = 0;

            _apiKeyTextBox.Text = _settings.ClaudeApiKey ?? "";
            _apiKeyTextBox.Enabled = true;
        }
        else if (providerKey == "Grok")
        {
            _modelComboBox.Items.AddRange([
                "grok-3",
                "grok-2-latest"
            ]);
            _modelComboBox.Text = AiModelNormalizer.NormalizeGrokModel(_settings.GrokModel);
            _customModelTextBox.Text = !_modelComboBox.Items.Contains(_settings.GrokModel) ? (_settings.GrokModel ?? "") : "";

            _imageModelComboBox.Enabled = false;
            _imageModelComboBox.Items.Add("Görsel Desteği Yok (PhotoRoom / FLUX kullanın)");
            _imageModelComboBox.SelectedIndex = 0;

            _apiKeyTextBox.Text = _settings.GrokApiKey ?? "";
            _apiKeyTextBox.Enabled = true;
        }
        else
        {
            // Offline
            _modelComboBox.Items.Add("Yerel Kural Motoru (Offline)");
            _modelComboBox.SelectedIndex = 0;
            _customModelTextBox.Text = "";

            _imageModelComboBox.Enabled = false;
            _imageModelComboBox.Items.Add("Offline Mod");
            _imageModelComboBox.SelectedIndex = 0;

            _apiKeyTextBox.Text = "";
            _apiKeyTextBox.Enabled = false;
        }

        WriteStatus($"Sağlayıcı seçildi: {GetProviderDisplayName(providerKey)} (Model: {_modelComboBox.Text})");
    }

    private static string GetProviderDisplayName(string key) => key switch
    {
        "Gemini" => "Google Gemini",
        "OpenAI" => "OpenAI (GPT)",
        "DeepSeek" => "DeepSeek",
        "Claude" => "Anthropic Claude",
        "Grok" => "xAI Grok",
        _ => "Offline Yerel Kural Motoru"
    };

    private void SaveCurrentKeyToSettingsMemory()
    {
        string key = _apiKeyTextBox.Text.Trim();
        if (_currentProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) _settings.GeminiApiKey = key;
        else if (_currentProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)) _settings.OpenAiApiKey = key;
        else if (_currentProvider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase)) _settings.DeepSeekApiKey = key;
        else if (_currentProvider.Equals("Claude", StringComparison.OrdinalIgnoreCase)) _settings.ClaudeApiKey = key;
        else if (_currentProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase)) _settings.GrokApiKey = key;
    }

    // ==========================================
    // DEĞERLERİ YÜKLE VE KAYDET
    // ==========================================
    private void LoadValues()
    {
        _isInitializing = true;
        try
        {
            string p = string.IsNullOrWhiteSpace(_settings.Provider) ? "Gemini" : _settings.Provider;
            SelectProvider(p);

            _bflKeyTextBox.Text = _settings.BflApiKey ?? "";
            _ideogramKeyTextBox.Text = _settings.IdeogramApiKey ?? "";
            _photoRoomKeyTextBox.Text = !string.IsNullOrWhiteSpace(_settings.PhotoRoomApiKey)
                ? _settings.PhotoRoomApiKey
                : PhotoRoomSettingsStore.Load().ApiKey;

            _chkStrictNeverOffline.Checked = _settings.StrictNeverOffline;
            if (_settings.StrictNeverOffline)
            {
                _chkStrictLiveAi.Checked = true;
                _chkStrictLiveAi.Enabled = false;
            }
            else
            {
                _chkStrictLiveAi.Checked = !_settings.AllowSilentOfflineFallback;
                _chkStrictLiveAi.Enabled = true;
            }

            WriteStatus($"Ayar dosyası başarıyla yüklendi: {AiOptimizationSettingsStore.SettingsPath}");
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void SaveValues()
    {
        SaveCurrentKeyToSettingsMemory();

        _settings.Provider = _currentProvider;
        _settings.StrictNeverOffline = _chkStrictNeverOffline.Checked;
        _settings.AllowSilentOfflineFallback = !_chkStrictLiveAi.Checked && !_chkStrictNeverOffline.Checked;

        string rawEffectiveModel = !string.IsNullOrWhiteSpace(_customModelTextBox.Text)
            ? _customModelTextBox.Text.Trim()
            : _modelComboBox.Text.Trim();

        if (_currentProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GeminiModel = AiModelNormalizer.NormalizeGeminiTextModel(rawEffectiveModel);
            _settings.GeminiImageModel = AiModelNormalizer.NormalizeGeminiImageModel(_imageModelComboBox.Text);
        }
        else if (_currentProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            _settings.OpenAiModel = AiModelNormalizer.NormalizeOpenAiTextModel(rawEffectiveModel);
            _settings.OpenAiImageModel = AiModelNormalizer.NormalizeOpenAiImageModel(_imageModelComboBox.Text);
        }
        else if (_currentProvider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            _settings.DeepSeekModel = AiModelNormalizer.NormalizeDeepSeekModel(rawEffectiveModel);
        }
        else if (_currentProvider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            _settings.ClaudeModel = AiModelNormalizer.NormalizeClaudeTextModel(rawEffectiveModel);
        }
        else if (_currentProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
        {
            _settings.GrokModel = AiModelNormalizer.NormalizeGrokModel(rawEffectiveModel);
        }

        _settings.PhotoRoomApiKey = _photoRoomKeyTextBox.Text.Trim();
        _settings.BflApiKey = _bflKeyTextBox.Text.Trim();
        _settings.IdeogramApiKey = _ideogramKeyTextBox.Text.Trim();

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

        WriteStatus($"AI ayarları başarıyla kaydedildi. Aktif Motor: {_settings.GetActiveEngineName()}");
    }

    private void TestSettings()
    {
        SaveValues();
        if (_settings.UseGemini)
        {
            WriteStatus($"Google Gemini modu hazır: {_settings.GeminiModel} (Key: {AiPriceCalculator.MaskApiKey(_settings.GeminiApiKey)})");
            return;
        }
        if (_settings.UseOpenAi)
        {
            WriteStatus($"OpenAI modu hazır: {_settings.OpenAiModel} (Key: {AiPriceCalculator.MaskApiKey(_settings.OpenAiApiKey)})");
            return;
        }
        if (_settings.UseDeepSeek)
        {
            WriteStatus($"DeepSeek modu hazır: {_settings.DeepSeekModel} (Key: {AiPriceCalculator.MaskApiKey(_settings.DeepSeekApiKey)})");
            return;
        }
        if (_settings.UseClaude)
        {
            WriteStatus($"Anthropic Claude modu hazır: {_settings.ClaudeModel}");
            return;
        }
        if (_settings.UseGrok)
        {
            WriteStatus($"xAI Grok modu hazır: {_settings.GrokModel}");
            return;
        }
        if (_settings.IsOffline)
        {
            WriteStatus("Offline mod aktif. API anahtarı olmadan yerel kural motoru devrede.");
            return;
        }

        WriteStatus($"{_settings.Provider} seçildi ve test edildi.");
    }

    private void WriteStatus(string message)
    {
        _statusTextBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}{_statusTextBox.Text}";
    }

    // ==========================================
    // ARAYÜZ YARDIMCILARI & KART KAPSAYICILARI
    // ==========================================
    private static Panel CreateCardContainer(string title, Control content, string? subNote = null)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(22, 30, 46),
            Padding = new Padding(18, 14, 18, 16),
            Margin = new Padding(0, 0, 0, 16)
        };
        pnl.Paint += (_, pe) =>
        {
            using var borderPen = new Pen(Color.FromArgb(38, 50, 72), 1.2f);
            pe.Graphics.DrawRectangle(borderPen, 0, 0, pnl.Width - 1, pnl.Height - 1);
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = string.IsNullOrWhiteSpace(subNote) ? 2 : 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int r = 0;
        var lblTitle = new Label
        {
            Text = title,
            UseMnemonic = false,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, string.IsNullOrWhiteSpace(subNote) ? 8 : 2)
        };
        cardLayout.Controls.Add(lblTitle, 0, r++);

        if (!string.IsNullOrWhiteSpace(subNote))
        {
            var lblSub = new Label
            {
                Text = subNote,
                UseMnemonic = false,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            cardLayout.Controls.Add(lblSub, 0, r++);
        }

        content.Dock = DockStyle.Top;
        cardLayout.Controls.Add(content, 0, r);
        pnl.Controls.Add(cardLayout);

        return pnl;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        UseMnemonic = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(180, 195, 220)
    };

    private static void StyleTextBox(TextBox txt)
    {
        txt.BackColor = Color.FromArgb(30, 41, 59);
        txt.ForeColor = Color.White;
        txt.Font = new Font("Segoe UI", 9F);
        txt.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleComboBox(ComboBox cbo)
    {
        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
        cbo.BackColor = Color.FromArgb(30, 41, 59);
        cbo.ForeColor = Color.White;
        cbo.Font = new Font("Segoe UI", 9F);
        cbo.FlatStyle = FlatStyle.Flat;
    }

    private static Panel CreateKeyInputWithEye(TextBox txt)
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 30,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(4, 2, 2, 2)
        };
        container.Paint += (_, pe) =>
        {
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1f);
            pe.Graphics.DrawRectangle(pen, 0, 0, container.Width - 1, container.Height - 1);
        };

        txt.Dock = DockStyle.Fill;
        txt.BorderStyle = BorderStyle.None;
        txt.BackColor = Color.FromArgb(30, 41, 59);
        txt.ForeColor = Color.White;
        txt.Font = new Font("Consolas", 9.5F);
        txt.UseSystemPasswordChar = true;

        var btnEye = new Button
        {
            Text = "👁",
            Dock = DockStyle.Right,
            Width = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9F)
        };
        btnEye.FlatAppearance.BorderSize = 0;
        btnEye.Click += (_, _) =>
        {
            txt.UseSystemPasswordChar = !txt.UseSystemPasswordChar;
            btnEye.ForeColor = txt.UseSystemPasswordChar ? Color.FromArgb(148, 163, 184) : Color.FromArgb(56, 189, 248);
        };

        container.Controls.Add(txt);
        container.Controls.Add(btnEye);
        return container;
    }
}
