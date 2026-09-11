namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;
using SimilarProductsWinForms.Services;

internal sealed class ShopVaultMigrationControl : UserControl
{
    private readonly IEtsyVaultApiClient _apiClient;
    private readonly IAntiBanSanitizer _antiBanSanitizer;
    private readonly ShopMigrationDeploymentService _deploymentService;

    private IReadOnlyList<VaultListing> _queuedListings = [];
    private List<VaultListing> _failedListings = [];
    private CancellationTokenSource? _cts;

    // Target Shop Verification UI
    private readonly Label _lblShopStatus = new()
    {
        AutoSize = true,
        Text = "⏳ Hedef mağaza API bağlantısı test ediliyor...",
        ForeColor = UiStyle.WarningColor,
        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
        Margin = new Padding(8, 6, 12, 0)
    };

    private readonly ModernButtonControl _btnVerifyShop = new()
    {
        Text = "🔍 Bağlantıyı Doğrula",
        Width = 150,
        Height = 34,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnOpenApiSettings = new()
    {
        Text = "⚙️ API Ayarlarını Aç",
        Width = 150,
        Height = 34,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private long _targetShopId = 0;
    private string _targetShopName = "";

    // Security & Anti-ban Presets
    private readonly ModernButtonControl _btnPresetHigh = new()
    {
        Text = "🛡️ Maksimum Güvenlik (Önerilen)",
        Width = 240,
        Height = 36,
        BackColor = UiStyle.PrimaryColor,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnPresetStandard = new()
    {
        Text = "⚡ Hızlı Standart",
        Width = 150,
        Height = 36,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnPresetClone = new()
    {
        Text = "📋 Ayna Klon (Anti-Bansız)",
        Width = 200,
        Height = 36,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly CheckBox _chkStripExif = new() { Text = "EXIF, GPS & Kamera Seri Numaralarını Temizle", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI", 9F) };
    private readonly CheckBox _chkPermutateImageHash = new() { Text = "pHash Parmak İzi Kırıcı (1-2px Micro-crop & Yeniden Kodlama)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI", 9F) };
    private readonly CheckBox _chkAiRewriteTitle = new() { Text = "Yapay Zeka ile Başlığı Özgünleştir (AI Title Rewrite)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI", 9F) };
    private readonly CheckBox _chkAiRewriteDesc = new() { Text = "Yapay Zeka ile Açıklamayı Özgünleştir (AI Desc Rewrite)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI", 9F) };
    private readonly CheckBox _chkDraftFirst = new() { Text = "Güvenli Taslak Olarak Yükle (Draft Mode)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI", 9F) };
    private readonly NumericUpDown _numThrottle = new() { Minimum = 1, Maximum = 30, Value = 4, Width = 70, Font = new Font("Segoe UI", 9F) };
    private readonly NumericUpDown _numPriceAdj = new() { Minimum = -50, Maximum = 100, Value = 0, Width = 70, Font = new Font("Segoe UI", 9F) };
    private readonly TextBox _txtSkuPrefix = new() { Text = "NEW_", Width = 90, Font = new Font("Segoe UI", 9F) };

    // Mapping inputs
    private readonly TextBox _txtDefaultShippingId = new() { Width = 150, PlaceholderText = "Örn: 123456789", Font = new Font("Segoe UI", 9F) };
    private readonly TextBox _txtDefaultReturnId = new() { Width = 150, PlaceholderText = "Örn: 987654321", Font = new Font("Segoe UI", 9F) };

    // Deployment controls
    private readonly Label _lblQueueSummary = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
        ForeColor = UiStyle.AccentColor,
        Text = "Kuyrukta: 0 ürün seçili"
    };

    private readonly ModernButtonControl _btnStart = new()
    {
        Text = "🚀 1-TIKLA GÜVENLİ GÖÇÜ BAŞLAT",
        Height = 46,
        Width = 330,
        BackColor = UiStyle.PrimaryColor,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnCancel = new()
    {
        Text = "⏹️ Durdur",
        Height = 46,
        Width = 110,
        BackColor = UiStyle.DangerColor,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Enabled = false,
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnRetryFailed = new()
    {
        Text = "🔄 Başarısızları Yeniden Dene",
        Height = 46,
        Width = 230,
        BackColor = UiStyle.WarningColor,
        ForeColor = Color.Black,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Visible = false,
        Cursor = Cursors.Hand
    };

    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Fill, Height = 22, Minimum = 0, Maximum = 100, Value = 0 };
    private readonly Label _lblProgressStatus = new() { AutoSize = true, ForeColor = UiStyle.TextDark, Font = UiStyle.BaseFont, Text = "Hazır." };
    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(15, 23, 42),
        ForeColor = Color.FromArgb(226, 232, 240),
        Font = new Font("Consolas", 9.2F)
    };

    public ShopVaultMigrationControl(
        IEtsyVaultApiClient apiClient,
        IAntiBanSanitizer antiBanSanitizer)
    {
        _apiClient = apiClient;
        _antiBanSanitizer = antiBanSanitizer;
        _deploymentService = new ShopMigrationDeploymentService(_apiClient, _antiBanSanitizer);

        Dock = DockStyle.Fill;
        BuildLayout();
        HookEvents();
        _ = LoadAndVerifyTargetShopAsync();
    }

    public void SetListingsToMigrate(IReadOnlyList<VaultListing> listings)
    {
        _queuedListings = listings ?? [];
        _lblQueueSummary.Text = $"Kuyrukta: {_queuedListings.Count} adet ürün transfer edilmeye hazır";
        _progressBar.Value = 0;
        _lblProgressStatus.Text = $"{_queuedListings.Count} ürün transfer edilmek üzere bekliyor.";
        _btnStart.Enabled = _queuedListings.Count > 0;
        _btnStart.Text = $"🚀 1-TIKLA GÜVENLİ GÖÇÜ BAŞLAT ({_queuedListings.Count} ÜRÜN)";
        _btnRetryFailed.Visible = false;
        AppendLog($"[KUYRUK GÜNCELLENDİ] {_queuedListings.Count} ürün transfer kuyruğuna yüklendi.");
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(8),
            BackColor = UiStyle.BackgroundColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));  // Shop & Account Card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); // Anti-Ban Settings Card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116)); // Deployment Actions & Progress
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Live Log Console

        // 1. SHOP VERIFICATION CARD
        var shopCard = CreateCardPanel("🎯 Hedef Etsy Mağazası (Aktarım / Yeni Mağaza Hesabı)", 66);
        var shopFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = true,
            WrapContents = false,
            Padding = new Padding(8, 4, 8, 4)
        };
        shopFlow.Controls.Add(_lblShopStatus);
        shopFlow.Controls.Add(_btnVerifyShop);
        shopFlow.Controls.Add(_btnOpenApiSettings);
        shopCard.Controls.Add(shopFlow);
        mainLayout.Controls.Add(shopCard, 0, 0);

        // 2. ANTI-BAN & MAPPING CARD
        var antiBanCard = CreateCardPanel("🛡️ Anti-Ban Koruma Profili, Yapay Zeka Özgünleştirme & Kargo Eşleştirme", 170);
        var settingsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(8, 4, 8, 4)
        };
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        // Preset selector bar (Row 0 across columns)
        var pnlPresets = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4)
        };
        pnlPresets.Controls.Add(new Label { Text = "Güvenlik Seviyesi:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 8, 6, 0), Font = new Font("Segoe UI Semibold", 9F) });
        pnlPresets.Controls.Add(_btnPresetHigh);
        pnlPresets.Controls.Add(_btnPresetStandard);
        pnlPresets.Controls.Add(_btnPresetClone);
        settingsTable.SetColumnSpan(pnlPresets, 3);
        settingsTable.Controls.Add(pnlPresets, 0, 0);

        // Column 1: Core Protection Checkboxes
        settingsTable.Controls.Add(_chkStripExif, 0, 1);
        settingsTable.Controls.Add(_chkPermutateImageHash, 0, 2);
        settingsTable.Controls.Add(_chkDraftFirst, 0, 3);

        // Column 2: AI Rewriting & Delays
        settingsTable.Controls.Add(_chkAiRewriteTitle, 1, 1);
        settingsTable.Controls.Add(_chkAiRewriteDesc, 1, 2);

        var pnlThrottle = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlThrottle.Controls.Add(new Label { Text = "İnsan Gecikmesi (sn):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 2, 0) });
        pnlThrottle.Controls.Add(_numThrottle);
        pnlThrottle.Controls.Add(new Label { Text = "Fiyat Farkı (%):", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(6, 4, 2, 0) });
        pnlThrottle.Controls.Add(_numPriceAdj);
        settingsTable.Controls.Add(pnlThrottle, 1, 3);

        // Column 3: Mapping & SKU
        var pnlShipping = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlShipping.Controls.Add(new Label { Text = "Kargo Profili ID:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 2, 0) });
        pnlShipping.Controls.Add(_txtDefaultShippingId);
        settingsTable.Controls.Add(pnlShipping, 2, 1);

        var pnlReturn = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlReturn.Controls.Add(new Label { Text = "İade Şablonu ID:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 2, 0) });
        pnlReturn.Controls.Add(_txtDefaultReturnId);
        settingsTable.Controls.Add(pnlReturn, 2, 2);

        var pnlSku = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlSku.Controls.Add(new Label { Text = "SKU Ön Eki:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 4, 2, 0) });
        pnlSku.Controls.Add(_txtSkuPrefix);
        settingsTable.Controls.Add(pnlSku, 2, 3);

        antiBanCard.Controls.Add(settingsTable);
        mainLayout.Controls.Add(antiBanCard, 0, 1);

        // 3. ACTIONS & PROGRESS CARD
        var actionCard = CreateCardPanel("🚀 1-Tıkla Dağıtım & İlerleme Motoru", 110);
        var actionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(8, 4, 8, 4) };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        var statusFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        statusFlow.Controls.Add(_lblQueueSummary);
        statusFlow.Controls.Add(new Label { Text = "  |  ", ForeColor = UiStyle.BorderColor, AutoSize = true });
        statusFlow.Controls.Add(_lblProgressStatus);
        actionLayout.Controls.Add(statusFlow, 0, 0);

        var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        btnFlow.Controls.Add(_btnStart);
        btnFlow.Controls.Add(_btnCancel);
        btnFlow.Controls.Add(_btnRetryFailed);
        actionLayout.Controls.Add(btnFlow, 0, 1);

        actionLayout.Controls.Add(_progressBar, 0, 2);
        actionCard.Controls.Add(actionLayout);
        mainLayout.Controls.Add(actionCard, 0, 2);

        // 4. LIVE LOG CONSOLE
        var logCard = CreateCardPanel("📜 Canlı Dağıtım Terminali & Hata Teşhis Konsolu", 100);
        logCard.Controls.Add(_txtLog);
        mainLayout.Controls.Add(logCard, 0, 3);

        Controls.Add(mainLayout);
    }

    private Panel CreateCardPanel(string title, int minHeight)
    {
        var panel = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(6),
            Margin = new Padding(0, 0, 0, 8),
            MinimumSize = new Size(0, minHeight)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
            ForeColor = UiStyle.PrimaryColor,
            Padding = new Padding(4, 2, 0, 0)
        };
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private void HookEvents()
    {
        _btnPresetHigh.Click += (_, _) => ApplyPreset(0);
        _btnPresetStandard.Click += (_, _) => ApplyPreset(1);
        _btnPresetClone.Click += (_, _) => ApplyPreset(2);

        _btnVerifyShop.Click += async (_, _) => await LoadAndVerifyTargetShopAsync();
        _btnOpenApiSettings.Click += (_, _) =>
        {
            using var dlg = new EtsyApiSettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadAndVerifyTargetShopAsync();
            }
        };

        _btnStart.Click += async (_, _) => await StartMigrationAsync(_queuedListings);
        _btnRetryFailed.Click += async (_, _) =>
        {
            if (_failedListings.Count > 0)
            {
                await StartMigrationAsync(_failedListings);
            }
        };
        _btnCancel.Click += (_, _) =>
        {
            _cts?.Cancel();
            AppendLog("[İPTAL TALEBİ] Kullanıcı transferi durdurdu.");
        };
    }

    private void ApplyPreset(int presetIndex)
    {
        _btnPresetHigh.BackColor = presetIndex == 0 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnPresetHigh.ForeColor = presetIndex == 0 ? Color.White : UiStyle.TextDark;

        _btnPresetStandard.BackColor = presetIndex == 1 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnPresetStandard.ForeColor = presetIndex == 1 ? Color.White : UiStyle.TextDark;

        _btnPresetClone.BackColor = presetIndex == 2 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnPresetClone.ForeColor = presetIndex == 2 ? Color.White : UiStyle.TextDark;

        if (presetIndex == 0) // High Security
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = true;
            _chkAiRewriteTitle.Checked = true;
            _chkAiRewriteDesc.Checked = true;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 5;
            AppendLog("[GÜVENLİK PROFİLİ] 'Maksimum Güvenlik' seçildi: EXIF temizle, pHash micro-crop, AI başlık/açıklama aktif.");
        }
        else if (presetIndex == 1) // Standard
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = true;
            _chkAiRewriteTitle.Checked = false;
            _chkAiRewriteDesc.Checked = false;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 4;
            AppendLog("[GÜVENLİK PROFİLİ] 'Hızlı Standart' seçildi: EXIF ve pHash temizleme aktif.");
        }
        else // Mirror clone
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = false;
            _chkAiRewriteTitle.Checked = false;
            _chkAiRewriteDesc.Checked = false;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 3;
            AppendLog("[GÜVENLİK PROFİLİ] 'Ayna Klon' seçildi: Sadece EXIF temizleme.");
        }
    }

    private async Task LoadAndVerifyTargetShopAsync()
    {
        try
        {
            _btnVerifyShop.Enabled = false;
            _lblShopStatus.Text = "⏳ Hedef mağaza API bağlantısı test ediliyor...";
            _lblShopStatus.ForeColor = UiStyle.WarningColor;

            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasAccessToken)
            {
                _lblShopStatus.Text = "⚠️ Hedef Etsy API erişim anahtarı (Access Token) bulunamadı. Lütfen API Ayarlarını yapın.";
                _lblShopStatus.ForeColor = UiStyle.DangerColor;
                return;
            }

            var shopInfo = await _apiClient.GetCurrentShopInfoAsync();
            _targetShopId = shopInfo.ShopId;
            _targetShopName = shopInfo.ShopName;

            _lblShopStatus.Text = $"✅ Bağlandı: {shopInfo.ShopName} (ID: {shopInfo.ShopId}) - Aktarıma Hazır";
            _lblShopStatus.ForeColor = UiStyle.SuccessColor;
            AppendLog($"[HEDEF MAĞAZA DOĞRULANDI] {shopInfo.ShopName} (#{shopInfo.ShopId}) - URL: {shopInfo.ShopUrl}");
        }
        catch (Exception ex)
        {
            _lblShopStatus.Text = $"❌ Bağlantı hatası: {ex.Message}";
            _lblShopStatus.ForeColor = UiStyle.DangerColor;
            AppendLog($"[HEDEF MAĞAZA HATASI] {ex.Message}");
        }
        finally
        {
            _btnVerifyShop.Enabled = true;
        }
    }

    private async Task StartMigrationAsync(IReadOnlyList<VaultListing> listings)
    {
        if (listings.Count == 0)
        {
            MessageBox.Show(
                "Transfer edilecek ürün bulunamadı. Lütfen 'Ürün Kasası & İnceleme' sekmesinden ürünleri seçip 'Seçilenleri Transfer Et' butonuna tıklayın.",
                "Uyarı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_targetShopId <= 0)
        {
            await LoadAndVerifyTargetShopAsync();
            if (_targetShopId <= 0)
            {
                MessageBox.Show("Hedef Etsy mağazasına bağlanılamadı. Lütfen API ayarlarınızı kontrol edin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        var confirm = MessageBox.Show(
            $"{listings.Count} adet ürün '{_targetShopName}' (#{_targetShopId}) mağazasına taslak olarak aktarılacaktır.\n\n" +
            $"• Anti-Ban EXIF Temizleme: {(_chkStripExif.Checked ? "Aktif" : "Pasif")}\n" +
            $"• pHash Parmak İzi Kırıcı: {(_chkPermutateImageHash.Checked ? "Aktif" : "Pasif")}\n" +
            $"• Yapay Zeka Başlık/Açıklama: {(_chkAiRewriteTitle.Checked || _chkAiRewriteDesc.Checked ? "Aktif" : "Pasif")}\n" +
            $"• İnsan Davranışı Gecikmesi: {_numThrottle.Value} sn\n\n" +
            "Transferi başlatmak istiyor musunuz?",
            "1-Tıkla Güvenli Etsy Mağaza Göçü",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        // Prepare settings & mapping
        var antiBanSettings = new AntiBanSettings
        {
            StripExifMetadata = _chkStripExif.Checked,
            PermutateImageHash = _chkPermutateImageHash.Checked,
            RewriteTitleWithAi = _chkAiRewriteTitle.Checked,
            RewriteDescriptionWithAi = _chkAiRewriteDesc.Checked,
            CreateAsDraftFirst = _chkDraftFirst.Checked,
            ThrottleDelaySeconds = (int)_numThrottle.Value,
            PriceAdjustmentPercent = _numPriceAdj.Value,
            SkuPrefix = _txtSkuPrefix.Text.Trim()
        };

        long.TryParse(_txtDefaultShippingId.Text.Trim(), out var defShippingId);
        long.TryParse(_txtDefaultReturnId.Text.Trim(), out var defReturnId);

        var mapping = new MigrationMappingProfile
        {
            TargetShopId = _targetShopId,
            TargetShopName = _targetShopName,
            DefaultTargetShippingProfileId = defShippingId,
            DefaultTargetReturnPolicyId = defReturnId
        };

        string? aiApiKey = null;
        try
        {
            aiApiKey = AiOptimizationSettingsStore.Load()?.OpenAiApiKey;
        }
        catch { }

        _cts = new CancellationTokenSource();
        _btnStart.Enabled = false;
        _btnCancel.Enabled = true;
        _btnRetryFailed.Visible = false;
        _failedListings.Clear();

        var progress = new Progress<ShopMigrationProgress>(p =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateProgress(p));
            }
            else
            {
                UpdateProgress(p);
            }
        });

        AppendLog($"[BAŞLADI] {listings.Count} ürün için transfer işlemi başlatıldı. Hedef: {_targetShopName}");

        try
        {
            var result = await _deploymentService.DeployListingsAsync(
                listings,
                mapping,
                antiBanSettings,
                aiApiKey,
                progress,
                _cts.Token);

            AppendLog($"[BİTTİ] Toplam: {result.TotalProcessed}, Başarılı: {result.SuccessCount}, Hatalı: {result.FailedCount}");

            if (result.FailedCount > 0)
            {
                var successfulIds = new HashSet<long>(result.CreatedListingIds);
                _failedListings = listings.Where(l => !successfulIds.Contains(l.ListingId)).ToList();
                _btnRetryFailed.Visible = _failedListings.Count > 0;
                _btnRetryFailed.Text = $"🔄 Başarısız {_failedListings.Count} Ürünü Yeniden Dene";
            }
            else
            {
                MessageBox.Show(
                    $"Tebrikler! {result.SuccessCount} ürün başarıyla '{_targetShopName}' mağazasına taslak olarak aktarıldı.",
                    "Transfer Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("[DURDURULDU] Transfer işlemi kullanıcı tarafından iptal edildi.");
        }
        catch (Exception ex)
        {
            AppendLog($"[KRİTİK HATA] Transfer motoru durdu: {ex.Message}");
            MessageBox.Show($"Transfer hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnStart.Enabled = true;
            _btnCancel.Enabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void UpdateProgress(ShopMigrationProgress p)
    {
        _progressBar.Value = Math.Clamp((int)p.Percent, 0, 100);
        _lblProgressStatus.Text = $"{p.ProcessedCount}/{p.TotalCount} (Başarılı: {p.SuccessCount}, Hatalı: {p.FailedCount}) • {p.StatusMessage}";

        if (!string.IsNullOrWhiteSpace(p.ErrorDetail))
        {
            AppendLog($"❌ HATA: {p.ErrorDetail}");
        }
        else if (!string.IsNullOrWhiteSpace(p.CurrentListingTitle))
        {
            AppendLog($"➡️ {p.StatusMessage}");
        }
        else
        {
            AppendLog($"ℹ️ {p.StatusMessage}");
        }
    }

    private void AppendLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }
}
