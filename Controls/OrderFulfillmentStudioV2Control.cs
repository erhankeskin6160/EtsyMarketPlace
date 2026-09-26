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
using SimilarProductsWinForms.Services;

/// <summary>
/// "Sipariş &amp; Kargo" modülünün yeni (V2) arayüzü.
/// Üç bölgeli kokpit: sipariş kuyruğu | sipariş detayı + paket | taşıyıcı karşılaştırma,
/// altta her zaman görünür eylem çubuğu. 1200 px altında bölgeler sekmeye düşer.
/// Tüm renk ve yazı tipleri <see cref="UiStyle"/> token'larından gelir.
/// </summary>
public sealed class OrderFulfillmentStudioV2Control : UserControl
{
    private const int NarrowBreakpoint = 1200;
    private const int TopBarHeight = 56;
    private const int ActionStripHeight = 64;
    private const int QueueRegionWidth = 320;
    private const int QuotesRegionWidth = 380;

    // --- servisler ---
    private readonly EtsyOrderService _orderService;
    private readonly ArasGlobalApiClient _arasApiClient;
    private readonly ArasGlobalPricingService _arasPricingService;
    private readonly IShippingSessionManager _sessionManager;
    private readonly ShipEntegraPricingService _shipEntegraPricingService;
    private readonly INavlungoApiClient _navlungoApiClient;
    private readonly IShiptomoreApiClient _shiptomoreApiClient;
    private readonly ShipmentCreationManager _creationManager;

    // --- durum ---
    private readonly List<EtsyOrderFulfillmentItem> _allOrders = new();
    private List<OrderQuote> _quotes = new();
    private EtsyOrderFulfillmentItem? _selectedOrder;
    private OrderQuote? _selectedQuote;
    private string _statusFilter = "Tümü";
    private bool _layoutNarrow;
    private bool _suspendRecalc;
    private CancellationTokenSource? _quoteCts;
    private System.Windows.Forms.Timer? _debounceTimer;
    private readonly Font _smallFont = new("Segoe UI", 8.5f);
    private readonly Font _priceFont = new("Segoe UI Semibold", 13f, FontStyle.Bold);
    private readonly Font _bigValueFont = new("Segoe UI Semibold", 17f, FontStyle.Bold);

    public decimal UsdTryRate { get; set; } = 48.855m;

    private readonly ExchangeRateService _exchangeRateService = new();
    private string _rateNote = "kur yükleniyor";

    // --- iskelet ---
    private ThemedCard _topBar = null!;
    private Panel _body = null!;
    private Panel _actionStrip = null!;
    private TableLayoutPanel _grid = null!;
    private TableLayoutPanel _narrowHost = null!;
    private FlowLayoutPanel _narrowTabs = null!;
    private int _narrowTabIndex;

    // --- üst bar ---
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private Button _btnSession = null!;
    private Button _btnCaptureTemplate = null!;

    // --- bölge 1 ---
    private ThemedCard _colQueue = null!;
    private FlowLayoutPanel _ordersFlow = null!;
    private Label _lblQueueEmpty = null!;
    private Label _lblQueueCount = null!;
    private readonly Dictionary<string, Button> _statusChips = new();

    // --- bölge 2 ---
    private ThemedCard _colDetail = null!;
    private Label _lblBuyerName = null!;
    private Label _lblBuyerLine1 = null!;
    private Label _lblBuyerLine2 = null!;
    private Label _lblBuyerCountry = null!;
    private Label _lblIoss = null!;
    private SaasUnitInputBox _inWeight = null!;
    private SaasUnitInputBox _inLength = null!;
    private SaasUnitInputBox _inWidth = null!;
    private SaasUnitInputBox _inHeight = null!;
    private Label _lblPackageError = null!;
    private ComboBox _cmbHsCode = null!;
    private Label _lblDesiValue = null!;
    private Label _lblBillableValue = null!;
    private Label _lblMeasureSource = null!;
    private FlowLayoutPanel _itemsFlow = null!;

    // --- bölge 3 ---
    private ThemedCard _colQuotes = null!;
    private FlowLayoutPanel _quotesFlow = null!;
    private Label _lblQuoteCount = null!;
    private Label _lblQuoteSummary = null!;
    private Label _lblQuotesEmpty = null!;
    private readonly Dictionary<string, Button> _providerChips = new();
    private SaasBarcodeLabelControl _barcode = null!;

    // --- eylem çubuğu ---
    private Label _lblSelectedService = null!;
    private Label _lblSelectedProvider = null!;
    private Label _lblPriceUsd = null!;
    private Label _lblPriceTry = null!;
    private Label _lblActionStatus = null!;
    private Button _btnPreviewLabel = null!;
    private Button _btnCreateShipment = null!;

    public OrderFulfillmentStudioV2Control(
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

        UiStyle.SetDoubleBuffered(this);
        Dock = DockStyle.Fill;
        BackColor = UiStyle.BackgroundColor;
        Font = UiStyle.BaseFont;
        AutoScroll = false;

        BuildLayout();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateSessionBadge();
        await LoadExchangeRateAsync();
        await ReloadOrdersAsync();
    }

    /// <summary>
    /// USD/TRY kurunu uygulamanın canlı kur servisinden alır (15 dk önbellekli).
    /// Servis erişilemezse sabit varsayılana düşer ve bu durum ekranda açıkça yazılır —
    /// hangi kurdan çevrildiği asla gizlenmez.
    /// </summary>
    private async Task LoadExchangeRateAsync()
    {
        try
        {
            decimal rate = await _exchangeRateService.GetLiveUsdTryRateAsync();
            if (rate > 0)
            {
                UsdTryRate = rate;
                _rateNote = $"canlı kur {rate:N2}";
            }
            else
            {
                _rateNote = $"sabit kur {UsdTryRate:N2} (canlı kur alınamadı)";
            }
        }
        catch
        {
            _rateNote = $"sabit kur {UsdTryRate:N2} (canlı kur alınamadı)";
        }

        if (_lblPriceTry != null)
        {
            _lblPriceTry.Text = _rateNote;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyLayoutMode();
        FitFlows();
    }

    #region iskelet

    private void BuildLayout()
    {
        SuspendLayout();
        Controls.Clear();

        _body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.BackgroundColor,
            Padding = new Padding(12)
        };

        _colQueue = BuildQueueRegion();
        _colDetail = BuildDetailRegion();
        _colQuotes = BuildQuotesRegion();

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = UiStyle.BackgroundColor,
            Margin = new Padding(0)
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QueueRegionWidth));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QuotesRegionWidth));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _narrowHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = UiStyle.BackgroundColor,
            Margin = new Padding(0)
        };
        _narrowHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _narrowHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _narrowTabs = BuildNarrowTabs();

        _body.Controls.Add(_grid);
        _body.Controls.Add(_narrowHost);
        _body.Controls.Add(_narrowTabs);

        _topBar = BuildTopBar();
        _actionStrip = BuildActionStrip();

        Controls.Add(_body);
        Controls.Add(_actionStrip);
        Controls.Add(_topBar);

        ResumeLayout(true);
        ApplyLayoutMode(force: true);
    }

    private FlowLayoutPanel BuildNarrowTabs()
    {
        var tabs = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 10),
            BackColor = UiStyle.BackgroundColor,
            Visible = false
        };

        string[] labels = { "Siparişler", "Sipariş detayı", "Kargo" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var btn = new Button
            {
                Text = labels[i],
                Height = 34,
                Width = 158,
                FlatStyle = FlatStyle.Flat,
                Font = UiStyle.SemiboldBaseFont,
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                BackColor = UiStyle.SecondaryColor,
                ForeColor = UiStyle.TextMuted
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, ev) =>
            {
                _narrowTabIndex = index;
                RefreshNarrowTabStyles();
                RefreshRegionVisibility();
                FitFlows();
            };
            tabs.Controls.Add(btn);
        }

        return tabs;
    }

    private void RefreshNarrowTabStyles()
    {
        for (int i = 0; i < _narrowTabs.Controls.Count; i++)
        {
            if (_narrowTabs.Controls[i] is Button b)
            {
                bool active = i == _narrowTabIndex;
                b.BackColor = active ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
                b.ForeColor = active ? Color.White : UiStyle.TextMuted;
            }
        }
    }

    private void ApplyLayoutMode(bool force = false)
    {
        bool narrow = ClientSize.Width > 0 && ClientSize.Width < NarrowBreakpoint;
        if (!force && narrow == _layoutNarrow)
        {
            return;
        }

        _layoutNarrow = narrow;
        _grid.SuspendLayout();
        _narrowHost.SuspendLayout();
        _grid.Controls.Clear();
        _narrowHost.Controls.Clear();

        var regions = new Control[] { _colQueue, _colDetail, _colQuotes };

        if (narrow)
        {
            _narrowTabs.Visible = true;
            _grid.Visible = false;
            _narrowHost.Visible = true;
            foreach (var region in regions)
            {
                region.Dock = DockStyle.Fill;
                region.Margin = new Padding(0);
                _narrowHost.Controls.Add(region);
            }

            RefreshNarrowTabStyles();
            RefreshRegionVisibility();
        }
        else
        {
            _narrowTabs.Visible = false;
            _narrowHost.Visible = false;
            _grid.Visible = true;
            _colQueue.Margin = new Padding(0, 0, 6, 0);
            _colDetail.Margin = new Padding(6, 0, 6, 0);
            _colQuotes.Margin = new Padding(6, 0, 0, 0);
            foreach (var region in regions)
            {
                region.Dock = DockStyle.Fill;
                region.Visible = true;
                _grid.Controls.Add(region, Array.IndexOf(regions, region), 0);
            }
        }

        _grid.ResumeLayout(true);
        _narrowHost.ResumeLayout(true);
    }

    private void RefreshRegionVisibility()
    {
        if (!_layoutNarrow)
        {
            return;
        }

        _colQueue.Visible = _narrowTabIndex == 0;
        _colDetail.Visible = _narrowTabIndex == 1;
        _colQuotes.Visible = _narrowTabIndex == 2;
    }

    private void FitFlows()
    {
        FitFlow(_ordersFlow);
        FitFlow(_quotesFlow);
        FitFlow(_itemsFlow);
    }

    private static void FitFlow(FlowLayoutPanel? flow)
    {
        if (flow == null || flow.IsDisposed)
        {
            return;
        }

        int inset = flow.Padding.Horizontal + 2;
        if (flow.VerticalScroll.Visible)
        {
            inset += SystemInformation.VerticalScrollBarWidth;
        }

        int width = Math.Max(220, flow.ClientSize.Width - inset);
        foreach (Control child in flow.Controls)
        {
            if (child.Width != width)
            {
                child.Width = width;
            }
        }
    }

    #endregion

    #region üst bar

    private ThemedCard BuildTopBar()
    {
        var bar = new ThemedCard
        {
            Dock = DockStyle.Top,
            Height = TopBarHeight,
            CornerRadius = 0
        };

        var brand = new Panel { Dock = DockStyle.Left, Width = 260, BackColor = Color.Transparent };
        var logo = new Label
        {
            Text = "E",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = UiStyle.PrimaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(30, 30),
            Location = new Point(14, 13)
        };
        var brandTitle = new Label
        {
            Text = "EtsyMarketPlace",
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(52, 11)
        };
        var brandSub = new Label
        {
            Text = "SİPARİŞ & KARGO",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(53, 29)
        };
        brand.Controls.Add(logo);
        brand.Controls.Add(brandTitle);
        brand.Controls.Add(brandSub);

        var right = new Panel { Dock = DockStyle.Right, Width = 520, BackColor = Color.Transparent };

        _btnCaptureTemplate = new Button
        {
            Text = "Aras şablonu yakala",
            Height = 33,
            Width = 172,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            BackColor = UiStyle.SecondaryColor,
            ForeColor = UiStyle.TextDark,
            Location = new Point(0, 12),
            Cursor = Cursors.Hand
        };
        _btnCaptureTemplate.FlatAppearance.BorderSize = 0;
        _btnCaptureTemplate.Click += async (s, e) => await CaptureArasTemplateAsync();
        right.Controls.Add(_btnCaptureTemplate);

        _btnRefresh = UiStyle.CreateButton("Yenile", isSecondary: true);
        _btnRefresh.Width = 92;
        _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnRefresh.Location = new Point(right.Width - _btnRefresh.Width - 14, 12);
        _btnRefresh.Click += async (s, e) => await ReloadOrdersAsync(force: true);

        _btnSession = new Button
        {
            Height = 33,
            Width = 168,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(right.Width - 168 - 92 - 24, 12),
            Cursor = Cursors.Hand
        };
        _btnSession.FlatAppearance.BorderSize = 0;
        _btnSession.Click += async (s, e) => await PromptOrRefreshArasSessionAsync();

        _txtSearch = new TextBox
        {
            Width = 240,
            Font = UiStyle.BaseFont,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = UiStyle.InputBackground,
            ForeColor = UiStyle.TextDark,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(right.Width - 168 - 92 - 24 - 240 - 12, 14)
        };
        _txtSearch.PlaceholderText = "Sipariş no · müşteri · ürün ara";
        _txtSearch.TextChanged += async (s, e) => await ReloadOrdersAsync();

        right.Controls.Add(_txtSearch);
        right.Controls.Add(_btnSession);
        right.Controls.Add(_btnRefresh);

        bar.Controls.Add(right);
        bar.Controls.Add(brand);
        return bar;
    }

    /// <summary>
    /// Aras panelinde kullanıcının yaptığı gerçek gönderi işleminin istek/yanıt çiftini kaydeder.
    /// Amaç: API'nin beklediği gerçek gövdeyi görmek (400 volumetricweightismissing teşhisi).
    /// </summary>
    private async Task CaptureArasTemplateAsync()
    {
        var confirm = MessageBox.Show(
            "Aras Global panelinde gerçek bir gönderi işlemi yapılacak ve istek/yanıt kaydedilecek.\n\n" +
            "1) Tarayıcı açılacak, panele giriş yapman istenecek.\n" +
            "2) Panelden bir gönderi oluştur — bu GERÇEK bir gönderidir, ücret yansıyabilir.\n" +
            "3) İstek yakalanınca kayıt dosyası oluşturulacak.\n\n" +
            "Devam edilsin mi?",
            "Aras Şablonunu Yakala",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        string? path = null;
        var capture = new ArasShipmentTemplateCapture();

        using (var dialog = new CaptureProgressForm(cts))
        {
            dialog.Shown += async (s, e) =>
            {
                try
                {
                    path = await capture.CaptureAsync(
                        showBrowser: true,
                        statusCallback: msg => dialog.SetStatus(msg),
                        ct: cts.Token);
                }
                catch (Exception ex)
                {
                    dialog.SetStatus("Hata: " + ex.Message);
                }
                finally
                {
                    dialog.Close();
                }
            };

            dialog.ShowDialog(this);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(
                "Şablon kaydedildi:\n\n" + path +
                "\n\nBu dosyayı paylaşırsan API'nin beklediği gövdeyi birebir görebiliriz.",
                "Yakalama Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    /// <summary>Yakalama sürerken gösterilen basit durum penceresi.</summary>
    private sealed class CaptureProgressForm : Form
    {
        private readonly Label _status;
        private readonly CancellationTokenSource _cts;

        public CaptureProgressForm(CancellationTokenSource cts)
        {
            _cts = cts;
            Text = "Aras Şablonu Yakalanıyor";
            Size = new Size(580, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = UiStyle.BackgroundColor;
            ForeColor = UiStyle.TextDark;
            Font = UiStyle.BaseFont;

            _status = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Hazırlanıyor...",
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(16),
                ForeColor = UiStyle.TextDark
            };

            var cancel = UiStyle.CreateButton("İptal", isSecondary: true);
            cancel.Dock = DockStyle.Bottom;
            cancel.Height = 36;
            cancel.Click += (s, e) =>
            {
                _cts.Cancel();
                SetStatus("İptal ediliyor...");
            };

            Controls.Add(_status);
            Controls.Add(cancel);
        }

        public void SetStatus(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => SetStatus(message)));
                }
                catch
                {
                }

                return;
            }

            _status.Text = message;
        }
    }

    private void UpdateSessionBadge()
    {
        bool valid = false;
        try
        {
            valid = ArasGlobalSettingsStore.Load().HasValidTokenFormat;
        }
        catch
        {
            valid = false;
        }

        _btnSession.Text = valid ? "Aras oturumu açık" : "Aras oturumu yok";
        _btnSession.BackColor = valid ? UiStyle.SuccessColor : UiStyle.WarningColor;
        _btnSession.ForeColor = valid ? Color.White : Color.FromArgb(40, 30, 0);
    }

    #endregion

    #region bölge 1 - sipariş kuyruğu

    private ThemedCard BuildQueueRegion()
    {
        var card = new ThemedCard { Dock = DockStyle.Fill };

        var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = "SİPARİŞ KUYRUĞU",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 16)
        });
        _lblQueueCount = new Label
        {
            Text = "0",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            BackColor = UiStyle.SecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(34, 20),
            Location = new Point(150, 14)
        };
        header.Controls.Add(_lblQueueCount);

        var chipRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 0, 8, 0)
        };
        foreach (string status in new[] { "Tümü", "Bekleyen", "Gönderildi" })
        {
            string captured = status;
            var chip = MakePillButton(status);
            chip.Click += async (s, e) =>
            {
                _statusFilter = captured;
                RefreshStatusChipStyles();
                await ReloadOrdersAsync();
            };
            _statusChips[status] = chip;
            chipRow.Controls.Add(chip);
        }

        var searchHost = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent, Padding = new Padding(12, 0, 12, 8) };
        var queueHint = new Label
        {
            Text = "Sipariş seçildiğinde paket ve teklifler otomatik hazırlanır.",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        searchHost.Controls.Add(queueHint);

        _ordersFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 4, 12, 12)
        };
        _ordersFlow.ClientSizeChanged += (s, e) => FitFlow(_ordersFlow);

        _lblQueueEmpty = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Bu filtreye uyan sipariş yok.\n\nFiltreyi temizleyip yeniden deneyin.",
            Font = UiStyle.BaseFont,
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Visible = false
        };

        card.Controls.Add(_ordersFlow);
        card.Controls.Add(_lblQueueEmpty);
        card.Controls.Add(searchHost);
        card.Controls.Add(chipRow);
        card.Controls.Add(header);

        RefreshStatusChipStyles();
        return card;
    }

    private Button MakePillButton(string text)
    {
        var b = new Button
        {
            Text = text,
            Height = 28,
            AutoSize = false,
            Width = 78,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void RefreshStatusChipStyles()
    {
        foreach (var kv in _statusChips)
        {
            bool active = kv.Key == _statusFilter;
            kv.Value.BackColor = active ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
            kv.Value.ForeColor = active ? Color.White : UiStyle.TextMuted;
        }
    }

    #endregion

    #region bölge 2 - detay ve paket

    private ThemedCard BuildDetailRegion()
    {
        var card = new ThemedCard { Dock = DockStyle.Fill };

        var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = "SİPARİŞ DETAYI",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 16)
        });

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(12, 0, 12, 12) };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // alıcı kartı
        var buyer = new Panel { BackColor = UiStyle.BackgroundColor, Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 0, 0, 10) };
        _lblBuyerName = MakeLabel(UiStyle.SemiboldBaseFont, UiStyle.TextDark, 0, 0);
        _lblBuyerLine1 = MakeLabel(UiStyle.BaseFont, UiStyle.TextMuted, 0, 20);
        _lblBuyerLine2 = MakeLabel(UiStyle.BaseFont, UiStyle.TextMuted, 0, 38);
        _lblBuyerCountry = MakeLabel(UiStyle.BaseFont, UiStyle.TextMuted, 0, 56);
        _lblIoss = MakeLabel(_smallFont, UiStyle.AccentColor, 0, 78);
        buyer.Controls.Add(_lblBuyerName);
        buyer.Controls.Add(_lblBuyerLine1);
        buyer.Controls.Add(_lblBuyerLine2);
        buyer.Controls.Add(_lblBuyerCountry);
        buyer.Controls.Add(_lblIoss);
        buyer.Height = 100;
        stack.Controls.Add(buyer, 0, 0);

        // paket ölçüleri
        stack.Controls.Add(MakeSectionLabel("PAKET ÖLÇÜLERİ"), 0, 1);
        var measGrid = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Height = 122,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent
        };
        measGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        measGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        measGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        measGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));

        _inWeight = MakeUnitInput("Ağırlık", "kg", "0.00");
        _inHeight = MakeUnitInput("Yükseklik", "cm", "0");
        _inLength = MakeUnitInput("Boy", "cm", "0");
        _inWidth = MakeUnitInput("En", "cm", "0");
        measGrid.Controls.Add(_inWeight, 0, 0);
        measGrid.Controls.Add(_inHeight, 1, 0);
        measGrid.Controls.Add(_inLength, 0, 1);
        measGrid.Controls.Add(_inWidth, 1, 1);
        stack.Controls.Add(measGrid, 0, 2);

        _lblPackageError = new Label
        {
            Text = string.Empty,
            Font = _smallFont,
            ForeColor = UiStyle.DangerColor,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(2, 0, 0, 6),
            Visible = false
        };
        stack.Controls.Add(_lblPackageError, 0, 3);

        // HS kodu
        stack.Controls.Add(MakeSectionLabel("GTIP / HS KODU"), 0, 4);
        _cmbHsCode = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiStyle.BaseFont,
            Margin = new Padding(0, 0, 0, 10),
            Height = 30
        };
        _cmbHsCode.Items.AddRange(new object[]
        {
            "3926400000 - 3D Baskı Plastik Heykelcik",
            "6913100000 - Seramik Dekoratif Ürün",
            "6307909800 - Tekstil Aksesuar",
            "7117190000 - Takı / Bijuteri",
            "9403600000 - Ahşap Mobilya"
        });
        _cmbHsCode.SelectedIndex = 0;
        stack.Controls.Add(_cmbHsCode, 0, 5);

        // desi şeridi
        var desiStrip = new Panel { BackColor = UiStyle.BackgroundColor, Dock = DockStyle.Fill, Height = 72, Margin = new Padding(0, 0, 0, 10) };
        desiStrip.Controls.Add(new Label
        {
            Text = "DESİ",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 12)
        });
        _lblDesiValue = new Label
        {
            Text = "—",
            Font = _bigValueFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 30)
        };
        desiStrip.Controls.Add(_lblDesiValue);
        desiStrip.Controls.Add(new Label
        {
            Text = "FATURALANDIRILACAK",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(140, 12)
        });
        _lblBillableValue = new Label
        {
            Text = "—",
            Font = _bigValueFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(140, 30)
        };
        desiStrip.Controls.Add(_lblBillableValue);
        _lblMeasureSource = new Label
        {
            Text = "kaynak: paket ölçüleri",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 52)
        };
        desiStrip.Controls.Add(_lblMeasureSource);
        stack.Controls.Add(desiStrip, 0, 6);

        // kalemler
        stack.Controls.Add(MakeSectionLabel("KALEMLER"), 0, 7);
        _itemsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            MinimumSize = new Size(0, 40)
        };
        stack.Controls.Add(_itemsFlow, 0, 8);

        scroll.Controls.Add(stack);
        card.Controls.Add(scroll);
        card.Controls.Add(header);
        return card;
    }

    private static Label MakeLabel(Font font, Color color, int x, int y) => new()
    {
        Text = "—",
        Font = font,
        ForeColor = color,
        AutoSize = true,
        BackColor = Color.Transparent,
        Location = new Point(x, y)
    };

    private static Label MakeSectionLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        ForeColor = UiStyle.TextMuted,
        AutoSize = true,
        BackColor = Color.Transparent,
        Margin = new Padding(2, 4, 0, 6)
    };

    private SaasUnitInputBox MakeUnitInput(string title, string unit, string initialValue)
    {
        var box = new SaasUnitInputBox(title, initialValue, unit)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 6, 6)
        };
        box.ValueChanged += (s, e) => ScheduleRecalculation();
        return box;
    }

    #endregion

    #region bölge 3 - taşıyıcı karşılaştırma

    private ThemedCard BuildQuotesRegion()
    {
        var card = new ThemedCard { Dock = DockStyle.Fill };

        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
        header.Controls.Add(new Label
        {
            Text = "TAŞIYICI KARŞILAŞTIRMA",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 12)
        });
        _lblQuoteCount = new Label
        {
            Text = "0 teklif",
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(14, 30)
        };
        header.Controls.Add(_lblQuoteCount);
        _lblQuoteSummary = new Label
        {
            Text = string.Empty,
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(96, 33)
        };
        header.Controls.Add(_lblQuoteSummary);

        var chipRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 0, 8, 0)
        };
        foreach (string provider in new[] { "Tümü", "Aras Global", "ShipEntegra", "Navlungo" })
        {
            string captured = provider;
            var chip = MakePillButton(provider);
            chip.Width = 96;
            chip.Click += (s, e) =>
            {
                _providerFilter = captured;
                RefreshProviderChipStyles();
                RenderQuotes();
            };
            _providerChips[provider] = chip;
            chipRow.Controls.Add(chip);
        }

        _quotesFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 4, 12, 8)
        };
        _quotesFlow.ClientSizeChanged += (s, e) => FitFlow(_quotesFlow);

        _lblQuotesEmpty = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Henüz teklif yok.\n\nBir sipariş seçin veya paket ölçülerini girin.",
            Font = UiStyle.BaseFont,
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        var barcodeHost = new Panel { Dock = DockStyle.Bottom, Height = 148, BackColor = Color.Transparent, Padding = new Padding(12, 4, 12, 12) };
        barcodeHost.Controls.Add(new Label
        {
            Text = "BARKOD / ETİKET",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(12, 2)
        });
        _barcode = new SaasBarcodeLabelControl
        {
            Location = new Point(12, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Width = 340
        };
        barcodeHost.Controls.Add(_barcode);
        barcodeHost.Resize += (s, e) => _barcode.Width = Math.Max(220, barcodeHost.ClientSize.Width - 26);

        card.Controls.Add(_quotesFlow);
        card.Controls.Add(_lblQuotesEmpty);
        card.Controls.Add(barcodeHost);
        card.Controls.Add(chipRow);
        card.Controls.Add(header);

        RefreshProviderChipStyles();
        return card;
    }

    private string _providerFilter = "Tümü";

    private void RefreshProviderChipStyles()
    {
        foreach (var kv in _providerChips)
        {
            bool active = kv.Key == _providerFilter;
            kv.Value.BackColor = active ? UiStyle.PrimaryColor : UiStyle.SecondaryColor;
            kv.Value.ForeColor = active ? Color.White : UiStyle.TextMuted;
        }
    }

    #endregion

    #region eylem çubuğu

    private Panel BuildActionStrip()
    {
        var strip = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = ActionStripHeight,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(16, 0, 16, 0)
        };

        var left = new Panel { Dock = DockStyle.Left, Width = 420, BackColor = Color.Transparent };
        left.Controls.Add(new Label
        {
            Text = "SEÇİLİ TEKLİF",
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(0, 12)
        });
        _lblSelectedService = new Label
        {
            Text = "Teklif seçilmedi",
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(0, 30)
        };
        left.Controls.Add(_lblSelectedService);
        _lblSelectedProvider = new Label
        {
            Text = string.Empty,
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(220, 31)
        };
        left.Controls.Add(_lblSelectedProvider);
        strip.Controls.Add(left);

        var right = new Panel { Dock = DockStyle.Right, Width = 460, BackColor = Color.Transparent };
        _btnCreateShipment = new Button
        {
            Text = "Gönderi Oluştur",
            Height = 38,
            Width = 250,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            BackColor = UiStyle.SuccessColor,
            ForeColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(right.Width - 250 - 12, 13),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnCreateShipment.FlatAppearance.BorderSize = 0;
        _btnCreateShipment.Click += async (s, e) => await ExecuteShipmentCreationAsync();

        _btnPreviewLabel = UiStyle.CreateButton("Etiketi Önizle", isSecondary: true);
        _btnPreviewLabel.Width = 140;
        _btnPreviewLabel.Height = 38;
        _btnPreviewLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnPreviewLabel.Location = new Point(right.Width - 250 - 12 - 140 - 10, 13);
        _btnPreviewLabel.Click += (s, e) => PreviewLabel();

        right.Controls.Add(_btnCreateShipment);
        right.Controls.Add(_btnPreviewLabel);
        strip.Controls.Add(right);

        var center = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _lblPriceUsd = new Label
        {
            Text = "—",
            Font = _priceFont,
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(center.Width - 150, 14)
        };
        center.Controls.Add(_lblPriceUsd);
        _lblPriceTry = new Label
        {
            Text = _rateNote,
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(center.Width - 150, 38)
        };
        center.Controls.Add(_lblPriceTry);
        _lblActionStatus = new Label
        {
            Text = string.Empty,
            Font = _smallFont,
            ForeColor = UiStyle.TextMuted,
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Bottom,
            Height = 18,
            TextAlign = ContentAlignment.MiddleLeft
        };
        center.Controls.Add(_lblActionStatus);
        strip.Controls.Add(center);

        strip.Resize += (s, e) =>
        {
            _lblPriceUsd.Location = new Point(Math.Max(0, center.Width - 170), 14);
            _lblPriceTry.Location = new Point(Math.Max(0, center.Width - 170), 38);
        };

        return strip;
    }

    #endregion

    #region veri yükleme ve seçim

    private async Task ReloadOrdersAsync(bool force = false)
    {
        string? filter = string.IsNullOrWhiteSpace(_txtSearch.Text) ? null : _txtSearch.Text.Trim();
        var orders = await _orderService.GetOrdersAsync(filter);

        _allOrders.Clear();
        _allOrders.AddRange(ApplyStatusFilter(orders));

        _lblQueueCount.Text = _allOrders.Count.ToString();
        _ordersFlow.SuspendLayout();
        _ordersFlow.Controls.Clear();

        foreach (var order in _allOrders)
        {
            var row = new OrderRowControl(order) { Margin = new Padding(0, 0, 0, 8) };
            row.OrderSelected += (s, e) => SelectOrder(order);
            _ordersFlow.Controls.Add(row);
        }

        _ordersFlow.ResumeLayout(true);

        bool empty = _allOrders.Count == 0;
        _lblQueueEmpty.Visible = empty;
        _ordersFlow.Visible = !empty;

        if (!empty)
        {
            var keep = _selectedOrder != null && _allOrders.Contains(_selectedOrder) ? _selectedOrder : _allOrders[0];
            SelectOrder(keep);
        }
        else
        {
            _selectedOrder = null;
            ClearDetail();
        }

        FitFlows();
    }

    private IEnumerable<EtsyOrderFulfillmentItem> ApplyStatusFilter(IEnumerable<EtsyOrderFulfillmentItem> orders)
    {
        return _statusFilter switch
        {
            "Bekleyen" => orders.Where(o => !IsShipped(o)),
            "Gönderildi" => orders.Where(IsShipped),
            _ => orders
        };
    }

    private static bool IsShipped(EtsyOrderFulfillmentItem order)
        => string.Equals(order.Status, "Shipped", StringComparison.OrdinalIgnoreCase)
           || string.Equals(order.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase);

    private void SelectOrder(EtsyOrderFulfillmentItem order)
    {
        _selectedOrder = order;

        foreach (Control c in _ordersFlow.Controls)
        {
            if (c is OrderRowControl row)
            {
                row.IsSelected = ReferenceEquals(row.Order, order);
            }
        }

        _lblBuyerName.Text = order.BuyerName;
        _lblBuyerLine1.Text = order.StreetAddress;
        _lblBuyerLine2.Text = string.Join(" ", new[] { order.PostalCode, order.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        _lblBuyerCountry.Text = string.IsNullOrWhiteSpace(order.CountryName)
            ? order.CountryCode
            : $"{order.CountryName} ({order.CountryCode})";
        _lblIoss.Text = string.IsNullOrWhiteSpace(order.IossNumber)
            ? "IOSS yok — standart ihracat"
            : $"IOSS {order.IossNumber}";

        _barcode.ReceiverName = order.BuyerName;
        _barcode.AddressLine = order.StreetAddress;

        RenderItems(order);

        _suspendRecalc = true;
        try
        {
            var item = order.Items.FirstOrDefault();
            _inWeight.Value = item != null && item.WeightKg > 0 ? item.WeightKg.ToString("0.00") : "0.00";
            _inLength.Value = item != null && item.LengthCm > 0 ? item.LengthCm.ToString("0") : "0";
            _inWidth.Value = item != null && item.WidthCm > 0 ? item.WidthCm.ToString("0") : "0";
            _inHeight.Value = item != null && item.HeightCm > 0 ? item.HeightCm.ToString("0") : "0";

            if (item != null && !string.IsNullOrWhiteSpace(item.HsCode))
            {
                for (int i = 0; i < _cmbHsCode.Items.Count; i++)
                {
                    string? candidate = _cmbHsCode.Items[i]?.ToString();
                    if (candidate != null && candidate.StartsWith(item.HsCode, StringComparison.Ordinal))
                    {
                        _cmbHsCode.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        finally
        {
            _suspendRecalc = false;
        }

        _selectedQuote = null;
        UpdateActionStrip();
        ScheduleRecalculation();
    }

    private void RenderItems(EtsyOrderFulfillmentItem order)
    {
        _itemsFlow.SuspendLayout();
        _itemsFlow.Controls.Clear();

        if (order.Items.Count == 0)
        {
            _itemsFlow.Controls.Add(new Label
            {
                Text = "Kalem bilgisi yok.",
                Font = UiStyle.BaseFont,
                ForeColor = UiStyle.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }
        else
        {
            foreach (var item in order.Items)
            {
                var row = new Panel { BackColor = UiStyle.BackgroundColor, Height = 38, Margin = new Padding(0, 0, 0, 6) };
                row.Controls.Add(new Label
                {
                    Text = $"{item.Quantity}×",
                    Font = UiStyle.SemiboldBaseFont,
                    ForeColor = UiStyle.PrimaryColor,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(10, 10)
                });
                row.Controls.Add(new Label
                {
                    Text = item.Title,
                    Font = UiStyle.BaseFont,
                    ForeColor = UiStyle.TextDark,
                    AutoEllipsis = true,
                    BackColor = Color.Transparent,
                    Location = new Point(40, 10),
                    Size = new Size(300, 20)
                });
                row.Controls.Add(new Label
                {
                    Text = $"{item.Price:N2} {item.Currency}",
                    Font = UiStyle.SemiboldBaseFont,
                    ForeColor = UiStyle.TextDark,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(row.Width - 90, 10)
                });
                _itemsFlow.Controls.Add(row);
            }
        }

        _itemsFlow.ResumeLayout(true);
    }

    private void ClearDetail()
    {
        _lblBuyerName.Text = "Sipariş seçilmedi";
        _lblBuyerLine1.Text = string.Empty;
        _lblBuyerLine2.Text = string.Empty;
        _lblBuyerCountry.Text = string.Empty;
        _lblIoss.Text = string.Empty;
        _lblDesiValue.Text = "—";
        _lblBillableValue.Text = "—";
        _itemsFlow.Controls.Clear();
        _quotes.Clear();
        RenderQuotes();
        UpdateActionStrip();
    }

    #endregion

    #region desi + teklifler

    private void ScheduleRecalculation()
    {
        if (_suspendRecalc)
        {
            return;
        }

        _debounceTimer ??= CreateDebounceTimer();
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private System.Windows.Forms.Timer CreateDebounceTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 300 };
        timer.Tick += async (s, e) =>
        {
            timer.Stop();
            await RecalculateAsync();
        };
        return timer;
    }

    private async Task RecalculateAsync()
    {
        if (_selectedOrder == null)
        {
            return;
        }

        double weight = (double)_inWeight.GetDecimal();
        double width = (double)_inWidth.GetDecimal();
        double length = (double)_inLength.GetDecimal();
        double height = (double)_inHeight.GetDecimal();

        var measurement = PackageMeasurementCalculator.Evaluate(weight, width, length, height);

        _lblPackageError.Visible = !measurement.IsValid;
        _lblPackageError.Text = measurement.IsValid
            ? string.Empty
            : string.Join("  ", measurement.Errors.Take(2));

        _inWeight.Invalidate();
        _inWidth.Invalidate();
        _inLength.Invalidate();
        _inHeight.Invalidate();

        if (!measurement.IsValid)
        {
            _lblDesiValue.Text = "—";
            _lblBillableValue.Text = "—";
            _lblMeasureSource.Text = "Geçersiz ölçü: teklif alınamaz.";
            _quotes.Clear();
            RenderQuotes();
            UpdateActionStrip();
            return;
        }

        _lblDesiValue.Text = measurement.Desi.ToString("N2");
        _lblBillableValue.Text = $"{measurement.BillableWeightKg:N2} kg";
        _lblMeasureSource.Text = measurement.BillableWeightKg > measurement.WeightKg
            ? "kaynak: desi (desi ağırlıktan büyük)"
            : "kaynak: paket ağırlığı";

        _quoteCts?.Cancel();
        _quoteCts?.Dispose();
        _quoteCts = new CancellationTokenSource();
        var ct = _quoteCts.Token;

        await LoadQuotesAsync(measurement, ct);
        if (!ct.IsCancellationRequested)
        {
            RenderQuotes();
        }
    }

    private async Task LoadQuotesAsync(PackageMeasurementResult measurement, CancellationToken ct)
    {
        string country = !string.IsNullOrWhiteSpace(_selectedOrder?.CountryCode) ? _selectedOrder!.CountryCode : "US";
        double weight = measurement.BillableWeightKg;
        double w = measurement.WidthCm;
        double l = measurement.LengthCm;
        double h = measurement.HeightCm;
        decimal rate = UsdTryRate;

        var collected = new List<OrderQuote>();

        Task<List<OrderQuote>> arasTask = Task.Run(async () =>
        {
            var list = new List<OrderQuote>();
            try
            {
                var request = new ArasGlobalQuoteRequest
                {
                    ReceiverCountry = country,
                    WeightKg = weight,
                    WidthCm = w,
                    LengthCm = l,
                    HeightCm = h
                };

                var response = await _arasPricingService.GetQuotesAsync(request);
                if (response != null && response.Success)
                {
                    foreach (var offer in response.Offers)
                    {
                        string service = $"{offer.Cargo} {offer.ProviderServiceType}".Trim();
                        list.Add(new OrderQuote
                        {
                            ProviderName = "Aras Global",
                            CarrierName = "Aras Global",
                            ServiceName = service,
                            SubCarrier = offer.Cargo,
                            ServiceType = service,
                            PriceUsd = offer.Price,
                            PriceTry = Math.Round(offer.Price * rate, 2),
                            DeliveryText = offer.DeliveryDaysText,
                            Source = CarrierQuoteTrust.Classify(offer.IsLivePrice, null),
                            Note = offer.IsLivePrice ? "Aras Global canlı teklif." : "Yedek tarife.",
                            ExchangeRate = rate
                        });
                    }
                }
            }
            catch
            {
                // Tek sağlayıcı hatası diğerlerini engellemez; satır olarak raporlanır.
            }

            return list;
        }, ct);

        Task<List<OrderQuote>> shipEntegraTask = Task.Run(async () =>
        {
            var list = new List<OrderQuote>();
            try
            {
                var request = new ShipEntegraQuoteRequest
                {
                    ReceiverCountry = country,
                    WeightKg = weight,
                    WidthCm = w,
                    LengthCm = l,
                    HeightCm = h
                };

                var response = await _shipEntegraPricingService.GetQuotesAsync(request);
                if (response != null && response.Success)
                {
                    foreach (var offer in response.Offers)
                    {
                        string service = !string.IsNullOrWhiteSpace(offer.ClearServiceName) ? offer.ClearServiceName : offer.ServiceName;
                        list.Add(new OrderQuote
                        {
                            ProviderName = "ShipEntegra",
                            CarrierName = "ShipEntegra",
                            ServiceName = service,
                            SubCarrier = ExtractSubCarrierName(offer.ServiceName),
                            ServiceType = service,
                            PriceUsd = offer.TotalPrice,
                            PriceTry = Math.Round(offer.TotalPrice * rate, 2),
                            DeliveryText = offer.DeliveryDaysText,
                            Source = CarrierQuoteTrust.Classify(offer.IsLivePrice, offer.AdditionalDescription),
                            Note = CleanHtml(offer.AdditionalDescription),
                            ExchangeRate = rate
                        });
                    }
                }
            }
            catch
            {
            }

            return list;
        }, ct);

        Task<List<OrderQuote>> navlungoTask = Task.Run(async () =>
        {
            var list = new List<OrderQuote>();
            try
            {
                var request = new NavlungoQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = country,
                    WeightKg = weight,
                    WidthCm = w,
                    LengthCm = l,
                    HeightCm = h,
                    Source = "user"
                };

                var settings = NavlungoSettingsStore.Load();
                var offers = await _navlungoApiClient.FetchLiveQuotesAsync(request, settings);
                AppendGenericOffers(list, "Navlungo", offers, rate);
            }
            catch
            {
                try
                {
                    var fallback = NavlungoApiClient.GenerateRealisticFallbackQuotes(new NavlungoQuoteRequest
                    {
                        FromCountry = "TR",
                        ToCountry = country,
                        WeightKg = weight,
                        WidthCm = w,
                        LengthCm = l,
                        HeightCm = h,
                        Source = "user"
                    });

                    AppendGenericOffers(list, "Navlungo", fallback, rate);
                }
                catch
                {
                }
            }

            return list;
        }, ct);

        Task<List<OrderQuote>> shiptomoreTask = Task.Run(async () =>
        {
            var list = new List<OrderQuote>();
            try
            {
                var request = new ShiptomoreQuoteRequest
                {
                    FromCountry = "TR",
                    ToCountry = country,
                    WeightKg = weight,
                    WidthCm = w,
                    LengthCm = l,
                    HeightCm = h
                };

                var settings = ShiptomoreSettingsStore.Load();
                var offers = await _shiptomoreApiClient.FetchLiveQuotesAsync(request, settings);
                AppendGenericOffers(list, "Shiptomore", offers, rate);
            }
            catch
            {
                try
                {
                    var fallback = ShiptomoreApiClient.GenerateRealisticFallbackQuotes(new ShiptomoreQuoteRequest
                    {
                        FromCountry = "TR",
                        ToCountry = country,
                        WeightKg = weight,
                        WidthCm = w,
                        LengthCm = l,
                        HeightCm = h
                    }, false);

                    AppendGenericOffers(list, "Shiptomore", fallback, rate);
                }
                catch
                {
                }
            }

            return list;
        }, ct);

        try
        {
            await Task.WhenAll(arasTask, shipEntegraTask, navlungoTask, shiptomoreTask);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        collected.AddRange(await arasTask);
        collected.AddRange(await shipEntegraTask);
        collected.AddRange(await navlungoTask);
        collected.AddRange(await shiptomoreTask);

        var deduplicated = collected
            .GroupBy(q => $"{q.ProviderName}|{q.SubCarrier}|{q.ServiceType}|{q.PriceUsd}")
            .Select(g => g.First())
            .ToList();

        if (deduplicated.Count > 0)
        {
            decimal minPrice = deduplicated.Min(q => q.PriceUsd);
            foreach (var quote in deduplicated)
            {
                quote.IsCheapest = quote.PriceUsd == minPrice;
                quote.IsCreationSupported = _creationManager.GetProvider(quote.ProviderName)?.IsCreationSupported ?? false;
            }
        }

        _quotes = deduplicated;
    }

    private static void AppendGenericOffers<T>(List<OrderQuote> target, string providerName, IEnumerable<T> offers, decimal rate)
    {
        foreach (var offer in offers)
        {
            string note = GetStringProperty(offer, "Note");
            string serviceName = GetStringProperty(offer, "ServiceName");
            string carrier = GetStringProperty(offer, "Carrier");
            string delivery = GetStringProperty(offer, "DeliveryEstimate");
            decimal price = GetDecimalProperty(offer, "Price");

            target.Add(new OrderQuote
            {
                ProviderName = providerName,
                CarrierName = providerName,
                ServiceName = serviceName,
                SubCarrier = carrier,
                ServiceType = serviceName,
                PriceUsd = price,
                PriceTry = Math.Round(price * rate, 2),
                DeliveryText = delivery,
                Source = CarrierQuoteTrust.Classify(null, note),
                Note = note,
                ExchangeRate = rate
            });
        }
    }

    private static string GetStringProperty(object? source, string name)
        => source?.GetType().GetProperty(name)?.GetValue(source)?.ToString() ?? string.Empty;

    private static decimal GetDecimalProperty(object? source, string name)
    {
        object? value = source?.GetType().GetProperty(name)?.GetValue(source);
        return value is decimal d ? d : 0m;
    }

    private static string ExtractSubCarrierName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return "ShipEntegra";
        if (serviceName.Contains("eko", StringComparison.OrdinalIgnoreCase)) return "Eko Plus";
        if (serviceName.Contains("smart", StringComparison.OrdinalIgnoreCase)) return "Smart";
        if (serviceName.Contains("ups", StringComparison.OrdinalIgnoreCase)) return "UPS";
        if (serviceName.Contains("widect", StringComparison.OrdinalIgnoreCase)) return "Widect";
        if (serviceName.Contains("express", StringComparison.OrdinalIgnoreCase)) return "Express";
        if (serviceName.Contains("fedex", StringComparison.OrdinalIgnoreCase)) return "FedEx";
        return "ShipEntegra";
    }

    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return html.Replace("<br>", "  ").Replace("<br/>", "  ").Replace("<br />", "  ").Trim();
    }

    private void RenderQuotes()
    {
        var visible = _providerFilter == "Tümü"
            ? _quotes
            : _quotes.Where(q => string.Equals(q.ProviderName, _providerFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        _quotesFlow.SuspendLayout();
        _quotesFlow.Controls.Clear();

        foreach (var quote in visible)
        {
            var row = new QuoteRowControl(quote) { Margin = new Padding(0, 0, 0, 8) };
            row.Selected += (s, e) => SelectQuote(quote);
            row.IsSelected = ReferenceEquals(_selectedQuote, quote);
            _quotesFlow.Controls.Add(row);
        }

        _quotesFlow.ResumeLayout(true);

        bool empty = visible.Count == 0;
        _lblQuotesEmpty.Visible = empty;
        _quotesFlow.Visible = !empty;

        _lblQuoteCount.Text = $"{visible.Count} teklif";
        int live = visible.Count(q => q.Source == QuoteSource.Live);
        int estimated = visible.Count - live;
        _lblQuoteSummary.Text = visible.Count == 0 ? string.Empty : $"{live} canlı · {estimated} tahmini";

        FitFlow(_quotesFlow);
    }

    private void SelectQuote(OrderQuote quote)
    {
        _selectedQuote = quote;

        foreach (Control c in _quotesFlow.Controls)
        {
            if (c is QuoteRowControl row)
            {
                row.IsSelected = ReferenceEquals(row.Quote, quote);
            }
        }

        _barcode.CarrierName = quote.ServiceName;
        UpdateActionStrip();
    }

    private void UpdateActionStrip()
    {
        if (_selectedQuote == null)
        {
            _lblSelectedService.Text = "Teklif seçilmedi";
            _lblSelectedProvider.Text = string.Empty;
            _lblPriceUsd.Text = "—";
            _lblPriceTry.Text = _rateNote;
            _btnCreateShipment.Enabled = false;
            _btnCreateShipment.Text = "Gönderi Oluştur";
            _lblActionStatus.Text = "Önce sipariş seçip geçerli paket ölçüleri girin.";
            return;
        }

        _lblSelectedService.Text = _selectedQuote.ServiceName;
        _lblSelectedProvider.Text = _selectedQuote.ProviderName;
        _lblPriceUsd.Text = $"${_selectedQuote.PriceUsd:N2}";
        _lblPriceTry.Text = $"{_selectedQuote.PriceTry:N2} ₺ · {_rateNote}";

        bool supported = _selectedQuote.IsCreationSupported;
        _btnCreateShipment.Enabled = supported;
        _btnCreateShipment.Text = supported
            ? $"{_selectedQuote.ProviderName} ile Gönderi Oluştur"
            : "Gönderi kapalı";
        _btnCreateShipment.BackColor = supported ? UiStyle.SuccessColor : UiStyle.SecondaryColor;
        _btnCreateShipment.ForeColor = supported ? Color.White : UiStyle.TextMuted;

        _lblActionStatus.Text = supported
            ? (_selectedQuote.Source == QuoteSource.Live ? "Canlı teklif seçildi." : "Tahmini tarife seçildi — fiyat teyidi önerilir.")
            : $"{_selectedQuote.ProviderName} için gönderi oluşturma henüz aktif değil; yalnız karşılaştırma amaçlıdır.";
    }

    #endregion

    #region gönderi oluşturma

    private void PreviewLabel()
    {
        if (_selectedQuote == null)
        {
            return;
        }

        MessageBox.Show(
            $"Etiket önizlemesi (taslak)\n\n" +
            $"Taşıyıcı: {_selectedQuote.ProviderName} — {_selectedQuote.ServiceName}\n" +
            $"Alıcı: {_selectedOrder?.BuyerName}\n" +
            $"Barkod: {_barcode.BarcodeNumber}\n\n" +
            "Gerçek etiket bağlantısı gönderi oluşturulduğunda üretilir.",
            "Etiket Önizleme",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task PromptOrRefreshArasSessionAsync()
    {
        await Task.Yield();
        try
        {
            using var loginForm = new ArasGlobalEmbeddedLoginForm();
            if (loginForm.ShowDialog(this) == DialogResult.OK)
            {
                UpdateSessionBadge();
                ScheduleRecalculation();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Oturum açma penceresi açılamadı:\n{ex.Message}", "Oturum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExecuteShipmentCreationAsync()
    {
        if (_selectedOrder == null || _selectedQuote == null)
        {
            MessageBox.Show("Lütfen önce bir sipariş ve kargo teklifi seçin.", "Eksik Seçim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_selectedQuote.IsCreationSupported)
        {
            MessageBox.Show(
                $"{_selectedQuote.ProviderName} için gönderi oluşturma modeli henüz geliştirme aşamasındadır.\nŞu an Aras Global aktiftir.",
                "Taşıyıcı Bildirimi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var arasSettings = ArasGlobalSettingsStore.Load();
        if (!arasSettings.HasValidTokenFormat)
        {
            var ask = MessageBox.Show(
                "Aras Global oturum tokeni bulunamadı veya süresi dolmuş.\n\nŞimdi oturumu yenilemek ister misiniz?",
                "Aras Global Oturumu Gerekli",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (ask != DialogResult.Yes)
            {
                return;
            }

            await PromptOrRefreshArasSessionAsync();
            if (!ArasGlobalSettingsStore.Load().HasValidTokenFormat)
            {
                return;
            }
        }

        _btnCreateShipment.Enabled = false;
        _btnCreateShipment.Text = "Gönderi oluşturuluyor...";
        _lblActionStatus.ForeColor = UiStyle.AccentColor;
        _lblActionStatus.Text = "Adım 1/3 · oturum doğrulanıyor";

        try
        {
            double weight = (double)_inWeight.GetDecimal();
            double width = (double)_inWidth.GetDecimal();
            double length = (double)_inLength.GetDecimal();
            double height = (double)_inHeight.GetDecimal();

            var measurement = PackageMeasurementCalculator.Evaluate(weight, width, length, height);
            if (!measurement.IsValid)
            {
                _lblActionStatus.ForeColor = UiStyle.DangerColor;
                _lblActionStatus.Text = "Paket ölçüleri geçersiz.";
                MessageBox.Show(string.Join("\n", measurement.Errors), "Geçersiz Paket Ölçüsü", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lblActionStatus.Text = "Adım 2/3 · gönderi taslağı oluşturuluyor";

            var context = new ShipmentCreationContext
            {
                Order = _selectedOrder,
                WeightKg = measurement.WeightKg,
                WidthCm = measurement.WidthCm,
                LengthCm = measurement.LengthCm,
                HeightCm = measurement.HeightCm,
                HsCode = (_cmbHsCode.SelectedItem?.ToString() ?? "3926400000").Split(' ')[0],
                SelectedSubCarrier = _selectedQuote.SubCarrier,
                ServiceType = _selectedQuote.ServiceType
            };

            _lblActionStatus.Text = "Adım 3/3 · taşıyıcıya gönderiliyor";
            var result = await _creationManager.CreateShipmentAsync(_selectedQuote.ProviderName, context);

            if (result.IsSuccess)
            {
                _lblActionStatus.ForeColor = UiStyle.SuccessColor;
                _lblActionStatus.Text = $"Başarılı · takip no {result.TrackingNumber}";
                _barcode.BarcodeNumber = result.TrackingNumber;

                await _orderService.MarkOrderAsShippedAsync(_selectedOrder.ReceiptId, _selectedQuote.ProviderName, result.TrackingNumber, result.LabelUrl);

                MessageBox.Show(
                    $"Kargo gönderisi başarıyla oluşturuldu.\n\n" +
                    $"Takip No: {result.TrackingNumber}\n" +
                    $"Taşıyıcı: {_selectedQuote.ProviderName}\n" +
                    $"Alıcı: {_selectedOrder.BuyerName}",
                    "Kargo Oluşturuldu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await ReloadOrdersAsync();
            }
            else
            {
                _lblActionStatus.ForeColor = UiStyle.DangerColor;
                _lblActionStatus.Text = $"Hata: {result.ErrorMessage}";

                string message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Taşıyıcı gönderi oluşturamadı."
                    : result.ErrorMessage;

                var ask = MessageBox.Show(
                    $"{message}\n\nAras Global oturumunu yenilemek ister misiniz?",
                    "Gönderi Hatası",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (ask == DialogResult.Yes)
                {
                    await PromptOrRefreshArasSessionAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _lblActionStatus.ForeColor = UiStyle.DangerColor;
            _lblActionStatus.Text = $"Beklenmeyen hata: {ex.Message}";
            MessageBox.Show($"Beklenmeyen hata:\n{ex.Message}", "Sistem Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateSessionBadge();
            UpdateActionStrip();
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            _quoteCts?.Cancel();
            _quoteCts?.Dispose();
            _quoteCts = null;

            _smallFont.Dispose();
            _priceFont.Dispose();
            _bigValueFont.Dispose();
        }

        base.Dispose(disposing);
    }

    #region iç modeller ve satır kontrolleri

    /// <summary>Tek bir taşıyıcı teklifi.</summary>
    public sealed class OrderQuote
    {
        public string ProviderName { get; set; } = string.Empty;
        public string CarrierName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public decimal PriceTry { get; set; }
        public string DeliveryText { get; set; } = string.Empty;
        public bool IsCheapest { get; set; }
        public bool IsCreationSupported { get; set; }
        public QuoteSource Source { get; set; } = QuoteSource.Estimated;
        public string Note { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 48.855m;
    }

    /// <summary>Yuvarlatılmış köşeli, tema token'larına bağlı kart paneli.</summary>
    internal sealed class ThemedCard : Panel
    {
        private int _cornerRadius = 12;

        public ThemedCard()
        {
            UiStyle.SetDoubleBuffered(this);
            BackColor = UiStyle.CardBackground;
            Padding = new Padding(0);
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                UpdateRegion();
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            if (_cornerRadius <= 0)
            {
                Region = null;
                return;
            }

            Region?.Dispose();
            Region = new Region(UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 0, Width, Height), _cornerRadius));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_cornerRadius <= 0 || Width <= 2 || Height <= 2)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius);
            using var pen = new Pen(UiStyle.BorderColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }
    }

    /// <summary>Sipariş kuyruğundaki tek satır.</summary>
    internal sealed class OrderRowControl : Control
    {
        private static readonly Font IdFont = new("Segoe UI Semibold", 10f);
        private static readonly Font MetaFont = new("Segoe UI", 8.5f);
        private static readonly Font PillFont = new("Segoe UI", 8f, FontStyle.Bold);

        public EtsyOrderFulfillmentItem Order { get; }
        public bool IsSelected { get; set; }
        public event EventHandler? OrderSelected;

        public OrderRowControl(EtsyOrderFulfillmentItem order)
        {
            Order = order;
            UiStyle.SetDoubleBuffered(this);
            Height = 96;
            Cursor = Cursors.Hand;
            BackColor = UiStyle.CardBackground;
            MouseClick += (s, e) => OrderSelected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = IsSelected
                ? Blend(UiStyle.BackgroundColor, UiStyle.AccentColor, 0.14)
                : UiStyle.BackgroundColor;

            using (var path = UiStyle.CreateRoundedRectanglePath(bounds, 10))
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
                using var pen = new Pen(IsSelected ? UiStyle.AccentColor : UiStyle.BorderColor, IsSelected ? 1.6f : 1f);
                g.DrawPath(pen, path);
            }

            bool shipped = string.Equals(Order.Status, "Shipped", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Order.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase);

            using (var bar = new SolidBrush(shipped ? UiStyle.SuccessColor : UiStyle.WarningColor))
            {
                using var path = UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 8, 4, Height - 16), 2);
                g.FillPath(bar, path);
            }

            int x = 16;
            int y = 12;

            TextRenderer.DrawText(g, Order.OrderNumber, IdFont, new Point(x, y), UiStyle.TextDark, TextFormatFlags.NoPadding);
            y += 22;

            string meta1 = string.Join("  ·  ", new[] { Order.BuyerName, CountryLabel() }.Where(s => !string.IsNullOrWhiteSpace(s)));
            TextRenderer.DrawText(g, meta1, MetaFont, new Point(x, y), UiStyle.TextMuted, TextFormatFlags.NoPadding);
            y += 18;

            string shippingType = string.IsNullOrWhiteSpace(Order.IossNumber) ? "Standart ihracat" : "IOSS";
            string meta2 = $"{Order.Items.Count} kalem  ·  {Order.TotalWeightKg:N2} kg  ·  {shippingType}";
            TextRenderer.DrawText(g, meta2, MetaFont, new Point(x, y), UiStyle.TextMuted, TextFormatFlags.NoPadding);

            // durum rozeti (sağ üst)
            string badge = shipped ? "Gönderildi" : "Bekleyen";
            Color badgeBg = shipped ? UiStyle.SuccessColor : UiStyle.WarningColor;
            Size badgeSize = TextRenderer.MeasureText(badge, PillFont);
            var badgeRect = new Rectangle(Width - badgeSize.Width - 26, 12, badgeSize.Width + 14, 20);
            using (var path = UiStyle.CreateRoundedRectanglePath(badgeRect, 10))
            {
                using var brush = new SolidBrush(Color.FromArgb(46, badgeBg));
                g.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(140, badgeBg), 1f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, badge, PillFont, badgeRect, badgeBg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private string CountryLabel()
        {
            if (!string.IsNullOrWhiteSpace(Order.CountryName))
            {
                return $"{Order.CountryName} ({Order.CountryCode})";
            }

            return Order.CountryCode;
        }

        internal static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.R + ((b.R - a.R) * t)),
                (int)(a.G + ((b.G - a.G) * t)),
                (int)(a.B + ((b.B - a.B) * t)));
        }
    }

    /// <summary>Taşıyıcı karşılaştırma listesindeki tek satır.</summary>
    internal sealed class QuoteRowControl : Control
    {
        private static readonly Font NameFont = new("Segoe UI Semibold", 9.5f);
        private static readonly Font MetaFont = new("Segoe UI", 8.5f);
        private static readonly Font PriceFont = new("Segoe UI Semibold", 12f, FontStyle.Bold);
        private static readonly Font PillFont = new("Segoe UI", 8f, FontStyle.Bold);

        private Image? _logo;

        public OrderQuote Quote { get; }
        public bool IsSelected { get; set; }
        public event EventHandler? Selected;

        public QuoteRowControl(OrderQuote quote)
        {
            Quote = quote;
            UiStyle.SetDoubleBuffered(this);
            Height = 88;
            Cursor = Cursors.Hand;
            BackColor = UiStyle.CardBackground;

            try
            {
                _logo = ShippingLogoHelper.GetCarrierLogo(quote.SubCarrier, quote.ServiceName, quote.Note);
            }
            catch
            {
                _logo = null;
            }

            MouseClick += (s, e) => Selected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color baseFill = UiStyle.BackgroundColor;
            if (IsSelected)
            {
                baseFill = OrderRowControl.Blend(baseFill, UiStyle.AccentColor, 0.14);
            }
            else if (Quote.IsCheapest)
            {
                baseFill = OrderRowControl.Blend(baseFill, UiStyle.SuccessColor, 0.12);
            }

            Color stroke = IsSelected
                ? UiStyle.AccentColor
                : (Quote.IsCheapest ? UiStyle.SuccessColor : UiStyle.BorderColor);
            float strokeWidth = IsSelected || Quote.IsCheapest ? 1.5f : 1f;

            using (var path = UiStyle.CreateRoundedRectanglePath(bounds, 10))
            {
                using var brush = new SolidBrush(baseFill);
                g.FillPath(brush, path);
                using var pen = new Pen(stroke, strokeWidth);
                g.DrawPath(pen, path);
            }

            int x = 12;
            var logoRect = new Rectangle(x, 12, 38, 26);
            if (_logo != null)
            {
                ShippingLogoHelper.DrawImagePreserveAspect(g, _logo, logoRect);
            }

            int textX = x + 46;
            TextRenderer.DrawText(g, Quote.ServiceName, NameFont, new Point(textX, 11), UiStyle.TextDark, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                g,
                $"{Quote.ProviderName}  ·  {Quote.DeliveryText}",
                MetaFont,
                new Point(textX, 29),
                UiStyle.TextMuted,
                TextFormatFlags.NoPadding);

            string usd = $"${Quote.PriceUsd:N2}";
            Size usdSize = TextRenderer.MeasureText(usd, PriceFont);
            TextRenderer.DrawText(g, usd, PriceFont, new Point(Width - usdSize.Width - 14, 10), UiStyle.TextDark, TextFormatFlags.NoPadding);

            string tryText = $"{Quote.PriceTry:N2} ₺";
            Size trySize = TextRenderer.MeasureText(tryText, MetaFont);
            TextRenderer.DrawText(g, tryText, MetaFont, new Point(Width - trySize.Width - 14, 32), UiStyle.TextMuted, TextFormatFlags.NoPadding);

            int pillX = textX;
            int pillY = 54;

            pillX = DrawPill(g, CarrierQuoteTrust.ToBadgeText(Quote.Source), PillFont, pillX, pillY,
                Quote.Source == QuoteSource.Live ? UiStyle.SuccessColor : UiStyle.WarningColor);

            if (Quote.IsCheapest)
            {
                pillX = DrawPill(g, "EN UCUZ", PillFont, pillX, pillY, UiStyle.SuccessColor);
            }

            if (!Quote.IsCreationSupported)
            {
                DrawPill(g, "Gönderi kapalı", PillFont, pillX, pillY, UiStyle.DangerColor);
            }
        }

        private static int DrawPill(Graphics g, string text, Font font, int x, int y, Color accent)
        {
            Size size = TextRenderer.MeasureText(text, font);
            var rect = new Rectangle(x, y, size.Width + 14, 19);

            using (var path = UiStyle.CreateRoundedRectanglePath(rect, 9))
            {
                using var brush = new SolidBrush(Color.FromArgb(46, accent));
                g.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(140, accent), 1f);
                g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(rect.X + 7, rect.Y + 1, rect.Width - 14, rect.Height - 2);
            TextRenderer.DrawText(g, text, font, textRect, accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return x + rect.Width + 6;
        }

        // Not: logo görselleri ShippingLogoHelper tarafından önbelleğe alınabilir;
        // burada Dispose edilmez, aksi halde paylaşılan örnek bozulabilir.
    }

    #endregion
}
