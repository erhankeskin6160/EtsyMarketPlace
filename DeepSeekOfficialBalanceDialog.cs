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

public sealed class DeepSeekOfficialBalanceDialog : Form
{
    private readonly Button _btnRefresh = new();
    private readonly Button _btnKey = new();
    private readonly Button _btnWeb = new();
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

    private AiProviderBalanceInfo? _currentBalance;

    public DeepSeekOfficialBalanceDialog()
    {
        Text = "DeepSeek Resmi Bakiye, Model & Tüketim Raporu";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 680);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // 4 KPI Cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Model badges
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));  // Bottom Notice
        Controls.Add(mainLayout);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "🔴 DeepSeek Canlı Bakiye, Model & Harcama Merkezi",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        };
        var lblSubtitle = new Label
        {
            Text = "DeepSeek resmi platform hesabı (api.deepseek.com) anlık bakiye sorgusu, aktif modeller ve yerel token tüketim dökümü.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(160, 175, 200),
            AutoSize = true,
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

        _btnRefresh.Text = "🔄 Canlı Bakiye Sorgula";
        _btnRefresh.BackColor = UiStyle.AiColor;
        _btnRefresh.ForeColor = Color.White;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Height = 32;
        _btnRefresh.Width = 175;
        _btnRefresh.Cursor = Cursors.Hand;
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();

        _btnKey.Text = "🔑 API Anahtarı Tanımla / Değiştir";
        _btnKey.BackColor = Color.FromArgb(40, 50, 75);
        _btnKey.ForeColor = Color.White;
        _btnKey.FlatStyle = FlatStyle.Flat;
        _btnKey.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 120);
        _btnKey.Margin = new Padding(10, 0, 0, 0);
        _btnKey.Height = 32;
        _btnKey.Width = 220;
        _btnKey.Cursor = Cursors.Hand;
        _btnKey.Click += (_, _) => ShowKeyDialog();

        _btnWeb.Text = "🌐 DeepSeek Platform (Web)";
        _btnWeb.BackColor = Color.FromArgb(30, 41, 59);
        _btnWeb.ForeColor = Color.FromArgb(210, 225, 255);
        _btnWeb.FlatStyle = FlatStyle.Flat;
        _btnWeb.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 100);
        _btnWeb.Margin = new Padding(10, 0, 0, 0);
        _btnWeb.Height = 32;
        _btnWeb.Width = 190;
        _btnWeb.Cursor = Cursors.Hand;
        _btnWeb.Click += (_, _) => OpenUrl("https://platform.deepseek.com");

        _btnExport.Text = "📥 CSV İndir";
        _btnExport.BackColor = Color.FromArgb(35, 45, 65);
        _btnExport.ForeColor = Color.White;
        _btnExport.FlatStyle = FlatStyle.Flat;
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Margin = new Padding(10, 0, 0, 0);
        _btnExport.Height = 32;
        _btnExport.Width = 110;
        _btnExport.Cursor = Cursors.Hand;
        _btnExport.Click += ExportCsv;

        pnlButtons.Controls.Add(_btnRefresh);
        pnlButtons.Controls.Add(_btnKey);
        pnlButtons.Controls.Add(_btnWeb);
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

        pnlCards.Controls.Add(CreateCard("💳 CANLI TOPLAM BAKİYE", _lblCard1Value, _lblCard1Sub, Color.FromArgb(20, 35, 60)), 0, 0);
        pnlCards.Controls.Add(CreateCard("⚡ YÜKLENEN BAKİYE (TOPPED-UP)", _lblCard2Value, _lblCard2Sub, Color.FromArgb(25, 30, 55)), 1, 0);
        pnlCards.Controls.Add(CreateCard("🎁 HEDİYE/HİBE BAKİYE (GRANTED)", _lblCard3Value, _lblCard3Sub, Color.FromArgb(20, 40, 45)), 2, 0);
        pnlCards.Controls.Add(CreateCard("🔑 BAĞLI ANAHTAR / DURUM", _lblCard4Value, _lblCard4Sub, Color.FromArgb(35, 25, 50)), 3, 0);
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
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(4)
        };
        var lblT = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(170, 185, 215),
            AutoSize = true,
            Location = new Point(12, 8)
        };
        lblVal.Text = "-";
        lblVal.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
        lblVal.ForeColor = Color.White;
        lblVal.AutoSize = true;
        lblVal.Location = new Point(10, 28);

        lblSub.Text = "-";
        lblSub.Font = new Font("Segoe UI", 8F);
        lblSub.ForeColor = Color.FromArgb(160, 175, 205);
        lblSub.AutoSize = true;
        lblSub.Location = new Point(12, 58);

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
    }

    public async Task LoadDataAsync()
    {
        UseWaitCursor = true;
        _btnRefresh.Enabled = false;

        try
        {
            var settings = AiOptimizationSettingsStore.Load();

            if (string.IsNullOrWhiteSpace(settings.DeepSeekApiKey))
            {
                _lblCard1Value.Text = "Anahtar Yok";
                _lblCard1Sub.Text = "DeepSeek API anahtarı tanımlanmadı";
                _lblCard2Value.Text = "$0.00";
                _lblCard2Sub.Text = "Bakiye bulunamadı";
                _lblCard3Value.Text = "$0.00";
                _lblCard3Sub.Text = "Hibe bakiye yok";
                _lblCard4Value.Text = "Tanımlanmadı";
                _lblCard4Sub.Text = "api.deepseek.com bağlantısı yok";
                _lblNotice.Text = "💡 DeepSeek canlı bakiyesini görmek için lütfen 'API Anahtarı Tanımla' butonuna tıklayarak anahtarınızı girin.";
                return;
            }

            // Canlı Bakiye ve Model Sorgusu
            var balance = await AiBalanceCheckerService.CheckDeepSeekBalanceAsync(settings.DeepSeekApiKey);
            _currentBalance = balance;

            if (balance.IsAvailable)
            {
                if (balance.BalanceCny.HasValue && balance.Currency == "CNY")
                {
                    _lblCard1Value.Text = $"¥{balance.BalanceCny.Value:N2} CNY";
                    _lblCard1Sub.Text = $"~${balance.TotalBalanceUsd:N2} USD (~{balance.TotalBalanceTry():N0} ₺)";
                }
                else
                {
                    _lblCard1Value.Text = $"${balance.TotalBalanceUsd:N2} USD";
                    _lblCard1Sub.Text = $"~{balance.TotalBalanceTry():N2} TL (api.deepseek.com)";
                }

                _lblCard2Value.Text = $"${balance.ToppedUpBalanceUsd:N2}";
                _lblCard2Sub.Text = "Kullanıcı tarafından yüklenen";

                _lblCard3Value.Text = $"${balance.GrantedBalanceUsd:N2}";
                _lblCard3Sub.Text = "DeepSeek hediye/promosyon bakiyesi";

                _lblCard4Value.Text = !string.IsNullOrWhiteSpace(balance.MaskedApiKey) ? balance.MaskedApiKey : "Aktif";
                _lblCard4Sub.Text = "✅ DeepSeek API Canlı & Hazır";
            }
            else
            {
                _lblCard1Value.Text = "Bakiye Yetersiz";
                _lblCard1Sub.Text = balance.StatusMessage;
                _lblCard4Value.Text = !string.IsNullOrWhiteSpace(balance.MaskedApiKey) ? balance.MaskedApiKey : "Geçersiz";
                _lblCard4Sub.Text = "⚠️ Bakiye tükendi veya hesap askıda";
            }

            // Model Rozetleri
            _pnlModels.Controls.Clear();
            var lblMLabel = new Label
            {
                Text = "Desteklenen Modeller & Tarifeler:",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(170, 185, 210),
                Margin = new Padding(0, 5, 8, 0)
            };
            _pnlModels.Controls.Add(lblMLabel);

            var badgeChat = new Label
            {
                Text = "deepseek-chat (V3): $0.15 / $0.60 per 1M (Önbellek: $0.075)",
                AutoSize = true,
                BackColor = Color.FromArgb(20, 35, 60),
                ForeColor = Color.FromArgb(200, 225, 255),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgeChat);

            var badgeReasoner = new Label
            {
                Text = "deepseek-reasoner (R1): $0.55 / $2.19 per 1M (Düşünce Akışı Dahil)",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 25, 55),
                ForeColor = Color.FromArgb(235, 210, 255),
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(0, 2, 8, 2),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _pnlModels.Controls.Add(badgeReasoner);

            // Tablo: Yerel SQLite geçmişindeki DeepSeek işlemleri
            var repo = AiTokenUsageTrackerService.GetRepository();
            var records = await repo.GetHistoryAsync(providerFilter: "DeepSeek", limit: 300);

            _grid.Rows.Clear();
            if (records.Count == 0)
            {
                _grid.Rows.Add(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    "Genel",
                    "deepseek-reasoner",
                    "0",
                    "0",
                    "0",
                    "$0.00",
                    "0.00 ₺",
                    "Hazır",
                    "Henüz bu oturumda DeepSeek işlemi yapılmadı."
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

            _lblNotice.Text = $"✅ Canlı bakiye sorgusu başarılı ({DateTime.Now:HH:mm:ss}). Toplam {records.Count} DeepSeek işlemi kaydedildi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"DeepSeek bakiye sorgulama hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Text = "DeepSeek Platform (platform.deepseek.com/api_keys) sayfasından aldığınız\nAPI anahtarını buraya yapıştırın:\n(Örnek: sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)",
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
            Text = "💾 Kaydet ve Canlı Sorgula",
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
            settings.DeepSeekApiKey = txtKey.Text.Trim();
            AiOptimizationSettingsStore.Save(settings);
            _ = LoadDataAsync();
        }
    }

    private void ExportCsv(object? sender, EventArgs e)
    {
        if (_grid.Rows.Count == 0) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"DeepSeek_Bakiye_ve_Kullanim_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);
            sw.WriteLine("Tarih;Modül;Model;Girdi Token;Çıkış Token;Toplam Token;Maliyet USD;Maliyet TL;Durum;Not");

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value?.ToString()?.Replace("\"", "\"\"")}\"");
                sw.WriteLine(string.Join(";", cells));
            }

            MessageBox.Show(this, "DeepSeek raporu CSV olarak kaydedildi.", "Dışa Aktarma", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        catch (Exception ex)
        {
            MessageBox.Show($"Web sayfası açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
