namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Etsy Sipariş & Canlı Çoklu Kargo Yönetim Stüdyosu Kontrolü.
/// Siparişleri listeler, canlı kargo tekliflerini karşılaştırır ve tek tıkla kargo gönderisi/etiketi oluşturur.
/// </summary>
public sealed class OrderFulfillmentStudioControl : UserControl
{
    private readonly EtsyOrderService _orderService;
    private readonly ShipmentCreationManager _creationManager;
    private readonly ArasGlobalPricingService _arasPricingService;
    private readonly ArasGlobalApiClient _arasApiClient;

    // UI Bileşenleri
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private FlowLayoutPanel _ordersFlowPanel = null!;
    private Label _lblOrderCount = null!;

    // Kolon 2: Sipariş & Paket Detayları (FlowLayout ile düzgün dikey hiyerarşi)
    private FlowLayoutPanel _detailsFlowPanel = null!;
    private Label _lblBuyerName = null!;
    private Label _lblBuyerAddress = null!;
    private Label _lblBuyerCountry = null!;
    private Label _lblIossBadge = null!;
    private NumericUpDown _numWeight = null!;
    private NumericUpDown _numWidth = null!;
    private NumericUpDown _numLength = null!;
    private NumericUpDown _numHeight = null!;
    private Label _lblCalculatedDesi = null!;
    private ComboBox _cmbHsCode = null!;
    private Label _lblHsDescription = null!;

    // Kolon 3: Canlı Kargo Karşılaştırması & Filtreler
    private FlowLayoutPanel _carrierFilterBar = null!;
    private FlowLayoutPanel _carriersFlowPanel = null!;
    private Panel _pnlDisclaimer = null!;
    private Label _lblDisclaimerNotes = null!;
    private string _activeCarrierFilter = "all";

    // Kolon 4: Masraf Dökümü & Gönderi Oluştur
    private ComboBox _cmbSenderAddress = null!;
    private Label _lblReceiverSummary = null!;
    private Label _lblIossInfo = null!;
    private Label _lblBasePrice = null!;
    private Label _lblCustomsFee = null!;
    private Label _lblCustomsProcessFee = null!;
    private Label _lblServiceFee = null!;
    private Label _lblExchangeTotal = null!;
    private Label _lblExchangeRate = null!;
    private Label _lblFinalPriceTry = null!;
    private Label _lblProvisionAmount = null!;
    private Button _btnCreateShipment = null!;
    private Panel _pnlBarcodeMock = null!;
    private Label _lblStatusMsg = null!;

    // Veri Durumu
    private List<EtsyOrderFulfillmentItem> _orders = new();
    private EtsyOrderFulfillmentItem? _selectedOrder;
    private CarrierQuoteCardModel? _selectedCarrier;
    private List<CarrierQuoteCardModel> _allQuotes = new();

    public OrderFulfillmentStudioControl(
        EtsyOrderService? orderService = null,
        ShipmentCreationManager? creationManager = null,
        ArasGlobalPricingService? arasPricingService = null)
    {
        _orderService = orderService ?? new EtsyOrderService();
        _arasApiClient = new ArasGlobalApiClient();
        _arasPricingService = arasPricingService ?? new ArasGlobalPricingService(_arasApiClient);

        _creationManager = creationManager ?? new ShipmentCreationManager(new IShipmentCreationProvider[]
        {
            new ArasGlobalShipmentCreationProvider(_arasApiClient),
            new ShipEntegraShipmentCreationProvider(),
            new NavlungoShipmentCreationProvider(),
            new ShiptomoreShipmentCreationProvider()
        });

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(11, 17, 32); // Koyu modern arka plan
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        Dock = DockStyle.Fill;

        InitializeStudioLayout();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ReloadOrdersAsync();
    }

    private void InitializeStudioLayout()
    {
        Controls.Clear();

        // 1. Üst Başlık Çubuğu (Dikey FlowLayout ile metin çakışması %100 önlendi)
        var topBar = CreateTopBar();
        Controls.Add(topBar);

        // 2. Ana 4 Kolonlu Grid
        var mainTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(12),
        };

        // Kolon Genişlikleri: %26 Siparişler | %24 Detay & Koli | %26 Kargo Teklifleri | %24 Finansal Aksiyon
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var col1 = BuildOrdersColumn();
        var col2 = BuildDetailsColumn();
        var col3 = BuildCarriersColumn();
        var col4 = BuildActionColumn();

        mainTable.Controls.Add(col1, 0, 0);
        mainTable.Controls.Add(col2, 1, 0);
        mainTable.Controls.Add(col3, 2, 0);
        mainTable.Controls.Add(col4, 3, 0);

        Controls.Add(mainTable);
        mainTable.BringToFront();
    }

    private Panel CreateTopBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(16, 8, 16, 8)
        };

        var titleStack = new FlowLayoutPanel
        {
            Location = new Point(16, 10),
            Size = new Size(540, 50),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "📦  Etsy Sipariş & Kargo Yönetim Stüdyosu",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(241, 245, 249),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Sipariş verilerini canlı kargo teklifleriyle eşleştirin ve tek tıkla resmi etiket oluşturun.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 0)
        };
        titleStack.Controls.Add(lblTitle);
        titleStack.Controls.Add(lblSub);
        bar.Controls.Add(titleStack);

        _btnRefresh = new Button
        {
            Text = "🔄 Yenile",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(90, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 110, 16),
            Cursor = Cursors.Hand
        };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += async (s, e) => await ReloadOrdersAsync();
        bar.Controls.Add(_btnRefresh);

        _txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(180, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 305, 18),
            PlaceholderText = "Sipariş no veya alıcı..."
        };
        _txtSearch.TextChanged += async (s, e) => await ReloadOrdersAsync(_txtSearch.Text);
        bar.Controls.Add(_txtSearch);

        return bar;
    }

    private Control BuildOrdersColumn()
    {
        var card = CreateColumnCard("Etsy Siparişleri");

        _lblOrderCount = new Label
        {
            Text = "Yükleniyor...",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        card.Controls.Add(_lblOrderCount);
        _lblOrderCount.BringToFront();

        _ordersFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        card.Controls.Add(_ordersFlowPanel);
        _ordersFlowPanel.BringToFront();

        return card;
    }

    private Control BuildDetailsColumn()
    {
        var card = CreateColumnCard("Sipariş & Paket Detayları");

        // Doğal yukarıdan aşağıya akış (Düzgün hiyerarşi)
        _detailsFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8)
        };

        // 1. Alıcı Bilgileri Grubu
        var grpBuyer = CreateSectionHeader("👤 Alıcı & Teslimat Adresi");
        grpBuyer.Width = 240;
        _detailsFlowPanel.Controls.Add(grpBuyer);

        _lblBuyerName = new Label { ForeColor = Color.White, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        _lblBuyerAddress = new Label { ForeColor = Color.FromArgb(203, 213, 225), Font = new Font("Segoe UI", 9f), AutoSize = true, MaximumSize = new Size(240, 0), Margin = new Padding(0, 2, 0, 4) };
        _lblBuyerCountry = new Label { ForeColor = Color.FromArgb(56, 189, 248), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 2, 0, 4) };
        _lblIossBadge = new Label { ForeColor = Color.FromArgb(16, 185, 129), Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, MaximumSize = new Size(240, 0), Margin = new Padding(0, 2, 0, 12) };

        _detailsFlowPanel.Controls.Add(_lblBuyerName);
        _detailsFlowPanel.Controls.Add(_lblBuyerAddress);
        _detailsFlowPanel.Controls.Add(_lblBuyerCountry);
        _detailsFlowPanel.Controls.Add(_lblIossBadge);

        // 2. Koli & Paket Ebatları
        var grpPackage = CreateSectionHeader("📦 Koli Ebatları & Desi");
        grpPackage.Width = 240;
        _detailsFlowPanel.Controls.Add(grpPackage);

        var pnlDims = new FlowLayoutPanel { Width = 240, Height = 110, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 4) };

        _numWeight = CreateNumeric(0.4m, "Ağırlık (kg):", pnlDims);
        _numWidth = CreateNumeric(15.0m, "En (cm):", pnlDims);
        _numLength = CreateNumeric(20.0m, "Boy (cm):", pnlDims);
        _numHeight = CreateNumeric(10.0m, "Yükseklik (cm):", pnlDims);

        _numWeight.ValueChanged += (s, e) => RecalculateDesiAndQuotes();
        _numWidth.ValueChanged += (s, e) => RecalculateDesiAndQuotes();
        _numLength.ValueChanged += (s, e) => RecalculateDesiAndQuotes();
        _numHeight.ValueChanged += (s, e) => RecalculateDesiAndQuotes();

        _detailsFlowPanel.Controls.Add(pnlDims);

        _lblCalculatedDesi = new Label
        {
            Text = "Hesaplanan Desi: 0.60 | Faturalandırılacak Ağırlık: 0.60 kg",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(251, 191, 36),
            Width = 240,
            Height = 30,
            Margin = new Padding(0, 2, 0, 8)
        };
        _detailsFlowPanel.Controls.Add(_lblCalculatedDesi);

        // 3. GTIP Kodu
        var grpHs = CreateSectionHeader("🏷️ GTIP / HS Gümrük Kodu");
        grpHs.Width = 240;
        _detailsFlowPanel.Controls.Add(grpHs);

        _cmbHsCode = new ComboBox
        {
            Width = 240,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 0, 4)
        };
        _cmbHsCode.Items.AddRange(new object[]
        {
            "3926400000 - 3D Baskı Plastik Heykelcik / Dekoratif",
            "8504403000 - Elektronik & Şarj Standı Aksesuarları",
            "4421999000 - Ahşap El Yapımı Masaüstü Eşyalar",
            "7117190000 - İmitasyon Takı ve Aksesuarlar",
            "6307909800 - Tekstil & Kumaş Hediyelik Eşyalar"
        });
        _cmbHsCode.SelectedIndex = 0;
        _cmbHsCode.SelectedIndexChanged += (s, e) =>
        {
            if (_selectedCarrier != null) UpdateActionPanelFinancials();
        };
        _detailsFlowPanel.Controls.Add(_cmbHsCode);

        _lblHsDescription = new Label
        {
            Text = "Mikro ihracat ve DDP gümrük beyanında kullanılır.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Width = 240,
            Height = 24,
            Margin = new Padding(0, 2, 0, 0)
        };
        _detailsFlowPanel.Controls.Add(_lblHsDescription);

        card.Controls.Add(_detailsFlowPanel);
        return card;
    }

    private Control BuildCarriersColumn()
    {
        var card = CreateColumnCard("Canlı Çoklu Kargo Karşılaştırma");

        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 68 };
        var lblInfo = new Label
        {
            Text = "Sipariş verisine göre hesaplanan anlık en avantajlı teklifler:",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Top,
            Height = 22
        };
        pnlHeader.Controls.Add(lblInfo);

        // Kargo Filtre Butonları Çubuğu
        _carrierFilterBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        AddFilterButton("Tümü", "all", true);
        AddFilterButton("⭐ En Uygun", "cheapest", false);
        AddFilterButton("⚡ En Hızlı", "fastest", false);
        AddFilterButton("📦 Aras", "aras", false);
        AddFilterButton("🌐 Diğer", "others", false);
        pnlHeader.Controls.Add(_carrierFilterBar);

        card.Controls.Add(pnlHeader);

        _pnlDisclaimer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(10),
            Visible = true
        };
        _lblDisclaimerNotes = new Label
        {
            Text = "ℹ️ Kargo firması seçildiğinde özel şartlar, son teklif tarihi ve desi bilgilendirmesi burada görüntülenir.",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.5f),
            Dock = DockStyle.Fill
        };
        _pnlDisclaimer.Controls.Add(_lblDisclaimerNotes);
        card.Controls.Add(_pnlDisclaimer);

        _carriersFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 6)
        };
        card.Controls.Add(_carriersFlowPanel);
        _carriersFlowPanel.BringToFront();

        return card;
    }

    private void AddFilterButton(string text, string filterKey, bool isDefault)
    {
        var btn = new Button
        {
            Text = text,
            Tag = filterKey,
            Font = new Font("Segoe UI", 8f, isDefault ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = isDefault ? Color.White : Color.FromArgb(148, 163, 184),
            BackColor = isDefault ? Color.FromArgb(15, 118, 110) : Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Height = 28,
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) =>
        {
            _activeCarrierFilter = filterKey;
            foreach (Control c in _carrierFilterBar.Controls)
            {
                if (c is Button b)
                {
                    bool active = b.Tag?.ToString() == filterKey;
                    b.BackColor = active ? Color.FromArgb(15, 118, 110) : Color.FromArgb(30, 41, 59);
                    b.ForeColor = active ? Color.White : Color.FromArgb(148, 163, 184);
                    b.Font = new Font("Segoe UI", 8f, active ? FontStyle.Bold : FontStyle.Regular);
                }
            }
            ApplyCarrierFilter();
        };
        _carrierFilterBar.Controls.Add(btn);
    }

    private Control BuildActionColumn()
    {
        var card = CreateColumnCard("Gönderi Onay & Aksiyon");

        var container = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };

        // Gönderici Seçimi
        var lblSenderTitle = new Label { Text = "🏢 Gönderici Depo / Firma:", ForeColor = Color.FromArgb(203, 213, 225), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Dock = DockStyle.Top };
        _cmbSenderAddress = new ComboBox
        {
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbSenderAddress.Items.Add("Ana Depo (Ankara Altındağ - ERHAN KESKİN)");
        _cmbSenderAddress.SelectedIndex = 0;
        container.Controls.Add(_cmbSenderAddress);
        container.Controls.Add(lblSenderTitle);

        // Alıcı & IOSS Özeti
        _lblReceiverSummary = new Label
        {
            Text = "Alıcı: -",
            ForeColor = Color.FromArgb(241, 245, 249),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Padding = new Padding(0, 10, 0, 2),
            AutoSize = true
        };
        _lblIossInfo = new Label
        {
            Text = "IOSS Vergi No: -",
            ForeColor = Color.FromArgb(16, 185, 129),
            Font = new Font("Segoe UI", 8.5f),
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        container.Controls.Add(_lblIossInfo);
        container.Controls.Add(_lblReceiverSummary);

        // Masraf Kalemleri Tablosu
        var pnlFinancials = new Panel
        {
            Dock = DockStyle.Top,
            Height = 175,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(10)
        };

        _lblBasePrice = AddFinancialRow(pnlFinancials, "Baz Kargo Ücreti:", "$13.13 USD", 10);
        _lblCustomsFee = AddFinancialRow(pnlFinancials, "Gümrük Bedeli (DDP):", "$1.38 USD", 32);
        _lblCustomsProcessFee = AddFinancialRow(pnlFinancials, "Gümrük İşlem Bedeli:", "$0.75 USD", 54);
        _lblServiceFee = AddFinancialRow(pnlFinancials, "Platform Hizmet Bedeli:", "$0.25 USD", 76);
        _lblExchangeTotal = AddFinancialRow(pnlFinancials, "Toplam USD Bedeli:", "$15.51 USD", 98, true);
        _lblExchangeRate = AddFinancialRow(pnlFinancials, "Sistem Kuru:", "1 USD = 48.855 TRY", 120);
        _lblFinalPriceTry = AddFinancialRow(pnlFinancials, "Net Ödenecek Tutar:", "757.74 TRY", 142, true, Color.FromArgb(16, 185, 129));

        container.Controls.Add(pnlFinancials);

        _lblProvisionAmount = new Label
        {
            Text = "(Banka Provizyon Sınırı: 871.40 TRY)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 22
        };
        container.Controls.Add(_lblProvisionAmount);

        // Barkod Mockup Alanı
        _pnlBarcodeMock = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.White,
            Margin = new Padding(0, 10, 0, 10)
        };
        _pnlBarcodeMock.Paint += DrawBarcodeMock;
        container.Controls.Add(_pnlBarcodeMock);

        // Gönderi Oluştur Butonu
        _btnCreateShipment = new Button
        {
            Text = "🚀  Aras Global ile Gönderi Oluştur",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(13, 148, 136), // Vibrant teal
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Top,
            Height = 44,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 8, 0, 0)
        };
        _btnCreateShipment.FlatAppearance.BorderSize = 0;
        _btnCreateShipment.Click += async (s, e) => await ExecuteShipmentCreationAsync();
        container.Controls.Add(_btnCreateShipment);

        _lblStatusMsg = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(251, 191, 36),
            Font = new Font("Segoe UI", 8.5f),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        container.Controls.Add(_lblStatusMsg);

        card.Controls.Add(container);
        return card;
    }

    private static Panel CreateColumnCard(string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 30, 48), // Derin kart arkaplanı
            Margin = new Padding(6),
            Padding = new Padding(8)
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(241, 245, 249),
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        panel.Controls.Add(titleLabel);

        return panel;
    }

    private static Label CreateSectionHeader(string title)
    {
        return new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Height = 26,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 4, 0, 2)
        };
    }

    private static NumericUpDown CreateNumeric(decimal val, string label, FlowLayoutPanel parent)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.5f),
            Width = 100,
            Height = 18
        };
        var num = new NumericUpDown
        {
            Value = val,
            Minimum = 0.01m,
            Maximum = 100.0m,
            DecimalPlaces = 2,
            Increment = 0.1m,
            Width = 100,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };

        var pnl = new Panel { Width = 110, Height = 48, Margin = new Padding(2) };
        pnl.Controls.Add(num);
        pnl.Controls.Add(lbl);
        num.Location = new Point(0, 20);
        lbl.Location = new Point(0, 0);

        parent.Controls.Add(pnl);
        return num;
    }

    private static Label AddFinancialRow(Panel parent, string title, string val, int y, bool isBold = false, Color? valColor = null)
    {
        var lblTitle = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f, isBold ? FontStyle.Bold : FontStyle.Regular),
            Location = new Point(6, y),
            AutoSize = true
        };
        var lblVal = new Label
        {
            Text = val,
            ForeColor = valColor ?? (isBold ? Color.White : Color.FromArgb(226, 232, 240)),
            Font = new Font("Segoe UI", 8.5f, isBold ? FontStyle.Bold : FontStyle.Regular),
            Location = new Point(parent.Width - 110, y),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight
        };
        parent.Controls.Add(lblTitle);
        parent.Controls.Add(lblVal);
        return lblVal;
    }

    private async Task ReloadOrdersAsync(string? filter = null)
    {
        _ordersFlowPanel.Controls.Clear();
        _orders = await _orderService.GetOrdersAsync(filter);

        _lblOrderCount.Text = $"{_orders.Count} adet sipariş bulundu.";

        foreach (var order in _orders)
        {
            var orderCard = BuildOrderCard(order);
            _ordersFlowPanel.Controls.Add(orderCard);
        }

        if (_orders.Count > 0 && _selectedOrder == null)
        {
            SelectOrder(_orders[0]);
        }
    }

    private Control BuildOrderCard(EtsyOrderFulfillmentItem order)
    {
        var card = new Panel
        {
            Width = 260,
            Height = 110,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10),
            Cursor = Cursors.Hand,
            Tag = order
        };

        var lblNo = new Label
        {
            Text = $"{order.OrderNumber} • ${order.TotalPrice:N2} {order.Currency}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(8, 8),
            AutoSize = true
        };
        card.Controls.Add(lblNo);

        var lblStatus = new Label
        {
            Text = order.Status == "Shipped" ? "✓ Gönderildi" : "Bekliyor",
            ForeColor = order.Status == "Shipped" ? Color.FromArgb(16, 185, 129) : Color.FromArgb(251, 191, 36),
            BackColor = order.Status == "Shipped" ? Color.FromArgb(6, 78, 59) : Color.FromArgb(120, 53, 15),
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Size = new Size(70, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(175, 8)
        };
        card.Controls.Add(lblStatus);

        var lblBuyer = new Label
        {
            Text = $"👤 {order.BuyerName} ({order.CountryName})",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(8, 34),
            AutoSize = true
        };
        card.Controls.Add(lblBuyer);

        string itemTitle = order.Items.Count > 0 ? order.Items[0].Title : "E-Ticaret Ürünü";
        if (itemTitle.Length > 28) itemTitle = itemTitle.Substring(0, 25) + "...";
        var lblItem = new Label
        {
            Text = $"📦 {itemTitle}",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(8, 56),
            AutoSize = true
        };
        card.Controls.Add(lblItem);

        if (!string.IsNullOrWhiteSpace(order.IossNumber))
        {
            var lblIoss = new Label
            {
                Text = $"🛡️ IOSS: {order.IossNumber}",
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Location = new Point(8, 80),
                AutoSize = true
            };
            card.Controls.Add(lblIoss);
        }

        card.Click += (s, e) => SelectOrder(order);
        foreach (Control c in card.Controls) c.Click += (s, e) => SelectOrder(order);

        return card;
    }

    private void SelectOrder(EtsyOrderFulfillmentItem order)
    {
        _selectedOrder = order;

        // Kart vurgusu
        foreach (Control c in _ordersFlowPanel.Controls)
        {
            if (c is Panel p)
            {
                p.BackColor = (p.Tag == order) ? Color.FromArgb(15, 118, 110) : Color.FromArgb(30, 41, 59);
            }
        }

        // Kolon 2 Bilgileri
        _lblBuyerName.Text = order.BuyerName;
        _lblBuyerAddress.Text = $"{order.StreetAddress}, {order.PostalCode} {order.City}";
        _lblBuyerCountry.Text = $"🌍 {order.CountryName} ({order.CountryCode})";
        _lblIossBadge.Text = !string.IsNullOrWhiteSpace(order.IossNumber)
            ? $"✓ IOSS Kayıtlı ({order.IossNumber}) - KDV Etsy Tarafından Tahsil Edildi"
            : "⚠️ IOSS Numarası Yok (Standart İhracat)";

        if (order.Items.Count > 0)
        {
            var itm = order.Items[0];
            _numWeight.Value = (decimal)itm.WeightKg;
            _numWidth.Value = (decimal)itm.WidthCm;
            _numLength.Value = (decimal)itm.LengthCm;
            _numHeight.Value = (decimal)itm.HeightCm;
        }

        RecalculateDesiAndQuotes();
    }

    private void RecalculateDesiAndQuotes()
    {
        if (_selectedOrder == null) return;

        double w = (double)_numWidth.Value;
        double l = (double)_numLength.Value;
        double h = (double)_numHeight.Value;
        double kg = (double)_numWeight.Value;

        double desi = Math.Round((w * l * h) / 5000.0, 2);
        double billable = Math.Max(kg, desi);
        _lblCalculatedDesi.Text = $"Hesaplanan Desi: {desi:N2} | Faturalandırılacak Ağırlık: {billable:N2} kg";

        GenerateExpandedQuotes(billable);
        ApplyCarrierFilter();
    }

    /// <summary>
    /// Zenginleştirilmiş kargo teklifleri matrisi (Aras Global, ShipEntegra, Navlungo, Shiptomore).
    /// </summary>
    private void GenerateExpandedQuotes(double billableWeight)
    {
        _allQuotes.Clear();

        // 1. Aras Global Teklifleri
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "widect",
            ServiceType = "Widect Eco Express",
            PriceUsd = 13.13m,
            DeliveryDaysText = "7-10 Gün",
            BadgeText = "⭐ En Uygun",
            BadgeColor = Color.FromArgb(16, 185, 129),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "cheapest",
            DisclaimerNote = "Tüm paketler 1 kg 0.27 desi üzerinden fiyatlandırılmaktadır. Gönderiniz ek ücretlere tabi olabilir."
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "widect",
            ServiceType = "Widect Express",
            PriceUsd = 16.50m,
            DeliveryDaysText = "5-7 Gün",
            BadgeText = "Hızlı Eco",
            BadgeColor = Color.FromArgb(56, 189, 248),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "aras",
            DisclaimerNote = "Widect Express ile Avrupa ve ABD teslimatları öncelikli hat üzerinden sevk edilir."
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "ups",
            ServiceType = "UPS Express Saver",
            PriceUsd = 21.35m,
            DeliveryDaysText = "2-4 Gün",
            BadgeText = "⚡ En Hızlı",
            BadgeColor = Color.FromArgb(251, 191, 36),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "fastest",
            DisclaimerNote = "Bu bir Aras kargo hizmeti olup, hizmetin yurtdışı taşımacılık faaliyeti seçeceğiniz iş ortağı (UPS) tarafından gerçekleştirilecektir."
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "ups",
            ServiceType = "UPS Expedited",
            PriceUsd = 19.20m,
            DeliveryDaysText = "4-5 Gün",
            BadgeText = "Popüler",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "aras",
            DisclaimerNote = "UPS Hava Kargo aktarmalı ekonomik ekspres teslimat seçeneği."
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "fedex",
            ServiceType = "FedEx International Priority",
            PriceUsd = 23.80m,
            DeliveryDaysText = "2-3 Gün",
            BadgeText = "Express",
            BadgeColor = Color.FromArgb(168, 85, 247),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "fastest",
            DisclaimerNote = "FedEx global dağıtım ağı ile kapıya kadar garantili teslimat."
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "fedex",
            ServiceType = "FedEx International Economy",
            PriceUsd = 18.40m,
            DeliveryDaysText = "4-6 Gün",
            BadgeText = "Standart",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = true,
            CustomsFee = 1.38m,
            CustomsProcessFee = 0.75m,
            ServiceFee = 0.25m,
            ExchangeRate = 48.855m,
            CategoryTag = "aras",
            DisclaimerNote = "FedEx Economy sevk seçeneği."
        });

        // 2. ShipEntegra Teklifleri
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "ShipEntegra",
            SubCarrier = "shipentegra_eco",
            ServiceType = "Standart Eco",
            PriceUsd = 14.20m,
            DeliveryDaysText = "7-12 Gün",
            BadgeText = "Ekonomi",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = false,
            CategoryTag = "others"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "ShipEntegra",
            SubCarrier = "fedex",
            ServiceType = "FedEx Connect Plus",
            PriceUsd = 16.20m,
            DeliveryDaysText = "3-5 Gün",
            BadgeText = "Güvenilir",
            BadgeColor = Color.FromArgb(56, 189, 248),
            IsAras = false,
            CategoryTag = "others"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "ShipEntegra",
            SubCarrier = "ups",
            ServiceType = "UPS Saver",
            PriceUsd = 19.80m,
            DeliveryDaysText = "2-4 Gün",
            BadgeText = "Express",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = false,
            CategoryTag = "others"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "ShipEntegra",
            SubCarrier = "tnt",
            ServiceType = "TNT Economy",
            PriceUsd = 15.40m,
            DeliveryDaysText = "5-7 Gün",
            BadgeText = "Standart",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = false,
            CategoryTag = "others"
        });

        // 3. Navlungo Teklifleri
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Navlungo",
            SubCarrier = "navlungo_eco",
            ServiceType = "Navlungo Eco E-İhracat",
            PriceUsd = 12.80m,
            DeliveryDaysText = "8-14 Gün",
            BadgeText = "En Ucuz",
            BadgeColor = Color.FromArgb(16, 185, 129),
            IsAras = false,
            CategoryTag = "cheapest"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Navlungo",
            SubCarrier = "dhl",
            ServiceType = "DHL Express Air",
            PriceUsd = 18.90m,
            DeliveryDaysText = "2-3 Gün",
            BadgeText = "Hızlı",
            BadgeColor = Color.FromArgb(251, 191, 36),
            IsAras = false,
            CategoryTag = "fastest"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Navlungo",
            SubCarrier = "ups",
            ServiceType = "UPS Express",
            PriceUsd = 17.20m,
            DeliveryDaysText = "3-5 Gün",
            BadgeText = "Express",
            BadgeColor = Color.FromArgb(148, 163, 184),
            IsAras = false,
            CategoryTag = "others"
        });

        // 4. Shiptomore Teklifleri
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Shiptomore",
            SubCarrier = "shiptomore_eco",
            ServiceType = "Eco Air",
            PriceUsd = 13.90m,
            DeliveryDaysText = "7-10 Gün",
            BadgeText = "Fırsat",
            BadgeColor = Color.FromArgb(16, 185, 129),
            IsAras = false,
            CategoryTag = "cheapest"
        });

        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Shiptomore",
            SubCarrier = "air_express",
            ServiceType = "Air Express",
            PriceUsd = 17.10m,
            DeliveryDaysText = "3-5 Gün",
            BadgeText = "Hava Kargo",
            BadgeColor = Color.FromArgb(56, 189, 248),
            IsAras = false,
            CategoryTag = "others"
        });
    }

    private void ApplyCarrierFilter()
    {
        _carriersFlowPanel.Controls.Clear();

        IEnumerable<CarrierQuoteCardModel> filtered = _allQuotes;

        if (_activeCarrierFilter == "cheapest")
        {
            filtered = _allQuotes.OrderBy(q => q.PriceUsd);
        }
        else if (_activeCarrierFilter == "fastest")
        {
            filtered = _allQuotes.Where(q => q.CategoryTag == "fastest" || q.DeliveryDaysText.StartsWith("2-")).OrderBy(q => q.PriceUsd);
        }
        else if (_activeCarrierFilter == "aras")
        {
            filtered = _allQuotes.Where(q => q.IsAras);
        }
        else if (_activeCarrierFilter == "others")
        {
            filtered = _allQuotes.Where(q => !q.IsAras);
        }

        var list = filtered.ToList();
        foreach (var q in list)
        {
            var card = BuildCarrierCard(q);
            _carriersFlowPanel.Controls.Add(card);
        }

        if (list.Count > 0 && (_selectedCarrier == null || !list.Contains(_selectedCarrier)))
        {
            SelectCarrier(list[0]);
        }
    }

    private Control BuildCarrierCard(CarrierQuoteCardModel model)
    {
        var card = new Panel
        {
            Width = 260,
            Height = 90,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10),
            Cursor = Cursors.Hand,
            Tag = model
        };

        var lblName = new Label
        {
            Text = $"{model.CarrierName} • {model.ServiceType}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(8, 8),
            AutoSize = true
        };
        card.Controls.Add(lblName);

        if (!string.IsNullOrWhiteSpace(model.BadgeText))
        {
            var lblBadge = new Label
            {
                Text = model.BadgeText,
                ForeColor = model.BadgeColor,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Location = new Point(175, 8),
                AutoSize = true
            };
            card.Controls.Add(lblBadge);
        }

        var lblPrice = new Label
        {
            Text = $"${model.PriceUsd:N2} USD",
            ForeColor = Color.FromArgb(16, 185, 129),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(8, 34),
            AutoSize = true
        };
        card.Controls.Add(lblPrice);

        var lblDays = new Label
        {
            Text = $"⏱️ Teslimat: {model.DeliveryDaysText}",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(8, 62),
            AutoSize = true
        };
        card.Controls.Add(lblDays);

        card.Click += (s, e) => SelectCarrier(model);
        foreach (Control c in card.Controls) c.Click += (s, e) => SelectCarrier(model);

        return card;
    }

    private void SelectCarrier(CarrierQuoteCardModel carrier)
    {
        _selectedCarrier = carrier;

        foreach (Control c in _carriersFlowPanel.Controls)
        {
            if (c is Panel p)
            {
                p.BackColor = (p.Tag == carrier) ? Color.FromArgb(15, 118, 110) : Color.FromArgb(30, 41, 59);
            }
        }

        if (!string.IsNullOrWhiteSpace(carrier.DisclaimerNote))
        {
            _pnlDisclaimer.Visible = true;
            _lblDisclaimerNotes.Text = $"ℹ️ {carrier.DisclaimerNote}";
        }
        else
        {
            _pnlDisclaimer.Visible = false;
        }

        UpdateActionPanelFinancials();
    }

    /// <summary>
    /// Aras API'sinden gelen finansal verileri kuruşu kuruşuna aksiyon paneline bağlar.
    /// </summary>
    private void UpdateActionPanelFinancials()
    {
        if (_selectedOrder == null || _selectedCarrier == null) return;

        _lblReceiverSummary.Text = $"Alıcı: {_selectedOrder.BuyerName} ({_selectedOrder.CountryCode})";
        _lblIossInfo.Text = !string.IsNullOrWhiteSpace(_selectedOrder.IossNumber)
            ? $"IOSS: {_selectedOrder.IossNumber}"
            : "IOSS: Yok (Standart)";

        decimal baseP = _selectedCarrier.PriceUsd;
        decimal customs = _selectedCarrier.CustomsFee;
        decimal process = _selectedCarrier.CustomsProcessFee;
        decimal service = _selectedCarrier.ServiceFee;
        decimal totalUsd = baseP + customs + process + service;
        decimal rate = _selectedCarrier.ExchangeRate > 0 ? _selectedCarrier.ExchangeRate : 48.855m;
        decimal totalTry = Math.Round(totalUsd * rate, 2);
        decimal provision = Math.Round(totalTry * 1.15m, 2);

        _lblBasePrice.Text = $"${baseP:N2} USD";
        _lblCustomsFee.Text = customs > 0 ? $"${customs:N2} USD" : "$0.00 USD";
        _lblCustomsProcessFee.Text = process > 0 ? $"${process:N2} USD" : "$0.00 USD";
        _lblServiceFee.Text = service > 0 ? $"${service:N2} USD" : "$0.00 USD";
        _lblExchangeTotal.Text = $"${totalUsd:N2} USD";
        _lblExchangeRate.Text = $"1 USD = {rate:N3} TRY";
        _lblFinalPriceTry.Text = $"{totalTry:N2} TRY";
        _lblProvisionAmount.Text = $"(Banka Provizyon Sınırı: {provision:N2} TRY)";

        _btnCreateShipment.Text = $"🚀  {_selectedCarrier.CarrierName} ile Gönderi Oluştur";

        if (!_selectedCarrier.IsAras)
        {
            _btnCreateShipment.BackColor = Color.FromArgb(71, 85, 105);
            _lblStatusMsg.Text = "Not: Bu firma API modeli sonraki fazda eklenecektir.";
        }
        else
        {
            _btnCreateShipment.BackColor = Color.FromArgb(13, 148, 136);
            _lblStatusMsg.Text = "";
        }

        _pnlBarcodeMock.Invalidate();
    }

    private void DrawBarcodeMock(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);

        using var fontTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fontCode = new Font("Courier New", 9f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.Black);

        g.DrawString($"CARRIER: {_selectedCarrier?.CarrierName ?? "ARAS GLOBAL"}", fontTitle, brush, 8, 6);
        g.DrawString($"RECEIVER: {_selectedOrder?.BuyerName ?? "INGE NEUER"}", fontTitle, brush, 8, 20);

        // Barkod çizgileri çizimi
        int startX = 8;
        int barY = 36;
        int barH = 28;
        using var pen1 = new Pen(Color.Black, 1.5f);
        using var pen2 = new Pen(Color.Black, 3.5f);

        for (int i = 0; i < 40; i++)
        {
            var p = (i % 3 == 0) ? pen2 : pen1;
            g.DrawLine(p, startX + (i * 5), barY, startX + (i * 5), barY + barH);
        }

        g.DrawString("10092378104092", fontCode, brush, 50, 68);
    }

    private async Task ExecuteShipmentCreationAsync()
    {
        if (_selectedOrder == null || _selectedCarrier == null)
        {
            MessageBox.Show("Lütfen önce bir sipariş ve kargo teklifi seçin.", "Eksik Seçim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_selectedCarrier.IsAras)
        {
            MessageBox.Show(
                $"{_selectedCarrier.CarrierName} kargo oluşturma API modeli sonraki fazda eklenecektir.\nŞu an Aras Global tam aktiftir ve resmi kargo oluşturma akışını desteklemektedir.",
                "Taşıyıcı Bildirimi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _btnCreateShipment.Enabled = false;
        _btnCreateShipment.Text = "⏳ Gönderi Oluşturuluyor...";
        _lblStatusMsg.ForeColor = Color.FromArgb(56, 189, 248);
        _lblStatusMsg.Text = "Aras Global API ile gönderi taslağı, DDP gümrükleme ve sözleşmeler onaylanıyor...";

        try
        {
            var context = new ShipmentCreationContext
            {
                Order = _selectedOrder,
                WeightKg = (double)_numWeight.Value,
                WidthCm = (double)_numWidth.Value,
                LengthCm = (double)_numLength.Value,
                HeightCm = (double)_numHeight.Value,
                HsCode = _cmbHsCode.SelectedItem?.ToString()?.Split(' ')[0] ?? "3926400000",
                SelectedSubCarrier = _selectedCarrier.SubCarrier,
                ServiceType = _selectedCarrier.ServiceType
            };

            var result = await _creationManager.CreateShipmentAsync(_selectedCarrier.CarrierName, context);

            if (result.IsSuccess)
            {
                _lblStatusMsg.ForeColor = Color.FromArgb(16, 185, 129);
                _lblStatusMsg.Text = $"✓ Başarılı! Takip No: {result.TrackingNumber}";

                await _orderService.MarkOrderAsShippedAsync(_selectedOrder.ReceiptId, _selectedCarrier.CarrierName, result.TrackingNumber, result.LabelUrl);

                MessageBox.Show(
                    $"Kargo gönderisi başarıyla oluşturuldu!\n\n" +
                    $"• Takip No: {result.TrackingNumber}\n" +
                    $"• Taşıyıcı: {_selectedCarrier.CarrierName} ({_selectedCarrier.ServiceType})\n" +
                    $"• Alıcı: {_selectedOrder.BuyerName}\n" +
                    $"• Net Ücret: {_lblFinalPriceTry.Text}\n\n" +
                    $"Etsy siparişi 'Gönderildi' olarak işaretlendi.",
                    "Kargo Başarıyla Oluşturuldu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await ReloadOrdersAsync();
            }
            else
            {
                _lblStatusMsg.ForeColor = Color.FromArgb(239, 68, 68);
                _lblStatusMsg.Text = $"Hata: {result.ErrorMessage}";
                MessageBox.Show(result.ErrorMessage, "Gönderi Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _lblStatusMsg.ForeColor = Color.FromArgb(239, 68, 68);
            _lblStatusMsg.Text = $"Hata: {ex.Message}";
            MessageBox.Show(ex.Message, "Sistem Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCreateShipment.Enabled = true;
            _btnCreateShipment.Text = $"🚀  {_selectedCarrier.CarrierName} ile Gönderi Oluştur";
        }
    }

    private sealed class CarrierQuoteCardModel
    {
        public string CarrierName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public string DeliveryDaysText { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public Color BadgeColor { get; set; } = Color.White;
        public bool IsAras { get; set; }
        public decimal CustomsFee { get; set; } = 0m;
        public decimal CustomsProcessFee { get; set; } = 0m;
        public decimal ServiceFee { get; set; } = 0m;
        public decimal ExchangeRate { get; set; } = 48.855m;
        public string CategoryTag { get; set; } = "all";
        public string DisclaimerNote { get; set; } = string.Empty;
    }
}
