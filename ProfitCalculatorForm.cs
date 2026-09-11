namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Profitability;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class ProfitCalculatorForm : Form
{
    private readonly ProfitabilityCalculator _calculator = new();
    private readonly ExchangeRateService _exchangeRateService = new();
    private decimal _liveExchangeRate = 48.26m;

    // Header Controls
    private readonly Label _headerTitle = new();
    private readonly Label _headerSubtitle = new();
    private readonly Label _rateBadge = new();
    private readonly Label _healthBadge = new();

    // Input Controls
    private readonly TextBox _productNameTextBox = new();
    private readonly NumericUpDown _salePriceInput = CreateMoneyInput(183.00m);
    private readonly Label _salePriceTryLabel = new();

    private readonly NumericUpDown _buyerShippingInput = CreateMoneyInput(0m);
    private readonly NumericUpDown _giftWrapInput = CreateMoneyInput(0m);

    private readonly NumericUpDown _materialCostInput = CreateMoneyInput(34.00m);
    private readonly Label _materialCostTryLabel = new();

    private readonly NumericUpDown _sellerShippingCostInput = CreateMoneyInput(25.00m);
    private readonly Label _sellerShippingCostTryLabel = new();

    private readonly NumericUpDown _packagingCostInput = CreateMoneyInput(0.00m);
    private readonly NumericUpDown _adCostInput = CreateMoneyInput(0.00m);

    private readonly ComboBox _countryComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _offsiteAdsComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernCheckBox _currencyConversionCheckBox = new() { Text = "Para birimi dönüştürme (%2.5)", AutoSize = true };
    private readonly NumericUpDown _targetMarginInput = CreatePercentInput(30.0m);

    // KPI Cards Controls
    private readonly Label _netProfitUsdLabel = new();
    private readonly Label _netProfitTryLabel = new();
    private readonly Label _marginPercentLabel = new();
    private readonly Label _roiLabel = new();
    private readonly Label _totalExpenseUsdLabel = new();
    private readonly Label _totalExpenseTryLabel = new();

    // Visual Split Bar
    private readonly Panel _splitBarPanel = new();
    private double _ratioNetProfit = 0.53;
    private double _ratioCosts = 0.32;
    private double _ratioEtsyFees = 0.15;
    private readonly Label _splitBarLegendLabel = new();

    // Structured Fee Breakdown DataGridView
    private readonly DataGridView _feesGrid = new();

    // AI Strategic Recommendations
    private readonly Label _breakEvenLabel = new();
    private readonly Label _optimalPriceLabel = new();
    private readonly Label _offsiteComparisonLabel = new();
    private readonly Panel _recommendationsContainer = new();
    private ModernScrollPanel? _recScroll;

    public ProfitCalculatorForm(ProductCandidate? product = null)
    {
        InitializeComponentCustom();
        PopulateDropdowns();
        if (product is not null) LoadProduct(product);
        
        // Fetch Live Exchange Rate in background
        _ = FetchLiveRateAsync();

        Calculate();
    }

    private async Task FetchLiveRateAsync()
    {
        try
        {
            var rate = await _exchangeRateService.GetLiveUsdTryRateAsync();
            if (rate > 25m)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    _liveExchangeRate = rate;
                    _rateBadge.Text = $"💱 1 USD = ₺{_liveExchangeRate:N2}";
                    Calculate();
                });
            }
        }
        catch
        {
            // Keep fallback 48.26
        }
    }

    private void InitializeComponentCustom()
    {
        Text = "Etsy Komisyon ve Net Kâr Marjı Simülatörü";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiStyle.BackgroundColor;
        Font = UiStyle.BaseFont;
        ForeColor = UiStyle.TextDark;
        AutoScroll = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(20),
            BackColor = UiStyle.BackgroundColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main Body
        Controls.Add(root);

        // 1. Header
        root.Controls.Add(BuildHeaderPanel(), 0, 0);

        // 2. Main Body: Left Inputs (42%) and Right Results (58%)
        var mainContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 10, 0, 0),
        };
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        mainContent.Controls.Add(BuildScrollableInputCard(), 0, 0);
        mainContent.Controls.Add(BuildOutputAnalyticsCard(), 1, 0);

        root.Controls.Add(mainContent, 0, 1);
    }

    private Control BuildHeaderPanel()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Left Title & Subtitle
        var titleBox = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        _headerTitle.Text = "📊 Etsy Komisyon & Net Kâr Marjı Simülatörü";
        _headerTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _headerTitle.ForeColor = UiStyle.TextDark;
        _headerTitle.AutoSize = true;

        _headerSubtitle.Text = "Sipariş başı net hakediş, komisyonlar, ürün maliyetleri ve kuruşu kuruşuna canlı TL kâr bilançosu";
        _headerSubtitle.Font = new Font("Segoe UI", 9F);
        _headerSubtitle.ForeColor = UiStyle.TextMuted;
        _headerSubtitle.AutoSize = true;
        _headerSubtitle.Margin = new Padding(2, 4, 0, 0);

        titleBox.Controls.Add(_headerTitle);
        titleBox.Controls.Add(_headerSubtitle);
        header.Controls.Add(titleBox, 0, 0);

        // Right Badges (Live FX & Health Pill)
        var rightBadges = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
        };

        _rateBadge.AutoSize = true;
        _rateBadge.Text = $"💱 1 USD = ₺{_liveExchangeRate:N2}";
        _rateBadge.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _rateBadge.ForeColor = Color.FromArgb(56, 189, 248); // Sky 400
        _rateBadge.BackColor = Color.FromArgb(30, 41, 59);
        _rateBadge.Padding = new Padding(12, 8, 12, 8);
        _rateBadge.Margin = new Padding(0, 4, 10, 0);
        _rateBadge.Cursor = Cursors.Hand;
        var tt = new ToolTip();
        tt.SetToolTip(_rateBadge, "TCMB & Piyasa canlı kuru. Kuru manuel düzenlemek için tıklayın.");
        _rateBadge.Click += (_, _) => ShowRateEditDialog();
        rightBadges.Controls.Add(_rateBadge);

        _healthBadge.AutoSize = true;
        _healthBadge.Text = "Durum: Hesaplanıyor";
        _healthBadge.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _healthBadge.ForeColor = Color.White;
        _healthBadge.BackColor = UiStyle.SuccessColor;
        _healthBadge.Padding = new Padding(14, 8, 14, 8);
        _healthBadge.Margin = new Padding(0, 4, 0, 0);
        rightBadges.Controls.Add(_healthBadge);

        header.Controls.Add(rightBadges, 1, 0);
        return header;
    }

    private Control BuildScrollableInputCard()
    {
        var outerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 10, 0),
        };
        outerPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(UiStyle.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, outerPanel.Width - 1, outerPanel.Height - 1);
        };

        var scrollContainer = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460,
        };

        // Section 1: Gelirler
        flow.Controls.Add(CreateSectionHeader("💰 Satış ve Müşteri Gelirleri"));
        flow.Controls.Add(CreateInputFieldWithTry("Ürün Adı / Başlık", _productNameTextBox, null));
        flow.Controls.Add(CreateInputFieldWithTry("Satış Fiyatı ($ USD)", _salePriceInput, _salePriceTryLabel));
        flow.Controls.Add(CreateInputFieldWithTry("Müşteri Kargo Ücreti ($)", _buyerShippingInput, null));
        flow.Controls.Add(CreateInputFieldWithTry("Hediye Paketi Ücreti ($)", _giftWrapInput, null));

        // Section 2: Maliyetler
        flow.Controls.Add(CreateSectionHeader("📦 Ürün, Kargo & Reklam Maliyetleri"));
        flow.Controls.Add(CreateInputFieldWithTry("Ürün Üretim / Hammadde ($)", _materialCostInput, _materialCostTryLabel));
        flow.Controls.Add(CreateInputFieldWithTry("Satıcı Kargo Maliyeti ($)", _sellerShippingCostInput, _sellerShippingCostTryLabel));
        flow.Controls.Add(CreateInputFieldWithTry("Ambalaj & Paketleme Maliyeti ($)", _packagingCostInput, null));
        flow.Controls.Add(CreateInputFieldWithTry("Etsy İçi Reklam / İlan Başı ($)", _adCostInput, null));

        // Section 3: Mağaza & Vergi Parametreleri
        flow.Controls.Add(CreateSectionHeader("⚙️ Mağaza, Komisyon & Kur Ayarları"));
        flow.Controls.Add(CreateInputFieldWithTry("Satıcı Ülkesi", _countryComboBox, null));
        flow.Controls.Add(CreateInputFieldWithTry("Offsite Ads (Dış Reklam)", _offsiteAdsComboBox, null));

        // Currency Conversion Checkbox
        _currencyConversionCheckBox.Font = new Font("Segoe UI", 9F);
        _currencyConversionCheckBox.ForeColor = UiStyle.TextDark;
        _currencyConversionCheckBox.Margin = new Padding(4, 6, 4, 8);
        flow.Controls.Add(_currencyConversionCheckBox);

        // Target Margin Stepper
        flow.Controls.Add(CreateInputFieldWithTry("Hedef Kâr Marjı (%)", _targetMarginInput, null));

        scrollContainer.SetContent(flow);
        outerPanel.Controls.Add(scrollContainer);

        return outerPanel;
    }

    private Control BuildOutputAnalyticsCard()
    {
        var outerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(16),
            Margin = new Padding(10, 0, 0, 0),
        };
        outerPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(UiStyle.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, outerPanel.Width - 1, outerPanel.Height - 1);
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // 1. Top 3 KPI Cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // 2. Visual Revenue Split Bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));  // 3. Structured Fee Table
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));  // 4. Strategic AI & Break-Even Card

        // 1. Top 3 KPI Cards
        mainLayout.Controls.Add(BuildKpiRow(), 0, 0);

        // 2. Visual Revenue Split Bar
        mainLayout.Controls.Add(BuildRevenueSplitPanel(), 0, 1);

        // 3. Structured Fee Table
        mainLayout.Controls.Add(BuildFeeTableGroup(), 0, 2);

        // 4. Strategic AI & Break-Even Card
        mainLayout.Controls.Add(BuildStrategicRecommendationCard(), 0, 3);

        outerPanel.Controls.Add(mainLayout);
        return outerPanel;
    }

    private Control BuildKpiRow()
    {
        var kpiRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 8),
        };
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        // Card 1: NET KÂR
        kpiRow.Controls.Add(CreateKpiCard(
            "💵 NET KÂR (Cepte Kalan)",
            _netProfitUsdLabel,
            _netProfitTryLabel,
            UiStyle.SuccessColor), 0, 0);

        // Card 2: KÂR MARJI & ROI
        kpiRow.Controls.Add(CreateKpiCard(
            "📈 KÂR MARJI (%)",
            _marginPercentLabel,
            _roiLabel,
            Color.FromArgb(6, 182, 212)), 1, 0); // Cyan

        // Card 3: TOPLAM GİDER
        kpiRow.Controls.Add(CreateKpiCard(
            "💸 TOPLAM GİDERLER",
            _totalExpenseUsdLabel,
            _totalExpenseTryLabel,
            Color.FromArgb(244, 63, 94)), 2, 0); // Rose Red

        return kpiRow;
    }

    private Control CreateKpiCard(string title, Label mainVal, Label subVal, Color accentColor)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42), // Slate 950
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(3),
        };
        card.Paint += (s, e) =>
        {
            using var borderPen = new Pen(UiStyle.BorderColor, 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);
            using var accentBrush = new SolidBrush(accentColor);
            e.Graphics.FillRectangle(accentBrush, 0, 0, 4, card.Height); // Left colored accent stripe
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.TextMuted,
            Dock = DockStyle.Top,
            Height = 18,
        };

        mainVal.Dock = DockStyle.Top;
        mainVal.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        mainVal.ForeColor = accentColor;
        mainVal.Height = 32;
        mainVal.Text = "$0.00";

        subVal.Dock = DockStyle.Top;
        subVal.Font = new Font("Segoe UI Semibold", 9.5F);
        subVal.ForeColor = Color.FromArgb(251, 191, 36); // Amber Gold
        subVal.Height = 20;
        subVal.Text = "≈ ₺0,00";

        card.Controls.Add(subVal);
        card.Controls.Add(mainVal);
        card.Controls.Add(lblTitle);

        return card;
    }

    private Control BuildRevenueSplitPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 6),
        };

        _splitBarPanel.Dock = DockStyle.Top;
        _splitBarPanel.Height = 16;
        _splitBarPanel.Paint += PaintSplitBar;

        _splitBarLegendLabel.Dock = DockStyle.Top;
        _splitBarLegendLabel.Height = 24;
        _splitBarLegendLabel.Font = new Font("Segoe UI", 8.5F);
        _splitBarLegendLabel.ForeColor = UiStyle.TextMuted;
        _splitBarLegendLabel.TextAlign = ContentAlignment.MiddleLeft;
        _splitBarLegendLabel.Text = "🟩 Net Kâr: %52.9   |   🟧 Ürün & Kargo Maliyeti: %33.6   |   🟪 Etsy Kesintileri: %13.5";

        panel.Controls.Add(_splitBarLegendLabel);
        panel.Controls.Add(_splitBarPanel);

        return panel;
    }

    private void PaintSplitBar(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = _splitBarPanel.Width;
        int h = _splitBarPanel.Height;
        if (w <= 10 || h <= 4) return;

        double total = Math.Max(0.001, _ratioNetProfit + _ratioCosts + _ratioEtsyFees);
        int wProfit = (int)(w * (_ratioNetProfit / total));
        int wCosts = (int)(w * (_ratioCosts / total));
        int wFees = w - wProfit - wCosts;

        int curX = 0;
        // 1. Net Profit Segment (Emerald)
        if (wProfit > 0)
        {
            using var b = new SolidBrush(Color.FromArgb(16, 185, 129));
            g.FillRectangle(b, curX, 0, wProfit, h);
            curX += wProfit;
        }

        // 2. Direct Costs Segment (Amber/Orange)
        if (wCosts > 0)
        {
            using var b = new SolidBrush(Color.FromArgb(245, 158, 11));
            g.FillRectangle(b, curX, 0, wCosts, h);
            curX += wCosts;
        }

        // 3. Etsy Fees Segment (Violet/Purple)
        if (wFees > 0)
        {
            using var b = new SolidBrush(Color.FromArgb(139, 92, 246));
            g.FillRectangle(b, curX, 0, wFees, h);
        }

        // Draw border
        using var pen = new Pen(UiStyle.BorderColor, 1);
        g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
    }

    private Control BuildFeeTableGroup()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "🧾 Kalem Kalem Etsy Kesintileri ve Masraflar",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 4, 0, 4),
        };

        _feesGrid.Dock = DockStyle.Fill;
        _feesGrid.BackgroundColor = UiStyle.CardBackground;
        _feesGrid.BorderStyle = BorderStyle.None;
        _feesGrid.RowHeadersVisible = false;
        _feesGrid.AllowUserToAddRows = false;
        _feesGrid.AllowUserToResizeRows = false;
        _feesGrid.ReadOnly = true;
        _feesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _feesGrid.EnableHeadersVisualStyles = false;
        _feesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _feesGrid.ColumnHeadersHeight = 28;

        _feesGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
        _feesGrid.ColumnHeadersDefaultCellStyle.ForeColor = UiStyle.TextMuted;
        _feesGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
        _feesGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(15, 23, 42);

        _feesGrid.DefaultCellStyle.BackColor = UiStyle.CardBackground;
        _feesGrid.DefaultCellStyle.ForeColor = UiStyle.TextDark;
        _feesGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _feesGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 65, 85);
        _feesGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        _feesGrid.GridColor = Color.FromArgb(30, 41, 59);

        _feesGrid.Columns.Add("Kalem", "Kesinti / Masraf Kalemi");
        _feesGrid.Columns.Add("Oran", "Kural / Oran");
        _feesGrid.Columns.Add("TutarUsd", "Tutar ($ USD)");
        _feesGrid.Columns.Add("TutarTry", "Canlı Karşılık (₺ TL)");

        _feesGrid.Columns[0].FillWeight = 42;
        _feesGrid.Columns[1].FillWeight = 26;
        _feesGrid.Columns[2].FillWeight = 16;
        _feesGrid.Columns[3].FillWeight = 16;
        _feesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _feesGrid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _feesGrid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        group.Controls.Add(_feesGrid);
        return group;
    }

    private Control BuildStrategicRecommendationCard()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "🎯 Fiyatlandırma Hedefleri & AI Stratejik Tavsiyeler",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 4, 0, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(8, 4, 8, 4),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Break-Even
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Target Price
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Offsite Comparison
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Recommendations List

        _breakEvenLabel.Dock = DockStyle.Fill;
        _breakEvenLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _breakEvenLabel.ForeColor = Color.FromArgb(248, 113, 113); // Red
        layout.Controls.Add(_breakEvenLabel, 0, 0);

        _optimalPriceLabel.Dock = DockStyle.Fill;
        _optimalPriceLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _optimalPriceLabel.ForeColor = Color.FromArgb(56, 189, 248); // Sky
        layout.Controls.Add(_optimalPriceLabel, 0, 1);

        _offsiteComparisonLabel.Dock = DockStyle.Fill;
        _offsiteComparisonLabel.Font = new Font("Segoe UI", 9F);
        _offsiteComparisonLabel.ForeColor = UiStyle.TextMuted;
        layout.Controls.Add(_offsiteComparisonLabel, 0, 2);

        _recScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 2, 0)
        };
        _recommendationsContainer.Dock = DockStyle.Top;
        _recommendationsContainer.AutoSize = true;
        _recommendationsContainer.AutoScroll = false;
        _recommendationsContainer.BackColor = Color.FromArgb(15, 23, 42);
        _recommendationsContainer.Padding = new Padding(8);
        _recScroll.SetContent(_recommendationsContainer);
        layout.Controls.Add(_recScroll, 0, 3);

        group.Controls.Add(layout);
        return group;
    }

    private Control CreateInputFieldWithTry(string labelText, Control inputControl, Label? tryLabel)
    {
        var rowPanel = new Panel
        {
            Width = 440,
            Height = tryLabel != null ? 56 : 38,
            Margin = new Padding(0, 2, 0, 4),
        };

        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 9F),
            ForeColor = UiStyle.TextDark,
            Location = new Point(0, 6),
            Size = new Size(185, 24),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        rowPanel.Controls.Add(lbl);

        inputControl.Location = new Point(190, 4);
        inputControl.Width = 240;
        inputControl.Font = new Font("Segoe UI", 9.5F);
        rowPanel.Controls.Add(inputControl);

        if (tryLabel != null)
        {
            tryLabel.Location = new Point(192, 32);
            tryLabel.Size = new Size(238, 20);
            tryLabel.Font = new Font("Segoe UI Semibold", 8.5F);
            tryLabel.ForeColor = Color.FromArgb(251, 191, 36); // Gold
            tryLabel.Text = "≈ ₺0,00";
            rowPanel.Controls.Add(tryLabel);
        }

        return rowPanel;
    }

    private static Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(129, 140, 248), // Indigo 400
            Width = 440,
            Height = 28,
            Margin = new Padding(0, 10, 0, 4),
            TextAlign = ContentAlignment.BottomLeft,
        };
    }

    private void PopulateDropdowns()
    {
        _countryComboBox.Items.AddRange([
            "Türkiye (%1.67 Yasal Düzenleme)",
            "ABD (%0 Yasal Düzenleme)",
            "İngiltere (%0.48 Yasal Düzenleme)",
            "Fransa (%1.14 Yasal Düzenleme)",
            "İspanya (%0.88 Yasal Düzenleme)",
            "İtalya (%0.80 Yasal Düzenleme)",
        ]);
        _countryComboBox.SelectedIndex = 0;

        _offsiteAdsComboBox.Items.AddRange([
            "Kapalı (%0)",
            "Opsiyonel (%15 - Satış <$10k)",
            "Zorunlu (%12 - Satış >$10k)",
        ]);
        _offsiteAdsComboBox.SelectedIndex = 0;

        // Auto-recalculate triggers
        _salePriceInput.ValueChanged += (_, _) => Calculate();
        _buyerShippingInput.ValueChanged += (_, _) => Calculate();
        _giftWrapInput.ValueChanged += (_, _) => Calculate();
        _materialCostInput.ValueChanged += (_, _) => Calculate();
        _sellerShippingCostInput.ValueChanged += (_, _) => Calculate();
        _packagingCostInput.ValueChanged += (_, _) => Calculate();
        _adCostInput.ValueChanged += (_, _) => Calculate();
        _targetMarginInput.ValueChanged += (_, _) => Calculate();
        _countryComboBox.SelectedIndexChanged += (_, _) => Calculate();
        _offsiteAdsComboBox.SelectedIndexChanged += (_, _) => Calculate();
        _currencyConversionCheckBox.CheckedChanged += (_, _) => Calculate();
    }

    private void LoadProduct(ProductCandidate? product)
    {
        if (product is null) return;

        _productNameTextBox.Text = product.Name;
        _salePriceInput.Value = Math.Clamp((decimal)product.MaxPrice, _salePriceInput.Minimum, _salePriceInput.Maximum);

        if (product.MaxPrice >= 100)
        {
            _materialCostInput.Value = 25m;
            _sellerShippingCostInput.Value = 18m;
            _packagingCostInput.Value = 5m;
        }
        else if (product.MaxPrice >= 50)
        {
            _materialCostInput.Value = 12m;
            _sellerShippingCostInput.Value = 10m;
            _packagingCostInput.Value = 3m;
        }
        else
        {
            _materialCostInput.Value = 5m;
            _sellerShippingCostInput.Value = 6m;
            _packagingCostInput.Value = 2m;
        }
    }

    private void Calculate()
    {
        var input = new ProfitabilityInput(
            (double)_salePriceInput.Value,
            (double)_buyerShippingInput.Value,
            (double)_giftWrapInput.Value,
            (double)_materialCostInput.Value,
            (double)_sellerShippingCostInput.Value,
            (double)_packagingCostInput.Value,
            (double)_adCostInput.Value,
            _countryComboBox.SelectedItem?.ToString() ?? "Türkiye",
            (OffsiteAdsMode)_offsiteAdsComboBox.SelectedIndex,
            _currencyConversionCheckBox.Checked,
            (double)_targetMarginInput.Value);

        var result = _calculator.Calculate(input);

        decimal rate = _liveExchangeRate > 0 ? _liveExchangeRate : 48.26m;

        // 1. Update Input Live TRY Micro-Badges
        _salePriceTryLabel.Text = $"≈ ₺{_salePriceInput.Value * rate:N2} TL";
        _materialCostTryLabel.Text = $"≈ ₺{_materialCostInput.Value * rate:N2} TL";
        _sellerShippingCostTryLabel.Text = $"≈ ₺{_sellerShippingCostInput.Value * rate:N2} TL";

        // 2. Update Top 3 KPI Cards
        decimal netProfitUsd = (decimal)result.NetProfitUsd;
        decimal netProfitTry = netProfitUsd * rate;
        _netProfitUsdLabel.Text = $"${netProfitUsd:N2}";
        _netProfitUsdLabel.ForeColor = netProfitUsd >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _netProfitTryLabel.Text = $"≈ ₺{netProfitTry:N2} TL";
        _netProfitTryLabel.ForeColor = netProfitUsd >= 0 ? Color.FromArgb(251, 191, 36) : UiStyle.DangerColor;

        _marginPercentLabel.Text = $"%{result.ProfitMarginPercent:F1}";
        _marginPercentLabel.ForeColor = result.ProfitMarginPercent >= 25.0 ? UiStyle.SuccessColor : result.ProfitMarginPercent >= 15.0 ? UiStyle.AccentColor : UiStyle.DangerColor;
        _roiLabel.Text = $"ROI: %{result.ReturnOnInvestmentPercent:F1} Getiri";

        decimal totalExpensesUsd = (decimal)result.TotalExpensesUsd;
        decimal totalExpensesTry = totalExpensesUsd * rate;
        _totalExpenseUsdLabel.Text = $"-${totalExpensesUsd:N2}";
        _totalExpenseTryLabel.Text = $"≈ -₺{totalExpensesTry:N2} TL";

        // 3. Update Health Badge
        _healthBadge.Text = $"🏆 Durum: {result.HealthRating}";
        _healthBadge.BackColor = result.HealthRating switch
        {
            "Mükemmel" => UiStyle.SuccessColor,
            "İyi (Sağlıklı)" => Color.FromArgb(59, 130, 246), // Blue
            "Düşük Kâr" => UiStyle.WarningColor,
            _ => UiStyle.DangerColor,
        };

        // 4. Update Revenue Split Bar
        double rev = Math.Max(0.01, result.TotalRevenueUsd);
        _ratioNetProfit = Math.Max(0, result.NetProfitUsd) / rev;
        double directCosts = (double)(_materialCostInput.Value + _sellerShippingCostInput.Value + _packagingCostInput.Value + _adCostInput.Value);
        _ratioCosts = directCosts / rev;
        _ratioEtsyFees = result.Fees.TotalEtsyFeesUsd / rev;

        _splitBarLegendLabel.Text = $"🟩 Net Kâr: %{_ratioNetProfit * 100:F1}   |   🟧 Ürün & Kargo Maliyeti: %{_ratioCosts * 100:F1}   |   🟪 Etsy Kesintileri: %{_ratioEtsyFees * 100:F1}";
        _splitBarPanel.Invalidate();

        // 5. Update Structured Fee Table
        _feesGrid.Rows.Clear();
        AddFeeRow("İlan Ücreti (Listing Fee)", "0.20 USD / İlan", (decimal)result.Fees.ListingFeeUsd, rate);
        AddFeeRow("İşlem Komisyonu (Transaction)", "%6.5 (Ürün + Kargo)", (decimal)result.Fees.TransactionFeeUsd, rate);
        AddFeeRow("Ödeme İşleme (Payment Processing)", "%6.5 + 3 TL", (decimal)result.Fees.PaymentProcessingFeeUsd, rate);

        if (result.Fees.RegulatoryOperatingFeeUsd > 0)
        {
            AddFeeRow("Yasal Düzenleme (Regulatory Operating)", "%1.67 (TR Mevzuatı)", (decimal)result.Fees.RegulatoryOperatingFeeUsd, rate);
        }
        if (result.Fees.OffsiteAdsFeeUsd > 0)
        {
            AddFeeRow("Dış Reklam (Offsite Ads)", input.OffsiteAds == OffsiteAdsMode.Mandatory12Percent ? "%12 (Zorunlu)" : "%15 (Opsiyonel)", (decimal)result.Fees.OffsiteAdsFeeUsd, rate);
        }
        if (result.Fees.CurrencyConversionFeeUsd > 0)
        {
            AddFeeRow("Para Birimi Dönüştürme", "%2.5 (USD -> TRY)", (decimal)result.Fees.CurrencyConversionFeeUsd, rate);
        }

        // Summary Total Row
        int totalRowIdx = _feesGrid.Rows.Add(
            "👉 TOPLAM ETSY KESİNTİLERİ",
            $"Toplam Komisyon Payı: %{_ratioEtsyFees * 100:F1}",
            $"${result.Fees.TotalEtsyFeesUsd:N2}",
            $"₺{(decimal)result.Fees.TotalEtsyFeesUsd * rate:N2}"
        );
        _feesGrid.Rows[totalRowIdx].DefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _feesGrid.Rows[totalRowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(167, 139, 250); // Light Violet

        // 6. Strategic AI Recommendations
        decimal breakEvenUsd = (decimal)result.BreakEvenItemPriceUsd;
        decimal breakEvenTry = breakEvenUsd * rate;
        _breakEvenLabel.Text = $"🛑 Başabaş Satış Fiyatı (Break-Even): ${breakEvenUsd:N2}  (≈ ₺{breakEvenTry:N2} TL) — Bunun altı doğrudan zarardır";

        decimal optimalUsd = (decimal)result.RecommendedOptimalPriceUsd;
        decimal optimalTry = optimalUsd * rate;
        _optimalPriceLabel.Text = $"🎯 Hedef %{_targetMarginInput.Value} Kâr Marjı İçin İdeal Satış Fiyatı: ${optimalUsd:N2}  (≈ ₺{optimalTry:N2} TL)";

        decimal offsiteUsd = (decimal)result.OffsiteAdsComparisonProfitUsd;
        decimal offsiteTry = offsiteUsd * rate;
        _offsiteComparisonLabel.Text = input.OffsiteAds == OffsiteAdsMode.Disabled
            ? $"💡 Offsite Ads aktif olsaydı tahmini net kâr: ${offsiteUsd:N2} (≈ ₺{offsiteTry:N2} TL)"
            : $"💡 Offsite Ads olmasaydı tahmini net kâr: ${offsiteUsd:N2} (≈ ₺{offsiteTry:N2} TL)";

        // Recommendations List
        _recommendationsContainer.Controls.Clear();
        foreach (var rec in result.Recommendations)
        {
            var lblRec = new Label
            {
                Text = $"• {rec}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiStyle.TextDark,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 3, 0, 3),
            };
            _recommendationsContainer.Controls.Add(lblRec);
        }
        _recScroll?.RecalculateScroll();
    }

    private void AddFeeRow(string name, string rule, decimal usd, decimal rate)
    {
        _feesGrid.Rows.Add(name, rule, $"${usd:N2}", $"₺{usd * rate:N2}");
    }

    private void ShowRateEditDialog()
    {
        using var dialog = new Form
        {
            Text = "💱 Canlı Dolar/TL Kurunu Belirle",
            Size = new Size(360, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UiStyle.CardBackground,
            ForeColor = UiStyle.TextDark,
        };

        var lbl = new Label
        {
            Text = "Simülatörde kullanılacak 1 USD = TL kurunu girin:",
            Location = new Point(20, 20),
            Size = new Size(300, 30),
            Font = new Font("Segoe UI", 9.5F),
        };
        dialog.Controls.Add(lbl);

        var num = new NumericUpDown
        {
            Location = new Point(20, 55),
            Size = new Size(200, 30),
            DecimalPlaces = 2,
            Minimum = 10m,
            Maximum = 500m,
            Value = _liveExchangeRate,
            Font = new Font("Segoe UI Semibold", 11F),
        };
        dialog.Controls.Add(num);

        var btnOk = new Button
        {
            Text = "Uygula",
            DialogResult = DialogResult.OK,
            Location = new Point(20, 105),
            Size = new Size(100, 35),
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        dialog.Controls.Add(btnOk);

        var btnCancel = new Button
        {
            Text = "İptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(130, 105),
            Size = new Size(90, 35),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        dialog.Controls.Add(btnCancel);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _liveExchangeRate = num.Value;
            _rateBadge.Text = $"💱 1 USD = ₺{_liveExchangeRate:N2}";
            Calculate();
        }
    }

    private static NumericUpDown CreateMoneyInput(decimal defaultValue) => new()
    {
        Minimum = 0,
        Maximum = 100000,
        DecimalPlaces = 2,
        Increment = 0.50m,
        Value = defaultValue,
        BackColor = Color.FromArgb(15, 23, 42),
        ForeColor = Color.White,
    };

    private static NumericUpDown CreatePercentInput(decimal defaultValue = 30.0m) => new()
    {
        Minimum = 1,
        Maximum = 90,
        DecimalPlaces = 1,
        Increment = 1m,
        Value = defaultValue,
        BackColor = Color.FromArgb(15, 23, 42),
        ForeColor = Color.White,
    };
}
