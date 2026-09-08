namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AbTesting;

internal sealed class BulkAbTestLaunchDialog : Form
{
    private readonly RadioButton _rdo7Days = new() { Text = "⚡ 7 Gün (Hızlı Test)", AutoSize = true };
    private readonly RadioButton _rdo14Days = new() { Text = "⭐ 14 Gün (Önerilen - Etsy Algoritma Döngüsü)", AutoSize = true, Checked = true };
    private readonly RadioButton _rdo30Days = new() { Text = "📅 30 Gün (Uzun Vadeli & Sezonluk Doğrulama)", AutoSize = true };
    private readonly TextBox _txtPrefix = new() { Text = "[Toplu A/B]", Width = 200 };
    private readonly CheckBox _chkAutoDeploy = new() { Text = "⚡ Varyant B'yi (AI Optimizasyonunu) hemen canlı Etsy'ye uygula", AutoSize = true, Checked = false };
    private readonly CheckBox _chkGuardrail = new() { Text = "🛡️ Akıllı Güvenlik Kalkanı (Trafik %40+ düşerse erken uyarı ver)", AutoSize = true, Checked = true };

    public BulkAbTestLaunchOptions Options { get; private set; } = new();

    public BulkAbTestLaunchDialog(int itemCount)
    {
        Text = "Toplu A/B Testi Başlat";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(580, 520);
        UiStyle.ApplyTheme(this);

        BuildLayout(itemCount);
    }

    private void BuildLayout(int itemCount)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            Padding = new Padding(24),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Info Card
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Options Area
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons Row
        Controls.Add(root);

        // 1. Header
        var lblTitle = new Label
        {
            Text = "🧪 Toplu A/B Deneyi Başlatma",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(lblTitle, 0, 0);

        // 2. Info Card
        var infoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(12),
        };
        var lblInfo = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Seçili {itemCount} adet ürün için 2 varyantlı A/B deneyi kurulacaktır:\n• Varyant A (Orijinal) ➔ Mevcut Başlık, Etiket ve Açıklama\n• Varyant B (Deney Grubu) ➔ AI Tarafından Optimize Edilen Sürüm",
            Font = UiStyle.BaseFont,
            ForeColor = UiStyle.TextMuted,
        };
        infoPanel.Controls.Add(lblInfo);
        root.Controls.Add(infoPanel, 0, 1);

        // 3. Options Panel
        var optionsBox = new GroupBox
        {
            Text = " Deney Yapılandırması ",
            Dock = DockStyle.Fill,
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(16, 20, 16, 16),
        };

        var optionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
        };
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Prefix
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // 7 Days
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // 14 Days
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // 30 Days
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Auto Deploy Checkbox
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Guardrail Checkbox

        // Prefix Row
        var prefixPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var lblPrefix = new Label { Text = "Deney Adı Ön Eki:", AutoSize = true, Padding = new Padding(0, 5, 10, 0), ForeColor = UiStyle.TextDark, Font = UiStyle.BaseFont };
        _txtPrefix.Font = UiStyle.BaseFont;
        prefixPanel.Controls.Add(lblPrefix);
        prefixPanel.Controls.Add(_txtPrefix);
        optionsLayout.Controls.Add(prefixPanel, 0, 0);

        _rdo7Days.Font = UiStyle.BaseFont;
        _rdo7Days.ForeColor = UiStyle.TextDark;
        optionsLayout.Controls.Add(_rdo7Days, 0, 1);

        _rdo14Days.Font = UiStyle.BaseFont;
        _rdo14Days.ForeColor = UiStyle.TextDark;
        optionsLayout.Controls.Add(_rdo14Days, 0, 2);

        _rdo30Days.Font = UiStyle.BaseFont;
        _rdo30Days.ForeColor = UiStyle.TextDark;
        optionsLayout.Controls.Add(_rdo30Days, 0, 3);

        _chkAutoDeploy.Font = UiStyle.BaseFont;
        _chkAutoDeploy.ForeColor = UiStyle.TextDark;
        optionsLayout.Controls.Add(_chkAutoDeploy, 0, 4);

        _chkGuardrail.Font = UiStyle.BaseFont;
        _chkGuardrail.ForeColor = UiStyle.TextDark;
        optionsLayout.Controls.Add(_chkGuardrail, 0, 5);

        optionsBox.Controls.Add(optionsLayout);
        root.Controls.Add(optionsBox, 0, 2);

        // 4. Buttons Row
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
        };

        var btnCancel = UiStyle.CreateButton("İptal", isSecondary: true);
        btnCancel.Width = 110;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnPanel.Controls.Add(btnCancel);

        var btnStart = UiStyle.CreateButton("🧪 Testleri Başlat");
        btnStart.Width = 160;
        btnStart.Click += (_, _) =>
        {
            int days = _rdo7Days.Checked ? 7 : (_rdo30Days.Checked ? 30 : 14);
            string prefix = string.IsNullOrWhiteSpace(_txtPrefix.Text) ? "[Toplu A/B]" : _txtPrefix.Text.Trim();

            Options = new BulkAbTestLaunchOptions(
                DurationDays: days,
                ExperimentPrefix: prefix,
                AutoDeployVariantBToEtsy: _chkAutoDeploy.Checked,
                EnableSafetyGuardrail: _chkGuardrail.Checked);

            DialogResult = DialogResult.OK;
            Close();
        };
        btnPanel.Controls.Add(btnStart);

        root.Controls.Add(btnPanel, 0, 3);
    }
}
