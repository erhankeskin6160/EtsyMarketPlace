namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class OpenAiOfficialUsageDialog : Form
{
    private readonly ComboBox _cboMonth = new();
    private readonly Button _btnQuery = new();
    private readonly Button _btnAdminKey = new();
    private readonly Button _btnExport = new();

    private readonly Label _lblCard1Value = new();
    private readonly Label _lblCard1Sub = new();
    private readonly Label _lblCard2Value = new();
    private readonly Label _lblCard2Sub = new();
    private readonly Label _lblCard3Value = new();
    private readonly Label _lblCard3Sub = new();
    private readonly Label _lblCard4Value = new();
    private readonly Label _lblCard4Sub = new();

    private readonly DataGridView _grid = new();
    private readonly Label _lblNotice = new();
    private OpenAiOfficialUsageReport? _currentReport;

    public OpenAiOfficialUsageDialog()
    {
        Text = "OpenAI Resmi Kullanım, Kota & Fatura Raporu";
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
            RowCount = 5,
            Padding = new Padding(18)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Controls
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // 4 KPI Cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Bottom
        Controls.Add(mainLayout);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        var lblTitle = new Label
        {
            Text = "📊 OpenAI Resmi Kullanım & Fatura Dökümü",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        };
        var lblSubtitle = new Label
        {
            Text = "OpenAI Platform hesabınızdaki aylık harcamalar, tüketilen toplam token sayıları ve gün bazlı maliyetler.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(160, 175, 200),
            AutoSize = true,
            Location = new Point(0, 26)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // 2. Filter Bar
        var pnlFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var lblMonth = new Label { Text = "Dönem:", AutoSize = true, Margin = new Padding(0, 7, 5, 0), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _cboMonth.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboMonth.Width = 160;

        // Popüle et (Son 6 ay)
        var now = DateTime.UtcNow;
        for (int i = 0; i < 6; i++)
        {
            var dt = now.AddMonths(-i);
            _cboMonth.Items.Add($"{dt:MMMM yyyy}");
        }
        _cboMonth.SelectedIndex = 0;
        _cboMonth.SelectedIndexChanged += async (_, _) => await LoadDataAsync();

        _btnQuery.Text = "🔄 Verileri Çek";
        _btnQuery.BackColor = UiStyle.AiColor;
        _btnQuery.ForeColor = Color.White;
        _btnQuery.FlatStyle = FlatStyle.Flat;
        _btnQuery.FlatAppearance.BorderSize = 0;
        _btnQuery.Height = 30;
        _btnQuery.Width = 130;
        _btnQuery.Margin = new Padding(12, 1, 0, 0);
        _btnQuery.Cursor = Cursors.Hand;
        _btnQuery.Click += async (_, _) => await LoadDataAsync();

        _btnAdminKey.Text = "🔑 Admin Key Tanımla";
        _btnAdminKey.BackColor = Color.FromArgb(40, 50, 70);
        _btnAdminKey.ForeColor = Color.White;
        _btnAdminKey.FlatStyle = FlatStyle.Flat;
        _btnAdminKey.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 110);
        _btnAdminKey.Height = 30;
        _btnAdminKey.Width = 165;
        _btnAdminKey.Margin = new Padding(10, 1, 0, 0);
        _btnAdminKey.Cursor = Cursors.Hand;
        _btnAdminKey.Click += (_, _) => ShowAdminKeyDialog();

        _btnExport.Text = "📥 CSV İndir";
        _btnExport.BackColor = Color.FromArgb(40, 50, 70);
        _btnExport.ForeColor = Color.White;
        _btnExport.FlatStyle = FlatStyle.Flat;
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Height = 30;
        _btnExport.Width = 110;
        _btnExport.Margin = new Padding(10, 1, 0, 0);
        _btnExport.Cursor = Cursors.Hand;
        _btnExport.Click += ExportCsv;

        pnlFilters.Controls.Add(lblMonth);
        pnlFilters.Controls.Add(_cboMonth);
        pnlFilters.Controls.Add(_btnQuery);
        pnlFilters.Controls.Add(_btnAdminKey);
        pnlFilters.Controls.Add(_btnExport);
        mainLayout.Controls.Add(pnlFilters, 0, 1);

        // 3. 4 KPI Cards
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        for (int i = 0; i < 4; i++) pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        pnlCards.Controls.Add(CreateCard("💳 AYLIK TOPLAM HARCAMA", _lblCard1Value, _lblCard1Sub, Color.FromArgb(20, 35, 60)), 0, 0);
        pnlCards.Controls.Add(CreateCard("⚡ TOPLAM TOKEN SAYISI", _lblCard2Value, _lblCard2Sub, Color.FromArgb(25, 30, 55)), 1, 0);
        pnlCards.Controls.Add(CreateCard("🔄 TOPLAM İSTEKLER", _lblCard3Value, _lblCard3Sub, Color.FromArgb(20, 40, 45)), 2, 0);
        pnlCards.Controls.Add(CreateCard("🔑 BAĞLI ANAHTAR / KAYNAK", _lblCard4Value, _lblCard4Sub, Color.FromArgb(35, 25, 50)), 3, 0);
        mainLayout.Controls.Add(pnlCards, 0, 2);

        // 4. DataGridView
        ConfigureGrid();
        mainLayout.Controls.Add(_grid, 0, 3);

        // 5. Bottom Notice Bar
        var pnlBottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _lblNotice.AutoSize = true;
        _lblNotice.ForeColor = Color.FromArgb(160, 175, 200);
        _lblNotice.Font = new Font("Segoe UI", 8.5F);
        _lblNotice.Margin = new Padding(0, 10, 0, 0);
        pnlBottom.Controls.Add(_lblNotice);
        mainLayout.Controls.Add(pnlBottom, 0, 4);
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
        lblVal.Text = "0";
        lblVal.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
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
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowTemplate.Height = 32;

        _grid.DefaultCellStyle.BackColor = Color.FromArgb(18, 26, 46);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 60, 95);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(200, 215, 240);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 36;
        _grid.EnableHeadersVisualStyles = false;

        _grid.Columns.Add("Date", "Tarih");
        _grid.Columns.Add("Service", "Hizmet / Model");
        _grid.Columns.Add("Requests", "İstek Sayısı");
        _grid.Columns.Add("InputTokens", "Girdi Token");
        _grid.Columns.Add("OutputTokens", "Çıktı Token");
        _grid.Columns.Add("TotalTokens", "Toplam Token");
        _grid.Columns.Add("CostUsd", "Maliyet ($)");
        _grid.Columns.Add("CostTry", "Maliyet (₺)");
    }

    private async Task LoadDataAsync()
    {
        UseWaitCursor = true;
        _btnQuery.Enabled = false;

        try
        {
            var settings = AiOptimizationSettingsStore.Load();
            var dt = DateTime.UtcNow.AddMonths(-_cboMonth.SelectedIndex);

            var report = await OpenAiUsageFetcherService.FetchOfficialUsageReportAsync(
                settings.OpenAiApiKey,
                settings.OpenAiAdminApiKey,
                dt.Year,
                dt.Month);

            _currentReport = report;

            // KPI Kartlarını güncelle
            _lblCard1Value.Text = $"${report.TotalCostUsd:N2} USD";
            _lblCard1Sub.Text = $"Yaklaşık {report.TotalCostTry:N2} TL";

            _lblCard2Value.Text = report.TotalTokens.ToString("N0");
            _lblCard2Sub.Text = "Toplam Token";

            _lblCard3Value.Text = $"{report.TotalRequests:N0} İstek";
            _lblCard3Sub.Text = "Tamamlanan Çağrı";

            _lblCard4Value.Text = !string.IsNullOrWhiteSpace(report.MaskedKey) ? report.MaskedKey : "Yok";
            _lblCard4Sub.Text = report.DataSource;

            // Tabloyu doldur
            _grid.Rows.Clear();
            if (report.DailyItems.Count == 0)
            {
                _grid.Rows.Add(
                    dt.ToString("yyyy-MM-dd"),
                    "Kullanım Verisi Yok",
                    "0",
                    "0",
                    "0",
                    "0",
                    "$0.00",
                    "0.00 ₺"
                );
            }
            else
            {
                foreach (var item in report.DailyItems.OrderByDescending(x => x.Date))
                {
                    _grid.Rows.Add(
                        item.Date.ToString("yyyy-MM-dd"),
                        item.ServiceOrModel,
                        item.RequestCount.ToString("N0"),
                        item.InputTokens.ToString("N0"),
                        item.OutputTokens.ToString("N0"),
                        item.TotalTokens.ToString("N0"),
                        $"${item.CostUsd:F4}",
                        $"{item.CostTry:F2} ₺"
                    );
                }
            }

            if (!report.HasAdminKey)
            {
                _lblNotice.Text = "💡 Bilgi: Şirket genel faturanızın tamamını bağlamak için 'Admin Key Tanımla' butonunu kullanabilirsiniz.";
            }
            else
            {
                _lblNotice.Text = "✅ Veriler OpenAI resmi organizasyon uç noktalarından canlı olarak çekilmiştir.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Veri çekme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnQuery.Enabled = true;
        }
    }

    private void ShowAdminKeyDialog()
    {
        var settings = AiOptimizationSettingsStore.Load();

        using var promptForm = new Form
        {
            Text = "OpenAI Admin API Key Tanımla",
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
            Text = "OpenAI Platform (platform.openai.com/settings/organization/admin-keys)\nsayfasından 'Read' yetkili bir Admin Key alarak buraya yapıştırın:\n(Örnek: sk-admin-abcdef...)",
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
            Text = "💾 Kaydet ve Canlı Sorgula",
            DialogResult = DialogResult.OK,
            Location = new Point(280, 130),
            Size = new Size(200, 34),
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
            _ = LoadDataAsync();
        }
    }

    private void ExportCsv(object? sender, EventArgs e)
    {
        if (_currentReport == null || _grid.Rows.Count == 0) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"OpenAI_Resmi_Fatura_{_cboMonth.Text.Replace(" ", "_")}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);
            sw.WriteLine("Tarih;Hizmet/Model;İstek Sayısı;Girdi Token;Çıktı Token;Toplam Token;Maliyet USD;Maliyet TL");

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var cells = row.Cells.Cast<DataGridViewCell>().Select(c => $"\"{c.Value?.ToString()?.Replace("\"", "\"\"")}\"");
                sw.WriteLine(string.Join(";", cells));
            }

            MessageBox.Show(this, "Rapor CSV olarak kaydedildi.", "Dışa Aktarma", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
