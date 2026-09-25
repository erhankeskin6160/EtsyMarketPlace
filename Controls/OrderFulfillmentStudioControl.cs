namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Etsy Sipariş & Canlı Çoklu Kargo Yönetim Stüdyosu Kontrolü.
/// Modern SaaS Dark Mode Figma tasarımıyla pixel-perfect birebir uyumlu.
/// Sipariş kartları (thumbnail & IOSS rozetleri), taşıyıcı canlı teklif kartları,
/// gerçekçi barkod etiketi ve tek tıkla Aras Global kargo oluşturma.
/// </summary>
public sealed class OrderFulfillmentStudioControl : UserControl
{
    private readonly EtsyOrderService _orderService;
    private readonly ShipmentCreationManager _creationManager;
    private readonly ArasGlobalPricingService _arasPricingService;
    private readonly ArasGlobalApiClient _arasApiClient;
    private readonly IShippingSessionManager _sessionManager;
    private readonly ShipEntegraPricingService _shipEntegraPricingService;
    private readonly INavlungoApiClient _navlungoApiClient;
    private readonly IShiptomoreApiClient _shiptomoreApiClient;

    // Üst Bar Bileşenleri
    private Button _tabOrders = null!;
    private Button _tabSettings = null!;
    private Button _tabInventory = null!;
    private Button _btnRefresh = null!;
    private Button _btnArasSession = null!;
    private TextBox _txtSearch = null!;

    // Kolon 1: Etsy Orders
    private FlowLayoutPanel _ordersFlowPanel = null!;
    private Label _lblOrderCount = null!;

    // Kolon 2: Order Details & Package
    private Label _lblBuyerName = null!;
    private Label _lblBuyerAddressLine1 = null!;
    private Label _lblBuyerAddressLine2 = null!;
    private Label _lblBuyerCountry = null!;
    private SaasUnitInputBox _inputWeight = null!;
    private SaasUnitInputBox _inputLength = null!;
    private SaasUnitInputBox _inputWidth = null!;
    private SaasUnitInputBox _inputHeight = null!;
    private ComboBox _cmbHsCode = null!;
    private Label _lblCalculatedDesi = null!;

    // Kolon 3: Package Customization / Live Multi-Carrier Shipping Comparison
    private FlowLayoutPanel _carriersFlowPanel = null!;
    private FlowLayoutPanel _pnlFilterPills = null!;
    private Panel _pnlDisclaimer = null!;
    private Label _lblDisclaimerNotes = null!;
    private string _selectedCarrierFilter = "Tümü";

    // Kolon 4: Action Panel (Shipment Actions)
    private ComboBox _cmbSenderAddress = null!;
    private Label _lblReceiverSummaryName = null!;
    private Label _lblReceiverSummaryStreet = null!;
    private Label _lblReceiverSummaryCity = null!;
    private Label _lblReceiverSummaryCountry = null!;
    private Label _lblIossAppliedRate = null!;
    private Label _lblIossId = null!;
    private SaasBarcodeLabelControl _barcodeControl = null!;
    private Label _lblBasePrice = null!;
    private Label _lblFinalPriceTry = null!;
    private Button _btnCreateShipment = null!;
    private Label _lblStatusMsg = null!;

    // Veri Durumu
    private List<EtsyOrderFulfillmentItem> _orders = new();
    private EtsyOrderFulfillmentItem? _selectedOrder;
    private CarrierQuoteCardModel? _selectedCarrier;
    private List<CarrierQuoteCardModel> _allQuotes = new();
    public decimal UsdTryRate { get; set; } = 48.855m;

    public OrderFulfillmentStudioControl(
        EtsyOrderService? orderService = null,
        ShipmentCreationManager? creationManager = null,
        ArasGlobalPricingService? arasPricingService = null,
        IShippingSessionManager? sessionManager = null)
    {
        _orderService = orderService ?? new EtsyOrderService();
        _arasApiClient = new ArasGlobalApiClient();
        _arasPricingService = arasPricingService ?? new ArasGlobalPricingService(_arasApiClient);
        _sessionManager = sessionManager ?? new PuppeteerShippingSessionManager();
        _shipEntegraPricingService = new ShipEntegraPricingService();
        _navlungoApiClient = new NavlungoApiClient();
        _shiptomoreApiClient = new ShiptomoreApiClient();

        _creationManager = creationManager ?? new ShipmentCreationManager(new IShipmentCreationProvider[]
        {
            new ArasGlobalShipmentCreationProvider(
                _arasApiClient,
                _sessionManager,
                pass => ShippingCredentialEncryptor.Decrypt(pass)),
            new ShipEntegraShipmentCreationProvider(),
            new NavlungoShipmentCreationProvider(),
            new ShiptomoreShipmentCreationProvider()
        });

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(11, 19, 35); // Derin SaaS Slate Navy #0B1323
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        Dock = DockStyle.Fill;

        InitializeSaaSLegacyLayout();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateSessionButtonState();
        await ReloadOrdersAsync();
    }

    private void InitializeSaaSLegacyLayout()
    {
        Controls.Clear();

        // 1. Üst SaaS Bar (macOS noktaları, Sekmeler, Oturum & Yenile)
        var topBar = CreateSaaSTopBar();
        Controls.Add(topBar);

        // 2. Ana 4 Kolonlu Grid Tablosu
        var mainTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(11, 19, 35),
            Padding = new Padding(12, 6, 12, 12),
        };

        // Genişlik dağılımı: %27, %23, %27, %23 (Görseldeki oranlar)
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23f));
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var col1 = BuildOrdersColumn();
        var col2 = BuildDetailsAndPackageColumn();
        var col3 = BuildCarriersColumn();
        var col4 = BuildActionPanelColumn();

        mainTable.Controls.Add(col1, 0, 0);
        mainTable.Controls.Add(col2, 1, 0);
        mainTable.Controls.Add(col3, 2, 0);
        mainTable.Controls.Add(col4, 3, 0);

        Controls.Add(mainTable);
        mainTable.BringToFront();
    }

    #region 1. Top SaaS Bar
    private Panel CreateSaaSTopBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(13, 21, 37), // #0D1525
            Padding = new Padding(16, 0, 16, 0)
        };

        // macOS tarzı 3 nokta (Kırmızı, Sarı, Yeşil)
        var pnlDots = new Panel
        {
            Size = new Size(60, 20),
            Location = new Point(16, 17),
            BackColor = Color.Transparent
        };
        pnlDots.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var r = new SolidBrush(Color.FromArgb(239, 68, 68));   // Red
            using var y = new SolidBrush(Color.FromArgb(245, 158, 11));  // Yellow
            using var g = new SolidBrush(Color.FromArgb(16, 185, 129));  // Green
            e.Graphics.FillEllipse(r, 0, 4, 11, 11);
            e.Graphics.FillEllipse(y, 16, 4, 11, 11);
            e.Graphics.FillEllipse(g, 32, 4, 11, 11);
        };
        bar.Controls.Add(pnlDots);

        // Orta Sekmeler: [📑 Orders] [⚙️ Settings] [📦 Inventory]
        var pnlTabs = new FlowLayoutPanel
        {
            Location = new Point(100, 10),
            Size = new Size(380, 36),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent
        };

        _tabOrders = CreateNavTab("📑  Orders", true);
        _tabSettings = CreateNavTab("⚙️  Settings", false);
        _tabSettings.Click += async (s, e) => await PromptOrRefreshArasSessionAsync();

        _tabInventory = CreateNavTab("📦  Inventory", false);
        _tabInventory.Click += (s, e) => MessageBox.Show("Envanter & 3D Model Kataloğu hazır.", "Envanter", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlTabs.Controls.Add(_tabOrders);
        pnlTabs.Controls.Add(_tabSettings);
        pnlTabs.Controls.Add(_tabInventory);
        bar.Controls.Add(pnlTabs);

        // Sağ Taraf Aksiyonları: Aras Oturumu Butonu & Yenile
        _btnRefresh = new Button
        {
            Text = "🔄 Yenile",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 96, 11),
            Cursor = Cursors.Hand
        };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += async (s, e) => await ReloadOrdersAsync();
        bar.Controls.Add(_btnRefresh);

        _txtSearch = new TextBox
        {
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(130, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 236, 14),
            PlaceholderText = "Sipariş Ara..."
        };
        _txtSearch.TextChanged += async (s, e) => await ReloadOrdersAsync(_txtSearch.Text);
        bar.Controls.Add(_txtSearch);

        _btnArasSession = new Button
        {
            Text = "🔑 Aras Oturumu",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Size = new Size(155, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 400, 11),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnArasSession.FlatAppearance.BorderSize = 0;
        _btnArasSession.Click += async (s, e) => await PromptOrRefreshArasSessionAsync();
        bar.Controls.Add(_btnArasSession);

        UpdateSessionButtonState();
        return bar;
    }

    private Button CreateNavTab(string text, bool isActive)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, isActive ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = isActive ? Color.FromArgb(56, 189, 248) : Color.FromArgb(148, 163, 184),
            BackColor = isActive ? Color.FromArgb(24, 38, 61) : Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void UpdateSessionButtonState()
    {
        if (_btnArasSession == null || _btnArasSession.IsDisposed) return;
        var settings = ArasGlobalSettingsStore.Load();
        bool hasValid = settings.HasValidTokenFormat;

        _btnArasSession.Text = hasValid ? "🟢 Aras Oturumu: Açık" : "⚡ Aras Oturumu Yenile";
        _btnArasSession.BackColor = hasValid ? Color.FromArgb(6, 78, 59) : Color.FromArgb(120, 53, 15);
        _btnArasSession.ForeColor = hasValid ? Color.FromArgb(167, 243, 208) : Color.FromArgb(254, 215, 170);
    }
    #endregion

    #region 2. Kolon 1: Etsy Orders
    private Control BuildOrdersColumn()
    {
        var colPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };

        // Üst Başlık Satırı: Etsy Orders + Arama/Filtre ikonları
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
        var lblTitle = new Label
        {
            Text = "Etsy Orders",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(4, 8),
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblTitle);

        var btnAddIcon = new Button
        {
            Text = "🔄",
            Size = new Size(28, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlHeader.Width - 32, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand
        };
        btnAddIcon.FlatAppearance.BorderSize = 0;
        btnAddIcon.Click += async (s, e) => await ReloadOrdersAsync();

        var btnSearchIcon = new Button
        {
            Text = "🔍",
            Size = new Size(28, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlHeader.Width - 64, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand
        };
        btnSearchIcon.FlatAppearance.BorderSize = 0;
        btnSearchIcon.Click += (s, e) => { _txtSearch.Focus(); };

        pnlHeader.Controls.Add(btnSearchIcon);
        pnlHeader.Controls.Add(btnAddIcon);
        pnlHeader.SizeChanged += (s, e) =>
        {
            btnAddIcon.Location = new Point(pnlHeader.Width - 32, 6);
            btnSearchIcon.Location = new Point(pnlHeader.Width - 64, 6);
        };
        colPanel.Controls.Add(pnlHeader);

        _lblOrderCount = new Label
        {
            Text = "3 adet sipariş yüklendi",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Top,
            Height = 20,
            Padding = new Padding(6, 0, 0, 0)
        };
        colPanel.Controls.Add(_lblOrderCount);

        _ordersFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 6, 4, 0)
        };
        colPanel.Controls.Add(_ordersFlowPanel);
        _ordersFlowPanel.BringToFront();

        return colPanel;
    }
    #endregion

    #region 3. Kolon 2: Order Details & Package
    private Control BuildDetailsAndPackageColumn()
    {
        var colPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };

        // 1. Order Details Başlığı
        var lblHeader = new Label
        {
            Text = "Order Details",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        colPanel.Controls.Add(lblHeader);

        var contentFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 4, 0)
        };

        // 2. Order Details Kartı (Müşteri & Açık Adres)
        var detailsCard = new SaasCardPanel
        {
            Width = 230,
            Height = 115,
            CardBackground = Color.FromArgb(21, 30, 48),
            BorderColor = Color.FromArgb(37, 51, 71),
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(12)
        };

        _lblBuyerName = new Label
        {
            Text = "Inge Neuer",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(12, 10),
            AutoSize = true
        };
        _lblBuyerAddressLine1 = new Label
        {
            Text = "Am Rheinufer 12",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(12, 34),
            AutoSize = true
        };
        _lblBuyerAddressLine2 = new Label
        {
            Text = "56068 Koblenz",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(12, 54),
            AutoSize = true
        };
        _lblBuyerCountry = new Label
        {
            Text = "Germany (DE)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(12, 76),
            AutoSize = true
        };

        detailsCard.Controls.Add(_lblBuyerName);
        detailsCard.Controls.Add(_lblBuyerAddressLine1);
        detailsCard.Controls.Add(_lblBuyerAddressLine2);
        detailsCard.Controls.Add(_lblBuyerCountry);
        contentFlow.Controls.Add(detailsCard);

        // 3. Package Başlığı
        var lblPackageHeader = new Label
        {
            Text = "Package",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Height = 26,
            Width = 250,
            Margin = new Padding(0, 2, 0, 6)
        };
        contentFlow.Controls.Add(lblPackageHeader);

        // 4. Ağırlık, Boy, En, Yükseklik 2x2 Grid (Kırpılma / Taşma Olmayan SaaS Yerleşimi)
        var pnlPackageGrid = new TableLayoutPanel
        {
            Width = 250,
            Height = 120,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        pnlPackageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pnlPackageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pnlPackageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        pnlPackageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));

        _inputWeight = new SaasUnitInputBox("Ağırlık (Weight)", "0.40", "kg") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 4, 4) };
        _inputLength = new SaasUnitInputBox("Boy (Length)", "20", "cm") { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 4) };
        _inputWidth = new SaasUnitInputBox("En (Width)", "15", "cm") { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 4, 0) };
        _inputHeight = new SaasUnitInputBox("Yükseklik (Height)", "10", "cm") { Dock = DockStyle.Fill, Margin = new Padding(4, 4, 0, 0) };

        _inputWeight.ValueChanged += (s, e) => _ = RecalculateDesiAndQuotesAsync();
        _inputLength.ValueChanged += (s, e) => _ = RecalculateDesiAndQuotesAsync();
        _inputWidth.ValueChanged += (s, e) => _ = RecalculateDesiAndQuotesAsync();
        _inputHeight.ValueChanged += (s, e) => _ = RecalculateDesiAndQuotesAsync();

        pnlPackageGrid.Controls.Add(_inputWeight, 0, 0);
        pnlPackageGrid.Controls.Add(_inputLength, 1, 0);
        pnlPackageGrid.Controls.Add(_inputWidth, 0, 1);
        pnlPackageGrid.Controls.Add(_inputHeight, 1, 1);
        contentFlow.Controls.Add(pnlPackageGrid);

        // 5. GTIP HS Code Alanı
        var lblGtipTitle = new Label
        {
            Text = "GTIP HS Code",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Height = 20,
            Width = 250,
            Margin = new Padding(0, 4, 0, 2)
        };
        contentFlow.Controls.Add(lblGtipTitle);

        _cmbHsCode = new ComboBox
        {
            Width = 250,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 0, 6)
        };
        _cmbHsCode.Items.AddRange(new object[]
        {
            "3926.40.00 - 3D Baskı Plastik Heykelcik",
            "8504.40.30 - Elektronik Aksesuar Standı",
            "4421.99.90 - Ahşap El Yapımı Dekorasyon",
            "7117.19.00 - İmitasyon Takı Aksesuarı",
            "6307.90.98 - Tekstil Hediyelik Eşya"
        });
        _cmbHsCode.SelectedIndex = 0;
        _cmbHsCode.SelectedIndexChanged += (s, e) => UpdateActionPanelFinancials();
        contentFlow.Controls.Add(_cmbHsCode);

        // 6. Hesaplanan Desi & Bilgilendirme
        _lblCalculatedDesi = new Label
        {
            Text = "Desi: 0.60 | Faturalandırılacak: 0.60 kg",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(251, 191, 36),
            Width = 250,
            Height = 28,
            AutoSize = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        contentFlow.Controls.Add(_lblCalculatedDesi);

        contentFlow.ClientSizeChanged += (_, _) =>
        {
            int targetW = Math.Max(230, contentFlow.ClientSize.Width - 10);
            detailsCard.Width = targetW;
            lblPackageHeader.Width = targetW;
            pnlPackageGrid.Width = targetW;
            lblGtipTitle.Width = targetW;
            _cmbHsCode.Width = targetW;
            _lblCalculatedDesi.Width = targetW;
        };

        colPanel.Controls.Add(contentFlow);
        contentFlow.BringToFront();

        return colPanel;
    }
    #endregion

    #region 4. Kolon 3: Package Customization / Live Multi-Carrier Shipping Comparison
    private Control BuildCarriersColumn()
    {
        var colPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };

        // Üst Başlık & Açıklamalar & Filtre Hapları
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.Transparent };

        var lblMainTitle = new Label
        {
            Text = "Package Customization",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(4, 4),
            AutoSize = true
        };
        var lblSubTitle = new Label
        {
            Text = "Live Multi-Carrier Shipping Comparison",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(4, 25),
            AutoSize = true
        };
        var lblDesc = new Label
        {
            Text = "Real-time rate quotes from omnicommercial carriers and multi-carrier shipping",
            Font = new Font("Segoe UI", 7.8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(4, 45),
            Size = new Size(270, 22)
        };

        // Filtreleme Hapları (Pills)
        _pnlFilterPills = new FlowLayoutPanel
        {
            Location = new Point(2, 70),
            Size = new Size(340, 30),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        string[] filterNames = { "Tümü", "Aras Global", "ShipEntegra", "Navlungo", "Shiptomore" };
        foreach (var fname in filterNames)
        {
            var btnPill = new Button
            {
                Text = fname,
                Tag = fname,
                Height = 26,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 7.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0),
                Padding = new Padding(6, 0, 6, 0)
            };
            btnPill.FlatAppearance.BorderSize = 1;
            UpdatePillStyle(btnPill, fname == _selectedCarrierFilter);
            btnPill.Click += (s, e) =>
            {
                _selectedCarrierFilter = (string)((Button)s!).Tag!;
                foreach (Control c in _pnlFilterPills.Controls)
                {
                    if (c is Button b)
                    {
                        UpdatePillStyle(b, (string)b.Tag! == _selectedCarrierFilter);
                    }
                }
                RenderCarrierCards();
            };
            _pnlFilterPills.Controls.Add(btnPill);
        }

        pnlHeader.Controls.Add(lblMainTitle);
        pnlHeader.Controls.Add(lblSubTitle);
        pnlHeader.Controls.Add(lblDesc);
        pnlHeader.Controls.Add(_pnlFilterPills);
        colPanel.Controls.Add(pnlHeader);

        // Canlı Teklif Kartları Listesi
        _carriersFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 4, 0)
        };
        _carriersFlowPanel.ClientSizeChanged += (_, _) =>
        {
            int targetWidth = Math.Max(260, _carriersFlowPanel.ClientSize.Width - 10);
            foreach (Control c in _carriersFlowPanel.Controls)
            {
                if (c is SaasCarrierCardControl card)
                {
                    card.Width = targetWidth;
                }
            }
        };

        colPanel.Controls.Add(_carriersFlowPanel);
        _carriersFlowPanel.BringToFront();

        // Alt Bilgilendirme Notu
        _pnlDisclaimer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(17, 24, 39),
            Padding = new Padding(8)
        };
        _lblDisclaimerNotes = new Label
        {
            Text = "ℹ️ Seçilen kargo taşıyıcısının gümrük ve teslimat şartları uygulanır.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Dock = DockStyle.Fill
        };
        _pnlDisclaimer.Controls.Add(_lblDisclaimerNotes);
        colPanel.Controls.Add(_pnlDisclaimer);

        return colPanel;
    }

    private static void UpdatePillStyle(Button btn, bool isActive)
    {
        if (isActive)
        {
            btn.BackColor = Color.FromArgb(16, 185, 129); // Vibrant emerald #10B981
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = Color.FromArgb(16, 185, 129);
        }
        else
        {
            btn.BackColor = Color.FromArgb(21, 30, 48);
            btn.ForeColor = Color.FromArgb(148, 163, 184);
            btn.FlatAppearance.BorderColor = Color.FromArgb(37, 51, 71);
        }
    }
    #endregion

    #region 5. Kolon 4: Action Panel (Shipment Actions)
    private Control BuildActionPanelColumn()
    {
        var colPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };

        // Üst Başlık Satırı: Action Panel + Kapatma İkonu
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
        var lblTitle = new Label
        {
            Text = "Action Panel",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(4, 8),
            AutoSize = true
        };
        var btnClose = new Label
        {
            Text = "✕",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(colPanel.Width - 32, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(btnClose);
        colPanel.Controls.Add(pnlHeader);

        var lblSub = new Label
        {
            Text = "Shipment Actions",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(4, 0, 0, 0)
        };
        colPanel.Controls.Add(lblSub);

        var scrollContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 4, 0)
        };

        // 1. Sender Address
        var lblSender = new Label
        {
            Text = "Sender Address",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Width = 250,
            Height = 18,
            Margin = new Padding(0, 0, 0, 2)
        };
        _cmbSenderAddress = new ComboBox
        {
            Width = 250,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 0, 8)
        };
        _cmbSenderAddress.Items.Add("Ana Depo (Ankara Altındağ - ERHAN KESKİN)");
        _cmbSenderAddress.Items.Add("Main Warehouse, Berlin, DE");
        _cmbSenderAddress.SelectedIndex = 0;
        scrollContainer.Controls.Add(lblSender);
        scrollContainer.Controls.Add(_cmbSenderAddress);

        // 2. Receiver Summary Kartı
        var pnlReceiver = new SaasCardPanel
        {
            Width = 250,
            Height = 92,
            CardBackground = Color.FromArgb(21, 30, 48),
            BorderColor = Color.FromArgb(37, 51, 71),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10)
        };
        var lblRecTitle = new Label { Text = "Receiver Summary", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(8, 6), AutoSize = true };
        _lblReceiverSummaryName = new Label { Text = "Inge Neuer", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(8, 24), AutoSize = true };
        _lblReceiverSummaryStreet = new Label { Text = "Am Rheinufer 12", Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(203, 213, 225), Location = new Point(8, 42), AutoSize = true };
        _lblReceiverSummaryCity = new Label { Text = "56068 Koblenz", Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(203, 213, 225), Location = new Point(8, 58), AutoSize = true };
        _lblReceiverSummaryCountry = new Label { Text = "Germany", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(56, 189, 248), Location = new Point(8, 72), AutoSize = true };

        pnlReceiver.Controls.Add(lblRecTitle);
        pnlReceiver.Controls.Add(_lblReceiverSummaryName);
        pnlReceiver.Controls.Add(_lblReceiverSummaryStreet);
        pnlReceiver.Controls.Add(_lblReceiverSummaryCity);
        pnlReceiver.Controls.Add(_lblReceiverSummaryCountry);
        scrollContainer.Controls.Add(pnlReceiver);

        // 3. IOSS Tax Info Kartı
        var pnlIoss = new SaasCardPanel
        {
            Width = 250,
            Height = 65,
            CardBackground = Color.FromArgb(21, 30, 48),
            BorderColor = Color.FromArgb(37, 51, 71),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10)
        };
        var lblIossHead = new Label { Text = "IOSS Tax Info", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(8, 6), AutoSize = true };
        _lblIossAppliedRate = new Label { Text = "Applied rate: €14.50", Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(203, 213, 225), Location = new Point(8, 24), AutoSize = true };
        _lblIossId = new Label { Text = "ID: IM3720000224", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), Location = new Point(8, 42), AutoSize = true };

        pnlIoss.Controls.Add(lblIossHead);
        pnlIoss.Controls.Add(_lblIossAppliedRate);
        pnlIoss.Controls.Add(_lblIossId);
        scrollContainer.Controls.Add(pnlIoss);

        // 4. Canlı Barkod Etiketi Önizlemesi (Görseldeki Gerçekçi Beyaz Barkod Kartı)
        _barcodeControl = new SaasBarcodeLabelControl
        {
            Width = 250,
            Height = 98,
            Margin = new Padding(0, 0, 0, 8)
        };
        scrollContainer.Controls.Add(_barcodeControl);

        // 5. Ücret Özeti (USD & TRY)
        var pnlPricing = new Panel { Width = 250, Height = 50, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 6) };
        _lblBasePrice = new Label { Text = "Ücret: $13.13 (≈ 641.47 ₺)", ForeColor = Color.FromArgb(16, 185, 129), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Dock = DockStyle.Top, AutoSize = true };
        _lblFinalPriceTry = new Label { Text = "Tüm vergiler dahil (DDP)", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8f), Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        pnlPricing.Controls.Add(_lblFinalPriceTry);
        pnlPricing.Controls.Add(_lblBasePrice);
        scrollContainer.Controls.Add(pnlPricing);

        // 6. Büyük Zümrüt Yeşili Gönderi Oluştur Butonu
        _btnCreateShipment = new Button
        {
            Text = "Aras Global ile Gönderi Oluştur",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(16, 185, 129), // Emerald #10B981
            FlatStyle = FlatStyle.Flat,
            Width = 250,
            Height = 44,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 6)
        };
        _btnCreateShipment.FlatAppearance.BorderSize = 0;
        _btnCreateShipment.Click += async (s, e) => await ExecuteShipmentCreationAsync();
        scrollContainer.Controls.Add(_btnCreateShipment);

        // 7. Durum ve Hata Bildirimi (Butonun Hemen Altında)
        _lblStatusMsg = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(251, 191, 36),
            Font = new Font("Segoe UI", 8f),
            Width = 250,
            Height = 54,
            TextAlign = ContentAlignment.TopCenter,
            Margin = new Padding(0, 0, 0, 4)
        };
        scrollContainer.Controls.Add(_lblStatusMsg);

        scrollContainer.ClientSizeChanged += (_, _) =>
        {
            int targetW = Math.Max(220, scrollContainer.ClientSize.Width - 10);
            lblSender.Width = targetW;
            _cmbSenderAddress.Width = targetW;
            pnlReceiver.Width = targetW;
            pnlIoss.Width = targetW;
            _barcodeControl.Width = targetW;
            pnlPricing.Width = targetW;
            _btnCreateShipment.Width = targetW;
            _lblStatusMsg.Width = targetW;
        };

        colPanel.Controls.Add(scrollContainer);
        scrollContainer.BringToFront();

        return colPanel;
    }
    #endregion

    #region 6. Veri Yükleme & Eşleştirme
    private async Task ReloadOrdersAsync(string? filter = null)
    {
        _ordersFlowPanel.Controls.Clear();
        _orders = await _orderService.GetOrdersAsync(filter);
        _lblOrderCount.Text = $"{_orders.Count} adet sipariş bulundu";

        foreach (var order in _orders)
        {
            var card = new SaasOrderCardControl(order);
            card.OrderSelected += (s, e) => SelectOrder(order);
            _ordersFlowPanel.Controls.Add(card);
        }

        if (_orders.Count > 0 && _selectedOrder == null)
        {
            SelectOrder(_orders[0]);
        }
    }

    private void SelectOrder(EtsyOrderFulfillmentItem order)
    {
        _selectedOrder = order;

        foreach (Control c in _ordersFlowPanel.Controls)
        {
            if (c is SaasOrderCardControl card)
            {
                card.IsSelected = (card.Order == order);
            }
        }

        // Kolon 2 Bilgileri
        _lblBuyerName.Text = order.BuyerName;
        _lblBuyerAddressLine1.Text = order.StreetAddress;
        _lblBuyerAddressLine2.Text = $"{order.PostalCode} {order.City}";
        _lblBuyerCountry.Text = $"{order.CountryName} ({order.CountryCode})";

        // Kolon 4 Bilgileri
        _lblReceiverSummaryName.Text = order.BuyerName;
        _lblReceiverSummaryStreet.Text = order.StreetAddress;
        _lblReceiverSummaryCity.Text = $"{order.PostalCode} {order.City}";
        _lblReceiverSummaryCountry.Text = order.CountryName;
        _lblIossId.Text = !string.IsNullOrWhiteSpace(order.IossNumber) ? $"ID: {order.IossNumber}" : "ID: Yok (Standart)";

        // Barkod Güncelleme
        _barcodeControl.ReceiverName = order.BuyerName;
        _barcodeControl.AddressLine = order.StreetAddress;

        if (order.Items.Count > 0)
        {
            var itm = order.Items[0];
            _inputWeight.Value = itm.WeightKg > 0 ? itm.WeightKg.ToString("0.00") : "0.40";
            _inputLength.Value = itm.LengthCm > 0 ? itm.LengthCm.ToString("0") : "20";
            _inputWidth.Value = itm.WidthCm > 0 ? itm.WidthCm.ToString("0") : "15";
            _inputHeight.Value = itm.HeightCm > 0 ? itm.HeightCm.ToString("0") : "10";
        }

        _ = RecalculateDesiAndQuotesAsync();
    }

    private async Task RecalculateDesiAndQuotesAsync()
    {
        if (_selectedOrder == null) return;

        decimal kg = _inputWeight.GetDecimal();
        double w = (double)_inputWidth.GetDecimal();
        double l = (double)_inputLength.GetDecimal();
        double h = (double)_inputHeight.GetDecimal();

        if (w <= 0) w = 15;
        if (l <= 0) l = 20;
        if (h <= 0) h = 10;
        if (kg <= 0) kg = 0.40m;

        double desi = Math.Round((w * l * h) / 5000.0, 2);
        double billable = Math.Max((double)kg, desi);
        _lblCalculatedDesi.Text = $"Desi: {desi:N2} | Faturalandırılacak: {billable:N2} kg";

        await GenerateCarrierQuotesAsync(billable, w, l, h);
        RenderCarrierCards();
    }

    private async Task GenerateCarrierQuotesAsync(double billableWeight, double w, double l, double h)
    {
        _allQuotes.Clear();

        string countryCode = !string.IsNullOrWhiteSpace(_selectedOrder?.CountryCode) ? _selectedOrder.CountryCode : "US";
        double weight = billableWeight;
        double width = w;
        double length = l;
        double height = h;

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
                return await _arasPricingService.GetQuotesAsync(req);
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
                return await _shipEntegraPricingService.GetQuotesAsync(req);
            }
            catch
            {
                return null;
            }
        });

        // 3. Navlungo Sorgusu
        var navSettings = NavlungoSettingsStore.Load();
        var navlungoTask = Task.Run(async () =>
        {
            try
            {
                var req = new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height,
                    Source = "user"
                };
                return await _navlungoApiClient.FetchLiveQuotesAsync(req, navSettings);
            }
            catch
            {
                return NavlungoApiClient.GenerateRealisticFallbackQuotes(new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height,
                    Source = "user"
                });
            }
        });

        // 4. Shiptomore Sorgusu
        var stmSettings = ShiptomoreSettingsStore.Load();
        var shiptomoreTask = Task.Run(async () =>
        {
            try
            {
                var req = new ShiptomoreQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                };
                return await _shiptomoreApiClient.FetchLiveQuotesAsync(req, stmSettings);
            }
            catch
            {
                return ShiptomoreApiClient.GenerateRealisticFallbackQuotes(new ShiptomoreQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = countryCode,
                    WeightKg = weight,
                    WidthCm = width,
                    LengthCm = length,
                    HeightCm = height
                }, !string.IsNullOrWhiteSpace(stmSettings.SessionCookie));
            }
        });

        await Task.WhenAll(arasTask, shipEntegraTask, navlungoTask, shiptomoreTask);

        var arasRes = await arasTask;
        var seRes = await shipEntegraTask;
        var navOffers = await navlungoTask;
        var stmOffers = await shiptomoreTask;

        // 1. Aras Global Tekliflerini Ekle
        if (arasRes != null && arasRes.Success && arasRes.Offers.Count > 0)
        {
            foreach (var off in arasRes.Offers)
            {
                string sName = $"{off.Cargo} {off.ProviderServiceType}".Trim();
                _allQuotes.Add(new CarrierQuoteCardModel
                {
                    ProviderName = "Aras Global",
                    CarrierName = "Aras Global",
                    ServiceName = sName,
                    SubCarrier = off.Cargo,
                    ServiceType = sName,
                    PriceUsd = off.Price,
                    PriceEur = Math.Round(off.Price * 0.92m, 2),
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryDaysText = off.DeliveryDaysText,
                    IsAras = true,
                    IsLive = off.IsLivePrice,
                    DisclaimerNote = off.IsLivePrice ? "Aras Global canlı DDP entegrasyonu." : "Yedek tarife (DDP Avrupa teslimat).",
                    ExchangeRate = UsdTryRate
                });
            }
        }

        // 2. ShipEntegra Tekliflerini Ekle
        if (seRes != null && seRes.Success && seRes.Offers.Count > 0)
        {
            foreach (var off in seRes.Offers)
            {
                string sName = !string.IsNullOrWhiteSpace(off.ClearServiceName) ? off.ClearServiceName : off.ServiceName;
                _allQuotes.Add(new CarrierQuoteCardModel
                {
                    ProviderName = "ShipEntegra",
                    CarrierName = "ShipEntegra",
                    ServiceName = sName,
                    SubCarrier = ExtractSubCarrierName(off.ServiceName),
                    ServiceType = sName,
                    PriceUsd = off.TotalPrice,
                    PriceEur = Math.Round(off.TotalPrice * 0.92m, 2),
                    PriceTry = Math.Round(off.TotalPrice * UsdTryRate, 2),
                    DeliveryDaysText = ExtractDeliveryTime(off.AdditionalDescription),
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.AdditionalDescription),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.AdditionalDescription),
                    IsAras = false,
                    IsLive = off.IsLivePrice,
                    DisclaimerNote = CleanHtml(off.AdditionalDescription),
                    ExchangeRate = UsdTryRate
                });
            }
        }

        // 3. Navlungo Tekliflerini Ekle
        if (navOffers != null && navOffers.Count > 0)
        {
            foreach (var off in navOffers)
            {
                bool isLiveOffer = off.Note.Contains("Canlı", StringComparison.OrdinalIgnoreCase) ||
                                   (!off.Note.Contains("Referans", StringComparison.OrdinalIgnoreCase) && !off.Note.Contains("Yedek", StringComparison.OrdinalIgnoreCase));
                _allQuotes.Add(new CarrierQuoteCardModel
                {
                    ProviderName = "Navlungo",
                    CarrierName = "Navlungo",
                    ServiceName = off.ServiceName,
                    SubCarrier = off.Carrier,
                    ServiceType = off.ServiceName,
                    PriceUsd = off.Price,
                    PriceEur = Math.Round(off.Price * 0.92m, 2),
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryDaysText = off.DeliveryEstimate,
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.DeliveryEstimate),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.DeliveryEstimate),
                    IsAras = false,
                    IsLive = isLiveOffer,
                    DisclaimerNote = off.Note,
                    ExchangeRate = UsdTryRate
                });
            }
        }

        // 4. Shiptomore Tekliflerini Ekle
        if (stmOffers != null && stmOffers.Count > 0)
        {
            foreach (var off in stmOffers)
            {
                bool isLiveOffer = off.Note.Contains("Canlı", StringComparison.OrdinalIgnoreCase) ||
                                   (!off.Note.Contains("Simüle", StringComparison.OrdinalIgnoreCase) && !off.Note.Contains("Yedek", StringComparison.OrdinalIgnoreCase));
                _allQuotes.Add(new CarrierQuoteCardModel
                {
                    ProviderName = "Shiptomore",
                    CarrierName = "Shiptomore",
                    ServiceName = off.ServiceName,
                    SubCarrier = off.Carrier,
                    ServiceType = off.ServiceName,
                    PriceUsd = off.Price,
                    PriceEur = Math.Round(off.Price * 0.92m, 2),
                    PriceTry = Math.Round(off.Price * UsdTryRate, 2),
                    DeliveryDaysText = off.DeliveryEstimate,
                    DeliveryDaysMin = ExtractDeliveryDaysMin(off.DeliveryEstimate),
                    DeliveryDaysMax = ExtractDeliveryDaysMax(off.DeliveryEstimate),
                    IsAras = false,
                    IsLive = isLiveOffer,
                    DisclaimerNote = off.Note,
                    ExchangeRate = UsdTryRate
                });
            }
        }

        // En ucuz teklifi belirle
        if (_allQuotes.Count > 0)
        {
            decimal minPrice = _allQuotes.Min(q => q.PriceUsd);
            foreach (var q in _allQuotes)
            {
                q.IsCheapest = (q.PriceUsd == minPrice);
            }
        }
    }

    private void RenderCarrierCards()
    {
        _carriersFlowPanel.Controls.Clear();

        var filtered = _allQuotes.AsEnumerable();
        if (!string.Equals(_selectedCarrierFilter, "Tümü", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(q => string.Equals(q.ProviderName, _selectedCarrierFilter, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.OrderBy(q => q.PriceUsd).ToList();
        int targetWidth = Math.Max(260, _carriersFlowPanel.ClientSize.Width - 10);

        foreach (var q in list)
        {
            var card = new SaasCarrierCardControl
            {
                ProviderName = q.ProviderName,
                CarrierName = q.CarrierName,
                SubCarrier = q.SubCarrier,
                ServiceType = q.ServiceName,
                Price = q.PriceUsd,
                Currency = "$",
                PriceTry = q.PriceTry,
                DeliveryDays = q.DeliveryDaysText,
                IsCheapest = q.IsCheapest,
                IsLive = q.IsLive,
                Width = targetWidth,
                AssociatedModel = q
            };

            card.CarrierSelected += (s, e) => SelectCarrier(q);
            _carriersFlowPanel.Controls.Add(card);
        }

        if (list.Count > 0 && (_selectedCarrier == null || !list.Contains(_selectedCarrier)))
        {
            SelectCarrier(list[0]);
        }
        else if (_selectedCarrier != null && list.Contains(_selectedCarrier))
        {
            SelectCarrier(_selectedCarrier);
        }
    }

    private void SelectCarrier(CarrierQuoteCardModel carrier)
    {
        _selectedCarrier = carrier;

        foreach (Control c in _carriersFlowPanel.Controls)
        {
            if (c is SaasCarrierCardControl card)
            {
                card.IsSelected = (card.AssociatedModel == carrier);
            }
        }

        _barcodeControl.CarrierName = carrier.ServiceName;
        _lblDisclaimerNotes.Text = !string.IsNullOrWhiteSpace(carrier.DisclaimerNote)
            ? $"ℹ️ {carrier.DisclaimerNote}"
            : $"ℹ️ {carrier.ProviderName} ({carrier.ServiceName}) teslimat ve gümrük koşulları geçerlidir.";

        UpdateActionPanelFinancials();
    }

    private void UpdateActionPanelFinancials()
    {
        if (_selectedOrder == null || _selectedCarrier == null) return;

        decimal priceEur = _selectedCarrier.PriceEur;
        decimal priceUsd = _selectedCarrier.PriceUsd;
        decimal rate = _selectedCarrier.ExchangeRate > 0 ? _selectedCarrier.ExchangeRate : UsdTryRate;
        decimal totalTry = Math.Round(priceUsd * rate, 2);

        _lblIossAppliedRate.Text = $"Applied rate: ${priceUsd:N2} (€{priceEur:N2})";
        _lblBasePrice.Text = $"Ücret: ${priceUsd:N2} (≈ {totalTry:N2} ₺)";
        _lblFinalPriceTry.Text = $"{_selectedCarrier.ProviderName} • {_selectedCarrier.ServiceName}";

        _btnCreateShipment.Text = $"{_selectedCarrier.ProviderName} ile Gönderi Oluştur";

        if (!_selectedCarrier.IsAras)
        {
            _btnCreateShipment.BackColor = Color.FromArgb(71, 85, 105);
            _lblStatusMsg.Text = "Not: Aras Global dışındaki API entegrasyonu sonraki fazda eklenecektir.";
        }
        else
        {
            _btnCreateShipment.BackColor = Color.FromArgb(16, 185, 129); // Vibrant emerald #10B981
            _lblStatusMsg.Text = "";
        }
    }

    private static string ExtractSubCarrierName(string serviceName)
    {
        if (serviceName.Contains("eko", StringComparison.OrdinalIgnoreCase)) return "Eko Plus";
        if (serviceName.Contains("smart", StringComparison.OrdinalIgnoreCase)) return "Smart (FedEx/TNT)";
        if (serviceName.Contains("ups", StringComparison.OrdinalIgnoreCase)) return "UPS";
        if (serviceName.Contains("widect", StringComparison.OrdinalIgnoreCase)) return "Widect";
        if (serviceName.Contains("express", StringComparison.OrdinalIgnoreCase)) return "Express";
        if (serviceName.Contains("fedex", StringComparison.OrdinalIgnoreCase)) return "FedEx";
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
        return 3;
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
    #endregion

    #region 7. Aras Global Gönderi Oluşturma Akışı & Oturum Yenileme
    private async Task PromptOrRefreshArasSessionAsync()
    {
        var settings = ArasGlobalSettingsStore.Load();
        string email = settings.SavedEmail;
        string pass = !string.IsNullOrWhiteSpace(settings.EncryptedPassword)
            ? ShippingCredentialEncryptor.Decrypt(settings.EncryptedPassword)
            : string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            using var dlg = new ShippingLoginCredentialsDialog("Aras Global", email);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            email = dlg.Email;
            pass = dlg.Password;
            settings.SavedEmail = email;
            settings.EncryptedPassword = ShippingCredentialEncryptor.Encrypt(pass);
            settings.AutoRefreshEnabled = dlg.AutoRefresh;
            ArasGlobalSettingsStore.Save(settings);
        }

        _btnArasSession.Text = "⏳ Giriş Yapılıyor...";
        _btnArasSession.Enabled = false;

        try
        {
            string? fresh = await _sessionManager.RefreshArasGlobalTokenAsync(
                email,
                pass,
                showBrowser: false,
                knownExpiredToken: settings.CleanToken);
            if (!string.IsNullOrWhiteSpace(fresh))
            {
                settings.BearerToken = fresh.Trim();
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                ArasGlobalSettingsStore.Save(settings);
                MessageBox.Show("Aras Global canlı oturumu başarıyla açıldı ve yeni token alındı!", "Oturum Açıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var ask = MessageBox.Show(
                    "Otomatik oturum açılamadı. Aras Global panelini tarayıcıda açarak manuel giriş yapmak ister misiniz?",
                    "Tarayıcıda Aç",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (ask == DialogResult.Yes)
                {
                    await _sessionManager.RefreshArasGlobalTokenAsync(
                        email,
                        pass,
                        showBrowser: true,
                        knownExpiredToken: settings.CleanToken);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Oturum açma hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnArasSession.Enabled = true;
            UpdateSessionButtonState();
        }
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

        if (_selectedCarrier.IsAras)
        {
            var arasSettings = ArasGlobalSettingsStore.Load();
            if (!arasSettings.HasValidTokenFormat)
            {
                var ask = MessageBox.Show(
                    "Aras Global oturum tokeni bulunamadı veya süresi dolmuş.\n\nKargo oluşturabilmek için şimdi Aras Global oturumunu yenilemek ister misiniz?",
                    "Aras Global Oturumu Gerekli",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ask == DialogResult.Yes)
                {
                    await PromptOrRefreshArasSessionAsync();
                    arasSettings = ArasGlobalSettingsStore.Load();
                    if (!arasSettings.HasValidTokenFormat)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        _btnCreateShipment.Enabled = false;
        _btnCreateShipment.Text = "⏳ Gönderi Oluşturuluyor...";
        _lblStatusMsg.ForeColor = Color.FromArgb(56, 189, 248);
        _lblStatusMsg.Text = "Aras Global API ile gönderi taslağı ve sözleşmeler onaylanıyor...";

        try
        {
            decimal kg = _inputWeight.GetDecimal();
            double w = (double)_inputWidth.GetDecimal();
            double l = (double)_inputLength.GetDecimal();
            double h = (double)_inputHeight.GetDecimal();

            if (kg <= 0) kg = 0.5m;
            if (w <= 0) w = 15;
            if (l <= 0) l = 20;
            if (h <= 0) h = 10;

            var context = new ShipmentCreationContext
            {
                Order = _selectedOrder,
                WeightKg = (double)kg,
                WidthCm = w,
                LengthCm = l,
                HeightCm = h,
                HsCode = _cmbHsCode.SelectedItem?.ToString()?.Split(' ')[0] ?? "3926400000",
                SelectedSubCarrier = _selectedCarrier.SubCarrier,
                ServiceType = _selectedCarrier.ServiceType
            };

            var result = await _creationManager.CreateShipmentAsync(_selectedCarrier.CarrierName, context);

            if (result.IsSuccess)
            {
                _lblStatusMsg.ForeColor = Color.FromArgb(16, 185, 129);
                _lblStatusMsg.Text = $"✓ Başarılı! Takip No: {result.TrackingNumber}";
                _barcodeControl.BarcodeNumber = result.TrackingNumber;

                await _orderService.MarkOrderAsShippedAsync(_selectedOrder.ReceiptId, _selectedCarrier.CarrierName, result.TrackingNumber, result.LabelUrl);

                MessageBox.Show(
                    $"Kargo gönderisi başarıyla oluşturuldu!\n\n" +
                    $"• Takip No: {result.TrackingNumber}\n" +
                    $"• Taşıyıcı: {_selectedCarrier.CarrierName}\n" +
                    $"• Alıcı: {_selectedOrder.BuyerName}\n" +
                    $"• Ücret: €{_selectedCarrier.PriceEur:N2}\n\n" +
                    $"Etsy siparişi 'Gönderildi' olarak güncellendi.",
                    "Kargo Başarıyla Oluşturuldu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await ReloadOrdersAsync();
            }
            else
            {
                _lblStatusMsg.ForeColor = Color.FromArgb(239, 68, 68);
                _lblStatusMsg.Text = $"Hata: {result.ErrorMessage}";

                if (result.ErrorMessage.Contains("token") || result.ErrorMessage.Contains("401"))
                {
                    var ask = MessageBox.Show(
                        $"{result.ErrorMessage}\n\nŞimdi tek tıkla Aras Global oturumunu yenilemek ister misiniz?",
                        "Oturum Yenileme",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (ask == DialogResult.Yes)
                    {
                        await PromptOrRefreshArasSessionAsync();
                    }
                }
                else
                {
                    MessageBox.Show(result.ErrorMessage, "Gönderi Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
            _btnCreateShipment.Text = $"{_selectedCarrier.CarrierName} ile Gönderi Oluştur";
            UpdateSessionButtonState();
        }
    }
    #endregion

    private sealed class CarrierQuoteCardModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string CarrierName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal PriceEur { get; set; }
        public decimal PriceUsd { get; set; }
        public decimal PriceTry { get; set; }
        public string DeliveryDaysText { get; set; } = string.Empty;
        public double DeliveryDaysMin { get; set; }
        public double DeliveryDaysMax { get; set; }
        public bool IsCheapest { get; set; }
        public bool IsAras { get; set; }
        public bool IsLive { get; set; }
        public string DisclaimerNote { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 48.855m;
    }
}
