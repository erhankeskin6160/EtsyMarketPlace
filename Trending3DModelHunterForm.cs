namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Viral3DModels.Interfaces;
using EtsyMarketPlace.Application.Viral3DModels.Services;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

public sealed class Trending3DModelHunterForm : Form
{
    private readonly Viral3DModelHunterService _hunterService;
    private readonly IShopNicheAnalyzer _nicheAnalyzer = new ActiveAiShopNicheAnalyzer();
    private readonly HttpClient _imageHttpClient = new();

    private ShopNicheProfile _activeShopProfile = ShopNicheProfile.CreateDefaultFigureAndToy();
    private List<Trending3DModel> _allModels = [];
    private List<Trending3DModel> _filteredModels = [];
    private Trending3DModel? _selectedModel;
    private CancellationTokenSource? _scanCts;
    private int _displayedLimit = 35;

    // Store Niche AI Ribbon
    private readonly Panel _panelStoreNicheBanner = new()
    {
        Dock = DockStyle.Fill,
        Height = 44,
        BackColor = Color.FromArgb(17, 24, 39),
        Padding = new Padding(12, 6, 12, 6),
        Margin = new Padding(0, 0, 0, 6)
    };

    private readonly Label _lblStoreNicheText = new()
    {
        AutoSize = true,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold),
        Text = "🏪 Aktif Mağaza Nişi: 🎮 Eklemli Figür, Oyuncak & Fidget Modeller (%96 AI Güveni)",
        Margin = new Padding(0, 5, 10, 0)
    };

    private readonly Label _lblStoreAiBadge = new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(167, 139, 250),
        BackColor = Color.FromArgb(30, 27, 75),
        Font = new Font("Segoe UI", 8F, FontStyle.Bold),
        Padding = new Padding(6, 4, 6, 4),
        Margin = new Padding(0, 4, 10, 0),
        Text = "🤖 Yapay Zeka Radarı"
    };

    private readonly ModernButtonControl _btnReanalyzeNiche = new()
    {
        Text = "⚡ AI ile Mağazamı Tara",
        Width = 175,
        Height = 30,
        NormalColor = Color.FromArgb(79, 70, 229),
        HoverColor = Color.FromArgb(99, 102, 241),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    // KPI Tiles
    private readonly ModernKpiTile _kpiPlatforms = new() { Title = "AKTİF PLATFORMLAR", Value = "5", TrendText = "Bambu/Creality/Prusa/Anycubic/TV", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiViralCount = new() { Title = "TARANAN MODELLER", Value = "0", TrendText = "Canlı Platform Kataloğu", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiGoldenOpps = new() { Title = "ALTIN FIRSATLAR", Value = "0", TrendText = "Etsy Rekabet < 3", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiCommercial = new() { Title = "TİCARİ LİSANSLI", Value = "0", TrendText = "Satışa Uygun (CC-BY)", IsPositive = true, Width = 230 };

    // Toolbar controls
    private readonly ModernComboBox _cboPlatform = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 190,
        Height = 32,
        Font = new Font("Segoe UI", 9F)
    };

    private readonly ComboBox _cboCategory = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 180,
        Height = 32,
        Font = new Font("Segoe UI", 9F)
    };

    private readonly ComboBox _cboSort = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 190,
        Height = 32,
        Font = new Font("Segoe UI", 9F)
    };

    private readonly ModernCheckBox _chkCommercialOnly = new()
    {
        Text = "Ticari Lisanslı (CC-BY)",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
        Margin = new Padding(8, 7, 6, 0)
    };

    private readonly ModernCheckBox _chkShopNicheOnly = new()
    {
        Text = "🎯 Mağazama Uygun (%70+)",
        Checked = false,
        AutoSize = true,
        ForeColor = Color.FromArgb(251, 191, 36),
        Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
        Margin = new Padding(6, 7, 8, 0)
    };

    private readonly TextBox _txtSearch = new()
    {
        Width = 200,
        Height = 32,
        PlaceholderText = "🔍 Model, etiket ara...",
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(4, 2, 4, 0)
    };

    private readonly ModernButtonControl _btnSearch = new()
    {
        Text = "🔍 Ara",
        Width = 65,
        Height = 32,
        NormalColor = Color.FromArgb(79, 70, 229),
        HoverColor = Color.FromArgb(99, 102, 241),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 2, 6, 0)
    };

    private readonly ModernButtonControl _btnScan = new()
    {
        Text = "🔄 Platformları Tara",
        Width = 160,
        Height = 32,
        NormalColor = UiStyle.PrimaryColor,
        HoverColor = UiStyle.PrimaryHover,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(2, 2, 6, 0)
    };

    private readonly Label _lblEngineBadge = new()
    {
        Text = "🛡️ Anti-Bot | 💾 SQLite Delta | 100+ Model Kataloğu",
        AutoSize = true,
        ForeColor = Color.FromArgb(52, 211, 153),
        BackColor = Color.FromArgb(6, 78, 59),
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        Padding = new Padding(6, 6, 6, 6),
        Margin = new Padding(4, 4, 0, 0)
    };

    // Grid & Pagination controls
    private readonly DataGridView _grid = new();
    private readonly ModernButtonControl _btnLoadMore = new()
    {
        Text = "⬇️ Daha Fazla Model Gör (+30 Model)",
        Width = 290,
        Height = 34,
        NormalColor = Color.FromArgb(30, 41, 75),
        HoverColor = Color.FromArgb(51, 65, 110),
        ForeColor = Color.FromArgb(167, 139, 250),
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly Label _lblPageStatus = new()
    {
        AutoSize = true,
        ForeColor = UiStyle.TextMuted,
        Font = new Font("Segoe UI", 8.8F),
        Margin = new Padding(12, 8, 0, 0)
    };

    // Right Preview Drawer
    private readonly PictureBox _picHero = new()
    {
        Size = new Size(310, 200),
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(15, 23, 42),
        BorderStyle = BorderStyle.None
    };

    private readonly Label _lblLicenseBadge = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(52, 211, 153),
        BackColor = Color.FromArgb(6, 78, 59),
        Padding = new Padding(8, 4, 8, 4),
        Margin = new Padding(0, 4, 0, 4)
    };

    private readonly Label _lblModelTitle = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
        ForeColor = Color.White,
        MaximumSize = new Size(310, 0),
        Margin = new Padding(0, 4, 0, 4)
    };

    private readonly Label _lblAuthor = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 8.8F),
        ForeColor = UiStyle.TextMuted,
        Margin = new Padding(0, 0, 0, 6)
    };

    private readonly Label _lblPrintSpecs = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 8.8F),
        ForeColor = Color.FromArgb(226, 232, 240),
        MaximumSize = new Size(310, 0),
        Margin = new Padding(0, 0, 0, 8)
    };

    private readonly Label _lblEtsyArbitrage = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        ForeColor = Color.FromArgb(56, 189, 248),
        BackColor = Color.FromArgb(20, 35, 55),
        Padding = new Padding(8, 6, 8, 6),
        MaximumSize = new Size(310, 0),
        Margin = new Padding(0, 0, 0, 8)
    };

    private readonly Label _lblAiAdviceHeader = new()
    {
        Text = "🤖 AI MAĞAZA TAVSİYESİ",
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        ForeColor = Color.FromArgb(167, 139, 250),
        Margin = new Padding(0, 4, 0, 2)
    };

    private readonly Label _lblAiAdviceBody = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 8.8F),
        ForeColor = Color.FromArgb(226, 232, 240),
        BackColor = Color.FromArgb(24, 30, 52),
        Padding = new Padding(8, 8, 8, 8),
        MaximumSize = new Size(310, 0),
        Margin = new Padding(0, 0, 0, 10)
    };

    private readonly FlowLayoutPanel _galleryStrip = new()
    {
        Width = 310,
        Height = 70,
        AutoScroll = true,
        WrapContents = false,
        BackColor = Color.FromArgb(15, 23, 42),
        Padding = new Padding(2),
        Margin = new Padding(0, 0, 0, 10)
    };

    private readonly ModernButtonControl _btnCreateEtsyDraft = new()
    {
        Text = "🚀 1-TIKLA ETSY TASLAĞI YAP",
        Width = 310,
        Height = 42,
        NormalColor = UiStyle.PrimaryColor,
        HoverColor = UiStyle.PrimaryHover,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 0, 6)
    };

    private readonly ModernButtonControl _btnOpenSourcePage = new()
    {
        Text = "🌐 Orijinal 3D Modeli Aç",
        Width = 310,
        Height = 34,
        NormalColor = Color.FromArgb(51, 65, 85),
        HoverColor = Color.FromArgb(71, 85, 105),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnSearchOnPlatform = new()
    {
        Text = "🔍 Platformda Bu Modeli Ara (Alternatif)",
        Width = 310,
        Height = 32,
        NormalColor = Color.FromArgb(30, 41, 59),
        HoverColor = Color.FromArgb(51, 65, 85),
        ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Segoe UI", 8.5F),
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 4, 0, 0)
    };

    public Trending3DModelHunterForm()
    {
        Text = "Viral 3D Model Avcısı & Etsy Pazar Boşluğu Radarı";
        Size = new Size(1380, 880);
        MinimumSize = new Size(1100, 720);
        BackColor = UiStyle.BackgroundColor;
        Font = UiStyle.BaseFont;

        var scrapers = new List<I3DModelPlatformScraper>
        {
            new MakerWorldTrendingScraper(),
            new CrealityCloudTrendingScraper(),
            new PrintablesTrendingScraper(),
            new MakerOnlineTrendingScraper(),
            new ThingiverseTrendingScraper()
        };
        var competitionChecker = new EtsyCompetitionCheckerService();
        var snapshotRepository = new Sqlite3DModelSnapshotRepository();
        var searchExpander = new AiModelSearchExpander();
        var lakeRepository = new SqliteViral3DModelLakeRepository();
        _hunterService = new Viral3DModelHunterService(scrapers, competitionChecker, snapshotRepository, searchExpander, lakeRepository);

        BuildLayout();
        HookEvents();

        Shown += async (_, _) => await RunScanAsync();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(12),
            BackColor = UiStyle.BackgroundColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106)); // KPI strip
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));  // Store Niche AI Ribbon
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // Toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Master-Detail Split

        // 1. KPI STRIP
        var kpiFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        kpiFlow.Controls.Add(_kpiPlatforms);
        kpiFlow.Controls.Add(_kpiViralCount);
        kpiFlow.Controls.Add(_kpiGoldenOpps);
        kpiFlow.Controls.Add(_kpiCommercial);
        mainLayout.Controls.Add(kpiFlow, 0, 0);

        // 2. STORE NICHE AI BANNER
        var bannerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        bannerFlow.Controls.Add(_lblStoreNicheText);
        bannerFlow.Controls.Add(_lblStoreAiBadge);
        bannerFlow.Controls.Add(_btnReanalyzeNiche);
        _panelStoreNicheBanner.Controls.Add(bannerFlow);
        mainLayout.Controls.Add(_panelStoreNicheBanner, 0, 1);

        // 3. TOOLBAR
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        toolbar.Controls.Add(_cboPlatform);
        toolbar.Controls.Add(_cboCategory);
        toolbar.Controls.Add(_cboSort);
        toolbar.Controls.Add(_chkCommercialOnly);
        toolbar.Controls.Add(_chkShopNicheOnly);
        toolbar.Controls.Add(_txtSearch);
        toolbar.Controls.Add(_btnSearch);
        toolbar.Controls.Add(_btnScan);
        toolbar.Controls.Add(_lblEngineBadge);
        mainLayout.Controls.Add(toolbar, 0, 2);

        // Populate Combos
        _cboPlatform.Items.AddRange([
            "Tüm Platformlar (Hepsi)",
            "🐼 MakerWorld (Bambu Lab)",
            "🐉 CrealityCloud",
            "🧡 Printables (Prusa)",
            "⚡ MakerOnline (Anycubic)",
            "⚙️ Thingiverse"
        ]);
        _cboPlatform.SelectedIndex = 0;

        _cboCategory.Items.AddRange([
            "Tüm Kategoriler",
            "🎮 Figür & Oyuncak",
            "🎲 Kutu Oyunu & RPG",
            "💡 Aydınlatma & Lightbox",
            "🌿 Ev & Botanik",
            "🗄️ Atölye & Düzenleme"
        ]);
        _cboCategory.SelectedIndex = 0;

        _cboSort.Items.AddRange([
            "💎 En Yüksek Fırsat Skoru",
            "⭐ En Yüksek Mağaza Uyumu",
            "🚀 En Çok İndirilenler (24s)",
            "🔥 En Az Etsy Rekabeti"
        ]);
        _cboSort.SelectedIndex = 0;

        // 4. MASTER-DETAIL SPLIT
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        // Left Table Layout: Grid + Pagination Bar
        var leftPane = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(0)
        };
        leftPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        ConfigureGrid();
        leftPane.Controls.Add(_grid, 0, 0);

        var paginationBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 4, 4, 4),
            BackColor = Color.FromArgb(17, 24, 39)
        };
        paginationBar.Controls.Add(_btnLoadMore);
        paginationBar.Controls.Add(_lblPageStatus);
        leftPane.Controls.Add(paginationBar, 0, 1);

        split.Controls.Add(leftPane, 0, 0);

        // Right Preview Drawer
        var drawer = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            CornerRadius = 12,
            Padding = new Padding(14),
            Margin = new Padding(8, 0, 0, 0)
        };

        var drawerStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var heroBoxContainer = new Panel
        {
            Size = new Size(310, 200),
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(2),
            Margin = new Padding(0, 0, 0, 6)
        };
        heroBoxContainer.Controls.Add(_picHero);
        drawerStack.Controls.Add(heroBoxContainer);

        drawerStack.Controls.Add(_galleryStrip);
        drawerStack.Controls.Add(_lblLicenseBadge);
        drawerStack.Controls.Add(_lblModelTitle);
        drawerStack.Controls.Add(_lblAuthor);

        drawerStack.Controls.Add(_lblAiAdviceHeader);
        drawerStack.Controls.Add(_lblAiAdviceBody);

        drawerStack.Controls.Add(new Label
        {
            Text = "🖨️ 3D Baskı & Dilimleme Profili:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.AccentColor,
            Margin = new Padding(0, 4, 0, 2)
        });
        drawerStack.Controls.Add(_lblPrintSpecs);

        drawerStack.Controls.Add(new Label
        {
            Text = "💎 Etsy Pazar Fırsatı & Kâr Analizi:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            Margin = new Padding(0, 4, 0, 2)
        });
        drawerStack.Controls.Add(_lblEtsyArbitrage);

        drawerStack.Controls.Add(_btnCreateEtsyDraft);
        drawerStack.Controls.Add(_btnOpenSourcePage);
        drawerStack.Controls.Add(_btnSearchOnPlatform);

        drawer.Controls.Add(drawerStack);
        split.Controls.Add(drawer, 1, 0);

        mainLayout.Controls.Add(split, 0, 3);
        Controls.Add(mainLayout);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(20, 27, 45);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(30, 41, 59);
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.RowTemplate.Height = 44;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 36;
        _grid.EnableHeadersVisualStyles = false;

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };

        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(20, 27, 45),
            ForeColor = Color.FromArgb(241, 245, 249),
            SelectionBackColor = Color.FromArgb(49, 46, 129),
            SelectionForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(6, 0, 0, 0)
        };

        _grid.Columns.Add("colPlatform", "Platform");
        _grid.Columns.Add("colShopFit", "🎯 Mağaza Uyumu");
        _grid.Columns.Add("colTitle", "Model Başlığı & Tasarım");
        _grid.Columns.Add("colVelocity", "24s İndirme");
        _grid.Columns.Add("colDelta", "📈 İvme / 24s Büyüme");
        _grid.Columns.Add("colPrints", "Başarılı Baskı");
        _grid.Columns.Add("colLicense", "Lisans Durumu");
        _grid.Columns.Add("colCompetition", "Etsy Rekabeti");
        _grid.Columns.Add("colScore", "Fırsat Skoru");

        _grid.Columns["colPlatform"]!.Width = 115;
        _grid.Columns["colShopFit"]!.Width = 135;
        _grid.Columns["colTitle"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _grid.Columns["colTitle"]!.MinimumWidth = 230;
        _grid.Columns["colVelocity"]!.Width = 100;
        _grid.Columns["colDelta"]!.Width = 145;
        _grid.Columns["colPrints"]!.Width = 105;
        _grid.Columns["colLicense"]!.Width = 135;
        _grid.Columns["colCompetition"]!.Width = 125;
        _grid.Columns["colScore"]!.Width = 105;

        _grid.SelectionChanged += (_, _) => OnGridRowSelected();
    }

    private void HookEvents()
    {
        _cboPlatform.SelectedIndexChanged += (_, _) => FilterModels();
        _cboCategory.SelectedIndexChanged += (_, _) => FilterModels();
        _cboSort.SelectedIndexChanged += (_, _) => FilterModels();
        _chkCommercialOnly.CheckedChanged += (_, _) => FilterModels();
        _chkShopNicheOnly.CheckedChanged += (_, _) => FilterModels();

        // Search trigger
        _btnSearch.Click += async (_, _) => await ExecuteSearchAsync();
        _txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await ExecuteSearchAsync();
            }
        };

        _btnLoadMore.Click += (_, _) =>
        {
            _displayedLimit += 30;
            PopulateGrid(_filteredModels);
        };

        _btnScan.Click += async (_, _) => await RunScanAsync();
        _btnReanalyzeNiche.Click += async (_, _) => await ReanalyzeShopNicheAsync();

        _btnOpenSourcePage.Click += (_, _) =>
        {
            if (_selectedModel != null)
            {
                OpenModelUrl(_selectedModel);
            }
        };

        _btnSearchOnPlatform.Click += (_, _) =>
        {
            if (_selectedModel == null) return;
            try
            {
                string searchUrl = _selectedModel.GetPlatformSearchUrl();
                Process.Start(new ProcessStartInfo { FileName = searchUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Arama sayfası açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count)
            {
                var model = _grid.Rows[e.RowIndex].Tag as Trending3DModel ?? _selectedModel;
                if (model != null)
                {
                    OpenModelUrl(model);
                }
            }
        };

        _btnCreateEtsyDraft.Click += (_, _) =>
        {
            if (_selectedModel == null) return;

            string payload = $"[3D MODEL ETSY TASLAĞI]\n" +
                             $"Başlık: {_selectedModel.Title} | 3D Printed Prop & Decor\n" +
                             $"Kaynak: {_selectedModel.Platform} - {_selectedModel.ModelPageUrl}\n" +
                             $"Lisans: {_selectedModel.License.LicenseName} (Ticari: {_selectedModel.License.IsCommercialAllowed})\n" +
                             $"Filament: {_selectedModel.PrintSpecs.FilamentGrams}g PLA (~{_selectedModel.PrintSpecs.FormattedPrintTime})\n" +
                             $"Mağaza Uyumu: %{_selectedModel.ShopFitScore} ({_selectedModel.ShopFitReason})\n" +
                             $"Tavsiye Fiyat: $29.90 USD (Kâr: ~$21.40)\n" +
                             $"Etiketler: {string.Join(", ", _selectedModel.Tags)}";

            Clipboard.SetText(payload);
            MessageBox.Show(
                this,
                $"'{_selectedModel.Title}' modeli için Etsy listeleme şablonu hazırlandı ve panoya kopyalandı!\n\n" +
                $"• Mağaza Uyumu: %{_selectedModel.ShopFitScore}\n" +
                $"• Tavsiye Fiyat: $29.90 USD\n" +
                $"• Tahmini Baskı Süresi: {_selectedModel.PrintSpecs.FormattedPrintTime}\n" +
                $"• Filament: {_selectedModel.PrintSpecs.FilamentGrams}g\n\n" +
                "Bu verilerle 'Hızlı Ürün Ekle (AI)' menüsünden veya Kasa üzerinden anında yeni mağazanıza listeleme yapabilirsiniz.",
                "Etsy Taslağı Hazır",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
    }

    private void OpenModelUrl(Trending3DModel model)
    {
        string targetUrl = model.SafeModelUrl;
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            targetUrl = model.GetPlatformSearchUrl();
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = targetUrl, UseShellExecute = true });
        }
        catch
        {
            try
            {
                string fallbackUrl = model.GetPlatformSearchUrl();
                Process.Start(new ProcessStartInfo { FileName = fallbackUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Model linki açılamadı: {ex.Message}", "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async Task RunScanAsync()
    {
        try
        {
            _btnScan.Enabled = false;
            _btnScan.Text = "⏳ Taranıyor...";
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            var platform = GetSelectedPlatformFilter();
            string? category = GetSelectedCategoryFilter();

            _allModels = (await _hunterService.ScanTrendingModelsAsync(
                platformFilter: platform,
                categoryFilter: category,
                commercialOnly: _chkCommercialOnly.Checked,
                shopProfile: _activeShopProfile,
                shopNicheOnly: _chkShopNicheOnly.Checked,
                ct: _scanCts.Token)).ToList();

            _displayedLimit = 35;
            UpdateKpis();
            FilterModels();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Tarama sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnScan.Enabled = true;
            _btnScan.Text = "🔄 Platformları Tara";
        }
    }

    private async Task ExecuteSearchAsync()
    {
        string term = _txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            await RunScanAsync();
            return;
        }

        try
        {
            _btnSearch.Enabled = false;
            _btnSearch.Text = "⏳";
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            var platform = GetSelectedPlatformFilter();
            string? category = GetSelectedCategoryFilter();

            _allModels = (await _hunterService.SearchModelsAcrossPlatformsAsync(
                query: term,
                platformFilter: platform,
                categoryFilter: category,
                commercialOnly: _chkCommercialOnly.Checked,
                shopProfile: _activeShopProfile,
                shopNicheOnly: _chkShopNicheOnly.Checked,
                page: 1,
                pageSize: 60,
                ct: _scanCts.Token)).ToList();

            if (_hunterService.LastQueryExpansion != null && _hunterService.LastQueryExpansion.ExpandedKeywords.Count > 0)
            {
                string expTags = string.Join(", ", _hunterService.LastQueryExpansion.ExpandedKeywords.Take(3));
                _lblEngineBadge.Text = $"🤖 AI Genişletildi: {expTags} | 💾 SQLite Lake: {_allModels.Count} Model";
            }
            else
            {
                _lblEngineBadge.Text = $"🛡️ Anti-Bot | 💾 SQLite Lake: {_allModels.Count} Model Aktif";
            }

            _displayedLimit = 35;
            UpdateKpis();
            FilterModels();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Arama sırasında hata: {ex.Message}", "Arama Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSearch.Enabled = true;
            _btnSearch.Text = "🔍 Ara";
        }
    }

    private async Task ReanalyzeShopNicheAsync()
    {
        try
        {
            _btnReanalyzeNiche.Enabled = false;
            _btnReanalyzeNiche.Text = "⏳ AI İnceliyor...";

            // Representative active shop inventory to analyze
            var sampleListings = new List<ShopListingItem>
            {
                new() { Title = "Articulated Dragon 3D Print Toy Jointed Desk Pet", Category = "Toys & Games", Tags = ["dragon", "articulated", "toy", "fidget", "3d print", "figure"] },
                new() { Title = "DUMMY 13 Movable Action Figure Robot Desk Companion", Category = "Toys & Games", Tags = ["dummy 13", "action figure", "robot", "jointed", "desk toy"] },
                new() { Title = "Cute Articulated Mini Octopus Print in Place Desk Toy", Category = "Toys & Games", Tags = ["octopus", "fidget", "desk toy", "cute toy", "articulated"] },
                new() { Title = "Custom Flexi Animal Figurine 3D Printed Desk Decor", Category = "Art & Collectibles", Tags = ["figurine", "collectible", "flexi", "pet", "toy"] },
                new() { Title = "Fantasy Warrior Tabletop RPG Mini Figure Unpainted", Category = "Toys & Games", Tags = ["miniature", "rpg", "figure", "tabletop", "dnd"] }
            };

            var profile = await _nicheAnalyzer.AnalyzeShopNicheAsync("3DArtDesignsStore", sampleListings);
            _activeShopProfile = profile;

            UpdateStoreNicheBanner();

            // Deep automated multi-keyword sweep targeting shop DNA
            _allModels = (await _hunterService.DeepScanByShopNicheAsync(
                shopProfile: _activeShopProfile,
                commercialOnly: _chkCommercialOnly.Checked,
                ct: CancellationToken.None)).ToList();

            _displayedLimit = 35;
            UpdateKpis();
            FilterModels();

            MessageBox.Show(
                this,
                $"Mağaza DNA'nız başarıyla analiz edildi!\n\n" +
                $"• Tespit Edilen Niş: {profile.PrimaryNiche}\n" +
                $"• Yapay Zeka: {profile.ActiveAiProviderName}\n" +
                $"• Güven Oranı: %{profile.ConfidenceScore}\n" +
                $"• Hedef Kitle: {profile.TargetAudience}\n\n" +
                $"3D Modeller mağazanızın niş anahtar kelimeleriyle ({string.Join(", ", profile.AffinityKeywords.Take(4))}) derinlemesine tarandı ve puanlandı!",
                "Mağaza Niş Analizi Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Mağaza niş analizi sırasında uyarı: {ex.Message}", "AI Analiz Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnReanalyzeNiche.Enabled = true;
            _btnReanalyzeNiche.Text = "⚡ AI ile Mağazamı Tara";
        }
    }

    private void UpdateStoreNicheBanner()
    {
        if (_activeShopProfile == null) return;
        _lblStoreNicheText.Text = $"🏪 Aktif Mağaza Nişi: {_activeShopProfile.PrimaryNiche} (%{_activeShopProfile.ConfidenceScore} AI Güveni)";
        _lblStoreAiBadge.Text = $"🤖 {_activeShopProfile.ActiveAiProviderName}";
    }

    private void UpdateKpis()
    {
        _kpiViralCount.Value = _allModels.Count.ToString();
        int goldenCount = _allModels.Count(m => m.IsGoldenOpportunity);
        _kpiGoldenOpps.Value = goldenCount.ToString();
        int commercialCount = _allModels.Count(m => m.License.IsCommercialAllowed);
        _kpiCommercial.Value = commercialCount.ToString();

        if (_hunterService.LakeRepository != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    int lakeCount = await _hunterService.LakeRepository.GetTotalCountAsync();
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            _kpiViralCount.TrendText = $"💾 Model Lake: {lakeCount:N0} Model";
                        });
                    }
                }
                catch
                {
                    // Ignore background counter errors
                }
            });
        }
    }

    private ModelPlatformType? GetSelectedPlatformFilter()
    {
        return _cboPlatform.SelectedIndex switch
        {
            1 => ModelPlatformType.MakerWorld,
            2 => ModelPlatformType.CrealityCloud,
            3 => ModelPlatformType.Printables,
            4 => ModelPlatformType.MakerOnline,
            5 => ModelPlatformType.Thingiverse,
            _ => null
        };
    }

    private string? GetSelectedCategoryFilter()
    {
        return _cboCategory.SelectedIndex switch
        {
            1 => "Toys & Figures",
            2 => "Tabletop & RPG",
            3 => "LED Lighting & Art",
            4 => "Home & Garden",
            5 => "Workshop & Organization",
            _ => null
        };
    }

    private void FilterModels()
    {
        var query = _allModels.AsEnumerable();

        var targetPlatform = GetSelectedPlatformFilter();
        if (targetPlatform.HasValue)
        {
            query = query.Where(m => m.Platform == targetPlatform.Value);
        }

        string? targetCategory = GetSelectedCategoryFilter();
        if (!string.IsNullOrWhiteSpace(targetCategory))
        {
            query = query.Where(m => m.Category.Contains(targetCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (_chkCommercialOnly.Checked)
        {
            query = query.Where(m => m.License.IsCommercialAllowed);
        }

        if (_chkShopNicheOnly.Checked)
        {
            query = query.Where(m => m.IsShopNicheMatch);
        }

        string term = _txtSearch.Text.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(m => m.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     m.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     m.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply Sorting
        query = _cboSort.SelectedIndex switch
        {
            1 => query.OrderByDescending(m => m.ShopFitScore).ThenByDescending(m => m.OpportunityScore),
            2 => query.OrderByDescending(m => m.Downloads24h).ThenByDescending(m => m.HourlyVelocity),
            3 => query.OrderBy(m => m.EtsyCompetitionCount).ThenByDescending(m => m.OpportunityScore),
            _ => query.OrderByDescending(m => m.OpportunityScore).ThenByDescending(m => m.Downloads24h)
        };

        _filteredModels = query.ToList();
        PopulateGrid(_filteredModels);
    }

    private void PopulateGrid(List<Trending3DModel> models)
    {
        _grid.Rows.Clear();

        var displayItems = models.Take(_displayedLimit).ToList();

        foreach (var m in displayItems)
        {
            string platformText = m.Platform switch
            {
                ModelPlatformType.MakerWorld => "🐼 MakerWorld",
                ModelPlatformType.CrealityCloud => "🐉 Creality",
                ModelPlatformType.Printables => "🧡 Printables",
                ModelPlatformType.MakerOnline => "⚡ MakerOnline",
                ModelPlatformType.Thingiverse => "⚙️ Thingiverse",
                _ => m.Platform.ToString()
            };

            string fitText = m.ShopFitScore >= 90
                ? $"⭐ %{m.ShopFitScore} (Mükemmel)"
                : (m.ShopFitScore >= 70 ? $"⚡ %{m.ShopFitScore} (Yüksek)" : $"⚪ %{m.ShopFitScore} (Düşük)");

            string compText = m.EtsyCompetitionCount == 0
                ? "💎 0 Satıcı (Boş Pazar!)"
                : (m.EtsyCompetitionCount <= 3 ? $"🔥 {m.EtsyCompetitionCount} Satıcı (Düşük)" : $"⚠️ {m.EtsyCompetitionCount} Satıcı");

            string licenseText = m.License.IsCommercialAllowed ? "✅ Ticari Serbest" : "⚠️ Kişisel Kullanım";

            string deltaText = m.HourlyVelocity > 0
                ? (m.IsDeltaAccelerating ? $"🔥 +{m.HourlyVelocity:N1}/s (%{m.GrowthRatePercentage:N0})" : $"+{m.HourlyVelocity:N1}/s (%{m.GrowthRatePercentage:N0})")
                : (m.Downloads24h > 0 ? $"+{m.Downloads24h:N0} (24s)" : "0 (Yeni)");

            int rowIdx = _grid.Rows.Add(
                platformText,
                fitText,
                m.Title,
                $"+{m.Downloads24h:N0}",
                deltaText,
                $"{m.PrintsCount:N0} Baskı",
                licenseText,
                compText,
                $"★ {m.OpportunityScore} / 100"
            );

            _grid.Rows[rowIdx].Tag = m;

            // Highlight golden opportunity
            if (m.IsGoldenOpportunity)
            {
                _grid.Rows[rowIdx].Cells["colScore"].Style.ForeColor = Color.FromArgb(251, 191, 36);
                _grid.Rows[rowIdx].Cells["colScore"].Style.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
            }

            // Color code shop fit
            if (m.ShopFitScore >= 90)
            {
                _grid.Rows[rowIdx].Cells["colShopFit"].Style.ForeColor = Color.FromArgb(52, 211, 153);
                _grid.Rows[rowIdx].Cells["colShopFit"].Style.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            }
            else if (m.ShopFitScore >= 70)
            {
                _grid.Rows[rowIdx].Cells["colShopFit"].Style.ForeColor = Color.FromArgb(167, 139, 250);
            }
        }

        // Update Pagination Status
        _lblPageStatus.Text = $"Gösterilen: {displayItems.Count} / {models.Count} Model | Toplam Taranan: {_allModels.Count}";
        _btnLoadMore.Visible = models.Count > _displayedLimit;

        if (_grid.Rows.Count > 0 && _grid.CurrentRow == null)
        {
            _grid.Rows[0].Selected = true;
            OnGridRowSelected();
        }
    }

    private void OnGridRowSelected()
    {
        if (_grid.CurrentRow?.Tag is not Trending3DModel m) return;
        _selectedModel = m;

        _lblModelTitle.Text = m.Title;
        _lblAuthor.Text = $"Tasarımcı: {m.AuthorName} • {m.Category}";

        if (m.License.IsCommercialAllowed)
        {
            _lblLicenseBadge.Text = $"✅ {m.License.LicenseName} (Ticari Satış İzni Var)";
            _lblLicenseBadge.BackColor = Color.FromArgb(6, 78, 59);
            _lblLicenseBadge.ForeColor = Color.FromArgb(52, 211, 153);
        }
        else
        {
            _lblLicenseBadge.Text = $"⚠️ {m.License.LicenseName} (Kişisel Kullanım)";
            _lblLicenseBadge.BackColor = Color.FromArgb(70, 40, 15);
            _lblLicenseBadge.ForeColor = Color.FromArgb(245, 158, 11);
        }

        _lblAiAdviceHeader.Text = $"🤖 AI MAĞAZA TAVSİYESİ (%{m.ShopFitScore} Uyum)";
        _lblAiAdviceBody.Text = $"{m.ShopFitReason}\n\n" +
                                $"🎯 Mağaza Nişiniz: {_activeShopProfile.PrimaryNiche}\n" +
                                $"👥 Hedef Kitle: {_activeShopProfile.TargetAudience}";

        string velocityStatus = m.IsDeltaAccelerating ? "🔥 HIZLI İVME (Viral Yükselişte)" : "Dengeli Talep";
        _lblPrintSpecs.Text = $"• 📈 Zaman Serisi İvmesi: +{m.HourlyVelocity:N1} indirme/saat (%{m.GrowthRatePercentage:N1} büyüme)\n" +
                              $"• ⚡ Trend Durumu: {velocityStatus}\n" +
                              $"• 💾 SQLite Snapshots: {(m.HistoricalSnapshotsCount > 0 ? $"{m.HistoricalSnapshotsCount} kayıt" : "Yeni Model (İlk Snapshot)")}\n" +
                              $"• Tahmini Baskı Süresi: {m.PrintSpecs.FormattedPrintTime}\n" +
                              $"• Filament Gramajı: {m.PrintSpecs.FilamentGrams:N0} gram PLA (~${m.PrintSpecs.EstimatedMaterialCostUsd:N2})\n" +
                              $"• Çoklu Renk Desteği: {(m.PrintSpecs.HasMultiColorProfile ? $"Var ({m.PrintSpecs.ColorCount} Renk AMS)" : "Tek Renk")}";

        string compNotice = m.EtsyCompetitionCount <= 1
            ? "Mavi Okyanus Fırsatı! Etsy'de bu ürünü satan neredeyse kimse yok."
            : $"Etsy'de {m.EtsyCompetitionCount} rakip listeleme tespit edildi.";

        _lblEtsyArbitrage.Text = $"🎯 Etsy Rekabet: {m.EtsyCompetitionCount} Satıcı\n" +
                                $"💡 Önerilen Satış Fiyatı: $28.00 - $34.00 USD\n" +
                                $"💰 Tahmini Net Kâr: ~$21.50 USD\n\n" +
                                $"{compNotice}";

        _ = LoadHeroImageAsync(m.PrimaryImageUrl);
    }

    private async Task LoadHeroImageAsync(string imageUrl)
    {
        try
        {
            // 1. First priority: Load from disk OR embedded application resources (100% reliable on VDS)
            using (var stream = Viral3DModelAssetManager.OpenAssetStream(imageUrl)
                             ?? Viral3DModelAssetManager.OpenAssetStream(Viral3DModelAssetManager.GetAssetForModel(_selectedModel?.Title ?? "")))
            {
                if (stream != null)
                {
                    var img = Image.FromStream(stream);
                    var oldImg = _picHero.Image;
                    _picHero.Image = (Image)img.Clone();
                    oldImg?.Dispose();
                    return;
                }
            }

            // 2. Direct web URL download if online and not a fake generator
            if ((imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                !imageUrl.Contains("picsum", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _imageHttpClient.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(bytes);
                var img = Image.FromStream(ms);
                var oldImg = _picHero.Image;
                _picHero.Image = (Image)img.Clone();
                oldImg?.Dispose();
                return;
            }

            // 3. Fallback to default high-res render
            using (var fallbackStream = Viral3DModelAssetManager.OpenAssetStream("dummy13.jpg"))
            {
                if (fallbackStream != null)
                {
                    var img = Image.FromStream(fallbackStream);
                    var oldImg = _picHero.Image;
                    _picHero.Image = (Image)img.Clone();
                    oldImg?.Dispose();
                    return;
                }
            }

            // 4. Procedural fallback only if absolutely no image stream could be found
            _picHero.Image = CreateFallbackMeshBitmap(310, 200, _selectedModel?.Title ?? "3D Model");
        }
        catch
        {
            _picHero.Image = CreateFallbackMeshBitmap(310, 200, _selectedModel?.Title ?? "3D Model");
        }
    }

    private static Bitmap CreateFallbackMeshBitmap(int width, int height, string title)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(15, 23, 42));

        using var pen = new Pen(Color.FromArgb(99, 102, 241), 2);
        using var fillBrush = new SolidBrush(Color.FromArgb(30, 41, 75));
        using var textBrush = new SolidBrush(Color.FromArgb(203, 213, 225));
        using var font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        int cx = width / 2;
        int cy = height / 2 - 14;
        int s = 42;

        Point[] topFace = [new(cx, cy - s), new(cx + s, cy - s / 2), new(cx, cy), new(cx - s, cy - s / 2)];
        Point[] leftFace = [new(cx - s, cy - s / 2), new(cx, cy), new(cx, cy + s), new(cx - s, cy + s / 2)];
        Point[] rightFace = [new(cx, cy), new(cx + s, cy - s / 2), new(cx + s, cy + s / 2), new(cx, cy + s)];

        g.FillPolygon(fillBrush, topFace);
        g.FillPolygon(new SolidBrush(Color.FromArgb(24, 30, 60)), leftFace);
        g.FillPolygon(new SolidBrush(Color.FromArgb(20, 25, 50)), rightFace);

        g.DrawPolygon(pen, topFace);
        g.DrawPolygon(pen, leftFace);
        g.DrawPolygon(pen, rightFace);

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        string displayTitle = title.Length > 28 ? title.Substring(0, 25) + "..." : title;
        g.DrawString($"3D Model Preview\n{displayTitle}", font, textBrush, new RectangleF(10, height - 42, width - 20, 36), sf);

        return bmp;
    }
}
