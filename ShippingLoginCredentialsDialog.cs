namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Aras Global veya ShipEntegra için kullanıcı adı ve şifre kaydetme / otomatik giriş diyaloğu.
/// </summary>
internal sealed class ShippingLoginCredentialsDialog : Form
{
    public string Email => _txtEmail.Text.Trim();
    public string Password => _txtPassword.Text;
    public bool AutoRefresh => _chkAutoRefresh.Checked;
    public bool OpenInBrowserRequested { get; private set; }
    public bool OpenInDefaultBrowserRequested { get; private set; }
    public string DirectToken => _txtDirectToken.Text.Trim();

    private readonly TextBox _txtDirectToken = new();
    private readonly TextBox _txtEmail = new();
    private readonly TextBox _txtPassword = new();
    private readonly CheckBox _chkAutoRefresh = new();

    public ShippingLoginCredentialsDialog(string providerName, string existingEmail)
    {
        Text = $"{providerName} - Oturum ve Giriş Bilgileri";
        Size = new Size(580, 490);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

        // 1. Seçenek: Canlı Token Yapıştır
        var lblTokenSection = new Label
        {
            Text = "📋 Seçenek 1: Canlı Token (Chrome DevTools'tan Doğrudan Yapıştır)",
            ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Location = new Point(20, 16),
            Size = new Size(520, 20)
        };
        pnl.Controls.Add(lblTokenSection);

        _txtDirectToken.Location = new Point(20, 38);
        _txtDirectToken.Width = 520;
        _txtDirectToken.Height = 44;
        _txtDirectToken.Multiline = true;
        _txtDirectToken.BackColor = Color.FromArgb(30, 41, 59);
        _txtDirectToken.ForeColor = Color.FromArgb(241, 245, 249);
        _txtDirectToken.Font = new Font("Consolas", 8.2f);
        _txtDirectToken.PlaceholderText = "Bearer eyJhbGciOi... veya eyJhbGciOi...";
        pnl.Controls.Add(_txtDirectToken);

        var btnApplyToken = new Button
        {
            Text = "🔑 Bu Tokeni Kaydet & Kullan",
            Location = new Point(20, 88),
            Size = new Size(520, 32),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
        };
        btnApplyToken.FlatAppearance.BorderSize = 0;
        btnApplyToken.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtDirectToken.Text))
            {
                MessageBox.Show("Lütfen geçerli bir Bearer token yapıştırın.", "Token Boş", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            OpenInBrowserRequested = false;
            OpenInDefaultBrowserRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnApplyToken);

        // Ayırıcı
        var lblDivider = new Label
        {
            Text = "─────────────────────────── VEYA ───────────────────────────",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(20, 128),
            Size = new Size(520, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnl.Controls.Add(lblDivider);

        // 2. Seçenek: Otomatik Giriş Bilgileri
        var lblAutoSection = new Label
        {
            Text = "🔐 Seçenek 2: Otomatik Giriş ve Tarayıcı Seçenekleri",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Location = new Point(20, 150),
            Size = new Size(520, 20)
        };
        pnl.Controls.Add(lblAutoSection);

        // Email / Telefon
        var lblEmail = new Label { Text = "Telefon Numarası veya E-Posta:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 172), AutoSize = true };
        pnl.Controls.Add(lblEmail);
        _txtEmail.Location = new Point(20, 192);
        _txtEmail.Width = 520;
        _txtEmail.BackColor = Color.FromArgb(30, 41, 59);
        _txtEmail.ForeColor = Color.White;
        _txtEmail.PlaceholderText = "Örn: 5342600561 veya e-posta adresi";
        _txtEmail.Text = existingEmail;
        pnl.Controls.Add(_txtEmail);

        // Password
        var lblPassword = new Label { Text = "Şifre:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 222), AutoSize = true };
        pnl.Controls.Add(lblPassword);
        _txtPassword.Location = new Point(20, 242);
        _txtPassword.Width = 520;
        _txtPassword.UseSystemPasswordChar = true;
        _txtPassword.BackColor = Color.FromArgb(30, 41, 59);
        _txtPassword.ForeColor = Color.White;
        pnl.Controls.Add(_txtPassword);

        // Checkbox
        _chkAutoRefresh.Text = "Şifremi Windows DPAPI ile güvenli şifrele & token bittiğinde otomatik yenile";
        _chkAutoRefresh.Checked = true;
        _chkAutoRefresh.Location = new Point(20, 274);
        _chkAutoRefresh.Width = 520;
        _chkAutoRefresh.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
        pnl.Controls.Add(_chkAutoRefresh);

        // Canlı Rehberlik / İpucu Notu
        var lblHint = new Label
        {
            Text = "💡 İpucu: Tarayıcı açılmıyorsa '🚀 Normal Chrome'da Aç' butonuna basarak doğrudan giriş yapabilir ve tokeni kopyalayıp yukarı yapıştırabilirsiniz.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 7.8f, FontStyle.Italic),
            Location = new Point(20, 310),
            Size = new Size(520, 36)
        };
        pnl.Controls.Add(lblHint);

        // Alt Butonlar Satırı
        var btnBrowser = new Button
        {
            Text = "🌐 Tarayıcı (Otomatik)",
            Location = new Point(20, 355),
            Size = new Size(155, 36),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.2f)
        };
        btnBrowser.FlatAppearance.BorderSize = 0;
        btnBrowser.Click += (_, _) =>
        {
            OpenInBrowserRequested = true;
            OpenInDefaultBrowserRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnBrowser);

        var btnDefaultBrowser = new Button
        {
            Text = "🚀 Normal Chrome'da Aç",
            Location = new Point(180, 355),
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(14, 116, 144), // Cyan 700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.2f)
        };
        btnDefaultBrowser.FlatAppearance.BorderSize = 0;
        btnDefaultBrowser.Click += (_, _) =>
        {
            OpenInBrowserRequested = false;
            OpenInDefaultBrowserRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnDefaultBrowser);

        var btnSave = new Button
        {
            Text = "⚡ Otomatik Giriş",
            Location = new Point(345, 355),
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(37, 99, 235), // Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            OpenInBrowserRequested = false;
            OpenInDefaultBrowserRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "Kapat",
            Location = new Point(480, 355),
            Size = new Size(60, 36),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(148, 163, 184),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.2f)
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        pnl.Controls.Add(btnCancel);

        Controls.Add(pnl);
    }
}
