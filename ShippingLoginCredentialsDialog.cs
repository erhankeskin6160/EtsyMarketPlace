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

    private readonly TextBox _txtEmail = new();
    private readonly TextBox _txtPassword = new();
    private readonly CheckBox _chkAutoRefresh = new();

    public ShippingLoginCredentialsDialog(string providerName, string existingEmail)
    {
        Text = $"{providerName} - Otomatik Giriş Bilgileri";
        Size = new Size(460, 310);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

        var lblInfo = new Label
        {
            Text = $"🔑 {providerName} oturumunuz bittiğinde sistemin arka planda F12 gerektirmeden otomatik token alabilmesi için bilgilerinizi giriniz:",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI Semibold", 9F),
            Dock = DockStyle.Top,
            Height = 44
        };
        pnl.Controls.Add(lblInfo);

        // Email
        var lblEmail = new Label { Text = "E-Posta / Kullanıcı Adı:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 70), AutoSize = true };
        pnl.Controls.Add(lblEmail);
        _txtEmail.Location = new Point(20, 92);
        _txtEmail.Width = 400;
        _txtEmail.BackColor = Color.FromArgb(30, 41, 59);
        _txtEmail.ForeColor = Color.White;
        _txtEmail.Text = existingEmail;
        pnl.Controls.Add(_txtEmail);

        // Password
        var lblPassword = new Label { Text = "Şifre:", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(20, 126), AutoSize = true };
        pnl.Controls.Add(lblPassword);
        _txtPassword.Location = new Point(20, 148);
        _txtPassword.Width = 400;
        _txtPassword.UseSystemPasswordChar = true;
        _txtPassword.BackColor = Color.FromArgb(30, 41, 59);
        _txtPassword.ForeColor = Color.White;
        pnl.Controls.Add(_txtPassword);

        // Checkbox
        _chkAutoRefresh.Text = "Şifremi Windows DPAPI ile güvenli şifrele & token bittiğinde otomatik yenile";
        _chkAutoRefresh.Checked = true;
        _chkAutoRefresh.Location = new Point(20, 182);
        _chkAutoRefresh.Width = 400;
        _chkAutoRefresh.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
        pnl.Controls.Add(_chkAutoRefresh);

        // Butonlar
        var btnBrowser = new Button
        {
            Text = "🌐 Tarayıcıda Aç",
            Location = new Point(20, 220),
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnBrowser.FlatAppearance.BorderSize = 0;
        btnBrowser.Click += (_, _) =>
        {
            OpenInBrowserRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnBrowser);

        var btnSave = new Button
        {
            Text = "⚡ Otomatik Giriş Yap",
            Location = new Point(160, 220),
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(37, 99, 235), // Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) =>
        {
            OpenInBrowserRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        };
        pnl.Controls.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(330, 220),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(148, 163, 184),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
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
