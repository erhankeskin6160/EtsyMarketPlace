namespace SimilarProductsWinForms;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class AiProviderHubDialog : Form
{
    private readonly Button _btnRefreshAll = new();
    private readonly Button _btnGlobalSettings = new();
    private readonly Label _lblStatusNotice = new();

    // OpenAI Card Controls
    private readonly Label _lblOpenAiPill = new();
    private readonly Label _lblOpenAiUsage = new();
    private readonly TextBox _txtOpenAiKey = new();
    private readonly Button _btnSaveOpenAi = new();
    private readonly Button _btnOpenAiReport = new();
    private readonly Button _btnOpenAiWeb = new();

    // DeepSeek Card Controls
    private readonly Label _lblDeepSeekPill = new();
    private readonly Label _lblDeepSeekBalance = new();
    private readonly TextBox _txtDeepSeekKey = new();
    private readonly Button _btnSaveDeepSeek = new();
    private readonly Button _btnDeepSeekReport = new();
    private readonly Button _btnDeepSeekWeb = new();

    // Gemini Card Controls
    private readonly Label _lblGeminiPill = new();
    private readonly Label _lblGeminiQuota = new();
    private readonly TextBox _txtGeminiKey = new();
    private readonly Button _btnSaveGemini = new();
    private readonly Button _btnGeminiWeb = new();

    public AiProviderHubDialog()
    {
        Text = "Yapay Zeka Sağlayıcı & API Yönetim Merkezi";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1060, 640);
        MinimumSize = new Size(960, 580);
        BackColor = Color.FromArgb(15, 23, 42); // #0f172a
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        Load += async (_, _) => await LoadAllProvidersAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22, 18, 22, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 3 Provider Cards
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Bottom Action Bar
        Controls.Add(root);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "⚡ Yapay Zeka Sağlayıcı & API Yönetim Merkezi",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 2)
        };
        var lblSubtitle = new Label
        {
            Text = "OpenAI, DeepSeek ve Google Gemini API anahtarları, anlık bakiye durumları ve canlı tüketim konsolu.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(150, 165, 195),
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 30)
        };

        _btnRefreshAll.Text = "🔄 Tümünü Doğrula & Yenile";
        _btnRefreshAll.BackColor = Color.FromArgb(30, 41, 59);
        _btnRefreshAll.ForeColor = Color.White;
        _btnRefreshAll.FlatStyle = FlatStyle.Flat;
        _btnRefreshAll.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 100);
        _btnRefreshAll.Cursor = Cursors.Hand;
        _btnRefreshAll.Height = 34;
        _btnRefreshAll.Width = 200;
        _btnRefreshAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnRefreshAll.Location = new Point(root.Width - 250, 8);
        _btnRefreshAll.Click += async (_, _) => await LoadAllProvidersAsync();

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        pnlHeader.Controls.Add(_btnRefreshAll);
        root.Controls.Add(pnlHeader, 0, 0);

        // 2. 3 Cards Table Layout
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 8)
        };
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        pnlCards.Controls.Add(BuildOpenAiCard(), 0, 0);
        pnlCards.Controls.Add(BuildDeepSeekCard(), 1, 0);
        pnlCards.Controls.Add(BuildGeminiCard(), 2, 0);
        root.Controls.Add(pnlCards, 0, 1);

        // 3. Bottom Action Bar
        var pnlBottom = new Panel { Dock = DockStyle.Fill };

        _btnGlobalSettings.Text = "⚙️ Gelişmiş Ayarlar (Tüm Yapay Zeka Parametreleri)";
        _btnGlobalSettings.BackColor = Color.FromArgb(30, 41, 59);
        _btnGlobalSettings.ForeColor = Color.FromArgb(200, 215, 245);
        _btnGlobalSettings.FlatStyle = FlatStyle.Flat;
        _btnGlobalSettings.FlatAppearance.BorderColor = Color.FromArgb(55, 68, 90);
        _btnGlobalSettings.Height = 34;
        _btnGlobalSettings.Width = 330;
        _btnGlobalSettings.Cursor = Cursors.Hand;
        _btnGlobalSettings.Location = new Point(0, 8);
        _btnGlobalSettings.Click += async (_, _) =>
        {
            using var dlg = new AiOptimizationSettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                await LoadAllProvidersAsync();
            }
        };

        _lblStatusNotice.AutoSize = true;
        _lblStatusNotice.ForeColor = Color.FromArgb(140, 155, 180);
        _lblStatusNotice.Font = new Font("Segoe UI", 8.5F);
        _lblStatusNotice.Location = new Point(350, 17);

        var btnClose = new Button
        {
            Text = "Kapat",
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(35, 45, 65),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 34,
            Width = 110,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(root.Width - 160, 8)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        AcceptButton = btnClose;

        pnlBottom.Controls.Add(_btnGlobalSettings);
        pnlBottom.Controls.Add(_lblStatusNotice);
        pnlBottom.Controls.Add(btnClose);
        root.Controls.Add(pnlBottom, 0, 2);
    }

    private Control BuildOpenAiCard()
    {
        var card = CreateCardContainer(Color.FromArgb(22, 32, 50), Color.FromArgb(35, 50, 75));

        // Header: Logo / Title + Pill
        var lblHeader = new Label
        {
            Text = "🟢 OpenAI Platform",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(16, 14)
        };

        _lblOpenAiPill.Text = "Kontrol Ediliyor...";
        _lblOpenAiPill.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _lblOpenAiPill.ForeColor = Color.FromArgb(110, 231, 183);
        _lblOpenAiPill.BackColor = Color.FromArgb(16, 45, 35);
        _lblOpenAiPill.Padding = new Padding(8, 3, 8, 3);
        _lblOpenAiPill.AutoSize = true;
        _lblOpenAiPill.Location = new Point(16, 44);

        // Metric
        var lblMetricTitle = new Label
        {
            Text = "Canlı Kullanım / Fatura:",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 78)
        };

        _lblOpenAiUsage.Text = "-";
        _lblOpenAiUsage.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblOpenAiUsage.ForeColor = Color.White;
        _lblOpenAiUsage.AutoSize = true;
        _lblOpenAiUsage.Location = new Point(16, 96);

        // Input Box
        var lblKeyPrompt = new Label
        {
            Text = "API Anahtarı (sk-... veya sk-admin-...):",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 145)
        };

        _txtOpenAiKey.Location = new Point(16, 166);
        _txtOpenAiKey.Width = 230;
        _txtOpenAiKey.BackColor = Color.FromArgb(15, 23, 42);
        _txtOpenAiKey.ForeColor = Color.White;
        _txtOpenAiKey.Font = new Font("Consolas", 9F);
        _txtOpenAiKey.PasswordChar = '•';

        _btnSaveOpenAi.Text = "Kaydet";
        _btnSaveOpenAi.Location = new Point(252, 164);
        _btnSaveOpenAi.Size = new Size(58, 25);
        _btnSaveOpenAi.BackColor = Color.FromArgb(35, 50, 75);
        _btnSaveOpenAi.ForeColor = Color.White;
        _btnSaveOpenAi.FlatStyle = FlatStyle.Flat;
        _btnSaveOpenAi.FlatAppearance.BorderSize = 0;
        _btnSaveOpenAi.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _btnSaveOpenAi.Cursor = Cursors.Hand;
        _btnSaveOpenAi.Click += async (_, _) =>
        {
            var s = AiOptimizationSettingsStore.Load();
            s.OpenAiApiKey = _txtOpenAiKey.Text.Trim();
            if (s.OpenAiApiKey.StartsWith("sk-admin-")) s.OpenAiAdminApiKey = s.OpenAiApiKey;
            AiOptimizationSettingsStore.Save(s);
            await LoadAllProvidersAsync();
        };

        // Actions
        _btnOpenAiReport.Text = "📊 Canlı Fatura & Döküm (Pencere)";
        _btnOpenAiReport.Location = new Point(16, 215);
        _btnOpenAiReport.Size = new Size(294, 34);
        _btnOpenAiReport.BackColor = Color.FromArgb(16, 185, 129); // Emerald
        _btnOpenAiReport.ForeColor = Color.White;
        _btnOpenAiReport.FlatStyle = FlatStyle.Flat;
        _btnOpenAiReport.FlatAppearance.BorderSize = 0;
        _btnOpenAiReport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _btnOpenAiReport.Cursor = Cursors.Hand;
        _btnOpenAiReport.Click += async (_, _) =>
        {
            using var dlg = new OpenAiOfficialUsageDialog();
            dlg.ShowDialog(this);
            await LoadAllProvidersAsync();
        };

        _btnOpenAiWeb.Text = "↗ OpenAI Platform (Web)";
        _btnOpenAiWeb.Location = new Point(16, 258);
        _btnOpenAiWeb.Size = new Size(294, 32);
        _btnOpenAiWeb.BackColor = Color.FromArgb(28, 40, 60);
        _btnOpenAiWeb.ForeColor = Color.FromArgb(200, 220, 255);
        _btnOpenAiWeb.FlatStyle = FlatStyle.Flat;
        _btnOpenAiWeb.FlatAppearance.BorderColor = Color.FromArgb(50, 70, 100);
        _btnOpenAiWeb.Font = new Font("Segoe UI", 8.5F);
        _btnOpenAiWeb.Cursor = Cursors.Hand;
        _btnOpenAiWeb.Click += (_, _) => OpenUrl("https://platform.openai.com/usage");

        card.Controls.Add(lblHeader);
        card.Controls.Add(_lblOpenAiPill);
        card.Controls.Add(lblMetricTitle);
        card.Controls.Add(_lblOpenAiUsage);
        card.Controls.Add(lblKeyPrompt);
        card.Controls.Add(_txtOpenAiKey);
        card.Controls.Add(_btnSaveOpenAi);
        card.Controls.Add(_btnOpenAiReport);
        card.Controls.Add(_btnOpenAiWeb);

        return card;
    }

    private Control BuildDeepSeekCard()
    {
        var card = CreateCardContainer(Color.FromArgb(20, 32, 55), Color.FromArgb(30, 50, 85));

        var lblHeader = new Label
        {
            Text = "🔵 DeepSeek API",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(16, 14)
        };

        _lblDeepSeekPill.Text = "Kontrol Ediliyor...";
        _lblDeepSeekPill.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _lblDeepSeekPill.ForeColor = Color.FromArgb(125, 211, 252);
        _lblDeepSeekPill.BackColor = Color.FromArgb(15, 45, 65);
        _lblDeepSeekPill.Padding = new Padding(8, 3, 8, 3);
        _lblDeepSeekPill.AutoSize = true;
        _lblDeepSeekPill.Location = new Point(16, 44);

        var lblMetricTitle = new Label
        {
            Text = "Canlı Hesap Bakiyesi:",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 78)
        };

        _lblDeepSeekBalance.Text = "-";
        _lblDeepSeekBalance.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblDeepSeekBalance.ForeColor = Color.White;
        _lblDeepSeekBalance.AutoSize = true;
        _lblDeepSeekBalance.Location = new Point(16, 96);

        var lblKeyPrompt = new Label
        {
            Text = "DeepSeek API Anahtarı (sk-...):",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 145)
        };

        _txtDeepSeekKey.Location = new Point(16, 166);
        _txtDeepSeekKey.Width = 230;
        _txtDeepSeekKey.BackColor = Color.FromArgb(15, 23, 42);
        _txtDeepSeekKey.ForeColor = Color.White;
        _txtDeepSeekKey.Font = new Font("Consolas", 9F);
        _txtDeepSeekKey.PasswordChar = '•';

        _btnSaveDeepSeek.Text = "Kaydet";
        _btnSaveDeepSeek.Location = new Point(252, 164);
        _btnSaveDeepSeek.Size = new Size(58, 25);
        _btnSaveDeepSeek.BackColor = Color.FromArgb(35, 55, 85);
        _btnSaveDeepSeek.ForeColor = Color.White;
        _btnSaveDeepSeek.FlatStyle = FlatStyle.Flat;
        _btnSaveDeepSeek.FlatAppearance.BorderSize = 0;
        _btnSaveDeepSeek.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _btnSaveDeepSeek.Cursor = Cursors.Hand;
        _btnSaveDeepSeek.Click += async (_, _) =>
        {
            var s = AiOptimizationSettingsStore.Load();
            s.DeepSeekApiKey = _txtDeepSeekKey.Text.Trim();
            AiOptimizationSettingsStore.Save(s);
            await LoadAllProvidersAsync();
        };

        _btnDeepSeekReport.Text = "📊 Canlı Bakiye & Model Raporu";
        _btnDeepSeekReport.Location = new Point(16, 215);
        _btnDeepSeekReport.Size = new Size(294, 34);
        _btnDeepSeekReport.BackColor = Color.FromArgb(2, 132, 199); // Sky Blue
        _btnDeepSeekReport.ForeColor = Color.White;
        _btnDeepSeekReport.FlatStyle = FlatStyle.Flat;
        _btnDeepSeekReport.FlatAppearance.BorderSize = 0;
        _btnDeepSeekReport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _btnDeepSeekReport.Cursor = Cursors.Hand;
        _btnDeepSeekReport.Click += async (_, _) =>
        {
            using var dlg = new DeepSeekOfficialBalanceDialog();
            dlg.ShowDialog(this);
            await LoadAllProvidersAsync();
        };

        _btnDeepSeekWeb.Text = "↗ DeepSeek Platform (Web)";
        _btnDeepSeekWeb.Location = new Point(16, 258);
        _btnDeepSeekWeb.Size = new Size(294, 32);
        _btnDeepSeekWeb.BackColor = Color.FromArgb(25, 40, 65);
        _btnDeepSeekWeb.ForeColor = Color.FromArgb(200, 225, 255);
        _btnDeepSeekWeb.FlatStyle = FlatStyle.Flat;
        _btnDeepSeekWeb.FlatAppearance.BorderColor = Color.FromArgb(45, 70, 105);
        _btnDeepSeekWeb.Font = new Font("Segoe UI", 8.5F);
        _btnDeepSeekWeb.Cursor = Cursors.Hand;
        _btnDeepSeekWeb.Click += (_, _) => OpenUrl("https://platform.deepseek.com");

        card.Controls.Add(lblHeader);
        card.Controls.Add(_lblDeepSeekPill);
        card.Controls.Add(lblMetricTitle);
        card.Controls.Add(_lblDeepSeekBalance);
        card.Controls.Add(lblKeyPrompt);
        card.Controls.Add(_txtDeepSeekKey);
        card.Controls.Add(_btnSaveDeepSeek);
        card.Controls.Add(_btnDeepSeekReport);
        card.Controls.Add(_btnDeepSeekWeb);

        return card;
    }

    private Control BuildGeminiCard()
    {
        var card = CreateCardContainer(Color.FromArgb(26, 25, 52), Color.FromArgb(45, 38, 80));

        var lblHeader = new Label
        {
            Text = "🟣 Google Gemini",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(16, 14)
        };

        _lblGeminiPill.Text = "Kontrol Ediliyor...";
        _lblGeminiPill.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _lblGeminiPill.ForeColor = Color.FromArgb(216, 180, 254);
        _lblGeminiPill.BackColor = Color.FromArgb(45, 25, 65);
        _lblGeminiPill.Padding = new Padding(8, 3, 8, 3);
        _lblGeminiPill.AutoSize = true;
        _lblGeminiPill.Location = new Point(16, 44);

        var lblMetricTitle = new Label
        {
            Text = "Model & Kota Durumu:",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 78)
        };

        _lblGeminiQuota.Text = "-";
        _lblGeminiQuota.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _lblGeminiQuota.ForeColor = Color.White;
        _lblGeminiQuota.AutoSize = true;
        _lblGeminiQuota.Location = new Point(16, 96);

        var lblKeyPrompt = new Label
        {
            Text = "Gemini API Anahtarı (AIzaSy...):",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 165, 190),
            AutoSize = true,
            Location = new Point(16, 145)
        };

        _txtGeminiKey.Location = new Point(16, 166);
        _txtGeminiKey.Width = 230;
        _txtGeminiKey.BackColor = Color.FromArgb(15, 23, 42);
        _txtGeminiKey.ForeColor = Color.White;
        _txtGeminiKey.Font = new Font("Consolas", 9F);
        _txtGeminiKey.PasswordChar = '•';

        _btnSaveGemini.Text = "Kaydet";
        _btnSaveGemini.Location = new Point(252, 164);
        _btnSaveGemini.Size = new Size(58, 25);
        _btnSaveGemini.BackColor = Color.FromArgb(50, 40, 85);
        _btnSaveGemini.ForeColor = Color.White;
        _btnSaveGemini.FlatStyle = FlatStyle.Flat;
        _btnSaveGemini.FlatAppearance.BorderSize = 0;
        _btnSaveGemini.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _btnSaveGemini.Cursor = Cursors.Hand;
        _btnSaveGemini.Click += async (_, _) =>
        {
            var s = AiOptimizationSettingsStore.Load();
            s.GeminiApiKey = _txtGeminiKey.Text.Trim();
            AiOptimizationSettingsStore.Save(s);
            await LoadAllProvidersAsync();
        };

        _btnGeminiWeb.Text = "↗ Google AI Studio (Web Paneli)";
        _btnGeminiWeb.Location = new Point(16, 215);
        _btnGeminiWeb.Size = new Size(294, 36);
        _btnGeminiWeb.BackColor = Color.FromArgb(35, 30, 60);
        _btnGeminiWeb.ForeColor = Color.FromArgb(220, 205, 255);
        _btnGeminiWeb.FlatStyle = FlatStyle.Flat;
        _btnGeminiWeb.FlatAppearance.BorderColor = Color.FromArgb(65, 55, 100);
        _btnGeminiWeb.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _btnGeminiWeb.Cursor = Cursors.Hand;
        _btnGeminiWeb.Click += (_, _) => OpenUrl("https://aistudio.google.com/app/rate-limit?timeRange=last-28-days");

        card.Controls.Add(lblHeader);
        card.Controls.Add(_lblGeminiPill);
        card.Controls.Add(lblMetricTitle);
        card.Controls.Add(_lblGeminiQuota);
        card.Controls.Add(lblKeyPrompt);
        card.Controls.Add(_txtGeminiKey);
        card.Controls.Add(_btnSaveGemini);
        card.Controls.Add(_btnGeminiWeb);

        return card;
    }

    private Panel CreateCardContainer(Color bg, Color borderColor)
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg,
            Margin = new Padding(8, 6, 8, 6),
            Padding = new Padding(12)
        };
    }

    private async Task LoadAllProvidersAsync()
    {
        UseWaitCursor = true;
        _btnRefreshAll.Enabled = false;
        _lblStatusNotice.Text = "Tüm sağlayıcıların bağlantıları test ediliyor...";

        try
        {
            var settings = AiOptimizationSettingsStore.Load();

            _txtOpenAiKey.Text = settings.OpenAiApiKey;
            _txtDeepSeekKey.Text = settings.DeepSeekApiKey;
            _txtGeminiKey.Text = settings.GeminiApiKey;

            // 1. OpenAI
            if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
            {
                _lblOpenAiPill.Text = "Tanımlanmadı";
                _lblOpenAiPill.ForeColor = Color.FromArgb(248, 113, 113);
                _lblOpenAiPill.BackColor = Color.FromArgb(45, 20, 25);
                _lblOpenAiUsage.Text = "$0.00 USD";
            }
            else
            {
                var report = await OpenAiUsageFetcherService.FetchCurrentMonthUsageAsync(
                    settings.OpenAiApiKey,
                    settings.OpenAiAdminApiKey);

                if (report.HasAdminKey)
                {
                    _lblOpenAiPill.Text = "Aktif (Canlı Fatura)";
                    _lblOpenAiPill.ForeColor = Color.FromArgb(110, 231, 183);
                    _lblOpenAiPill.BackColor = Color.FromArgb(16, 45, 35);
                    _lblOpenAiUsage.Text = $"${report.TotalCostUsd:N2} USD ({report.TotalTokens:N0} Token)";
                }
                else
                {
                    var status = await AiBalanceCheckerService.CheckOpenAiStatusAsync(settings.OpenAiApiKey);
                    _lblOpenAiPill.Text = status.IsAvailable ? "Aktif (Standart Key)" : "Geçersiz Key";
                    _lblOpenAiPill.ForeColor = status.IsAvailable ? Color.FromArgb(110, 231, 183) : Color.FromArgb(248, 113, 113);
                    _lblOpenAiPill.BackColor = status.IsAvailable ? Color.FromArgb(16, 45, 35) : Color.FromArgb(45, 20, 25);
                    _lblOpenAiUsage.Text = status.IsAvailable ? "API Bağlantısı Hazır" : "Hata / Kota";
                }
            }

            // 2. DeepSeek
            if (string.IsNullOrWhiteSpace(settings.DeepSeekApiKey))
            {
                _lblDeepSeekPill.Text = "Tanımlanmadı";
                _lblDeepSeekPill.ForeColor = Color.FromArgb(248, 113, 113);
                _lblDeepSeekPill.BackColor = Color.FromArgb(45, 20, 25);
                _lblDeepSeekBalance.Text = "$0.00 USD";
            }
            else
            {
                var dsBalance = await AiBalanceCheckerService.CheckDeepSeekBalanceAsync(settings.DeepSeekApiKey);
                if (dsBalance.IsAvailable)
                {
                    _lblDeepSeekPill.Text = "Aktif (Canlı Bakiye)";
                    _lblDeepSeekPill.ForeColor = Color.FromArgb(125, 211, 252);
                    _lblDeepSeekPill.BackColor = Color.FromArgb(15, 45, 65);

                    if (dsBalance.BalanceCny.HasValue && dsBalance.Currency == "CNY")
                    {
                        _lblDeepSeekBalance.Text = $"¥{dsBalance.BalanceCny.Value:N2} CNY (~${dsBalance.TotalBalanceUsd:N2})";
                    }
                    else
                    {
                        _lblDeepSeekBalance.Text = $"${dsBalance.TotalBalanceUsd:N2} USD (~{dsBalance.TotalBalanceTry():N0} ₺)";
                    }
                }
                else
                {
                    _lblDeepSeekPill.Text = "Bakiye Yok / Askıda";
                    _lblDeepSeekPill.ForeColor = Color.FromArgb(248, 113, 113);
                    _lblDeepSeekPill.BackColor = Color.FromArgb(45, 20, 25);
                    _lblDeepSeekBalance.Text = "Bakiye Alınamadı";
                }
            }

            // 3. Gemini
            if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                _lblGeminiPill.Text = "Tanımlanmadı";
                _lblGeminiPill.ForeColor = Color.FromArgb(248, 113, 113);
                _lblGeminiPill.BackColor = Color.FromArgb(45, 20, 25);
                _lblGeminiQuota.Text = "Anahtar Girilmedi";
            }
            else
            {
                var geminiStatus = await AiBalanceCheckerService.CheckGeminiStatusAsync(settings.GeminiApiKey);
                if (geminiStatus.IsAvailable)
                {
                    int mCount = geminiStatus.AvailableModels?.Count ?? 0;
                    _lblGeminiPill.Text = mCount > 0 ? $"{mCount} Model Aktif" : "API Aktif";
                    _lblGeminiPill.ForeColor = Color.FromArgb(216, 180, 254);
                    _lblGeminiPill.BackColor = Color.FromArgb(45, 25, 65);
                    _lblGeminiQuota.Text = "15 RPM / 1,500 RPD Hazır";
                }
                else
                {
                    _lblGeminiPill.Text = "Bağlantı Hatası";
                    _lblGeminiPill.ForeColor = Color.FromArgb(248, 113, 113);
                    _lblGeminiPill.BackColor = Color.FromArgb(45, 20, 25);
                    _lblGeminiQuota.Text = geminiStatus.StatusMessage;
                }
            }

            _lblStatusNotice.Text = $"✅ Canlı kontrol tamamlandı ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            _lblStatusNotice.Text = $"⚠️ Kontrol hatası: {ex.Message}";
        }
        finally
        {
            UseWaitCursor = false;
            _btnRefreshAll.Enabled = true;
        }
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
        catch { }
    }
}
