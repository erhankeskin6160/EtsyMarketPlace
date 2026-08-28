namespace SimilarProductsWinForms;

using System;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class OrderDetailsForm : Form
{
    private readonly SqliteProductCostRepository _repository = new();
    private readonly OrderFinancialSummary _order;
    
    private decimal _productionCost;
    private decimal _shippingCost;
    private decimal _packagingCost;

    public OrderDetailsForm(OrderFinancialSummary order)
    {
        _order = order;
        Text = $"🧾 Sipariş Detayları: #{order.ReceiptId}";
        Size = new Size(880, 660);
        MinimumSize = new Size(840, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;

        Load += OrderDetailsForm_Load;

        BuildLayout();
        UiStyle.ApplyTheme(this);
    }

    private async void OrderDetailsForm_Load(object? sender, EventArgs e)
    {
        var entry = await _repository.GetByIdAsync(_order.ListingId.ToString());
        if (entry != null)
        {
            _productionCost = entry.UnitCost;
            _shippingCost = entry.UnitShippingCost;
            _packagingCost = entry.UnitPackagingCost;
        }
        else
        {
            _productionCost = _order.ProductCost / Math.Max(1, _order.Quantity);
        }
        UpdateProfitLabel();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Bottom profit/cost section
        Controls.Add(mainLayout);

        // -- Top Section (Left: Earnings, Right: Fees)
        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.Controls.Add(topLayout, 0, 0);

        // Left Panel (Earnings)
        var pnlEarnings = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 10, 0),
            CardColor = Color.White,
            BorderColor = UiStyle.BorderColor
        };
        BuildEarningsPanel(pnlEarnings);
        topLayout.Controls.Add(pnlEarnings, 0, 0);

        // Right Panel (Fees)
        var pnlFees = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(10, 0, 0, 0),
            CardColor = Color.White,
            BorderColor = UiStyle.BorderColor
        };
        BuildFeesPanel(pnlFees);
        topLayout.Controls.Add(pnlFees, 1, 0);

        // -- Bottom Section (Profit & Cost)
        var pnlBottom = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(20, 16, 20, 16),
            Margin = new Padding(0, 16, 0, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        BuildBottomPanel(pnlBottom);
        mainLayout.Controls.Add(pnlBottom, 0, 1);
    }

    private void BuildEarningsPanel(Panel container)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        int row = 0;
        
        // Header
        AddRow(layout, "Müşteri Ödemesi", $"${_order.GrandTotal:N2}", row++, true, UiStyle.TextDark);
        
        // Items
        AddRow(layout, "Ürün Fiyatı", $"${_order.Subtotal:N2}", row++, false, UiStyle.TextMuted);
        AddRow(layout, "Kargo Ücreti", $"${_order.ShippingPrice:N2}", row++, false, UiStyle.TextMuted);
        if (_order.DiscountAmt > 0)
        {
            AddRow(layout, "Mağaza İndirimi", $"-${_order.DiscountAmt:N2}", row++, false, UiStyle.TextMuted);
        }
        
        // Subtotal (Subtotal + Shipping - Discount)
        decimal calcSubtotal = _order.Subtotal + _order.ShippingPrice - _order.DiscountAmt;
        AddRow(layout, "Vergi Öncesi Ara Toplam", $"${calcSubtotal:N2}", row++, false, UiStyle.TextDark);
        AddRow(layout, "Müşterinin Ödediği Vergi", $"${_order.TaxPaidByBuyer:N2}", row++, false, UiStyle.TextMuted);

        // Total Earned Label at top basically
        var lblEarned = new Label
        {
            Text = $"Etsy Net Geliri: ${(_order.GrandTotal - _order.EtsyFees - _order.OffsiteAdFee):N2}",
            Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = UiStyle.SuccessColor,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        container.Controls.Add(lblEarned);
        container.Controls.Add(layout);
        layout.BringToFront();
    }

    private void BuildFeesPanel(Panel container)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        int row = 0;
        
        // Header
        decimal totalFeesAndTax = _order.EtsyFees + _order.OffsiteAdFee;
        AddRow(layout, "Etsy Kesintileri ve Ücretler", $"-${totalFeesAndTax:N2}", row++, true, UiStyle.DangerColor);
        
        // Items
        if (_order.TaxPaidByBuyer > 0)
            AddRow(layout, "Alıcı Vergisi (Etsy alır)", $"-${_order.TaxPaidByBuyer:N2}", row++, false, UiStyle.TextMuted);
        
        if (_order.TransactionFee > 0)
            AddRow(layout, "İşlem Komisyonu (%6.5)", $"-${_order.TransactionFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.PaymentProcessingFee > 0)
            AddRow(layout, "Ödeme İşleme (%6.5 + 3 TL)", $"-${_order.PaymentProcessingFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.RegulatoryOperatingFee > 0)
            AddRow(layout, "Yasal İşlem Ücreti (%1.5)", $"-${_order.RegulatoryOperatingFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.VatOnFees > 0)
            AddRow(layout, "Hizmet KDV'si (%20)", $"-${_order.VatOnFees:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.ListingFee > 0)
            AddRow(layout, "İlan Yenileme", $"-${_order.ListingFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.OffsiteAdFee > 0)
            AddRow(layout, "Dış Reklam (Offsite Ads)", $"-${_order.OffsiteAdFee:N2}", row++, false, UiStyle.TextMuted);

        container.Controls.Add(layout);
    }

    private void BuildBottomPanel(Panel container)
    {
        var pnlProfit = new Panel { Dock = DockStyle.Fill, AutoSize = true, Name = "pnlProfit" };
        container.Controls.Add(pnlProfit);
        
        UpdateProfitLabel(pnlProfit);
    }
    
    private void UpdateProfitLabel(Panel? pnl = null)
    {
        pnl ??= Controls.Find("pnlProfit", true).FirstOrDefault() as Panel;
        if (pnl == null) return;
        
        pnl.Controls.Clear();
        
        decimal totalUnit = _productionCost + _shippingCost + _packagingCost;
        decimal totalProductCost = Math.Round(totalUnit * Math.Max(1, _order.Quantity), 2);
        decimal totalShipping = Math.Round(_shippingCost * Math.Max(1, _order.Quantity), 2);
        
        decimal netUSD = _order.GrandTotal - _order.EtsyFees - _order.OffsiteAdFee - totalProductCost;
        decimal netTRY = Math.Round(netUSD * _order.ExchangeRate, 2);
        
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0)
        };

        var lblProfit = new Label 
        { 
            Text = $"GERÇEK NET KÂR: ${netUSD:N2}  (₺{netTRY:N2})", 
            AutoSize = true,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = netUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor,
            Margin = new Padding(0, 0, 0, 6)
        };
        layout.Controls.Add(lblProfit);

        var subInfo = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4)
        };

        var lblCostBreakdown = new Label
        {
            Text = $"Üretim: ${_productionCost * _order.Quantity:N2} | 🚚 Kargo: ${totalShipping:N2} | Paketleme: ${_packagingCost * _order.Quantity:N2}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = UiStyle.TextMuted,
            Padding = new Padding(0, 4, 12, 0)
        };
        subInfo.Controls.Add(lblCostBreakdown);

        if (_order.HasInvoice)
        {
            var btnInvoice = new Button
            {
                Text = "📄 Kargo Faturasını Aç",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 8, 0)
            };
            btnInvoice.FlatAppearance.BorderSize = 0;
            btnInvoice.Click += (_, _) => InvoiceStorageService.OpenInvoice(_order.InvoiceFilePath);
            subInfo.Controls.Add(btnInvoice);
        }

        layout.Controls.Add(subInfo);
        pnl.Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel panel, string title, string value, int row, bool isHeader, Color color)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, isHeader ? 40 : 30));
        
        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(isHeader ? "Segoe UI Semibold" : "Segoe UI", isHeader ? 11F : 9.5F),
            ForeColor = color
        };
        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(isHeader ? "Segoe UI Semibold" : "Segoe UI", isHeader ? 11F : 9.5F),
            ForeColor = color
        };
        
        panel.Controls.Add(lblTitle, 0, row);
        panel.Controls.Add(lblValue, 1, row);
    }
}
