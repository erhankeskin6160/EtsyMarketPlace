namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EtsyMarketPlace.Infrastructure.Shipping;

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
    private readonly Label _lblHint = new();
    private readonly string _providerName;

    public ShippingLoginCredentialsDialog(string providerName, string existingEmail)
    {
        _providerName = providerName;
        Text = $"{providerName} - Canlı Oturum & Giriş Bilgileri";
        Size = new Size(590, 510);
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
            Text = "📋 Seçenek 1: Canlı Bearer Token (En Hızlı Yöntem)",
            ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Location = new Point(20, 14),
            Size = new Size(530, 20)
        };
        pnl.Controls.Add(lblTokenSection);

        _txtDirectToken.Location = new Point(20, 36);
        _txtDirectToken.Width = 380;
        _txtDirectToken.Height = 44;
        _txtDirectToken.Multiline = true;
        _txtDirectToken.BackColor = Color.FromArgb(30, 41, 59);
        _txtDirectToken.ForeColor = Color.FromArgb(241, 245, 249);
        _txtDirectToken.Font = new Font("Consolas", 8.2f);
        _txtDirectToken.PlaceholderText = "Bearer eyJhbGciOi... veya eyJhbGciOi...";
        pnl.Controls.Add(_txtDirectToken);

        var btnPasteClipboard = new Button
        {
            Text = "📋 Panodan Al",
            Location = new Point(410, 36),
            Size = new Size(140, 44),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold)
        };
        btnPasteClipboard.FlatAppearance.BorderSize = 0;
        btnPasteClipboard.Click += (_, _) =>
        {
            TryAutoFillFromClipboard(showFeedback: true);
        };
        pnl.Controls.Add(btnPasteClipboard);

        var btnApplyToken = new Button
        {
            Text = "🔑 Bu Tokeni Kaydet & Kullan",
            Location = new Point(20, 88),
            Size = new Size(530, 32),
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
            Location = new Point(20, 126),
            Size = new Size(530, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnl.Controls.Add(lblDivider);

        // 2. Seçenek: Otomatik Giriş Bilgileri
        var lblAutoSection = new Label
        {
            Text = "🔐 Seçenek 2: Otomatik Giriş ve Tarayıcı Seçenekleri",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Location = new Point(20, 146),
            Size = new Size(530, 20)
        };
        pnl.Controls.Add(lblAutoSection);

        // Email / Telefon
        var lblEmail = new Label { Text = "Telefon Numarası veya E-Posta:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 168), AutoSize = true };
        pnl.Controls.Add(lblEmail);
        _txtEmail.Location = new Point(20, 188);
        _txtEmail.Width = 530;
        _txtEmail.BackColor = Color.FromArgb(30, 41, 59);
        _txtEmail.ForeColor = Color.White;
        _txtEmail.PlaceholderText = "Örn: 5342600561 veya e-posta adresi";
        _txtEmail.Text = existingEmail;
        pnl.Controls.Add(_txtEmail);

        // Password
        var lblPassword = new Label { Text = "Şifre:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 218), AutoSize = true };
        pnl.Controls.Add(lblPassword);
        _txtPassword.Location = new Point(20, 238);
        _txtPassword.Width = 530;
        _txtPassword.UseSystemPasswordChar = true;
        _txtPassword.BackColor = Color.FromArgb(30, 41, 59);
        _txtPassword.ForeColor = Color.White;
        pnl.Controls.Add(_txtPassword);

        // Checkbox
        _chkAutoRefresh.Text = "Şifremi Windows DPAPI ile güvenli şifrele & token bittiğinde otomatik yenile";
        _chkAutoRefresh.Checked = true;
        _chkAutoRefresh.Location = new Point(20, 270);
        _chkAutoRefresh.Width = 530;
        _chkAutoRefresh.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
        pnl.Controls.Add(_chkAutoRefresh);

        // Canlı Rehberlik / İpucu Notu
        _lblHint.Text = "💡 İpucu: '🌐 Tarayıcı (Otomatik)' butonuna bastığınızda giriş yaptığınız an token kendiliğinden yakalanır ve pencere kapanır.";
        _lblHint.ForeColor = Color.FromArgb(148, 163, 184);
        _lblHint.Font = new Font("Segoe UI", 7.8f, FontStyle.Italic);
        _lblHint.Location = new Point(20, 304);
        _lblHint.Size = new Size(530, 44);
        pnl.Controls.Add(_lblHint);

        // Alt Butonlar Satırı
        var btnBrowser = new Button
        {
            Text = "🌐 Tarayıcı (Otomatik)",
            Location = new Point(20, 355),
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold)
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
            Location = new Point(190, 355),
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
            string url = _providerName.Contains("Aras", StringComparison.OrdinalIgnoreCase)
                ? "https://panel.arasglobalcargo.com/auth"
                : "https://shiptomore.com/web/login";

            PuppeteerShippingSessionManager.OpenOfficialPortalInDefaultBrowser(url);

            _lblHint.Text = "🌐 Google Chrome açıldı! Panelde oturum açtıktan sonra F12 DevTools Network'ten aldığınız tokeni yukarı yapıştırın veya '📋 Panodan Al'a basın.";
            _lblHint.ForeColor = Color.FromArgb(56, 189, 248);
        };
        pnl.Controls.Add(btnDefaultBrowser);

        var btnSave = new Button
        {
            Text = "⚡ Otomatik Giriş",
            Location = new Point(360, 355),
            Size = new Size(125, 36),
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
            Location = new Point(495, 355),
            Size = new Size(55, 36),
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

        // Kullanıcı Chrome'dan bu pencereye tıkladığında panoda JWT varsa otomatik yakala
        Activated += (_, _) =>
        {
            TryAutoFillFromClipboard(showFeedback: false);
        };
    }

    private void TryAutoFillFromClipboard(bool showFeedback)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (text.Contains("eyJ") && text.Length > 25)
                {
                    var match = Regex.Match(text, @"ey[A-Za-z0-9_-]+\.ey[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+");
                    if (match.Success)
                    {
                        string foundJwt = match.Value;
                        if (!foundJwt.Equals(_txtDirectToken.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            _txtDirectToken.Text = foundJwt;
                            _lblHint.Text = "✨ Panodan kopyalanan canlı token algılandı! '🔑 Bu Tokeni Kaydet & Kullan' butonuna basarak kaydedin.";
                            _lblHint.ForeColor = Color.FromArgb(52, 211, 153);
                            return;
                        }
                    }
                }
            }

            if (showFeedback)
            {
                MessageBox.Show("Panoda geçerli bir Bearer JWT tokeni bulunamadı. Lütfen Chrome'dan tokeni kopyalayıp tekrar deneyin.", "Pano Boş", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch { }
    }
}
