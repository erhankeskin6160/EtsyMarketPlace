namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using SimilarProductsWinForms.Controls;

/// <summary>
/// Aras Global canlı API ve akıllı yedek navlun fiyat hesaplama formu.
/// </summary>
internal sealed class ArasGlobalShippingCalculatorForm : Form
{
    private readonly ArasGlobalPricingService _pricingService = new(new ArasGlobalApiClient());
    private ArasGlobalSettings _settings = ArasGlobalSettingsStore.Load();

    // Seçilen teklif (Diğer formlardan çağrıldığında okunabilir)
    public ArasGlobalQuoteOffer? SelectedOffer { get; private set; }
    public Action<ArasGlobalQuoteOffer>? OnOfferSelected { get; set; }

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
    private readonly Panel _pnlTokenContainer = new();

    // Offers Display
    private readonly FlowLayoutPanel _pnlOffers = new();

    public ArasGlobalShippingCalculatorForm()
    {
        InitializeComponent();
        LoadSavedSettings();
    }

    private void InitializeComponent()
    {
        Text = "Aras Global Uluslararası Kargo & Navlun Hesaplayıcı";
        Size = new Size(1100, 720);
        MinimumSize = new Size(950, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 18, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Alert Banner
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // Token Bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Main Body (2 Columns)

        // 1. Header
        root.Controls.Add(BuildHeader(), 0, 0);

        // 2. Alert Banner (Token süresi bittiğinde çıkar)
        root.Controls.Add(BuildAlertBanner(), 0, 1);

        // 3. Token Bar
        root.Controls.Add(BuildTokenBar(), 0, 2);

        // 4. Main Body: Sol Form + Sağ Teklif Kartları
        root.Controls.Add(BuildMainBody(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var pnl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titleBox = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        _lblTitle.Text = "📦 Aras Global Uluslararası Kargo Hesaplayıcı";
        _lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _lblTitle.ForeColor = Color.White;
        _lblTitle.AutoSize = true;
        titleBox.Controls.Add(_lblTitle);

        _lblSubtitle.Text = "api.arasglobalcargo.com Canlı API & Widect, UPS, FedEx Anlık Navlun Teklifleri";
        _lblSubtitle.Font = new Font("Segoe UI", 9F);
        _lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
        _lblSubtitle.AutoSize = true;
        titleBox.Controls.Add(_lblSubtitle);

        pnl.Controls.Add(titleBox, 0, 0);

        // Status Badge
        _badgeStatus.Text = "⚪ Durum Kontrol Ediliyor...";
        _badgeStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _badgeStatus.ForeColor = Color.FromArgb(203, 213, 225);
        _badgeStatus.BackColor = Color.FromArgb(30, 41, 59);
        _badgeStatus.Padding = new Padding(12, 6, 12, 6);
        _badgeStatus.AutoSize = true;
        _badgeStatus.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        pnl.Controls.Add(_badgeStatus, 1, 0);

        return pnl;
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
        _lblAlertMessage.Text = "⚠️ Aras Global oturum tokeninizin süresi doldu! Gösterilen fiyatlar yedek listedir ve güncel olmayabilir. Lütfen tokeninizi yenileyin.";
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
            Text = "Aras Oturum Tokeni:",
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
        _txtBearerToken.PlaceholderText = "panel.arasglobalcargo.com Network sekmesindeki Bearer eyJhbGciOiJSUzI1... anahtarınızı buraya yapıştırın";
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
            Text = "💡 İpucu: panel.arasglobalcargo.com adresinde F12 (Network) -> Hızlı Fiyat Hesapla yaptığınızda Request Headers altındaki Authorization: Bearer kodudur.",
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
            Margin = new Padding(0, 0, 14, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 1. Hedef Ülke
        layout.Controls.Add(CreateFieldLabel("Hedef Ülke:"), 0, 0);
        _cmbCountry.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCountry.Dock = DockStyle.Fill;
        _cmbCountry.BackColor = Color.FromArgb(15, 23, 42);
        _cmbCountry.ForeColor = Color.White;
        _cmbCountry.Font = new Font("Segoe UI", 9.5F);
        _cmbCountry.Items.AddRange([
            "US - Amerika Birleşik Devletleri",
            "GB - Birleşik Krallık (İngiltere)",
            "DE - Almanya",
            "FR - Fransa",
            "CA - Kanada",
            "AU - Avustralya",
            "ES - İspanya",
            "IT - İtalya",
            "NL - Hollanda",
            "IE - İrlanda",
            "BE - Belçika"
        ]);
        _cmbCountry.SelectedIndex = 0;
        layout.Controls.Add(_cmbCountry, 1, 0);

        // 2. Posta Kodu
        layout.Controls.Add(CreateFieldLabel("Posta Kodu:"), 0, 1);
        _txtPostalCode.Dock = DockStyle.Fill;
        _txtPostalCode.BackColor = Color.FromArgb(15, 23, 42);
        _txtPostalCode.ForeColor = Color.White;
        _txtPostalCode.Text = "8537";
        layout.Controls.Add(_txtPostalCode, 1, 1);

        // 3. Ağırlık (kg)
        layout.Controls.Add(CreateFieldLabel("Ağırlık (kg):"), 0, 2);
        ConfigureNumericInput(_numWeightKg, 0.05m, 50.0m, 0.40m, 2);
        layout.Controls.Add(_numWeightKg, 1, 2);

        // 4. En (cm)
        layout.Controls.Add(CreateFieldLabel("Genişlik/En (cm):"), 0, 3);
        ConfigureNumericInput(_numWidthCm, 1.0m, 200.0m, 15.0m, 1);
        layout.Controls.Add(_numWidthCm, 1, 3);

        // 5. Boy (cm)
        layout.Controls.Add(CreateFieldLabel("Uzunluk/Boy (cm):"), 0, 4);
        ConfigureNumericInput(_numLengthCm, 1.0m, 200.0m, 20.0m, 1);
        layout.Controls.Add(_numLengthCm, 1, 4);

        // 6. Yükseklik (cm)
        layout.Controls.Add(CreateFieldLabel("Yükseklik (cm):"), 0, 5);
        ConfigureNumericInput(_numHeightCm, 1.0m, 200.0m, 10.0m, 1);
        layout.Controls.Add(_numHeightCm, 1, 5);

        // 7. Desi Bilgi Rozeti
        _lblDesiSummary.Dock = DockStyle.Fill;
        _lblDesiSummary.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _lblDesiSummary.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
        _lblDesiSummary.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_lblDesiSummary, 0, 6);
        layout.SetColumnSpan(_lblDesiSummary, 2);

        // Ebatlar değiştikçe desi özetini güncelle
        _numWeightKg.ValueChanged += (_, _) => UpdateDesiSummary();
        _numWidthCm.ValueChanged += (_, _) => UpdateDesiSummary();
        _numLengthCm.ValueChanged += (_, _) => UpdateDesiSummary();
        _numHeightCm.ValueChanged += (_, _) => UpdateDesiSummary();

        // 8. Fiyat Getir Butonu
        _btnFetchQuotes.Text = "🚀 Canlı Teklifleri Getir";
        _btnFetchQuotes.Dock = DockStyle.Fill;
        _btnFetchQuotes.Height = 42;
        _btnFetchQuotes.BackColor = Color.FromArgb(16, 185, 129); // Emerald Green
        _btnFetchQuotes.ForeColor = Color.White;
        _btnFetchQuotes.FlatStyle = FlatStyle.Flat;
        _btnFetchQuotes.Cursor = Cursors.Hand;
        _btnFetchQuotes.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
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
            Text = "Mevcut Navlun Teklifleri",
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

        // İlk boş karşılama kartı
        ShowPlaceholderOffer("Koli ölçülerinizi belirleyip 'Canlı Teklifleri Getir' butonuna basarak anlık Widect ve UPS/FedEx tekliflerini çekebilirsiniz.");

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
                ArasGlobalSettingsStore.Save(_settings);
            }

            string countryCode = _cmbCountry.Text[..2].Trim();
            var req = new ArasGlobalQuoteRequest
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
                _badgeStatus.Text = "🟢 Canlı Aras API Bağlı";
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

    private void RenderOfferCards(System.Collections.Generic.List<ArasGlobalQuoteOffer> offers)
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
                Width = 580,
                Height = 110,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 12)
            };

            // Taşıyıcı adı ve Servis
            var lblCarrier = new Label
            {
                Text = $"{offer.Cargo} - {offer.ProviderServiceType}",
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = offer.Cargo.Equals("Widect", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(56, 189, 248) : Color.FromArgb(251, 146, 60),
                Location = new Point(16, 14),
                AutoSize = true
            };
            card.Controls.Add(lblCarrier);

            // Teslimat Süresi
            var lblDelivery = new Label
            {
                Text = $"⏱️ Tahmini Teslimat: {offer.DeliveryDaysText}",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(16, 40),
                AutoSize = true
            };
            card.Controls.Add(lblDelivery);

            // Canlı / Yedek Etiketi
            var lblSource = new Label
            {
                Text = offer.IsLivePrice ? "🟢 Canlı Teklif" : "🟡 Yedek Liste Fiyatı",
                Font = new Font("Segoe UI", 8F),
                ForeColor = offer.IsLivePrice ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36),
                Location = new Point(16, 68),
                AutoSize = true
            };
            card.Controls.Add(lblSource);

            // Fiyat (Büyük)
            var lblPrice = new Label
            {
                Text = $"${offer.Price:N2} {offer.Currency}",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(280, 16),
                AutoSize = true
            };
            card.Controls.Add(lblPrice);

            // "Seç & Kâr Formuna Aktar" Butonu
            var btnSelect = new Button
            {
                Text = "Kâr Formuna Aktar ➜",
                Size = new Size(150, 36),
                Location = new Point(410, 20),
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
                    $"${offer.Price:N2} {offer.Currency} kargo maliyeti seçildi ve kâr hesaplayıcıya aktarılmak üzere hazırlandı!",
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
            MessageBox.Show("Lütfen geçerli bir Bearer token yapıştırın.", "Token Boş", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.BearerToken = token;
        _settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        ArasGlobalSettingsStore.Save(_settings);

        MessageBox.Show("Aras Global oturum tokeniniz kaydedildi! Şimdi 'Canlı Teklifleri Getir' butonuna basarak test edebilirsiniz.", "Token Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await FetchQuotesAsync();
    }

    private void LoadSavedSettings()
    {
        _settings = ArasGlobalSettingsStore.Load();
        _txtBearerToken.Text = _settings.BearerToken ?? string.Empty;
        _numWeightKg.Value = (decimal)_settings.DefaultWeightKg;
        _numWidthCm.Value = (decimal)_settings.DefaultWidthCm;
        _numLengthCm.Value = (decimal)_settings.DefaultLengthCm;
        _numHeightCm.Value = (decimal)_settings.DefaultHeightCm;

        if (_settings.HasValidTokenFormat)
        {
            _badgeStatus.Text = "🟢 Token Kayıtlı";
            _badgeStatus.BackColor = Color.FromArgb(6, 78, 59);
            _badgeStatus.ForeColor = Color.FromArgb(52, 211, 153);
        }
        else
        {
            _badgeStatus.Text = "🔴 Token Eksik (Yedek Fiyat Modu)";
            _badgeStatus.BackColor = Color.FromArgb(127, 29, 29);
            _badgeStatus.ForeColor = Color.FromArgb(254, 202, 202);
            _alertBanner.Visible = true;
            _lblAlertMessage.Text = "⚠️ Aras Global tokeniniz henüz girilmemiş! Canlı fiyatlar için lütfen tokeninizi yapıştırıp kaydedin.";
        }
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        ForeColor = Color.FromArgb(203, 213, 225),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 8.5F)
    };

    private static void ConfigureNumericInput(NumericUpDown num, decimal min, decimal max, decimal val, int dec)
    {
        num.Minimum = min;
        num.Maximum = max;
        num.DecimalPlaces = dec;
        num.Value = val;
        num.Dock = DockStyle.Fill;
        num.BackColor = Color.FromArgb(15, 23, 42);
        num.ForeColor = Color.White;
        num.Font = new Font("Segoe UI", 9.5F);
    }
}
