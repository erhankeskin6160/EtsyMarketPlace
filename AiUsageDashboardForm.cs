namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class AiUsageDashboardForm : Form
{
    private readonly ComboBox _cboProvider = new();
    private readonly ComboBox _cboPeriod = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnExport = new();
    private readonly Button _btnGeminiRateLimit = new();

    // KPI Cards
    private readonly Label _lblCard1Title = new();
    private readonly Label _lblCard1Value = new();
    private readonly Label _lblCard1Sub = new();

    private readonly Label _lblCard2Title = new();
    private readonly Label _lblCard2Value = new();
    private readonly Label _lblCard2Sub = new();

    private readonly Label _lblCard3Title = new();
    private readonly Label _lblCard3Value = new();
    private readonly Label _lblCard3Sub = new();

    private readonly Label _lblCard4Title = new();
    private readonly Label _lblCard4Value = new();
    private readonly Label _lblCard4Sub = new();

    private readonly FlowLayoutPanel _pnlModelBreakdown = new();
    private readonly DataGridView _grid = new();
    private readonly Label _lblStatus = new();

    private AiProviderBalanceInfo? _deepSeekBalance;
    private AiProviderBalanceInfo? _openAiStatus;
    private AiProviderBalanceInfo? _geminiStatus;
    private OpenAiOfficialUsageReport? _officialOpenAiReport;
    private GeminiRateLimitReport? _geminiRateLimitReport;

    public AiUsageDashboardForm()
    {
        Text = "AI Token, Maliyet & Bakiye Takip Merkezi";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 750);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = UiStyle.BackgroundColor;
        ForeColor = Color.White;

        BuildLayout();
        Load += async (_, _) => await RefreshDataAsync();

        Action onUsage = () =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(async () => await RefreshDataAsync(queryLiveBalance: false));
            }
            catch { }
        };
        AiDataCacheService.OnUsageUpdated += onUsage;
        FormClosed += (_, _) => AiDataCacheService.OnUsageUpdated -= onUsage;
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Başlık
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Filtre çubuğu
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118)); // 4 KPI Kartı (DPI güvenli yükseklik)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Model Dağılım Barı
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tablo
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Alt durum çubuğu
        Controls.Add(mainLayout);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "⚡ Yapay Zeka Token, Maliyet & Bakiye Takip Merkezi",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 0)
        };
        var lblSub = new Label
        {
            Text = "Model bazlı resmi token tüketimi, DeepSeek/OpenAI canlı bakiye sorgusu ve kuruşu kuruşuna maliyet dökümü.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(160, 170, 190),
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 28)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSub);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // 2. Filter Bar
        var pnlFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var lblProv = new Label { Text = "Sağlayıcı:", AutoSize = true, Margin = new Padding(0, 8, 5, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _cboProvider.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboProvider.Items.AddRange(["Tümü (Genel Bakış)", "DeepSeek", "Google Gemini", "OpenAI", "Claude", "xAI Grok"]);
        _cboProvider.SelectedIndex = 0;
        _cboProvider.Width = 170;
        _cboProvider.SelectedIndexChanged += async (_, _) =>
        {
            UpdateProviderSpecificControls();
            await RefreshDataAsync();
        };

        var lblPer = new Label { Text = "Zaman:", AutoSize = true, Margin = new Padding(15, 8, 5, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _cboPeriod.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboPeriod.Items.AddRange(["Bugün", "Son 7 Gün", "Bu Ay (1-30 Gün)", "Tüm Zamanlar"]);
        _cboPeriod.SelectedIndex = 2; // Bu Ay
        _cboPeriod.Width = 140;
        _cboPeriod.SelectedIndexChanged += async (_, _) => await RefreshDataAsync();

        _btnRefresh.Text = "🔄 Yenile & Canlı Bakiye";
        _btnRefresh.BackColor = UiStyle.AiColor;
        _btnRefresh.ForeColor = Color.White;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Margin = new Padding(20, 2, 0, 0);
        _btnRefresh.Height = 32;
        _btnRefresh.Width = 180;
        _btnRefresh.Cursor = Cursors.Hand;
        _btnRefresh.Click += async (_, _) => await RefreshDataAsync(queryLiveBalance: true);

        _btnExport.Text = "📥 CSV Olarak İndir";
        _btnExport.BackColor = Color.FromArgb(45, 55, 75);
        _btnExport.ForeColor = Color.White;
        _btnExport.FlatStyle = FlatStyle.Flat;
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Margin = new Padding(10, 2, 0, 0);
        _btnExport.Height = 32;
        _btnExport.Width = 140;
        _btnExport.Cursor = Cursors.Hand;
        _btnExport.Click += ExportCsv;

        var btnApiHub = new Button
        {
            Text = "⚡ AI Sağlayıcı & API Merkezi",
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            Width = 220,
            Cursor = Cursors.Hand,
            Margin = new Padding(10, 2, 0, 0)
        };
        btnApiHub.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 100);
        btnApiHub.Click += async (_, _) =>
        {
            using var hub = new AiProviderHubDialog();
            hub.ShowDialog(this);
            await RefreshDataAsync(queryLiveBalance: true);
        };

        _btnGeminiRateLimit.Text = "📊 Gemini Hız Sınırları & Kotalar";
        _btnGeminiRateLimit.BackColor = Color.FromArgb(139, 92, 246);
        _btnGeminiRateLimit.ForeColor = Color.White;
        _btnGeminiRateLimit.FlatStyle = FlatStyle.Flat;
        _btnGeminiRateLimit.Height = 32;
        _btnGeminiRateLimit.Width = 235;
        _btnGeminiRateLimit.Cursor = Cursors.Hand;
        _btnGeminiRateLimit.Margin = new Padding(10, 2, 0, 0);
        _btnGeminiRateLimit.Visible = false; // Sadece dropdown'da Google Gemini seçilirse gözükür
        _btnGeminiRateLimit.FlatAppearance.BorderSize = 0;
        _btnGeminiRateLimit.Click += (_, _) =>
        {
            using var dlg = new GeminiRateLimitDialog();
            dlg.ShowDialog(this);
        };

        pnlFilters.Controls.Add(lblProv);
        pnlFilters.Controls.Add(_cboProvider);
        pnlFilters.Controls.Add(lblPer);
        pnlFilters.Controls.Add(_cboPeriod);
        pnlFilters.Controls.Add(_btnRefresh);
        pnlFilters.Controls.Add(btnApiHub);
        pnlFilters.Controls.Add(_btnGeminiRateLimit);
        pnlFilters.Controls.Add(_btnExport);
        mainLayout.Controls.Add(pnlFilters, 0, 1);

        // 3. KPI Cards
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        for (int i = 0; i < 4; i++) pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        Action onGeminiCardClick = () =>
        {
            if (_cboProvider.SelectedItem?.ToString() == "Google Gemini")
            {
                using var dlg = new GeminiRateLimitDialog();
                dlg.ShowDialog(this);
            }
            else
            {
                using var hub = new AiProviderHubDialog();
                hub.ShowDialog(this);
            }
        };

        pnlCards.Controls.Add(CreateKpiCard(_lblCard1Title, _lblCard1Value, _lblCard1Sub, Color.FromArgb(20, 35, 60), onGeminiCardClick), 0, 0);
        pnlCards.Controls.Add(CreateKpiCard(_lblCard2Title, _lblCard2Value, _lblCard2Sub, Color.FromArgb(25, 30, 50)), 1, 0);
        pnlCards.Controls.Add(CreateKpiCard(_lblCard3Title, _lblCard3Value, _lblCard3Sub, Color.FromArgb(30, 25, 45)), 2, 0);
        pnlCards.Controls.Add(CreateKpiCard(_lblCard4Title, _lblCard4Value, _lblCard4Sub, Color.FromArgb(40, 25, 30), onGeminiCardClick), 3, 0);
        mainLayout.Controls.Add(pnlCards, 0, 2);

        // 4. Model Breakdown Bar
        _pnlModelBreakdown.Dock = DockStyle.Fill;
        _pnlModelBreakdown.FlowDirection = FlowDirection.LeftToRight;
        _pnlModelBreakdown.WrapContents = true;
        mainLayout.Controls.Add(_pnlModelBreakdown, 0, 3);

        // 5. DataGridView
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(22, 27, 34);
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(30, 36, 46),
            ForeColor = Color.FromArgb(200, 210, 225),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(22, 27, 34),
            ForeColor = Color.White,
            SelectionBackColor = Color.FromArgb(40, 50, 70),
            SelectionForeColor = Color.White
        };

        _grid.Columns.Add("Date", "Tarih/Saat");
        _grid.Columns.Add("Module", "Modül");
        _grid.Columns.Add("Provider", "Sağlayıcı");
        _grid.Columns.Add("Model", "Model");
        _grid.Columns.Add("PromptTokens", "Giriş Token");
        _grid.Columns.Add("CompTokens", "Çıkış Token");
        _grid.Columns.Add("TotalTokens", "Toplam Token");
        _grid.Columns.Add("CostUsd", "Maliyet ($)");
        _grid.Columns.Add("CostTry", "Maliyet (₺)");
        _grid.Columns.Add("Status", "Durum");
        _grid.Columns.Add("Note", "Detay");

        _grid.Columns["Date"].Width = 130;
        _grid.Columns["Module"].Width = 140;
        _grid.Columns["Provider"].Width = 110;
        _grid.Columns["Model"].Width = 130;
        _grid.Columns["CostUsd"].Width = 85;
        _grid.Columns["CostTry"].Width = 85;
        _grid.Columns["Status"].Width = 130;

        mainLayout.Controls.Add(_grid, 0, 4);

        // 6. Status Label
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.ForeColor = Color.FromArgb(140, 150, 170);
        _lblStatus.Font = new Font("Segoe UI", 8.5F);
        mainLayout.Controls.Add(_lblStatus, 0, 5);
    }

    private Control CreateKpiCard(Label lblTitle, Label lblValue, Label lblSub, Color bg, Action? onClick = null)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(5)
        };

        if (onClick != null)
        {
            pnl.Cursor = Cursors.Hand;
            pnl.Click += (_, _) => onClick();
            lblTitle.Click += (_, _) => onClick();
            lblValue.Click += (_, _) => onClick();
            lblSub.Click += (_, _) => onClick();
        }

        lblTitle.UseMnemonic = false;
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(170, 185, 210);
        lblTitle.Height = 18;

        lblValue.UseMnemonic = false;
        lblValue.Dock = DockStyle.Top;
        lblValue.Font = new Font("Segoe UI", 14.5F, FontStyle.Bold);
        lblValue.ForeColor = Color.White;
        lblValue.Height = 36;
        lblValue.Margin = new Padding(0, 3, 0, 2);

        lblSub.UseMnemonic = false;
        lblSub.Dock = DockStyle.Bottom;
        lblSub.Font = new Font("Segoe UI", 8F);
        lblSub.ForeColor = Color.FromArgb(135, 150, 175);
        lblSub.Height = 22;

        pnl.Controls.Add(lblSub);
        pnl.Controls.Add(lblValue);
        pnl.Controls.Add(lblTitle);
        return pnl;
    }

    private void UpdateProviderSpecificControls()
    {
        string selected = _cboProvider.SelectedItem?.ToString() ?? "";
        _btnGeminiRateLimit.Visible = selected.Contains("Gemini", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshDataAsync(bool queryLiveBalance = false)
    {
        UseWaitCursor = true;
        _btnRefresh.Enabled = false;
        _lblStatus.Text = "Veriler güncelleniyor ve canlı bakiye sorgulanıyor...";

        try
        {
            var settings = AiOptimizationSettingsStore.Load();
            var selectedProvider = _cboProvider.SelectedItem?.ToString() ?? "Tümü (Genel Bakış)";
            UpdateProviderSpecificControls();

            // 1. Canlı Bakiye ve Sağlayıcı Durum Sorguları (Önbellekli / Delta)
            if (!string.IsNullOrWhiteSpace(settings.DeepSeekApiKey))
            {
                _deepSeekBalance = await AiBalanceCheckerService.CheckDeepSeekBalanceAsync(settings.DeepSeekApiKey, forceRefresh: queryLiveBalance);
            }
            if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey) || !string.IsNullOrWhiteSpace(settings.OpenAiAdminApiKey))
            {
                _openAiStatus = await AiBalanceCheckerService.CheckOpenAiStatusAsync(settings.OpenAiApiKey, settings.OpenAiAdminApiKey, forceRefresh: queryLiveBalance);
            }
            if (!string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                _geminiStatus = await AiBalanceCheckerService.CheckGeminiStatusAsync(settings.GeminiApiKey, forceRefresh: queryLiveBalance);
            }

            // 2. OpenAI Resmi Kullanım & Fatura Verilerini Otomatik Olarak Çek
            string? effectiveAdminKey = !string.IsNullOrWhiteSpace(settings.OpenAiAdminApiKey)
                ? settings.OpenAiAdminApiKey.Trim()
                : (settings.OpenAiApiKey?.Trim().StartsWith("sk-admin-", StringComparison.OrdinalIgnoreCase) == true ? settings.OpenAiApiKey.Trim() : null);

            int? targetDays = _cboPeriod.SelectedIndex switch
            {
                0 => 1,  // Bugün
                1 => 7,  // Son 7 Gün
                2 => 30, // Bu Ay (30 Gün)
                _ => 90  // Tüm Zamanlar
            };

            if (!string.IsNullOrWhiteSpace(effectiveAdminKey) && (selectedProvider.Contains("OpenAI") || selectedProvider.StartsWith("Tümü")))
            {
                try
                {
                    _officialOpenAiReport = await OpenAiUsageFetcherService.FetchOfficialUsageReportAsync(
                        settings.OpenAiApiKey ?? "",
                        settings.OpenAiAdminApiKey,
                        lastDays: targetDays,
                        forceRefresh: queryLiveBalance);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OpenAI canlı fatura çekme hatası: {ex.Message}");
                }
            }
            else if (!selectedProvider.Contains("OpenAI") && !selectedProvider.StartsWith("Tümü"))
            {
                _officialOpenAiReport = null;
            }

            // 2b. Gemini Resmi Hız Sınırları & Kota Verilerini Otomatik Olarak Çek
            if (selectedProvider.Contains("Gemini") && !string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                try
                {
                    _geminiRateLimitReport = await GeminiRateLimitService.FetchRateLimitReportAsync(
                        settings.GeminiApiKey,
                        days: targetDays ?? 28,
                        projectName: "gen-lang-client-0458130432",
                        forceRefresh: queryLiveBalance);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Gemini rate limit çekme hatası: {ex.Message}");
                }
            }
            else if (!selectedProvider.Contains("Gemini"))
            {
                _geminiRateLimitReport = null;
            }

            // 3. Zaman Filtresi (SQLite Yerel Veritabanı)
            DateTimeOffset? since = _cboPeriod.SelectedIndex switch
            {
                0 => DateTimeOffset.Now.Date, // Bugün
                1 => DateTimeOffset.Now.AddDays(-7), // Son 7 Gün
                2 => new DateTimeOffset(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)), // Bu Ay
                _ => null // Tüm Zamanlar
            };

            string? filterArg = selectedProvider.StartsWith("Tümü") ? null : selectedProvider;
            var repo = AiTokenUsageTrackerService.GetRepository();
            var stats = await repo.GetSummaryStatsAsync(filterArg, since);
            var records = await repo.GetHistoryAsync(filterArg, since, limit: 300);

            // 4. Kartları Dinamik Güncelle (Kullanıcının seçtiği sağlayıcıya ve resmi verilere göre)
            UpdateKpiCards(selectedProvider, stats);

            // 5. Model Dağılım Rozetleri
            UpdateModelBreakdown(stats, selectedProvider);

            // 6. Grid Tablosunu Doldur
            _grid.Rows.Clear();

            // 6a. OpenAI Resmi Dökümü (Otomatik olarak grid'e eklenir)
            if (_officialOpenAiReport != null && _officialOpenAiReport.DailyItems.Count > 0 && (selectedProvider.Contains("OpenAI") || selectedProvider.StartsWith("Tümü")))
            {
                foreach (var item in _officialOpenAiReport.DailyItems.OrderByDescending(x => x.Date))
                {
                    var idx = _grid.Rows.Add(
                        item.Date.ToString("yyyy-MM-dd"),
                        "OpenAI Platform",
                        "OpenAI",
                        item.ServiceOrModel,
                        item.InputTokens.ToString("N0"),
                        item.OutputTokens.ToString("N0"),
                        item.TotalTokens.ToString("N0"),
                        $"${item.CostUsd:F4}",
                        $"{item.CostTry:F2} ₺",
                        "Başarılı (Resmi)",
                        $"OpenAI resmi kullanım ({item.RequestCount} çağrı)"
                    );
                    _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(200, 225, 255);
                }
            }

            // 6b. Gemini Resmi Hız Sınırları & Model Kotası Dökümü (Otomatik olarak grid'e eklenir)
            if (_geminiRateLimitReport != null && _geminiRateLimitReport.Models.Count > 0 && selectedProvider.Contains("Gemini"))
            {
                foreach (var m in _geminiRateLimitReport.Models)
                {
                    string rpmVal = $"{m.PeakRpm} / {m.LimitRpm} RPM";
                    string tpmVal = $"{FormatTokens(m.PeakTpm)} / {FormatTokens(m.LimitTpm)} TPM";
                    string rpdVal = $"{m.PeakRpd} / {m.LimitRpd} RPD";
                    string status = m.IsCritical ? "🚨 KOTA AŞILDI" : "✅ Normal";
                    string note = m.IsExceededRpd
                        ? $"Günlük RPD limiti aşıldı! ({m.PeakRpd}/{m.LimitRpd})"
                        : (m.IsExceededRpm
                            ? $"Dakikalık RPM limiti aşıldı! ({m.PeakRpm}/{m.LimitRpm})"
                            : $"Free Tier Kota: {m.LimitRpm} RPM | {m.LimitRpd} RPD");

                    var idx = _grid.Rows.Add(
                        DateTime.Now.ToString("yyyy-MM-dd"),
                        m.Category,
                        "Google Gemini",
                        m.DisplayName,
                        rpmVal,
                        tpmVal,
                        rpdVal,
                        "$0.00 (Free)",
                        "0.00 ₺",
                        status,
                        note
                    );

                    if (m.IsCritical)
                    {
                        _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(255, 110, 110);
                        _grid.Rows[idx].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                    }
                    else
                    {
                        _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(180, 215, 255);
                    }
                }
            }

            // 6c. Yerel SQLite Kayıtları
            foreach (var r in records)
            {
                var idx = _grid.Rows.Add(
                    r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    r.ModuleName,
                    r.Provider,
                    r.ModelName,
                    r.PromptTokens.ToString("N0"),
                    r.CompletionTokens.ToString("N0"),
                    r.TotalTokens.ToString("N0"),
                    $"${r.EstimatedCostUsd:F5}",
                    $"{r.EstimatedCostTry:F4} ₺",
                    r.Status,
                    r.Note ?? ""
                );

                if (r.Status.Contains("429") || r.Status.Contains("Kota"))
                {
                    _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(255, 120, 120);
                }
            }

            if (_grid.Rows.Count == 0)
            {
                string infoMsg = selectedProvider.Contains("Gemini")
                    ? "Henüz Gemini ile bir işlem yapılmadı. 📊 Canlı Hız Sınırları & Kotaları (RPM/TPM/RPD ve 28 Günlük Grafiği) görmek için yukarıdaki [📊 Gemini Hız Sınırları] butonuna tıklayın."
                    : "Seçilen tarih aralığında ve filtrede henüz yapay zeka işlem kaydı bulunmuyor.";

                var idx = _grid.Rows.Add(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    "Bilgilendirme",
                    selectedProvider.Contains("Gemini") ? "Google Gemini" : selectedProvider,
                    "-",
                    "0",
                    "0",
                    "0",
                    "$0.00",
                    "0.00 ₺",
                    "Hazır",
                    infoMsg
                );
                _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(160, 175, 200);
            }

            int totalRows = _grid.Rows.Count;
            string officialNotice = (_officialOpenAiReport?.DailyItems.Count > 0) ? " | OpenAI resmi verileri dahil edildi" : "";
            string cacheNotice = queryLiveBalance ? " 🔄 (Canlı API Sorgulandı)" : " ⚡ (Akıllı Önbellek & 0ms)";
            _lblStatus.Text = $"Son güncelleme: {DateTime.Now:HH:mm:ss} | Toplam {totalRows} işlem/gün listelendi{officialNotice}{cacheNotice}.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Hata: {ex.Message}";
        }
        finally
        {
            UseWaitCursor = false;
            _btnRefresh.Enabled = true;
        }
    }

    private void UpdateKpiCards(string selectedProvider, AiUsageSummaryStats stats)
    {
        if (selectedProvider.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            _lblCard1Title.Text = "💳 DEEPSEEK CANLI BAKİYE";
            if (_deepSeekBalance != null && _deepSeekBalance.IsAvailable)
            {
                if (_deepSeekBalance.BalanceCny.HasValue && _deepSeekBalance.Currency == "CNY")
                {
                    _lblCard1Value.Text = $"¥{_deepSeekBalance.BalanceCny.Value:N2} CNY";
                    _lblCard1Sub.Text = $"~${_deepSeekBalance.TotalBalanceUsd:N2} USD (~{_deepSeekBalance.TotalBalanceTry():N0} TL)";
                }
                else
                {
                    _lblCard1Value.Text = $"${_deepSeekBalance.TotalBalanceUsd:N2} USD";
                    _lblCard1Sub.Text = $"~{_deepSeekBalance.TotalBalanceTry():N0} TL (Yüklenen: ${_deepSeekBalance.ToppedUpBalanceUsd:N2})";
                }
            }
            else
            {
                var settings = AiOptimizationSettingsStore.Load();
                if (string.IsNullOrWhiteSpace(settings.DeepSeekApiKey))
                {
                    _lblCard1Value.Text = "Tanımlanmadı";
                    _lblCard1Sub.Text = "🔑 'API Anahtarları' menüsünden tanımlayın";
                }
                else
                {
                    _lblCard1Value.Text = "Bakiye Alınamadı";
                    _lblCard1Sub.Text = _deepSeekBalance?.StatusMessage ?? "API anahtarı kontrol edin";
                }
            }

            _lblCard2Title.Text = "⚡ TÜKETİLEN TOPLAM TOKEN";
            _lblCard2Value.Text = stats.TotalTokens.ToString("N0");
            _lblCard2Sub.Text = $"Girdi: {stats.TotalPromptTokens:N0} | Çıkış: {stats.TotalCompletionTokens:N0}";

            _lblCard3Title.Text = "💰 TAHMİNİ TOPLAM FATURA";
            _lblCard3Value.Text = $"${stats.TotalCostUsd:N3} USD";
            _lblCard3Sub.Text = $"Yaklaşık {stats.TotalCostTry:N2} TL (DeepSeek Tarifesi)";

            _lblCard4Title.Text = "🚨 KOTA & İŞLEM SAĞLIĞI";
            _lblCard4Value.Text = _deepSeekBalance?.IsAvailable == true
                ? $"{stats.SuccessfulRequests} Başarılı (API Aktif)"
                : $"{stats.SuccessfulRequests} Başarılı";
            _lblCard4Sub.Text = stats.Blocked429Requests > 0
                ? $"⚠️ {stats.Blocked429Requests} İstek Kotaya Takıldı (429)!"
                : "Tüm DeepSeek istekleri sorunsuz tamamlandı";
        }
        else if (selectedProvider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _lblCard1Title.Text = "🔵 GEMINI KOTA & DURUM";
            if (_geminiRateLimitReport != null)
            {
                _lblCard1Value.Text = $"{_geminiRateLimitReport.TotalAvailableModels} Model Aktif";
                _lblCard1Sub.Text = $"Proje: {_geminiRateLimitReport.ProjectName} ({_geminiRateLimitReport.Tier})";
            }
            else if (_geminiStatus?.IsAvailable == true)
            {
                _lblCard1Value.Text = !string.IsNullOrWhiteSpace(_geminiStatus.MaskedApiKey)
                    ? _geminiStatus.MaskedApiKey
                    : "API Aktif (Hazır)";
                _lblCard1Sub.Text = _geminiStatus.StatusMessage;
            }
            else
            {
                var settings = AiOptimizationSettingsStore.Load();
                if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
                {
                    _lblCard1Value.Text = "Tanımlanmadı";
                    _lblCard1Sub.Text = "🔑 'API Anahtarları' menüsünden tanımlayın";
                }
                else
                {
                    _lblCard1Value.Text = "Bağlantı Hatası";
                    _lblCard1Sub.Text = _geminiStatus?.StatusMessage ?? "API anahtarını kontrol edin";
                }
            }

            if (_geminiRateLimitReport != null)
            {
                _lblCard2Title.Text = "⚡ TEPE HIZ YÜKÜ (RPM)";
                _lblCard2Value.Text = $"{_geminiRateLimitReport.PeakActiveRpm} / {_geminiRateLimitReport.PeakRpmLimit} Tepe";
                _lblCard2Sub.Text = $"Dakikalık İstek Yükü ({_geminiRateLimitReport.TimeRange})";

                _lblCard3Title.Text = "💎 TEPE TOKEN / DAKİKA (TPM)";
                _lblCard3Value.Text = $"{FormatTokens(_geminiRateLimitReport.PeakTpm)} / {FormatTokens(_geminiRateLimitReport.PeakTpmLimit)}";
                _lblCard3Sub.Text = "Resmi Free Tier Dakikalık Hacim";

                _lblCard4Title.Text = "🚨 GÜNLÜK İSTEK AŞIMI (RPD)";
                _lblCard4Value.Text = _geminiRateLimitReport.TotalCriticalOverQuotaModels > 0
                    ? $"{_geminiRateLimitReport.TotalCriticalOverQuotaModels} Model Kritik!"
                    : "Tüm Kotalar Normal";
                _lblCard4Sub.Text = _geminiRateLimitReport.TotalCriticalOverQuotaModels > 0
                    ? "Günlük Free Tier RPD aşımı mevcut (Tıkla ve İncele)"
                    : "Free Tier: 20 RPD ve 1.500 RPD Hazır";
            }
            else
            {
                _lblCard2Title.Text = "⚡ TÜKETİLEN TOPLAM TOKEN";
                _lblCard2Value.Text = stats.TotalTokens.ToString("N0");
                _lblCard2Sub.Text = $"Girdi: {stats.TotalPromptTokens:N0} | Çıkış: {stats.TotalCompletionTokens:N0}";

                _lblCard3Title.Text = "💰 TAHMİNİ TOPLAM FATURA";
                _lblCard3Value.Text = stats.TotalCostUsd == 0 ? "$0.00 USD" : $"${stats.TotalCostUsd:F4} USD";
                _lblCard3Sub.Text = $"Yaklaşık {stats.TotalCostTry:N2} TL (Resmi Gemini Tarifesi)";

                _lblCard4Title.Text = "🚨 KOTA & İŞLEM SAĞLIĞI";
                _lblCard4Value.Text = $"{stats.SuccessfulRequests} Başarılı";
                _lblCard4Sub.Text = stats.Blocked429Requests > 0
                    ? $"⚠️ {stats.Blocked429Requests} İstek Kotaya Takıldı (429)!"
                    : (_geminiStatus?.IsAvailable == true ? "Ücretsiz Plan: 15 RPM / 1,500 RPD Hazır" : "API bağlantı hatası");
            }
        }
        else if (selectedProvider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            _lblCard1Title.Text = "🔑 OPENAI RESMİ HESAP";
            string keyText = _officialOpenAiReport != null && !string.IsNullOrWhiteSpace(_officialOpenAiReport.MaskedKey)
                ? _officialOpenAiReport.MaskedKey
                : (_openAiStatus?.MaskedApiKey ?? "sk-admin-...");
            _lblCard1Value.Text = keyText;
            _lblCard1Sub.Text = _officialOpenAiReport?.HasAdminKey == true
                ? $"Canlı Bağlantı Aktif ({_officialOpenAiReport.DailyItems.Count} gün dökümü)"
                : (_openAiStatus?.StatusMessage ?? "OpenAI API Aktif");

            // Kart 2: Tüketilen Token (Girdi ve Çıktı Token Verileri)
            long inTokens = _officialOpenAiReport != null && _officialOpenAiReport.TotalTokens > 0
                ? _officialOpenAiReport.TotalInputTokens
                : stats.TotalPromptTokens;
            long outTokens = _officialOpenAiReport != null && _officialOpenAiReport.TotalTokens > 0
                ? _officialOpenAiReport.TotalOutputTokens
                : stats.TotalCompletionTokens;
            long totTokens = _officialOpenAiReport != null && _officialOpenAiReport.TotalTokens > 0
                ? _officialOpenAiReport.TotalTokens
                : stats.TotalTokens;

            _lblCard2Title.Text = "⚡ TÜKETİLEN TOPLAM TOKEN";
            _lblCard2Value.Text = totTokens.ToString("N0");
            _lblCard2Sub.Text = $"Girdi: {inTokens:N0} | Çıkış: {outTokens:N0}";

            // Kart 3: Maliyet
            decimal costUsd = _officialOpenAiReport != null && _officialOpenAiReport.TotalCostUsd > 0
                ? _officialOpenAiReport.TotalCostUsd
                : stats.TotalCostUsd;
            decimal costTry = _officialOpenAiReport != null && _officialOpenAiReport.TotalCostUsd > 0
                ? _officialOpenAiReport.TotalCostTry
                : stats.TotalCostTry;

            _lblCard3Title.Text = "💰 OPENAI RESMİ FATURA (CANLI)";
            _lblCard3Value.Text = $"${costUsd:N2} USD";
            _lblCard3Sub.Text = $"Yaklaşık {costTry:N2} TL (OpenAI Canlı Platform)";

            // Kart 4: İşlem Sayısı
            int reqCount = _officialOpenAiReport != null && _officialOpenAiReport.TotalRequests > 0
                ? _officialOpenAiReport.TotalRequests
                : stats.SuccessfulRequests;
            _lblCard4Title.Text = "🚨 KOTA & İŞLEM SAĞLIĞI";
            _lblCard4Value.Text = $"{reqCount} Başarılı Çağrı";
            _lblCard4Sub.Text = _officialOpenAiReport?.HasAdminKey == true
                ? "OpenAI organizasyon resmi verileri başarıyla çekildi"
                : "Tüm istekler başarıyla tamamlandı";
        }
        else
        {
            // Tümü (Genel Bakış)
            _lblCard1Title.Text = "💳 CANLI BAKİYE (DEEPSEEK)";
            if (_deepSeekBalance != null && _deepSeekBalance.IsAvailable)
            {
                _lblCard1Value.Text = $"${_deepSeekBalance.TotalBalanceUsd:N2} USD";
                _lblCard1Sub.Text = $"~{_deepSeekBalance.TotalBalanceTry():N0} TL kalan bakiye";
            }
            else
            {
                _lblCard1Value.Text = "Aktif";
                _lblCard1Sub.Text = "DeepSeek API hazır";
            }

            // Kart 2: Tüketilen Token (Yerel + OpenAI Resmi)
            long inTokens = stats.TotalPromptTokens + (_officialOpenAiReport?.TotalInputTokens ?? 0);
            long outTokens = stats.TotalCompletionTokens + (_officialOpenAiReport?.TotalOutputTokens ?? 0);
            long totTokens = stats.TotalTokens + (_officialOpenAiReport?.TotalTokens ?? 0);

            _lblCard2Title.Text = "⚡ TÜKETİLEN TOPLAM TOKEN";
            _lblCard2Value.Text = totTokens.ToString("N0");
            _lblCard2Sub.Text = $"Girdi: {inTokens:N0} | Çıkış: {outTokens:N0}";

            // Kart 3: Toplam Maliyet (Yerel + OpenAI Resmi)
            decimal totCostUsd = stats.TotalCostUsd + (_officialOpenAiReport?.TotalCostUsd ?? 0);
            decimal totCostTry = stats.TotalCostTry + (_officialOpenAiReport?.TotalCostTry ?? 0);

            _lblCard3Title.Text = "💰 TAHMİNİ TOPLAM FATURA";
            _lblCard3Value.Text = $"${totCostUsd:N2} USD";
            _lblCard3Sub.Text = $"Yaklaşık {totCostTry:N2} TL (Tüm Sağlayıcılar)";

            // Kart 4: İşlem Sayısı
            int totReqs = stats.SuccessfulRequests + (_officialOpenAiReport?.TotalRequests ?? 0);
            _lblCard4Title.Text = "🚨 KOTA & İŞLEM SAĞLIĞI";
            _lblCard4Value.Text = $"{totReqs} Başarılı İşlem";
            _lblCard4Sub.Text = stats.Blocked429Requests > 0
                ? $"⚠️ {stats.Blocked429Requests} İstek Kotaya Takıldı (429)!"
                : "Tüm sistemler sorunsuz çalışıyor";
        }
    }

    private void UpdateModelBreakdown(AiUsageSummaryStats stats, string selectedProvider)
    {
        _pnlModelBreakdown.Controls.Clear();
        var lbl = new Label
        {
            Text = "Model Dağılımı:",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(170, 180, 200),
            Margin = new Padding(0, 5, 10, 0)
        };
        _pnlModelBreakdown.Controls.Add(lbl);

        // OpenAI Resmi Verileri varsa rozetleri oluştur
        if (_officialOpenAiReport != null && _officialOpenAiReport.DailyItems.Count > 0 && (selectedProvider.Contains("OpenAI") || selectedProvider.StartsWith("Tümü")))
        {
            var officialGroups = _officialOpenAiReport.DailyItems
                .GroupBy(x => x.ServiceOrModel)
                .Select(g => new
                {
                    Model = g.Key,
                    Cost = g.Sum(x => x.CostUsd),
                    Input = g.Sum(x => x.InputTokens),
                    Output = g.Sum(x => x.OutputTokens),
                    Total = g.Sum(x => x.TotalTokens)
                })
                .OrderByDescending(x => x.Cost);

            foreach (var og in officialGroups)
            {
                var badge = new Label
                {
                    Text = $"{og.Model}: ${og.Cost:F2} USD (~{og.Cost * 40m:F2} ₺) [Girdi: {og.Input:N0} • Çıkış: {og.Output:N0}]",
                    AutoSize = true,
                    BackColor = Color.FromArgb(25, 45, 75),
                    ForeColor = Color.FromArgb(210, 230, 255),
                    Padding = new Padding(8, 4, 8, 4),
                    Margin = new Padding(0, 2, 8, 2),
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold)
                };
                _pnlModelBreakdown.Controls.Add(badge);
            }
        }

        // Gemini Resmi Hız Sınırları & Kota Rozetleri
        if (_geminiRateLimitReport != null && _geminiRateLimitReport.Models.Count > 0 && selectedProvider.Contains("Gemini"))
        {
            foreach (var gm in _geminiRateLimitReport.Models)
            {
                var badge = new Label
                {
                    Text = $"{gm.DisplayName}: {gm.PeakRpd}/{gm.LimitRpd} RPD • {gm.PeakRpm}/{gm.LimitRpm} RPM • {FormatTokens(gm.PeakTpm)} TPM",
                    AutoSize = true,
                    BackColor = gm.IsCritical ? Color.FromArgb(60, 20, 30) : Color.FromArgb(30, 45, 70),
                    ForeColor = gm.IsCritical ? Color.FromArgb(255, 120, 120) : Color.FromArgb(200, 225, 255),
                    Padding = new Padding(8, 4, 8, 4),
                    Margin = new Padding(0, 2, 8, 2),
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold)
                };
                _pnlModelBreakdown.Controls.Add(badge);
            }
        }

        // Yerel SQLite modelleri
        foreach (var (model, cost) in stats.CostByModel.OrderByDescending(x => x.Value))
        {
            var badge = new Label
            {
                Text = $"{model}: ${cost:F3} USD (~{cost * 40m:F2} ₺)",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 45, 65),
                ForeColor = Color.FromArgb(200, 220, 255),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F)
            };
            _pnlModelBreakdown.Controls.Add(badge);
        }
    }

    private static string FormatTokens(long val)
    {
        if (val >= 1_000_000) return $"{(val / 1_000_000.0):0.#}M";
        if (val >= 1_000) return $"{(val / 1_000.0):0.#}K";
        return val.ToString();
    }

    private void ExportCsv(object? sender, EventArgs e)
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Dosyası (*.csv)|*.csv",
                FileName = $"AI_Token_Harcama_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);
                sw.WriteLine("Tarih;Modül;Sağlayıcı;Model;Giriş Token;Çıkış Token;Toplam Token;Maliyet USD;Maliyet TL;Durum;Not");

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value?.ToString()?.Replace("\"", "\"\"")}\"");
                    sw.WriteLine(string.Join(";", cells));
                }

                MessageBox.Show(this, "AI kullanım ve harcama raporu başarıyla kaydedildi.", "Dışa Aktarma", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"CSV oluşturma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowAdminKeyDialog()
    {
        var settings = AiOptimizationSettingsStore.Load();

        using var promptForm = new Form
        {
            Text = "OpenAI Admin API Key Tanımla (Canlı Fatura)",
            Size = new Size(520, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.BackgroundColor,
            ForeColor = Color.White
        };

        var lblInfo = new Label
        {
            Text = "OpenAI Platform (platform.openai.com/settings/organization/admin-keys)\nhesabınızdan 'Read' yetkili bir Admin Key alarak buraya kaydedin:\n(Örnek: sk-admin-abcdef...)",
            Location = new Point(20, 15),
            Size = new Size(465, 55),
            Font = new Font("Segoe UI", 9F)
        };

        var txtKey = new TextBox
        {
            Text = settings.OpenAiAdminApiKey,
            Location = new Point(20, 80),
            Width = 460,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10F)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet ve Otomatik Getir",
            DialogResult = DialogResult.OK,
            Location = new Point(260, 130),
            Size = new Size(220, 34),
            BackColor = UiStyle.AiColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        promptForm.Controls.Add(lblInfo);
        promptForm.Controls.Add(txtKey);
        promptForm.Controls.Add(btnSave);
        promptForm.AcceptButton = btnSave;

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            settings.OpenAiAdminApiKey = txtKey.Text.Trim();
            AiOptimizationSettingsStore.Save(settings);
            _ = RefreshDataAsync(queryLiveBalance: true);
        }
    }

    private void ShowDeepSeekKeyDialog()
    {
        var settings = AiOptimizationSettingsStore.Load();

        using var promptForm = new Form
        {
            Text = "DeepSeek API Anahtarı Tanımla",
            Size = new Size(520, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.BackgroundColor,
            ForeColor = Color.White
        };

        var lblInfo = new Label
        {
            Text = "DeepSeek Platform (platform.deepseek.com/api_keys) hesabınızdan\naldığınız API anahtarını buraya girin (Örn: sk-...):\nCanlı bakiye ve aktif model doğrulaması anında yapılacaktır.",
            Location = new Point(20, 15),
            Size = new Size(465, 55),
            Font = new Font("Segoe UI", 9F)
        };

        var txtKey = new TextBox
        {
            Text = settings.DeepSeekApiKey,
            Location = new Point(20, 80),
            Width = 460,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10F)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet ve Doğrula",
            DialogResult = DialogResult.OK,
            Location = new Point(260, 130),
            Size = new Size(220, 34),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        promptForm.Controls.Add(lblInfo);
        promptForm.Controls.Add(txtKey);
        promptForm.Controls.Add(btnSave);
        promptForm.AcceptButton = btnSave;

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            settings.DeepSeekApiKey = txtKey.Text.Trim();
            AiOptimizationSettingsStore.Save(settings);
            _ = RefreshDataAsync(queryLiveBalance: true);
        }
    }

    private void ShowGeminiKeyDialog()
    {
        var settings = AiOptimizationSettingsStore.Load();

        using var promptForm = new Form
        {
            Text = "Google Gemini API Anahtarı Tanımla",
            Size = new Size(520, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.BackgroundColor,
            ForeColor = Color.White
        };

        var lblInfo = new Label
        {
            Text = "Google AI Studio (aistudio.google.com/app/apikey) sayfasından aldığınız\nGemini API anahtarını buraya girin (Örn: AIzaSy...):\nCanlı model doğrulaması ve kota kontrolü anında yapılacaktır.",
            Location = new Point(20, 15),
            Size = new Size(465, 55),
            Font = new Font("Segoe UI", 9F)
        };

        var txtKey = new TextBox
        {
            Text = settings.GeminiApiKey,
            Location = new Point(20, 80),
            Width = 460,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10F)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet ve Doğrula",
            DialogResult = DialogResult.OK,
            Location = new Point(260, 130),
            Size = new Size(220, 34),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        promptForm.Controls.Add(lblInfo);
        promptForm.Controls.Add(txtKey);
        promptForm.Controls.Add(btnSave);
        promptForm.AcceptButton = btnSave;

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            settings.GeminiApiKey = txtKey.Text.Trim();
            AiOptimizationSettingsStore.Save(settings);
            _ = RefreshDataAsync(queryLiveBalance: true);
        }
    }

    private void ShowApiKeysMenu(Button? anchor = null)
    {
        using var hub = new AiProviderHubDialog();
        hub.ShowDialog(this);
        _ = RefreshDataAsync(queryLiveBalance: true);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Web sayfası açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
