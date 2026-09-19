namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Kâr Simülatörü içine entegre, aşağı doğru pürüzsüz animasyonla açılan
/// Aras Global & ShipEntegra canlı kargo karşılaştırma ve seçim çekmecesi.
/// </summary>
public sealed class AnimatedShippingComparisonDrawer : Panel
{
    public event Action<decimal, string>? OnOfferSelected;
    public event Action<bool>? OnExpansionChanged;

    private readonly Timer _animTimer = new() { Interval = 16 };
    private int _targetHeight = 0;
    private const int ExpandedHeight = 520;
    private bool _isExpanded = false;

    // Girdi alanları
    private readonly ComboBox _cbCountry = new();
    private readonly NumericUpDown _numWeight = new();
    private readonly NumericUpDown _numWidth = new();
    private readonly NumericUpDown _numLength = new();
    private readonly NumericUpDown _numHeight = new();
    private readonly Label _lblDesi = new();
    private readonly Button _btnFetchQuotes = new();
    private readonly Button _btnClose = new();
    private readonly Label _lblStatus = new();

    // Filtreleme ve Sıralama
    private readonly ComboBox _cbSort = new();
    private readonly RadioButton _rbAll = new();
    private readonly RadioButton _rbAras = new();
    private readonly RadioButton _rbShipEntegra = new();

    // Teklif Listesi Paneli
    private readonly FlowLayoutPanel _pnlOffersList = new();

    // Servisler
    private readonly ArasGlobalPricingService _arasService = new();
    private readonly ShipEntegraPricingService _shipEntegraService = new();

    // Canlı Döviz Kuru
    public decimal UsdTryRate { get; set; } = 48.26m;

    // Teklif Listesi Önbelleği
    private readonly List<UnifiedShippingQuote> _loadedQuotes = new();

    public bool IsExpanded => _isExpanded;

    public AnimatedShippingComparisonDrawer()
    {
        Dock = DockStyle.Top;
        Height = 0;
        Visible = false;
        BackColor = Color.FromArgb(17, 24, 39); // Gray 900
        Padding = new Padding(16);
        DoubleBuffered = true;

        _animTimer.Tick += AnimTimer_Tick;

        BuildLayout();
        CalculateDesi();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        int diff = _targetHeight - Height;
        if (Math.Abs(diff) <= 12)
        {
            Height = _targetHeight;
            _animTimer.Stop();
            if (_targetHeight == 0)
            {
                Visible = false;
            }
            OnExpansionChanged?.Invoke(_isExpanded);
        }
        else
        {
            // Smooth ease-out lerp
            Height += (int)(diff * 0.28f);
        }
    }

    public void ToggleDrawer()
    {
        if (_isExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    public void Expand()
    {
        _isExpanded = true;
        Visible = true;
        _targetHeight = ExpandedHeight;
        _animTimer.Start();
        if (_loadedQuotes.Count == 0)
        {
            _ = FetchAllQuotesAsync();
        }
    }

    public void Collapse()
    {
        _isExpanded = false;
        _targetHeight = 0;
        _animTimer.Start();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 0: Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // 1: Inputs
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 2: Filter & Sort Bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 3: Offers List

        // 0: Header
        mainLayout.Controls.Add(BuildHeaderRow(), 0, 0);

        // 1: Inputs
        mainLayout.Controls.Add(BuildInputsRow(), 0, 1);

        // 2: Filter & Sort Bar
        mainLayout.Controls.Add(BuildFilterAndSortRow(), 0, 2);

        // 3: Offers List Container
        _pnlOffersList.Dock = DockStyle.Fill;
        _pnlOffersList.AutoScroll = true;
        _pnlOffersList.FlowDirection = FlowDirection.TopDown;
        _pnlOffersList.WrapContents = false;
        _pnlOffersList.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        _pnlOffersList.Padding = new Padding(8);
        mainLayout.Controls.Add(_pnlOffersList, 0, 3);

        Controls.Add(mainLayout);
    }

    private Control BuildHeaderRow()
    {
        var pnl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var lblTitle = new Label
        {
            Text = "📦 Canlı Kargo Karşılaştırma & Seçim Paneli (Aras Global & ShipEntegra)",
            ForeColor = Color.FromArgb(56, 189, 248), // Cyan
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnl.Controls.Add(lblTitle, 0, 0);

        _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
        _lblStatus.Font = new Font("Segoe UI", 9F);
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.TextAlign = ContentAlignment.MiddleRight;
        pnl.Controls.Add(_lblStatus, 1, 0);

        _btnClose.Text = "▲ Kapat";
        _btnClose.Dock = DockStyle.Fill;
        _btnClose.BackColor = Color.FromArgb(51, 65, 85);
        _btnClose.ForeColor = Color.White;
        _btnClose.FlatStyle = FlatStyle.Flat;
        _btnClose.Cursor = Cursors.Hand;
        _btnClose.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Click += (_, _) => Collapse();
        pnl.Controls.Add(_btnClose, 2, 0);

        return pnl;
    }

    private Control BuildInputsRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59), // Slate 800
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 0, 6)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Ülke
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));  // Ağırlık
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // En
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Boy
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Yükseklik
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Desi Rozeti
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // Teklif Butonu

        // 1. Ülke
        layout.Controls.Add(CreateFieldWrapper("Hedef Ülke:", _cbCountry), 0, 0);
        PopulateCountries();

        // 2. Ağırlık
        ConfigureNumeric(_numWeight, 0.01m, 70m, 0.40m, 2);
        layout.Controls.Add(CreateFieldWrapper("Ağırlık (kg):", _numWeight), 1, 0);

        // 3. En
        ConfigureNumeric(_numWidth, 1m, 200m, 15m, 1);
        layout.Controls.Add(CreateFieldWrapper("En (cm):", _numWidth), 2, 0);

        // 4. Boy
        ConfigureNumeric(_numLength, 1m, 200m, 20m, 1);
        layout.Controls.Add(CreateFieldWrapper("Boy (cm):", _numLength), 3, 0);

        // 5. Yükseklik
        ConfigureNumeric(_numHeight, 1m, 200m, 10m, 1);
        layout.Controls.Add(CreateFieldWrapper("Yükseklik (cm):", _numHeight), 4, 0);

        // Değişiklik dinleyicileri
        _numWidth.ValueChanged += (_, _) => CalculateDesi();
        _numLength.ValueChanged += (_, _) => CalculateDesi();
        _numHeight.ValueChanged += (_, _) => CalculateDesi();
        _numWeight.ValueChanged += (_, _) => CalculateDesi();

        // 6. Desi etiketi
        _lblDesi.Dock = DockStyle.Fill;
        _lblDesi.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
        _lblDesi.Font = new Font("Segoe UI Semibold", 8.5F);
        _lblDesi.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_lblDesi, 5, 0);

        // 7. Karşılaştır Butonu
        _btnFetchQuotes.Text = "⚡ Teklifleri Getir";
        _btnFetchQuotes.Dock = DockStyle.Fill;
        _btnFetchQuotes.BackColor = Color.FromArgb(16, 185, 129); // Emerald 500
        _btnFetchQuotes.ForeColor = Color.White;
        _btnFetchQuotes.FlatStyle = FlatStyle.Flat;
        _btnFetchQuotes.Cursor = Cursors.Hand;
        _btnFetchQuotes.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _btnFetchQuotes.FlatAppearance.BorderSize = 0;
        _btnFetchQuotes.Click += async (_, _) => await FetchAllQuotesAsync();
        layout.Controls.Add(_btnFetchQuotes, 6, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildFilterAndSortRow()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Tümü
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Aras
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // ShipEntegra
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Boşluk
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Sıralama Dropdown

        ConfigureRadio(_rbAll, "Tümü (Karşılaştır)", true);
        ConfigureRadio(_rbAras, "🚚 Sadece Aras Global", false);
        ConfigureRadio(_rbShipEntegra, "📦 Sadece ShipEntegra", false);

        _rbAll.CheckedChanged += (_, _) => RenderFilteredOffers();
        _rbAras.CheckedChanged += (_, _) => RenderFilteredOffers();
        _rbShipEntegra.CheckedChanged += (_, _) => RenderFilteredOffers();

        layout.Controls.Add(_rbAll, 0, 0);
        layout.Controls.Add(_rbAras, 1, 0);
        layout.Controls.Add(_rbShipEntegra, 2, 0);

        // Sıralama
        _cbSort.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbSort.BackColor = Color.FromArgb(30, 41, 59);
        _cbSort.ForeColor = Color.White;
        _cbSort.Font = new Font("Segoe UI", 8.5F);
        _cbSort.Dock = DockStyle.Fill;
        _cbSort.Items.Add("🔽 En Ucuzdan Pahalıya (Fiyat Artan)");
        _cbSort.Items.Add("🔼 En Pahalıdan Ucuza (Fiyat Azalan)");
        _cbSort.Items.Add("⚡ En Hızlı Teslimat Süresi");
        _cbSort.SelectedIndex = 0;
        _cbSort.SelectedIndexChanged += (_, _) => RenderFilteredOffers();

        var sortWrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        sortWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
        sortWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblSort = new Label
        {
            Text = "Sırala:",
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8.5F)
        };
        sortWrapper.Controls.Add(lblSort, 0, 0);
        sortWrapper.Controls.Add(_cbSort, 1, 0);

        layout.Controls.Add(sortWrapper, 4, 0);

        bar.Controls.Add(layout);
        return bar;
    }

    private static void ConfigureRadio(RadioButton rb, string text, bool isChecked)
    {
        rb.Text = text;
        rb.Checked = isChecked;
        rb.ForeColor = Color.FromArgb(226, 232, 240);
        rb.Font = new Font("Segoe UI Semibold", 8.5F);
        rb.Dock = DockStyle.Fill;
        rb.Cursor = Cursors.Hand;
    }

    private void CalculateDesi()
    {
        decimal w = _numWidth.Value;
        decimal l = _numLength.Value;
        decimal h = _numHeight.Value;
        decimal weight = _numWeight.Value;

        decimal desi = Math.Round((w * l * h) / 5000m, 2);
        decimal chargeable = Math.Max(desi, weight);

        _lblDesi.Text = $"📐 Desi: {desi:0.00} | Faturalandırılacak: {chargeable:0.00} kg";
    }

    public async Task FetchAllQuotesAsync()
    {
        _btnFetchQuotes.Enabled = false;
        _btnFetchQuotes.Text = "⏳ Fiyatlar Çekiliyor...";
        _lblStatus.Text = "Canlı API'ler sorgulanıyor...";
        _lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

        _pnlOffersList.Controls.Clear();
        var lblWait = new Label
        {
            Text = "🔄 Aras Global ve ShipEntegra sunucularından anlık canlı navlun teklifleri çekiliyor, lütfen bekleyiniz...",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12),
            AutoSize = true
        };
        _pnlOffersList.Controls.Add(lblWait);

        _loadedQuotes.Clear();

        string countryCode = GetSelectedCountryCode();
        double weight = (double)_numWeight.Value;
        double width = (double)_numWidth.Value;
        double length = (double)_numLength.Value;
        double height = (double)_numHeight.Value;

        // 1. Aras Global Sorgusu
        var arasTask = Task.Run(async () =>
        {
            try
            {
                var req = new ArasGlobalQuoteRequest
                {
                    ReceiverCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _arasService.GetQuotesAsync(req);
            }
            catch
            {
                return null;
            }
        });

        // 2. ShipEntegra Sorgusu
        var shipEntegraTask = Task.Run(async () =>
        {
            try
            {
                var req = new ShipEntegraQuoteRequest
                {
                    ReceiverCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _shipEntegraService.GetQuotesAsync(req);
            }
            catch
            {
                return null;
            }
        });

        await Task.WhenAll(arasTask, shipEntegraTask);

        var arasRes = await arasTask;
        var seRes = await shipEntegraTask;

        // Aras Tekliflerini Ekle
        if (arasRes != null && arasRes.Success && arasRes.Offers.Count > 0)
        {
            foreach (var off in arasRes.Offers)
            {
                string sName = $"{off.Cargo} {off.ProviderServiceType}".Trim();
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "Aras Global",
                    ServiceName = sName,
                    SubCarrier = off.Cargo,
                    PriceUsd = off.Price,
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryText = off.DeliveryDaysText,
                    DeliveryDaysMin = off.EstimatedStartDeliveryDate,
                    DeliveryDaysMax = off.EstimatedEndDeliveryDate,
                    Note = off.IsLivePrice ? "Aras Global Canlı Entegrasyon" : "Yedek Tarife",
                    IsLive = off.IsLivePrice
                });
            }
        }

        // ShipEntegra Tekliflerini Ekle
        if (seRes != null && seRes.Success && seRes.Offers.Count > 0)
        {
            foreach (var off in seRes.Offers)
            {
                string sName = !string.IsNullOrWhiteSpace(off.ClearServiceName) ? off.ClearServiceName : off.ServiceName;
                _loadedQuotes.Add(new UnifiedShippingQuote
                {
                    Provider = "ShipEntegra",
                    ServiceName = sName,
                    SubCarrier = ExtractSubCarrierName(off.ServiceName),
                    PriceUsd = off.TotalPrice,
                    PriceTry = Math.Round(off.TotalPrice * UsdTryRate, 2),
                    DeliveryText = ExtractDeliveryTime(off.AdditionalDescription),
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.AdditionalDescription),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.AdditionalDescription),
                    Note = CleanHtml(off.AdditionalDescription),
                    IsLive = off.IsLivePrice
                });
            }
        }

        _btnFetchQuotes.Enabled = true;
        _btnFetchQuotes.Text = "⚡ Teklifleri Getir";

        if (_loadedQuotes.Count == 0)
        {
            _lblStatus.Text = "⚠️ Canlı teklif alınamadı (Token eksik veya yetkisiz).";
            _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
        }
        else
        {
            _lblStatus.Text = $"✅ {_loadedQuotes.Count} adet alternatif kargo teklifi bulundu.";
            _lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
        }

        RenderFilteredOffers();
    }

    private void RenderFilteredOffers()
    {
        _pnlOffersList.Controls.Clear();

        if (_loadedQuotes.Count == 0)
        {
            var pnlEmpty = new Panel { AutoSize = true, Padding = new Padding(16) };
            var lblEmpty = new Label
            {
                Text = "Teklif bulunamadı veya canlı token geçerli değil.\nLütfen Aras Global veya ShipEntegra ekranından '⚡ Otomatik Giriş' yaparak tokeni güncelleyin.",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9.5F),
                AutoSize = true
            };
            pnlEmpty.Controls.Add(lblEmpty);
            _pnlOffersList.Controls.Add(pnlEmpty);
            return;
        }

        // 1. Sağlayıcı Filtresi
        IEnumerable<UnifiedShippingQuote> filtered = _loadedQuotes;
        if (_rbAras.Checked)
        {
            filtered = filtered.Where(q => q.Provider == "Aras Global");
        }
        else if (_rbShipEntegra.Checked)
        {
            filtered = filtered.Where(q => q.Provider == "ShipEntegra");
        }

        // 2. Sıralama
        filtered = _cbSort.SelectedIndex switch
        {
            1 => filtered.OrderByDescending(q => q.PriceUsd), // Pahalıdan ucuza
            2 => filtered.OrderBy(q => q.DeliveryDaysMin).ThenBy(q => q.PriceUsd), // En hızlı
            _ => filtered.OrderBy(q => q.PriceUsd) // En ucuz (varsayılan)
        };

        var list = filtered.ToList();
        if (list.Count == 0)
        {
            var lbl = new Label { Text = "Seçilen filtrede kargo teklifi bulunamadı.", ForeColor = Color.FromArgb(148, 163, 184), AutoSize = true, Padding = new Padding(12) };
            _pnlOffersList.Controls.Add(lbl);
            return;
        }

        decimal minPrice = _loadedQuotes.Min(q => q.PriceUsd);

        foreach (var quote in list)
        {
            bool isCheapest = quote.PriceUsd == minPrice;
            var card = CreateOfferCard(quote, isCheapest);
            _pnlOffersList.Controls.Add(card);
        }
    }

    private Control CreateOfferCard(UnifiedShippingQuote quote, bool isCheapest)
    {
        var card = new Panel
        {
            Width = _pnlOffersList.ClientSize.Width - 28,
            Height = 64,
            BackColor = isCheapest ? Color.FromArgb(19, 42, 41) : Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 8, 12, 8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Sağlayıcı Rozeti
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Servis & Not
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Teslimat Süresi
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Rozet (En Uygun)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Fiyat ($ ve TL)
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Seç Butonu

        // 1. Sağlayıcı Rozeti
        var pnlProvider = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var lblProvider = new Label
        {
            Text = quote.Provider == "Aras Global" ? "🚚 Aras Global" : "📦 ShipEntegra",
            ForeColor = quote.Provider == "Aras Global" ? Color.FromArgb(56, 189, 248) : Color.FromArgb(74, 222, 128),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            AutoSize = true
        };
        var lblCarrier = new Label
        {
            Text = $"Hat: {quote.SubCarrier}",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 7.5F),
            AutoSize = true
        };
        pnlProvider.Controls.Add(lblProvider);
        pnlProvider.Controls.Add(lblCarrier);
        layout.Controls.Add(pnlProvider, 0, 0);

        // 2. Servis & Açıklama
        var pnlService = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var lblService = new Label
        {
            Text = quote.ServiceName,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            AutoSize = true
        };
        var lblNote = new Label
        {
            Text = quote.Note,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F),
            AutoSize = true
        };
        pnlService.Controls.Add(lblService);
        pnlService.Controls.Add(lblNote);
        layout.Controls.Add(pnlService, 1, 0);

        // 3. Teslimat Süresi
        var lblDelivery = new Label
        {
            Text = $"⏱ {quote.DeliveryText}",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.5F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblDelivery, 2, 0);

        // 4. En Uygun Rozeti
        if (isCheapest)
        {
            var badge = new Label
            {
                Text = "🏆 EN UCUZ",
                BackColor = Color.FromArgb(6, 78, 59),
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI Black", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.None,
                Size = new Size(95, 26),
                Margin = new Padding(0, 10, 0, 0)
            };
            layout.Controls.Add(badge, 3, 0);
        }
        else
        {
            layout.Controls.Add(new Label(), 3, 0);
        }

        // 5. Fiyat
        var pnlPrice = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var lblPrice = new Label
        {
            Text = $"${quote.PriceUsd:0.00}",
            ForeColor = isCheapest ? Color.FromArgb(52, 211, 153) : Color.FromArgb(56, 189, 248),
            Font = new Font("Segoe UI Black", 11.5F, FontStyle.Bold),
            AutoSize = true
        };
        var lblPriceTry = new Label
        {
            Text = $"≈ ₺{quote.PriceTry:N2}",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F),
            AutoSize = true
        };
        pnlPrice.Controls.Add(lblPrice);
        pnlPrice.Controls.Add(lblPriceTry);
        layout.Controls.Add(pnlPrice, 4, 0);

        // 6. Seç Butonu
        var btnSelect = new Button
        {
            Text = "✅ Bu Teklifi Kullan",
            Dock = DockStyle.Fill,
            BackColor = isCheapest ? Color.FromArgb(16, 185, 129) : Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Margin = new Padding(4, 8, 4, 8)
        };
        btnSelect.FlatAppearance.BorderSize = 0;
        btnSelect.Click += (_, _) =>
        {
            OnOfferSelected?.Invoke(quote.PriceUsd, $"{quote.Provider} - {quote.ServiceName}");
            Collapse();
        };
        layout.Controls.Add(btnSelect, 5, 0);

        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateFieldWrapper(string labelText, Control inputControl)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        var lbl = new Label
        {
            Text = labelText,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Top,
            Height = 16
        };
        inputControl.Dock = DockStyle.Top;
        box.Controls.Add(inputControl);
        box.Controls.Add(lbl);
        return box;
    }

    private static void ConfigureNumeric(NumericUpDown num, decimal min, decimal max, decimal val, int dec)
    {
        num.Minimum = min;
        num.Maximum = max;
        num.DecimalPlaces = dec;
        num.Value = val;
        num.BackColor = Color.FromArgb(15, 23, 42);
        num.ForeColor = Color.White;
        num.Font = new Font("Segoe UI", 9F);
    }

    private void PopulateCountries()
    {
        _cbCountry.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbCountry.BackColor = Color.FromArgb(15, 23, 42);
        _cbCountry.ForeColor = Color.White;
        _cbCountry.Font = new Font("Segoe UI", 9F);

        _cbCountry.Items.Add(new CountryItem("US", "🇺🇸 Amerika (US)"));
        _cbCountry.Items.Add(new CountryItem("DE", "🇩🇪 Almanya (DE)"));
        _cbCountry.Items.Add(new CountryItem("GB", "🇬🇧 İngiltere (GB)"));
        _cbCountry.Items.Add(new CountryItem("FR", "🇫🇷 Fransa (FR)"));
        _cbCountry.Items.Add(new CountryItem("IT", "🇮🇹 İtalya (IT)"));
        _cbCountry.Items.Add(new CountryItem("CA", "🇨🇦 Kanada (CA)"));
        _cbCountry.Items.Add(new CountryItem("AU", "🇦🇺 Avustralya (AU)"));
        _cbCountry.Items.Add(new CountryItem("NL", "🇳🇱 Hollanda (NL)"));
        _cbCountry.SelectedIndex = 0;
    }

    private string GetSelectedCountryCode()
    {
        if (_cbCountry.SelectedItem is CountryItem ci) return ci.Code;
        return "US";
    }

    private static string ExtractSubCarrierName(string serviceName)
    {
        if (serviceName.Contains("eko", StringComparison.OrdinalIgnoreCase)) return "Eko Plus";
        if (serviceName.Contains("smart", StringComparison.OrdinalIgnoreCase)) return "Smart (FedEx/TNT)";
        if (serviceName.Contains("ups", StringComparison.OrdinalIgnoreCase)) return "UPS";
        if (serviceName.Contains("widect", StringComparison.OrdinalIgnoreCase)) return "Widect";
        if (serviceName.Contains("express", StringComparison.OrdinalIgnoreCase)) return "Express";
        return "ShipEntegra";
    }

    private static string ExtractDeliveryTime(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return "3-6 iş günü";
        int idx = desc.IndexOf("Tahmini Teslimat Süresi", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = desc.IndexOf("Tahmini Teslim Süresi", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            string part = desc[idx..];
            int br = part.IndexOf("<br", StringComparison.OrdinalIgnoreCase);
            if (br > 0) part = part[..br];
            return part.Replace("Tahmini Teslimat Süresi", "", StringComparison.OrdinalIgnoreCase)
                       .Replace("Tahmini Teslim Süresi", "", StringComparison.OrdinalIgnoreCase)
                       .Trim();
        }
        return "3-6 iş günü";
    }

    private static double ExtractDeliveryDaysMin(string desc)
    {
        string text = ExtractDeliveryTime(desc);
        var parts = text.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && double.TryParse(parts[0], out double val)) return val;
        return 5;
    }

    private static double ExtractDeliveryDaysMax(string desc)
    {
        string text = ExtractDeliveryTime(desc);
        var parts = text.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && double.TryParse(parts[1], out double val)) return val;
        return 7;
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return html.Replace("<br>", " • ").Replace("<br/>", " • ").Replace("<br />", " • ").Trim(' ', '•');
    }

    private sealed record CountryItem(string Code, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed class UnifiedShippingQuote
    {
        public string Provider { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public decimal PriceTry { get; set; }
        public string DeliveryText { get; set; } = string.Empty;
        public double DeliveryDaysMin { get; set; }
        public double DeliveryDaysMax { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsLive { get; set; }
    }
}
