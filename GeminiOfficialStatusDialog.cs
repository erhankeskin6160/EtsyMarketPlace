namespace SimilarProductsWinForms;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class GeminiOfficialStatusDialog : Form
{
    private readonly Button _btnRefresh = new();
    private readonly Button _btnKey = new();
    private readonly Button _btnWeb = new();
    private readonly Button _btnBilling = new();
    private readonly Button _btnExport = new();

    private readonly Label _lblCard1Value = new();
    private readonly Label _lblCard1Sub = new();
    private readonly Label _lblCard2Value = new();
    private readonly Label _lblCard2Sub = new();
    private readonly Label _lblCard3Value = new();
    private readonly Label _lblCard3Sub = new();
    private readonly Label _lblCard4Value = new();
    private readonly Label _lblCard4Sub = new();

    private readonly FlowLayoutPanel _pnlModels = new();
    private readonly DataGridView _grid = new();
    private readonly Label _lblNotice = new();

    private AiProviderBalanceInfo? _currentStatus;

    public GeminiOfficialStatusDialog()
    {
        Text = "Google Gemini Canlı Durum, Kota & Tüketim Raporu";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1020, 680);
        MinimumSize = new Size(880, 580);
        BackColor = UiStyle.BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        Load += async (_, _) => await LoadDataAsync();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(18)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Buttons bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115)); // 4 KPI Cards (DPI güvenli)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Model badges (2 satır güvenli)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));  // Bottom Notice
        Controls.Add(mainLayout);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "🔵 Google Gemini Canlı Durum, Kota & Tüketim Merkezi",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 0)
        };
        var lblSubtitle = new Label
        {
            Text = "Google AI Studio API (generativelanguage.googleapis.com) canlı model doğrulaması, kota limitleri ve yerel token tüketim dökümü.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(160, 175, 200),
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(0, 26)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // 2. Buttons Bar
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnRefresh.Text = "🔄 Canlı Durum Sorgula";
        _btnRefresh.BackColor = Color.FromArgb(59, 130, 246);
        _btnRefresh.ForeColor = Color.White;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Height = 32;
        _btnRefresh.Width = 175;
        _btnRefresh.Cursor = Cursors.Hand;
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();

        _btnKey.Text = "🔑 API Anahtarı Tanımla";
        _btnKey.BackColor = Color.FromArgb(40, 50, 75);
        _btnKey.ForeColor = Color.White;
        _btnKey.FlatStyle = FlatStyle.Flat;
        _btnKey.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 120);
        _btnKey.Margin = new Padding(8, 0, 0, 0);
        _btnKey.Height = 32;
        _btnKey.Width = 175;
        _btnKey.Cursor = Cursors.Hand;
        _btnKey.Click += (_, _) => ShowKeyDialog();

        _btnWeb.Text = "↗ Google AI Studio (Web)";
        _btnWeb.BackColor = Color.FromArgb(30, 41, 59);
        _btnWeb.ForeColor = Color.FromArgb(210, 225, 255);
        _btnWeb.FlatStyle = FlatStyle.Flat;
        _btnWeb.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 100);
        _btnWeb.Margin = new Padding(8, 0, 0, 0);
        _btnWeb.Height = 32;
        _btnWeb.Width = 180;
        _btnWeb.Cursor = Cursors.Hand;
        _btnWeb.Click += (_, _) => OpenUrl("https://aistudio.google.com/app/apikey");

        _btnBilling.Text = "↗ GCP Billing (Web)";
        _btnBilling.BackColor = Color.FromArgb(30, 41, 59);
        _btnBilling.ForeColor = Color.FromArgb(210, 225, 255);
        _btnBilling.FlatStyle = FlatStyle.Flat;
        _btnBilling.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 100);
        _btnBilling.Margin = new Padding(8, 0, 0, 0);
        _btnBilling.Height = 32;
        _btnBilling.Width = 150;
        _btnBilling.Cursor = Cursors.Hand;
        _btnBilling.Click += (_, _) => OpenUrl("https://console.cloud.google.com/billing");

        var btnRateLimit = new Button
        {
            Text = "⚡ Hız Sınırları & Kota Takip (Rate Limit)",
            BackColor = Color.FromArgb(139, 92, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0),
            Height = 32,
            Width = 260,
            Cursor = Cursors.Hand
        };
        btnRateLimit.FlatAppearance.BorderSize = 0;
        btnRateLimit.Click += (_, _) =>
        {
            using var dlg = new GeminiRateLimitDialog();
            dlg.ShowDialog(this);
        };

        _btnExport.Text = "📥 CSV İndir";
        _btnExport.BackColor = Color.FromArgb(35, 45, 65);
        _btnExport.ForeColor = Color.White;
        _btnExport.FlatStyle = FlatStyle.Flat;
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Margin = new Padding(8, 0, 0, 0);
        _btnExport.Height = 32;
        _btnExport.Width = 110;
        _btnExport.Cursor = Cursors.Hand;
        _btnExport.Click += ExportCsv;

        pnlButtons.Controls.Add(_btnRefresh);
        pnlButtons.Controls.Add(_btnKey);
        pnlButtons.Controls.Add(_btnWeb);
        pnlButtons.Controls.Add(_btnBilling);
        pnlButtons.Controls.Add(btnRateLimit);
        pnlButtons.Controls.Add(_btnExport);
        mainLayout.Controls.Add(pnlButtons, 0, 1);

        // 3. KPI Cards
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        for (int i = 0; i < 4; i++) pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        pnlCards.Controls.Add(CreateCard("🔵 GEMINI API BAĞLANTISI", _lblCard1Value, _lblCard1Sub, Color.FromArgb(20, 35, 65)), 0, 0);
        pnlCards.Controls.Add(CreateCard("⚡ TÜKETİLEN TOPLAM TOKEN", _lblCard2Value, _lblCard2Sub, Color.FromArgb(25, 30, 55)), 1, 0);
        pnlCards.Controls.Add(CreateCard("💰 TAHMİNİ MALİYET / FATURA", _lblCard3Value, _lblCard3Sub, Color.FromArgb(20, 40, 45)), 2, 0);
        pnlCards.Controls.Add(CreateCard("🚨 KOTA & İŞLEM SAĞLIĞI", _lblCard4Value, _lblCard4Sub, Color.FromArgb(35, 25, 50)), 3, 0);
        mainLayout.Controls.Add(pnlCards, 0, 2);

        // 4. Model Badges Bar
        _pnlModels.Dock = DockStyle.Fill;
        _pnlModels.FlowDirection = FlowDirection.LeftToRight;
        _pnlModels.WrapContents = true;
        mainLayout.Controls.Add(_pnlModels, 0, 3);

        // 5. DataGridView
        ConfigureGrid();
        mainLayout.Controls.Add(_grid, 0, 4);

        // 6. Bottom Notice Bar
        var pnlBottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _lblNotice.AutoSize = true;
        _lblNotice.ForeColor = Color.FromArgb(160, 175, 200);
        _lblNotice.Font = new Font("Segoe UI", 8.5F);
        _lblNotice.Margin = new Padding(0, 6, 0, 0);
        pnlBottom.Controls.Add(_lblNotice);
        mainLayout.Controls.Add(pnlBottom, 0, 5);
    }

    private Control CreateCard(string title, Label lblVal, Label lblSub, Color bg)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(4)
        };
        var lblT = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(170, 185, 215),
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(10, 8)
        };
        lblVal.Text = "-";
        lblVal.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
        lblVal.ForeColor = Color.White;
        lblVal.AutoSize = true;
        lblVal.UseMnemonic = false;
        lblVal.Location = new Point(10, 32);

        lblSub.Text = "-";
        lblSub.Font = new Font("Segoe UI", 8F);
        lblSub.ForeColor = Color.FromArgb(160, 175, 205);
        lblSub.AutoSize = true;
        lblSub.UseMnemonic = false;
        lblSub.Location = new Point(10, 68);

        panel.Controls.Add(lblT);
        panel.Controls.Add(lblVal);
        panel.Controls.Add(lblSub);
        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(15, 23, 42);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(30, 41, 59);
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowTemplate.Height = 30;

        _grid.DefaultCellStyle.BackColor = Color.FromArgb(18, 26, 46);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 60, 95);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(200, 215, 240);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 34;
        _grid.EnableHeadersVisualStyles = false;

        _grid.Columns.Add("Date", "Tarih/Saat");
        _grid.Columns.Add("Module", "Modül");
        _grid.Columns.Add("Model", "Model");
        _grid.Columns.Add("InputTokens", "Girdi Token");
        _grid.Columns.Add("OutputTokens", "Çıktı Token");
        _grid.Columns.Add("TotalTokens", "Toplam Token");
        _grid.Columns.Add("CostUsd", "Maliyet ($)");
        _grid.Columns.Add("CostTry", "Maliyet (₺)");
        _grid.Columns.Add("Status", "Durum");
        _grid.Columns.Add("Note", "Detay");

        _grid.Columns["Date"].Width = 145;
        _grid.Columns["Module"].Width = 140;
        _grid.Columns["Model"].Width = 135;
        _grid.Columns["InputTokens"].Width = 95;
        _grid.Columns["OutputTokens"].Width = 95;
        _grid.Columns["TotalTokens"].Width = 100;
        _grid.Columns["CostUsd"].Width = 85;
        _grid.Columns["CostTry"].Width = 85;
        _grid.Columns["Status"].Width = 90;
        _grid.Columns["Note"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    public async Task LoadDataAsync()
    {
        UseWaitCursor = true;
        _btnRefresh.Enabled = false;

        try
        {
            var settings = AiOptimizationSettingsStore.Load();

            if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                _lblCard1Value.Text = "Tanımlanmadı";
                _lblCard1Sub.Text = "Google Gemini API anahtarı girilmedi";
                _lblCard2Value.Text = "0 Token";
                _lblCard2Sub.Text = "Girdi: 0 | Çıktı: 0";
                _lblCard3Value.Text = "$0.00 USD";
                _lblCard3Sub.Text = "Harcama yok";
                _lblCard4Value.Text = "Bağlantı Yok";
                _lblCard4Sub.Text = "generativelanguage.googleapis.com";
                _lblNotice.Text = "💡 Google Gemini'yi kullanmak için lütfen 'API Anahtarı Tanımla' butonuna tıklayarak AI Studio anahtarınızı girin.";
                return;
            }

            // Canlı Model ve Durum Sorgusu
            var status = await AiBalanceCheckerService.CheckGeminiStatusAsync(settings.GeminiApiKey);
            _currentStatus = status;

            // Yerel SQLite geçmişindeki Gemini işlemleri ve özet istatistikler
            var repo = AiTokenUsageTrackerService.GetRepository();
            var records = await repo.GetHistoryAsync(providerFilter: "Gemini", limit: 300);
            var stats = await repo.GetSummaryStatsAsync(providerFilter: "Gemini");

            // Kart 1: API Durumu & Maskeli Anahtar
            int modelCount = status.AvailableModels?.Count ?? 0;
            _lblCard1Value.Text = !string.IsNullOrWhiteSpace(status.MaskedApiKey) ? status.MaskedApiKey : "Aktif";
            _lblCard1Sub.Text = status.IsAvailable
                ? (modelCount > 0 ? $"✅ Google Gemini Aktif ({modelCount} Model)" : "✅ Google Gemini Aktif")
                : $"⚠️ {status.StatusMessage}";

            // Kart 2: Tüketilen Token (Girdi & Çıktı)
            _lblCard2Value.Text = stats.TotalTokens.ToString("N0");
            _lblCard2Sub.Text = $"Girdi: {stats.TotalPromptTokens:N0} | Çıkış: {stats.TotalCompletionTokens:N0}";

            // Kart 3: Tahmini Maliyet (USD & TL)
            _lblCard3Value.Text = stats.TotalCostUsd == 0 ? "$0.00 USD" : $"${stats.TotalCostUsd:F4} USD";
            _lblCard3Sub.Text = $"Yaklaşık {stats.TotalCostTry:N2} TL (Resmi Tarife)";

            // Kart 4: Kota & İşlem Sağlığı
            _lblCard4Value.Text = $"{stats.SuccessfulRequests} Başarılı";
            _lblCard4Sub.Text = stats.Blocked429Requests > 0
                ? $"⚠️ {stats.Blocked429Requests} İstek Kotaya Takıldı (429)!"
                : (status.IsAvailable ? "Ücretsiz Plan: 15 RPM / 1,500 RPD Hazır" : "API bağlantı hatası");

            // Model Rozetleri Barı
            _pnlModels.Controls.Clear();
            var lblMLabel = new Label
            {
                Text = "Model Tarifeleri & Limitler:",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(170, 185, 210),
                Margin = new Padding(0, 5, 8, 0)
            };
            _pnlModels.Controls.Add(lblMLabel);

            var badgeFlash2 = new Label
            {
                Text = "gemini-2.0-flash: $0.10 / $0.40 per 1M (1M Context)",
                AutoSize = true,
                BackColor = Color.FromArgb(20, 35, 65),
                ForeColor = Color.FromArgb(200, 225, 255),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgeFlash2);

            var badgeFlash15 = new Label
            {
                Text = "gemini-1.5-flash: $0.075 / $0.30 per 1M (1M Context)",
                AutoSize = true,
                BackColor = Color.FromArgb(25, 45, 55),
                ForeColor = Color.FromArgb(190, 245, 235),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgeFlash15);

            var badgePro = new Label
            {
                Text = "gemini-1.5-pro: $1.25 / $5.00 per 1M (2M Context)",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 25, 60),
                ForeColor = Color.FromArgb(235, 205, 255),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgePro);

            var badgeFree = new Label
            {
                Text = "Ücretsiz Plan: 15 RPM / 1,500 RPD / 1M TPM (Ücretsiz)",
                AutoSize = true,
                BackColor = Color.FromArgb(20, 45, 30),
                ForeColor = Color.FromArgb(180, 255, 200),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgeFree);

            // Tablo: Yerel SQLite geçmişindeki Gemini işlemleri
            _grid.Rows.Clear();
            if (records.Count == 0)
            {
                _grid.Rows.Add(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    "Bilgilendirme",
                    "Google Gemini",
                    "0",
                    "0",
                    "0",
                    "$0.00",
                    "0.00 ₺",
                    status.IsAvailable ? "Hazır" : "Tanımlanmadı",
                    status.IsAvailable
                        ? "API bağlantısı kuruldu. Optimizasyon veya kategori analizi yapıldığında tüketilen gerçek tokenlar burada listelenecektir."
                        : status.StatusMessage
                );
            }
            else
            {
                foreach (var r in records)
                {
                    _grid.Rows.Add(
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        r.ModuleName,
                        r.ModelName,
                        r.PromptTokens.ToString("N0"),
                        r.CompletionTokens.ToString("N0"),
                        r.TotalTokens.ToString("N0"),
                        $"${r.EstimatedCostUsd:F5}",
                        $"{r.EstimatedCostTry:F4} ₺",
                        r.Status,
                        r.Note ?? ""
                    );
                }
            }

            _lblNotice.Text = $"✅ Canlı API sorgusu başarılı ({DateTime.Now:HH:mm:ss}). Google üzerinde {modelCount} model aktif. Toplam {records.Count} yerel işlem listelendi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Google Gemini durum sorgulama hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnRefresh.Enabled = true;
        }
    }

    private void ShowKeyDialog()
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
            Text = "Google AI Studio (aistudio.google.com/app/apikey) sayfasından aldığınız\nGemini API anahtarını buraya yapıştırın:\n(Örnek: AIzaSyxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)",
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
            _ = LoadDataAsync();
        }
    }

    private void ExportCsv(object? sender, EventArgs e)
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Dosyası (*.csv)|*.csv",
                FileName = $"Gemini_Kullanim_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);

                var headers = _grid.Columns.Cast<DataGridViewColumn>().Select(c => $"\"{c.HeaderText}\"");
                sw.WriteLine(string.Join(";", headers));

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value?.ToString()?.Replace("\"", "\"\"")}\"");
                    sw.WriteLine(string.Join(";", cells));
                }

                MessageBox.Show(this, "Gemini kullanım ve tüketim tablosu CSV olarak kaydedildi.", "Dışa Aktarma Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"CSV oluşturma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
