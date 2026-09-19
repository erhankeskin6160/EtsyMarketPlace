namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using SimilarProductsWinForms.Controls;

/// <summary>
/// ShipEntegra canlı API ve akıllı yedek navlun fiyat hesaplama formu.
/// </summary>
internal sealed class ShipEntegraShippingCalculatorForm : Form
{
    private readonly ShipEntegraPricingService _pricingService = new(new ShipEntegraApiClient());
    private ShipEntegraSettings _settings = ShipEntegraSettingsStore.Load();

    // Seçilen teklif (Diğer formlardan çağrıldığında okunabilir)
    public ShipEntegraQuoteOffer? SelectedOffer { get; private set; }
    public Action<ShipEntegraQuoteOffer>? OnOfferSelected { get; set; }

    // Header & Status
    private readonly Label _lblTitle = new();
    private readonly Label _lblSubtitle = new();
    private readonly Label _badgeStatus = new();
    private readonly Panel _alertBanner = new();
    private readonly Label _lblAlertMessage = new();

    // Inputs
    private readonly ComboBox _cmbCountry = new();
    private readonly TextBox _txtPostalCode = new();
    private readonly NumericUpDown _numWeightKg = new();
    private readonly NumericUpDown _numWidthCm = new();
    private readonly NumericUpDown _numLengthCm = new();
    private readonly NumericUpDown _numHeightCm = new();
    private readonly Label _lblDesiSummary = new();
    private readonly Button _btnFetchQuotes = new();

    // Token Management
    private readonly TextBox _txtBearerToken = new();
    private readonly Button _btnSaveToken = new();

    // Offers Display
    private readonly FlowLayoutPanel _pnlOffers = new();

    public ShipEntegraShippingCalculatorForm()
    {
        InitializeComponent();
        LoadSavedSettings();
    }

    private void InitializeComponent()
    {
        Text = "ShipEntegra Uluslararası Kargo & Navlun Hesaplayıcı";
        Size = new Size(1100, 740);
        MinimumSize = new Size(950, 640);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(24)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Alert Banner
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));  // Token Bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Main Body (Split)

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildAlertBanner(), 0, 1);
        root.Controls.Add(BuildTokenBar(), 0, 2);
        root.Controls.Add(BuildMainBody(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        _lblTitle.Text = "📦 ShipEntegra Uluslararası Kargo & Navlun Hesaplayıcı";
        _lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _lblTitle.ForeColor = Color.White;
        _lblTitle.Location = new Point(0, 4);
        _lblTitle.AutoSize = true;
        panel.Controls.Add(_lblTitle);

        _lblSubtitle.Text = "api.shipentegra.com Canlı API: Amerika Eko Plus, Smart Express, Widect, Express ve UPS Teklifleri";
        _lblSubtitle.Font = new Font("Segoe UI", 9F);
        _lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184); // Slate 400
        _lblSubtitle.Location = new Point(2, 38);
        _lblSubtitle.AutoSize = true;
        panel.Controls.Add(_lblSubtitle);

        // Durum Rozeti
        _badgeStatus.Text = _settings.HasValidTokenFormat ? "🟢 Token Hazır" : "🟡 Yedek Fiyat Listesi";
        _badgeStatus.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _badgeStatus.BackColor = _settings.HasValidTokenFormat ? Color.FromArgb(6, 78, 59) : Color.FromArgb(120, 53, 15);
        _badgeStatus.ForeColor = _settings.HasValidTokenFormat ? Color.FromArgb(52, 211, 153) : Color.FromArgb(253, 230, 138);
        _badgeStatus.Padding = new Padding(10, 5, 10, 5);
        _badgeStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _badgeStatus.Location = new Point(panel.Width - 220, 10);
        _badgeStatus.AutoSize = true;
        panel.Controls.Add(_badgeStatus);

        panel.Resize += (_, _) =>
        {
            _badgeStatus.Location = new Point(panel.Width - _badgeStatus.Width - 10, 10);
        };

        return panel;
    }

    private Control BuildAlertBanner()
    {
        _alertBanner.Dock = DockStyle.Fill;
        _alertBanner.BackColor = Color.FromArgb(69, 26, 3); // Koyu Amber / Uyarı
        _alertBanner.Padding = new Padding(14, 10, 14, 10);
        _alertBanner.Margin = new Padding(0, 0, 0, 12);
        _alertBanner.Visible = false;

        _lblAlertMessage.Dock = DockStyle.Fill;
        _lblAlertMessage.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _lblAlertMessage.ForeColor = Color.FromArgb(254, 215, 170); // Açık Amber
        _lblAlertMessage.Text = "⚠️ ShipEntegra oturum tokeninizin süresi doldu! Gösterilen fiyatlar yedek listedir ve güncel olmayabilir. Lütfen tokeninizi yenileyin.";
        _alertBanner.Controls.Add(_lblAlertMessage);

        return _alertBanner;
    }

    private Control BuildTokenBar()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59), // Slate 800
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        var lblToken = new Label
        {
            Text = "ShipEntegra Tokeni:",
            ForeColor = Color.FromArgb(226, 232, 240),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8.5F)
        };
        layout.Controls.Add(lblToken, 0, 0);

        _txtBearerToken.Dock = DockStyle.Fill;
        _txtBearerToken.BackColor = Color.FromArgb(15, 23, 42);
        _txtBearerToken.ForeColor = Color.FromArgb(56, 189, 248);
        _txtBearerToken.Font = new Font("Consolas", 8.5F);
        _txtBearerToken.PlaceholderText = "app.shipentegra.com Network sekmesindeki Bearer ... kodunu buraya yapıştırın";
        layout.Controls.Add(_txtBearerToken, 1, 0);

        var btnPaste = new Button
        {
            Text = "📋 Yapıştır",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5F)
        };
        btnPaste.FlatAppearance.BorderSize = 0;
        btnPaste.Click += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                _txtBearerToken.Text = Clipboard.GetText().Trim();
            }
        };
        layout.Controls.Add(btnPaste, 2, 0);

        _btnSaveToken.Text = "💾 Tokeni Kaydet";
        _btnSaveToken.Dock = DockStyle.Fill;
        _btnSaveToken.BackColor = Color.FromArgb(37, 99, 235); // Royal Blue
        _btnSaveToken.ForeColor = Color.White;
        _btnSaveToken.FlatStyle = FlatStyle.Flat;
        _btnSaveToken.Cursor = Cursors.Hand;
        _btnSaveToken.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnSaveToken.FlatAppearance.BorderSize = 0;
        _btnSaveToken.Click += async (_, _) => await SaveAndTestTokenAsync();
        layout.Controls.Add(_btnSaveToken, 3, 0);

        var lblHelp = new Label
        {
            Text = "💡 İpucu: app.shipentegra.com adresinde F12 (Network) -> Fiyat Hesapla yaptığınızda Request Headers altındaki Authorization: Bearer kodudur.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8F)
        };
        layout.Controls.Add(lblHelp, 1, 1);
        layout.SetColumnSpan(lblHelp, 3);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildMainBody()
    {
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380)); // Sol Parametreler
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Sağ Teklifler

        split.Controls.Add(BuildInputPanel(), 0, 0);
        split.Controls.Add(BuildOffersPanel(), 1, 0);

        return split;
    }

    private Control BuildInputPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 16, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Hedef Ülke
        layout.Controls.Add(new Label { Text = "Hedef Ülke:", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _cmbCountry.Dock = DockStyle.Fill;
        _cmbCountry.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCountry.BackColor = Color.FromArgb(15, 23, 42);
        _cmbCountry.ForeColor = Color.White;
        _cmbCountry.Items.AddRange(
        [
            "US - Amerika Birleşik Devletleri",
            "CA - Kanada",
            "GB - Birleşik Krallık",
            "DE - Almanya",
            "FR - Fransa",
            "IT - İtalya",
            "ES - İspanya",
            "NL - Hollanda",
            "AU - Avustralya"
        ]);
        _cmbCountry.SelectedIndex = 0;
        layout.Controls.Add(_cmbCountry, 1, 0);

        // Posta Kodu
        layout.Controls.Add(new Label { Text = "Posta Kodu:", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _txtPostalCode.Dock = DockStyle.Fill;
        _txtPostalCode.BackColor = Color.FromArgb(15, 23, 42);
        _txtPostalCode.ForeColor = Color.White;
        _txtPostalCode.Text = "8537";
        layout.Controls.Add(_txtPostalCode, 1, 1);

        // Ağırlık (kg)
        layout.Controls.Add(new Label { Text = "Ağırlık (kg):", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        _numWeightKg.Dock = DockStyle.Fill;
        _numWeightKg.BackColor = Color.FromArgb(15, 23, 42);
        _numWeightKg.ForeColor = Color.White;
        _numWeightKg.DecimalPlaces = 2;
        _numWeightKg.Increment = 0.05m;
        _numWeightKg.Minimum = 0.01m;
        _numWeightKg.Maximum = 70.00m;
        _numWeightKg.Value = 0.40m;
        _numWeightKg.ValueChanged += (_, _) => UpdateDesiSummary();
        layout.Controls.Add(_numWeightKg, 1, 2);

        // Genişlik / En (cm)
        layout.Controls.Add(new Label { Text = "Genişlik/En (cm):", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        _numWidthCm.Dock = DockStyle.Fill;
        _numWidthCm.BackColor = Color.FromArgb(15, 23, 42);
        _numWidthCm.ForeColor = Color.White;
        _numWidthCm.DecimalPlaces = 1;
        _numWidthCm.Minimum = 1.0m;
        _numWidthCm.Maximum = 200.0m;
        _numWidthCm.Value = 15.0m;
        _numWidthCm.ValueChanged += (_, _) => UpdateDesiSummary();
        layout.Controls.Add(_numWidthCm, 1, 3);

        // Uzunluk / Boy (cm)
        layout.Controls.Add(new Label { Text = "Uzunluk/Boy (cm):", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
        _numLengthCm.Dock = DockStyle.Fill;
        _numLengthCm.BackColor = Color.FromArgb(15, 23, 42);
        _numLengthCm.ForeColor = Color.White;
        _numLengthCm.DecimalPlaces = 1;
        _numLengthCm.Minimum = 1.0m;
        _numLengthCm.Maximum = 200.0m;
        _numLengthCm.Value = 20.0m;
        _numLengthCm.ValueChanged += (_, _) => UpdateDesiSummary();
        layout.Controls.Add(_numLengthCm, 1, 4);

        // Yükseklik (cm)
        layout.Controls.Add(new Label { Text = "Yükseklik (cm):", ForeColor = Color.FromArgb(203, 213, 225), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
        _numHeightCm.Dock = DockStyle.Fill;
        _numHeightCm.BackColor = Color.FromArgb(15, 23, 42);
        _numHeightCm.ForeColor = Color.White;
        _numHeightCm.DecimalPlaces = 1;
        _numHeightCm.Minimum = 1.0m;
        _numHeightCm.Maximum = 200.0m;
        _numHeightCm.Value = 10.0m;
        _numHeightCm.ValueChanged += (_, _) => UpdateDesiSummary();
        layout.Controls.Add(_numHeightCm, 1, 5);

        // Desi Özeti
        _lblDesiSummary.Dock = DockStyle.Fill;
        _lblDesiSummary.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
        _lblDesiSummary.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _lblDesiSummary.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_lblDesiSummary, 0, 6);
        layout.SetColumnSpan(_lblDesiSummary, 2);

        // Sorgula Butonu
        _btnFetchQuotes.Text = "🚀 Canlı Teklifleri Getir";
        _btnFetchQuotes.Dock = DockStyle.Fill;
        _btnFetchQuotes.Height = 44;
        _btnFetchQuotes.BackColor = Color.FromArgb(16, 185, 129); // Emerald 500
        _btnFetchQuotes.ForeColor = Color.White;
        _btnFetchQuotes.FlatStyle = FlatStyle.Flat;
        _btnFetchQuotes.Cursor = Cursors.Hand;
        _btnFetchQuotes.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _btnFetchQuotes.FlatAppearance.BorderSize = 0;
        _btnFetchQuotes.Click += async (_, _) => await FetchQuotesAsync();
        layout.Controls.Add(_btnFetchQuotes, 0, 7);
        layout.SetColumnSpan(_btnFetchQuotes, 2);

        card.Controls.Add(layout);
        UpdateDesiSummary();

        return card;
    }

    private Control BuildOffersPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(18),
            AutoScroll = true
        };

        var title = new Label
        {
            Text = "Mevcut ShipEntegra Navlun Teklifleri",
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 32
        };
        card.Controls.Add(title);

        _pnlOffers.Dock = DockStyle.Fill;
        _pnlOffers.FlowDirection = FlowDirection.TopDown;
        _pnlOffers.WrapContents = false;
        _pnlOffers.AutoScroll = true;
        _pnlOffers.Padding = new Padding(0, 8, 0, 0);
        card.Controls.Add(_pnlOffers);

        ShowPlaceholderOffer("Koli ölçülerinizi belirleyip 'Canlı Teklifleri Getir' butonuna basarak anlık ShipEntegra Eko Plus, Smart Express ve UPS tekliflerini çekebilirsiniz.");

        return card;
    }

    private void UpdateDesiSummary()
    {
        double w = (double)_numWidthCm.Value;
        double l = (double)_numLengthCm.Value;
        double h = (double)_numHeightCm.Value;
        double weight = (double)_numWeightKg.Value;

        double desi = Math.Round((w * l * h) / 5000.0, 2);
        double billable = Math.Max(weight, desi);

        _lblDesiSummary.Text = $"📊 Desi: {desi:N2} | Gerçek: {weight:N2} kg | Faturalandırılan: {billable:N2} kg";
    }

    private async Task FetchQuotesAsync()
    {
        _btnFetchQuotes.Enabled = false;
        _btnFetchQuotes.Text = "⏳ Fiyatlar Hesaplanıyor...";
        Cursor = Cursors.WaitCursor;

        try
        {
            // Kullanıcı kutucuğa yeni token yapıştırdıysa otomatik olarak kaydet
            string enteredToken = _txtBearerToken.Text.Trim();
            if (!string.IsNullOrWhiteSpace(enteredToken) && _settings.BearerToken != enteredToken)
            {
                _settings.BearerToken = enteredToken;
                _settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                ShipEntegraSettingsStore.Save(_settings);
            }

            string countryCode = _cmbCountry.Text[..2].Trim();
            var req = new ShipEntegraQuoteRequest
            {
                ReceiverCountry = countryCode,
                ReceiverPostalCode = _txtPostalCode.Text.Trim(),
                WeightKg = (double)_numWeightKg.Value,
                WidthCm = (double)_numWidthCm.Value,
                LengthCm = (double)_numLengthCm.Value,
                HeightCm = (double)_numHeightCm.Value,
                Currency = "USD"
            };

            var result = await _pricingService.GetQuotesAsync(req);

            // Durum Rozeti ve Uyarı Bannerı
            if (result.IsLive)
            {
                _badgeStatus.Text = "🟢 Canlı ShipEntegra API Bağlı";
                _badgeStatus.BackColor = Color.FromArgb(6, 78, 59); // Koyu Yeşil
                _badgeStatus.ForeColor = Color.FromArgb(52, 211, 153);
                _alertBanner.Visible = false;
            }
            else if (result.TokenExpired)
            {
                _badgeStatus.Text = "🔴 Oturum Süresi Doldu (Token Yenileyin)";
                _badgeStatus.BackColor = Color.FromArgb(127, 29, 29); // Koyu Kırmızı
                _badgeStatus.ForeColor = Color.FromArgb(254, 202, 202);

                _alertBanner.Visible = true;
                _alertBanner.BackColor = Color.FromArgb(69, 26, 3);
                _lblAlertMessage.ForeColor = Color.FromArgb(254, 215, 170);
                _lblAlertMessage.Text = result.StatusMessage;
            }
            else
            {
                _badgeStatus.Text = "🟡 Yedek Fiyat Listesi";
                _badgeStatus.BackColor = Color.FromArgb(120, 53, 15);
                _badgeStatus.ForeColor = Color.FromArgb(253, 230, 138);

                if (!string.IsNullOrWhiteSpace(result.StatusMessage))
                {
                    _alertBanner.Visible = true;
                    _alertBanner.BackColor = Color.FromArgb(40, 45, 60);
                    _lblAlertMessage.ForeColor = Color.FromArgb(203, 213, 225);
                    _lblAlertMessage.Text = result.StatusMessage;
                }
                else
                {
                    _alertBanner.Visible = false;
                }
            }

            RenderOfferCards(result.Offers);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kargo fiyatı sorgulanırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnFetchQuotes.Enabled = true;
            _btnFetchQuotes.Text = "🚀 Canlı Teklifleri Getir";
            Cursor = Cursors.Default;
        }
    }

    private void RenderOfferCards(System.Collections.Generic.List<ShipEntegraQuoteOffer> offers)
    {
        _pnlOffers.Controls.Clear();

        if (offers.Count == 0)
        {
            ShowPlaceholderOffer("Bu rota ve ölçüler için uygun teklif bulunamadı.");
            return;
        }

        foreach (var offer in offers)
        {
            var card = new Panel
            {
                Width = 590,
                Height = 115,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 12)
            };

            // Servis Adı
            var lblCarrier = new Label
            {
                Text = offer.ClearServiceName,
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = offer.IsBestCarrier ? Color.FromArgb(52, 211, 153) : Color.FromArgb(56, 189, 248),
                Location = new Point(16, 12),
                AutoSize = true
            };
            card.Controls.Add(lblCarrier);

            // En Uygun Rozeti
            if (offer.IsBestCarrier)
            {
                var lblBest = new Label
                {
                    Text = "🟢 En Uygun",
                    Font = new Font("Segoe UI Bold", 7.5F),
                    BackColor = Color.FromArgb(6, 78, 59),
                    ForeColor = Color.FromArgb(52, 211, 153),
                    Padding = new Padding(6, 2, 6, 2),
                    Location = new Point(lblCarrier.Right + 12, 12),
                    AutoSize = true
                };
                card.Controls.Add(lblBest);
            }

            // Teslimat Süresi
            var lblDelivery = new Label
            {
                Text = $"⏱️ Tahmini Teslimat: {offer.DeliveryDaysText}",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(16, 38),
                AutoSize = true
            };
            card.Controls.Add(lblDelivery);

            // Maliyet Kırılımı (Navlun + Yakıt)
            var lblBreakdown = new Label
            {
                Text = offer.FuelCost > 0
                    ? $"Kargo: ${offer.CargoPrice:N2} + Yakıt: ${offer.FuelCost:N2}"
                    : $"Servis: {offer.ServiceType}",
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(16, 62),
                AutoSize = true
            };
            card.Controls.Add(lblBreakdown);

            // Canlı / Yedek Etiketi
            var lblSource = new Label
            {
                Text = offer.IsLivePrice ? "🟢 Canlı Teklif" : "🟡 Yedek Liste Fiyatı",
                Font = new Font("Segoe UI", 8F),
                ForeColor = offer.IsLivePrice ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36),
                Location = new Point(16, 85),
                AutoSize = true
            };
            card.Controls.Add(lblSource);

            // Toplam Fiyat (Büyük)
            var lblPrice = new Label
            {
                Text = $"${offer.TotalPrice:N2} {offer.Currency}",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(275, 18),
                AutoSize = true
            };
            card.Controls.Add(lblPrice);

            // "Kâr Formuna Aktar" Butonu
            var btnSelect = new Button
            {
                Text = "Kâr Formuna Aktar ➜",
                Size = new Size(150, 36),
                Location = new Point(420, 22),
                BackColor = Color.FromArgb(37, 99, 235), // Blue
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
            };
            btnSelect.FlatAppearance.BorderSize = 0;
            btnSelect.Click += (_, _) =>
            {
                SelectedOffer = offer;
                OnOfferSelected?.Invoke(offer);
                MessageBox.Show(
                    $"${offer.TotalPrice:N2} {offer.Currency} kargo maliyeti ({offer.ClearServiceName}) seçildi ve kâr hesaplayıcıya aktarılmak üzere hazırlandı!",
                    "Teklif Seçildi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            };
            card.Controls.Add(btnSelect);

            _pnlOffers.Controls.Add(card);
        }
    }

    private void ShowPlaceholderOffer(string message)
    {
        _pnlOffers.Controls.Clear();
        var lbl = new Label
        {
            Text = message,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12),
            AutoSize = true
        };
        _pnlOffers.Controls.Add(lbl);
    }

    private async Task SaveAndTestTokenAsync()
    {
        string token = _txtBearerToken.Text.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Lütfen geçerli bir ShipEntegra tokeni yapıştırın.", "Token Boş", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.BearerToken = token;
        _settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        ShipEntegraSettingsStore.Save(_settings);

        MessageBox.Show("ShipEntegra oturum tokeniniz kaydedildi! Şimdi 'Canlı Teklifleri Getir' butonuna basarak test edebilirsiniz.", "Token Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await FetchQuotesAsync();
    }

    private void LoadSavedSettings()
    {
        _settings = ShipEntegraSettingsStore.Load();
        _txtBearerToken.Text = _settings.BearerToken ?? string.Empty;
        _numWeightKg.Value = (decimal)_settings.DefaultWeightKg;
        _numWidthCm.Value = (decimal)_settings.DefaultWidthCm;
        _numLengthCm.Value = (decimal)_settings.DefaultLengthCm;
        _numHeightCm.Value = (decimal)_settings.DefaultHeightCm;

        for (int i = 0; i < _cmbCountry.Items.Count; i++)
        {
            if (_cmbCountry.Items[i]?.ToString()?.StartsWith(_settings.DefaultCountry, StringComparison.OrdinalIgnoreCase) == true)
            {
                _cmbCountry.SelectedIndex = i;
                break;
            }
        }
    }
}
