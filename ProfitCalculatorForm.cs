namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.Profitability;
using SimilarProductsWinForms.Services;

internal sealed class ProfitCalculatorForm : Form
{
    private readonly ProfitabilityCalculator _calculator = new();
    private readonly TextBox _productNameTextBox = new();
    private readonly NumericUpDown _salePriceInput = CreateMoneyInput(49.90m);
    private readonly NumericUpDown _buyerShippingInput = CreateMoneyInput(0m);
    private readonly NumericUpDown _giftWrapInput = CreateMoneyInput(0m);
    private readonly NumericUpDown _materialCostInput = CreateMoneyInput(12.00m);
    private readonly NumericUpDown _sellerShippingCostInput = CreateMoneyInput(8.50m);
    private readonly NumericUpDown _packagingCostInput = CreateMoneyInput(2.50m);
    private readonly NumericUpDown _adCostInput = CreateMoneyInput(2.00m);
    private readonly ComboBox _countryComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _offsiteAdsComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _currencyConversionCheckBox = new() { Text = "Para birimi dönüştürme (%2.5)", AutoSize = true };
    private readonly NumericUpDown _targetMarginInput = CreatePercentInput(30.0m);

    private readonly Label _netProfitBadge = new();
    private readonly Label _profitMarginBadge = new();
    private readonly Label _healthRatingBadge = new();
    private readonly Label _breakEvenLabel = new();
    private readonly Label _optimalPriceLabel = new();
    private readonly Label _offsiteComparisonLabel = new();
    private readonly ListBox _feeBreakdownListBox = new();
    private readonly ListBox _recommendationsListBox = new();

    public ProfitCalculatorForm(ProductCandidate? product = null)
    {
        BuildLayout();
        PopulateDropdowns();
        if (product is not null) LoadProduct(product);
        Calculate();
    }

    private void BuildLayout()
    {
        Text = "Etsy Komisyon ve Net Kâr Marjı Simülatörü (Profitability Calculator)";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Komisyon ve Net Kâr Marjı Simülatörü",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _healthRatingBadge.Dock = DockStyle.Fill;
        _healthRatingBadge.Font = new Font("Segoe UI Semibold", 11F);
        _healthRatingBadge.TextAlign = ContentAlignment.MiddleCenter;
        _healthRatingBadge.ForeColor = Color.White;
        _healthRatingBadge.BackColor = UiStyle.SecondaryColor;
        _healthRatingBadge.Text = "Durum: Hesaplanıyor";
        header.Controls.Add(_healthRatingBadge, 1, 0);

        root.Controls.Add(header, 0, 0);

        // Main Content (Left Inputs 42%, Right Calculation Output 58%)
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        content.Controls.Add(BuildInputPanel(), 0, 0);
        content.Controls.Add(BuildOutputPanel(), 1, 0);
        root.Controls.Add(content, 0, 1);

        // Bottom Bar
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        bottomBar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        var calcBtn = UiStyle.CreateButton("Yeniden Hesapla");
        calcBtn.Click += (_, _) => Calculate();
        bottomBar.Controls.Add(calcBtn, 1, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        bottomBar.Controls.Add(closeBtn, 2, 0);

        root.Controls.Add(bottomBar, 0, 2);

        UiStyle.AttachSidebarNav(this, "profit");
    }

    private Control BuildInputPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Satış ve Maliyet Parametreleri",
            Font = new Font("Segoe UI Semibold", 10F),
        };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 12, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 12; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        AddControlRow(table, 0, "Ürün Adı / Başlık", _productNameTextBox);
        AddControlRow(table, 1, "Satış Fiyatı ($ USD)", _salePriceInput);
        AddControlRow(table, 2, "Müşteri Kargo Ücreti ($)", _buyerShippingInput);
        AddControlRow(table, 3, "Hediye Paketi Ücreti ($)", _giftWrapInput);
        AddControlRow(table, 4, "Ürün Üretim/Maliyet ($)", _materialCostInput);
        AddControlRow(table, 5, "Satıcı Kargo Maliyeti ($)", _sellerShippingCostInput);
        AddControlRow(table, 6, "Ambalaj Maliyeti ($)", _packagingCostInput);
        AddControlRow(table, 7, "Etsy İçi Reklam / İlan ($)", _adCostInput);
        AddControlRow(table, 8, "Satıcı Ülkesi", _countryComboBox);
        AddControlRow(table, 9, "Offsite Ads (Dış Reklam)", _offsiteAdsComboBox);
        AddControlRow(table, 10, "Para Birimi Dönüşümü", _currencyConversionCheckBox);
        AddControlRow(table, 11, "Hedef Kâr Marjı (%)", _targetMarginInput);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildOutputPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12, 0, 0, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // KPI Badges
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));   // Fee Breakdown List
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));   // Recommendations & Pricing Targets

        // KPI Badges Row
        var kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddKpiCard(kpiTable, 0, "NET KÂR", _netProfitBadge);
        AddKpiCard(kpiTable, 1, "KÂR MARJI (%)", _profitMarginBadge);
        panel.Controls.Add(kpiTable, 0, 0);

        // Fee Breakdown Group
        var feeGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Kalem Kalem Etsy Kesintileri ve Komisyon Listesi",
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        _feeBreakdownListBox.Dock = DockStyle.Fill;
        _feeBreakdownListBox.Font = new Font("Segoe UI", 9.5F);
        feeGroup.Controls.Add(_feeBreakdownListBox);
        panel.Controls.Add(feeGroup, 0, 1);

        // Recommendations & Break-Even Group
        var recoGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Başabaş Fiyatı, Hedef Satış Fiyatı ve AI Tavsiyeleri",
            Font = new Font("Segoe UI Semibold", 9.5F),
        };
        var recoTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(6) };
        recoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        recoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        recoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        recoTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _breakEvenLabel.Dock = DockStyle.Fill;
        _breakEvenLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _breakEvenLabel.ForeColor = UiStyle.TextDark;
        recoTable.Controls.Add(_breakEvenLabel, 0, 0);

        _optimalPriceLabel.Dock = DockStyle.Fill;
        _optimalPriceLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _optimalPriceLabel.ForeColor = UiStyle.PrimaryColor;
        recoTable.Controls.Add(_optimalPriceLabel, 0, 1);

        _offsiteComparisonLabel.Dock = DockStyle.Fill;
        _offsiteComparisonLabel.Font = new Font("Segoe UI", 9F);
        _offsiteComparisonLabel.ForeColor = UiStyle.TextMuted;
        recoTable.Controls.Add(_offsiteComparisonLabel, 0, 2);

        _recommendationsListBox.Dock = DockStyle.Fill;
        _recommendationsListBox.Font = new Font("Segoe UI", 9F);
        recoTable.Controls.Add(_recommendationsListBox, 0, 3);

        recoGroup.Controls.Add(recoTable);
        panel.Controls.Add(recoGroup, 0, 2);

        return panel;
    }

    private static void AddControlRow(TableLayoutPanel table, int row, string labelText, Control control)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = labelText,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiStyle.TextDark,
        };
        table.Controls.Add(label, 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static void AddKpiCard(TableLayoutPanel parent, int col, string title, Label valueLabel)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White, Margin = new Padding(col == 0 ? 0 : 4, 2, col == 1 ? 0 : 4, 4), Padding = new Padding(8, 4, 8, 4), CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI", 8.5F), ForeColor = UiStyle.TextMuted }, 0, 0);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "$0.00";
        valueLabel.Font = UiStyle.KpiValueFont;
        valueLabel.ForeColor = UiStyle.TextDark;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(valueLabel, 0, 1);

        parent.Controls.Add(panel, col, 0);
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

        // Auto-recalculate on change
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

        // Update KPI Badges
        _netProfitBadge.Text = $"${result.NetProfitUsd:F2}";
        _netProfitBadge.ForeColor = result.NetProfitUsd >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor;

        _profitMarginBadge.Text = $"%{result.ProfitMarginPercent:F1}  (ROI: %{result.ReturnOnInvestmentPercent:F1})";
        _profitMarginBadge.ForeColor = result.ProfitMarginPercent >= 25.0 ? UiStyle.SuccessColor : result.ProfitMarginPercent >= 15.0 ? UiStyle.AccentColor : UiStyle.DangerColor;

        _healthRatingBadge.Text = $"Durum: {result.HealthRating}";
        _healthRatingBadge.BackColor = result.HealthRating switch
        {
            "Mükemmel" => UiStyle.SuccessColor,
            "İyi (Sağlıklı)" => UiStyle.PrimaryColor,
            "Düşük Kâr" => UiStyle.AccentColor,
            _ => UiStyle.DangerColor,
        };

        // Fee Breakdown List
        _feeBreakdownListBox.Items.Clear();
        _feeBreakdownListBox.Items.Add($"  • İlan Ücreti (Listing Fee): ${result.Fees.ListingFeeUsd:F2}");
        _feeBreakdownListBox.Items.Add($"  • İşlem Komisyonu (Transaction Fee %6.5): ${result.Fees.TransactionFeeUsd:F2}");
        _feeBreakdownListBox.Items.Add($"  • Ödeme İşleme Ücreti (Payment Processing): ${result.Fees.PaymentProcessingFeeUsd:F2}");
        if (result.Fees.RegulatoryOperatingFeeUsd > 0)
        {
            _feeBreakdownListBox.Items.Add($"  • Yasal Düzenleme Ücreti (Regulatory Fee): ${result.Fees.RegulatoryOperatingFeeUsd:F2}");
        }
        if (result.Fees.OffsiteAdsFeeUsd > 0)
        {
            _feeBreakdownListBox.Items.Add($"  • Offsite Ads Dış Reklam Ücreti: ${result.Fees.OffsiteAdsFeeUsd:F2}");
        }
        if (result.Fees.CurrencyConversionFeeUsd > 0)
        {
            _feeBreakdownListBox.Items.Add($"  • Para Birimi Dönüştürme Ücreti (%2.5): ${result.Fees.CurrencyConversionFeeUsd:F2}");
        }
        _feeBreakdownListBox.Items.Add("----------------------------------------------------------------------------");
        _feeBreakdownListBox.Items.Add($"  👉 TOPLAM ETSY KESİNTİLERİ: ${result.Fees.TotalEtsyFeesUsd:F2}");
        _feeBreakdownListBox.Items.Add($"  👉 TOPLAM GİDERLER (Maliyetler + Kesintiler): ${result.TotalExpensesUsd:F2}");

        // Break-Even & Targets
        _breakEvenLabel.Text = $"🛑 Başabaş Fiyatı (Break-Even): ${result.BreakEvenItemPriceUsd:F2} (Bunun altı zarardır)";
        _optimalPriceLabel.Text = $"🎯 Hedef %{_targetMarginInput.Value} Kâr Marjı İçin İdeal Satış Fiyatı: ${result.RecommendedOptimalPriceUsd:F2}";
        _offsiteComparisonLabel.Text = input.OffsiteAds == OffsiteAdsMode.Disabled
            ? $"💡 Offsite Ads aktif olsaydı tahmini net kâr: ${result.OffsiteAdsComparisonProfitUsd:F2}"
            : $"💡 Offsite Ads olmasaydı net kâr: ${result.OffsiteAdsComparisonProfitUsd:F2}";

        // Recommendations List
        _recommendationsListBox.Items.Clear();
        foreach (var rec in result.Recommendations)
        {
            _recommendationsListBox.Items.Add($"  • {rec}");
        }
    }

    private static NumericUpDown CreateMoneyInput(decimal defaultValue) => new()
    {
        Minimum = 0,
        Maximum = 100000,
        DecimalPlaces = 2,
        Increment = 0.50m,
        Value = defaultValue,
    };

    private static NumericUpDown CreatePercentInput(decimal defaultValue = 30.0m) => new()
    {
        Minimum = 1,
        Maximum = 90,
        DecimalPlaces = 1,
        Increment = 1m,
        Value = defaultValue,
    };
}
