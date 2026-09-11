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
    private readonly Label _lblShopStatus = new() { AutoSize = true, Text = "Hedef mağaza kontrol ediliyor...", ForeColor = UiStyle.TextMuted, Font = UiStyle.BaseFont };
    private readonly Button _btnVerifyShop = new() { Text = "🔍 Bağlantıyı Doğrula", AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = UiStyle.CardBackground, ForeColor = UiStyle.TextDark, Cursor = Cursors.Hand };
    private readonly Button _btnOpenApiSettings = new() { Text = "⚙️ API Ayarları", AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = UiStyle.CardBackground, ForeColor = UiStyle.TextDark, Cursor = Cursors.Hand };
    private long _targetShopId = 0;
    private string _targetShopName = "";

    // Security & Anti-ban Presets
    private readonly ComboBox _cboPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Font = UiStyle.BaseFont };
    private readonly CheckBox _chkStripExif = new() { Text = "EXIF & Kamera Metaverilerini Temizle", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark };
    private readonly CheckBox _chkPermutateImageHash = new() { Text = "pHash Kırıcı (1px Micro-crop & Yeniden Kodlama)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark };
    private readonly CheckBox _chkAiRewriteTitle = new() { Text = "Yapay Zeka ile Başlığı Özgünleştir (AI Title)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark };
    private readonly CheckBox _chkAiRewriteDesc = new() { Text = "Yapay Zeka ile Açıklamayı Özgünleştir (AI Desc)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark };
    private readonly CheckBox _chkDraftFirst = new() { Text = "Güvenli Taslak Olarak Yükle (Draft Mode)", Checked = true, AutoSize = true, ForeColor = UiStyle.TextDark };
    private readonly NumericUpDown _numThrottle = new() { Minimum = 1, Maximum = 30, Value = 4, Width = 70 };
    private readonly NumericUpDown _numPriceAdj = new() { Minimum = -50, Maximum = 100, Value = 0, Width = 70 };
    private readonly TextBox _txtSkuPrefix = new() { Text = "NEW_", Width = 90 };

    // Mapping inputs
    private readonly TextBox _txtDefaultShippingId = new() { Width = 160, PlaceholderText = "Örn: 123456789" };
    private readonly TextBox _txtDefaultReturnId = new() { Width = 160, PlaceholderText = "Örn: 987654321" };

    // Deployment controls
    private readonly Label _lblQueueSummary = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold), ForeColor = UiStyle.AccentColor, Text = "Kuyrukta: 0 ürün seçili" };
    private readonly Button _btnStart = new() { Text = "🚀 Güvenli Transferi Başlat", Height = 42, Width = 230, FlatStyle = FlatStyle.Flat, BackColor = UiStyle.PrimaryColor, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
    private readonly Button _btnCancel = new() { Text = "⏹️ İptal Et", Height = 42, Width = 110, FlatStyle = FlatStyle.Flat, BackColor = UiStyle.DangerColor, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Enabled = false, Cursor = Cursors.Hand };
    private readonly Button _btnRetryFailed = new() { Text = "🔄 Başarısızları Yeniden Dene", Height = 42, Width = 210, FlatStyle = FlatStyle.Flat, BackColor = UiStyle.WarningColor, ForeColor = Color.Black, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Visible = false, Cursor = Cursors.Hand };
    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Fill, Height = 22, Minimum = 0, Maximum = 100, Value = 0 };
    private readonly Label _lblProgressStatus = new() { AutoSize = true, ForeColor = UiStyle.TextDark, Font = UiStyle.BaseFont, Text = "Hazır." };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Consolas", 9F) };

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
        _lblQueueSummary.Text = $"Kuyrukta: {_queuedListings.Count} ürün transfer edilmeye hazır";
        _progressBar.Value = 0;
        _lblProgressStatus.Text = $"{_queuedListings.Count} ürün transfer edilmek üzere bekliyor.";
        _btnStart.Enabled = _queuedListings.Count > 0;
        _btnRetryFailed.Visible = false;
        AppendLog($"[KUYRUK GÜNCELLENDİ] {_queuedListings.Count} ürün transfer sihirbazına yüklendi.");
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(12),
            BackColor = UiStyle.BackgroundColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));  // Shop & Account Card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156)); // Anti-Ban Settings Card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Deployment Actions & Progress
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Live Log Console

        // 1. SHOP VERIFICATION CARD
        var shopCard = CreateCardPanel("🎯 Hedef Etsy Mağazası (Yeni/Aktarım Mağazası)", 70);
        var shopFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true, WrapContents = false, Padding = new Padding(8, 6, 8, 4) };
        shopFlow.Controls.Add(_lblShopStatus);
        shopFlow.Controls.Add(_btnVerifyShop);
        shopFlow.Controls.Add(_btnOpenApiSettings);
        shopCard.Controls.Add(shopFlow);
        mainLayout.Controls.Add(shopCard, 0, 0);

        // 2. ANTI-BAN & MAPPING CARD
        var antiBanCard = CreateCardPanel("🛡️ Anti-Ban Koruma Profili, Yapay Zeka Özgünleştirme & Kargo Şablonları", 150);
        var settingsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, Padding = new Padding(8, 4, 8, 4) };
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        // Preset selector
        _cboPreset.Items.AddRange(new object[] { "🛡️ Maksimum Güvenlik (Önerilen)", "⚡ Standart Koruma", "📋 Ayna Klon (Anti-Bansız)" });
        _cboPreset.SelectedIndex = 0;
        var pnlPreset = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlPreset.Controls.Add(new Label { Text = "Güvenlik Profili:", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(0, 4, 0, 0) });
        pnlPreset.Controls.Add(_cboPreset);
        settingsTable.Controls.Add(pnlPreset, 0, 0);

        settingsTable.Controls.Add(_chkStripExif, 0, 1);
        settingsTable.Controls.Add(_chkPermutateImageHash, 0, 2);
        settingsTable.Controls.Add(_chkDraftFirst, 0, 3);

        settingsTable.Controls.Add(_chkAiRewriteTitle, 1, 0);
        settingsTable.Controls.Add(_chkAiRewriteDesc, 1, 1);

        var pnlThrottle = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlThrottle.Controls.Add(new Label { Text = "İnsan Gecikmesi (sn):", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(0, 4, 0, 0) });
        pnlThrottle.Controls.Add(_numThrottle);
        settingsTable.Controls.Add(pnlThrottle, 1, 2);

        var pnlPrice = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlPrice.Controls.Add(new Label { Text = "Fiyat Farkı (%):", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(0, 4, 0, 0) });
        pnlPrice.Controls.Add(_numPriceAdj);
        pnlPrice.Controls.Add(new Label { Text = "SKU Ön Eki:", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(6, 4, 0, 0) });
        pnlPrice.Controls.Add(_txtSkuPrefix);
        settingsTable.Controls.Add(pnlPrice, 1, 3);

        // Column 3: Mapping
        var pnlShipping = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlShipping.Controls.Add(new Label { Text = "Varsayılan Kargo ID:", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(0, 4, 0, 0) });
        pnlShipping.Controls.Add(_txtDefaultShippingId);
        settingsTable.Controls.Add(pnlShipping, 2, 0);

        var pnlReturn = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlReturn.Controls.Add(new Label { Text = "Varsayılan İade ID:", AutoSize = true, ForeColor = UiStyle.TextMuted, Padding = new Padding(0, 4, 0, 0) });
        pnlReturn.Controls.Add(_txtDefaultReturnId);
        settingsTable.Controls.Add(pnlReturn, 2, 1);

        antiBanCard.Controls.Add(settingsTable);
        mainLayout.Controls.Add(antiBanCard, 0, 1);

        // 3. ACTIONS & PROGRESS CARD
        var actionCard = CreateCardPanel("🚀 Dağıtım & İlerleme Motoru", 104);
        var actionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(8, 4, 8, 4) };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
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
        var logCard = CreateCardPanel("📜 Canlı Dağıtım & Hata Teşhis Konsolu", 100);
        logCard.Controls.Add(_txtLog);
        mainLayout.Controls.Add(logCard, 0, 3);

        Controls.Add(mainLayout);
    }

    private Panel CreateCardPanel(string title, int minHeight)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.CardBackground,
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
        _cboPreset.SelectedIndexChanged += (_, _) =>
        {
            if (_cboPreset.SelectedIndex == 0) // High Security
            {
                _chkStripExif.Checked = true;
                _chkPermutateImageHash.Checked = true;
                _chkAiRewriteTitle.Checked = true;
                _chkAiRewriteDesc.Checked = true;
                _chkDraftFirst.Checked = true;
                _numThrottle.Value = 5;
            }
            else if (_cboPreset.SelectedIndex == 1) // Standard
            {
                _chkStripExif.Checked = true;
                _chkPermutateImageHash.Checked = true;
                _chkAiRewriteTitle.Checked = false;
                _chkAiRewriteDesc.Checked = false;
                _chkDraftFirst.Checked = true;
                _numThrottle.Value = 4;
            }
            else // Mirror clone
            {
                _chkStripExif.Checked = true;
                _chkPermutateImageHash.Checked = false;
                _chkAiRewriteTitle.Checked = false;
                _chkAiRewriteDesc.Checked = false;
                _chkDraftFirst.Checked = true;
                _numThrottle.Value = 3;
            }
        };

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

            _lblShopStatus.Text = $"✅ Bağlandı: {shopInfo.ShopName} (ID: {shopInfo.ShopId})";
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
            MessageBox.Show("Transfer edilecek ürün bulunamadı. Lütfen Katalog sekmesinden ürün seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            $"Anti-Ban Koruma: {(_chkStripExif.Checked ? "Aktif" : "Pasif")}\n" +
            $"pHash Kırıcı: {(_chkPermutateImageHash.Checked ? "Aktif" : "Pasif")}\n" +
            $"Yapay Zeka Başlık/Açıklama: {(_chkAiRewriteTitle.Checked || _chkAiRewriteDesc.Checked ? "Aktif" : "Pasif")}\n" +
            $"İnsan Gecikmesi: {_numThrottle.Value} sn\n\n" +
            "Devam etmek istiyor musunuz?",
            "Güvenli Etsy Mağaza Göçü Onayı",
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
                // Find which listings failed
                var successfulIds = new HashSet<long>(result.CreatedListingIds);
                _failedListings = listings.Where(l => !successfulIds.Contains(l.ListingId)).ToList();
                _btnRetryFailed.Visible = _failedListings.Count > 0;
                _btnRetryFailed.Text = $"🔄 Başarısız {_failedListings.Count} Ürünü Yeniden Dene";
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
        _lblProgressStatus.Text = $"{p.ProcessedCount}/{p.TotalCount} (Başarılı: {p.SuccessCount}, Hatalı: {p.FailedCount}) - {p.StatusMessage}";

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
