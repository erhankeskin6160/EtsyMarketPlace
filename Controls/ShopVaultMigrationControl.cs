namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;
using SimilarProductsWinForms.Services;

internal sealed class ShopVaultMigrationControl : UserControl
{
    private readonly IEtsyVaultApiClient _apiClient;
    private readonly IAntiBanSanitizer _antiBanSanitizer;
    private readonly ShopMigrationDeploymentService _deploymentService;

    private IReadOnlyList<VaultListing> _queuedListings = [];
    private List<VaultListing> _failedListings = [];
    private CancellationTokenSource? _cts;

    // Target Shop Verification UI
    private readonly Label _lblShopStatus = new()
    {
        AutoSize = true,
        Text = "● Hedef mağaza API bağlantısı test ediliyor...",
        ForeColor = UiStyle.WarningColor,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Margin = new Padding(4, 7, 16, 0)
    };

    private readonly ModernButtonControl _btnVerifyShop = new()
    {
        Text = "🔍 Bağlantıyı Test Et",
        Width = 150,
        Height = 34,
        NormalColor = Color.FromArgb(51, 65, 85),
        HoverColor = Color.FromArgb(71, 85, 105),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnOpenApiSettings = new()
    {
        Text = "⚙️ API Ayarlarını Aç",
        Width = 150,
        Height = 34,
        NormalColor = Color.FromArgb(51, 65, 85),
        HoverColor = Color.FromArgb(71, 85, 105),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private long _targetShopId = 0;
    private string _targetShopName = "";

    // Security & Anti-ban 3 Tier Cards
    private SecurityTierCardControl _cardTierHigh = null!;
    private SecurityTierCardControl _cardTierStandard = null!;
    private SecurityTierCardControl _cardTierClone = null!;

    // Anti-ban Checkboxes
    private readonly CheckBox _chkStripExif = new()
    {
        Text = "EXIF, GPS & Seri No Temizle",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 3, 8, 3)
    };

    private readonly CheckBox _chkPermutateImageHash = new()
    {
        Text = "pHash Kırıcı (1-2px Micro-crop)",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 3, 8, 3)
    };

    private readonly CheckBox _chkAiRewriteTitle = new()
    {
        Text = "AI ile Başlığı Özgünleştir",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 3, 8, 3)
    };

    private readonly CheckBox _chkAiRewriteDesc = new()
    {
        Text = "AI ile Açıklamayı Özgünleştir",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 3, 8, 3)
    };

    private readonly CheckBox _chkDraftFirst = new()
    {
        Text = "Taslak Olarak Yükle (Draft Mode)",
        Checked = true,
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI", 9F),
        Margin = new Padding(0, 3, 8, 3)
    };

    private readonly NumericUpDown _numThrottle = new()
    {
        Minimum = 1,
        Maximum = 30,
        Value = 5,
        Width = 60,
        Height = 26,
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(30, 41, 59),
        ForeColor = UiStyle.TextDark
    };

    private readonly NumericUpDown _numPriceAdj = new()
    {
        Minimum = -50,
        Maximum = 100,
        Value = 0,
        Width = 85,
        Height = 26,
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(30, 41, 59),
        ForeColor = UiStyle.TextDark
    };

    private readonly TextBox _txtSkuPrefix = new()
    {
        Text = "NEW_",
        Width = 95,
        Height = 26,
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(30, 41, 59),
        ForeColor = UiStyle.TextDark,
        BorderStyle = BorderStyle.FixedSingle
    };

    // Mapping inputs
    private readonly TextBox _txtDefaultShippingId = new()
    {
        Width = 160,
        Height = 26,
        PlaceholderText = "Örn: 123456789",
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(30, 41, 59),
        ForeColor = UiStyle.TextDark,
        BorderStyle = BorderStyle.FixedSingle
    };

    private readonly TextBox _txtDefaultReturnId = new()
    {
        Width = 160,
        Height = 26,
        PlaceholderText = "Örn: 987654321",
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(30, 41, 59),
        ForeColor = UiStyle.TextDark,
        BorderStyle = BorderStyle.FixedSingle
    };

    // Deployment controls
    private readonly Label _lblQueueSummary = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(52, 211, 153),
        BackColor = Color.FromArgb(6, 78, 59),
        Text = "📦 Kuyruk: 0 Ürün Seçili",
        Padding = new Padding(8, 4, 8, 4)
    };

    private readonly ModernButtonControl _btnStart = new()
    {
        Text = "🚀 1-TIKLA GÜVENLİ GÖÇÜ BAŞLAT",
        Height = 42,
        Width = 340,
        NormalColor = UiStyle.PrimaryColor,
        HoverColor = UiStyle.PrimaryHover,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnCancel = new()
    {
        Text = "⏹️ Durdur",
        Height = 42,
        Width = 110,
        NormalColor = UiStyle.DangerColor,
        HoverColor = Color.FromArgb(220, 38, 38),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Enabled = false,
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnRetryFailed = new()
    {
        Text = "🔄 Başarısızları Yeniden Dene",
        Height = 42,
        Width = 240,
        NormalColor = UiStyle.WarningColor,
        HoverColor = Color.FromArgb(217, 119, 6),
        ForeColor = Color.Black,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Visible = false,
        Cursor = Cursors.Hand
    };

    private readonly ProgressBar _progressBar = new()
    {
        Dock = DockStyle.Fill,
        Height = 16,
        Minimum = 0,
        Maximum = 100,
        Value = 0
    };

    private readonly Label _lblProgressStatus = new()
    {
        AutoSize = true,
        ForeColor = UiStyle.TextDark,
        Font = UiStyle.BaseFont,
        Text = "0/0 Ürün • Hazır.",
        Margin = new Padding(12, 5, 0, 0)
    };

    // Terminal controls
    private readonly ModernButtonControl _btnCopyLog = new()
    {
        Text = "📋 Kopyala",
        Width = 85,
        Height = 24,
        NormalColor = Color.FromArgb(30, 41, 59),
        HoverColor = Color.FromArgb(51, 65, 85),
        ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnClearLog = new()
    {
        Text = "🧹 Temizle",
        Width = 85,
        Height = 24,
        NormalColor = Color.FromArgb(30, 41, 59),
        HoverColor = Color.FromArgb(51, 65, 85),
        ForeColor = Color.FromArgb(203, 213, 225),
        Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(11, 15, 25),
        ForeColor = Color.FromArgb(226, 232, 240),
        Font = new Font("Consolas", 9.2F),
        BorderStyle = BorderStyle.None
    };

    public ShopVaultMigrationControl(
        IEtsyVaultApiClient apiClient,
        IAntiBanSanitizer antiBanSanitizer)
    {
        _apiClient = apiClient;
        _antiBanSanitizer = antiBanSanitizer;
        _deploymentService = new ShopMigrationDeploymentService(_apiClient, _antiBanSanitizer);

        Dock = DockStyle.Fill;
        BuildLayout();
        HookEvents();
        _ = LoadAndVerifyTargetShopAsync();
    }

    public void SetListingsToMigrate(IReadOnlyList<VaultListing> listings)
    {
        _queuedListings = listings ?? [];
        _lblQueueSummary.Text = $"📦 Kuyruk: {_queuedListings.Count} Ürün Seçili";
        _progressBar.Value = 0;
        _lblProgressStatus.Text = $"{_queuedListings.Count} ürün aktarılmak üzere bekliyor.";
        _btnStart.Enabled = _queuedListings.Count > 0;
        _btnStart.Text = $"🚀 1-TIKLA GÜVENLİ GÖÇÜ BAŞLAT ({_queuedListings.Count} ÜRÜN)";
        _btnRetryFailed.Visible = false;
        AppendLog($"[KUYRUK GÜNCELLENDİ] {_queuedListings.Count} ürün transfer kuyruğuna yüklendi.");
    }

    private void BuildLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = UiStyle.BackgroundColor,
            Panel1MinSize = 340,
            Panel2MinSize = 130
        };

        // Panel 1: Top Scrollable Form Content (Never clips or crunches controls)
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = UiStyle.BackgroundColor
        };

        var contentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = UiStyle.BackgroundColor
        };
        contentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 1. Shop Card
        var shopCard = BuildShopVerificationCard();
        contentTable.Controls.Add(shopCard, 0, 0);

        // 2. Strategy Card (3 Tier comparison cards)
        var strategyCard = BuildStrategyTierCard();
        contentTable.Controls.Add(strategyCard, 0, 1);

        // 3. Settings & Mapping Card
        var settingsCard = BuildSettingsAndMappingCard();
        contentTable.Controls.Add(settingsCard, 0, 2);

        // 4. Action & Progress Card
        var actionCard = BuildActionAndProgressCard();
        contentTable.Controls.Add(actionCard, 0, 3);

        scrollPanel.Controls.Add(contentTable);
        split.Panel1.Controls.Add(scrollPanel);

        // Panel 2: Bottom Terminal Console
        var terminalCard = BuildTerminalCard();
        split.Panel2.Controls.Add(terminalCard);

        Controls.Add(split);

        // Set initial splitter distance based on height safely
        split.Resize += (_, _) =>
        {
            try
            {
                if (split.Height > split.Panel1MinSize + split.Panel2MinSize)
                {
                    split.SplitterDistance = Math.Clamp(split.Height - 220, split.Panel1MinSize, split.Height - split.Panel2MinSize);
                }
            }
            catch { }
        };
    }

    private Control BuildShopVerificationCard()
    {
        var card = CreateCardPanel("🎯 Hedef Etsy Mağazası (Aktarım / Yeni Mağaza Hesabı)");
        
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(4, 2, 4, 4)
        };
        flow.Controls.Add(_lblShopStatus);
        flow.Controls.Add(_btnVerifyShop);
        flow.Controls.Add(_btnOpenApiSettings);

        card.Controls.Add(flow);
        return card;
    }

    private Control BuildStrategyTierCard()
    {
        var card = CreateCardPanel("🛡️ Anti-Ban Güvenlik Stratejisi (Bir Profil Seçin)");

        var tierTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 224,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        tierTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tierTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tierTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        _cardTierHigh = new SecurityTierCardControl(
            tierIndex: 0,
            title: "🛡️ Maksimum Güvenlik",
            badgeText: "EN ÇOK TERCİH EDİLEN",
            badgeBg: Color.FromArgb(49, 46, 129),
            badgeFg: Color.FromArgb(165, 180, 252),
            subtitle: "Sıfır ban riski için tam izolasyon",
            features:
            [
                ("EXIF, GPS & Kamera Seri No Silinir", true),
                ("pHash Parmak İzi Kırıcı (Micro-crop)", true),
                ("AI ile Başlık & Açıklama Yenilenir", true),
                ("5 sn İnsan Davranış Gecikmesi & Taslak", true)
            ])
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4)
        };

        _cardTierStandard = new SecurityTierCardControl(
            tierIndex: 1,
            title: "⚡ Hızlı Standart",
            badgeText: "HIZLI & GÜVENLİ",
            badgeBg: Color.FromArgb(30, 58, 95),
            badgeFg: Color.FromArgb(56, 189, 248),
            subtitle: "Görsel korumalı, orijinal metinler",
            features:
            [
                ("EXIF, GPS & Kamera Seri No Silinir", true),
                ("pHash Parmak İzi Kırıcı (Micro-crop)", true),
                ("Orijinal Metinler Korunur (AI Yok)", false),
                ("4 sn İnsan Davranış Gecikmesi & Taslak", true)
            ])
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4)
        };

        _cardTierClone = new SecurityTierCardControl(
            tierIndex: 2,
            title: "📋 Ayna Klon",
            badgeText: "TAM KOPYA",
            badgeBg: Color.FromArgb(55, 65, 81),
            badgeFg: Color.FromArgb(156, 163, 175),
            subtitle: "Doğrudan veri replikasyonu (Yüksek risk)",
            features:
            [
                ("Temel EXIF Temizleme", true),
                ("Orijinal Görseller (pHash Yok)", false),
                ("Orijinal Başlık & Açıklama", false),
                ("3 sn İnsan Gecikmesi & Taslak Modu", true)
            ])
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4)
        };

        _cardTierHigh.IsSelected = true;

        tierTable.Controls.Add(_cardTierHigh, 0, 0);
        tierTable.Controls.Add(_cardTierStandard, 1, 0);
        tierTable.Controls.Add(_cardTierClone, 2, 0);

        card.Controls.Add(tierTable);
        return card;
    }

    private Control BuildSettingsAndMappingCard()
    {
        var card = CreateCardPanel("⚙️ Gelişmiş Parametreler, Fiyatlandırma & Kargo Eşleştirme");

        var mainGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
        mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));

        // Left Col: Mapping & Price
        var pnlLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 12, 0)
        };

        pnlLeft.Controls.Add(new Label
        {
            Text = "Kargo Profili ID (Varsayılan):",
            AutoSize = true,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI Semibold", 8.5F),
            Margin = new Padding(0, 2, 0, 2)
        });
        pnlLeft.Controls.Add(_txtDefaultShippingId);

        pnlLeft.Controls.Add(new Label
        {
            Text = "İade Şablonu ID (Varsayılan):",
            AutoSize = true,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI Semibold", 8.5F),
            Margin = new Padding(0, 6, 0, 2)
        });
        pnlLeft.Controls.Add(_txtDefaultReturnId);

        var pnlPricing = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0)
        };

        var pnlPrice = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 12, 0) };
        pnlPrice.Controls.Add(new Label { Text = "Fiyat Farkı (%):", AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 0, 0, 2) });
        pnlPrice.Controls.Add(_numPriceAdj);
        pnlPricing.Controls.Add(pnlPrice);

        var pnlSku = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        pnlSku.Controls.Add(new Label { Text = "SKU Ön Eki:", AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 0, 0, 2) });
        pnlSku.Controls.Add(_txtSkuPrefix);
        pnlPricing.Controls.Add(pnlSku);

        pnlLeft.Controls.Add(pnlPricing);
        mainGrid.Controls.Add(pnlLeft, 0, 0);

        // Right Col: Checkboxes & Throttle
        var pnlRight = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3
        };
        pnlRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pnlRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        pnlRight.Controls.Add(_chkStripExif, 0, 0);
        pnlRight.Controls.Add(_chkAiRewriteTitle, 1, 0);

        pnlRight.Controls.Add(_chkPermutateImageHash, 0, 1);
        pnlRight.Controls.Add(_chkAiRewriteDesc, 1, 1);

        pnlRight.Controls.Add(_chkDraftFirst, 0, 2);

        var pnlThrottle = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
        pnlThrottle.Controls.Add(new Label { Text = "İnsan Gecikmesi:", AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 4, 4, 0) });
        pnlThrottle.Controls.Add(_numThrottle);
        pnlThrottle.Controls.Add(new Label { Text = "sn", AutoSize = true, ForeColor = UiStyle.TextMuted, Font = new Font("Segoe UI", 9F), Margin = new Padding(4, 4, 0, 0) });
        pnlRight.Controls.Add(pnlThrottle, 1, 2);

        mainGrid.Controls.Add(pnlRight, 1, 0);

        card.Controls.Add(mainGrid);
        return card;
    }

    private Control BuildActionAndProgressCard()
    {
        var card = CreateCardPanel("🚀 1-Tıkla Dağıtım Motoru & İlerleme Konsolu");

        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 6)
        };
        statusFlow.Controls.Add(_lblQueueSummary);
        statusFlow.Controls.Add(_lblProgressStatus);
        card.Controls.Add(statusFlow);

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 8)
        };
        btnFlow.Controls.Add(_btnStart);
        btnFlow.Controls.Add(_btnCancel);
        btnFlow.Controls.Add(_btnRetryFailed);
        card.Controls.Add(btnFlow);

        var progressPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new Padding(0, 4, 0, 2)
        };
        progressPanel.Controls.Add(_progressBar);
        card.Controls.Add(progressPanel);

        return card;
    }

    private Control BuildTerminalCard()
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = Color.FromArgb(11, 15, 25),
            BorderColor = UiStyle.BorderColor,
            CornerRadius = 10,
            Padding = new Padding(0),
            Margin = new Padding(10, 0, 10, 8)
        };

        // Terminal Header Bar
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(18, 24, 38)
        };

        pnlHeader.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 3 macOS style dots
            using var redBrush = new SolidBrush(Color.FromArgb(239, 68, 68));
            using var yellowBrush = new SolidBrush(Color.FromArgb(245, 158, 11));
            using var greenBrush = new SolidBrush(Color.FromArgb(16, 185, 129));

            g.FillEllipse(redBrush, 12, 12, 10, 10);
            g.FillEllipse(yellowBrush, 28, 12, 10, 10);
            g.FillEllipse(greenBrush, 44, 12, 10, 10);
        };

        var lblTerminalTitle = new Label
        {
            Text = "bash — etsy-migration.log  (Canlı Dağıtım Konsolu)",
            AutoSize = true,
            Location = new Point(64, 9),
            Font = new Font("Consolas", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        pnlHeader.Controls.Add(lblTerminalTitle);

        var pnlHeaderBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 8, 0)
        };
        pnlHeaderBtns.Controls.Add(_btnCopyLog);
        pnlHeaderBtns.Controls.Add(_btnClearLog);
        pnlHeader.Controls.Add(pnlHeaderBtns);

        card.Controls.Add(_txtLog);
        card.Controls.Add(pnlHeader);

        return card;
    }

    private ModernCardPanel CreateCardPanel(string title)
    {
        var panel = new ModernCardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            CornerRadius = 10,
            Padding = new Padding(12, 10, 12, 12),
            Margin = new Padding(0, 0, 0, 10)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(129, 140, 248),
            Padding = new Padding(2, 0, 0, 4)
        };
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private void HookEvents()
    {
        _cardTierHigh.Selected += (_, _) => ApplyPreset(0);
        _cardTierStandard.Selected += (_, _) => ApplyPreset(1);
        _cardTierClone.Selected += (_, _) => ApplyPreset(2);

        _btnVerifyShop.Click += async (_, _) => await LoadAndVerifyTargetShopAsync();
        _btnOpenApiSettings.Click += (_, _) =>
        {
            using var dlg = new EtsyApiSettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadAndVerifyTargetShopAsync();
            }
        };

        _btnStart.Click += async (_, _) => await StartMigrationAsync(_queuedListings);
        _btnRetryFailed.Click += async (_, _) =>
        {
            if (_failedListings.Count > 0)
            {
                await StartMigrationAsync(_failedListings);
            }
        };
        _btnCancel.Click += (_, _) =>
        {
            _cts?.Cancel();
            AppendLog("[İPTAL TALEBİ] Kullanıcı transferi durdurdu.");
        };

        _btnCopyLog.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_txtLog.Text))
            {
                Clipboard.SetText(_txtLog.Text);
                AppendLog("[KONSOL] Konsol logları panoya kopyalandı.");
            }
        };

        _btnClearLog.Click += (_, _) =>
        {
            _txtLog.Clear();
            AppendLog("[KONSOL] Konsol temizlendi.");
        };
    }

    private void ApplyPreset(int presetIndex)
    {
        _cardTierHigh.IsSelected = presetIndex == 0;
        _cardTierStandard.IsSelected = presetIndex == 1;
        _cardTierClone.IsSelected = presetIndex == 2;

        if (presetIndex == 0) // High Security
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = true;
            _chkAiRewriteTitle.Checked = true;
            _chkAiRewriteDesc.Checked = true;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 5;
            AppendLog("[GÜVENLİK PROFİLİ] 'Maksimum Güvenlik' seçildi: EXIF temizle, pHash micro-crop, AI başlık/açıklama aktif.");
        }
        else if (presetIndex == 1) // Standard
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = true;
            _chkAiRewriteTitle.Checked = false;
            _chkAiRewriteDesc.Checked = false;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 4;
            AppendLog("[GÜVENLİK PROFİLİ] 'Hızlı Standart' seçildi: EXIF ve pHash temizleme aktif.");
        }
        else // Mirror clone
        {
            _chkStripExif.Checked = true;
            _chkPermutateImageHash.Checked = false;
            _chkAiRewriteTitle.Checked = false;
            _chkAiRewriteDesc.Checked = false;
            _chkDraftFirst.Checked = true;
            _numThrottle.Value = 3;
            AppendLog("[GÜVENLİK PROFİLİ] 'Ayna Klon' seçildi: Sadece EXIF temizleme.");
        }
    }

    private async Task LoadAndVerifyTargetShopAsync()
    {
        try
        {
            _btnVerifyShop.Enabled = false;
            _lblShopStatus.Text = "⏳ Hedef mağaza API bağlantısı test ediliyor...";
            _lblShopStatus.ForeColor = UiStyle.WarningColor;

            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasAccessToken)
            {
                _lblShopStatus.Text = "⚠️ Hedef Etsy API erişim anahtarı (Access Token) bulunamadı. Lütfen API Ayarlarını yapın.";
                _lblShopStatus.ForeColor = UiStyle.DangerColor;
                return;
            }

            var shopInfo = await _apiClient.GetCurrentShopInfoAsync();
            _targetShopId = shopInfo.ShopId;
            _targetShopName = shopInfo.ShopName;

            _lblShopStatus.Text = $"● Bağlandı: {shopInfo.ShopName} (#{shopInfo.ShopId}) — Aktarıma Hazır";
            _lblShopStatus.ForeColor = UiStyle.SuccessColor;
            AppendLog($"[HEDEF MAĞAZA DOĞRULANDI] {shopInfo.ShopName} (#{shopInfo.ShopId}) - URL: {shopInfo.ShopUrl}");
        }
        catch (Exception ex)
        {
            _lblShopStatus.Text = $"❌ Bağlantı hatası: {ex.Message}";
            _lblShopStatus.ForeColor = UiStyle.DangerColor;
            AppendLog($"[HEDEF MAĞAZA HATASI] {ex.Message}");
        }
        finally
        {
            _btnVerifyShop.Enabled = true;
        }
    }

    private async Task StartMigrationAsync(IReadOnlyList<VaultListing> listings)
    {
        if (listings.Count == 0)
        {
            MessageBox.Show(
                "Transfer edilecek ürün bulunamadı. Lütfen 'Ürün Kasası & İnceleme' sekmesinden ürünleri seçip 'Seçilenleri Transfer Et' butonuna tıklayın.",
                "Uyarı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_targetShopId <= 0)
        {
            await LoadAndVerifyTargetShopAsync();
            if (_targetShopId <= 0)
            {
                MessageBox.Show("Hedef Etsy mağazasına bağlanılamadı. Lütfen API ayarlarınızı kontrol edin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        var confirm = MessageBox.Show(
            $"{listings.Count} adet ürün '{_targetShopName}' (#{_targetShopId}) mağazasına taslak olarak aktarılacaktır.\n\n" +
            $"• Anti-Ban EXIF Temizleme: {(_chkStripExif.Checked ? "Aktif" : "Pasif")}\n" +
            $"• pHash Parmak İzi Kırıcı: {(_chkPermutateImageHash.Checked ? "Aktif" : "Pasif")}\n" +
            $"• Yapay Zeka Başlık/Açıklama: {(_chkAiRewriteTitle.Checked || _chkAiRewriteDesc.Checked ? "Aktif" : "Pasif")}\n" +
            $"• İnsan Davranışı Gecikmesi: {_numThrottle.Value} sn\n\n" +
            "Transferi başlatmak istiyor musunuz?",
            "1-Tıkla Güvenli Etsy Mağaza Göçü",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        // Prepare settings & mapping
        var antiBanSettings = new AntiBanSettings
        {
            StripExifMetadata = _chkStripExif.Checked,
            PermutateImageHash = _chkPermutateImageHash.Checked,
            RewriteTitleWithAi = _chkAiRewriteTitle.Checked,
            RewriteDescriptionWithAi = _chkAiRewriteDesc.Checked,
            CreateAsDraftFirst = _chkDraftFirst.Checked,
            ThrottleDelaySeconds = (int)_numThrottle.Value,
            PriceAdjustmentPercent = _numPriceAdj.Value,
            SkuPrefix = _txtSkuPrefix.Text.Trim()
        };

        long.TryParse(_txtDefaultShippingId.Text.Trim(), out var defShippingId);
        long.TryParse(_txtDefaultReturnId.Text.Trim(), out var defReturnId);

        var mapping = new MigrationMappingProfile
        {
            TargetShopId = _targetShopId,
            TargetShopName = _targetShopName,
            DefaultTargetShippingProfileId = defShippingId,
            DefaultTargetReturnPolicyId = defReturnId
        };

        string? aiApiKey = null;
        try
        {
            aiApiKey = AiOptimizationSettingsStore.Load()?.OpenAiApiKey;
        }
        catch { }

        _cts = new CancellationTokenSource();
        _btnStart.Enabled = false;
        _btnCancel.Enabled = true;
        _btnRetryFailed.Visible = false;
        _failedListings.Clear();

        var progress = new Progress<ShopMigrationProgress>(p =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateProgress(p));
            }
            else
            {
                UpdateProgress(p);
            }
        });

        AppendLog($"[BAŞLADI] {listings.Count} ürün için transfer işlemi başlatıldı. Hedef: {_targetShopName}");

        try
        {
            var result = await _deploymentService.DeployListingsAsync(
                listings,
                mapping,
                antiBanSettings,
                aiApiKey,
                progress,
                _cts.Token);

            AppendLog($"[BİTTİ] Toplam: {result.TotalProcessed}, Başarılı: {result.SuccessCount}, Hatalı: {result.FailedCount}");

            if (result.FailedCount > 0)
            {
                var successfulIds = new HashSet<long>(result.CreatedListingIds);
                _failedListings = listings.Where(l => !successfulIds.Contains(l.ListingId)).ToList();
                _btnRetryFailed.Visible = _failedListings.Count > 0;
                _btnRetryFailed.Text = $"🔄 Başarısız {_failedListings.Count} Ürünü Yeniden Dene";
            }
            else
            {
                MessageBox.Show(
                    $"Tebrikler! {result.SuccessCount} ürün başarıyla '{_targetShopName}' mağazasına taslak olarak aktarıldı.",
                    "Transfer Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("[DURDURULDU] Transfer işlemi kullanıcı tarafından iptal edildi.");
        }
        catch (Exception ex)
        {
            AppendLog($"[KRİTİK HATA] Transfer motoru durdu: {ex.Message}");
            MessageBox.Show($"Transfer hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnStart.Enabled = true;
            _btnCancel.Enabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void UpdateProgress(ShopMigrationProgress p)
    {
        _progressBar.Value = Math.Clamp((int)p.Percent, 0, 100);
        _lblProgressStatus.Text = $"{p.ProcessedCount}/{p.TotalCount} (Başarılı: {p.SuccessCount}, Hatalı: {p.FailedCount}) • {p.StatusMessage}";

        if (!string.IsNullOrWhiteSpace(p.ErrorDetail))
        {
            AppendLog($"❌ HATA: {p.ErrorDetail}");
        }
        else if (!string.IsNullOrWhiteSpace(p.CurrentListingTitle))
        {
            AppendLog($"➡️ {p.StatusMessage}");
        }
        else
        {
            AppendLog($"ℹ️ {p.StatusMessage}");
        }
    }

    private void AppendLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    /// <summary>
    /// Interactive, responsive Security Tier Card with glowing border and feature checklist.
    /// </summary>
    private sealed class SecurityTierCardControl : ModernCardPanel
    {
        private bool _isSelected;
        private bool _isHovered;
        private readonly Label _lblBadge = new();
        private readonly Label _lblTitle = new();
        private readonly Label _lblSubtitle = new();
        private readonly Label _lblSelectButton = new();
        private readonly FlowLayoutPanel _featuresFlow = new();

        public int TierIndex { get; }
        public event EventHandler? Selected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateVisualState();
            }
        }

        public SecurityTierCardControl(
            int tierIndex,
            string title,
            string badgeText,
            Color badgeBg,
            Color badgeFg,
            string subtitle,
            (string Text, bool Included)[] features)
        {
            TierIndex = tierIndex;
            CornerRadius = 12;
            Padding = new Padding(12);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _lblBadge.Text = badgeText.ToUpperInvariant();
            _lblBadge.AutoSize = true;
            _lblBadge.Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
            _lblBadge.BackColor = badgeBg;
            _lblBadge.ForeColor = badgeFg;
            _lblBadge.Padding = new Padding(6, 2, 6, 2);
            _lblBadge.Margin = new Padding(0, 0, 0, 4);

            _lblTitle.Text = title;
            _lblTitle.AutoSize = true;
            _lblTitle.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
            _lblTitle.ForeColor = Color.White;
            _lblTitle.Margin = new Padding(0, 2, 0, 2);

            _lblSubtitle.Text = subtitle;
            _lblSubtitle.AutoSize = true;
            _lblSubtitle.Font = new Font("Segoe UI", 8.2F);
            _lblSubtitle.ForeColor = UiStyle.TextMuted;
            _lblSubtitle.Margin = new Padding(0, 0, 0, 8);

            _featuresFlow.FlowDirection = FlowDirection.TopDown;
            _featuresFlow.WrapContents = false;
            _featuresFlow.AutoSize = true;
            _featuresFlow.Margin = new Padding(0, 0, 0, 6);

            foreach (var (featText, included) in features)
            {
                var lblFeat = new Label
                {
                    Text = $"{(included ? "✓" : "•")} {featText}",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8.2F),
                    ForeColor = included ? Color.FromArgb(226, 232, 240) : Color.FromArgb(100, 116, 139),
                    Margin = new Padding(0, 1, 0, 2)
                };
                _featuresFlow.Controls.Add(lblFeat);
            }

            _lblSelectButton.AutoSize = false;
            _lblSelectButton.Dock = DockStyle.Bottom;
            _lblSelectButton.Height = 28;
            _lblSelectButton.TextAlign = ContentAlignment.MiddleCenter;
            _lblSelectButton.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            _lblSelectButton.Cursor = Cursors.Hand;

            var contentFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };
            contentFlow.Controls.Add(_lblBadge);
            contentFlow.Controls.Add(_lblTitle);
            contentFlow.Controls.Add(_lblSubtitle);
            contentFlow.Controls.Add(_featuresFlow);

            Controls.Add(contentFlow);
            Controls.Add(_lblSelectButton);

            BindEventsRecursively(this);
            UpdateVisualState();
        }

        private void BindEventsRecursively(Control control)
        {
            control.Click += (_, _) => Selected?.Invoke(this, EventArgs.Empty);
            control.MouseEnter += (_, _) => { _isHovered = true; UpdateVisualState(); };
            control.MouseLeave += (_, _) => { _isHovered = false; UpdateVisualState(); };

            foreach (Control child in control.Controls)
            {
                BindEventsRecursively(child);
            }
        }

        private void UpdateVisualState()
        {
            if (_isSelected)
            {
                BorderColor = UiStyle.PrimaryColor;
                CardColor = Color.FromArgb(30, 41, 59);
                _lblSelectButton.Text = "✓ Seçili Profil";
                _lblSelectButton.BackColor = UiStyle.PrimaryColor;
                _lblSelectButton.ForeColor = Color.White;
            }
            else
            {
                BorderColor = _isHovered ? Color.FromArgb(99, 102, 241) : UiStyle.BorderColor;
                CardColor = _isHovered ? Color.FromArgb(24, 32, 47) : Color.FromArgb(18, 24, 38);
                _lblSelectButton.Text = "Bu Profili Seç";
                _lblSelectButton.BackColor = Color.FromArgb(33, 45, 66);
                _lblSelectButton.ForeColor = UiStyle.TextMuted;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundedRectanglePath(rect, CornerRadius);

            using var fillBrush = new SolidBrush(CardColor);
            g.FillPath(fillBrush, path);

            float penWidth = _isSelected ? 2.2f : 1.2f;
            using var borderPen = new Pen(BorderColor, penWidth);
            g.DrawPath(borderPen, path);

            base.OnPaint(e);
        }
    }
}
