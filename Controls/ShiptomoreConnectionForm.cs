namespace SimilarProductsWinForms.Controls;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Ship to More bağlantı ekranı.
///
/// Tasarım kararı: kullanıcıya "API" kavramı anlatılmaz. Panelde gördüğü iki değeri
/// (Client ID / Client Secret) bir kez yapıştırır, bağlantı test edilir ve bir daha sorulmaz.
/// Değerler şifreli saklanır (DPAPI) — bkz. ShiptomoreSettingsStore.
/// </summary>
public sealed class ShiptomoreConnectionForm : Form
{
    private readonly ShiptomoreConnectionService _service;

    private TextBox _txtClientId = null!;
    private TextBox _txtClientSecret = null!;
    private Label _lblStatus = null!;
    private Button _btnTest = null!;
    private Button _btnSave = null!;
    private Button _btnForget = null!;

    public ShiptomoreConnectionForm(ShiptomoreConnectionService? service = null)
    {
        _service = service ?? new ShiptomoreConnectionService(new ShiptomoreOfficialApiClient());

        BuildUi();
        LoadExisting();
    }

    private void BuildUi()
    {
        Text = "Ship to More Bağlantısı";
        Size = new Size(620, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UiStyle.BackgroundColor;
        ForeColor = UiStyle.TextDark;
        Font = UiStyle.BaseFont;

        var title = new Label
        {
            Text = "Ship to More bağlantısı",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Location = new Point(20, 18)
        };

        var help = new Label
        {
            Text =
                "Gönderi fiyatlarını ve gönderi oluşturmayı Ship to More üzerinden yapmak için bir kez\n" +
                "erişim anahtarı girmen gerekiyor. Anahtarı Ship to More panelinden alabilirsin:\n" +
                "panelde \"Bağlantı & Güvenlik\" bölümü (veya dev.shiptomore.com).\n\n" +
                "Anahtar yalnızca oluşturulduğu anda gösterilir; kaydedilmez. Bu yüzden kopyalayıp\n" +
                "buraya yapıştır — sonrası bizde şifreli saklanır, bir daha sorulmaz.",
            Font = UiStyle.BaseFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Location = new Point(22, 52)
        };

        var btnOpenPanel = UiStyle.CreateButton("Paneli Aç", isSecondary: true);
        btnOpenPanel.Dock = DockStyle.None;
        btnOpenPanel.Size = new Size(120, 36);
        btnOpenPanel.Location = new Point(22, 158);
        btnOpenPanel.Click += (s, e) => OpenPortal();

        AddField("Client ID", 200, out _txtClientId, secret: false);
        AddField("Client Secret", 262, out _txtClientSecret, secret: true);

        _lblStatus = new Label
        {
            Text = "Durum: bilinmiyor",
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = false,
            Location = new Point(22, 318),
            Size = new Size(560, 24)
        };

        _btnTest = UiStyle.CreateButton("Bağlantıyı Test Et", isSecondary: true);
        _btnTest.Dock = DockStyle.None;
        _btnTest.Size = new Size(160, 36);
        _btnTest.Location = new Point(22, 348);
        _btnTest.Click += async (s, e) => await TestAsync();

        _btnSave = UiStyle.CreateButton("Kaydet", isSecondary: false);
        _btnSave.Dock = DockStyle.None;
        _btnSave.Size = new Size(110, 36);
        _btnSave.Location = new Point(192, 348);
        _btnSave.Click += (s, e) => Save();

        _btnForget = UiStyle.CreateButton("Bağlantıyı Kes", isSecondary: true);
        _btnForget.Dock = DockStyle.None;
        _btnForget.Size = new Size(150, 36);
        _btnForget.Location = new Point(312, 348);
        _btnForget.Click += (s, e) => Forget();

        var btnClose = UiStyle.CreateButton("Kapat", isSecondary: true);
        btnClose.Dock = DockStyle.None;
        btnClose.Size = new Size(110, 36);
        btnClose.Location = new Point(472, 348);
        btnClose.Click += (s, e) => Close();

        AcceptButton = _btnSave;

        Controls.Add(title);
        Controls.Add(help);
        Controls.Add(btnOpenPanel);
        Controls.Add(_lblStatus);
        Controls.Add(_btnTest);
        Controls.Add(_btnSave);
        Controls.Add(_btnForget);
        Controls.Add(btnClose);
    }

    private void AddField(string label, int top, out TextBox box, bool secret)
    {
        var lbl = new Label
        {
            Text = label,
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Location = new Point(22, top)
        };

        box = new TextBox
        {
            Font = UiStyle.BaseFont,
            BackColor = UiStyle.InputBackground,
            ForeColor = UiStyle.TextDark,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(22, top + 22),
            Width = 556
        };

        if (secret)
        {
            box.UseSystemPasswordChar = true;
        }

        Controls.Add(lbl);
        Controls.Add(box);
    }

    private void LoadExisting()
    {
        var (id, secret) = ShiptomoreSettingsStore.LoadCredentials();
        _txtClientId.Text = id;
        _txtClientSecret.Text = secret;

        bool has = _service.HasSavedCredentials();
        SetStatus(has ? "Durum: anahtar kayıtlı (test edilmedi)" : "Durum: anahtar girilmemiş", has);
    }

    private void OpenPortal()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ShiptomoreConnectionService.DeveloperPortalUrl) { UseShellExecute = true });
            SetStatus("Panel tarayıcıda açıldı. Anahtarı kopyalayıp yukarıdaki alanlara yapıştır.", null);
        }
        catch (Exception ex)
        {
            SetStatus("Panel açılamadı: " + ex.Message, false);
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_txtClientId.Text) || string.IsNullOrWhiteSpace(_txtClientSecret.Text))
        {
            SetStatus("İki alan da doldurulmalı.", false);
            return;
        }

        _service.SaveFromUserInput(_txtClientId.Text, _txtClientSecret.Text);
        SetStatus("Anahtar şifreli olarak kaydedildi. Bağlantıyı test edebilirsin.", true);
    }

    private async System.Threading.Tasks.Task TestAsync()
    {
        _btnTest.Enabled = false;
        SetStatus("Bağlantı deneniyor...", null);

        try
        {
            // Test, kayıtlı anahtarla yapılır; girilen ama kaydedilmemiş değer varsa önce kaydet.
            if (!_service.HasSavedCredentials() &&
                !string.IsNullOrWhiteSpace(_txtClientId.Text) &&
                !string.IsNullOrWhiteSpace(_txtClientSecret.Text))
            {
                _service.SaveFromUserInput(_txtClientId.Text, _txtClientSecret.Text);
            }

            var status = await _service.TestAsync();
            SetStatus(status.Message, status.IsConnected);
        }
        catch (Exception ex)
        {
            SetStatus("Beklenmeyen hata: " + ex.Message, false);
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    private void Forget()
    {
        var answer = MessageBox.Show(
            "Kayıtlı Ship to More anahtarı silinsin mi?\n\nSilindikten sonra fiyat ve gönderi işlemleri çalışmaz.",
            "Bağlantıyı Kes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _service.Forget();
        _txtClientId.Text = string.Empty;
        _txtClientSecret.Text = string.Empty;
        SetStatus("Anahtar silindi.", false);
    }

    private void SetStatus(string message, bool? ok)
    {
        _lblStatus.Text = message;
        _lblStatus.ForeColor = ok switch
        {
            true => UiStyle.SuccessColor,
            false => UiStyle.DangerColor,
            _ => UiStyle.TextMuted
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
