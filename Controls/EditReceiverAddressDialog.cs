namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Etsy siparişinin alıcı ve teslimat adresini düzenlemek/tamamlamak için modern diyalog penceresi.
/// Aras Global veya diğer kargo firmalarına gönderi oluşturulmadan önce eksik adres bilgilerinin
/// giderilmesini sağlar.
/// </summary>
public sealed class EditReceiverAddressDialog : Form
{
    private readonly EtsyOrderFulfillmentItem _order;

    private readonly TextBox _txtBuyerName = new();
    private readonly TextBox _txtBuyerEmail = new();
    private readonly TextBox _txtPhone = new();
    private readonly TextBox _txtStreetAddress = new();
    private readonly TextBox _txtSecondAddress = new();
    private readonly TextBox _txtCity = new();
    private readonly ComboBox _cmbState = new();
    private readonly TextBox _txtPostalCode = new();
    private readonly TextBox _txtCountryCode = new();
    private readonly TextBox _txtCountryName = new();
    private readonly Label _lblValidationMsg = new();

    public EditReceiverAddressDialog(EtsyOrderFulfillmentItem order)
    {
        _order = order ?? throw new ArgumentNullException(nameof(order));

        Text = $"Alıcı Adresini Düzenle - Sipariş #{order.ReceiptId}";
        Size = new Size(580, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        PopulateFields();
    }

    private void BuildLayout()
    {
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            AutoScroll = true
        };
        Controls.Add(mainPanel);

        int y = 10;

        var lblTitle = new Label
        {
            Text = "✏️ Alıcı & Teslimat Adresini Düzenle",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248), // Sky 400
            Location = new Point(24, y),
            Size = new Size(510, 26)
        };
        mainPanel.Controls.Add(lblTitle);
        y += 28;

        var lblSub = new Label
        {
            Text = "Etsy API'den eksik dönen adres alanlarını tamamlayın. Aras Global bu bilgileri kullanacaktır.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(148, 163, 184), // Slate 400
            Location = new Point(24, y),
            Size = new Size(510, 20)
        };
        mainPanel.Controls.Add(lblSub);
        y += 30;

        // Alıcı Adı
        AddLabel(mainPanel, "Alıcı Adı & Soyadı *", 24, y);
        y += 20;
        ConfigureInput(_txtBuyerName, 24, y, 510);
        mainPanel.Controls.Add(_txtBuyerName);
        y += 40;

        // E-Posta & Telefon yan yana
        AddLabel(mainPanel, "E-Posta Adresi", 24, y);
        AddLabel(mainPanel, "Telefon Numarası", 285, y);
        y += 20;
        ConfigureInput(_txtBuyerEmail, 24, y, 250);
        ConfigureInput(_txtPhone, 285, y, 250);
        mainPanel.Controls.Add(_txtBuyerEmail);
        mainPanel.Controls.Add(_txtPhone);
        y += 40;

        // Sokak Adresi 1
        AddLabel(mainPanel, "Sokak / Cadde Adresi 1 *", 24, y);
        y += 20;
        ConfigureInput(_txtStreetAddress, 24, y, 510);
        mainPanel.Controls.Add(_txtStreetAddress);
        y += 40;

        // Sokak Adresi 2 (Apartman / Daire)
        AddLabel(mainPanel, "Adres Satırı 2 (Apt, Daire, Kat)", 24, y);
        y += 20;
        ConfigureInput(_txtSecondAddress, 24, y, 510);
        mainPanel.Controls.Add(_txtSecondAddress);
        y += 40;

        // Şehir ve Eyalet yan yana
        AddLabel(mainPanel, "Şehir (İl) *", 24, y);
        AddLabel(mainPanel, "Eyalet / Bölge (State)", 285, y);
        y += 20;
        ConfigureInput(_txtCity, 24, y, 250);
        _cmbState.Location = new Point(285, y);
        _cmbState.Size = new Size(250, 26);
        _cmbState.BackColor = Color.FromArgb(30, 41, 59);
        _cmbState.ForeColor = Color.White;
        _cmbState.FlatStyle = FlatStyle.Flat;
        _cmbState.Font = new Font("Segoe UI", 9.5F);
        // Eyaletleri doldur
        foreach (var (code, name) in UsStateHelper.UsStates)
        {
            _cmbState.Items.Add($"{code} - {name}");
        }
        mainPanel.Controls.Add(_txtCity);
        mainPanel.Controls.Add(_cmbState);
        y += 40;

        // Posta Kodu, Ülke Kodu ve Ülke Adı
        AddLabel(mainPanel, "Posta Kodu (ZIP) *", 24, y);
        AddLabel(mainPanel, "Ülke Kodu *", 200, y);
        AddLabel(mainPanel, "Ülke Adı", 330, y);
        y += 20;
        ConfigureInput(_txtPostalCode, 24, y, 160);
        ConfigureInput(_txtCountryCode, 200, y, 115);
        ConfigureInput(_txtCountryName, 330, y, 205);
        mainPanel.Controls.Add(_txtPostalCode);
        mainPanel.Controls.Add(_txtCountryCode);
        mainPanel.Controls.Add(_txtCountryName);
        y += 44;

        // Hata / Uyarı mesajı etiketi
        _lblValidationMsg.Location = new Point(24, y);
        _lblValidationMsg.Size = new Size(510, 36);
        _lblValidationMsg.ForeColor = Color.FromArgb(248, 113, 113); // Red 400
        _lblValidationMsg.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _lblValidationMsg.Visible = false;
        mainPanel.Controls.Add(_lblValidationMsg);
        y += 40;

        // Butonlar
        var btnSave = new Button
        {
            Text = "✓ Adresi Kaydet",
            BackColor = Color.FromArgb(37, 99, 235), // Blue 600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(24, y),
            Size = new Size(360, 40),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;
        mainPanel.Controls.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "İptal",
            BackColor = Color.FromArgb(51, 65, 85), // Slate 700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(395, y),
            Size = new Size(140, 40),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        mainPanel.Controls.Add(btnCancel);
    }

    private static void AddLabel(Panel panel, string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            ForeColor = Color.FromArgb(203, 213, 225), // Slate 300
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            Location = new Point(x, y),
            AutoSize = true
        };
        panel.Controls.Add(lbl);
    }

    private static void ConfigureInput(TextBox txt, int x, int y, int width)
    {
        txt.Location = new Point(x, y);
        txt.Size = new Size(width, 26);
        txt.BackColor = Color.FromArgb(30, 41, 59); // Slate 800
        txt.ForeColor = Color.White;
        txt.BorderStyle = BorderStyle.FixedSingle;
        txt.Font = new Font("Segoe UI", 9.5F);
    }

    private void PopulateFields()
    {
        _txtBuyerName.Text = _order.BuyerName;
        _txtBuyerEmail.Text = _order.BuyerEmail;
        _txtPhone.Text = _order.Phone;
        _txtStreetAddress.Text = _order.StreetAddress;
        _txtSecondAddress.Text = _order.SecondAddress;
        _txtCity.Text = _order.City;
        _txtPostalCode.Text = _order.PostalCode;
        _txtCountryCode.Text = string.IsNullOrWhiteSpace(_order.CountryCode) ? "US" : _order.CountryCode;
        _txtCountryName.Text = _order.CountryName;

        if (!string.IsNullOrWhiteSpace(_order.State))
        {
            var (code, name) = UsStateHelper.ResolveUsOrCaState(_order.State);
            string found = $"{code} - {name}";
            int idx = _cmbState.FindString(code);
            if (idx >= 0)
            {
                _cmbState.SelectedIndex = idx;
            }
            else
            {
                _cmbState.Text = _order.State;
            }
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        string name = _txtBuyerName.Text.Trim();
        string street = _txtStreetAddress.Text.Trim();
        string city = _txtCity.Text.Trim();
        string zip = _txtPostalCode.Text.Trim();
        string country = _txtCountryCode.Text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Alıcı Adı boş bırakılamaz.");
            return;
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            ShowError("Sokak Adresi boş bırakılamaz.");
            return;
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            ShowError("Şehir boş bırakılamaz.");
            return;
        }

        if (string.IsNullOrWhiteSpace(zip))
        {
            ShowError("Posta Kodu boş bırakılamaz.");
            return;
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            ShowError("Ülke Kodu boş bırakılamaz (örn: US, DE).");
            return;
        }

        string rawState = _cmbState.Text.Trim();
        string stateCode = rawState;
        if (rawState.Contains(" - "))
        {
            stateCode = rawState.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();
        }

        if (UsStateHelper.RequiresState(country) && string.IsNullOrWhiteSpace(stateCode))
        {
            ShowError("ABD ve Kanada siparişlerinde Eyalet seçimi zorunludur.");
            return;
        }

        // Güncellemeleri siparişe uygula
        _order.BuyerName = name;
        _order.BuyerEmail = _txtBuyerEmail.Text.Trim();
        _order.Phone = _txtPhone.Text.Trim();
        _order.StreetAddress = street;
        _order.SecondAddress = _txtSecondAddress.Text.Trim();
        _order.City = city;
        _order.State = stateCode;
        _order.HasState = !string.IsNullOrWhiteSpace(stateCode);
        _order.PostalCode = zip;
        _order.CountryCode = country;
        _order.CountryName = !string.IsNullOrWhiteSpace(_txtCountryName.Text) ? _txtCountryName.Text.Trim() : country;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowError(string msg)
    {
        _lblValidationMsg.Text = "⚠️ " + msg;
        _lblValidationMsg.Visible = true;
    }
}
