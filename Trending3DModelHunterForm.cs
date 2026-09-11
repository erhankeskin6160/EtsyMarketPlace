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
using EtsyMarketPlace.Application.Viral3DModels.Services;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
using SimilarProductsWinForms.Controls;

public sealed class Trending3DModelHunterForm : Form
{
    private readonly Viral3DModelHunterService _hunterService;
    private readonly HttpClient _imageHttpClient = new();

    private List<Trending3DModel> _allModels = [];
    private Trending3DModel? _selectedModel;
    private CancellationTokenSource? _scanCts;

    // KPI Tiles
    private readonly ModernKpiTile _kpiPlatforms = new() { Title = "AKTİF PLATFORMLAR", Value = "5", TrendText = "Bambu/Creality/Prusa", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiViralCount = new() { Title = "VİRAL MODELLER", Value = "0", TrendText = "Son 24-48 Saat", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiGoldenOpps = new() { Title = "ALTIN FIRSATLAR", Value = "0", TrendText = "Etsy Rekabet < 3", IsPositive = true, Width = 230 };
    private readonly ModernKpiTile _kpiCommercial = new() { Title = "TİCARİ LİSANSLI", Value = "0", TrendText = "Satılabilir", IsPositive = true, Width = 230 };

    // Toolbar controls
    private readonly ComboBox _cboPlatform = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 210,
        Height = 32,
        Font = new Font("Segoe UI", 9.2F)
    };

    private readonly CheckBox _chkCommercialOnly = new()
    {
        Text = "Yalnızca Ticari Satışa Uygun (Commercial Use) Modeller",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Margin = new Padding(12, 6, 12, 0)
    };

    private readonly TextBox _txtSearch = new()
    {
        Width = 220,
        Height = 32,
        PlaceholderText = "🔍 Model, etiket ara...",
        Font = new Font("Segoe UI", 9.2F),
        Margin = new Padding(6, 2, 6, 0)
    };

    private readonly ModernButtonControl _btnScan = new()
    {
        Text = "🔄 Platformları Şimdi Tara",
        Width = 190,
        Height = 34,
        NormalColor = UiStyle.PrimaryColor,
        HoverColor = UiStyle.PrimaryHover,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly Label _lblEngineBadge = new()
    {
        Text = "🛡️ Anti-Bot & Throttled | 💾 SQLite Delta Radarı Aktif",
        AutoSize = true,
        ForeColor = Color.FromArgb(52, 211, 153),
        BackColor = Color.FromArgb(6, 78, 59),
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        Padding = new Padding(6, 6, 6, 6),
        Margin = new Padding(8, 4, 0, 0)
    };

    // Grid
    private readonly DataGridView _grid = new();

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

    public Trending3DModelHunterForm()
    {
        Text = "Viral 3D Model Avcısı & Etsy Pazar Boşluğu Radarı";
        Size = new Size(1360, 860);
        MinimumSize = new Size(1100, 720);
        BackColor = UiStyle.BackgroundColor;
        Font = UiStyle.BaseFont;

        var scrapers = new List<EtsyMarketPlace.Domain.Viral3DModels.Interfaces.I3DModelPlatformScraper>
        {
            new MakerWorldTrendingScraper(),
            new CrealityCloudTrendingScraper(),
            new PrintablesTrendingScraper(),
            new MakerOnlineTrendingScraper(),
            new ThingiverseTrendingScraper()
        };
        var competitionChecker = new EtsyCompetitionCheckerService();
        var snapshotRepository = new Sqlite3DModelSnapshotRepository();
        _hunterService = new Viral3DModelHunterService(scrapers, competitionChecker, snapshotRepository);

        BuildLayout();
        HookEvents();

        Shown += async (_, _) => await RunScanAsync();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
            BackColor = UiStyle.BackgroundColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106)); // KPI strip
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // Toolbar
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

        // 2. TOOLBAR
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };
        toolbar.Controls.Add(new Label { Text = "Platform:", AutoSize = true, ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 4, 0), Font = new Font("Segoe UI Semibold", 9F) });
        toolbar.Controls.Add(_cboPlatform);
        toolbar.Controls.Add(_chkCommercialOnly);
        toolbar.Controls.Add(_txtSearch);
        toolbar.Controls.Add(_btnScan);
        toolbar.Controls.Add(_lblEngineBadge);
        mainLayout.Controls.Add(toolbar, 0, 1);

        // Populate Platform combo
        _cboPlatform.Items.Add("Tüm Platformlar (Hepsi)");
        _cboPlatform.Items.Add("🐼 MakerWorld (Bambu Lab)");
        _cboPlatform.Items.Add("🐉 CrealityCloud");
        _cboPlatform.Items.Add("🧡 Printables (Prusa)");
        _cboPlatform.Items.Add("⚡ MakerOnline (Anycubic)");
        _cboPlatform.Items.Add("⚙️ Thingiverse");
        _cboPlatform.SelectedIndex = 0;

        // 3. MASTER-DETAIL SPLIT
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        ConfigureGrid();
        split.Controls.Add(_grid, 0, 0);

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

        drawerStack.Controls.Add(new Label
        {
            Text = "🖨️ 3D Baskı & Dilimleme Profili:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.AccentColor,
            Margin = new Padding(0, 6, 0, 2)
        });
        drawerStack.Controls.Add(_lblPrintSpecs);

        drawerStack.Controls.Add(new Label
        {
            Text = "💎 Etsy Pazar Fırsatı & Kâr Analizi:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            Margin = new Padding(0, 6, 0, 2)
        });
        drawerStack.Controls.Add(_lblEtsyArbitrage);

        drawerStack.Controls.Add(_btnCreateEtsyDraft);
        drawerStack.Controls.Add(_btnOpenSourcePage);

        drawer.Controls.Add(drawerStack);
        split.Controls.Add(drawer, 1, 0);

        mainLayout.Controls.Add(split, 0, 2);
        Controls.Add(mainLayout);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(20, 27, 45);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(45, 55, 75);
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowTemplate.Height = 46;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 27, 45);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.2F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 38;
        _grid.EnableHeadersVisualStyles = false;

        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            _grid,
            new object[] { true });

        _grid.Columns.Add("colPlatform", "Platform");
        _grid.Columns.Add("colTitle", "Model Başlığı & Tasarım");
        _grid.Columns.Add("colVelocity", "24s İndirme");
        _grid.Columns.Add("colDelta", "📈 İvme / 24s Büyüme");
        _grid.Columns.Add("colPrints", "Başarılı Baskı");
        _grid.Columns.Add("colLicense", "Lisans Durumu");
        _grid.Columns.Add("colCompetition", "Etsy Rekabeti");
        _grid.Columns.Add("colScore", "Fırsat Skoru");

        _grid.Columns["colPlatform"]!.Width = 120;
        _grid.Columns["colTitle"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _grid.Columns["colTitle"]!.MinimumWidth = 230;
        _grid.Columns["colVelocity"]!.Width = 100;
        _grid.Columns["colDelta"]!.Width = 145;
        _grid.Columns["colPrints"]!.Width = 105;
        _grid.Columns["colLicense"]!.Width = 140;
        _grid.Columns["colCompetition"]!.Width = 130;
        _grid.Columns["colScore"]!.Width = 110;

        _grid.SelectionChanged += (_, _) => OnGridRowSelected();
    }

    private void HookEvents()
    {
        _cboPlatform.SelectedIndexChanged += (_, _) => FilterModels();
        _chkCommercialOnly.CheckedChanged += (_, _) => FilterModels();
        _txtSearch.TextChanged += (_, _) => FilterModels();

        _btnScan.Click += async (_, _) => await RunScanAsync();

        _btnOpenSourcePage.Click += (_, _) =>
        {
            if (_selectedModel != null && !string.IsNullOrWhiteSpace(_selectedModel.ModelPageUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = _selectedModel.ModelPageUrl, UseShellExecute = true });
                }
                catch { }
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
                             $"Tavsiye Fiyat: $29.90 USD (Kâr: ~$21.40)\n" +
                             $"Etiketler: {string.Join(", ", _selectedModel.Tags)}";

            Clipboard.SetText(payload);
            MessageBox.Show(
                this,
                $"'{_selectedModel.Title}' modeli için Etsy listeleme şablonu hazırlandı ve panoya kopyalandı!\n\n" +
                $"• Tavsiye Fiyat: $29.90 USD\n" +
                $"• Tahmini Baskı Süresi: {_selectedModel.PrintSpecs.FormattedPrintTime}\n" +
                $"• Filament: {_selectedModel.PrintSpecs.FilamentGrams}g\n\n" +
                "Bu verilerle 'Hızlı Ürün Ekle (AI)' menüsünden veya Kasa üzerinden anında yeni mağazanıza listeleme yapabilirsiniz.",
                "Etsy Taslağı Hazır",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
    }

    private async Task RunScanAsync()
    {
        try
        {
            _btnScan.Enabled = false;
            _btnScan.Text = "⏳ Taranıyor...";
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            _allModels = (await _hunterService.ScanTrendingModelsAsync(ct: _scanCts.Token)).ToList();

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
            _btnScan.Text = "🔄 Platformları Şimdi Tara";
        }
    }

    private void UpdateKpis()
    {
        _kpiViralCount.Value = _allModels.Count.ToString();
        int goldenCount = _allModels.Count(m => m.IsGoldenOpportunity);
        _kpiGoldenOpps.Value = goldenCount.ToString();
        int commercialCount = _allModels.Count(m => m.License.IsCommercialAllowed);
        _kpiCommercial.Value = commercialCount.ToString();
    }

    private void FilterModels()
    {
        var query = _allModels.AsEnumerable();

        int selectedPlatformIdx = _cboPlatform.SelectedIndex;
        if (selectedPlatformIdx > 0)
        {
            var targetPlatform = (selectedPlatformIdx - 1) switch
            {
                0 => ModelPlatformType.MakerWorld,
                1 => ModelPlatformType.CrealityCloud,
                2 => ModelPlatformType.Printables,
                3 => ModelPlatformType.MakerOnline,
                4 => ModelPlatformType.Thingiverse,
                _ => (ModelPlatformType?)null
            };

            if (targetPlatform.HasValue)
            {
                query = query.Where(m => m.Platform == targetPlatform.Value);
            }
        }

        if (_chkCommercialOnly.Checked)
        {
            query = query.Where(m => m.License.IsCommercialAllowed);
        }

        string term = _txtSearch.Text.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(m => m.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     m.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     m.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        PopulateGrid(filtered);
    }

    private void PopulateGrid(List<Trending3DModel> models)
    {
        _grid.Rows.Clear();
        foreach (var m in models)
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

            string compText = m.EtsyCompetitionCount == 0
                ? "💎 0 Satıcı (Boş Pazar!)"
                : (m.EtsyCompetitionCount <= 3 ? $"🔥 {m.EtsyCompetitionCount} Satıcı (Düşük)" : $"⚠️ {m.EtsyCompetitionCount} Satıcı");

            string licenseText = m.License.IsCommercialAllowed ? "✅ Ticari Serbest" : "⚠️ Kişisel Kullanım";

            string deltaText = m.HourlyVelocity > 0
                ? (m.IsDeltaAccelerating ? $"🔥 +{m.HourlyVelocity:N1}/s (%{m.GrowthRatePercentage:N0})" : $"+{m.HourlyVelocity:N1}/s (%{m.GrowthRatePercentage:N0})")
                : $"+{m.Downloads24h:N0} (24s)";

            int rowIdx = _grid.Rows.Add(
                platformText,
                m.Title,
                $"+{m.Downloads24h:N0}",
                deltaText,
                $"{m.PrintsCount:N0} Baskı",
                licenseText,
                compText,
                $"⭐ {m.OpportunityScore} / 100"
            );
            _grid.Rows[rowIdx].Tag = m;
        }

        if (_grid.Rows.Count > 0)
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
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _picHero.Image = null;
                return;
            }

            var bytes = await _imageHttpClient.GetByteArrayAsync(imageUrl);
            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            var oldImg = _picHero.Image;
            _picHero.Image = img;
            oldImg?.Dispose();
        }
        catch
        {
            _picHero.Image = null;
        }
    }
}
