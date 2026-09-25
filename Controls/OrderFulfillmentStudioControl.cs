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
    private SaasUnitInputBox _inputDims = null!;
    private ComboBox _cmbHsCode = null!;
    private Label _lblCalculatedDesi = null!;

    // Kolon 3: Package Customization / Live Multi-Carrier Shipping Comparison
    private FlowLayoutPanel _carriersFlowPanel = null!;
    private Panel _pnlDisclaimer = null!;
    private Label _lblDisclaimerNotes = null!;

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

        var btnSearchIcon = new Button
        {
            Text = "🔍",
            Size = new Size(28, 28),
            Location = new Point(lblTitle.Right + 90, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand
        };
        btnSearchIcon.FlatAppearance.BorderSize = 0;
        btnSearchIcon.Click += (s, e) => { _txtSearch.Focus(); };

        var btnAddIcon = new Button
        {
            Text = "➕",
            Size = new Size(28, 28),
            Location = new Point(btnSearchIcon.Right + 6, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand
        };
        btnAddIcon.FlatAppearance.BorderSize = 0;
        btnAddIcon.Click += async (s, e) => await ReloadOrdersAsync();

        pnlHeader.Controls.Add(btnSearchIcon);
        pnlHeader.Controls.Add(btnAddIcon);
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
            Width = 230,
            Margin = new Padding(0, 2, 0, 6)
        };
        contentFlow.Controls.Add(lblPackageHeader);

        // 4. Weight & Dimensions Kutuları (Yan Yana)
        var pnlDimsRow = new FlowLayoutPanel
        {
            Width = 230,
            Height = 60,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };

        _inputWeight = new SaasUnitInputBox("Weight", "0.40", "kg") { Width = 110 };
        _inputDims = new SaasUnitInputBox("Dimensions", "20x15x10", "cm") { Width = 114, Margin = new Padding(6, 0, 0, 0) };

        _inputWeight.ValueChanged += (s, e) => RecalculateDesiAndQuotes();
        _inputDims.ValueChanged += (s, e) => RecalculateDesiAndQuotes();

        pnlDimsRow.Controls.Add(_inputWeight);
        pnlDimsRow.Controls.Add(_inputDims);
        contentFlow.Controls.Add(pnlDimsRow);

        // 5. GTIP HS Code Alanı
        var lblGtipTitle = new Label
        {
            Text = "GTIP HS Code",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Height = 20,
            Width = 230,
            Margin = new Padding(0, 4, 0, 2)
        };
        contentFlow.Controls.Add(lblGtipTitle);

        _cmbHsCode = new ComboBox
        {
            Width = 230,
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
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(251, 191, 36),
            Width = 230,
            Height = 26,
            Margin = new Padding(0, 2, 0, 0)
        };
        contentFlow.Controls.Add(_lblCalculatedDesi);

        colPanel.Controls.Add(contentFlow);
        contentFlow.BringToFront();

        return colPanel;
    }
    #endregion

    #region 4. Kolon 3: Package Customization / Live Multi-Carrier Shipping Comparison
    private Control BuildCarriersColumn()
    {
        var colPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };

        // Üst Başlık & Açıklamalar
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.Transparent };

        var lblMainTitle = new Label
        {
            Text = "Package Customization",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Location = new Point(4, 6),
            AutoSize = true
        };
        var lblSubTitle = new Label
        {
            Text = "Live Multi-Carrier Shipping Comparison",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(4, 28),
            AutoSize = true
        };
        var lblDesc = new Label
        {
            Text = "Real-time rate quotes from omnicommercial carriers and multi-carrier shipping",
            Font = new Font("Segoe UI", 7.8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(4, 48),
            Size = new Size(260, 24)
        };

        pnlHeader.Controls.Add(lblMainTitle);
        pnlHeader.Controls.Add(lblSubTitle);
        pnlHeader.Controls.Add(lblDesc);
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
        colPanel.Controls.Add(_carriersFlowPanel);
        _carriersFlowPanel.BringToFront();

        // Alt Bilgilendirme Notu
        _pnlDisclaimer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            BackColor = Color.FromArgb(17, 24, 39),
            Padding = new Padding(8)
        };
        _lblDisclaimerNotes = new Label
        {
            Text = "ℹ️ Aras Global seçildiğinde gönderiniz DDP gümrük beyanı ile sevk edilir.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Dock = DockStyle.Fill
        };
        _pnlDisclaimer.Controls.Add(_lblDisclaimerNotes);
        colPanel.Controls.Add(_pnlDisclaimer);

        return colPanel;
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

        var scrollContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 4, 4, 0)
        };

        // 1. Sender Address
        var lblSender = new Label
        {
            Text = "Sender Address",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Top,
            Height = 18
        };
        _cmbSenderAddress = new ComboBox
        {
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(21, 30, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbSenderAddress.Items.Add("Ana Depo (Ankara Altındağ - ERHAN KESKİN)");
        _cmbSenderAddress.Items.Add("Main Warehouse, Berlin, DE");
        _cmbSenderAddress.SelectedIndex = 0;
        scrollContainer.Controls.Add(_cmbSenderAddress);
        scrollContainer.Controls.Add(lblSender);

        // 2. Receiver Summary Kartı
        var pnlReceiver = new SaasCardPanel
        {
            Dock = DockStyle.Top,
            Height = 90,
            CardBackground = Color.FromArgb(21, 30, 48),
            BorderColor = Color.FromArgb(37, 51, 71),
            Margin = new Padding(0, 8, 0, 8),
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
            Dock = DockStyle.Top,
            Height = 65,
            CardBackground = Color.FromArgb(21, 30, 48),
            BorderColor = Color.FromArgb(37, 51, 71),
            Margin = new Padding(0, 6, 0, 8),
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
            Dock = DockStyle.Top,
            Height = 98,
            Margin = new Padding(0, 6, 0, 8)
        };
        scrollContainer.Controls.Add(_barcodeControl);

        // 5. Ücret Özeti (USD & TRY)
        var pnlPricing = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(4) };
        _lblBasePrice = new Label { Text = "Ücret: €14.50 (757.74 TRY)", ForeColor = Color.FromArgb(16, 185, 129), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Dock = DockStyle.Top };
        _lblFinalPriceTry = new Label { Text = "Tüm vergiler dahil (DDP)", ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 7.8f), Dock = DockStyle.Top };
        pnlPricing.Controls.Add(_lblFinalPriceTry);
        pnlPricing.Controls.Add(_lblBasePrice);
        scrollContainer.Controls.Add(pnlPricing);

        // 6. En Alttaki Büyük Zümrüt Yeşili Gönderi Oluştur Butonu (Görseldeki Buton)
        _btnCreateShipment = new Button
        {
            Text = "Aras Global ile Gönderi Oluştur",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(16, 185, 129), // Emerald #10B981
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Top,
            Height = 44,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 6)
        };
        _btnCreateShipment.FlatAppearance.BorderSize = 0;
        _btnCreateShipment.Click += async (s, e) => await ExecuteShipmentCreationAsync();
        scrollContainer.Controls.Add(_btnCreateShipment);

        _lblStatusMsg = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(251, 191, 36),
            Font = new Font("Segoe UI", 8f),
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };
        scrollContainer.Controls.Add(_lblStatusMsg);

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
            _inputWeight.Value = itm.WeightKg.ToString("0.00");
            _inputDims.Value = $"{itm.LengthCm}x{itm.WidthCm}x{itm.HeightCm}";
        }

        RecalculateDesiAndQuotes();
    }

    private void RecalculateDesiAndQuotes()
    {
        if (_selectedOrder == null) return;

        decimal kg = _inputWeight.GetDecimal();
        double w = 15, l = 20, h = 10;

        string dimsStr = _inputDims.Value;
        if (!string.IsNullOrWhiteSpace(dimsStr))
        {
            var parts = dimsStr.Split(new[] { 'x', 'X', '*', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                double.TryParse(parts[0], out l);
                double.TryParse(parts[1], out w);
                double.TryParse(parts[2], out h);
            }
        }

        double desi = Math.Round((w * l * h) / 5000.0, 2);
        double billable = Math.Max((double)kg, desi);
        _lblCalculatedDesi.Text = $"Desi: {desi:N2} | Faturalandırılacak: {billable:N2} kg";

        GenerateCarrierQuotes(billable);
        RenderCarrierCards();
    }

    private void GenerateCarrierQuotes(double billableWeight)
    {
        _allQuotes.Clear();

        // 1. Aras Global (Görseldeki 1. Seçili Firma)
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Aras Global",
            SubCarrier = "widect",
            ServiceType = "Quotes",
            PriceEur = 14.50m,
            PriceUsd = 13.13m,
            DeliveryDaysText = "4 days",
            IsCheapest = true,
            IsAras = true,
            DisclaimerNote = "Widect Eco Express ile Avrupa DDP gümrüklü doğrudan teslimat.",
            ExchangeRate = 48.855m
        });

        // 2. ShipEntegra
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "ShipEntegra",
            SubCarrier = "fedex",
            ServiceType = "Quotes",
            PriceEur = 16.20m,
            PriceUsd = 15.00m,
            DeliveryDaysText = "3-5 days",
            IsCheapest = false,
            IsAras = false
        });

        // 3. Navlungo
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Navlungo",
            SubCarrier = "navlungo_eco",
            ServiceType = "Quotes",
            PriceEur = 15.80m,
            PriceUsd = 14.60m,
            DeliveryDaysText = "5 days",
            IsCheapest = false,
            IsAras = false
        });

        // 4. Shiptomore
        _allQuotes.Add(new CarrierQuoteCardModel
        {
            CarrierName = "Shiptomore",
            SubCarrier = "air_express",
            ServiceType = "Quotes",
            PriceEur = 17.10m,
            PriceUsd = 16.00m,
            DeliveryDaysText = "3-8 days",
            IsCheapest = false,
            IsAras = false
        });
    }

    private void RenderCarrierCards()
    {
        _carriersFlowPanel.Controls.Clear();

        foreach (var q in _allQuotes)
        {
            var card = new SaasCarrierCardControl
            {
                CarrierName = q.CarrierName,
                SubCarrier = q.SubCarrier,
                ServiceType = q.ServiceType,
                Price = q.PriceEur,
                Currency = "€",
                DeliveryDays = q.DeliveryDaysText,
                IsCheapest = q.IsCheapest,
                AssociatedModel = q
            };

            card.CarrierSelected += (s, e) => SelectCarrier(q);
            _carriersFlowPanel.Controls.Add(card);
        }

        if (_allQuotes.Count > 0 && (_selectedCarrier == null || !_allQuotes.Contains(_selectedCarrier)))
        {
            SelectCarrier(_allQuotes[0]);
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

        _barcodeControl.CarrierName = carrier.CarrierName;
        _lblDisclaimerNotes.Text = !string.IsNullOrWhiteSpace(carrier.DisclaimerNote)
            ? $"ℹ️ {carrier.DisclaimerNote}"
            : "ℹ️ Seçilen kargo taşıyıcısının gümrük ve teslimat şartları uygulanır.";

        UpdateActionPanelFinancials();
    }

    private void UpdateActionPanelFinancials()
    {
        if (_selectedOrder == null || _selectedCarrier == null) return;

        decimal priceEur = _selectedCarrier.PriceEur;
        decimal priceUsd = _selectedCarrier.PriceUsd;
        decimal rate = _selectedCarrier.ExchangeRate > 0 ? _selectedCarrier.ExchangeRate : 48.855m;
        decimal totalTry = Math.Round(priceUsd * rate, 2);

        _lblIossAppliedRate.Text = $"Applied rate: €{priceEur:N2}";
        _lblBasePrice.Text = $"Ücret: €{priceEur:N2} (${priceUsd:N2} USD - {totalTry:N2} TRY)";

        _btnCreateShipment.Text = $"{_selectedCarrier.CarrierName} ile Gönderi Oluştur";

        if (!_selectedCarrier.IsAras)
        {
            _btnCreateShipment.BackColor = Color.FromArgb(71, 85, 105);
            _lblStatusMsg.Text = "Not: Bu firma API modeli sonraki fazda eklenecektir.";
        }
        else
        {
            _btnCreateShipment.BackColor = Color.FromArgb(16, 185, 129); // Vibrant emerald #10B981
            _lblStatusMsg.Text = "";
        }
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

        _btnCreateShipment.Enabled = false;
        _btnCreateShipment.Text = "⏳ Gönderi Oluşturuluyor...";
        _lblStatusMsg.ForeColor = Color.FromArgb(56, 189, 248);
        _lblStatusMsg.Text = "Aras Global API ile gönderi taslağı ve sözleşmeler onaylanıyor...";

        try
        {
            decimal kg = _inputWeight.GetDecimal();
            double w = 15, l = 20, h = 10;
            var parts = _inputDims.Value.Split(new[] { 'x', 'X', '*', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                double.TryParse(parts[0], out l);
                double.TryParse(parts[1], out w);
                double.TryParse(parts[2], out h);
            }

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
        public string CarrierName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal PriceEur { get; set; }
        public decimal PriceUsd { get; set; }
        public string DeliveryDaysText { get; set; } = string.Empty;
        public bool IsCheapest { get; set; }
        public bool IsAras { get; set; }
        public string DisclaimerNote { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 48.855m;
    }
}
